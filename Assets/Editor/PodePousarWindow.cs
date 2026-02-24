using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class PodePousarWindow : EditorWindow
{
    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private SensorMovementMode movementMode = SensorMovementMode.MoveuParado;
    [SerializeField] private bool useManualRemainingMovement = false;
    [SerializeField] private int manualRemainingMovement = 0;

    private PodePousarReport latestReport;
    private string statusMessage = "Ready.";

    [MenuItem("Tools/Pode Mudar de Altitude")]
    public static void OpenWindow()
    {
        GetWindow<PodePousarWindow>("Pode Mudar de Altitude");
    }

    private void OnEnable()
    {
        AutoDetectContext();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sensor Pode Mudar de Altitude", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Sensor generico de mudanca de altitude/camada. Disponivel para qualquer unidade com mais de uma camada operacional.",
            MessageType.Info);

        selectedUnit = (UnitManager)EditorGUILayout.ObjectField("Unidade", selectedUnit, typeof(UnitManager), true);
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        movementMode = (SensorMovementMode)EditorGUILayout.EnumPopup("Modo", movementMode);
        useManualRemainingMovement = EditorGUILayout.ToggleLeft("Usar movimento restante manual", useManualRemainingMovement);
        using (new EditorGUI.DisabledScope(!useManualRemainingMovement))
            manualRemainingMovement = EditorGUILayout.IntField("Movimento Restante", Mathf.Max(0, manualRemainingMovement));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Simular"))
            RunSimulation();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Mudanca de Altitude/Camada", EditorStyles.boldLabel);
        if (latestReport == null)
        {
            EditorGUILayout.HelpBox("Sem simulacao.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField("Status", latestReport.status ? "valido" : "invalido");
            EditorGUILayout.LabelField("Explicacao", string.IsNullOrWhiteSpace(latestReport.explicacao) ? "-" : latestReport.explicacao);
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(true))
        {
            GUILayout.Button("Confirmar (in-game: tecla L)");
        }
    }

    private void RunSimulation()
    {
        latestReport = EvaluateLayerChange();

        statusMessage = latestReport != null
            ? $"Simulacao concluida. Mudanca de altitude: {(latestReport.status ? "valida" : "invalida")}."
            : "Falha ao executar simulacao.";
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;
        if (selectedUnit != null && selectedUnit.BoardTilemap != null)
            return selectedUnit.BoardTilemap;
        return FindPreferredTilemap();
    }

    private void AutoDetectContext()
    {
        if (selectedUnit == null)
            TryUseCurrentSelection();
        if (overrideTilemap == null)
            overrideTilemap = FindPreferredTilemap();
        if (terrainDatabase == null)
            terrainDatabase = FindFirstTerrainDatabaseAsset();
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

    private PodePousarReport EvaluateLayerChange()
    {
        var report = new PodePousarReport
        {
            status = false,
            explicacao = "Contexto nao avaliado."
        };

        if (selectedUnit == null)
        {
            report.explicacao = "Selecione uma unidade.";
            return report;
        }

        Tilemap map = ResolveTilemap();
        if (map == null)
        {
            report.explicacao = "Tilemap base nao encontrado.";
            return report;
        }

        IReadOnlyList<UnitLayerMode> modes = selectedUnit.GetAllLayerModes();
        if (modes == null || modes.Count <= 1)
        {
            report.explicacao = "Unidade com apenas 1 camada operacional.";
            return report;
        }

        Domain currentDomain = selectedUnit.GetDomain();
        HeightLevel currentHeight = selectedUnit.GetHeightLevel();
        Vector3Int cell = selectedUnit.CurrentCellPosition;
        cell.z = 0;
        int validTransitions = 0;
        string firstReason = string.Empty;

        for (int i = 0; i < modes.Count; i++)
        {
            UnitLayerMode mode = modes[i];
            if (mode.domain == currentDomain && mode.heightLevel == currentHeight)
                continue;

            if (CanUseLayerModeAtCurrentCell(selectedUnit, map, terrainDatabase, cell, mode.domain, mode.heightLevel, out string reason))
            {
                validTransitions++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(firstReason))
                firstReason = reason;
        }

        report.status = validTransitions > 0;
        report.explicacao = report.status
            ? $"Unidade apta a mudar de altitude/camada (transicoes validas: {validTransitions})."
            : $"Unidade sem transicao valida no hex atual. {firstReason}";

        if (useManualRemainingMovement)
        {
            int safeRemaining = Mathf.Max(0, manualRemainingMovement);
            report.explicacao += $" Movimento restante manual={safeRemaining} (informativo neste sensor).";
        }

        return report;
    }

    private static bool CanUseLayerModeAtCurrentCell(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDb,
        Vector3Int cell,
        Domain targetDomain,
        HeightLevel targetHeight,
        out string reason)
    {
        reason = string.Empty;
        if (unit == null || boardMap == null)
        {
            reason = "Contexto de mapa/unidade invalido.";
            return false;
        }

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
        if (construction != null)
        {
            if (!construction.SupportsLayerMode(targetDomain, targetHeight))
            {
                reason = $"Construcao no hex nao suporta {targetDomain}/{targetHeight}.";
                return false;
            }

            if (!UnitPassesSkillRequirement(unit, construction.GetRequiredSkillsToEnter()))
            {
                reason = "Unidade nao possui skill exigida pela construcao.";
                return false;
            }

            return true;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardMap, cell);
        if (structure != null)
        {
            if (!StructureSupportsLayerMode(structure, targetDomain, targetHeight))
            {
                reason = $"Estrutura no hex nao suporta {targetDomain}/{targetHeight}.";
                return false;
            }

            if (!UnitPassesSkillRequirement(unit, structure.requiredSkillsToEnter))
            {
                reason = "Unidade nao possui skill exigida pela estrutura.";
                return false;
            }

            if (!TryResolveTerrainAtCell(boardMap, terrainDb, cell, out TerrainTypeData terrainWithStructure) || terrainWithStructure == null)
            {
                reason = "Terreno do hex nao encontrado para validar com estrutura.";
                return false;
            }

            if (!TerrainSupportsLayerMode(terrainWithStructure, targetDomain, targetHeight))
            {
                reason = $"Terreno no hex (com estrutura) nao suporta {targetDomain}/{targetHeight}.";
                return false;
            }

            if (!UnitPassesSkillRequirement(unit, terrainWithStructure.requiredSkillsToEnter))
            {
                reason = "Unidade nao possui skill exigida pelo terreno.";
                return false;
            }

            return true;
        }

        if (!TryResolveTerrainAtCell(boardMap, terrainDb, cell, out TerrainTypeData terrain) || terrain == null)
        {
            reason = "Terreno do hex nao encontrado para validar camada.";
            return false;
        }

        if (!TerrainSupportsLayerMode(terrain, targetDomain, targetHeight))
        {
            reason = $"Terreno no hex nao suporta {targetDomain}/{targetHeight}.";
            return false;
        }

        if (!UnitPassesSkillRequirement(unit, terrain.requiredSkillsToEnter))
        {
            reason = "Unidade nao possui skill exigida pelo terreno.";
            return false;
        }

        return true;
    }

    private static bool UnitPassesSkillRequirement(UnitManager unit, IReadOnlyList<SkillData> requiredSkills)
    {
        if (requiredSkills == null || requiredSkills.Count == 0)
            return true;
        if (unit == null)
            return false;

        for (int i = 0; i < requiredSkills.Count; i++)
        {
            SkillData skill = requiredSkills[i];
            if (skill == null)
                continue;

            if (unit.HasSkill(skill))
                return true;
        }

        return false;
    }

    private static bool TerrainSupportsLayerMode(TerrainTypeData terrain, Domain domain, HeightLevel heightLevel)
    {
        if (terrain == null)
            return false;

        if (terrain.domain == domain && terrain.heightLevel == heightLevel)
            return true;

        if (domain == Domain.Air && terrain.alwaysAllowAirDomain)
            return true;

        if (terrain.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < terrain.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = terrain.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool StructureSupportsLayerMode(StructureData structure, Domain domain, HeightLevel heightLevel)
    {
        if (structure == null)
            return false;

        if (structure.domain == domain && structure.heightLevel == heightLevel)
            return true;

        if (domain == Domain.Air && structure.alwaysAllowAirDomain)
            return true;

        if (structure.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < structure.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = structure.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool TryResolveTerrainAtCell(
        Tilemap terrainTilemap,
        TerrainDatabase terrainDb,
        Vector3Int cell,
        out TerrainTypeData terrain)
    {
        terrain = null;
        if (terrainTilemap == null || terrainDb == null)
            return false;

        cell.z = 0;
        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null && terrainDb.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
        {
            terrain = byMainTile;
            return true;
        }

        GridLayout grid = terrainTilemap.layoutGrid;
        if (grid == null)
            return false;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            TileBase other = map.GetTile(cell);
            if (other == null)
                continue;

            if (terrainDb.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) && byGridTile != null)
            {
                terrain = byGridTile;
                return true;
            }
        }

        return false;
    }

    private static Tilemap FindPreferredTilemap()
    {
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (maps == null || maps.Length == 0)
            return null;

        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map != null && string.Equals(map.name, "Tilemap", System.StringComparison.OrdinalIgnoreCase))
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
}
