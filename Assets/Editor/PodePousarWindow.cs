using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public sealed class PodePousarWindow : EditorWindow
{
    [SerializeField] private UnitManager aircraft;
    [SerializeField] private Tilemap map;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private SensorMovementMode movementMode =
        SensorMovementMode.MoveuParado;
    [SerializeField] private bool hasDestination;
    [SerializeField] private Vector3Int destination;

    private bool pickingDestination;
    private Vector3Int hoverCell;
    private PodePousarReport report;
    private Domain predictedDomain;
    private HeightLevel predictedHeight;
    private bool hasPredictedLayer;
    private AirOperationTileContext evaluatedContext;
    private AirLandingEvaluation landingEvaluation;
    private string resolvedLayerSource;
    private bool occupancyAllowed;
    private UnitManager occupancyBlocker;
    private Vector2 detailsScroll;

    public static void Open()
    {
        GetWindow<PodePousarWindow>(
            "Pode Pousar").Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetect();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSelectionChange()
    {
        TryUseCurrentSelection();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Pode Pousar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura: simula o pouso no hex escolhido e restaura " +
            "imediatamente a posição da unidade. Nenhuma ação é confirmada.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        aircraft = (UnitManager)EditorGUILayout.ObjectField(
            "Aeronave", aircraft, typeof(UnitManager), true);
        map = (Tilemap)EditorGUILayout.ObjectField(
            "Tilemap", map, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database",
            terrainDatabase,
            typeof(TerrainDatabase),
            false);
        movementMode = (SensorMovementMode)EditorGUILayout.EnumPopup(
            "Estado de movimento", movementMode);
        if (EditorGUI.EndChangeCheck())
            ClearResult();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();

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

        Vector3Int effectiveCell =
            hasDestination
                ? destination
                : aircraft != null
                    ? aircraft.CurrentCellPosition
                    : Vector3Int.zero;
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Vector3IntField(
                "Hex avaliado", effectiveCell);

        using (new EditorGUI.DisabledScope(!hasDestination))
        {
            if (GUILayout.Button(
                    "Usar o Próprio Hex da Aeronave"))
            {
                hasDestination = false;
                pickingDestination = false;
                ClearResult();
                SceneView.RepaintAll();
            }
        }

        using (new EditorGUI.DisabledScope(
                   aircraft == null
                   || map == null
                   || terrainDatabase == null))
        {
            if (GUILayout.Button(
                    "Verificar Pouso", GUILayout.Height(28f)))
                Evaluate();
        }

        EditorGUILayout.Space(6f);
        if (report == null)
            return;

        EditorGUILayout.HelpBox(
            report.status
                ? $"PODE POUSAR\n{report.explicacao}"
                : $"NÃO PODE POUSAR\n{report.explicacao}",
            report.status
                ? MessageType.Info
                : MessageType.Warning);

        if (hasPredictedLayer)
        {
            EditorGUILayout.LabelField(
                "Camada após pousar",
                $"{predictedDomain} / {predictedHeight}");
        }

        DrawEvaluatedContext();
    }

    private void Evaluate()
    {
        AutoDetect();
        if (aircraft == null || map == null
            || terrainDatabase == null)
            return;

        Vector3Int testCell =
            hasDestination ? destination : aircraft.CurrentCellPosition;
        testCell.z = 0;

        evaluatedContext = AirOperationResolver.ResolveContext(
            map, terrainDatabase, testCell);
        landingEvaluation = AirOperationResolver.EvaluateLanding(
            aircraft, evaluatedContext, movementMode);
        LayerTransitionRules.TryResolvePrimaryLayerAtCell(
            map,
            terrainDatabase,
            testCell,
            out predictedDomain,
            out predictedHeight,
            out resolvedLayerSource);
        occupancyAllowed = UnitOccupancyRules.CanEndLayerTransitionAtCell(
            map,
            testCell,
            aircraft,
            predictedDomain,
            predictedHeight,
            out occupancyBlocker);

        // Consulta por hex: o sensor recebe a celula, ninguem e deslocado.
        report = PodePousarSensor.Evaluate(
            aircraft,
            map,
            terrainDatabase,
            movementMode,
            useManualRemainingMovement: false,
            manualRemainingMovement: 0,
            atCell: testCell);

        hasPredictedLayer = report != null && report.status;
        if (hasPredictedLayer)
        {
            predictedDomain = report.landingDomain;
            predictedHeight = report.landingHeight;
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private void DrawEvaluatedContext()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Contexto avaliado", EditorStyles.boldLabel);
        detailsScroll = EditorGUILayout.BeginScrollView(
            detailsScroll, GUILayout.MinHeight(175f));
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Hierarquia vencedora", evaluatedContext.source.ToString());
        EditorGUILayout.LabelField(
            "Construção",
            evaluatedContext.construction != null
                ? evaluatedContext.construction.displayName
                : "—");
        EditorGUILayout.LabelField(
            "Estrutura",
            evaluatedContext.structure != null
                ? evaluatedContext.structure.displayName
                : "—");
        EditorGUILayout.LabelField(
            "Terreno",
            evaluatedContext.terrain != null
                ? evaluatedContext.terrain.displayName
                : "—");
        EditorGUILayout.LabelField(
            "Camada física",
            $"{predictedDomain} / {predictedHeight} ({resolvedLayerSource})");
        EditorGUILayout.LabelField(
            "Aircraft Ops",
            $"allow={landingEvaluation.allowed} | superfície={evaluatedContext.landingSurface}");
        EditorGUILayout.LabelField(
            "Ocupação",
            occupancyAllowed
                ? "Livre"
                : $"Bloqueada por {(occupancyBlocker != null ? occupancyBlocker.UnitDisplayName : "unidade")}");

        DrawLandingSkillContext();
        DrawAircraftSkills();

        if (!string.IsNullOrWhiteSpace(landingEvaluation.reason))
            EditorGUILayout.HelpBox(landingEvaluation.reason, MessageType.Warning);

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void DrawLandingSkillContext()
    {
        IReadOnlyList<SkillData> required = null;
        bool anySkill = false;

        if (evaluatedContext.source == AirOperationRuleSource.Terrain
            && evaluatedContext.terrain != null)
        {
            required = evaluatedContext.terrain.requiredLandingSkills;
            anySkill = evaluatedContext.terrain.requireAtLeastOneLandingSkill;
        }
        else if (evaluatedContext.source == AirOperationRuleSource.Construction
                 && evaluatedContext.construction != null)
        {
            var skills = new List<SkillData>();
            List<ConstructionLandingSkillRule> rules =
                evaluatedContext.construction.requiredLandingSkillRules;
            if (rules != null)
            {
                for (int i = 0; i < rules.Count; i++)
                    if (rules[i] != null && rules[i].skill != null)
                        skills.Add(rules[i].skill);
            }
            required = skills;
            anySkill = evaluatedContext.construction.requireAtLeastOneLandingSkill;
        }
        else if (evaluatedContext.source == AirOperationRuleSource.Structure
                 && evaluatedContext.structure != null
                 && evaluatedContext.structure.aircraftOpsByTerrain != null)
        {
            for (int i = 0; i < evaluatedContext.structure.aircraftOpsByTerrain.Count; i++)
            {
                StructureAirOpsTerrainRule rule =
                    evaluatedContext.structure.aircraftOpsByTerrain[i];
                if (rule == null || rule.terrainData != evaluatedContext.terrain)
                    continue;

                var skills = new List<SkillData>();
                if (rule.requiredLandingSkillRules != null)
                {
                    for (int j = 0; j < rule.requiredLandingSkillRules.Count; j++)
                    {
                        StructureLandingSkillRule entry =
                            rule.requiredLandingSkillRules[j];
                        if (entry != null && entry.skill != null)
                            skills.Add(entry.skill);
                    }
                }
                required = skills;
                anySkill = rule.requireAtLeastOneLandingSkill;
                break;
            }
        }

        EditorGUILayout.LabelField(
            "Regra de skills",
            required == null || required.Count == 0
                ? "Nenhuma skill explícita"
                : anySkill ? "OU — pelo menos 1" : "E — exige todas");

        if (required == null)
            return;
        for (int i = 0; i < required.Count; i++)
        {
            SkillData skill = required[i];
            if (skill == null)
                continue;
            bool has = aircraft != null && aircraft.HasSkill(skill);
            EditorGUILayout.LabelField(
                $"  {(has ? "✓" : "✗")} {skill.displayName}",
                has ? EditorStyles.boldLabel : EditorStyles.miniLabel);
        }
    }

    private void DrawAircraftSkills()
    {
        EditorGUILayout.LabelField("Skills da aeronave", EditorStyles.boldLabel);
        if (aircraft == null
            || !aircraft.TryGetUnitData(out UnitData data)
            || data == null
            || data.skills == null
            || data.skills.Count == 0)
        {
            EditorGUILayout.LabelField("  Nenhuma", EditorStyles.miniLabel);
            return;
        }

        for (int i = 0; i < data.skills.Count; i++)
        {
            SkillData skill = data.skills[i];
            if (skill != null)
                EditorGUILayout.LabelField($"  • {skill.displayName}", EditorStyles.miniLabel);
        }
    }

    private void TryUseCurrentSelection()
    {
        GameObject selected = Selection.activeGameObject;
        UnitManager unit = selected != null
            ? selected.GetComponentInParent<UnitManager>()
            : null;
        if (unit == null)
            return;

        aircraft = unit;
        AutoDetect();
        ClearResult();
    }

    private void AutoDetect()
    {
        if (aircraft != null
            && aircraft.BoardTilemap != null)
            map = aircraft.BoardTilemap;

        if (map == null)
        {
            Tilemap[] maps =
                Object.FindObjectsByType<Tilemap>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] != null
                    && maps[i].name == "Tilemap")
                {
                    map = maps[i];
                    break;
                }
            }
        }

        if (terrainDatabase == null)
        {
            string[] guids =
                AssetDatabase.FindAssets("t:TerrainDatabase");
            if (guids.Length > 0)
            {
                terrainDatabase =
                    AssetDatabase.LoadAssetAtPath<TerrainDatabase>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
    }

    private void ClearResult()
    {
        report = null;
        hasPredictedLayer = false;
        resolvedLayerSource = string.Empty;
        occupancyBlocker = null;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!pickingDestination || map == null)
            return;

        Event current = Event.current;
        Ray ray =
            HandleUtility.GUIPointToWorldRay(
                current.mousePosition);
        Plane plane = new Plane(
            map.transform.forward,
            map.transform.position);
        if (!plane.Raycast(ray, out float distance))
            return;

        Vector3 world = ray.GetPoint(distance);
        hoverCell = map.WorldToCell(world);
        hoverCell.z = 0;
        Vector3 center = map.GetCellCenterWorld(hoverCell);

        Handles.color = new Color(1f, 0.8f, 0.1f);
        Handles.DrawWireDisc(
            center,
            map.transform.forward,
            Mathf.Max(
                map.cellSize.x,
                map.cellSize.y) * 0.45f);
        Handles.Label(center, $"Pouso {hoverCell}");
        HandleUtility.AddDefaultControl(
            GUIUtility.GetControlID(
                FocusType.Passive));

        if (current.type == EventType.MouseDown
            && current.button == 0
            && !current.alt)
        {
            destination = hoverCell;
            hasDestination = true;
            pickingDestination = false;
            ClearResult();
            current.Use();
            Repaint();
            SceneView.RepaintAll();
        }
    }
}
