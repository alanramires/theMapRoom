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

    private bool TryBuildRepairFireSupportHoldAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        HashSet<Vector3Int> occupied,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        if (unit == null || snapshot == null || !IsFireSupportUnit(unit))
            return false;

        Vector3Int anchor = fromCell;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        SectorObjective assigned = ResolveAssignedFireSupportObjective(unit, plan);
        if (assigned != null)
            anchor = ResolveFireSupportObjectiveAnchor(assigned, snapshot.AITeam, fromCell);

        return TryBuildBestFireSupportAttack(
            unit,
            snapshot,
            fromCell,
            null,
            occupied,
            anchor,
            defensiveContext: true,
            out action,
            out reason);
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

            if (TryBuildStationaryLogisticsSupplyAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction noPathSupply, out string noPathSupplyReason))
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} parado em reparo atende logistica {noPathSupplyReason}");
                return noPathSupply;
            }

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
            bool aircraftShouldSeekPreferredRepair = aircraftRepair
                && !IsPreferredAircraftRepairConstruction(currentBldg, aiTeam);
            if (safe && !isBlockingCapTarget && !aircraftShouldSeekPreferredRepair)
            {
                if (IsFireSupportUnit(unit)
                    && TryBuildRepairFireSupportHoldAttack(unit, snapshot, fromCell, occupied,
                        out PlayerAction fireSupportHoldAction, out string fireSupportHoldReason))
                {
                    Debug.Log($"{TL("Repair")} {unit.InstanceId} aguarda reparo em {fromCell} (conquistado, setor seguro) + dispara {fireSupportHoldReason}");
                    return fireSupportHoldAction;
                }

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
                if (TryBuildStationaryLogisticsSupplyAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction holdSupply, out string holdSupplyReason))
                {
                    Debug.Log($"{TL("Repair")} {unit.InstanceId} segura {fromCell} sem substituto e atende logistica {holdSupplyReason}");
                    return holdSupply;
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

        if (aircraftRepair
            && !HasUsableAircraftRepairConstruction(unit, fromCell, aiTeam, occupiedForRepair)
            && TryDecideAircraftRoadRecoveryFallback(unit, snapshot, fromCell, paths, occupied, out PlayerAction roadRecovery))
        {
            return roadRecovery;
        }

        ConstructionManager repairDest = FindRepairConstruction(unit, fromCell, aiTeam, occupiedForRepair);
        if (repairDest == null)
        {
            if (TryDecideRepairFallbackToHQ(unit, snapshot, fromCell, paths, occupied, out PlayerAction hqFallback))
                return hqFallback;

            if (TryBuildStationaryLogisticsSupplyAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction noDestSupply, out string noDestSupplyReason))
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} sem destino de reparo atende logistica parado {noDestSupplyReason}");
                return noDestSupply;
            }

            Debug.Log($"{TL("Repair")} {unit.InstanceId} sem destino de reparo e sem HQ válido — conservador");
            return BuildMoveBatch(unit, aiTeam, fromCell, fromCell);
        }

        Vector3Int destCell = repairDest.CurrentCellPosition; destCell.z = 0;

        if (fromCell == destCell)
        {
            if (TryBuildStationaryLogisticsSupplyAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction repairSupply, out string repairSupplyReason))
            {
                Debug.Log($"{TL("Repair")} {unit.InstanceId} aguarda reparo em {fromCell} e atende logistica {repairSupplyReason}");
                return repairSupply;
            }

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
            unit, aiTeam, fromCell, destCell, repairDest, paths, occupied, hqAlt, out bool usedEmergencyFlee);

        if (usedEmergencyFlee
            && TryBuildRepairBlockedAnchorsFightAction(
                unit,
                snapshot,
                fromCell,
                paths,
                occupied,
                out PlayerAction blockedAnchorsFight,
                out string blockedAnchorsFightReason))
        {
            Debug.Log($"{TL("Repair")} {unit.InstanceId} todas ancoras bloqueadas — luta ate o fim ({blockedAnchorsFightReason})");
            return blockedAnchorsFight;
        }

        Debug.Log($"{TL("Repair")} {unit.InstanceId} marcha para reparo em {destCell} via {bestStep}");
        return BuildMoveBatch(unit, aiTeam, fromCell, bestStep, paths);
    }

}

