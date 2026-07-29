using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Nivel de dificuldade escolhido na Tela de Entrada. Mapeia direto para os flags
// easyMode/hardMode/conscriptionWhenLosing/conscriptionDoctrine do AIController:
//   Iniciante   = 1/3 da renda fora das cidades; sem hard, sem conscricao
//   Facil       = renda e regras normais; sem hard, sem conscricao
//   Medio       = regras normais; conscricao SO perdendo
//   Formigueiro = regras normais; doutrina do enxame (conscricao SEMPRE), sem pacote hard
//   Competitiva = pacote hard (lista banida, projecao, blitz reserve); conscricao SO perdendo
//   Agressiva   = pacote hard + doutrina do enxame (conscricao SEMPRE)
public enum AIDifficulty
{
    Iniciante,
    Facil,
    Medio,
    Formigueiro,
    Competitiva,
    Agressiva
}

/// <summary>
// Controla a IA Global, incluindo a execu��o do plano de objetivos, compras, movimenta��o e combate.

/// </summary>
public partial class AIController : MonoBehaviour
{
    [Header("AI References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private ReplayManager replayManager;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    [Header("AI Settings")]
    [Tooltip("Exibe no Console os logs gerais com prefixo [AI].")]
    [InspectorName("Show AI Logs")]
    [SerializeField] private bool showAILogs;
    public bool ShowAILogs => showAILogs;

    [Tooltip("Exibe os elementos de AI sobre as unidades, tais como plano, badge, eixo.")]
    [SerializeField] private bool showAIUnitHUD;

    [Tooltip("Inicia a partida com a IA pausada, equivalente a pressionar F10.")]
    [SerializeField] private bool startOnPause;

    [Tooltip("Quando ligada, a IA executa batches sem sustentar menus e confirmações intermediárias. Desligue para apresentar no cursor e no panel_helper as mesmas etapas vistas pelo jogador.")]
    [InspectorName("IA Rapida")]
    [SerializeField] private bool iaRapida = true;
    public bool IARapida => iaRapida;

    [Tooltip("Modo difícil. Por enquanto: dobra os slots de capturador por setor e habilita os limites/banimentos espec�ficos de hard mode (log�stica e unidades banidas).")]
    [SerializeField] private bool hardMode = false;
    public bool HardMode => hardMode;

    [Tooltip("Modo facil: a IA recebe 1/3 da renda de construcoes que nao sejam cidades. Ignorado quando Hard Mode esta ligado.")]
    [SerializeField] private bool easyMode = false;
    public bool EasyMode => easyMode && !hardMode;

    [Tooltip("Strong/Weak Side Politic (PROTOTIPO, off por padrao): classifica os eixos em forte/equilibrado/fraco (com histerese) e enviesa a distribuicao de elite (fire support) para o lado forte, aliviando o fraco. Doutrina de concentracao de forca ao estilo AWBW. So mexe na priorizacao do elite existente — nao infla demanda de slots.")]
    [SerializeField] private bool strongWeakSidePolitic = false;
    public bool StrongWeakSidePolitic => strongWeakSidePolitic;

    [Tooltip("Doutrina da Conscricao (tatica do enxame): todo produtor do exercito compra o corpo mais barato TODO turno; demandas/elite so gastam por cima da massa garantida (imposto de conscricao no shopping). Desenhada pro Hard (Agressivo), mas pode ser ligada avulsa pra experimentar um Normal mais dificil.")]
    [SerializeField] private bool conscriptionDoctrine = false;
    public bool ConscriptionDoctrine => conscriptionDoctrine;

    [Tooltip("Fase de Massacre (valvula da conscricao): com clara vantagem numerica a doutrina cessa a massa e o caixa volta pro elite. Entra quando o ForceRatio macro atinge este valor (0.66 = ~2:1).")]
    [SerializeField, Range(0.5f, 1f)] private float massacreEnterForceRatio = 0.66f;
    public float MassacreEnterForceRatio => massacreEnterForceRatio;

    [Tooltip("Histerese da Fase de Massacre: uma vez ativa, so volta a conscricao se o ForceRatio cair abaixo deste valor (evita alternar soldado/elite com o ratio oscilando na fronteira).")]
    [SerializeField, Range(0.5f, 1f)] private float massacreExitForceRatio = 0.58f;
    public float MassacreExitForceRatio => massacreExitForceRatio;

    [Tooltip("Valvula de teto da Fase de Massacre: cessa a massa quando as unidades em campo atingem esta fracao do limite por time — perto do teto, corpo barato rouba o slot do elite.")]
    [SerializeField, Range(0.5f, 1f)] private float massacreUnitCapFillRatio = 0.85f;
    public float MassacreUnitCapFillRatio => massacreUnitCapFillRatio;

    // Estado de histerese da Fase de Massacre (persiste no save via SaveGameManager).
    [System.NonSerialized] private bool massacrePhaseActive;
    public bool MassacrePhaseActive
    {
        get => massacrePhaseActive;
        set => massacrePhaseActive = value;
    }

    public MatchController Match => matchController;

    [Tooltip("Recrutamento forcado emergencial quando a IA esta perdendo. Independente do pacote Hard.")]
    [SerializeField] private bool conscriptionWhenLosing = false;
    public bool ConscriptionWhenLosing => conscriptionWhenLosing;

    // Aplica a dificuldade escolhida na Tela de Entrada. Os flags sao mutuamente
    // exclusivos por combinacao: Facil liga easy; Competitivo liga hard; Agressivo
    // liga hard + conscricao; Normal desliga tudo.
    public void ApplyDifficulty(AIDifficulty difficulty)
    {
        easyMode = difficulty == AIDifficulty.Iniciante;
        hardMode = difficulty == AIDifficulty.Competitiva || difficulty == AIDifficulty.Agressiva;
        conscriptionWhenLosing = difficulty == AIDifficulty.Medio
            || difficulty == AIDifficulty.Competitiva;
        conscriptionDoctrine = difficulty == AIDifficulty.Formigueiro
            || difficulty == AIDifficulty.Agressiva;

        // FASE 1 da migração para AIPresetData: resolve e guarda o preset correspondente.
        // Nenhuma decisão lê dele ainda — os flags acima continuam sendo a fonte de verdade.
        ResolveActivePreset(difficulty);
    }

    // Save/Load: a dificuldade escolhida na Tela de Entrada só existe nestes flags depois
    // do ApplyDifficulty (PartidaConfig é consumido uma vez) — sem persistir, o load
    // voltava aos defaults serializados na cena.
    public void CaptureDifficultyForSave(out bool easy, out bool hard,
        out bool conscriptionWhenBehind, out bool conscriptionAlways)
    {
        easy = easyMode;
        hard = hardMode;
        conscriptionWhenBehind = conscriptionWhenLosing;
        conscriptionAlways = conscriptionDoctrine;
    }

    public void RestoreDifficultyFromSave(bool easy, bool hard,
        bool conscriptionWhenBehind, bool conscriptionAlways)
    {
        easyMode = easy;
        hardMode = hard;
        conscriptionWhenLosing = conscriptionWhenBehind;
        conscriptionDoctrine = conscriptionAlways;

        // Save antigo não guarda o preset, só os flags — reconstrói a dificuldade a partir
        // deles para que a inspeção mostre o perfil certo depois de um load.
        ResolveActivePreset(InferDifficultyFromFlags());
    }

    [Header("AI Stage Emulation")]
    [Tooltip("O core da IA pode ser testado em diferentes est�gios")]
    [SerializeField] private bool emulateStage0 = true;    
    [SerializeField] private bool emulateStage1 = true;
    [SerializeField] private bool emulateStage2 = true;
    [SerializeField] private bool emulateStage3 = true;
    [SerializeField] private bool emulateStage4 = true;

    [Header("Captura")]
    [Tooltip("Impacto do risco na tomada de decis�o (0 = ignora risco, 2 = risco pesa muito)")]
    [SerializeField, Range(0f, 2f)] private float riskDecisionImpact = 0.5f;
    public float RiskDecisionImpact => riskDecisionImpact;

    [Header("Objetivo Defensivo")]
    [Tooltip("Raio em hexes para criar objetivo defensivo num setor conquistado quando inimigo est� pr�ximo")]
    [SerializeField, Range(1, 8)] private int defenseEnemyRange = 3;
    public int DefenseEnemyRange => defenseEnemyRange;

    [Tooltip("Multiplicador de recrutamento: rogues dentro de defenseEnemyRange s�o convocados para defesa")]
    [SerializeField, Range(1, 8)] private int defenseCallRange = 4;
    public int DefenseCallRange => defenseCallRange;

    [Header("Refor�o SOS")]
    [Tooltip("Raio em hexes para detectar inimigos pr�ximo ao alvo de captura e acionar refor�o SOS")]
    [SerializeField, Range(1, 8)] private int alliesEnemyRange = 2;
    public int AlliesEnemyRange => alliesEnemyRange;

    [Tooltip("Raio em hexes para recrutar rogues como refor�o SOS em captura com desvantagem")]
    [SerializeField, Range(1, 16)] private int alliesCallRange = 8;
    public int AlliesCallRange => alliesCallRange;

    [Tooltip("Ratio inimigo HP / aliado HP para acionar refor�o SOS numa captura (ex: 2 = inimigo tem o dobro do HP)")]
    [SerializeField, Range(1f, 5f)] private float alliesAgainstEnemiesHpRatio = 2f;
    public float AlliesAgainstEnemiesHpRatio => alliesAgainstEnemiesHpRatio;

    [Header("Transporte")]
    [Tooltip("Dist�ncia m�nima em hexes do HQ para que um setor gere slot de transportador no plano e no shopping")]
    [SerializeField, Range(1, 30)] private int minDistanceForTransportSlot = 7;
    public int MinDistanceForTransportSlot => minDistanceForTransportSlot;

    [Header("Logistica")]
    [Tooltip("Hard Mode: m�ximo de unidades de log�stica em Hard Mode ligado.")]
    [SerializeField, Range(0, 10)] private int maxLogisticUnitsOnHardMode = 1;
    public int MaxLogisticUnitsOnHardMode => maxLogisticUnitsOnHardMode;

    [Header("Elite Demand")]
    [Tooltip("entre Assalto e Fogo Indireto quando ainda h� press�o terrestre aberta. Modo normal.")]
    [SerializeField, Range(0f, 1f)] private float eliteRatioNormalPressure = 0.33f;
    [Tooltip("quando as press�es anti-tank E anti-infantaria j� est�o cobertas (folga vira superioridade qualitativa). Modo normal.")]
    [SerializeField, Range(0f, 1f)] private float eliteRatioNormalSafe = 0.50f;
    [Tooltip("entre Assalto e Fogo Indireto quando ainda h� press�o terrestre aberta. Hard Mode.")]
    [SerializeField, Range(0f, 1f)] private float eliteRatioHardPressure = 0.60f;
    [Tooltip("quando as press�es anti-tank E anti-infantaria j� est�o cobertas . Hard Mode.")]
    [SerializeField, Range(0f, 1f)] private float eliteRatioHardSafe = 0.80f;
    [Tooltip("Quantos turnos de renda a IA topa POUPAR mirando um elite/capacidade. 0 = nunca poupa (guloso). Modo normal.")]
    [SerializeField, Range(0, 4)] private int eliteSaveTurnsNormal = 1;
    [Tooltip("TQuantos turnos de renda a IA topa POUPAR mirando um elite/capacidade. Hard Mode (subir ajuda a alcançar elites mais caros).")]
    [SerializeField, Range(0, 4)] private int eliteSaveTurnsHard = 1;
    [Tooltip("Margem de caixa (%) mantida como troco ENQUANTO poupa pra elite, pra ainda comprar coisas baratas. Escalada pela maturidade do ex�rcito. Modo normal.")]
    [SerializeField, Range(0f, 50f)] private float eliteMaintenanceReserveNormal = 20f;
    [Tooltip("Margem de troco (%) durante a poupan�a de elite. Hard Mode.")]
    [SerializeField, Range(0f, 50f)] private float eliteMaintenanceReserveHard = 20f;
    
    // Resolve Quais variaveis usar conforme o modo atual (normal ou hard)
    public float EliteRatioPressure => hardMode ? eliteRatioHardPressure : eliteRatioNormalPressure;
    public float EliteRatioSafe     => hardMode ? eliteRatioHardSafe     : eliteRatioNormalSafe;
    public int   EliteSaveTurns      => hardMode ? eliteSaveTurnsHard      : eliteSaveTurnsNormal;
    public float EliteMaintenanceReservePercent => hardMode ? eliteMaintenanceReserveHard : eliteMaintenanceReserveNormal;

    [Header("Composi��o m�nima do Ex�rcito (Elite Gate)")]
    [Tooltip("N�cleo operacional que libera compra de elite capturadores (infantaria) exigidos. Modo normal.")]
    [SerializeField, Range(0, 12)] private int minInfantryNormal = 2;
    [Tooltip("N�cleo operacional unidades de Assalto exigidas. Modo normal.")]
    [SerializeField, Range(0, 12)] private int minAssaultNormal = 2;
    [Tooltip("N�cleo operacional unidades de Artilharia/Fogo Indireto exigidas. Modo normal.")]
    [SerializeField, Range(0, 12)] private int minArtilleryNormal = 1;
    [Tooltip("N�cleo operacional capturadores (infantaria) exigidos. Hard Mode.")]
    [SerializeField, Range(0, 12)] private int minInfantryHard = 4;
    [Tooltip("N�cleo operacional Assalto exigido. Hard Mode (0 = n�o exige; tank b�sico costuma estar banido).")]
    [SerializeField, Range(0, 12)] private int minAssaultHard = 0;
    [Tooltip("N�cleo operacional Artilharia exigida. Hard Mode (0 = n�o exige; artilharia b�sica costuma estar banida).")]
    [SerializeField, Range(0, 12)] private int minArtilleryHard = 0;
    
    // Resolve a composi��o m�nima do N�cleo conforme o modo atual.
    public int CoreMinInfantry  => hardMode ? minInfantryHard  : minInfantryNormal;
    public int CoreMinAssault   => hardMode ? minAssaultHard   : minAssaultNormal;
    public int CoreMinArtillery => hardMode ? minArtilleryHard : minArtilleryNormal;

    [Tooltip("Gate de núcleo SUAVE: em vez de banir elite até a composição mínima fechar (muro), a maturidade do núcleo (0..1) vira PESO no score. Corrige o incentivo perverso do gate duro, que ao mesmo tempo bane o elite E desliga a penalidade anti-barato — fazendo a IA comprar a artilharia mais fraca só para 'pagar o imposto' e destravar a cancela. Off = comportamento atual.")]
    [SerializeField] private bool softCoreGate = false;
    public bool SoftCoreGate => softCoreGate;

    [Header("Plano de Objetivos")]
    [Tooltip("M�ximo de objetivos ofensivos simult�neos (Pending/Pursuing/Capturing). Limita demand de capturadores em mapas grandes.")]
    [SerializeField, Range(1, 12)] private int maxActiveObjectives = 4;
    public int MaxActiveObjectives => maxActiveObjectives;


    // Ativa��es de vari�eis
    private static AIController _instance;
    public static AIController Instance => _instance;
    public static bool ShowAIHUD => _instance != null && _instance.showAIUnitHUD;

    private readonly HashSet<Vector3Int> plannedDestinations = new HashSet<Vector3Int>();
    // Objetivos de captura escolhidos nesta passada pela faccao sem QG. Nao se
    // confunde com plannedDestinations: aquele conjunto reserva a celula onde a
    // unidade terminara o movimento; este reserva o predio distante para que a
    // proxima unidade rebelde varra a bolha seguinte.
    private readonly HashSet<Vector3Int> rebelCaptureTargetReservations = new HashSet<Vector3Int>();
    // Intencoes calculadas nesta decisao. So viram estado persistente da
    // unidade depois que o batch retorna comprometido com sucesso.
    private readonly Dictionary<int, ConstructionManager>
        pendingRebelCaptureTargets =
            new Dictionary<int, ConstructionManager>();

    // Buffers e caches para otimiza��o de performance
    private readonly List<UnitManager> _availableUnitsBuffer = new List<UnitManager>();
    private readonly Dictionary<int, int> _groupCache = new Dictionary<int, int>();
    private readonly Dictionary<int, float> _availableDistanceCache =
        new Dictionary<int, float>();
    private readonly Dictionary<int, InitiativeSortFacts> _initiativeSortFacts =
        new Dictionary<int, InitiativeSortFacts>();
    private static readonly System.Text.StringBuilder _initLogBuilder = new System.Text.StringBuilder();
    private Comparison<UnitManager> _availableUnitsComparison;
    private Comparison<UnitManager> _initiativeComparison;
    private TeamId _sortAiTeam;
    private TeamObjectivePlan _sortActivePlan;
    // Contexto de invasão para a ordenação de iniciativa: durante a invasão, feridas saem na frente.
    private bool _sortIsInvading;

    private sealed class InitiativeSortFacts
    {
        public bool IsBlocker;
        public bool HasFireSupportAttack;
        public bool HasFireSupportPrimaryTarget;
        public int FireSupportElite;
        public BazookaTargetPriority FireSupportPreference;
        public bool HasCombatOpportunity;
        public SectorObjective AssignedObjective;
        public float TransportDistance = float.MaxValue;
        public int EliteLevel;
        public int EffectiveInitiative;
    }

    // Estado da IA
    private bool   isActive;
    private bool   isDebugPaused;
    // Pause de JOGADOR (menu in-game aberto durante o turno da IA). Separado do isDebugPaused (F10):
    // sem maquinaria de AI STEP/RESUME — só segura o loop enquanto o menu do jogador está aberto.
    private bool   isPlayerPaused;
    private bool   isDebugShoppingPaused;
    private Coroutine aiCoroutine;
    private int    aiTurnNumber;
    private string aiTeamTag;

    [Header("Em tempo de execu��o (Debug)")]
    [SerializeField, Range(0, 4)] private int currentAIStage;
    [SerializeField] private TeamId currentAITeam = TeamId.Neutral;
    [SerializeField] private int currentAISlotIndex = -1;

    // Debugging
    private enum DebugStepRequest
    {
        None,
        Prepare,
        Execute
    }

    private DebugStepRequest debugStepRequest;
    private PlayerAction debugStepPendingAction;

    private string TL(string category = "")
        => string.IsNullOrEmpty(category)
            ? $"[AI {aiTeamTag}][T{aiTurnNumber}]"
            : $"[AI {aiTeamTag}][T{aiTurnNumber}][{category}]";

    internal static int ResolveAISlotKey(TeamId visualTeam)
    {
        MatchController match = UnityEngine.Object.FindAnyObjectByType<MatchController>();
        if (match == null)
            return -1;
        PlayerSlotId activeSlot = match.ActiveSlotId;
        if (match.IsValidPlayerSlot(activeSlot) &&
            match.GetVisualTeamForSlot(activeSlot) == visualTeam)
            return activeSlot.Value;
        return match.TryGetUniqueSlotForTeam(visualTeam, out PlayerSlotId uniqueSlot)
            ? uniqueSlot.Value
            : -1;
    }

    // Propriedades p�blicas
  
    public static bool IsDebugPaused { get; private set; }
    public static bool IsDebugShoppingPaused { get; private set; }
    public bool IsAIRuntimeActive => isActive;
    public int CurrentAIStage => currentAIStage;
    public TeamId CurrentAITeam => currentAITeam;
    public PlayerSlotId CurrentAISlotId => PlayerSlotId.FromIndex(currentAISlotIndex);
    public int CurrentAITurnNumber => aiTurnNumber;

    public void RestoreAIRuntimeState(bool active, TeamId team, int turnNumber, int stage)
    {
        int slotIndex = -1;
        if (matchController != null &&
            matchController.TryGetUniqueSlotForTeam(team, out PlayerSlotId migratedSlot))
            slotIndex = migratedSlot.Value;
        RestoreAIRuntimeState(active, PlayerSlotId.FromIndex(slotIndex), team, turnNumber, stage);
    }

    public void RestoreAIRuntimeState(
        bool active,
        PlayerSlotId slot,
        TeamId team,
        int turnNumber,
        int stage)
    {
        isActive = active;
        currentAISlotIndex = slot.Value;
        currentAITeam = team;
        aiTurnNumber = turnNumber;
        currentAIStage = Mathf.Clamp(stage, 0, 4);
        aiTeamTag = team == TeamId.Neutral ? string.Empty : TeamUtils.GetName(team).ToUpper();
    }

}
