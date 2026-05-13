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

            // Slot compatibility check — reboque skill requirement verified here via PodeEmbarcarSensor.
            if (FindFittingSlotIndex(transporter, transporterData, candidate, candidateData) < 0) continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition; candidateCell.z = 0;
            Vector3Int deliveryCell = FindTowDeliveryTarget(candidate, candidateCell, snapshot, plan);
            if (deliveryCell == Vector3Int.zero) continue;

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

    // Finds where to deliver a towed unit.
    // Primary: the unit's own plan-assigned objective (the planner already decided).
    // Fallback (rogue unit): highest-risk offensive sector with a capturer, then enemy HQ.
    internal Vector3Int FindTowDeliveryTarget(
        UnitManager candidate,
        Vector3Int candidateCell,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        // Use the unit's own plan assignment as the authoritative delivery target.
        Vector3Int assigned = ResolveUnitObjectiveCell(candidate, plan, snapshot);
        if (assigned != Vector3Int.zero) return assigned;

        // Rogue unit — no plan slot. Pick the most contested offensive sector.
        Vector3Int best = Vector3Int.zero;
        float bestScore = float.MinValue;

        if (plan != null)
        {
            foreach (SectorObjective obj in plan.Objectives)
            {
                if (!HasFilledSlot(obj, UnitRole.Capturador)) continue;

                Vector3Int objCell;
                ConstructionManager tgt = FindCapturableInSector(obj.Sector, snapshot.AITeam);
                if (tgt != null) { objCell = tgt.CurrentCellPosition; objCell.z = 0; }
                else if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo si))
                { objCell = si.RepresentativeCell; objCell.z = 0; }
                else continue;

                TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo sInfo);
                float risk = sInfo != null ? sInfo.GetRiskRatioFor(snapshot.AITeam) : 0f;
                float dist = SectorManager.HexDistance(candidateCell, objCell);
                float score = dist * 40f + risk * 200f;
                if (score > bestScore) { bestScore = score; best = objCell; }
            }
        }

        if (best != Vector3Int.zero) return best;

        if (snapshot.EnemyHQ != null)
        { Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition; hq.z = 0; return hq; }

        return Vector3Int.zero;
    }
}
