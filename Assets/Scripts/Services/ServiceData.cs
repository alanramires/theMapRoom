using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

public enum ServiceType
{
    Combat = 0,
    Transfer = 1
}

[CreateAssetMenu(menuName = "Game/Services/Service Data", fileName = "ServiceData_")]
public class ServiceData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    [Tooltip("Apelido curto para tabelas/matrizes (ex.: RPR, RST).")]
    public string apelido;
    [TextArea] public string description;

    [Header("Visuals")]
    public Sprite spriteDefault;

    [Header("Service Type")]
    [Tooltip("Combat: reparar/reabastecer/rearmar. Transfer: transferir estoque.")]
    public ServiceType serviceType = ServiceType.Combat;
    [Tooltip("Marca se este item eh um servico prestado em campo. Desative para operacoes de movimentacao de estoque.")]
    public bool isService = true;
    [Tooltip("Servico recupera HP da unidade alvo.")]
    public bool recuperaHp;
    [Tooltip("Servico recupera autonomia da unidade alvo.")]
    public bool recuperaAutonomia;
    [Tooltip("Servico recupera municao da unidade alvo.")]
    public bool recuperaMunicao;
    [Tooltip("Quando ativo, este servico so pode ser aplicado entre unidades/construcoes supridoras.")]
    public bool apenasEntreSupridores;

    [Header("Economy")]
    [Range(0, 100)]
    public int percentCost = 100;

    [Header("Supply")]
    [FormerlySerializedAs("supplyUsed")]
    [Tooltip("Suprimentos consumidos para usar este servico (opcional).")]
    public List<SupplyData> suppliesUsed = new List<SupplyData>();

    [Header("Points recover per 1 unit of supply used")]
    [Tooltip("How many points this service recovers per unit of supply consumed. Light units are more efficient and recover more per unit.")]
    public List<ServiceEfficiencyByClass> serviceEfficiency = new List<ServiceEfficiencyByClass>();

    [FormerlySerializedAs("weight")]
    [Header("Cost Weight")]
    [Tooltip("Cost weight values by armor/weapon class for this service.")]
    public List<ServiceEfficiencyByClass> costWeight = new List<ServiceEfficiencyByClass>();

    [Header("Service Limits")]
    [Tooltip("Limite de servico por unidade/turno. 0 = sem limite.")]
    [Min(0)]
    public int serviceLimitPerUnitPerTurn = 0;

    private void OnValidate()
    {
        percentCost = Mathf.Clamp(percentCost, 0, 100);
        if (suppliesUsed == null)
            suppliesUsed = new List<SupplyData>();
        if (serviceEfficiency == null)
            serviceEfficiency = new List<ServiceEfficiencyByClass>();
        if (costWeight == null)
            costWeight = new List<ServiceEfficiencyByClass>();
        serviceLimitPerUnitPerTurn = Mathf.Max(0, serviceLimitPerUnitPerTurn);
    }
}

public static class ServiceCostFormula
{
    public static int ComputeServiceMoneyCost(
        UnitManager target,
        ServiceData service,
        int hpGain,
        int fuelGain,
        int ammoGain,
        IReadOnlyList<int> ammoGainByWeapon = null,
        List<int> ammoCostByWeapon = null)
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
            int unitHpCost = Mathf.RoundToInt(allocated / maxHp);
            totalCost += Mathf.Max(0, hpGain) * Mathf.Max(0, unitHpCost);
        }

        if (fuelGain > 0)
        {
            int maxFuel = Mathf.Max(1, target.GetMaxFuel());
            int unitFuelCost = Mathf.RoundToInt(allocated / maxFuel);
            totalCost += Mathf.Max(0, fuelGain) * Mathf.Max(0, unitFuelCost);
        }

        if (ammoGain > 0)
        {
            int weightedCapacity = ComputeWeightedWeaponCapacity(targetData, service);
            if (weightedCapacity > 0)
            {
                float costPerWeightedPoint = allocated / weightedCapacity;
                int ammoCost = 0;
                bool usedByWeapon = ammoGainByWeapon != null && ammoGainByWeapon.Count > 0;
                if (usedByWeapon && targetData.embarkedWeapons != null)
                {
                    int count = Mathf.Min(ammoGainByWeapon.Count, targetData.embarkedWeapons.Count);
                    for (int i = 0; i < count; i++)
                    {
                        int recovered = Mathf.Max(0, ammoGainByWeapon[i]);
                        if (recovered <= 0)
                            continue;

                        UnitEmbarkedWeapon weaponEntry = targetData.embarkedWeapons[i];
                        WeaponData weapon = weaponEntry != null ? weaponEntry.weapon : null;
                        float weight = ResolveWeaponCostWeight(service, weapon);
                        int unitWeaponCost = Mathf.RoundToInt(costPerWeightedPoint * weight);
                        int weaponCost = Mathf.Max(0, recovered) * Mathf.Max(0, unitWeaponCost);
                        ammoCost += Mathf.Max(0, weaponCost);

                        if (ammoCostByWeapon != null)
                        {
                            while (ammoCostByWeapon.Count <= i)
                                ammoCostByWeapon.Add(0);
                            ammoCostByWeapon[i] = Mathf.Max(0, weaponCost);
                        }
                    }
                }
                else
                {
                    float averageWeight = ComputeAverageWeaponCostWeight(targetData, service);
                    int unitAmmoCost = Mathf.RoundToInt(costPerWeightedPoint * averageWeight);
                    ammoCost = Mathf.Max(0, ammoGain) * Mathf.Max(0, unitAmmoCost);
                }

                totalCost += Mathf.Max(0, ammoCost);
            }
        }

        return Mathf.Max(0, totalCost);
    }

    public static float ResolveCostWeightForClass(ServiceData service, ArmorWeaponClass classKey)
    {
        if (service != null && service.costWeight != null)
        {
            for (int i = 0; i < service.costWeight.Count; i++)
            {
                ServiceEfficiencyByClass entry = service.costWeight[i];
                if (entry == null || entry.armorWeaponClass != classKey)
                    continue;
                return Mathf.Max(0f, entry.value);
            }
        }

        switch (classKey)
        {
            case ArmorWeaponClass.Heavy:
                return 3f;
            case ArmorWeaponClass.Medium:
                return 2f;
            default:
                return 1f;
        }
    }

    private static float ResolveWeaponCostWeight(ServiceData service, WeaponData weapon)
    {
        ArmorWeaponClass classKey = ArmorWeaponClass.Light;
        if (weapon != null)
        {
            switch (weapon.WeaponClass)
            {
                case WeaponClass.Heavy:
                    classKey = ArmorWeaponClass.Heavy;
                    break;
                case WeaponClass.Medium:
                    classKey = ArmorWeaponClass.Medium;
                    break;
                default:
                    classKey = ArmorWeaponClass.Light;
                    break;
            }
        }

        return ResolveCostWeightForClass(service, classKey);
    }

    private static int ComputeWeightedWeaponCapacity(UnitData targetData, ServiceData service)
    {
        if (targetData == null || targetData.embarkedWeapons == null)
            return 0;

        float weighted = 0f;
        for (int i = 0; i < targetData.embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon weaponEntry = targetData.embarkedWeapons[i];
            if (weaponEntry == null || weaponEntry.weapon == null)
                continue;

            int maxAmmo = Mathf.Max(0, weaponEntry.squadAmmunition);
            float weight = ResolveWeaponCostWeight(service, weaponEntry.weapon);
            weighted += maxAmmo * Mathf.Max(0f, weight);
        }

        return Mathf.Max(0, Mathf.RoundToInt(weighted));
    }

    private static float ComputeAverageWeaponCostWeight(UnitData targetData, ServiceData service)
    {
        if (targetData == null || targetData.embarkedWeapons == null || targetData.embarkedWeapons.Count <= 0)
            return 1f;

        float totalAmmo = 0f;
        float weightedAmmo = 0f;
        for (int i = 0; i < targetData.embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon weaponEntry = targetData.embarkedWeapons[i];
            if (weaponEntry == null || weaponEntry.weapon == null)
                continue;
            int maxAmmo = Mathf.Max(0, weaponEntry.squadAmmunition);
            if (maxAmmo <= 0)
                continue;
            float weight = ResolveWeaponCostWeight(service, weaponEntry.weapon);
            totalAmmo += maxAmmo;
            weightedAmmo += maxAmmo * Mathf.Max(0f, weight);
        }

        if (totalAmmo <= 0f)
            return 1f;
        return Mathf.Max(0f, weightedAmmo / totalAmmo);
    }
}

public static class ServiceLogisticsFormula
{
    public static void EstimatePotentialServiceGains(
        UnitManager target,
        ServiceData service,
        Dictionary<SupplyData, int> sourceStock,
        out int hpGain,
        out int fuelGain,
        out int ammoGain,
        List<int> ammoByWeapon = null,
        int simulatedHp = -1,
        int simulatedFuel = -1,
        List<int> simulatedAmmoByWeapon = null,
        Dictionary<SupplyData, int> consumedBySupply = null,
        bool allowHp = true,
        bool allowFuel = true,
        bool allowAmmo = true)
    {
        hpGain = 0;
        fuelGain = 0;
        ammoGain = 0;
        if (target == null || service == null || sourceStock == null)
            return;

        if (!TryResolveSupplyFromSnapshot(service, sourceStock, out SupplyData usedSupply, out int stockAmount))
            return;
        if (stockAmount <= 0)
            return;

        int budget = service.serviceLimitPerUnitPerTurn > 0 ? service.serviceLimitPerUnitPerTurn : int.MaxValue;

        if (allowHp && service.recuperaHp && budget > 0)
        {
            int currentHp = simulatedHp >= 0 ? simulatedHp : target.CurrentHP;
            int missing = Mathf.Max(0, target.GetMaxHP() - currentHp);
            int cap = service.serviceLimitPerUnitPerTurn > 0 ? Mathf.Min(missing, budget) : missing;
            int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
            if (cap > 0 && pointsPerSupply > 0)
            {
                int maxByStock = SafeMultiplyToIntMax(stockAmount, pointsPerSupply);
                int recovered = Mathf.Min(cap, maxByStock);
                if (recovered > 0)
                {
                    int requiredSupplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
                    int spent = ConsumeFromSnapshot(sourceStock, usedSupply, requiredSupplies);
                    hpGain = Mathf.Min(recovered, SafeMultiplyToIntMax(spent, pointsPerSupply));
                    if (spent > 0 && consumedBySupply != null)
                        AddConsumed(consumedBySupply, usedSupply, spent);
                    budget -= hpGain;
                    stockAmount = ReadStockAmount(sourceStock, usedSupply);
                }
            }
        }

        if (allowFuel && service.recuperaAutonomia && budget > 0 && stockAmount > 0)
        {
            int currentFuel = simulatedFuel >= 0 ? simulatedFuel : target.CurrentFuel;
            int missing = Mathf.Max(0, target.GetMaxFuel() - currentFuel);
            int cap = service.serviceLimitPerUnitPerTurn > 0 ? Mathf.Min(missing, budget) : missing;
            int pointsPerSupply = ResolvePointsPerSupply(service, ResolveArmorClass(target));
            if (cap > 0 && pointsPerSupply > 0)
            {
                int maxByStock = SafeMultiplyToIntMax(stockAmount, pointsPerSupply);
                int recovered = Mathf.Min(cap, maxByStock);
                if (recovered > 0)
                {
                    int requiredSupplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
                    int spent = ConsumeFromSnapshot(sourceStock, usedSupply, requiredSupplies);
                    fuelGain = Mathf.Min(recovered, SafeMultiplyToIntMax(spent, pointsPerSupply));
                    if (spent > 0 && consumedBySupply != null)
                        AddConsumed(consumedBySupply, usedSupply, spent);
                    budget -= fuelGain;
                    stockAmount = ReadStockAmount(sourceStock, usedSupply);
                }
            }
        }

        if (allowAmmo && service.recuperaMunicao && budget > 0 && stockAmount > 0)
        {
            if (target.TryGetUnitData(out UnitData targetData) && targetData != null && targetData.embarkedWeapons != null)
            {
                IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
                int count = Mathf.Min(runtimeWeapons != null ? runtimeWeapons.Count : 0, targetData.embarkedWeapons.Count);
                for (int i = 0; i < count && budget > 0 && stockAmount > 0; i++)
                {
                    UnitEmbarkedWeapon runtime = runtimeWeapons[i];
                    UnitEmbarkedWeapon baseline = targetData.embarkedWeapons[i];
                    if (runtime == null || baseline == null || baseline.weapon == null)
                        continue;

                    int maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
                    int currentAmmo = runtime.squadAmmunition;
                    if (simulatedAmmoByWeapon != null && i < simulatedAmmoByWeapon.Count)
                        currentAmmo = Mathf.Max(0, simulatedAmmoByWeapon[i]);

                    int missing = Mathf.Max(0, maxAmmo - currentAmmo);
                    if (missing <= 0)
                        continue;

                    int cap = Mathf.Min(missing, budget);
                    int pointsPerSupply = ResolvePointsPerSupply(service, ResolveWeaponClass(baseline.weapon));
                    if (cap <= 0 || pointsPerSupply <= 0)
                        continue;

                    int maxByStock = SafeMultiplyToIntMax(stockAmount, pointsPerSupply);
                    int recovered = Mathf.Min(cap, maxByStock);
                    if (recovered <= 0)
                        continue;

                    int requiredSupplies = Mathf.CeilToInt(recovered / (float)pointsPerSupply);
                    int spent = ConsumeFromSnapshot(sourceStock, usedSupply, requiredSupplies);
                    int actualRecovered = Mathf.Min(recovered, SafeMultiplyToIntMax(spent, pointsPerSupply));
                    if (spent > 0 && consumedBySupply != null)
                        AddConsumed(consumedBySupply, usedSupply, spent);
                    ammoGain += actualRecovered;
                    if (actualRecovered > 0 && ammoByWeapon != null)
                    {
                        while (ammoByWeapon.Count <= i)
                            ammoByWeapon.Add(0);
                        ammoByWeapon[i] += actualRecovered;
                    }
                    if (actualRecovered > 0 && simulatedAmmoByWeapon != null)
                    {
                        while (simulatedAmmoByWeapon.Count <= i)
                            simulatedAmmoByWeapon.Add(0);
                        simulatedAmmoByWeapon[i] = Mathf.Min(maxAmmo, currentAmmo + actualRecovered);
                    }
                    budget -= actualRecovered;
                    stockAmount = ReadStockAmount(sourceStock, usedSupply);
                }
            }
        }
    }

    private static void AddConsumed(Dictionary<SupplyData, int> map, SupplyData supply, int amount)
    {
        if (map == null || supply == null || amount <= 0)
            return;
        if (map.TryGetValue(supply, out int current))
            map[supply] = current + amount;
        else
            map.Add(supply, amount);
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

    private static int SafeMultiplyToIntMax(int a, int b)
    {
        if (a <= 0 || b <= 0)
            return 0;

        long product = (long)a * b;
        if (product >= int.MaxValue)
            return int.MaxValue;
        return (int)product;
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
}
