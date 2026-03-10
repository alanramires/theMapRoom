using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    private bool transferPromptConfirmationPending;
    private bool transferCursorSelectionMode;
    private bool transferExecutionInProgress;
    private LineRenderer transferPreviewRenderer;

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
            transferCursorSelectionMode = false;
            FocusTransferOptionByIndex(transferPromptSelectedIndex, playSfx: true);
            EnterTransferConfirmStep();
            return;
        }

        if (!TrySelectTransferPromptOptionByNumber(number))
        {
            cursorController?.PlayErrorSfx();
            Debug.Log($"[Transfer] Opcao invalida: {number}.");
        }
    }

    private void UpdateTransferPromptPreview()
    {
        bool shouldShow = IsTransferPromptActive() &&
                          !transferExecutionInProgress &&
                          selectedUnit != null &&
                          transferPromptSelectedIndex >= 0 &&
                          transferPromptSelectedIndex < transferPromptOptions.Count &&
                          !IsTransferEmbarkedOnlyCollection(selectedUnit);
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

        EnsureTransferPreviewRenderer();
        if (transferPreviewRenderer == null)
            return;

        float width = Mathf.Max(0.025f, GetMirandoPreviewWidth() * 0.85f);
        Color color = GetMirandoPreviewColor();
        color.a = Mathf.Clamp01(color.a * 0.85f);
        transferPreviewRenderer.startWidth = width;
        transferPreviewRenderer.endWidth = width;
        transferPreviewRenderer.startColor = color;
        transferPreviewRenderer.endColor = color;
        transferPreviewRenderer.positionCount = 2;
        transferPreviewRenderer.SetPosition(0, from);
        transferPreviewRenderer.SetPosition(1, to);
        transferPreviewRenderer.enabled = true;
    }

    private bool IsTransferPromptActive()
    {
        return (transferPromptSelectionPending || transferPromptConfirmationPending) &&
               transferPromptOptions.Count > 0 &&
               (cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado);
    }

    private bool IsTransferSelectionStepActive()
    {
        return IsTransferPromptActive() && transferPromptSelectionPending && !transferPromptConfirmationPending;
    }

    private bool IsTransferConfirmStepActive()
    {
        return IsTransferPromptActive() && transferPromptConfirmationPending;
    }

    private bool TrySelectTransferPromptOptionByNumber(int oneBasedNumber)
    {
        if (!IsTransferSelectionStepActive())
            return false;

        int optionCount = transferPromptOptions.Count;
        if (oneBasedNumber >= 1 && oneBasedNumber <= optionCount)
        {
            transferPromptSelectedIndex = oneBasedNumber - 1;
            transferCursorSelectionMode = false;
            FocusTransferOptionByIndex(transferPromptSelectedIndex, playSfx: true);
            EnterTransferConfirmStep();
            return true;
        }

        int cursorOptionNumber = optionCount + 1;
        if (oneBasedNumber == cursorOptionNumber && HasMultipleTransferTargetCells())
        {
            transferCursorSelectionMode = true;
            cursorController?.PlayConfirmSfx();
            PanelDialogController.TrySetExternalText("Transferir :: escolha no cursor + Enter");
            LogTransferPromptOptions();
            return true;
        }

        return false;
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
            if (transferPromptSelectedIndex < 0 || transferPromptSelectedIndex >= transferPromptOptions.Count || transferCursorSelectionMode)
            {
                if (!TrySelectTransferOptionFromCursor())
                {
                    Debug.Log("[Transfer] Selecione um destino valido por numero ou cursor.");
                    cursorController?.PlayErrorSfx();
                    return true;
                }
            }

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

        StartCoroutine(ExecuteTransferPromptSequence(option));
        return true;
    }

    private IEnumerator ExecuteTransferPromptSequence(PodeTransferirOption option)
    {
        transferExecutionInProgress = true;
        try
        {
            bool executed = TryExecuteTransferOptionRuntime(option, selectedUnit, out int movedTotal, out string message, out Dictionary<SupplyData, int> movedBySupply);
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

    private bool TryCancelPendingTransferPrompt()
    {
        if (!IsTransferPromptActive())
            return false;

        if (IsTransferConfirmStepActive())
        {
            if (transferPromptOptions.Count <= 1)
            {
                ClearPendingTransferPrompt();
                Debug.Log("[Transfer] Cancelado.");
                return true;
            }

            transferPromptConfirmationPending = false;
            transferPromptSelectionPending = true;
            transferCursorSelectionMode = false;
            transferPreviewLines.Clear();
            LogTransferPromptOptions();
            Debug.Log("[Transfer] Confirmacao cancelada. Retornando para selecao.");
            return true;
        }

        ClearPendingTransferPrompt();
        Debug.Log("[Transfer] Cancelado.");
        return true;
    }

    private void ClearPendingTransferPrompt()
    {
        transferPromptSelectionPending = false;
        transferPromptConfirmationPending = false;
        transferCursorSelectionMode = false;
        transferPromptSelectedIndex = -1;
        transferPromptOptions.Clear();
        transferPromptIndexByCell.Clear();
        transferPreviewLines.Clear();
        SetTransferPreviewVisible(false);
        PanelDialogController.ClearExternalText();
    }

    private void EnterTransferSelectionStep()
    {
        transferPromptSelectionPending = true;
        transferPromptConfirmationPending = false;
        transferCursorSelectionMode = false;
        transferPromptSelectedIndex = transferPromptOptions.Count > 0 ? 0 : -1;
        transferPreviewLines.Clear();
        RebuildTransferCellIndex();
        TrySelectTransferOptionFromCursor();
        if (transferPromptOptions.Count == 1)
        {
            EnterTransferConfirmStep();
            return;
        }

        LogTransferPromptOptions();
    }

    private void EnterTransferConfirmStep()
    {
        if (transferPromptSelectedIndex < 0 || transferPromptSelectedIndex >= transferPromptOptions.Count)
            return;

        transferPromptSelectionPending = false;
        transferPromptConfirmationPending = true;
        transferCursorSelectionMode = false;
        RebuildTransferPreviewLines();
        string label = ResolveTransferOptionLabel(transferPromptOptions[transferPromptSelectedIndex], transferPromptSelectedIndex + 1);
        PanelDialogController.TrySetExternalText($"Transferir :: Confirmar {label}");
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
            string marker = i == selectedIndex && !transferCursorSelectionMode ? ">" : " ";
            Debug.Log($"{marker} {label}");
        }

        if (HasMultipleTransferTargetCells())
        {
            string marker = transferCursorSelectionMode ? ">" : " ";
            Debug.Log($"{marker} {transferPromptOptions.Count + 1}. Selecionar pelo cursor");
        }

        PanelDialogController.TrySetExternalText("Transferir :: escolha numero + Enter");
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
        transferCursorSelectionMode = false;
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
            transferCursorSelectionMode = false;
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

    private void EnsureTransferPreviewRenderer()
    {
        if (transferPreviewRenderer != null)
            return;

        GameObject go = new GameObject("TransferConfirmPreviewLine");
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
        transferPreviewRenderer = renderer;
    }

    private void SetTransferPreviewVisible(bool visible)
    {
        if (transferPreviewRenderer == null)
            return;

        if (!visible)
        {
            transferPreviewRenderer.positionCount = 0;
            transferPreviewRenderer.enabled = false;
            return;
        }

        transferPreviewRenderer.enabled = true;
    }

    private static bool IsTransferEmbarkedOnlyCollection(UnitManager supplier)
    {
        if (supplier == null)
            return false;
        if (!supplier.TryGetUnitData(out UnitData data) || data == null)
            return false;
        return data.collectionRange == SupplierRangeMode.EmbarkedOnly;
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

        if (!TryEstimateTransferOption(option, selectedUnit, out Dictionary<SupplyData, int> sourceStock, out Dictionary<SupplyData, int> destinationStock, out Dictionary<SupplyData, int> movedBySupply))
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
            if (destinationUnit != null)
            {
                int capacity = destinationCapacity != null && destinationCapacity.TryGetValue(supply, out int maxCap) ? Mathf.Max(0, maxCap) : 0;
                int current = destinationStock != null && destinationStock.TryGetValue(supply, out int currentDst) ? Mathf.Max(0, currentDst) : 0;
                int remaining = Mathf.Max(0, capacity - current);
                transferable = Mathf.Min(transferable, remaining);
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

        foreach (KeyValuePair<SupplyData, int> pair in movedBySupply)
        {
            SupplyData supply = pair.Key;
            int moved = Mathf.Max(0, pair.Value);
            if (supply == null || moved <= 0)
                continue;

            float duration = animationManager.PlayServiceProjectileStraight(sourcePos, destinationPos, supply.spriteDefault);
            if (spawnInterval > 0f)
                yield return new WaitForSeconds(spawnInterval);
            if (duration > 0f)
                yield return new WaitForSeconds(duration + flightPadding);
        }
    }

    private bool TryExecuteTransferOptionRuntime(PodeTransferirOption option, UnitManager supplier, out int movedTotal, out string message, out Dictionary<SupplyData, int> movedBySupply)
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
