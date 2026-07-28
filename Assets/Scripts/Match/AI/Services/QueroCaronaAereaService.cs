using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Pedido de plataforma para uma aeronave de combate ou Vigilancia Aerea. Esta
/// consulta nao escolhe uma acao: a recuperacao e o embarque continuam sendo
/// materializados pelos sensores e pelo controlador da IA.
/// </summary>
public sealed class QueroCaronaAereaRequest
{
    public UnitManager aircraft;
    public Tilemap map;
    public TerrainDatabase terrainDatabase;
    public int operationalTurns = 2;
    public bool emulateUnderRepairFromUnitData;
    public MelhorPousoResult landingSnapshot;

    // Um foco e opcional porque patrulha/interceptacao ainda nao tem um plano
    // de setor proprio. Quando informado, ele permite testar se uma plataforma
    // realmente melhora o posicionamento da missao em vez de ser apenas carona.
    public bool hasMissionFocus;
    public Vector3Int missionFocus;
    public float minimumMissionDistanceGain = 1f;

    // Uma plataforma pode ser a unica recuperacao compativel no horizonte.
    // Fora de emergencia ela so e aceita se nao afastar demais a missao.
    public bool acceptPlatformWhenOnlyRecovery;
    public float maximumMissionRegressionForRecovery;
}

public sealed class QueroCaronaAereaResult
{
    public bool isSupportedAirRole;
    public bool isAirCombatRole;
    public bool isAirSurveillanceRole;
    public bool wantsRide;
    public bool isEmergency;
    public bool isOnlyCompatibleRecovery;
    public string reason;
    public string repairEvaluation;
    public int tacticalBudget;
    public int operationalBudget;
    public int platformCount;
    public float currentMissionDistance;
    public float platformMissionDistance;
    public float missionDistanceGain;
    public MelhorPousoOption bestPlatform;
    public readonly List<MelhorPousoOption> platforms =
        new List<MelhorPousoOption>();
}

/// <summary>
/// Contrapeso aereo do Melhor LZ de Embarque. A plataforma e descoberta pelo
/// MelhorPouso/PodePousar, portanto slot, classe, skills, vaga e exclusividade
/// continuam tendo uma unica autoridade.
/// </summary>
public static class QueroCaronaAereaService
{
    public static QueroCaronaAereaResult Evaluate(
        QueroCaronaAereaRequest request)
    {
        using var perf = new AIDecisionPerfScope(
            request?.aircraft,
            "queroCaronaAerea");
        AIDecisionPerf.AddCount("QueroCaronaAereaCalls");
        var result = new QueroCaronaAereaResult
        {
            reason = "Aeronave nao avaliada."
        };
        if (request?.aircraft == null || request.map == null
            || request.terrainDatabase == null)
        {
            result.reason = "Aeronave, tilemap ou catalogo de terrenos ausente.";
            return result;
        }

        UnitManager aircraft = request.aircraft;
        if (!aircraft.TryGetUnitData(out UnitData data) || data == null)
        {
            result.reason = "Aeronave sem UnitData valido.";
            return result;
        }

        bool nativeAir = data.domain == Domain.Air;
        result.isAirCombatRole = nativeAir
            && data.roles != null
            && (data.roles.Contains(UnitRole.Interceptador)
                || data.roles.Contains(UnitRole.AtaqueAereo));
        result.isAirSurveillanceRole = nativeAir
            && data.roles != null
            && data.roles.Contains(UnitRole.VigilanciaAerea);
        result.isSupportedAirRole =
            result.isAirCombatRole
            || result.isAirSurveillanceRole;
        if (!result.isSupportedAirRole)
        {
            result.reason =
                "A ferramenta atende aeronaves com papel Interceptador, Ataque Aereo ou Vigilancia Aerea.";
            return result;
        }

        // A emergência é uma consulta barata de estado. Não execute a análise
        // terrestre de objetivo nem construa uma onda operacional só para
        // saber se a aeronave precisa de reparo.
        QueroCaronaResult repairProbe =
            QueroCaronaService.EvaluateEmergencyOnly(
            new QueroCaronaRequest
            {
                unit = aircraft,
                map = request.map,
                terrainDatabase = request.terrainDatabase,
                context = QueroCaronaContext.RogueOuRebelde,
                operationalTurns = Mathf.Max(1, request.operationalTurns),
                emulateUnderRepairFromUnitData =
                    request.emulateUnderRepairFromUnitData
            });
        result.isEmergency = repairProbe != null && repairProbe.isEmergency;
        result.repairEvaluation = repairProbe != null
            ? repairProbe.repairEvaluation
            : string.Empty;

        MelhorPousoResult landing =
            CanReuseLandingSnapshot(
                request, aircraft, request.landingSnapshot)
                ? request.landingSnapshot
                : null;
        if (landing != null)
        {
            AIDecisionPerf.AddCount(
                "QueroCaronaAereaLandingSnapshotHits");
        }
        else
        {
            landing = MelhorPousoService.Evaluate(
                new MelhorPousoRequest
                {
                    aircraft = aircraft,
                    map = request.map,
                    terrainDatabase = request.terrainDatabase,
                    tacticalBudget = Mathf.Max(
                        0, aircraft.RemainingMovementPoints),
                    operationalTurns = Mathf.Max(
                        1, request.operationalTurns)
                });
            AIDecisionPerf.AddCount(
                "QueroCaronaAereaLandingSnapshotBuilds");
        }
        result.tacticalBudget = landing.tacticalBudget;
        result.operationalBudget = landing.operationalBudget;
        bool hasSurfaceRecovery = false;
        for (int i = 0; i < landing.options.Count; i++)
        {
            MelhorPousoOption option = landing.options[i];
            if (option == null)
                continue;
            if (option.IsPlatform)
            {
                result.platforms.Add(option);
            }
            else
            {
                hasSurfaceRecovery = true;
            }
        }
        result.platformCount = result.platforms.Count;
        result.isOnlyCompatibleRecovery =
            result.platformCount > 0 && !hasSurfaceRecovery;
        if (result.platformCount == 0)
        {
            result.reason = result.isEmergency
                ? "Emergencia aerea detectada, mas nao ha plataforma compativel em Tactical ou Operational."
                : "Nenhuma plataforma compativel em Tactical ou Operational.";
            return result;
        }

        if (result.isEmergency)
        {
            result.bestPlatform = result.platforms[0];
            result.wantsRide = true;
            result.reason =
                "Emergencia de reparo: aceita plataforma compativel para recuperacao.";
            return result;
        }

        if (!request.hasMissionFocus)
        {
            result.bestPlatform = result.platforms[0];
            result.reason =
                "Ha plataforma compativel, mas sem foco de missao a politica nao pede rebasing. " +
                "Informe um hex de missao para comparar o ganho de posicionamento.";
            return result;
        }

        Vector3Int origin = aircraft.CurrentCellPosition;
        origin.z = 0;
        Vector3Int focus = request.missionFocus;
        focus.z = 0;
        float currentDistance = SectorManager.HexDistance(origin, focus);
        MelhorPousoOption best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < result.platforms.Count; i++)
        {
            MelhorPousoOption option = result.platforms[i];
            float distance = SectorManager.HexDistance(option.cell, focus);
            if (best == null || distance < bestDistance
                || (Mathf.Approximately(distance, bestDistance)
                    && option.tier < best.tier))
            {
                best = option;
                bestDistance = distance;
            }
        }

        result.bestPlatform = best;
        result.currentMissionDistance = currentDistance;
        result.platformMissionDistance = bestDistance;
        result.missionDistanceGain =
            best != null ? currentDistance - bestDistance : 0f;
        float minimumGain =
            Mathf.Max(0.1f, request.minimumMissionDistanceGain);
        bool significantMissionGain =
            best != null
            && result.missionDistanceGain >= minimumGain;
        bool preservesMissionForRecovery =
            best != null
            && bestDistance <= currentDistance
                + Mathf.Max(
                    0f,
                    request.maximumMissionRegressionForRecovery);
        bool necessaryRecovery =
            request.acceptPlatformWhenOnlyRecovery
            && result.isOnlyCompatibleRecovery
            && preservesMissionForRecovery;
        result.wantsRide =
            significantMissionGain || necessaryRecovery;
        if (significantMissionGain)
        {
            result.reason =
                $"A plataforma aproxima a missao de {currentDistance:0.#} " +
                $"para {bestDistance:0.#} hex(es), ganho " +
                $"{result.missionDistanceGain:0.#} >= " +
                $"{minimumGain:0.#}: aceita rebasing.";
        }
        else if (necessaryRecovery)
        {
            result.reason =
                "Plataforma e a unica recuperacao compativel e preserva " +
                $"a missao ({currentDistance:0.#} -> " +
                $"{bestDistance:0.#}): aceita rebasing.";
        }
        else
        {
            result.reason =
                $"Plataforma sem ganho operacional suficiente " +
                $"({currentDistance:0.#} -> {bestDistance:0.#}, ganho " +
                $"{result.missionDistanceGain:0.#} < " +
                $"{minimumGain:0.#}): permanece em voo/base atual.";
        }
        return result;
    }

    private static bool CanReuseLandingSnapshot(
        QueroCaronaAereaRequest request,
        UnitManager aircraft,
        MelhorPousoResult landing)
    {
        if (request == null
            || aircraft == null
            || landing == null
            || !ReferenceEquals(landing.aircraft, aircraft)
            || !ReferenceEquals(landing.map, request.map)
            || !ReferenceEquals(
                landing.terrainDatabase,
                request.terrainDatabase)
            || landing.operationalTurns
                != Mathf.Max(1, request.operationalTurns)
            || landing.autonomyRemaining
                != Mathf.Max(0, aircraft.CurrentFuel)
            || landing.tacticalBudget
                != Mathf.Min(
                    Mathf.Max(
                        0, aircraft.RemainingMovementPoints),
                    Mathf.Max(0, aircraft.CurrentFuel)))
        {
            return false;
        }

        Vector3Int liveOrigin = aircraft.CurrentCellPosition;
        liveOrigin.z = 0;
        if (landing.origin != liveOrigin)
            return false;

        if (!Application.isPlaying)
            return true;
        if (landing.confirmedOccupancyRevision < 0
            || !ConfirmedOccupancyIndex.TryGetFor(
                request.map,
                out ConfirmedOccupancyIndex occupancy)
            || occupancy == null
            || !occupancy.CanServeLiveQueries)
        {
            return false;
        }
        return landing.confirmedOccupancyRevision
            == occupancy.ConfirmedRevision;
    }
}
