using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class HotzoneWindow : EditorWindow
{
    // Espelha ReachIntent. A janela nao interpreta a intencao: apenas escolhe
    // qual delas pedir ao servico unificado.
    private enum IntentMode
    {
        Combate,
        Logistica,
        Transferencia,
        Fusao,
        Embarque
    }

    private enum ViewMode
    {
        Todas,
        Tactical,
        Operational,
        Strategic
    }

    [SerializeField] private UnitManager unit;
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private DPQAirHeightConfig dpqAirHeightConfig;
    [SerializeField] private IntentMode intentMode = IntentMode.Combate;
    [SerializeField] private ViewMode viewMode = ViewMode.Todas;
    [SerializeField] private bool showCellCosts;

    private readonly HashSet<Vector3Int> tactical =
        new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> operational =
        new HashSet<Vector3Int>();
    private readonly HashSet<Vector3Int> strategic =
        new HashSet<Vector3Int>();
    private readonly Dictionary<Vector3Int, int> operationalCosts =
        new Dictionary<Vector3Int, int>();

    private string status = "Selecione uma unidade e calcule.";
    private bool hasResult;

    [MenuItem("Tools/Utils/Hotzone")]
    public static void Open()
    {
        GetWindow<HotzoneWindow>("Hotzone").Show();
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
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Hotzone — Tactical / Operational / Strategic",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Escolha a intenção: combate, atendimento logístico, " +
            "transferência de estoque, fusão ou embarque. Tactical mostra a " +
            "Hotzone confirmada dessa intenção na rodada atual, com o custo " +
            "de entrada resolvido pelo sensor Pode* correspondente. " +
            "Operational usa MP base x2: cúbico para aeronáuticas e " +
            "geográfico para as demais. Strategic é o restante do tabuleiro " +
            "e serve apenas como direção — o serviço não materializa células " +
            "para ela.",
            MessageType.Info);

        unit = (UnitManager)EditorGUILayout.ObjectField(
            "Unidade", unit, typeof(UnitManager), true);
        tilemap = (Tilemap)EditorGUILayout.ObjectField(
            "Tabuleiro", tilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database",
            terrainDatabase,
            typeof(TerrainDatabase),
            false);
        dpqAirHeightConfig = (DPQAirHeightConfig)EditorGUILayout.ObjectField(
            "DPQ Air Height",
            dpqAirHeightConfig,
            typeof(DPQAirHeightConfig),
            false);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            UseSelected();
        if (GUILayout.Button("Auto Detect"))
            AutoDetect();
        EditorGUILayout.EndHorizontal();

        intentMode = (IntentMode)EditorGUILayout.EnumPopup(
            "Intenção", intentMode);
        viewMode = (ViewMode)EditorGUILayout.EnumPopup(
            "Modalidade", viewMode);
        showCellCosts = EditorGUILayout.Toggle(
            "Mostrar custos Operational", showCellCosts);

        using (new EditorGUI.DisabledScope(
                   unit == null || tilemap == null))
        {
            if (GUILayout.Button("Calcular Hotzone", GUILayout.Height(30f)))
                Calculate();
        }

        EditorGUILayout.Space(5f);
        EditorGUILayout.HelpBox(status, MessageType.None);
        if (!hasResult)
            return;

        EditorGUILayout.LabelField(
            $"Tactical: {tactical.Count} células",
            EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            $"Operational: {operational.Count} células",
            EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            $"Strategic: {strategic.Count} células",
            EditorStyles.miniLabel);
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField(
            $"{ResolveIntentLabel()} | Verde = Tactical | " +
            "Azul = Operational | Cinza = Strategic",
            EditorStyles.miniLabel);
    }

    private void UseSelected()
    {
        GameObject selected = Selection.activeGameObject;
        UnitManager selectedUnit =
            selected != null
                ? selected.GetComponent<UnitManager>()
                : null;
        if (selectedUnit == null)
        {
            status = "A seleção atual não contém UnitManager.";
            return;
        }

        unit = selectedUnit;
        if (unit.BoardTilemap != null)
            tilemap = unit.BoardTilemap;
        AutoDetectAssets();
        status = $"Unidade selecionada: {unit.name}.";
        hasResult = false;
        SceneView.RepaintAll();
    }

    private void AutoDetect()
    {
        UseSelectedIfAvailable();
        if (tilemap == null && unit != null)
            tilemap = unit.BoardTilemap;
        if (tilemap == null)
        {
            UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
            if (spawner != null)
                tilemap = spawner.GetComponentInChildren<Tilemap>();
            if (tilemap == null)
                tilemap = FindFirstObjectByType<Tilemap>();
        }
        AutoDetectAssets();
        Repaint();
    }

    private void UseSelectedIfAvailable()
    {
        GameObject selected = Selection.activeGameObject;
        UnitManager selectedUnit =
            selected != null
                ? selected.GetComponent<UnitManager>()
                : null;
        if (selectedUnit != null)
            unit = selectedUnit;
    }

    private void AutoDetectAssets()
    {
        if (terrainDatabase == null)
            terrainDatabase = FindFirstAsset<TerrainDatabase>();
        if (dpqAirHeightConfig == null)
            dpqAirHeightConfig = FindFirstAsset<DPQAirHeightConfig>();
    }

    private static T FindFirstAsset<T>() where T : Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        return guids.Length > 0
            ? AssetDatabase.LoadAssetAtPath<T>(
                AssetDatabase.GUIDToAssetPath(guids[0]))
            : null;
    }

    private void Calculate()
    {
        tactical.Clear();
        operational.Clear();
        strategic.Clear();
        operationalCosts.Clear();

        if (unit == null || tilemap == null)
        {
            status = "Unidade ou tabuleiro ausente.";
            hasResult = false;
            return;
        }

        if (!unit.TryGetUnitData(out UnitData unitData)
            || unitData == null)
        {
            status = "UnitData ausente.";
            hasResult = false;
            return;
        }

        if (!TryResolveIntent(unitData, out ReachIntent intent))
        {
            hasResult = false;
            return;
        }

        // A janela nao calcula mais nada: pede as duas bandas materializaveis
        // ao servico unificado e apenas pinta o resultado. Strategic continua
        // sendo o complemento — o servico nao materializa celulas para ela.
        UnitReachProfile profile =
            UnitReachEnvelopeService.BuildProfile(new UnitReachRequest
            {
                Unit = unit,
                BoardMap = tilemap,
                TerrainDatabase = terrainDatabase,
                Intent = intent,
                Band = ReachBand.Tactical,
                MovementBudget = Mathf.Max(0, unit.MaxMovementPoints),
                FilterByOperationDomain = intent != ReachIntent.Combat,
                MovementMode = UnitThreatEnvelopeMovement.Potential,
                DpqAirHeightConfig = dpqAirHeightConfig,
                EnableLdt = true,
                EnableLos = true,
                EnableSpotter = true
            });

        if (profile.Tactical != null)
            tactical.UnionWith(profile.Tactical.ActionCells);

        int operationalBudget =
            AIActionReachCoordinator.ResolveOperationalBudget(unit);
        if (profile.Operational != null)
        {
            foreach (KeyValuePair<Vector3Int, int> pair
                     in profile.Operational.CostByCell)
            {
                Vector3Int costCell = pair.Key;
                costCell.z = 0;
                operationalCosts[costCell] = pair.Value;
            }

            foreach (Vector3Int rawCell in profile.Operational.ActionCells)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (!tactical.Contains(cell))
                    operational.Add(cell);
            }
        }

        foreach (Vector3Int rawCell in tilemap.cellBounds.allPositionsWithin)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (tilemap.GetTile(cell) == null
                || tactical.Contains(cell)
                || operational.Contains(cell))
                continue;
            strategic.Add(cell);
        }

        string geometry =
            AIActionReachCoordinator.UsesCubicSectorReach(unit)
                ? "cúbica (aeronáutica)"
                : "geográfica (caminhos e custos)";
        status =
            $"{unit.name}: intenção={ResolveIntentLabel()}; " +
            $"Tactical={tactical.Count}; " +
            $"Operational={operational.Count}, orçamento={operationalBudget}, " +
            $"regra={geometry}; Strategic={strategic.Count} (só direção).";
        hasResult = true;
        SceneView.RepaintAll();
    }

    private bool TryResolveIntent(UnitData unitData, out ReachIntent intent)
    {
        switch (intentMode)
        {
            case IntentMode.Logistica:
                intent = ReachIntent.Service;
                if (!unitData.isSupplier
                    || unitData.supplierServiceProfile
                        == SupplierServiceProfile.StockTransfer)
                {
                    status =
                        "A unidade não oferece atendimento logístico de campo.";
                    return false;
                }
                return true;

            case IntentMode.Transferencia:
                intent = ReachIntent.Transfer;
                if (!unitData.isSupplier
                    || unitData.supplierTier != SupplierTier.Hub)
                {
                    status = "A unidade não é Hub de transferência.";
                    return false;
                }
                return true;

            case IntentMode.Embarque:
                intent = ReachIntent.Embark;
                if (unit.IsEmbarked)
                {
                    status = "A unidade já está embarcada.";
                    return false;
                }
                return true;

            case IntentMode.Fusao:
                intent = ReachIntent.Fusion;
                return true;

            default:
                intent = ReachIntent.Combat;
                return true;
        }
    }

    private string ResolveIntentLabel()
    {
        switch (intentMode)
        {
            case IntentMode.Logistica:
                return "Logística";
            case IntentMode.Transferencia:
                return "Transferência";
            case IntentMode.Embarque:
                return "Embarque";
            case IntentMode.Fusao:
                return "Fusão";
            default:
                return "Combate";
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!hasResult || tilemap == null)
            return;

        if (viewMode == ViewMode.Todas
            || viewMode == ViewMode.Strategic)
        {
            DrawCells(
                strategic,
                new Color(0.45f, 0.45f, 0.45f, 0.10f),
                false);
        }
        if (viewMode == ViewMode.Todas
            || viewMode == ViewMode.Operational)
        {
            DrawCells(
                operational,
                new Color(0.10f, 0.55f, 1f, 0.25f),
                showCellCosts);
        }
        if (viewMode == ViewMode.Todas
            || viewMode == ViewMode.Tactical)
        {
            DrawCells(
                tactical,
                new Color(0.15f, 1f, 0.35f, 0.32f),
                false);
        }
    }

    private void DrawCells(
        IEnumerable<Vector3Int> cells,
        Color color,
        bool drawCosts)
    {
        Color previous = Handles.color;
        Handles.color = color;
        foreach (Vector3Int cell in cells)
        {
            Vector3 world = tilemap.GetCellCenterWorld(cell);
            float radius = Mathf.Max(
                0.15f,
                Mathf.Min(
                    Mathf.Abs(tilemap.cellSize.x),
                    Mathf.Abs(tilemap.cellSize.y)) * 0.42f);
            Handles.DrawSolidDisc(world, Vector3.forward, radius);
            if (drawCosts
                && operationalCosts.TryGetValue(cell, out int cost))
            {
                Handles.Label(
                    world,
                    cost.ToString(),
                    EditorStyles.miniBoldLabel);
            }
        }
        Handles.color = previous;
    }
}
