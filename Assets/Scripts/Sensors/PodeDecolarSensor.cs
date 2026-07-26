using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

[System.Serializable]
public class PodeDecolarReport
{
    public bool status;
    public string explicacao;
    public Vector3Int cell;
    public TakeoffProcedure procedure;
    public HeightLevel endHeight;
    public bool canDecolar0Hex;
    public bool canDecolar1Hex;
    public bool canDecolarFullMove;
    public List<int> takeoffMoveOptions = new List<int>();
}

public static class PodeDecolarSensor
{
    public static bool CanTakeoffAtCell(
        UnitManager aircraft,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out string reason,
        SensorMovementMode movementMode = SensorMovementMode.MoveuParado,
        bool allowSameTeamAirBlockerForMovementTakeoff = false)
    {
        PodeDecolarReport report = Evaluate(
            aircraft,
            map,
            terrainDatabase,
            allowSameTeamAirBlockerForMovementTakeoff,
            cell,
            movementMode);
        reason = report != null ? report.explicacao : "PodeDecolar sem resultado.";
        return report != null && report.status;
    }

    public static PodeDecolarReport Evaluate(
        UnitManager selectedUnit,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        bool allowSameTeamAirBlockerForMovementTakeoff = false,
        Vector3Int? atCell = null,
        SensorMovementMode movementMode = SensorMovementMode.MoveuParado)
    {
        Vector3Int cell = atCell ?? (selectedUnit != null
            ? selectedUnit.CurrentCellPosition
            : Vector3Int.zero);
        cell.z = 0;

        var report = new PodeDecolarReport
        {
            status = false,
            explicacao = "Contexto nao avaliado.",
            cell = cell,
            procedure = TakeoffProcedure.InstantToPreferredHeight,
            endHeight = HeightLevel.AirLow,
            canDecolar0Hex = false,
            canDecolar1Hex = false,
            canDecolarFullMove = false,
            takeoffMoveOptions = new List<int>()
        };

        if (selectedUnit == null)
        {
            report.explicacao = "Selecione uma unidade.";
            return report;
        }

        if (selectedUnit.IsEmbarked)
        {
            report.explicacao = "Unidade embarcada nao entra no sensor de decolagem.";
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

        if (!selectedUnit.TryGetUnitData(out UnitData data) || data == null)
        {
            report.explicacao = "UnitData nao encontrado.";
            return report;
        }

        if (!data.IsAircraft())
        {
            report.explicacao = "Unidade selecionada nao e aeronave (Jet/Helicopter/Plane).";
            report.takeoffMoveOptions.Add(-1);
            return report;
        }

        bool isAirborne = selectedUnit.GetDomain() == Domain.Air && !selectedUnit.IsAircraftGrounded;
        if (isAirborne)
        {
            report.explicacao = "Aeronave ja esta no ar; sensor de decolagem ignorado para esta selecao.";
            report.takeoffMoveOptions.Add(-1);
            return report;
        }

        if (selectedUnit.AircraftForcedLandingAwaitingRefuel)
        {
            report.explicacao = "Aeronave aguarda reabastecimento apos pouso forcado.";
            return report;
        }

        // Aeronaves anfibias podem estar pousadas em Naval/Surface (ex.: hidroaviao).
        // O AirOperationResolver abaixo decide se o terreno/estrutura autoriza a decolagem.
        if (selectedUnit.GetDomain() == Domain.Air || selectedUnit.GetHeightLevel() != HeightLevel.Surface || !selectedUnit.IsAircraftGrounded)
        {
            report.explicacao = "A aeronave precisa estar pousada em uma camada Surface para avaliar decolagem.";
            return report;
        }

        AircraftOperationDecision decision = AircraftOperationRules.Evaluate(
            selectedUnit,
            map,
            terrainDatabase,
            movementMode,
            allowSameTeamAirBlockerForMovementTakeoff,
            cell);

        bool canTakeoff = decision.available && decision.action == AircraftOperationAction.Takeoff;
        report.status = canTakeoff;
        if (canTakeoff)
        {
            AirOperationTileContext tileContext = AirOperationResolver.ResolveContext(
                map,
                terrainDatabase,
                cell);
            if (AirOperationResolver.TryGetTakeoffPlan(
                    selectedUnit,
                    tileContext,
                    movementMode,
                    out AirTakeoffPlan plan,
                    out _))
            {
                report.procedure = plan.procedure;
                report.endHeight = plan.endHeight;
                ApplyButtonAvailability(ref report, plan);
            }

            BuildTakeoffMoveOptions(ref report);
            report.explicacao = BuildAllowedExplanation(report);
            return report;
        }

        report.explicacao = string.IsNullOrWhiteSpace(decision.reason)
            ? "Decolagem nao autorizada neste hex."
            : decision.reason;
        return report;
    }

    private static void BuildTakeoffMoveOptions(ref PodeDecolarReport report)
    {
        report.takeoffMoveOptions.Clear();
        if (!report.status)
            return;

        if (report.canDecolarFullMove)
        {
            report.takeoffMoveOptions.Add(9);
            return;
        }

        if (report.canDecolar0Hex)
            report.takeoffMoveOptions.Add(0);

        if (report.canDecolar1Hex)
            report.takeoffMoveOptions.Add(1);
    }

    private static void ApplyButtonAvailability(ref PodeDecolarReport report, AirTakeoffPlan plan)
    {
        report.canDecolar0Hex = plan.rollMinHex == 0;
        report.canDecolar1Hex = plan.rollMaxHex >= 1;

        // Full move so quando a decolagem nao exige corrida curta/obrigatoria.
        report.canDecolarFullMove = plan.procedure == TakeoffProcedure.InstantToPreferredHeight;
    }

    private static string BuildAllowedExplanation(PodeDecolarReport report)
    {
        string modes = string.Empty;
        if (report.canDecolar0Hex)
            modes += "0 hex";
        if (report.canDecolar1Hex)
            modes += string.IsNullOrEmpty(modes) ? "1 hex" : ", 1 hex";
        if (report.canDecolarFullMove)
            modes += string.IsNullOrEmpty(modes) ? "full move" : ", full move";

        if (string.IsNullOrEmpty(modes))
            modes = "nenhum modo";

        return $"Decolagem autorizada neste hex. Modos disponiveis: {modes}.";
    }
}
