using TMPro;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PanelHelperController : MonoBehaviour
{
    private static PanelHelperController instance;

    private struct CoordinateOverlayLabel
    {
        public Vector3Int cell;
        public string text;
        public Color color;
    }

    [Header("References")]
    [SerializeField] private CursorController cursorController;
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private AnimationManager animationManager;
    [SerializeField] private SaveGameManager saveGameManager;
    [SerializeField] private HelperDatabase helperDatabase;
    [SerializeField] private GameObject panelHelper;
    [SerializeField] private TMP_Text helperTitle;
    [SerializeField] private TMP_Text helperTxt;

    [Header("Dock")]
    [SerializeField] [Range(0f, 300f)] private float dockEnterProximityPixels = 80f;
    [SerializeField] [Range(0f, 500f)] private float dockExitProximityPixels = 140f;
    [SerializeField] private Vector2 dockedAnchoredPosition = new Vector2(18f, 0f);
    [Header("Layout")]
    [SerializeField] private bool autoExpandHeight = true;
    [SerializeField] [Range(0f, 2000f)] private float minPanelHeight = 0f;
    [SerializeField] [Range(100f, 4000f)] private float maxPanelHeight = 1200f;
    [SerializeField] [Range(0f, 300f)] private float contentVerticalPadding = 24f;

    private string lastTitle = string.Empty;
    private string lastBody = string.Empty;
    private bool lastPanelVisible;
    private CanvasGroup selfPanelCanvasGroup;
    private RectTransform helperRect;
    private RectTransform helperTitleRect;
    private RectTransform helperTxtRect;
    private RectMask2D helperMask;
    private Vector2 originalAnchorMin;
    private Vector2 originalAnchorMax;
    private Vector2 originalPivot;
    private Vector2 originalAnchoredPosition;
    private Vector2 originalHelperTitleAnchoredPosition;
    private Vector2 originalHelperTxtAnchoredPosition;
    private float originalBodySpacingFromTitle = 0f;
    private float originalHelperTitleHeight = -1f;
    private float originalHelperTxtHeight = -1f;
    private bool layoutCached;
    private bool isDockedCenterLeft;
    private bool hasLastUndockedScreenRect;
    private Rect lastUndockedScreenRect;
    private bool cursorNearUndockedDockRegion;
    private Color lastHelperTxtColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
    private float cachedBasePanelHeight = -1f;
    private float helperScrollOffset;
    private float helperScrollMaxOffset;
    private bool helperScrollActive;
    private GameObject cancelControlRoot;
    private Button cancelActionButton;
    private Image cancelActionImage;
    private TMP_Text cancelActionLabel;
    private const float CancelControlHeight = 52f;
    private GameObject executeCommandServiceControlRoot;
    private Button executeCommandServiceButton;
    private Image executeCommandServiceImage;
    private TMP_Text executeCommandServiceLabel;
    private const float ExecuteCommandServiceControlHeight = 52f;
    // Cor de fallback dos botoes gerados via script (usada so na criacao; o tint por time
    // sobrescreve todo frame). Os botoes seguem a cor do time ativo (virou tudo slot de jogador).
    private static readonly Color FooterButtonIdleColor = new Color(0.04f, 0.12f, 0.06f, 0.92f);
    private static readonly Color FooterLabelIdleColor = new Color(0.65f, 1f, 0.65f, 1f);
    // Cor do time ativo neste frame, resolvida no refresh e aplicada a todos os botoes de script.
    private Color currentTeamColor = Color.white;

    // Fundo do botao = tint escuro da cor do time; foco = tint mais claro. Rotulo = cor do time;
    // foco = clareada rumo ao branco (destaque legivel em qualquer cor de time).
    private static Color TeamButtonBackground(Color team, bool focused)
    {
        float k = focused ? 0.42f : 0.16f;
        return new Color(team.r * k, team.g * k, team.b * k, focused ? 0.98f : 0.92f);
    }

    private static Color TeamButtonLabel(Color team, bool focused)
    {
        return focused ? Color.Lerp(team, Color.white, 0.55f) : team;
    }

    private void TintScriptButtonToTeamIdle(Button button)
    {
        TintScriptButtonToTeam(button, focused: false);
    }

    private void TintScriptButtonToTeam(Button button, bool focused)
    {
        if (button == null)
            return;
        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = TeamButtonBackground(currentTeamColor, focused);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = TeamButtonLabel(currentTeamColor, focused);
    }

    private GameObject sensorActionsRoot;
    private readonly List<Button> sensorActionButtons = new List<Button>();
    private readonly List<char> sensorActionButtonCodes = new List<char>();
    private string sensorActionsSignature = string.Empty;
    private const float SensorActionButtonHeight = 42f;
    private GameObject aimTargetsRoot;
    private readonly List<Button> aimTargetButtons = new List<Button>();
    private string aimTargetsSignature = string.Empty;
    // Alvo mostra 2 linhas (nome+HP / terreno), entao precisa de mais altura que os demais botoes.
    private const float AimTargetButtonHeight = 58f;
    // Seccao de detalhes do passo CONFIRMAR ATAQUE: HP + LOCAL (icone do hex + nome do terreno).
    private GameObject aimConfirmDetailsRoot;
    private Image aimConfirmTargetIcon;
    private TMP_Text aimConfirmTargetText;
    private TMP_Text aimConfirmHpText;
    private Image aimConfirmLocalIcon;
    private TMP_Text aimConfirmLocalText;
    private const float AimConfirmDetailsHeight = 172f;
    private GameObject shoppingActionsRoot;
    private readonly List<Button> shoppingActionButtons = new List<Button>();
    private string shoppingActionsSignature = string.Empty;
    private const float ShoppingActionButtonHeight = 42f;
    private GameObject persistenceActionsRoot;
    private readonly List<Button> persistenceActionButtons = new List<Button>();
    private readonly List<Image> persistenceActionImages = new List<Image>();
    private readonly List<TMP_Text> persistenceActionLabels = new List<TMP_Text>();
    private string persistenceActionsSignature = string.Empty;
    private const float PersistenceActionButtonHeight = 42f;
    [SerializeField] [Range(1f, 80f)] private float helperScrollStep = 24f;

    [Header("Coordinate Overlay")]
    [SerializeField] private KeyCode toggleCoordinateOverlayKey = KeyCode.F3;
    private bool showCoordinateOverlay
    {
        get => cursorController != null && cursorController.ShowCoordinates;
        set { if (cursorController != null) cursorController.ShowCoordinates = value; }
    }
    [SerializeField] [Range(0f, 4f)] private float coordinateLabelWorldYOffset = 0.42f;
    [SerializeField] private Color cursorCoordinateColor = new Color(1f, 0.92f, 0.40f, 1f);
    [SerializeField] private Color selectedCoordinateColor = new Color(0.50f, 1f, 0.68f, 1f);
    [SerializeField] private Color eventCoordinateColor = new Color(1f, 0.60f, 0.60f, 1f);
    [SerializeField] private Color eventHighlightColor = new Color(1f, 0.30f, 0.20f, 1f);
    [SerializeField] [Range(0.1f, 3f)] private float eventHighlightSeconds = 1.4f;

    private readonly List<CoordinateOverlayLabel> coordinateOverlayLabels = new List<CoordinateOverlayLabel>();
    private readonly List<Vector3Int> upkeepEventCells = new List<Vector3Int>();
    private readonly Dictionary<Vector3Int, float> highlightedEventCells = new Dictionary<Vector3Int, float>();
    private GUIStyle coordinateOverlayLabelStyle;
    private GUIStyle coordinateOverlayHighlightStyle;
    private int lastUpkeepSignature;
    private bool hasExternalOverrideText;
    private string externalOverrideTitle = string.Empty;
    private string externalOverrideBody = string.Empty;
    private float externalOverrideUntilUnscaledTime = -1f;

    private void Awake()
    {
        instance = this;
        TryAutoAssignReferences();
        CacheOriginalLayoutIfNeeded();
        HideAll(force: true);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        TryAutoAssignReferences();
        Refresh(force: false);
        HandleCoordinateOverlayHotkeys();
        RefreshCoordinateOverlayState();
        HandleHelperScrollInput();
    }

    private void OnGUI()
    {
        if (!showCoordinateOverlay)
            return;

        if (!Application.isPlaying || cursorController == null || cursorController.BoardTilemap == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        EnsureCoordinateOverlayStyles();

        for (int i = 0; i < coordinateOverlayLabels.Count; i++)
        {
            CoordinateOverlayLabel label = coordinateOverlayLabels[i];
            DrawCoordinateLabel(cam, cursorController.BoardTilemap, label.cell, label.text, label.color, isHighlight: false);
        }

        if (highlightedEventCells.Count <= 0)
            return;

        List<Vector3Int> expired = null;
        foreach (KeyValuePair<Vector3Int, float> pair in highlightedEventCells)
        {
            if (Time.unscaledTime > pair.Value)
            {
                expired ??= new List<Vector3Int>();
                expired.Add(pair.Key);
                continue;
            }

            DrawCoordinateLabel(cam, cursorController.BoardTilemap, pair.Key, "!", eventHighlightColor, isHighlight: true);
        }

        if (expired == null)
            return;

        for (int i = 0; i < expired.Count; i++)
            highlightedEventCells.Remove(expired[i]);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoAssignReferences();
        CacheOriginalLayoutIfNeeded();
        HideAll(force: true);
    }
#endif

    private void TryAutoAssignReferences()
    {
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();
        if (animationManager == null)
            animationManager = FindAnyObjectByType<AnimationManager>();
        if (saveGameManager == null)
            saveGameManager = FindAnyObjectByType<SaveGameManager>();

        if (panelHelper == null)
            panelHelper = FindNamedObject("panel_helper") ?? FindNamedObject("Panel_helper") ?? FindNamedObject("Panel_Helper");
        if (panelHelper == null)
            panelHelper = gameObject;

        if (helperTitle == null)
            helperTitle = FindNamedTmpText("helper_title");
        if (helperTitleRect == null && helperTitle != null)
            helperTitleRect = helperTitle.rectTransform;
        if (helperTxt == null)
            helperTxt = FindNamedTmpText("helper_txt");
        if (helperTxtRect == null && helperTxt != null)
            helperTxtRect = helperTxt.rectTransform;

        if (helperRect == null && panelHelper != null)
            helperRect = panelHelper.GetComponent<RectTransform>();

        EnsureCancelControl();
        EnsureExecuteCommandServiceControl();
        EnsureShoppingActionsRoot();
        EnsurePersistenceActionsRoot();

#if UNITY_EDITOR
        if (helperDatabase == null)
            helperDatabase = FindFirstAssetEditor<HelperDatabase>();
#endif
    }

    private void Refresh(bool force)
    {
        if (hasExternalOverrideText &&
            externalOverrideUntilUnscaledTime > 0f &&
            Time.unscaledTime >= externalOverrideUntilUnscaledTime)
        {
            hasExternalOverrideText = false;
            externalOverrideTitle = string.Empty;
            externalOverrideBody = string.Empty;
            externalOverrideUntilUnscaledTime = -1f;
        }

        if (hasExternalOverrideText)
        {
            SetVisible(panelVisible: true, title: externalOverrideTitle, body: externalOverrideBody, data: null, force: force);
            RefreshDockByCursorProximity();
            return;
        }

        if (turnStateManager == null || !turnStateManager.TryBuildHelperPanelData(out TurnStateManager.HelperPanelData data))
        {
            HideAll(force);
            return;
        }

        BuildHelperText(data, out string title, out string body);

        SetVisible(panelVisible: true, title: title, body: body, data: data, force: force);
        RefreshDockByCursorProximity();
    }

    private void BuildHelperText(TurnStateManager.HelperPanelData data, out string title, out string body)
    {
        title = string.Empty;
        body = string.Empty;
        if (data == null)
            return;

        switch (data.Kind)
        {
            case TurnStateManager.HelperPanelKind.Shopping:
                title = ResolveMessage(
                    "helper.title.shopping",
                    "SHOPPING",
                    new Dictionary<string, string>
                    {
                        ["Construction"] = string.IsNullOrWhiteSpace(data.ShoppingConstructionName)
                            ? "Construction"
                            : data.ShoppingConstructionName
                    });
                body = BuildShoppingBody(data);
                return;

            case TurnStateManager.HelperPanelKind.RemovingUnit:
                title = "REMOVER UNIDADE";
                body = $"A unidade {data.RemovingUnitName} vai ser removida.";
                return;

            case TurnStateManager.HelperPanelKind.AimTargets:
                title = "ESCOLHER ALVO";
                body = string.Empty;
                return;

            case TurnStateManager.HelperPanelKind.AimConfirm:
                title = "CONFIRMAR ATAQUE";
                body = string.Empty;
                return;

            case TurnStateManager.HelperPanelKind.Sensors:
                title = data.ThreatLayerSelectionActive
                    ? ResolveMessage("helper.title.hotzone", "HOT ZONE")
                    : ResolveMessage("helper.title.sensors", "SENSORS");
                body = BuildSensorsBody(data);
                return;

            case TurnStateManager.HelperPanelKind.Disembark:
                title = ResolveMessage("helper.title.disembark", "DISEMBARK");
                body = BuildDisembarkBody(data);
                return;

            case TurnStateManager.HelperPanelKind.Merge:
                title = ResolveMessage("helper.title.merge", "MERGE");
                body = BuildMergeBody(data);
                return;

            case TurnStateManager.HelperPanelKind.Embark:
                title = ResolveMessage("helper.title.embark", "EMBARK");
                body = BuildEmbarkBody(data);
                return;

            case TurnStateManager.HelperPanelKind.Supply:
                title = ResolveMessage("helper.title.supply_preview", "SUPPLY");
                body = BuildSupplyBody(data);
                return;

            case TurnStateManager.HelperPanelKind.Transfer:
                title = ResolveMessage("helper.title.transfer_preview", "TRANSFER");
                body = BuildTransferBody(data);
                return;

            case TurnStateManager.HelperPanelKind.CommandService:
                title = ResolveMessage("helper.title.command_service", "COMMAND SERVICE");
                body = BuildCommandServiceBody(data);
                return;

            case TurnStateManager.HelperPanelKind.UnitStats:
                title = data.UnitStatsName ?? ResolveMessage("helper.title.unit_stats", "UNIT");
                body = BuildUnitStatsBody(data);
                return;

            case TurnStateManager.HelperPanelKind.ConstructionStats:
                title = data.ConstructionStatsName ?? ResolveMessage("helper.title.construction_stats", "CONSTRUCTION");
                body = BuildConstructionStatsBody(data);
                return;

            case TurnStateManager.HelperPanelKind.TurnStartAutonomy:
                title = ResolveMessage("helper.title.turn_start_autonomy", "TURN START");
                body = BuildTurnStartAutonomyBody(data);
                return;

            default:
                title = string.Empty;
                body = string.Empty;
                return;
        }
    }

    private string BuildUnitStatsBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null || data.UnitStatsLines == null || data.UnitStatsLines.Count <= 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < data.UnitStatsLines.Count; i++)
        {
            string line = ResolveUnitStatsLine(data.UnitStatsLines[i] ?? string.Empty);
            if (i > 0)
                sb.AppendLine();
            sb.Append(line);
        }

        return sb.ToString();
    }

    private string ResolveUnitStatsLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return string.Empty;

        if (line.StartsWith("HP: "))
        {
            string value = line.Substring(4);
            string[] parts = value.Split('/');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int current) &&
                int.TryParse(parts[1], out int max))
            {
                return ResolveMessage(
                    "helper.unit_stats.line.hp",
                    "HP: <current>/<max>",
                    new Dictionary<string, string>
                    {
                        { "current", Mathf.Max(0, current).ToString() },
                        { "max", Mathf.Max(0, max).ToString() }
                    });
            }
        }

        if (line.StartsWith("MOV: "))
        {
            string value = line.Substring(5);
            if (int.TryParse(value, out int movement))
            {
                return ResolveMessage(
                    "helper.unit_stats.line.mov",
                    "MOV: <mov>",
                    new Dictionary<string, string>
                    {
                        { "mov", Mathf.Max(0, movement).ToString() }
                    });
            }
        }

        if (line.StartsWith("AUT: "))
        {
            string value = line.Substring(5);
            string[] parts = value.Split('/');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out int current) &&
                int.TryParse(parts[1], out int max))
            {
                return ResolveMessage(
                    "helper.unit_stats.line.aut",
                    "AUT: <current>/<max>",
                    new Dictionary<string, string>
                    {
                        { "current", Mathf.Max(0, current).ToString() },
                        { "max", Mathf.Max(0, max).ToString() }
                    });
            }
        }

        if (line.StartsWith("DESC: "))
        {
            string description = line.Substring(6).Trim();
            return ResolveMessage(
                "helper.unit_stats.line.description",
                "<description>",
                new Dictionary<string, string>
                {
                    { "description", description }
                });
        }

        if (line == "SECTION:Weapons")
            return ResolveMessage("helper.unit_stats.section.weapons", "Armas");

        if (line == "SECTION:Transporting" || line == "Transportando")
            return ResolveMessage("helper.unit_stats.section.transporting", "Transportando");

        if (line == "SECTION:Services")
            return ResolveMessage("helper.unit_stats.section.services", "Serviços Prestados");

        if (line == "SECTION:Supplies" || line == "Suprimentos Carregados" || line == "Reserva")
            return ResolveMessage("helper.unit_stats.section.supplies", "Suprimentos Carregados");

        string supplies = string.Empty;
        const string transportedSuppliesMarker = "||SUPPLIES||";
        int suppliesMarkerIndex = line.IndexOf(transportedSuppliesMarker, System.StringComparison.Ordinal);
        if (suppliesMarkerIndex >= 0)
        {
            supplies = line.Substring(suppliesMarkerIndex + transportedSuppliesMarker.Length);
            line = line.Substring(0, suppliesMarkerIndex);
        }

        int openStatsIndex = line.LastIndexOf(" (", System.StringComparison.Ordinal);
        if (openStatsIndex > 0 && line.EndsWith(")", System.StringComparison.Ordinal))
        {
            string head = line.Substring(0, openStatsIndex);
            string stats = line.Substring(openStatsIndex + 2, line.Length - openStatsIndex - 3);
            if (!string.IsNullOrWhiteSpace(stats))
            {
                int unitStart = 0;
                while (unitStart < head.Length && head[unitStart] == ' ')
                    unitStart++;

                string indent = unitStart > 0 ? head.Substring(0, unitStart) : string.Empty;
                string unitName = unitStart < head.Length ? head.Substring(unitStart) : string.Empty;
                if (!string.IsNullOrWhiteSpace(unitName))
                {
                    string resolved = ResolveMessage(
                        "helper.unit_stats.line.transported",
                        "<indent><unit>\n<indent>   <stats>\n<indent>   <supplies>",
                        new Dictionary<string, string>
                        {
                            { "indent", indent },
                            { "unit", unitName },
                            { "stats", stats },
                            { "supplies", supplies }
                        });

                    return RemoveWhitespaceOnlyLines(resolved);
                }
            }
        }

        return line;
    }

    private static string RemoveWhitespaceOnlyLines(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string[] lines = value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(lines[i]);
        }

        return sb.ToString();
    }

    private string BuildConstructionStatsBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null || data.ConstructionStatsLines == null || data.ConstructionStatsLines.Count <= 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < data.ConstructionStatsLines.Count; i++)
        {
            string line = data.ConstructionStatsLines[i] ?? string.Empty;
            if (i > 0)
                sb.AppendLine();
            sb.Append(line);
        }

        return sb.ToString().TrimEnd();
    }

    private string BuildTurnStartAutonomyBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null || data.TurnStartAutonomyLines == null || data.TurnStartAutonomyLines.Count <= 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(ResolveMessage("helper.turn_start_autonomy.header", "Consumo de autonomia (operando):"));

        for (int i = 0; i < data.TurnStartAutonomyLines.Count; i++)
        {
            TurnStateManager.HelperTurnStartAutonomyLine line = data.TurnStartAutonomyLines[i];
            if (line == null)
                continue;

            string unitName = string.IsNullOrWhiteSpace(line.unitName) ? "Unidade" : line.unitName;
            int consumed = Mathf.Max(0, line.autonomyConsumed);
            int fuelBefore = Mathf.Max(0, line.fuelBefore);
            int fuelAfter = Mathf.Max(0, line.fuelAfter);
            sb.AppendLine(ResolveMessage(
                "helper.turn_start_autonomy.line",
                "<unit> <cell> Fuel <before> - <consumed> = <after>",
                new Dictionary<string, string>
                {
                    { "unit", unitName },
                    { "cell", FormatMapCell(line.cell) },
                    { "before", fuelBefore.ToString() },
                    { "consumed", consumed.ToString() },
                    { "after", fuelAfter.ToString() }
                }));
        }

        return sb.ToString().TrimEnd();
    }

    private string BuildShoppingBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null || data.ShoppingLines == null || data.ShoppingLines.Count == 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < data.ShoppingLines.Count; i++)
        {
            TurnStateManager.HelperShoppingLine line = data.ShoppingLines[i];
            if (line == null)
                continue;

            if (sb.Length > 0)
                sb.AppendLine();

            if (line.cost.HasValue)
            {
                string content = ResolveMessage(
                    "helper.shopping.line.with_cost",
                    "<index> - <unit> ($<valor>)",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty },
                        { "valor", line.cost.Value.ToString() }
                    });
                sb.Append(line.isFocused ? ">> " : string.Empty);
                sb.Append(content);
            }
            else
            {
                string content = ResolveMessage(
                    "helper.shopping.line.no_cost",
                    "<index> - <unit>",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty }
                    });
                sb.Append(line.isFocused ? ">> " : string.Empty);
                sb.Append(content);
            }
        }

        sb.AppendLine();
        sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
        sb.Append(ResolveMessage("helper.shopping.hint", "Setas: foco | Enter: comprar | ESC: cancelar"));
        return sb.ToString().TrimEnd();
    }

    private string BuildSensorsBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        if (data.SensorLines != null)
        {
            for (int i = 0; i < data.SensorLines.Count; i++)
            {
                TurnStateManager.HelperSensorLine line = data.SensorLines[i];
                if (line == null)
                    continue;

                string label = ResolveSensorLabel(line.sensorKey);
                string resolvedLineId = line.sensorKey == "move_only"
                    ? "helper.sensors.line.move_only"
                    : "helper.sensors.line.format";

                if (sb.Length > 0)
                    sb.AppendLine();

                sb.Append(ResolveMessage(
                    resolvedLineId,
                    "<action> - <label>",
                    new Dictionary<string, string>
                    {
                        { "action", line.actionCode.ToString() },
                        { "label", label }
                    }));
            }
        }

        if (data.ThreatLayerSelectionActive)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(ResolveMessage(
                "helper.sensors.threat_layers.current_team",
                "Time inspecionado: <team_name> (<team_id>)",
                new Dictionary<string, string>
                {
                    { "team_name", TeamUtils.GetName((TeamId)data.ThreatLayerInspectedTeamId) },
                    { "team_id", data.ThreatLayerInspectedTeamId.ToString() }
                }));
            sb.AppendLine(ResolveMessage("helper.sensors.threat_layers.select_header", "Times pra inspecionar:"));
            for (int i = 0; i < data.ThreatLayerTeamLines.Count; i++)
            {
                TurnStateManager.HelperThreatLayerTeamLine line = data.ThreatLayerTeamLines[i];
                if (line == null)
                    continue;
                if (line.isOwnTeam)
                    sb.AppendLine(ResolveMessage(
                        "helper.sensors.threat_layers.option.own",
                        "<option>: seu time",
                        new Dictionary<string, string>
                        {
                            { "option", line.optionNumber.ToString() }
                        }));
                else
                    sb.AppendLine(ResolveMessage(
                        "helper.sensors.threat_layers.option.other",
                        "<option>: time <team_name> (<team_id>)",
                        new Dictionary<string, string>
                        {
                            { "option", line.optionNumber.ToString() },
                            { "team_name", line.teamName ?? string.Empty },
                            { "team_id", line.teamId.ToString() }
                        }));
            }
        }

        if (sb.Length <= 0)
            return string.Empty;

        return sb.ToString();
    }

    private string BuildDisembarkBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(ResolveMessage("helper.disembark.section.order", "Order"));

        if (data.DisembarkOrderLines != null && data.DisembarkOrderLines.Count > 0)
        {
            for (int i = 0; i < data.DisembarkOrderLines.Count; i++)
            {
                TurnStateManager.HelperDisembarkOrderLine line = data.DisembarkOrderLines[i];
                if (line == null)
                    continue;

                sb.AppendLine(ResolveMessage(
                    "helper.disembark.order.line",
                    "<index> - <unit> (<stats>) -> <terrain>",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty },
                        { "stats", line.stats ?? string.Empty },
                        { "terrain", line.terrainName ?? string.Empty }
                    }));
            }
        }
        else
        {
            sb.AppendLine(ResolveMessage("helper.disembark.order.empty", "0 - (empty)"));
        }

        sb.AppendLine();
        sb.AppendLine(ResolveMessage("helper.disembark.section.select_passenger", "Select Passenger"));

        if (data.DisembarkPassengerLines != null)
        {
            for (int i = 0; i < data.DisembarkPassengerLines.Count; i++)
            {
                TurnStateManager.HelperDisembarkPassengerLine line = data.DisembarkPassengerLines[i];
                if (line == null)
                    continue;

                sb.AppendLine(ResolveMessage(
                    "helper.disembark.passenger.line",
                    "<index> - <unit> (<stats>)",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty },
                        { "stats", line.stats ?? string.Empty }
                    }));
            }
        }

        if (data.HasQueuedDisembarkOrders)
            sb.Append(ResolveMessage("helper.disembark.process_order.line", "0 - Process Order"));

        return sb.ToString().TrimEnd();
    }

    private string BuildMergeBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(ResolveMessage("helper.merge.section.select", "Select Unit"));

        if (data.MergeCandidateLines != null && data.MergeCandidateLines.Count > 0)
        {
            for (int i = 0; i < data.MergeCandidateLines.Count; i++)
            {
                TurnStateManager.HelperMergeCandidateLine line = data.MergeCandidateLines[i];
                if (line == null)
                    continue;

                sb.AppendLine(ResolveMessage(
                    line.isValid ? "helper.merge.candidate.line" : "helper.merge.candidate.invalid",
                    line.isValid ? "<index> - <unit> (<stats>)" : "<color=#8F8F8F><s><index> - <unit> (<stats>)</s></color> <color=#8F8F8F>- <reason></color>",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty },
                        { "stats", line.stats ?? string.Empty },
                        { "reason", string.IsNullOrWhiteSpace(line.invalidReason) ? "invalido" : line.invalidReason }
                    }));
            }
        }
        else
        {
            sb.AppendLine(ResolveMessage("helper.merge.candidate.empty", "(none)"));
        }

        if (data.IsMergeConfirmStep && data.HasSelectedMergeCandidate)
        {
            sb.AppendLine();
            sb.Append(ResolveMessage(
                "helper.merge.confirm.line",
                "Confirm <index> - <unit> (<stats>)",
                new Dictionary<string, string>
                {
                    { "index", data.SelectedMergeCandidateNumber.ToString() },
                    { "unit", data.SelectedMergeCandidateName ?? string.Empty },
                    { "stats", data.SelectedMergeCandidateStats ?? string.Empty }
                }));

            if (!string.IsNullOrWhiteSpace(data.MergeConfirmPreview))
            {
                sb.AppendLine();
                sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
                sb.Append(ResolveMessage(
                    "helper.merge.confirm.preview",
                    "Result: <preview>",
                    new Dictionary<string, string>
                    {
                        { "preview", data.MergeConfirmPreview }
                    }));
            }
        }

        return sb.ToString().TrimEnd();
    }

    private string BuildCommandServiceBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null || data.CommandServiceServedTargets <= 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(ResolveMessage(
            data.CommandServiceIsEstimate ? "helper.command_service.targets.estimate" : "helper.command_service.targets",
            data.CommandServiceIsEstimate ? "Previstos: <targets>" : "Atendidos: <targets>",
            new Dictionary<string, string>
            {
                { "targets", Mathf.Max(0, data.CommandServiceServedTargets).ToString() }
            }));
        sb.AppendLine(ResolveMessage(
            data.CommandServiceIsEstimate ? "helper.command_service.total_cost.estimate" : "helper.command_service.total_cost",
            data.CommandServiceIsEstimate ? "Custo previsto: $<valor>" : "Custo final: $<valor>",
            new Dictionary<string, string>
            {
                { "valor", Mathf.Max(0, data.CommandServiceTotalCost).ToString() }
            }));

        if (data.CommandServiceIsEstimate)
        {
            sb.AppendLine(ResolveMessage(
                "helper.command_service.balance.estimate",
                "Saldo: $<after>",
                new Dictionary<string, string>
                {
                    { "before", Mathf.Max(0, data.CommandServiceMoneyBefore).ToString() },
                    { "after", Mathf.Max(0, data.CommandServiceMoneyAfter).ToString() }
                }));
        }

        if (data.CommandServiceIsEstimate && data.CommandServiceTargetLines != null && data.CommandServiceTargetLines.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
            for (int i = 0; i < data.CommandServiceTargetLines.Count; i++)
            {
                TurnStateManager.HelperCommandServiceTargetLine line = data.CommandServiceTargetLines[i];
                if (line == null)
                    continue;

                string prefix = line.isFocused ? ">> " : string.Empty;
                // Linhas desta lista sempre recebem ao menos um servico. Quando nem
                // todos cabem no saldo, laranja comunica atendimento parcial; cinza
                // fica reservado a CommandServiceSkippedUnitLines (nao atendidas).
                string colorOpen = line.isFullyAffordable ? string.Empty : "<color=#FFB347>";
                string colorClose = line.isFullyAffordable ? string.Empty : "</color>";
                sb.AppendLine($"{colorOpen}{prefix}{line.unitName}{colorClose}");
                sb.AppendLine($"{colorOpen}({line.gainsLabel}){colorClose}");
            }
        }

        if (data.CommandServiceIsEstimate && data.CommandServiceSkippedUnitLines != null && data.CommandServiceSkippedUnitLines.Count > 0)
        {
            sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
            sb.AppendLine($"<color=#FFB347>Unidades nao atendidas: {data.CommandServiceSkippedUnitLines.Count}</color>");
            for (int i = 0; i < data.CommandServiceSkippedUnitLines.Count; i++)
            {
                TurnStateManager.HelperCommandServiceSkippedUnitLine line = data.CommandServiceSkippedUnitLines[i];
                if (line == null)
                    continue;

                string prefix = line.isFocused ? ">> " : string.Empty;
                sb.AppendLine($"<color=#8F8F8F>{prefix}{line.unitName} ({line.sourceLabel})</color>");
            }
        }
        else if (data.CommandServiceStoppedByEconomy)
        {
            sb.AppendLine();
            string economyWarning = ResolveMessage(
                data.CommandServiceIsEstimate ? "helper.command_service.economy_stop.estimate" : "helper.command_service.economy_stop",
                data.CommandServiceIsEstimate ? "Fila vai parar por saldo" : "Fila interrompida por saldo");
            sb.Append($"<color=#FFB347>{economyWarning}</color>");
        }

        return sb.ToString().TrimEnd();
    }

    private string BuildEmbarkBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null || data.EmbarkCandidateLines == null || data.EmbarkCandidateLines.Count <= 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(ResolveMessage("helper.embark.section.transporters", "Transporters"));
        for (int i = 0; i < data.EmbarkCandidateLines.Count; i++)
        {
            TurnStateManager.HelperEmbarkCandidateLine line = data.EmbarkCandidateLines[i];
            if (line == null)
                continue;

            string prefix = line.isFocused ? ">> " : string.Empty;
            if (line.isValid)
            {
                string label = line.index > 0 ? $"{line.index}" : "-";
                sb.AppendLine(ResolveMessage(
                    "helper.embark.candidate.line",
                    "<prefix><index> - <unit> (<stats>)",
                    new Dictionary<string, string>
                    {
                        { "prefix", prefix },
                        { "index", label },
                        { "unit", line.unitName ?? string.Empty },
                        { "stats", line.stats ?? string.Empty }
                    }));
            }
            else
            {
                sb.AppendLine(ResolveMessage(
                    "helper.embark.candidate.invalid",
                    "<color=#8F8F8F><prefix><unit> (<stats>)</color> <color=#8F8F8F>- <reason></color>",
                    new Dictionary<string, string>
                    {
                        { "prefix", prefix },
                        { "unit", line.unitName ?? string.Empty },
                        { "stats", line.stats ?? string.Empty },
                        { "reason", string.IsNullOrWhiteSpace(line.invalidReason) ? "invalido" : line.invalidReason }
                    }));
            }
        }

        return sb.ToString().TrimEnd();
    }

    private string BuildSupplyBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null || data.SupplyServedTargets <= 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(ResolveMessage(
            data.SupplyIsConfirmStep ? "helper.supply.targets.confirm" : "helper.supply.targets.queue",
            data.SupplyIsConfirmStep ? "Previstos: <targets>" : "Na fila: <targets>",
            new Dictionary<string, string>
            {
                { "targets", Mathf.Max(0, data.SupplyServedTargets).ToString() }
            }));
        string gainsLine = ResolveMessage(
            "helper.supply.gains",
            "Ganhos: HP +<hp> | FUEL +<fuel> | AMMO +<ammo>",
            new Dictionary<string, string>
            {
                { "hp", Mathf.Max(0, data.SupplyRecoveredHp).ToString() },
                { "fuel", Mathf.Max(0, data.SupplyRecoveredFuel).ToString() },
                { "ammo", Mathf.Max(0, data.SupplyRecoveredAmmo).ToString() }
            });
        gainsLine = RemoveZeroGainSegments(gainsLine);
        if (!string.IsNullOrWhiteSpace(gainsLine))
            sb.AppendLine(gainsLine);
        sb.AppendLine(ResolveMessage(
            "helper.supply.total_cost",
            "Custo estimado: $<valor>",
            new Dictionary<string, string>
            {
                { "valor", Mathf.Max(0, data.SupplyTotalCost).ToString() }
            }));

        if (data.SupplyTargetLines != null && data.SupplyTargetLines.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
            for (int i = 0; i < data.SupplyTargetLines.Count; i++)
            {
                TurnStateManager.HelperSupplyTargetLine line = data.SupplyTargetLines[i];
                if (line == null)
                    continue;

                string prefix = line.isFocused ? ">> " : string.Empty;
                sb.AppendLine($"{prefix}{line.index}. {line.unitName}");
                sb.AppendLine($"({line.gainsLabel}) | ${Mathf.Max(0, line.estimatedCost)}");
            }
        }

        if (data.SupplyResourceLines != null && data.SupplyResourceLines.Count > 0)
        {
            sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
            sb.AppendLine(ResolveMessage("helper.supply.supplier_consumption", "Consumo do Supridor"));
            for (int i = 0; i < data.SupplyResourceLines.Count; i++)
            {
                TurnStateManager.HelperSupplyResourceLine line = data.SupplyResourceLines[i];
                if (line == null)
                    continue;
                sb.AppendLine(ResolveMessage(
                    "helper.supply.supplier_consumption.line",
                    "<supply>: <before> - <consumed> -> <after>",
                    new Dictionary<string, string>
                    {
                        { "supply", line.supplyName ?? "Supply" },
                        { "before", Mathf.Max(0, line.beforeAmount).ToString() },
                        { "consumed", Mathf.Max(0, line.beforeAmount - line.afterAmount).ToString() },
                        { "after", Mathf.Max(0, line.afterAmount).ToString() }
                    }));
            }
        }

        if (data.SupplyHasQueuedOrders)
        {
            sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
            sb.Append(ResolveMessage("helper.supply.process_order.line", "0 - Processar Ordem de Suprimentos"));
        }

        return sb.ToString().TrimEnd();
    }

    private static string RemoveZeroGainSegments(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return string.Empty;

        string[] segments = line.Split('|');
        if (segments.Length <= 1)
            return ContainsStandalonePlusZero(line) ? string.Empty : line.Trim();

        List<string> kept = new List<string>(segments.Length);
        for (int i = 0; i < segments.Length; i++)
        {
            string segment = segments[i].Trim();
            if (string.IsNullOrWhiteSpace(segment))
                continue;
            if (ContainsStandalonePlusZero(segment))
                continue;
            kept.Add(segment);
        }

        return kept.Count <= 0 ? string.Empty : string.Join(" | ", kept);
    }

    private static bool ContainsStandalonePlusZero(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        int index = text.IndexOf("+0", System.StringComparison.Ordinal);
        while (index >= 0)
        {
            int next = index + 2;
            if (next >= text.Length || !char.IsDigit(text[next]))
                return true;
            index = text.IndexOf("+0", next, System.StringComparison.Ordinal);
        }

        return false;
    }

    private string BuildTransferBody(TurnStateManager.HelperPanelData data)
    {
        if (data == null || data.TransferCandidateLines == null || data.TransferCandidateLines.Count <= 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        if (!data.TransferIsConfirmStep)
        {
            sb.AppendLine(ResolveMessage("helper.transfer.section.select", "Selecionar Transferencia"));
            for (int i = 0; i < data.TransferCandidateLines.Count; i++)
            {
                TurnStateManager.HelperTransferCandidateLine line = data.TransferCandidateLines[i];
                if (line == null)
                    continue;

                string prefix = line.isFocused ? ">> " : string.Empty;
                string transferType = line.isDonate
                    ? ResolveMessage("helper.transfer.type.donate", "Doar")
                    : ResolveMessage("helper.transfer.type.receive", "Receber");
                sb.AppendLine(prefix + ResolveMessage(
                    "helper.transfer.candidate.line",
                    "<number>. <transfer_type> -> <unit_name>",
                    new Dictionary<string, string>
                    {
                        { "number", line.index.ToString() },
                        { "transfer_type", transferType },
                        { "unit_name", line.unitName ?? "(alvo)" }
                    }));
            }

            sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
            sb.Append(ResolveMessage("helper.transfer.select.hint", "Enter - confirmar destino | ESC - cancelar"));
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine(ResolveMessage("helper.transfer.section.confirm", "Confirmar Transferencia"));
        TurnStateManager.HelperTransferCandidateLine selectedLine = null;
        for (int i = 0; i < data.TransferCandidateLines.Count; i++)
        {
            TurnStateManager.HelperTransferCandidateLine candidate = data.TransferCandidateLines[i];
            if (candidate != null && candidate.isFocused)
            {
                selectedLine = candidate;
                break;
            }
        }
        if (selectedLine != null)
        {
            string transferType = selectedLine.isDonate
                ? ResolveMessage("helper.transfer.type.donate", "Doar")
                : ResolveMessage("helper.transfer.type.receive", "Receber");
            sb.AppendLine(ResolveMessage(
                "helper.transfer.candidate.line",
                "<number>. <transfer_type> -> <unit_name>",
                new Dictionary<string, string>
                {
                    { "number", selectedLine.index.ToString() },
                    { "transfer_type", transferType },
                    { "unit_name", selectedLine.unitName ?? "(alvo)" }
                }));
        }
        else if (!string.IsNullOrWhiteSpace(data.TransferSelectedLabel))
        {
            sb.AppendLine(data.TransferSelectedLabel);
        }
        sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));

        if (data.TransferResourceLines != null && data.TransferResourceLines.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(data.TransferSourceLabel))
            {
                sb.AppendLine(ResolveMessage(
                    "helper.transfer.source.header",
                    "Fornecedor: <source>",
                    new Dictionary<string, string>
                    {
                        { "source", data.TransferSourceLabel }
                    }));
                for (int i = 0; i < data.TransferResourceLines.Count; i++)
                {
                    TurnStateManager.HelperTransferResourceLine line = data.TransferResourceLines[i];
                    if (line == null)
                        continue;

                    // Em origem infinita, mostra somente o volume enviado (evita int.MaxValue no helper).
                    if (line.sourceIsInfinite)
                    {
                        sb.AppendLine(ResolveMessage(
                            "helper.transfer.source.line.infinite",
                            "- <supply>: <moved>",
                            new Dictionary<string, string>
                            {
                                { "supply", line.supplyName ?? "Supply" },
                                { "moved", Mathf.Max(0, line.movedAmount).ToString() }
                            }));
                    }
                    else
                    {
                        sb.AppendLine(ResolveMessage(
                            "helper.transfer.source.line.finite",
                            "- <supply>: <before> - <moved> -> <after>",
                            new Dictionary<string, string>
                            {
                                { "supply", line.supplyName ?? "Supply" },
                                { "before", Mathf.Max(0, line.sourceBefore).ToString() },
                                { "moved", Mathf.Max(0, line.movedAmount).ToString() },
                                { "after", Mathf.Max(0, line.sourceAfter).ToString() }
                            }));
                    }
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(data.TransferDestinationLabel))
            {
                sb.AppendLine(ResolveMessage(
                    "helper.transfer.destination.header",
                    "Destino: <destination>",
                    new Dictionary<string, string>
                    {
                        { "destination", data.TransferDestinationLabel }
                    }));
            }

            for (int i = 0; i < data.TransferResourceLines.Count; i++)
            {
                TurnStateManager.HelperTransferResourceLine line = data.TransferResourceLines[i];
                if (line == null)
                    continue;

                string dstBefore = line.destinationBefore >= int.MaxValue ? "INF" : Mathf.Max(0, line.destinationBefore).ToString();
                string dstAfter = line.destinationAfter >= int.MaxValue ? "INF" : Mathf.Max(0, line.destinationAfter).ToString();
                sb.AppendLine(ResolveMessage(
                    "helper.transfer.destination.line",
                    "- <supply>: <before> + <moved> -> <after>",
                    new Dictionary<string, string>
                    {
                        { "supply", line.supplyName ?? "Supply" },
                        { "before", dstBefore },
                        { "moved", Mathf.Max(0, line.movedAmount).ToString() },
                        { "after", dstAfter }
                    }));
            }
        }
        else
        {
            sb.AppendLine(ResolveMessage("helper.transfer.preview.empty", "Sem estoque transferivel para esta opcao."));
        }

        sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
        sb.Append(ResolveMessage("helper.transfer.confirm.hint", "Enter - executar | ESC - voltar"));
        return sb.ToString().TrimEnd();
    }

    private string ResolveSensorLabel(string sensorKey)
    {
        switch (sensorKey)
        {
            case "aim":
                return ResolveMessage("helper.sensors.label.aim", "Aim");
            case "embark":
                return ResolveMessage("helper.sensors.label.embark", "Embark");
            case "disembark":
                return ResolveMessage("helper.sensors.label.disembark", "Disembark");
            case "capture":
                return ResolveMessage("helper.sensors.label.capture", "Capture");
            case "fuse":
                return ResolveMessage("helper.sensors.label.fuse", "Fuse units");
            case "supply":
                return ResolveMessage("helper.sensors.label.supply", "Supply");
            case "transfer":
                return ResolveMessage("helper.sensors.label.transfer", "Transfer");
            case "layer":
                return ResolveMessage("helper.sensors.label.layer", "Layer");
            case "threat_layers":
                return ResolveMessage("helper.sensors.label.threat_layers", "Threat Layers");
            case "move_only":
                return ResolveMessage("helper.sensors.label.move_only", "Move Only");
            default:
                return sensorKey ?? string.Empty;
        }
    }

    private string ResolveMessage(string id, string fallback)
    {
        if (helperDatabase == null)
            return fallback ?? string.Empty;

        return helperDatabase.Resolve(id, fallback);
    }

    private string ResolveMessage(string id, string fallback, IReadOnlyDictionary<string, string> tokens)
    {
        if (helperDatabase == null)
            return ApplyInlineTokens(fallback ?? string.Empty, tokens);

        return helperDatabase.Resolve(id, fallback, tokens);
    }

    private static string ApplyInlineTokens(string template, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(template) || tokens == null || tokens.Count == 0)
            return template ?? string.Empty;

        string output = template;
        foreach (KeyValuePair<string, string> pair in tokens)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            string key = pair.Key.Trim();
            string val = pair.Value ?? string.Empty;
            
            output = output.Replace($"<{key}>", val);
            output = output.Replace($"<{key.ToLowerInvariant()}>", val);
            output = output.Replace($"<{key.ToUpperInvariant()}>", val);
            if (key.Length > 0)
            {
                string titleCase = char.ToUpperInvariant(key[0]) + (key.Length > 1 ? key.Substring(1).ToLowerInvariant() : string.Empty);
                output = output.Replace($"<{titleCase}>", val);
            }
        }

        return output;
    }

    private void HideAll(bool force)
    {
        if (isDockedCenterLeft)
            RestoreOriginalLayout();
        hasLastUndockedScreenRect = false;
        cursorNearUndockedDockRegion = false;
        ResetHelperScrollLayout();
        SetVisible(panelVisible: false, title: string.Empty, body: string.Empty, data: null, force: force);
    }

    private void SetVisible(bool panelVisible, string title, string body, TurnStateManager.HelperPanelData data, bool force)
    {
        bool textChanged = force || lastTitle != title || lastBody != body;
        if (force || panelVisible != lastPanelVisible)
            SetPanelVisible(panelVisible);

        if (helperTitle != null)
        {
            if (force || lastTitle != title)
                helperTitle.text = title ?? string.Empty;
            helperTitle.enabled = panelVisible;
        }

        if (helperTxt != null)
        {
            if (force || lastBody != body)
                helperTxt.text = body ?? string.Empty;
            Color txtColor = ResolveActiveTeamColor(data);
            if (force || txtColor != lastHelperTxtColor)
            {
                helperTxt.color = txtColor;
                lastHelperTxtColor = txtColor;
            }
            helperTxt.enabled = panelVisible;
        }

        // Cor do time ativo deste frame — aplicada a todos os botoes gerados via script abaixo.
        currentTeamColor = ResolveActiveTeamColor(data);
        RefreshCancelControl(panelVisible);
        RefreshExecuteCommandServiceControl(panelVisible);
        RefreshCommandServicePreviewFocusHighlight(panelVisible);
        RefreshSensorActionControls(panelVisible, data);
        RefreshAimTargetControls(panelVisible, data);
        RefreshAimFooterFocus(panelVisible, data);
        RefreshAimConfirmDetails(panelVisible, data);
        RefreshShoppingActionControls(panelVisible, data);
        RefreshPersistenceActionControls(panelVisible);
        RefreshDynamicPanelHeight(panelVisible, textChanged);

        lastPanelVisible = panelVisible;
        lastTitle = title ?? string.Empty;
        lastBody = body ?? string.Empty;
    }

    private void RefreshDynamicPanelHeight(bool panelVisible, bool contentChanged)
    {
        if (helperRect == null)
            return;

        if (cachedBasePanelHeight <= 0f)
            cachedBasePanelHeight = Mathf.Max(0f, helperRect.rect.height);

        if (!autoExpandHeight)
            return;

        if (!panelVisible)
        {
            float resetHeight = cachedBasePanelHeight > 0f ? cachedBasePanelHeight : helperRect.rect.height;
            helperRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, resetHeight);
            ResetHelperScrollLayout();
            return;
        }

        if (!contentChanged)
            return;

        float titleHeight = 0f;
        if (helperTitle != null)
        {
            helperTitle.ForceMeshUpdate();
            titleHeight = originalHelperTitleHeight > 0f
                ? originalHelperTitleHeight
                : Mathf.Max(0f, helperTitle.preferredHeight);
        }

        float bodyHeight = 0f;
        bool sensorButtonsActive = sensorActionsRoot != null && sensorActionsRoot.activeSelf;
        bool aimButtonsActive = aimTargetsRoot != null && aimTargetsRoot.activeSelf;
        bool aimConfirmActive = aimConfirmDetailsRoot != null && aimConfirmDetailsRoot.activeSelf;
        bool shoppingButtonsActive = shoppingActionsRoot != null && shoppingActionsRoot.activeSelf;
        bool persistenceButtonsActive = persistenceActionsRoot != null && persistenceActionsRoot.activeSelf;
        if (sensorButtonsActive)
        {
            bodyHeight = sensorActionButtons.Count * (SensorActionButtonHeight + 4f);
        }
        else if (aimButtonsActive)
        {
            bodyHeight = aimTargetButtons.Count * (AimTargetButtonHeight + 4f);
        }
        else if (aimConfirmActive)
        {
            bodyHeight = AimConfirmDetailsHeight;
        }
        else if (shoppingButtonsActive)
        {
            bodyHeight = shoppingActionButtons.Count * (ShoppingActionButtonHeight + 4f);
        }
        else if (persistenceButtonsActive)
        {
            bodyHeight = persistenceActionButtons.Count * (PersistenceActionButtonHeight + 4f);
        }
        else if (helperTxt != null)
        {
            helperTxt.ForceMeshUpdate();
            bodyHeight = Mathf.Max(0f, helperTxt.preferredHeight);
        }

        float baseMin = cachedBasePanelHeight > 0f ? cachedBasePanelHeight : 0f;
        float minHeight = Mathf.Max(minPanelHeight, baseMin);
        float maxHeight = Mathf.Max(minHeight, maxPanelHeight);
        float footerHeight = GetActiveFooterHeight();
        float targetHeight = Mathf.Clamp(titleHeight + bodyHeight + Mathf.Max(0f, contentVerticalPadding) + footerHeight, minHeight, maxHeight);
        helperRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        RefreshHelperScrollLayout(titleHeight, bodyHeight, targetHeight);
    }

    private void RefreshSensorActionControls(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        bool active = panelVisible && data != null &&
                      data.Kind == TurnStateManager.HelperPanelKind.Sensors &&
                      !data.ThreatLayerSelectionActive &&
                      data.SensorLines != null && data.SensorLines.Count > 0;

        if (!active)
        {
            if (sensorActionsRoot != null)
                sensorActionsRoot.SetActive(false);
            sensorActionsSignature = string.Empty;
            return;
        }

        EnsureSensorActionsRoot();
        if (sensorActionsRoot == null)
            return;

        string signature = BuildSensorActionsSignature(data.SensorLines);
        if (signature != sensorActionsSignature)
        {
            RebuildSensorActionButtons(data.SensorLines);
            sensorActionsSignature = signature;
        }

        sensorActionsRoot.SetActive(true);
        for (int i = 0; i < sensorActionButtons.Count; i++)
        {
            bool focused = turnStateManager != null && i < sensorActionButtonCodes.Count &&
                           sensorActionButtonCodes[i] == turnStateManager.SensorOptionFocusCode;
            TintScriptButtonToTeam(sensorActionButtons[i], focused);
        }
        ApplyFooterButtonFocus(cancelActionImage, cancelActionLabel,
            turnStateManager != null && turnStateManager.SensorOptionCancelFocused);
        if (helperTxt != null)
            helperTxt.enabled = false;

        if (panelHelper == gameObject && selfPanelCanvasGroup != null)
        {
            selfPanelCanvasGroup.interactable = true;
            selfPanelCanvasGroup.blocksRaycasts = true;
        }
    }

    private void EnsureSensorActionsRoot()
    {
        if (!Application.isPlaying || sensorActionsRoot != null || helperRect == null)
            return;

        sensorActionsRoot = new GameObject("helper_sensor_actions", typeof(RectTransform), typeof(VerticalLayoutGroup));
        RectTransform rect = sensorActionsRoot.GetComponent<RectTransform>();
        rect.SetParent(helperRect, false);
        rect.anchorMin = new Vector2(0.06f, 1f);
        rect.anchorMax = new Vector2(0.94f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -48f);
        rect.sizeDelta = new Vector2(0f, 1f);
        rect.SetAsLastSibling();

        VerticalLayoutGroup layout = sensorActionsRoot.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        sensorActionsRoot.SetActive(false);
    }

    private void RefreshAimTargetControls(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        bool active = panelVisible && data != null && data.Kind == TurnStateManager.HelperPanelKind.AimTargets &&
                      data.AimTargetLines != null && data.AimTargetLines.Count > 0;
        if (!active)
        {
            if (aimTargetsRoot != null) aimTargetsRoot.SetActive(false);
            aimTargetsSignature = string.Empty;
            return;
        }
        EnsureAimTargetsRoot();
        StringBuilder signatureBuilder = new StringBuilder();
        for (int i = 0; i < data.AimTargetLines.Count; i++)
            signatureBuilder.Append(data.AimTargetLines[i].unitName).Append(data.AimTargetLines[i].isValid);
        string signature = signatureBuilder.ToString();
        if (signature != aimTargetsSignature)
        {
            RebuildAimTargetButtons(data.AimTargetLines);
            aimTargetsSignature = signature;
        }
        aimTargetsRoot.SetActive(true);
        for (int i = 0; i < aimTargetButtons.Count && i < data.AimTargetLines.Count; i++)
        {
            Button button = aimTargetButtons[i];
            if (!data.AimTargetLines[i].isValid)
            {
                button.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
                button.GetComponentInChildren<TMP_Text>(true).color = Color.gray;
            }
            else
                TintScriptButtonToTeam(button, data.AimTargetLines[i].isFocused);
        }
        if (helperTxt != null) helperTxt.enabled = false;
        // O CANCELAR agora e o ultimo item da lista (destacado no loop acima), nao mais o botao de rodape.
        if (panelHelper == gameObject && selfPanelCanvasGroup != null)
        {
            selfPanelCanvasGroup.interactable = true;
            selfPanelCanvasGroup.blocksRaycasts = true;
        }
    }

    private void RefreshAimFooterFocus(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        if (!panelVisible || data == null || data.Kind != TurnStateManager.HelperPanelKind.AimConfirm || turnStateManager == null)
            return;
        int focus = turnStateManager.MirandoConfirmButtonFocus;
        ApplyFooterButtonFocus(executeCommandServiceImage, executeCommandServiceLabel, focus == 0);
        ApplyFooterButtonFocus(cancelActionImage, cancelActionLabel, focus == 1);
    }

    // Detalhes do CONFIRMAR ATAQUE: sprite + nome do alvo, HP e LOCAL.
    private void RefreshAimConfirmDetails(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        bool active = panelVisible && data != null && data.Kind == TurnStateManager.HelperPanelKind.AimConfirm;
        if (!active)
        {
            if (aimConfirmDetailsRoot != null) aimConfirmDetailsRoot.SetActive(false);
            return;
        }

        EnsureAimConfirmDetailsRoot();
        if (aimConfirmDetailsRoot == null) return;

        aimConfirmTargetText.text = data.AimConfirmTargetName;
        aimConfirmTargetIcon.sprite = data.AimConfirmTargetSprite;
        aimConfirmTargetIcon.enabled = data.AimConfirmTargetSprite != null;
        aimConfirmTargetIcon.color = data.AimConfirmTargetColor;
        aimConfirmHpText.text = $"HP: {data.AimConfirmHp}";
        aimConfirmLocalText.text = string.IsNullOrWhiteSpace(data.AimConfirmTerrainLabel)
            ? "LOCAL:" : $"LOCAL: {data.AimConfirmTerrainLabel}";
        aimConfirmLocalIcon.sprite = data.AimConfirmLocalSprite;
        aimConfirmLocalIcon.enabled = data.AimConfirmLocalSprite != null;
        aimConfirmLocalIcon.color = data.AimConfirmLocalColor;

        aimConfirmTargetText.color = currentTeamColor;
        aimConfirmHpText.color = currentTeamColor;
        aimConfirmLocalText.color = currentTeamColor;

        aimConfirmDetailsRoot.SetActive(true);
        if (helperTxt != null) helperTxt.enabled = false;
    }

    private void EnsureAimConfirmDetailsRoot()
    {
        if (!Application.isPlaying || aimConfirmDetailsRoot != null || helperRect == null) return;
        aimConfirmDetailsRoot = new GameObject("helper_aim_confirm_details", typeof(RectTransform), typeof(VerticalLayoutGroup));
        RectTransform rect = aimConfirmDetailsRoot.GetComponent<RectTransform>();
        rect.SetParent(helperRect, false);
        rect.anchorMin = new Vector2(0.06f, 1f); rect.anchorMax = new Vector2(0.94f, 1f);
        rect.pivot = new Vector2(0.5f, 1f); rect.anchoredPosition = new Vector2(0f, -48f);
        rect.sizeDelta = new Vector2(0f, AimConfirmDetailsHeight);
        VerticalLayoutGroup layout = aimConfirmDetailsRoot.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 6f; layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

        GameObject targetRow = new GameObject("aim_confirm_target", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        targetRow.transform.SetParent(aimConfirmDetailsRoot.transform, false);
        LayoutElement targetRowLE = targetRow.GetComponent<LayoutElement>();
        targetRowLE.minHeight = 68f; targetRowLE.preferredHeight = 68f;
        HorizontalLayoutGroup targetLayout = targetRow.GetComponent<HorizontalLayoutGroup>();
        targetLayout.spacing = 10f; targetLayout.childAlignment = TextAnchor.MiddleCenter;
        targetLayout.childControlWidth = true; targetLayout.childControlHeight = true;
        targetLayout.childForceExpandWidth = false; targetLayout.childForceExpandHeight = false;

        GameObject targetIconObj = new GameObject("target_icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        targetIconObj.transform.SetParent(targetRow.transform, false);
        LayoutElement targetIconLE = targetIconObj.GetComponent<LayoutElement>();
        targetIconLE.minWidth = 68f; targetIconLE.preferredWidth = 68f;
        targetIconLE.minHeight = 68f; targetIconLE.preferredHeight = 68f;
        aimConfirmTargetIcon = targetIconObj.GetComponent<Image>();
        aimConfirmTargetIcon.preserveAspect = true; aimConfirmTargetIcon.raycastTarget = false;

        GameObject targetNameObj = new GameObject("target_name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        targetNameObj.transform.SetParent(targetRow.transform, false);
        LayoutElement targetNameLE = targetNameObj.GetComponent<LayoutElement>();
        targetNameLE.minWidth = 80f; targetNameLE.preferredWidth = 160f;
        aimConfirmTargetText = targetNameObj.GetComponent<TMP_Text>();
        aimConfirmTargetText.fontSize = 22f; aimConfirmTargetText.fontStyle = FontStyles.Bold;
        aimConfirmTargetText.alignment = TextAlignmentOptions.MidlineLeft; aimConfirmTargetText.raycastTarget = false;
        aimConfirmTargetText.enableAutoSizing = true; aimConfirmTargetText.fontSizeMin = 13f; aimConfirmTargetText.fontSizeMax = 22f;

        aimConfirmHpText = CreateAimConfirmText("aim_confirm_hp", 22f, 28f);

        // Linha LOCAL: nome (label + terreno) a esquerda e o icone do hex a direita.
        GameObject row = new GameObject("aim_confirm_local", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(aimConfirmDetailsRoot.transform, false);
        LayoutElement rowLE = row.GetComponent<LayoutElement>(); rowLE.minHeight = 48f; rowLE.preferredHeight = 48f;
        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        GameObject nameObj = new GameObject("local_text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        nameObj.transform.SetParent(row.transform, false);
        LayoutElement nameLE = nameObj.GetComponent<LayoutElement>(); nameLE.minWidth = 60f; nameLE.preferredWidth = 150f;
        aimConfirmLocalText = nameObj.GetComponent<TMP_Text>();
        aimConfirmLocalText.fontSize = 18f; aimConfirmLocalText.fontStyle = FontStyles.Bold;
        aimConfirmLocalText.alignment = TextAlignmentOptions.MidlineRight; aimConfirmLocalText.raycastTarget = false;
        aimConfirmLocalText.enableAutoSizing = true; aimConfirmLocalText.fontSizeMin = 12f; aimConfirmLocalText.fontSizeMax = 18f;

        GameObject iconObj = new GameObject("local_icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(row.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>(); iconRect.sizeDelta = new Vector2(44f, 44f);
        LayoutElement iconLE = iconObj.GetComponent<LayoutElement>(); iconLE.minWidth = 44f; iconLE.preferredWidth = 44f; iconLE.minHeight = 44f; iconLE.preferredHeight = 44f;
        aimConfirmLocalIcon = iconObj.GetComponent<Image>();
        aimConfirmLocalIcon.preserveAspect = true; aimConfirmLocalIcon.raycastTarget = false;

        aimConfirmDetailsRoot.SetActive(false);
    }

    private TMP_Text CreateAimConfirmText(string objectName, float fontSize, float height)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        obj.transform.SetParent(aimConfirmDetailsRoot.transform, false);
        LayoutElement le = obj.GetComponent<LayoutElement>(); le.minHeight = height; le.preferredHeight = height;
        TMP_Text text = obj.GetComponent<TMP_Text>();
        text.fontSize = fontSize; text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.enableAutoSizing = true; text.fontSizeMin = 12f; text.fontSizeMax = fontSize;
        return text;
    }

    private void EnsureAimTargetsRoot()
    {
        if (!Application.isPlaying || aimTargetsRoot != null || helperRect == null) return;
        aimTargetsRoot = new GameObject("helper_aim_targets", typeof(RectTransform), typeof(VerticalLayoutGroup));
        RectTransform rect = aimTargetsRoot.GetComponent<RectTransform>();
        rect.SetParent(helperRect, false);
        rect.anchorMin = new Vector2(0.06f, 1f); rect.anchorMax = new Vector2(0.94f, 1f);
        rect.pivot = new Vector2(0.5f, 1f); rect.anchoredPosition = new Vector2(0f, -48f);
        VerticalLayoutGroup layout = aimTargetsRoot.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f; layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        aimTargetsRoot.SetActive(false);
    }

    private void RebuildAimTargetButtons(List<TurnStateManager.HelperAimTargetLine> lines)
    {
        for (int i = aimTargetButtons.Count - 1; i >= 0; i--)
            if (aimTargetButtons[i] != null) Destroy(aimTargetButtons[i].gameObject);
        aimTargetButtons.Clear();
        for (int i = 0; i < lines.Count; i++)
        {
            TurnStateManager.HelperAimTargetLine line = lines[i];
            bool isCancel = line.isCancel;
            int targetIndex = line.index;
            string objSuffix = isCancel ? "cancel" : i.ToString();
            GameObject obj = new GameObject($"button_aim_target_{objSuffix}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            obj.transform.SetParent(aimTargetsRoot.transform, false);
            obj.GetComponent<Image>().color = FooterButtonIdleColor;
            LayoutElement element = obj.GetComponent<LayoutElement>(); element.minHeight = AimTargetButtonHeight; element.preferredHeight = AimTargetButtonHeight;
            Button button = obj.GetComponent<Button>();
            if (isCancel)
                button.onClick.AddListener(() => cursorController?.TryCancelCurrentActionFromPointer());
            else
                button.onClick.AddListener(() => turnStateManager?.TrySelectMirandoTargetFromPointer(targetIndex));
            GameObject labelObj = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObj.GetComponent<RectTransform>(); labelRect.SetParent(obj.transform, false);
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero;
            TMP_Text label = labelObj.GetComponent<TMP_Text>();
            if (isCancel)
                label.text = line.unitName;
            else
            {
                // Linha 1: "N - Nome (Hp: X)". Linha 2: terreno (Cidade / Estrada na Floresta / Floresta).
                string head = $"{i + 1} - {line.unitName} (Hp: {line.hp})";
                label.text = string.IsNullOrWhiteSpace(line.terrainLabel) ? head : $"{head}\n{line.terrainLabel}";
            }
            label.fontStyle = FontStyles.Bold; label.color = FooterLabelIdleColor; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            // Auto-encolhe se o nome for grande, pra nao estourar a largura do botao.
            label.enableAutoSizing = true; label.fontSizeMin = 12f; label.fontSizeMax = 20f;
            aimTargetButtons.Add(button);
        }
        aimTargetsRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, aimTargetButtons.Count * (AimTargetButtonHeight + 4f));
    }

    private void RefreshShoppingActionControls(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        bool active = panelVisible && data != null &&
                      data.Kind == TurnStateManager.HelperPanelKind.Shopping &&
                      data.ShoppingLines != null && data.ShoppingLines.Count > 0;

        if (!active)
        {
            if (shoppingActionsRoot != null)
                shoppingActionsRoot.SetActive(false);
            shoppingActionsSignature = string.Empty;
            return;
        }

        EnsureShoppingActionsRoot();
        if (shoppingActionsRoot == null)
            return;

        string signature = BuildShoppingActionsSignature(data.ShoppingLines);
        if (signature != shoppingActionsSignature)
        {
            RebuildShoppingActionButtons(data.ShoppingLines);
            shoppingActionsSignature = signature;
        }

        shoppingActionsRoot.SetActive(true);
        // Reflete a navegacao: destaca o item selecionado (ou o CANCELAR, o ultimo botao da lista).
        int focusedShoppingIndex = turnStateManager.ShoppingCancelFocused
            ? shoppingActionButtons.Count - 1
            : turnStateManager.ShoppingSelectedOptionIndex;
        for (int i = 0; i < shoppingActionButtons.Count; i++)
        {
            Button shoppingButton = shoppingActionButtons[i];
            if (shoppingButton == null)
                continue;
            ApplyFooterButtonFocus(
                shoppingButton.GetComponent<Image>(),
                shoppingButton.GetComponentInChildren<TMP_Text>(true),
                i == focusedShoppingIndex);
        }
        if (helperTxt != null)
            helperTxt.enabled = false;

        if (panelHelper == gameObject && selfPanelCanvasGroup != null)
        {
            selfPanelCanvasGroup.interactable = true;
            selfPanelCanvasGroup.blocksRaycasts = true;
        }
    }

    private void EnsureShoppingActionsRoot()
    {
        if (!Application.isPlaying || shoppingActionsRoot != null || helperRect == null)
            return;

        shoppingActionsRoot = new GameObject("helper_shopping_actions", typeof(RectTransform), typeof(VerticalLayoutGroup));
        RectTransform rect = shoppingActionsRoot.GetComponent<RectTransform>();
        rect.SetParent(helperRect, false);
        rect.anchorMin = new Vector2(0.06f, 1f);
        rect.anchorMax = new Vector2(0.94f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -48f);
        rect.sizeDelta = new Vector2(0f, 1f);
        rect.SetAsLastSibling();

        VerticalLayoutGroup layout = shoppingActionsRoot.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        shoppingActionsRoot.SetActive(false);
    }

    private void RefreshPersistenceActionControls(bool panelVisible)
    {
        bool active = panelVisible && saveGameManager != null &&
                      (saveGameManager.IsPersistenceSlotSelectionActive ||
                       saveGameManager.IsPersistenceOverwriteConfirmationActive);
        if (!active)
        {
            if (persistenceActionsRoot != null)
                persistenceActionsRoot.SetActive(false);
            persistenceActionsSignature = string.Empty;
            return;
        }

        EnsurePersistenceActionsRoot();
        string signature = saveGameManager.IsPersistenceOverwriteConfirmationActive
            ? "overwrite"
            : string.Join("|", saveGameManager.GetPersistenceSlotButtonLabel(1),
                saveGameManager.GetPersistenceSlotButtonLabel(2), saveGameManager.GetPersistenceSlotButtonLabel(3));
        if (signature != persistenceActionsSignature)
        {
            RebuildPersistenceActionButtons();
            persistenceActionsSignature = signature;
        }

        persistenceActionsRoot.SetActive(true);
        RefreshPersistencePromptFocusHighlight();
        if (helperTxt != null)
            helperTxt.enabled = false;
        if (panelHelper == gameObject && selfPanelCanvasGroup != null)
        {
            selfPanelCanvasGroup.interactable = true;
            selfPanelCanvasGroup.blocksRaycasts = true;
        }
    }

    private void EnsurePersistenceActionsRoot()
    {
        if (!Application.isPlaying || persistenceActionsRoot != null || helperRect == null)
            return;
        persistenceActionsRoot = new GameObject("helper_persistence_actions", typeof(RectTransform), typeof(VerticalLayoutGroup));
        RectTransform rect = persistenceActionsRoot.GetComponent<RectTransform>();
        rect.SetParent(helperRect, false);
        rect.anchorMin = new Vector2(0.06f, 1f);
        rect.anchorMax = new Vector2(0.94f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -48f);
        rect.sizeDelta = new Vector2(0f, 1f);
        rect.SetAsLastSibling();
        VerticalLayoutGroup layout = persistenceActionsRoot.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        persistenceActionsRoot.SetActive(false);
    }

    private void RebuildPersistenceActionButtons()
    {
        for (int i = persistenceActionButtons.Count - 1; i >= 0; i--)
            if (persistenceActionButtons[i] != null)
                Destroy(persistenceActionButtons[i].gameObject);
        persistenceActionButtons.Clear();
        persistenceActionImages.Clear();
        persistenceActionLabels.Clear();

        if (saveGameManager.IsPersistenceOverwriteConfirmationActive)
        {
            CreatePersistenceButton("CONFIRMAR SOBRESCRITA", () => saveGameManager.TryConfirmPersistenceOverwriteFromPointer());
            CreatePersistenceButton("VOLTAR", () => saveGameManager.TryCancelPersistencePromptFromPointer());
        }
        else
        {
            for (int slot = 1; slot <= 3; slot++)
            {
                int selectedSlot = slot;
                CreatePersistenceButton(saveGameManager.GetPersistenceSlotButtonLabel(slot),
                    () => saveGameManager.TryChoosePersistenceSlotFromPointer(selectedSlot));
            }
            CreatePersistenceButton("CANCELAR", () => saveGameManager.TryCancelPersistencePromptFromPointer());
        }

        persistenceActionsRoot.GetComponent<RectTransform>().sizeDelta =
            new Vector2(0f, persistenceActionButtons.Count * (PersistenceActionButtonHeight + 4f));
    }

    // Destaca o botao de save/load em foco (mesmo visual do preview do Servico do Comando).
    private void RefreshPersistencePromptFocusHighlight()
    {
        int focus = saveGameManager != null ? saveGameManager.PersistencePromptFocusIndex : -1;
        for (int i = 0; i < persistenceActionButtons.Count; i++)
        {
            Image image = i < persistenceActionImages.Count ? persistenceActionImages[i] : null;
            TMP_Text label = i < persistenceActionLabels.Count ? persistenceActionLabels[i] : null;
            ApplyFooterButtonFocus(image, label, i == focus);
        }
    }

    private void CreatePersistenceButton(string text, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject("button_persistence", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(persistenceActionsRoot.transform, false);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = FooterButtonIdleColor;
        LayoutElement element = buttonObject.GetComponent<LayoutElement>();
        element.minHeight = PersistenceActionButtonHeight;
        element.preferredHeight = PersistenceActionButtonHeight;
        Button button = buttonObject.GetComponent<Button>();
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        button.onClick.AddListener(action);

        GameObject labelObject = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonObject.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = 18f;
        label.fontStyle = FontStyles.Bold;
        label.color = FooterLabelIdleColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        persistenceActionButtons.Add(button);
        persistenceActionImages.Add(buttonImage);
        persistenceActionLabels.Add(label);
    }

    private static string BuildShoppingActionsSignature(List<TurnStateManager.HelperShoppingLine> lines)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            TurnStateManager.HelperShoppingLine line = lines[i];
            if (line != null)
                sb.Append(line.index).Append('|').Append(line.unitName).Append('|').Append(line.cost).Append(';');
        }
        return sb.ToString();
    }

    private void RebuildShoppingActionButtons(List<TurnStateManager.HelperShoppingLine> lines)
    {
        for (int i = shoppingActionButtons.Count - 1; i >= 0; i--)
            if (shoppingActionButtons[i] != null)
                Destroy(shoppingActionButtons[i].gameObject);
        shoppingActionButtons.Clear();

        for (int i = 0; i < lines.Count; i++)
        {
            TurnStateManager.HelperShoppingLine line = lines[i];
            if (line == null)
                continue;

            bool isCancel = line.isCancel;
            int optionIndex = line.index - 1;
            string objectSuffix = isCancel ? "cancel" : line.index.ToString();
            GameObject buttonObject = new GameObject($"button_shopping_{objectSuffix}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(shoppingActionsRoot.transform, false);
            buttonObject.GetComponent<Image>().color = FooterButtonIdleColor;
            LayoutElement element = buttonObject.GetComponent<LayoutElement>();
            element.minHeight = ShoppingActionButtonHeight;
            element.preferredHeight = ShoppingActionButtonHeight;

            Button button = buttonObject.GetComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            if (isCancel)
                button.onClick.AddListener(() => turnStateManager?.TryCancelShoppingFromPointer());
            else
                button.onClick.AddListener(() => turnStateManager?.TryPurchaseShoppingOptionFromPointer(optionIndex));

            GameObject labelObject = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(buttonObject.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            if (isCancel)
            {
                label.text = line.unitName;
            }
            else
            {
                string cost = line.cost.HasValue ? $" (${line.cost.Value})" : string.Empty;
                label.text = $"{line.index} - {line.unitName}{cost}";
            }
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.color = FooterLabelIdleColor;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            shoppingActionButtons.Add(button);
        }

        RectTransform rootRect = shoppingActionsRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(0f, shoppingActionButtons.Count * (ShoppingActionButtonHeight + 4f));
    }

    private static string BuildSensorActionsSignature(List<TurnStateManager.HelperSensorLine> lines)
    {
        StringBuilder sb = new StringBuilder(lines != null ? lines.Count : 0);
        if (lines != null)
        {
            for (int i = 0; i < lines.Count; i++)
                if (lines[i] != null)
                    sb.Append(lines[i].actionCode);
        }
        return sb.ToString();
    }

    private void RebuildSensorActionButtons(List<TurnStateManager.HelperSensorLine> lines)
    {
        for (int i = sensorActionButtons.Count - 1; i >= 0; i--)
            if (sensorActionButtons[i] != null)
                Destroy(sensorActionButtons[i].gameObject);
        sensorActionButtons.Clear();
        sensorActionButtonCodes.Clear();

        for (int i = 0; i < lines.Count; i++)
        {
            TurnStateManager.HelperSensorLine line = lines[i];
            if (line == null)
                continue;

            char actionCode = line.actionCode;
            GameObject buttonObject = new GameObject($"button_action_{actionCode}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(sensorActionsRoot.transform, false);
            buttonObject.GetComponent<Image>().color = new Color(0.04f, 0.12f, 0.06f, 0.92f);
            LayoutElement element = buttonObject.GetComponent<LayoutElement>();
            element.minHeight = SensorActionButtonHeight;
            element.preferredHeight = SensorActionButtonHeight;

            Button button = buttonObject.GetComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(() =>
            {
                turnStateManager?.SetSensorOptionFocus(actionCode);
                turnStateManager?.TryInvokeSensorActionFromPointer(actionCode);
            });

            GameObject labelObject = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(buttonObject.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.text = $"{actionCode} - {ResolveSensorLabel(line.sensorKey)}";
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.65f, 1f, 0.65f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            sensorActionButtons.Add(button);
            sensorActionButtonCodes.Add(actionCode);
        }

        RectTransform rootRect = sensorActionsRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(0f, sensorActionButtons.Count * (SensorActionButtonHeight + 4f));
    }

    private void EnsureCancelControl()
    {
        if (!Application.isPlaying || cancelControlRoot != null || helperRect == null)
            return;

        cancelControlRoot = new GameObject("helper_cancel_control", typeof(RectTransform));
        RectTransform rootRect = cancelControlRoot.GetComponent<RectTransform>();
        rootRect.SetParent(helperRect, false);
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(0f, CancelControlHeight);
        rootRect.SetAsLastSibling();

        GameObject buttonObject = new GameObject("button_cancel_action", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(rootRect, false);
        buttonRect.anchorMin = new Vector2(0.08f, 0f);
        buttonRect.anchorMax = new Vector2(0.92f, 1f);
        buttonRect.offsetMin = new Vector2(4f, 5f);
        buttonRect.offsetMax = new Vector2(-4f, -5f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = FooterButtonIdleColor;
        cancelActionImage = image;
        cancelActionButton = buttonObject.GetComponent<Button>();
        Navigation navigation = cancelActionButton.navigation;
        navigation.mode = Navigation.Mode.None;
        cancelActionButton.navigation = navigation;

        GameObject labelObject = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = "CANCELAR";
        label.fontSize = 20f;
        label.fontStyle = FontStyles.Bold;
        label.color = FooterLabelIdleColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        cancelActionLabel = label;

        cancelActionButton.onClick.AddListener(() =>
        {
            if (turnStateManager != null)
            {
                turnStateManager.SetCommandServicePreviewFocus(1);
                turnStateManager.SetRemovingUnitFocus(1);
            }
            cursorController?.TryCancelCurrentActionFromPointer();
        });
        cancelControlRoot.SetActive(false);
    }

    private void EnsureExecuteCommandServiceControl()
    {
        if (!Application.isPlaying || executeCommandServiceControlRoot != null || helperRect == null)
            return;

        executeCommandServiceControlRoot = new GameObject("helper_execute_command_service_control", typeof(RectTransform));
        RectTransform rootRect = executeCommandServiceControlRoot.GetComponent<RectTransform>();
        rootRect.SetParent(helperRect, false);
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, CancelControlHeight);
        rootRect.sizeDelta = new Vector2(0f, ExecuteCommandServiceControlHeight);
        rootRect.SetAsLastSibling();

        GameObject buttonObject = new GameObject("button_execute_command_service", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(rootRect, false);
        buttonRect.anchorMin = new Vector2(0.08f, 0f);
        buttonRect.anchorMax = new Vector2(0.92f, 1f);
        buttonRect.offsetMin = new Vector2(4f, 5f);
        buttonRect.offsetMax = new Vector2(-4f, -5f);

        executeCommandServiceImage = buttonObject.GetComponent<Image>();
        executeCommandServiceImage.color = FooterButtonIdleColor;
        executeCommandServiceButton = buttonObject.GetComponent<Button>();
        Navigation navigation = executeCommandServiceButton.navigation;
        navigation.mode = Navigation.Mode.None;
        executeCommandServiceButton.navigation = navigation;

        GameObject labelObject = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = "EXECUTAR";
        label.fontSize = 20f;
        label.fontStyle = FontStyles.Bold;
        label.color = FooterLabelIdleColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        executeCommandServiceLabel = label;

        executeCommandServiceButton.onClick.AddListener(() =>
        {
            if (turnStateManager != null &&
                turnStateManager.CurrentCursorState == TurnStateManager.CursorState.CommandService)
                turnStateManager.SetCommandServicePreviewFocus(0);
            else if (turnStateManager != null &&
                     turnStateManager.CurrentCursorState == TurnStateManager.CursorState.RemovingUnit)
                turnStateManager.SetRemovingUnitFocus(0);
            turnStateManager?.HandleConfirmWithFeedback();
        });
        executeCommandServiceControlRoot.SetActive(false);
    }

    // Destaca o botao em foco durante o preview do Servico do Comando (EXECUTAR x CANCELAR).
    // Fora desse estado, ambos voltam ao visual neutro (o botao Cancelar aparece sozinho em varios
    // outros estados e nao deve ficar destacado).
    private void RefreshCommandServicePreviewFocusHighlight(bool panelVisible)
    {
        bool commandService = panelVisible && turnStateManager != null &&
                              turnStateManager.CurrentCursorState == TurnStateManager.CursorState.CommandService;
        bool removingUnit = panelVisible && turnStateManager != null &&
                            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.RemovingUnit;
        int focus = commandService
            ? turnStateManager.CommandServicePreviewFocusIndex
            : removingUnit ? turnStateManager.RemovingUnitFocusIndex : -1;

        ApplyFooterButtonFocus(executeCommandServiceImage, executeCommandServiceLabel, focus == 0);
        ApplyFooterButtonFocus(cancelActionImage, cancelActionLabel, focus == 1);
    }

    private void ApplyFooterButtonFocus(Image image, TMP_Text label, bool focused)
    {
        if (image != null)
            image.color = TeamButtonBackground(currentTeamColor, focused);
        if (label != null)
            label.color = TeamButtonLabel(currentTeamColor, focused);
    }

    private void RefreshExecuteCommandServiceControl(bool panelVisible)
    {
        if (executeCommandServiceControlRoot == null)
            return;

        bool commandService = turnStateManager != null &&
                              turnStateManager.CurrentCursorState == TurnStateManager.CursorState.CommandService;
        bool removingUnit = turnStateManager != null &&
                            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.RemovingUnit;
        bool aiming = turnStateManager != null &&
                      turnStateManager.IsMirandoConfirmStep;
        bool active = panelVisible && (commandService || removingUnit || aiming);
        if (executeCommandServiceControlRoot.activeSelf != active)
            executeCommandServiceControlRoot.SetActive(active);

        if (executeCommandServiceButton != null)
            executeCommandServiceButton.interactable = active;
        if (executeCommandServiceLabel != null)
            executeCommandServiceLabel.text = (removingUnit || aiming) ? "CONFIRMAR" : "EXECUTAR";

        if (active && panelHelper == gameObject && selfPanelCanvasGroup != null)
        {
            selfPanelCanvasGroup.interactable = true;
            selfPanelCanvasGroup.blocksRaycasts = true;
        }
    }

    private float GetActiveFooterHeight()
    {
        float height = 0f;
        if (cancelControlRoot != null && cancelControlRoot.activeSelf)
            height += CancelControlHeight;
        if (executeCommandServiceControlRoot != null && executeCommandServiceControlRoot.activeSelf)
            height += ExecuteCommandServiceControlHeight;
        return height;
    }

    private void RefreshCancelControl(bool panelVisible)
    {
        if (cancelControlRoot == null)
            return;

        bool active = panelVisible && CanCancelCurrentStateFromHelper();
        if (cancelControlRoot.activeSelf != active)
            cancelControlRoot.SetActive(active);

        if (panelHelper == gameObject && selfPanelCanvasGroup != null)
        {
            selfPanelCanvasGroup.interactable = active;
            selfPanelCanvasGroup.blocksRaycasts = active;
        }
    }

    private bool CanCancelCurrentStateFromHelper()
    {
        if (turnStateManager == null)
            return false;

        switch (turnStateManager.CurrentCursorState)
        {
            case TurnStateManager.CursorState.Neutral:
            case TurnStateManager.CursorState.ShoppingAndServices:
            case TurnStateManager.CursorState.PlayerMenu:
            case TurnStateManager.CursorState.Replay:
            case TurnStateManager.CursorState.Saving:
            case TurnStateManager.CursorState.Loading:
            case TurnStateManager.CursorState.CommandServiceExecuting:
            case TurnStateManager.CursorState.RemovingUnitExecuting:
            case TurnStateManager.CursorState.EndingTurnExecuting:
            case TurnStateManager.CursorState.AircraftFuelDepletionQueue:
            case TurnStateManager.CursorState.TurnStartRallyQueue:
                return false;
            case TurnStateManager.CursorState.Mirando:
                // No passo de escolher alvo o CANCELAR fica na propria lista (como no shopping);
                // o rodape so aparece no passo de confirmar o ataque (CONFIRMAR/CANCELAR).
                return turnStateManager.IsMirandoConfirmStep;
            default:
                return true;
        }
    }

    private Color ResolveActiveTeamColor(TurnStateManager.HelperPanelData data)
    {
        TeamId team = TeamId.Neutral;
        if (data != null && data.SubjectTeamId != int.MinValue)
            team = (TeamId)data.SubjectTeamId;
        else if (matchController != null)
            team = matchController.ActiveTeam;

        return TeamUtils.GetColor(team);
    }

    public static bool TrySetExternalText(string title, string body)
    {
        if (instance == null)
            return false;

        instance.SetExternalText(title, body, 0f, timed: false);
        return true;
    }

    public static string ResolveHelperMessage(string id, string fallback)
    {
        if (instance == null)
            return fallback ?? string.Empty;

        return instance.ResolveMessage(id, fallback);
    }

    public static string ResolveHelperMessage(string id, string fallback, IReadOnlyDictionary<string, string> tokens)
    {
        if (instance == null)
            return ApplyInlineTokens(fallback ?? string.Empty, tokens);

        return instance.ResolveMessage(id, fallback, tokens);
    }

    public static bool IsDockedCenterLeft()
    {
        if (instance == null)
            return false;

        return instance.isDockedCenterLeft &&
               instance.panelHelper != null &&
               instance.panelHelper.activeInHierarchy;
    }

    public static bool IsCursorNearOriginalDockRegion()
    {
        if (instance == null)
            return false;

        return instance.cursorNearUndockedDockRegion;
    }

    public static bool TrySetTransientText(string title, string body, float durationSeconds = 2.4f)
    {
        if (instance == null)
            return false;

        instance.SetExternalText(title, body, Mathf.Max(0.05f, durationSeconds), timed: true);
        return true;
    }

    public static void ClearExternalText()
    {
        if (instance == null)
            return;

        instance.hasExternalOverrideText = false;
        instance.externalOverrideTitle = string.Empty;
        instance.externalOverrideBody = string.Empty;
        instance.externalOverrideUntilUnscaledTime = -1f;
    }

    private void SetExternalText(string title, string body, float durationSeconds, bool timed)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
        {
            hasExternalOverrideText = false;
            externalOverrideTitle = string.Empty;
            externalOverrideBody = string.Empty;
            externalOverrideUntilUnscaledTime = -1f;
            return;
        }

        hasExternalOverrideText = true;
        externalOverrideTitle = title ?? string.Empty;
        externalOverrideBody = body ?? string.Empty;
        externalOverrideUntilUnscaledTime = timed ? Time.unscaledTime + Mathf.Max(0.05f, durationSeconds) : -1f;
    }

    private void RefreshDockByCursorProximity()
    {
        if (helperRect == null || cursorController == null || panelHelper == null)
        {
            cursorNearUndockedDockRegion = false;
            return;
        }
        if (!panelHelper.activeInHierarchy)
        {
            cursorNearUndockedDockRegion = false;
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            cursorNearUndockedDockRegion = false;
            return;
        }

        Vector3 cursorWorld = cursorController.transform.position;
        Vector3 cursorScreen = cam.WorldToScreenPoint(cursorWorld);
        if (cursorScreen.z < 0f)
        {
            cursorNearUndockedDockRegion = false;
            return;
        }

        if (!isDockedCenterLeft)
        {
            Rect panelScreenRect = GetScreenRect(helperRect);
            if (panelScreenRect.width <= 0f || panelScreenRect.height <= 0f)
            {
                cursorNearUndockedDockRegion = false;
                return;
            }

            lastUndockedScreenRect = panelScreenRect;
            hasLastUndockedScreenRect = true;
            cursorNearUndockedDockRegion = IsNearRect(panelScreenRect, cursorScreen, dockEnterProximityPixels);

            if (cursorNearUndockedDockRegion)
                ApplyDockCenterLeft();
            return;
        }

        if (!hasLastUndockedScreenRect)
        {
            cursorNearUndockedDockRegion = false;
            return;
        }

        cursorNearUndockedDockRegion = IsNearRect(lastUndockedScreenRect, cursorScreen, dockExitProximityPixels);
        if (!cursorNearUndockedDockRegion)
            RestoreOriginalLayout();
    }

    private static bool IsNearRect(Rect rect, Vector3 screenPoint, float marginPixels)
    {
        float margin = Mathf.Max(0f, marginPixels);
        Rect expanded = new Rect(
            rect.xMin - margin,
            rect.yMin - margin,
            rect.width + margin * 2f,
            rect.height + margin * 2f);

        return expanded.Contains(new Vector2(screenPoint.x, screenPoint.y));
    }

    private void CacheOriginalLayoutIfNeeded()
    {
        if (layoutCached || helperRect == null)
            return;

        originalAnchorMin = helperRect.anchorMin;
        originalAnchorMax = helperRect.anchorMax;
        originalPivot = helperRect.pivot;
        originalAnchoredPosition = helperRect.anchoredPosition;
        cachedBasePanelHeight = Mathf.Max(0f, helperRect.rect.height);
        if (helperTitle != null)
        {
            helperTitleRect = helperTitle.rectTransform;
            originalHelperTitleAnchoredPosition = helperTitleRect.anchoredPosition;
            originalHelperTitleHeight = Mathf.Max(0f, helperTitleRect.rect.height);
        }
        if (helperTxt != null)
        {
            helperTxtRect = helperTxt.rectTransform;
            originalHelperTxtAnchoredPosition = helperTxtRect.anchoredPosition;
            originalHelperTxtHeight = Mathf.Max(0f, helperTxtRect.rect.height);
            float titleBottom = -originalHelperTitleAnchoredPosition.y + Mathf.Max(0f, originalHelperTitleHeight);
            float bodyTop = -originalHelperTxtAnchoredPosition.y;
            originalBodySpacingFromTitle = Mathf.Max(0f, bodyTop - titleBottom);
        }
        layoutCached = true;
    }

    private void ApplyDockCenterLeft()
    {
        if (helperRect == null)
            return;

        CacheOriginalLayoutIfNeeded();
        helperRect.anchorMin = new Vector2(0f, 0.5f);
        helperRect.anchorMax = new Vector2(0f, 0.5f);
        helperRect.pivot = new Vector2(0f, 0.5f);
        helperRect.anchoredPosition = dockedAnchoredPosition;
        isDockedCenterLeft = true;
    }

    private void RestoreOriginalLayout()
    {
        if (helperRect == null || !layoutCached)
            return;

        helperRect.anchorMin = originalAnchorMin;
        helperRect.anchorMax = originalAnchorMax;
        helperRect.pivot = originalPivot;
        helperRect.anchoredPosition = originalAnchoredPosition;
        isDockedCenterLeft = false;
    }

    private static Rect GetScreenRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < 4; i++)
        {
            Vector3 c = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
            if (c.x < minX) minX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.x > maxX) maxX = c.x;
            if (c.y > maxY) maxY = c.y;
        }

        if (!float.IsFinite(minX) || !float.IsFinite(minY) || !float.IsFinite(maxX) || !float.IsFinite(maxY))
            return new Rect();

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelHelper == null)
            return;

        if (panelHelper == gameObject)
        {
            if (selfPanelCanvasGroup == null)
                selfPanelCanvasGroup = panelHelper.GetComponent<CanvasGroup>();
            if (selfPanelCanvasGroup == null)
                selfPanelCanvasGroup = panelHelper.AddComponent<CanvasGroup>();

            selfPanelCanvasGroup.alpha = visible ? 1f : 0f;
            selfPanelCanvasGroup.interactable = false;
            selfPanelCanvasGroup.blocksRaycasts = false;
            return;
        }

        if (panelHelper.activeSelf != visible)
            panelHelper.SetActive(visible);
    }

    private void RefreshHelperScrollLayout(float titleHeight, float bodyHeight, float panelHeight)
    {
        if (helperRect == null || helperTitle == null || helperTxt == null)
            return;

        EnsureHelperMask();
        CacheOriginalLayoutIfNeeded();
        helperTitleRect = helperTitle.rectTransform;
        helperTxtRect = helperTxt.rectTransform;
        if (helperTitleRect == null || helperTxtRect == null)
            return;

        float targetTitleHeight = Mathf.Max(originalHelperTitleHeight > 0f ? originalHelperTitleHeight : 0f, titleHeight);
        float targetBodyHeight = Mathf.Max(originalHelperTxtHeight > 0f ? originalHelperTxtHeight : 0f, bodyHeight);
        helperTitleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetTitleHeight);
        helperTxtRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetBodyHeight);

        float titleTopInset = Mathf.Max(0f, -originalHelperTitleAnchoredPosition.y);
        float combinedContentHeight = targetTitleHeight + originalBodySpacingFromTitle + targetBodyHeight;
        float footerHeight = GetActiveFooterHeight();
        float viewportHeight = Mathf.Max(1f, panelHeight - titleTopInset - footerHeight);
        helperScrollMaxOffset = Mathf.Max(0f, combinedContentHeight - viewportHeight);
        helperScrollActive = helperScrollMaxOffset > 0.5f;
        helperScrollOffset = Mathf.Clamp(helperScrollOffset, 0f, helperScrollMaxOffset);
        if (!helperScrollActive)
            helperScrollOffset = 0f;

        Vector2 scrollOffset = new Vector2(0f, helperScrollOffset);
        Vector2 bodyBasePosition = new Vector2(
            originalHelperTxtAnchoredPosition.x,
            originalHelperTitleAnchoredPosition.y - targetTitleHeight - originalBodySpacingFromTitle);
        helperTitleRect.anchoredPosition = originalHelperTitleAnchoredPosition + scrollOffset;
        helperTxtRect.anchoredPosition = bodyBasePosition + scrollOffset;
    }

    private void ResetHelperScrollLayout()
    {
        helperScrollOffset = 0f;
        helperScrollMaxOffset = 0f;
        helperScrollActive = false;

        if (helperTitle != null)
        {
            helperTitleRect = helperTitle.rectTransform;
            if (helperTitleRect != null)
            {
                if (originalHelperTitleHeight > 0f)
                    helperTitleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, originalHelperTitleHeight);
                helperTitleRect.anchoredPosition = originalHelperTitleAnchoredPosition;
            }
        }

        if (helperTxt == null)
            return;

        helperTxtRect = helperTxt.rectTransform;
        if (helperTxtRect == null)
            return;

        if (originalHelperTxtHeight > 0f)
            helperTxtRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, originalHelperTxtHeight);
        helperTxtRect.anchoredPosition = originalHelperTxtAnchoredPosition;
    }

    private void EnsureHelperMask()
    {
        if (panelHelper == null)
            return;

        if (helperMask == null)
            helperMask = panelHelper.GetComponent<RectMask2D>();
        if (helperMask == null)
            helperMask = panelHelper.AddComponent<RectMask2D>();
    }

    private void HandleHelperScrollInput()
    {
        if (!helperScrollActive || !lastPanelVisible || helperRect == null)
            return;

        Vector2 scrollDelta = ReadMouseScrollDelta();
        if (Mathf.Abs(scrollDelta.y) <= 0.01f)
            return;

        Rect panelScreenRect = GetScreenRect(helperRect);
        Vector3 mouseScreen = ReadMouseScreenPosition();
        if (!panelScreenRect.Contains(new Vector2(mouseScreen.x, mouseScreen.y)))
            return;

        helperScrollOffset = Mathf.Clamp(
            helperScrollOffset - scrollDelta.y * Mathf.Max(1f, helperScrollStep),
            0f,
            helperScrollMaxOffset);

        if (helperTitle != null)
        {
            helperTitleRect = helperTitle.rectTransform;
            if (helperTitleRect != null)
                helperTitleRect.anchoredPosition = originalHelperTitleAnchoredPosition + new Vector2(0f, helperScrollOffset);
        }

        if (helperTxt != null)
        {
            helperTxtRect = helperTxt.rectTransform;
            if (helperTxtRect != null)
            {
                Vector2 bodyBasePosition = new Vector2(
                    originalHelperTxtAnchoredPosition.x,
                    originalHelperTitleAnchoredPosition.y - Mathf.Max(0f, helperTitleRect != null ? helperTitleRect.rect.height : originalHelperTitleHeight) - originalBodySpacingFromTitle);
                helperTxtRect.anchoredPosition = bodyBasePosition + new Vector2(0f, helperScrollOffset);
            }
        }
    }

    private static Vector2 ReadMouseScrollDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            return Mouse.current.scroll.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mouseScrollDelta;
#else
        return Vector2.zero;
#endif
    }

    private static Vector3 ReadMouseScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector3.zero;
#endif
    }

    private void HandleCoordinateOverlayHotkeys()
    {
        if (UiInputBlocker.IsTextInputFocused())
            return;

        if (WasCoordinateOverlayTogglePressedThisFrame())
        {
            showCoordinateOverlay = !showCoordinateOverlay;
            string state = showCoordinateOverlay ? "ON" : "OFF";
            PanelDialogController.TrySetTransientText($"Coordinate Overlay: {state}", 1.6f);
        }
    }

    private void RefreshCoordinateOverlayState()
    {
        coordinateOverlayLabels.Clear();
        upkeepEventCells.Clear();

        if (!showCoordinateOverlay || cursorController == null)
            return;

        Vector3Int cursorCell = cursorController.CurrentCell;
        cursorCell.z = 0;
        coordinateOverlayLabels.Add(new CoordinateOverlayLabel
        {
            cell = cursorCell,
            text = $"CUR {FormatMapCell(cursorCell)}",
            color = cursorCoordinateColor
        });

        if (turnStateManager != null && turnStateManager.SelectedUnit != null)
        {
            Vector3Int selectedCell = turnStateManager.SelectedUnit.CurrentCellPosition;
            selectedCell.z = 0;
            coordinateOverlayLabels.Add(new CoordinateOverlayLabel
            {
                cell = selectedCell,
                text = $"SEL {FormatMapCell(selectedCell)}",
                color = selectedCoordinateColor
            });
        }

        if (turnStateManager == null || !turnStateManager.TryBuildHelperPanelData(out TurnStateManager.HelperPanelData data))
            return;
        if (data == null || data.Kind != TurnStateManager.HelperPanelKind.TurnStartAutonomy || data.TurnStartAutonomyLines == null)
            return;

        int signature = 17;
        for (int i = 0; i < data.TurnStartAutonomyLines.Count; i++)
        {
            TurnStateManager.HelperTurnStartAutonomyLine line = data.TurnStartAutonomyLines[i];
            if (line == null)
                continue;

            Vector3Int cell = line.cell;
            cell.z = 0;
            signature = signature * 31 + cell.x;
            signature = signature * 31 + cell.y;
            if (!upkeepEventCells.Contains(cell))
                upkeepEventCells.Add(cell);

            coordinateOverlayLabels.Add(new CoordinateOverlayLabel
            {
                cell = cell,
                text = $"EV {FormatMapCell(cell)}",
                color = eventCoordinateColor
            });
        }

        if (signature != lastUpkeepSignature)
        {
            lastUpkeepSignature = signature;
            for (int i = 0; i < upkeepEventCells.Count; i++)
                HighlightEventCell(upkeepEventCells[i]);
        }
    }

    private void HighlightEventCell(Vector3Int cell)
    {
        float duration = animationManager != null
            ? animationManager.TurnStartAutonomyHelperBangDuration
            : Mathf.Max(0.1f, eventHighlightSeconds);
        float expiresAt = Time.unscaledTime + Mathf.Max(0.1f, duration);
        highlightedEventCells[cell] = expiresAt;
    }

    private void DrawCoordinateLabel(Camera cam, Tilemap tilemap, Vector3Int cell, string text, Color color, bool isHighlight)
    {
        if (cam == null || tilemap == null || string.IsNullOrWhiteSpace(text))
            return;

        Vector3 world = tilemap.GetCellCenterWorld(cell);
        world.y += coordinateLabelWorldYOffset;
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z <= 0f)
            return;

        float regularWidth = cursorController != null
            ? Mathf.Clamp(cursorController.CoordinateOverlayLabelWidth, 60f, 400f)
            : 220f;
        float width = isHighlight ? 24f : regularWidth;
        float height = isHighlight ? 24f : 20f;
        Rect rect = new Rect(screen.x - width * 0.5f, Screen.height - screen.y - height * 0.5f, width, height);
        GUIStyle style = isHighlight ? coordinateOverlayHighlightStyle : coordinateOverlayLabelStyle;

        Color previous = GUI.color;
        GUI.color = color;
        GUI.Label(rect, text, style);
        GUI.color = previous;
    }

    private static string FormatMapCell(Vector3Int cell)
    {
        return $"C{cell.x},L{cell.y}";
    }

    private void EnsureCoordinateOverlayStyles()
    {
        if (coordinateOverlayLabelStyle == null)
        {
            coordinateOverlayLabelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                richText = false,
                normal = { textColor = Color.white }
            };
            coordinateOverlayLabelStyle.padding = new RectOffset(4, 4, 1, 1);
        }

        if (coordinateOverlayHighlightStyle != null)
            return;

        coordinateOverlayHighlightStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        coordinateOverlayHighlightStyle.padding = new RectOffset(0, 0, 0, 0);
    }

    private bool WasCoordinateOverlayTogglePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (WasKeyPressedThisFrame(toggleCoordinateOverlayKey))
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(toggleCoordinateOverlayKey);
#else
        return false;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static bool WasKeyPressedThisFrame(KeyCode keyCode)
    {
        if (Keyboard.current == null)
            return false;

        switch (keyCode)
        {
            case KeyCode.F1: return Keyboard.current.f1Key.wasPressedThisFrame;
            case KeyCode.F2: return Keyboard.current.f2Key.wasPressedThisFrame;
            case KeyCode.F3: return Keyboard.current.f3Key.wasPressedThisFrame;
            case KeyCode.F4: return Keyboard.current.f4Key.wasPressedThisFrame;
            case KeyCode.F5: return Keyboard.current.f5Key.wasPressedThisFrame;
            case KeyCode.F6: return Keyboard.current.f6Key.wasPressedThisFrame;
            case KeyCode.F7: return Keyboard.current.f7Key.wasPressedThisFrame;
            case KeyCode.F8: return Keyboard.current.f8Key.wasPressedThisFrame;
            case KeyCode.F9: return Keyboard.current.f9Key.wasPressedThisFrame;
            case KeyCode.F10: return Keyboard.current.f10Key.wasPressedThisFrame;
            case KeyCode.F11: return Keyboard.current.f11Key.wasPressedThisFrame;
            case KeyCode.F12: return false; // reserved — AI Resume (DebugManager)
            default: return false;
        }
    }
#endif

    private GameObject FindNamedObject(string name)
    {
        Transform local = FindChildRecursive(transform, name);
        if (local != null)
            return local.gameObject;

        GameObject global = GameObject.Find(name);
        return global;
    }

    private TMP_Text FindNamedTmpText(string name)
    {
        Transform local = FindChildRecursive(transform, name);
        if (local != null)
            return local.GetComponent<TMP_Text>();

        GameObject global = GameObject.Find(name);
        return global != null ? global.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

#if UNITY_EDITOR
    private static T FindFirstAssetEditor<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
        }

        return null;
    }
#endif
}
