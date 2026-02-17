using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Game/Terrain/Terrain Data", fileName = "TerrainData_")]
public class TerrainTypeData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico usado para lookup e regras.")]
    public string id;

    [Tooltip("Nome mostrado na UI/debug.")]
    public string displayName;

    [TextArea]
    public string description;

    [Header("Palette Identity")]
    [Tooltip("Tile da palette que identifica este terreno no mapa.")]
    public TileBase paletteTile;

    [Header("Native Domain")]
    [Tooltip("Dominio/altura nativo do terreno.")]
    public Domain domain = Domain.Land;
    [Tooltip("Dominio/altura nativo do terreno.")]
    public HeightLevel heightLevel = HeightLevel.Surface;
    [FormerlySerializedAs("additionalLayerModes")]
    [Tooltip("Dominios/alturas adicionais permitidos neste terreno.")]
    public List<TerrainLayerMode> aditionalDomainsAllowed = new List<TerrainLayerMode>();

    [Header("Flags")]
    [Tooltip("Se true, dominio do ar e sempre permitido (independente dos modos).")]
    public bool alwaysAllowAirDomain = true;

    [Header("Movement")]
    [Tooltip("Custo basico de autonomia para entrar neste hex. Minimo 1.")]
    [Min(1)]
    public int basicAutonomyCost = 1;

    [Header("Skill Rules")]
    [Tooltip("Se houver skills nesta lista, a unidade precisa ter pelo menos uma para entrar neste terreno.")]
    public List<SkillData> requiredSkillsToEnter = new List<SkillData>();

    [Tooltip("Overrides opcionais de custo de autonomia por skill.")]
    public List<TerrainSkillCostOverride> skillCostOverrides = new List<TerrainSkillCostOverride>();
}
