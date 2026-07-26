using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class AITargetPreferenceByClassRule
{
    [Tooltip("Classe de unidade alvo para esta regra de prioridade.")]
    public GameUnitClass targetClass = GameUnitClass.Infantry;

    [Tooltip("Prioridade t�tica aplicada ao atacar esta classe.")]
    public BazookaTargetPriority priority = BazookaTargetPriority.Tertiary;
}

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

[System.Flags]
public enum ConstructionFacilityType
{
    None = 0,
    Harbor = 1 << 0,
    Airport = 1 << 1,
    City = 1 << 2,
    TransportTerminal = 1 << 3,
    AnyConstruction = ~0
}

public static class ConstructionFacilityTypeRules
{
    public static bool MatchesAny(ConstructionData construction, ConstructionFacilityType allowedFacilities)
    {
        if (construction == null || allowedFacilities == ConstructionFacilityType.None)
            return false;

        if (allowedFacilities == ConstructionFacilityType.AnyConstruction)
            return true;

        return (construction.isHarbor && (allowedFacilities & ConstructionFacilityType.Harbor) != 0)
            || (construction.isAirport && (allowedFacilities & ConstructionFacilityType.Airport) != 0)
            || (construction.isCity && (allowedFacilities & ConstructionFacilityType.City) != 0)
            || (construction.isTransportTerminal && (allowedFacilities & ConstructionFacilityType.TransportTerminal) != 0);
    }
}

[System.Serializable]
public enum AIPurchaseMode
{
    Either    = 0,  // Comprado em qualquer contexto (padrão)
    Offensive = 1,  // Apenas modo ofensivo/normal; ignorado quando defensiveBaseThreat
    Defensive = 2,  // Apenas quando há ameaça defensiva; nunca em modo ofensivo
}

[System.Serializable]
public enum LosPolicy
{
    InheritGlobal = 0,
    ForceOn = 1,
    ForceOff = 2
}

[System.Serializable]
public enum UnitCombatClassification
{
    Combatente = 0,
    Artilheiro = 1,
    Hibrido = 2,
    Civil = 3
}

[System.Serializable]
public class UnitVisionException
{
    [Tooltip("Dominio alvo para esta excecao de visao.")]
    public Domain domain = Domain.Land;

    [Tooltip("Se ativo, esta excecao se aplica a qualquer altura do dominio (heightLevel ignorado).")]
    public bool allHeights = false;

    [Tooltip("Altura alvo para esta excecao de visao (ignorado quando allHeights estiver ativo).")]
    public HeightLevel heightLevel = HeightLevel.Surface;

    [Min(0)]
    [Tooltip("Alcance de visao quando o alvo estiver no dominio/altura desta excecao.")]
    public int vision = 3;

    [Tooltip("Detecta unidades que possuam qualquer skill desta lista (match por referencia ou ID).")]
    public List<SkillData> detectUnitsWithFollowingSkills = new List<SkillData>();

    [Tooltip("Politica de LOS para esta especializacao. InheritGlobal usa o toggle global da partida.")]
    public LosPolicy losPolicy = LosPolicy.InheritGlobal;
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

    [Header("Construction Requirements")]
    [Tooltip("Construcao que a equipe precisa ter capturado ao menos uma vez antes de poder produzir esta unidade.")]
    public ConstructionData requiredBuilding;

    [FormerlySerializedAs("sprite")]
    public Sprite spriteDefault;
    public List<TeamVariantSprite> teamVariantSprites = new List<TeamVariantSprite>();

    [Header("Attributes")]
    public int maxHP = 10;
    public int defense = 0;
    [SerializeField, HideInInspector] private ArmorClass armorClass = ArmorClass.Light;
    public int movement = 3;
    [Min(1)] public int visao = 3;
    [FormerlySerializedAs("visionExceptions")]
    [Tooltip("Vision Specializations por dominio/altura do alvo. Se nao houver match, usa o campo visao padrao.")]
    public List<UnitVisionException> visionSpecializations = new List<UnitVisionException>();
    public MovementCategory movementCategory = MovementCategory.Marcha;
    public MilitaryForce militaryForce = MilitaryForce.Army;
    public GameUnitClass unitClass = GameUnitClass.Infantry;
    [SerializeField] private UnitCombatClassification combatClassification = UnitCombatClassification.Civil;
    [Header("Roles")]
    [Tooltip("Papeis assumidos por esta unidade (ex: Capturador, Assalto, Transporte).")]
    public List<UnitRole> roles = new List<UnitRole>();
    
    [Header("AI")]
    [Tooltip("Ordem de iniciativa na fila de ação da IA. Priority age primeiro, Retreat age por último. Desempate: mais HP age antes.")]
    public AiInitiative aiInitiative = AiInitiative.Medium;
    [Tooltip("Contexto de compra pela IA. Defensive = só comprada quando há ameaça à base; Offensive = nunca em modo defensivo; Either = sem restrição.")]
    public AIPurchaseMode aiPurchaseMode = AIPurchaseMode.Either;
    [Tooltip("Se ativo, esta unidade não aparece na lista de compras da IA quando o Hard Mode está ligado.")]
    public bool bannedOnHardMode = false;
    [Tooltip("Preferencia de alvo por classe (usada pelo gate de Target Preference no AI Unit Profile).")]
    public List<AITargetPreferenceByClassRule> aiTargetPreferenceByClass = new List<AITargetPreferenceByClassRule>();

    [Header("Combat Behavior")]
    [Tooltip("Ordem de prioridade das acoes que a IA executara com esta unidade. A IA tenta cada entrada em sequencia e executa a primeira que for possivel.")]
    public List<AISensorPriority> aiSensorPriority = new List<AISensorPriority> { AISensorPriority.Attack, AISensorPriority.Reposition };
    [Tooltip("Ao se posicionar para combate (inimigo engajado), prioriza celulas DPQ favoraveis (construcoes, estruturas) em vez de avancar em linha reta.")]
    public bool prioritizeDpqAtBattle = false;
    [Tooltip("Quando ativo, o capturador evita hexes com ameaca proxima e da passos mais curtos mas seguros. Quando inativo (padrao), o capturador avanca direto para o objetivo ignorando ameacas.")]
    public bool playConservative = false;
    [Tooltip("Ao se mover (fora de combate), prefere rotas com melhor DPQ em vez da rota mais curta. Util para unidades de suporte que devem permanecer em posicoes defensivas.")]
    public bool preferMoveOnBestDPQ = false;
    [Tooltip("Ao se mover rumo a um destino, prioriza atingir o hex mais distante possivel dentro do alcance de movimento. Favorece rotas de estrada naturalmente pois permitem chegar mais longe por turno. Usado por transportadores para cobrir distancias maximas por turno.")]
    public bool preferMaxDisplacement = false;

    [Header("Long Range Decision")]
    [Tooltip("Se ativo, a IA nao reposiciona esta unidade apos compra/spawn. Outros casos especiais podem mover a unidade em regras futuras.")]
    public bool longRangeStationary = false;
    [Tooltip("Se ativo, a IA prefere reposicionar para manter o alvo no alcance maximo da arma em vez de se aproximar do alvo.")]
    [FormerlySerializedAs("preferRepositionToMaxRange")]
    public bool preferRepositionAtWeaponMaxRange = false;
    [Tooltip("Se ativo, unidades hibridas tentam agir primeiro como artilheiro/fogo indireto; se nao houver acao valida, caem para comportamento combatente.")]
    public bool preferArtilleryModeBeforeCombatant = false;

    [Header("Attack Decision")]
    [Tooltip("Se ativo, a IA usa simulacao da Matriz de HP para decidir se esta unidade deve aceitar um combate.")]
    public bool useAttackDecision = true;
    [Tooltip("Perda maxima de HP que a unidade aceita sofrer ao iniciar combate. Ex.: 50 permite perder ate metade do HP atual.")]
    [Range(0, 100)] public int attackAcceptHpLossPercent = 50;
    [Tooltip("Dano minimo exigido no alvo, em percentual do HP maximo do alvo, para aceitar o combate mesmo quando nao houver kill.")]
    [Range(0, 100)] public int attackEliminationMinPercent = 10;
    [Tooltip("Se ativo, a unidade rejeita ataques em que a simulacao indica que ela morre no contra-ataque.")]
    public bool attackMustSurvive = true;
    [Tooltip("Se ativo, ignora o gate de Attack Decision quando a unidade estiver alocada em um plano defensivo.")]
    public bool ignoreAttackDecisionWhenDefending = false;
    [Tooltip("Tolerancia extra de perda de HP quando a unidade estiver defendendo um setor/base. Ex.: 20 transforma limite 50 em 70.")]
    [Range(0, 100)] public int defensiveAttackExtraHpLossPercent = 20;

    [Header("Repair Decision")]
    // Decision data lives here (UnitData). Runtime state lives in UnitManager.isUnderRepair.
    // --- Trigger (activate isUnderRepair) ---
    [Tooltip("TRIGGER — Enter repair mode when HP is at or below this value (1–9). Set 0 to disable.")]
    [Range(0, 9)] public int repairTriggerHpBelow = 0;
    [Tooltip("TRIGGER — Enter repair mode when operational autonomy remaining is at or below this percentage (0–100). Set 0 to disable.")]
    [Range(0, 100)] public int repairTriggerAutonomyPct = 0;
    [Tooltip("TRIGGER — Enable ammo as a repair trigger.")]
    public bool repairTriggerAmmoEnabled = false;
    [Tooltip("TRIGGER — Enter repair mode when any embarked weapon ammo is at or below this percentage (0 = completely out of ammo). Only evaluated when the toggle above is enabled.")]
    [Range(0, 100)] public int repairTriggerAmmoPct = 0;
    // --- Recovery (deactivate isUnderRepair) ---
    [Tooltip("RECOVERY — Leave repair mode when HP reaches this value or above AND autonomy/ammo (if they were triggers) are also above their thresholds.")]
    [Range(1, 10)] public int repairRecoverHpAbove = 8;
    [Tooltip("BEHAVIOR — While under repair, attempt to fuse with a damaged allied unit of the same type nearby instead of waiting passively.")]
    public bool fuseWhileInRepair = false;

    [Header("Restock Decision")]
    [Tooltip("TRIGGER - Supplier returns to a safe allied reload/transfer point when gallon stock is at or below this percentage. Set 0 to disable.")]
    [Range(0, 100)] public int restockTriggerGallonPct = 0;
    [Tooltip("TRIGGER - Supplier returns to a safe allied reload/transfer point when ammo box stock is at or below this percentage. Set 0 to disable.")]
    [Range(0, 100)] public int restockTriggerAmmoBoxPct = 0;
    [Tooltip("TRIGGER - Supplier returns to a safe allied reload/transfer point when tools/parts stock is at or below this percentage. Set 0 to disable.")]
    [Range(0, 100)] public int restockTriggerToolsPct = 0;
    [Tooltip("TRIGGER - Supplier returns to restock when any runtime embarked supply reaches 0. Preserves the old empty-cargo reload behavior.")]
    public bool restockWhenAnyRuntimeSupplyEmpty = true;

    [Header("Logistics AI")]
    [Tooltip("Se ativo, a IA usa este supridor em manutencao preventiva.")]
    public bool aiPreventiveMaintenanceEnabled = true;
    [Tooltip("Se ativo, a manutencao preventiva tambem pode ser atendida mesmo quando houver aliados em UnderRepair. Se desativo, UnderRepair bloqueia preventivos.")]
    public bool aiPreventiveSupplyCanRunWithUnderRepair = false;
    [Tooltip("Para supridores com Play Conservative: evita hex de atendimento com inimigo visivel neste raio. 0 desativa. Use 2 para tolerar inimigo a 3 hexes.")]
    [Range(0, 6)] public int aiConservativeSupplyAvoidEnemyRange = 2;
    [Tooltip("Manutencao preventiva: suprir aliados com HP abaixo deste percentual. 0 desativa este criterio.")]
    [Range(0, 100)] public int aiPreventiveSupplyHpBelowPct = 70;
    [Tooltip("Manutencao preventiva: suprir aliados com autonomia abaixo deste percentual. 0 desativa este criterio.")]
    [Range(0, 100)] public int aiPreventiveSupplyAutonomyBelowPct = 60;
    [Tooltip("Manutencao preventiva: suprir aliados com alguma arma embarcada com munição nesse valor ou abaixo. 0 desativa este criterio.")]
    [Range(0, 10)] public int aiPreventiveSupplyWeaponAmmoAtOrBelow = 1;
    [Tooltip("So vale para unidades que sao supridoras E transportadoras. Carregando um passageiro em reparo, a ordem e: suprir a bordo > voltar para transferir estoque mantendo o ferido > e SO entao desembarcar num ponto de reparo. Desligado, o passageiro em reparo e entregue pelo EVAC normal como qualquer outra carga.")]
    public bool aiDisembarkWhenCannotSupply = true;

    [Header("Elite")]
    [Tooltip("Nivel de elite da unidade (padrao: 0).")]
    [Min(0)] public int eliteLevel = 0;
    [Tooltip("Unidade base da qual esta é um upgrade direto. Define a cadeia de evolução usada pela IA para o próximo upgrade (ex.: Tanque A → Tanque Z).")]
    public UnitData eliteFrom;
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
    [Header("Layer Reveal After Attacking")]
    [Tooltip("Se ligado, ao atacar esta unidade e forcada para o dominio/altura abaixo (ex.: submarino que emerge para disparar), revelando-a. O retorno so acontece apos o lock abaixo expirar (mesmo comportamento do Layer Force After Hit).")]
    public bool emergesToAttack = false;
    [Tooltip("Dominio para o qual a unidade e forcada apos atacar, quando o campo acima estiver ligado.")]
    public Domain emergeAfterAttackDomain = Domain.Naval;
    [Tooltip("Altura para a qual a unidade e forcada apos atacar, quando o campo acima estiver ligado.")]
    public HeightLevel emergeAfterAttackHeight = HeightLevel.Surface;
    [Min(1)]
    [Tooltip("Duracao do lock de camada (em turnos do dono da unidade) apos emergir por ataque. Mantenha igual ao 'turns' da arma que forca emersao por dano, senao atacar fica mais seguro que ser atingido.")]
    public int emergeAfterAttackTurns = 2;
    [Header("Skills")]
    [Tooltip("Skills base da unidade. As instancias herdam essa lista ao aplicar o UnitData.")]
    public List<SkillData> skills = new List<SkillData>();
    [Header("Stealth Skills")]
    [Tooltip("Skills que tornam a unidade stealth. Detectores precisam casar essas skills na fechadura de deteccao.")]
    public List<SkillData> stealthSkills = new List<SkillData>();
    [Tooltip("Versao por dominio/altura das stealth skills. Se houver match na camada atual, esta lista tem prioridade.")]
    public List<UnitStealthSkillRule> stealthSkillRules = new List<UnitStealthSkillRule>();
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
    [Tooltip("Nasce com as reservas VAZIAS. Padrao: true. Desmarque para a unidade sair da compra ja carregada, o que so faz sentido quando a instalacao onde ela foi comprada teria o que dar (caminhao de suprimentos, aviao-tanque, porta-avioes, trem). Manter marcado preserva a cadeia logistica: quem nasce vazio precisa ser abastecido por outro elo.")]
    public bool startsWithEmptySupplies = true;
    public SupplierTier supplierTier = SupplierTier.Hub;
    [Min(0)] public int maxUnitsServedPerTurn = 0;
    public SupplierRangeMode serviceRange = SupplierRangeMode.SameHexOrEmbarked;
    public SupplierRangeMode collectionRange = SupplierRangeMode.SameHexOrEmbarked;
    [Tooltip("Dominios/alturas onde esta unidade consegue operar logistica.")]
    public List<SupplierOperationDomain> supplierOperationDomains = new List<SupplierOperationDomain>();
    [Tooltip("Servicos oferecidos por esta unidade de logistica.")]
    public List<ServiceData> supplierServicesProvided = new List<ServiceData>();
    [Tooltip("Classificacao automatica: None sem servicos; StockTransfer somente Transfer; FieldService quando oferece qualquer outro servico.")]
    public SupplierServiceProfile supplierServiceProfile = SupplierServiceProfile.None;
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
    [Tooltip("Facilities: categorias de construcao validas para o HEX atual do transportador no embarque. Basta corresponder a uma categoria. None = sem restricao por construcao.")]
    public ConstructionFacilityType allowedEmbarkWhenTransporterAtFacilities = ConstructionFacilityType.Harbor;
    [Header("Allowed Disembark Terrain When Transporter At")]
    [FormerlySerializedAs("allowedDisembarkTerrains")]
    [Tooltip("Terrain: Terrenos validos para o HEX atual do transportador no desembarque. Vazio = sem restricao por terreno.")]
    public List<TerrainTypeData> allowedDisembarkWhenTransporterAtTerrains = new List<TerrainTypeData>();
    [FormerlySerializedAs("allowedDisembarkStructures")]
    [Tooltip("Terrain + Structure: Pares estrutura+terreno base validos para o HEX atual do transportador no desembarque.")]
    public List<TransportStructureTerrainRule> allowedDisembarkWhenTransporterAtTerrainStructures = new List<TransportStructureTerrainRule>();
    [Tooltip("Facilities: categorias de construcao validas para o HEX atual do transportador no desembarque. Basta corresponder a uma categoria. None = sem restricao por construcao.")]
    public ConstructionFacilityType allowedDisembarkWhenTransporterAtFacilities = ConstructionFacilityType.Harbor | ConstructionFacilityType.Airport;
    [Tooltip("Slots de transporte e regras de embarque.")]
    public List<UnitTransportSlotRule> transportSlots = new List<UnitTransportSlotRule>();

    public int autonomia = 99;
    public int cost = 100;

    public ArmorClass ArmorClass => armorClass;
    public UnitCombatClassification CombatClassification => combatClassification;

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
        supplierServiceProfile =
            ServiceData.ResolveSupplierServiceProfile(supplierServicesProvided);
        if (supplierResources == null)
            supplierResources = new List<UnitEmbarkedSupply>();
        if (combatModifiers == null)
            combatModifiers = new List<CombatModifierData>();
        if (skills == null)
            skills = new List<SkillData>();
        if (stealthSkills == null)
            stealthSkills = new List<SkillData>();
        if (stealthSkillRules == null)
            stealthSkillRules = new List<UnitStealthSkillRule>();
        if (visionSpecializations == null)
            visionSpecializations = new List<UnitVisionException>();
        if (aiTargetPreferenceByClass == null)
            aiTargetPreferenceByClass = new List<AITargetPreferenceByClassRule>();
        if (transportSlots == null)
            transportSlots = new List<UnitTransportSlotRule>();
        if (allowedEmbarkWhenTransporterAtTerrains == null)
            allowedEmbarkWhenTransporterAtTerrains = new List<TerrainTypeData>();
        if (allowedEmbarkWhenTransporterAtTerrainStructures == null)
            allowedEmbarkWhenTransporterAtTerrainStructures = new List<TransportStructureTerrainRule>();
        if (allowedDisembarkWhenTransporterAtTerrains == null)
            allowedDisembarkWhenTransporterAtTerrains = new List<TerrainTypeData>();
        if (allowedDisembarkWhenTransporterAtTerrainStructures == null)
            allowedDisembarkWhenTransporterAtTerrainStructures = new List<TransportStructureTerrainRule>();
        eliteLevel = Mathf.Max(0, eliteLevel);
        visao = Mathf.Max(1, visao);
        attackAcceptHpLossPercent = Mathf.Clamp(attackAcceptHpLossPercent, 0, 100);
        attackEliminationMinPercent = Mathf.Clamp(attackEliminationMinPercent, 0, 100);
        defensiveAttackExtraHpLossPercent = Mathf.Clamp(defensiveAttackExtraHpLossPercent, 0, 100);
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
        aiPreventiveSupplyHpBelowPct = Mathf.Clamp(aiPreventiveSupplyHpBelowPct, 0, 100);
        aiPreventiveSupplyAutonomyBelowPct = Mathf.Clamp(aiPreventiveSupplyAutonomyBelowPct, 0, 100);
        aiPreventiveSupplyWeaponAmmoAtOrBelow = Mathf.Max(0, aiPreventiveSupplyWeaponAmmoAtOrBelow);
        aiConservativeSupplyAvoidEnemyRange = Mathf.Max(0, aiConservativeSupplyAvoidEnemyRange);

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

        for (int i = aiTargetPreferenceByClass.Count - 1; i >= 0; i--)
        {
            if (aiTargetPreferenceByClass[i] == null)
                aiTargetPreferenceByClass.RemoveAt(i);
        }
        RecomputeCombatClassification();
    }

    public void RecomputeCombatClassification()
    {
        combatClassification = ResolveCombatClassification();
    }

    private UnitCombatClassification ResolveCombatClassification()
    {
        if (embarkedWeapons == null || embarkedWeapons.Count == 0)
            return UnitCombatClassification.Civil;

        bool hasWeapon = false;
        bool allDirect = true;
        bool allIndirect = true;
        bool allHybrid = true;

        for (int i = 0; i < embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = embarkedWeapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;

            hasWeapon = true;
            int minRange = embarked.GetRangeMin();
            int maxRange = embarked.GetRangeMax();
            bool isDirect = minRange == 1 && maxRange == 1;
            bool isIndirect = minRange > 1 && maxRange > 1;
            bool isHybrid = minRange == 1 && maxRange > 1;

            allDirect &= isDirect;
            allIndirect &= isIndirect;
            allHybrid &= isHybrid;
        }

        if (!hasWeapon)
            return UnitCombatClassification.Civil;
        if (allDirect)
            return UnitCombatClassification.Combatente;
        if (allIndirect)
            return UnitCombatClassification.Artilheiro;
        if (allHybrid)
            return UnitCombatClassification.Hibrido;

        return UnitCombatClassification.Hibrido;
    }

    public bool IsAircraft()
    {
        if (domain == Domain.Air) return true;
        if (aditionalDomainsAllowed != null)
        {
            for (int i = 0; i < aditionalDomainsAllowed.Count; i++)
            {
                if (aditionalDomainsAllowed[i].domain == Domain.Air) return true;
            }
        }
        return false;
    }

    public bool IsMaritime()
    {
        if (domain == Domain.Naval || domain == Domain.Submarine) return true;
        if (aditionalDomainsAllowed != null)
        {
            for (int i = 0; i < aditionalDomainsAllowed.Count; i++)
            {
                var d = aditionalDomainsAllowed[i].domain;
                if (d == Domain.Naval || d == Domain.Submarine) return true;
            }
        }
        return false;
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

    public BazookaTargetPriority ResolveAiTargetPriorityForTargetClass(GameUnitClass targetClass)
    {
        if (aiTargetPreferenceByClass == null || aiTargetPreferenceByClass.Count == 0)
            return BazookaTargetPriority.Tertiary;

        for (int i = 0; i < aiTargetPreferenceByClass.Count; i++)
        {
            AITargetPreferenceByClassRule entry = aiTargetPreferenceByClass[i];
            if (entry == null || entry.targetClass != targetClass)
                continue;
            return entry.priority;
        }

        return BazookaTargetPriority.Tertiary;
    }

    public bool HasAnyAiTargetPreferenceConfigured()
    {
        if (aiTargetPreferenceByClass == null || aiTargetPreferenceByClass.Count == 0)
            return false;

        for (int i = 0; i < aiTargetPreferenceByClass.Count; i++)
        {
            AITargetPreferenceByClassRule entry = aiTargetPreferenceByClass[i];
            if (entry == null)
                continue;
            if (entry.priority != BazookaTargetPriority.Tertiary)
                return true;
        }

        return false;
    }

    public bool CanDetectStealthFor(Domain targetDomain, HeightLevel targetHeightLevel, UnitData targetData = null)
    {
        if (!TryGetVisionException(targetDomain, targetHeightLevel, out UnitVisionException entry))
            return false;

        if (targetData == null || entry.detectUnitsWithFollowingSkills == null || entry.detectUnitsWithFollowingSkills.Count == 0)
            return false;

        List<SkillData> targetStealthSkills = targetData.ResolveStealthSkillsForDetection(targetDomain, targetHeightLevel);
        if (targetStealthSkills == null || targetStealthSkills.Count == 0)
            return false;

        return HasAnySkillMatch(entry.detectUnitsWithFollowingSkills, targetStealthSkills);
    }

    public LosPolicy ResolveLosPolicyFor(Domain targetDomain, HeightLevel targetHeightLevel)
    {
        if (TryGetVisionException(targetDomain, targetHeightLevel, out UnitVisionException entry) && entry != null)
            return entry.losPolicy;

        return LosPolicy.InheritGlobal;
    }

    public bool ResolveLosValidationFor(Domain targetDomain, HeightLevel targetHeightLevel, bool enableLosValidationGlobal)
    {
        LosPolicy policy = ResolveLosPolicyFor(targetDomain, targetHeightLevel);
        if (policy == LosPolicy.InheritGlobal)
            return enableLosValidationGlobal;

        return policy == LosPolicy.ForceOn;
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
                if (entry == null || entry.domain != targetDomain)
                    continue;
                if (entry.allHeights || entry.heightLevel == targetHeightLevel)
                {
                    match = entry;
                    return true;
                }
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
