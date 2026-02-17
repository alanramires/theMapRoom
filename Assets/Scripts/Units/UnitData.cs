using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Game/Units/Unit Data", fileName = "UnitData_")]
public class UnitData : ScriptableObject
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
    public int maxHP = 10;
    public int defense = 0;
    public int movement = 3;
    public MovementCategory movementCategory = MovementCategory.Marcha;
    [Header("Native Domain")]
    [Tooltip("Dominio/altura nativo da unidade.")]
    public Domain domain = Domain.Land;
    [Tooltip("Dominio/altura nativo da unidade.")]
    public HeightLevel heightLevel = HeightLevel.Surface;
    [FormerlySerializedAs("additionalLayerModes")]
    [Tooltip("Modos alternativos de dominio/altura (ex.: Submarine/Submerged tambem pode Naval/Surface).")]
    public List<UnitLayerMode> aditionalDomainsAllowed = new List<UnitLayerMode>();
    [Header("Skills")]
    [Tooltip("Skills base da unidade. As instancias herdam essa lista ao aplicar o UnitData.")]
    public List<SkillData> skills = new List<SkillData>();
    public int autonomia = 99;
    public int cost = 100;
}
