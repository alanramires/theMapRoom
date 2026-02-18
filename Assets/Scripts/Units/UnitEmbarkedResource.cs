using UnityEngine;

[System.Serializable]
public class UnitEmbarkedResource
{
    [Tooltip("Recurso escolhido do catalogo (ResourceData).")]
    public ResourceData resource;

    [Tooltip("Quantidade de cargas disponiveis deste recurso nesta unidade.")]
    [Min(0)]
    public int charges = 0;

    [Header("Range Setup")]
    [Tooltip("Alcance operacional minimo deste recurso nesta unidade.")]
    [Min(0)]
    public int operationRangeMin = 0;

    [Tooltip("Alcance operacional maximo deste recurso nesta unidade.")]
    [Min(0)]
    public int operationRangeMax = 0;

    [SerializeField, HideInInspector] private ResourceData lastSyncedResource;

    public void SyncFromResourceDefaultsIfNeeded()
    {
        bool resourceChanged = lastSyncedResource != resource;
        if (resource == null)
        {
            if (resourceChanged)
            {
                operationRangeMin = 0;
                operationRangeMax = 0;
            }

            lastSyncedResource = null;
            return;
        }

        if (resourceChanged)
        {
            operationRangeMin = Mathf.Max(0, resource.operationRangeMin);
            operationRangeMax = Mathf.Max(operationRangeMin, resource.operationRangeMax);
        }
        else if (operationRangeMax < operationRangeMin)
        {
            operationRangeMax = operationRangeMin;
        }

        lastSyncedResource = resource;
    }
}
