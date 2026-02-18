using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

public enum ResourceType
{
    Sensor = 0,
    Utility = 1,
    Energy = 2
}

[CreateAssetMenu(menuName = "Game/Resources/Resource Data", fileName = "ResourceData_")]
public class ResourceData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico usado para lookup e referencia.")]
    public string id;

    [Tooltip("Nome mostrado na UI/debug.")]
    public string displayName;

    [TextArea]
    public string description;

    [Header("Visuals")]
    public Sprite spriteDefault;

    [Header("Resource")]
    public ResourceType resourceType = ResourceType.Sensor;
    [Tooltip("Alcance operacional minimo deste recurso.")]
    [Min(0)]
    public int operationRangeMin = 1;
    [Tooltip("Alcance operacional maximo deste recurso.")]
    [Min(0)]
    public int operationRangeMax = 1;

    [Header("Native Domain")]
    [Tooltip("Dominio/altura nativo deste recurso/sensor.")]
    public Domain domain = Domain.Land;
    [Tooltip("Dominio/altura nativo deste recurso/sensor.")]
    public HeightLevel heightLevel = HeightLevel.Surface;

    [Header("Detection Domains")]
    [FormerlySerializedAs("additionalLayerModes")]
    [Tooltip("Dominios/alturas que este recurso consegue detectar/afetar.")]
    public List<TerrainLayerMode> detectableDomainsAllowed = new List<TerrainLayerMode>();

    public bool CanOperateOn(Domain operationDomain, HeightLevel operationHeightLevel)
    {
        return domain == operationDomain && heightLevel == operationHeightLevel;
    }

    public bool CanDetect(Domain targetDomain, HeightLevel targetHeightLevel)
    {
        if (domain == targetDomain && heightLevel == targetHeightLevel)
            return true;

        if (detectableDomainsAllowed == null)
            return false;

        for (int i = 0; i < detectableDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = detectableDomainsAllowed[i];
            if (mode.domain == targetDomain && mode.heightLevel == targetHeightLevel)
                return true;
        }

        return false;
    }

    private void OnValidate()
    {
        if (operationRangeMin < 0)
            operationRangeMin = 0;
        if (operationRangeMax < 0)
            operationRangeMax = 0;
        if (operationRangeMax < operationRangeMin)
            operationRangeMax = operationRangeMin;
    }
}
