using UnityEngine;

[System.Serializable]
public struct UnitLayerMode
{
    public Domain domain;
    public HeightLevel heightLevel;
    [Header("Visuals (Optional)")]
    public Sprite spriteDefault;
    public Sprite spriteGreen;
    public Sprite spriteRed;
    public Sprite spriteBlue;
    public Sprite spriteYellow;

    public UnitLayerMode(Domain domain, HeightLevel heightLevel)
    {
        this.domain = domain;
        this.heightLevel = heightLevel;
        spriteDefault = null;
        spriteGreen = null;
        spriteRed = null;
        spriteBlue = null;
        spriteYellow = null;
    }

    public UnitLayerMode(
        Domain domain,
        HeightLevel heightLevel,
        Sprite spriteDefault,
        Sprite spriteGreen,
        Sprite spriteRed,
        Sprite spriteBlue,
        Sprite spriteYellow)
    {
        this.domain = domain;
        this.heightLevel = heightLevel;
        this.spriteDefault = spriteDefault;
        this.spriteGreen = spriteGreen;
        this.spriteRed = spriteRed;
        this.spriteBlue = spriteBlue;
        this.spriteYellow = spriteYellow;
    }
}
