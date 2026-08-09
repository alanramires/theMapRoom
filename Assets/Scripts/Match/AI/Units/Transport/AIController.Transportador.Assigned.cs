using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Assigned transport — has a formal slot in a distant objective's plan
    // -------------------------------------------------------------------------

    private PlayerAction DecideAssignedTransportAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        SectorObjective assigned,
        bool hasCargo)
    {
        if (hasCargo)
        {
            // Deliver the cargo toward the assigned sector.
            // Pass the sector building as an override so the courier doesn't fall back to
            // enemy HQ when the passenger was picked up informally (no plan slot).
            ConstructionManager assignedBldg = FindCapturableInSector(assigned.Sector, snapshot.AITeam);
            Vector3Int assignedTarget = IsActiveRallyAssemblyObjective(assigned)
                ? ResolveRallyAssemblyAnchor(assigned, snapshot.AITeam, unit.CurrentCellPosition)
                : assignedBldg != null
                    ? (Vector3Int)(assignedBldg.CurrentCellPosition)
                    : Vector3Int.zero;
            if (assignedTarget != Vector3Int.zero) assignedTarget.z = 0;
            return DecideTransportadorCourierAction(unit, snapshot, assignedTarget);
        }

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        // Find the capturer unit this transport is supposed to ferry.
        // Discard the passenger if they're already close enough to walk — same threshold as
        // TryDecideCapturerEmbarkAction, so both sides agree on when transport is no longer needed.
        // Resolve o passageiro DESTE transportador distribuindo os capturadores needy do objetivo
        // entre TODOS os APCs designados a ele (1:1). Evita que 2 APCs do mesmo Bravo fixem o mesmo
        // capturador e ignorem o segundo. null = este APC nao recebe passageiro (mais APCs que
        // passageiros, ou todos perto/dispensados) -> cai no ramo "sem passageiro".
        UnitManager targetPassenger = ResolveAssignedPassengerForTransporter(unit, snapshot, plan, assigned);
        if (targetPassenger != null)
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — pareado com passageiro {targetPassenger.InstanceId}");

        bool preferNoMove = unit.TryGetUnitData(out UnitData assignedData) && assignedData.prioritizeDpqAtBattle;

        if (targetPassenger == null)
        {
            // Formal passenger unavailable (already acted or within walking distance).
            // Before pressuring the sector, check if there is ANY nearby pickup candidate
            // (e.g. a freshly-purchased capturer at the base waiting for a ride).
            // If found, act as shuttle toward them instead of rushing into combat.
            ConstructionManager sectorTarget = FindCapturableInSector(assigned.Sector, snapshot.AITeam);
            Vector3Int sectorCell = IsActiveRallyAssemblyObjective(assigned)
                ? ResolveRallyAssemblyAnchor(assigned, snapshot.AITeam, fromCell)
                : fromCell;
            if (!IsActiveRallyAssemblyObjective(assigned) && sectorTarget != null) { sectorCell = sectorTarget.CurrentCellPosition; sectorCell.z = 0; }

            if (TryDecideAssignedTransportLocalDefense(
                    unit, snapshot, plan, assigned, fromCell, paths, occupied, preferNoMove,
                    out PlayerAction localDefenseAction))
                return localDefenseAction;

            UnitManager nearbyCandidate = FindBestShuttleCandidate(unit, snapshot, plan, fromCell, out Vector3Int nearbyCell, assigned);
            if (nearbyCandidate != null)
            {
                Vector3Int objCell2 = ResolveUnitObjectiveCell(nearbyCandidate, plan, snapshot);
                if (objCell2 == Vector3Int.zero)
                    objCell2 = sectorCell;
                HashSet<Vector3Int> nearbyReachable =
                    BuildPassengerReachableSet(nearbyCandidate);
                Vector3Int shuttleMove = FindTransportShuttleMove(
                    unit, fromCell, nearbyCell, paths, occupied,
                    snapshot.AITeam, objCell2, nearbyReachable,
                    plan, assigned, paths);
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — sem passageiro formal, aguarda candidato {nearbyCandidate.InstanceId}@{nearbyCell} obj={objCell2} via {shuttleMove}");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, shuttleMove, paths);
            }

            // O passageiro formal pode já ter chegado ao objetivo enquanto ainda existem
            // capturadores sem pareamento em outros setores (ou ainda sem objetivo formal).
            // Nesse caso o APC deixa de pertencer exclusivamente ao setor concluído e volta
            // a operar como pickup global. Pressionar o objetivo antigo faria o transportador
            // fugir justamente de quem ainda precisa da carona.
            UnitManager globalCandidate =
                FindBestShuttleCandidate(unit, snapshot, plan, fromCell,
                    out Vector3Int globalCandidateCell, assignedSector: null);
            if (globalCandidate != null)
            {
                Vector3Int globalObjective =
                    ResolveUnitObjectiveCell(globalCandidate, plan, snapshot);
                HashSet<Vector3Int> globalReachable =
                    BuildPassengerReachableSet(globalCandidate);
                Vector3Int globalPickupMove = FindTransportShuttleMove(
                    unit, fromCell, globalCandidateCell, paths, occupied,
                    snapshot.AITeam, globalObjective, globalReachable,
                    plan, null, paths);
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — " +
                          $"passageiro formal concluido; retorna para passageiro global " +
                          $"{globalCandidate.InstanceId}@{globalCandidateCell} " +
                          $"obj={globalObjective} via {globalPickupMove}");
                return BuildMoveBatch(
                    unit, snapshot.AITeam, fromCell, globalPickupMove, paths);
            }

            // APC vazio designado a um RALLY, sem candidato fresco por perto (2h): em vez de campar
            // o anchor (empatando a artilharia de se posicionar atrás da linha) ou buscar capturador
            // já montado, volta pra perto da fábrica/HQ pra pegar a PRÓXIMA leva comprada. Os
            // capturadores já presentes montam a pé; o papel do APC aqui é trazer reforço da retaguarda.
            if (IsActiveRallyAssemblyObjective(assigned))
            {
                Vector3Int waitTarget = FindTransportWaitTarget(snapshot.AITeam, fromCell);
                Vector3Int waitMove = FindTransportShuttleMove(unit, fromCell, waitTarget, paths, occupied, snapshot.AITeam, waitTarget);
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — rally vazio sem leva nova; aguarda reforço perto da produção {waitTarget} via {waitMove}");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, waitMove, paths);
            }

            // Sem candidato perto (2h) e sem passageiro needy (todos walkable): em vez de pressionar
            // o objetivo vazio, VAI BUSCAR o capturador mais proximo do proprio plano — mesmo que
            // ele consiga ir a pe, a carona acelera. Util > marchar sozinho pro objetivo.
            UnitManager softPassenger = FindNearestPlanCapturer(assigned, snapshot.AITeam, fromCell);
            if (softPassenger != null)
            {
                Vector3Int softCell = softPassenger.CurrentCellPosition; softCell.z = 0;
                Vector3Int softObj = ResolveUnitObjectiveCell(softPassenger, plan, snapshot);
                HashSet<Vector3Int> softReachable =
                    BuildPassengerReachableSet(softPassenger);
                Vector3Int softMove = FindTransportShuttleMove(
                    unit, fromCell, softCell, paths, occupied,
                    snapshot.AITeam, softObj, softReachable,
                    plan, assigned, paths);
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — vai buscar capturador {softPassenger.InstanceId}@{softCell} (sem needy; carona antecipada) via {softMove}");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, softMove, paths);
            }

            Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — sem passageiro, pressiona {sectorCell}");
            Vector3Int sectorMove = FindTransportMove(unit, fromCell, sectorCell, paths, occupied, snapshot.AITeam);

            if (TryFindTransportBreakerAttack(unit, snapshot, fromCell, paths, occupied, sectorCell,
                    out Vector3Int attackCell, out UnitManager attackTarget, preferNoMove, plan))
            {
                Vector3Int targetCell = attackTarget.CurrentCellPosition; targetCell.z = 0;
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — ataca {attackTarget.InstanceId} via {attackCell}");
                return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                    attackTarget.InstanceId.ToString(), targetCell, paths);
            }

            // APC VAZIO nao avanca sozinho pra dentro de mais perigo (revelar fog / suicidar). Se
            // o passo escolhido aumenta a ameaca vs a posicao atual, segura a posicao em vez de
            // pressionar o objetivo sem carga e sem escolta.
            float curThreat = CalculateThreatLevel(fromCell, snapshot.AITeam);
            float moveThreat = CalculateThreatLevel(sectorMove, snapshot.AITeam);
            if (sectorMove != fromCell && moveThreat > curThreat + 0.01f)
            {
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — vazio NAO avanca pra ameaca (threat {moveThreat:F0} > atual {curThreat:F0}); segura posicao");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
            }

            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, sectorMove, paths);
        }

        // Park on the path to the objective: within ShuttlePickupRange of the passenger,
        // as close to the objective as possible so the journey starts immediately after boarding.
        // Use ResolveUnitObjectiveCell so the objective falls back to enemy HQ when the
        // sector capturable is gone (e.g. already taken by another AI unit this turn).
        Vector3Int passengerCell = targetPassenger.CurrentCellPosition; passengerCell.z = 0;
        Vector3Int objCell = ResolveUnitObjectiveCell(targetPassenger, plan, snapshot);
        HashSet<Vector3Int> passengerReachable =
            BuildPassengerReachableSet(targetPassenger);
        Vector3Int moveTarget = FindTransportShuttleMove(
            unit, fromCell, passengerCell, paths, occupied,
            snapshot.AITeam, objCell, passengerReachable,
            plan, assigned, paths);

        if (TryFindTransportBreakerAttack(unit, snapshot, fromCell, paths, occupied, passengerCell,
                out Vector3Int pickupAttackCell, out UnitManager pickupAttackTarget, preferNoMove, plan))
        {
            Vector3Int pickupTargetCell = pickupAttackTarget.CurrentCellPosition; pickupTargetCell.z = 0;
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — ataca {pickupAttackTarget.InstanceId} via {pickupAttackCell} (en route pickup {targetPassenger.InstanceId})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, pickupAttackCell,
                pickupAttackTarget.InstanceId.ToString(), pickupTargetCell, paths);
        }

        Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} — pickup {targetPassenger.InstanceId}@{passengerCell} via {moveTarget}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

    private bool TryDecideAssignedTransportLocalDefense(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        SectorObjective assigned,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        bool preferNoMove,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;

        if (!TryFindLocalTransportDefenseAnchor(unit, snapshot.AITeam, fromCell,
                out ConstructionManager defenseTarget, out UnitManager pressureEnemy, out string defenseReason))
            return false;

        Vector3Int defenseCell = defenseTarget.CurrentCellPosition;
        defenseCell.z = 0;

        if (TryFindTransportBreakerAttack(unit, snapshot, fromCell, paths, occupied, defenseCell,
                out Vector3Int attackCell, out UnitManager attackTarget, preferNoMove, plan))
        {
            Vector3Int targetCell = attackTarget.CurrentCellPosition;
            targetCell.z = 0;
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} - suspende carona antecipada: defende {defenseTarget.Sector} ({defenseReason}) atacando {attackTarget.InstanceId} via {attackCell}");
            action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                attackTarget.InstanceId.ToString(), targetCell, paths);
            return true;
        }

        if (pressureEnemy != null)
        {
            Vector3Int enemyCell = pressureEnemy.CurrentCellPosition;
            enemyCell.z = 0;
            Vector3Int moveCell = FindAssaultPressureMove(unit, snapshot, fromCell, enemyCell, paths, occupied, out string pressureReason);
            if (moveCell != fromCell && !IsTeamProductionBuilding(moveCell, snapshot.AITeam))
            {
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} assigned {assigned.Sector} - suspende carona antecipada: reforca {defenseTarget.Sector} ({defenseReason}) contra {pressureEnemy.InstanceId} via {moveCell} ({pressureReason})");
                action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
                return true;
            }
        }

        return false;
    }

    private bool TryFindLocalTransportDefenseAnchor(
        UnitManager unit,
        TeamId aiTeam,
        Vector3Int fromCell,
        out ConstructionManager bestTarget,
        out UnitManager bestEnemy,
        out string reason)
    {
        bestTarget = null;
        bestEnemy = null;
        reason = "";
        fromCell.z = 0;

        List<UnitManager> enemies = CollectVisibleAssaultEnemies(aiTeam);
        if (enemies == null || enemies.Count == 0)
            return false;

        float bestScore = float.MinValue;
        foreach (ConstructionManager building in ConstructionManager.AllActive)
        {
            if (building == null || !building.IsCapturable || building.SlotIndex != ResolveAISlotKey(aiTeam))
                continue;

            Vector3Int buildingCell = building.CurrentCellPosition;
            buildingCell.z = 0;

            float transportDist = CalculateRouteDistanceOrHex(unit, fromCell, buildingCell);
            if (transportDist > 7f)
                continue;

            UnitManager nearestEnemy = null;
            float nearestEnemyDist = float.MaxValue;
            int localEnemies = 0;
            float enemyHpPressure = 0f;

            foreach (UnitManager enemy in enemies)
            {
                if (enemy == null || enemy.IsDead || enemy.IsEmbarked || enemy.SlotIndex == ResolveAISlotKey(aiTeam))
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition;
                enemyCell.z = 0;
                float enemyDist = SectorManager.HexDistance(enemyCell, buildingCell);
                if (enemyDist > 4f)
                    continue;

                localEnemies++;
                enemyHpPressure += Mathf.Max(1, enemy.CurrentHP);
                if (enemyDist < nearestEnemyDist)
                {
                    nearestEnemyDist = enemyDist;
                    nearestEnemy = enemy;
                }
            }

            if (localEnemies <= 0)
                continue;

            float captureLoss = Mathf.Max(0, building.CapturePointsMax - building.CurrentCapturePoints);
            float score =
                localEnemies * 3000f
                + enemyHpPressure * 80f
                + captureLoss * 120f
                + (building.IsRallyPoint ? 700f : 0f)
                - transportDist * 260f
                - nearestEnemyDist * 120f
                - building.InstanceId * 0.001f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestTarget = building;
            bestEnemy = nearestEnemy;
            reason = $"inimigos={localEnemies} dist={transportDist:F1} capLoss={captureLoss:F0} enemyDist={nearestEnemyDist:F1}";
        }

        return bestTarget != null;
    }
    private bool ShouldLeaveAssignedPassengerOnLocalRallyPressure(
        UnitManager passenger,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int passengerCell,
        Vector3Int objectiveCell,
        out string reason)
    {
        reason = "";
        if (passenger == null || snapshot == null || passenger.HasActed || passenger.IsEmbarked)
            return false;

        passengerCell.z = 0;
        objectiveCell.z = 0;

        if (TryFindNearbyPassengerRallyCapture(passenger, snapshot, plan, passengerCell, out ConstructionManager localTarget, out Vector3Int localCell))
        {
            reason = $"captura local {localTarget.Sector}@{localCell}";
            return true;
        }

        if (TryFindNearbyUnheldPassengerRallyPressure(passengerCell, snapshot.AITeam, plan, out ConstructionManager pressureTarget))
        {
            reason = $"pressao local {pressureTarget.Sector}@{pressureTarget.CurrentCellPosition}";
            return true;
        }

        if (objectiveCell != Vector3Int.zero
            && IsPassengerForwardDeployed(passengerCell, snapshot.AITeam)
            && !HasSafeHeldRallyNearCell(objectiveCell, snapshot.AITeam, 6f))
        {
            reason = $"sem rally aliado seguro perto do objetivo {objectiveCell}";
            return true;
        }

        return false;
    }

    private bool TryFindNearbyPassengerRallyCapture(
        UnitManager passenger,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int passengerCell,
        out ConstructionManager bestTarget,
        out Vector3Int bestCell)
    {
        bestTarget = null;
        bestCell = passengerCell;
        if (passenger == null || snapshot == null)
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> passengerPaths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, passenger, Mathf.Max(0, passenger.RemainingMovementPoints), terrainDatabase);
        if (passengerPaths == null || passengerPaths.Count == 0)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(passenger);
        float bestScore = float.MinValue;
        foreach (Vector3Int rawCell in passengerPaths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != passengerCell && occupied.Contains(cell))
                continue;
            if (SectorManager.HexDistance(passengerCell, cell) > 3f)
                continue;
            if (!SimulateCaptureSensor(passenger, cell, out ConstructionManager target))
                continue;
            if (target == null || !target.IsCapturable || target.CapturePointsMax <= 0)
                continue;
            if (target.SlotIndex == snapshot.AISlotIndex && target.CurrentCapturePoints >= target.CapturePointsMax)
                continue;
            if (ShouldReserveOpportunisticCaptureForCloserUnit(passenger, snapshot.AITeam, cell, passengerPaths, out _))
                continue;

            bool rallyLike = target.IsRallyPoint || IsPlannedRallyAssemblySector(target.Sector, plan);
            float missingCapture = Mathf.Max(0, target.CapturePointsMax - target.CurrentCapturePoints);
            float score = missingCapture * 80f
                + (rallyLike ? 3000f : 0f)
                - SectorManager.HexDistance(passengerCell, cell) * 250f
                - CalculateThreatLevel(cell, snapshot.AITeam) * 45f
                - target.InstanceId * 0.001f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestTarget = target;
            bestCell = cell;
        }

        return bestTarget != null;
    }

    private bool IsPassengerForwardDeployed(Vector3Int passengerCell, TeamId aiTeam)
    {
        passengerCell.z = 0;
        Vector3Int home = FindTransportWaitTarget(aiTeam, passengerCell);
        home.z = 0;
        return SectorManager.HexDistance(passengerCell, home) > TransportDropOffRange + 1f;
    }

    private bool TryFindNearbyUnheldPassengerRallyPressure(
        Vector3Int passengerCell,
        TeamId aiTeam,
        TeamObjectivePlan plan,
        out ConstructionManager bestTarget)
    {
        bestTarget = null;
        passengerCell.z = 0;
        float bestScore = float.MinValue;

        for (int i = 0; i < ConstructionManager.AllActive.Count; i++)
        {
            ConstructionManager building = ConstructionManager.AllActive[i];
            if (building == null || !building.IsCapturable || building.CapturePointsMax <= 0)
                continue;
            if (building.SlotIndex == ResolveAISlotKey(aiTeam) && building.CurrentCapturePoints >= building.CapturePointsMax)
                continue;

            bool rallyLike = building.IsRallyPoint || IsPlannedRallyAssemblySector(building.Sector, plan);
            if (!rallyLike)
                continue;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            float distance = SectorManager.HexDistance(passengerCell, cell);
            if (distance > TransportDropOffRange + 1f)
                continue;

            float score = (TransportDropOffRange + 1f - distance) * 400f
                + Mathf.Max(0, building.CapturePointsMax - building.CurrentCapturePoints) * 40f
                - CalculateThreatLevel(cell, aiTeam) * 35f
                - building.InstanceId * 0.001f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestTarget = building;
        }

        return bestTarget != null;
    }

    private bool HasSafeHeldRallyNearCell(Vector3Int anchor, TeamId aiTeam, float maxDistance)
    {
        anchor.z = 0;
        for (int i = 0; i < ConstructionManager.AllActive.Count; i++)
        {
            ConstructionManager building = ConstructionManager.AllActive[i];
            if (building == null || !building.IsRallyPoint || building.SlotIndex != ResolveAISlotKey(aiTeam))
                continue;
            if (building.CurrentCapturePoints < building.CapturePointsMax)
                continue;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            if (SectorManager.HexDistance(anchor, cell) > maxDistance)
                continue;
            if (CalculateThreatLevel(cell, aiTeam) > 0f)
                continue;

            return true;
        }

        return false;
    }

    // Distribui os capturadores que REALMENTE precisam de carona entre os transportadores
    // designados ao MESMO objetivo (1:1) e devolve o passageiro DESTE transportador. Determinístico
    // (independe da ordem da Phase 2): transportadores em ordem de InstanceId pegam o passageiro
    // needy nao reivindicado mais proximo. Evita 2 APCs do mesmo Bravo brigando pelo mesmo
    // capturador e ignorando o segundo. Retorna null se este APC nao recebe passageiro.
    private UnitManager ResolveAssignedPassengerForTransporter(
        UnitManager transporter, AIWorldSnapshot snapshot, TeamObjectivePlan plan, SectorObjective assigned)
    {
        if (assigned?.Slots == null || transporter == null) return null;
        TeamId aiTeam = snapshot.AITeam;

        ConstructionManager objBldg = FindCapturableInSector(assigned.Sector, aiTeam);
        Vector3Int objCell = Vector3Int.zero;
        if (objBldg != null) { objCell = objBldg.CurrentCellPosition; objCell.z = 0; }
        else if (IsActiveRallyAssemblyObjective(assigned))
        {
            // Rally não tem capturável: o destino dos capturadores é o ANCHOR do rally. Sem isto,
            // objCell ficava zero, cost=999 e TODO capturador virava needy — fazendo o APC campar o
            // rally esperando quem já está coladinho (e monta a pé) em vez de buscar leva nova.
            Vector3Int anchorHint = transporter.CurrentCellPosition; anchorHint.z = 0;
            objCell = ResolveRallyAssemblyAnchor(assigned, aiTeam, anchorHint);
            objCell.z = 0;
        }
        bool hasObjCell = objCell != Vector3Int.zero;
        // Passageiros needy: capturadores do objetivo vivos, fora, sem ter agido e LONGE A PE
        // (alem da distancia de embarque) — quem chega a pe nao precisa de carona. NAO usar o
        // ShouldLeaveAssignedPassengerOnLocalRallyPressure aqui: ele e liberal demais (acha rally
        // ate ATRAS do capturador) e zerava o needy, deixando os APCs vazios.
        var passengers = new List<UnitManager>();
        var diag = new System.Text.StringBuilder();
        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!IsGroundTransportPassengerSlot(assigned, slot, aiTeam)) continue;
            if (!slot.Filled) { diag.Append($" cap[vazio]"); continue; }
            UnitManager cap = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (cap == null) { diag.Append($" cap#{slot.AssignedUnitId}[null]"); continue; }
            if (cap.IsEmbarked) { diag.Append($" cap#{cap.InstanceId}[embarcado]"); continue; }
            if (cap.HasActed) { diag.Append($" cap#{cap.InstanceId}[jaAgiu]"); continue; }
            Vector3Int capCell = cap.CurrentCellPosition; capCell.z = 0;
            if (IsPassengerAlreadyAtCaptureObjective(cap, snapshot.AITeam))
            {
                diag.Append($" cap#{cap.InstanceId}[jaNoObjetivo]");
                continue;
            }
            int transportThreshold =
                ResolvePassengerWalkWithoutTransportBudget(cap);
            int cost = hasObjCell ? TerrainCostToCell(cap, capCell, objCell, transportThreshold) : 999;
            if (hasObjCell && cost <= transportThreshold)
            {
                diag.Append($" cap#{cap.InstanceId}[perto cost={cost}<={transportThreshold}]");
                continue; // perto o bastante pra ir a pe
            }
            diag.Append($" cap#{cap.InstanceId}[NEEDY cost={cost}]");
            passengers.Add(cap);
        }
        Debug.Log($"{TL("Transporte")} matching {assigned.Sector} APC {transporter.InstanceId}: needy={passengers.Count} thr=2turnosPorUnidade obj={(objBldg != null ? objCell.ToString() : "null")} caps:{diag}");
        if (passengers.Count == 0) return null;

        // O farol e estavel para ESTE APC durante a Phase 2, mas nao possui o
        // passageiro. Outro APC pode escolher exatamente o mesmo P1.
        if (transportPickupBeacons.TryGetValue(transporter.InstanceId, out int prevPid))
        {
            foreach (UnitManager p in passengers)
                if (p.InstanceId == prevPid) return p;
            transportPickupBeacons.Remove(transporter.InstanceId);
        }

        Vector3Int tc = transporter.CurrentCellPosition; tc.z = 0;
        UnitManager best = null;
        float bestD = float.MaxValue;
        bool duplicatedBeacon = false;
        foreach (UnitManager p in passengers)
        {
            if (IsPickupBeaconedByOtherTransport(transporter, p))
                continue;
            Vector3Int pc = p.CurrentCellPosition; pc.z = 0;
            float d = SectorManager.HexDistance(tc, pc);
            if (d < bestD) { bestD = d; best = p; }
        }

        // Todos ja iluminados: duplica o melhor em vez de ficar parado.
        if (best == null)
        {
            duplicatedBeacon = true;
            foreach (UnitManager p in passengers)
            {
                Vector3Int pc = p.CurrentCellPosition; pc.z = 0;
                float d = SectorManager.HexDistance(tc, pc);
                if (d < bestD) { bestD = d; best = p; }
            }
        }

        if (best == null) return null;

        transportPickupBeacons[transporter.InstanceId] = best.InstanceId;
        Debug.Log($"{TL("Transporte")} matching {assigned.Sector}: APC {transporter.InstanceId}@{tc} segue farol passageiro {best.InstanceId} dist={bestD:F0} cobertura={(duplicatedBeacon ? "compartilhada" : "nova")} (needy={passengers.Count})");
        return best;
    }

    // Capturador VIVO mais proximo do APC dentre os slots do objetivo (sem filtro de distancia/needy).
    // Usado quando nao ha passageiro needy: o APC vazio vai buscar o mais perto em vez de pressionar
    // o objetivo sozinho. So conta quem ainda pode embarcar (vivo, fora, sem ter agido).
    private UnitManager FindNearestPlanCapturer(SectorObjective assigned, TeamId aiTeam, Vector3Int fromCell)
    {
        if (assigned?.Slots == null) return null;
        fromCell.z = 0;
        UnitManager best = null;
        float bestD = float.MaxValue;
        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!IsGroundTransportPassengerSlot(assigned, slot, aiTeam)) continue;
            UnitManager cap = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (cap == null || cap.IsEmbarked || cap.HasActed) continue;
            if (IsPassengerAlreadyAtCaptureObjective(cap, aiTeam)) continue;
            Vector3Int cc = cap.CurrentCellPosition; cc.z = 0;
            float d = SectorManager.HexDistance(fromCell, cc);
            if (d < bestD) { bestD = d; best = cap; }
        }
        return best;
    }

    private bool IsPassengerAlreadyAtCaptureObjective(UnitManager passenger, TeamId aiTeam)
    {
        if (passenger == null || passenger.IsDead || passenger.IsEmbarked)
            return false;

        Vector3Int cell = passenger.CurrentCellPosition;
        cell.z = 0;
        ConstructionManager building =
            ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (building == null || !building.IsCapturable)
            return false;

        int aiSlot = ResolveAISlotKey(aiTeam);
        return building.SlotIndex != aiSlot
            || building.CurrentCapturePoints < building.CapturePointsMax;
    }

    // Returns the first live, unacted capturer assigned to this objective's slots.
    private static UnitManager ResolveAssignedPassengerUnit(SectorObjective assigned, TeamId aiTeam)
    {
        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!IsGroundTransportPassengerSlot(assigned, slot, aiTeam)) continue;
            UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (capturer != null && !capturer.IsEmbarked && !capturer.HasActed)
                return capturer;
        }
        return null;
    }

    private static UnitManager ResolveAssignedPassengerSlotUnit(SectorObjective assigned, TeamId aiTeam)
    {
        if (assigned == null || assigned.Slots == null) return null;
        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!IsGroundTransportPassengerSlot(assigned, slot, aiTeam)) continue;
            UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (capturer != null && !capturer.IsDead)
                return capturer;
        }
        return null;
    }
}
