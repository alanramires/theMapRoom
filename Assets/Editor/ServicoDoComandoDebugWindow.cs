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
        DrawEligibleSection();
        EditorGUILayout.Space(8f);
        DrawInvalidSection();

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
