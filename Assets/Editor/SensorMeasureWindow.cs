using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SensorMeasureWindow : EditorWindow
{
    private enum PickMode
    {
        None = 0,
        Start = 1,
        End = 2
    }

    [SerializeField] private Tilemap overrideTilemap;

    private PickMode pickMode;
    private bool hasStartCell;
    private bool hasEndCell;
    private Vector3Int startCell;
    private Vector3Int endCell;
    private readonly List<Vector3Int> straightIntermediateCells = new List<Vector3Int>();
    private int straightDistanceHexes = -1;
    private int validPathDistanceHexes = -1;
    private string statusMessage = "Selecione hex inicial e final para medir.";

    [MenuItem("Tools/Sensor/Medir")]
    public static void OpenWindow()
    {
        GetWindow<SensorMeasureWindow>("Sensor Medir");
    }

    private void OnEnable()
    {
        AutoDetectContext();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        pickMode = PickMode.None;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sensor Medir", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1) Clique em \"Selecionar Hex Inicial\"\n" +
            "2) Clique no hex no Scene\n" +
            "3) Clique em \"Selecionar Hex Final\" e escolha o destino\n" +
            "4) \"Limpar\" reinicia a medicao",
            MessageType.Info);

        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Selecionar Hex Inicial"))
        {
            pickMode = PickMode.Start;
            statusMessage = "Clique no Scene para definir o hex inicial.";
            Repaint();
        }
        if (GUILayout.Button("Selecionar Hex Final"))
        {
            pickMode = PickMode.End;
            statusMessage = "Clique no Scene para definir o hex final.";
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Limpar"))
            ClearSelection();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        EditorGUILayout.LabelField("Hex Inicial", hasStartCell ? FormatCell(startCell) : "-");
        EditorGUILayout.LabelField("Hex Final", hasEndCell ? FormatCell(endCell) : "-");
        EditorGUILayout.LabelField("Distancia (reta)", straightDistanceHexes >= 0 ? $"{straightDistanceHexes} hexes" : "-");
        EditorGUILayout.LabelField(
            "Distancia (caminho valido)",
            validPathDistanceHexes >= 0 ? $"{validPathDistanceHexes} hexes" : "sem caminho valido");
        if (pickMode != PickMode.None)
            EditorGUILayout.HelpBox("Modo de clique ativo no Scene.", MessageType.Warning);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        Tilemap map = ResolveTilemap();
        DrawOverlay(map);

        if (pickMode == PickMode.None)
            return;
        if (map == null)
            return;

        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (e.type != EventType.MouseDown || e.button != 0)
            return;

        Vector3 world = GetMouseWorldOnTilemapPlane(e.mousePosition, map);
        Vector3Int cell = map.WorldToCell(world);
        cell.z = 0;

        if (!IsCellPaintedOnGrid(map, cell))
        {
            ShowNotification(new GUIContent("Hex invalido (sem tile)"));
            e.Use();
            return;
        }

        if (pickMode == PickMode.Start)
        {
            startCell = cell;
            hasStartCell = true;
            pickMode = PickMode.None;
            statusMessage = $"Hex inicial definido: {FormatCell(startCell)}";
            if (hasEndCell)
                RecomputeMeasure();
        }
        else if (pickMode == PickMode.End)
        {
            endCell = cell;
            hasEndCell = true;
            pickMode = PickMode.None;
            statusMessage = $"Hex final definido: {FormatCell(endCell)}";
            if (hasStartCell)
                RecomputeMeasure();
        }

        Repaint();
        SceneView.RepaintAll();
        e.Use();
    }

    private void DrawOverlay(Tilemap map)
    {
        if (map == null)
            return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        if (hasStartCell)
        {
            Vector3 startWorld = map.GetCellCenterWorld(startCell);
            Handles.color = new Color(0.25f, 1f, 0.4f, 1f);
            float radius = HandleUtility.GetHandleSize(startWorld) * 0.14f;
            Handles.DrawWireDisc(startWorld, Vector3.forward, radius);
            Handles.Label(startWorld + new Vector3(0.1f, 0.1f, 0f), "Inicio");
        }

        if (hasEndCell)
        {
            Vector3 endWorld = map.GetCellCenterWorld(endCell);
            Handles.color = new Color(1f, 0.35f, 0.35f, 1f);
            float radius = HandleUtility.GetHandleSize(endWorld) * 0.14f;
            Handles.DrawWireDisc(endWorld, Vector3.forward, radius);
            Handles.Label(endWorld + new Vector3(0.1f, 0.1f, 0f), "Fim");
        }

        if (!hasStartCell || !hasEndCell)
            return;

        Vector3 from = map.GetCellCenterWorld(startCell);
        Vector3 to = map.GetCellCenterWorld(endCell);
        Handles.color = new Color(0.2f, 0.8f, 1f, 1f);
        Handles.DrawAAPolyLine(4f, from, to);

        string distanceLabel = validPathDistanceHexes >= 0
            ? $"{validPathDistanceHexes} hexes"
            : straightDistanceHexes >= 0 ? $"{straightDistanceHexes} hexes (reta)" : "-";
        Vector3 labelPos = Vector3.Lerp(from, to, 0.5f) + new Vector3(0f, 0.25f, 0f);
        Handles.Label(labelPos, distanceLabel);
    }

    private void RecomputeMeasure()
    {
        straightIntermediateCells.Clear();
        straightDistanceHexes = -1;
        validPathDistanceHexes = -1;

        if (!hasStartCell || !hasEndCell)
            return;

        Tilemap map = ResolveTilemap();
        if (map == null)
        {
            statusMessage = "Sem tilemap valido para medir.";
            return;
        }

        straightIntermediateCells.AddRange(GetIntermediateCellsByCellLerp(map, startCell, endCell));
        straightDistanceHexes = startCell == endCell ? 0 : straightIntermediateCells.Count + 1;
        validPathDistanceHexes = ComputeReachableHexDistance(map, startCell, endCell);

        if (validPathDistanceHexes >= 0)
            statusMessage = $"Distancia medida: {validPathDistanceHexes} hexes (caminho valido).";
        else
            statusMessage = $"Distancia reta: {straightDistanceHexes} hexes (sem caminho valido no grid).";
    }

    private void ClearSelection()
    {
        pickMode = PickMode.None;
        hasStartCell = false;
        hasEndCell = false;
        startCell = Vector3Int.zero;
        endCell = Vector3Int.zero;
        straightIntermediateCells.Clear();
        straightDistanceHexes = -1;
        validPathDistanceHexes = -1;
        statusMessage = "Selecao limpa. Escolha novos hexes.";
        Repaint();
        SceneView.RepaintAll();
    }

    private void AutoDetectContext()
    {
        if (overrideTilemap != null)
            return;

        CursorController cursor = FindAnyObjectByType<CursorController>();
        if (cursor != null && cursor.BoardTilemap != null)
        {
            overrideTilemap = cursor.BoardTilemap;
            return;
        }

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.BoardTilemap == null)
                continue;

            overrideTilemap = unit.BoardTilemap;
            return;
        }
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;

        CursorController cursor = FindAnyObjectByType<CursorController>();
        if (cursor != null && cursor.BoardTilemap != null)
            return cursor.BoardTilemap;

        return null;
    }

    private static string FormatCell(Vector3Int cell)
    {
        return $"{cell.x},{cell.y},0";
    }

    private static List<Vector3Int> GetIntermediateCellsByCellLerp(Tilemap tilemap, Vector3Int originCell, Vector3Int targetCell)
    {
        List<Vector3Int> cells = new List<Vector3Int>();

        originCell.z = 0;
        targetCell.z = 0;
        int dx = targetCell.x - originCell.x;
        int dy = targetCell.y - originCell.y;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        if (steps <= 1 || tilemap == null)
            return cells;

        Vector3 originWorld = tilemap.GetCellCenterWorld(originCell);
        Vector3 targetWorld = tilemap.GetCellCenterWorld(targetCell);
        int sampleCount = steps * 4;
        if (sampleCount <= 1)
            sampleCount = steps;

        HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        for (int i = 1; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector3 sampleWorld = Vector3.Lerp(originWorld, targetWorld, t);
            Vector3Int cell = tilemap.WorldToCell(sampleWorld);
            cell.z = 0;
            if (cell == originCell || cell == targetCell)
                continue;
            if (seen.Add(cell))
                cells.Add(cell);
        }

        return cells;
    }

    private static int ComputeReachableHexDistance(Tilemap tilemap, Vector3Int origin, Vector3Int target)
    {
        if (tilemap == null)
            return -1;

        origin.z = 0;
        target.z = 0;
        if (origin == target)
            return 0;
        if (!IsCellPaintedOnGrid(tilemap, origin) || !IsCellPaintedOnGrid(tilemap, target))
            return -1;

        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        Dictionary<Vector3Int, int> distances = new Dictionary<Vector3Int, int>();
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        distances[origin] = 0;
        frontier.Enqueue(origin);

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            int currentDistance = distances[current];

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                if (distances.ContainsKey(next))
                    continue;
                if (!IsCellPaintedOnGrid(tilemap, next))
                    continue;

                int nextDistance = currentDistance + 1;
                if (next == target)
                    return nextDistance;

                distances[next] = nextDistance;
                frontier.Enqueue(next);
            }
        }

        return -1;
    }

    private static bool IsCellPaintedOnGrid(Tilemap referenceTilemap, Vector3Int cell)
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

    private static Vector3 GetMouseWorldOnTilemapPlane(Vector2 mousePosition, Tilemap tilemap)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane tilemapPlane = new Plane(tilemap.transform.forward, tilemap.transform.position);
        if (tilemapPlane.Raycast(ray, out float enter) && enter >= 0f)
            return ray.GetPoint(enter);

        SceneView view = SceneView.currentDrawingSceneView;
        if (view != null && view.camera != null)
        {
            Camera cam = view.camera;
            Vector3 screen = new Vector3(
                mousePosition.x,
                cam.pixelHeight - mousePosition.y,
                Mathf.Abs(cam.transform.position.z - tilemap.transform.position.z));
            return cam.ScreenToWorldPoint(screen);
        }

        return tilemap.transform.position;
    }
}
