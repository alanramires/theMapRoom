using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

[ExecuteAlways]
public class UnitManager : MonoBehaviour
{
    public static readonly List<UnitManager> AllActive = new List<UnitManager>();

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private UnitHudController unitHud;
    [SerializeField] private SpriteRenderer actedLockRenderer;
    [SerializeField] private UnitDatabase unitDatabase;
    [SerializeField] private MatchController matchController;
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private bool snapToCellCenter = true;
    [SerializeField] private bool autoSnapWhenMovedInEditor = true;
    [SerializeField] private Vector3Int currentCellPosition = Vector3Int.zero;
    [SerializeField] private bool hasActed;
    [SerializeField, HideInInspector] private bool hasFiredThisTurn;
    [SerializeField] private bool receivedSuppliesThisTurn;
    [SerializeField] private TeamId teamId = TeamId.Green;
    [SerializeField] private string unitId;
    [SerializeField] private int instanceId;
    [SerializeField] private Vector3 currentPosition = Vector3.zero;
    [SerializeField] private string unitDisplayName;
    [SerializeField] private int currentHP;
    [SerializeField] private int currentAmmo = 3;
    [SerializeField] private int maxAmmo = 3;
    [SerializeField] private int currentFuel = 99;
    [SerializeField] private int maxFuel = 99;
    [SerializeField, Min(0)] private int remainingMovementPoints;
    [SerializeField, Min(1)] private int visao = 3;
    [Header("Embarked Weapons Runtime")]
    [SerializeField] private List<UnitEmbarkedWeapon> embarkedWeaponsRuntime = new List<UnitEmbarkedWeapon>();
    [Header("Supplier Runtime")]
    [SerializeField] private List<UnitEmbarkedSupply> embarkedResourcesRuntime = new List<UnitEmbarkedSupply>();
    [SerializeField] private List<ServiceData> embarkedServicesRuntime = new List<ServiceData>();
    [SerializeField, HideInInspector] private bool appliedHasActed;
    [SerializeField, HideInInspector] private int appliedActiveTeamId = int.MinValue;
    [SerializeField] private bool isEmbarked;
    [SerializeField] private int embarkedVisualPreviewDepth;
    [SerializeField] private bool isSelected;
    [SerializeField, HideInInspector] private bool isPreviewDimmed;
    [SerializeField, HideInInspector] private bool hasTemporarySortingOverride;
    [SerializeField, HideInInspector] private bool hiddenByFogOfWar;
    [SerializeField, HideInInspector] private int cachedSpriteSortingOrder;
    [SerializeField, HideInInspector] private int cachedActedLockSortingOrder;
    [SerializeField] private bool enableSelectionBlink = true;
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkInterval = 0.16f;
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkActiveDuration = 0.16f;
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkInactiveDuration = 0.16f;
    [SerializeField] [Range(0f, 1f)] private float actedDarkenFactor = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float actedGrayBlend = 0.6f;
    [SerializeField] [Range(0f, 1f)] private float previewDimDarkenFactor = 0.55f;
    [SerializeField] [Range(0f, 1f)] private float previewDimGrayBlend = 0.75f;
    [SerializeField] private Color actedGlowColor = Color.white;
    [SerializeField] [Range(0.1f, 6f)] private float actedGlowSize = 1.5f;
    [SerializeField] [Range(0f, 4f)] private float actedGlowStrength = 1.25f;
    [Header("Layer State")]
    [SerializeField] private Domain currentDomain = Domain.Land;
    [SerializeField] private HeightLevel currentHeightLevel = HeightLevel.Surface;
    [SerializeField] private int currentLayerModeIndex = 0;
    [SerializeField] private bool layerStateInitialized;
    [SerializeField] private bool useExplicitPreferredAirHeightRuntime;
    [SerializeField] private HeightLevel preferredAirHeightRuntime = HeightLevel.AirLow;
    [SerializeField] private bool useExplicitPreferredNavalHeightRuntime;
    [SerializeField] private HeightLevel preferredNavalHeightRuntime = HeightLevel.Submerged;
    [Header("Forced Layer Lock")]
    [SerializeField] private bool hasForcedLayerLock;
    [SerializeField] private Domain forcedLayerLockDomain = Domain.Land;
    [SerializeField] private HeightLevel forcedLayerLockHeight = HeightLevel.Surface;
    [SerializeField, Min(0)] private int forcedLayerLockTurnsRemaining;
    [Header("Transport Runtime")]
    [SerializeField] private List<UnitTransportSeatRuntime> transportedUnitSlots = new List<UnitTransportSeatRuntime>();
    [SerializeField, HideInInspector] private UnitManager embarkedTransporter;
    [SerializeField, HideInInspector] private int embarkedTransporterSlotIndex = -1;
    [Header("Stealth Runtime")]
    [SerializeField, HideInInspector] private List<int> currentlyObservedByTeamIds = new List<int>();

    public TeamId TeamId => teamId;
    public Tilemap BoardTilemap => boardTilemap;
    public Vector3Int CurrentCellPosition => currentCellPosition;
    public string UnitId => unitId;
    public int InstanceId => instanceId;
    public Vector3 CurrentPosition => currentPosition;
    public string UnitDisplayName => unitDisplayName;
    public int CurrentHP => currentHP;
    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public int CurrentFuel => currentFuel;
    public int MaxFuel => maxFuel;
    public int MaxMovementPoints => Mathf.Max(0, GetMovementRange());
    public int RemainingMovementPoints => Mathf.Clamp(remainingMovementPoints, 0, MaxMovementPoints);
    public int Visao => Mathf.Max(1, visao);
    public bool HasActed => hasActed;
    public bool HasFiredThisTurn => hasFiredThisTurn;
    public bool ReceivedSuppliesThisTurn => receivedSuppliesThisTurn;
    public bool IsEmbarked => isEmbarked;
    public bool IsEmbarkedVisualPreviewActive => embarkedVisualPreviewDepth > 0;
    public bool IsSelected => isSelected;
    public UnitDatabase UnitDatabase => unitDatabase;
    public bool IsAircraftGrounded => GetAircraftType() != AircraftType.None && currentDomain != Domain.Air;
    public bool IsAircraftEmbarkedInCarrier => isEmbarked;
    public int AircraftOperationLockTurns => Mathf.Max(0, forcedLayerLockTurnsRemaining);
    public bool HasForcedLayerLock => hasForcedLayerLock && forcedLayerLockTurnsRemaining > 0;
    public Domain ForcedLayerLockDomain => forcedLayerLockDomain;
    public HeightLevel ForcedLayerLockHeight => forcedLayerLockHeight;
    public int ForcedLayerLockTurnsRemaining => Mathf.Max(0, forcedLayerLockTurnsRemaining);
    public IReadOnlyList<UnitTransportSeatRuntime> TransportedUnitSlots => transportedUnitSlots;
    public UnitManager EmbarkedTransporter => embarkedTransporter;
    public int EmbarkedTransporterSlotIndex => embarkedTransporterSlotIndex;
    public IReadOnlyList<int> CurrentlyObservedByTeamIds => currentlyObservedByTeamIds;

    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowSizeId = Shader.PropertyToID("_GlowSize");
    private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");

    private Material defaultSpriteMaterial;
    private MaterialPropertyBlock spritePropertyBlock;
    private static Material actedGlowMaterial;
    private Coroutine selectionBlinkRoutine;

    private void Awake()
    {
        EnsureDefaults();
        TryAutoAssignHud();
        TryAutoAssignLockRenderer();
        TryAutoAssignBoardTilemap();
        DisableLegacyOutlineObjects();
        CacheSpriteMaterial();
        SyncPositionState();
        appliedHasActed = hasActed;
        appliedActiveTeamId = matchController != null ? matchController.ActiveTeamId : int.MinValue;
        RefreshActedVisual();
    }

    private void LateUpdate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || !autoSnapWhenMovedInEditor)
            return;

        if (boardTilemap == null)
            TryAutoAssignBoardTilemap();

        if (boardTilemap == null || !transform.hasChanged)
            return;

        transform.hasChanged = false;
        PullCellFromTransform();
        SnapToCellCenter();
#endif
    }

    private void Start()
    {
        TryAutoAssignMatchController();
        appliedHasActed = hasActed;
        appliedActiveTeamId = matchController != null ? matchController.ActiveTeamId : int.MinValue;
        RefreshActedVisual();
        RefreshDetectedIndicator();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && !AllActive.Contains(this))
            AllActive.Add(this);
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
        MatchController.OnUnitActedStateChanged += HandleUnitActedStateChanged;
        MatchController.OnFogOfWarUpdated += HandleFogOfWarUpdated;
        if (Application.isPlaying)
        {
            Vector3Int cell = currentCellPosition;
            cell.z = 0;
            UnitOccupancyRules.NotifyUnitOccupancyChanged(this, cell, cell);
            RefreshDetectedIndicator();
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            Vector3Int cell = currentCellPosition;
            cell.z = 0;
            UnitOccupancyRules.NotifyUnitOccupancyChanged(this, cell, cell);
        }
        AllActive.Remove(this);
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
        MatchController.OnUnitActedStateChanged -= HandleUnitActedStateChanged;
        MatchController.OnFogOfWarUpdated -= HandleFogOfWarUpdated;
        ThreatRevisionTracker.NotifyUnitDisabled(this, teamId, isEmbarked);
        StopSelectionBlinkRoutine();
        ClearTemporarySortingOrder();
        SetSpriteVisible(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        TryAutoAssignHud();
        TryAutoAssignLockRenderer();
        EnsureDefaults();
        TryAutoAssignBoardTilemap();
        TryAutoAssignMatchController();
        DisableLegacyOutlineObjects();
        CacheSpriteMaterial();

        if (IsEditingPrefabContext())
            return;

        SyncPositionState();
        UpdateDynamicName();

        RefreshActedVisual();
    }
#endif

    public void Setup(UnitDatabase database, string id)
    {
        unitDatabase = database;
        unitId = id;
        EnsureDefaults();
        UpdateDynamicName();
    }

    public bool ApplyFromDatabase()
    {
        if (unitDatabase == null || string.IsNullOrWhiteSpace(unitId))
            return false;

        if (!unitDatabase.TryGetById(unitId, out UnitData data))
            return false;

        Apply(data);
        return true;
    }

    public void Apply(UnitData data)
    {
        if (data == null)
            return;

        unitId = data.id;
        unitDisplayName = string.IsNullOrWhiteSpace(data.displayName) ? data.id : data.displayName;

        if (currentHP <= 0 || currentHP > data.maxHP)
            currentHP = data.maxHP;

        maxFuel = Mathf.Max(1, data.autonomia);
        visao = Mathf.Max(1, data.visao);
        currentAmmo = Mathf.Clamp(currentAmmo, 0, GetMaxAmmo());
        currentFuel = Mathf.Clamp(currentFuel, 0, GetMaxFuel());
        if (!hasActed)
            remainingMovementPoints = Mathf.Max(0, data.movement);
        else
            remainingMovementPoints = Mathf.Clamp(remainingMovementPoints, 0, Mathf.Max(0, data.movement));
        SyncEmbarkedWeaponsFromData(data);
        SyncSupplierRuntimeFromData(data);
        SyncTransportRuntimeSlotsWithData(data);
        SyncCurrentLayerStateWithData(data, forceNativeDefault: true);
        SyncPreferredLayerPreferencesFromData(data);
        RefreshSpriteForCurrentLayer(data);

        currentPosition = transform.position;
        UpdateDynamicName();
        RefreshActedVisual();
        ThreatRevisionTracker.NotifyUnitDataApplied(this);
    }

    public void SetAutonomia(int autonomiaMax, bool refillCurrentFuel)
    {
        maxFuel = Mathf.Max(1, autonomiaMax);
        currentFuel = refillCurrentFuel ? maxFuel : Mathf.Clamp(currentFuel, 0, maxFuel);
        RefreshActedVisual();
    }

    public void SetCurrentHP(int value)
    {
        int max = GetMaxHP();
        currentHP = Mathf.Clamp(value, 0, max);
        RefreshActedVisual();
    }

    public void SetCurrentAmmo(int value)
    {
        currentAmmo = Mathf.Clamp(value, 0, GetMaxAmmo());
        RefreshActedVisual();
    }

    public void SetCurrentFuel(int value)
    {
        currentFuel = Mathf.Clamp(value, 0, GetMaxFuel());
        RefreshActedVisual();
    }

    public SpriteRenderer GetMainSpriteRenderer()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        return spriteRenderer;
    }

    public void SetTemporarySortingOrder(int forcedSortingOrder = 999)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer == null)
            return;

        if (!hasTemporarySortingOverride)
        {
            cachedSpriteSortingOrder = spriteRenderer.sortingOrder;
            cachedActedLockSortingOrder = actedLockRenderer != null ? actedLockRenderer.sortingOrder : 0;
            hasTemporarySortingOverride = true;
        }

        spriteRenderer.sortingOrder = forcedSortingOrder;
        if (actedLockRenderer != null)
            actedLockRenderer.sortingOrder = forcedSortingOrder;
    }

    public void ClearTemporarySortingOrder()
    {
        if (!hasTemporarySortingOverride)
            return;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = cachedSpriteSortingOrder;

        if (actedLockRenderer != null)
            actedLockRenderer.sortingOrder = cachedActedLockSortingOrder;

        hasTemporarySortingOverride = false;
    }

    public void MarkAsActed()
    {
        hasActed = true;
        appliedHasActed = hasActed;
        RefreshActedVisual();
        TryAutoAssignMatchController();
        matchController?.NotifyUnitReachedHasAct(this);
    }

    public void MarkAsFired()
    {
        hasFiredThisTurn = true;
    }

    public void SetFogOfWarVisibility(bool visible)
    {
        bool shouldHide = !visible;
        if (hiddenByFogOfWar == shouldHide)
            return;

        hiddenByFogOfWar = shouldHide;
        ApplyFogOfWarVisibility();
    }

    public void ResetActed()
    {
        hasActed = false;
        hasFiredThisTurn = false;
        ResetRemainingMovement();
        appliedHasActed = hasActed;
        RefreshActedVisual();
    }

    public void SetRemainingMovementPoints(int value)
    {
        remainingMovementPoints = Mathf.Clamp(value, 0, GetMovementRange());
        RefreshActedVisual();
    }

    public void ConsumeMovementPoints(int movementCost)
    {
        int clampedCost = Mathf.Max(0, movementCost);
        int maxMovement = Mathf.Max(0, GetMovementRange());
        int currentRemaining = Mathf.Clamp(remainingMovementPoints, 0, maxMovement);
        remainingMovementPoints = Mathf.Clamp(currentRemaining - clampedCost, 0, maxMovement);
        RefreshActedVisual();
    }

    public void ResetRemainingMovement()
    {
        remainingMovementPoints = Mathf.Max(0, GetMovementRange());
        RefreshActedVisual();
    }

    public void MarkReceivedSuppliesThisTurn()
    {
        SetReceivedSuppliesThisTurn(true);
    }

    public void ClearReceivedSuppliesThisTurn()
    {
        SetReceivedSuppliesThisTurn(false);
    }

    public void RefreshRuntimeVisualState()
    {
        RefreshActedVisual();
    }

    public void RegisterStealthReveal(int detectorTeamId)
    {
        AddCurrentlyObservedByTeam(detectorTeamId);
    }

    public bool IsStealthRevealedForTeam(int viewerTeamId, int currentTurn)
    {
        if (currentlyObservedByTeamIds == null || currentlyObservedByTeamIds.Count <= 0)
            return false;

        return currentlyObservedByTeamIds.Contains(viewerTeamId);
    }

    public void ClearStealthRevealState()
    {
        if (currentlyObservedByTeamIds != null)
            currentlyObservedByTeamIds.Clear();
        RefreshDetectedIndicator();
    }

    public bool AddCurrentlyObservedByTeam(int teamId)
    {
        if (currentlyObservedByTeamIds == null)
            currentlyObservedByTeamIds = new List<int>();
        if (teamId < -1 || teamId > 3)
            return false;
        if (currentlyObservedByTeamIds.Contains(teamId))
            return false;

        currentlyObservedByTeamIds.Add(teamId);
        RefreshDetectedIndicator();
        return true;
    }

    public bool RemoveCurrentlyObservedByTeam(int teamId)
    {
        if (currentlyObservedByTeamIds == null || currentlyObservedByTeamIds.Count <= 0)
            return false;

        bool removed = currentlyObservedByTeamIds.Remove(teamId);
        if (removed)
            RefreshDetectedIndicator();
        return removed;
    }

    public bool SyncCurrentlyObservedByTeams(IEnumerable<int> teamIds)
    {
        if (currentlyObservedByTeamIds == null)
            currentlyObservedByTeamIds = new List<int>();

        HashSet<int> desired = new HashSet<int>();
        if (teamIds != null)
        {
            foreach (int teamId in teamIds)
            {
                if (teamId < -1 || teamId > 3)
                    continue;
                desired.Add(teamId);
            }
        }

        bool changed = false;
        for (int i = currentlyObservedByTeamIds.Count - 1; i >= 0; i--)
        {
            int teamId = currentlyObservedByTeamIds[i];
            if (desired.Contains(teamId))
                continue;

            currentlyObservedByTeamIds.RemoveAt(i);
            changed = true;
        }

        foreach (int teamId in desired)
        {
            if (currentlyObservedByTeamIds.Contains(teamId))
                continue;

            currentlyObservedByTeamIds.Add(teamId);
            changed = true;
        }

        if (changed)
            RefreshDetectedIndicator();
        return changed;
    }

    public bool ClearCurrentlyObservedByTeams()
    {
        if (currentlyObservedByTeamIds == null || currentlyObservedByTeamIds.Count <= 0)
            return false;

        currentlyObservedByTeamIds.Clear();
        RefreshDetectedIndicator();
        return true;
    }

    public void SetReceivedSuppliesThisTurn(bool value)
    {
        if (receivedSuppliesThisTurn == value)
            return;

        receivedSuppliesThisTurn = value;
        UpdateDynamicName();
    }

    public void SetSelected(bool selected)
    {
        if (isSelected == selected)
            return;

        isSelected = selected;
        if (isSelected)
            SetTemporarySortingOrder();
        else
            ClearTemporarySortingOrder();
        RefreshSelectionVisual();
    }

    public void SetPreviewDimmed(bool dimmed)
    {
        if (isPreviewDimmed == dimmed)
            return;

        isPreviewDimmed = dimmed;
        RefreshActedVisual();
    }

    public void SetSelectionBlinkInterval(float interval)
    {
        float clamped = Mathf.Clamp(interval, 0.05f, 1f);
        selectionBlinkInterval = clamped;
        selectionBlinkActiveDuration = clamped;
        selectionBlinkInactiveDuration = clamped;
    }

    public void SetSelectionBlinkDurations(float activeDuration, float inactiveDuration)
    {
        selectionBlinkActiveDuration = Mathf.Clamp(activeDuration, 0.05f, 1f);
        selectionBlinkInactiveDuration = Mathf.Clamp(inactiveDuration, 0.05f, 1f);
    }

    public int GetMaxHP()
    {
        if (unitDatabase != null && !string.IsNullOrWhiteSpace(unitId) && unitDatabase.TryGetById(unitId, out UnitData data))
            return Mathf.Max(1, data.maxHP);

        return Mathf.Max(1, currentHP);
    }

    public int GetMaxAmmo()
    {
        return Mathf.Max(1, maxAmmo);
    }

    public int GetMaxFuel()
    {
        return Mathf.Max(1, maxFuel);
    }

    public int GetMovementRange()
    {
        if (unitDatabase != null && !string.IsNullOrWhiteSpace(unitId) && unitDatabase.TryGetById(unitId, out UnitData data))
            return Mathf.Max(0, data.movement);

        return 0;
    }

    public Domain GetDomain()
    {
        return currentDomain;
    }

    public IReadOnlyList<UnitLayerMode> GetAllLayerModes()
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        return modes;
    }

    public IReadOnlyList<UnitLayerMode> GetAdditionalLayerModes()
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        if (modes.Length <= 1)
            return System.Array.Empty<UnitLayerMode>();

        UnitLayerMode[] additional = new UnitLayerMode[modes.Length - 1];
        for (int i = 1; i < modes.Length; i++)
            additional[i - 1] = modes[i];
        return additional;
    }

    public UnitLayerMode GetCurrentLayerMode()
    {
        return new UnitLayerMode(currentDomain, currentHeightLevel);
    }

    public bool TrySetCurrentLayerMode(int index)
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        if (modes.Length == 0)
            return false;
        if (index < 0 || index >= modes.Length)
            return false;

        SetCurrentLayerState(index, modes[index]);
        return true;
    }

    public bool TrySetCurrentLayerMode(Domain domain, HeightLevel heightLevel)
    {
        if (IsLayerChangeBlockedByForcedLock(domain, heightLevel, out _))
            return false;

        Domain previousDomain = currentDomain;
        HeightLevel previousHeight = currentHeightLevel;
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i].domain == domain && modes[i].heightLevel == heightLevel)
            {
                SetCurrentLayerState(i, modes[i]);
                ThreatRevisionTracker.NotifyUnitLayerChanged(this, previousDomain, previousHeight, currentDomain, currentHeightLevel);
                return true;
            }
        }

        return false;
    }

    public bool SupportsLayerMode(Domain domain, HeightLevel heightLevel)
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i].domain == domain && modes[i].heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    public bool TryGetForcedLayerLock(out Domain domain, out HeightLevel heightLevel, out int turnsRemaining)
    {
        if (!HasForcedLayerLock)
        {
            domain = currentDomain;
            heightLevel = currentHeightLevel;
            turnsRemaining = 0;
            return false;
        }

        domain = forcedLayerLockDomain;
        heightLevel = forcedLayerLockHeight;
        turnsRemaining = Mathf.Max(0, forcedLayerLockTurnsRemaining);
        return true;
    }

    public bool IsLayerChangeBlockedByForcedLock(Domain targetDomain, HeightLevel targetHeightLevel, out string reason)
    {
        if (!HasForcedLayerLock)
        {
            reason = string.Empty;
            return false;
        }

        bool sameLockedLayer = forcedLayerLockDomain == targetDomain && forcedLayerLockHeight == targetHeightLevel;
        if (sameLockedLayer)
        {
            reason = string.Empty;
            return false;
        }

        reason = PanelDialogController.ResolveDialogMessage(
            "layer.locked.by.weapon",
            "Camada travada em <domain>/<height> por <turns> turno(s).",
            new Dictionary<string, string>
            {
                { "unit", ResolveRuntimeUnitName() },
                { "domain", forcedLayerLockDomain.ToString() },
                { "height", forcedLayerLockHeight.ToString() },
                { "turns", forcedLayerLockTurnsRemaining.ToString() }
            });
        return true;
    }

    public void SetForcedLayerLock(Domain domain, HeightLevel heightLevel, int turns)
    {
        hasForcedLayerLock = true;
        forcedLayerLockDomain = domain;
        forcedLayerLockHeight = heightLevel;
        forcedLayerLockTurnsRemaining = Mathf.Max(1, turns);
    }

    public void ClearForcedLayerLock()
    {
        hasForcedLayerLock = false;
        forcedLayerLockTurnsRemaining = 0;
    }

    public void ConsumeForcedLayerLockTurn()
    {
        if (!HasForcedLayerLock)
            return;

        forcedLayerLockTurnsRemaining = Mathf.Max(0, forcedLayerLockTurnsRemaining - 1);
        if (forcedLayerLockTurnsRemaining <= 0)
            ClearForcedLayerLock();
    }

    // Debug utility: allows forcing a runtime layer state even when that exact
    // mode is not declared on UnitData (useful for gameplay investigation).
    public bool ForceLayerStateForDebug(Domain domain, HeightLevel heightLevel)
    {
        currentDomain = domain;
        currentHeightLevel = heightLevel;
        layerStateInitialized = true;

        currentLayerModeIndex = ResolveLayerModeIndex(domain, heightLevel);
        SyncAircraftRuntimeStateWithCurrentLayer();
        RefreshSpriteForCurrentLayer();
        RefreshActedVisual();
        return true;
    }

    // Debug step order used by editor buttons while playing:
    // Land/Surface -> Air/Low -> Air/High (up) and reverse (down).
    public bool TryStepLayerStateForDebug(int delta)
    {
        if (delta == 0)
            return false;

        Domain targetDomain = currentDomain;
        HeightLevel targetHeight = currentHeightLevel;

        if (delta < 0)
        {
            if (currentDomain == Domain.Air && currentHeightLevel == HeightLevel.AirHigh)
            {
                targetDomain = Domain.Air;
                targetHeight = HeightLevel.AirLow;
            }
            else if (currentDomain == Domain.Air && currentHeightLevel == HeightLevel.AirLow)
            {
                targetDomain = Domain.Land;
                targetHeight = HeightLevel.Surface;
            }
            else
            {
                return false;
            }
        }
        else
        {
            if (currentDomain != Domain.Air)
            {
                targetDomain = Domain.Air;
                targetHeight = HeightLevel.AirLow;
            }
            else if (currentHeightLevel == HeightLevel.AirLow)
            {
                targetDomain = Domain.Air;
                targetHeight = HeightLevel.AirHigh;
            }
            else
            {
                return false;
            }
        }

        return ForceLayerStateForDebug(targetDomain, targetHeight);
    }

    public MovementCategory GetMovementCategory()
    {
        if (unitDatabase != null && !string.IsNullOrWhiteSpace(unitId) && unitDatabase.TryGetById(unitId, out UnitData data))
            return data.movementCategory;

        return MovementCategory.Marcha;
    }

    public HeightLevel GetHeightLevel()
    {
        return currentHeightLevel;
    }

    public bool HasSkill(SkillData skill)
    {
        if (skill == null)
            return false;

        UnitData data = TryGetUnitData();
        if (data == null || data.skills == null)
            return false;

        if (data.skills.Contains(skill))
            return true;

        string requestedId = string.IsNullOrWhiteSpace(skill.id) ? string.Empty : skill.id.Trim();
        if (requestedId.Length == 0)
            return false;

        for (int i = 0; i < data.skills.Count; i++)
        {
            SkillData ownedSkill = data.skills[i];
            if (ownedSkill == null || string.IsNullOrWhiteSpace(ownedSkill.id))
                continue;

            if (ownedSkill.id.Trim() == requestedId)
                return true;
        }

        return false;
    }

    public bool TryGetUnitData(out UnitData data)
    {
        data = TryGetUnitData();
        return data != null;
    }

    public AircraftType GetAircraftType()
    {
        UnitData data = TryGetUnitData();
        if (data == null)
            return AircraftType.None;

        if (data.unitClass == GameUnitClass.Helicopter)
            return AircraftType.Helicopter;
        if (data.unitClass == GameUnitClass.Jet || data.unitClass == GameUnitClass.Plane)
            return AircraftType.FixedWing;
        return AircraftType.None;
    }

    public HeightLevel GetPreferredAirHeight()
    {
        if (useExplicitPreferredAirHeightRuntime)
            return preferredAirHeightRuntime == HeightLevel.AirHigh ? HeightLevel.AirHigh : HeightLevel.AirLow;

        UnitData data = TryGetUnitData();
        if (data == null)
            return HeightLevel.AirLow;

        if (data.domain == Domain.Air && (data.heightLevel == HeightLevel.AirLow || data.heightLevel == HeightLevel.AirHigh))
            return data.heightLevel;

        if (data.aditionalDomainsAllowed != null)
        {
            for (int i = 0; i < data.aditionalDomainsAllowed.Count; i++)
            {
                UnitLayerMode mode = data.aditionalDomainsAllowed[i];
                if (mode.domain == Domain.Air && (mode.heightLevel == HeightLevel.AirLow || mode.heightLevel == HeightLevel.AirHigh))
                    return mode.heightLevel;
            }
        }

        return HeightLevel.AirLow;
    }

    public bool TryGetPreferredNavalLayerMode(out Domain domain, out HeightLevel heightLevel)
    {
        domain = Domain.Naval;
        heightLevel = HeightLevel.Surface;

        if (!useExplicitPreferredNavalHeightRuntime)
            return false;

        heightLevel = preferredNavalHeightRuntime == HeightLevel.Submerged
            ? HeightLevel.Submerged
            : HeightLevel.Surface;
        domain = heightLevel == HeightLevel.Submerged ? Domain.Submarine : Domain.Naval;
        return true;
    }

    public bool HasSkillId(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return false;

        UnitData data = TryGetUnitData();
        if (data == null || data.skills == null)
            return false;

        string normalized = skillId.Trim();
        for (int i = 0; i < data.skills.Count; i++)
        {
            SkillData owned = data.skills[i];
            if (owned == null || string.IsNullOrWhiteSpace(owned.id))
                continue;

            if (string.Equals(owned.id.Trim(), normalized, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public void SetAircraftGrounded(bool grounded)
    {
        if (grounded)
        {
            if (currentDomain == Domain.Air || currentHeightLevel != HeightLevel.Surface)
                TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface);
            return;
        }

        if (currentDomain != Domain.Air)
            TrySetCurrentLayerMode(Domain.Air, GetPreferredAirHeight());
    }

    public void SetAircraftEmbarkedInCarrier(bool embarkedInCarrier)
    {
        SetEmbarked(embarkedInCarrier);
    }

    public void SetAircraftOperationLockTurns(int turns)
    {
        if (turns <= 0)
        {
            ClearForcedLayerLock();
            return;
        }

        SetForcedLayerLock(currentDomain, currentHeightLevel, turns);
    }

    private string ResolveRuntimeUnitName()
    {
        if (!string.IsNullOrWhiteSpace(unitDisplayName))
            return unitDisplayName;
        if (!string.IsNullOrWhiteSpace(unitId))
            return unitId;
        return name;
    }

    public IReadOnlyList<UnitEmbarkedWeapon> GetEmbarkedWeapons()
    {
        return embarkedWeaponsRuntime;
    }

    public IReadOnlyList<UnitEmbarkedSupply> GetEmbarkedResources()
    {
        return embarkedResourcesRuntime;
    }

    public IReadOnlyList<ServiceData> GetEmbarkedServices()
    {
        return embarkedServicesRuntime;
    }

    public bool TryConsumeEmbarkedWeaponAmmo(int embarkedWeaponIndex, int amount = 1)
    {
        if (amount <= 0)
            amount = 1;

        if (embarkedWeaponIndex < 0 || embarkedWeaponIndex >= embarkedWeaponsRuntime.Count)
            return false;

        UnitEmbarkedWeapon embarked = embarkedWeaponsRuntime[embarkedWeaponIndex];
        if (embarked == null || embarked.squadAmmunition < amount)
            return false;

        embarked.squadAmmunition -= amount;
        RefreshActedVisual();
        return true;
    }

    public int GetOccupiedTransportSeatCountForSlot(int slotIndex)
    {
        UnitData data = TryGetUnitData();
        SyncTransportRuntimeSlotsWithData(data);

        int count = 0;
        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.slotIndex != slotIndex || seat.embarkedUnit == null)
                continue;

            if (!seat.embarkedUnit.IsEmbarked)
            {
                seat.embarkedUnit = null;
                continue;
            }

            count++;
        }

        return count;
    }

    public int GetCapacityForTransportSlot(int slotIndex)
    {
        UnitData data = TryGetUnitData();
        if (data == null || data.transportSlots == null || slotIndex < 0 || slotIndex >= data.transportSlots.Count)
            return 0;

        return Mathf.Max(1, data.transportSlots[slotIndex].capacity);
    }

    public bool TryEmbarkPassengerInSlot(UnitManager passenger, int slotIndex, out string reason)
    {
        reason = string.Empty;
        if (passenger == null)
        {
            reason = "Passageiro invalido.";
            return false;
        }

        if (passenger == this)
        {
            reason = "Unidade nao pode embarcar em si mesma.";
            return false;
        }

        if (!TryGetUnitData(out UnitData data) || data == null || !data.isTransporter)
        {
            reason = "Unidade alvo nao eh transportadora.";
            return false;
        }

        if (data.transportSlots == null || slotIndex < 0 || slotIndex >= data.transportSlots.Count)
        {
            reason = "Slot de transporte invalido.";
            return false;
        }

        SyncTransportRuntimeSlotsWithData(data);
        UnitTransportSeatRuntime freeSeat = FindFirstFreeSeat(slotIndex);
        if (freeSeat == null)
        {
            reason = "Slot lotado.";
            return false;
        }

        UnitManager currentTransporter = passenger.EmbarkedTransporter;
        if (passenger.IsEmbarked && currentTransporter != null)
        {
            if (currentTransporter != this)
                currentTransporter.RemoveEmbarkedPassenger(passenger);
            else
                RemoveEmbarkedPassenger(passenger);
        }

        Vector3Int transporterCell = currentCellPosition;
        transporterCell.z = 0;
        passenger.SetCurrentCellPosition(transporterCell, enforceFinalOccupancyRule: false);
        passenger.AssignEmbarkTransport(this, slotIndex);
        if (!passenger.IsEmbarked)
            passenger.SetEmbarked(true);
        else
            passenger.SyncHierarchyForEmbarkedState();
        // Defensive refresh after reparent to transporter hierarchy:
        // guarantees embarked passenger visuals/HUD remain hidden.
        passenger.RefreshRuntimeVisualState();
        freeSeat.embarkedUnit = passenger;
        RefreshSpriteForCurrentLayer(data);
        RefreshActedVisual();
        return true;
    }

    public bool TryEmbarkPassengerInSeat(UnitManager passenger, int slotIndex, int seatIndex, out string reason)
    {
        reason = string.Empty;
        if (!TryGetUnitData(out UnitData data) || data == null || !data.isTransporter)
        {
            reason = "Unidade alvo nao eh transportadora.";
            return false;
        }

        if (passenger == null)
        {
            reason = "Passageiro invalido.";
            return false;
        }

        if (passenger == this)
        {
            reason = "Unidade nao pode embarcar em si mesma.";
            return false;
        }

        if (data.transportSlots == null || slotIndex < 0 || slotIndex >= data.transportSlots.Count)
        {
            reason = "Slot de transporte invalido.";
            return false;
        }

        SyncTransportRuntimeSlotsWithData(data);
        UnitTransportSeatRuntime targetSeat = FindSeat(slotIndex, seatIndex);
        if (targetSeat == null)
        {
            reason = "Vaga de transporte invalida.";
            return false;
        }

        if (targetSeat.embarkedUnit != null && targetSeat.embarkedUnit != passenger)
        {
            reason = "Vaga ocupada.";
            return false;
        }

        UnitManager currentTransporter = passenger.EmbarkedTransporter;
        if (passenger.IsEmbarked && currentTransporter != null)
        {
            if (currentTransporter != this)
                currentTransporter.RemoveEmbarkedPassenger(passenger);
            else
                RemoveEmbarkedPassenger(passenger);
        }

        Vector3Int transporterCell = currentCellPosition;
        transporterCell.z = 0;
        passenger.SetCurrentCellPosition(transporterCell, enforceFinalOccupancyRule: false);
        passenger.AssignEmbarkTransport(this, slotIndex);
        if (!passenger.IsEmbarked)
            passenger.SetEmbarked(true);
        else
            passenger.SyncHierarchyForEmbarkedState();
        // Defensive refresh after reparent to transporter hierarchy:
        // guarantees embarked passenger visuals/HUD remain hidden.
        passenger.RefreshRuntimeVisualState();
        targetSeat.embarkedUnit = passenger;
        RefreshSpriteForCurrentLayer(data);
        RefreshActedVisual();
        return true;
    }

    public bool TryDisembarkPassengerFromSeat(int slotIndex, int seatIndex, out UnitManager passenger, out string reason)
    {
        passenger = null;
        reason = string.Empty;

        UnitData data = TryGetUnitData();
        SyncTransportRuntimeSlotsWithData(data);
        UnitTransportSeatRuntime seat = FindSeat(slotIndex, seatIndex);
        if (seat == null)
        {
            reason = "Vaga de transporte invalida.";
            return false;
        }

        passenger = seat.embarkedUnit;
        if (passenger == null)
        {
            reason = "Vaga ja esta livre.";
            return false;
        }

        seat.embarkedUnit = null;
        passenger.SetEmbarked(false);
        RefreshSpriteForCurrentLayer();
        RefreshActedVisual();
        return true;
    }

    public bool RemoveEmbarkedPassenger(UnitManager passenger)
    {
        if (passenger == null || transportedUnitSlots == null)
            return false;

        bool removed = false;
        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.embarkedUnit != passenger)
                continue;

            seat.embarkedUnit = null;
            removed = true;
        }

        if (removed)
        {
            RefreshSpriteForCurrentLayer();
            RefreshActedVisual();
        }

        return removed;
    }

    public void SyncLayerStateFromData(bool forceNativeDefault)
    {
        SyncCurrentLayerStateWithData(forceNativeDefault);
    }

    public void RefreshTransportSlotsFromData()
    {
        SyncTransportRuntimeSlotsWithData(TryGetUnitData());
        RefreshSpriteForCurrentLayer();
        RefreshActedVisual();
    }

    public void RefreshSupplierRuntimeFromData()
    {
        SyncSupplierRuntimeFromData(TryGetUnitData());
        RefreshActedVisual();
    }

    private void SyncEmbarkedWeaponsFromData(UnitData data)
    {
        if (embarkedWeaponsRuntime == null)
            embarkedWeaponsRuntime = new List<UnitEmbarkedWeapon>();

        embarkedWeaponsRuntime.Clear();
        if (data == null || data.embarkedWeapons == null)
            return;

        for (int i = 0; i < data.embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon source = data.embarkedWeapons[i];
            if (source == null || source.weapon == null)
                continue;

            UnitEmbarkedWeapon copy = new UnitEmbarkedWeapon
            {
                weapon = source.weapon,
                squadAmmunition = Mathf.Max(0, source.squadAmmunition),
                operationRangeMin = source.GetRangeMin(),
                operationRangeMax = source.GetRangeMax(),
                selectedTrajectory = source.selectedTrajectory
            };
            copy.EnsureValidSelectedTrajectory();
            embarkedWeaponsRuntime.Add(copy);
        }
    }

    private void SyncSupplierRuntimeFromData(UnitData data)
    {
        if (embarkedResourcesRuntime == null)
            embarkedResourcesRuntime = new List<UnitEmbarkedSupply>();
        if (embarkedServicesRuntime == null)
            embarkedServicesRuntime = new List<ServiceData>();
        if (currentlyObservedByTeamIds == null)
            currentlyObservedByTeamIds = new List<int>();
        for (int i = currentlyObservedByTeamIds.Count - 1; i >= 0; i--)
        {
            int team = currentlyObservedByTeamIds[i];
            if (team < -1 || team > 3)
                currentlyObservedByTeamIds.RemoveAt(i);
        }

        if (data == null || !data.isSupplier)
        {
            embarkedResourcesRuntime.Clear();
            embarkedServicesRuntime.Clear();
            return;
        }

        embarkedResourcesRuntime.Clear();
        List<UnitEmbarkedSupply> sourceResources = data.supplierResources;

        if (sourceResources != null)
        {
            for (int i = 0; i < sourceResources.Count; i++)
            {
                UnitEmbarkedSupply source = sourceResources[i];
                if (source == null || source.supply == null)
                    continue;

                UnitEmbarkedSupply copy = new UnitEmbarkedSupply
                {
                    supply = source.supply,
                    amount = Mathf.Max(0, source.amount)
                };
                embarkedResourcesRuntime.Add(copy);
            }
        }

        embarkedServicesRuntime.Clear();
        if (data.supplierServicesProvided == null)
            return;

        for (int i = 0; i < data.supplierServicesProvided.Count; i++)
        {
            ServiceData service = data.supplierServicesProvided[i];
            if (service == null || embarkedServicesRuntime.Contains(service))
                continue;
            embarkedServicesRuntime.Add(service);
        }
    }

    private void SyncTransportRuntimeSlotsWithData(UnitData data, bool preserveSeatPassengers = false)
    {
        if (transportedUnitSlots == null)
            transportedUnitSlots = new List<UnitTransportSeatRuntime>();

        if (data == null || !data.isTransporter || data.transportSlots == null || data.transportSlots.Count == 0)
        {
            transportedUnitSlots.Clear();
            return;
        }

        Dictionary<string, UnitManager> existing = new Dictionary<string, UnitManager>();
        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.embarkedUnit == null)
                continue;
            if (!preserveSeatPassengers && !seat.embarkedUnit.IsEmbarked)
                continue;

            string key = BuildTransportSeatKey(seat.slotIndex, seat.seatIndex);
            existing[key] = seat.embarkedUnit;
        }

        transportedUnitSlots.Clear();

        for (int slotIndex = 0; slotIndex < data.transportSlots.Count; slotIndex++)
        {
            UnitTransportSlotRule slot = data.transportSlots[slotIndex];
            if (slot == null)
                continue;

            int capacity = Mathf.Max(1, slot.capacity);
            string slotId = !string.IsNullOrWhiteSpace(slot.slotId) ? slot.slotId : $"slot_{slotIndex}";
            for (int seatIndex = 0; seatIndex < capacity; seatIndex++)
            {
                UnitTransportSeatRuntime runtimeSeat = new UnitTransportSeatRuntime
                {
                    slotIndex = slotIndex,
                    slotId = slotId,
                    seatIndex = seatIndex
                };

                string key = BuildTransportSeatKey(slotIndex, seatIndex);
                if (existing.TryGetValue(key, out UnitManager passenger) && passenger != null && passenger.IsEmbarked)
                    runtimeSeat.embarkedUnit = passenger;

                transportedUnitSlots.Add(runtimeSeat);
            }
        }
    }

    private UnitTransportSeatRuntime FindFirstFreeSeat(int slotIndex)
    {
        if (transportedUnitSlots == null)
            return null;

        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.slotIndex != slotIndex)
                continue;

            if (seat.embarkedUnit != null && !seat.embarkedUnit.IsEmbarked)
                seat.embarkedUnit = null;

            if (seat.embarkedUnit == null)
                return seat;
        }

        return null;
    }

    private UnitTransportSeatRuntime FindSeat(int slotIndex, int seatIndex)
    {
        if (transportedUnitSlots == null)
            return null;

        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null)
                continue;
            if (seat.slotIndex == slotIndex && seat.seatIndex == seatIndex)
                return seat;
        }

        return null;
    }

    private static string BuildTransportSeatKey(int slotIndex, int seatIndex)
    {
        return slotIndex.ToString() + ":" + seatIndex.ToString();
    }

    private UnitLayerMode[] BuildLayerModesSnapshot()
    {
        if (unitDatabase == null || string.IsNullOrWhiteSpace(unitId) || !unitDatabase.TryGetById(unitId, out UnitData data) || data == null)
            return new[] { new UnitLayerMode(Domain.Land, HeightLevel.Surface) };

        int additionalCount = data.aditionalDomainsAllowed != null ? data.aditionalDomainsAllowed.Count : 0;
        UnitLayerMode[] modes = new UnitLayerMode[1 + additionalCount];
        modes[0] = new UnitLayerMode(data.domain, data.heightLevel);

        for (int i = 0; i < additionalCount; i++)
            modes[i + 1] = data.aditionalDomainsAllowed[i];

        return modes;
    }

    private void SyncCurrentLayerStateWithData(bool forceNativeDefault)
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        SyncCurrentLayerStateWithModes(modes, forceNativeDefault);
    }

    private void SyncCurrentLayerStateWithData(UnitData data, bool forceNativeDefault)
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot(data);
        SyncCurrentLayerStateWithModes(modes, forceNativeDefault);
    }

    private void SyncCurrentLayerStateWithModes(UnitLayerMode[] modes, bool forceNativeDefault)
    {
        if (modes.Length == 0)
        {
            SetCurrentLayerState(0, new UnitLayerMode(Domain.Land, HeightLevel.Surface));
            return;
        }

        if (forceNativeDefault || !layerStateInitialized)
        {
            SetCurrentLayerState(0, modes[0]);
            return;
        }

        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i].domain == currentDomain && modes[i].heightLevel == currentHeightLevel)
            {
                SetCurrentLayerState(i, modes[i]);
                return;
            }
        }

        SetCurrentLayerState(0, modes[0]);
    }

    private static UnitLayerMode[] BuildLayerModesSnapshot(UnitData data)
    {
        if (data == null)
            return new[] { new UnitLayerMode(Domain.Land, HeightLevel.Surface) };

        int additionalCount = data.aditionalDomainsAllowed != null ? data.aditionalDomainsAllowed.Count : 0;
        UnitLayerMode[] modes = new UnitLayerMode[1 + additionalCount];
        modes[0] = new UnitLayerMode(data.domain, data.heightLevel);

        for (int i = 0; i < additionalCount; i++)
            modes[i + 1] = data.aditionalDomainsAllowed[i];

        return modes;
    }

    private int ResolveLayerModeIndex(Domain domain, HeightLevel heightLevel)
    {
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i].domain == domain && modes[i].heightLevel == heightLevel)
                return i;
        }

        return 0;
    }

    private void SetCurrentLayerState(int modeIndex, UnitLayerMode mode)
    {
        currentLayerModeIndex = Mathf.Max(0, modeIndex);
        currentDomain = mode.domain;
        currentHeightLevel = mode.heightLevel;
        layerStateInitialized = true;
        SyncAircraftRuntimeStateWithCurrentLayer();
        RefreshSpriteForCurrentLayer();
        RefreshActedVisual();
    }

    private UnitData TryGetUnitData()
    {
        if (unitDatabase == null || string.IsNullOrWhiteSpace(unitId))
            return null;
        if (!unitDatabase.TryGetById(unitId, out UnitData data))
            return null;
        return data;
    }

    private void RefreshSpriteForCurrentLayer()
    {
        RefreshSpriteForCurrentLayer(TryGetUnitData());
    }

    private void RefreshSpriteForCurrentLayer(UnitData data)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer == null || data == null)
            return;

        bool preferTransportSprite = ShouldUseTransportSprite(data);
        Sprite baseTeamSprite = TeamUtils.GetTeamSprite(data, teamId, preferTransportSprite);
        Sprite finalSprite = baseTeamSprite;
        if (currentLayerModeIndex > 0 && data.aditionalDomainsAllowed != null)
        {
            int additionalIndex = currentLayerModeIndex - 1;
            if (additionalIndex >= 0 && additionalIndex < data.aditionalDomainsAllowed.Count)
            {
                UnitLayerMode mode = data.aditionalDomainsAllowed[additionalIndex];
                Sprite layerSprite = TeamUtils.GetTeamSprite(mode, teamId, baseTeamSprite);
                if (layerSprite != null)
                    finalSprite = layerSprite;
            }
        }

        if (finalSprite != null)
            spriteRenderer.sprite = finalSprite;

        spriteRenderer.color = TeamUtils.GetColor(teamId);
        ApplyTeamVisualFlipFromMatchController();
    }

    private bool ShouldUseTransportSprite(UnitData data)
    {
        if (data == null || !data.isTransporter || data.spriteTransport == null)
            return false;
        return HasAnyEmbarkedPassenger(data);
    }

    private bool HasAnyEmbarkedPassenger(UnitData data)
    {
        if (data == null || !data.isTransporter || isEmbarked)
            return false;

        SyncTransportRuntimeSlotsWithData(data, preserveSeatPassengers: !Application.isPlaying);
        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.embarkedUnit == null)
                continue;

            if (seat.embarkedUnit.IsEmbarked)
                return true;

            if (Application.isPlaying)
                seat.embarkedUnit = null;
        }

        return false;
    }

    public void SetCurrentPosition(Vector3 position)
    {
        currentPosition = position;
        transform.position = position;
        if (boardTilemap != null)
            currentCellPosition = HexCoordinates.WorldToCell(boardTilemap, position);
    }

    public void SetTeamId(TeamId team)
    {
        TeamId previousTeam = teamId;
        teamId = team;
        if (!ApplyFromDatabase())
        {
            RefreshSpriteForCurrentLayer();
            UpdateDynamicName();
        }
        RefreshActedVisual();
        ThreatRevisionTracker.NotifyUnitTeamChanged(previousTeam, teamId);
        if (Application.isPlaying)
        {
            Vector3Int cell = currentCellPosition;
            cell.z = 0;
            UnitOccupancyRules.NotifyUnitOccupancyChanged(this, cell, cell);
        }
    }

    public void ApplyTeamVisualFlipX(bool flipX)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
            return;

        spriteRenderer.flipX = flipX;
    }

    public void AssignSpawnInstanceId(int id)
    {
        if (id <= 0)
            return;

        instanceId = id;
        UpdateDynamicName();
    }

    public void SetBoardTilemap(Tilemap tilemap)
    {
        boardTilemap = tilemap;
        SyncPositionState();
    }

    public void SetMatchController(MatchController mc)
    {
        matchController = mc;
    }

    public void SetCurrentCellPosition(Vector3Int cell, bool enforceFinalOccupancyRule = true)
    {
        Vector3Int previousCell = currentCellPosition;
        if (enforceFinalOccupancyRule && Application.isPlaying && boardTilemap != null)
        {
            Vector3Int target = cell;
            target.z = 0;
            if (UnitRulesDefinition.IsUnitCellOccupied(boardTilemap, target, this))
            {
                Debug.LogWarning($"[UnitManager] Destino bloqueado: hex ({target.x},{target.y},0) ja possui unidade.", this);
                return;
            }
        }

        currentCellPosition = cell;
        SnapToCellCenter();
        ThreatRevisionTracker.NotifyUnitCellChanged(this, previousCell, currentCellPosition);
        if (Application.isPlaying)
            UnitOccupancyRules.NotifyUnitOccupancyChanged(this, previousCell, currentCellPosition);
    }

    public void SetEmbarked(bool embarked)
    {
        bool previousEmbarked = isEmbarked;
        if (isEmbarked == embarked)
            return;

        isEmbarked = embarked;
        if (!isEmbarked)
            embarkedVisualPreviewDepth = 0;
        if (isEmbarked)
        {
            SetSelected(false);
            SetSpriteVisible(false);
            SetHudVisible(false);
            SetOwnedUiVisualsVisible(false);
            SyncHierarchyForEmbarkedState();
            if (actedLockRenderer != null)
                actedLockRenderer.enabled = false;
            RefreshDetectedIndicator();
            ThreatRevisionTracker.NotifyUnitEmbarkStateChanged(this, previousEmbarked, isEmbarked);
            if (Application.isPlaying)
            {
                Vector3Int cell = currentCellPosition;
                cell.z = 0;
                UnitOccupancyRules.NotifyUnitOccupancyChanged(this, cell, cell);
            }
            return;
        }

        if (embarkedTransporter != null)
            embarkedTransporter.RemoveEmbarkedPassenger(this);
        ClearEmbarkTransport();
        SyncHierarchyForEmbarkedState();

        SetSpriteVisible(true);
        SetHudVisible(true);
        SetOwnedUiVisualsVisible(true);
        RefreshActedVisual();
        RefreshDetectedIndicator();
        ThreatRevisionTracker.NotifyUnitEmbarkStateChanged(this, previousEmbarked, isEmbarked);
        if (Application.isPlaying)
        {
            Vector3Int cell = currentCellPosition;
            cell.z = 0;
            UnitOccupancyRules.NotifyUnitOccupancyChanged(this, cell, cell);
        }
    }

    private void AssignEmbarkTransport(UnitManager transporter, int slotIndex)
    {
        embarkedTransporter = transporter;
        embarkedTransporterSlotIndex = slotIndex;
        if (isEmbarked)
            SyncHierarchyForEmbarkedState();
    }

    private void ClearEmbarkTransport()
    {
        embarkedTransporter = null;
        embarkedTransporterSlotIndex = -1;
    }

    private void SyncHierarchyForEmbarkedState()
    {
        // Keep units independent in hierarchy even when embarked.
        // Embark linkage is controlled by runtime references/slots only.
        if (embarkedTransporter != null && transform.parent == embarkedTransporter.transform)
            transform.SetParent(null, true);
    }

    public void SnapToCellCenter()
    {
        if (boardTilemap == null)
        {
            currentPosition = transform.position;
            return;
        }

        Vector3 snapped = HexCoordinates.GetCellCenterWorld(boardTilemap, currentCellPosition);
        transform.position = snapped;
        currentPosition = snapped;
    }

    public void PullCellFromTransform()
    {
        currentPosition = transform.position;
        if (boardTilemap != null)
            currentCellPosition = HexCoordinates.WorldToCell(boardTilemap, currentPosition);
    }

    private void EnsureDefaults()
    {
        if ((int)teamId < -1 || (int)teamId > 3)
            teamId = TeamId.Green;

        if (string.IsNullOrWhiteSpace(unitId) && unitDatabase != null && unitDatabase.TryGetFirst(out UnitData first) && first != null)
            unitId = first.id;

        if (!IsFinite(currentPosition))
            currentPosition = Vector3.zero;

        if (instanceId < 0)
            instanceId = 0;

        maxAmmo = Mathf.Max(1, maxAmmo);
        maxFuel = Mathf.Max(1, maxFuel);
        visao = Mathf.Max(1, visao);
        currentAmmo = Mathf.Clamp(currentAmmo, 0, maxAmmo);
        currentFuel = Mathf.Clamp(currentFuel, 0, maxFuel);
        if (embarkedResourcesRuntime == null)
            embarkedResourcesRuntime = new List<UnitEmbarkedSupply>();
        if (embarkedServicesRuntime == null)
            embarkedServicesRuntime = new List<ServiceData>();

        UnitData data = TryGetUnitData();
        int maxMovement = data != null ? Mathf.Max(0, data.movement) : Mathf.Max(0, remainingMovementPoints);
        if (!hasActed)
            remainingMovementPoints = maxMovement;
        else
            remainingMovementPoints = Mathf.Clamp(remainingMovementPoints, 0, maxMovement);
        SyncTransportRuntimeSlotsWithData(data, preserveSeatPassengers: !Application.isPlaying);
        RestoreEditorEmbarkedStateFromSeats(data);
        SyncPreferredLayerPreferencesFromData(TryGetUnitData());
        SyncCurrentLayerStateWithData(forceNativeDefault: false);
        SyncAircraftRuntimeStateWithCurrentLayer();
    }

    private void RestoreEditorEmbarkedStateFromSeats(UnitData data)
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;
        if (data == null || !data.isTransporter || isEmbarked)
            return;

        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.embarkedUnit == null || seat.embarkedUnit == this)
                continue;

            UnitManager passenger = seat.embarkedUnit;
            passenger.isEmbarked = true;
            passenger.AssignEmbarkTransport(this, seat.slotIndex);
            passenger.SyncHierarchyForEmbarkedState();
            passenger.SetSelected(false);
            passenger.SetSpriteVisible(false);
            if (passenger.unitHud != null)
                passenger.HideHudForEditorEmbarkedPreview();
            if (passenger.actedLockRenderer != null)
                passenger.actedLockRenderer.enabled = false;
            passenger.RefreshActedVisual();
        }
#endif
    }

#if UNITY_EDITOR
    private void HideHudForEditorEmbarkedPreview()
    {
        SetHudVisible(false);
    }
#endif

    private void SetHudVisible(bool visible)
    {
        if (unitHud == null)
            TryAutoAssignHud();

        // Passenger embarked must keep HUD hidden unless an explicit visual
        // preview is active (e.g. temporary supply animation preview).
        if (isEmbarked && visible && !IsEmbarkedVisualPreviewActive)
            visible = false;
        if (hiddenByFogOfWar && visible)
            visible = false;

        ApplyOwnedHudVisibility(visible);

        if (visible)
            RefreshHudWidgetsOnly();
    }

    private void ApplyOwnedHudVisibility(bool visible)
    {
        bool anyOwnedHud = false;
        UnitHudController[] ownedHuds = GetComponentsInChildren<UnitHudController>(true);
        for (int i = 0; i < ownedHuds.Length; i++)
        {
            UnitHudController hud = ownedHuds[i];
            if (hud == null)
                continue;

            UnitManager owner = hud.ResolveOwnerUnit();
            if (owner != this)
                continue;

            hud.gameObject.SetActive(visible);
            anyOwnedHud = true;
        }

        if (!anyOwnedHud && unitHud != null)
            unitHud.gameObject.SetActive(visible);
    }

    private void RefreshHudWidgetsOnly()
    {
        if (unitHud == null || (isEmbarked && !IsEmbarkedVisualPreviewActive))
            return;

        TryAutoAssignMatchController();
        UnitData unitData = TryGetUnitData();
        bool showTransportIndicator = HasAnyEmbarkedPassenger(unitData);
        bool showDetectedIndicator = ShouldShowDetectedIndicator(unitData);
        Color teamColor = TeamUtils.GetColor(teamId);
        unitHud.RefreshBindings();
        unitHud.Apply(
            currentHP,
            GetMaxHP(),
            currentAmmo,
            GetMaxAmmo(),
            currentFuel,
            GetMaxFuel(),
            teamColor,
            currentDomain,
            currentHeightLevel,
            showTransportIndicator,
            showDetectedIndicator);
    }

    private void SyncPreferredLayerPreferencesFromData(UnitData data)
    {
        if (data == null)
        {
            useExplicitPreferredAirHeightRuntime = false;
            preferredAirHeightRuntime = HeightLevel.AirLow;
            useExplicitPreferredNavalHeightRuntime = false;
            preferredNavalHeightRuntime = HeightLevel.Submerged;
            return;
        }

        useExplicitPreferredAirHeightRuntime = data.useExplicitPreferredAirHeight;
        preferredAirHeightRuntime = data.preferredAirHeight == HeightLevel.AirHigh ? HeightLevel.AirHigh : HeightLevel.AirLow;
        useExplicitPreferredNavalHeightRuntime = data.useExplicitPreferredNavalHeight;
        preferredNavalHeightRuntime = data.preferredNavalHeight == HeightLevel.Surface ? HeightLevel.Surface : HeightLevel.Submerged;
    }

    private void SyncAircraftRuntimeStateWithCurrentLayer()
    {
        // Aircraft runtime now derives from layer state + IsEmbarked.
    }

    private void SyncPositionState()
    {
        if (boardTilemap == null)
        {
            TryAutoAssignBoardTilemap();
        }

        if (boardTilemap == null)
        {
            currentPosition = transform.position;
            return;
        }

        if (snapToCellCenter)
            SnapToCellCenter();
        else
            PullCellFromTransform();
    }

    private void TryAutoAssignBoardTilemap()
    {
        if (boardTilemap != null &&
            string.Equals(boardTilemap.name, "TileMap", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Avoid trying to bind scene references while editing the prefab asset itself.
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            return;

        Tilemap namedBoard = FindTilemapByName("TileMap");
        if (namedBoard != null)
        {
            boardTilemap = namedBoard;
            return;
        }

        if (boardTilemap != null)
            return;

        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] == null)
                continue;

            GridLayout.CellLayout layout = maps[i].layoutGrid != null ? maps[i].layoutGrid.cellLayout : GridLayout.CellLayout.Rectangle;
            if (layout == GridLayout.CellLayout.Hexagon)
            {
                boardTilemap = maps[i];
                return;
            }
        }
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

    private void TryAutoAssignMatchController()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
    }

    private void ApplyTeamVisualFlipFromMatchController()
    {
        TryAutoAssignMatchController();
        if (matchController == null)
            return;

        ApplyTeamVisualFlipX(matchController.GetTeamFlipX(teamId));
    }

    private static bool IsFinite(Vector3 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }

    private void UpdateDynamicName()
    {
#if UNITY_EDITOR
        if (IsEditingPrefabContext())
            return;
#endif

        string baseName = !string.IsNullOrWhiteSpace(unitDisplayName) ? unitDisplayName : unitId;
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "Unit";

        baseName = baseName.Replace(" ", string.Empty);
        int team = (int)teamId;
        int uid = instanceId > 0 ? instanceId : 0;
        string receivedShortcut = receivedSuppliesThisTurn ? "_X" : string.Empty;
        gameObject.name = $"{baseName}_T{team}_U{uid}{receivedShortcut}";
    }

    private void TryAutoAssignLockRenderer()
    {
        if (actedLockRenderer != null)
            return;

        Transform lockChild = transform.Find("ActedLock");
        if (lockChild == null)
            return;

        actedLockRenderer = lockChild.GetComponent<SpriteRenderer>();
    }

    private void TryAutoAssignHud()
    {
        if (unitHud != null)
            return;

        UnitHudController[] candidates = GetComponentsInChildren<UnitHudController>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            UnitHudController candidate = candidates[i];
            if (candidate == null)
                continue;

            UnitManager owner = candidate.GetComponentInParent<UnitManager>();
            if (owner == this)
            {
                unitHud = candidate;
                return;
            }
        }
    }

    private void CacheSpriteMaterial()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null && defaultSpriteMaterial == null)
            defaultSpriteMaterial = spriteRenderer.sharedMaterial;

        if (spritePropertyBlock == null)
            spritePropertyBlock = new MaterialPropertyBlock();
    }

    private static Material GetActedGlowMaterial()
    {
        if (actedGlowMaterial != null)
            return actedGlowMaterial;

        Shader shader = Shader.Find("Custom/SpriteGlowOutline");
        if (shader == null)
            return null;

        actedGlowMaterial = new Material(shader)
        {
            name = "Runtime_UnitActedGlow"
        };
        return actedGlowMaterial;
    }

    private void SetActedGlowEnabled(bool enabled)
    {
        if (spriteRenderer == null)
            return;

        CacheSpriteMaterial();
        Material glowMaterial = GetActedGlowMaterial();
        if (enabled && glowMaterial != null)
        {
            spriteRenderer.sharedMaterial = glowMaterial;
            spriteRenderer.GetPropertyBlock(spritePropertyBlock);
            spritePropertyBlock.SetColor(GlowColorId, actedGlowColor);
            spritePropertyBlock.SetFloat(GlowSizeId, actedGlowSize);
            spritePropertyBlock.SetFloat(GlowStrengthId, actedGlowStrength);
            spriteRenderer.SetPropertyBlock(spritePropertyBlock);
        }
        else
        {
            if (defaultSpriteMaterial != null)
            {
                spriteRenderer.sharedMaterial = defaultSpriteMaterial;
            }
            else if (spriteRenderer.sharedMaterial == glowMaterial)
            {
                // Fallback: volta para o material padrao do SpriteRenderer.
                spriteRenderer.sharedMaterial = null;
            }

            spriteRenderer.SetPropertyBlock(null);
        }
    }

    private void DisableLegacyOutlineObjects()
    {
        Transform legacy = transform.Find("ActedOutline");
        if (legacy != null && legacy.gameObject.activeSelf)
            legacy.gameObject.SetActive(false);

        for (int i = 0; i < 4; i++)
        {
            Transform old = transform.Find($"ActedOutline_{i}");
            if (old != null && old.gameObject.activeSelf)
                old.gameObject.SetActive(false);
        }
    }

    private void RefreshActedVisual()
    {
#if UNITY_EDITOR
        if (IsEditingPrefabContext())
            return;
#endif

        if (isEmbarked && !IsEmbarkedVisualPreviewActive)
        {
            SetActedGlowEnabled(false);
            SetSpriteVisible(false);
            SetHudVisible(false);
            SetOwnedUiVisualsVisible(false);
            if (actedLockRenderer != null)
                actedLockRenderer.enabled = false;
            return;
        }

        TryAutoAssignMatchController();
        Color teamColor = TeamUtils.GetColor(teamId);
        bool isActiveTeamUnit = matchController != null && (int)teamId == matchController.ActiveTeamId;
        UnitData unitData = TryGetUnitData();
        bool showTransportIndicator = HasAnyEmbarkedPassenger(unitData);
        bool showDetectedIndicator = ShouldShowDetectedIndicator(unitData);

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Unidade fora do time ativo nunca escurece e nunca recebe glow de "ja agiu".
        if (!isActiveTeamUnit)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = ResolvePreviewDimmedColor(teamColor);

            SetActedGlowEnabled(false);

            if (unitHud != null)
            {
                unitHud.Apply(
                    currentHP,
                    GetMaxHP(),
                    currentAmmo,
                    GetMaxAmmo(),
                    currentFuel,
                    GetMaxFuel(),
                    teamColor,
                    currentDomain,
                    currentHeightLevel,
                    showTransportIndicator,
                    showDetectedIndicator
                );
            }

            if (actedLockRenderer != null)
                actedLockRenderer.enabled = false;

            return;
        }

        bool shouldHighlightActed = hasActed;

        if (spriteRenderer != null)
        {
            Color grayMixed = Color.Lerp(teamColor, Color.gray, Mathf.Clamp01(actedGrayBlend));
            Color unitColor = shouldHighlightActed
                ? new Color(grayMixed.r * Mathf.Clamp01(actedDarkenFactor), grayMixed.g * Mathf.Clamp01(actedDarkenFactor), grayMixed.b * Mathf.Clamp01(actedDarkenFactor), teamColor.a)
                : teamColor;
            spriteRenderer.color = ResolvePreviewDimmedColor(unitColor);
        }

        SetActedGlowEnabled(shouldHighlightActed);

        if (unitHud != null)
        {
            unitHud.Apply(
                currentHP,
                GetMaxHP(),
                currentAmmo,
                GetMaxAmmo(),
                currentFuel,
                GetMaxFuel(),
                teamColor,
                currentDomain,
                currentHeightLevel,
                showTransportIndicator,
                showDetectedIndicator
            );
        }

        if (actedLockRenderer != null)
            actedLockRenderer.enabled = false;
    }

    private Color ResolvePreviewDimmedColor(Color baseColor)
    {
        if (!isPreviewDimmed)
            return baseColor;

        Color grayMixed = Color.Lerp(baseColor, Color.gray, Mathf.Clamp01(previewDimGrayBlend));
        return new Color(
            grayMixed.r * Mathf.Clamp01(previewDimDarkenFactor),
            grayMixed.g * Mathf.Clamp01(previewDimDarkenFactor),
            grayMixed.b * Mathf.Clamp01(previewDimDarkenFactor),
            baseColor.a);
    }

    private bool ShouldShowDetectedIndicator(UnitData unitData)
    {
        if (unitData == null || !unitData.IsStealthUnit())
            return false;

        if (currentlyObservedByTeamIds == null || currentlyObservedByTeamIds.Count <= 0)
            return false;

        int ownerTeamId = (int)teamId;
        for (int i = 0; i < currentlyObservedByTeamIds.Count; i++)
        {
            int observerTeamId = currentlyObservedByTeamIds[i];
            if (observerTeamId < -1 || observerTeamId > 3)
                continue;
            if (observerTeamId == ownerTeamId)
                continue;

            return true;
        }

        return false;
    }

    private void RefreshDetectedIndicator()
    {
        if (unitHud == null)
            TryAutoAssignHud();
        if (unitHud == null || !unitHud.gameObject.activeInHierarchy)
            return;

        bool shouldShow = ShouldShowDetectedIndicator(TryGetUnitData());
        unitHud.SetDetectedIndicatorVisible(shouldShow);
    }

    private void HandleActiveTeamChanged(int newTeamId)
    {
        if (appliedActiveTeamId == newTeamId)
            return;

        appliedActiveTeamId = newTeamId;
        RefreshActedVisual();
    }

    private void HandleUnitActedStateChanged(UnitManager changed)
    {
        if (changed != this)
            return;
        if (appliedHasActed == hasActed)
            return;

        appliedHasActed = hasActed;
        RefreshActedVisual();
    }

    private void HandleFogOfWarUpdated()
    {
        RefreshDetectedIndicator();
    }

    private void RefreshSelectionVisual()
    {
        if (!isSelected)
        {
            StopSelectionBlinkRoutine();
            SetSpriteVisible(true);
            ApplyFogOfWarVisibility();
            return;
        }

        if (!enableSelectionBlink)
        {
            SetSpriteVisible(true);
            ApplyFogOfWarVisibility();
            return;
        }

        if (!Application.isPlaying)
        {
            SetSpriteVisible(true);
            ApplyFogOfWarVisibility();
            return;
        }

        if (selectionBlinkRoutine == null)
            selectionBlinkRoutine = StartCoroutine(SelectionBlinkRoutine());
    }

    private IEnumerator SelectionBlinkRoutine()
    {
        while (isSelected)
        {
            SetSpriteVisible(false);
            yield return new WaitForSeconds(selectionBlinkInactiveDuration);
            SetSpriteVisible(true);
            yield return new WaitForSeconds(selectionBlinkActiveDuration);
        }

        selectionBlinkRoutine = null;
        SetSpriteVisible(true);
    }

    private void StopSelectionBlinkRoutine()
    {
        if (selectionBlinkRoutine == null)
            return;

        StopCoroutine(selectionBlinkRoutine);
        selectionBlinkRoutine = null;
    }

    private void SetSpriteVisible(bool visible)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (unitHud == null)
            TryAutoAssignHud();

        // Passenger embarked must stay visually hidden even if other
        // systems request visibility (selection cleanup, blink stop, etc).
        if (isEmbarked && visible && !IsEmbarkedVisualPreviewActive)
            visible = false;
        if (hiddenByFogOfWar && visible)
            visible = false;

        if (spriteRenderer != null && spriteRenderer.GetComponentInParent<UnitManager>() == this)
            spriteRenderer.enabled = visible;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        Transform hudRoot = unitHud != null ? unitHud.transform : null;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;
            if (renderer == spriteRenderer)
                continue;

            UnitManager owner = renderer.GetComponentInParent<UnitManager>();
            if (owner != this)
                continue;

            // HUD sprites (altitude/detected/etc) sao controlados pelo UnitHudController.
            if (renderer.GetComponentInParent<UnitHudController>() != null)
                continue;

            if (hudRoot != null && renderer.transform.IsChildOf(hudRoot))
                continue;

            renderer.enabled = visible;
        }
    }

    private void ApplyFogOfWarVisibility()
    {
        bool visible = !hiddenByFogOfWar;
        if (!visible)
        {
            StopSelectionBlinkRoutine();
            isSelected = false;
        }

        SetSpriteVisible(visible);
        SetHudVisible(visible);
        SetOwnedUiVisualsVisible(visible);
    }

    private void SetOwnedUiVisualsVisible(bool visible)
    {
        if (unitHud == null)
            TryAutoAssignHud();
        Transform hudRoot = unitHud != null ? unitHud.transform : null;

        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;

            UnitManager owner = canvas.GetComponentInParent<UnitManager>();
            if (owner != this)
                continue;
            if (hudRoot != null && canvas.transform.IsChildOf(hudRoot))
                continue;

            canvas.enabled = visible;
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            UnitManager owner = graphic.GetComponentInParent<UnitManager>();
            if (owner != this)
                continue;
            if (hudRoot != null && graphic.transform.IsChildOf(hudRoot))
                continue;

            graphic.enabled = visible;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            UnitManager owner = text.GetComponentInParent<UnitManager>();
            if (owner != this)
                continue;
            if (hudRoot != null && text.transform.IsChildOf(hudRoot))
                continue;

            text.enabled = visible;
        }
    }

    public void BeginEmbarkedVisualPreview()
    {
        embarkedVisualPreviewDepth = Mathf.Max(0, embarkedVisualPreviewDepth) + 1;
        RefreshActedVisual();
    }

    public void EndEmbarkedVisualPreview()
    {
        if (embarkedVisualPreviewDepth > 0)
            embarkedVisualPreviewDepth--;
        RefreshActedVisual();
    }

    [ContextMenu("Apply From Database")]
    private void ApplyFromDatabaseContext()
    {
        bool ok = ApplyFromDatabase();
        if (!ok)
            Debug.LogWarning("[UnitManager] Nao foi possivel aplicar UnitData (db/id).", this);
    }

    [ContextMenu("Snap To Cell Center")]
    private void SnapToCellCenterContext()
    {
        SnapToCellCenter();
    }

    [ContextMenu("Pull Cell From Transform")]
    private void PullCellFromTransformContext()
    {
        PullCellFromTransform();
    }

#if UNITY_EDITOR
    private bool IsEditingPrefabContext()
    {
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject))
            return true;

        UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject);
        return stage != null;
    }
#endif
}
