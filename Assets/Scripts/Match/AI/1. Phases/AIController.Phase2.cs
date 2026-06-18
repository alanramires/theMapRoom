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

            if (!secondPass
                && ShouldDeferCapturerForAirTransportVacate(unit, action, activePlan, aiTeam,
                    out UnitManager airTransporter))
            {
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — capturador {unit.InstanceId} cede vez para heli {airTransporter.InstanceId} sair da producao antes do embarque");
                cursor++;
                yield return null;
                continue;
            }

            if (!secondPass
                && ShouldDeferAttackForFireSupportPrep(unit, action, aiTeam,
                    out UnitManager prepFireSupport, out UnitManager prepTarget, out Vector3Int prepCell))
            {
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — {FormatInitiativeUnitName(unit)} cede ataque em {prepTarget.InstanceId} para artilharia {prepFireSupport.InstanceId} amaciar via {prepCell}");
                cursor++;
                yield return null;
                continue;
            }

            if (!secondPass
                && ShouldDeferAttackForAirCombatPrep(unit, action, aiTeam,
                    out UnitManager prepAirCombat, out UnitManager prepAirTarget, out Vector3Int prepAirCell))
            {
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — {FormatInitiativeUnitName(unit)} cede ataque aereo em {prepAirTarget.InstanceId} para {FormatInitiativeUnitName(prepAirCombat)} atacar primeiro via {prepAirCell}");
                cursor++;
                yield return null;
                continue;
            }

            if (!secondPass
                && (action == null || IsNoOpUnitAction(action))
                && ShouldDeferCapturerForRogueEmbarkBlocker(unit, activePlan, aiTeam,
                    out UnitManager embarkBlocker, out UnitManager blockedTransporter))
            {
                deferredUnitIds.Add(unit.InstanceId);
                Debug.Log($"{TL()} Fase2 — capturador {unit.InstanceId} cede vez para rogue {embarkBlocker.InstanceId} liberar embarque no transporte {blockedTransporter.InstanceId}");
                cursor++;
                yield return null;
                continue;
            }

            if (!secondPass && IsNoOpUnitAction(action) && ShouldDeferIdleAssaultForSectorCapturer(unit, activePlan, aiTeam))
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

        _initLogBuilder.Clear();
        int idleCount = 0;
        foreach (UnitManager u in units)
        {
            if (u == null || u.IsDead || u.IsEmbarked || u.HasActed)
                continue;
            idleCount++;
            Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
            _initLogBuilder.AppendLine($"  {FormatInitiativeUnitName(u)} @ {uc} (HasActed=false)");
        }

        if (idleCount > 0)
            Debug.LogWarning($"{TL()} Fase2 concluída — {idleCount} unidade(s) NÃO agiram:\n{_initLogBuilder}");
        else
            Debug.Log($"{TL()} Fase2 concluída — todas as {units.Count} unidades agiram.");
        _initLogBuilder.Clear();
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

        int firesupportConsidered = 0;
        _initLogBuilder.Clear();
        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == null || candidate == attacker)
                continue;
            if (!IsFireSupportUnit(candidate))
                continue;
            if (candidate.TeamId != aiTeam)
                continue;

            // Diagnóstico: por que esta artilharia não cedeu o amaciamento do alvo do atacante.
            if (candidate.HasActed || candidate.IsDead || candidate.IsEmbarked)
            {
                firesupportConsidered++;
                _initLogBuilder.AppendLine($"  {FormatInitiativeUnitName(candidate)}: indisponivel (acted={candidate.HasActed} dead={candidate.IsDead} embarked={candidate.IsEmbarked})");
                continue;
            }

            firesupportConsidered++;
            if (TryFindFireSupportPrepShot(candidate, target, aiTeam, out Vector3Int candidateCell, out string failReason))
            {
                fireSupport = candidate;
                fireCell = candidateCell;
                _initLogBuilder.Clear();
                return true;
            }

            _initLogBuilder.AppendLine($"  {FormatInitiativeUnitName(candidate)}: {failReason}");
        }

        // Atacante quer bater num alvo válido, existia artilharia no time, mas ninguém cedeu:
        // expõe o motivo (antes era um false silencioso).
        if (firesupportConsidered > 0)
        {
            Debug.Log($"{TL()} Fase2 — {FormatInitiativeUnitName(attacker)} NAO cedeu ataque em " +
                      $"{target.UnitDisplayName}#{target.InstanceId} (nenhum amaciamento de artilharia):\n{_initLogBuilder}");
        }
        _initLogBuilder.Clear();

        return false;
    }

    private bool ShouldDeferAttackForAirCombatPrep(
        UnitManager attacker,
        PlayerAction action,
        TeamId aiTeam,
        out UnitManager airCombat,
        out UnitManager target,
        out Vector3Int attackCell)
    {
        airCombat = null;
        target = null;
        attackCell = Vector3Int.zero;

        if (attacker == null || action == null)
            return false;
        if (action.SensorAction != SensorActionType.Attack || string.IsNullOrEmpty(action.TargetInstanceId))
            return false;
        if (IsAirCombatUnit(attacker))
            return false;
        // Fire support amacia primeiro — nunca cede o tiro de prep a um combatente.
        // Sem esta guarda, uma AA (fire support, terrestre) cede ao air combat enquanto
        // o air combat cede a ela (ShouldDeferAttackForFireSupportPrep), gerando cessão mútua.
        if (IsFireSupportUnit(attacker))
            return false;
        if (!int.TryParse(action.TargetInstanceId, out int targetId))
            return false;

        target = FindAttackPrepTarget(targetId, aiTeam);
        if (target == null || !target.TryGetUnitData(out UnitData targetData) || targetData == null || targetData.domain != Domain.Air)
            return false;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == null || candidate == attacker)
                continue;
            if (candidate.TeamId != aiTeam || candidate.HasActed || candidate.IsDead || candidate.IsEmbarked)
                continue;
            if (!IsAirCombatUnit(candidate))
                continue;
            if (TryFindAirCombatPrepShot(candidate, target, aiTeam, out Vector3Int candidateCell))
            {
                airCombat = candidate;
                attackCell = candidateCell;
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
        out Vector3Int fireCell,
        out string failReason)
    {
        fireCell = Vector3Int.zero;
        failReason = "sem-fire-support";
        if (fireSupport == null || target == null)
            return false;

        Vector3Int fromCell = fireSupport.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(fireSupport);
        HashSet<Vector3Int> occupied = BuildOccupied(fireSupport);
        bool stationary = IsLongRangeStationary(fireSupport);
        TeamObjectivePlan capPlan = ObjectiveManager.GetPlanForTeam(aiTeam);
        WeaponPriorityData weaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;

        bool sensorListedTarget = false;       // o sensor chegou a mirar o alvo de alguma célula?
        string lastDecisionReason = null;       // último motivo de PassesAttackDecision reprovar

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
                sensorListedTarget = true;
                if (!PassesAttackDecision(fireSupport, target, cell, defensiveContext: false, out string decisionReason))
                {
                    lastDecisionReason = decisionReason;
                    continue;
                }

                fireCell = cell;
                failReason = null;
                return true;
            }
        }

        failReason = sensorListedTarget
            ? $"prep-reprovado-PassesAttackDecision ({lastDecisionReason})"
            : "sensor-nao-mira-alvo-de-nenhuma-celula";
        return false;
    }

    private bool TryFindAirCombatPrepShot(
        UnitManager airCombat,
        UnitManager target,
        TeamId aiTeam,
        out Vector3Int attackCell)
    {
        attackCell = Vector3Int.zero;
        if (airCombat == null || target == null)
            return false;

        bool wasGrounded = airCombat.IsAircraftGrounded;
        List<int> takeoffMoveOptions = null;
        if (wasGrounded && !TryGetAITakeoffMoveOptions(airCombat, out takeoffMoveOptions, out _))
            return false;

        if (wasGrounded)
            airCombat.SetAircraftGrounded(false);

        try
        {
            Vector3Int fromCell = airCombat.CurrentCellPosition;
            fromCell.z = 0;
            Dictionary<Vector3Int, List<Vector3Int>> paths =
                UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap, airCombat, Mathf.Max(0, airCombat.RemainingMovementPoints), terrainDatabase);
            HashSet<Vector3Int> occupied = BuildAirOccupied(airCombat);
            if (paths == null || paths.Count == 0)
                return false;

            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (cell != fromCell && occupied != null && occupied.Contains(cell))
                    continue;
                if (!IsAITakeoffDestinationAllowed(paths, cell, takeoffMoveOptions))
                    continue;
                if (!CanAttackTargetFrom(fromCell, cell, airCombat, target))
                    continue;
                if (!TryFindAttackDecisionOption(airCombat, target, cell, out _))
                    continue;

                attackCell = cell;
                return true;
            }
        }
        finally
        {
            if (wasGrounded)
                airCombat.SetAircraftGrounded(true);
        }

        return false;
    }

    private bool ShouldDeferCapturerForAirTransportVacate(
        UnitManager unit,
        PlayerAction action,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        out UnitManager airTransporter)
    {
        airTransporter = null;

        if (unit == null || unit.HasActed || unit.IsDead || unit.IsEmbarked || unit.IsUnderRepair)
            return false;

        if (!unit.TryGetUnitData(out UnitData unitData) || unitData == null
            || unitData.roles == null || !unitData.roles.Contains(UnitRole.Capturador))
            return false;

        if (action != null
            && (!string.IsNullOrEmpty(action.TargetInstanceId)
                || !string.IsNullOrEmpty(action.TargetConstructionId)
                || action.SensorAction == SensorActionType.Attack))
            return false;

        SectorObjective assigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;
        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;

        foreach (UnitManager transporter in UnitManager.AllActive)
        {
            if (transporter == null || transporter == unit)
                continue;
            if (transporter.TeamId != aiTeam || transporter.HasActed || transporter.IsDead
                || transporter.IsEmbarked || transporter.IsUnderRepair)
                continue;
            if (!IsAirTransporter(transporter) || HasTransportCargo(transporter))
                continue;
            if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null
                || !transporterData.isTransporter)
                continue;
            if (FindFittingSlotIndex(transporter, transporterData, unit, unitData) < 0)
                continue;

            Vector3Int transporterCell = transporter.CurrentCellPosition;
            transporterCell.z = 0;
            if (!IsTeamProductionBuilding(transporterCell, aiTeam))
                continue;

            float pickupReach = Mathf.Max(8f,
                unit.RemainingMovementPoints + transporter.RemainingMovementPoints + ShuttlePickupRange + 1f);
            if (SectorManager.HexDistance(unitCell, transporterCell) > pickupReach)
                continue;

            SectorObjective transporterObjective = plan != null
                ? ResolveAssignedTransportObjective(transporter, plan)
                : null;
            if (assigned != null && transporterObjective != null
                && assigned.Sector != transporterObjective.Sector
                && !AreEmbarkSectorsCompatible(assigned.Sector, transporterObjective.Sector))
                continue;
            if (assigned == null && transporterObjective != null
                && !CanRogueUseAssignedTransporter(unit, transporter, transporterObjective, aiTeam))
                continue;

            airTransporter = transporter;
            return true;
        }

        return false;
    }
}
