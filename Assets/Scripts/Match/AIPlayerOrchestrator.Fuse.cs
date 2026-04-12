using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Lógica de decisão de fusão (reparo) para a IA.
/// Partial class de AIPlayerOrchestrator.
/// </summary>
public partial class AIPlayerOrchestrator
{
    private readonly List<PodeFundirOption> _fuseBuffer = new List<PodeFundirOption>();

    /// <summary>
    /// Simula fusão em todas as posições alcançáveis e retorna o melhor batch,
    /// ou null se nenhum candidato válido for encontrado.
    /// Condição: myHP + allyHP ≤ 10.
    /// </summary>
    private PlayerAction TryDecideFuse(UnitManager unit, TeamId myTeam, Vector3Int fromCell)
    {
        int remainingMove = Mathf.Max(0, unit.RemainingMovementPoints);

        Dictionary<Vector3Int, List<Vector3Int>> validPaths = UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap, unit, remainingMove, terrainDatabase);

        // Posição atual + células alcançáveis livres (mesmo padrão do TryFindAttack)
        var positions = new List<Vector3Int> { fromCell };
        if (validPaths != null)
            positions.AddRange(validPaths.Keys.Where(c =>
                !c.Equals(fromCell) &&
                !UnitOccupancyRules.IsUnitCellOccupied(boardTilemap, c, unit)));

        PodeFundirOption bestOption = null;
        Vector3Int bestDest = fromCell;

        foreach (Vector3Int dest in positions)
        {
            int remainingAtDest = remainingMove;
            if (!dest.Equals(fromCell) && validPaths != null &&
                validPaths.TryGetValue(dest, out List<Vector3Int> pathToDest) && pathToDest != null)
            {
                int costToDest = Mathf.Max(0, UnitMovementPathRules.CalculateAutonomyCostForPath(
                    boardTilemap, unit, pathToDest, terrainDatabase, applyOperationalAutonomyModifier: false));
                remainingAtDest = Mathf.Max(0, remainingMove - costToDest);
            }

            _fuseBuffer.Clear();
            PodeFundirSensor.CollectOptions(
                unit, boardTilemap, terrainDatabase,
                remainingAtDest, _fuseBuffer, out _,
                fromCell: dest);

            foreach (PodeFundirOption opt in _fuseBuffer)
            {
                if (opt.candidateUnit == null) continue;
                if (unit.CurrentHP + opt.candidateUnit.CurrentHP > 10) continue;
                if (bestOption == null || opt.candidateUnit.CurrentHP < bestOption.candidateUnit.CurrentHP)
                {
                    bestOption = opt;
                    bestDest = dest;
                }
            }
        }

        if (bestOption == null) return null;

        Vector3Int candidateCell = bestOption.candidateCell; candidateCell.z = 0;
        string candidateId = bestOption.candidateUnit.InstanceId.ToString();
        Debug.Log($"[AI] {unit.InstanceId} move para {bestDest}, funde com {candidateId} ({unit.CurrentHP}+{bestOption.candidateUnit.CurrentHP}HP)");
        return BuildFuseBatch(unit, myTeam, fromCell, bestDest, candidateCell, candidateId);
    }

    private PlayerAction BuildFuseBatch(UnitManager unit, TeamId myTeam,
        Vector3Int fromCell, Vector3Int stopCell, Vector3Int candidateCell, string candidateId)
    {
        return new PlayerAction
        {
            IsAIGenerated = true,
            ActionType = PlayerActionType.UnitAction,
            ActingTeam = myTeam,
            TurnNumber = matchController.CurrentTurn,
            CursorHex = fromCell,
            HasCursorHex = true,
            UnitInstanceId = unit.InstanceId.ToString(),
            MoveFrom = fromCell,
            MoveTo = stopCell,
            HasMoveFrom = true,
            HasMoveTo = true,
            SensorAction = SensorActionType.Merge,
            TargetInstanceId = candidateId,
            SubSteps = new List<PlayerActionSubStep>
            {
                new PlayerActionSubStep
                {
                    Label = "QueueConfirm",
                    TargetInstanceId = candidateId,
                    TargetHex = candidateCell,
                    HasTargetHex = true
                }
            }
        };
    }
}
