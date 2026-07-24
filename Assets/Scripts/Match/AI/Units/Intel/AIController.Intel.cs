using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private PlayerAction TryDecideIntelAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!IsIntelUnit(unit) || snapshot == null)
            return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths != null && paths.Count > 0
            && TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction vacateAction))
        {
            return vacateAction;
        }

        if (TryResolveIntelAnchor(unit, snapshot, plan, fromCell, out Vector3Int anchor, out bool offensiveAnchor, out string anchorReason)
            && TryFindIntelPostureCell(unit, snapshot, fromCell, anchor, offensiveAnchor, paths, occupied, out Vector3Int postureCell, out string postureReason))
        {
            if (postureCell != fromCell)
            {
                Debug.Log($"{TL("Intel")} {unit.InstanceId} reposiciona retaguarda via {postureCell} anchor={anchor} ({anchorReason}; {postureReason})");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, postureCell, paths);
            }

            Debug.Log($"{TL("Intel")} {unit.InstanceId} segura observacao @ {fromCell} anchor={anchor} ({anchorReason}; {postureReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        Vector3Int conservative = FindConservativeRogueFireSupportCell(unit, snapshot, fromCell, paths, occupied);
        if (conservative != fromCell)
        {
            Debug.Log($"{TL("Intel")} {unit.InstanceId} sem anchor seguro, reagrupa retaguarda via {conservative}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, conservative, paths);
        }

        Debug.Log($"{TL("Intel")} {unit.InstanceId} aguarda em retaguarda @ {fromCell}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private static bool IsIntelUnit(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        return data.roles != null && data.roles.Contains(UnitRole.Intel);
    }

    private static bool IsBacklineSupportUnit(UnitManager unit)
    {
        return IsFireSupportUnit(unit) || IsIntelUnit(unit);
    }

    private bool TryResolveIntelAnchor(
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

        UnitManager airAsset = FindBestOwnAirAssetForIntel(unit, snapshot, fromCell);
        if (airAsset != null)
        {
            anchor = airAsset.CurrentCellPosition;
            anchor.z = 0;
            offensiveAnchor = false;
            reason = $"cobertura aerea #{airAsset.InstanceId}";
            return true;
        }

        ConstructionManager home = FindBestIntelHomeAnchor(snapshot, fromCell);
        if (home != null)
        {
            anchor = home.CurrentCellPosition;
            anchor.z = 0;
            reason = $"{home.ConstructionDisplayName}#{home.InstanceId}";
            return true;
        }

        return false;
    }

    private bool TryFindIntelPostureCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int anchor,
        bool offensiveAnchor,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = "";
        if (unit == null || snapshot == null)
            return false;

        TeamObjectivePlan capPlan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        float fromScore = ScoreIntelPostureCell(unit, snapshot, fromCell, fromCell, anchor, offensiveAnchor, 0, out string fromReason);
        float bestScore = fromScore;
        string bestReason = fromReason;

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
                if (!IsIntelCellAllowedByRearLine(unit, snapshot, fromCell, cell, anchor, offensiveAnchor))
                    continue;

                float score = ScoreIntelPostureCell(
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
                    bestReason = scoreReason;
                }
            }
        }

        reason = $"{bestReason} score={bestScore:F0} hold={fromScore:F0}";
        return true;
    }

    private bool IsIntelCellAllowedByRearLine(
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

    private float ScoreIntelPostureCell(
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
        float rearLine = offensiveAnchor ? CalculateIntelFrontlineRearScore(unit, snapshot, cell, anchor, out lineGap) : 0f;
        float nearestAlly = DistanceToNearestNonSupportAlly(unit, snapshot, cell);
        float isolationPenalty = nearestAlly < float.MaxValue
            ? Mathf.Max(0f, nearestAlly - 3f) * (offensiveAnchor ? 180f : 95f)
            : 0f;
        int airVision = ResolveIntelAirVision(unit);
        int generalVision = ResolveIntelGeneralVision(unit);
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

    private float CalculateIntelFrontlineRearScore(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int cell, Vector3Int anchor, out float gap)
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

    private static int ResolveIntelAirVision(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return 0;

        return Mathf.Max(
            data.ResolveVisionFor(Domain.Air, HeightLevel.AirLow),
            data.ResolveVisionFor(Domain.Air, HeightLevel.AirHigh));
    }

    private static int ResolveIntelGeneralVision(UnitManager unit)
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

    private static UnitManager FindBestOwnAirAssetForIntel(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int fromCell)
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

    private static ConstructionManager FindBestIntelHomeAnchor(AIWorldSnapshot snapshot, Vector3Int fromCell)
    {
        if (snapshot == null || snapshot.MyBuildings == null)
            return null;

        ConstructionManager best = null;
        float bestScore = float.MinValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null)
                continue;

            bool valuable = building.IsPlayerHeadQuarter || building.CanProduceUnitsForTeam(snapshot.AITeam);
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
