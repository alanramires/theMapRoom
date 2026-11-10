using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Shared fact for shopping and interceptor posture. All caches belong to the
// caller's decision; this service neither scans hidden units nor mutates them.
public static class AIAirExposureService
{
    public static HashSet<Vector3Int> GetAntiAirUmbrella(AIWorldSnapshot snapshot,
        UnityEngine.Tilemaps.Tilemap board, TerrainDatabase terrain, GameUnitClass preyClass,
        Dictionary<GameUnitClass, HashSet<Vector3Int>> cache)
    {
        if (cache.TryGetValue(preyClass, out HashSet<Vector3Int> cached))
            return cached;
        var umbrella = new HashSet<Vector3Int>();
        if (snapshot?.MyUnits != null)
            foreach (UnitManager unit in snapshot.MyUnits)
            {
                if (unit == null || unit.IsDead || unit.IsEmbarked
                    || !unit.TryGetUnitData(out UnitData data) || data == null
                    || !UnitRoleCompatibility.CanSatisfy(data, UnitRole.Antiaereo)
                    || (int)data.ResolveAiTargetPriorityForTargetClass(preyClass)
                        < (int)BazookaTargetPriority.Secondary)
                    continue;
                UnitReachEnvelope cover = BuildEnvelope(unit, board, terrain,
                    ReachIntent.Combat, Mathf.Max(1, unit.MaxMovementPoints));
                if (cover == null)
                    continue;
                foreach (Vector3Int cell in cover.AttackableCells)
                {
                    Vector3Int c = cell;
                    c.z = 0;
                    umbrella.Add(c);
                }
            }
        cache[preyClass] = umbrella;
        return umbrella;
    }

    public static bool TouchesUncovered(UnitReachEnvelope envelope, HashSet<Vector3Int> targets,
        HashSet<Vector3Int> umbrella)
    {
        if (targets == null || targets.Count == 0)
            return false;
        bool Hit(Vector3Int cell)
        {
            cell.z = 0;
            return targets.Contains(cell) && (umbrella == null || !umbrella.Contains(cell));
        }
        foreach (Vector3Int cell in envelope.MovementCells)
            if (Hit(cell)) return true;
        foreach (Vector3Int cell in envelope.AttackableCells)
            if (Hit(cell)) return true;
        return false;
    }

    public static UnitReachEnvelope BuildEnvelope(UnitManager unit,
        UnityEngine.Tilemaps.Tilemap board, TerrainDatabase terrain, ReachIntent intent, int budget)
    {
        return UnitReachEnvelopeService.Build(new UnitReachRequest
        {
            Unit = unit,
            BoardMap = board,
            TerrainDatabase = terrain,
            Intent = intent,
            Band = ReachBand.Tactical,
            SubStep = unit.GetAircraftType() != AircraftType.None ? ReachSubStep.Aereo
                : intent == ReachIntent.Combat
                    && !UnitReachEnvelopeService.IsSubStepValid(intent, ReachSubStep.Terrestre, unit)
                        ? ReachSubStep.Artilheiro : ReachSubStep.Terrestre,
            MovementBudget = budget,
            FilterByOperationDomain = false
        });
    }


    public static bool IsExposed(Vector3Int cell, AIWorldSnapshot snapshot,
        IReadOnlyList<UnitManager> knownAircraft, Tilemap board, TerrainDatabase terrain,
        Dictionary<int, UnitReachEnvelope> threats,
        Dictionary<GameUnitClass, HashSet<Vector3Int>> umbrellas)
    {
        if (knownAircraft == null || board == null) return false;
        cell.z = 0;
        var target = new HashSet<Vector3Int> { cell };
        foreach (UnitManager enemy in knownAircraft)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked || enemy.GetDomain() != Domain.Air
                || !enemy.TryGetUnitData(out UnitData data) || data == null) continue;
            if (!threats.TryGetValue(enemy.InstanceId, out UnitReachEnvelope threat))
            {
                threat = BuildEnvelope(enemy, board, terrain, ReachIntent.Combat,
                    Mathf.Max(1, enemy.MaxMovementPoints));
                threats[enemy.InstanceId] = threat;
            }
            if (threat != null && TouchesUncovered(threat, target,
                    GetAntiAirUmbrella(snapshot, board, terrain, data.unitClass, umbrellas)))
                return true;
        }
        return false;
    }
}
