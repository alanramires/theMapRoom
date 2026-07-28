using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Pedido de plataforma para uma aeronave de combate. Esta consulta nao escolhe
/// uma acao: a recuperacao e o embarque continuam sendo materializados pelos
/// sensores e pelo controlador da IA.
/// </summary>
public sealed class QueroCaronaAereaRequest
{
    public UnitManager aircraft;
    public Tilemap map;
    public TerrainDatabase terrainDatabase;
    public int operationalTurns = 2;
    public bool emulateUnderRepairFromUnitData;

    // Um foco e opcional porque patrulha/interceptacao ainda nao tem um plano
    // de setor proprio. Quando informado, ele permite testar se uma plataforma
    // realmente melhora o posicionamento da missao em vez de ser apenas carona.
    public bool hasMissionFocus;
    public Vector3Int missionFocus;
}

public sealed class QueroCaronaAereaResult
{
    public bool isAirCombatRole;
    public bool wantsRide;
    public bool isEmergency;
    public string reason;
    public string repairEvaluation;
    public int tacticalBudget;
    public int operationalBudget;
    public int platformCount;
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

        result.isAirCombatRole = data.domain == Domain.Air
            && data.roles != null
            && (data.roles.Contains(UnitRole.Interceptador)
                || data.roles.Contains(UnitRole.AtaqueAereo));
        if (!result.isAirCombatRole)
        {
            result.reason =
                "A ferramenta atende apenas aeronaves com papel Interceptador ou Ataque Aereo.";
            return result;
        }

        // Reusa o diagnostico ja existente apenas para o gatilho de emergencia;
        // a decisao normal de predios do QueroCarona terrestre nao participa.
        QueroCaronaResult repairProbe = QueroCaronaService.Evaluate(
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

        MelhorPousoResult landing = MelhorPousoService.Evaluate(
            new MelhorPousoRequest
            {
                aircraft = aircraft,
                map = request.map,
                terrainDatabase = request.terrainDatabase,
                tacticalBudget = Mathf.Max(0, aircraft.RemainingMovementPoints),
                operationalTurns = Mathf.Max(1, request.operationalTurns)
            });
        result.tacticalBudget = landing.tacticalBudget;
        result.operationalBudget = landing.operationalBudget;
        for (int i = 0; i < landing.options.Count; i++)
        {
            MelhorPousoOption option = landing.options[i];
            if (option != null && option.IsPlatform)
                result.platforms.Add(option);
        }
        result.platformCount = result.platforms.Count;
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
        result.wantsRide = best != null && bestDistance < currentDistance;
        result.reason = result.wantsRide
            ? $"A plataforma aproxima a missao de {currentDistance:0} para {bestDistance:0} hex(es): aceita rebasing."
            : $"A plataforma nao melhora a distancia da missao ({currentDistance:0} -> {bestDistance:0}): permanece em voo/base atual.";
        return result;
    }
}
