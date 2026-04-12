using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Lógica de decisão de combate (ataque) para a IA.
/// Partial class de AIPlayerOrchestrator.
/// </summary>
public partial class AIPlayerOrchestrator
{
    // Buffer reutilizável para não alocar listas a cada consulta
    private readonly List<PodeMirarTargetOption> _targetBuffer = new List<PodeMirarTargetOption>();

    private PlayerAction TryDecideAttack(UnitManager unit, TeamId myTeam,
        Vector3Int fromCell, List<Vector3Int> freeCells, bool useDpq)
    {
        if (useDpq)
        {
            var attackOptions = new List<(Vector3Int dest, PodeMirarTargetOption target, int dpq)>();

            if (TryFindAttack(unit, fromCell, SensorMovementMode.MoveuParado, out PodeMirarTargetOption stayTarget))
                attackOptions.Add((fromCell, stayTarget, turnStateManager.GetCellDpqPoints(fromCell, unit)));

            foreach (Vector3Int dest in freeCells)
            {
                if (TryFindAttack(unit, dest, SensorMovementMode.MoveuAndando, out PodeMirarTargetOption moveTarget))
                    attackOptions.Add((dest, moveTarget, turnStateManager.GetCellDpqPoints(dest, unit)));
            }

            if (attackOptions.Count > 0)
            {
                var best = attackOptions.OrderByDescending(o => o.dpq).First();
                Debug.Log($"[AI] {unit.InstanceId} DPQ-attack: dest={best.dest} dpq={best.dpq} alvo={best.target.targetUnit.InstanceId}");
                return BuildAttackBatch(unit, myTeam, fromCell, best.dest, best.target);
            }
        }
        else
        {
            if (TryFindAttack(unit, fromCell, SensorMovementMode.MoveuParado, out PodeMirarTargetOption stayTarget))
            {
                Debug.Log($"[AI] {unit.InstanceId} fica parado e ataca {stayTarget.targetUnit.InstanceId}");
                return BuildAttackBatch(unit, myTeam, fromCell, fromCell, stayTarget);
            }

            foreach (Vector3Int dest in freeCells)
            {
                if (TryFindAttack(unit, dest, SensorMovementMode.MoveuAndando, out PodeMirarTargetOption moveTarget))
                {
                    Debug.Log($"[AI] {unit.InstanceId} move para {dest} e ataca {moveTarget.targetUnit.InstanceId}");
                    return BuildAttackBatch(unit, myTeam, fromCell, dest, moveTarget);
                }
            }
        }

        return null;
    }

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
}
