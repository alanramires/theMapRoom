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

        // No Hard, a retaguarda pode j� estar preenchendo o segundo slot do mesmo objetivo.
        // Portanto, aus�ncia de capturadores livres n�o significa aus�ncia de sucessor.
        if (freeCapturers.Count == 0 && !hardMode) return;

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

            // Blitzkrieg: a ponta de lança pode estar EM CIMA do prédio parcial, bloqueando a
            // célula. Nesse caso o seguidor do eixo nunca "alcança" o alvo (célula ocupada) e o
            // handoff era pulado — a ponta ficava terminando a captura. Se há Foxtrot à frente,
            // aceitamos um seguidor que alcance um VIZINHO do prédio: ele entra assim que a ponta
            // vaga a célula (grp=0), e a ponta segue o eixo. Sem seguidor, mantém o fallback.
            Vector3Int assignedPos0 = assignedUnit.CurrentCellPosition; assignedPos0.z = 0;
            bool frontBlocksTarget = hardMode && assignedPos0 == targetCell
                && ComputeBlitzkriegForwardSector(obj.Sector, aiTeam) != ConstructionSector.None;

            UnitManager substitute   = null;
            SlotNeed substituteExistingSlot = null;
            float       bestSubScore = float.MinValue;

            var handoffCandidates = new List<UnitManager>(freeCapturers);
            if (hardMode)
            {
                foreach (SlotNeed slot in obj.Slots)
                {
                    if (!slot.Filled || slot == filledSlot || slot.Role != UnitRole.Capturador)
                        continue;
                    UnitManager trailing = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                    if (trailing != null && !trailing.IsUnderRepair && !handoffCandidates.Contains(trailing))
                        handoffCandidates.Add(trailing);
                }
            }

            foreach (UnitManager candidate in handoffCandidates)
            {
                if (!SimulateCaptureSensor(candidate, targetCell, out _)) continue;

                SlotNeed candidateExistingSlot = null;
                foreach (SlotNeed slot in obj.Slots)
                    if (slot.Filled && slot.Role == UnitRole.Capturador
                        && slot.AssignedUnitId == candidate.InstanceId)
                    {
                        candidateExistingSlot = slot;
                        break;
                    }

                Dictionary<Vector3Int, List<Vector3Int>> candidatePaths =
                    UnitMovementPathRules.CalcularCaminhosValidos(
                        boardTilemap, candidate,
                        Mathf.Max(0, candidate.RemainingMovementPoints), terrainDatabase);
                bool reachesTarget = candidatePaths != null && (candidatePaths.ContainsKey(targetCell)
                    || (frontBlocksTarget && CandidateReachesTargetNeighbor(candidatePaths, targetCell)));
                if (!reachesTarget && candidateExistingSlot == null) continue;

                int capturePower = PodeCapturarSensor.GetCapturePower(candidate);
                bool completesCapture = pts + capturePower >= max;
                Vector3Int cc = candidate.CurrentCellPosition; cc.z = 0;
                float dist = SectorManager.TryGetLandMovementDistance(cc, targetCell, out int landDistance)
                    ? landDistance : SectorManager.HexDistance(cc, targetCell);
                float score = capturePower * 100f - dist * 20f;
                if (completesCapture) score += 500f;
                if (reachesTarget) score += 250f;

                if (score > bestSubScore)
                {
                    bestSubScore = score;
                    substitute = candidate;
                    substituteExistingSlot = candidateExistingSlot;
                }
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
                ConstructionSector fwdSec = hardMode
                    ? ComputeBlitzkriegForwardSector(obj.Sector, aiTeam)
                    : ComputeForwardNeighborSector(obj.Sector, aiTeam);
                if (fwdSec != default) plan.VacaterForwardSectors.Add(fwdSec);

                swapFromSlot.Filled         = false;
                swapFromSlot.AssignedUnitId = -1;

                obj.TryFillSlot(UnitRole.Capturador, swapCandidate.InstanceId);
                obj.Status                     = ObjectiveStatus.PartialReadyForHandoff;
                obj.HandoffEligible            = true;
                obj.PreferredHandoffFromUnitId = assignedUnit.InstanceId;
                ApplyPlanHUD(swapCandidate, obj);

                bool swapCompletes = pts + PodeCapturarSensor.GetCapturePower(swapCandidate) >= max;
                Debug.Log($"{TL("Handoff")}[Swap] Unit{swapCandidate.InstanceId} " +
                          $"({swapFromObj.Sector} pri={swapFromObj.Priority}→{obj.Sector} pri={obj.Priority}) " +
                          $"já no prédio; Unit{assignedUnit.InstanceId} livre (era {assignedDist:F0}h)" +
                          $"{(swapCompletes ? " completa captura" : "")}");
                continue;
            }

            bool subCompletes = pts + PodeCapturarSensor.GetCapturePower(substitute) >= max;
            Debug.Log($"{TL("Handoff")} Unit{assignedUnit.InstanceId} hp={assignedUnit.CurrentHP} avança; " +
                      $"Unit{substitute.InstanceId} hp={substitute.CurrentHP} herda {obj.Sector} " +
                      $"({pts}/{max}){(subCompletes ? " → completa" : "")}" +
                      $"{(frontBlocksTarget ? " [blitz: ponta sai de cima, seguidor do eixo assume]" : "")}");

            filledSlot.Filled         = false;
            filledSlot.AssignedUnitId = -1;
            assignedUnit.ClearAIAssignedPlan();
            plan.HandoffVacaterIds.Add(assignedUnit.InstanceId);
            ConstructionSector fwdSector = hardMode
                ? ComputeBlitzkriegForwardSector(obj.Sector, aiTeam)
                : ComputeForwardNeighborSector(obj.Sector, aiTeam);
            if (fwdSector != default) plan.VacaterForwardSectors.Add(fwdSector);

            if (substituteExistingSlot == null)
                obj.TryFillSlot(UnitRole.Capturador, substitute.InstanceId);
            obj.Status                     = ObjectiveStatus.PartialReadyForHandoff;
            obj.HandoffEligible            = true;
            obj.PreferredHandoffFromUnitId = assignedUnit.InstanceId;
            ApplyPlanHUD(substitute, obj);

            if (substituteExistingSlot == null)
                freeCapturers.Remove(substitute);
            assignedIds.Add(substitute.InstanceId);
        }
    }

    // Alcance máximo (hexes entre setores) para a cascata suprimir o vizinho. Equivale à
    // "ponte" de mover ~3 hexes e capturar; acima disso o vizinho precisa da própria unidade.
    private const float MaxCascadeBridgeDistance = 3f;

    private void MarkCascadeNeighbor1(ConstructionSector sector, HashSet<ConstructionSector> covered, TeamId aiTeam, HashSet<ConstructionSector> vacaterProtected = null, AIRallyPlanContext rallyContext = default)
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

        // A cascata só pode cobrir o vizinho se ele estiver dentro do alcance de "ponte"
        // (a unidade captura o setor atual e no(s) turno(s) seguinte(s) faz a ponte de
        // ~3 hexes até o vizinho). Acima disso o vizinho precisa da própria unidade —
        // suprimi-lo deixa capturadores ociosos virando rogue.
        if (candidateDist > MaxCascadeBridgeDistance)
        {
            Debug.Log($"{TL("Plan")} cascata: {sector} → {candidate} ({candidateDist:F1}h) fora do alcance de ponte (>{MaxCascadeBridgeDistance:F0}h), não cobre");
            return;
        }

        if (vacaterProtected != null && vacaterProtected.Contains(candidate))
        {
            Debug.Log($"{TL("Plan")} cascata: {sector} → {candidate} protegido por vacater, supressão ignorada");
            return;
        }

        if (IsEnemyHQRallySector(rallyContext, candidate))
        {
            Debug.Log($"{TL("Plan")} cascata: {sector} -> {candidate} ignorada; rally point de invasao");
            return;
        }

        covered.Add(candidate);
        Debug.Log($"{TL("Plan")} cascata: {sector} → {candidate} ({candidateDist:F1}h)");
    }

    private void MarkSelectionCascadeNeighbor(ConstructionSector sector, HashSet<ConstructionSector> covered, TeamId aiTeam, AIRallyPlanContext rallyContext = default)
    {
        if (covered == null)
            return;

        ConstructionSector forward = ComputeForwardNeighborSector(sector, aiTeam);
        if (forward == ConstructionSector.None)
            return;

        if (IsEnemyHQRallySector(rallyContext, forward))
        {
            Debug.Log($"{TL("Plan")} cascata inicial: {sector} nao cobre {forward}; rally point de invasao");
            return;
        }

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

    // Verdadeiro se o candidato consegue parar num hex adjacente ao prédio parcial neste turno.
    // Usado no handoff blitz quando a ponta de lança ocupa a própria célula do alvo: o seguidor
    // não pode parar sobre o prédio (ocupado), mas fica pronto ao lado para assumir assim que a
    // ponta vaga. A célula do alvo em si é liberada em Phase 2 (a ponta é grp=0 e sai primeiro).
    private readonly List<Vector3Int> handoffNeighborBuffer = new List<Vector3Int>(6);
    private bool CandidateReachesTargetNeighbor(
        Dictionary<Vector3Int, List<Vector3Int>> candidatePaths, Vector3Int targetCell)
    {
        if (candidatePaths == null) return false;
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, targetCell, handoffNeighborBuffer);
        foreach (Vector3Int raw in handoffNeighborBuffer)
        {
            Vector3Int neighbor = raw; neighbor.z = 0;
            if (candidatePaths.ContainsKey(neighbor)) return true;
        }
        return false;
    }
}
