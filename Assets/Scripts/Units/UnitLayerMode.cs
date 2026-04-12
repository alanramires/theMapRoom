using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct UnitLayerMode
{
    public Domain domain;
    public HeightLevel heightLevel;
    [Header("Visuals (Optional)")]
    public Sprite spriteDefault;
    public List<TeamVariantSprite> teamVariantSprites;

    public UnitLayerMode(Domain domain, HeightLevel heightLevel)
    {
        this.domain = domain;
        this.heightLevel = heightLevel;
        spriteDefault = null;
        teamVariantSprites = new List<TeamVariantSprite>();
    }

    public UnitLayerMode(
        Domain domain,
        HeightLevel heightLevel,
        Sprite spriteDefault,
        List<TeamVariantSprite> teamVariantSprites)
    {
        this.domain = domain;
        this.heightLevel = heightLevel;
        this.spriteDefault = spriteDefault;
        this.teamVariantSprites = teamVariantSprites != null ? teamVariantSprites : new List<TeamVariantSprite>();
    }
}
