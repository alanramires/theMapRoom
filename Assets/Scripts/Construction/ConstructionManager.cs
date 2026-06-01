using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class ConstructionManager : MonoBehaviour
{
    private const string VictoryBuildingOverlayObjectName = "VictoryBuildingOverlay";
    public static readonly List<ConstructionManager> AllActive = new List<ConstructionManager>();
    private static int activeTeamChangedHandlerCount;
    private static double activeTeamChangedHandlerTotalMs;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private ConstructionDatabase constructionDatabase;
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private bool snapToCellCenter = true;
    [SerializeField] private bool autoSnapWhenMovedInEditor = true;
    [SerializeField] private Vector3Int currentCellPosition = Vector3Int.zero;
    [SerializeField] private TeamId teamId = TeamId.Green;
    [Tooltip("Slot do MatchController que controla este time. -1 = Neutral fixo (sem slot).")]
    [SerializeField] private int slotIndex = -1;
    [Tooltip("Setor estratégico ao qual esta construção pertence. Use Base1-Base4 para areas de base de cada jogador.")]
    [SerializeField] private ConstructionSector sector = ConstructionSector.None;
    [SerializeField] private string constructionId;
    [SerializeField] private int instanceId;
    [SerializeField] private Vector3 currentPosition = Vector3.zero;
    [SerializeField] private string constructionDisplayName;
    [Tooltip("Override visual desta construcao na cena. Quando desligado, o objeto continua existindo para regras, mas sprite/HUD/overlay ficam ocultos.")]
    [SerializeField] private bool isVisible = true;
    [SerializeField] private bool autoApplyOnStart = true;
    [SerializeField] private ConstructionSiteRuntime siteRuntime = new ConstructionSiteRuntime();
    [SerializeField, HideInInspector] private bool hasSiteRuntimeOverride;
    [SerializeField] private int currentCapturePoints;
    [SerializeField] private bool hasInfiniteSuppliesOverride;
    [SerializeField] private TeamId originalOwnerTeamId = TeamId.Neutral;
    [SerializeField] private bool originalOwnerInitialized;
    [SerializeField] private TeamId firstOwnerTeamId = TeamId.Neutral;
    [SerializeField] private bool firstOwnerInitialized;
    [Header("Runtime Visual")]
    [SerializeField] [Range(0f, 1f)] private float occupiedByReadyUnitDarkenFactor = 0.4f;
    [SerializeField] private ConstructionHudController hudController;
    [SerializeField] private MatchController matchController;
    [Header("Tactical Role")]
    [Tooltip("Este prédio serve como ponto de observação avançada (Forward Observer) para artilharia.")]
    [SerializeField] private bool isForwardObserverSpot;
    [Tooltip("Este prédio serve como ponto de reunião (Rally Point) para unidades recém-compradas.")]
    [SerializeField] private bool isRallyPoint;
    [HideInInspector, SerializeField] private int rallyTargetSlotIndex = -1;
    [Tooltip("Slots dos HQs alvo deste Rally Point. Vazio = nenhum alvo configurado.")]
    [SerializeField] private List<int> rallyTargetSlotIndexes = new List<int>();

    [Header("Victory Building")]
    [SerializeField] private SpriteRenderer victoryBuildingOverlayRenderer;
    [SerializeField] private Sprite victoryBuildingOverlaySprite;
    [SerializeField] private Color victoryBuildingOverlayColor = new Color(1f, 0.84f, 0.22f, 1f);
    [SerializeField] private Vector3 victoryBuildingOverlayLocalPosition = new Vector3(0f, 0f, -0.01f);
    [SerializeField] private Vector3 victoryBuildingOverlayLocalScale = Vector3.one;
    [SerializeField] [Range(-100, 100)] private int victoryBuildingOverlaySortingOrderOffset = 6;
    [Header("Editor")]
    [SerializeField] private bool continuousEditorVisualRefresh = false;
    [System.NonSerialized] private int cachedOccupantInstanceId = int.MinValue;
    [System.NonSerialized] private bool cachedOccupantVisible;
    [System.NonSerialized] private bool cachedOccupantShouldDarken;
    [System.NonSerialized] private bool cachedShowFlagThreatOutline;
#if UNITY_EDITOR
    [System.NonSerialized] private bool editorTickRegistered;
#endif

    public TeamId TeamId => teamId;
    public int SlotIndex => slotIndex;
    public ConstructionSector Sector => sector;
    public bool IsForwardObserverSpot => isForwardObserverSpot;
    public bool IsRallyPoint => isRallyPoint;
    public int RallyTargetSlotIndex => rallyTargetSlotIndexes != null && rallyTargetSlotIndexes.Count > 0 ? rallyTargetSlotIndexes[0] : rallyTargetSlotIndex;
    public IReadOnlyList<int> RallyTargetSlotIndexes => rallyTargetSlotIndexes != null ? rallyTargetSlotIndexes : System.Array.Empty<int>();

    public void SetSlotIndex(int index)
    {
        slotIndex = index;
        ResolveTeamIdFromSlot();
    }
    public Tilemap BoardTilemap => boardTilemap;
    public Vector3Int CurrentCellPosition => currentCellPosition;
    public string ConstructionId => constructionId;
    public int InstanceId => instanceId;
    public Vector3 CurrentPosition => currentPosition;
    public string ConstructionDisplayName => constructionDisplayName;
    public bool IsVisible => isVisible;
    public ConstructionDatabase ConstructionDatabase => constructionDatabase;
    public bool IsCapturable => siteRuntime != null && siteRuntime.isCapturable;
    public int CapturePointsMax => siteRuntime != null ? siteRuntime.capturePointsMax : 0;
    public int CurrentCapturePoints => currentCapturePoints;
    public bool CanProduceUnits => CanProduceUnitsForTeam(teamId);
    public bool CanProvideSupplies => siteRuntime != null && siteRuntime.canProvideSupplies;
    public bool HasInfiniteSuppliesOverride => hasInfiniteSuppliesOverride;
    public bool IsPlayerHeadQuarter => siteRuntime != null && siteRuntime.isPlayerHeadQuarter;
    public bool IsVictoryBuilding => siteRuntime != null && siteRuntime.isVictoryBuilding;
    public int CapturedIncoming => siteRuntime != null ? Mathf.Max(0, siteRuntime.capturedIncoming) : 0;
    public IReadOnlyList<ServiceData> OfferedServices => siteRuntime != null && siteRuntime.offeredServices != null ? siteRuntime.offeredServices : System.Array.Empty<ServiceData>();
    public IReadOnlyList<UnitData> OfferedUnits => siteRuntime != null && siteRuntime.offeredUnits != null ? siteRuntime.offeredUnits : System.Array.Empty<UnitData>();
    public IReadOnlyList<ConstructionSupplyOffer> OfferedSupplies => siteRuntime != null && siteRuntime.offeredSupplies != null ? siteRuntime.offeredSupplies : System.Array.Empty<ConstructionSupplyOffer>();
    public TeamId OriginalOwnerTeamId => originalOwnerTeamId;
    public bool HasOriginalOwner => originalOwnerInitialized;
    public TeamId FirstOwnerTeamId => firstOwnerTeamId;
    public bool HasFirstOwner => firstOwnerInitialized;

    public static void ResetActiveTeamChangedPerfCounters()
    {
        activeTeamChangedHandlerCount = 0;
        activeTeamChangedHandlerTotalMs = 0d;
    }

    public static void GetActiveTeamChangedPerfCounters(out int count, out double totalMs)
    {
        count = activeTeamChangedHandlerCount;
        totalMs = activeTeamChangedHandlerTotalMs;
    }

    public Domain GetDomain()
    {
        if (TryGetConstructionData(out ConstructionData data))
            return data.domain;

        return Domain.Land;
    }

    public HeightLevel GetHeightLevel()
    {
        if (TryGetConstructionData(out ConstructionData data))
            return data.heightLevel;

        return HeightLevel.Surface;
    }

    public bool AllowsAirDomain()
    {
        if (TryGetConstructionData(out ConstructionData data))
            return data.alwaysAllowAirDomain;

        return false;
    }

    public bool IsFakeBuilding
    {
        get
        {
            if (TryGetConstructionData(out ConstructionData data))
                return data.isFakeBuilding;
            return false;
        }
    }

    public int GetBaseMovementCost()
    {
        if (TryGetConstructionData(out ConstructionData data))
            return Mathf.Max(1, data.baseMovementCost);

        return 1;
    }

    public IReadOnlyList<SkillData> GetRequiredSkillsToEnter()
    {
        if (TryGetConstructionData(out ConstructionData data) && data.requiredSkillsToEnter != null)
            return data.requiredSkillsToEnter;

        return System.Array.Empty<SkillData>();
    }

    public IReadOnlyList<SkillData> GetBlockedSkillsToEnter()
    {
        if (TryGetConstructionData(out ConstructionData data) && data.blockedSkills != null)
            return data.blockedSkills;

        return System.Array.Empty<SkillData>();
    }

    public IReadOnlyList<TerrainSkillCostOverride> GetSkillCostOverrides()
    {
        if (TryGetConstructionData(out ConstructionData data) && data.skillCostOverrides != null)
            return data.skillCostOverrides;

        return System.Array.Empty<TerrainSkillCostOverride>();
    }

    public bool TryResolveConstructionData(out ConstructionData data)
    {
        return TryGetConstructionData(out data);
    }

    public IReadOnlyList<TerrainLayerMode> GetAllLayerModes()
    {
        if (!TryGetConstructionData(out ConstructionData data))
            return new[] { new TerrainLayerMode(Domain.Land, HeightLevel.Surface) };

        int additionalCount = data.aditionalDomainsAllowed != null ? data.aditionalDomainsAllowed.Count : 0;
        TerrainLayerMode[] modes = new TerrainLayerMode[1 + additionalCount];
        modes[0] = new TerrainLayerMode(data.domain, data.heightLevel);
        for (int i = 0; i < additionalCount; i++)
            modes[i + 1] = data.aditionalDomainsAllowed[i];

        return modes;
    }

    public bool SupportsLayerMode(Domain domain, HeightLevel heightLevel)
    {
        IReadOnlyList<TerrainLayerMode> modes = GetAllLayerModes();
        for (int i = 0; i < modes.Count; i++)
        {
            TerrainLayerMode mode = modes[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private void Awake()
    {
        EnsureDefaults();
        TryAutoAssignMatchController();
        TryAutoAssignBoardTilemap();
        ResolveTeamIdFromSlot();
        SyncPositionState();
        RefreshRuntimeVisualState(force: true);
    }

    private void Start()
    {
        TryAutoAssignMatchController();
        if (autoApplyOnStart)
            ApplyFromDatabase();

        RefreshRuntimeVisualState(force: true);
    }

    private void OnEnable()
    {
        if (!AllActive.Contains(this))
            AllActive.Add(this);

        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
        MatchController.OnFogOfWarUpdated += HandleFogOfWarUpdated;
        MatchController.OnUnitActedStateChanged += HandleUnitActedStateChanged;
        MatchController.OnSlotConfigChanged += HandleSlotConfigChanged;
        UnitOccupancyRules.OnUnitOccupancyChanged += HandleUnitOccupancyChanged;
        RefreshRuntimeVisualState(force: true);
#if UNITY_EDITOR
        RegisterEditorTick();
#endif
    }

    private void OnDisable()
    {
        AllActive.Remove(this);
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
        MatchController.OnFogOfWarUpdated -= HandleFogOfWarUpdated;
        MatchController.OnUnitActedStateChanged -= HandleUnitActedStateChanged;
        MatchController.OnSlotConfigChanged -= HandleSlotConfigChanged;
        UnitOccupancyRules.OnUnitOccupancyChanged -= HandleUnitOccupancyChanged;
#if UNITY_EDITOR
        UnregisterEditorTick();
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (spriteRenderer == null)
            spriteRenderer = ResolvePrimarySpriteRenderer();

        EnsureDefaults();
        TryAutoAssignMatchController();
        TryAutoAssignBoardTilemap();

        if (IsEditingPrefabContext())
            return;

        SyncPositionState();
        if (!ApplyFromDatabase())
            UpdateDynamicName();
        RefreshRuntimeVisualState(force: true);
    }
#endif

    public void Setup(ConstructionDatabase database, string id)
    {
        constructionDatabase = database;
        constructionId = id;
        EnsureDefaults();
        UpdateDynamicName();
    }

    public bool ApplyFromDatabase()
    {
        if (constructionDatabase == null || string.IsNullOrWhiteSpace(constructionId))
            return false;

        if (!constructionDatabase.TryGetById(constructionId, out ConstructionData data))
            return false;

        Apply(data);
        return true;
    }

    public void Apply(ConstructionData data)
    {
        if (data == null)
            return;

        constructionId = data.id;
        constructionDisplayName = string.IsNullOrWhiteSpace(data.displayName) ? data.id : data.displayName;

        if (spriteRenderer == null)
            spriteRenderer = ResolvePrimarySpriteRenderer();

        if (spriteRenderer != null)
        {
            Sprite chosen = TeamUtils.GetTeamSprite(data, teamId);
            if (chosen != null)
                spriteRenderer.sprite = chosen;

            spriteRenderer.color = TeamUtils.GetColor(teamId);
            ApplyTeamVisualFlipFromMatchController();
        }

        ApplyDefaultSiteRuntime(data);
        EnsureCapturePointsInitialized();
        currentPosition = transform.position;
        UpdateDynamicName();
        RefreshRuntimeVisualState(force: true);
    }

    public void SetCurrentPosition(Vector3 position)
    {
        currentPosition = position;
        transform.position = position;
        if (boardTilemap != null)
            currentCellPosition = HexCoordinates.WorldToCell(boardTilemap, position);
    }

    public void SetVisible(bool value)
    {
        if (isVisible == value)
            return;

        isVisible = value;
        RefreshRuntimeVisualState(force: true);
    }

    public void SetRallyTargetSlotIndex(int value)
    {
        rallyTargetSlotIndex = Mathf.Max(-1, value);
        if (rallyTargetSlotIndexes == null)
            rallyTargetSlotIndexes = new List<int>();
        rallyTargetSlotIndexes.Clear();
        if (rallyTargetSlotIndex >= 0)
            rallyTargetSlotIndexes.Add(rallyTargetSlotIndex);
    }

    public void SetRallyTargetSlotIndexes(IEnumerable<int> values)
    {
        List<int> sanitizedSlots = new List<int>();

        if (values != null)
        {
            foreach (int value in values)
            {
                int slot = Mathf.Max(-1, value);
                if (slot < 0 || sanitizedSlots.Contains(slot))
                    continue;

                sanitizedSlots.Add(slot);
            }
        }

        if (rallyTargetSlotIndexes == null)
            rallyTargetSlotIndexes = new List<int>();
        rallyTargetSlotIndexes.Clear();
        rallyTargetSlotIndexes.AddRange(sanitizedSlots);
        rallyTargetSlotIndex = rallyTargetSlotIndexes.Count > 0 ? rallyTargetSlotIndexes[0] : -1;
    }

    public void SetTeamId(TeamId team)
    {
        TeamId previousTeam = teamId;
        teamId = team;
        TryAutoAssignMatchController();
        slotIndex = matchController != null ? matchController.GetSlotIndexForTeam(team) : (team == TeamId.Neutral ? -1 : slotIndex);
        if (!originalOwnerInitialized)
        {
            originalOwnerTeamId = team;
            originalOwnerInitialized = true;
        }

        if (!firstOwnerInitialized && team != TeamId.Neutral)
        {
            firstOwnerTeamId = team;
            firstOwnerInitialized = true;
        }

        if (!ApplyFromDatabase())
            UpdateDynamicName();
        RefreshRuntimeVisualState(force: true);
        ThreatRevisionTracker.NotifyConstructionTeamChanged(previousTeam, teamId);
    }

    public void ApplyTeamVisualFlipX(bool flipX)
    {
        if (spriteRenderer == null)
            spriteRenderer = ResolvePrimarySpriteRenderer();
        if (spriteRenderer == null)
            return;

        spriteRenderer.flipX = flipX;
    }

    public void InitializeOwnershipForSpawn(TeamId initialTeam)
    {
        TeamId previousTeam = teamId;
        teamId = initialTeam;
        originalOwnerTeamId = initialTeam;
        originalOwnerInitialized = true;

        if (initialTeam == TeamId.Neutral)
        {
            firstOwnerTeamId = TeamId.Neutral;
            firstOwnerInitialized = false;
        }
        else
        {
            firstOwnerTeamId = initialTeam;
            firstOwnerInitialized = true;
        }

        if (!ApplyFromDatabase())
            UpdateDynamicName();
        RefreshRuntimeVisualState(force: true);
        ThreatRevisionTracker.NotifyConstructionTeamChanged(previousTeam, teamId);
    }

    public void ApplyOwnershipState(TeamId currentTeam, TeamId originalOwner, bool hasOriginalOwner, TeamId firstOwner, bool hasFirstOwner)
    {
        TeamId previousTeam = teamId;
        teamId = currentTeam;
        originalOwnerTeamId = originalOwner;
        originalOwnerInitialized = hasOriginalOwner;
        firstOwnerTeamId = firstOwner;
        firstOwnerInitialized = hasFirstOwner;

        if (!ApplyFromDatabase())
            UpdateDynamicName();
        RefreshRuntimeVisualState(force: true);
        ThreatRevisionTracker.NotifyConstructionTeamChanged(previousTeam, teamId);
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

    public void SetCurrentCellPosition(Vector3Int cell)
    {
        Vector3Int previousCell = currentCellPosition;
        currentCellPosition = cell;
        SnapToCellCenter();
        ThreatRevisionTracker.NotifyConstructionCellChanged(this, previousCell, currentCellPosition);
    }

    public void ApplySiteRuntime(ConstructionSiteRuntime runtime)
    {
        if (runtime == null)
        {
            siteRuntime = new ConstructionSiteRuntime();
            siteRuntime.Sanitize();
            hasSiteRuntimeOverride = false;
            RefreshRuntimeVisualState(force: true);
            return;
        }

        siteRuntime = runtime.Clone();
        hasSiteRuntimeOverride = true;
        RefreshRuntimeVisualState(force: true);
    }

    public ConstructionSiteRuntime GetSiteRuntimeSnapshot()
    {
        if (siteRuntime == null)
            siteRuntime = new ConstructionSiteRuntime();

        siteRuntime.Sanitize();
        return siteRuntime.Clone();
    }

    public void SetCurrentCapturePoints(int value)
    {
        int max = Mathf.Max(0, CapturePointsMax);
        currentCapturePoints = Mathf.Clamp(value, 0, max);
        RefreshRuntimeVisualState(force: true);
    }

    public void SetSector(ConstructionSector value)
    {
        sector = value;
        RefreshRuntimeVisualState(force: true);
    }

    public void SetInfiniteSuppliesOverride(bool value)
    {
        hasInfiniteSuppliesOverride = value;
        RefreshRuntimeVisualState(force: true);
    }

    public bool GetVictoryBuildingRuntimeFlag()
    {
        return siteRuntime != null && siteRuntime.isVictoryBuilding;
    }

    public void SetVictoryBuildingRuntimeFlag(bool enabled)
    {
        if (siteRuntime == null)
            siteRuntime = new ConstructionSiteRuntime();

        siteRuntime.isVictoryBuilding = enabled;
        hasSiteRuntimeOverride = true;
        RefreshRuntimeVisualState(force: true);
    }

    public bool HasInfiniteSuppliesFor(SupplyData supply = null)
    {
        if (hasInfiniteSuppliesOverride)
        {
            if (supply == null)
                return true;
            return ContainsOfferedSupply(supply);
        }

        IReadOnlyList<ConstructionSupplyOffer> offers = OfferedSupplies;
        bool hasRuntimeSupplyData = false;
        if (offers != null)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                ConstructionSupplyOffer offer = offers[i];
                if (offer == null || offer.supply == null)
                    continue;
                if (supply != null && offer.supply != supply)
                    continue;

                hasRuntimeSupplyData = true;
                if (offer.quantity >= int.MaxValue)
                    return true;
            }
        }

        // Se a instancia possui dado runtime desse supply (ou da lista geral), ele prevalece
        // sobre o asset pai, inclusive para "nao infinito".
        if (hasRuntimeSupplyData)
            return false;

        if (TryResolveConstructionData(out ConstructionData constructionData) && constructionData != null && constructionData.supplierResources != null)
        {
            for (int i = 0; i < constructionData.supplierResources.Count; i++)
            {
                ConstructionSupplierResourceCapacity entry = constructionData.supplierResources[i];
                if (entry == null || entry.supply == null)
                    continue;
                if (supply != null && entry.supply != supply)
                    continue;
                if (entry.IsInfinite())
                    return true;
            }
        }

        return false;
    }

    public bool ContainsOfferedSupply(SupplyData supply)
    {
        if (supply == null)
            return false;

        IReadOnlyList<ConstructionSupplyOffer> offers = OfferedSupplies;
        if (offers == null)
            return false;

        for (int i = 0; i < offers.Count; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer != null && offer.supply == supply)
                return true;
        }

        return false;
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

        if (string.IsNullOrWhiteSpace(constructionId) && constructionDatabase != null && constructionDatabase.TryGetFirst(out ConstructionData first) && first != null)
            constructionId = first.id;

        if (!IsFinite(currentPosition))
            currentPosition = Vector3.zero;

        if (instanceId < 0)
            instanceId = 0;

        if (siteRuntime == null)
            siteRuntime = new ConstructionSiteRuntime();
        siteRuntime.Sanitize();
        SanitizeRallyTargetSlots();
        if (spriteRenderer == null)
            spriteRenderer = ResolvePrimarySpriteRenderer();
        if (victoryBuildingOverlayRenderer == null)
            victoryBuildingOverlayRenderer = FindVictoryBuildingOverlayRenderer();
        if (hudController == null)
            hudController = GetComponentInChildren<ConstructionHudController>(true);
        if (hudController == null)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || !string.Equals(child.name, "Construction HUD", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                hudController = child.GetComponent<ConstructionHudController>();
                if (hudController == null)
                    hudController = child.gameObject.AddComponent<ConstructionHudController>();
                break;
            }
        }
        if (hudController == null && Application.isPlaying)
            hudController = CreateRuntimeHudController();
        if (hudController != null)
            hudController.RefreshBindings();
        EnsureCapturePointsInitialized();
    }

    private ConstructionHudController CreateRuntimeHudController()
    {
        GameObject hudGo = new GameObject("Construction HUD", typeof(RectTransform), typeof(Canvas));
        hudGo.transform.SetParent(transform, false);
        RectTransform rect = hudGo.GetComponent<RectTransform>();
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        Canvas canvas = hudGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;

        return hudGo.AddComponent<ConstructionHudController>();
    }

    private void ApplyDefaultSiteRuntime(ConstructionData data)
    {
        if (hasSiteRuntimeOverride)
            return;

        if (data == null || data.constructionConfiguration == null)
        {
            siteRuntime = new ConstructionSiteRuntime();
            siteRuntime.Sanitize();
            return;
        }

        siteRuntime = data.constructionConfiguration.Clone();
        EnsureCapturePointsInitialized();
    }

    private void SanitizeRallyTargetSlots()
    {
        if (rallyTargetSlotIndexes == null)
            rallyTargetSlotIndexes = new List<int>();

        for (int i = rallyTargetSlotIndexes.Count - 1; i >= 0; i--)
        {
            int slot = rallyTargetSlotIndexes[i];
            if (slot < 0 || rallyTargetSlotIndexes.IndexOf(slot) != i)
                rallyTargetSlotIndexes.RemoveAt(i);
        }

        if (rallyTargetSlotIndexes.Count == 0 && rallyTargetSlotIndex >= 0)
            rallyTargetSlotIndexes.Add(rallyTargetSlotIndex);

        rallyTargetSlotIndex = rallyTargetSlotIndexes.Count > 0 ? rallyTargetSlotIndexes[0] : -1;
    }

    public bool CanProduceUnitsForTeam(TeamId buyerTeam)
    {
        if (siteRuntime == null)
            return false;
        if (buyerTeam == TeamId.Neutral)
            return false;
        if (buyerTeam != teamId)
            return false;
        if (siteRuntime.offeredUnits == null || siteRuntime.offeredUnits.Count <= 0)
            return false;

        switch (siteRuntime.sellingRule)
        {
            case ConstructionUnitMarketRule.Disabled:
                return false;
            case ConstructionUnitMarketRule.FreeMarket:
                return true;
            case ConstructionUnitMarketRule.OriginalOwner:
                return buyerTeam == originalOwnerTeamId;
            case ConstructionUnitMarketRule.FirstOwner:
                return firstOwnerInitialized && buyerTeam == firstOwnerTeamId;
            default:
                return false;
        }
    }

    private void EnsureCapturePointsInitialized()
    {
        int max = Mathf.Max(0, CapturePointsMax);
        if (currentCapturePoints < 0 || currentCapturePoints > max)
            currentCapturePoints = max;
    }

    private void SyncPositionState()
    {
        if (boardTilemap == null)
            TryAutoAssignBoardTilemap();

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

    private static bool IsFinite(Vector3 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }

#if UNITY_EDITOR
    private void RegisterEditorTick()
    {
        if (editorTickRegistered)
            return;

        EditorApplication.update += HandleEditorTick;
        editorTickRegistered = true;
    }

    private void UnregisterEditorTick()
    {
        if (!editorTickRegistered)
            return;

        EditorApplication.update -= HandleEditorTick;
        editorTickRegistered = false;
    }

    private void HandleEditorTick()
    {
        if (this == null || !isActiveAndEnabled)
            return;
        if (Application.isPlaying)
            return;
        if (IsEditingPrefabContext())
            return;

        if (autoSnapWhenMovedInEditor)
        {
            if (boardTilemap == null)
                TryAutoAssignBoardTilemap();

            if (boardTilemap != null && transform.hasChanged)
            {
                transform.hasChanged = false;
                PullCellFromTransform();
                SnapToCellCenter();
            }
        }

        if (continuousEditorVisualRefresh)
            RefreshRuntimeVisualState(force: false);
    }
#endif

    private void UpdateDynamicName()
    {
#if UNITY_EDITOR
        if (IsEditingPrefabContext())
            return;
#endif

        string baseName = !string.IsNullOrWhiteSpace(constructionDisplayName) ? constructionDisplayName : constructionId;
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "Construction";

        baseName = baseName.Replace(" ", string.Empty);
        int team = (int)teamId;
        int cid = instanceId > 0 ? instanceId : 0;
        gameObject.name = $"{baseName}_T{team}_C{cid}";
    }

    [ContextMenu("Apply From Database")]
    private void ApplyFromDatabaseContext()
    {
        bool ok = ApplyFromDatabase();
        if (!ok)
            Debug.LogWarning("[ConstructionManager] Nao foi possivel aplicar ConstructionData (db/id).", this);
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

    private bool TryGetConstructionData(out ConstructionData data)
    {
        if (constructionDatabase != null
            && !string.IsNullOrWhiteSpace(constructionId)
            && constructionDatabase.TryGetById(constructionId, out data)
            && data != null)
            return true;

        data = null;
        return false;
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

    private void RefreshOccupancyVisualTint(UnitManager occupant)
    {
        if (spriteRenderer == null)
            spriteRenderer = ResolvePrimarySpriteRenderer();
        if (spriteRenderer == null)
            return;

        Color baseColor = TeamUtils.GetColor(teamId);
        Color targetColor = baseColor;

        if (ShouldDarkenForOccupant(occupant))
        {
            float darken = Mathf.Clamp01(occupiedByReadyUnitDarkenFactor);
            targetColor = new Color(baseColor.r * darken, baseColor.g * darken, baseColor.b * darken, baseColor.a);
        }

        if (spriteRenderer.color != targetColor)
            spriteRenderer.color = targetColor;

        bool effectiveVisible = IsRuntimeVisible();
        if (spriteRenderer.enabled != effectiveVisible)
            spriteRenderer.enabled = effectiveVisible;
    }

    private bool ShouldDarkenForOccupant(UnitManager occupant)
    {
        if (occupant == null || !IsOccupantVisibleForHud(occupant))
            return false;
        bool isActiveTeam = matchController != null && (int)occupant.TeamId == matchController.ActiveTeamId;
        return !isActiveTeam || !occupant.HasActed;
    }

    private void RefreshHud(UnitManager occupant)
    {
        if (hudController == null)
            hudController = GetComponentInChildren<ConstructionHudController>(true);
        if (hudController == null)
            EnsureDefaults();
        if (hudController == null)
            return;

        bool effectiveVisible = IsRuntimeVisible();
        if (hudController.gameObject.activeSelf != effectiveVisible)
            hudController.gameObject.SetActive(effectiveVisible);
        if (!effectiveVisible)
            return;

        bool occupantVisible = IsOccupantVisibleForHud(occupant);
        bool hasUnitOnTop = occupant != null && occupantVisible;
        bool showFlagThreatOutline = occupant != null
            && occupantVisible
            && occupant.TeamId != teamId
            && currentCapturePoints <= Mathf.Max(0, occupant.CurrentHP);

        hudController.Apply(
            currentCapturePoints,
            CapturePointsMax,
            IsCapturable,
            teamId,
            hasUnitOnTop,
            showFlagThreatOutline);

        hudController.ApplySectorBadge(AIController.ShowAIHUD, hasUnitOnTop, sector, IsFakeBuilding);
    }

    public void RefreshRuntimeVisualState(bool force = true)
    {
        ApplyPrimaryVisibility();
        RefreshVictoryBuildingOverlayVisual();

        UnitManager occupant = TryGetOccupantOnTop();
        int occupantId = occupant != null ? occupant.GetInstanceID() : 0;
        bool occupantVisible = IsOccupantVisibleForHud(occupant);
        bool shouldDarken = ShouldDarkenForOccupant(occupant);
        bool showFlagThreatOutline = occupant != null
            && occupantVisible
            && occupant.TeamId != teamId
            && currentCapturePoints <= Mathf.Max(0, occupant.CurrentHP);

        if (!force
            && cachedOccupantInstanceId == occupantId
            && cachedOccupantVisible == occupantVisible
            && cachedOccupantShouldDarken == shouldDarken
            && cachedShowFlagThreatOutline == showFlagThreatOutline)
        {
            return;
        }

        cachedOccupantInstanceId = occupantId;
        cachedOccupantVisible = occupantVisible;
        cachedOccupantShouldDarken = shouldDarken;
        cachedShowFlagThreatOutline = showFlagThreatOutline;
        RefreshOccupancyVisualTint(occupant);
        RefreshHud(occupant);
    }

    private static bool IsOccupantVisibleForHud(UnitManager occupant)
    {
        if (occupant == null)
            return false;
        if (!Application.isPlaying)
            return true;

        return !occupant.IsDead && !occupant.IsHiddenByFogOfWar;
    }

    private void RefreshVictoryBuildingOverlayVisual()
    {
        bool shouldShow = IsRuntimeVisible() && siteRuntime != null && siteRuntime.isVictoryBuilding;
        if (!shouldShow && victoryBuildingOverlayRenderer == null)
            return;

        if (shouldShow && victoryBuildingOverlayRenderer == null)
            victoryBuildingOverlayRenderer = EnsureVictoryBuildingOverlayRenderer();
        if (victoryBuildingOverlayRenderer == null)
            return;

        victoryBuildingOverlayRenderer.gameObject.SetActive(shouldShow);
        if (!shouldShow)
            return;

        victoryBuildingOverlayRenderer.sprite = victoryBuildingOverlaySprite;
        victoryBuildingOverlayRenderer.color = victoryBuildingOverlayColor;
        Transform overlayTransform = victoryBuildingOverlayRenderer.transform;
        overlayTransform.localPosition = victoryBuildingOverlayLocalPosition;
        overlayTransform.localRotation = Quaternion.identity;
        overlayTransform.localScale = victoryBuildingOverlayLocalScale;

        if (spriteRenderer != null)
        {
            victoryBuildingOverlayRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            victoryBuildingOverlayRenderer.sortingOrder = spriteRenderer.sortingOrder + victoryBuildingOverlaySortingOrderOffset;
        }
    }

    private SpriteRenderer EnsureVictoryBuildingOverlayRenderer()
    {
        SpriteRenderer existing = FindVictoryBuildingOverlayRenderer();
        if (existing != null)
            return existing;

        GameObject overlayGo = new GameObject(VictoryBuildingOverlayObjectName);
        overlayGo.transform.SetParent(transform, false);
        overlayGo.transform.localPosition = victoryBuildingOverlayLocalPosition;
        overlayGo.transform.localRotation = Quaternion.identity;
        overlayGo.transform.localScale = victoryBuildingOverlayLocalScale;

        SpriteRenderer overlayRenderer = overlayGo.AddComponent<SpriteRenderer>();
        overlayRenderer.sprite = victoryBuildingOverlaySprite;
        overlayRenderer.color = victoryBuildingOverlayColor;
        return overlayRenderer;
    }

    private SpriteRenderer FindVictoryBuildingOverlayRenderer()
    {
        if (victoryBuildingOverlayRenderer != null)
            return victoryBuildingOverlayRenderer;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || !string.Equals(child.name, VictoryBuildingOverlayObjectName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer != null)
                return renderer;
        }

        return null;
    }

    private SpriteRenderer ResolvePrimarySpriteRenderer()
    {
        SpriteRenderer ownRenderer = GetComponent<SpriteRenderer>();
        if (ownRenderer != null)
            return ownRenderer;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate == null)
                continue;
            if (victoryBuildingOverlayRenderer != null && candidate == victoryBuildingOverlayRenderer)
                continue;
            if (string.Equals(candidate.gameObject.name, VictoryBuildingOverlayObjectName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            return candidate;
        }

        return null;
    }

    private void ApplyPrimaryVisibility()
    {
        bool effectiveVisible = IsRuntimeVisible();
        if (spriteRenderer == null)
            spriteRenderer = ResolvePrimarySpriteRenderer();
        if (spriteRenderer != null && spriteRenderer.enabled != effectiveVisible)
            spriteRenderer.enabled = effectiveVisible;

        if (!effectiveVisible && victoryBuildingOverlayRenderer != null && victoryBuildingOverlayRenderer.gameObject.activeSelf)
            victoryBuildingOverlayRenderer.gameObject.SetActive(false);

        if (!effectiveVisible && hudController != null && hudController.gameObject.activeSelf)
            hudController.gameObject.SetActive(false);
    }

    private bool IsRuntimeVisible()
    {
        return !Application.isPlaying || isVisible;
    }

    private void HandleActiveTeamChanged(int _)
    {
        double startMs = Time.realtimeSinceStartupAsDouble * 1000d;
        RefreshRuntimeVisualState(force: true);
        activeTeamChangedHandlerCount++;
        activeTeamChangedHandlerTotalMs += (Time.realtimeSinceStartupAsDouble * 1000d) - startMs;
    }

    private void HandleSlotConfigChanged()
    {
        ResolveTeamIdFromSlot();
        RefreshRuntimeVisualState(force: true);
    }

    private void HandleFogOfWarUpdated()
    {
        RefreshRuntimeVisualState(force: false);
    }

    // Resolve o teamId a partir do slotIndex no MatchController.
    // slotIndex == -1 forca Neutral fixo; sem MatchController, mantem o teamId atual.
    private void ResolveTeamIdFromSlot()
    {
        TeamId resolved = teamId;
        bool canResolve = true;

        if (slotIndex < 0)
        {
            resolved = TeamId.Neutral;
        }
        else
        {
            if (matchController == null)
                matchController = FindAnyObjectByType<MatchController>();

            if (matchController == null)
            {
                canResolve = false;
            }
            else
            {
                resolved = matchController.GetTeamIdForSlot(slotIndex);
            }
        }

        if (!canResolve)
            return;

        if (teamId != resolved)
        {
            teamId = resolved;

            if (!ApplyFromDatabase())
                UpdateDynamicName();

            RefreshRuntimeVisualState(force: true);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    private void HandleUnitOccupancyChanged(UnitManager unit, Vector3Int previousCell, Vector3Int currentCell)
    {
        if (unit == null || unit.BoardTilemap == null || boardTilemap == null)
            return;
        if (unit.BoardTilemap != boardTilemap)
            return;
        if (unit.gameObject.scene != gameObject.scene)
            return;

        previousCell.z = 0;
        currentCell.z = 0;
        Vector3Int ownCell = currentCellPosition;
        ownCell.z = 0;
        if (previousCell != ownCell && currentCell != ownCell)
            return;

        RefreshRuntimeVisualState(force: false);
    }

    private void HandleUnitActedStateChanged(UnitManager unit)
    {
        if (unit == null || unit.gameObject.scene != gameObject.scene || unit.IsEmbarked)
            return;

        Vector3Int ownCell = currentCellPosition;
        ownCell.z = 0;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;

        if (unit.BoardTilemap != null && boardTilemap != null && unit.BoardTilemap == boardTilemap)
        {
            unitCell = HexCoordinates.WorldToCell(boardTilemap, unit.transform.position);
            unitCell.z = 0;
        }

        if (unitCell != ownCell)
            return;

        RefreshRuntimeVisualState(force: true);
    }

    private UnitManager TryGetOccupantOnTop()
    {
        Tilemap map = boardTilemap;
        if (map == null)
            TryAutoAssignBoardTilemap();
        map = boardTilemap;
        if (map != null)
        {
            UnitManager byCell = UnitOccupancyRules.GetUnitAtCell(map, currentCellPosition);
            if (byCell != null)
                return byCell;
        }

        // Runtime fallback: avoid global scene scans when tilemap references are inconsistent.
        if (Application.isPlaying)
        {
            List<UnitManager> units = UnitManager.AllActive;
            for (int i = 0; i < units.Count; i++)
            {
                UnitManager unit = units[i];
                if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                    continue;

                Vector3Int unitCell = unit.CurrentCellPosition;
                Tilemap unitMap = unit.BoardTilemap != null ? unit.BoardTilemap : map;
                if (unitMap != null)
                    unitCell = HexCoordinates.WorldToCell(unitMap, unit.transform.position);

                unitCell.z = 0;
                if (unitCell == currentCellPosition)
                    return unit;
            }

            return null;
        }

        // Editor fallback for manual scene edits.
        UnitManager[] editorUnits = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < editorUnits.Length; i++)
        {
            UnitManager unit = editorUnits[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;

            Vector3Int unitCell = unit.CurrentCellPosition;
            Tilemap unitMap = unit.BoardTilemap != null ? unit.BoardTilemap : map;
            if (unitMap != null)
                unitCell = HexCoordinates.WorldToCell(unitMap, unit.transform.position);

            unitCell.z = 0;
            if (unitCell == currentCellPosition)
                return unit;
        }

        return null;
    }
}

