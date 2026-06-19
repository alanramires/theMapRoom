using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Capturador Defensor - setor conquistado ou objetivo defensivo
    // -------------------------------------------------------------------------

    private PlayerAction DecideCapturerDefenderAction(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned, Vector3Int fromCell)
    {
        TeamObjectivePlan activePlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        if (!IsCriticalHomeDefenseObjective(assigned, snapshot.AITeam)
            && TryFindCriticalHomeDefenseObjectiveForUnit(activePlan, snapshot.AITeam, unit, fromCell, "Defensor", out SectorObjective criticalHome))
        {
            Debug.Log($"{TL("Defensor")} {unit.InstanceId} redireciona {assigned.Sector} -> {criticalHome.Sector}: Base/HQ sob ameaca");
            assigned = criticalHome;
        }

        bool activeCriticalHomeDefense = IsCriticalHomeDefenseObjective(assigned, snapshot.AITeam);
        bool activeRecentGarrison = IsRecentlyCapturedSector(snapshot.AITeam, assigned.Sector, snapshot.TurnNumber);
        bool activeRallyAssembly = IsActiveRallyAssemblyObjective(assigned)
            && IsValidRallyAssemblySectorForSlot(assigned.Sector, snapshot.AITeam, snapshot.AISlotIndex);
        if (IsRallyAssemblyObjective(assigned) && !activeRallyAssembly)
        {
            assigned.ObjectiveType = AIObjectiveType.CaptureSector;
            assigned.Status = ObjectiveStatus.Pending;
            Debug.Log($"{TL("Defensor")} {unit.InstanceId} libera {assigned.Sector}: rally pertence a outro slot");
            return null;
        }

        assigned.Status = activeRallyAssembly
            ? ObjectiveStatus.Pursuing
            : (activeCriticalHomeDefense || activeRecentGarrison) ? ObjectiveStatus.Defending : ObjectiveStatus.Complete;
        ApplyPlanHUD(unit, assigned, UnitRole.Capturador);

        TryGetAnySectorInfo(assigned.Sector, out SectorManager.SectorInfo secInfo);
        Vector3Int repCell = secInfo != null ? secInfo.RepresentativeCell : fromCell; repCell.z = 0;
        if (activeRallyAssembly)
            repCell = ResolveRallyAssemblyAnchor(assigned, snapshot.AITeam, repCell);

        // No active SOS: release to HexEvaluator only if neither the defended sector nor
        // the unit's current area has visible pressure.
        if (!activeCriticalHomeDefense && !activeRecentGarrison && !activeRallyAssembly)
        {
            bool sectorEnemyNearby = false;
            bool localEnemyNearby = false;
            int sectorEnemyCount = 0;
            int localEnemyCount = 0;
            int localThreatRange = Mathf.Max(DefenseEnemyRange, Mathf.Max(0, unit.RemainingMovementPoints) + 1);
            MatchController mcRelease = GetMatchController();
            foreach (UnitManager e in UnitManager.AllActive)
            {
                if (e == null || e.TeamId == snapshot.AITeam || e.IsDead || e.IsEmbarked) continue;
                Vector3Int ec = e.CurrentCellPosition; ec.z = 0;
                if (mcRelease != null && !mcRelease.IsUnitVisibleForTeamNoCache(e, snapshot.AITeam))
                    continue;

                float sectorDist = SectorManager.HexDistance(ec, repCell);
                float localDist = SectorManager.HexDistance(ec, fromCell);
                if (sectorDist <= DefenseEnemyRange)
                {
                    sectorEnemyNearby = true;
                    sectorEnemyCount++;
                }
                if (localDist <= localThreatRange)
                {
                    localEnemyNearby = true;
                    localEnemyCount++;
                }
            }
            if (!sectorEnemyNearby && !localEnemyNearby)
            {
                Debug.Log($"{TL("Defensor")} {unit.InstanceId} libera {assigned.Sector}: setor e area local sem inimigos visiveis");
                return null;
            }
            if (!sectorEnemyNearby && localEnemyNearby)
                Debug.Log($"{TL("Defensor")} {unit.InstanceId} mantem defesa local em {assigned.Sector}: inimigos locais={localEnemyCount} setor={sectorEnemyCount}");
        }

        // Captura oportunista mesmo em defesa: setor vazio é sempre preenchido
        Dictionary<Vector3Int, List<Vector3Int>> defPaths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> defOcc = BuildOccupied(unit);
        if (defPaths != null
            && TryFindUnreservedOpportunisticCapture(unit, snapshot.AITeam, defPaths, defOcc, repCell, out Vector3Int defOpCell, "defensor"))
        {
            Debug.Log($"{TL("Oportunista")} {unit.InstanceId} captura oportunista @ {defOpCell} (defende {assigned.Sector})");
            return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, defOpCell, defPaths);
        }

        // Unidade no repCell → defende ativamente
        if (defPaths != null
            && TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, defPaths, defOcc, out PlayerAction vacateAction))
            return vacateAction;

        if (fromCell == repCell)
        {
            if (HasAttackTargetAtCurrentPos(unit))
            {
                var defTargets = new List<PodeMirarTargetOption>();
                PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                    SensorMovementMode.MoveuParado, defTargets);
                UnitManager defBest = null; float defScore = float.MinValue; string defReason = "";
                foreach (PodeMirarTargetOption opt in defTargets)
                {
                    if (opt?.targetUnit == null) continue;
                    Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                    if (TryEvaluateDefenderAttackScore(unit, opt.targetUnit, fromCell, tc, repCell, null, out float score, out string reason)
                        && score > defScore)
                    {
                        defScore = score;
                        defReason = reason;
                        defBest = opt.targetUnit;
                    }
                }
                if (defBest != null)
                {
                    Vector3Int dtCell = defBest.CurrentCellPosition; dtCell.z = 0;
                    Debug.Log($"{TL("Defensor")} {unit.InstanceId} defende {assigned.Sector} — ataca {defBest.UnitDisplayName}#{defBest.InstanceId} @ {dtCell} ({defReason})");
                    return BuildAttackBatch(unit, snapshot.AITeam, fromCell, fromCell,
                        defBest.InstanceId.ToString(), dtCell);
                }
            }
            bool isLongRangeStationary = unit.TryGetUnitData(out UnitData stationaryUd) && stationaryUd != null && stationaryUd.longRangeStationary;
            if (!activeCriticalHomeDefense || activeRecentGarrison || isLongRangeStationary)
            {
                string holdKind = activeRallyAssembly ? "rally" : (activeCriticalHomeDefense ? "SOS" : (activeRecentGarrison ? "guarnicao" : "guarda"));
                Debug.Log($"{TL("Defensor")} {unit.InstanceId} segura {assigned.Sector} ({holdKind}) — mantém posição");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);
            }
            // SOS crítico + móvel: cai na lógica de zona para avaliar mover+atacar
            Debug.Log($"{TL("Defensor")} {unit.InstanceId} SOS {assigned.Sector} — busca mover+atacar na zona");
        }

        if (defPaths == null) return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        // Unidade fora do repCell → cobre ou combate na zona
        if (defPaths.ContainsKey(repCell) && !defOcc.Contains(repCell))
        {
            var coverBuffer = new List<PodeMirarTargetOption>();
            if (PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                    SensorMovementMode.MoveuAndando, coverBuffer, fromCell: repCell) && coverBuffer.Count > 0)
            {
                UnitManager coverTarget = null; float coverScore = float.MinValue; string coverReason = "";
                foreach (PodeMirarTargetOption opt in coverBuffer)
                {
                    if (opt?.targetUnit == null) continue;
                    Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                    if (SectorManager.HexDistance(tc, repCell) > DefenseEnemyRange) continue;
                    if (TryEvaluateDefenderAttackScore(unit, opt.targetUnit, repCell, tc, repCell, defPaths, out float score, out string reason)
                        && score > coverScore)
                    {
                        coverScore = score;
                        coverReason = reason;
                        coverTarget = opt.targetUnit;
                    }
                }
                if (coverTarget != null)
                {
                    Vector3Int ctCell = coverTarget.CurrentCellPosition; ctCell.z = 0;
                    Debug.Log($"{TL("Defensor")} {unit.InstanceId} cobre + ataca {assigned.Sector} via {repCell} → {coverTarget.UnitDisplayName}#{coverTarget.InstanceId} ({coverReason})");
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
                if (mcZone != null && !mcZone.IsUnitVisibleForTeamNoCache(enemy, snapshot.AITeam)) continue;
                Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                if (SectorManager.HexDistance(ec, repCell) <= DefenseEnemyRange) zoneEnemies.Add(enemy);
            }
        }
        int engageRadius = Mathf.Max(0, unit.RemainingMovementPoints) + 1;
        MatchController mcLocal = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mcLocal != null && !mcLocal.IsUnitVisibleForTeamNoCache(enemy, snapshot.AITeam)) continue;
            if (zoneEnemies.Contains(enemy)) continue;

            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, fromCell) <= engageRadius)
                zoneEnemies.Add(enemy);
        }

        if (!inDefenseZone)
        {
            Vector3Int bestAttCell = fromCell; UnitManager bestAttEnemy = null; float bestAttScore = float.MinValue; string bestAttReason = "";
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
                    if (TryEvaluateDefenderAttackScore(unit, opt.targetUnit, cell, tc, repCell, defPaths, out float score, out string reason)
                        && score > bestAttScore)
                    {
                        bestAttScore = score;
                        bestAttReason = reason;
                        bestAttEnemy = opt.targetUnit;
                        bestAttCell = cell;
                    }
                }
            }
            if (bestAttEnemy != null)
            {
                bool dpqPrefAdv = unit.TryGetUnitData(out UnitData udAdv) && udAdv.prioritizeDpqAtBattle;
                if (dpqPrefAdv) { Vector3Int ec2 = bestAttEnemy.CurrentCellPosition; ec2.z = 0;
                if (TryFindBestLoSCell(unit, defPaths, defOcc, ec2, out Vector3Int dpqAdv)) bestAttCell = dpqAdv; }
                Vector3Int enemyCell = bestAttEnemy.CurrentCellPosition; enemyCell.z = 0;
                Debug.Log($"{TL("Defensor")} {unit.InstanceId} avança + ataca zona de {assigned.Sector} via {bestAttCell} → {bestAttEnemy.UnitDisplayName}#{bestAttEnemy.InstanceId} ({bestAttReason})");
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
        Vector3Int bestAttackMove = fromCell; UnitManager bestAttackTarget = null; float bestAttackScore = float.MinValue;
        string bestAttackReason = "";
        var stayBuf = new List<PodeMirarTargetOption>();
        if (PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                SensorMovementMode.MoveuParado, stayBuf) && stayBuf.Count > 0)
            foreach (PodeMirarTargetOption opt in stayBuf)
            {
                if (opt?.targetUnit == null || !zoneEnemies.Contains(opt.targetUnit)) continue;
                Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                if (TryEvaluateDefenderAttackScore(unit, opt.targetUnit, fromCell, tc, repCell, defPaths, out float score, out string reason)
                    && score > bestAttackScore)
                {
                    bestAttackScore = score;
                    bestAttackReason = reason;
                    bestAttackTarget = opt.targetUnit;
                    bestAttackMove = fromCell;
                }
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
                if (TryEvaluateDefenderAttackScore(unit, opt.targetUnit, cell, tc, repCell, defPaths, out float score, out string reason)
                    && score > bestAttackScore)
                {
                    bestAttackScore = score;
                    bestAttackReason = reason;
                    bestAttackTarget = opt.targetUnit;
                    bestAttackMove = cell;
                }
            }
        }
        if (bestAttackTarget != null)
        {
            Vector3Int atCell = bestAttackTarget.CurrentCellPosition; atCell.z = 0;
            bool dpqPref = unit.TryGetUnitData(out UnitData udDef2) && udDef2.prioritizeDpqAtBattle;
            if (dpqPref && TryFindBestLoSCell(unit, defPaths, defOcc, atCell, out Vector3Int dpqCell2))
                bestAttackMove = dpqCell2;
            Debug.Log($"{TL("Defensor")} {unit.InstanceId} defende {assigned.Sector} — ataca via {bestAttackMove} → {bestAttackTarget.UnitDisplayName}#{bestAttackTarget.InstanceId} ({bestAttackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, bestAttackMove,
                bestAttackTarget.InstanceId.ToString(), atCell, defPaths);
        }

        if (fromCell != repCell
            && TryFindDefensiveInterceptCell(unit, snapshot, fromCell, repCell, defPaths, defOcc,
                out Vector3Int interceptCell, out UnitManager interceptEnemy, out string interceptReason))
        {
            Vector3Int enemyCell = interceptEnemy.CurrentCellPosition; enemyCell.z = 0;
            Debug.Log($"{TL("Defensor")} {unit.InstanceId} intercepta ameaça de {assigned.Sector} via {interceptCell} → {interceptEnemy.UnitDisplayName}#{interceptEnemy.InstanceId} @ {enemyCell}");
            if (CanAttackTargetFrom(fromCell, interceptCell, unit, interceptEnemy)
                && PassesAttackDecision(unit, interceptEnemy, interceptCell, true, out string interceptAttackDecision))
            {
                Debug.Log($"{TL("Defensor")} {unit.InstanceId} intercepta ataque aprovado ({interceptAttackDecision})");
                return BuildAttackBatch(unit, snapshot.AITeam, fromCell, interceptCell,
                    interceptEnemy.InstanceId.ToString(), enemyCell, defPaths);
            }
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

    private bool TryEvaluateDefenderAttackScore(
        UnitManager attacker,
        UnitManager target,
        Vector3Int attackCell,
        Vector3Int targetCell,
        Vector3Int repCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out float score,
        out string reason)
    {
        score = float.MinValue;
        reason = "";
        if (attacker == null || target == null)
            return false;
        if (!PassesAttackDecision(attacker, target, attackCell, true, out string attackDecisionReason))
        {
            reason = attackDecisionReason;
            return false;
        }

        bool onObjective = targetCell == repCell;
        float distToObjective = SectorManager.HexDistance(targetCell, repCell);
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, targetCell);
        bool inConstruction = bldg != null;

        int damage = 0;
        int hpAfter = Mathf.Max(0, target.CurrentHP);
        bool kill = false;
        bool simulated = false;

        if (attacker.TryGetUnitData(out UnitData attackerData) && attackerData != null
            && target.TryGetUnitData(out UnitData targetData) && targetData != null
            && turnStateManager != null
            && turnStateManager.RpsDatabaseRef != null
            && turnStateManager.DpqMatchupDatabaseRef != null
            && turnStateManager.WeaponPriorityDataRef != null)
        {
            int distance = Mathf.Max(1, Mathf.RoundToInt(SectorManager.HexDistance(attackCell, targetCell)));
            AICombatHpSimulator.AICombatHpResult sim = AICombatHpSimulator.Simulate(
                attackerData,
                targetData,
                Mathf.Max(0, attacker.CurrentHP),
                Mathf.Max(0, target.CurrentHP),
                distance,
                turnStateManager.RpsDatabaseRef,
                turnStateManager.DpqMatchupDatabaseRef,
                turnStateManager.WeaponPriorityDataRef);

            if (sim.isValid)
            {
                simulated = true;
                hpAfter = Mathf.Max(0, sim.defenderHpAfter);
                damage = Mathf.Max(0, target.CurrentHP - hpAfter);
                kill = sim.killGuaranteed;
            }
        }

        int pathSteps = paths != null ? GetPathStepCount(paths, attackCell) : 0;
        float objectiveScore = onObjective ? 1000000f : 0f;
        float killScore = kill ? 100000f : 0f;
        float hpScore = Mathf.Max(0, 20 - Mathf.Max(0, target.CurrentHP)) * 1000f;
        float damageScore = damage * 4000f;
        float proximityScore = Mathf.Max(0f, 20f - distToObjective) * 100f;
        float constructionScore = inConstruction ? 50f : 0f;
        float stableTie = -target.InstanceId * 0.001f;

        score = objectiveScore
            + killScore
            + damageScore
            + hpScore
            + proximityScore
            + constructionScore
            - pathSteps * 2f
            + stableTie;

        reason = $"score={score:F0} kill={kill} hp={target.CurrentHP}->{hpAfter} dmg={damage} distObj={distToObjective:F1} bldg={inConstruction} sim={simulated} {attackDecisionReason}";
        return true;
    }

    private bool TryFindDefensiveInterceptCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int repCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out UnitManager bestEnemy,
        out string reason)
    {
        bestCell = fromCell;
        bestEnemy = null;
        reason = "";
        if (unit == null || snapshot == null || paths == null)
            return false;

        int interceptRange = DefenseEnemyRange;
        var threats = new List<UnitManager>();
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeamNoCache(enemy, snapshot.AITeam)) continue;

            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, repCell) <= interceptRange)
                threats.Add(enemy);
        }

        if (threats.Count == 0)
            return false;

        bool preferDpq = unit.TryGetUnitData(out UnitData data) && data.prioritizeDpqAtBattle;
        float bestScore = float.MinValue;
        float fromBestThreatDist = float.MaxValue;
        float fromRepDist = SectorManager.HexDistance(fromCell, repCell);

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
                if (repDist > fromRepDist) continue;

                float dpq = preferDpq ? GetTerrainDpqPontos(cell) : GetTerrainEv(cell);
                float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                float score =
                    (fromEnemyDist - enemyDist) * 1000f
                    - repDist * 250f
                    + dpq * 80f
                    - threat * ThreatWeight
                    - GetPathStepCount(paths, cell);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestEnemy = enemy;
                    reason = $"threatDist={SectorManager.HexDistance(enemyCell, repCell):F1} enemyDist={fromEnemyDist:F1}->{enemyDist:F1} repDist={fromRepDist:F1}->{repDist:F1}";
                }
            }
        }

        return bestEnemy != null && bestCell != fromCell;
    }
}
