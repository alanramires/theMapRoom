using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;

/// <summary>
/// O Cérebro da Inteligência Artificial V1.
/// Fabrica PlayerActions idênticos aos gerados por humanos e os entrega ao ReplayManager.
/// Toda animação, sensor, fog of war e gravação de replay são herdados de graça.
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

    // Buffer reutilizável para não alocar listas a cada consulta
    private readonly List<PodeMirarTargetOption> _targetBuffer = new List<PodeMirarTargetOption>();

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Turno
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Loop principal
    // -------------------------------------------------------------------------

    private IEnumerator AIBatchExecutionLoop()
    {
        while (isActiveAndPlaying)
        {
            float batchDelay = replayManager != null
                ? replayManager.GetEffectiveTimeBetweenBatchesForAutoplay()
                : 0.5f;
            if (batchDelay > 0f)
                yield return new WaitForSeconds(batchDelay);

            // 1. Filtra unidades aptas
            TeamId myTeam = matchController.ActiveTeam;
            List<UnitManager> myAvailableUnits = UnitManager.AllActive
                .Where(u => u.TeamId == myTeam && !u.HasActed && !u.IsDead && !u.IsEmbarked && !u.HasMerged)
                .ToList();

            if (myAvailableUnits.Count == 0)
            {
                Debug.Log("[AI] Nenhuma unidade apta restante. Passando o Turno.");
                isActiveAndPlaying = false;
                matchController.AdvanceTurnWithTransition();
                break;
            }

            // 2. Decide a melhor ação para a próxima unidade
            UnitManager selectedUnit = myAvailableUnits[0]; // TODO: priorizar por tática
            PlayerAction actionBatch = DecideBatch(selectedUnit, myTeam);

            // 3. Executa e aguarda
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

    // -------------------------------------------------------------------------
    // Decisão
    // -------------------------------------------------------------------------

    private PlayerAction DecideBatch(UnitManager unit, TeamId myTeam)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        // Snapshot de células ocupadas agora (exclui a própria unidade)
        HashSet<Vector3Int> occupiedCells = BuildOccupiedSnapshot(unit);

        // Caminhos válidos de movimento
        Dictionary<Vector3Int, List<Vector3Int>> validPaths = UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap,
            unit,
            Mathf.Max(0, unit.RemainingMovementPoints),
            terrainDatabase);

        List<Vector3Int> freeCells = validPaths != null
            ? validPaths.Keys.Where(c => !occupiedCells.Contains(c)).ToList()
            : new List<Vector3Int>();

        // --- Seleção de posição de ataque (com ou sem DPQ) ---
        bool useDpq = unit.TryGetUnitData(out UnitData unitData) && unitData != null && unitData.prioritizeDpqAtBattle;

        if (useDpq)
        {
            // Coleta TODAS as posições de onde é possível atacar, escolhe a com maior DPQ
            var attackOptions = new List<(Vector3Int dest, SensorMovementMode mode, PodeMirarTargetOption target, int dpq)>();

            if (TryFindAttack(unit, fromCell, SensorMovementMode.MoveuParado, out PodeMirarTargetOption stayTarget))
            {
                int dpq = turnStateManager.GetCellDpqPoints(fromCell, unit);
                attackOptions.Add((fromCell, SensorMovementMode.MoveuParado, stayTarget, dpq));
            }

            foreach (Vector3Int dest in freeCells)
            {
                if (TryFindAttack(unit, dest, SensorMovementMode.MoveuAndando, out PodeMirarTargetOption moveTarget))
                {
                    int dpq = turnStateManager.GetCellDpqPoints(dest, unit);
                    attackOptions.Add((dest, SensorMovementMode.MoveuAndando, moveTarget, dpq));
                }
            }

            if (attackOptions.Count > 0)
            {
                var best = attackOptions.OrderByDescending(o => o.dpq).First();
                Debug.Log($"[AI] {unit.InstanceId} DPQ-attack: dest={best.dest} dpq={best.dpq} alvo={best.target.targetUnit.InstanceId}");
                return BuildAttackBatch(unit, myTeam, moveFrom: fromCell, moveTo: best.dest, target: best.target);
            }
        }
        else
        {
            // --- Prioridade 1: atacar ficando parado ---
            if (TryFindAttack(unit, fromCell, SensorMovementMode.MoveuParado, out PodeMirarTargetOption stayTarget))
            {
                Debug.Log($"[AI] {unit.InstanceId} fica parado e ataca {stayTarget.targetUnit.InstanceId}");
                return BuildAttackBatch(unit, myTeam, moveFrom: fromCell, moveTo: fromCell, target: stayTarget);
            }

            // --- Prioridade 2: mover e atacar ---
            foreach (Vector3Int dest in freeCells)
            {
                if (TryFindAttack(unit, dest, SensorMovementMode.MoveuAndando, out PodeMirarTargetOption moveTarget))
                {
                    Debug.Log($"[AI] {unit.InstanceId} move para {dest} e ataca {moveTarget.targetUnit.InstanceId}");
                    return BuildAttackBatch(unit, myTeam, moveFrom: fromCell, moveTo: dest, target: moveTarget);
                }
            }
        }

        // --- Avançar em direção ao inimigo visível mais próximo ---
        Vector3Int destination = fromCell;
        if (freeCells.Count > 0)
        {
            UnitManager closestEnemy = FindClosestVisibleEnemy(fromCell, myTeam);
            if (closestEnemy != null)
            {
                Vector3Int enemyCell = closestEnemy.CurrentCellPosition;
                enemyCell.z = 0;

                if (useDpq)
                {
                    // Avança priorizando DPQ em empate de distância
                    int bestDist = freeCells.Min(c => HexApproxDistance(c, enemyCell));
                    destination = freeCells
                        .Where(c => HexApproxDistance(c, enemyCell) == bestDist)
                        .OrderByDescending(c => turnStateManager.GetCellDpqPoints(c, unit))
                        .First();
                }
                else
                {
                    destination = freeCells
                        .OrderBy(c => HexApproxDistance(c, enemyCell))
                        .First();
                }

                Debug.Log($"[AI] {unit.InstanceId} avança em direção a {closestEnemy.InstanceId} em {enemyCell} -> dest {destination}");
            }
            else
            {
                destination = freeCells[Random.Range(0, freeCells.Count)];
                Debug.Log($"[AI] {unit.InstanceId} sem inimigos visíveis, move aleatório para {destination}");
            }
        }

        return BuildMoveBatch(unit, myTeam, moveFrom: fromCell, moveTo: destination);
    }

    // -------------------------------------------------------------------------
    // Consulta de ataque (PodeMirar hipotético)
    // -------------------------------------------------------------------------

    private bool TryFindAttack(UnitManager unit, Vector3Int fromCell, SensorMovementMode mode, out PodeMirarTargetOption bestTarget)
    {
        bestTarget = default;
        _targetBuffer.Clear();

        bool hasTargets = PodeMirarSensor.CollectTargets(
            attacker: unit,
            boardTilemap: boardTilemap,
            terrainDatabase: terrainDatabase,
            movementMode: mode,
            output: _targetBuffer,
            fromCell: fromCell);

        if (!hasTargets || _targetBuffer.Count == 0)
            return false;

        bestTarget = _targetBuffer[0]; // TODO: ranquear alvos (hp baixo, kill garantido, etc.)
        return true;
    }

    // -------------------------------------------------------------------------
    // Construtores de batch
    // -------------------------------------------------------------------------

    private PlayerAction BuildAttackBatch(UnitManager unit, TeamId myTeam,
        Vector3Int moveFrom, Vector3Int moveTo, PodeMirarTargetOption target)
    {
        Vector3Int targetCell = target.targetUnit.CurrentCellPosition;
        targetCell.z = 0;

        return new PlayerAction
        {
            IsAIGenerated = true,
            ActionType = PlayerActionType.UnitAction,
            ActingTeam = myTeam,
            TurnNumber = matchController.CurrentTurn,

            CursorHex = moveFrom,
            HasCursorHex = true,

            UnitInstanceId = unit.InstanceId.ToString(),

            MoveFrom = moveFrom,
            MoveTo = moveTo,
            HasMoveFrom = true,
            HasMoveTo = true,

            SensorAction = SensorActionType.Attack,

            TargetInstanceId = target.targetUnit.InstanceId.ToString(),
            TargetHex = targetCell,
            HasTargetHex = true,
        };
    }

    private PlayerAction BuildMoveBatch(UnitManager unit, TeamId myTeam,
        Vector3Int moveFrom, Vector3Int moveTo)
    {
        return new PlayerAction
        {
            IsAIGenerated = true,
            ActionType = PlayerActionType.UnitAction,
            ActingTeam = myTeam,
            TurnNumber = matchController.CurrentTurn,

            CursorHex = moveFrom,
            HasCursorHex = true,

            UnitInstanceId = unit.InstanceId.ToString(),

            MoveFrom = moveFrom,
            MoveTo = moveTo,
            HasMoveFrom = true,
            HasMoveTo = true,

            SensorAction = SensorActionType.None, // Menu: Wait
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private HashSet<Vector3Int> BuildOccupiedSnapshot(UnitManager excludeUnit)
    {
        var set = new HashSet<Vector3Int>();
        foreach (var u in UnitManager.AllActive)
        {
            if (u == excludeUnit || u.IsEmbarked) continue;
            Vector3Int pos = u.CurrentCellPosition;
            pos.z = 0;
            set.Add(pos);
        }
        return set;
    }

    /// <summary>
    /// Retorna o inimigo visível para myTeam mais próximo de fromCell.
    /// Usa IsUnitVisibleForTeam — mesma fonte de verdade do fog of war do jogo.
    /// </summary>
    private UnitManager FindClosestVisibleEnemy(Vector3Int fromCell, TeamId myTeam)
    {
        UnitManager closest = null;
        int bestDist = int.MaxValue;

        foreach (var u in UnitManager.AllActive)
        {
            if (u.TeamId == myTeam || u.IsDead || u.IsEmbarked) continue;
            if (!matchController.IsUnitVisibleForTeam(u, myTeam)) continue;

            Vector3Int pos = u.CurrentCellPosition;
            pos.z = 0;
            int dist = HexApproxDistance(fromCell, pos);
            if (dist < bestDist)
            {
                bestDist = dist;
                closest = u;
            }
        }

        return closest;
    }

    /// <summary>
    /// Distância aproximada entre dois hexes em offset coordinates.
    /// Suficiente para comparar e escolher o destino mais próximo de um alvo.
    /// </summary>
    private static int HexApproxDistance(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        // Para hex offset, o componente x já encapsula a direção principal.
        // Esta fórmula dá uma boa aproximação sem precisar converter para cube coords.
        return dx + Mathf.Max(0, dy - (dx + 1) / 2);
    }
}
