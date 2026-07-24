using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//-------------------------------------------------------------------------
// Controla a IA inimiga, incluindo a execu��o de suas a��es, planejamento de objetivos e tomada de decis�es.
// Implementa uma abordagem baseada em est�gios para organizar o comportamento da IA,
// desde a avalia��o do estado do jogo at� a execu��o de a��es espec�ficas. 
//-------------------------------------------------------------------------

public partial class AIController
{
    private GUIStyle aiTurnIndicatorTitleStyle;
    private GUIStyle aiTurnIndicatorStageStyle;
    private GUIStyle aiTurnIndicatorBoxStyle;
    private bool aiTurnBatchExecuting;
    private Coroutine postLoadResumeRoutine;

    private bool IsMatchEnded()
    {
        return matchController != null && matchController.HasVictoryWinner;
    }

    private bool ShouldStopAIForMatchEnd(string context)
    {
        if (!IsMatchEnded())
            return false;

        StopAIForMatchEnd(context);
        return true;
    }

    private void StopAIForMatchEnd(string context)
    {
        isActive = false;
        aiTurnBatchExecuting = false;
        currentAIStage = 0;
        currentAITeam = TeamId.Neutral;
        currentAISlotIndex = -1;
        aiCoroutine = null;
        debugStepPendingAction = null;
        debugStepRequest = DebugStepRequest.None;
        if (IsDebugPaused) IsDebugPaused = false;
        if (IsDebugShoppingPaused) IsDebugShoppingPaused = false;
        PanelDialogController.ClearExternalText();
        if (showAILogs)
            Debug.Log($"[AI] Partida encerrada ({context}); IA interrompida.");
    }

    private void OnGUI()
    {
        if (matchController == null || matchController.HasVictoryWinner ||
            !matchController.IsActiveTeamAI())
            return;
        if (currentAIStage == 2 && aiTurnBatchExecuting)
            return;

        EnsureAITurnIndicatorStyles();
        float width = Mathf.Clamp(Screen.width * 0.34f, 250f, 440f);
        float height = Mathf.Clamp(Screen.height * 0.085f, 58f, 90f);
        Rect panel = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);
        float pulse = 0.68f + 0.32f * (0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 5f));
        Color team = TeamUtils.GetColor(matchController.ActiveTeam);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.025f, 0.06f, 0.075f, 0.88f * pulse);
        GUI.Box(panel, GUIContent.none, aiTurnIndicatorBoxStyle);

        GUI.color = new Color(1f, 1f, 1f, pulse);
        aiTurnIndicatorTitleStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.027f, 18f, 32f));
        aiTurnIndicatorTitleStyle.normal.textColor = Color.Lerp(team, Color.white, 0.28f);
        GUI.Label(new Rect(panel.x + 8f, panel.y + 4f, panel.width - 16f, panel.height * 0.55f),
            "TURNO DA IA", aiTurnIndicatorTitleStyle);

        aiTurnIndicatorStageStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.017f, 12f, 20f));
        aiTurnIndicatorStageStyle.normal.textColor = Color.Lerp(team, Color.white, 0.5f);
        GUI.Label(new Rect(panel.x + 8f, panel.y + panel.height * 0.5f, panel.width - 16f, panel.height * 0.42f),
            ResolveAITurnIndicatorStage(), aiTurnIndicatorStageStyle);
        GUI.color = previousColor;
    }

    private void EnsureAITurnIndicatorStyles()
    {
        if (aiTurnIndicatorBoxStyle == null)
            aiTurnIndicatorBoxStyle = new GUIStyle(GUI.skin.box);
        if (aiTurnIndicatorTitleStyle == null)
        {
            aiTurnIndicatorTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }
        if (aiTurnIndicatorStageStyle == null)
        {
            aiTurnIndicatorStageStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }
    }

    private string ResolveAITurnIndicatorStage()
    {
        switch (currentAIStage)
        {
            case 0: return "PREPARANDO...";
            case 1: return "SERVICOS DO COMANDO...";
            case 2: return "CALCULANDO MOVIMENTOS...";
            case 3: return "ORGANIZANDO COMPRAS...";
            case 4: return "ENCERRANDO TURNO...";
            default: return "CALCULANDO...";
        }
    }

    // -------------------------------------------------------------------------

    // Lifecycle

    // -------------------------------------------------------------------------

    private void Awake()

    {

        _instance = this;
        MatchController.OnActiveTeamChanged += HandleTeamChanged;
        SaveGameManager.OnAfterLoadSuccess += HandleAfterLoadSuccess;
        _availableUnitsComparison = CompareAvailableUnits;
        _initiativeComparison = CompareUnitsByInitiative;

    }

    private void Start()

    {

        // Dificuldade escolhida na Tela de Entrada (consumida uma vez por partida nova).
        if (PartidaConfig.TryConsumeDifficulty(out AIDifficulty pendingDifficulty))
            ApplyDifficulty(pendingDifficulty);

        if (matchController == null)  matchController  = FindAnyObjectByType<MatchController>();

        if (replayManager == null)    replayManager    = FindAnyObjectByType<ReplayManager>();

        if (turnStateManager == null) turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (turnStateManager != null && turnStateManager.MovementTilemapRef != null)
        {
            boardTilemap = turnStateManager.MovementTilemapRef;
        }
        else if (boardTilemap == null)
        {
            CursorController cursor = FindAnyObjectByType<CursorController>();
            if (cursor != null) boardTilemap = cursor.BoardTilemap;
        }

        if (terrainDatabase == null)

            terrainDatabase = Resources.Load<TerrainDatabase>("TerrainDatabase");

        if (showAILogs)
            Debug.Log($"[AI] Start — matchController={matchController != null} replayManager={replayManager != null} turnStateManager={turnStateManager != null}");

        if (startOnPause)
        {
            SetDebugPaused(true);
            if (showAILogs)
                Debug.Log("[AI] Start on Pause ativo - estado inicial equivalente ao F10.");
        }

        RefreshConstructionHudAfterAIHudReady();

        // OnActiveTeamChanged pode ter disparado antes do Awake (raro mas possível).

        // Verifica se o time já ativo é IA e inicia o turno se necessário.

        if (matchController != null && matchController.IsActiveTeamAI())

        {

            if (showAILogs)
                Debug.Log($"[AI] Start — time ativo ({matchController.ActiveTeam}) já é IA, iniciando turno.");

            HandleTeamChanged((int)matchController.ActiveTeam);

        }

    }

    private static void RefreshConstructionHudAfterAIHudReady()
    {
        List<ConstructionManager> constructions = ConstructionManager.AllActive;
        if (constructions == null || constructions.Count == 0)
            return;

        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;

            construction.RefreshRuntimeVisualState(force: true);
        }
    }
    private void OnDestroy()

    {

        MatchController.OnActiveTeamChanged -= HandleTeamChanged;
        SaveGameManager.OnAfterLoadSuccess -= HandleAfterLoadSuccess;

        if (aiCoroutine != null) StopCoroutine(aiCoroutine);
        if (postLoadResumeRoutine != null) StopCoroutine(postLoadResumeRoutine);

        if (IsDebugPaused) IsDebugPaused = false;

        if (IsDebugShoppingPaused) IsDebugShoppingPaused = false;

    }

    // -------------------------------------------------------------------------

    // Entrada do turno

    // -------------------------------------------------------------------------

    private void HandleAfterLoadSuccess()
    {
        if (postLoadResumeRoutine != null)
            StopCoroutine(postLoadResumeRoutine);
        postLoadResumeRoutine = StartCoroutine(ResumeActiveAITurnAfterLoad());
    }

    private IEnumerator ResumeActiveAITurnAfterLoad()
    {
        // OnAfterLoadSuccess dispara ainda dentro de LoadSlotAsync. Aguarda tambem a
        // restauracao dos logs/replay e a liberacao da apresentacao de turno.
        while (SaveGameManager.IsAnyLoadInProgress)
            yield return null;

        postLoadResumeRoutine = null;
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (replayManager == null)
            replayManager = FindAnyObjectByType<ReplayManager>();
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (matchController == null || matchController.HasVictoryWinner)
            yield break;

        TeamId activeTeam = matchController.ActiveTeam;
        if (!matchController.IsActiveTeamAI())
            yield break;

        // O load ja restaurou time, turno, stage, plano e snapshot confirmado. Reiniciamos
        // somente a coroutine operacional; nao reaplicamos inicio de turno nem seus efeitos.
        isActive = true;
        aiTurnBatchExecuting = false;
        if (aiCoroutine != null)
            StopCoroutine(aiCoroutine);
        aiCoroutine = StartCoroutine(RunAITurn(matchController.ActiveSlotId, activeTeam));

        Debug.Log($"[AI Stage] Pos-load: coroutine retomada para {activeTeam} "
            + $"no turno {aiTurnNumber}, stage {currentAIStage}, paused={isDebugPaused}.");
    }

    private void HandleTeamChanged(int teamIndex)

    {

        TeamId newTeam = (TeamId)teamIndex;

        bool aiCheck = matchController != null && matchController.IsActiveTeamAI();

        if (showAILogs)
            Debug.Log($"[AI] HandleTeamChanged — teamIndex={teamIndex} newTeam={newTeam} matchController={matchController != null} isAI={aiCheck}");

        if (matchController == null) return;
        if (matchController.HasVictoryWinner)
        {
            StopAIForMatchEnd("team_changed");
            return;
        }

        if (aiCheck)

        {
            aiTurnBatchExecuting = false;

            // Durante um load, NAO iniciar o turno: o estado da AI (stage/plano) ainda esta sendo
            // restaurado pelo LoadRoutine. Iniciar agora leria estado default -> resumeStage=0 ->
            // BuildObjectivePlan do zero (ignora o plano salvo). O load reinicia o turno no fim
            // (ForceReapplyActiveTeamWithTurnStart), ja com IsAnyLoadInProgress=false e o estado
            // restaurado -> resume correto. Paramos qualquer corrotina previa pra nao correr stale.
            if (SaveGameManager.IsAnyLoadInProgress)
            {
                if (aiCoroutine != null) { StopCoroutine(aiCoroutine); aiCoroutine = null; }
                if (showAILogs)
                    Debug.Log("[AI] HandleTeamChanged adiado: load em andamento; turno (re)inicia pos-restauracao.");
                return;
            }

            isActive = true;

            if (aiCoroutine != null) StopCoroutine(aiCoroutine);

            aiCoroutine = StartCoroutine(RunAITurn(matchController.ActiveSlotId, newTeam));

        }

        else

        {

            isActive = false;
            aiTurnBatchExecuting = false;

            if (currentAIStage < 4)
            {
                currentAIStage = 0;
                currentAITeam = TeamId.Neutral;
                currentAISlotIndex = -1;
            }

        }

    }
}
