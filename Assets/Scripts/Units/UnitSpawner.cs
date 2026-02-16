using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class UnitSpawner : MonoBehaviour
{
    [System.Serializable]
    public class MapSpawnEntry
    {
        public TeamId teamId = TeamId.Green;
        public string unitId;
        public Vector3Int cellPosition = Vector3Int.zero;
    }

    [Header("Data")]
    [SerializeField] private UnitDatabase unitDatabase;
    [SerializeField] private MatchController matchController;
    [SerializeField] private int currentId = 1;

    [Header("Spawn Template")]
    [Tooltip("Prefab unico de molde (Assets/Prefab/unit.prefab). No spawn, sprite/nome sao aplicados via UnitData e a autonomia atual inicia cheia com base em UnitData.autonomia.")]
    [SerializeField] private GameObject unitPrefab;
    [SerializeField] private Tilemap boardTilemap;
    [Tooltip("Se true, unidade nasce com 'ja agiu' = false. Se false, nasce com 'ja agiu' = true.")]
    [SerializeField] private bool spawnWithHasActedFalse = true;

    [SerializeField] private Transform spawnParent;

    [Header("Manual Spawn")]
    [SerializeField] private TeamId manualTeamId = TeamId.Green;
    [SerializeField] private string manualUnitId;
    [SerializeField] private Vector3Int manualCellPosition = Vector3Int.zero;

    [Header("Map Spawn")]
    [SerializeField] private bool spawnMapListOnStart = false;
    [SerializeField] private List<MapSpawnEntry> mapSpawnEntries = new List<MapSpawnEntry>();

    private bool mapSpawnExecuted;

    private void Start()
    {
        TryAutoAssignMatchController();
        if (spawnMapListOnStart)
            SpawnMapList();
    }

    public GameObject Spawn(string unitId, Vector3 position, Quaternion rotation)
    {
        return Spawn(unitId, TeamId.Green, position, rotation);
    }

    public GameObject Spawn(string unitId, TeamId teamId, Vector3 position, Quaternion rotation)
    {
        if (boardTilemap != null)
        {
            Vector3Int targetCell = HexCoordinates.WorldToCell(boardTilemap, position);
            targetCell.z = 0;
            int targetSortingLayerId = GetTargetSortingLayerId();
            if (IsCellOccupiedOnSortingLayer(targetCell, targetSortingLayerId))
            {
                Debug.LogWarning($"[UnitSpawner] Celula ocupada na mesma sorting layer em ({targetCell.x},{targetCell.y},0). Spawn cancelado.");
                return null;
            }
        }

        if (unitDatabase == null)
        {
            Debug.LogError("[UnitSpawner] UnitDatabase nao setado.");
            return null;
        }

        if (!unitDatabase.TryGetById(unitId, out UnitData data))
        {
            Debug.LogWarning($"[UnitSpawner] Unidade '{unitId}' nao encontrada no banco.");
            return null;
        }

        return Spawn(data, teamId, position, rotation);
    }

    public GameObject Spawn(UnitData data, Vector3 position, Quaternion rotation)
    {
        return Spawn(data, TeamId.Green, position, rotation);
    }

    public GameObject Spawn(UnitData data, TeamId teamId, Vector3 position, Quaternion rotation)
    {
        if (data == null)
        {
            Debug.LogWarning("[UnitSpawner] UnitData nulo.");
            return null;
        }

        if (unitPrefab == null)
        {
            Debug.LogError("[UnitSpawner] unitPrefab nao setado. Aponte para Assets/Prefab/unit.prefab.");
            return null;
        }

        Transform parent = spawnParent != null ? spawnParent : null;
        GameObject instance = Instantiate(unitPrefab, position, rotation, parent);

        UnitManager manager = instance.GetComponent<UnitManager>();
        if (manager != null)
        {
            manager.AssignSpawnInstanceId(GetNextId());
            manager.SetBoardTilemap(boardTilemap);
            manager.SetTeamId(teamId);
            manager.Setup(unitDatabase, data.id);
            if (boardTilemap != null)
                manager.SetCurrentCellPosition(HexCoordinates.WorldToCell(boardTilemap, position));
            else
                manager.SetCurrentPosition(position);
            manager.Apply(data);
            manager.SetAutonomia(data.autonomia, true);
            if (spawnWithHasActedFalse)
                manager.ResetActed();
            else
                manager.MarkAsActed();
            return instance;
        }

        // Backward compatibility with older prefab setup.
        UnitInstanceView view = instance.GetComponent<UnitInstanceView>();
        if (view == null)
            view = instance.AddComponent<UnitInstanceView>();

        view.Setup(unitDatabase, data.id);
        view.Apply(data);
        return instance;
    }

    public GameObject SpawnAt(string unitId, Vector3 position)
    {
        return Spawn(unitId, position, Quaternion.identity);
    }

    public GameObject SpawnAt(string unitId, TeamId teamId, Vector3 position)
    {
        return Spawn(unitId, teamId, position, Quaternion.identity);
    }

    public GameObject SpawnAt(UnitData data, Vector3 position)
    {
        return Spawn(data, position, Quaternion.identity);
    }

    public GameObject SpawnAt(UnitData data, TeamId teamId, Vector3 position)
    {
        return Spawn(data, teamId, position, Quaternion.identity);
    }

    public GameObject SpawnAtCell(string unitId, Vector3Int cell)
    {
        return SpawnAtCell(unitId, TeamId.Green, cell);
    }

    public GameObject SpawnAtCell(string unitId, TeamId teamId, Vector3Int cell)
    {
        if (boardTilemap == null)
        {
            Debug.LogError("[UnitSpawner] boardTilemap nao setado para SpawnAtCell.");
            return null;
        }

        Vector3Int fixedCell = new Vector3Int(cell.x, cell.y, 0);
        int targetSortingLayerId = GetTargetSortingLayerId();
        if (IsCellOccupiedOnSortingLayer(fixedCell, targetSortingLayerId))
        {
            Debug.LogWarning($"[UnitSpawner] Celula ocupada na mesma sorting layer em ({fixedCell.x},{fixedCell.y},0). Spawn cancelado.");
            return null;
        }

        return Spawn(unitId, teamId, HexCoordinates.GetCellCenterWorld(boardTilemap, fixedCell), Quaternion.identity);
    }

    public GameObject SpawnAtCell(UnitData data, Vector3Int cell)
    {
        return SpawnAtCell(data, TeamId.Green, cell);
    }

    public GameObject SpawnAtCell(UnitData data, TeamId teamId, Vector3Int cell)
    {
        if (boardTilemap == null)
        {
            Debug.LogError("[UnitSpawner] boardTilemap nao setado para SpawnAtCell.");
            return null;
        }

        Vector3Int fixedCell = new Vector3Int(cell.x, cell.y, 0);
        int targetSortingLayerId = GetTargetSortingLayerId();
        if (IsCellOccupiedOnSortingLayer(fixedCell, targetSortingLayerId))
        {
            Debug.LogWarning($"[UnitSpawner] Celula ocupada na mesma sorting layer em ({fixedCell.x},{fixedCell.y},0). Spawn cancelado.");
            return null;
        }

        return Spawn(data, teamId, HexCoordinates.GetCellCenterWorld(boardTilemap, fixedCell), Quaternion.identity);
    }

    public void SpawnManual()
    {
        Vector3Int fixedCell = new Vector3Int(manualCellPosition.x, manualCellPosition.y, 0);
        SpawnAtCell(manualUnitId, manualTeamId, fixedCell);
    }

    public void SpawnMapList(bool force = false)
    {
        if (!CanRunMapSpawn())
        {
            Debug.LogWarning("[UnitSpawner] Map Spawn so pode rodar no turno 0 do MatchController.");
            return;
        }

        if (mapSpawnExecuted && !force)
            return;

        for (int i = 0; i < mapSpawnEntries.Count; i++)
        {
            MapSpawnEntry entry = mapSpawnEntries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.unitId))
                continue;

            Vector3Int fixedCell = new Vector3Int(entry.cellPosition.x, entry.cellPosition.y, 0);
            SpawnAtCell(entry.unitId, entry.teamId, fixedCell);
        }

        mapSpawnExecuted = true;
    }

    private bool CanRunMapSpawn()
    {
        TryAutoAssignMatchController();
        return matchController != null && matchController.CurrentTurn == 0;
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

    private int GetTargetSortingLayerId()
    {
        if (unitPrefab == null)
            return 0;

        SpriteRenderer renderer = unitPrefab.GetComponentInChildren<SpriteRenderer>();
        return renderer != null ? renderer.sortingLayerID : 0;
    }

    private bool IsCellOccupiedOnSortingLayer(Vector3Int cell, int sortingLayerId)
    {
        if (boardTilemap == null)
            return false;

        // Regra global: no maximo 1 unidade por hex.
        if (UnitRulesDefinition.IsUnitCellOccupied(boardTilemap, cell))
            return true;

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

        return false;
    }
}
