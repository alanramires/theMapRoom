using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeMirarSensorDebugWindow : EditorWindow
{
    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private Color rangeOverlayColor = new Color(0.2f, 0.8f, 1f, 0.45f);
    [SerializeField] private Color lineOfFireOverlayColor = new Color(1f, 0.35f, 0.35f, 0.45f);
    [SerializeField] private SensorMovementMode movementMode = SensorMovementMode.MoveuParado;
    [SerializeField] private bool logToConsole = true;

    private readonly List<PodeMirarTargetOption> results = new List<PodeMirarTargetOption>();
    private readonly List<PodeMirarInvalidOption> invalidResults = new List<PodeMirarInvalidOption>();
    private Vector2 scroll;
    private string statusMessage = "Ready.";
    private bool hasSelectedLine;
    private Vector3Int selectedLineStartCell;
    private Vector3Int selectedLineEndCell;
    private List<Vector3Int> selectedLineIntermediateCells = new List<Vector3Int>();
    private Color selectedLineColor = Color.green;
    private string selectedLineLabel = string.Empty;
    private bool isRangeMapPainted;
    private bool isLineOfFireMapPainted;
    private readonly List<Vector3Int> paintedRangeCells = new List<Vector3Int>();
    private readonly List<Vector3Int> paintedLineOfFireCells = new List<Vector3Int>();

    [MenuItem("Tools/Sensors/Simular Pode Mirar")]
    public static void OpenWindow()
    {
        GetWindow<PodeMirarSensorDebugWindow>("Simular Pode Mirar");
    }

    private void OnEnable()
    {
        AutoDetectContext();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        ClearPaintedRangeMap();
        ClearPaintedLineOfFireMap();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sensor Debug", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Selecione uma unidade em campo, escolha o modo de movimento e rode a simulacao do sensor Pode Mirar.", MessageType.Info);

        selectedUnit = (UnitManager)EditorGUILayout.ObjectField("Unidade", selectedUnit, typeof(UnitManager), true);
        turnStateManager = (TurnStateManager)EditorGUILayout.ObjectField("TurnStateManager", turnStateManager, typeof(TurnStateManager), true);
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        movementMode = (SensorMovementMode)EditorGUILayout.EnumPopup("Modo", movementMode);
        logToConsole = EditorGUILayout.ToggleLeft("Log no Console", logToConsole);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();

        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();

        if (GUILayout.Button("Simular"))
            RunSimulation();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(isRangeMapPainted ? "Desligar RangeMap" : "Pintar RangeMap"))
            TogglePaintRangeMap();
        if (GUILayout.Button(isLineOfFireMapPainted ? "Desligar LinhaDeTiroMap" : "Pintar LinhaDeTiroMap"))
            TogglePaintLineOfFireMap();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        if (hasSelectedLine)
            EditorGUILayout.HelpBox($"Linha selecionada: {selectedLineLabel}", MessageType.None);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField($"Resultados ({results.Count})", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (results.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum alvo encontrado.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < results.Count; i++)
            {
                DrawResultItem(i, results[i]);
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Resultados Invalidos ({invalidResults.Count})", EditorStyles.boldLabel);
        if (invalidResults.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhum alvo invalido registrado nesta simulacao.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < invalidResults.Count; i++)
            {
                DrawInvalidResultItem(i, invalidResults[i]);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void TryUseCurrentSelection()
    {
        if (Selection.activeGameObject == null)
            return;

        UnitManager unit = Selection.activeGameObject.GetComponent<UnitManager>();
        if (unit == null)
            unit = Selection.activeGameObject.GetComponentInParent<UnitManager>();

        if (unit != null)
            selectedUnit = unit;
    }

    private void RunSimulation()
    {
        results.Clear();
        invalidResults.Clear();

        if (selectedUnit == null)
        {
            statusMessage = "Selecione uma unidade valida.";
            return;
        }

        Tilemap map = overrideTilemap != null ? overrideTilemap : selectedUnit.BoardTilemap;
        TerrainDatabase db = terrainDatabase != null ? terrainDatabase : FindFirstTerrainDatabaseAsset();
        bool canAim = PodeMirarSensor.CollectTargets(selectedUnit, map, db, movementMode, results, invalidResults);
        statusMessage = canAim
            ? $"Sensor TRUE. {results.Count} opcao(oes) valida(s), {invalidResults.Count} invalida(s)."
            : $"Sensor FALSE. Nenhum alvo elegivel ({invalidResults.Count} invalido(s)).";

        if (!logToConsole)
            return;

        Debug.Log($"[PodeMirarSensorDebug] Unit={selectedUnit.name}, Mode={movementMode}, CanAim={canAim}, Validos={results.Count}, Invalidos={invalidResults.Count}");
        for (int i = 0; i < results.Count; i++)
        {
            PodeMirarTargetOption item = results[i];
            if (item == null)
                continue;

            string attackerName = item.attackerUnit != null ? item.attackerUnit.name : "(null)";
            string targetName = item.targetUnit != null ? item.targetUnit.name : "(null)";
            string weaponName = item.weapon != null
                ? (!string.IsNullOrWhiteSpace(item.weapon.displayName) ? item.weapon.displayName : item.weapon.name)
                : "(sem arma)";
            string targetLayer = item.targetUnit != null ? $"{item.targetUnit.GetDomain()}/{item.targetUnit.GetHeightLevel()}" : "-";
            string counterWeapon = item.defenderCounterWeapon != null
                ? (!string.IsNullOrWhiteSpace(item.defenderCounterWeapon.displayName) ? item.defenderCounterWeapon.displayName : item.defenderCounterWeapon.name)
                : "-";

            Debug.Log($"[PodeMirarSensorDebug][VALIDO] {i + 1}. {attackerName} -> {targetName} | arma={weaponName} | dist={item.distance} | alvoLayer={targetLayer} | revide={item.defenderCanCounterAttack} | armaRevide={counterWeapon} | distRevide={item.defenderCounterDistance} | linha={FormatCells(item.lineOfFireIntermediateCells)}");
        }

        for (int i = 0; i < invalidResults.Count; i++)
        {
            PodeMirarInvalidOption item = invalidResults[i];
            if (item == null)
                continue;

            string attackerName = item.attackerUnit != null ? item.attackerUnit.name : "(null)";
            string targetName = item.targetUnit != null ? item.targetUnit.name : "(null)";
            string weaponName = item.weapon != null
                ? (!string.IsNullOrWhiteSpace(item.weapon.displayName) ? item.weapon.displayName : item.weapon.name)
                : "(sem arma)";

            Debug.Log($"[PodeMirarSensorDebug][INVALIDO] {i + 1}. {attackerName} -> {targetName} | arma={weaponName} | dist={item.distance} | motivo={item.reason} | bloqueio={item.blockedCell.x},{item.blockedCell.y} | linha={FormatCells(item.lineOfFireIntermediateCells)}");
        }
    }

    private void AutoDetectContext()
    {
        if (turnStateManager == null)
            turnStateManager = Object.FindAnyObjectByType<TurnStateManager>();

        if (overrideTilemap == null)
            overrideTilemap = FindPreferredTilemap();

        ResolveContextFromTurnStateManager();
        if (terrainDatabase == null)
            terrainDatabase = FindFirstTerrainDatabaseAsset();
    }

    private static Tilemap FindPreferredTilemap()
    {
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (maps == null || maps.Length == 0)
            return null;

        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;
            if (string.Equals(map.name, "Tilemap", System.StringComparison.OrdinalIgnoreCase))
                return map;
        }

        return maps[0];
    }

    private static TerrainDatabase FindFirstTerrainDatabaseAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TerrainDatabase db = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(path);
            if (db != null)
                return db;
        }

        return null;
    }

    private static Tilemap FindMapByName(params string[] names)
    {
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (maps == null || maps.Length == 0)
            return null;

        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            string lower = map.name.ToLowerInvariant();
            for (int j = 0; j < names.Length; j++)
            {
                if (lower == names[j])
                    return map;
            }
        }

        return null;
    }

    private void DrawResultItem(int index, PodeMirarTargetOption item)
    {
        if (item == null)
            return;

        string weaponName = item.weapon != null
            ? (!string.IsNullOrWhiteSpace(item.weapon.displayName) ? item.weapon.displayName : item.weapon.name)
            : "(sem arma)";
        string attackerName = item.attackerUnit != null ? item.attackerUnit.name : "(null)";
        string targetName = item.targetUnit != null ? item.targetUnit.name : "(null)";
        string targetLayer = item.targetUnit != null ? $"{item.targetUnit.GetDomain()}/{item.targetUnit.GetHeightLevel()}" : "-";
        string label = string.IsNullOrWhiteSpace(item.displayLabel) ? $"alvo {index + 1}" : item.displayLabel;
        string counterWeapon = item.defenderCounterWeapon != null
            ? (!string.IsNullOrWhiteSpace(item.defenderCounterWeapon.displayName) ? item.defenderCounterWeapon.displayName : item.defenderCounterWeapon.name)
            : "-";

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"{index + 1}. {label}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Atacante", attackerName);
        EditorGUILayout.LabelField("Alvo", targetName);
        EditorGUILayout.LabelField("Arma", weaponName);
        EditorGUILayout.LabelField("Distancia", item.distance.ToString());
        EditorGUILayout.LabelField("Layer do Alvo", targetLayer);
        EditorGUILayout.LabelField("Defensor revida ?", item.defenderCanCounterAttack ? "Sim" : "Nao");
        EditorGUILayout.LabelField("Com que arma", counterWeapon);
        EditorGUILayout.LabelField("Distancia (revide)", item.defenderCanCounterAttack ? item.defenderCounterDistance.ToString() : "-");
        if (!item.defenderCanCounterAttack)
            EditorGUILayout.LabelField("Motivo sem revide", string.IsNullOrWhiteSpace(item.defenderCounterReason) ? "-" : item.defenderCounterReason);
        EditorGUILayout.LabelField("Linha (hexes intermediarios)", FormatCells(item.lineOfFireIntermediateCells));
        if (GUILayout.Button("Desenhar Linha no Scene View"))
            SelectLineForDrawing(item.attackerUnit, item.targetUnit, item.lineOfFireIntermediateCells, Color.green, $"VAL: {attackerName} -> {targetName}");
        EditorGUILayout.EndVertical();
    }

    private void DrawInvalidResultItem(int index, PodeMirarInvalidOption item)
    {
        if (item == null)
            return;

        string weaponName = item.weapon != null
            ? (!string.IsNullOrWhiteSpace(item.weapon.displayName) ? item.weapon.displayName : item.weapon.name)
            : "(sem arma)";
        string attackerName = item.attackerUnit != null ? item.attackerUnit.name : "(null)";
        string targetName = item.targetUnit != null ? item.targetUnit.name : "(null)";

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"{index + 1}. invalido", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Atacante", attackerName);
        EditorGUILayout.LabelField("Alvo", targetName);
        EditorGUILayout.LabelField("Arma", weaponName);
        EditorGUILayout.LabelField("Distancia", item.distance.ToString());
        EditorGUILayout.LabelField("Motivo", string.IsNullOrWhiteSpace(item.reason) ? "-" : item.reason);
        EditorGUILayout.LabelField("Hex bloqueador", $"{item.blockedCell.x},{item.blockedCell.y}");
        EditorGUILayout.LabelField("Linha (hexes intermediarios)", FormatCells(item.lineOfFireIntermediateCells));
        if (GUILayout.Button("Desenhar Linha no Scene View"))
            SelectLineForDrawing(item.attackerUnit, item.targetUnit, item.lineOfFireIntermediateCells, Color.red, $"INV: {attackerName} -> {targetName}");
        EditorGUILayout.EndVertical();
    }

    private static string FormatCells(List<Vector3Int> cells)
    {
        if (cells == null || cells.Count == 0)
            return "-";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int c = cells[i];
            if (i > 0)
                sb.Append(" -> ");
            sb.Append(c.x).Append(",").Append(c.y);
        }

        return sb.ToString();
    }

    private void SelectLineForDrawing(UnitManager attacker, UnitManager target, List<Vector3Int> intermediateCells, Color color, string label)
    {
        if (attacker == null || target == null)
            return;

        selectedLineStartCell = attacker.CurrentCellPosition;
        selectedLineStartCell.z = 0;
        selectedLineEndCell = target.CurrentCellPosition;
        selectedLineEndCell.z = 0;
        selectedLineIntermediateCells = intermediateCells != null ? new List<Vector3Int>(intermediateCells) : new List<Vector3Int>();
        selectedLineColor = color;
        selectedLineLabel = label;
        hasSelectedLine = true;
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!hasSelectedLine)
            return;

        Tilemap map = ResolveDrawTilemap();
        if (map == null)
            return;

        Vector3 start = map.GetCellCenterWorld(selectedLineStartCell);
        Vector3 end = map.GetCellCenterWorld(selectedLineEndCell);

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = selectedLineColor;
        Handles.DrawAAPolyLine(4f, start, end);
        Handles.SphereHandleCap(0, start, Quaternion.identity, 0.15f, EventType.Repaint);
        Handles.SphereHandleCap(0, end, Quaternion.identity, 0.15f, EventType.Repaint);

        for (int i = 0; i < selectedLineIntermediateCells.Count; i++)
        {
            Vector3 p = map.GetCellCenterWorld(selectedLineIntermediateCells[i]);
            Handles.DrawWireDisc(p, Vector3.forward, 0.2f);
        }

        Vector3 mid = Vector3.Lerp(start, end, 0.5f);
        Handles.Label(mid + new Vector3(0.1f, 0.1f, 0f), selectedLineLabel);
    }

    private Tilemap ResolveDrawTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;
        if (selectedUnit != null && selectedUnit.BoardTilemap != null)
            return selectedUnit.BoardTilemap;
        return FindPreferredTilemap();
    }

    private void TogglePaintRangeMap()
    {
        if (isRangeMapPainted)
        {
            ClearPaintedRangeMap();
            isRangeMapPainted = false;
            return;
        }

        if (selectedUnit == null)
        {
            statusMessage = "Selecione uma unidade para pintar o RangeMap.";
            return;
        }

        if (!TryResolveRangeMapContext(out Tilemap rangeMapTilemap, out TileBase rangeOverlayTile))
        {
            statusMessage = "RangeMap/RangeTile nao encontrados no TurnStateManager.";
            return;
        }

        Tilemap board = ResolveBoardTilemapForSimulation();
        if (board == null)
        {
            statusMessage = "Tilemap base da unidade nao encontrado.";
            return;
        }

        Dictionary<Vector3Int, List<Vector3Int>> validPaths = UnitMovementPathRules.CalcularCaminhosValidos(
            board,
            selectedUnit,
            Mathf.Max(0, selectedUnit.GetMovementRange()),
            terrainDatabase);

        ClearPaintedRangeMap();
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in validPaths)
        {
            Vector3Int cell = pair.Key;
            if (board.GetTile(cell) == null)
                continue;

            rangeMapTilemap.SetTile(cell, rangeOverlayTile);
            rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
            rangeMapTilemap.SetColor(cell, rangeOverlayColor);
            paintedRangeCells.Add(cell);
        }

        isRangeMapPainted = paintedRangeCells.Count > 0;
        statusMessage = isRangeMapPainted
            ? $"RangeMap pintado com {paintedRangeCells.Count} celulas."
            : "RangeMap sem celulas para pintar.";
    }

    private void TogglePaintLineOfFireMap()
    {
        if (isLineOfFireMapPainted)
        {
            ClearPaintedLineOfFireMap();
            isLineOfFireMapPainted = false;
            return;
        }

        if (selectedUnit == null)
        {
            statusMessage = "Selecione uma unidade para pintar o LinhaDeTiroMap.";
            return;
        }

        if (!TryResolveLineOfFireMapContext(out Tilemap lineOfFireMapTilemap, out TileBase lineOfFireOverlayTile))
        {
            statusMessage = "LinhaDeTiroMap/LinhaDeTiroTile nao encontrados no TurnStateManager.";
            return;
        }

        Tilemap board = ResolveBoardTilemapForSimulation();
        if (board == null)
        {
            statusMessage = "Tilemap base da unidade nao encontrado.";
            return;
        }

        HashSet<Vector3Int> validCells = new HashSet<Vector3Int>();
        PodeMirarSensor.CollectValidFireCells(
            selectedUnit,
            board,
            terrainDatabase,
            movementMode,
            validCells);

        ClearPaintedLineOfFireMap();
        foreach (Vector3Int cell in validCells)
        {
            lineOfFireMapTilemap.SetTile(cell, lineOfFireOverlayTile);
            lineOfFireMapTilemap.SetTileFlags(cell, TileFlags.None);
            lineOfFireMapTilemap.SetColor(cell, lineOfFireOverlayColor);
            paintedLineOfFireCells.Add(cell);
        }

        isLineOfFireMapPainted = paintedLineOfFireCells.Count > 0;
        statusMessage = isLineOfFireMapPainted
            ? $"LinhaDeTiroMap pintado com {paintedLineOfFireCells.Count} celulas."
            : "LinhaDeTiroMap sem celulas para pintar.";
    }

    private void ClearPaintedRangeMap()
    {
        if (!TryResolveRangeMapContext(out Tilemap rangeMapTilemap, out _))
            return;

        for (int i = 0; i < paintedRangeCells.Count; i++)
        {
            Vector3Int cell = paintedRangeCells[i];
            rangeMapTilemap.SetTile(cell, null);
            rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
            rangeMapTilemap.SetColor(cell, Color.white);
        }

        paintedRangeCells.Clear();
    }

    private void ClearPaintedLineOfFireMap()
    {
        if (!TryResolveLineOfFireMapContext(out Tilemap lineOfFireMapTilemap, out _))
            return;

        for (int i = 0; i < paintedLineOfFireCells.Count; i++)
        {
            Vector3Int cell = paintedLineOfFireCells[i];
            lineOfFireMapTilemap.SetTile(cell, null);
            lineOfFireMapTilemap.SetTileFlags(cell, TileFlags.None);
            lineOfFireMapTilemap.SetColor(cell, Color.white);
        }

        paintedLineOfFireCells.Clear();
    }

    private Tilemap ResolveBoardTilemapForSimulation()
    {
        if (overrideTilemap != null)
            return overrideTilemap;
        if (selectedUnit != null && selectedUnit.BoardTilemap != null)
            return selectedUnit.BoardTilemap;
        return FindPreferredTilemap();
    }

    private void ResolveContextFromTurnStateManager()
    {
        if (turnStateManager == null)
            return;

        SerializedObject so = new SerializedObject(turnStateManager);
        if (terrainDatabase == null)
            terrainDatabase = so.FindProperty("terrainDatabase")?.objectReferenceValue as TerrainDatabase;
    }

    private bool TryResolveRangeMapContext(out Tilemap map, out TileBase tile)
    {
        map = null;
        tile = null;

        if (turnStateManager != null)
        {
            SerializedObject so = new SerializedObject(turnStateManager);
            map = so.FindProperty("rangeMapTilemap")?.objectReferenceValue as Tilemap;
            tile = so.FindProperty("rangeOverlayTile")?.objectReferenceValue as TileBase;
        }

        if (map == null)
            map = FindMapByName("rangemap");

        return map != null && tile != null;
    }

    private bool TryResolveLineOfFireMapContext(out Tilemap map, out TileBase tile)
    {
        map = null;
        tile = null;

        if (turnStateManager != null)
        {
            SerializedObject so = new SerializedObject(turnStateManager);
            map = so.FindProperty("lineOfFireMapTilemap")?.objectReferenceValue as Tilemap;
            tile = so.FindProperty("lineOfFireOverlayTile")?.objectReferenceValue as TileBase;
        }

        if (map == null)
            map = FindMapByName("linhadetiromap", "lineoffiremap");

        return map != null && tile != null;
    }
}
