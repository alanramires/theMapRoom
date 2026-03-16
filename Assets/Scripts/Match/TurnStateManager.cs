using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class TurnStateManager : MonoBehaviour
{
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
        Suprindo = 11
    }

    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private PathManager pathManager;
    [SerializeField] private UnitSpawner unitSpawner;
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
    [SerializeField] private CursorState cursorState = CursorState.Neutral;
    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] [Range(0.05f, 1f)] private float movementRangeAlpha = 0.6f;
    [SerializeField] [Range(0.05f, 1f)] private float lineOfFireAlpha = 0.45f;

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
    private bool captureExecutionInProgress;
    private readonly List<UnitManager> turnStartFuelDepletionDeathQueue = new List<UnitManager>();
    private bool turnStartFuelDepletionExecutionInProgress;

    public CursorState CurrentCursorState => cursorState;
    public UnitManager SelectedUnit => selectedUnit;
    public TerrainDatabase TerrainDatabaseRef => terrainDatabase;
    public DPQAirHeightConfig DpqAirHeightConfigRef => dpqAirHeightConfig;
    public bool IsScannerActionExecutionInProgress =>
        embarkExecutionInProgress
        || landingExecutionInProgress
        || combatExecutionInProgress
        || captureExecutionInProgress
        || mergeExecutionInProgress
        || supplyExecutionInProgress
        || transferExecutionInProgress
        || disembarkExecutionInProgress;

    private void LogStateStep(string step, bool rollback = false)
    {
        string rollbackTag = rollback ? " [roll back]" : string.Empty;
        string selectedName = selectedUnit != null ? selectedUnit.name : "(none)";
        Debug.Log($"[TurnState]{rollbackTag} state={cursorState} | step={step} | selected={selectedName}");
    }

    private static void PushPanelUnitMessage(string text, float durationSeconds = 2.8f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        string normalized = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        const int maxLen = 64;
        if (normalized.Length > maxLen)
            normalized = normalized.Substring(0, maxLen - 1).TrimEnd() + "â€¦";

        PanelDialogController.TrySetTransientText(normalized, durationSeconds);
    }

    private void SetCursorState(CursorState nextState, string reason, bool rollback = false)
    {
        CursorState previous = cursorState;
        if (nextState != CursorState.Neutral)
        {
            if (scannerPromptStep == ScannerPromptStep.ThreatLayerTeamSelect)
                scannerPromptStep = ScannerPromptStep.AwaitingAction;
            ClearEnemyThreatLayersOverlay();
        }

        cursorState = nextState;

        string rollbackTag = rollback ? " [roll back]" : string.Empty;
        string selectedName = selectedUnit != null ? selectedUnit.name : "(none)";
        Debug.Log($"[TurnState]{rollbackTag} transition={previous} -> {nextState} | reason={reason} | selected={selectedName}");
    }

    public bool TryFinalizeSelectedUnitActionFromDebug()
    {
        if (selectedUnit == null)
            return false;

        CommitPreparedFuelCost();
        selectedUnit.MarkAsActed();
        ClearSelectionAndReturnToNeutral(keepPreparedFuelCost: true);
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

        if (selectedUnit != null || cursorState != CursorState.Neutral)
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
        message = $"Movimento restante atualizado: {ResolveDebugUnitName(target)} {before}->{target.RemainingMovementPoints}/{max} em {FormatMapCellWithZ(cursorCell)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
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

        message = $"Spawnado: {ResolveDebugUnitDataName(unitData)} em {FormatMapCellWithZ(cursorCell)} para team {TeamUtils.GetName(teamId)}.";
        Debug.Log($"[Debug Command] {message}");
        return true;
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
        TryAutoAssignReferences();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoAssignReferences();
    }
#endif

    public void ForceNeutral()
    {
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
        SetCursorState(CursorState.Neutral, "ClearSelectionAndReturnToNeutral", rollback: !keepPreparedFuelCost);
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
            StartCoroutine(ExecuteTurnStartFuelDepletionDeathQueue());
    }

    private IEnumerator ExecuteTurnStartFuelDepletionDeathQueue()
    {
        if (turnStartFuelDepletionExecutionInProgress)
            yield break;

        turnStartFuelDepletionExecutionInProgress = true;
        try
        {
            float initialDelay = animationManager != null ? animationManager.TurnStartFuelDeathInitialDelay : 0.20f;
            float cursorFocusDelay = animationManager != null ? animationManager.TurnStartFuelDeathCursorFocusDelay : 0.20f;
            float betweenKillsDelay = animationManager != null ? animationManager.TurnStartFuelDeathBetweenKillsDelay : 0.15f;

            if (turnStartFuelDepletionDeathQueue.Count > 0 && initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);

            while (turnStartFuelDepletionDeathQueue.Count > 0)
            {
                UnitManager target = turnStartFuelDepletionDeathQueue[0];
                turnStartFuelDepletionDeathQueue.RemoveAt(0);
                if (target == null || !target.gameObject.activeInHierarchy)
                    continue;

                if (selectedUnit == target)
                    ClearSelectionAndReturnToNeutral();

                Vector3Int targetCell = target.CurrentCellPosition;
                targetCell.z = 0;
                Vector3 worldPos = target.transform.position;

                if (cursorController != null)
                {
                    cursorController.SetCell(targetCell, playMoveSfx: true, adjustCamera: true);
                    if (cursorFocusDelay > 0f)
                        yield return new WaitForSeconds(cursorFocusDelay);
                }

                target.SetCurrentHP(0);
                KillEmbarkedChildrenChain(target);
                PanelDialogController.TrySetTransientText("caiu por falta de combustivel", 2.6f);
                yield return ExecuteUnitDeathPresentation(
                    target,
                    targetCell,
                    worldPos,
                    applyStartDelay: false,
                    moveCursorFirst: false);

                cursorController?.PlayLoadSfx();

                if (turnStartFuelDepletionDeathQueue.Count > 0)
                {
                    if (betweenKillsDelay > 0f)
                        yield return new WaitForSeconds(betweenKillsDelay);
                }
            }
        }
        finally
        {
            turnStartFuelDepletionExecutionInProgress = false;
            if (turnStartFuelDepletionDeathQueue.Count > 0)
                StartCoroutine(ExecuteTurnStartFuelDepletionDeathQueue());
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

