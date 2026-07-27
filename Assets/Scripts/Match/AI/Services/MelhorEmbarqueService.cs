using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum MelhorEmbarqueTier
{
    Tactical,
    Operational,
    Strategic
}

public sealed class MelhorEmbarquePassengerScore
{
    public UnitManager passenger;
    public int slotIndex;
    public Vector3Int passengerCell;
    public int passengerMoveCost;
    public string reason;
}

public sealed class MelhorEmbarqueLzScore
{
    public Vector3Int cell;
    public MelhorEmbarqueTier tier;
    public int transporterDistance;
    public int transporterRouteCost;
    public float score;
    public string reason;
    public readonly List<MelhorEmbarquePassengerScore> passengers =
        new List<MelhorEmbarquePassengerScore>();
}

public sealed class MelhorEmbarqueReject
{
    public UnitManager passenger;
    public string reason;
}

public sealed class MelhorEmbarqueRequest
{
    public UnitManager transporter;
    public Tilemap map;
    public TerrainDatabase terrainDatabase;
    public int tacticalBudget;
    public int operationalTurns = 2;
    public bool includeStrategic;
    public Func<UnitManager, bool> allowPassenger;
    public Action<string> diagnosticLog;
}

public sealed class MelhorEmbarqueResult
{
    public readonly List<MelhorEmbarqueLzScore> ranking =
        new List<MelhorEmbarqueLzScore>();
    public readonly List<MelhorEmbarqueReject> rejectedPassengers =
        new List<MelhorEmbarqueReject>();

    public MelhorEmbarqueLzScore best =>
        ranking.Count > 0 ? ranking[0] : null;
}

/// <summary>
/// Consulta pura para coleta: transportador -> LZ permitido <- passageiro.
/// A origem das ondas e sempre o transportador. O UnitData decide quais
/// terrenos, pares estrutura+terreno e construcoes podem ser LZ.
/// </summary>
public static class MelhorEmbarqueService
{
    public static MelhorEmbarqueResult Evaluate(
        MelhorEmbarqueRequest request)
    {
        var result = new MelhorEmbarqueResult();
        if (request?.transporter == null
            || request.map == null
            || request.terrainDatabase == null
            || !request.transporter.TryGetUnitData(
                out UnitData transporterData)
            || transporterData == null
            || !transporterData.isTransporter
            || transporterData.transportSlots == null
            || transporterData.transportSlots.Count == 0)
            return result;

        Vector3Int origin = request.transporter.CurrentCellPosition;
        origin.z = 0;
        int tactical = Mathf.Max(0, request.tacticalBudget);
        int operational = tactical * Mathf.Max(1, request.operationalTurns);
        Dictionary<Vector3Int, List<Vector3Int>> tacticalPaths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                request.map, request.transporter, tactical,
                request.terrainDatabase);

        var passengers = new List<UnitManager>();
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (!TryResolvePassengerSlot(
                    request, transporterData, unit,
                    out int slotIndex, out string reason))
            {
                if (unit != null
                    && unit != request.transporter
                    && PlayerSlotRelations.AreAllies(
                        request.transporter, unit))
                {
                    result.rejectedPassengers.Add(
                        new MelhorEmbarqueReject
                        {
                            passenger = unit,
                            reason = reason
                        });
                }
                continue;
            }

            passengers.Add(unit);
            request.diagnosticLog?.Invoke(
                $"ACCEPT pax=#{unit.InstanceId} slot={slotIndex}");
        }

        BoundsInt bounds = request.map.cellBounds;
        foreach (Vector3Int rawCell in bounds.allPositionsWithin)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (!request.map.HasTile(cell)
                || !PodeEmbarcarSensor.IsTransporterCellValidForEmbark(
                    request.map, request.terrainDatabase,
                    transporterData, cell))
                continue;

            if (request.transporter.GetDomain() == Domain.Air
                && !PodePousarSensor.CanLandAtCell(
                    request.transporter, request.map,
                    request.terrainDatabase, cell, out _))
                continue;

            int distance = Mathf.RoundToInt(
                SectorManager.HexDistance(origin, cell));
            MelhorEmbarqueTier tier = distance <= tactical
                ? MelhorEmbarqueTier.Tactical
                : distance <= operational
                    ? MelhorEmbarqueTier.Operational
                    : MelhorEmbarqueTier.Strategic;
            if (tier == MelhorEmbarqueTier.Strategic
                && !request.includeStrategic)
                continue;

            var lz = new MelhorEmbarqueLzScore
            {
                cell = cell,
                tier = tier,
                transporterDistance = distance,
                transporterRouteCost =
                    tacticalPaths != null
                    && tacticalPaths.TryGetValue(
                        cell, out List<Vector3Int> route)
                        ? Mathf.Max(0, route.Count - 1)
                        : -1
            };

            for (int i = 0; i < passengers.Count; i++)
            {
                UnitManager passenger = passengers[i];
                if (!TryResolvePassengerSlot(
                        request, transporterData, passenger,
                        out int slotIndex, out _))
                    continue;
                if (!TryResolvePassengerMeetingCost(
                        request, passenger, cell, out int moveCost))
                    continue;

                Vector3Int passengerCell =
                    passenger.CurrentCellPosition;
                passengerCell.z = 0;
                lz.passengers.Add(
                    new MelhorEmbarquePassengerScore
                    {
                        passenger = passenger,
                        slotIndex = slotIndex,
                        passengerCell = passengerCell,
                        passengerMoveCost = moveCost,
                        reason =
                            $"slot={slotIndex} encontro={cell} " +
                            $"movePax={moveCost}"
                    });
            }

            if (lz.passengers.Count == 0)
                continue;

            lz.score = lz.passengers.Count * 100000f
                     - distance * 100f
                     - lz.passengers[0].passengerMoveCost;
            lz.reason =
                $"tier={tier} pax={lz.passengers.Count} " +
                $"distTransport={distance} " +
                $"rotaTatica={lz.transporterRouteCost}";

            result.ranking.Add(lz);
            request.diagnosticLog?.Invoke(
                $"LZ={cell} {lz.reason}");
        }

        result.ranking.Sort(Compare);
        return result;
    }

    private static bool TryResolvePassengerSlot(
        MelhorEmbarqueRequest request,
        UnitData transporterData,
        UnitManager passenger,
        out int slotIndex,
        out string reason)
    {
        slotIndex = -1;
        reason = string.Empty;
        if (passenger == null || passenger == request.transporter)
        {
            reason = "unidade invalida";
            return false;
        }
        if (passenger.IsDead || passenger.IsEmbarked)
        {
            reason = passenger.IsDead ? "morta" : "ja embarcada";
            return false;
        }
        if (!PlayerSlotRelations.AreAllies(
                request.transporter, passenger))
        {
            reason = "nao aliada";
            return false;
        }
        if (request.allowPassenger != null
            && !request.allowPassenger(passenger))
        {
            reason = "bloqueada pelo consumidor";
            return false;
        }
        if (!passenger.TryGetUnitData(out UnitData passengerData)
            || passengerData == null)
        {
            reason = "sem UnitData";
            return false;
        }

        string lastReason = "nenhum slot compativel";
        for (int i = 0; i < transporterData.transportSlots.Count; i++)
        {
            UnitTransportSlotRule slot =
                transporterData.transportSlots[i];
            if (!PodeEmbarcarSensor.CanUseSlot(
                    passenger, passengerData, slot,
                    out lastReason))
                continue;
            if (!request.transporter.CanUseTransportSlotExclusivity(
                    i, out lastReason))
                continue;
            int occupied =
                request.transporter.GetOccupiedTransportSeatCountForSlot(i);
            if (occupied >= Mathf.Max(1, slot.capacity))
            {
                lastReason = $"slot {i} lotado";
                continue;
            }

            slotIndex = i;
            reason = "compativel";
            return true;
        }

        reason = lastReason;
        return false;
    }

    private static bool TryResolvePassengerMeetingCost(
        MelhorEmbarqueRequest request,
        UnitManager passenger,
        Vector3Int transporterCell,
        out int moveCost)
    {
        moveCost = int.MaxValue;
        int budget = Mathf.Max(0, passenger.RemainingMovementPoints);
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                request.map, passenger, budget,
                request.terrainDatabase);
        if (paths == null)
            return false;

        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in paths)
        {
            Vector3Int stop = pair.Key;
            stop.z = 0;
            if (SectorManager.HexDistance(
                    stop, transporterCell) > 1.5f)
                continue;
            int cost = Mathf.Max(0, pair.Value.Count - 1);
            if (cost < moveCost)
                moveCost = cost;
        }
        return moveCost < int.MaxValue;
    }

    private static int Compare(
        MelhorEmbarqueLzScore a,
        MelhorEmbarqueLzScore b)
    {
        int byTier = a.tier.CompareTo(b.tier);
        if (byTier != 0) return byTier;
        int byPassengers =
            b.passengers.Count.CompareTo(a.passengers.Count);
        if (byPassengers != 0) return byPassengers;
        int byDistance =
            a.transporterDistance.CompareTo(b.transporterDistance);
        if (byDistance != 0) return byDistance;
        return b.score.CompareTo(a.score);
    }
}
