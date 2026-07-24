using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlanningPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlanningManager planningManager;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Texts")]
    [SerializeField] private TMP_Text helperText;
    [SerializeField] private TMP_Text rallyPointsText;
    [SerializeField] private TMP_Text assignmentsText;

    [Header("Inputs")]
    [SerializeField] private TMP_InputField rallyNameInput;

    [Header("Buttons")]
    [SerializeField] private Button createButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private Button previousRallyButton;
    [SerializeField] private Button nextRallyButton;

    private void Awake()
    {
        if (planningManager == null)
            planningManager = FindAnyObjectByType<PlanningManager>();
        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        Bind();
        Refresh();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Update()
    {
        Refresh();
    }

    private void Bind()
    {
        createButton?.onClick.AddListener(OnCreateClicked);
        startButton?.onClick.AddListener(OnStartClicked);
        removeButton?.onClick.AddListener(OnRemoveClicked);
        previousRallyButton?.onClick.AddListener(OnPreviousClicked);
        nextRallyButton?.onClick.AddListener(OnNextClicked);
        if (rallyNameInput != null)
            rallyNameInput.onValueChanged.AddListener(OnNameInputChanged);

        if (planningManager != null)
        {
            planningManager.PlanningDataChanged += Refresh;
            planningManager.PlanningModeChanged += OnPlanningModeChanged;
        }
    }

    private void Unbind()
    {
        createButton?.onClick.RemoveListener(OnCreateClicked);
        startButton?.onClick.RemoveListener(OnStartClicked);
        removeButton?.onClick.RemoveListener(OnRemoveClicked);
        previousRallyButton?.onClick.RemoveListener(OnPreviousClicked);
        nextRallyButton?.onClick.RemoveListener(OnNextClicked);
        if (rallyNameInput != null)
            rallyNameInput.onValueChanged.RemoveListener(OnNameInputChanged);

        if (planningManager != null)
        {
            planningManager.PlanningDataChanged -= Refresh;
            planningManager.PlanningModeChanged -= OnPlanningModeChanged;
        }
    }

    private void OnPlanningModeChanged(bool active)
    {
        Refresh();
    }

    private void OnNameInputChanged(string value)
    {
        planningManager?.SetPendingRallyName(value);
    }

    private void OnCreateClicked()
    {
        if (planningManager == null)
            return;

        if (!planningManager.TryCreateRallyPoint(out string message))
        {
            PanelDialogController.TrySetTransientText(message, 2.2f);
            return;
        }

        if (rallyNameInput != null)
            rallyNameInput.SetTextWithoutNotify(string.Empty);

        PanelDialogController.TrySetTransientText(message, 2.2f);
        Refresh();
    }

    private void OnStartClicked()
    {
        if (planningManager == null)
            return;

        if (!planningManager.TryStartSelectedRallyPoint(out string message))
        {
            PanelDialogController.TrySetTransientText(message, 2.2f);
            return;
        }

        PanelDialogController.TrySetTransientText(message, 2.2f);
        Refresh();
    }

    private void OnRemoveClicked()
    {
        if (planningManager == null)
            return;

        if (!planningManager.TryRemoveSelectedRallyPoint(out string message))
        {
            PanelDialogController.TrySetTransientText(message, 2.2f);
            return;
        }

        PanelDialogController.TrySetTransientText(message, 2.2f);
        Refresh();
    }

    private void OnPreviousClicked()
    {
        SelectRelative(-1);
    }

    private void OnNextClicked()
    {
        SelectRelative(1);
    }

    private void SelectRelative(int delta)
    {
        if (planningManager == null)
            return;

        PlayerSlotId activeSlot = ResolveActiveSlot();
        IReadOnlyList<RallyPoint> points = planningManager.GetRallyPointsForSlot(activeSlot);
        if (points == null || points.Count <= 0)
            return;

        int selectedId = planningManager.SelectedRallyPointId;
        int index = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] != null && points[i].id == selectedId)
            {
                index = i;
                break;
            }
        }

        index += delta;
        if (index < 0)
            index = points.Count - 1;
        else if (index >= points.Count)
            index = 0;

        RallyPoint next = points[index];
        if (next != null)
            planningManager.TrySelectRallyPoint(next.id);

        Refresh();
    }

    private PlayerSlotId ResolveActiveSlot()
    {
        MatchController match = FindAnyObjectByType<MatchController>();
        return match != null ? match.ActiveSlotId : PlayerSlotId.Invalid;
    }

    private void Refresh()
    {
        bool active = planningManager != null && planningManager.IsPlanningModeActive;
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = active ? 1f : 0f;
            panelCanvasGroup.interactable = active;
            panelCanvasGroup.blocksRaycasts = active;
        }

        if (!active)
            return;

        if (rallyNameInput != null && planningManager != null)
            rallyNameInput.SetTextWithoutNotify(planningManager.PendingRallyName ?? string.Empty);

        BuildHelperText();
        BuildRallyPointsText();
        BuildAssignmentsText();
    }

    private void BuildHelperText()
    {
        if (helperText == null)
            return;

        string pending = planningManager != null && planningManager.HasPendingDestination
            ? $"({planningManager.PendingDestination.x},{planningManager.PendingDestination.y})"
            : "(nenhum)";

        helperText.text =
            "Planning ativo\n" +
            "1) Clique no mapa para destino\n" +
            "2) Nomeie e crie o rally\n" +
            "3) Clique em unidades para atribuir/remover\n" +
            "4) Inicie o rally (execucao no proximo turno)\n" +
            $"Destino pendente: {pending}\n" +
            "P ou ESC: sair";
    }

    private void BuildRallyPointsText()
    {
        if (rallyPointsText == null || planningManager == null)
            return;

        PlayerSlotId activeSlot = ResolveActiveSlot();
        IReadOnlyList<RallyPoint> points = planningManager.GetRallyPointsForSlot(activeSlot);
        int maxPerTeam = planningManager.Config != null ? Mathf.Max(1, planningManager.Config.maxRallyPointsPerTeam) : 5;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Rally Points ({(points != null ? points.Count : 0)}/{maxPerTeam})");

        if (points == null || points.Count <= 0)
        {
            sb.AppendLine("- nenhum");
        }
        else
        {
            for (int i = 0; i < points.Count; i++)
            {
                RallyPoint point = points[i];
                if (point == null)
                    continue;

                bool selected = point.id == planningManager.SelectedRallyPointId;
                string mark = selected ? ">" : " ";
                string active = point.ativo ? "ativo" : "inativo";
                sb.AppendLine($"{mark} #{point.id} {point.nome} -> ({point.hexDestino.x},{point.hexDestino.y}) [{active}]");
            }
        }

        rallyPointsText.text = sb.ToString().TrimEnd();
    }

    private void BuildAssignmentsText()
    {
        if (assignmentsText == null || planningManager == null)
            return;

        int selectedRallyId = planningManager.SelectedRallyPointId;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Assignments (Rally #{selectedRallyId})");

        if (selectedRallyId <= 0)
        {
            sb.AppendLine("- selecione um rally");
            assignmentsText.text = sb.ToString().TrimEnd();
            return;
        }

        IReadOnlyList<RallyAssignment> assignments = planningManager.GetAssignmentsForRally(selectedRallyId);
        if (assignments == null || assignments.Count <= 0)
        {
            sb.AppendLine("- nenhuma unidade");
            assignmentsText.text = sb.ToString().TrimEnd();
            return;
        }

        for (int i = 0; i < assignments.Count; i++)
        {
            RallyAssignment assignment = assignments[i];
            if (assignment == null)
                continue;
            sb.AppendLine($"- unitId {assignment.unitId}");
        }

        assignmentsText.text = sb.ToString().TrimEnd();
    }
}
