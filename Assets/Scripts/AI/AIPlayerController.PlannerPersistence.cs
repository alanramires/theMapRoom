using System.Collections.Generic;
using UnityEngine;

public partial class AIPlayerController
{
    public AIPlannerMultiTeamSaveData BuildPlannerSaveData()
    {
        AIPlannerMultiTeamSaveData data = new AIPlannerMultiTeamSaveData();
        List<TeamId> teams = new List<TeamId>(plannerStateByTeam.Keys);
        teams.Sort((a, b) => ((int)a).CompareTo((int)b));

        for (int t = 0; t < teams.Count; t++)
        {
            TeamId team = teams[t];
            if (matchController != null && !matchController.IsPlayerAI(team))
                continue;

            TeamPlannerRuntimeState state = plannerStateByTeam[team];
            TeamPlannerSaveBlock teamBlock = new TeamPlannerSaveBlock
            {
                teamId = (int)team,
                hasRestoredStatePendingUse = state.hasRestoredStatePendingUse,
                restoredTurn = state.restoredTurn
            };

            RefreshPlanCatalogForTeam(team, state);
            foreach (var catalogKvp in state.planCatalogByKey)
            {
                SavedCatalogPlan savedCatalog = BuildSavedCatalogPlan(catalogKvp.Value);
                if (savedCatalog != null)
                    teamBlock.catalogPlans.Add(savedCatalog);
            }

            for (int i = 0; i < state.currentTurnPlans.Count; i++)
            {
                AIPlanIntent intent = state.currentTurnPlans[i];
                if (intent == null)
                    continue;

                string planKey = AIPlanEvaluator.BuildPlanKey(intent);
                if (string.IsNullOrWhiteSpace(planKey))
                    continue;

                teamBlock.activePlans.Add(new SavedPlan
                {
                    planKey = planKey,
                    displayName = ResolvePlanDisplayName(intent),
                    sector = intent.Sector.ToString(),
                    badgeSymbol = intent.BadgeSymbol,
                    tacticalRiskScore = intent.TacticalRiskScore,
                    selectionReason = intent.SelectionReason,
                    hasCaptureTarget = intent.HasCaptureTarget,
                    captureX = intent.CaptureTargetCell.x,
                    captureY = intent.CaptureTargetCell.y,
                    captureLabel = intent.CaptureTargetLabel
                });
            }

            foreach (var kv in state.unitAssignments)
            {
                AIPlanAssignment assignment = kv.Value;
                if (assignment == null || assignment.Intent == null)
                    continue;

                string planKey = AIPlanEvaluator.BuildPlanKey(assignment.Intent);
                if (string.IsNullOrWhiteSpace(planKey))
                    continue;

                teamBlock.assignments.Add(new SavedAssignment
                {
                    unitInstanceId = assignment.UnitInstanceId,
                    planKey = planKey,
                    role = (int)assignment.Role,
                    roleName = assignment.Role.ToString()
                });
            }

            foreach (var kv in state.previousAssignmentsByUnitId)
            {
                AIPlanEvaluator.MissionAssignmentMemory memory = kv.Value;
                if (string.IsNullOrWhiteSpace(memory.PlanKey))
                    continue;

                teamBlock.previousAssignments.Add(new SavedPreviousAssignmentMemory
                {
                    unitInstanceId = memory.UnitInstanceId,
                    planKey = memory.PlanKey,
                    role = (int)memory.Role,
                    roleName = memory.Role.ToString(),
                    lastProgressTurn = memory.LastProgressTurn,
                    lastDistanceToTarget = memory.LastDistanceToTarget
                });
            }

            data.teams.Add(teamBlock);
        }

        return data;
    }

    public void RestorePlannerSaveData(AIPlannerMultiTeamSaveData data)
    {
        plannerStateByTeam.Clear();

        if (data == null || data.teams == null || data.teams.Count == 0)
        {
            plannerDebugView.Clear();
            plannerDebugViewDirty = false;
            return;
        }

        int restoredAssignments = 0;
        int droppedAssignments = 0;
        int activeTeamIdAtLoad = matchController != null ? matchController.ActiveTeamId : int.MinValue;

        for (int i = 0; i < data.teams.Count; i++)
        {
            TeamPlannerSaveBlock teamBlock = data.teams[i];
            if (teamBlock == null)
                continue;
            if (!System.Enum.IsDefined(typeof(TeamId), teamBlock.teamId))
                continue;

            TeamId team = (TeamId)teamBlock.teamId;
            if (matchController != null && !matchController.IsPlayerAI(team))
                continue;

            TeamPlannerRuntimeState state = GetOrCreatePlannerState(team);
            state.currentTurnPlans.Clear();
            state.unitRoles.Clear();
            state.unitAssignments.Clear();
            state.previousAssignmentsByUnitId.Clear();
            state.currentPlannerLifecycleLogs.Clear();
            state.previousPlanMetricsByKey.Clear();
            state.planCatalogByKey.Clear();
            state.restoredTurn = teamBlock.restoredTurn >= 0 ? teamBlock.restoredTurn : (matchController != null ? matchController.CurrentTurn : 0);

            EnsureSectorCatalogPlans(state);
            EnsureInvasionCatalogPlans(state, team);
            if (teamBlock.catalogPlans != null)
            {
                for (int c = 0; c < teamBlock.catalogPlans.Count; c++)
                    TryApplySavedCatalogPlan(state, teamBlock.catalogPlans[c]);
            }

            Dictionary<string, AIPlanIntent> planByKey = new Dictionary<string, AIPlanIntent>(System.StringComparer.Ordinal);
            if (teamBlock.activePlans != null)
            {
                for (int p = 0; p < teamBlock.activePlans.Count; p++)
                {
                    SavedPlan savedPlan = teamBlock.activePlans[p];
                    if (savedPlan == null || string.IsNullOrWhiteSpace(savedPlan.planKey))
                        continue;

                    if (TryBuildIntentFromSavedPlan(savedPlan, out AIPlanIntent intent))
                    {
                        state.currentTurnPlans.Add(intent);
                        planByKey[savedPlan.planKey] = intent;
                    }
                    else
                    {
                        Debug.LogWarning($"[AI][planner-load][{team}] plano nao restaurado: {savedPlan.planKey}");
                    }
                }
            }

            if (teamBlock.assignments != null)
            {
                for (int a = 0; a < teamBlock.assignments.Count; a++)
                {
                    SavedAssignment saved = teamBlock.assignments[a];
                    if (saved == null || saved.unitInstanceId <= 0 || string.IsNullOrWhiteSpace(saved.planKey))
                        continue;

                    if (!planByKey.TryGetValue(saved.planKey, out AIPlanIntent intent) || intent == null)
                    {
                        droppedAssignments++;
                        Debug.LogWarning($"[AI][planner-load][{team}] assignment descartado (plano ausente): unit={saved.unitInstanceId} plan={saved.planKey}");
                        continue;
                    }

                    UnitManager unit = FindUnitById(saved.unitInstanceId);
                    if (unit == null || unit.IsDead || unit.TeamId != team)
                    {
                        droppedAssignments++;
                        Debug.LogWarning($"[AI][planner-load][{team}] assignment descartado (unidade invalida): unit={saved.unitInstanceId} plan={saved.planKey}");
                        continue;
                    }

                    AIPlanRole role = TryParseSavedRole(saved.role, saved.roleName);
                    AIPlanAssignment assignment = new AIPlanAssignment
                    {
                        UnitInstanceId = unit.InstanceId,
                        Role = role,
                        Intent = intent
                    };

                    if (role == AIPlanRole.Capture && intent.HasCaptureTarget)
                    {
                        assignment.HasPlannedCaptureTarget = true;
                        assignment.PlannedCaptureCell = intent.CaptureTargetCell;
                        assignment.PlannedCaptureLabel = intent.CaptureTargetLabel;
                    }

                    intent.Assignments.Add(assignment);
                    state.unitAssignments[unit.InstanceId] = assignment;
                    state.unitRoles[unit.InstanceId] = intent;
                    restoredAssignments++;
                }
            }

            if (teamBlock.previousAssignments != null)
            {
                for (int m = 0; m < teamBlock.previousAssignments.Count; m++)
                {
                    SavedPreviousAssignmentMemory savedMemory = teamBlock.previousAssignments[m];
                    if (savedMemory == null || savedMemory.unitInstanceId <= 0 || string.IsNullOrWhiteSpace(savedMemory.planKey))
                        continue;

                    AIPlanRole role = TryParseSavedRole(savedMemory.role, savedMemory.roleName);
                    state.previousAssignmentsByUnitId[savedMemory.unitInstanceId] = new AIPlanEvaluator.MissionAssignmentMemory
                    {
                        UnitInstanceId = savedMemory.unitInstanceId,
                        PlanKey = savedMemory.planKey,
                        Role = role,
                        LastProgressTurn = savedMemory.lastProgressTurn,
                        LastDistanceToTarget = savedMemory.lastDistanceToTarget
                    };
                }
            }

            state.hasRestoredStatePendingUse = teamBlock.hasRestoredStatePendingUse
                && state.currentTurnPlans.Count > 0
                && (int)team == activeTeamIdAtLoad;
            if (!teamBlock.hasRestoredStatePendingUse && state.currentTurnPlans.Count > 0)
                state.hasRestoredStatePendingUse = state.currentTurnPlans.Count > 0 && (int)team == activeTeamIdAtLoad;
            if (state.restoredTurn < 0 && matchController != null)
                state.restoredTurn = matchController.CurrentTurn;

            ApplyUnitPlanDebugBadges(team);
            if (state.currentTurnPlans.Count > 0 && !state.hasRestoredStatePendingUse)
            {
                Debug.Log($"[AI][planner-load][{team}] restore aguardando turno futuro; activeTeamAtLoad={(TeamId)activeTeamIdAtLoad} consumeOnNextEvaluate={state.hasRestoredStatePendingUse}");
            }
            Debug.Log($"[AI][planner-load][{team}] planos={state.currentTurnPlans.Count} assignments={state.unitAssignments.Count} previous={state.previousAssignmentsByUnitId.Count} pendingUse={state.hasRestoredStatePendingUse}");
        }

        MarkPlannerDebugViewDirty();
        RefreshPlannerDebugViewIfDirty();
        Debug.Log($"[AI][planner-load] assignments restaurados={restoredAssignments} descartados={droppedAssignments}");
    }

    private bool TryBuildIntentFromSavedPlan(SavedPlan savedPlan, out AIPlanIntent intent)
    {
        intent = null;
        if (savedPlan == null || string.IsNullOrWhiteSpace(savedPlan.planKey))
            return false;

        if (TryParseDynamicCapturePlanKey(savedPlan.planKey, out ConstructionSector dynamicSector))
        {
            intent = new AIPlanIntent
            {
                Sector = dynamicSector,
                DisplayName = !string.IsNullOrWhiteSpace(savedPlan.displayName) ? savedPlan.displayName : $"Captura {dynamicSector}",
                BadgeSymbol = !string.IsNullOrWhiteSpace(savedPlan.badgeSymbol) ? savedPlan.badgeSymbol : GetSectorBadgeSymbol(dynamicSector),
                TacticalRiskScore = Mathf.Max(0, savedPlan.tacticalRiskScore),
                SelectionReason = savedPlan.selectionReason
            };
        }
        else if (System.Enum.TryParse(savedPlan.sector, true, out ConstructionSector sectorFromSave))
        {
            intent = new AIPlanIntent
            {
                Sector = sectorFromSave,
                DisplayName = !string.IsNullOrWhiteSpace(savedPlan.displayName) ? savedPlan.displayName : sectorFromSave.ToString(),
                BadgeSymbol = !string.IsNullOrWhiteSpace(savedPlan.badgeSymbol) ? savedPlan.badgeSymbol : GetSectorBadgeSymbol(sectorFromSave),
                TacticalRiskScore = Mathf.Max(0, savedPlan.tacticalRiskScore),
                SelectionReason = savedPlan.selectionReason
            };
        }

        if (intent == null)
            return false;

        intent.HasCaptureTarget = savedPlan.hasCaptureTarget;
        intent.CaptureTargetCell = new Vector3Int(savedPlan.captureX, savedPlan.captureY, 0);
        intent.CaptureTargetLabel = savedPlan.captureLabel;
        return true;
    }

    private static AIPlanRole TryParseSavedRole(int roleValue, string roleName)
    {
        if (System.Enum.IsDefined(typeof(AIPlanRole), roleValue))
            return (AIPlanRole)roleValue;

        if (!string.IsNullOrWhiteSpace(roleName) && System.Enum.TryParse(roleName, true, out AIPlanRole parsed))
            return parsed;

        return AIPlanRole.Assault;
    }
}
