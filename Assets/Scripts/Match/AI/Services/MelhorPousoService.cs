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
    public PodePousarReport landing;
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

        foreach (Vector3Int rawCell in
                 request.map.cellBounds.allPositionsWithin)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (!request.map.HasTile(cell))
                continue;

            // Pre-filtro barato: a LZ final sempre fica numa camada fisica do
            // hex. A ficha da aeronave (nativa + modos adicionais) ja informa
            // quais bandas ela suporta; Air nunca e uma LZ de solo. Assim nao
            // chamamos PodePousar para mar/terra que ela jamais conseguiria
            // ocupar.
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

            // A LZ termina na banda Surface. A consulta generica de ocupacao
            // so acusa o hex quando alcanca seu teto total e, portanto, deixa
            // passar um tanque/navio sozinho sob uma aeronave. Aqui basta uma
            // unidade de superficie para inutilizar a LZ; unidades em Air ou
            // Sub continuam podendo coexistir no mesmo hex.
            if (HasSurfaceOccupant(request.map, cell, aircraft))
            {
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

            PodePousarReport landing = PodePousarSensor.Evaluate(
                aircraft, request.map, request.terrainDatabase,
                SensorMovementMode.MoveuAndando,
                useManualRemainingMovement: false,
                manualRemainingMovement: 0,
                atCell: cell);
            if (landing == null || !landing.status)
                continue;

            int routeCost = tacticalReachable && route != null
                ? Mathf.Max(0, route.Count - 1)
                : -1;
            AirOperationTileContext context =
                AirOperationResolver.ResolveContext(
                    request.map, request.terrainDatabase, cell);
            float surfaceBonus = context.landingSurface ==
                LandingSurface.RoadRunway ? 250f : 0f;
            result.options.Add(new MelhorPousoOption
            {
                cell = cell,
                tier = tier,
                distance = distance,
                routeCost = routeCost,
                landing = landing,
                score = (tier == MelhorPousoTier.Tactical
                    ? 100000f : 50000f)
                    + surfaceBonus - distance * 100f
            });
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

    private static bool HasSurfaceOccupant(
        Tilemap map,
        Vector3Int cell,
        UnitManager exceptUnit)
    {
        List<UnitManager> occupants = UnitOccupancyRules.GetUnitsAtCell(
            map, cell, exceptUnit);
        for (int i = 0; i < occupants.Count; i++)
        {
            UnitManager occupant = occupants[i];
            if (occupant != null
                && OccupancyResolver.GetHeightBand(occupant)
                    == HeightBand.Blocking)
            {
                return true;
            }
        }

        return false;
    }
}
