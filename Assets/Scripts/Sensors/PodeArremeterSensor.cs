using UnityEngine.Tilemaps;

public enum AirGoAroundOperation
{
    None = 0,
    Supply = 1,
    CommandService = 2,
    Transfer = 3,
    FutureExplicitOperation = 4
}

public sealed class PodeArremeterReport
{
    public bool status;
    public string explicacao;
    public PodeDecolarReport takeoffReport;
}

/// <summary>
/// Consulta pura para pousar -> operar -> tentar decolar no mesmo hex.
/// A ordem e decidida pelo snapshot anterior à operacao.
/// </summary>
public static class PodeArremeterSensor
{
    public static PodeArremeterReport Evaluate(
        UnitManager aircraft,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        AirGoAroundOperation operation,
        bool wasAirborneBeforeOperation,
        bool landedForOperation,
        int fuelBeforeOperation,
        bool operationExplicitlyAllowsGoAround)
    {
        var report = new PodeArremeterReport
        {
            status = false,
            explicacao = "Contexto nao avaliado."
        };

        if (aircraft == null)
        {
            report.explicacao = "Aeronave nao encontrada.";
            return report;
        }

        if (!aircraft.TryGetUnitData(out UnitData data) || data == null || !data.IsAircraft())
        {
            report.explicacao = "Unidade nao e aeronave.";
            return report;
        }

        if (!wasAirborneBeforeOperation)
        {
            report.explicacao = "Aeronave que ja estava pousada nao arremete.";
            return report;
        }

        if (!landedForOperation || aircraft.GetDomain() == Domain.Air)
        {
            report.explicacao = "Aeronave nao pousou para esta operacao.";
            return report;
        }

        if (operation == AirGoAroundOperation.Transfer)
        {
            report.explicacao = "Aeronave que pousou para transferir permanece pousada.";
            return report;
        }

        if (!operationExplicitlyAllowsGoAround ||
            operation == AirGoAroundOperation.None)
        {
            report.explicacao = "Operacao nao autoriza arremetida.";
            return report;
        }

        // Combustivel recebido depois do pouso nao cria permissao retroativa.
        if (fuelBeforeOperation <= 0)
        {
            report.explicacao = "Aeronave pousou sem combustivel e permanece pousada apos o abastecimento.";
            return report;
        }

        PodeDecolarReport takeoff =
            PodeDecolarSensor.Evaluate(aircraft, boardMap, terrainDatabase);
        report.takeoffReport = takeoff;
        if (takeoff == null || !takeoff.status)
        {
            report.explicacao = takeoff != null
                ? takeoff.explicacao
                : "PodeDecolar sem resultado.";
            return report;
        }

        report.status = true;
        report.explicacao = "Arremetida autorizada; decolagem final validada por PodeDecolar.";
        return report;
    }
}
