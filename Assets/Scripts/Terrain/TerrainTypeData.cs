using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

[System.Serializable]
public class TerrainConstructionVisionOverride
{
    [Tooltip("Construcao alvo desta excecao de visao/LoS.")]
    public ConstructionData construction;

    [Tooltip("EV aplicado quando esta construcao estiver neste terreno.")]
    public int ev = 0;

    [Tooltip("Block LoS aplicado quando esta construcao estiver neste terreno.")]
    public bool blockLoS = true;
}

[System.Serializable]
public class TerrainStructureVisionOverride
{
    [Tooltip("Estrutura alvo desta excecao de visao/LoS.")]
    public StructureData structure;

    [Tooltip("EV aplicado quando esta estrutura estiver neste terreno.")]
    public int ev = 0;

    [Tooltip("Block LoS aplicado quando esta estrutura estiver neste terreno.")]
    public bool blockLoS = true;
}

[CreateAssetMenu(menuName = "Game/Terrain/Terrain Data", fileName = "TerrainData_")]
public class TerrainTypeData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico usado para lookup e regras.")]
    public string id;

    [Tooltip("Nome mostrado na UI/debug.")]
    public string displayName;

    [TextArea]
    public string description;

    [Header("Palette Identity")]
    [Tooltip("Tile da palette que identifica este terreno no mapa.")]
    public TileBase paletteTile;

    [Header("Native Domain")]
    [Tooltip("Dominio/altura nativo do terreno.")]
    public Domain domain = Domain.Land;
    [Tooltip("Dominio/altura nativo do terreno.")]
    public HeightLevel heightLevel = HeightLevel.Surface;
    [FormerlySerializedAs("additionalLayerModes")]
    [Tooltip("Dominios/alturas adicionais permitidos neste terreno.")]
    public List<TerrainLayerMode> aditionalDomainsAllowed = new List<TerrainLayerMode>();

    [Header("Flags")]
    [Tooltip("Se true, dominio do ar e sempre permitido (independente dos modos).")]
    public bool alwaysAllowAirDomain = true;
    [Header("Aircraft Ops")]
    [Tooltip("Permite pouso de aeronaves diretamente neste terreno.")]
    public bool allowAircraftLanding = false;
    [Tooltip("Classes de unidade permitidas para pouso. Se vazio, aceita Jet/Plane/Helicopter.")]
    public List<GameUnitClass> landingAllowedClasses = new List<GameUnitClass>();
    [Tooltip("Skills exigidas para pouso. Se vazio, nao exige skill.")]
    public List<SkillData> landingRequiredSkills = new List<SkillData>();
    [Tooltip("Permite decolagem de aeronaves diretamente deste terreno.")]
    public bool allowAircraftTakeoff = false;
    [Tooltip("Modos de movimento permitidos para decolagem. Se vazio, permite MoveuParado e MoveuAndando.")]
    public List<SensorMovementMode> takeoffAllowedMovementModes = new List<SensorMovementMode>();

    [Header("Movement")]
    [Tooltip("Custo basico de autonomia para entrar neste hex. Minimo 1.")]
    [Min(1)]
    public int basicAutonomyCost = 1;
    [Header("DPQ")]
    [Tooltip("Referencia de qualidade de posicao (DPQ) aplicada a este terreno.")]
    public DPQData dpqData;

    [Header("Vision")]
    [Tooltip("EV (elevacao de visada) base deste terreno.")]
    public int ev = 0;

    [Tooltip("Se true, este terreno bloqueia linha de visada por padrao.")]
    public bool blockLoS = true;

    [Header("Vision Exceptions")]
    [Tooltip("Excecoes de EV/Block LoS para construcoes sobre este terreno.")]
    public List<TerrainConstructionVisionOverride> constructionVisionOverrides = new List<TerrainConstructionVisionOverride>();

    [Tooltip("Excecoes de EV/Block LoS para estruturas sobre este terreno.")]
    public List<TerrainStructureVisionOverride> structureVisionOverrides = new List<TerrainStructureVisionOverride>();

    [Header("Skill Rules")]
    [Tooltip("Se houver skills nesta lista, a unidade precisa ter pelo menos uma para entrar neste terreno.")]
    public List<SkillData> requiredSkillsToEnter = new List<SkillData>();

    [Tooltip("Overrides opcionais de custo de autonomia por skill.")]
    public List<TerrainSkillCostOverride> skillCostOverrides = new List<TerrainSkillCostOverride>();

    public bool TryGetConstructionVisionOverride(ConstructionData constructionData, out int overrideEv, out bool overrideBlockLoS)
    {
        overrideEv = 0;
        overrideBlockLoS = true;
        if (constructionData == null || constructionVisionOverrides == null)
            return false;

        for (int i = 0; i < constructionVisionOverrides.Count; i++)
        {
            TerrainConstructionVisionOverride item = constructionVisionOverrides[i];
            if (item == null || item.construction != constructionData)
                continue;

            overrideEv = Mathf.Max(0, item.ev);
            overrideBlockLoS = item.blockLoS;
            return true;
        }

        return false;
    }

    public bool TryGetStructureVisionOverride(StructureData structureData, out int overrideEv, out bool overrideBlockLoS)
    {
        overrideEv = 0;
        overrideBlockLoS = true;
        if (structureData == null || structureVisionOverrides == null)
            return false;

        for (int i = 0; i < structureVisionOverrides.Count; i++)
        {
            TerrainStructureVisionOverride item = structureVisionOverrides[i];
            if (item == null || item.structure != structureData)
                continue;

            overrideEv = Mathf.Max(0, item.ev);
            overrideBlockLoS = item.blockLoS;
            return true;
        }

        return false;
    }
}
