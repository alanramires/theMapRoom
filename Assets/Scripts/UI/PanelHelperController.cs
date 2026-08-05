using TMPro;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
    [SerializeField] private CameraController cameraController;
    [SerializeField] private SaveGameManager saveGameManager;
    [SerializeField] private MainMenuLoadPanelController mainMenuLoadPanelController;
    [SerializeField] private MainMenuTutorialPanelController mainMenuTutorialPanelController;
    [SerializeField] private PanelMenu mainMenuPanel;
    [SerializeField] private BattleMapMenuRootController battleMapMenuController;
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
    private bool externalWideMode;
    private float externalWideOriginalWidth;
    private const float ExternalWideExtraWidth = 260f;
    private RectTransform helperTitleRect;
    private RectTransform helperTxtRect;
    private RectMask2D helperMask;
    private GameObject dragHandleRoot;
    private bool manuallyPositioned;
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
    private const float CancelControlHeight = 80f;
    private GameObject executeCommandServiceControlRoot;
    private Button executeCommandServiceButton;
    private Image executeCommandServiceImage;
    private TMP_Text executeCommandServiceLabel;
    private const float ExecuteCommandServiceControlHeight = 80f;
    private GameObject keepPositionControlRoot;
    private Button keepPositionButton;
    private Image keepPositionImage;
    private TMP_Text keepPositionLabel;
    private const float KeepPositionControlHeight = 80f;
    // Faixa invisivel sobre o titulo: arrastar o painel segurando qualquer
    // ponto do titulo, nao apenas a alca do canto.
    private GameObject titleDragSurfaceRoot;
    private RectTransform titleDragSurfaceRect;
    // Viewport com mascara propria da lista de alvos da mira: a lista rola por
    // baixo do titulo fixo em vez de deslizar por cima dele.
    private GameObject aimTargetsViewportRoot;
    private RectTransform aimTargetsViewportRect;
    // Botao TROCAR UNIDADE: alias tocavel do PageUp/PageDown para hex com
    // empilhamento (ex.: aereo + terrestre). So aparece quando ha 2+ entradas
    // ciclaveis na ancora da selecao.
    private GameObject cycleSelectionControlRoot;
    private Button cycleSelectionButton;
    private Image cycleSelectionImage;
    private TMP_Text cycleSelectionLabel;
    private const float CycleSelectionControlHeight = 80f;
    // Cor de fallback dos botoes gerados via script (usada so na criacao; o tint por time
    // sobrescreve todo frame). Os botoes seguem a cor do time ativo (virou tudo slot de jogador).
    private static readonly Color FooterButtonIdleColor = new Color(0.04f, 0.12f, 0.06f, 0.92f);
    private static readonly Color FooterLabelIdleColor = new Color(0.65f, 1f, 0.65f, 1f);
    // Cor do time ativo neste frame, resolvida no refresh e aplicada a todos os botoes de script.
    private Color currentTeamColor = Color.white;
    private GameObject timeoutProgressRoot;
    private Image timeoutProgressFill;
    private RectTransform timeoutProgressFillRect;

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

    private void ConfigureMobileActionLabel(TMP_Text label)
    {
        if (label == null)
            return;

        if (helperTxt != null && helperTxt.font != null)
            label.font = helperTxt.font;
        label.fontSize = 22f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 22f;
        label.fontSizeMax = 22f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private GameObject sensorActionsRoot;
    private readonly List<Button> sensorActionButtons = new List<Button>();
    private readonly List<char> sensorActionButtonCodes = new List<char>();
    private string sensorActionsSignature = string.Empty;
    private const float SensorActionButtonHeight = 50f;
    private GameObject aimTargetsRoot;
    private readonly List<Button> aimTargetButtons = new List<Button>();
    private string aimTargetsSignature = string.Empty;
    private int aimFocusedButtonIndex = -1;
    private float aimTargetsViewportHeight;
    private const float AimTargetsMaxPanelHeight = 800f;
    // Alvo mostra 2 linhas (nome+HP / terreno), entao precisa de mais altura que os demais botoes.
    private const float AimTargetButtonHeight = 50f;
    // Seccao de detalhes do passo CONFIRMAR ATAQUE: HP + LOCAL (icone do hex + nome do terreno).
    private GameObject aimConfirmDetailsRoot;
    private Image aimConfirmTargetIcon;
    private TMP_Text aimConfirmTargetText;
    private TMP_Text aimConfirmHpText;
    private TMP_Text aimConfirmWeaponText;
    private Image aimConfirmLocalIcon;
    private TMP_Text aimConfirmLocalText;
    private const float AimConfirmDetailsHeight = 206f;
    private GameObject autonomyUpkeepRoot;
    private GameObject autonomyUpkeepViewportRoot;
    private RectTransform autonomyUpkeepViewportRect;
    private TMP_Text autonomyUpkeepTitleLabel;
    private readonly List<GameObject> autonomyUpkeepRows = new List<GameObject>();
    private string autonomyUpkeepSignature = string.Empty;
    private const float AutonomyUpkeepHeaderHeight = 34f;
    private const float AutonomyUpkeepRowHeight = 82f;
    private const float AutonomyUpkeepTierHeaderHeight = 28f;
    // Altura total do conteudo rolavel do Jornal (cabecalhos de tier + linhas),
    // computada no rebuild e reusada pelo scroll layout. O titulo fica fora.
    private float autonomyUpkeepContentHeight;
    // Rolagem propria do Jornal: um turno movimentado passa facil de dez
    // noticias, entao o painel para de crescer na altura do viewport e a lista
    // desliza dentro da mascara (roda do mouse, arraste, ou seguindo o foco).
    private float autonomyUpkeepScrollOffset;
    private float autonomyUpkeepViewportHeight;
    // Enquadramento de cada linha dentro do conteudo: o topo inclui o cabecalho
    // de tier quando a linha abre um tier (o rotulo entra junto no foco).
    private readonly List<float> autonomyUpkeepRowFrameTops = new List<float>();
    private readonly List<float> autonomyUpkeepRowBottoms = new List<float>();
    private int autonomyUpkeepFocusedRowIndex = -1;
    private const float AutonomyUpkeepMaxListHeight = 620f;
    private GameObject commandServiceRowsRoot;
    private GameObject commandServiceViewportRoot;
    private RectTransform commandServiceViewportRect;
    private TMP_Text commandServiceSummaryLabel;
    private readonly List<GameObject> commandServiceRows = new List<GameObject>();
    private string commandServiceRowsSignature = string.Empty;
    private const float CommandServiceSummaryHeight = 96f;
    private const float CommandServiceRowHeight = 72f;
    private const float CommandServiceMaxListHeight = 500f;
    private GameObject unitStatsLocalRoot;
    private GameObject unitStatsIconsRoot;
    private LayoutElement unitStatsIconsLayout;
    private Image unitStatsLocalIcon;
    private Image unitStatsStructureIcon;
    private TMP_Text unitStatsLocalText;
    private TMP_Text unitStatsDefenseText;
    private TMP_Text unitStatsConstructionStockText;
    private bool unitStatsLocalAtBottom;
    private readonly List<Image> unitStatsTransportedIcons = new List<Image>();
    private GameObject disembarkActionsRoot;
    private readonly List<Button> disembarkActionButtons = new List<Button>();
    private readonly List<int> disembarkActionFocusIndices = new List<int>();
    private string disembarkActionsSignature = string.Empty;
    private bool disembarkLayoutDirty;
    private const float DisembarkActionButtonHeight = 50f;
    private GameObject shoppingActionsRoot;
    private readonly List<Button> shoppingActionButtons = new List<Button>();
    private string shoppingActionsSignature = string.Empty;
    private const float ShoppingActionButtonHeight = 50f;
    private GameObject persistenceActionsRoot;
    private readonly List<Button> persistenceActionButtons = new List<Button>();
    private readonly List<Image> persistenceActionImages = new List<Image>();
    private readonly List<TMP_Text> persistenceActionLabels = new List<TMP_Text>();
    private readonly List<Color> persistenceActionTeamColors = new List<Color>();
    private readonly List<bool> persistenceActionUsesTeamColor = new List<bool>();
    private GameObject persistenceConfirmationDetails;
    private string persistenceActionsSignature = string.Empty;
    private const float PersistenceActionButtonHeight = 50f;
    private const float PersistenceConfirmationDetailsHeight = 190f;
    // Respiro entre as opcoes e o botao de cancelar/voltar, pra ele parecer um rodape
    // destacado (como MANTER POSICAO/CANCELAR nas telas de unidade), e nao mais um item da lista.
    private const float PersistenceFooterGap = 24f;
    private float persistenceFooterSpacerHeight;
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
        if (ShouldHideForActiveAI())
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
        if (cameraController == null)
            cameraController = FindAnyObjectByType<CameraController>();
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
        if (mainMenuLoadPanelController == null)
            mainMenuLoadPanelController = FindAnyObjectByType<MainMenuLoadPanelController>();
        if (mainMenuTutorialPanelController == null)
            mainMenuTutorialPanelController = FindAnyObjectByType<MainMenuTutorialPanelController>();
        if (mainMenuPanel == null)
            mainMenuPanel = FindAnyObjectByType<PanelMenu>();
        if (battleMapMenuController == null)
            battleMapMenuController = FindAnyObjectByType<BattleMapMenuRootController>();

        EnsureCancelControl();
        EnsureExecuteCommandServiceControl();
        EnsureKeepPositionControl();
        EnsureCycleSelectionControl();
        EnsureShoppingActionsRoot();
        EnsurePersistenceActionsRoot();
        EnsureTimeoutProgressBar();
        EnsureDragHandle();

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

        if (ShouldHideForActiveAI())
        {
            HideAll(force);
            return;
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

    // No turno normal da IA o helper acompanha a politica de apresentacao oculta.
    // F10, porem, entrega a tela ao desenvolvedor para inspecao: assim como o
    // menuRoot volta a responder, o panel_helper deve voltar a renderizar o estado.
    private bool ShouldHideForActiveAI()
    {
        return matchController != null
            && matchController.ShouldHideActiveAiActionPresentation()
            && !AIController.IsDebugPaused;
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
                title = data.DisembarkStep == 0 ? "ESCOLHER UNIDADE" :
                        data.DisembarkStep == 1 ? "ESCOLHER LOCAL" : "CONFIRMAR DESEMBARQUE";
                body = string.Empty;
                return;

            case TurnStateManager.HelperPanelKind.Merge:
                title = data.IsMergeConfirmStep ? "CONFIRMAR FUSÃO" : "ESCOLHER UNIDADE";
                body = string.Empty;
                return;

            case TurnStateManager.HelperPanelKind.Embark:
                title = ResolveMessage("helper.title.embark", "EMBARK");
                body = string.Empty;
                return;

            case TurnStateManager.HelperPanelKind.EmbarkConfirm:
                title = "CONFIRMAR EMBARQUE";
                body = string.Empty;
                return;

            case TurnStateManager.HelperPanelKind.Supply:
                title = data.SupplyIsConfirmStep ? "CONFIRMAR SUPRIMENTO" : "ESCOLHER UNIDADE";
                body = BuildSupplyBody(data);
                return;

            case TurnStateManager.HelperPanelKind.Transfer:
                title = data.TransferIsConfirmStep ? "CONFIRMAR TRANSFERÊNCIA" : "ESCOLHER DESTINO";
                body = string.Empty;
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

            case TurnStateManager.HelperPanelKind.TerrainStats:
                title = data.TerrainStatsName ?? "TERRENO";
                body = BuildTerrainStatsBody(data);
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

        if (data.UnitStatsShowKeepPositionAimHint)
        {
            if (sb.Length > 0)
                sb.AppendLine().AppendLine();
            sb.Append(ResolveMessage(
                "helper.unit_stats.hint.keep_position_to_aim",
                "Dica: mantenha posição para mirar com armas de longo alcance."));
        }

        return sb.ToString();
    }

    private static string BuildTerrainStatsBody(TurnStateManager.HelperPanelData data)
    {
        // LOCAL/DEFESA ocupa o topo; a descricao comeca somente abaixo dessa linha visual.
        StringBuilder sb = new StringBuilder("\n\n\n");
        if (!string.IsNullOrWhiteSpace(data?.TerrainStatsDescription))
            sb.Append(data.TerrainStatsDescription.Trim());
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

        if (line == "SECTION:Vision")
            return ResolveMessage("helper.unit_stats.section.vision", "Visão");

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
        return string.Empty;
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

        // So listamos os recursos que realmente sao gastos. Ex.: reparo apenas de HP consome
        // Pecas, entao Galoes/Municao (0 gastos) nao aparecem — evita o jogador achar que vai
        // gastar combustivel quando nao vai.
        if (data.SupplyResourceLines != null && data.SupplyResourceLines.Count > 0)
        {
            bool wroteConsumptionHeader = false;
            for (int i = 0; i < data.SupplyResourceLines.Count; i++)
            {
                TurnStateManager.HelperSupplyResourceLine line = data.SupplyResourceLines[i];
                if (line == null)
                    continue;
                int consumed = Mathf.Max(0, line.beforeAmount - line.afterAmount);
                if (consumed <= 0)
                    continue;

                if (!wroteConsumptionHeader)
                {
                    sb.AppendLine(ResolveMessage("helper.merge.separator", "----------------"));
                    sb.AppendLine(ResolveMessage("helper.supply.supplier_consumption", "Consumo do Supridor"));
                    wroteConsumptionHeader = true;
                }

                sb.AppendLine(ResolveMessage(
                    "helper.supply.supplier_consumption.line",
                    "<supply>: <before> - <consumed> -> <after>",
                    new Dictionary<string, string>
                    {
                        { "supply", line.supplyName ?? "Supply" },
                        { "before", Mathf.Max(0, line.beforeAmount).ToString() },
                        { "consumed", consumed.ToString() },
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
            case "confirm_position":
                return ResolveMessage("helper.sensors.label.confirm_position", "Confirmar Posição");
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
        // O arraste vale somente durante a exibicao atual. Quando o helper some,
        // esquece a posicao manual e volta ao layout/dock original na proxima vez.
        if (isDockedCenterLeft || manuallyPositioned)
            RestoreOriginalLayout();
        manuallyPositioned = false;
        hasLastUndockedScreenRect = false;
        cursorNearUndockedDockRegion = false;
        ResetHelperScrollLayout();
        SetVisible(panelVisible: false, title: string.Empty, body: string.Empty, data: null, force: force);
    }

    private void SetVisible(bool panelVisible, string title, string body, TurnStateManager.HelperPanelData data, bool force)
    {
        bool textChanged = force || lastTitle != title || lastBody != body;
        if (force || panelVisible != lastPanelVisible)
        {
            // Qualquer sumico — inclusive o timeout da inspecao por hover, que
            // nao passa pelo HideAll — esquece a posicao manual: o painel
            // renasce no dock original, nao onde foi largado pelo drag.
            if (!panelVisible && (isDockedCenterLeft || manuallyPositioned))
            {
                RestoreOriginalLayout();
                manuallyPositioned = false;
                hasLastUndockedScreenRect = false;
                cursorNearUndockedDockRegion = false;
            }

            SetPanelVisible(panelVisible);
        }

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
        RefreshTimeoutProgressBar(panelVisible, data);
        RefreshDragSurfaces(panelVisible);
        RefreshCancelControl(panelVisible);
        RefreshKeepPositionControl(panelVisible, data);
        RefreshCycleSelectionControl(panelVisible, data);
        RefreshExecuteCommandServiceControl(panelVisible);
        RefreshCommandServicePreviewFocusHighlight(panelVisible);
        RefreshSensorActionControls(panelVisible, data);
        RefreshAimTargetControls(panelVisible, data);
        RefreshAimFooterFocus(panelVisible, data);
        RefreshAimConfirmDetails(panelVisible, data);
        RefreshTurnStartAutonomyControls(panelVisible, data);
        RefreshCommandServiceRows(panelVisible, data);
        RefreshUnitStatsLocal(panelVisible, data);
        RefreshUnitStatsTransportedIcons(panelVisible, data);
        RefreshDisembarkActionControls(panelVisible, data);
        RefreshShoppingActionControls(panelVisible, data);
        RefreshPersistenceActionControls(panelVisible);
        RefreshDynamicPanelHeight(panelVisible, textChanged);

        lastPanelVisible = panelVisible;
        lastTitle = title ?? string.Empty;
        lastBody = body ?? string.Empty;
    }

    private void EnsureTimeoutProgressBar()
    {
        if (!Application.isPlaying || timeoutProgressRoot != null || helperRect == null)
            return;

        timeoutProgressRoot = new GameObject("helper_timeout_progress", typeof(RectTransform), typeof(Image));
        RectTransform rootRect = timeoutProgressRoot.GetComponent<RectTransform>();
        rootRect.SetParent(helperRect, false);
        rootRect.anchorMin = new Vector2(0.02f, 1f);
        rootRect.anchorMax = new Vector2(0.98f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -31f);
        rootRect.sizeDelta = new Vector2(0f, 5f);
        rootRect.SetAsLastSibling();

        Image background = timeoutProgressRoot.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.65f);
        background.raycastTarget = false;

        GameObject fillObject = new GameObject("fill", typeof(RectTransform), typeof(Image));
        timeoutProgressFillRect = fillObject.GetComponent<RectTransform>();
        timeoutProgressFillRect.SetParent(rootRect, false);
        timeoutProgressFillRect.anchorMin = Vector2.zero;
        timeoutProgressFillRect.anchorMax = Vector2.one;
        timeoutProgressFillRect.offsetMin = Vector2.zero;
        timeoutProgressFillRect.offsetMax = Vector2.zero;

        timeoutProgressFill = fillObject.GetComponent<Image>();
        timeoutProgressFill.type = Image.Type.Simple;
        timeoutProgressFill.raycastTarget = false;
        timeoutProgressRoot.SetActive(false);
    }

    private void RefreshTimeoutProgressBar(
        bool panelVisible,
        TurnStateManager.HelperPanelData data)
    {
        EnsureTimeoutProgressBar();
        if (timeoutProgressRoot == null || timeoutProgressFill == null || timeoutProgressFillRect == null)
            return;

        bool visible = panelVisible && data != null && data.ShowTimeoutProgress;
        timeoutProgressRoot.SetActive(visible);
        if (!visible)
            return;

        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        timeoutProgressFill.color = TeamUtils.GetColor(activeTeam);
        float progress = Mathf.Clamp01(data.TimeoutProgress01);
        timeoutProgressFillRect.anchorMax = new Vector2(progress, 1f);
        timeoutProgressFillRect.offsetMin = Vector2.zero;
        timeoutProgressFillRect.offsetMax = Vector2.zero;
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

        if (!contentChanged && !disembarkLayoutDirty)
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
        bool autonomyUpkeepActive = autonomyUpkeepRoot != null && autonomyUpkeepRoot.activeSelf;
        bool commandServiceRowsActive = commandServiceRowsRoot != null && commandServiceRowsRoot.activeSelf;
        bool disembarkButtonsActive = disembarkActionsRoot != null && disembarkActionsRoot.activeSelf;
        bool shoppingButtonsActive = shoppingActionsRoot != null && shoppingActionsRoot.activeSelf;
        bool persistenceButtonsActive = persistenceActionsRoot != null && persistenceActionsRoot.activeSelf;
        if (sensorButtonsActive)
        {
            bodyHeight = sensorActionButtons.Count * (SensorActionButtonHeight + 4f);
        }
        else if (aimButtonsActive)
        {
            bodyHeight = GetAimTargetsPreferredHeight();
        }
        else if (aimConfirmActive)
        {
            bodyHeight = AimConfirmDetailsHeight;
        }
        else if (autonomyUpkeepActive)
        {
            // Titulo fixo + viewport: passando do teto, o excedente vira rolagem
            // em vez de esticar o painel pra fora da tela.
            bodyHeight = GetAutonomyUpkeepPreferredHeight();
        }
        else if (commandServiceRowsActive)
        {
            bodyHeight = GetCommandServicePreferredHeight();
        }
        else if (disembarkButtonsActive)
        {
            bodyHeight = GetDisembarkActionsPreferredHeight();
        }
        else if (shoppingButtonsActive)
        {
            bodyHeight = shoppingActionButtons.Count * (ShoppingActionButtonHeight + 4f);
        }
        else if (persistenceButtonsActive)
        {
            LayoutElement detailsLayout = persistenceConfirmationDetails != null
                ? persistenceConfirmationDetails.GetComponent<LayoutElement>()
                : null;
            bodyHeight = persistenceActionButtons.Count * (PersistenceActionButtonHeight + 10f)
                + (detailsLayout != null ? detailsLayout.preferredHeight + 10f : 0f)
                + persistenceFooterSpacerHeight;
        }
        else if (helperTxt != null)
        {
            helperTxt.ForceMeshUpdate();
            bodyHeight = Mathf.Max(0f, helperTxt.preferredHeight);
            if (unitStatsLocalAtBottom && unitStatsLocalRoot != null && unitStatsLocalRoot.activeSelf)
                bodyHeight += unitStatsLocalRoot.GetComponent<RectTransform>().rect.height + 6f;
        }

        float baseMin = cachedBasePanelHeight > 0f ? cachedBasePanelHeight : 0f;
        float minHeight = Mathf.Max(minPanelHeight, baseMin);
        float configuredMaxHeight = aimButtonsActive
            ? Mathf.Min(maxPanelHeight, AimTargetsMaxPanelHeight)
            : maxPanelHeight;
        float maxHeight = Mathf.Max(minHeight, configuredMaxHeight);
        float footerHeight = GetActiveFooterHeight();
        float targetHeight = Mathf.Clamp(titleHeight + bodyHeight + Mathf.Max(0f, contentVerticalPadding) + footerHeight, minHeight, maxHeight);
        helperRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        RefreshHelperScrollLayout(titleHeight, bodyHeight, targetHeight);
        disembarkLayoutDirty = false;
    }

    private void RefreshCommandServiceRows(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        bool active = panelVisible && data != null && data.Kind == TurnStateManager.HelperPanelKind.CommandService &&
                      data.CommandServiceIsEstimate && data.CommandServiceTargetLines != null;
        if (!active)
        {
            if (commandServiceRowsRoot != null) commandServiceRowsRoot.SetActive(false);
            if (commandServiceViewportRoot != null) commandServiceViewportRoot.SetActive(false);
            if (commandServiceSummaryLabel != null) commandServiceSummaryLabel.gameObject.SetActive(false);
            commandServiceRowsSignature = string.Empty;
            return;
        }

        EnsureCommandServiceRowsRoot();
        StringBuilder signature = new StringBuilder();
        signature.Append(data.CommandServiceServedTargets).Append('|').Append(data.CommandServiceTotalCost)
            .Append('|').Append(data.CommandServiceMoneyAfter);
        for (int i = 0; i < data.CommandServiceTargetLines.Count; i++)
        {
            var line = data.CommandServiceTargetLines[i];
            if (line != null) signature.Append('|').Append(line.unitName).Append('|').Append(line.gainsLabel)
                .Append('|').Append(line.isFocused).Append('|').Append(line.isFullyAffordable);
        }
        for (int i = 0; i < data.CommandServiceSkippedUnitLines.Count; i++)
        {
            var line = data.CommandServiceSkippedUnitLines[i];
            if (line != null) signature.Append("|skip|").Append(line.unitName).Append('|').Append(line.isFocused);
        }

        if (signature.ToString() != commandServiceRowsSignature)
        {
            RebuildCommandServiceRows(data);
            commandServiceRowsSignature = signature.ToString();
            disembarkLayoutDirty = true;
        }
        commandServiceRowsRoot.SetActive(true);
        commandServiceViewportRoot.SetActive(true);
        commandServiceSummaryLabel.gameObject.SetActive(true);
        if (helperTxt != null) helperTxt.enabled = false;
        if (selfPanelCanvasGroup != null)
        {
            selfPanelCanvasGroup.interactable = true;
            selfPanelCanvasGroup.blocksRaycasts = true;
        }
    }

    private void EnsureCommandServiceRowsRoot()
    {
        if (!Application.isPlaying || commandServiceRowsRoot != null || helperRect == null) return;
        GameObject summary = new GameObject("helper_command_service_summary", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform summaryRect = summary.GetComponent<RectTransform>();
        summaryRect.SetParent(helperRect, false);
        summaryRect.anchorMin = new Vector2(0.04f, 1f);
        summaryRect.anchorMax = new Vector2(0.96f, 1f);
        summaryRect.pivot = new Vector2(0.5f, 1f);
        summaryRect.anchoredPosition = new Vector2(0f, -48f);
        summaryRect.sizeDelta = new Vector2(0f, CommandServiceSummaryHeight);
        commandServiceSummaryLabel = summary.GetComponent<TMP_Text>();
        commandServiceSummaryLabel.fontSize = 18f;
        commandServiceSummaryLabel.fontStyle = FontStyles.Bold;
        commandServiceSummaryLabel.alignment = TextAlignmentOptions.Center;
        commandServiceSummaryLabel.raycastTarget = false;
        commandServiceSummaryLabel.margin = new Vector4(8f, 3f, 8f, 3f);

        commandServiceViewportRoot = new GameObject("helper_command_service_viewport", typeof(RectTransform), typeof(RectMask2D));
        commandServiceViewportRect = commandServiceViewportRoot.GetComponent<RectTransform>();
        commandServiceViewportRect.SetParent(helperRect, false);
        commandServiceViewportRect.anchorMin = new Vector2(0.04f, 1f);
        commandServiceViewportRect.anchorMax = new Vector2(0.96f, 1f);
        commandServiceViewportRect.pivot = new Vector2(0.5f, 1f);
        commandServiceViewportRect.anchoredPosition = new Vector2(0f, -48f - CommandServiceSummaryHeight - 4f);

        commandServiceRowsRoot = new GameObject("helper_command_service_rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
        RectTransform rect = commandServiceRowsRoot.GetComponent<RectTransform>();
        rect.SetParent(commandServiceViewportRect, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        VerticalLayoutGroup layout = commandServiceRowsRoot.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        commandServiceRowsRoot.SetActive(false);
        commandServiceViewportRoot.SetActive(false);
    }

    private void RebuildCommandServiceRows(TurnStateManager.HelperPanelData data)
    {
        for (int i = commandServiceRowsRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(commandServiceRowsRoot.transform.GetChild(i).gameObject);
        commandServiceRows.Clear();

        commandServiceSummaryLabel.text = $"Previstos: {data.CommandServiceServedTargets}\nCusto previsto: ${data.CommandServiceTotalCost}\nRestante: ${data.CommandServiceMoneyAfter}";
        commandServiceSummaryLabel.color = currentTeamColor;

        for (int i = 0; i < data.CommandServiceTargetLines.Count; i++)
        {
            var line = data.CommandServiceTargetLines[i];
            if (line == null) continue;
            CreateCommandServiceUnitRow(line.unitName, line.gainsLabel, line.unitSprite, line.unitColor,
                line.cell, true, i, line.isFocused, line.isFullyAffordable ? currentTeamColor : new Color(1f, 0.7f, 0.28f));
        }
        for (int i = 0; i < data.CommandServiceSkippedUnitLines.Count; i++)
        {
            var line = data.CommandServiceSkippedUnitLines[i];
            if (line == null) continue;
            CreateCommandServiceUnitRow(line.unitName, $"Não atendida — {line.sourceLabel}", line.unitSprite,
                line.unitColor, line.cell, false, i, line.isFocused, new Color(0.58f, 0.58f, 0.58f));
        }
        float contentHeight = commandServiceRows.Count * (CommandServiceRowHeight + 4f);
        commandServiceRowsRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, contentHeight);
        float viewportHeight = GetCommandServiceListViewportHeight();
        commandServiceViewportRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewportHeight);
        EnsureFocusedCommandServiceRowVisible();
    }

    private float GetCommandServiceListViewportHeight()
    {
        float content = commandServiceRows.Count * (CommandServiceRowHeight + 4f);
        float screenLimit = Mathf.Clamp(Screen.height * 0.52f, CommandServiceRowHeight * 2f, CommandServiceMaxListHeight);
        return Mathf.Min(content, screenLimit);
    }

    private float GetCommandServicePreferredHeight()
    {
        return CommandServiceSummaryHeight + 4f + GetCommandServiceListViewportHeight();
    }

    private void EnsureFocusedCommandServiceRowVisible()
    {
        if (commandServiceRowsRoot == null || commandServiceViewportRect == null || commandServiceRows.Count == 0) return;
        int focused = -1;
        for (int i = 0; i < commandServiceRows.Count; i++)
        {
            Image image = commandServiceRows[i] != null ? commandServiceRows[i].GetComponent<Image>() : null;
            if (image != null && image.color.a > 0.9f) { focused = i; break; }
        }
        RectTransform contentRect = commandServiceRowsRoot.GetComponent<RectTransform>();
        float viewport = commandServiceViewportRect.rect.height;
        float content = commandServiceRows.Count * (CommandServiceRowHeight + 4f);
        float current = contentRect.anchoredPosition.y;
        if (focused >= 0)
        {
            float top = focused * (CommandServiceRowHeight + 4f);
            float bottom = top + CommandServiceRowHeight;
            if (top < current) current = top;
            else if (bottom > current + viewport) current = bottom - viewport;
        }
        contentRect.anchoredPosition = new Vector2(0f, Mathf.Clamp(current, 0f, Mathf.Max(0f, content - viewport)));
    }

    private void CreateCommandServiceUnitRow(string unitName, string details, Sprite sprite, Color spriteColor,
        Vector3Int cell, bool served, int lineIndex, bool focused, Color textColor)
    {
        GameObject row = new GameObject("command_service_unit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        row.transform.SetParent(commandServiceRowsRoot.transform, false);
        row.GetComponent<Image>().color = focused ? TeamButtonBackground(currentTeamColor, true) : new Color(0.02f, 0.06f, 0.04f, 0.72f);
        LayoutElement element = row.GetComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = CommandServiceRowHeight;
        Button button = row.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => turnStateManager?.FocusCommandServicePreviewLine(served, lineIndex));
        if (sprite != null) CreateDisembarkRowIcon(row.transform, "unit_icon", sprite, spriteColor, true);

        GameObject labelObject = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(row.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(sprite != null ? 52f : 8f, 3f);
        labelRect.offsetMax = new Vector2(-6f, -3f);
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = $"{unitName}\n{details}";
        label.fontStyle = FontStyles.Bold;
        label.fontSize = 18f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 18f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = textColor;
        label.raycastTarget = false;
        commandServiceRows.Add(row);
    }

    private void CreateCommandServiceTextRow(string text, float height, Color color, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject("command_service_summary", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        obj.transform.SetParent(commandServiceRowsRoot.transform, false);
        LayoutElement element = obj.GetComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = height;
        TMP_Text label = obj.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = 18f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = alignment;
        label.color = color;
        label.margin = new Vector4(8f, 3f, 8f, 3f);
        label.raycastTarget = false;
    }

    private void RefreshTurnStartAutonomyControls(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        bool active = panelVisible && data != null &&
                      data.Kind == TurnStateManager.HelperPanelKind.TurnStartAutonomy &&
                      data.TurnStartAutonomyLines != null && data.TurnStartAutonomyLines.Count > 0;
        if (!active)
        {
            // Jornal fechado: a proxima abertura comeca do topo da lista (a
            // posicao e devolvida ANTES de desligar, senao uma reabertura sem
            // rebuild reaparece rolada).
            autonomyUpkeepScrollOffset = 0f;
            ApplyAutonomyUpkeepScrollPosition();
            if (autonomyUpkeepRoot != null)
                autonomyUpkeepRoot.SetActive(false);
            if (autonomyUpkeepViewportRoot != null)
                autonomyUpkeepViewportRoot.SetActive(false);
            if (autonomyUpkeepTitleLabel != null)
                autonomyUpkeepTitleLabel.gameObject.SetActive(false);
            autonomyUpkeepSignature = string.Empty;
            return;
        }

        EnsureAutonomyUpkeepRoot();
        StringBuilder signatureBuilder = new StringBuilder();
        for (int i = 0; i < data.TurnStartAutonomyLines.Count; i++)
        {
            TurnStateManager.HelperTurnStartAutonomyLine line = data.TurnStartAutonomyLines[i];
            if (line == null) continue;
            signatureBuilder.Append('|').Append(line.unitName).Append('|').Append(line.cell)
                .Append('|').Append(line.fuelBefore).Append('|').Append(line.autonomyConsumed)
                .Append('|').Append(line.fuelAfter).Append('/').Append(line.fuelMax)
                .Append('|').Append(line.isFocused)
                .Append('|').Append(line.customText ?? string.Empty)
                .Append('|').Append(line.severityTier)
                .Append('|').Append(line.unitSprite != null ? line.unitSprite.name : string.Empty);
        }
        string signature = signatureBuilder.ToString();
        if (signature != autonomyUpkeepSignature)
        {
            RebuildAutonomyUpkeepRows(data.TurnStartAutonomyLines);
            autonomyUpkeepSignature = signature;
            // A altura do painel depende do viewport recem-medido.
            disembarkLayoutDirty = true;
        }

        autonomyUpkeepRoot.SetActive(true);
        if (autonomyUpkeepViewportRoot != null)
            autonomyUpkeepViewportRoot.SetActive(true);
        if (autonomyUpkeepTitleLabel != null)
            autonomyUpkeepTitleLabel.gameObject.SetActive(true);
        if (helperTxt != null)
            helperTxt.enabled = false;
        ApplyFooterButtonFocus(
            cancelActionImage,
            cancelActionLabel,
            turnStateManager != null && turnStateManager.IsTurnStartAutonomyReportCancelFocused);
        if (panelHelper == gameObject && selfPanelCanvasGroup != null)
        {
            selfPanelCanvasGroup.interactable = true;
            selfPanelCanvasGroup.blocksRaycasts = true;
        }
    }

    private void EnsureAutonomyUpkeepRoot()
    {
        if (!Application.isPlaying || autonomyUpkeepRoot != null || helperRect == null)
            return;

        // Titulo do Jornal fixo FORA da area rolavel (mesmo arranjo do resumo do
        // Servico do Comando): rolar as noticias nao deve levar o nome do
        // relatorio embora.
        GameObject titleObject = new GameObject(
            "helper_autonomy_upkeep_title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.SetParent(helperRect, false);
        titleRect.anchorMin = new Vector2(0.04f, 1f);
        titleRect.anchorMax = new Vector2(0.96f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -48f);
        titleRect.sizeDelta = new Vector2(0f, AutonomyUpkeepHeaderHeight);
        autonomyUpkeepTitleLabel = titleObject.GetComponent<TMP_Text>();
        autonomyUpkeepTitleLabel.fontSize = 20f;
        autonomyUpkeepTitleLabel.fontStyle = FontStyles.Bold;
        autonomyUpkeepTitleLabel.alignment = TextAlignmentOptions.Center;
        autonomyUpkeepTitleLabel.enableAutoSizing = true;
        autonomyUpkeepTitleLabel.fontSizeMin = 14f;
        autonomyUpkeepTitleLabel.fontSizeMax = 20f;
        autonomyUpkeepTitleLabel.margin = new Vector4(8f, 3f, 8f, 3f);
        autonomyUpkeepTitleLabel.raycastTarget = false;
        titleObject.SetActive(false);

        // Viewport com RectMask2D propria: a lista corta por baixo do titulo em
        // vez de deslizar as noticias por cima dele.
        autonomyUpkeepViewportRoot = new GameObject(
            "helper_autonomy_upkeep_viewport", typeof(RectTransform), typeof(RectMask2D));
        autonomyUpkeepViewportRect = autonomyUpkeepViewportRoot.GetComponent<RectTransform>();
        autonomyUpkeepViewportRect.SetParent(helperRect, false);
        autonomyUpkeepViewportRect.anchorMin = new Vector2(0.04f, 1f);
        autonomyUpkeepViewportRect.anchorMax = new Vector2(0.96f, 1f);
        autonomyUpkeepViewportRect.pivot = new Vector2(0.5f, 1f);
        autonomyUpkeepViewportRect.anchoredPosition = new Vector2(0f, -48f - AutonomyUpkeepHeaderHeight - 4f);
        autonomyUpkeepViewportRect.sizeDelta = new Vector2(0f, AutonomyUpkeepRowHeight);

        autonomyUpkeepRoot = new GameObject("helper_autonomy_upkeep", typeof(RectTransform), typeof(VerticalLayoutGroup));
        RectTransform rect = autonomyUpkeepRoot.GetComponent<RectTransform>();
        rect.SetParent(autonomyUpkeepViewportRect, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        VerticalLayoutGroup layout = autonomyUpkeepRoot.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        autonomyUpkeepRoot.SetActive(false);
        autonomyUpkeepViewportRoot.SetActive(false);
    }

    private void RebuildAutonomyUpkeepRows(List<TurnStateManager.HelperTurnStartAutonomyLine> lines)
    {
        for (int i = autonomyUpkeepRoot.transform.childCount - 1; i >= 0; i--)
            Destroy(autonomyUpkeepRoot.transform.GetChild(i).gameObject);
        autonomyUpkeepRows.Clear();
        autonomyUpkeepRowFrameTops.Clear();
        autonomyUpkeepRowBottoms.Clear();
        autonomyUpkeepFocusedRowIndex = -1;

        if (autonomyUpkeepTitleLabel != null)
            autonomyUpkeepTitleLabel.color = currentTeamColor;

        // Percorre o conteudo medindo: o topo de cada noticia e o que o scroll
        // usa para enquadrar a linha focada.
        float contentCursor = 0f;
        int lastTier = int.MinValue;
        for (int i = 0; i < lines.Count; i++)
        {
            TurnStateManager.HelperTurnStartAutonomyLine line = lines[i];
            if (line == null) continue;

            float frameTop = contentCursor;

            // Cabecalho do tier de severidade, na troca de tier. Cor fixa (nao
            // por time): severidade e universal e precisa saltar aos olhos.
            if (line.severityTier != lastTier)
            {
                lastTier = line.severityTier;
                GetSeverityTierHeader(line.severityTier, out string tierLabel, out Color tierColor);
                CreateAutonomyUpkeepTextRow(tierLabel, AutonomyUpkeepTierHeaderHeight, 16f, TextAlignmentOptions.Left, tierColor);
                contentCursor += AutonomyUpkeepTierHeaderHeight + 4f;
            }

            autonomyUpkeepRowFrameTops.Add(frameTop);
            autonomyUpkeepRowBottoms.Add(contentCursor + AutonomyUpkeepRowHeight);
            contentCursor += AutonomyUpkeepRowHeight + 4f;
            if (line.isFocused)
                autonomyUpkeepFocusedRowIndex = autonomyUpkeepRows.Count;

            bool isBriefingLine = !string.IsNullOrWhiteSpace(line.customText);
            GameObject row = new GameObject("autonomy_upkeep_unit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(autonomyUpkeepRoot.transform, false);
            row.GetComponent<Image>().color = line.isFocused
                ? TeamButtonBackground(currentTeamColor, true)
                : new Color(0.02f, 0.06f, 0.04f, 0.72f);
            Button rowButton = row.GetComponent<Button>();
            rowButton.transition = Selectable.Transition.None;
            int targetIndex = i;
            rowButton.onClick.AddListener(() => turnStateManager?.TryPanToTurnStartAutonomyUnitFromPointer(targetIndex));
            row.AddComponent<PanelHelperJournalScrollDragHandle>().Configure(this);
            LayoutElement element = row.GetComponent<LayoutElement>();
            element.minHeight = AutonomyUpkeepRowHeight;
            element.preferredHeight = AutonomyUpkeepRowHeight;

            if (line.unitSprite != null)
                CreateDisembarkRowIcon(row.transform, "unit_icon", line.unitSprite, line.unitColor, true);

            GameObject labelObject = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(row.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            // Linhas de briefing nao tem barra de combustivel: usam a altura toda.
            labelRect.offsetMin = new Vector2(line.unitSprite != null ? 52f : 8f, isBriefingLine ? 3f : 12f);
            labelRect.offsetMax = new Vector2(-6f, -3f);
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            if (isBriefingLine)
            {
                // Linha do Jornal (evento de briefing): texto pronto, sem barra.
                label.text = line.customText;
            }
            else
            {
                string unitName = string.IsNullOrWhiteSpace(line.unitName) ? "Unidade" : line.unitName;
                label.text = $"{unitName}\nCombustível {Mathf.Max(0, line.fuelBefore)} − {Mathf.Max(0, line.autonomyConsumed)} = {Mathf.Max(0, line.fuelAfter)}\n{FormatMapCell(line.cell)}";
            }
            label.fontStyle = FontStyles.Bold;
            label.fontSize = 18f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 18f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = currentTeamColor;
            label.raycastTarget = false;
            if (!isBriefingLine)
                CreateAutonomyFuelBar(row.transform, line.fuelAfter, line.fuelMax);
            autonomyUpkeepRows.Add(row);
        }

        autonomyUpkeepContentHeight = contentCursor;
        autonomyUpkeepRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, autonomyUpkeepContentHeight);
        autonomyUpkeepViewportHeight = GetAutonomyUpkeepViewportHeight();
        if (autonomyUpkeepViewportRect != null)
            autonomyUpkeepViewportRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, autonomyUpkeepViewportHeight);

        // Quando sobra noticia fora do viewport, o titulo avisa quantas existem
        // — sem isso o corte da mascara parece o fim do relatorio.
        if (autonomyUpkeepTitleLabel != null)
            autonomyUpkeepTitleLabel.text = autonomyUpkeepContentHeight > autonomyUpkeepViewportHeight + 0.5f
                ? $"Jornal do Comandante ({autonomyUpkeepRows.Count} notícias)"
                : "Jornal do Comandante";

        EnsureFocusedAutonomyUpkeepRowVisible();
        ApplyAutonomyUpkeepScrollPosition();
    }

    // Teto do que fica visivel de uma vez: o painel nao cresce alem disso e o
    // resto do Jornal passa a existir na rolagem.
    private float GetAutonomyUpkeepViewportHeight()
    {
        float screenLimit = Mathf.Clamp(
            Screen.height * 0.52f, AutonomyUpkeepRowHeight * 2f, AutonomyUpkeepMaxListHeight);
        return autonomyUpkeepContentHeight > 0f
            ? Mathf.Min(autonomyUpkeepContentHeight, screenLimit)
            : screenLimit;
    }

    private float GetAutonomyUpkeepPreferredHeight()
    {
        return AutonomyUpkeepHeaderHeight + 4f + GetAutonomyUpkeepViewportHeight();
    }

    private void ApplyAutonomyUpkeepScrollPosition()
    {
        if (autonomyUpkeepRoot == null)
            return;
        RectTransform content = autonomyUpkeepRoot.GetComponent<RectTransform>();
        if (content == null)
            return;

        float maxOffset = Mathf.Max(0f, autonomyUpkeepContentHeight - autonomyUpkeepViewportHeight);
        autonomyUpkeepScrollOffset = Mathf.Clamp(autonomyUpkeepScrollOffset, 0f, maxOffset);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, autonomyUpkeepScrollOffset);
    }

    // Navegacao por setas no relatorio aberto pelo menu: a linha destacada tem
    // de estar dentro do viewport, senao o foco desaparece embaixo da mascara.
    private void EnsureFocusedAutonomyUpkeepRowVisible()
    {
        if (autonomyUpkeepFocusedRowIndex < 0 ||
            autonomyUpkeepFocusedRowIndex >= autonomyUpkeepRowFrameTops.Count ||
            autonomyUpkeepViewportHeight <= 0f)
            return;

        float top = autonomyUpkeepRowFrameTops[autonomyUpkeepFocusedRowIndex];
        float bottom = autonomyUpkeepRowBottoms[autonomyUpkeepFocusedRowIndex];
        if (top < autonomyUpkeepScrollOffset)
            autonomyUpkeepScrollOffset = top;
        else if (bottom > autonomyUpkeepScrollOffset + autonomyUpkeepViewportHeight)
            autonomyUpkeepScrollOffset = bottom - autonomyUpkeepViewportHeight;
    }

    // Arraste (toque/mouse) sobre as noticias: o conteudo acompanha o ponteiro
    // — puxar pra cima traz as proximas linhas.
    public void ScrollTurnBriefingByPointerDelta(float screenDeltaY)
    {
        if (autonomyUpkeepRoot == null || !autonomyUpkeepRoot.activeSelf)
            return;
        autonomyUpkeepScrollOffset += screenDeltaY;
        ApplyAutonomyUpkeepScrollPosition();
    }

    private static void GetSeverityTierHeader(int tier, out string label, out Color color)
    {
        // Marcadores ASCII (qualquer fonte tem); a cor faz o peso da severidade.
        switch (tier)
        {
            case 0:
                label = "!! CRÍTICO";
                color = new Color(1f, 0.42f, 0.38f); // vermelho
                break;
            case 1:
                label = "! ATENÇÃO";
                color = new Color(1f, 0.80f, 0.32f); // ambar
                break;
            default:
                label = "- INFORMATIVO";
                color = new Color(0.62f, 0.85f, 1f); // azul claro
                break;
        }
    }

    private static void CreateAutonomyFuelBar(Transform parent, int fuelAfter, int fuelMax)
    {
        GameObject backgroundObject = new GameObject("autonomy_bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.SetParent(parent, false);
        backgroundRect.anchorMin = new Vector2(0.02f, 0f);
        backgroundRect.anchorMax = new Vector2(0.98f, 0f);
        backgroundRect.pivot = new Vector2(0.5f, 0f);
        backgroundRect.anchoredPosition = new Vector2(0f, 4f);
        backgroundRect.sizeDelta = new Vector2(0f, 7f);
        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.78f);
        background.raycastTarget = false;

        float ratio = Mathf.Clamp01((float)Mathf.Max(0, fuelAfter) / Mathf.Max(1, fuelMax));
        GameObject fillObject = new GameObject("fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.SetParent(backgroundRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(ratio, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        // Mesmos thresholds e cores do indicador de autonomia do Unit.prefab.
        fill.color = ratio <= 0.25f
            ? new Color(1f, 0.3f, 0.3f, 1f)
            : ratio <= 0.50f
                ? new Color(1f, 0.85f, 0.35f, 1f)
                : new Color(0.8235295f, 0.4117647f, 0.1176471f, 1f);
        fill.raycastTarget = false;
    }

    public static bool TryPanToAutonomyCell(Vector3Int cell)
    {
        if (instance == null)
            return false;
        if (instance.cameraController == null)
            instance.cameraController = FindAnyObjectByType<CameraController>();
        Tilemap board = instance.cursorController != null ? instance.cursorController.BoardTilemap : null;
        if (instance.cameraController == null || board == null)
            return false;

        cell.z = 0;
        instance.cameraController.FocusOn(board.GetCellCenterWorld(cell));
        return true;
    }

    private void CreateAutonomyUpkeepTextRow(string text, float height, float fontSize, TextAlignmentOptions alignment, Color? fixedColor = null)
    {
        GameObject obj = new GameObject("autonomy_upkeep_header", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        obj.transform.SetParent(autonomyUpkeepRoot.transform, false);
        LayoutElement element = obj.GetComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        TMP_Text label = obj.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = alignment;
        // Cabecalhos de tier usam cor fixa de severidade; o titulo segue o time.
        label.color = fixedColor ?? currentTeamColor;
        label.raycastTarget = false;
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
        bool active = panelVisible && data != null &&
                      (data.Kind == TurnStateManager.HelperPanelKind.AimTargets ||
                       data.Kind == TurnStateManager.HelperPanelKind.Embark) &&
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
            signatureBuilder
                .Append(data.AimTargetLines[i].unitName)
                .Append('|').Append(data.AimTargetLines[i].hp)
                .Append('|').Append(data.AimTargetLines[i].terrainLabel)
                .Append('|').Append(data.AimTargetLines[i].weaponName)
                .Append('|').Append(data.AimTargetLines[i].weaponCategoryLabel)
                .Append('|').Append(data.AimTargetLines[i].isValid);
        string signature = data.Kind + signatureBuilder.ToString();
        if (signature != aimTargetsSignature)
        {
            RebuildAimTargetButtons(data.AimTargetLines,
                data.Kind == TurnStateManager.HelperPanelKind.Embark);
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
                Outline focusBorder = button.GetComponent<Outline>();
                if (focusBorder != null)
                {
                    focusBorder.effectColor = currentTeamColor;
                    focusBorder.enabled = data.AimTargetLines[i].isFocused;
                }
            }
            else
            {
                Outline focusBorder = button.GetComponent<Outline>();
                if (focusBorder != null)
                    focusBorder.enabled = false;
                TintScriptButtonToTeam(button, data.AimTargetLines[i].isFocused);
            }
        }
        aimFocusedButtonIndex = data.AimTargetLines.FindIndex(line => line != null && line.isFocused);
        EnsureFocusedAimTargetVisible();
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
        if (!panelVisible || data == null ||
            (data.Kind != TurnStateManager.HelperPanelKind.AimConfirm &&
             data.Kind != TurnStateManager.HelperPanelKind.EmbarkConfirm) ||
            turnStateManager == null)
            return;
        int focus = data.Kind == TurnStateManager.HelperPanelKind.EmbarkConfirm
            ? turnStateManager.EmbarkConfirmButtonFocus
            : turnStateManager.MirandoConfirmButtonFocus;
        ApplyFooterButtonFocus(executeCommandServiceImage, executeCommandServiceLabel, focus == 0);
        ApplyFooterButtonFocus(cancelActionImage, cancelActionLabel, focus == 1);
    }

    // Detalhes do CONFIRMAR ATAQUE: sprite + nome do alvo, HP e LOCAL.
    private void RefreshAimConfirmDetails(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        bool active = panelVisible && data != null &&
                      (data.Kind == TurnStateManager.HelperPanelKind.AimConfirm ||
                       data.Kind == TurnStateManager.HelperPanelKind.EmbarkConfirm);
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
        bool showWeapon = data.Kind == TurnStateManager.HelperPanelKind.AimConfirm;
        aimConfirmWeaponText.text = string.IsNullOrWhiteSpace(data.AimConfirmWeaponName)
            ? "ARMA: —"
            : $"ARMA: {data.AimConfirmWeaponName}";
        aimConfirmWeaponText.gameObject.SetActive(showWeapon);
        aimConfirmLocalText.text = string.IsNullOrWhiteSpace(data.AimConfirmTerrainLabel)
            ? "LOCAL:" : $"LOCAL: {data.AimConfirmTerrainLabel}";
        aimConfirmLocalIcon.sprite = data.AimConfirmLocalSprite;
        aimConfirmLocalIcon.enabled = data.AimConfirmLocalSprite != null;
        aimConfirmLocalIcon.color = data.AimConfirmLocalColor;

        aimConfirmTargetText.color = currentTeamColor;
        aimConfirmHpText.color = currentTeamColor;
        aimConfirmWeaponText.color = currentTeamColor;
        aimConfirmLocalText.color = currentTeamColor;

        aimConfirmDetailsRoot.SetActive(true);
        if (helperTxt != null) helperTxt.enabled = false;
    }

    private void RefreshUnitStatsLocal(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        bool active = panelVisible && data != null &&
                      (data.Kind == TurnStateManager.HelperPanelKind.UnitStats ||
                       data.Kind == TurnStateManager.HelperPanelKind.ConstructionStats ||
                       data.Kind == TurnStateManager.HelperPanelKind.TerrainStats);
        if (!active)
        {
            unitStatsLocalAtBottom = false;
            if (unitStatsLocalRoot != null)
                unitStatsLocalRoot.SetActive(false);
            return;
        }

        EnsureUnitStatsLocalRoot();
        if (unitStatsLocalRoot == null)
            return;

        unitStatsLocalText.text = string.IsNullOrWhiteSpace(data.UnitStatsLocalLabel)
            ? "LOCAL: —"
            : $"LOCAL: {data.UnitStatsLocalLabel}";
        unitStatsDefenseText.text = $"DEFESA: {data.UnitStatsDefensePoints}";
        bool showConstructionStock = data.Kind == TurnStateManager.HelperPanelKind.UnitStats &&
                                     !string.IsNullOrWhiteSpace(data.UnitStatsConstructionStockLine);
        unitStatsConstructionStockText.text = data.UnitStatsConstructionStockLine ?? string.Empty;
        unitStatsConstructionStockText.gameObject.SetActive(showConstructionStock);
        unitStatsLocalIcon.sprite = data.UnitStatsLocalSprite;
        unitStatsLocalIcon.enabled = data.UnitStatsLocalSprite != null;
        unitStatsLocalIcon.color = data.UnitStatsLocalColor;
        unitStatsStructureIcon.sprite = data.UnitStatsStructureSprite;
        unitStatsStructureIcon.enabled = data.UnitStatsStructureSprite != null;
        unitStatsStructureIcon.color = data.UnitStatsStructureColor;
        ConfigureUnitStatsLocalIcons(data.UnitStatsStructureIsSeparate && data.UnitStatsStructureSprite != null);
        unitStatsLocalText.color = currentTeamColor;
        unitStatsDefenseText.color = currentTeamColor;
        unitStatsConstructionStockText.color = currentTeamColor;
        RectTransform localRect = unitStatsLocalRoot.GetComponent<RectTransform>();
        localRect.sizeDelta = new Vector2(0f, showConstructionStock ? 108f : 60f);
        if (data.Kind == TurnStateManager.HelperPanelKind.TerrainStats)
        {
            localRect.anchoredPosition = new Vector2(0f, -48f);
        }
        else if (helperTxt != null)
        {
            helperTxt.ForceMeshUpdate();
            float contentHeight = Mathf.Max(0f, helperTxt.preferredHeight);
            localRect.anchoredPosition = new Vector2(0f, -48f - contentHeight - 6f);
        }
        unitStatsLocalAtBottom = data.Kind == TurnStateManager.HelperPanelKind.UnitStats ||
                                 data.Kind == TurnStateManager.HelperPanelKind.ConstructionStats;
        unitStatsLocalRoot.SetActive(true);
    }

    private void RefreshUnitStatsTransportedIcons(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        bool active = panelVisible && helperTxt != null && data != null &&
                      data.Kind == TurnStateManager.HelperPanelKind.UnitStats &&
                      data.UnitStatsTransportedVisuals != null && data.UnitStatsTransportedVisuals.Count > 0;
        if (!active)
        {
            for (int i = 0; i < unitStatsTransportedIcons.Count; i++)
                if (unitStatsTransportedIcons[i] != null) unitStatsTransportedIcons[i].gameObject.SetActive(false);
            return;
        }

        helperTxt.ForceMeshUpdate();
        string renderedText = helperTxt.text ?? string.Empty;
        int searchStart = 0;
        for (int i = 0; i < data.UnitStatsTransportedVisuals.Count; i++)
        {
            TurnStateManager.HelperTransportedUnitVisual visual = data.UnitStatsTransportedVisuals[i];
            Image icon = EnsureUnitStatsTransportedIcon(i);
            if (visual == null || visual.sprite == null || icon == null)
            {
                if (icon != null) icon.gameObject.SetActive(false);
                continue;
            }

            int textIndex = renderedText.IndexOf(visual.unitName ?? string.Empty, searchStart, System.StringComparison.Ordinal);
            if (textIndex < 0 || textIndex >= helperTxt.textInfo.characterCount)
            {
                icon.gameObject.SetActive(false);
                continue;
            }

            searchStart = textIndex + Mathf.Max(1, (visual.unitName ?? string.Empty).Length);
            TMP_CharacterInfo character = helperTxt.textInfo.characterInfo[textIndex];
            RectTransform rect = icon.rectTransform;
            float iconHalfWidth = rect.rect.width * 0.5f;
            float minimumCenterX = helperTxt.rectTransform.rect.xMin + iconHalfWidth + 2f;
            rect.localPosition = new Vector3(
                Mathf.Max(character.bottomLeft.x - iconHalfWidth - 8f, minimumCenterX),
                (character.bottomLeft.y + character.topRight.y) * 0.5f,
                0f);
            icon.sprite = visual.sprite;
            icon.color = visual.color;
            icon.gameObject.SetActive(true);
        }

        for (int i = data.UnitStatsTransportedVisuals.Count; i < unitStatsTransportedIcons.Count; i++)
            if (unitStatsTransportedIcons[i] != null) unitStatsTransportedIcons[i].gameObject.SetActive(false);
    }

    private Image EnsureUnitStatsTransportedIcon(int index)
    {
        while (unitStatsTransportedIcons.Count <= index)
        {
            GameObject obj = new GameObject(
                $"transported_unit_icon_{unitStatsTransportedIcons.Count}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(helperTxt.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            // Mesmo tamanho visual do icone do hex inspecionado (LOCAL).
            rect.sizeDelta = new Vector2(52f, 52f);
            Image image = obj.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            unitStatsTransportedIcons.Add(image);
        }

        return unitStatsTransportedIcons[index];
    }

    private void EnsureUnitStatsLocalRoot()
    {
        if (!Application.isPlaying || unitStatsLocalRoot != null || helperRect == null)
            return;

        unitStatsLocalRoot = new GameObject("helper_unit_stats_local", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform rootRect = unitStatsLocalRoot.GetComponent<RectTransform>();
        rootRect.SetParent(helperRect, false);
        rootRect.anchorMin = new Vector2(0.06f, 1f);
        rootRect.anchorMax = new Vector2(0.94f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -48f);
        rootRect.sizeDelta = new Vector2(0f, 60f);

        HorizontalLayoutGroup layout = unitStatsLocalRoot.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        unitStatsIconsRoot = new GameObject("local_icons", typeof(RectTransform), typeof(LayoutElement));
        unitStatsIconsRoot.transform.SetParent(rootRect, false);
        unitStatsIconsLayout = unitStatsIconsRoot.GetComponent<LayoutElement>();
        unitStatsIconsLayout.minWidth = 52f;
        unitStatsIconsLayout.preferredWidth = 52f;
        unitStatsIconsLayout.minHeight = 52f;
        unitStatsIconsLayout.preferredHeight = 52f;

        GameObject iconObject = new GameObject("local_icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(unitStatsIconsRoot.transform, false);
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        unitStatsLocalIcon = iconObject.GetComponent<Image>();
        unitStatsLocalIcon.preserveAspect = true;
        unitStatsLocalIcon.raycastTarget = false;

        GameObject structureIconObject = new GameObject("local_structure_icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform structureIconRect = structureIconObject.GetComponent<RectTransform>();
        structureIconRect.SetParent(iconObject.transform, false);
        // A estrutura fica estreita e centralizada, deixando o terreno-base legivel.
        structureIconRect.anchorMin = new Vector2(0.30f, 0f);
        structureIconRect.anchorMax = new Vector2(0.70f, 1f);
        structureIconRect.offsetMin = Vector2.zero;
        structureIconRect.offsetMax = Vector2.zero;
        unitStatsStructureIcon = structureIconObject.GetComponent<Image>();
        // Mantem a altura integral e comprime somente a largura da estrutura.
        unitStatsStructureIcon.preserveAspect = false;
        unitStatsStructureIcon.raycastTarget = false;
        unitStatsStructureIcon.enabled = false;

        GameObject textObject = new GameObject("local_details", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        textObject.transform.SetParent(rootRect, false);
        LayoutElement textLayout = textObject.GetComponent<LayoutElement>();
        textLayout.minWidth = 130f;
        textLayout.preferredWidth = 190f;
        VerticalLayoutGroup textGroup = textObject.GetComponent<VerticalLayoutGroup>();
        textGroup.spacing = 2f;
        textGroup.childAlignment = TextAnchor.MiddleLeft;
        textGroup.childControlWidth = true;
        textGroup.childControlHeight = true;
        textGroup.childForceExpandWidth = true;
        textGroup.childForceExpandHeight = false;

        unitStatsLocalText = CreateUnitStatsLocalText("local_name", textObject.transform);
        unitStatsDefenseText = CreateUnitStatsLocalText("local_defense", textObject.transform);
        unitStatsConstructionStockText = CreateUnitStatsLocalText("local_construction_stock", textObject.transform);
        unitStatsConstructionStockText.fontSize = 15f;
        unitStatsConstructionStockText.fontSizeMax = 15f;
        unitStatsConstructionStockText.fontSizeMin = 12f;
        unitStatsConstructionStockText.textWrappingMode = TextWrappingModes.NoWrap;
        LayoutElement stockLayout = unitStatsConstructionStockText.GetComponent<LayoutElement>();
        stockLayout.minHeight = 48f;
        stockLayout.preferredHeight = 48f;
        unitStatsConstructionStockText.gameObject.SetActive(false);
        unitStatsLocalRoot.SetActive(false);
    }

    private void ConfigureUnitStatsLocalIcons(bool separate)
    {
        if (unitStatsIconsRoot == null || unitStatsIconsLayout == null ||
            unitStatsLocalIcon == null || unitStatsStructureIcon == null)
            return;

        unitStatsIconsLayout.minWidth = 52f;
        unitStatsIconsLayout.preferredWidth = 52f;
        unitStatsIconsLayout.minHeight = separate ? 108f : 52f;
        unitStatsIconsLayout.preferredHeight = separate ? 108f : 52f;

        RectTransform localRect = unitStatsLocalIcon.rectTransform;
        RectTransform structureRect = unitStatsStructureIcon.rectTransform;
        structureRect.SetParent(separate ? unitStatsIconsRoot.transform : unitStatsLocalIcon.transform, false);

        localRect.anchorMin = separate ? new Vector2(0f, 0.52f) : Vector2.zero;
        localRect.anchorMax = Vector2.one;
        localRect.offsetMin = Vector2.zero;
        localRect.offsetMax = Vector2.zero;
        unitStatsLocalIcon.preserveAspect = true;

        structureRect.anchorMin = separate ? Vector2.zero : new Vector2(0.30f, 0f);
        structureRect.anchorMax = separate ? new Vector2(1f, 0.48f) : new Vector2(0.70f, 1f);
        structureRect.offsetMin = Vector2.zero;
        structureRect.offsetMax = Vector2.zero;
        unitStatsStructureIcon.preserveAspect = separate;
    }

    private TMP_Text CreateUnitStatsLocalText(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        LayoutElement element = obj.GetComponent<LayoutElement>();
        element.minHeight = 25f;
        element.preferredHeight = 25f;
        TMP_Text text = obj.GetComponent<TMP_Text>();
        if (helperTxt != null && helperTxt.font != null)
            text.font = helperTxt.font;
        text.fontSize = 18f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 18f;
        text.raycastTarget = false;
        return text;
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
        aimConfirmWeaponText = CreateAimConfirmText("aim_confirm_weapon", 18f, 28f);

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

        // Viewport com RectMask2D propria: o scroll da lista corta por baixo do
        // titulo (fixo no modo mira) em vez de deslizar os botoes por cima dele.
        aimTargetsViewportRoot = new GameObject("helper_aim_targets_viewport", typeof(RectTransform), typeof(RectMask2D));
        aimTargetsViewportRect = aimTargetsViewportRoot.GetComponent<RectTransform>();
        aimTargetsViewportRect.SetParent(helperRect, false);
        aimTargetsViewportRect.anchorMin = new Vector2(0.06f, 1f);
        aimTargetsViewportRect.anchorMax = new Vector2(0.94f, 1f);
        aimTargetsViewportRect.pivot = new Vector2(0.5f, 1f);
        aimTargetsViewportRect.anchoredPosition = new Vector2(0f, -48f);
        aimTargetsViewportRect.sizeDelta = new Vector2(0f, 200f);

        aimTargetsRoot = new GameObject("helper_aim_targets", typeof(RectTransform), typeof(VerticalLayoutGroup));
        RectTransform rect = aimTargetsRoot.GetComponent<RectTransform>();
        rect.SetParent(aimTargetsViewportRect, false);
        rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f); rect.anchoredPosition = Vector2.zero;
        VerticalLayoutGroup layout = aimTargetsRoot.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f; layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        aimTargetsRoot.SetActive(false);
    }

    private void RefreshDisembarkActionControls(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        bool isDisembark = data != null && data.Kind == TurnStateManager.HelperPanelKind.Disembark;
        // No passo de CONFIRMAR suprimento nao usamos botoes: as infos (consumo/carroceria)
        // vao pro texto do corpo e o "ADICIONAR A FILA" vira botao de rodape (igual ao
        // CONFIRMAR do ataque). Assim o jogador nao confunde as linhas informativas com acoes
        // e enxerga a acao real. So o passo de SELECAO (escolher unidade) usa a lista de botoes.
        bool isSupply = data != null && data.Kind == TurnStateManager.HelperPanelKind.Supply && !data.SupplyIsConfirmStep;
        bool isMerge = data != null && data.Kind == TurnStateManager.HelperPanelKind.Merge;
        bool isTransfer = data != null && data.Kind == TurnStateManager.HelperPanelKind.Transfer;
        bool active = panelVisible && (isDisembark || isSupply || isMerge || isTransfer);
        if (!active)
        {
            if (disembarkActionsRoot != null) disembarkActionsRoot.SetActive(false);
            disembarkActionsSignature = string.Empty;
            return;
        }

        EnsureDisembarkActionsRoot();
        if (disembarkActionsRoot == null) return;

        StringBuilder sb = new StringBuilder().Append(data.Kind);
        if (isDisembark)
        {
            sb.Append('|').Append(data.DisembarkStep)
                .Append('|').Append(data.DisembarkSelectedPassengerName)
                .Append('|').Append(data.DisembarkSelectedLandingLabel)
                .Append('|').Append(data.HasQueuedDisembarkOrders);
            for (int i = 0; i < data.DisembarkOrderLines.Count; i++)
                sb.Append('|').Append(data.DisembarkOrderLines[i].unitName).Append(data.DisembarkOrderLines[i].terrainName);
            for (int i = 0; i < data.DisembarkPassengerLines.Count; i++)
                sb.Append('|').Append(data.DisembarkPassengerLines[i].index).Append(data.DisembarkPassengerLines[i].unitName);
        }
        else if (isSupply)
        {
            sb.Append('|').Append(data.SupplyIsConfirmStep).Append('|').Append(data.SupplyHasQueuedOrders);
            for (int i = 0; i < data.SupplyCandidateLines.Count; i++)
                sb.Append('|').Append(data.SupplyCandidateLines[i].index)
                    .Append(data.SupplyCandidateLines[i].unitName)
                    .Append(data.SupplyCandidateLines[i].isValid)
                    .Append(data.SupplyCandidateLines[i].invalidReason);
            for (int i = 0; i < data.SupplyTargetLines.Count; i++)
                sb.Append('|').Append(data.SupplyTargetLines[i].unitName).Append(data.SupplyTargetLines[i].gainsLabel);
        }
        else if (isMerge)
        {
            sb.Append('|').Append(data.IsMergeConfirmStep).Append('|').Append(data.SelectedMergeCandidateNumber);
            for (int i = 0; i < data.MergeCandidateLines.Count; i++)
                sb.Append('|').Append(data.MergeCandidateLines[i].index)
                    .Append(data.MergeCandidateLines[i].unitName)
                    .Append(data.MergeCandidateLines[i].isValid)
                    .Append(data.MergeCandidateLines[i].invalidReason);
        }
        else
        {
            sb.Append('|').Append(data.TransferIsConfirmStep)
                .Append('|').Append(data.TransferIsDonationPercentageStep)
                .Append('|').Append(data.TransferDonationPercent)
                .Append('|').Append(data.TransferSelectedLabel);
            for (int i = 0; i < data.TransferCandidateLines.Count; i++)
                sb.Append('|').Append(data.TransferCandidateLines[i].index)
                    .Append(data.TransferCandidateLines[i].unitName)
                    .Append(data.TransferCandidateLines[i].isDonate)
                    .Append(data.TransferCandidateLines[i].isFocused);
            for (int i = 0; i < data.TransferResourceLines.Count; i++)
                sb.Append('|').Append(data.TransferResourceLines[i].supplyName)
                    .Append(data.TransferResourceLines[i].movedAmount);
        }
        string signature = sb.ToString();
        if (signature != disembarkActionsSignature)
        {
            if (isDisembark) RebuildDisembarkActionButtons(data);
            else if (isSupply) RebuildSupplyActionButtons(data);
            else if (isMerge) RebuildMergeActionButtons(data);
            else RebuildTransferActionButtons(data);
            disembarkActionsSignature = signature;
            disembarkLayoutDirty = true;
        }

        disembarkActionsRoot.SetActive(true);
        int focusedIndex = turnStateManager == null ? -1 :
            (isDisembark ? turnStateManager.DisembarkPassengerFocusIndex :
             isSupply ? turnStateManager.SupplyHelperFocusIndex : turnStateManager.MergeHelperFocusIndex);
        if (isTransfer && turnStateManager != null)
            focusedIndex = turnStateManager.TransferHelperFocusIndex;
        for (int i = 0; i < disembarkActionButtons.Count; i++)
        {
            Button button = disembarkActionButtons[i];
            int buttonFocus = i < disembarkActionFocusIndices.Count ? disembarkActionFocusIndices[i] : -1;
            if (button != null && button.interactable)
            {
                bool focused = (isDisembark ? data.DisembarkStep == 0 :
                                isSupply ? !data.SupplyIsConfirmStep :
                                isMerge ? !data.IsMergeConfirmStep : true) &&
                               buttonFocus == focusedIndex;
                bool invalidSupply = isSupply && buttonFocus >= 0 &&
                                     buttonFocus < data.SupplyCandidateLines.Count &&
                                     !data.SupplyCandidateLines[buttonFocus].isValid;
                bool invalidMerge = isMerge && !data.IsMergeConfirmStep && buttonFocus >= 0 &&
                                    buttonFocus < data.MergeCandidateLines.Count &&
                                    !data.MergeCandidateLines[buttonFocus].isValid;
                if (invalidSupply || invalidMerge)
                {
                    button.GetComponent<Image>().color = focused
                        ? new Color(0.28f, 0.28f, 0.28f, 0.98f)
                        : new Color(0.12f, 0.12f, 0.12f, 0.92f);
                    button.GetComponentInChildren<TMP_Text>(true).color = focused
                        ? new Color(0.78f, 0.78f, 0.78f, 1f)
                        : Color.gray;
                }
                else
                    TintScriptButtonToTeam(button, focused);
            }
        }
        ApplyFooterButtonFocus(cancelActionImage, cancelActionLabel,
            turnStateManager != null && (isDisembark
                ? turnStateManager.DisembarkPassengerCancelFocused
                : isSupply ? turnStateManager.SupplyHelperCancelFocused
                : isMerge ? turnStateManager.MergeHelperCancelFocused
                : turnStateManager.TransferHelperCancelFocused));
        if (helperTxt != null) helperTxt.enabled = false;
        if (panelHelper == gameObject && selfPanelCanvasGroup != null)
        {
            selfPanelCanvasGroup.interactable = true;
            selfPanelCanvasGroup.blocksRaycasts = true;
        }
    }

    private void EnsureDisembarkActionsRoot()
    {
        if (!Application.isPlaying || disembarkActionsRoot != null || helperRect == null) return;
        disembarkActionsRoot = new GameObject("helper_disembark_actions", typeof(RectTransform), typeof(VerticalLayoutGroup));
        RectTransform rect = disembarkActionsRoot.GetComponent<RectTransform>();
        rect.SetParent(helperRect, false);
        rect.anchorMin = new Vector2(0.06f, 1f); rect.anchorMax = new Vector2(0.94f, 1f);
        rect.pivot = new Vector2(0.5f, 1f); rect.anchoredPosition = new Vector2(0f, -48f);
        VerticalLayoutGroup layout = disembarkActionsRoot.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f; layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true; layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        disembarkActionsRoot.SetActive(false);
    }

    private void RebuildDisembarkActionButtons(TurnStateManager.HelperPanelData data)
    {
        for (int i = disembarkActionButtons.Count - 1; i >= 0; i--)
            if (disembarkActionButtons[i] != null) Destroy(disembarkActionButtons[i].gameObject);
        disembarkActionButtons.Clear();
        disembarkActionFocusIndices.Clear();

        for (int i = 0; i < data.DisembarkOrderLines.Count; i++)
        {
            TurnStateManager.HelperDisembarkOrderLine order = data.DisembarkOrderLines[i];
            CreateDisembarkButton($"{order.index} - {order.unitName} → {order.terrainName}", null, false, -1,
                order.unitSprite, order.unitColor, order.localSprite, order.localColor);
        }

        if (data.DisembarkStep == 0)
        {
            for (int i = 0; i < data.DisembarkPassengerLines.Count; i++)
            {
                TurnStateManager.HelperDisembarkPassengerLine passenger = data.DisembarkPassengerLines[i];
                int selectionNumber = passenger.index;
                CreateDisembarkButton($"{passenger.index} - {passenger.unitName} ({passenger.stats})",
                    () => turnStateManager?.TrySelectDisembarkPassengerFromPointer(selectionNumber), true, i,
                    flexibleTextHeight: true);
            }
            if (data.HasQueuedDisembarkOrders)
                CreateDisembarkButton("EXECUTAR FILA", () => turnStateManager?.TryExecuteDisembarkQueueFromPointer(), true,
                    data.DisembarkPassengerLines.Count);
        }
        else
        {
            string passenger = string.IsNullOrWhiteSpace(data.DisembarkSelectedPassengerName)
                ? "Unidade" : data.DisembarkSelectedPassengerName;
            string landing = string.IsNullOrWhiteSpace(data.DisembarkSelectedLandingLabel)
                ? "Local não selecionado" : data.DisembarkSelectedLandingLabel;
            CreateDisembarkButton($"{passenger} → {landing}", null, false, -1);
            string action = data.DisembarkStep == 1 ? "CONFIRMAR LOCAL" : "ADICIONAR À FILA";
            CreateDisembarkButton(action, () => turnStateManager?.TryAdvanceDisembarkFromPointer(), true, -1);
        }

        disembarkActionsRoot.GetComponent<RectTransform>().sizeDelta =
            new Vector2(0f, GetDisembarkActionsPreferredHeight());
    }

    private void RebuildSupplyActionButtons(TurnStateManager.HelperPanelData data)
    {
        for (int i = disembarkActionButtons.Count - 1; i >= 0; i--)
            if (disembarkActionButtons[i] != null) Destroy(disembarkActionButtons[i].gameObject);
        disembarkActionButtons.Clear();
        disembarkActionFocusIndices.Clear();

        if (!data.SupplyIsConfirmStep)
        {
            for (int i = 0; i < data.SupplyTargetLines.Count; i++)
            {
                TurnStateManager.HelperSupplyTargetLine queued = data.SupplyTargetLines[i];
                if (queued == null || queued.isFocused)
                    continue;
                CreateDisembarkButton($"{queued.index} - {queued.unitName} | {queued.gainsLabel} | ${queued.estimatedCost}",
                    null, false, -1, queued.unitSprite, queued.unitColor);
            }
            for (int i = 0; i < data.SupplyCandidateLines.Count; i++)
            {
                TurnStateManager.HelperSupplyCandidateLine candidate = data.SupplyCandidateLines[i];
                int selectionNumber = candidate.index;
                int invalidIndex = i - (data.SupplyCandidateLines.Count - CountInvalidSupplyCandidates(data));
                UnityEngine.Events.UnityAction action = candidate.isValid
                    ? () => turnStateManager?.TrySelectSupplyCandidateFromPointer(selectionNumber)
                    : () => turnStateManager?.TrySelectInvalidSupplyCandidateFromPointer(invalidIndex);
                CreateDisembarkButton($"{candidate.index} - {candidate.unitName} ({candidate.stats})",
                    action, true, i,
                    candidate.unitSprite, candidate.unitColor);
                if (!candidate.isValid && disembarkActionButtons.Count > 0)
                {
                    Button invalidButton = disembarkActionButtons[disembarkActionButtons.Count - 1];
                    invalidButton.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
                    invalidButton.GetComponentInChildren<TMP_Text>(true).color = Color.gray;
                }
            }
            if (data.SupplyHasQueuedOrders)
                CreateDisembarkButton("EXECUTAR FILA", () => turnStateManager?.TryExecuteSupplyQueueFromPointer(), true,
                    data.SupplyCandidateLines.Count);
        }
        // O passo de CONFIRMAR suprimento nao passa mais por aqui: as infos (consumo/carroceria)
        // sao renderizadas como texto no corpo (BuildSupplyBody) e o "ADICIONAR A FILA" vira botao
        // de rodape (RefreshExecuteCommandServiceControl).

        disembarkActionsRoot.GetComponent<RectTransform>().sizeDelta =
            new Vector2(0f, disembarkActionButtons.Count * (DisembarkActionButtonHeight + 4f));
    }

    private static int CountInvalidSupplyCandidates(TurnStateManager.HelperPanelData data)
    {
        int count = 0;
        for (int i = 0; i < data.SupplyCandidateLines.Count; i++)
            if (data.SupplyCandidateLines[i] != null && !data.SupplyCandidateLines[i].isValid)
                count++;
        return count;
    }

    private void RebuildMergeActionButtons(TurnStateManager.HelperPanelData data)
    {
        for (int i = disembarkActionButtons.Count - 1; i >= 0; i--)
            if (disembarkActionButtons[i] != null) Destroy(disembarkActionButtons[i].gameObject);
        disembarkActionButtons.Clear();
        disembarkActionFocusIndices.Clear();

        if (!data.IsMergeConfirmStep)
        {
            for (int i = 0; i < data.MergeCandidateLines.Count; i++)
            {
                TurnStateManager.HelperMergeCandidateLine candidate = data.MergeCandidateLines[i];
                int selectionNumber = candidate.index;
                CreateDisembarkButton($"{candidate.index} - {candidate.unitName} ({candidate.stats})",
                    () => turnStateManager?.TrySelectMergeCandidateFromPointer(selectionNumber), true, i,
                    candidate.unitSprite, candidate.unitColor);
                if (!candidate.isValid && disembarkActionButtons.Count > 0)
                {
                    Button invalidButton = disembarkActionButtons[disembarkActionButtons.Count - 1];
                    invalidButton.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
                    invalidButton.GetComponentInChildren<TMP_Text>(true).color = Color.gray;
                }
            }
        }
        else
        {
            TurnStateManager.HelperMergeCandidateLine selected = null;
            for (int i = 0; i < data.MergeCandidateLines.Count; i++)
                if (data.MergeCandidateLines[i] != null &&
                    data.MergeCandidateLines[i].index == data.SelectedMergeCandidateNumber)
                {
                    selected = data.MergeCandidateLines[i];
                    break;
                }
            string summary = selected != null
                ? $"{selected.unitName} ({selected.stats})"
                : data.SelectedMergeCandidateName;
            CreateDisembarkButton(summary, null, false, -1,
                selected != null ? selected.unitSprite : null,
                selected != null ? selected.unitColor : Color.white);
            ConfigureLastDisembarkRowLayout(82f, 18f);
            if (!string.IsNullOrWhiteSpace(data.MergeConfirmPreview))
            {
                CreateDisembarkButton($"RESULTADO: {data.MergeConfirmPreview}", null, false, -1);
                ConfigureLastDisembarkRowLayout(76f, 17f);
            }
            CreateDisembarkButton("CONFIRMAR FUSÃO", () => turnStateManager?.TryAdvanceMergeFromPointer(), true, -1);
            ConfigureLastDisembarkRowLayout(58f, 20f);
        }

        disembarkActionsRoot.GetComponent<RectTransform>().sizeDelta =
            new Vector2(0f, GetDisembarkActionsPreferredHeight());
    }

    private void ConfigureLastDisembarkRowLayout(float height, float fontSizeMax)
    {
        if (disembarkActionButtons.Count <= 0)
            return;
        Button button = disembarkActionButtons[disembarkActionButtons.Count - 1];
        if (button == null)
            return;
        LayoutElement element = button.GetComponent<LayoutElement>();
        if (element != null)
        {
            element.minHeight = height;
            element.preferredHeight = height;
        }
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = fontSizeMax;
            label.lineSpacing = 4f;
        }
    }

    private float GetDisembarkActionsPreferredHeight()
    {
        float height = 0f;
        for (int i = 0; i < disembarkActionButtons.Count; i++)
        {
            Button button = disembarkActionButtons[i];
            LayoutElement element = button != null ? button.GetComponent<LayoutElement>() : null;
            height += element != null ? Mathf.Max(DisembarkActionButtonHeight, element.preferredHeight) : DisembarkActionButtonHeight;
            if (i < disembarkActionButtons.Count - 1)
                height += 4f;
        }
        return height;
    }

    private void RebuildTransferActionButtons(TurnStateManager.HelperPanelData data)
    {
        for (int i = disembarkActionButtons.Count - 1; i >= 0; i--)
            if (disembarkActionButtons[i] != null) Destroy(disembarkActionButtons[i].gameObject);
        disembarkActionButtons.Clear();
        disembarkActionFocusIndices.Clear();

        if (data.TransferIsDonationPercentageStep)
        {
            TurnStateManager.HelperTransferCandidateLine selected = null;
            for (int i = 0; i < data.TransferCandidateLines.Count; i++)
                if (data.TransferCandidateLines[i] != null && data.TransferCandidateLines[i].isFocused)
                {
                    selected = data.TransferCandidateLines[i];
                    break;
                }

            string target = selected != null ? selected.unitName : data.TransferSelectedLabel;
            CreateDisembarkButton($"DOAR → {target}", null, false, -1,
                selected != null ? selected.targetSprite : null,
                selected != null ? selected.targetColor : Color.white);
            TintLastSupplyInformationRow(currentTeamColor);

            int[] percentages = { 25, 50, 75, 100 };
            for (int i = 0; i < percentages.Length; i++)
            {
                int percentageIndex = i;
                CreateDisembarkButton($"{percentages[i]}%",
                    () => turnStateManager?.TrySelectTransferDonationPercentageFromPointer(percentageIndex),
                    true, i);
            }
        }
        else if (!data.TransferIsConfirmStep)
        {
            for (int i = 0; i < data.TransferCandidateLines.Count; i++)
            {
                TurnStateManager.HelperTransferCandidateLine candidate = data.TransferCandidateLines[i];
                int optionIndex = i;
                string mode = candidate.isDonate ? "DOAR" : "RECEBER";
                CreateDisembarkButton($"{candidate.index} - {mode} → {candidate.unitName}",
                    () => turnStateManager?.TrySelectTransferOptionFromPointer(optionIndex), true, i,
                    candidate.targetSprite, candidate.targetColor);
            }
        }
        else
        {
            TurnStateManager.HelperTransferCandidateLine selected = null;
            for (int i = 0; i < data.TransferCandidateLines.Count; i++)
                if (data.TransferCandidateLines[i] != null && data.TransferCandidateLines[i].isFocused)
                {
                    selected = data.TransferCandidateLines[i];
                    break;
                }
            string mode = selected != null && selected.isDonate ? "DOAR" : "RECEBER";
            if (selected != null && selected.isDonate)
                mode += $" {data.TransferDonationPercent}%";
            string target = selected != null ? selected.unitName : data.TransferSelectedLabel;
            CreateDisembarkButton($"{mode} → {target}", null, false, -1,
                selected != null ? selected.targetSprite : null,
                selected != null ? selected.targetColor : Color.white);
            TintLastSupplyInformationRow(currentTeamColor);

            for (int i = 0; i < data.TransferResourceLines.Count; i++)
            {
                TurnStateManager.HelperTransferResourceLine line = data.TransferResourceLines[i];
                if (line == null) continue;
                string sourceBefore = line.sourceIsInfinite ? "INF" : line.sourceBefore.ToString();
                string sourceAfter = line.sourceIsInfinite ? "INF" : line.sourceAfter.ToString();
                string destinationBefore = line.destinationIsInfinite ? "INF" : line.destinationBefore.ToString();
                string destinationAfter = line.destinationIsInfinite ? "INF" : line.destinationAfter.ToString();
                CreateDisembarkButton(
                    $"{line.supplyName}: {sourceBefore} - {line.movedAmount} → {sourceAfter} | {destinationBefore} + {line.movedAmount} → {destinationAfter}",
                    null, false, -1);
                TintLastSupplyInformationRow(currentTeamColor);
            }
            CreateDisembarkButton("CONFIRMAR TRANSFERÊNCIA",
                () => turnStateManager?.TryConfirmTransferFromPointer(), true, 0);
        }

        disembarkActionsRoot.GetComponent<RectTransform>().sizeDelta =
            new Vector2(0f, disembarkActionButtons.Count * (DisembarkActionButtonHeight + 4f));
    }

    private void TintLastSupplyInformationRow(Color textColor)
    {
        if (disembarkActionButtons.Count <= 0)
            return;
        Button button = disembarkActionButtons[disembarkActionButtons.Count - 1];
        if (button == null)
            return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = textColor;
    }

    private void CreateDisembarkButton(
        string text,
        UnityEngine.Events.UnityAction action,
        bool interactable,
        int focusIndex,
        Sprite leftSprite = null,
        Color? leftColor = null,
        Sprite rightSprite = null,
        Color? rightColor = null,
        bool flexibleTextHeight = false)
    {
        GameObject obj = new GameObject("button_disembark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.transform.SetParent(disembarkActionsRoot.transform, false);
        LayoutElement element = obj.GetComponent<LayoutElement>();
        element.minHeight = DisembarkActionButtonHeight; element.preferredHeight = DisembarkActionButtonHeight;
        Button button = obj.GetComponent<Button>();
        button.interactable = interactable;
        if (action != null) button.onClick.AddListener(action);
        GameObject labelObj = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.SetParent(obj.transform, false); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        float iconReserve = leftSprite != null || rightSprite != null ? 48f : 8f;
        labelRect.offsetMin = new Vector2(iconReserve, 2f); labelRect.offsetMax = new Vector2(-iconReserve, -2f);
        TMP_Text label = labelObj.GetComponent<TMP_Text>();
        label.text = text; label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true; label.fontSizeMin = 11f; label.fontSizeMax = 18f; label.raycastTarget = false;
        ConfigureMobileActionLabel(label);
        if (flexibleTextHeight)
        {
            // No passo ESCOLHER UNIDADE, nomes e estatisticas podem quebrar em mais
            // de uma linha. O botao acompanha o texto e preserva um respiro vertical.
            label.enableWordWrapping = true;
            label.lineSpacing = 4f;
            float panelWidth = helperRect != null ? helperRect.rect.width : 320f;
            float buttonWidth = panelWidth * 0.88f;
            float availableTextWidth = Mathf.Max(80f, buttonWidth - iconReserve * 2f);
            float preferredTextHeight = label.GetPreferredValues(text, availableTextWidth, 0f).y;
            float flexibleHeight = Mathf.Max(DisembarkActionButtonHeight, preferredTextHeight + 20f);
            element.minHeight = flexibleHeight;
            element.preferredHeight = flexibleHeight;
            labelRect.offsetMin = new Vector2(iconReserve, 10f);
            labelRect.offsetMax = new Vector2(-iconReserve, -10f);
        }
        if (interactable)
            TintScriptButtonToTeam(button, false);
        else
        {
            obj.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.88f);
            label.color = Color.gray;
        }

        if (leftSprite != null)
            CreateDisembarkRowIcon(obj.transform, "unit_icon", leftSprite, leftColor ?? Color.white, true);
        if (rightSprite != null)
            CreateDisembarkRowIcon(obj.transform, "local_icon", rightSprite, rightColor ?? Color.white, false);
        disembarkActionButtons.Add(button);
        disembarkActionFocusIndices.Add(focusIndex);
    }

    private static void CreateDisembarkRowIcon(
        Transform parent,
        string objectName,
        Sprite sprite,
        Color color,
        bool alignLeft)
    {
        GameObject iconObj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = iconObj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(alignLeft ? 0f : 1f, 0.5f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(alignLeft ? 0f : 1f, 0.5f);
        rect.anchoredPosition = new Vector2(alignLeft ? 5f : -5f, 0f);
        rect.sizeDelta = new Vector2(40f, 40f);
        Image image = iconObj.GetComponent<Image>();
        image.sprite = sprite;
        // Sprites usados como miniaturas no helper devem permanecer legiveis mesmo quando
        // o renderer original estiver escurecido por FOW, seleção ou estado provisório.
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void RebuildAimTargetButtons(List<TurnStateManager.HelperAimTargetLine> lines, bool embark)
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
            GameObject obj = new GameObject($"button_aim_target_{objSuffix}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Button), typeof(LayoutElement));
            obj.transform.SetParent(aimTargetsRoot.transform, false);
            obj.GetComponent<Image>().color = FooterButtonIdleColor;
            Outline focusBorder = obj.GetComponent<Outline>();
            focusBorder.effectDistance = new Vector2(2f, -2f);
            focusBorder.useGraphicAlpha = false;
            focusBorder.enabled = false;
            LayoutElement element = obj.GetComponent<LayoutElement>(); element.minHeight = AimTargetButtonHeight; element.preferredHeight = AimTargetButtonHeight;
            Button button = obj.GetComponent<Button>();
            obj.AddComponent<PanelHelperAimScrollDragHandle>().Configure(this);
            if (isCancel)
                button.onClick.AddListener(() => cursorController?.TryCancelCurrentActionFromPointer());
            else if (embark)
                button.onClick.AddListener(() => turnStateManager?.TrySelectEmbarkTargetFromPointer(targetIndex));
            else
                button.onClick.AddListener(() => turnStateManager?.TrySelectMirandoTargetFromPointer(targetIndex));
            GameObject labelObj = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObj.GetComponent<RectTransform>(); labelRect.SetParent(obj.transform, false);
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(line.unitSprite != null ? 48f : 0f, 0f);
            labelRect.offsetMax = Vector2.zero;
            TMP_Text label = labelObj.GetComponent<TMP_Text>();
            if (isCancel)
                label.text = line.unitName;
            else
            {
                // Linha 1: alvo/HP. Linha 2: camada/local. Linha 3: arma concreta e
                // categoria. O motivo detalhado da opcao invalida permanece no
                // PanelDialog quando o jogador tenta confirma-la.
                string head = $"{i + 1} - {line.unitName} (Hp: {line.hp})";
                string targetContext = string.IsNullOrWhiteSpace(line.terrainLabel)
                    ? head
                    : $"{head}\n{line.terrainLabel}";
                string weaponLine = string.IsNullOrWhiteSpace(line.weaponName)
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(line.weaponCategoryLabel)
                        ? line.weaponName
                        : $"{line.weaponName}  [{line.weaponCategoryLabel}]";
                label.text = string.IsNullOrWhiteSpace(weaponLine)
                    ? targetContext
                    : $"{targetContext}\n{weaponLine}";
            }
            label.fontStyle = FontStyles.Bold; label.color = FooterLabelIdleColor; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            // Auto-encolhe se o nome for grande, pra nao estourar a largura do botao.
            label.enableAutoSizing = true; label.fontSizeMin = 12f; label.fontSizeMax = 20f;
            ConfigureMobileActionLabel(label);

            // O texto pode ocupar mais de duas linhas (nome/HP + terreno comprido).
            // Mede na largura real disponivel e deixa o VerticalLayoutGroup empurrar
            // naturalmente os botoes seguintes, sem encolher a fonte.
            float panelWidth = helperRect != null ? helperRect.rect.width : 320f;
            float horizontalInset = line.unitSprite != null ? 48f : 0f;
            float availableTextWidth = Mathf.Max(80f, panelWidth * 0.88f - horizontalInset);
            float preferredTextHeight = label.GetPreferredValues(label.text, availableTextWidth, 0f).y;
            float buttonHeight = Mathf.Max(AimTargetButtonHeight, preferredTextHeight + 12f);
            element.minHeight = buttonHeight;
            element.preferredHeight = buttonHeight;
            if (!isCancel && line.unitSprite != null)
                CreateDisembarkRowIcon(obj.transform, "unit_icon", line.unitSprite, line.unitColor, true);
            aimTargetButtons.Add(button);
        }
        aimTargetsRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, GetAimTargetsPreferredHeight());
    }

    private float GetAimTargetsPreferredHeight()
    {
        float height = 0f;
        for (int i = 0; i < aimTargetButtons.Count; i++)
        {
            Button button = aimTargetButtons[i];
            if (button == null)
                continue;
            LayoutElement element = button.GetComponent<LayoutElement>();
            height += element != null ? Mathf.Max(AimTargetButtonHeight, element.preferredHeight) : AimTargetButtonHeight;
            if (i < aimTargetButtons.Count - 1)
                height += 4f;
        }
        return height;
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
            bool unavailable = i < data.ShoppingLines.Count &&
                               data.ShoppingLines[i] != null &&
                               !data.ShoppingLines[i].isCancel &&
                               (!data.ShoppingLines[i].canAfford || !data.ShoppingLines[i].requirementMet);
            // Mantem itens bloqueados clicaveis para que a tentativa mostre o motivo no PanelDialog.
            shoppingButton.interactable = true;
            ApplyFooterButtonFocus(
                shoppingButton.GetComponent<Image>(),
                shoppingButton.GetComponentInChildren<TMP_Text>(true),
                i == focusedShoppingIndex);
            if (unavailable)
            {
                Image image = shoppingButton.GetComponent<Image>();
                TMP_Text label = shoppingButton.GetComponentInChildren<TMP_Text>(true);
                if (image != null)
                    image.color = new Color(0.12f, 0.12f, 0.12f, 0.88f);
                if (label != null)
                    label.color = new Color(0.52f, 0.52f, 0.52f, 1f);
            }
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
        bool menuDeleteActive = mainMenuLoadPanelController != null &&
                                mainMenuLoadPanelController.IsDeleteConfirmationOpen;
        bool menuQuitActive = mainMenuPanel != null && mainMenuPanel.IsQuitConfirmationOpen;
        bool menuAboutActive = mainMenuPanel != null && mainMenuPanel.IsAboutOpen;
        bool newGameWizardActive = mainMenuPanel != null && mainMenuPanel.IsNewGameWizardOpen;
        bool tutorialColorActive = mainMenuTutorialPanelController != null && mainMenuTutorialPanelController.IsColorStepOpen;
        bool battleExitActive = battleMapMenuController != null && battleMapMenuController.IsExitConfirmationOpen;
        bool battleSurrenderActive = battleMapMenuController != null && battleMapMenuController.IsSurrenderConfirmationOpen;
        bool battleEndTurnActive = battleMapMenuController != null && battleMapMenuController.IsEndTurnConfirmationOpen;
        bool battleLayerActive = battleMapMenuController != null && battleMapMenuController.IsLayerSelectionOpen;
        bool savePromptActive = saveGameManager != null &&
                                (saveGameManager.IsPersistenceSlotSelectionActive ||
                                 saveGameManager.IsPersistenceOverwriteConfirmationActive);
        bool active = panelVisible && (menuDeleteActive || menuQuitActive || menuAboutActive || newGameWizardActive || tutorialColorActive || battleExitActive || battleSurrenderActive || battleEndTurnActive || battleLayerActive || savePromptActive);
        if (!active)
        {
            if (persistenceActionsRoot != null)
                persistenceActionsRoot.SetActive(false);
            persistenceActionsSignature = string.Empty;
            return;
        }

        EnsurePersistenceActionsRoot();
        string signature = menuDeleteActive ? "main_menu_delete" :
            menuQuitActive ? "main_menu_quit" :
            menuAboutActive ? "main_menu_about" :
            newGameWizardActive ? $"new_game_{mainMenuPanel.NewGameWizardStep}" :
            tutorialColorActive ? "tutorial_color" :
            battleExitActive ? "battle_exit" :
            battleSurrenderActive ? "battle_surrender" :
            battleEndTurnActive ? "battle_end_turn" :
            battleLayerActive ? "battle_layer" :
            saveGameManager.IsPersistenceOverwriteConfirmationActive
            ? "overwrite"
            : string.Join("|", saveGameManager.GetPersistenceSlotButtonLabel(1),
                saveGameManager.GetPersistenceSlotButtonLabel(2), saveGameManager.GetPersistenceSlotButtonLabel(3));
        if (signature != persistenceActionsSignature)
        {
            RebuildPersistenceActionButtons(menuDeleteActive, menuQuitActive, menuAboutActive, newGameWizardActive, tutorialColorActive, battleExitActive, battleSurrenderActive, battleEndTurnActive, battleLayerActive);
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
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        persistenceActionsRoot.SetActive(false);
    }

    private void RebuildPersistenceActionButtons(bool menuDeleteActive, bool menuQuitActive, bool menuAboutActive, bool newGameWizardActive, bool tutorialColorActive, bool battleExitActive, bool battleSurrenderActive, bool battleEndTurnActive, bool battleLayerActive)
    {
        // Destroi todos os filhos (botoes, spacers de rodape e detalhes de confirmacao) de uma vez.
        RectTransform persistenceRootRect = persistenceActionsRoot.GetComponent<RectTransform>();
        for (int i = persistenceRootRect.childCount - 1; i >= 0; i--)
            Destroy(persistenceRootRect.GetChild(i).gameObject);
        persistenceActionButtons.Clear();
        persistenceActionImages.Clear();
        persistenceActionLabels.Clear();
        persistenceActionTeamColors.Clear();
        persistenceActionUsesTeamColor.Clear();
        persistenceConfirmationDetails = null;
        persistenceFooterSpacerHeight = 0f;

        if (menuDeleteActive)
        {
            CreatePersistenceButton("CONFIRMAR EXCLUSÃO", () => mainMenuLoadPanelController?.ConfirmDeleteFromPointer());
            CreatePersistenceButton("CANCELAR", () => mainMenuLoadPanelController?.CancelDeleteFromPointer());
        }
        else if (menuQuitActive)
        {
            CreatePersistenceButton("SAIR PARA O WINDOWS", () => mainMenuPanel?.ConfirmQuitFromPointer());
            CreatePersistenceButton("CANCELAR", () => mainMenuPanel?.CancelQuitFromPointer());
        }
        else if (menuAboutActive)
        {
            CreateAboutDetails(mainMenuPanel.AboutBody);
            CreatePersistenceButton("OK", () => mainMenuPanel?.ConfirmAboutFromPointer());
        }
        else if (newGameWizardActive)
        {
            if (mainMenuPanel.IsNewGameWizardConfirmStep)
                CreateNewGameConfirmationDetails(mainMenuPanel.GetNewGameWizardConfirmationSummary());

            int count = mainMenuPanel.GetNewGameWizardOptionCount();
            for (int i = 0; i < count; i++)
            {
                // A ultima opcao de todo passo e sempre CANCELAR/VOLTAR: destaca ela no rodape.
                if (count > 1 && i == count - 1)
                    CreatePersistenceFooterSpacer(PersistenceFooterGap);
                int selected = i;
                bool hasTeamColor = mainMenuPanel.TryGetNewGameWizardOptionColor(i, out Color teamColor);
                CreatePersistenceButton(mainMenuPanel.GetNewGameWizardOptionLabel(i),
                    () => mainMenuPanel?.InvokeNewGameWizardOption(selected),
                    hasTeamColor ? teamColor : (Color?)null);
            }
        }
        else if (tutorialColorActive)
        {
            int count = mainMenuTutorialPanelController.GetColorStepOptionCount();
            for (int i = 0; i < count; i++)
            {
                // A ultima opcao e sempre VOLTAR: destaca ela no rodape.
                if (count > 1 && i == count - 1)
                    CreatePersistenceFooterSpacer(PersistenceFooterGap);
                int selected = i;
                bool hasTeamColor = mainMenuTutorialPanelController.TryGetColorStepOptionColor(i, out Color teamColor);
                CreatePersistenceButton(mainMenuTutorialPanelController.GetColorStepOptionLabel(i),
                    () => mainMenuTutorialPanelController?.InvokeColorStepOption(selected),
                    hasTeamColor ? teamColor : (Color?)null);
            }
        }
        else if (battleExitActive)
        {
            CreatePersistenceButton("VOLTAR AO MENU PRINCIPAL", () => battleMapMenuController?.InvokeExitConfirmationOption(0));
            CreatePersistenceButton("SAIR PARA O WINDOWS", () => battleMapMenuController?.InvokeExitConfirmationOption(1));
            CreatePersistenceButton("CANCELAR", () => battleMapMenuController?.InvokeExitConfirmationOption(2));
        }
        else if (battleSurrenderActive)
        {
            CreatePersistenceButton("CONFIRMAR RENDIÇÃO", () => battleMapMenuController?.InvokeSurrenderConfirmationOption(0));
            CreatePersistenceButton("CANCELAR", () => battleMapMenuController?.InvokeSurrenderConfirmationOption(1));
        }
        else if (battleEndTurnActive)
        {
            CreatePersistenceButton("PASSAR A VEZ", () => battleMapMenuController?.InvokeEndTurnConfirmationOption(0));
            CreatePersistenceButton("CANCELAR", () => battleMapMenuController?.InvokeEndTurnConfirmationOption(1));
        }
        else if (battleLayerActive)
        {
            int count = battleMapMenuController.GetLayerSelectionOptionCount();
            for (int i = 0; i < count; i++)
            {
                if (i == count - 1)
                    CreatePersistenceFooterSpacer(PersistenceFooterGap);
                int selected = i;
                CreatePersistenceButton(battleMapMenuController.GetLayerSelectionOptionLabel(i),
                    () => battleMapMenuController?.InvokeLayerSelectionOption(selected));
            }
        }
        else if (saveGameManager.IsPersistenceOverwriteConfirmationActive)
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

        LayoutElement detailsLayout = persistenceConfirmationDetails != null
            ? persistenceConfirmationDetails.GetComponent<LayoutElement>()
            : null;
        float detailsHeight = detailsLayout != null ? detailsLayout.preferredHeight + 10f : 0f;
        persistenceActionsRoot.GetComponent<RectTransform>().sizeDelta =
            new Vector2(0f, persistenceActionButtons.Count * (PersistenceActionButtonHeight + 10f) + detailsHeight + persistenceFooterSpacerHeight);
    }

    private void CreateNewGameConfirmationDetails(string text)
    {
        persistenceConfirmationDetails = new GameObject(
            "new_game_confirmation_details",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        persistenceConfirmationDetails.transform.SetParent(persistenceActionsRoot.transform, false);

        LayoutElement element = persistenceConfirmationDetails.GetComponent<LayoutElement>();
        element.minHeight = PersistenceConfirmationDetailsHeight;
        element.preferredHeight = PersistenceConfirmationDetailsHeight;

        TMP_Text label = persistenceConfirmationDetails.GetComponent<TMP_Text>();
        label.text = text ?? string.Empty;
        label.richText = true;
        label.fontSize = 18f;
        label.fontStyle = FontStyles.Bold;
        label.color = FooterLabelIdleColor;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;

        float availableWidth = helperRect != null
            ? Mathf.Max(1f, helperRect.rect.width * 0.88f)
            : 300f;
        float requiredHeight = label.GetPreferredValues(label.text, availableWidth, Mathf.Infinity).y + 6f;
        float detailsHeight = Mathf.Max(PersistenceConfirmationDetailsHeight, requiredHeight);
        element.minHeight = detailsHeight;
        element.preferredHeight = detailsHeight;
    }

    private void CreateAboutDetails(string text)
    {
        const float aboutHeight = 330f;
        persistenceConfirmationDetails = new GameObject(
            "about_game_details",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        persistenceConfirmationDetails.transform.SetParent(persistenceActionsRoot.transform, false);

        LayoutElement element = persistenceConfirmationDetails.GetComponent<LayoutElement>();
        element.minHeight = aboutHeight;
        element.preferredHeight = aboutHeight;

        TMP_Text label = persistenceConfirmationDetails.GetComponent<TMP_Text>();
        label.text = text ?? string.Empty;
        label.richText = true;
        label.fontSize = 20f;
        label.fontStyle = FontStyles.Normal;
        label.color = FooterLabelIdleColor;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
    }

    // Destaca o botao de save/load em foco (mesmo visual do preview do Servico do Comando).
    private void RefreshPersistencePromptFocusHighlight()
    {
        int focus = mainMenuLoadPanelController != null && mainMenuLoadPanelController.IsDeleteConfirmationOpen
            ? mainMenuLoadPanelController.DeleteConfirmationFocusIndex
            : (mainMenuPanel != null && mainMenuPanel.IsQuitConfirmationOpen
                ? mainMenuPanel.QuitConfirmationFocusIndex
                : (mainMenuPanel != null && mainMenuPanel.IsAboutOpen
                    ? 0
                : (mainMenuPanel != null && mainMenuPanel.IsNewGameWizardOpen
                    ? mainMenuPanel.NewGameWizardFocusIndex
                    : (mainMenuTutorialPanelController != null && mainMenuTutorialPanelController.IsColorStepOpen
                    ? mainMenuTutorialPanelController.ColorStepFocusIndex
                    : (battleMapMenuController != null && battleMapMenuController.IsExitConfirmationOpen
                        ? battleMapMenuController.ExitConfirmationFocusIndex
                        : (battleMapMenuController != null && battleMapMenuController.IsSurrenderConfirmationOpen
                            ? battleMapMenuController.SurrenderConfirmationFocusIndex
                            : (battleMapMenuController != null && battleMapMenuController.IsEndTurnConfirmationOpen
                                ? battleMapMenuController.EndTurnConfirmationFocusIndex
                                : (battleMapMenuController != null && battleMapMenuController.IsLayerSelectionOpen
                                    ? battleMapMenuController.LayerSelectionFocusIndex
                                : (saveGameManager != null ? saveGameManager.PersistencePromptFocusIndex : -1)))))))));
        for (int i = 0; i < persistenceActionButtons.Count; i++)
        {
            Image image = i < persistenceActionImages.Count ? persistenceActionImages[i] : null;
            TMP_Text label = i < persistenceActionLabels.Count ? persistenceActionLabels[i] : null;
            bool focused = i == focus;
            if (i < persistenceActionUsesTeamColor.Count && persistenceActionUsesTeamColor[i])
            {
                Color team = persistenceActionTeamColors[i];
                if (image != null) image.color = TeamButtonBackground(team, focused);
                if (label != null) label.color = TeamButtonLabel(team, focused);
            }
            else
                ApplyFooterButtonFocus(image, label, focused);
        }
    }

    // Elemento invisivel de altura fixa que abre um respiro no VerticalLayoutGroup, empurrando
    // o botao de cancelar/voltar pra baixo (visual de rodape destacado).
    private void CreatePersistenceFooterSpacer(float height)
    {
        GameObject spacer = new GameObject("persistence_footer_spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(persistenceActionsRoot.transform, false);
        LayoutElement element = spacer.GetComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleHeight = 0f;
        persistenceFooterSpacerHeight += height;
    }

    private void CreatePersistenceButton(string text, UnityEngine.Events.UnityAction action, Color? teamColor = null)
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
        ConfigureMobileActionLabel(label);
        persistenceActionButtons.Add(button);
        persistenceActionImages.Add(buttonImage);
        persistenceActionLabels.Add(label);
        persistenceActionTeamColors.Add(teamColor ?? Color.white);
        persistenceActionUsesTeamColor.Add(teamColor.HasValue);
    }

    private static string BuildShoppingActionsSignature(List<TurnStateManager.HelperShoppingLine> lines)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            TurnStateManager.HelperShoppingLine line = lines[i];
            if (line != null)
                sb.Append(line.index).Append('|').Append(line.unitName).Append('|').Append(line.cost)
                    .Append('|').Append(line.canAfford).Append('|').Append(line.requirementMet)
                    .Append('|').Append(line.requiredBuildingName).Append(';');
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
            button.interactable = true;
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
                string locked = line.requirementMet
                    ? string.Empty
                    : $"\n<size=70%>[REQUER: {line.requiredBuildingName}]</size>";
                label.text = $"{line.index} - {line.unitName}{cost}{locked}";
            }
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.color = FooterLabelIdleColor;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            ConfigureMobileActionLabel(label);
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
            ConfigureMobileActionLabel(label);
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
        ConfigureMobileActionLabel(label);
        cancelActionLabel = label;

        cancelActionButton.onClick.AddListener(() =>
        {
            if (turnStateManager != null
                && turnStateManager
                    .CloseTurnStartAutonomyReportFromPointer())
            {
                cursorController?.PlayCancelSfx();
                return;
            }
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
        // Auto-size para caber rotulos mais longos (ex.: "ADICIONAR À FILA") sem estourar a largura.
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 20f;
        label.fontStyle = FontStyles.Bold;
        label.color = FooterLabelIdleColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        ConfigureMobileActionLabel(label);
        executeCommandServiceLabel = label;

        executeCommandServiceButton.onClick.AddListener(() =>
        {
            if (turnStateManager != null &&
                turnStateManager.CurrentCursorState == TurnStateManager.CursorState.CommandService)
                turnStateManager.SetCommandServicePreviewFocus(0);
            else if (turnStateManager != null &&
                     turnStateManager.CurrentCursorState == TurnStateManager.CursorState.RemovingUnit)
                turnStateManager.SetRemovingUnitFocus(0);
            else if (turnStateManager != null && turnStateManager.IsEmbarkConfirmStep)
                turnStateManager.SetEmbarkConfirmFocus(0);
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
        bool embarking = turnStateManager != null &&
                         turnStateManager.IsEmbarkConfirmStep;
        bool supplyConfirm = turnStateManager != null &&
                             turnStateManager.IsSupplyConfirmStep;
        bool active = panelVisible && (commandService || removingUnit || aiming || embarking || supplyConfirm);
        if (executeCommandServiceControlRoot.activeSelf != active)
            executeCommandServiceControlRoot.SetActive(active);

        if (executeCommandServiceButton != null)
            executeCommandServiceButton.interactable = active;
        if (executeCommandServiceLabel != null)
            executeCommandServiceLabel.text = supplyConfirm ? "ADICIONAR À FILA"
                : (removingUnit || aiming || embarking) ? "CONFIRMAR" : "EXECUTAR";

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
        if (keepPositionControlRoot != null && keepPositionControlRoot.activeSelf)
            height += KeepPositionControlHeight;
        if (cycleSelectionControlRoot != null && cycleSelectionControlRoot.activeSelf)
            height += CycleSelectionControlHeight;
        return height;
    }

    private void EnsureKeepPositionControl()
    {
        if (!Application.isPlaying || keepPositionControlRoot != null || helperRect == null)
            return;

        keepPositionControlRoot = new GameObject("helper_keep_position_control", typeof(RectTransform));
        RectTransform rootRect = keepPositionControlRoot.GetComponent<RectTransform>();
        rootRect.SetParent(helperRect, false);
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, CancelControlHeight);
        rootRect.sizeDelta = new Vector2(0f, KeepPositionControlHeight);
        rootRect.SetAsLastSibling();

        GameObject buttonObject = new GameObject("button_keep_position", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(rootRect, false);
        buttonRect.anchorMin = new Vector2(0.08f, 0f);
        buttonRect.anchorMax = new Vector2(0.92f, 1f);
        buttonRect.offsetMin = new Vector2(4f, 5f);
        buttonRect.offsetMax = new Vector2(-4f, -5f);

        keepPositionImage = buttonObject.GetComponent<Image>();
        keepPositionImage.color = FooterButtonIdleColor;
        keepPositionButton = buttonObject.GetComponent<Button>();
        Navigation navigation = keepPositionButton.navigation;
        navigation.mode = Navigation.Mode.None;
        keepPositionButton.navigation = navigation;

        GameObject labelObject = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        keepPositionLabel = labelObject.GetComponent<TMP_Text>();
        keepPositionLabel.text = "MANTER POSIÇÃO";
        keepPositionLabel.fontSize = 20f;
        keepPositionLabel.fontStyle = FontStyles.Bold;
        keepPositionLabel.color = FooterLabelIdleColor;
        keepPositionLabel.alignment = TextAlignmentOptions.Center;
        keepPositionLabel.raycastTarget = false;
        ConfigureMobileActionLabel(keepPositionLabel);

        keepPositionButton.onClick.AddListener(() => turnStateManager?.TryKeepSelectedUnitPositionFromHelper());
        keepPositionControlRoot.SetActive(false);
    }

    // Botao TROCAR UNIDADE: mesmo padrao do MANTER POSICAO, empilhado logo acima
    // dele. Alias tocavel do PageUp para alternar a ancora num hex empilhado
    // (mobile nao tem segunda tecla; o segundo toque no hex confirma mover).
    private void EnsureCycleSelectionControl()
    {
        if (!Application.isPlaying || cycleSelectionControlRoot != null || helperRect == null)
            return;

        cycleSelectionControlRoot = new GameObject("helper_cycle_selection_control", typeof(RectTransform));
        RectTransform rootRect = cycleSelectionControlRoot.GetComponent<RectTransform>();
        rootRect.SetParent(helperRect, false);
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, CancelControlHeight + KeepPositionControlHeight);
        rootRect.sizeDelta = new Vector2(0f, CycleSelectionControlHeight);
        rootRect.SetAsLastSibling();

        GameObject buttonObject = new GameObject("button_cycle_selection", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(rootRect, false);
        buttonRect.anchorMin = new Vector2(0.08f, 0f);
        buttonRect.anchorMax = new Vector2(0.92f, 1f);
        buttonRect.offsetMin = new Vector2(4f, 5f);
        buttonRect.offsetMax = new Vector2(-4f, -5f);

        cycleSelectionImage = buttonObject.GetComponent<Image>();
        cycleSelectionImage.color = FooterButtonIdleColor;
        cycleSelectionButton = buttonObject.GetComponent<Button>();
        Navigation navigation = cycleSelectionButton.navigation;
        navigation.mode = Navigation.Mode.None;
        cycleSelectionButton.navigation = navigation;

        GameObject labelObject = new GameObject("label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        cycleSelectionLabel = labelObject.GetComponent<TMP_Text>();
        cycleSelectionLabel.text = "TROCAR UNIDADE";
        cycleSelectionLabel.fontSize = 20f;
        cycleSelectionLabel.fontStyle = FontStyles.Bold;
        cycleSelectionLabel.color = FooterLabelIdleColor;
        cycleSelectionLabel.alignment = TextAlignmentOptions.Center;
        cycleSelectionLabel.raycastTarget = false;
        ConfigureMobileActionLabel(cycleSelectionLabel);

        cycleSelectionButton.onClick.AddListener(() => turnStateManager?.TryCycleSelectionWithinHexFromHelper());
        cycleSelectionControlRoot.SetActive(false);
    }

    private void RefreshCycleSelectionControl(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        if (cycleSelectionControlRoot == null)
            return;

        int cyclePosition = 0;
        int cycleTotal = 0;
        bool active = panelVisible && data != null &&
                      data.Kind == TurnStateManager.HelperPanelKind.UnitStats &&
                      turnStateManager != null &&
                      turnStateManager.CurrentCursorState == TurnStateManager.CursorState.UnitSelected &&
                      turnStateManager.TryGetSelectionCycleInfo(out cyclePosition, out cycleTotal);

        if (cycleSelectionControlRoot.activeSelf != active)
            cycleSelectionControlRoot.SetActive(active);
        if (cycleSelectionButton != null)
            cycleSelectionButton.interactable = active;

        if (active)
        {
            if (cycleSelectionLabel != null)
                cycleSelectionLabel.text = $"TROCAR UNIDADE {cyclePosition}/{cycleTotal}";
            TintScriptButtonToTeamIdle(cycleSelectionButton);
            if (panelHelper == gameObject && selfPanelCanvasGroup != null)
            {
                selfPanelCanvasGroup.interactable = true;
                selfPanelCanvasGroup.blocksRaycasts = true;
            }
        }
    }

    private void RefreshKeepPositionControl(bool panelVisible, TurnStateManager.HelperPanelData data)
    {
        if (keepPositionControlRoot == null)
            return;

        bool active = panelVisible && data != null &&
                      data.Kind == TurnStateManager.HelperPanelKind.UnitStats &&
                      turnStateManager != null &&
                      turnStateManager.CurrentCursorState == TurnStateManager.CursorState.UnitSelected;
        if (keepPositionControlRoot.activeSelf != active)
            keepPositionControlRoot.SetActive(active);
        if (keepPositionButton != null)
            // Tutorial: antes da ordem de marcha, o MANTER POSICAO fica cinza.
            keepPositionButton.interactable = active && !TutorialManager.IsMovementLockedByTutorial;
        if (active)
        {
            TintScriptButtonToTeamIdle(keepPositionButton);
            if (panelHelper == gameObject && selfPanelCanvasGroup != null)
            {
                selfPanelCanvasGroup.interactable = true;
                selfPanelCanvasGroup.blocksRaycasts = true;
            }
        }
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
        if (turnStateManager.IsTurnStartAutonomyReportActive)
            return true;

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
            case TurnStateManager.CursorState.Embarcando:
                return turnStateManager.IsEmbarkConfirmStep;
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

    public static void SetExternalWideMode(bool wide)
    {
        if (instance == null)
            return;
        instance.ApplyExternalWideMode(wide);
    }

    private void ApplyExternalWideMode(bool wide)
    {
        if (helperRect == null)
            TryAutoAssignReferences();
        if (helperRect == null || externalWideMode == wide)
            return;

        if (wide)
        {
            externalWideOriginalWidth = helperRect.sizeDelta.x;
            helperRect.sizeDelta = new Vector2(externalWideOriginalWidth + ExternalWideExtraWidth, helperRect.sizeDelta.y);
        }
        else
        {
            helperRect.sizeDelta = new Vector2(externalWideOriginalWidth, helperRect.sizeDelta.y);
        }
        externalWideMode = wide;
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

    // True se o ponto de tela (mouse/toque) esta sobre o painel de ajuda visivel.
    // Usado pela camera para nao dar zoom no mapa quando o scroll rola o texto do painel.
    public static bool IsPointerOverHelperPanel(Vector2 screenPoint)
    {
        if (instance == null)
            return false;

        return instance.ContainsScreenPoint(screenPoint);
    }

    public static bool IsCurrentPointerOverHelperPanel()
    {
        if (instance == null)
            return false;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return instance.ContainsScreenPoint(
                mouse.position.ReadValue());
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null
            && touchscreen.primaryTouch.press.isPressed)
        {
            return instance.ContainsScreenPoint(
                touchscreen.primaryTouch.position.ReadValue());
        }

        return false;
#else
        return instance.ContainsScreenPoint(Input.mousePosition);
#endif
    }

    private bool ContainsScreenPoint(Vector2 screenPoint)
    {
        RectTransform rect = helperRect;
        if (rect == null && panelHelper != null)
            rect = panelHelper.GetComponent<RectTransform>();
        if (rect == null || panelHelper == null || !panelHelper.activeInHierarchy)
            return false;

        // Quando o painel e' o proprio GameObject do controller, sumir e' alpha 0:
        // o objeto continua ativo e o rect continua ocupando a area do dock. Sem
        // esta guarda o painel invisivel ainda "engole" o scroll da camera (zoom
        // morre) e o clique fora do Inspect, ate ele abrir e fugir do cursor.
        if (!IsPanelActuallyVisible())
            return false;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, cam);
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
        if (manuallyPositioned)
        {
            cursorNearUndockedDockRegion = false;
            return;
        }

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

    public void NotifyHelperPanelManuallyPositioned()
    {
        // O dock automatico troca anchors/pivot. Antes de assumir controle manual,
        // volta ao sistema de coordenadas original preservando a posicao visual;
        // assim a memoria continua valida quando o painel some e reaparece.
        if (helperRect != null && isDockedCenterLeft && layoutCached)
        {
            Vector3 worldPosition = helperRect.position;
            helperRect.anchorMin = originalAnchorMin;
            helperRect.anchorMax = originalAnchorMax;
            helperRect.pivot = originalPivot;
            helperRect.position = worldPosition;
        }

        manuallyPositioned = true;
        isDockedCenterLeft = false;
        cursorNearUndockedDockRegion = false;
    }

    private void EnsureDragHandle()
    {
        // A alca e apresentacao runtime. OnValidate tambem passa por
        // TryAutoAssignReferences ao inspecionar Prefab Assets; criar/reparentear
        // objetos nesse contexto deixa GameObjects orfaos e pode corromper o prefab.
        if (!Application.isPlaying || helperRect == null || dragHandleRoot != null)
            return;

        Transform existing = helperRect.Find("helper_drag_handle");
        dragHandleRoot = existing != null ? existing.gameObject : new GameObject(
            "helper_drag_handle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(PanelHelperDragHandle));

        RectTransform rect = dragHandleRoot.GetComponent<RectTransform>();
        rect.SetParent(helperRect, false);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-8f, -8f);
        rect.sizeDelta = new Vector2(46f, 38f);
        rect.SetAsLastSibling();

        Image image = dragHandleRoot.GetComponent<Image>();
        if (image == null)
            image = dragHandleRoot.AddComponent<Image>();
        image.color = new Color(0.08f, 0.08f, 0.06f, 0.92f);
        image.raycastTarget = true;

        Transform labelTransform = rect.Find("label");
        GameObject labelRoot = labelTransform != null ? labelTransform.gameObject : new GameObject(
            "label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelRoot.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMP_Text label = labelRoot.GetComponent<TMP_Text>();
        label.text = "↔";
        label.fontSize = 25f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = helperTitle != null ? helperTitle.color : Color.white;
        label.raycastTarget = false;

        PanelHelperDragHandle handle = dragHandleRoot.GetComponent<PanelHelperDragHandle>();
        if (handle == null)
            handle = dragHandleRoot.AddComponent<PanelHelperDragHandle>();
        handle.Configure(helperRect, this);
        // Mesma independencia de raycast da faixa do titulo: a alca do canto
        // tambem deve arrastar (e nao vazar clique pro mapa) na inspecao em Neutral.
        MakeRaycastIndependent(dragHandleRoot);

        EnsureTitleDragSurface();
    }

    // Faixa transparente cobrindo a regiao do titulo, com o mesmo handle de
    // drag do painel: mover o helper segurando o titulo inteiro (mobile), sem
    // depender da alca pequena do canto. Altura sincronizada com o titulo real
    // em RefreshHelperScrollLayout.
    private void EnsureTitleDragSurface()
    {
        if (!Application.isPlaying || titleDragSurfaceRoot != null || helperRect == null)
            return;

        Transform existing = helperRect.Find("helper_title_drag_surface");
        titleDragSurfaceRoot = existing != null ? existing.gameObject : new GameObject(
            "helper_title_drag_surface",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(PanelHelperDragHandle));

        titleDragSurfaceRect = titleDragSurfaceRoot.GetComponent<RectTransform>();
        titleDragSurfaceRect.SetParent(helperRect, false);
        titleDragSurfaceRect.anchorMin = new Vector2(0f, 1f);
        titleDragSurfaceRect.anchorMax = new Vector2(1f, 1f);
        titleDragSurfaceRect.pivot = new Vector2(0.5f, 1f);
        titleDragSurfaceRect.anchoredPosition = Vector2.zero;
        titleDragSurfaceRect.sizeDelta = new Vector2(0f, 52f);
        titleDragSurfaceRect.SetAsLastSibling();

        Image surfaceImage = titleDragSurfaceRoot.GetComponent<Image>();
        if (surfaceImage == null)
            surfaceImage = titleDragSurfaceRoot.AddComponent<Image>();
        surfaceImage.color = new Color(0f, 0f, 0f, 0f);
        surfaceImage.raycastTarget = true;

        PanelHelperDragHandle surfaceHandle = titleDragSurfaceRoot.GetComponent<PanelHelperDragHandle>();
        if (surfaceHandle == null)
            surfaceHandle = titleDragSurfaceRoot.AddComponent<PanelHelperDragHandle>();
        surfaceHandle.Configure(helperRect, this);

        // Independente do CanvasGroup do painel: na inspecao em Neutral o painel
        // desliga blocksRaycasts (cliques atravessam pro mapa) — sem isto, o
        // clique no titulo viraria clique no hex atras e fecharia a inspecao.
        MakeRaycastIndependent(titleDragSurfaceRoot);
    }

    private static void MakeRaycastIndependent(GameObject target)
    {
        if (target == null)
            return;
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
            group = target.AddComponent<CanvasGroup>();
        group.ignoreParentGroups = true;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    // Contrapartida do ignoreParentGroups: o sumico do painel (alpha 0 no grupo
    // pai, ex.: timeout de 6s da inspecao por hover) nao alcanca a alca nem a
    // faixa do titulo — sincroniza visibilidade e raycast delas a cada refresh.
    private void RefreshDragSurfaces(bool panelVisible)
    {
        SyncDragSurfaceGroup(dragHandleRoot, panelVisible);
        SyncDragSurfaceGroup(titleDragSurfaceRoot, panelVisible);
    }

    public static bool IsPointerOverDragSurface()
    {
        if (instance == null || !instance.IsPanelVisibleForDrag)
            return false;

        Vector2 screenPosition;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            screenPosition = Mouse.current.position.ReadValue();
        else
#endif
            screenPosition = Input.mousePosition;

        return IsScreenPointInsideRect(instance.titleDragSurfaceRect, screenPosition) ||
               IsScreenPointInsideRect(
                   instance.dragHandleRoot != null
                       ? instance.dragHandleRoot.GetComponent<RectTransform>()
                       : null,
                   screenPosition);
    }

    private static bool IsScreenPointInsideRect(RectTransform rect, Vector2 screenPosition)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy)
            return false;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera);
    }

    private static void SyncDragSurfaceGroup(GameObject target, bool panelVisible)
    {
        if (target == null)
            return;
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
            return;
        group.alpha = panelVisible ? 1f : 0f;
        group.blocksRaycasts = panelVisible;
        group.interactable = panelVisible;
    }

    // Consultado pelo PanelHelperDragHandle: um drag em andamento morre se o
    // painel sumir no meio do gesto (senao o jogador arrasta um painel fantasma).
    public bool IsPanelVisibleForDrag => lastPanelVisible;

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

    // Fonte unica de "o painel esta realmente aparecendo". Cobre os dois modos de
    // SetPanelVisible: alpha (painel == este GameObject) e SetActive (painel filho).
    private bool IsPanelActuallyVisible()
    {
        if (panelHelper == null || !panelHelper.activeInHierarchy)
            return false;

        if (selfPanelCanvasGroup != null && selfPanelCanvasGroup.alpha <= 0.01f)
            return false;

        return lastPanelVisible;
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
        // Faixa de drag do titulo acompanha a altura real do titulo do frame.
        if (titleDragSurfaceRect != null)
            titleDragSurfaceRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, titleTopInset + targetTitleHeight);
        float footerHeight = GetActiveFooterHeight();
        bool aimButtonsActive = aimTargetsRoot != null && aimTargetsRoot.activeSelf;
        float viewportHeight = aimButtonsActive
            ? Mathf.Max(1f, panelHeight - titleTopInset - targetTitleHeight - originalBodySpacingFromTitle - footerHeight)
            : Mathf.Max(1f, panelHeight - titleTopInset - footerHeight);
        float combinedContentHeight = aimButtonsActive
            ? targetBodyHeight
            : targetTitleHeight + originalBodySpacingFromTitle + targetBodyHeight;
        aimTargetsViewportHeight = aimButtonsActive ? viewportHeight : 0f;
        helperScrollMaxOffset = Mathf.Max(0f, combinedContentHeight - viewportHeight);
        helperScrollActive = helperScrollMaxOffset > 0.5f;
        helperScrollOffset = Mathf.Clamp(helperScrollOffset, 0f, helperScrollMaxOffset);
        if (!helperScrollActive)
            helperScrollOffset = 0f;

        Vector2 bodyBasePosition = new Vector2(
            originalHelperTxtAnchoredPosition.x,
            originalHelperTitleAnchoredPosition.y - targetTitleHeight - originalBodySpacingFromTitle);
        if (aimButtonsActive)
        {
            helperTitleRect.anchoredPosition = originalHelperTitleAnchoredPosition;
            helperTxtRect.anchoredPosition = bodyBasePosition;
            ApplyAimTargetsScrollPosition(bodyBasePosition.y);
            EnsureFocusedAimTargetVisible();
        }
        else
        {
            Vector2 scrollOffset = new Vector2(0f, helperScrollOffset);
            helperTitleRect.anchoredPosition = originalHelperTitleAnchoredPosition + scrollOffset;
            helperTxtRect.anchoredPosition = bodyBasePosition + scrollOffset;
        }
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
        if (!lastPanelVisible || helperRect == null)
            return;

        // O Jornal tem rolagem propria (viewport mascarado), independente do
        // scroll do corpo de texto do helper.
        bool journalActive = autonomyUpkeepRoot != null && autonomyUpkeepRoot.activeSelf;
        if (!helperScrollActive && !journalActive)
            return;

        Vector2 scrollDelta = ReadMouseScrollDelta();
        if (Mathf.Abs(scrollDelta.y) <= 0.01f)
            return;

        Rect panelScreenRect = GetScreenRect(helperRect);
        Vector3 mouseScreen = ReadMouseScreenPosition();
        if (!panelScreenRect.Contains(new Vector2(mouseScreen.x, mouseScreen.y)))
            return;

        if (journalActive)
        {
            // So a DIRECAO da roda importa: o Input System entrega ±120 por
            // clique e o legado ±1 — multiplicar o valor cru mandaria a lista
            // pro fim num clique so. Cada clique anda pouco mais de meia linha.
            autonomyUpkeepScrollOffset -= Mathf.Sign(scrollDelta.y) * Mathf.Max(1f, helperScrollStep) * 2f;
            ApplyAutonomyUpkeepScrollPosition();
            return;
        }

        helperScrollOffset = Mathf.Clamp(
            helperScrollOffset - scrollDelta.y * Mathf.Max(1f, helperScrollStep),
            0f,
            helperScrollMaxOffset);

        if (aimTargetsRoot != null && aimTargetsRoot.activeSelf)
        {
            ApplyAimTargetsScrollPosition();
            return;
        }

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

    public void ScrollAimTargetsByPointerDelta(float screenDeltaY)
    {
        if (!helperScrollActive || aimTargetsRoot == null || !aimTargetsRoot.activeSelf)
            return;
        helperScrollOffset = Mathf.Clamp(helperScrollOffset + screenDeltaY, 0f, helperScrollMaxOffset);
        ApplyAimTargetsScrollPosition();
    }

    private void ApplyAimTargetsScrollPosition(float? bodyBaseY = null)
    {
        if (aimTargetsRoot == null)
            return;
        RectTransform rect = aimTargetsRoot.GetComponent<RectTransform>();
        if (rect == null)
            return;
        float baseY = bodyBaseY ?? (originalHelperTitleAnchoredPosition.y -
            Mathf.Max(0f, helperTitleRect != null ? helperTitleRect.rect.height : originalHelperTitleHeight) -
            originalBodySpacingFromTitle);

        if (aimTargetsViewportRect != null)
        {
            // Viewport ancorado logo abaixo do titulo; so o conteudo desloca
            // dentro da mascara — o titulo nunca e coberto pela lista.
            aimTargetsViewportRect.anchoredPosition = new Vector2(aimTargetsViewportRect.anchoredPosition.x, baseY);
            if (aimTargetsViewportHeight > 0f)
                aimTargetsViewportRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, aimTargetsViewportHeight);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, helperScrollOffset);
            return;
        }

        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, baseY + helperScrollOffset);
    }

    private void EnsureFocusedAimTargetVisible()
    {
        if (!helperScrollActive || aimFocusedButtonIndex < 0 ||
            aimFocusedButtonIndex >= aimTargetButtons.Count || aimTargetsViewportHeight <= 0f)
            return;

        float top = 0f;
        for (int i = 0; i < aimFocusedButtonIndex; i++)
        {
            LayoutElement prior = aimTargetButtons[i] != null ? aimTargetButtons[i].GetComponent<LayoutElement>() : null;
            top += (prior != null ? Mathf.Max(AimTargetButtonHeight, prior.preferredHeight) : AimTargetButtonHeight) + 4f;
        }
        LayoutElement focused = aimTargetButtons[aimFocusedButtonIndex] != null
            ? aimTargetButtons[aimFocusedButtonIndex].GetComponent<LayoutElement>()
            : null;
        float bottom = top + (focused != null ? Mathf.Max(AimTargetButtonHeight, focused.preferredHeight) : AimTargetButtonHeight);
        if (top < helperScrollOffset)
            helperScrollOffset = top;
        else if (bottom > helperScrollOffset + aimTargetsViewportHeight)
            helperScrollOffset = bottom - aimTargetsViewportHeight;
        helperScrollOffset = Mathf.Clamp(helperScrollOffset, 0f, helperScrollMaxOffset);
        ApplyAimTargetsScrollPosition();
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

public sealed class PanelHelperAimScrollDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private PanelHelperController owner;

    public void Configure(PanelHelperController controller) => owner = controller;
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) => owner?.ScrollAimTargetsByPointerDelta(eventData.delta.y);
    public void OnEndDrag(PointerEventData eventData) { }
}

// Arraste nas noticias do Jornal do Comandante: o conteudo acompanha o
// dedo/ponteiro (puxar pra cima traz as proximas linhas).
public sealed class PanelHelperJournalScrollDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private PanelHelperController owner;

    public void Configure(PanelHelperController controller) => owner = controller;
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) => owner?.ScrollTurnBriefingByPointerDelta(eventData.delta.y);
    public void OnEndDrag(PointerEventData eventData) { }
}

public sealed class PanelHelperDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform target;
    private PanelHelperController owner;
    private Canvas canvas;

    public void Configure(RectTransform dragTarget, PanelHelperController controller)
    {
        target = dragTarget;
        owner = controller;
        canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (target == null)
            return;
        // Painel invisivel (ex.: acabou de sumir por timeout) nao inicia drag.
        if (owner != null && !owner.IsPanelVisibleForDrag)
            return;
        owner.NotifyHelperPanelManuallyPositioned();
        target.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null)
            return;
        // Painel sumiu no meio do gesto (timeout da inspecao por hover): aborta
        // o arrasto para nao mover um painel fantasma.
        if (owner != null && !owner.IsPanelVisibleForDrag)
            return;
        float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        target.anchoredPosition += eventData.delta / scale;
        ClampToCanvas();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (target == null)
            return;
        ClampToCanvas();
    }

    private void ClampToCanvas()
    {
        if (target == null)
            return;
        RectTransform bounds = canvas != null ? canvas.transform as RectTransform : target.parent as RectTransform;
        if (bounds == null)
            return;

        Vector3[] panelCorners = new Vector3[4];
        Vector3[] boundsCorners = new Vector3[4];
        target.GetWorldCorners(panelCorners);
        bounds.GetWorldCorners(boundsCorners);
        Vector3 correction = Vector3.zero;
        if (panelCorners[0].x < boundsCorners[0].x) correction.x = boundsCorners[0].x - panelCorners[0].x;
        else if (panelCorners[2].x > boundsCorners[2].x) correction.x = boundsCorners[2].x - panelCorners[2].x;
        if (panelCorners[0].y < boundsCorners[0].y) correction.y = boundsCorners[0].y - panelCorners[0].y;
        else if (panelCorners[2].y > boundsCorners[2].y) correction.y = boundsCorners[2].y - panelCorners[2].y;
        if (correction.sqrMagnitude > 0f) target.position += correction;
    }
}
