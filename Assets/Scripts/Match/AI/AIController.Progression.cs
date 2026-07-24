using System.Collections.Generic;
using UnityEngine;

// -------------------------------------------------------------------------
// Controla a IA inimiga, incluindo a execu��o de suas a��es, planejamento de objetivos e tomada de decis�es.
// Implementa uma abordagem baseada em est�gios para organizar o comportamento da IA,
// desde a avalia��o do estado do jogo at� a execu��o de a��es espec�ficas.
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
        using var perf = new AIDecisionPerfScope(unit, "twoTurnProgression");
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

        float originDistance = CalculateRouteDistanceOrHex(unit, origin, target);
        float firstStopDistance = CalculateRouteDistanceOrHex(unit, firstStop, target);
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

                float nextDistance = CalculateRouteDistanceOrHex(unit, nextStop, target);
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
        // being reachable, it was reached via the road-bonus free step — actual MP spent == full budget.
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
    private bool TryScoreToolRouteProgression(
        UnitManager unit,
        Vector3Int origin,
        Vector3Int target,
        Vector3Int firstStop,
        IReadOnlyList<Vector3Int> firstPath,
        HashSet<Vector3Int> occupied,
        out int score,
        out float bestDistanceAfterNextMove,
        out int firstMoveCost)
    {
        score = 0;
        bestDistanceAfterNextMove = float.MaxValue;
        firstMoveCost = int.MaxValue;
        if (unit == null || boardTilemap == null)
            return false;

        origin.z = 0;
        target.z = 0;
        firstStop.z = 0;
        if (firstStop == origin)
            return false;

        Vector3Int originalCell = unit.CurrentCellPosition;
        originalCell.z = 0;

        float originDistance = CalculateRouteDistanceOrHex(unit, origin, target);
        bestDistanceAfterNextMove = CalculateRouteDistanceOrHex(unit, firstStop, target);

        int movementPoints = Mathf.Max(0, unit.RemainingMovementPoints);
        Dictionary<Vector3Int, int> costMap =
            UnitMovementPathRules.CalculateMovementCostMap(
                boardTilemap,
                unit,
                origin,
                movementPoints,
                terrainDatabase);

        firstMoveCost = costMap != null && costMap.TryGetValue(firstStop, out int cost)
            ? cost
            : (firstPath != null ? Mathf.Max(0, firstPath.Count - 1) : 0);

        try
        {
            unit.SetCurrentCellPosition(firstStop, enforceFinalOccupancyRule: false);
            Dictionary<Vector3Int, List<Vector3Int>> nextPaths =
                UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap,
                    unit,
                    movementPoints,
                    terrainDatabase);

            if (nextPaths != null)
            {
                foreach (Vector3Int rawNextStop in nextPaths.Keys)
                {
                    Vector3Int nextStop = rawNextStop;
                    nextStop.z = 0;
                    if (!CanUseAsToolProgressStopCell(unit, nextStop, firstStop))
                        continue;

                    float nextDistance = CalculateRouteDistanceOrHex(unit, nextStop, target);
                    if (nextDistance < bestDistanceAfterNextMove)
                        bestDistanceAfterNextMove = nextDistance;
                }
            }
        }
        finally
        {
            unit.SetCurrentCellPosition(originalCell, enforceFinalOccupancyRule: false);
        }

        float twoTurnProgress = originDistance - bestDistanceAfterNextMove;
        float firstTurnProgress = originDistance - CalculateRouteDistanceOrHex(unit, firstStop, target);
        float lineDeviation = DistanceFromHexLine(firstStop, origin, target);

        // Progressão = aproximar do alvo. 1T (avanço já no turno 1) é termo principal; 2T mantém a
        // viabilidade de chegar em 2 turnos; line entra suave; firstMoveCost NÃO penaliza (gastar
        // movimento avançando é o objetivo). Espelha CaminhosValidosWindow.CalculateTwoTurnProgressScore.
        float rawScore =
            twoTurnProgress * 10f
            + firstTurnProgress * 6f
            - lineDeviation * 1f;

        score = Mathf.RoundToInt(rawScore);
        return true;
    }

    private static bool CanUseAsToolProgressStopCell(UnitManager mover, Vector3Int cell, Vector3Int origin)
    {
        if (mover == null)
            return false;

        cell.z = 0;
        origin.z = 0;
        if (cell == origin)
            return true;

        HeightBand moverBand = OccupancyResolver.GetHeightBand(mover);
        if (moverBand != HeightBand.Blocking)
            return true;

        foreach (UnitManager occupant in UnitManager.AllActive)
        {
            if (occupant == null || occupant == mover || occupant.IsDead || occupant.IsEmbarked)
                continue;

            Vector3Int occupantCell = occupant.CurrentCellPosition;
            occupantCell.z = 0;
            if (occupantCell != cell)
                continue;

            occupant.SyncLayerStateFromData(forceNativeDefault: false);
            if (OccupancyResolver.GetHeightBand(occupant) != moverBand)
                continue;

            if (occupant.SlotIndex == mover.SlotIndex)
                return false;

            if (!OccupancyResolver.IsLayerAwareRulesActive)
                return false;
        }

        return true;
    }
}
