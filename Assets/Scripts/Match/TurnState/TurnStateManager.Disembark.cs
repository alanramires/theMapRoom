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
    private bool disembarkLandingAutoEntered;
    private bool disembarkExecutionInProgress;
    private bool disembarkSuppressDefaultConfirmSfxOnce;
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
            lines[i] = $"{i + 1}. {passengerName} | slot={order.slotIndex}:{order.seatIndex} -> ({cell.x},{cell.y})";
        }

        return lines;
    }

    private void HandleDisembarkActionRequested()
    {
        if (selectedUnit == null)
            return;
        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
            return;

        if (cachedPodeDesembarcarTargets.Count == 0)
        {
            Debug.Log("Pode Desembarcar (\"D\"): nao ha opcoes validas agora.");
            LogScannerPanel();
            return;
        }

        cursorController?.PlayConfirmSfx();
        cursorStateBeforeDesembarcando = cursorState == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        SetCursorState(CursorState.Desembarcando, "HandleDisembarkActionRequested");
        ClearCommittedPathVisual();
        disembarkQueuedOrders.Clear();
        EnterDisembarkPassengerSelectStep();
    }

    private void ProcessDisembarkPromptInput()
    {
        if (cursorState != CursorState.Desembarcando)
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

                Debug.Log("[Desembarque] Nenhuma ordem em fila para executar.");
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
                Debug.Log($"[Desembarque] Passageiro invalido: {number}. Escolha uma das opcoes listadas.");
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
        if (cursorState != CursorState.Desembarcando)
            return false;

        if (scannerPromptStep == ScannerPromptStep.DisembarkPassengerSelect)
            return false;

        if (scannerPromptStep == ScannerPromptStep.DisembarkLandingSelect)
        {
            if (!disembarkSelectedLandingCellValid || !disembarkLandingByCell.ContainsKey(disembarkSelectedLandingCell))
            {
                Debug.Log("[Desembarque] Escolha um hex valido para desembarque.");
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
            Debug.Log($"[Desembarque] {ResolveUnitRuntimeName(entry.passenger)} ja possui ordem em fila. Escolha outro passageiro.");
            ReturnToDisembarkPassengerSelect();
            return true;
        }

        Vector3Int targetCell = option.disembarkCell;
        targetCell.z = 0;
        if (IsCellAlreadyQueuedForDisembark(targetCell))
        {
            Debug.Log($"[Desembarque] Hex ({targetCell.x},{targetCell.y}) ja reservado por outra ordem. Escolha outro hex.");
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

        Debug.Log($"[Desembarque] Ordem adicionada para {ResolveUnitRuntimeName(entry.passenger)} -> ({option.disembarkCell.x},{option.disembarkCell.y}).");
        EnterDisembarkPassengerSelectStep();
        return true;
    }

    private void EnterDisembarkPassengerSelectStep()
    {
        ClearDisembarkLandingOptionsAndPaint();
        RebuildDisembarkPassengerEntries();
        scannerPromptStep = ScannerPromptStep.DisembarkPassengerSelect;
        disembarkSelectedPassengerIndex = -1;
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
            Debug.Log($"[Desembarque] {ResolveUnitRuntimeName(entry.passenger)} sem hex valido para desembarque no momento.");
            return false;
        }

        scannerPromptStep = ScannerPromptStep.DisembarkLandingSelect;
        disembarkLandingAutoEntered = autoEntered;
        SetDisembarkSelectedLandingCell(disembarkLandingOptions[0].disembarkCell, moveCursor: true);
        PaintDisembarkLandingOptions();
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
            Debug.Log($"[Desembarque] Ordem desfeita para {ResolveUnitRuntimeName(lastOrder.passenger)}. Escolha novo hex.");
            return true;
        }

        EnterDisembarkPassengerSelectStep();
        Debug.Log("[Desembarque] Ordem desfeita. Retornando para selecao de passageiro.");
        return true;
    }

    private void ReturnToDisembarkLandingSelect()
    {
        if (scannerPromptStep == ScannerPromptStep.DisembarkLandingSelect)
            return;
        if (!TryGetSelectedPassengerEntry(out _))
        {
            EnterDisembarkPassengerSelectStep();
            return;
        }

        scannerPromptStep = ScannerPromptStep.DisembarkLandingSelect;
        LogDisembarkLandingSelectionPanel(disembarkPassengerEntries[disembarkSelectedPassengerIndex]);
    }

    private void ExitDisembarkStateToMovement()
    {
        if (cursorState != CursorState.Desembarcando)
            return;

        CursorState targetMovementState = cursorStateBeforeDesembarcando == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        SetCursorState(targetMovementState, "ExitDisembarkStateToMovement", rollback: true);
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
        if (transporter != null)
        {
            transporter.SetTemporarySortingOrder();
            transporterSortingRaised = true;
        }

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
                Debug.Log(string.IsNullOrWhiteSpace(landingDecision.reason)
                    ? "[Desembarque] Transportador aereo sem pouso valido."
                    : $"[Desembarque] {landingDecision.reason}");
                if (transporterSortingRaised && transporter != null)
                    transporter.ClearTemporarySortingOrder();
                disembarkExecutionInProgress = false;
                ExitDisembarkStateToMovement();
                yield break;
            }

            PlayMovementStartSfx(transporter);
            Debug.Log("[Desembarque] Transportador pousou antes do desembarque.");

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
                Debug.Log("[Desembarque] Falha ao concluir pouso do transportador (Land/Surface).");
                if (transporterSortingRaised && transporter != null)
                    transporter.ClearTemporarySortingOrder();
                disembarkExecutionInProgress = false;
                ExitDisembarkStateToMovement();
                yield break;
            }

            if (transporter.HasSkillId("vtol"))
            {
                float vtolFxDuration = animationManager != null ? animationManager.PlayVtolLandingEffect(transporter) : 0f;
                landingDuration = Mathf.Max(landingDuration, vtolFxDuration);
            }
            if (landingDuration > 0f)
                yield return new WaitForSeconds(landingDuration);

            float postLandingDelay = GetDisembarkAfterForcedLandingDelay();
            if (postLandingDelay > 0f)
                yield return new WaitForSeconds(postLandingDelay);
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
                if (!passenger.TrySetCurrentLayerMode(Domain.Air, HeightLevel.AirLow))
                    passenger.TrySetCurrentLayerMode(Domain.Air, passenger.GetPreferredAirHeight());
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
        disembarkExecutionInProgress = false;
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

            selectionNumber++;
            if (IsPassengerAlreadyQueued(seat.embarkedUnit))
                continue;

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
        {
            Debug.Log($"[Desembarque] {skippedByQueuedReservation} hex(es) filtrado(s) para {ResolveUnitRuntimeName(passengerEntry.passenger)} por reserva em ordens ja definidas.");
        }
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
        if (cursorState != CursorState.Desembarcando)
            return false;
        if (scannerPromptStep != ScannerPromptStep.DisembarkLandingSelect)
            return false;
        if (paintedRangeLookup.Count == 0)
            return false;

        Vector3Int desired = currentCell + inputDelta;
        desired.z = 0;
        if (paintedRangeLookup.Contains(desired))
        {
            resolvedCell = desired;
            SetDisembarkSelectedLandingCell(desired, moveCursor: false);
            return true;
        }

        if (HexPathResolver.TryResolveDirectionalFallback(
                terrainTilemap,
                paintedRangeLookup,
                currentCell,
                desired,
                out resolvedCell))
        {
            SetDisembarkSelectedLandingCell(resolvedCell, moveCursor: false);
            return true;
        }

        // Fallback robusto: cicla entre opcoes validas (mesmo padrao dos outros prompts).
        if (disembarkLandingOptions.Count > 1)
        {
            int step = GetMirandoStepFromInput(inputDelta);
            if (step != 0)
            {
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
                if (next != null)
                {
                    Vector3Int nextCell = next.disembarkCell;
                    nextCell.z = 0;
                    resolvedCell = nextCell;
                    SetDisembarkSelectedLandingCell(nextCell, moveCursor: false);
                    return true;
                }
            }
        }

        return false;
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
            count++;
        }

        return count;
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

        Debug.Log(text);
    }

    private void LogDisembarkLandingSelectionPanel(DisembarkPassengerEntry entry)
    {
        string passengerLabel = entry != null ? entry.label : "passageiro";
        string text =
            $"[Desembarque] Landing Select para {passengerLabel}\n" +
            $"Hex validos: {disembarkLandingOptions.Count}\n" +
            "Use setas para selecionar hex valido.\n" +
            "Enter para confirmar alvo. ESC para voltar.";
        Debug.Log(text);
    }

    private void LogDisembarkConfirmPrompt()
    {
        if (!TryGetSelectedDisembarkLandingOption(out PodeDesembarcarOption option))
            return;

        string label = !string.IsNullOrWhiteSpace(option.displayLabel) ? option.displayLabel : ResolveUnitRuntimeName(option.passengerUnit);
        Debug.Log($"[Desembarque] Confirmar {label}? (Enter=sim, ESC=voltar)");
    }

    private static string ResolveUnitRuntimeName(UnitManager unit)
    {
        if (unit == null)
            return "(unidade)";
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

    private bool ConsumeDisembarkSuppressDefaultConfirmSfxOnce()
    {
        if (!disembarkSuppressDefaultConfirmSfxOnce)
            return false;

        disembarkSuppressDefaultConfirmSfxOnce = false;
        return true;
    }
}
