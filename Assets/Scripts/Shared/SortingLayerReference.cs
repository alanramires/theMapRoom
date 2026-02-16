using UnityEngine;

[System.Serializable]
public struct SortingLayerReference
{
    [SerializeField] private int id;

    public int Id => id;

    public static SortingLayerReference FromName(string layerName)
    {
        return new SortingLayerReference
        {
            id = SortingLayer.NameToID(layerName)
        };
    }
}
