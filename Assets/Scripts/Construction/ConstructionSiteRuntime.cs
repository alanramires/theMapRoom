using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConstructionSiteRuntime
{
    [Header("Role")]
    [Tooltip("Marca esta construcao como HQ de jogador (pode disparar condicao de fim de jogo).")]
    public bool isPlayerHeadQuarter = false;

    [Header("Capture")]
    public bool isCapturable = true;
    [Min(0)] public int capturePointsMax = 20;

    [Header("Production")]
    public bool canProduceUnits = false;
    [Tooltip("Unidades que esta construcao pode produzir nesta partida/mapa.")]
    public List<UnitData> offeredUnits = new List<UnitData>();

    [Header("Supplies")]
    public bool canProvideSupplies = false;
    [Tooltip("Suprimentos oferecidos por esta construcao (com quantidade).")]
    public List<ConstructionSupplyOffer> offeredSupplies = new List<ConstructionSupplyOffer>();

    [Header("Services")]
    [Tooltip("Servicos oferecidos por esta construcao (ex.: repair, refuel, rearm).")]
    public List<ServiceData> offeredServices = new List<ServiceData>();

    public void Sanitize()
    {
        capturePointsMax = Mathf.Max(0, capturePointsMax);

        if (offeredUnits == null)
            offeredUnits = new List<UnitData>();
        if (offeredSupplies == null)
            offeredSupplies = new List<ConstructionSupplyOffer>();
        if (offeredServices == null)
            offeredServices = new List<ServiceData>();

        for (int i = 0; i < offeredSupplies.Count; i++)
        {
            ConstructionSupplyOffer offer = offeredSupplies[i];
            if (offer == null)
                continue;
            offer.quantity = Mathf.Max(0, offer.quantity);
        }
    }

    public ConstructionSiteRuntime Clone()
    {
        ConstructionSiteRuntime copy = new ConstructionSiteRuntime
        {
            isPlayerHeadQuarter = isPlayerHeadQuarter,
            isCapturable = isCapturable,
            capturePointsMax = capturePointsMax,
            canProduceUnits = canProduceUnits,
            canProvideSupplies = canProvideSupplies,
            offeredUnits = offeredUnits != null ? new List<UnitData>(offeredUnits) : new List<UnitData>(),
            offeredServices = offeredServices != null ? new List<ServiceData>(offeredServices) : new List<ServiceData>(),
            offeredSupplies = CloneSupplyOffers(offeredSupplies)
        };
        copy.Sanitize();
        return copy;
    }

    private static List<ConstructionSupplyOffer> CloneSupplyOffers(List<ConstructionSupplyOffer> source)
    {
        List<ConstructionSupplyOffer> result = new List<ConstructionSupplyOffer>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            ConstructionSupplyOffer offer = source[i];
            if (offer == null)
                continue;

            result.Add(new ConstructionSupplyOffer
            {
                supply = offer.supply,
                quantity = Mathf.Max(0, offer.quantity)
            });
        }

        return result;
    }
}
