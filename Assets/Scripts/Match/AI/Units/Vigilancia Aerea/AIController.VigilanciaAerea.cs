using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private enum AirSurveillancePolicyStage
    {
        EmergencyAndRepair,
        Recovery,
        TransportOrPlatform,
        ExitObstructedPosition,
        ImproveAirCoverage,
        ConservativeRear,
        Hold,
        Orbit
    }

    private PlayerAction TryDecideAirSurveillanceAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!IsAirSurveillanceUnit(unit) || snapshot == null)
            return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            BuildFireSupportPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        EwacsRecoverySnapshot ewacsRecovery =
            BuildEwacsRecoverySnapshot(unit);
        if (ewacsRecovery != null
            && TryBuildEwacsEmergencyRecoveryAction(
                unit,
                snapshot,
                fromCell,
                paths,
                ewacsRecovery,
                out PlayerAction ewacsRecoveryAction,
                out string ewacsRecoveryReason))
        {
            LogAirSurveillancePolicyStage(
                unit,
                AirSurveillancePolicyStage.Recovery,
                ewacsRecoveryReason);
            return ewacsRecoveryAction;
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
                LogAirSurveillancePolicyStage(
                    unit,
                    AirSurveillancePolicyStage.TransportOrPlatform,
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
        if (ewacsRecovery != null
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
            LogAirSurveillancePolicyStage(
                unit,
                AirSurveillancePolicyStage.TransportOrPlatform,
                platformReason);
            return platformAction;
        }

        if (paths != null && paths.Count > 0
            && TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction vacateAction))
        {
            LogAirSurveillancePolicyStage(
                unit,
                AirSurveillancePolicyStage.ExitObstructedPosition,
                $"libera construcao em {fromCell}");
            return vacateAction;
        }

        if (hasSurveillanceAnchor
            && TryFindAirSurveillancePostureCell(unit, snapshot, fromCell, anchor, offensiveAnchor, paths, occupied, ewacsRecovery, out Vector3Int postureCell, out string postureReason))
        {
            if (postureCell != fromCell)
            {
                LogAirSurveillancePolicyStage(
                    unit,
                    AirSurveillancePolicyStage.ImproveAirCoverage,
                    $"reposiciona via {postureCell} anchor={anchor} ({anchorReason}; {postureReason})");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, postureCell, paths);
            }

            LogAirSurveillancePolicyStage(
                unit,
                ewacsRecovery != null
                    ? AirSurveillancePolicyStage.Orbit
                    : AirSurveillancePolicyStage.Hold,
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
            LogAirSurveillancePolicyStage(
                unit,
                AirSurveillancePolicyStage.ConservativeRear,
                "sem ancora operacional; acompanha retaguarda aliada");
            return conservativeAction;
        }

        LogAirSurveillancePolicyStage(
            unit,
            ewacsRecovery != null
                ? AirSurveillancePolicyStage.Orbit
                : AirSurveillancePolicyStage.Hold,
            ewacsRecovery != null
                ? $"sem direcao segura; mantem orbita em {fromCell}"
                : $"sem direcao segura; aguarda em {fromCell}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private void LogAirSurveillancePolicyStage(
        UnitManager unit,
        AirSurveillancePolicyStage stage,
        string reason)
    {
        if (unit == null)
            return;

        Debug.Log(
            $"{TL("VigilanciaAerea")} {unit.InstanceId} " +
            $"policy={stage} {reason}");
    }

    private static bool IsAirSurveillanceUnit(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        return data.roles != null
            && data.roles.Contains(UnitRole.VigilanciaAerea);
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

        TeamObjectivePlan capPlan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        float fromScore = ScoreAirSurveillancePostureCell(unit, snapshot, fromCell, fromCell, anchor, offensiveAnchor, 0, out string fromReason);
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
        string bestReason =
            ewacsRecovery != null
                ? $"{fromReason} recovery={fromRecoveryReason}"
                : fromReason;

        if (paths != null)
        {
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
                if (!IsAirSurveillanceCellAllowedByRearLine(unit, snapshot, fromCell, cell, anchor, offensiveAnchor))
                    continue;
                paths.TryGetValue(
                    cell,
                    out List<Vector3Int> candidatePath);
                if (!IsEwacsRecoveryCellSafe(
                        unit,
                        cell,
                        candidatePath,
                        ewacsRecovery,
                        out string recoveryReason))
                {
                    continue;
                }

                float score = ScoreAirSurveillancePostureCell(
                    unit,
                    snapshot,
                    cell,
                    fromCell,
                    anchor,
                    offensiveAnchor,
                    GetPathStepCount(paths, cell),
                    out string scoreReason);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                    bestReason = ewacsRecovery != null
                        ? $"{scoreReason} recovery={recoveryReason}"
                        : scoreReason;
                }
            }
        }

        reason = $"{bestReason} score={bestScore:F0} hold={fromScore:F0}";
        return true;
    }

    private readonly struct MobileRadarCoverageSample
    {
        public readonly int AirLow;
        public readonly int AirHigh;
        public readonly int MarginalAirLow;
        public readonly int MarginalAirHigh;
        public readonly float Score;

        public int VisibleCells => AirLow + AirHigh;

        public MobileRadarCoverageSample(
            int airLow,
            int airHigh,
            int marginalAirLow,
            int marginalAirHigh,
            float score)
        {
            AirLow = airLow;
            AirHigh = airHigh;
            MarginalAirLow = marginalAirLow;
            MarginalAirHigh = marginalAirHigh;
            Score = score;
        }

        public override string ToString()
        {
            return
                $"low={AirLow}(new={MarginalAirLow}) " +
                $"high={AirHigh}(new={MarginalAirHigh}) " +
                $"coverage={Score:F0}";
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
                            $"{TL("VigilanciaAerea")} " +
                            $"Radar#{radar.InstanceId} " +
                            $"QueroCarona: {message}")
                        : null
                });

        Debug.Log(
            $"{TL("VigilanciaAerea")} Radar#{radar.InstanceId} " +
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

        MobileRadarCoverageSample originCoverage =
            EvaluateMobileRadarCoverage(
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
                originCoverage);
        float bestScore = float.MinValue;
        MobileRadarCoverageSample bestCoverage = default;

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
                MobileRadarCoverageSample coverage =
                    EvaluateMobileRadarCoverage(
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
                        coverage);
                if (score > bestScore)
                {
                    bestScore = score;
                    target = cell;
                    bestCoverage = coverage;
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
            $"{originCoverage}->{bestCoverage} " +
            $"gain={gain:F0} required={requiredGain:F0}";
        return !requireCoverageGain || gain >= requiredGain;
    }

    private float ScoreMobileRadarTransportTarget(
        UnitManager radar,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int anchor,
        MobileRadarCoverageSample coverage)
    {
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
            - threat * 220f;
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

        var alliedAirLow = new HashSet<Vector3Int>();
        var alliedAirHigh = new HashSet<Vector3Int>();
        CollectAlliedAirSurveillanceCoverage(
            unit,
            snapshot,
            alliedAirLow,
            alliedAirHigh);

        MobileRadarCoverageSample fromCoverage =
            EvaluateMobileRadarCoverage(
                unit,
                fromCell,
                alliedAirLow,
                alliedAirHigh);
        float fromPosture = ScoreAirSurveillancePostureCell(
            unit,
            snapshot,
            fromCell,
            fromCell,
            anchor,
            offensiveAnchor,
            pathCost: 0,
            out _);
        float fromScore = fromCoverage.Score + fromPosture * 0.2f;
        float bestScore = fromScore;
        MobileRadarCoverageSample bestCoverage = fromCoverage;
        int bestPathCost = 0;

        TeamObjectivePlan capPlan =
            ObjectiveManager.GetPlanForSlot(
                PlayerSlotId.FromIndex(snapshot.AISlotIndex));

        if (paths != null)
        {
            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (cell == fromCell)
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

                int pathCost = GetPathStepCount(paths, cell);
                MobileRadarCoverageSample coverage =
                    EvaluateMobileRadarCoverage(
                        unit,
                        cell,
                        alliedAirLow,
                        alliedAirHigh);
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
                float score = coverage.Score + posture * 0.2f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestCell = cell;
                bestCoverage = coverage;
                bestPathCost = pathCost;
            }
        }

        float requiredGain = fromCoverage.VisibleCells <= 2
            ? 20f
            : Mathf.Max(90f, fromCoverage.Score * 0.06f);
        float actualGain = bestScore - fromScore;
        if (bestCell == fromCell || actualGain < requiredGain)
        {
            bestCell = fromCell;
            reason =
                $"radar stationary hold {fromCoverage} " +
                $"bestGain={actualGain:F0} required={requiredGain:F0}";
            return true;
        }

        reason =
            $"radar stationary move {fromCoverage} -> " +
            $"{bestCoverage} gain={actualGain:F0} " +
            $"required={requiredGain:F0} path={bestPathCost}";
        return true;
    }

    private MobileRadarCoverageSample EvaluateMobileRadarCoverage(
        UnitManager radar,
        Vector3Int observerCell,
        HashSet<Vector3Int> alliedAirLow,
        HashSet<Vector3Int> alliedAirHigh)
    {
        var airLow = new HashSet<Vector3Int>();
        var airHigh = new HashSet<Vector3Int>();
        CollectAirSurveillanceCoverageAt(
            radar,
            observerCell,
            airLow,
            airHigh);

        int marginalLow = 0;
        foreach (Vector3Int cell in airLow)
        {
            if (alliedAirLow == null
                || !alliedAirLow.Contains(cell))
            {
                marginalLow++;
            }
        }

        int marginalHigh = 0;
        foreach (Vector3Int cell in airHigh)
        {
            if (alliedAirHigh == null
                || !alliedAirHigh.Contains(cell))
            {
                marginalHigh++;
            }
        }

        radar.TryGetUnitData(out UnitData data);
        bool detectsLowStealth = data != null
            && data.CanDetectStealthFor(
                Domain.Air,
                HeightLevel.AirLow);
        bool detectsHighStealth = data != null
            && data.CanDetectStealthFor(
                Domain.Air,
                HeightLevel.AirHigh);

        float score =
            airLow.Count * 8f
            + airHigh.Count * 10f
            + marginalLow * 6f
            + marginalHigh * 7f
            + (detectsLowStealth ? airLow.Count * 2f : 0f)
            + (detectsHighStealth ? airHigh.Count * 2f : 0f);
        return new MobileRadarCoverageSample(
            airLow.Count,
            airHigh.Count,
            marginalLow,
            marginalHigh,
            score);
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
        DPQAirHeightConfig airConfig = turnStateManager != null
            ? turnStateManager.DpqAirHeightConfigRef
            : null;
        bool enableLos = matchController == null
            || matchController.EnableLosValidation;

        PodeDetectarSensor.CollectVisibleAirCellsAt(
            observer,
            observerCell,
            boardTilemap,
            terrainDatabase,
            airLow,
            HeightLevel.AirLow,
            airConfig,
            enableLos);
        PodeDetectarSensor.CollectVisibleAirCellsAt(
            observer,
            observerCell,
            boardTilemap,
            terrainDatabase,
            airHigh,
            HeightLevel.AirHigh,
            airConfig,
            enableLos);
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
