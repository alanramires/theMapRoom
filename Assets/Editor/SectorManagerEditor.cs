using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[CustomPropertyDrawer(typeof(SectorManager.SectorTeamDistances))]
public class SectorTeamDistancesDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty teamProp    = property.FindPropertyRelative("Team");
        SerializedProperty entriesProp = property.FindPropertyRelative("Entries");

        string teamName = teamProp != null
            ? teamProp.enumDisplayNames[Mathf.Clamp(teamProp.enumValueIndex, 0, teamProp.enumDisplayNames.Length - 1)]
            : label.text;

        float lineH   = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        Rect foldRect = new Rect(position.x, position.y, position.width, lineH);
        property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, teamName, true);

        if (!property.isExpanded || entriesProp == null)
            return;

        float y = position.y + lineH + spacing;
        EditorGUI.indentLevel++;
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entry    = entriesProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = entry.FindPropertyRelative("ConstructionName");
            SerializedProperty distProp = entry.FindPropertyRelative("Distance");
            SerializedProperty hqProp   = entry.FindPropertyRelative("IsHQ");

            string entryLabel = (nameProp != null ? nameProp.stringValue : "?")
                + (distProp != null ? $": {distProp.floatValue:F0}h" : "")
                + (hqProp != null && hqProp.boolValue ? " [HQ]" : "");

            Rect entryRect = new Rect(position.x, y, position.width, lineH);
            EditorGUI.LabelField(entryRect, entryLabel);
            y += lineH + spacing;
        }
        EditorGUI.indentLevel--;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineH   = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        if (!property.isExpanded) return lineH;

        SerializedProperty entriesProp = property.FindPropertyRelative("Entries");
        int count = entriesProp != null ? entriesProp.arraySize : 0;
        return lineH + (lineH + spacing) * count;
    }
}

[CustomPropertyDrawer(typeof(SectorManager.SectorRiskEntry))]
public class SectorRiskEntryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty teamProp = property.FindPropertyRelative("Team");
        if (teamProp != null)
        {
            int idx = Mathf.Clamp(teamProp.enumValueIndex, 0, teamProp.enumDisplayNames.Length - 1);
            label.text = teamProp.enumDisplayNames[idx];
        }
        EditorGUI.PropertyField(position, property, label, includeChildren: true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUI.GetPropertyHeight(property, label, includeChildren: true);
}

[CustomEditor(typeof(SectorManager))]
public class SectorManagerEditor : Editor
{
    private SerializedProperty sectorLogProp;
    private SerializedProperty useTerrainCostForNeighborDistancesProp;
    private SerializedProperty neighborDistanceTilemapProp;
    private SerializedProperty neighborDistanceTerrainDatabaseProp;
    private SerializedProperty neighborDistanceReferenceUnitDataProp;
    private SerializedProperty sectorInfosProp;
    private SerializedProperty baseInfosProp;
    private static readonly System.Collections.Generic.List<SectorEdgeLine> drawnLines = new System.Collections.Generic.List<SectorEdgeLine>();
    private static readonly System.Collections.Generic.List<SectorPathMarker> drawnPathMarkers = new System.Collections.Generic.List<SectorPathMarker>();
    private static readonly Color singleSectorLineColor = new Color(0.15f, 0.9f, 1f, 1f);
    private static readonly Color allSectorLineColor = new Color(1f, 0.82f, 0.2f, 1f);
    private static readonly Color pathMarkerColor = new Color(1f, 0.25f, 0.15f, 1f);

    private struct SectorEdgeLine
    {
        public Vector3Int FromCell;
        public Vector3Int ToCell;
        public string Label;
        public Color Color;
    }

    private struct SectorPathMarker
    {
        public Vector3Int Cell;
        public string Label;
        public Color Color;
    }

    private void OnEnable()
    {
        sectorLogProp   = serializedObject.FindProperty("sectorLog");
        useTerrainCostForNeighborDistancesProp = serializedObject.FindProperty("useTerrainCostForNeighborDistances");
        neighborDistanceTilemapProp = serializedObject.FindProperty("neighborDistanceTilemap");
        neighborDistanceTerrainDatabaseProp = serializedObject.FindProperty("neighborDistanceTerrainDatabase");
        neighborDistanceReferenceUnitDataProp = serializedObject.FindProperty("neighborDistanceReferenceUnitData");
        sectorInfosProp = serializedObject.FindProperty("sectorInfos");
        baseInfosProp   = serializedObject.FindProperty("baseInfos");
        if (useTerrainCostForNeighborDistancesProp != null && !useTerrainCostForNeighborDistancesProp.boolValue)
        {
            useTerrainCostForNeighborDistancesProp.boolValue = true;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (sectorLogProp != null)
            EditorGUILayout.PropertyField(sectorLogProp);

        if (useTerrainCostForNeighborDistancesProp != null)
            EditorGUILayout.PropertyField(useTerrainCostForNeighborDistancesProp, new GUIContent("Use Terrain Cost For Neighbors"));
        if (neighborDistanceTilemapProp != null)
            EditorGUILayout.PropertyField(neighborDistanceTilemapProp, new GUIContent("Neighbor Distance Tilemap"));
        if (neighborDistanceTerrainDatabaseProp != null)
            EditorGUILayout.PropertyField(neighborDistanceTerrainDatabaseProp, new GUIContent("Neighbor Distance Terrain DB"));
        if (neighborDistanceReferenceUnitDataProp != null)
            EditorGUILayout.PropertyField(neighborDistanceReferenceUnitDataProp, new GUIContent("Neighbor Distance Reference Unit Data"));

        EditorGUILayout.Space(4f);

        SectorManager manager = (SectorManager)target;
        if (GUILayout.Button("Rebuild From Active Constructions"))
        {
            serializedObject.ApplyModifiedProperties();
            manager.RebuildFromActiveConstructions();
            serializedObject.Update();
            EditorUtility.SetDirty(manager);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Desenhar todas as linhas"))
            DrawAllSectorNeighborLines(manager);
        using (new EditorGUI.DisabledScope(drawnLines.Count == 0 && drawnPathMarkers.Count == 0))
        {
            if (GUILayout.Button("Limpar linhas"))
                ClearDrawnLines();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);

        DrawSectorList(sectorInfosProp, "Sector Infos", manager, enableDrawButtons: true);
        EditorGUILayout.Space(4f);
        DrawSectorList(baseInfosProp, "Base Infos", manager, enableDrawButtons: false);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawSectorList(SerializedProperty prop, string label, SectorManager manager, bool enableDrawButtons)
    {
        if (prop == null)
            return;

        EditorGUILayout.LabelField($"{label} ({prop.arraySize})", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        for (int i = 0; i < prop.arraySize; i++)
        {
            SerializedProperty element = prop.GetArrayElementAtIndex(i);
            if (element == null)
                continue;

            SerializedProperty sectorProp = element.FindPropertyRelative("sector");
            string sectorLabel = sectorProp != null
                ? sectorProp.enumDisplayNames[Mathf.Clamp(sectorProp.enumValueIndex, 0, sectorProp.enumDisplayNames.Length - 1)]
                : $"Setor {i}";

            EditorGUILayout.PropertyField(element, new GUIContent(sectorLabel), includeChildren: true);

            if (element.isExpanded)
            {
                SerializedProperty n1 = element.FindPropertyRelative("closestNeighbor1");
                SerializedProperty d1 = element.FindPropertyRelative("closestNeighbor1Distance");
                SerializedProperty n2 = element.FindPropertyRelative("closestNeighbor2");
                SerializedProperty d2 = element.FindPropertyRelative("closestNeighbor2Distance");

                string FormatNeighbor(SerializedProperty nProp, SerializedProperty dProp)
                {
                    if (nProp == null || dProp == null || dProp.floatValue >= float.MaxValue * 0.5f)
                        return "—";
                    string name = nProp.enumDisplayNames[Mathf.Clamp(nProp.enumValueIndex, 0, nProp.enumDisplayNames.Length - 1)];
                    return $"{name} ({dProp.floatValue:F0}h)";
                }

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Neighbors", $"{FormatNeighbor(n1, d1)}  |  {FormatNeighbor(n2, d2)}");
                if (enableDrawButtons && GUILayout.Button("Desenhar linha"))
                {
                    DrawLinesForSector(manager, ReadSector(sectorProp), singleSectorLineColor, replaceExisting: true);
                    LogAndDrawNeighborDistanceDebug(ReadSector(sectorProp));
                }
                EditorGUI.indentLevel--;
            }
        }
        EditorGUI.indentLevel--;
    }

    private static ConstructionSector ReadSector(SerializedProperty sectorProp)
    {
        if (sectorProp == null)
            return default;
        return (ConstructionSector)sectorProp.enumValueIndex;
    }

    private static void DrawLinesForSector(SectorManager manager, ConstructionSector sector, Color color, bool replaceExisting)
    {
        if (replaceExisting)
        {
            drawnLines.Clear();
            drawnPathMarkers.Clear();
        }

        if (manager == null || !SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info) || info == null)
        {
            SceneView.RepaintAll();
            return;
        }

        AddNeighborLine(info, info.ClosestNeighbor1, info.ClosestNeighbor1Distance, color);
        AddNeighborLine(info, info.ClosestNeighbor2, info.ClosestNeighbor2Distance, color);
        SceneView.RepaintAll();
    }

    private static void DrawAllSectorNeighborLines(SectorManager manager)
    {
        drawnLines.Clear();
        drawnPathMarkers.Clear();
        if (manager == null)
        {
            SceneView.RepaintAll();
            return;
        }

        System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>();
        foreach (SectorManager.SectorInfo info in manager.SectorInfos)
        {
            if (info == null)
                continue;
            AddNeighborLine(info, info.ClosestNeighbor1, info.ClosestNeighbor1Distance, allSectorLineColor, seen);
            AddNeighborLine(info, info.ClosestNeighbor2, info.ClosestNeighbor2Distance, allSectorLineColor, seen);
        }
        SceneView.RepaintAll();
    }

    private static void AddNeighborLine(
        SectorManager.SectorInfo from,
        ConstructionSector toSector,
        float distance,
        Color color,
        System.Collections.Generic.HashSet<string> seen = null)
    {
        if (from == null || distance >= float.MaxValue * 0.5f)
            return;
        if (!SectorManager.TryGetSectorInfo(toSector, out SectorManager.SectorInfo to) || to == null)
            return;

        int a = (int)from.Sector;
        int b = (int)to.Sector;
        if (seen != null)
        {
            string key = a < b ? $"{a}:{b}" : $"{b}:{a}";
            if (!seen.Add(key))
                return;
        }

        drawnLines.Add(new SectorEdgeLine
        {
            FromCell = from.RepresentativeCell,
            ToCell = to.RepresentativeCell,
            Label = $"{from.Sector} -> {to.Sector} ({distance:F0}h)",
            Color = color,
        });
    }

    private static void ClearDrawnLines()
    {
        drawnLines.Clear();
        drawnPathMarkers.Clear();
        SceneView.RepaintAll();
    }

    private static void LogAndDrawNeighborDistanceDebug(ConstructionSector sector)
    {
        var entries = new System.Collections.Generic.List<SectorManager.SectorNeighborDistanceDebugEntry>();
        if (!SectorManager.TryBuildNeighborDistanceDebug(sector, entries))
        {
            Debug.LogWarning($"[SectorGraphDebug] Falha ao calcular debug de vizinhos para {sector}.");
            SceneView.RepaintAll();
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[SectorGraphDebug] {sector} - candidatos por custo:");
        for (int i = 0; i < entries.Count; i++)
        {
            SectorManager.SectorNeighborDistanceDebugEntry e = entries[i];
            bool winner = i < 2;
            sb.Append(winner ? "  * " : "    ");
            sb.Append($"{e.Sector}: dist={e.Distance:F0} mode={(e.UsedTerrainCost ? "terrain-cost" : "hex-fallback")}");
            if (e.Path != null && e.Path.Count > 0)
            {
                sb.Append(" path=");
                for (int p = 0; p < e.Path.Count; p++)
                {
                    Vector3Int cell = e.Path[p];
                    sb.Append(p == 0 ? "" : " -> ");
                    sb.Append($"({cell.x},{cell.y})");
                }
            }
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());

        drawnPathMarkers.Clear();
        int winners = Mathf.Min(2, entries.Count);
        for (int i = 0; i < winners; i++)
        {
            SectorManager.SectorNeighborDistanceDebugEntry e = entries[i];
            if (e.Path == null || e.Path.Count == 0)
                continue;

            for (int p = 0; p < e.Path.Count; p++)
            {
                Vector3Int cell = e.Path[p];
                drawnPathMarkers.Add(new SectorPathMarker
                {
                    Cell = cell,
                    Label = $"{e.Sector} #{p}",
                    Color = i == 0 ? pathMarkerColor : singleSectorLineColor,
                });
            }
        }

        SceneView.RepaintAll();
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (drawnLines.Count == 0)
            return;

        Tilemap map = ResolveDrawTilemap();
        if (map == null)
            return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        for (int i = 0; i < drawnLines.Count; i++)
        {
            SectorEdgeLine line = drawnLines[i];
            Vector3 from = map.GetCellCenterWorld(line.FromCell);
            Vector3 to = map.GetCellCenterWorld(line.ToCell);
            Vector3 mid = Vector3.Lerp(from, to, 0.5f);

            Handles.color = line.Color;
            Handles.DrawAAPolyLine(4f, from, to);
            Handles.SphereHandleCap(0, from, Quaternion.identity, 0.12f, EventType.Repaint);
            Handles.SphereHandleCap(0, to, Quaternion.identity, 0.12f, EventType.Repaint);
            Handles.Label(mid + new Vector3(0.1f, 0.1f, 0f), line.Label);
        }

        for (int i = 0; i < drawnPathMarkers.Count; i++)
        {
            SectorPathMarker marker = drawnPathMarkers[i];
            Vector3 pos = map.GetCellCenterWorld(marker.Cell);
            Handles.color = marker.Color;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, 0.18f, EventType.Repaint);
            Handles.Label(pos + new Vector3(0.12f, 0.12f, 0f), marker.Label);
        }
    }

    private static Tilemap ResolveDrawTilemap()
    {
        CursorController cursor = Object.FindAnyObjectByType<CursorController>();
        if (cursor != null && cursor.BoardTilemap != null)
            return cursor.BoardTilemap;

        ConstructionManager construction = Object.FindAnyObjectByType<ConstructionManager>();
        if (construction != null && construction.BoardTilemap != null)
            return construction.BoardTilemap;

        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
            if (maps[i] != null && string.Equals(maps[i].name, "TileMap", System.StringComparison.OrdinalIgnoreCase))
                return maps[i];
        return maps != null && maps.Length > 0 ? maps[0] : null;
    }
}
