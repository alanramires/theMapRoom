using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
    [Tooltip("Se houver skills nesta lista, a unidade precisa ter pelo menos uma para entrar nesta estrutura.")]
    public List<SkillData> requiredSkillsToEnter = new List<SkillData>();
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

    [Header("Road Routes")]
    [Tooltip("Rotas de rodovia desta estrutura (centro-a-centro dos hexes).")]
    public List<RoadRouteDefinition> roadRoutes = new List<RoadRouteDefinition>();

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
