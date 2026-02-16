using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class RoadNetworkManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StructureDatabase structureDatabase;
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    [Header("Road Visual")]
    [SerializeField] private Material roadMaterial;
    [SerializeField] private Color roadColor = Color.white;
    [SerializeField] [Range(0.03f, 0.6f)] private float roadWidth = 0.16f;
    [SerializeField] private SortingLayerReference sortingLayer;
    [SerializeField, HideInInspector] private bool sortingLayerInitialized;
    [SerializeField] [Range(-100, 500)] private int sortingOrder = 20;

    [Header("Validation")]
    [Tooltip("Se true, so desenha rota em celulas cujo terreno suporte Land/Surface.")]
    [SerializeField] private bool enforceLandSurfaceCells = true;
    [SerializeField] private bool logInvalidRoadCells = true;
    [Header("Live Preview")]
    [SerializeField] private bool livePreviewInEditor = true;
    [SerializeField] private bool livePreviewInPlayMode = false;
    [SerializeField] [Range(0.05f, 1f)] private float livePreviewInterval = 0.2f;

    private readonly List<GameObject> generatedRoadObjects = new List<GameObject>();
    private float nextLivePreviewTime;
    private int lastPreviewSignature = int.MinValue;

    public Tilemap BoardTilemap => boardTilemap;
    public StructureDatabase StructureDatabase => structureDatabase;

    private void Awake()
    {
        EnsureDefaults();
        TryAutoAssignBoardTilemap();
        RebuildRoadVisuals();
    }

    private void OnEnable()
    {
        EnsureDefaults();
        TryAutoAssignBoardTilemap();
        RebuildRoadVisuals();
    }

    private void Start()
    {
        // Garantia extra para Play Mode.
        RebuildRoadVisuals();
    }

    private void Update()
    {
        bool shouldPreview = Application.isPlaying ? livePreviewInPlayMode : livePreviewInEditor;
        if (!shouldPreview)
            return;

        if (Time.realtimeSinceStartup < nextLivePreviewTime)
            return;

        nextLivePreviewTime = Time.realtimeSinceStartup + Mathf.Max(0.05f, livePreviewInterval);
        TryRebuildIfChanged();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureDefaults();
        TryAutoAssignBoardTilemap();
        RebuildRoadVisuals();
    }
#endif

    [ContextMenu("Rebuild Road Visuals")]
    public void RebuildRoadVisuals()
    {
        ClearRoadVisuals();
        if (structureDatabase == null || boardTilemap == null)
        {
            if (logInvalidRoadCells)
                Debug.LogWarning("[RoadNetworkManager] Sem StructureDatabase ou BoardTilemap. Nada para desenhar.", this);
            return;
        }

        IReadOnlyList<StructureData> structures = structureDatabase.Structures;
        int drawnRoutes = 0;
        for (int s = 0; s < structures.Count; s++)
        {
            StructureData structure = structures[s];
            if (structure == null || structure.roadRoutes == null)
                continue;

            for (int r = 0; r < structure.roadRoutes.Count; r++)
            {
                RoadRouteDefinition route = structure.roadRoutes[r];
                if (route == null || route.cells == null || route.cells.Count < 2)
                    continue;
                if (!IsRouteValid(route))
                {
                    if (logInvalidRoadCells)
                    {
                        string routeName = route != null && !string.IsNullOrWhiteSpace(route.routeName) ? route.routeName : $"route_{r}";
                        string structureName = !string.IsNullOrWhiteSpace(structure.id) ? structure.id : structure.name;
                        Debug.LogWarning($"[RoadNetworkManager] Rota ignorada: {structureName}/{routeName}.", this);
                    }
                    continue;
                }

                CreateRouteLine(structure, route, r);
                drawnRoutes++;
            }
        }

        lastPreviewSignature = ComputePreviewSignature();
        if (logInvalidRoadCells)
            Debug.Log($"[RoadNetworkManager] Rotas desenhadas: {drawnRoutes}.", this);
    }

    [ContextMenu("Clear Road Visuals")]
    public void ClearRoadVisuals()
    {
        for (int i = 0; i < generatedRoadObjects.Count; i++)
        {
            GameObject go = generatedRoadObjects[i];
            if (go == null)
                continue;

            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        generatedRoadObjects.Clear();
    }

    private void CreateRouteLine(StructureData structure, RoadRouteDefinition route, int routeIndex)
    {
        GameObject go = new GameObject(GetRouteObjectName(structure, route, routeIndex));
        go.transform.SetParent(transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 0;
        lr.numCornerVertices = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.startWidth = roadWidth;
        lr.endWidth = roadWidth;
        lr.startColor = roadColor;
        lr.endColor = roadColor;
        lr.material = roadMaterial != null ? roadMaterial : new Material(Shader.Find("Sprites/Default"));
        if (sortingLayer.Id != 0)
            lr.sortingLayerID = sortingLayer.Id;
        lr.sortingOrder = sortingOrder;

        lr.positionCount = route.cells.Count;
        for (int i = 0; i < route.cells.Count; i++)
        {
            Vector3Int cell = route.cells[i];
            cell.z = 0;
            Vector3 world = boardTilemap.GetCellCenterWorld(cell);
            world.z = boardTilemap.transform.position.z;
            lr.SetPosition(i, world);
        }

        generatedRoadObjects.Add(go);
    }

    private bool IsRouteValid(RoadRouteDefinition route)
    {
        if (!enforceLandSurfaceCells)
            return true;

        for (int i = 0; i < route.cells.Count; i++)
        {
            Vector3Int cell = route.cells[i];
            cell.z = 0;

            // Mesma regra do movimento: construcao sobrescreve terreno.
            ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
            if (construction != null)
            {
                if (construction.SupportsLayerMode(Domain.Land, HeightLevel.Surface))
                    continue;

                if (logInvalidRoadCells)
                    Debug.LogWarning($"[RoadNetworkManager] Rota invalida em ({cell.x},{cell.y}): construcao no hex nao suporta Land/Surface.");
                return false;
            }

            if (!TryGetAnyPaintedTileOnGrid(cell, out TileBase anyPaintedTile))
            {
                if (logInvalidRoadCells)
                    Debug.LogWarning($"[RoadNetworkManager] Rota invalida em ({cell.x},{cell.y}): celula sem tile no grid.");
                return false;
            }

            if (terrainDatabase == null)
                continue;

            if (!TryResolveTerrainAtCell(cell, anyPaintedTile, out TerrainTypeData terrain) || terrain == null)
            {
                if (logInvalidRoadCells)
                    Debug.LogWarning($"[RoadNetworkManager] Rota invalida em ({cell.x},{cell.y}): tile sem mapeamento no TerrainDatabase.");
                return false;
            }

            bool supportsLandSurface =
                (terrain.domain == Domain.Land && terrain.heightLevel == HeightLevel.Surface) ||
                TerrainSupportsLayer(terrain.additionalLayerModes, Domain.Land, HeightLevel.Surface);

            if (!supportsLandSurface)
            {
                if (logInvalidRoadCells)
                    Debug.LogWarning($"[RoadNetworkManager] Rota invalida em ({cell.x},{cell.y}). Terreno nao suporta Land/Surface.");
                return false;
            }
        }

        return true;
    }

    private bool TryResolveTerrainAtCell(Vector3Int cell, TileBase fallbackTile, out TerrainTypeData terrain)
    {
        // Primeiro tenta o tile do boardTilemap de referencia.
        TileBase boardTile = boardTilemap != null ? boardTilemap.GetTile(cell) : null;
        if (boardTile != null && terrainDatabase.TryGetByPaletteTile(boardTile, out terrain) && terrain != null)
            return true;

        // Depois tenta o tile fallback capturado no grid.
        if (fallbackTile != null && terrainDatabase.TryGetByPaletteTile(fallbackTile, out terrain) && terrain != null)
            return true;

        // Por fim varre todos os tilemaps do mesmo grid em busca de um tile mapeado no TerrainDatabase.
        if (boardTilemap != null && boardTilemap.layoutGrid != null)
        {
            Tilemap[] maps = boardTilemap.layoutGrid.GetComponentsInChildren<Tilemap>(includeInactive: true);
            for (int i = 0; i < maps.Length; i++)
            {
                Tilemap map = maps[i];
                if (map == null)
                    continue;

                TileBase tile = map.GetTile(cell);
                if (tile == null)
                    continue;

                if (terrainDatabase.TryGetByPaletteTile(tile, out terrain) && terrain != null)
                    return true;
            }
        }

        terrain = null;
        return false;
    }

    private bool TryGetAnyPaintedTileOnGrid(Vector3Int cell, out TileBase tile)
    {
        tile = null;
        if (boardTilemap == null)
            return false;

        TileBase boardTile = boardTilemap.GetTile(cell);
        if (boardTile != null)
        {
            tile = boardTile;
            return true;
        }

        GridLayout grid = boardTilemap.layoutGrid;
        if (grid == null)
            return false;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            TileBase candidate = map.GetTile(cell);
            if (candidate == null)
                continue;

            tile = candidate;
            return true;
        }

        return false;
    }

    private static bool TerrainSupportsLayer(IReadOnlyList<TerrainLayerMode> modes, Domain domain, HeightLevel heightLevel)
    {
        if (modes == null)
            return false;

        for (int i = 0; i < modes.Count; i++)
        {
            TerrainLayerMode mode = modes[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private string GetRouteObjectName(StructureData structure, RoadRouteDefinition route, int routeIndex)
    {
        string sid = structure != null && !string.IsNullOrWhiteSpace(structure.id) ? structure.id : "structure";
        string rname = route != null && !string.IsNullOrWhiteSpace(route.routeName) ? route.routeName : $"route_{routeIndex}";
        return $"Road_{sid}_{rname}";
    }

    private void TryAutoAssignBoardTilemap()
    {
        if (boardTilemap != null)
            return;

        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            GridLayout.CellLayout layout = map.layoutGrid != null ? map.layoutGrid.cellLayout : GridLayout.CellLayout.Rectangle;
            if (layout == GridLayout.CellLayout.Hexagon)
            {
                boardTilemap = map;
                return;
            }
        }
    }

    private void EnsureDefaults()
    {
        if (!sortingLayerInitialized)
        {
            sortingLayer = SortingLayerReference.FromName("Estruturas");
            sortingLayerInitialized = true;
        }
    }

    private void TryRebuildIfChanged()
    {
        int currentSignature = ComputePreviewSignature();
        if (currentSignature == lastPreviewSignature)
            return;

        RebuildRoadVisuals();
    }

    private int ComputePreviewSignature()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (structureDatabase != null ? structureDatabase.GetInstanceID() : 0);
            hash = hash * 31 + (boardTilemap != null ? boardTilemap.GetInstanceID() : 0);
            hash = hash * 31 + (terrainDatabase != null ? terrainDatabase.GetInstanceID() : 0);
            hash = hash * 31 + (enforceLandSurfaceCells ? 1 : 0);
            hash = hash * 31 + (int)(roadWidth * 1000f);
            hash = hash * 31 + sortingLayer.Id;
            hash = hash * 31 + sortingOrder;
            hash = hash * 31 + roadColor.GetHashCode();

            if (structureDatabase == null || structureDatabase.Structures == null)
                return hash;

            IReadOnlyList<StructureData> structures = structureDatabase.Structures;
            hash = hash * 31 + structures.Count;
            for (int s = 0; s < structures.Count; s++)
            {
                StructureData structure = structures[s];
                if (structure == null)
                {
                    hash = hash * 31;
                    continue;
                }

                hash = hash * 31 + structure.GetInstanceID();
                hash = hash * 31 + (structure.roadRoutes != null ? structure.roadRoutes.Count : 0);
                if (structure.roadRoutes == null)
                    continue;

                for (int r = 0; r < structure.roadRoutes.Count; r++)
                {
                    RoadRouteDefinition route = structure.roadRoutes[r];
                    if (route == null || route.cells == null)
                    {
                        hash = hash * 31;
                        continue;
                    }

                    hash = hash * 31 + (route.routeName != null ? route.routeName.GetHashCode() : 0);
                    hash = hash * 31 + route.cells.Count;
                    for (int i = 0; i < route.cells.Count; i++)
                        hash = hash * 31 + route.cells[i].GetHashCode();
                }
            }

            return hash;
        }
    }
}
