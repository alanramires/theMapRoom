using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Familia topologica de uma estrutura de rota.
///
/// Estruturas diferentes da mesma familia podem formar uma rota continua
/// quando suas definicoes compartilham um no. Ex.: Trilho e Ponte
/// Ferroviaria pertencem ambos a MalhaFerroviaria.
/// </summary>
public enum RouteNetworkType
{
    None = 0,
    Asfaltado = 1,
    MalhaFerroviaria = 2,
    Fluvial = 3
}

/// <summary>
/// Override tri-state de Road Boost para um par Estrutura+Terreno.
/// Herdar usa o valor global da estrutura; Ativar e Desativar vencem o global.
/// </summary>
public enum RoadBoostOverride
{
    HerdarDaEstrutura = 0,
    Ativar = 1,
    Desativar = 2
}

[System.Serializable]
public class StructureLandingSkillRule
{
    [Tooltip("Skill exigida para pouso/decolagem neste par Estrutura+Terreno.")]
    public SkillData skill;

    [Tooltip("Modo de decolagem aplicado quando esta skill for a regra usada neste par.")]
    public TakeoffProcedure takeoffMode = TakeoffProcedure.InstantToPreferredHeight;
}

[System.Serializable]
public class StructureAirOpsTerrainRule
{
    [Tooltip("Terreno base desta regra em par com a estrutura.")]
    public TerrainTypeData terrainData;

    [FormerlySerializedAs("isRoadRunway")]
    [Tooltip("Se true, este par Estrutura+Terreno permite pouso e decolagem.")]
    public bool allowTakeoffAndLanding = false;

    [FormerlySerializedAs("landingRequiredSkills")]
    [HideInInspector]
    [Tooltip("Campo legado de skills exigidas para pouso/decolagem neste par (mantido para migracao).")]
    public List<SkillData> legacyRequiredLandingSkills = new List<SkillData>();
    [Tooltip("Skills exigidas para pouso/decolagem e seu modo de decolagem neste par.")]
    public List<StructureLandingSkillRule> requiredLandingSkillRules = new List<StructureLandingSkillRule>();

    [Tooltip("Se true, basta ter pelo menos 1 skill da lista para pousar/decolar neste par. Se false, exige todas.")]
    public bool requireAtLeastOneLandingSkill = false;
}

[System.Serializable]
public class StructureSkillTerrainRule
{
    [Tooltip("Terreno base desta regra em par com a estrutura.")]
    public TerrainTypeData terrainData;

    [Tooltip("Se houver skills nesta lista, a unidade precisa ter pelo menos uma para entrar nesta estrutura neste terreno.")]
    public List<SkillData> requiredSkillsToEnter = new List<SkillData>();
    [Tooltip("Se a unidade possuir qualquer skill desta lista, entrada nesta estrutura neste terreno e bloqueada.")]
    public List<SkillData> blockedSkills = new List<SkillData>();
    [Tooltip("Overrides opcionais de custo de autonomia por skill neste par Estrutura+Terreno.")]
    public List<TerrainSkillCostOverride> skillCostOverrides = new List<TerrainSkillCostOverride>();

    [Tooltip("Exigencia de rota declarada NESTE par Estrutura+Terreno. Herdar = usa o valor global da estrutura. Ex.: Rodovia e livre na floresta (global false) mas canalizada na montanha (override Exigir).")]
    public ExigenciaDeRotaDeclarada rotaDeclarada = ExigenciaDeRotaDeclarada.HerdarDaEstrutura;

    [Tooltip("Road Boost NESTE par Estrutura+Terreno. Herdar usa o valor global da estrutura; Ativar ou Desativar sobrescrevem o global.")]
    public RoadBoostOverride roadBoost = RoadBoostOverride.HerdarDaEstrutura;
}

// Exigencia de conexao por rota declarada. Quando ativa, uma unidade so entra na celula
// vindo de um hex que seja o par consecutivo dela em alguma RoadRouteDefinition — nao basta
// a estrutura estar pintada no destino. Modela trilho (trem segue a linha) e estrada de
// montanha (so se sobe a serra pela boca do desfiladeiro, nao por qualquer flanco).
public enum ExigenciaDeRotaDeclarada
{
    HerdarDaEstrutura = 0,
    Exigir = 1,
    NaoExigir = 2
}

[System.Serializable]
public class StructureNavalOpsTerrainRule
{
    [Tooltip("Terreno base que completa o par Estrutura+Terreno.")]
    public TerrainTypeData terrainData;

    [Tooltip("Unidades nestes dominios/alturas encerram o movimento e emergem neste par.")]
    public List<TerrainLayerMode> forceEndMovementOnTerrainDomainForDomains = new List<TerrainLayerMode>();

    [Tooltip("Quando ligado, unidades afetadas ficam livremente detectaveis neste par.")]
    public bool forceDetectOnForcedEndMovementDomains = false;

    [Tooltip("Se preenchido, somente unidades com estas Stealth Skills ficam livremente detectaveis neste par.")]
    public List<SkillData> forceDetectUnitsWithFollowingStealthSkills = new List<SkillData>();

    [Tooltip("O conves fica ACIMA da agua NESTE par: Land/Surface e Naval/Surface deixam de ser o mesmo andar. Marque na ponte sobre MAR — tanque para em cima, navio passa embaixo. Deixe desmarcado na ponte sobre PRAIA: la a ponte encosta no chao e nao ha vao, entao navio e tanque continuam disputando a mesma vaga.")]
    public bool separaConvesEAgua = false;

    [Tooltip("Proibe unidades navais de superficie NESTE par, mesmo que a estrutura e o terreno as aceitem isoladamente. Marque em Ponte + Praia: ali fica a cabeceira da ponte (encontro, aterro, estacas), nao agua navegavel. Deixe desmarcado em Ponte + Mar, onde ha vao e o navio passa embaixo.")]
    public bool bloqueiaNaval = false;
}

[System.Serializable]
public class StructureTerrainDescription
{
    [Tooltip("Terreno base deste par Estrutura+Terreno.")]
    public TerrainTypeData terrainData;

    [TextArea]
    [Tooltip("Descricao exibida para esta estrutura quando estiver sobre este terreno.")]
    public string description;

    [HideInInspector]
    [Tooltip("LEGADO: use Road Boost em Skill Rules By Terrain.")]
    public bool roadBoostOff;
}

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

    [Tooltip("Descricoes especificas por par Estrutura+Terreno. Se nao houver par correspondente, usa Description.")]
    public List<StructureTerrainDescription> descriptionsByTerrain = new List<StructureTerrainDescription>();

    [Tooltip("Prioridade de sobreposicao da estrutura. Maior valor vence em hex com conflito.")]
    public int priorityOrder = 0;

    [Header("Route Network")]
    [Tooltip("Familia topologica desta rota. Estruturas da mesma familia podem se conectar por um no compartilhado, como Trilho + Ponte Ferroviaria.")]
    public RouteNetworkType routeNetworkType = RouteNetworkType.None;

    [Header("Native Domain / Can be build on")]
    [Tooltip("Dominio/altura nativo da estrutura.")]
    public Domain domain = Domain.Land;
    [Tooltip("Dominio/altura nativo da estrutura.")]
    public HeightLevel heightLevel = HeightLevel.Surface;
    [FormerlySerializedAs("additionalLayerModes")]
    [Tooltip("Dominios/alturas adicionais permitidos pela estrutura.")]
    public List<TerrainLayerMode> aditionalDomainsAllowed = new List<TerrainLayerMode>();
    [Tooltip("Se true, dominio do ar e sempre permitido nesta estrutura.")]
    public bool alwaysAllowAirDomain = false;
    [Tooltip("Custo basico de movimento/autonomia para entrar neste hex de estrutura. Minimo 1.")]
    [Min(1)]
    public int baseMovementCost = 1;

    [Header("DPQ")]
    [Tooltip("Referencia de qualidade de posicao (DPQ) aplicada a esta estrutura.")]
    public DPQData dpqData;

    [Header("Skill Rules")]
    [Tooltip("Se houver skills nesta lista, a unidade precisa ter pelo menos uma para entrar nesta estrutura.")]
    public List<SkillData> requiredSkillsToEnter = new List<SkillData>();
    [Tooltip("Se a unidade possuir qualquer skill desta lista, entrada nesta estrutura e bloqueada.")]
    public List<SkillData> blockedSkills = new List<SkillData>();
    [Tooltip("Overrides opcionais de custo de autonomia por skill.")]
    public List<TerrainSkillCostOverride> skillCostOverrides = new List<TerrainSkillCostOverride>();
    [Tooltip("Regras por par Estrutura+Terreno. Requisitos e custos nao vazios substituem o campo global correspondente; listas vazias herdam o global. Bloqueios do par se somam aos bloqueios globais.")]
    public List<StructureSkillTerrainRule> skillRulesByTerrain = new List<StructureSkillTerrainRule>();
    [Tooltip("Valor GLOBAL da exigencia de rota declarada. Marque na Linha de Trem para cobrir todos os terrenos de uma vez. Cada par Estrutura+Terreno pode sobrescrever este valor.")]
    public bool exigeRotaDeclarada = false;
    [Tooltip("A exigencia de rota sobrevive a uma construcao no hex. Marque na Linha de Trem: o trem so entra numa cidade se houver trilho ligando ate ela. Desmarque na Rodovia: uma cidade na montanha liberta o movimento e encerra o desfiladeiro.")]
    public bool exigeEstruturaNaConstrucao = false;

    [Header("Build Rules")]
    [FormerlySerializedAs("additionalBuildLayerModes")]
    [Tooltip("Camadas adicionais onde esta estrutura pode ser construida. Se vazio, usa apenas o dominio/altura nativos.")]
    public List<TerrainLayerMode> canAlsoBeBuiltOnTheFollowDomains = new List<TerrainLayerMode>();

    [Header("Road Visual")]
    [Tooltip("Sprite do segmento da rota para esta estrutura (ex.: estrada, ponte). Se null, usa o default do RoadNetworkManager.")]
    public Sprite roadSegmentSprite;
    [Tooltip("Cor da rota desta estrutura.")]
    public Color roadColor = Color.white;
    [Tooltip("Largura da rota desta estrutura.")]
    [Range(0.03f, 0.6f)]
    public float roadWidth = 0.16f;
    [Tooltip("Sobreposicao entre segmentos para evitar gaps visuais.")]
    [Range(0f, 0.3f)]
    public float segmentOverlap = 0.02f;
    [Tooltip("Se true, esta estrutura habilita bonus de deslocamento em full move (ex.: estrada).")]
    public bool roadBoost = false;

    [Header("Aircraft Ops (Structure + Terrain Pair)")]
    [Tooltip("Mapa de pares Estrutura+Terreno para air ops. Cada elemento define se o par atua como RoadRunway e skills exigidas.")]
    public List<StructureAirOpsTerrainRule> aircraftOpsByTerrain = new List<StructureAirOpsTerrainRule>();
    [Header("Naval Ops")]
    [Tooltip("Regras navais especificas por par Estrutura+Terreno.")]
    public List<StructureNavalOpsTerrainRule> navalOpsByTerrain = new List<StructureNavalOpsTerrainRule>();
    [Tooltip("LEGADO: regra global da estrutura. Prefira Naval Ops By Terrain.")]
    public List<TerrainLayerMode> forceEndMovementOnTerrainDomainForDomains = new List<TerrainLayerMode>();
    [Tooltip("Quando ligado, unidades nos dominios/alturas acima ficam livremente detectaveis neste par Estrutura+Terreno.")]
    public bool forceDetectOnForcedEndMovementDomains = false;
    [Tooltip("Se preenchido, somente unidades com essas Stealth Skills ficam livremente detectaveis neste par Estrutura+Terreno (nos dominios/alturas acima).")]
    public List<SkillData> forceDetectUnitsWithFollowingStealthSkills = new List<SkillData>();

    [System.NonSerialized]
    private Dictionary<TerrainTypeData, List<SkillData>>
        combinedBlockedSkillsByTerrain;

    // NAO existe roadRoutes aqui, de proposito. Existia — e era layout no TIPO
    // COMPARTILHADO: o asset "Rodovias", que diz o que uma rodovia E, carregava 11
    // tracados concretos. Toda cena que usasse o tipo herdava os tracados de
    // outro mapa, e isso e a contaminacao mais global que o projeto tinha.
    //
    // Layout de estrada mora na CENA (RoadNetworkManager) e, no modelo de
    // campanha, no bake do quadrante.

    private void OnValidate()
    {
        combinedBlockedSkillsByTerrain = null;

        if (requiredSkillsToEnter == null)
            requiredSkillsToEnter = new List<SkillData>();
        if (descriptionsByTerrain == null)
            descriptionsByTerrain = new List<StructureTerrainDescription>();
        if (blockedSkills == null)
            blockedSkills = new List<SkillData>();
        if (skillCostOverrides == null)
            skillCostOverrides = new List<TerrainSkillCostOverride>();
        if (skillRulesByTerrain == null)
            skillRulesByTerrain = new List<StructureSkillTerrainRule>();
        if (aircraftOpsByTerrain == null)
            aircraftOpsByTerrain = new List<StructureAirOpsTerrainRule>();
        if (forceEndMovementOnTerrainDomainForDomains == null)
            forceEndMovementOnTerrainDomainForDomains = new List<TerrainLayerMode>();
        if (navalOpsByTerrain == null)
            navalOpsByTerrain = new List<StructureNavalOpsTerrainRule>();
        if (forceDetectUnitsWithFollowingStealthSkills == null)
            forceDetectUnitsWithFollowingStealthSkills = new List<SkillData>();

        for (int i = navalOpsByTerrain.Count - 1; i >= 0; i--)
        {
            StructureNavalOpsTerrainRule rule = navalOpsByTerrain[i];
            if (rule == null)
            {
                navalOpsByTerrain.RemoveAt(i);
                continue;
            }

            if (rule.forceEndMovementOnTerrainDomainForDomains == null)
                rule.forceEndMovementOnTerrainDomainForDomains = new List<TerrainLayerMode>();
            if (rule.forceDetectUnitsWithFollowingStealthSkills == null)
                rule.forceDetectUnitsWithFollowingStealthSkills = new List<SkillData>();
        }

        for (int i = 0; i < aircraftOpsByTerrain.Count; i++)
        {
            StructureAirOpsTerrainRule pairRule = aircraftOpsByTerrain[i];
            if (pairRule == null)
                continue;

            if (pairRule.legacyRequiredLandingSkills == null)
                pairRule.legacyRequiredLandingSkills = new List<SkillData>();
            if (pairRule.requiredLandingSkillRules == null)
                pairRule.requiredLandingSkillRules = new List<StructureLandingSkillRule>();

            for (int j = pairRule.requiredLandingSkillRules.Count - 1; j >= 0; j--)
            {
                StructureLandingSkillRule entry = pairRule.requiredLandingSkillRules[j];
                if (entry == null)
                    pairRule.requiredLandingSkillRules.RemoveAt(j);
                else if (!System.Enum.IsDefined(typeof(TakeoffProcedure), entry.takeoffMode))
                    entry.takeoffMode = TakeoffProcedure.InstantToPreferredHeight;
            }

            if (pairRule.requiredLandingSkillRules.Count == 0 && pairRule.legacyRequiredLandingSkills.Count > 0)
            {
                for (int j = 0; j < pairRule.legacyRequiredLandingSkills.Count; j++)
                {
                    SkillData skill = pairRule.legacyRequiredLandingSkills[j];
                    if (skill == null)
                        continue;
                    pairRule.requiredLandingSkillRules.Add(new StructureLandingSkillRule
                    {
                        skill = skill,
                        takeoffMode = TakeoffProcedure.InstantToPreferredHeight
                    });
                }
            }
        }

        for (int i = 0; i < skillRulesByTerrain.Count; i++)
        {
            StructureSkillTerrainRule rule = skillRulesByTerrain[i];
            if (rule == null)
                continue;

            if (rule.requiredSkillsToEnter == null)
                rule.requiredSkillsToEnter = new List<SkillData>();
            if (rule.blockedSkills == null)
                rule.blockedSkills = new List<SkillData>();
            if (rule.skillCostOverrides == null)
                rule.skillCostOverrides = new List<TerrainSkillCostOverride>();
            if (!System.Enum.IsDefined(typeof(RoadBoostOverride), rule.roadBoost))
                rule.roadBoost = RoadBoostOverride.HerdarDaEstrutura;
        }
    }

    // Veto do par Estrutura+Terreno sobre uma camada. Roda antes de qualquer concessao:
    // nem o dominio nativo da estrutura, nem o adicional, nem o terreno base valem se o
    // par proibiu. E como a ponte na praia recusa navio sem deixar de aceita-lo no mar.
    public bool IsLayerBlockedAt(TerrainTypeData terrain, Domain domain, HeightLevel height)
    {
        if (domain != Domain.Naval || height != HeightLevel.Surface)
            return false;

        return TryGetNavalOpsRuleForTerrain(terrain, out StructureNavalOpsTerrainRule rule)
            && rule != null
            && rule.bloqueiaNaval;
    }

    public bool TryGetNavalOpsRuleForTerrain(TerrainTypeData terrain, out StructureNavalOpsTerrainRule rule)
    {
        rule = null;
        if (terrain == null || navalOpsByTerrain == null)
            return false;

        for (int i = 0; i < navalOpsByTerrain.Count; i++)
        {
            StructureNavalOpsTerrainRule candidate = navalOpsByTerrain[i];
            if (candidate != null && candidate.terrainData == terrain)
            {
                rule = candidate;
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<SkillData> GetRequiredSkillsToEnter(TerrainTypeData terrain)
    {
        if (TryGetSkillRuleForTerrain(
                terrain,
                out StructureSkillTerrainRule rule)
            && rule.requiredSkillsToEnter != null
            && rule.requiredSkillsToEnter.Count > 0)
        {
            return rule.requiredSkillsToEnter;
        }

        return requiredSkillsToEnter != null
            ? requiredSkillsToEnter
            : System.Array.Empty<SkillData>();
    }

    public string GetDescription(TerrainTypeData terrain)
    {
        if (terrain != null && descriptionsByTerrain != null)
        {
            for (int i = 0; i < descriptionsByTerrain.Count; i++)
            {
                StructureTerrainDescription pair = descriptionsByTerrain[i];
                if (pair != null && pair.terrainData == terrain && !string.IsNullOrWhiteSpace(pair.description))
                    return pair.description;
            }
        }

        return description ?? string.Empty;
    }

    public bool IsRoadBoostEnabled(TerrainTypeData terrain)
    {
        if (TryGetSkillRuleForTerrain(
                terrain,
                out StructureSkillTerrainRule rule)
            && rule != null)
        {
            if (rule.roadBoost == RoadBoostOverride.Ativar)
                return true;
            if (rule.roadBoost == RoadBoostOverride.Desativar)
                return false;
        }

        // Compatibilidade com assets anteriores ao override tri-state. O campo
        // legado fica oculto e so e consultado quando a nova regra esta em Herdar.
        if (terrain != null && descriptionsByTerrain != null)
        {
            for (int i = 0; i < descriptionsByTerrain.Count; i++)
            {
                StructureTerrainDescription pair = descriptionsByTerrain[i];
                if (pair != null && pair.terrainData == terrain && pair.roadBoostOff)
                    return false;
            }
        }

        return roadBoost;
    }

    public IReadOnlyList<SkillData> GetBlockedSkillsToEnter(TerrainTypeData terrain)
    {
        IReadOnlyList<SkillData> globalBlocked =
            blockedSkills != null
                ? blockedSkills
                : System.Array.Empty<SkillData>();
        if (!TryGetSkillRuleForTerrain(
                terrain,
                out StructureSkillTerrainRule rule)
            || rule.blockedSkills == null
            || rule.blockedSkills.Count == 0)
        {
            return globalBlocked;
        }

        if (globalBlocked.Count == 0)
            return rule.blockedSkills;

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
            globalBlocked.Count + rule.blockedSkills.Count);
        for (int i = 0; i < globalBlocked.Count; i++)
        {
            SkillData skill = globalBlocked[i];
            if (skill != null && !combined.Contains(skill))
                combined.Add(skill);
        }
        for (int i = 0; i < rule.blockedSkills.Count; i++)
        {
            SkillData skill = rule.blockedSkills[i];
            if (skill != null && !combined.Contains(skill))
                combined.Add(skill);
        }

        if (terrain != null)
            combinedBlockedSkillsByTerrain[terrain] = combined;
        return combined;
    }

    public IReadOnlyList<TerrainSkillCostOverride> GetSkillCostOverrides(TerrainTypeData terrain)
    {
        if (TryGetSkillRuleForTerrain(
                terrain,
                out StructureSkillTerrainRule rule)
            && rule.skillCostOverrides != null
            && rule.skillCostOverrides.Count > 0)
        {
            return rule.skillCostOverrides;
        }

        return skillCostOverrides != null
            ? skillCostOverrides
            : System.Array.Empty<TerrainSkillCostOverride>();
    }

    // Exigencia de rota declarada para este par Estrutura+Terreno. O par manda; se ele
    // herdar, vale o valor global da estrutura. Ex.: Trilhos marca o global e cobre todos
    // os terrenos; Rodovias fica global=false e sobrescreve so no par com Montanha.
    public bool ExigeRotaDeclaradaEm(TerrainTypeData terrain)
    {
        if (TryGetSkillRuleForTerrain(terrain, out StructureSkillTerrainRule rule) && rule != null)
        {
            switch (rule.rotaDeclarada)
            {
                case ExigenciaDeRotaDeclarada.Exigir:
                    return true;
                case ExigenciaDeRotaDeclarada.NaoExigir:
                    return false;
            }
        }

        return exigeRotaDeclarada;
    }

    public bool TryGetSkillRuleForTerrain(TerrainTypeData terrain, out StructureSkillTerrainRule rule)
    {
        rule = null;
        if (terrain == null || skillRulesByTerrain == null || skillRulesByTerrain.Count == 0)
            return false;

        for (int i = 0; i < skillRulesByTerrain.Count; i++)
        {
            StructureSkillTerrainRule candidate = skillRulesByTerrain[i];
            if (candidate == null || candidate.terrainData == null)
                continue;

            if (candidate.terrainData == terrain)
            {
                rule = candidate;
                return true;
            }
        }

        return false;
    }

    public bool SupportsBuildOn(Domain domainToBuildOn, HeightLevel heightLevelToBuildOn)
    {
        if (alwaysAllowAirDomain && domainToBuildOn == Domain.Air)
            return true;

        if (domain == domainToBuildOn && heightLevel == heightLevelToBuildOn)
            return true;

        if (canAlsoBeBuiltOnTheFollowDomains == null || canAlsoBeBuiltOnTheFollowDomains.Count == 0)
            return false;

        for (int i = 0; i < canAlsoBeBuiltOnTheFollowDomains.Count; i++)
        {
            TerrainLayerMode mode = canAlsoBeBuiltOnTheFollowDomains[i];
            if (mode.domain == domainToBuildOn && mode.heightLevel == heightLevelToBuildOn)
                return true;
        }

        return false;
    }
}
