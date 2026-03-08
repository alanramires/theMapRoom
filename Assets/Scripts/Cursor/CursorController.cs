using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CursorController : MonoBehaviour
{
    public enum BoundsMode
    {
        PaintedTiles,
        Collider,
        PaintedTilesOrCollider
    }

    [Header("Board")]
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private Collider2D boardCollider;
    [SerializeField] private BoundsMode boundsMode = BoundsMode.PaintedTiles;

    [Header("Movement")]
    [SerializeField] private Vector3Int currentCell = Vector3Int.zero;
    [SerializeField] private float firstRepeatDelay = 0.22f;
    [SerializeField] private float repeatRate = 0.08f;

#if ENABLE_INPUT_SYSTEM
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActionsAsset;
    [SerializeField] private string playerMapName = "Player";
    [SerializeField] private string uiMapName = "UI";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string submitActionName = "Submit";
    [SerializeField] private string cancelActionName = "Cancel";
    [SerializeField] private string cycleActionName = "Next";
    [SerializeField] private string cycleModifierActionName = "Sprint";

    private InputAction moveAction;
    private InputAction submitAction;
    private InputAction cancelAction;
    private InputAction cycleAction;
    private InputAction cycleModifierAction;
#endif

    [Header("Camera Follow")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private bool adjustCameraOnMove = true;
    [Header("Overlay")]
    [SerializeField] [Range(60f, 400f)] private float coordinateOverlayLabelWidth = 220f;

    [Header("Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip moveSfx;
    [SerializeField] private AudioClip confirmSfx;
    [SerializeField] private AudioClip cancelSfx;
    [SerializeField] private AudioClip errorSfx;
    [SerializeField] private AudioClip beepSfx;
    [SerializeField] private AudioClip doneSfx;
    [SerializeField] private AudioClip loadSfx;
    [SerializeField] private AudioClip meleeAttackSfx;
    [SerializeField] private AudioClip rangedAttackSfx;
    [SerializeField] private AudioClip capturingSfx;
    [SerializeField] private AudioClip capturedSfx;
    [SerializeField] private AudioClip explosionSfx;
    [SerializeField] private AudioClip endingTurnSfx;
    [SerializeField] private AudioClip heliceMoveSfx;
    [SerializeField] private AudioClip jatoMoveSfx;
    [SerializeField] private AudioClip marchaMoveSfx;
    [SerializeField] private AudioClip navalMoveSfx;
    [SerializeField] private AudioClip motorMoveSfx;
    [Range(0f, 1f)]
    [SerializeField] private float moveSfxVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float uiSfxVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float unitMoveSfxVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float combatSfxVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float stateSfxVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float explosionSfxVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float endingTurnSfxVolume = 1f;

    [Header("State")]
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private MatchController matchController;
    [SerializeField] private bool enableNeutralLeftClickTeleport = true;

    private Vector3Int heldDirection = Vector3Int.zero;
    private float nextRepeatTime;
    private static int lastConfirmFrameProcessed = -1;
    private static int lastCancelFrameProcessed = -1;
    private bool pendingEndTurnConfirmation;

    public Vector3Int CurrentCell => currentCell;
    public Tilemap BoardTilemap => boardTilemap;
    public float CoordinateOverlayLabelWidth => Mathf.Clamp(coordinateOverlayLabelWidth, 60f, 400f);

    private void Awake()
    {
        TryAutoAssignReferences();
        SnapToCell(currentCell);
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        EnsureInputActionsBound();
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        DisableBoundInputActions();
#endif
        ClearPendingEndTurnConfirmation();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoAssignReferences();
        SnapToCell(currentCell);
    }
#endif

    private void Update()
    {
        TryAutoAssignMatchController();
        if (UiInputBlocker.IsTextInputFocused())
        {
            heldDirection = Vector3Int.zero;
            return;
        }

        if (matchController != null && matchController.IsTurnTransitionInProgress)
        {
            heldDirection = Vector3Int.zero;
            return;
        }

        HandleCycleUnitInput();
        HandleActionInput();
        HandleNeutralLeftClickTeleport();

        if (pendingEndTurnConfirmation)
        {
            heldDirection = Vector3Int.zero;
            return;
        }

        Vector3Int inputDir = ReadDirectionInput();
        if (inputDir == Vector3Int.zero)
        {
            heldDirection = Vector3Int.zero;
            return;
        }

        if (heldDirection != inputDir)
        {
            heldDirection = inputDir;
            nextRepeatTime = Time.unscaledTime + Mathf.Max(0.01f, firstRepeatDelay);
            TryMove(inputDir);
            return;
        }

        if (Time.unscaledTime >= nextRepeatTime)
        {
            TryMove(inputDir);
            nextRepeatTime = Time.unscaledTime + Mathf.Max(0.01f, repeatRate);
        }
    }

    private void HandleCycleUnitInput()
    {
        int direction = 0;
        if (WasCycleForwardPressedThisFrame())
            direction = 1;
        else if (WasCycleBackwardPressedThisFrame())
            direction = -1;

        if (direction == 0)
            return;

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            return;

        if (turnStateManager != null && turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
            return;

        if (!TryCycleToReadyUnit(direction))
        {
            PanelDialogController.TrySetTransientText("Sem unidades prontas", 2.2f);
            PlayUiSfx(errorSfx);
        }
    }

    private bool TryCycleToReadyUnit(int direction)
    {
        TryAutoAssignMatchController();
        int activeTeamId = matchController != null ? matchController.ActiveTeamId : -1;
        if (activeTeamId < 0)
            return false;

        List<UnitManager> candidates = CollectCycleCandidates(activeTeamId);
        if (candidates.Count == 0)
            return false;

        int currentIndex = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3Int cell = candidates[i].CurrentCellPosition;
            cell.z = 0;
            if (cell == currentCell)
            {
                currentIndex = i;
                break;
            }
        }

        int step = direction >= 0 ? 1 : -1;
        int nextIndex = currentIndex < 0
            ? (step > 0 ? 0 : candidates.Count - 1)
            : (currentIndex + step + candidates.Count) % candidates.Count;

        for (int attempts = 0; attempts < candidates.Count; attempts++)
        {
            UnitManager targetUnit = candidates[nextIndex];
            if (targetUnit != null && targetUnit.gameObject.activeInHierarchy && !targetUnit.IsEmbarked && !targetUnit.HasActed)
            {
                Vector3Int cell = targetUnit.CurrentCellPosition;
                cell.z = 0;
                if (SetCell(cell))
                    return true;
            }

            nextIndex = (nextIndex + step + candidates.Count) % candidates.Count;
        }

        return false;
    }

    private List<UnitManager> CollectCycleCandidates(int activeTeamId)
    {
        List<UnitManager> list = new List<UnitManager>();
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;

            if ((int)unit.TeamId != activeTeamId)
                continue;

            if (unit.HasActed)
                continue;

            list.Add(unit);
        }

        list.Sort(CompareCycleCandidate);
        return list;
    }

    private static int CompareCycleCandidate(UnitManager a, UnitManager b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int aId = a.InstanceId;
        int bId = b.InstanceId;
        bool aHasId = aId > 0;
        bool bHasId = bId > 0;
        if (aHasId && bHasId && aId != bId)
            return aId.CompareTo(bId);
        if (aHasId != bHasId)
            return aHasId ? -1 : 1;

        Vector3Int aCell = a.CurrentCellPosition;
        Vector3Int bCell = b.CurrentCellPosition;
        int byY = bCell.y.CompareTo(aCell.y);
        if (byY != 0)
            return byY;

        int byX = aCell.x.CompareTo(bCell.x);
        if (byX != 0)
            return byX;

        return string.CompareOrdinal(a.name, b.name);
    }

    private void HandleNeutralLeftClickTeleport()
    {
        if (!enableNeutralLeftClickTeleport)
            return;

        if (!GetMouseButtonDown(0))
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (turnStateManager == null || turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
            return;

        Camera cam = cameraController != null ? cameraController.GetComponent<Camera>() : Camera.main;
        if (cam == null || boardTilemap == null)
            return;

        Vector3 mouseScreen = GetMousePosition();
        mouseScreen.z = -cam.transform.position.z;
        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);
        Vector3Int targetCell = boardTilemap.WorldToCell(mouseWorld);
        targetCell.z = 0;

        if (!SetCell(targetCell, playMoveSfx: false))
            return;

        PlayMoveSfx();
    }

    public bool TryMove(Vector3Int delta)
    {
        Vector3Int target;
        if (turnStateManager != null)
        {
            bool canMove = turnStateManager.TryResolveCursorMove(currentCell, delta, out target);
            if (!canMove)
                return false;
        }
        else
        {
            target = currentCell + delta;
            target.z = 0;
        }

        return SetCell(target);
    }

    public bool SetCell(Vector3Int cell)
    {
        return SetCell(cell, playMoveSfx: true);
    }

    public bool SetCell(Vector3Int cell, bool playMoveSfx)
    {
        cell.z = 0;
        if (!IsCellValid(cell))
            return false;

        currentCell = cell;
        SnapToCell(currentCell);
        if (playMoveSfx)
            OnCursorMoved();
        if (adjustCameraOnMove)
            TryAdjustCameraToCursor();
        return true;
    }

    [ContextMenu("Snap To Current Cell")]
    public void SnapToCurrentCell()
    {
        SnapToCell(currentCell);
        if (adjustCameraOnMove)
            TryAdjustCameraToCursor();
    }

    private void SnapToCell(Vector3Int cell)
    {
        if (boardTilemap == null)
            return;

        Vector3 center = boardTilemap.GetCellCenterWorld(cell);
        transform.position = new Vector3(center.x, center.y, transform.position.z);
    }

    private bool IsCellValid(Vector3Int cell)
    {
        if (boardTilemap == null)
            return true;

        bool byTiles = boardTilemap.HasTile(cell);
        bool byCollider = false;

        if (boardCollider != null)
        {
            Vector3 center = boardTilemap.GetCellCenterWorld(cell);
            byCollider = boardCollider.OverlapPoint(center);
        }

        switch (boundsMode)
        {
            case BoundsMode.PaintedTiles:
                return byTiles;
            case BoundsMode.Collider:
                return byCollider;
            case BoundsMode.PaintedTilesOrCollider:
                return byTiles || byCollider;
            default:
                return byTiles;
        }
    }

    private Vector3Int ReadDirectionInput()
    {
#if ENABLE_INPUT_SYSTEM
        EnsureInputActionsBound();

        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        if (input.sqrMagnitude > 0.0001f)
        {
            if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
                return input.y > 0f ? new Vector3Int(0, 1, 0) : new Vector3Int(0, -1, 0);

            return input.x > 0f ? new Vector3Int(1, 0, 0) : new Vector3Int(-1, 0, 0);
        }

        if (Keyboard.current.upArrowKey.isPressed)
            return new Vector3Int(0, 1, 0);
        if (Keyboard.current.downArrowKey.isPressed)
            return new Vector3Int(0, -1, 0);
        if (Keyboard.current.leftArrowKey.isPressed)
            return new Vector3Int(-1, 0, 0);
        if (Keyboard.current.rightArrowKey.isPressed)
            return new Vector3Int(1, 0, 0);
#else
        if (Input.GetKey(KeyCode.UpArrow))
            return new Vector3Int(0, 1, 0);
        if (Input.GetKey(KeyCode.DownArrow))
            return new Vector3Int(0, -1, 0);
        if (Input.GetKey(KeyCode.LeftArrow))
            return new Vector3Int(-1, 0, 0);
        if (Input.GetKey(KeyCode.RightArrow))
            return new Vector3Int(1, 0, 0);
#endif

        return Vector3Int.zero;
    }

    private void TryAutoAssignReferences()
    {
        if (boardTilemap == null)
            boardTilemap = GetComponentInParent<Tilemap>();

        if (boardTilemap == null && gameObject.scene.IsValid() && gameObject.scene.isLoaded)
        {
            Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] == null)
                    continue;

                GridLayout.CellLayout layout = maps[i].layoutGrid != null ? maps[i].layoutGrid.cellLayout : GridLayout.CellLayout.Rectangle;
                if (layout == GridLayout.CellLayout.Hexagon)
                {
                    boardTilemap = maps[i];
                    break;
                }
            }
        }

        if (boardCollider == null && boardTilemap != null)
            boardCollider = boardTilemap.GetComponent<Collider2D>();

        if (cameraController == null)
        {
            if (Camera.main != null)
                cameraController = Camera.main.GetComponent<CameraController>();

            if (cameraController == null)
                cameraController = FindAnyObjectByType<CameraController>();
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

#if UNITY_EDITOR
        if (moveSfx == null)
        {
            moveSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/ui/cursor.mp3");
        }

        if (confirmSfx == null)
            confirmSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/ui/confirm.mp3");

        if (cancelSfx == null)
            cancelSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/ui/cancel.mp3");

        if (errorSfx == null)
            errorSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/UI/error.MP3");
        if (beepSfx == null)
            beepSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/UI/beep.MP3");

        if (doneSfx == null)
            doneSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/UI/done.MP3");
        if (loadSfx == null)
            loadSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/UI/load.MP3");
        if (meleeAttackSfx == null)
            meleeAttackSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/combat/melee attack.mp3");
        if (rangedAttackSfx == null)
            rangedAttackSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/combat/ranged attack.mp3");
        if (capturingSfx == null)
            capturingSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/state/capturing.MP3");
        if (capturedSfx == null)
            capturedSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/state/captured.MP3");
        if (explosionSfx == null)
            explosionSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/combat/explosion.MP3");
        if (explosionSfx == null)
            explosionSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/combat/explosion.mp3");
        if (endingTurnSfx == null)
            endingTurnSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/UI/ending the turn.MP3");

        if (heliceMoveSfx == null)
            heliceMoveSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/move/helice.MP3");
        if (jatoMoveSfx == null)
            jatoMoveSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/move/jato.MP3");
        if (marchaMoveSfx == null)
            marchaMoveSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/move/marcha.MP3");
        if (navalMoveSfx == null)
            navalMoveSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/move/naval.MP3");
        if (motorMoveSfx == null)
            motorMoveSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/move/motor.MP3");
#endif

        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        TryAutoAssignMatchController();
    }

    private void TryAutoAssignMatchController()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
    }

    private void TryAdjustCameraToCursor()
    {
        if (cameraController == null)
            return;

        cameraController.AdjustCameraForCursor(transform.position);
    }

    private void OnCursorMoved()
    {
        PlayMoveSfx();
    }

    private void HandleActionInput()
    {
        if (pendingEndTurnConfirmation)
        {
            if (WasConfirmPressedThisFrame())
            {
                ConfirmPendingEndTurn();
                return;
            }

            if (WasCancelPressedThisFrame())
            {
                CancelPendingEndTurn();
                return;
            }

            // Enquanto aguarda confirmacao de fim de turno, bloqueia outros atalhos.
            return;
        }

        if (WasAdvanceTurnPressedThisFrame())
        {
            if (turnStateManager != null && turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
                return;

            pendingEndTurnConfirmation = true;
            PanelDialogController.TrySetExternalText("End Turn :: Confirm");
            PlayBeepSfx();
            return;
        }

        if (WasTeleportToHeadQuarterPressedThisFrame() && TryTeleportToActiveTeamHeadQuarterOnNeutral())
            return;

        if (WasConfirmPressedThisFrame())
        {
            if (lastConfirmFrameProcessed == Time.frameCount)
                return;

            lastConfirmFrameProcessed = Time.frameCount;
            TurnStateManager.ActionSfx feedback = turnStateManager != null
                ? turnStateManager.HandleConfirm()
                : TurnStateManager.ActionSfx.Confirm;
            PlayActionFeedback(feedback);
        }

        if (WasCancelPressedThisFrame())
        {
            if (lastCancelFrameProcessed == Time.frameCount)
                return;

            lastCancelFrameProcessed = Time.frameCount;
            TurnStateManager.ActionSfx feedback = turnStateManager != null
                ? turnStateManager.HandleCancel()
                : TurnStateManager.ActionSfx.Cancel;
            PlayActionFeedback(feedback);
        }
    }

    private void ConfirmPendingEndTurn()
    {
        TryAutoAssignMatchController();
        if (matchController == null)
        {
            ClearPendingEndTurnConfirmation();
            return;
        }

        ClearPendingEndTurnConfirmation();
        matchController.AdvanceTurnWithTransition();
    }

    private void CancelPendingEndTurn()
    {
        ClearPendingEndTurnConfirmation();
        PlayActionFeedback(TurnStateManager.ActionSfx.Cancel);
    }

    private void ClearPendingEndTurnConfirmation()
    {
        if (!pendingEndTurnConfirmation)
            return;

        pendingEndTurnConfirmation = false;
        PanelDialogController.ClearExternalText();
    }

    private bool TryTeleportToActiveTeamHeadQuarterOnNeutral()
    {
        if (turnStateManager == null || turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
            return false;

        TryAutoAssignMatchController();
        int activeTeam = matchController != null ? matchController.ActiveTeamId : -1;
        if (activeTeam < 0)
            return false;

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        ConstructionManager bestHq = null;
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager c = constructions[i];
            if (c == null || !c.gameObject.activeInHierarchy)
                continue;
            if (!IsHeadQuarterConstruction(c))
                continue;
            if ((int)c.TeamId != activeTeam)
                continue;

            if (bestHq == null || c.InstanceId < bestHq.InstanceId)
                bestHq = c;
        }

        if (bestHq == null)
            return false;

        Vector3Int hqCell = bestHq.CurrentCellPosition;
        hqCell.z = 0;
        if (!SetCell(hqCell, playMoveSfx: false))
            return false;

        PlayUiSfx(beepSfx);
        return true;
    }

    private bool HasAnyUnitUnderCursor()
    {
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;

            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            if (cell == currentCell)
                return true;
        }

        return false;
    }

    private static bool IsHeadQuarterConstruction(ConstructionManager construction)
    {
        if (construction == null)
            return false;

        if (construction.IsPlayerHeadQuarter)
            return true;

        string constructionId = construction.ConstructionId;
        if (!string.IsNullOrWhiteSpace(constructionId) &&
            string.Equals(constructionId.Trim(), "hq", System.StringComparison.OrdinalIgnoreCase))
            return true;

        string displayName = construction.ConstructionDisplayName;
        if (!string.IsNullOrWhiteSpace(displayName) &&
            displayName.IndexOf("hq", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    private bool WasConfirmPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        EnsureInputActionsBound();
        if (submitAction != null && submitAction.WasPerformedThisFrame())
            return true;

        return Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    private bool WasCancelPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        EnsureInputActionsBound();
        if (cancelAction != null && cancelAction.WasPerformedThisFrame())
            return true;

        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private bool WasCycleForwardPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        EnsureInputActionsBound();
        if (cycleAction != null && cycleAction.WasPerformedThisFrame())
            return !IsCycleModifierPressed();

        if (Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame)
            return false;

        return !IsCycleModifierPressed();
#else
        return Input.GetKeyDown(KeyCode.Tab) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift);
#endif
    }

    private bool WasCycleBackwardPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        EnsureInputActionsBound();
        if (cycleAction != null && cycleAction.WasPerformedThisFrame())
            return IsCycleModifierPressed();

        if (Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame)
            return false;

        return IsCycleModifierPressed();
#else
        return Input.GetKeyDown(KeyCode.Tab) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
#endif
    }

    private bool WasAdvanceTurnPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    private bool WasTeleportToHeadQuarterPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.homeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Home);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private bool IsCycleModifierPressed()
    {
        if (cycleModifierAction != null)
            return cycleModifierAction.IsPressed();

        return Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
    }

    private void EnsureInputActionsBound()
    {
        if (moveAction != null || submitAction != null || cancelAction != null || cycleAction != null)
            return;

        if (inputActionsAsset == null)
        {
            PlayerInput playerInput = GetComponent<PlayerInput>();
            if (playerInput == null)
                playerInput = FindAnyObjectByType<PlayerInput>();

            if (playerInput != null)
                inputActionsAsset = playerInput.actions;
        }

        if (inputActionsAsset == null)
            return;

        moveAction = inputActionsAsset.FindAction(playerMapName + "/" + moveActionName, false);
        submitAction = inputActionsAsset.FindAction(uiMapName + "/" + submitActionName, false);
        cancelAction = inputActionsAsset.FindAction(uiMapName + "/" + cancelActionName, false);
        cycleAction = inputActionsAsset.FindAction(playerMapName + "/" + cycleActionName, false);
        cycleModifierAction = inputActionsAsset.FindAction(playerMapName + "/" + cycleModifierActionName, false);

        EnableActionIfNeeded(moveAction);
        EnableActionIfNeeded(submitAction);
        EnableActionIfNeeded(cancelAction);
        EnableActionIfNeeded(cycleAction);
        EnableActionIfNeeded(cycleModifierAction);
    }

    private void DisableBoundInputActions()
    {
        DisableActionIfNeeded(moveAction);
        DisableActionIfNeeded(submitAction);
        DisableActionIfNeeded(cancelAction);
        DisableActionIfNeeded(cycleAction);
        DisableActionIfNeeded(cycleModifierAction);

        moveAction = null;
        submitAction = null;
        cancelAction = null;
        cycleAction = null;
        cycleModifierAction = null;
    }

    private static void EnableActionIfNeeded(InputAction action)
    {
        if (action != null && !action.enabled)
            action.Enable();
    }

    private static void DisableActionIfNeeded(InputAction action)
    {
        if (action != null && action.enabled)
            action.Disable();
    }
#endif

    private bool GetMouseButtonDown(int button)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null)
            return false;

        if (button == 0) return Mouse.current.leftButton.wasPressedThisFrame;
        if (button == 1) return Mouse.current.rightButton.wasPressedThisFrame;
        if (button == 2) return Mouse.current.middleButton.wasPressedThisFrame;
        return false;
#else
        return Input.GetMouseButtonDown(button);
#endif
    }

    private Vector3 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    private void PlayMoveSfx()
    {
        if (moveSfx == null)
            return;

        if (audioSource == null)
        {
            AudioSource.PlayClipAtPoint(moveSfx, transform.position, moveSfxVolume);
            return;
        }

        audioSource.PlayOneShot(moveSfx, moveSfxVolume);
    }

    private void PlayUiSfx(AudioClip clip)
    {
        if (clip == null)
            return;

        if (clip == errorSfx && !PanelDialogController.HasActiveExternalText())
            PanelDialogController.TrySetTransientText("Invalid action", 2f);

        if (audioSource == null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, uiSfxVolume);
            return;
        }

        audioSource.PlayOneShot(clip, uiSfxVolume);
    }

    private void PlayActionFeedback(TurnStateManager.ActionSfx feedback)
    {
        switch (feedback)
        {
            case TurnStateManager.ActionSfx.Confirm:
                PlayUiSfx(confirmSfx);
                break;
            case TurnStateManager.ActionSfx.Cancel:
                PlayUiSfx(cancelSfx);
                break;
            case TurnStateManager.ActionSfx.Error:
                PlayUiSfx(errorSfx);
                break;
        }
    }

    public void PlayDoneSfx()
    {
        PlayUiSfx(doneSfx);
    }

    public void PlayLoadSfx()
    {
        PlayUiSfx(loadSfx);
    }

    public void PlayConfirmSfx()
    {
        PlayUiSfx(confirmSfx);
    }

    public void PlayBeepSfx()
    {
        PlayUiSfx(beepSfx);
    }

    public void ClearRuntimeInputLocksAfterLoad()
    {
        heldDirection = Vector3Int.zero;
        nextRepeatTime = 0f;
        ClearPendingEndTurnConfirmation();
#if ENABLE_INPUT_SYSTEM
        EnsureInputActionsBound();
#endif
    }

    public void PlayErrorSfx()
    {
        PlayUiSfx(errorSfx);
    }

    public float PlayCombatAttackSfx(WeaponTrajectoryType trajectory, float volumeScale = 1f)
    {
        AudioClip clip = trajectory == WeaponTrajectoryType.Parabolic ? rangedAttackSfx : meleeAttackSfx;
        if (clip == null)
            return 0f;

        PlayClipWithPitch(clip, Mathf.Clamp01(combatSfxVolume * Mathf.Clamp01(volumeScale)), 1f);
        return clip.length;
    }

    public void PlayWeaponFireSfx(WeaponData weapon, float volumeScale = 1f)
    {
        if (weapon == null || weapon.fireSfx == null)
            return;

        float volume = Mathf.Clamp01(combatSfxVolume * Mathf.Clamp01(volumeScale) * Mathf.Clamp01(weapon.fireSfxVolume));
        PlayClipWithPitch(weapon.fireSfx, volume, 1f);
    }

    public float PlayCapturingSfx(float pitch = 1f, float volumeScale = 1f)
    {
        if (capturingSfx == null)
            return 0f;

        PlayClipWithPitch(capturingSfx, Mathf.Clamp01(stateSfxVolume * Mathf.Clamp01(volumeScale)), pitch);
        return capturingSfx.length;
    }

    public float PlayCapturedSfx(float pitch = 1f, float volumeScale = 1f)
    {
        if (capturedSfx == null)
            return 0f;

        PlayClipWithPitch(capturedSfx, Mathf.Clamp01(stateSfxVolume * Mathf.Clamp01(volumeScale)), pitch);
        return capturedSfx.length;
    }

    public float PlayExplosionSfx(float volumeScale = 1f)
    {
        if (explosionSfx == null)
            return 0f;

        PlayClipWithPitch(explosionSfx, Mathf.Clamp01(explosionSfxVolume * Mathf.Clamp01(volumeScale)), 1f);
        return explosionSfx.length;
    }

    public float PlayEndingTurnSfx(float volumeScale = 1f)
    {
        if (endingTurnSfx == null)
            return 0f;

        PlayClipWithPitch(endingTurnSfx, Mathf.Clamp01(endingTurnSfxVolume * Mathf.Clamp01(volumeScale)), 1f);
        return endingTurnSfx.length;
    }

    public void PlayUnitMovementSfx(MovementCategory category)
    {
        AudioClip clip = GetUnitMovementClipFor(category);
        if (clip == null)
            return;

        if (audioSource == null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, unitMoveSfxVolume);
            return;
        }

        audioSource.PlayOneShot(clip, unitMoveSfxVolume);
    }

    private AudioClip GetUnitMovementClipFor(MovementCategory category)
    {
        switch (category)
        {
            case MovementCategory.Helice:
                return heliceMoveSfx;
            case MovementCategory.Jato:
                return jatoMoveSfx;
            case MovementCategory.Naval:
                return navalMoveSfx;
            case MovementCategory.Motor:
                return motorMoveSfx;
            case MovementCategory.Marcha:
            default:
                return marchaMoveSfx;
        }
    }

    private void PlayClipWithPitch(AudioClip clip, float volume, float pitch)
    {
        if (clip == null)
            return;

        float safeVolume = Mathf.Clamp01(volume);
        if (safeVolume <= 0f)
            return;

        float safePitch = Mathf.Clamp(pitch, 0.1f, 3f);
        if (audioSource == null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, safeVolume);
            return;
        }

        float basePitch = audioSource.pitch;
        audioSource.pitch = safePitch;
        audioSource.PlayOneShot(clip, safeVolume);
        audioSource.pitch = basePitch;
    }

}

