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
                if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo defInf)
                    && defInf.IsFullyControlled && defInf.ControllingTeam == aiTeam
                    && (HasNearbyVisibleEnemy(defInf.RepresentativeCell, aiTeam, defenseEnemyRange)
                        || (IsCriticalHomeDefenseSector(defInf, aiTeam)
                            && IsHomeDefenseThreatened(defInf, aiTeam, HomeDefenseThreatRange))))
                {
                    obj.Status = ObjectiveStatus.Defending;
                    // Remove slots vazios e slots de Transportador (transporte é fase ofensiva).
                    // Valida e preserva apenas Capturador/Assalto preenchidos para defesa.
                    for (int s = obj.Slots.Count - 1; s >= 0; s--)
                    {
                        SlotNeed slot = obj.Slots[s];
                        if (!slot.Filled || slot.Role == UnitRole.Transportador)
                        {
                            if (slot.Filled)
                            {
                                UnitManager transUnit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                                transUnit?.ClearAIAssignedPlan();
                            }
                            obj.Slots.RemoveAt(s);
                            continue;
                        }
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
                    slotUnit?.ClearAIAssignedPlan();
                    slot.Filled = false;
                    slot.AssignedUnitId = -1;
                }
            }
        }

        // Passo 2: adiciona objetivos para setores ainda não cobertos.
        // Em Defensive: não abre novos objetivos em setores Medium ou piores (já ocupados persistem).
        // Cap de objetivos ofensivos simultâneos para evitar demand excessiva de capturadores.
        IReadOnlyList<SectorManager.SectorInfo> allSectors = SectorManager.GetAllSectorInfos();
        IReadOnlyList<SectorManager.SectorInfo> allBases = SectorManager.GetAllBaseInfos();

        int existingOffensive = 0;
        foreach (SectorObjective existing in plan.Objectives)
            if (existing.Status != ObjectiveStatus.Defending
                && existing.Status != ObjectiveStatus.Complete
                && existing.Status != ObjectiveStatus.Abandoned)
                existingOffensive++;

        int maxObj  = Instance != null ? Instance.MaxActiveObjectives : 4;
        int newSlots = Mathf.Max(0, maxObj - existingOffensive);

        var sectorCandidates = new List<SectorObjective>();
        foreach (SectorManager.SectorInfo info in allSectors)
        {
            if (info.IsFullyControlled && info.ControllingTeam == aiTeam) continue;
            bool hasCapturable = false;
            foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
            {
                // Not owned by us, or owned but capture not yet finished (mirrors FindCapturableInSector)
                if (c.OwnerTeam != aiTeam) { hasCapturable = true; break; }
                if (c.CapturePointsMax > 0 && c.CurrentCapturePoints < c.CapturePointsMax) { hasCapturable = true; break; }
            }
            if (!hasCapturable) { Debug.Log($"{TL("Plan")} skip {info.Sector}: sem capturável"); continue; }
            if (plan.GetObjectiveForSector(info.Sector) != null) { Debug.Log($"{TL("Plan")} skip {info.Sector}: já tem objetivo"); continue; }

            // Defensive stance blocks Medium+ risk sectors — EXCEPT buildings we already started
            // capturing (partial own capture). Abandoning a 11/20 capture mid-way is wasteful.
            bool hasPartialOwnCapture = false;
            foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
                if (c.OwnerTeam == aiTeam && c.CapturePointsMax > 0
                    && c.CurrentCapturePoints > 0 && c.CurrentCapturePoints < c.CapturePointsMax)
                { hasPartialOwnCapture = true; break; }

            if (snapshot.Stance == AIStance.Defensive
                && info.GetRiskLevelFor(aiTeam) >= SectorManager.SectorRiskLevel.Medium
                && !hasPartialOwnCapture)
            {
                Debug.Log($"{TL("Plan")} skip {info.Sector}: Defensive + risco={info.GetRiskLevelFor(aiTeam)} (>= Medium)");
                continue;
            }

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
                    // Gate only applies when ≥2 capturers exist to compare; with 0 or 1 the plan
                    // must still admit the sector so shopping can buy the needed capturers.
                    if (near2 < float.MaxValue && gap > MaxArrivalGap)
                    {
                        Debug.Log($"{TL("Plan")} Setor {info.Sector} ignorado: risco alto, batedor muito distante (gap={gap:F0}h)");
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
            int slots = Mathf.Clamp(Mathf.CeilToInt(info.ConstructionCount / 2f), 1, 4);
            bool highRisk = info.GetRiskLevelFor(aiTeam) >= SectorManager.SectorRiskLevel.High;
            if (highRisk) slots = Mathf.Max(slots, 2);
            for (int s = 0; s < slots; s++)
                obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
            if (highRisk)
                obj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
            float distHQ = info.GetDistanceToHQ(aiTeam);
            int   transThreshold = GetEffectiveTransportThreshold(aiTeam);
            bool  addTrans = distHQ >= transThreshold;
            if (addTrans) obj.Slots.Add(new SlotNeed { Role = UnitRole.Transportador });
            sectorCandidates.Add(obj);
        }

        // Adiciona apenas os N mais prioritários para não ultrapassar o cap.
        sectorCandidates.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        int addedSectors = 0;
        foreach (SectorObjective obj in sectorCandidates)
        {
            if (addedSectors >= newSlots)
            {
                Debug.Log($"{TL("Plan")} cap atingido ({maxObj}): {obj.Sector} descartado (pri={obj.Priority})");
                continue;
            }
            int capSlots  = obj.Slots.FindAll(s => s.Role == UnitRole.Capturador).Count;
            bool hasAss   = obj.Slots.Exists(s => s.Role == UnitRole.Assalto);
            bool hasTrans = obj.Slots.Exists(s => s.Role == UnitRole.Transportador);
            Debug.Log($"{TL("Plan")} {obj.Sector}: {capSlots}xCap{(hasAss ? " +Ass" : "")}{(hasTrans ? " +Trans" : "")} pri={obj.Priority}");
            plan.Objectives.Add(obj);
            addedSectors++;
        }

        // Passo 2b: base inimiga — entra no plano ofensivo quando setores regulares estão cobertos.
        // Capturadores proporcionais ao número de construções (1 por 2 prédios, mín 2).
        foreach (SectorManager.SectorInfo baseInfo in allBases)
        {
            // Usa HQ como referência canônica de dono da base — ControllingTeam pode estar errado
            // se prédios capturáveis tiverem slotIndex errado e aparecerem como Green no snapshot.
            if (FindHQTeamInSector(baseInfo.Sector) == aiTeam) continue;
            if (plan.GetObjectiveForSector(baseInfo.Sector) != null) continue;

            bool hasCapturable = false;
            foreach (SectorManager.SectorConstructionInfo c in baseInfo.Constructions)
            {
                if (c.OwnerTeam != aiTeam) { hasCapturable = true; break; }
                if (c.CapturePointsMax > 0 && c.CurrentCapturePoints < c.CapturePointsMax) { hasCapturable = true; break; }
            }
            if (!hasCapturable) continue;

            bool hasPartialOwnCapture = false;
            foreach (SectorManager.SectorConstructionInfo c in baseInfo.Constructions)
                if (c.OwnerTeam == aiTeam && c.CapturePointsMax > 0
                    && c.CurrentCapturePoints > 0 && c.CurrentCapturePoints < c.CapturePointsMax)
                { hasPartialOwnCapture = true; break; }

            if (snapshot.Stance == AIStance.Defensive && !hasPartialOwnCapture)
            {
                Debug.Log($"{TL("Plan")} skip base inimiga {baseInfo.Sector}: Defensive sem captura parcial");
                continue;
            }

            // Co-chegada: base inimiga sempre requer pelo menos 2 capturadores próximos
            ConstructionManager baseTgt = FindCapturableInSector(baseInfo.Sector, aiTeam);
            if (baseTgt != null)
            {
                const float MaxArrivalGap = 5f;
                Vector3Int rtc = baseTgt.CurrentCellPosition; rtc.z = 0;
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
                if (near2 < float.MaxValue && gap > MaxArrivalGap)
                {
                    Debug.Log($"{TL("Plan")} base inimiga {baseInfo.Sector} aguarda co-chegada (gap={gap:F0}h)");
                    continue;
                }
            }

            // Base inimiga também respeita o cap de objetivos ofensivos.
            int currentOffensive = existingOffensive + addedSectors;
            if (currentOffensive >= maxObj)
            {
                Debug.Log($"{TL("Plan")} cap atingido ({maxObj}): base inimiga {baseInfo.Sector} descartada");
                continue;
            }

            int capturerSlots = Mathf.Clamp(Mathf.CeilToInt(baseInfo.ConstructionCount / 2f), 2, 4);
            var baseObj = new SectorObjective
            {
                Sector       = baseInfo.Sector,
                AssignedTeam = aiTeam,
                Status       = ObjectiveStatus.Pending,
                Priority     = CalculateSectorPriority(baseInfo, aiTeam, snapshot.Stance),
            };
            for (int s = 0; s < capturerSlots; s++)
                baseObj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
            baseObj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
            if (baseInfo.GetDistanceToHQ(aiTeam) >= GetEffectiveTransportThreshold(aiTeam))
                baseObj.Slots.Add(new SlotNeed { Role = UnitRole.Transportador });
            plan.Objectives.Add(baseObj);
            addedSectors++;
            Debug.Log($"{TL("Plan")} base inimiga {baseInfo.Sector}: {capturerSlots}xCap + Assalto construcoes={baseInfo.ConstructionCount} dist={baseInfo.GetDistanceToHQ(aiTeam):F0}h");
        }

        ClearResolvedCriticalHomeDefenseObjectives(plan, aiTeam);
        EnsureCriticalHomeDefenseObjectives(plan, aiTeam, allBases);
        EnsureCriticalHomeDefenseObjectivesFromConstructions(plan, aiTeam);

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
                {
                    if (c.OwnerTeam != aiTeam) { hasCapturable = true; break; }
                    if (c.CapturePointsMax > 0 && c.CurrentCapturePoints < c.CapturePointsMax) { hasCapturable = true; break; }
                }
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
            if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo inf))
                obj.Priority = CalculateSectorPriority(inf, aiTeam, snapshot.Stance);
        }

        SortPlanObjectivesByStrategicPriority(plan, aiTeam, priorityNumberAscending: false);

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
                bool criticalHomeThreat = IsCriticalHomeDefenseSector(info, aiTeam)
                    && IsHomeDefenseThreatened(info, aiTeam, HomeDefenseThreatRange);
                if (!criticalHomeThreat && !HasNearbyVisibleEnemy(rc, aiTeam, DefenseEnemyRange)) continue;

                var defObj = new SectorObjective
                {
                    Sector = info.Sector, AssignedTeam = aiTeam,
                    Status = ObjectiveStatus.Defending, Priority = defPriority++,
                };
                // 2º capturer se inimigo já está recapturando — reforço urgente
                int defSlots = info.HasPartialCapture || criticalHomeThreat ? 2 : 1;
                for (int s = 0; s < defSlots; s++)
                    defObj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
                // Setor não-base com captura ativa: pede assault para expulsar o inimigo
                if (info.HasPartialCapture && !criticalHomeThreat)
                    defObj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
                plan.Objectives.Add(defObj);
                Debug.Log($"{TL("Plan")} Objetivo defensivo: {info.Sector} (pri {defPriority - 1}, inimigo ≤{DefenseEnemyRange}h, partialCapture={info.HasPartialCapture})");
            }
        }

        SortPlanObjectivesByStrategicPriority(plan, aiTeam, priorityNumberAscending: true);

        plan.HandoffVacaterIds.Clear();
        plan.VacaterForwardSectors.Clear();

        // Passo 3b: handoff — capturador mais saudável herda objetivo parcial;
        //           capturador original fica livre para o backtracking reatribuir
        EvaluateCaptureHandoffs(plan, aiTeam);

        // Passo 3d: transporte vazio perto demais do objetivo deixa de ser logística.
        // Se a captura não está em desvantagem de combate, libera o APC para voltar ao HQ
        // ou buscar passageiro de outro plano distante.
        ReleaseShortRangeEmptyTransportAssignments(plan, aiTeam);

        // Passo 3e: objetivos ofensivos sem capturador preenchido não sustentam apoio.
        // Assalto/Fogo/Transporte só acompanham uma captura real; sem capturador, voltam
        // ao pool livre e serão reatribuídos depois se o plano receber capturador.
        ReleaseOffensiveSupportWithoutCapturer(plan, aiTeam);

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
            else if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)
                && defInfo.IsFullyControlled && defInfo.ControllingTeam == aiTeam)
            {
                isDefensive = true;
                tc = defInfo.RepresentativeCell; tc.z = 0;
            }
            else continue;
            if (!isDefensive && cascadeCovered.Contains(obj.Sector)) continue;

            // 2º slot de setor de alto risco: só abre se houver capturador livre que possa
            // chegar junto com o 1º (co-chegada). Evita mandar unidade recém-comprada a 10h.
            if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo slotInfo)
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
            // Exceção: setor com captura própria parcial tem risco efetivo reduzido à metade —
            // já temos presença lá, o risco real é menor que num setor virgem de mesmo risco nominal.
            var riskMultipliers = new float[nObj];
            for (int oj = 0; oj < nObj; oj++)
            {
                riskMultipliers[oj] = 1f;
                if (SectorManager.TryGetSectorInfo(assignableObjs[oj].obj.Sector,
                        out SectorManager.SectorInfo sInfo))
                {
                    riskMultipliers[oj] = 1f + sInfo.GetRiskRatioFor(aiTeam) * RiskCostWeight;
                    foreach (SectorManager.SectorConstructionInfo c in sInfo.Constructions)
                    {
                        if (c.OwnerTeam == aiTeam && c.CapturePointsMax > 0
                            && c.CurrentCapturePoints > 0 && c.CurrentCapturePoints < c.CapturePointsMax)
                        {
                            riskMultipliers[oj] = Mathf.Max(1f, riskMultipliers[oj] * 0.5f);
                            break;
                        }
                    }
                }
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
                if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)) continue;
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

            // Raio máximo para atribuir um batedor a defesa estável (sem emergência).
            // Unidades além desse raio ficam como rogues e pressionam o front inimigo em vez
            // de voltar à base — assegurar Base a 11 PM não vale mandar o batedor de volta.
            const float DefenseEscortMaxPM = 8f;
            foreach (SectorObjective obj in plan.Objectives)
            {
                if (freeAssaults.Count == 0) break;
                if (obj.Status != ObjectiveStatus.Defending) continue;
                if (HasAnySlot(obj, UnitRole.Assalto)) continue;
                if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)) continue;
                Vector3Int targetCell = defInfo.RepresentativeCell; targetCell.z = 0;

                UnitManager best = null;
                float bestDist = float.MaxValue;
                foreach (UnitManager u in freeAssaults)
                {
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float d = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                        ? td : SectorManager.HexDistance(uc, targetCell);
                    if (d > DefenseEscortMaxPM) continue;
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
                if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo escortInfo)) continue;
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
                // Fallback escort: Medium sectors first, then Low sectors with risk >= threshold.
                // Low sectors below MinLowRiskForEscort (safe backyard near own HQ) are excluded.
                const float MinLowRiskForEscort = 0.45f;
                var escortFallbacks = new List<SectorObjective>();
                foreach (SectorObjective obj in plan.Objectives)
                {
                    if (!HasFilledSlot(obj, UnitRole.Capturador)) continue;
                    if (HasAnySlot(obj, UnitRole.Assalto)) continue;
                    if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo escortInfo)) continue;
                    var riskLevel = escortInfo.GetRiskLevelFor(aiTeam);
                    bool isMedium = riskLevel == SectorManager.SectorRiskLevel.Medium;
                    bool isLow = riskLevel == SectorManager.SectorRiskLevel.Low
                        && escortInfo.GetRiskRatioFor(aiTeam) >= MinLowRiskForEscort;
                    if (!isMedium && !isLow) continue;
                    escortFallbacks.Add(obj);
                }

                escortFallbacks.Sort((a, b) =>
                {
                    bool aIsMed = TryGetAnySectorInfo(a.Sector, out SectorManager.SectorInfo ai)
                        && ai.GetRiskLevelFor(aiTeam) == SectorManager.SectorRiskLevel.Medium;
                    bool bIsMed = TryGetAnySectorInfo(b.Sector, out SectorManager.SectorInfo bi)
                        && bi.GetRiskLevelFor(aiTeam) == SectorManager.SectorRiskLevel.Medium;
                    if (aIsMed != bIsMed) return aIsMed ? -1 : 1; // Medium first
                    float ar = SectorManager.TryGetSectorInfo(a.Sector, out SectorManager.SectorInfo aInfo)
                        ? aInfo.GetRiskRatioFor(aiTeam) : 0f;
                    float br = SectorManager.TryGetSectorInfo(b.Sector, out SectorManager.SectorInfo bInfo)
                        ? bInfo.GetRiskRatioFor(aiTeam) : 0f;
                    int riskCompare = br.CompareTo(ar);
                    return riskCompare != 0 ? riskCompare : a.Priority.CompareTo(b.Priority);
                });

                foreach (SectorObjective obj in escortFallbacks)
                {
                    if (freeAssaults.Count == 0) break;
                    if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo escortInfo)) continue;
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
                    Debug.Log($"{TL("Plan")} Assalto {best.InstanceId} → batedor fallback {escortInfo.GetRiskLevelFor(aiTeam)} de {obj.Sector} " +
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
                if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)) continue;
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

        // Passo 5g: artilharia primária livre acompanha o front em vez de ficar
        // como rogue estacionário na base sem alvo.
        {
            List<UnitManager> freeFireSupports = GetAvailablePrimaryFireSupports(aiTeam);
            foreach (UnitManager u in freeFireSupports)
            {
                if (assignedIds.Contains(u.InstanceId)) continue;

                SectorObjective bestObj = null;
                float bestScore = float.MinValue;
                float bestDist = float.MaxValue;
                foreach (SectorObjective obj in plan.Objectives)
                {
                    if (obj == null || obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned)
                        continue;
                    if (!HasFilledSlot(obj, UnitRole.Capturador) && !HasFilledSlot(obj, UnitRole.Assalto))
                        continue;
                    if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
                        continue;

                    ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
                    Vector3Int targetCell = tgt != null ? tgt.CurrentCellPosition : info.RepresentativeCell;
                    targetCell.z = 0;

                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float dist = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                        ? td : SectorManager.HexDistance(uc, targetCell);
                    float risk = info.GetRiskRatioFor(aiTeam);
                    float score = -obj.Priority * 900f
                        - dist * 45f
                        + risk * 220f
                        + (obj.Status == ObjectiveStatus.Defending ? 180f : 0f);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestObj = obj;
                        bestDist = dist;
                    }
                }

                if (bestObj == null) continue;
                if (!bestObj.HasOpenSlot(UnitRole.FogoIndireto))
                    bestObj.Slots.Add(new SlotNeed { Role = UnitRole.FogoIndireto });
                bestObj.TryFillSlot(UnitRole.FogoIndireto, u.InstanceId);
                assignedIds.Add(u.InstanceId);
                ApplyPlanHUD(u, bestObj, UnitRole.FogoIndireto);
                bool fsIsEnemySector;
                if (ConstructionSectorHelper.IsBase(bestObj.Sector))
                    fsIsEnemySector = FindHQTeamInSector(bestObj.Sector) != aiTeam;
                else
                    fsIsEnemySector = TryGetAnySectorInfo(bestObj.Sector, out SectorManager.SectorInfo fsSecInfo)
                        && fsSecInfo.ControllingTeam != aiTeam;
                string fsActionLabel = fsIsEnemySector ? "apoio a captura de" : "apoio de";
                Debug.Log($"{TL("Plan")} FireSupport {u.InstanceId} -> {fsActionLabel} {bestObj.Sector} (dist={bestDist:F0}h score={bestScore:F0})");
            }
        }

        // Passo 5f: atribui transportadores livres a slots de Transportador abertos.
        // Se um objetivo nao abriu slot, mas tem capturador de campo longe e o APC
        // consegue oferecer pickup agora, cria um slot oportunista para esse setor.
        // Critério primário: capturer de campo (não em prédio de produção) mais longe do objetivo.
        //   Capturadores recém-comprados ainda na fábrica são ignorados no scoring — eles embarcam
        //   naturalmente no APC que já está lá; o que importa é o capturer já desdobrado no campo.
        //   Usamos o máximo entre todos os capturadores de campo para não depender da ordem dos slots.
        // Critério secundário: APC mais perto do objetivo (chega antes).
        {
            List<UnitManager> freeTransporters = GetAvailableTransporters(aiTeam);
            foreach (UnitManager u in freeTransporters)
            {
                if (assignedIds.Contains(u.InstanceId)) continue;
                if (!u.TryGetUnitData(out UnitData uData) || uData == null) continue;

                SectorObjective bestObj = null;
                UnitManager bestPassenger = null;
                float bestScore = float.MinValue;
                float bestCapturerDist = -1f;
                float bestRisk = float.MaxValue;
                float bestApcDist = float.MaxValue;
                float bestPickupDist = float.MaxValue;
                bool bestCreatedSlot = false;
                int planThreshold = GetEffectiveTransportThreshold(aiTeam);
                foreach (SectorObjective obj in plan.Objectives)
                {
                    bool hasOpenTransportSlot = obj.HasOpenSlot(UnitRole.Transportador);
                    Vector3Int tc;
                    ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
                    if (tgt != null)
                    {
                        tc = tgt.CurrentCellPosition; tc.z = 0;
                    }
                    else if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo targetInfo))
                    {
                        tc = targetInfo.RepresentativeCell; tc.z = 0;
                    }
                    else
                    {
                        continue;
                    }
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;

                    // Scoring: maior distância entre capturadores de CAMPO (não em prédio de produção).
                    // Capturadores recém-comprados na fábrica não distorcem o score quando há
                    // capturadores de campo disponíveis — eles não precisam que o APC viaje até eles.
                    // Fallback: se o setor tem APENAS recém-comprados (time destruído, por exemplo),
                    // usa a distância deles mesmo — precisam de carona e não há opção melhor.
                    float sectorRisk = 0f;
                    float capturerDistToObj = -1f;
                    float pickupDist = float.MaxValue;
                    UnitManager pickupPassenger = null;
                    TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo sInfo);
                    if (sInfo != null) sectorRisk = sInfo.GetRiskRatioFor(aiTeam);
                    foreach (SlotNeed slot in obj.Slots)
                    {
                        if (slot.Role != UnitRole.Capturador || !slot.Filled) continue;
                        UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                        if (capturer == null || capturer.IsEmbarked) continue;
                        if (!capturer.TryGetUnitData(out UnitData capData) || FindFittingSlotIndex(u, uData, capturer, capData) < 0) continue;
                        Vector3Int cc = capturer.CurrentCellPosition; cc.z = 0;
                        if (IsTeamProductionBuilding(cc, aiTeam)) continue; // prefere campo
                        float dist = TerrainCostToCell(capturer, cc, tc, planThreshold * 2);
                        float apcToPassenger = SectorManager.HexDistance(uc, cc);
                        if (dist > capturerDistToObj
                            || (Mathf.Abs(dist - capturerDistToObj) <= 0.5f && apcToPassenger < pickupDist))
                        {
                            capturerDistToObj = dist;
                            pickupDist = apcToPassenger;
                            pickupPassenger = capturer;
                        }
                    }
                    if (capturerDistToObj < 0f) // nenhum de campo — fallback para recém-comprados
                    {
                        foreach (SlotNeed slot in obj.Slots)
                        {
                            if (slot.Role != UnitRole.Capturador || !slot.Filled) continue;
                            UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                            if (capturer == null || capturer.IsEmbarked) continue;
                            if (!capturer.TryGetUnitData(out UnitData capData) || FindFittingSlotIndex(u, uData, capturer, capData) < 0) continue;
                            Vector3Int cc = capturer.CurrentCellPosition; cc.z = 0;
                            float dist = TerrainCostToCell(capturer, cc, tc, planThreshold * 2);
                            float apcToPassenger = SectorManager.HexDistance(uc, cc);
                            if (dist > capturerDistToObj
                                || (Mathf.Abs(dist - capturerDistToObj) <= 0.5f && apcToPassenger < pickupDist))
                            {
                                capturerDistToObj = dist;
                                pickupDist = apcToPassenger;
                                pickupPassenger = capturer;
                            }
                        }
                    }
                    if (capturerDistToObj < 0f) continue; // sem nenhum capturer alocado — pula

                    // Build list of active non-embarked capturers for compatibility check.
                    List<UnitManager> activeCapturers = new List<UnitManager>();
                    foreach (SlotNeed ts in obj.Slots)
                    {
                        if (ts.Role != UnitRole.Capturador || !ts.Filled) continue;
                        UnitManager cap = FindActiveUnit(ts.AssignedUnitId, aiTeam);
                        if (cap != null && !cap.IsEmbarked) activeCapturers.Add(cap);
                    }
                    // Skip if this transporter cannot carry any capturer in this objective (wrong slot type).
                    if (GetCompatibleSlotCapacity(u, activeCapturers) == 0) continue;

                    float apcDistToObj = SectorManager.HexDistance(uc, tc);
                    float pickupReach = Mathf.Max(0, u.RemainingMovementPoints) + ShuttlePickupRange;

                    // Compare compatible transport capacity against active capturers — supports multi-slot transports (e.g. helicopter).
                    int totalTransportCapacity = 0;
                    foreach (SlotNeed ts in obj.Slots)
                    {
                        if (ts.Role != UnitRole.Transportador || !ts.Filled) continue;
                        UnitManager transporter = FindActiveUnit(ts.AssignedUnitId, aiTeam);
                        if (transporter == null) continue;
                        totalTransportCapacity += GetCompatibleSlotCapacity(transporter, activeCapturers);
                    }
                    bool transportCapacityMet = totalTransportCapacity >= activeCapturers.Count;
                    bool canCreateOpportunisticSlot = !hasOpenTransportSlot
                        && !transportCapacityMet
                        && capturerDistToObj >= GetEffectiveTransportThreshold(aiTeam)
                        && pickupDist <= pickupReach + 0.5f;
                    if (!hasOpenTransportSlot && !canCreateOpportunisticSlot) continue;

                    float localScore = capturerDistToObj * 120f
                        - pickupDist * 95f
                        - apcDistToObj * 8f
                        - sectorRisk * 120f;
                    if (canCreateOpportunisticSlot) localScore += 180f;
                    const float eps = 0.5f;
                    bool isBetter = localScore > bestScore + eps
                        || (localScore >= bestScore - eps && pickupDist < bestPickupDist - eps)
                        || (localScore >= bestScore - eps && pickupDist < bestPickupDist + eps && capturerDistToObj > bestCapturerDist + eps)
                        || (localScore >= bestScore - eps && pickupDist < bestPickupDist + eps && capturerDistToObj >= bestCapturerDist - eps && sectorRisk < bestRisk - 0.01f)
                        || (localScore >= bestScore - eps && pickupDist < bestPickupDist + eps && capturerDistToObj >= bestCapturerDist - eps && sectorRisk < bestRisk + 0.01f && apcDistToObj < bestApcDist);
                    if (isBetter)
                    {
                        bestScore = localScore;
                        bestCapturerDist = capturerDistToObj;
                        bestRisk = sectorRisk;
                        bestApcDist = apcDistToObj;
                        bestPickupDist = pickupDist;
                        bestPassenger = pickupPassenger;
                        bestCreatedSlot = canCreateOpportunisticSlot;
                        bestObj = obj;
                    }
                }

                if (bestObj == null) continue;
                if (bestCreatedSlot)
                    bestObj.Slots.Add(new SlotNeed { Role = UnitRole.Transportador });
                bestObj.TryFillSlot(UnitRole.Transportador, u.InstanceId);
                assignedIds.Add(u.InstanceId);
                ApplyPlanHUD(u, bestObj, UnitRole.Transportador);
                string passengerLabel = bestPassenger != null ? $" passenger={bestPassenger.InstanceId}" : " passenger=?";
                string slotLabel = bestCreatedSlot ? " slot=opportunistic" : " slot=open";
                Debug.Log($"{TL("Plan")} Transportador {u.InstanceId} -> {bestObj.Sector} ({passengerLabel}{slotLabel} capturerDist={bestCapturerDist:F0}h pickup={bestPickupDist:F0}h risk={bestRisk:F2} apcDist={bestApcDist:F0}h score={bestScore:F0})");
            }
        }

        // Passo 6: reaplica HUD para atribuições anteriores
        ReleaseOffensiveSupportWithoutCapturer(plan, aiTeam);

        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && !freeCapturers.Exists(u => u.InstanceId == slot.AssignedUnitId))
                {
                    UnitManager u = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                    if (u != null) ApplyPlanHUD(u, obj, slot.Role);
                }

        // Passo 7: limpa badge de unidades que não estão em nenhum slot ativo do plano
        var slottedIds = new HashSet<int>();
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled) slottedIds.Add(slot.AssignedUnitId);
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId != aiTeam || u.IsDead) continue;
            if (!slottedIds.Contains(u.InstanceId)) u.ClearAIAssignedPlan();
        }

        // Passo 8: atualiza distância real de cada slot até seu objetivo (propaga pelo transporter se embarcado)
        RefreshSlotDistances(plan, aiTeam);

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
                string distStr = slot.DistanceToObjective >= 0 ? $"{slot.DistanceToObjective}h" : "?";
                string embarkedTag = (u != null && u.IsEmbarked) ? " [APC]" : "";
                planLog.AppendLine($"  pri={obj.Priority} {obj.Sector}: {slot.Role} Unit{slot.AssignedUnitId}{embarkedTag} @ {distStr}");
            }
        }
        planLog.Append($"  → {totalAssigned} atribuídos | {plan.RogueUnitIds.Count} rogues");
        Debug.Log(planLog.ToString());
    }

    private void RefreshSlotDistances(TeamObjectivePlan plan, TeamId aiTeam)
    {
        foreach (SectorObjective obj in plan.Objectives)
        {
            Vector3Int targetCell = Vector3Int.zero;
            bool hasTarget = false;
            ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
            if (tgt != null)
            {
                targetCell = tgt.CurrentCellPosition; targetCell.z = 0;
                hasTarget = true;
            }
            else if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
            {
                targetCell = info.RepresentativeCell; targetCell.z = 0;
                hasTarget = true;
            }

            foreach (SlotNeed slot in obj.Slots)
            {
                slot.DistanceToObjective = -1;
                if (!slot.Filled || !hasTarget) continue;

                UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (unit == null) continue;

                // Unidade embarcada: usa posição do transportador como ponto de partida
                Vector3Int fromCell = (unit.IsEmbarked && unit.EmbarkedTransporter != null)
                    ? unit.EmbarkedTransporter.CurrentCellPosition
                    : unit.CurrentCellPosition;
                fromCell.z = 0;

                if (unit.TryGetUnitData(out UnitData unitData) && unitData != null
                    && SectorManager.TryGetLandMovementDistance(fromCell, targetCell, unitData, out int d1))
                    slot.DistanceToObjective = d1;
                else if (SectorManager.TryGetLandMovementDistance(fromCell, targetCell, out int d2))
                    slot.DistanceToObjective = d2;
                else
                    slot.DistanceToObjective = (int)SectorManager.HexDistance(fromCell, targetCell);
            }
        }
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

    // -------------------------------------------------------------------------
    // Mid-turn threat invalidation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Removes Defending objectives whose threat was eliminated mid-turn.
    /// Called at the top of every Phase 2 iteration after fog refresh so that
    /// units freed from a stale defense plan can redirect immediately.
    /// </summary>
    internal void InvalidateStaleThreatObjectives(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null) return;

        for (int i = plan.Objectives.Count - 1; i >= 0; i--)
        {
            SectorObjective obj = plan.Objectives[i];
            if (obj.Status != ObjectiveStatus.Defending) continue;

            bool stillThreatened;
            if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
            {
                // Critical home sectors use the broader HomeDefenseThreatRange check
                // (covers capture-in-progress and nearby enemies at construction level).
                bool criticalHomeThreat = IsCriticalHomeDefenseSector(info, aiTeam)
                    && IsHomeDefenseThreatened(info, aiTeam, HomeDefenseThreatRange);
                if (criticalHomeThreat)
                {
                    stillThreatened = true;
                }
                else
                {
                    Vector3Int rc = info.RepresentativeCell; rc.z = 0;
                    stillThreatened = HasNearbyVisibleEnemy(rc, aiTeam, defenseEnemyRange);
                    // Base sectors are rectangles — also scan each construction cell
                    // so enemies near corner buildings aren't missed by the representative cell.
                    if (!stillThreatened && ConstructionSectorHelper.IsBase(obj.Sector))
                    {
                        foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
                        {
                            Vector3Int cc = c.Cell; cc.z = 0;
                            if (HasNearbyVisibleEnemy(cc, aiTeam, defenseEnemyRange))
                            {
                                stillThreatened = true;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                stillThreatened = true; // can't confirm — preserve
            }

            if (!stillThreatened)
            {
                Debug.Log($"{TL("Plan")} Objetivo defensivo {obj.Sector} invalidado mid-turn (ameaça eliminada)");
                ClearObjectiveHUD(obj);
                plan.Objectives.RemoveAt(i);
            }
        }
    }

    private void ApplyPlanHUD(UnitManager unit, SectorObjective obj, UnitRole role = UnitRole.Capturador)
    {
        string sectorName = obj.Sector.ToString();
        string badge;
        if (ConstructionSectorHelper.IsBase(obj.Sector))
        {
            // Identifica o dono do HQ nesse setor para saber se é base própria ou inimiga.
            TeamId hqTeam = FindHQTeamInSector(obj.Sector);
            badge = hqTeam != obj.AssignedTeam
                ? ">>"
                : IsCriticalHomeDefenseObjective(obj, obj.AssignedTeam) ? "!" : "#";
        }
        else
        {
            badge = sectorName.Length > 0 ? sectorName[0].ToString().ToUpper() : "?";
        }
        unit.SetAIAssignedPlan(sectorName, sectorName, badge, (int)role, showAIUnitHUD);
    }

    private static TeamId FindHQTeamInSector(ConstructionSector sector)
    {
        foreach (ConstructionManager c in ConstructionManager.AllActive)
            if (c.Sector == sector && c.IsPlayerHeadQuarter)
                return c.TeamId;
        return TeamId.Neutral;
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

    // Returns total slot capacity of a transporter that can physically carry at least one unit in the list.
    // Uses PodeEmbarcarSensor.CanUseSlot as source of truth (class, skills, domain/height).
    private static int GetCompatibleSlotCapacity(UnitManager transporter, List<UnitManager> capturers)
    {
        if (transporter == null || capturers == null || capturers.Count == 0) return 0;
        if (!transporter.TryGetUnitData(out UnitData tData) || tData == null
            || tData.transportSlots == null || tData.transportSlots.Count == 0) return 0;
        int total = 0;
        foreach (UnitTransportSlotRule tSlot in tData.transportSlots)
        {
            foreach (UnitManager cap in capturers)
            {
                if (!cap.TryGetUnitData(out UnitData cData) || cData == null) continue;
                if (PodeEmbarcarSensor.CanUseSlot(cap, cData, tSlot, out _))
                {
                    total += Mathf.Max(1, tSlot.capacity);
                    break;
                }
            }
        }
        return total;
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

    private static List<UnitManager> GetAvailablePrimaryFireSupports(TeamId aiTeam)
    {
        var list = new List<UnitManager>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId != aiTeam || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
            if (!u.TryGetUnitData(out UnitData data)) continue;
            if (data.roles != null && data.roles.Count > 0 && data.roles[0] == UnitRole.FogoIndireto)
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

    private void SortPlanObjectivesByStrategicPriority(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        bool priorityNumberAscending)
    {
        if (plan == null) return;

        plan.Objectives.Sort((a, b) =>
        {
            bool aCritical = IsCriticalHomeDefenseObjective(a, aiTeam);
            bool bCritical = IsCriticalHomeDefenseObjective(b, aiTeam);
            if (aCritical != bCritical) return aCritical ? -1 : 1;

            bool aDefending = a.Status == ObjectiveStatus.Defending;
            bool bDefending = b.Status == ObjectiveStatus.Defending;
            if (aDefending != bDefending) return aDefending ? 1 : -1;

            return priorityNumberAscending
                ? a.Priority.CompareTo(b.Priority)
                : b.Priority.CompareTo(a.Priority);
        });

        for (int i = 0; i < plan.Objectives.Count; i++)
            plan.Objectives[i].Priority = i + 1;
    }

    private int HomeDefenseThreatRange => Mathf.Max(3, defenseEnemyRange);

    private static bool TryGetAnySectorInfo(ConstructionSector sector, out SectorManager.SectorInfo info)
    {
        if (SectorManager.TryGetSectorInfo(sector, out info))
            return true;
        return SectorManager.TryGetBaseInfo(sector, out info);
    }

    private bool IsCriticalHomeDefenseObjective(SectorObjective obj, TeamId aiTeam)
    {
        if (obj == null || obj.Status != ObjectiveStatus.Defending)
            return false;
        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
            return IsCriticalHomeDefenseSector(info, aiTeam)
                && IsHomeDefenseThreatened(info, aiTeam, HomeDefenseThreatRange);

        return IsCriticalHomeDefenseSector(obj.Sector, aiTeam)
            && IsHomeDefenseThreatened(obj.Sector, aiTeam, HomeDefenseThreatRange);
    }

    private bool TryFindCriticalHomeDefenseObjective(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        out SectorObjective best)
    {
        best = null;
        if (plan == null)
            return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (!IsCriticalHomeDefenseObjective(obj, aiTeam))
                continue;
            if (best == null || obj.Priority < best.Priority)
                best = obj;
        }

        return best != null;
    }

    private bool TryFindCriticalHomeDefenseObjectiveForUnit(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        UnitManager unit,
        Vector3Int fromCell,
        string callerLabel,
        out SectorObjective best)
    {
        best = null;
        if (plan == null || unit == null)
            return false;

        int responseRange = Mathf.Max(HomeDefenseThreatRange, defenseEnemyRange * defenseCallRange);
        float bestDistance = float.MaxValue;
        bool foundCritical = false;
        fromCell.z = 0;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (!IsCriticalHomeDefenseObjective(obj, aiTeam))
                continue;
            foundCritical = true;
            Vector3Int targetCell = ResolveCriticalHomeDefenseTargetCell(obj, aiTeam, fromCell);
            float distance = CalculateUnitResponseDistance(unit, fromCell, targetCell);
            if (distance > responseRange)
            {
                Debug.Log($"{TL("Plan")} {callerLabel} {unit.InstanceId} nao redireciona para {obj.Sector}: dist={distance:F0}h > chamada={responseRange}h");
                continue;
            }
            if (best == null
                || obj.Priority < best.Priority
                || (obj.Priority == best.Priority && distance < bestDistance))
            {
                best = obj;
                bestDistance = distance;
            }
        }

        if (!foundCritical)
            Debug.Log($"{TL("Plan")} {callerLabel} {unit.InstanceId} nao achou SOS Base/HQ ativo no plano");

        return best != null;
    }

    private Vector3Int ResolveCriticalHomeDefenseTargetCell(
        SectorObjective obj,
        TeamId aiTeam,
        Vector3Int fallback)
    {
        ConstructionManager target = obj != null
            ? FindCapturableInSector(obj.Sector, aiTeam, fallback)
            : null;
        if (target != null)
        {
            Vector3Int tc = target.CurrentCellPosition; tc.z = 0;
            return tc;
        }

        if (obj != null && TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
        {
            Vector3Int rc = info.RepresentativeCell; rc.z = 0;
            return rc;
        }

        if (obj != null && TryFindHomeDefenseLiveAnchor(obj.Sector, aiTeam, fallback, out Vector3Int liveAnchor))
            return liveAnchor;

        fallback.z = 0;
        return fallback;
    }

    private static float CalculateUnitResponseDistance(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell)
    {
        fromCell.z = 0;
        targetCell.z = 0;
        if (unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && SectorManager.TryGetLandMovementDistance(fromCell, targetCell, data, out int unitCost))
            return unitCost;
        if (SectorManager.TryGetLandMovementDistance(fromCell, targetCell, out int terrainCost))
            return terrainCost;
        return SectorManager.HexDistance(fromCell, targetCell);
    }

    private void EnsureCriticalHomeDefenseObjectives(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        IReadOnlyList<SectorManager.SectorInfo> allSectors)
    {
        if (plan == null || allSectors == null)
            return;

        foreach (SectorManager.SectorInfo info in allSectors)
        {
            if (!IsCriticalHomeDefenseSector(info, aiTeam))
                continue;
            if (!IsHomeDefenseThreatened(info, aiTeam, HomeDefenseThreatRange))
                continue;

            SectorObjective obj = plan.GetObjectiveForSector(info.Sector);
            if (obj == null)
            {
                obj = new SectorObjective
                {
                    Sector = info.Sector,
                    AssignedTeam = aiTeam,
                    Status = ObjectiveStatus.Defending,
                    Priority = int.MaxValue,
                };
                plan.Objectives.Add(obj);
            }
            else
            {
                obj.Status = ObjectiveStatus.Defending;
                obj.AssignedTeam = aiTeam;
                obj.Priority = int.MaxValue;
            }

            RemoveTransportSlotsFromDefense(obj, aiTeam);
            EnsureOpenSlots(obj, UnitRole.Capturador, 2);

            Debug.Log($"{TL("Plan")} SOS Base/HQ: {info.Sector} sob captura/ameaca critica");
        }
    }

    private void ClearResolvedCriticalHomeDefenseObjectives(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null || plan.Objectives == null)
            return;

        for (int i = plan.Objectives.Count - 1; i >= 0; i--)
        {
            SectorObjective obj = plan.Objectives[i];
            if (obj == null || obj.Status != ObjectiveStatus.Defending)
                continue;
            if (!IsCriticalHomeDefenseSector(obj.Sector, aiTeam))
                continue;
            if (IsHomeDefenseThreatened(obj.Sector, aiTeam, HomeDefenseThreatRange))
                continue;
            if (FindCapturableInSector(obj.Sector, aiTeam) != null)
                continue;

            if (obj.Slots != null)
            {
                foreach (SlotNeed slot in obj.Slots)
                {
                    if (slot == null || !slot.Filled)
                        continue;
                    UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                    unit?.ClearAIAssignedPlan();
                }
            }

            ClearObjectiveHUD(obj);
            plan.Objectives.RemoveAt(i);
            Debug.Log($"{TL("Plan")} SOS Base/HQ resolvido: {obj.Sector} seguro - objetivo defensivo removido");
        }
    }

    private void EnsureCriticalHomeDefenseObjectivesFromConstructions(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null)
            return;

        var sectors = new HashSet<ConstructionSector>();
        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null)
                continue;
            if (!IsCriticalHomeDefenseSector(construction.Sector, aiTeam))
                continue;
            if (!IsHomeDefenseThreatened(construction.Sector, aiTeam, HomeDefenseThreatRange))
                continue;
            sectors.Add(construction.Sector);
        }

        foreach (ConstructionSector sector in sectors)
        {
            SectorObjective obj = plan.GetObjectiveForSector(sector);
            if (obj == null)
            {
                obj = new SectorObjective
                {
                    Sector = sector,
                    AssignedTeam = aiTeam,
                    Status = ObjectiveStatus.Defending,
                    Priority = int.MaxValue,
                };
                plan.Objectives.Add(obj);
            }
            else
            {
                obj.Status = ObjectiveStatus.Defending;
                obj.AssignedTeam = aiTeam;
                obj.Priority = int.MaxValue;
            }

            RemoveTransportSlotsFromDefense(obj, aiTeam);
            EnsureOpenSlots(obj, UnitRole.Capturador, 2);

            Debug.Log($"{TL("Plan")} SOS Base/HQ live: {sector} sob captura/ameaca no conjunto da base");
        }
    }

    private void RemoveTransportSlotsFromDefense(SectorObjective obj, TeamId aiTeam)
    {
        if (obj == null || obj.Slots == null)
            return;

        for (int i = obj.Slots.Count - 1; i >= 0; i--)
        {
            SlotNeed slot = obj.Slots[i];
            if (slot.Role != UnitRole.Transportador)
                continue;
            if (slot.Filled)
            {
                UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                unit?.ClearAIAssignedPlan();
            }
            obj.Slots.RemoveAt(i);
        }
    }

    private void ReleaseShortRangeEmptyTransportAssignments(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null || plan.Objectives == null)
            return;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Slots == null)
                continue;

            if (!TryResolveObjectiveTargetCell(obj, aiTeam, out Vector3Int targetCell))
                continue;

            for (int i = obj.Slots.Count - 1; i >= 0; i--)
            {
                SlotNeed slot = obj.Slots[i];
                if (slot.Role != UnitRole.Transportador)
                    continue;

                if (!slot.Filled)
                    continue;

                UnitManager transporter = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (transporter == null)
                {
                    obj.Slots.RemoveAt(i);
                    continue;
                }

                if (HasTransportCargo(transporter))
                    continue;

                // APC vazio perto de base inimiga deve FICAR — está em território adversário,
                // não num destino seguro onde o capturador pode caminhar sozinho.
                // Usa HQ como referência — ControllingTeam pode estar incorreto se prédios
                // capturáveis tiverem slotIndex apontando pro time errado no snapshot.
                if (ConstructionSectorHelper.IsBase(obj.Sector)
                    && FindHQTeamInSector(obj.Sector) != aiTeam)
                    continue;

                Vector3Int transporterCell = transporter.CurrentCellPosition; transporterCell.z = 0;
                float apcDist = SectorManager.HexDistance(transporterCell, targetCell);
                int effectiveThreshold = GetEffectiveTransportThreshold(aiTeam);
                if (apcDist >= effectiveThreshold)
                    continue;

                bool supportNeeded = IsObjectiveInCombatDisadvantage(obj, aiTeam, targetCell,
                    alliesEnemyRange, alliesAgainstEnemiesHpRatio, out int enemyHp, out int allyHp);
                if (supportNeeded)
                {
                    Debug.Log($"{TL("Plan")} Transportador {transporter.InstanceId} mantém apoio em {obj.Sector} " +
                              $"({apcDist:F0}h<{effectiveThreshold}h, combate inimigo={enemyHp}HP aliado={allyHp}HP)");
                    continue;
                }

                transporter.ClearAIAssignedPlan();
                obj.Slots.RemoveAt(i);
                Debug.Log($"{TL("Plan")} Transportador {transporter.InstanceId} libera {obj.Sector}: " +
                          $"vazio e perto ({apcDist:F0}h<{effectiveThreshold}h), volta a shuttle/HQ");
            }
        }
    }

    private void ReleaseOffensiveSupportWithoutCapturer(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null || plan.Objectives == null)
            return;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Slots == null)
                continue;
            if (HasFilledSlot(obj, UnitRole.Capturador))
                continue;

            for (int i = obj.Slots.Count - 1; i >= 0; i--)
            {
                SlotNeed slot = obj.Slots[i];
                if (slot.Role == UnitRole.Capturador)
                    continue;
                if (slot.Role != UnitRole.Assalto
                    && slot.Role != UnitRole.FogoIndireto
                    && slot.Role != UnitRole.Transportador)
                    continue;

                if (slot.Filled)
                {
                    UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                    unit?.ClearAIAssignedPlan();
                    Debug.Log($"{TL("Plan")} {slot.Role} {slot.AssignedUnitId} liberado de {obj.Sector}: sem capturador no plano");
                }

                obj.Slots.RemoveAt(i);
            }
        }
    }

    private bool TryResolveObjectiveTargetCell(SectorObjective obj, TeamId aiTeam, out Vector3Int targetCell)
    {
        targetCell = Vector3Int.zero;
        if (obj == null)
            return false;

        ConstructionManager target = FindCapturableInSector(obj.Sector, aiTeam);
        if (target != null)
        {
            targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            return true;
        }

        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
        {
            targetCell = info.RepresentativeCell;
            targetCell.z = 0;
            return true;
        }

        return false;
    }

    private static void EnsureOpenSlots(SectorObjective obj, UnitRole role, int desiredTotal)
    {
        if (obj == null || obj.Slots == null)
            return;

        int total = 0;
        foreach (SlotNeed slot in obj.Slots)
            if (slot.Role == role)
                total++;

        while (total < desiredTotal)
        {
            obj.Slots.Add(new SlotNeed { Role = role });
            total++;
        }
    }

    private static bool IsCriticalHomeDefenseSector(SectorManager.SectorInfo info, TeamId aiTeam)
    {
        if (info == null)
            return false;
        if (IsCriticalHomeDefenseSector(info.Sector, aiTeam))
            return true;

        foreach (SectorManager.SectorConstructionInfo construction in info.Constructions)
        {
            if (construction?.Source == null) continue;
            if (construction.Source.IsPlayerHeadQuarter && construction.OwnerTeam == aiTeam)
                return true;
        }

        return false;
    }

    private static bool IsCriticalHomeDefenseSector(ConstructionSector sector, TeamId aiTeam)
    {
        if (ConstructionSectorHelper.IsBase(sector))
            return FindHQTeamInSector(sector) == aiTeam;

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.Sector != sector)
                continue;
            if (construction.TeamId == aiTeam && construction.IsPlayerHeadQuarter)
                return true;
        }

        return false;
    }

    private bool IsHomeDefenseThreatened(SectorManager.SectorInfo info, TeamId aiTeam, int range)
    {
        if (info == null)
            return false;
        if (IsHomeDefenseThreatened(info.Sector, aiTeam, range))
            return true;

        Vector3Int rep = info.RepresentativeCell; rep.z = 0;
        if (HasNearbyVisibleEnemy(rep, aiTeam, range))
            return true;

        foreach (SectorManager.SectorConstructionInfo construction in info.Constructions)
        {
            Vector3Int cell = construction.Cell; cell.z = 0;
            if (HasNearbyVisibleEnemy(cell, aiTeam, range))
                return true;
        }

        return false;
    }

    private bool IsHomeDefenseThreatened(ConstructionSector sector, TeamId aiTeam, int range)
    {
        if (!IsCriticalHomeDefenseSector(sector, aiTeam))
            return false;

        MatchController mc = GetMatchController();
        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.Sector != sector)
                continue;

            if (construction.IsCapturable
                && construction.CapturePointsMax > 0
                && (construction.TeamId == aiTeam || construction.TeamId == TeamId.Neutral)
                && construction.CurrentCapturePoints < construction.CapturePointsMax)
                return true;

            Vector3Int cc = construction.CurrentCellPosition; cc.z = 0;
            foreach (UnitManager enemy in UnitManager.AllActive)
            {
                if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked)
                    continue;
                if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam))
                    continue;
                Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                if (SectorManager.HexDistance(ec, cc) <= range)
                    return true;
            }
        }

        return false;
    }

    private bool TryFindHomeDefenseLiveAnchor(
        ConstructionSector sector,
        TeamId aiTeam,
        Vector3Int fromCell,
        out Vector3Int anchor)
    {
        anchor = fromCell;
        ConstructionManager best = null;
        float bestScore = float.MinValue;
        MatchController mc = GetMatchController();
        fromCell.z = 0;
        bool ownBaseSector = ConstructionSectorHelper.IsBase(sector)
            && FindHQTeamInSector(sector) == aiTeam;

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.Sector != sector)
                continue;

            Vector3Int cc = construction.CurrentCellPosition; cc.z = 0;
            bool ownedHomeConstruction = construction.TeamId == aiTeam
                && (ownBaseSector || construction.IsPlayerHeadQuarter);
            bool underCapture = construction.IsCapturable
                && construction.CapturePointsMax > 0
                && ownedHomeConstruction
                && construction.CurrentCapturePoints < construction.CapturePointsMax;
            bool hasNearbyEnemy = false;
            if (ownedHomeConstruction)
            {
                foreach (UnitManager enemy in UnitManager.AllActive)
                {
                    if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked)
                        continue;
                    if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam))
                        continue;
                    Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                    if (SectorManager.HexDistance(ec, cc) <= HomeDefenseThreatRange)
                    {
                        hasNearbyEnemy = true;
                        break;
                    }
                }
            }

            if (!underCapture && !hasNearbyEnemy && !ownedHomeConstruction)
                continue;

            float score = 0f;
            if (underCapture) score += 100000f;
            if (hasNearbyEnemy) score += 50000f;
            if (construction.IsPlayerHeadQuarter) score += 1000f;
            score -= SectorManager.HexDistance(fromCell, cc);

            if (best == null || score > bestScore)
            {
                best = construction;
                bestScore = score;
                anchor = cc;
            }
        }

        return best != null;
    }

    private static bool HasHomeConstructionUnderCapture(ConstructionSector sector, TeamId aiTeam)
    {
        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.Sector != sector)
                continue;
            if (!construction.IsCapturable || construction.CapturePointsMax <= 0)
                continue;
            if (construction.TeamId != aiTeam)
                continue;
            if (construction.CurrentCapturePoints < construction.CapturePointsMax)
                return true;
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
