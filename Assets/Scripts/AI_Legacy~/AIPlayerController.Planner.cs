using System.Collections.Generic;
using UnityEngine;

public partial class AIPlayerController
{
    [System.Serializable]
    private sealed class TeamPlannerRuntimeState
    {
        public TeamId teamId;
        public AIStance currentStance = AIStance.Attack;
        public readonly List<AIPlanIntent> currentTurnPlans = new List<AIPlanIntent>();
        public readonly Dictionary<int, AIPlanIntent> unitRoles = new Dictionary<int, AIPlanIntent>();
        public readonly Dictionary<int, AIPlanAssignment> unitAssignments = new Dictionary<int, AIPlanAssignment>();
        public readonly Dictionary<int, AIPlanEvaluator.MissionAssignmentMemory> previousAssignmentsByUnitId = new Dictionary<int, AIPlanEvaluator.MissionAssignmentMemory>();
        public readonly List<string> currentPlannerLifecycleLogs = new List<string>();
        public readonly Dictionary<string, PlanMetricState> previousPlanMetricsByKey = new Dictionary<string, PlanMetricState>();
        public readonly Dictionary<string, PlannerCatalogPlanState> planCatalogByKey = new Dictionary<string, PlannerCatalogPlanState>();
        public bool hasRestoredStatePendingUse = false;
        public int restoredTurn = -1;
    }

    private enum PlannerCatalogStatus
    {
        Inactive = 0,
        Active = 1,
        Protect = 2,
        Completed = 3
    }

    private sealed class PlannerCatalogPlanState
    {
        public string planKey;
        public string displayName;
        public ConstructionSector sector;
        public bool isFixedPlan;
        public string fixedPlanKind;
        public PlannerCatalogStatus status;
        public bool conquered;
        public bool sectorClear;
        public TeamId controllingTeam = TeamId.Neutral;
        public int progressCurrent;
        public int progressMax;
        public int lastActivationTurn = -1;
        public int lastCompletionTurn = -1;
        public int tacticalRiskScore;
        public int desiredTransportCount;
        public int distToObjective = -1;
        public string selectionReason;
        public readonly List<AIPlanAssignment> assignments = new List<AIPlanAssignment>();
    }

    private static SavedCatalogPlan BuildSavedCatalogPlan(PlannerCatalogPlanState planState)
    {
        if (planState == null || string.IsNullOrWhiteSpace(planState.planKey))
            return null;

        return new SavedCatalogPlan
        {
            planKey = planState.planKey,
            displayName = planState.displayName ?? string.Empty,
            sector = planState.sector.ToString(),
            isFixedPlan = planState.isFixedPlan,
            fixedPlanKind = planState.fixedPlanKind ?? string.Empty,
            status = (int)planState.status,
            statusName = planState.status.ToString(),
            conquered = planState.conquered,
            sectorClear = planState.sectorClear,
            controllingTeam = planState.controllingTeam.ToString(),
            progressCurrent = planState.progressCurrent,
            progressMax = planState.progressMax,
            lastActivationTurn = planState.lastActivationTurn,
            lastCompletionTurn = planState.lastCompletionTurn,
            tacticalRiskScore = planState.tacticalRiskScore,
            selectionReason = planState.selectionReason ?? string.Empty
        };
    }

    private static PlannerCatalogStatus TryParseCatalogStatus(int value, string name)
    {
        if (System.Enum.IsDefined(typeof(PlannerCatalogStatus), value))
            return (PlannerCatalogStatus)value;

        if (!string.IsNullOrWhiteSpace(name) && System.Enum.TryParse(name, true, out PlannerCatalogStatus parsed))
            return parsed;

        return PlannerCatalogStatus.Inactive;
    }

    private static bool TryApplySavedCatalogPlan(TeamPlannerRuntimeState state, SavedCatalogPlan savedPlan)
    {
        if (state == null || savedPlan == null || string.IsNullOrWhiteSpace(savedPlan.planKey))
            return false;

        ConstructionSector sector = ConstructionSector.Base1;
        if (!string.IsNullOrWhiteSpace(savedPlan.sector) && System.Enum.TryParse(savedPlan.sector, true, out ConstructionSector parsedSector))
            sector = parsedSector;

        EnsureCatalogPlan(
            state,
            savedPlan.planKey,
            !string.IsNullOrWhiteSpace(savedPlan.displayName) ? savedPlan.displayName : sector.ToString(),
            sector,
            savedPlan.isFixedPlan,
            savedPlan.fixedPlanKind);

        if (!state.planCatalogByKey.TryGetValue(savedPlan.planKey, out PlannerCatalogPlanState planState) || planState == null)
            return false;

        planState.status = TryParseCatalogStatus(savedPlan.status, savedPlan.statusName);
        planState.conquered = savedPlan.conquered;
        planState.sectorClear = savedPlan.sectorClear;
        if (!string.IsNullOrWhiteSpace(savedPlan.controllingTeam) && System.Enum.TryParse(savedPlan.controllingTeam, true, out TeamId parsedControllingTeam))
            planState.controllingTeam = parsedControllingTeam;
        planState.progressCurrent = Mathf.Max(0, savedPlan.progressCurrent);
        planState.progressMax = Mathf.Max(0, savedPlan.progressMax);
        planState.lastActivationTurn = savedPlan.lastActivationTurn;
        planState.lastCompletionTurn = savedPlan.lastCompletionTurn;
        planState.tacticalRiskScore = Mathf.Max(0, savedPlan.tacticalRiskScore);
        planState.selectionReason = savedPlan.selectionReason ?? string.Empty;
        return true;
    }

    private TeamPlannerRuntimeState GetOrCreatePlannerState(TeamId team)
    {
        if (!plannerStateByTeam.TryGetValue(team, out TeamPlannerRuntimeState state))
        {
            state = new TeamPlannerRuntimeState { teamId = team };
            plannerStateByTeam[team] = state;
            plannerDebugViewDirty = true;
        }

        return state;
    }

    private TeamId GetDebugReferenceTeam()
    {
        if (matchController != null)
            return matchController.ActiveTeam;

        return plannerContextTeam;
    }

    private void SetPlannerContextTeam(TeamId team)
    {
        plannerContextTeam = team;
        GetOrCreatePlannerState(team);
    }

    private void MarkPlannerDebugViewDirty()
    {
        plannerDebugViewDirty = true;
    }

    private void RefreshPlanCatalogForTeam(TeamId team, TeamPlannerRuntimeState state)
    {
        if (state == null)
            return;

        EnsureSectorCatalogPlans(state);
        EnsureInvasionCatalogPlans(state, team);

        Dictionary<string, AIPlanIntent> activePlanByKey = new Dictionary<string, AIPlanIntent>();
        for (int i = 0; i < state.currentTurnPlans.Count; i++)
        {
            AIPlanIntent intent = state.currentTurnPlans[i];
            string key = AIPlanEvaluator.BuildPlanKey(intent);
            if (intent == null || string.IsNullOrWhiteSpace(key))
                continue;
            activePlanByKey[key] = intent;
        }

        Dictionary<ConstructionSector, SectorManager.SectorInfo> sectorInfoBySector = BuildSectorInfoMap();
        AISnapshot sectorClearSnapshot = BuildSnapshotForTeam(team);
        int currentTurn = matchController != null ? matchController.CurrentTurn : 0;

        foreach (PlannerCatalogPlanState planState in state.planCatalogByKey.Values)
        {
            if (planState == null)
                continue;

            PlannerCatalogStatus previousStatus = planState.status;
            planState.assignments.Clear();

            if (activePlanByKey.TryGetValue(planState.planKey, out AIPlanIntent activeIntent) && activeIntent != null)
            {
                planState.status = PlannerCatalogStatus.Active;
                if (IsProtectIntent(activeIntent))
                    planState.status = PlannerCatalogStatus.Protect;
                planState.displayName = ResolveCatalogDisplayName(planState, activeIntent);
                planState.selectionReason = activeIntent.SelectionReason ?? string.Empty;
                planState.tacticalRiskScore = Mathf.Max(0, activeIntent.TacticalRiskScore);
                planState.desiredTransportCount = Mathf.Max(0, activeIntent.DesiredTransportCount);
                for (int a = 0; a < activeIntent.Assignments.Count; a++)
                {
                    AIPlanAssignment assignment = activeIntent.Assignments[a];
                    if (assignment != null)
                        planState.assignments.Add(assignment);
                }

                // Distancia maxima entre qualquer capturer atribuido e o objetivo de captura.
                // Usa o mais afastado (recem comprado, perto do HQ) — e esse que o APC deve buscar.
                // Usa GetHexDistance com tilemap (caminho valido) para ser consistente com Sensor Medir.
                planState.distToObjective = -1;
                if (activeIntent.HasCaptureTarget && sectorClearSnapshot?.FriendlyUnits != null)
                {
                    Vector3Int capTarget = activeIntent.CaptureTargetCell;
                    capTarget.z = 0;
                    UnityEngine.Tilemaps.Tilemap boardMap = sectorClearSnapshot.BoardTilemap;
                    for (int a = 0; a < activeIntent.Assignments.Count; a++)
                    {
                        AIPlanAssignment assignment = activeIntent.Assignments[a];
                        if (assignment == null || assignment.Role != AIPlanRole.Capture)
                            continue;
                        for (int u = 0; u < sectorClearSnapshot.FriendlyUnits.Count; u++)
                        {
                            UnitManager unit = sectorClearSnapshot.FriendlyUnits[u];
                            if (unit == null || unit.InstanceId != assignment.UnitInstanceId)
                                continue;
                            Vector3Int unitCell = unit.CurrentCellPosition;
                            unitCell.z = 0;
                            int dist = boardMap != null
                                ? GetHexDistance(boardMap, unitCell, capTarget, 64)
                                : GetHexDistance(unitCell, capTarget);
                            if (dist != int.MaxValue && dist > planState.distToObjective)
                                planState.distToObjective = dist;
                            break;
                        }
                    }
                }
            }
            else
            {
                planState.status = PlannerCatalogStatus.Inactive;
                planState.displayName = ResolveCatalogDisplayName(planState, null);
                planState.selectionReason = string.Empty;
                planState.tacticalRiskScore = 0;
                planState.desiredTransportCount = 0;
                planState.distToObjective = -1;
            }

            if (!planState.isFixedPlan && sectorInfoBySector.TryGetValue(planState.sector, out SectorManager.SectorInfo sectorInfo) && sectorInfo != null)
            {
                int teamProgress = 0;
                IReadOnlyList<SectorManager.SectorConstructionInfo> constructions = sectorInfo.Constructions;
                for (int i = 0; i < constructions.Count; i++)
                {
                    SectorManager.SectorConstructionInfo construction = constructions[i];
                    if (construction == null)
                        continue;

                    int maxCapture = Mathf.Max(0, construction.CapturePointsMax);
                    int currentCapture = Mathf.Clamp(construction.CurrentCapturePoints, 0, maxCapture);
                    int contribution = construction.OwnerTeam == team
                        ? currentCapture
                        : Mathf.Max(0, maxCapture - currentCapture);

                    teamProgress += contribution;
                }

                planState.progressCurrent = teamProgress;
                planState.progressMax = Mathf.Max(0, sectorInfo.TotalCapturePointsMax);
                planState.conquered = sectorInfo.IsFullyControlled && sectorInfo.ControllingTeam == team;
                planState.controllingTeam = sectorInfo.ControllingTeam;
                planState.sectorClear = planState.conquered
                    && !sectorInfo.IsDisputed
                    && !sectorInfo.HasPartialCapture
                    && IsSectorClearForTeam(sectorClearSnapshot, sectorInfo.RepresentativeCell, 2);
                if (planState.conquered
                    && planState.sectorClear
                    && planState.status != PlannerCatalogStatus.Active
                    && planState.status != PlannerCatalogStatus.Protect)
                    planState.status = PlannerCatalogStatus.Completed;
            }
            else if (planState.isFixedPlan)
            {
                planState.progressCurrent = 0;
                planState.progressMax = 0;
                planState.conquered = false;
                planState.sectorClear = false;
                planState.controllingTeam = TeamId.Neutral;
            }
            else
            {
                planState.progressCurrent = 0;
                planState.progressMax = 0;
                planState.conquered = false;
                planState.sectorClear = false;
                planState.controllingTeam = TeamId.Neutral;
            }

            if ((planState.status == PlannerCatalogStatus.Active || planState.status == PlannerCatalogStatus.Protect)
                && previousStatus != planState.status)
                planState.lastActivationTurn = currentTurn;
            if (planState.status == PlannerCatalogStatus.Completed && previousStatus != PlannerCatalogStatus.Completed)
                planState.lastCompletionTurn = currentTurn;
        }
    }

    private void EnsureSectorCatalogPlans(TeamPlannerRuntimeState state)
    {
        IReadOnlyList<SectorManager.SectorInfo> sectors = SectorManager.GetAllSectorInfos();
        for (int i = 0; i < sectors.Count; i++)
        {
            SectorManager.SectorInfo sectorInfo = sectors[i];
            if (sectorInfo == null)
                continue;

            string key = BuildSectorCatalogPlanKey(sectorInfo.Sector);
            EnsureCatalogPlan(state, key, sectorInfo.Sector.ToString(), sectorInfo.Sector, false, string.Empty);
        }
    }

    private void EnsureInvasionCatalogPlans(TeamPlannerRuntimeState state, TeamId aiTeam)
    {
        IReadOnlyList<SectorManager.SectorInfo> bases = SectorManager.GetAllBaseInfos();
        for (int i = 0; i < bases.Count; i++)
        {
            SectorManager.SectorInfo baseInfo = bases[i];
            if (baseInfo == null)
                continue;

            if (baseInfo.ControllingTeam == aiTeam)
                continue;

            string key = BuildInvasionPlanKey(baseInfo.Sector);
            string displayName = $"Invasao {baseInfo.Sector}";
            EnsureCatalogPlan(state, key, displayName, baseInfo.Sector, false, string.Empty);
        }
    }

    private static string BuildInvasionPlanKey(ConstructionSector sector)
    {
        return $"invasion:{sector}";
    }

    private static void EnsureCatalogPlan(TeamPlannerRuntimeState state, string key, string displayName, ConstructionSector sector, bool isFixedPlan, string fixedPlanKind)
    {
        if (state == null || string.IsNullOrWhiteSpace(key))
            return;

        if (!state.planCatalogByKey.TryGetValue(key, out PlannerCatalogPlanState planState) || planState == null)
        {
            planState = new PlannerCatalogPlanState { planKey = key };
            state.planCatalogByKey[key] = planState;
        }

        planState.displayName = displayName ?? string.Empty;
        planState.sector = sector;
        planState.isFixedPlan = isFixedPlan;
        planState.fixedPlanKind = fixedPlanKind ?? string.Empty;
    }

    private static Dictionary<ConstructionSector, SectorManager.SectorInfo> BuildSectorInfoMap()
    {
        Dictionary<ConstructionSector, SectorManager.SectorInfo> result = new Dictionary<ConstructionSector, SectorManager.SectorInfo>();
        IReadOnlyList<SectorManager.SectorInfo> sectors = SectorManager.GetAllSectorInfos();
        for (int i = 0; i < sectors.Count; i++)
        {
            SectorManager.SectorInfo sectorInfo = sectors[i];
            if (sectorInfo != null)
                result[sectorInfo.Sector] = sectorInfo;
        }

        return result;
    }

    private static int CompareCatalogPlansForDebug(PlannerCatalogPlanState a, PlannerCatalogPlanState b)
    {
        int statusCompare = CompareStatusForDebug(a != null ? a.status : PlannerCatalogStatus.Inactive, b != null ? b.status : PlannerCatalogStatus.Inactive);
        if (statusCompare != 0)
            return statusCompare;

        int fixedCompare = (a != null && a.isFixedPlan ? 0 : 1).CompareTo(b != null && b.isFixedPlan ? 0 : 1);
        if (fixedCompare != 0)
            return fixedCompare;

        return string.CompareOrdinal(a != null ? a.planKey : string.Empty, b != null ? b.planKey : string.Empty);
    }

    private static int CompareStatusForDebug(PlannerCatalogStatus a, PlannerCatalogStatus b)
    {
        int rankA = a == PlannerCatalogStatus.Active ? 0
            : a == PlannerCatalogStatus.Protect ? 1
            : a == PlannerCatalogStatus.Inactive ? 2
            : a == PlannerCatalogStatus.Completed ? 3
            : 4;
        int rankB = b == PlannerCatalogStatus.Active ? 0
            : b == PlannerCatalogStatus.Protect ? 1
            : b == PlannerCatalogStatus.Inactive ? 2
            : b == PlannerCatalogStatus.Completed ? 3
            : 4;
        return rankA.CompareTo(rankB);
    }

    private string ResolveCatalogDisplayName(PlannerCatalogPlanState planState, AIPlanIntent activeIntent)
    {
        if (planState == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(planState.displayName))
            return planState.displayName;
        if (activeIntent != null)
            return ResolvePlanDisplayName(activeIntent);
        return planState.sector.ToString();
    }

    private static string BuildSectorCatalogPlanKey(ConstructionSector sector)
    {
        return $"dynamic:capture:{sector}";
    }

    private List<TeamId> GetPlannerDebugTeams()
    {
        EnsureAIDataDefaults();

        List<TeamId> teams = new List<TeamId>();
        if (matchController != null)
        {
            IReadOnlyList<TeamId> players = matchController.Players;
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    TeamId team = players[i];
                    if (!matchController.IsPlayerAI(team))
                        continue;

                    GetOrCreatePlannerState(team);
                    teams.Add(team);
                }
            }
        }

        if (teams.Count == 0)
            teams.AddRange(plannerStateByTeam.Keys);

        teams.Sort((a, b) => ((int)a).CompareTo((int)b));
        return teams;
    }

    public void RefreshPlannerDebugViewNow()
    {
        plannerDebugView.Clear();

        List<TeamId> teams = GetPlannerDebugTeams();

        for (int t = 0; t < teams.Count; t++)
        {
            TeamId team = teams[t];
            TeamPlannerRuntimeState state = plannerStateByTeam[team];
            TeamPlannerDebugView teamView = new TeamPlannerDebugView
            {
                team = team,
                currentStanceName = state.currentStance.ToString()
            };

            RefreshPlanCatalogForTeam(team, state);

            List<PlannerCatalogPlanState> catalogPlans = new List<PlannerCatalogPlanState>(state.planCatalogByKey.Values);
            catalogPlans.Sort(CompareCatalogPlansForDebug);

            for (int i = 0; i < catalogPlans.Count; i++)
            {
                PlannerCatalogPlanState planState = catalogPlans[i];
                if (planState == null)
                    continue;

                PlanDebugView planView = new PlanDebugView
                {
                    planKey = planState.planKey,
                    displayName = planState.displayName,
                    sector = planState.sector.ToString(),
                    status = planState.status.ToString(),
                    conquered = planState.conquered,
                    sectorClear = planState.sectorClear,
                    controllingTeam = planState.controllingTeam.ToString(),
                    progressCurrent = planState.progressCurrent,
                    progressMax = planState.progressMax,
                    lastActivationTurn = planState.lastActivationTurn,
                    lastCompletionTurn = planState.lastCompletionTurn,
                    tacticalRiskScore = planState.tacticalRiskScore,
                    desiredTransportCount = planState.desiredTransportCount,
                    distToObjective = planState.distToObjective,
                    selectionReason = planState.selectionReason
                };

                List<AIPlanAssignment> assignments = new List<AIPlanAssignment>(planState.assignments);
                assignments.Sort((a, b) => a.UnitInstanceId.CompareTo(b.UnitInstanceId));
                for (int a = 0; a < assignments.Count; a++)
                {
                    AIPlanAssignment assignment = assignments[a];
                    if (assignment == null)
                        continue;

                    UnitManager unit = FindUnitById(assignment.UnitInstanceId);
                    planView.assignments.Add(new AssignmentDebugView
                    {
                        unitInstanceId = assignment.UnitInstanceId,
                        unitName = unit != null ? unit.name : string.Empty,
                        role = assignment.Role.ToDebugLabel()
                    });
                }

                teamView.plans.Add(planView);
            }

            foreach (var kv in state.unitAssignments)
            {
                AIPlanAssignment assignment = kv.Value;
                if (assignment == null)
                    continue;
                if (assignment.Intent != null && !state.currentTurnPlans.Contains(assignment.Intent))
                    Debug.LogWarning($"[AI][planner-debug] Inconsistencia: time {team} possui assignment {assignment.UnitInstanceId} para plano ausente no state.");
            }

            plannerDebugView.Add(teamView);
        }

        plannerDebugViewDirty = false;
    }

    private void RefreshPlannerDebugViewIfDirty()
    {
        if (!plannerDebugViewDirty)
            return;

        RefreshPlannerDebugViewNow();
    }

    public void RemoveTeamState(TeamId team)
    {
        if (!plannerStateByTeam.Remove(team))
            return;

        MarkPlannerDebugViewDirty();
        RefreshPlannerDebugViewIfDirty();
        Debug.Log($"[AI][planner] estado removido para time {team}.");
    }

    private void HandleTeamDefeated(TeamId team)
    {
        RemoveTeamState(team);
    }

    private void HandleUnitDestroyedPlannerCleanup(UnitManager unit)
    {
        if (unit == null)
            return;

        int unitId = unit.InstanceId;
        bool changed = false;

        foreach (var stateKvp in plannerStateByTeam)
        {
            TeamPlannerRuntimeState state = stateKvp.Value;
            if (state == null)
                continue;

            if (state.unitRoles.Remove(unitId))
                changed = true;
            if (state.unitAssignments.Remove(unitId))
                changed = true;
            if (state.previousAssignmentsByUnitId.Remove(unitId))
                changed = true;

            for (int i = 0; i < state.currentTurnPlans.Count; i++)
            {
                AIPlanIntent intent = state.currentTurnPlans[i];
                if (intent == null || intent.Assignments == null)
                    continue;

                int removed = intent.Assignments.RemoveAll(a => a != null && a.UnitInstanceId == unitId);
                if (removed > 0)
                    changed = true;
            }
        }

        if (!changed)
            return;

        MarkPlannerDebugViewDirty();
        RefreshPlannerDebugViewIfDirty();
    }
}
