using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Planejamento de objetivos de captura
    // -------------------------------------------------------------------------

    private void BuildObjectivePlan(AIWorldSnapshot snapshot)
    {
        TeamId aiTeam = snapshot.AITeam;
        TeamObjectivePlan plan = ObjectiveManager.GetOrCreatePlanForTeam(aiTeam);

        // Passo 1: valida objetivos existentes
        for (int i = plan.Objectives.Count - 1; i >= 0; i--)
        {
            SectorObjective obj = plan.Objectives[i];
            if (FindCapturableInSector(obj.Sector, aiTeam) == null)
            {
                ClearObjectiveHUD(obj);
                plan.Objectives.RemoveAt(i);
                continue;
            }
            foreach (SlotNeed slot in obj.Slots)
            {
                if (!slot.Filled) continue;
                if (FindActiveUnit(slot.AssignedUnitId, aiTeam) == null)
                {
                    slot.Filled = false;
                    slot.AssignedUnitId = -1;
                }
            }
        }

        // Passo 2: adiciona objetivos para setores ainda não cobertos
        IReadOnlyList<SectorManager.SectorInfo> allSectors = SectorManager.GetAllSectorInfos();
        foreach (SectorManager.SectorInfo info in allSectors)
        {
            if (info.IsFullyControlled && info.ControllingTeam == aiTeam) continue;
            bool hasCapturable = false;
            foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
                if (c.OwnerTeam != aiTeam) { hasCapturable = true; break; }
            if (!hasCapturable) continue;
            if (plan.GetObjectiveForSector(info.Sector) != null) continue;

            SectorObjective obj = new SectorObjective
            {
                Sector       = info.Sector,
                AssignedTeam = aiTeam,
                Status       = ObjectiveStatus.Pending,
                Priority     = CalculateSectorPriority(info, aiTeam),
            };
            int slots = info.GetRiskLevelFor(aiTeam) == SectorManager.SectorRiskLevel.High ? 2 : 1;
            for (int s = 0; s < slots; s++)
                obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
            plan.Objectives.Add(obj);
        }

        // Passo 3: recalcula prioridades, ordena e renumera
        foreach (SectorObjective obj in plan.Objectives)
            if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo inf))
                obj.Priority = CalculateSectorPriority(inf, aiTeam);

        plan.Objectives.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        for (int i = 0; i < plan.Objectives.Count; i++)
            plan.Objectives[i].Priority = i + 1;

        // Passo 4: coleta IDs já atribuídos (sticky — não serão remexidos)
        var assignedIds = new HashSet<int>();
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled) assignedIds.Add(slot.AssignedUnitId);

        // Passo 5: atribui capturadores livres com consciência de posição
        plan.RogueUnitIds.Clear();
        List<UnitManager> allCapturers  = GetAvailableCapturers(aiTeam);
        List<UnitManager> freeCapturers = new List<UnitManager>();
        foreach (UnitManager u in allCapturers)
            if (!assignedIds.Contains(u.InstanceId)) freeCapturers.Add(u);

        // 5a: captura imediata — unidade já está em cima de um prédio capturável
        var immediateList = new List<UnitManager>();
        foreach (UnitManager u in freeCapturers)
        {
            Vector3Int uCell = u.CurrentCellPosition; uCell.z = 0;
            if (!SimulateCaptureSensor(u, uCell, out ConstructionManager bldg)) continue;
            SectorObjective obj = plan.GetObjectiveForSector(bldg.Sector);
            if (obj == null || !obj.HasOpenSlot(UnitRole.Capturador)) continue;
            obj.TryFillSlot(UnitRole.Capturador, u.InstanceId);
            obj.Status = ObjectiveStatus.Pursuing;
            ApplyPlanHUD(u, obj);
            immediateList.Add(u);
            Debug.Log($"[AI][Plan] {u.InstanceId} já está em {bldg.Sector} → captura imediata");
        }
        foreach (UnitManager u in immediateList) freeCapturers.Remove(u);

        // 5b: atribuição ótima por backtracking (minimiza distância total)
        // Para N ≤ 8 capturadores e ≤ 8 objetivos abertos, o espaço de busca é trivial
        // (ex: 3 unidades × 7 objetivos = P(7,3) = 210 combinações).
        var assignableObjs = new List<(SectorObjective obj, Vector3Int cell)>();
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (!obj.HasOpenSlot(UnitRole.Capturador)) continue;
            ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
            if (tgt == null) continue;
            Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;
            assignableObjs.Add((obj, tc));
        }

        int nu = Mathf.Min(freeCapturers.Count, assignableObjs.Count);
        if (nu > 0)
        {
            int[] bestAssign = new int[nu];
            float bestCost   = float.MaxValue;
            SolveAssignment(freeCapturers, assignableObjs,
                new bool[assignableObjs.Count], new int[nu],
                0, nu, 0f, ref bestCost, ref bestAssign);

            for (int i = 0; i < nu; i++)
            {
                UnitManager u       = freeCapturers[i];
                SectorObjective obj = assignableObjs[bestAssign[i]].obj;
                obj.TryFillSlot(UnitRole.Capturador, u.InstanceId);
                obj.Status = ObjectiveStatus.Pursuing;
                ApplyPlanHUD(u, obj);
            }
            freeCapturers.RemoveRange(0, nu);
        }

        foreach (UnitManager u in freeCapturers)
            plan.RogueUnitIds.Add(u.InstanceId);

        // Passo 6: reaplica HUD para atribuições anteriores
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && !freeCapturers.Exists(u => u.InstanceId == slot.AssignedUnitId))
                {
                    UnitManager u = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                    if (u != null) ApplyPlanHUD(u, obj);
                }

        int totalAssigned = 0;
        var planLog = new System.Text.StringBuilder();
        planLog.AppendLine($"[AI][Plan] {aiTeam} — {plan.Objectives.Count} objetivos:");
        foreach (SectorObjective obj in plan.Objectives)
        {
            foreach (SlotNeed slot in obj.Slots)
            {
                if (!slot.Filled)
                {
                    planLog.AppendLine($"  pri={obj.Priority} {obj.Sector}: —");
                    continue;
                }
                totalAssigned++;
                UnitManager u = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
                string distStr = "?";
                if (u != null && tgt != null)
                {
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;
                    distStr = $"{Vector3Int.Distance(uc, tc):F1}h";
                }
                planLog.AppendLine($"  pri={obj.Priority} {obj.Sector}: Unit{slot.AssignedUnitId} @ {distStr}");
            }
        }
        planLog.Append($"  → {totalAssigned} atribuídos | {plan.RogueUnitIds.Count} rogues");
        Debug.Log(planLog.ToString());
    }

    // -------------------------------------------------------------------------
    // Helpers de planejamento
    // -------------------------------------------------------------------------

    private void ClearObjectiveHUD(SectorObjective obj)
    {
        foreach (SlotNeed slot in obj.Slots)
        {
            if (!slot.Filled) continue;
            UnitManager u = FindActiveUnit(slot.AssignedUnitId, obj.AssignedTeam);
            if (u != null) u.ClearAIAssignedPlan();
        }
    }

    private void ApplyPlanHUD(UnitManager unit, SectorObjective obj)
    {
        string sectorName = obj.Sector.ToString();
        string badge = sectorName.Length > 0 ? sectorName[0].ToString().ToUpper() : "?";
        unit.SetAIAssignedPlan(sectorName, sectorName, badge, (int)UnitRole.Capturador, showAIUnitHUD);
    }

    private static UnitManager FindActiveUnit(int instanceId, TeamId team)
    {
        foreach (UnitManager u in UnitManager.AllActive)
            if (u.InstanceId == instanceId && u.TeamId == team && !u.IsDead) return u;
        return null;
    }

    private static int CalculateSectorPriority(SectorManager.SectorInfo info, TeamId aiTeam)
    {
        float distToAI = info.GetDistanceToHQ(aiTeam);
        if (distToAI == float.MaxValue) return 0;

        int riskBonus;
        switch (info.GetRiskLevelFor(aiTeam))
        {
            case SectorManager.SectorRiskLevel.Safe:   riskBonus = 40; break;
            case SectorManager.SectorRiskLevel.Low:    riskBonus = 30; break;
            case SectorManager.SectorRiskLevel.Medium: riskBonus = 20; break;
            case SectorManager.SectorRiskLevel.High:   riskBonus = 10; break;
            default:                                   riskBonus =  0; break;
        }

        int distPenalty   = Mathf.RoundToInt(distToAI);
        int disputedBonus = info.IsDisputed ? 15 : 0;

        return riskBonus - distPenalty + disputedBonus;
    }

    // Retorna distância até o alvo atribuído. Rogues e sem plano = float.MaxValue (agem por último).
    private static float GetDistanceToAssignedTarget(UnitManager unit, TeamId aiTeam, TeamObjectivePlan plan)
    {
        if (plan == null) return float.MaxValue;
        if (plan.RogueUnitIds.Contains(unit.InstanceId)) return float.MaxValue;

        foreach (SectorObjective obj in plan.Objectives)
        {
            bool assigned = false;
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && slot.AssignedUnitId == unit.InstanceId) { assigned = true; break; }
            if (!assigned) continue;

            ConstructionManager target = FindCapturableInSector(obj.Sector, aiTeam);
            if (target == null) return 0f;

            Vector3Int uCell = unit.CurrentCellPosition; uCell.z = 0;
            Vector3Int tCell = target.CurrentCellPosition; tCell.z = 0;
            return Vector3Int.Distance(uCell, tCell);
        }

        return float.MaxValue;
    }

    private static MatchController cachedMatchController;
    private static MatchController GetMatchController()
    {
        if (cachedMatchController == null)
            cachedMatchController = Object.FindAnyObjectByType<MatchController>();
        return cachedMatchController;
    }

    // Backtracking ótimo: minimiza a soma das distâncias euclidiana unit→objetivo.
    // Para N ≤ 8 e M ≤ 8 objetivos abertos, P(M,N) ≤ 40 320 iterações — trivial.
    private static void SolveAssignment(
        List<UnitManager> units,
        List<(SectorObjective obj, Vector3Int cell)> objs,
        bool[] usedObj,
        int[] current,
        int depth,
        int maxDepth,
        float cost,
        ref float bestCost,
        ref int[] bestAssign)
    {
        if (depth == maxDepth)
        {
            if (cost < bestCost)
            {
                bestCost = cost;
                System.Array.Copy(current, bestAssign, maxDepth);
            }
            return;
        }

        Vector3Int uc = units[depth].CurrentCellPosition; uc.z = 0;
        for (int j = 0; j < objs.Count; j++)
        {
            if (usedObj[j]) continue;
            float newCost = cost + Vector3Int.Distance(uc, objs[j].cell);
            if (newCost >= bestCost) continue;
            current[depth] = j;
            usedObj[j] = true;
            SolveAssignment(units, objs, usedObj, current, depth + 1, maxDepth, newCost, ref bestCost, ref bestAssign);
            usedObj[j] = false;
        }
    }
}
