using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class ConstructionManager : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private ConstructionDatabase constructionDatabase;
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private bool snapToCellCenter = true;
    [SerializeField] private bool autoSnapWhenMovedInEditor = true;
    [SerializeField] private Vector3Int currentCellPosition = Vector3Int.zero;
    [SerializeField] private TeamId teamId = TeamId.Green;
    [SerializeField] private string constructionId;
    [SerializeField] private int instanceId;
    [SerializeField] private Vector3 currentPosition = Vector3.zero;
    [SerializeField] private string constructionDisplayName;
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

    public TeamId TeamId => teamId;
    public Tilemap BoardTilemap => boardTilemap;
    public Vector3Int CurrentCellPosition => currentCellPosition;
    public string ConstructionId => constructionId;
    public int InstanceId => instanceId;
    public Vector3 CurrentPosition => currentPosition;
    public string ConstructionDisplayName => constructionDisplayName;
    public ConstructionDatabase ConstructionDatabase => constructionDatabase;
    public bool IsCapturable => siteRuntime != null && siteRuntime.isCapturable;
    public int CapturePointsMax => siteRuntime != null ? siteRuntime.capturePointsMax : 0;
    public int CurrentCapturePoints => currentCapturePoints;
    public bool CanProduceUnits => CanProduceUnitsForTeam(teamId);
    public bool CanProvideSupplies => siteRuntime != null && siteRuntime.canProvideSupplies;
    public bool HasInfiniteSuppliesOverride => hasInfiniteSuppliesOverride;
    public bool IsPlayerHeadQuarter => siteRuntime != null && siteRuntime.isPlayerHeadQuarter;
    public int CapturedIncoming => siteRuntime != null ? Mathf.Max(0, siteRuntime.capturedIncoming) : 0;
    public IReadOnlyList<ServiceData> OfferedServices => siteRuntime != null && siteRuntime.offeredServices != null ? siteRuntime.offeredServices : System.Array.Empty<ServiceData>();
    public IReadOnlyList<UnitData> OfferedUnits => siteRuntime != null && siteRuntime.offeredUnits != null ? siteRuntime.offeredUnits : System.Array.Empty<UnitData>();
    public IReadOnlyList<ConstructionSupplyOffer> OfferedSupplies => siteRuntime != null && siteRuntime.offeredSupplies != null ? siteRuntime.offeredSupplies : System.Array.Empty<ConstructionSupplyOffer>();
    public TeamId OriginalOwnerTeamId => originalOwnerTeamId;
    public bool HasOriginalOwner => originalOwnerInitialized;
    public TeamId FirstOwnerTeamId => firstOwnerTeamId;
    public bool HasFirstOwner => firstOwnerInitialized;

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
        SyncPositionState();
        RefreshHud();
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
        if (autoApplyOnStart)
            ApplyFromDatabase();

        RefreshHud();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && IsEditingPrefabContext())
            return;
#endif

        RefreshOccupancyVisualTint();
        RefreshHud();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        EnsureDefaults();
        TryAutoAssignMatchController();
        TryAutoAssignBoardTilemap();

        if (IsEditingPrefabContext())
            return;

        SyncPositionState();
        if (!ApplyFromDatabase())
            UpdateDynamicName();
        RefreshHud();
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
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

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
        RefreshHud();
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
        RefreshHud();
        ThreatRevisionTracker.NotifyConstructionTeamChanged(previousTeam, teamId);
    }

    public void ApplyTeamVisualFlipX(bool flipX)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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
        RefreshHud();
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
        RefreshHud();
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
            RefreshHud();
            return;
        }

        siteRuntime = runtime.Clone();
        hasSiteRuntimeOverride = true;
        RefreshHud();
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
        RefreshHud();
    }

    public void SetInfiniteSuppliesOverride(bool value)
    {
        hasInfiniteSuppliesOverride = value;
        RefreshHud();
    }

    public bool HasInfiniteSuppliesFor(SupplyData supply = null)
    {
        if (hasInfiniteSuppliesOverride)
        {
            if (supply == null)
                return true;
            return ContainsOfferedSupply(supply);
        }

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

        IReadOnlyList<ConstructionSupplyOffer> offers = OfferedSupplies;
        if (offers == null)
            return false;

        for (int i = 0; i < offers.Count; i++)
        {
            ConstructionSupplyOffer offer = offers[i];
            if (offer == null || offer.supply == null)
                continue;
            if (supply != null && offer.supply != supply)
                continue;
            if (offer.quantity >= int.MaxValue)
                return true;
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
        if (hudController != null)
            hudController.RefreshBindings();
        EnsureCapturePointsInitialized();
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
        if (boardTilemap != null)
            return;

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

    private void RefreshOccupancyVisualTint()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
            return;

        Color baseColor = TeamUtils.GetColor(teamId);
        Color targetColor = baseColor;

        UnitManager occupant = TryGetOccupantOnTop();
        bool sameTeam = occupant != null && occupant.TeamId == teamId;
        if (sameTeam && !occupant.HasActed)
        {
            float darken = Mathf.Clamp01(occupiedByReadyUnitDarkenFactor);
            targetColor = new Color(baseColor.r * darken, baseColor.g * darken, baseColor.b * darken, baseColor.a);
        }

        if (spriteRenderer.color != targetColor)
            spriteRenderer.color = targetColor;
    }

    private void RefreshHud()
    {
        if (hudController == null)
            hudController = GetComponentInChildren<ConstructionHudController>(true);
        if (hudController == null)
            return;

        UnitManager occupant = TryGetOccupantOnTop();
        bool hasUnitOnTop = occupant != null;
        bool showFlagThreatOutline = occupant != null
            && occupant.TeamId != teamId
            && currentCapturePoints <= Mathf.Max(0, occupant.CurrentHP);

        hudController.Apply(
            currentCapturePoints,
            CapturePointsMax,
            IsCapturable,
            teamId,
            hasUnitOnTop,
            showFlagThreatOutline);
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

        // Fallback para cenarios com referencias de tilemap inconsistentes na cena.
        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;

            // In editor, CurrentCellPosition can lag behind if the unit was moved manually.
            // Recompute by world position to keep occupancy tint/HUD consistent with in-game behavior.
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
