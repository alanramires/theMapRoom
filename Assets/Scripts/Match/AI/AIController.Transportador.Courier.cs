using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Courier — transporter carrying passengers, delivering to objective
    // -------------------------------------------------------------------------

    private PlayerAction DecideTransportadorCourierAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);

        List<UnitManager> passengers = CollectPassengers(unit);
        if (passengers.Count == 0)
        {
            Debug.LogWarning($"[AI] {TL("Transporte")} {unit.InstanceId} courier: cargo inconsistente, reverte para shuttle");
            return DecideRogueShuttleAction(unit, snapshot, plan);
        }

        UnitManager primaryPassenger = ResolvePrimaryPassenger(passengers);
        Vector3Int primaryTarget = ResolveUnitObjectiveCell(primaryPassenger, plan, snapshot);
        if (primaryTarget == Vector3Int.zero) primaryTarget = fromCell;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        Vector3Int moveTarget = FindTransportMove(unit, fromCell, primaryTarget, paths, occupied, snapshot.AITeam);

        // If FindTransportMove landed on the objective building itself, redirect to an adjacent
        // reachable cell so the passenger can be disembarked directly onto the building.
        if (moveTarget == primaryTarget)
        {
            var neighbors = new List<Vector3Int>(6);
            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, primaryTarget, neighbors);
            Vector3Int bestAdj = Vector3Int.zero;
            float bestThreat = float.MaxValue;
            foreach (Vector3Int nb in neighbors)
            {
                Vector3Int nbc = nb; nbc.z = 0;
                if (occupied.Contains(nbc) || !paths.ContainsKey(nbc)) continue;
                float threat = CalculateThreatLevel(nbc, snapshot.AITeam);
                if (bestAdj == Vector3Int.zero || threat < bestThreat - 0.001f)
                    { bestAdj = nbc; bestThreat = threat; }
            }
            if (bestAdj != Vector3Int.zero) moveTarget = bestAdj;
        }

        float moveImprovement = CalculateRouteDistanceOrHex(unit, fromCell, primaryTarget)
                              - CalculateRouteDistanceOrHex(unit, moveTarget, primaryTarget);

        // Priority 1: move + disembark when moving brings the APC meaningfully closer
        // AND the simulated drop-off from moveTarget is within delivery range.
        if (moveTarget != fromCell && moveImprovement > 1f)
        {
            List<PodeDesembarcarOption> optionsFromMove = SimulateDisembarkFromCell(unit, moveTarget);
            if (optionsFromMove != null && optionsFromMove.Count > 0)
            {
                List<PodeDesembarcarOption> selectedFromMove =
                    SelectBestDisembarkPerPassenger(optionsFromMove, passengers, plan, snapshot);
                PodeDesembarcarOption primaryOpt = selectedFromMove.Count > 0
                    ? selectedFromMove.Find(o => o.passengerUnit == primaryPassenger) : null;
                if (primaryOpt != null)
                {
                    Vector3Int dc = primaryOpt.disembarkCell; dc.z = 0;
                    if (SectorManager.HexDistance(dc, primaryTarget) <= TransportDropOffRange)
                    {
                        paths.TryGetValue(moveTarget, out List<Vector3Int> movePath);
                        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — move+desembarca {selectedFromMove.Count} passageiro(s) via {moveTarget} → {primaryTarget}");
                        return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selectedFromMove, moveTarget, movePath);
                    }
                }
            }
        }

        // Priority 2: disembark from current position.
        // Normal case: moving gains ≤1h, so current position is already near-optimal.
        // Emergency case: completely blocked (moveTarget == fromCell) — disembark regardless of
        // distance so passengers can fight instead of staying trapped in an immobile APC.
        bool isStuck = moveTarget == fromCell;
        var disembarkOptions = new List<PodeDesembarcarOption>();
        bool canDisembark = PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, disembarkOptions);
        if (canDisembark && disembarkOptions.Count > 0 && (moveImprovement <= 1f || isStuck))
        {
            List<PodeDesembarcarOption> selected = SelectBestDisembarkPerPassenger(disembarkOptions, passengers, plan, snapshot);
            if (selected.Count > 0)
            {
                PodeDesembarcarOption primaryOption = selected.Find(o => o.passengerUnit == primaryPassenger);
                if (primaryOption != null)
                {
                    Vector3Int dc = primaryOption.disembarkCell; dc.z = 0;
                    if (isStuck || SectorManager.HexDistance(dc, primaryTarget) <= TransportDropOffRange)
                    {
                        string reason = isStuck ? "bloqueado, libera carga" : $"desembarca para {primaryTarget}";
                        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — {reason} ({selected.Count} passageiro(s))");
                        return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected);
                    }
                }
            }
        }

        // No combat with passengers aboard — delivering is the only priority.

        // Priority 3: move toward target
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — move para {moveTarget} alvo={primaryTarget}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

    // Temporarily repositions the APC to simCell, collects disembark options from there,
    // then restores the original position. Side effects on occupancy/threat are transient
    // (restored before any other unit decision runs).
    private List<PodeDesembarcarOption> SimulateDisembarkFromCell(UnitManager unit, Vector3Int simCell)
    {
        Vector3Int originalCell = unit.CurrentCellPosition;
        simCell.z = 0; originalCell.z = 0;
        unit.SetCurrentCellPosition(simCell, enforceFinalOccupancyRule: false);
        var options = new List<PodeDesembarcarOption>();
        PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, options);
        unit.SetCurrentCellPosition(originalCell, enforceFinalOccupancyRule: false);
        return options;
    }

    // -------------------------------------------------------------------------
    // Passenger helpers
    // -------------------------------------------------------------------------

    private static List<UnitManager> CollectPassengers(UnitManager transporter)
    {
        var list = new List<UnitManager>();
        if (transporter.TransportedUnitSlots == null) return list;
        foreach (UnitTransportSeatRuntime seat in transporter.TransportedUnitSlots)
            if (seat.embarkedUnit != null && seat.embarkedUnit.IsEmbarked)
                list.Add(seat.embarkedUnit);
        return list;
    }

    private static UnitManager ResolvePrimaryPassenger(List<UnitManager> passengers)
    {
        UnitManager best = passengers[0];
        int bestPriority = int.MaxValue;
        foreach (UnitManager p in passengers)
        {
            if (!p.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
            int priority = (int)d.roles[0];
            if (priority < bestPriority) { bestPriority = priority; best = p; }
        }
        return best;
    }

    // For each passenger, picks the best delivery cell, preferring immediate capture chances
    // that do not pull the courier away from its current delivery route.
    private List<PodeDesembarcarOption> SelectBestDisembarkPerPassenger(
        List<PodeDesembarcarOption> options,
        List<UnitManager> passengers,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot)
    {
        var selected = new List<PodeDesembarcarOption>();
        foreach (UnitManager passenger in passengers)
        {
            Vector3Int target = ResolveUnitObjectiveCell(passenger, plan, snapshot);
            PodeDesembarcarOption best = null;
            float bestDist = float.MaxValue;
            float bestThreat = float.MaxValue;

            foreach (PodeDesembarcarOption opt in options)
            {
                if (opt.passengerUnit != passenger) continue;
                Vector3Int dc = opt.disembarkCell; dc.z = 0;
                float dist = target != Vector3Int.zero
                    ? SectorManager.HexDistance(dc, target)
                    : 0f;
                float threat = CalculateThreatLevel(dc, snapshot.AITeam);
                float score = ScoreCourierDisembarkOption(passenger, dc, target, snapshot.AITeam, dist, threat);
                float bestScore = best != null
                    ? ScoreCourierDisembarkOption(passenger, best.disembarkCell, target, snapshot.AITeam, bestDist, bestThreat)
                    : float.MinValue;
                bool isBetter = score > bestScore + 0.1f
                    || (score > bestScore - 0.1f && dist < bestDist - 0.1f)
                    || (score > bestScore - 0.1f && dist < bestDist + 0.1f && threat < bestThreat - 0.001f);
                if (isBetter) { bestDist = dist; bestThreat = threat; best = opt; }
            }

            if (best != null) selected.Add(best);
        }
        return selected;
    }

    private float ScoreCourierDisembarkOption(
        UnitManager passenger,
        Vector3Int disembarkCell,
        Vector3Int assignedTarget,
        TeamId aiTeam,
        float distToAssignedTarget,
        float threat)
    {
        disembarkCell.z = 0;
        float score = -distToAssignedTarget * 20f - threat * 8f;

        if (SimulateCaptureSensor(passenger, disembarkCell, out ConstructionManager captureTarget))
        {
            Vector3Int captureCell = captureTarget.CurrentCellPosition; captureCell.z = 0;
            bool isAssignedTarget = assignedTarget != Vector3Int.zero && captureCell == assignedTarget;
            bool isNeutralOrEnemy = captureTarget.TeamId != aiTeam;
            score += isAssignedTarget ? 3000f : isNeutralOrEnemy ? 1800f : 900f;
        }

        return score;
    }

    // -------------------------------------------------------------------------
    // Restricted combat (courier mode): HP <= 2, deviation <= 2h from route
    // -------------------------------------------------------------------------

    private bool TryFindTransportCourierAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int primaryTarget,
        out Vector3Int bestCell,
        out UnitManager bestTarget)
    {
        bestCell = fromCell;
        bestTarget = null;

        List<UnitManager> enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        if (enemies == null || enemies.Count == 0) return false;

        float fromDistToTarget = SectorManager.HexDistance(fromCell, primaryTarget);

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell != fromCell && occupied.Contains(cell)) continue;
            if (SectorManager.HexDistance(cell, primaryTarget) > fromDistToTarget + 2f) continue;

            foreach (UnitManager enemy in enemies)
            {
                if (enemy.CurrentHP > 2) continue;
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy)) continue;
                if (!PassesAttackDecision(unit, enemy, cell, false, out _)) continue;

                bestCell = cell;
                bestTarget = enemy;
                return true;
            }
        }
        return false;
    }
}
