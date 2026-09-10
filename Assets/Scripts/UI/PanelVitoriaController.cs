using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Os botoes da tela de resultado: liga o clique, a navegacao e o som.
///
/// POR QUE ELE EXISTE. Quando a partida termina o tabuleiro congela — nao ha
/// cursor, nao ha turno, nao ha nada para clicar. A tela de vitoria passa a ser a
/// unica superficie viva, e ate agora ela era so texto: o jogador saia dali por
/// um Enter escondido no QuadranteController ou pelo Esc.
///
/// Mora num componente proprio, e nao no MatchController que ativa o painel,
/// porque o painel aparece em cena de campanha, de tutorial e de mapa avulso — e
/// em cada uma o conjunto de saidas e diferente. Perguntar isso e trabalho de
/// quem desenha a tela, nao de quem declara a vitoria.
///
/// COMO LIGAR: adicione este componente ao Panel_vitoria (o prefab ja tem os dois
/// botoes). Nao precisa arrastar mais nada — ele acha os filhos pelo nome e liga
/// o OnClick sozinho, entao vale para toda cena que instanciar o painel.
///
/// Nao mexe no OnClick que voce tiver ligado no Inspector: o AddListener soma, e
/// ligar o mesmo destino duas vezes so faria o LoadScene disparar duas vezes.
/// Se voce ligar a mao, desmarque <see cref="ligarCliqueAutomaticamente"/>.
/// </summary>
public class PanelVitoriaController : MonoBehaviour
{
    /// <summary>
    /// Verdadeiro enquanto esta tela esta conduzindo o teclado.
    ///
    /// O QuadranteController tambem escuta Enter depois do resultado, para as
    /// cenas em que o painel nao tem botoes. Sem esta bandeira, o mesmo Enter
    /// acionaria o botao E a volta escondida — dois LoadScene no mesmo frame.
    /// </summary>
    public static bool EstaConduzindo { get; private set; }

    [Header("Botoes — achados por nome entre os filhos")]
    [SerializeField] private string nomeBotaoCampanha = "Button_campanha";
    [SerializeField] private string nomeBotaoMenuPrincipal = "Button_principal";

    [Header("Comportamento")]
    [Tooltip(
        "Liga o OnClick por codigo. Desmarque se voce preferir ligar a mao no "
        + "Inspector — com os dois ligados, o destino dispara duas vezes.")]
    [SerializeField] private bool ligarCliqueAutomaticamente = true;
    [Tooltip(
        "Esconde 'Campanha' quando esta partida nao veio da campanha. Um botao que "
        + "nao leva a lugar nenhum e pior que botao nenhum.")]
    [SerializeField] private bool esconderCampanhaQuandoIndisponivel = true;

    private Button botaoCampanha;
    private Button botaoMenuPrincipal;
    private readonly System.Collections.Generic.List<Button> foco =
        new System.Collections.Generic.List<Button>();
    private int indiceFoco;
    private int frameDeAbertura;
    private bool acionado;
    private CursorController cursor;

    private void OnEnable()
    {
        cursor = FindAnyObjectByType<CursorController>();
        acionado = false;
        frameDeAbertura = Time.frameCount;

        ResolverBotoes();
        MontarFoco();

        EstaConduzindo = foco.Count > 0;
        AplicarDestaque();
    }

    private void OnDisable()
    {
        EstaConduzindo = false;
    }

    private void ResolverBotoes()
    {
        if (botaoCampanha != null || botaoMenuPrincipal != null)
            return;

        foreach (Button b in GetComponentsInChildren<Button>(includeInactive: true))
        {
            if (b == null)
                continue;

            if (string.Equals(b.name, nomeBotaoCampanha, System.StringComparison.OrdinalIgnoreCase))
                botaoCampanha = b;
            else if (string.Equals(b.name, nomeBotaoMenuPrincipal, System.StringComparison.OrdinalIgnoreCase))
                botaoMenuPrincipal = b;
        }

        if (botaoCampanha == null && botaoMenuPrincipal == null)
        {
            Debug.LogWarning(
                $"[Vitoria] Nenhum botao encontrado em '{name}'. Procurei por "
                + $"'{nomeBotaoCampanha}' e '{nomeBotaoMenuPrincipal}' entre os filhos. "
                + "A tela de resultado vai aparecer sem saida.",
                this);
            return;
        }

        // A NAVEGACAO EMBUTIDA DA UNITY SAI DO CAMINHO.
        //
        // Como o EventSystem fica com um Button selecionado (e dele que vem o
        // destaque visual), o modulo de input da Unity tambem processaria setas e
        // Submit. Duas maquinas movendo o mesmo foco desencontram: o indice daqui
        // apontaria para um botao e a selecao para outro.
        //
        // Runtime apenas — nao serializa, entao o prefab nao muda.
        DesligarNavegacaoEmbutida(botaoCampanha);
        DesligarNavegacaoEmbutida(botaoMenuPrincipal);

        if (!ligarCliqueAutomaticamente)
            return;

        // RemoveListener antes de somar: OnEnable roda toda vez que o painel
        // reaparece, e sem isto o mesmo destino acumularia um disparo por abertura.
        if (botaoCampanha != null)
        {
            botaoCampanha.onClick.RemoveListener(AcionarCampanha);
            botaoCampanha.onClick.AddListener(AcionarCampanha);
        }

        if (botaoMenuPrincipal != null)
        {
            botaoMenuPrincipal.onClick.RemoveListener(AcionarMenuPrincipal);
            botaoMenuPrincipal.onClick.AddListener(AcionarMenuPrincipal);
        }
    }

    private static void DesligarNavegacaoEmbutida(Button botao)
    {
        if (botao == null)
            return;

        UnityEngine.UI.Navigation nav = botao.navigation;
        nav.mode = UnityEngine.UI.Navigation.Mode.None;
        botao.navigation = nav;
    }

    /// <summary>
    /// Monta a lista navegavel. "Campanha" so entra quando ha campanha para onde
    /// voltar — a mesma pergunta que o menu do Esc faz, para as duas telas nunca
    /// oferecerem coisas diferentes.
    /// </summary>
    private void MontarFoco()
    {
        foco.Clear();

        bool temCampanha = QuadranteController.PodeVoltarParaCampanha;
        if (botaoCampanha != null)
        {
            if (esconderCampanhaQuandoIndisponivel)
                botaoCampanha.gameObject.SetActive(temCampanha);

            if (temCampanha)
                foco.Add(botaoCampanha);
        }

        if (botaoMenuPrincipal != null && botaoMenuPrincipal.gameObject.activeSelf)
            foco.Add(botaoMenuPrincipal);

        // Depois de um resultado de campanha, voltar ao mapa e o destino provavel.
        indiceFoco = 0;
    }

    private void Update()
    {
        if (acionado || foco.Count == 0)
            return;

        // O mesmo Enter que fechou a ultima acao da partida nao pode acionar um
        // botao que acabou de aparecer. Exige tecla nova.
        if (Time.frameCount <= frameDeAbertura)
            return;

        if (foco.Count > 1)
        {
            if (Anterior())
            {
                Mover(-1);
                return;
            }

            if (Proximo())
            {
                Mover(+1);
                return;
            }
        }

        if (Confirmar())
            foco[Mathf.Clamp(indiceFoco, 0, foco.Count - 1)].onClick.Invoke();
    }

    private void Mover(int direcao)
    {
        indiceFoco = (indiceFoco + direcao + foco.Count) % foco.Count;
        cursor?.PlayCursorMoveSfx();
        AplicarDestaque();
    }

    /// <summary>
    /// Aponta o EventSystem para o botao em foco: e o que da o destaque visual do
    /// proprio Button, sem precisar de arte nem de highlight paralelo.
    /// </summary>
    private void AplicarDestaque()
    {
        if (foco.Count == 0)
            return;

        indiceFoco = Mathf.Clamp(indiceFoco, 0, foco.Count - 1);
        UnityEngine.EventSystems.EventSystem events = UnityEngine.EventSystems.EventSystem.current;
        if (events != null)
            events.SetSelectedGameObject(foco[indiceFoco].gameObject);
    }

    private void AcionarCampanha()
    {
        if (acionado)
            return;

        acionado = true;
        EstaConduzindo = false;
        cursor?.PlayConfirmSfx();

        if (!QuadranteController.TryVoltarParaCampanha())
        {
            Debug.LogWarning(
                "[Vitoria] 'Campanha' acionado sem campanha para onde voltar. Nada feito.",
                this);
            acionado = false;
            EstaConduzindo = foco.Count > 0;
        }
    }

    private void AcionarMenuPrincipal()
    {
        if (acionado)
            return;

        acionado = true;
        EstaConduzindo = false;
        cursor?.PlayConfirmSfx();

        BattleMapMenuRootController menu = FindAnyObjectByType<BattleMapMenuRootController>(
            FindObjectsInactive.Include);
        if (menu != null)
        {
            menu.BotaoVoltarAoMenuPrincipal();
            return;
        }

        Debug.LogError(
            "[Vitoria] 'Menu Principal' acionado mas nao ha BattleMapMenuRootController "
            + "nesta cena — e e ele quem conhece o nome da cena de menu. O jogador fica "
            + "preso na tela de resultado.",
            this);
        acionado = false;
        EstaConduzindo = foco.Count > 0;
    }

    private static bool Anterior()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.leftArrowKey.wasPressedThisFrame ||
             Keyboard.current.upArrowKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow);
    }

    private static bool Proximo()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.rightArrowKey.wasPressedThisFrame ||
             Keyboard.current.downArrowKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow);
    }

    private static bool Confirmar()
    {
        if (RemoteInput.ConfirmDownThisFrame())
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame ||
             Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }
}
