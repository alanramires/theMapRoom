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
                UnitManager slotUnit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (slotUnit == null || slotUnit.IsUnderRepair)
                {
                    slot.Filled = false;
                    slot.AssignedUnitId = -1;
                }
            }
        }

        // Passo 2: adiciona objetivos para setores ainda não cobertos.
        // Em Defensive: não abre novos objetivos em setores Medium ou piores (já ocupados persistem).
        IReadOnlyList<SectorManager.SectorInfo> allSectors = SectorManager.GetAllSectorInfos();
        foreach (SectorManager.SectorInfo info in allSectors)
        {
            if (info.IsFullyControlled && info.ControllingTeam == aiTeam) continue;
            bool hasCapturable = false;
            foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
                if (c.OwnerTeam != aiTeam) { hasCapturable = true; break; }
            if (!hasCapturable) continue;
            if (plan.GetObjectiveForSector(info.Sector) != null) continue;

            if (snapshot.Stance == AIStance.Defensive
                && info.GetRiskLevelFor(aiTeam) >= SectorManager.SectorRiskLevel.Medium)
                continue;

            SectorObjective obj = new SectorObjective
            {
                Sector       = info.Sector,
                AssignedTeam = aiTeam,
                Status       = ObjectiveStatus.Pending,
                Priority     = CalculateSectorPriority(info, aiTeam, snapshot.Stance),
            };
            int slots = info.GetRiskLevelFor(aiTeam) == SectorManager.SectorRiskLevel.High ? 2 : 1;
            for (int s = 0; s < slots; s++)
                obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
            plan.Objectives.Add(obj);
        }

        // Edge case defensivo: se não sobrou nenhum objetivo, adiciona o setor capturável mais próximo do HQ
        if (snapshot.Stance == AIStance.Defensive && plan.Objectives.Count == 0)
        {
            SectorManager.SectorInfo closest = null;
            float closestDist = float.MaxValue;
            foreach (SectorManager.SectorInfo info in allSectors)
            {
                if (info.IsFullyControlled && info.ControllingTeam == aiTeam) continue;
                bool hasCapturable = false;
                foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
                    if (c.OwnerTeam != aiTeam) { hasCapturable = true; break; }
                if (!hasCapturable) continue;
                float d = info.GetDistanceToHQ(aiTeam);
                if (d < closestDist) { closestDist = d; closest = info; }
            }
            if (closest != null && plan.GetObjectiveForSector(closest.Sector) == null)
            {
                SectorObjective fallback = new SectorObjective
                {
                    Sector       = closest.Sector,
                    AssignedTeam = aiTeam,
                    Status       = ObjectiveStatus.Pending,
                    Priority     = CalculateSectorPriority(closest, aiTeam, snapshot.Stance),
                };
                fallback.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
                plan.Objectives.Add(fallback);
                Debug.Log($"{TL("Plan")} Defensive sem objetivos seguros — fallback para {closest.Sector} ({closestDist:F1}h do HQ)");
            }
        }

        // Passo 3: recalcula prioridades, ordena e renumera
        foreach (SectorObjective obj in plan.Objectives)
            if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo inf))
                obj.Priority = CalculateSectorPriority(inf, aiTeam, snapshot.Stance);

        plan.Objectives.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        for (int i = 0; i < plan.Objectives.Count; i++)
            plan.Objectives[i].Priority = i + 1;

        plan.HandoffVacaterIds.Clear();
        plan.VacaterForwardSectors.Clear();

        // Passo 3b: handoff — capturador mais saudável herda objetivo parcial;
        //           capturador original fica livre para o backtracking reatribuir
        EvaluateCaptureHandoffs(plan, aiTeam);

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
            Debug.Log($"{TL("Plan")} {u.InstanceId} já está em {bldg.Sector} → captura imediata");
        }
        foreach (UnitManager u in immediateList) freeCapturers.Remove(u);

        // 5b: atribuição ótima por backtracking (minimiza passos reais de caminho).
        // Cascata DINÂMICA: setores com slot aberto são processados em ordem de prioridade;
        // cada setor selecionado suprime apenas seu neighbor1 (vizinho mais próximo).
        // Setores já preenchidos (sticky) NÃO geram cascata — só setores abertos participam.
        // Isso garante que: (a) setores com substituto handoff não bloqueiam o vacater,
        //                   (b) a cascata só reflete quem está disputando slot neste turno.
        var cascadeCovered = new HashSet<ConstructionSector>();
        var assignableObjs = new List<(SectorObjective obj, Vector3Int cell)>();
        foreach (SectorObjective obj in plan.Objectives) // pri=1 primeiro (mais prioritário)
        {
            if (!obj.HasOpenSlot(UnitRole.Capturador)) continue;
            if (cascadeCovered.Contains(obj.Sector))   continue;
            ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
            if (tgt == null) continue;
            Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;
            assignableObjs.Add((obj, tc));
            MarkCascadeNeighbor1(obj.Sector, cascadeCovered, aiTeam, plan.VacaterForwardSectors);
        }

        int nu = Mathf.Min(freeCapturers.Count, assignableObjs.Count);
        if (nu > 0)
        {
            int nObj = assignableObjs.Count;

            // Pré-seleciona os N candidatos mais próximos (Euclidiana como filtro rápido).
            if (freeCapturers.Count > nu)
            {
                freeCapturers.Sort((a, b) =>
                {
                    float minA = float.MaxValue, minB = float.MaxValue;
                    Vector3Int ca = a.CurrentCellPosition; ca.z = 0;
                    Vector3Int cb = b.CurrentCellPosition; cb.z = 0;
                    foreach ((_, Vector3Int tc) in assignableObjs)
                    {
                        float da = Vector3Int.Distance(ca, tc);
                        float db = Vector3Int.Distance(cb, tc);
                        if (da < minA) minA = da;
                        if (db < minB) minB = db;
                    }
                    return minA.CompareTo(minB);
                });
            }

            // Pré-computa matriz de distâncias reais (passos de caminho).
            // Orçamento = 30 pts de movimento ≈ 6-10 turnos para a maioria das unidades.
            // Fallback para Euclidiana×2 quando o alvo está fora do alcance do orçamento.
            const int PlanBudget = 30;
            var distMatrix = new float[nu, nObj];
            for (int ui = 0; ui < nu; ui++)
            {
                UnitManager u   = freeCapturers[ui];
                Vector3Int  uc  = u.CurrentCellPosition; uc.z = 0;
                var planPaths   = UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap, u, PlanBudget, terrainDatabase);
                for (int oj = 0; oj < nObj; oj++)
                {
                    Vector3Int tc = assignableObjs[oj].cell;
                    if (planPaths != null
                        && planPaths.TryGetValue(tc, out List<Vector3Int> path)
                        && path.Count > 0)
                        distMatrix[ui, oj] = path.Count;
                    else
                        if (SectorManager.TryGetLandMovementDistance(uc, tc, out int terrainCost))
                            distMatrix[ui, oj] = terrainCost;
                        else
                            distMatrix[ui, oj] = SectorManager.HexDistance(uc, tc);
                }
            }

            int[] bestAssign = new int[nu];
            float bestCost   = float.MaxValue;
            SolveAssignment(freeCapturers, assignableObjs,
                new bool[nObj], new int[nu],
                0, nu, 0f, distMatrix, ref bestCost, ref bestAssign);

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
        {
            u.ClearAIAssignedPlan();
            plan.RogueUnitIds.Add(u.InstanceId);
        }

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
        planLog.AppendLine($"{TL("Plan")} {aiTeam} — {plan.Objectives.Count} objetivos:");
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

    private static int CalculateSectorPriority(SectorManager.SectorInfo info, TeamId aiTeam, AIStance stance = AIStance.Tactical)
    {
        float distToAI = info.GetDistanceToHQ(aiTeam);
        if (distToAI == float.MaxValue) return 0;

        SectorManager.SectorRiskLevel risk = info.GetRiskLevelFor(aiTeam);

        int riskBonus;
        switch (risk)
        {
            case SectorManager.SectorRiskLevel.Safe:   riskBonus = 40; break;
            case SectorManager.SectorRiskLevel.Low:    riskBonus = 30; break;
            case SectorManager.SectorRiskLevel.Medium: riskBonus = 20; break;
            case SectorManager.SectorRiskLevel.High:   riskBonus = 10; break;
            default:                                   riskBonus =  0; break;
        }

        int distPenalty   = Mathf.RoundToInt(distToAI);
        int disputedBonus = info.IsDisputed ? 15 : 0;

        // Bônus por tipo de construção no setor (HQ inimigo, fábrica, prédio com renda)
        int buildingValueBonus = 0;
        foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
        {
            if (c.Source == null || c.OwnerTeam == aiTeam) continue;
            if (c.Source.IsPlayerHeadQuarter)
            {
                int hqVal = stance == AIStance.Defensive
                    ? Mathf.RoundToInt(100 * 0.2f)   // modera rush irresponsável em defensive
                    : 100;
                buildingValueBonus += hqVal;
            }
            else if (c.Source.CanProduceUnits)
                buildingValueBonus += 30;
            else if (c.Source.CapturedIncoming > 0)
                buildingValueBonus += 15;
        }
        buildingValueBonus = Mathf.Clamp(buildingValueBonus, 0, 80);

        int stanceBonus = 0;
        switch (stance)
        {
            case AIStance.Defensive:
                stanceBonus += Mathf.RoundToInt(1f / (distToAI + 1f) * 30f);
                if (risk >= SectorManager.SectorRiskLevel.High)
                    stanceBonus -= 30;
                break;
            case AIStance.Offensive:
                if (risk >= SectorManager.SectorRiskLevel.High)
                    stanceBonus += 20;
                else if (risk <= SectorManager.SectorRiskLevel.Low)
                    stanceBonus -= 10;
                break;
        }

        return riskBonus - distPenalty + disputedBonus + buildingValueBonus + stanceBonus;
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
            return SectorManager.HexDistance(uCell, tCell);
        }

        return float.MaxValue;
    }

    // -------------------------------------------------------------------------
    // Handoff: capturador mais saudável herda objetivo parcial
    // -------------------------------------------------------------------------

    private void EvaluateCaptureHandoffs(TeamObjectivePlan plan, TeamId aiTeam)
    {
        // IDs já atribuídos (snapshot local antes das modificações)
        var assignedIds = new HashSet<int>();
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled) assignedIds.Add(slot.AssignedUnitId);

        // Capturadores livres: não atribuídos, não em reparo
        List<UnitManager> allCapturers = GetAvailableCapturers(aiTeam);
        var freeCapturers = new List<UnitManager>();
        foreach (UnitManager u in allCapturers)
            if (!assignedIds.Contains(u.InstanceId)) freeCapturers.Add(u);

        if (freeCapturers.Count == 0) return;

        for (int i = 0; i < plan.Objectives.Count; i++)
        {
            SectorObjective obj = plan.Objectives[i];

            // Precisa de slot preenchido por capturador
            SlotNeed filledSlot = null;
            foreach (SlotNeed s in obj.Slots)
                if (s.Filled && s.Role == UnitRole.Capturador) { filledSlot = s; break; }
            if (filledSlot == null) continue;

            UnitManager assignedUnit = FindActiveUnit(filledSlot.AssignedUnitId, aiTeam);
            if (assignedUnit == null || assignedUnit.IsUnderRepair) continue;

            // Construção precisa estar parcialmente capturada
            ConstructionManager target = FindCapturableInSector(obj.Sector, aiTeam);
            if (target == null) continue;
            int pts = target.CurrentCapturePoints;
            int max = target.CapturePointsMax;
            if (pts <= 0 || pts >= max) continue;

            // Só vale handoff se existe algum outro objetivo aberto para mandar a unidade original
            if (!HasOpenObjectiveOtherThan(plan, obj)) continue;

            // Inimigo visível perto do alvo → não abandonar
            Vector3Int targetCell = target.CurrentCellPosition; targetCell.z = 0;
            if (HasEnemyNearCell(targetCell, aiTeam)) continue;

            // Procura o melhor substituto entre capturadores livres:
            // qualquer um que alcance o alvo este turno serve; preferência por completar a captura
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
                Debug.Log($"{TL("Handoff")}[Skip] sem substituto para {obj.Sector} ({pts}/{max})");
                continue;
            }

            bool subCompletes = pts + substitute.CurrentHP >= max;
            Debug.Log($"{TL("Handoff")} Unit{assignedUnit.InstanceId} hp={assignedUnit.CurrentHP} avança; " +
                      $"Unit{substitute.InstanceId} hp={substitute.CurrentHP} herda {obj.Sector} " +
                      $"({pts}/{max}){(subCompletes ? " → completa" : "")}");

            // Libera slot do capturador original → será reatribuído pelo backtracking (Passo 5)
            filledSlot.Filled         = false;
            filledSlot.AssignedUnitId = -1;
            assignedUnit.ClearAIAssignedPlan();
            plan.HandoffVacaterIds.Add(assignedUnit.InstanceId);
            ConstructionSector fwdSector = ComputeForwardNeighborSector(obj.Sector, aiTeam);
            if (fwdSector != default) plan.VacaterForwardSectors.Add(fwdSector);

            // Atribui substituto ao objetivo parcial
            obj.TryFillSlot(UnitRole.Capturador, substitute.InstanceId);
            obj.Status                     = ObjectiveStatus.PartialReadyForHandoff;
            obj.HandoffEligible            = true;
            obj.PreferredHandoffFromUnitId = assignedUnit.InstanceId;
            ApplyPlanHUD(substitute, obj);

            // Substituto sai do pool livre; capturador original re-entra via Passo 4/5
            freeCapturers.Remove(substitute);
            assignedIds.Add(substitute.InstanceId);
        }
    }

    private void MarkCascadeNeighbor1(ConstructionSector sector, HashSet<ConstructionSector> covered, TeamId aiTeam, HashSet<ConstructionSector> vacaterProtected = null)
    {
        if (!SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info)) return;

        // Prefere neighbor1 (mais próximo).
        // Se já for totalmente controlado pelo time AI (atrás do front), usa neighbor2 (direção de avanço).
        ConstructionSector candidate = info.ClosestNeighbor1;
        float              candidateDist = info.ClosestNeighbor1Distance;

        if (candidate != default
            && SectorManager.TryGetSectorInfo(candidate, out SectorManager.SectorInfo n1)
            && n1.IsFullyControlled && n1.ControllingTeam == aiTeam)
        {
            candidate     = info.ClosestNeighbor2;
            candidateDist = info.ClosestNeighbor2Distance;
        }

        if (candidate == default) return;

        // Setor forward de um vacater: não suprimir — o vacater vai naturalmente para lá
        if (vacaterProtected != null && vacaterProtected.Contains(candidate))
        {
            Debug.Log($"{TL("Plan")} cascata: {sector} → {candidate} protegido por vacater, supressão ignorada");
            return;
        }

        covered.Add(candidate);
        Debug.Log($"{TL("Plan")} cascata: {sector} → {candidate} ({candidateDist:F1}h)");
    }

    // Mesmo cálculo de direção que MarkCascadeNeighbor1: retorna o setor forward sem suprimir nada.
    private static ConstructionSector ComputeForwardNeighborSector(ConstructionSector sector, TeamId aiTeam)
    {
        if (!SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info)) return default;
        ConstructionSector candidate = info.ClosestNeighbor1;
        if (candidate != default
            && SectorManager.TryGetSectorInfo(candidate, out SectorManager.SectorInfo n1)
            && n1.IsFullyControlled && n1.ControllingTeam == aiTeam)
            candidate = info.ClosestNeighbor2;
        return candidate;
    }

    private static bool HasOpenObjectiveOtherThan(TeamObjectivePlan plan, SectorObjective exclude)
    {
        foreach (SectorObjective obj in plan.Objectives)
            if (obj != exclude && obj.HasOpenSlot(UnitRole.Capturador)) return true;
        return false;
    }

    private static MatchController cachedMatchController;
    private static MatchController GetMatchController()
    {
        if (cachedMatchController == null)
            cachedMatchController = Object.FindAnyObjectByType<MatchController>();
        return cachedMatchController;
    }

    // Backtracking ótimo: minimiza a soma de passos reais de caminho unit→objetivo.
    // Para N ≤ 8 e M ≤ 8 objetivos abertos, P(M,N) ≤ 40 320 iterações — trivial.
    private static void SolveAssignment(
        List<UnitManager> units,
        List<(SectorObjective obj, Vector3Int cell)> objs,
        bool[] usedObj,
        int[] current,
        int depth,
        int maxDepth,
        float cost,
        float[,] distMatrix,
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

        for (int j = 0; j < objs.Count; j++)
        {
            if (usedObj[j]) continue;
            float newCost = cost + distMatrix[depth, j];
            if (newCost >= bestCost) continue;
            current[depth] = j;
            usedObj[j] = true;
            SolveAssignment(units, objs, usedObj, current, depth + 1, maxDepth, newCost, distMatrix, ref bestCost, ref bestAssign);
            usedObj[j] = false;
        }
    }
}
