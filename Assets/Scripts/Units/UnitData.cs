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
    [SerializeField, HideInInspector] private ArmorClass armorClass = ArmorClass.Light;
    public int movement = 3;
    public MovementCategory movementCategory = MovementCategory.Marcha;
    public MilitaryForce militaryForce = MilitaryForce.Army;
    public GameUnitClass unitClass = GameUnitClass.Infantry;
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
    [Header("Embarked Weapons")]
    [Tooltip("Armas embarcadas na unidade. A ordem da lista define prioridade (primaria, secundaria...). Pode ficar vazia para unidades desarmadas.")]
    public List<UnitEmbarkedWeapon> embarkedWeapons = new List<UnitEmbarkedWeapon>();
    [Header("Embarked Supplies")]
    [Tooltip("Suprimentos embarcados na unidade. Pode ficar vazia para unidades sem carga logistica.")]
    public List<UnitEmbarkedSupply> embarkedSupplies = new List<UnitEmbarkedSupply>();

    [Header("Logistics")]
    public bool isSupplier = false;
    public SupplierTier supplierTier = SupplierTier.Hub;
    [Min(0)] public int maxUnitsServedPerTurn = 0;
    public SupplierRangeMode serviceRange = SupplierRangeMode.EmbarkedOnly;
    public SupplierRangeMode collectionRange = SupplierRangeMode.EmbarkedOnly;
    [Tooltip("Dominios/alturas onde esta unidade consegue operar logistica.")]
    public List<SupplierOperationDomain> supplierOperationDomains = new List<SupplierOperationDomain>();
    [Tooltip("Servicos oferecidos por esta unidade de logistica.")]
    public List<ServiceData> supplierServicesProvided = new List<ServiceData>();

    public int autonomia = 99;
    public int cost = 100;

    public ArmorClass ArmorClass => armorClass;

    private void OnValidate()
    {
        SyncArmorClassFromDefense();

        if (embarkedWeapons == null)
            embarkedWeapons = new List<UnitEmbarkedWeapon>();
        if (embarkedSupplies == null)
            embarkedSupplies = new List<UnitEmbarkedSupply>();
        if (supplierOperationDomains == null)
            supplierOperationDomains = new List<SupplierOperationDomain>();
        if (supplierServicesProvided == null)
            supplierServicesProvided = new List<ServiceData>();
        maxUnitsServedPerTurn = Mathf.Max(0, maxUnitsServedPerTurn);

        for (int i = 0; i < embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon embarkedWeapon = embarkedWeapons[i];
            if (embarkedWeapon == null)
                continue;

            embarkedWeapon.SyncFromWeaponDefaultsIfNeeded();
        }

    }

    private void SyncArmorClassFromDefense()
    {
        if (defense >= 15)
        {
            armorClass = ArmorClass.Heavy;
            return;
        }

        if (defense >= 12)
        {
            armorClass = ArmorClass.Medium;
            return;
        }

        armorClass = ArmorClass.Light;
    }
}
