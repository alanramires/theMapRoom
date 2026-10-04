using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Panel_Configuracoes — a gaveta das PREFERÊNCIAS (docs/playtest de 3 de outubro 2026.md).
///
///   Modo Turbo    → AIController.iaRapida        gravado (PreferenciasDoJogador)
///   Ação Direta   → MatchController.atalhoContextual  gravado
///   Tela Cheia    → Screen.fullScreen            ESPELHO da tela, nunca gravado
///
/// Os dois primeiros valem na próxima partida (o Awake de cada dono lê a preferência) e,
/// se a partida já estiver aberta, na hora.
///
/// A tela cheia é diferente: o navegador derruba a tela cheia quando o celular bloqueia,
/// e só aceita voltar com um gesto do jogador — no PointerDown, não no release em que o
/// Toggle muda. Por isso o check não MANDA na tela: ele pede no toque e, a cada quadro,
/// apenas reflete o que a tela realmente está fazendo.
///
/// Teclado: setas, Enter e Esc chegam pelo MainMenuStateController (RouteConfigMenuInput),
/// como em todo painel do menu; a navegação nativa da Unity fica desligada.
/// </summary>
public sealed class PainelConfiguracoesController : MonoBehaviour
{
    [SerializeField] private Toggle toggleModoTurbo;
    [SerializeField] private Toggle toggleAcaoDireta;
    [SerializeField] private Toggle toggleTelaCheia;
    [SerializeField] private Button buttonVoltar;
    [SerializeField] private Button buttonSobre;

    private MainMenuStateController stateController;
    private PanelMenu panelMenu;
    private CursorController cursorController;
    private bool telaCheiaPedidaPorToque;
    private bool ligado;
    private int selectedIndex;
    private Action aoFechar;
    private int abertoNoQuadro = -1;

    /// <summary>
    /// Abre a tela a partir de um menu de PARTIDA (Batalha, Campanha), onde não há
    /// MainMenuStateController: a tela roteia o próprio teclado e, ao fechar, chama
    /// <paramref name="onClosed"/> para o menu que a abriu se reabrir. O painel é um
    /// prefab presente em cada cena; sem ele na cena, devolve false.
    /// </summary>
    public static bool AbrirNaCena(Action onClosed)
    {
        Scene ativa = SceneManager.GetActiveScene();
        PainelConfiguracoesController[] todos = FindObjectsByType<PainelConfiguracoesController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < todos.Length; i++)
        {
            PainelConfiguracoesController painel = todos[i];
            if (painel == null || painel.gameObject.scene != ativa)
                continue;
            painel.aoFechar = onClosed;
            painel.abertoNoQuadro = Time.frameCount;
            painel.gameObject.SetActive(true);
            return true;
        }
        return false;
    }

    // Na Tela de Entrada quem manda no teclado é o MainMenuStateController (estado
    // Config). Fora dela — o prefab dentro da Batalha ou da Campanha — a tela se vira.
    private bool RoteiaProprioInput =>
        stateController == null
        || !stateController.isActiveAndEnabled
        || stateController.CurrentState != MainMenuState.Config;

    private void Awake()
    {
        ResolverReferencias();
    }

    private void OnEnable()
    {
        ResolverReferencias();
        Ligar();
        Sincronizar();

        // Teclado: como os outros painéis do menu, navegação PRÓPRIA — o
        // MainMenuStateController (RouteConfigMenuInput) repassa setas, Enter e Esc.
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = false;
        // O Sobre mora no menu da Tela de Entrada (PanelMenu). Na Batalha e na
        // Campanha não há onde abri-lo: o botão some em vez de não fazer nada.
        if (buttonSobre != null)
            buttonSobre.gameObject.SetActive(panelMenu != null);

        AplicarDestaqueDeSelecao();
        selectedIndex = 0;
        SelecionarAtual(playSfx: false);

        // A bússola é o cursor do menu raiz; aqui o destaque é a cor de seleção.
        // Mesmo padrão do Load e do Tutorial (SetCompassCursorVisible).
        panelMenu?.SetCompassCursorVisible(false);
    }

    // A mesma cor do Panel_NewGame (NewGamePanelController.ApplySelectionHighlight).
    // O Toggle padrão da Unity seleciona com (245,245,245) sobre o quadradinho branco:
    // a seta andava, mas ninguém via.
    private static readonly Color CorDeSelecao = new Color(0.290f, 0.353f, 0.263f); // #4A5A43

    private void AplicarDestaqueDeSelecao()
    {
        List<Selectable> itens = ItensNavegaveis();
        for (int i = 0; i < itens.Count; i++)
        {
            ColorBlock cores = itens[i].colors;
            cores.selectedColor = CorDeSelecao;
            itens[i].colors = cores;
        }
    }

    private void OnDisable()
    {
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
        panelMenu?.SetCompassCursorVisible(true);
        DevolverHelperAoLugar();
    }

    // ── Teclado ───────────────────────────────────────────────────────────

    /// <summary>
    /// Ordem de LEITURA da tela, tirada da posição de cada item, e não de quem o
    /// controlador reconheceu: de cima para baixo e, na mesma linha (o rodapé), da
    /// esquerda para a direita. Um check novo entra na navegação sem tocar no código.
    /// </summary>
    private List<Selectable> ItensNavegaveis()
    {
        var itens = new List<Selectable>(GetComponentsInChildren<Selectable>(false));
        itens.RemoveAll(item => item == null || !item.gameObject.activeInHierarchy);
        itens.Sort((a, b) =>
        {
            // No espaço local do painel (unidades de UI), qualquer que seja o modo do Canvas.
            Vector3 pa = transform.InverseTransformPoint(a.transform.position);
            Vector3 pb = transform.InverseTransformPoint(b.transform.position);
            // Mesma linha: tolera desalinhamento de alguns pixels do arranjo "no olho".
            if (Mathf.Abs(pa.y - pb.y) > 8f)
                return pb.y.CompareTo(pa.y);
            return pa.x.CompareTo(pb.x);
        });
        return itens;
    }

    public bool Navigate(int direction)
    {
        List<Selectable> itens = ItensNavegaveis();
        if (itens.Count == 0 || direction == 0)
            return false;

        // Se o mouse/dedo mexeu na seleção, a seta parte de onde ele deixou.
        GameObject atual = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        for (int i = 0; atual != null && i < itens.Count; i++)
            if (itens[i].gameObject == atual)
                selectedIndex = i;

        int passo = direction > 0 ? 1 : -1;
        int indice = Mathf.Clamp(selectedIndex, 0, itens.Count - 1);
        for (int i = 0; i < itens.Count; i++)
        {
            indice = (indice + passo + itens.Count) % itens.Count;
            if (itens[indice] != null && itens[indice].interactable && itens[indice].gameObject.activeInHierarchy)
            {
                selectedIndex = indice;
                SelecionarAtual(playSfx: true);
                return true;
            }
        }
        return false;
    }

    public void ConfirmCurrentSelection()
    {
        List<Selectable> itens = ItensNavegaveis();
        if (itens.Count == 0)
            return;

        Selectable item = itens[Mathf.Clamp(selectedIndex, 0, itens.Count - 1)];
        if (item == null || !item.interactable)
            return;

        // Enter num check inverte (o onValueChanged faz o resto — na tela cheia, o
        // próprio keydown é o gesto que o navegador aceita); num botão, aperta.
        if (item is Toggle toggle)
            toggle.isOn = !toggle.isOn;
        else if (item is Button button)
            button.onClick?.Invoke();
    }

    private void SelecionarAtual(bool playSfx)
    {
        List<Selectable> itens = ItensNavegaveis();
        if (itens.Count == 0)
            return;

        Selectable item = itens[Mathf.Clamp(selectedIndex, 0, itens.Count - 1)];
        if (item != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(item.gameObject);
        if (playSfx)
            cursorController?.PlayCursorMoveSfx();
    }

    private void Update()
    {
        if (RoteiaProprioInput)
            RotearInputProprio();

        // O Sobre também fecha pelo botão OK do próprio helper, sem passar por FecharSobre.
        if (ramoDoHelperErguido != null && !SobreAberto)
            DevolverHelperAoLugar();

        // O espelho: o navegador pode ter derrubado a tela cheia sem avisar ninguém.
        if (toggleTelaCheia != null && toggleTelaCheia.isOn != Screen.fullScreen)
            toggleTelaCheia.SetIsOnWithoutNotify(Screen.fullScreen);
    }

    /// <summary>O texto do Sobre está aberto no helper? Enquanto estiver, Esc fecha só ele.</summary>
    public bool SobreAberto => panelMenu != null && panelMenu.IsAboutOpen;

    // O estado do Sobre continua sendo do PanelMenu (o PanelHelperController lê de lá);
    // esta tela só é dona do BOTÃO e de quem o Esc fecha primeiro.
    private void AbrirSobre()
    {
        if (panelMenu == null)
            return;
        panelMenu.OpenAbout();
        TrazerHelperParaFrente();
    }

    public void FecharSobre()
    {
        if (panelMenu == null)
            return;
        panelMenu.CloseAbout();
        DevolverHelperAoLugar();
        int indiceSobre = ItensNavegaveis().IndexOf(buttonSobre);
        if (indiceSobre >= 0)
            selectedIndex = indiceSobre;
        SelecionarAtual(playSfx: false);
    }

    // O texto do Sobre mora no panel_helper, que na hierarquia fica ATRÁS desta tela:
    // o painel de Configurações cobria a borda esquerda do texto. Enquanto o Sobre
    // estiver aberto, o ramo do helper sobe para depois do ramo desta tela (no ancestral
    // comum) e, ao fechar, volta para o índice de antes.
    private Transform ramoDoHelperErguido;
    private int indiceOriginalDoRamo = -1;

    private void TrazerHelperParaFrente()
    {
        DevolverHelperAoLugar();
        RectTransform helper = PanelHelperController.PanelRect;
        if (helper == null)
            return;

        Transform ancestral = transform;
        while (ancestral != null && !helper.IsChildOf(ancestral))
            ancestral = ancestral.parent;
        if (ancestral == null)
            return; // Canvas diferentes: a ordem de irmãos não decide.

        Transform ramoHelper = RamoSob(ancestral, helper);
        Transform ramoConfig = RamoSob(ancestral, transform);
        if (ramoHelper == null || ramoConfig == null || ramoHelper == ramoConfig)
            return;
        if (ramoHelper.GetSiblingIndex() > ramoConfig.GetSiblingIndex())
            return; // já está na frente

        ramoDoHelperErguido = ramoHelper;
        indiceOriginalDoRamo = ramoHelper.GetSiblingIndex();
        ramoHelper.SetSiblingIndex(ramoConfig.GetSiblingIndex());
    }

    private void DevolverHelperAoLugar()
    {
        if (ramoDoHelperErguido != null && indiceOriginalDoRamo >= 0)
            ramoDoHelperErguido.SetSiblingIndex(indiceOriginalDoRamo);
        ramoDoHelperErguido = null;
        indiceOriginalDoRamo = -1;
    }

    // O filho direto de 'ancestral' que contém 'alvo'.
    private static Transform RamoSob(Transform ancestral, Transform alvo)
    {
        Transform t = alvo;
        while (t != null && t.parent != ancestral)
            t = t.parent;
        return t;
    }

    /// <summary>Voltar / Esc. Volta ao menu raiz.</summary>
    public void Fechar()
    {
        if (stateController != null && stateController.CurrentState == MainMenuState.Config)
        {
            stateController.RequestState(MainMenuState.RootMenu);
            cursorController?.PlayCancelSfx();
            return;
        }

        // O mesmo Esc/toque não pode vazar para o tabuleiro ou para o menu que reabre.
        UiInputBlocker.SuppressGameplayInputForFrames(2);
        gameObject.SetActive(false);
        cursorController?.PlayCancelSfx();
        Action callback = aoFechar;
        aoFechar = null;
        callback?.Invoke();
    }

    // Mesmo contrato do RouteConfigMenuInput do MainMenuStateController.
    private void RotearInputProprio()
    {
        UiInputBlocker.SuppressGameplayInputForFrames(1);
        // O Enter/toque que abriu a tela não pode, no mesmo quadro, apertar um item.
        if (Time.frameCount <= abertoNoQuadro + 1)
            return;

        if (SobreAberto)
        {
            if (TeclaConfirmar() || TeclaCancelar())
                FecharSobre();
            return;
        }

        if (TeclaCima() || TeclaEsquerda()) { Navigate(-1); return; }
        if (TeclaBaixo() || TeclaDireita()) { Navigate(+1); return; }
        if (TeclaConfirmar()) { ConfirmCurrentSelection(); return; }
        if (TeclaCancelar()) Fechar();
    }

    private static bool TeclaCima()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
    }

    private static bool TeclaBaixo()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
    }

    private static bool TeclaEsquerda()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
    }

    private static bool TeclaDireita()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
    }

    private static bool TeclaConfirmar()
    {
        if (RemoteInput.ConfirmDownThisFrame())
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private static bool TeclaCancelar()
    {
        if (RemoteInput.CancelDownThisFrame() || RemoteInput.RightClickCancelDownThisFrame())
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }

    private void Sincronizar()
    {
        AIController ai = FindAnyObjectByType<AIController>();
        MatchController match = FindAnyObjectByType<MatchController>();

        if (toggleModoTurbo != null)
            toggleModoTurbo.SetIsOnWithoutNotify(
                PreferenciasDoJogador.ModoTurbo(ai != null && ai.IARapida));
        if (toggleAcaoDireta != null)
            toggleAcaoDireta.SetIsOnWithoutNotify(
                PreferenciasDoJogador.AcaoDireta(match != null && match.AtalhoContextual));
        if (toggleTelaCheia != null)
            toggleTelaCheia.SetIsOnWithoutNotify(Screen.fullScreen);
    }

    private void Ligar()
    {
        if (ligado)
            return;
        ligado = true;

        if (toggleModoTurbo != null)
            toggleModoTurbo.onValueChanged.AddListener(OnModoTurbo);
        if (toggleAcaoDireta != null)
            toggleAcaoDireta.onValueChanged.AddListener(OnAcaoDireta);
        if (toggleTelaCheia != null)
        {
            toggleTelaCheia.onValueChanged.AddListener(OnTelaCheiaToggle);
            GestoTelaCheia gesto = toggleTelaCheia.GetComponent<GestoTelaCheia>();
            if (gesto == null)
                gesto = toggleTelaCheia.gameObject.AddComponent<GestoTelaCheia>();
            gesto.dono = this;
        }
        if (buttonVoltar != null)
            buttonVoltar.onClick.AddListener(Fechar);
        if (buttonSobre != null)
            buttonSobre.onClick.AddListener(AbrirSobre);
    }

    private void OnModoTurbo(bool valor)
    {
        PreferenciasDoJogador.SetModoTurbo(valor);
        FindAnyObjectByType<AIController>()?.SetIARapida(valor);
        cursorController?.PlayConfirmSfx();
    }

    private void OnAcaoDireta(bool valor)
    {
        PreferenciasDoJogador.SetAcaoDireta(valor);
        FindAnyObjectByType<MatchController>()?.SetAtalhoContextual(valor);
        cursorController?.PlayConfirmSfx();
    }

    // Toque/clique: o pedido já foi feito no PointerDown (o gesto que o navegador
    // aceita); aqui só consome a marca. Teclado (Enter/Submit) não passa pelo
    // PointerDown — o próprio keydown é o gesto, então o pedido sai daqui.
    private void OnTelaCheiaToggle(bool _)
    {
        if (telaCheiaPedidaPorToque)
            telaCheiaPedidaPorToque = false;
        else
            FullscreenShortcutButton.SetFullscreen(!Screen.fullScreen);

        toggleTelaCheia.SetIsOnWithoutNotify(Screen.fullScreen);
        cursorController?.PlayConfirmSfx();
    }

    internal void PedirTelaCheiaNoToque()
    {
        telaCheiaPedidaPorToque = true;
        FullscreenShortcutButton.SetFullscreen(!Screen.fullScreen);
    }

    private void ResolverReferencias()
    {
        if (stateController == null)
            stateController = FindAnyObjectByType<MainMenuStateController>();
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (panelMenu == null)
            panelMenu = FindAnyObjectByType<PanelMenu>(FindObjectsInactive.Include);

        // Sem ligação no Inspector, acha pelo texto do rótulo (pt ou en).
        Toggle[] toggles = GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < toggles.Length; i++)
        {
            string rotulo = LerRotulo(toggles[i]);
            if (toggleModoTurbo == null && rotulo.Contains("turbo"))
                toggleModoTurbo = toggles[i];
            else if (toggleAcaoDireta == null
                && (rotulo.Contains("diret") || rotulo.Contains("direct") || rotulo.Contains("contextual")))
                toggleAcaoDireta = toggles[i];
            else if (toggleTelaCheia == null
                && (rotulo.Contains("cheia") || rotulo.Contains("fullscreen") || rotulo.Contains("full screen")))
                toggleTelaCheia = toggles[i];
        }

        if (buttonVoltar == null || buttonSobre == null)
        {
            Button[] botoes = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < botoes.Length; i++)
            {
                string nome = botoes[i].name.ToLowerInvariant();
                if (buttonVoltar == null && nome.Contains("voltar"))
                    buttonVoltar = botoes[i];
                else if (buttonSobre == null && (nome.Contains("sobre") || nome.Contains("about")))
                    buttonSobre = botoes[i];
            }
        }
    }

    // O rótulo é o filho "Label" do Toggle (o nome que a Unity cria). Pegar o primeiro
    // texto qualquer lia a AJUDA quando ela é filha do toggle — e a ajuda do Modo Turbo
    // ("o cursor salta direto") passava por Ação Direta.
    private static string LerRotulo(Toggle toggle)
    {
        Transform label = toggle.transform.Find("Label");
        if (label != null)
        {
            string texto = LerTexto(label);
            if (!string.IsNullOrEmpty(texto))
                return texto;
        }
        return LerTexto(toggle.transform);
    }

    private static string LerTexto(Transform raiz)
    {
        TMP_Text tmp = raiz.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            return tmp.text.ToLowerInvariant();
        Text legado = raiz.GetComponentInChildren<Text>(true);
        return legado != null ? legado.text.ToLowerInvariant() : string.Empty;
    }
}

/// <summary>Pendurado no check de tela cheia em runtime: pede no PointerDown.</summary>
internal sealed class GestoTelaCheia : MonoBehaviour, IPointerDownHandler
{
    public PainelConfiguracoesController dono;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (dono == null || eventData == null
            || eventData.button != PointerEventData.InputButton.Left)
            return;
        dono.PedirTelaCheiaNoToque();
    }
}
