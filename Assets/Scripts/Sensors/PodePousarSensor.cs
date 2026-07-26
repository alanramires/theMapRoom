using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class PodePousarReport
{
    public bool status;
    public string explicacao;
    public Vector3Int cell;
    public Domain landingDomain;
    public HeightLevel landingHeight;
}

public static class PodePousarSensor
{
    /// <summary>
    /// "Posso pousar ALI?" — consulta pura por hex, sem mover a unidade nem tocar
    /// em estado. Use este overload quando o hex avaliado nao e o hex atual da
    /// aeronave (escolha de LZ pela IA, preview de destino, ferramentas de debug).
    /// Os gates de ESTADO (trava de camada, perfil aereo, no ar x pousada) seguem
    /// valendo pela unidade real; so o hex e hipotetico.
    /// </summary>
    public static bool CanLandAtCell(
        UnitManager aircraft,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out string reason,
        SensorMovementMode movementMode = SensorMovementMode.MoveuParado)
    {
        PodePousarReport report = Evaluate(
            aircraft,
            map,
            terrainDatabase,
            movementMode,
            useManualRemainingMovement: false,
            manualRemainingMovement: 0,
            atCell: cell);

        reason = report != null ? report.explicacao : "PodePousar sem resultado.";
        return report != null && report.status;
    }

    public static PodePousarReport Evaluate(
        UnitManager selectedAircraft,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        bool useManualRemainingMovement,
        int manualRemainingMovement,
        Vector3Int? atCell = null)
    {
        bool sensorLogs = SensorLogGate.IsPodePousarEnabled();
        var report = new PodePousarReport
        {
            status = false,
            explicacao = "Contexto nao avaliado.",
            cell = atCell ?? (selectedAircraft != null
                ? selectedAircraft.CurrentCellPosition
                : Vector3Int.zero),
            landingDomain = Domain.Land,
            landingHeight = HeightLevel.Surface
        };
        report.cell.z = 0;

        if (sensorLogs)
            SensorLogGate.Log("PodePousarSensor", $"collect unit={(selectedAircraft != null ? selectedAircraft.name : "(null)")} movement={movementMode}");

        if (selectedAircraft == null)
        {
            report.explicacao = "Selecione uma unidade.";
            return report;
        }

        if (selectedAircraft.IsEmbarked)
        {
            report.explicacao = "Unidade embarcada nao entra no sensor de pouso.";
            return report;
        }

        if (map == null)
        {
            report.explicacao = "Tilemap base nao encontrado.";
            return report;
        }

        if (terrainDatabase == null)
        {
            report.explicacao = "TerrainDatabase nao encontrado. Defina o banco de terrenos para avaliar pouso em terreno/estrutura.";
            return report;
        }

        if (!selectedAircraft.TryGetUnitData(out UnitData data) || data == null)
        {
            report.explicacao = "UnitData nao encontrado.";
            return report;
        }

        if (!data.IsAircraft())
        {
            report.explicacao = "Unidade selecionada nao e aeronave.";
            return report;
        }

        if (selectedAircraft.GetDomain() != Domain.Air || selectedAircraft.IsAircraftGrounded)
        {
            report.explicacao = "A aeronave precisa estar em voo para avaliar pouso.";
            return report;
        }

        AircraftOperationRules.ResolveGroundedLayerForCell(
            selectedAircraft,
            map,
            terrainDatabase,
            report.cell,
            out report.landingDomain,
            out report.landingHeight);

        AircraftOperationDecision decision = AircraftOperationRules.Evaluate(
            selectedAircraft,
            map,
            terrainDatabase,
            movementMode,
            allowSameTeamAirBlockerForMovementTakeoff: false,
            atCell: atCell);

        bool canLand = decision.available && decision.action == AircraftOperationAction.Land;
        report.status = canLand;

        if (canLand)
        {
            report.explicacao = "Pouso autorizado neste hex.";
        }
        else if (decision.available && decision.action == AircraftOperationAction.Takeoff)
        {
            report.explicacao = "Pouso indisponivel: unidade ja esta pousada neste hex (acao atual: decolar).";
        }
        else
        {
            report.explicacao = string.IsNullOrWhiteSpace(decision.reason)
                ? "Pouso nao autorizado neste hex."
                : decision.reason;
        }

        if (useManualRemainingMovement)
        {
            int safeRemaining = Mathf.Max(0, manualRemainingMovement);
            report.explicacao += $" Movimento restante manual={safeRemaining} (informativo neste sensor).";
        }

        if (sensorLogs)
            SensorLogGate.Log("PodePousarSensor", $"result status={report.status} msg=\"{report.explicacao}\"");
        return report;
    }
}
