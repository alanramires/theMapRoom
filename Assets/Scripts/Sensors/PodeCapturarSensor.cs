using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeCapturarSensor
{
    public enum CaptureOperationType
    {
        None = 0,
        CaptureEnemy = 1,
        RecoverAlly = 2
    }

    public static bool TryGetCaptureTarget(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        SensorMovementMode movementMode,
        out ConstructionManager targetConstruction,
        out string reason)
    {
        return TryGetCaptureTarget(
            selectedUnit,
            boardTilemap,
            movementMode,
            out targetConstruction,
            out _,
            out reason);
    }

    public static bool TryGetCaptureTarget(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        SensorMovementMode movementMode,
        out ConstructionManager targetConstruction,
        out CaptureOperationType operationType,
        out string reason)
    {
        targetConstruction = null;
        operationType = CaptureOperationType.None;
        reason = string.Empty;

        if (selectedUnit == null)
        {
            reason = "Selecione uma unidade.";
            return false;
        }

        if (selectedUnit.IsEmbarked)
        {
            reason = "Unidade embarcada nao pode capturar.";
            return false;
        }

        if (movementMode != SensorMovementMode.MoveuParado && movementMode != SensorMovementMode.MoveuAndando)
        {
            reason = "Captura so pode ser avaliada em Moveu Parado ou Moveu Andando.";
            return false;
        }

        if (!selectedUnit.TryGetUnitData(out UnitData unitData) || unitData == null)
        {
            reason = "UnitData indisponivel.";
            return false;
        }

        if (unitData.unitClass != GameUnitClass.Infantry)
        {
            reason = "Apenas infantaria pode capturar.";
            return false;
        }

        Tilemap map = boardTilemap != null ? boardTilemap : selectedUnit.BoardTilemap;
        if (map == null)
        {
            reason = "Tilemap indisponivel para avaliar captura.";
            return false;
        }

        Vector3Int cell = selectedUnit.CurrentCellPosition;
        cell.z = 0;
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(map, cell);
        if (construction == null)
        {
            reason = "Nao ha construcao no hex atual.";
            return false;
        }

        if (!construction.IsCapturable || construction.CapturePointsMax <= 0)
        {
            reason = "Construcao atual nao e capturavel.";
            return false;
        }

        TeamId unitTeam = selectedUnit.TeamId;
        if (unitTeam == TeamId.Neutral)
        {
            reason = "Unidade neutra nao captura.";
            return false;
        }

        if (construction.TeamId == unitTeam)
        {
            if (construction.CurrentCapturePoints < construction.CapturePointsMax)
            {
                targetConstruction = construction;
                operationType = CaptureOperationType.RecoverAlly;
                return true;
            }

            reason = "Construcao aliada ja esta com captura maxima.";
            return false;
        }

        targetConstruction = construction;
        operationType = CaptureOperationType.CaptureEnemy;
        return true;
    }
}
