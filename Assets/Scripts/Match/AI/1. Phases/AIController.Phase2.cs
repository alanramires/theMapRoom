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

        List<UnitManager> units = GetAvailableUnits(aiTeam);
        if (units.Count == 0)
        {
            Debug.Log($"{TL()} Fase 2 — sem unidades em campo, pulando.");
            if (matchController == null || !matchController.IsPlayerCommandServiceAutomatic(snapshot.AITeam))
                JogadasManager.EnsureInstance()?.RegistrarServicoComando(snapshot.TurnNumber, (int)aiTeam);
            yield break;
        }

        Debug.Log($"{TL()} Fase2 — iniciando ações.");
        plannedDestinations.Clear();

        // ---- Setup: executado uma única vez por fase ----
        SyncAIUnitCellsFromTransforms();
        AIWorldSnapshot current = AIWorldSnapshot.BuildLight(aiTeam, matchController);
        TeamObjectivePlan activePlan = ObjectiveManager.GetPlanForTeam(aiTeam);
        InvalidateStaleThreatObjectives(activePlan, aiTeam);

        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId == aiTeam && !u.IsDead)
                UpdateRepairState(u, activePlan);
        }

        _groupCache.Clear();
        foreach (UnitManager u in units)
            _groupCache[u.InstanceId] = GetInitiativeGroup(u, activePlan, aiTeam);

        _sortAiTeam = aiTeam;
        _sortActivePlan = activePlan;
        units.Sort(_initiativeComparison);

        _initLogBuilder.Clear();
        _initLogBuilder.AppendLine($"{TL()} Fase2 iniciativa ({units.Count} unidades):");
        foreach (UnitManager u in units)
        {
            int g = _groupCache[u.InstanceId];
            Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
            Vector3Int? tgt = GetAssignedTargetCell(u, activePlan);
            string tgtStr = tgt.HasValue ? tgt.Value.ToString() : "null";
            _initLogBuilder.AppendLine($"  [grp={g}] {FormatInitiativeUnitName(u)} @ {uc} target={tgtStr}");
        }
        Debug.Log(_initLogBuilder.ToString());
        _initLogBuilder.Clear();

        // ---- Loop por unidade: decisão + execução ----
        var deferredUnitIds = new HashSet<int>();
        int cursor = 0;
        bool secondPass = false;

        while (isActive && !IsMatchEnded())
        {
            yield return WaitIfDebugPaused();
            if (ShouldStopAIForMatchEnd("phase2_loop_apos_pause"))
                yield break;

            // Avança o cursor sobre unidades já agidas, mortas ou deferidas (1ª passagem)
            while (cursor < units.Count &&
                   (units[cursor] == null || units[cursor].IsDead || units[cursor].HasActed ||
                    (!secondPass && deferredUnitIds.Contains(units[cursor].InstanceId))))
            {
                cursor++;
            }

            if (cursor >= units.Count)
            {
                if (!secondPass && deferredUnitIds.Count > 0)
                {
                    // Todas as unidades não-deferidas agiram; processa as deferidas
                    deferredUnitIds.Clear();
                    cursor = 0;
                    secondPass = true;
                    continue;
                }
                break;
            }

            UnitManager unit = units[cursor];
            PlayerAction action = DecideUnitAction(unit, current);

            if (ShouldDeferAttackForFireSupportPrep(unit, action, aiTeam,
                    out UnitManager prepFireSupport, out UnitManager prepTarget, out Vector3Int prepCell))
            {
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — {FormatInitiativeUnitName(unit)} cede ataque em {prepTarget.InstanceId} para artilharia {prepFireSupport.InstanceId} amaciar via {prepCell}");
                cursor++;
                yield return null;
                continue;
            }

            if ((action == null || IsNoOpUnitAction(action))
                && ShouldDeferCapturerForRogueEmbarkBlocker(unit, activePlan, aiTeam,
                    out UnitManager embarkBlocker, out UnitManager blockedTransporter))
            {
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — capturador {unit.InstanceId} cede vez para rogue {embarkBlocker.InstanceId} liberar embarque no transporte {blockedTransporter.InstanceId}");
                cursor++;
                yield return null;
                continue;
            }

            if (IsNoOpUnitAction(action) && ShouldDeferIdleAssaultForSectorCapturer(unit, activePlan, aiTeam))
            {
                SectorObjective obj = ResolveAssignedAssaultObjective(unit, activePlan);
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — batedor {unit.InstanceId} cede vez para capturador de {obj.Sector}");
                cursor++;
                yield return null;
                continue;
            }

            if (action == null)
            {
                Debug.LogWarning($"[AI] Sem decisão para {unit.InstanceId} — marcando como agida.");
                unit.MarkAsActed();
                cursor++;
                continue;
            }

            if (action.HasMoveTo && action.MoveTo != action.MoveFrom)
            {
                Vector3Int dest = action.MoveTo; dest.z = 0;
                plannedDestinations.Add(dest);
            }

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

            // Reconstrói o snapshot para a próxima decisão (hexes ocupados mudam após cada ação)
            current = AIWorldSnapshot.BuildLight(aiTeam, matchController);

            float delay = GetBatchDelay();
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            cursor++;
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

    private int CompareFireSupportAttackInitiative(UnitManager a, UnitManager b, TeamId aiTeam)
    {
        if (!IsFireSupportUnit(a) || !IsFireSupportUnit(b))
            return 0;

        bool hasA = TryGetFireSupportCurrentAttackInitiative(a, aiTeam,
            out bool primaryA, out int eliteA, out BazookaTargetPriority prefA);
        bool hasB = TryGetFireSupportCurrentAttackInitiative(b, aiTeam,
            out bool primaryB, out int eliteB, out BazookaTargetPriority prefB);

        if (hasA != hasB)
            return hasA ? -1 : 1;
        if (!hasA)
            return 0;

        if (primaryA != primaryB)
            return primaryA ? -1 : 1;

        if (!primaryA)
        {
            int eliteCmp = eliteB.CompareTo(eliteA);
            if (eliteCmp != 0)
                return eliteCmp;
        }

        int prefCmp = prefB.CompareTo(prefA);
        return prefCmp != 0 ? prefCmp : 0;
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

    private bool ShouldDeferAttackForFireSupportPrep(
        UnitManager attacker,
        PlayerAction action,
        TeamId aiTeam,
        out UnitManager fireSupport,
        out UnitManager target,
        out Vector3Int fireCell)
    {
        fireSupport = null;
        target = null;
        fireCell = Vector3Int.zero;

        if (attacker == null || action == null)
            return false;
        if (action.SensorAction != SensorActionType.Attack || string.IsNullOrEmpty(action.TargetInstanceId))
            return false;
        if (IsFireSupportUnit(attacker))
            return false;
        if (!int.TryParse(action.TargetInstanceId, out int targetId))
            return false;

        target = FindAttackPrepTarget(targetId, aiTeam);
        if (target == null)
            return false;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == null || candidate == attacker)
                continue;
            if (candidate.TeamId != aiTeam || candidate.HasActed || candidate.IsDead || candidate.IsEmbarked)
                continue;
            if (!IsFireSupportUnit(candidate))
                continue;
            if (TryFindFireSupportPrepShot(candidate, target, aiTeam, out Vector3Int candidateCell))
            {
                fireSupport = candidate;
                fireCell = candidateCell;
                return true;
            }
        }

        return false;
    }

    private UnitManager FindAttackPrepTarget(int targetId, TeamId aiTeam)
    {
        MatchController mc = GetMatchController();
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.InstanceId != targetId)
                continue;
            if (unit.TeamId == aiTeam || unit.IsDead || unit.IsEmbarked)
                return null;
            if (mc != null && !mc.IsUnitVisibleForTeam(unit, aiTeam))
                return null;
            return unit;
        }

        return null;
    }

    private bool TryFindFireSupportPrepShot(
        UnitManager fireSupport,
        UnitManager target,
        TeamId aiTeam,
        out Vector3Int fireCell)
    {
        fireCell = Vector3Int.zero;
        if (fireSupport == null || target == null)
            return false;

        Vector3Int fromCell = fireSupport.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(fireSupport);
        HashSet<Vector3Int> occupied = BuildOccupied(fireSupport);
        bool stationary = IsLongRangeStationary(fireSupport);
        TeamObjectivePlan capPlan = ObjectiveManager.GetPlanForTeam(aiTeam);
        WeaponPriorityData weaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;

        foreach (Vector3Int rawCell in EnumerateFireSupportCandidateCells(fromCell, paths, stationary))
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;
            if (cell != fromCell && IsCellACapturerTarget(cell, capPlan, aiTeam))
                continue;

            SensorMovementMode mode = cell != fromCell
                ? SensorMovementMode.MoveuAndando
                : SensorMovementMode.MoveuParado;

            var targets = new List<PodeMirarTargetOption>();
            if (!PodeMirarSensor.CollectTargets(
                    fireSupport,
                    boardTilemap,
                    terrainDatabase,
                    mode,
                    targets,
                    weaponPriorityData: weaponPriorityData,
                    dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
                    fromCell: cell))
                continue;

            foreach (PodeMirarTargetOption opt in targets)
            {
                if (opt == null || opt.targetUnit != target)
                    continue;
                if (!PassesAttackDecision(fireSupport, target, cell, defensiveContext: false, out _))
                    continue;

                fireCell = cell;
                return true;
            }
        }

        return false;
    }
}
