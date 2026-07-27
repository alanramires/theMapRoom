using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private sealed class TransferEstimateLine
    {
        public SupplyData supply;
        public int moved;
        public int sourceBefore;
        public int sourceAfter;
        public int destinationBefore;
        public int destinationAfter;
    }

    private readonly List<PodeTransferirOption> transferPromptOptions = new List<PodeTransferirOption>();
    private readonly Dictionary<Vector3Int, int> transferPromptIndexByCell = new Dictionary<Vector3Int, int>();
    private readonly List<TransferEstimateLine> transferPreviewLines = new List<TransferEstimateLine>();
    private int transferPromptSelectedIndex = -1;
    private bool transferPromptSelectionPending;
    private bool transferPromptDonationPercentagePending;
    private bool transferPromptConfirmationPending;
    private static readonly int[] TransferDonationPercentOptions = { 25, 50, 75, 100 };
    private int transferDonationPercentIndex = 3;
    private bool transferExecutionInProgress;
    private readonly List<LineRenderer> transferPreviewRenderers = new List<LineRenderer>();
    private readonly List<Vector3> transferPreviewPathPoints = new List<Vector3>(2);
    private readonly List<Vector3> transferPreviewSegmentPoints = new List<Vector3>(8);
    private float transferPreviewPathLength;
    private float transferPreviewHeadDistance;
    private bool transferPromptTemporarilyHidCommittedPath;
    private int transferHelperFocusIndex;

    public bool IsTransferSelectionStep => IsTransferSelectionStepActive();
    public bool IsTransferDonationPercentageStep => IsTransferDonationPercentageStepActive();
    public bool IsTransferHelperActive => IsTransferPromptActive();
    public int TransferHelperFocusIndex => transferHelperFocusIndex;
    public bool TransferHelperCancelFocused => IsTransferPromptActive() &&
        (IsTransferConfirmStepActive() ? transferHelperFocusIndex == 1 :
         IsTransferDonationPercentageStepActive() ? transferHelperFocusIndex == TransferDonationPercentOptions.Length :
         transferHelperFocusIndex == transferPromptOptions.Count);

    public bool NavigateTransferHelperFocus(int delta)
    {
        if (!IsTransferPromptActive() || delta == 0)
            return false;
        int total = IsTransferConfirmStepActive() ? 2 :
            IsTransferDonationPercentageStepActive() ? TransferDonationPercentOptions.Length + 1 :
            transferPromptOptions.Count + 1;
        transferHelperFocusIndex = (transferHelperFocusIndex + (delta > 0 ? 1 : -1) + total) % total;
        if (IsTransferDonationPercentageStepActive() && transferHelperFocusIndex < TransferDonationPercentOptions.Length)
            transferDonationPercentIndex = transferHelperFocusIndex;
        if (IsTransferSelectionStepActive() && transferHelperFocusIndex < transferPromptOptions.Count)
        {
            transferPromptSelectedIndex = transferHelperFocusIndex;
            FocusTransferOptionByIndex(transferPromptSelectedIndex, playSfx: false);
        }
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    public bool TryInvokeFocusedTransferOption()
    {
        if (!IsTransferPromptActive() || TransferHelperCancelFocused)
            return false;
        if (IsTransferConfirmStepActive())
            return TryConfirmPendingTransferPrompt();
        if (IsTransferDonationPercentageStepActive())
            return TrySelectTransferDonationPercentageFromPointer(transferHelperFocusIndex);
        return TrySelectTransferOptionFromPointer(transferHelperFocusIndex);
    }

    public bool TrySelectTransferOptionFromPointer(int index)
    {
        if (!IsTransferSelectionStepActive() || index < 0 || index >= transferPromptOptions.Count)
            return false;
        transferPromptSelectedIndex = index;
        transferHelperFocusIndex = index;
        FocusTransferOptionByIndex(index, playSfx: false);
        EnterTransferStepAfterTargetSelection();
        cursorController?.PlayConfirmSfx();
        return true;
    }

    public bool TrySelectTransferDonationPercentageFromPointer(int index)
    {
        if (!IsTransferDonationPercentageStepActive() || index < 0 || index >= TransferDonationPercentOptions.Length)
            return false;
        transferDonationPercentIndex = index;
        transferHelperFocusIndex = index;
        EnterTransferConfirmStep();
        cursorController?.PlayConfirmSfx();
        return true;
    }

    public bool TryConfirmTransferFromPointer()
    {
        if (!IsTransferConfirmStepActive())
            return false;
        transferHelperFocusIndex = 0;
        return TryConfirmPendingTransferPrompt();
    }

    private void HandleTransferActionRequested()
    {
        if (transferExecutionInProgress)
        {
            Debug.Log("[Transfer] Aguarde o fim da execucao atual.");
            return;
        }

        bool canTransfer = availableSensorActionCodes.Contains('T');
        if (!canTransfer || cachedPodeTransferirTargets.Count == 0)
        {
            string reason = string.IsNullOrWhiteSpace(cachedPodeTransferirReason)
                ? "sem opcoes validas agora."
                : cachedPodeTransferirReason;
            Debug.Log($"Pode Transferir (\"T\"): {reason}");
            LogScannerPanel();
            return;
        }

        transferPromptOptions.Clear();
        for (int i = 0; i < cachedPodeTransferirTargets.Count; i++)
        {
            PodeTransferirOption option = cachedPodeTransferirTargets[i];
            if (option == null)
                continue;
            transferPromptOptions.Add(option);
        }

        if (transferPromptOptions.Count <= 0)
        {
            Debug.Log("Pode Transferir (\"T\"): sem opcoes validas agora.");
            LogScannerPanel();
            return;
        }

        SortClockwiseAroundUnit(transferPromptOptions, o => o.targetCell, selectedUnit);
        replayManager?.UpdateCurrentBufferSensorAction(SensorActionType.Transfer, "TransferActionRequested");
        EnterTransferSelectionStep();
        cursorController?.PlayConfirmSfx();
    }

    private void ProcessTransferPromptInput()
    {
        if (!IsTransferSelectionStepActive())
            return;

        if (!TryReadPressedDigitIncludingZero(out int number))
            return;
        if (number <= 0)
            return;

        // Regra principal: escolheu uma opcao numerada valida, vai direto para a tela final.
        int optionCount = transferPromptOptions.Count;
        if (number >= 1 && number <= optionCount)
        {
            transferPromptSelectedIndex = number - 1;
            FocusTransferOptionByIndex(transferPromptSelectedIndex, playSfx: true);
            EnterTransferStepAfterTargetSelection();
            return;
        }

        cursorController?.PlayErrorSfx();
        Debug.Log($"[Transfer] Opcao invalida: {number}.");
    }

    private void UpdateTransferPromptPreview()
    {
        bool shouldShow = !ShouldSuppressAiActionPreviewLines() &&
                          IsTransferPromptActive() &&
                          !transferExecutionInProgress &&
                          selectedUnit != null &&
                          transferPromptSelectedIndex >= 0 &&
                          transferPromptSelectedIndex < transferPromptOptions.Count &&
                          !IsTransferSameHexOrEmbarkedCollection(selectedUnit);
        if (!shouldShow)
        {
            SetTransferPreviewVisible(false);
            return;
        }

        PodeTransferirOption option = transferPromptOptions[transferPromptSelectedIndex];
        if (option == null)
        {
            SetTransferPreviewVisible(false);
            return;
        }

        ResolveTransferEndpoints(option, selectedUnit, out UnitManager sourceUnit, out ConstructionManager sourceConstruction, out UnitManager destinationUnit, out ConstructionManager destinationConstruction);
        Vector3 from = sourceUnit != null ? sourceUnit.transform.position : (sourceConstruction != null ? sourceConstruction.transform.position : selectedUnit.transform.position);
        Vector3 to = destinationUnit != null ? destinationUnit.transform.position : (destinationConstruction != null ? destinationConstruction.transform.position : selectedUnit.transform.position);
        from.z = to.z;

        if (Vector3.Distance(from, to) <= 0.01f)
        {
            SetTransferPreviewVisible(false);
            return;
        }

        transferPreviewPathPoints.Clear();
        transferPreviewPathPoints.Add(from);
        transferPreviewPathPoints.Add(to);
        transferPreviewPathLength = ComputePathLength(transferPreviewPathPoints);
        if (transferPreviewPathLength <= 0.0001f)
        {
            SetTransferPreviewVisible(false);
            return;
        }

        int segmentQuantities = Mathf.Max(1, GetMergeQueuePreviewSegmentQuantities());
        float previewMultiplier = GetMergeQueuePreviewMultiplier();
        float speed = Mathf.Max(0.2f, GetMergeQueuePreviewSegmentSpeed());
        float spacingMultiplier = Mathf.Max(0.2f, GetMergeQueuePreviewSegmentSpacingMultiplier());
        float segmentLen = Mathf.Max(0.08f, GetMirandoPreviewSegmentLength() * previewMultiplier);
        float width = Mathf.Max(0.025f, GetMirandoPreviewWidth() * 0.85f);
        Color baseColor = GetMirandoPreviewColor();
        Color color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(baseColor.a * 0.85f));

        EnsureTransferPreviewRenderers(segmentQuantities);
        if (transferPreviewRenderers.Count == 0)
            return;

        float cycleLen = transferPreviewPathLength + segmentLen;
        transferPreviewHeadDistance += speed * Time.deltaTime;
        if (transferPreviewHeadDistance > cycleLen)
            transferPreviewHeadDistance = 0f;

        float spacing = (cycleLen / segmentQuantities) * spacingMultiplier;
        for (int segmentIndex = 0; segmentIndex < segmentQuantities; segmentIndex++)
        {
            if (segmentIndex >= transferPreviewRenderers.Count)
                break;

            LineRenderer renderer = transferPreviewRenderers[segmentIndex];
            if (renderer == null)
                continue;

            float segmentHeadDistance = transferPreviewHeadDistance - (spacing * segmentIndex);
            while (segmentHeadDistance < 0f)
                segmentHeadDistance += cycleLen;
            while (segmentHeadDistance > cycleLen)
                segmentHeadDistance -= cycleLen;

            float startDist = Mathf.Max(0f, segmentHeadDistance - segmentLen);
            float endDist = Mathf.Min(segmentHeadDistance, transferPreviewPathLength);
            if (endDist <= startDist + 0.0001f)
            {
                renderer.positionCount = 0;
                renderer.enabled = false;
                continue;
            }

            BuildPathSegmentPointsFrom(transferPreviewPathPoints, startDist, endDist, transferPreviewSegmentPoints);
            if (transferPreviewSegmentPoints.Count < 2)
            {
                renderer.positionCount = 0;
                renderer.enabled = false;
                continue;
            }

            renderer.startWidth = width;
            renderer.endWidth = width;
            renderer.startColor = color;
            renderer.endColor = color;
            renderer.positionCount = transferPreviewSegmentPoints.Count;
            for (int p = 0; p < transferPreviewSegmentPoints.Count; p++)
                renderer.SetPosition(p, transferPreviewSegmentPoints[p]);
            renderer.enabled = true;
        }
    }

    private bool IsTransferPromptActive()
    {
        return (transferPromptSelectionPending || transferPromptDonationPercentagePending || transferPromptConfirmationPending) &&
               transferPromptOptions.Count > 0 &&
               (CurrentCursorState == CursorState.MoveuAndando || CurrentCursorState == CursorState.MoveuParado);
    }

    private bool IsTransferSelectionStepActive()
    {
        return IsTransferPromptActive() && transferPromptSelectionPending &&
               !transferPromptDonationPercentagePending && !transferPromptConfirmationPending;
    }

    private bool IsTransferDonationPercentageStepActive()
    {
        return IsTransferPromptActive() && transferPromptDonationPercentagePending &&
               !transferPromptSelectionPending && !transferPromptConfirmationPending;
    }

    private bool IsTransferConfirmStepActive()
    {
        return IsTransferPromptActive() && transferPromptConfirmationPending;
    }

    private bool TryConfirmPendingTransferPrompt()
    {
        if (!IsTransferPromptActive())
            return false;

        if (selectedUnit == null)
        {
            ClearPendingTransferPrompt();
            Debug.Log("[Transfer] Cancelado: unidade selecionada ausente.");
            return true;
        }

        if (IsTransferSelectionStepActive())
        {
            if (transferPromptSelectedIndex < 0 || transferPromptSelectedIndex >= transferPromptOptions.Count)
            {
                if (!TrySelectTransferOptionFromCursor())
                {
                    Debug.Log("[Transfer] Selecione um destino valido por numero ou cursor.");
                    cursorController?.PlayErrorSfx();
                    return true;
                }
            }

            EnterTransferStepAfterTargetSelection();
            cursorController?.PlayConfirmSfx();
            return true;
        }

        if (IsTransferDonationPercentageStepActive())
        {
            transferDonationPercentIndex = Mathf.Clamp(transferHelperFocusIndex, 0, TransferDonationPercentOptions.Length - 1);
            EnterTransferConfirmStep();
            cursorController?.PlayConfirmSfx();
            return true;
        }

        if (!IsTransferConfirmStepActive())
            return true;
        if (transferExecutionInProgress)
            return true;

        int index = Mathf.Clamp(transferPromptSelectedIndex, 0, transferPromptOptions.Count - 1);
        PodeTransferirOption option = transferPromptOptions[index];
        if (option == null)
        {
            ClearPendingTransferPrompt();
            Debug.Log("[Transfer] Cancelado: opcao invalida.");
            return true;
        }

        replayManager?.UpdateCurrentBufferTarget(
            option.targetUnit,
            option.targetConstruction,
            option.targetCell,
            "TransferTargetConfirm");
        StartCoroutine(ExecuteTransferPromptSequence(option));
        return true;
    }

    private IEnumerator ExecuteTransferPromptSequence(PodeTransferirOption option)
    {
        transferExecutionInProgress = true;
        try
        {
            if (!TryEstimateTransferOption(option, selectedUnit, GetSelectedTransferDonationPercent(), out _, out _, out _))
            {
                cursorController?.PlayErrorSfx();
                Debug.Log("[Transfer] Contexto mudou: nao ha mais estoque/capacidade para concluir.");
                yield break;
            }

            if (option != null
                && option.requiresSupplierLanding
                && !CanApplyRequiredTransferLanding(
                    selectedUnit,
                    option.landingDomain,
                    option.landingHeight,
                    option.landingMovementMode,
                    out string supplierLandingReason))
            {
                cursorController?.PlayErrorSfx();
                Debug.Log(
                    $"[Transfer] Pouso preparatorio de " +
                    $"{selectedUnit?.name} deixou de ser valido: " +
                    supplierLandingReason);
                yield break;
            }

            UnitManager landingTarget =
                option != null ? option.targetUnit : null;
            if (option != null
                && option.requiresTargetUnitLanding
                && !CanApplyRequiredTransferLanding(
                    landingTarget,
                    option.targetLandingDomain,
                    option.targetLandingHeight,
                    option.targetLandingMovementMode,
                    out string targetLandingReason))
            {
                cursorController?.PlayErrorSfx();
                Debug.Log(
                    $"[Transfer] Pouso preparatorio de " +
                    $"{landingTarget?.name} deixou de ser valido: " +
                    targetLandingReason);
                yield break;
            }

            if (option != null && option.requiresSupplierLanding)
            {
                bool supplierLanded = false;
                yield return ApplyRequiredTransferLanding(
                    selectedUnit,
                    option.landingDomain,
                    option.landingHeight,
                    option.landingMovementMode,
                    success => supplierLanded = success);
                if (!supplierLanded)
                    yield break;
            }

            if (option != null && option.requiresTargetUnitLanding)
            {
                bool targetLanded = false;
                yield return ApplyRequiredTransferLanding(
                    landingTarget,
                    option.targetLandingDomain,
                    option.targetLandingHeight,
                    option.targetLandingMovementMode,
                    success => targetLanded = success);
                if (!targetLanded)
                    yield break;
            }

            bool executed = TryExecuteTransferOptionRuntime(option, selectedUnit, GetSelectedTransferDonationPercent(), out int movedTotal, out string message, out Dictionary<SupplyData, int> movedBySupply);
            if (executed)
            {
                yield return PlayTransferSupplyProjectiles(option, selectedUnit, movedBySupply);
                cursorController?.PlayDoneSfx();
                Debug.Log($"[Transfer] {message}");
                TryFinalizeSelectedUnitActionFromDebug();
            }
            else
            {
                cursorController?.PlayErrorSfx();
                Debug.Log($"[Transfer] {message}");
            }
        }
        finally
        {
            transferExecutionInProgress = false;
            ClearPendingTransferPrompt();
        }
    }

    private bool CanApplyRequiredTransferLanding(
        UnitManager aircraft,
        Domain plannedDomain,
        HeightLevel plannedHeight,
        SensorMovementMode movementMode,
        out string reason)
    {
        reason = string.Empty;
        if (aircraft == null)
        {
            reason = "aeronave ausente";
            return false;
        }

        Tilemap boardMap =
            terrainTilemap != null
                ? terrainTilemap
                : aircraft.BoardTilemap;
        Vector3Int cell = aircraft.CurrentCellPosition;
        cell.z = 0;
        PodePousarReport report = PodePousarSensor.Evaluate(
            aircraft,
            boardMap,
            terrainDatabase,
            movementMode,
            useManualRemainingMovement: false,
            manualRemainingMovement: 0,
            atCell: cell);
        if (report == null || !report.status)
        {
            reason = report != null
                ? report.explicacao
                : "PodePousar sem resultado";
            return false;
        }

        if (report.landingDomain != plannedDomain
            || report.landingHeight != plannedHeight)
        {
            reason =
                $"pouso agora resulta em " +
                $"{report.landingDomain}/{report.landingHeight}, " +
                $"mas a transferencia planejou " +
                $"{plannedDomain}/{plannedHeight}";
            return false;
        }

        return true;
    }

    private IEnumerator ApplyRequiredTransferLanding(
        UnitManager aircraft,
        Domain plannedDomain,
        HeightLevel plannedHeight,
        SensorMovementMode movementMode,
        System.Action<bool> completed)
    {
        if (aircraft == null)
        {
            completed?.Invoke(false);
            yield break;
        }

        Tilemap boardMap =
            terrainTilemap != null
                ? terrainTilemap
                : aircraft.BoardTilemap;
        PlayMovementStartSfx(aircraft);
        bool startedHigh =
            aircraft.GetDomain() == Domain.Air
            && aircraft.GetHeightLevel() ==
                HeightLevel.AirHigh;
        if (startedHigh)
        {
            float highToLowDuration =
                GetEmbarkAirHighToGroundDuration()
                * Mathf.Clamp01(
                    GetEmbarkHighToLowNormalizedTime());
            if (highToLowDuration > 0f)
                yield return new WaitForSeconds(
                    highToLowDuration);
            aircraft.TrySetCurrentLayerMode(
                Domain.Air,
                HeightLevel.AirLow);
        }

        float landingDuration =
            GetLayerOperationTransitionDuration();
        if (landingDuration > 0f)
            yield return new WaitForSeconds(
                landingDuration);

        if (!AircraftOperationRules.TryApplyOperation(
                aircraft,
                boardMap,
                terrainDatabase,
                movementMode,
                out AircraftOperationDecision appliedLanding)
            || appliedLanding.action !=
                AircraftOperationAction.Land
            || aircraft.GetDomain() != plannedDomain
            || aircraft.GetHeightLevel() != plannedHeight)
        {
            cursorController?.PlayErrorSfx();
            Debug.Log(
                $"[Transfer] Falha ao pousar " +
                $"{aircraft.name} em " +
                $"{plannedDomain}/{plannedHeight}; " +
                "transferencia cancelada.");
            completed?.Invoke(false);
            yield break;
        }

        float landingFxDuration =
            animationManager != null
                ? animationManager.PlayVtolLandingEffect(
                    aircraft)
                : 0f;
        if (landingFxDuration > 0f)
            yield return new WaitForSeconds(
                landingFxDuration);

        Debug.Log(
            $"[Transfer] {aircraft.name} pousou em " +
            $"{plannedDomain}/{plannedHeight} para " +
            "transferir e permanecera pousado.");
        completed?.Invoke(true);
    }

    private bool TryCancelPendingTransferPrompt()
    {
        if (!IsTransferPromptActive())
            return false;

        if (IsTransferConfirmStepActive())
        {
            PodeTransferirOption selectedOption = transferPromptSelectedIndex >= 0 &&
                transferPromptSelectedIndex < transferPromptOptions.Count
                ? transferPromptOptions[transferPromptSelectedIndex]
                : null;
            if (selectedOption != null && selectedOption.flowMode == TransferFlowMode.Fornecimento)
            {
                transferPromptConfirmationPending = false;
                transferPromptDonationPercentagePending = true;
                transferHelperFocusIndex = transferDonationPercentIndex;
                transferPreviewLines.Clear();
                PanelDialogController.TrySetExternalText("Transferir :: escolha quanto doar");
                return true;
            }

            if (transferPromptOptions.Count <= 1)
            {
                ClearPendingTransferPrompt();
                Debug.Log("[Transfer] Cancelado.");
                return true;
            }

            transferPromptConfirmationPending = false;
            transferPromptSelectionPending = true;
            transferPreviewLines.Clear();
            LogTransferPromptOptions();
            Debug.Log("[Transfer] Confirmacao cancelada. Retornando para selecao.");
            return true;
        }

        if (IsTransferDonationPercentageStepActive())
        {
            if (transferPromptOptions.Count <= 1)
            {
                ClearPendingTransferPrompt();
                Debug.Log("[Transfer] Cancelado.");
                return true;
            }

            transferPromptDonationPercentagePending = false;
            transferPromptSelectionPending = true;
            transferHelperFocusIndex = Mathf.Clamp(transferPromptSelectedIndex, 0, transferPromptOptions.Count - 1);
            LogTransferPromptOptions();
            return true;
        }

        ClearPendingTransferPrompt();
        Debug.Log("[Transfer] Cancelado.");
        return true;
    }

    public bool TryExecuteAutomatedTransferReplayOrder(string targetInstanceId, Vector3Int targetCell)
    {
        if (!IsTransferPromptActive() || transferExecutionInProgress)
            return false;

        int selectedIndex = -1;
        for (int i = 0; i < transferPromptOptions.Count; i++)
        {
            PodeTransferirOption option = transferPromptOptions[i];
            if (option == null)
                continue;

            bool idMatch = !string.IsNullOrWhiteSpace(targetInstanceId)
                && option.targetUnit != null
                && option.targetUnit.InstanceId.ToString() == targetInstanceId;

            Vector3Int optionCell = option.targetCell;
            optionCell.z = 0;
            Vector3Int desiredCell = targetCell;
            desiredCell.z = 0;
            bool cellMatch = optionCell == desiredCell;

            if (!idMatch && !cellMatch)
                continue;

            selectedIndex = i;
            break;
        }

        if (selectedIndex < 0)
            return false;

        transferPromptSelectedIndex = selectedIndex;
        FocusTransferOptionByIndex(transferPromptSelectedIndex, playSfx: false);

        if (IsTransferSelectionStepActive())
        {
            if (!TryConfirmPendingTransferPrompt())
                return false;
        }

        if (IsTransferDonationPercentageStepActive())
        {
            transferDonationPercentIndex = TransferDonationPercentOptions.Length - 1;
            transferHelperFocusIndex = transferDonationPercentIndex;
            EnterTransferConfirmStep();
        }

        if (IsTransferConfirmStepActive())
            return TryConfirmPendingTransferPrompt();

        return transferExecutionInProgress || !IsTransferPromptActive();
    }

    private void ClearPendingTransferPrompt()
    {
        TryRestoreCommittedPathAfterTransferPrompt();
        transferPromptSelectionPending = false;
        transferPromptDonationPercentagePending = false;
        transferPromptConfirmationPending = false;
        transferPromptSelectedIndex = -1;
        transferDonationPercentIndex = TransferDonationPercentOptions.Length - 1;
        transferHelperFocusIndex = 0;
        transferPromptOptions.Clear();
        transferPromptIndexByCell.Clear();
        transferPreviewLines.Clear();
        transferPreviewPathPoints.Clear();
        transferPreviewSegmentPoints.Clear();
        transferPreviewPathLength = 0f;
        transferPreviewHeadDistance = 0f;
        SetTransferPreviewVisible(false);
        PanelDialogController.ClearExternalText();
    }

    private void EnterTransferSelectionStep()
    {
        TryHideCommittedPathForTransferPrompt();
        transferPromptSelectionPending = true;
        transferPromptDonationPercentagePending = false;
        transferPromptConfirmationPending = false;
        transferPromptSelectedIndex = transferPromptOptions.Count > 0 ? 0 : -1;
        transferHelperFocusIndex = 0;
        transferPreviewLines.Clear();
        RebuildTransferCellIndex();
        TrySelectTransferOptionFromCursor();
        if (transferPromptOptions.Count == 1)
        {
            EnterTransferStepAfterTargetSelection();
            return;
        }

        LogTransferPromptOptions();
    }

    private void TryHideCommittedPathForTransferPrompt()
    {
        transferPromptTemporarilyHidCommittedPath = false;
        if (!hasCommittedMovement || committedMovementPath == null || committedMovementPath.Count < 2)
            return;

        ClearCommittedPathVisual();
        transferPromptTemporarilyHidCommittedPath = true;
    }

    private void TryRestoreCommittedPathAfterTransferPrompt()
    {
        if (!transferPromptTemporarilyHidCommittedPath)
            return;

        transferPromptTemporarilyHidCommittedPath = false;
        bool canRestoreInMovementSensors = CurrentCursorState == CursorState.MoveuAndando || CurrentCursorState == CursorState.MoveuParado;
        if (!canRestoreInMovementSensors)
            return;
        if (!hasCommittedMovement || committedMovementPath == null || committedMovementPath.Count < 2)
            return;

        DrawCommittedPathVisual(committedMovementPath);
    }

    private void EnterTransferConfirmStep()
    {
        if (transferPromptSelectedIndex < 0 || transferPromptSelectedIndex >= transferPromptOptions.Count)
            return;

        transferPromptSelectionPending = false;
        transferPromptDonationPercentagePending = false;
        transferPromptConfirmationPending = true;
        transferHelperFocusIndex = 0;
        RebuildTransferPreviewLines();
        PodeTransferirOption selectedOption = transferPromptOptions[transferPromptSelectedIndex];
        Dictionary<string, string> tokens = BuildTransferDialogTokens(selectedOption, transferPromptSelectedIndex + 1);
        string confirmText = PanelDialogController.ResolveDialogMessage(
            "transfer.prompt.confirm_prefix",
            "Transferir :: Confirmar <label>",
            tokens);
        PanelDialogController.TrySetExternalText(confirmText);
    }

    private void EnterTransferStepAfterTargetSelection()
    {
        if (transferPromptSelectedIndex < 0 || transferPromptSelectedIndex >= transferPromptOptions.Count)
            return;

        PodeTransferirOption option = transferPromptOptions[transferPromptSelectedIndex];
        if (option != null && option.flowMode == TransferFlowMode.Fornecimento)
        {
            transferPromptSelectionPending = false;
            transferPromptDonationPercentagePending = true;
            transferPromptConfirmationPending = false;
            transferDonationPercentIndex = TransferDonationPercentOptions.Length - 1;
            transferHelperFocusIndex = transferDonationPercentIndex;
            transferPreviewLines.Clear();
            PanelDialogController.TrySetExternalText("Transferir :: escolha quanto doar");
            return;
        }

        EnterTransferConfirmStep();
    }

    private int GetSelectedTransferDonationPercent()
    {
        int index = Mathf.Clamp(transferDonationPercentIndex, 0, TransferDonationPercentOptions.Length - 1);
        return TransferDonationPercentOptions[index];
    }

    private void LogTransferPromptOptions()
    {
        if (!IsTransferPromptActive())
            return;

        if (IsTransferConfirmStepActive())
        {
            int selected = Mathf.Clamp(transferPromptSelectedIndex, 0, transferPromptOptions.Count - 1);
            string selectedLabel = ResolveTransferOptionLabel(transferPromptOptions[selected], selected + 1);
            Debug.Log($"[Transfer] Confirmar: {selectedLabel}. Enter=executar | ESC=voltar");
            return;
        }

        int selectedIndex = Mathf.Clamp(transferPromptSelectedIndex, 0, transferPromptOptions.Count - 1);
        Debug.Log($"Pode Transferir (\"T\"): {transferPromptOptions.Count} opcao(oes) valida(s).");
        for (int i = 0; i < transferPromptOptions.Count; i++)
        {
            string label = ResolveTransferOptionLabel(transferPromptOptions[i], i + 1);
            string marker = i == selectedIndex ? ">" : " ";
            Debug.Log($"{marker} {label}");
        }

        Dictionary<string, string> tokens = BuildTransferDialogTokens(
            transferPromptOptions[selectedIndex],
            selectedIndex + 1);
        PanelDialogController.TrySetExternalText(PanelDialogController.ResolveDialogMessage(
            "transfer.prompt.select_number",
            "Transferir :: escolha numero + Enter",
            tokens));
    }

    private string ResolveTransferSupplierDisplayName()
    {
        if (selectedUnit == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(selectedUnit.UnitDisplayName))
            return selectedUnit.UnitDisplayName;

        return selectedUnit.name;
    }

    private Dictionary<string, string> BuildTransferDialogTokens(PodeTransferirOption option, int oneBasedIndex)
    {
        string transferType = ResolveTransferTypeLabel(option);
        string label = ResolveTransferOptionLabel(option, oneBasedIndex);
        Dictionary<string, string> tokens = new Dictionary<string, string>
        {
            { "unit", ResolveTransferSupplierDisplayName() },
            { "transfer type", transferType },
            { "transfer_type", transferType },
            { "transfer_type.selecionado", transferType },
            { "type", transferType },
            { "label", label },
            { "index", Mathf.Max(1, oneBasedIndex).ToString() }
        };
        return tokens;
    }

    private static string ResolveTransferTypeLabel(PodeTransferirOption option)
    {
        if (option == null)
            return string.Empty;

        if (option.flowMode == TransferFlowMode.Fornecimento)
        {
            return PanelDialogController.ResolveDialogMessage(
                "transfer_type.doar",
                "Doar");
        }

        return PanelDialogController.ResolveDialogMessage(
            "transfer_type.receber",
            "Receber");
    }

    private bool TryResolveTransferCursorMove(Vector3Int currentCell, Vector3Int inputDelta, out Vector3Int resolvedCell)
    {
        resolvedCell = currentCell;
        if (!IsTransferSelectionStepActive() || transferPromptOptions.Count <= 1)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        if (transferPromptSelectedIndex < 0 || transferPromptSelectedIndex >= transferPromptOptions.Count)
            transferPromptSelectedIndex = 0;

        int nextIndex = (transferPromptSelectedIndex + step + transferPromptOptions.Count) % transferPromptOptions.Count;
        transferPromptSelectedIndex = nextIndex;
        FocusTransferOptionByIndex(nextIndex, playSfx: false);
        resolvedCell = ResolveTransferOptionCell(transferPromptOptions[nextIndex]);
        return true;
    }

    private void RebuildTransferCellIndex()
    {
        transferPromptIndexByCell.Clear();
        for (int i = 0; i < transferPromptOptions.Count; i++)
        {
            Vector3Int cell = ResolveTransferOptionCell(transferPromptOptions[i]);
            cell.z = 0;
            if (!transferPromptIndexByCell.ContainsKey(cell))
                transferPromptIndexByCell[cell] = i;
        }
    }

    private bool TrySelectTransferOptionFromCursor()
    {
        if (cursorController == null)
            return false;

        Vector3Int cursorCell = cursorController.CurrentCell;
        cursorCell.z = 0;

        int foundIndex = -1;
        int matches = 0;
        for (int i = 0; i < transferPromptOptions.Count; i++)
        {
            Vector3Int optionCell = ResolveTransferOptionCell(transferPromptOptions[i]);
            optionCell.z = 0;
            if (optionCell != cursorCell)
                continue;

            matches++;
            if (foundIndex < 0)
                foundIndex = i;
        }

        if (matches == 1 && foundIndex >= 0)
        {
            transferPromptSelectedIndex = foundIndex;
            return true;
        }

        return false;
    }

    private void FocusTransferOptionByIndex(int index, bool playSfx)
    {
        if (index < 0 || index >= transferPromptOptions.Count)
            return;

        Vector3Int cell = ResolveTransferOptionCell(transferPromptOptions[index]);
        cell.z = 0;
        cursorController?.SetCell(cell, playMoveSfx: false);
        if (playSfx)
            cursorController?.PlayConfirmSfx();
    }

    private static Vector3Int ResolveTransferOptionCell(PodeTransferirOption option)
    {
        if (option == null)
            return Vector3Int.zero;
        Vector3Int cell = option.targetCell;
        cell.z = 0;
        return cell;
    }

    private void EnsureTransferPreviewRenderers(int count)
    {
        int desired = Mathf.Max(1, count);
        while (transferPreviewRenderers.Count < desired)
        {
            int segmentIndex = transferPreviewRenderers.Count;
            GameObject go = new GameObject($"TransferConfirmPreviewLine_{segmentIndex + 1}");
            go.transform.SetParent(transform, false);
            LineRenderer renderer = go.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.numCapVertices = 2;
            renderer.numCornerVertices = 2;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material previewMaterial = GetMirandoPreviewMaterial();
            renderer.material = previewMaterial != null ? previewMaterial : new Material(Shader.Find("Sprites/Default"));
            int sortingLayerId = GetMirandoPreviewSortingLayerId();
            if (sortingLayerId != 0)
                renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = Mathf.Max(0, GetMirandoPreviewSortingOrder() - 1);
            renderer.enabled = false;
            transferPreviewRenderers.Add(renderer);
        }
    }

    private void SetTransferPreviewVisible(bool visible)
    {
        visible = visible && !ShouldSuppressAiActionPreviewLines();
        for (int i = 0; i < transferPreviewRenderers.Count; i++)
        {
            LineRenderer renderer = transferPreviewRenderers[i];
            if (renderer == null)
                continue;

            if (!visible)
            {
                renderer.positionCount = 0;
                renderer.enabled = false;
                continue;
            }

            renderer.enabled = true;
        }
    }

    private static bool IsTransferSameHexOrEmbarkedCollection(UnitManager supplier)
    {
        if (supplier == null)
            return false;
        if (!supplier.TryGetUnitData(out UnitData data) || data == null)
            return false;
        return data.collectionRange == SupplierRangeMode.SameHexOrEmbarked;
    }

    private bool HasMultipleTransferTargetCells()
    {
        Vector3Int? first = null;
        for (int i = 0; i < transferPromptOptions.Count; i++)
        {
            Vector3Int cell = ResolveTransferOptionCell(transferPromptOptions[i]);
            if (!first.HasValue)
            {
                first = cell;
                continue;
            }

            if (first.Value != cell)
                return true;
        }

        return false;
    }

    private string ResolveTransferOptionLabel(PodeTransferirOption option, int oneBasedIndex)
    {
        if (option == null)
            return $"{oneBasedIndex}. (invalido)";
        string label = !string.IsNullOrWhiteSpace(option.displayLabel)
            ? option.displayLabel
            : option.flowMode.ToString();
        return $"{oneBasedIndex}. {label}";
    }

    private void RebuildTransferPreviewLines()
    {
        transferPreviewLines.Clear();
        if (selectedUnit == null)
            return;
        if (transferPromptSelectedIndex < 0 || transferPromptSelectedIndex >= transferPromptOptions.Count)
            return;

        PodeTransferirOption option = transferPromptOptions[transferPromptSelectedIndex];
        if (option == null)
            return;

        if (!TryEstimateTransferOption(option, selectedUnit, GetSelectedTransferDonationPercent(), out Dictionary<SupplyData, int> sourceStock, out Dictionary<SupplyData, int> destinationStock, out Dictionary<SupplyData, int> movedBySupply))
            return;

        foreach (KeyValuePair<SupplyData, int> pair in movedBySupply)
        {
            SupplyData supply = pair.Key;
            if (supply == null)
                continue;

            int moved = Mathf.Max(0, pair.Value);
            int sourceBefore = sourceStock.TryGetValue(supply, out int srcBefore) ? Mathf.Max(0, srcBefore) : 0;
            int destinationBefore = destinationStock.TryGetValue(supply, out int dstBefore) ? Mathf.Max(0, dstBefore) : 0;
            transferPreviewLines.Add(new TransferEstimateLine
            {
                supply = supply,
                moved = moved,
                sourceBefore = sourceBefore,
                sourceAfter = Mathf.Max(0, sourceBefore - moved),
                destinationBefore = destinationBefore,
                destinationAfter = destinationBefore >= int.MaxValue || moved >= int.MaxValue
                    ? int.MaxValue
                    : destinationBefore + moved
            });
        }
    }

    private static bool TryEstimateTransferOption(
        PodeTransferirOption option,
        UnitManager supplier,
        int donationPercent,
        out Dictionary<SupplyData, int> sourceStock,
        out Dictionary<SupplyData, int> destinationStock,
        out Dictionary<SupplyData, int> movedBySupply)
    {
        sourceStock = new Dictionary<SupplyData, int>();
        destinationStock = new Dictionary<SupplyData, int>();
        movedBySupply = new Dictionary<SupplyData, int>();
        if (option == null || supplier == null)
            return false;

        ResolveTransferEndpoints(option, supplier, out UnitManager sourceUnit, out ConstructionManager sourceConstruction, out UnitManager destinationUnit, out ConstructionManager destinationConstruction);
        if (sourceUnit == null && sourceConstruction == null)
            return false;

        sourceStock = sourceUnit != null ? ReadUnitStockMap(sourceUnit) : ReadConstructionStockMap(sourceConstruction);
        if (sourceStock.Count <= 0)
            return false;

        destinationStock = destinationUnit != null ? ReadUnitStockMap(destinationUnit) : ReadConstructionStockMap(destinationConstruction);
        Dictionary<SupplyData, int> destinationCapacity = destinationUnit != null ? ReadUnitCapacityMap(destinationUnit) : null;

        foreach (KeyValuePair<SupplyData, int> pair in sourceStock)
        {
            SupplyData supply = pair.Key;
            int available = Mathf.Max(0, pair.Value);
            if (supply == null || available <= 0)
                continue;

            int transferable = available;
            if (option.flowMode == TransferFlowMode.Fornecimento)
                transferable = ResolveDonationAmount(available, donationPercent);
            if (destinationUnit != null)
            {
                int capacity = destinationCapacity != null && destinationCapacity.TryGetValue(supply, out int maxCap) ? Mathf.Max(0, maxCap) : 0;
                int current = destinationStock != null && destinationStock.TryGetValue(supply, out int currentDst) ? Mathf.Max(0, currentDst) : 0;
                int remaining = Mathf.Max(0, capacity - current);
                transferable = Mathf.Min(transferable, remaining);

                // Limite hard no runtime real da unidade (evita qualquer overflow por divergencia de snapshot).
                int liveRemaining = GetUnitRemainingCapacityForSupply(destinationUnit, supply);
                transferable = Mathf.Min(transferable, liveRemaining);
            }

            if (transferable <= 0)
                continue;
            movedBySupply[supply] = transferable;
        }

        return movedBySupply.Count > 0;
    }

    private IEnumerator PlayTransferSupplyProjectiles(PodeTransferirOption option, UnitManager supplier, Dictionary<SupplyData, int> movedBySupply)
    {
        if (option == null || supplier == null || movedBySupply == null || movedBySupply.Count <= 0)
            yield break;
        if (animationManager == null)
            yield break;

        ResolveTransferEndpoints(option, supplier, out UnitManager sourceUnit, out ConstructionManager sourceConstruction, out UnitManager destinationUnit, out ConstructionManager destinationConstruction);
        Vector3 sourcePos = sourceUnit != null ? sourceUnit.transform.position : (sourceConstruction != null ? sourceConstruction.transform.position : supplier.transform.position);
        Vector3 destinationPos = destinationUnit != null ? destinationUnit.transform.position : (destinationConstruction != null ? destinationConstruction.transform.position : supplier.transform.position);

        float spawnInterval = GetSupplySpawnInterval();
        float flightPadding = GetSupplyFlightPadding();
        bool renderAboveFog = matchController != null
            && matchController.ShouldPromoteActiveAiActionFxAboveFog();

        foreach (KeyValuePair<SupplyData, int> pair in movedBySupply)
        {
            SupplyData supply = pair.Key;
            int moved = Mathf.Max(0, pair.Value);
            if (supply == null || moved <= 0)
                continue;

            float duration = animationManager.PlayServiceProjectileStraight(
                sourcePos,
                destinationPos,
                supply.spriteDefault,
                renderAboveFog);
            if (spawnInterval > 0f)
                yield return new WaitForSeconds(spawnInterval);
            if (duration > 0f)
                yield return new WaitForSeconds(duration + flightPadding);
        }
    }

    private bool TryExecuteTransferOptionRuntime(PodeTransferirOption option, UnitManager supplier, int donationPercent, out int movedTotal, out string message, out Dictionary<SupplyData, int> movedBySupply)
    {
        movedTotal = 0;
        movedBySupply = new Dictionary<SupplyData, int>();
        message = "Falha ao executar transferencia.";
        if (option == null || supplier == null)
        {
            message = "Contexto de transferencia invalido.";
            return false;
        }

        ResolveTransferEndpoints(option, supplier, out UnitManager sourceUnit, out ConstructionManager sourceConstruction, out UnitManager destinationUnit, out ConstructionManager destinationConstruction);
        if (sourceUnit == null && sourceConstruction == null)
        {
            message = "Origem da transferencia invalida.";
            return false;
        }

        Dictionary<SupplyData, int> sourceStock = sourceUnit != null
            ? ReadUnitStockMap(sourceUnit)
            : ReadConstructionStockMap(sourceConstruction);
        if (sourceStock.Count <= 0)
        {
            message = "Origem sem estoque para transferir.";
            return false;
        }

        Dictionary<SupplyData, int> destinationStock = destinationUnit != null
            ? ReadUnitStockMap(destinationUnit)
            : ReadConstructionStockMap(destinationConstruction);
        Dictionary<SupplyData, int> destinationCapacity = destinationUnit != null
            ? ReadUnitCapacityMap(destinationUnit)
            : null;

        foreach (KeyValuePair<SupplyData, int> pair in sourceStock)
        {
            SupplyData supply = pair.Key;
            int available = Mathf.Max(0, pair.Value);
            if (supply == null || available <= 0)
                continue;

            int transferable = available;
            if (option.flowMode == TransferFlowMode.Fornecimento)
                transferable = ResolveDonationAmount(available, donationPercent);
            if (destinationUnit != null)
            {
                int capacity = destinationCapacity != null && destinationCapacity.TryGetValue(supply, out int maxCap) ? Mathf.Max(0, maxCap) : 0;
                int current = destinationStock != null && destinationStock.TryGetValue(supply, out int currentDst) ? Mathf.Max(0, currentDst) : 0;
                int remaining = Mathf.Max(0, capacity - current);
                transferable = Mathf.Min(transferable, remaining);
            }

            if (transferable <= 0)
                continue;

            int consumed = sourceUnit != null
                ? ConsumeFromUnit(sourceUnit, supply, transferable)
                : ConsumeFromConstruction(sourceConstruction, supply, transferable);
            if (consumed <= 0)
                continue;

            int added = destinationUnit != null
                ? AddToUnit(destinationUnit, supply, consumed)
                : AddToConstruction(destinationConstruction, supply, consumed);
            if (added <= 0)
                continue;

            movedTotal += Mathf.Max(0, added);
            movedBySupply[supply] = movedBySupply.TryGetValue(supply, out int existing) ? existing + added : added;
        }

        if (movedTotal <= 0)
        {
            message = "Nenhum supply foi transferido (capacidade/estoque).";
            return false;
        }

        string flowLabel = option.flowMode == TransferFlowMode.Fornecimento ? "Doar" : "Receber";
        message = $"{flowLabel} concluido. Movido={movedTotal}.";
        return true;
    }

    private static int ResolveDonationAmount(int available, int donationPercent)
    {
        available = Mathf.Max(0, available);
        if (available <= 0)
            return 0;
        int percent = Mathf.Clamp(donationPercent, 1, 100);
        long proportional = ((long)available * percent) / 100L;
        return Mathf.Clamp((int)proportional, 1, available);
    }

    private static void ResolveTransferEndpoints(
        PodeTransferirOption option,
        UnitManager supplier,
        out UnitManager sourceUnit,
        out ConstructionManager sourceConstruction,
        out UnitManager destinationUnit,
        out ConstructionManager destinationConstruction)
    {
        sourceUnit = null;
        sourceConstruction = null;
        destinationUnit = null;
        destinationConstruction = null;

        if (option == null || supplier == null)
            return;

        if (option.flowMode == TransferFlowMode.Fornecimento)
        {
            sourceUnit = supplier;
            destinationUnit = option.targetUnit;
            destinationConstruction = option.targetConstruction;
            return;
        }

        destinationUnit = supplier;
        sourceUnit = option.targetUnit;
        sourceConstruction = option.targetConstruction;
    }

    private static Dictionary<SupplyData, int> ReadUnitStockMap(UnitManager unit)
    {
        Dictionary<SupplyData, int> map = new Dictionary<SupplyData, int>();
        if (unit == null)
            return map;

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources == null)
            return map;

        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || entry.supply == null)
                continue;
            int amount = Mathf.Max(0, entry.amount);
            if (map.TryGetValue(entry.supply, out int existing))
                map[entry.supply] = existing + amount;
            else
                map[entry.supply] = amount;
        }

        return map;
    }

    private static Dictionary<SupplyData, int> ReadUnitCapacityMap(UnitManager unit)
    {
        Dictionary<SupplyData, int> map = new Dictionary<SupplyData, int>();
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.supplierResources == null)
            return map;

        for (int i = 0; i < data.supplierResources.Count; i++)
        {
            UnitEmbarkedSupply entry = data.supplierResources[i];
            if (entry == null || entry.supply == null)
                continue;
            int capacity = Mathf.Max(0, entry.amount);
            if (map.TryGetValue(entry.supply, out int existing))
                map[entry.supply] = existing + capacity;
            else
                map[entry.supply] = capacity;
        }

        return map;
    }

    private static Dictionary<SupplyData, int> ReadConstructionStockMap(ConstructionManager construction)
    {
        Dictionary<SupplyData, int> map = new Dictionary<SupplyData, int>();
        if (construction == null)
            return map;

        IReadOnlyList<ConstructionSupplyOffer> offers = construction.OfferedSupplies;
        if (offers == null)
            return map;

        for (int i = 0; i < offers.Count; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer == null || offer.supply == null)
                continue;
            int amount = construction.HasInfiniteSuppliesFor(offer.supply) ? int.MaxValue : Mathf.Max(0, offer.quantity);
            if (map.TryGetValue(offer.supply, out int existing))
                map[offer.supply] = existing >= int.MaxValue || amount >= int.MaxValue ? int.MaxValue : existing + amount;
            else
                map[offer.supply] = amount;
        }

        return map;
    }

    private static int ConsumeFromUnit(UnitManager unit, SupplyData supply, int amount)
    {
        if (unit == null || supply == null || amount <= 0)
            return 0;

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources == null)
            return 0;

        int remaining = amount;
        for (int i = 0; i < resources.Count && remaining > 0; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || entry.supply != supply || entry.amount <= 0)
                continue;
            int spent = Mathf.Min(entry.amount, remaining);
            entry.amount -= spent;
            remaining -= spent;
        }

        return amount - remaining;
    }

    private static int ConsumeFromConstruction(ConstructionManager construction, SupplyData supply, int amount)
    {
        if (construction == null || supply == null || amount <= 0)
            return 0;
        if (construction.HasInfiniteSuppliesFor(supply))
            return amount;

        IReadOnlyList<ConstructionSupplyOffer> offers = construction.OfferedSupplies;
        if (offers == null)
            return 0;

        int remaining = amount;
        for (int i = 0; i < offers.Count && remaining > 0; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer == null || offer.supply != supply || offer.quantity <= 0)
                continue;
            int spent = Mathf.Min(offer.quantity, remaining);
            offer.quantity -= spent;
            remaining -= spent;
        }

        return amount - remaining;
    }

    private static int AddToUnit(UnitManager unit, SupplyData supply, int amount)
    {
        if (unit == null || supply == null || amount <= 0)
            return 0;

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.supplierResources == null)
            return 0;

        int remaining = amount;
        int count = Mathf.Min(resources.Count, data.supplierResources.Count);
        for (int i = 0; i < count && remaining > 0; i++)
        {
            UnitEmbarkedSupply runtime = resources[i];
            UnitEmbarkedSupply baseline = data.supplierResources[i];
            if (runtime == null || baseline == null || runtime.supply == null || baseline.supply == null)
                continue;
            if (runtime.supply != supply || baseline.supply != supply)
                continue;

            int max = Mathf.Max(0, baseline.amount);
            int current = Mathf.Max(0, runtime.amount);
            int free = Mathf.Max(0, max - current);
            if (free <= 0)
                continue;

            int add = Mathf.Min(free, remaining);
            runtime.amount = current + add;
            remaining -= add;
        }

        return amount - remaining;
    }

    private static int GetUnitRemainingCapacityForSupply(UnitManager unit, SupplyData supply)
    {
        if (unit == null || supply == null)
            return 0;

        Dictionary<SupplyData, int> stockBySupply = ReadUnitStockMap(unit);
        Dictionary<SupplyData, int> capacityBySupply = ReadUnitCapacityMap(unit);
        if (capacityBySupply == null || !capacityBySupply.TryGetValue(supply, out int capacity))
            return 0;

        int current = stockBySupply != null && stockBySupply.TryGetValue(supply, out int existing)
            ? Mathf.Max(0, existing)
            : 0;
        return Mathf.Max(0, Mathf.Max(0, capacity) - current);
    }

    private static int AddToConstruction(ConstructionManager construction, SupplyData supply, int amount)
    {
        if (construction == null || supply == null || amount <= 0)
            return 0;
        if (construction.HasInfiniteSuppliesFor(supply))
            return amount;

        IReadOnlyList<ConstructionSupplyOffer> offers = construction.OfferedSupplies;
        if (offers == null)
            return 0;

        for (int i = 0; i < offers.Count; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer == null || offer.supply != supply)
                continue;
            long sum = (long)Mathf.Max(0, offer.quantity) + amount;
            offer.quantity = sum >= int.MaxValue ? int.MaxValue : (int)sum;
            return amount;
        }

        return 0;
    }
}
