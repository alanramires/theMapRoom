using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Capturador Defensor - setor conquistado ou objetivo defensivo
    // -------------------------------------------------------------------------

    private PlayerAction DecideCapturerDefenderAction(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned, Vector3Int fromCell)
    {
        assigned.Status = ObjectiveStatus.Complete;

        SectorManager.TryGetSectorInfo(assigned.Sector, out SectorManager.SectorInfo secInfo);
        Vector3Int repCell = secInfo != null ? secInfo.RepresentativeCell : fromCell; repCell.z = 0;

        // Captura oportunista mesmo em defesa: setor vazio é sempre preenchido
        Dictionary<Vector3Int, List<Vector3Int>> defPaths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> defOcc = BuildOccupied(unit);
        if (defPaths != null && TryFindOpportunisticCapture(unit, defPaths, defOcc, repCell, out Vector3Int defOpCell))
        {
            Debug.Log($"{TL("Oportunista")} {unit.InstanceId} captura oportunista @ {defOpCell} (defende {assigned.Sector})");
            return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, defOpCell, defPaths);
        }

        // Unidade no repCell → defende ativamente
        if (fromCell == repCell)
        {
            if (HasAttackTargetAtCurrentPos(unit))
            {
                var defTargets = new List<PodeMirarTargetOption>();
                PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                    SensorMovementMode.MoveuParado, defTargets);
                UnitManager defBest = null; float defPri = float.MinValue;
                foreach (PodeMirarTargetOption opt in defTargets)
                {
                    if (opt?.targetUnit == null) continue;
                    Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                    float p = AttackTargetPriority(tc, repCell);
                    if (p > defPri) { defPri = p; defBest = opt.targetUnit; }
                }
                if (defBest != null)
                {
                    Vector3Int dtCell = defBest.CurrentCellPosition; dtCell.z = 0;
                    Debug.Log($"{TL("Defensor")} {unit.InstanceId} defende {assigned.Sector} — ataca {defBest.UnitDisplayName}#{defBest.InstanceId} @ {dtCell}");
                    return BuildAttackBatch(unit, snapshot.AITeam, fromCell, fromCell,
                        defBest.InstanceId.ToString(), dtCell);
                }
            }
            Debug.Log($"{TL("Defensor")} {unit.InstanceId} segura {assigned.Sector} — mantém posição");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);
        }

        if (defPaths == null) return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        // Unidade fora do repCell → cobre ou combate na zona
        if (defPaths.ContainsKey(repCell) && !defOcc.Contains(repCell))
        {
            var coverBuffer = new List<PodeMirarTargetOption>();
            if (PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                    SensorMovementMode.MoveuAndando, coverBuffer, fromCell: repCell) && coverBuffer.Count > 0)
            {
                UnitManager coverTarget = null; float coverPri = float.MinValue;
                foreach (PodeMirarTargetOption opt in coverBuffer)
                {
                    if (opt?.targetUnit == null) continue;
                    Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                    if (SectorManager.HexDistance(tc, repCell) > DefenseEnemyRange) continue;
                    float p = AttackTargetPriority(tc, repCell);
                    if (p > coverPri) { coverPri = p; coverTarget = opt.targetUnit; }
                }
                if (coverTarget != null)
                {
                    Vector3Int ctCell = coverTarget.CurrentCellPosition; ctCell.z = 0;
                    Debug.Log($"{TL("Defensor")} {unit.InstanceId} cobre + ataca {assigned.Sector} via {repCell} → {coverTarget.UnitDisplayName}#{coverTarget.InstanceId}");
                    return BuildAttackBatch(unit, snapshot.AITeam, fromCell, repCell,
                        coverTarget.InstanceId.ToString(), ctCell, defPaths);
                }
            }
            Debug.Log($"{TL("Defensor")} {unit.InstanceId} reforça {assigned.Sector} → {repCell}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, repCell, defPaths);
        }

        // repCell ocupada ou fora de alcance — combate na zona
        bool inDefenseZone = SectorManager.HexDistance(fromCell, repCell) <= DefenseEnemyRange;
        var zoneEnemies = new List<UnitManager>();
        {
            MatchController mcZone = GetMatchController();
            foreach (UnitManager enemy in UnitManager.AllActive)
            {
                if (enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked) continue;
                if (mcZone != null && !mcZone.IsUnitVisibleForTeam(enemy, snapshot.AITeam)) continue;
                Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                if (SectorManager.HexDistance(ec, repCell) <= DefenseEnemyRange) zoneEnemies.Add(enemy);
            }
        }

        if (!inDefenseZone)
        {
            Vector3Int bestAttCell = fromCell; UnitManager bestAttEnemy = null; float bestAttPri = float.MinValue;
            foreach (Vector3Int cell in defPaths.Keys)
            {
                if (defOcc.Contains(cell)) continue;
                var buf = new List<PodeMirarTargetOption>();
                if (!PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                        SensorMovementMode.MoveuAndando, buf, fromCell: cell)) continue;
                foreach (PodeMirarTargetOption opt in buf)
                {
                    if (opt?.targetUnit == null || !zoneEnemies.Contains(opt.targetUnit)) continue;
                    Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                    float p = AttackTargetPriority(tc, repCell);
                    if (p > bestAttPri) { bestAttPri = p; bestAttEnemy = opt.targetUnit; bestAttCell = cell; }
                }
            }
            if (bestAttEnemy != null)
            {
                bool dpqPrefAdv = unit.TryGetUnitData(out UnitData udAdv) && udAdv.prioritizeDpqAtBattle;
                if (dpqPrefAdv) { Vector3Int ec2 = bestAttEnemy.CurrentCellPosition; ec2.z = 0;
                    if (TryFindBestLoSCell(unit, defPaths, defOcc, ec2, out Vector3Int dpqAdv)) bestAttCell = dpqAdv; }
                Vector3Int enemyCell = bestAttEnemy.CurrentCellPosition; enemyCell.z = 0;
                Debug.Log($"{TL("Defensor")} {unit.InstanceId} avança + ataca zona de {assigned.Sector} via {bestAttCell} → {bestAttEnemy.UnitDisplayName}#{bestAttEnemy.InstanceId}");
                return BuildAttackBatch(unit, snapshot.AITeam, fromCell, bestAttCell,
                    bestAttEnemy.InstanceId.ToString(), enemyCell, defPaths);
            }
            Vector3Int advTarget = repCell;
            if (zoneEnemies.Count > 0)
            {
                float closestD = float.MaxValue;
                foreach (UnitManager ze in zoneEnemies)
                {
                    Vector3Int ec = ze.CurrentCellPosition; ec.z = 0;
                    float d = SectorManager.HexDistance(fromCell, ec);
                    if (d < closestD) { closestD = d; advTarget = ec; }
                }
            }
            Vector3Int adv     = fromCell;
            float      advDist = SectorManager.HexDistance(fromCell, advTarget);
            float      advHqTie = CalculateEnemyHqTieBreak(CalculateEnemyHqDistance(fromCell, snapshot, unit));
            var defMarchLog = showAIUnitHUD ? new System.Text.StringBuilder() : null;
            defMarchLog?.AppendLine($"{TL("Defensor")} Unit{unit.InstanceId} marcha → {assigned.Sector} advTarget={advTarget}");
            foreach (Vector3Int cell in defPaths.Keys)
            {
                if (defOcc.Contains(cell)) continue;
                float d = SectorManager.HexDistance(cell, advTarget);
                if (d > advDist) continue;
                float hqDist = CalculateEnemyHqDistance(cell, snapshot, unit);
                float hqTie  = CalculateEnemyHqTieBreak(hqDist);
                string marker = (d < advDist || hqTie > advHqTie) ? "★" : " ";
                defMarchLog?.AppendLine($"  {marker} {cell} dist={d:F1} hq={( hqDist < float.MaxValue ? hqDist.ToString("F1") : "?")} hqTie={hqTie:F1}");
                if (d < advDist || hqTie > advHqTie) { advDist = d; advHqTie = hqTie; adv = cell; }
            }
            if (defMarchLog != null) Debug.Log(defMarchLog.ToString());
            Debug.Log($"{TL("Defensor")} {unit.InstanceId} marcha para zona de {assigned.Sector} via {adv}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, adv, defPaths);
        }

        // Na zona: ataca inimigos da zona
        Vector3Int bestAttackMove = fromCell; UnitManager bestAttackTarget = null; float bestAttackPri = float.MinValue;
        var stayBuf = new List<PodeMirarTargetOption>();
        if (PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                SensorMovementMode.MoveuParado, stayBuf) && stayBuf.Count > 0)
            foreach (PodeMirarTargetOption opt in stayBuf)
            {
                if (opt?.targetUnit == null || !zoneEnemies.Contains(opt.targetUnit)) continue;
                Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                float p = AttackTargetPriority(tc, repCell);
                if (p > bestAttackPri) { bestAttackPri = p; bestAttackTarget = opt.targetUnit; bestAttackMove = fromCell; }
            }
        foreach (Vector3Int cell in defPaths.Keys)
        {
            if (defOcc.Contains(cell)) continue;
            var moveBuf = new List<PodeMirarTargetOption>();
            if (!PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                    SensorMovementMode.MoveuAndando, moveBuf, fromCell: cell)) continue;
            foreach (PodeMirarTargetOption opt in moveBuf)
            {
                if (opt?.targetUnit == null || !zoneEnemies.Contains(opt.targetUnit)) continue;
                Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                float p = AttackTargetPriority(tc, repCell);
                if (p > bestAttackPri) { bestAttackPri = p; bestAttackTarget = opt.targetUnit; bestAttackMove = cell; }
            }
        }
        if (bestAttackTarget != null)
        {
            Vector3Int atCell = bestAttackTarget.CurrentCellPosition; atCell.z = 0;
            bool dpqPref = unit.TryGetUnitData(out UnitData udDef2) && udDef2.prioritizeDpqAtBattle;
            if (dpqPref && TryFindBestLoSCell(unit, defPaths, defOcc, atCell, out Vector3Int dpqCell2))
                bestAttackMove = dpqCell2;
            Debug.Log($"{TL("Defensor")} {unit.InstanceId} defende {assigned.Sector} — ataca via {bestAttackMove} → {bestAttackTarget.UnitDisplayName}#{bestAttackTarget.InstanceId}");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, bestAttackMove,
                bestAttackTarget.InstanceId.ToString(), atCell, defPaths);
        }

        if (fromCell != repCell
            && TryFindDefensiveInterceptCell(unit, snapshot, fromCell, repCell, defPaths, defOcc,
                out Vector3Int interceptCell, out UnitManager interceptEnemy))
        {
            Vector3Int enemyCell = interceptEnemy.CurrentCellPosition; enemyCell.z = 0;
            Debug.Log($"{TL("Defensor")} {unit.InstanceId} intercepta ameaça de {assigned.Sector} via {interceptCell} → {interceptEnemy.UnitDisplayName}#{interceptEnemy.InstanceId} @ {enemyCell}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, interceptCell, defPaths);
        }

        Vector3Int bestPos = fromCell; float bestPosDist = SectorManager.HexDistance(fromCell, repCell);
        foreach (Vector3Int cell in defPaths.Keys)
        {
            if (defOcc.Contains(cell)) continue;
            float d = SectorManager.HexDistance(cell, repCell);
            if (d < bestPosDist) { bestPosDist = d; bestPos = cell; }
        }
        if (bestPos != fromCell)
        {
            Debug.Log($"{TL("Defensor")} {unit.InstanceId} aguarda posição em {assigned.Sector} via {bestPos}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestPos, defPaths);
        }
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);
    }

    private bool TryFindDefensiveInterceptCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int repCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out UnitManager bestEnemy)
    {
        bestCell = fromCell;
        bestEnemy = null;
        if (unit == null || snapshot == null || paths == null)
            return false;

        int interceptRange = DefenseEnemyRange + 1;
        var threats = new List<UnitManager>();
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, snapshot.AITeam)) continue;

            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, repCell) <= interceptRange)
                threats.Add(enemy);
        }

        if (threats.Count == 0)
            return false;

        bool preferDpq = unit.TryGetUnitData(out UnitData data) && data.prioritizeDpqAtBattle;
        float bestScore = float.MinValue;
        float fromBestThreatDist = float.MaxValue;

        foreach (UnitManager enemy in threats)
        {
            Vector3Int enemyCell = enemy.CurrentCellPosition; enemyCell.z = 0;
            float fromEnemyDist = SectorManager.HexDistance(fromCell, enemyCell);
            if (fromEnemyDist < fromBestThreatDist)
                fromBestThreatDist = fromEnemyDist;

            foreach (Vector3Int cell in paths.Keys)
            {
                if (cell != fromCell && occupied.Contains(cell)) continue;

                float enemyDist = SectorManager.HexDistance(cell, enemyCell);
                if (enemyDist >= fromEnemyDist) continue;

                float repDist = SectorManager.HexDistance(cell, repCell);
                if (repDist > interceptRange) continue;

                float dpq = preferDpq ? GetTerrainDpqPontos(cell) : GetTerrainEv(cell);
                float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                float score =
                    (fromEnemyDist - enemyDist) * 1000f
                    - repDist * 120f
                    + dpq * 80f
                    - threat * ThreatWeight
                    - GetPathStepCount(paths, cell);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestEnemy = enemy;
                }
            }
        }

        return bestEnemy != null && bestCell != fromCell;
    }
}
