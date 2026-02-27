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
}
