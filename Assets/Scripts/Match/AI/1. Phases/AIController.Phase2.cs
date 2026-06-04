using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fase 2: Ações de unidades
    // -------------------------------------------------------------------------

    private IEnumerator Phase2_UnitActions(AIWorldSnapshot snapshot)
    {
        if (ShouldStopAIForMatchEnd("phase2_start"))
            yield break;

        TeamId aiTeam = snapshot.AITeam;

        List<UnitManager> initial = GetAvailableUnits(aiTeam);
        if (initial.Count == 0)
        {
            Debug.Log($"{TL()} Fase 2 — sem unidades em campo, pulando.");
            if (matchController == null || !matchController.IsPlayerCommandServiceAutomatic(snapshot.AITeam))
                JogadasManager.EnsureInstance()?.RegistrarServicoComando(snapshot.TurnNumber, (int)aiTeam);
            yield break;
        }

        Debug.Log($"{TL()} Fase2 — iniciando ações.");
        plannedDestinations.Clear();
        var deferredUnitIds = new HashSet<int>();
        Dictionary<int, int> prevGroupCache = null;

        while (isActive && !IsMatchEnded())
        {
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("phase2_loop_apos_pause"))
                yield break;

            List<UnitManager> available = GetAvailableUnits(aiTeam);
            if (available.Count == 0) break;
            if (deferredUnitIds.Count > 0)
            {
                available.RemoveAll(u => u != null && deferredUnitIds.Contains(u.InstanceId));
                if (available.Count == 0)
                {
                    deferredUnitIds.Clear();
                    available = GetAvailableUnits(aiTeam);
                    if (available.Count == 0) break;
                }
            }

            SyncAIUnitCellsFromTransforms();

            // Reconstrói a foto do mundo após cada batch — hexes ocupados mudam
            // BuildLight omite campos não usados pelos handlers (MyUnits, EnemyUnits,
            // OccupiedCells, Stance), reduzindo custo de ~50 iterações por unidade.
            AIWorldSnapshot current = AIWorldSnapshot.BuildLight(aiTeam, matchController);

            // Ordena iniciativa por grupo (menor = age primeiro):
            // 0 = vacater handoff / blocker com inimigo adjacente
            // 1 = helicoptero
            // 2 = unidade ativa liberando corredor/posicionamento
            // 3 = objetivo normal  4 = rogue/sem objetivo
            // 5 = IsUnderRepair / manutencao - age por ultimo
            TeamObjectivePlan activePlan = ObjectiveManager.GetPlanForTeam(aiTeam);
            InvalidateStaleThreatObjectives(activePlan, aiTeam);

            // Pre-pass: atualiza estado de reparo antes do sort para que IsUnderRepair
            // esteja correto quando GetInitiativeGroup classificar cada unidade.
            // Inclui embarcados: passageiro curado pelo CommandService precisa sair do
            // modo reparo antes que o transporter decida rota de evac vs entrega.
            foreach (UnitManager u in UnitManager.AllActive)
            {
                if (u.TeamId == aiTeam && !u.IsDead)
                    UpdateRepairState(u, activePlan);
            }

            // Pre-computa grupos uma vez por unidade (evita O(N log N) chamadas no comparador).
            var groupCache = new Dictionary<int, int>(available.Count);
            foreach (UnitManager u in available)
                groupCache[u.InstanceId] = GetInitiativeGroup(u, activePlan, aiTeam);

            // Dirty flag: grupos podem mudar após cada ação (captura concluída, reparo, etc.).
            // Só re-sort quando ao menos um grupo mudou em relação à iteração anterior.
            bool needsSort = true;
            if (!needsSort)
            {
                foreach (UnitManager u in available)
                {
                    if (!prevGroupCache.TryGetValue(u.InstanceId, out int prev) || prev != groupCache[u.InstanceId])
                    {
                        needsSort = true;
                        break;
                    }
                }
            }

            if (needsSort)
            {
                available.Sort((a, b) =>
                {
                    int groupA = groupCache[a.InstanceId];
                    int groupB = groupCache[b.InstanceId];

                    if (groupA != groupB) return groupA.CompareTo(groupB);

                    // Dentro do grupo 0: blocker (IsBlockingCaptureTarget) age antes de vacater/outros
                    if (groupA == 0 && activePlan != null)
                    {
                        bool blockerA = IsBlockingCaptureTarget(a, activePlan, aiTeam);
                        bool blockerB = IsBlockingCaptureTarget(b, activePlan, aiTeam);
                        if (blockerA != blockerB) return blockerA ? -1 : 1;
                    }

                    // Dentro do grupo 2: combate local real vem antes de apoio de posicionamento
                    // (observador, liberacao de corredor, pickup etc.).
                    if (groupA == 2)
                    {
                        bool combatA = HasInitiativeCombatOpportunity(a, aiTeam);
                        bool combatB = HasInitiativeCombatOpportunity(b, aiTeam);
                        if (combatA != combatB) return combatA ? -1 : 1;
                    }

                    // Dentro do grupo 3: prioridade do objetivo (pri=1 = age primeiro)
                    if (groupA == 3 && activePlan != null)
                    {
                        SectorObjective objA = ResolveAnyAssignedObjective(a, activePlan);
                        SectorObjective objB = ResolveAnyAssignedObjective(b, activePlan);
                        if (objA == null && objB == null) return b.CurrentHP.CompareTo(a.CurrentHP);
                        if (objA == null) return 1;
                        if (objB == null) return -1;

                        int cmp = objA.Priority.CompareTo(objB.Priority);
                        if (cmp != 0) return cmp;

                        return b.CurrentHP.CompareTo(a.CurrentHP);
                    }

                    // Dentro do grupo 4 (rogues): capturadores mais próximos de um
                    // transporter com slot livre agem primeiro — garante embarque antes do heli encher.
                    if (groupA == 4)
                    {
                        float transA = GetDistanceToNearestAvailableTransporter(a, aiTeam);
                        float transB = GetDistanceToNearestAvailableTransporter(b, aiTeam);
                        if (Mathf.Abs(transA - transB) > 0.5f)
                            return transA.CompareTo(transB); // mais perto age primeiro; distantes agem por último
                    }

                    int initiativeCmp = CompareUnitInitiative(a, b);
                    return initiativeCmp != 0 ? initiativeCmp : b.CurrentHP.CompareTo(a.CurrentHP);
                });
            }

            prevGroupCache = groupCache;

            // LOG: ordem de iniciativa apos o sort real.
            {
                var initLog = new System.Text.StringBuilder();
                initLog.AppendLine($"{TL()} Fase2 iniciativa ({available.Count} unidades):");
                foreach (UnitManager u in available)
                {
                    int g  = groupCache[u.InstanceId];
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    Vector3Int? tgt = GetAssignedTargetCell(u, activePlan);
                    string tgtStr = tgt.HasValue ? tgt.Value.ToString() : "null";
                    initLog.AppendLine($"  [grp={g}] {FormatInitiativeUnitName(u)} @ {uc} target={tgtStr}");
                }
                Debug.Log(initLog.ToString());
            }

            UnitManager unit = available[0];
            PlayerAction action = DecideUnitAction(unit, current);

            if (ShouldDeferCapturerForRogueEmbarkBlocker(unit, activePlan, aiTeam,
                    out UnitManager embarkBlocker, out UnitManager blockedTransporter))
            {
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — capturador {unit.InstanceId} cede vez para rogue {embarkBlocker.InstanceId} liberar embarque no transporte {blockedTransporter.InstanceId}");
                continue;
            }

            if (IsNoOpUnitAction(action) && ShouldDeferIdleAssaultForSectorCapturer(unit, activePlan, aiTeam))
            {
                SectorObjective obj = ResolveAssignedAssaultObjective(unit, activePlan);
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — batedor {unit.InstanceId} cede vez para capturador de {obj.Sector}");
                continue;
            }

            if (action == null)
            {
                Debug.LogWarning($"[AI] Sem decisão para {unit.InstanceId} — marcando como agida.");
                unit.MarkAsActed();
                continue;
            }

            // Registra destino para que unidades subsequentes não colidam
            if (action.HasMoveTo && action.MoveTo != action.MoveFrom)
            {
                Vector3Int dest = action.MoveTo; dest.z = 0;
                plannedDestinations.Add(dest);
            }

            // Recalcula FoW apenas quando algo que altera visibilidade ocorreu:
            // movimento (nova posição = novo cone de visão) ou ataque (inimigo pode
            // ter morrido, liberando LOS para células antes bloqueadas).
            bool unitMoved    = action.HasMoveTo && action.MoveTo != action.MoveFrom;
            bool unitAttacked = !string.IsNullOrEmpty(action.TargetInstanceId);
            JogadasManager.RegistrarPlayerAction(action);
            yield return ExecuteAIBatchWithDebugStep(action);
            if (ShouldStopAIForMatchEnd("phase2_apos_batch"))
                yield break;
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("phase2_apos_pause_batch"))
                yield break;

            if (!IsNoOpUnitAction(action) || unitMoved || unitAttacked)
            {
                bool targetedConstruction = !string.IsNullOrEmpty(action.TargetConstructionId);
                bool refreshFoW = unitMoved || unitAttacked || targetedConstruction;
                CommitAIWorldLightAfterAction(aiTeam, $"phase2:{FormatInitiativeUnitName(unit)}", refreshFoW);
            }

            float delay = GetBatchDelay();
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        }

        Debug.Log($"{TL()} Fase2 concluída.");
    }

    private static bool IsNoOpUnitAction(PlayerAction action)
    {
        if (action == null) return false;
        if (!string.IsNullOrEmpty(action.TargetInstanceId)) return false;
        if (!string.IsNullOrEmpty(action.TargetConstructionId)) return false;
        if (!action.HasMoveTo || !action.HasMoveFrom) return false;

        Vector3Int from = action.MoveFrom; from.z = 0;
        Vector3Int to = action.MoveTo; to.z = 0;
        return from == to;
    }

    private void SyncAIUnitCellsFromTransforms()
    {
        foreach (UnitManager unit in UnitManager.AllActive)
            GetLiveUnitCell(unit, syncState: true);
    }

    private Vector3Int GetLiveUnitCell(UnitManager unit, bool syncState = false)
    {
        if (unit == null)
            return Vector3Int.zero;

        Vector3Int stateCell = unit.CurrentCellPosition;
        stateCell.z = 0;

        if (unit.IsDead || unit.IsEmbarked || !unit.gameObject.activeInHierarchy || unit.BoardTilemap == null)
            return stateCell;

        Vector3Int worldCell = HexCoordinates.WorldToCell(unit.BoardTilemap, unit.transform.position);
        worldCell.z = 0;
        if (worldCell == stateCell)
            return stateCell;

        if (syncState)
        {
            unit.SetCurrentCellPosition(worldCell, enforceFinalOccupancyRule: false);
            Debug.Log($"{TL()} sync cell {unit.InstanceId}: state={stateCell} world={worldCell}");
        }

        return worldCell;
    }

    private bool ShouldDeferIdleAssaultForSectorCapturer(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (unit == null || plan == null) return false;
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Assalto)
            return false;

        SectorObjective assaultObjective = ResolveAssignedAssaultObjective(unit, plan);
        if (assaultObjective == null) return false;

        foreach (SlotNeed slot in assaultObjective.Slots)
        {
            if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
            UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (capturer != null && !capturer.HasActed)
                return true;
        }

        return false;
    }
}
