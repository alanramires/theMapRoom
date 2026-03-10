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
    private Color lastHelperTxtColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
    private float cachedBasePanelHeight = -1f;
    private float helperScrollOffset;
    private float helperScrollMaxOffset;
    private bool helperScrollActive;
    [SerializeField] [Range(1f, 80f)] private float helperScrollStep = 24f;

    [Header("Coordinate Overlay")]
    [SerializeField] private bool showCoordinateOverlay = true;
    [SerializeField] private KeyCode toggleCoordinateOverlayKey = KeyCode.F3;
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

    private void Awake()
    {
        TryAutoAssignReferences();
        CacheOriginalLayoutIfNeeded();
        HideAll(force: true);
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

#if UNITY_EDITOR
        if (helperDatabase == null)
            helperDatabase = FindFirstAssetEditor<HelperDatabase>();
#endif
    }

    private void Refresh(bool force)
    {
        if (turnStateManager == null || !turnStateManager.TryBuildHelperPanelData(out TurnStateManager.HelperPanelData data))
        {
            HideAll(force);
            return;
        }

        BuildHelperText(data, out string title, out string body);

        SetVisible(panelVisible: true, title: title, body: body, force: force);
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
                title = ResolveMessage("helper.title.shopping", "SHOPPING");
                body = BuildShoppingBody(data);
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
            string line = data.UnitStatsLines[i] ?? string.Empty;
            if (i > 0)
                sb.AppendLine();
            sb.Append(line);
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
            sb.AppendLine($"{unitName} {line.cell.x},{line.cell.y} Fuel {fuelBefore} - {consumed} = {fuelAfter}");
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
                sb.Append(ResolveMessage(
                    "helper.shopping.line.with_cost",
                    "<index> - <unit> ($<valor>)",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty },
                        { "valor", line.cost.Value.ToString() }
                    }));
            }
            else
            {
                sb.Append(ResolveMessage(
                    "helper.shopping.line.no_cost",
                    "<index> - <unit>",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty }
                    }));
            }
        }

        return sb.ToString();
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
        sb.AppendLine(ResolveMessage("helper.merge.section.queue", "Queue"));

        if (data.MergeQueueLines != null && data.MergeQueueLines.Count > 0)
        {
            for (int i = 0; i < data.MergeQueueLines.Count; i++)
            {
                TurnStateManager.HelperMergeQueueLine line = data.MergeQueueLines[i];
                if (line == null)
                    continue;

                sb.AppendLine(ResolveMessage(
                    "helper.merge.queue.line",
                    "<index> - <unit> (<stats>)",
                    new Dictionary<string, string>
                    {
                        { "index", line.index.ToString() },
                        { "unit", line.unitName ?? string.Empty },
                        { "stats", line.stats ?? string.Empty }
                    }));
            }
        }
        else
        {
            sb.AppendLine(ResolveMessage("helper.merge.queue.empty", "0 - (empty)"));
        }

        sb.AppendLine();
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
        else if (data.MergeQueueLines != null && data.MergeQueueLines.Count > 0)
        {
            sb.AppendLine();
            sb.Append(ResolveMessage(
                "helper.merge.process_order.line",
                "0 - Process Queue | <preview>",
                new Dictionary<string, string>
                {
                    { "preview", data.MergeQueuePreview ?? string.Empty }
                }));
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
                sb.AppendLine($"{prefix}{line.unitName}");
                sb.AppendLine($"({line.gainsLabel})");
            }
        }

        if (data.CommandServiceIsEstimate && data.CommandServiceSkippedUnitLines != null && data.CommandServiceSkippedUnitLines.Count > 0)
        {
            sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
            sb.AppendLine($"Unidades nao atendidas: {data.CommandServiceSkippedUnitLines.Count}");
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
            sb.Append(ResolveMessage(
                data.CommandServiceIsEstimate ? "helper.command_service.economy_stop.estimate" : "helper.command_service.economy_stop",
                data.CommandServiceIsEstimate ? "Fila vai parar por saldo" : "Fila interrompida por saldo"));
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
        sb.AppendLine(ResolveMessage(
            "helper.supply.gains",
            "Ganhos: HP +<hp> | FUEL +<fuel> | AMMO +<ammo>",
            new Dictionary<string, string>
            {
                { "hp", Mathf.Max(0, data.SupplyRecoveredHp).ToString() },
                { "fuel", Mathf.Max(0, data.SupplyRecoveredFuel).ToString() },
                { "ammo", Mathf.Max(0, data.SupplyRecoveredAmmo).ToString() }
            }));
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
                    "<supply>: <before> -> <after>",
                    new Dictionary<string, string>
                    {
                        { "supply", line.supplyName ?? "Supply" },
                        { "before", Mathf.Max(0, line.beforeAmount).ToString() },
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
                sb.AppendLine($"{prefix}{line.label}");
            }

            if (data.TransferHasCursorOption)
            {
                string prefix = data.TransferCursorOptionFocused ? ">> " : string.Empty;
                int index = data.TransferCandidateLines.Count + 1;
                sb.AppendLine($"{prefix}{index}. Selecionar pelo cursor");
            }

            sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
            sb.Append(ResolveMessage("helper.transfer.select.hint", "Enter - confirmar destino | ESC - cancelar"));
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine(ResolveMessage("helper.transfer.section.confirm", "Confirmar Transferencia"));
        if (!string.IsNullOrWhiteSpace(data.TransferSelectedLabel))
            sb.AppendLine(data.TransferSelectedLabel);
        sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));

        if (data.TransferResourceLines != null && data.TransferResourceLines.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(data.TransferSourceLabel))
            {
                sb.AppendLine($"Fornecedor: {data.TransferSourceLabel}");
                for (int i = 0; i < data.TransferResourceLines.Count; i++)
                {
                    TurnStateManager.HelperTransferResourceLine line = data.TransferResourceLines[i];
                    if (line == null)
                        continue;

                    // Em origem infinita, mostra somente o volume enviado (evita int.MaxValue no helper).
                    if (line.sourceIsInfinite)
                        sb.AppendLine($"- {line.supplyName}: {Mathf.Max(0, line.movedAmount)}");
                    else
                        sb.AppendLine($"- {line.supplyName}: {Mathf.Max(0, line.sourceBefore)} - {Mathf.Max(0, line.movedAmount)} -> {Mathf.Max(0, line.sourceAfter)}");
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(data.TransferDestinationLabel))
                sb.AppendLine($"Destino: {data.TransferDestinationLabel}");

            for (int i = 0; i < data.TransferResourceLines.Count; i++)
            {
                TurnStateManager.HelperTransferResourceLine line = data.TransferResourceLines[i];
                if (line == null)
                    continue;

                string dstBefore = line.destinationBefore >= int.MaxValue ? "INF" : Mathf.Max(0, line.destinationBefore).ToString();
                string dstAfter = line.destinationAfter >= int.MaxValue ? "INF" : Mathf.Max(0, line.destinationAfter).ToString();
                sb.AppendLine($"- {line.supplyName}: {dstBefore} + {Mathf.Max(0, line.movedAmount)} -> {dstAfter}");
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

            output = output.Replace($"<{pair.Key.Trim()}>", pair.Value ?? string.Empty);
        }

        return output;
    }

    private void HideAll(bool force)
    {
        if (isDockedCenterLeft)
            RestoreOriginalLayout();
        hasLastUndockedScreenRect = false;
        ResetHelperScrollLayout();
        SetVisible(panelVisible: false, title: string.Empty, body: string.Empty, force: force);
    }

    private void SetVisible(bool panelVisible, string title, string body, bool force)
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
            Color txtColor = ResolveActiveTeamColor();
            if (force || txtColor != lastHelperTxtColor)
            {
                helperTxt.color = txtColor;
                lastHelperTxtColor = txtColor;
            }
            helperTxt.enabled = panelVisible;
        }

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
        if (helperTxt != null)
        {
            helperTxt.ForceMeshUpdate();
            bodyHeight = Mathf.Max(0f, helperTxt.preferredHeight);
        }

        float baseMin = cachedBasePanelHeight > 0f ? cachedBasePanelHeight : 0f;
        float minHeight = Mathf.Max(minPanelHeight, baseMin);
        float maxHeight = Mathf.Max(minHeight, maxPanelHeight);
        float targetHeight = Mathf.Clamp(titleHeight + bodyHeight + Mathf.Max(0f, contentVerticalPadding), minHeight, maxHeight);
        helperRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        RefreshHelperScrollLayout(titleHeight, bodyHeight, targetHeight);
    }

    private Color ResolveActiveTeamColor()
    {
        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        return TeamUtils.GetColor(activeTeam);
    }

    private void RefreshDockByCursorProximity()
    {
        if (helperRect == null || cursorController == null || panelHelper == null)
            return;
        if (!panelHelper.activeInHierarchy)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 cursorWorld = cursorController.transform.position;
        Vector3 cursorScreen = cam.WorldToScreenPoint(cursorWorld);
        if (cursorScreen.z < 0f)
            return;

        if (!isDockedCenterLeft)
        {
            Rect panelScreenRect = GetScreenRect(helperRect);
            if (panelScreenRect.width <= 0f || panelScreenRect.height <= 0f)
                return;

            lastUndockedScreenRect = panelScreenRect;
            hasLastUndockedScreenRect = true;

            if (IsNearRect(panelScreenRect, cursorScreen, dockEnterProximityPixels))
                ApplyDockCenterLeft();
            return;
        }

        if (!hasLastUndockedScreenRect)
            return;

        if (!IsNearRect(lastUndockedScreenRect, cursorScreen, dockExitProximityPixels))
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
        float viewportHeight = Mathf.Max(1f, panelHeight - titleTopInset);
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
            text = $"CUR {cursorCell.x},{cursorCell.y}",
            color = cursorCoordinateColor
        });

        if (turnStateManager != null && turnStateManager.SelectedUnit != null)
        {
            Vector3Int selectedCell = turnStateManager.SelectedUnit.CurrentCellPosition;
            selectedCell.z = 0;
            coordinateOverlayLabels.Add(new CoordinateOverlayLabel
            {
                cell = selectedCell,
                text = $"SEL {selectedCell.x},{selectedCell.y}",
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
                text = $"EV {cell.x},{cell.y}",
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
        float expiresAt = Time.unscaledTime + Mathf.Max(0.1f, eventHighlightSeconds);
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
            case KeyCode.F12: return Keyboard.current.f12Key.wasPressedThisFrame;
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
