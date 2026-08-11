using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public delegate bool MelhorDesembarqueTargetResolver(
    UnitManager passenger,
    Vector3Int disembarkCell,
    out Vector3Int targetCell,
    out int remainingRouteCost);

public sealed class MelhorDesembarqueSpotScore
{
    public PodeDesembarcarOption option;
    public Vector3Int target;
    public int routeCost;
}

public sealed class MelhorDesembarqueLzScore
{
    public Vector3Int cell;
    public int moveCost;
    public int delivered;
    public int passengerPriorityCost;
    public int totalRouteCost;
    public int routeProgress;
    public int displayScore;
    public float score;
    public string reason;
    public readonly List<MelhorDesembarqueSpotScore> spots =
        new List<MelhorDesembarqueSpotScore>();
}

public sealed class MelhorDesembarqueRequest
{
    public UnitManager transporter;
    public UnitManager passengerFilter;
    public Tilemap map;
    public TerrainDatabase terrainDatabase;
    public int movementBudget;
    public Dictionary<Vector3Int, List<Vector3Int>> pathsByDestination;
    public MelhorDesembarqueTargetResolver resolvePassengerTarget;
    public IReadOnlyDictionary<int, int> passengerPriorityByInstanceId;
    // Quando definido, a LZ pertence a este passageiro principal. Os demais
    // apenas pegam carona no mesmo desembarque se o matching tambem os servir.
    public int requiredPassengerInstanceId = -1;
    public Func<Vector3Int, bool> allowTransporterCell;
    public Func<Vector3Int, bool> allowDisembarkCell;
    public Func<UnitManager, Vector3Int, bool> allowPassengerDisembarkCell;
    public Action<string> diagnosticLog;
}

public sealed class MelhorDesembarqueResult
{
    public readonly List<MelhorDesembarqueLzScore> ranking =
        new List<MelhorDesembarqueLzScore>();

    public MelhorDesembarqueLzScore best =>
        ranking.Count > 0 ? ranking[0] : null;
}

/// <summary>
/// Consulta pura de planejamento para qualquer unidade que transporte outra unidade.
/// O consumidor fornece a intencao de cada carga; o servico valida movimento,
/// contexto de desembarque, ocupacao e combina passageiros com spots exclusivos.
/// Nao move unidades nem altera ocupacao, FOW, revisoes ou recursos.
/// </summary>
public static class MelhorDesembarqueService
{
    public static MelhorDesembarqueResult Evaluate(MelhorDesembarqueRequest request)
    {
        using var perf = new AIDecisionPerfScope(
            request?.transporter,
            "melhorDesembarque");
        AIDecisionPerf.AddCount("MelhorDesembarqueCalls");
        var result = new MelhorDesembarqueResult();
        if (request == null
            || request.transporter == null
            || request.map == null
            || request.terrainDatabase == null
            || request.resolvePassengerTarget == null)
            return result;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            request.pathsByDestination
            ?? UnitMovementPathRules.CalcularCaminhosValidos(
                request.map,
                request.transporter,
                Mathf.Max(0, request.movementBudget),
                request.terrainDatabase);

        Vector3Int origin = request.transporter.CurrentCellPosition;
        origin.z = 0;
        if (!paths.ContainsKey(origin))
        {
            if (ReferenceEquals(
                    paths, request.pathsByDestination))
            {
                paths = ClonePaths(paths);
            }
            paths[origin] = new List<Vector3Int>();
        }

        HashSet<Vector3Int> structuralLzCells =
            CollectStructuralLzCells(request);
        var options = new List<PodeDesembarcarOption>();
        int lzCellsVisited = 0;
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> path in paths)
        {
            Vector3Int lzCell = path.Key;
            lzCell.z = 0;
            if (structuralLzCells != null
                && !structuralLzCells.Contains(lzCell))
                continue;
            lzCellsVisited++;
            if (request.allowTransporterCell != null
                && !request.allowTransporterCell(lzCell))
            {
                request.diagnosticLog?.Invoke(
                    $"LZ={lzCell} REJECT " +
                    "reason=transporter_cell_not_visible_or_explored");
                continue;
            }
            List<UnitManager> lzOccupants =
                UnitOccupancyRules.GetUnitsAtCell(
                    request.map, lzCell, request.transporter);
            if (!OccupancyResolver.CanEndMove(
                    request.transporter, lzCell, lzOccupants))
                continue;

            if (!PodeDesembarcarSensor.CollectOptionsFromCell(
                    request.transporter,
                    lzCell,
                    request.map,
                    request.terrainDatabase,
                    options,
                    out string contextReason))
                continue;

            var lz = new MelhorDesembarqueLzScore
            {
                cell = lzCell,
                moveCost = ResolvePathCost(path.Value)
            };
            var diagnostics = request.diagnosticLog != null
                ? new List<string>()
                : null;
            SolveBestMatching(request, lz, options, diagnostics);
            if (lz.delivered <= 0)
                continue;

            lz.score = lz.delivered * 100000f
                     - lz.totalRouteCost * 100f
                     - lz.moveCost;
            lz.displayScore = lz.delivered * 100
                            - lz.totalRouteCost
                            - lz.moveCost;
            lz.reason =
                $"pax={lz.delivered} (termo={lz.delivered * 100000}) | " +
                $"prioridade={lz.passengerPriorityCost} | " +
                $"rotaRestante={lz.totalRouteCost} (termo={-lz.totalRouteCost * 100}) | " +
                $"move={lz.moveCost} | sensor={contextReason}";
            result.ranking.Add(lz);

            if (request.diagnosticLog != null)
            {
                request.diagnosticLog(
                    $"LZ={lz.cell} sensorOptions={options.Count} " +
                    $"matching={lz.delivered} move={lz.moveCost} " +
                    $"rotaTotal={lz.totalRouteCost}");
                if (diagnostics != null)
                {
                    for (int i = 0; i < diagnostics.Count; i++)
                        request.diagnosticLog(diagnostics[i]);
                }
            }
        }

        AIDecisionPerf.AddCount(
            "DisembarkLzCellsVisited",
            lzCellsVisited);
        AIDecisionPerf.AddCount(
            "CellsVisited",
            lzCellsVisited);
        result.ranking.Sort(CompareLz);
        MelhorDesembarqueLzScore originLz =
            result.ranking.Find(lz => lz.cell == origin);
        for (int i = 0; i < result.ranking.Count; i++)
        {
            MelhorDesembarqueLzScore lz = result.ranking[i];
            MelhorDesembarqueLzScore reference =
                originLz != null && originLz.delivered == lz.delivered
                    ? originLz
                    : result.ranking.FindLast(
                        other => other.delivered == lz.delivered);
            lz.routeProgress = reference != null
                ? reference.totalRouteCost - lz.totalRouteCost
                : 0;
        }

        return result;
    }

    private static HashSet<Vector3Int> CollectStructuralLzCells(
        MelhorDesembarqueRequest request)
    {
        // Aeronaves embarcadas possuem regras proprias de decolagem. Nao aplique
        // nelas um pre-filtro estrutural terrestre: preserve a autoridade do
        // PodeDesembarcar e use todos os caminhos alcancaveis.
        if (HasEmbarkedAircraft(request))
            return null;

        BoardTopologyIndex topology =
            BoardTopologyIndex.GetOrCreateRuntime(
                request.map,
                request.terrainDatabase);
        bool usedTopology = topology != null && topology.IsReady;
        AIDecisionPerf.AddCount("TopologyIndexQueries");
        AIDecisionPerf.AddCount(
            usedTopology ? "TopologyIndexHits" : "TopologyIndexMisses");
        if (!usedTopology)
            return null;

        var lzCells = new HashSet<Vector3Int>();
        IReadOnlyList<Vector3Int> spots =
            topology.PotentialDisembarkCells;
        for (int i = 0; i < spots.Count; i++)
        {
            Vector3Int spot = spots[i];
            spot.z = 0;
            IReadOnlyList<Vector3Int> neighbors =
                topology.GetNeighbors(spot);
            for (int n = 0; n < neighbors.Count; n++)
            {
                Vector3Int lz = neighbors[n];
                lz.z = 0;
                lzCells.Add(lz);
            }
        }
        AIDecisionPerf.AddCount(
            "MelhorDesembarqueStructuralLzCandidates",
            lzCells.Count);
        return lzCells;
    }

    private static bool HasEmbarkedAircraft(
        MelhorDesembarqueRequest request)
    {
        if (request?.transporter == null)
            return false;

        IReadOnlyList<UnitManager> units = null;
        if (Application.isPlaying
            && ConfirmedOccupancyIndex.TryGetFor(
                request.map,
                out ConfirmedOccupancyIndex occupancy)
            && occupancy != null
            && occupancy.CanServeLiveQueries)
        {
            units = occupancy.GetEmbarkedPassengers(
                request.transporter);
        }
        else
        {
            units = UnitManager.AllActive;
        }

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager passenger = units[i];
            if (passenger == null
                || !passenger.IsEmbarked
                || passenger.EmbarkedTransporter
                    != request.transporter
                || !passenger.TryGetUnitData(
                    out UnitData data)
                || data == null)
                continue;
            if (data.domain == Domain.Air)
                return true;
        }
        return false;
    }

    private static int CompareLz(
        MelhorDesembarqueLzScore a,
        MelhorDesembarqueLzScore b)
    {
        int byDelivered = b.delivered.CompareTo(a.delivered);
        if (byDelivered != 0) return byDelivered;
        int byPassengerPriority =
            a.passengerPriorityCost.CompareTo(b.passengerPriorityCost);
        if (byPassengerPriority != 0) return byPassengerPriority;
        int byRoute = a.totalRouteCost.CompareTo(b.totalRouteCost);
        if (byRoute != 0) return byRoute;
        return a.moveCost.CompareTo(b.moveCost);
    }

    private static void SolveBestMatching(
        MelhorDesembarqueRequest request,
        MelhorDesembarqueLzScore lz,
        List<PodeDesembarcarOption> options,
        List<string> diagnostics)
    {
        var scored = new List<MelhorDesembarqueSpotScore>();
        for (int i = 0; i < options.Count; i++)
        {
            PodeDesembarcarOption option = options[i];
            if (option?.passengerUnit == null)
                continue;
            if (request.passengerFilter != null
                && option.passengerUnit != request.passengerFilter)
                continue;

            Vector3Int spotCell = option.disembarkCell;
            spotCell.z = 0;
            if (request.allowDisembarkCell != null
                && !request.allowDisembarkCell(spotCell))
            {
                diagnostics?.Add(
                    $"  REJECT pax=#{option.passengerUnit.InstanceId} " +
                    $"spot={spotCell} reason=confirmed_visibility");
                continue;
            }
            if (request.allowPassengerDisembarkCell != null
                && !request.allowPassengerDisembarkCell(
                    option.passengerUnit,
                    spotCell))
            {
                diagnostics?.Add(
                    $"  REJECT pax=#{option.passengerUnit.InstanceId} " +
                    $"spot={spotCell} reason=passenger_policy");
                continue;
            }
            List<UnitManager> spotOccupants =
                UnitOccupancyRules.GetUnitsAtCell(
                    request.map, spotCell, option.passengerUnit);
            if (!OccupancyResolver.CanEndMove(
                    option.passengerUnit, spotCell, spotOccupants))
            {
                diagnostics?.Add(
                    $"  REJECT pax=#{option.passengerUnit.InstanceId} " +
                    $"spot={spotCell} reason=occupancy " +
                    $"occupants={spotOccupants.Count}");
                continue;
            }

            if (!request.resolvePassengerTarget(
                    option.passengerUnit,
                    spotCell,
                    out Vector3Int target,
                    out int routeCost))
            {
                diagnostics?.Add(
                    $"  REJECT pax=#{option.passengerUnit.InstanceId} " +
                    $"spot={spotCell} reason=route_or_limit");
                continue;
            }

            target.z = 0;
            diagnostics?.Add(
                $"  ACCEPT pax=#{option.passengerUnit.InstanceId} " +
                $"spot={spotCell} target={target} route={routeCost}");
            scored.Add(new MelhorDesembarqueSpotScore
            {
                option = option,
                target = target,
                routeCost = routeCost
            });
        }

        var passengers = new List<UnitManager>();
        for (int i = 0; i < scored.Count; i++)
        {
            UnitManager passenger = scored[i].option.passengerUnit;
            if (!passengers.Contains(passenger))
                passengers.Add(passenger);
        }
        passengers.Sort((a, b) =>
            ResolvePassengerPriority(request, a)
                .CompareTo(ResolvePassengerPriority(request, b)));

        SearchMatching(
            request,
            0,
            passengers,
            scored,
            new List<MelhorDesembarqueSpotScore>(),
            new HashSet<Vector3Int>(),
            lz);
    }

    private static void SearchMatching(
        MelhorDesembarqueRequest request,
        int passengerIndex,
        List<UnitManager> passengers,
        List<MelhorDesembarqueSpotScore> candidates,
        List<MelhorDesembarqueSpotScore> current,
        HashSet<Vector3Int> used,
        MelhorDesembarqueLzScore best)
    {
        if (passengerIndex >= passengers.Count)
        {
            if (request.requiredPassengerInstanceId >= 0)
            {
                bool containsRequired = false;
                for (int i = 0; i < current.Count; i++)
                {
                    if (current[i].option.passengerUnit.InstanceId
                        == request.requiredPassengerInstanceId)
                    {
                        containsRequired = true;
                        break;
                    }
                }
                if (!containsRequired)
                    return;
            }

            int count = current.Count;
            int cost = 0;
            int priorityCost = 0;
            for (int i = 0; i < current.Count; i++)
            {
                cost += current[i].routeCost;
                priorityCost += ResolvePassengerPriority(
                    request, current[i].option.passengerUnit);
            }
            if (count < best.delivered
                || (count == best.delivered
                    && priorityCost > best.passengerPriorityCost)
                || (count == best.delivered
                    && priorityCost == best.passengerPriorityCost
                    && cost >= best.totalRouteCost))
                return;

            best.delivered = count;
            best.passengerPriorityCost = priorityCost;
            best.totalRouteCost = cost;
            best.spots.Clear();
            best.spots.AddRange(current);
            return;
        }

        SearchMatching(
            request,
            passengerIndex + 1, passengers, candidates, current, used, best);
        UnitManager passenger = passengers[passengerIndex];
        for (int i = 0; i < candidates.Count; i++)
        {
            MelhorDesembarqueSpotScore spot = candidates[i];
            Vector3Int cell = spot.option.disembarkCell;
            if (spot.option.passengerUnit != passenger || used.Contains(cell))
                continue;

            used.Add(cell);
            current.Add(spot);
            SearchMatching(
                request,
                passengerIndex + 1, passengers, candidates, current, used, best);
            current.RemoveAt(current.Count - 1);
            used.Remove(cell);
        }
    }

    private static int ResolvePathCost(List<Vector3Int> path) =>
        path == null ? 0 : Mathf.Max(0, path.Count - 1);

    private static Dictionary<Vector3Int, List<Vector3Int>>
        ClonePaths(
            Dictionary<Vector3Int, List<Vector3Int>> source)
    {
        var clone =
            new Dictionary<Vector3Int, List<Vector3Int>>(
                source?.Count ?? 0);
        if (source == null)
            return clone;
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair
                 in source)
        {
            clone[pair.Key] = pair.Value != null
                ? new List<Vector3Int>(pair.Value)
                : null;
        }
        return clone;
    }

    private static int ResolvePassengerPriority(
        MelhorDesembarqueRequest request,
        UnitManager passenger)
    {
        if (passenger != null
            && request?.passengerPriorityByInstanceId != null
            && request.passengerPriorityByInstanceId.TryGetValue(
                passenger.InstanceId, out int priority))
            return Mathf.Max(0, priority);
        return int.MaxValue / 1024;
    }
}
