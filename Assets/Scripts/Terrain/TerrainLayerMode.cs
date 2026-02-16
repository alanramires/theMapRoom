using UnityEngine;

[System.Serializable]
public struct TerrainLayerMode
{
    public Domain domain;
    public HeightLevel heightLevel;

    public TerrainLayerMode(Domain domain, HeightLevel heightLevel)
    {
        this.domain = domain;
        this.heightLevel = heightLevel;
    }
}
