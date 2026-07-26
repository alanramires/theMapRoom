using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PodeSubmergirReport
{
    public bool status;
    public string explicacao;
    public Vector3Int cell;
}

/// <summary>
/// Consulta pura e autoritativa para Naval/Surface -> Submarine/Submerged.
/// Nao move a unidade, nao altera locks e nao atualiza deteccao/FOW.
/// </summary>
public static class PodeSubmergirSensor
{
    public static bool CanSubmergeAtCell(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out string reason)
    {
        PodeSubmergirReport report = Evaluate(unit, boardMap, terrainDatabase, cell);
        reason = report != null ? report.explicacao : "PodeSubmergir sem resultado.";
        return report != null && report.status;
    }

    public static PodeSubmergirReport Evaluate(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Vector3Int? atCell = null)
    {
        Vector3Int cell = atCell ?? (unit != null ? unit.CurrentCellPosition : Vector3Int.zero);
        cell.z = 0;

        var report = new PodeSubmergirReport
        {
            status = false,
            explicacao = "Contexto nao avaliado.",
            cell = cell
        };

        if (unit == null)
        {
            report.explicacao = "Selecione uma unidade.";
            return report;
        }

        if (unit.IsEmbarked)
        {
            report.explicacao = "Unidade embarcada nao pode submergir.";
            return report;
        }

        if (boardMap == null)
        {
            report.explicacao = "Tilemap base nao encontrado.";
            return report;
        }

        if (terrainDatabase == null)
        {
            report.explicacao = "TerrainDatabase nao encontrado.";
            return report;
        }

        if (unit.GetDomain() != Domain.Naval || unit.GetHeightLevel() != HeightLevel.Surface)
        {
            report.explicacao = "Submergir exige unidade em Naval/Surface.";
            return report;
        }

        if (!unit.SupportsLayerMode(Domain.Submarine, HeightLevel.Submerged))
        {
            report.explicacao = "Unidade nao suporta Submarine/Submerged.";
            return report;
        }

        if (unit.HasFiredThisTurn)
        {
            report.explicacao = "Unidade disparou nesta rodada e permanece exposta na superficie.";
            return report;
        }

        if (unit.SurfacedForSupplyThisTurn)
        {
            report.explicacao = "Unidade emergiu para receber suprimento e permanece na superficie nesta rodada.";
            return report;
        }

        if (unit.IsLayerChangeBlockedByForcedLock(
                Domain.Submarine,
                HeightLevel.Submerged,
                out string lockReason))
        {
            report.explicacao = lockReason;
            return report;
        }

        if (unit.IsCurrentlyObservedByOpponent())
        {
            report.explicacao = "Unidade detectada recentemente por um oponente nao pode submergir.";
            return report;
        }

        if (!LayerTransitionRules.CanUseLayerModeAtCell(
                unit,
                boardMap,
                terrainDatabase,
                cell,
                Domain.Submarine,
                HeightLevel.Submerged,
                out string cellReason))
        {
            report.explicacao = cellReason;
            return report;
        }

        report.status = true;
        report.explicacao = "Submersao disponivel neste hex.";
        return report;
    }

}
