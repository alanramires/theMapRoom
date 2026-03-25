using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class PanelTutorialController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text text_titulo;
    [SerializeField] private TMP_Text text_tarefaas;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Settings")]
    [SerializeField] private bool panelVisivel = true;

    private void OnEnable()
    {
        PanelVisibilityHotkeysController.OnF6Pressed += HandleF6Pressed;
        ApplyVisibility();
        RefreshPanel();
    }

    private void OnDisable()
    {
        PanelVisibilityHotkeysController.OnF6Pressed -= HandleF6Pressed;
    }

    private void Update()
    {
        ApplyVisibility();

        if (panelVisivel && HelpManager.Instance != null && HelpManager.Instance.IsTutorialMode)
        {
            RefreshPanel();
        }
    }

    private void ApplyVisibility()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        bool shouldBeVisible = HelpManager.Instance != null && HelpManager.Instance.IsTutorialMode && panelVisivel;
        
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
        if (HelpManager.Instance == null || !HelpManager.Instance.IsTutorialMode || !panelVisivel)
            return;

        TutorialData data = HelpManager.Instance.ActiveTutorial;
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
            int totalCount = data.objectives.Count;
            
            for (int i = 0; i < totalCount; i++)
            {
                TutorialObjective obj = data.objectives[i];
                if (obj.isCompleted) completedCount++;

                string checkMark = "<color=#888888>[  ]</color>";
                if (obj.isCompleted) checkMark = "<color=#00FF00>[ V ]</color>";
                else if (obj.hasFailed) checkMark = "<color=#FF0000>[ X ]</color>";

                string texColor = (obj.isCompleted || obj.hasFailed) ? "<color=#AAAAAA>" : "<color=#FFFFFF>";
                
                sb.AppendLine($"{checkMark} {texColor}{obj.description}</color>");
            }

            sb.AppendLine("---------------");
            sb.AppendLine($"{completedCount}/{totalCount} completos");

            text_tarefaas.text = sb.ToString();
        }
    }

    private void HandleF6Pressed()
    {
        if (HelpManager.Instance == null || !HelpManager.Instance.IsTutorialMode)
            return;

        panelVisivel = !panelVisivel;
        ApplyVisibility();
        
        string status = panelVisivel ? "Ativado" : "Oculto";
        PanelDialogController.TrySetTransientText($"Tutorial: {status}", 1.5f);
    }
}
