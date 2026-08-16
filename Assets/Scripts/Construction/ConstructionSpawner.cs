using UnityEngine;
using UnityEngine.Tilemaps;

public class ConstructionSpawner : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ConstructionDatabase constructionDatabase;
    [SerializeField] private MatchController matchController;
    [SerializeField] private int currentId = 1;

    [Header("Spawn Template")]
    [Tooltip("Prefab unico de molde (Assets/Prefab/construction.prefab). O sprite e nome sao aplicados via ConstructionData no spawn.")]
    [SerializeField] private GameObject constructionPrefab;
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private Transform spawnParent;

    public ConstructionDatabase ConstructionDatabase => constructionDatabase;

    private void Start()
    {
        TryAutoAssignMatchController();
        // RecomputeTeamFlips (nao AutoCompute direto): o auto cru sobrescrevia o
        // override manual de flip configurado no MatchController.
        if (matchController != null)
            matchController.RecomputeTeamFlips();
    }

    public GameObject Spawn(string constructionId, TeamId teamId, Vector3 position, Quaternion rotation)
    {
        if (boardTilemap != null)
        {
            Vector3Int targetCell = HexCoordinates.WorldToCell(boardTilemap, position);
            targetCell.z = 0;
            int targetSortingLayerId = GetTargetSortingLayerId();
            if (IsCellOccupiedOnSortingLayer(targetCell, targetSortingLayerId))
            {
                Debug.LogWarning($"[ConstructionSpawner] Celula ocupada na mesma sorting layer em ({targetCell.x},{targetCell.y},0). Spawn cancelado.");
                return null;
            }
        }

        if (constructionDatabase == null)
        {
            Debug.LogError("[ConstructionSpawner] ConstructionDatabase nao setado.");
            return null;
        }

        if (!constructionDatabase.TryGetById(constructionId, out ConstructionData data))
        {
            Debug.LogWarning($"[ConstructionSpawner] Construcao '{constructionId}' nao encontrada no banco.");
            return null;
        }

        return Spawn(data, teamId, position, rotation);
    }

    public GameObject Spawn(ConstructionData data, TeamId teamId, Vector3 position, Quaternion rotation)
    {
        if (data == null)
        {
            Debug.LogWarning("[ConstructionSpawner] ConstructionData nulo.");
            return null;
        }

        if (constructionPrefab == null)
        {
            Debug.LogError("[ConstructionSpawner] constructionPrefab nao setado. Aponte para Assets/Prefab/construction.prefab.");
            return null;
        }

        Transform parent = spawnParent != null ? spawnParent : null;
        GameObject instance = Instantiate(constructionPrefab, position, rotation, parent);

        ConstructionManager manager = instance.GetComponent<ConstructionManager>();
        if (manager == null)
            manager = instance.AddComponent<ConstructionManager>();

        manager.AssignSpawnInstanceId(GetNextId());
        manager.SetBoardTilemap(boardTilemap);
        manager.InitializeOwnershipForSpawn(teamId);
        manager.Setup(constructionDatabase, data.id);
        if (boardTilemap != null)
            manager.SetCurrentCellPosition(HexCoordinates.WorldToCell(boardTilemap, position));
        else
            manager.SetCurrentPosition(position);
        manager.Apply(data);
        return instance;
    }

    public bool TryGetConstructionData(string constructionId, out ConstructionData data)
    {
        data = null;
        if (constructionDatabase == null || string.IsNullOrWhiteSpace(constructionId))
            return false;

        return constructionDatabase.TryGetById(constructionId, out data);
    }

    public GameObject SpawnAtCell(string constructionId, TeamId teamId, Vector3Int cell)
    {
        if (boardTilemap == null)
        {
            Debug.LogError("[ConstructionSpawner] boardTilemap nao setado para SpawnAtCell.");
            return null;
        }

        Vector3Int fixedCell = new Vector3Int(cell.x, cell.y, 0);
        int targetSortingLayerId = GetTargetSortingLayerId();
        if (IsCellOccupiedOnSortingLayer(fixedCell, targetSortingLayerId))
        {
            Debug.LogWarning($"[ConstructionSpawner] Celula ocupada na mesma sorting layer em ({fixedCell.x},{fixedCell.y},0). Spawn cancelado.");
            return null;
        }

        return Spawn(constructionId, teamId, HexCoordinates.GetCellCenterWorld(boardTilemap, fixedCell), Quaternion.identity);
    }

    private void TryAutoAssignMatchController()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
    }

    private int GetNextId()
    {
        if (currentId < 1)
            currentId = 1;

        int id = currentId;
        currentId++;
        return id;
    }

    public void EnsureNextIdAbove(int usedId)
    {
        if (currentId <= usedId)
            currentId = usedId + 1;
    }

    public void SetNextIdAfterMax(int maxUsedId)
    {
        currentId = Mathf.Max(1, maxUsedId + 1);
    }

    private int GetTargetSortingLayerId()
    {
        if (constructionPrefab == null)
            return 0;

        SpriteRenderer renderer = constructionPrefab.GetComponentInChildren<SpriteRenderer>();
        return renderer != null ? renderer.sortingLayerID : 0;
    }

    private bool IsCellOccupiedOnSortingLayer(Vector3Int cell, int sortingLayerId)
    {
        if (boardTilemap == null)
            return false;

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.gameObject.activeInHierarchy)
                continue;

            SpriteRenderer renderer = construction.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null || renderer.sortingLayerID != sortingLayerId)
                continue;

            Vector3Int occupiedCell = construction.BoardTilemap == boardTilemap
                ? construction.CurrentCellPosition
                : HexCoordinates.WorldToCell(boardTilemap, construction.transform.position);

            occupiedCell.z = 0;
            if (occupiedCell == cell)
                return true;
        }

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;

            SpriteRenderer renderer = unit.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null || renderer.sortingLayerID != sortingLayerId)
                continue;

            Vector3Int occupiedCell = unit.BoardTilemap == boardTilemap
                ? unit.CurrentCellPosition
                : HexCoordinates.WorldToCell(boardTilemap, unit.transform.position);

            occupiedCell.z = 0;
            if (occupiedCell == cell)
                return true;
        }

        return false;
    }
}
