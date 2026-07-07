using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    [System.NonSerialized] private bool difficultyModesValidated;
    [System.NonSerialized] private bool previouslyValidatedHardMode;
    [System.NonSerialized] private bool previouslyValidatedEasyMode;

    private void OnValidate()
    {
        if (hardMode && easyMode)
        {
            bool hardWasJustEnabled = difficultyModesValidated && !previouslyValidatedHardMode;
            bool easyWasJustEnabled = difficultyModesValidated && !previouslyValidatedEasyMode;

            if (easyWasJustEnabled && !hardWasJustEnabled)
                hardMode = false;
            else
                easyMode = false;
        }

        previouslyValidatedHardMode = hardMode;
        previouslyValidatedEasyMode = easyMode;
        difficultyModesValidated = true;
    }

    // Unidade -> prédio cuja primeira etapa ela abriu. Enquanto esse prédio continuar
    // parcial, a unidade permanece liberada para avançar no mesmo eixo.
    private readonly Dictionary<int, int> hardBlitzkriegHandoffs = new Dictionary<int, int>();

    private void RememberHardBlitzkriegCapture(UnitManager unit, TeamId team, Vector3Int cell)
    {
        if (!hardMode || unit == null || team == TeamId.Neutral)
            return;

        cell.z = 0;
        ConstructionManager building = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (building == null || !building.IsCapturable || building.IsRallyPoint || building.IsPlayerHeadQuarter)
            return;

        hardBlitzkriegHandoffs[unit.InstanceId] = building.InstanceId;
    }

    private bool TryDecideHardBlitzkriegHandoff(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        out PlayerAction action)
    {
        action = null;
        if (!hardMode || unit == null || snapshot == null)
            return false;

        bool plannedHandoff = TryGetPlannedHardHandoff(plan, unit, out SectorObjective handoffObjective);
        ConstructionManager origin;
        ConstructionSector advanceSector;

        if (plannedHandoff)
        {
            origin = FindCapturableInSector(handoffObjective.Sector, snapshot.AITeam);
            advanceSector = ComputeBlitzkriegForwardSector(handoffObjective.Sector, snapshot.AITeam);
        }
        else
        {
            if (!hardBlitzkriegHandoffs.TryGetValue(unit.InstanceId, out int buildingId))
                return false;

            origin = FindBlitzkriegConstruction(buildingId);
            if (origin == null || origin.CurrentCapturePoints <= 0
                || origin.CurrentCapturePoints >= origin.CapturePointsMax)
            {
                hardBlitzkriegHandoffs.Remove(unit.InstanceId);
                return false;
            }

            SectorObjective assigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;
            advanceSector = ResolveBlitzkriegAdvanceSector(origin.Sector, assigned, snapshot.AITeam);
        }

        ConstructionSector originSector = origin != null ? origin.Sector : handoffObjective.Sector;
        if (advanceSector == ConstructionSector.None || advanceSector == originSector)
            return false;

        Vector3Int from = unit.CurrentCellPosition;
        from.z = 0;
        ConstructionManager target = FindCapturableInSector(advanceSector, snapshot.AITeam, from);
        Vector3Int targetCell;
        if (target != null)
        {
            targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
        }
        else if (SectorManager.TryGetSectorInfo(advanceSector, out SectorManager.SectorInfo targetInfo)
            && targetInfo != null)
        {
            targetCell = targetInfo.RepresentativeCell;
            targetCell.z = 0;
        }
        else
            return false;
        Dictionary<Vector3Int, List<Vector3Int>> paths = UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        if (paths == null || paths.Count == 0)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        if (paths.ContainsKey(targetCell) && !occupied.Contains(targetCell))
        {
            Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} hard handoff {originSector} -> captura {advanceSector}");
            action = SimulateCaptureSensor(unit, targetCell, out _)
                ? BuildCaptureBatch(unit, snapshot.AITeam, from, targetCell, paths)
                : BuildMoveBatch(unit, snapshot.AITeam, from, targetCell, paths);
            return true;
        }

        Vector3Int best = from;
        float bestDistance = SectorManager.HexDistance(from, targetCell);
        int bestSteps = -1;
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> entry in paths)
        {
            Vector3Int candidate = entry.Key;
            if (occupied.Contains(candidate)) continue;
            float distance = SectorManager.HexDistance(candidate, targetCell);
            int steps = entry.Value != null ? entry.Value.Count : 0;
            if (distance < bestDistance || (Mathf.Approximately(distance, bestDistance) && steps > bestSteps))
            {
                best = candidate;
                bestDistance = distance;
                bestSteps = steps;
            }
        }

        if (best == from)
            return false;

        Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} hard handoff {originSector} -> {advanceSector} via {best}");
        action = SimulateCaptureSensor(unit, best, out _)
            ? BuildCaptureBatch(unit, snapshot.AITeam, from, best, paths)
            : BuildMoveBatch(unit, snapshot.AITeam, from, best, paths);
        return true;
    }

    private static bool TryGetPlannedHardHandoff(
        TeamObjectivePlan plan,
        UnitManager unit,
        out SectorObjective objective)
    {
        objective = null;
        if (plan == null || unit == null || plan.HandoffVacaterIds == null
            || !plan.HandoffVacaterIds.Contains(unit.InstanceId))
            return false;

        foreach (SectorObjective candidate in plan.Objectives)
        {
            if (candidate != null
                && candidate.HandoffEligible
                && candidate.PreferredHandoffFromUnitId == unit.InstanceId)
            {
                objective = candidate;
                return true;
            }
        }

        // Saves retomados no meio do turno podem preservar HandoffVacaterIds sem manter
        // PreferredHandoffFromUnitId sincronizado. O marcador de vacate é autoritativo:
        // reconstruímos o objetivo pelo prédio parcial ocupado pela unidade.
        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        foreach (ConstructionManager building in ConstructionManager.AllActive)
        {
            if (building == null || !building.IsCapturable) continue;
            Vector3Int buildingCell = building.CurrentCellPosition;
            buildingCell.z = 0;
            if (buildingCell != cell) continue;
            objective = plan.GetObjectiveForSector(building.Sector);
            if (objective != null)
                return true;
        }
        return false;
    }

    private ConstructionSector ResolveBlitzkriegAdvanceSector(
        ConstructionSector originSector,
        SectorObjective assigned,
        TeamId team)
    {
        if (assigned != null && assigned.Sector != ConstructionSector.None && assigned.Sector != originSector)
            return assigned.Sector;

        InvasionAxisMap axisMap = InvasionAxisMap.Build(team, boardTilemap);
        int axisIndex = axisMap.GetEixo(originSector);
        return axisIndex > 0 ? axisMap.GetTransportTargetSector(axisIndex) : ConstructionSector.None;
    }

    private ConstructionSector ComputeBlitzkriegForwardSector(ConstructionSector sector, TeamId team)
    {
        InvasionAxisMap axisMap = InvasionAxisMap.Build(team, boardTilemap);
        int axisIndex = axisMap.GetEixo(sector);
        if (axisIndex > 0)
        {
            ConstructionSector axisForward = axisMap.GetTransportTargetSector(axisIndex);
            if (axisForward != ConstructionSector.None && axisForward != sector)
                return axisForward;
        }

        return ComputeForwardNeighborSector(sector, team);
    }

    private static ConstructionManager FindBlitzkriegConstruction(int instanceId)
    {
        foreach (ConstructionManager building in ConstructionManager.AllActive)
            if (building != null && building.InstanceId == instanceId)
                return building;
        return null;
    }
}
