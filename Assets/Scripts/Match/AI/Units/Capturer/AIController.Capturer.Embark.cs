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
    private bool TryGetRallyAnchorCell(ConstructionSector sector, out Vector3Int anchor)
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
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} NAO embarca: montando massa no rally "
                + $"{assigned.Sector} (state={assigned.RallyState}) dentro do raio {RallyAssemblyForceRadius}h");
            return null;
        }

        // Gate único de necessidade. Não escolhe transporte, vaga ou LZ e não
        // materializa ação; apenas decide se vale iniciar os scans de embarque.
        QueroCaronaResult rideNeed =
            EvaluateCapturerRideNeed(unit, assigned);
        if (!rideNeed.wantsRide)
            return null;

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
                    continue;

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

        MelhorEmbarqueResult evaluated =
            MelhorEmbarqueService.EvaluateForPassenger(
                new MelhorEmbarquePassengerRequest
                {
                    passenger = passenger,
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

        // Farol provisorio desta Phase 2. Ajuda o casco que ainda vai decidir,
        // sem criar reserva nem alterar a missao confirmada do passageiro.
        transportPickupBeacons[best.transporter.InstanceId] =
            passenger.InstanceId;

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
                    out string progressionReason)
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
        return action;
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
                : QueroCaronaContext.RogueOuRebelde,
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
