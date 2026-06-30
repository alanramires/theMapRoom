using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // Min distance from delivery target that justifies towing a unit (slightly lower than
    // MinDistanceForTransportSlot because artillery has range and is useful before arriving).
    private const int TowDeliveryThreshold = 5;

    // -------------------------------------------------------------------------
    // Tow Shuttle — transporter with reboque-type slots ferries heavy units
    // (e.g. field artillery) toward useful sectors when no infantry to ferry.
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideTowShuttleAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        if (paths == null || paths.Count == 0)
            return null;
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        UnitManager towCandidate = FindBestTowCandidate(unit, snapshot, plan, fromCell,
            out Vector3Int towCandidateCell, out Vector3Int deliveryTarget);
        if (towCandidate == null) return null;

        // Already adjacent — stay in place; artillery will embark this same iteration.
        if (SectorManager.HexDistance(fromCell, towCandidateCell) <= 1f)
        {
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} tow-shuttle — adjacente a #{towCandidate.InstanceId}@{towCandidateCell}, aguarda embarque");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        // Head straight toward the artillery — it can't walk to meet us.
        Vector3Int moveTarget = FindTransportMove(unit, fromCell, towCandidateCell, paths, occupied, snapshot.AITeam);
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} tow-shuttle — reboca {towCandidate.InstanceId}@{towCandidateCell} → {deliveryTarget} via {moveTarget}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

    // -------------------------------------------------------------------------
    // Candidate selection
    // -------------------------------------------------------------------------

    private UnitManager FindBestTowCandidate(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int transporterCell,
        out Vector3Int bestCandidateCell,
        out Vector3Int bestDeliveryCell)
    {
        bestCandidateCell = transporterCell;
        bestDeliveryCell = Vector3Int.zero;

        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null)
            return null;

        UnitManager best = null;
        float bestScore = float.MinValue;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == transporter) continue;
            if (candidate.TeamId != snapshot.AITeam || candidate.IsDead || candidate.IsEmbarked || candidate.HasActed) continue;
            if (!candidate.TryGetUnitData(out UnitData candidateData)) continue;

            // Capturador units are handled by the regular infantry shuttle.
            if (candidateData.roles != null && candidateData.roles.Count > 0
                && candidateData.roles[0] == UnitRole.Capturador) continue;

            // Fora da invasão a unidade de reboque não embarca (TryDecideFireSupportEmbarkAction
            // bloqueia), então sair para rebocá-la só desperdiça movimento do supridor — ignora-a.
            if (!snapshot.IsInvading && UnitNeedsTow(candidate)) continue;

            // Slot compatibility check — reboque skill requirement verified here via PodeEmbarcarSensor.
            if (FindFittingSlotIndex(transporter, transporterData, candidate, candidateData) < 0) continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition; candidateCell.z = 0;
            if (!TryFindTowDeliveryTarget(candidate, candidateCell, snapshot, plan, out Vector3Int deliveryCell)) continue;

            float distToTarget = SectorManager.HexDistance(candidateCell, deliveryCell);
            if (distToTarget < TowDeliveryThreshold) continue; // already close enough, no tow needed

            float distToCandidate = SectorManager.HexDistance(transporterCell, candidateCell);
            float score = distToTarget * 80f - distToCandidate * 60f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                bestCandidateCell = candidateCell;
                bestDeliveryCell = deliveryCell;
            }
        }

        return best;
    }

    // -------------------------------------------------------------------------
    // Delivery target — shared with TryDecideAssaultEmbarkAction
    // -------------------------------------------------------------------------

    // Returns true if a delivery target was found (including cell (0,0,0) which is valid).
    // Returns false only when no target could be determined at all.
    internal bool TryFindTowDeliveryTarget(
        UnitManager candidate,
        Vector3Int candidateCell,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        out Vector3Int deliveryTarget)
    {
        deliveryTarget = Vector3Int.zero;

        // Use the unit's own plan slot as the authoritative delivery target.
        // Do NOT call ResolveUnitObjectiveCell here — its HQ fallback returns a non-zero value
        // even when no slot exists, causing the fallback logic below to be skipped.
        if (plan != null)
        {
            foreach (SectorObjective obj in plan.Objectives)
                foreach (SlotNeed slot in obj.Slots)
                    if (slot.Filled && slot.AssignedUnitId == candidate.InstanceId)
                    {
                        ConstructionManager tgt = FindCapturableInSector(obj.Sector, snapshot.AITeam);
                        if (tgt != null)
                        {
                            Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;
                            Debug.Log($"{TL("TowTarget")} #{candidate.InstanceId} setor={obj.Sector} capturable={tc}");
                            deliveryTarget = tc; return true;
                        }
                        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo si))
                        {
                            Vector3Int rc = si.RepresentativeCell; rc.z = 0;
                            Debug.Log($"{TL("TowTarget")} #{candidate.InstanceId} setor={obj.Sector} repCell={rc} (sem capturable)");
                            deliveryTarget = rc; return true;
                        }
                        Debug.LogWarning($"{TL("TowTarget")} #{candidate.InstanceId} setor={obj.Sector} sem sectorInfo");
                        return false;
                    }
        }

        // Rogue unit — no plan slot. Pick the nearest sector with capturers that is
        // not under active threat (artillery stays behind the front line).
        bool found = false;
        float bestScore = float.MinValue;

        if (plan != null)
        {
            foreach (SectorObjective obj in plan.Objectives)
            {
                if (!HasFilledSlot(obj, UnitRole.Capturador)) continue;

                Vector3Int objCell;
                ConstructionManager tgt = FindCapturableInSector(obj.Sector, snapshot.AITeam, candidateCell);
                if (tgt != null) { objCell = tgt.CurrentCellPosition; objCell.z = 0; }
                else if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo si))
                { objCell = si.RepresentativeCell; objCell.z = 0; }
                else continue;

                // Never drop artillery into active combat — skip sectors where enemies
                // are within close range of the target building.
                if (HasNearbyVisibleEnemy(objCell, snapshot.AITeam, 2)) continue;

                float dist = SectorManager.HexDistance(candidateCell, objCell);
                float score = -dist * 40f; // prefer nearest safe sector
                if (score > bestScore) { bestScore = score; deliveryTarget = objCell; found = true; }
            }
        }

        if (found) return true;

        if (snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition; hq.z = 0;
            deliveryTarget = hq; return true;
        }

        return false;
    }

    // Backward-compat wrapper for callers that can't be null (keep Vector3Int.zero meaning "not found").
    internal Vector3Int FindTowDeliveryTarget(
        UnitManager candidate,
        Vector3Int candidateCell,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        TryFindTowDeliveryTarget(candidate, candidateCell, snapshot, plan, out Vector3Int result);
        return result;
    }
}
