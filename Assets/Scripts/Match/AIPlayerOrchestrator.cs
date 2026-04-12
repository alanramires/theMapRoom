using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;

/// <summary>
/// O Cérebro da Inteligência Artificial V1.
/// Ele escuta as mudancas de Turno e, se for a vez do computador, itera pelas pecas ativas
/// e emite PlayerActions diretamente pro ReplayManager jogar na via Rapida usando as mesmas
/// regras e animacoes aplicadas a humanos.
/// </summary>
public class AIPlayerOrchestrator : MonoBehaviour
{
    [SerializeField] private MatchController matchController;
    [SerializeField] private ReplayManager replayManager;
    [SerializeField] private TurnStateManager turnStateManager;

    [Header("Dependencies")]
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    private bool isActiveAndPlaying;
    private Coroutine aiExecutionRoutine;

    private void Start()
    {
        if (matchController == null) matchController = FindAnyObjectByType<MatchController>();
        if (replayManager == null) replayManager = FindAnyObjectByType<ReplayManager>();
        if (turnStateManager == null) turnStateManager = FindAnyObjectByType<TurnStateManager>();
        if (boardTilemap == null)
        {
            var cursorControl = FindAnyObjectByType<CursorController>();
            if (cursorControl != null) boardTilemap = cursorControl.BoardTilemap;
        }
        if (terrainDatabase == null) terrainDatabase = Resources.Load<TerrainDatabase>("TerrainDatabase");

        MatchController.OnActiveTeamChanged += HandleTeamChanged;
    }

    private void OnDestroy()
    {
        MatchController.OnActiveTeamChanged -= HandleTeamChanged;

        if (aiExecutionRoutine != null)
            StopCoroutine(aiExecutionRoutine);
    }

    private void HandleTeamChanged(int teamIndex)
    {
        if (matchController == null) return;

        TeamId newTeam = (TeamId)teamIndex;
        if (matchController.IsPlayerAI(newTeam))
        {
            Debug.Log($"[AI] Turno da IA comecou: {newTeam}");
            isActiveAndPlaying = true;

            if (aiExecutionRoutine != null)
                StopCoroutine(aiExecutionRoutine);
            aiExecutionRoutine = StartCoroutine(AIBatchExecutionLoop());
        }
        else
        {
            isActiveAndPlaying = false;
        }
    }

    private IEnumerator AIBatchExecutionLoop()
    {
        while (isActiveAndPlaying)
        {
            float batchDelay = replayManager != null
                ? replayManager.GetEffectiveTimeBetweenBatchesForAutoplay()
                : 0.5f;
            if (batchDelay > 0f)
                yield return new WaitForSeconds(batchDelay);

            // 1. Filtra unidades aptas deste time
            TeamId myTeam = matchController.ActiveTeam;
            List<UnitManager> myAvailableUnits = UnitManager.AllActive
                .Where(u => u.TeamId == myTeam && !u.HasActed && !u.IsDead && !u.IsEmbarked && !u.HasMerged)
                .ToList();

            // 2. Sem unidades disponíveis → passa o turno
            if (myAvailableUnits.Count == 0)
            {
                Debug.Log("[AI] Nenhuma unidade apta restante. Passando o Turno.");
                isActiveAndPlaying = false;
                matchController.AdvanceTurnWithTransition();
                break;
            }

            // 3. Escolhe a próxima unidade e calcula destino
            UnitManager selectedUnit = myAvailableUnits[0]; // TODO: priorizar por tática
            Vector3Int fromCell = selectedUnit.CurrentCellPosition;
            fromCell.z = 0;

            Dictionary<Vector3Int, List<Vector3Int>> validPaths = UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap,
                selectedUnit,
                Mathf.Max(0, selectedUnit.RemainingMovementPoints),
                terrainDatabase);

            // Snapshot das células ocupadas agora (exclui a própria unidade)
            HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
            foreach (var u in UnitManager.AllActive)
            {
                if (u == selectedUnit || u.IsEmbarked) continue;
                Vector3Int pos = u.CurrentCellPosition;
                pos.z = 0;
                occupiedCells.Add(pos);
            }

            Vector3Int destinationCell = fromCell;
            if (validPaths != null && validPaths.Count > 0)
            {
                List<Vector3Int> freeCells = validPaths.Keys
                    .Where(c => !occupiedCells.Contains(c))
                    .ToList();

                if (freeCells.Count > 0)
                    destinationCell = freeCells[Random.Range(0, freeCells.Count)];
            }

            // 4. Monta o batch
            PlayerAction actionBatch = new PlayerAction
            {
                IsAIGenerated = true,
                ActionType = PlayerActionType.UnitAction,
                ActingTeam = myTeam,
                TurnNumber = matchController.CurrentTurn,

                CursorHex = fromCell,
                HasCursorHex = true,

                UnitInstanceId = selectedUnit.InstanceId.ToString(),

                MoveFrom = fromCell,
                MoveTo = destinationCell,
                HasMoveFrom = true,
                HasMoveTo = true,

                SensorAction = SensorActionType.None // Menu: Wait
            };

            Debug.Log($"[AI] Batch {myAvailableUnits.Count} unidades restantes | Soldado {selectedUnit.InstanceId}: {fromCell} -> {destinationCell}");

            // 5. Executa e aguarda o ReplayManager liberar
            if (replayManager == null)
            {
                Debug.LogError("[AI] ReplayManager nulo!");
                break;
            }

            replayManager.ExecuteLiveAIBatch(actionBatch);
            yield return new WaitUntil(() => !replayManager.IsStepExecutionBusy);
        }

        aiExecutionRoutine = null;
    }
}
