using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Pecas comuns das janelas de Tools/Operacoes Navais (Pode Emergir, Pode Submergir).
// Só GUI e resolucao de contexto — nenhuma regra de jogo mora aqui. Quem responde
// "pode?" continua sendo o sensor / o validador do TurnStateManager.
public static class NavalOpsWindowGui
{
    public static UnitManager ResolveSelectedUnit()
    {
        GameObject selected = Selection.activeGameObject;
        return selected != null
            ? selected.GetComponentInParent<UnitManager>()
            : null;
    }

    public static void AutoDetectContext(
        UnitManager unit,
        ref Tilemap map,
        ref TerrainDatabase terrainDatabase)
    {
        if (unit != null && unit.BoardTilemap != null)
            map = unit.BoardTilemap;

        if (map == null)
        {
            Tilemap[] maps = Object.FindObjectsByType<Tilemap>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] != null && maps[i].name == "Tilemap")
                {
                    map = maps[i];
                    break;
                }
            }
        }

        if (terrainDatabase == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
            if (guids.Length > 0)
            {
                terrainDatabase = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
    }

    public static void DrawUnitPickerRow(
        System.Action useSelected,
        ref bool pickingDestination)
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            useSelected?.Invoke();

        GUI.backgroundColor = pickingDestination
            ? new Color(1f, 0.75f, 0.2f)
            : Color.white;
        if (GUILayout.Button(
                pickingDestination
                    ? "Clique no Scene View..."
                    : "Escolher Hex de Destino"))
        {
            pickingDestination = !pickingDestination;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    public static void DrawEvaluatedCell(
        UnitManager unit,
        bool hasDestination,
        Vector3Int destination)
    {
        Vector3Int effectiveCell = hasDestination
            ? destination
            : unit != null
                ? unit.CurrentCellPosition
                : Vector3Int.zero;
        effectiveCell.z = 0;

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Vector3IntField("Hex avaliado", effectiveCell);
    }

    public static void DrawCurrentLayer(UnitManager unit)
    {
        if (unit == null)
            return;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField(
                "Camada atual",
                $"{unit.GetDomain()} / {unit.GetHeightLevel()}");
        }
    }

    // Devolve true no frame em que o usuario clicou num hex do Scene View.
    public static bool TryPickCellInScene(
        Tilemap map,
        ref bool pickingDestination,
        ref Vector3Int hoverCell,
        string label,
        out Vector3Int picked)
    {
        picked = Vector3Int.zero;
        if (!pickingDestination || map == null)
            return false;

        Event current = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
        Plane plane = new Plane(map.transform.forward, map.transform.position);
        if (!plane.Raycast(ray, out float distance))
            return false;

        Vector3 world = ray.GetPoint(distance);
        hoverCell = map.WorldToCell(world);
        hoverCell.z = 0;
        Vector3 center = map.GetCellCenterWorld(hoverCell);

        Handles.color = new Color(0.2f, 0.7f, 1f);
        Handles.DrawWireDisc(
            center,
            map.transform.forward,
            Mathf.Max(map.cellSize.x, map.cellSize.y) * 0.45f);
        Handles.Label(center, $"{label} {hoverCell}");
        HandleUtility.AddDefaultControl(
            GUIUtility.GetControlID(FocusType.Passive));

        if (current.type != EventType.MouseDown
            || current.button != 0
            || current.alt)
        {
            return false;
        }

        picked = hoverCell;
        pickingDestination = false;
        current.Use();
        SceneView.RepaintAll();
        return true;
    }
}
