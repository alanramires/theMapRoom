using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

    [Tooltip("Prioridade de sobreposicao da estrutura. Maior valor vence em hex com conflito.")]
    public int priorityOrder = 0;

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
    [Tooltip("Unidades nesses dominios/alturas encerram movimento no dominio nativo do terreno quando estiverem neste par Estrutura+Terreno.")]
    public List<TerrainLayerMode> forceEndMovementOnTerrainDomainForDomains = new List<TerrainLayerMode>();

    [Header("Road Routes")]
    [Tooltip("Rotas de rodovia desta estrutura (centro-a-centro dos hexes).")]
    public List<RoadRouteDefinition> roadRoutes = new List<RoadRouteDefinition>();

    [Tooltip("Marca esta estrutura como trilho (Rail) para unidades com skill de linha de trem.")]
    public bool isRail = false;
    [Tooltip("Se true, bloqueia o uso de trilho por unidades com skill de linha de trem neste hex.")]
    public bool structureBlocksRail = false;

    private void OnValidate()
    {
        if (requiredSkillsToEnter == null)
            requiredSkillsToEnter = new List<SkillData>();
        if (blockedSkills == null)
            blockedSkills = new List<SkillData>();
        if (skillCostOverrides == null)
            skillCostOverrides = new List<TerrainSkillCostOverride>();
        if (aircraftOpsByTerrain == null)
            aircraftOpsByTerrain = new List<StructureAirOpsTerrainRule>();
        if (forceEndMovementOnTerrainDomainForDomains == null)
            forceEndMovementOnTerrainDomainForDomains = new List<TerrainLayerMode>();

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
