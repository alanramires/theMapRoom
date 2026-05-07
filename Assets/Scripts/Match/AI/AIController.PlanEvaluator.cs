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
                // Setor já conquistado: transita para Defending e preserva enquanto a ameaça persistir.
                // Captura tanto a transição ofensivo→defensivo (1º turno pós-conquista)
                // quanto turnos subsequentes (já Defending), evitando redistribuição dos defensores.
                if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo defInf)
                    && defInf.IsFullyControlled && defInf.ControllingTeam == aiTeam
                    && HasNearbyVisibleEnemy(defInf.RepresentativeCell, aiTeam, defenseEnemyRange))
                {
                    obj.Status = ObjectiveStatus.Defending;
                    // Remove slots vazios de rodadas anteriores; valida slots preenchidos.
                    for (int s = obj.Slots.Count - 1; s >= 0; s--)
                    {
                        SlotNeed slot = obj.Slots[s];
                        if (!slot.Filled) { obj.Slots.RemoveAt(s); continue; }
                        UnitManager slotUnit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                        if (slotUnit == null || slotUnit.IsUnderRepair)
                        {
                            slotUnit?.ClearAIAssignedPlan();
                            obj.Slots.RemoveAt(s);
                        }
                    }
                    if (obj.Slots.Count == 0)
                        obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
                    continue;
                }
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

            // Gating de co-chegada: setor arriscado só abre se pelo menos 2 capturadores
            // chegarem com gap ≤ MaxArrivalGap hexes entre si (evita capturador solo em zona inimiga).
            if (info.GetRiskLevelFor(aiTeam) >= SectorManager.SectorRiskLevel.High)
            {
                ConstructionManager riskTgt = FindCapturableInSector(info.Sector, aiTeam);
                if (riskTgt != null)
                {
                    const float MaxArrivalGap = 5f;
                    Vector3Int rtc = riskTgt.CurrentCellPosition; rtc.z = 0;
                    float near1 = float.MaxValue, near2 = float.MaxValue;
                    foreach (UnitManager cap in GetAvailableCapturers(aiTeam))
                    {
                        Vector3Int cc = cap.CurrentCellPosition; cc.z = 0;
                        float d = SectorManager.TryGetLandMovementDistance(cc, rtc, out int td)
                            ? td : SectorManager.HexDistance(cc, rtc);
                        if (d < near1) { near2 = near1; near1 = d; }
                        else if (d < near2) near2 = d;
                    }
                    float gap = near2 - near1;
                    if (near2 == float.MaxValue || gap > MaxArrivalGap)
                    {
                        Debug.Log($"{TL("Plan")} Setor {info.Sector} ignorado: risco alto, batedor muito distante (gap={(near2 == float.MaxValue ? "?" : gap.ToString("F0"))}h)");
                        continue;
                    }
                }
            }

            SectorObjective obj = new SectorObjective
            {
                Sector       = info.Sector,
                AssignedTeam = aiTeam,
                Status       = ObjectiveStatus.Pending,
                Priority     = CalculateSectorPriority(info, aiTeam, snapshot.Stance),
            };
            int slots = info.GetRiskLevelFor(aiTeam) >= SectorManager.SectorRiskLevel.High ? 2 : 1;
            for (int s = 0; s < slots; s++)
                obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
            if (info.GetRiskLevelFor(aiTeam) >= SectorManager.SectorRiskLevel.High)
                obj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
            if (info.GetDistanceToHQ(aiTeam) >= MinDistanceForTransportSlot)
                obj.Slots.Add(new SlotNeed { Role = UnitRole.Transportador });
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

        // Passo 3: recalcula prioridades, ordena e renumera.
        // Objetivos defensivos (preservados do turno anterior) ficam sempre após os ofensivos.
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj.Status == ObjectiveStatus.Defending) continue;
            if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo inf))
                obj.Priority = CalculateSectorPriority(inf, aiTeam, snapshot.Stance);
        }

        plan.Objectives.Sort((a, b) =>
        {
            bool aDefending = a.Status == ObjectiveStatus.Defending;
            bool bDefending = b.Status == ObjectiveStatus.Defending;
            if (aDefending != bDefending) return aDefending ? 1 : -1;
            return b.Priority.CompareTo(a.Priority);
        });
        for (int i = 0; i < plan.Objectives.Count; i++)
            plan.Objectives[i].Priority = i + 1;

        // Passo 3c: objetivos defensivos — setores conquistados com inimigo visível a ≤3h.
        // Adicionados APÓS a renumeração ofensiva → sempre prioridade mais baixa.
        // Rogues livres após o solver ofensivo são alocados aqui antes de virarem permanentemente rogues.
        {
            int DefenseEnemyRange = defenseEnemyRange;
            int defPriority = plan.Objectives.Count + 1;
            foreach (SectorManager.SectorInfo info in allSectors)
            {
                if (!info.IsFullyControlled || info.ControllingTeam != aiTeam) continue;
                if (plan.GetObjectiveForSector(info.Sector) != null) continue;
                Vector3Int rc = info.RepresentativeCell; rc.z = 0;
                if (!HasNearbyVisibleEnemy(rc, aiTeam, DefenseEnemyRange)) continue;

                var defObj = new SectorObjective
                {
                    Sector = info.Sector, AssignedTeam = aiTeam,
                    Status = ObjectiveStatus.Defending, Priority = defPriority++,
                };
                // 2º slot se inimigo já está recapturando — precisa de reforço urgente
                int defSlots = info.HasPartialCapture ? 2 : 1;
                for (int s = 0; s < defSlots; s++)
                    defObj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
                plan.Objectives.Add(defObj);
                Debug.Log($"{TL("Plan")} Objetivo defensivo: {info.Sector} (pri {defPriority - 1}, inimigo ≤{DefenseEnemyRange}h)");
            }
        }

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
            if (obj.Status != ObjectiveStatus.Defending) obj.Status = ObjectiveStatus.Pursuing;
            ApplyPlanHUD(u, obj);
            immediateList.Add(u);
            Debug.Log($"{TL("Plan")} {u.InstanceId} já está em {bldg.Sector} → captura imediata");
        }
        foreach (UnitManager u in immediateList) freeCapturers.Remove(u);

        // 5b: atribuição ótima por backtracking (minimiza passos reais de caminho).
        // Cascata apenas na distribuição inicial (nenhum objetivo ainda em Pursuing):
        // cada setor selecionado suprime seu vizinho forward (direção de avanço),
        // para que as unidades se espalhem em vez de se aglomerar no mesmo front.
        // Após T1, quando unidades já estão em rota, a cascata é desativada para que
        // cada setor possa receber cobertura independente.
        bool isInitialDistribution = !plan.Objectives.Exists(o => o.Status == ObjectiveStatus.Pursuing);
        const float MaxCoArrivalGap = 5f;
        var cascadeCovered = new HashSet<ConstructionSector>();
        var assignableObjs = new List<(SectorObjective obj, Vector3Int cell)>();
        foreach (SectorObjective obj in plan.Objectives) // pri=1 primeiro (mais prioritário)
        {
            if (!obj.HasOpenSlot(UnitRole.Capturador)) continue;
            // Objetivos defensivos nunca são bloqueados pelo cascade (território já conquistado).
            bool isDefensive = false;
            ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
            Vector3Int tc;
            if (tgt != null)
            {
                tc = tgt.CurrentCellPosition; tc.z = 0;
            }
            else if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)
                && defInfo.IsFullyControlled && defInfo.ControllingTeam == aiTeam)
            {
                isDefensive = true;
                tc = defInfo.RepresentativeCell; tc.z = 0;
            }
            else continue;
            if (!isDefensive && cascadeCovered.Contains(obj.Sector)) continue;

            // 2º slot de setor de alto risco: só abre se houver capturador livre que possa
            // chegar junto com o 1º (co-chegada). Evita mandar unidade recém-comprada a 10h.
            if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo slotInfo)
                && slotInfo.GetRiskLevelFor(aiTeam) >= SectorManager.SectorRiskLevel.High)
            {
                SlotNeed filledSlot = obj.Slots.Find(s => s.Role == UnitRole.Capturador && s.Filled);
                if (filledSlot != null)
                {
                    UnitManager assigned = FindActiveUnit(filledSlot.AssignedUnitId, aiTeam);
                    Vector3Int  aPos     = assigned != null ? assigned.CurrentCellPosition : tc; aPos.z = 0;
                    float assignedDist   = SectorManager.TryGetLandMovementDistance(aPos, tc, out int adCost)
                        ? adCost : SectorManager.HexDistance(aPos, tc);

                    float nearestFree = float.MaxValue;
                    foreach (UnitManager cap in freeCapturers)
                    {
                        Vector3Int cc = cap.CurrentCellPosition; cc.z = 0;
                        float d = SectorManager.TryGetLandMovementDistance(cc, tc, out int td)
                            ? td : SectorManager.HexDistance(cc, tc);
                        if (d < nearestFree) nearestFree = d;
                    }

                    if (nearestFree == float.MaxValue || nearestFree > assignedDist + MaxCoArrivalGap)
                    {
                        Debug.Log($"{TL("Plan")} {obj.Sector} 2º slot bloqueado: livre mais próximo " +
                                  $"({(nearestFree == float.MaxValue ? "?" : nearestFree.ToString("F0"))}h) " +
                                  $"fora de alcance do 1º ({assignedDist:F0}h)");
                        continue;
                    }
                }
            }

            assignableObjs.Add((obj, tc));
            if (isInitialDistribution && !isDefensive)
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
            const int   PlanBudget     = 30;
            float RiskCostWeight = riskDecisionImpact;

            // Multiplicador de risco: setores de alto risco encarecem atribuição (incentiva co-chegada).
            var riskMultipliers = new float[nObj];
            for (int oj = 0; oj < nObj; oj++)
            {
                riskMultipliers[oj] = 1f;
                if (SectorManager.TryGetSectorInfo(assignableObjs[oj].obj.Sector,
                        out SectorManager.SectorInfo sInfo))
                    riskMultipliers[oj] = 1f + sInfo.GetRiskRatioFor(aiTeam) * RiskCostWeight;
            }

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
                    float rawDist;
                    if (planPaths != null
                        && planPaths.TryGetValue(tc, out List<Vector3Int> path)
                        && path.Count > 0)
                        rawDist = path.Count;
                    else if (SectorManager.TryGetLandMovementDistance(uc, tc, out int terrainCost))
                        rawDist = terrainCost;
                    else
                        rawDist = SectorManager.HexDistance(uc, tc);
                    distMatrix[ui, oj] = rawDist * riskMultipliers[oj];
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
                if (obj.Status != ObjectiveStatus.Defending) obj.Status = ObjectiveStatus.Pursuing;
                ApplyPlanHUD(u, obj);
            }
            freeCapturers.RemoveRange(0, nu);
        }

        // Passo 5c: assaltos primários preenchem slots de batedor.
        {
            var assignedAfterCapturers = new HashSet<int>();
            foreach (SectorObjective obj in plan.Objectives)
                foreach (SlotNeed slot in obj.Slots)
                    if (slot.Filled) assignedAfterCapturers.Add(slot.AssignedUnitId);

            List<UnitManager> freeAssaults = GetAvailablePrimaryAssaults(aiTeam);
            for (int i = freeAssaults.Count - 1; i >= 0; i--)
                if (assignedAfterCapturers.Contains(freeAssaults[i].InstanceId))
                    freeAssaults.RemoveAt(i);

            foreach (SectorObjective obj in plan.Objectives)
            {
                if (freeAssaults.Count == 0) break;
                if (obj.Status != ObjectiveStatus.Defending) continue;
                if (HasAnySlot(obj, UnitRole.Assalto)) continue;
                if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)) continue;
                Vector3Int targetCell = defInfo.RepresentativeCell; targetCell.z = 0;
                if (!IsObjectiveInCombatDisadvantage(obj, aiTeam, targetCell,
                        defenseEnemyRange, alliesAgainstEnemiesHpRatio, out int enemyHp, out int allyHp))
                    continue;

                UnitManager best = null;
                float bestDist = float.MaxValue;
                foreach (UnitManager u in freeAssaults)
                {
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float d = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                        ? td : SectorManager.HexDistance(uc, targetCell);
                    if (d < bestDist) { bestDist = d; best = u; }
                }

                if (best == null) continue;

                obj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
                obj.TryFillSlot(UnitRole.Assalto, best.InstanceId);
                ApplyPlanHUD(best, obj, UnitRole.Assalto);
                freeAssaults.Remove(best);
                Debug.Log($"{TL("Plan")} Assalto {best.InstanceId} → defesa crítica de {obj.Sector} " +
                          $"(inimigo={enemyHp}HP aliado={allyHp}HP ratio={enemyHp / (float)allyHp:F1}× dist={bestDist:F0}h)");
            }

            foreach (SectorObjective obj in plan.Objectives)
            {
                if (freeAssaults.Count == 0) break;
                if (obj.Status != ObjectiveStatus.Pursuing && obj.Status != ObjectiveStatus.Capturing) continue;
                if (HasAnySlot(obj, UnitRole.Assalto)) continue;

                ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
                if (tgt == null) continue;
                Vector3Int targetCell = tgt.CurrentCellPosition; targetCell.z = 0;

                if (!IsObjectiveInCombatDisadvantage(obj, aiTeam, targetCell,
                        alliesEnemyRange, alliesAgainstEnemiesHpRatio, out int enemyHp, out int allyHp))
                    continue;

                UnitManager best = null;
                float bestDist = float.MaxValue;
                foreach (UnitManager u in freeAssaults)
                {
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float d = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                        ? td : SectorManager.HexDistance(uc, targetCell);
                    if (d < bestDist) { bestDist = d; best = u; }
                }

                if (best == null) continue;

                obj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
                obj.TryFillSlot(UnitRole.Assalto, best.InstanceId);
                ApplyPlanHUD(best, obj, UnitRole.Assalto);
                freeAssaults.Remove(best);
                Debug.Log($"{TL("Plan")} Assalto {best.InstanceId} → SOS ofensivo {obj.Sector} " +
                          $"(inimigo={enemyHp}HP aliado={allyHp}HP ratio={enemyHp / (float)allyHp:F1}× dist={bestDist:F0}h)");
            }

            foreach (SectorObjective obj in plan.Objectives)
            {
                if (freeAssaults.Count == 0) break;
                if (obj.Status != ObjectiveStatus.Defending) continue;
                if (HasAnySlot(obj, UnitRole.Assalto)) continue;
                if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)) continue;
                Vector3Int targetCell = defInfo.RepresentativeCell; targetCell.z = 0;

                UnitManager best = null;
                float bestDist = float.MaxValue;
                foreach (UnitManager u in freeAssaults)
                {
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float d = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                        ? td : SectorManager.HexDistance(uc, targetCell);
                    if (d < bestDist) { bestDist = d; best = u; }
                }

                if (best == null) continue;

                obj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
                obj.TryFillSlot(UnitRole.Assalto, best.InstanceId);
                ApplyPlanHUD(best, obj, UnitRole.Assalto);
                freeAssaults.Remove(best);
                Debug.Log($"{TL("Plan")} Assalto {best.InstanceId} → defesa estável de {obj.Sector} (dist={bestDist:F0}h)");
            }

            foreach (SectorObjective obj in plan.Objectives)
            {
                if (!obj.HasOpenSlot(UnitRole.Assalto)) continue;
                if (!HasFilledSlot(obj, UnitRole.Capturador)) continue;
                if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo escortInfo)) continue;
                Vector3Int targetCell = escortInfo.RepresentativeCell; targetCell.z = 0;

                UnitManager best = null;
                float bestDist = float.MaxValue;
                foreach (UnitManager u in freeAssaults)
                {
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float d = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                        ? td : SectorManager.HexDistance(uc, targetCell);
                    if (d < bestDist) { bestDist = d; best = u; }
                }

                if (best == null) continue;

                obj.TryFillSlot(UnitRole.Assalto, best.InstanceId);
                if (obj.Status != ObjectiveStatus.Defending) obj.Status = ObjectiveStatus.Pursuing;
                ApplyPlanHUD(best, obj, UnitRole.Assalto);
                freeAssaults.Remove(best);
                Debug.Log($"{TL("Plan")} Assalto {best.InstanceId} → batedor de {obj.Sector} (dist={bestDist:F0}h)");
            }

            if (freeAssaults.Count > 0)
            {
                var lowRiskFallbacks = new List<SectorObjective>();
                foreach (SectorObjective obj in plan.Objectives)
                {
                    if (!HasFilledSlot(obj, UnitRole.Capturador)) continue;
                    if (HasAnySlot(obj, UnitRole.Assalto)) continue;
                    if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo escortInfo)) continue;
                    if (escortInfo.GetRiskLevelFor(aiTeam) != SectorManager.SectorRiskLevel.Low) continue;
                    lowRiskFallbacks.Add(obj);
                }

                lowRiskFallbacks.Sort((a, b) =>
                {
                    float ar = SectorManager.TryGetSectorInfo(a.Sector, out SectorManager.SectorInfo ai)
                        ? ai.GetRiskRatioFor(aiTeam) : 0f;
                    float br = SectorManager.TryGetSectorInfo(b.Sector, out SectorManager.SectorInfo bi)
                        ? bi.GetRiskRatioFor(aiTeam) : 0f;
                    int riskCompare = br.CompareTo(ar);
                    return riskCompare != 0 ? riskCompare : a.Priority.CompareTo(b.Priority);
                });

                foreach (SectorObjective obj in lowRiskFallbacks)
                {
                    if (freeAssaults.Count == 0) break;
                    if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo escortInfo)) continue;
                    Vector3Int targetCell = escortInfo.RepresentativeCell; targetCell.z = 0;

                    UnitManager best = null;
                    float bestDist = float.MaxValue;
                    foreach (UnitManager u in freeAssaults)
                    {
                        Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                        float d = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                            ? td : SectorManager.HexDistance(uc, targetCell);
                        if (d < bestDist) { bestDist = d; best = u; }
                    }

                    if (best == null) continue;

                    obj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
                    obj.TryFillSlot(UnitRole.Assalto, best.InstanceId);
                    if (obj.Status != ObjectiveStatus.Defending) obj.Status = ObjectiveStatus.Pursuing;
                    ApplyPlanHUD(best, obj, UnitRole.Assalto);
                    freeAssaults.Remove(best);
                    Debug.Log($"{TL("Plan")} Assalto {best.InstanceId} → batedor fallback Low de {obj.Sector} " +
                              $"(risco={escortInfo.GetRiskRatioFor(aiTeam):F2}, dist={bestDist:F0}h)");
                }
            }
        }

        // Passo 5d: rogues próximos reforçam objetivos defensivos (sem slot fixo — escala com disponíveis)
        // Candidatos ordenados por distância ao HQ aliado (ascendente): recém-comprados no HQ
        // são preferidos sobre unidades já posicionadas no front, preservando a cobertura avançada.
        {
            int maxDefenders  = 3;
            int defReachRange = DefenseEnemyRange * defenseCallRange;
            var rogueAssigned = new List<UnitManager>();

            var defCandidates = new List<UnitManager>(freeCapturers);
            if (snapshot.MyHQ != null)
            {
                Vector3Int hqCell = snapshot.MyHQ.CurrentCellPosition; hqCell.z = 0;
                defCandidates.Sort((a, b) =>
                {
                    Vector3Int ca = a.CurrentCellPosition; ca.z = 0;
                    Vector3Int cb = b.CurrentCellPosition; cb.z = 0;
                    return SectorManager.HexDistance(ca, hqCell)
                        .CompareTo(SectorManager.HexDistance(cb, hqCell));
                });
            }

            foreach (SectorObjective obj in plan.Objectives)
            {
                if (obj.Status != ObjectiveStatus.Defending) continue;
                if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)) continue;
                int defenders = 0; foreach (SlotNeed s in obj.Slots) if (s.Filled) defenders++;
                Vector3Int rc = defInfo.RepresentativeCell; rc.z = 0;
                foreach (UnitManager u in defCandidates)
                {
                    if (rogueAssigned.Contains(u)) continue;
                    if (defenders >= maxDefenders) break;
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    if (SectorManager.HexDistance(uc, rc) > defReachRange) continue;
                    obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador, Filled = true, AssignedUnitId = u.InstanceId });
                    ApplyPlanHUD(u, obj);
                    rogueAssigned.Add(u);
                    defenders++;
                    Debug.Log($"{TL("Plan")} Rogue {u.InstanceId} → defesa de {obj.Sector} (dist={SectorManager.HexDistance(uc, rc)})");
                }
            }
            foreach (UnitManager u in rogueAssigned) freeCapturers.Remove(u);
        }

        // Passo 5e: rogues próximos reforçam captura em severa desvantagem
        {
            const int MaxCaptureReinforcements = 2;
            float     SosRatio                 = alliesAgainstEnemiesHpRatio;
            int       sosReachRange            = alliesCallRange;
            var         captureReinforced        = new List<UnitManager>();

            foreach (SectorObjective obj in plan.Objectives)
            {
                if (obj.Status != ObjectiveStatus.Pursuing && obj.Status != ObjectiveStatus.Capturing) continue;

                ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
                if (tgt == null) continue;
                Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;

                // HP inimigo visível dentro de alliesEnemyRange do alvo
                int enemyHp = 0;
                MatchController mc = GetMatchController();
                foreach (UnitManager enemy in UnitManager.AllActive)
                {
                    if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
                    if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
                    Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                    if (SectorManager.HexDistance(ec, tc) <= alliesEnemyRange) enemyHp += enemy.CurrentHP;
                }
                if (enemyHp == 0) continue;

                // HP aliado já atribuído ao objetivo
                int allyHp = 0;
                foreach (SlotNeed s in obj.Slots)
                {
                    if (!s.Filled) continue;
                    UnitManager ally = FindActiveUnit(s.AssignedUnitId, aiTeam);
                    if (ally != null) allyHp += ally.CurrentHP;
                }

                if (enemyHp < allyHp * SosRatio) continue;

                int reinforcers = 0;
                foreach (UnitManager u in freeCapturers)
                {
                    if (captureReinforced.Contains(u)) continue;
                    if (reinforcers >= MaxCaptureReinforcements) break;
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    if (SectorManager.HexDistance(uc, tc) > sosReachRange) continue;
                    obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador, Filled = true, AssignedUnitId = u.InstanceId });
                    ApplyPlanHUD(u, obj);
                    captureReinforced.Add(u);
                    reinforcers++;
                    Debug.Log($"{TL("Plan")} SOS {u.InstanceId} → reforço de captura {obj.Sector} (inimigo={enemyHp}HP aliado={allyHp}HP ratio={enemyHp / (float)allyHp:F1}×)");
                }
            }
            foreach (UnitManager u in captureReinforced) freeCapturers.Remove(u);
        }

        foreach (UnitManager u in freeCapturers)
        {
            u.ClearAIAssignedPlan();
            plan.RogueUnitIds.Add(u.InstanceId);
        }

        // Passo 5f: atribui transportadores livres a slots de Transportador abertos.
        // Critério primário: capturer mais longe do seu objetivo (precisa mais de carona).
        // Critério secundário: APC mais perto do objetivo (chega antes).
        {
            List<UnitManager> freeTransporters = GetAvailableTransporters(aiTeam);
            foreach (UnitManager u in freeTransporters)
            {
                if (assignedIds.Contains(u.InstanceId)) continue;

                SectorObjective bestObj = null;
                float bestCapturerDist = -1f;
                float bestRisk = float.MaxValue;
                float bestApcDist = float.MaxValue;
                foreach (SectorObjective obj in plan.Objectives)
                {
                    if (!obj.HasOpenSlot(UnitRole.Transportador)) continue;
                    ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
                    if (tgt == null) continue;
                    Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;

                    // Distância do capturer alocado ao objetivo (quem mais precisa de carona).
                    // Fallback: distância do setor ao HQ — mesmo critério que criou o slot de transporte.
                    float sectorRisk = 0f;
                    float capturerDistToObj = SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo sInfo)
                        ? sInfo.GetDistanceToHQ(aiTeam) : 0f;
                    if (sInfo != null) sectorRisk = sInfo.GetRiskRatioFor(aiTeam);
                    foreach (SlotNeed slot in obj.Slots)
                    {
                        if (slot.Role != UnitRole.Capturador || !slot.Filled) continue;
                        UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                        if (capturer == null || capturer.IsEmbarked) continue;
                        Vector3Int cc = capturer.CurrentCellPosition; cc.z = 0;
                        capturerDistToObj = SectorManager.HexDistance(cc, tc);
                        break;
                    }

                    float apcDistToObj = SectorManager.HexDistance(uc, tc);
                    const float eps = 0.5f;
                    bool isBetter = capturerDistToObj > bestCapturerDist + eps
                        || (capturerDistToObj >= bestCapturerDist - eps && sectorRisk < bestRisk - 0.01f)
                        || (capturerDistToObj >= bestCapturerDist - eps && sectorRisk < bestRisk + 0.01f && apcDistToObj < bestApcDist);
                    if (isBetter)
                    {
                        bestCapturerDist = capturerDistToObj;
                        bestRisk = sectorRisk;
                        bestApcDist = apcDistToObj;
                        bestObj = obj;
                    }
                }

                if (bestObj == null) continue;
                bestObj.TryFillSlot(UnitRole.Transportador, u.InstanceId);
                assignedIds.Add(u.InstanceId);
                ApplyPlanHUD(u, bestObj, UnitRole.Transportador);
                Debug.Log($"{TL("Plan")} Transportador {u.InstanceId} → {bestObj.Sector} (capturerDist={bestCapturerDist:F0}h risk={bestRisk:F2} apcDist={bestApcDist:F0}h)");
            }
        }

        // Passo 6: reaplica HUD para atribuições anteriores
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && !freeCapturers.Exists(u => u.InstanceId == slot.AssignedUnitId))
                {
                    UnitManager u = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                    if (u != null) ApplyPlanHUD(u, obj, slot.Role);
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
                Vector3Int tc = Vector3Int.zero;
                bool hasTargetCell = false;
                if (tgt != null)
                {
                    tc = tgt.CurrentCellPosition; tc.z = 0;
                    hasTargetCell = true;
                }
                else if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo objInfo))
                {
                    tc = objInfo.RepresentativeCell; tc.z = 0;
                    hasTargetCell = true;
                }

                if (u != null && hasTargetCell)
                {
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float d = SectorManager.TryGetLandMovementDistance(uc, tc, out int td)
                        ? td
                        : SectorManager.HexDistance(uc, tc);
                    distStr = $"{d:F0}h";
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

    private void ApplyPlanHUD(UnitManager unit, SectorObjective obj, UnitRole role = UnitRole.Capturador)
    {
        string sectorName = obj.Sector.ToString();
        string badge = sectorName.Length > 0 ? sectorName[0].ToString().ToUpper() : "?";
        unit.SetAIAssignedPlan(sectorName, sectorName, badge, (int)role, showAIUnitHUD);
    }

    private static UnitManager FindActiveUnit(int instanceId, TeamId team)
    {
        foreach (UnitManager u in UnitManager.AllActive)
            if (u.InstanceId == instanceId && u.TeamId == team && !u.IsDead) return u;
        return null;
    }

    private static bool HasFilledSlot(SectorObjective obj, UnitRole role)
    {
        if (obj == null || obj.Slots == null) return false;
        foreach (SlotNeed slot in obj.Slots)
            if (slot.Role == role && slot.Filled) return true;
        return false;
    }

    private static bool HasAnySlot(SectorObjective obj, UnitRole role)
    {
        if (obj == null || obj.Slots == null) return false;
        foreach (SlotNeed slot in obj.Slots)
            if (slot.Role == role) return true;
        return false;
    }

    private bool IsObjectiveInCombatDisadvantage(
        SectorObjective obj,
        TeamId aiTeam,
        Vector3Int targetCell,
        int enemyRange,
        float disadvantageRatio,
        out int enemyHp,
        out int allyHp)
    {
        enemyHp = 0;
        allyHp = 0;
        if (obj == null)
            return false;

        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, targetCell) <= enemyRange)
                enemyHp += Mathf.Max(1, enemy.CurrentHP);
        }
        if (enemyHp == 0)
            return false;

        foreach (SlotNeed slot in obj.Slots)
        {
            if (!slot.Filled) continue;
            UnitManager ally = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (ally != null)
                allyHp += Mathf.Max(1, ally.CurrentHP);
        }

        return allyHp > 0 && enemyHp >= allyHp * disadvantageRatio;
    }

    private static List<UnitManager> GetAvailablePrimaryAssaults(TeamId aiTeam)
    {
        var list = new List<UnitManager>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId != aiTeam || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
            if (!u.TryGetUnitData(out UnitData data)) continue;
            if (data.roles != null && data.roles.Count > 0 && data.roles[0] == UnitRole.Assalto)
                list.Add(u);
        }
        return list;
    }

    private bool HasNearbyVisibleEnemy(Vector3Int cell, TeamId aiTeam, int range)
    {
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, cell) <= range) return true;
        }
        return false;
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

            // Handoff só se aplica a objetivos ofensivos — defensores não são substituídos
            if (obj.Status == ObjectiveStatus.Defending) continue;

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
                // Swap: sem capturador livre — avalia se uma unidade sticky de outro objetivo
                // já está aqui e vale redirecionar. Duas condições obrigatórias:
                //   1. A unidade original está longe o suficiente (swap compensa a troca)
                //   2. O candidato vem de objetivo de prioridade igual ou menor (não deserta missão importante)
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
                        if (otherObj.Priority < obj.Priority) continue; // não deserta missão mais importante

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

                // Libera a unidade original deste obj → passo 5 a reatribui
                filledSlot.Filled         = false;
                filledSlot.AssignedUnitId = -1;
                assignedUnit.ClearAIAssignedPlan();
                plan.HandoffVacaterIds.Add(assignedUnit.InstanceId);
                ConstructionSector fwdSec = ComputeForwardNeighborSector(obj.Sector, aiTeam);
                if (fwdSec != default) plan.VacaterForwardSectors.Add(fwdSec);

                // Libera o slot original do swap candidate → passo 5 pode preenchê-lo de novo
                swapFromSlot.Filled         = false;
                swapFromSlot.AssignedUnitId = -1;

                // Atribui swap candidate a este objetivo
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

        // Cascateia apenas para o vizinho que está À FRENTE: mais longe do HQ amigo do que o setor atual.
        // Isso garante que a cascata vai na direção de avanço independente do time.
        float myHQDist = info.GetDistanceToHQ(aiTeam);

        ConstructionSector candidate     = default;
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
        float myHQDist = info.GetDistanceToHQ(aiTeam);
        if (info.ClosestNeighbor1 != default
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor1, out SectorManager.SectorInfo n1)
            && n1.GetDistanceToHQ(aiTeam) > myHQDist)
            return info.ClosestNeighbor1;
        if (info.ClosestNeighbor2 != default
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor2, out SectorManager.SectorInfo n2)
            && n2.GetDistanceToHQ(aiTeam) > myHQDist)
            return info.ClosestNeighbor2;
        return default;
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
