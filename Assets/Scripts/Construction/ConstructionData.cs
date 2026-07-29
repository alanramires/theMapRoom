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

[System.Serializable]
public class ConstructionSkillTerrainRule
{
    [Tooltip("Terreno base desta regra em par com a construcao.")]
    public TerrainTypeData terrainData;

    [Tooltip("Se houver skills nesta lista, a unidade precisa ter pelo menos uma para entrar nesta construcao neste terreno.")]
    public List<SkillData> requiredSkillsToEnter = new List<SkillData>();

    [Tooltip("Skills bloqueadas adicionalmente neste par Construcao+Terreno. Bloqueios globais da construcao continuam valendo.")]
    public List<SkillData> blockedSkills = new List<SkillData>();

    [Tooltip("Overrides opcionais de custo de autonomia por skill neste par Construcao+Terreno.")]
    public List<TerrainSkillCostOverride> skillCostOverrides =
        new List<TerrainSkillCostOverride>();
}

public enum ConstructionGrammaticalGender
{
    Masculine = 0,
    Feminine = 1
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

    [Tooltip("Sigla curta do tipo (ex.: HQ, Fáb, Aero, Porto). Usada em logs/depuração como o painel de Jogadas.")]
    public string sufixo;

    [TextArea]
    public string description;

    [Tooltip("Genero gramatical usado nas mensagens: 'um' para masculino e 'uma' para feminino.")]
    public ConstructionGrammaticalGender grammaticalGender = ConstructionGrammaticalGender.Masculine;

    [Header("Construction Requirements")]
    [Tooltip("Construcao que a equipe precisa ter capturado ao menos uma vez antes de poder construir esta.")]
    public ConstructionData requiredBuilding;

    [Header("Facility Type")]
    [Tooltip("Marca esta construcao como aeroporto real para prioridades de IA e regras aereas.")]
    public bool isAirport = false;
    [Tooltip("Marca esta construcao como porto real para prioridades de IA e regras navais.")]
    public bool isHarbor = false;
    [Tooltip("Cidade mantem renda integral para a IA mesmo no modo Easy.")]
    public bool isCity = false;
    [Tooltip("Marca esta construcao como terminal de transporte, sem aplicar regras economicas de cidade.")]
    public bool isTransportTerminal = false;

    [Header("Visuals")]
    [FormerlySerializedAs("sprite")]
    public Sprite spriteDefault;
    public Sprite spriteGreen;
    public Sprite spriteRed;
    public Sprite spriteBlue;
    public Sprite spriteYellow;

    [Header("Attributes")]
    [Min(0)]
    [Tooltip("Alcance de visao da construcao. Zero permite observar apenas alvos no proprio hex.")]
    public int visao = 0;
    [Tooltip("Custo basico de movimento/autonomia para entrar neste hex de construcao. Minimo 1.")]
    [Min(1)]
    public int baseMovementCost = 1;
    [Tooltip("Se true, esta construcao e um edificio falso (decoy) e nao deve ser tratada como alvo real pela IA ou sistemas de captura.")]
    public bool isFakeBuilding = false;
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
    [Tooltip("Regras opcionais por par Construcao+Terreno. Requisitos e custos nao vazios substituem o campo global correspondente; listas vazias herdam o global. Bloqueios do par se somam aos bloqueios globais.")]
    public List<ConstructionSkillTerrainRule> skillRulesByTerrain =
        new List<ConstructionSkillTerrainRule>();
    [Tooltip("Nestes terrenos, uma estrutura de rota conectada sob a construcao assume integralmente suas proprias regras e custo de movimento. Sem aresta estrutural conectada, continuam valendo as regras da construcao neste terreno.")]
    public List<TerrainTypeData> inheritStructureRulesOnlyOn =
        new List<TerrainTypeData>();
    [Tooltip("Nestes terrenos, quando nenhuma estrutura conectada estiver sendo herdada, o terreno assume integralmente suas proprias regras e custo de movimento. A construcao continua existindo para captura, producao e demais sistemas.")]
    public List<TerrainTypeData> inheritTerrainRulesOnlyOn =
        new List<TerrainTypeData>();

    [System.NonSerialized]
    private Dictionary<TerrainTypeData, List<SkillData>>
        combinedBlockedSkillsByTerrain;

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
    // Sem [Header("Aircraft Ops")]: o agrupamento e o foldout homonimo em ConstructionDataEditor.
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
    // Sem [Header("Naval Ops")]: o agrupamento e o foldout homonimo em ConstructionDataEditor.
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
    [Tooltip("Classificacao automatica: None sem servicos; StockTransfer somente Transfer; FieldService quando oferece qualquer outro servico.")]
    public SupplierServiceProfile supplierServiceProfile = SupplierServiceProfile.None;

    [Header("Construction Resources")]
    [Tooltip("Supplies fornecidos por esta construcao. Max Capacity define a reserva de referencia da IA; a construcao continua podendo armazenar acima dela.")]
    public List<ConstructionSupplierResourceCapacity> supplierResources = new List<ConstructionSupplierResourceCapacity>();
    [Range(0, 100)]
    [Tooltip("IA repoe o recurso da construcao quando ele cai para este percentual da reserva configurada. Zero so pede quando zerar. Padrao: 25%.")]
    public int aiStockRestockTriggerPercent = 25;

    [Header("Rebel AI")]
    [Tooltip("A faccao sem QG (rebelde) NUNCA produz — nem no que captura — porque sua doutrina e negacao territorial, nao expansao produtiva. Esta flag e a excecao renegada: um predio marcado permite que o rebelde que o TOMAR compre unidades aqui, ignorando as regras de dono (OriginalOwner/FirstOwner) — afinal um insurgente jamais e o dono original do que conquista. So sellingRule=Disabled ainda barra. Default false: marque apenas os poucos predios que voce quer que abasteçam a insurgencia.")]
    public bool allowRebelAIPurchase = false;

    [Header("Construction Configuration")]
    [FormerlySerializedAs("defaultSiteRuntime")]
    [Tooltip("Configuracao padrao de captura, producao e logistica desta construcao. Pode ser sobrescrita por ponto do mapa.")]
    public ConstructionSiteRuntime constructionConfiguration = new ConstructionSiteRuntime();

    private void OnValidate()
    {
        combinedBlockedSkillsByTerrain = null;

        visao = Mathf.Max(0, visao);
        maxUnitsServedPerTurn = Mathf.Max(0, maxUnitsServedPerTurn);
        aiStockRestockTriggerPercent = Mathf.Clamp(
            aiStockRestockTriggerPercent, 0, 100);
        if (supplierTier == SupplierTier.SelfSupplier)
            supplierTier = SupplierTier.Receiver;
        if (supplierOperationDomains == null)
            supplierOperationDomains = new List<TerrainLayerMode>();
        if (supplierServicesProvided == null)
            supplierServicesProvided = new List<ServiceData>();
        supplierServiceProfile =
            ServiceData.ResolveSupplierServiceProfile(supplierServicesProvided);
        if (legacyRequiredLandingSkills == null)
            legacyRequiredLandingSkills = new List<SkillData>();
        if (requiredSkillsToEnter == null)
            requiredSkillsToEnter = new List<SkillData>();
        if (blockedSkills == null)
            blockedSkills = new List<SkillData>();
        if (skillCostOverrides == null)
            skillCostOverrides = new List<TerrainSkillCostOverride>();
        if (skillRulesByTerrain == null)
            skillRulesByTerrain = new List<ConstructionSkillTerrainRule>();
        if (inheritStructureRulesOnlyOn == null)
            inheritStructureRulesOnlyOn = new List<TerrainTypeData>();
        if (inheritTerrainRulesOnlyOn == null)
            inheritTerrainRulesOnlyOn = new List<TerrainTypeData>();
        if (requiredLandingSkillRules == null)
            requiredLandingSkillRules = new List<ConstructionLandingSkillRule>();
        if (forceEndMovementOnTerrainDomainForDomains == null)
            forceEndMovementOnTerrainDomainForDomains = new List<TerrainLayerMode>();
        if (forceDetectUnitsWithFollowingStealthSkills == null)
            forceDetectUnitsWithFollowingStealthSkills = new List<SkillData>();

        for (int i = skillRulesByTerrain.Count - 1; i >= 0; i--)
        {
            ConstructionSkillTerrainRule rule = skillRulesByTerrain[i];
            if (rule == null)
            {
                skillRulesByTerrain.RemoveAt(i);
                continue;
            }

            if (rule.requiredSkillsToEnter == null)
                rule.requiredSkillsToEnter = new List<SkillData>();
            if (rule.blockedSkills == null)
                rule.blockedSkills = new List<SkillData>();
            if (rule.skillCostOverrides == null)
            {
                rule.skillCostOverrides =
                    new List<TerrainSkillCostOverride>();
            }
        }

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

    public IReadOnlyList<SkillData> GetRequiredSkillsToEnter(
        TerrainTypeData terrain)
    {
        if (TryGetSkillRuleForTerrain(
                terrain,
                out ConstructionSkillTerrainRule rule)
            && rule.requiredSkillsToEnter != null
            && rule.requiredSkillsToEnter.Count > 0)
        {
            return rule.requiredSkillsToEnter;
        }

        return requiredSkillsToEnter != null
            ? requiredSkillsToEnter
            : System.Array.Empty<SkillData>();
    }

    public IReadOnlyList<SkillData> GetBlockedSkillsToEnter(
        TerrainTypeData terrain)
    {
        IReadOnlyList<SkillData> globalBlocked =
            blockedSkills != null
                ? blockedSkills
                : System.Array.Empty<SkillData>();
        TryGetSkillRuleForTerrain(
            terrain,
            out ConstructionSkillTerrainRule rule);
        IReadOnlyList<SkillData> terrainBlocked =
            rule != null && rule.blockedSkills != null
                ? rule.blockedSkills
                : System.Array.Empty<SkillData>();
        if (terrainBlocked.Count == 0)
        {
            return globalBlocked;
        }

        combinedBlockedSkillsByTerrain ??=
            new Dictionary<TerrainTypeData, List<SkillData>>();
        if (terrain != null
            && combinedBlockedSkillsByTerrain.TryGetValue(
                terrain,
                out List<SkillData> cached))
        {
            return cached;
        }

        var combined = new List<SkillData>(
            globalBlocked.Count
            + terrainBlocked.Count);
        AddDistinctSkills(combined, globalBlocked);
        AddDistinctSkills(combined, terrainBlocked);

        if (terrain != null)
            combinedBlockedSkillsByTerrain[terrain] = combined;
        return combined;
    }

    public IReadOnlyList<TerrainSkillCostOverride> GetSkillCostOverrides(
        TerrainTypeData terrain)
    {
        if (TryGetSkillRuleForTerrain(
                terrain,
                out ConstructionSkillTerrainRule rule)
            && rule.skillCostOverrides != null
            && rule.skillCostOverrides.Count > 0)
        {
            return rule.skillCostOverrides;
        }

        return skillCostOverrides != null
            ? skillCostOverrides
            : System.Array.Empty<TerrainSkillCostOverride>();
    }

    public bool TryGetSkillRuleForTerrain(
        TerrainTypeData terrain,
        out ConstructionSkillTerrainRule rule)
    {
        rule = null;
        if (terrain == null
            || skillRulesByTerrain == null
            || skillRulesByTerrain.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < skillRulesByTerrain.Count; i++)
        {
            ConstructionSkillTerrainRule candidate =
                skillRulesByTerrain[i];
            if (candidate != null
                && candidate.terrainData == terrain)
            {
                rule = candidate;
                return true;
            }
        }

        return false;
    }

    public bool InheritsStructureRulesOn(TerrainTypeData terrain)
    {
        if (terrain == null
            || inheritStructureRulesOnlyOn == null)
            return false;

        for (int i = 0; i < inheritStructureRulesOnlyOn.Count; i++)
        {
            if (inheritStructureRulesOnlyOn[i] == terrain)
                return true;
        }

        return false;
    }

    public bool InheritsTerrainRulesOn(TerrainTypeData terrain)
    {
        if (terrain == null
            || inheritTerrainRulesOnlyOn == null)
        {
            return false;
        }

        for (int i = 0; i < inheritTerrainRulesOnlyOn.Count; i++)
        {
            if (inheritTerrainRulesOnlyOn[i] == terrain)
                return true;
        }

        return false;
    }

    private static void AddDistinctSkills(
        List<SkillData> destination,
        IReadOnlyList<SkillData> source)
    {
        if (destination == null || source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            SkillData skill = source[i];
            if (skill != null && !destination.Contains(skill))
                destination.Add(skill);
        }
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
                existing.peakQuantity = Mathf.Max(existing.peakQuantity, existing.quantity);
                continue;
            }

            result.Add(new ConstructionSupplyOffer
            {
                supply = entry.supply,
                quantity = quantity,
                // Teto dinamico nasce no estoque inicial da partida.
                peakQuantity = quantity
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
