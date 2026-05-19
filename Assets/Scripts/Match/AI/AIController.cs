using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Cérebro da IA V2. Orquestra as 4 fases do turno via coroutine,
/// usando HexEvaluator para posicionamento e ExecuteLiveAIBatch para execução.
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

    [Header("Plano de Objetivos")]
    [Tooltip("Máximo de objetivos ofensivos simultâneos (Pending/Pursuing/Capturing). Limita demand de capturadores em mapas grandes.")]
    [SerializeField, Range(1, 12)] private int maxActiveObjectives = 4;
    public int MaxActiveObjectives => maxActiveObjectives;


    private static AIController _instance;
    public static AIController Instance => _instance;
    public static bool ShowAIHUD => _instance != null && _instance.showAIUnitHUD;

    private readonly HashSet<Vector3Int> plannedDestinations = new HashSet<Vector3Int>();

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
