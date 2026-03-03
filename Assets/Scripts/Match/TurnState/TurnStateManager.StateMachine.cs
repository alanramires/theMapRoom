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

        switch (cursorState)
        {
            case CursorState.Neutral:
                return HandleConfirmWhileNeutral();
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
        }

        return ActionSfx.None;
    }

    public ActionSfx HandleCancel()
    {
        LogStateStep("HandleCancel", rollback: true);
        if (IsMovementAnimationRunning())
            return ActionSfx.None;

        switch (cursorState)
        {
            case CursorState.Neutral:
                return ActionSfx.None;
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
        }

        return ActionSfx.None;
    }

    private ActionSfx HandleConfirmWhileNeutral()
    {
        LogStateStep("HandleConfirmWhileNeutral");
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
                return ActionSfx.Confirm;
            }

            if (unit.HasActed)
            {
                Debug.Log($"debug: inspecionando aliado que ja agiu (unit={unit.name}, unitTeam={(int)unit.TeamId}, activeTeam={activeTeam}, hasActed={unit.HasActed})");
                return ActionSfx.Confirm;
            }

            TryPrepareTemporaryTakeoffStateForSelection(unit, out string takeoffInfo);
            if (!string.IsNullOrWhiteSpace(takeoffInfo))
                Debug.Log($"[Pode Decolar] {takeoffInfo}");

            SetSelectedUnit(unit);
            SetCursorState(CursorState.UnitSelected, "HandleConfirmWhileNeutral: ally selected");
            return ActionSfx.Confirm;
        }

        ConstructionManager construction = FindConstructionAtCell(cursorCell);
        if (construction == null)
            return ActionSfx.None;

        bool isConstructionAlly = (int)construction.TeamId == activeTeam;
        if (!isConstructionAlly)
        {
            LogEnemyConstructionInspection(construction, activeTeam);
            return ActionSfx.Confirm;
        }

        if (TryEnterConstructionShoppingState(construction, activeTeam))
            return ActionSfx.Confirm;

        LogAllyConstructionInspection(construction, activeTeam);
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

        Debug.Log(
            $"[Inspecao Inimigo] Construcao: {constructionName} | Team: {TeamUtils.GetName(construction.TeamId)} ({(int)construction.TeamId}) | " +
            $"Capture: {construction.CurrentCapturePoints}/{construction.CapturePointsMax} | ActiveTeam: {activeTeam}");
    }

    private static void LogAllyConstructionInspection(ConstructionManager construction, int activeTeam)
    {
        if (construction == null)
            return;

        string constructionName = !string.IsNullOrWhiteSpace(construction.ConstructionDisplayName)
            ? construction.ConstructionDisplayName
            : (!string.IsNullOrWhiteSpace(construction.ConstructionId) ? construction.ConstructionId : construction.name);

        Debug.Log(
            $"[Inspecao Aliada] Construcao: {constructionName} | Team: {TeamUtils.GetName(construction.TeamId)} ({(int)construction.TeamId}) | " +
            $"Capture: {construction.CurrentCapturePoints}/{construction.CapturePointsMax} | ActiveTeam: {activeTeam} | Sem unidades a venda.");
    }

    private ActionSfx HandleConfirmWhileUnitSelected()
    {
        LogStateStep("HandleConfirmWhileUnitSelected");
        if (cursorController == null || selectedUnit == null)
            return ActionSfx.None;

        Vector3Int cursorCell = cursorController.CurrentCell;
        UnitManager unit = FindUnitAtCell(cursorCell);
        if (unit != null && unit != selectedUnit)
        {
            PushPanelUnitMessage("Hex ocupado", 2.4f);
            Debug.Log("unidade selecionada, escolha um local valido para movimento");
            return ActionSfx.Error;
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

    private bool TryPrepareAutomaticTakeoffForMovement(out string blockReason)
    {
        blockReason = string.Empty;
        if (selectedUnit == null || !selectedUnit.IsAircraftGrounded)
            return true;
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
        Debug.Log(
            $"[Rollback] ESC em fluxo andado (state={cursorState}) | hasCommittedMovement={hasCommittedMovement} | " +
            $"pathCount={committedMovementPath.Count} | selected={(selectedUnit != null ? selectedUnit.name : "(none)")}");

        if (HandleScannerPromptCancel())
        {
            Debug.Log("[Rollback] ESC consumido por submenu/scanner prompt.");
            return ActionSfx.Cancel;
        }

        if (selectedUnit == null)
            return ActionSfx.Cancel;

        if (!hasCommittedMovement || committedMovementPath.Count < 2)
        {
            Debug.Log("[Rollback] Sem caminho comprometido valido. Fallback para UnitSelected.");
            SetCursorState(CursorState.UnitSelected, "HandleCancelWhileMoveuAndando: fallback without committed path", rollback: true);
            ClearSensorResults();
            PaintSelectedUnitMovementRange();
            return ActionSfx.Cancel;
        }

        RestorePreparedFuelCostIfAny();
        if (!BeginRollbackToSelection())
        {
            Debug.Log("[Rollback] Falha ao iniciar animacao de rollback. Fallback para UnitSelected.");
            SetCursorState(CursorState.UnitSelected, "HandleCancelWhileMoveuAndando: rollback animation failed", rollback: true);
            ClearCommittedMovement();
            ClearSensorResults();
            PaintSelectedUnitMovementRange();
        }
        else
        {
            Debug.Log("[Rollback] Animacao de rollback iniciada.");
        }
        return ActionSfx.Cancel;
    }

    private ActionSfx HandleCancelWhileMoveuParado()
    {
        LogStateStep("HandleCancelWhileMoveuParado", rollback: true);
        RestoreForcedLayerAfterRollbackIfNeeded();
        SetCursorState(CursorState.UnitSelected, "HandleCancelWhileMoveuParado", rollback: true);
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
        LogConstructionShoppingPanel();
        return ActionSfx.None;
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
}
