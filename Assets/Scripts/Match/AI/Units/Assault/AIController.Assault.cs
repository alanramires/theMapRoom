using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private enum CombatPassengerTransportPolicy
    {
        Assault,
        FireSupport,
        AirSurveillance
    }

    private enum FireSupportTransportOutcome
    {
        Handled,
        NoAction,
        TransportRejected
    }

    private sealed class CombatPassengerTransportDecision
    {
        public CombatPassengerTransportPolicy policy;
        public QueroCaronaResult rideNeed;
        public UnitManager transporter;
        public MelhorEmbarqueOption option;
        public float policyScore;
    }

    private PlayerAction TryDecideCombatPassengerTransportAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        CombatPassengerTransportPolicy policy,
        SectorObjective assigned,
        QueroCaronaResult evaluatedRideNeed = null)
    {
        QueroCaronaResult rideNeed =
            evaluatedRideNeed
            ?? EvaluateCombatPassengerRideNeed(
                unit,
                policy,
                assigned);
        if (rideNeed == null || !rideNeed.wantsRide)
            return null;

        CombatPassengerTransportDecision decision =
            FindBestCombatPassengerTransportDecision(
                unit, snapshot, plan, policy, assigned, rideNeed);
        if (decision?.transporter == null
            || decision.option == null)
            return null;

        Vector3Int transporterCell =
            decision.transporter.CurrentCellPosition;
        transporterCell.z = 0;
        Vector3Int selectedLz = decision.option.lzCell;
        selectedLz.z = 0;
        Vector3Int passengerMeeting =
            decision.option.hasPassengerMeetingCell
                ? decision.option.passengerMeetingCell
                : selectedLz;
        passengerMeeting.z = 0;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit,
                Mathf.Max(0, unit.RemainingMovementPoints),
                terrainDatabase);

        if (selectedLz == transporterCell)
        {
            var adjacent = new List<PodeEmbarcarOption>();
            PodeEmbarcarSensor.CollectOptions(
                unit, boardTilemap, terrainDatabase,
                Mathf.Max(0, unit.RemainingMovementPoints), adjacent);
            PodeEmbarcarOption legal = adjacent.Find(option =>
                option?.transporterUnit == decision.transporter
                && option.transporterSlotIndex == decision.option.slotIndex);
            if (legal != null)
            {
                ClaimCombatPassengerTransportDecision(decision);
                string roleLabel;
                switch (policy)
                {
                    case CombatPassengerTransportPolicy.FireSupport:
                        roleLabel = TL("FireSupport");
                        break;
                    case CombatPassengerTransportPolicy.AirSurveillance:
                        roleLabel = TL("VigilanciaAerea");
                        break;
                    default:
                        roleLabel = TL("Assalto");
                        break;
                }
                Debug.Log(
                    $"{roleLabel} {unit.InstanceId} embarca policy={policy} → " +
                    $"{decision.transporter.InstanceId} slot " +
                    $"{decision.option.slotIndex} LZ={selectedLz} " +
                    $"tier={decision.option.transporterTier} " +
                    $"paxCost={decision.option.passengerRouteCost} " +
                    $"transportCost={decision.option.transporterRouteCost}");
                return BuildEmbarcarBatch(
                    unit, snapshot.AITeam, fromCell,
                    decision.transporter, decision.option.slotIndex, paths);
            }
        }

        return TryBuildCombatPassengerRendezvousAction(
            unit, snapshot, decision, fromCell,
            selectedLz, passengerMeeting, paths);
    }

    private PlayerAction TryBuildCombatPassengerRendezvousAction(
        UnitManager passenger,
        AIWorldSnapshot snapshot,
        CombatPassengerTransportDecision decision,
        Vector3Int fromCell,
        Vector3Int selectedLz,
        Vector3Int passengerMeeting,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        if (passenger == null || snapshot == null || decision?.transporter == null)
            return null;

        if (paths != null
            && passengerMeeting != fromCell
            && paths.ContainsKey(passengerMeeting))
        {
            Vector3Int liveTransporterCell =
                decision.transporter.CurrentCellPosition;
            liveTransporterCell.z = 0;
            if (selectedLz == liveTransporterCell)
            {
                List<Vector3Int> pathToMeeting = paths[passengerMeeting];
                int remainingMovement =
                    CalculateRemainingMovementAfterPath(passenger, pathToMeeting);
                if (PodeEmbarcarSensor.CanEmbarkFromProjectedCell(
                        passenger,
                        passengerMeeting,
                        decision.transporter,
                        decision.option.slotIndex,
                        boardTilemap,
                        terrainDatabase,
                        remainingMovement,
                        out int embarkCost,
                        out string embarkReason))
                {
                    ClaimCombatPassengerTransportDecision(decision);
                    Debug.Log(
                        $"{TL("Transporte")} passageiro #{passenger.InstanceId} " +
                        $"policy={decision.policy} move e embarca: encontroPax=" +
                        $"{passengerMeeting} LZTransport={selectedLz} " +
                        $"transportador=#{decision.transporter.InstanceId} " +
                        $"slot={decision.option.slotIndex} " +
                        $"movRest={remainingMovement} custoEmbarque={embarkCost}.");
                    var embarkPaths =
                        new Dictionary<Vector3Int, List<Vector3Int>>
                        {
                            [selectedLz] = pathToMeeting
                        };
                    return BuildEmbarcarBatch(
                        passenger,
                        snapshot.AITeam,
                        fromCell,
                        decision.transporter,
                        decision.option.slotIndex,
                        embarkPaths);
                }

                Debug.Log(
                    $"{TL("Transporte")} passageiro #{passenger.InstanceId} " +
                    $"policy={decision.policy} nao encadeia embarque projetado: " +
                    $"{embarkReason}");
            }

            ClaimCombatPassengerTransportDecision(decision);
            Debug.Log(
                $"{TL("Transporte")} passageiro #{passenger.InstanceId} " +
                $"policy={decision.policy} segue para encontroPax=" +
                $"{passengerMeeting} LZTransport={selectedLz} " +
                $"do transportador #{decision.transporter.InstanceId} " +
                $"tier={decision.option.transporterTier}.");
            return BuildMoveBatch(
                passenger, snapshot.AITeam, fromCell,
                passengerMeeting, paths);
        }

        if (passengerMeeting == fromCell)
        {
            ClaimCombatPassengerTransportDecision(decision);
            Debug.Log(
                $"{TL("Transporte")} passageiro #{passenger.InstanceId} " +
                $"policy={decision.policy} aguarda no encontroPax=" +
                $"{passengerMeeting} LZTransport={selectedLz} " +
                $"transportador=#{decision.transporter.InstanceId}.");
            return BuildMoveBatch(
                passenger, snapshot.AITeam, fromCell, fromCell, paths);
        }

        HashSet<Vector3Int> occupied = passenger.GetDomain() == Domain.Air
            ? BuildAirOccupied(passenger)
            : BuildOccupied(passenger);
        if (TryFindBestToolProgressionCell(
                passenger,
                snapshot,
                fromCell,
                passengerMeeting,
                paths,
                occupied,
                ToolProgressionIntent.TransportRendezvous,
                out Vector3Int progressionCell,
                out _,
                out string progressionReason)
            && progressionCell != fromCell)
        {
            ClaimCombatPassengerTransportDecision(decision);
            Debug.Log(
                $"{TL("Transporte")} passageiro #{passenger.InstanceId} " +
                $"policy={decision.policy} progride para encontroPax=" +
                $"{passengerMeeting} LZTransport={selectedLz} " +
                $"via={progressionCell} transportador=" +
                $"#{decision.transporter.InstanceId} " +
                $"({progressionReason}).");
            return BuildMoveBatch(
                passenger, snapshot.AITeam, fromCell,
                progressionCell, paths);
        }

        Debug.Log(
            $"{TL("Transporte")} passageiro #{passenger.InstanceId} " +
            $"policy={decision.policy} encontroPax={passengerMeeting} " +
            $"LZTransport={selectedLz} sem progressão " +
            "materializável; libera o papel para outra ação.");
        return null;
    }

    private void ClaimCombatPassengerTransportDecision(
        CombatPassengerTransportDecision decision)
    {
        if (decision?.transporter == null || decision.option?.passenger == null)
            return;

        // Reserva apenas a decisão desta passada da Phase 2. Não altera unidade,
        // ocupação ou qualquer verdade confirmada do tabuleiro.
        assignedTransportClaims[decision.transporter.InstanceId] =
            decision.option.passenger.InstanceId;
    }

    private QueroCaronaResult EvaluateCombatPassengerRideNeed(
        UnitManager unit,
        CombatPassengerTransportPolicy policy,
        SectorObjective assigned)
    {
        QueroCaronaContext context = assigned != null
            ? QueroCaronaContext.ComPlano
            : QueroCaronaContext.RogueOuRebelde;
        QueroCaronaResult result = QueroCaronaService.Evaluate(
            new QueroCaronaRequest
            {
                unit = unit,
                map = boardTilemap,
                terrainDatabase = terrainDatabase,
                context = context,
                plannedSector = assigned != null
                    ? assigned.Sector
                    : ConstructionSector.None,
                operationalTurns = 2,
                emulateUnderRepairFromUnitData = false
            });
        Debug.Log(
            $"{TL("Transporte")} {unit.InstanceId} policy={policy} " +
            $"QueroCarona={(result.wantsRide ? "SIM" : "NAO")} " +
            $"contexto={context} setor=" +
            $"{(assigned != null ? assigned.Sector.ToString() : "rogue")} " +
            $"emergencia={result.isEmergency} reach={result.reach} " +
            $"custo={(result.routeCost == int.MaxValue ? "-" : result.routeCost.ToString())} " +
            $"motivo={result.reason}");
        return result;
    }

    private CombatPassengerTransportDecision
        FindBestCombatPassengerTransportDecision(
            UnitManager unit,
            AIWorldSnapshot snapshot,
            TeamObjectivePlan plan,
            CombatPassengerTransportPolicy policy,
            SectorObjective assigned,
            QueroCaronaResult rideNeed)
    {
        CombatPassengerTransportDecision best = null;
        int compatibleTransporters = 0;
        int offeredRendezvous = 0;
        int rejectedByPolicy = 0;
        foreach (UnitManager transporter in UnitManager.AllActive)
        {
            if (transporter == null
                || transporter == unit
                || transporter.IsDead
                || transporter.IsEmbarked
                || transporter.IsUnderRepair
                || !PlayerSlotRelations.AreAllies(unit, transporter)
                || !transporter.TryGetUnitData(
                    out UnitData transporterData)
                || transporterData == null
                || !transporterData.isTransporter)
                continue;
            if (assignedTransportClaims.TryGetValue(
                    transporter.InstanceId, out int claimedPassengerId)
                && claimedPassengerId != unit.InstanceId)
                continue;

            if (!MelhorEmbarqueService
                    .TryResolveCompatiblePassengerSlot(
                        transporter,
                        unit,
                        out _,
                        out _))
            {
                AIDecisionPerf.AddCount(
                    "MelhorEmbarqueCompatibilityRejects");
                continue;
            }

            compatibleTransporters++;
            TransportPlanningSnapshot transportPlanning =
                GetOrCreateTransportPlanningSnapshot(
                    transporter, snapshot, plan);
            MelhorEmbarqueResult evaluated =
                GetOrBuildTransportPassengerProjection(
                    transportPlanning, unit, rideNeed);
            MelhorEmbarqueOption option =
                evaluated.options.Find(candidate =>
                    candidate?.passenger == unit
                    && (candidate.rideDisposition ==
                            MelhorEmbarqueRideDisposition.Requested
                        || candidate.rideDisposition ==
                            MelhorEmbarqueRideDisposition.Emergency));
            if (option == null)
                continue;
            if (!option.hasPassengerMeetingCell
                && unit.GetDomain() != Domain.Air)
            {
                Debug.Log(
                    $"{TL("Transporte")} {unit.InstanceId} rejeita " +
                    $"#{transporter.InstanceId} LZ={option.lzCell}: " +
                    "nenhum hex de encontro transitável pelo passageiro.");
                rejectedByPolicy++;
                continue;
            }
            offeredRendezvous++;

            // Guarda provisoria para pecas rebocadas, hoje a Artilharia de
            // Campanha. Ela pode pedir carona fora de IsInvading, mas nao deve
            // embarcar para uma hotzone enquanto ainda nao existir uma rota de
            // retaguarda ou zona segura de desembarque. A avaliacao propria de
            // posicao de artilharia substituira esta regra quando existir.
            if (policy == CombatPassengerTransportPolicy.FireSupport
                && UnitNeedsTow(unit))
            {
                Vector3Int safetyTarget = rideNeed.evaluatedTarget;
                safetyTarget.z = 0;
                if (assigned == null
                    && rideNeed.evaluatedConstruction == null
                    && TryFindTowDeliveryTarget(
                        unit, unit.CurrentCellPosition, snapshot, plan,
                        out Vector3Int fireSupportTarget))
                {
                    safetyTarget = fireSupportTarget;
                    safetyTarget.z = 0;
                }

                if (!CanFireSupportTowEmbarkSafely(
                        unit, transporter, snapshot, plan,
                        unit.CurrentCellPosition, safetyTarget,
                        out string safetyReason))
                {
                    Debug.Log(
                        $"{TL("FireSupport")} {unit.InstanceId} rejeita " +
                        $"#{transporter.InstanceId} LZ={option.lzCell}: " +
                        safetyReason);
                    rejectedByPolicy++;
                    continue;
                }
            }

            float policyScore = option.score
                + ResolveCombatPassengerTransportPolicyAdjustment(
                    transporter, assigned, plan);
            if (best == null
                || IsBetterCombatPassengerTransportOption(
                    option, policyScore,
                    best.option, best.policyScore))
            {
                best = new CombatPassengerTransportDecision
                {
                    policy = policy,
                    rideNeed = rideNeed,
                    transporter = transporter,
                    option = option,
                    policyScore = policyScore
                };
            }
        }

        if (best != null)
        {
            Debug.Log(
                $"{TL("Transporte")} {unit.InstanceId} policy={policy} " +
                $"MelhorEmbarque transporter=#{best.transporter.InstanceId} " +
                $"LZ={best.option.lzCell} encontroPax=" +
                $"{best.option.passengerMeetingCell} " +
                $"slot={best.option.slotIndex} " +
                $"tier={best.option.transporterTier} " +
                $"route={best.option.passengerRouteState} " +
                $"ride={best.option.rideDisposition} " +
                $"paxCost={best.option.passengerRouteCost} " +
                $"transportCost={best.option.transporterRouteCost} " +
                $"score={best.policyScore:F0}");
        }
        else
        {
            Debug.Log(
                $"{TL("Transporte")} {unit.InstanceId} policy={policy} " +
                "sem MelhorEmbarque materializavel " +
                $"transporters={compatibleTransporters} " +
                $"rendezvous={offeredRendezvous} " +
                $"policyRejected={rejectedByPolicy}.");
        }
        return best;
    }

    private static bool IsBetterCombatPassengerTransportOption(
        MelhorEmbarqueOption candidate,
        float candidatePolicyScore,
        MelhorEmbarqueOption current,
        float currentPolicyScore)
    {
        if (current == null)
            return true;
        int byTier = candidate.transporterTier.CompareTo(
            current.transporterTier);
        if (byTier != 0)
            return byTier < 0;
        if (!Mathf.Approximately(
                candidatePolicyScore, currentPolicyScore))
            return candidatePolicyScore > currentPolicyScore;
        int candidateTotalCost =
            candidate.passengerTotalCost >= 0
                ? candidate.passengerTotalCost
                : candidate.passengerRouteCost;
        int currentTotalCost =
            current.passengerTotalCost >= 0
                ? current.passengerTotalCost
                : current.passengerRouteCost;
        if (candidateTotalCost != currentTotalCost)
            return candidateTotalCost < currentTotalCost;
        return candidate.transporterDistance
            < current.transporterDistance;
    }

    private float ResolveCombatPassengerTransportPolicyAdjustment(
        UnitManager transporter,
        SectorObjective passengerObjective,
        TeamObjectivePlan plan)
    {
        SectorObjective transportObjective = plan != null
            ? ResolveAssignedTransportObjective(transporter, plan)
            : null;
        if (passengerObjective == null)
            return transportObjective == null ? 500f : 0f;
        if (transportObjective == null)
            return 250f;
        if (transportObjective.Sector == passengerObjective.Sector)
            return 3000f;
        return AreEmbarkSectorsCompatible(
            passengerObjective.Sector, transportObjective.Sector)
                ? 1000f
                : -1000f;
    }

    private const int AssaultScoutZoneRadius = 2;
    // Se qualquer slot de Capturador no objetivo tiver DistanceToObjective ≤ esse limiar,
    // o escort entra em "advance mode": prioriza avançar ao objetivo em vez de patrulhar.
    private const int AdvancedCapturerThreshold = 6;
    // Penalidade por congestionamento à frente: vizinhos com custo menor (próximo passo de rota)
    // que estão bloqueados por aliados. Ratio 0..1 × esse peso é subtraído do score.
    private const float ForwardCongestionWeight = 700f;

    // -------------------------------------------------------------------------
    // Assalto Batedor - protege e varre a zona do objetivo de captura atribuido.
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideAssaultAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        bool allowTransport = true)
    {
        if (unit == null || snapshot == null || plan == null)
            return null;
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || !UnitRoleCompatibility.CanSatisfy(data, UnitRole.Assalto))
            return null;
        SectorObjective assigned = ResolveAssignedAssaultObjective(unit, plan);

        if (assigned == null)
        {
            if (TryFindCriticalHomeDefenseObjectiveForUnit(plan, snapshot.AITeam, unit, unit.CurrentCellPosition, "Assalto Rogue", out SectorObjective rogueCriticalHome))
            {
                Debug.Log($"{TL("Assalto")} {unit.InstanceId} rogue redireciona -> {rogueCriticalHome.Sector}: Base/HQ sob ameaca");
                return DecideAssignedAssaultEscortAction(unit, snapshot, rogueCriticalHome);
            }

            if (allowTransport)
            {
                PlayerAction embarkAction =
                    TryDecideCombatPassengerTransportAction(
                        unit, snapshot, plan,
                        CombatPassengerTransportPolicy.Assault,
                        assigned: null);
                if (embarkAction != null) return embarkAction;
            }
            return DecideRogueAssaultBreakerAction(unit, snapshot, plan);
        }

        if (!IsCriticalHomeDefenseObjective(assigned, snapshot.AITeam)
            && TryFindCriticalHomeDefenseObjectiveForUnit(plan, snapshot.AITeam, unit, unit.CurrentCellPosition, "Assalto", out SectorObjective criticalHome))
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} redireciona {assigned.Sector} -> {criticalHome.Sector}: Base/HQ sob ameaca");
            assigned = criticalHome;
        }

        if (IsActiveRallyAssemblyObjective(assigned))
            return DecideRallyAssemblyAssaultAction(unit, snapshot, assigned);

        // Assalto com plano também pode depender de transporte. Antes, somente
        // o rogue passava por esta avaliação; um tanque separado de Charlie
        // pelo mar caía direto no batedor e "mantinha patrulha", mesmo com um
        // navio compatível disponível para embarque.
        PlayerAction assignedEmbarkAction = allowTransport
            ? TryDecideCombatPassengerTransportAction(
                unit, snapshot, plan,
                CombatPassengerTransportPolicy.Assault,
                assigned)
            : null;
        if (assignedEmbarkAction != null)
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} {assigned.Sector} " +
                      $"prioriza embarque: rota terrestre insuficiente.");
            return assignedEmbarkAction;
        }

        return DecideAssignedAssaultEscortAction(unit, snapshot, assigned);
    }

    private PlayerAction DecideAssignedAssaultEscortAction(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Vector3Int scoutAnchorCell = ResolveAssaultEscortCell(assigned, snapshot.AITeam, fromCell);
        int scoutZoneRadius = ResolveAssaultScoutZoneRadius(unit, assigned);

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        if (TryFindAssaultCaptureTargetVacateAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction targetVacateAction))
            return targetVacateAction;

        // Se o escort está no corredor de avanço do capturador, exclui a célula atual
        // do patrol para forçar movimento real e liberar o caminho.
        if (TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction assignedVacateAction))
            return assignedVacateAction;

        TeamObjectivePlan escortPlan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        bool inCorridor = escortPlan != null && IsAssaultEscortInCapturerCorridor(unit, fromCell, escortPlan, snapshot.AITeam);
        if (inCorridor)
        {
            occupied.Add(fromCell);
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — cede corredor, força movimento");
        }

        List<UnitManager> threats = CollectAssaultEscortThreats(snapshot.AITeam, scoutAnchorCell, scoutZoneRadius);
        AddAssaultEscortTravelThreats(snapshot.AITeam, fromCell, paths, threats);
        bool defensiveContext = assigned.Status == ObjectiveStatus.Defending;
        if (TryFindAssaultEscortAttack(unit, snapshot, fromCell, scoutAnchorCell, scoutZoneRadius, defensiveContext, paths, occupied, threats,
                out Vector3Int attackCell, out UnitManager attackTarget, out string attackReason))
        {
            Vector3Int targetCell = attackTarget.CurrentCellPosition; targetCell.z = 0;
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — ataca via {attackCell} → {attackTarget.UnitDisplayName}#{attackTarget.InstanceId} ({attackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                attackTarget.InstanceId.ToString(), targetCell, paths);
        }

        int bestCapturerDist = GetBestCapturerDistanceToObjective(assigned);
        bool escortAdvanceMode = bestCapturerDist >= 0 && bestCapturerDist <= AdvancedCapturerThreshold;
        if (escortAdvanceMode)
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — ADVANCE MODE: capturador mais próximo a {bestCapturerDist}PM de {assigned.Sector}");

        if (escortAdvanceMode
            && TryFindAssaultAdvanceRouteAttack(unit, snapshot, fromCell, scoutAnchorCell, defensiveContext, paths, occupied,
                out Vector3Int routeAttackCell, out UnitManager routeAttackTarget, out string routeAttackReason))
        {
            Vector3Int targetCell = routeAttackTarget.CurrentCellPosition; targetCell.z = 0;
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — intercepta via {routeAttackCell} → {routeAttackTarget.UnitDisplayName}#{routeAttackTarget.InstanceId} ({routeAttackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, routeAttackCell,
                routeAttackTarget.InstanceId.ToString(), targetCell, paths);
        }

        List<Vector3Int> suspectCells = CollectSweepSuspectCells(snapshot.AITeam, scoutAnchorCell, scoutZoneRadius);
        if (TryFindAssaultScoutRevealMove(unit, snapshot, fromCell, scoutAnchorCell, scoutZoneRadius, paths, occupied, suspectCells,
                out Vector3Int revealCell, out string revealReason))
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — abre FoW via {revealCell} ({revealReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, revealCell, paths);
        }

        Vector3Int coverCell = FindAssaultEscortCoverCell(unit, snapshot, fromCell, scoutAnchorCell, scoutZoneRadius, paths, occupied, threats, bestCapturerDist, out string coverEvaluationLog);
        if (!string.IsNullOrEmpty(coverEvaluationLog))
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — HexEvaluator.Batedor target={scoutAnchorCell} zona={scoutZoneRadius}h advanceMode={escortAdvanceMode} melhorCapt={bestCapturerDist}PM\n{coverEvaluationLog}");
        if (coverCell != fromCell)
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — patrulha via {coverCell}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, coverCell, paths);
        }

        Debug.Log($"{TL("Assalto")} {unit.InstanceId} batedor {assigned.Sector} — mantém patrulha");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private bool TryFindAssaultCaptureTargetVacateAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        if (plan == null || !IsOtherAssignedCapturerTarget(fromCell, unit, null, plan, snapshot.AITeam))
            return false;

        if (TryFindAssaultCaptureTargetVacateAttackAction(unit, snapshot, fromCell, paths, occupied, plan, out action))
            return true;

        Vector3Int bestCell = fromCell;
        float bestScore = float.MinValue;
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;
            if (occupied != null && occupied.Contains(cell))
                continue;
            if (IsOtherAssignedCapturerTarget(cell, unit, null, plan, snapshot.AITeam))
                continue;

            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            if (construction != null && construction.CanProduceUnitsForSlot(snapshot.AISlotIndex))
                continue;

            int pathCost = GetPathStepCount(paths, cell);
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float dpq = GetTerrainDpqPontos(cell);
            float score =
                dpq * 90f
                - threat * 70f
                - pathCost * 25f
                - SectorManager.HexDistance(fromCell, cell) * 10f;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        if (bestCell == fromCell)
            return false;

        Debug.Log($"{TL("Assalto")} {unit.InstanceId} cede predio-alvo de capturador {fromCell} via {bestCell}");
        action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestCell, paths);
        return true;
    }

    private PlayerAction DecideGroundAntiAirAssaultAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan, SectorObjective assigned)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        if (TryFindGroundAntiAirAttack(unit, snapshot, fromCell, paths, occupied,
                out Vector3Int attackCell, out UnitManager target, out string attackReason))
        {
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} AAA intercepta via {attackCell} -> {target.UnitDisplayName}#{target.InstanceId} ({attackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                target.InstanceId.ToString(), targetCell, paths);
        }

        if (TryFindGroundAntiAirEscortGroupMove(unit, snapshot, assigned, fromCell, paths, occupied, out Vector3Int escortCell, out string escortReason))
        {
            if (escortCell != fromCell)
            {
                Debug.Log($"{TL("Assalto")} {unit.InstanceId} AAA escolta {assigned.Sector} via {escortCell} ({escortReason})");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, escortCell, paths);
            }

            Debug.Log($"{TL("Assalto")} {unit.InstanceId} AAA mantem escolta {assigned.Sector} ({escortReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        if (TryFindGroundAntiAirCohesionMove(unit, snapshot, fromCell, paths, occupied, out Vector3Int moveCell, out string moveReason)
            && moveCell != fromCell)
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} AAA coesao base via {moveCell} ({moveReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        Debug.Log($"{TL("Assalto")} {unit.InstanceId} AAA sem alvo aereo - segura cobertura base");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private bool TryFindGroundAntiAirAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out UnitManager bestTarget,
        out string bestReason)
    {
        bestCell = fromCell;
        bestTarget = null;
        bestReason = "";
        float bestScore = float.MinValue;
        List<UnitManager> enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        if (enemies == null || enemies.Count == 0)
            return false;
        int visibleAir = 0;
        int hotzoneAir = 0;
        int sensorAttackableAir = 0;
        int decisionRejectedAir = 0;
        string lastDecisionRejection = "";

        Vector3Int home = snapshot.MyHQ != null ? snapshot.MyHQ.CurrentCellPosition : fromCell;
        home.z = 0;
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;

            foreach (UnitManager enemy in enemies)
            {
                if (enemy == null || enemy.GetDomain() != Domain.Air)
                    continue;
                visibleAir++;
                Vector3Int enemyCell = enemy.CurrentCellPosition;
                enemyCell.z = 0;
                UnitThreatEnvelope envelope = GetAIThreatEnvelope(unit);
                if (envelope != null && envelope.CanThreaten(enemyCell))
                    hotzoneAir++;
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy))
                    continue;
                sensorAttackableAir++;
                if (!PassesAttackDecision(unit, enemy, cell, false, out string attackDecisionReason))
                {
                    decisionRejectedAir++;
                    lastDecisionRejection = attackDecisionReason;
                    continue;
                }

                float score =
                    150000f
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 1000f
                    - SectorManager.HexDistance(cell, enemyCell) * 250f
                    - SectorManager.HexDistance(cell, home) * 45f
                    - CalculateThreatLevel(cell, snapshot.AITeam) * 80f
                    - GetPathStepCount(paths, cell) * 20f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestCell = cell;
                bestTarget = enemy;
                bestReason = $"airOnly {attackDecisionReason}";
            }
        }

        if (bestTarget == null)
        {
            Debug.Log(
                $"{TL("AntiaereoCombatente")} {unit.InstanceId} diagnostico sem tiro " +
                $"visibleAirChecks={visibleAir} hotzoneChecks={hotzoneAir} " +
                $"sensorAttackableChecks={sensorAttackableAir} " +
                $"decisionRejectedChecks={decisionRejectedAir} " +
                $"lastDecision=[{lastDecisionRejection}] paths={paths.Count}");
        }
        return bestTarget != null;
    }

    private bool TryFindGroundAntiAirEscortGroupMove(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = "";
        if (unit == null || snapshot == null || assigned == null || assigned.Slots == null || paths == null || paths.Count == 0)
            return false;
        if (IsCriticalHomeDefenseObjective(assigned, snapshot.AITeam))
            return false;

        Vector3Int objectiveCell = ResolveAssaultEscortCell(assigned, snapshot.AITeam, fromCell);
        UnitManager anchor = null;
        Vector3Int anchorCell = fromCell;
        float bestAnchorScore = float.MinValue;

        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!slot.Filled || slot.AssignedUnitId == unit.InstanceId)
                continue;
            if (slot.Role != UnitRole.Capturador && slot.Role != UnitRole.Assalto)
                continue;

            UnitManager ally = FindActiveUnit(slot.AssignedUnitId, snapshot.AITeam);
            if (ally == null || ally.IsEmbarked || ally.GetDomain() == Domain.Air)
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float score =
                -SectorManager.HexDistance(allyCell, objectiveCell) * 220f
                -SectorManager.HexDistance(allyCell, fromCell) * 90f
                + (slot.Role == UnitRole.Capturador ? 650f : 300f);
            if (slot.DistanceToObjective >= 0)
                score -= slot.DistanceToObjective * 25f;

            if (score <= bestAnchorScore)
                continue;

            bestAnchorScore = score;
            anchor = ally;
            anchorCell = allyCell;
        }

        if (anchor == null)
            return false;

        float currentAnchorDist = SectorManager.HexDistance(fromCell, anchorCell);
        bool currentProducer = IsOwnProductionCell(fromCell, snapshot.AITeam);
        if (!currentProducer && currentAnchorDist >= 1f && currentAnchorDist <= 2f)
        {
            reason = $"grupo={anchor.InstanceId} dist={currentAnchorDist:F0} objetivoDist={SectorManager.HexDistance(fromCell, objectiveCell):F0}";
            return true;
        }

        float bestScore = float.MinValue;
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;
            bool cellProducer = IsOwnProductionCell(cell, snapshot.AITeam);
            if (cellProducer && cell != fromCell)
                continue;

            float anchorDist = SectorManager.HexDistance(cell, anchorCell);
            if (anchorDist <= 0f)
                continue;
            float escortBandPenalty = anchorDist <= 2f ? 0f : anchorDist - 2f;
            float objectiveDist = SectorManager.HexDistance(cell, objectiveCell);
            float score =
                -escortBandPenalty * 1200f
                -Mathf.Abs(anchorDist - 1.5f) * 220f
                -objectiveDist * 40f
                -CalculateThreatLevel(cell, snapshot.AITeam) * 85f
                -GetPathStepCount(paths, cell) * 35f
                + GetTerrainDpqPontos(cell) * 30f
                - (cellProducer ? 1800f : 0f);
            if (anchorDist < currentAnchorDist)
                score += 500f;
            if (cell == fromCell && anchorDist <= 2f && !cellProducer)
                score += 450f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestCell = cell;
            reason = $"grupo={anchor.InstanceId} dist={anchorDist:F0} objetivoDist={objectiveDist:F0}";
        }

        return !string.IsNullOrEmpty(reason);
    }

    private bool TryFindGroundAntiAirCohesionMove(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = "";
        Vector3Int home = snapshot.MyHQ != null ? snapshot.MyHQ.CurrentCellPosition : fromCell;
        home.z = 0;
        float fromHomeDist = SectorManager.HexDistance(fromCell, home);
        bool onProducer = IsOwnProductionCell(fromCell, snapshot.AITeam);
        if (!onProducer && fromHomeDist <= 3f)
            return false;

        float bestScore = float.MinValue;
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;
            bool cellProducer = IsOwnProductionCell(cell, snapshot.AITeam);
            if (cellProducer && cell != fromCell)
                continue;

            float homeDist = SectorManager.HexDistance(cell, home);
            float targetBand = homeDist <= 3f ? 0f : homeDist - 3f;
            float score =
                -targetBand * 900f
                - (cellProducer ? 1500f : 0f)
                - CalculateThreatLevel(cell, snapshot.AITeam) * 80f
                - GetPathStepCount(paths, cell) * 30f
                + GetTerrainDpqPontos(cell) * 35f;
            if (homeDist < fromHomeDist)
                score += 450f;
            if (onProducer && !cellProducer)
                score += 1200f;

            if (score <= bestScore)
                continue;
            bestScore = score;
            bestCell = cell;
            reason = $"homeDist={homeDist:F0} producer={cellProducer}";
        }

        return bestCell != fromCell;
    }

    private bool IsOwnProductionCell(Vector3Int cell, TeamId aiTeam)
    {
        cell.z = 0;
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        return construction != null && construction.CanProduceUnitsForSlot(AIController.ResolveAISlotKey(aiTeam));
    }

    private static bool IsGroundAntiAirOnlyAssault(UnitData data)
    {
        if (data == null || data.domain != Domain.Land || data.roles == null || data.roles.Count == 0)
            return false;
        if (data.roles[0] != UnitRole.Assalto)
            return false;
        if (data.embarkedWeapons == null || data.embarkedWeapons.Count == 0)
            return false;
        foreach (UnitEmbarkedWeapon ew in data.embarkedWeapons)
        {
            if (ew?.weapon == null)
                continue;
            if (ew.weapon.WeaponCategory != WeaponCategory.AntiAerea)
                return false;
        }
        return true;
    }

    private bool TryFindAssaultCaptureTargetVacateAttackAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamObjectivePlan plan,
        out PlayerAction action)
    {
        action = null;
        List<UnitManager> enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        if (enemies == null || enemies.Count == 0)
            return false;

        bool preferDpq = unit.TryGetUnitData(out UnitData attackerUd) && attackerUd != null && attackerUd.prioritizeDpqAtBattle;
        float dpqWeight = preferDpq ? 2000f : 40f;
        Vector3Int enemyHqCell = snapshot.EnemyHQ != null
            ? snapshot.EnemyHQ.CurrentCellPosition
            : fromCell;
        enemyHqCell.z = 0;

        Vector3Int bestCell = fromCell;
        UnitManager bestTarget = null;
        string bestReason = "";
        float bestScore = float.MinValue;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;
            if (occupied != null && occupied.Contains(cell))
                continue;
            if (IsOtherAssignedCapturerTarget(cell, unit, null, plan, snapshot.AITeam))
                continue;

            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            if (construction != null && construction.CanProduceUnitsForSlot(snapshot.AISlotIndex))
                continue;

            foreach (UnitManager enemy in enemies)
            {
                if (!PassesFireSupportRoleTargetFilter(unit, enemy))
                    continue;
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy))
                    continue;
                if (!PassesAttackDecision(unit, enemy, cell, false, out string attackDecisionReason))
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition;
                enemyCell.z = 0;
                ConstructionManager enemyBldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, enemyCell);
                bool inOwnConstruction = enemyBldg != null && enemyBldg.SlotIndex == snapshot.AISlotIndex;
                bool inConstruction = enemyBldg != null;
                float constructionBonus = inOwnConstruction ? 20000f : inConstruction ? 5000f : 0f;
                float enemyHqDist = SectorManager.HexDistance(enemyCell, enemyHqCell);
                float cellHqDist = SectorManager.HexDistance(cell, enemyHqCell);
                float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                float dpq = GetTerrainDpqPontos(cell);
                int pathCost = GetPathStepCount(paths, cell);
                BazookaTargetPriority targetPreference = ResolveAssaultTargetPreference(unit, enemy);
                float targetPreferenceScore = GetAssaultTargetPreferenceScore(targetPreference);
                float score =
                    targetPreferenceScore
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 900f
                    + constructionBonus
                    - (inOwnConstruction ? 0f : enemyHqDist * 120f)
                    - cellHqDist * 30f
                    + dpq * dpqWeight
                    - threat * 70f
                    - pathCost * 10f
                    - SectorManager.HexDistance(fromCell, cell) * 8f
                    - enemy.InstanceId * 0.001f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestTarget = enemy;
                    bestReason = $"score={score:F0} pref={targetPreference} hp={enemy.CurrentHP} bldg={inConstruction} ownBldg={inOwnConstruction} dpq={dpq:F1} dpqW={dpqWeight:F0} threat={threat:F1} preferDpq={preferDpq} {attackDecisionReason}";
                }
            }
        }

        if (bestTarget == null)
            return false;

        Vector3Int targetCell = bestTarget.CurrentCellPosition;
        targetCell.z = 0;
        Debug.Log($"{TL("Assalto")} {unit.InstanceId} cede predio-alvo de capturador {fromCell} e ataca via {bestCell} \u2192 {bestTarget.UnitDisplayName}#{bestTarget.InstanceId} ({bestReason})");
        action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, bestCell, bestTarget.InstanceId.ToString(), targetCell, paths);
        return true;
    }

    private static SectorObjective ResolveAssignedAssaultObjective(UnitManager unit, TeamObjectivePlan plan)
    {
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Assalto && slot.Filled && slot.AssignedUnitId == unit.InstanceId)
                    return obj;
        return null;
    }

    // Retorna o menor DistanceToObjective entre os slots de Capturador preenchidos no objetivo.
    // Retorna -1 se nenhum capturador tiver distância conhecida.
    private static int GetBestCapturerDistanceToObjective(SectorObjective obj)
    {
        int best = int.MaxValue;
        foreach (SlotNeed slot in obj.Slots)
        {
            if (slot.Role != UnitRole.Capturador || !slot.Filled || slot.DistanceToObjective < 0) continue;
            if (slot.DistanceToObjective < best) best = slot.DistanceToObjective;
        }
        return best == int.MaxValue ? -1 : best;
    }
}
