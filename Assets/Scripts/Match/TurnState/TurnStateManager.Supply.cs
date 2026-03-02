using System.Collections.Generic;
using UnityEngine;

public partial class TurnStateManager
{
    private void HandleSupplyActionRequested()
    {
        bool canSupply = availableSensorActionCodes.Contains('S');
        if (!canSupply || cachedPodeSuprirTargets.Count == 0)
        {
            string reason = string.IsNullOrWhiteSpace(cachedPodeSuprirReason)
                ? "sem candidatos validos agora."
                : cachedPodeSuprirReason;
            Debug.Log($"Pode Suprir (\"S\"): {reason}");
            LogScannerPanel();
            return;
        }
        EnterSupplyStateFromSensors();
    }

    private static bool ApplyServicesToTarget(
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
        if (supplier == null || target == null || services == null || services.Count == 0)
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

    private static int ApplyHpService(UnitManager supplier, UnitManager target, ServiceData service)
    {
        string supplierName = supplier != null ? supplier.name : "(supplier-null)";
        string targetName = target != null ? target.name : "(target-null)";
        string serviceLabel = ResolveServiceLabel(service);

        int missing = Mathf.Max(0, target.GetMaxHP() - target.CurrentHP);
        if (missing <= 0)
        {
            Debug.Log($"[HpRepair] modo=Suprimento | alvo={targetName} | fornecedor={supplierName} | servico={serviceLabel} | sem reparo (HP cheio: {target.CurrentHP}/{target.GetMaxHP()})");
            return 0;
        }

        int cap = service.serviceLimitPerUnitPerTurn > 0
            ? Mathf.Min(missing, service.serviceLimitPerUnitPerTurn)
            : missing;
        if (cap <= 0)
        {
            Debug.Log($"[HpRepair] modo=Suprimento | alvo={targetName} | fornecedor={supplierName} | servico={serviceLabel} | sem reparo (cap=0, limite por turno={service.serviceLimitPerUnitPerTurn})");
            return 0;
        }

        int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
        if (pointsPerSupply <= 0)
        {
            Debug.Log($"[HpRepair] modo=Suprimento | alvo={targetName} | fornecedor={supplierName} | servico={serviceLabel} | sem reparo (eficiencia HP <= 0 para classe)");
            return 0;
        }

        if (!TryResolveSupplyForService(supplier, service, out SupplyData supply, out int stock))
        {
            Debug.Log($"[HpRepair] modo=Suprimento | alvo={targetName} | fornecedor={supplierName} | servico={serviceLabel} | sem reparo (sem estoque de suprimento para o servico)");
            return 0;
        }

        int maxByStock = SafeMultiplyToIntMax(stock, pointsPerSupply);
        int recovered = Mathf.Min(cap, maxByStock);
        if (recovered <= 0)
        {
            Debug.Log($"[HpRepair] modo=Suprimento | alvo={targetName} | fornecedor={supplierName} | servico={serviceLabel} | sem reparo (recuperacao calculada=0; stock={stock}, pontosPorSup={pointsPerSupply}, maxByStock={maxByStock})");
            return 0;
        }

        int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
        if (!TryConsumeSupplyFromSupplier(supplier, supply, supplies))
        {
            Debug.Log($"[HpRepair] modo=Suprimento | alvo={targetName} | fornecedor={supplierName} | servico={serviceLabel} | sem reparo (falha ao consumir suprimento; qtdNec={supplies})");
            return 0;
        }

        int beforeHp = target.CurrentHP;
        target.SetCurrentHP(target.CurrentHP + recovered);
        int afterHp = target.CurrentHP;
        int actualGain = Mathf.Max(0, afterHp - beforeHp);
        Debug.Log($"[HpRepair] modo=Suprimento | alvo={targetName} | fornecedor={supplierName} | servico={serviceLabel} | {beforeHp}->{afterHp} (+{actualGain})");
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

        int maxByStock = SafeMultiplyToIntMax(stock, pointsPerSupply);
        int recovered = Mathf.Min(cap, maxByStock);
        if (recovered <= 0)
            return 0;

        int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
        if (!TryConsumeSupplyFromSupplier(supplier, supply, supplies))
            return 0;

        target.SetCurrentFuel(target.CurrentFuel + recovered);
        return recovered;
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
        bool hasMissingAmmo = false;
        bool hasPositiveEfficiency = false;
        bool hasStockForAmmo = false;
        bool consumeFailed = false;
        string supplierName = supplier != null ? supplier.name : "(supplier-null)";
        string targetName = target != null ? target.name : "(target-null)";
        string serviceLabel = ResolveServiceLabel(service);

        for (int i = 0; i < count && serviceBudget > 0; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = baselineWeapons[i];
            if (runtime == null || baseline == null || baseline.weapon == null)
                continue;

            int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
            int beforeAmmo = Mathf.Max(0, runtime.squadAmmunition);
            int missing = Mathf.Max(0, maxAmmo - beforeAmmo);
            if (missing <= 0)
                continue;
            hasMissingAmmo = true;

            int cap = Mathf.Min(missing, serviceBudget);
            if (cap <= 0)
                continue;

            int pointsPerSupply = ResolvePointsPerSupply(service, ResolveWeaponClass(baseline.weapon));
            if (pointsPerSupply <= 0)
                continue;
            hasPositiveEfficiency = true;

            if (!TryResolveSupplyForService(supplier, service, out SupplyData supply, out int stock))
                break;
            hasStockForAmmo = true;

            int maxByStock = SafeMultiplyToIntMax(stock, pointsPerSupply);
            int recovered = Mathf.Min(cap, maxByStock);
            if (recovered <= 0)
                continue;

            int supplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
            if (!TryConsumeSupplyFromSupplier(supplier, supply, supplies))
            {
                consumeFailed = true;
                continue;
            }

            runtime.squadAmmunition = Mathf.Clamp(beforeAmmo + recovered, 0, maxAmmo);
            int afterAmmo = runtime.squadAmmunition;
            int actualGain = Mathf.Max(0, afterAmmo - beforeAmmo);
            if (actualGain > 0)
            {
                string weaponLabel = ResolveWeaponLabel(baseline.weapon);
                Debug.Log($"[AmmoGain] modo=Suprimento | alvo={targetName} | fornecedor={supplierName} | servico={serviceLabel} | arma={weaponLabel} | {beforeAmmo}->{afterAmmo} (+{actualGain})");
            }
            recoveredTotal += recovered;
            serviceBudget -= recovered;
        }

        if (recoveredTotal <= 0)
        {
            string reason = !hasMissingAmmo
                ? "todas as armas ja estao com municao cheia"
                : !hasPositiveEfficiency
                    ? "eficiencia de municao <= 0 para as classes das armas com falta"
                    : !hasStockForAmmo
                        ? "sem estoque de suprimento para municao"
                        : consumeFailed
                            ? "falha ao consumir suprimento de municao"
                            : "sem ganho calculado por limites/cap";
            Debug.Log($"[AmmoGain] modo=Suprimento | alvo={targetName} | fornecedor={supplierName} | servico={serviceLabel} | sem rearm ({reason})");
        }

        return recoveredTotal;
    }

    private static bool TryResolveSupplyForService(UnitManager supplier, ServiceData service, out SupplyData supply, out int stockAmount)
    {
        supply = null;
        stockAmount = 0;
        if (supplier == null || service == null)
            return false;

        if (service.suppliesUsed == null || service.suppliesUsed.Count == 0)
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

        if (remaining > 0)
            return false;

        return true;
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

    private static string ResolveWeaponLabel(WeaponData weapon)
    {
        if (weapon == null)
            return "(arma)";
        if (!string.IsNullOrWhiteSpace(weapon.displayName))
            return weapon.displayName;
        if (!string.IsNullOrWhiteSpace(weapon.id))
            return weapon.id;
        return weapon.name;
    }

    private static int SafeMultiplyToIntMax(int a, int b)
    {
        if (a <= 0 || b <= 0)
            return 0;

        long product = (long)a * b;
        if (product >= int.MaxValue)
            return int.MaxValue;
        return (int)product;
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

    private static bool IsSupplier(UnitManager unit)
    {
        return unit != null && unit.TryGetUnitData(out UnitData data) && data != null && data.isSupplier;
    }

    private bool TryPayServiceCostForExecution(
        TeamId team,
        UnitManager target,
        ServiceData service,
        int hpGain,
        int fuelGain,
        int ammoGain,
        string contextLabel,
        out int chargedCost)
    {
        chargedCost = 0;
        if (target == null || service == null)
            return true;
        if (matchController == null)
            return true;

        int baseCost = ComputeServiceMoneyCost(target, service, hpGain, fuelGain, ammoGain);
        int finalCost = matchController.ResolveEconomyCost(baseCost);
        if (finalCost <= 0)
            return true;

        if (matchController.TrySpendActualMoney(team, finalCost, out int remaining))
        {
            chargedCost = finalCost;
            return true;
        }

        cursorController?.PlayErrorSfx();
        Debug.LogError($"[{contextLabel}] Economia insuficiente: custo=${finalCost}, saldo=${Mathf.Max(0, remaining)}. Servico cancelado.");
        return false;
    }

    private static int ComputeServiceMoneyCost(UnitManager target, ServiceData service, int hpGain, int fuelGain, int ammoGain)
    {
        if (target == null || service == null)
            return 0;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return 0;

        int squadCost = Mathf.Max(0, targetData.cost);
        if (squadCost <= 0)
            return 0;

        float share = Mathf.Clamp(service.percentCost, 0, 100) / 100f;
        float allocated = squadCost * share;
        if (allocated <= 0f)
            return 0;

        int totalCost = 0;
        if (hpGain > 0)
        {
            int maxHp = Mathf.Max(1, targetData.maxHP);
            float costPerPoint = allocated / maxHp;
            totalCost += Mathf.RoundToInt(hpGain * costPerPoint);
        }

        if (fuelGain > 0)
        {
            int maxFuel = Mathf.Max(1, target.GetMaxFuel());
            float costPerPoint = allocated / maxFuel;
            totalCost += Mathf.RoundToInt(fuelGain * costPerPoint);
        }

        if (ammoGain > 0)
        {
            int totalWeaponCapacity = ComputeTotalWeaponPointCapacity(targetData);
            if (totalWeaponCapacity > 0)
            {
                float costPerPoint = allocated / totalWeaponCapacity;
                totalCost += Mathf.RoundToInt(ammoGain * costPerPoint);
            }
        }

        return Mathf.Max(0, totalCost);
    }

    private static int ComputeTotalWeaponPointCapacity(UnitData targetData)
    {
        if (targetData == null || targetData.embarkedWeapons == null)
            return 0;

        int total = 0;
        for (int i = 0; i < targetData.embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon weapon = targetData.embarkedWeapons[i];
            if (weapon == null)
                continue;
            total += Mathf.Max(0, weapon.squadAmmunition);
        }

        return Mathf.Max(0, total);
    }

    private static void EstimateServiceGainsFromSupplier(
        UnitManager supplier,
        UnitManager target,
        ServiceData service,
        out int hpGain,
        out int fuelGain,
        out int ammoGain)
    {
        hpGain = EstimateHpGainFromSupplier(supplier, target, service);
        fuelGain = EstimateFuelGainFromSupplier(supplier, target, service);
        ammoGain = EstimateAmmoGainFromSupplier(supplier, target, service);
    }

    private static void EstimateServiceGainsFromConstruction(
        ConstructionManager sourceConstruction,
        UnitManager target,
        ServiceData service,
        out int hpGain,
        out int fuelGain,
        out int ammoGain)
    {
        hpGain = EstimateHpGainFromConstruction(sourceConstruction, target, service);
        fuelGain = EstimateFuelGainFromConstruction(sourceConstruction, target, service);
        ammoGain = EstimateAmmoGainFromConstruction(sourceConstruction, target, service);
    }

    private static int EstimateHpGainFromSupplier(UnitManager supplier, UnitManager target, ServiceData service)
    {
        if (supplier == null || target == null || service == null || !service.recuperaHp)
            return 0;

        int missing = Mathf.Max(0, target.GetMaxHP() - target.CurrentHP);
        if (missing <= 0)
            return 0;

        int cap = service.serviceLimitPerUnitPerTurn > 0 ? Mathf.Min(missing, service.serviceLimitPerUnitPerTurn) : missing;
        if (cap <= 0)
            return 0;

        int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
        if (pointsPerSupply <= 0)
            return 0;

        if (!TryResolveSupplyForService(supplier, service, out _, out int stock) || stock <= 0)
            return 0;

        int maxByStock = SafeMultiplyToIntMax(stock, pointsPerSupply);
        return Mathf.Min(cap, maxByStock);
    }

    private static int EstimateFuelGainFromSupplier(UnitManager supplier, UnitManager target, ServiceData service)
    {
        if (supplier == null || target == null || service == null || !service.recuperaAutonomia)
            return 0;

        int missing = Mathf.Max(0, target.MaxFuel - target.CurrentFuel);
        if (missing <= 0)
            return 0;

        int cap = service.serviceLimitPerUnitPerTurn > 0 ? Mathf.Min(missing, service.serviceLimitPerUnitPerTurn) : missing;
        if (cap <= 0)
            return 0;

        int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
        if (pointsPerSupply <= 0)
            return 0;

        if (!TryResolveSupplyForService(supplier, service, out _, out int stock) || stock <= 0)
            return 0;

        int maxByStock = SafeMultiplyToIntMax(stock, pointsPerSupply);
        return Mathf.Min(cap, maxByStock);
    }

    private static int EstimateAmmoGainFromSupplier(UnitManager supplier, UnitManager target, ServiceData service)
    {
        if (supplier == null || target == null || service == null || !service.recuperaMunicao)
            return 0;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null || targetData.embarkedWeapons == null)
            return 0;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
        if (runtimeWeapons == null || runtimeWeapons.Count <= 0)
            return 0;

        int count = Mathf.Min(runtimeWeapons.Count, targetData.embarkedWeapons.Count);
        int serviceBudget = service.serviceLimitPerUnitPerTurn > 0 ? service.serviceLimitPerUnitPerTurn : int.MaxValue;
        if (serviceBudget <= 0)
            return 0;

        if (service.suppliesUsed == null || service.suppliesUsed.Count <= 0)
            return 0;

        Dictionary<SupplyData, int> localStock = new Dictionary<SupplyData, int>();
        for (int i = 0; i < service.suppliesUsed.Count; i++)
        {
            SupplyData supply = service.suppliesUsed[i];
            if (supply == null || localStock.ContainsKey(supply))
                continue;
            localStock[supply] = Mathf.Max(0, GetSupplierSupplyAmount(supplier, supply));
        }

        int recoveredTotal = 0;
        for (int i = 0; i < count && serviceBudget > 0; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = targetData.embarkedWeapons[i];
            if (runtime == null || baseline == null || baseline.weapon == null)
                continue;

            int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
            int beforeAmmo = Mathf.Max(0, runtime.squadAmmunition);
            int missing = Mathf.Max(0, maxAmmo - beforeAmmo);
            if (missing <= 0)
                continue;

            int cap = Mathf.Min(missing, serviceBudget);
            int pointsPerSupply = ResolvePointsPerSupply(service, ResolveWeaponClass(baseline.weapon));
            if (cap <= 0 || pointsPerSupply <= 0)
                continue;

            if (!TryResolveAvailableSupply(service, localStock, out SupplyData chosenSupply, out int available) || available <= 0)
                break;

            int maxByStock = SafeMultiplyToIntMax(available, pointsPerSupply);
            int recovered = Mathf.Min(cap, maxByStock);
            if (recovered <= 0)
                continue;

            int consumed = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
            localStock[chosenSupply] = Mathf.Max(0, available - consumed);
            recoveredTotal += recovered;
            serviceBudget -= recovered;
        }

        return recoveredTotal;
    }

    private static int EstimateHpGainFromConstruction(ConstructionManager sourceConstruction, UnitManager target, ServiceData service)
    {
        if (sourceConstruction == null || target == null || service == null || !service.recuperaHp)
            return 0;

        int missing = Mathf.Max(0, target.GetMaxHP() - target.CurrentHP);
        if (missing <= 0)
            return 0;

        int cap = service.serviceLimitPerUnitPerTurn > 0 ? Mathf.Min(missing, service.serviceLimitPerUnitPerTurn) : missing;
        if (cap <= 0)
            return 0;

        int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
        if (pointsPerSupply <= 0)
            return 0;

        if (!TryResolveSupplyForService(sourceConstruction, service, out _, out int stock) || stock <= 0)
            return 0;

        int maxByStock = SafeMultiplyToIntMax(stock, pointsPerSupply);
        return Mathf.Min(cap, maxByStock);
    }

    private static int EstimateFuelGainFromConstruction(ConstructionManager sourceConstruction, UnitManager target, ServiceData service)
    {
        if (sourceConstruction == null || target == null || service == null || !service.recuperaAutonomia)
            return 0;

        int missing = Mathf.Max(0, target.MaxFuel - target.CurrentFuel);
        if (missing <= 0)
            return 0;

        int cap = service.serviceLimitPerUnitPerTurn > 0 ? Mathf.Min(missing, service.serviceLimitPerUnitPerTurn) : missing;
        if (cap <= 0)
            return 0;

        int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
        if (pointsPerSupply <= 0)
            return 0;

        if (!TryResolveSupplyForService(sourceConstruction, service, out _, out int stock) || stock <= 0)
            return 0;

        int maxByStock = SafeMultiplyToIntMax(stock, pointsPerSupply);
        return Mathf.Min(cap, maxByStock);
    }

    private static int EstimateAmmoGainFromConstruction(ConstructionManager sourceConstruction, UnitManager target, ServiceData service)
    {
        if (sourceConstruction == null || target == null || service == null || !service.recuperaMunicao)
            return 0;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null || targetData.embarkedWeapons == null)
            return 0;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
        if (runtimeWeapons == null || runtimeWeapons.Count <= 0)
            return 0;

        int count = Mathf.Min(runtimeWeapons.Count, targetData.embarkedWeapons.Count);
        int serviceBudget = service.serviceLimitPerUnitPerTurn > 0 ? service.serviceLimitPerUnitPerTurn : int.MaxValue;
        if (serviceBudget <= 0)
            return 0;

        if (service.suppliesUsed == null || service.suppliesUsed.Count <= 0)
            return 0;

        Dictionary<SupplyData, int> localStock = new Dictionary<SupplyData, int>();
        for (int i = 0; i < service.suppliesUsed.Count; i++)
        {
            SupplyData supply = service.suppliesUsed[i];
            if (supply == null || localStock.ContainsKey(supply))
                continue;
            localStock[supply] = Mathf.Max(0, GetConstructionSupplyAmount(sourceConstruction, supply));
        }

        int recoveredTotal = 0;
        for (int i = 0; i < count && serviceBudget > 0; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = targetData.embarkedWeapons[i];
            if (runtime == null || baseline == null || baseline.weapon == null)
                continue;

            int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
            int beforeAmmo = Mathf.Max(0, runtime.squadAmmunition);
            int missing = Mathf.Max(0, maxAmmo - beforeAmmo);
            if (missing <= 0)
                continue;

            int cap = Mathf.Min(missing, serviceBudget);
            int pointsPerSupply = ResolvePointsPerSupply(service, ResolveWeaponClass(baseline.weapon));
            if (cap <= 0 || pointsPerSupply <= 0)
                continue;

            if (!TryResolveAvailableSupply(service, localStock, out SupplyData chosenSupply, out int available) || available <= 0)
                break;

            int maxByStock = SafeMultiplyToIntMax(available, pointsPerSupply);
            int recovered = Mathf.Min(cap, maxByStock);
            if (recovered <= 0)
                continue;

            int consumed = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
            localStock[chosenSupply] = Mathf.Max(0, available - consumed);
            recoveredTotal += recovered;
            serviceBudget -= recovered;
        }

        return recoveredTotal;
    }

    private static bool TryResolveAvailableSupply(
        ServiceData service,
        Dictionary<SupplyData, int> stockMap,
        out SupplyData chosenSupply,
        out int availableAmount)
    {
        chosenSupply = null;
        availableAmount = 0;
        if (service == null || stockMap == null || service.suppliesUsed == null)
            return false;

        for (int i = 0; i < service.suppliesUsed.Count; i++)
        {
            SupplyData supply = service.suppliesUsed[i];
            if (supply == null)
                continue;
            if (!stockMap.TryGetValue(supply, out int amount))
                continue;
            if (amount <= 0)
                continue;

            chosenSupply = supply;
            availableAmount = amount;
            return true;
        }

        return false;
    }
}
