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
    [Tooltip("Custo basico de movimento/autonomia para entrar neste hex de construcao. Minimo 1.")]
    [Min(1)]
    public int baseMovementCost = 1;
    [Header("DPQ")]
    [Tooltip("Referencia de qualidade de posicao (DPQ) aplicada a esta construcao.")]
    public DPQData dpqData;
    [Tooltip("Se houver skills nesta lista, a unidade precisa ter pelo menos uma para entrar nesta construcao.")]
    public List<SkillData> requiredSkillsToEnter = new List<SkillData>();
    [Tooltip("Overrides opcionais de custo de autonomia por skill.")]
    public List<TerrainSkillCostOverride> skillCostOverrides = new List<TerrainSkillCostOverride>();

    [Header("Native Domain")]
    [Tooltip("Dominio/altura nativo da construcao.")]
    public Domain domain = Domain.Land;
    [Tooltip("Dominio/altura nativo da construcao.")]
    public HeightLevel heightLevel = HeightLevel.Surface;
    [FormerlySerializedAs("additionalLayerModes")]
    [Tooltip("Dominios/alturas adicionais permitidos pela construcao (ex.: porto permite Naval/Surface e Submarine/Submerged).")]
    public List<TerrainLayerMode> aditionalDomainsAllowed = new List<TerrainLayerMode>();
    [Tooltip("Se true, dominio do ar e sempre permitido para esta construcao.")]
    public bool alwaysAllowAirDomain = true;
    [Header("Aircraft Ops")]
    [Tooltip("Permite pouso de aeronaves neste tipo de construcao.")]
    public bool allowAircraftLanding = false;
    [Tooltip("Classes de unidade permitidas para pouso. Se vazio, aceita Jet/Plane/Helicopter.")]
    public List<GameUnitClass> landingAllowedClasses = new List<GameUnitClass>();
    [Tooltip("Skills exigidas para pouso. Se vazio, nao exige skill.")]
    public List<SkillData> landingRequiredSkills = new List<SkillData>();
    [Tooltip("Permite decolagem de aeronaves neste tipo de construcao.")]
    public bool allowAircraftTakeoff = false;
    [Tooltip("Modos de movimento permitidos para decolagem. Se vazio, permite MoveuParado e MoveuAndando.")]
    public List<SensorMovementMode> takeoffAllowedMovementModes = new List<SensorMovementMode>();

    [Header("Construction Supplier Settings")]
    public bool isSupplier = false;
    public SupplierTier supplierTier = SupplierTier.Hub;
    [Min(0)] public int maxUnitsServedPerTurn = 0;
    [Tooltip("OverlappingOnly por padrao. Use Adjacent1Hex para casos como porto atendendo navios atracados.")]
    public ConstructionSupplierRangeMode serviceRange = ConstructionSupplierRangeMode.OverlappingOnly;
    [Tooltip("OverlappingOnly por padrao.")]
    public ConstructionSupplierRangeMode collectionRange = ConstructionSupplierRangeMode.OverlappingOnly;

    [Header("Construction Supplier Operation Domain")]
    [Tooltip("Dominios/alturas onde esta construcao opera logistica.")]
    public List<TerrainLayerMode> supplierOperationDomains = new List<TerrainLayerMode>();

    [Header("Construction Services Provided")]
    [Tooltip("Servicos fornecidos por esta construcao.")]
    public List<ServiceData> supplierServicesProvided = new List<ServiceData>();

    [Header("Construction Resources")]
    [Tooltip("Supplies fornecidos por esta construcao com capacidade maxima.")]
    public List<ConstructionSupplierResourceCapacity> supplierResources = new List<ConstructionSupplierResourceCapacity>();

    [Header("Construction Configuration")]
    [FormerlySerializedAs("defaultSiteRuntime")]
    [Tooltip("Configuracao padrao de captura, producao e logistica desta construcao. Pode ser sobrescrita por ponto do mapa.")]
    public ConstructionSiteRuntime constructionConfiguration = new ConstructionSiteRuntime();

    private void OnValidate()
    {
        maxUnitsServedPerTurn = Mathf.Max(0, maxUnitsServedPerTurn);
        if (supplierOperationDomains == null)
            supplierOperationDomains = new List<TerrainLayerMode>();
        if (supplierServicesProvided == null)
            supplierServicesProvided = new List<ServiceData>();
        if (landingAllowedClasses == null)
            landingAllowedClasses = new List<GameUnitClass>();
        if (landingRequiredSkills == null)
            landingRequiredSkills = new List<SkillData>();
        if (takeoffAllowedMovementModes == null)
            takeoffAllowedMovementModes = new List<SensorMovementMode>();
        if (supplierResources == null)
            supplierResources = new List<ConstructionSupplierResourceCapacity>();
        for (int i = 0; i < supplierResources.Count; i++)
        {
            ConstructionSupplierResourceCapacity entry = supplierResources[i];
            if (entry == null)
                continue;
            entry.maxCapacity = Mathf.Max(0, entry.maxCapacity);
        }

        if (constructionConfiguration == null)
            constructionConfiguration = new ConstructionSiteRuntime();

        constructionConfiguration.Sanitize();
    }
}
