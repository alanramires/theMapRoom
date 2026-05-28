using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Capturer Swap
    // Quando um capturador fraco está sobre o edificio do seu objetivo e outro
    // capturador do MESMO objetivo (HP maior) consegue chegar este turno,
    // o fraco cede o hex e o forte captura no lugar.
    // -------------------------------------------------------------------------

    // Versão rápida (sem pathfinding) usada na ordenação de iniciativa.
    private bool HasSwapIncomingCapturerFast(UnitManager occupant, TeamObjectivePlan plan, TeamId aiTeam)
    {
        return FindSwapIncomingCapturer(occupant, plan, aiTeam, fullPathCheck: false) != null;
    }

    // Versão completa (com pathfinding) usada na decisão de ação.
    private UnitManager FindSwapIncomingCapturer(UnitManager occupant, TeamObjectivePlan plan, TeamId aiTeam)
    {
        return FindSwapIncomingCapturer(occupant, plan, aiTeam, fullPathCheck: true);
    }

    private UnitManager FindSwapIncomingCapturer(UnitManager occupant, TeamObjectivePlan plan, TeamId aiTeam, bool fullPathCheck)
    {
        if (occupant == null || plan == null) return null;
        if (!occupant.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Capturador) return null;

        SectorObjective objective = ResolveAssignedObjective(occupant, plan);
        if (objective == null) return null;

        ConstructionManager capturable = FindCapturableInSector(objective.Sector, aiTeam);
        if (capturable == null) return null;
        Vector3Int capCell = capturable.CurrentCellPosition; capCell.z = 0;
        Vector3Int occCell = occupant.CurrentCellPosition;  occCell.z = 0;
        if (occCell != capCell) return null;

        UnitManager best  = null;
        int         bestHP = occupant.CurrentHP;

        foreach (SlotNeed slot in objective.Slots)
        {
            if (slot.Role != UnitRole.Capturador || !slot.Filled) continue;
            if (slot.AssignedUnitId == occupant.InstanceId) continue;

            UnitManager candidate = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (candidate == null || candidate.HasActed || candidate.IsDead || candidate.IsEmbarked) continue;
            if (candidate.CurrentHP <= bestHP) continue;

            Vector3Int candCell = candidate.CurrentCellPosition; candCell.z = 0;
            float hexDist = SectorManager.HexDistance(candCell, capCell);
            if (hexDist > candidate.RemainingMovementPoints) continue;

            if (fullPathCheck)
            {
                // CalcularCaminhosValidos inclui células ocupadas por aliados (são passáveis),
                // portanto capCell estará no dicionário mesmo com o ocupante ainda lá.
                Dictionary<Vector3Int, List<Vector3Int>> candPaths =
                    UnitMovementPathRules.CalcularCaminhosValidos(
                        boardTilemap, candidate, Mathf.Max(0, candidate.RemainingMovementPoints), terrainDatabase);
                if (candPaths == null || !candPaths.ContainsKey(capCell)) continue;
            }

            best   = candidate;
            bestHP = candidate.CurrentHP;
        }

        return best;
    }

    // Move o ocupante para o melhor hex adjacente livre, cedendo o edificio.
    private PlayerAction DecideSwapVacateAction(UnitManager occupant, UnitManager incoming, AIWorldSnapshot snapshot)
    {
        Vector3Int fromCell = occupant.CurrentCellPosition; fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, occupant, Mathf.Max(0, occupant.RemainingMovementPoints), terrainDatabase);
        if (paths == null || paths.Count == 0) return null;

        HashSet<Vector3Int> occupied = BuildOccupied(occupant);

        // 1ª passagem: hex adjacente livre
        var neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, fromCell, neighbors);

        Vector3Int bestCell = Vector3Int.zero;
        bool found = false;

        foreach (Vector3Int rawCell in neighbors)
        {
            Vector3Int cell = rawCell; cell.z = 0;
            if (!paths.ContainsKey(cell)) continue;
            if (occupied.Contains(cell)) continue;
            bestCell = cell;
            found = true;
            break;
        }

        // 2ª passagem: qualquer hex alcançável se nenhum adjacente estiver livre
        if (!found)
        {
            int bestSteps = int.MaxValue;
            foreach (Vector3Int cell in paths.Keys)
            {
                if (cell == fromCell) continue;
                if (occupied.Contains(cell)) continue;
                int steps = paths[cell].Count;
                if (steps < bestSteps) { bestSteps = steps; bestCell = cell; found = true; }
            }
        }

        if (!found) return null;

        Debug.Log($"{TL("Swap")} {occupant.InstanceId} cede edificio para #{incoming.InstanceId} (HP {occupant.CurrentHP}→{incoming.CurrentHP}) → {fromCell}→{bestCell}");
        plannedDestinations.Add(bestCell);
        return BuildMoveBatch(occupant, snapshot.AITeam, fromCell, bestCell, paths);
    }
}
