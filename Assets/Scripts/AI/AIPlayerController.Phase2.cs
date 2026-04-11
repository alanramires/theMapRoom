using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class AIPlayerController
{
    private IEnumerator WaitIfPlayerMenuOpen()
    {
        while (turnStateManager != null &&
               (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.PlayerMenu
                || turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Replay))
            yield return null;
    }

    private IEnumerator Phase2_MoveUnit(TeamId aiTeam, AISnapshot snapshot, UnitManager unit, UnitManager assignedEnemy)
    {
        if (turnStateManager == null || unit == null || unit.IsDead || unit.HasActed)
            yield break;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        float selectDelay = turnStateManager != null ? turnStateManager.GetAutomatedConfirmDelay() : (AnimationManager.Instance != null ? AnimationManager.Instance.AIUnitSelectDelay : 0.12f);

        yield return StartCoroutine(turnStateManager.MoveCursorToCellWithAutomatedTravel(unitCell));
        if (unit == null)
            yield break;
        turnStateManager.HandleConfirmWithFeedback();
        if (turnStateManager.SelectedUnit != unit || turnStateManager.CurrentCursorState != TurnStateManager.CursorState.UnitSelected)
        {
            if (aiLog) Debug.Log($"{T(aiTeam, 2)} falha ao selecionar unidade {unit.name} via fluxo replay; encerrando unidade");
            turnStateManager.HandleCancel();
            yield break;
        }
        if (selectDelay > 0f)
            yield return new WaitForSeconds(selectDelay);

        turnStateManager.MarkCurrentActionAsAIGenerated();

        bool intelCanFire = turnStateManager.CanUnitFireAtAnyTargetFromCurrentPosition(unit, out UnitManager intelFireTarget);

        bool engagingEnemy = false;
        bool repositioningForDefense = false;
        Vector3Int moveTarget = unitCell;
        UnitManager artilleryRepositionAnchorEnemy = null;
        BattleStanceData activeStanceData = battleStanceDatabase != null ? battleStanceDatabase.GetStanceData(currentStance) : null;

        unit.TryGetUnitData(out UnitData unitData);
        AIUnitProfile aiProfile = unitData != null ? unitData.aiUnitProfile : null;
        AIUnitStanceBehavior stanceBehavior = aiProfile != null
            ? aiProfile.GetStanceBehavior(currentStance)
            : new AIUnitStanceBehavior();
        bool defendMode = stanceBehavior.playConservative;
        UnitCombatClassification combatClassification = unit != null ? unit.CombatClassification : UnitCombatClassification.Civil;
        bool captureObjectiveActive = false;
        bool captureActionNow = false;
        Vector3Int captureObjectiveCell = unitCell;
        string captureObjectiveLabel = string.Empty;
        bool captureObjectiveIsEnemyTerritory = false;
        // Le o papel planejado para esta unidade neste turno (pode ser null)
        snapshot.UnitRoles.TryGetValue(unit.InstanceId, out AIPlanIntent unitIntent);
        snapshot.UnitPlanAssignments.TryGetValue(unit.InstanceId, out AIPlanAssignment unitAssignment);
        Vector3Int? plannedCaptureCell = null;
        if (unitAssignment != null && unitAssignment.HasPlannedCaptureTarget)
            plannedCaptureCell = unitAssignment.PlannedCaptureCell;
        else if (unitIntent != null && unitIntent.HasCaptureTarget)
            plannedCaptureCell = unitIntent.CaptureTargetCell;
        ConstructionSector? plannedCaptureSector = null;
        if (unitIntent != null && !ConstructionSectorHelper.IsBase(unitIntent.Sector))
            plannedCaptureSector = unitIntent.Sector;
        if (unitIntent?.SectorEnemy != null && !unitIntent.SectorEnemy.IsDead && assignedEnemy == null)
            assignedEnemy = unitIntent.SectorEnemy;
        bool captureUsedPlanner = false;
        string planAllocationLabel = "sem plano";
        if (unitIntent != null)
        {
            string planName = !string.IsNullOrWhiteSpace(unitIntent.DisplayName)
                ? unitIntent.DisplayName
                : unitIntent.Sector.ToString();
            string planRole = unitAssignment != null
                ? unitAssignment.Role.ToDebugLabel()
                : "sem papel";
            planAllocationLabel = $"{planName} [{planRole}]";
        }
        bool planCohesionActive = false;
        Vector3Int planCohesionCell = unitCell;
        string planCohesionLabel = string.Empty;
        bool captureRoleFilter = unitAssignment != null
            && unitAssignment.Role == AIPlanRole.Capture
            && unitAssignment.Intent != null
            && unitAssignment.Intent.HasCaptureTarget;
        bool protectCaptureDiscipline = captureRoleFilter && IsProtectIntent(unitIntent);

        // SectorEnemy pode ter ressuscitado assignedEnemy que AssignTargetForUnit zerou via captureInterruptBias.
        // Reaplicar o mesmo gate aqui para garantir coerÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âªncia.
        if (captureRoleFilter && assignedEnemy != null && stanceBehavior.captureInterruptBias == CaptureInterruptBias.None)
        {
            Vector3Int enemyCell = assignedEnemy.CurrentCellPosition; enemyCell.z = 0;
            Vector3Int objCell   = unitAssignment.Intent.CaptureTargetCell; objCell.z = 0;
            if (enemyCell != objCell)
                assignedEnemy = null;
        }

        if (unitIntent != null && unitAssignment != null && unitAssignment.Role != AIPlanRole.Capture)
        {
            planCohesionActive = TryGetPlanCohesionObjective(unit, snapshot, unitIntent, unitAssignment, out planCohesionCell, out planCohesionLabel);
        }


        bool repairModeActive = ShouldKeepUnitInRepairMode(unit);
        unit.SetAIMaintenanceActive(repairModeActive);
        bool mergeObjectiveActive = false;
        bool mergeActionNow = false;
        UnitManager mergeTargetUnit = null;
        Vector3Int mergeObjectiveCell = unitCell;
        Vector3Int mergeApproachCell = unitCell;
        string mergeObjectiveLabel = string.Empty;
        bool repairActionNow = false;
        Vector3Int repairObjectiveCell = unitCell;
        string repairObjectiveLabel = string.Empty;
        bool repairDislodgeActive = false;
        UnitManager repairDislodgeTarget = null;
        string repairDislodgeReason = string.Empty;

        bool isSupplierUnit = unitData != null && unitData.isSupplier;
        bool supplyObjectiveActive = false;
        bool supplyRefillMode = false;
        bool supplyActionNow = false;
        UnitManager supplyTargetUnit = null;
        ConstructionManager supplyReceiveConstruction = null;
        Vector3Int supplyObjectiveCell = unitCell;
        string supplyObjectiveLabel = string.Empty;
        bool supplierParkingRequested = false;
        bool supplierPlanCohesionRequested = false;
        bool transportIdlePickupRequested = false;
        bool transportPickupObjectiveActive = false;
        bool transportCarryObjectiveActive = false;
        bool transportRendezvousObjectiveActive = false;
        bool transportEmbarkNow = false;
        bool transportDisembarkNow = false;
        UnitManager transportTargetTransporter = null;
        UnitManager transportPassengerUnit = null;
        Vector3Int transportObjectiveCell = unitCell;
        Vector3Int transportPassengerTargetCell = unitCell;
        string transportObjectiveLabel = string.Empty;
        bool isTransporterUnit = unitData != null && unitData.isTransporter;
        bool hasTransportBehavior = HasTransportSensor(unitData != null ? unitData.aiUnitProfile : null);
        bool returnTransporterToPickupAfterDisembark = unitData != null
            && unitData.aiUnitProfile != null
            && unitData.aiUnitProfile.returnToPickupAfterDisembark;

        bool foundDestination = false;
        Vector3Int bestDest = unitCell;
        HashSet<Vector3Int> occupiedByAllies = BuildAllyCellSet(snapshot, unit);
        bool supportSafetyMode = stanceBehavior.requireSightlineBeforeEngaging
            || stanceBehavior.holdPositionWhenInRange
            || stanceBehavior.repositionToFireRange
            || stanceBehavior.holdGroundWhenIdle
            || defendMode;
        HashSet<Vector3Int> preferredSupportCells = supportSafetyMode
            ? BuildFriendlySupportPreferenceCells(snapshot, unit, supportOnlyCombatAnchors: !isSupplierUnit)
            : null;
        HashSet<Vector3Int> dangerPenaltyCells = (supportSafetyMode || isTransporterUnit)
            ? BuildEnemyDangerCells(snapshot, isSupplierUnit ? 2 : 1)
            : null;

        // Refill forcado: sem estoque ou autonomia propria baixa, precisa reabastecer antes de qualquer acao (analogo ao repairMode).
        if (isSupplierUnit && (IsSupplyTruckOutOfReserves(unit, aiProfile) || IsLowAutonomy(unit, aiProfile)))
        {
            supplyObjectiveActive = true;
            supplyRefillMode = true;
            if (TryGetNearestOwnedConstruction(unit, snapshot, out supplyReceiveConstruction, out supplyObjectiveCell, out supplyObjectiveLabel))
                supplyActionNow = supplyObjectiveCell == unitCell;
            else
            {
                supplyObjectiveCell = unitCell;
                supplyActionNow = true;
                supplyObjectiveLabel = "sem construcao propria";
            }
        }

        if (!supplyObjectiveActive
            && repairModeActive
            && (aiProfile == null || aiProfile.fuseWhileOnRepairMode)
            && TryResolveMergeObjectiveForRepairingUnit(unit, snapshot, out mergeTargetUnit, out mergeObjectiveCell, out mergeApproachCell, out mergeActionNow, out mergeObjectiveLabel))
        {
            mergeObjectiveActive = true;
            // Fusao aqui e um atalho de reparo; a unidade continua em maintenance mode se a fusao nao acontecer.
        }

        UnitManager targetEnemy = null;
        if (!supplyObjectiveActive && repairModeActive)
        {
            if (TryGetNearestOwnedConstruction(unit, snapshot, out _, out repairObjectiveCell, out repairObjectiveLabel))
            {
                repairActionNow = repairObjectiveCell == unitCell;
            }
            else
            {
                if (TryGetBestRepairDislodgeTarget(unit, snapshot, occupiedByAllies, unitCell, out repairDislodgeTarget, out repairDislodgeReason))
                {
                    repairDislodgeActive = true;
                    repairObjectiveCell = unitCell;
                    repairActionNow = false;
                    repairObjectiveLabel = $"desocupar para reparar ({repairDislodgeTarget.name})";
                }
                else if (TryGetRepairFallbackCell(unit, snapshot, out repairObjectiveCell, out string fallbackLabel))
                {
                    repairActionNow = repairObjectiveCell == unitCell;
                    repairObjectiveLabel = $"reparo: construcao bloqueada por inimigo, escolhendo alternativa ({fallbackLabel})";
                }
                else
                {
                    repairObjectiveCell = unitCell;
                    repairActionNow = true;
                    repairObjectiveLabel = "reparo: sem construcao livre, mantendo sobrevivencia";
                }
            }

            if (repairDislodgeActive && repairDislodgeTarget != null && !repairDislodgeTarget.IsDead)
            {
                targetEnemy = repairDislodgeTarget;
                engagingEnemy = true;
            }
        }
        else if (!supplyObjectiveActive)
        {
            // Itera sensorPriority e executa o primeiro sensor cujas condicoes sao atendidas.
            // Supply e Repair sao pre-filtros de papel ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â nao entram no loop.
            // Usa a lista da stance atual quando disponivel; fallback para a lista global do perfil.
            IReadOnlyList<AIUnitSensorKind> priority = stanceBehavior.sensorPriority;
            int priorityCount = priority != null ? priority.Count : 0;

            for (int s = 0; s < priorityCount; s++)
            {
                AIUnitSensorKind sensor = priority[s];

                if (sensor == AIUnitSensorKind.Transport)
                {
                    if (unitData != null && unitData.isTransporter)
                    {
                        if (TryGetTransportCarryObjective(unit, snapshot, out transportPassengerUnit, out transportPassengerTargetCell, out transportObjectiveCell, out transportObjectiveLabel, out transportDisembarkNow))
                        {
                            transportCarryObjectiveActive = true;
                            break;
                        }
                        if (TryGetTransportRendezvousObjective(unit, snapshot, out transportObjectiveCell, out transportObjectiveLabel))
                        {
                            transportRendezvousObjectiveActive = true;
                            break;
                        }
                        if (TryGetTransportRoguePickupObjective(unit, snapshot, out transportObjectiveCell, out transportObjectiveLabel))
                        {
                            transportRendezvousObjectiveActive = true;
                            break;
                        }
                    }
                    else if (TryGetTransportPickupObjective(unit, snapshot, unitIntent, unitAssignment, plannedCaptureCell, out transportTargetTransporter, out transportObjectiveCell, out transportObjectiveLabel, out transportEmbarkNow))
                    {
                        transportPickupObjectiveActive = true;
                        break;
                    }
                    else if (TryGetTransportPickupObjectiveForRogue(unit, snapshot, out transportTargetTransporter, out transportObjectiveCell, out transportObjectiveLabel, out transportEmbarkNow))
                    {
                        transportPickupObjectiveActive = true;
                        break;
                    }
                }
                else if (sensor == AIUnitSensorKind.Capture)
                {
                    if (turnStateManager.CanUnitCaptureFromCurrentPosition(
                        unit,
                        out ConstructionManager captureNowConstruction,
                        out _,
                        out _)
                        && captureNowConstruction != null)
                    {
                        captureObjectiveActive = true;
                        captureActionNow = true;
                        captureObjectiveCell = unitCell;
                        captureObjectiveLabel = captureNowConstruction.ConstructionDisplayName;
                        break;
                    }
                    if (TryGetBestCaptureObjectiveForInfantry(unit, snapshot, out _, out captureObjectiveCell, out captureObjectiveLabel, out captureObjectiveIsEnemyTerritory, plannedCaptureCell, plannedCaptureSector))
                    {
                        captureObjectiveActive = true;
                        captureUsedPlanner = plannedCaptureCell.HasValue && captureObjectiveCell == plannedCaptureCell.Value;
                        break;
                    }
                    // Sensor Capture falhou (sem alvo alcancavel) ? tenta proximo.
                }
                else if (sensor == AIUnitSensorKind.Attack)
                {
                    // Capturador com captureRoleFilter: nao faz fallback para FindClosestVisibleEnemy.
                    // Se assignedEnemy foi descartado pelo captureInterruptBias em AssignTargetForUnit,
                    // o sensor Attack deve falhar aqui tambem ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â a unidade segue para o proximo sensor (Reposition).
                    UnitManager candidate = assignedEnemy != null && !assignedEnemy.IsDead
                        ? assignedEnemy
                        : (captureRoleFilter ? null : FindClosestVisibleEnemy(unit, snapshot, restrictToDefenseRadius: false));

                    if (candidate != null && matchController != null && !IsEnemyVisibleForAiTeam(candidate, aiTeam))
                    {
                        if (aiLog)
                            Debug.Log($"{T(aiTeam, 2)} [intel] alvo atribuido ficou invisivel no FoW atual: {candidate.name}. Recalculando alvo visivel.");
                        candidate = FindClosestVisibleEnemy(unit, snapshot, restrictToDefenseRadius: false);
                    }

                    if (stanceBehavior.requireSightlineBeforeEngaging)
                    {
                        if (candidate == null || !CanUnitFireAtTargetFromCurrentPosition(unit, candidate))
                        {
                            if (candidate != null && stanceBehavior.repositionToFireRange)
                                artilleryRepositionAnchorEnemy = candidate;

                            if (aiLog)
                            {
                                string anchorLabel = candidate != null ? candidate.name : "sem ancora";
                                Debug.Log($"{T(aiTeam, 2)} [intel] {unit.name} exige linha de tiro e nao tem tiro parado valido; Attack falhou e vai para o proximo sensor. ancora={anchorLabel}");
                            }
                            candidate = null;
                        }
                    }
                    else if (stanceBehavior.repositionToFireRange && candidate != null)
                    {
                        // Marca ancora agora. Se ao final nao engajar (sem tiro parado e sem alcance),
                        // o bloco de reposicionamento usa este anchor para se aproximar do alvo.
                        artilleryRepositionAnchorEnemy = candidate;
                    }

                    if (candidate != null)
                    {
                        targetEnemy = candidate;
                        engagingEnemy = true;
                        break;
                    }
                    // Sensor Attack falhou (sem inimigo engajavel agora) ? tenta proximo.
                }
                else if (sensor == AIUnitSensorKind.Supply && isSupplierUnit)
                {
                    // Adjacente com necessidade imediata: supre agora sem mover.
                    if (TryGetImmediateSupplyTargetsNow(unit, out List<UnitManager> immediateSupplyTargets) && immediateSupplyTargets.Count > 0)
                    {
                        supplyObjectiveActive = true;
                        supplyRefillMode = false;
                        supplyActionNow = true;
                        supplyObjectiveCell = unitCell;
                        supplyTargetUnit = immediateSupplyTargets[0];
                        supplyObjectiveLabel = supplyTargetUnit.name;
                        break;
                    }
                    // Alvo nao-adjacente: navega ate ele.
                    if (TryCollectSupplyNavigationCandidates(unit, snapshot, out List<UnitManager> navSupplyTargets) && navSupplyTargets.Count > 0)
                    {
                        for (int navIndex = 0; navIndex < navSupplyTargets.Count; navIndex++)
                        {
                            UnitManager navSupplyTarget = navSupplyTargets[navIndex];
                            if (navSupplyTarget == null)
                                continue;

                            if (!TryResolveSupplyObjectiveCell(unit, navSupplyTarget, snapshot, occupiedByAllies, preferredSupportCells, dangerPenaltyCells, out supplyObjectiveCell, out supplyActionNow))
                                continue;

                            supplyObjectiveActive = true;
                            supplyRefillMode = false;
                            supplyTargetUnit = navSupplyTarget;
                            supplyObjectiveLabel = navSupplyTarget.name;

                            if (aiLog && navIndex > 0)
                                Debug.Log($"{T(aiTeam, 2)} [support] {unit.name} pulou alvo de supply mais critico/bloqueado e caiu para fallback util: {navSupplyTarget.name}.");
                            break;
                        }

                        if (supplyObjectiveActive)
                            break;
                    }
                    // Sensor Supply falhou (sem aliado que precise) ? tenta proximo.
                }
                else if (sensor == AIUnitSensorKind.Reposition)
                {
                    break; // Reposicionar e o fallback ? sempre aceito.
                }
            }
        }


        bool escortRoleActive = unitAssignment != null && IsEscortMissionRole(unitAssignment.Role);
        HashSet<Vector3Int> penalizedCaptureCells = escortRoleActive && !repairModeActive && !mergeObjectiveActive && !captureObjectiveActive
            ? BuildReservedCaptureCellsForEscort(unit, unitIntent)
            : null;
        HashSet<Vector3Int> penalizedMovementCells = MergeCellSets(penalizedCaptureCells, dangerPenaltyCells);
        if (!supplyObjectiveActive
            && !repairModeActive
            && escortRoleActive
            && planCohesionActive
            && targetEnemy != null
            && !stanceBehavior.engageNearestEnemies
            && targetEnemy != assignedEnemy  // scoring ja validou assignedEnemy com contexto de escort; confiar no resultado.
            && !IsEscortThreatRelevant(unit, targetEnemy, snapshot, unitIntent, planCohesionCell))
        {
            if (aiLog)
                Debug.Log($"{T(aiTeam, 2)} [intel] escolta ignorou alvo fora do contexto do plano: {targetEnemy.name} | mantendo coesao em {planCohesionLabel}");
            targetEnemy = null;
            engagingEnemy = false;
        }

        if (!supplyObjectiveActive
            && !repairModeActive
            && !mergeObjectiveActive
            && !captureObjectiveActive
            && stanceBehavior.requireSightlineBeforeEngaging
            && targetEnemy != null
            && !CanUnitFireAtTargetFromCurrentPosition(unit, targetEnemy))
        {
            if (aiLog)
                Debug.Log($"{T(aiTeam, 2)} [intel] {unit.name} descartou alvo de engajamento sem tiro parado valido ({targetEnemy.name}); retomando coesao/reposicionamento.");

            targetEnemy = null;
            engagingEnemy = false;
        }

        if (supplyObjectiveActive)
        {
            moveTarget = supplyObjectiveCell;
            moveTarget.z = 0;
        }
        else if (isSupplierUnit && (defendMode || stanceBehavior.retreatToHqWhenIdle))
        {
            repositioningForDefense = true;
            if (planCohesionActive)
            {
                supplierPlanCohesionRequested = true;
                moveTarget = planCohesionCell;
                moveTarget.z = 0;
            }
            else if (TryGetSupplierIdleParkingCell(unit, snapshot, occupiedByAllies, out Vector3Int supplierParkingCell))
            {
                supplierParkingRequested = true;
                moveTarget = supplierParkingCell;
                moveTarget.z = 0;
            }
            else if (defendMode && TryGetSupplyTruckDefensivePatrolCell(unit, snapshot, out Vector3Int stDefensiveCell))
            {
                moveTarget = stDefensiveCell;
                moveTarget.z = 0;
            }
            else if (snapshot.HasHq)
            {
                moveTarget = snapshot.HqCell;
                moveTarget.z = 0;
            }
            else
            {
                moveTarget = unitCell;
            }
        }
        else if (isSupplierUnit)
        {
            // Supply truck sem objetivo de supply e sem postura conservativa:
            // move-se em direcao a unidade amiga mais proxima para ficar em posicao de apoio.
            // Evita o fallback generico de avancar rumo ao HQ inimigo.
            repositioningForDefense = true;
            Vector3Int anchor = snapshot.HasHq ? snapshot.HqCell : unitCell;
            if (snapshot.FriendlyUnits != null)
            {
                int bestDist = int.MaxValue;
                for (int fi = 0; fi < snapshot.FriendlyUnits.Count; fi++)
                {
                    UnitManager ally = snapshot.FriendlyUnits[fi];
                    if (ally == null || ally == unit || ally.IsDead) continue;
                    Vector3Int allyCell2 = ally.CurrentCellPosition;
                    allyCell2.z = 0;
                    int d = GetHexDistance(snapshot.BoardTilemap, unitCell, allyCell2, 64);
                    if (d < bestDist) { bestDist = d; anchor = allyCell2; }
                }
            }
            moveTarget = anchor;
            moveTarget.z = 0;
        }
        else if (isTransporterUnit
            && hasTransportBehavior
            && !HasAnyTransportedPassenger(unit)
            && !transportCarryObjectiveActive
            && !transportRendezvousObjectiveActive
            && !mergeObjectiveActive
            && !repairModeActive)
        {
            repositioningForDefense = true;

            if (!defendMode && TryGetTransportForwardStagingCell(unit, snapshot, out Vector3Int transportForwardCell))
            {
                // Modo agressivo: avanca em direcao ao objetivo de captura mais relevante.
                moveTarget = transportForwardCell;
                moveTarget.z = 0;
            }
            else if (returnTransporterToPickupAfterDisembark
                && TryGetTransportIdlePickupCell(unit, snapshot, occupiedByAllies, out Vector3Int transportParkingCell))
            {
                // Modo conservador: aguarda na zona de embarque proxima a base.
                transportIdlePickupRequested = true;
                moveTarget = transportParkingCell;
                moveTarget.z = 0;
            }
            else if (returnTransporterToPickupAfterDisembark && snapshot.HasHq)
            {
                moveTarget = snapshot.HqCell;
                moveTarget.z = 0;
            }
            else
            {
                moveTarget = unitCell;
            }

            if (moveTarget == unitCell)
            {
                foundDestination = true;
                bestDest = unitCell;
            }
        }
        else if (mergeObjectiveActive)
        {
            moveTarget = mergeActionNow ? unitCell : mergeApproachCell;
            moveTarget.z = 0;
        }
        else if (repairModeActive)
        {
            if (repairDislodgeActive)
            {
                moveTarget = unitCell;
            }
            else
            {
                moveTarget = repairObjectiveCell;
                moveTarget.z = 0;
            }
        }
        else if (captureObjectiveActive)
        {
            moveTarget = captureObjectiveCell;
            moveTarget.z = 0;
        }
        else if (targetEnemy != null)
        {
            engagingEnemy = true;
            moveTarget = targetEnemy.CurrentCellPosition;
            moveTarget.z = 0;
        }
        else if ((unitIntent == null || unitAssignment == null)
            && TryGetBackupPlanObjectiveForRogue(unit, snapshot, out Vector3Int backupPlanCell, out string backupPlanLabel))
        {
            repositioningForDefense = true;
            moveTarget = backupPlanCell;
            moveTarget.z = 0;
            if (aiLog)
                Debug.Log($"{T(aiTeam, 2)} [rogue] {unit.name} priorizou backup plan em vez de HQ: {backupPlanLabel} @ L{moveTarget.x}, C{moveTarget.y}");
        }
        else if ((stanceBehavior.retreatToHqWhenIdle || defendMode) && snapshot.HasHq)
        {
            repositioningForDefense = true;
            moveTarget = snapshot.HqCell;
            moveTarget.z = 0;
        }
        else if (planCohesionActive)
        {
            moveTarget = planCohesionCell;
            moveTarget.z = 0;
        }
        else if (stanceBehavior.holdGroundWhenIdle)
        {
            // Ancora na posicao atual. Com prioritizeDpqDuringTravel, o path planning
            // gravita para a melhor celula DPQ proxima (predio, cobertura) e fica la.
            repositioningForDefense = true;
            moveTarget = unitCell;
            foundDestination = true;
            bestDest = unitCell;
        }
        else if (currentStance == AIStance.Invasion
            && (unitIntent == null || unitAssignment == null)
            && snapshot.EnemyHqs != null && snapshot.EnemyHqs.Count > 0)
        {
            Vector3Int nearestEnemyHq = snapshot.EnemyHqs[0].Cell;
            nearestEnemyHq.z = 0;
            int bestDistToHq = GetHexDistance(snapshot.BoardTilemap, unitCell, nearestEnemyHq, 64);
            for (int i = 1; i < snapshot.EnemyHqs.Count; i++)
            {
                Vector3Int hqCell = snapshot.EnemyHqs[i].Cell;
                hqCell.z = 0;
                int d = GetHexDistance(snapshot.BoardTilemap, unitCell, hqCell, 64);
                if (d < bestDistToHq)
                {
                    bestDistToHq = d;
                    nearestEnemyHq = hqCell;
                }
            }
            moveTarget = nearestEnemyHq;
        }
        else if (snapshot.EnemyHqs != null && snapshot.EnemyHqs.Count > 0)
        {
            Vector3Int nearestEnemyHq = snapshot.EnemyHqs[0].Cell;
            nearestEnemyHq.z = 0;
            int bestDistToHq = GetHexDistance(snapshot.BoardTilemap, unitCell, nearestEnemyHq, 64);
            for (int i = 1; i < snapshot.EnemyHqs.Count; i++)
            {
                Vector3Int hqCell = snapshot.EnemyHqs[i].Cell;
                hqCell.z = 0;
                int d = GetHexDistance(snapshot.BoardTilemap, unitCell, hqCell, 64);
                if (d < bestDistToHq)
                {
                    bestDistToHq = d;
                    nearestEnemyHq = hqCell;
                }
            }
            moveTarget = nearestEnemyHq;
        }
        else
        {
            moveTarget = unitCell;
        }

        if (transportPickupObjectiveActive && transportEmbarkNow)
        {
            foundDestination = true;
            bestDest = unitCell;
            moveTarget = unitCell;
        }

        if (supplyObjectiveActive)
        {
            if (supplyActionNow || moveTarget == unitCell)
            {
                foundDestination = true;
                bestDest = unitCell;
            }
            else
            {
                foundDestination = turnStateManager.TryGetBestReachableCellTowardsHexDistance(
                    snapshot.BoardTilemap,
                    moveTarget,
                    occupiedByAllies,
                    out bestDest,
                    prioritizeDpq: false,
                    unit: unit,
                    preferLongerAdvanceOnTie: false,
                    preferShorterAdvanceOnTie: true,
                    penalizedCells: penalizedMovementCells,
                preferredCells: preferredSupportCells);
            }
        }
        else if (mergeObjectiveActive)
        {
            if (mergeActionNow || moveTarget == unitCell)
            {
                foundDestination = true;
                bestDest = unitCell;
            }
            else
            {
                foundDestination = true;
                bestDest = moveTarget;
            }
        }
        else if (repairModeActive)
        {
            if (repairActionNow || moveTarget == unitCell)
            {
                foundDestination = true;
                bestDest = unitCell;
            }
            else
            {
                foundDestination = turnStateManager.TryGetBestReachableCellTowardsHexDistance(
                    snapshot.BoardTilemap,
                    moveTarget,
                    occupiedByAllies,
                    out bestDest,
                    prioritizeDpq: false,
                    unit: unit,
                    preferLongerAdvanceOnTie: true,
                    penalizedCells: penalizedMovementCells,
                preferredCells: preferredSupportCells);
            }
        }
        else if (captureObjectiveActive)
        {
            // Desvio tatico: se assignedEnemy esta no corredor de captura, posiciona em celula DPQ
            // adjacente ao inimigo e ataca antes de retomar a marcha no proximo turno.
            bool skirmishDivert = false;
            if (!protectCaptureDiscipline
                && !captureActionNow && moveTarget != unitCell
                && assignedEnemy != null && !assignedEnemy.IsDead
                && unit.RemainingMovementPoints > 1
                && TryGetPreferredEngagementRangeForTarget(unit, assignedEnemy, out int skirmMinRange, out int skirmMaxRange))
            {
                Vector3Int enemyCellSkirmish = assignedEnemy.CurrentCellPosition;
                enemyCellSkirmish.z = 0;
                if (IsEnemyInCaptureLane(snapshot.BoardTilemap, unitCell, captureObjectiveCell, enemyCellSkirmish))
                {
                    bool skirmFound = turnStateManager.TryGetBestReachableCellAtHexDistanceBand(
                        snapshot.BoardTilemap,
                        enemyCellSkirmish,
                        skirmMinRange,
                        skirmMaxRange,
                        occupiedByAllies,
                        out bestDest,
                        prioritizeDpq: true,
                        unit: unit,
                        preferMaxDistance: false,
                        penalizedCells: penalizedMovementCells,
                        preferredCells: preferredSupportCells);

                    if (skirmFound)
                    {
                        int dpqDest = turnStateManager.GetCellDpqPoints(bestDest, unit);
                        if (dpqDest > 0)
                        {
                            // So desvia se a celula encontrada oferece cobertura real (DPQ > 0).
                            // Se o melhor destino eh terreno aberto, mantem a marcha normal.
                            foundDestination = true;
                            skirmishDivert = true;
                            targetEnemy = assignedEnemy;
                            engagingEnemy = true;
                            if (aiLog)
                            {
                                int dpqCurrent = turnStateManager.GetCellDpqPoints(unitCell, unit);
                                Debug.Log($"{T(aiTeam, 2)} [engage] {unit.name} desvio tatico DPQ vs {assignedEnemy.name}: {FormatCellLC(unitCell)}({dpqCurrent}) -> {FormatCellLC(bestDest)}({dpqDest})");
                            }
                        }
                        else if (aiLog)
                        {
                            Debug.Log($"{T(aiTeam, 2)} [engage] {unit.name} ignorou desvio vs {assignedEnemy.name}: melhor celula disponivel e DPQ=0, mantendo marcha.");
                        }
                    }
                }
            }
            else if (protectCaptureDiscipline && aiLog && assignedEnemy != null && !assignedEnemy.IsDead)
            {
                Debug.Log($"{T(aiTeam, 2)} [protect] {unit.name} ignorou desvio tatico lateral em plano Protect; mantendo foco em {captureObjectiveLabel}.");
            }

            if (!skirmishDivert)
            {
                if (captureActionNow || moveTarget == unitCell)
                {
                    foundDestination = true;
                    bestDest = unitCell;
                }
                else if (!captureActionNow && HasVisibleEnemyOnCell(snapshot, captureObjectiveCell)
                    && unit.RemainingMovementPoints > 1)
                {
                    // Objetivo ocupado por inimigo visÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­vel: usa assignedEnemy se disponÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â­vel,
                    // O alvo ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â© sempre o inimigo NA cÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©lula do objetivo (o bloqueador real).
                    // assignedEnemy pode ser um inimigo prÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â³ximo mas fora do prÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â©dio ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â nÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¾Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â£o serve aqui.
                    UnitManager capEngTarget = GetVisibleEnemyOnCell(snapshot, captureObjectiveCell);
                    bool capEngFound = false;
                    if (capEngTarget != null && TryGetPreferredEngagementRangeForTarget(unit, capEngTarget, out int capEngMinRange, out int capEngMaxRange))
                    {
                        // Posiciona em celula DPQ adjacente e ataca. Nao entra no predio enquanto ha combatente inimigo la.
                        capEngFound = turnStateManager.TryGetBestReachableCellAtHexDistanceBand(
                            snapshot.BoardTilemap,
                            captureObjectiveCell,
                            capEngMinRange,
                            capEngMaxRange,
                            occupiedByAllies,
                            out bestDest,
                            prioritizeDpq: true,
                            unit: unit,
                            preferMaxDistance: false,
                            penalizedCells: penalizedMovementCells,
                            preferredCells: preferredSupportCells);
                        if (capEngFound)
                        {
                            foundDestination = true;
                            targetEnemy = capEngTarget;
                            engagingEnemy = true;
                            if (aiLog)
                                Debug.Log($"{T(aiTeam, 2)} [engage] {unit.name} objetivo ocupado por inimigo ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â posicionando em DPQ adjacente ({FormatCellLC(captureObjectiveCell)}).");
                        }
                    }
                    // Se nao encontrou celula DPQ ou sem alvo valido, marcha normal em direcao ao objetivo.
                    if (!foundDestination)
                    {
                        foundDestination = turnStateManager.TryGetBestReachableCellTowardsHexDistance(
                            snapshot.BoardTilemap,
                            moveTarget,
                            occupiedByAllies,
                            out bestDest,
                            prioritizeDpq: true,
                            unit: unit,
                            preferLongerAdvanceOnTie: true,
                            penalizedCells: penalizedMovementCells,
                            preferredCells: preferredSupportCells);
                    }
                }
                else if (captureObjectiveIsEnemyTerritory && !HasVisibleEnemyOnCell(snapshot, captureObjectiveCell) && !occupiedByAllies.Contains(captureObjectiveCell))
                {
                    // FoW intel: objetivo e territorio inimigo mas sem visibilidade direta.
                    // Avanca o minimo necessario priorizando DPQ para revelar e cobrir o objetivo.
                    foundDestination = turnStateManager.TryGetBestReachableCellTowardsHexDistance(
                        snapshot.BoardTilemap,
                        moveTarget,
                        occupiedByAllies,
                        out bestDest,
                        prioritizeDpq: true,
                        unit: unit,
                        preferShorterAdvanceOnTie: true,
                        penalizedCells: penalizedMovementCells,
                        preferredCells: preferredSupportCells);
                    if (aiLog)
                        Debug.Log($"{T(aiTeam, 2)} [intel] {unit.name} avancando com cautela (FoW) para {FormatCellLC(captureObjectiveCell)}: priorizando DPQ e movimento minimo.");
                }
                else
                {
                    foundDestination = turnStateManager.TryGetBestReachableCellTowardsHexDistance(
                        snapshot.BoardTilemap,
                        moveTarget,
                        occupiedByAllies,
                        out bestDest,
                        prioritizeDpq: true,
                        unit: unit,
                        preferLongerAdvanceOnTie: true,
                        penalizedCells: penalizedMovementCells,
                        preferredCells: preferredSupportCells);
                }
            }
        }

        bool canRepositionWhileFiring = unit.RemainingMovementPoints > 1;
        if (!supplyObjectiveActive && !repairModeActive && !mergeObjectiveActive && !captureObjectiveActive && engagingEnemy && TryGetPreferredEngagementRangeForTarget(unit, targetEnemy, out int engageMinRange, out int engageMaxRange))
        {
            bool canFireAssignedTargetFromCurrent = targetEnemy != null && CanUnitFireAtTargetFromCurrentPosition(unit, targetEnemy);

            if (stanceBehavior.holdPositionWhenInRange && canFireAssignedTargetFromCurrent)
            {
                foundDestination = true;
                bestDest = unitCell;
            }
            else if (stanceBehavior.requireSightlineBeforeEngaging && !canFireAssignedTargetFromCurrent)
            {
                engagingEnemy = false;
                targetEnemy = null;
            }
            else if (canRepositionWhileFiring)
            {
                bool preferMaxRangeWhenAlreadyFiring = stanceBehavior.preferMaxEngagementRange;
                foundDestination = turnStateManager.TryGetBestReachableCellAtHexDistanceBand(
                    snapshot.BoardTilemap,
                    moveTarget,
                    engageMinRange,
                    engageMaxRange,
                    occupiedByAllies,
                    out bestDest,
                    prioritizeDpq: true,
                    unit: unit,
                    preferMaxDistance: preferMaxRangeWhenAlreadyFiring,
                    penalizedCells: penalizedMovementCells,
                preferredCells: preferredSupportCells);

                if (foundDestination)
                {
                    bestDest.z = 0;


                    if (bestDest != unitCell)
                    {
                        int currentDpq = turnStateManager.GetCellDpqPoints(unitCell, unit);
                        int candidateDpq = turnStateManager.GetCellDpqPoints(bestDest, unit);
                        if (aiLog)
                        {
                            Debug.Log(
                                $"{T(aiTeam, 2)} [engage] {unit.name} reposicionando para combate por classificacao={combatClassification}: " +
                                $"{FormatCellLC(unitCell)}({currentDpq}) -> {FormatCellLC(bestDest)}({candidateDpq})");
                        }
                    }
                }
            }
            else
            {
                foundDestination = true;
                bestDest = unitCell;
            }
        }

        if (!supplyObjectiveActive && !repairModeActive && !mergeObjectiveActive && !captureObjectiveActive && intelCanFire && !foundDestination)
            foundDestination = true;

        if (!supplyObjectiveActive && !repairModeActive && !mergeObjectiveActive && !captureObjectiveActive && !foundDestination && engagingEnemy && TryGetPreferredArtilleryRange(unit, out int artilleryMinRange, out int artilleryMaxRange))
        {
            foundDestination = turnStateManager.TryGetBestReachableCellAtHexDistanceBand(
                snapshot.BoardTilemap,
                moveTarget,
                artilleryMinRange,
                artilleryMaxRange,
                occupiedByAllies,
                out bestDest,
                prioritizeDpq: true,
                unit: unit,
                preferMaxDistance: true,
                penalizedCells: penalizedMovementCells,
                preferredCells: preferredSupportCells);
        }

        if (!supplyObjectiveActive
            && !repairModeActive
            && !mergeObjectiveActive
            && !captureObjectiveActive
            && !engagingEnemy
            && !foundDestination
            && stanceBehavior.repositionToFireRange
            && artilleryRepositionAnchorEnemy != null
            && !artilleryRepositionAnchorEnemy.IsDead
            && TryGetPreferredArtilleryRange(unit, out int artilleryRepositionMinRange, out int artilleryRepositionMaxRange))
        {
            Vector3Int artilleryAnchorCell = artilleryRepositionAnchorEnemy.CurrentCellPosition;
            artilleryAnchorCell.z = 0;

            foundDestination = turnStateManager.TryGetBestReachableCellAtHexDistanceBand(
                snapshot.BoardTilemap,
                artilleryAnchorCell,
                artilleryRepositionMinRange,
                artilleryRepositionMaxRange,
                occupiedByAllies,
                out bestDest,
                prioritizeDpq: true,
                unit: unit,
                preferMaxDistance: true,
                penalizedCells: penalizedMovementCells,
                preferredCells: preferredSupportCells);

            if (foundDestination && aiLog)
            {
                Debug.Log($"{T(aiTeam, 2)} [reposition] {unit.name} (artilharia) escolheu reposicionamento tatico em faixa {artilleryRepositionMinRange}-{artilleryRepositionMaxRange} de {artilleryRepositionAnchorEnemy.name}.");
            }
            else if (!foundDestination && !stanceBehavior.holdGroundWhenIdle)
            {
                // Nenhuma celula na faixa de alcance atingivel: avanca em direcao ao ancora
                // para reduzir a distancia nos proximos turnos (em vez de seguir coesao de plano).
                // Com holdGroundWhenIdle: nao avanca; ancora onde esta e aguarda.
                Vector3Int advanceTowardAnchor = artilleryRepositionAnchorEnemy.CurrentCellPosition;
                advanceTowardAnchor.z = 0;
                moveTarget = advanceTowardAnchor;
                if (aiLog)
                    Debug.Log($"{T(aiTeam, 2)} [reposition] {unit.name} (artilharia) sem celula na faixa; avancando em direcao a {artilleryRepositionAnchorEnemy.name} @ {FormatCellLC(advanceTowardAnchor)}.");
            }
        }

        if (!foundDestination)
        {
            bool isIndirectUnit = stanceBehavior.repositionToFireRange;
            if (transportPickupObjectiveActive || transportCarryObjectiveActive || transportRendezvousObjectiveActive)
                moveTarget = transportObjectiveCell;
            foundDestination = turnStateManager.TryGetBestReachableCellTowardsHexDistance(
                snapshot.BoardTilemap,
                moveTarget,
                occupiedByAllies,
                out bestDest,
                prioritizeDpq: engagingEnemy || isIndirectUnit
                    ? stanceBehavior.prioritizeDpqAtBattle || engagingEnemy || isIndirectUnit
                    : stanceBehavior.prioritizeDpqDuringTravel,
                unit: unit,
                preferLongerAdvanceOnTie: supplyObjectiveActive || repairModeActive || captureObjectiveActive || supplierParkingRequested || transportRendezvousObjectiveActive || (!defendMode && !intelCanFire),
                preferShorterAdvanceOnTie: supplierPlanCohesionRequested,
                penalizedCells: penalizedMovementCells,
                preferredCells: preferredSupportCells);
        }

        if (!foundDestination)
            bestDest = unitCell;

        bestDest.z = 0;

        AIUnitProfile supplierProfile = null;
        if (isSupplierUnit && unit.TryGetUnitData(out UnitData supplierUnitData) && supplierUnitData != null)
            supplierProfile = supplierUnitData.aiUnitProfile;

        if (isSupplierUnit
            && !repairDislodgeActive
            && bestDest != unitCell
            && IsCellTooDangerousForSupport(snapshot, bestDest, allowModerateRiskForSupply: supplyObjectiveActive, supplierProfile: supplierProfile))
        {
            if (TryFindSaferSupplierStagingCell(unit, snapshot, moveTarget, occupiedByAllies, preferredSupportCells, penalizedMovementCells, out Vector3Int saferSupportCell))
            {
                if (aiLog)
                    Debug.Log($"{T(aiTeam, 2)} [support] {unit.name} evitou avancar para {FormatCellLC(bestDest)} por risco alto; redirecionando para staging seguro em {FormatCellLC(saferSupportCell)}.");
                bestDest = saferSupportCell;
            }
            else if (supplyObjectiveActive && !supplyActionNow && IsFriendlyConstructionCell(snapshot, unit.TeamId, unitCell) && TryGetSupplierIdleParkingCell(unit, snapshot, occupiedByAllies, out Vector3Int supplierParkingCell))
            {
                if (aiLog)
                    Debug.Log($"{T(aiTeam, 2)} [support] {unit.name} evitou avancar para {FormatCellLC(bestDest)} por risco alto; saindo da construcao para estacionamento seguro em {FormatCellLC(supplierParkingCell)}.");
                bestDest = supplierParkingCell;
            }
            else
            {
                if (aiLog)
                    Debug.Log($"{T(aiTeam, 2)} [support] {unit.name} evitou avancar para {FormatCellLC(bestDest)} por risco alto; mantendo posicao segura.");
                bestDest = unitCell;
            }
        }


        if (occupiedByAllies != null && bestDest != unitCell && occupiedByAllies.Contains(bestDest))
        {
            if (aiLog)
                Debug.Log($"{T(aiTeam, 2)} destino calculado ocupado por aliado ({bestDest}); aplicando stay/fallback.");
            bestDest = unitCell;
        }

        if (bestDest != unitCell && !turnStateManager.IsAutomatedMovementCellReachable(bestDest))
        {
            if (aiLog)
                Debug.Log($"{T(aiTeam, 2)} destino calculado nao esta nos caminhos validos atuais ({bestDest}); aplicando stay/fallback.");
            bestDest = unitCell;
        }

        if (bestDest == unitCell)
        {
            turnStateManager.HandleConfirmWithFeedback();
            if (selectDelay > 0f)
                yield return new WaitForSeconds(selectDelay);
        }
        else
        {
            yield return StartCoroutine(turnStateManager.MoveCursorToCellWithAutomatedTravel(bestDest));
            turnStateManager.HandleConfirmWithFeedback();
            yield return StartCoroutine(turnStateManager.WaitUntilMovementAnimationDone(5f));
        }

        if (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.UnitSelected)
        {
            if (intelCanFire)
            {
                if (aiLog)
                    Debug.Log($"{T(aiTeam, 2)} destino invalido para mover; fallback para atirar parado.");

                yield return StartCoroutine(turnStateManager.MoveCursorToCellWithAutomatedTravel(unitCell));
                turnStateManager.HandleConfirmWithFeedback();
                if (selectDelay > 0f)
                    yield return new WaitForSeconds(selectDelay);

                if (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.UnitSelected)
                {
                    turnStateManager.HandleConfirmWithFeedback();
                    if (selectDelay > 0f)
                        yield return new WaitForSeconds(selectDelay);
                }
            }
        }

        if (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.UnitSelected ||
            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral)
        {
            if (aiLog) Debug.Log($"{T(aiTeam, 2)} estado inesperado {turnStateManager.CurrentCursorState}, encerrando unidade");
            turnStateManager.HandleCancel();
            yield break;
        }

        bool merged = false;
        bool transferred = false;
        bool supplied = false;
        bool captured = false;
        bool embarked = false;
        bool disembarked = false;
        bool opportunisticCaptureActive = false;
        string opportunisticCaptureLabel = string.Empty;

        if (!repairModeActive
            && !mergeObjectiveActive
            && !supplyObjectiveActive
            && !transportPickupObjectiveActive
            && !transportCarryObjectiveActive
            && !transportRendezvousObjectiveActive
            && !captureObjectiveActive
            && turnStateManager.CanUnitCaptureFromCurrentPosition(
                unit,
                out ConstructionManager opportunisticConstruction,
                out _,
                out _)
            && opportunisticConstruction != null)
        {
            opportunisticCaptureActive = true;
            opportunisticCaptureLabel = opportunisticConstruction.ConstructionDisplayName;
        }

        if (mergeObjectiveActive && mergeTargetUnit != null)
        {
            merged = turnStateManager.HasAutomatedMergeAvailable()
                && turnStateManager.TryExecuteAutomatedMergePreferredTarget(mergeTargetUnit);
        }

        if (!merged && isSupplierUnit && supplyObjectiveActive)
        {
            if (supplyRefillMode)
            {
                if (CanTransferReceiveNow(supplyReceiveConstruction, null))
                {
                    transferred = turnStateManager.TryExecuteAutomatedTransferReceive(
                        preferredConstruction: supplyReceiveConstruction,
                        preferredUnit: null);
                }
                else if (aiLog)
                {
                    string refillLabel = !string.IsNullOrWhiteSpace(supplyObjectiveLabel) ? supplyObjectiveLabel : "hub aliado";
                    Debug.Log($"{T(aiTeam, 2)} [support] {unit.name} ficou sem refill agora: nenhum Recebedor valido em alcance para {refillLabel}. Encerrando unidade sem tentar transferir.");
                }
            }
            else
            {
                if (supplyTargetUnit != null && CanSupplyTargetNow(unit, supplyTargetUnit))
                    supplied = turnStateManager.TryExecuteAutomatedSupplyPreferredTarget(supplyTargetUnit);

                if (!supplied && TryCollectPrioritizedSupplyTargetsNow(unit, supplyTargetUnit, out List<UnitManager> supplyTargetsNow) && supplyTargetsNow.Count > 0)
                {
                    supplied = turnStateManager.TryExecuteAutomatedSupplyPreferredTargets(supplyTargetsNow);
                }
                else if (!supplied && supplyTargetUnit != null && aiLog)
                {
                    Debug.Log($"{T(aiTeam, 2)} [support] {unit.name} nao tentou suprir {supplyTargetUnit.name}: alvo nao esta valido/adjacente da posicao final.");
                }
            }
        }

        if (!merged && !supplied && !transferred && !captured && transportPickupObjectiveActive && transportTargetTransporter != null && turnStateManager.CachedPodeEmbarcarTargets != null && turnStateManager.CachedPodeEmbarcarTargets.Count > 0)
        {
            if (turnStateManager.HandleAutomatedSensorActionRequested(SensorActionType.Embark))
            {
                Vector3Int transporterCell = transportTargetTransporter.CurrentCellPosition;
                transporterCell.z = 0;
                embarked = turnStateManager.TryExecuteAutomatedEmbarkReplayTarget(transportTargetTransporter.InstanceId.ToString(), transporterCell);
                if (aiLog && embarked)
                    Debug.Log($"{T(aiTeam, 2)} [transport] {unit.name} embarcou em {transportTargetTransporter.name} para apoiar captura em {transportObjectiveLabel}.");
            }
        }

        bool shouldTryTransportDisembark =
            !merged &&
            !supplied &&
            !transferred &&
            !captured &&
            !embarked &&
            transportCarryObjectiveActive &&
            transportPassengerUnit != null &&
            (transportDisembarkNow || ShouldTransportDisembarkNow(
                unit,
                transportPassengerUnit,
                transportPassengerTargetCell,
                transportObjectiveCell,
                Mathf.Max(1, transportPassengerUnit.GetMovementRange())));

        if (shouldTryTransportDisembark)
        {
            if (TryExecuteAutomatedTransportDisembark(transportPassengerUnit, transportPassengerTargetCell, out Vector3Int chosenDisembarkCell))
            {
                disembarked = true;
                if (aiLog)
                    Debug.Log($"{T(aiTeam, 2)} [transport] {unit.name} desembarcou {transportPassengerUnit.name} em {FormatCellLC(chosenDisembarkCell)} para captura.");
            }
        }

        if (!merged && !supplied && !transferred)
        {
            captured =
                !repairModeActive &&
                !mergeObjectiveActive &&
                (captureObjectiveActive || opportunisticCaptureActive) &&
                turnStateManager.TryExecuteAutomatedCaptureIfAvailable();
        }

        if (!supplyObjectiveActive && !repairModeActive && !mergeObjectiveActive && !captureObjectiveActive && bestDest == unitCell && intelCanFire && intelFireTarget != null && targetEnemy == null)
            targetEnemy = intelFireTarget;

        if (!merged && !supplied && !transferred && !captured && captureObjectiveActive && stanceBehavior.engageNearestEnemies
            && stanceBehavior.captureInterruptBias != CaptureInterruptBias.None)
        {
            bool postMoveCanFire = turnStateManager.CanUnitFireAtAnyTargetFromCurrentPosition(unit, out UnitManager postMoveFireTarget);
            if (postMoveCanFire && postMoveFireTarget != null)
            {
                Vector3Int postMoveCell = unit.CurrentCellPosition;
                postMoveCell.z = 0;
                Vector3Int fireTargetCell = postMoveFireTarget.CurrentCellPosition;
                fireTargetCell.z = 0;

                // Morde no caminho: ataca inimigo dentro do corredor de captura se o score justifica.
                if (IsEnemyInCaptureLane(snapshot.BoardTilemap, postMoveCell, captureObjectiveCell, fireTargetCell)
                    && TryEvaluateReachableAttackScore(unit, postMoveFireTarget, snapshot, occupiedByAllies, out int captureSkirmishScore))
                {
                    int minCaptureSkirmishScore = stanceBehavior.captureInterruptBias switch
                    {
                        CaptureInterruptBias.Aggressive => 22000,
                        CaptureInterruptBias.Normal     => 28000,
                        CaptureInterruptBias.Passive    => 38000,
                        _                               => 28000
                    };
                    if (captureSkirmishScore >= minCaptureSkirmishScore)
                    {
                        targetEnemy = postMoveFireTarget;
                        engagingEnemy = true;
                        if (aiLog)
                            Debug.Log($"{T(aiTeam, 2)} [intel] captura interrompida por alvo no caminho: {postMoveFireTarget.name} (score={captureSkirmishScore})");
                    }
                }
                // Fallback oportunista: qualquer alvo alcancavel apos mover.
                // None ja foi filtrado acima. Passive nao dispara o fallback ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â¦ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â so morde no caminho com score alto.
                if (!engagingEnemy && stanceBehavior.captureInterruptBias != CaptureInterruptBias.Passive)
                {
                    targetEnemy = postMoveFireTarget;
                    engagingEnemy = true;
                    if (aiLog)
                        Debug.Log($"{T(aiTeam, 2)} [intel] captura oportunista pos-movimento: {postMoveFireTarget.name}");
                }
            }
        }

        // Post-move rescan para escolta com engageNearestEnemies: mesmo que tenha movido por coesao,
        // verifica se ha inimigos alcancaveis da nova posicao e engaja de oportunidade.
        if (!merged && !supplied && !transferred && !captured && !engagingEnemy
            && escortRoleActive && stanceBehavior.engageNearestEnemies)
        {
            bool postMoveCanFire = turnStateManager.CanUnitFireAtAnyTargetFromCurrentPosition(unit, out UnitManager postMoveEscortTarget);
            if (postMoveCanFire && postMoveEscortTarget != null)
            {
                targetEnemy = postMoveEscortTarget;
                engagingEnemy = true;
                if (aiLog)
                    Debug.Log($"{T(aiTeam, 2)} [intel] escolta engageNearestEnemies: oportunidade pos-movimento em {postMoveEscortTarget.name}");
            }
        }

        // Post-move rescan para transportador (rendezvous, carry ou idle): aproveita inimigos atacaveis
        // da posicao atual sem cancelar o objetivo de transporte.
        if (!merged && !supplied && !transferred && !captured && !engagingEnemy
            && (transportRendezvousObjectiveActive || transportCarryObjectiveActive || transportIdlePickupRequested)
            && stanceBehavior.engageNearestEnemies)
        {
            bool postMoveCanFire = turnStateManager.CanUnitFireAtAnyTargetFromCurrentPosition(unit, out UnitManager postMoveTransportTarget);
            if (postMoveCanFire && postMoveTransportTarget != null)
            {
                targetEnemy = postMoveTransportTarget;
                engagingEnemy = true;
                if (aiLog)
                    Debug.Log($"{T(aiTeam, 2)} [intel] transporte: oportunidade de ataque pos-movimento em {postMoveTransportTarget.name}");
            }
        }

        // Revalida o alvo apos o movimento real: cohesion ou bloqueio podem ter levado a unidade
        // para longe do alvo calculado no scoring (que assumia movimento em direcao a ele).
        if (engagingEnemy && targetEnemy != null)
        {
            Vector3Int postMoveCell = unit.CurrentCellPosition;
            postMoveCell.z = 0;
            if (!CanReachAndAttackThisTurn(unit, targetEnemy, snapshot, occupiedByAllies, postMoveCell))
            {
                if (aiLog)
                    Debug.Log($"{T(aiTeam, 2)} [revalida] {unit.name}: alvo {targetEnemy.name} nao alcancavel da posicao final {FormatCellLC(postMoveCell)}; descartando.");
                engagingEnemy = false;
                targetEnemy = null;
            }
        }

        // Unidades com holdPositionWhenInRange (artilharia) atiram paradas mesmo em modo reparo,
        // desde que nao tenham movido (bestDest == unitCell = ja estao na construcao).
        // Combatentes em reparo nao revidiam: priorizam chegar a base.
        bool canFireWhileRepairing = repairModeActive && aiProfile != null && aiProfile.canShootFromDistanceWhileRepairing && bestDest == unitCell;

        bool attacked = !merged
            && !supplied
            && !transferred
            && !captured
            && !embarked
            && !disembarked
            && !supplyObjectiveActive
            && !mergeObjectiveActive
            && (!repairModeActive || repairDislodgeActive || canFireWhileRepairing)
            && engagingEnemy
            && turnStateManager.HasAutomatedAttackAvailable()
            && turnStateManager.TryExecuteAutomatedAttackPreferredTarget(targetEnemy);

        // Fallback intel fire: ataque falhou (ex: hibrido nao alcancou o alvo apos cohesion bloquear movimento),
        // mas a unidade pode mirar outro alvo da posicao atual.
        // Respeita as mesmas guards do bloco principal: nao atira em modo reparo, suprimento ou fusao.
        if (!attacked && !merged && !supplied && !transferred && !captured && !embarked && !disembarked
            && !supplyObjectiveActive && !mergeObjectiveActive
            && (!repairModeActive || repairDislodgeActive || canFireWhileRepairing)
            && engagingEnemy
            && intelCanFire && intelFireTarget != null
            && turnStateManager.HasAutomatedAttackAvailable())
        {
            attacked = turnStateManager.TryExecuteAutomatedAttackPreferredTarget(intelFireTarget);
        }

        if (!merged && !supplied && !transferred && !captured && !embarked && !disembarked && !attacked)
            turnStateManager.HandleAutomatedMoveOnlyActionRequested();

        yield return StartCoroutine(turnStateManager.WaitUntilAutomatedNeutralReady(12f));

        if (aiLog)
        {
            string detailLabel = supplyObjectiveActive
                ? (supplyRefillMode ? $"reabastecer ST em {supplyObjectiveLabel}" : $"suprir {supplyObjectiveLabel}")
                : (mergeObjectiveActive
                    ? mergeObjectiveLabel
                    : (repairModeActive
                        ? $"reparo em {repairObjectiveLabel}"
                        : (captureObjectiveActive
                            ? $"captura em {captureObjectiveLabel}{(captureUsedPlanner ? " [planner]" : " [sensor]")}"
                            : (opportunisticCaptureActive
                                ? $"captura em {opportunisticCaptureLabel} [oportunista]"
                                : (engagingEnemy && targetEnemy != null
                                    ? targetEnemy.name
                                    : (planCohesionActive
                                        ? $"coesao em {planCohesionLabel}"
                                        : (transportRendezvousObjectiveActive
                                            ? $"rendezvous transporte: {transportObjectiveLabel}"
                                            : (transportIdlePickupRequested ? "aguarda pickup" : "reposicionar"))))))));

            if (intelCanFire && intelFireTarget != null)
                Debug.Log($"{T(aiTeam, 2)} [intel] {unit.name} @ {FormatCellLC(unitCell)} | PODE MIRAR {intelFireTarget.name} @ {FormatCellLC(intelFireTarget.CurrentCellPosition)} | acao: {detailLabel} | alocado: {planAllocationLabel}");
            else
                Debug.Log($"{T(aiTeam, 2)} [intel] {unit.name} @ {FormatCellLC(unitCell)} | sem alcance na posicao atual -> acao: {detailLabel} | alocado: {planAllocationLabel}");

            string outcome;
            if (merged)
                outcome = "fundiu";
            else if (supplied)
                outcome = "supriu aliado";
            else if (transferred)
                outcome = "recebeu transferencia";
            else if (captured)
                outcome = "capturou/recuperou";
            else if (engagingEnemy)
                outcome = attacked ? "atacou" : "moveu sem ataque";
            else if (repositioningForDefense)
                outcome = "reposicionou defesa";
            else if (mergeObjectiveActive)
                outcome = "avancou para fusao";
            else if (repairModeActive)
                outcome = "retornou para reparo";
            else if (captureObjectiveActive)
                outcome = "avancou para captura";
            else if (opportunisticCaptureActive)
                outcome = "captura oportunista";
            else if (supplyObjectiveActive)
                outcome = "avancou para suprir";
            else if (planCohesionActive)
                outcome = "manteve coesao do plano";
            else if (transportRendezvousObjectiveActive)
                outcome = "rendezvous com passageiro";
            else if (transportIdlePickupRequested)
                outcome = "aguardou pickup";
            else
                outcome = "avancou HQ";
            Debug.Log($"{T(aiTeam, 2)} {FormatCellLC(unitCell)} -> {FormatCellLC(bestDest)} (alvo: {FormatCellLC(moveTarget)}) | {outcome}");
        }
    }
    private static int GetHexDistance(Tilemap tilemap, Vector3Int from, Vector3Int to, int maxSteps)
    {
        if (tilemap == null)
            return Mathf.RoundToInt((to - from).magnitude);

        from.z = 0;
        to.z = 0;
        if (from == to)
            return 0;

        HashSet<Vector3Int> visited = new HashSet<Vector3Int> { from };
        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        Queue<int> depth = new Queue<int>();
        frontier.Enqueue(from);
        depth.Enqueue(0);

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        int safeMax = Mathf.Clamp(maxSteps, 1, 256);

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            int d = depth.Dequeue();
            if (d >= safeMax)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int n = neighbors[i];
                n.z = 0;
                if (visited.Contains(n))
                    continue;
                if (n == to)
                    return d + 1;

                visited.Add(n);
                frontier.Enqueue(n);
                depth.Enqueue(d + 1);
            }
        }

        return int.MaxValue;
    }
private static bool IsEnemyWithinDefendRadius(AISnapshot snapshot, UnitManager enemy)
    {
        if (snapshot == null || enemy == null || !snapshot.HasHq || snapshot.BoardTilemap == null)
            return false;

        Vector3Int hqCell = snapshot.HqCell;
        hqCell.z = 0;
        Vector3Int enemyCell = enemy.CurrentCellPosition;
        enemyCell.z = 0;
        return HexCoordinates.IsWithinRange(snapshot.BoardTilemap, hqCell, enemyCell, snapshot.HqDefendRadius);
    }

    private static HashSet<Vector3Int> BuildAllyCellSet(AISnapshot snapshot, UnitManager excluding)
    {
        HashSet<Vector3Int> set = new HashSet<Vector3Int>();
        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager u = snapshot.FriendlyUnits[i];
            if (u == null || u == excluding || u.IsDead)
                continue;
            Vector3Int cell = u.CurrentCellPosition;
            cell.z = 0;
            set.Add(cell);
        }
        return set;
    }

    private UnitManager FindClosestVisibleEnemy(UnitManager unit, AISnapshot snapshot, bool restrictToDefenseRadius = false)
    {
        Tilemap board = snapshot.BoardTilemap;
        Vector3 unitWorld = board != null
            ? board.GetCellCenterWorld(unit.CurrentCellPosition)
            : new Vector3(unit.CurrentCellPosition.x, unit.CurrentCellPosition.y, 0f);

        UnitManager closest = null;
        float bestDistSq = float.MaxValue;
        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;
            if (restrictToDefenseRadius && !IsEnemyWithinDefendRadius(snapshot, enemy))
                continue;
            Vector3 enemyWorld = board != null
                ? board.GetCellCenterWorld(enemy.CurrentCellPosition)
                : new Vector3(enemy.CurrentCellPosition.x, enemy.CurrentCellPosition.y, 0f);
            float distSq = (enemyWorld - unitWorld).sqrMagnitude;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                closest = enemy;
            }
        }
        return closest;
    }


    private static bool IsSupplyTruckOutOfReserves(UnitManager unit, AIUnitProfile profile = null)
    {
        if (unit == null)
            return false;

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources == null || resources.Count <= 0)
            return true;

        List<UnitEmbarkedSupply> baseline = null;
        if (unit.TryGetUnitData(out UnitData unitDataRef) && unitDataRef != null)
            baseline = unitDataRef.supplierResources;

        int fuelThreshold  = profile != null ? profile.restockFuelThresholdPercent  : 0;
        int ammoThreshold  = profile != null ? profile.restockAmmoThresholdPercent  : 0;
        int partsThreshold = profile != null ? profile.restockPartsThresholdPercent : 0;

        bool hasCore = false;
        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || entry.supply == null)
                continue;
            if (!IsCoreSupplyForTruck(entry.supply))
                continue;

            hasCore = true;

            if (entry.amount <= 0)
                return true;

            int threshold = GetRestockThresholdForSupply(entry.supply, fuelThreshold, ammoThreshold, partsThreshold);
            if (threshold > 0)
            {
                int maxAmount = FindBaselineSupplyAmount(baseline, entry.supply);
                if (maxAmount > 0 && entry.amount * 100 < maxAmount * threshold)
                    return true;
            }
        }

        if (hasCore)
            return false;

        // Fallback: sem mapeamento de supply core, considera vazio apenas quando TODOS estao zerados.
        bool hasAnyPositive = false;
        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry != null && entry.amount > 0)
            {
                hasAnyPositive = true;
                break;
            }
        }

        return !hasAnyPositive;
    }

    private static int GetRestockThresholdForSupply(SupplyData supply, int fuelThreshold, int ammoThreshold, int partsThreshold)
    {
        string text = ((supply.id ?? "") + " " + (supply.displayName ?? "")).ToLowerInvariant();
        if (text.Contains("galao")) return fuelThreshold;
        if (text.Contains("caixa")) return ammoThreshold;
        if (text.Contains("peca"))  return partsThreshold;
        return 0;
    }

    private static int FindBaselineSupplyAmount(List<UnitEmbarkedSupply> baseline, SupplyData supply)
    {
        if (baseline == null || supply == null)
            return 0;
        for (int i = 0; i < baseline.Count; i++)
        {
            if (baseline[i] != null && baseline[i].supply == supply)
                return baseline[i].amount;
        }
        return 0;
    }

    private static bool IsCoreSupplyForTruck(SupplyData supply)
    {
        if (supply == null)
            return false;

        string id = !string.IsNullOrWhiteSpace(supply.id) ? supply.id : string.Empty;
        string name = !string.IsNullOrWhiteSpace(supply.displayName) ? supply.displayName : string.Empty;
        string text = (id + " " + name).ToLowerInvariant();

        return text.Contains("peca")
            || text.Contains("galao")
            || text.Contains("caixa");
    }

    private bool TryGetMostCriticalAllyForSupply(UnitManager supplier, AISnapshot snapshot, out UnitManager target)
    {
        target = null;
        if (!TryCollectSupplyNavigationCandidates(supplier, snapshot, out List<UnitManager> targets) || targets.Count <= 0)
            return false;

        target = targets[0];
        return target != null;
    }

    private bool TryCollectSupplyNavigationCandidates(UnitManager supplier, AISnapshot snapshot, out List<UnitManager> targets)
    {
        targets = new List<UnitManager>();
        if (supplier == null || snapshot == null)
            return false;

        AIUnitProfile supplierProfile = null;
        if (supplier.TryGetUnitData(out UnitData supplierDataNav) && supplierDataNav != null)
            supplierProfile = supplierDataNav.aiUnitProfile;

        Vector3Int supplierCell = supplier.CurrentCellPosition;
        supplierCell.z = 0;

        // Prioridade 1: alvos validados pelo sensor (adjacentes/alcance valido agora).
        // Entre eles, escolhe o mais proximo; criticidade desempata.
        if (turnStateManager != null)
        {
            var sensorOptions = new List<PodeSuprirOption>();
            if (turnStateManager.TryGetSupplyTargets(supplier, sensorOptions, out _) && sensorOptions.Count > 0)
            {
                UnitManager immediateTarget = null;
                int bestDistance = int.MaxValue;
                int bestScore = int.MinValue;
                for (int i = 0; i < sensorOptions.Count; i++)
                {
                    UnitManager candidate = sensorOptions[i]?.targetUnit;
                    if (candidate == null || candidate.IsDead || candidate.ReceivedSuppliesThisTurn)
                        continue;
                    if (!IsSupplyTruckTargetThresholdMet(candidate, out int score, supplierProfile))
                        continue;

                    Vector3Int candidateCell = candidate.CurrentCellPosition;
                    candidateCell.z = 0;
                    int distance = GetHexDistance(snapshot.BoardTilemap, supplierCell, candidateCell, 64);
                    if (distance == int.MaxValue)
                        distance = 64;

                    bool better = distance < bestDistance || (distance == bestDistance && score > bestScore);
                    if (!better)
                        continue;

                    bestDistance = distance;
                    bestScore = score;
                    immediateTarget = candidate;
                }
                if (immediateTarget != null)
                {
                    targets.Add(immediateTarget);
                    return true;
                }
            }
        }

        // Prioridade 2: objetivo de navegacao ? aliado mais proximo no snapshot.
        // Criticidade entra apenas como desempate.
        if (snapshot.FriendlyUnits == null)
            return false;

        List<(UnitManager unit, int score, int distance)> ranked = new List<(UnitManager unit, int score, int distance)>();

        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager ally = snapshot.FriendlyUnits[i];
            if (ally == null || ally == supplier || ally.IsDead || ally.IsEmbarked || ally.ReceivedSuppliesThisTurn)
                continue;
            if (!IsSupplyTruckTargetThresholdMet(ally, out int score, supplierProfile))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            int distance = GetHexDistance(snapshot.BoardTilemap, supplierCell, allyCell, 64);
            if (distance == int.MaxValue)
                distance = 64;

            ranked.Add((ally, score, distance));
        }

        if (ranked.Count <= 0)
            return false;

        ranked.Sort((a, b) =>
        {
            int scoreCompare = b.score.CompareTo(a.score);
            if (scoreCompare != 0)
                return scoreCompare;
            return a.distance.CompareTo(b.distance);
        });

        for (int i = 0; i < ranked.Count; i++)
            targets.Add(ranked[i].unit);

        return targets.Count > 0;
    }
    private bool CanSupplyTargetNow(UnitManager supplier, UnitManager target)
    {
        if (supplier == null || target == null || turnStateManager == null)
            return false;
        if (target.ReceivedSuppliesThisTurn)
            return false;

        List<PodeSuprirOption> options = new List<PodeSuprirOption>();
        if (!turnStateManager.TryGetSupplyTargets(supplier, options, out _) || options.Count <= 0)
            return false;

        for (int i = 0; i < options.Count; i++)
        {
            PodeSuprirOption option = options[i];
            if (option == null || option.targetUnit == null)
                continue;
            if (option.targetUnit == target)
                return true;
        }

        return false;
    }

    private bool CanTransferReceiveNow(ConstructionManager preferredConstruction, UnitManager preferredUnit)
    {
        if (turnStateManager == null)
            return false;

        IReadOnlyList<PodeTransferirOption> options = turnStateManager.CachedPodeTransferirTargets;
        if (options == null || options.Count <= 0)
            return false;

        for (int i = 0; i < options.Count; i++)
        {
            PodeTransferirOption option = options[i];
            if (option == null || option.flowMode != TransferFlowMode.Recebedor)
                continue;

            if (preferredConstruction != null && option.targetConstruction != preferredConstruction)
                continue;
            if (preferredUnit != null && option.targetUnit != preferredUnit)
                continue;

            return true;
        }

        return false;
    }

    private bool TryFindSaferSupplierStagingCell(
        UnitManager supplier,
        AISnapshot snapshot,
        Vector3Int moveTarget,
        HashSet<Vector3Int> occupiedByAllies,
        HashSet<Vector3Int> preferredCells,
        HashSet<Vector3Int> penalizedCells,
        out Vector3Int safeCell)
    {
        safeCell = default;
        if (supplier == null || snapshot == null || snapshot.BoardTilemap == null)
            return false;

        AIUnitProfile supplierProfile = null;
        if (supplier.TryGetUnitData(out UnitData supplierData) && supplierData != null)
            supplierProfile = supplierData.aiUnitProfile;

        TerrainDatabase terrainDb = turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null;
        int moveBudget = Mathf.Max(0, supplier.RemainingMovementPoints);
        Dictionary<Vector3Int, List<Vector3Int>> paths = UnitMovementPathRules.CalcularCaminhosValidos(
            snapshot.BoardTilemap,
            supplier,
            moveBudget,
            terrainDb);
        if (paths == null || paths.Count <= 0)
            return false;

        Vector3Int origin = supplier.CurrentCellPosition;
        origin.z = 0;
        moveTarget.z = 0;

        bool found = false;
        int bestScore = int.MinValue;
        int bestDistanceToTarget = int.MaxValue;

        foreach (var kv in paths)
        {
            Vector3Int candidate = kv.Key;
            candidate.z = 0;
            if (candidate == origin)
                continue;
            if (occupiedByAllies != null && occupiedByAllies.Contains(candidate))
                continue;
            if (IsCellTooDangerousForSupport(snapshot, candidate, allowModerateRiskForSupply: true, supplierProfile: supplierProfile))
                continue;

            int distanceToTarget = GetHexDistance(snapshot.BoardTilemap, candidate, moveTarget, 64);
            if (distanceToTarget == int.MaxValue)
                distanceToTarget = 64;

            int score = -distanceToTarget * 100;
            if (preferredCells != null && preferredCells.Contains(candidate))
                score += 350;
            if (penalizedCells != null && penalizedCells.Contains(candidate))
                score -= 500;
            if (IsFriendlyConstructionCell(snapshot, supplier.TeamId, candidate))
                score -= 800;

            bool better = !found || score > bestScore || (score == bestScore && distanceToTarget < bestDistanceToTarget);
            if (!better)
                continue;

            found = true;
            bestScore = score;
            bestDistanceToTarget = distanceToTarget;
            safeCell = candidate;
        }

        return found;
    }

    private bool TryResolveSupplyObjectiveCell(
        UnitManager supplier,
        UnitManager target,
        AISnapshot snapshot,
        HashSet<Vector3Int> occupiedByAllies,
        HashSet<Vector3Int> preferredCells,
        HashSet<Vector3Int> penalizedCells,
        out Vector3Int objectiveCell,
        out bool actionNow)
    {
        objectiveCell = default;
        actionNow = false;
        if (supplier == null || target == null || snapshot == null || snapshot.BoardTilemap == null)
            return false;

        Vector3Int supplierCell = supplier.CurrentCellPosition;
        supplierCell.z = 0;
        if (CanSupplyTargetNow(supplier, target))
        {
            objectiveCell = supplierCell;
            actionNow = true;
            return true;
        }

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(snapshot.BoardTilemap, targetCell, neighbors);

        int bestScore = int.MinValue;
        int bestDistance = int.MaxValue;
        bool found = false;

        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int candidate = neighbors[i];
            candidate.z = 0;

            if ((occupiedByAllies != null && occupiedByAllies.Contains(candidate))
                || IsCellOccupiedBySnapshotUnit(snapshot, supplier, target, candidate))
                continue;

            int distance = GetHexDistance(snapshot.BoardTilemap, supplierCell, candidate, 64);
            if (distance == int.MaxValue)
                distance = 64;

            int score = -distance * 100;
            if (turnStateManager != null && turnStateManager.IsAutomatedMovementCellReachable(candidate))
                score += 1500;
            if (preferredCells != null && preferredCells.Contains(candidate))
                score += 250;
            if (penalizedCells != null && penalizedCells.Contains(candidate))
                score -= 500;

            bool better = !found || score > bestScore || (score == bestScore && distance < bestDistance);
            if (!better)
                continue;

            bestScore = score;
            bestDistance = distance;
            objectiveCell = candidate;
            found = true;
        }

        return found;
    }

    private static bool IsCellOccupiedBySnapshotUnit(AISnapshot snapshot, UnitManager supplier, UnitManager supplyTarget, Vector3Int cell)
    {
        if (snapshot == null)
            return false;

        cell.z = 0;
        if (snapshot.FriendlyUnits != null)
        {
            for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
            {
                UnitManager ally = snapshot.FriendlyUnits[i];
                if (ally == null || ally.IsDead || ally == supplier || ally == supplyTarget)
                    continue;

                Vector3Int allyCell = ally.CurrentCellPosition;
                allyCell.z = 0;
                if (allyCell == cell)
                    return true;
            }
        }

        if (snapshot.VisibleEnemies != null)
        {
            for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
            {
                UnitManager enemy = snapshot.VisibleEnemies[i];
                if (enemy == null || enemy.IsDead)
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition;
                enemyCell.z = 0;
                if (enemyCell == cell)
                    return true;
            }
        }

        return false;
    }

    private bool TryResolveMergeObjectiveForRepairingUnit(UnitManager unit, AISnapshot snapshot, out UnitManager target, out Vector3Int objectiveCell, out Vector3Int approachCell, out bool actionNow, out string label)
    {
        target = null;
        objectiveCell = unit != null ? unit.CurrentCellPosition : Vector3Int.zero;
        approachCell = unit != null ? unit.CurrentCellPosition : Vector3Int.zero;
        actionNow = false;
        label = string.Empty;
        if (unit == null || snapshot == null || snapshot.BoardTilemap == null)
            return false;

        if (TryGetImmediateMergeTargetForRepairingUnit(unit, snapshot, out target, out label))
        {
            objectiveCell = unit.CurrentCellPosition;
            objectiveCell.z = 0;
            approachCell = objectiveCell;
            actionNow = true;
            return true;
        }

        if (!TryGetMergeApproachTargetForRepairingUnit(unit, snapshot, out target, out objectiveCell, out approachCell, out label))
            return false;

        objectiveCell.z = 0;
        approachCell.z = 0;
        actionNow = false;
        return true;
    }

    private bool TryGetImmediateMergeTargetForRepairingUnit(UnitManager unit, AISnapshot snapshot, out UnitManager target, out string label)
    {
        target = null;
        label = string.Empty;
        if (unit == null || snapshot == null || snapshot.BoardTilemap == null)
            return false;

        List<PodeFundirOption> mergeOptions = new List<PodeFundirOption>();
        if (!PodeFundirSensor.CollectOptions(unit, snapshot.BoardTilemap, turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null, Mathf.Max(0, unit.RemainingMovementPoints), mergeOptions, out _) || mergeOptions.Count <= 0)
            return false;

        int bestCombinedHp = int.MinValue;
        for (int i = 0; i < mergeOptions.Count; i++)
        {
            PodeFundirOption option = mergeOptions[i];
            UnitManager candidate = option != null ? option.candidateUnit : null;
            if (candidate == null || candidate.IsDead)
                continue;

            int combinedHp = Mathf.Max(0, unit.CurrentHP) + Mathf.Max(0, candidate.CurrentHP);
            if (combinedHp > 10)
                continue;

            if (combinedHp <= bestCombinedHp)
                continue;

            bestCombinedHp = combinedHp;
            target = candidate;
        }

        if (target == null)
            return false;

        label = $"fusao com {target.name} (hp total={Mathf.Max(0, unit.CurrentHP) + Mathf.Max(0, target.CurrentHP)})";
        return true;
    }

    private bool TryGetMergeApproachTargetForRepairingUnit(UnitManager unit, AISnapshot snapshot, out UnitManager target, out Vector3Int objectiveCell, out Vector3Int approachCell, out string label)
    {
        target = null;
        objectiveCell = unit != null ? unit.CurrentCellPosition : Vector3Int.zero;
        approachCell = unit != null ? unit.CurrentCellPosition : Vector3Int.zero;
        label = string.Empty;
        if (unit == null || snapshot == null || snapshot.BoardTilemap == null)
            return false;

        Tilemap boardTilemap = snapshot.BoardTilemap;
        TerrainDatabase terrainDb = turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null;
        int remainingMovement = Mathf.Max(0, unit.RemainingMovementPoints);
        if (remainingMovement <= 0)
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> reachablePaths = UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap,
            unit,
            remainingMovement,
            terrainDb);
        if (reachablePaths == null || reachablePaths.Count <= 0)
            return false;

        int bestApproachCost = int.MaxValue;
        int bestCandidateDistance = int.MaxValue;
        int bestCombinedHp = int.MinValue;
        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        IReadOnlyList<UnitManager> friendlyUnits = snapshot.FriendlyUnits;
        for (int i = 0; friendlyUnits != null && i < friendlyUnits.Count; i++)
        {
            UnitManager candidate = friendlyUnits[i];
            if (!CanRepairModeMergeWithCandidate(unit, candidate))
                continue;

            int combinedHp = Mathf.Max(0, unit.CurrentHP) + Mathf.Max(0, candidate.CurrentHP);
            if (combinedHp > 10)
                continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition;
            candidateCell.z = 0;
            if (!UnitMovementPathRules.TryGetEnterCellCost(
                    boardTilemap,
                    unit,
                    candidateCell,
                    terrainDb,
                    applyOperationalAutonomyModifier: false,
                    out int enterCost))
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, candidateCell, neighbors);
            for (int n = 0; n < neighbors.Count; n++)
            {
                Vector3Int approachNeighbor = neighbors[n];
                approachNeighbor.z = 0;
                if (!reachablePaths.TryGetValue(approachNeighbor, out List<Vector3Int> path) || path == null || path.Count <= 0)
                    continue;
                if (!CanAiUnitEndMoveAtCell(unit, boardTilemap, approachNeighbor))
                    continue;

                int approachCost = Mathf.Max(0, UnitMovementPathRules.CalculateAutonomyCostForPath(
                    boardTilemap,
                    unit,
                    path,
                    terrainDb,
                    applyOperationalAutonomyModifier: false));
                int remainingAfterApproach = Mathf.Max(0, remainingMovement - approachCost);
                if (remainingAfterApproach < enterCost)
                    continue;

                int candidateDistance = GetHexDistance(boardTilemap, unit.CurrentCellPosition, candidateCell, 64);
                bool better = approachCost < bestApproachCost
                    || (approachCost == bestApproachCost && candidateDistance < bestCandidateDistance)
                    || (approachCost == bestApproachCost && candidateDistance == bestCandidateDistance && combinedHp > bestCombinedHp);
                if (!better)
                    continue;

                bestApproachCost = approachCost;
                bestCandidateDistance = candidateDistance;
                bestCombinedHp = combinedHp;
                target = candidate;
                objectiveCell = candidateCell;
                approachCell = approachNeighbor;
            }
        }

        if (target == null)
            return false;

        label = $"aproximar para fusao com {target.name} (hp total={Mathf.Max(0, unit.CurrentHP) + Mathf.Max(0, target.CurrentHP)})";
        return true;
    }

    private static bool CanAiUnitEndMoveAtCell(UnitManager unit, Tilemap boardTilemap, Vector3Int cell)
    {
        if (unit == null)
            return false;

        List<UnitManager> occupants = new List<UnitManager>();
        IReadOnlyList<UnitManager> activeUnits = UnitManager.AllActive;
        for (int i = 0; activeUnits != null && i < activeUnits.Count; i++)
        {
            UnitManager occupant = activeUnits[i];
            if (occupant == null || occupant == unit || occupant.IsEmbarked || !occupant.gameObject.activeInHierarchy)
                continue;
            if (boardTilemap != null && occupant.BoardTilemap != boardTilemap)
                continue;

            Vector3Int occupiedCell = occupant.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell != cell)
                continue;

            occupants.Add(occupant);
        }

        return OccupancyResolver.CanEndMove(unit, cell, occupants);
    }

    private static bool CanRepairModeMergeWithCandidate(UnitManager unit, UnitManager candidate)
    {
        if (unit == null || candidate == null || candidate == unit || candidate.IsDead || candidate.IsEmbarked)
            return false;
        if (!candidate.gameObject.activeInHierarchy)
            return false;
        if ((int)unit.TeamId != (int)candidate.TeamId)
            return false;
        if (!AreUnitsSameTypeForAiMerge(unit, candidate))
            return false;
        if (HasAnyTransportedPassenger(candidate))
            return false;
        return unit.GetDomain() == candidate.GetDomain()
            && unit.GetHeightLevel() == candidate.GetHeightLevel();
    }

    private static bool AreUnitsSameTypeForAiMerge(UnitManager a, UnitManager b)
    {
        if (a == null || b == null)
            return false;

        string aId = a.UnitId;
        string bId = b.UnitId;
        if (!string.IsNullOrWhiteSpace(aId) && !string.IsNullOrWhiteSpace(bId))
            return string.Equals(aId.Trim(), bId.Trim(), System.StringComparison.OrdinalIgnoreCase);

        if (a.TryGetUnitData(out UnitData aData) && b.TryGetUnitData(out UnitData bData))
            return aData != null && bData != null && aData == bData;

        return false;
    }

    private static bool IsFriendlyConstructionCell(AISnapshot snapshot, TeamId team, Vector3Int cell)
    {
        if (snapshot == null || snapshot.KnownConstructions == null)
            return false;

        cell.z = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Source == null)
                continue;
            if (info.TeamId != team)
                continue;

            Vector3Int c = info.Cell;
            c.z = 0;
            if (c == cell)
                return true;
        }

        return false;
    }

    private static bool ShouldKeepUnitInRepairMode(UnitManager unit)
    {
        if (unit == null || unit.IsDead)
            return false;

        unit.TryGetUnitData(out UnitData data);
        AIUnitProfile profile = data?.aiUnitProfile;
        int enterThreshold = profile != null ? profile.hpRepairThreshold    : 3;
        int exitThreshold  = profile != null ? profile.hpRepairExitThreshold : 8;

        if (enterThreshold <= 0)
        {
            if (unit.AIForcedToRepair)
                unit.SetAIForcedToRepair(false);
            return false;
        }

        bool forced = unit.AIForcedToRepair;

        if (!forced && unit.CurrentHP <= enterThreshold)
            forced = true;
        if (!forced && IsLowAutonomy(unit, profile) && !(data != null && data.isSupplier))
            forced = true;
        if (!forced && IsOutOfCombatAmmo(unit, profile))
            forced = true;

        if (forced)
        {
            bool hpOk   = unit.CurrentHP >= exitThreshold;
            bool fuelOk = !IsLowAutonomy(unit, profile);
            bool ammoOk = !IsOutOfCombatAmmo(unit, profile);
            if (hpOk && fuelOk && ammoOk)
                forced = false;
        }

        if (unit.AIForcedToRepair != forced)
            unit.SetAIForcedToRepair(forced);

        return forced;
    }

    private AIInitiative GetEffectiveInitiative(UnitManager unit)
    {
        if (unit == null)
            return AIInitiative.Medium;

        if (ShouldKeepUnitInRepairMode(unit))
            return AIInitiative.Retreat;

        if (unit.TryGetUnitData(out UnitData data) && data?.aiUnitProfile != null)
            return data.aiUnitProfile.initiative;

        return AIInitiative.Medium;
    }
    private static bool IsLowAutonomy(UnitManager unit, AIUnitProfile profile = null)
    {
        if (unit == null)
            return false;
        int maxFuel = unit.GetMaxFuel();
        if (maxFuel <= 0)
            return false;
        int threshold = profile != null ? profile.repairAutonomyThresholdPercent : 25;
        if (threshold <= 0)
            return false;
        return unit.CurrentFuel * 100 <= maxFuel * threshold;
    }

    private static bool IsOutOfCombatAmmo(UnitManager unit, AIUnitProfile profile = null)
    {
        if (unit == null)
            return false;

        bool anyWeapon = profile == null || profile.repairWhenAnyWeaponOutOfAmmo;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null || weapons.Count == 0)
            return false;

        bool hasAmmoBasedWeapon = false;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon w = weapons[i];
            if (w == null || w.weapon == null)
                continue;

            hasAmmoBasedWeapon = true;
            if (w.squadAmmunition <= 0)
            {
                if (anyWeapon)
                    return true; // qualquer arma zerada ja dispara
            }
            else if (!anyWeapon)
            {
                return false; // todas precisam estar zeradas; esta nao esta
            }
        }

        // anyWeapon=true e nenhuma zerada ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ false. anyWeapon=false e todas zeradas ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â‚¬Å¾Ã‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¡ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã†â€™Ãƒâ€ Ã¢â‚¬â„¢ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡Ãƒâ€šÃ‚Â¬ÃƒÆ’Ã¢â‚¬Â¦Ãƒâ€šÃ‚Â¾ÃƒÆ’Ã†â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…Â¡ÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Â¢ hasAmmoBasedWeapon.
        return !anyWeapon && hasAmmoBasedWeapon;
    }

    private bool TryGetBestRepairDislodgeTarget(
        UnitManager unit,
        AISnapshot snapshot,
        HashSet<Vector3Int> occupiedByAllies,
        Vector3Int unitCell,
        out UnitManager target,
        out string reason)
    {
        target = null;
        reason = string.Empty;

        if (unit == null || snapshot == null || snapshot.VisibleEnemies == null || snapshot.VisibleEnemies.Count <= 0)
            return false;
        if (!unit.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return false;

        RPSDatabase rpsDb = turnStateManager != null ? turnStateManager.RpsDatabaseRef : null;
        DPQMatchupDatabase dpqDb = turnStateManager != null ? turnStateManager.DpqMatchupDatabaseRef : null;
        WeaponPriorityData wpDb = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;
        if (rpsDb == null || dpqDb == null || wpDb == null)
            return false;

        int bestScore = int.MinValue;
        UnitManager best = null;

        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;
            if (!IsEnemyOccupyingOwnedConstructionForMyTeam(snapshot, unit, enemy))
                continue;
            if (!CanReachAndAttackThisTurn(unit, enemy, snapshot, occupiedByAllies, unitCell))
                continue;
            if (!enemy.TryGetUnitData(out UnitData defenderData) || defenderData == null)
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            int rawDist = GetHexDistance(snapshot.BoardTilemap, unitCell, enemyCell, 64);
            if (rawDist == int.MaxValue)
                continue;

            List<int> reachableDistances = new List<int>(8);
            int moveBudget = Mathf.Max(0, unit.RemainingMovementPoints);
            Dictionary<Vector3Int, List<Vector3Int>> paths = UnitMovementPathRules.CalcularCaminhosValidos(
                snapshot.BoardTilemap,
                unit,
                moveBudget,
                turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null);
            if (!TryCollectReachableAttackDistances(snapshot.BoardTilemap, unit, enemy, unitCell, enemyCell, paths, occupiedByAllies, reachableDistances))
                continue;

            int score = EvaluateAttackTargetScore(
                attackerData,
                defenderData,
                unit.CurrentHP,
                enemy.CurrentHP,
                rawDist,
                reachableDistances,
                currentStance == AIStance.Defend,
                rpsDb,
                dpqDb,
                wpDb,
                out _);

            if (score < 26000)
                continue;
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = enemy;
        }

        if (best == null)
            return false;

        target = best;
        reason = "desocupar construcao propria para reparar";
        return true;
    }

    private bool TryGetRepairFallbackCell(
        UnitManager unit,
        AISnapshot snapshot,
        out Vector3Int targetCell,
        out string label)
    {
        targetCell = default;
        label = string.Empty;

        if (unit == null || snapshot == null)
            return false;

        if (snapshot.HasHq)
        {
            targetCell = snapshot.HqCell;
            targetCell.z = 0;
            label = "HQ (fallback defensivo)";
            return true;
        }

        targetCell = unit.CurrentCellPosition;
        targetCell.z = 0;
        label = "fallback local";
        return true;
    }

    private static bool IsEnemyOccupyingOwnedConstructionForMyTeam(AISnapshot snapshot, UnitManager attacker, UnitManager enemy)
    {
        if (snapshot == null || attacker == null || enemy == null)
            return false;

        return TryGetEnemyOccupyingOwnedConstruction(snapshot, attacker.TeamId, enemy.CurrentCellPosition, out _);
    }

    private static bool TryGetEnemyOccupyingOwnedConstruction(AISnapshot snapshot, TeamId myTeam, Vector3Int enemyCell, out AIConstructionInfo occupiedInfo)
    {
        occupiedInfo = null;
        if (snapshot == null || snapshot.KnownConstructions == null)
            return false;

        enemyCell.z = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Source == null)
                continue;

            Vector3Int constructionCell = info.Cell;
            constructionCell.z = 0;
            if (constructionCell != enemyCell)
                continue;
            if (!info.Source.IsCapturable || info.Source.CapturePointsMax <= 0)
                continue;
            if (info.TeamId != myTeam)
                continue;

            occupiedInfo = info;
            return true;
        }

        return false;
    }

    private bool TryGetNearestOwnedConstruction(
        UnitManager unit,
        AISnapshot snapshot,
        out ConstructionManager targetConstruction,
        out Vector3Int targetCell,
        out string targetLabel)
    {
        targetConstruction = null;
        targetCell = default;
        targetLabel = string.Empty;

        if (unit == null || snapshot == null || snapshot.KnownConstructions == null || snapshot.KnownConstructions.Count == 0)
            return false;

        TeamId myTeam = unit.TeamId;
        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;

        int bestDistance = int.MaxValue;
        AIConstructionInfo bestInfo = null;

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Source == null)
                continue;
            if (info.TeamId != myTeam)
                continue;
            if (TryGetBlockingShoppingOccupant(info.Source, out UnitManager blocker) && blocker != null && blocker != unit)
                continue;

            Vector3Int cell = info.Cell;
            cell.z = 0;
            int distance = GetHexDistance(snapshot.BoardTilemap, unitCell, cell, 64);
            if (distance == int.MaxValue)
                distance = 64;

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestInfo = info;
        }

        if (bestInfo == null || bestInfo.Source == null)
            return false;

        targetConstruction = bestInfo.Source;
        targetCell = bestInfo.Cell;
        targetCell.z = 0;
        targetLabel = !string.IsNullOrWhiteSpace(bestInfo.DisplayName) ? bestInfo.DisplayName : bestInfo.Source.name;
        return true;
    }
    private static bool IsEscortMissionRole(AIPlanRole role)
    {
        return role == AIPlanRole.Escort
            || role == AIPlanRole.Artillery
            || role == AIPlanRole.Support;
    }


    private int GetEscortThreatPriority(
        UnitManager escortUnit,
        UnitManager enemy,
        AISnapshot snapshot,
        AIPlanIntent intent,
        Vector3Int cohesionCell,
        out string reason)
    {
        reason = string.Empty;

        if (escortUnit == null || enemy == null || snapshot == null)
            return int.MaxValue;

        Vector3Int escortCell = escortUnit.CurrentCellPosition;
        escortCell.z = 0;
        Vector3Int enemyCell = enemy.CurrentCellPosition;
        enemyCell.z = 0;
        cohesionCell.z = 0;

        if (intent != null)
        {
            if (intent.Assignments != null)
            {
                for (int i = 0; i < intent.Assignments.Count; i++)
                {
                    AIPlanAssignment asgn = intent.Assignments[i];
                    if (asgn == null || asgn.Role != AIPlanRole.Capture)
                        continue;

                    UnitManager captureUnit = FindUnitById(asgn.UnitInstanceId);
                    if (captureUnit != null && !captureUnit.IsDead)
                    {
                        Vector3Int captureCell = captureUnit.CurrentCellPosition;
                        captureCell.z = 0;
                        int toCaptureUnit = GetHexDistance(snapshot.BoardTilemap, captureCell, enemyCell, 64);
                        if (toCaptureUnit != int.MaxValue && toCaptureUnit <= 4)
                        {
                            reason = $"ameaca-ao-capturador d={toCaptureUnit}";
                            return 0;
                        }
                    }

                    if (asgn.HasPlannedCaptureTarget)
                    {
                        Vector3Int plannedCell = asgn.PlannedCaptureCell;
                        plannedCell.z = 0;
                        int toPlanned = GetHexDistance(snapshot.BoardTilemap, plannedCell, enemyCell, 64);
                        if (toPlanned != int.MaxValue && toPlanned <= 4)
                        {
                            reason = $"ameaca-ao-objetivo-planejado d={toPlanned}";
                            return 1;
                        }
                    }
                }
            }

            if (intent.HasCaptureTarget)
            {
                Vector3Int captureTarget = intent.CaptureTargetCell;
                captureTarget.z = 0;
                int toCaptureTarget = GetHexDistance(snapshot.BoardTilemap, captureTarget, enemyCell, 64);
                if (toCaptureTarget != int.MaxValue && toCaptureTarget <= 4)
                {
                    reason = $"ameaca-ao-objetivo-do-plano d={toCaptureTarget}";
                    return 1;
                }
            }
        }

        int toCohesion = GetHexDistance(snapshot.BoardTilemap, cohesionCell, enemyCell, 64);
        if (toCohesion != int.MaxValue && toCohesion <= 4)
        {
            reason = $"ameaca-a-coesao d={toCohesion}";
            return 2;
        }

        int toEscort = GetHexDistance(snapshot.BoardTilemap, escortCell, enemyCell, 64);
        if (toEscort != int.MaxValue && toEscort <= 3)
        {
            reason = $"ameaca-imediata-a-escolta d={toEscort}";
            return 3;
        }

        return int.MaxValue;
    }

    private bool IsEscortThreatRelevant(
        UnitManager escortUnit,
        UnitManager enemy,
        AISnapshot snapshot,
        AIPlanIntent intent,
        Vector3Int cohesionCell)
    {
        return GetEscortThreatPriority(escortUnit, enemy, snapshot, intent, cohesionCell, out _) != int.MaxValue;
    }


    private bool TryGetBackupPlanObjectiveForRogue(
        UnitManager unit,
        AISnapshot snapshot,
        out Vector3Int targetCell,
        out string targetLabel)
    {
        targetCell = default;
        targetLabel = string.Empty;

        if (unit == null || snapshot == null || currentTurnPlans == null || currentTurnPlans.Count == 0)
            return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        int bestScore = int.MinValue;
        bool found = false;

        for (int i = 0; i < currentTurnPlans.Count; i++)
        {
            AIPlanIntent intent = currentTurnPlans[i];
            if (intent == null)
                continue;
            if (string.IsNullOrWhiteSpace(intent.SelectionReason) || intent.SelectionReason.IndexOf("backup-retake", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Vector3Int candidateCell;
            if (intent.HasCaptureTarget)
            {
                candidateCell = intent.CaptureTargetCell;
            }
            else if (!TryGetSectorRepresentativeCell(snapshot, intent.Sector, out candidateCell))
            {
                continue;
            }

            candidateCell.z = 0;
            int pathDist = GetHexDistance(snapshot.BoardTilemap, unitCell, candidateCell, 64);
            if (pathDist == int.MaxValue)
                pathDist = 64;

            int pressure = Mathf.Max(0, intent.TacticalRiskScore);
            int assigned = intent.Assignments != null ? intent.Assignments.Count : 0;
            int score = pressure * 100 - pathDist * 10 - assigned * 25;
            if (!found || score > bestScore)
            {
                bestScore = score;
                targetCell = candidateCell;
                targetLabel = ResolvePlanDisplayName(intent);
                found = true;
            }
        }

        return found;
    }

    private static bool TryGetSectorRepresentativeCell(AISnapshot snapshot, ConstructionSector sector, out Vector3Int targetCell)
    {
        targetCell = default;
        if (snapshot == null || snapshot.KnownConstructions == null)
            return false;

        bool found = false;
        int bestPriority = int.MaxValue;
        int bestCapturePoints = int.MaxValue;

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Sector != sector)
                continue;

            int priority = info.IsCapturable ? 0 : 1;
            int capturePoints = info.IsCapturable ? info.CapturePoints : int.MaxValue;
            if (!found
                || priority < bestPriority
                || (priority == bestPriority && capturePoints < bestCapturePoints))
            {
                targetCell = info.Cell;
                targetCell.z = 0;
                bestPriority = priority;
                bestCapturePoints = capturePoints;
                found = true;
            }
        }

        return found;
    }
}
