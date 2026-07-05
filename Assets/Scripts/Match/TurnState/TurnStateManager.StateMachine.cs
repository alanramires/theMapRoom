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

        switch (CurrentCursorState)
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

    // Page Up / Page Down em UnitSelected: alterna entre unidades aliadas prontas
    // que dividem o mesmo hex (coabitacao aereo+terrestre), sem mover o cursor.
    public ActionSfx HandleCycleSelectionWithinHex(int direction)
    {
        if (IsMovementAnimationRunning())
            return ActionSfx.None;

        return TryCycleSelectionWithinHex(direction) ? ActionSfx.Confirm : ActionSfx.None;
    }

    // Page Up / Page Down anchored on a single hex. The cycle spans every selectable ally unit
    // AND the hex's shoppable ally construction (appended at the end), so the player can rotate
    // between, e.g., the helicopter in the air, the tank on the ground, and the factory's shop.
    // Crossing onto the construction leaves UnitSelected for ShoppingAndServices and vice versa.
    private bool TryCycleSelectionWithinHex(int direction)
    {
        if (direction == 0)
            return false;

        Vector3Int cell;
        bool currentIsConstruction;
        if (CurrentCursorState == CursorState.UnitSelected && selectedUnit != null)
        {
            cell = selectedUnit.CurrentCellPosition;
            currentIsConstruction = false;
        }
        else if (CurrentCursorState == CursorState.ShoppingAndServices && shoppingConstruction != null)
        {
            cell = shoppingConstruction.CurrentCellPosition;
            currentIsConstruction = true;
        }
        else
        {
            return false;
        }
        cell.z = 0;

        int activeTeam = matchController != null ? matchController.ActiveTeamId : -1;
        List<UnitManager> units = CollectSelectableAlliesAtCell(cell);
        // When already in the shop, the construction is definitionally part of the cycle (we got
        // here); otherwise it joins only when reachable (ally, shoppable, cell not blocked).
        ConstructionManager construction = currentIsConstruction
            ? shoppingConstruction
            : ResolveCyclableConstructionAtCell(cell, activeTeam);
        bool hasConstruction = construction != null;

        // Construction occupies the slot right after the units.
        int total = units.Count + (hasConstruction ? 1 : 0);
        if (total <= 1)
            return false;

        int currentIndex;
        if (currentIsConstruction)
            currentIndex = units.Count;
        else
        {
            currentIndex = units.IndexOf(selectedUnit);
            if (currentIndex < 0)
                return false;
        }

        int step = direction >= 0 ? 1 : -1;
        int nextIndex = (currentIndex + step + total) % total;
        if (nextIndex == currentIndex)
            return false;

        bool nextIsConstruction = hasConstruction && nextIndex == units.Count;

        if (!currentIsConstruction && !nextIsConstruction)
        {
            // Unit -> unit: swap in place, staying in UnitSelected.
            UnitManager next = units[nextIndex];
            if (next == null || next == selectedUnit)
                return false;
            ReselectUnitInPlace(next, cell);
            return true;
        }

        if (!currentIsConstruction && nextIsConstruction)
        {
            // Unit -> construction: drop the unit selection and open the shop.
            ClearSelectionAndReturnToNeutral();
            return TryEnterConstructionShoppingState(construction, activeTeam);
        }

        // Construction -> unit: close the shop and select the unit.
        UnitManager nextUnit = units[nextIndex];
        if (nextUnit == null)
            return false;
        ExitConstructionShoppingStateToNeutral(rollback: false);
        return SelectUnitFromNeutral(nextUnit, cell);
    }

    // The hex's ally construction is part of the cycle only when it can actually be shopped
    // (mirrors the conditions in TryEnterConstructionShoppingState), matching the default ENTER.
    private ConstructionManager ResolveCyclableConstructionAtCell(Vector3Int cell, int activeTeam)
    {
        if (activeTeam < 0)
            return null;

        ConstructionManager construction = FindConstructionAtCell(cell);
        if (construction == null || (int)construction.TeamId != activeTeam)
            return null;

        // The shop is unreachable while a ground/surface (blocking-band) unit sits on the
        // construction — a new unit cannot be produced onto an occupied cell. Mirror the default
        // ENTER rule (TryEnterUnblockedConstructionShoppingBeforeUnit) and keep it out of the loop.
        if (HasBlockingUnitOnConstructionCell(cell))
            return null;

        // A landed aircraft that only took off temporarily for selection is still really grounded
        // here — it returns to the ground once the selection is dropped, so the cell stays
        // occupied and the shop must not open. An aircraft that was already airborne does not set
        // this state and can reach the shop.
        if (IsTemporarySelectionTakeoffGroundedOnCell(cell))
            return null;

        if (!construction.CanProduceUnitsForTeam((TeamId)activeTeam))
            return null;

        IReadOnlyList<UnitData> offered = construction.OfferedUnits;
        if (offered == null || offered.Count == 0)
            return null;

        return construction;
    }

    // True when the active selection-takeoff lifted an originally-grounded aircraft off this cell.
    // Its airborne state is temporary, so for occupancy purposes it still blocks the cell.
    private bool IsTemporarySelectionTakeoffGroundedOnCell(Vector3Int cell)
    {
        if (!hasTemporaryTakeoffSelectionState
            || temporaryTakeoffUnit == null
            || !temporaryTakeoffOriginalGrounded)
            return false;

        Vector3Int takeoffCell = temporaryTakeoffUnit.CurrentCellPosition;
        takeoffCell.z = 0;
        cell.z = 0;
        return takeoffCell == cell;
    }

    // Selects an ally unit starting from Neutral, mirroring the selection branch of
    // HandleConfirmFromNeutralLikeState (takeoff prep, action buffer, advance to UnitSelected).
    private bool SelectUnitFromNeutral(UnitManager unit, Vector3Int cell)
    {
        if (unit == null)
            return false;

        cell.z = 0;
        int activeTeam = matchController != null ? matchController.ActiveTeamId : -1;

        double takeoffPerfStart = Time.realtimeSinceStartupAsDouble;
        TryPrepareTemporaryTakeoffStateForSelection(unit, out string takeoffInfo);
        RegisterPerfTakeoffPrepDuration((Time.realtimeSinceStartupAsDouble - takeoffPerfStart) * 1000d);
        if (!string.IsNullOrWhiteSpace(takeoffInfo) && SensorLogGate.IsPodeDecolarEnabled())
            SensorLogGate.Log("PodeDecolar", takeoffInfo);

        replayManager?.EnsureCurrentUnitActionBuffer(unit, cell);
        SetSelectedUnit(unit);
        ClearInspectedHelper();
        Advance(CursorState.UnitSelected, "SelectUnitFromNeutral: cycle to unit within hex");
        if (activeTeam >= 0)
            DialogManager.Instance?.MarkHintLearned((TeamId)activeTeam, HelpHintId.Act);
        return true;
    }

    // Unidades aliadas do time ativo, prontas (nao agiram), visiveis e ancoradas no hex.
    // Ordenadas por InstanceId para um ciclo estavel e previsivel.
    private List<UnitManager> CollectSelectableAlliesAtCell(Vector3Int cell)
    {
        List<UnitManager> result = new List<UnitManager>();
        int activeTeam = matchController != null ? matchController.ActiveTeamId : -1;
        if (activeTeam < 0)
            return result;

        Tilemap referenceTilemap = terrainTilemap != null
            ? terrainTilemap
            : (selectedUnit != null
                ? selectedUnit.BoardTilemap
                : (cursorController != null ? cursorController.BoardTilemap : null));
        if (referenceTilemap == null)
            return result;

        cell.z = 0;
        List<UnitManager> occupants = UnitOccupancyRules.GetUnitsAtCell(referenceTilemap, cell);
        for (int i = 0; i < occupants.Count; i++)
        {
            UnitManager u = occupants[i];
            if (u == null || u.IsEmbarked || u.IsDead)
                continue;
            if ((int)u.TeamId != activeTeam)
                continue;
            if (u.HasActed)
                continue;
            if (matchController != null && !matchController.IsUnitVisibleForActiveTeam(u))
                continue;

            result.Add(u);
        }

        result.Sort((a, b) => a.InstanceId.CompareTo(b.InstanceId));
        return result;
    }

    // Troca a unidade ancorada permanecendo em UnitSelected, espelhando a sequencia
    // de selecao do estado Neutral (desfaz o preparo da anterior antes de preparar a nova).
    private void ReselectUnitInPlace(UnitManager unit, Vector3Int cell)
    {
        if (unit == null)
            return;

        RestoreTemporaryTakeoffSelectionStateIfAny();
        RestorePreparedFuelCostIfAny();
        RestorePreparedMovementCostIfAny();
        ClearCommittedPathVisual();
        ClearSensorResults();

        double takeoffPerfStart = Time.realtimeSinceStartupAsDouble;
        TryPrepareTemporaryTakeoffStateForSelection(unit, out string takeoffInfo);
        RegisterPerfTakeoffPrepDuration((Time.realtimeSinceStartupAsDouble - takeoffPerfStart) * 1000d);
        if (!string.IsNullOrWhiteSpace(takeoffInfo) && SensorLogGate.IsPodeDecolarEnabled())
            SensorLogGate.Log("PodeDecolar", takeoffInfo);

        replayManager?.EnsureCurrentUnitActionBuffer(unit, cell);
        SetSelectedUnit(unit);
        ClearInspectedHelper();
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

        switch (CurrentCursorState)
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
        if (CurrentCursorState != CursorState.Neutral)
            ExecuteAndReset("HandleConfirmFromNeutralLikeState: normalize");

        if (cursorController == null)
            return ActionSfx.None;

        Vector3Int cursorCell = cursorController.CurrentCell;
        int activeTeam = matchController != null ? matchController.ActiveTeamId : -1;

        if (TryEnterUnblockedConstructionShoppingBeforeUnit(cursorCell, activeTeam))
            return ActionSfx.Confirm;

        UnitManager unit = FindUnitAtCell(cursorCell);
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

    private bool TryEnterUnblockedConstructionShoppingBeforeUnit(Vector3Int cursorCell, int activeTeam)
    {
        ConstructionManager construction = FindConstructionAtCell(cursorCell);
        if (construction == null || activeTeam < 0 || (int)construction.TeamId != activeTeam)
            return false;

        // A unidade visivel no hex e sempre o alvo primario da confirmacao. A loja
        // continua acessivel pelo ciclo de selecao quando as regras permitirem.
        if (FindUnitAtCell(cursorCell) != null)
            return false;

        if (!TryEnterConstructionShoppingState(construction, activeTeam))
            return false;

        DialogManager.Instance?.MarkHintLearned((TeamId)activeTeam, HelpHintId.Produce);
        return true;
    }

    private bool HasBlockingUnitOnConstructionCell(Vector3Int cell)
    {
        Tilemap referenceTilemap = terrainTilemap != null
            ? terrainTilemap
            : (cursorController != null ? cursorController.BoardTilemap : null);

        if (referenceTilemap == null)
            return false;

        cell.z = 0;

        List<UnitManager> units = UnitOccupancyRules.GetUnitsAtCell(referenceTilemap, cell);
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;

            if (!OccupancyResolver.IsLayerAwareRulesActive)
                return true;

            if (OccupancyResolver.GetHeightBand(unit) == HeightBand.Blocking)
                return true;
        }

        return false;
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
            if (!CanSelectedUnitEndMoveAtCell(cursorCell, GetOccupantsAtCellForConfirm(cursorCell, selectedUnit, occupancyMap)))
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
                string reason = "Decolagem nao autorizada: este hex exige corrida, nao da pra decolar parado.";
                PushPanelUnitMessage(reason, 2.6f);
                Debug.Log(reason);
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
                if (string.IsNullOrWhiteSpace(takeoffBlockReason))
                    takeoffBlockReason = "Decolagem nao autorizada neste hex.";
                PushPanelUnitMessage(takeoffBlockReason, 2.6f);
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
            string reason = "Decolagem nao autorizada: distancia invalida para a decolagem neste hex.";
            PushPanelUnitMessage(reason, 2.6f);
            Debug.Log(reason);
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
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked || unit.IsDead)
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

    private bool CanSelectedUnitEndMoveAtCell(Vector3Int cell, IEnumerable<UnitManager> occupants)
    {
        if (selectedUnit == null)
            return false;

        Vector3Int unitCell = selectedUnit.CurrentCellPosition;
        unitCell.z = 0;
        cell.z = 0;

        bool projectsToAir =
            selectedUnit.GetDomain() == Domain.Air && !selectedUnit.IsAircraftGrounded;

        if (!projectsToAir && selectedUnit.IsAircraftGrounded && cell != unitCell)
            projectsToAir = true;

        if (projectsToAir)
        {
            HeightLevel finalHeight = selectedUnit.GetDomain() == Domain.Air
                ? selectedUnit.GetHeightLevel()
                : HeightLevel.AirLow;
            return OccupancyResolver.CanEndMoveAsLayer(selectedUnit, Domain.Air, finalHeight, occupants);
        }

        return OccupancyResolver.CanEndMove(selectedUnit, cell, occupants);
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
            SensorMovementMode.MoveuParado,
            allowSameTeamAirBlockerForMovementTakeoff: true);
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
                out _,
                allowSameTeamAirBlockerForMovementTakeoff: true))
        {
            blockReason = "Falha ao preparar decolagem automatica.";
            return false;
        }

        hasPendingTookOffRecentlyCommit = true;
        pendingTookOffRecentlyUnit = selectedUnit;
        PaintSelectedUnitMovementRange();
        return true;
    }

    private ActionSfx HandleConfirmWhileMoveuAndando()
    {
        LogStateStep("HandleConfirmWhileMoveuAndando");
        if (SensorOptionCancelFocused)
            return HandleCancelWhileMoveuAndando();
        return TryInvokeFocusedSensorOption() ? ActionSfx.Confirm : ActionSfx.None;
    }

    private ActionSfx HandleConfirmWhileMoveuParado()
    {
        LogStateStep("HandleConfirmWhileMoveuParado");
        if (SensorOptionCancelFocused)
            return HandleCancelWhileMoveuParado();
        return TryInvokeFocusedSensorOption() ? ActionSfx.Confirm : ActionSfx.None;
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
            $"[Rollback] ESC em fluxo andado (state={CurrentCursorState}) | hasCommittedMovement={hasCommittedMovement} | " +
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
        if (mirandoCancelFocused || (IsMirandoConfirmStep && mirandoConfirmButtonFocus == 1))
            return HandleCancelWhileMirando();
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
        if (embarkCancelFocused || (IsEmbarkConfirmStep && embarkConfirmButtonFocus == 1))
            return HandleCancelWhileEmbarcando();
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
        if (IsDisembarkPassengerSelectStep)
        {
            if (DisembarkPassengerCancelFocused)
                return HandleCancelWhileDesembarcando();
            return TryInvokeFocusedDisembarkPassengerOption() ? ActionSfx.Confirm : ActionSfx.None;
        }
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
        // Enter com o slot CANCELAR em foco sai da loja em vez de comprar.
        if (ShoppingCancelFocused)
            return HandleCancelWhileShoppingAndServices();
        if (!TryConfirmSelectedShoppingOption())
            return ActionSfx.Error;
        if (ConsumeShoppingSuppressDefaultConfirmSfxOnce())
            return ActionSfx.None;
        return ActionSfx.Confirm;
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
        // Enter respeita o botao em foco: com CANCELAR destacado, cancela em vez de executar.
        if (commandServicePreviewFocusIndex == CommandServicePreviewCancelIndex)
            return HandleCancelWhileCommandService();
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
        if (removingUnitFocusIndex == RemovingUnitCancelFocusIndex)
            return HandleCancelWhileRemovingUnit();
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
        if (CurrentCursorState != CursorState.Neutral)
        {
            message = $"Ending Turn exige cursor em Neutral (atual: {CurrentCursorState}).";
            return false;
        }

        Advance(CursorState.EndingTurn, "TryOpenEndingTurnConfirmation");
        PanelDialogController.TrySetExternalText("End Turn :: Confirm");
        return true;
    }

    public bool TryCancelEndingTurnConfirmation()
    {
        if (CurrentCursorState != CursorState.EndingTurn)
            return false;

        PanelDialogController.ClearExternalText();
        Retreat("TryCancelEndingTurnConfirmation");
        return true;
    }

    public bool TryExecuteEndingTurnFromConfirmation(out string message)
    {
        message = string.Empty;
        if (CurrentCursorState != CursorState.EndingTurn)
        {
            message = $"Ending Turn confirmation exige estado EndingTurn (atual: {CurrentCursorState}).";
            return false;
        }

        return TryExecuteEndingTurn("TryExecuteEndingTurnFromConfirmation", out message);
    }

    public bool TryExecuteEndingTurnFromMenu(out string message)
    {
        message = string.Empty;
        if (CurrentCursorState != CursorState.PlayerMenu && CurrentCursorState != CursorState.Neutral)
        {
            message = $"Passar a vez exige PlayerMenu/Neutral (atual: {CurrentCursorState}).";
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
            if (CurrentCursorState == CursorState.EndingTurn)
                Retreat($"{reason}: missing MatchController");
            return false;
        }

        if (CurrentCursorState != CursorState.EndingTurnExecuting)
            Advance(CursorState.EndingTurnExecuting, reason);

        PanelDialogController.ClearExternalText();
        matchController.AdvanceTurnWithTransition();
        ExecuteAndReset($"{reason}: dispatched");
        return true;
    }

    public bool TryEnterSavingState(out string message)
    {
        message = string.Empty;
        if (CurrentCursorState == CursorState.Saving)
            return true;

        if (CurrentCursorState != CursorState.Neutral && CurrentCursorState != CursorState.PlayerMenu)
        {
            message = $"Saving exige Neutral/PlayerMenu (atual: {CurrentCursorState}).";
            return false;
        }

        Advance(CursorState.Saving, "TryEnterSavingState");
        return true;
    }

    public bool TryEnterLoadingState(out string message)
    {
        message = string.Empty;
        if (CurrentCursorState == CursorState.Loading)
            return true;

        if (CurrentCursorState != CursorState.Neutral && CurrentCursorState != CursorState.PlayerMenu)
        {
            message = $"Loading exige Neutral/PlayerMenu (atual: {CurrentCursorState}).";
            return false;
        }

        Advance(CursorState.Loading, "TryEnterLoadingState");
        return true;
    }

    public void TryExitPersistencePromptState()
    {
        if (CurrentCursorState != CursorState.Saving && CurrentCursorState != CursorState.Loading)
            return;

        Retreat("TryExitPersistencePromptState");
    }

    public bool TryEnterPlayerMenuState()
    {
        if (CurrentCursorState != CursorState.Neutral)
            return false;

        Advance(CursorState.PlayerMenu, "TryEnterPlayerMenuState");
        return true;
    }

    public void TryExitPlayerMenuStateToNeutral()
    {
        if (CurrentCursorState != CursorState.PlayerMenu)
            return;

        Retreat("TryExitPlayerMenuStateToNeutral");
        // Saída canônica do menu do jogador para Neutral → retoma a IA pausada pelo menu (pause de
        // jogador). Idempotente: se a IA não estava em pause de jogador, é no-op.
        AIController.Instance?.SetPlayerPaused(false);
    }

    public bool TryEnterReplayState()
    {
        if (CurrentCursorState == CursorState.Replay)
            return true;

        if (CurrentCursorState != CursorState.Neutral)
            return false;

        Advance(CursorState.Replay, "TryEnterReplayState");
        return true;
    }

    public void TryExitReplayStateToNeutral()
    {
        if (CurrentCursorState != CursorState.Replay)
            return;

        Retreat("TryExitReplayStateToNeutral");
    }
}

