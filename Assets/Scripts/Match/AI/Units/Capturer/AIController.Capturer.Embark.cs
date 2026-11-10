using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int CapturerRideOperationalTurns = 2;

    // -------------------------------------------------------------------------
    // Intercepção de embarque — capturador embarca em transporte no alcance
    // -------------------------------------------------------------------------

    // True se a unidade esta atribuida a um rally assembly ainda ativo (montando massa, nao GoGreen)
    // E ja esta dentro do raio de montagem do ponto de rally — nesse caso deve SEGURAR, nao embarcar.
    private bool ShouldHoldRallyAssemblyInsteadOfEmbark(UnitManager unit, SectorObjective assigned)
    {
        if (!IsActiveRallyAssemblyObjective(assigned))
            return false;
        if (!TryGetRallyAnchorCell(assigned.Sector, out Vector3Int anchor))
            return false;
        Vector3Int cell = unit.CurrentCellPosition; cell.z = 0;
        return SectorManager.HexDistance(anchor, cell) <= RallyAssemblyForceRadius;
    }

    // Celula do ponto de rally do setor (anchor usado na contagem de presenca do GoGreen).
    internal bool TryGetRallyAnchorCell(ConstructionSector sector, out Vector3Int anchor)
    {
        anchor = default;
        if (sector == ConstructionSector.None || ConstructionManager.AllActive == null)
            return false;
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally != null && rally.IsRallyPoint && rally.Sector == sector)
            {
                anchor = rally.CurrentCellPosition; anchor.z = 0;
                return true;
            }
        }
        return false;
    }

    private PlayerAction TryDecideCapturerEmbarkAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!unit.TryGetUnitData(out UnitData data)
            || !UnitRoleCompatibility.CanSatisfy(data, UnitRole.Capturador)) return null;

        // capturerAssigned: slot de capturador exclusivo — usado para o skip de embarque.
        // Rogues (sem slot de capturador) recebem null e nunca pulam o embarque por
        // "estar perto do objetivo", pois seu destino real é o HQ inimigo, não o setor.
        SectorObjective capturerAssigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;

        // assigned: ampliado para multi-role (e.g. Assalto+Capturador) que não têm slot de
        // capturador mas têm atribuição em outro role — usado para sector match do APC.
        SectorObjective assigned = capturerAssigned;
        if (assigned == null && plan != null)
            assigned = ResolveAnyAssignedObjective(unit, plan);

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;

        // Capturador montando massa num rally assembly AINDA ativo (nao GoGreen/Expired) e ja
        // DENTRO do raio de montagem NAO deve embarcar — sair leva a massa embora e adia o GoGreen
        // (presenca conta, ver EvaluateRallyReadiness). Se estiver LONGE do rally, carona pra chegar
        // continua valendo. (Caso: 63 segurando Foxtrot em Assembling pegou carona no APC 19.)
        if (ShouldHoldRallyAssemblyInsteadOfEmbark(unit, assigned))
        {
            TraceAIReviewCandidate("Embarque", "Embark", fromCell, null, false,
                "hold_rally_assembly", "Unidade deve manter presenca na montagem do rally.");
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} NAO embarca: montando massa no rally "
                + $"{assigned.Sector} (state={assigned.RallyState}) dentro do raio {RallyAssemblyForceRadius}h");
            return null;
        }

        // Gate único de necessidade. Não escolhe transporte, vaga ou LZ e não
        // materializa ação; apenas decide se vale iniciar os scans de embarque.
        QueroCaronaResult rideNeed =
            EvaluateCapturerRideNeed(unit, assigned);
        if (!rideNeed.wantsRide)
        {
            TraceAIReviewCandidate("Embarque", "Embark", fromCell, null, false,
                "ride_not_needed", "QueroCarona recusou a necessidade de iniciar scans de embarque.");
            return null;
        }

        // Pass 1: sensor padrão — encontra transporters adjacentes (1h).
        // Só varre depois de o passageiro confirmar que precisa de carona.
        var options = new List<PodeEmbarcarOption>();
        PodeEmbarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase,
            Mathf.Max(0, unit.RemainingMovementPoints), options);

        // Não embarcar em transporters ainda no aeroporto/fábrica — espera sair primeiro.
        options.RemoveAll(opt =>
        {
            if (opt?.transporterUnit == null) return false;
            Vector3Int tc = opt.transporterUnit.CurrentCellPosition; tc.z = 0;
            return IsTeamProductionBuilding(tc, unit.TeamId);
        });

        PodeEmbarcarOption best = null;
        int bestPriority = int.MaxValue;
        float bestDistance = float.MaxValue;

        if (options.Count > 0)
        {
            foreach (PodeEmbarcarOption opt in options)
            {
                if (!TryGetCapturerEmbarkPreference(unit, assigned, opt, plan, snapshot, snapshot.AITeam,
                        out int priority, out float distance))
                {
                    TraceAIReviewCandidate("Embarque", "Embark", opt.transporterUnit.CurrentCellPosition, opt.transporterUnit.InstanceId.ToString(), false,
                        "embark_preference_rejected", "A preferencia de embarque recusou esta opcao.");
                    continue;
                }
                TraceAIReviewCandidate("Embarque", "Embark", opt.transporterUnit.CurrentCellPosition, opt.transporterUnit.InstanceId.ToString(), true,
                    "preference", "Menor prioridade vence; distancia desempata.", hasPreference: true, priority: priority, distance: distance);

                if (priority < bestPriority
                    || (priority == bestPriority && distance < bestDistance))
                {
                    best = opt;
                    bestPriority = priority;
                    bestDistance = distance;
                }
            }
        }

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);

        // Pass 2: simula PodeEmbarcarSensor em cada hex candidato (ficar parado + hexes alcançáveis).
        // Pass 2a: exige transporter formalmente pareado com este passageiro.
        // Pass 2b: exige transporter do mesmo setor do plano.
        // Pass 2c: aceita transporter livre (sem passageiro formal).
        // Pass 3: overflow — embarca em qualquer transporter com slot físico livre (último recurso).
        if (paths == null || paths.Count == 0)
        {
            Debug.Log(BuildCapturerEmbarkScanDebug(unit, data, assigned, plan, snapshot,
                fromCell, options.Count, best, bestPriority, "sem paths"));
            return null;
        }

        // Combate/captura imediatos continuam sendo política do controller,
        // mesmo quando a estimativa confirmou necessidade de transporte.
        if (assigned == null && ShouldRogueCapturerFightBeforeTransport(unit, snapshot, fromCell, paths))
            return null;

        if (best != null && bestPriority <= 0)
        {
            if (!IsTransportPickupBeaconFor(
                    best.transporterUnit, unit)
                && ShouldYieldEmbarkToNeedierCapturer(
                    unit, best.transporterUnit, assigned, plan))
                return null;
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca → {best.transporterUnit.InstanceId} slot {best.transporterSlotIndex}");
            return BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, best.transporterUnit, best.transporterSlotIndex, paths);
        }

        PlayerAction formalExtendedEmbark =
            TryBuildExtendedEmbarkBatch(
                unit, data, snapshot, plan, assigned, rideNeed,
                fromCell, paths, requireFormalPassenger: true);
        if (formalExtendedEmbark != null) return formalExtendedEmbark;

        if (best != null)
        {
            if (ShouldYieldEmbarkToNeedierCapturer(
                    unit, best.transporterUnit, assigned, plan))
                return null;
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca fallback p{bestPriority} â†’ {best.transporterUnit.InstanceId} slot {best.transporterSlotIndex}");
            return BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, best.transporterUnit, best.transporterSlotIndex, paths);
        }

        PlayerAction extendedEmbark =
            TryBuildExtendedEmbarkBatch(
                unit, data, snapshot, plan, assigned, rideNeed,
                fromCell, paths, requireSectorMatch: true)
            ?? TryBuildExtendedEmbarkBatch(
                unit, data, snapshot, plan, assigned, rideNeed,
                fromCell, paths, requireSectorMatch: false)
            ?? TryBuildExtendedEmbarkBatch(
                unit, data, snapshot, plan, assigned, rideNeed,
                fromCell, paths, requireSectorMatch: false,
                allowOverflow: true);
        if (extendedEmbark != null) return extendedEmbark;

        // Nao existe embarque materializavel neste turno. A partir daqui o
        // capturador que ja provou precisar de carona nao pode voltar a usar o
        // predio do outro lado da agua como ancora de movimento. Pergunta ao
        // MelhorEmbarque qual e o lado terrestre do encontro e passa a
        // facilitar a coleta.
        PlayerAction lzRendezvous =
            TryBuildCapturerLzRendezvousAction(
                unit,
                snapshot,
                rideNeed,
                fromCell,
                paths);
        if (lzRendezvous != null)
            return lzRendezvous;

        // Rogue capturer: extended embark failed — move toward nearest rogue transporter so
        // it enters embark range next turn. Only applies when there is no sector assignment
        // (rogues march to enemy HQ; boarding any rogue transport accelerates the push).
        if (assigned == null)
        {
            UnitManager rogueTransport = FindNearestRogueTransporter(
                unit, data, plan, snapshot, rideNeed);
            if (rogueTransport != null)
            {
                Vector3Int tCell = rogueTransport.CurrentCellPosition; tCell.z = 0;
                HashSet<Vector3Int> occ = BuildOccupied(unit);
                Vector3Int moveTarget = FindTransportMove(unit, fromCell, tCell, paths, occ, snapshot.AITeam);
                if (moveTarget != fromCell)
                {
                    Debug.Log($"{TL("Capturador")} {unit.InstanceId} rogue — avança para transporte rogue {rogueTransport.InstanceId}@{tCell} via {moveTarget}");
                    return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
                }
            }
        }
        Debug.Log(BuildCapturerEmbarkScanDebug(unit, data, assigned, plan, snapshot,
            fromCell, options.Count, best, bestPriority, "sem embarque valido"));
        return null;
    }

    /// <summary>
    /// Perspectiva Passageiro do MelhorEmbarque. O alvo de movimento e sempre
    /// passengerMeetingCell (terra), nunca lzCell (que pode ser agua).
    ///
    /// Uma promessa persistida da preferencia ao casco prometido dentro da
    /// mesma banda, mas nao o transforma em dono do passageiro: uma solucao
    /// Tactical de outro casco vence uma promessa Operational/Strategic, e na
    /// ausencia de solucao prometida qualquer transportador compativel serve.
    /// </summary>
    // O casco ja tem passageiro escolhido nesta Phase 2 e nao e este. Manifesto
    // Tactical (varios assentos na mesma LZ) continua aceitando os seus.
    private bool IsPickupTakenByOtherPassenger(UnitManager transporter, UnitManager passenger)
    {
        if (transporter == null || passenger == null)
            return false;
        if (!transportPickupBeacons.TryGetValue(transporter.InstanceId, out int chosenId)
            || chosenId == passenger.InstanceId)
            return false;
        return !(tacticalPickupManifestBeacons.TryGetValue(transporter.InstanceId, out HashSet<int> manifest)
                 && manifest != null && manifest.Contains(passenger.InstanceId));
    }

    private PlayerAction TryBuildCapturerLzRendezvousAction(
        UnitManager passenger,
        AIWorldSnapshot snapshot,
        QueroCaronaResult rideNeed,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        if (passenger == null
            || snapshot == null
            || rideNeed == null
            || !rideNeed.wantsRide)
        {
            return null;
        }

        // O filtro roda por CELULA de cada mapa de encontro (agora, proximo turno e o de longo
        // alcance, que e o tabuleiro inteiro). IsOwnProductionCell pergunta a CENA a cada chamada
        // (FindObjectsByType): milhares de buscas por decisao, ~13 s. Uma lista, montada uma vez.
        HashSet<Vector3Int> ownProduction = BuildOwnProductionCells(snapshot.AITeam);
        MelhorEmbarqueResult evaluated =
            MelhorEmbarqueService.EvaluateForPassenger(
                new MelhorEmbarquePassengerRequest
                {
                    passenger = passenger,
                    allowPassengerMeetingCell = cell =>
                    {
                        cell.z = 0;
                        return !ownProduction.Contains(cell);
                    },
                    // Vazio de proposito: o passageiro nao escolhe um dono.
                    // O farol persistido entra apenas na preferencia abaixo.
                    transporter = null,
                    map = boardTilemap,
                    terrainDatabase = terrainDatabase,
                    operationalTurns = CapturerRideOperationalTurns,
                    includeStrategic = true,
                    evaluateRideNeed = candidate =>
                        candidate == passenger ? rideNeed : null,
                    diagnosticLog = showAILogs
                        ? (System.Action<string>)(message =>
                            Debug.Log(message))
                        : null
                });

        MelhorEmbarqueOption best = null;
        MelhorEmbarqueOption promised = null;
        for (int i = 0; i < evaluated.options.Count; i++)
        {
            MelhorEmbarqueOption option = evaluated.options[i];
            if (option == null
                || option.passenger != passenger
                || option.transporter == null
                || !option.hasPassengerMeetingCell
                || option.passengerRouteState ==
                    MelhorEmbarquePassengerRouteState.NoCurrentRoute)
            {
                continue;
            }

            // VEICULO COM DONO NAO ATRAI. Cada passageiro perguntava sozinho e
            // sobrescrevia o farol com o proprio nome: o ultimo a agir ganhava,
            // e todos andavam ate o APC (AI Step 2026-11-04: #9, #13, #14, #19
            // e #20 "vao ao encontro" do #21, que tinha escolhido o #9). Se o
            // casco ja tem passageiro escolhido — por ele mesmo ou por quem agiu
            // antes —, quem nao e esse passageiro nem esta no manifesto dele
            // procura outro casco ou segue a vida.
            if (IsPickupTakenByOtherPassenger(option.transporter, passenger))
                continue;

            if (best == null)
                best = option;
            if (promised == null
                && HasActiveRidePromiseFor(
                    option.transporter,
                    passenger))
            {
                promised = option;
            }
        }

        if (promised != null
            && (best == null
                || promised.transporterTier <= best.transporterTier))
        {
            best = promised;
        }

        if (best == null)
        {
            Debug.Log(
                $"{TL("Capturador")} {passenger.InstanceId} " +
                "MelhorEmbarque nao encontrou encontro terrestre " +
                $"materializavel (opcoes={evaluated.options.Count}).");

            // Sem rota propria, voltar ao magnetico recria exatamente o bug:
            // a unidade anda na direcao cubica de um destino pertencente a
            // outro componente. Segura e continua emitindo demanda de carona.
            if (rideNeed.isStranded)
            {
                // ...MAS SEGURAR NAO E TRABALHAR. "Antes do tiro vem o dinheiro":
                // sem carona materializavel, anda ate o capturavel mais perto
                // alcancavel A PE (o componente dele, nao o do plano). AI Step T3
                // amarelo: #19 parado em (13,1) esperando carona para November com
                // Echo e Foxtrot livres na mesma ilha. O plano e o pedido de carona
                // continuam; a carona o encontra onde ele estiver.
                if (TryBuildWorkWhileWaitingForRide(
                        passenger, snapshot, fromCell, paths, out PlayerAction work))
                    return work;

                // Sem nada para capturar a pe: anda ate a CABECA DE PRAIA — a celula do
                // proprio componente mais perto do alvo do plano — e espera la (autor,
                // 2026-11-06). Ja otimiza a LZ futura. Nao e o bug antigo: o destino sai
                // das celulas que ele alcanca a pe, nunca da direcao cubica.
                if (TryBuildAdvanceToBeachhead(
                        passenger, snapshot, fromCell, paths, rideNeed.evaluatedTarget,
                        out PlayerAction advance))
                    return advance;

                // ESPERAR NAO E EM CIMA DA FABRICA. Sem trabalho a pe, a espera tranca a
                // compra daquele produtor no fim do turno (AI Step T4 azul: #26 aguardando
                // carona em (1,14), na propria Fabrica Leve). Sai para o lado — a melhor
                // celula fora de produtora no alcance — e espera ali; a carona o acha.
                if (IsOwnProductionCell(fromCell, snapshot.AITeam)
                    && TryFindProductionUnlockVacateCell(
                        passenger, snapshot, fromCell, paths, BuildOccupied(passenger),
                        out Vector3Int vacateCell, out string vacateReason))
                {
                    Debug.Log($"{TL("Capturador")} {passenger.InstanceId} aguarda carona FORA da " +
                              $"produtora: libera {fromCell} via {vacateCell} ({vacateReason})");
                    PlayerAction vacate = BuildVacateBatch(
                        passenger, snapshot.AITeam, fromCell, vacateCell, paths);
                    vacate.DebugLabel = "aguarda Melhor LZ de Embarque fora da produtora";
                    return vacate;
                }

                PlayerAction hold = BuildMoveBatch(
                    passenger,
                    snapshot.AITeam,
                    fromCell,
                    fromCell,
                    paths);
                hold.DebugLabel =
                    "aguarda Melhor LZ de Embarque; sem rota propria " +
                    "e sem encontro terrestre materializavel";
                return hold;
            }

            return null;
        }

        Vector3Int meetingCell = best.passengerMeetingCell;
        meetingCell.z = 0;
        Vector3Int transporterLz = best.lzCell;
        transporterLz.z = 0;
        bool followsPromise = HasActiveRidePromiseFor(
            best.transporter,
            passenger);

        // QUEM ESCOLHE E O VEICULO. Casco do mesmo time que ainda vai agir nesta
        // fase: o passageiro NAO anda para a LZ antes dele, nem marca o farol —
        // so registra a espera e a Phase 2 o adia (cede a vez). Depois que o
        // veiculo decide e reserva o encontro, o passageiro re-decide: se foi o
        // escolhido, vai; se nao, o casco tem dono e ele segue a propria agenda.
        if (!best.transporter.HasActed
            && best.transporter.SlotIndex == passenger.SlotIndex)
        {
            pendingRendezvousTransporters[passenger.InstanceId] =
                best.transporter.InstanceId;
        }
        else
        {
            // Veiculo ja agiu (ou nao e desta IA): o farol so registra o par.
            transportPickupBeacons[best.transporter.InstanceId] =
                passenger.InstanceId;
        }

        PlayerAction action;
        string verb;
        if (meetingCell == fromCell)
        {
            action = BuildMoveBatch(
                passenger,
                snapshot.AITeam,
                fromCell,
                fromCell,
                paths);
            verb = "aguarda no encontro";
        }
        else if (paths != null && paths.ContainsKey(meetingCell))
        {
            action = BuildMoveBatch(
                passenger,
                snapshot.AITeam,
                fromCell,
                meetingCell,
                paths);
            verb = "vai ao encontro";
        }
        else
        {
            HashSet<Vector3Int> occupied = BuildOccupied(passenger);
            if (TryFindBestToolProgressionCell(
                    passenger,
                    snapshot,
                    fromCell,
                    meetingCell,
                    paths,
                    occupied,
                    ToolProgressionIntent.TransportRendezvous,
                    out Vector3Int progressionCell,
                    out _,
                    out string progressionReason,
                    allowCell: cell =>
                    {
                        cell.z = 0;
                        return !ownProduction.Contains(cell);
                    })
                && progressionCell != fromCell)
            {
                action = BuildMoveBatch(
                    passenger,
                    snapshot.AITeam,
                    fromCell,
                    progressionCell,
                    paths);
                verb = $"progride ao encontro ({progressionReason})";
            }
            else
            {
                // A LZ existe e continua sendo a ancora correta. Se nenhuma
                // progressao e materializavel agora, esperar e melhor que
                // abandonar o encontro e voltar a perseguir o predio remoto.
                action = BuildMoveBatch(
                    passenger,
                    snapshot.AITeam,
                    fromCell,
                    fromCell,
                    paths);
                verb = "aguarda progressao ao encontro";
            }
        }

        action.DebugLabel =
            $"{verb} do transportador " +
            $"{best.transporter.UnitDisplayName}" +
            $"#{best.transporter.InstanceId}; " +
            $"encontroPax={meetingCell}, LZTransport={transporterLz}, " +
            $"tier={best.transporterTier}, " +
            $"promessa={(followsPromise ? "sim" : "nao")}";
        Debug.Log(
            $"{TL("Capturador")} {passenger.InstanceId} " +
            $"MelhorEmbarque: {verb}; encontroPax={meetingCell} " +
            $"LZTransport={transporterLz} " +
            $"transportador=#{best.transporter.InstanceId} " +
            $"tier={best.transporterTier} " +
            $"route={best.passengerRouteState} " +
            $"promessa={(followsPromise ? "sim" : "nao")}.");
        if (TryBuildRideWaitOpportunityKill(passenger, snapshot, action, best, paths, out var opportunisticKill))
            return opportunisticKill;
        return action;
    }

    private bool TryBuildRideWaitOpportunityKill(UnitManager passenger, AIWorldSnapshot snapshot,
        PlayerAction waitingAction, MelhorEmbarqueOption meeting,
        Dictionary<Vector3Int, List<Vector3Int>> paths, out PlayerAction attack)
    {
        attack = null;
        if (waitingAction == null || waitingAction.SensorAction != SensorActionType.None
            || !waitingAction.HasMoveTo || passenger.IsUnderRepair || meeting == null) return false;
        var from = passenger.CurrentCellPosition; from.z = 0;
        var occupied = BuildOccupied(passenger);
        var enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        UnitManager bestTarget = null;
        Vector3Int bestCell = from;
        int bestDpq = int.MinValue, bestLoss = int.MaxValue;
        float bestThreat = float.MaxValue;
        int bestSteps = int.MaxValue;
        string bestReason = "";
        int weaponRange = Mathf.Max(1, GetFireSupportMaxWeaponRange(passenger));
        float expectedThreat = CalculateThreatLevel(waitingAction.MoveTo, snapshot.AITeam);
        foreach (var pair in paths)
        {
            var cell = pair.Key;
            // No chase: keep the exact planned endpoint, or another boarding position at the SAME LZ.
            if (cell != waitingAction.MoveTo && SectorManager.HexDistance(cell, meeting.lzCell) != 1f) continue;
            if (cell != from && (occupied.Contains(cell)
                || (plannedDestinations != null && plannedDestinations.Contains(cell)))) continue;
            if (IsOwnProductionCell(cell, snapshot.AITeam)) continue;
            foreach (var enemy in enemies)
            {
                if (enemy == null || enemy.IsDead || enemy.IsEmbarked
                    || SectorManager.HexDistance(cell, enemy.CurrentCellPosition) > weaponRange
                    || !CanAttackTargetFrom(from, cell, passenger, enemy)
                    || !PassesAttackDecision(passenger, enemy, cell, false, out var decision)
                    || !TrySimulateAttackForAI(passenger, enemy, cell, out var sim)
                    || !sim.Simulation.killGuaranteed || !sim.Simulation.attackerSurvives || sim.AttackerLoss > 1) continue;
                // Full position DPQ includes terrain/construction and unit rules, not terrain alone.
                int dpq = sim.AttackerDpq.Points;
                float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                if (threat > expectedThreat + 0.001f) continue;
                int steps = GetPathStepCount(paths, cell);
                bool better = dpq > bestDpq || (dpq == bestDpq && (sim.AttackerLoss < bestLoss
                    || (sim.AttackerLoss == bestLoss && (threat < bestThreat
                        || (Mathf.Approximately(threat, bestThreat) && steps < bestSteps)))));
                if (!better) continue;
                bestTarget = enemy; bestCell = cell; bestDpq = dpq; bestLoss = sim.AttackerLoss;
                bestThreat = threat; bestSteps = steps;
                bestReason = $"abate garantido, perda={sim.AttackerLoss} HP, DPQ={dpq}, encontro preservado; {decision}";
            }
        }
        if (bestTarget == null) return false;
        attack = BuildAttackBatch(passenger, snapshot.AITeam, from, bestCell,
            bestTarget.InstanceId.ToString(), bestTarget.CurrentCellPosition, paths);
        attack.DebugLabel = $"abate oportunista enquanto espera carona: #{bestTarget.InstanceId}; {bestReason}";
        Debug.Log($"{TL("Capturador")} {passenger.InstanceId} ataca #{bestTarget.InstanceId} via {bestCell} "
            + $"enquanto espera #{meeting.transporter.InstanceId} na LZ {meeting.lzCell}: {bestReason}");
        TraceAIReviewCandidate("Capturador.CaronaCombate", "Attack", bestCell, bestTarget.InstanceId.ToString(), true,
            "ride_wait_safe_kill", bestReason);
        return true;
    }

    /// <summary>
    /// Um lugar so descreve o pedido de carona do capturador. Os dois pontos
    /// que precisam da reivindicacao - a avaliacao de carona e a ancora do
    /// rogue - tem que montar o MESMO pedido, senao caem em entradas
    /// diferentes do cache e voltam a divergir por outro caminho.
    /// </summary>
    private QueroCaronaRequest BuildCapturerRideRequest(
        UnitManager unit,
        SectorObjective assigned = null)
    {
        return new QueroCaronaRequest
        {
            unit = unit,
            map = boardTilemap,
            terrainDatabase = terrainDatabase,
            context = assigned != null
                ? QueroCaronaContext.ComPlano
                : QueroCaronaContext.SemPlano,
            plannedSector = assigned != null
                ? assigned.Sector
                : ConstructionSector.None,
            operationalTurns = CapturerRideOperationalTurns,
            emulateUnderRepairFromUnitData = false
        };
    }

    /// <summary>
    /// "Eu chego sozinho no MEU alvo?" — e o alvo vem da alocacao, nao do
    /// servico de carona.
    ///
    /// A ORDEM ESTAVA INVERTIDA. Antes esta chamada so dizia "tenho plano" ou
    /// "sou rogue" e o QueroCarona ESCOLHIA um alvo para conseguir responder.
    /// Depois o capturador resolvia o alvo de novo, por outro caminho, e jogava
    /// o primeiro fora — duas resolucoes independentes para a mesma unidade, na
    /// mesma decisao, livres para discordar.
    ///
    /// Quem aloca e o matching 1:1, que ja escolheu UM alvo por capturador. Ler
    /// a alocacao e de graca e nao tem como divergir dela. Sem alvo alocado o
    /// pedido segue sem alvo explicito, e o servico cai na avaliacao de fome
    /// estrutural — que e a pergunta certa para quem nao recebeu nada.
    /// </summary>
    private QueroCaronaResult EvaluateCapturerRideNeed(
        UnitManager unit,
        SectorObjective assigned)
    {
        QueroCaronaRequest request =
            BuildCapturerRideRequestWithTarget(unit, assigned);

        QueroCaronaResult result = QueroCaronaService.Evaluate(request);

        ApplyRideWaitStamp(unit, result);

        string target = result.evaluatedConstruction != null
            ? $"{result.evaluatedConstruction.name}@{result.evaluatedTarget}"
            : result.evaluatedTarget.ToString();
        string routeCost = result.routeCost == int.MaxValue
            ? "-"
            : result.routeCost.ToString();
        Debug.Log(
            $"{TL("Capturador")} {unit.InstanceId} QueroCarona=" +
            $"{(result.wantsRide ? "SIM" : "NAO")} " +
            $"contexto={request.context} setor={request.plannedSector} " +
            // De onde veio o alvo. "reserva" = leu a alocacao do matching;
            // "servico" = nao havia alocacao e o QueroCarona resolveu sozinho.
            // Se isto disser "servico" para um capturador que deveria ter alvo,
            // o problema esta no matching, nao aqui.
            $"origemAlvo={(request.useExplicitTarget ? "reserva" : "servico")} " +
            $"emergencia={result.isEmergency} " +
            $"envelope={result.reach} custo={routeCost} " +
            $"alvo={target} motivo={result.reason}");
        return result;
    }

    /// <summary>
    /// O pedido de carona com o alvo ja resolvido, sem consultar nem carimbar
    /// nada. Separado do EvaluateCapturerRideNeed porque o observador do
    /// questionario precisa do MESMO pedido — o mesmo alvo e a mesma entrada de
    /// cache — e o carimbo de espera e efeito colateral que ele nao pode fazer.
    /// </summary>
    private QueroCaronaRequest BuildCapturerRideRequestWithTarget(
        UnitManager unit,
        SectorObjective assigned)
    {
        QueroCaronaRequest request =
            BuildCapturerRideRequest(unit, assigned);

        // Com plano, o endereco veio do planner e ja esta no Mission Intent.
        // MelhorCaptura pertence somente ao ramo sem plano. Ambos terminam no
        // mesmo request explicito, entao o QueroCarona mede alcance sem escolher
        // outro predio por conta propria.
        if (assigned != null
            && TryResolveUnitDesignatedCaptureTarget(
                unit, out ConstructionManager plannedTarget))
        {
            Vector3Int planned = plannedTarget.CurrentCellPosition;
            planned.z = 0;
            request.useExplicitTarget = true;
            request.explicitTarget = planned;
            request.explicitTargetLabel =
                $"alvo do plano {assigned.Sector} " +
                $"{plannedTarget.ConstructionDisplayName}@{planned}";
        }
        else
        {
            CaptureOpportunityClaimSnapshot allocation =
                CaptureOpportunityClaimService.GetOrBuild(request);
            if (allocation.TryGetClaimForUnit(
                    unit, out CaptureOpportunityClaim claim)
                && claim.Construction != null)
            {
                Vector3Int claimed =
                    claim.Construction.CurrentCellPosition;
                claimed.z = 0;
                request.useExplicitTarget = true;
                request.explicitTarget = claimed;
                request.explicitTargetLabel =
                    $"alvo rogue reservado " +
                    $"{claim.Construction.ConstructionDisplayName}" +
                    $"@{claimed}";
            }
            else if (allocation.TryGetUnmatched(
                         unit,
                         out CaptureOpportunityUnmatched unmatched)
                     && unmatched.MagneticTarget != null)
            {
                Vector3Int magnetic =
                    unmatched.MagneticTarget.CurrentCellPosition;
                magnetic.z = 0;
                request.useExplicitTarget = true;
                request.explicitTarget = magnetic;
                request.explicitTargetLabel =
                    $"magnetico sem reserva " +
                    $"{unmatched.MagneticTarget.ConstructionDisplayName}" +
                    $"@{magnetic} ({unmatched.Reason})";
            }
        }

        return request;
    }

    // Orcamento do "trabalha enquanto espera": o mesmo da matriz de atribuicao do
    // planner (PlanBudget). Mede alcance A PE no componente da propria unidade.
    private const int WorkWhileWaitingBudget = 30;

    /// <summary>
    /// Capturador ilhado (o alvo do plano so se alcanca de carona) e sem carona
    /// materializavel neste turno: em vez de segurar, vai ao capturavel mais perto que
    /// ele alcanca a pe. Captura se chega; senao avanca pelo caminho valido ate ele.
    /// Respeita o predio que e alvo do plano de OUTRO capturador e o destino ja
    /// planejado por quem agiu antes. Mesma politica do perfil do oportunista
    /// (FazCapturaOportunista).
    /// </summary>
    private bool TryBuildWorkWhileWaitingForRide(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out PlayerAction action)
    {
        action = null;
        if (!FazCapturaOportunista || unit == null || snapshot == null || paths == null)
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> longPaths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, WorkWhileWaitingBudget, terrainDatabase);
        if (longPaths == null || longPaths.Count == 0)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        ConstructionManager best = null;
        Vector3Int bestCell = fromCell;
        int bestSteps = int.MaxValue;
        foreach (ConstructionManager building in ConstructionManager.AllActive)
        {
            if (building == null || !building.IsCapturable)
                continue;
            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            if (cell == fromCell)
                continue;
            if (!longPaths.TryGetValue(cell, out List<Vector3Int> route) || route == null || route.Count == 0)
                continue;
            if (plannedDestinations != null && plannedDestinations.Contains(cell))
                continue;
            // Aliado JA em cima do predio: e dele (AI Step T3 amarelo: #19 voltou para a
            // base atras do predio em que o #10 estava parado).
            if (UnitOccupancyRules.HasBlockingOccupantForUnitAtCell(
                    boardTilemap, cell, unit, alliedOnly: true))
                continue;
            if (!SimulateCaptureSensor(unit, cell, out _))
                continue;
            // Mesma cessao do oportunista: designado pelo plano OU capturador mais perto.
            if (ShouldReserveOpportunisticCaptureForCloserUnit(
                    unit, snapshot.AITeam, cell, longPaths, out UnitManager reservedFor)
                && reservedFor != null && reservedFor != unit)
                continue;
            if (route.Count < bestSteps)
            {
                best = building;
                bestCell = cell;
                bestSteps = route.Count;
            }
        }
        if (best == null)
            return false;

        // Registra a aproximacao: o veiculo le isto na hora de escolher quem tirar da corrida.
        CaptureApproachLedger.Record(unit, bestCell, bestSteps, snapshot.TurnNumber);

        // Chega neste turno: captura.
        if (paths.ContainsKey(bestCell) && !occupied.Contains(bestCell))
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} trabalha enquanto espera carona: " +
                      $"captura {best.Sector} @ {bestCell} (plano segue pedindo carona)");
            action = BuildCaptureBatch(unit, snapshot.AITeam, fromCell, bestCell, paths);
            return true;
        }

        // Senao, o ponto mais adiantado do caminho longo que cabe nesta rodada.
        List<Vector3Int> longRoute = longPaths[bestCell];
        Vector3Int moveCell = fromCell;
        for (int i = longRoute.Count - 1; i >= 0; i--)
        {
            Vector3Int step = longRoute[i];
            step.z = 0;
            if (step == fromCell)
                break;
            if (paths.ContainsKey(step) && !occupied.Contains(step)
                && (plannedDestinations == null || !plannedDestinations.Contains(step)))
            {
                moveCell = step;
                break;
            }
        }
        if (moveCell == fromCell)
            return false;

        Debug.Log($"{TL("Capturador")} {unit.InstanceId} trabalha enquanto espera carona: " +
                  $"avanca via {moveCell} para {best.Sector} @ {bestCell} ({bestSteps} passos a pe; " +
                  "plano segue pedindo carona)");
        action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        action.DebugLabel = $"trabalha enquanto espera carona: rumo a {best.Sector}";
        return true;
    }

    /// <summary>
    /// Capturador ilhado sem trabalho a pe e sem carona: escolhe, entre as celulas que ele
    /// alcanca A PE no proprio componente (caminho valido, orcamento do plano), a mais perto
    /// do alvo do plano — na ilha, a cabeca de praia voltada para la — e avanca pelo caminho
    /// ate onde der nesta rodada. Ja estando no ponto mais perto (empate inclusive), nao anda:
    /// fica esperando, e o MelhorEmbarque encontra a LZ ali. Nunca termina em produtora
    /// propria nem em celula ocupada ou ja planejada.
    /// </summary>
    private bool TryBuildAdvanceToBeachhead(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Vector3Int target,
        out PlayerAction action)
    {
        action = null;
        target.z = 0;
        if (unit == null || snapshot == null || paths == null || target == Vector3Int.zero)
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> longPaths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, WorkWhileWaitingBudget, terrainDatabase);
        if (longPaths == null || longPaths.Count == 0)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        float currentDistance = SectorManager.HexDistance(fromCell, target);
        Vector3Int bestCell = fromCell;
        float bestDistance = currentDistance;
        int bestSteps = 0;
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> entry in longPaths)
        {
            Vector3Int cell = entry.Key;
            cell.z = 0;
            if (cell == fromCell || occupied.Contains(cell)
                || IsOwnProductionCell(cell, snapshot.AITeam)
                || (plannedDestinations != null && plannedDestinations.Contains(cell)))
                continue;
            float distance = SectorManager.HexDistance(cell, target);
            int steps = entry.Value != null ? entry.Value.Count : 0;
            if (distance < bestDistance || (distance == bestDistance && bestCell != fromCell && steps < bestSteps))
            {
                bestCell = cell;
                bestDistance = distance;
                bestSteps = steps;
            }
        }

        // Ja no ponto mais perto do alvo (ou sem ganho): espera aqui.
        if (bestCell == fromCell || bestDistance >= currentDistance)
            return false;

        List<Vector3Int> route = longPaths[bestCell];
        Vector3Int moveCell = fromCell;
        for (int i = route.Count - 1; i >= 0; i--)
        {
            Vector3Int step = route[i];
            step.z = 0;
            if (step == fromCell)
                break;
            if (paths.ContainsKey(step) && !occupied.Contains(step)
                && !IsOwnProductionCell(step, snapshot.AITeam)
                && (plannedDestinations == null || !plannedDestinations.Contains(step)))
            {
                moveCell = step;
                break;
            }
        }
        if (moveCell == fromCell)
            return false;

        Debug.Log($"{TL("Capturador")} {unit.InstanceId} sem trabalho a pe: avanca via {moveCell} " +
                  $"para a cabeca de praia {bestCell} (dist {bestDistance:F0}h do alvo {target}; " +
                  "espera a carona la)");
        action = BuildVacateBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        action.DebugLabel = "aguarda carona na cabeca de praia";
        return true;
    }

    private bool ShouldRogueCapturerFightBeforeTransport(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int pressureTarget = snapshot.EnemyHQ != null
            ? snapshot.EnemyHQ.CurrentCellPosition
            : fromCell;
        pressureTarget.z = 0;

        if (TryFindUnreservedOpportunisticCapture(unit, snapshot.AITeam, paths, occupied, pressureTarget, out Vector3Int captureCell, "rogue transporte"))
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} rogue ignora transporte: captura/pressao disponivel @ {captureCell}");
            return true;
        }

        if (HasAttackTargetAtCurrentPos(unit))
        {
            var stayTargets = new List<PodeMirarTargetOption>();
            PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                SensorMovementMode.MoveuParado, stayTargets);
            UnitManager stayBest = PickBestRogueTarget(stayTargets, snapshot.AITeam, unit, fromCell, false, out _);
            if (stayBest != null)
            {
                Debug.Log($"{TL("Capturador")} {unit.InstanceId} rogue ignora transporte: alvo atual {stayBest.UnitDisplayName}#{stayBest.InstanceId}");
                return true;
            }
        }

        var targets = new List<PodeMirarTargetOption>();
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (occupied.Contains(cell))
                continue;

            targets.Clear();
            PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                SensorMovementMode.MoveuAndando, targets, fromCell: cell);
            UnitManager bestTarget = PickBestRogueTarget(targets, snapshot.AITeam, unit, cell, false, out _);
            if (bestTarget == null)
                continue;

            Debug.Log($"{TL("Capturador")} {unit.InstanceId} rogue ignora transporte: ataque disponivel {bestTarget.UnitDisplayName}#{bestTarget.InstanceId} via {cell}");
            return true;
        }

        return false;
    }

}
