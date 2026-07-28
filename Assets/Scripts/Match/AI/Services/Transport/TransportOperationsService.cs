using System;
using System.Collections.Generic;
using UnityEngine;

public enum TransportOperationType
{
    None,
    Hospital,
    Evac,
    Supply,
    Pickup,
    Courier,
    Delivery
}

public enum TransportDomainAdapter
{
    Land,
    Air,
    Naval,
    Rail
}

public sealed class TransportCapabilityProfile
{
    public bool CanTransport;
    public bool CanSupply;
    public bool CanFight;
    public bool PlayConservative;
    public TransportDomainAdapter Adapter;

    public static TransportCapabilityProfile From(UnitData data)
    {
        var profile = new TransportCapabilityProfile();
        if (data == null)
            return profile;

        profile.CanTransport = data.isTransporter
            && data.transportSlots != null
            && data.transportSlots.Count > 0;
        profile.CanSupply = data.isSupplier;
        profile.CanFight = data.embarkedWeapons != null
            && data.embarkedWeapons.Count > 0;
        profile.PlayConservative = data.playConservative;
        profile.Adapter = data.movementCategory == MovementCategory.Train
            ? TransportDomainAdapter.Rail
            : data.domain == Domain.Air
                ? TransportDomainAdapter.Air
                : data.domain == Domain.Naval
                    ? TransportDomainAdapter.Naval
                    : TransportDomainAdapter.Land;
        return profile;
    }
}

public sealed class TransportOperationDecision
{
    public TransportOperationType Operation;
    public AIReachDecisionTier ReachTier;
    public UnitManager TargetUnit;
    public Vector3Int TargetCell;
    public Vector3Int RendezvousCell;
    public int MovementBudget;
    public float Score;
    public string Reason;
    public MelhorEmbarqueOption PickupOption;
    public MelhorEmbarqueRideDisposition RideDisposition =
        MelhorEmbarqueRideDisposition.NotEvaluated;
    public MelhorEmbarquePassengerRouteState PassengerRouteState;
    public int PassengerRouteCost = -1;
    public int TransporterRouteCost = -1;
}

public delegate bool TransportOperationEvaluator(
    TransportOperationType operation,
    AIReachDecisionTier tier,
    int movementBudget,
    out TransportOperationDecision decision);

public sealed class TransportOperationContext
{
    public UnitManager Unit;
    public TransportCapabilityProfile Capabilities;
    public bool HasCargo;
    public bool HasPatient;
    public bool StrategicAllowed;
    public TransportOperationEvaluator Evaluate;
    public Action<string> DiagnosticLog;
}

/// <summary>
/// Ordena operacoes de transporte por capacidade e horizonte. O servico nao
/// move unidades, nao constroi PlayerAction, nao consome recursos e nao
/// confirma batches. O consumidor fornece consultas puras e materializa a
/// decisao somente depois que ela vence.
/// </summary>
public static class TransportOperationsService
{
    private readonly struct Attempt
    {
        public readonly TransportOperationType Operation;
        public readonly AIReachDecisionTier Tier;

        public Attempt(TransportOperationType operation, AIReachDecisionTier tier)
        {
            Operation = operation;
            Tier = tier;
        }
    }

    public static TransportOperationDecision Evaluate(
        TransportOperationContext context)
    {
        using var perf = new AIDecisionPerfScope(
            context?.Unit,
            "transportPlanning");
        AIDecisionPerf.AddCount("TransportPlanningCalls");
        if (context == null
            || context.Unit == null
            || context.Capabilities == null
            || context.Evaluate == null)
            return null;

        List<Attempt> attempts = BuildAttempts(context);
        for (int i = 0; i < attempts.Count; i++)
        {
            Attempt attempt = attempts[i];
            AIReachDecisionResult<TransportOperationDecision> reach =
                EvaluateAttempt(context, attempt);
            TransportOperationDecision decision =
                reach.Found ? reach.Decision.Value : null;
            bool found = decision != null;
            string result = found && decision != null ? "hit" : "miss";
            context.DiagnosticLog?.Invoke(
                $"[TransportOps][Unit#{context.Unit.InstanceId}]" +
                $"[{attempt.Operation}][{attempt.Tier}] {result}" +
                $"{(decision != null && !string.IsNullOrWhiteSpace(decision.Reason) ? $" {decision.Reason}" : string.Empty)}");
            if (!found || decision == null)
                continue;

            decision.Operation = attempt.Operation;
            decision.ReachTier = attempt.Tier;
            return decision;
        }

        return null;
    }

    private static AIReachDecisionResult<TransportOperationDecision>
        EvaluateAttempt(TransportOperationContext context, Attempt attempt)
    {
        bool EvaluateTier(
            int movementBudget,
            out AIReachDecisionCandidate<TransportOperationDecision> candidate)
        {
            candidate = null;
            if (!context.Evaluate(
                    attempt.Operation,
                    attempt.Tier,
                    movementBudget,
                    out TransportOperationDecision decision)
                || decision == null)
            {
                return false;
            }

            candidate =
                new AIReachDecisionCandidate<TransportOperationDecision>
                {
                    Value = decision,
                    ActionCell = decision.RendezvousCell,
                    TargetCell = decision.TargetCell,
                    Score = decision.Score,
                    Reason = decision.Reason
                };
            return true;
        }

        AIReachDecisionStages stage =
            attempt.Tier == AIReachDecisionTier.Tactical
                ? AIReachDecisionStages.Tactical
                : attempt.Tier == AIReachDecisionTier.Operational
                    ? AIReachDecisionStages.Operational
                    : AIReachDecisionStages.Strategic;
        var request =
            new AIReachDecisionRequest<TransportOperationDecision>
            {
                Context =
                    $"Transport:{context.Unit.InstanceId}:{attempt.Operation}",
                Policy = new AIReachDecisionPolicy(stage, operationalTurns: 2),
                CurrentMovementBudget = Mathf.Max(
                    0, context.Unit.RemainingMovementPoints),
                StrategicSearchBudget = int.MaxValue,
                DiagnosticLog = context.DiagnosticLog
            };
        if (attempt.Tier == AIReachDecisionTier.Tactical)
            request.EvaluateTactical = EvaluateTier;
        else if (attempt.Tier == AIReachDecisionTier.Operational)
            request.EvaluateOperational = EvaluateTier;
        else
            request.EvaluateStrategic = EvaluateTier;

        return AIActionReachCoordinator.Evaluate(request);
    }

    private static List<Attempt> BuildAttempts(
        TransportOperationContext context)
    {
        var attempts = new List<Attempt>();
        TransportCapabilityProfile caps = context.Capabilities;

        if (caps.CanSupply && caps.CanTransport && context.HasPatient)
            attempts.Add(new Attempt(
                TransportOperationType.Hospital,
                AIReachDecisionTier.Tactical));

        if (caps.CanTransport && context.HasCargo)
        {
            attempts.Add(new Attempt(
                TransportOperationType.Courier,
                AIReachDecisionTier.Tactical));
            attempts.Add(new Attempt(
                TransportOperationType.Delivery,
                AIReachDecisionTier.Operational));
            return attempts;
        }

        if (caps.CanTransport)
        {
            attempts.Add(new Attempt(
                TransportOperationType.Evac,
                AIReachDecisionTier.Tactical));
            attempts.Add(new Attempt(
                TransportOperationType.Evac,
                AIReachDecisionTier.Operational));
        }

        if (caps.CanSupply)
        {
            attempts.Add(new Attempt(
                TransportOperationType.Supply,
                AIReachDecisionTier.Tactical));
            attempts.Add(new Attempt(
                TransportOperationType.Supply,
                AIReachDecisionTier.Operational));
        }

        if (caps.CanTransport)
        {
            attempts.Add(new Attempt(
                TransportOperationType.Pickup,
                AIReachDecisionTier.Tactical));
            attempts.Add(new Attempt(
                TransportOperationType.Pickup,
                AIReachDecisionTier.Operational));
        }

        if (context.StrategicAllowed)
        {
            if (caps.CanTransport)
            {
                attempts.Add(new Attempt(
                    TransportOperationType.Evac,
                    AIReachDecisionTier.Strategic));
                attempts.Add(new Attempt(
                    TransportOperationType.Pickup,
                    AIReachDecisionTier.Strategic));
            }
        }

        return attempts;
    }
}
