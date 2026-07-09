using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PanelTutorialController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text text_titulo;
    [SerializeField] private TMP_Text text_tarefaas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform scrollContentRect;
    [SerializeField] private MatchController matchController;
    
    [Header("Settings")]
    [SerializeField] private bool panelVisivel = true;
    [SerializeField] private bool autoResizeHeight = true;
    [SerializeField] [Range(0.2f, 0.95f)] private float maxScreenHeightPercent = 0.72f;
    [SerializeField] [Range(40f, 420f)] private float minPanelHeight = 120f;
    [SerializeField] [Range(0f, 80f)] private float verticalPadding = 24f;
    [SerializeField] [Range(2f, 120f)] private float manualScrollStep = 28f;

    private RectMask2D panelMask;
    private RectTransform titleRect;
    private RectTransform bodyRect;
    private Vector2 titleBaseAnchoredPosition;
    private Vector2 bodyBaseAnchoredPosition;
    private float baseTitleHeight = -1f;
    private float baseBodyHeight = -1f;
    private float baseBodySpacingFromTitle;
    private bool baseLayoutCached;
    private float manualBodyScrollOffset;
    private float manualBodyScrollMaxOffset;
    private bool manualBodyScrollActive;

    private MatchController ResolveMatchController()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        return matchController;
    }

    private void OnEnable()
    {
        PanelVisibilityHotkeysController.OnF6Pressed += HandleF6Pressed;
        ResolveUiReferences();
        ApplyVisibility();
        RefreshPanel();
    }

    private void Start()
    {
        // Diagnostico de visibilidade: painel de tarefas fora de Canvas ou sem
        // tutorial ativo falha em silencio — este log denuncia o motivo no Play.
        MatchController match = ResolveMatchController();
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        RectTransform rect = GetComponent<RectTransform>();
        Debug.Log(
            $"[PanelTutorial] Start | match={(match != null)} | tutorialAtivo={(match != null && match.IsTutorialMode)} | " +
            $"panelVisivel={panelVisivel} | canvasPai={(parentCanvas != null ? parentCanvas.name : "<FORA DE CANVAS>")} | " +
            $"dockHelper={PanelHelperController.IsDockedCenterLeft()} | anchoredPos={(rect != null ? rect.anchoredPosition.ToString() : "<sem rect>")}");
    }

    private void OnDisable()
    {
        PanelVisibilityHotkeysController.OnF6Pressed -= HandleF6Pressed;
    }

    private void Update()
    {
        ApplyVisibility();

        MatchController match = ResolveMatchController();
        if (panelVisivel && match != null && match.IsTutorialMode)
        {
            RefreshPanel();
            HandleManualBodyScrollInput();
        }
    }

    private void ApplyVisibility()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        MatchController match = ResolveMatchController();
        bool tutorialEnabled = match != null && match.IsTutorialMode && panelVisivel;
        bool helperDockedLeft = PanelHelperController.IsDockedCenterLeft();
        bool shouldBeVisible = tutorialEnabled && !helperDockedLeft;
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = shouldBeVisible ? 1f : 0f;
            canvasGroup.interactable = shouldBeVisible;
            canvasGroup.blocksRaycasts = shouldBeVisible;
        }
        
        // Sempre aplica aos textos e ao fundo como redundancia ou fallback
        if (text_titulo != null && text_titulo.gameObject.activeSelf != shouldBeVisible) 
            text_titulo.gameObject.SetActive(shouldBeVisible);
        
        if (text_tarefaas != null && text_tarefaas.gameObject.activeSelf != shouldBeVisible) 
            text_tarefaas.gameObject.SetActive(shouldBeVisible);

        // Se o CanvasGroup falhou ou nao existe, tenta esconder o fundo (Image) deste objeto
        Image bg = GetComponent<Image>();
        if (bg != null && bg.enabled != shouldBeVisible)
            bg.enabled = shouldBeVisible;
    }

    public void RefreshPanel()
    {
        MatchController match = ResolveMatchController();
        if (match == null || !match.IsTutorialMode || !panelVisivel)
            return;

        TutorialData data = match.ActiveTutorial;
        if (data == null)
            return;

        if (text_titulo != null)
        {
            text_titulo.text = !string.IsNullOrWhiteSpace(data.description) 
                ? data.description 
                : "Tutorial";
        }

        if (text_tarefaas != null)
        {
            StringBuilder sb = new StringBuilder();
            int completedCount = 0;
            int totalCount = 0;
            
            for (int i = 0; i < data.objectives.Count; i++)
            {
                TutorialObjective obj = data.objectives[i];
                if (obj == null || !obj.isVisible)
                    continue;

                totalCount++;
                if (obj.isCompleted) completedCount++;

                string checkMark = "<color=#888888>[  ]</color>";
                if (obj.isCompleted) checkMark = "<color=#00FF00>[ V ]</color>";
                else if (obj.hasFailed) checkMark = "<color=#FF0000>[ X ]</color>";

                string texColor = (obj.isCompleted || obj.hasFailed) ? "<color=#AAAAAA>" : "<color=#FFFFFF>";
                string optionalSuffix = obj.isOptional ? " <color=#77DDEE>(opcional)</color>" : string.Empty;
                
                sb.AppendLine($"{checkMark} {texColor}{obj.description}</color>{optionalSuffix}");
            }

            if (totalCount <= 0)
            {
                sb.AppendLine("<color=#AAAAAA>Aguardando próximo objetivo...</color>");
            }
            else
            {
                sb.AppendLine("---------------");
                sb.AppendLine($"{completedCount}/{totalCount} completos");
            }

            text_tarefaas.text = sb.ToString();
        }

        ApplyDynamicSizing();
    }

    private void HandleF6Pressed()
    {
        MatchController match = ResolveMatchController();
        if (match == null || !match.IsTutorialMode)
            return;

        panelVisivel = !panelVisivel;
        ApplyVisibility();
        
        string status = panelVisivel ? "Ativado" : "Oculto";
        PanelDialogController.TrySetTransientText($"Tutorial: {status}", 1.5f);
    }

    private void ResolveUiReferences()
    {
        if (panelRect == null)
            panelRect = GetComponent<RectTransform>();
        if (titleRect == null && text_titulo != null)
            titleRect = text_titulo.rectTransform;
        if (bodyRect == null && text_tarefaas != null)
            bodyRect = text_tarefaas.rectTransform;
        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(includeInactive: true);
        if (scrollContentRect == null && scrollRect != null)
            scrollContentRect = scrollRect.content;

        CacheBaseLayoutIfNeeded();

        if (panelRect != null && scrollRect == null)
        {
            if (panelMask == null)
                panelMask = panelRect.GetComponent<RectMask2D>();
            if (panelMask == null)
                panelMask = panelRect.gameObject.AddComponent<RectMask2D>();
        }
    }

    private void ApplyDynamicSizing()
    {
        if (!autoResizeHeight)
            return;

        ResolveUiReferences();
        if (panelRect == null)
            return;

        if (text_titulo != null)
            text_titulo.ForceMeshUpdate(ignoreActiveState: true);
        if (text_tarefaas != null)
            text_tarefaas.ForceMeshUpdate(ignoreActiveState: true);

        float titleWidth = ResolveTextWidth(text_titulo, panelRect.rect.width);
        float bodyWidth = ResolveTextWidth(text_tarefaas, panelRect.rect.width);
        float titleHeight = text_titulo != null
            ? Mathf.Max(0f, text_titulo.GetPreferredValues(text_titulo.text, titleWidth, 0f).y)
            : 0f;
        float bodyHeight = text_tarefaas != null
            ? Mathf.Max(0f, text_tarefaas.GetPreferredValues(text_tarefaas.text, bodyWidth, 0f).y)
            : 0f;

        float desiredHeight = Mathf.Max(minPanelHeight, titleHeight + bodyHeight + verticalPadding);
        float maxHeight = Mathf.Max(minPanelHeight, Screen.height * Mathf.Clamp(maxScreenHeightPercent, 0.2f, 0.95f));
        bool overflow = desiredHeight > maxHeight + 0.5f;
        float targetHeight = Mathf.Min(desiredHeight, maxHeight);

        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        if (scrollRect != null)
        {
            scrollRect.enabled = overflow;
            scrollRect.vertical = overflow;
            scrollRect.horizontal = false;
            if (overflow)
                scrollRect.verticalNormalizedPosition = 1f;
        }

        if (scrollContentRect != null)
        {
            float contentHeight = Mathf.Max(targetHeight, desiredHeight);
            scrollContentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        }

        ApplyManualBodyScrollLayout(titleHeight, bodyHeight, targetHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    }

    private static float ResolveTextWidth(TMP_Text text, float fallbackWidth)
    {
        if (text == null)
            return Mathf.Max(40f, fallbackWidth);
        RectTransform rt = text.rectTransform;
        if (rt == null)
            return Mathf.Max(40f, fallbackWidth);
        float width = rt.rect.width;
        if (width <= 1f)
            width = fallbackWidth;
        return Mathf.Max(40f, width);
    }

    private void CacheBaseLayoutIfNeeded()
    {
        if (baseLayoutCached)
            return;

        if (panelRect == null || titleRect == null || bodyRect == null)
            return;

        titleBaseAnchoredPosition = titleRect.anchoredPosition;
        bodyBaseAnchoredPosition = bodyRect.anchoredPosition;
        baseTitleHeight = Mathf.Max(0f, titleRect.rect.height);
        baseBodyHeight = Mathf.Max(0f, bodyRect.rect.height);
        float titleBottom = -titleBaseAnchoredPosition.y + baseTitleHeight;
        float bodyTop = -bodyBaseAnchoredPosition.y;
        baseBodySpacingFromTitle = Mathf.Max(0f, bodyTop - titleBottom);
        baseLayoutCached = true;
    }

    private void ApplyManualBodyScrollLayout(float titleHeight, float bodyHeight, float panelHeight)
    {
        if (scrollRect != null)
            return;
        if (!baseLayoutCached || titleRect == null || bodyRect == null)
            return;

        float clampedTitleHeight = Mathf.Max(baseTitleHeight, titleHeight);
        float clampedBodyHeight = Mathf.Max(baseBodyHeight, bodyHeight);
        float topInset = Mathf.Max(0f, -titleBaseAnchoredPosition.y);
        float reserved = topInset + clampedTitleHeight + baseBodySpacingFromTitle + Mathf.Max(0f, verticalPadding * 0.5f);
        float bodyViewportHeight = Mathf.Max(1f, panelHeight - reserved);
        manualBodyScrollMaxOffset = Mathf.Max(0f, clampedBodyHeight - bodyViewportHeight);
        manualBodyScrollActive = manualBodyScrollMaxOffset > 0.5f;
        manualBodyScrollOffset = Mathf.Clamp(manualBodyScrollOffset, 0f, manualBodyScrollMaxOffset);
        if (!manualBodyScrollActive)
            manualBodyScrollOffset = 0f;

        titleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, clampedTitleHeight);
        titleRect.anchoredPosition = titleBaseAnchoredPosition;

        bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, clampedBodyHeight);
        float bodyY = titleBaseAnchoredPosition.y - clampedTitleHeight - baseBodySpacingFromTitle;
        bodyRect.anchoredPosition = new Vector2(bodyBaseAnchoredPosition.x, bodyY - manualBodyScrollOffset);
    }

    private void HandleManualBodyScrollInput()
    {
        if (!manualBodyScrollActive || scrollRect != null || panelRect == null)
            return;

        Vector2 scrollDelta = ReadMouseScrollDelta();
        if (Mathf.Abs(scrollDelta.y) <= 0.01f)
            return;

        Rect panelScreenRect = GetScreenRect(panelRect);
        Vector3 mouseScreen = ReadMouseScreenPosition();
        if (!panelScreenRect.Contains(new Vector2(mouseScreen.x, mouseScreen.y)))
            return;

        manualBodyScrollOffset = Mathf.Clamp(
            manualBodyScrollOffset - scrollDelta.y * Mathf.Max(1f, manualScrollStep),
            0f,
            manualBodyScrollMaxOffset);

        if (bodyRect != null && titleRect != null)
        {
            float titleHeight = Mathf.Max(baseTitleHeight, titleRect.rect.height);
            float bodyY = titleBaseAnchoredPosition.y - titleHeight - baseBodySpacingFromTitle;
            bodyRect.anchoredPosition = new Vector2(bodyBaseAnchoredPosition.x, bodyY - manualBodyScrollOffset);
        }
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
}
