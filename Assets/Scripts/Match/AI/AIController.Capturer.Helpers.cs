using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class AIController
{

    // -------------------------------------------------------------------------
    // Helpers de combate
    // -------------------------------------------------------------------------

    // 3 = defensor do prédio objetivo  2 = inimigo em qualquer construção  1 = terreno aberto
    private float AttackTargetPriority(Vector3Int targetUnitCell, Vector3Int captureTargetCell)
    {
        if (targetUnitCell == captureTargetCell) return 3f;
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, targetUnitCell);
        return bldg != null ? 2f : 1f;
    }

    // Perseguidor: prioriza inimigos mais próximos ao objetivo a capturar
    private float AttackTargetPriorityPursuer(Vector3Int targetUnitCell, Vector3Int captureTargetCell)
    {
        if (targetUnitCell == captureTargetCell) return 3f;
        float dist = SectorManager.HexDistance(targetUnitCell, captureTargetCell);
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, targetUnitCell);
        float bldgBonus = bldg != null ? 0.5f : 0f;
        return 2f - dist * 0.1f + bldgBonus;
    }

    private bool HasAttackTargetAtCurrentPos(UnitManager unit)
    {
        var targets = new List<PodeMirarTargetOption>();
        return PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
            SensorMovementMode.MoveuParado, targets) && targets.Count > 0;
    }

    // Escolhe o melhor alvo para o rogue: capturadores ativos em prédios têm prioridade máxima,
    // desempate por HP mais baixo (mais fácil de eliminar).
    private UnitManager PickBestRogueTarget(List<PodeMirarTargetOption> options, TeamId aiTeam)
    {
        UnitManager best = null;
        float bestPriority = float.MinValue;
        foreach (PodeMirarTargetOption opt in options)
        {
            if (opt?.targetUnit == null || opt.targetUnit.TeamId == aiTeam) continue;
            float priority = 10f - opt.targetUnit.CurrentHP;

            // Inimigo em prédio capturável → ameaça direta ao objetivo, prioridade máxima
            Vector3Int ec = opt.targetUnit.CurrentCellPosition; ec.z = 0;
            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, ec);
            if (bldg != null && bldg.IsCapturable
                && !(bldg.TeamId == aiTeam && bldg.CurrentCapturePoints >= bldg.CapturePointsMax))
                priority += 1000f;

            if (priority > bestPriority) { bestPriority = priority; best = opt.targetUnit; }
        }
        return best;
    }

    private static bool HasEnemyInEngageRadius(UnitManager unit, Vector3Int fromCell, TeamId aiTeam)
    {
        int radius = Mathf.Max(0, unit.RemainingMovementPoints) + 1;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (Vector3Int.Distance(fromCell, ec) <= radius) return true;
        }
        return false;
    }

    // Inimigo visível dentro do raio de movimento+1 que está mais perto do alvo do que a unidade
    // (bloqueador de rota) → deve-se lutar, não rotear pelo QG.
    private static bool HasEnemyBlockingPath(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell, TeamId aiTeam)
    {
        float unitDist   = Vector3Int.Distance(fromCell, targetCell);
        int engageRadius = Mathf.Max(0, unit.RemainingMovementPoints) + 1;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (Vector3Int.Distance(fromCell, ec) > engageRadius) continue;
            if (Vector3Int.Distance(ec, targetCell) < unitDist) return true;
        }
        return false;
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

    // -------------------------------------------------------------------------
    // Helpers de captura
    // -------------------------------------------------------------------------

    // Captura oportunista: primeiro prédio capturável alcançável, excluindo excludeCell.
    // excludeCurrentCell=true: ignora fromCell (usado após handoff — não re-capturar o setor abandonado).
    private bool TryFindOpportunisticCapture(
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int excludeCell,
        out Vector3Int captureCell,
        bool excludeCurrentCell = false)
    {
        captureCell = Vector3Int.zero;
        Vector3Int currentCell = unit.CurrentCellPosition; currentCell.z = 0;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (occupied.Contains(cell) || cell == excludeCell) continue;
            if (excludeCurrentCell && cell == currentCell) continue;
            if (!SimulateCaptureSensor(unit, cell, out _)) continue;
            captureCell = cell;
            return true;
        }
        return false;
    }

    // FoW passinho: entre os hexes adjacentes ao alvo alcançáveis, prefere maior DPQ (prioritizeDpqAtBattle=true) ou maior EV.
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
        if (assigned == null || assigned.Sector != captureTarget.Sector) return false;

        ConstructionManager assignedTarget = FindCapturableInSector(assigned.Sector, aiTeam, unit.CurrentCellPosition);
        return assignedTarget == captureTarget;
    }

    private bool TryFindBestLoSCell(
        UnitManager unit,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int targetCell,
        out Vector3Int bestCell)
    {
        bool preferDpq = unit.TryGetUnitData(out UnitData ud) && ud.prioritizeDpqAtBattle;

        bestCell = Vector3Int.zero;
        var neighbors = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, targetCell, neighbors);

        bool found = false;
        float bestScore = float.MinValue;
        foreach (Vector3Int n in neighbors)
        {
            Vector3Int nc = n; nc.z = 0;
            if (!paths.ContainsKey(nc) || occupied.Contains(nc)) continue;
            float score = preferDpq ? GetTerrainDpqPontos(nc) : GetTerrainEv(nc);
            if (!found || score > bestScore) { bestScore = score; bestCell = nc; found = true; }
        }
        return found;
    }

    // Fase 2: ameaça local por inimigos visíveis dentro do raio ThreatRadius.
    private float CalculateThreatLevel(Vector3Int cell, TeamId aiTeam)
    {
        float threat = 0f;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            float dist = Vector3Int.Distance(cell, ec);
            if (dist <= ThreatRadius)
                threat += (ThreatRadius - dist + 1f) * 10f;
        }
        return threat;
    }

    // Fase 3: verifica se a unidade consegue atacar o alvo a partir de toCell.
    private bool CanAttackTargetFrom(Vector3Int fromCell, Vector3Int toCell,
        UnitManager unit, UnitManager target)
    {
        SensorMovementMode mode = toCell != fromCell
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;

        var targets = new List<PodeMirarTargetOption>();
        bool hasAny = PodeMirarSensor.CollectTargets(
            unit, boardTilemap, terrainDatabase, mode, targets, fromCell: toCell);

        if (!hasAny) return false;
        foreach (PodeMirarTargetOption opt in targets)
            if (opt?.targetUnit == target) return true;
        return false;
    }

    private float GetTerrainEv(Vector3Int cell)
    {
        if (boardTilemap == null || terrainDatabase == null) return 0f;
        cell.z = 0;
        TileBase tile = boardTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData data) && data != null)
            return data.ev;
        return 0f;
    }

    private float GetTerrainDpqPontos(Vector3Int cell)
    {
        if (boardTilemap == null || terrainDatabase == null) return 0f;
        cell.z = 0;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (construction != null
            && construction.TryResolveConstructionData(out ConstructionData constructionData)
            && constructionData != null
            && constructionData.dpqData != null)
        {
            return constructionData.dpqData.Pontos;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardTilemap, cell);
        if (structure != null && structure.dpqData != null)
            return structure.dpqData.Pontos;

        TileBase tile = boardTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData data) && data?.dpqData != null)
            return data.dpqData.Pontos;

        GridLayout grid = boardTilemap.layoutGrid;
        if (grid != null)
        {
            Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
            for (int i = 0; i < maps.Length; i++)
            {
                Tilemap map = maps[i];
                if (map == null || map == boardTilemap)
                    continue;

                TileBase other = map.GetTile(cell);
                if (other != null && terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData otherData) && otherData?.dpqData != null)
                    return otherData.dpqData.Pontos;
            }
        }

        return 0f;
    }

    private static bool IsBetterAttackCandidate(
        bool preferDpqAtBattle,
        float targetPriority,
        float attackDpq,
        float score,
        float sectorTie,
        float hqTie,
        float bestTargetPriority,
        float bestAttackDpq,
        float bestScore,
        float bestSectorTie,
        float bestHqTie)
    {
        const float epsilon = 0.001f;

        if (preferDpqAtBattle)
        {
            if (targetPriority > bestTargetPriority + epsilon) return true;
            if (Mathf.Abs(targetPriority - bestTargetPriority) > epsilon) return false;

            if (attackDpq > bestAttackDpq + epsilon) return true;
            if (Mathf.Abs(attackDpq - bestAttackDpq) > epsilon) return false;
        }

        return IsBetterScore(score, sectorTie, hqTie, bestScore, bestSectorTie, bestHqTie);
    }

    private static bool IsBetterScore(float score, float sectorTie, float hqTie,
                                       float bestScore, float bestSectorTie, float bestHqTie)
    {
        const float epsilon = 0.001f;
        if (score > bestScore + epsilon) return true;
        if (Mathf.Abs(score - bestScore) > epsilon) return false;
        // desempate 1: setor atribuído (menor distância ao alvo do setor)
        if (sectorTie > bestSectorTie + epsilon) return true;
        if (Mathf.Abs(sectorTie - bestSectorTie) > epsilon) return false;
        // desempate 2: HQ inimigo
        return hqTie > bestHqTie + epsilon;
    }

    private static float CalculateEnemyHqDistance(Vector3Int cell, AIWorldSnapshot snapshot, UnitManager unit)
    {
        if (snapshot == null || snapshot.EnemyHQ == null)
            return float.MaxValue;

        Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition;
        hq.z = 0;
        cell.z = 0;

        if (unit != null
            && unit.TryGetUnitData(out UnitData unitData)
            && unitData != null
            && SectorManager.TryGetLandMovementDistance(cell, hq, unitData, out int movementCost))
        {
            return movementCost;
        }

        if (SectorManager.TryGetLandMovementDistance(cell, hq, out int fallbackCost))
            return fallbackCost;

        return SectorManager.HexDistance(cell, hq);
    }

    private static float CalculateEnemyHqTieBreak(float hqDistance)
    {
        return hqDistance < float.MaxValue ? -hqDistance : 0f;
    }

    private static bool IsBetterRogueAdvance(Vector3Int from, Vector3Int target, Vector3Int candidate, float candidateHexDist, Vector3Int currentBest, float bestHexDist)
    {
        const float epsilon = 0.001f;
        if (candidateHexDist < bestHexDist - epsilon)
            return true;
        if (candidateHexDist > bestHexDist + epsilon)
            return false;

        float candidateLine = CalculateLineProgressTieBreak(from, target, candidate);
        float bestLine = CalculateLineProgressTieBreak(from, target, currentBest);
        if (candidateLine > bestLine + epsilon)
            return true;
        if (candidateLine < bestLine - epsilon)
            return false;

        return Vector3Int.Distance(candidate, target) < Vector3Int.Distance(currentBest, target) - epsilon;
    }

    private static float CalculateLineProgressTieBreak(Vector3Int from, Vector3Int target, Vector3Int candidate)
    {
        Vector2 origin = new Vector2(from.x, from.y);
        Vector2 goal = new Vector2(target.x, target.y);
        Vector2 point = new Vector2(candidate.x, candidate.y);
        Vector2 direction = goal - origin;
        float lengthSq = direction.sqrMagnitude;
        if (lengthSq <= 0.001f)
            return 0f;

        Vector2 advanced = point - origin;
        float projection = Vector2.Dot(advanced, direction.normalized);
        float lateral = Mathf.Abs(direction.x * advanced.y - direction.y * advanced.x) / Mathf.Sqrt(lengthSq);
        return projection - lateral * 0.25f;
    }

    private static SectorObjective ResolveAssignedObjective(UnitManager unit, TeamObjectivePlan plan)
    {
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && slot.AssignedUnitId == unit.InstanceId) return obj;
        return null;
    }

    // Replica PodeCapturarSensor em um hex simulado sem mover a unidade.
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

    private static ConstructionManager FindCapturableInSector(ConstructionSector sector, TeamId aiTeam, Vector3Int? unitPos = null)
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
