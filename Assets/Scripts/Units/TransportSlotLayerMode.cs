using UnityEngine;

[System.Serializable]
public struct TransportSlotLayerMode
{
    [Tooltip("Dominio permitido para embarque neste slot.")]
    public Domain domain;

    [Tooltip("Altura permitida para embarque neste slot.")]
    public HeightLevel heightLevel;

    public TransportSlotLayerMode(Domain domain, HeightLevel heightLevel)
    {
        this.domain = domain;
        this.heightLevel = heightLevel;
    }
}
