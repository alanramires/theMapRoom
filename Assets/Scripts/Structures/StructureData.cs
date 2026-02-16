using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Structures/Structure Data", fileName = "StructureData_")]
public class StructureData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico usado para lookup e referencia.")]
    public string id;

    [Tooltip("Nome mostrado na UI/debug.")]
    public string displayName;

    [TextArea]
    public string description;

    [Header("Layer Modes")]
    [Tooltip("Dominio/altura nativo da estrutura.")]
    public Domain domain = Domain.Land;
    [Tooltip("Dominio/altura nativo da estrutura.")]
    public HeightLevel heightLevel = HeightLevel.Surface;
    [Tooltip("Dominios/alturas adicionais permitidos pela estrutura.")]
    public List<TerrainLayerMode> additionalLayerModes = new List<TerrainLayerMode>();
    [Tooltip("Se true, dominio do ar e sempre permitido nesta estrutura.")]
    public bool alwaysAllowAirDomain = false;

    [Header("Road Routes")]
    [Tooltip("Rotas de rodovia desta estrutura (centro-a-centro dos hexes).")]
    public List<RoadRouteDefinition> roadRoutes = new List<RoadRouteDefinition>();
}
