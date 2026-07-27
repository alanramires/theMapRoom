using System.Collections.Generic;
using UnityEngine;

public enum StockNeedLevel
{
    None = 0,
    Preventive = 1,
    Operational = 2,
    Critical = 3
}

public sealed class StockResourceNeed
{
    public SupplyData supply;
    public int current;
    public int capacity;
    public int missing;
    public float fillRatio;
}

public sealed class StockTransferEstimate
{
    public SupplyData supply;
    public int available;
    public int requested;
    public int amount;
}

public sealed class StockNeedAssessment
{
    public UnitManager unit;
    public StockNeedLevel level;
    public int totalCurrent;
    public int totalCapacity;
    public int totalMissing;
    public float fillRatio;
    public bool blocksFieldService;
    public string reason;
    public readonly List<StockResourceNeed> resources =
        new List<StockResourceNeed>();

    public bool NeedsStock => level != StockNeedLevel.None;
}

/// <summary>
/// Consulta pura do estado de reservas de uma unidade. A capacidade vem do
/// UnitData e o estoque vem da instancia. No Scene Editor, quando a instancia
/// ainda nao possui runtime inicializado, a consulta emula o estado de compra
/// descrito por startsWithEmptySupplies.
/// </summary>
public static class StockNeedAssessmentService
{
    public static StockNeedAssessment Evaluate(
        UnitManager unit,
        bool emulateFromUnitDataWhenRuntimeUnavailable = false)
    {
        var result = new StockNeedAssessment
        {
            unit = unit,
            level = StockNeedLevel.None,
            reason = "Unidade sem perfil de estoque."
        };
        if (unit == null
            || !unit.TryGetUnitData(out UnitData data)
            || data == null
            || !data.isSupplier
            || data.supplierResources == null
            || data.supplierResources.Count == 0)
            return result;

        Dictionary<SupplyData, int> capacity =
            BuildSupplyMap(data.supplierResources);
        IReadOnlyList<UnitEmbarkedSupply> runtime =
            unit.GetEmbarkedResources();
        bool hasRuntime = runtime != null && runtime.Count > 0;
        Dictionary<SupplyData, int> current = hasRuntime
            ? BuildSupplyMap(runtime)
            : BuildEditorFallback(
                data,
                emulateFromUnitDataWhenRuntimeUnavailable);

        foreach (KeyValuePair<SupplyData, int> pair in capacity)
        {
            SupplyData supply = pair.Key;
            int max = Mathf.Max(0, pair.Value);
            int amount = current.TryGetValue(supply, out int stored)
                ? Mathf.Clamp(stored, 0, max)
                : 0;
            int missing = Mathf.Max(0, max - amount);
            result.resources.Add(new StockResourceNeed
            {
                supply = supply,
                current = amount,
                capacity = max,
                missing = missing,
                fillRatio = max > 0 ? (float)amount / max : 1f
            });
            result.totalCurrent += amount;
            result.totalCapacity += max;
            result.totalMissing += missing;
        }

        result.fillRatio = result.totalCapacity > 0
            ? Mathf.Clamp01(
                (float)result.totalCurrent / result.totalCapacity)
            : 1f;
        result.level = ResolveLevel(
            result.totalCurrent,
            result.totalMissing,
            result.fillRatio);
        result.blocksFieldService =
            result.totalCurrent <= 0
            && data.supplierServiceProfile ==
                SupplierServiceProfile.FieldService;
        result.reason = BuildReason(result);
        return result;
    }

    public static int EstimateCompatibleTransfer(
        UnitManager source,
        UnitManager destination,
        bool emulateFromUnitDataWhenRuntimeUnavailable = false)
    {
        if (source == null || destination == null)
            return 0;

        Dictionary<SupplyData, int> sourceStock =
            ReadCurrentStock(
                source,
                emulateFromUnitDataWhenRuntimeUnavailable);
        StockNeedAssessment need = Evaluate(
            destination,
            emulateFromUnitDataWhenRuntimeUnavailable);
        int total = 0;
        for (int i = 0; i < need.resources.Count; i++)
        {
            StockResourceNeed resource = need.resources[i];
            if (resource?.supply == null || resource.missing <= 0)
                continue;
            if (!sourceStock.TryGetValue(
                    resource.supply, out int available)
                || available <= 0)
                continue;
            total += Mathf.Min(available, resource.missing);
        }
        return total;
    }

    public static int CollectTransferEstimate(
        UnitManager sourceUnit,
        ConstructionManager sourceConstruction,
        UnitManager destinationUnit,
        ConstructionManager destinationConstruction,
        bool emulateFromUnitDataWhenRuntimeUnavailable,
        List<StockTransferEstimate> output)
    {
        output?.Clear();
        Dictionary<SupplyData, int> sourceStock =
            sourceUnit != null
                ? ReadCurrentStock(
                    sourceUnit,
                    emulateFromUnitDataWhenRuntimeUnavailable)
                : ReadConstructionStock(sourceConstruction);
        if (sourceStock.Count == 0)
            return 0;

        var missing = new Dictionary<SupplyData, int>();
        if (destinationUnit != null)
        {
            StockNeedAssessment need = Evaluate(
                destinationUnit,
                emulateFromUnitDataWhenRuntimeUnavailable);
            for (int i = 0; i < need.resources.Count; i++)
            {
                StockResourceNeed resource = need.resources[i];
                if (resource?.supply != null
                    && resource.missing > 0)
                    missing[resource.supply] = resource.missing;
            }
        }

        int total = 0;
        foreach (KeyValuePair<SupplyData, int> pair in sourceStock)
        {
            SupplyData supply = pair.Key;
            int available = Mathf.Max(0, pair.Value);
            if (supply == null || available <= 0)
                continue;

            int requested;
            if (destinationUnit != null)
            {
                if (!missing.TryGetValue(supply, out requested)
                    || requested <= 0)
                    continue;
            }
            else if (destinationConstruction != null)
            {
                // A capacidade de construcoes e administrada pelo fluxo de
                // transferencia; para planejamento, todo o lote embarcado e
                // uma quantidade potencial.
                requested = available;
            }
            else
            {
                continue;
            }

            int amount = available == int.MaxValue
                ? requested
                : Mathf.Min(available, requested);
            if (amount <= 0)
                continue;
            total += amount;
            output?.Add(new StockTransferEstimate
            {
                supply = supply,
                available = available,
                requested = requested,
                amount = amount
            });
        }
        return total;
    }

    public static int GetTotalCurrentStock(
        UnitManager unit,
        bool emulateFromUnitDataWhenRuntimeUnavailable = false)
    {
        Dictionary<SupplyData, int> stock =
            ReadCurrentStock(
                unit,
                emulateFromUnitDataWhenRuntimeUnavailable);
        int total = 0;
        foreach (KeyValuePair<SupplyData, int> pair in stock)
            total += Mathf.Max(0, pair.Value);
        return total;
    }

    private static StockNeedLevel ResolveLevel(
        int current,
        int missing,
        float ratio)
    {
        if (missing <= 0)
            return StockNeedLevel.None;
        if (current <= 0 || ratio <= 0.25f)
            return StockNeedLevel.Critical;
        if (ratio <= 0.5f)
            return StockNeedLevel.Operational;
        return StockNeedLevel.Preventive;
    }

    private static string BuildReason(StockNeedAssessment result)
    {
        if (result == null || result.totalCapacity <= 0)
            return "Sem capacidade configurada.";
        if (!result.NeedsStock)
            return $"Estoque completo: {result.totalCurrent}/" +
                   $"{result.totalCapacity}.";

        string service = result.blocksFieldService
            ? " Servicos de campo indisponiveis."
            : string.Empty;
        return $"{result.level}: estoque {result.totalCurrent}/" +
               $"{result.totalCapacity}; faltam " +
               $"{result.totalMissing}.{service}";
    }

    private static Dictionary<SupplyData, int> ReadCurrentStock(
        UnitManager unit,
        bool emulateFromUnitDataWhenRuntimeUnavailable)
    {
        var result = new Dictionary<SupplyData, int>();
        if (unit == null
            || !unit.TryGetUnitData(out UnitData data)
            || data == null)
            return result;

        IReadOnlyList<UnitEmbarkedSupply> runtime =
            unit.GetEmbarkedResources();
        if (runtime != null && runtime.Count > 0)
            return BuildSupplyMap(runtime);
        return BuildEditorFallback(
            data,
            emulateFromUnitDataWhenRuntimeUnavailable);
    }

    private static Dictionary<SupplyData, int> ReadConstructionStock(
        ConstructionManager construction)
    {
        var result = new Dictionary<SupplyData, int>();
        if (construction == null || !construction.CanProvideSupplies)
            return result;
        IReadOnlyList<ConstructionSupplyOffer> offers =
            construction.OfferedSupplies;
        for (int i = 0; offers != null && i < offers.Count; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer?.supply == null)
                continue;
            int amount = construction.HasInfiniteSuppliesFor(
                offer.supply)
                    ? int.MaxValue
                    : Mathf.Max(0, offer.quantity);
            if (result.TryGetValue(
                    offer.supply, out int existing))
            {
                result[offer.supply] =
                    existing == int.MaxValue || amount == int.MaxValue
                        ? int.MaxValue
                        : existing + amount;
            }
            else
            {
                result[offer.supply] = amount;
            }
        }
        return result;
    }

    private static Dictionary<SupplyData, int> BuildEditorFallback(
        UnitData data,
        bool emulate)
    {
        if (!emulate || data == null || data.startsWithEmptySupplies)
            return new Dictionary<SupplyData, int>();
        return BuildSupplyMap(data.supplierResources);
    }

    private static Dictionary<SupplyData, int> BuildSupplyMap(
        IReadOnlyList<UnitEmbarkedSupply> entries)
    {
        var result = new Dictionary<SupplyData, int>();
        if (entries == null)
            return result;
        for (int i = 0; i < entries.Count; i++)
        {
            UnitEmbarkedSupply entry = entries[i];
            if (entry?.supply == null)
                continue;
            int amount = Mathf.Max(0, entry.amount);
            if (result.TryGetValue(
                    entry.supply, out int existing))
                result[entry.supply] = existing + amount;
            else
                result[entry.supply] = amount;
        }
        return result;
    }
}
