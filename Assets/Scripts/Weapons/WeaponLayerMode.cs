using UnityEngine;

[System.Serializable]
public struct WeaponLayerMode
{
    public Domain domain;
    public HeightLevel heightLevel;

    public WeaponLayerMode(Domain domain, HeightLevel heightLevel)
    {
        this.domain = domain;
        this.heightLevel = heightLevel;
    }
}
