using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeEmergirReport
{
    public bool status;
    public string explicacao;
}

public static class PodeEmergirSensor
{
    /// <summary>
    /// "Posso emergir ALI?" — mesma consulta por hex do PodePousarSensor.CanLandAtCell,
    /// do outro lado da escada. Nao move a unidade: os gates de ESTADO (submersa,
    /// suporta Naval/Surface) valem pela unidade real, so o hex e hipotetico.
    /// </summary>
    public static bool CanEmergeAtCell(
        UnitManager unit,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out string reason)
    {
        PodeEmergirReport report = Evaluate(unit, map, terrainDatabase, cell);
        reason = report != null ? report.explicacao : "PodeEmergir sem resultado.";
        return report != null && report.status;
    }

    public static PodeEmergirReport Evaluate(
        UnitManager unit,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int? atCell = null)
    {
        var report = new PodeEmergirReport
        {
            status = false,
            explicacao = "Contexto nao avaliado."
        };

        bool sensorLogs = SensorLogGate.IsPodeEmergirEnabled();

        if (unit == null)
        {
            report.explicacao = "Selecione uma unidade.";
            return report;
        }

        if (unit.IsEmbarked)
        {
            report.explicacao = "Unidade embarcada nao pode emergir.";
            return report;
        }

        if (map == null)
        {
            report.explicacao = "Tilemap base nao encontrado.";
            return report;
        }

        if (terrainDatabase == null)
        {
            report.explicacao = "TerrainDatabase nao encontrado.";
            return report;
        }

        Domain currentDomain = unit.GetDomain();
        HeightLevel currentHeight = unit.GetHeightLevel();

        if (currentDomain != Domain.Submarine || currentHeight != HeightLevel.Submerged)
        {
            report.explicacao = "Unidade nao esta submersa (Submarine/Submerged).";
            return report;
        }

        if (!unit.SupportsLayerMode(Domain.Naval, HeightLevel.Surface))
        {
            report.explicacao = "Unidade nao suporta camada Naval/Surface.";
            return report;
        }

        Vector3Int cell = atCell ?? unit.CurrentCellPosition;
        cell.z = 0;

        if (!LayerTransitionRules.CanUseLayerModeAtCell(
                unit,
                map,
                terrainDatabase,
                cell,
                Domain.Naval,
                HeightLevel.Surface,
                out string cellReason))
        {
            report.explicacao = cellReason;
            if (sensorLogs)
                SensorLogGate.Log("PodeEmergir", $"{unit.name} nao pode emergir em {cell}: {cellReason}");
            return report;
        }

        report.status = true;
        report.explicacao = "Emersao disponivel neste hex.";

        if (sensorLogs)
            SensorLogGate.Log("PodeEmergir", $"{unit.name} pode emergir em {cell}.");

        return report;
    }

}
