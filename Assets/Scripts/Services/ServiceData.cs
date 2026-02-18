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
    [TextArea] public string description;

    [Header("Visuals")]
    public Sprite spriteDefault;

    [Header("Service Type")]
    [Tooltip("Combat: reparar/reabastecer/rearmar. Transfer: transferir estoque.")]
    public ServiceType serviceType = ServiceType.Combat;

    [Header("Economy")]
    [Range(0, 100)]
    public int percentCost = 100;

    [Header("Supply")]
    [FormerlySerializedAs("supplyUsed")]
    [Tooltip("Suprimentos consumidos para usar este servico (opcional).")]
    public List<SupplyData> suppliesUsed = new List<SupplyData>();

    [Header("Points Recovered Per Supply Unit")]
    [Tooltip("How many points this service recovers per unit of supply consumed. Light units are more efficient and recover more per unit.")]
    public List<ServiceEfficiencyByClass> serviceEfficiency = new List<ServiceEfficiencyByClass>();

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
        serviceLimitPerUnitPerTurn = Mathf.Max(0, serviceLimitPerUnitPerTurn);
    }
}
