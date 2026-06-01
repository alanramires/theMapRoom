using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Helpers de captura: sensor, atribuição e seleção de alvos
    // -------------------------------------------------------------------------

    // Ameaça local por inimigos visíveis dentro do raio ThreatRadius.
    private float CalculateThreatLevel(Vector3Int cell, TeamId aiTeam)
    {
        float threat = 0f;
        Vector3Int cellXY = cell; cellXY.z = 0;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            float dist = SectorManager.HexDistance(cellXY, ec);
            if (dist <= ThreatRadius)
                threat += (ThreatRadius - dist + 1f) * 10f;
        }
        return threat;
    }

    // Inimigo visível no hex alvo ou num dos seus adjacentes (ameaça direta ao objetivo).
    private bool HasEnemyNearCell(Vector3Int cell, TeamId aiTeam)
    {
        var neighbors = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, cell, neighbors);
        var nearCells = new HashSet<Vector3Int>(neighbors) { cell };

        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (nearCells.Contains(ec)) return true;
        }
        return false;
    }

    private bool TryFindOpportunisticCapture(
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int excludeCell,
        out Vector3Int captureCell,
        bool excludeCurrentCell = false,
        HashSet<Vector3Int> skippedCaptureCells = null)
    {
        captureCell = Vector3Int.zero;
        Vector3Int currentCell = unit.CurrentCellPosition; currentCell.z = 0;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell != currentCell && occupied.Contains(cell)) continue;
            if (cell == excludeCell) continue;
            if (skippedCaptureCells != null && skippedCaptureCells.Contains(cell)) continue;
            if (excludeCurrentCell && cell == currentCell) continue;
            if (!SimulateCaptureSensor(unit, cell, out _)) continue;
            captureCell = cell;
            return true;
        }
        return false;
    }

    private bool ShouldReserveOpportunisticCaptureForCloserUnit(
        UnitManager opportunist,
        TeamId aiTeam,
        Vector3Int captureCell,
        Dictionary<Vector3Int, List<Vector3Int>> opportunistPaths,
        out UnitManager reservedFor)
    {
        reservedFor = null;

        if (!SimulateCaptureSensor(opportunist, captureCell, out ConstructionManager captureTarget))
            return false;

        int opportunistCost = GetPathStepCount(opportunistPaths, captureCell);
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);

        if (TryFindAssignedCapturerForCaptureTarget(
                opportunist, plan, captureTarget, aiTeam, captureCell, out UnitManager assignedCapturer))
        {
            reservedFor = assignedCapturer;
            return true;
        }

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == opportunist || candidate.TeamId != aiTeam) continue;
            if (candidate.HasActed || candidate.IsDead || candidate.IsEmbarked || candidate.IsUnderRepair) continue;
            if (!SimulateCaptureSensor(candidate, captureCell, out _)) continue;

            Dictionary<Vector3Int, List<Vector3Int>> candidatePaths =
                UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap, candidate, Mathf.Max(0, candidate.RemainingMovementPoints), terrainDatabase);
            if (candidatePaths == null || !candidatePaths.ContainsKey(captureCell)) continue;

            int candidateCost = GetPathStepCount(candidatePaths, captureCell);
            bool candidateOwnsTarget = IsAssignedToCaptureTarget(candidate, plan, captureTarget, aiTeam);

            if (candidateCost < opportunistCost || (candidateOwnsTarget && candidateCost <= opportunistCost))
            {
                reservedFor = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryFindAssignedCapturerForCaptureTarget(
        UnitManager opportunist,
        TeamObjectivePlan plan,
        ConstructionManager captureTarget,
        TeamId aiTeam,
        Vector3Int captureCell,
        out UnitManager assignedCapturer)
    {
        assignedCapturer = null;
        if (plan == null || captureTarget == null) return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj.Status == ObjectiveStatus.Defending) continue;
            if (obj.Sector != captureTarget.Sector) continue;

            ConstructionManager assignedTarget = FindCapturableInSector(obj.Sector, aiTeam);
            if (assignedTarget != captureTarget) continue;

            foreach (SlotNeed slot in obj.Slots)
            {
                if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;

                UnitManager candidate = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (candidate == null || candidate == opportunist) continue;
                if (candidate.HasActed || candidate.IsDead || candidate.IsEmbarked || candidate.IsUnderRepair) continue;
                if (!SimulateCaptureSensor(candidate, captureCell, out _)) continue;

                Dictionary<Vector3Int, List<Vector3Int>> candidatePaths =
                    UnitMovementPathRules.CalcularCaminhosValidos(
                        boardTilemap, candidate, Mathf.Max(0, candidate.RemainingMovementPoints), terrainDatabase);

                if (candidatePaths == null || !candidatePaths.ContainsKey(captureCell)) continue;

                assignedCapturer = candidate;
                return true;
            }
        }

        return false;
    }

    private static int GetPathStepCount(Dictionary<Vector3Int, List<Vector3Int>> paths, Vector3Int cell)
    {
        return paths != null && paths.TryGetValue(cell, out List<Vector3Int> path) && path != null
            ? path.Count
            : int.MaxValue;
    }

    private static bool IsAssignedToCaptureTarget(UnitManager unit, TeamObjectivePlan plan, ConstructionManager captureTarget, TeamId aiTeam)
    {
        if (plan == null || captureTarget == null) return false;
        SectorObjective assigned = ResolveAssignedObjective(unit, plan);
        return assigned != null && assigned.Sector == captureTarget.Sector;
    }

    private static SectorObjective ResolveAssignedObjective(UnitManager unit, TeamObjectivePlan plan)
    {
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Capturador && slot.Filled && slot.AssignedUnitId == unit.InstanceId) return obj;
        return null;
    }

    private bool SimulateCaptureSensor(UnitManager unit, Vector3Int simulatedCell,
        out ConstructionManager targetConstruction)
    {
        targetConstruction = null;
        if (!unit.TryGetUnitData(out UnitData data)) return false;
        if (data.roles == null || !data.roles.Contains(UnitRole.Capturador)) return false;
        if (unit.TeamId == TeamId.Neutral) return false;

        ConstructionManager c = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, simulatedCell);
        if (c == null || !c.IsCapturable || c.CapturePointsMax <= 0) return false;
        if (c.TeamId == unit.TeamId && c.CurrentCapturePoints >= c.CapturePointsMax) return false;

        targetConstruction = c;
        return true;
    }

    public static ConstructionManager FindCapturableInSector(ConstructionSector sector, TeamId aiTeam, Vector3Int? unitPos = null)
    {
        ConstructionManager best = null;
        float bestDist = float.MaxValue;

        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.Sector != sector || !c.IsCapturable) continue;
            if (c.TeamId == aiTeam && c.CurrentCapturePoints >= c.CapturePointsMax) continue;

            if (unitPos == null) return c;

            Vector3Int tc = c.CurrentCellPosition; tc.z = 0;
            float dist = Vector3Int.Distance(unitPos.Value, tc);
            if (dist < bestDist) { bestDist = dist; best = c; }
        }

        return best;
    }

    private static List<UnitManager> GetAvailableCapturers(TeamId aiTeam)
    {
        var list = new List<UnitManager>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId != aiTeam || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
            if (!u.TryGetUnitData(out UnitData data)) continue;
            if (data.roles != null && data.roles.Contains(UnitRole.Capturador))
                list.Add(u);
        }
        return list;
    }
}
