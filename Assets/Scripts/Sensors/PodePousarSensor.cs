using System.Collections.Generic;
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

/// <summary>
/// Diagnostico de um slot de plataforma transportadora como pista de pouso.
/// Preenchido por <see cref="PodePousarSensor.DescribeTransporterLanding"/>.
/// </summary>
public struct PodePousarSlotReport
{
    public int slotIndex;
    public string slotId;
    public bool available;
    public string reason;
    public int occupiedSeats;
    public int capacity;
}

public static class PodePousarSensor
{
    /// <summary>
    /// Pouso/recuperacao em uma plataforma transportadora. A plataforma nao
    /// e terreno, estrutura ou construcao: seu Aircraft Ops e o slot de
    /// transporte. Ainda assim os gates comuns sao os mesmos do PodePousar
    /// (aeronave em voo e sem lock), e o slot e a fonte de verdade para
    /// camada, classe, skills, bloqueios e vaga.
    /// </summary>
    public static bool CanLandOnTransporter(
        UnitManager aircraft,
        UnitManager transporter,
        out int slotIndex,
        out string reason)
    {
        return DescribeTransporterLanding(
            aircraft, transporter, null, out slotIndex, out reason);
    }

    /// <summary>
    /// Mesma decisao de <see cref="CanLandOnTransporter"/>, opcionalmente
    /// preenchendo o laudo slot a slot. Existe um unico laco: a ferramenta de
    /// diagnostico le exatamente o que o jogo decide, nunca uma copia.
    /// Quando <paramref name="slotOutput"/> e nulo o laco para no primeiro slot
    /// valido, preservando o custo do caminho de decisao.
    /// </summary>
    public static bool DescribeTransporterLanding(
        UnitManager aircraft,
        UnitManager transporter,
        List<PodePousarSlotReport> slotOutput,
        out int slotIndex,
        out string reason)
    {
        slotOutput?.Clear();
        slotIndex = -1;
        if (!AircraftOperationRules.CanAttemptLanding(
                aircraft, out reason))
            return false;
        if (transporter == null || transporter.IsDead
            || transporter.IsEmbarked)
        {
            reason = "Plataforma transportadora invalida.";
            return false;
        }
        if (!PlayerSlotRelations.AreAllies(aircraft, transporter))
        {
            reason = "Plataforma de outro time.";
            return false;
        }
        if (!aircraft.TryGetUnitData(out UnitData aircraftData)
            || aircraftData == null
            || !transporter.TryGetUnitData(out UnitData transporterData)
            || transporterData == null
            || !transporterData.isTransporter
            || transporterData.transportSlots == null)
        {
            reason = "Plataforma sem transport slots configurados.";
            return false;
        }

        string lastReason = "Nenhum slot de pouso compativel.";
        for (int index = 0;
             index < transporterData.transportSlots.Count;
             index++)
        {
            UnitTransportSlotRule slot =
                transporterData.transportSlots[index];
            int capacity = slot != null ? Mathf.Max(1, slot.capacity) : 0;
            int occupied =
                transporter.GetOccupiedTransportSeatCountForSlot(index);

            string slotReason;
            bool available;
            if (!PodeEmbarcarSensor.CanUseSlot(
                    aircraft, aircraftData, slot, out slotReason)
                || !transporter.CanUseTransportSlotExclusivity(
                    index, out slotReason))
            {
                available = false;
            }
            else if (occupied >= capacity)
            {
                available = false;
                slotReason = $"Slot lotado ({occupied}/{capacity}).";
            }
            else
            {
                available = true;
                slotReason = "Slot compativel para pouso.";
            }

            slotOutput?.Add(new PodePousarSlotReport
            {
                slotIndex = index,
                slotId = slot != null ? slot.slotId : "(slot nulo)",
                available = available,
                reason = slotReason,
                occupiedSeats = occupied,
                capacity = capacity
            });

            if (!available)
            {
                lastReason = slotReason;
                continue;
            }

            if (slotIndex < 0)
                slotIndex = index;
            if (slotOutput == null)
                break;
        }

        if (slotIndex >= 0)
        {
            reason = "Pouso autorizado na plataforma transportadora.";
            return true;
        }

        reason = lastReason;
        return false;
    }

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
