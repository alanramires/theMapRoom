using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/Construction/Construction Data", fileName = "ConstructionData_")]
public class ConstructionData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico usado para spawn e lookup.")]
    public string id;

    [Tooltip("Nome mostrado na UI.")]
    public string displayName;

    [TextArea]
    public string description;

    [Header("Visuals")]
    [FormerlySerializedAs("sprite")]
    public Sprite spriteDefault;
    public Sprite spriteGreen;
    public Sprite spriteRed;
    public Sprite spriteBlue;
    public Sprite spriteYellow;

    [Header("Attributes")]
    public int maxHP = 30;

    [Header("Layer Modes")]
    [Tooltip("Dominio/altura nativo da construcao.")]
    public Domain domain = Domain.Land;
    [Tooltip("Dominio/altura nativo da construcao.")]
    public HeightLevel heightLevel = HeightLevel.Surface;
    [Tooltip("Dominios/alturas adicionais permitidos pela construcao (ex.: porto permite Naval/Surface e Submarine/Submerged).")]
    public List<TerrainLayerMode> additionalLayerModes = new List<TerrainLayerMode>();
    [Tooltip("Se true, dominio do ar e sempre permitido para esta construcao.")]
    public bool alwaysAllowAirDomain = true;
}
