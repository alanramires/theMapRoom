using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeTransferirSensorDebugWindow : EditorWindow
{
    private sealed class TransferCandidateEntry
    {
        public PodeTransferirOption option;
        public string label;
    }

    private sealed class IneligibleTransferEntry
    {
        public PodeTransferirInvalidOption invalid;
        public string label;
    }

    private sealed class TransferReportData
    {
        public readonly List<string> selectedOrderLines = new List<string>();
        public readonly List<TransferOrderReportEntry> orderEntries = new List<TransferOrderReportEntry>();
        public readonly Dictionary<SupplyData, long> movedBySupply = new Dictionary<SupplyData, long>();
        public readonly Dictionary<SupplyData, long> blockedByCapacity = new Dictionary<SupplyData, long>();
        public Dictionary<SupplyData, long> selectedSupplierBefore = new Dictionary<SupplyData, long>();
        public Dictionary<SupplyData, long> selectedSupplierAfter = new Dictionary<SupplyData, long>();
        public readonly List<EndpointStockSnapshot> destinationSnapshots = new List<EndpointStockSnapshot>();
    }

    private sealed class TransferOrderReportEntry
    {
        public int orderIndex;
        public TransferFlowMode mode;
        public string orderLabel;
        public string supplierLabel;
        public string destinationLabel;
        public bool supplierIsInfinite;
        public readonly Dictionary<SupplyData, long> supplierBefore = new Dictionary<SupplyData, long>();
        public readonly Dictionary<SupplyData, long> supplierAfter = new Dictionary<SupplyData, long>();
        public readonly Dictionary<SupplyData, long> destinationBefore = new Dictionary<SupplyData, long>();
        public readonly Dictionary<SupplyData, long> destinationAfter = new Dictionary<SupplyData, long>();
        public readonly Dictionary<SupplyData, long> sentBySupply = new Dictionary<SupplyData, long>();
        public readonly Dictionary<SupplyData, long> blockedByCapacity = new Dictionary<SupplyData, long>();
        public readonly Dictionary<SupplyData, long> availableAtSource = new Dictionary<SupplyData, long>();
    }

    private sealed class EndpointStockSnapshot
    {
        public string key;
        public string label;
        public Dictionary<SupplyData, long> before = new Dictionary<SupplyData, long>();
        public Dictionary<SupplyData, long> after = new Dictionary<SupplyData, long>();
    }

    [SerializeField] private UnitManager selectedSupplier;
    [SerializeField] private Tilemap overrideTilemap;

    private readonly List<TransferCandidateEntry> eligibleCandidates = new List<TransferCandidateEntry>();
    private readonly List<IneligibleTransferEntry> ineligibleCandidates = new List<IneligibleTransferEntry>();
    private readonly List<TransferCandidateEntry> transferOrder = new List<TransferCandidateEntry>();

    private string statusMessage = "Ready.";
    private string sensorReason = "Ready.";
    private string orderMessage = "Ordem vazia.";
    private bool canTransfer;
    private int selectedEligibleIndex = -1;
    private int selectedInvalidIndex = -1;
    private Vector2 windowScroll;

    private bool hasSelectedLine;
    private Vector3Int selectedLineStartCell;
    private Vector3Int selectedLineEndCell;
    private Color selectedLineColor = Color.cyan;
    private string selectedLineLabel = string.Empty;

    [MenuItem("Tools/Logistica/Pode Transferir")]
    public static void OpenWindow()
    {
        GetWindow<PodeTransferirSensorDebugWindow>("Pode Transferir");
    }

    private void OnEnable()
    {
        AutoDetectContext();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        ClearSelectedLine();
    }

    private void OnGUI()
    {
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
        EditorGUILayout.LabelField("Sensor Pode Transferir", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Regras:\n" +
            "1) Unidade selecionada deve ser supplier com serviceType=Transfer\n" +
            "2) Hierarquia: Hub > Receiver\n" +
            "3) Precisa estar em construcao aliada ou com hub no collection range\n" +
            "4) Hub: Recebedor sempre, Fornecimento apenas para receiver no alcance\n" +
            "5) Construcao com suprimento infinito (-1) bloqueia Fornecimento do Hub",
            MessageType.Info);

        selectedSupplier = (UnitManager)EditorGUILayout.ObjectField("Supplier", selectedSupplier, typeof(UnitManager), true);
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Simular"))
            RunSimulation();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        EditorGUILayout.HelpBox(orderMessage, MessageType.None);
        if (!string.IsNullOrWhiteSpace(sensorReason))
            EditorGUILayout.HelpBox($"Sensor: {sensorReason}", canTransfer ? MessageType.Info : MessageType.Warning);
        EditorGUILayout.LabelField("Pode Transferir", canTransfer ? "SIM" : "NAO");
        using (new EditorGUI.DisabledScope(transferOrder.Count == 0))
        {
            if (GUILayout.Button("Transferir (Debug)"))
                ExecuteTransferOrderDebug();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.MinWidth(420f), GUILayout.ExpandWidth(true));
        DrawOrderSection();
        EditorGUILayout.Space(8f);
        DrawEligibleSection();
        EditorGUILayout.Space(8f);
        DrawInvalidSection();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(12f);
        EditorGUILayout.BeginVertical(GUILayout.Width(420f));
        DrawTransferReportSection();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    private void DrawEligibleSection()
    {
        EditorGUILayout.LabelField($"Candidatos validos ({eligibleCandidates.Count})", EditorStyles.boldLabel);
        if (eligibleCandidates.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum candidato valido.", MessageType.Info);
            return;
        }

        for (int i = 0; i < eligibleCandidates.Count; i++)
        {
            TransferCandidateEntry entry = eligibleCandidates[i];
            if (entry == null || entry.option == null)
                continue;

            int orderLimit = GetTransferOrderLimit();
            bool limitReached = transferOrder.Count >= orderLimit;
            bool alreadyQueued = ContainsEquivalentOrderEntry(entry.option);

            EditorGUILayout.BeginVertical("box");
            bool isSelected = selectedEligibleIndex == i;
            bool toggled = GUILayout.Toggle(isSelected, $"{i + 1}. {entry.label}", "Button");
            if (toggled && selectedEligibleIndex != i)
            {
                selectedEligibleIndex = i;
                selectedInvalidIndex = -1;
                SelectLineForDrawing(entry.option, Color.cyan, entry.label);
            }

            PodeTransferirOption option = entry.option;
            EditorGUILayout.LabelField("Fluxo", option.flowMode.ToString());
            EditorGUILayout.LabelField("Hex", $"{option.targetCell.x},{option.targetCell.y}");
            EditorGUILayout.LabelField("Destino Unidade", option.targetUnit != null ? option.targetUnit.name : "-");
            EditorGUILayout.LabelField("Destino Construcao", option.targetConstruction != null ? ResolveConstructionDisplayName(option.targetConstruction) : "-");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Desenhar Linha"))
            {
                selectedEligibleIndex = i;
                selectedInvalidIndex = -1;
                SelectLineForDrawing(option, Color.cyan, entry.label);
            }
            using (new EditorGUI.DisabledScope(limitReached || alreadyQueued))
            {
                if (GUILayout.Button("Adicionar a ordem"))
                    TryAddCandidateToOrder(entry);
            }
            EditorGUILayout.EndHorizontal();
            if (alreadyQueued)
                EditorGUILayout.HelpBox("Ja esta na ordem.", MessageType.Info);
            else if (limitReached)
                EditorGUILayout.HelpBox($"Limite da ordem atingido ({orderLimit}).", MessageType.Warning);
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawOrderSection()
    {
        EditorGUILayout.LabelField($"Ordem de Transferencia ({transferOrder.Count})", EditorStyles.boldLabel);
        if (transferOrder.Count == 0)
        {
            EditorGUILayout.HelpBox("Ordem vazia.", MessageType.Info);
            return;
        }

        for (int i = 0; i < transferOrder.Count; i++)
        {
            TransferCandidateEntry entry = transferOrder[i];
            if (entry == null || entry.option == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"{i + 1}. {entry.label}");
            EditorGUILayout.LabelField("Fluxo", entry.option.flowMode.ToString());
            EditorGUILayout.LabelField("Hex", $"{entry.option.targetCell.x},{entry.option.targetCell.y}");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Desenhar Linha"))
                SelectLineForDrawing(entry.option, Color.green, entry.label);
            if (GUILayout.Button("Remover da ordem"))
            {
                transferOrder.RemoveAt(i);
                orderMessage = "Item removido da ordem.";
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawTransferReportSection()
    {
        EditorGUILayout.LabelField("Relatorio de Transferencia", EditorStyles.boldLabel);
        if (transferOrder.Count == 0)
        {
            EditorGUILayout.HelpBox("Monte uma ordem para gerar o relatorio.", MessageType.Info);
            return;
        }

        TransferReportData report = BuildTransferReport();
        if (report == null)
        {
            EditorGUILayout.HelpBox("Nao foi possivel gerar o relatorio.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Ordem Escolhida", EditorStyles.boldLabel);
        if (report.selectedOrderLines.Count == 0)
            EditorGUILayout.HelpBox("Sem itens na ordem.", MessageType.Info);
        else
        {
            for (int i = 0; i < report.selectedOrderLines.Count; i++)
                EditorGUILayout.LabelField(report.selectedOrderLines[i], EditorStyles.wordWrappedMiniLabel);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Detalhe por Ordem", EditorStyles.boldLabel);
        if (report.orderEntries.Count == 0)
        {
            EditorGUILayout.HelpBox("Sem detalhes para exibir.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < report.orderEntries.Count; i++)
            {
                TransferOrderReportEntry entry = report.orderEntries[i];
                if (entry == null)
                    continue;
                DrawOrderEntryReport(entry);
            }
        }

    }

    private void DrawInvalidSection()
    {
        EditorGUILayout.LabelField($"Candidatos invalidos ({ineligibleCandidates.Count})", EditorStyles.boldLabel);
        if (ineligibleCandidates.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum candidato invalido.", MessageType.Info);
            return;
        }

        for (int i = 0; i < ineligibleCandidates.Count; i++)
        {
            IneligibleTransferEntry entry = ineligibleCandidates[i];
            if (entry == null || entry.invalid == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            bool isSelected = selectedInvalidIndex == i;
            bool toggled = GUILayout.Toggle(isSelected, $"{i + 1}. {entry.label}", "Button");
            if (toggled && selectedInvalidIndex != i)
            {
                selectedInvalidIndex = i;
                selectedEligibleIndex = -1;
                SelectLineForDrawing(entry.invalid, Color.red, entry.label);
            }

            PodeTransferirInvalidOption invalid = entry.invalid;
            EditorGUILayout.LabelField("Fluxo", invalid.flowMode.ToString());
            EditorGUILayout.LabelField("Hex", $"{invalid.targetCell.x},{invalid.targetCell.y}");
            EditorGUILayout.LabelField("Bloqueado", string.IsNullOrWhiteSpace(invalid.reason) ? "-" : invalid.reason);
            if (GUILayout.Button("Desenhar Linha"))
            {
                selectedInvalidIndex = i;
                selectedEligibleIndex = -1;
                SelectLineForDrawing(invalid, Color.red, entry.label);
            }
            EditorGUILayout.EndVertical();
        }
    }

    private void RunSimulation()
    {
        eligibleCandidates.Clear();
        ineligibleCandidates.Clear();
        selectedEligibleIndex = -1;
        selectedInvalidIndex = -1;
        canTransfer = false;
        sensorReason = string.Empty;
        ClearSelectedLine();

        if (selectedSupplier == null)
        {
            statusMessage = "Selecione um supplier valido.";
            transferOrder.Clear();
            orderMessage = "Ordem vazia.";
            return;
        }

        Tilemap map = ResolveTilemap();
        if (map == null)
        {
            statusMessage = "Tilemap base nao encontrado.";
            transferOrder.Clear();
            orderMessage = "Ordem vazia.";
            return;
        }

        List<PodeTransferirOption> options = new List<PodeTransferirOption>();
        List<PodeTransferirInvalidOption> invalids = new List<PodeTransferirInvalidOption>();
        canTransfer = PodeTransferirSensor.CollectOptions(selectedSupplier, map, options, out sensorReason, invalids);

        for (int i = 0; i < options.Count; i++)
        {
            PodeTransferirOption option = options[i];
            if (option == null)
                continue;

            eligibleCandidates.Add(new TransferCandidateEntry
            {
                option = option,
                label = BuildOptionLabel(option)
            });
        }

        for (int i = 0; i < invalids.Count; i++)
        {
            PodeTransferirInvalidOption invalid = invalids[i];
            if (invalid == null)
                continue;

            ineligibleCandidates.Add(new IneligibleTransferEntry
            {
                invalid = invalid,
                label = BuildInvalidLabel(invalid)
            });
        }

        statusMessage = canTransfer
            ? $"Sensor TRUE. {eligibleCandidates.Count} opcao(oes) valida(s)."
            : "Sensor FALSE. Transferencia indisponivel.";

        if (eligibleCandidates.Count > 0)
        {
            selectedEligibleIndex = 0;
            SelectLineForDrawing(eligibleCandidates[0].option, Color.cyan, eligibleCandidates[0].label);
        }

        PruneOrderAgainstCurrentCandidates();

        Debug.Log(
            $"[PodeTransferirSensorDebug] supplier={(selectedSupplier != null ? selectedSupplier.name : "(null)")} | " +
            $"canTransfer={canTransfer} | valid={eligibleCandidates.Count} | invalid={ineligibleCandidates.Count} | reason={sensorReason}");
    }

    private void TryUseCurrentSelection()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            statusMessage = "Selecione uma unidade na hierarquia.";
            return;
        }

        UnitManager unit = go.GetComponent<UnitManager>();
        if (unit == null)
        {
            statusMessage = "GameObject selecionado nao possui UnitManager.";
            return;
        }

        selectedSupplier = unit;
        statusMessage = $"Supplier selecionado: {unit.name}.";
        transferOrder.Clear();
        orderMessage = "Ordem reiniciada para o novo supplier.";
        Repaint();
    }

    private void AutoDetectContext()
    {
        if (selectedSupplier == null)
        {
            TurnStateManager state = FindAnyObjectByType<TurnStateManager>();
            if (state != null)
                selectedSupplier = state.SelectedUnit;
        }

        if (selectedSupplier == null)
            selectedSupplier = FindAnyObjectByType<UnitManager>();

        if (overrideTilemap == null)
        {
            if (selectedSupplier != null)
                overrideTilemap = selectedSupplier.BoardTilemap;
            if (overrideTilemap == null)
                overrideTilemap = FindAnyObjectByType<Tilemap>();
        }

        if (selectedSupplier != null)
            statusMessage = $"Contexto detectado. Supplier: {selectedSupplier.name}.";
        else
            statusMessage = "Contexto detectado sem supplier selecionado.";

        Repaint();
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;
        if (selectedSupplier != null && selectedSupplier.BoardTilemap != null)
            return selectedSupplier.BoardTilemap;
        return FindAnyObjectByType<Tilemap>();
    }

    private void TryAddCandidateToOrder(TransferCandidateEntry entry)
    {
        if (entry == null || entry.option == null)
        {
            orderMessage = "Candidato invalido.";
            return;
        }

        int limit = GetTransferOrderLimit();
        if (transferOrder.Count >= limit)
        {
            orderMessage = $"Limite da ordem atingido ({limit}).";
            return;
        }

        if (ContainsEquivalentOrderEntry(entry.option))
        {
            orderMessage = "Candidato ja esta na ordem.";
            return;
        }

        transferOrder.Add(new TransferCandidateEntry
        {
            option = entry.option,
            label = entry.label
        });
        orderMessage = $"Adicionado a ordem: {entry.label}.";
    }

    private int GetTransferOrderLimit()
    {
        if (selectedSupplier != null &&
            selectedSupplier.TryGetUnitData(out UnitData data) &&
            data != null)
        {
            return Mathf.Max(0, data.maxUnitsServedPerTurn);
        }

        return 0;
    }

    private bool ContainsEquivalentOrderEntry(PodeTransferirOption candidate)
    {
        if (candidate == null)
            return false;

        for (int i = 0; i < transferOrder.Count; i++)
        {
            TransferCandidateEntry existing = transferOrder[i];
            if (existing == null || existing.option == null)
                continue;

            PodeTransferirOption option = existing.option;
            if (option.flowMode != candidate.flowMode)
                continue;
            if (option.targetUnit != candidate.targetUnit)
                continue;
            if (option.targetConstruction != candidate.targetConstruction)
                continue;
            if (option.targetCell != candidate.targetCell)
                continue;
            return true;
        }

        return false;
    }

    private void PruneOrderAgainstCurrentCandidates()
    {
        if (transferOrder.Count == 0)
            return;

        List<TransferCandidateEntry> validNow = new List<TransferCandidateEntry>(eligibleCandidates);
        for (int i = transferOrder.Count - 1; i >= 0; i--)
        {
            TransferCandidateEntry queued = transferOrder[i];
            if (queued == null || queued.option == null)
            {
                transferOrder.RemoveAt(i);
                continue;
            }

            bool stillValid = false;
            for (int c = 0; c < validNow.Count; c++)
            {
                TransferCandidateEntry now = validNow[c];
                if (now == null || now.option == null)
                    continue;
                if (now.option.flowMode != queued.option.flowMode)
                    continue;
                if (now.option.targetUnit != queued.option.targetUnit)
                    continue;
                if (now.option.targetConstruction != queued.option.targetConstruction)
                    continue;
                if (now.option.targetCell != queued.option.targetCell)
                    continue;

                stillValid = true;
                break;
            }

            if (!stillValid)
                transferOrder.RemoveAt(i);
        }

        int limit = GetTransferOrderLimit();
        if (transferOrder.Count > limit)
            transferOrder.RemoveRange(limit, transferOrder.Count - limit);

        orderMessage = transferOrder.Count > 0
            ? $"Ordem pronta: {transferOrder.Count}/{limit}."
            : "Ordem vazia.";
    }

    private TransferReportData BuildTransferReport()
    {
        if (selectedSupplier == null)
            return null;

        TransferReportData report = new TransferReportData();
        Dictionary<UnitManager, Dictionary<SupplyData, long>> unitStocks = new Dictionary<UnitManager, Dictionary<SupplyData, long>>();
        Dictionary<ConstructionManager, Dictionary<SupplyData, long>> constructionStocks = new Dictionary<ConstructionManager, Dictionary<SupplyData, long>>();
        Dictionary<UnitManager, Dictionary<SupplyData, long>> unitCapacities = new Dictionary<UnitManager, Dictionary<SupplyData, long>>();
        Dictionary<string, EndpointStockSnapshot> destinationSnapshotByKey = new Dictionary<string, EndpointStockSnapshot>();

        Dictionary<SupplyData, long> selectedSupplierMap = GetOrCreateUnitStockMap(unitStocks, selectedSupplier);
        report.selectedSupplierBefore = CloneStockMap(selectedSupplierMap);

        for (int i = 0; i < transferOrder.Count; i++)
        {
            TransferCandidateEntry queued = transferOrder[i];
            if (queued == null || queued.option == null)
                continue;

            PodeTransferirOption option = queued.option;
            ResolveTransferEndpoints(option, selectedSupplier, out UnitManager sourceUnit, out ConstructionManager sourceConstruction, out UnitManager destinationUnit, out ConstructionManager destinationConstruction);

            if (sourceUnit == null && sourceConstruction == null)
            {
                continue;
            }

            Dictionary<SupplyData, long> sourceMap = sourceUnit != null
                ? GetOrCreateUnitStockMap(unitStocks, sourceUnit)
                : GetOrCreateConstructionStockMap(constructionStocks, sourceConstruction);
            Dictionary<SupplyData, long> destinationMap = destinationUnit != null
                ? GetOrCreateUnitStockMap(unitStocks, destinationUnit)
                : GetOrCreateConstructionStockMap(constructionStocks, destinationConstruction);
            Dictionary<SupplyData, long> destinationCapacityMap = destinationUnit != null
                ? GetOrCreateUnitCapacityMap(unitCapacities, destinationUnit)
                : null;

            if (sourceMap == null || sourceMap.Count == 0)
            {
                continue;
            }

            bool sourceIsInfiniteConstruction = sourceConstruction != null && IsInfiniteConstruction(sourceConstruction);
            TransferOrderReportEntry orderEntry = new TransferOrderReportEntry
            {
                orderIndex = i + 1,
                mode = option.flowMode,
                orderLabel = queued.label,
                supplierLabel = sourceUnit != null ? sourceUnit.name : ResolveConstructionDisplayName(sourceConstruction),
                destinationLabel = ResolveDestinationLabel(destinationUnit, destinationConstruction),
                supplierIsInfinite = sourceIsInfiniteConstruction
            };
            CopyMap(orderEntry.supplierBefore, sourceMap);
            CopyMap(orderEntry.destinationBefore, destinationMap);

            string destinationKey = BuildEndpointKey(destinationUnit, destinationConstruction);
            bool destinationIsSelectedSupplier =
                destinationUnit != null &&
                selectedSupplier != null &&
                destinationUnit == selectedSupplier;
            if (!destinationIsSelectedSupplier &&
                !string.IsNullOrWhiteSpace(destinationKey) &&
                !destinationSnapshotByKey.ContainsKey(destinationKey))
            {
                EndpointStockSnapshot snapshot = new EndpointStockSnapshot
                {
                    key = destinationKey,
                    label = ResolveDestinationLabel(destinationUnit, destinationConstruction),
                    before = CloneStockMap(destinationMap)
                };
                destinationSnapshotByKey[destinationKey] = snapshot;
                report.destinationSnapshots.Add(snapshot);
            }

            List<SupplyData> supplies = new List<SupplyData>(sourceMap.Keys);
            for (int s = 0; s < supplies.Count; s++)
            {
                SupplyData supply = supplies[s];
                if (supply == null)
                    continue;

                long amount = sourceMap.TryGetValue(supply, out long value) ? value : 0;
                if (amount <= 0)
                    continue;
                if (!orderEntry.availableAtSource.ContainsKey(supply))
                    orderEntry.availableAtSource[supply] = amount;

                long transferable = amount;
                if (destinationUnit != null)
                {
                    long currentDestination = destinationMap != null && destinationMap.TryGetValue(supply, out long existingDestination)
                        ? existingDestination
                        : 0;
                    long capacity = destinationCapacityMap != null && destinationCapacityMap.TryGetValue(supply, out long maxCapacity)
                        ? System.Math.Max(0L, maxCapacity)
                        : 0;
                    long remainingCapacity = System.Math.Max(0L, capacity - currentDestination);
                    if (remainingCapacity <= 0)
                        continue;
                    transferable = System.Math.Min(transferable, remainingCapacity);
                }

                if (transferable > 0)
                {
                    AddAmount(report.movedBySupply, supply, transferable);
                    AddAmount(orderEntry.sentBySupply, supply, transferable);
                    if (!sourceIsInfiniteConstruction)
                        sourceMap[supply] = System.Math.Max(0L, amount - transferable);
                    AddAmount(destinationMap, supply, transferable);
                }
                else
                {
                    sourceMap[supply] = amount;
                }

                long blockedAmount = System.Math.Max(0L, amount - transferable);
                if (blockedAmount > 0 && destinationUnit != null)
                {
                    AddAmount(report.blockedByCapacity, supply, blockedAmount);
                    if (!sourceIsInfiniteConstruction)
                        AddAmount(orderEntry.blockedByCapacity, supply, blockedAmount);
                }
            }
            string flowLabel = option.flowMode == TransferFlowMode.Fornecimento ? "Fornecer" : "Receber";
            report.selectedOrderLines.Add($"{i + 1}. {flowLabel} | {queued.label}");
            CopyMap(orderEntry.supplierAfter, sourceMap);
            CopyMap(orderEntry.destinationAfter, destinationMap);
            report.orderEntries.Add(orderEntry);
        }

        report.selectedSupplierAfter = CloneStockMap(GetOrCreateUnitStockMap(unitStocks, selectedSupplier));
        for (int i = 0; i < report.destinationSnapshots.Count; i++)
        {
            EndpointStockSnapshot snapshot = report.destinationSnapshots[i];
            if (snapshot == null)
                continue;

            TryResolveEndpointByKey(snapshot.key, out UnitManager destinationUnit, out ConstructionManager destinationConstruction);
            Dictionary<SupplyData, long> finalMap = destinationUnit != null
                ? GetOrCreateUnitStockMap(unitStocks, destinationUnit)
                : GetOrCreateConstructionStockMap(constructionStocks, destinationConstruction);
            snapshot.after = CloneStockMap(finalMap);
        }

        return report;
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

    private static string ResolveDestinationLabel(UnitManager unit, ConstructionManager construction)
    {
        if (unit != null)
            return unit.name;
        if (construction != null)
            return ResolveConstructionDisplayName(construction);
        return "(sem destino)";
    }

    private static string BuildEndpointKey(UnitManager unit, ConstructionManager construction)
    {
        if (unit != null)
            return $"unit:{unit.GetInstanceID()}";
        if (construction != null)
            return $"construction:{construction.GetInstanceID()}";
        return string.Empty;
    }

    private static void TryResolveEndpointByKey(string key, out UnitManager unit, out ConstructionManager construction)
    {
        unit = null;
        construction = null;
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (key.StartsWith("unit:"))
        {
            int id;
            if (int.TryParse(key.Substring(5), out id))
            {
                UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < units.Length; i++)
                {
                    if (units[i] != null && units[i].GetInstanceID() == id)
                    {
                        unit = units[i];
                        return;
                    }
                }
            }
            return;
        }

        if (key.StartsWith("construction:"))
        {
            int id;
            if (int.TryParse(key.Substring(13), out id))
            {
                ConstructionManager[] constructions = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < constructions.Length; i++)
                {
                    if (constructions[i] != null && constructions[i].GetInstanceID() == id)
                    {
                        construction = constructions[i];
                        return;
                    }
                }
            }
        }
    }

    private static Dictionary<SupplyData, long> GetOrCreateUnitStockMap(
        Dictionary<UnitManager, Dictionary<SupplyData, long>> cache,
        UnitManager unit)
    {
        if (cache == null || unit == null)
            return null;

        if (cache.TryGetValue(unit, out Dictionary<SupplyData, long> existing))
            return existing;

        Dictionary<SupplyData, long> map = new Dictionary<SupplyData, long>();
        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources != null)
        {
            for (int i = 0; i < resources.Count; i++)
            {
                UnitEmbarkedSupply entry = resources[i];
                if (entry == null || entry.supply == null)
                    continue;

                long amount = Mathf.Max(0, entry.amount);
                if (amount <= 0)
                    continue;

                AddAmount(map, entry.supply, amount);
            }
        }

        cache[unit] = map;
        return map;
    }

    private static Dictionary<SupplyData, long> GetOrCreateUnitCapacityMap(
        Dictionary<UnitManager, Dictionary<SupplyData, long>> cache,
        UnitManager unit)
    {
        if (cache == null || unit == null)
            return null;

        if (cache.TryGetValue(unit, out Dictionary<SupplyData, long> existing))
            return existing;

        Dictionary<SupplyData, long> map = new Dictionary<SupplyData, long>();
        if (unit.TryGetUnitData(out UnitData data) && data != null && data.supplierResources != null)
        {
            for (int i = 0; i < data.supplierResources.Count; i++)
            {
                UnitEmbarkedSupply entry = data.supplierResources[i];
                if (entry == null || entry.supply == null)
                    continue;

                long amount = Mathf.Max(0, entry.amount);
                if (amount <= 0)
                    continue;

                AddAmount(map, entry.supply, amount);
            }
        }

        cache[unit] = map;
        return map;
    }

    private static Dictionary<SupplyData, long> GetOrCreateConstructionStockMap(
        Dictionary<ConstructionManager, Dictionary<SupplyData, long>> cache,
        ConstructionManager construction)
    {
        if (cache == null || construction == null)
            return null;

        if (cache.TryGetValue(construction, out Dictionary<SupplyData, long> existing))
            return existing;

        Dictionary<SupplyData, long> map = new Dictionary<SupplyData, long>();
        IReadOnlyList<ConstructionSupplyOffer> offers = construction.OfferedSupplies;
        if (offers != null)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                ConstructionSupplyOffer offer = offers[i];
                if (offer == null || offer.supply == null)
                    continue;

                long amount = Mathf.Max(0, offer.quantity);
                if (amount <= 0)
                    continue;

                AddAmount(map, offer.supply, amount);
            }
        }

        cache[construction] = map;
        return map;
    }

    private static void DrawBeforeAfterMap(Dictionary<SupplyData, long> before, Dictionary<SupplyData, long> after)
    {
        Dictionary<SupplyData, long> safeBefore = before ?? new Dictionary<SupplyData, long>();
        Dictionary<SupplyData, long> safeAfter = after ?? new Dictionary<SupplyData, long>();
        HashSet<SupplyData> union = new HashSet<SupplyData>();
        foreach (KeyValuePair<SupplyData, long> pair in safeBefore)
            union.Add(pair.Key);
        foreach (KeyValuePair<SupplyData, long> pair in safeAfter)
            union.Add(pair.Key);

        if (union.Count == 0)
        {
            EditorGUILayout.LabelField("- sem estoque");
            return;
        }

        List<SupplyData> rows = new List<SupplyData>(union);
        rows.Sort((a, b) =>
        {
            string aName = a != null && !string.IsNullOrWhiteSpace(a.displayName) ? a.displayName : (a != null ? a.name : string.Empty);
            string bName = b != null && !string.IsNullOrWhiteSpace(b.displayName) ? b.displayName : (b != null ? b.name : string.Empty);
            return string.Compare(aName, bName, System.StringComparison.OrdinalIgnoreCase);
        });

        bool hasChanges = false;
        for (int i = 0; i < rows.Count; i++)
        {
            SupplyData supply = rows[i];
            if (supply == null)
                continue;
            long beforeAmount = safeBefore.TryGetValue(supply, out long bValue) ? bValue : 0;
            long afterAmount = safeAfter.TryGetValue(supply, out long aValue) ? aValue : 0;
            if (beforeAmount == afterAmount)
                continue;
            string name = !string.IsNullOrWhiteSpace(supply.displayName) ? supply.displayName : supply.name;
            EditorGUILayout.LabelField($"- {name}: {FormatAmountForReport(beforeAmount)} -> {FormatAmountForReport(afterAmount)}");
            hasChanges = true;
        }

        if (!hasChanges)
            EditorGUILayout.LabelField("- sem alteracoes");
    }

    private static Dictionary<SupplyData, long> CloneStockMap(Dictionary<SupplyData, long> source)
    {
        Dictionary<SupplyData, long> clone = new Dictionary<SupplyData, long>();
        if (source == null)
            return clone;

        foreach (KeyValuePair<SupplyData, long> pair in source)
        {
            if (pair.Key == null || pair.Value <= 0)
                continue;
            clone[pair.Key] = pair.Value;
        }

        return clone;
    }

    private static void AddAmount(Dictionary<SupplyData, long> map, SupplyData supply, long amount)
    {
        if (map == null || supply == null || amount <= 0)
            return;

        if (map.TryGetValue(supply, out long existing))
            map[supply] = existing + amount;
        else
            map[supply] = amount;
    }

    private static void CopyMap(Dictionary<SupplyData, long> destination, Dictionary<SupplyData, long> source)
    {
        if (destination == null)
            return;

        destination.Clear();
        if (source == null)
            return;

        foreach (KeyValuePair<SupplyData, long> pair in source)
        {
            if (pair.Key == null || pair.Value <= 0)
                continue;
            destination[pair.Key] = pair.Value;
        }
    }

    private void DrawOrderEntryReport(TransferOrderReportEntry entry)
    {
        if (entry == null)
            return;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"Ordem: {entry.mode}", EditorStyles.boldLabel);
        if (!string.IsNullOrWhiteSpace(entry.orderLabel))
            EditorGUILayout.LabelField(entry.orderLabel, EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField($"Fornecedor: {entry.supplierLabel}", EditorStyles.boldLabel);
        DrawSupplierSection(entry);

        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField($"Destino: {entry.destinationLabel}", EditorStyles.boldLabel);
        DrawDestinationSection(entry);

        if (!entry.supplierIsInfinite && entry.blockedByCapacity.Count > 0)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField($"Excedente nao enviado: {entry.destinationLabel}", EditorStyles.boldLabel);
            DrawBlockedSection(entry);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSupplierSection(TransferOrderReportEntry entry)
    {
        if (entry == null || entry.sentBySupply.Count == 0)
        {
            EditorGUILayout.LabelField("- sem transferencia");
            return;
        }

        List<SupplyData> rows = new List<SupplyData>(entry.sentBySupply.Keys);
        SortSupplies(rows);
        for (int i = 0; i < rows.Count; i++)
        {
            SupplyData supply = rows[i];
            if (supply == null)
                continue;

            long sent = entry.sentBySupply.TryGetValue(supply, out long sentValue) ? sentValue : 0;
            if (sent <= 0)
                continue;

            string supplyName = ResolveSupplyName(supply);
            if (entry.supplierIsInfinite)
            {
                EditorGUILayout.LabelField($"- {supplyName}: {FormatAmountForReport(sent)}");
                continue;
            }

            long before = entry.supplierBefore.TryGetValue(supply, out long b) ? b : 0;
            long after = entry.supplierAfter.TryGetValue(supply, out long a) ? a : 0;
            EditorGUILayout.LabelField($"- {supplyName}: {FormatAmountForReport(before)} - {FormatAmountForReport(sent)} -> {FormatAmountForReport(after)}");
        }
    }

    private void DrawDestinationSection(TransferOrderReportEntry entry)
    {
        if (entry == null || entry.sentBySupply.Count == 0)
        {
            EditorGUILayout.LabelField("- sem transferencia");
            return;
        }

        List<SupplyData> rows = new List<SupplyData>(entry.sentBySupply.Keys);
        SortSupplies(rows);
        for (int i = 0; i < rows.Count; i++)
        {
            SupplyData supply = rows[i];
            if (supply == null)
                continue;

            long received = entry.sentBySupply.TryGetValue(supply, out long moved) ? moved : 0;
            if (received <= 0)
                continue;

            long before = entry.destinationBefore.TryGetValue(supply, out long b) ? b : 0;
            long after = entry.destinationAfter.TryGetValue(supply, out long a) ? a : 0;
            string supplyName = ResolveSupplyName(supply);
            EditorGUILayout.LabelField($"- {supplyName}: {FormatAmountForReport(before)} + {FormatAmountForReport(received)} -> {FormatAmountForReport(after)}");
        }
    }

    private void DrawBlockedSection(TransferOrderReportEntry entry)
    {
        if (entry == null || entry.blockedByCapacity.Count == 0)
        {
            EditorGUILayout.LabelField("- sem excedente");
            return;
        }

        List<SupplyData> rows = new List<SupplyData>(entry.blockedByCapacity.Keys);
        SortSupplies(rows);
        for (int i = 0; i < rows.Count; i++)
        {
            SupplyData supply = rows[i];
            if (supply == null)
                continue;

            long blocked = entry.blockedByCapacity.TryGetValue(supply, out long blockedValue) ? blockedValue : 0;
            if (blocked <= 0)
                continue;

            string supplyName = ResolveSupplyName(supply);
            EditorGUILayout.LabelField($"- {supplyName}: {FormatAmountForReport(blocked)}");
        }
    }

    private static void SortSupplies(List<SupplyData> rows)
    {
        if (rows == null)
            return;

        rows.Sort((a, b) =>
        {
            string aName = ResolveSupplyName(a);
            string bName = ResolveSupplyName(b);
            return string.Compare(aName, bName, System.StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string ResolveSupplyName(SupplyData supply)
    {
        if (supply == null)
            return "(null)";
        return !string.IsNullOrWhiteSpace(supply.displayName) ? supply.displayName : supply.name;
    }

    private static string FormatAmountForReport(long value)
    {
        if (value >= int.MaxValue)
            return "infinite";
        return value.ToString();
    }

    private void ExecuteTransferOrderDebug()
    {
        if (selectedSupplier == null)
        {
            statusMessage = "Selecione um supplier valido.";
            return;
        }

        if (transferOrder.Count == 0)
        {
            statusMessage = "Ordem vazia.";
            return;
        }

        long movedTotal = 0;
        long blockedTotal = 0;
        List<Object> touched = new List<Object>();

        for (int i = 0; i < transferOrder.Count; i++)
        {
            TransferCandidateEntry queued = transferOrder[i];
            if (queued == null || queued.option == null)
                continue;

            PodeTransferirOption option = queued.option;
            ResolveTransferEndpoints(option, selectedSupplier, out UnitManager sourceUnit, out ConstructionManager sourceConstruction, out UnitManager destinationUnit, out ConstructionManager destinationConstruction);
            if (sourceUnit == null && sourceConstruction == null)
                continue;

            RegisterUndoTarget(sourceUnit, touched);
            RegisterUndoTarget(sourceConstruction, touched);
            RegisterUndoTarget(destinationUnit, touched);
            RegisterUndoTarget(destinationConstruction, touched);

            long movedThisOrder = ExecuteSingleTransfer(
                sourceUnit,
                sourceConstruction,
                destinationUnit,
                destinationConstruction,
                out long blockedThisOrder);

            movedTotal += movedThisOrder;
            blockedTotal += blockedThisOrder;
        }

        for (int i = 0; i < touched.Count; i++)
            EditorUtility.SetDirty(touched[i]);

        if (selectedSupplier.gameObject != null && selectedSupplier.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(selectedSupplier.gameObject.scene);

        transferOrder.Clear();
        orderMessage = "Ordem executada.";
        statusMessage = blockedTotal > 0
            ? $"Transferencia executada. Transferido: {movedTotal}. Excedente nao transferido por limite de capacidade: {blockedTotal}."
            : $"Transferencia executada. Movido: {movedTotal}.";

        RunSimulation();
    }

    private static void RegisterUndoTarget(Object target, List<Object> touched)
    {
        if (target == null || touched == null || touched.Contains(target))
            return;

        Undo.RecordObject(target, "Transferir (Debug)");
        touched.Add(target);
    }

    private static long ExecuteSingleTransfer(
        UnitManager sourceUnit,
        ConstructionManager sourceConstruction,
        UnitManager destinationUnit,
        ConstructionManager destinationConstruction,
        out long blockedByCapacity)
    {
        blockedByCapacity = 0;

        Dictionary<SupplyData, long> sourceStock = sourceUnit != null
            ? ReadUnitStock(sourceUnit)
            : ReadConstructionStock(sourceConstruction);

        if (sourceStock == null || sourceStock.Count == 0)
            return 0;

        Dictionary<SupplyData, long> destinationStock = destinationUnit != null
            ? ReadUnitStock(destinationUnit)
            : ReadConstructionStock(destinationConstruction);
        if (destinationStock == null)
            destinationStock = new Dictionary<SupplyData, long>();

        Dictionary<SupplyData, long> destinationCapacity = destinationUnit != null
            ? ReadUnitCapacity(destinationUnit)
            : null;

        bool sourceIsInfiniteConstruction = sourceConstruction != null && IsInfiniteConstruction(sourceConstruction);
        long moved = 0;

        List<SupplyData> supplies = new List<SupplyData>(sourceStock.Keys);
        for (int i = 0; i < supplies.Count; i++)
        {
            SupplyData supply = supplies[i];
            if (supply == null)
                continue;

            long available = sourceStock.TryGetValue(supply, out long amount) ? amount : 0;
            if (available <= 0)
                continue;

            long transferable = available;
            if (destinationUnit != null)
            {
                long currentDestination = destinationStock.TryGetValue(supply, out long destAmount) ? destAmount : 0;
                long capacity = destinationCapacity != null && destinationCapacity.TryGetValue(supply, out long cap) ? cap : 0;
                long remaining = System.Math.Max(0L, capacity - currentDestination);
                if (remaining <= 0)
                    continue;
                transferable = System.Math.Min(transferable, remaining);
            }

            long blocked = System.Math.Max(0L, available - transferable);
            blockedByCapacity += blocked;

            if (transferable <= 0)
                continue;

            moved += transferable;
            AddAmount(destinationStock, supply, transferable);

            if (!sourceIsInfiniteConstruction)
                sourceStock[supply] = System.Math.Max(0L, available - transferable);
        }

        if (sourceUnit != null)
            WriteUnitStock(sourceUnit, sourceStock);
        else
            WriteConstructionStock(sourceConstruction, sourceStock);

        if (destinationUnit != null)
            WriteUnitStock(destinationUnit, destinationStock);
        else
            WriteConstructionStock(destinationConstruction, destinationStock);

        return moved;
    }

    private static Dictionary<SupplyData, long> ReadUnitStock(UnitManager unit)
    {
        Dictionary<SupplyData, long> map = new Dictionary<SupplyData, long>();
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
            AddAmount(map, entry.supply, System.Math.Max(0L, entry.amount));
        }

        return map;
    }

    private static void WriteUnitStock(UnitManager unit, Dictionary<SupplyData, long> stock)
    {
        if (unit == null || stock == null)
            return;

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources == null)
            return;

        Dictionary<SupplyData, UnitEmbarkedSupply> runtimeBySupply = new Dictionary<SupplyData, UnitEmbarkedSupply>();
        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || entry.supply == null)
                continue;
            runtimeBySupply[entry.supply] = entry;
        }

        foreach (KeyValuePair<SupplyData, long> pair in stock)
        {
            SupplyData supply = pair.Key;
            if (supply == null)
                continue;

            int safeAmount = pair.Value >= int.MaxValue ? int.MaxValue : (pair.Value <= 0 ? 0 : (int)pair.Value);
            if (runtimeBySupply.TryGetValue(supply, out UnitEmbarkedSupply existing))
            {
                existing.amount = safeAmount;
            }
            else if (resources is List<UnitEmbarkedSupply> list)
            {
                list.Add(new UnitEmbarkedSupply
                {
                    supply = supply,
                    amount = safeAmount
                });
            }
        }
    }

    private static Dictionary<SupplyData, long> ReadUnitCapacity(UnitManager unit)
    {
        Dictionary<SupplyData, long> map = new Dictionary<SupplyData, long>();
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.supplierResources == null)
            return map;

        for (int i = 0; i < data.supplierResources.Count; i++)
        {
            UnitEmbarkedSupply entry = data.supplierResources[i];
            if (entry == null || entry.supply == null)
                continue;
            AddAmount(map, entry.supply, System.Math.Max(0L, entry.amount));
        }

        return map;
    }

    private static Dictionary<SupplyData, long> ReadConstructionStock(ConstructionManager construction)
    {
        Dictionary<SupplyData, long> map = new Dictionary<SupplyData, long>();
        if (construction == null)
            return map;

        ConstructionSiteRuntime runtime = construction.GetSiteRuntimeSnapshot();
        if (runtime == null || runtime.offeredSupplies == null)
            return map;

        for (int i = 0; i < runtime.offeredSupplies.Count; i++)
        {
            ConstructionSupplyOffer offer = runtime.offeredSupplies[i];
            if (offer == null || offer.supply == null)
                continue;
            AddAmount(map, offer.supply, System.Math.Max(0L, offer.quantity));
        }

        return map;
    }

    private static void WriteConstructionStock(ConstructionManager construction, Dictionary<SupplyData, long> stock)
    {
        if (construction == null || stock == null)
            return;

        ConstructionSiteRuntime runtime = construction.GetSiteRuntimeSnapshot();
        if (runtime == null)
            return;

        if (runtime.offeredSupplies == null)
            runtime.offeredSupplies = new List<ConstructionSupplyOffer>();

        Dictionary<SupplyData, ConstructionSupplyOffer> offerBySupply = new Dictionary<SupplyData, ConstructionSupplyOffer>();
        for (int i = 0; i < runtime.offeredSupplies.Count; i++)
        {
            ConstructionSupplyOffer offer = runtime.offeredSupplies[i];
            if (offer == null || offer.supply == null)
                continue;
            offerBySupply[offer.supply] = offer;
        }

        foreach (KeyValuePair<SupplyData, long> pair in stock)
        {
            SupplyData supply = pair.Key;
            if (supply == null)
                continue;

            int safeAmount = pair.Value >= int.MaxValue ? int.MaxValue : (pair.Value <= 0 ? 0 : (int)pair.Value);
            if (offerBySupply.TryGetValue(supply, out ConstructionSupplyOffer existing))
            {
                existing.quantity = safeAmount;
            }
            else
            {
                runtime.offeredSupplies.Add(new ConstructionSupplyOffer
                {
                    supply = supply,
                    quantity = safeAmount
                });
            }
        }

        construction.ApplySiteRuntime(runtime);
    }

    private static bool IsInfiniteConstruction(ConstructionManager construction)
    {
        if (construction == null || !construction.TryResolveConstructionData(out ConstructionData data) || data == null || data.supplierResources == null)
            return false;

        for (int i = 0; i < data.supplierResources.Count; i++)
        {
            ConstructionSupplierResourceCapacity entry = data.supplierResources[i];
            if (entry != null && entry.maxCapacity < 0)
                return true;
        }

        return false;
    }

    private static string BuildOptionLabel(PodeTransferirOption option)
    {
        if (option == null)
            return "(null)";
        if (!string.IsNullOrWhiteSpace(option.displayLabel))
            return option.displayLabel;

        if (option.targetUnit != null)
            return $"{option.flowMode} -> {option.targetUnit.name}";
        if (option.targetConstruction != null)
            return $"{option.flowMode} -> {ResolveConstructionDisplayName(option.targetConstruction)}";
        return option.flowMode.ToString();
    }

    private static string BuildInvalidLabel(PodeTransferirInvalidOption invalid)
    {
        if (invalid == null)
            return "(null)";

        string supplier = invalid.supplierUnit != null
            ? invalid.supplierUnit.name
            : "(unidade)";
        string endpoint = invalid.targetUnit != null
            ? invalid.targetUnit.name
            : (invalid.targetConstruction != null ? ResolveConstructionDisplayName(invalid.targetConstruction) : "sem destino");

        if (invalid.flowMode == TransferFlowMode.Fornecimento)
            return $"{supplier} -> Fornece -> {endpoint}";
        return $"{supplier} <- Recebe <- {endpoint}";
    }

    private static string ResolveConstructionDisplayName(ConstructionManager construction)
    {
        if (construction == null)
            return "(null)";
        if (!string.IsNullOrWhiteSpace(construction.ConstructionDisplayName))
            return construction.ConstructionDisplayName;
        if (!string.IsNullOrWhiteSpace(construction.ConstructionId))
            return construction.ConstructionId;
        return construction.name;
    }

    private void SelectLineForDrawing(PodeTransferirOption option, Color color, string label)
    {
        if (selectedSupplier == null || option == null)
        {
            ClearSelectedLine();
            return;
        }

        Vector3Int from = selectedSupplier.CurrentCellPosition;
        from.z = 0;
        Vector3Int to = option.targetCell;
        to.z = 0;
        SetSelectedLine(from, to, color, label);
    }

    private void SelectLineForDrawing(PodeTransferirInvalidOption invalid, Color color, string label)
    {
        if (selectedSupplier == null || invalid == null)
        {
            ClearSelectedLine();
            return;
        }

        Vector3Int from = selectedSupplier.CurrentCellPosition;
        from.z = 0;
        Vector3Int to = invalid.targetCell;
        to.z = 0;
        SetSelectedLine(from, to, color, label);
    }

    private void SetSelectedLine(Vector3Int fromCell, Vector3Int toCell, Color color, string label)
    {
        hasSelectedLine = true;
        selectedLineStartCell = fromCell;
        selectedLineEndCell = toCell;
        selectedLineColor = color;
        selectedLineLabel = label;
        SceneView.RepaintAll();
    }

    private void ClearSelectedLine()
    {
        hasSelectedLine = false;
        selectedLineLabel = string.Empty;
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!hasSelectedLine)
            return;

        Tilemap map = ResolveTilemap();
        if (map == null)
            return;

        Vector3 from = map.GetCellCenterWorld(selectedLineStartCell);
        Vector3 to = map.GetCellCenterWorld(selectedLineEndCell);
        Handles.color = selectedLineColor;
        Handles.DrawAAPolyLine(4f, from, to);
        Handles.SphereHandleCap(0, to, Quaternion.identity, HandleUtility.GetHandleSize(to) * 0.08f, EventType.Repaint);
        if (!string.IsNullOrWhiteSpace(selectedLineLabel))
            Handles.Label(to + Vector3.up * 0.25f, selectedLineLabel);
    }
}
