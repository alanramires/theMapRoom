using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Handoff: capturador mais saudável herda objetivo parcial;
    // Cascade: suprime o setor vizinho forward durante distribuição inicial.
    // -------------------------------------------------------------------------

    private void EvaluateCaptureHandoffs(TeamObjectivePlan plan, TeamId aiTeam)
    {
        var assignedIds = new HashSet<int>();
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled) assignedIds.Add(slot.AssignedUnitId);

        List<UnitManager> allCapturers = GetAvailableCapturers(aiTeam);
        var freeCapturers = new List<UnitManager>();
        foreach (UnitManager u in allCapturers)
            if (!assignedIds.Contains(u.InstanceId)) freeCapturers.Add(u);

        if (freeCapturers.Count == 0) return;

        for (int i = 0; i < plan.Objectives.Count; i++)
        {
            SectorObjective obj = plan.Objectives[i];

            if (obj.Status == ObjectiveStatus.Defending) continue;

            SlotNeed filledSlot = null;
            foreach (SlotNeed s in obj.Slots)
                if (s.Filled && s.Role == UnitRole.Capturador) { filledSlot = s; break; }
            if (filledSlot == null) continue;

            UnitManager assignedUnit = FindActiveUnit(filledSlot.AssignedUnitId, aiTeam);
            if (assignedUnit == null || assignedUnit.IsUnderRepair) continue;

            ConstructionManager target = FindCapturableInSector(obj.Sector, aiTeam);
            if (target == null) continue;
            int pts = target.CurrentCapturePoints;
            int max = target.CapturePointsMax;
            if (pts <= 0 || pts >= max) continue;

            if (!HasOpenObjectiveOtherThan(plan, obj)) continue;

            Vector3Int targetCell = target.CurrentCellPosition; targetCell.z = 0;
            if (HasEnemyNearCell(targetCell, aiTeam)) continue;

            UnitManager substitute   = null;
            float       bestSubScore = float.MinValue;

            foreach (UnitManager candidate in freeCapturers)
            {
                if (!SimulateCaptureSensor(candidate, targetCell, out _)) continue;

                Dictionary<Vector3Int, List<Vector3Int>> candidatePaths =
                    UnitMovementPathRules.CalcularCaminhosValidos(
                        boardTilemap, candidate,
                        Mathf.Max(0, candidate.RemainingMovementPoints), terrainDatabase);
                if (candidatePaths == null || !candidatePaths.ContainsKey(targetCell)) continue;

                bool completesCapture = pts + candidate.CurrentHP >= max;
                Vector3Int cc = candidate.CurrentCellPosition; cc.z = 0;
                float dist  = SectorManager.HexDistance(cc, targetCell);
                float score = candidate.CurrentHP * 100f - dist * 20f;
                if (completesCapture) score += 500f;

                if (score > bestSubScore) { bestSubScore = score; substitute = candidate; }
            }

            if (substitute == null)
            {
                const float SwapMinAssignedDist = 4f;

                Vector3Int assignedPos = assignedUnit.CurrentCellPosition; assignedPos.z = 0;
                float assignedDist = SectorManager.TryGetLandMovementDistance(assignedPos, targetCell, out int adSwap)
                    ? adSwap : SectorManager.HexDistance(assignedPos, targetCell);

                UnitManager     swapCandidate = null;
                SectorObjective swapFromObj   = null;
                SlotNeed        swapFromSlot  = null;

                if (assignedDist >= SwapMinAssignedDist)
                {
                    foreach (SectorObjective otherObj in plan.Objectives)
                    {
                        if (otherObj == obj) continue;
                        if (otherObj.Priority < obj.Priority) continue;

                        foreach (SlotNeed otherSlot in otherObj.Slots)
                        {
                            if (!otherSlot.Filled || otherSlot.Role != UnitRole.Capturador) continue;
                            UnitManager cand = FindActiveUnit(otherSlot.AssignedUnitId, aiTeam);
                            if (cand == null || cand.IsUnderRepair) continue;
                            if (!SimulateCaptureSensor(cand, targetCell, out _)) continue;

                            var swapPaths = UnitMovementPathRules.CalcularCaminhosValidos(
                                boardTilemap, cand,
                                Mathf.Max(0, cand.RemainingMovementPoints), terrainDatabase);
                            if (swapPaths == null || !swapPaths.ContainsKey(targetCell)) continue;

                            swapCandidate = cand;
                            swapFromObj   = otherObj;
                            swapFromSlot  = otherSlot;
                            break;
                        }
                        if (swapCandidate != null) break;
                    }
                }

                if (swapCandidate == null)
                {
                    Debug.Log($"{TL("Handoff")}[Skip] sem substituto para {obj.Sector} ({pts}/{max})" +
                              $" assignedDist={assignedDist:F0}h");
                    continue;
                }

                filledSlot.Filled         = false;
                filledSlot.AssignedUnitId = -1;
                assignedUnit.ClearAIAssignedPlan();
                plan.HandoffVacaterIds.Add(assignedUnit.InstanceId);
                ConstructionSector fwdSec = ComputeForwardNeighborSector(obj.Sector, aiTeam);
                if (fwdSec != default) plan.VacaterForwardSectors.Add(fwdSec);

                swapFromSlot.Filled         = false;
                swapFromSlot.AssignedUnitId = -1;

                obj.TryFillSlot(UnitRole.Capturador, swapCandidate.InstanceId);
                obj.Status                     = ObjectiveStatus.PartialReadyForHandoff;
                obj.HandoffEligible            = true;
                obj.PreferredHandoffFromUnitId = assignedUnit.InstanceId;
                ApplyPlanHUD(swapCandidate, obj);

                bool swapCompletes = pts + swapCandidate.CurrentHP >= max;
                Debug.Log($"{TL("Handoff")}[Swap] Unit{swapCandidate.InstanceId} " +
                          $"({swapFromObj.Sector} pri={swapFromObj.Priority}→{obj.Sector} pri={obj.Priority}) " +
                          $"já no prédio; Unit{assignedUnit.InstanceId} livre (era {assignedDist:F0}h)" +
                          $"{(swapCompletes ? " completa captura" : "")}");
                continue;
            }

            bool subCompletes = pts + substitute.CurrentHP >= max;
            Debug.Log($"{TL("Handoff")} Unit{assignedUnit.InstanceId} hp={assignedUnit.CurrentHP} avança; " +
                      $"Unit{substitute.InstanceId} hp={substitute.CurrentHP} herda {obj.Sector} " +
                      $"({pts}/{max}){(subCompletes ? " → completa" : "")}");

            filledSlot.Filled         = false;
            filledSlot.AssignedUnitId = -1;
            assignedUnit.ClearAIAssignedPlan();
            plan.HandoffVacaterIds.Add(assignedUnit.InstanceId);
            ConstructionSector fwdSector = ComputeForwardNeighborSector(obj.Sector, aiTeam);
            if (fwdSector != default) plan.VacaterForwardSectors.Add(fwdSector);

            obj.TryFillSlot(UnitRole.Capturador, substitute.InstanceId);
            obj.Status                     = ObjectiveStatus.PartialReadyForHandoff;
            obj.HandoffEligible            = true;
            obj.PreferredHandoffFromUnitId = assignedUnit.InstanceId;
            ApplyPlanHUD(substitute, obj);

            freeCapturers.Remove(substitute);
            assignedIds.Add(substitute.InstanceId);
        }
    }

    private void MarkCascadeNeighbor1(ConstructionSector sector, HashSet<ConstructionSector> covered, TeamId aiTeam, HashSet<ConstructionSector> vacaterProtected = null)
    {
        if (!SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info)) return;

        float myHQDist = info.GetDistanceToHQ(aiTeam);

        ConstructionSector candidate     = ConstructionSector.None;
        float              candidateDist = float.MaxValue;

        if (info.ClosestNeighbor1 != default
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor1, out SectorManager.SectorInfo n1)
            && n1.GetDistanceToHQ(aiTeam) > myHQDist)
        {
            candidate     = info.ClosestNeighbor1;
            candidateDist = info.ClosestNeighbor1Distance;
        }
        else if (info.ClosestNeighbor2 != default
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor2, out SectorManager.SectorInfo n2)
            && n2.GetDistanceToHQ(aiTeam) > myHQDist)
        {
            candidate     = info.ClosestNeighbor2;
            candidateDist = info.ClosestNeighbor2Distance;
        }

        if (candidate == ConstructionSector.None) return;

        if (vacaterProtected != null && vacaterProtected.Contains(candidate))
        {
            Debug.Log($"{TL("Plan")} cascata: {sector} → {candidate} protegido por vacater, supressão ignorada");
            return;
        }

        covered.Add(candidate);
        Debug.Log($"{TL("Plan")} cascata: {sector} → {candidate} ({candidateDist:F1}h)");
    }

    private void MarkSelectionCascadeNeighbor(ConstructionSector sector, HashSet<ConstructionSector> covered, TeamId aiTeam)
    {
        if (covered == null)
            return;

        ConstructionSector forward = ComputeForwardNeighborSector(sector, aiTeam);
        if (forward == ConstructionSector.None)
            return;

        covered.Add(forward);
        Debug.Log($"{TL("Plan")} cascata inicial: {sector} cobre {forward}");
    }

    private static ConstructionSector ComputeForwardNeighborSector(ConstructionSector sector, TeamId aiTeam)
    {
        if (TryGetPrimaryCampaignSuccessor(sector, aiTeam, out ConstructionSector campaignForward))
            return campaignForward;

        if (!SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info)) return ConstructionSector.None;
        float myHQDist = info.GetDistanceToHQ(aiTeam);
        if (info.ClosestNeighbor1 != default
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor1, out SectorManager.SectorInfo n1)
            && n1.GetDistanceToHQ(aiTeam) > myHQDist)
            return info.ClosestNeighbor1;
        if (info.ClosestNeighbor2 != default
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor2, out SectorManager.SectorInfo n2)
            && n2.GetDistanceToHQ(aiTeam) > myHQDist)
            return info.ClosestNeighbor2;
        return ConstructionSector.None;
    }

    private static bool HasOpenObjectiveOtherThan(TeamObjectivePlan plan, SectorObjective exclude)
    {
        foreach (SectorObjective obj in plan.Objectives)
            if (obj != exclude && obj.HasOpenSlot(UnitRole.Capturador)) return true;
        return false;
    }
}
