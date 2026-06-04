using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private static readonly Dictionary<TeamId, int> repairActivationsThisSessionByTeam = new Dictionary<TeamId, int>();

    // -------------------------------------------------------------------------
    // Modo de reparo
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideRepairAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        UpdateRepairState(unit, plan);
        if (!unit.IsUnderRepair)
            return null;

        // Aircraft under repair still navigates as aircraft. If it is currently
        // grounded, temporarily lift it for AI path planning so mountains/terrain
        // do not turn the repair march into a ground route.
        bool wasGrounded = unit.IsAircraftGrounded;
        if (wasGrounded)
            unit.SetAircraftGrounded(false);

        try
        {
            return DecideUnderRepairAction(unit, snapshot);
        }
        finally
        {
            if (wasGrounded)
                unit.SetAircraftGrounded(true);
        }
    }

    private void UpdateRepairState(UnitManager unit, TeamObjectivePlan plan)
    {
        if (!unit.TryGetUnitData(out UnitData data)) return;

        bool anyTrigger = EvaluateRepairTriggers(unit, data);

        if (!unit.IsUnderRepair && anyTrigger)
        {
            unit.SetIsUnderRepair(true);
            int sessionCount = IncrementRepairActivationCount(unit.TeamId);
            // Libera o slot do objetivo para reatribuição imediata
            if (plan != null)
            {
                foreach (SectorObjective obj in plan.Objectives)
                    foreach (SlotNeed slot in obj.Slots)
                        if (slot.Filled && slot.AssignedUnitId == unit.InstanceId)
                        {
                            slot.Filled = false;
                            slot.AssignedUnitId = -1;
                            break;
                        }
                plan.RogueUnitIds.Remove(unit.InstanceId);
            }
            unit.SetAIMaintenanceActive(true);
            Debug.Log($"{TL("Repair")} {unit.InstanceId} entra em reparo " +
                      $"hp={unit.CurrentHP} fuel={unit.CurrentFuel}/{unit.GetMaxFuel()} " +
                      $"ammo={unit.CurrentAmmo}/{unit.GetMaxAmmo()} sessao={sessionCount}");
        }
        else if (unit.IsUnderRepair && !anyTrigger && unit.CurrentHP >= data.repairRecoverHpAbove)
        {
            unit.SetIsUnderRepair(false);
            unit.SetAIMaintenanceActive(false);
            Debug.Log($"{TL("Repair")} {unit.InstanceId} saiu do reparo hp={unit.CurrentHP}");
        }
    }

    private static int IncrementRepairActivationCount(TeamId team)
    {
        repairActivationsThisSessionByTeam.TryGetValue(team, out int count);
        count++;
        repairActivationsThisSessionByTeam[team] = count;
        return count;
    }

    public static int GetRepairActivationCountThisSession(TeamId team)
    {
        repairActivationsThisSessionByTeam.TryGetValue(team, out int count);
        return count;
    }

    private static bool EvaluateRepairTriggers(UnitManager unit, UnitData data)
    {
        if (data.repairTriggerHpBelow > 0 && unit.CurrentHP <= data.repairTriggerHpBelow)
            return true;
        if (data.repairTriggerAutonomyPct > 0 &&
            unit.CurrentFuel * 100f / unit.GetMaxFuel() <= data.repairTriggerAutonomyPct)
            return true;
        if (data.repairTriggerAmmoEnabled)
        {
            System.Collections.Generic.IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
            for (int i = 0; i < weapons.Count; i++)
            {
                UnitEmbarkedWeapon rw = weapons[i];
                if (rw == null) continue;
                int baseAmmo = (data.embarkedWeapons != null && i < data.embarkedWeapons.Count
                                && data.embarkedWeapons[i] != null)
                    ? data.embarkedWeapons[i].squadAmmunition : 0;
                if (baseAmmo <= 0) continue; // arma sem ammo base não é rastreada
                float ammoPct = rw.squadAmmunition * 100f / baseAmmo;
                if (ammoPct <= data.repairTriggerAmmoPct)
                    return true;
            }
        }
        return false;
    }

    private PlayerAction DecideUnderRepairAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        // Logistics unit in repair while towing field artillery → drop it off at a safe construction first.
        if (IsPrimaryLogisticsUnit(unit) && HasTransportCargo(unit))
        {
            List<UnitManager> towPassengers = CollectPassengers(unit);
            UnitManager artPassenger = towPassengers.Find(p => IsFireSupportUnit(p));
            if (artPassenger != null)
            {
                Vector3Int towFrom = unit.CurrentCellPosition; towFrom.z = 0;
                Dictionary<Vector3Int, List<Vector3Int>> towPaths = BuildLogisticsPaths(unit);
                HashSet<Vector3Int> towOccupied = BuildOccupied(unit);
                PlayerAction dropOff = TryDropArtilleryAtSafeConstruction(
                    unit, artPassenger, towPassengers, snapshot, towFrom, towPaths, towOccupied);
                if (dropOff != null) return dropOff;
            }
        }

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        TeamId aiTeam = snapshot.AITeam;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        bool aircraftRepair = unit.GetAircraftType() != AircraftType.None;
        HashSet<Vector3Int> occupied = aircraftRepair ? BuildAirOccupied(unit) : BuildOccupied(unit);
        HashSet<Vector3Int> repairDestinationOccupied = aircraftRepair ? BuildOccupied(unit) : occupied;
        ConstructionManager currentBldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell);

        TeamObjectivePlan capBlockPlan = ObjectiveManager.GetPlanForTeam(aiTeam);
        bool isBlockingCapTarget = capBlockPlan != null && IsBlockingCaptureTarget(unit, capBlockPlan, aiTeam);
        if (isBlockingCapTarget)
            Debug.Log($"{TL("Repair")} {unit.InstanceId} em {fromCell} bloqueia capturador designado — priorizando saida do predio");

        if (paths == null || paths.Count == 0)
        {
            if (TryBuildRepairLastStandAttack(unit, aiTeam, fromCell, currentBldg, paths, occupied, out PlayerAction noPathLastStand))
                return noPathLastStand;

            return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
        }

        // EVAC: if in danger, try to board a nearby empty transporter before walking back alone.
        if (HasNearbyVisibleEnemy(fromCell, aiTeam, DefenseEnemyRange))
        {
            PlayerAction evacEmbark = TryEvacEmbarkAction(unit, aiTeam, fromCell, paths);
            if (evacEmbark != null) return evacEmbark;
        }

        // 1. Prédio conquistado: verifica segurança e presença de substituto
        if (currentBldg != null && currentBldg.IsCapturable
            && currentBldg.TeamId == aiTeam && currentBldg.CurrentCapturePoints >= currentBldg.CapturePointsMax)
        {
            bool safe = IsRepairConstructionSectorSafe(currentBldg, aiTeam)
                && !HasNearbyVisibleEnemy(fromCell, aiTeam, DefenseEnemyRange);
            if (safe && !isBlockingCapTarget)
            {
                // While waiting for repair, a logistics unit can still receive a factory transfer.
                if (IsPrimaryLogisticsUnit(unit)
                    && TryBuildLogisticsTransferReceiveAction(unit, snapshot, fromCell, paths, out PlayerAction transferAction, out string transferReason))
                {
                    Debug.Log($"{TL("Repair")} {unit.InstanceId} aguarda reparo + transferência logística {transferReason}");
                    return transferAction;
                }

                Debug.Log($"{TL("Repair")} {unit.InstanceId} aguarda reparo em {fromCell} (conquistado, setor seguro)");
                return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
            }

            // Com ameaça: só sai se houver aliado saudável próximo que pode substituir
            bool hasReplacement = false;
            foreach (UnitManager ally in UnitManager.AllActive)
            {
                if (ally == unit || ally.TeamId != aiTeam || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair) continue;
                Vector3Int ac = ally.CurrentCellPosition; ac.z = 0;
                if (SectorManager.HexDistance(ac, fromCell) <= DefenseEnemyRange) { hasReplacement = true; break; }
            }
            if (!hasReplacement && !isBlockingCapTarget)
            {
                // Sem substituto: defende o prédio enquanto aguarda reparo
                if (HasAttackTargetAtCurrentPos(unit))
                {
                    var defBuf = new List<PodeMirarTargetOption>();
                    PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                        SensorMovementMode.MoveuParado, defBuf);
                    UnitManager defTarget = null; float defPri = float.MinValue;
                    foreach (PodeMirarTargetOption opt in defBuf)
                    {
                        if (opt?.targetUnit == null) continue;
                        if (!PassesAttackDecision(unit, opt.targetUnit, fromCell, true, out _)) continue;
                        Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                        float p = AttackTargetPriority(tc, fromCell);
                        if (p > defPri) { defPri = p; defTarget = opt.targetUnit; }
                    }
                    if (defTarget != null)
                    {
                        Vector3Int dtc = defTarget.CurrentCellPosition; dtc.z = 0;
                        Debug.Log($"{TL("Repair")} {unit.InstanceId} segura {fromCell} sem substituto — ataca {defTarget.UnitDisplayName}#{defTarget.InstanceId}");
                        return BuildAttackBatch(unit, aiTeam, fromCell, fromCell, defTarget.InstanceId.ToString(), dtc);
                    }
                }
                Debug.Log($"{TL("Repair")} {unit.InstanceId} segura {fromCell} sem substituto");
                return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
            }
        }

        // 2. Fusão: libera o hex e recupera a unidade ao mesmo tempo
        // Scoring: candidato em repCell defensivo (+20) > em prédio (+10) > campo (0); desempate por HP combinado
        if (unit.TryGetUnitData(out UnitData fuseData) && fuseData.fuseWhileInRepair)
        {
            var defensiveRepCells = new HashSet<Vector3Int>();
            TeamObjectivePlan fusePlan = ObjectiveManager.GetPlanForTeam(aiTeam);
            if (fusePlan != null)
                foreach (SectorObjective obj in fusePlan.Objectives)
                {
                    if (obj.Status != ObjectiveStatus.Defending) continue;
                    if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo di)) continue;
                    Vector3Int rc = di.RepresentativeCell; rc.z = 0;
                    defensiveRepCells.Add(rc);
                }

            int totalMovement = Mathf.Max(0, unit.RemainingMovementPoints);
            var fuseOptions = new List<PodeFundirOption>();
            Vector3Int bestFuseCell = Vector3Int.zero;
            PodeFundirOption bestFuseOpt = null;
            float bestFuseScore = float.MinValue;

            foreach (Vector3Int cell in paths.Keys)
            {
                if (occupied.Contains(cell)) continue;

                List<Vector3Int> pathToCell = paths[cell];
                int costToCell = pathToCell != null && pathToCell.Count > 0
                    ? Mathf.Max(0, UnitMovementPathRules.CalculateAutonomyCostForPath(
                        boardTilemap, unit, pathToCell, terrainDatabase,
                        applyOperationalAutonomyModifier: false))
                    : 0;
                int remainingAfterMove = Mathf.Max(0, totalMovement - costToCell);

                fuseOptions.Clear();
                bool canFuse = PodeFundirSensor.CollectOptions(unit, boardTilemap, terrainDatabase,
                    remainingAfterMove, fuseOptions, out _, fromCell: cell);
                Debug.Log($"[Repair] fusão de {cell} mov={remainingAfterMove} canFuse={canFuse} opts={fuseOptions.Count}");
                if (!canFuse) continue;

                foreach (PodeFundirOption opt in fuseOptions)
                {
                    if (opt?.candidateUnit == null) continue;
                    if (opt.candidateUnit.CurrentHP + unit.CurrentHP > 10)
                    {
                        Debug.Log($"[Repair] skip fusão {opt.candidateUnit.InstanceId} hp={unit.CurrentHP}+{opt.candidateUnit.CurrentHP}>10");
                        continue;
                    }
                    Vector3Int cc = opt.candidateUnit.CurrentCellPosition; cc.z = 0;
                    float score = 0f;
                    if (defensiveRepCells.Contains(cc)) score += 20f;
                    else
                    {
                        ConstructionManager candBldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cc);
                        if (candBldg != null && candBldg.IsCapturable) score += 10f;
                    }
                    score += opt.candidateUnit.CurrentHP + unit.CurrentHP;

                    if (score > bestFuseScore) { bestFuseScore = score; bestFuseOpt = opt; bestFuseCell = cell; }
                }
            }

            if (bestFuseOpt != null)
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} fusão oportunista com " +
                          $"{bestFuseOpt.candidateUnit.InstanceId} hp={unit.CurrentHP}+{bestFuseOpt.candidateUnit.CurrentHP}" +
                          $" via {bestFuseCell} (score={bestFuseScore:F0})");
                return BuildMergeBatch(unit, aiTeam, fromCell, bestFuseCell, bestFuseOpt.candidateUnit, paths);
            }
        }

        // 3. Área home (base/HQ): reparo pode marchar no setor mas DEVE lutar — sem filtro de sobrevivência.
        // Fire support units skip this when in open field — they must reach a construction first.
        // Se prioritizeDpqAtBattle, tenta se mover para célula de maior DPQ antes de atacar.
        bool fireSupportInOpenField = IsFireSupportUnit(unit)
            && (currentBldg == null || currentBldg.TeamId != aiTeam);
        if (!fireSupportInOpenField && IsRepairUnitInOwnHomeArea(snapshot, fromCell, aiTeam))
        {
            bool repairPreferDpq = unit.TryGetUnitData(out UnitData repairUd)
                && repairUd != null && repairUd.prioritizeDpqAtBattle;

            UnitManager homeTarget = null;
            Vector3Int homeAttackCell = fromCell;
            float homeBestScore = float.MinValue;
            var homeCandidateBuf = new List<PodeMirarTargetOption>();

            // When preferDpq, evaluate all reachable home-area cells; otherwise only current cell.
            System.Collections.Generic.IEnumerable<Vector3Int> homeCells = repairPreferDpq && paths != null
                ? (System.Collections.Generic.IEnumerable<Vector3Int>)paths.Keys
                : new[] { fromCell };

            foreach (Vector3Int rawAttackFrom in homeCells)
            {
                Vector3Int attackFrom = rawAttackFrom; attackFrom.z = 0;
                bool staysInPlace = attackFrom == fromCell;
                if (!staysInPlace && occupied.Contains(attackFrom)) continue;
                if (repairPreferDpq && !staysInPlace
                    && !IsRepairUnitInOwnHomeArea(snapshot, attackFrom, aiTeam)) continue;

                SensorMovementMode homeMode = staysInPlace
                    ? SensorMovementMode.MoveuParado
                    : SensorMovementMode.MoveuAndando;
                homeCandidateBuf.Clear();
                if (!PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                        homeMode, homeCandidateBuf, fromCell: attackFrom))
                    continue;

                float dpqBonus = repairPreferDpq ? GetTerrainDpqPontos(attackFrom) * 500f : 0f;
                float movPenalty = staysInPlace ? 0f : GetPathStepCount(paths, attackFrom) * 10f;

                foreach (PodeMirarTargetOption opt in homeCandidateBuf)
                {
                    if (opt?.targetUnit == null) continue;
                    Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                    float p = AttackTargetPriority(tc, attackFrom) + dpqBonus - movPenalty;
                    if (p > homeBestScore)
                    {
                        homeBestScore = p;
                        homeTarget = opt.targetUnit;
                        homeAttackCell = attackFrom;
                    }
                }
            }

            if (homeTarget != null)
            {
                Vector3Int dtc = homeTarget.CurrentCellPosition; dtc.z = 0;
                Debug.Log($"{TL("Repair")} {unit.InstanceId} area home — DEVE lutar: ataca {homeTarget.UnitDisplayName}#{homeTarget.InstanceId} de {homeAttackCell} (preferDpq={repairPreferDpq}) antes de reparar");
                return BuildAttackBatch(unit, aiTeam, fromCell, homeAttackCell, homeTarget.InstanceId.ToString(), dtc, paths);
            }
        }

        // 4. Marcha para a construção aliada mais próxima desocupada (não defensiva)
        // Exclui: célula atual + repCells de objetivos defensivos ativos
        // Mordiscada en route: HP above trigger level (fuel-only repair) — stationary shot, no deviation.
        if (!fireSupportInOpenField && !IsRepairUnitInOwnHomeArea(snapshot, fromCell, aiTeam)
            && unit.TryGetUnitData(out UnitData routeData) && routeData != null)
        {
            bool hpOkForAttack = routeData.repairTriggerHpBelow <= 0 || unit.CurrentHP > routeData.repairTriggerHpBelow;
            bool hasAmmo = false;
            {
                var ws = unit.GetEmbarkedWeapons();
                for (int wi = 0; wi < ws.Count; wi++)
                    if (ws[wi] != null && ws[wi].squadAmmunition > 0) { hasAmmo = true; break; }
            }
            if (hpOkForAttack && hasAmmo && HasAttackTargetAtCurrentPos(unit))
            {
                var routeBuf = new List<PodeMirarTargetOption>();
                PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                    SensorMovementMode.MoveuParado, routeBuf);
                UnitManager routeTarget = null;
                float routeBestScore = float.MinValue;
                foreach (PodeMirarTargetOption opt in routeBuf)
                {
                    if (opt?.targetUnit == null) continue;
                    if (!PassesAttackDecision(unit, opt.targetUnit, fromCell, false, out _)) continue;
                    Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                    float p = AttackTargetPriority(tc, fromCell)
                        + Mathf.Max(0, 20 - opt.targetUnit.CurrentHP) * 25f
                        - opt.distance * 5f;
                    if (p > routeBestScore) { routeBestScore = p; routeTarget = opt.targetUnit; }
                }
                if (routeTarget != null)
                {
                    Vector3Int dtc = routeTarget.CurrentCellPosition; dtc.z = 0;
                    Debug.Log($"{TL("Repair")} {unit.InstanceId} mordiscada en route — ataca {routeTarget.UnitDisplayName}#{routeTarget.InstanceId} de {fromCell} hp={unit.CurrentHP}>{routeData.repairTriggerHpBelow}");
                    return BuildAttackBatch(unit, aiTeam, fromCell, fromCell, routeTarget.InstanceId.ToString(), dtc);
                }
            }
        }

        if (TryBuildRepairLastStandAttack(unit, aiTeam, fromCell, currentBldg, paths, occupied, out PlayerAction lastStandAction))
            return lastStandAction;

        var occupiedForRepair = new HashSet<Vector3Int>(repairDestinationOccupied) { fromCell };
        TeamObjectivePlan repPlan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (repPlan != null)
            foreach (SectorObjective obj in repPlan.Objectives)
            {
                if (obj.Status != ObjectiveStatus.Defending) continue;
                // Base and HQ sectors must never be blocked for repair — skip at sector level
                // so units can always route back to them, regardless of cell lookup results.
                if (ConstructionSectorHelper.IsBase(obj.Sector)) continue;
                if (snapshot.MyHQ != null && snapshot.MyHQ.Sector == obj.Sector) continue;
                if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)) continue;
                Vector3Int rc = defInfo.RepresentativeCell; rc.z = 0;
                ConstructionManager reservedConstruction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, rc);
                if (IsRepairHomeConstruction(reservedConstruction, aiTeam)) continue;
                occupiedForRepair.Add(rc);
            }

        ConstructionManager repairDest = FindRepairConstruction(unit, fromCell, aiTeam, occupiedForRepair);
        if (repairDest == null)
        {
            if (TryDecideRepairFallbackToHQ(unit, snapshot, fromCell, paths, occupied, out PlayerAction hqFallback))
                return hqFallback;

            Debug.Log($"{TL("Repair")} {unit.InstanceId} sem destino de reparo e sem HQ válido — conservador");
            return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
        }

        Vector3Int destCell = repairDest.CurrentCellPosition; destCell.z = 0;

        if (fromCell == destCell)
        {
            Debug.Log($"{TL("Repair")} {unit.InstanceId} aguarda reparo em {fromCell}");
            return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
        }

        if (ShouldPreferRepairEvac(unit, fromCell, aiTeam))
        {
            TeamObjectivePlan repairPlan = ObjectiveManager.GetPlanForTeam(aiTeam);
            PlayerAction evacAction = TryEvacEmbarkOrApproachAction(unit, snapshot, repairPlan, aiTeam, fromCell, paths);
            if (evacAction != null)
                return evacAction;
        }

        // Avança para o destino: mínima distância hex + mínima ameaça
        // Pass HQ as secondary anchor — cells blocked relative to the primary target
        // may still make positive progress toward HQ, breaking the deadlock.
        Vector3Int? hqAlt = null;
        if (snapshot.MyHQ != null)
        {
            Vector3Int hc = snapshot.MyHQ.CurrentCellPosition; hc.z = 0;
            if (hc != destCell) hqAlt = hc;
        }
        Vector3Int bestStep = FindRepairApproachStep(
            unit, aiTeam, fromCell, destCell, repairDest, paths, occupied, hqAlt);

        Debug.Log($"{TL("Repair")} {unit.InstanceId} marcha para reparo em {destCell} via {bestStep}");
        return BuildMoveBatch(unit, aiTeam, fromCell, bestStep, paths);
    }

    private bool TryBuildRepairLastStandAttack(
        UnitManager unit,
        TeamId aiTeam,
        Vector3Int fromCell,
        ConstructionManager currentConstruction,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (unit == null)
            return false;

        if (currentConstruction != null && currentConstruction.TeamId == aiTeam)
            return false;

        if (HasAnyUnoccupiedRepairMove(fromCell, paths, occupied))
            return false;

        var targets = new List<PodeMirarTargetOption>();
        if (!PodeMirarSensor.CollectTargets(
                unit,
                boardTilemap,
                terrainDatabase,
                SensorMovementMode.MoveuParado,
                targets,
                fromCell: fromCell) || targets.Count == 0)
            return false;

        UnitManager bestTarget = null;
        float bestPriority = float.MinValue;
        foreach (PodeMirarTargetOption opt in targets)
        {
            if (opt?.targetUnit == null) continue;
            if (!PassesAttackDecision(unit, opt.targetUnit, fromCell, true, out _)) continue;

            Vector3Int targetCell = opt.targetUnit.CurrentCellPosition;
            targetCell.z = 0;
            float priority = AttackTargetPriority(targetCell, fromCell) * 1000f
                + Mathf.Max(0, 20 - opt.targetUnit.CurrentHP) * 25f
                - opt.distance * 5f
                - opt.targetUnit.InstanceId * 0.001f;

            if (priority > bestPriority)
            {
                bestPriority = priority;
                bestTarget = opt.targetUnit;
            }
        }

        if (bestTarget == null)
            return false;

        Vector3Int bestTargetCell = bestTarget.CurrentCellPosition;
        bestTargetCell.z = 0;
        Debug.Log($"{TL("Repair")} {unit.InstanceId} cercado fora de construcao aliada - ultimo recurso: ataca {bestTarget.UnitDisplayName}#{bestTarget.InstanceId}");
        action = BuildAttackBatch(unit, aiTeam, fromCell, fromCell, bestTarget.InstanceId.ToString(), bestTargetCell, paths);
        return true;
    }

    private static bool HasAnyUnoccupiedRepairMove(
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (paths == null || paths.Count == 0)
            return false;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;

            if (occupied != null && occupied.Contains(cell))
                continue;

            return true;
        }

        return false;
    }

    private Vector3Int FindRepairApproachStep(
        UnitManager unit,
        TeamId aiTeam,
        Vector3Int fromCell,
        Vector3Int destCell,
        ConstructionManager repairDest,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        Vector3Int? altDestCell = null)
    {
        if (paths == null || paths.Count == 0)
            return fromCell;

        bool homeRepair = IsRepairHomeConstruction(repairDest, aiTeam);
        bool destOccupied = occupied != null && occupied.Contains(destCell) && destCell != fromCell;
        if (!destOccupied && paths.ContainsKey(destCell))
            return destCell;

        Vector3Int bestStep = ScoreRepairApproach(unit, aiTeam, fromCell, destCell, paths, occupied, homeRepair);

        // Secondary anchor: if still stuck in a hotzone, retry scoring toward HQ (or altDest).
        // A cell that's -5 toward Mike can be +1 toward HQ — either direction breaks the deadlock.
        if (bestStep == fromCell
            && altDestCell.HasValue
            && altDestCell.Value != destCell
            && HasNearbyVisibleEnemy(fromCell, aiTeam, DefenseEnemyRange))
        {
            Vector3Int altDest = altDestCell.Value; altDest.z = 0;
            Vector3Int altStep = ScoreRepairApproach(unit, aiTeam, fromCell, altDest, paths, occupied, homeRepair: false);
            if (altStep != fromCell)
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} redirecionado p/ âncora secundária {altDest} via {altStep} (destino primário {destCell} bloqueado)");
                bestStep = altStep;
            }
        }

        // Last-resort: if still stuck and in a hotzone, flee to the minimum-threat reachable cell.
        if (bestStep == fromCell && HasNearbyVisibleEnemy(fromCell, aiTeam, DefenseEnemyRange))
        {
            Vector3Int fleeCell = fromCell;
            float lowestThreat = float.MaxValue;
            foreach (Vector3Int cell in paths.Keys)
            {
                if (cell == fromCell) continue;
                if (occupied != null && occupied.Contains(cell)) continue;
                float t = CalculateThreatLevel(cell, aiTeam);
                if (t < lowestThreat) { lowestThreat = t; fleeCell = cell; }
            }
            if (fleeCell != fromCell)
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} fuga de emergência — todas âncoras bloqueadas; foge p/ {fleeCell} (threat={lowestThreat:F0})");
                bestStep = fleeCell;
            }
        }

        return bestStep;
    }

    private Vector3Int ScoreRepairApproach(
        UnitManager unit, TeamId aiTeam, Vector3Int fromCell, Vector3Int target,
        Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied, bool homeRepair)
    {
        float fromDist = SectorManager.HexDistance(fromCell, target);
        bool fromRouteFound = TryCalculateRouteDistance(unit, fromCell, target, out float fromRouteDist);
        Vector3Int bestStep = fromCell;
        float bestScore = float.MinValue;
        int bestToolScore = int.MinValue;
        float bestToolNextDist = float.MaxValue;
        int bestToolMoveCost = int.MaxValue;
        float bestLegacyScore = float.MinValue;
        float bestThreat = float.MaxValue;
        bool bestRoadBonus = false;

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;

            float dist = SectorManager.HexDistance(cell, target);
            bool cellRouteFound = TryCalculateRouteDistance(unit, cell, target, out float routeDist);
            float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - routeDist : 0f;
            bool recoversMissingRoute = !fromRouteFound && cellRouteFound;
            // When recovering a missing route, credit progress as if we closed fromDist worth of gap
            // (treating "no route" as effectively infinite distance). Without this, -routeDist produces
            // a catastrophic score that always loses to staying put.
            float progress = recoversMissingRoute
                ? fromDist
                : (fromRouteFound && cellRouteFound) ? routeProgress : fromDist - dist;
            float recoveryBonus = recoversMissingRoute ? 5000f : 0f;
            float effectiveDist = cellRouteFound ? routeDist : dist;
            float threat = CalculateThreatLevel(cell, aiTeam);
            float pathCost = cell == fromCell ? 0f : GetPathStepCount(paths, cell);

            float threatMult = homeRepair ? 0f : 0.35f;
            float score =
                progress * 1200f
                - effectiveDist * 180f
                - pathCost * 4f
                - threat * ThreatWeight * threatMult
                + recoveryBonus;

            if (homeRepair && progress > 0f)
                score += 350f;
            if (cell == target)
                score += 10000f;

            bool hasToolScore = TryScoreToolRouteProgression(
                unit,
                fromCell,
                target,
                cell,
                paths[cell],
                occupied,
                out int toolScore,
                out float toolNextDist,
                out int toolMoveCost);

            int candidateToolScore = hasToolScore ? toolScore : int.MinValue;
            float candidateToolNextDist = hasToolScore ? toolNextDist : float.MaxValue;
            int candidateToolMoveCost = hasToolScore ? toolMoveCost : int.MaxValue;
            bool roadBonus = cell != fromCell
                && UnitMovementPathRules.DidUseRoadFullMoveBonus(boardTilemap, unit, paths[cell], terrainDatabase);

            float candidateScore = hasToolScore ? candidateToolScore * 1000f : score;
            if (roadBonus)
                candidateScore += 500f;
            if (cell == target)
                candidateScore += 25000f;
            if (homeRepair && progress > 0f)
                candidateScore += 350f;
            candidateScore += Mathf.Clamp(score, -5000f, 5000f) * 0.01f;
            candidateScore -= threat * ThreatWeight * threatMult;

            bool better =
                candidateScore > bestScore + 0.01f
                || (Mathf.Abs(candidateScore - bestScore) <= 0.01f && candidateToolNextDist < bestToolNextDist - 0.01f)
                || (Mathf.Abs(candidateScore - bestScore) <= 0.01f && Mathf.Abs(candidateToolNextDist - bestToolNextDist) <= 0.01f && candidateToolMoveCost < bestToolMoveCost)
                || (Mathf.Abs(candidateScore - bestScore) <= 0.01f && Mathf.Abs(candidateToolNextDist - bestToolNextDist) <= 0.01f && candidateToolMoveCost == bestToolMoveCost && threat < bestThreat);

            if (better)
            {
                bestScore = candidateScore;
                bestToolScore = candidateToolScore;
                bestToolNextDist = candidateToolNextDist;
                bestToolMoveCost = candidateToolMoveCost;
                bestLegacyScore = score;
                bestThreat = threat;
                bestRoadBonus = roadBonus;
                bestStep = cell;
            }
        }

        if (bestStep != fromCell && bestToolScore != int.MinValue)
        {
            Debug.Log($"{TL("Repair")} {unit.InstanceId} tool-progress repair via {bestStep} target={target} " +
                      $"tool={bestToolScore} nextDist={bestToolNextDist:F1} moveCost={bestToolMoveCost} roadBonus={bestRoadBonus} final={bestScore:F0} legacy={bestLegacyScore:F0}");
        }

        return bestStep;
    }

    private bool TryDecideRepairHoldHomeDefense(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamId aiTeam,
        Vector3Int fromCell,
        out PlayerAction action)
    {
        action = null;
        if (!IsRepairUnitInThreatenedOwnHqArea(snapshot, fromCell, aiTeam))
            return false;

        if (HasAttackTargetAtCurrentPos(unit))
        {
            var targets = new List<PodeMirarTargetOption>();
            PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                SensorMovementMode.MoveuParado, targets);

            UnitManager bestTarget = null;
            float bestPriority = float.MinValue;
            foreach (PodeMirarTargetOption opt in targets)
            {
                if (opt?.targetUnit == null) continue;
                if (!PassesAttackDecision(unit, opt.targetUnit, fromCell, true, out _)) continue;

                Vector3Int targetCell = opt.targetUnit.CurrentCellPosition;
                targetCell.z = 0;
                float priority = AttackTargetPriority(targetCell, fromCell);
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestTarget = opt.targetUnit;
                }
            }

            if (bestTarget != null)
            {
                Vector3Int targetCell = bestTarget.CurrentCellPosition;
                targetCell.z = 0;
                Debug.Log($"{TL("Repair")} {unit.InstanceId} segura base/HQ em {fromCell} sob ameaca - ataca {bestTarget.UnitDisplayName}#{bestTarget.InstanceId}");
                action = BuildAttackBatch(unit, aiTeam, fromCell, fromCell, bestTarget.InstanceId.ToString(), targetCell);
                return true;
            }
        }

        Debug.Log($"{TL("Repair")} {unit.InstanceId} segura base/HQ em {fromCell} sob ameaca");
        action = BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
        return true;
    }

    private bool IsRepairUnitInThreatenedOwnHqArea(AIWorldSnapshot snapshot, Vector3Int fromCell, TeamId aiTeam)
    {
        if (snapshot == null || snapshot.MyHQ == null)
            return false;

        fromCell.z = 0;
        Vector3Int hqCell = snapshot.MyHQ.CurrentCellPosition;
        hqCell.z = 0;
        ConstructionSector hqSector = snapshot.MyHQ.Sector;

        ConstructionManager current = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell);
        if (current != null
            && current.Sector == hqSector
            && IsHomeDefenseThreatened(hqSector, aiTeam, HomeDefenseThreatRange))
            return true;

        if (SectorManager.HexDistance(fromCell, hqCell) <= HomeDefenseThreatRange
            && IsHomeDefenseThreatened(hqSector, aiTeam, HomeDefenseThreatRange))
            return true;

        return false;
    }

    private bool IsRepairUnitInOwnHomeArea(AIWorldSnapshot snapshot, Vector3Int fromCell, TeamId aiTeam)
    {
        fromCell.z = 0;

        ConstructionManager current = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, fromCell);
        if (current != null && current.TeamId == aiTeam
            && (current.IsPlayerHeadQuarter || ConstructionSectorHelper.IsBase(current.Sector)))
            return true;

        if (snapshot?.MyHQ != null)
        {
            Vector3Int hqCell = snapshot.MyHQ.CurrentCellPosition; hqCell.z = 0;
            if (SectorManager.HexDistance(fromCell, hqCell) <= HomeDefenseThreatRange)
                return true;
        }

        return false;
    }

    private bool TryDecideRepairFallbackToHQ(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (snapshot == null || snapshot.MyHQ == null || paths == null || paths.Count == 0)
            return false;

        TeamId aiTeam = snapshot.AITeam;
        Vector3Int hqCell = snapshot.MyHQ.CurrentCellPosition;
        hqCell.z = 0;

        bool safeNearHQ = SectorManager.HexDistance(fromCell, hqCell) <= DefenseEnemyRange
            && !HasNearbyVisibleEnemy(fromCell, aiTeam, DefenseEnemyRange)
            && !HasNearbyVisibleEnemy(hqCell, aiTeam, DefenseEnemyRange);
        if (safeNearHQ)
        {
            Debug.Log($"{TL("Repair")} {unit.InstanceId} aguarda nos arredores do HQ {hqCell} (sem ameaça)");
            action = BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
            return true;
        }

        Vector3Int bestStep = fromCell;
        float bestScore = float.MinValue;
        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell != fromCell && occupied.Contains(cell)) continue;

            float dist = SectorManager.HexDistance(cell, hqCell);
            float threat = CalculateThreatLevel(cell, aiTeam);
            float hqAdjacencyBonus = SectorManager.HexDistance(cell, hqCell) <= DefenseEnemyRange ? 25f : 0f;
            float score = -dist * 100f - threat * ThreatWeight + hqAdjacencyBonus;
            if (score > bestScore)
            {
                bestScore = score;
                bestStep = cell;
            }
        }

        Debug.Log($"{TL("Repair")} {unit.InstanceId} sem destino de reparo — retorna ao HQ {hqCell} via {bestStep}");
        action = BuildMoveBatch(unit, aiTeam, fromCell, bestStep, paths);
        return true;
    }

    private ConstructionManager FindRepairConstruction(UnitManager unit, Vector3Int fromCell, TeamId aiTeam, HashSet<Vector3Int> occupied)
    {
        ConstructionManager best = null;
        float bestScore = float.MinValue;
        bool preferAircraftFacility = unit != null && unit.GetAircraftType() != AircraftType.None;
        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            Vector3Int cc = c.CurrentCellPosition; cc.z = 0;
            float dist = SectorManager.HexDistance(fromCell, cc);
            bool isHomeRepair = IsRepairHomeConstruction(c, aiTeam);
            bool isAircraftFacility = IsAircraftRepairConstruction(c);
            if (c.TeamId != aiTeam)
            {
                Debug.Log($"[Repair] skip {cc} team={c.TeamId} (need {aiTeam}) dist={dist:F1}");
                continue;
            }
            if (c.CurrentCapturePoints < c.CapturePointsMax)
            {
                Debug.Log($"[Repair] skip {cc} cap={c.CurrentCapturePoints}/{c.CapturePointsMax} (incompleto) dist={dist:F1}");
                continue;
            }
            if (!isHomeRepair && !IsRepairConstructionSectorSafe(c, aiTeam))
            {
                Debug.Log($"[Repair] skip {cc} setor inseguro sector={c.Sector} dist={dist:F1}");
                continue;
            }
            bool occupiedCell = occupied.Contains(cc);
            if (occupiedCell && !isHomeRepair)
            {
                Debug.Log($"[Repair] skip {cc} ocupado dist={dist:F1}");
                continue;
            }
            if (occupiedCell && isHomeRepair)
                Debug.Log($"[Repair] home {cc} ocupado, mantendo como fallback de reparo dist={dist:F1}");

            bool safe = !HasNearbyVisibleEnemy(cc, aiTeam, DefenseEnemyRange);
            if (!safe && !isHomeRepair)
            {
                Debug.Log($"[Repair] skip {cc} unsafe (não-home) dist={dist:F1}");
                continue;
            }
            float score = -dist * 100f;
            if (safe) score += 500f;
            if (isHomeRepair) score += 25f;
            float aircraftCohesion = 0f;
            if (preferAircraftFacility)
            {
                aircraftCohesion = CalculateAircraftRepairCohesionScore(unit, cc, aiTeam);
                if (isAircraftFacility) score += 20000f + aircraftCohesion;
                else if (isHomeRepair) score -= 1000f;
            }
            if (occupiedCell && isHomeRepair) score -= 10000f;

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }
        if (best != null)
        {
            Vector3Int bc = best.CurrentCellPosition; bc.z = 0;
            string home = IsRepairHomeConstruction(best, aiTeam) ? " home" : string.Empty;
            string aircraft = IsAircraftRepairConstruction(best) ? " airport" : string.Empty;
            string safe = !HasNearbyVisibleEnemy(bc, aiTeam, DefenseEnemyRange) ? " safe" : string.Empty;
            string cohesion = unit != null && unit.GetAircraftType() != AircraftType.None
                ? $" coh={CalculateAircraftRepairCohesionScore(unit, bc, aiTeam):F0}"
                : string.Empty;
            Debug.Log($"[Repair] destino{home}{aircraft}{safe} selecionado {bc} dist={SectorManager.HexDistance(fromCell, bc):F1}{cohesion} score={bestScore:F0}");
        }
        return best;
    }

    private float CalculateAircraftRepairCohesionScore(UnitManager unit, Vector3Int cell, TeamId aiTeam)
    {
        cell.z = 0;

        int closeAllies = 0;
        int nearbyAllies = 0;
        float nearest = float.MaxValue;
        float weighted = 0f;

        foreach (UnitManager ally in UnitManager.AllActive)
        {
            if (ally == null || ally == unit || ally.TeamId != aiTeam)
                continue;
            if (ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float dist = SectorManager.HexDistance(cell, allyCell);
            nearest = Mathf.Min(nearest, dist);

            if (dist <= 4f)
            {
                closeAllies++;
                weighted += (5f - dist) * 110f;
            }
            else if (dist <= 8f)
            {
                nearbyAllies++;
                weighted += (9f - dist) * 30f;
            }
        }

        if (nearest == float.MaxValue)
            return -1800f;

        float isolationPenalty = nearest > 8f ? -2200f
            : nearest > 6f ? -1200f
            : nearest > 4f ? -450f
            : 0f;

        float closeBonus = Mathf.Min(closeAllies, 5) * 260f;
        float nearbyBonus = Mathf.Min(nearbyAllies, 6) * 80f;
        return weighted + closeBonus + nearbyBonus + isolationPenalty;
    }

    private static bool IsAircraftRepairConstruction(ConstructionManager construction)
    {
        return construction != null
            && construction.TryResolveConstructionData(out ConstructionData data)
            && data != null
            && data.allowAircraftTakeoffAndLanding;
    }

    private static bool IsRepairConstructionSectorSafe(ConstructionManager construction, TeamId aiTeam)
    {
        if (construction == null || construction.TeamId != aiTeam)
            return false;

        if (IsRepairHomeConstruction(construction, aiTeam))
            return true;

        if (!TryGetAnySectorInfo(construction.Sector, out SectorManager.SectorInfo info) || info == null)
            return false;

        return info.IsFullyControlled
            && !info.IsDisputed
            && !info.HasPartialCapture
            && info.ControllingTeam == aiTeam;
    }

    private static bool IsRepairHomeConstruction(ConstructionManager construction, TeamId aiTeam)
    {
        return construction != null
            && construction.TeamId == aiTeam
            && (construction.IsPlayerHeadQuarter || ConstructionSectorHelper.IsBase(construction.Sector));
    }

    // Like FindRepairConstruction but rejects any construction (including home/base) if enemies
    // are nearby — artillery should never be dropped in an actively invaded sector.
    private ConstructionManager FindSafeArtilleryDropConstruction(
        UnitManager unit, Vector3Int fromCell, TeamId aiTeam, HashSet<Vector3Int> occupied)
    {
        ConstructionManager best = null;
        float bestScore = float.MinValue;
        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.TeamId != aiTeam) continue;
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
            Vector3Int marchStep = FindRepairApproachStep(unit, aiTeam, fromCell, targetCell, dropTarget, paths, occupied);
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
            Vector3Int marchHome = FindRepairApproachStep(unit, aiTeam, fromCell, homeCell, homeTarget, paths, occupied);
            if (marchHome != fromCell)
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} T3 marcha → setor home {homeCell} com art #{artPassenger.InstanceId} via {marchHome}");
                return BuildMoveBatch(unit, aiTeam, fromCell, marchHome, paths);
            }
        }

        // ---- Tier 4: setor home lotado/inacessível → qualquer célula de baixa ameaça atrás das linhas ----
        Debug.Log($"{TL("Repair")} {unit.InstanceId} T4 último recurso atrás das linhas para art #{artPassenger.InstanceId}");
        return TryDropFireSupportConservative(unit, artPassenger, passengers, snapshot, plan, fromCell, paths, occupied);
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
                if (bldg == null || bldg.TeamId != aiTeam) continue;
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

    private ConstructionManager FindNearestHomeConstruction(Vector3Int fromCell, TeamId aiTeam)
    {
        ConstructionManager best = null;
        float bestDist = float.MaxValue;
        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.TeamId != aiTeam || !IsRepairHomeConstruction(c, aiTeam)) continue;
            Vector3Int cc = c.CurrentCellPosition; cc.z = 0;
            float dist = SectorManager.HexDistance(fromCell, cc);
            if (dist < bestDist) { bestDist = dist; best = c; }
        }
        return best;
    }
}

