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

[System.Serializable]
public class UnitVisionException
{
    [Tooltip("Dominio alvo para esta excecao de visao.")]
    public Domain domain = Domain.Land;

    [Tooltip("Altura alvo para esta excecao de visao.")]
    public HeightLevel heightLevel = HeightLevel.Surface;

    [Min(0)]
    [Tooltip("Alcance de visao quando o alvo estiver no dominio/altura desta excecao.")]
    public int vision = 3;

    [Tooltip("Se true, esta excecao tambem habilita deteccao de alvos stealth neste dominio/altura.")]
    public bool detectsStealth = false;

    [Tooltip("Detecta unidades que possuam qualquer skill desta lista (match por referencia ou ID).")]
    public List<SkillData> detectUnitsWithFollowingSkills = new List<SkillData>();
}

[System.Serializable]
public class UnitStealthSkillRule
{
    [Tooltip("Dominio onde esta skill stealth fica ativa para deteccao.")]
    public Domain domain = Domain.Land;

    [Tooltip("Altura onde esta skill stealth fica ativa para deteccao.")]
    public HeightLevel heightLevel = HeightLevel.Surface;

    [Tooltip("Skill stealth ativa neste dominio/altura.")]
    public SkillData skill;
}

public enum StealthRevealScope
{
    AllTeams = 0,
    DetectorTeamOnly = 1,
    ConfiguredTeams = 2
}

[CreateAssetMenu(menuName = "Game/Units/Unit Data", fileName = "UnitData_")]
public class UnitData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico usado para spawn e lookup.")]
    public string id;

    [Tooltip("Nome mostrado na UI.")]
    public string displayName;
    [Tooltip("Apelido curto para tabelas/matrizes (ex.: SD, BZ).")]
    public string apelido;

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
    [FormerlySerializedAs("visionExceptions")]
    [Tooltip("Vision Specializations por dominio/altura do alvo. Se nao houver match, usa o campo visao padrao.")]
    public List<UnitVisionException> visionSpecializations = new List<UnitVisionException>();
    [Header("Stealth Visibility")]
    [Tooltip("Escopo de quem passa a enxergar a unidade quando ela e detectada.")]
    public StealthRevealScope stealthRevealScope = StealthRevealScope.AllTeams;
    [Tooltip("Quando scope = ConfiguredTeams, define quais times passam a enxergar a unidade apos deteccao.")]
    public List<TeamId> stealthRevealTeams = new List<TeamId>();
    [Min(1)]
    [Tooltip("Quantidade de turnos (round) em que o alvo stealth permanece revelado apos deteccao.")]
    public int stealthVisibleIfDetectedForTurns = 1;
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
    [Header("Stealth Skills")]
    [Tooltip("Skills que tornam a unidade stealth. Detectores precisam casar essas skills na fechadura de deteccao.")]
    public List<SkillData> stealthSkills = new List<SkillData>();
    [Tooltip("Versao por dominio/altura das stealth skills. Se houver match na camada atual, esta lista tem prioridade.")]
    public List<UnitStealthSkillRule> stealthSkillRules = new List<UnitStealthSkillRule>();
    [Header("Autonomy")]
    [Tooltip("Perfil de autonomia usado pelas regras da skill Operational Autonomy.")]
    public AutonomyData autonomyData;
    [Header("Embarked Weapons")]
    [Tooltip("Armas embarcadas na unidade. A ordem da lista define prioridade (primaria, secundaria...). Pode ficar vazia para unidades desarmadas.")]
    public List<UnitEmbarkedWeapon> embarkedWeapons = new List<UnitEmbarkedWeapon>();
    [Header("Weapon Restrictions")]
    [Tooltip("Bloqueia o uso de armas quando a unidade estiver nestes dominios/alturas (ex.: cacas pousados em Land/Surface).")]
    public List<WeaponLayerMode> cantUseWeaponsOnTheFollowDomain = new List<WeaponLayerMode>();

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
        if (cantUseWeaponsOnTheFollowDomain == null)
            cantUseWeaponsOnTheFollowDomain = new List<WeaponLayerMode>();
        if (supplierOperationDomains == null)
            supplierOperationDomains = new List<SupplierOperationDomain>();
        if (supplierServicesProvided == null)
            supplierServicesProvided = new List<ServiceData>();
        if (supplierResources == null)
            supplierResources = new List<UnitEmbarkedSupply>();
        if (combatModifiers == null)
            combatModifiers = new List<CombatModifierData>();
        if (skills == null)
            skills = new List<SkillData>();
        if (stealthSkills == null)
            stealthSkills = new List<SkillData>();
        if (stealthRevealTeams == null)
            stealthRevealTeams = new List<TeamId>();
        if (stealthSkillRules == null)
            stealthSkillRules = new List<UnitStealthSkillRule>();
        if (visionSpecializations == null)
            visionSpecializations = new List<UnitVisionException>();
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
        stealthVisibleIfDetectedForTurns = Mathf.Max(1, stealthVisibleIfDetectedForTurns);
        NormalizeStealthRevealTeams();
        for (int i = 0; i < visionSpecializations.Count; i++)
        {
            UnitVisionException entry = visionSpecializations[i];
            if (entry == null)
                continue;

            entry.vision = Mathf.Max(0, entry.vision);
            if (entry.detectUnitsWithFollowingSkills == null)
                entry.detectUnitsWithFollowingSkills = new List<SkillData>();
        }
        maxUnitsServedPerTurn = Mathf.Max(0, maxUnitsServedPerTurn);
        if (supplierTier == SupplierTier.SelfSupplier)
            supplierTier = SupplierTier.Receiver;
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

    private void NormalizeStealthRevealTeams()
    {
        if (stealthRevealTeams == null || stealthRevealTeams.Count <= 1)
            return;

        HashSet<TeamId> seen = new HashSet<TeamId>();
        for (int i = stealthRevealTeams.Count - 1; i >= 0; i--)
        {
            TeamId team = stealthRevealTeams[i];
            if (seen.Contains(team))
            {
                stealthRevealTeams.RemoveAt(i);
                continue;
            }

            seen.Add(team);
        }
    }

    public bool IsAircraft()
    {
        return unitClass == GameUnitClass.Jet ||
               unitClass == GameUnitClass.Plane ||
               unitClass == GameUnitClass.Helicopter;
    }

    public bool IsWeaponUseBlockedAt(Domain currentDomain, HeightLevel currentHeightLevel)
    {
        if (cantUseWeaponsOnTheFollowDomain == null || cantUseWeaponsOnTheFollowDomain.Count <= 0)
            return false;

        for (int i = 0; i < cantUseWeaponsOnTheFollowDomain.Count; i++)
        {
            WeaponLayerMode blocked = cantUseWeaponsOnTheFollowDomain[i];
            if (blocked.domain == currentDomain && blocked.heightLevel == currentHeightLevel)
                return true;
        }

        return false;
    }

    public int ResolveVisionFor(Domain targetDomain, HeightLevel targetHeightLevel)
    {
        if (TryGetVisionException(targetDomain, targetHeightLevel, out UnitVisionException entry))
            return Mathf.Max(0, entry.vision);

        return Mathf.Max(1, visao);
    }

    public bool CanDetectStealthFor(Domain targetDomain, HeightLevel targetHeightLevel, UnitData targetData = null)
    {
        if (!TryGetVisionException(targetDomain, targetHeightLevel, out UnitVisionException entry))
            return false;

        if (entry.detectsStealth)
            return true;

        if (targetData == null || entry.detectUnitsWithFollowingSkills == null || entry.detectUnitsWithFollowingSkills.Count == 0)
            return false;

        List<SkillData> targetStealthSkills = targetData.ResolveStealthSkillsForDetection(targetDomain, targetHeightLevel);
        if (targetStealthSkills == null || targetStealthSkills.Count == 0)
            return false;

        return HasAnySkillMatch(entry.detectUnitsWithFollowingSkills, targetStealthSkills);
    }

    public int ResolveStealthVisibleTurns()
    {
        return Mathf.Max(1, stealthVisibleIfDetectedForTurns);
    }

    public bool IsStealthUnit()
    {
        return ResolveStealthSkillsForDetection().Count > 0;
    }

    public bool IsStealthUnit(Domain currentDomain, HeightLevel currentHeightLevel)
    {
        return ResolveStealthSkillsForDetection(currentDomain, currentHeightLevel).Count > 0;
    }

    public List<SkillData> ResolveStealthSkillsForDetection()
    {
        List<SkillData> flattened = new List<SkillData>();
        CollectStealthSkillsFromRules(flattened, domainFilterEnabled: false, Domain.Land, HeightLevel.Surface);
        if (flattened.Count > 0)
            return flattened;

        if (stealthSkills != null && stealthSkills.Count > 0)
            return stealthSkills;

        return ResolveLegacyStealthSkills();
    }

    public List<SkillData> ResolveStealthSkillsForDetection(Domain targetDomain, HeightLevel targetHeightLevel)
    {
        bool hasLayerRules = stealthSkillRules != null && stealthSkillRules.Count > 0;
        List<SkillData> byLayer = new List<SkillData>();
        CollectStealthSkillsFromRules(byLayer, domainFilterEnabled: true, targetDomain, targetHeightLevel);
        if (hasLayerRules)
            return byLayer;

        if (stealthSkills != null && stealthSkills.Count > 0)
            return stealthSkills;

        return ResolveLegacyStealthSkills();
    }

    private List<SkillData> ResolveLegacyStealthSkills()
    {
        // Compatibilidade: enquanto assets antigos nao migram, aceita skills stealth legadas por ID.
        List<SkillData> legacy = new List<SkillData>();
        if (skills == null || skills.Count == 0)
            return legacy;

        for (int i = 0; i < skills.Count; i++)
        {
            SkillData skill = skills[i];
            if (skill == null || string.IsNullOrWhiteSpace(skill.id))
                continue;

            string id = skill.id.Trim();
            if (id.Equals("stealth", System.StringComparison.OrdinalIgnoreCase) ||
                id.Equals("furtividade", System.StringComparison.OrdinalIgnoreCase) ||
                id.Equals("submarine_stealth", System.StringComparison.OrdinalIgnoreCase) ||
                id.Equals("submerged_stealth", System.StringComparison.OrdinalIgnoreCase))
            {
                legacy.Add(skill);
            }
        }

        return legacy;
    }

    private void CollectStealthSkillsFromRules(List<SkillData> output, bool domainFilterEnabled, Domain domainFilter, HeightLevel heightFilter)
    {
        if (output == null || stealthSkillRules == null || stealthSkillRules.Count == 0)
            return;

        for (int i = 0; i < stealthSkillRules.Count; i++)
        {
            UnitStealthSkillRule rule = stealthSkillRules[i];
            if (rule == null || rule.skill == null)
                continue;

            if (domainFilterEnabled && (rule.domain != domainFilter || rule.heightLevel != heightFilter))
                continue;

            if (!ContainsSkill(output, rule.skill))
                output.Add(rule.skill);
        }
    }

    private static bool ContainsSkill(List<SkillData> list, SkillData skill)
    {
        if (list == null || skill == null)
            return false;

        string id = string.IsNullOrWhiteSpace(skill.id) ? string.Empty : skill.id.Trim();
        for (int i = 0; i < list.Count; i++)
        {
            SkillData current = list[i];
            if (current == null)
                continue;

            if (ReferenceEquals(current, skill))
                return true;

            string currentId = string.IsNullOrWhiteSpace(current.id) ? string.Empty : current.id.Trim();
            if (id.Length > 0 && currentId.Length > 0 &&
                string.Equals(id, currentId, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetVisionException(Domain targetDomain, HeightLevel targetHeightLevel, out UnitVisionException match)
    {
        if (visionSpecializations != null)
        {
            for (int i = 0; i < visionSpecializations.Count; i++)
            {
                UnitVisionException entry = visionSpecializations[i];
                if (entry == null)
                    continue;
                if (entry.domain != targetDomain || entry.heightLevel != targetHeightLevel)
                    continue;

                match = entry;
                return true;
            }
        }

        match = null;
        return false;
    }

    private static bool HasAnySkillMatch(List<SkillData> detectorSkills, List<SkillData> targetSkills)
    {
        if (detectorSkills == null || targetSkills == null)
            return false;

        for (int i = 0; i < detectorSkills.Count; i++)
        {
            SkillData detectorSkill = detectorSkills[i];
            if (detectorSkill == null)
                continue;

            string detectorId = string.IsNullOrWhiteSpace(detectorSkill.id) ? string.Empty : detectorSkill.id.Trim();
            for (int j = 0; j < targetSkills.Count; j++)
            {
                SkillData targetSkill = targetSkills[j];
                if (targetSkill == null)
                    continue;

                if (ReferenceEquals(detectorSkill, targetSkill))
                    return true;

                string targetId = string.IsNullOrWhiteSpace(targetSkill.id) ? string.Empty : targetSkill.id.Trim();
                if (detectorId.Length > 0 && targetId.Length > 0 &&
                    string.Equals(detectorId, targetId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
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
