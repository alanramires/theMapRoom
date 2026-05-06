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

        // Check if we can disembark passengers close to their targets
        var disembarkOptions = new List<PodeDesembarcarOption>();
        bool canDisembark = PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, disembarkOptions);

        if (canDisembark && disembarkOptions.Count > 0)
        {
            List<PodeDesembarcarOption> selected = SelectBestDisembarkPerPassenger(disembarkOptions, passengers, plan, snapshot);
            if (selected.Count > 0)
            {
                PodeDesembarcarOption primaryOption = selected.Find(o => o.passengerUnit == primaryPassenger);
                if (primaryOption != null)
                {
                    Vector3Int dc = primaryOption.disembarkCell; dc.z = 0;
                    if (SectorManager.HexDistance(dc, primaryTarget) <= TransportDropOffRange)
                    {
                        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — desembarca {selected.Count} passageiro(s) para {primaryTarget}");
                        return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected);
                    }
                }
            }
        }

        // Move toward primary target
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        // Restricted combat: attack only near-dead enemies (HP <= 2) with at most 2h route deviation
        if (TryFindTransportCourierAttack(unit, snapshot, fromCell, paths, occupied, primaryTarget,
                out Vector3Int attackCell, out UnitManager attackTarget))
        {
            Vector3Int targetCell = attackTarget.CurrentCellPosition; targetCell.z = 0;
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — ataca oportunista HP={attackTarget.CurrentHP} via {attackCell}");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                attackTarget.InstanceId.ToString(), targetCell, paths);
        }

        Vector3Int moveTarget = FindTransportMove(fromCell, primaryTarget, paths, occupied, snapshot.AITeam);
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — move para {moveTarget} alvo={primaryTarget}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
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

    // For each passenger, picks the disembark option closest to their objective.
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

            foreach (PodeDesembarcarOption opt in options)
            {
                if (opt.passengerUnit != passenger) continue;
                Vector3Int dc = opt.disembarkCell; dc.z = 0;
                float dist = target != Vector3Int.zero
                    ? SectorManager.HexDistance(dc, target)
                    : 0f;
                if (dist < bestDist) { bestDist = dist; best = opt; }
            }

            if (best != null) selected.Add(best);
        }
        return selected;
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
