using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // LEGADO. Numero fixo de antes de o envelope existir, e ele e MAIS FROUXO que
    // a doutrina: um soldado move 3 e o teto e 4, entao um drop no limite custa
    // um turno extra ao passageiro sem ninguem registrar a perda.
    // Nao usar em codigo novo — ver ResolvePassengerDropOffRange abaixo.
    private const int TransportDropOffRange = 4;
    // Passageiro que alcança o objetivo em ate duas rodadas completas nao
    // precisa ocupar transporte. O orçamento concreto depende do movimento
    // da própria unidade: move 3 => 6 MP; move 2 => 4 MP.
    private const int TransportPassengerWalkTurns = 2;
    // Delivery range, not weapon range: artillery should be carried to the sector front
    // before DPQ decides the exact landing hex.
    private const int FireSupportDropOffRange = 3;

    // Reserva de passageiro por APC dentro de UMA passada da Phase 2 (transporterId -> passengerId).
    // Persiste entre as decisoes dos APCs para que, depois que um APC se move, o recalculo do
    // proximo NAO realoque o mesmo passageiro (evita 2 APCs no mesmo cara). Limpo a cada Phase 2.
    private readonly Dictionary<int, int> assignedTransportClaims = new Dictionary<int, int>();

    /// <summary>
    /// Banda de desembarque do PASSAGEIRO — nao constante do papel.
    ///
    /// <para><b>Tactical primeiro.</b> Largar dentro do Tactical significa que o
    /// passageiro materializa no alvo na rodada seguinte, sozinho. E a unica
    /// entrega que nao cobra um turno de ninguem.</para>
    ///
    /// <para><b>Operational e FALLBACK, nao alternativa.</b> So vale quando nada
    /// no Tactical serve: cerco, montanha, LZ sem rota, hex ocupado. Ai duas
    /// rodadas de marcha ainda sao melhores que continuar a bordo.</para>
    ///
    /// O orcamento e <c>MaxMovementPoints</c>, nao o restante: a marcha do
    /// passageiro comeca num turno novo, entao o que ele gastou embarcado nao
    /// conta. Ver <c>docs/AI Behavior/contrato_envelope_alcance.md</c> e a secao
    /// "Ranges are bands, not hex numbers" do CLAUDE.md.
    /// </summary>
    private static int ResolvePassengerDropOffRange(
        UnitManager passenger, bool operationalFallback)
    {
        int tactical = Mathf.Max(0, passenger != null ? passenger.MaxMovementPoints : 0);
        if (tactical <= 0)
            return 0;
        return operationalFallback
            ? tactical * Mathf.Max(1, TransportPassengerWalkTurns)
            : tactical;
    }

    /// <summary>
    /// ZONA DE ENTREGA — a banda do passageiro medida A PARTIR DO ALVO, e a
    /// celula dela mais barata para o transportador alcancar.
    ///
    /// <para>A zona e propriedade do DESTINO e do PASSAGEIRO. Ela nao encolhe
    /// quando o transportador se enrosca, e por isso serve de ancora estavel
    /// para o movimento — ao contrario do alvo cru, que fica atras do obstaculo
    /// e produz progresso zero.</para>
    ///
    /// <para>Duas medidas, e cada uma com a unidade certa:
    /// o raio da zona sai do <b>movimento do passageiro</b> (ele e quem anda o
    /// resto); a escolha de qual celula da zona mirar sai do <b>movimento do
    /// transportador</b> (ele e quem dirige). Misturar as duas foi a causa do
    /// vaivem no pe da serra.</para>
    ///
    /// <para>Devolve <c>false</c> quando nao ha zona alcancavel a pe — e ai o
    /// chamador mantem o alvo cru, que e o comportamento antigo.</para>
    /// </summary>
    private bool TryResolveDeliveryZoneAnchor(
        UnitManager transporter,
        UnitManager passenger,
        Vector3Int targetCell,
        Vector3Int transporterCell,
        out Vector3Int anchorCell,
        out int passengerWalkCost,
        out bool anchorIsTactical)
    {
        anchorCell = targetCell;
        passengerWalkCost = 0;
        anchorIsTactical = false;
        if (transporter == null || passenger == null || boardTilemap == null)
            return false;

        targetCell.z = 0;
        transporterCell.z = 0;

        // Raio da zona: Operational do passageiro. Tactical seria a entrega
        // ideal, mas como ancora de MOVIMENTO o Operational e o certo — o
        // transportador mira a borda util e o ranking do MelhorDesembarque
        // continua preferindo o ponto mais perto na hora de largar.
        int passengerBudget = ResolvePassengerDropOffRange(
            passenger, operationalFallback: true);
        if (passengerBudget <= 0)
            return false;

        Dictionary<Vector3Int, int> zone =
            UnitMovementPathRules.CalculateMovementCostMap(
                boardTilemap, passenger, targetCell, passengerBudget,
                terrainDatabase);
        if (zone == null || zone.Count == 0)
            return false;

        // Custo do TRANSPORTADOR ate cada celula da zona. Horizonte generoso:
        // a ancora pode estar a varios turnos, e o que importa e a direcao.
        Dictionary<Vector3Int, int> transporterCost =
            UnitMovementPathRules.CalculateMovementCostMap(
                boardTilemap, transporter, transporterCell, 120, terrainDatabase);

        // BORDA E PARA EMBARQUE. ENTREGA E PARA DENTRO.
        //
        //   "borda do tatico e para EMBARQUE (para ir rapidamente para o
        //    destino); dentro do tatico ou em cima do objetivo e para
        //    DESEMBARQUE (para desembarcar rapidamente na captura)"
        //   (autor, 2026-08-07)
        //
        // Sao duas geometrias opostas para a mesma banda, e confundi-las inverte
        // o resultado. No ENCONTRO o passageiro caminha para FORA, na direcao do
        // destino, entao a borda economiza turno dos dois. Na ENTREGA quem anda
        // depois e so ele, e cada hex que sobra e turno perdido antes de
        // capturar — a entrega quer o FUNDO da zona, colada no objetivo.
        //
        // Minha primeira versao ordenava por custo do transportador, que dentro
        // de qualquer faixa aponta sempre para a borda externa. Isso e a regra do
        // embarque aplicada ao desembarque: o APC pararia a 6 hexes do predio em
        // campo aberto e largaria o soldado a duas rodadas de marcha.
        //
        // Criterio correto, nesta ordem:
        //   1. a faixa           Tactical vence Operational, sempre
        //   2. marcha do PASSAGEIRO   menor e melhor — colado no objetivo
        //   3. custo do transportador  so como desempate
        int tacticalRange = ResolvePassengerDropOffRange(
            passenger, operationalFallback: false);

        bool found = false;
        int bestTier = int.MaxValue;
        int bestTransporterCost = int.MaxValue;
        int bestWalk = int.MaxValue;

        foreach (KeyValuePair<Vector3Int, int> entry in zone)
        {
            Vector3Int cell = entry.Key;
            cell.z = 0;
            if (transporterCost == null
                || !transporterCost.TryGetValue(cell, out int driveCost))
                continue;

            int tier = entry.Value <= tacticalRange ? 0 : 1;

            bool better =
                tier < bestTier
                || (tier == bestTier
                    && (entry.Value < bestWalk
                        || (entry.Value == bestWalk
                            && driveCost < bestTransporterCost)));
            if (!better)
                continue;

            bestTier = tier;
            bestTransporterCost = driveCost;
            bestWalk = entry.Value;
            anchorCell = cell;
            passengerWalkCost = entry.Value;
            found = true;
        }

        anchorIsTactical = found && bestTier == 0;
        return found;
    }

    private static int ResolvePassengerWalkWithoutTransportBudget(
        UnitManager passenger)
    {
        return passenger != null
            ? Mathf.Max(1, passenger.MaxMovementPoints)
                * TransportPassengerWalkTurns
            : 0;
    }

    // -------------------------------------------------------------------------
    // Entry point
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideTransportadorAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (unit == null || snapshot == null) return null;
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || !UnitRoleCompatibility.CanSatisfy(data, UnitRole.Transportador)) return null;

        PlayerAction repairAction = TryDecideRepairAction(unit, snapshot, plan);
        if (repairAction != null) return repairAction;

        // A entrada legada permanece apenas como adaptador de ENTREGA para
        // carga já embarcada. Pickup vazio é responsabilidade exclusiva de
        // TransportOperationsService + MelhorEmbarque.
        if (!HasTransportCargo(unit))
            return null;

        // Ferido a bordo de um supridor ja foi tratado no roteador (modo hospital). Se o
        // fluxo chegou aqui carregando paciente, e porque nao havia servico nem recarga:
        // o courier/EVAC abaixo desembarca normalmente. Ver Transportador.Hospital.
        if (data.domain == Domain.Air)
            return TryDecideAirTransportAction(unit, snapshot, plan);

        // Naval tem caminho proprio pelo mesmo motivo do aereo: o fluxo terrestre rota ATE
        // o objetivo, e um navio nunca alcanca um objetivo em terra. Ver Transportador.Naval.
        if (data.domain == Domain.Naval)
            return TryDecideNavalTransportAction(unit, snapshot, plan);

        // Os adaptadores de dominio abaixo apenas entregam carga existente.
        // A classificacao unificada ja decidiu pickups antes desta entrada.
        SectorObjective assigned = plan != null ? ResolveAssignedTransportObjective(unit, plan) : null;

        if (assigned != null)
            return DecideAssignedTransportAction(
                unit, snapshot, plan, assigned, hasCargo: true);
        return DecideTransportadorCourierAction(unit, snapshot);
    }

    // -------------------------------------------------------------------------
    // Helpers shared across Shuttle / Courier / Assigned
    // -------------------------------------------------------------------------

    private static bool HasTransportCargo(UnitManager unit)
    {
        if (unit.TransportedUnitSlots == null) return false;
        foreach (UnitTransportSeatRuntime seat in unit.TransportedUnitSlots)
            if (seat.embarkedUnit != null && seat.embarkedUnit.IsEmbarked) return true;
        return false;
    }

    private static bool IsTransporterAtCapacity(UnitManager unit, UnitData data = null)
    {
        if (unit == null)
            return false;
        if (data == null && !unit.TryGetUnitData(out data))
            return false;
        if (data == null || !data.isTransporter
            || data.transportSlots == null || data.transportSlots.Count == 0)
            return false;

        for (int slotIndex = 0; slotIndex < data.transportSlots.Count; slotIndex++)
        {
            UnitTransportSlotRule slot = data.transportSlots[slotIndex];
            if (slot == null)
                continue;
            int capacity = Mathf.Max(1, slot.capacity);
            int occupied = unit.GetOccupiedTransportSeatCountForSlot(slotIndex);
            if (occupied < capacity
                && unit.CanUseTransportSlotExclusivity(slotIndex, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAirTransporter(UnitManager unit)
    {
        if (unit == null) return false;
        if (!unit.TryGetUnitData(out UnitData data) || data == null) return false;
        return data.isTransporter && data.domain == Domain.Air;
    }

    private static bool IsPrimaryTransportRole(UnitManager unit)
    {
        if (unit == null
            || !unit.TryGetUnitData(out UnitData data)
            || data == null
            || !data.isTransporter
            || data.roles == null
            || data.roles.Count == 0)
            return false;

        return data.roles[0] == UnitRole.Transportador;
    }

    private PlayerAction DecideIdleTransportReturnAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        // Sem passageiro, usa a busca do assault rogue: considera todo o movimento
        // disponivel e pode executar movimento + ataque no mesmo batch.
        List<UnitManager> visibleEnemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        if (TryFindAssaultBreakerAttack(
                unit, snapshot, fromCell, paths, occupied, visibleEnemies,
                out Vector3Int idleAttackCell, out UnitManager idleAttackTarget, out string idleAttackReason))
        {
            Vector3Int idleTargetCell = idleAttackTarget.CurrentCellPosition; idleTargetCell.z = 0;
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} vazio - combate como assault rogue: {idleAttackReason}");
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} vazio — ataca {idleAttackTarget.InstanceId} via {idleAttackCell} (em vez de voltar a base sem pickup)");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, idleAttackCell,
                idleAttackTarget.InstanceId.ToString(), idleTargetCell, paths);
        }

        if (TryFindIdleTransportCombatAdvance(
                unit, snapshot, fromCell, paths, occupied,
                out Vector3Int combatMove, out UnitManager pressureTarget, out string combatReason))
        {
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} vazio - avanca contra {pressureTarget.UnitDisplayName}#{pressureTarget.InstanceId} via {combatMove} ({combatReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, combatMove, paths);
        }

        Vector3Int waitTarget = FindTransportWaitTarget(snapshot.AITeam, fromCell);
        waitTarget.z = 0;
        Vector3Int moveTarget = FindIdleTransportStagingCell(
            snapshot, fromCell, waitTarget, paths, occupied, out string stagingReason);

        Debug.Log($"{TL("Transporte")} {unit.InstanceId} vazio sem passageiro/TOW — estaciona fora da produtora alvo={waitTarget} via {moveTarget} ({stagingReason})");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

    private bool TryFindIdleTransportCombatAdvance(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int moveCell,
        out UnitManager pressureTarget,
        out string reason)
    {
        moveCell = fromCell;
        pressureTarget = null;
        reason = "";

        List<UnitManager> enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        float bestTargetScore = float.MinValue;
        foreach (UnitManager enemy in enemies)
        {
            BazookaTargetPriority preference = ResolveTransportTargetPreference(unit, enemy);
            if (preference == BazookaTargetPriority.Tertiary)
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            float score =
                (preference == BazookaTargetPriority.Primary ? 100000f : 50000f)
                - SectorManager.HexDistance(fromCell, enemyCell) * 100f
                + Mathf.Max(0, 20 - enemy.CurrentHP) * 10f
                - enemy.InstanceId * 0.001f;
            if (score <= bestTargetScore)
                continue;

            bestTargetScore = score;
            pressureTarget = enemy;
        }

        if (pressureTarget == null)
            return false;

        Vector3Int targetCell = pressureTarget.CurrentCellPosition;
        targetCell.z = 0;
        Vector3Int candidate = FindAssaultPressureMove(
            unit, snapshot, fromCell, targetCell, paths, occupied, out reason);
        if (candidate == fromCell || IsTeamProductionBuilding(candidate, snapshot.AITeam))
            return false;

        moveCell = candidate;
        return true;
    }

    private Vector3Int FindIdleTransportStagingCell(
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int waitTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out string reason)
    {
        Vector3Int bestCell = fromCell;
        float bestScore = float.MinValue;
        reason = "sem celula livre";

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied.Contains(cell))
                continue;
            if (IsTeamProductionBuilding(cell, snapshot.AITeam))
                continue;

            float distance = SectorManager.HexDistance(cell, waitTarget);
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float dpq = GetTerrainDpqPontos(cell);
            int pathCost = GetPathStepCount(paths, cell);
            float score =
                -distance * 1000f
                - threat * 80f
                + dpq * 100f
                + pathCost * 0.1f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestCell = cell;
            reason = $"dist={distance:F1} threat={threat:F1} dpq={dpq:F1} path={pathCost}";
        }

        return bestCell;
    }

    private static SectorObjective ResolveAssignedTransportObjective(UnitManager unit, TeamObjectivePlan plan)
    {
        if (unit == null || plan?.Objectives == null)
            return null;

        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Transportador && slot.Filled && slot.AssignedUnitId == unit.InstanceId)
                    return obj;
        return null;
    }

    private bool IsNonTeamConstruction(Vector3Int cell, TeamId aiTeam)
    {
        ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        return bldg != null && bldg.SlotIndex != ResolveAISlotKey(aiTeam);
    }

    private float ScoreTransportTrafficPenalty(UnitManager unit, Vector3Int cell, Vector3Int targetCell, TeamId aiTeam)
    {
        if (unit == null)
            return 0f;

        cell.z = 0;
        targetCell.z = 0;
        float cellDistToTarget = SectorManager.HexDistance(cell, targetCell);
        HeightBand moverBand = OccupancyResolver.GetHeightBand(unit);
        float penalty = 0f;

        foreach (UnitManager ally in UnitManager.AllActive)
        {
            if (ally == null || ally == unit || ally.SlotIndex != ResolveAISlotKey(aiTeam) || ally.IsDead || ally.IsEmbarked)
                continue;

            ally.SyncLayerStateFromData(forceNativeDefault: false);
            if (OccupancyResolver.GetHeightBand(ally) != moverBand)
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float allyDistFromCandidate = SectorManager.HexDistance(cell, allyCell);
            float allyDistToTarget = SectorManager.HexDistance(allyCell, targetCell);
            bool allyAhead = allyDistToTarget < cellDistToTarget + 0.1f;
            float lineDeviation = DistanceFromHexLine(allyCell, cell, targetCell);

            if (allyDistFromCandidate <= 1.5f)
            {
                penalty += 1800f;
                continue;
            }

            if (allyAhead && lineDeviation <= 1.25f)
            {
                penalty += 1500f;
                continue;
            }

            if (allyAhead && allyDistFromCandidate <= 3f && lineDeviation <= 2.25f)
                penalty += 800f;
        }

        return penalty;
    }

    // Max-displacement move: minimises real MP cost to target (unit-aware terrain costs),
    // prefers cells without non-team constructions, uses threat as tiebreaker.
    private Vector3Int FindTransportMove(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int pressureTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamId aiTeam)
    {
        using var perf = new AIDecisionPerfScope(unit, "transportMove");
        // Reverse cost map: how many MP does this unit need to go from pressureTarget to each cell?
        // Keep this horizon generous: transport objectives can require long detours around
        // terrain chokepoints, and a short horizon looks like a false blockade.
        Dictionary<Vector3Int, int> costFromTarget;
        using (new AIDecisionPerfScope(unit, "transportReverseCostMap"))
        {
            costFromTarget = UnitMovementPathRules.CalculateMovementCostMap(
                boardTilemap, unit, pressureTarget, 120, terrainDatabase);
        }
        // Forward cost map from origin (no road bonus — cells reachable only via the free road step
        // will be absent and handled as full-budget cost in TryScoreTwoTurnProgression).
        Dictionary<Vector3Int, int> costFromOrigin;
        using (new AIDecisionPerfScope(unit, "transportOriginCostMap"))
        {
            costFromOrigin = UnitMovementPathRules.CalculateMovementCostMap(
                boardTilemap, unit, fromCell, unit.RemainingMovementPoints, terrainDatabase);
        }

        float GetCost(Vector3Int c) =>
            costFromTarget.TryGetValue(c, out int v) ? (float)v : float.MaxValue;

        Vector3Int bestCell = fromCell;
        float bestDist = GetCost(fromCell);
        bool bestIsNonTeamBldg = false;
        float bestThreat = float.MaxValue;
        bool foundReachableRoute = bestDist < float.MaxValue;
        bool foundImprovingMove = false;
        Vector3Int bestHorizonCell = fromCell;
        float bestHorizonScore = float.MinValue;
        bool foundHorizonMove = false;

        const float eps = 0.01f;
        float originRouteCost = GetCost(fromCell);
        int nextTurnBudget = Mathf.Max(0, unit.RemainingMovementPoints);

        foreach (Vector3Int cell in paths.Keys)
        {
            if (cell == fromCell) continue;
            if (occupied.Contains(cell)) continue;

            float dist = GetCost(cell);
            bool isNonTeamBldg = IsNonTeamConstruction(cell, aiTeam);
            float threat = CalculateThreatLevel(cell, aiTeam);
            if (dist < float.MaxValue)
                foundReachableRoute = true;

            // O mapa reverso já responde quanto falta deste candidato até o alvo. Antes o APC era
            // movido virtualmente para CADA célula e uma nova BFS era executada dali (duas vezes em
            // alguns fluxos). Para transporte, a projeção de 2T pode ser derivada diretamente:
            // distância restante após o próximo turno = max(0, custoAtéAlvo - orçamentoDoTurno).
            if (dist < float.MaxValue && originRouteCost < float.MaxValue)
            {
                float firstTurnProgress = originRouteCost - dist;
                float distanceAfterNextTurn = Mathf.Max(0f, dist - nextTurnBudget);
                float twoTurnProgress = originRouteCost - distanceAfterNextTurn;
                float lineDeviation = DistanceFromHexLine(cell, fromCell, pressureTarget);
                int firstMoveCost = costFromOrigin.TryGetValue(cell, out int realCost)
                    ? realCost
                    : nextTurnBudget;
                bool usedRoadBonus = paths.TryGetValue(cell, out List<Vector3Int> candidatePath)
                    && candidatePath != null
                    && UnitMovementPathRules.DidUseRoadFullMoveBonus(
                        boardTilemap, unit, candidatePath, terrainDatabase);

                float toolScore = Mathf.Round(
                    twoTurnProgress * 10f
                    + firstTurnProgress * 6f
                    - lineDeviation);
                float horizonScore = toolScore * 1000f
                    + (usedRoadBonus ? 650f : 0f);
                horizonScore -= threat * 0.5f;
                horizonScore -= ScoreTransportTrafficPenalty(unit, cell, pressureTarget, aiTeam);
                horizonScore -= isNonTeamBldg ? 300f : 0f;
                // Desempate suave: entre candidatos com a mesma progressão inteira, prefere gastar
                // menos MP. Não compete com um ponto real de progressão (1000 no score principal).
                horizonScore -= firstMoveCost * 0.01f;
                if (horizonScore > bestHorizonScore)
                {
                    bestHorizonScore = horizonScore;
                    bestHorizonCell = cell;
                    foundHorizonMove = true;
                }
            }

            bool isBetter;
            if (dist < bestDist - eps)
                isBetter = true;
            else if (dist < bestDist + eps)
                isBetter = (!isNonTeamBldg && bestIsNonTeamBldg)
                           || (isNonTeamBldg == bestIsNonTeamBldg && threat < bestThreat - eps);
            else
                isBetter = false;

            if (isBetter)
            {
                bestCell = cell;
                bestDist = dist;
                bestIsNonTeamBldg = isNonTeamBldg;
                bestThreat = threat;
                if (dist < GetCost(fromCell) - eps)
                    foundImprovingMove = true;
            }
        }


        if (foundHorizonMove && bestHorizonScore > 0f)
        {
            Debug.Log($"{TL("Progressao2")} transporte {unit.InstanceId} alvo={pressureTarget} via {bestHorizonCell} (heuristica score={bestHorizonScore:F0})");
            return bestHorizonCell;
        }

        if (foundReachableRoute && (bestCell != fromCell || foundImprovingMove))
        {
            Debug.Log($"{TL("Progressao2")} transporte {unit.InstanceId} alvo={pressureTarget} via {bestCell} (heuristica dist={bestDist:F0})");
            return bestCell;
        }

        // Rota alcancavel mas ja estamos no melhor lugar (ex.: em cima do alvo de espera): FICA.
        // A exploracao premia andar o maximo (pathSteps), entao so deve rodar quando NAO ha rota
        // (bloqueio real) — senao um APC parado no ponto certo sairia vagando a esmo.
        if (foundReachableRoute)
            return fromCell;

        return FindTransportExplorationMove(unit, fromCell, pressureTarget, paths, occupied, aiTeam);
    }

    private Vector3Int FindTransportExplorationMove(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int pressureTarget,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamId aiTeam)
    {
        Vector3Int bestCell = fromCell;
        float bestScore = float.MinValue;
        float fromHexDist = SectorManager.HexDistance(fromCell, pressureTarget);
        bool fromRouteFound = TryCalculateRouteDistance(unit, fromCell, pressureTarget, out float fromRouteDist);

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell) continue;
            if (occupied != null && occupied.Contains(cell)) continue;

            float hexDist = SectorManager.HexDistance(cell, pressureTarget);
            bool cellRouteFound = TryCalculateRouteDistance(unit, cell, pressureTarget, out float routeDist);
            float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - routeDist : 0f;
            bool recoversMissingRoute = !fromRouteFound && cellRouteFound;
            float progress = recoversMissingRoute
                ? -routeDist
                : (fromRouteFound && cellRouteFound) ? routeProgress : fromHexDist - hexDist;
            int pathSteps = GetPathStepCount(paths, cell);
            float threat = CalculateThreatLevel(cell, aiTeam);
            bool isNonTeamBldg = IsNonTeamConstruction(cell, aiTeam);

            float score =
                Mathf.Min(pathSteps, 8) * 55f
                + Mathf.Max(0f, progress) * 35f
                - Mathf.Max(0f, -progress) * 18f
                - threat * 0.5f
                - (isNonTeamBldg ? 300f : 0f);

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    // Returns the effective transport slot threshold for the current map.
    // If the farthest capturable sector is closer than MinDistanceForTransportSlot,
    // the threshold adapts so transport slots are still created and shuttle candidates still qualify.
    public int GetEffectiveTransportThresholdForSlot(PlayerSlotId aiSlot)
    {
        int maxDist = 0;
        foreach (SectorManager.SectorInfo info in SectorManager.GetAllSectorInfos())
        {
            if (info.IsFullyControlled && info.ControllingSlotIndex == aiSlot.Value) continue;
            int d = Mathf.RoundToInt(info.GetDistanceToHQ(aiSlot));
            if (d > maxDist) maxDist = d;
        }
        // Include enemy base sectors so threshold stays at MinDistanceForTransportSlot in late
        // game when all regular sectors are captured (maxDist would otherwise collapse to 0).
        foreach (SectorManager.SectorInfo baseInfo in SectorManager.GetAllBaseInfos())
        {
            if (baseInfo.ControllingSlotIndex == aiSlot.Value) continue;
            int d = Mathf.RoundToInt(baseInfo.GetDistanceToHQ(aiSlot));
            if (d > maxDist) maxDist = d;
        }
        return Mathf.Min(MinDistanceForTransportSlot, Mathf.Max(3, maxDist));
    }

    private static List<UnitManager> GetAvailableTransporters(TeamId aiTeam)
    {
        var list = new List<UnitManager>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.SlotIndex != ResolveAISlotKey(aiTeam) || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
            if (!u.TryGetUnitData(out UnitData data)) continue;
            if (UnitRoleCompatibility.IsOperationalTransporter(data))
                list.Add(u);
        }
        return list;
    }

    // Resolves the objective target cell for a unit: assigned capturable, or fallback to enemy HQ/building.
    private static Vector3Int ResolveUnitObjectiveCell(UnitManager unit, TeamObjectivePlan plan, AIWorldSnapshot snapshot)
    {
        if (plan != null)
        {
            foreach (SectorObjective obj in plan.Objectives)
                foreach (SlotNeed slot in obj.Slots)
                    if (slot.Filled && slot.AssignedUnitId == unit.InstanceId)
                    {
                        if (IsActiveRallyAssemblyObjective(obj))
                            return ResolveRallyAssemblyAnchor(obj, snapshot.AITeam, unit.CurrentCellPosition);

                        ConstructionManager tgt = FindCapturableInSector(obj.Sector, snapshot.AITeam);
                        if (tgt != null) { Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0; return tc; }
                        // No capturable (e.g. FireSupport assigned to a support sector already controlled):
                        // use the sector's representative cell so navigation still targets the right area.
                        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo si))
                        { Vector3Int rc = si.RepresentativeCell; rc.z = 0; return rc; }
                    }
        }

        if (snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition; hq.z = 0;
            return hq;
        }

        if (snapshot.EnemyBuildings != null && snapshot.EnemyBuildings.Count > 0)
        {
            Vector3Int unitCell = unit.CurrentCellPosition; unitCell.z = 0;
            ConstructionManager nearest = null;
            float nearestDist = float.MaxValue;
            foreach (ConstructionManager eb in snapshot.EnemyBuildings)
            {
                Vector3Int ec = eb.CurrentCellPosition; ec.z = 0;
                float d = SectorManager.HexDistance(unitCell, ec);
                if (d < nearestDist) { nearestDist = d; nearest = eb; }
            }
            if (nearest != null)
            {
                Vector3Int nc = nearest.CurrentCellPosition; nc.z = 0;
                return nc;
            }
        }

        return Vector3Int.zero;
    }
}
