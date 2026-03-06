using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ServicoDoComandoDebugWindow : EditorWindow
{
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private TeamId teamOverride = TeamId.Green;
    [SerializeField] private bool useActiveTeamFromMatch = true;

    private readonly List<ServicoDoComandoOption> eligibleOptions = new List<ServicoDoComandoOption>();
    private readonly List<ServicoDoComandoInvalidOption> invalidOptions = new List<ServicoDoComandoInvalidOption>();
    private string sensorReason = "Ready.";
    private string status = "Ready.";
    private Vector2 scroll;

    private sealed class CostEstimateBreakdown
    {
        public int totalMoney;
        public readonly List<ServiceEstimateLine> serviceLines = new List<ServiceEstimateLine>();
        public readonly Dictionary<SupplyData, int> consumedBySupply = new Dictionary<SupplyData, int>();
    }

    private sealed class ServiceEstimateLine
    {
        public string summary;
        public readonly List<string> details = new List<string>();
    }

    [MenuItem("Tools/Logistica/Servico do Comando")]
    public static void OpenWindow()
    {
        GetWindow<ServicoDoComandoDebugWindow>("Servico do Comando");
    }

    private void OnEnable()
    {
        AutoDetectContext();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Servico do Comando [Debug]", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Escaneia unidades sobre construcoes aliadas supridoras e embarcadas em transportadores supridores.\n" +
            "Ao iniciar a ordem, atende os elegiveis em fila e marca Received Supply This Turn.",
            MessageType.Info);

        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        useActiveTeamFromMatch = EditorGUILayout.ToggleLeft("Usar time ativo do MatchController", useActiveTeamFromMatch);
        using (new EditorGUI.DisabledScope(useActiveTeamFromMatch))
            teamOverride = (TeamId)EditorGUILayout.EnumPopup("Time (manual)", teamOverride);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Simular"))
            RunSimulation();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(status, MessageType.None);
        if (!string.IsNullOrWhiteSpace(sensorReason))
            EditorGUILayout.HelpBox($"Sensor: {sensorReason}", eligibleOptions.Count > 0 ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.Space(8f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.MinWidth(420f), GUILayout.ExpandWidth(true));
        DrawEligibleSection();
        EditorGUILayout.Space(8f);
        DrawInvalidSection();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(12f);
        EditorGUILayout.BeginVertical(GUILayout.Width(420f));
        DrawCostReportSection();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(eligibleOptions.Count <= 0))
        {
            if (GUILayout.Button("Iniciar ordem do comando [debug]"))
                ExecuteDebugOrder();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawEligibleSection()
    {
        EditorGUILayout.LabelField($"Candidatos elegiveis ({eligibleOptions.Count})", EditorStyles.boldLabel);
        if (eligibleOptions.Count <= 0)
        {
            EditorGUILayout.HelpBox("Nenhum elegivel.", MessageType.Info);
            return;
        }

        for (int i = 0; i < eligibleOptions.Count; i++)
        {
            ServicoDoComandoOption option = eligibleOptions[i];
            if (option == null || option.targetUnit == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"{i + 1}. {option.targetUnit.name}");
            if (option.sourceConstruction != null)
                EditorGUILayout.LabelField("Origem", ResolveConstructionLabel(option.sourceConstruction));
            else if (option.sourceSupplierUnit != null)
                EditorGUILayout.LabelField("Origem", ResolveSupplierUnitLabel(option.sourceSupplierUnit));
            EditorGUILayout.LabelField("Hex", $"{option.targetCell.x},{option.targetCell.y}");
            EditorGUILayout.LabelField("Camada planejada", $"{option.plannedServiceDomain}/{option.plannedServiceHeight}");
            string servicesText = (option.plannedServices != null && option.plannedServices.Count > 0)
                ? string.Join(", ", option.plannedServices)
                : "-";
            EditorGUILayout.LabelField("Servicos previstos", servicesText);
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawInvalidSection()
    {
        EditorGUILayout.LabelField($"Candidatos invalidos ({invalidOptions.Count})", EditorStyles.boldLabel);
        if (invalidOptions.Count <= 0)
        {
            EditorGUILayout.HelpBox("Nenhum invalido.", MessageType.Info);
            return;
        }

        for (int i = 0; i < invalidOptions.Count; i++)
        {
            ServicoDoComandoInvalidOption entry = invalidOptions[i];
            if (entry == null || entry.targetUnit == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"{i + 1}. {entry.targetUnit.name}");
            if (entry.sourceConstruction != null)
                EditorGUILayout.LabelField("Origem", ResolveConstructionLabel(entry.sourceConstruction));
            else if (entry.sourceSupplierUnit != null)
                EditorGUILayout.LabelField("Origem", ResolveSupplierUnitLabel(entry.sourceSupplierUnit));
            EditorGUILayout.LabelField("Hex", $"{entry.targetCell.x},{entry.targetCell.y}");
            EditorGUILayout.LabelField("Motivo", string.IsNullOrWhiteSpace(entry.reason) ? "-" : entry.reason);
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawCostReportSection()
    {
        EditorGUILayout.LabelField("Relatorio de Custos (estimado)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        TeamId team = ResolveTargetTeam();
        MatchController match = FindAnyObjectByType<MatchController>();
        int availableMoney = match != null ? Mathf.Max(0, match.GetActualMoney(team)) : 0;

        EditorGUILayout.LabelField("Time", $"{TeamUtils.GetName(team)} ({(int)team})");
        EditorGUILayout.LabelField("Saldo atual", match != null ? $"${availableMoney}" : "(MatchController nao encontrado)");
        EditorGUILayout.Space(4f);
        int totalEligibleCost = 0;

        EditorGUILayout.LabelField($"Elegiveis ({eligibleOptions.Count})", EditorStyles.boldLabel);
        if (eligibleOptions.Count <= 0)
        {
            EditorGUILayout.LabelField("- nenhum", EditorStyles.wordWrappedMiniLabel);
        }
        else
        {
            for (int i = 0; i < eligibleOptions.Count; i++)
            {
                ServicoDoComandoOption option = eligibleOptions[i];
                if (option == null || option.targetUnit == null)
                    continue;

                CostEstimateBreakdown estimate = EstimateOptionCost(option, match);
                int estimated = Mathf.Max(0, estimate.totalMoney);
                totalEligibleCost += estimated;
                bool affordable = match == null || estimated <= availableMoney;
                string source = option.sourceConstruction != null
                    ? ResolveConstructionLabel(option.sourceConstruction)
                    : ResolveSupplierUnitLabel(option.sourceSupplierUnit);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"{i + 1}. {option.targetUnit.name}");
                EditorGUILayout.LabelField("Origem", source);
                EditorGUILayout.LabelField("Custo estimado", $"${estimated}");
                EditorGUILayout.LabelField("Status", affordable ? "OK" : "SEM SALDO");
                DrawServiceLines(estimate.serviceLines);
                DrawConsumedSuppliesLines(estimate.consumedBySupply);
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField($"Invalidos ({invalidOptions.Count})", EditorStyles.boldLabel);
        if (invalidOptions.Count <= 0)
        {
            EditorGUILayout.LabelField("- nenhum", EditorStyles.wordWrappedMiniLabel);
        }
        else
        {
            for (int i = 0; i < invalidOptions.Count; i++)
            {
                ServicoDoComandoInvalidOption invalid = invalidOptions[i];
                if (invalid == null || invalid.targetUnit == null)
                    continue;

                CostEstimateBreakdown estimate = EstimateInvalidCost(invalid, match);
                int estimated = Mathf.Max(0, estimate.totalMoney);
                string reason = string.IsNullOrWhiteSpace(invalid.reason) ? "-" : invalid.reason;
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"{i + 1}. {invalid.targetUnit.name}");
                EditorGUILayout.LabelField("Custo estimado", $"${estimated}");
                EditorGUILayout.LabelField("Motivo", reason, EditorStyles.wordWrappedMiniLabel);
                DrawServiceLines(estimate.serviceLines);
                DrawConsumedSuppliesLines(estimate.consumedBySupply);
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Total estimado (elegiveis)", $"${Mathf.Max(0, totalEligibleCost)}", EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();
    }

    private static void DrawServiceLines(List<ServiceEstimateLine> serviceLines)
    {
        if (serviceLines == null || serviceLines.Count <= 0)
        {
            EditorGUILayout.LabelField("Servicos", "-");
            return;
        }

        EditorGUILayout.LabelField("Servicos");
        for (int i = 0; i < serviceLines.Count; i++)
        {
            ServiceEstimateLine line = serviceLines[i];
            if (line == null || string.IsNullOrWhiteSpace(line.summary))
                continue;

            EditorGUILayout.LabelField($"- {line.summary}", EditorStyles.wordWrappedMiniLabel);
            for (int j = 0; j < line.details.Count; j++)
            {
                string detail = line.details[j];
                if (string.IsNullOrWhiteSpace(detail))
                    continue;
                EditorGUILayout.LabelField($"  {detail}", EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    private static void DrawConsumedSuppliesLines(Dictionary<SupplyData, int> consumedBySupply)
    {
        if (consumedBySupply == null || consumedBySupply.Count <= 0)
            return;

        EditorGUILayout.LabelField("Consumo origem");
        foreach (KeyValuePair<SupplyData, int> pair in consumedBySupply)
        {
            if (pair.Key == null || pair.Value <= 0)
                continue;
            string supplyName = !string.IsNullOrWhiteSpace(pair.Key.displayName)
                ? pair.Key.displayName
                : (!string.IsNullOrWhiteSpace(pair.Key.id) ? pair.Key.id : pair.Key.name);
            EditorGUILayout.LabelField($"- {supplyName}: -{pair.Value}", EditorStyles.wordWrappedMiniLabel);
        }
    }

    private static CostEstimateBreakdown EstimateOptionCost(ServicoDoComandoOption option, MatchController match)
    {
        CostEstimateBreakdown breakdown = new CostEstimateBreakdown();
        if (option == null || option.targetUnit == null)
            return breakdown;

        IReadOnlyList<ServiceData> offered = option.sourceConstruction != null
            ? option.sourceConstruction.OfferedServices
            : option.sourceSupplierUnit != null ? option.sourceSupplierUnit.GetEmbarkedServices() : null;
        List<ServiceData> services = BuildDistinctServiceList(offered);
        Dictionary<SupplyData, int> sourceStock = option.sourceConstruction != null
            ? BuildConstructionStockSnapshot(option.sourceConstruction)
            : BuildSupplierStockSnapshot(option.sourceSupplierUnit);
        EstimateServicesCost(option.targetUnit, services, match, sourceStock, breakdown);
        return breakdown;
    }

    private static CostEstimateBreakdown EstimateInvalidCost(ServicoDoComandoInvalidOption invalid, MatchController match)
    {
        CostEstimateBreakdown breakdown = new CostEstimateBreakdown();
        if (invalid == null || invalid.targetUnit == null)
            return breakdown;

        IReadOnlyList<ServiceData> offered = invalid.sourceConstruction != null
            ? invalid.sourceConstruction.OfferedServices
            : invalid.sourceSupplierUnit != null ? invalid.sourceSupplierUnit.GetEmbarkedServices() : null;
        List<ServiceData> services = BuildDistinctServiceList(offered);
        Dictionary<SupplyData, int> sourceStock = invalid.sourceConstruction != null
            ? BuildConstructionStockSnapshot(invalid.sourceConstruction)
            : BuildSupplierStockSnapshot(invalid.sourceSupplierUnit);
        EstimateServicesCost(invalid.targetUnit, services, match, sourceStock, breakdown);
        return breakdown;
    }

    private static void EstimateServicesCost(
        UnitManager target,
        List<ServiceData> services,
        MatchController match,
        Dictionary<SupplyData, int> sourceStock,
        CostEstimateBreakdown breakdown)
    {
        if (target == null || services == null || services.Count <= 0 || breakdown == null)
            return;

        int hpMissing = Mathf.Max(0, target.GetMaxHP() - target.CurrentHP);
        if (hpMissing > 0)
            TryEstimateNeedService(services, target, hpMissing, forHp: true, forFuel: false, sourceStock, breakdown, match);

        int fuelMissing = Mathf.Max(0, target.GetMaxFuel() - target.CurrentFuel);
        if (fuelMissing > 0)
            TryEstimateNeedService(services, target, fuelMissing, forHp: false, forFuel: true, sourceStock, breakdown, match);

        if (IsAnyAmmoMissing(target))
            TryEstimateAmmoService(services, target, sourceStock, breakdown, match);
    }

    private static bool TryEstimateNeedService(
        List<ServiceData> services,
        UnitManager target,
        int pointsMissing,
        bool forHp,
        bool forFuel,
        Dictionary<SupplyData, int> sourceStock,
        CostEstimateBreakdown breakdown,
        MatchController match)
    {
        if (services == null || target == null || pointsMissing <= 0 || breakdown == null)
            return false;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return false;

        bool targetIsSupplier = targetData.isSupplier;
        ServiceData chosenService = null;

        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || !service.isService)
                continue;
            if (service.apenasEntreSupridores && !targetIsSupplier)
                continue;
            if (forHp && !service.recuperaHp)
                continue;
            if (forFuel && !service.recuperaAutonomia)
                continue;
            if (!TryResolveSupplyFromSnapshot(service, sourceStock, out _, out _))
                continue;

            chosenService = service;
            break;
        }

        if (chosenService == null)
            return false;

        Dictionary<SupplyData, int> consumedBySupply = new Dictionary<SupplyData, int>();
        ServiceLogisticsFormula.EstimatePotentialServiceGains(
            target,
            chosenService,
            sourceStock,
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

        int unitsConsumed = 0;
        SupplyData chosenSupply = null;
        foreach (KeyValuePair<SupplyData, int> pair in consumedBySupply)
        {
            int used = Mathf.Max(0, pair.Value);
            if (pair.Key == null || used <= 0)
                continue;
            unitsConsumed += used;
            if (chosenSupply == null)
                chosenSupply = pair.Key;
            AccumulateConsumedSupply(breakdown.consumedBySupply, pair.Key, used);
        }

        int baseCost = ComputeServiceMoneyCostEstimate(
            target,
            chosenService,
            forHp ? pointsServed : 0,
            forFuel ? pointsServed : 0,
            null,
            out _);
        int finalCost = match != null ? match.ResolveEconomyCost(baseCost) : Mathf.Max(0, baseCost);
        breakdown.totalMoney += Mathf.Max(0, finalCost);

        string serviceName = !string.IsNullOrWhiteSpace(chosenService.displayName) ? chosenService.displayName : chosenService.name;
        string supplyLabel = chosenSupply != null
            ? (!string.IsNullOrWhiteSpace(chosenSupply.displayName) ? chosenSupply.displayName : chosenSupply.name)
            : "-";
        string gainLabel = forFuel ? $"AUT +{pointsServed}" : $"HP +{pointsServed}";
        breakdown.serviceLines.Add(new ServiceEstimateLine
        {
            summary = $"{serviceName}: {gainLabel} | {supplyLabel} = -{Mathf.Max(0, unitsConsumed)} | ${Mathf.Max(0, finalCost)}"
        });
        return true;
    }

    private static bool TryEstimateAmmoService(
        List<ServiceData> services,
        UnitManager target,
        Dictionary<SupplyData, int> sourceStock,
        CostEstimateBreakdown breakdown,
        MatchController match)
    {
        if (services == null || target == null || breakdown == null)
            return false;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null || targetData.embarkedWeapons == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
        if (runtimeWeapons == null || runtimeWeapons.Count == 0 || targetData.embarkedWeapons.Count == 0)
            return false;

        bool targetIsSupplier = targetData.isSupplier;
        ServiceData chosenService = null;
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || !service.isService || !service.recuperaMunicao)
                continue;
            if (service.apenasEntreSupridores && !targetIsSupplier)
                continue;
            if (!TryResolveSupplyFromSnapshot(service, sourceStock, out _, out _))
                continue;

            chosenService = service;
            break;
        }

        if (chosenService == null)
            return false;

        int count = Mathf.Min(runtimeWeapons.Count, targetData.embarkedWeapons.Count);
        List<int> ammoByWeapon = new List<int>();
        Dictionary<SupplyData, int> consumedBySupply = new Dictionary<SupplyData, int>();
        ServiceLogisticsFormula.EstimatePotentialServiceGains(
            target,
            chosenService,
            sourceStock,
            out _,
            out _,
            out int totalRecoveredAmmo,
            ammoByWeapon: ammoByWeapon,
            allowHp: false,
            allowFuel: false,
            allowAmmo: true,
            consumedBySupply: consumedBySupply);

        int totalBoxesUsed = 0;
        SupplyData chosenSupply = null;
        List<int> ammoSuppliesByWeapon = new List<int>();
        for (int i = 0; i < ammoByWeapon.Count; i++)
        {
            int recovered = Mathf.Max(0, ammoByWeapon[i]);
            if (recovered <= 0)
                continue;
            int pointsPerSupply = 1;
            if (i < targetData.embarkedWeapons.Count)
            {
                UnitEmbarkedWeapon baseline = targetData.embarkedWeapons[i];
                if (baseline != null && baseline.weapon != null)
                    pointsPerSupply = Mathf.Max(1, ResolvePointsPerSupply(chosenService, ResolveWeaponClass(baseline.weapon)));
            }
            while (ammoSuppliesByWeapon.Count <= i)
                ammoSuppliesByWeapon.Add(0);
            ammoSuppliesByWeapon[i] = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
        }

        if (totalRecoveredAmmo <= 0)
            return false;

        foreach (KeyValuePair<SupplyData, int> pair in consumedBySupply)
        {
            int used = Mathf.Max(0, pair.Value);
            if (pair.Key == null || used <= 0)
                continue;
            if (chosenSupply == null)
                chosenSupply = pair.Key;
            totalBoxesUsed += used;
            AccumulateConsumedSupply(breakdown.consumedBySupply, pair.Key, used);
        }

        int baseCost = ComputeServiceMoneyCostEstimate(
            target,
            chosenService,
            0,
            0,
            ammoByWeapon,
            out List<int> ammoCostByWeapon);
        int finalCost = match != null ? match.ResolveEconomyCost(baseCost) : Mathf.Max(0, baseCost);
        breakdown.totalMoney += Mathf.Max(0, finalCost);

        string serviceName = !string.IsNullOrWhiteSpace(chosenService.displayName) ? chosenService.displayName : chosenService.name;
        string supplyLabel = chosenSupply != null
            ? (!string.IsNullOrWhiteSpace(chosenSupply.displayName) ? chosenSupply.displayName : chosenSupply.name)
            : "-";
        ServiceEstimateLine line = new ServiceEstimateLine
        {
            summary = $"{serviceName}: MUN +{totalRecoveredAmmo} | {supplyLabel} = -{Mathf.Max(0, totalBoxesUsed)} | ${Mathf.Max(0, finalCost)}"
        };
        AddAmmoWeaponDetailLines(line.details, ammoByWeapon, ammoSuppliesByWeapon, ammoCostByWeapon);
        breakdown.serviceLines.Add(line);
        return true;
    }

    private static void EstimatePotentialServiceGains(
        UnitManager target,
        ServiceData service,
        Dictionary<SupplyData, int> sourceStock,
        out int hpGain,
        out int fuelGain,
        out int ammoGain,
        out List<int> ammoByWeapon,
        out List<int> ammoSuppliesByWeapon,
        out SupplyData usedSupply,
        out int consumedSupplies)
    {
        ammoByWeapon = new List<int>();
        ammoSuppliesByWeapon = new List<int>();
        Dictionary<SupplyData, int> consumedBySupply = new Dictionary<SupplyData, int>();
        ServiceLogisticsFormula.EstimatePotentialServiceGains(
            target,
            service,
            sourceStock,
            out hpGain,
            out fuelGain,
            out ammoGain,
            ammoByWeapon: ammoByWeapon,
            consumedBySupply: consumedBySupply);

        usedSupply = null;
        consumedSupplies = 0;
        foreach (KeyValuePair<SupplyData, int> pair in consumedBySupply)
        {
            if (usedSupply == null && pair.Key != null && pair.Value > 0)
                usedSupply = pair.Key;
            consumedSupplies += Mathf.Max(0, pair.Value);
        }

        if (usedSupply != null && consumedSupplies > 0 && ammoByWeapon != null && ammoByWeapon.Count > 0)
        {
            for (int i = 0; i < ammoByWeapon.Count; i++)
            {
                int recovered = Mathf.Max(0, ammoByWeapon[i]);
                if (recovered <= 0)
                    continue;
                int pointsPerSupply = 1;
                if (target != null && target.TryGetUnitData(out UnitData targetData) && targetData != null && targetData.embarkedWeapons != null && i < targetData.embarkedWeapons.Count)
                {
                    UnitEmbarkedWeapon baseline = targetData.embarkedWeapons[i];
                    pointsPerSupply = baseline != null && baseline.weapon != null
                        ? Mathf.Max(1, ResolvePointsPerSupply(service, ResolveWeaponClass(baseline.weapon)))
                        : 1;
                }
                while (ammoSuppliesByWeapon.Count <= i)
                    ammoSuppliesByWeapon.Add(0);
                ammoSuppliesByWeapon[i] = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
            }
        }
    }

    private static void AccumulateConsumedSupply(Dictionary<SupplyData, int> map, SupplyData supply, int amount)
    {
        if (map == null || supply == null || amount <= 0)
            return;

        if (map.TryGetValue(supply, out int current))
            map[supply] = current + amount;
        else
            map.Add(supply, amount);
    }

    private static string BuildServiceGainLabel(ServiceData service, int hpGain, int fuelGain, List<int> ammoByWeapon)
    {
        List<string> chunks = new List<string>();
        if (service != null && service.recuperaHp && hpGain > 0)
            chunks.Add($"HP +{hpGain}");
        if (service != null && service.recuperaAutonomia && fuelGain > 0)
            chunks.Add($"AUT +{fuelGain}");
        if (service != null && service.recuperaMunicao && ammoByWeapon != null)
            chunks.Add($"MUN +{SumAmmoGain(ammoByWeapon)}");

        return chunks.Count > 0 ? string.Join(" | ", chunks) : "-";
    }

    private static int SumAmmoGain(List<int> ammoByWeapon)
    {
        if (ammoByWeapon == null || ammoByWeapon.Count <= 0)
            return 0;

        int total = 0;
        for (int i = 0; i < ammoByWeapon.Count; i++)
            total += Mathf.Max(0, ammoByWeapon[i]);
        return Mathf.Max(0, total);
    }

    private static void AddAmmoWeaponDetailLines(
        List<string> detailLines,
        List<int> ammoByWeapon,
        List<int> ammoSuppliesByWeapon,
        List<int> ammoCostByWeapon)
    {
        if (detailLines == null || ammoByWeapon == null)
            return;

        for (int i = 0; i < ammoByWeapon.Count; i++)
        {
            int recovered = Mathf.Max(0, ammoByWeapon[i]);
            if (recovered <= 0)
                continue;

            int supplySpent = ammoSuppliesByWeapon != null && i < ammoSuppliesByWeapon.Count
                ? Mathf.Max(0, ammoSuppliesByWeapon[i])
                : 0;
            int cost = ammoCostByWeapon != null && i < ammoCostByWeapon.Count
                ? Mathf.Max(0, ammoCostByWeapon[i])
                : 0;
            detailLines.Add($"{ResolveWeaponSlotLabel(i)}: MUN +{recovered} | caixas = -{supplySpent} | ${cost}");
        }
    }

    private static string ResolveWeaponSlotLabel(int weaponIndex)
    {
        if (weaponIndex <= 0)
            return "Primary";
        if (weaponIndex == 1)
            return "Secondary";
        return $"Weapon {weaponIndex + 1}";
    }

    private static Dictionary<SupplyData, int> BuildConstructionStockSnapshot(ConstructionManager sourceConstruction)
    {
        Dictionary<SupplyData, int> map = new Dictionary<SupplyData, int>();
        if (sourceConstruction == null)
            return map;

        IReadOnlyList<ConstructionSupplyOffer> offers = sourceConstruction.OfferedSupplies;
        if (offers == null)
            return map;

        for (int i = 0; i < offers.Count; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer == null || offer.supply == null)
                continue;
            int current = map.TryGetValue(offer.supply, out int existing) ? existing : 0;
            int add = sourceConstruction.HasInfiniteSuppliesFor(offer.supply) || offer.quantity >= int.MaxValue
                ? int.MaxValue
                : Mathf.Max(0, offer.quantity);
            if (current == int.MaxValue || add == int.MaxValue)
            {
                map[offer.supply] = int.MaxValue;
            }
            else
            {
                long sum = (long)current + add;
                map[offer.supply] = sum >= int.MaxValue ? int.MaxValue : (int)sum;
            }
        }

        return map;
    }

    private static Dictionary<SupplyData, int> BuildSupplierStockSnapshot(UnitManager supplier)
    {
        Dictionary<SupplyData, int> map = new Dictionary<SupplyData, int>();
        if (supplier == null)
            return map;

        IReadOnlyList<UnitEmbarkedSupply> resources = supplier.GetEmbarkedResources();
        if (resources == null)
            return map;

        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || entry.supply == null)
                continue;
            int current = map.TryGetValue(entry.supply, out int existing) ? existing : 0;
            long sum = (long)current + Mathf.Max(0, entry.amount);
            map[entry.supply] = sum >= int.MaxValue ? int.MaxValue : (int)sum;
        }

        return map;
    }

    private static bool TryResolveSupplyFromSnapshot(ServiceData service, Dictionary<SupplyData, int> stockBySupply, out SupplyData supply, out int amount)
    {
        supply = null;
        amount = 0;
        if (service == null || stockBySupply == null || service.suppliesUsed == null)
            return false;

        for (int i = 0; i < service.suppliesUsed.Count; i++)
        {
            SupplyData candidate = service.suppliesUsed[i];
            if (candidate == null)
                continue;
            int current = ReadStockAmount(stockBySupply, candidate);
            if (current <= 0)
                continue;
            supply = candidate;
            amount = current;
            return true;
        }

        return false;
    }

    private static int ReadStockAmount(Dictionary<SupplyData, int> stockBySupply, SupplyData supply)
    {
        if (stockBySupply == null || supply == null)
            return 0;
        return stockBySupply.TryGetValue(supply, out int current) ? current : 0;
    }

    private static int ConsumeFromSnapshot(Dictionary<SupplyData, int> stockBySupply, SupplyData supply, int amount)
    {
        if (stockBySupply == null || supply == null || amount <= 0)
            return 0;
        if (!stockBySupply.TryGetValue(supply, out int current) || current <= 0)
            return 0;
        if (current == int.MaxValue)
            return amount;

        int spent = Mathf.Min(current, amount);
        stockBySupply[supply] = Mathf.Max(0, current - spent);
        return spent;
    }

    private static int ComputeMissingAmmoPoints(UnitManager target)
    {
        if (target == null)
            return 0;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null || targetData.embarkedWeapons == null)
            return 0;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
        int totalMissing = 0;
        for (int i = 0; i < targetData.embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon baseline = targetData.embarkedWeapons[i];
            if (baseline == null)
                continue;
            int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
            int currentAmmo = runtimeWeapons != null && i < runtimeWeapons.Count && runtimeWeapons[i] != null
                ? Mathf.Max(0, runtimeWeapons[i].squadAmmunition)
                : 0;
            totalMissing += Mathf.Max(0, maxAmmo - currentAmmo);
        }

        return Mathf.Max(0, totalMissing);
    }

    private static bool IsAnyAmmoMissing(UnitManager target)
    {
        return ComputeMissingAmmoPoints(target) > 0;
    }

    private static int ComputeServiceMoneyCostEstimate(
        UnitManager target,
        ServiceData service,
        int hpGain,
        int fuelGain,
        List<int> ammoByWeapon,
        out List<int> ammoCostByWeapon)
    {
        ammoCostByWeapon = new List<int>();
        return ServiceCostFormula.ComputeServiceMoneyCost(
            target,
            service,
            hpGain,
            fuelGain,
            SumAmmoGain(ammoByWeapon),
            ammoByWeapon,
            ammoCostByWeapon);
    }

    private void AutoDetectContext()
    {
        if (terrainDatabase == null)
            terrainDatabase = FindFirstAssetEditor<TerrainDatabase>();
        if (overrideTilemap == null)
            overrideTilemap = FindAnyObjectByType<CursorController>() != null ? FindAnyObjectByType<CursorController>().BoardTilemap : null;

        status = "Contexto atualizado.";
        RunSimulation();
    }

    private void RunSimulation()
    {
        Tilemap map = ResolveTilemap();
        TeamId team = ResolveTargetTeam();
        bool canRun = ServicoDoComandoSensor.CollectOptions(
            team,
            map,
            terrainDatabase,
            eligibleOptions,
            out sensorReason,
            invalidOptions);

        status = canRun
            ? $"Simulacao OK: elegiveis={eligibleOptions.Count}, invalidos={invalidOptions.Count}."
            : $"Sem ordem valida. {sensorReason}";
        Repaint();
    }

    private void ExecuteDebugOrder()
    {
        if (Application.isPlaying)
        {
            TurnStateManager turnState = FindAnyObjectByType<TurnStateManager>();
            if (turnState != null)
            {
                bool started = turnState.TryStartCommandServiceOrder(out string runtimeMessage);
                status = !string.IsNullOrWhiteSpace(runtimeMessage)
                    ? runtimeMessage
                    : (started ? "Servico do Comando iniciado no runtime." : "Servico do Comando nao iniciou.");
                Debug.Log($"[ServicoComandoDebug] {status}");
                RunSimulation();
                return;
            }
        }

        int servedTargets = 0;
        int recoveredHp = 0;
        int recoveredFuel = 0;
        int recoveredAmmo = 0;

        for (int i = 0; i < eligibleOptions.Count; i++)
        {
            ServicoDoComandoOption option = eligibleOptions[i];
            if (option == null || option.targetUnit == null)
                continue;
            if (option.targetUnit.ReceivedSuppliesThisTurn)
                continue;

            bool fromConstruction = option.sourceConstruction != null;
            bool fromSupplierUnit = option.sourceSupplierUnit != null;
            if (!fromConstruction && !fromSupplierUnit)
                continue;

            if (option.forceSurfaceBeforeSupply)
                option.targetUnit.TrySetCurrentLayerMode(Domain.Naval, HeightLevel.Surface);
            if (option.forceLandBeforeSupply || option.forceTakeoffBeforeSupply)
                option.targetUnit.TrySetCurrentLayerMode(option.plannedServiceDomain, option.plannedServiceHeight);

            IReadOnlyList<ServiceData> offeredServices = fromConstruction
                ? option.sourceConstruction.OfferedServices
                : option.sourceSupplierUnit.GetEmbarkedServices();
            List<ServiceData> services = BuildDistinctServiceList(offeredServices);
            bool changed = fromConstruction
                ? ApplyConstructionServicesToTarget(option.sourceConstruction, option.targetUnit, services, out int hp, out int fuel, out int ammo)
                : ApplyUnitServicesToTarget(option.sourceSupplierUnit, option.targetUnit, services, out hp, out fuel, out ammo);
            if (!changed)
                continue;

            servedTargets++;
            recoveredHp += hp;
            recoveredFuel += fuel;
            recoveredAmmo += ammo;
        }

        status = servedTargets > 0
            ? $"Ordem executada: alvos={servedTargets} | HP +{recoveredHp} | autonomia +{recoveredFuel} | municao +{recoveredAmmo}"
            : "Ordem executada: nenhum alvo recebeu servico.";
        Debug.Log($"[ServicoComandoDebug] {status}");
        RunSimulation();
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;

        CursorController cursor = FindAnyObjectByType<CursorController>();
        if (cursor != null && cursor.BoardTilemap != null)
            return cursor.BoardTilemap;

        return FindAnyObjectByType<Tilemap>();
    }

    private TeamId ResolveTargetTeam()
    {
        if (!useActiveTeamFromMatch)
            return teamOverride;

        MatchController match = FindAnyObjectByType<MatchController>();
        if (match == null)
            return teamOverride;

        int activeId = match.ActiveTeamId;
        if (activeId < -1 || activeId > 3)
            return teamOverride;
        return (TeamId)activeId;
    }

    private static List<ServiceData> BuildDistinctServiceList(IReadOnlyList<ServiceData> services)
    {
        var list = new List<ServiceData>();
        if (services == null)
            return list;

        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || list.Contains(service))
                continue;
            list.Add(service);
        }

        return list;
    }

    private static bool ApplyConstructionServicesToTarget(
        ConstructionManager sourceConstruction,
        UnitManager target,
        List<ServiceData> services,
        out int hpRecovered,
        out int fuelRecovered,
        out int ammoRecovered)
    {
        hpRecovered = 0;
        fuelRecovered = 0;
        ammoRecovered = 0;
        if (sourceConstruction == null || target == null || services == null || services.Count <= 0)
            return false;

        bool any = false;
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || !service.isService)
                continue;

            if (service.apenasEntreSupridores && !IsSupplier(target))
                continue;

            if (service.recuperaHp)
            {
                int applied = ApplyHpService(sourceConstruction, target, service);
                if (applied > 0)
                {
                    hpRecovered += applied;
                    any = true;
                }
            }

            if (service.recuperaAutonomia)
            {
                int applied = ApplyFuelService(sourceConstruction, target, service);
                if (applied > 0)
                {
                    fuelRecovered += applied;
                    any = true;
                }
            }

            if (service.recuperaMunicao)
            {
                int applied = ApplyAmmoService(sourceConstruction, target, service);
                if (applied > 0)
                {
                    ammoRecovered += applied;
                    any = true;
                }
            }
        }

        if (any)
            target.MarkReceivedSuppliesThisTurn();

        return any;
    }

    private static bool ApplyUnitServicesToTarget(
        UnitManager supplier,
        UnitManager target,
        List<ServiceData> services,
        out int hpRecovered,
        out int fuelRecovered,
        out int ammoRecovered)
    {
        hpRecovered = 0;
        fuelRecovered = 0;
        ammoRecovered = 0;
        if (supplier == null || target == null || services == null || services.Count <= 0)
            return false;

        bool any = false;
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service == null || !service.isService)
                continue;

            if (service.apenasEntreSupridores && !IsSupplier(target))
                continue;

            if (service.recuperaHp)
            {
                int applied = ApplyHpService(supplier, target, service);
                if (applied > 0)
                {
                    hpRecovered += applied;
                    any = true;
                }
            }

            if (service.recuperaAutonomia)
            {
                int applied = ApplyFuelService(supplier, target, service);
                if (applied > 0)
                {
                    fuelRecovered += applied;
                    any = true;
                }
            }

            if (service.recuperaMunicao)
            {
                int applied = ApplyAmmoService(supplier, target, service);
                if (applied > 0)
                {
                    ammoRecovered += applied;
                    any = true;
                }
            }
        }

        if (any)
            target.MarkReceivedSuppliesThisTurn();

        return any;
    }

    private static int ApplyHpService(ConstructionManager sourceConstruction, UnitManager target, ServiceData service)
    {
        int missing = Mathf.Max(0, target.GetMaxHP() - target.CurrentHP);
        if (missing <= 0)
            return 0;

        int cap = service.serviceLimitPerUnitPerTurn > 0
            ? Mathf.Min(missing, service.serviceLimitPerUnitPerTurn)
            : missing;
        if (cap <= 0)
            return 0;

        int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
        if (pointsPerSupply <= 0)
            return 0;

        if (!TryResolveSupplyForService(sourceConstruction, service, out SupplyData supply, out int stock))
            return 0;

        int maxByStock = stock * pointsPerSupply;
        int recovered = Mathf.Min(cap, maxByStock);
        if (recovered <= 0)
            return 0;

        int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
        if (!TryConsumeSupplyFromConstruction(sourceConstruction, supply, supplies))
            return 0;

        target.SetCurrentHP(target.CurrentHP + recovered);
        return recovered;
    }

    private static int ApplyHpService(UnitManager supplier, UnitManager target, ServiceData service)
    {
        int missing = Mathf.Max(0, target.GetMaxHP() - target.CurrentHP);
        if (missing <= 0)
            return 0;

        int cap = service.serviceLimitPerUnitPerTurn > 0
            ? Mathf.Min(missing, service.serviceLimitPerUnitPerTurn)
            : missing;
        if (cap <= 0)
            return 0;

        int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
        if (pointsPerSupply <= 0)
            return 0;

        if (!TryResolveSupplyForService(supplier, service, out SupplyData supply, out int stock))
            return 0;

        int maxByStock = stock * pointsPerSupply;
        int recovered = Mathf.Min(cap, maxByStock);
        if (recovered <= 0)
            return 0;

        int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
        if (!TryConsumeSupplyFromSupplier(supplier, supply, supplies))
            return 0;

        target.SetCurrentHP(target.CurrentHP + recovered);
        return recovered;
    }

    private static int ApplyFuelService(ConstructionManager sourceConstruction, UnitManager target, ServiceData service)
    {
        int missing = Mathf.Max(0, target.MaxFuel - target.CurrentFuel);
        if (missing <= 0)
            return 0;

        int cap = service.serviceLimitPerUnitPerTurn > 0
            ? Mathf.Min(missing, service.serviceLimitPerUnitPerTurn)
            : missing;
        if (cap <= 0)
            return 0;

        int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
        if (pointsPerSupply <= 0)
            return 0;

        if (!TryResolveSupplyForService(sourceConstruction, service, out SupplyData supply, out int stock))
            return 0;

        int maxByStock = stock * pointsPerSupply;
        int recovered = Mathf.Min(cap, maxByStock);
        if (recovered <= 0)
            return 0;

        int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
        if (!TryConsumeSupplyFromConstruction(sourceConstruction, supply, supplies))
            return 0;

        target.SetCurrentFuel(target.CurrentFuel + recovered);
        return recovered;
    }

    private static int ApplyFuelService(UnitManager supplier, UnitManager target, ServiceData service)
    {
        int missing = Mathf.Max(0, target.MaxFuel - target.CurrentFuel);
        if (missing <= 0)
            return 0;

        int cap = service.serviceLimitPerUnitPerTurn > 0
            ? Mathf.Min(missing, service.serviceLimitPerUnitPerTurn)
            : missing;
        if (cap <= 0)
            return 0;

        int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
        if (pointsPerSupply <= 0)
            return 0;

        if (!TryResolveSupplyForService(supplier, service, out SupplyData supply, out int stock))
            return 0;

        int maxByStock = stock * pointsPerSupply;
        int recovered = Mathf.Min(cap, maxByStock);
        if (recovered <= 0)
            return 0;

        int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
        if (!TryConsumeSupplyFromSupplier(supplier, supply, supplies))
            return 0;

        target.SetCurrentFuel(target.CurrentFuel + recovered);
        return recovered;
    }

    private static int ApplyAmmoService(ConstructionManager sourceConstruction, UnitManager target, ServiceData service)
    {
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return 0;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
        List<UnitEmbarkedWeapon> baselineWeapons = targetData.embarkedWeapons;
        if (runtimeWeapons == null || baselineWeapons == null)
            return 0;

        int count = Mathf.Min(runtimeWeapons.Count, baselineWeapons.Count);
        if (count <= 0)
            return 0;

        int serviceBudget = service.serviceLimitPerUnitPerTurn > 0
            ? service.serviceLimitPerUnitPerTurn
            : int.MaxValue;
        int recoveredTotal = 0;

        for (int i = 0; i < count && serviceBudget > 0; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = baselineWeapons[i];
            if (runtime == null || baseline == null || baseline.weapon == null)
                continue;

            int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
            int missing = Mathf.Max(0, maxAmmo - runtime.squadAmmunition);
            if (missing <= 0)
                continue;

            int cap = Mathf.Min(missing, serviceBudget);
            if (cap <= 0)
                continue;

            int pointsPerSupply = ResolvePointsPerSupply(service, ResolveWeaponClass(baseline.weapon));
            if (pointsPerSupply <= 0)
                continue;

            if (!TryResolveSupplyForService(sourceConstruction, service, out SupplyData supply, out int stock))
                break;

            int maxByStock = stock * pointsPerSupply;
            int recovered = Mathf.Min(cap, maxByStock);
            if (recovered <= 0)
                continue;

            int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
            if (!TryConsumeSupplyFromConstruction(sourceConstruction, supply, supplies))
                continue;

            runtime.squadAmmunition = Mathf.Clamp(runtime.squadAmmunition + recovered, 0, maxAmmo);
            recoveredTotal += recovered;
            serviceBudget -= recovered;
        }

        return recoveredTotal;
    }

    private static int ApplyAmmoService(UnitManager supplier, UnitManager target, ServiceData service)
    {
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return 0;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
        List<UnitEmbarkedWeapon> baselineWeapons = targetData.embarkedWeapons;
        if (runtimeWeapons == null || baselineWeapons == null)
            return 0;

        int count = Mathf.Min(runtimeWeapons.Count, baselineWeapons.Count);
        if (count <= 0)
            return 0;

        int serviceBudget = service.serviceLimitPerUnitPerTurn > 0
            ? service.serviceLimitPerUnitPerTurn
            : int.MaxValue;
        int recoveredTotal = 0;

        for (int i = 0; i < count && serviceBudget > 0; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = baselineWeapons[i];
            if (runtime == null || baseline == null || baseline.weapon == null)
                continue;

            int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
            int missing = Mathf.Max(0, maxAmmo - runtime.squadAmmunition);
            if (missing <= 0)
                continue;

            int cap = Mathf.Min(missing, serviceBudget);
            if (cap <= 0)
                continue;

            int pointsPerSupply = ResolvePointsPerSupply(service, ResolveWeaponClass(baseline.weapon));
            if (pointsPerSupply <= 0)
                continue;

            if (!TryResolveSupplyForService(supplier, service, out SupplyData supply, out int stock))
                break;

            int maxByStock = stock * pointsPerSupply;
            int recovered = Mathf.Min(cap, maxByStock);
            if (recovered <= 0)
                continue;

            int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
            if (!TryConsumeSupplyFromSupplier(supplier, supply, supplies))
                continue;

            runtime.squadAmmunition = Mathf.Clamp(runtime.squadAmmunition + recovered, 0, maxAmmo);
            recoveredTotal += recovered;
            serviceBudget -= recovered;
        }

        return recoveredTotal;
    }

    private static bool TryResolveSupplyForService(ConstructionManager sourceConstruction, ServiceData service, out SupplyData supply, out int stockAmount)
    {
        supply = null;
        stockAmount = 0;
        if (sourceConstruction == null || service == null)
            return false;
        if (service.suppliesUsed == null || service.suppliesUsed.Count <= 0)
            return false;

        for (int i = 0; i < service.suppliesUsed.Count; i++)
        {
            SupplyData candidate = service.suppliesUsed[i];
            if (candidate == null)
                continue;

            int amount = GetConstructionSupplyAmount(sourceConstruction, candidate);
            if (amount <= 0)
                continue;

            supply = candidate;
            stockAmount = amount;
            return true;
        }

        return false;
    }

    private static bool TryResolveSupplyForService(UnitManager supplier, ServiceData service, out SupplyData supply, out int stockAmount)
    {
        supply = null;
        stockAmount = 0;
        if (supplier == null || service == null)
            return false;
        if (service.suppliesUsed == null || service.suppliesUsed.Count <= 0)
            return false;

        for (int i = 0; i < service.suppliesUsed.Count; i++)
        {
            SupplyData candidate = service.suppliesUsed[i];
            if (candidate == null)
                continue;

            int amount = GetSupplierSupplyAmount(supplier, candidate);
            if (amount <= 0)
                continue;

            supply = candidate;
            stockAmount = amount;
            return true;
        }

        return false;
    }

    private static int GetConstructionSupplyAmount(ConstructionManager sourceConstruction, SupplyData supply)
    {
        if (sourceConstruction == null || supply == null)
            return 0;
        if (sourceConstruction.HasInfiniteSuppliesFor(supply))
            return int.MaxValue;

        IReadOnlyList<ConstructionSupplyOffer> offers = sourceConstruction.OfferedSupplies;
        if (offers == null)
            return 0;

        int total = 0;
        for (int i = 0; i < offers.Count; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer == null || offer.supply != supply)
                continue;
            total += Mathf.Max(0, offer.quantity);
            if (total >= int.MaxValue)
                return int.MaxValue;
        }

        return total;
    }

    private static int GetSupplierSupplyAmount(UnitManager supplier, SupplyData supply)
    {
        if (supplier == null || supply == null)
            return 0;

        IReadOnlyList<UnitEmbarkedSupply> resources = supplier.GetEmbarkedResources();
        if (resources == null)
            return 0;

        int total = 0;
        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || entry.supply != supply)
                continue;
            total += Mathf.Max(0, entry.amount);
        }

        return total;
    }

    private static bool TryConsumeSupplyFromConstruction(ConstructionManager sourceConstruction, SupplyData supply, int amount)
    {
        if (sourceConstruction == null || supply == null || amount <= 0)
            return false;
        if (sourceConstruction.HasInfiniteSuppliesFor(supply))
            return sourceConstruction.ContainsOfferedSupply(supply);

        IReadOnlyList<ConstructionSupplyOffer> offers = sourceConstruction.OfferedSupplies;
        if (offers == null)
            return false;

        int remaining = amount;
        for (int i = 0; i < offers.Count && remaining > 0; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer == null || offer.supply != supply || offer.quantity <= 0)
                continue;

            if (offer.quantity >= int.MaxValue)
                return true;

            int spent = Mathf.Min(offer.quantity, remaining);
            offer.quantity -= spent;
            remaining -= spent;
        }

        return remaining <= 0;
    }

    private static bool TryConsumeSupplyFromSupplier(UnitManager supplier, SupplyData supply, int amount)
    {
        if (supplier == null || supply == null || amount <= 0)
            return false;

        IReadOnlyList<UnitEmbarkedSupply> resources = supplier.GetEmbarkedResources();
        if (resources == null)
            return false;

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

        return remaining <= 0;
    }

    private static int ResolvePointsPerSupply(ServiceData service, ArmorWeaponClass targetClass)
    {
        if (service == null || service.serviceEfficiency == null)
            return 0;

        for (int i = 0; i < service.serviceEfficiency.Count; i++)
        {
            ServiceEfficiencyByClass entry = service.serviceEfficiency[i];
            if (entry == null || entry.armorWeaponClass != targetClass)
                continue;

            int points = Mathf.RoundToInt(entry.value);
            return Mathf.Max(0, points);
        }

        return 0;
    }

    private static ArmorWeaponClass ResolveArmorClass(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return (ArmorWeaponClass)data.ArmorClass;
        return ArmorWeaponClass.Light;
    }

    private static ArmorWeaponClass ResolveWeaponClass(WeaponData weapon)
    {
        if (weapon == null)
            return ArmorWeaponClass.Light;
        switch (weapon.WeaponClass)
        {
            case WeaponClass.Heavy:
                return ArmorWeaponClass.Heavy;
            case WeaponClass.Medium:
                return ArmorWeaponClass.Medium;
            default:
                return ArmorWeaponClass.Light;
        }
    }

    private static bool IsSupplier(UnitManager unit)
    {
        return unit != null && unit.TryGetUnitData(out UnitData data) && data != null && data.isSupplier;
    }

    private static string ResolveConstructionLabel(ConstructionManager construction)
    {
        if (construction == null)
            return "(construcao)";
        if (!string.IsNullOrWhiteSpace(construction.ConstructionDisplayName))
            return construction.ConstructionDisplayName;
        if (!string.IsNullOrWhiteSpace(construction.ConstructionId))
            return construction.ConstructionId;
        return construction.name;
    }

    private static string ResolveSupplierUnitLabel(UnitManager supplier)
    {
        if (supplier == null)
            return "(supridor)";
        if (!string.IsNullOrWhiteSpace(supplier.UnitDisplayName))
            return supplier.UnitDisplayName;
        if (!string.IsNullOrWhiteSpace(supplier.UnitId))
            return supplier.UnitId;
        return supplier.name;
    }

    private static T FindFirstAssetEditor<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
        }

        return null;
    }
}
