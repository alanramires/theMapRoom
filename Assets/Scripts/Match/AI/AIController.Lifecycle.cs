using UnityEngine;

public partial class AIController
{
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
        currentAIStage = 0;
        currentAITeam = TeamId.Neutral;
        aiCoroutine = null;
        debugStepPendingAction = null;
        debugStepRequest = DebugStepRequest.None;
        if (IsDebugPaused) IsDebugPaused = false;
        if (IsDebugShoppingPaused) IsDebugShoppingPaused = false;
        PanelDialogController.ClearExternalText();
        Debug.Log($"[AI] Partida encerrada ({context}); IA interrompida.");
    }

    // -------------------------------------------------------------------------

    // Lifecycle

    // -------------------------------------------------------------------------

    private void Awake()

    {

        _instance = this;
        MatchController.OnActiveTeamChanged += HandleTeamChanged;

    }

    private void Start()

    {

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

        Debug.Log($"[AI] Start — matchController={matchController != null} replayManager={replayManager != null} turnStateManager={turnStateManager != null}");

        // OnActiveTeamChanged pode ter disparado antes do Awake (raro mas possível).

        // Verifica se o time já ativo é IA e inicia o turno se necessário.

        if (matchController != null && matchController.IsPlayerAI(matchController.ActiveTeam))

        {

            Debug.Log($"[AI] Start — time ativo ({matchController.ActiveTeam}) já é IA, iniciando turno.");

            HandleTeamChanged((int)matchController.ActiveTeam);

        }

    }

    private void OnDestroy()

    {

        MatchController.OnActiveTeamChanged -= HandleTeamChanged;

        if (aiCoroutine != null) StopCoroutine(aiCoroutine);

        if (IsDebugPaused) IsDebugPaused = false;

        if (IsDebugShoppingPaused) IsDebugShoppingPaused = false;

    }

    // -------------------------------------------------------------------------

    // Entrada do turno

    // -------------------------------------------------------------------------

    private void HandleTeamChanged(int teamIndex)

    {

        TeamId newTeam = (TeamId)teamIndex;

        bool aiCheck = matchController != null && matchController.IsPlayerAI(newTeam);

        Debug.Log($"[AI] HandleTeamChanged — teamIndex={teamIndex} newTeam={newTeam} matchController={matchController != null} isAI={aiCheck}");

        if (matchController == null) return;
        if (matchController.HasVictoryWinner)
        {
            StopAIForMatchEnd("team_changed");
            return;
        }

        if (aiCheck)

        {

            isActive = true;

            if (aiCoroutine != null) StopCoroutine(aiCoroutine);

            aiCoroutine = StartCoroutine(RunAITurn(newTeam));

        }

        else

        {

            isActive = false;

            currentAIStage = 0;

            currentAITeam = TeamId.Neutral;

        }

    }
}
