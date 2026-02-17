using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RoadRoutePainterWindow : EditorWindow
{
    private RoadNetworkManager roadNetworkManager;
    private StructureData structureData;
    private int selectedRouteIndex;
    private bool isPainting;
    private bool autoConnectAB = true;
    private Vector2 scroll;

    [MenuItem("Tools/Structures/Road Route Painter")]
    public static void OpenWindow()
    {
        RoadRoutePainterWindow window = GetWindow<RoadRoutePainterWindow>("Road Route Painter");
        window.minSize = new Vector2(360f, 260f);
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        TryAutoAssignReferences(force: false);
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnFocus()
    {
        TryAutoAssignReferences(force: false);
    }

    private void OnGUI()
    {
        TryAutoAssignReferences(force: false);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        roadNetworkManager = (RoadNetworkManager)EditorGUILayout.ObjectField("Road Manager", roadNetworkManager, typeof(RoadNetworkManager), true);
        structureData = (StructureData)EditorGUILayout.ObjectField("Structure Data", structureData, typeof(StructureData), false);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Auto Detect", GUILayout.Width(110f)))
            TryAutoAssignReferences(force: true);
        EditorGUILayout.EndHorizontal();

        if (roadNetworkManager == null)
        {
            EditorGUILayout.HelpBox("Arraste um RoadNetworkManager da cena.", MessageType.Info);
            DrawTogglePaintButton(disabled: true);
            EditorGUILayout.EndScrollView();
            return;
        }

        if (roadNetworkManager.BoardTilemap == null)
            EditorGUILayout.HelpBox("RoadNetworkManager precisa de Board Tilemap.", MessageType.Warning);

        if (structureData == null)
        {
            EditorGUILayout.HelpBox("Escolha um StructureData para editar as rotas.", MessageType.Info);
            DrawTogglePaintButton(disabled: true);
            EditorGUILayout.EndScrollView();
            return;
        }

        if (!IsSelectedStructureInManagerDatabase())
        {
            EditorGUILayout.HelpBox("Este StructureData nao esta no StructureDatabase do RoadNetworkManager. O preview de sprite no Scene nao sera desenhado.", MessageType.Warning);
        }

        EnsureRouteSelectionInBounds();

        EditorGUILayout.Space(6f);
        DrawRouteSelector();
        DrawRouteNameEditor();
        DrawRouteActions();

        EditorGUILayout.Space(8f);
        autoConnectAB = EditorGUILayout.ToggleLeft("Auto Connect A->B (preenche hexes intermediarios validos)", autoConnectAB);
        DrawTogglePaintButton(disabled: false);
        if (isPainting)
            EditorGUILayout.HelpBox("Pintura ativa no Scene: Left Click adiciona destino (com Auto Connect), Right Click remove ultimo ponto.", MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    private void DrawRouteSelector()
    {
        List<RoadRouteDefinition> routes = structureData.roadRoutes ?? (structureData.roadRoutes = new List<RoadRouteDefinition>());
        string[] labels = BuildRouteLabels(routes);
        selectedRouteIndex = Mathf.Clamp(selectedRouteIndex, 0, Mathf.Max(0, labels.Length - 1));
        selectedRouteIndex = EditorGUILayout.Popup("Route", selectedRouteIndex, labels);
    }

    private void DrawRouteActions()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("New Route"))
            CreateRoute();

        using (new EditorGUI.DisabledScope(!HasValidSelectedRoute()))
        {
            if (GUILayout.Button("Clear Route"))
                ClearSelectedRoute();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!HasValidSelectedRoute()))
        {
            if (GUILayout.Button("Remove Last Point"))
                RemoveLastPoint();
        }

        using (new EditorGUI.DisabledScope(!HasValidSelectedRoute()))
        {
            if (GUILayout.Button("Delete Route"))
                DeleteSelectedRoute();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawRouteNameEditor()
    {
        if (!HasValidSelectedRoute())
            return;

        RoadRouteDefinition route = structureData.roadRoutes[selectedRouteIndex];
        if (route == null)
            return;

        string currentName = route.routeName ?? string.Empty;
        string newName = EditorGUILayout.TextField("Route Name", currentName);
        if (newName == currentName)
            return;

        Undo.RecordObject(structureData, "Rename Road Route");
        route.routeName = newName;
        EditorUtility.SetDirty(structureData);
        roadNetworkManager?.RebuildRoadVisuals();
    }

    private void DrawTogglePaintButton(bool disabled)
    {
        using (new EditorGUI.DisabledScope(disabled))
        {
            string label = isPainting ? "Stop Painting" : "Start Painting";
            if (GUILayout.Button(label, GUILayout.Height(28f)))
                isPainting = !isPainting;
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!isPainting || roadNetworkManager == null || structureData == null)
            return;

        Tilemap tilemap = roadNetworkManager.BoardTilemap;
        if (tilemap == null)
            return;
        if (!HasValidSelectedRoute())
            return;

        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        DrawCurrentRouteOverlay(tilemap);

        if (e.type != EventType.MouseDown)
            return;
        if (e.button != 0 && e.button != 1)
            return;

        RoadRouteDefinition route = structureData.roadRoutes[selectedRouteIndex];
        if (route == null)
            return;

        if (e.button == 1)
        {
            RemoveLastPoint();
            e.Use();
            return;
        }

        Vector3 world = GetMouseWorldOnTilemapPlane(e.mousePosition, tilemap);
        Vector3Int cell = tilemap.WorldToCell(world);
        cell.z = 0;
        if (!IsCellPaintedOnGrid(tilemap, cell))
        {
            ShowNotification(new GUIContent("Hex invalido (sem tile)"));
            e.Use();
            return;
        }

        if (!roadNetworkManager.IsRoadCellValidForStructure(cell, structureData, logReason: false))
        {
            ShowNotification(new GUIContent("Hex invalido para rodovia"));
            e.Use();
            return;
        }

        if (route.cells.Count > 0 && route.cells[route.cells.Count - 1] == cell)
        {
            e.Use();
            return;
        }

        Undo.RecordObject(structureData, "Paint Road Route Cell");
        if (route.cells.Count == 0)
        {
            route.cells.Add(cell);
        }
        else if (autoConnectAB)
        {
            Vector3Int start = route.cells[route.cells.Count - 1];
            if (!TryBuildRoadPath(tilemap, start, cell, out List<Vector3Int> autoPath))
            {
                ShowNotification(new GUIContent("Sem caminho valido A->B"));
                e.Use();
                return;
            }

            AppendPathExcludingFirst(route.cells, autoPath);
        }
        else
        {
            route.cells.Add(cell);
        }

        EditorUtility.SetDirty(structureData);
        roadNetworkManager.RebuildRoadVisuals();
        e.Use();
    }

    private void DrawCurrentRouteOverlay(Tilemap tilemap)
    {
        if (!HasValidSelectedRoute())
            return;

        RoadRouteDefinition route = structureData.roadRoutes[selectedRouteIndex];
        if (route == null || route.cells == null || route.cells.Count == 0)
            return;

        Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Vector3? prev = null;
        for (int i = 0; i < route.cells.Count; i++)
        {
            Vector3Int cell = route.cells[i];
            cell.z = 0;
            Vector3 p = tilemap.GetCellCenterWorld(cell);
            p.z = tilemap.transform.position.z;

            Handles.DrawSolidDisc(p, Vector3.forward, 0.06f);
            Handles.Label(p + Vector3.up * 0.08f, i.ToString());

            if (prev.HasValue)
                Handles.DrawLine(prev.Value, p);
            prev = p;
        }
    }

    private Vector3 GetMouseWorldOnTilemapPlane(Vector2 mousePosition, Tilemap tilemap)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        // Usa o plano real do tilemap (independente de objetos por cima no Scene).
        Plane tilemapPlane = new Plane(tilemap.transform.forward, tilemap.transform.position);
        if (tilemapPlane.Raycast(ray, out float enter) && enter >= 0f)
            return ray.GetPoint(enter);

        // Fallback para SceneView ortografica/perspectiva.
        SceneView view = SceneView.currentDrawingSceneView;
        if (view != null && view.camera != null)
        {
            Camera cam = view.camera;
            Vector2 gui = mousePosition;
            Vector3 screen = new Vector3(gui.x, cam.pixelHeight - gui.y, Mathf.Abs(cam.transform.position.z - tilemap.transform.position.z));
            return cam.ScreenToWorldPoint(screen);
        }

        return tilemap.transform.position;
    }

    private bool IsCellPaintedOnGrid(Tilemap referenceTilemap, Vector3Int cell)
    {
        if (referenceTilemap == null)
            return false;

        if (referenceTilemap.HasTile(cell))
            return true;

        GridLayout grid = referenceTilemap.layoutGrid;
        if (grid == null)
            return false;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            if (map.HasTile(cell))
                return true;
        }

        return false;
    }

    private void CreateRoute()
    {
        if (structureData == null)
            return;

        Undo.RecordObject(structureData, "Create Road Route");
        if (structureData.roadRoutes == null)
            structureData.roadRoutes = new List<RoadRouteDefinition>();

        RoadRouteDefinition route = new RoadRouteDefinition
        {
            routeName = $"Route {structureData.roadRoutes.Count + 1}",
            cells = new List<Vector3Int>()
        };

        structureData.roadRoutes.Add(route);
        selectedRouteIndex = Mathf.Max(0, structureData.roadRoutes.Count - 1);
        EditorUtility.SetDirty(structureData);
        roadNetworkManager?.RebuildRoadVisuals();
    }

    private void ClearSelectedRoute()
    {
        if (!HasValidSelectedRoute())
            return;

        Undo.RecordObject(structureData, "Clear Road Route");
        structureData.roadRoutes[selectedRouteIndex].cells.Clear();
        EditorUtility.SetDirty(structureData);
        roadNetworkManager?.RebuildRoadVisuals();
    }

    private void RemoveLastPoint()
    {
        if (!HasValidSelectedRoute())
            return;

        RoadRouteDefinition route = structureData.roadRoutes[selectedRouteIndex];
        if (route == null || route.cells == null || route.cells.Count == 0)
            return;

        Undo.RecordObject(structureData, "Remove Road Route Point");
        route.cells.RemoveAt(route.cells.Count - 1);
        EditorUtility.SetDirty(structureData);
        roadNetworkManager?.RebuildRoadVisuals();
    }

    private void DeleteSelectedRoute()
    {
        if (!HasValidSelectedRoute())
            return;

        Undo.RecordObject(structureData, "Delete Road Route");
        structureData.roadRoutes.RemoveAt(selectedRouteIndex);
        selectedRouteIndex = Mathf.Clamp(selectedRouteIndex, 0, Mathf.Max(0, structureData.roadRoutes.Count - 1));
        EditorUtility.SetDirty(structureData);
        roadNetworkManager?.RebuildRoadVisuals();
    }

    private bool HasValidSelectedRoute()
    {
        return structureData != null
            && structureData.roadRoutes != null
            && structureData.roadRoutes.Count > 0
            && selectedRouteIndex >= 0
            && selectedRouteIndex < structureData.roadRoutes.Count;
    }

    private void EnsureRouteSelectionInBounds()
    {
        if (structureData == null)
        {
            selectedRouteIndex = 0;
            return;
        }

        if (structureData.roadRoutes == null || structureData.roadRoutes.Count == 0)
        {
            selectedRouteIndex = 0;
            return;
        }

        selectedRouteIndex = Mathf.Clamp(selectedRouteIndex, 0, structureData.roadRoutes.Count - 1);
    }

    private static string[] BuildRouteLabels(IReadOnlyList<RoadRouteDefinition> routes)
    {
        if (routes == null || routes.Count == 0)
            return new[] { "<no routes>" };

        string[] labels = new string[routes.Count];
        for (int i = 0; i < routes.Count; i++)
        {
            RoadRouteDefinition route = routes[i];
            string name = route != null && !string.IsNullOrWhiteSpace(route.routeName) ? route.routeName : $"Route {i + 1}";
            int count = route != null && route.cells != null ? route.cells.Count : 0;
            labels[i] = $"{name} ({count} pts)";
        }

        return labels;
    }

    private bool TryBuildRoadPath(Tilemap tilemap, Vector3Int start, Vector3Int goal, out List<Vector3Int> path)
    {
        path = new List<Vector3Int>();
        start.z = 0;
        goal.z = 0;

        if (start == goal)
        {
            path.Add(start);
            return true;
        }

        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        frontier.Enqueue(start);
        cameFrom[start] = start;

        bool found = false;
        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            if (current == goal)
            {
                found = true;
                break;
            }

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;

                if (cameFrom.ContainsKey(next))
                    continue;
                if (!IsCellPaintedOnGrid(tilemap, next))
                    continue;
                if (!roadNetworkManager.IsRoadCellValidForStructure(next, structureData, logReason: false))
                    continue;

                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }

        if (!found)
            return false;

        Vector3Int step = goal;
        path.Add(step);
        while (step != start)
        {
            step = cameFrom[step];
            path.Add(step);
        }

        path.Reverse();
        return true;
    }

    private static void AppendPathExcludingFirst(List<Vector3Int> routeCells, List<Vector3Int> autoPath)
    {
        if (routeCells == null || autoPath == null || autoPath.Count == 0)
            return;

        int begin = autoPath.Count > 0 ? 1 : 0;
        for (int i = begin; i < autoPath.Count; i++)
        {
            Vector3Int cell = autoPath[i];
            if (routeCells.Count > 0 && routeCells[routeCells.Count - 1] == cell)
                continue;

            routeCells.Add(cell);
        }
    }

    private void TryAutoAssignReferences(bool force)
    {
        if (force || roadNetworkManager == null)
        {
            RoadNetworkManager[] managers = Object.FindObjectsByType<RoadNetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (managers != null && managers.Length > 0)
                roadNetworkManager = managers[0];
        }

        if (!force && structureData != null)
            return;

        StructureData resolved = TryResolveStructureDataFromManager();
        if (resolved != null)
        {
            structureData = resolved;
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:StructureData");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            StructureData candidate = AssetDatabase.LoadAssetAtPath<StructureData>(path);
            if (candidate == null)
                continue;

            structureData = candidate;
            return;
        }
    }

    private StructureData TryResolveStructureDataFromManager()
    {
        if (roadNetworkManager == null || roadNetworkManager.StructureDatabase == null)
            return null;

        IReadOnlyList<StructureData> structures = roadNetworkManager.StructureDatabase.Structures;
        if (structures == null || structures.Count == 0)
            return null;

        // Preferencia: estrutura que ja tenha ao menos uma rota.
        for (int i = 0; i < structures.Count; i++)
        {
            StructureData candidate = structures[i];
            if (candidate == null)
                continue;

            if (candidate.roadRoutes != null && candidate.roadRoutes.Count > 0)
                return candidate;
        }

        // Fallback: primeira estrutura valida.
        for (int i = 0; i < structures.Count; i++)
        {
            if (structures[i] != null)
                return structures[i];
        }

        return null;
    }

    private bool IsSelectedStructureInManagerDatabase()
    {
        if (roadNetworkManager == null || structureData == null)
            return false;

        StructureDatabase db = roadNetworkManager.StructureDatabase;
        if (db == null || db.Structures == null)
            return false;

        IReadOnlyList<StructureData> structures = db.Structures;
        for (int i = 0; i < structures.Count; i++)
        {
            if (structures[i] == structureData)
                return true;
        }

        return false;
    }
}
