using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private ConstructionManager FindSafeArtilleryDropConstruction(
        UnitManager unit, Vector3Int fromCell, TeamId aiTeam, HashSet<Vector3Int> occupied)
    {
        ConstructionManager best = null;
        float bestScore = float.MinValue;
        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.SlotIndex != ResolveAISlotKey(aiTeam)) continue;
            if (c.CurrentCapturePoints < c.CapturePointsMax) continue;
            Vector3Int cc = c.CurrentCellPosition; cc.z = 0;
            if (HasNearbyVisibleEnemy(cc, aiTeam, DefenseEnemyRange)) continue; // no exemption for home
            if (!IsRepairHomeConstruction(c, aiTeam) && !IsRepairConstructionSectorSafe(c, aiTeam)) continue;
            if (occupied.Contains(cc)) continue;
            float dist = SectorManager.HexDistance(fromCell, cc);
            float score = -dist * 100f + 500f;
            if (IsRepairHomeConstruction(c, aiTeam)) score += 25f;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return best;
    }

    // Drop field artillery progressively safer locations.
    // T1) safe construction (no nearby enemies) reachable this turn → disembark.
    // T2) safe construction exists but not reachable → march toward it with cargo.
    // T3) all constructions threatened → march toward nearest home (HQ/base), try to disembark in that sector.
    // T4) home sector occupied/inaccessible → any low-threat cell behind the lines (TryDropFireSupportConservative).

    private PlayerAction TryDropArtilleryAtSafeConstruction(
        UnitManager unit, UnitManager artPassenger, List<UnitManager> passengers,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        TeamId aiTeam = snapshot.AITeam;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);

        // ---- Tier 1 & 2: safe construction (no nearby enemies) ----
        var occupiedForSearch = new HashSet<Vector3Int>(occupied) { fromCell };
        ConstructionManager dropTarget = FindSafeArtilleryDropConstruction(unit, fromCell, aiTeam, occupiedForSearch);
        if (dropTarget != null)
        {
            Vector3Int targetCell = dropTarget.CurrentCellPosition; targetCell.z = 0;
            PlayerAction t1 = TryDisembarkArtAtConstruction(
                unit, artPassenger, passengers, plan, snapshot, aiTeam, fromCell, targetCell, paths, occupied,
                requireSafe: true);
            if (t1 != null) return t1;

            // Tier 2: march toward safe construction
            Vector3Int marchStep = FindRepairApproachStep(unit, aiTeam, fromCell, targetCell, dropTarget, paths, occupied, null, out _);
            if (marchStep != fromCell)
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} T2 marcha → construção segura {targetCell} com art #{artPassenger.InstanceId} via {marchStep}");
                return BuildMoveBatch(unit, aiTeam, fromCell, marchStep, paths);
            }
        }

        // ---- Tier 3: todas as construções sob ameaça → marcha para setor home e tenta desembarcar lá ----
        ConstructionManager homeTarget = FindNearestHomeConstruction(fromCell, aiTeam);
        if (homeTarget != null)
        {
            Vector3Int homeCell = homeTarget.CurrentCellPosition; homeCell.z = 0;

            // Try disembark in the home sector (no threat check — unit can hold its own there).
            PlayerAction t3 = TryDisembarkArtAtConstruction(
                unit, artPassenger, passengers, plan, snapshot, aiTeam, fromCell, homeCell, paths, occupied,
                requireSafe: false);
            if (t3 != null)
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} T3 desembarca art #{artPassenger.InstanceId} no setor home {homeCell}");
                return t3;
            }

            // March toward home sector with cargo
            Vector3Int marchHome = FindRepairApproachStep(unit, aiTeam, fromCell, homeCell, homeTarget, paths, occupied, null, out _);
            if (marchHome != fromCell)
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} T3 marcha → setor home {homeCell} com art #{artPassenger.InstanceId} via {marchHome}");
                return BuildMoveBatch(unit, aiTeam, fromCell, marchHome, paths);
            }
        }

        // ---- Tier 4: setor home lotado/inacessível → qualquer célula de baixa ameaça atrás das linhas ----
        Debug.Log($"{TL("Repair")} {unit.InstanceId} T4 último recurso atrás das linhas para art #{artPassenger.InstanceId}");
        return TryDropFireSupportConservative(unit, artPassenger, passengers, snapshot, plan, fromCell, fromCell, paths, occupied);
    }

    // Tries to disembark artPassenger onto a construction building near targetCell.
    // requireSafe=true: construction must have no nearby enemies.
    // requireSafe=false: accepts any allied home/safe construction regardless of threat.

    private PlayerAction TryDisembarkArtAtConstruction(
        UnitManager unit, UnitManager artPassenger, List<UnitManager> passengers,
        TeamObjectivePlan plan, AIWorldSnapshot snapshot, TeamId aiTeam,
        Vector3Int fromCell, Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied,
        bool requireSafe)
    {
        var candidates = new List<(Vector3Int cell, List<Vector3Int> path)> { (fromCell, null) };
        foreach (var kvp in paths)
        {
            Vector3Int c = kvp.Key; c.z = 0;
            if (c == fromCell || occupied.Contains(c)) continue;
            candidates.Add((c, kvp.Value));
        }
        candidates.Sort((a, b) =>
            SectorManager.HexDistance(a.cell, targetCell)
            .CompareTo(SectorManager.HexDistance(b.cell, targetCell)));

        foreach (var (tCell, tPath) in candidates)
        {
            List<PodeDesembarcarOption> opts;
            if (tCell == fromCell)
            {
                opts = new List<PodeDesembarcarOption>();
                PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, opts);
            }
            else opts = SimulateDisembarkFromCell(unit, tCell);

            if (opts == null || opts.Count == 0) continue;

            bool hasValidDrop = false;
            foreach (PodeDesembarcarOption opt in opts)
            {
                if (opt.passengerUnit != artPassenger) continue;
                Vector3Int dc = opt.disembarkCell; dc.z = 0;
                ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, dc);
                if (bldg == null || bldg.SlotIndex != ResolveAISlotKey(aiTeam)) continue;
                if (!IsRepairHomeConstruction(bldg, aiTeam) && !IsRepairConstructionSectorSafe(bldg, aiTeam)) continue;
                if (requireSafe && HasNearbyVisibleEnemy(dc, aiTeam, DefenseEnemyRange)) continue;
                hasValidDrop = true; break;
            }
            if (!hasValidDrop) continue;

            List<PodeDesembarcarOption> selected = SelectBestDisembarkPerPassenger(opts, passengers, plan, snapshot);
            if (tCell == fromCell)
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} desembarca art #{artPassenger.InstanceId} → {targetCell} safe={!requireSafe}");
                return BuildDesembarcarBatch(unit, aiTeam, fromCell, selected);
            }
            Debug.Log($"{TL("Repair")} {unit.InstanceId} move+desembarca art #{artPassenger.InstanceId} via {tCell} → {targetCell} safe={!requireSafe}");
            return BuildDesembarcarBatch(unit, aiTeam, fromCell, selected, tCell, tPath);
        }
        return null;
    }


}
