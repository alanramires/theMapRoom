using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    public ActionSfx HandleConfirm()
    {
        LogStateStep("HandleConfirm");
        if (IsMovementAnimationRunning())
            return ActionSfx.None;
        if (TryConfirmPendingTransferPrompt())
            return ActionSfx.Confirm;

        switch (cursorState)
        {
            case CursorState.Neutral:
                return HandleConfirmWhileNeutral();
            case CursorState.InspectingUnit:
            case CursorState.InspectingBuilding:
            case CursorState.InspectingHotZone:
                return HandleConfirmWhileInspecting();
            case CursorState.UnitSelected:
                return HandleConfirmWhileUnitSelected();
            case CursorState.MoveuAndando:
                return HandleConfirmWhileMoveuAndando();
            case CursorState.MoveuParado:
                return HandleConfirmWhileMoveuParado();
            case CursorState.Capturando:
                return HandleConfirmWhileCapturando();
            case CursorState.Mirando:
                return HandleConfirmWhileMirando();
            case CursorState.Pousando:
                return HandleConfirmWhilePousando();
            case CursorState.Embarcando:
                return HandleConfirmWhileEmbarcando();
            case CursorState.Desembarcando:
                return HandleConfirmWhileDesembarcando();
            case CursorState.Fundindo:
                return HandleConfirmWhileFundindo();
            case CursorState.ShoppingAndServices:
                return HandleConfirmWhileShoppingAndServices();
            case CursorState.Suprindo:
                return HandleConfirmWhileSuprindo();
            case CursorState.CommandService:
                return HandleConfirmWhileCommandService();
            case CursorState.CommandServiceExecuting:
                return ActionSfx.None;
            case CursorState.RemovingUnit:
                return HandleConfirmWhileRemovingUnit();
            case CursorState.RemovingUnitExecuting:
                return ActionSfx.None;
            case CursorState.EndingTurn:
                return HandleConfirmWhileEndingTurn();
            case CursorState.EndingTurnExecuting:
                return ActionSfx.None;
            case CursorState.Saving:
            case CursorState.Loading:
                return ActionSfx.None;
            case CursorState.Planning:
                return HandleConfirmWhilePlanning();
            case CursorState.PlayerMenu:
            case CursorState.Replay:
                return ActionSfx.None;
            case CursorState.AircraftFuelDepletionQueue:
            case CursorState.TurnStartRallyQueue:
                return ActionSfx.None;
        }

        return ActionSfx.None;
    }

    public ActionSfx HandleCancel()
    {
        LogStateStep("HandleCancel", rollback: true);
        if (IsMovementAnimationRunning())
            return ActionSfx.None;
        if (TryCancelPendingTransferPrompt())
            return ActionSfx.Cancel;

        DiscardPendingCombatCinematicTrack();
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        switch (cursorState)
        {
            case CursorState.Neutral:
                return ActionSfx.None;
            case CursorState.InspectingUnit:
            case CursorState.InspectingBuilding:
            case CursorState.InspectingHotZone:
                return HandleCancelWhileInspecting();
            case CursorState.UnitSelected:
                ClearSelectionAndReturnToNeutral();
                return ActionSfx.Cancel;
            case CursorState.MoveuAndando:
                return HandleCancelWhileMoveuAndando();
            case CursorState.MoveuParado:
                return HandleCancelWhileMoveuParado();
            case CursorState.Capturando:
                return HandleCancelWhileCapturando();
            case CursorState.Mirando:
                return HandleCancelWhileMirando();
            case CursorState.Pousando:
                return HandleCancelWhilePousando();
            case CursorState.Embarcando:
                return HandleCancelWhileEmbarcando();
            case CursorState.Desembarcando:
                return HandleCancelWhileDesembarcando();
            case CursorState.Fundindo:
                return HandleCancelWhileFundindo();
            case CursorState.ShoppingAndServices:
                return HandleCancelWhileShoppingAndServices();
            case CursorState.Suprindo:
                return HandleCancelWhileSuprindo();
            case CursorState.CommandService:
                return HandleCancelWhileCommandService();
            case CursorState.CommandServiceExecuting:
                return ActionSfx.None;
            case CursorState.RemovingUnit:
                return HandleCancelWhileRemovingUnit();
            case CursorState.RemovingUnitExecuting:
                return ActionSfx.None;
            case CursorState.EndingTurn:
                return HandleCancelWhileEndingTurn();
            case CursorState.EndingTurnExecuting:
                return ActionSfx.None;
            case CursorState.Saving:
            case CursorState.Loading:
                return ActionSfx.None;
            case CursorState.Planning:
                return HandleCancelWhilePlanning();
            case CursorState.PlayerMenu:
            case CursorState.Replay:
                return ActionSfx.None;
            case CursorState.AircraftFuelDepletionQueue:
            case CursorState.TurnStartRallyQueue:
                return ActionSfx.None;
        }

        return ActionSfx.None;
    }

    private ActionSfx HandleConfirmWhileNeutral()
    {
        LogStateStep("HandleConfirmWhileNeutral");
        return HandleConfirmFromNeutralLikeState();
    }

    private ActionSfx HandleConfirmWhileInspecting()
    {
        LogStateStep("HandleConfirmWhileInspecting");
        ExitInspectStateToNeutral();
        return HandleConfirmFromNeutralLikeState();
    }

    private ActionSfx HandleConfirmFromNeutralLikeState()
    {
        if (cursorState != CursorState.Neutral)
            ExecuteAndReset("HandleConfirmFromNeutralLikeState: normalize");

        if (cursorController == null)
            return ActionSfx.None;

        Vector3Int cursorCell = cursorController.CurrentCell;
        UnitManager unit = FindUnitAtCell(cursorCell);
        int activeTeam = matchController != null ? matchController.ActiveTeamId : -1;

        if (unit != null)
        {
            bool isAlly = (int)unit.TeamId == activeTeam;
            if (!isAlly)
            {
                LogEnemyUnitInspection(unit, activeTeam);
                BeginInspectedHelper(unit);
                Advance(CursorState.InspectingUnit, "HandleConfirmFromNeutralLikeState: enemy inspect");
                DialogManager.Instance?.MarkHintLearned((TeamId)activeTeam, HelpHintId.Inspect);
                return ActionSfx.Confirm;
            }

            if (unit.HasActed)
            {
                Debug.Log($"debug: inspecionando aliado que ja agiu (unit={unit.name}, unitTeam={(int)unit.TeamId}, activeTeam={activeTeam}, hasActed={unit.HasActed})");
                BeginInspectedHelper(unit, paintThreatOverlay: false);
                Advance(CursorState.InspectingUnit, "HandleConfirmFromNeutralLikeState: acted ally inspect");
                DialogManager.Instance?.MarkHintLearned((TeamId)activeTeam, HelpHintId.Inspect);
                return ActionSfx.Confirm;
            }

            double takeoffPerfStart = Time.realtimeSinceStartupAsDouble;
            TryPrepareTemporaryTakeoffStateForSelection(unit, out string takeoffInfo);
            RegisterPerfTakeoffPrepDuration((Time.realtimeSinceStartupAsDouble - takeoffPerfStart) * 1000d);
            if (!string.IsNullOrWhiteSpace(takeoffInfo) && SensorLogGate.IsPodeDecolarEnabled())
                SensorLogGate.Log("PodeDecolar", takeoffInfo);

            replayManager?.EnsureCurrentUnitActionBuffer(unit, cursorCell);
            SetSelectedUnit(unit);
            ClearInspectedHelper();
            Advance(CursorState.UnitSelected, "HandleConfirmWhileNeutral: ally selected");
            DialogManager.Instance?.MarkHintLearned((TeamId)activeTeam, HelpHintId.Act);
            return ActionSfx.Confirm;
        }

        ConstructionManager construction = FindConstructionAtCell(cursorCell);
        if (construction == null)
            return ActionSfx.None;

        bool isConstructionAlly = (int)construction.TeamId == activeTeam;
        if (!isConstructionAlly)
        {
            BeginInspectedConstructionHelper(construction);
            Advance(CursorState.InspectingBuilding, "HandleConfirmFromNeutralLikeState: enemy construction inspect");
            return ActionSfx.Confirm;
        }

        if (TryEnterConstructionShoppingState(construction, activeTeam))
        {
            DialogManager.Instance?.MarkHintLearned((TeamId)activeTeam, HelpHintId.Produce);
            return ActionSfx.Confirm;
        }

        BeginInspectedConstructionHelper(construction);
        Advance(CursorState.InspectingBuilding, "HandleConfirmFromNeutralLikeState: ally construction inspect");
        return ActionSfx.Confirm;
    }

    private static void LogEnemyUnitInspection(UnitManager unit, int activeTeam)
    {
        if (unit == null)
            return;

        StringBuilder sb = new StringBuilder();
        string unitName = !string.IsNullOrWhiteSpace(unit.UnitDisplayName)
            ? unit.UnitDisplayName
            : (!string.IsNullOrWhiteSpace(unit.UnitId) ? unit.UnitId : unit.name);

        sb.Append("[Inspecao Inimigo] Unidade: ");
        sb.Append(unitName);
        sb.Append(" | Team: ");
        sb.Append(TeamUtils.GetName(unit.TeamId));
        sb.Append(" (");
        sb.Append((int)unit.TeamId);
        sb.Append(")");
        sb.Append(" | ActiveTeam: ");
        sb.Append(activeTeam);

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null || weapons.Count == 0)
        {
            sb.Append(" | Armas: nenhuma configurada");
            Debug.Log(sb.ToString());
            return;
        }

        sb.Append(" | Armas: ");
        bool addedAnyWeapon = false;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null)
                continue;

            string weaponName = embarked.weapon != null
                ? (!string.IsNullOrWhiteSpace(embarked.weapon.displayName) ? embarked.weapon.displayName : embarked.weapon.id)
                : $"arma_{i + 1}";

            if (addedAnyWeapon)
                sb.Append("; ");

            sb.Append(weaponName);
            sb.Append(" ammo=");
            sb.Append(Mathf.Max(0, embarked.squadAmmunition));
            addedAnyWeapon = true;
        }

        if (!addedAnyWeapon)
            sb.Append("nenhuma configurada");

        Debug.Log(sb.ToString());
    }

    private static void LogEnemyConstructionInspection(ConstructionManager construction, int activeTeam)
    {
        if (construction == null)
            return;

        string constructionName = !string.IsNullOrWhiteSpace(construction.ConstructionDisplayName)
            ? construction.ConstructionDisplayName
            : (!string.IsNullOrWhiteSpace(construction.ConstructionId) ? construction.ConstructionId : construction.name);

        StringBuilder sb = new StringBuilder();
        sb.Append($"[Inspecao Inimigo] Construcao: {constructionName} | Dono Atual: {TeamUtils.GetName(construction.TeamId)} ({(int)construction.TeamId}) | ");
        sb.Append($"Capture: {construction.CurrentCapturePoints}/{construction.CapturePointsMax} | ActiveTeam: {activeTeam}");
        AppendConstructionSupplyAndServiceDetails(sb, construction);
        Debug.Log(sb.ToString());
    }

    private static void LogAllyConstructionInspection(ConstructionManager construction, int activeTeam)
    {
        if (construction == null)
            return;

        string constructionName = !string.IsNullOrWhiteSpace(construction.ConstructionDisplayName)
            ? construction.ConstructionDisplayName
            : (!string.IsNullOrWhiteSpace(construction.ConstructionId) ? construction.ConstructionId : construction.name);

        StringBuilder sb = new StringBuilder();
        sb.Append($"[Inspecao Aliada] Construcao: {constructionName} | Dono Atual: {TeamUtils.GetName(construction.TeamId)} ({(int)construction.TeamId}) | ");
        sb.Append($"Capture: {construction.CurrentCapturePoints}/{construction.CapturePointsMax} | ActiveTeam: {activeTeam} | Sem unidades a venda.");
        AppendConstructionSupplyAndServiceDetails(sb, construction);
        Debug.Log(sb.ToString());
    }

    private static void AppendConstructionSupplyAndServiceDetails(StringBuilder sb, ConstructionManager construction)
    {
        if (sb == null || construction == null)
            return;

        IReadOnlyList<ConstructionSupplyOffer> offers = construction.OfferedSupplies;
        if (offers != null && offers.Count > 0)
        {
            sb.Append(" | Estoques: ");
            bool appendedAnyStock = false;
            for (int i = 0; i < offers.Count; i++)
            {
                ConstructionSupplyOffer offer = offers[i];
                if (offer == null || offer.supply == null)
                    continue;

                if (appendedAnyStock)
                    sb.Append("; ");

                string supplyName = ResolveSupplyDisplayName(offer.supply);
                string amount = construction.HasInfiniteSuppliesFor(offer.supply)
                    ? "INF"
                    : Mathf.Max(0, offer.quantity).ToString();
                sb.Append($"{supplyName}={amount}");
                appendedAnyStock = true;
            }

            if (!appendedAnyStock)
                sb.Append("nenhum");
        }
        else
        {
            sb.Append(" | Estoques: nenhum");
        }

        IReadOnlyList<ServiceData> services = construction.OfferedServices;
        if (services != null && services.Count > 0)
        {
            sb.Append(" | Servicos: ");
            bool appendedAnyService = false;
            for (int i = 0; i < services.Count; i++)
            {
                ServiceData service = services[i];
                if (service == null)
                    continue;

                if (appendedAnyService)
                    sb.Append("; ");

                if (service.serviceType == ServiceType.Transfer)
                {
                    sb.Append(ResolveConstructionTransferRoleLabel(construction));
                }
                else
                {
                    sb.Append(!string.IsNullOrWhiteSpace(service.displayName)
                        ? service.displayName
                        : (!string.IsNullOrWhiteSpace(service.id) ? service.id : service.name));
                }
                appendedAnyService = true;
            }

            if (!appendedAnyService)
                sb.Append("nenhum");
        }
        else
        {
            sb.Append(" | Servicos: nenhum");
        }
    }

    private static string ResolveConstructionTransferRoleLabel(ConstructionManager construction)
    {
        if (construction == null || !construction.TryResolveConstructionData(out ConstructionData data) || data == null || !data.isSupplier)
            return "Transferir - (indisponivel)";

        if (data.supplierTier == SupplierTier.Receiver)
            return "Transferir - Recebedor";
        if (data.supplierTier != SupplierTier.Hub)
            return "Transferir - (indisponivel)";
        if (construction.HasInfiniteSuppliesFor())
            return "Transferir - Fornecedor";
        return "Transferir - Recebedor/Fornecedor";
    }

    private ActionSfx HandleConfirmWhileUnitSelected()
    {
        LogStateStep("HandleConfirmWhileUnitSelected");
        if (cursorController == null || selectedUnit == null)
            return ActionSfx.None;

        Vector3Int cursorCell = cursorController.CurrentCell;
        Tilemap occupancyMap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;

        if (OccupancyResolver.IsLayerAwareRulesActive)
        {
            if (!OccupancyResolver.CanEndMove(selectedUnit, cursorCell, GetOccupantsAtCellForConfirm(cursorCell, selectedUnit, occupancyMap)))
            {
                PushPanelUnitMessage("Hex ocupado", 2.4f);
                Debug.Log("unidade selecionada, escolha um local valido para movimento");
                return ActionSfx.Error;
            }
        }
        else
        {
            if (UnitRulesDefinition.IsTotalWarEnabled() &&
                occupancyMap != null &&
                UnitRulesDefinition.IsUnitCellOccupiedForTeam(
                    occupancyMap,
                    cursorCell,
                    selectedUnit.TeamId,
                    selectedUnit))
            {
                string message = PanelDialogController.ResolveDialogMessage(
                    "hex.contested.occupied",
                    "Hex disputado: ja existe uma unidade no local");
                PushPanelUnitMessage(message, 2.4f);
                Debug.Log("movimento bloqueado: em hex disputado nao pode haver duas unidades do mesmo time");
                return ActionSfx.Error;
            }

            UnitManager unit = FindUnitAtCell(cursorCell);
            if (unit != null && unit != selectedUnit)
            {
                PushPanelUnitMessage("Hex ocupado", 2.4f);
                Debug.Log("unidade selecionada, escolha um local valido para movimento");
                return ActionSfx.Error;
            }
        }

        Vector3Int currentUnitCell = selectedUnit.CurrentCellPosition;
        currentUnitCell.z = 0;
        if (cursorCell == currentUnitCell)
        {
            if (!IsTakeoffMoveDistanceAllowed(0))
            {
                Debug.Log("Decolagem neste contexto nao permite permanecer em 0 hex.");
                return ActionSfx.Error;
            }

            EnterSensorsState(CursorState.MoveuParado);
            Debug.Log("moveu no mesmo lugar");
            return ActionSfx.Confirm;
        }

        if (selectedUnit.IsAircraftGrounded)
        {
            if (!TryPrepareAutomaticTakeoffForMovement(out string takeoffBlockReason))
            {
                if (!string.IsNullOrWhiteSpace(takeoffBlockReason))
                    Debug.Log(takeoffBlockReason);
                return ActionSfx.Error;
            }
        }

        if (!paintedRangeLookup.Contains(cursorCell))
            return ActionSfx.Error;

        if (!TryGetSelectedUnitPath(cursorCell, out List<Vector3Int> path))
            return ActionSfx.Error;
        if (!IsTakeoffMoveDistanceAllowed(path.Count - 1))
        {
            Debug.Log("Distancia invalida para o modo de decolagem disponivel neste contexto.");
            return ActionSfx.Error;
        }

        BeginMovementToSelectedCell(path);
        return ActionSfx.Confirm;
    }

    private IEnumerable<UnitManager> GetOccupantsAtCellForConfirm(Vector3Int cell, UnitManager exceptUnit, Tilemap occupancyMap)
    {
        cell.z = 0;
        List<UnitManager> units = UnitManager.AllActive;
        if (units == null || units.Count <= 0)
            yield break;

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked)
                continue;
            if (occupancyMap != null && (unit.BoardTilemap == null || unit.BoardTilemap != occupancyMap))
                continue;
            if (occupancyMap != null && unit.gameObject.scene != occupancyMap.gameObject.scene)
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell == cell)
                yield return unit;
        }
    }

    private bool TryPrepareAutomaticTakeoffForMovement(out string blockReason)
    {
        blockReason = string.Empty;
        if (selectedUnit == null || !selectedUnit.IsAircraftGrounded)
            return true;
        if (selectedUnit.CurrentFuel <= 0)
        {
            blockReason = "Autonomia insuficiente para decolagem.";
            return false;
        }
        if (hasTemporaryTakeoffSelectionState &&
            (temporaryTakeoffMoveOptions.Contains(0) || temporaryTakeoffMoveOptions.Contains(1)))
            return true;

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        AircraftOperationDecision decision = AircraftOperationRules.Evaluate(
            selectedUnit,
            boardMap,
            terrainDatabase,
            SensorMovementMode.MoveuParado);
        if (!decision.available || decision.action != AircraftOperationAction.Takeoff)
        {
            blockReason = string.IsNullOrWhiteSpace(decision.reason)
                ? "Aeronave em solo sem decolagem valida neste hex."
                : decision.reason;
            return false;
        }

        if (decision.consumesAction)
        {
            blockReason = "Decolagem neste hex consome acao. Use \"E\" para decolar parado.";
            return false;
        }

        if (!AircraftOperationRules.TryApplyOperation(
                selectedUnit,
                boardMap,
                terrainDatabase,
                SensorMovementMode.MoveuParado,
                out _))
        {
            blockReason = "Falha ao preparar decolagem automatica.";
            return false;
        }

        PaintSelectedUnitMovementRange();
        return true;
    }

    private ActionSfx HandleConfirmWhileMoveuAndando()
    {
        LogStateStep("HandleConfirmWhileMoveuAndando");
        return ActionSfx.None;
    }

    private ActionSfx HandleConfirmWhileMoveuParado()
    {
        LogStateStep("HandleConfirmWhileMoveuParado");
        return ActionSfx.None;
    }

    private ActionSfx HandleConfirmWhileCapturando()
    {
        LogStateStep("HandleConfirmWhileCapturando");
        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhileMoveuAndando()
    {
        LogStateStep("HandleCancelWhileMoveuAndando", rollback: true);
        RuntimeLog(
            $"[Rollback] ESC em fluxo andado (state={cursorState}) | hasCommittedMovement={hasCommittedMovement} | " +
            $"pathCount={committedMovementPath.Count} | selected={(selectedUnit != null ? selectedUnit.name : "(none)")}");

        if (HandleScannerPromptCancel())
        {
            RuntimeLog("[Rollback] ESC consumido por submenu/scanner prompt.");
            return ActionSfx.Cancel;
        }

        if (selectedUnit == null)
            return ActionSfx.Cancel;

        if (!hasCommittedMovement || committedMovementPath.Count < 2)
        {
            RuntimeLog("[Rollback] Sem caminho comprometido valido. Fallback para UnitSelected.");
            Retreat("HandleCancelWhileMoveuAndando: fallback without committed path");
            ClearSensorResults();
            PaintSelectedUnitMovementRange();
            return ActionSfx.Cancel;
        }

        RestorePreparedFuelCostIfAny();
        RestorePreparedMovementCostIfAny();
        if (!BeginRollbackToSelection())
        {
            RuntimeLog("[Rollback] Falha ao iniciar animacao de rollback. Fallback para UnitSelected.");
            Retreat("HandleCancelWhileMoveuAndando: rollback animation failed");
            ClearCommittedMovement();
            ClearSensorResults();
            PaintSelectedUnitMovementRange();
        }
        else
        {
            RuntimeLog("[Rollback] Animacao de rollback iniciada.");
        }
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleCancelWhileMoveuParado()
    {
        LogStateStep("HandleCancelWhileMoveuParado", rollback: true);
        RestoreForcedLayerAfterRollbackIfNeeded();
        Retreat("HandleCancelWhileMoveuParado");
        ClearSensorResults();
        PaintSelectedUnitMovementRange();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleCancelWhileCapturando()
    {
        LogStateStep("HandleCancelWhileCapturando", rollback: true);
        return ActionSfx.None;
    }

    private ActionSfx HandleConfirmWhileMirando()
    {
        LogStateStep("HandleConfirmWhileMirando");
        if (TryConfirmScannerAttack())
            return ActionSfx.Confirm;

        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhileMirando()
    {
        LogStateStep("HandleCancelWhileMirando", rollback: true);
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        ExitMirandoStateToMovement();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhilePousando()
    {
        LogStateStep("HandleConfirmWhilePousando");
        if (TryConfirmScannerLanding())
            return ActionSfx.Confirm;

        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhilePousando()
    {
        LogStateStep("HandleCancelWhilePousando", rollback: true);
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        ExitLandingStateToMovement();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhileEmbarcando()
    {
        LogStateStep("HandleConfirmWhileEmbarcando");
        if (TryConfirmScannerEmbark())
            return ActionSfx.Confirm;

        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhileEmbarcando()
    {
        LogStateStep("HandleCancelWhileEmbarcando", rollback: true);
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        ExitEmbarkStateToMovement();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhileDesembarcando()
    {
        LogStateStep("HandleConfirmWhileDesembarcando");
        if (TryConfirmScannerDisembark())
        {
            if (ConsumeDisembarkSuppressDefaultConfirmSfxOnce())
                return ActionSfx.None;
            return ActionSfx.Confirm;
        }
        return ActionSfx.None;
    }

    private ActionSfx HandleConfirmWhileShoppingAndServices()
    {
        LogStateStep("HandleConfirmWhileShoppingAndServices");
        return TryConfirmSelectedShoppingOption()
            ? ActionSfx.Confirm
            : ActionSfx.Error;
    }

    private ActionSfx HandleCancelWhileDesembarcando()
    {
        LogStateStep("HandleCancelWhileDesembarcando", rollback: true);
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        ExitDisembarkStateToMovement();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhileFundindo()
    {
        LogStateStep("HandleConfirmWhileFundindo");
        if (TryConfirmScannerMerge())
        {
            if (ConsumeMergeSuppressDefaultConfirmSfxOnce())
                return ActionSfx.None;
            return ActionSfx.Confirm;
        }
        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhileFundindo()
    {
        LogStateStep("HandleCancelWhileFundindo", rollback: true);
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        ExitMergeStateToMovement();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleCancelWhileShoppingAndServices()
    {
        LogStateStep("HandleCancelWhileShoppingAndServices", rollback: true);
        ExitConstructionShoppingStateToNeutral(rollback: true);
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleCancelWhileInspecting()
    {
        LogStateStep("HandleCancelWhileInspecting", rollback: true);
        ExitInspectStateToNeutral();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhileCommandService()
    {
        LogStateStep("HandleConfirmWhileCommandService");
        if (IsCommandServiceExecutionRunning)
            return ActionSfx.None;
        return TryConfirmPendingCommandServiceOrder()
            ? ActionSfx.Confirm
            : ActionSfx.Error;
    }

    private ActionSfx HandleCancelWhileCommandService()
    {
        LogStateStep("HandleCancelWhileCommandService", rollback: true);
        if (IsCommandServiceExecutionRunning)
            return ActionSfx.None;
        ExitCommandServiceStateToNeutral();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhileRemovingUnit()
    {
        LogStateStep("HandleConfirmWhileRemovingUnit");
        return TryConfirmRemovingUnit()
            ? ActionSfx.Confirm
            : ActionSfx.Error;
    }

    private ActionSfx HandleCancelWhileRemovingUnit()
    {
        LogStateStep("HandleCancelWhileRemovingUnit", rollback: true);
        ExitRemovingUnitStateToNeutral(logCanceled: true);
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhileEndingTurn()
    {
        LogStateStep("HandleConfirmWhileEndingTurn");
        return TryExecuteEndingTurnFromConfirmation(out _)
            ? ActionSfx.Confirm
            : ActionSfx.Error;
    }

    private ActionSfx HandleCancelWhileEndingTurn()
    {
        LogStateStep("HandleCancelWhileEndingTurn", rollback: true);
        return TryCancelEndingTurnConfirmation()
            ? ActionSfx.Cancel
            : ActionSfx.None;
    }

    private ActionSfx HandleConfirmWhileSuprindo()
    {
        LogStateStep("HandleConfirmWhileSuprindo");
        if (TryConfirmScannerSupply())
        {
            if (ConsumeSupplySuppressDefaultConfirmSfxOnce())
                return ActionSfx.None;
            return ActionSfx.Confirm;
        }

        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhileSuprindo()
    {
        LogStateStep("HandleCancelWhileSuprindo", rollback: true);
        if (HandleScannerPromptCancel())
            return ActionSfx.Cancel;

        ExitSupplyStateToMovement();
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleConfirmWhilePlanning()
    {
        LogStateStep("HandleConfirmWhilePlanning");
        return ActionSfx.None;
    }

    private ActionSfx HandleCancelWhilePlanning()
    {
        LogStateStep("HandleCancelWhilePlanning", rollback: true);
        ExitPlanningStateToNeutral(rollback: true);
        return ActionSfx.Cancel;
    }

    public bool TryOpenEndingTurnConfirmation(out string message)
    {
        message = string.Empty;
        if (cursorState != CursorState.Neutral)
        {
            message = $"Ending Turn exige cursor em Neutral (atual: {cursorState}).";
            return false;
        }

        Advance(CursorState.EndingTurn, "TryOpenEndingTurnConfirmation");
        PanelDialogController.TrySetExternalText("End Turn :: Confirm");
        return true;
    }

    public bool TryCancelEndingTurnConfirmation()
    {
        if (cursorState != CursorState.EndingTurn)
            return false;

        PanelDialogController.ClearExternalText();
        Retreat("TryCancelEndingTurnConfirmation");
        return true;
    }

    public bool TryExecuteEndingTurnFromConfirmation(out string message)
    {
        message = string.Empty;
        if (cursorState != CursorState.EndingTurn)
        {
            message = $"Ending Turn confirmation exige estado EndingTurn (atual: {cursorState}).";
            return false;
        }

        return TryExecuteEndingTurn("TryExecuteEndingTurnFromConfirmation", out message);
    }

    public bool TryExecuteEndingTurnFromMenu(out string message)
    {
        message = string.Empty;
        if (cursorState != CursorState.PlayerMenu && cursorState != CursorState.Neutral)
        {
            message = $"Passar a vez exige PlayerMenu/Neutral (atual: {cursorState}).";
            return false;
        }

        return TryExecuteEndingTurn("TryExecuteEndingTurnFromMenu", out message);
    }

    private bool TryExecuteEndingTurn(string reason, out string message)
    {
        message = string.Empty;
        if (matchController == null)
        {
            message = "MatchController ausente para passar a vez.";
            PanelDialogController.ClearExternalText();
            if (cursorState == CursorState.EndingTurn)
                Retreat($"{reason}: missing MatchController");
            return false;
        }

        if (cursorState != CursorState.EndingTurnExecuting)
            Advance(CursorState.EndingTurnExecuting, reason);

        PanelDialogController.ClearExternalText();
        matchController.AdvanceTurnWithTransition();
        ExecuteAndReset($"{reason}: dispatched");
        return true;
    }

    public bool TryEnterSavingState(out string message)
    {
        message = string.Empty;
        if (cursorState == CursorState.Saving)
            return true;

        if (cursorState != CursorState.Neutral && cursorState != CursorState.PlayerMenu)
        {
            message = $"Saving exige Neutral/PlayerMenu (atual: {cursorState}).";
            return false;
        }

        Advance(CursorState.Saving, "TryEnterSavingState");
        return true;
    }

    public bool TryEnterLoadingState(out string message)
    {
        message = string.Empty;
        if (cursorState == CursorState.Loading)
            return true;

        if (cursorState != CursorState.Neutral && cursorState != CursorState.PlayerMenu)
        {
            message = $"Loading exige Neutral/PlayerMenu (atual: {cursorState}).";
            return false;
        }

        Advance(CursorState.Loading, "TryEnterLoadingState");
        return true;
    }

    public void TryExitPersistencePromptState()
    {
        if (cursorState != CursorState.Saving && cursorState != CursorState.Loading)
            return;

        Retreat("TryExitPersistencePromptState");
    }

    public bool TryEnterPlayerMenuState()
    {
        if (cursorState != CursorState.Neutral)
            return false;

        Advance(CursorState.PlayerMenu, "TryEnterPlayerMenuState");
        return true;
    }

    public void TryExitPlayerMenuStateToNeutral()
    {
        if (cursorState != CursorState.PlayerMenu)
            return;

        Retreat("TryExitPlayerMenuStateToNeutral");
    }

    public bool TryEnterReplayState()
    {
        if (cursorState == CursorState.Replay)
            return true;

        if (cursorState != CursorState.Neutral)
            return false;

        Advance(CursorState.Replay, "TryEnterReplayState");
        return true;
    }

    public void TryExitReplayStateToNeutral()
    {
        if (cursorState != CursorState.Replay)
            return;

        Retreat("TryExitReplayStateToNeutral");
    }
}

