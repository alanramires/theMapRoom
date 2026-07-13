using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PanelRodadaController : MonoBehaviour
{
    [SerializeField] private Button botaoRodada;
    [SerializeField] private TMP_Text textoJogador;
    [SerializeField] private TMP_Text textoTurno;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip menuOpen;
    [SerializeField] private AudioClip menuClose;
    [SerializeField] private AudioClip aguardandoRodada;
    [SerializeField, Min(0f)] private float atrasoAntesMenuOpen = 0.5f;
    [SerializeField, Min(0.05f)] private float duracaoAnimacao = 0.3f;

    private CanvasGroup canvasGroup;
    private CanvasGroup buttonCanvasGroup;
    private RectTransform panelRect;
    private bool aguardandoConfirmacao;
    private bool confirmado;

    public bool IsPresenting { get; private set; }

    private void Awake()
    {
        panelRect = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (botaoRodada == null)
            botaoRodada = GetComponentInChildren<Button>(true);
        if (botaoRodada != null)
        {
            buttonCanvasGroup = botaoRodada.GetComponent<CanvasGroup>();
            if (buttonCanvasGroup == null)
                buttonCanvasGroup = botaoRodada.gameObject.AddComponent<CanvasGroup>();
        }
        TMP_Text[] textos = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textos.Length; i++)
        {
            if (textos[i].name == "text_rodada") textoJogador = textos[i];
            else if (textos[i].name == "text_turn") textoTurno = textos[i];
        }
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        if (botaoRodada != null)
            botaoRodada.onClick.AddListener(Confirmar);
    }

    private void Update()
    {
        if (aguardandoConfirmacao && (Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)))
            Confirmar();
    }

    public IEnumerator Apresentar(TeamId team, int numeroJogador, int turno)
    {
        IsPresenting = true;
        confirmado = false;
        aguardandoConfirmacao = false;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        canvasGroup.alpha = 1f; // cobre a tela imediatamente; nada do novo jogador vaza
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        panelRect.localScale = Vector3.one;

        if (textoJogador != null)
        {
            textoJogador.color = Color.white;
            string htmlColor = ColorUtility.ToHtmlStringRGB(TeamUtils.GetColor(team));
            textoJogador.text = $"Vez do Time <color=#{htmlColor}>{TeamUtils.GetName(team)}</color>";
        }
        if (textoTurno != null)
        {
            textoTurno.text = $"Turno {turno}";
            textoTurno.color = TeamUtils.GetColor(team);
        }
        if (botaoRodada != null)
            botaoRodada.interactable = false;

        SetContentAlpha(0f);
        if (atrasoAntesMenuOpen > 0f)
            yield return new WaitForSecondsRealtime(atrasoAntesMenuOpen);
        yield return PlayClip(menuOpen, false);
        yield return AnimateContent(0f, 1f);

        aguardandoConfirmacao = true;
        if (botaoRodada != null)
        {
            botaoRodada.interactable = true;
            if (buttonCanvasGroup != null)
            {
                buttonCanvasGroup.interactable = true;
                buttonCanvasGroup.blocksRaycasts = true;
            }
            botaoRodada.Select();
        }
        if (aguardandoRodada != null)
        {
            audioSource.clip = aguardandoRodada;
            audioSource.loop = true;
            audioSource.Play();
        }
        yield return new WaitUntil(() => confirmado);

        aguardandoConfirmacao = false;
        audioSource.Stop();
        audioSource.loop = false;
        if (botaoRodada != null) botaoRodada.interactable = false;
        if (buttonCanvasGroup != null)
        {
            buttonCanvasGroup.interactable = false;
            buttonCanvasGroup.blocksRaycasts = false;
        }
        if (menuClose != null)
        {
            audioSource.clip = menuClose;
            audioSource.loop = false;
            audioSource.Play();
        }
        yield return AnimateClose();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        IsPresenting = false;
        gameObject.SetActive(false);
    }

    private void Confirmar()
    {
        if (!aguardandoConfirmacao || confirmado) return;
        confirmado = true;
    }

    private IEnumerator PlayClip(AudioClip clip, bool loop)
    {
        if (clip == null) yield break;
        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.Play();
        if (!loop) yield return new WaitForSecondsRealtime(clip.length);
    }

    private IEnumerator AnimateContent(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < duracaoAnimacao)
        {
            elapsed += Time.unscaledDeltaTime;
            SetContentAlpha(Mathf.Lerp(from, to, elapsed / duracaoAnimacao));
            yield return null;
        }
        SetContentAlpha(to);
    }

    private IEnumerator AnimateClose()
    {
        float elapsed = 0f;
        while (elapsed < duracaoAnimacao)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duracaoAnimacao);
            canvasGroup.alpha = 1f - t;
            panelRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, t);
            yield return null;
        }
    }

    private void SetContentAlpha(float alpha)
    {
        SetAlpha(textoJogador, alpha);
        SetAlpha(textoTurno, alpha);
        if (buttonCanvasGroup != null)
        {
            buttonCanvasGroup.alpha = alpha;
            buttonCanvasGroup.blocksRaycasts = alpha >= 0.999f && aguardandoConfirmacao;
            buttonCanvasGroup.interactable = alpha >= 0.999f && aguardandoConfirmacao;
        }
    }

    private static void SetAlpha(TMP_Text text, float alpha)
    {
        if (text == null) return;
        Color color = text.color;
        color.a = alpha;
        text.color = color;
    }
}
