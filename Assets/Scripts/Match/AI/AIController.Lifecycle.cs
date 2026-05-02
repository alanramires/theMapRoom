using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------

    // Lifecycle

    // -------------------------------------------------------------------------

    private void Awake()

    {

        MatchController.OnActiveTeamChanged += HandleTeamChanged;

    }

    private void Start()

    {

        if (matchController == null)  matchController  = FindAnyObjectByType<MatchController>();

        if (replayManager == null)    replayManager    = FindAnyObjectByType<ReplayManager>();

        if (turnStateManager == null) turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (boardTilemap == null)

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
