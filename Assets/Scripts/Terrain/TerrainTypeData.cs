using System.Collections.Generic;
using UnityEngine;
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

    [Header("Layer Modes")]
    [Tooltip("Dominio/altura nativo do terreno.")]
    public Domain domain = Domain.Land;
    [Tooltip("Dominio/altura nativo do terreno.")]
    public HeightLevel heightLevel = HeightLevel.Surface;
    [Tooltip("Dominios/alturas adicionais permitidos neste terreno.")]
    public List<TerrainLayerMode> additionalLayerModes = new List<TerrainLayerMode>();

    [Header("Flags")]
    [Tooltip("Se true, dominio do ar e sempre permitido (independente dos modos).")]
    public bool alwaysAllowAirDomain = true;
}
