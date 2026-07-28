using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum MelhorPousoTier
{
    Tactical,
    Operational
}

/// <summary>Resultado puro de uma LZ que o PodePousar autorizou.</summary>
public sealed class MelhorPousoOption
{
    public Vector3Int cell;
    public MelhorPousoTier tier;
    public int distance;
    public int routeCost = -1;

    /// <summary>Laudo do pouso em superficie. Nulo quando a LZ e um conves.</summary>
    public PodePousarReport landing;

    /// <summary>Transportador que recebe a aeronave. Nulo em LZ de superficie.</summary>
    public UnitManager platform;
    public int platformSlotIndex = -1;
    public string platformReason;

    public bool IsPlatform => platform != null;

    public float score;
}

public sealed class MelhorPousoRequest
{
    public UnitManager aircraft;
    public Tilemap map;
    public TerrainDatabase terrainDatabase;
    public int tacticalBudget;
    public int operationalTurns = 2;
}

public sealed class MelhorPousoResult
{
    public readonly List<MelhorPousoOption> options =
        new List<MelhorPousoOption>();
    public int tacticalBudget;
    public int operationalBudget;
    public int autonomyRemaining;
    public MelhorPousoOption best =>
        options.Count > 0 ? options[0] : null;
}

/// <summary>
/// Consulta de LZ para aeronaves. A regra de pouso pertence inteiramente ao
/// PodePousar; este servico apenas organiza as respostas por alcance tatico e
/// operacional, sem mover ou alterar qualquer estado confirmado.
/// </summary>
public static class MelhorPousoService
{
    public static MelhorPousoResult Evaluate(MelhorPousoRequest request)
    {
        using var perf = new AIDecisionPerfScope(
            request?.aircraft,
            "melhorPouso");
        AIDecisionPerf.AddCount("MelhorPousoCalls");
        var result = new MelhorPousoResult();
        if (request?.aircraft == null || request.map == null
            || request.terrainDatabase == null)
        {
            return result;
        }

        UnitManager aircraft = request.aircraft;
        if (!aircraft.TryGetUnitData(out UnitData aircraftData)
            || aircraftData == null)
        {
            return result;
        }

        Vector3Int origin = aircraft.CurrentCellPosition;
        origin.z = 0;
        // Autonomia e teto duro da consulta. Movimento restante define o que
        // ainda cabe neste turno; autonomia atual tambem limita a projeção
        // operacional, para um aviao quase seco nao procurar LZ do outro lado
        // do mapa apenas porque sua ficha tem movimento alto.
        int autonomy = Mathf.Max(0, aircraft.CurrentFuel);
        int tactical = Mathf.Min(
            Mathf.Max(0, request.tacticalBudget), autonomy);
        int operational = Mathf.Min(
            autonomy,
            tactical * Mathf.Max(1, request.operationalTurns));
        result.tacticalBudget = tactical;
        result.operationalBudget = operational;
        result.autonomyRemaining = autonomy;
        Dictionary<Vector3Int, List<Vector3Int>> tacticalPaths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                request.map, aircraft, tactical,
                request.terrainDatabase);

        List<Vector3Int> candidateCells = CollectCandidateCells(
            request,
            aircraft,
            out bool usedTopologyIndex);
        AIDecisionPerf.AddCount("TopologyIndexQueries");
        AIDecisionPerf.AddCount(
            usedTopologyIndex
                ? "TopologyIndexHits"
                : "TopologyIndexMisses");
        if (!usedTopologyIndex)
            AIDecisionPerf.AddCount("TopologyFullScans");

        int topologyCellsVisited = 0;
        for (int candidateIndex = 0;
             candidateIndex < candidateCells.Count;
             candidateIndex++)
        {
            topologyCellsVisited++;
            Vector3Int cell = candidateCells[candidateIndex];
            cell.z = 0;
            if (!request.map.HasTile(cell))
                continue;

            // Uma passada so sobre os ocupantes do hex: ela responde tanto
            // "tem alguem na superficie?" quanto "esse alguem e um conves que
            // me recebe?".
            InspectSurface(
                request.map, cell, aircraft,
                out bool hasSurfaceOccupant,
                out UnitManager platform,
                out int platformSlot,
                out string platformReason);

            // Pouso em plataforma nao passa pelos dois vetos abaixo, e por
            // desenho: a aeronave vai para um SLOT do transportador, nao para a
            // camada fisica do hex. Logo (a) nao precisa suportar Naval/Surface
            // para pousar numa fragata no mar, e (b) o ocupante de superficie
            // nao inutiliza a LZ — ele E a LZ.
            if (platform == null)
            {
                // Pre-filtro barato: a LZ final sempre fica numa camada fisica
                // do hex. A ficha da aeronave (nativa + modos adicionais) ja
                // informa quais bandas ela suporta; Air nunca e uma LZ de solo.
                // Assim nao chamamos PodePousar para mar/terra que ela jamais
                // conseguiria ocupar.
                if (!LayerTransitionRules.TryResolvePrimaryLayerAtCell(
                        request.map, request.terrainDatabase, cell,
                        out Domain physicalDomain,
                        out HeightLevel physicalHeight,
                        out _)
                    || physicalDomain == Domain.Air
                    || !aircraft.SupportsLayerMode(
                        physicalDomain, physicalHeight))
                {
                    continue;
                }

                // A LZ termina na banda Surface. A consulta generica de
                // ocupacao so acusa o hex quando alcanca seu teto total e,
                // portanto, deixa passar um tanque/navio sozinho sob uma
                // aeronave. Aqui basta uma unidade de superficie para
                // inutilizar a LZ; unidades em Air ou Sub continuam podendo
                // coexistir no mesmo hex.
                if (hasSurfaceOccupant)
                    continue;
            }

            int distance = AIActionReachCoordinator.CubicDistance(
                origin, cell);
            List<Vector3Int> route = null;
            bool tacticalReachable = tacticalPaths != null
                && tacticalPaths.TryGetValue(cell, out route);
            MelhorPousoTier tier;
            if (tacticalReachable)
                tier = MelhorPousoTier.Tactical;
            else if (distance <= operational)
                tier = MelhorPousoTier.Operational;
            else
                continue;

            PodePousarReport landing = null;
            float surfaceBonus;
            if (platform != null)
            {
                // Conves e pista dedicada: mesmo peso de uma RoadRunway.
                surfaceBonus = 250f;
            }
            else
            {
                landing = PodePousarSensor.Evaluate(
                    aircraft, request.map, request.terrainDatabase,
                    SensorMovementMode.MoveuAndando,
                    useManualRemainingMovement: false,
                    manualRemainingMovement: 0,
                    atCell: cell);
                if (landing == null || !landing.status)
                    continue;

                AirOperationTileContext context =
                    AirOperationResolver.ResolveContext(
                        request.map, request.terrainDatabase, cell);
                surfaceBonus = context.landingSurface ==
                    LandingSurface.RoadRunway ? 250f : 0f;
            }

            int routeCost = tacticalReachable && route != null
                ? Mathf.Max(0, route.Count - 1)
                : -1;
            result.options.Add(new MelhorPousoOption
            {
                cell = cell,
                tier = tier,
                distance = distance,
                routeCost = routeCost,
                landing = landing,
                platform = platform,
                platformSlotIndex = platformSlot,
                platformReason = platformReason,
                score = (tier == MelhorPousoTier.Tactical
                    ? 100000f : 50000f)
                    + surfaceBonus - distance * 100f
            });
        }

        AIDecisionPerf.AddCount(
            "TopologyCellsVisited",
            topologyCellsVisited);
        AIDecisionPerf.AddCount(
            "CellsVisited",
            topologyCellsVisited);
        if (usedTopologyIndex)
        {
            AIDecisionPerf.AddCount(
                "TopologyIndexCandidateCells",
                topologyCellsVisited);
        }
        result.options.Sort((a, b) =>
        {
            int scoreCompare = b.score.CompareTo(a.score);
            if (scoreCompare != 0)
                return scoreCompare;
            return a.cell.GetHashCode().CompareTo(b.cell.GetHashCode());
        });
        return result;
    }

    private static List<Vector3Int> CollectCandidateCells(
        MelhorPousoRequest request,
        UnitManager aircraft,
        out bool usedTopologyIndex)
    {
        var cells = new List<Vector3Int>();
        var unique = new HashSet<Vector3Int>();
        BoardTopologyIndex topology =
            BoardTopologyIndex.GetOrCreateRuntime(
                request.map,
                request.terrainDatabase);
        usedTopologyIndex = topology != null && topology.IsReady;

        if (usedTopologyIndex)
        {
            IReadOnlyList<Vector3Int> landingCells =
                topology.PotentialLandingCells;
            for (int i = 0; i < landingCells.Count; i++)
            {
                Vector3Int cell = landingCells[i];
                cell.z = 0;
                if (request.map.HasTile(cell) && unique.Add(cell))
                    cells.Add(cell);
            }

            IReadOnlyList<UnitManager> units =
                ResolveUnitsForPlatformQuery(request.map);
            for (int i = 0; i < units.Count; i++)
            {
                UnitManager candidate = units[i];
                if (candidate == null
                    || candidate == aircraft
                    || !candidate.gameObject.activeInHierarchy
                    || candidate.IsDead
                    || candidate.IsEmbarked
                    || !PodePousarSensor.CanLandOnTransporter(
                        aircraft,
                        candidate,
                        out _,
                        out _))
                {
                    continue;
                }

                Vector3Int cell = candidate.CurrentCellPosition;
                cell.z = 0;
                if (request.map.HasTile(cell) && unique.Add(cell))
                    cells.Add(cell);
            }

            cells.Sort(CompareLegacyCellTraversal);
            return cells;
        }

        foreach (Vector3Int rawCell in
                 request.map.cellBounds.allPositionsWithin)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            cells.Add(cell);
        }
        return cells;
    }

    private static IReadOnlyList<UnitManager>
        ResolveUnitsForPlatformQuery(Tilemap map)
    {
        if (Application.isPlaying)
        {
            if (ConfirmedOccupancyIndex.TryGetFor(
                    map,
                    out ConfirmedOccupancyIndex occupancy)
                && occupancy != null
                && occupancy.CanServeLiveQueries)
            {
                return occupancy.Transporters;
            }
            return UnitManager.AllActive;
        }
        return Object.FindObjectsByType<UnitManager>(
            FindObjectsInactive.Exclude);
    }

    private static int CompareLegacyCellTraversal(
        Vector3Int left,
        Vector3Int right)
    {
        int z = left.z.CompareTo(right.z);
        if (z != 0)
            return z;
        int y = left.y.CompareTo(right.y);
        if (y != 0)
            return y;
        return left.x.CompareTo(right.x);
    }

    /// <summary>
    /// Uma varredura dos ocupantes do hex respondendo as duas perguntas que a
    /// LZ faz: a superficie esta tomada, e existe um conves que aceita esta
    /// aeronave? A regra de conves e do PodePousar (slot, camada, classe,
    /// skills, exclusividade, vaga) — aqui so se escolhe o primeiro que aceita.
    /// </summary>
    private static void InspectSurface(
        Tilemap map,
        Vector3Int cell,
        UnitManager aircraft,
        out bool hasSurfaceOccupant,
        out UnitManager platform,
        out int platformSlotIndex,
        out string platformReason)
    {
        hasSurfaceOccupant = false;
        platform = null;
        platformSlotIndex = -1;
        platformReason = string.Empty;

        List<UnitManager> occupants = UnitOccupancyRules.GetUnitsAtCell(
            map, cell, aircraft);
        for (int i = 0; i < occupants.Count; i++)
        {
            UnitManager occupant = occupants[i];
            if (occupant == null)
                continue;

            if (OccupancyResolver.GetHeightBand(occupant)
                == HeightBand.Blocking)
            {
                hasSurfaceOccupant = true;
            }

            if (platform == null
                && PodePousarSensor.CanLandOnTransporter(
                    aircraft, occupant, out int slot, out string reason))
            {
                platform = occupant;
                platformSlotIndex = slot;
                platformReason = reason;
            }
        }
    }
}
