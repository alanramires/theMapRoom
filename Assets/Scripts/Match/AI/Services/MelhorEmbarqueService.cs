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

public enum MelhorEmbarquePassengerRouteState
{
    ReachableNow,
    ReachableLater,
    NoCurrentRoute
}

public enum MelhorEmbarqueRideDisposition
{
    NotEvaluated,
    Emergency,
    Requested,
    OpportunisticFallback
}

/// <summary>
/// Uma combinação passageiro-LZ. É um fato classificado da consulta, não uma
/// ordem para aproximar, esperar ou embarcar.
/// </summary>
public sealed class MelhorEmbarqueOption
{
    public UnitManager passenger;
    public int slotIndex;
    public Vector3Int passengerCell;
    public Vector3Int lzCell;
    public MelhorEmbarqueTier transporterTier;
    public int transporterDistance;
    public int transporterRouteCost;
    public MelhorEmbarquePassengerRouteState passengerRouteState;
    public int passengerRouteCost = -1;
    public MelhorEmbarqueRideDisposition rideDisposition =
        MelhorEmbarqueRideDisposition.NotEvaluated;
    public QueroCaronaResult rideNeed;
    public float rideNeedAdjustment;
    public float score;
    public string reason;
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
    public Func<UnitManager, bool> includeInLegacyRanking;
    public Func<UnitManager, QueroCaronaResult> evaluateRideNeed;
    public Action<string> diagnosticLog;
}

public sealed class MelhorEmbarqueResult
{
    public readonly List<MelhorEmbarqueOption> options =
        new List<MelhorEmbarqueOption>();
    public readonly List<MelhorEmbarqueLzScore> ranking =
        new List<MelhorEmbarqueLzScore>();
    public readonly List<MelhorEmbarqueReject> rejectedPassengers =
        new List<MelhorEmbarqueReject>();

    public MelhorEmbarqueLzScore best =>
        ranking.Count > 0 ? ranking[0] : null;
    public MelhorEmbarqueOption bestOption =>
        options.Count > 0 ? options[0] : null;
}

/// <summary>
/// Consulta pura para coleta: transportador -> LZ permitido <- passageiro.
/// A origem das ondas e sempre o transportador. O UnitData decide quais
/// terrenos, pares estrutura+terreno e construcoes podem ser LZ.
/// </summary>
public static class MelhorEmbarqueService
{
    private sealed class PassengerReachProfile
    {
        public Dictionary<Vector3Int, int> now;
        public Dictionary<Vector3Int, int> later;
    }

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
        var passengerSlots = new Dictionary<UnitManager, int>();
        var passengerReach =
            new Dictionary<UnitManager, PassengerReachProfile>();
        var passengerRideNeed =
            new Dictionary<UnitManager, QueroCaronaResult>();
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
            passengerSlots[unit] = slotIndex;
            passengerReach[unit] = BuildPassengerReachProfile(
                request, unit);
            passengerRideNeed[unit] =
                request.evaluateRideNeed?.Invoke(unit);
            request.diagnosticLog?.Invoke(
                $"ACCEPT pax=#{unit.InstanceId} slot={slotIndex} " +
                FormatRideNeedDiagnostic(passengerRideNeed[unit]));
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
            int optionCountBeforeLz = result.options.Count;

            for (int i = 0; i < passengers.Count; i++)
            {
                UnitManager passenger = passengers[i];
                Vector3Int passengerCell =
                    passenger.CurrentCellPosition;
                passengerCell.z = 0;
                ResolvePassengerMeeting(
                    passengerReach[passenger],
                    cell,
                    out MelhorEmbarquePassengerRouteState routeState,
                    out int moveCost);
                int slotIndex = passengerSlots[passenger];
                QueroCaronaResult rideNeed =
                    passengerRideNeed[passenger];
                MelhorEmbarqueRideDisposition disposition =
                    ResolveRideDisposition(rideNeed);
                float rideNeedAdjustment =
                    ResolveRideNeedAdjustment(
                        disposition, rideNeed);
                // A grade de movimento do passageiro pode nao conter uma
                // LZ naval mesmo que o slot aceite a camada atual dele
                // (ex.: Apache Air/High -> helipad da Fragata). Isso nao
                // invalida o resgate: apenas significa que o transportador
                // deve aproximar-se da LZ mais proxima do passageiro, para
                // que os sensores de embarque resolvam a transicao final.
                // Sem esta distancia, o hex atual do transportador ganha
                // por custo zero e ele fica esperando longe do passageiro.
                float rescueApproachPenalty = routeState ==
                    MelhorEmbarquePassengerRouteState.NoCurrentRoute
                    ? SectorManager.HexDistance(passengerCell, cell)
                        * 10000f
                    : 0f;
                float optionScore = 100000f
                    - distance * 100f
                    - ResolvePassengerRoutePenalty(
                        routeState, moveCost)
                    - rescueApproachPenalty
                    + rideNeedAdjustment;
                var option = new MelhorEmbarqueOption
                {
                    passenger = passenger,
                    slotIndex = slotIndex,
                    passengerCell = passengerCell,
                    lzCell = cell,
                    transporterTier = tier,
                    transporterDistance = distance,
                    transporterRouteCost =
                        lz.transporterRouteCost,
                    passengerRouteState = routeState,
                    passengerRouteCost =
                        moveCost < int.MaxValue ? moveCost : -1,
                    rideDisposition = disposition,
                    rideNeed = rideNeed,
                    rideNeedAdjustment = rideNeedAdjustment,
                    score = optionScore,
                    reason =
                        $"slot={slotIndex} encontro={cell} " +
                        $"rotaPax={routeState} " +
                        $"custoPax=" +
                        $"{(moveCost < int.MaxValue ? moveCost.ToString() : "n/a")} " +
                        $"distTransport={distance} " +
                        $"aproxPax={SectorManager.HexDistance(passengerCell, cell):F0} " +
                        $"carona={disposition} " +
                        $"ajusteCarona={rideNeedAdjustment:0}"
                };
                result.options.Add(option);

                // Compatibilidade da parte 1: o controller atual enxerga a
                // coleção legada somente quando o passageiro chega agora.
                // ReachableLater/NoCurrentRoute já aparecem no ranking plano,
                // mas só passam a influenciar decisões nas próximas partes.
                if (routeState ==
                        MelhorEmbarquePassengerRouteState.ReachableNow
                    && (request.includeInLegacyRanking == null
                        || request.includeInLegacyRanking(passenger)))
                {
                    lz.passengers.Add(
                        new MelhorEmbarquePassengerScore
                        {
                            passenger = passenger,
                            slotIndex = slotIndex,
                            passengerCell = passengerCell,
                            passengerMoveCost = moveCost,
                            reason = option.reason
                        });
                }
            }

            int optionCountForLz =
                result.options.Count - optionCountBeforeLz;
            if (optionCountForLz <= 0)
                continue;
            if (lz.passengers.Count == 0)
            {
                request.diagnosticLog?.Invoke(
                    $"LZ={cell} tier={tier} opcoes={optionCountForLz} " +
                    "sem passageiro ReachableNow; preservada apenas no " +
                    "ranking plano.");
                continue;
            }

            int legacyFirstCost = lz.passengers.Count > 0
                ? lz.passengers[0].passengerMoveCost
                : 0;
            lz.score = lz.passengers.Count * 100000f
                     - distance * 100f
                     - legacyFirstCost;
            lz.reason =
                $"tier={tier} paxAgora={lz.passengers.Count} " +
                $"opcoes={optionCountForLz} " +
                $"distTransport={distance} " +
                $"rotaTatica={lz.transporterRouteCost}";

            result.ranking.Add(lz);
            request.diagnosticLog?.Invoke(
                $"LZ={cell} {lz.reason}");
        }

        result.ranking.Sort(Compare);
        result.options.Sort(CompareOptions);
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

    private static PassengerReachProfile BuildPassengerReachProfile(
        MelhorEmbarqueRequest request,
        UnitManager passenger)
    {
        Vector3Int origin = passenger.CurrentCellPosition;
        origin.z = 0;
        int nowBudget = Mathf.Max(
            0, passenger.RemainingMovementPoints);
        int laterBudget = Mathf.Max(
            nowBudget,
            passenger.MaxMovementPoints
            * Mathf.Max(1, request.operationalTurns));
        return new PassengerReachProfile
        {
            now = UnitMovementPathRules.CalculateMovementCostMap(
                request.map, passenger, origin, nowBudget,
                request.terrainDatabase),
            later = UnitMovementPathRules.CalculateMovementCostMap(
                request.map, passenger, origin, laterBudget,
                request.terrainDatabase)
        };
    }

    private static void ResolvePassengerMeeting(
        PassengerReachProfile profile,
        Vector3Int transporterCell,
        out MelhorEmbarquePassengerRouteState state,
        out int moveCost)
    {
        if (TryFindMeetingCost(
                profile?.now, transporterCell, out moveCost))
        {
            state = MelhorEmbarquePassengerRouteState.ReachableNow;
            return;
        }
        if (TryFindMeetingCost(
                profile?.later, transporterCell, out moveCost))
        {
            state = MelhorEmbarquePassengerRouteState.ReachableLater;
            return;
        }

        state = MelhorEmbarquePassengerRouteState.NoCurrentRoute;
        moveCost = int.MaxValue;
    }

    private static bool TryFindMeetingCost(
        Dictionary<Vector3Int, int> costs,
        Vector3Int transporterCell,
        out int moveCost)
    {
        moveCost = int.MaxValue;
        if (costs == null)
            return false;
        foreach (KeyValuePair<Vector3Int, int> pair in costs)
        {
            Vector3Int stop = pair.Key;
            stop.z = 0;
            if (SectorManager.HexDistance(
                    stop, transporterCell) > 1.5f)
                continue;
            if (pair.Value < moveCost)
                moveCost = pair.Value;
        }
        return moveCost < int.MaxValue;
    }

    private static float ResolvePassengerRoutePenalty(
        MelhorEmbarquePassengerRouteState state,
        int moveCost)
    {
        switch (state)
        {
            case MelhorEmbarquePassengerRouteState.ReachableNow:
                return Mathf.Max(0, moveCost);
            case MelhorEmbarquePassengerRouteState.ReachableLater:
                return 1000f + Mathf.Max(0, moveCost);
            default:
                return 5000f;
        }
    }

    private static MelhorEmbarqueRideDisposition
        ResolveRideDisposition(QueroCaronaResult rideNeed)
    {
        if (rideNeed == null)
            return MelhorEmbarqueRideDisposition.NotEvaluated;
        if (rideNeed.isEmergency)
            return MelhorEmbarqueRideDisposition.Emergency;
        return rideNeed.wantsRide
            ? MelhorEmbarqueRideDisposition.Requested
            : MelhorEmbarqueRideDisposition.OpportunisticFallback;
    }

    private static float ResolveRideNeedAdjustment(
        MelhorEmbarqueRideDisposition disposition,
        QueroCaronaResult rideNeed)
    {
        switch (disposition)
        {
            case MelhorEmbarqueRideDisposition.Emergency:
                return Mathf.Max(
                    2000f, rideNeed?.rideNeedScore ?? 0);
            case MelhorEmbarqueRideDisposition.Requested:
                return Mathf.Max(
                    1000f, rideNeed?.rideNeedScore ?? 0);
            case MelhorEmbarqueRideDisposition.OpportunisticFallback:
                return -5000f;
            default:
                return 0f;
        }
    }

    private static string FormatRideNeedDiagnostic(
        QueroCaronaResult rideNeed)
    {
        if (rideNeed == null)
            return "carona=NotEvaluated";
        MelhorEmbarqueRideDisposition disposition =
            ResolveRideDisposition(rideNeed);
        return $"carona={disposition} " +
               $"ajuste={ResolveRideNeedAdjustment(disposition, rideNeed):0} " +
               $"motivo={rideNeed.reason}";
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

    private static int CompareOptions(
        MelhorEmbarqueOption a,
        MelhorEmbarqueOption b)
    {
        int byTier = a.transporterTier.CompareTo(
            b.transporterTier);
        if (byTier != 0) return byTier;
        int byScore = b.score.CompareTo(a.score);
        if (byScore != 0) return byScore;
        int byPassenger = a.passenger != null
            && b.passenger != null
                ? a.passenger.InstanceId.CompareTo(
                    b.passenger.InstanceId)
                : 0;
        if (byPassenger != 0) return byPassenger;
        int byX = a.lzCell.x.CompareTo(b.lzCell.x);
        return byX != 0 ? byX : a.lzCell.y.CompareTo(b.lzCell.y);
    }
}
