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
    [SerializeField] private int currentHP;
    [SerializeField] private bool autoApplyOnStart = true;

    public TeamId TeamId => teamId;
    public Tilemap BoardTilemap => boardTilemap;
    public Vector3Int CurrentCellPosition => currentCellPosition;
    public string ConstructionId => constructionId;
    public int InstanceId => instanceId;
    public Vector3 CurrentPosition => currentPosition;
    public string ConstructionDisplayName => constructionDisplayName;
    public int CurrentHP => currentHP;
    public ConstructionDatabase ConstructionDatabase => constructionDatabase;

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

    public IReadOnlyList<TerrainSkillCostOverride> GetSkillCostOverrides()
    {
        if (TryGetConstructionData(out ConstructionData data) && data.skillCostOverrides != null)
            return data.skillCostOverrides;

        return System.Array.Empty<TerrainSkillCostOverride>();
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
        TryAutoAssignBoardTilemap();
        SyncPositionState();
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
        if (autoApplyOnStart)
            ApplyFromDatabase();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        EnsureDefaults();
        TryAutoAssignBoardTilemap();

        if (IsEditingPrefabContext())
            return;

        SyncPositionState();
        if (!ApplyFromDatabase())
            UpdateDynamicName();
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
        }

        if (currentHP <= 0 || currentHP > data.maxHP)
            currentHP = data.maxHP;

        currentPosition = transform.position;
        UpdateDynamicName();
    }

    public void SetCurrentHP(int value)
    {
        int max = GetMaxHP();
        currentHP = Mathf.Clamp(value, 0, max);
    }

    public int GetMaxHP()
    {
        if (constructionDatabase != null && !string.IsNullOrWhiteSpace(constructionId) && constructionDatabase.TryGetById(constructionId, out ConstructionData data))
            return Mathf.Max(1, data.maxHP);

        return Mathf.Max(1, currentHP);
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
            UpdateDynamicName();
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
        currentCellPosition = cell;
        SnapToCellCenter();
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
}
