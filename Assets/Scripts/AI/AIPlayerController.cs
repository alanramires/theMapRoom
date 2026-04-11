using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Coordena o turno da IA em 4 fases sequenciais:
//   1. Servico do Comando  - reabastece/repara unidades
//   2. Mover Unidades      - executa movimento das unidades amigas
//   3. Comprar Unidades    - compra unidades nas fabricas proprias
//   4. Passar a Vez        - encerra o turno
public partial class AIPlayerController : MonoBehaviour
{
    [System.Serializable]
    private struct TeamProfileAssignment
    {
        public TeamId team;
        public AIGeneralProfile profile;
    }

    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private bool aiLog = true;
    [Header("AI Debug")]
    [SerializeField, Tooltip("Mostra no UnitHUD uma letra/simbolo do plano atual da unidade (debug).")]
    private bool showPlanDebugAtUnit = false;
    [SerializeField, Tooltip("Setor destacado nos logs de restore/planner.")]
    private ConstructionSector plannerDebugFocusSector = ConstructionSector.Alpha;
    [SerializeField, Tooltip("Apos o load, forca Build()+EvaluatePlanner sem consumir estado restaurado para o time configurado.")]
    private bool debugRunPostLoadPlannerRebuildTest = false;
    [SerializeField] private TeamId debugPostLoadPlannerTeam = TeamId.Blue;
    [SerializeField, Tooltip("Time usado pelo botao de simulacao manual do planner no inspector.")]
    private TeamId plannerDebugSimulationTeam = TeamId.Green;

    [Header("AI Data")]
    [SerializeField, Tooltip("Catalogo oficial de perfis de IA.")]
    private AIDatabase aiDatabase;
    [SerializeField, Tooltip("Bindings auto-gerados: time IA -> AI Profile.")]
    private List<TeamProfileAssignment> teamProfileAssignments = new List<TeamProfileAssignment>();
    [SerializeField, Tooltip("Banco de posturas de batalha. Define comportamento modificador por stance (ataque, defesa, invasao).")]
    private BattleStanceDatabase battleStanceDatabase;

    private BeginnerAIProfile fallbackBehaviorProfile;
    private AIStance currentStance = AIStance.Attack;
    private Coroutine activeTurnRoutine;
    private AIData runtimeAiDataFallback;

    [System.Serializable]
    public sealed class AssignmentDebugView
    {
        public int unitInstanceId;
        public string unitName;
        public string role;
    }

    [System.Serializable]
    public sealed class PlanDebugView
    {
        public string planKey;
        public string displayName;
        public string sector;
        public string status;
        public bool conquered;
        public bool sectorClear;
        public string controllingTeam;
        public int progressCurrent;
        public int progressMax;
        public int lastActivationTurn;
        public int lastCompletionTurn;
        public int tacticalRiskScore;
        public string selectionReason;
        public List<AssignmentDebugView> assignments = new List<AssignmentDebugView>();
    }

    [System.Serializable]
    public sealed class ShoppingOrderDebugView
    {
        public string orderId;
        public string planLabel;
        public string capability;
        public int remainingCount;
        public int priorityScore;
        public bool critical;
        public string reason;
    }

    [System.Serializable]
    public sealed class ShoppingCapabilityPressureDebugView
    {
        public string capability;
        public int orderCount;
        public int basePressure;
        public int missingPressure;
        public int riskPressure;
        public int criticalPressure;
        public int dynamicPressure;
        public int totalPressure;
        public string criteriaSummary;
        public bool urgentLogisticsNeed;
        public int supplyChainPressureScore;
        public int supplyChainAvailableSuppliers;
        public int supplyChainTargetSuppliers;
        public int supplyChainAdditionalSuppliersNeeded;
        public int supplyChainPlansMissingLogistics;
        public int supplyChainLowAutonomyUnits;
        public int supplyChainOutOfAmmoUnits;
        public int supplyChainDamagedUnits;
        public int supplyChainFrontlineCriticalUnits;
        public string supplyChainReason;
    }

    [System.Serializable]
    public sealed class ShoppingDecisionDebugView
    {
        public string constructionLabel;
        public string kind;
        public string plannedUnitId;
        public int targetIndex;
        public int cost;
        public bool usedSavingFallback;
        public string planKey;
        public string capability;
        public string reason;
    }

    [System.Serializable]
    public sealed class TeamPlannerDebugView
    {
        public TeamId team;
        public string currentStanceName;
        public List<PlanDebugView> plans = new List<PlanDebugView>();
    }

    [System.Serializable]
    public sealed class TeamShoppingDebugView
    {
        public TeamId team;
        public string currentStanceName;
        public int turnNumber;
        public int totalMoney;
        public int reservedMoney;
        public int freeMoney;
        public int saveTargetMoney;
        public int strategicReserveMoney;
        public bool massFloorBlocked;
        public int massFloorCurrentUnits;
        public int massFloorRequiredUnits;
        public string massFloorReason;
        public bool hasCriticalCaptureGap;
        public bool strategicSaveActive;
        public string strategicSaveUnitId;
        public string strategicSaveSourceLabel;
        public int strategicSaveCost;
        public int strategicSaveTurnsToAfford;
        public int strategicSaveDeferredOrders;
        public string strategicSaveReason;
        public List<ShoppingCapabilityPressureDebugView> capabilityPressures = new List<ShoppingCapabilityPressureDebugView>();
        public List<ShoppingOrderDebugView> orders = new List<ShoppingOrderDebugView>();
        public List<ShoppingDecisionDebugView> decisions = new List<ShoppingDecisionDebugView>();
    }

    [SerializeField, Tooltip("Visao consolidada dos planos por time IA (debug). Atualizada sob demanda.")]
    private List<TeamPlannerDebugView> plannerDebugView = new List<TeamPlannerDebugView>();
    [SerializeField, Tooltip("Ultimo plano central de compras por time IA (debug).")]
    private List<TeamShoppingDebugView> shoppingDebugView = new List<TeamShoppingDebugView>();

    private readonly Dictionary<TeamId, TeamPlannerRuntimeState> plannerStateByTeam = new Dictionary<TeamId, TeamPlannerRuntimeState>();
    private TeamId plannerContextTeam = TeamId.Neutral;
    private bool plannerDebugViewDirty;

    private List<AIPlanIntent> currentTurnPlans => GetOrCreatePlannerState(plannerContextTeam).currentTurnPlans;
    private Dictionary<int, AIPlanIntent> currentTurnUnitRoles => GetOrCreatePlannerState(plannerContextTeam).unitRoles;
    private Dictionary<int, AIPlanAssignment> currentTurnUnitAssignments => GetOrCreatePlannerState(plannerContextTeam).unitAssignments;
    private Dictionary<int, AIPlanEvaluator.MissionAssignmentMemory> previousAssignmentsByUnitId => GetOrCreatePlannerState(plannerContextTeam).previousAssignmentsByUnitId;
    private List<string> currentPlannerLifecycleLogs => GetOrCreatePlannerState(plannerContextTeam).currentPlannerLifecycleLogs;
    private Dictionary<string, PlanMetricState> previousPlanMetricsByKey => GetOrCreatePlannerState(plannerContextTeam).previousPlanMetricsByKey;

    private struct PlanMetricState
    {
        public int OutstandingCaptures;
        public int VisibleThreats;
    }

    public AIStance CurrentStance => currentStance;
    public IReadOnlyList<AIPlanIntent> CurrentTurnPlans => GetOrCreatePlannerState(GetDebugReferenceTeam()).currentTurnPlans;
    public IReadOnlyDictionary<int, AIPlanIntent> CurrentTurnUnitRoles => GetOrCreatePlannerState(GetDebugReferenceTeam()).unitRoles;
    public IReadOnlyList<TeamPlannerDebugView> PlannerDebugView => plannerDebugView;
    public IReadOnlyList<TeamShoppingDebugView> ShoppingDebugView => shoppingDebugView;

    public IReadOnlyList<AIPlanIntent> GetCurrentTurnPlansForDebugTeam(TeamId team)
    {
        return GetOrCreatePlannerState(team).currentTurnPlans;
    }


    public void SimulatePlannerGenerationForDebugSelectedTeam()
    {
        SimulatePlannerGenerationForDebug(plannerDebugSimulationTeam);
    }

    public void SimulatePlannerGenerationForDebugAllAiTeams()
    {
        EnsureAIDataDefaults();
        SectorManager.RequestRebuildFromActiveConstructions("planner-debug-sim-all");

        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (matchController == null)
        {
            Debug.LogWarning("[AI][planner-debug] simulacao abortada: MatchController nao encontrado.");
            return;
        }

        IReadOnlyList<TeamId> players = matchController.Players;
        bool simulatedAny = false;
        for (int i = 0; i < players.Count; i++)
        {
            TeamId team = players[i];
            if (!matchController.IsPlayerAI(team))
                continue;

            if (SimulatePlannerGenerationForDebug(team))
                simulatedAny = true;
        }

        if (!simulatedAny)
            Debug.LogWarning("[AI][planner-debug] simulacao abortada: nenhum time IA disponivel para simular.");
    }

    public bool SimulatePlannerGenerationForDebug(TeamId aiTeam)
    {
        EnsureAIDataDefaults();
        SectorManager.RequestRebuildFromActiveConstructions($"planner-debug-sim-{aiTeam}");

        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (matchController == null)
        {
            Debug.LogWarning($"[AI][planner-debug][{aiTeam}] simulacao abortada: MatchController nao encontrado.");
            return false;
        }


        if (!matchController.IsPlayerAI(aiTeam))
        {
            Debug.LogWarning($"[AI][planner-debug][{aiTeam}] simulacao abortada: time nao esta configurado como IA no MatchController.");
            return false;
        }

        AISnapshot snapshot = BuildSnapshotForTeam(aiTeam);
        EvaluatePlanner(aiTeam, snapshot, allowRestoredStateConsume: false);
        RefreshPlannerDebugViewNow();
        Debug.Log($"[AI][planner-debug][{aiTeam}] simulacao concluida | amigos={snapshot.FriendlyUnits.Count} inimigosVisiveis={snapshot.VisibleEnemies.Count} planos={GetOrCreatePlannerState(aiTeam).currentTurnPlans.Count}");
        return true;
    }

    private string T(TeamId team, int fase) =>
        $"[AI][T{(matchController != null ? matchController.CurrentTurn : 0)}][{team}][Fase {fase}]";

    private static string GetStanceLabel(AIStance stance)
    {
        switch (stance)
        {
            case AIStance.Defend:
                return "DEFESA";
            case AIStance.Invasion:
                return "INVASAO";
            default:
                return "ATAQUE";
        }
    }

    private static string GetStanceLabelLower(AIStance stance)
    {
        switch (stance)
        {
            case AIStance.Defend:
                return "defesa";
            case AIStance.Invasion:
                return "invasao";
            default:
                return "ataque";
        }
    }
    private bool IsEnemyVisibleForAiTeam(UnitManager enemy, TeamId aiTeam)
    {
        if (enemy == null)
            return false;
        if (matchController == null)
            return true;
        if (enemy.IsDead || enemy.IsEmbarked)
            return false;

        Tilemap boardTilemap = enemy.BoardTilemap;
        if (boardTilemap == null)
        {
            IReadOnlyList<UnitManager> units = UnitManager.AllActive;
            for (int i = 0; i < units.Count; i++)
            {
                UnitManager unit = units[i];
                if (unit != null && unit.BoardTilemap != null)
                {
                    boardTilemap = unit.BoardTilemap;
                    break;
                }
            }
        }

        if (boardTilemap == null)
            return true;

        bool enforceStealthValidation = matchController.EnableStealthValidation
            && !enemy.HasFiredThisTurn;
        return PodeDetectarSensor.IsTargetObservedByTeam(
            enemy,
            (int)aiTeam,
            boardTilemap,
            matchController.TerrainDatabaseRef,
            null,
            matchController.EnableLosValidation,
            matchController.EnableSpotter,
            enforceStealthValidation);
    }

    private static string FormatCellLC(Vector3Int cell)
    {
        cell.z = 0;
        return $"L{cell.y}, C{cell.x}";
    }
    private static bool IsProtectIntent(AIPlanIntent intent)
    {
        if (intent == null || string.IsNullOrWhiteSpace(intent.SelectionReason))
            return false;

        return intent.SelectionReason.IndexOf("sector-hold", System.StringComparison.OrdinalIgnoreCase) >= 0
            || intent.SelectionReason.IndexOf("backup-retake", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void Awake()
    {
        EnsureAIDataDefaults();
        fallbackBehaviorProfile = ScriptableObject.CreateInstance<BeginnerAIProfile>();
    }

    private void OnEnable()
    {
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
        MatchController.OnSlotConfigChanged += HandleSlotConfigChanged;
        MatchController.OnTeamDefeated += HandleTeamDefeated;
        TurnStateManager.OnUnitDestroyed += HandleUnitDestroyedPlannerCleanup;
        SaveGameManager.OnAfterLoadSuccess += HandleAfterLoadSuccess;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureAIDataDefaults();
    }
#endif

    private void EnsureAIDataDefaults()
    {
        RefreshAiTeamProfileAssignments();

        for (int i = 0; i < teamProfileAssignments.Count; i++)
        {
            AIGeneralProfile p = teamProfileAssignments[i].profile;
            if (p != null && p.aiData != null)
                p.aiData.EnsureDefaults();
        }
    }

    private void HandleSlotConfigChanged()
    {
        RefreshAiTeamProfileAssignments();
    }

    private void RefreshAiTeamProfileAssignments()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        teamProfileAssignments.Clear();

        if (matchController == null || aiDatabase == null || aiDatabase.profiles == null)
            return;

        HashSet<AIGeneralProfile> used = new HashSet<AIGeneralProfile>();
        IReadOnlyList<TeamId> players = matchController.Players;
        if (players == null)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            TeamId team = players[i];
            if (!matchController.IsPlayerAI(team))
                continue;

            AIGeneralProfile resolved = SelectBestProfileForTeam(team, used);
            TeamProfileAssignment assignment = new TeamProfileAssignment
            {
                team = team,
                profile = resolved
            };
            teamProfileAssignments.Add(assignment);

            if (resolved != null)
                used.Add(resolved);
        }
    }

    private AIGeneralProfile SelectBestProfileForTeam(TeamId team, HashSet<AIGeneralProfile> used)
    {
        if (aiDatabase == null || aiDatabase.profiles == null)
            return null;

        for (int i = 0; i < aiDatabase.profiles.Count; i++)
        {
            AIGeneralProfile p = aiDatabase.profiles[i];
            if (p == null || p.aiData == null)
                continue;
            if (p.preferedTeamAssignment != team)
                continue;
            if (used.Contains(p))
                continue;
            return p;
        }

        for (int i = 0; i < aiDatabase.profiles.Count; i++)
        {
            AIGeneralProfile p = aiDatabase.profiles[i];
            if (p == null || p.aiData == null)
                continue;
            if (p.preferedTeamAssignment != TeamId.Neutral)
                continue;
            if (used.Contains(p))
                continue;
            return p;
        }

        for (int i = 0; i < aiDatabase.profiles.Count; i++)
        {
            AIGeneralProfile p = aiDatabase.profiles[i];
            if (p == null || p.aiData == null)
                continue;
            if (p.preferedTeamAssignment == team)
                return p;
        }

        for (int i = 0; i < aiDatabase.profiles.Count; i++)
        {
            AIGeneralProfile p = aiDatabase.profiles[i];
            if (p == null || p.aiData == null)
                continue;
            if (p.preferedTeamAssignment == TeamId.Neutral)
                return p;
        }

        return null;
    }

    private AIGeneralProfile GetAssignedProfileForTeam(TeamId team)
    {
        for (int i = 0; i < teamProfileAssignments.Count; i++)
        {
            TeamProfileAssignment a = teamProfileAssignments[i];
            if (a.team == team)
                return a.profile;
        }
        return null;
    }

    private AIProfile GetBehaviorProfile(TeamId team)
    {
        AIGeneralProfile general = GetAssignedProfileForTeam(team);
        if (general != null && general.behaviorProfile != null)
            return general.behaviorProfile;
        return fallbackBehaviorProfile;
    }

    public bool TryGetAssignedAIDataForTeam(TeamId team, out AIData data, out AIGeneralProfile profileForTeam)
    {
        RefreshAiTeamProfileAssignments();
        profileForTeam = GetAssignedProfileForTeam(team);
        data = profileForTeam != null ? profileForTeam.aiData : null;
        return data != null;
    }

    private AIData GetEffectiveAIData(TeamId aiTeam)
    {
        RefreshAiTeamProfileAssignments();

        AIGeneralProfile assigned = GetAssignedProfileForTeam(aiTeam);
        if (assigned != null && aiDatabase != null)
        {
            AIData fromDb = aiDatabase.ResolveFor(currentStance, assigned);
            if (fromDb != null)
            {
                fromDb.EnsureDefaults();
                return fromDb;
            }
        }

        if (assigned != null && assigned.aiData != null)
        {
            assigned.aiData.EnsureDefaults();
            return assigned.aiData;
        }

        if (runtimeAiDataFallback == null)
            runtimeAiDataFallback = AIData.CreateRuntimeBasic();
        else
            runtimeAiDataFallback.EnsureDefaults();

        return runtimeAiDataFallback;
    }
    private void OnDisable()
    {
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
        MatchController.OnSlotConfigChanged -= HandleSlotConfigChanged;
        MatchController.OnTeamDefeated -= HandleTeamDefeated;
        TurnStateManager.OnUnitDestroyed -= HandleUnitDestroyedPlannerCleanup;
        SaveGameManager.OnAfterLoadSuccess -= HandleAfterLoadSuccess;
        if (activeTurnRoutine != null)
            StopCoroutine(activeTurnRoutine);
    }

    private void HandleAfterLoadSuccess()
    {
        if (!Application.isPlaying || !debugRunPostLoadPlannerRebuildTest)
            return;

        StartCoroutine(DebugRunPostLoadPlannerRebuildTestNextFrame());
    }

    private IEnumerator DebugRunPostLoadPlannerRebuildTestNextFrame()
    {
        yield return null;
        DebugForceRebuildSnapshotAndReevaluate(debugPostLoadPlannerTeam);
    }
    private void HandleActiveTeamChanged(int teamId)
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (activeTurnRoutine != null)
        {
            StopCoroutine(activeTurnRoutine);
            activeTurnRoutine = null;
        }

        if (matchController == null)
            return;

        if (!matchController.IsActiveTeamAI())
            return;

        if (SaveGameManager.IsAnyLoadInProgress)
        {
            if (aiLog)
                Debug.Log($"[AI] HandleActiveTeamChanged ignorado durante load (teamId={teamId}).");
            return;
        }

        TeamId aiTeam = (TeamId)teamId;

        activeTurnRoutine = StartCoroutine(ExecuteAITurn(aiTeam));
    }


        private IEnumerator ExecuteAITurn(TeamId aiTeam)
    {
        SetPlannerContextTeam(aiTeam);
        float delay = turnStateManager != null
            ? turnStateManager.GetAutomatedPhaseDelay()
            : (AnimationManager.Instance != null ? AnimationManager.Instance.AIPhaseDuration : 0.5f);

        yield return StartCoroutine(WaitIfPlayerMenuOpen());
        yield return null;
        AISnapshot snapshot = TakeSnapshot(aiTeam, suppressLog: true, refreshStance: true);
        LogUnitPlanBindingsAtTurnStart(aiTeam, snapshot, "pre-evaluate");
        LogPlannerSectorOwnershipSnapshot(aiTeam, snapshot, "pre-evaluate");
        EvaluatePlanner(aiTeam, snapshot);
        LogUnitPlanBindingsAtTurnStart(aiTeam, snapshot, "post-evaluate");

        snapshot.ActivePlans.Clear();
        snapshot.ActivePlans.AddRange(currentTurnPlans);
        snapshot.UnitRoles.Clear();
        foreach (var kv in currentTurnUnitRoles)
            snapshot.UnitRoles[kv.Key] = kv.Value;
        snapshot.UnitPlanAssignments.Clear();
        foreach (var kv in currentTurnUnitAssignments)
            snapshot.UnitPlanAssignments[kv.Key] = kv.Value;
        LogSnapshot(aiTeam, snapshot);

        yield return StartCoroutine(WaitIfPlayerMenuOpen());
        yield return StartCoroutine(Phase1_CommandService(aiTeam, snapshot));
        yield return StartCoroutine(WaitIfPlayerMenuOpen());
        yield return new WaitForSeconds(delay);

        List<int> unitInstanceIds = CollectFriendlyUnitIds(snapshot);
        // Ordena por initiative efetiva (Priority->Retreat), desempate por HP decrescente (mais saudavel age primeiro).
        unitInstanceIds.Sort((a, b) =>
        {
            UnitManager ua = FindUnitById(a);
            UnitManager ub = FindUnitById(b);
            AIInitiative ia = GetEffectiveInitiative(ua);
            AIInitiative ib = GetEffectiveInitiative(ub);
            int cmp = ((int)ia).CompareTo((int)ib);
            if (cmp != 0) return cmp;
            int hpA = ua != null ? ua.CurrentHP : 0;
            int hpB = ub != null ? ub.CurrentHP : 0;
            return hpB.CompareTo(hpA); // decrescente: maior HP age primeiro
        });
        for (int i = 0; i < unitInstanceIds.Count; i++)
        {
            yield return StartCoroutine(WaitIfPlayerMenuOpen());
            yield return StartCoroutine(turnStateManager.WaitUntilAutomatedNeutralReady(3f));
            yield return StartCoroutine(WaitIfPlayerMenuOpen());

            snapshot = TakeSnapshot(aiTeam, refreshStance: false);

            UnitManager unit = FindUnitById(unitInstanceIds[i]);
            if (unit == null || unit.IsDead)
                continue;

            UnitManager assignedEnemy = AssignTargetForUnit(unit, snapshot, unitInstanceIds, i);

            yield return StartCoroutine(WaitIfPlayerMenuOpen());
            yield return StartCoroutine(Phase2_MoveUnit(aiTeam, snapshot, unit, assignedEnemy));
            yield return StartCoroutine(WaitIfPlayerMenuOpen());
            yield return new WaitForSeconds(delay);
        }

        yield return StartCoroutine(WaitIfPlayerMenuOpen());
        snapshot = TakeSnapshot(aiTeam, refreshStance: false);
        yield return StartCoroutine(WaitIfPlayerMenuOpen());
        yield return StartCoroutine(Phase3_BuyUnits(aiTeam, snapshot));
        yield return StartCoroutine(WaitIfPlayerMenuOpen());
        yield return new WaitForSeconds(delay);

        yield return StartCoroutine(WaitIfPlayerMenuOpen());
        Phase4_EndTurn(aiTeam);
    }

    private AISnapshot TakeSnapshot(TeamId aiTeam, bool suppressLog = false, bool refreshStance = true)
    {
        SetPlannerContextTeam(aiTeam);
        TeamPlannerRuntimeState state = GetOrCreatePlannerState(aiTeam);

        AISnapshot snapshot = BuildSnapshotForTeam(aiTeam);
        if (refreshStance)
        {
            AIStance previousStance = state.currentStance;
            AIStance evaluatedStance = GetBehaviorProfile(aiTeam).EvaluateStance(snapshot, battleStanceDatabase);
            state.currentStance = evaluatedStance;
            currentStance = evaluatedStance;
            if (evaluatedStance != previousStance)
            {
                ApplyUnitPlanDebugBadges(aiTeam);
                MarkPlannerDebugViewDirty();
            }
        }
        else
        {
            currentStance = state.currentStance;
        }

        snapshot.ActivePlans.Clear();
        snapshot.ActivePlans.AddRange(currentTurnPlans);
        snapshot.UnitRoles.Clear();
        foreach (var kv in currentTurnUnitRoles)
            snapshot.UnitRoles[kv.Key] = kv.Value;
        snapshot.UnitPlanAssignments.Clear();
        foreach (var kv in currentTurnUnitAssignments)
            snapshot.UnitPlanAssignments[kv.Key] = kv.Value;

        if (!suppressLog)
            LogSnapshot(aiTeam, snapshot);
        return snapshot;
    }



    private void LogUnitPlanBindingsAtTurnStart(TeamId aiTeam, AISnapshot snapshot, string stage)
    {
        if (!aiLog)
            return;

        TeamPlannerRuntimeState state = GetOrCreatePlannerState(aiTeam);
        var lines = new List<string>();

        for (int i = 0; i < UnitManager.AllActive.Count; i++)
        {
            UnitManager unit = UnitManager.AllActive[i];
            if (unit == null || unit.IsDead || unit.TeamId != aiTeam)
                continue;

            bool hasRuntime = state.unitAssignments.TryGetValue(unit.InstanceId, out AIPlanAssignment runtimeAsg) && runtimeAsg != null && runtimeAsg.Intent != null;
            string runtimePlan = hasRuntime ? AIPlanEvaluator.BuildPlanKey(runtimeAsg.Intent) : "(none)";
            string runtimeSector = hasRuntime ? runtimeAsg.Intent.Sector.ToString() : "(none)";
            string runtimeRole = hasRuntime ? runtimeAsg.Role.ToDebugLabel() : "(none)";

            AIPlanAssignment snapAsg = null;
            bool hasSnapshot = snapshot != null && snapshot.UnitPlanAssignments != null
                && snapshot.UnitPlanAssignments.TryGetValue(unit.InstanceId, out snapAsg)
                && snapAsg != null && snapAsg.Intent != null;
            string snapshotPlan = hasSnapshot ? AIPlanEvaluator.BuildPlanKey(snapAsg.Intent) : "(none)";
            string snapshotSector = hasSnapshot ? snapAsg.Intent.Sector.ToString() : "(none)";

            lines.Add($"unit={unit.name} id={unit.InstanceId} debugPlan={unit.AIAssignedPlanKey}/{unit.AIAssignedPlanRole.ToDebugLabel()} runtime={runtimePlan}/{runtimeSector}/{runtimeRole} snapshot={snapshotPlan}/{snapshotSector}");
        }

        lines.Sort(System.StringComparer.Ordinal);
        Debug.Log($"{T(aiTeam, 0)} [planner-bindings][{stage}]\n{string.Join("\n", lines)}");
    }
    private void LogPlannerSectorOwnershipSnapshot(TeamId aiTeam, AISnapshot snapshot, string stage)
    {
        if (!aiLog || snapshot == null || snapshot.KnownConstructions == null)
            return;

        var rows = new List<string>();
        var focusedRows = new List<string>();
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || !info.IsCapturable)
                continue;

            string label = !string.IsNullOrWhiteSpace(info.DisplayName) ? info.DisplayName : info.Sector.ToString();
            bool aiOwnsFully = info.TeamId == aiTeam && info.CapturePoints >= info.CapturePointsMax;
            string row = $"{label}|sector={info.Sector}|team={info.TeamId}|cp={info.CapturePoints}/{info.CapturePointsMax}|aiOwnsFully={aiOwnsFully}";
            rows.Add(row);
            if (info.Sector == plannerDebugFocusSector)
                focusedRows.Add(row);
        }

        rows.Sort(System.StringComparer.Ordinal);
        Debug.Log($"{T(aiTeam, 0)} [planner][{stage}] snapshot-constructions: {string.Join(" ; ", rows)}");

        if (focusedRows.Count > 0)
        {
            focusedRows.Sort(System.StringComparer.Ordinal);
            Debug.Log($"{T(aiTeam, 0)} [planner][{stage}][focus-sector={plannerDebugFocusSector}] {string.Join(" ; ", focusedRows)}");
        }
    }

    public void DebugLogRestoredPlannerSnapshots(string stage)
    {
        if (!aiLog || matchController == null)
            return;

        foreach (var kv in plannerStateByTeam)
        {
            TeamPlannerRuntimeState state = kv.Value;
            if (state == null || state.currentTurnPlans.Count == 0)
                continue;

            AISnapshot snapshot = TakeSnapshotForDebug(kv.Key);
            LogPlannerSectorOwnershipSnapshot(kv.Key, snapshot, stage);
        }
    }

    public void DebugForceRebuildSnapshotAndReevaluate(TeamId aiTeam)
    {
        SetPlannerContextTeam(aiTeam);
        TeamPlannerRuntimeState state = GetOrCreatePlannerState(aiTeam);
        AISnapshot snapshot = TakeSnapshotForDebug(aiTeam);
        Debug.Log($"{T(aiTeam, 0)} [planner-debug] forced post-load rebuild begin pendingUse={state.hasRestoredStatePendingUse} restoredPlans={state.currentTurnPlans.Count}");
        LogPlannerSectorOwnershipSnapshot(aiTeam, snapshot, "forced-rebuild-pre");
        EvaluatePlanner(aiTeam, snapshot, allowRestoredStateConsume: false);
        LogPlannerSectorOwnershipSnapshot(aiTeam, snapshot, "forced-rebuild-post");
    }

    private AISnapshot TakeSnapshotForDebug(TeamId aiTeam)
    {
        SetPlannerContextTeam(aiTeam);
        return BuildSnapshotForTeam(aiTeam);
    }

    private void DumpPlannerAssignmentsForTeam(TeamId aiTeam, string stage, AISnapshot snapshot)
    {
        if (!aiLog)
            return;

        TeamPlannerRuntimeState state = GetOrCreatePlannerState(aiTeam);
        var lines = new List<string>();
        for (int i = 0; i < state.currentTurnPlans.Count; i++)
        {
            AIPlanIntent intent = state.currentTurnPlans[i];
            if (intent == null)
                continue;

            string key = AIPlanEvaluator.BuildPlanKey(intent);
            string display = ResolvePlanDisplayName(intent);
            string capture = intent.HasCaptureTarget
                ? $"{intent.CaptureTargetLabel}({intent.CaptureTargetCell.x},{intent.CaptureTargetCell.y})"
                : "none";

            lines.Add($"plan[{i}] key={key} sector={intent.Sector} capture={capture} assignments={intent.Assignments.Count} display={display}");
            for (int a = 0; a < intent.Assignments.Count; a++)
            {
                AIPlanAssignment asg = intent.Assignments[a];
                if (asg == null)
                    continue;
                UnitManager u = FindUnitById(asg.UnitInstanceId);
                string unitName = u != null ? u.name : $"#{asg.UnitInstanceId}";
                lines.Add($"  - unit={unitName} id={asg.UnitInstanceId} role={asg.Role.ToDebugLabel()} plannedCell={(asg.HasPlannedCaptureTarget ? asg.PlannedCaptureCell.ToString() : "none")}");
            }
        }

        Debug.Log($"{T(aiTeam, 0)} [planner-dump][{stage}] plans={state.currentTurnPlans.Count} unitRoles={state.unitRoles.Count} unitAssignments={state.unitAssignments.Count}\n{string.Join("\n", lines)}");
    }
    private bool HasAnyValidRestoredPlan(AISnapshot snapshot, TeamId aiTeam, List<AIPlanIntent> plans, string stage)
    {
        if (plans == null || plans.Count == 0)
            return false;

        bool anyValid = false;
        for (int i = 0; i < plans.Count; i++)
        {
            AIPlanIntent intent = plans[i];
            bool valid = IsRestoredPlanValidForCurrentSnapshot(snapshot, aiTeam, intent, stage);
            anyValid |= valid;
        }

        return anyValid;
    }

    private bool IsRestoredPlanValidForCurrentSnapshot(AISnapshot snapshot, TeamId aiTeam, AIPlanIntent intent, string stage)
    {
        if (intent == null)
        {
            if (aiLog)
                Debug.Log($"{T(aiTeam, 0)} [planner-restore-validate][{stage}] plan=(null) result=false");
            return false;
        }

        string planKey = AIPlanEvaluator.BuildPlanKey(intent);
        string targetLabel = intent.HasCaptureTarget
            ? $"{intent.CaptureTargetLabel}({intent.CaptureTargetCell.x},{intent.CaptureTargetCell.y})"
            : "none";

        if (!intent.HasCaptureTarget || snapshot == null || snapshot.KnownConstructions == null)
        {
            if (aiLog)
                Debug.Log($"{T(aiTeam, 0)} [planner-restore-validate][{stage}] plan={planKey} sector={intent.Sector} target={targetLabel} result=true reason=no-capture-target-or-snapshot");
            return true;
        }

        Vector3Int target = intent.CaptureTargetCell;
        target.z = 0;

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || !info.IsCapturable)
                continue;

            Vector3Int cell = info.Cell;
            cell.z = 0;
            if (cell != target)
                continue;

            bool aiOwnsFully = info.TeamId == aiTeam && info.CapturePoints >= info.CapturePointsMax;
            bool result = !aiOwnsFully;
            if (aiLog)
            {
                string label = !string.IsNullOrWhiteSpace(info.DisplayName) ? info.DisplayName : info.Sector.ToString();
                Debug.Log($"{T(aiTeam, 0)} [planner-restore-validate][{stage}] plan={planKey} sector={intent.Sector} target={targetLabel} matched={label} team={info.TeamId} cp={info.CapturePoints}/{info.CapturePointsMax} aiOwnsFully={aiOwnsFully} result={result}");
            }
            return result;
        }

        if (aiLog)
            Debug.Log($"{T(aiTeam, 0)} [planner-restore-validate][{stage}] plan={planKey} sector={intent.Sector} target={targetLabel} result=true reason=target-not-found-in-snapshot");
        return true;
    }

    private void EvaluatePlanner(TeamId aiTeam, AISnapshot snapshot, bool allowRestoredStateConsume = true)
    {
        SetPlannerContextTeam(aiTeam);
        TeamPlannerRuntimeState state = GetOrCreatePlannerState(aiTeam);
        LogPlannerSectorOwnershipSnapshot(aiTeam, snapshot, "evaluate-entry");
        if (aiLog)
            Debug.Log($"{T(aiTeam, 0)} [planner] evaluate-entry pendingUse={state.hasRestoredStatePendingUse} restoredPlans={state.currentTurnPlans.Count} allowRestoredStateConsume={allowRestoredStateConsume}");

        if (state.currentTurnPlans.Count > 0 && !state.hasRestoredStatePendingUse && state.restoredTurn >= 0)
        {
            Debug.Log($"{T(aiTeam, 0)} [planner] restored-plans-present-but-not-pending | plans={state.currentTurnPlans.Count} restoredTurn={state.restoredTurn} activeTeam={(matchController != null ? matchController.ActiveTeam.ToString() : "(null)")}");
        }

        if (allowRestoredStateConsume && state.hasRestoredStatePendingUse && state.currentTurnPlans.Count > 0)
        {
            state.hasRestoredStatePendingUse = false;
            bool hasValidRestoredPlan = HasAnyValidRestoredPlan(snapshot, aiTeam, state.currentTurnPlans, "consume");
            currentPlannerLifecycleLogs.Clear();

            if (hasValidRestoredPlan)
            {
                currentPlannerLifecycleLogs.Add("planner-load-restore-consumido | reutilizando planos restaurados no time ativo do load");
                ApplyUnitPlanDebugBadges(aiTeam);
                MarkPlannerDebugViewDirty();
                RefreshPlannerDebugViewIfDirty();
                Debug.Log($"{T(aiTeam, 0)} [planner] estado restaurado consumido no time ativo do load; reavaliacao adiada.");
                DumpPlannerAssignmentsForTeam(aiTeam, "consume-restored", snapshot);
                return;
            }

            currentPlannerLifecycleLogs.Add("planner-load-restore-invalidado | planos restaurados sem alvo valido no snapshot atual; reavaliando");
        }

        currentTurnPlans.Clear();
        currentTurnUnitRoles.Clear();
        currentTurnUnitAssignments.Clear();
        currentPlannerLifecycleLogs.Clear();

        AIPlanEvaluator.PlannerRuntimeConfig plannerConfig = BuildPlannerRuntimeConfig(aiTeam);
        List<AIPlanEvaluator.MissionAssignmentMemory> previous = new List<AIPlanEvaluator.MissionAssignmentMemory>(previousAssignmentsByUnitId.Values);

        List<AIPlanIntent> plans = AIPlanEvaluator.Evaluate(
            snapshot,
            previous,
            plannerConfig,
            currentPlannerLifecycleLogs);

        currentTurnPlans.AddRange(plans);

        for (int p = 0; p < plans.Count; p++)
        {
            AIPlanIntent intent = plans[p];
            for (int a = 0; a < intent.Assignments.Count; a++)
            {
                AIPlanAssignment assignment = intent.Assignments[a];
                currentTurnUnitRoles[assignment.UnitInstanceId] = intent;
                currentTurnUnitAssignments[assignment.UnitInstanceId] = assignment;
            }
        }

        ApplySavedPlanFallbackAssignments(aiTeam, snapshot, previous);

        UpdateMissionPersistenceMemory(snapshot, plannerConfig.CurrentTurn);
        ApplyUnitPlanDebugBadges(aiTeam);

        for (int i = 0; i < currentPlannerLifecycleLogs.Count; i++)
            Debug.Log($"{T(aiTeam, 0)} [planner] {currentPlannerLifecycleLogs[i]}");

        DumpPlannerAssignmentsForTeam(aiTeam, "post-evaluate", snapshot);

        MarkPlannerDebugViewDirty();
        RefreshPlannerDebugViewIfDirty();
    }

    private void ApplySavedPlanFallbackAssignments(
        TeamId aiTeam,
        AISnapshot snapshot,
        IReadOnlyCollection<AIPlanEvaluator.MissionAssignmentMemory> previousAssignments)
    {
        if (snapshot == null || previousAssignments == null || previousAssignments.Count == 0)
            return;

        var plannerConfig = BuildPlannerRuntimeConfig(aiTeam);
        int maxVariablePlans = plannerConfig.MaxVariablePlans + plannerConfig.MaxBackupPlans;
        var planByKey = new Dictionary<string, AIPlanIntent>();
        for (int i = 0; i < currentTurnPlans.Count; i++)
        {
            AIPlanIntent intent = currentTurnPlans[i];
            string key = AIPlanEvaluator.BuildPlanKey(intent);
            if (!string.IsNullOrWhiteSpace(key))
                planByKey[key] = intent;
        }

        foreach (AIPlanEvaluator.MissionAssignmentMemory memory in previousAssignments)
        {
            UnitManager unit = FindUnitById(memory.UnitInstanceId);
            if (unit == null || unit.IsDead || unit.TeamId != aiTeam)
                continue;
            if (string.IsNullOrWhiteSpace(memory.PlanKey))
                continue;
            if (!TryParseDynamicCapturePlanKey(memory.PlanKey, out ConstructionSector sector))
                continue;

            if (currentTurnUnitAssignments.TryGetValue(unit.InstanceId, out AIPlanAssignment existing)
                && existing != null
                && existing.Intent != null
                && string.Equals(AIPlanEvaluator.BuildPlanKey(existing.Intent), memory.PlanKey, System.StringComparison.Ordinal))
            {
                continue;
            }

            if (currentTurnUnitAssignments.TryGetValue(unit.InstanceId, out AIPlanAssignment oldAssignment)
                && oldAssignment != null)
            {
                oldAssignment.Intent?.Assignments.Remove(oldAssignment);
                currentTurnUnitAssignments.Remove(unit.InstanceId);
                currentTurnUnitRoles.Remove(unit.InstanceId);
            }

            if (planByKey.TryGetValue(memory.PlanKey, out AIPlanIntent existingPlan))
            {
                RestoreUnitAssignmentToPlan(snapshot, unit, memory.Role, existingPlan, memory.PlanKey, "fallback-plano-load");
                continue;
            }

            if (TryAttachRestoredUnitToActivePlan(snapshot, unit, memory.Role, memory.PlanKey))
                continue;

            if (IsSectorCompletedAndSafeForTeam(snapshot, aiTeam, sector))
            {
                currentPlannerLifecycleLogs.Add($"fallback-plano-load | liberado {unit.name} de {memory.PlanKey} [{memory.Role.ToDebugLabel()}] setor-ja-seguro");
                continue;
            }

            int dynamicPlanCount = CountDynamicCapturePlans(currentTurnPlans);
            if (memory.Role != AIPlanRole.Capture || dynamicPlanCount >= maxVariablePlans)
            {
                currentPlannerLifecycleLogs.Add($"fallback-plano-load | liberado {unit.name} de {memory.PlanKey} [{memory.Role.ToDebugLabel()}] sem slot de plano/sem reaproveitamento");
                continue;
            }

            AIPlanIntent fallbackIntent = BuildFallbackDynamicIntent(snapshot, sector, unit);
            if (fallbackIntent == null)
            {
                currentPlannerLifecycleLogs.Add($"fallback-plano-load | ignorado {unit.name} de {memory.PlanKey} sem alvo capturavel valido");
                continue;
            }

            currentTurnPlans.Add(fallbackIntent);
            planByKey[memory.PlanKey] = fallbackIntent;
            currentPlannerLifecycleLogs.Add($"fallback-plano-load | criou {memory.PlanKey} para preservar atribuicoes salvas");
            RestoreUnitAssignmentToPlan(snapshot, unit, memory.Role, fallbackIntent, memory.PlanKey, "fallback-plano-load");
        }
    }

    private bool TryAttachRestoredUnitToActivePlan(
        AISnapshot snapshot,
        UnitManager unit,
        AIPlanRole role,
        string previousPlanKey)
    {
        AIPlanIntent bestPlan = null;
        int bestScore = int.MaxValue;

        for (int i = 0; i < currentTurnPlans.Count; i++)
        {
            AIPlanIntent intent = currentTurnPlans[i];
            if (intent == null || !intent.HasCaptureTarget)
                continue;

            int participants = intent.Assignments != null ? intent.Assignments.Count : 0;
            int distance = GetHexDistance(unit.CurrentCellPosition, intent.CaptureTargetCell);
            int score = distance + (participants * 100);
            if (participants < 2)
                score -= 1000;
            if (score < bestScore)
            {
                bestScore = score;
                bestPlan = intent;
            }
        }

        if (bestPlan == null)
            return false;

        RestoreUnitAssignmentToPlan(snapshot, unit, role, bestPlan, previousPlanKey, "fallback-realocado");
        return true;
    }

    private void RestoreUnitAssignmentToPlan(
        AISnapshot snapshot,
        UnitManager unit,
        AIPlanRole role,
        AIPlanIntent targetPlan,
        string sourcePlanKey,
        string eventTag)
    {
        if (snapshot == null || unit == null || targetPlan == null)
            return;

        AIPlanAssignment restoredAssignment = new AIPlanAssignment
        {
            UnitInstanceId = unit.InstanceId,
            Role = role,
            Intent = targetPlan
        };

        TryResolveFallbackCaptureTarget(snapshot, targetPlan, restoredAssignment);
        targetPlan.Assignments.Add(restoredAssignment);
        currentTurnUnitRoles[unit.InstanceId] = targetPlan;
        currentTurnUnitAssignments[unit.InstanceId] = restoredAssignment;
        currentPlannerLifecycleLogs.Add($"{eventTag} | {unit.name} {sourcePlanKey} -> {AIPlanEvaluator.BuildPlanKey(targetPlan)} [{role.ToDebugLabel()}]");
    }

    private static bool IsSectorCompletedAndSafeForTeam(AISnapshot snapshot, TeamId aiTeam, ConstructionSector sector)
    {
        if (snapshot == null)
            return false;

        if (!SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo sectorInfo)
            && !SectorManager.TryGetBaseInfo(sector, out sectorInfo))
            return false;

        if (sectorInfo == null)
            return false;

        if (!sectorInfo.IsFullyControlled || sectorInfo.ControllingTeam != aiTeam)
            return false;

        if (sectorInfo.IsDisputed || sectorInfo.HasPartialCapture)
            return false;

        return IsSectorClearForTeam(snapshot, sectorInfo.RepresentativeCell, 2);
    }

    private static int CountDynamicCapturePlans(List<AIPlanIntent> plans)
    {
        if (plans == null)
            return 0;

        int count = 0;
        for (int i = 0; i < plans.Count; i++)
        {
            AIPlanIntent intent = plans[i];
            if (intent == null)
                continue;
            if (TryParseDynamicCapturePlanKey(AIPlanEvaluator.BuildPlanKey(intent), out _))
                count++;
        }

        return count;
    }
    private static bool TryParseDynamicCapturePlanKey(string planKey, out ConstructionSector sector)
    {
        sector = ConstructionSector.Base1;
        if (string.IsNullOrWhiteSpace(planKey))
            return false;

        const string prefix = "dynamic:capture:";
        if (!planKey.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            return false;

        string rawSector = planKey.Substring(prefix.Length);
        if (string.IsNullOrWhiteSpace(rawSector))
            return false;

        return System.Enum.TryParse(rawSector, true, out sector);
    }

    private static AIPlanIntent BuildFallbackDynamicIntent(AISnapshot snapshot, ConstructionSector sector, UnitManager referenceUnit)
    {
        AIPlanIntent intent = new AIPlanIntent
        {
            Sector = sector,
            DisplayName = $"Captura {sector} [fallback-load]",
            BadgeSymbol = GetSectorBadgeSymbol(sector)
        };

        AIConstructionInfo best = null;
        int bestDist = int.MaxValue;
        Vector3Int unitCell = referenceUnit != null ? referenceUnit.CurrentCellPosition : Vector3Int.zero;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || !info.IsCapturable || info.Sector != sector)
                continue;

            bool ownedByAi = info.TeamId == snapshot.AiTeam;
            if (ownedByAi)
                continue;

            int dist = Mathf.Abs(unitCell.x - info.Cell.x) + Mathf.Abs(unitCell.y - info.Cell.y);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = info;
            }
        }

        if (best == null)
            return null;

        intent.HasCaptureTarget = true;
        intent.CaptureTargetCell = best.Cell;
        intent.CaptureTargetLabel = !string.IsNullOrWhiteSpace(best.DisplayName)
            ? best.DisplayName
            : sector.ToString();

        return intent;
    }

    private static string GetSectorBadgeSymbol(ConstructionSector sector)
    {
        string raw = sector.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        return raw.Substring(0, 1).ToUpperInvariant();
    }

    private void ApplyUnitPlanDebugBadges(TeamId aiTeam)
    {
        SetPlannerContextTeam(aiTeam);
        for (int i = 0; i < UnitManager.AllActive.Count; i++)
        {
            UnitManager unit = UnitManager.AllActive[i];
            if (unit == null || unit.TeamId != aiTeam)
                continue;

            unit.ClearAIAssignedPlan();
            BattleStanceData stanceData = battleStanceDatabase != null ? battleStanceDatabase.GetStanceData(currentStance) : null;
            unit.SetAIStance(currentStance, stanceData?.stanceIcon, showPlanDebugAtUnit);
        }

        foreach (var kv in currentTurnUnitAssignments)
        {
            AIPlanAssignment assignment = kv.Value;
            if (assignment == null)
                continue;

            UnitManager unit = FindUnitById(assignment.UnitInstanceId);
            if (unit == null || unit.IsDead || unit.TeamId != aiTeam)
                continue;

            string badge = assignment.Intent != null ? (assignment.Intent.BadgeSymbol ?? string.Empty) : string.Empty;
            string planKey = AIPlanEvaluator.BuildPlanKey(assignment.Intent);
            string planName = ResolvePlanDisplayName(assignment.Intent);
            unit.SetAIAssignedPlan(planKey, planName, badge, assignment.Role, showPlanDebugAtUnit);
        }

        MarkPlannerDebugViewDirty();
    }

    private static string ResolvePlanDisplayName(AIPlanIntent intent)
    {
        if (intent == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(intent.DisplayName))
            return intent.DisplayName;

        return intent.Sector.ToString();
    }

    private static UnitHudController FindOwnedUnitHud(UnitManager unit)
    {
        if (unit == null)
            return null;

        UnitHudController[] huds = unit.GetComponentsInChildren<UnitHudController>(true);
        for (int i = 0; i < huds.Length; i++)
        {
            UnitHudController hud = huds[i];
            if (hud == null)
                continue;

            if (hud.ResolveOwnerUnit() == unit)
                return hud;
        }

        return null;
    }


    private AIPlanEvaluator.PlannerRuntimeConfig BuildPlannerRuntimeConfig(TeamId aiTeam)
    {
        const int defaultStagnationTurns = 2;
        const int defaultMaxVariablePlans = 3;

        AIGeneralProfile profileForTeam = GetAssignedProfileForTeam(aiTeam);

        int stagnationTurns = defaultStagnationTurns;
        int minimumRangeForDefensePlan = AIGeneralProfile.DefaultMinimumRangeForDefensePlan;
        int maxVariablePlans = defaultMaxVariablePlans;
        int maxBackupPlans = 0;

        if (profileForTeam != null)
        {
            stagnationTurns = Mathf.Max(0, profileForTeam.stagnationTurns);
            minimumRangeForDefensePlan = Mathf.Max(1, profileForTeam.minimumRangeForDefensePlan);
            maxVariablePlans = Mathf.Max(0, profileForTeam.maxVariablePlans);
            maxBackupPlans = Mathf.Max(0, profileForTeam.maxBackupPlans);
        }

        return new AIPlanEvaluator.PlannerRuntimeConfig
        {
            StagnationTurns = stagnationTurns,
            MinimumRangeForDefensePlan = minimumRangeForDefensePlan,
            MaxVariablePlans = maxVariablePlans,
            MaxBackupPlans = maxBackupPlans,
            CurrentStance = currentStance,
            CurrentTurn = matchController != null ? matchController.CurrentTurn : 0
        };
    }

    private AISnapshot BuildSnapshotForTeam(TeamId aiTeam)
    {
        AIGeneralProfile profileForTeam = GetAssignedProfileForTeam(aiTeam);
        int defendRadius = profileForTeam != null
            ? Mathf.Max(1, profileForTeam.minimumRangeForDefensePlan)
            : AISnapshot.DefaultDefendRadius;
        return AISnapshot.Build(aiTeam, matchController, defendRadius);
    }

    private void UpdateMissionPersistenceMemory(AISnapshot snapshot, int currentTurn)
    {
        if (snapshot != null)
            SetPlannerContextTeam(snapshot.AiTeam);
        var nextAssignments = new Dictionary<int, AIPlanEvaluator.MissionAssignmentMemory>();
        var nextPlanMetrics = new Dictionary<string, PlanMetricState>();
        var friendlyById = new Dictionary<int, UnitManager>();
        if (snapshot != null && snapshot.FriendlyUnits != null)
        {
            for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
            {
                UnitManager friendly = snapshot.FriendlyUnits[i];
                if (friendly == null || friendly.IsDead)
                    continue;
                friendlyById[friendly.InstanceId] = friendly;
            }
        }
        IReadOnlyList<UnitManager> allUnits = UnitManager.AllActive;
        for (int i = 0; allUnits != null && i < allUnits.Count; i++)
        {
            UnitManager friendly = allUnits[i];
            if (friendly == null || friendly.IsDead || friendly.TeamId != plannerContextTeam)
                continue;
            friendlyById[friendly.InstanceId] = friendly;
        }


        for (int p = 0; p < currentTurnPlans.Count; p++)
        {
            AIPlanIntent intent = currentTurnPlans[p];
            if (intent == null)
                continue;

            string planKey = AIPlanEvaluator.BuildPlanKey(intent);
            bool planProgress = DetectPlanProgress(snapshot, intent, planKey, out int currentOutstanding, out int currentThreats);
            nextPlanMetrics[planKey] = new PlanMetricState
            {
                OutstandingCaptures = currentOutstanding,
                VisibleThreats = currentThreats
            };

            for (int a = 0; a < intent.Assignments.Count; a++)
            {
                AIPlanAssignment assignment = intent.Assignments[a];
                if (assignment == null)
                    continue;

                UnitManager unit = null;
                if (!friendlyById.TryGetValue(assignment.UnitInstanceId, out unit))
                    unit = FindUnitById(assignment.UnitInstanceId);
                if (unit == null || unit.IsDead)
                    continue;

                int distance = GetDistanceToPlanTarget(unit, intent, snapshot);
                int lastProgressTurn = currentTurn;
                if (previousAssignmentsByUnitId.TryGetValue(unit.InstanceId, out AIPlanEvaluator.MissionAssignmentMemory previous)
                    && string.Equals(previous.PlanKey, planKey, System.StringComparison.Ordinal))
                {
                    bool distanceReduced = distance < previous.LastDistanceToTarget;
                    bool progressed = distanceReduced || planProgress;
                    lastProgressTurn = progressed ? currentTurn : previous.LastProgressTurn;
                }

                nextAssignments[unit.InstanceId] = new AIPlanEvaluator.MissionAssignmentMemory
                {
                    UnitInstanceId = unit.InstanceId,
                    PlanKey = planKey,
                    Role = assignment.Role,
                    LastProgressTurn = lastProgressTurn,
                    LastDistanceToTarget = distance
                };
            }
        }

        previousAssignmentsByUnitId.Clear();
        foreach (var kv in nextAssignments)
            previousAssignmentsByUnitId[kv.Key] = kv.Value;

        previousPlanMetricsByKey.Clear();
        foreach (var kv in nextPlanMetrics)
            previousPlanMetricsByKey[kv.Key] = kv.Value;
    }

    private bool DetectPlanProgress(AISnapshot snapshot, AIPlanIntent intent, string planKey, out int currentOutstanding, out int currentThreats)
    {
        currentOutstanding = CountOutstandingCaptureTargets(snapshot, intent.Sector);
        currentThreats = CountVisibleEnemiesNearSector(snapshot, intent.Sector);

        if (!previousPlanMetricsByKey.TryGetValue(planKey, out PlanMetricState previousMetric))
            return false;

        if (currentOutstanding < previousMetric.OutstandingCaptures)
            return true;

        if (currentThreats < previousMetric.VisibleThreats)
            return true;

        return false;
    }

    private static bool IsSectorClearForTeam(AISnapshot snapshot, Vector3Int representativeCell, int radius)
    {
        if (snapshot == null)
            return true;

        representativeCell.z = 0;
        int safeRadius = Mathf.Max(0, radius);
        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            if (GetHexDistance(representativeCell, enemyCell) <= safeRadius)
                return false;
        }

        return true;
    }

    private static int CountVisibleEnemiesNearSector(AISnapshot snapshot, ConstructionSector sector)
    {
        if (snapshot == null)
            return 0;

        if (ConstructionSectorHelper.IsBase(sector))
        {
            if (!snapshot.HasHq)
                return 0;

            int aroundHq = 0;
            for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
            {
                UnitManager enemy = snapshot.VisibleEnemies[i];
                if (enemy == null || enemy.IsDead)
                    continue;

                int dist = GetHexDistance(snapshot.HqCell, enemy.CurrentCellPosition);
                if (dist <= snapshot.HqDefendRadius)
                    aroundHq++;
            }

            return aroundHq;
        }

        Vector2 centroid = Vector2.zero;
        int centroidCount = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Sector != sector)
                continue;

            centroid += new Vector2(info.Cell.x, info.Cell.y);
            centroidCount++;
        }

        if (centroidCount <= 0)
            return 0;

        centroid /= centroidCount;

        int threats = 0;
        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            Vector3Int ec = enemy.CurrentCellPosition;
            float sqrDist = (new Vector2(ec.x, ec.y) - centroid).sqrMagnitude;
            if (sqrDist <= 36f)
                threats++;
        }

        return threats;
    }

    private static int GetDistanceToPlanTarget(UnitManager unit, AIPlanIntent intent, AISnapshot snapshot)
    {
        if (unit == null || intent == null)
            return int.MaxValue;

        Vector3Int from = unit.CurrentCellPosition;
        from.z = 0;

        Vector3Int target;
        if (intent.HasCaptureTarget)
            target = intent.CaptureTargetCell;
        else if (snapshot != null && snapshot.HasHq)
            target = snapshot.HqCell;
        else
            target = from;

        target.z = 0;
        return GetHexDistance(from, target);
    }

    private static int GetHexDistance(Vector3Int a, Vector3Int b)
    {
        a.z = 0;
        b.z = 0;
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static List<int> CollectFriendlyUnitIds(AISnapshot snapshot)
    {
        List<int> ids = new List<int>(snapshot.FriendlyUnits.Count);
        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager u = snapshot.FriendlyUnits[i];
            if (u != null && !u.IsDead && !u.HasActed)
                ids.Add(u.InstanceId);
        }
        return ids;
    }

    private static UnitManager FindUnitById(int instanceId)
    {
        for (int i = 0; i < UnitManager.AllActive.Count; i++)
        {
            UnitManager u = UnitManager.AllActive[i];
            if (u != null && u.InstanceId == instanceId)
                return u;
        }
        return null;
    }


    private IEnumerator Phase1_CommandService(TeamId aiTeam, AISnapshot snapshot)
    {
        if (turnStateManager == null)
        {
            if (aiLog) Debug.Log($"{T(aiTeam, 1)} TurnStateManager ausente, pulando Servico do Comando");
            yield break;
        }

        yield return StartCoroutine(turnStateManager.WaitUntilAutomatedNeutralReady(2f));

        bool opened = turnStateManager.HandleAutomatedSensorActionRequested(SensorActionType.CommandService);
        if (!opened)
        {
            if (aiLog) Debug.Log($"{T(aiTeam, 1)} Servico do Comando: sem ordens validas neste turno");
            yield break;
        }

        if (aiLog) Debug.Log($"{T(aiTeam, 1)} Servico do Comando: confirmar execucao (equiv. X + Enter)");
        turnStateManager.HandleConfirmWithFeedback();

        float confirmDelay = turnStateManager != null ? turnStateManager.GetAutomatedConfirmDelay() : (AnimationManager.Instance != null ? AnimationManager.Instance.AIUnitSelectDelay : 0.12f);
        if (confirmDelay > 0f)
            yield return new WaitForSeconds(confirmDelay);

        yield return StartCoroutine(turnStateManager.WaitUntilAutomatedNeutralReady(12f));

        if (aiLog) Debug.Log($"{T(aiTeam, 1)} Servico do Comando: concluido e voltou para Neutral");
    }
    private static bool TryGetPreferredEngagementRangeForTarget(UnitManager attacker, UnitManager target, out int minRange, out int maxRange)
    {
        minRange = 1;
        maxRange = 1;
        if (attacker == null || target == null)
            return false;

        Domain targetDomain = target.GetDomain();
        HeightLevel targetHeight = target.GetHeightLevel();
        IReadOnlyList<UnitEmbarkedWeapon> weapons = attacker.GetEmbarkedWeapons();
        if (weapons == null || weapons.Count == 0)
            return false;

        int resolvedMin = int.MaxValue;
        int resolvedMax = int.MinValue;
        bool found = false;

        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;
            if (embarked.squadAmmunition <= 0)
                continue;
            if (!embarked.weapon.SupportsOperationOn(targetDomain, targetHeight))
                continue;
            if (!PodeMirarSensor.TryResolveWeaponRangeCandidate(
                    embarked,
                    SensorMovementMode.MoveuAndando,
                    requireAmmo: true,
                    out int wMin,
                    out int wMax))
                continue;

            found = true;
            if (wMin < resolvedMin)
                resolvedMin = wMin;
            if (wMax > resolvedMax)
                resolvedMax = wMax;
        }

        if (!found)
            return false;

        minRange = Mathf.Max(1, resolvedMin);
        maxRange = Mathf.Max(minRange, resolvedMax);
        return true;
    }
    private static bool TryGetPreferredArtilleryRange(UnitManager unit, out int minRange, out int maxRange)
    {
        minRange = 1;
        maxRange = 1;
        if (unit == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null || weapons.Count == 0)
            return false;

        Domain domain = unit.GetDomain();
        HeightLevel height = unit.GetHeightLevel();
        int bestMax = int.MinValue;
        bool found = false;

        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon weapon = weapons[i];
            if (weapon == null)
                continue;
            if (weapon.squadAmmunition <= 0)
                continue;
            if (!weapon.CanFireAtLayer(domain, height))
                continue;

            int wMin = weapon.GetRangeMin();
            int wMax = weapon.GetRangeMax();
            if (wMin <= 1)
                continue;

            if (!found || wMax > bestMax)
            {
                found = true;
                bestMax = wMax;
                minRange = wMin;
                maxRange = wMax;
            }
        }

        return found;
    }

}













