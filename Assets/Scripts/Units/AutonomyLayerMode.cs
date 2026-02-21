using UnityEngine;

[System.Serializable]
public struct AutonomyLayerMode
{
    [Tooltip("Dominio para validar upkeep de autonomia.")]
    public Domain domain;

    [Tooltip("Altura para validar upkeep de autonomia.")]
    public HeightLevel heightLevel;

    public AutonomyLayerMode(Domain domain, HeightLevel heightLevel)
    {
        this.domain = domain;
        this.heightLevel = heightLevel;
    }
}
