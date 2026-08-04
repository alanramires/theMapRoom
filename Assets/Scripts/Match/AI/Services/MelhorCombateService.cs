using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum MelhorCombateMode
{
    Stationary = 0,
    MoveAndAttack = 1,
    Hybrid = 2,
    AutoFromUnitData = 3
}

public enum MelhorCombateCandidateMode
{
    Stationary = 0,
    MoveAndAttack = 1
}

public enum CombatAdmissionTier
{
    Unavailable = 0,
    Blocked = 1,
    Allowed = 2
}

/// <summary>
/// Canonical, decomposed ranking key for one executable sensor option.
/// Consumers must use <see cref="MelhorCombateService.CompareCandidates"/>
/// instead of rebuilding a weighted score from these components.
/// </summary>
public readonly struct CombatRankKey
{
    public readonly CombatAdmissionTier Admission;
    public readonly bool KillGuaranteed;
    public readonly bool AttackerSurvives;
    public readonly int TradeBalancePercent;
    public readonly int TargetDamagePercent;
    public readonly int AttackerLossPercent;
    public readonly BazookaTargetPriority TargetPreference;
    public readonly int RangeDistanceFromPreferred;
    public readonly int AttackerDpqPoints;
    public readonly int AttackerDefenseBonus;
    public readonly int MovementCost;
    public readonly Vector3Int FromCell;
    public readonly Vector3Int TargetCell;
    public readonly int TargetInstanceId;
    public readonly int TargetDomain;
    public readonly int TargetHeight;
    public readonly string TargetStableName;
    public readonly int WeaponIndex;

    public CombatRankKey(
        CombatAdmissionTier admission,
        bool killGuaranteed,
        bool attackerSurvives,
        int tradeBalancePercent,
        int targetDamagePercent,
        int attackerLossPercent,
        BazookaTargetPriority targetPreference,
        int rangeDistanceFromPreferred,
        int attackerDpqPoints,
        int attackerDefenseBonus,
        int movementCost,
        Vector3Int fromCell,
        Vector3Int targetCell,
        int targetInstanceId,
        int targetDomain,
        int targetHeight,
        string targetStableName,
        int weaponIndex)
    {
        fromCell.z = 0;
        targetCell.z = 0;
        Admission = admission;
        KillGuaranteed = killGuaranteed;
        AttackerSurvives = attackerSurvives;
        TradeBalancePercent = tradeBalancePercent;
        TargetDamagePercent = targetDamagePercent;
        AttackerLossPercent = attackerLossPercent;
        TargetPreference = targetPreference;
        RangeDistanceFromPreferred = Mathf.Max(0, rangeDistanceFromPreferred);
        AttackerDpqPoints = Mathf.Max(0, attackerDpqPoints);
        AttackerDefenseBonus = attackerDefenseBonus;
        MovementCost = Mathf.Max(0, movementCost);
        FromCell = fromCell;
        TargetCell = targetCell;
        TargetInstanceId = targetInstanceId;
        TargetDomain = targetDomain;
        TargetHeight = targetHeight;
        TargetStableName = targetStableName ?? string.Empty;
        WeaponIndex = weaponIndex;
    }
}

public sealed class MelhorCombateRequest
{
    public UnitManager Unit;
    public Tilemap BoardMap;
    public TerrainDatabase TerrainDatabase;
    public RPSDatabase RpsDatabase;
    public DPQMatchupDatabase DpqMatchupDatabase;
    public WeaponPriorityData WeaponPriorityData;
    public DPQAirHeightConfig DpqAirHeightConfig;

    public MelhorCombateMode Mode = MelhorCombateMode.AutoFromUnitData;

    /// <summary>
    /// Geometry is an input. The service does not infer whether the caller is
    /// asking for terrestrial paths or an aerial cubic hotzone.
    /// </summary>
    public ReachSubStep MobileSubStep = ReachSubStep.Terrestre;

    /// <summary>Zero uses the project's current-turn budget rule.</summary>
    public int MovementBudget;

    /// <summary>Optional reuse when a caller already owns the tactical envelope.</summary>
    public UnitReachEnvelope MobileEnvelope;

    public bool DefensiveContext;
    public bool EnableLdt = true;
    public bool EnableLos = true;
    public bool EnableSpotter = true;
    public bool EnableStealth = true;
    public bool RespectTotalWarVisibility = true;

    /// <summary>
    /// Optional caller-owned target snapshot. Runtime callers pass confirmed
    /// contacts already published for their slot; Scene:Edit callers may pass a
    /// FogKnowledgeSnapshot cooked offline. Every hypothetical origin reuses
    /// the same list instead of scanning the board.
    /// </summary>
    public IReadOnlyList<UnitManager> TargetCandidates;

    /// <summary>
    /// Optional snapshot of targets acquired before movement. It is applied only
    /// to MoveAndAttack origins: a hypothetical origin may improve the firing
    /// line, but it cannot reveal a new target in time for that same action.
    /// The predicate must not mutate board or unit state.
    /// </summary>
    public Predicate<UnitManager> PreMovementTargetFilter;

    /// <summary>
    /// Percepcao ja confirmada para todos os alvos do snapshot. Opcional: sem
    /// ela o PodeMirar conserva o fluxo normal de descoberta por sensores.
    /// </summary>
    public PodeMirarPerceptionSnapshot PerceptionSnapshot;
}

public sealed class MelhorCombateCandidate
{
    public MelhorCombateCandidateMode Mode;
    public Vector3Int FromCell;
    public int MovementCost;
    public int RemainingMovement;
    public UnitManager Target;
    public PodeMirarTargetOption SensorOption;
    public CombatEvaluationOutcome Evaluation;
    public CombatRankKey RankKey;

    public bool IsCanonicalSensorOption =>
        SensorOption != null
        && SensorOption.attackerUnit != null
        && SensorOption.targetUnit == Target;
}

public sealed class MelhorCombateCellResult
{
    public MelhorCombateCandidateMode Mode;
    public Vector3Int Cell;
    public int MovementCost;
    public int RemainingMovement;
    public readonly List<MelhorCombateCandidate> Candidates =
        new List<MelhorCombateCandidate>();
    public MelhorCombateCandidate Best;
}

public sealed class MelhorCombateSensorRejection
{
    public MelhorCombateCandidateMode Mode;
    public Vector3Int FromCell;
    public PodeMirarInvalidOption InvalidOption;
}

public sealed class MelhorCombateResult
{
    public MelhorCombateMode RequestedMode;
    public MelhorCombateCandidateMode PreferredMode;
    public int MovementBudget;
    public string Diagnostic = string.Empty;

    public readonly List<MelhorCombateCellResult> StationaryCells =
        new List<MelhorCombateCellResult>();
    public readonly List<MelhorCombateCellResult> MobileCells =
        new List<MelhorCombateCellResult>();
    public readonly List<MelhorCombateCandidate> StationaryRanking =
        new List<MelhorCombateCandidate>();
    public readonly List<MelhorCombateCandidate> MobileRanking =
        new List<MelhorCombateCandidate>();
    public readonly List<MelhorCombateSensorRejection> SensorRejections =
        new List<MelhorCombateSensorRejection>();

    public MelhorCombateCandidate BestStationary =>
        StationaryRanking.Count > 0 ? StationaryRanking[0] : null;
    public MelhorCombateCandidate BestMobile =>
        MobileRanking.Count > 0 ? MobileRanking[0] : null;

    /// <summary>
    /// In Auto mode this honors the UnitData mode preference and falls back to
    /// the other ranking only when the preferred one has no admitted combat.
    /// Hybrid deliberately keeps both rankings separate and returns their
    /// canonical best only as a convenience.
    /// </summary>
    public MelhorCombateCandidate Best { get; internal set; }
}

/// <summary>
/// Read-only consumer that crosses tactical origins with PodeMirar, evaluates
/// exactly the canonical sensor option for each target, and ranks the resulting
/// combats without role, mission or router policy.
///
/// TRANSACTIONAL CONTRACT: this service never moves a unit, spends movement,
/// fuel or ammunition, applies damage, changes occupancy, FOW, detection,
/// HasActed, confirmed caches or scene serialization.
/// </summary>
public static class MelhorCombateService
{
    public static MelhorCombateResult Evaluate(MelhorCombateRequest request)
    {
        var result = new MelhorCombateResult
        {
            RequestedMode = request != null
                ? request.Mode
                : MelhorCombateMode.AutoFromUnitData
        };

        if (!TryValidate(request, out UnitData unitData, out Tilemap map, out string invalidReason))
        {
            result.Diagnostic = invalidReason;
            return result;
        }

        int movementBudget = request.MovementBudget > 0
            ? request.MovementBudget
            : AIActionReachCoordinator.ResolveTacticalBudget(request.Unit);
        result.MovementBudget = movementBudget;

        bool includeStationary = request.Mode != MelhorCombateMode.MoveAndAttack;
        bool includeMobile = request.Mode != MelhorCombateMode.Stationary;
        bool preferStationary = request.Mode == MelhorCombateMode.Stationary
            || ((request.Mode == MelhorCombateMode.AutoFromUnitData
                 || request.Mode == MelhorCombateMode.Hybrid)
                && unitData.preferArtilleryModeBeforeCombatant);
        result.PreferredMode = preferStationary
            ? MelhorCombateCandidateMode.Stationary
            : MelhorCombateCandidateMode.MoveAndAttack;

        if (includeStationary)
        {
            Vector3Int origin = Normalize(request.Unit.CurrentCellPosition);
            EvaluateOrigin(
                request,
                unitData,
                map,
                MelhorCombateCandidateMode.Stationary,
                SensorMovementMode.MoveuParado,
                origin,
                0,
                movementBudget,
                result.StationaryCells,
                result.StationaryRanking,
                result.SensorRejections);
        }

        UnitReachEnvelope envelope = null;
        if (includeMobile)
        {
            envelope = request.MobileEnvelope ?? UnitReachEnvelopeService.Build(
                new UnitReachRequest
                {
                    Unit = request.Unit,
                    BoardMap = map,
                    TerrainDatabase = request.TerrainDatabase,
                    Intent = ReachIntent.Combat,
                    Band = ReachBand.Tactical,
                    MovementBudget = movementBudget,
                    IncludeMovementCosts = true,
                    MovementMode = UnitThreatEnvelopeMovement.CurrentTurn,
                    DpqAirHeightConfig = request.DpqAirHeightConfig,
                    EnableLdt = request.EnableLdt,
                    EnableLos = request.EnableLos,
                    EnableSpotter = request.EnableSpotter,
                    // O ranking abaixo cruza cada origem com os contatos reais.
                    // Construir antes a zona virtual de tiro repetiria a busca
                    // de observadores para depois descartar esse resultado.
                    CombatMovementOriginsOnly = true,
                    SubStep = request.MobileSubStep
                });

            if (envelope != null)
            {
                var origins = new List<Vector3Int>(envelope.MovementCells);
                origins.Sort(CompareCells);
                Vector3Int current = Normalize(request.Unit.CurrentCellPosition);
                for (int i = 0; i < origins.Count; i++)
                {
                    Vector3Int origin = Normalize(origins[i]);
                    if (origin == current)
                        continue;

                    int cost = ResolveMovementCost(
                        request,
                        map,
                        request.MobileSubStep,
                        current,
                        origin,
                        envelope);
                    EvaluateOrigin(
                        request,
                        unitData,
                        map,
                        MelhorCombateCandidateMode.MoveAndAttack,
                        SensorMovementMode.MoveuAndando,
                        origin,
                        cost,
                        Mathf.Max(0, movementBudget - cost),
                        result.MobileCells,
                        result.MobileRanking,
                        result.SensorRejections);
                }
            }
        }

        result.StationaryRanking.Sort(CompareCandidates);
        result.MobileRanking.Sort(CompareCandidates);
        ResolveBest(result);

        string envelopeDiagnostic = envelope != null && !string.IsNullOrWhiteSpace(envelope.Diagnostic)
            ? $"; envelope=({envelope.Diagnostic})"
            : string.Empty;
        result.Diagnostic =
            $"mode={request.Mode}; stationaryCells={result.StationaryCells.Count}; "
            + $"mobileCells={result.MobileCells.Count}; "
            + $"stationaryCombats={result.StationaryRanking.Count}; "
            + $"mobileCombats={result.MobileRanking.Count}; "
            + $"sensorRejects={result.SensorRejections.Count}"
            + envelopeDiagnostic;
        return result;
    }

    /// <summary>
    /// Canonical comparison. Negative means <paramref name="a"/> ranks before
    /// <paramref name="b"/>. It is public so consumers do not invent weights.
    /// </summary>
    public static int CompareCandidates(MelhorCombateCandidate a, MelhorCombateCandidate b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        CombatRankKey x = a.RankKey;
        CombatRankKey y = b.RankKey;
        int compare;

        compare = y.Admission.CompareTo(x.Admission);
        if (compare != 0) return compare;
        compare = y.KillGuaranteed.CompareTo(x.KillGuaranteed);
        if (compare != 0) return compare;
        compare = y.AttackerSurvives.CompareTo(x.AttackerSurvives);
        if (compare != 0) return compare;
        compare = y.TradeBalancePercent.CompareTo(x.TradeBalancePercent);
        if (compare != 0) return compare;
        compare = y.TargetDamagePercent.CompareTo(x.TargetDamagePercent);
        if (compare != 0) return compare;
        compare = x.AttackerLossPercent.CompareTo(y.AttackerLossPercent);
        if (compare != 0) return compare;
        compare = y.TargetPreference.CompareTo(x.TargetPreference);
        if (compare != 0) return compare;
        compare = x.RangeDistanceFromPreferred.CompareTo(y.RangeDistanceFromPreferred);
        if (compare != 0) return compare;
        compare = y.AttackerDpqPoints.CompareTo(x.AttackerDpqPoints);
        if (compare != 0) return compare;
        compare = y.AttackerDefenseBonus.CompareTo(x.AttackerDefenseBonus);
        if (compare != 0) return compare;
        compare = x.MovementCost.CompareTo(y.MovementCost);
        if (compare != 0) return compare;
        compare = CompareCells(x.FromCell, y.FromCell);
        if (compare != 0) return compare;
        compare = CompareCells(x.TargetCell, y.TargetCell);
        if (compare != 0) return compare;
        compare = x.TargetInstanceId.CompareTo(y.TargetInstanceId);
        if (compare != 0) return compare;
        compare = x.TargetDomain.CompareTo(y.TargetDomain);
        if (compare != 0) return compare;
        compare = x.TargetHeight.CompareTo(y.TargetHeight);
        if (compare != 0) return compare;
        compare = string.CompareOrdinal(x.TargetStableName, y.TargetStableName);
        if (compare != 0) return compare;
        return x.WeaponIndex.CompareTo(y.WeaponIndex);
    }

    public static bool IsAdmitted(MelhorCombateCandidate candidate)
    {
        return candidate != null
            && candidate.RankKey.Admission == CombatAdmissionTier.Allowed;
    }

    private static void EvaluateOrigin(
        MelhorCombateRequest request,
        UnitData unitData,
        Tilemap map,
        MelhorCombateCandidateMode candidateMode,
        SensorMovementMode sensorMode,
        Vector3Int fromCell,
        int movementCost,
        int remainingMovement,
        List<MelhorCombateCellResult> cellDestination,
        List<MelhorCombateCandidate> rankingDestination,
        List<MelhorCombateSensorRejection> rejectionDestination)
    {
        var options = new List<PodeMirarTargetOption>();
        var invalidOptions = new List<PodeMirarInvalidOption>();
        Predicate<UnitManager> targetFilter =
            candidateMode == MelhorCombateCandidateMode.MoveAndAttack
                ? request.PreMovementTargetFilter
                : null;
        PodeMirarSensor.CollectTargets(
            request.Unit,
            map,
            request.TerrainDatabase,
            sensorMode,
            options,
            invalidOptions,
            request.WeaponPriorityData,
            request.DpqAirHeightConfig,
            request.EnableLdt,
            request.EnableLos,
            request.EnableSpotter,
            request.EnableStealth,
            request.RespectTotalWarVisibility && request.TargetCandidates == null,
            fromCell,
            request.TargetCandidates,
            request.PerceptionSnapshot);

        for (int i = 0; i < invalidOptions.Count; i++)
        {
            PodeMirarInvalidOption invalid = invalidOptions[i];
            if (invalid == null
                || (targetFilter != null
                    && invalid.targetUnit != null
                    && !targetFilter(invalid.targetUnit)))
            {
                continue;
            }

            rejectionDestination.Add(new MelhorCombateSensorRejection
            {
                Mode = candidateMode,
                FromCell = fromCell,
                InvalidOption = invalid
            });
        }

        var cell = new MelhorCombateCellResult
        {
            Mode = candidateMode,
            Cell = fromCell,
            MovementCost = movementCost,
            RemainingMovement = remainingMovement
        };

        // PodeMirar owns canonical ordering and ammunition legality. Example:
        // if a tank's preferred roof gun has no ammo, it is emitted as a sensor
        // rejection and the cannon becomes the first executable option even
        // against infantry. We intentionally simulate that poor fallback. The
        // executor also selects the first option for a target, so later weapons
        // are never evaluated or promised while PlayerAction lacks weaponIndex.
        var seenTargets = new HashSet<UnitManager>();
        for (int i = 0; i < options.Count; i++)
        {
            PodeMirarTargetOption option = options[i];
            UnitManager target = option != null ? option.targetUnit : null;
            if (target == null
                || target.IsDead
                || !seenTargets.Add(target)
                || (targetFilter != null && !targetFilter(target)))
            {
                continue;
            }

            PositionDpqResult attackerDpq = PositionDpqResolver.Resolve(
                request.Unit,
                fromCell,
                map,
                request.TerrainDatabase,
                request.DpqAirHeightConfig);
            Vector3Int targetCell = Normalize(target.CurrentCellPosition);
            PositionDpqResult defenderDpq = PositionDpqResolver.Resolve(
                target,
                targetCell,
                map,
                request.TerrainDatabase,
                request.DpqAirHeightConfig);

            CombatEvaluationOutcome evaluation = CombatEvaluationService.Evaluate(
                new CombatEvaluationRequest
                {
                    Attacker = request.Unit,
                    Target = target,
                    AttackCell = fromCell,
                    SensorOption = option,
                    RpsDatabase = request.RpsDatabase,
                    DpqMatchupDatabase = request.DpqMatchupDatabase,
                    WeaponPriorityData = request.WeaponPriorityData,
                    AttackerDpq = attackerDpq,
                    DefenderDpq = defenderDpq,
                    DefensiveContext = request.DefensiveContext,
                    AllowLegacyAutomaticWeaponFallback = false
                });

            BazookaTargetPriority targetPreference = BazookaTargetPriority.Tertiary;
            if (target.TryGetUnitData(out UnitData targetData) && targetData != null)
            {
                targetPreference = unitData.ResolveAiTargetPriorityForTargetClass(
                    targetData.unitClass);
            }

            int rangeDistanceFromPreferred = ResolveRangeDistanceFromPreferred(
                request.Unit,
                option,
                unitData.preferRepositionAtWeaponMaxRange);
            int targetDamagePercent = evaluation.HasSimulation
                ? evaluation.Combat.TargetDamagePercent
                : 0;
            int attackerLossPercent = evaluation.HasSimulation
                ? evaluation.Combat.AttackerLossPercent
                : 0;
            bool kill = evaluation.HasSimulation
                && evaluation.Combat.Simulation.killGuaranteed;
            bool survives = evaluation.HasSimulation
                && evaluation.Combat.Simulation.attackerSurvives;
            CombatAdmissionTier admission = ResolveAdmission(evaluation);

            var candidate = new MelhorCombateCandidate
            {
                Mode = candidateMode,
                FromCell = fromCell,
                MovementCost = movementCost,
                RemainingMovement = remainingMovement,
                Target = target,
                SensorOption = option,
                Evaluation = evaluation,
                RankKey = new CombatRankKey(
                    admission,
                    kill,
                    survives,
                    targetDamagePercent - attackerLossPercent,
                    targetDamagePercent,
                    attackerLossPercent,
                    targetPreference,
                    rangeDistanceFromPreferred,
                    unitData.prioritizeDpqAtBattle ? attackerDpq.Points : 0,
                    unitData.prioritizeDpqAtBattle ? attackerDpq.DefenseBonus : 0,
                    movementCost,
                    fromCell,
                    targetCell,
                    target.InstanceId,
                    (int)target.GetDomain(),
                    (int)target.GetHeightLevel(),
                    ResolveStableTargetName(target),
                    option.embarkedWeaponIndex)
            };

            cell.Candidates.Add(candidate);
            rankingDestination.Add(candidate);
        }

        cell.Candidates.Sort(CompareCandidates);
        cell.Best = cell.Candidates.Count > 0 ? cell.Candidates[0] : null;
        cellDestination.Add(cell);
    }

    private static bool TryValidate(
        MelhorCombateRequest request,
        out UnitData unitData,
        out Tilemap map,
        out string reason)
    {
        unitData = null;
        map = null;
        if (request == null)
        {
            reason = "request=null";
            return false;
        }

        if (request.Unit == null)
        {
            reason = "unit=null";
            return false;
        }

        if (!request.Unit.TryGetUnitData(out unitData) || unitData == null)
        {
            reason = "unitData=unavailable";
            return false;
        }

        map = request.BoardMap != null ? request.BoardMap : request.Unit.BoardTilemap;
        if (map == null)
        {
            reason = "boardMap=null";
            return false;
        }

        if (request.TerrainDatabase == null)
        {
            reason = "terrainDatabase=null";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static CombatAdmissionTier ResolveAdmission(CombatEvaluationOutcome evaluation)
    {
        AttackDecisionStatus status = evaluation.AttackDecision.Status;
        if (!evaluation.HasSimulation
            || status == AttackDecisionStatus.SimulationUnavailable
            || status == AttackDecisionStatus.SimulationInvalid
            || status == AttackDecisionStatus.MissingParticipants
            || status == AttackDecisionStatus.AttackerDataUnavailable
            || status == AttackDecisionStatus.TargetDataUnavailable)
        {
            return CombatAdmissionTier.Unavailable;
        }

        return evaluation.AttackDecision.IsAllowed
            ? CombatAdmissionTier.Allowed
            : CombatAdmissionTier.Blocked;
    }

    private static int ResolveRangeDistanceFromPreferred(
        UnitManager attacker,
        PodeMirarTargetOption option,
        bool preferMaximumRange)
    {
        if (!preferMaximumRange || attacker == null || option == null)
            return 0;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = attacker.GetEmbarkedWeapons();
        int index = option.embarkedWeaponIndex;
        if (weapons == null || index < 0 || index >= weapons.Count || weapons[index] == null)
            return 0;

        return Mathf.Abs(weapons[index].GetRangeMax() - option.distance);
    }

    private static int ResolveMovementCost(
        MelhorCombateRequest request,
        Tilemap map,
        ReachSubStep subStep,
        Vector3Int current,
        Vector3Int destination,
        UnitReachEnvelope envelope)
    {
        if (envelope != null && envelope.TryGetCost(destination, out int cost))
            return Mathf.Max(0, cost);

        if (subStep == ReachSubStep.Aereo)
            return Mathf.Max(0, AIActionReachCoordinator.CubicDistance(current, destination));

        if (envelope != null
            && envelope.PathsByDestination.TryGetValue(destination, out List<Vector3Int> path)
            && path != null)
        {
            return Mathf.Max(
                0,
                UnitMovementPathRules.CalculateAutonomyCostForPath(
                    map,
                    request.Unit,
                    path,
                    request.TerrainDatabase,
                    applyOperationalAutonomyModifier: false));
        }

        return 0;
    }

    private static void ResolveBest(MelhorCombateResult result)
    {
        MelhorCombateCandidate stationary = result.BestStationary;
        MelhorCombateCandidate mobile = result.BestMobile;
        if (result.RequestedMode == MelhorCombateMode.Stationary)
        {
            result.Best = stationary;
            return;
        }

        if (result.RequestedMode == MelhorCombateMode.MoveAndAttack)
        {
            result.Best = mobile;
            return;
        }

        if (result.RequestedMode == MelhorCombateMode.AutoFromUnitData)
        {
            MelhorCombateCandidate preferred = result.PreferredMode
                == MelhorCombateCandidateMode.Stationary ? stationary : mobile;
            MelhorCombateCandidate fallback = result.PreferredMode
                == MelhorCombateCandidateMode.Stationary ? mobile : stationary;
            result.Best = IsAdmitted(preferred)
                ? preferred
                : IsAdmitted(fallback)
                    ? fallback
                    : preferred ?? fallback;
            return;
        }

        result.Best = CompareNullable(stationary, mobile) <= 0
            ? stationary ?? mobile
            : mobile;
    }

    private static int CompareNullable(MelhorCombateCandidate a, MelhorCombateCandidate b)
    {
        return CompareCandidates(a, b);
    }

    private static Vector3Int Normalize(Vector3Int cell)
    {
        cell.z = 0;
        return cell;
    }

    private static string ResolveStableTargetName(UnitManager target)
    {
        if (target == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(target.UnitId))
            return target.UnitId;
        if (!string.IsNullOrWhiteSpace(target.UnitDisplayName))
            return target.UnitDisplayName;
        return target.name ?? string.Empty;
    }

    private static int CompareCells(Vector3Int a, Vector3Int b)
    {
        int compare = a.y.CompareTo(b.y);
        return compare != 0 ? compare : a.x.CompareTo(b.x);
    }
}
