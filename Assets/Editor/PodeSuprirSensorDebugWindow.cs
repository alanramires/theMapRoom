using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeSuprirSensorDebugWindow : EditorWindow
{
    private enum SupplyServiceLayerPlan
    {
        DefaultSameDomain = 0,
        AirLow = 1,
        NavalSurface = 2
    }

    private sealed class SupplyCandidateEntry
    {
        public UnitManager unit;
        public Vector3Int cell;
        public string mode;
        public bool forceLandBeforeSupply;
        public bool forceTakeoffBeforeSupply;
        public bool forceSurfaceBeforeSupply;
        public Domain plannedServiceDomain;
        public HeightLevel plannedServiceHeight;
    }

    private sealed class IneligibleSupplyEntry
    {
        public UnitManager unit;
        public Vector3Int cell;
        public string reason;
    }

    private sealed class QueueUsageEstimate
    {
        public readonly Dictionary<ServiceData, int> serviceUsageByTarget = new Dictionary<ServiceData, int>();
        public readonly Dictionary<SupplyData, int> supplyUsage = new Dictionary<SupplyData, int>();
        public readonly List<string> perTargetLines = new List<string>();
        public readonly List<string> perTargetSupplyLines = new List<string>();
        public readonly List<string> perTargetCostLines = new List<string>();
        public readonly Dictionary<ServiceData, ServiceExecutionSummary> serviceSummaries = new Dictionary<ServiceData, ServiceExecutionSummary>();
        public int totalSupplyUnitsUsed;
        public int totalEstimatedCost;
    }

    private sealed class ServiceExecutionSummary
    {
        public ServiceData service;
        public readonly List<string> participantLines = new List<string>();
        public readonly Dictionary<SupplyData, int> consumedBySupply = new Dictionary<SupplyData, int>();
        public readonly List<int> ammoCostByWeapon = new List<int>();
        public readonly Dictionary<WeaponClass, int> ammoBoxesByClass = new Dictionary<WeaponClass, int>();
        public int recoveredHp;
        public int recoveredFuel;
        public int recoveredAmmo;
        public int subtotalCost;
    }

    [SerializeField] private UnitManager selectedSupplier;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    private readonly List<SupplyCandidateEntry> eligibleCandidates = new List<SupplyCandidateEntry>();
    private readonly List<IneligibleSupplyEntry> ineligibleCandidates = new List<IneligibleSupplyEntry>();
    private readonly List<SupplyCandidateEntry> supplyQueue = new List<SupplyCandidateEntry>();

    private string statusMessage = "Ready.";
    private string sensorReason = "Ready.";
    private string queueMessage = "Fila vazia.";
    private bool canSupply;
    private int maxUnitsServedPerTurn;
    private int selectedCandidateIndex = -1;
    private int selectedInvalidIndex = -1;
    private Vector2 windowScroll;

    private bool hasSelectedLine;
    private Vector3Int selectedLineStartCell;
    private Vector3Int selectedLineEndCell;
    private Color selectedLineColor = Color.cyan;
    private string selectedLineLabel = string.Empty;

    [MenuItem("Tools/Logistica/Pode Suprir")]
    public static void OpenWindow()
    {
        GetWindow<PodeSuprirSensorDebugWindow>("Pode Suprir");
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
        EditorGUILayout.LabelField("Sensor Pode Suprir", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Regra atual:\n" +
            "1) Unidade selecionada deve ser supplier (isSupplier=true)\n" +
            "2) Escaneia adjacentes (1 hex)\n" +
            "3) Dominio diferente: tenta pousar; se AR pousada, tenta decolar\n" +
            "4) Lista respeita maxUnitsServedPerTurn\n" +
            "5) Atalhos: 1..9 seleciona | Enter adiciona na fila | 0 executa parcial",
            MessageType.Info);

        selectedSupplier = (UnitManager)EditorGUILayout.ObjectField("Supplier", selectedSupplier, typeof(UnitManager), true);
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);

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
        EditorGUILayout.HelpBox(queueMessage, MessageType.None);
        EditorGUILayout.LabelField("Pode Suprir", canSupply ? "SIM" : "NAO");
        EditorGUILayout.LabelField("Max Units Served Per Turn", maxUnitsServedPerTurn.ToString());
        EditorGUILayout.LabelField("Fila atual", $"{supplyQueue.Count}/{Mathf.Max(0, maxUnitsServedPerTurn)}");
        if (!string.IsNullOrWhiteSpace(sensorReason))
            EditorGUILayout.HelpBox($"Sensor: {sensorReason}", canSupply ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.MinWidth(420f), GUILayout.ExpandWidth(true));
        DrawQueueSection();
        EditorGUILayout.Space(8f);
        DrawEligibleCandidatesSection();
        EditorGUILayout.Space(8f);
        DrawIneligibleCandidatesSection();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(12f);
        EditorGUILayout.BeginVertical(GUILayout.Width(420f));
        DrawSupplyReportSection();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        HandleKeyboardShortcuts();
        EditorGUILayout.EndScrollView();
    }

    private void DrawQueueSection()
    {
        EditorGUILayout.LabelField($"Fila de Suprimento (Debug) ({supplyQueue.Count})", EditorStyles.boldLabel);
        if (supplyQueue.Count == 0)
        {
            EditorGUILayout.HelpBox("Fila vazia.", MessageType.Info);
            return;
        }

        SupplyServiceLayerPlan queuePlan = ResolveServiceLayerPlan(selectedSupplier, supplyQueue, out _);

        for (int i = 0; i < supplyQueue.Count; i++)
        {
            SupplyCandidateEntry order = supplyQueue[i];
            if (order == null || order.unit == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"{i + 1}. {order.unit.name}");
            EditorGUILayout.LabelField("Modo", order.mode);
            EditorGUILayout.LabelField("Camada atual", $"{order.unit.GetDomain()}/{order.unit.GetHeightLevel()}");
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ToggleLeft("Pousa antes de suprir", order.forceLandBeforeSupply);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ToggleLeft("Decola antes de suprir", order.forceTakeoffBeforeSupply);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ToggleLeft("Emerge antes de suprir", order.forceSurfaceBeforeSupply);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ToggleLeft("Nivela em baixa altitude antes de suprir", ShouldLevelToAirLowBeforeSupply(order, queuePlan));
            if (GUILayout.Button("Remover da Fila"))
            {
                UnitManager removed = order.unit;
                supplyQueue.RemoveAt(i);
                queueMessage = removed != null ? $"Removido da fila: {removed.name}." : "Removido da fila.";
                RebuildCandidateLists(ResolveTilemap());
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawEligibleCandidatesSection()
    {
        EditorGUILayout.LabelField($"Candidatos validos ({eligibleCandidates.Count})", EditorStyles.boldLabel);
        if (eligibleCandidates.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum candidato valido.", MessageType.Info);
            return;
        }

        for (int i = 0; i < eligibleCandidates.Count; i++)
        {
            SupplyCandidateEntry candidate = eligibleCandidates[i];
            if (candidate == null || candidate.unit == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            bool isSelected = selectedCandidateIndex == i;
            bool toggled = GUILayout.Toggle(isSelected, $"{i + 1}. {candidate.unit.name}", "Button");
            if (toggled && selectedCandidateIndex != i)
            {
                selectedCandidateIndex = i;
                selectedInvalidIndex = -1;
                SelectLineForDrawing(candidate.unit);
            }

            EditorGUILayout.LabelField("Modo", candidate.mode);
            EditorGUILayout.LabelField("Camada", $"{candidate.unit.GetDomain()}/{candidate.unit.GetHeightLevel()}");
            EditorGUILayout.LabelField("Hex", $"{candidate.cell.x},{candidate.cell.y}");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Desenhar Linha"))
            {
                selectedCandidateIndex = i;
                selectedInvalidIndex = -1;
                SelectLineForDrawing(candidate.unit);
            }
            if (GUILayout.Button("Adicionar na Fila"))
                TryAddCandidateToQueue(candidate);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawIneligibleCandidatesSection()
    {
        EditorGUILayout.LabelField($"Candidatos invalidos ({ineligibleCandidates.Count})", EditorStyles.boldLabel);
        if (ineligibleCandidates.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum candidato invalido adjacente.", MessageType.Info);
            return;
        }

        for (int i = 0; i < ineligibleCandidates.Count; i++)
        {
            IneligibleSupplyEntry entry = ineligibleCandidates[i];
            if (entry == null || entry.unit == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            bool isSelected = selectedInvalidIndex == i;
            bool toggled = GUILayout.Toggle(isSelected, $"{i + 1}. {entry.unit.name}", "Button");
            if (toggled && selectedInvalidIndex != i)
            {
                selectedInvalidIndex = i;
                selectedCandidateIndex = -1;
                SelectLineForDrawing(entry.unit, Color.red, $"Invalido: {entry.reason}");
            }
            EditorGUILayout.LabelField("Camada", $"{entry.unit.GetDomain()}/{entry.unit.GetHeightLevel()}");
            EditorGUILayout.LabelField("Hex", $"{entry.cell.x},{entry.cell.y}");
            EditorGUILayout.LabelField("Motivo", string.IsNullOrWhiteSpace(entry.reason) ? "-" : entry.reason);
            if (GUILayout.Button("Desenhar Linha Vermelha"))
            {
                selectedInvalidIndex = i;
                selectedCandidateIndex = -1;
                SelectLineForDrawing(entry.unit, Color.red, $"Invalido: {entry.reason}");
            }
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawSupplyReportSection()
    {
        EditorGUILayout.LabelField("Relatorio de Suprimento", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        if (selectedSupplier == null)
        {
            EditorGUILayout.HelpBox("Selecione um supplier.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField("Supplier", selectedSupplier.name);
        EditorGUILayout.LabelField("Fila selecionada", $"{supplyQueue.Count}/{Mathf.Max(0, maxUnitsServedPerTurn)}");
        EditorGUILayout.Space(2f);
        DrawUsedServicesSummarySection();
        EditorGUILayout.Space(4f);

        SupplyServiceLayerPlan plan = ResolveServiceLayerPlan(selectedSupplier, supplyQueue, out string observation);
        EditorGUILayout.HelpBox($"OBS: {observation}", MessageType.Info);
        EditorGUILayout.LabelField("O servico sera realizado em");
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ToggleLeft("Default Service (same domain)", plan == SupplyServiceLayerPlan.DefaultSameDomain);
            EditorGUILayout.ToggleLeft("Air / Low", plan == SupplyServiceLayerPlan.AirLow);
            EditorGUILayout.ToggleLeft("Naval / Surface", plan == SupplyServiceLayerPlan.NavalSurface);
        }
        EditorGUILayout.EndVertical();

        bool canExecutePartial = selectedSupplier != null && supplyQueue.Count > 0;
        using (new EditorGUI.DisabledScope(!canExecutePartial))
        {
            if (GUILayout.Button("EXECUTAR ORDEM PARCIAL [DEBUG]"))
                ExecutePartialSupplyOrderDebug();
        }
    }

    private void DrawUsedServicesSummarySection()
    {
        EditorGUILayout.LabelField("Servicos executados", EditorStyles.boldLabel);
        if (selectedSupplier == null || supplyQueue == null || supplyQueue.Count == 0)
        {
            EditorGUILayout.LabelField("-");
            return;
        }

        if (!selectedSupplier.TryGetUnitData(out UnitData supplierData) || supplierData == null)
        {
            EditorGUILayout.LabelField("Supplier sem UnitData.");
            return;
        }

        QueueUsageEstimate estimate = BuildQueueUsageEstimate(selectedSupplier, supplierData, supplyQueue);
        if (estimate.serviceSummaries.Count <= 0)
        {
            EditorGUILayout.LabelField("Nenhum servico aplicavel para a fila atual.");
            return;
        }

        List<ServiceExecutionSummary> ordered = BuildOrderedServiceSummaries(estimate.serviceSummaries);
        DrawConsolidatedSupplyReport(ordered, estimate);
        EditorGUILayout.Space(6f);

        for (int i = 0; i < ordered.Count; i++)
        {
            ServiceExecutionSummary summary = ordered[i];
            if (summary == null || summary.service == null)
                continue;

            string title = BuildServiceSectionTitle(summary.service);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (int j = 0; j < summary.participantLines.Count; j++)
                EditorGUILayout.LabelField(summary.participantLines[j]);

            EditorGUILayout.LabelField($"Subtotal {ResolveServiceName(summary.service)}:");
            if (summary.recoveredHp > 0)
                EditorGUILayout.LabelField($"+{summary.recoveredHp} HP");
            if (summary.recoveredFuel > 0)
                EditorGUILayout.LabelField($"+{summary.recoveredFuel} autonomia");
            if (summary.recoveredAmmo > 0)
                EditorGUILayout.LabelField($"+{summary.recoveredAmmo} municao");

            foreach (KeyValuePair<SupplyData, int> pair in summary.consumedBySupply)
            {
                string supplyName = ResolveSupplyName(pair.Key);
                EditorGUILayout.LabelField($"{pair.Value} {supplyName} consumidos");
            }

            EditorGUILayout.LabelField($"${summary.subtotalCost}");
            EditorGUILayout.Space(4f);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Subtotal de Servicos", EditorStyles.boldLabel);
        foreach (ServiceExecutionSummary summary in ordered)
        {
            if (summary == null || summary.service == null)
                continue;
            EditorGUILayout.LabelField($"{ResolveServiceName(summary.service)}: ${summary.subtotalCost}");
        }
        EditorGUILayout.LabelField($"Total Servicos: ${estimate.totalEstimatedCost}");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Subtotal de Suprimentos Consumidos", EditorStyles.boldLabel);
        foreach (KeyValuePair<SupplyData, int> pair in estimate.supplyUsage)
            EditorGUILayout.LabelField($"{ResolveSupplyName(pair.Key)}: {pair.Value}");
        EditorGUILayout.LabelField($"Total de supplies utilizados: {estimate.totalSupplyUnitsUsed}");
    }

    private static void DrawConsolidatedSupplyReport(List<ServiceExecutionSummary> ordered, QueueUsageEstimate estimate)
    {
        ServiceExecutionSummary repair = null;
        ServiceExecutionSummary refuel = null;
        ServiceExecutionSummary rearm = null;
        if (ordered != null)
        {
            for (int i = 0; i < ordered.Count; i++)
            {
                ServiceExecutionSummary summary = ordered[i];
                if (summary == null || summary.service == null)
                    continue;
                if (repair == null && summary.service.recuperaHp)
                    repair = summary;
                else if (refuel == null && summary.service.recuperaAutonomia)
                    refuel = summary;
                else if (rearm == null && summary.service.recuperaMunicao)
                    rearm = summary;
            }
        }

        int repairCost = repair != null ? Mathf.Max(0, repair.subtotalCost) : 0;
        int refuelCost = refuel != null ? Mathf.Max(0, refuel.subtotalCost) : 0;
        int rearmCost = rearm != null ? Mathf.Max(0, rearm.subtotalCost) : 0;
        int primaryCost = GetAmmoWeaponCost(rearm, 0);
        int secondaryCost = GetAmmoWeaponCost(rearm, 1);
        int pieces = SumConsumedByService(repair);
        int gallons = SumConsumedByService(refuel);
        int ammoBoxesTotal = SumConsumedByService(rearm);
        int heavyBoxes = GetAmmoBoxesByClass(rearm, WeaponClass.Heavy);
        int mediumBoxes = GetAmmoBoxesByClass(rearm, WeaponClass.Medium);

        EditorGUILayout.LabelField("Relatorio Consolidado", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Reparar: ${repairCost}");
        EditorGUILayout.LabelField($"Reabastecimento: ${refuelCost}");
        EditorGUILayout.LabelField($"Rearmamento: ${rearmCost}");
        EditorGUILayout.LabelField($"  primary: ${primaryCost}");
        EditorGUILayout.LabelField($"  secondary: ${secondaryCost}");
        EditorGUILayout.LabelField($"Total Servicos: ${Mathf.Max(0, estimate != null ? estimate.totalEstimatedCost : 0)}");
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField($"Pecas de manutencao: {pieces}");
        EditorGUILayout.LabelField($"Galoes de combustivel: {gallons}");
        EditorGUILayout.LabelField($"Caixas totais de municao: {ammoBoxesTotal}");
        EditorGUILayout.LabelField($"  Caixas heavy: {heavyBoxes}");
        EditorGUILayout.LabelField($"  Caixas medium: {mediumBoxes}");
    }

    private static int SumConsumedByService(ServiceExecutionSummary summary)
    {
        if (summary == null || summary.consumedBySupply == null)
            return 0;

        int total = 0;
        foreach (KeyValuePair<SupplyData, int> pair in summary.consumedBySupply)
            total += Mathf.Max(0, pair.Value);
        return Mathf.Max(0, total);
    }

    private static int GetAmmoWeaponCost(ServiceExecutionSummary summary, int weaponIndex)
    {
        if (summary == null || summary.ammoCostByWeapon == null || weaponIndex < 0 || weaponIndex >= summary.ammoCostByWeapon.Count)
            return 0;
        return Mathf.Max(0, summary.ammoCostByWeapon[weaponIndex]);
    }

    private static int GetAmmoBoxesByClass(ServiceExecutionSummary summary, WeaponClass weaponClass)
    {
        if (summary == null || summary.ammoBoxesByClass == null)
            return 0;
        return summary.ammoBoxesByClass.TryGetValue(weaponClass, out int value) ? Mathf.Max(0, value) : 0;
    }

    private static QueueUsageEstimate BuildQueueUsageEstimate(UnitManager supplier, UnitData supplierData, List<SupplyCandidateEntry> queue)
    {
        QueueUsageEstimate estimate = new QueueUsageEstimate();
        if (supplier == null || supplierData == null || queue == null || queue.Count == 0)
            return estimate;

        Dictionary<SupplyData, int> workingStock = BuildWorkingSupplyStock(supplier);
        for (int i = 0; i < queue.Count; i++)
        {
            SupplyCandidateEntry order = queue[i];
            UnitManager target = order != null ? order.unit : null;
            if (target == null)
                continue;

            string targetName = !string.IsNullOrWhiteSpace(target.name) ? target.name : $"Alvo {i + 1}";
            List<string> usedByTarget = new List<string>();
            Dictionary<SupplyData, int> supplyByTarget = new Dictionary<SupplyData, int>();
            int targetCost = 0;

            int hpMissing = Mathf.Max(0, target.GetMaxHP() - target.CurrentHP);
            int fuelMissing = Mathf.Max(0, target.GetMaxFuel() - target.CurrentFuel);
            bool ammoMissing = IsAnyAmmoMissing(target);

            if (hpMissing > 0 && TryApplyNeedEstimate(supplier, supplierData, target, hpMissing, forHp: true, forFuel: false, workingStock, estimate, out string hpUsed, out SupplyData hpSupply, out int hpSupplyConsumed, out int hpCost))
            {
                usedByTarget.Add(hpUsed);
                if (hpSupply != null && hpSupplyConsumed > 0)
                    AddSupplyConsumption(supplyByTarget, hpSupply, hpSupplyConsumed);
                targetCost += Mathf.Max(0, hpCost);
            }
            if (fuelMissing > 0 && TryApplyNeedEstimate(supplier, supplierData, target, fuelMissing, forHp: false, forFuel: true, workingStock, estimate, out string fuelUsed, out SupplyData fuelSupply, out int fuelSupplyConsumed, out int fuelCost))
            {
                usedByTarget.Add(fuelUsed);
                if (fuelSupply != null && fuelSupplyConsumed > 0)
                    AddSupplyConsumption(supplyByTarget, fuelSupply, fuelSupplyConsumed);
                targetCost += Mathf.Max(0, fuelCost);
            }
            if (ammoMissing && TryApplyAmmoNeedEstimate(supplier, supplierData, target, workingStock, estimate, out string ammoUsed, out SupplyData ammoSupply, out int ammoSupplyConsumed, out int ammoCost))
            {
                usedByTarget.Add(ammoUsed);
                if (ammoSupply != null && ammoSupplyConsumed > 0)
                    AddSupplyConsumption(supplyByTarget, ammoSupply, ammoSupplyConsumed);
                targetCost += Mathf.Max(0, ammoCost);
            }

            if (usedByTarget.Count > 0)
                estimate.perTargetLines.Add($"{targetName}: {string.Join(", ", usedByTarget)}");

            if (supplyByTarget.Count > 0)
            {
                List<string> chunks = new List<string>();
                foreach (KeyValuePair<SupplyData, int> pair in supplyByTarget)
                {
                    string supplyName = pair.Key != null && !string.IsNullOrWhiteSpace(pair.Key.displayName) ? pair.Key.displayName : (pair.Key != null ? pair.Key.name : "Supply");
                    chunks.Add($"{supplyName}: {pair.Value}");
                }
                estimate.perTargetSupplyLines.Add($"{targetName}: {string.Join(", ", chunks)}");
            }

            estimate.perTargetCostLines.Add($"{targetName}: ${targetCost}");
        }

        return estimate;
    }

    private static Dictionary<SupplyData, int> BuildWorkingSupplyStock(UnitManager supplier)
    {
        Dictionary<SupplyData, int> stock = new Dictionary<SupplyData, int>();
        IReadOnlyList<UnitEmbarkedSupply> runtime = supplier != null ? supplier.GetEmbarkedResources() : null;
        if (runtime == null)
            return stock;

        for (int i = 0; i < runtime.Count; i++)
        {
            UnitEmbarkedSupply entry = runtime[i];
            if (entry == null || entry.supply == null)
                continue;

            int amount = Mathf.Max(0, entry.amount);
            if (stock.TryGetValue(entry.supply, out int current))
                stock[entry.supply] = current + amount;
            else
                stock.Add(entry.supply, amount);
        }

        return stock;
    }

    private static bool TryApplyNeedEstimate(
        UnitManager supplier,
        UnitData supplierData,
        UnitManager target,
        int pointsMissing,
        bool forHp,
        bool forFuel,
        Dictionary<SupplyData, int> workingStock,
        QueueUsageEstimate estimate,
        out string usedText,
        out SupplyData consumedSupply,
        out int consumedAmount,
        out int estimatedCost)
    {
        usedText = string.Empty;
        consumedSupply = null;
        consumedAmount = 0;
        estimatedCost = 0;
        if (supplierData == null || target == null || pointsMissing <= 0)
            return false;

        bool targetIsSupplier = target.TryGetUnitData(out UnitData targetData) && targetData != null && targetData.isSupplier;
        ServiceData chosenService = null;
        SupplyData chosenSupply = null;
        int chosenSupplyAvailable = 0;
        float chosenEfficiency = 1f;

        for (int i = 0; i < supplierData.supplierServicesProvided.Count; i++)
        {
            ServiceData service = supplierData.supplierServicesProvided[i];
            if (service == null || !service.isService)
                continue;
            if (service.apenasEntreSupridores && !targetIsSupplier)
                continue;
            if (forHp && !service.recuperaHp)
                continue;
            if (forFuel && !service.recuperaAutonomia)
                continue;

            if (!TryResolveConsumableSupplyForService(service, workingStock, out SupplyData candidateSupply, out int candidateAvailable))
                continue;

            float efficiency = ResolveServiceEfficiencyValue(service, targetData);
            if (efficiency <= 0f)
                continue;

            chosenService = service;
            chosenSupply = candidateSupply;
            chosenSupplyAvailable = candidateAvailable;
            chosenEfficiency = efficiency;
            break;
        }

        if (chosenService == null)
            return false;

        Dictionary<SupplyData, int> consumedBySupply = new Dictionary<SupplyData, int>();
        ServiceLogisticsFormula.EstimatePotentialServiceGains(
            target,
            chosenService,
            workingStock,
            out int hpServed,
            out int fuelServed,
            out _,
            allowHp: forHp,
            allowFuel: forFuel,
            allowAmmo: false,
            consumedBySupply: consumedBySupply);

        int pointsServed = forHp ? hpServed : fuelServed;
        if (pointsServed <= 0)
            return false;

        foreach (KeyValuePair<SupplyData, int> pair in consumedBySupply)
        {
            int used = Mathf.Max(0, pair.Value);
            if (pair.Key == null || used <= 0)
                continue;
            if (estimate.supplyUsage.TryGetValue(pair.Key, out int consumed))
                estimate.supplyUsage[pair.Key] = consumed + used;
            else
                estimate.supplyUsage.Add(pair.Key, used);
            estimate.totalSupplyUnitsUsed += used;
            if (consumedSupply == null)
                consumedSupply = pair.Key;
            consumedAmount += used;
        }

        if (estimate.serviceUsageByTarget.TryGetValue(chosenService, out int countByService))
            estimate.serviceUsageByTarget[chosenService] = countByService + 1;
        else
            estimate.serviceUsageByTarget.Add(chosenService, 1);

        int money = ComputeServiceMoneyCostEstimate(
            target,
            chosenService,
            forHp ? pointsServed : 0,
            forFuel ? pointsServed : 0,
            0);
        estimate.totalEstimatedCost += Mathf.Max(0, money);
        estimatedCost = Mathf.Max(0, money);

        string serviceName = !string.IsNullOrWhiteSpace(chosenService.displayName) ? chosenService.displayName : chosenService.name;
        string needLabel = forHp ? "HP" : "Autonomia";
        string partial = pointsServed < pointsMissing ? " (parcial)" : string.Empty;
        usedText = $"{serviceName} {needLabel} {pointsServed}/{pointsMissing}{partial}";

        string targetName = !string.IsNullOrWhiteSpace(target.name) ? target.name : "Alvo";
        RegisterServiceExecution(
            estimate,
            chosenService,
            targetName,
            needLabel,
            pointsServed,
            pointsMissing,
            estimatedCost,
            consumedSupply,
            consumedAmount,
            null);
        return true;
    }

    private static bool TryApplyAmmoNeedEstimate(
        UnitManager supplier,
        UnitData supplierData,
        UnitManager target,
        Dictionary<SupplyData, int> workingStock,
        QueueUsageEstimate estimate,
        out string usedText,
        out SupplyData consumedSupply,
        out int consumedAmount,
        out int estimatedCost)
    {
        usedText = string.Empty;
        consumedSupply = null;
        consumedAmount = 0;
        estimatedCost = 0;

        if (supplier == null || supplierData == null || target == null)
            return false;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null || targetData.embarkedWeapons == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
        if (runtimeWeapons == null || runtimeWeapons.Count == 0 || targetData.embarkedWeapons.Count == 0)
            return false;

        bool targetIsSupplier = targetData.isSupplier;
        ServiceData chosenService = null;
        SupplyData chosenSupply = null;
        int chosenSupplyAvailable = 0;
        for (int i = 0; i < supplierData.supplierServicesProvided.Count; i++)
        {
            ServiceData service = supplierData.supplierServicesProvided[i];
            if (service == null || !service.isService || !service.recuperaMunicao)
                continue;
            if (service.apenasEntreSupridores && !targetIsSupplier)
                continue;
            if (!TryResolveConsumableSupplyForService(service, workingStock, out SupplyData candidateSupply, out int candidateAvailable))
                continue;

            chosenService = service;
            chosenSupply = candidateSupply;
            chosenSupplyAvailable = candidateAvailable;
            break;
        }

        if (chosenService == null)
            return false;

        int count = Mathf.Min(runtimeWeapons.Count, targetData.embarkedWeapons.Count);
        int totalMissingAmmo = 0;
        for (int i = 0; i < count; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = targetData.embarkedWeapons[i];
            if (runtime == null || baseline == null || baseline.weapon == null)
                continue;
            int missing = Mathf.Max(0, Mathf.Max(0, baseline.squadAmmunition) - Mathf.Max(0, runtime.squadAmmunition));
            totalMissingAmmo += missing;
        }

        List<int> ammoByWeapon = new List<int>();
        Dictionary<SupplyData, int> consumedBySupply = new Dictionary<SupplyData, int>();
        ServiceLogisticsFormula.EstimatePotentialServiceGains(
            target,
            chosenService,
            workingStock,
            out _,
            out _,
            out int totalRecoveredAmmo,
            ammoByWeapon: ammoByWeapon,
            allowHp: false,
            allowFuel: false,
            allowAmmo: true,
            consumedBySupply: consumedBySupply);

        Dictionary<WeaponClass, int> recoveredByClass = new Dictionary<WeaponClass, int>();
        Dictionary<WeaponClass, int> boxesByClass = new Dictionary<WeaponClass, int>();
        int boxesUsed = 0;
        for (int i = 0; i < ammoByWeapon.Count; i++)
        {
            int recovered = Mathf.Max(0, ammoByWeapon[i]);
            if (recovered <= 0)
                continue;
            WeaponClass cls = WeaponClass.Light;
            if (i < targetData.embarkedWeapons.Count && targetData.embarkedWeapons[i] != null && targetData.embarkedWeapons[i].weapon != null)
                cls = targetData.embarkedWeapons[i].weapon.WeaponClass;
            int pointsPerSupply = Mathf.Max(1, Mathf.RoundToInt(ResolveServiceEfficiencyValueForClass(chosenService, MapWeaponClassToArmorWeaponClass(cls))));
            int usedBoxesForWeapon = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
            AccumulateClassMetric(recoveredByClass, cls, recovered);
            AccumulateClassMetric(boxesByClass, cls, usedBoxesForWeapon);
        }

        if (totalRecoveredAmmo <= 0)
            return false;

        foreach (KeyValuePair<SupplyData, int> pair in consumedBySupply)
        {
            int used = Mathf.Max(0, pair.Value);
            if (pair.Key == null || used <= 0)
                continue;
            if (estimate.supplyUsage.TryGetValue(pair.Key, out int consumed))
                estimate.supplyUsage[pair.Key] = consumed + used;
            else
                estimate.supplyUsage.Add(pair.Key, used);
            estimate.totalSupplyUnitsUsed += used;
            boxesUsed += used;
            if (consumedSupply == null)
                consumedSupply = pair.Key;
            consumedAmount += used;
        }

        if (estimate.serviceUsageByTarget.TryGetValue(chosenService, out int countByService))
            estimate.serviceUsageByTarget[chosenService] = countByService + 1;
        else
            estimate.serviceUsageByTarget.Add(chosenService, 1);

        List<int> ammoCostByWeapon = new List<int>();
        int totalCost = ServiceCostFormula.ComputeServiceMoneyCost(
            target,
            chosenService,
            0,
            0,
            totalRecoveredAmmo,
            ammoByWeapon,
            ammoCostByWeapon);
        estimate.totalEstimatedCost += totalCost;
        estimatedCost = totalCost;

        string serviceName = !string.IsNullOrWhiteSpace(chosenService.displayName) ? chosenService.displayName : chosenService.name;
        string partial = totalRecoveredAmmo < totalMissingAmmo ? " (parcial)" : string.Empty;
        usedText = $"{serviceName} Municao {totalRecoveredAmmo}/{totalMissingAmmo}{partial}";
        string detail = BuildAmmoClassBreakdownText(recoveredByClass, boxesByClass);
        string targetName = !string.IsNullOrWhiteSpace(target.name) ? target.name : "Alvo";
        RegisterServiceExecution(
            estimate,
            chosenService,
            targetName,
            "Municao",
            totalRecoveredAmmo,
            totalMissingAmmo,
            estimatedCost,
            consumedSupply,
            consumedAmount,
            detail);
        AccumulateAmmoReportMetrics(estimate, chosenService, ammoCostByWeapon, boxesByClass);
        return true;
    }

    private static void AccumulateAmmoReportMetrics(
        QueueUsageEstimate estimate,
        ServiceData service,
        List<int> ammoCostByWeapon,
        Dictionary<WeaponClass, int> boxesByClass)
    {
        if (estimate == null || service == null)
            return;

        ServiceExecutionSummary summary = GetOrCreateServiceSummary(estimate, service);
        if (ammoCostByWeapon != null)
        {
            for (int i = 0; i < ammoCostByWeapon.Count; i++)
            {
                int cost = Mathf.Max(0, ammoCostByWeapon[i]);
                if (cost <= 0)
                    continue;
                while (summary.ammoCostByWeapon.Count <= i)
                    summary.ammoCostByWeapon.Add(0);
                summary.ammoCostByWeapon[i] += cost;
            }
        }

        if (boxesByClass != null)
        {
            foreach (KeyValuePair<WeaponClass, int> pair in boxesByClass)
            {
                int used = Mathf.Max(0, pair.Value);
                if (used <= 0)
                    continue;
                if (summary.ammoBoxesByClass.TryGetValue(pair.Key, out int current))
                    summary.ammoBoxesByClass[pair.Key] = current + used;
                else
                    summary.ammoBoxesByClass.Add(pair.Key, used);
            }
        }
    }

    private static void AddSupplyConsumption(Dictionary<SupplyData, int> map, SupplyData supply, int amount)
    {
        if (map == null || supply == null || amount <= 0)
            return;

        if (map.TryGetValue(supply, out int current))
            map[supply] = current + amount;
        else
            map.Add(supply, amount);
    }

    private static void RegisterServiceExecution(
        QueueUsageEstimate estimate,
        ServiceData service,
        string targetName,
        string needLabel,
        int recovered,
        int needed,
        int cost,
        SupplyData supply,
        int supplyAmount,
        string detail = null)
    {
        if (estimate == null || service == null || recovered <= 0)
            return;

        ServiceExecutionSummary summary = GetOrCreateServiceSummary(estimate, service);
        string partial = recovered < needed ? " (parcial)" : string.Empty;
        string line = $"{targetName}: {needLabel} {recovered}/{needed}{partial}";
        if (supply != null && supplyAmount > 0)
            line += $" | {ResolveSupplyName(supply)}: {supplyAmount}";
        line += $" | ${Mathf.Max(0, cost)}";
        summary.participantLines.Add(line);
        if (!string.IsNullOrWhiteSpace(detail))
            summary.participantLines.Add($"  - {detail}");

        if (needLabel == "HP")
            summary.recoveredHp += recovered;
        else if (needLabel == "Autonomia")
            summary.recoveredFuel += recovered;
        else if (needLabel == "Municao")
            summary.recoveredAmmo += recovered;

        summary.subtotalCost += Mathf.Max(0, cost);
        if (supply != null && supplyAmount > 0)
        {
            if (summary.consumedBySupply.TryGetValue(supply, out int current))
                summary.consumedBySupply[supply] = current + supplyAmount;
            else
                summary.consumedBySupply.Add(supply, supplyAmount);
        }
    }

    private static ServiceExecutionSummary GetOrCreateServiceSummary(QueueUsageEstimate estimate, ServiceData service)
    {
        if (estimate.serviceSummaries.TryGetValue(service, out ServiceExecutionSummary existing))
            return existing;

        ServiceExecutionSummary created = new ServiceExecutionSummary { service = service };
        estimate.serviceSummaries.Add(service, created);
        return created;
    }

    private static List<ServiceExecutionSummary> BuildOrderedServiceSummaries(Dictionary<ServiceData, ServiceExecutionSummary> map)
    {
        List<ServiceExecutionSummary> ordered = new List<ServiceExecutionSummary>();
        if (map == null || map.Count == 0)
            return ordered;

        ServiceExecutionSummary repair = null;
        ServiceExecutionSummary refuel = null;
        ServiceExecutionSummary rearm = null;
        List<ServiceExecutionSummary> others = new List<ServiceExecutionSummary>();

        foreach (KeyValuePair<ServiceData, ServiceExecutionSummary> pair in map)
        {
            ServiceExecutionSummary summary = pair.Value;
            if (summary == null || summary.service == null)
                continue;

            if (summary.service.recuperaHp)
                repair = summary;
            else if (summary.service.recuperaAutonomia)
                refuel = summary;
            else if (summary.service.recuperaMunicao)
                rearm = summary;
            else
                others.Add(summary);
        }

        if (repair != null)
            ordered.Add(repair);
        if (refuel != null)
            ordered.Add(refuel);
        if (rearm != null)
            ordered.Add(rearm);
        for (int i = 0; i < others.Count; i++)
            ordered.Add(others[i]);
        return ordered;
    }

    private static string BuildServiceSectionTitle(ServiceData service)
    {
        if (service == null)
            return "Servico";

        if (service.recuperaHp)
            return "Reparos (HP)";
        if (service.recuperaAutonomia)
            return "Reabastecimento";
        if (service.recuperaMunicao)
            return "Rearmamento";
        return ResolveServiceName(service);
    }

    private static string ResolveServiceName(ServiceData service)
    {
        if (service == null)
            return "Servico";
        if (!string.IsNullOrWhiteSpace(service.displayName))
            return service.displayName;
        if (!string.IsNullOrWhiteSpace(service.id))
            return service.id;
        return service.name;
    }

    private static string ResolveSupplyName(SupplyData supply)
    {
        if (supply == null)
            return "Supply";
        if (!string.IsNullOrWhiteSpace(supply.displayName))
            return supply.displayName;
        if (!string.IsNullOrWhiteSpace(supply.id))
            return supply.id;
        return supply.name;
    }

    private static bool TryResolveConsumableSupplyForService(
        ServiceData service,
        Dictionary<SupplyData, int> workingStock,
        out SupplyData chosenSupply,
        out int available)
    {
        chosenSupply = null;
        available = 0;
        if (service == null)
            return false;

        if (service.suppliesUsed == null || service.suppliesUsed.Count == 0)
            return true;

        for (int i = 0; i < service.suppliesUsed.Count; i++)
        {
            SupplyData required = service.suppliesUsed[i];
            if (required == null)
                continue;

            int amount = workingStock != null && workingStock.TryGetValue(required, out int current) ? Mathf.Max(0, current) : 0;
            if (amount <= 0)
                continue;

            chosenSupply = required;
            available = amount;
            return true;
        }

        return false;
    }

    private static float ResolveServiceEfficiencyValue(ServiceData service, UnitData targetData)
    {
        if (service == null)
            return 1f;

        ArmorWeaponClass targetClass = ArmorWeaponClass.Light;
        if (targetData != null)
            targetClass = (ArmorWeaponClass)targetData.ArmorClass;

        if (service.serviceEfficiency != null)
        {
            for (int i = 0; i < service.serviceEfficiency.Count; i++)
            {
                ServiceEfficiencyByClass entry = service.serviceEfficiency[i];
                if (entry == null)
                    continue;
                if (entry.armorWeaponClass != targetClass)
                    continue;
                return Mathf.Max(0f, entry.value);
            }
        }

        return 1f;
    }

    private static float ResolveServiceEfficiencyValueForClass(ServiceData service, ArmorWeaponClass classKey)
    {
        if (service == null || service.serviceEfficiency == null || service.serviceEfficiency.Count == 0)
            return 1f;

        for (int i = 0; i < service.serviceEfficiency.Count; i++)
        {
            ServiceEfficiencyByClass entry = service.serviceEfficiency[i];
            if (entry == null || entry.armorWeaponClass != classKey)
                continue;
            return Mathf.Max(0f, entry.value);
        }

        return 1f;
    }

    private static ArmorWeaponClass MapWeaponClassToArmorWeaponClass(WeaponClass weaponClass)
    {
        switch (weaponClass)
        {
            case WeaponClass.Heavy:
                return ArmorWeaponClass.Heavy;
            case WeaponClass.Medium:
                return ArmorWeaponClass.Medium;
            default:
                return ArmorWeaponClass.Light;
        }
    }

    private static int ComputeServiceMoneyCostEstimate(UnitManager target, ServiceData service, int hpGain, int fuelGain, int ammoGain)
    {
        return ServiceCostFormula.ComputeServiceMoneyCost(target, service, hpGain, fuelGain, ammoGain);
    }

    private static void AccumulateClassMetric(Dictionary<WeaponClass, int> map, WeaponClass weaponClass, int amount)
    {
        if (map == null || amount <= 0)
            return;

        if (map.TryGetValue(weaponClass, out int current))
            map[weaponClass] = current + amount;
        else
            map.Add(weaponClass, amount);
    }

    private static string BuildAmmoClassBreakdownText(
        Dictionary<WeaponClass, int> recoveredByClass,
        Dictionary<WeaponClass, int> boxesByClass)
    {
        List<string> chunks = new List<string>();
        AppendAmmoClassBreakdownChunk(chunks, WeaponClass.Light, recoveredByClass, boxesByClass);
        AppendAmmoClassBreakdownChunk(chunks, WeaponClass.Medium, recoveredByClass, boxesByClass);
        AppendAmmoClassBreakdownChunk(chunks, WeaponClass.Heavy, recoveredByClass, boxesByClass);
        return chunks.Count > 0 ? string.Join(" | ", chunks) : string.Empty;
    }

    private static void AppendAmmoClassBreakdownChunk(
        List<string> chunks,
        WeaponClass weaponClass,
        Dictionary<WeaponClass, int> recoveredByClass,
        Dictionary<WeaponClass, int> boxesByClass)
    {
        if (chunks == null)
            return;

        int recovered = recoveredByClass != null && recoveredByClass.TryGetValue(weaponClass, out int r) ? Mathf.Max(0, r) : 0;
        int boxes = boxesByClass != null && boxesByClass.TryGetValue(weaponClass, out int b) ? Mathf.Max(0, b) : 0;
        if (recovered <= 0 && boxes <= 0)
            return;

        chunks.Add($"{ResolveWeaponClassName(weaponClass)}: +{recovered} (caixas: {boxes})");
    }

    private static string ResolveWeaponClassName(WeaponClass weaponClass)
    {
        switch (weaponClass)
        {
            case WeaponClass.Heavy:
                return "Heavy";
            case WeaponClass.Medium:
                return "Medium";
            default:
                return "Light";
        }
    }

    private static SupplyServiceLayerPlan ResolveServiceLayerPlan(
        UnitManager supplier,
        List<SupplyCandidateEntry> queue,
        out string observation)
    {
        observation = "Sem dados.";
        if (supplier == null)
        {
            observation = "Sem supplier selecionado.";
            return SupplyServiceLayerPlan.DefaultSameDomain;
        }

        if (!supplier.TryGetUnitData(out UnitData supplierData) || supplierData == null)
        {
            observation = "Supplier sem UnitData.";
            return SupplyServiceLayerPlan.DefaultSameDomain;
        }

        Domain supplierDomain = supplier.GetDomain();
        HeightLevel supplierHeight = supplier.GetHeightLevel();

        if (queue == null || queue.Count == 0)
        {
            observation = "Sem alvos na fila de suprimento.";
            return SupplyServiceLayerPlan.DefaultSameDomain;
        }

        bool hasDifferentEffectiveLayer = false;
        bool needsAirLow = false;
        bool needsNavalSurface = false;
        bool hasAirTargets = false;
        for (int i = 0; i < queue.Count; i++)
        {
            SupplyCandidateEntry entry = queue[i];
            if (entry == null || entry.unit == null)
                continue;

            Domain effectiveDomain = entry.forceLandBeforeSupply ? supplierDomain : entry.unit.GetDomain();
            HeightLevel effectiveHeight = entry.forceLandBeforeSupply ? supplierHeight : entry.unit.GetHeightLevel();
            if (entry.forceTakeoffBeforeSupply || entry.forceSurfaceBeforeSupply)
            {
                effectiveDomain = entry.plannedServiceDomain;
                effectiveHeight = entry.plannedServiceHeight;
            }

            if (effectiveDomain == Domain.Air)
                hasAirTargets = true;

            if (effectiveDomain != supplierDomain || effectiveHeight != supplierHeight)
                hasDifferentEffectiveLayer = true;

            if (effectiveDomain == Domain.Air && effectiveHeight == HeightLevel.AirLow)
                needsAirLow = true;
            if (effectiveDomain == Domain.Naval && effectiveHeight == HeightLevel.Surface)
                needsNavalSurface = true;
        }

        if (!hasDifferentEffectiveLayer)
        {
            observation = "Todos os alvos da fila serao atendidos na mesma camada/dominio do supplier.";
            return SupplyServiceLayerPlan.DefaultSameDomain;
        }

        bool supportsAirLow = SupportsSupplierOperationLayer(supplierData, Domain.Air, HeightLevel.AirLow);
        bool supportsNavalSurface = SupportsSupplierOperationLayer(supplierData, Domain.Naval, HeightLevel.Surface);

        // Common ground for air logistics: Air/Low.
        // If any target requires transition (land<->air or air high->low scenario), prefer Air/Low when supported.
        if (hasAirTargets && supportsAirLow && hasDifferentEffectiveLayer)
        {
            observation = "Fila aerea com camadas mistas: atendimento converge para Air/Low (alvos aereos nivelam e pousados decolam para Air/Low).";
            return SupplyServiceLayerPlan.AirLow;
        }

        if (needsAirLow && supportsAirLow)
        {
            observation = "Fila possui alvo em Air/Low; supplier usa Supplier Operation Domain Air/Low.";
            return SupplyServiceLayerPlan.AirLow;
        }

        if (needsNavalSurface && supportsNavalSurface)
        {
            observation = "Fila possui alvo em Naval/Surface; supplier usa Supplier Operation Domain Naval/Surface.";
            return SupplyServiceLayerPlan.NavalSurface;
        }

        if (needsAirLow && !supportsAirLow)
        {
            observation = "Fila pede Air/Low, mas o supplier nao possui esse Supplier Operation Domain. Mantendo same domain.";
            return SupplyServiceLayerPlan.DefaultSameDomain;
        }

        if (needsNavalSurface && !supportsNavalSurface)
        {
            observation = "Fila pede Naval/Surface, mas o supplier nao possui esse Supplier Operation Domain. Mantendo same domain.";
            return SupplyServiceLayerPlan.DefaultSameDomain;
        }

        if (supportsAirLow)
        {
            observation = "Fila em camada mista; fallback para Supplier Operation Domain Air/Low.";
            return SupplyServiceLayerPlan.AirLow;
        }

        if (supportsNavalSurface)
        {
            observation = "Fila em camada mista; fallback para Supplier Operation Domain Naval/Surface.";
            return SupplyServiceLayerPlan.NavalSurface;
        }

        observation = "Fila em camada mista, sem Supplier Operation Domain compativel. Mantendo same domain.";
        return SupplyServiceLayerPlan.DefaultSameDomain;
    }

    private static bool SupportsSupplierOperationLayer(UnitData supplierData, Domain domain, HeightLevel height)
    {
        if (supplierData == null || supplierData.supplierOperationDomains == null)
            return false;

        for (int i = 0; i < supplierData.supplierOperationDomains.Count; i++)
        {
            SupplierOperationDomain mode = supplierData.supplierOperationDomains[i];
            if (mode.domain == domain && mode.heightLevel == height)
                return true;
        }

        return false;
    }


    private static bool IsSupplierCurrentlyInOperationDomain(UnitManager supplier, UnitData supplierData)
    {
        if (supplier == null || supplierData == null)
            return false;

        return SupportsSupplierOperationLayer(supplierData, supplier.GetDomain(), supplier.GetHeightLevel());
    }

    private static bool HasAtLeastOneOperationalService(UnitManager supplier, UnitData supplierData)
    {
        if (supplierData == null || supplierData.supplierServicesProvided == null)
            return false;

        for (int i = 0; i < supplierData.supplierServicesProvided.Count; i++)
        {
            ServiceData service = supplierData.supplierServicesProvided[i];
            if (service != null && service.isService && ServiceHasAvailableSupplies(supplier, service))
                return true;
        }

        return false;
    }

    private void HandleKeyboardShortcuts()
    {
        Event current = Event.current;
        if (current == null || current.type != EventType.KeyDown)
            return;
        if (EditorGUIUtility.editingTextField)
            return;

        if (current.keyCode >= KeyCode.Alpha1 && current.keyCode <= KeyCode.Alpha9)
        {
            int index = current.keyCode - KeyCode.Alpha1;
            SelectCandidateByIndex(index);
            current.Use();
            Repaint();
            return;
        }

        if (current.keyCode >= KeyCode.Keypad1 && current.keyCode <= KeyCode.Keypad9)
        {
            int index = current.keyCode - KeyCode.Keypad1;
            SelectCandidateByIndex(index);
            current.Use();
            Repaint();
            return;
        }

        if (current.keyCode == KeyCode.UpArrow)
        {
            CycleCandidateSelection(-1);
            current.Use();
            Repaint();
            return;
        }

        if (current.keyCode == KeyCode.DownArrow)
        {
            CycleCandidateSelection(+1);
            current.Use();
            Repaint();
            return;
        }

        if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
        {
            if (TryGetSelectedCandidate(out SupplyCandidateEntry selected))
                TryAddCandidateToQueue(selected);
            current.Use();
            Repaint();
            return;
        }

        if (current.keyCode == KeyCode.Alpha0 || current.keyCode == KeyCode.Keypad0)
        {
            if (supplyQueue.Count > 0)
                ExecutePartialSupplyOrderDebug();
            current.Use();
            Repaint();
        }
    }

    private void SelectCandidateByIndex(int index)
    {
        if (index < 0 || index >= eligibleCandidates.Count)
            return;

        selectedCandidateIndex = index;
        SupplyCandidateEntry entry = eligibleCandidates[index];
        if (entry != null && entry.unit != null)
            SelectLineForDrawing(entry.unit);
    }

    private void CycleCandidateSelection(int delta)
    {
        if (eligibleCandidates.Count <= 0)
            return;

        if (selectedCandidateIndex < 0 || selectedCandidateIndex >= eligibleCandidates.Count)
            selectedCandidateIndex = 0;
        else
            selectedCandidateIndex = (selectedCandidateIndex + delta + eligibleCandidates.Count) % eligibleCandidates.Count;

        SupplyCandidateEntry entry = eligibleCandidates[selectedCandidateIndex];
        if (entry != null && entry.unit != null)
            SelectLineForDrawing(entry.unit);
    }

    private bool TryGetSelectedCandidate(out SupplyCandidateEntry selected)
    {
        selected = null;
        if (selectedCandidateIndex < 0 || selectedCandidateIndex >= eligibleCandidates.Count)
            return false;
        selected = eligibleCandidates[selectedCandidateIndex];
        return selected != null && selected.unit != null;
    }

    private void TryAddCandidateToQueue(SupplyCandidateEntry candidate)
    {
        if (candidate == null || candidate.unit == null)
            return;

        if (IsCandidateAlreadyQueued(candidate.unit))
        {
            queueMessage = $"{candidate.unit.name} ja esta na fila.";
            return;
        }

        int limit = Mathf.Max(0, maxUnitsServedPerTurn);
        if (supplyQueue.Count >= limit)
        {
            queueMessage = $"Fila cheia para este supplier (max {limit}).";
            return;
        }

        supplyQueue.Add(candidate);
        queueMessage = $"Candidato adicionado: {candidate.unit.name}.";
        RebuildCandidateLists(ResolveTilemap());
    }

    private bool IsCandidateAlreadyQueued(UnitManager unit)
    {
        if (unit == null)
            return false;

        for (int i = 0; i < supplyQueue.Count; i++)
        {
            SupplyCandidateEntry entry = supplyQueue[i];
            if (entry != null && entry.unit == unit)
                return true;
        }
        return false;
    }

    private void ExecutePartialSupplyOrderDebug()
    {
        if (selectedSupplier == null || supplyQueue.Count <= 0)
            return;

        string list = string.Empty;
        for (int i = 0; i < supplyQueue.Count; i++)
        {
            SupplyCandidateEntry entry = supplyQueue[i];
            if (entry == null || entry.unit == null)
                continue;
            if (list.Length > 0)
                list += ", ";
            list += entry.unit.name;
        }

        statusMessage = $"Execucao parcial [debug] simulada. Supplier={selectedSupplier.name} | alvos={list}";
        Debug.Log($"[PodeSuprirSensorDebug] {statusMessage}");
        supplyQueue.Clear();
        queueMessage = "Fila vazia apos execucao parcial [debug].";
        RebuildCandidateLists(ResolveTilemap());
    }

    private void RunSimulation()
    {
        eligibleCandidates.Clear();
        ineligibleCandidates.Clear();
        selectedCandidateIndex = -1;
        selectedInvalidIndex = -1;
        ClearSelectedLine();
        canSupply = false;
        sensorReason = string.Empty;
        maxUnitsServedPerTurn = 0;

        if (selectedSupplier == null)
        {
            statusMessage = "Selecione uma unidade supplier valida.";
            return;
        }

        Tilemap map = ResolveTilemap();
        if (map == null)
        {
            statusMessage = "Tilemap base nao encontrado.";
            return;
        }

        RebuildCandidateLists(map);
        statusMessage = canSupply
            ? $"Sensor TRUE. {eligibleCandidates.Count} candidato(s) valido(s)."
            : "Sensor FALSE. Suprimento indisponivel.";
    }

    private void RebuildCandidateLists(Tilemap map)
    {
        eligibleCandidates.Clear();
        ineligibleCandidates.Clear();
        selectedCandidateIndex = -1;
        selectedInvalidIndex = -1;

        if (selectedSupplier == null || map == null)
        {
            canSupply = false;
            sensorReason = "Sem contexto valido.";
            maxUnitsServedPerTurn = 0;
            ClearSelectedLine();
            return;
        }

        if (!selectedSupplier.TryGetUnitData(out UnitData supplierData) || supplierData == null)
        {
            canSupply = false;
            sensorReason = "Supplier sem UnitData.";
            maxUnitsServedPerTurn = 0;
            ClearSelectedLine();
            return;
        }

        if (!supplierData.isSupplier)
        {
            canSupply = false;
            sensorReason = "Unidade selecionada nao eh supplier.";
            maxUnitsServedPerTurn = 0;
            ClearSelectedLine();
            return;
        }

        if (!HasAtLeastOneOperationalService(selectedSupplier, supplierData))
        {
            canSupply = false;
            sensorReason = "Supplier sem servico operacional com estoque disponivel. Atua apenas como hub/carga.";
            maxUnitsServedPerTurn = 0;
            ClearSelectedLine();
            return;
        }

        maxUnitsServedPerTurn = Mathf.Max(0, supplierData.maxUnitsServedPerTurn);
        if (maxUnitsServedPerTurn <= 0)
        {
            canSupply = false;
            sensorReason = "Supplier sem capacidade de atendimento (maxUnitsServedPerTurn=0).";
            ClearSelectedLine();
            return;
        }

        if (!IsSupplierCurrentlyInOperationDomain(selectedSupplier, supplierData))
        {
            canSupply = false;
            sensorReason =
                $"Supplier fora do Supplier Operation Domain atual ({selectedSupplier.GetDomain()}/{selectedSupplier.GetHeightLevel()}). " +
                "Reposicione para um dominio/altura permitido para prestar servicos.";
            ClearSelectedLine();
            return;
        }

        Vector3Int origin = selectedSupplier.CurrentCellPosition;
        origin.z = 0;
        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(map, origin, neighbors);

        List<SupplyCandidateEntry> prelim = new List<SupplyCandidateEntry>();
        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int cell = neighbors[i];
            cell.z = 0;
            UnitManager other = UnitOccupancyRules.GetUnitAtCell(map, cell, selectedSupplier);
            if (other == null || other == selectedSupplier)
                continue;

            if (!other.gameObject.activeInHierarchy)
            {
                AddInvalid(other, cell, "Unidade inativa.");
                continue;
            }

            if (other.IsEmbarked)
            {
                AddInvalid(other, cell, "Unidade embarcada.");
                continue;
            }

            if ((int)other.TeamId != (int)selectedSupplier.TeamId)
            {
                AddInvalid(other, cell, "Unidade de outro time.");
                continue;
            }

            if (!TryResolveServiceDemandMatch(selectedSupplier, supplierData, other, out string serviceDemandReason))
            {
                AddInvalid(other, cell, serviceDemandReason);
                continue;
            }

            bool sameDomain = other.GetDomain() == selectedSupplier.GetDomain();
            bool needsForcedSurfaceBeforeSupply = IsSubmergedNavalUnit(other);
            if (needsForcedSurfaceBeforeSupply)
            {
                if (!CanEmergeToNavalSurface(other))
                {
                    AddInvalid(other, cell, "Alvo submerso nao possui modo Naval/Surface para emergir antes do suprimento.");
                    continue;
                }

                bool supplierCanServeNavalSurface =
                    (selectedSupplier.GetDomain() == Domain.Naval && selectedSupplier.GetHeightLevel() == HeightLevel.Surface) ||
                    SupportsSupplierOperationLayer(supplierData, Domain.Naval, HeightLevel.Surface);

                if (!supplierCanServeNavalSurface)
                {
                    AddInvalid(other, cell, "Alvo submerso exige emergencia para suprimento em Naval/Surface, mas o supplier nao atende essa camada.");
                    continue;
                }

                if (!sameDomain)
                {
                    prelim.Add(new SupplyCandidateEntry
                    {
                        unit = other,
                        cell = cell,
                        mode = "Dominio diferente (emergencia valida -> Naval/Surface)",
                        forceLandBeforeSupply = false,
                        forceTakeoffBeforeSupply = false,
                        forceSurfaceBeforeSupply = true,
                        plannedServiceDomain = Domain.Naval,
                        plannedServiceHeight = HeightLevel.Surface
                    });
                    continue;
                }
            }

            if (sameDomain)
            {
                prelim.Add(new SupplyCandidateEntry
                {
                    unit = other,
                    cell = cell,
                    mode = needsForcedSurfaceBeforeSupply ? "Mesmo dominio (emerge para suprir)" : "Mesmo dominio",
                    forceLandBeforeSupply = false,
                    forceTakeoffBeforeSupply = false,
                    forceSurfaceBeforeSupply = needsForcedSurfaceBeforeSupply,
                    plannedServiceDomain = needsForcedSurfaceBeforeSupply ? Domain.Naval : other.GetDomain(),
                    plannedServiceHeight = needsForcedSurfaceBeforeSupply ? HeightLevel.Surface : other.GetHeightLevel()
                });
                continue;
            }

            if (terrainDatabase == null)
            {
                AddInvalid(other, cell, "TerrainDatabase ausente para validar pouso AR.");
                continue;
            }

            PodePousarReport landing = PodePousarSensor.Evaluate(
                other,
                map,
                terrainDatabase,
                SensorMovementMode.MoveuParado,
                useManualRemainingMovement: false,
                manualRemainingMovement: 0);

            if (landing != null && landing.status)
            {
                prelim.Add(new SupplyCandidateEntry
                {
                    unit = other,
                    cell = cell,
                    mode = "Dominio diferente (pouso valido)",
                    forceLandBeforeSupply = true,
                    forceTakeoffBeforeSupply = false,
                    forceSurfaceBeforeSupply = false,
                    plannedServiceDomain = selectedSupplier.GetDomain(),
                    plannedServiceHeight = selectedSupplier.GetHeightLevel()
                });
            }
            else
            {
                bool isAirFamily = IsAirFamilyUnit(other);
                if (isAirFamily)
                {
                    PodeDecolarReport takeoff = PodeDecolarSensor.Evaluate(other, map, terrainDatabase);
                    if (takeoff != null && takeoff.status)
                    {
                        HeightLevel takeoffHeight = ResolveTakeoffServiceHeight(other);
                        bool supplierSupportsTakeoffLayer =
                            SupportsSupplierOperationLayer(supplierData, Domain.Air, takeoffHeight) ||
                            (selectedSupplier.GetDomain() == Domain.Air && selectedSupplier.GetHeightLevel() == takeoffHeight);

                        if (supplierSupportsTakeoffLayer)
                        {
                            prelim.Add(new SupplyCandidateEntry
                            {
                                unit = other,
                                cell = cell,
                                mode = $"Dominio diferente (decolagem valida -> Air/{takeoffHeight})",
                                forceLandBeforeSupply = false,
                                forceTakeoffBeforeSupply = true,
                                forceSurfaceBeforeSupply = false,
                                plannedServiceDomain = Domain.Air,
                                plannedServiceHeight = takeoffHeight
                            });
                            continue;
                        }

                        AddInvalid(other, cell, $"Decolagem valida, mas supplier nao atende Air/{takeoffHeight} no Supplier Operation Domain.");
                        continue;
                    }
                }

                string landingReason = landing != null && !string.IsNullOrWhiteSpace(landing.explicacao)
                    ? landing.explicacao
                    : "Pouso invalido no hex atual.";
                string reason = $"Dominio incompativel ({other.GetDomain()} != {selectedSupplier.GetDomain()}) e sem pouso/decolagem validos. {landingReason}";
                AddInvalid(other, cell, reason);
            }
        }

        SyncQueueWithCurrentCandidates(prelim);

        for (int i = 0; i < prelim.Count; i++)
        {
            SupplyCandidateEntry candidate = prelim[i];
            if (candidate == null || candidate.unit == null)
                continue;
            if (IsCandidateAlreadyQueued(candidate.unit))
                continue;
            eligibleCandidates.Add(candidate);
        }

        canSupply = eligibleCandidates.Count > 0 || supplyQueue.Count > 0;
        sensorReason = canSupply ? string.Empty : "Sem candidatos adjacentes validos para suprir.";

        if (eligibleCandidates.Count > 0)
        {
            selectedCandidateIndex = 0;
            SelectLineForDrawing(eligibleCandidates[0].unit);
        }
        else
        {
            ClearSelectedLine();
        }
    }

    private void SyncQueueWithCurrentCandidates(List<SupplyCandidateEntry> currentValid)
    {
        if (currentValid == null)
            currentValid = new List<SupplyCandidateEntry>();

        for (int i = supplyQueue.Count - 1; i >= 0; i--)
        {
            SupplyCandidateEntry queued = supplyQueue[i];
            if (queued == null || queued.unit == null)
            {
                supplyQueue.RemoveAt(i);
                continue;
            }

            bool stillValid = false;
            for (int j = 0; j < currentValid.Count; j++)
            {
                SupplyCandidateEntry candidate = currentValid[j];
                if (candidate != null && candidate.unit == queued.unit)
                {
                    stillValid = true;
                    queued.mode = candidate.mode;
                    queued.cell = candidate.cell;
                    queued.forceLandBeforeSupply = candidate.forceLandBeforeSupply;
                    queued.forceTakeoffBeforeSupply = candidate.forceTakeoffBeforeSupply;
                    queued.forceSurfaceBeforeSupply = candidate.forceSurfaceBeforeSupply;
                    queued.plannedServiceDomain = candidate.plannedServiceDomain;
                    queued.plannedServiceHeight = candidate.plannedServiceHeight;
                    break;
                }
            }

            if (!stillValid)
                supplyQueue.RemoveAt(i);
        }

        int limit = Mathf.Max(0, maxUnitsServedPerTurn);
        while (supplyQueue.Count > limit)
            supplyQueue.RemoveAt(supplyQueue.Count - 1);
    }

    private void AddInvalid(UnitManager unit, Vector3Int cell, string reason)
    {
        ineligibleCandidates.Add(new IneligibleSupplyEntry
        {
            unit = unit,
            cell = cell,
            reason = reason
        });
    }

    private void TryUseCurrentSelection()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
            return;

        UnitManager unit = go.GetComponent<UnitManager>();
        if (unit == null)
            unit = go.GetComponentInParent<UnitManager>();
        if (unit != null)
            selectedSupplier = unit;
    }

    private void AutoDetectContext()
    {
        if (selectedSupplier == null)
            selectedSupplier = FindAnyObjectByType<TurnStateManager>()?.SelectedUnit;
        if (selectedSupplier == null)
            TryUseCurrentSelection();
        if (overrideTilemap == null)
            overrideTilemap = selectedSupplier != null ? selectedSupplier.BoardTilemap : FindPreferredTilemap();
        if (terrainDatabase == null)
            terrainDatabase = FindFirstTerrainDatabaseAsset();
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;
        if (selectedSupplier != null && selectedSupplier.BoardTilemap != null)
            return selectedSupplier.BoardTilemap;
        return FindPreferredTilemap();
    }

    private void SelectLineForDrawing(UnitManager target)
    {
        SelectLineForDrawing(target, TeamUtils.GetColor(selectedSupplier != null ? selectedSupplier.TeamId : TeamId.Green), null);
    }

    private void SelectLineForDrawing(UnitManager target, Color color, string customLabel)
    {
        if (selectedSupplier == null || target == null)
        {
            ClearSelectedLine();
            return;
        }

        selectedLineStartCell = selectedSupplier.CurrentCellPosition;
        selectedLineStartCell.z = 0;
        selectedLineEndCell = target.CurrentCellPosition;
        selectedLineEndCell.z = 0;
        selectedLineColor = color;
        selectedLineLabel = string.IsNullOrWhiteSpace(customLabel)
            ? $"Suprimento: {selectedSupplier.name} -> {target.name}"
            : customLabel;
        hasSelectedLine = true;
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

        Vector3 start = map.GetCellCenterWorld(selectedLineStartCell);
        Vector3 end = map.GetCellCenterWorld(selectedLineEndCell);

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = selectedLineColor;
        Handles.DrawAAPolyLine(4f, start, end);
        Handles.SphereHandleCap(0, start, Quaternion.identity, 0.12f, EventType.Repaint);
        Handles.SphereHandleCap(0, end, Quaternion.identity, 0.12f, EventType.Repaint);

        Vector3 mid = Vector3.Lerp(start, end, 0.5f);
        Handles.Label(mid + new Vector3(0.1f, 0.1f, 0f), selectedLineLabel);
    }

    private static Tilemap FindPreferredTilemap()
    {
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (maps == null || maps.Length == 0)
            return null;

        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map != null && string.Equals(map.name, "Tilemap", System.StringComparison.OrdinalIgnoreCase))
                return map;
        }

        return maps[0];
    }

    private static TerrainDatabase FindFirstTerrainDatabaseAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TerrainDatabase db = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(path);
            if (db != null)
                return db;
        }

        return null;
    }

    private static bool TryResolveServiceDemandMatch(UnitManager supplier, UnitData supplierData, UnitManager target, out string reason)
    {
        reason = string.Empty;
        if (supplierData == null)
        {
            reason = "Supplier sem UnitData para validar servicos.";
            return false;
        }

        if (target == null)
        {
            reason = "Unidade alvo invalida.";
            return false;
        }

        bool needsHp = target.CurrentHP < target.GetMaxHP();
        bool needsFuel = target.CurrentFuel < target.GetMaxFuel();
        bool needsAmmo = IsAnyAmmoMissing(target);
        bool targetIsSupplier = target.TryGetUnitData(out UnitData targetData) && targetData != null && targetData.isSupplier;
        if (!needsHp && !needsFuel && !needsAmmo)
        {
            reason = "Sem necessidade de servico (HP, autonomia e municao ja estao cheios).";
            return false;
        }

        List<ServiceData> matchedServices = ResolveMatchingServicesForTarget(
            supplier,
            supplierData,
            target,
            out needsHp,
            out needsFuel,
            out needsAmmo);
        if (matchedServices.Count > 0)
            return true;

        ResolveSupplierServiceCoverage(
            supplier,
            supplierData,
            targetIsSupplier,
            out bool canRepair,
            out bool canRefuel,
            out bool canRearm);

        bool offersAnyRecoveryService = canRepair || canRefuel || canRearm;
        if (!offersAnyRecoveryService)
        {
            reason = "Supplier nao oferece servico de recuperacao compativel (HP, autonomia ou municao) para este alvo.";
            return false;
        }

        bool hasAtLeastOneMatchingNeed = (needsHp && canRepair) ||
                                         (needsFuel && canRefuel) ||
                                         (needsAmmo && canRearm);
        if (hasAtLeastOneMatchingNeed)
            return true;

        reason = BuildServiceMismatchReason(needsHp, needsFuel, needsAmmo, canRepair, canRefuel, canRearm);
        return false;
    }

    private static List<ServiceData> ResolveMatchingServicesForTarget(
        UnitManager supplier,
        UnitData supplierData,
        UnitManager target,
        out bool needsHp,
        out bool needsFuel,
        out bool needsAmmo)
    {
        List<ServiceData> result = new List<ServiceData>();
        needsHp = false;
        needsFuel = false;
        needsAmmo = false;

        if (supplierData == null || supplierData.supplierServicesProvided == null || target == null)
            return result;

        needsHp = target.CurrentHP < target.GetMaxHP();
        needsFuel = target.CurrentFuel < target.GetMaxFuel();
        needsAmmo = IsAnyAmmoMissing(target);

        if (!needsHp && !needsFuel && !needsAmmo)
            return result;

        bool targetIsSupplier = target.TryGetUnitData(out UnitData targetData) && targetData != null && targetData.isSupplier;
        for (int i = 0; i < supplierData.supplierServicesProvided.Count; i++)
        {
            ServiceData service = supplierData.supplierServicesProvided[i];
            if (service == null || !service.isService)
                continue;
            if (service.apenasEntreSupridores && !targetIsSupplier)
                continue;
            if (!ServiceHasAvailableSupplies(supplier, service))
                continue;

            bool matchesAnyNeed = (needsHp && service.recuperaHp) ||
                                  (needsFuel && service.recuperaAutonomia) ||
                                  (needsAmmo && service.recuperaMunicao);
            if (!matchesAnyNeed)
                continue;

            if (!result.Contains(service))
                result.Add(service);
        }

        return result;
    }

    private static bool IsAnyAmmoMissing(UnitManager unit)
    {
        if (unit == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = unit.GetEmbarkedWeapons();
        if (!unit.TryGetUnitData(out UnitData unitData) || unitData == null || unitData.embarkedWeapons == null)
            return unit.CurrentAmmo < unit.GetMaxAmmo();

        if (runtimeWeapons == null || runtimeWeapons.Count == 0 || unitData.embarkedWeapons.Count == 0)
            return unit.CurrentAmmo < unit.GetMaxAmmo();

        int count = Mathf.Min(runtimeWeapons.Count, unitData.embarkedWeapons.Count);
        for (int i = 0; i < count; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = unitData.embarkedWeapons[i];
            if (runtime == null || baseline == null)
                continue;

            if (Mathf.Max(0, runtime.squadAmmunition) < Mathf.Max(0, baseline.squadAmmunition))
                return true;
        }

        // When unit has embarked weapons, that ammo state is the source of truth.
        return false;
    }

    private static void ResolveSupplierServiceCoverage(
        UnitManager supplier,
        UnitData supplierData,
        bool targetIsSupplier,
        out bool canRepair,
        out bool canRefuel,
        out bool canRearm)
    {
        canRepair = false;
        canRefuel = false;
        canRearm = false;

        if (supplierData == null || supplierData.supplierServicesProvided == null)
            return;

        for (int i = 0; i < supplierData.supplierServicesProvided.Count; i++)
        {
            ServiceData service = supplierData.supplierServicesProvided[i];
            if (service == null || !service.isService)
                continue;
            if (service.apenasEntreSupridores && !targetIsSupplier)
                continue;
            if (!ServiceHasAvailableSupplies(supplier, service))
                continue;

            if (service.recuperaHp)
                canRepair = true;
            if (service.recuperaAutonomia)
                canRefuel = true;
            if (service.recuperaMunicao)
                canRearm = true;
        }
    }

    private static bool ServiceHasAvailableSupplies(UnitManager supplier, ServiceData service)
    {
        if (service == null)
            return false;

        if (service.suppliesUsed == null || service.suppliesUsed.Count == 0)
            return true;

        IReadOnlyList<UnitEmbarkedSupply> runtimeSupplies = supplier != null ? supplier.GetEmbarkedResources() : null;
        if (runtimeSupplies == null || runtimeSupplies.Count == 0)
            return false;

        bool hasDefinedSupplyRequirement = false;
        for (int i = 0; i < service.suppliesUsed.Count; i++)
        {
            SupplyData required = service.suppliesUsed[i];
            if (required == null)
                continue;

            hasDefinedSupplyRequirement = true;
            if (HasRuntimeSupplyAmount(runtimeSupplies, required))
                return true;
        }

        return !hasDefinedSupplyRequirement;
    }

    private static bool HasRuntimeSupplyAmount(IReadOnlyList<UnitEmbarkedSupply> runtimeSupplies, SupplyData required)
    {
        if (runtimeSupplies == null || required == null)
            return false;

        for (int i = 0; i < runtimeSupplies.Count; i++)
        {
            UnitEmbarkedSupply embarked = runtimeSupplies[i];
            if (embarked == null || embarked.supply == null)
                continue;
            if (embarked.supply != required)
                continue;
            if (Mathf.Max(0, embarked.amount) > 0)
                return true;
        }

        return false;
    }

    private static string BuildServiceMismatchReason(
        bool needsHp,
        bool needsFuel,
        bool needsAmmo,
        bool canRepair,
        bool canRefuel,
        bool canRearm)
    {
        List<string> needs = new List<string>();
        if (needsHp)
            needs.Add("HP");
        if (needsFuel)
            needs.Add("Autonomia");
        if (needsAmmo)
            needs.Add("Municao");

        List<string> offers = new List<string>();
        if (canRepair)
            offers.Add("Reparo");
        if (canRefuel)
            offers.Add("Reabastecimento");
        if (canRearm)
            offers.Add("Rearmamento");

        string needsText = needs.Count > 0 ? string.Join(", ", needs) : "nenhuma";
        string offersText = offers.Count > 0 ? string.Join(", ", offers) : "nenhum servico compativel";
        return $"Sem match entre necessidade ({needsText}) e servicos oferecidos ({offersText}).";
    }

    private static bool IsAirFamilyUnit(UnitManager unit)
    {
        if (unit == null)
            return false;

        if (unit.GetDomain() == Domain.Air)
            return true;
        if (unit.SupportsLayerMode(Domain.Air, HeightLevel.AirLow) || unit.SupportsLayerMode(Domain.Air, HeightLevel.AirHigh))
            return true;

        if (!unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        return data.IsAircraft();
    }

    private static HeightLevel ResolveTakeoffServiceHeight(UnitManager unit)
    {
        // Para suprimento entre dominios, o common ground aereo e Air/Low.
        return HeightLevel.AirLow;
    }

    private static bool ShouldLevelToAirLowBeforeSupply(SupplyCandidateEntry entry, SupplyServiceLayerPlan plan)
    {
        if (entry == null || entry.unit == null)
            return false;
        if (plan != SupplyServiceLayerPlan.AirLow)
            return false;
        if (entry.forceLandBeforeSupply || entry.forceTakeoffBeforeSupply)
            return false;

        return entry.unit.GetDomain() == Domain.Air && entry.unit.GetHeightLevel() == HeightLevel.AirHigh;
    }

    private static bool IsSubmergedNavalUnit(UnitManager unit)
    {
        return unit != null &&
               (unit.GetDomain() == Domain.Naval || unit.GetDomain() == Domain.Submarine) &&
               unit.GetHeightLevel() == HeightLevel.Submerged;
    }

    private static bool CanEmergeToNavalSurface(UnitManager unit)
    {
        return unit != null && unit.SupportsLayerMode(Domain.Naval, HeightLevel.Surface);
    }
}

