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
    private SerializedProperty neighborDistanceVehicleUnitDataProp;
    private SerializedProperty sectorInfosProp;
    private SerializedProperty baseInfosProp;
    private static readonly System.Collections.Generic.List<SectorEdgeLine> drawnLines = new System.Collections.Generic.List<SectorEdgeLine>();
    private static readonly System.Collections.Generic.List<SectorEdgeLine> drawnRepLines = new System.Collections.Generic.List<SectorEdgeLine>();
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
        neighborDistanceVehicleUnitDataProp = serializedObject.FindProperty("neighborDistanceVehicleUnitData");
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
            EditorGUILayout.PropertyField(neighborDistanceReferenceUnitDataProp, new GUIContent("Foot Reference (Soldier)"));
        if (neighborDistanceVehicleUnitDataProp != null)
            EditorGUILayout.PropertyField(neighborDistanceVehicleUnitDataProp, new GUIContent("Vehicle Reference (APC)"));

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

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Desenhar linhas HQ → Rep (todos)"))
            DrawAllRepToHQLines(manager);
        using (new EditorGUI.DisabledScope(drawnRepLines.Count == 0))
        {
            if (GUILayout.Button("Limpar linhas HQ → Rep"))
                ClearRepLines();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);

        DrawSectorList(sectorInfosProp, "Sector Infos", manager, enableDrawButtons: true, enableRepButtons: true);
        EditorGUILayout.Space(4f);
        DrawSectorList(baseInfosProp, "Base Infos", manager, enableDrawButtons: false, enableRepButtons: true);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawSectorList(SerializedProperty prop, string label, SectorManager manager, bool enableDrawButtons, bool enableRepButtons = false)
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

            // Append controlling team + HQ distances to the foldout label.
            // controllingTeam shown first; distances sorted ascending (0h = home base first).
            SerializedProperty ctProp = element.FindPropertyRelative("controllingTeam");
            string ctName = ctProp != null
                ? ctProp.enumDisplayNames[Mathf.Clamp(ctProp.enumValueIndex, 0, ctProp.enumDisplayNames.Length - 1)]
                : null;

            SerializedProperty distancesProp = element.FindPropertyRelative("sectorDistances");
            var distParts = new System.Collections.Generic.List<(float dist, string text)>();
            if (distancesProp != null)
            {
                for (int d = 0; d < distancesProp.arraySize; d++)
                {
                    SerializedProperty td       = distancesProp.GetArrayElementAtIndex(d);
                    SerializedProperty tdTeam   = td.FindPropertyRelative("Team");
                    SerializedProperty tdEntries= td.FindPropertyRelative("Entries");
                    if (tdTeam == null || tdEntries == null) continue;
                    string teamName = tdTeam.enumDisplayNames[Mathf.Clamp(tdTeam.enumValueIndex, 0, tdTeam.enumDisplayNames.Length - 1)];
                    for (int e = 0; e < tdEntries.arraySize; e++)
                    {
                        SerializedProperty entry   = tdEntries.GetArrayElementAtIndex(e);
                        SerializedProperty isHQ    = entry.FindPropertyRelative("IsHQ");
                        SerializedProperty dist    = entry.FindPropertyRelative("Distance");
                        SerializedProperty vehDist = entry.FindPropertyRelative("VehicleDistance");
                        SerializedProperty airDist = entry.FindPropertyRelative("AirDistance");
                        if (isHQ == null || !isHQ.boolValue || dist == null) continue;
                        if (dist.floatValue >= float.MaxValue * 0.5f)
                        {
                            var reachableParts = new System.Collections.Generic.List<string>();
                            float reachableSortDist = float.MaxValue;
                            if (vehDist != null && vehDist.floatValue < float.MaxValue * 0.5f)
                            {
                                reachableParts.Add($"ðŸš—{vehDist.floatValue:F0}h");
                                reachableSortDist = Mathf.Min(reachableSortDist, vehDist.floatValue);
                            }
                            if (airDist != null && airDist.floatValue < float.MaxValue * 0.5f)
                            {
                                reachableParts.Add($"âœˆ{airDist.floatValue:F0}h");
                                reachableSortDist = Mathf.Min(reachableSortDist, airDist.floatValue);
                            }
                            if (reachableParts.Count > 0)
                                distParts.Add((reachableSortDist, BuildSectorDistanceSummary(teamName, dist.floatValue, vehDist, airDist)));
                            break;
                        }
                        if (dist.floatValue >= float.MaxValue * 0.5f) continue;

                        var parts = new System.Collections.Generic.List<string>();
                        parts.Add($"👣{dist.floatValue:F0}h");
                        if (vehDist != null && vehDist.floatValue < float.MaxValue * 0.5f)
                            parts.Add($"🚗{vehDist.floatValue:F0}h");
                        if (airDist != null && airDist.floatValue < float.MaxValue * 0.5f)
                            parts.Add($"✈{airDist.floatValue:F0}h");
                        distParts.Add((dist.floatValue, BuildSectorDistanceSummary(teamName, dist.floatValue, vehDist, airDist)));
                        break;
                    }
                }
                distParts.Sort((a, b) => a.dist.CompareTo(b.dist));
            }

            {
                var tagParts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(ctName)) tagParts.Add(ctName);
                foreach (var dp in distParts) tagParts.Add(dp.text);
                if (tagParts.Count > 0)
                    sectorLabel += $"  [{string.Join(" | ", tagParts)}]";
            }

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
                EditorGUILayout.BeginHorizontal();
                if (enableDrawButtons && GUILayout.Button("Desenhar linha"))
                {
                    DrawLinesForSector(manager, ReadSector(sectorProp), singleSectorLineColor, replaceExisting: true);
                    LogAndDrawNeighborDistanceDebug(ReadSector(sectorProp));
                }
                if (enableRepButtons && GUILayout.Button("HQ → Rep"))
                    DrawRepToHQLines(ReadSector(sectorProp), replaceExisting: true);
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }
        }
        EditorGUI.indentLevel--;
    }

    private static string BuildSectorDistanceSummary(
        string teamName,
        float footDistance,
        SerializedProperty vehicleDistance,
        SerializedProperty airDistance)
    {
        const float Unreachable = float.MaxValue * 0.5f;
        float vehicle = vehicleDistance != null ? vehicleDistance.floatValue : float.MaxValue;
        float air = airDistance != null ? airDistance.floatValue : float.MaxValue;

        string footText = footDistance < Unreachable ? $"{footDistance:F0}h" : "--";
        string vehicleText = vehicle < Unreachable ? $"{vehicle:F0}h" : "--";
        string airText = air < Unreachable ? $"{air:F0}h" : "--";

        return $"{teamName}: 👣 {footText} 🚗 {vehicleText} ✈ {airText}";
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
            AddNeighborLine(info, info.ClosestNeighbor1, info.ClosestNeighbor1Distance, ResolveNeighborLineColor(info, info.ClosestNeighbor1), seen);
            AddNeighborLine(info, info.ClosestNeighbor2, info.ClosestNeighbor2Distance, ResolveNeighborLineColor(info, info.ClosestNeighbor2), seen);
        }

        DrawHQToNearestRallyPointLines();
        SceneView.RepaintAll();
    }

    private static void DrawHQToNearestRallyPointLines()
    {
        ConstructionManager[] all = Object.FindObjectsByType<ConstructionManager>(FindObjectsSortMode.None);

        var rallyPoints = new System.Collections.Generic.List<ConstructionManager>();
        var hqs = new System.Collections.Generic.List<ConstructionManager>();
        foreach (ConstructionManager c in all)
        {
            if (c == null) continue;
            if (c.IsPlayerHeadQuarter) hqs.Add(c);
            if (c.IsRallyPoint) rallyPoints.Add(c);
        }

        if (hqs.Count == 0 || rallyPoints.Count == 0)
            return;

        foreach (ConstructionManager rally in rallyPoints)
        {
            Vector3Int rallyCell = rally.CurrentCellPosition; rallyCell.z = 0;

            int ownerSlot = rally.RallyOwnerSlotIndex;
            AddRallyOwnerLine(rallyCell, hqs, ownerSlot, explicitOwner: ownerSlot >= 0);
        }
    }

    private static void AddRallyOwnerLine(
        Vector3Int rallyCell,
        System.Collections.Generic.List<ConstructionManager> hqs,
        int ownerSlot,
        bool explicitOwner)
    {
        ConstructionManager ownerHQ = null;
        float ownerDist = float.MaxValue;

        if (explicitOwner)
        {
            foreach (ConstructionManager hq in hqs)
            {
                if (hq == null || hq.SlotIndex != ownerSlot)
                    continue;

                Vector3Int hqCell = hq.CurrentCellPosition; hqCell.z = 0;
                ownerDist = SectorManager.HexDistance(rallyCell, hqCell);
                ownerHQ = hq;
                break;
            }
        }
        else
        {
            foreach (ConstructionManager hq in hqs)
            {
                if (hq == null)
                    continue;

                Vector3Int hqCell = hq.CurrentCellPosition; hqCell.z = 0;
                float d = SectorManager.HexDistance(rallyCell, hqCell);
                if (d < ownerDist) { ownerDist = d; ownerHQ = hq; }
            }
        }

        if (ownerHQ == null)
        {
            drawnLines.Add(new SectorEdgeLine
            {
                FromCell = rallyCell,
                ToCell   = rallyCell,
                Label    = $"Rally owner slot {ownerSlot} sem HQ",
                Color    = new Color(1f, 0.1f, 0.8f, 1f),
            });
            return;
        }

        Vector3Int ownerHQCell = ownerHQ.CurrentCellPosition; ownerHQCell.z = 0;
        Color lineColor = TeamUtils.GetColor(ownerHQ.TeamId);
        lineColor.a = 1f;
        drawnLines.Add(new SectorEdgeLine
        {
            FromCell = ownerHQCell,
            ToCell   = rallyCell,
            Label    = explicitOwner
                ? $"HQ({ownerHQ.TeamId}) -> Rally owner slot {ownerSlot} ({ownerDist:F0}h)"
                : $"HQ({ownerHQ.TeamId}) -> Rally auto ({ownerDist:F0}h)",
            Color    = lineColor,
        });
    }

    private static Color ResolveNeighborLineColor(SectorManager.SectorInfo from, ConstructionSector toSector)
    {
        if (from == null || !SectorManager.TryGetSectorInfo(toSector, out SectorManager.SectorInfo to) || to == null)
            return allSectorLineColor;

        TeamId fromTeam = from.ControllingTeam;
        TeamId toTeam = to.ControllingTeam;
        if (fromTeam == TeamId.Neutral && toTeam == TeamId.Neutral)
            return new Color(0.75f, 0.75f, 0.75f, 1f);

        if (fromTeam == toTeam)
            return ResolveTeamLineColor(fromTeam);

        if (fromTeam == TeamId.Neutral)
            return WithAlpha(ResolveTeamLineColor(toTeam), 0.7f);

        if (toTeam == TeamId.Neutral)
            return WithAlpha(ResolveTeamLineColor(fromTeam), 0.7f);

        Color mixed = Color.Lerp(ResolveTeamLineColor(fromTeam), ResolveTeamLineColor(toTeam), 0.5f);
        mixed.a = 1f;
        return mixed;
    }

    private static Color ResolveTeamLineColor(TeamId team)
    {
        if (team == TeamId.Neutral)
            return new Color(0.75f, 0.75f, 0.75f, 1f);

        Color color = TeamUtils.GetColor(team);
        color.a = 1f;
        return color;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
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

    private static void ClearRepLines()
    {
        drawnRepLines.Clear();
        SceneView.RepaintAll();
    }

    private static void DrawRepToHQLines(ConstructionSector sector, bool replaceExisting)
    {
        if (replaceExisting)
            drawnRepLines.Clear();

        SectorManager.SectorInfo info = null;
        if (!SectorManager.TryGetSectorInfo(sector, out info) || info == null)
            SectorManager.TryGetBaseInfo(sector, out info);
        if (info == null) { SceneView.RepaintAll(); return; }

        Vector3Int repCell = info.RepresentativeCell; repCell.z = 0;
        ConstructionManager[] all = Object.FindObjectsByType<ConstructionManager>(FindObjectsSortMode.None);
        foreach (ConstructionManager c in all)
        {
            if (c == null || !c.IsPlayerHeadQuarter) continue;
            Vector3Int hqCell = c.CurrentCellPosition; hqCell.z = 0;
            float dist = GetHQDistanceFromInfo(info, c.TeamId);
            string distLabel = dist < float.MaxValue * 0.5f ? $" ({dist:F0}h)" : "";
            drawnRepLines.Add(new SectorEdgeLine
            {
                FromCell = hqCell,
                ToCell   = repCell,
                Label    = $"HQ({c.TeamId}) → {sector}{distLabel}",
                Color    = ResolveTeamLineColor(c.TeamId),
            });
        }
        SceneView.RepaintAll();
    }

    private static void DrawAllRepToHQLines(SectorManager manager)
    {
        drawnRepLines.Clear();
        if (manager == null) { SceneView.RepaintAll(); return; }

        ConstructionManager[] all = Object.FindObjectsByType<ConstructionManager>(FindObjectsSortMode.None);
        var hqs = new System.Collections.Generic.List<ConstructionManager>();
        foreach (ConstructionManager c in all)
            if (c != null && c.IsPlayerHeadQuarter) hqs.Add(c);

        foreach (SectorManager.SectorInfo info in manager.SectorInfos)
            AddRepHQLines(info, hqs);
        foreach (SectorManager.SectorInfo info in manager.BaseInfos)
            AddRepHQLines(info, hqs);

        SceneView.RepaintAll();
    }

    private static void AddRepHQLines(SectorManager.SectorInfo info, System.Collections.Generic.List<ConstructionManager> hqs)
    {
        if (info == null) return;
        Vector3Int repCell = info.RepresentativeCell; repCell.z = 0;
        foreach (ConstructionManager hq in hqs)
        {
            if (hq == null) continue;
            Vector3Int hqCell = hq.CurrentCellPosition; hqCell.z = 0;
            float dist = GetHQDistanceFromInfo(info, hq.TeamId);
            string distLabel = dist < float.MaxValue * 0.5f ? $" ({dist:F0}h)" : "";
            drawnRepLines.Add(new SectorEdgeLine
            {
                FromCell = hqCell,
                ToCell   = repCell,
                Label    = $"HQ({hq.TeamId}) → {info.Sector}{distLabel}",
                Color    = ResolveTeamLineColor(hq.TeamId),
            });
        }
    }

    private static float GetHQDistanceFromInfo(SectorManager.SectorInfo info, TeamId team)
    {
        if (info == null) return float.MaxValue;
        foreach (SectorManager.SectorTeamDistances td in info.SectorDistances)
        {
            if (td.Team != team) continue;
            foreach (SectorManager.SectorDistanceEntry e in td.Entries)
                if (e.IsHQ) return e.Distance;
        }
        return float.MaxValue;
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
        if (drawnLines.Count == 0 && drawnRepLines.Count == 0 && drawnPathMarkers.Count == 0)
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

        for (int i = 0; i < drawnRepLines.Count; i++)
        {
            SectorEdgeLine line = drawnRepLines[i];
            Vector3 from = map.GetCellCenterWorld(line.FromCell);
            Vector3 to = map.GetCellCenterWorld(line.ToCell);
            Vector3 mid = Vector3.Lerp(from, to, 0.5f);

            Handles.color = line.Color;
            Handles.DrawAAPolyLine(3f, from, to);
            Handles.SphereHandleCap(0, to, Quaternion.identity, 0.15f, EventType.Repaint);
            Handles.Label(mid + new Vector3(0.1f, 0.15f, 0f), line.Label);
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
