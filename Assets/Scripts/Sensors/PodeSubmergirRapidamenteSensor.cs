using UnityEngine;
using UnityEngine.Tilemaps;

public enum RapidSubmergeOperation
{
    None = 0,
    Supply = 1,
    FutureExplicitOperation = 2
}

public sealed class PodeSubmergirRapidamenteReport
{
    public bool status;
    public string explicacao;
    public PodeSubmergirReport submergeReport;
}

/// <summary>
/// Consulta pura para emergir -> operar -> tentar submergir no mesmo hex.
/// Operacoes atuais de suprimento nao autorizam o retorno rapido.
/// </summary>
public static class PodeSubmergirRapidamenteSensor
{
    public static PodeSubmergirRapidamenteReport Evaluate(
        UnitManager submarine,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        RapidSubmergeOperation operation,
        bool wasSubmergedBeforeOperation,
        bool surfacedForOperation,
        bool operationExplicitlyAllowsRapidSubmerge,
        Vector3Int? atCell = null)
    {
        var report = new PodeSubmergirRapidamenteReport
        {
            status = false,
            explicacao = "Contexto nao avaliado."
        };

        if (submarine == null)
        {
            report.explicacao = "Submarino nao encontrado.";
            return report;
        }

        if (!wasSubmergedBeforeOperation || !surfacedForOperation)
        {
            report.explicacao = "Retorno rapido exige submarino emergido para a operacao atual.";
            return report;
        }

        if (operation == RapidSubmergeOperation.Supply)
        {
            report.explicacao = "Submarino que emerge para receber suprimento permanece na superficie nesta rodada.";
            return report;
        }

        if (!operationExplicitlyAllowsRapidSubmerge ||
            operation == RapidSubmergeOperation.None)
        {
            report.explicacao = "Operacao nao autoriza submersao rapida.";
            return report;
        }

        PodeSubmergirReport submerge =
            PodeSubmergirSensor.Evaluate(submarine, boardMap, terrainDatabase, atCell);
        report.submergeReport = submerge;
        if (submerge == null || !submerge.status)
        {
            report.explicacao = submerge != null
                ? submerge.explicacao
                : "PodeSubmergir sem resultado.";
            return report;
        }

        report.status = true;
        report.explicacao = "Submersao rapida autorizada e validada por PodeSubmergir.";
        return report;
    }
}
