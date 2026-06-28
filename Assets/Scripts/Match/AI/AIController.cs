using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
// Controla a IA inimiga, incluindo a execu��o de suas a��es, planejamento de objetivos e tomada de decis�es.
// Implementa uma abordagem baseada em est�gios para organizar o comportamento da IA,
// desde a avalia��o do estado do jogo at� a execu��o de a��es espec�ficas.

/// </summary>
public partial class AIController : MonoBehaviour
{
    [SerializeField] private MatchController matchController;
    [SerializeField] private ReplayManager replayManager;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    [Header("AI HUD")]
    [SerializeField] private bool showAIUnitHUD;

    [Header("AI Debug")]
    [Tooltip("Inicia a partida com a IA pausada, equivalente a pressionar F10.")]
    [SerializeField] private bool startOnPause;

    [Header("Hard Mode")]
    [Tooltip("Modo difícil. Por enquanto: dobra os slots de capturador por setor e habilita os limites/banimentos específicos de hard mode (logística e unidades banidas).")]
    [SerializeField] private bool hardMode = false;
    public bool HardMode => hardMode;

    [Header("AI Stage Emulation")]
    [SerializeField] private bool emulateStage0 = true;
    [SerializeField] private bool emulateStage1 = true;
    [SerializeField] private bool emulateStage2 = true;
    [SerializeField] private bool emulateStage3 = true;
    [SerializeField] private bool emulateStage4 = true;

    [Header("Captura")]
    [Tooltip("Impacto do risco na tomada de decisão (0 = ignora risco, 2 = risco pesa muito)")]
    [SerializeField, Range(0f, 2f)] private float riskDecisionImpact = 0.5f;
    public float RiskDecisionImpact => riskDecisionImpact;

    [Tooltip("Raio em hexes para criar objetivo defensivo num setor conquistado quando inimigo está próximo")]
    [SerializeField, Range(1, 8)] private int defenseEnemyRange = 3;
    public int DefenseEnemyRange => defenseEnemyRange;

    [Tooltip("Multiplicador de recrutamento: rogues dentro de defenseEnemyRange × defenseCallRange hexes são convocados para defesa")]
    [SerializeField, Range(1, 8)] private int defenseCallRange = 4;
    public int DefenseCallRange => defenseCallRange;

    [Tooltip("Raio em hexes para detectar inimigos próximos ao alvo de captura e acionar reforço SOS")]
    [SerializeField, Range(1, 8)] private int alliesEnemyRange = 2;
    public int AlliesEnemyRange => alliesEnemyRange;

    [Tooltip("Raio em hexes para recrutar rogues como reforço SOS em captura com desvantagem")]
    [SerializeField, Range(1, 16)] private int alliesCallRange = 8;
    public int AlliesCallRange => alliesCallRange;

    [Tooltip("Ratio inimigo HP / aliado HP para acionar reforço SOS numa captura (ex: 2 = inimigo tem o dobro do HP)")]
    [SerializeField, Range(1f, 5f)] private float alliesAgainstEnemiesHpRatio = 2f;
    public float AlliesAgainstEnemiesHpRatio => alliesAgainstEnemiesHpRatio;

    [Header("Transporte")]
    [Tooltip("Distância mínima em hexes do HQ para que um setor gere slot de transportador no plano e no shopping")]
    [SerializeField, Range(1, 30)] private int minDistanceForTransportSlot = 7;
    public int MinDistanceForTransportSlot => minDistanceForTransportSlot;

    [Header("Logistics")]
    [Tooltip("Hard Mode: número máximo de unidades de logística que a IA mantém em campo enquanto o Hard Mode estiver ligado.")]
    [SerializeField, Range(0, 10)] private int maxLogisticUnitsOnHardMode = 1;
    public int MaxLogisticUnitsOnHardMode => maxLogisticUnitsOnHardMode;

    [Header("Elite Demand")]
    [Tooltip("Fração-alvo de elites (entre Assalto e Fogo Indireto) quando ainda há pressão terrestre aberta. Modo normal.")]
    [SerializeField, Range(0f, 1f)] private float eliteRatioNormalPressure = 0.33f;
    [Tooltip("Fração-alvo de elites quando as pressões anti-tank E anti-infantaria já estão cobertas (folga vira superioridade qualitativa). Modo normal.")]
    [SerializeField, Range(0f, 1f)] private float eliteRatioNormalSafe = 0.50f;
    [Tooltip("Fração-alvo de elites com pressão terrestre aberta. Hard Mode.")]
    [SerializeField, Range(0f, 1f)] private float eliteRatioHardPressure = 0.15f;
    [Tooltip("Fração-alvo de elites com pressões terrestres cobertas. Hard Mode.")]
    [SerializeField, Range(0f, 1f)] private float eliteRatioHardSafe = 0.33f;
    [Tooltip("Quantos turnos de renda a IA topa POUPAR mirando um elite/capacidade crítica. 0 = nunca poupa (guloso). Modo normal.")]
    [SerializeField, Range(0, 4)] private int eliteSaveTurnsNormal = 1;
    [Tooltip("Turnos de poupança rumo a um elite. Hard Mode (subir ajuda a alcançar elites mais caros).")]
    [SerializeField, Range(0, 4)] private int eliteSaveTurnsHard = 1;
    [Tooltip("Margem de caixa (%) mantida como troco ENQUANTO poupa pra elite, pra ainda comprar coisas baratas. Escalada pela maturidade do exército. Modo normal.")]
    [SerializeField, Range(0f, 50f)] private float eliteMaintenanceReserveNormal = 20f;
    [Tooltip("Margem de troco (%) durante a poupança de elite. Hard Mode.")]
    [SerializeField, Range(0f, 50f)] private float eliteMaintenanceReserveHard = 20f;
    // Resolve conforme o modo atual.
    public float EliteRatioPressure => hardMode ? eliteRatioHardPressure : eliteRatioNormalPressure;
    public float EliteRatioSafe     => hardMode ? eliteRatioHardSafe     : eliteRatioNormalSafe;
    public int   EliteSaveTurns      => hardMode ? eliteSaveTurnsHard      : eliteSaveTurnsNormal;
    public float EliteMaintenanceReservePercent => hardMode ? eliteMaintenanceReserveHard : eliteMaintenanceReserveNormal;

    [Header("Minimum Army Composition (Elite Gate)")]
    [Tooltip("Núcleo operacional que libera compra de elite — capturadores (infantaria) exigidos. Modo normal.")]
    [SerializeField, Range(0, 12)] private int minInfantryNormal = 2;
    [Tooltip("Núcleo operacional — unidades de Assalto exigidas. Modo normal.")]
    [SerializeField, Range(0, 12)] private int minAssaultNormal = 2;
    [Tooltip("Núcleo operacional — unidades de Artilharia/Fogo Indireto exigidas. Modo normal.")]
    [SerializeField, Range(0, 12)] private int minArtilleryNormal = 1;
    [Tooltip("Núcleo operacional — capturadores (infantaria) exigidos. Hard Mode.")]
    [SerializeField, Range(0, 12)] private int minInfantryHard = 4;
    [Tooltip("Núcleo operacional — Assalto exigido. Hard Mode (0 = não exige; tank básico costuma estar banido).")]
    [SerializeField, Range(0, 12)] private int minAssaultHard = 0;
    [Tooltip("Núcleo operacional — Artilharia exigida. Hard Mode (0 = não exige; artilharia básica costuma estar banida).")]
    [SerializeField, Range(0, 12)] private int minArtilleryHard = 0;
    // Resolve a composição mínima do núcleo conforme o modo atual.
    public int CoreMinInfantry  => hardMode ? minInfantryHard  : minInfantryNormal;
    public int CoreMinAssault   => hardMode ? minAssaultHard   : minAssaultNormal;
    public int CoreMinArtillery => hardMode ? minArtilleryHard : minArtilleryNormal;

    [Header("Plano de Objetivos")]
    [Tooltip("Máximo de objetivos ofensivos simultâneos (Pending/Pursuing/Capturing). Limita demand de capturadores em mapas grandes.")]
    [SerializeField, Range(1, 12)] private int maxActiveObjectives = 4;
    public int MaxActiveObjectives => maxActiveObjectives;


    private static AIController _instance;
    public static AIController Instance => _instance;
    public static bool ShowAIHUD => _instance != null && _instance.showAIUnitHUD;

    private readonly HashSet<Vector3Int> plannedDestinations = new HashSet<Vector3Int>();

    // Buffers reutilizáveis — evitam alocações por fase/iteração
    private readonly List<UnitManager> _availableUnitsBuffer = new List<UnitManager>();
    private readonly Dictionary<int, int> _groupCache = new Dictionary<int, int>();
    private static readonly System.Text.StringBuilder _initLogBuilder = new System.Text.StringBuilder();
    private Comparison<UnitManager> _availableUnitsComparison;
    private Comparison<UnitManager> _initiativeComparison;
    private TeamId _sortAiTeam;
    private TeamObjectivePlan _sortActivePlan;

    private bool   isActive;
    private bool   isDebugPaused;
    private bool   isDebugShoppingPaused;
    private Coroutine aiCoroutine;
    private int    aiTurnNumber;
    private string aiTeamTag;
    [SerializeField, Range(0, 4)] private int currentAIStage;
    [SerializeField] private TeamId currentAITeam = TeamId.Neutral;
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

    public static bool IsDebugPaused { get; private set; }
    public static bool IsDebugShoppingPaused { get; private set; }
    public bool IsAIRuntimeActive => isActive;
    public int CurrentAIStage => currentAIStage;
    public TeamId CurrentAITeam => currentAITeam;
    public int CurrentAITurnNumber => aiTurnNumber;

    public void RestoreAIRuntimeState(bool active, TeamId team, int turnNumber, int stage)
    {
        isActive = active;
        currentAITeam = team;
        aiTurnNumber = turnNumber;
        currentAIStage = Mathf.Clamp(stage, 0, 4);
        aiTeamTag = team == TeamId.Neutral ? string.Empty : TeamUtils.GetName(team).ToUpper();
    }

}
