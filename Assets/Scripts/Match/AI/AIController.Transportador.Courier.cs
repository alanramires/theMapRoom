using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Courier — transporter carrying passengers, delivering to objective
    // -------------------------------------------------------------------------

    private PlayerAction DecideTransportadorCourierAction(UnitManager unit, AIWorldSnapshot snapshot,
        Vector3Int assignedSectorTarget = default)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);

        List<UnitManager> passengers = CollectPassengers(unit);
        if (passengers.Count == 0)
        {
            Debug.LogWarning($"[AI] {TL("Transporte")} {unit.InstanceId} courier: cargo inconsistente, reverte para shuttle");
            return DecideRogueShuttleAction(unit, snapshot, plan);
        }

        UnitManager primaryPassenger = ResolvePrimaryPassenger(passengers, plan);
        bool primaryTargetFound = TryResolveCourierPassengerTarget(primaryPassenger, plan, snapshot, assignedSectorTarget, fromCell, out Vector3Int primaryTarget);
        if (!primaryTargetFound) primaryTarget = fromCell;
        int dropOffRange = IsFireSupportUnit(primaryPassenger) ? FireSupportDropOffRange : TransportDropOffRange;
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — passageiro #{primaryPassenger.InstanceId} alvo={primaryTarget} range={dropOffRange} distAtual={SectorManager.HexDistance(fromCell, primaryTarget):F0}h");

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        // EVAC mode: passenger is under repair → deliver to nearest safe repair location.
        UnitManager evacuee = passengers.Find(p => p.IsUnderRepair);
        if (evacuee != null)
            return DecideEvacCourierAction(unit, evacuee, passengers, snapshot, fromCell, paths, occupied);

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

        // Priority 1a — FireSupport passenger: first drive as far as this turn can toward
        // the assigned target, then choose the best legal landing cell from there.
        if (IsFireSupportUnit(primaryPassenger))
        {
            // Conservative transporter (logistics/supply truck): don't drive toward the artillery's
            // objective — the truck will never reach the front. Instead, drop the artillery at the
            // best safe rear-area cell reachable this turn (allied building > allies nearby > low threat).
            if (unit.TryGetUnitData(out UnitData towData) && towData != null && towData.playConservative)
                return TryDropFireSupportConservative(unit, primaryPassenger, passengers, snapshot, plan, fromCell, paths, occupied);

            SectorObjective fsObj = ResolveAssignedFireSupportObjective(primaryPassenger, plan);
            string fsSector = fsObj != null ? fsObj.Sector.ToString() : "?";
            float distToTarget = SectorManager.HexDistance(fromCell, primaryTarget);

            // Only skip the forward move if it would send the truck BACKWARD (redirect case:
            // truck is already on the objective cell, redirect moved it away → moveImprovement < 0).
            bool hasForwardMove = moveTarget != fromCell && moveImprovement >= 0f;

            if (hasForwardMove)
            {
                List<PodeDesembarcarOption> opts = SimulateDisembarkFromCell(unit, moveTarget);
                if (opts != null && opts.Count > 0)
                {
                    List<PodeDesembarcarOption> selected = SelectBestDisembarkPerPassenger(opts, passengers, plan, snapshot);
                    PodeDesembarcarOption primaryOpt = selected.Find(o => o.passengerUnit == primaryPassenger);
                    if (primaryOpt != null)
                    {
                        Vector3Int dc = primaryOpt.disembarkCell; dc.z = 0;
                        float dcDist = SectorManager.HexDistance(dc, primaryTarget);
                        // Drop if the artillery lands near the target OR if the truck itself
                        // is already close enough (artillery can walk the remaining distance).
                        float truckDistAfterMove = SectorManager.HexDistance(moveTarget, primaryTarget);
                        if (dcDist <= FireSupportDropOffRange || truckDistAfterMove <= FireSupportDropOffRange)
                        {
                            float score = ScoreCourierDisembarkOption(primaryPassenger, dc, primaryTarget, snapshot.AITeam,
                                dcDist, CalculateThreatLevel(dc, snapshot.AITeam));
                            paths.TryGetValue(moveTarget, out List<Vector3Int> fsMoved);
                            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — FireSupport #{primaryPassenger.InstanceId} setor={fsSector} avança+desembarca via {moveTarget} destDist={dcDist:F0}h truckDist={truckDistAfterMove:F0}h dpq={score:F0} → {primaryTarget}");
                            return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected, moveTarget, fsMoved);
                        }
                    }
                }
            }
            else
            {
                // If the truck cannot improve this turn, keep the artillery aboard unless
                // the landing cell is already close to the assigned sector.
                var currentOpts = new List<PodeDesembarcarOption>();
                if (PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, currentOpts) && currentOpts.Count > 0)
                {
                    List<PodeDesembarcarOption> selected = SelectBestDisembarkPerPassenger(currentOpts, passengers, plan, snapshot);
                    PodeDesembarcarOption primaryOpt = selected.Find(o => o.passengerUnit == primaryPassenger);
                    if (primaryOpt != null)
                    {
                        Vector3Int dc = primaryOpt.disembarkCell; dc.z = 0;
                        float dcDist = SectorManager.HexDistance(dc, primaryTarget);
                        // Also drop in place when the truck itself is within drop range.
                        if (dcDist <= FireSupportDropOffRange || distToTarget <= FireSupportDropOffRange)
                        {
                            float score = ScoreCourierDisembarkOption(primaryPassenger, dc, primaryTarget, snapshot.AITeam,
                                dcDist, CalculateThreatLevel(dc, snapshot.AITeam));
                            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — FireSupport #{primaryPassenger.InstanceId} setor={fsSector} desembarca no lugar destDist={dcDist:F0}h truckDist={distToTarget:F0}h dpq={score:F0} → {primaryTarget}");
                            return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected);
                        }
                    }
                }
            }
            // No disembark option available yet — fall through to Priority 3 (keep moving).
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — FireSupport #{primaryPassenger.InstanceId} setor={fsSector} sem opção de desembarque ainda dist={distToTarget:F0}h → {primaryTarget}");
        }
        else
        {
        // Priority 1b: move + disembark when moving brings the APC meaningfully closer
        // AND the simulated drop-off from moveTarget is within delivery range.
        if (moveTarget != fromCell && moveImprovement > 1f)
        {
            List<PodeDesembarcarOption> optionsFromMove = SimulateDisembarkFromCell(unit, moveTarget);
            if (optionsFromMove != null && optionsFromMove.Count > 0)
            {
                var optCells = string.Join(", ", optionsFromMove.ConvertAll(o => { var c = o.disembarkCell; c.z = 0; return $"{c}({SectorManager.HexDistance(c, primaryTarget):F0}h)"; }));
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} simDisembark from {moveTarget}: [{optCells}] → target={primaryTarget}");
                List<PodeDesembarcarOption> selectedFromMove =
                    SelectBestDisembarkPerPassenger(optionsFromMove, passengers, plan, snapshot);
                PodeDesembarcarOption primaryOpt = selectedFromMove.Count > 0
                    ? selectedFromMove.Find(o => o.passengerUnit == primaryPassenger) : null;
                if (primaryOpt != null)
                {
                    Vector3Int dc = primaryOpt.disembarkCell; dc.z = 0;
                    bool dcInRange = SectorManager.HexDistance(dc, primaryTarget) <= dropOffRange;
                    bool truckInRange = SectorManager.HexDistance(moveTarget, primaryTarget) <= dropOffRange;
                    if (dcInRange || truckInRange)
                    {
                        paths.TryGetValue(moveTarget, out List<Vector3Int> movePath);
                        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — move+desembarca {selectedFromMove.Count} passageiro(s) via {moveTarget} dc={dc} dcDist={SectorManager.HexDistance(dc, primaryTarget):F0}h truckDist={SectorManager.HexDistance(moveTarget, primaryTarget):F0}h → {primaryTarget}");
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
                    bool inRangeP2 = isStuck
                        || SectorManager.HexDistance(dc, primaryTarget) <= dropOffRange
                        || SectorManager.HexDistance(fromCell, primaryTarget) <= dropOffRange;
                    if (inRangeP2)
                    {
                        string reason = isStuck ? "bloqueado, libera carga" : $"desembarca para {primaryTarget}";
                        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — {reason} ({selected.Count} passageiro(s))");
                        return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected);
                    }
                }
            }
        }
        } // end else (non-FireSupport)

        // No combat with passengers aboard — delivering is the only priority.

        // Priority 3: move toward target
        float distRemaining = SectorManager.HexDistance(moveTarget, primaryTarget);
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — move para {moveTarget} alvo={primaryTarget} dist={distRemaining:F0}h passageiro=#{primaryPassenger.InstanceId}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

    // -------------------------------------------------------------------------
    // Conservative tow: drop FireSupport at best safe rear-area position
    // -------------------------------------------------------------------------

    private PlayerAction TryDropFireSupportConservative(
        UnitManager unit, UnitManager primaryPassenger,
        List<UnitManager> passengers,
        AIWorldSnapshot snapshot, TeamObjectivePlan plan,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        // Use enemy HQ as the "forward" direction reference for the main-line check.
        Vector3Int anchor = snapshot.EnemyHQ != null ? snapshot.EnemyHQ.CurrentCellPosition : fromCell;
        anchor.z = 0;

        float bestScore = float.MinValue;
        Vector3Int bestTransporterCell = fromCell;
        List<Vector3Int> bestTransporterPath = null;
        List<PodeDesembarcarOption> bestSelected = null;

        // Candidate transporter positions: current cell + all non-forward reachable cells.
        var candidateCells = new List<(Vector3Int cell, List<Vector3Int> path)> { (fromCell, null) };
        foreach (var kvp in paths)
        {
            Vector3Int c = kvp.Key; c.z = 0;
            if (c == fromCell || occupied.Contains(c)) continue;
            if (IsLogisticsForwardOfMainLine(unit, snapshot, c, anchor)) continue;
            candidateCells.Add((c, kvp.Value));
        }

        foreach (var (tCell, tPath) in candidateCells)
        {
            List<PodeDesembarcarOption> opts;
            if (tCell == fromCell)
            {
                opts = new List<PodeDesembarcarOption>();
                PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, opts);
            }
            else opts = SimulateDisembarkFromCell(unit, tCell);

            if (opts == null || opts.Count == 0) continue;

            // Score using the conservative metric (allied building, cohesion, threat).
            // Forward disembark cells are skipped.
            float cellBestScore = float.MinValue;
            foreach (PodeDesembarcarOption opt in opts)
            {
                if (opt.passengerUnit != primaryPassenger) continue;
                Vector3Int dc = opt.disembarkCell; dc.z = 0;
                if (IsLogisticsForwardOfMainLine(unit, snapshot, dc, anchor)) continue;
                float score = ScoreConservativeFireSupportDropOff(dc, snapshot);
                if (score > cellBestScore) cellBestScore = score;
            }
            if (cellBestScore <= float.MinValue || cellBestScore <= bestScore) continue;

            bestScore = cellBestScore;
            bestTransporterCell = tCell;
            bestTransporterPath = tPath;
            bestSelected = SelectBestDisembarkPerPassenger(opts, passengers, plan, snapshot);
        }

        if (bestSelected != null)
        {
            PodeDesembarcarOption primaryOpt = bestSelected.Find(o => o.passengerUnit == primaryPassenger);
            Vector3Int dropCell = primaryOpt != null ? primaryOpt.disembarkCell : Vector3Int.zero;
            dropCell.z = 0;
            if (bestTransporterCell == fromCell)
            {
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier conservador — desembarca FS #{primaryPassenger.InstanceId} @ {dropCell} score={bestScore:F0}");
                return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, bestSelected);
            }
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier conservador — move+desembarca FS #{primaryPassenger.InstanceId} via {bestTransporterCell} @ {dropCell} score={bestScore:F0}");
            return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, bestSelected, bestTransporterCell, bestTransporterPath);
        }

        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier conservador — sem drop-off seguro, aguarda");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    // Score a disembark cell for the conservative tow case.
    // Does NOT penalize distance from objective — rewards safety and allied presence.
    private float ScoreConservativeFireSupportDropOff(Vector3Int dc, AIWorldSnapshot snapshot)
    {
        float score = 0f;

        ConstructionManager building = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, dc);
        if (building != null && building.TeamId == snapshot.AITeam)
            score += 3000f;

        int allyCount = 0;
        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally.IsDead || ally.IsEmbarked) continue;
            Vector3Int ac = ally.CurrentCellPosition; ac.z = 0;
            if (SectorManager.HexDistance(ac, dc) <= 3f) allyCount++;
        }
        score += allyCount * 80f;

        score += GetTerrainDpqPontos(dc) * 40f;
        score -= CalculateThreatLevel(dc, snapshot.AITeam) * 120f;

        return score;
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

    private static UnitManager ResolvePrimaryPassenger(List<UnitManager> passengers, TeamObjectivePlan plan = null)
    {
        UnitManager best = passengers[0];
        int bestPriority = int.MaxValue;
        bool bestIsAssigned = false;
        foreach (UnitManager p in passengers)
        {
            if (!p.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
            int priority = (int)d.roles[0];
            bool isAssigned = plan != null && IsPassengerInPlanSlot(p, plan);
            bool isBetter = priority < bestPriority
                || (priority == bestPriority && isAssigned && !bestIsAssigned);
            if (isBetter) { bestPriority = priority; bestIsAssigned = isAssigned; best = p; }
        }
        return best;
    }

    private static bool IsPassengerInPlanSlot(UnitManager passenger, TeamObjectivePlan plan)
    {
        if (plan == null || passenger == null || plan.Objectives == null) return false;
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj.Slots == null) continue;
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && slot.AssignedUnitId == passenger.InstanceId) return true;
        }
        return false;
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
            bool targetFound = TryResolveCourierPassengerTarget(passenger, plan, snapshot, Vector3Int.zero, passenger.CurrentCellPosition, out Vector3Int target);
            PodeDesembarcarOption best = null;
            float bestDist = float.MaxValue;
            float bestThreat = float.MaxValue;

            foreach (PodeDesembarcarOption opt in options)
            {
                if (opt.passengerUnit != passenger) continue;
                Vector3Int dc = opt.disembarkCell; dc.z = 0;
                float dist = targetFound
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

    // Partial disembark: remove passengers whose best drop-off cell is outside dropOffRange
    // of their own objective. They stay aboard for delivery on the next leg.
    // When allowStuck is true (helicopter can't move), all passengers are released.
    private List<PodeDesembarcarOption> FilterDisembarkByTargetRange(
        List<PodeDesembarcarOption> options,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        int dropOffRange,
        bool allowStuck = false)
    {
        if (allowStuck || options.Count <= 1) return options;
        var filtered = new List<PodeDesembarcarOption>();
        foreach (PodeDesembarcarOption opt in options)
        {
            bool targetFound = TryResolveCourierPassengerTarget(
                opt.passengerUnit, plan, snapshot, Vector3Int.zero,
                opt.passengerUnit.CurrentCellPosition, out Vector3Int target);
            if (!targetFound) { filtered.Add(opt); continue; }
            Vector3Int dc = opt.disembarkCell; dc.z = 0;
            float dist = SectorManager.HexDistance(dc, target);
            if (dist <= dropOffRange)
                filtered.Add(opt);
            else if (IsRogueCapturerPassenger(opt.passengerUnit, plan)
                     && IsUsefulRogueDropCell(opt.passengerUnit, dc, snapshot, dropOffRange))
            {
                filtered.Add(opt);
                Debug.Log($"{TL("Transporte")} partial_disembark: #{opt.passengerUnit.InstanceId} rogue DESCE oportunista dc={dc} alvoOriginal={target} dist={dist:F0}h");
            }
            else
                Debug.Log($"{TL("Transporte")} partial_disembark: #{opt.passengerUnit.InstanceId} FICA — dc={dc} alvo={target} dist={dist:F0}h > {dropOffRange}h");
        }
        // Safety: never return empty — if every passenger is out of range keep all.
        // This prevents the helicopter from carrying cargo forever when it gets stuck.
        return filtered.Count > 0 ? filtered : options;
    }

    private bool IsRogueCapturerPassenger(UnitManager passenger, TeamObjectivePlan plan)
    {
        if (passenger == null || plan == null) return false;
        if (IsPassengerInPlanSlot(passenger, plan)) return false;
        if (!passenger.TryGetUnitData(out UnitData data) || data?.roles == null) return false;
        return data.roles.Contains(UnitRole.Capturador);
    }

    private bool IsUsefulRogueDropCell(UnitManager passenger, Vector3Int dropCell, AIWorldSnapshot snapshot, int range)
    {
        if (passenger == null || snapshot == null) return false;

        dropCell.z = 0;
        if (SimulateCaptureSensor(passenger, dropCell, out ConstructionManager immediate)
            && immediate != null
            && immediate.TeamId != snapshot.AITeam)
            return true;

        if (snapshot.EnemyBuildings != null)
        {
            foreach (ConstructionManager b in snapshot.EnemyBuildings)
            {
                if (b == null || !b.IsCapturable || b.TeamId == snapshot.AITeam) continue;
                Vector3Int bc = b.CurrentCellPosition; bc.z = 0;
                if (SectorManager.HexDistance(dropCell, bc) <= range)
                    return true;
            }
        }

        return false;
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
        float score = -distToAssignedTarget * 20f;

        // FireSupport units prioritize defensive position quality — they fight from where they land.
        if (IsFireSupportUnit(passenger))
            score += GetTerrainDpqPontos(disembarkCell) * 60f;

        if (SimulateCaptureSensor(passenger, disembarkCell, out ConstructionManager captureTarget))
        {
            Vector3Int captureCell = captureTarget.CurrentCellPosition; captureCell.z = 0;
            bool isAssignedTarget = assignedTarget != Vector3Int.zero && captureCell == assignedTarget;
            bool isNeutralOrEnemy = captureTarget.TeamId != aiTeam;
            score += isAssignedTarget ? 3000f : isNeutralOrEnemy ? 1800f : 900f;
        }

        return score;
    }

    // Returns true if a real target was resolved (including cell (0,0,0) which is a valid hex).
    // Returns false only when no target could be determined at all.
    private bool TryResolveCourierPassengerTarget(
        UnitManager passenger,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        Vector3Int assignedSectorTarget,
        Vector3Int fallbackCell,
        out Vector3Int resolvedTarget)
    {
        resolvedTarget = Vector3Int.zero;
        if (passenger == null) return false;
        if (assignedSectorTarget != Vector3Int.zero) assignedSectorTarget.z = 0;
        fallbackCell.z = 0;

        if (IsFireSupportUnit(passenger))
        {
            SectorObjective assignedFireSupport = ResolveAssignedFireSupportObjective(passenger, plan);
            if (assignedFireSupport != null)
            {
                // Do NOT use fallbackCell (truck position) as last resort — when the assigned
                // sector has no capturable building (already AI-controlled), passing the truck's
                // cell as fallback makes the truck believe it has already arrived, so it never drives.
                ConstructionManager tgt = FindCapturableInSector(assignedFireSupport.Sector, snapshot.AITeam);
                if (tgt != null)
                {
                    Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;
                    Debug.Log($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} setor={assignedFireSupport.Sector} capturable={tc}");
                    resolvedTarget = tc; return true;
                }
                if (TryGetAnySectorInfo(assignedFireSupport.Sector, out SectorManager.SectorInfo si))
                {
                    Vector3Int rc = si.RepresentativeCell; rc.z = 0;
                    Debug.Log($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} setor={assignedFireSupport.Sector} repCell={rc} (sem capturable)");
                    resolvedTarget = rc; return true;
                }
                Debug.LogWarning($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} setor={assignedFireSupport.Sector} sem sectorInfo");
            }

            if (assignedSectorTarget != Vector3Int.zero)
            { resolvedTarget = assignedSectorTarget; return true; }

            Vector3Int passengerCell = passenger.CurrentCellPosition;
            passengerCell.z = 0;
            if (TryFindTowDeliveryTarget(passenger, passengerCell, snapshot, plan, out Vector3Int towTarget))
            { resolvedTarget = towTarget; return true; }

            return false;
        }

        // Look up capturable in the passenger's assigned sector; do NOT fall back to
        // the sector RepresentativeCell — for already-captured sectors it equals the
        // truck's own starting position, causing a distance-0 disembark without moving.
        Vector3Int target = Vector3Int.zero;
        bool passengerHasPlanSlot = false;
        if (plan != null)
        {
            bool slotFound = false;
            foreach (SectorObjective obj in plan.Objectives)
            {
                if (slotFound) break;
                foreach (SlotNeed slot in obj.Slots)
                {
                    if (!slot.Filled || slot.AssignedUnitId != passenger.InstanceId) continue;
                    passengerHasPlanSlot = true;
                    ConstructionManager tgt = FindCapturableInSector(obj.Sector, snapshot.AITeam, fallbackCell);
                    if (tgt != null) { target = tgt.CurrentCellPosition; target.z = 0; }
                    else if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo si))
                    { target = si.RepresentativeCell; target.z = 0; }
                    Debug.Log($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} setor={obj.Sector} capturable={target} (fallback={fallbackCell})");
                    slotFound = true; break;
                }
            }
        }

        // Passenger without a plan slot is extra cargo. On an assigned transporter,
        // it follows the transporter's assigned sector instead of hijacking the route to HQ.
        if (!passengerHasPlanSlot && assignedSectorTarget != Vector3Int.zero)
        {
            resolvedTarget = assignedSectorTarget;
            return true;
        }

        // Rogue capturer — no plan slot. Head to the HQ sector and drop at the nearest
        // capturable building within it (could be a factory before the HQ itself).
        if (target == Vector3Int.zero && snapshot.EnemyHQ != null)
        {
            ConstructionManager tgt = FindCapturableInSector(snapshot.EnemyHQ.Sector, snapshot.AITeam, fallbackCell);
            if (tgt != null)
            {
                target = tgt.CurrentCellPosition; target.z = 0;
                Debug.Log($"{TL("Transporte")} PassengerTarget #{passenger.InstanceId} rogue → setor HQ capturable={target}");
            }
        }

        bool targetIsHQFallback = snapshot.EnemyHQ != null
            && target == snapshot.EnemyHQ.CurrentCellPosition;
        if ((target == Vector3Int.zero || targetIsHQFallback)
            && assignedSectorTarget != Vector3Int.zero)
            target = assignedSectorTarget;
        if (target == Vector3Int.zero && snapshot.EnemyHQ != null)
        { target = snapshot.EnemyHQ.CurrentCellPosition; target.z = 0; }
        if (target == Vector3Int.zero && snapshot.EnemyBuildings != null)
        {
            Vector3Int pCell = passenger.CurrentCellPosition; pCell.z = 0;
            float nearestDist = float.MaxValue;
            foreach (ConstructionManager eb in snapshot.EnemyBuildings)
            {
                if (eb == null) continue;
                Vector3Int ec = eb.CurrentCellPosition; ec.z = 0;
                float d = SectorManager.HexDistance(pCell, ec);
                if (d < nearestDist) { nearestDist = d; target = ec; }
            }
        }
        if (target != Vector3Int.zero) { resolvedTarget = target; return true; }
        return false;
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
