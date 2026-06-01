using System.Collections.Generic;
using UnityEngine;
//-------------------------------------------------------------------------
// Controla a IA inimiga, incluindo a execução de suas ações, planejamento de objetivos e tomada de decisões.
// Implementa uma abordagem baseada em estágios para organizar o comportamento da IA,
// desde a avaliação do estado do jogo até a execução de ações específicas. 
//-------------------------------------------------------------------------

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

        Debug.Log($"[AI] Start â€” matchController={matchController != null} replayManager={replayManager != null} turnStateManager={turnStateManager != null}");

        RefreshConstructionHudAfterAIHudReady();

        // OnActiveTeamChanged pode ter disparado antes do Awake (raro mas possÃ­vel).

        // Verifica se o time jÃ¡ ativo Ã© IA e inicia o turno se necessÃ¡rio.

        if (matchController != null && matchController.IsPlayerAI(matchController.ActiveTeam))

        {

            Debug.Log($"[AI] Start â€” time ativo ({matchController.ActiveTeam}) jÃ¡ Ã© IA, iniciando turno.");

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

        Debug.Log($"[AI] HandleTeamChanged â€” teamIndex={teamIndex} newTeam={newTeam} matchController={matchController != null} isAI={aiCheck}");

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

            if (currentAIStage < 4)
            {
                currentAIStage = 0;
                currentAITeam = TeamId.Neutral;
            }

        }

    }
}
