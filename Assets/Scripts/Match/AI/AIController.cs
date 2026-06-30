using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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
    [Tooltip("Exibe os elementos de AI sobre as unidades, tais como plano, badge, eixo.")]
    [SerializeField] private bool showAIUnitHUD;

    [Tooltip("Inicia a partida com a IA pausada, equivalente a pressionar F10.")]
    [SerializeField] private bool startOnPause;

    [Tooltip("Modo difícil. Por enquanto: dobra os slots de capturador por setor e habilita os limites/banimentos espec�ficos de hard mode (log�stica e unidades banidas).")]
    [SerializeField] private bool hardMode = false;
    public bool HardMode => hardMode;

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
    [SerializeField, Range(0f, 1f)] private float eliteRatioHardPressure = 0.15f;
    [Tooltip("quando as press�es anti-tank E anti-infantaria j� est�o cobertas . Hard Mode.")]
    [SerializeField, Range(0f, 1f)] private float eliteRatioHardSafe = 0.33f;
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

    [Header("Plano de Objetivos")]
    [Tooltip("M�ximo de objetivos ofensivos simult�neos (Pending/Pursuing/Capturing). Limita demand de capturadores em mapas grandes.")]
    [SerializeField, Range(1, 12)] private int maxActiveObjectives = 4;
    public int MaxActiveObjectives => maxActiveObjectives;


    // Ativa��es de vari�eis
    private static AIController _instance;
    public static AIController Instance => _instance;
    public static bool ShowAIHUD => _instance != null && _instance.showAIUnitHUD;

    private readonly HashSet<Vector3Int> plannedDestinations = new HashSet<Vector3Int>();

    // Buffers e caches para otimiza��o de performance
    private readonly List<UnitManager> _availableUnitsBuffer = new List<UnitManager>();
    private readonly Dictionary<int, int> _groupCache = new Dictionary<int, int>();
    private static readonly System.Text.StringBuilder _initLogBuilder = new System.Text.StringBuilder();
    private Comparison<UnitManager> _availableUnitsComparison;
    private Comparison<UnitManager> _initiativeComparison;
    private TeamId _sortAiTeam;
    private TeamObjectivePlan _sortActivePlan;
    // Contexto de invasão para a ordenação de iniciativa: durante a invasão, feridas saem na frente.
    private bool _sortIsInvading;

    // Estado da IA
    private bool   isActive;
    private bool   isDebugPaused;
    private bool   isDebugShoppingPaused;
    private Coroutine aiCoroutine;
    private int    aiTurnNumber;
    private string aiTeamTag;

    [Header("Em tempo de execu��o (Debug)")]
    [SerializeField, Range(0, 4)] private int currentAIStage;
    [SerializeField] private TeamId currentAITeam = TeamId.Neutral;

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

    // Propriedades p�blicas
  
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
