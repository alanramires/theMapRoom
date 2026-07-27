using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum QueroCaronaContext
{
    ComPlano,
    RogueOuRebelde
}

public enum QueroCaronaReach
{
    None,
    Tactical,
    Operational,
    BeyondOperational
}

public sealed class QueroCaronaRequest
{
    public UnitManager unit;
    public Tilemap map;
    public TerrainDatabase terrainDatabase;
    public QueroCaronaContext context;
    public ConstructionSector plannedSector;
    public int operationalTurns = 2;
    public bool emulateUnderRepairFromUnitData;
    public Action<string> diagnosticLog;
}

public sealed class QueroCaronaResult
{
    public bool wantsRide;
    public bool isEstimate = true;
    public bool isEmergency;
    public bool isUnderRepairRuntime;
    public bool isUnderRepairEmulated;
    public string repairEvaluation;
    public bool isInfantry;
    public QueroCaronaReach reach;
    public Vector3Int evaluatedTarget;
    public ConstructionManager evaluatedConstruction;
    public int tacticalBudget;
    public int operationalBudget;
    public int routeCost = int.MaxValue;
    public int rideNeedScore;
    public string reason;
}

/// <summary>
/// Contrapeso puro do Melhor Embarque. Estima se a unidade ainda precisa de
/// transporte depois de verificar se consegue cumprir seu objetivo sozinha
/// dentro dos envelopes Tactical e Operational. Nao reserva transporte, nao
/// move unidades e nao substitui prioridades operacionais do papel da unidade.
/// </summary>
public static class QueroCaronaService
{
    public static QueroCaronaResult Evaluate(
        QueroCaronaRequest request)
    {
        var result = new QueroCaronaResult
        {
            wantsRide = false,
            reason = "Contexto incompleto."
        };
        if (request?.unit == null
            || request.map == null
            || request.terrainDatabase == null
            || !request.unit.TryGetUnitData(out UnitData data)
            || data == null)
            return result;

        result.isInfantry =
            data.unitClass == GameUnitClass.Infantry;
        result.tacticalBudget = Mathf.Max(
            0, request.unit.RemainingMovementPoints);
        if (result.tacticalBudget <= 0)
            result.tacticalBudget =
                Mathf.Max(0, request.unit.MaxMovementPoints);
        result.operationalBudget =
            result.tacticalBudget
            * Mathf.Max(1, request.operationalTurns);

        Vector3Int origin = request.unit.CurrentCellPosition;
        origin.z = 0;

        string repairEvaluation = "Emulação desativada.";
        bool emulatedUnderRepair = request.emulateUnderRepairFromUnitData
            && EvaluateRepairTriggers(
                request.unit, data, out repairEvaluation);
        result.isUnderRepairRuntime = request.unit.IsUnderRepair;
        result.isUnderRepairEmulated =
            !result.isUnderRepairRuntime && emulatedUnderRepair;
        result.repairEvaluation = repairEvaluation;

        // UnderRepair representa uma necessidade operacional anterior ao
        // objetivo normal da unidade. O passageiro pede resgate mesmo que um
        // prédio ou representante esteja próximo; compatibilidade, fornecedor
        // e prioridade final continuam sendo decididos pelos outros serviços.
        if (result.isUnderRepairRuntime || result.isUnderRepairEmulated)
        {
            result.wantsRide = true;
            result.isEmergency = true;
            result.reach = QueroCaronaReach.None;
            result.evaluatedTarget = origin;
            result.routeCost = 0;
            result.rideNeedScore = 2000;
            result.reason =
                $"{ResolveUnitKind(result)} " +
                (result.isUnderRepairRuntime
                    ? "IsUnderRepair runtime"
                    : "IsUnderRepair simulado por AI Behavior") +
                ": necessidade " +
                "emergencial aceita carona antes da avaliação de objetivo. " +
                "O resultado é um pedido, não uma ordem de transporte.";
            request.diagnosticLog?.Invoke(result.reason);
            return result;
        }

        Dictionary<Vector3Int, int> reach =
            UnitMovementPathRules.CalculateMovementCostMap(
                request.map,
                request.unit,
                origin,
                Mathf.Max(0, result.operationalBudget),
                request.terrainDatabase);

        if (request.context == QueroCaronaContext.ComPlano)
        {
            if (request.plannedSector == ConstructionSector.None
                || !SectorManager.TryGetSectorInfo(
                    request.plannedSector,
                    out SectorManager.SectorInfo info)
                || info == null)
            {
                result.wantsRide = true;
                result.reach = QueroCaronaReach.BeyondOperational;
                result.rideNeedScore = 1000;
                result.reason =
                    "Plano sem representante válido no SectorManager; " +
                    "estimativa aceita carona.";
                return result;
            }

            Vector3Int representative = info.RepresentativeCell;
            representative.z = 0;
            if (TryFindBestAvailablePlannedTarget(
                    request,
                    reach,
                    info,
                    representative,
                    out Vector3Int plannedTarget,
                    out ConstructionManager plannedConstruction,
                    out int plannedCost))
            {
                result.evaluatedTarget = plannedTarget;
                result.evaluatedConstruction = plannedConstruction;
                result.routeCost = plannedCost;
                SetReachAndDecision(
                    result,
                    plannedCost,
                    plannedTarget == representative
                        ? $"representante de {request.plannedSector}"
                        : $"alternativa livre no setor " +
                          $"{request.plannedSector} {plannedTarget}");
            }
            else
            {
                result.wantsRide = true;
                result.reach =
                    QueroCaronaReach.BeyondOperational;
                result.rideNeedScore = 1000;
                result.reason =
                    $"{ResolveUnitKind(result)} sem destino livre " +
                    $"alcançável no setor {request.plannedSector} " +
                    "em Tactical ou Operational: aceita carona.";
            }
            request.diagnosticLog?.Invoke(result.reason);
            return result;
        }

        ConstructionManager nearest = null;
        int nearestCost = int.MaxValue;
        foreach (ConstructionManager construction
                 in ConstructionManager.AllActive)
        {
            if (construction == null
                || !construction.IsCapturable
                || construction.TeamId == request.unit.TeamId
                || IsClaimedByAlliedUnit(
                    request, construction.CurrentCellPosition))
                continue;
            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            if (!reach.TryGetValue(cell, out int cost)
                || cost >= nearestCost)
                continue;
            nearest = construction;
            nearestCost = cost;
        }

        if (nearest != null)
        {
            Vector3Int target = nearest.CurrentCellPosition;
            target.z = 0;
            result.evaluatedTarget = target;
            result.evaluatedConstruction = nearest;
            result.routeCost = nearestCost;
            SetReachAndDecision(
                result, nearestCost,
                $"prédio capturável próximo {target}");
        }
        else
        {
            result.wantsRide = true;
            result.reach = QueroCaronaReach.BeyondOperational;
            result.rideNeedScore = 1000;
            result.reason =
                $"{ResolveUnitKind(result)} rogue/rebelde sem prédio " +
                "capturável alcançável em Tactical ou Operational: " +
                "aceita carona.";
        }

        request.diagnosticLog?.Invoke(result.reason);
        return result;
    }

    private static bool EvaluateRepairTriggers(
        UnitManager unit,
        UnitData data,
        out string details)
    {
        var triggered = new List<string>();
        var evaluated = new List<string>();

        bool hpTriggered = data.repairTriggerHpBelow > 0
            && unit.CurrentHP <= data.repairTriggerHpBelow;
        evaluated.Add(
            data.repairTriggerHpBelow > 0
                ? $"HP {unit.CurrentHP} <= {data.repairTriggerHpBelow}: " +
                  (hpTriggered ? "ATIVO" : "não")
                : "HP: desativado");
        if (hpTriggered)
            triggered.Add("HP");

        int maxFuel = Mathf.Max(1, unit.GetMaxFuel());
        float fuelPct = unit.CurrentFuel * 100f / maxFuel;
        bool autonomyTriggered =
            data.repairTriggerAutonomyPct > 0
            && fuelPct <= data.repairTriggerAutonomyPct;
        evaluated.Add(
            data.repairTriggerAutonomyPct > 0
                ? $"Autonomia {unit.CurrentFuel}/{maxFuel} " +
                  $"({fuelPct:0.#}%) <= " +
                  $"{data.repairTriggerAutonomyPct}%: " +
                  (autonomyTriggered ? "ATIVO" : "não")
                : "Autonomia: desativada");
        if (autonomyTriggered)
            triggered.Add("autonomia");

        bool ammoTriggered = false;
        if (data.repairTriggerAmmoEnabled)
        {
            IReadOnlyList<UnitEmbarkedWeapon> weapons =
                unit.GetEmbarkedWeapons();
            int trackedWeapons = 0;
            if (weapons != null)
            {
                for (int i = 0; i < weapons.Count; i++)
                {
                    UnitEmbarkedWeapon runtimeWeapon = weapons[i];
                    int baseAmmo =
                        data.embarkedWeapons != null
                        && i < data.embarkedWeapons.Count
                        && data.embarkedWeapons[i] != null
                            ? data.embarkedWeapons[i].squadAmmunition
                            : 0;
                    if (runtimeWeapon == null || baseAmmo <= 0)
                        continue;

                    trackedWeapons++;
                    float ammoPct =
                        runtimeWeapon.squadAmmunition * 100f / baseAmmo;
                    if (ammoPct <= data.repairTriggerAmmoPct)
                    {
                        ammoTriggered = true;
                        break;
                    }
                }
            }

            evaluated.Add(
                trackedWeapons > 0
                    ? $"Munição <= {data.repairTriggerAmmoPct}%: " +
                      (ammoTriggered ? "ATIVO" : "não")
                    : "Munição: habilitada, sem arma runtime rastreável");
        }
        else
        {
            evaluated.Add("Munição: desativada");
        }
        if (ammoTriggered)
            triggered.Add("munição");

        details =
            $"Critérios AI Behavior: {string.Join(" | ", evaluated)}. " +
            (triggered.Count > 0
                ? $"Disparou: {string.Join(", ", triggered)}."
                : "Nenhum gatilho disparou.");
        return triggered.Count > 0;
    }

    private static bool TryFindBestAvailablePlannedTarget(
        QueroCaronaRequest request,
        Dictionary<Vector3Int, int> reach,
        SectorManager.SectorInfo info,
        Vector3Int representative,
        out Vector3Int target,
        out ConstructionManager construction,
        out int routeCost)
    {
        target = Vector3Int.zero;
        construction = null;
        routeCost = int.MaxValue;

        if (!IsClaimedByAlliedUnit(request, representative)
            && reach != null
            && reach.TryGetValue(
                representative, out int representativeCost))
        {
            target = representative;
            construction = info.RepresentativeConstruction;
            routeCost = representativeCost;
        }

        foreach (ConstructionManager candidate
                 in ConstructionManager.AllActive)
        {
            if (candidate == null
                || candidate.Sector != request.plannedSector
                || !candidate.IsCapturable
                || candidate.TeamId == request.unit.TeamId
                || IsClaimedByAlliedUnit(
                    request, candidate.CurrentCellPosition))
                continue;
            Vector3Int cell = candidate.CurrentCellPosition;
            cell.z = 0;
            if (reach == null
                || !reach.TryGetValue(cell, out int cost)
                || cost >= routeCost)
                continue;
            target = cell;
            construction = candidate;
            routeCost = cost;
        }

        return routeCost < int.MaxValue;
    }

    private static bool IsClaimedByAlliedUnit(
        QueroCaronaRequest request,
        Vector3Int cell)
    {
        if (request?.unit == null || request.map == null)
            return false;
        cell.z = 0;
        List<UnitManager> occupants =
            UnitOccupancyRules.GetUnitsAtCell(
                request.map, cell, request.unit);
        for (int i = 0; i < occupants.Count; i++)
        {
            UnitManager occupant = occupants[i];
            if (occupant != null
                && !occupant.IsDead
                && !occupant.IsEmbarked
                && PlayerSlotRelations.AreAllies(
                    request.unit, occupant))
                return true;
        }
        return false;
    }

    private static void SetReachAndDecision(
        QueroCaronaResult result,
        int routeCost,
        string targetLabel)
    {
        result.wantsRide = false;
        result.rideNeedScore = 0;
        if (routeCost <= result.tacticalBudget)
        {
            result.reach = QueroCaronaReach.Tactical;
            result.reason =
                $"{ResolveUnitKind(result)} alcança {targetLabel} " +
                $"no Tactical: custo={routeCost}<=" +
                $"{result.tacticalBudget}. Recusa carona.";
            return;
        }

        result.reach = QueroCaronaReach.Operational;
        result.reason =
            $"{ResolveUnitKind(result)} alcança {targetLabel} " +
            $"no Operational: custo={routeCost}<=" +
            $"{result.operationalBudget}. Recusa carona.";
    }

    private static string ResolveUnitKind(
        QueroCaronaResult result) =>
        result.isInfantry ? "Infantaria" : "Unidade";
}
