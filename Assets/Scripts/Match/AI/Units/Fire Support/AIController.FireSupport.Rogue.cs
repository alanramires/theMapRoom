using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Fire Support Rogue - sem slot de plano, pressiona alvo estrategico visivel.
    // -------------------------------------------------------------------------

    private PlayerAction DecideRogueFireSupportAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int anchor = ResolveRogueFireSupportAnchor(snapshot, fromCell);
        bool artilleryOnly = IsArtilleryModeOnly(unit);

        // Artillery mode: prefer max-range fire, then close-range (combatant), then reposition.
        // "preferArtilleryModeBeforeCombatant" means the order of preference, not exclusivity.
        // Normal mode: attack immediately if any target is available.
        if (artilleryOnly)
        {
            if (TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor,
                    defensiveContext: false, out PlayerAction indirectAction, out string indirectReason, indirectOnly: true))
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue - {indirectReason}");
                return indirectAction;
            }
            // No max-range target — try close-range (combatant) before repositioning.
            if (TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor,
                    defensiveContext: false, out PlayerAction combatAction, out string combatReason))
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue (combatente) - {combatReason}");
                return combatAction;
            }
        }
        else
        {
            if (TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor,
                    defensiveContext: false, out PlayerAction attackAction, out string attackReason))
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue - {attackReason}");
                return attackAction;
            }
        }

        // Stationary fire support: hold position when the active front is within firing range.
        // Prevents artillery bought from lateral factories from drifting toward HQ when idle.
        if (IsLongRangeStationary(unit))
        {
            int maxRange = GetFireSupportMaxWeaponRange(unit);
            int holdRange = maxRange + 2;
            bool nearFront = false;

            if (snapshot?.EnemyUnits != null)
                foreach (UnitManager enemy in snapshot.EnemyUnits)
                {
                    if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
                    Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                    if (SectorManager.HexDistance(fromCell, ec) <= holdRange) { nearFront = true; break; }
                }

            if (!nearFront && snapshot?.EnemyBuildings != null)
                foreach (ConstructionManager bldg in snapshot.EnemyBuildings)
                {
                    if (bldg == null) continue;
                    Vector3Int bc = bldg.CurrentCellPosition; bc.z = 0;
                    if (SectorManager.HexDistance(fromCell, bc) <= holdRange) { nearFront = true; break; }
                }

            if (nearFront)
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue estacionario @ {fromCell} — frente a ≤{holdRange}h, segura");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
            }
        }

        if (IsFireSupportConservative(unit))
        {
            Vector3Int conservativeCell = FindConservativeRogueFireSupportCell(unit, snapshot, fromCell, paths, occupied);
            if (conservativeCell != fromCell)
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue conservador reagrupa via {conservativeCell}");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, conservativeCell, paths);
            }

            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue conservador segura @ {fromCell} - sem alvo");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        if (IsLongRangeStationary(unit) && IsFireSupportCloseEnoughToHold(unit, fromCell, anchor))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue estacionario @ {fromCell} - sem alvo");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        // Artillery mode: use zero margin so any improvement toward ideal range triggers a move.
        // This handles the case where the standard margin (120) blocks small-but-necessary adjustments
        // (e.g. backing up from 1h to 2h when maxRange=2, improvement ~86pts < 120).
        float repoMargin = artilleryOnly ? 0f : -1f;
        if (TryFindFireSupportRepositionCell(unit, snapshot, fromCell, anchor, paths, occupied,
                out Vector3Int moveCell, out string moveReason, moveMarginOverride: repoMargin))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue reposiciona via {moveCell} alvo={anchor} ({moveReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        // Artillery mode truly stuck — allow direct fire as absolute last resort.
        if (artilleryOnly && TryBuildBestFireSupportAttack(unit, snapshot, fromCell, paths, occupied, anchor,
                defensiveContext: false, out PlayerAction fallbackAction, out string fallbackReason))
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} rogue (direto fallback) - {fallbackReason}");
            return fallbackAction;
        }

        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }
}
