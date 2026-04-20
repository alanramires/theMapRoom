using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class TurnStateManager : MonoBehaviour
{
    public static event Action OnSensorsReady;
    public static event Action<UnitManager> OnUnitPurchased;
    public static event Action<UnitManager> OnUnitInspected;
    public static event Action<ConstructionManager> OnConstructionInspected;
    public static event Action<UnitManager, UnitManager> OnAttackResolved;
    public static event Action<UnitManager> OnUnitRevealedFromFog;
    public static event Action<UnitManager> OnUnitDestroyed;
    public static event Action<UnitManager> OnUnitMovementExecuted;
    public static event Action<UnitManager> OnUnitSelected;
    public static event Action<UnitManager, UnitManager> OnUnitEmbarked;
    public static event Action<UnitManager, UnitManager> OnUnitDisembarked;
    public static event Action<UnitManager, UnitManager> OnUnitSupplied;

    public static void NotifyUnitRevealedFromFog(UnitManager unit)
    {
        OnUnitRevealedFromFog?.Invoke(unit);
    }

    public static void NotifyUnitSupplied(UnitManager supplier, UnitManager target)
    {
        OnUnitSupplied?.Invoke(supplier, target);
    }

    public enum ActionSfx
    {
        None = 0,
        Confirm = 1,
        Cancel = 2,
        Error = 3
    }

    public enum CursorState
    {
        Neutral = 0,
        UnitSelected = 1,
        MoveuAndando = 2,
        MoveuParado = 3,
        Capturando = 4,
        Mirando = 5,
        Pousando = 6,
        Embarcando = 7,
        Desembarcando = 8,
        Fundindo = 9,
        ShoppingAndServices = 10,
        Suprindo = 11,
        InspectingUnit = 12,
        InspectingBuilding = 13,
        InspectingHotZone = 14,
        CommandService = 15,
        RemovingUnit = 16,
        Planning = 17,
        PlayerMenu = 18,
        AircraftFuelDepletionQueue = 19,
        TurnStartRallyQueue = 20,
        Replay = 21,
        CommandServiceExecuting = 22,
        RemovingUnitExecuting = 23,
        EndingTurn = 24,
        EndingTurnExecuting = 25,
        Saving = 26,
        Loading = 27,
        CapturandoExecuting = 28,
        SuprindoExecuting = 29,
        FundindoExecuting = 30,
        DesembarcandoExecuting = 31,
        EmbarcandoExecuting = 32
    }

    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private PathManager pathManager;
    [SerializeField] private UnitSpawner unitSpawner;
    [SerializeField] private ReplayManager replayManager;
    [SerializeField] private PlanningManager planningManager;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private WeaponPriorityData weaponPriorityData;
    [SerializeField] private DPQMatchupDatabase dpqMatchupDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private RPSDatabase rpsDatabase;
    [SerializeField] private Tilemap rangeMapTilemap;
    [SerializeField] private Tilemap lineOfFireMapTilemap;
    [SerializeField] private Tilemap terrainTilemap;
    [SerializeField] private TileBase rangeOverlayTile;
    [SerializeField] private TileBase lineOfFireOverlayTile;
    [Header("Combat Audio")]
    // Audio de combate agora e centralizado no CursorController.

    [Header("State")]
    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] [Range(0.05f, 1f)] private float movementRangeAlpha = 0.6f;
    [SerializeField] [Range(0.05f, 1f)] private float lineOfFireAlpha = 0.45f;
    [Header("Turn Transition")]
    [SerializeField] [Range(0f, 2f)] private float advanceTurnPreDelay = 0.1f;
    [SerializeField] [Range(0f, 2f)] private float advanceTurnPostDelay = 0f;
    [Header("FSM Debug UI")]
    [SerializeField] private UIDocument fsmDebugDocument;
    [SerializeField] private string fsmDebugElementName = "FSM";

    private readonly List<Vector3Int> paintedRangeCells = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> paintedRangeLookup = new HashSet<Vector3Int>();
    private readonly List<Vector3Int> paintedLineOfFireCells = new List<Vector3Int>();
    private readonly HashSet<Vector3Int> paintedLineOfFireLookup = new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, List<Vector3Int>> movementPathsByCell = new Dictionary<Vector3Int, List<Vector3Int>>();
    private AircraftOperationDecision cachedAircraftOperationDecision;
    private readonly List<Vector3Int> committedMovementPath = new List<Vector3Int>();
    private Vector3Int committedOriginCell;
    private Vector3Int committedDestinationCell;
    private bool hasCommittedMovement;
    private Domain committedMovementLayerBeforeDomain = Domain.Land;
    private HeightLevel committedMovementLayerBeforeHeight = HeightLevel.Surface;
    private bool hasCommittedMovementLayerBefore;
    private int preparedFuelCost;
    private bool hasPreparedFuelCost;
    private int preparedMovementCost;
    private bool hasPreparedMovementCost;
    private bool hasTemporaryTakeoffSelectionState;
    private UnitManager temporaryTakeoffUnit;
    private Domain temporaryTakeoffOriginalDomain = Domain.Land;
    private HeightLevel temporaryTakeoffOriginalHeight = HeightLevel.Surface;
    private bool temporaryTakeoffOriginalGrounded = true;
    private bool temporaryTakeoffOriginalEmbarkedInCarrier;
    private readonly List<int> temporaryTakeoffMoveOptions = new List<int>();
    private bool hasAutoPromotionEntryLayer;
    private UnitManager autoPromotionUnit;
    private Domain autoPromotionEntryDomain = Domain.Land;
    private HeightLevel autoPromotionEntryHeight = HeightLevel.Surface;
    private bool hasForcedLayerRollbackSnapshot;
    private Domain forcedLayerRollbackDomain = Domain.Land;
    private HeightLevel forcedLayerRollbackHeight = HeightLevel.Surface;
    private ConstructionManager shoppingConstruction;
    private readonly List<UnitData> shoppingUnitsForSale = new List<UnitData>();
    private int shoppingSelectedIndex = -1;
    private bool captureExecutionInProgress;
    private bool removeUnitExecutionInProgress;
    private readonly List<UnitManager> turnStartFuelDepletionDeathQueue = new List<UnitManager>();
    private bool turnStartFuelDepletionExecutionInProgress;
    private bool turnStartRallyExecutionInProgress;
    private readonly List<PlayerActionSubStep> turnStartFuelDepletionReplaySubSteps = new List<PlayerActionSubStep>();
    private readonly HashSet<int> debugTempMoveUnitInstanceIds = new HashSet<int>();
    private readonly Stack<CursorState> stateStack = new Stack<CursorState>();
    private TextElement fsmDebugText;

    public CursorState CurrentCursorState => stateStack.Count > 0 ? stateStack.Peek() : CursorState.Neutral;
    public string CurrentCursorStateStackDebugText => BuildFsmDebugText(horizontal: true);
    public UnitManager SelectedUnit => selectedUnit;
    public TerrainDatabase TerrainDatabaseRef => terrainDatabase;
    public WeaponPriorityData WeaponPriorityDataRef => weaponPriorityData;
    public DPQMatchupDatabase DpqMatchupDatabaseRef => dpqMatchupDatabase;
    public RPSDatabase RpsDatabaseRef => rpsDatabase;
    public DPQAirHeightConfig DpqAirHeightConfigRef => dpqAirHeightConfig;
    public float AdvanceTurnPreDelay => Mathf.Max(0f, advanceTurnPreDelay);
    public float AdvanceTurnPostDelay => Mathf.Max(0f, advanceTurnPostDelay);
    public bool IsScannerActionExecutionInProgress =>
        embarkExecutionInProgress
        || landingExecutionInProgress
        || combatExecutionInProgress
        || captureExecutionInProgress
        || removeUnitExecutionInProgress
        || mergeExecutionInProgress
        || supplyExecutionInProgress
        || transferExecutionInProgress
        || disembarkExecutionInProgress
        || turnStartFuelDepletionExecutionInProgress
        || turnStartRallyExecutionInProgress;
    public bool IsTurnStartFuelDepletionExecutionInProgress =>
        turnStartFuelDepletionExecutionInProgress
        || CurrentCursorState == CursorState.AircraftFuelDepletionQueue;
    public bool IsTurnStartRallyExecutionInProgress =>
        turnStartRallyExecutionInProgress
        || CurrentCursorState == CursorState.TurnStartRallyQueue;

    private void LogStateStep(string step, bool rollback = false)
    {
        if (!enableTurnStateRuntimeLogs)
            return;

        string rollbackTag = rollback ? " [roll back]" : string.Empty;
        string selectedName = selectedUnit != null ? selectedUnit.name : "(none)";
        Debug.Log($"[TurnState]{rollbackTag} state={CurrentCursorState} | step={step} | selected={selectedName}");
    }

    private void RuntimeLog(string message)
    {
        if (!enableTurnStateRuntimeLogs || string.IsNullOrWhiteSpace(message))
            return;

        Debug.Log(message);
    }

    private void PushPanelUnitMessage(string text, float durationSeconds = 2.8f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (CurrentCursorState == CursorState.Suprindo && supplyExecutionInProgress)
            return;
        string normalized = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        const int maxLen = 64;
        if (normalized.Length > maxLen)
            normalized = normalized.Substring(0, maxLen - 1).TrimEnd() + "...";

        PanelDialogController.TrySetTransientText(normalized, durationSeconds);
    }

    private void ApplyStateTransition(CursorState previous, CursorState nextState, string reason, bool rollback = false)
    {
        bool shouldClearThreatOverlayOnTransition =
            nextState != CursorState.Neutral &&
            nextState != CursorState.InspectingHotZone;
        if (shouldClearThreatOverlayOnTransition)
        {
            if (scannerPromptStep == ScannerPromptStep.ThreatLayerTeamSelect)
                scannerPromptStep = ScannerPromptStep.AwaitingAction;
            ClearEnemyThreatLayersOverlay();
        }

        if (nextState == CursorState.Neutral && previous != CursorState.Neutral)
        {
            if (enableTurnStateRuntimeLogs)
                Debug.Log($"[Replay][Dispatch] OnCursorReturnedToNeutral fired previous={previous} current={nextState}");
            CursorController.NotifyCursorReturnedToNeutral();
        }
        RuntimeLog($"[FSM] Estado: {previous} -> {nextState}");

        if (!enableTurnStateRuntimeLogs)
            return;

        string rollbackTag = rollback ? " [roll back]" : string.Empty;
        string selectedName = selectedUnit != null ? selectedUnit.name : "(none)";
        Debug.Log($"[TurnState]{rollbackTag} transition={previous} -> {nextState} | reason={reason} | selected={selectedName} | stack={FormatStateStack()}");
    }

    private void Advance(CursorState nextState, string reason)
    {
        EnsureStateStackInitialized();
        CursorState previous = CurrentCursorState;
        stateStack.Push(nextState);
        ApplyStateTransition(previous, nextState, reason, rollback: false);
        HandleStateAdvanced(previous, nextState, reason);
        ValidateStateStack(reason);
        RefreshFsmDebugText();
    }

    private void Retreat(string reason)
    {
        EnsureStateStackInitialized();
        CursorState previous = CurrentCursorState;
        ValidateRetreatSource(previous, reason);
        if (stateStack.Count > 1)
            stateStack.Pop();
        CursorState revealed = CurrentCursorState;
        ApplyStateTransition(previous, revealed, reason, rollback: true);
        HandleStateRevealedByRetreat(previous, revealed, reason);
        ValidateStateStack(reason);
        RefreshFsmDebugText();
    }

    private void ExecuteAndReset(string reason)
    {
        CursorState previous = CurrentCursorState;
        stateStack.Clear();
        stateStack.Push(CursorState.Neutral);
        ApplyStateTransition(previous, CursorState.Neutral, reason, rollback: false);
        HandleStateStackReset(previous, reason);
        ValidateStateStack(reason);
        RefreshFsmDebugText();
    }

    private void EnsureStateStackInitialized()
    {
        if (stateStack.Count == 0)
            stateStack.Push(CursorState.Neutral);
    }

    private string FormatStateStack()
    {
        if (stateStack.Count == 0)
            return "(empty)";

        CursorState[] states = stateStack.ToArray();
        StringBuilder sb = new StringBuilder();
        for (int i = states.Length - 1; i >= 0; i--)
        {
            if (sb.Length > 0)
                sb.Append(" > ");
            sb.Append(states[i]);
        }

        return sb.ToString();
    }

    private void BindFsmDebugText()
    {
        fsmDebugText = null;
        if (string.IsNullOrWhiteSpace(fsmDebugElementName))
            return;

        if (TryBindFsmDebugText(fsmDebugDocument))
            return;

        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsInactive.Include);
        foreach (UIDocument document in documents)
        {
            if (TryBindFsmDebugText(document))
                return;
        }
    }

    private bool TryBindFsmDebugText(UIDocument document)
    {
        if (document == null || document.rootVisualElement == null)
            return false;

        fsmDebugText = document.rootVisualElement.Q<TextElement>(fsmDebugElementName);
        if (fsmDebugText == null)
            fsmDebugText = FindFsmDebugTextIgnoreCase(document.rootVisualElement, fsmDebugElementName);
        return fsmDebugText != null;
    }

    private static TextElement FindFsmDebugTextIgnoreCase(VisualElement root, string elementName)
    {
        if (root == null || string.IsNullOrWhiteSpace(elementName))
            return null;

        if (root is TextElement rootText &&
            string.Equals(root.name, elementName, StringComparison.OrdinalIgnoreCase))
        {
            return rootText;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            TextElement childMatch = FindFsmDebugTextIgnoreCase(root[i], elementName);
            if (childMatch != null)
                return childMatch;
        }

        return null;
    }

    private void RefreshFsmDebugText()
    {
        if (fsmDebugText == null || fsmDebugText.panel == null)
            BindFsmDebugText();
        if (fsmDebugText == null)
            return;

        fsmDebugText.text = BuildFsmDebugText();
    }

    private string BuildFsmDebugText()
    {
        return BuildFsmDebugText(horizontal: true);
    }

    private string BuildFsmDebugText(bool horizontal)
    {
        EnsureStateStackInitialized();
        CursorState[] states = stateStack.ToArray();
        if (states.Length == 0)
            return CursorState.Neutral.ToString();

        StringBuilder sb = new StringBuilder();
        if (horizontal)
        {
            for (int i = states.Length - 1; i >= 0; i--)
            {
                if (sb.Length > 0)
                    sb.Append(" > ");
                sb.Append(FormatCursorStateForDebug(states[i]));
            }
            return sb.ToString();
        }

        for (int i = 0; i < states.Length; i++)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append(FormatCursorStateForDebug(states[i]));
        }
        return sb.ToString();
    }

    private static string FormatCursorStateForDebug(CursorState state)
    {
        switch (state)
        {
            case CursorState.UnitSelected:
                return "Unit Selected";
            case CursorState.MoveuAndando:
                return "Moveu Andando";
            case CursorState.MoveuParado:
                return "Moveu Parado";
            case CursorState.InspectingUnit:
                return "Inspecting Unit";
            case CursorState.InspectingBuilding:
                return "Inspecting Building";
            case CursorState.InspectingHotZone:
                return "Inspecting Hot Zone";
            case CursorState.CommandService:
                return "Command";
            case CursorState.CommandServiceExecuting:
                return "Executing";
            case CursorState.RemovingUnit:
                return "Removing Unit";
            case CursorState.RemovingUnitExecuting:
                return "Removing Unit Executing";
            case CursorState.EndingTurn:
                return "Ending Turn";
            case CursorState.EndingTurnExecuting:
                return "Ending Turn Executing";
            case CursorState.Saving:
                return "Saving";
            case CursorState.Loading:
                return "Loading";
            case CursorState.PlayerMenu:
                return "Player Menu";
            case CursorState.AircraftFuelDepletionQueue:
                return "Aircraft Fuel Depletion Queue";
            case CursorState.TurnStartRallyQueue:
                return "Turn Start Rally Queue";
            case CursorState.ShoppingAndServices:
                return "Shopping And Services";
            default:
                return state.ToString();
        }
    }

    private void NotifySensorsReady()
    {
        string selectedId = selectedUnit != null ? selectedUnit.InstanceId.ToString() : "none";
        Debug.Log($"[Replay][Dispatch] OnSensorsReady fired state={CurrentCursorState} selected={selectedId}");
        OnSensorsReady?.Invoke();
    }

    private bool IsInspectingState(CursorState state)
    {
        return state == CursorState.InspectingUnit
            || state == CursorState.InspectingBuilding
            || state == CursorState.InspectingHotZone;
    }

    private bool IsInspectingState()
    {
        return IsInspectingState(CurrentCursorState);
    }

    public bool TryFinalizeSelectedUnitActionFromDebug()
    {
        if (selectedUnit == null)
            return false;

        string finalizedUnitId = selectedUnit.InstanceId.ToString();
        CommitPreparedFuelCost();
        selectedUnit.MarkAsActed();
        ClearSelectionAndReturnToNeutral(keepPreparedFuelCost: true);
        replayManager?.PromoteCurrentBuffer($"UnitAction: {finalizedUnitId}");
        return true;
    }

    public bool TryDestroyUnitUnderCursorFromDebug(out string message)
    {
        message = string.Empty;

        if (IsMovementAnimationRunning())
        {
            message = "Nao e possivel destruir unidade durante animacao de movimento.";
            return false;
        }

        if (cursorController == null)
        {
            message = "CursorController nao encontrado.";
            return false;
        }

        Vector3Int cursorCell = cursorController.CurrentCell;
        UnitManager target = FindUnitAtCell(cursorCell);
        if (target == null)
        {
            message = $"Nenhuma unidade no cursor {FormatMapCellWithZ(cursorCell)}.";
            return false;
        }

        if (selectedUnit != null || CurrentCursorState != CursorState.Neutral)
            ClearSelectionAndReturnToNeutral();

        string targetName = ResolveDebugUnitName(target);
        StartCoroutine(ExecuteDebugDestroyUnitWithPresentation(target));
        message = $"Destruindo unidade: {targetName} em {FormatMapCellWithZ(cursorCell)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TryWakeUnitUnderCursorFromDebug(out string message)
    {
        message = string.Empty;

        if (cursorController == null)
        {
            message = "CursorController nao encontrado.";
            return false;
        }

        Vector3Int cursorCell = cursorController.CurrentCell;
        UnitManager target = FindUnitAtCell(cursorCell);
        if (target == null)
        {
            message = $"Nenhuma unidade no cursor {FormatMapCellWithZ(cursorCell)}.";
            return false;
        }

        if (!target.HasActed)
        {
            message = $"{ResolveDebugUnitName(target)} ja esta pronta para agir.";
            return false;
        }

        target.ResetActed();
        message = $"Unidade reativada: {ResolveDebugUnitName(target)} em {FormatMapCellWithZ(cursorCell)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TryWakeAllUnitsForActiveTeamFromDebug(out string message)
    {
        message = string.Empty;
        if (matchController == null)
        {
            message = "MatchController nao encontrado.";
            return false;
        }

        TeamId activeTeam = matchController.ActiveTeam;
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int totalTeamUnits = 0;
        int wokeUnits = 0;

        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.TeamId != activeTeam)
                continue;

            totalTeamUnits++;
            if (!unit.HasActed)
                continue;

            unit.ResetActed();
            wokeUnits++;
        }

        message = $"Wake all units: time ativo {TeamUtils.GetName(activeTeam)} | reativadas {wokeUnits}/{totalTeamUnits}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TrySyncDebugSelectedUnitPositionFromTransform(out string message)
    {
        message = string.Empty;
        UnitManager target = null;

#if UNITY_EDITOR
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject != null)
        {
            target = selectedObject.GetComponent<UnitManager>();
            if (target == null)
                target = selectedObject.GetComponentInParent<UnitManager>();
            if (target == null)
                target = selectedObject.GetComponentInChildren<UnitManager>();
        }
#endif

        if (target == null)
            target = selectedUnit;

        if (target == null)
        {
            message = "Nenhuma unidade selecionada no Scene/Hierarchy nem no TurnStateManager.";
            return false;
        }

        Tilemap boardMap = target.BoardTilemap != null
            ? target.BoardTilemap
            : terrainTilemap != null ? terrainTilemap : cursorController != null ? cursorController.BoardTilemap : null;
        if (boardMap == null)
        {
            message = $"Tilemap nao encontrado para sincronizar {ResolveDebugUnitName(target)}.";
            return false;
        }

        Vector3Int previousCell = target.CurrentCellPosition;
        previousCell.z = 0;
        Vector3Int targetCell = HexCoordinates.WorldToCell(boardMap, target.transform.position);
        targetCell.z = 0;

        if (target.BoardTilemap == null)
            target.SetBoardTilemap(boardMap);
        target.SetCurrentCellPosition(targetCell, enforceFinalOccupancyRule: false);
        target.RefreshRuntimeVisualState();

        message = $"Posicao sincronizada: {ResolveDebugUnitName(target)} {FormatMapCellWithZ(previousCell)} -> {FormatMapCellWithZ(targetCell)}.";
        return true;
    }

    public bool TrySetActiveTeamFromDebug(int teamValue, out string message)
    {
        message = string.Empty;
        if (matchController == null)
        {
            message = "MatchController nao encontrado.";
            return false;
        }

        if (teamValue < (int)TeamId.Neutral || teamValue > (int)TeamId.Yellow)
        {
            message = $"Team invalido: {teamValue}. Use entre {(int)TeamId.Neutral} e {(int)TeamId.Yellow}.";
            return false;
        }

        TeamId previous = matchController.ActiveTeam;
        TeamId next = (TeamId)teamValue;
        if (previous == next)
        {
            message = $"Time ativo ja e {TeamUtils.GetName(next)} ({(int)next}).";
            return true;
        }

        UnitManager previouslySelected = selectedUnit;
        bool keepSelection =
            previouslySelected != null &&
            previouslySelected.TeamId == next &&
            previouslySelected.gameObject.activeInHierarchy &&
            !previouslySelected.IsEmbarked;

        if (!keepSelection)
        {
            // Evita estado selecionado de outra equipe durante o switch forÃƒÂ¯Ã‚Â¿Ã‚Â½ado.
            ForceNeutral();
        }

        matchController.SetActiveTeamIdWithoutTurnStart(teamValue);

        if (keepSelection && cursorController != null)
        {
            Vector3Int selectedCell = previouslySelected.CurrentCellPosition;
            selectedCell.z = 0;
            cursorController.SetCell(selectedCell, playMoveSfx: false, adjustCamera: true);
        }

        message = $"Time ativo alterado: {TeamUtils.GetName(previous)} ({(int)previous}) -> {TeamUtils.GetName(next)} ({(int)next}) sem avancar turno.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TrySetUnitHpUnderCursorFromDebug(int hpValue, out string message)
    {
        message = string.Empty;

        if (cursorController == null)
        {
            message = "CursorController nao encontrado.";
            return false;
        }

        Vector3Int cursorCell = cursorController.CurrentCell;
        UnitManager target = FindUnitAtCell(cursorCell);
        if (target == null)
        {
            message = $"Nenhuma unidade no cursor {FormatMapCellWithZ(cursorCell)}.";
            return false;
        }

        int maxHp = Mathf.Max(1, target.GetMaxHP());
        int clampedHp = Mathf.Clamp(hpValue, 0, maxHp);
        int beforeHp = target.CurrentHP;
        target.SetCurrentHP(clampedHp);

        message = $"HP atualizado: {ResolveDebugUnitName(target)} {beforeHp}->{target.CurrentHP} (req={hpValue}, max={maxHp}).";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TrySetUnitAutonomyUnderCursorFromDebug(int autonomyValue, out string message)
    {
        message = string.Empty;
        if (!TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out message))
            return false;

        int beforeFuel = target.CurrentFuel;
        target.SetCurrentFuel(autonomyValue);
        message = $"Autonomia atualizada: {ResolveDebugUnitName(target)} {beforeFuel}->{target.CurrentFuel}/{target.GetMaxFuel()} em {FormatMapCellWithZ(cursorCell)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TrySetUnitEmbarkedSupplyUnderCursorFromDebug(string supplyToken, int amountValue, out string message)
    {
        message = string.Empty;
        if (!TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out message))
            return false;

        if (string.IsNullOrWhiteSpace(supplyToken))
        {
            message = "Supply invalido.";
            return false;
        }

        if (!target.TryGetUnitData(out UnitData data) || data == null || !data.isSupplier)
        {
            message = $"{ResolveDebugUnitName(target)} nao e unidade logistica/supridora.";
            return false;
        }

        IReadOnlyList<UnitEmbarkedSupply> runtimeResources = target.GetEmbarkedResources();
        if (runtimeResources == null || runtimeResources.Count <= 0)
        {
            message = $"{ResolveDebugUnitName(target)} nao possui estoque embarcado.";
            return false;
        }

        List<int> matchingRuntimeIndices = new List<int>();
        int before = 0;
        int max = 0;
        SupplyData matchedSupply = null;

        for (int i = 0; i < runtimeResources.Count; i++)
        {
            UnitEmbarkedSupply runtime = runtimeResources[i];
            if (runtime == null || !SupplyMatchesDebugToken(runtime.supply, supplyToken))
                continue;

            matchingRuntimeIndices.Add(i);
            before += Mathf.Max(0, runtime.amount);
            max += ResolveSupplierRuntimeSlotMaxAmount(data, runtime, i);
            if (matchedSupply == null)
                matchedSupply = runtime.supply;
        }

        if (matchingRuntimeIndices.Count <= 0)
        {
            message = $"{ResolveDebugUnitName(target)} nao possui supply \"{supplyToken}\".";
            return false;
        }

        if (max <= 0 && before > 0)
            max = before;

        int clamped = Mathf.Clamp(amountValue, 0, Mathf.Max(0, max));
        int remaining = clamped;
        for (int i = 0; i < matchingRuntimeIndices.Count; i++)
        {
            int runtimeIndex = matchingRuntimeIndices[i];
            UnitEmbarkedSupply runtime = runtimeResources[runtimeIndex];
            if (runtime == null)
                continue;

            int slotMax = ResolveSupplierRuntimeSlotMaxAmount(data, runtime, runtimeIndex);
            int nextAmount = Mathf.Min(remaining, slotMax);
            runtime.amount = Mathf.Max(0, nextAmount);
            remaining -= nextAmount;
        }

        int after = 0;
        for (int i = 0; i < matchingRuntimeIndices.Count; i++)
        {
            UnitEmbarkedSupply runtime = runtimeResources[matchingRuntimeIndices[i]];
            if (runtime == null)
                continue;
            after += Mathf.Max(0, runtime.amount);
        }

        string supplyLabel = ResolveSupplyDisplayName(matchedSupply);
        message = $"Estoque atualizado: {ResolveDebugUnitName(target)} {supplyLabel} {before}->{after}/{Mathf.Max(0, max)} em {FormatMapCellWithZ(cursorCell)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TrySetUnitRemainingMovementUnderCursorFromDebug(int remainingMovementValue, out string message)
    {
        message = string.Empty;
        if (!TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out message))
            return false;

        int before = target.RemainingMovementPoints;
        int max = target.MaxMovementPoints;
        int clamped = Mathf.Clamp(remainingMovementValue, 0, Mathf.Max(0, max));
        target.SetRemainingMovementPoints(clamped);
        RegisterDebugTempMoveOverride(target);
        message = $"Movimento restante temporario: {ResolveDebugUnitName(target)} {before}->{target.RemainingMovementPoints}/{max} em {FormatMapCellWithZ(cursorCell)}. Reseta ao virar rodada.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    private void RegisterDebugTempMoveOverride(UnitManager unit)
    {
        if (unit == null)
            return;

        int id = unit.InstanceId;
        if (id <= 0)
            return;

        debugTempMoveUnitInstanceIds.Add(id);
    }

    private void ClearDebugTempMoveOverridesAtTurnAdvance()
    {
        if (debugTempMoveUnitInstanceIds.Count <= 0)
            return;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.IsDead)
                continue;

            int id = unit.InstanceId;
            if (id <= 0 || !debugTempMoveUnitInstanceIds.Contains(id))
                continue;

            int max = unit.MaxMovementPoints;
            unit.SetRemainingMovementPoints(max);
        }

        debugTempMoveUnitInstanceIds.Clear();
    }

    public bool TryRefuelUnitAutonomyUnderCursorFromDebug(out string message)
    {
        message = string.Empty;
        if (!TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out message))
            return false;

        int beforeFuel = target.CurrentFuel;
        int maxFuel = target.GetMaxFuel();
        target.SetCurrentFuel(maxFuel);
        message = $"Autonomia recarregada: {ResolveDebugUnitName(target)} {beforeFuel}->{target.CurrentFuel}/{maxFuel} em {FormatMapCellWithZ(cursorCell)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TryRepairUnitUnderCursorFromDebug(out string message)
    {
        message = string.Empty;
        if (!TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out message))
            return false;

        int beforeHp = target.CurrentHP;
        int maxHp = target.GetMaxHP();
        target.SetCurrentHP(maxHp);
        message = $"Unidade reparada: {ResolveDebugUnitName(target)} {beforeHp}->{target.CurrentHP}/{maxHp} em {FormatMapCellWithZ(cursorCell)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TrySetUnitEmbarkedAmmoUnderCursorFromDebug(int weaponOneBasedIndex, int ammoValue, out string message)
    {
        message = string.Empty;
        if (!TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out message))
            return false;

        int weaponIndex = weaponOneBasedIndex - 1;
        if (weaponIndex < 0)
        {
            message = "Indice de arma invalido. Use ammo#1, ammo#2, ...";
            return false;
        }

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
        if (runtimeWeapons == null || weaponIndex >= runtimeWeapons.Count || runtimeWeapons[weaponIndex] == null)
        {
            message = $"{ResolveDebugUnitName(target)} nao possui arma #{weaponOneBasedIndex}.";
            return false;
        }

        if (!TryResolveEmbarkedWeaponMaxAmmo(target, weaponIndex, out int maxAmmo))
        {
            message = $"Nao foi possivel resolver municao maxima da arma #{weaponOneBasedIndex} para {ResolveDebugUnitName(target)}.";
            return false;
        }

        UnitEmbarkedWeapon runtimeWeapon = runtimeWeapons[weaponIndex];
        int before = Mathf.Max(0, runtimeWeapon.squadAmmunition);
        runtimeWeapon.squadAmmunition = Mathf.Clamp(ammoValue, 0, maxAmmo);
        target.RefreshRuntimeVisualState();
        message = $"Muni arma#{weaponOneBasedIndex} atualizada: {ResolveDebugUnitName(target)} {before}->{runtimeWeapon.squadAmmunition}/{maxAmmo} em {FormatMapCellWithZ(cursorCell)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TryReplenishUnitEmbarkedAmmoUnderCursorFromDebug(out string message)
    {
        message = string.Empty;
        if (!TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out message))
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = target.GetEmbarkedWeapons();
        if (runtimeWeapons == null || runtimeWeapons.Count <= 0)
        {
            message = $"{ResolveDebugUnitName(target)} nao possui armas embarcadas.";
            return false;
        }

        int changed = 0;
        for (int i = 0; i < runtimeWeapons.Count; i++)
        {
            UnitEmbarkedWeapon runtimeWeapon = runtimeWeapons[i];
            if (runtimeWeapon == null)
                continue;
            if (!TryResolveEmbarkedWeaponMaxAmmo(target, i, out int maxAmmo))
                continue;

            int before = Mathf.Max(0, runtimeWeapon.squadAmmunition);
            runtimeWeapon.squadAmmunition = maxAmmo;
            if (runtimeWeapon.squadAmmunition != before)
                changed++;
        }

        target.RefreshRuntimeVisualState();
        message = $"Muni recarregada: {ResolveDebugUnitName(target)} | armas atualizadas={changed} em {FormatMapCellWithZ(cursorCell)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TrySpawnUnitUnderCursorFromDebug(string unitToken, int? teamOverride, out string message)
    {
        message = string.Empty;

        if (cursorController == null)
        {
            message = "CursorController nao encontrado.";
            return false;
        }

        if (unitSpawner == null)
        {
            message = "UnitSpawner nao encontrado.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(unitToken))
        {
            message = "Informe uma unidade. Ex.: spawn SD";
            return false;
        }

        if (!unitSpawner.TryResolveUnitDataByToken(unitToken, out UnitData unitData, out string resolveReason) || unitData == null)
        {
            message = string.IsNullOrWhiteSpace(resolveReason)
                ? $"Unidade nao encontrada para \"{unitToken}\"."
                : resolveReason;
            return false;
        }

        Vector3Int cursorCell = cursorController.CurrentCell;
        cursorCell.z = 0;
        int activeTeam = matchController != null ? matchController.ActiveTeamId : (int)TeamId.Green;
        int resolvedTeam = teamOverride.HasValue ? teamOverride.Value : activeTeam;
        TeamId teamId = (resolvedTeam >= (int)TeamId.Green && resolvedTeam <= (int)TeamId.Yellow)
            ? (TeamId)resolvedTeam
            : TeamId.Green;

        GameObject spawned = unitSpawner.SpawnAtCell(unitData, teamId, cursorCell);
        if (spawned == null)
        {
            message = $"Falha ao spawnar {ResolveDebugUnitDataName(unitData)} em {FormatMapCellWithZ(cursorCell)}. Hex pode estar ocupado.";
            return false;
        }

        RefreshFogOfWarAfterSpawn();
        message = $"Spawnado: {ResolveDebugUnitDataName(unitData)} em {FormatMapCellWithZ(cursorCell)} para team {TeamUtils.GetName(teamId)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TrySpawnUnitAtCell(string unitToken, int teamId, Vector3Int cell, out string message)
    {
        message = string.Empty;
        cell.z = 0;

        if (unitSpawner == null)
        {
            message = "UnitSpawner nao encontrado.";
            return false;
        }

        if (!unitSpawner.TryResolveUnitDataByToken(unitToken, out UnitData unitData, out string resolveReason) || unitData == null)
        {
            message = resolveReason;
            return false;
        }

        TeamId resolvedTeam = (teamId >= (int)TeamId.Green && teamId <= (int)TeamId.Yellow)
            ? (TeamId)teamId
            : TeamId.Green;

        GameObject spawned = unitSpawner.SpawnAtCell(unitData, resolvedTeam, cell);
        if (spawned == null)
        {
            message = $"Falha ao spawnar {unitToken} em {cell}.";
            return false;
        }

        RefreshFogOfWarAfterSpawn();
        message = $"Spawnado: {unitToken} em {cell} para team {TeamUtils.GetName(resolvedTeam)}.";
        return true;
    }

    private void RefreshFogOfWarAfterSpawn()
    {
        if (matchController == null)
            return;

        matchController.RefreshFogOfWarForActiveTeam();
    }

    public bool TrySetConstructionTeamUnderCursorFromDebug(int teamValue, out string message)
    {
        message = string.Empty;
        if (!TryGetConstructionUnderCursorForDebug(out ConstructionManager target, out Vector3Int cursorCell, out message))
            return false;

        if (teamValue < (int)TeamId.Neutral || teamValue > (int)TeamId.Yellow)
        {
            message = $"Team invalido: {teamValue}. Use entre {(int)TeamId.Neutral} e {(int)TeamId.Yellow}.";
            return false;
        }

        TeamId before = target.TeamId;
        TeamId next = (TeamId)teamValue;
        target.SetTeamId(next);
        message = $"Construcao atualizada: {target.name} team {TeamUtils.GetName(before)} -> {TeamUtils.GetName(next)} em {FormatMapCellWithZ(cursorCell)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TrySetConstructionCapturePointsUnderCursorFromDebug(int capturePointsValue, out string message)
    {
        message = string.Empty;
        if (!TryGetConstructionUnderCursorForDebug(out ConstructionManager target, out Vector3Int cursorCell, out message))
            return false;

        int before = target.CurrentCapturePoints;
        int max = Mathf.Max(0, target.CapturePointsMax);
        target.SetCurrentCapturePoints(capturePointsValue);
        message = $"Capture points atualizados: {target.name} {before}->{target.CurrentCapturePoints}/{max} (req={capturePointsValue}) em {FormatMapCellWithZ(cursorCell)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
    }

    public bool TryGetSelectedUnitPath(Vector3Int destinationCell, out List<Vector3Int> path)
    {
        destinationCell.z = 0;
        if (movementPathsByCell.TryGetValue(destinationCell, out List<Vector3Int> storedPath))
        {
            path = new List<Vector3Int>(storedPath);
            return true;
        }

        path = null;
        return false;
    }

    public bool TryGetCommittedMovementPath(out List<Vector3Int> path, out Vector3Int originCell, out Vector3Int destinationCell)
    {
        if (!hasCommittedMovement || committedMovementPath.Count < 2)
        {
            path = null;
            originCell = Vector3Int.zero;
            destinationCell = Vector3Int.zero;
            return false;
        }

        path = new List<Vector3Int>(committedMovementPath);
        originCell = committedOriginCell;
        destinationCell = committedDestinationCell;
        return true;
    }

    private void Awake()
    {
        stateStack.Clear();
        stateStack.Push(CursorState.Neutral);
        TryAutoAssignReferences();
        BindFsmDebugText();
        RefreshFsmDebugText();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoAssignReferences();
    }
#endif

    public void ForceNeutral()
    {
        if (CurrentCursorState == CursorState.Planning)
            planningManager?.ExitPlanningMode();
        ClearSelectionAndReturnToNeutral();
    }

    private void SetSelectedUnit(UnitManager unit)
    {
        double perfStart = Time.realtimeSinceStartupAsDouble;
        if (selectedUnit == unit)
        {
            RegisterPerfSelectionDuration((Time.realtimeSinceStartupAsDouble - perfStart) * 1000d);
            return;
        }

        ClearSensorResults();

        if (selectedUnit != null)
            animationManager?.ClearSelectionVisual(selectedUnit);

        selectedUnit = unit;
        if (selectedUnit != null)
        {
            animationManager?.ApplySelectionVisual(selectedUnit);
            PaintSelectedUnitMovementRange();
            OnUnitSelected?.Invoke(selectedUnit);
        }

        RegisterPerfSelectionDuration((Time.realtimeSinceStartupAsDouble - perfStart) * 1000d);
    }

    private void ClearSelectionAndReturnToNeutral(bool keepPreparedFuelCost = false)
    {
        animationManager?.StopCurrentMovement();
        ClearCommittedPathVisual();
        ClearSensorResults();
        shoppingConstruction = null;
        shoppingUnitsForSale.Clear();

        if (keepPreparedFuelCost)
        {
            CommitPreparedFuelCost();
            CommitPreparedMovementCost();
        }
        else
        {
            RestorePreparedFuelCostIfAny();
            RestorePreparedMovementCostIfAny();
        }

        if (selectedUnit != null)
            animationManager?.ClearSelectionVisual(selectedUnit);

        if (keepPreparedFuelCost)
            CommitTemporaryTakeoffSelectionState();
        else
            RestoreTemporaryTakeoffSelectionStateIfAny();

        selectedUnit = null;
        ExecuteAndReset("ClearSelectionAndReturnToNeutral");
        ClearMovementRange();
        ClearCommittedMovement();
    }
    private void CommitTemporaryTakeoffSelectionState()
    {
        hasTemporaryTakeoffSelectionState = false;
        temporaryTakeoffUnit = null;
        temporaryTakeoffMoveOptions.Clear();
        hasAutoPromotionEntryLayer = false;
        autoPromotionUnit = null;
    }

    private void RestoreTemporaryTakeoffSelectionStateIfAny()
    {
        if (hasTemporaryTakeoffSelectionState && temporaryTakeoffUnit != null)
        {
            temporaryTakeoffUnit.TrySetCurrentLayerMode(temporaryTakeoffOriginalDomain, temporaryTakeoffOriginalHeight);
            temporaryTakeoffUnit.SetAircraftGrounded(temporaryTakeoffOriginalGrounded);
            temporaryTakeoffUnit.SetAircraftEmbarkedInCarrier(temporaryTakeoffOriginalEmbarkedInCarrier);
        }

        if (hasAutoPromotionEntryLayer && autoPromotionUnit != null)
            autoPromotionUnit.TrySetCurrentLayerMode(autoPromotionEntryDomain, autoPromotionEntryHeight);

        hasTemporaryTakeoffSelectionState = false;
        temporaryTakeoffUnit = null;
        temporaryTakeoffMoveOptions.Clear();
        hasAutoPromotionEntryLayer = false;
        autoPromotionUnit = null;
    }

    private bool TryPrepareTemporaryTakeoffStateForSelection(UnitManager unit, out string reason)
    {
        reason = string.Empty;
        CommitTemporaryTakeoffSelectionState();

        if (unit == null)
            return false;

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : unit.BoardTilemap;
        PodeDecolarReport takeoffReport = PodeDecolarSensor.Evaluate(unit, boardMap, terrainDatabase);
        if (takeoffReport == null || takeoffReport.takeoffMoveOptions == null || takeoffReport.takeoffMoveOptions.Count == 0)
        {
            reason = takeoffReport != null ? takeoffReport.explicacao : string.Empty;
            return false;
        }

        if (takeoffReport.takeoffMoveOptions.Count == 1 && takeoffReport.takeoffMoveOptions[0] == -1)
        {
            TryPromoteAirborneUnitToPreferredHeight(unit, out reason);
            return false;
        }

        if (!takeoffReport.status)
        {
            reason = takeoffReport.explicacao;
            return false;
        }

        Domain originalDomain = unit.GetDomain();
        HeightLevel originalHeight = unit.GetHeightLevel();
        bool originalGrounded = unit.IsAircraftGrounded;
        bool originalEmbarkedInCarrier = unit.IsAircraftEmbarkedInCarrier;

        bool fullMoveTakeoff = takeoffReport.takeoffMoveOptions.Contains(9);
        HeightLevel targetHeight = fullMoveTakeoff ? unit.GetPreferredAirHeight() : HeightLevel.AirLow;
        if (!unit.TrySetCurrentLayerMode(Domain.Air, targetHeight))
        {
            reason = "Falha ao aplicar decolagem temporaria para selecao.";
            return false;
        }

        temporaryTakeoffUnit = unit;
        temporaryTakeoffOriginalDomain = originalDomain;
        temporaryTakeoffOriginalHeight = originalHeight;
        temporaryTakeoffOriginalGrounded = originalGrounded;
        temporaryTakeoffOriginalEmbarkedInCarrier = originalEmbarkedInCarrier;
        hasTemporaryTakeoffSelectionState = true;
        temporaryTakeoffMoveOptions.Clear();
        temporaryTakeoffMoveOptions.AddRange(takeoffReport.takeoffMoveOptions);

        unit.SetAircraftGrounded(false);
        unit.SetAircraftEmbarkedInCarrier(false);
        reason = takeoffReport.explicacao;
        return true;
    }

    private bool TryPromoteAirborneUnitToPreferredHeight(UnitManager unit, out string info)
    {
        info = string.Empty;
        if (unit == null)
            return false;
        if (unit.GetDomain() != Domain.Air || unit.IsAircraftGrounded)
            return false;

        Domain startDomain = unit.GetDomain();
        HeightLevel startHeight = unit.GetHeightLevel();
        HeightLevel preferred = unit.GetPreferredAirHeight();
        if (startHeight == preferred)
        {
            info = "Aeronave em voo ja esta na altitude nativa.";
            return false;
        }

        if (!unit.TrySetCurrentLayerMode(Domain.Air, preferred))
            return false;

        hasAutoPromotionEntryLayer = true;
        autoPromotionUnit = unit;
        autoPromotionEntryDomain = startDomain;
        autoPromotionEntryHeight = startHeight;
        info = $"Aeronave em voo ajustada para altitude nativa ({preferred}).";
        return true;
    }

    private bool IsTakeoffMoveDistanceAllowed(int movementSteps)
    {
        if (!hasTemporaryTakeoffSelectionState || temporaryTakeoffMoveOptions.Count == 0)
            return true;

        movementSteps = Mathf.Max(0, movementSteps);
        if (temporaryTakeoffMoveOptions.Contains(9))
            return movementSteps == 0 || movementSteps >= 1;

        return temporaryTakeoffMoveOptions.Contains(movementSteps);
    }

    private bool IsRangeCellAllowedByTakeoffOptions(Vector3Int cell, IReadOnlyList<Vector3Int> path)
    {
        if (!hasTemporaryTakeoffSelectionState || temporaryTakeoffMoveOptions.Count == 0 || selectedUnit == null)
            return true;

        if (temporaryTakeoffMoveOptions.Contains(-1) || temporaryTakeoffMoveOptions.Contains(9))
            return true;

        int movementHexes = (path != null && path.Count > 0) ? Mathf.Max(0, path.Count - 1) : 0;
        if (temporaryTakeoffMoveOptions.Contains(0) && temporaryTakeoffMoveOptions.Contains(1))
            return movementHexes <= 1;
        if (temporaryTakeoffMoveOptions.Contains(0))
            return movementHexes == 0;
        if (temporaryTakeoffMoveOptions.Contains(1))
            return movementHexes == 1;

        return true;
    }

    private bool TryGetAutoPromotionEntryLayer(out Domain domain, out HeightLevel height)
    {
        domain = autoPromotionEntryDomain;
        height = autoPromotionEntryHeight;
        return hasAutoPromotionEntryLayer;
    }

    private IEnumerator ExecuteDebugDestroyUnitWithPresentation(UnitManager target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            yield break;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        Vector3 worldPos = target.transform.position;

        target.SetCurrentHP(0);
        target.MarkDead("morto pelo comando destroy unit");
        KillEmbarkedChildrenChain(target);
        yield return ExecuteUnitDeathPresentation(target, targetCell, worldPos, applyStartDelay: false);
    }

    public void EnqueueTurnStartFuelDepletionDeaths(IReadOnlyList<UnitManager> units)
    {
        if (!Application.isPlaying || units == null || units.Count <= 0)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;

            if (turnStartFuelDepletionDeathQueue.Contains(unit))
                continue;

            turnStartFuelDepletionDeathQueue.Add(unit);
        }

        if (!turnStartFuelDepletionExecutionInProgress && turnStartFuelDepletionDeathQueue.Count > 0)
        {
            replayManager?.BeginTurnRecording();
            StartCoroutine(ExecuteTurnStartFuelDepletionDeathQueue());
        }
    }

    public bool TryStartAutomatedFuelDepletionReplayQueue(PlayerAction action)
    {
        if (!Application.isPlaying || action == null)
            return false;

        bool isFuelQueueAction =
            string.Equals(action.SubStepLabel, "TurnStartFuelDepletionQueue", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(action.DebugLabel) && action.DebugLabel.IndexOf("TurnStartFuelDepletionQueue", StringComparison.OrdinalIgnoreCase) >= 0);
        if (!isFuelQueueAction)
            return false;

        List<UnitManager> resolvedUnits = ResolveFuelDepletionReplayUnits(action);
        if (resolvedUnits.Count <= 0)
            return false;

        EnqueueTurnStartFuelDepletionDeaths(resolvedUnits);
        return true;
    }

    private List<UnitManager> ResolveFuelDepletionReplayUnits(PlayerAction action)
    {
        List<UnitManager> list = new List<UnitManager>();

        if (action.SubSteps != null)
        {
            for (int i = 0; i < action.SubSteps.Count; i++)
            {
                PlayerActionSubStep step = action.SubSteps[i];
                if (!TryResolveFuelDepletionReplayTarget(step != null ? step.TargetInstanceId : null, step != null && step.HasTargetHex, step != null ? step.TargetHex : Vector3Int.zero, out UnitManager unit))
                    continue;
                if (!list.Contains(unit))
                    list.Add(unit);
            }
        }

        if (list.Count <= 0)
        {
            if (TryResolveFuelDepletionReplayTarget(action.TargetInstanceId, action.HasTargetHex, action.TargetHex, out UnitManager single))
                list.Add(single);
        }

        return list;
    }

    private bool TryResolveFuelDepletionReplayTarget(string targetInstanceId, bool hasTargetHex, Vector3Int targetHex, out UnitManager unit)
    {
        unit = null;

        if (!string.IsNullOrWhiteSpace(targetInstanceId) && int.TryParse(targetInstanceId, out int id) && id > 0)
            unit = FindActiveUnitByInstanceId(id);

        if (unit == null && hasTargetHex)
        {
            Vector3Int cell = targetHex;
            cell.z = 0;
            unit = FindUnitAtCell(cell);
        }

        return unit != null && unit.gameObject.activeInHierarchy;
    }

    private static UnitManager FindActiveUnitByInstanceId(int instanceId)
    {
        if (instanceId <= 0)
            return null;

        UnitManager[] activeUnits = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < activeUnits.Length; i++)
        {
            UnitManager unit = activeUnits[i];
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;
            if (unit.InstanceId == instanceId)
                return unit;
        }

        return null;
    }
    private IEnumerator ExecuteTurnStartFuelDepletionDeathQueue()
    {
        if (turnStartFuelDepletionExecutionInProgress)
            yield break;

        turnStartFuelDepletionExecutionInProgress = true;
        EnterTurnStartFuelDepletionCursorState();
        try
        {
            turnStartFuelDepletionReplaySubSteps.Clear();
            float initialDelay = animationManager != null ? animationManager.TurnStartFuelDeathInitialDelay : 0.20f;
            float cursorFocusDelay = animationManager != null ? animationManager.TurnStartFuelDeathCursorFocusDelay : 0.20f;
            float replayCursorStepDelay = replayManager != null
                ? Mathf.Max(0f, replayManager.GetEffectiveCursorTravelStepDelayForRuntimeMotion())
                : Mathf.Max(0f, cursorFocusDelay);
            float betweenKillsDelay = animationManager != null ? animationManager.TurnStartFuelDeathBetweenKillsDelay : 0.15f;
            int actionTurn = matchController != null ? matchController.CurrentTurn : 0;
            TeamId actionTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;

            if (turnStartFuelDepletionDeathQueue.Count > 0 && initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);

            while (turnStartFuelDepletionDeathQueue.Count > 0)
            {
                UnitManager target = turnStartFuelDepletionDeathQueue[0];
                turnStartFuelDepletionDeathQueue.RemoveAt(0);
                if (target == null || !target.gameObject.activeInHierarchy)
                    continue;

                if (selectedUnit == target)
                    ClearSelectionForTurnStartFuelDepletionQueue();

                Vector3Int targetCell = target.CurrentCellPosition;
                targetCell.z = 0;
                Vector3 worldPos = target.transform.position;
                string targetInstanceId = target.InstanceId.ToString();

                yield return MoveCursorToCellLikeReplayAtTurnStart(targetCell, replayCursorStepDelay);

                turnStartFuelDepletionReplaySubSteps.Add(new PlayerActionSubStep
                {
                    TargetHex = targetCell,
                    HasTargetHex = true,
                    TargetInstanceId = targetInstanceId,
                    Label = "TurnStartFuelDepletionQueueConfirm"
                });

                target.SetCurrentHP(0);
                target.MarkDead("morto por falta de combustivel/ queda aerea");
                KillEmbarkedChildrenChain(target);
                PanelDialogController.TrySetTransientText("caiu por falta de combustivel", 2.6f);
                yield return ExecuteUnitDeathPresentation(
                    target,
                    targetCell,
                    worldPos,
                    applyStartDelay: false,
                    moveCursorFirst: false);
                yield return WaitForFuelDepletionDeathVisualSettle(target);

                cursorController?.PlayLoadSfx();

                if (turnStartFuelDepletionDeathQueue.Count > 0)
                {
                    if (betweenKillsDelay > 0f)
                        yield return new WaitForSeconds(betweenKillsDelay);
                }
            }

            if (turnStartFuelDepletionReplaySubSteps.Count > 0)
            {
                PlayerActionSubStep last = turnStartFuelDepletionReplaySubSteps[turnStartFuelDepletionReplaySubSteps.Count - 1];
                replayManager?.RecordStandaloneAction(new PlayerAction
                {
                    ActionType = PlayerActionType.RemoveUnit,
                    TurnNumber = actionTurn,
                    ActingTeam = actionTeam,
                    CursorHex = last.TargetHex,
                    HasCursorHex = true,
                    TargetHex = last.TargetHex,
                    HasTargetHex = true,
                    TargetInstanceId = last.TargetInstanceId,
                    SensorAction = SensorActionType.RemoveUnit,
                    SubStepLabel = "TurnStartFuelDepletionQueue",
                    SubSteps = new List<PlayerActionSubStep>(turnStartFuelDepletionReplaySubSteps),
                    Confirmed = true,
                    DebugLabel = $"TurnStartFuelDepletionQueue: {turnStartFuelDepletionReplaySubSteps.Count} removals"
                });
            }
        }
        finally
        {
            turnStartFuelDepletionReplaySubSteps.Clear();
            turnStartFuelDepletionExecutionInProgress = false;
            if (turnStartFuelDepletionDeathQueue.Count > 0)
                StartCoroutine(ExecuteTurnStartFuelDepletionDeathQueue());
            else
                StartCoroutine(ExecutePostFuelDepletionTurnStartQueues());
        }
    }

    private IEnumerator ExecutePostFuelDepletionTurnStartQueues()
    {
        if (planningManager == null)
        {
            ExitTurnStartFuelDepletionCursorState();
            yield break;
        }

        TeamId team = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        if (!planningManager.HasActiveAssignmentsForTeam(team))
        {
            ExitTurnStartFuelDepletionCursorState();
            yield break;
        }

        yield return ExecuteTurnStartRallyQueueRoutine();
    }

    private void EnterTurnStartFuelDepletionCursorState()
    {
        if (CurrentCursorState == CursorState.AircraftFuelDepletionQueue)
            return;

        Advance(CursorState.AircraftFuelDepletionQueue, "TurnStartFuelDepletionQueue: begin");
    }

    private void ExitTurnStartFuelDepletionCursorState()
    {
        if (CurrentCursorState != CursorState.AircraftFuelDepletionQueue)
            return;

        ExecuteAndReset("TurnStartFuelDepletionQueue: completed");
    }

    private void ClearSelectionForTurnStartFuelDepletionQueue()
    {
        animationManager?.StopCurrentMovement();
        ClearCommittedPathVisual();
        ClearSensorResults();
        shoppingConstruction = null;
        shoppingUnitsForSale.Clear();
        RestorePreparedFuelCostIfAny();
        RestorePreparedMovementCostIfAny();

        if (selectedUnit != null)
            animationManager?.ClearSelectionVisual(selectedUnit);

        RestoreTemporaryTakeoffSelectionStateIfAny();
        selectedUnit = null;
        ClearMovementRange();
        ClearCommittedMovement();
    }

    private IEnumerator MoveCursorToCellLikeReplayAtTurnStart(Vector3Int targetCell, float stepDelay)
    {
        if (cursorController == null)
            yield break;

        Vector3Int fromCell = NormalizeTurnStartCursorCell(cursorController.CurrentCell);
        Vector3Int toCell = NormalizeTurnStartCursorCell(targetCell);
        if (fromCell == toCell)
        {
            if (stepDelay > 0f)
                yield return new WaitForSeconds(stepDelay);
            yield break;
        }

        List<Vector3Int> path = BuildTurnStartCursorTravelPath(fromCell, toCell);
        if (path == null || path.Count <= 0)
            path = new List<Vector3Int> { toCell };

        for (int i = 0; i < path.Count; i++)
        {
            Vector3Int stepCell = NormalizeTurnStartCursorCell(path[i]);
            cursorController.SetCell(stepCell, playMoveSfx: true, adjustCamera: false);
            cursorController.TryAdjustCameraToCursor();

            if (stepDelay > 0f)
                yield return new WaitForSeconds(stepDelay);
            else
                yield return null;
        }
    }

    private List<Vector3Int> BuildTurnStartCursorTravelPath(Vector3Int fromCell, Vector3Int toCell)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        fromCell = NormalizeTurnStartCursorCell(fromCell);
        toCell = NormalizeTurnStartCursorCell(toCell);

        if (fromCell == toCell)
            return path;

        Tilemap board = cursorController != null ? cursorController.BoardTilemap : null;
        if (board == null)
        {
            path.Add(toCell);
            return path;
        }

        // Mantem o travel do cursor dentro da area pintada do tabuleiro.
        if (!board.HasTile(fromCell) || !board.HasTile(toCell))
        {
            path.Add(toCell);
            return path;
        }

        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        queue.Enqueue(fromCell);
        visited.Add(fromCell);

        bool found = false;
        int guard = 0;
        const int maxVisited = 8192;
        while (queue.Count > 0 && guard++ < maxVisited)
        {
            Vector3Int current = queue.Dequeue();
            if (current == toCell)
            {
                found = true;
                break;
            }

            neighbors.Clear();
            UnitMovementPathRules.GetImmediateHexNeighbors(board, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = NormalizeTurnStartCursorCell(neighbors[i]);
                if (!board.HasTile(next))
                    continue;
                if (!visited.Add(next))
                    continue;

                cameFrom[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!found)
        {
            path.Add(toCell);
            return path;
        }

        List<Vector3Int> reversed = new List<Vector3Int>();
        Vector3Int walk = toCell;
        while (walk != fromCell)
        {
            reversed.Add(walk);
            if (!cameFrom.TryGetValue(walk, out Vector3Int previous))
            {
                reversed.Clear();
                reversed.Add(toCell);
                break;
            }

            walk = previous;
        }

        for (int i = reversed.Count - 1; i >= 0; i--)
            path.Add(reversed[i]);

        return path;
    }

    private static Vector3Int NormalizeTurnStartCursorCell(Vector3Int cell)
    {
        cell.z = 0;
        return cell;
    }

    private static IEnumerator WaitForFuelDepletionDeathVisualSettle(UnitManager target)
    {
        const float minPresentationSeconds = 0.65f;
        const float maxWaitSeconds = 3.0f;

        float start = Time.realtimeSinceStartup;
        bool targetStillActive = target != null && target.gameObject != null && target.gameObject.activeInHierarchy;
        while (Time.realtimeSinceStartup - start < maxWaitSeconds)
        {
            float elapsed = Time.realtimeSinceStartup - start;
            bool minimumSatisfied = elapsed >= minPresentationSeconds;
            bool isActiveNow = target != null && target.gameObject != null && target.gameObject.activeInHierarchy;

            if (minimumSatisfied && !isActiveNow)
                yield break;

            // Se o alvo iniciou ativo, exige ao menos uma transicao para inativo
            // antes de liberar o proximo item da fila.
            if (!targetStillActive && minimumSatisfied)
                yield break;

            if (isActiveNow)
                targetStillActive = true;

            yield return null;
        }
    }

    private static string ResolveDebugUnitName(UnitManager unit)
    {
        if (unit == null)
            return "(null)";
        if (!string.IsNullOrWhiteSpace(unit.UnitDisplayName))
            return unit.UnitDisplayName;
        if (!string.IsNullOrWhiteSpace(unit.UnitId))
            return unit.UnitId;
        return unit.name;
    }

    private static string ResolveDebugUnitDataName(UnitData data)
    {
        if (data == null)
            return "(null)";
        if (!string.IsNullOrWhiteSpace(data.displayName))
            return data.displayName;
        if (!string.IsNullOrWhiteSpace(data.apelido))
            return data.apelido;
        if (!string.IsNullOrWhiteSpace(data.id))
            return data.id;
        return data.name;
    }

    protected static string FormatMapCell(Vector3Int cell)
    {
        cell.z = 0;
        int linha = -cell.y;
        int coluna = cell.x;
        return $"L{linha},C{coluna}";
    }

    protected static string FormatMapCellWithZ(Vector3Int cell)
    {
        return $"({FormatMapCell(cell)},0)";
    }

    private bool TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out string message)
    {
        target = null;
        cursorCell = Vector3Int.zero;
        message = string.Empty;

        if (cursorController == null)
        {
            message = "CursorController nao encontrado.";
            return false;
        }

        cursorCell = cursorController.CurrentCell;
        cursorCell.z = 0;
        target = FindUnitAtCell(cursorCell);
        if (target == null)
        {
            message = $"Nenhuma unidade no cursor {FormatMapCellWithZ(cursorCell)}.";
            return false;
        }

        return true;
    }

    private bool TryGetConstructionUnderCursorForDebug(out ConstructionManager target, out Vector3Int cursorCell, out string message)
    {
        target = null;
        cursorCell = Vector3Int.zero;
        message = string.Empty;

        if (cursorController == null)
        {
            message = "CursorController nao encontrado.";
            return false;
        }

        cursorCell = cursorController.CurrentCell;
        cursorCell.z = 0;

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : cursorController.BoardTilemap;
        if (boardMap != null)
            target = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cursorCell);

        if (target == null)
        {
            ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < constructions.Length; i++)
            {
                ConstructionManager construction = constructions[i];
                if (construction == null)
                    continue;
                Vector3Int constructionCell = construction.CurrentCellPosition;
                constructionCell.z = 0;
                if (constructionCell == cursorCell)
                {
                    target = construction;
                    break;
                }
            }
        }

        if (target == null)
        {
            message = $"Nenhuma construcao no cursor {FormatMapCellWithZ(cursorCell)}.";
            return false;
        }

        return true;
    }

    private static bool TryResolveEmbarkedWeaponMaxAmmo(UnitManager unit, int weaponIndex, out int maxAmmo)
    {
        maxAmmo = 0;
        if (unit == null || weaponIndex < 0)
            return false;

        if (!unit.TryGetUnitData(out UnitData data) || data == null || data.embarkedWeapons == null || weaponIndex >= data.embarkedWeapons.Count)
            return false;

        UnitEmbarkedWeapon baseline = data.embarkedWeapons[weaponIndex];
        if (baseline == null)
            return false;

        maxAmmo = Mathf.Max(0, baseline.squadAmmunition);
        return true;
    }

    private static bool SupplyMatchesDebugToken(SupplyData supply, string token)
    {
        if (supply == null || string.IsNullOrWhiteSpace(token))
            return false;

        string normalizedToken = token.Trim();
        return string.Equals(supply.id, normalizedToken, System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(supply.displayName, normalizedToken, System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(supply.name, normalizedToken, System.StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveSupplierRuntimeSlotMaxAmount(UnitData data, UnitEmbarkedSupply runtimeEntry, int runtimeIndex)
    {
        int fallbackCurrent = runtimeEntry != null ? Mathf.Max(0, runtimeEntry.amount) : 0;
        if (data == null || data.supplierResources == null || runtimeIndex < 0 || runtimeIndex >= data.supplierResources.Count)
            return fallbackCurrent;

        UnitEmbarkedSupply baseline = data.supplierResources[runtimeIndex];
        if (baseline == null)
            return fallbackCurrent;

        if (runtimeEntry == null || runtimeEntry.supply == null || baseline.supply == null)
            return Mathf.Max(0, baseline.amount);

        if (!AreSameSupply(baseline.supply, runtimeEntry.supply))
            return fallbackCurrent;

        return Mathf.Max(0, baseline.amount);
    }

    private static bool AreSameSupply(SupplyData a, SupplyData b)
    {
        if (a == null || b == null)
            return false;

        if (a == b)
            return true;

        if (!string.IsNullOrWhiteSpace(a.id) && !string.IsNullOrWhiteSpace(b.id))
            return string.Equals(a.id, b.id, System.StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private void TryAutoAssignReferences()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();

        if (animationManager == null)
            animationManager = FindAnyObjectByType<AnimationManager>();

        if (pathManager == null)
            pathManager = FindAnyObjectByType<PathManager>();
        if (pathManager == null)
        {
            GameObject go = new GameObject("Path Manager");
            pathManager = go.AddComponent<PathManager>();
        }

        if (unitSpawner == null)
            unitSpawner = FindAnyObjectByType<UnitSpawner>();

        if (replayManager == null)
            replayManager = FindAnyObjectByType<ReplayManager>();

        if (planningManager == null)
            planningManager = FindAnyObjectByType<PlanningManager>();

        if (terrainTilemap == null)
            terrainTilemap = ResolvePreferredTerrainTilemap();

        if (rangeMapTilemap == null)
            rangeMapTilemap = FindRangeMapTilemap();

        if (lineOfFireMapTilemap == null)
            lineOfFireMapTilemap = FindLineOfFireMapTilemap();

#if UNITY_EDITOR
        if (dpqAirHeightConfig == null)
            dpqAirHeightConfig = FindFirstAssetEditor<DPQAirHeightConfig>();
        if (terrainDatabase == null)
            terrainDatabase = FindFirstAssetEditor<TerrainDatabase>();
        if (weaponPriorityData == null)
            weaponPriorityData = FindFirstAssetEditor<WeaponPriorityData>();
        if (dpqMatchupDatabase == null)
            dpqMatchupDatabase = FindFirstAssetEditor<DPQMatchupDatabase>();
        if (rpsDatabase == null)
            rpsDatabase = FindFirstAssetEditor<RPSDatabase>();
#endif
    }

    private Tilemap ResolvePreferredTerrainTilemap()
    {
        Tilemap namedBoard = FindTilemapByName("TileMap");
        if (namedBoard != null)
            return namedBoard;

        if (cursorController != null && cursorController.BoardTilemap != null)
            return cursorController.BoardTilemap;

        return null;
    }

    private static Tilemap FindTilemapByName(string expectedName)
    {
        if (string.IsNullOrWhiteSpace(expectedName))
            return null;

        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            if (string.Equals(map.name, expectedName, System.StringComparison.OrdinalIgnoreCase))
                return map;
        }

        return null;
    }

    private void ClearCommittedMovement()
    {
        committedMovementPath.Clear();
        committedOriginCell = Vector3Int.zero;
        committedDestinationCell = Vector3Int.zero;
        hasCommittedMovement = false;
        hasCommittedMovementLayerBefore = false;
        hasForcedLayerRollbackSnapshot = false;
        ClearCommittedPathVisual();
    }

#if UNITY_EDITOR
    private static T FindFirstAssetEditor<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
        }

        return null;
    }
#endif

    private int GetAvailableMovementSteps(UnitManager unit)
    {
        if (unit == null)
            return 0;

        return Mathf.Max(0, unit.RemainingMovementPoints);
    }

    private void PrepareFuelCostForCommittedPath()
    {
        Tilemap movementTilemap = terrainTilemap != null ? terrainTilemap : (selectedUnit != null ? selectedUnit.BoardTilemap : null);
        int movementCost = UnitMovementPathRules.CalculateAutonomyCostForPath(
            movementTilemap,
            selectedUnit,
            committedMovementPath,
            terrainDatabase);
        ApplyPreparedFuelCost(movementCost);
    }

    private void PrepareMovementCostForCommittedPath()
    {
        Tilemap movementTilemap = terrainTilemap != null ? terrainTilemap : (selectedUnit != null ? selectedUnit.BoardTilemap : null);
        int movementCost = UnitMovementPathRules.CalculateAutonomyCostForPath(
            movementTilemap,
            selectedUnit,
            committedMovementPath,
            terrainDatabase,
            applyOperationalAutonomyModifier: false);
        ApplyPreparedMovementCost(movementCost);
    }

    private void ApplyPreparedFuelCost(int movementCost)
    {
        if (selectedUnit == null)
        {
            preparedFuelCost = 0;
            hasPreparedFuelCost = false;
            return;
        }

        RestorePreparedFuelCostIfAny();

        int clampedCost = Mathf.Clamp(movementCost, 0, selectedUnit.CurrentFuel);
        if (clampedCost <= 0)
        {
            preparedFuelCost = 0;
            hasPreparedFuelCost = false;
            return;
        }

        selectedUnit.SetCurrentFuel(selectedUnit.CurrentFuel - clampedCost);
        preparedFuelCost = clampedCost;
        hasPreparedFuelCost = true;
    }

    private void RestorePreparedFuelCostIfAny()
    {
        if (!hasPreparedFuelCost || preparedFuelCost <= 0 || selectedUnit == null)
        {
            preparedFuelCost = 0;
            hasPreparedFuelCost = false;
            return;
        }

        selectedUnit.SetCurrentFuel(selectedUnit.CurrentFuel + preparedFuelCost);
        preparedFuelCost = 0;
        hasPreparedFuelCost = false;
    }

    private void CommitPreparedFuelCost()
    {
        preparedFuelCost = 0;
        hasPreparedFuelCost = false;
    }

    private void ApplyPreparedMovementCost(int movementCost)
    {
        if (selectedUnit == null)
        {
            preparedMovementCost = 0;
            hasPreparedMovementCost = false;
            return;
        }

        RestorePreparedMovementCostIfAny();

        int currentRemaining = Mathf.Max(0, selectedUnit.RemainingMovementPoints);
        int clampedCost = Mathf.Clamp(movementCost, 0, currentRemaining);
        if (clampedCost <= 0)
        {
            preparedMovementCost = 0;
            hasPreparedMovementCost = false;
            return;
        }

        selectedUnit.ConsumeMovementPoints(clampedCost);
        preparedMovementCost = clampedCost;
        hasPreparedMovementCost = true;
    }

    private void RestorePreparedMovementCostIfAny()
    {
        if (!hasPreparedMovementCost || preparedMovementCost <= 0 || selectedUnit == null)
        {
            preparedMovementCost = 0;
            hasPreparedMovementCost = false;
            return;
        }

        selectedUnit.SetRemainingMovementPoints(selectedUnit.RemainingMovementPoints + preparedMovementCost);
        preparedMovementCost = 0;
        hasPreparedMovementCost = false;
    }

    private void CommitPreparedMovementCost()
    {
        preparedMovementCost = 0;
        hasPreparedMovementCost = false;
    }

    private bool IsMovementAnimationRunning()
    {
        return (animationManager != null && animationManager.IsAnimatingMovement) || embarkExecutionInProgress || disembarkExecutionInProgress;
    }
}








