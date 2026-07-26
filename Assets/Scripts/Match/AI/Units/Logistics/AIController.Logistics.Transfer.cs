using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private bool TryBuildLogisticsTransferReceiveAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        var options = new List<PodeTransferirOption>();
        if (!PodeTransferirSensor.CollectOptions(
                unit, boardTilemap, terrainDatabase, SensorMovementMode.MoveuParado,
                options, out string sensorReason) || options.Count <= 0)
        {
            reason = sensorReason;
            return false;
        }

        PodeTransferirOption best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < options.Count; i++)
        {
            PodeTransferirOption option = options[i];
            if (option == null || option.flowMode != TransferFlowMode.Recebedor)
                continue;

            Vector3Int cell = option.targetCell;
            cell.z = 0;
            float score = -CalculateThreatLevel(cell, snapshot.AITeam) * 100f
                - SectorManager.HexDistance(fromCell, cell) * 10f
                + (option.targetConstruction != null ? 50f : 0f);

            if (score > bestScore)
            {
                bestScore = score;
                best = option;
            }
        }

        if (best == null)
            return false;

        action = BuildTransferReceiveBatch(
            unit, snapshot.AITeam, fromCell, fromCell, best, paths);
        reason = $"alvo={best.targetCell} score={bestScore:F0}";
        return true;
    }

    private bool TryBuildLogisticsTransferReceiveActionAtCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int transferCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        if (unit == null)
            return false;

        Vector3Int originalCell = unit.CurrentCellPosition;
        originalCell.z = 0;
        transferCell.z = 0;

        unit.SetCurrentCellPosition(
            transferCell, enforceFinalOccupancyRule: false);
        try
        {
            var options = new List<PodeTransferirOption>();
            if (!PodeTransferirSensor.CollectOptions(
                    unit, boardTilemap, terrainDatabase,
                    SensorMovementMode.MoveuAndando,
                    options, out string sensorReason)
                || options.Count <= 0)
            {
                reason = sensorReason;
                return false;
            }

            PodeTransferirOption best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < options.Count; i++)
            {
                PodeTransferirOption option = options[i];
                if (option == null
                    || option.flowMode != TransferFlowMode.Recebedor)
                    continue;

                Vector3Int cell = option.targetCell;
                cell.z = 0;
                float score =
                    -CalculateThreatLevel(
                        transferCell, snapshot.AITeam) * 100f
                    - SectorManager.HexDistance(
                        transferCell, cell) * 10f
                    + (option.targetConstruction != null ? 50f : 0f);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = option;
                }
            }

            if (best == null)
            {
                reason = "sem opcao Recebedor apos mover";
                return false;
            }

            action = BuildTransferReceiveBatch(
                unit, snapshot.AITeam, fromCell,
                transferCell, best, paths);
            reason =
                $"aposMover={transferCell} alvo={best.targetCell} " +
                $"score={bestScore:F0}";
            return true;
        }
        finally
        {
            unit.SetCurrentCellPosition(
                originalCell, enforceFinalOccupancyRule: false);
        }
    }

    private bool CanLogisticsReceiveTransferAtCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int transferCell)
    {
        if (unit == null)
            return false;

        Vector3Int originalCell = unit.CurrentCellPosition;
        originalCell.z = 0;
        transferCell.z = 0;

        unit.SetCurrentCellPosition(
            transferCell, enforceFinalOccupancyRule: false);
        try
        {
            var options = new List<PodeTransferirOption>();
            if (!PodeTransferirSensor.CollectOptions(
                    unit, boardTilemap, terrainDatabase,
                    SensorMovementMode.MoveuAndando,
                    options, out _)
                || options.Count <= 0)
                return false;

            for (int i = 0; i < options.Count; i++)
            {
                PodeTransferirOption option = options[i];
                if (option == null
                    || option.flowMode != TransferFlowMode.Recebedor)
                    continue;

                Vector3Int targetCell = option.targetCell;
                targetCell.z = 0;
                if (snapshot != null
                    && CalculateThreatLevel(
                        targetCell, snapshot.AITeam) > 0f)
                    continue;

                return true;
            }

            return false;
        }
        finally
        {
            unit.SetCurrentCellPosition(
                originalCell, enforceFinalOccupancyRule: false);
        }
    }
}
