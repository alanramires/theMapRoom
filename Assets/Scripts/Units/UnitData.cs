using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class TransportStructureTerrainRule
{
    [Tooltip("Estrutura exigida neste contexto (ex.: estrada).")]
    public StructureData structure;
    [Tooltip("Terreno base exigido junto com a estrutura (ex.: planicie).")]
    public TerrainTypeData baseTerrain;
    [Tooltip("Se true, este par esta explicitamente bloqueado para a regra.")]
    public bool isBlocked = false;
}

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
    [Min(1)] public int visao = 3;
    public MovementCategory movementCategory = MovementCategory.Marcha;
    public MilitaryForce militaryForce = MilitaryForce.Army;
    public GameUnitClass unitClass = GameUnitClass.Infantry;
    [Header("Elite")]
    [Tooltip("Nivel de elite da unidade (padrao: 0).")]
    [Min(0)] public int eliteLevel = 0;
    [Tooltip("Modificadores de RPS de combate aplicados por esta unidade.")]
    public List<CombatModifierData> combatModifiers = new List<CombatModifierData>();
    [Header("Native Domain")]
    [Tooltip("Dominio/altura nativo da unidade.")]
    public Domain domain = Domain.Land;
    [Tooltip("Dominio/altura nativo da unidade.")]
    public HeightLevel heightLevel = HeightLevel.Surface;
    [FormerlySerializedAs("additionalLayerModes")]
    [Tooltip("Modos alternativos de dominio/altura (ex.: Submarine/Submerged tambem pode Naval/Surface).")]
    public List<UnitLayerMode> aditionalDomainsAllowed = new List<UnitLayerMode>();
    [Header("Air Preference")]
    [Tooltip("Se ligado, usa a altura abaixo como preferencial para auto-promocao/decolagem em vez de derivar do dominio nativo.")]
    public bool useExplicitPreferredAirHeight = false;
    [Tooltip("Altura aerea preferencial quando o override acima estiver ligado.")]
    public HeightLevel preferredAirHeight = HeightLevel.AirLow;
    [Header("Naval Preference")]
    [Tooltip("Se ligado, usa a altura naval abaixo como preferencial para auto-ajuste em transicoes de camada naval.")]
    public bool useExplicitPreferredNavalHeight = false;
    [Tooltip("Altura naval preferencial quando o override acima estiver ligado.")]
    public HeightLevel preferredNavalHeight = HeightLevel.Submerged;
    [Header("Skills")]
    [Tooltip("Skills base da unidade. As instancias herdam essa lista ao aplicar o UnitData.")]
    public List<SkillData> skills = new List<SkillData>();
    [Header("Autonomy")]
    [Tooltip("Perfil de autonomia usado pelas regras da skill Operational Autonomy.")]
    public AutonomyData autonomyData;
    [Header("Embarked Weapons")]
    [Tooltip("Armas embarcadas na unidade. A ordem da lista define prioridade (primaria, secundaria...). Pode ficar vazia para unidades desarmadas.")]
    public List<UnitEmbarkedWeapon> embarkedWeapons = new List<UnitEmbarkedWeapon>();

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
    [FormerlySerializedAs("embarkedResources")]
    [FormerlySerializedAs("embarkedSupplies")]
    [Tooltip("Recursos/logistica padrao de fabrica desta unidade fornecedora (Supply + quantidade).")]
    public List<UnitEmbarkedSupply> supplierResources = new List<UnitEmbarkedSupply>();
    [Header("Transport")]
    [Tooltip("Se true, esta unidade pode transportar outras unidades.")]
    public bool isTransporter = false;
    [Tooltip("Sprite opcional exibido quando este transportador estiver com unidades embarcadas. Se vazio, usa o sprite padrao da unidade.")]
    public Sprite spriteTransport;
    [Header("Allowed Embark Terrain When Transporter At")]
    [FormerlySerializedAs("allowedEmbarkTerrains")]
    [Tooltip("Terrain: Terrenos validos para o HEX atual do transportador no embarque. Vazio = sem restricao por terreno.")]
    public List<TerrainTypeData> allowedEmbarkWhenTransporterAtTerrains = new List<TerrainTypeData>();
    [FormerlySerializedAs("allowedEmbarkStructures")]
    [Tooltip("Terrain + Structure: Pares estrutura+terreno base validos para o HEX atual do transportador no embarque.")]
    public List<TransportStructureTerrainRule> allowedEmbarkWhenTransporterAtTerrainStructures = new List<TransportStructureTerrainRule>();
    [FormerlySerializedAs("allowedEmbarkConstructions")]
    [Tooltip("Constructions: Construcoes validas para o HEX atual do transportador no embarque. Vazio = sem restricao por construcao.")]
    public List<ConstructionData> allowedEmbarkWhenTransporterAtConstructions = new List<ConstructionData>();
    [Header("Allowed Disembark Terrain When Transporter At")]
    [FormerlySerializedAs("allowedDisembarkTerrains")]
    [Tooltip("Terrain: Terrenos validos para o HEX atual do transportador no desembarque. Vazio = sem restricao por terreno.")]
    public List<TerrainTypeData> allowedDisembarkWhenTransporterAtTerrains = new List<TerrainTypeData>();
    [FormerlySerializedAs("allowedDisembarkStructures")]
    [Tooltip("Terrain + Structure: Pares estrutura+terreno base validos para o HEX atual do transportador no desembarque.")]
    public List<TransportStructureTerrainRule> allowedDisembarkWhenTransporterAtTerrainStructures = new List<TransportStructureTerrainRule>();
    [FormerlySerializedAs("allowedDisembarkConstructions")]
    [Tooltip("Constructions: Construcoes validas para o HEX atual do transportador no desembarque. Vazio = sem restricao por construcao.")]
    public List<ConstructionData> allowedDisembarkWhenTransporterAtConstructions = new List<ConstructionData>();
    [Header("Passengers Can Disembark And Goes To")]
    [Tooltip("Terrain: Terrenos validos para o HEX de destino do passageiro no desembarque. Vazio = sem restricao por terreno.")]
    public List<TerrainTypeData> passengersCanDisembarkAndGoesToTerrains = new List<TerrainTypeData>();
    [Tooltip("Terrain + Structure: Pares estrutura+terreno base validos para o HEX de destino do passageiro no desembarque.")]
    public List<TransportStructureTerrainRule> passengersCanDisembarkAndGoesToTerrainStructures = new List<TransportStructureTerrainRule>();
    [Tooltip("Constructions: Construcoes validas para o HEX de destino do passageiro no desembarque. Vazio = sem restricao por construcao.")]
    public List<ConstructionData> passengersCanDisembarkAndGoesToConstructions = new List<ConstructionData>();
    [Tooltip("Slots de transporte e regras de embarque.")]
    public List<UnitTransportSlotRule> transportSlots = new List<UnitTransportSlotRule>();

    public int autonomia = 99;
    public int cost = 100;

    public ArmorClass ArmorClass => armorClass;

    private void OnValidate()
    {
        SyncArmorClassFromDefense();

        if (embarkedWeapons == null)
            embarkedWeapons = new List<UnitEmbarkedWeapon>();
        if (supplierOperationDomains == null)
            supplierOperationDomains = new List<SupplierOperationDomain>();
        if (supplierServicesProvided == null)
            supplierServicesProvided = new List<ServiceData>();
        if (supplierResources == null)
            supplierResources = new List<UnitEmbarkedSupply>();
        if (combatModifiers == null)
            combatModifiers = new List<CombatModifierData>();
        if (transportSlots == null)
            transportSlots = new List<UnitTransportSlotRule>();
        if (allowedEmbarkWhenTransporterAtTerrains == null)
            allowedEmbarkWhenTransporterAtTerrains = new List<TerrainTypeData>();
        if (allowedEmbarkWhenTransporterAtTerrainStructures == null)
            allowedEmbarkWhenTransporterAtTerrainStructures = new List<TransportStructureTerrainRule>();
        if (allowedEmbarkWhenTransporterAtConstructions == null)
            allowedEmbarkWhenTransporterAtConstructions = new List<ConstructionData>();
        if (allowedDisembarkWhenTransporterAtTerrains == null)
            allowedDisembarkWhenTransporterAtTerrains = new List<TerrainTypeData>();
        if (allowedDisembarkWhenTransporterAtTerrainStructures == null)
            allowedDisembarkWhenTransporterAtTerrainStructures = new List<TransportStructureTerrainRule>();
        if (allowedDisembarkWhenTransporterAtConstructions == null)
            allowedDisembarkWhenTransporterAtConstructions = new List<ConstructionData>();
        if (passengersCanDisembarkAndGoesToTerrains == null)
            passengersCanDisembarkAndGoesToTerrains = new List<TerrainTypeData>();
        if (passengersCanDisembarkAndGoesToTerrainStructures == null)
            passengersCanDisembarkAndGoesToTerrainStructures = new List<TransportStructureTerrainRule>();
        if (passengersCanDisembarkAndGoesToConstructions == null)
            passengersCanDisembarkAndGoesToConstructions = new List<ConstructionData>();
        eliteLevel = Mathf.Max(0, eliteLevel);
        visao = Mathf.Max(1, visao);
        maxUnitsServedPerTurn = Mathf.Max(0, maxUnitsServedPerTurn);
        if (preferredAirHeight != HeightLevel.AirLow && preferredAirHeight != HeightLevel.AirHigh)
            preferredAirHeight = HeightLevel.AirLow;
        if (preferredNavalHeight != HeightLevel.Surface && preferredNavalHeight != HeightLevel.Submerged)
            preferredNavalHeight = HeightLevel.Submerged;

        for (int i = 0; i < embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon embarkedWeapon = embarkedWeapons[i];
            if (embarkedWeapon == null)
                continue;

            embarkedWeapon.SyncFromWeaponDefaultsIfNeeded();
        }

        for (int i = 0; i < supplierResources.Count; i++)
        {
            UnitEmbarkedSupply embarkedResource = supplierResources[i];
            if (embarkedResource == null)
                continue;
            embarkedResource.amount = Mathf.Max(0, embarkedResource.amount);
        }

        for (int i = 0; i < transportSlots.Count; i++)
        {
            UnitTransportSlotRule slot = transportSlots[i];
            if (slot == null)
                continue;

            slot.EnsureDefaults();
        }

    }

    public bool IsAircraft()
    {
        return unitClass == GameUnitClass.Jet ||
               unitClass == GameUnitClass.Plane ||
               unitClass == GameUnitClass.Helicopter;
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
