using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeMudarAltitudeWindow : EditorWindow
{
    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] private Tilemap overrideTilemap;

    private PodeMudarAltitudeReport latestReport;
    private string statusMessage = "Ready.";
    private Vector2 windowScroll;

    [MenuItem("Tools/Operações Aéreas/Pode Mudar de Altitude")]
    public static void OpenWindow()
    {
        GetWindow<PodeMudarAltitudeWindow>("Pode Mudar de Altitude");
    }

    private void OnEnable()
    {
        AutoDetectContext();
    }

    private void OnGUI()
    {
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
        EditorGUILayout.LabelField("Sensor Pode Mudar de Altitude", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura de nivelamento AirLow <-> AirHigh. Terreno, estruturas, construcoes e skills do hex nao participam.",
            MessageType.Info);

        selectedUnit = (UnitManager)EditorGUILayout.ObjectField("Unidade", selectedUnit, typeof(UnitManager), true);
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);

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
        EditorGUILayout.EndScrollView();
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

    private PodeMudarAltitudeReport EvaluateLayerChange()
    {
        var report = new PodeMudarAltitudeReport
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

        HeightLevel currentHeight = selectedUnit.GetHeightLevel();
        if (selectedUnit.GetDomain() != Domain.Air ||
            (currentHeight != HeightLevel.AirLow && currentHeight != HeightLevel.AirHigh))
        {
            report.explicacao = "Selecione uma aeronave em AirLow ou AirHigh.";
            return report;
        }

        HeightLevel targetHeight = currentHeight == HeightLevel.AirLow
            ? HeightLevel.AirHigh
            : HeightLevel.AirLow;
        return PodeMudarAltitudeSensor.Evaluate(selectedUnit, map, targetHeight);
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

}
