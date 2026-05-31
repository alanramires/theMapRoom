using System.Collections.Generic;
using UnityEngine;

// -------------------------------------------------------------------------
// Controla a IA inimiga, incluindo a execução de suas ações, planejamento de objetivos e tomada de decisões.
// Implementa uma abordagem baseada em estágios para organizar o comportamento da IA,
// desde a avaliação do estado do jogo até a execução de ações específicas.
// -------------------------------------------------------------------------    
public partial class AIController
{
    private bool TryScoreTwoTurnProgression(
        UnitManager unit,
        Vector3Int origin,
        Vector3Int target,
        Vector3Int firstStop,
        IReadOnlyList<Vector3Int> firstPath,
        HashSet<Vector3Int> occupied,
        out float score,
        out float bestDistanceAfterNextMove,
        Dictionary<Vector3Int, int> costFromOrigin = null)
    {
        score = 0f;
        bestDistanceAfterNextMove = float.MaxValue;
        if (unit == null || boardTilemap == null)
            return false;

        origin.z = 0;
        target.z = 0;
        firstStop.z = 0;

        if (firstStop == origin)
            return false;

        Vector3Int originalCell = unit.CurrentCellPosition;
        originalCell.z = 0;

        float originDistance = SectorManager.HexDistance(origin, target);
        float firstStopDistance = SectorManager.HexDistance(firstStop, target);
        bestDistanceAfterNextMove = firstStopDistance;

        try
        {
            unit.SetCurrentCellPosition(firstStop, enforceFinalOccupancyRule: false);
            Dictionary<Vector3Int, List<Vector3Int>> nextPaths =
                UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap,
                    unit,
                    Mathf.Max(0, unit.RemainingMovementPoints),
                    terrainDatabase);

            foreach (Vector3Int rawNextStop in nextPaths.Keys)
            {
                Vector3Int nextStop = rawNextStop;
                nextStop.z = 0;
                if (occupied != null && occupied.Contains(nextStop))
                    continue;

                float nextDistance = SectorManager.HexDistance(nextStop, target);
                if (nextDistance < bestDistanceAfterNextMove)
                    bestDistanceAfterNextMove = nextDistance;
            }
        }
        finally
        {
            unit.SetCurrentCellPosition(originalCell, enforceFinalOccupancyRule: false);
        }

        float immediateProgress = originDistance - firstStopDistance;
        float twoTurnProgress = originDistance - bestDistanceAfterNextMove;
        float lineDeviation = DistanceFromHexLine(firstStop, origin, target);
        // Use real MP cost when available.  If the cell is absent from costFromOrigin while still
        // being reachable, it was reached via the road-bonus free step â€” actual MP spent == full budget.
        int firstMoveCost;
        if (costFromOrigin != null && costFromOrigin.TryGetValue(firstStop, out int c))
            firstMoveCost = c;
        else if (costFromOrigin != null)
            firstMoveCost = unit.RemainingMovementPoints; // road-bonus cell: free extra step used
        else
            firstMoveCost = firstPath != null ? Mathf.Max(0, firstPath.Count - 1) : 0;

        score =
            twoTurnProgress * 55f
            + Mathf.Max(0f, immediateProgress) * 15f
            - lineDeviation * 8f
            - firstMoveCost * 2f;

        return true;
    }
}
