using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class PodeMudarAltitudeReport
{
    public bool status;
    public string explicacao;
    public Vector3Int cell;
    public HeightLevel fromHeight;
    public HeightLevel toHeight;
}

/// <summary>
/// Consulta pura para nivelamento AirLow &lt;-&gt; AirHigh.
/// Nao consulta terreno, estrutura, construcao ou skills de entrada do hex.
/// </summary>
public static class PodeMudarAltitudeSensor
{
    public static PodeMudarAltitudeReport Evaluate(
        UnitManager unit,
        Tilemap boardMap,
        HeightLevel targetHeight,
        Vector3Int? atCell = null)
    {
        Vector3Int cell = atCell ?? (unit != null ? unit.CurrentCellPosition : Vector3Int.zero);
        cell.z = 0;

        var report = new PodeMudarAltitudeReport
        {
            status = false,
            explicacao = "Contexto nao avaliado.",
            cell = cell,
            fromHeight = unit != null ? unit.GetHeightLevel() : HeightLevel.Surface,
            toHeight = targetHeight
        };

        if (unit == null)
        {
            report.explicacao = "Selecione uma aeronave.";
            return report;
        }

        if (unit.IsEmbarked)
        {
            report.explicacao = "Aeronave embarcada nao pode mudar de altitude.";
            return report;
        }

        if (!unit.TryGetUnitData(out UnitData data) || data == null || !data.IsAircraft())
        {
            report.explicacao = "Unidade selecionada nao e aeronave.";
            return report;
        }

        if (boardMap == null)
        {
            report.explicacao = "Tilemap base nao encontrado.";
            return report;
        }

        HeightLevel currentHeight = unit.GetHeightLevel();
        if (unit.GetDomain() != Domain.Air ||
            unit.IsAircraftGrounded ||
            (currentHeight != HeightLevel.AirLow && currentHeight != HeightLevel.AirHigh))
        {
            report.explicacao = "Mudanca de altitude exige aeronave em voo.";
            return report;
        }

        if (targetHeight != HeightLevel.AirLow && targetHeight != HeightLevel.AirHigh)
        {
            report.explicacao = "Altitude de destino precisa ser AirLow ou AirHigh.";
            return report;
        }

        if (targetHeight == currentHeight)
        {
            report.explicacao = $"Aeronave ja esta em Air/{targetHeight}.";
            return report;
        }

        if (!unit.SupportsLayerMode(Domain.Air, targetHeight))
        {
            report.explicacao = $"Aeronave nao suporta Air/{targetHeight}.";
            return report;
        }

        if (unit.IsLayerChangeBlockedByForcedLock(Domain.Air, targetHeight, out string lockReason))
        {
            report.explicacao = lockReason;
            return report;
        }

        // A trava e especifica para a subida que tenta abandonar AirLow.
        // Decolagem completa que ja termina em AirHigh nao passa por este caso.
        if (currentHeight == HeightLevel.AirLow &&
            targetHeight == HeightLevel.AirHigh &&
            unit.TookOffRecently)
        {
            report.explicacao = "Aeronave que acabou de decolar em AirLow nao pode subir para AirHigh.";
            return report;
        }

        if (!UnitOccupancyRules.CanEndLayerTransitionAtCell(
                boardMap,
                cell,
                unit,
                Domain.Air,
                targetHeight,
                out UnitManager blocker))
        {
            string blockerName = blocker != null && !string.IsNullOrWhiteSpace(blocker.UnitDisplayName)
                ? blocker.UnitDisplayName
                : "aliado";
            report.explicacao = $"Banda aerea ocupada por {blockerName}.";
            return report;
        }

        report.status = true;
        report.explicacao = currentHeight == HeightLevel.AirLow
            ? "Subida para AirHigh disponivel."
            : "Descida para AirLow disponivel.";
        return report;
    }
}
