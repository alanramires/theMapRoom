using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

[System.Serializable]
public class ConstructionLandingSkillRule
{
    [Tooltip("Skill exigida para pouso/decolagem nesta construcao.")]
    public SkillData skill;

    [Tooltip("Modo de decolagem aplicado quando esta skill for a regra usada nesta construcao.")]
    public TakeoffProcedure takeoffMode = TakeoffProcedure.InstantToPreferredHeight;
}

[CreateAssetMenu(menuName = "Game/Construction/Construction Data", fileName = "ConstructionData_")]
public class ConstructionData : ScriptableObject
{
    private const int InfiniteSupplyOfferQuantity = int.MaxValue;

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

    [Header("Skill Rules")]
    [Tooltip("Se houver skills nesta lista, a unidade precisa ter pelo menos uma para entrar nesta construcao.")]
    public List<SkillData> requiredSkillsToEnter = new List<SkillData>();
    [Tooltip("Se a unidade possuir qualquer skill desta lista, entrada nesta construcao e bloqueada.")]
    public List<SkillData> blockedSkills = new List<SkillData>();
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
    [FormerlySerializedAs("allowAircraftLanding")]
    [FormerlySerializedAs("allowAircraftTakeoff")]
    [Tooltip("Permite pouso e decolagem de aeronaves neste tipo de construcao.")]
    public bool allowAircraftTakeoffAndLanding = false;
    [Tooltip("Se true, aeronaves pousadas nesta construcao pagam upkeep de autonomia na virada do turno.")]
    public bool aircraftUnitsPaysUpkeep = true;
    [FormerlySerializedAs("landingRequiredSkills")]
    [Tooltip("Campo legado de skills exigidas para pouso/decolagem (mantido para migracao).")]
    public List<SkillData> legacyRequiredLandingSkills = new List<SkillData>();
    [Tooltip("Skills exigidas para pouso/decolagem e seu modo de decolagem neste contexto.")]
    public List<ConstructionLandingSkillRule> requiredLandingSkillRules = new List<ConstructionLandingSkillRule>();
    [Tooltip("Se true, basta ter pelo menos 1 skill da lista para pousar/decolar nesta construcao. Se false, exige todas.")]
    public bool requireAtLeastOneLandingSkill = false;
    [Header("Naval Ops")]
    [Tooltip("Unidades nesses dominios/alturas encerram movimento no dominio nativo desta construcao.")]
    public List<TerrainLayerMode> forceEndMovementOnTerrainDomainForDomains = new List<TerrainLayerMode>();
    [Tooltip("Quando ligado, unidades nos dominios/alturas acima ficam livremente detectaveis nesta construcao.")]
    public bool forceDetectOnForcedEndMovementDomains = false;
    [Tooltip("Se preenchido, somente unidades com essas Stealth Skills ficam livremente detectaveis nesta construcao (nos dominios/alturas acima).")]
    public List<SkillData> forceDetectUnitsWithFollowingStealthSkills = new List<SkillData>();

    [Header("Construction Supplier Settings")]
    public bool isSupplier = false;
    public SupplierTier supplierTier = SupplierTier.Hub;
    [Min(0)] public int maxUnitsServedPerTurn = 0;
    [Tooltip("OverlappingOnly por padrao. Use Adjacent1Hex (so 1 hex) ou Hybrid0Or1Hex (mesmo hex + 1 hex).")]
    public ConstructionSupplierRangeMode serviceRange = ConstructionSupplierRangeMode.OverlappingOnly;
    [Tooltip("OverlappingOnly por padrao. Tambem suporta Adjacent1Hex e Hybrid0Or1Hex.")]
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
        if (supplierTier == SupplierTier.SelfSupplier)
            supplierTier = SupplierTier.Receiver;
        if (supplierOperationDomains == null)
            supplierOperationDomains = new List<TerrainLayerMode>();
        if (supplierServicesProvided == null)
            supplierServicesProvided = new List<ServiceData>();
        if (legacyRequiredLandingSkills == null)
            legacyRequiredLandingSkills = new List<SkillData>();
        if (requiredSkillsToEnter == null)
            requiredSkillsToEnter = new List<SkillData>();
        if (blockedSkills == null)
            blockedSkills = new List<SkillData>();
        if (skillCostOverrides == null)
            skillCostOverrides = new List<TerrainSkillCostOverride>();
        if (requiredLandingSkillRules == null)
            requiredLandingSkillRules = new List<ConstructionLandingSkillRule>();
        if (forceEndMovementOnTerrainDomainForDomains == null)
            forceEndMovementOnTerrainDomainForDomains = new List<TerrainLayerMode>();
        if (forceDetectUnitsWithFollowingStealthSkills == null)
            forceDetectUnitsWithFollowingStealthSkills = new List<SkillData>();

        for (int i = requiredLandingSkillRules.Count - 1; i >= 0; i--)
        {
            ConstructionLandingSkillRule entry = requiredLandingSkillRules[i];
            if (entry == null)
                requiredLandingSkillRules.RemoveAt(i);
            else if (!System.Enum.IsDefined(typeof(TakeoffProcedure), entry.takeoffMode))
                entry.takeoffMode = TakeoffProcedure.InstantToPreferredHeight;
        }

        if (requiredLandingSkillRules.Count == 0 && legacyRequiredLandingSkills.Count > 0)
        {
            for (int i = 0; i < legacyRequiredLandingSkills.Count; i++)
            {
                SkillData skill = legacyRequiredLandingSkills[i];
                if (skill == null)
                    continue;
                requiredLandingSkillRules.Add(new ConstructionLandingSkillRule
                {
                    skill = skill,
                    takeoffMode = TakeoffProcedure.InstantToPreferredHeight
                });
            }
        }
        if (supplierResources == null)
            supplierResources = new List<ConstructionSupplierResourceCapacity>();
        for (int i = 0; i < supplierResources.Count; i++)
        {
            ConstructionSupplierResourceCapacity entry = supplierResources[i];
            if (entry == null)
                continue;
            entry.Sanitize();
        }

        if (constructionConfiguration == null)
            constructionConfiguration = new ConstructionSiteRuntime();

        SyncSupplierSettingsToConstructionConfiguration();
        constructionConfiguration.Sanitize();
    }

    public void SyncSupplierSettingsToConstructionConfiguration()
    {
        if (constructionConfiguration == null)
            constructionConfiguration = new ConstructionSiteRuntime();

        if (!isSupplier)
            return;

        constructionConfiguration.canProvideSupplies = true;
        constructionConfiguration.offeredServices = BuildDistinctServiceList(supplierServicesProvided);
        constructionConfiguration.offeredSupplies = BuildSupplyOffersFromResources(supplierResources);
    }

    private static List<ServiceData> BuildDistinctServiceList(List<ServiceData> source)
    {
        List<ServiceData> result = new List<ServiceData>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            ServiceData service = source[i];
            if (service == null || result.Contains(service))
                continue;

            result.Add(service);
        }

        return result;
    }

    private static List<ConstructionSupplyOffer> BuildSupplyOffersFromResources(List<ConstructionSupplierResourceCapacity> source)
    {
        List<ConstructionSupplyOffer> result = new List<ConstructionSupplyOffer>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            ConstructionSupplierResourceCapacity entry = source[i];
            if (entry == null || entry.supply == null)
                continue;

            int quantity = entry.IsInfinite() ? InfiniteSupplyOfferQuantity : Mathf.Max(0, entry.maxCapacity);
            int existingIndex = FindSupplyOfferIndex(result, entry.supply);
            if (existingIndex >= 0)
            {
                ConstructionSupplyOffer existing = result[existingIndex];
                existing.quantity = Mathf.Max(existing.quantity, quantity);
                continue;
            }

            result.Add(new ConstructionSupplyOffer
            {
                supply = entry.supply,
                quantity = quantity
            });
        }

        return result;
    }

    private static int FindSupplyOfferIndex(List<ConstructionSupplyOffer> offers, SupplyData supply)
    {
        if (offers == null || supply == null)
            return -1;

        for (int i = 0; i < offers.Count; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer != null && offer.supply == supply)
                return i;
        }

        return -1;
    }
}
