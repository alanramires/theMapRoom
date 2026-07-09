using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class TurnStateManager
{
    private sealed class DisembarkPassengerEntry
    {
        public UnitManager passenger;
        public int slotIndex;
        public int seatIndex;
        public int selectionNumber;
        public string label;
    }

    private sealed class DisembarkOrder
    {
        public UnitManager passenger;
        public int slotIndex;
        public int seatIndex;
        public Vector3Int targetCell;
    }

    private sealed class DisembarkRuntimeOrder
    {
        public UnitManager passenger;
        public Vector3Int targetCell;
    }

    private readonly List<DisembarkPassengerEntry> disembarkPassengerEntries = new List<DisembarkPassengerEntry>();
    private readonly List<DisembarkOrder> disembarkQueuedOrders = new List<DisembarkOrder>();
    private readonly List<PodeDesembarcarOption> disembarkLandingOptions = new List<PodeDesembarcarOption>();
    private readonly Dictionary<Vector3Int, PodeDesembarcarOption> disembarkLandingByCell = new Dictionary<Vector3Int, PodeDesembarcarOption>();
    private int disembarkSelectedPassengerIndex = -1;
    private Vector3Int disembarkSelectedLandingCell = Vector3Int.zero;
    private bool disembarkSelectedLandingCellValid;
    private Vector3Int disembarkPreferredLandingCell = Vector3Int.zero;
    private bool disembarkPreferredLandingCellValid;
    private bool disembarkLandingAutoEntered;
    private bool disembarkExecutionInProgress;
    private bool disembarkSuppressDefaultConfirmSfxOnce;
    private int disembarkPassengerFocusIndex;
    private CursorState cursorStateBeforeDesembarcando = CursorState.MoveuParado;

    public bool IsDisembarkExecutionInProgress => disembarkExecutionInProgress;
    public int DisembarkSelectedPassengerIndex => disembarkSelectedPassengerIndex;
    public bool DisembarkLandingAutoEntered => disembarkLandingAutoEntered;
    public bool DisembarkSelectedLandingCellValid => disembarkSelectedLandingCellValid;
    public Vector3Int DisembarkSelectedLandingCell => disembarkSelectedLandingCell;
    public int DisembarkPassengerEntriesCount => disembarkPassengerEntries.Count;
    public int DisembarkQueuedOrdersCount => disembarkQueuedOrders.Count;
    public int DisembarkLandingOptionsCount => disembarkLandingOptions.Count;
    public string CurrentScannerPromptStepDebug => scannerPromptStep.ToString();
    public bool IsDisembarkPassengerSelectStep => CurrentCursorState == CursorState.Desembarcando &&
                                                  scannerPromptStep == ScannerPromptStep.DisembarkPassengerSelect;
    public int DisembarkPassengerFocusIndex => disembarkPassengerFocusIndex;
    public bool DisembarkPassengerCancelFocused => IsDisembarkPassengerSelectStep &&
                                                   disembarkPassengerFocusIndex == GetDisembarkPassengerCancelFocusIndex();

    private int GetDisembarkPassengerExecuteFocusIndex()
    {
        return disembarkQueuedOrders.Count > 0 ? disembarkPassengerEntries.Count : -1;
    }

    private int GetDisembarkPassengerCancelFocusIndex()
    {
        return disembarkPassengerEntries.Count + (disembarkQueuedOrders.Count > 0 ? 1 : 0);
    }

    public bool NavigateDisembarkPassengerFocus(int delta)
    {
        if (!IsDisembarkPassengerSelectStep || delta == 0)
            return false;
        int total = GetDisembarkPassengerCancelFocusIndex() + 1;
        if (total <= 0)
            return false;
        disembarkPassengerFocusIndex =
            (disembarkPassengerFocusIndex + (delta > 0 ? 1 : -1) + total) % total;
        cursorController?.PlayCursorMoveSfx();
        return true;
    }

    public bool TryInvokeFocusedDisembarkPassengerOption()
    {
        if (!IsDisembarkPassengerSelectStep || DisembarkPassengerCancelFocused)
            return false;
        int executeIndex = GetDisembarkPassengerExecuteFocusIndex();
        if (executeIndex >= 0 && disembarkPassengerFocusIndex == executeIndex)
            return TryExecuteDisembarkQueueFromPointer();
        if (disembarkPassengerFocusIndex < 0 || disembarkPassengerFocusIndex >= disembarkPassengerEntries.Count)
            return false;
        DisembarkPassengerEntry entry = disembarkPassengerEntries[disembarkPassengerFocusIndex];
        return entry != null && TrySelectDisembarkPassengerFromPointer(entry.selectionNumber);
    }

    public bool TrySelectDisembarkPassengerFromPointer(int selectionNumber)
    {
        if (CurrentCursorState != CursorState.Desembarcando ||
            scannerPromptStep != ScannerPromptStep.DisembarkPassengerSelect)
            return false;

        for (int i = 0; i < disembarkPassengerEntries.Count; i++)
        {
            DisembarkPassengerEntry entry = disembarkPassengerEntries[i];
            if (entry == null || entry.selectionNumber != selectionNumber)
                continue;
            disembarkPassengerFocusIndex = i;
            disembarkSelectedPassengerIndex = i;
            cursorController?.PlayConfirmSfx();
            if (!EnterDisembarkLandingSelectStep(autoEntered: false))
                EnterDisembarkPassengerSelectStep();
            return true;
        }
        return false;
    }

    public bool TryAdvanceDisembarkFromPointer()
    {
        if (CurrentCursorState != CursorState.Desembarcando ||
            scannerPromptStep == ScannerPromptStep.DisembarkPassengerSelect)
            return false;
        HandleConfirmWithFeedback();
        return true;
    }

    public bool TryQueueDisembarkAtCellFromPointer(Vector3Int cell)
    {
        if (CurrentCursorState != CursorState.Desembarcando)
            return false;

        cell.z = 0;

        if (scannerPromptStep == ScannerPromptStep.DisembarkPassengerSelect)
        {
            int matchingPassengers = 0;
            int matchingPassengerIndex = -1;
            for (int i = 0; i < disembarkPassengerEntries.Count; i++)
            {
                DisembarkPassengerEntry passenger = disembarkPassengerEntries[i];
                if (!PassengerHasDisembarkOptionAtCell(passenger, cell))
                    continue;
                matchingPassengers++;
                matchingPassengerIndex = i;
            }
            if (matchingPassengers <= 0)
                return false;

            SetPreferredDisembarkLandingCell(cell);
            // Sem ambiguidade: atribui o unico passageiro restante, mas para em CONFIRMAR.
            if (disembarkPassengerEntries.Count == 1 && matchingPassengerIndex == 0)
            {
                disembarkSelectedPassengerIndex = 0;
                disembarkPassengerFocusIndex = 0;
                return EnterDisembarkLandingSelectStep(autoEntered: true);
            }

            // Com mais passageiros, o local fica guardado ate o jogador escolher qual deles usar.
            return true;
        }

        if (scannerPromptStep == ScannerPromptStep.DisembarkConfirm)
        {
            Vector3Int selectedCell = disembarkSelectedLandingCell;
            selectedCell.z = 0;
            if (!disembarkSelectedLandingCellValid || cell != selectedCell)
                return false;

            // Segundo clique no mesmo hex confirma a ordem e a adiciona a fila.
            if (!AllowsContextualPointerConfirmation())
                return true;
            return TryConfirmScannerDisembark();
        }

        if (scannerPromptStep != ScannerPromptStep.DisembarkLandingSelect)
            return false;

        if (!disembarkLandingByCell.ContainsKey(cell))
            return false;

        SetDisembarkSelectedLandingCell(cell, moveCursor: true);
        // O clique escolhe o local e avanca somente ate a confirmacao. A ordem entra
        // na fila apenas com um novo comando explicito (botao/Enter), evitando toque acidental.
        return TryConfirmScannerDisembark() &&
               scannerPromptStep == ScannerPromptStep.DisembarkConfirm;
    }

    private bool TryBeginDisembarkAtClickedCell(Vector3Int cell)
    {
        if ((CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado) ||
            scannerPromptStep != ScannerPromptStep.AwaitingAction ||
            selectedUnit == null ||
            !availableSensorActionCodes.Contains('D'))
            return false;

        cell.z = 0;
        HashSet<UnitManager> passengersForClickedCell = new HashSet<UnitManager>();
        HashSet<UnitManager> allValidPassengers = new HashSet<UnitManager>();
        for (int i = 0; i < cachedPodeDesembarcarTargets.Count; i++)
        {
            PodeDesembarcarOption option = cachedPodeDesembarcarTargets[i];
            if (option == null || option.passengerUnit == null)
                continue;

            allValidPassengers.Add(option.passengerUnit);
            Vector3Int optionCell = option.disembarkCell;
            optionCell.z = 0;
            if (optionCell == cell)
                passengersForClickedCell.Add(option.passengerUnit);
        }

        // O clique so e trigger quando o proprio sensor reconhece o hex como destino valido.
        if (passengersForClickedCell.Count <= 0)
            return false;

        SetPreferredDisembarkLandingCell(cell);
        bool hasSinglePassenger = allValidPassengers.Count == 1;
        HandleDisembarkActionRequested();
        if (CurrentCursorState != CursorState.Desembarcando)
            return false;

        // Com varios passageiros, nao inferimos qual deles o jogador quis usar. O hex fica
        // guardado e sera revalidado quando o passageiro for escolhido.
        if (!hasSinglePassenger)
            return scannerPromptStep == ScannerPromptStep.DisembarkPassengerSelect;

        // O fluxo oficial auto-seleciona o unico passageiro e consome a preferencia,
        // parando em CONFIRMAR sem adicionar a ordem automaticamente.
        return true;
    }

    public bool TryExecuteDisembarkQueueFromPointer()
    {
        if (CurrentCursorState != CursorState.Desembarcando ||
            scannerPromptStep != ScannerPromptStep.DisembarkPassengerSelect ||
            disembarkQueuedOrders.Count <= 0 || disembarkExecutionInProgress)
            return false;
        disembarkPassengerFocusIndex = GetDisembarkPassengerExecuteFocusIndex();
        StartDisembarkExecution();
        return true;
    }

    public string[] GetDisembarkPassengerDebugLines()
    {
        if (disembarkPassengerEntries.Count <= 0)
            return System.Array.Empty<string>();

        string[] lines = new string[disembarkPassengerEntries.Count];
        for (int i = 0; i < disembarkPassengerEntries.Count; i++)
        {
            DisembarkPassengerEntry entry = disembarkPassengerEntries[i];
            if (entry == null)
            {
                lines[i] = $"{i + 1}. (null)";
                continue;
            }

            string passengerName = ResolveUnitRuntimeName(entry.passenger);
            lines[i] = $"{i + 1}. {passengerName} | slot={entry.slotIndex}:{entry.seatIndex}";
        }

        return lines;
    }

    public string[] GetDisembarkQueuedOrderDebugLines()
    {
        if (disembarkQueuedOrders.Count <= 0)
            return System.Array.Empty<string>();

        string[] lines = new string[disembarkQueuedOrders.Count];
        for (int i = 0; i < disembarkQueuedOrders.Count; i++)
        {
            DisembarkOrder order = disembarkQueuedOrders[i];
            if (order == null)
            {
                lines[i] = $"{i + 1}. (null)";
                continue;
            }

            string passengerName = ResolveUnitRuntimeName(order.passenger);
            Vector3Int cell = order.targetCell;
            lines[i] = $"{i + 1}. {passengerName} | slot={order.slotIndex}:{order.seatIndex} -> {FormatMapCellWithZ(cell)}";
        }

        return lines;
    }

    private void HandleDisembarkActionRequested()
    {
        if (selectedUnit == null)
            return;
        if (CurrentCursorState != CursorState.MoveuAndando && CurrentCursorState != CursorState.MoveuParado)
            return;

        if (cachedPodeDesembarcarTargets.Count == 0)
        {
            RuntimeLog("Pode Desembarcar (\"D\"): nao ha opcoes validas agora.");
            LogScannerPanel();
            return;
        }

        cursorController?.PlayConfirmSfx();
        replayManager?.UpdateCurrentBufferSensorAction(SensorActionType.Disembark, "DisembarkActionRequested");
        cursorStateBeforeDesembarcando = CurrentCursorState == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        Advance(CursorState.Desembarcando, "HandleDisembarkActionRequested");
        ClearCommittedPathVisual();
        disembarkQueuedOrders.Clear();
        EnterDisembarkPassengerSelectStep();
    }

    private void ProcessDisembarkPromptInput()
    {
        if (CurrentCursorState != CursorState.Desembarcando)
            return;

        if (scannerPromptStep == ScannerPromptStep.DisembarkPassengerSelect)
        {
            if (!TryReadPressedDigitIncludingZero(out int number))
                return;

            if (number == 0)
            {
                if (disembarkQueuedOrders.Count > 0)
                {
                    StartDisembarkExecution();
                    return;
                }

                RuntimeLog("[Desembarque] Nenhuma ordem em fila para executar.");
                return;
            }

            int index = number - 1;
            DisembarkPassengerEntry pickedEntry = null;
            for (int i = 0; i < disembarkPassengerEntries.Count; i++)
            {
                DisembarkPassengerEntry entry = disembarkPassengerEntries[i];
                if (entry != null && entry.selectionNumber == number)
                {
                    pickedEntry = entry;
                    index = i;
                    break;
                }
            }

            if (pickedEntry == null || index < 0 || index >= disembarkPassengerEntries.Count)
            {
                RuntimeLog($"[Desembarque] Passageiro invalido: {number}. Escolha uma das opcoes listadas.");
                return;
            }

            disembarkSelectedPassengerIndex = index;
            cursorController?.PlayConfirmSfx();
            if (!EnterDisembarkLandingSelectStep(autoEntered: false))
                EnterDisembarkPassengerSelectStep();
            return;
        }
    }

    private bool TryConfirmScannerDisembark()
    {
        if (CurrentCursorState != CursorState.Desembarcando)
            return false;

        if (scannerPromptStep == ScannerPromptStep.DisembarkPassengerSelect)
            return false;

        if (scannerPromptStep == ScannerPromptStep.DisembarkLandingSelect)
        {
            if (!disembarkSelectedLandingCellValid || !disembarkLandingByCell.ContainsKey(disembarkSelectedLandingCell))
            {
                RuntimeLog("[Desembarque] Escolha um hex valido para desembarque.");
                return true;
            }

            scannerPromptStep = ScannerPromptStep.DisembarkConfirm;
            cursorController?.PlayConfirmSfx();
            LogDisembarkConfirmPrompt();
            return true;
        }

        if (scannerPromptStep != ScannerPromptStep.DisembarkConfirm)
            return true;

        if (!TryGetSelectedDisembarkLandingOption(out PodeDesembarcarOption option))
        {
            ReturnToDisembarkLandingSelect();
            return true;
        }

        if (!TryGetSelectedPassengerEntry(out DisembarkPassengerEntry entry))
        {
            ReturnToDisembarkPassengerSelect();
            return true;
        }

        if (entry.passenger == null)
        {
            ReturnToDisembarkPassengerSelect();
            return true;
        }

        if (option.passengerUnit != null && option.passengerUnit != entry.passenger)
        {
            Debug.LogWarning("[Desembarque] Opcao selecionada nao corresponde ao passageiro atual. Voltando para selecao de landing.");
            ReturnToDisembarkLandingSelect();
            return true;
        }

        if (IsPassengerAlreadyQueued(entry.passenger))
        {
            RuntimeLog($"[Desembarque] {ResolveUnitRuntimeName(entry.passenger)} ja possui ordem em fila. Escolha outro passageiro.");
            ReturnToDisembarkPassengerSelect();
            return true;
        }

        Vector3Int targetCell = option.disembarkCell;
        targetCell.z = 0;
        if (IsCellAlreadyQueuedForDisembark(targetCell))
        {
            RuntimeLog($"[Desembarque] Hex {FormatMapCell(targetCell)} ja reservado por outra ordem. Escolha outro hex.");
            ReturnToDisembarkLandingSelect();
            return true;
        }

        disembarkQueuedOrders.Add(new DisembarkOrder
        {
            passenger = entry.passenger,
            slotIndex = entry.slotIndex,
            seatIndex = entry.seatIndex,
            targetCell = targetCell
        });
        cursorController?.PlayLoadSfx();
        disembarkSuppressDefaultConfirmSfxOnce = true;

        int remaining = CountRemainingPassengersForDisembark();
        if (remaining <= 0)
        {
            StartDisembarkExecution();
            return true;
        }

        RuntimeLog($"[Desembarque] Ordem adicionada para {ResolveUnitRuntimeName(entry.passenger)} -> {FormatMapCell(option.disembarkCell)}.");
        EnterDisembarkPassengerSelectStep();
        return true;
    }

    private void EnterDisembarkPassengerSelectStep()
    {
        ClearDisembarkLandingOptionsAndPaint();
        RebuildDisembarkPassengerEntries();
        scannerPromptStep = ScannerPromptStep.DisembarkPassengerSelect;
        disembarkSelectedPassengerIndex = -1;
        disembarkPassengerFocusIndex = 0;
        disembarkLandingAutoEntered = false;

        if (cursorController != null && selectedUnit != null)
        {
            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;
            cursorController.SetCell(unitCell, playMoveSfx: false);
        }

        if (disembarkPassengerEntries.Count <= 0)
        {
            if (disembarkQueuedOrders.Count > 0)
                StartDisembarkExecution();
            else
                ExitDisembarkStateToMovement();
            return;
        }

        if (disembarkPassengerEntries.Count == 1 && disembarkQueuedOrders.Count <= 0)
        {
            disembarkSelectedPassengerIndex = 0;
            if (!EnterDisembarkLandingSelectStep(autoEntered: true))
                ExitDisembarkStateToMovement();
            return;
        }

        LogDisembarkPassengerSelectionPanel();
    }

    private bool EnterDisembarkLandingSelectStep(bool autoEntered)
    {
        if (!TryGetSelectedPassengerEntry(out DisembarkPassengerEntry entry))
            return false;

        RebuildDisembarkLandingOptions(entry);
        if (disembarkLandingOptions.Count <= 0)
        {
            RuntimeLog($"[Desembarque] {ResolveUnitRuntimeName(entry.passenger)} sem hex valido para desembarque no momento.");
            return false;
        }

        scannerPromptStep = ScannerPromptStep.DisembarkLandingSelect;
        disembarkLandingAutoEntered = autoEntered;

        if (disembarkPreferredLandingCellValid)
        {
            Vector3Int preferredCell = disembarkPreferredLandingCell;
            preferredCell.z = 0;
            ClearPreferredDisembarkLandingCell();
            if (disembarkLandingByCell.ContainsKey(preferredCell))
            {
                SetDisembarkSelectedLandingCell(preferredCell, moveCursor: true);
                PaintDisembarkLandingOptions();
                scannerPromptStep = ScannerPromptStep.DisembarkConfirm;
                LogDisembarkConfirmPrompt();
                return true;
            }
        }

        SetDisembarkSelectedLandingCell(disembarkLandingOptions[0].disembarkCell, moveCursor: true);
        PaintDisembarkLandingOptions();

        if (disembarkLandingOptions.Count == 1)
        {
            scannerPromptStep = ScannerPromptStep.DisembarkConfirm;
            LogDisembarkConfirmPrompt();
            return true;
        }

        LogDisembarkLandingSelectionPanel(entry);
        return true;
    }

    private void ReturnToDisembarkPassengerSelect()
    {
        EnterDisembarkPassengerSelectStep();
    }

    private bool TryUndoLastQueuedDisembarkOrderAndReturnToLanding()
    {
        if (disembarkQueuedOrders.Count <= 0)
            return false;

        int lastIndex = disembarkQueuedOrders.Count - 1;
        DisembarkOrder lastOrder = disembarkQueuedOrders[lastIndex];
        disembarkQueuedOrders.RemoveAt(lastIndex);

        if (lastOrder == null || lastOrder.passenger == null)
        {
            EnterDisembarkPassengerSelectStep();
            return true;
        }

        RebuildDisembarkPassengerEntries();
        disembarkSelectedPassengerIndex = -1;
        for (int i = 0; i < disembarkPassengerEntries.Count; i++)
        {
            DisembarkPassengerEntry entry = disembarkPassengerEntries[i];
            if (entry == null || entry.passenger == null)
                continue;
            if (entry.passenger != lastOrder.passenger)
                continue;

            if (entry.slotIndex == lastOrder.slotIndex && entry.seatIndex == lastOrder.seatIndex)
            {
                disembarkSelectedPassengerIndex = i;
                break;
            }
        }

        if (disembarkSelectedPassengerIndex >= 0 && EnterDisembarkLandingSelectStep(autoEntered: false))
        {
            RuntimeLog($"[Desembarque] Ordem desfeita para {ResolveUnitRuntimeName(lastOrder.passenger)}. Escolha novo hex.");
            return true;
        }

        EnterDisembarkPassengerSelectStep();
        RuntimeLog("[Desembarque] Ordem desfeita. Retornando para selecao de passageiro.");
        return true;
    }

    private void ReturnToDisembarkLandingSelect()
    {
        if (scannerPromptStep == ScannerPromptStep.DisembarkLandingSelect)
            return;
        if (!TryGetSelectedPassengerEntry(out DisembarkPassengerEntry entry))
        {
            EnterDisembarkPassengerSelectStep();
            return;
        }

        scannerPromptStep = ScannerPromptStep.DisembarkLandingSelect;
        LogDisembarkLandingSelectionPanel(entry);
    }

    private void ExitDisembarkStateToMovement()
    {
        if (CurrentCursorState != CursorState.Desembarcando)
            return;

        Retreat("ExitDisembarkStateToMovement");
        CursorState targetMovementState = CurrentCursorState;
        if (targetMovementState == CursorState.MoveuAndando && hasCommittedMovement && committedMovementPath.Count >= 2)
            DrawCommittedPathVisual(committedMovementPath);
        if (cursorController != null && selectedUnit != null)
        {
            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;
            cursorController.SetCell(unitCell, playMoveSfx: false);
        }

        ResetDisembarkRuntimeState();
        LogScannerPanel();
    }

    private void StartDisembarkExecution()
    {
        if (disembarkExecutionInProgress)
            return;
        if (selectedUnit == null || disembarkQueuedOrders.Count <= 0)
        {
            ExitDisembarkStateToMovement();
            return;
        }

        // Antes de executar, limpa highlight de range e ancora o cursor no transportador.
        ClearDisembarkLandingOptionsAndPaint();
        if (cursorController != null && selectedUnit != null)
        {
            Vector3Int transporterCell = selectedUnit.CurrentCellPosition;
            transporterCell.z = 0;
            cursorController.SetCell(transporterCell, playMoveSfx: false);
        }

        StartCoroutine(ExecuteQueuedDisembarkOrdersSequence());
    }

    private IEnumerator ExecuteQueuedDisembarkOrdersSequence()
    {
        disembarkExecutionInProgress = true;
        UnitManager transporter = selectedUnit;
        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : (transporter != null ? transporter.BoardTilemap : null);
        if (transporter == null || boardMap == null)
        {
            disembarkExecutionInProgress = false;
            ExitDisembarkStateToMovement();
            yield break;
        }

        bool transporterSortingRaised = false;
        bool airTransporterForcedLandedForDisembark = false;
        if (transporter != null)
        {
            transporter.SetTemporarySortingOrder();
            transporterSortingRaised = true;
        }

        Advance(CursorState.DesembarcandoExecuting, "ExecuteQueuedDisembarkOrdersSequence: begin");

        try
        {

        // Mesmo comportamento do embarque: transportador aereo pousa antes do desembarque.
        if (transporter != null && transporter.GetDomain() == Domain.Air)
        {
            AircraftOperationDecision landingDecision = AircraftOperationRules.Evaluate(
                transporter,
                boardMap,
                terrainDatabase,
                SensorMovementMode.MoveuParado);
            if (!landingDecision.available || landingDecision.action != AircraftOperationAction.Land)
            {
                RuntimeLog(string.IsNullOrWhiteSpace(landingDecision.reason)
                    ? "[Desembarque] Transportador aereo sem pouso valido."
                    : $"[Desembarque] {landingDecision.reason}");
                if (transporterSortingRaised && transporter != null)
                    transporter.ClearTemporarySortingOrder();
                Retreat("DesembarcandoExecuting: air landing abort");
                ExitDisembarkStateToMovement();
                yield break;
            }

            PlayMovementStartSfx(transporter);
            RuntimeLog("[Desembarque] Transportador pousou antes do desembarque.");

            bool transporterStartHigh = transporter.GetDomain() == Domain.Air && transporter.GetHeightLevel() == HeightLevel.AirHigh;
            bool transporterStartLow = transporter.GetDomain() == Domain.Air && transporter.GetHeightLevel() == HeightLevel.AirLow;
            if (transporterStartHigh)
            {
                float highToLowDuration = GetDisembarkAirHighToGroundDuration() * Mathf.Clamp01(GetDisembarkHighToLowNormalizedTime());
                if (highToLowDuration > 0f)
                    yield return new WaitForSeconds(highToLowDuration);
                transporter.TrySetCurrentLayerMode(Domain.Air, HeightLevel.AirLow);
                transporterStartLow = transporter.GetDomain() == Domain.Air && transporter.GetHeightLevel() == HeightLevel.AirLow;
            }

            float landingDuration = transporterStartLow
                ? GetDisembarkAirLowToGroundDuration()
                : GetDisembarkForcedLandingDuration();
            if (!transporter.TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface))
            {
                RuntimeLog("[Desembarque] Falha ao concluir pouso do transportador (Land/Surface).");
                if (transporterSortingRaised && transporter != null)
                    transporter.ClearTemporarySortingOrder();
                Retreat("DesembarcandoExecuting: layer mode abort");
                ExitDisembarkStateToMovement();
                yield break;
            }

            float vtolFxDuration = animationManager != null ? animationManager.PlayVtolLandingEffect(transporter) : 0f;
            landingDuration = Mathf.Max(landingDuration, vtolFxDuration);
            if (landingDuration > 0f)
                yield return new WaitForSeconds(landingDuration);

            float postLandingDelay = GetDisembarkAfterForcedLandingDelay();
            if (postLandingDelay > 0f)
                yield return new WaitForSeconds(postLandingDelay);

            airTransporterForcedLandedForDisembark = true;
        }

        // 2) Aguarda apos pouso antes de spawnar passageiros.
        float preSpawnDelay = GetDisembarkBeforeSpawnDelay();
        if (preSpawnDelay > 0f)
            yield return new WaitForSeconds(preSpawnDelay);

        List<DisembarkRuntimeOrder> runtimeOrders = new List<DisembarkRuntimeOrder>(disembarkQueuedOrders.Count);
        Vector3Int transporterCellForSpawn = transporter.CurrentCellPosition;
        transporterCellForSpawn.z = 0;
        float spawnStepDelay = GetDisembarkSpawnStepDelay();
        for (int i = 0; i < disembarkQueuedOrders.Count; i++)
        {
            DisembarkOrder order = disembarkQueuedOrders[i];
            if (order == null)
                continue;

            if (!transporter.TryDisembarkPassengerFromSeat(order.slotIndex, order.seatIndex, out UnitManager passenger, out string reason))
            {
                Debug.LogWarning($"[Desembarque] Falha ao liberar passageiro do slot {order.slotIndex}:{order.seatIndex}. Motivo: {reason}");
                continue;
            }

            if (passenger == null)
                continue;

            // Passageiro nasce exatamente na coordenada atual do transportador.
            passenger.SetCurrentCellPosition(transporterCellForSpawn, enforceFinalOccupancyRule: false);
            if (passenger.TryGetUnitData(out UnitData passengerDataAtSpawn) && passengerDataAtSpawn != null && passengerDataAtSpawn.IsAircraft())
            {
                // No desembarque de aeronave, primeiro ela aparece pousada no deck.
                passenger.TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface);
            }
            passenger.SetTemporarySortingOrder(1000 + i);
            // Evita mostrar lock de "ja agiu" durante o spawn/movimento.
            passenger.ResetActed();
            ReapplyForcedUnitVisualForFog(passenger);
            runtimeOrders.Add(new DisembarkRuntimeOrder
            {
                passenger = passenger,
                targetCell = order.targetCell
            });

            // 3) Passageiros surgem um de cada vez.
            if (spawnStepDelay > 0f && i < disembarkQueuedOrders.Count - 1)
                yield return new WaitForSeconds(spawnStepDelay);
        }

        // 4) Aguarda apos o spawn empilhado para leitura visual.
        float postSpawnDelay = GetDisembarkAfterSpawnDelay();
        if (postSpawnDelay > 0f)
            yield return new WaitForSeconds(postSpawnDelay);

        List<UnitManager> movedPassengers = new List<UnitManager>(runtimeOrders.Count);
        float afterPassengerMoveDelay = GetDisembarkAfterPassengerMoveDelay();
        float afterPassengerLoadDelay = GetDisembarkAfterPassengerLoadDelay();
        for (int i = 0; i < runtimeOrders.Count; i++)
        {
            DisembarkRuntimeOrder runtimeOrder = runtimeOrders[i];
            UnitManager passenger = runtimeOrder != null ? runtimeOrder.passenger : null;
            Vector3Int targetCell = runtimeOrder != null ? runtimeOrder.targetCell : Vector3Int.zero;
            if (passenger == null)
                continue;

            // Guarda de runtime: evita desembarque de aeronave se regra de decolagem 1-hex deixou de ser valida.
            if (!CanDisembarkAircraftPassengerAtRuntime(passenger, transporter, transporter.CurrentCellPosition, out string aircraftReason))
            {
                Debug.LogWarning($"[Desembarque] {ResolveUnitRuntimeName(passenger)} bloqueado por regra de decolagem: {aircraftReason}");
                passenger.ClearTemporarySortingOrder();
                continue;
            }

            if (passenger.TryGetUnitData(out UnitData passengerDataAtMove) && passengerDataAtMove != null && passengerDataAtMove.IsAircraft())
            {
                // Decolagem curta de desembarque: sai para Air/Low antes do deslocamento.
                bool wasAirborne = passenger.GetDomain() == Domain.Air && !passenger.IsAircraftGrounded;
                if (!passenger.TrySetCurrentLayerMode(Domain.Air, HeightLevel.AirLow))
                    passenger.TrySetCurrentLayerMode(Domain.Air, passenger.GetPreferredAirHeight());
                if (!wasAirborne)
                    passenger.MarkTookOffRecently();
            }

            int beforeFuel = passenger.CurrentFuel;
            passenger.SetCurrentFuel(Mathf.Max(0, beforeFuel - 1));

            List<Vector3Int> path = new List<Vector3Int>(2)
            {
                transporter.CurrentCellPosition,
                targetCell
            };
            path[0] = new Vector3Int(path[0].x, path[0].y, 0);
            path[1] = new Vector3Int(path[1].x, path[1].y, 0);

            // Cursor como "dedo do jogador": foca passageiro atual (com move sfx a partir do 2o).
            if (cursorController != null)
            {
                Vector3Int passengerCell = passenger.CurrentCellPosition;
                passengerCell.z = 0;
                cursorController.SetCell(passengerCell, playMoveSfx: i > 0);
            }

            bool finished = false;
            if (animationManager != null)
            {
                animationManager.PlayMovement(
                    passenger,
                    boardMap,
                    path,
                    playStartSfx: true,
                    onAnimationStart: () => PlayMovementStartSfx(passenger),
                    onAnimationFinished: () => finished = true,
                    onCellReached: reachedCell =>
                    {
                        if (cursorController == null)
                            return;

                        Vector3Int c = reachedCell;
                        c.z = 0;
                        cursorController.SetCell(c, playMoveSfx: false);
                    });
                while (!finished)
                    yield return null;
            }
            else
            {
                PlayMovementStartSfx(passenger);
                passenger.SetCurrentCellPosition(targetCell, enforceFinalOccupancyRule: true);
                if (cursorController != null)
                {
                    Vector3Int c = targetCell;
                    c.z = 0;
                    cursorController.SetCell(c, playMoveSfx: false);
                }
            }

            if (afterPassengerMoveDelay > 0f)
                yield return new WaitForSeconds(afterPassengerMoveDelay);

            passenger.ClearTemporarySortingOrder();
            cursorController?.PlayLoadSfx();
            passenger.MarkAsActed();
            RecordDisembarkReplayCommand(passenger, transporter, targetCell);
            OnUnitDisembarked?.Invoke(passenger, transporter);
            movedPassengers.Add(passenger);

            // Pausa entre encerramento de um passageiro e inicio do proximo.
            if (afterPassengerLoadDelay > 0f)
                yield return new WaitForSeconds(afterPassengerLoadDelay);
        }

        // 6) Aguarda apos os movimentos antes de travar acao.
        float postMoveDelay = GetDisembarkAfterMoveDelay();
        if (postMoveDelay > 0f)
            yield return new WaitForSeconds(postMoveDelay);

        // Cursor volta para o transportador ao final da sequencia dos passageiros.
        if (cursorController != null && transporter != null)
        {
            Vector3Int transporterCell = transporter.CurrentCellPosition;
            transporterCell.z = 0;
            cursorController.SetCell(transporterCell, playMoveSfx: true);
        }

        if (airTransporterForcedLandedForDisembark && transporter != null)
            yield return ExecutePostDisembarkAirTransporterTakeoff(transporter, boardMap);

        if (transporter != null)
            transporter.MarkAsActed();
        cursorController?.PlayDoneSfx();
        float afterTransporterDoneDelay = GetDisembarkAfterTransporterDoneDelay();
        if (afterTransporterDoneDelay > 0f)
            yield return new WaitForSeconds(afterTransporterDoneDelay);
        bool finalized = TryFinalizeSelectedUnitActionFromDebug();
        if (!finalized)
            ClearSelectionAndReturnToNeutral(keepPreparedFuelCost: true);

        ResetDisembarkRuntimeState();
        if (transporterSortingRaised && transporter != null)
            transporter.ClearTemporarySortingOrder();

        } // try
        finally
        {
            disembarkExecutionInProgress = false;
        }
    }

    private IEnumerator ExecutePostDisembarkAirTransporterTakeoff(UnitManager transporter, Tilemap boardMap)
    {
        if (transporter == null || boardMap == null)
            yield break;
        if (!transporter.TryGetUnitData(out UnitData data) || data == null || !data.IsAircraft())
            yield break;
        if (transporter.GetDomain() == Domain.Air && !transporter.IsAircraftGrounded)
            yield break;

        PodeDecolarReport report = PodeDecolarSensor.Evaluate(transporter, boardMap, terrainDatabase);
        bool canTakeoffInPlace = report != null
            && report.status
            && report.takeoffMoveOptions != null
            && (report.takeoffMoveOptions.Contains(0) || report.takeoffMoveOptions.Contains(9));
        if (!canTakeoffInPlace)
        {
            RuntimeLog(report != null && !string.IsNullOrWhiteSpace(report.explicacao)
                ? $"[Desembarque] Transportador permanece no solo apos desembarque: {report.explicacao}"
                : "[Desembarque] Transportador permanece no solo apos desembarque: decolagem indisponivel.");
            yield break;
        }

        if (!AircraftOperationRules.TryApplyOperation(
                transporter,
                boardMap,
                terrainDatabase,
                SensorMovementMode.MoveuParado,
                out AircraftOperationDecision takeoffDecision))
        {
            RuntimeLog(string.IsNullOrWhiteSpace(takeoffDecision.reason)
                ? "[Desembarque] Falha ao decolar transportador apos desembarque."
                : $"[Desembarque] Transportador permanece no solo apos desembarque: {takeoffDecision.reason}");
            yield break;
        }

        transporter.MarkTookOffRecently();
        PlayMovementStartSfx(transporter);
        RuntimeLog("[Desembarque] Transportador decolou apos concluir o desembarque.");

        float takeoffFxDuration = animationManager != null ? animationManager.PlayVtolLandingEffect(transporter) : 0f;
        if (takeoffFxDuration > 0f)
            yield return new WaitForSeconds(takeoffFxDuration);
    }

    private void ResetDisembarkRuntimeState()
    {
        disembarkPassengerEntries.Clear();
        disembarkQueuedOrders.Clear();
        ClearDisembarkLandingOptionsAndPaint();
        scannerPromptStep = ScannerPromptStep.AwaitingAction;
        disembarkSelectedPassengerIndex = -1;
        disembarkLandingAutoEntered = false;
        disembarkSuppressDefaultConfirmSfxOnce = false;
        ClearPreferredDisembarkLandingCell();
    }

    private void SetPreferredDisembarkLandingCell(Vector3Int cell)
    {
        cell.z = 0;
        disembarkPreferredLandingCell = cell;
        disembarkPreferredLandingCellValid = true;
    }

    private void ClearPreferredDisembarkLandingCell()
    {
        disembarkPreferredLandingCell = Vector3Int.zero;
        disembarkPreferredLandingCellValid = false;
    }

    private bool PassengerHasDisembarkOptionAtCell(DisembarkPassengerEntry passenger, Vector3Int cell)
    {
        if (passenger == null || passenger.passenger == null)
            return false;
        cell.z = 0;
        for (int i = 0; i < cachedPodeDesembarcarTargets.Count; i++)
        {
            PodeDesembarcarOption option = cachedPodeDesembarcarTargets[i];
            if (option == null || option.passengerUnit != passenger.passenger ||
                option.transporterSlotIndex != passenger.slotIndex ||
                option.transporterSeatIndex != passenger.seatIndex)
                continue;
            Vector3Int optionCell = option.disembarkCell;
            optionCell.z = 0;
            if (optionCell == cell && !IsCellAlreadyQueuedForDisembark(cell))
                return true;
        }
        return false;
    }

    private void RebuildDisembarkPassengerEntries()
    {
        disembarkPassengerEntries.Clear();
        if (selectedUnit == null)
            return;

        IReadOnlyList<UnitTransportSeatRuntime> seats = selectedUnit.TransportedUnitSlots;
        if (seats == null || seats.Count <= 0)
            return;

        int selectionNumber = 0;
        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            if (seat == null || seat.embarkedUnit == null || !seat.embarkedUnit.IsEmbarked)
                continue;

            if (IsPassengerAlreadyQueued(seat.embarkedUnit))
                continue;

            selectionNumber++;
            string slotLabel = !string.IsNullOrWhiteSpace(seat.slotId) ? seat.slotId : $"slot {seat.slotIndex}";
            disembarkPassengerEntries.Add(new DisembarkPassengerEntry
            {
                passenger = seat.embarkedUnit,
                slotIndex = seat.slotIndex,
                seatIndex = seat.seatIndex,
                selectionNumber = selectionNumber,
                label = $"{ResolveUnitRuntimeName(seat.embarkedUnit)} ({slotLabel} vaga {seat.seatIndex + 1})"
            });
        }
    }

    private void RebuildDisembarkLandingOptions(DisembarkPassengerEntry passengerEntry)
    {
        disembarkLandingOptions.Clear();
        disembarkLandingByCell.Clear();
        disembarkSelectedLandingCellValid = false;

        if (passengerEntry == null || passengerEntry.passenger == null)
            return;

        int skippedByQueuedReservation = 0;
        for (int i = 0; i < cachedPodeDesembarcarTargets.Count; i++)
        {
            PodeDesembarcarOption option = cachedPodeDesembarcarTargets[i];
            if (option == null || option.passengerUnit != passengerEntry.passenger)
                continue;
            if (option.transporterSlotIndex != passengerEntry.slotIndex || option.transporterSeatIndex != passengerEntry.seatIndex)
                continue;

            Vector3Int cell = option.disembarkCell;
            cell.z = 0;
            if (IsCellAlreadyQueuedForDisembark(cell))
            {
                skippedByQueuedReservation++;
                continue;
            }
            if (disembarkLandingByCell.ContainsKey(cell))
                continue;

            disembarkLandingByCell.Add(cell, option);
            disembarkLandingOptions.Add(option);
        }

        if (skippedByQueuedReservation > 0)
            RuntimeLog($"[Desembarque] {skippedByQueuedReservation} hex(es) filtrado(s) para {ResolveUnitRuntimeName(passengerEntry.passenger)} por reserva em ordens ja definidas.");

        SortDisembarkLandingOptionsClockwise();
    }

    private void SortDisembarkLandingOptionsClockwise()
    {
        if (disembarkLandingOptions.Count <= 1 || terrainTilemap == null || selectedUnit == null)
            return;

        Vector3Int transporterCell = selectedUnit.CurrentCellPosition;
        transporterCell.z = 0;
        Vector3 center = HexCoordinates.GetCellCenterWorld(terrainTilemap, transporterCell);

        disembarkLandingOptions.Sort((a, b) =>
        {
            Vector3 posA = HexCoordinates.GetCellCenterWorld(terrainTilemap, new Vector3Int(a.disembarkCell.x, a.disembarkCell.y, 0));
            Vector3 posB = HexCoordinates.GetCellCenterWorld(terrainTilemap, new Vector3Int(b.disembarkCell.x, b.disembarkCell.y, 0));
            float angleA = Mathf.Atan2(posA.x - center.x, posA.y - center.y);
            float angleB = Mathf.Atan2(posB.x - center.x, posB.y - center.y);
            if (angleA < 0) angleA += 2 * Mathf.PI;
            if (angleB < 0) angleB += 2 * Mathf.PI;
            return angleA.CompareTo(angleB);
        });
    }

    private void PaintDisembarkLandingOptions()
    {
        ClearMovementRange(keepCommittedMovement: true);
        if (rangeMapTilemap == null || rangeOverlayTile == null || selectedUnit == null)
            return;

        Color teamColor = TeamUtils.GetColor(selectedUnit.TeamId);
        Color overlayColor = new Color(teamColor.r, teamColor.g, teamColor.b, Mathf.Clamp01(movementRangeAlpha));
        for (int i = 0; i < disembarkLandingOptions.Count; i++)
        {
            PodeDesembarcarOption option = disembarkLandingOptions[i];
            if (option == null)
                continue;

            Vector3Int cell = option.disembarkCell;
            cell.z = 0;
            rangeMapTilemap.SetTile(cell, rangeOverlayTile);
            rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
            rangeMapTilemap.SetColor(cell, overlayColor);
            paintedRangeCells.Add(cell);
            paintedRangeLookup.Add(cell);
        }
    }

    private void ClearDisembarkLandingOptionsAndPaint()
    {
        disembarkLandingOptions.Clear();
        disembarkLandingByCell.Clear();
        disembarkSelectedLandingCellValid = false;
        ClearMovementRange(keepCommittedMovement: true);
    }

    private bool TryResolveDisembarkCursorMove(Vector3Int currentCell, Vector3Int inputDelta, out Vector3Int resolvedCell)
    {
        resolvedCell = currentCell;
        if (CurrentCursorState != CursorState.Desembarcando)
            return false;
        if (scannerPromptStep != ScannerPromptStep.DisembarkLandingSelect)
            return false;
        if (disembarkLandingOptions.Count == 0)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        int currentIndex = 0;
        for (int i = 0; i < disembarkLandingOptions.Count; i++)
        {
            PodeDesembarcarOption item = disembarkLandingOptions[i];
            if (item == null)
                continue;
            Vector3Int cell = item.disembarkCell;
            cell.z = 0;
            if (cell == disembarkSelectedLandingCell)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = (currentIndex + step + disembarkLandingOptions.Count) % disembarkLandingOptions.Count;
        PodeDesembarcarOption next = disembarkLandingOptions[nextIndex];
        if (next == null)
            return false;

        Vector3Int nextCell = next.disembarkCell;
        nextCell.z = 0;
        resolvedCell = nextCell;
        SetDisembarkSelectedLandingCell(nextCell, moveCursor: false);
        return true;
    }

    private void SetDisembarkSelectedLandingCell(Vector3Int cell, bool moveCursor)
    {
        cell.z = 0;
        if (!disembarkLandingByCell.ContainsKey(cell))
            return;

        disembarkSelectedLandingCell = cell;
        disembarkSelectedLandingCellValid = true;
        if (moveCursor && cursorController != null)
            cursorController.SetCell(cell, playMoveSfx: false);
    }

    private bool TryGetSelectedDisembarkLandingOption(out PodeDesembarcarOption option)
    {
        option = null;
        if (!disembarkSelectedLandingCellValid)
            return false;
        return disembarkLandingByCell.TryGetValue(disembarkSelectedLandingCell, out option) && option != null;
    }

    private bool TryGetSelectedPassengerEntry(out DisembarkPassengerEntry entry)
    {
        entry = null;
        if (disembarkSelectedPassengerIndex < 0 || disembarkSelectedPassengerIndex >= disembarkPassengerEntries.Count)
            return false;
        entry = disembarkPassengerEntries[disembarkSelectedPassengerIndex];
        return entry != null && entry.passenger != null;
    }

    private int CountRemainingPassengersForDisembark()
    {
        // Conta somente passageiros que ainda possuem ao menos um hex valido nao reservado.
        // Passageiro embarcado sem destino disponivel nao deve manter a montagem da fila aberta.
        int count = 0;
        if (selectedUnit == null)
            return 0;

        IReadOnlyList<UnitTransportSeatRuntime> seats = selectedUnit.TransportedUnitSlots;
        if (seats == null)
            return 0;

        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            if (seat == null || seat.embarkedUnit == null || !seat.embarkedUnit.IsEmbarked)
                continue;
            if (IsPassengerAlreadyQueued(seat.embarkedUnit))
                continue;
            if (HasAvailableDisembarkTargetForSeat(seat))
                count++;
        }

        return count;
    }

    private bool HasAvailableDisembarkTargetForSeat(UnitTransportSeatRuntime seat)
    {
        if (seat == null || seat.embarkedUnit == null)
            return false;

        for (int i = 0; i < cachedPodeDesembarcarTargets.Count; i++)
        {
            PodeDesembarcarOption option = cachedPodeDesembarcarTargets[i];
            if (option == null || option.passengerUnit != seat.embarkedUnit)
                continue;
            if (option.transporterSlotIndex != seat.slotIndex ||
                option.transporterSeatIndex != seat.seatIndex)
                continue;

            Vector3Int cell = option.disembarkCell;
            cell.z = 0;
            if (!IsCellAlreadyQueuedForDisembark(cell))
                return true;
        }

        return false;
    }

    private bool IsPassengerAlreadyQueued(UnitManager passenger)
    {
        if (passenger == null)
            return false;

        for (int i = 0; i < disembarkQueuedOrders.Count; i++)
        {
            DisembarkOrder order = disembarkQueuedOrders[i];
            if (order != null && order.passenger == passenger)
                return true;
        }

        return false;
    }

    private bool IsCellAlreadyQueuedForDisembark(Vector3Int cell)
    {
        cell.z = 0;
        for (int i = 0; i < disembarkQueuedOrders.Count; i++)
        {
            DisembarkOrder order = disembarkQueuedOrders[i];
            if (order == null)
                continue;

            Vector3Int queued = order.targetCell;
            queued.z = 0;
            if (queued == cell)
                return true;
        }

        return false;
    }

    private void LogDisembarkPassengerSelectionPanel()
    {
        string text = $"[Desembarque] Passageiros embarcados: {disembarkPassengerEntries.Count}\n";
        text += "Escolha por numero (1..9).\n";
        if (disembarkQueuedOrders.Count > 0)
        {
            text += "Digite 0 para executar as ordens em fila.\n";
            text += "ESC desfaz a ultima ordem e volta para editar o hex.\n";
        }
        else
        {
            text += "ESC volta para sensores.\n";
        }

        for (int i = 0; i < disembarkPassengerEntries.Count; i++)
            text += $"{disembarkPassengerEntries[i].selectionNumber}. {disembarkPassengerEntries[i].label}\n";

        RuntimeLog(text);
    }

    private void LogDisembarkLandingSelectionPanel(DisembarkPassengerEntry entry)
    {
        string passengerLabel = entry != null ? entry.label : "passageiro";
        string text =
            $"[Desembarque] Landing Select para {passengerLabel}\n" +
            $"Hex validos: {disembarkLandingOptions.Count}\n" +
            "Use setas para selecionar hex valido.\n" +
            "Enter para confirmar alvo. ESC para voltar.";
        RuntimeLog(text);
    }

    private void LogDisembarkConfirmPrompt()
    {
        if (!TryGetSelectedDisembarkLandingOption(out PodeDesembarcarOption option))
            return;

        string label = !string.IsNullOrWhiteSpace(option.displayLabel) ? option.displayLabel : ResolveUnitRuntimeName(option.passengerUnit);
        RuntimeLog($"[Desembarque] Confirmar {label}? (Enter=sim, ESC=voltar)");
    }

    private static string ResolveUnitRuntimeName(UnitManager unit)
    {
        if (unit == null)
            return "(unidade)";
        if (!string.IsNullOrWhiteSpace(unit.UnitDisplayName))
            return unit.UnitDisplayName.Trim();
        if (unit.TryGetUnitData(out UnitData data) && data != null)
            return ResolveUnitName(data);
        return string.IsNullOrWhiteSpace(unit.name) ? "(unidade)" : unit.name;
    }

    private bool CanDisembarkAircraftPassengerAtRuntime(UnitManager passenger, UnitManager transporter, Vector3Int transporterCell, out string reason)
    {
        reason = string.Empty;
        if (passenger == null)
        {
            reason = "passageiro nulo";
            return false;
        }

        if (!passenger.TryGetUnitData(out UnitData data) || data == null || !data.IsAircraft())
            return true;

        // Carrier naval lancando aeronave: valida fora desta guarda (sequencia de 1 hex em Air/Low).
        if (transporter != null && transporter.GetDomain() == Domain.Naval)
            return true;

        Tilemap map = terrainTilemap != null ? terrainTilemap : passenger.BoardTilemap;
        if (map == null)
        {
            reason = "tilemap indisponivel";
            return false;
        }

        transporterCell.z = 0;
        Vector3Int originalCell = passenger.CurrentCellPosition;
        originalCell.z = 0;
        if (originalCell != transporterCell)
            passenger.SetCurrentCellPosition(transporterCell, enforceFinalOccupancyRule: false);

        PodeDecolarReport report = PodeDecolarSensor.Evaluate(passenger, map, terrainDatabase);

        if (originalCell != transporterCell)
            passenger.SetCurrentCellPosition(originalCell, enforceFinalOccupancyRule: false);

        if (report == null || !report.status || report.takeoffMoveOptions == null || report.takeoffMoveOptions.Count == 0)
        {
            reason = report != null && !string.IsNullOrWhiteSpace(report.explicacao)
                ? report.explicacao
                : "takeoff plan indisponivel";
            return false;
        }

        bool canFullMove = report.takeoffMoveOptions.Contains(9);
        bool can1 = report.takeoffMoveOptions.Contains(1);
        if (canFullMove || can1)
            return true;

        reason = "somente decolagem 0 permitida neste hex para desembarque";
        return false;
    }

    private float GetDisembarkForcedLandingDuration()
    {
        return animationManager != null ? animationManager.DisembarkForcedLandingDuration : 0.25f;
    }

    private float GetDisembarkAfterForcedLandingDelay()
    {
        return animationManager != null ? animationManager.DisembarkAfterForcedLandingDelay : 0.10f;
    }

    private float GetDisembarkBeforeSpawnDelay()
    {
        return animationManager != null ? animationManager.DisembarkBeforeSpawnDelay : 0.10f;
    }

    private float GetDisembarkAfterSpawnDelay()
    {
        return animationManager != null ? animationManager.DisembarkAfterSpawnDelay : 0.15f;
    }

    private float GetDisembarkSpawnStepDelay()
    {
        return animationManager != null ? animationManager.DisembarkSpawnStepDelay : 0.08f;
    }

    private float GetDisembarkAfterPassengerMoveDelay()
    {
        return animationManager != null ? animationManager.DisembarkAfterPassengerMoveDelay : 0.10f;
    }

    private float GetDisembarkAfterPassengerLoadDelay()
    {
        return animationManager != null ? animationManager.DisembarkAfterPassengerLoadDelay : 0.12f;
    }

    private float GetDisembarkAfterMoveDelay()
    {
        return animationManager != null ? animationManager.DisembarkAfterMoveDelay : 0.15f;
    }

    private float GetDisembarkAirHighToGroundDuration()
    {
        return animationManager != null ? animationManager.DisembarkAirHighToGroundDuration : 0.10f;
    }

    private float GetDisembarkAirLowToGroundDuration()
    {
        return animationManager != null ? animationManager.DisembarkAirLowToGroundDuration : 0.05f;
    }

    private float GetDisembarkHighToLowNormalizedTime()
    {
        return animationManager != null ? animationManager.DisembarkHighToLowNormalizedTime : 0.50f;
    }

    private float GetDisembarkAfterTransporterDoneDelay()
    {
        return animationManager != null ? animationManager.DisembarkAfterTransporterDoneDelay : 0.10f;
    }

    private static bool TryReadPressedDigitIncludingZero(out int number)
    {
        number = -1;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame) { number = 0; return true; }
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) { number = 1; return true; }
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) { number = 2; return true; }
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) { number = 3; return true; }
            if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) { number = 4; return true; }
            if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) { number = 5; return true; }
            if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame) { number = 6; return true; }
            if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame) { number = 7; return true; }
            if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame) { number = 8; return true; }
            if (Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame) { number = 9; return true; }
        }
#else
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) { number = 0; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { number = 1; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { number = 2; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { number = 3; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) { number = 4; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) { number = 5; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) { number = 6; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) { number = 7; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) { number = 8; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) { number = 9; return true; }
#endif
        return false;
    }
    public bool TryQueueAutomatedDisembarkReplayOrder(string passengerInstanceId, Vector3Int targetCell)
    {
        if (!TrySelectAutomatedDisembarkPassengerForPresentation(passengerInstanceId))
            return false;
        if (!TrySelectAutomatedDisembarkLandingForPresentation(targetCell))
            return false;
        return ConfirmAutomatedDisembarkOrderForPresentation();
    }

    public bool TrySelectAutomatedDisembarkPassengerForPresentation(string passengerInstanceId)
    {
        if (CurrentCursorState != CursorState.Desembarcando || selectedUnit == null)
            return false;

        RebuildDisembarkPassengerEntries();
        if (disembarkPassengerEntries.Count <= 0)
            return false;

        int selectedIndex = -1;
        if (!string.IsNullOrWhiteSpace(passengerInstanceId))
        {
            for (int i = 0; i < disembarkPassengerEntries.Count; i++)
            {
                DisembarkPassengerEntry entry = disembarkPassengerEntries[i];
                if (entry == null || entry.passenger == null)
                    continue;

                if (entry.passenger.InstanceId.ToString() == passengerInstanceId)
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        if (selectedIndex < 0)
            selectedIndex = 0;

        disembarkSelectedPassengerIndex = selectedIndex;
        return EnterDisembarkLandingSelectStep(autoEntered: false);
    }

    public bool TrySelectAutomatedDisembarkLandingForPresentation(Vector3Int targetCell)
    {
        if (CurrentCursorState != CursorState.Desembarcando)
            return false;
        targetCell.z = 0;
        if (!disembarkLandingByCell.ContainsKey(targetCell))
            return false;

        SetDisembarkSelectedLandingCell(targetCell, moveCursor: false);
        if (scannerPromptStep == ScannerPromptStep.DisembarkConfirm)
            return true;
        if (scannerPromptStep != ScannerPromptStep.DisembarkLandingSelect)
            return false;
        return TryConfirmScannerDisembark() && scannerPromptStep == ScannerPromptStep.DisembarkConfirm;
    }

    public bool ConfirmAutomatedDisembarkOrderForPresentation()
    {
        return CurrentCursorState == CursorState.Desembarcando &&
               scannerPromptStep == ScannerPromptStep.DisembarkConfirm &&
               TryConfirmScannerDisembark();
    }

    public bool TryStartAutomatedDisembarkReplayExecution()
    {
        if (CurrentCursorState != CursorState.Desembarcando || disembarkExecutionInProgress)
            return false;
        if (disembarkQueuedOrders.Count <= 0)
            return false;

        StartDisembarkExecution();
        return true;
    }

    private bool ConsumeDisembarkSuppressDefaultConfirmSfxOnce()
    {
        if (!disembarkSuppressDefaultConfirmSfxOnce)
            return false;

        disembarkSuppressDefaultConfirmSfxOnce = false;
        return true;
    }
}
