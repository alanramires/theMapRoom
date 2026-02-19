using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class RoadNetworkManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StructureDatabase structureDatabase;
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    [Header("Default Road Visual")]
    [SerializeField] private Sprite roadSegmentSprite;
    [SerializeField] private Color roadColor = Color.white;
    [SerializeField] [Range(0.03f, 0.6f)] private float roadWidth = 0.16f;
    [SerializeField] [Range(0f, 0.3f)] private float segmentOverlap = 0.02f;
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
    private readonly Dictionary<Vector3Int, StructureData> structureByCell = new Dictionary<Vector3Int, StructureData>();
    private float nextLivePreviewTime;
    private int lastPreviewSignature = int.MinValue;
#if UNITY_EDITOR
    private bool validateRebuildQueued;
#endif

    public Tilemap BoardTilemap => boardTilemap;
    public StructureDatabase StructureDatabase => structureDatabase;

    public bool TryGetStructureAtCell(Vector3Int cell, out StructureData structure)
    {
        cell.z = 0;
        EnsureStructureCellLookup();
        return structureByCell.TryGetValue(cell, out structure);
    }

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
        QueueValidateRebuild();
    }

    private void QueueValidateRebuild()
    {
        if (validateRebuildQueued)
            return;

        validateRebuildQueued = true;
        EditorApplication.delayCall += PerformDelayedValidateRebuild;
    }

    private void PerformDelayedValidateRebuild()
    {
        EditorApplication.delayCall -= PerformDelayedValidateRebuild;
        validateRebuildQueued = false;

        if (this == null)
            return;

        RebuildRoadVisuals();
    }
#endif

    [ContextMenu("Rebuild Road Visuals")]
    public void RebuildRoadVisuals()
    {
        RebuildStructureCellLookup();
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
                if (!IsRouteValid(structure, route))
                {
                    if (logInvalidRoadCells)
                    {
                        string routeName = route != null && !string.IsNullOrWhiteSpace(route.routeName) ? route.routeName : $"route_{r}";
                        string structureName = !string.IsNullOrWhiteSpace(structure.id) ? structure.id : structure.name;
                        Debug.LogWarning($"[RoadNetworkManager] Rota ignorada: {structureName}/{routeName}.", this);
                    }
                    continue;
                }

                CreateRouteSegments(structure, route, r);
                drawnRoutes++;
            }
        }

        lastPreviewSignature = ComputePreviewSignature();
       /* if (logInvalidRoadCells)
            Debug.Log($"[RoadNetworkManager] Rotas desenhadas: {drawnRoutes}.", this);*/
    }

    [ContextMenu("Clear Road Visuals")]
    public void ClearRoadVisuals()
    {
        // Rehidrata objetos gerados existentes na hierarquia (ex.: apos recompile/domain reload),
        // para evitar "lixo" visual quando pontos da rota sao removidos.
        CollectOrphanedGeneratedRoadObjects();

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

    private void CollectOrphanedGeneratedRoadObjects()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
                continue;

            GameObject childGo = child.gameObject;
            if (childGo == null)
                continue;

            if (!IsGeneratedRoadVisual(childGo))
                continue;

            if (!generatedRoadObjects.Contains(childGo))
                generatedRoadObjects.Add(childGo);
        }
    }

    private static bool IsGeneratedRoadVisual(GameObject go)
    {
        if (go == null)
            return false;

        bool hasLine = go.GetComponent<LineRenderer>() != null;
        bool hasSprite = go.GetComponent<SpriteRenderer>() != null || go.GetComponentInChildren<SpriteRenderer>() != null;
        if (!hasLine && !hasSprite)
            return false;

        return go.name.StartsWith("Road_");
    }

    private void CreateRouteSegments(StructureData structure, RoadRouteDefinition route, int routeIndex)
    {
        Sprite segmentSprite = ResolveSegmentSprite(structure);
        Color segmentColor = ResolveRoadColor(structure);
        float width = ResolveRoadWidth(structure);
        float overlap = ResolveSegmentOverlap(structure);

        GameObject routeRoot = new GameObject(GetRouteObjectName(structure, route, routeIndex));
        routeRoot.transform.SetParent(transform, false);

        for (int i = 0; i < route.cells.Count - 1; i++)
        {
            Vector3Int fromCell = route.cells[i];
            Vector3Int toCell = route.cells[i + 1];
            fromCell.z = 0;
            toCell.z = 0;

            Vector3 from = boardTilemap.GetCellCenterWorld(fromCell);
            Vector3 to = boardTilemap.GetCellCenterWorld(toCell);
            from.z = boardTilemap.transform.position.z;
            to.z = boardTilemap.transform.position.z;

            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.0001f)
                continue;

            float angle = Vector2.SignedAngle(Vector2.up, new Vector2(delta.x, delta.y));
            Vector3 midpoint = (from + to) * 0.5f;

            GameObject segment = new GameObject($"Segment_{i:00}");
            segment.transform.SetParent(routeRoot.transform, false);
            segment.transform.position = midpoint;
            segment.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            SpriteRenderer renderer = segment.AddComponent<SpriteRenderer>();
            renderer.sprite = segmentSprite;
            renderer.color = segmentColor;
            if (sortingLayer.Id != 0)
                renderer.sortingLayerID = sortingLayer.Id;
            renderer.sortingOrder = sortingOrder;

            float spriteWidth = 1f;
            float spriteHeight = 1f;
            if (segmentSprite != null)
            {
                Vector2 spriteSize = segmentSprite.bounds.size;
                spriteWidth = Mathf.Max(0.0001f, spriteSize.x);
                spriteHeight = Mathf.Max(0.0001f, spriteSize.y);
            }

            float targetLength = length + Mathf.Max(0f, overlap);
            float scaleX = Mathf.Max(0.0001f, width / spriteWidth);
            float scaleY = Mathf.Max(0.0001f, targetLength / spriteHeight);
            segment.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        generatedRoadObjects.Add(routeRoot);
    }

    private bool IsRouteValid(StructureData structure, RoadRouteDefinition route)
    {
        if (!enforceLandSurfaceCells)
            return true;

        for (int i = 0; i < route.cells.Count; i++)
        {
            Vector3Int cell = route.cells[i];
            cell.z = 0;
            if (!IsRoadCellValid(cell, structure, logInvalidRoadCells))
                return false;
        }

        return true;
    }

    public bool IsRoadCellValid(Vector3Int cell)
    {
        return IsRoadCellValid(cell, logReason: false);
    }

    public bool IsRoadCellValid(Vector3Int cell, bool logReason)
    {
        return IsRoadCellValid(cell, null, logReason);
    }

    public bool IsRoadCellValidForStructure(Vector3Int cell, StructureData structure, bool logReason = false)
    {
        return IsRoadCellValid(cell, structure, logReason);
    }

    private bool IsRoadCellValid(Vector3Int cell, StructureData routeStructure, bool logReason)
    {
        if (boardTilemap == null)
            return false;

        if (!enforceLandSurfaceCells)
            return true;

        cell.z = 0;

        // Mesma regra do movimento: construcao sobrescreve terreno.
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (construction != null)
        {
            if (CanBuildStructureOnConstruction(routeStructure, construction))
                return true;

            if (logReason)
                Debug.LogWarning($"[RoadNetworkManager] Rota invalida em ({cell.x},{cell.y}): construcao no hex nao suporta as regras de build da estrutura.");
            return false;
        }

        if (!TryGetAnyPaintedTileOnGrid(cell, out TileBase anyPaintedTile))
        {
            if (logReason)
                Debug.LogWarning($"[RoadNetworkManager] Rota invalida em ({cell.x},{cell.y}): celula sem tile no grid.");
            return false;
        }

        if (terrainDatabase == null)
            return true;

        if (!TryResolveTerrainAtCell(cell, anyPaintedTile, out TerrainTypeData terrain) || terrain == null)
        {
            if (logReason)
                Debug.LogWarning($"[RoadNetworkManager] Rota invalida em ({cell.x},{cell.y}): tile sem mapeamento no TerrainDatabase.");
            return false;
        }

        bool supportsBuild = SupportsBuildOnTerrain(routeStructure, terrain);

        if (!supportsBuild)
        {
            if (logReason)
                Debug.LogWarning($"[RoadNetworkManager] Rota invalida em ({cell.x},{cell.y}). Terreno nao suporta as regras de build da estrutura.");
            return false;
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

    private Sprite ResolveSegmentSprite(StructureData structure)
    {
        if (structure != null && structure.roadSegmentSprite != null)
            return structure.roadSegmentSprite;

        return roadSegmentSprite;
    }

    private Color ResolveRoadColor(StructureData structure)
    {
        if (structure != null)
            return structure.roadColor;

        return roadColor;
    }

    private float ResolveRoadWidth(StructureData structure)
    {
        if (structure != null)
            return Mathf.Clamp(structure.roadWidth, 0.03f, 0.6f);

        return Mathf.Clamp(roadWidth, 0.03f, 0.6f);
    }

    private float ResolveSegmentOverlap(StructureData structure)
    {
        if (structure != null)
            return Mathf.Clamp(structure.segmentOverlap, 0f, 0.3f);

        return Mathf.Clamp(segmentOverlap, 0f, 0.3f);
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

    private static bool SupportsBuildOnTerrain(StructureData structure, TerrainTypeData terrain)
    {
        if (terrain == null)
            return false;

        // Compatibilidade com comportamento antigo: sem estrutura explicita, usa Land/Surface.
        if (structure == null)
        {
            bool supportsLandSurface =
                (terrain.domain == Domain.Land && terrain.heightLevel == HeightLevel.Surface) ||
                TerrainSupportsLayer(terrain.aditionalDomainsAllowed, Domain.Land, HeightLevel.Surface);
            return supportsLandSurface;
        }

        if (structure.SupportsBuildOn(terrain.domain, terrain.heightLevel))
            return true;

        if (terrain.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < terrain.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = terrain.aditionalDomainsAllowed[i];
            if (structure.SupportsBuildOn(mode.domain, mode.heightLevel))
                return true;
        }

        return false;
    }

    private static bool CanBuildStructureOnConstruction(StructureData structure, ConstructionManager construction)
    {
        if (construction == null)
            return false;

        if (structure == null)
            return construction.SupportsLayerMode(Domain.Land, HeightLevel.Surface);

        IReadOnlyList<TerrainLayerMode> constructionModes = construction.GetAllLayerModes();
        for (int i = 0; i < constructionModes.Count; i++)
        {
            TerrainLayerMode mode = constructionModes[i];
            if (structure.SupportsBuildOn(mode.domain, mode.heightLevel))
                return true;
        }

        if (construction.AllowsAirDomain() && structure.SupportsBuildOn(Domain.Air, HeightLevel.Surface))
            return true;

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

    private void EnsureStructureCellLookup()
    {
        if (structureByCell.Count > 0)
            return;

        RebuildStructureCellLookup();
    }

    private void RebuildStructureCellLookup()
    {
        structureByCell.Clear();
        if (structureDatabase == null || structureDatabase.Structures == null)
            return;

        IReadOnlyList<StructureData> structures = structureDatabase.Structures;
        for (int s = 0; s < structures.Count; s++)
        {
            StructureData structure = structures[s];
            if (structure == null || structure.roadRoutes == null)
                continue;

            for (int r = 0; r < structure.roadRoutes.Count; r++)
            {
                RoadRouteDefinition route = structure.roadRoutes[r];
                if (route == null || route.cells == null)
                    continue;

                for (int i = 0; i < route.cells.Count; i++)
                {
                    Vector3Int cell = route.cells[i];
                    cell.z = 0;
                    if (!structureByCell.TryGetValue(cell, out StructureData current))
                    {
                        structureByCell.Add(cell, structure);
                        continue;
                    }

                    if (ShouldReplaceStructureAtCell(current, structure))
                        structureByCell[cell] = structure;
                }
            }
        }
    }

    private bool ShouldReplaceStructureAtCell(StructureData current, StructureData candidate)
    {
        if (candidate == null)
            return false;
        if (current == null)
            return true;

        if (structureDatabase != null)
            return structureDatabase.ComparePriority(candidate, current) > 0;

        if (candidate.priorityOrder != current.priorityOrder)
            return candidate.priorityOrder > current.priorityOrder;

        return false;
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
            hash = hash * 31 + (int)(segmentOverlap * 1000f);
            hash = hash * 31 + sortingLayer.Id;
            hash = hash * 31 + sortingOrder;
            hash = hash * 31 + roadColor.GetHashCode();
            hash = hash * 31 + (roadSegmentSprite != null ? roadSegmentSprite.GetInstanceID() : 0);

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
                hash = hash * 31 + (structure.roadSegmentSprite != null ? structure.roadSegmentSprite.GetInstanceID() : 0);
                hash = hash * 31 + structure.roadColor.GetHashCode();
                hash = hash * 31 + (int)(structure.roadWidth * 1000f);
                hash = hash * 31 + (int)(structure.segmentOverlap * 1000f);
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
