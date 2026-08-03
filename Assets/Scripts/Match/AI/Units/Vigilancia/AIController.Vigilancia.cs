using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private readonly struct SurveillanceProfile
    {
        public readonly VisionCoverageLayer Layer;
        public readonly bool DetectsStealth;

        public bool IsAirLayer =>
            !Layer.IsAll && Layer.Domain == Domain.Air;

        public SurveillanceProfile(
            VisionCoverageLayer layer,
            bool detectsStealth)
        {
            Layer = layer;
            DetectsStealth = detectsStealth;
        }
    }

    private static readonly VisionCoverageScoringPolicy
        SurveillanceVisionPolicy =
            new VisionCoverageScoringPolicy(
                visibleWeight: 0.25f,
                marginalWeight: 3f,
                unexploredMarginalWeight: 25f,
                recoveredWeight: 6f,
                retainedUniqueWeight: 2f,
                overlapWeight: 0.1f,
                lostUniqueWeight: -8f,
                focusWeight: 10f,
                lostFocusedWeight: -20f,
                movementCostWeight: -0.25f);

    private enum SurveillancePolicyStage
    {
        EmergencyAndRepair,
        Recovery,
        TransportOrPlatform,
        ExitObstructedPosition,
        ImproveAirCoverage,
        FollowMagnet,
        ConservativeRear,
        Hold,
        Orbit
    }

    private PlayerAction TryDecideSurveillanceAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!TryResolveSurveillanceProfile(
                unit,
                out SurveillanceProfile profile)
            || snapshot == null)
            return null;

        if (!profile.IsAirLayer)
            return TryDecideLayerSurveillanceAction(
                unit,
                snapshot,
                profile);

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        EwacsRecoverySnapshot ewacsRecovery =
            BuildEwacsRecoverySnapshot(unit);
        bool needsPreciseRecoveryPaths =
            ewacsRecovery != null
            && (ewacsRecovery.FuelCritical
                || unit.IsUnderRepair
                || (unit.TryGetUnitData(out UnitData recoveryData)
                    && recoveryData != null
                    && EvaluateRepairTriggers(unit, recoveryData)));
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            needsPreciseRecoveryPaths
                ? BuildFireSupportPaths(unit)
                : BuildSurveillanceDecisionReach(unit, fromCell);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        if (needsPreciseRecoveryPaths
            && TryBuildEwacsEmergencyRecoveryAction(
                unit,
                snapshot,
                fromCell,
                paths,
                ewacsRecovery,
                out PlayerAction ewacsRecoveryAction,
                out string ewacsRecoveryReason))
        {
            LogSurveillancePolicyStage(
                unit,
                SurveillancePolicyStage.Recovery,
                ewacsRecoveryReason);
            return ewacsRecoveryAction;
        }

        if (IsStationaryMobileAirSurveillanceRadar(unit)
            && paths != null
            && paths.Count > 0
            && IsUncontrolledConstructionCellForAirSurveillance(
                unit,
                snapshot,
                fromCell))
        {
            TeamObjectivePlan vacatePlan =
                ObjectiveManager.GetPlanForSlot(
                    PlayerSlotId.FromIndex(snapshot.AISlotIndex));
            Vector3Int vacateCell =
                FindFireSupportCapturerVacateCell(
                    unit,
                    snapshot,
                    fromCell,
                    vacatePlan,
                    paths,
                    occupied);
            if (vacateCell != fromCell)
            {
                LogSurveillancePolicyStage(
                    unit,
                    SurveillancePolicyStage.ExitObstructedPosition,
                    $"radar desocupa construcao nao controlada " +
                    $"{fromCell} via {vacateCell}");
                PlayerAction radarVacateAction =
                    BuildMoveBatch(
                        unit,
                        snapshot.AITeam,
                        fromCell,
                        vacateCell,
                        paths);
                radarVacateAction.DebugLabel =
                    $"Radar Movel desocupa construcao nao controlada " +
                    $"{fromCell} para liberar captura";
                return radarVacateAction;
            }
        }

        if (IsStationaryMobileAirSurveillanceRadar(unit))
        {
            PlayerAction transportAction =
                TryDecideMobileRadarTransportAction(
                    unit,
                    snapshot,
                    plan,
                    fromCell);
            if (transportAction != null)
            {
                LogSurveillancePolicyStage(
                    unit,
                    SurveillancePolicyStage.TransportOrPlatform,
                    "radar solicita transporte terrestre");
                return transportAction;
            }
        }

        bool hasSurveillanceAnchor =
            TryResolveAirSurveillanceAnchor(
                unit,
                snapshot,
                plan,
                fromCell,
                out Vector3Int anchor,
                out bool offensiveAnchor,
                out string anchorReason);
        if (needsPreciseRecoveryPaths
            && hasSurveillanceAnchor
            && TryBuildAirPlatformRuntimeAction(
                unit,
                snapshot,
                anchor,
                paths,
                ewacsRecovery.Landing,
                ewacsRecovery,
                minimumMissionGain: 2f,
                acceptOnlyRecovery: true,
                maximumRecoveryRegression: 2f,
                out PlayerAction platformAction,
                out string platformReason))
        {
            LogSurveillancePolicyStage(
                unit,
                SurveillancePolicyStage.TransportOrPlatform,
                platformReason);
            return platformAction;
        }

        if (paths != null && paths.Count > 0
            && TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction vacateAction))
        {
            LogSurveillancePolicyStage(
                unit,
                SurveillancePolicyStage.ExitObstructedPosition,
                $"libera construcao em {fromCell}");
            return vacateAction;
        }

        if (hasSurveillanceAnchor
            && TryFindAirSurveillancePostureCell(unit, snapshot, fromCell, anchor, offensiveAnchor, paths, occupied, ewacsRecovery, out Vector3Int postureCell, out string postureReason))
        {
            if (postureCell != fromCell)
            {
                LogSurveillancePolicyStage(
                    unit,
                    IsStationaryMobileAirSurveillanceRadar(unit)
                        ? SurveillancePolicyStage.ImproveAirCoverage
                        : SurveillancePolicyStage.FollowMagnet,
                    $"reposiciona via {postureCell} anchor={anchor} ({anchorReason}; {postureReason})");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, postureCell, paths);
            }

            LogSurveillancePolicyStage(
                unit,
                ewacsRecovery != null
                    ? SurveillancePolicyStage.Orbit
                    : SurveillancePolicyStage.Hold,
                ewacsRecovery != null
                    ? $"mantem orbita segura em {fromCell} anchor={anchor} ({anchorReason}; {postureReason})"
                    : $"segura observacao em {fromCell} anchor={anchor} ({anchorReason}; {postureReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        PlayerAction conservativeAction =
            TryBuildConservativeRearFollowAction(
                unit,
                snapshot,
                paths,
                context: "vigilancia-aerea");
        if (conservativeAction != null)
        {
            LogSurveillancePolicyStage(
                unit,
                SurveillancePolicyStage.ConservativeRear,
                "sem ancora operacional; acompanha retaguarda aliada");
            return conservativeAction;
        }

        LogSurveillancePolicyStage(
            unit,
            ewacsRecovery != null
                ? SurveillancePolicyStage.Orbit
                : SurveillancePolicyStage.Hold,
            ewacsRecovery != null
                ? $"sem direcao segura; mantem orbita em {fromCell}"
                : $"sem direcao segura; aguarda em {fromCell}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    /// <summary>
    /// Agenda comum das unidades de Vigilancia cuja camada principal nao e Air.
    /// A arma tem precedencia quando o PodeMirar materializa um tiro; sem tiro,
    /// a unidade escolhe a posicao pela camada principal declarada na ficha.
    /// </summary>
    private PlayerAction TryDecideLayerSurveillanceAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SurveillanceProfile profile)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            BuildSurveillanceDecisionReach(unit, fromCell);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (HasUsableWeapon(unit))
        {
            if (unit.TryGetUnitData(out UnitData data)
                && data != null
                && data.domain == Domain.Air)
            {
                PlayerAction airAttack =
                    TryDecideAirCombatAttackOnly(unit, snapshot);
                if (airAttack != null)
                    return airAttack;
            }
            else if (TryBuildRolePreemptiveAttack(
                    unit,
                    snapshot,
                    paths,
                    occupied,
                    defensiveContext: false,
                    out PlayerAction attack,
                    out string attackReason))
            {
                Debug.Log(
                    $"{TL("Vigilancia")} {unit.InstanceId} " +
                    $"ataca antes de reposicionar - {attackReason}");
                return attack;
            }
        }

        MelhorVisaoResult vision =
            EvaluateSurveillanceVision(unit, profile);
        MelhorVisaoCellScore origin = vision?.Origin;
        if (origin == null)
        {
            return BuildMoveBatch(
                unit,
                snapshot.AITeam,
                fromCell,
                fromCell,
                paths);
        }

        float originThreat =
            CalculateThreatLevel(fromCell, snapshot.AITeam);
        float originOperationalScore =
            origin.Score - originThreat * 40f;
        MelhorVisaoCellScore best = origin;
        float bestOperationalScore = originOperationalScore;

        for (int i = 0; i < vision.Ranking.Count; i++)
        {
            MelhorVisaoCellScore candidate = vision.Ranking[i];
            if (candidate == null)
                continue;
            Vector3Int cell = candidate.Cell;
            cell.z = 0;
            if (cell != fromCell
                && (paths == null || !paths.ContainsKey(cell)))
            {
                continue;
            }

            float threat =
                CalculateThreatLevel(cell, snapshot.AITeam);
            float operationalScore =
                candidate.Score - threat * 40f;
            if (operationalScore > bestOperationalScore)
            {
                best = candidate;
                bestOperationalScore = operationalScore;
            }
        }

        float requiredGain = origin.Coverage == null
            || origin.Coverage.VisibleCount <= 2
                ? 4f
                : Mathf.Max(8f, Mathf.Abs(origin.Score) * 0.04f);
        float actualGain =
            bestOperationalScore - originOperationalScore;
        if (best == origin
            || best.Cell == fromCell
            || actualGain < requiredGain)
        {
            Debug.Log(
                $"{TL("Vigilancia")} {unit.InstanceId} " +
                $"layer={profile.Layer.Label} hold={fromCell} " +
                $"gain={actualGain:F1}/{requiredGain:F1} " +
                $"({origin.Reason})");
            return BuildMoveBatch(
                unit,
                snapshot.AITeam,
                fromCell,
                fromCell,
                paths);
        }

        Vector3Int destination = best.Cell;
        destination.z = 0;
        Debug.Log(
            $"{TL("Vigilancia")} {unit.InstanceId} " +
            $"layer={profile.Layer.Label} move={fromCell}->{destination} " +
            $"gain={actualGain:F1} deltaVisao={best.DeltaFromOrigin:F1} " +
            $"({best.Reason})");
        PlayerAction move = BuildMoveBatch(
            unit,
            snapshot.AITeam,
            fromCell,
            destination,
            paths);
        move.DebugLabel =
            $"Vigilancia {profile.Layer.Label} " +
            $"{unit.InstanceId} -> {destination}";
        return move;
    }

    private MelhorVisaoResult EvaluateSurveillanceVision(
        UnitManager unit,
        SurveillanceProfile profile)
    {
        PlayerSlotId observerSlot =
            PlayerSlotId.FromIndex(unit.SlotIndex);
        bool hasConfirmedFog = matchController != null
            && matchController.ConcealsInformationFromObservers();
        return MelhorVisaoService.Evaluate(
            new MelhorVisaoRequest
            {
                Unit = unit,
                Map = boardTilemap,
                TerrainDatabase = terrainDatabase,
                DpqAirHeightConfig = turnStateManager != null
                    ? turnStateManager.DpqAirHeightConfigRef
                    : null,
                Layer = profile.Layer,
                ScoringPolicy = SurveillanceVisionPolicy,
                IsKnown = matchController != null
                    ? cell => !hasConfirmedFog
                        || matchController.IsCellKnownForActiveTeam(cell)
                    : null,
                IsExplored = matchController != null
                    ? cell => !hasConfirmedFog
                        || matchController.IsCellExploredBySlot(
                            observerSlot,
                            cell)
                    : null,
                AlliedObserverFilter = ally =>
                    IsEquivalentSurveillanceObserver(
                        ally,
                        profile),
                MovementBudget = Mathf.Max(
                    0,
                    unit.RemainingMovementPoints),
                EnableLos = matchController == null
                    || matchController.EnableLosValidation,
                IncludeAlliedCoverage = true,
                ValidateFinalOccupancy = true
            });
    }

    private static bool IsEquivalentSurveillanceObserver(
        UnitManager ally,
        SurveillanceProfile observedProfile)
    {
        if (!TryResolveSurveillanceProfile(
                ally,
                out SurveillanceProfile allyProfile)
            || !allyProfile.Layer.Equals(observedProfile.Layer))
        {
            return false;
        }

        return !observedProfile.DetectsStealth
            || allyProfile.DetectsStealth;
    }

    private Dictionary<Vector3Int, List<Vector3Int>>
        BuildSurveillanceDecisionReach(
            UnitManager unit,
            Vector3Int origin)
    {
        if (!AIActionReachCoordinator.UsesCubicSectorReach(unit))
            return BuildFireSupportPaths(unit);

        Dictionary<Vector3Int, int> hotzone =
            AIActionReachCoordinator.BuildSectorReachMap(
                unit,
                boardTilemap,
                terrainDatabase,
                origin,
                AIActionReachCoordinator.ResolveTacticalBudget(unit));
        var reach =
            new Dictionary<Vector3Int, List<Vector3Int>>(
                hotzone.Count);
        foreach (Vector3Int cell in hotzone.Keys)
            reach[cell] = null;

        AIDecisionPerf.AddCount(
            "AirSurveillanceHotzoneCandidates",
            reach.Count);
        return reach;
    }

    private void LogSurveillancePolicyStage(
        UnitManager unit,
        SurveillancePolicyStage stage,
        string reason)
    {
        if (unit == null)
            return;

        Debug.Log(
            $"{TL("Vigilancia")} {unit.InstanceId} " +
            $"policy={stage} {reason}");
    }

    private bool IsUncontrolledConstructionCellForAirSurveillance(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int cell)
    {
        if (unit == null
            || snapshot == null
            || !IsStationaryMobileAirSurveillanceRadar(unit))
            return false;

        cell.z = 0;
        ConstructionManager construction =
            ConstructionOccupancyRules.GetConstructionAtCell(
                boardTilemap, cell);
        return construction != null
            && construction.IsCapturable
            && construction.CapturePointsMax > 0
            && construction.SlotIndex
                != ResolveAISlotKey(snapshot.AITeam);
    }

    private static bool IsAirSurveillanceUnit(UnitManager unit)
    {
        return TryResolveSurveillanceProfile(
                unit,
                out SurveillanceProfile profile)
            && profile.IsAirLayer;
    }

    private static bool IsSurveillanceUnit(UnitManager unit) =>
        TryResolveSurveillanceProfile(unit, out _);

    private static bool TryResolveSurveillanceProfile(
        UnitManager unit,
        out SurveillanceProfile profile)
    {
        profile = default;
        if (unit == null
            || !unit.TryGetUnitData(out UnitData data)
            || data == null
            || data.roles == null
            || !data.roles.Contains(UnitRole.Vigilancia))
        {
            return false;
        }

        VisionCoverageLayer principal =
            VisionCoverageLayerResolver.ResolvePrincipal(data);
        bool detectsStealth = !principal.IsAll
            && data.HasStealthDetectionFor(
                principal.Domain,
                principal.Height);
        profile = new SurveillanceProfile(
            principal,
            detectsStealth);
        return true;
    }

    private static bool IsBacklineSupportUnit(UnitManager unit)
    {
        return IsFireSupportUnit(unit)
            || IsAirSurveillanceUnit(unit);
    }

    private bool TryResolveAirSurveillanceAnchor(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int fromCell,
        out Vector3Int anchor,
        out bool offensiveAnchor,
        out string reason)
    {
        anchor = fromCell;
        offensiveAnchor = false;
        reason = "fallback";

        // O EWACS acompanha a cabeca de ponte. Ele nao tenta descobrir uma
        // missao propria varrendo o tabuleiro: o capturador e o seu ima.
        // Radar Movel permanece na politica estacionaria de cobertura.
        if (!IsStationaryMobileAirSurveillanceRadar(unit)
            && TryResolveCapturerMagnet(
                unit,
                snapshot,
                fromCell,
                out UnitManager captain,
                out Vector3Int captainCell))
        {
            anchor = captainCell;
            offensiveAnchor = true;
            reason = $"CapturerMagnet=#{captain.InstanceId}";
            return true;
        }

        if (TryResolveRallyInfluence(plan, snapshot.AITeam, fromCell, includeGoGreen: false, out AIRallyInfluence rally)
            && rally.Active
            && IsRallyAssemblingState(rally.State))
        {
            anchor = rally.Anchor;
            offensiveAnchor = true;
            reason = $"rally {rally.Sector} {rally.State} {rally.Reason}";
            return true;
        }

        SectorObjective bestObjective = null;
        float bestScore = float.MinValue;
        if (plan != null && plan.Objectives != null)
        {
            foreach (SectorObjective obj in plan.Objectives)
            {
                if (obj == null || obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned)
                    continue;
                if (obj.Status == ObjectiveStatus.Defending)
                    continue;

                Vector3Int objAnchor = ResolveFireSupportObjectiveAnchor(obj, snapshot.AITeam, fromCell);
                int screenCount = CountNonSupportAlliesNearAnchor(unit, snapshot, objAnchor, 4);
                if (screenCount <= 0)
                    continue;

                float priorityBonus = Mathf.Max(0f, 12f - obj.Priority) * 80f;
                float score = priorityBonus
                    + screenCount * 280f
                    - SectorManager.HexDistance(fromCell, objAnchor) * 18f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestObjective = obj;
                    anchor = objAnchor;
                }
            }
        }

        if (bestObjective != null)
        {
            offensiveAnchor = true;
            reason = $"objetivo {bestObjective.Sector} {bestObjective.ObjectiveType}";
            return true;
        }

        UnitManager airAsset = FindBestOwnAirAssetForSurveillance(unit, snapshot, fromCell);
        if (airAsset != null)
        {
            anchor = airAsset.CurrentCellPosition;
            anchor.z = 0;
            offensiveAnchor = false;
            reason = $"cobertura aerea #{airAsset.InstanceId}";
            return true;
        }

        ConstructionManager home = FindBestAirSurveillanceHomeAnchor(snapshot, fromCell);
        if (home != null)
        {
            anchor = home.CurrentCellPosition;
            anchor.z = 0;
            reason = $"{home.ConstructionDisplayName}#{home.InstanceId}";
            return true;
        }

        return false;
    }

    private bool TryFindAirSurveillancePostureCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int anchor,
        bool offensiveAnchor,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        EwacsRecoverySnapshot ewacsRecovery,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = "";
        if (unit == null || snapshot == null)
            return false;

        if (IsStationaryMobileAirSurveillanceRadar(unit))
        {
            return TryFindStationaryMobileRadarCell(
                unit,
                snapshot,
                fromCell,
                anchor,
                offensiveAnchor,
                paths,
                out bestCell,
                out reason);
        }

        if (!TryResolveSurveillanceProfile(
                unit,
                out SurveillanceProfile profile))
        {
            return false;
        }

        MelhorVisaoResult vision =
            EvaluateSurveillanceVision(unit, profile);
        var visionByCell =
            new Dictionary<Vector3Int, MelhorVisaoCellScore>();
        if (vision != null)
        {
            for (int i = 0; i < vision.Ranking.Count; i++)
            {
                MelhorVisaoCellScore item = vision.Ranking[i];
                if (item != null)
                    visionByCell[item.Cell] = item;
            }
        }
        float fromVisionScore = vision?.Origin != null
            ? vision.Origin.Score
            : 0f;

        TeamObjectivePlan capPlan =
            ObjectiveManager.GetPlanForSlot(
                PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        int preferredEscortDistance = Mathf.Clamp(
            ResolveAirSurveillanceVision(unit),
            4,
            7);
        AirSurveillanceSpacingSample fromSpacing =
            EvaluateAirSurveillanceSpacing(
                unit,
                snapshot,
                fromCell);
        int fromAnchorDistance =
            AIActionReachCoordinator.CubicDistance(
                fromCell,
                anchor);
        float fromThreat =
            CalculateThreatLevel(fromCell, snapshot.AITeam);
        float fromScore =
            fromVisionScore
            - Mathf.Abs(
                fromAnchorDistance - preferredEscortDistance) * 160f
            - fromThreat * 240f
            - fromSpacing.Penalty;
        bool fromInsideRecoveryEnvelope =
            IsEwacsRecoveryCellSafe(
                unit,
                fromCell,
                path: null,
                ewacsRecovery,
                out string fromRecoveryReason);
        if (!fromInsideRecoveryEnvelope)
            fromScore -= 100000f;
        float bestScore = fromScore;
        string fromReason =
            $"magnetDist={fromAnchorDistance} " +
            $"escort={preferredEscortDistance} " +
            $"threat={fromThreat:F1}";
        string bestReason =
            ewacsRecovery != null
                ? $"{fromReason} {fromSpacing} recovery={fromRecoveryReason}"
                : $"{fromReason} {fromSpacing}";

        if (paths != null)
        {
            using var perf = new AIDecisionPerfScope(
                unit,
                "airSurveillanceMagnetRanking");
            var ranking =
                new List<(Vector3Int cell, int distance, int pathCost, float score)>();
            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (cell == fromCell)
                    continue;
                if (occupied != null && occupied.Contains(cell))
                    continue;
                if (IsCellACapturerTarget(cell, capPlan, snapshot.AITeam))
                    continue;
                if (IsUncontrolledConstructionCellForAirSurveillance(
                        unit, snapshot, cell))
                    continue;

                int distance =
                    AIActionReachCoordinator.CubicDistance(
                        cell,
                        anchor);
                int pathCost =
                    AIActionReachCoordinator.CubicDistance(
                        fromCell,
                        cell);
                float approximateScore =
                    (visionByCell.TryGetValue(
                            cell,
                            out MelhorVisaoCellScore approximateVision)
                        ? approximateVision.Score
                        : 0f)
                    - Mathf.Abs(
                        distance - preferredEscortDistance) * 160f
                    - pathCost * 8f;
                ranking.Add(
                    (cell, distance, pathCost, approximateScore));
            }

            ranking.Sort((left, right) =>
            {
                int byScore = right.score.CompareTo(left.score);
                if (byScore != 0)
                    return byScore;
                int byPath = left.pathCost.CompareTo(right.pathCost);
                if (byPath != 0)
                    return byPath;
                int byX = left.cell.x.CompareTo(right.cell.x);
                return byX != 0
                    ? byX
                    : left.cell.y.CompareTo(right.cell.y);
            });

            int preciseCount = Mathf.Min(12, ranking.Count);
            for (int i = 0; i < preciseCount; i++)
            {
                var candidate = ranking[i];
                Vector3Int cell = candidate.cell;
                int distance = candidate.distance;
                int pathCost = candidate.pathCost;
                paths.TryGetValue(cell, out List<Vector3Int> candidatePath);
                if (!IsEwacsRecoveryCellSafe(
                        unit,
                        cell,
                        candidatePath,
                        ewacsRecovery,
                        out string recoveryReason))
                {
                    continue;
                }

                float threat =
                    CalculateThreatLevel(cell, snapshot.AITeam);
                AirSurveillanceSpacingSample spacing =
                    EvaluateAirSurveillanceSpacing(
                        unit,
                        snapshot,
                        cell);
                float visionScore = visionByCell.TryGetValue(
                        cell,
                        out MelhorVisaoCellScore preciseVision)
                    ? preciseVision.Score
                    : 0f;
                float score =
                    visionScore
                    - Mathf.Abs(
                        distance - preferredEscortDistance) * 160f
                    - pathCost * 8f
                    - threat * 240f
                    - spacing.Penalty;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    string scoreReason =
                        $"magnetDist={distance} " +
                        $"escort={preferredEscortDistance} " +
                        $"threat={threat:F1} path={pathCost} " +
                        $"vision={visionScore:F1}";
                    bestReason = ewacsRecovery != null
                        ? $"{scoreReason} {spacing} recovery={recoveryReason}"
                        : $"{scoreReason} {spacing}";
                }
            }

            AIDecisionPerf.AddCount(
                "AirSurveillanceMagnetCandidates",
                ranking.Count);
            AIDecisionPerf.AddCount(
                "AirSurveillanceMagnetPreciseCandidates",
                preciseCount);
        }

        reason =
            $"{bestReason} layer={profile.Layer.Label} " +
            $"score={bestScore:F0} hold={fromScore:F0}";
        return true;
    }

    private readonly struct AirSurveillanceCoverageSample
    {
        public readonly int AirLow;
        public readonly int AirHigh;
        public readonly int MarginalAirLow;
        public readonly int MarginalAirHigh;
        public readonly int UnexploredMarginalAirHigh;
        public readonly float Score;

        public int VisibleCells => AirLow + AirHigh;
        public int OverlapAirLow =>
            Mathf.Max(0, AirLow - MarginalAirLow);
        public int OverlapAirHigh =>
            Mathf.Max(0, AirHigh - MarginalAirHigh);

        public AirSurveillanceCoverageSample(
            int airLow,
            int airHigh,
            int marginalAirLow,
            int marginalAirHigh,
            int unexploredMarginalAirHigh,
            float score)
        {
            AirLow = airLow;
            AirHigh = airHigh;
            MarginalAirLow = marginalAirLow;
            MarginalAirHigh = marginalAirHigh;
            UnexploredMarginalAirHigh =
                unexploredMarginalAirHigh;
            Score = score;
        }

        public override string ToString()
        {
            return
                $"low={AirLow}(new={MarginalAirLow}) " +
                $"high={AirHigh}(new={MarginalAirHigh}) " +
                $"fogNew={UnexploredMarginalAirHigh} " +
                $"overlap={OverlapAirLow}/{OverlapAirHigh} " +
                $"coverage={Score:F0}";
        }
    }

    private readonly struct AirSurveillanceSpacingSample
    {
        public readonly int NearestDistance;
        public readonly int PreferredDistance;
        public readonly float Penalty;

        public bool HasAlliedSensor =>
            NearestDistance < int.MaxValue;

        public AirSurveillanceSpacingSample(
            int nearestDistance,
            int preferredDistance,
            float penalty)
        {
            NearestDistance = nearestDistance;
            PreferredDistance = preferredDistance;
            Penalty = penalty;
        }

        public override string ToString()
        {
            return HasAlliedSensor
                ? $"spacing={NearestDistance}/{PreferredDistance} " +
                    $"repel={Penalty:F0}"
                : $"spacing=none/{PreferredDistance} repel=0";
        }
    }

    private static bool IsStationaryMobileAirSurveillanceRadar(
        UnitManager unit)
    {
        if (!IsAirSurveillanceUnit(unit)
            || !unit.TryGetUnitData(out UnitData data)
            || data == null)
        {
            return false;
        }

        return data.domain == Domain.Land
            && data.longRangeStationary;
    }

    private PlayerAction TryDecideMobileRadarTransportAction(
        UnitManager radar,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int fromCell)
    {
        if (!HasCompatibleMobileRadarTransport(radar))
            return null;

        if (!TryResolveMobileRadarTransportTarget(
                radar,
                snapshot,
                plan,
                fromCell,
                requireCoverageGain: true,
                out Vector3Int target,
                out float coverageGain,
                out string targetReason))
        {
            return null;
        }

        QueroCaronaResult rideNeed =
            QueroCaronaService.Evaluate(
                new QueroCaronaRequest
                {
                    unit = radar,
                    map = boardTilemap,
                    terrainDatabase = terrainDatabase,
                    context =
                        QueroCaronaContext.RogueOuRebelde,
                    useExplicitTarget = true,
                    explicitTarget = target,
                    explicitTargetLabel =
                        $"zona de vigilancia {target}",
                    operationalTurns = 2,
                    emulateUnderRepairFromUnitData = false,
                    diagnosticLog = showAILogs
                        ? message => Debug.Log(
                            $"{TL("Vigilancia")} " +
                            $"Radar#{radar.InstanceId} " +
                            $"QueroCarona: {message}")
                        : null
                });

        ApplyRideWaitStamp(radar, rideNeed);
        Debug.Log(
            $"{TL("Vigilancia")} Radar#{radar.InstanceId} " +
            $"transporte target={target} gain={coverageGain:F0} " +
            $"QueroCarona={(rideNeed.wantsRide ? "SIM" : "NAO")} " +
            $"reach={rideNeed.reach} ({targetReason}; " +
            $"{rideNeed.reason})");
        if (!rideNeed.wantsRide)
            return null;

        return TryDecideCombatPassengerTransportAction(
            radar,
            snapshot,
            plan,
            CombatPassengerTransportPolicy.AirSurveillance,
            assigned: null,
            evaluatedRideNeed: rideNeed);
    }

    private static bool HasCompatibleMobileRadarTransport(
        UnitManager radar)
    {
        if (radar == null)
            return false;

        foreach (UnitManager transporter in UnitManager.AllActive)
        {
            if (transporter == null
                || transporter == radar
                || transporter.IsDead
                || transporter.IsEmbarked
                || transporter.IsUnderRepair
                || !PlayerSlotRelations.AreAllies(
                    radar,
                    transporter)
                || !transporter.TryGetUnitData(
                    out UnitData transporterData)
                || transporterData == null
                || !transporterData.isTransporter)
            {
                continue;
            }

            if (MelhorEmbarqueService
                .TryResolveCompatiblePassengerSlot(
                    transporter,
                    radar,
                    out _,
                    out _))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveMobileRadarTransportTarget(
        UnitManager radar,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int origin,
        bool requireCoverageGain,
        out Vector3Int target,
        out float gain,
        out string reason)
    {
        target = origin;
        target.z = 0;
        gain = 0f;
        reason = "sem ancora de vigilancia";
        if (!IsStationaryMobileAirSurveillanceRadar(radar)
            || snapshot == null
            || !TryResolveAirSurveillanceAnchor(
                radar,
                snapshot,
                plan,
                target,
                out Vector3Int anchor,
                out bool offensiveAnchor,
                out string anchorReason))
        {
            return false;
        }

        BoardTopologyIndex topology =
            BoardTopologyIndex.GetOrCreateRuntime(
                boardTilemap,
                terrainDatabase);
        if (topology == null || !topology.IsReady)
        {
            reason = "BoardTopology indisponivel";
            return false;
        }

        var alliedAirLow = new HashSet<Vector3Int>();
        var alliedAirHigh = new HashSet<Vector3Int>();
        CollectAlliedAirSurveillanceCoverage(
            radar,
            snapshot,
            alliedAirLow,
            alliedAirHigh);

        AirSurveillanceCoverageSample originCoverage =
            EvaluateAirSurveillanceCoverage(
                radar,
                target,
                alliedAirLow,
                alliedAirHigh);
        float originScore =
            ScoreMobileRadarTransportTarget(
                radar,
                snapshot,
                target,
                anchor,
                originCoverage,
                out AirSurveillanceSpacingSample originSpacing);
        float bestScore = float.MinValue;
        AirSurveillanceCoverageSample bestCoverage = default;
        AirSurveillanceSpacingSample bestSpacing = default;

        var frontier =
            new Queue<(Vector3Int cell, int depth)>();
        var visited = new HashSet<Vector3Int>();
        anchor.z = 0;
        frontier.Enqueue((anchor, 0));
        visited.Add(anchor);

        const int targetRadius = 4;
        while (frontier.Count > 0)
        {
            (Vector3Int cell, int depth) = frontier.Dequeue();
            cell.z = 0;
            if (UnitMovementPathRules.TryGetEnterCellCost(
                    boardTilemap,
                    radar,
                    cell,
                    terrainDatabase,
                    out _)
                && !IsCellACapturerTarget(
                    cell,
                    plan,
                    snapshot.AITeam)
                && IsAirSurveillanceCellAllowedByRearLine(
                    radar,
                    snapshot,
                    origin,
                    cell,
                    anchor,
                    offensiveAnchor)
                && (!TryScoreBacklineCell(
                        radar,
                        snapshot,
                        cell,
                        anchor,
                        out AIBacklineScore rear)
                    || !rear.IsVanguard))
            {
                AirSurveillanceCoverageSample coverage =
                    EvaluateAirSurveillanceCoverage(
                        radar,
                        cell,
                        alliedAirLow,
                        alliedAirHigh);
                float score =
                    ScoreMobileRadarTransportTarget(
                        radar,
                        snapshot,
                        cell,
                        anchor,
                        coverage,
                        out AirSurveillanceSpacingSample spacing);
                if (score > bestScore)
                {
                    bestScore = score;
                    target = cell;
                    bestCoverage = coverage;
                    bestSpacing = spacing;
                }
            }

            if (depth >= targetRadius)
                continue;
            IReadOnlyList<Vector3Int> neighbors =
                topology.GetNeighbors(cell);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int neighbor = neighbors[i];
                neighbor.z = 0;
                if (visited.Add(neighbor))
                    frontier.Enqueue((neighbor, depth + 1));
            }
        }

        if (bestScore == float.MinValue)
        {
            reason =
                $"ancora {anchorReason} sem celula terrestre compativel";
            return false;
        }

        gain = bestScore - originScore;
        float requiredGain =
            Mathf.Max(180f, originCoverage.Score * 0.12f);
        reason =
            $"anchor={anchor} {anchorReason} " +
            $"{originCoverage} {originSpacing} -> " +
            $"{bestCoverage} {bestSpacing} " +
            $"gain={gain:F0} required={requiredGain:F0}";
        return !requireCoverageGain || gain >= requiredGain;
    }

    private float ScoreMobileRadarTransportTarget(
        UnitManager radar,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int anchor,
        AirSurveillanceCoverageSample coverage,
        out AirSurveillanceSpacingSample spacing)
    {
        spacing = EvaluateAirSurveillanceSpacing(
            radar,
            snapshot,
            cell);
        float anchorDistance =
            SectorManager.HexDistance(cell, anchor);
        float missionDirection =
            Mathf.Max(0f, 600f - anchorDistance * 30f);
        float threat =
            CalculateThreatLevel(cell, snapshot.AITeam);
        float cohesion =
            CalculateFireSupportCohesionScore(
                radar,
                snapshot,
                cell);
        return coverage.Score
            + missionDirection
            + cohesion * 0.25f
            + GetTerrainDpqPontos(cell) * 25f
            - threat * 220f
            - spacing.Penalty;
    }

    private bool TryFindStationaryMobileRadarCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int anchor,
        bool offensiveAnchor,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = "radar stationary";
        if (!TryResolveSurveillanceProfile(
                unit,
                out SurveillanceProfile profile))
        {
            return false;
        }

        MelhorVisaoResult vision =
            EvaluateSurveillanceVision(unit, profile);
        MelhorVisaoCellScore fromVision = vision?.Origin;
        if (fromVision == null)
            return false;

        AirSurveillanceSpacingSample fromSpacing =
            EvaluateAirSurveillanceSpacing(
                unit,
                snapshot,
                fromCell);
        float fromPosture = ScoreAirSurveillancePostureCell(
            unit,
            snapshot,
            fromCell,
            fromCell,
            anchor,
            offensiveAnchor,
            pathCost: 0,
            out _);
        float fromScore =
            fromVision.Score
            + fromPosture * 0.2f
            - fromSpacing.Penalty;
        float bestScore = fromScore;
        MelhorVisaoCellScore bestVision = fromVision;
        AirSurveillanceSpacingSample bestSpacing = fromSpacing;
        int bestPathCost = 0;

        TeamObjectivePlan capPlan =
            ObjectiveManager.GetPlanForSlot(
                PlayerSlotId.FromIndex(snapshot.AISlotIndex));

        if (paths != null && vision != null)
        {
            for (int i = 0; i < vision.Ranking.Count; i++)
            {
                MelhorVisaoCellScore candidate = vision.Ranking[i];
                if (candidate == null)
                    continue;
                Vector3Int cell = candidate.Cell;
                cell.z = 0;
                if (cell == fromCell)
                    continue;
                if (!paths.ContainsKey(cell))
                    continue;
                List<UnitManager> occupants =
                    UnitOccupancyRules.GetUnitsAtCell(
                        boardTilemap,
                        cell,
                        unit);
                if (!CanAIUnitEndMoveAtCell(
                        unit,
                        cell,
                        occupants))
                {
                    continue;
                }
                if (IsCellACapturerTarget(
                        cell,
                        capPlan,
                        snapshot.AITeam))
                {
                    continue;
                }
                if (IsUncontrolledConstructionCellForAirSurveillance(
                        unit, snapshot, cell))
                {
                    continue;
                }
                if (!IsAirSurveillanceCellAllowedByRearLine(
                        unit,
                        snapshot,
                        fromCell,
                        cell,
                        anchor,
                        offensiveAnchor))
                {
                    continue;
                }
                if (TryScoreBacklineCell(
                        unit,
                        snapshot,
                        cell,
                        anchor,
                        out AIBacklineScore rear)
                    && rear.IsVanguard)
                {
                    continue;
                }

                int pathCost = candidate.MovementCost;
                AirSurveillanceSpacingSample spacing =
                    EvaluateAirSurveillanceSpacing(
                        unit,
                        snapshot,
                        cell);
                float posture =
                    ScoreAirSurveillancePostureCell(
                        unit,
                        snapshot,
                        cell,
                        fromCell,
                        anchor,
                        offensiveAnchor,
                    pathCost,
                    out _);
                float score =
                    candidate.Score
                    + posture * 0.2f
                    - spacing.Penalty;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestCell = cell;
                bestVision = candidate;
                bestSpacing = spacing;
                bestPathCost = pathCost;
            }
        }

        float requiredGain = fromVision.Coverage == null
            || fromVision.Coverage.VisibleCount <= 2
            ? 20f
            : Mathf.Max(30f, Mathf.Abs(fromVision.Score) * 0.06f);
        float actualGain = bestScore - fromScore;
        if (bestCell == fromCell || actualGain < requiredGain)
        {
            bestCell = fromCell;
            reason =
                $"radar stationary layer={profile.Layer.Label} hold " +
                $"{fromVision.Reason} {fromSpacing} " +
                $"bestGain={actualGain:F0} required={requiredGain:F0}";
            return true;
        }

        reason =
            $"radar stationary layer={profile.Layer.Label} move " +
            $"{fromVision.Reason} {fromSpacing} -> " +
            $"{bestVision.Reason} {bestSpacing} gain={actualGain:F0} " +
            $"required={requiredGain:F0} path={bestPathCost}";
        return true;
    }

    private AirSurveillanceCoverageSample EvaluateAirSurveillanceCoverage(
        UnitManager observer,
        Vector3Int observerCell,
        HashSet<Vector3Int> alliedAirLow,
        HashSet<Vector3Int> alliedAirHigh)
    {
        VisionCoverageResult result =
            VisionCoverageService.Evaluate(
                new VisionCoverageRequest
                {
                    Observer = observer,
                    ObserverCell = observerCell,
                    Map = boardTilemap,
                    TerrainDatabase = terrainDatabase,
                    DpqAirHeightConfig = turnStateManager != null
                        ? turnStateManager.DpqAirHeightConfigRef
                        : null,
                    Layer = VisionCoverageLayer.Specific(
                        Domain.Air,
                        HeightLevel.AirHigh),
                    EnableLos = matchController == null
                        || matchController.EnableLosValidation
                });
        int marginalHigh = 0;
        int unexploredMarginalHigh = 0;
        foreach (Vector3Int cell in result.VisibleCells)
        {
            if (alliedAirHigh != null
                && alliedAirHigh.Contains(cell))
            {
                continue;
            }

            marginalHigh++;
            if (matchController != null
                && !matchController.IsCellExploredBySlot(
                    PlayerSlotId.FromIndex(observer.SlotIndex),
                    cell))
            {
                unexploredMarginalHigh++;
            }
        }

        int overlapHigh = Mathf.Max(
            0,
            result.VisibleCount - marginalHigh);
        float score =
            marginalHigh * 3f
            + unexploredMarginalHigh * 25f
            + overlapHigh
            + (result.DetectsStealth
                ? marginalHigh * 2f
                : 0f);
        return new AirSurveillanceCoverageSample(
            airLow: 0,
            airHigh: result.VisibleCount,
            marginalAirLow: 0,
            marginalAirHigh: marginalHigh,
            unexploredMarginalAirHigh: unexploredMarginalHigh,
            score: score);
    }

    private static AirSurveillanceSpacingSample
        EvaluateAirSurveillanceSpacing(
            UnitManager self,
            AIWorldSnapshot snapshot,
            Vector3Int cell)
    {
        int airVision = ResolveAirSurveillanceVision(self);
        int preferredDistance = Mathf.Clamp(
            Mathf.CeilToInt(airVision * 0.5f),
            3,
            5);
        int nearestDistance = int.MaxValue;
        if (snapshot != null && snapshot.MyUnits != null)
        {
            foreach (UnitManager ally in snapshot.MyUnits)
            {
                if (ally == null
                    || ally == self
                    || ally.IsDead
                    || ally.IsEmbarked
                    || ally.IsUnderRepair
                    || !IsAirSurveillanceUnit(ally))
                {
                    continue;
                }

                Vector3Int allyCell = ally.CurrentCellPosition;
                allyCell.z = 0;
                int distance = Mathf.RoundToInt(SectorManager.HexDistance(
                    cell,
                    allyCell));
                if (distance < nearestDistance)
                    nearestDistance = distance;
            }
        }

        float penalty = 0f;
        if (nearestDistance < preferredDistance)
        {
            penalty =
                (preferredDistance - nearestDistance) * 260f;
            if (nearestDistance <= 1)
                penalty += 260f;
        }

        return new AirSurveillanceSpacingSample(
            nearestDistance,
            preferredDistance,
            penalty);
    }

    private void CollectAlliedAirSurveillanceCoverage(
        UnitManager self,
        AIWorldSnapshot snapshot,
        HashSet<Vector3Int> airLow,
        HashSet<Vector3Int> airHigh)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return;

        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null
                || ally == self
                || ally.IsDead
                || ally.IsEmbarked
                || ally.IsUnderRepair
                || !IsAirSurveillanceUnit(ally))
            {
                continue;
            }

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            CollectAirSurveillanceCoverageAt(
                ally,
                allyCell,
                airLow,
                airHigh);
        }
    }

    private void CollectAirSurveillanceCoverageAt(
        UnitManager observer,
        Vector3Int observerCell,
        HashSet<Vector3Int> airLow,
        HashSet<Vector3Int> airHigh)
    {
        VisionCoverageResult coverage =
            VisionCoverageService.Evaluate(
                new VisionCoverageRequest
                {
                    Observer = observer,
                    ObserverCell = observerCell,
                    Map = boardTilemap,
                    TerrainDatabase = terrainDatabase,
                    DpqAirHeightConfig = turnStateManager != null
                        ? turnStateManager.DpqAirHeightConfigRef
                        : null,
                    Layer = VisionCoverageLayer.Specific(
                        Domain.Air,
                        HeightLevel.AirHigh),
                    EnableLos = matchController == null
                        || matchController.EnableLosValidation
                });
        airHigh?.UnionWith(coverage.VisibleCells);
    }

    private bool IsAirSurveillanceCellAllowedByRearLine(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int cell,
        Vector3Int anchor,
        bool offensiveAnchor)
    {
        int avoidRange = GetFireSupportConservativeAvoidEnemyRange(unit);
        if (avoidRange <= 0)
            avoidRange = 2;
        if (HasNearbyVisibleEnemy(cell, snapshot.AITeam, avoidRange))
            return false;

        if (!offensiveAnchor)
            return true;

        if (!TryScoreBacklineCell(unit, snapshot, cell, anchor, out AIBacklineScore backline)
            || !backline.InRearSlice
            || backline.Score <= 0f)
        {
            return false;
        }

        return HasAlliedScreenAheadOfFireSupportCell(unit, snapshot, cell, anchor);
    }

    private float ScoreAirSurveillancePostureCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int fromCell,
        Vector3Int anchor,
        bool offensiveAnchor,
        int pathCost,
        out string reason)
    {
        float anchorDist = SectorManager.HexDistance(cell, anchor);
        float threat = CalculateThreatLevel(cell, snapshot.AITeam);
        float dpq = GetTerrainDpqPontos(cell);
        float cohesion = CalculateFireSupportCohesionScore(unit, snapshot, cell);
        float lineGap = 0f;
        float rearLine = offensiveAnchor ? CalculateAirSurveillanceFrontlineRearScore(unit, snapshot, cell, anchor, out lineGap) : 0f;
        float nearestAlly = DistanceToNearestNonSupportAlly(unit, snapshot, cell);
        float isolationPenalty = nearestAlly < float.MaxValue
            ? Mathf.Max(0f, nearestAlly - 3f) * (offensiveAnchor ? 180f : 95f)
            : 0f;
        int airVision = ResolveAirSurveillanceVision(unit);
        int generalVision = ResolveAirSurveillanceGeneralVision(unit);
        float airEnvelope = airVision > 0
            ? Mathf.Max(0f, 420f - Mathf.Abs(anchorDist - Mathf.Min(airVision, 7)) * 70f)
            : 0f;
        float generalEnvelope = generalVision > 0
            ? Mathf.Max(0f, 180f - Mathf.Abs(anchorDist - Mathf.Min(generalVision, 4)) * 55f)
            : 0f;
        float holdBias = cell == fromCell ? 60f : 0f;
        float alliedConstructionPenalty = 0f;
        if (unit != null
            && unit.GetDomain() == Domain.Air
            && IsAlliedConstructionAtCell(cell, snapshot.AITeam))
        {
            alliedConstructionPenalty = cell == fromCell ? 420f : 180f;
        }
        float homeBias = 0f;
        if (!offensiveAnchor && snapshot.MyHQ != null)
        {
            Vector3Int hq = snapshot.MyHQ.CurrentCellPosition;
            hq.z = 0;
            homeBias = -SectorManager.HexDistance(cell, hq) * 25f;
        }

        float score = airEnvelope
            + generalEnvelope
            + dpq * 70f
            + cohesion * (offensiveAnchor ? 2.25f : 1f)
            + rearLine
            + homeBias
            + holdBias
            - threat * 240f
            - alliedConstructionPenalty
            - isolationPenalty
            - pathCost * 22f;

        if (offensiveAnchor && !HasAlliedScreenAheadOfFireSupportCell(unit, snapshot, cell, anchor))
            score -= 360f;

        reason = $"dist={anchorDist:F1} airVis={airVision} vis={generalVision} dpq={dpq:F1} coh={cohesion:F0} rear={rearLine:F0} gap={lineGap:F1} ally={nearestAlly:F1} iso={isolationPenalty:F0} threat={threat:F1} buildPenalty={alliedConstructionPenalty:F0} path={pathCost}";
        return score;
    }

    private float CalculateAirSurveillanceFrontlineRearScore(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int cell, Vector3Int anchor, out float gap)
    {
        gap = 0f;
        if (!TryScoreBacklineCell(unit, snapshot, cell, anchor, out AIBacklineScore score))
            return 0f;

        gap = score.Gap;
        return score.Score;
    }

    private static float DistanceToNearestNonSupportAlly(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int cell)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return float.MaxValue;

        float best = float.MaxValue;
        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;
            if (IsBacklineSupportUnit(ally))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            best = Mathf.Min(best, SectorManager.HexDistance(cell, allyCell));
        }

        return best;
    }

    private bool IsAlliedConstructionAtCell(Vector3Int cell, TeamId aiTeam)
    {
        cell.z = 0;
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        return construction != null && construction.SlotIndex == ResolveAISlotKey(aiTeam);
    }

    private static int ResolveAirSurveillanceVision(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return 0;

        return Mathf.Max(
            data.ResolveVisionFor(Domain.Air, HeightLevel.AirLow),
            data.ResolveVisionFor(Domain.Air, HeightLevel.AirHigh));
    }

    private static int ResolveAirSurveillanceGeneralVision(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return 0;

        return Mathf.Max(1, data.visao);
    }

    private static int CountNonSupportAlliesNearAnchor(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int anchor, int range)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0;

        int count = 0;
        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;
            if (IsBacklineSupportUnit(ally))
                continue;

            Vector3Int cell = ally.CurrentCellPosition;
            cell.z = 0;
            if (SectorManager.HexDistance(cell, anchor) <= range)
                count++;
        }

        return count;
    }

    private static UnitManager FindBestOwnAirAssetForSurveillance(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int fromCell)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return null;

        UnitManager best = null;
        float bestScore = float.MinValue;
        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;
            if (!ally.TryGetUnitData(out UnitData data) || data == null || data.domain != Domain.Air)
                continue;

            bool highValue = data.roles != null
                && (data.roles.Contains(UnitRole.AtaqueAereo)
                    || data.roles.Contains(UnitRole.Transportador)
                    || data.roles.Contains(UnitRole.Interceptador));
            if (!highValue)
                continue;

            Vector3Int cell = ally.CurrentCellPosition;
            cell.z = 0;
            float score = data.cost * 0.01f
                + data.eliteLevel * 80f
                - SectorManager.HexDistance(fromCell, cell) * 10f;
            if (score > bestScore)
            {
                bestScore = score;
                best = ally;
            }
        }

        return best;
    }

    private static ConstructionManager FindBestAirSurveillanceHomeAnchor(AIWorldSnapshot snapshot, Vector3Int fromCell)
    {
        if (snapshot == null || snapshot.MyBuildings == null)
            return null;

        ConstructionManager best = null;
        float bestScore = float.MinValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null)
                continue;

            bool valuable = building.IsPlayerHeadQuarter || building.CanProduceUnitsForSlot(snapshot.AISlotIndex);
            if (!valuable)
                continue;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            float score = (building.IsPlayerHeadQuarter ? 200f : 100f)
                - SectorManager.HexDistance(fromCell, cell) * 8f;
            if (score > bestScore)
            {
                bestScore = score;
                best = building;
            }
        }

        return best;
    }
}
