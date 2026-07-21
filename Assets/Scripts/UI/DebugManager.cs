using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Autoridade unica das flags de debug e dos atalhos globais de IA (F10/F11/F12).
//
// Precisa viver FORA do Panel_Debug: o painel comeca desativado e e ligado/desligado
// pelo PanelVisibilityHotkeysController, entao um componente dentro dele nao roda
// Update e nao pode responder por atalho nenhum.
//
// O lado do painel (campo de comando, botao enviar, execucao de comandos) e do
// PanelDebugController. Antes os dois papeis moravam nesta classe e eram escolhidos
// em runtime por "commandInputObject != null || sendButton != null"; em cenas onde o
// manager avulso tambem tinha esses campos preenchidos, os dois se declaravam painel
// e o jogo ficava sem autoridade — o atalho de debug simplesmente parava de funcionar.
public class DebugManager : MonoBehaviour
{
    private static DebugManager instance;
    private static bool missingAuthorityWarned;

    [Header("AI Debug Shortcuts")]
    [Tooltip("F12 = AI Resume | F10 = AI Pause | F11 = AI Step.")]
    [SerializeField] private bool aiDebugShortcutsEnabled;
    [Tooltip("Permite abrir o Panel_Debug pelos atalhos de desenvolvedor: ', ;, crase ou Ctrl+D.")]
    [SerializeField] private bool debugShortcutsEnabled;
    [Tooltip("Quando ativo, 'AI Stage 1' limpa o plano antes de rodar (força nova atribuição A+B).")]
    [SerializeField] private bool resetPlanOnDebugStage;

    [Header("Hot Seat Debug")]
    [Tooltip("Quando ativo, pula a apresentação do Panel_Rodada em partidas hotseat (pvp), tanto no início da partida quanto na transição entre rodadas.")]
    [SerializeField] private bool panelRodadaDesativadoPvpDebug;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning(
                $"[DebugManager] Ja existe uma autoridade de debug em '{instance.gameObject.name}'. "
                + $"'{gameObject.name}' sera ignorado. Mantenha apenas UM DebugManager por cena, "
                + "sempre fora do Panel_Debug.", this);
            return;
        }

        instance = this;
        missingAuthorityWarned = false;

        if (GetComponentInParent<PanelDebugController>() != null)
        {
            Debug.LogWarning(
                $"[DebugManager] '{gameObject.name}' esta dentro do Panel_Debug. O painel fica desativado "
                + "na maior parte do tempo, entao os atalhos nao vao rodar. Mova este componente para um "
                + "GameObject permanente da cena.", this);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (instance != this || !aiDebugShortcutsEnabled)
            return;

        HandleAIDebugShortcuts();
    }

    public static bool AreAIDebugShortcutsEnabled()
    {
        DebugManager authority = Resolve();
        return authority != null && authority.aiDebugShortcutsEnabled;
    }

    public static bool AreDebugShortcutsEnabled()
    {
        DebugManager authority = Resolve();
        return authority != null && authority.debugShortcutsEnabled;
    }

    public static bool ShouldResetPlanOnDebugStage()
    {
        DebugManager authority = Resolve();
        return authority != null && authority.resetPlanOnDebugStage;
    }

    public static bool IsPanelRodadaDisabledForHotSeat()
    {
        DebugManager authority = Resolve();
        return authority != null && authority.panelRodadaDesativadoPvpDebug;
    }

    // As flags sao serializadas por cena, entao um DebugManager criado em runtime
    // viria com tudo desligado — um default silenciosamente errado. Melhor avisar
    // uma vez e deixar claro o que falta ligar naquela cena.
    private static DebugManager Resolve()
    {
        if (instance != null)
            return instance;

        DebugManager[] found = Resources.FindObjectsOfTypeAll<DebugManager>();
        for (int i = 0; i < found.Length; i++)
        {
            DebugManager candidate = found[i];
            if (candidate == null || candidate.gameObject == null)
                continue;
            if (!candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded)
                continue;

            instance = candidate;
            missingAuthorityWarned = false;
            return candidate;
        }

        if (!missingAuthorityWarned)
        {
            missingAuthorityWarned = true;
            Debug.LogWarning(
                "[DebugManager] Nenhum DebugManager nesta cena: atalhos de debug (', ; crase, Ctrl+D) "
                + "e de IA (F10/F11/F12) estao desligados. Adicione o componente DebugManager a um "
                + "GameObject permanente da cena, fora do Panel_Debug, e marque as flags desejadas.");
        }

        return null;
    }

    private void HandleAIDebugShortcuts()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return;
        bool f12 = Keyboard.current.f12Key.wasPressedThisFrame;
        bool f10 = Keyboard.current.f10Key.wasPressedThisFrame;
        bool f11 = Keyboard.current.f11Key.wasPressedThisFrame;
#else
        bool f12 = Input.GetKeyDown(KeyCode.F12);
        bool f10 = Input.GetKeyDown(KeyCode.F10);
        bool f11 = Input.GetKeyDown(KeyCode.F11);
#endif
        if (!f12 && !f10 && !f11) return;

        AIController ai = FindAnyObjectByType<AIController>();
        MatchController match = FindAnyObjectByType<MatchController>();

        if (f12)
        {
            if (match == null || !match.IsActiveTeamAI())
            {
                Debug.Log("[AI Shortcuts] F12 ignorado: o time ativo nao esta configurado como AI.");
            }
            else
            {
                ForceNeutralBeforeAIControlRelease("F12 AI Resume");
                if (ai != null) ai.SetDebugPaused(false);
                else Debug.Log("[AI Shortcuts] AIController não encontrado.");
                Debug.Log("[AI Shortcuts] F12 — AI Resume");
            }
        }
        if (f10)
        {
            if (ai != null) ai.SetDebugPaused(true);
            else Debug.Log("[AI Shortcuts] AIController não encontrado.");
            Debug.Log("[AI Shortcuts] F10 — AI Pause");
        }
        if (f11)
        {
            if (match == null || !match.IsActiveTeamAI())
            {
                Debug.Log("[AI Shortcuts] F11 ignorado: o time ativo nao esta configurado como AI.");
            }
            else
            {
                ForceNeutralBeforeAIControlRelease("F11 AI Step");
                if (ai != null) ai.RequestDebugStep();
                else Debug.Log("[AI Shortcuts] AIController nao encontrado.");
                Debug.Log("[AI Shortcuts] F11 — AI Step");
            }
        }
    }

    private static void ForceNeutralBeforeAIControlRelease(string reason)
    {
        if (!AIController.IsDebugPaused)
            return;

        TurnStateManager turnState = FindAnyObjectByType<TurnStateManager>();
        if (turnState == null)
        {
            Debug.LogWarning($"[AI Shortcuts] {reason}: TurnStateManager nao encontrado; comando continua sem normalizacao.");
            return;
        }

        TurnStateManager.CursorState previous = turnState.CurrentCursorState;
        bool hadSelection = turnState.SelectedUnit != null;
        turnState.ForceNeutral();
        Debug.Log($"[AI Shortcuts] {reason}: cursor {previous}->Neutral "
            + $"selection={(hadSelection ? "cancelada" : "nenhuma")} antes de liberar a IA.");
    }
}
