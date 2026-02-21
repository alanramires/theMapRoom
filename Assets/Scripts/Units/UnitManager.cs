using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class UnitManager : MonoBehaviour
{
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
    [Header("Embarked Weapons Runtime")]
    [SerializeField] private List<UnitEmbarkedWeapon> embarkedWeaponsRuntime = new List<UnitEmbarkedWeapon>();
    [SerializeField, HideInInspector] private bool appliedHasActed;
    [SerializeField, HideInInspector] private int appliedActiveTeamId = int.MinValue;
    [SerializeField] private bool isEmbarked;
    [SerializeField] private bool isSelected;
    [SerializeField, HideInInspector] private bool hasTemporarySortingOverride;
    [SerializeField, HideInInspector] private int cachedSpriteSortingOrder;
    [SerializeField, HideInInspector] private int cachedActedLockSortingOrder;
    [SerializeField] private bool enableSelectionBlink = true;
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkInterval = 0.16f;
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkActiveDuration = 0.16f;
    [SerializeField] [Range(0.05f, 1f)] private float selectionBlinkInactiveDuration = 0.16f;
    [SerializeField] [Range(0f, 1f)] private float actedDarkenFactor = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float actedGrayBlend = 0.6f;
    [SerializeField] private Color actedGlowColor = Color.white;
    [SerializeField] [Range(0.1f, 6f)] private float actedGlowSize = 1.5f;
    [SerializeField] [Range(0f, 4f)] private float actedGlowStrength = 1.25f;
    [Header("Layer State")]
    [SerializeField] private Domain currentDomain = Domain.Land;
    [SerializeField] private HeightLevel currentHeightLevel = HeightLevel.Surface;
    [SerializeField] private int currentLayerModeIndex = 0;
    [SerializeField] private bool layerStateInitialized;
    [Header("Aircraft Runtime")]
    [SerializeField] private bool aircraftGrounded;
    [SerializeField] private bool aircraftEmbarkedInCarrier;
    [SerializeField] [Min(0)] private int aircraftOperationLockTurns;
    [Header("Transport Runtime")]
    [SerializeField] private List<UnitTransportSeatRuntime> transportedUnitSlots = new List<UnitTransportSeatRuntime>();
    [SerializeField, HideInInspector] private UnitManager embarkedTransporter;
    [SerializeField, HideInInspector] private int embarkedTransporterSlotIndex = -1;

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
    public bool HasActed => hasActed;
    public bool IsEmbarked => isEmbarked;
    public bool IsSelected => isSelected;
    public UnitDatabase UnitDatabase => unitDatabase;
    public bool IsAircraftGrounded => aircraftGrounded;
    public bool IsAircraftEmbarkedInCarrier => aircraftEmbarkedInCarrier;
    public int AircraftOperationLockTurns => Mathf.Max(0, aircraftOperationLockTurns);
    public IReadOnlyList<UnitTransportSeatRuntime> TransportedUnitSlots => transportedUnitSlots;
    public UnitManager EmbarkedTransporter => embarkedTransporter;
    public int EmbarkedTransporterSlotIndex => embarkedTransporterSlotIndex;

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
    }

    private void OnDisable()
    {
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

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        TryAutoAssignMatchController();
        int activeTeamId = matchController != null ? matchController.ActiveTeamId : int.MinValue;
        if (appliedActiveTeamId != activeTeamId)
        {
            appliedActiveTeamId = activeTeamId;
            RefreshActedVisual();
        }

        if (appliedHasActed != hasActed)
        {
            appliedHasActed = hasActed;
            RefreshActedVisual();
        }
    }

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
        currentAmmo = Mathf.Clamp(currentAmmo, 0, GetMaxAmmo());
        currentFuel = Mathf.Clamp(currentFuel, 0, GetMaxFuel());
        SyncEmbarkedWeaponsFromData(data);
        SyncTransportRuntimeSlotsWithData(data);
        SyncCurrentLayerStateWithData(data, forceNativeDefault: true);
        RefreshSpriteForCurrentLayer(data);

        currentPosition = transform.position;
        UpdateDynamicName();
        RefreshActedVisual();
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
    }

    public void ResetActed()
    {
        hasActed = false;
        if (aircraftOperationLockTurns > 0)
            aircraftOperationLockTurns = Mathf.Max(0, aircraftOperationLockTurns - 1);
        appliedHasActed = hasActed;
        RefreshActedVisual();
    }

    public void SetSelected(bool selected)
    {
        if (isSelected == selected)
            return;

        isSelected = selected;
        RefreshSelectionVisual();
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
        UnitLayerMode[] modes = BuildLayerModesSnapshot();
        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i].domain == domain && modes[i].heightLevel == heightLevel)
            {
                SetCurrentLayerState(i, modes[i]);
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
        aircraftGrounded = grounded;
        if (!grounded)
            aircraftEmbarkedInCarrier = false;
    }

    public void SetAircraftEmbarkedInCarrier(bool embarkedInCarrier)
    {
        aircraftEmbarkedInCarrier = embarkedInCarrier;
        if (aircraftEmbarkedInCarrier)
            aircraftGrounded = true;
    }

    public void SetAircraftOperationLockTurns(int turns)
    {
        aircraftOperationLockTurns = Mathf.Max(0, turns);
    }

    public IReadOnlyList<UnitEmbarkedWeapon> GetEmbarkedWeapons()
    {
        return embarkedWeaponsRuntime;
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

    private void SyncTransportRuntimeSlotsWithData(UnitData data)
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
            if (!seat.embarkedUnit.IsEmbarked)
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

        SyncTransportRuntimeSlotsWithData(data);
        for (int i = 0; i < transportedUnitSlots.Count; i++)
        {
            UnitTransportSeatRuntime seat = transportedUnitSlots[i];
            if (seat == null || seat.embarkedUnit == null)
                continue;

            if (seat.embarkedUnit.IsEmbarked)
                return true;

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
        teamId = team;
        if (!ApplyFromDatabase())
        {
            RefreshSpriteForCurrentLayer();
            UpdateDynamicName();
        }
        RefreshActedVisual();
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

    public void SetCurrentCellPosition(Vector3Int cell, bool enforceFinalOccupancyRule = true)
    {
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
    }

    public void SetEmbarked(bool embarked)
    {
        if (isEmbarked == embarked)
            return;

        isEmbarked = embarked;
        if (isEmbarked)
        {
            SetSelected(false);
            SetSpriteVisible(false);
            if (unitHud != null)
                unitHud.gameObject.SetActive(false);
            if (actedLockRenderer != null)
                actedLockRenderer.enabled = false;
            return;
        }

        if (embarkedTransporter != null)
            embarkedTransporter.RemoveEmbarkedPassenger(this);
        ClearEmbarkTransport();

        SetSpriteVisible(true);
        if (unitHud != null)
            unitHud.gameObject.SetActive(true);
        RefreshActedVisual();
    }

    private void AssignEmbarkTransport(UnitManager transporter, int slotIndex)
    {
        embarkedTransporter = transporter;
        embarkedTransporterSlotIndex = slotIndex;
    }

    private void ClearEmbarkTransport()
    {
        embarkedTransporter = null;
        embarkedTransporterSlotIndex = -1;
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
        currentAmmo = Mathf.Clamp(currentAmmo, 0, maxAmmo);
        currentFuel = Mathf.Clamp(currentFuel, 0, maxFuel);

        SyncTransportRuntimeSlotsWithData(TryGetUnitData());
        SyncCurrentLayerStateWithData(forceNativeDefault: false);
        SyncAircraftRuntimeStateWithCurrentLayer();
    }

    private void SyncAircraftRuntimeStateWithCurrentLayer()
    {
        UnitData data = TryGetUnitData();
        bool isAircraft = data != null && data.IsAircraft();
        if (!isAircraft)
        {
            aircraftGrounded = false;
            aircraftEmbarkedInCarrier = false;
            aircraftOperationLockTurns = 0;
            return;
        }

        if (currentDomain == Domain.Air)
            aircraftGrounded = false;
        else
            aircraftGrounded = true;

        if (!aircraftGrounded)
            aircraftEmbarkedInCarrier = false;

        aircraftOperationLockTurns = Mathf.Max(0, aircraftOperationLockTurns);
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
        if (boardTilemap != null)
            return;

        // Avoid trying to bind scene references while editing the prefab asset itself.
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
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

    private void TryAutoAssignMatchController()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
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
        gameObject.name = $"{baseName}_T{team}_U{uid}";
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

        unitHud = GetComponentInChildren<UnitHudController>();
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

        TryAutoAssignMatchController();
        Color teamColor = TeamUtils.GetColor(teamId);
        bool isActiveTeamUnit = matchController != null && (int)teamId == matchController.ActiveTeamId;
        UnitData unitData = TryGetUnitData();
        bool showTransportIndicator = HasAnyEmbarkedPassenger(unitData);

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Unidade fora do time ativo nunca escurece e nunca recebe glow de "ja agiu".
        if (!isActiveTeamUnit)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = teamColor;

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
                    showTransportIndicator
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
            spriteRenderer.color = unitColor;
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
                showTransportIndicator
            );
        }

        if (actedLockRenderer != null)
            actedLockRenderer.enabled = false;
    }

    private void RefreshSelectionVisual()
    {
        if (!isSelected)
        {
            StopSelectionBlinkRoutine();
            SetSpriteVisible(true);
            return;
        }

        if (!enableSelectionBlink)
        {
            SetSpriteVisible(true);
            return;
        }

        if (!Application.isPlaying)
        {
            SetSpriteVisible(true);
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

        if (spriteRenderer != null)
            spriteRenderer.enabled = visible;
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
