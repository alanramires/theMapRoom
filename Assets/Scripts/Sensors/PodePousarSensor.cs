using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class PodePousarReport
{
    public bool status;
    public string explicacao;
}

public static class PodePousarSensor
{
    public static PodePousarReport Evaluate(
        UnitManager selectedAircraft,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        bool useManualRemainingMovement,
        int manualRemainingMovement)
    {
        var report = new PodePousarReport
        {
            status = false,
            explicacao = "Contexto nao avaliado."
        };

        if (selectedAircraft == null)
        {
            report.explicacao = "Selecione uma unidade.";
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

        AircraftOperationDecision decision = AircraftOperationRules.Evaluate(
            selectedAircraft,
            map,
            terrainDatabase,
            movementMode);

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

        return report;
    }
}
