using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PanelRodadaController : MonoBehaviour
{
    private static int gameplayInputBlockCount;

    [SerializeField] private Button botaoRodada;
    [SerializeField] private TMP_Text textoJogador;
    [SerializeField] private TMP_Text textoTurno;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip menuOpen;
    [SerializeField] private AudioClip menuClose;
    [SerializeField] private AudioClip aguardandoRodada;
    [SerializeField, Min(0f)] private float atrasoAntesMenuOpen = 0.5f;
    [SerializeField, Min(0.05f)] private float duracaoAnimacao = 0.3f;
    [SerializeField, Min(0f)] private float loadingPlayerTextVerticalOffset = 55f;

    private CanvasGroup canvasGroup;
    private CanvasGroup buttonCanvasGroup;
    private RectTransform panelRect;
    private bool aguardandoConfirmacao;
    private bool confirmado;
    private int presentationVersion;
    private Coroutine loadingOpeningAudioRoutine;
    private bool gameplayInputBlockRegistered;
    private Vector2 textoJogadorDefaultAnchoredPosition;
    private bool textoJogadorPositionCached;

    public bool IsPresenting { get; private set; }
    public static bool IsGameplayInputBlocked => gameplayInputBlockCount > 0 || SaveGameManager.IsAnyLoadInProgress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGameplayInputGate()
    {
        gameplayInputBlockCount = 0;
    }

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
        CachePlayerTextPosition();
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

    private void OnDisable()
    {
        ReleaseGameplayInputBlock();
    }

    public IEnumerator Apresentar(TeamId team, int numeroJogador, int turno)
    {
        int version = ++presentationVersion;
        IsPresenting = true;
        AcquireGameplayInputBlock();
        confirmado = false;
        aguardandoConfirmacao = false;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        canvasGroup.alpha = 1f; // cobre a tela imediatamente; nada do novo jogador vaza
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        panelRect.localScale = Vector3.one;
        RestorePlayerTextPosition();

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
        if (version != presentationVersion) yield break;
        yield return PlayClip(menuOpen, false);
        if (version != presentationVersion) yield break;
        yield return AnimateContent(0f, 1f);
        if (version != presentationVersion) yield break;

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
        if (version != presentationVersion) yield break;

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
        ReleaseGameplayInputBlock();
        gameObject.SetActive(false);
    }

    public void BeginLoadingPresentation()
    {
        presentationVersion++;
        IsPresenting = true;
        AcquireGameplayInputBlock();
        confirmado = false;
        aguardandoConfirmacao = false;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        panelRect.localScale = Vector3.one;
        RestorePlayerTextPosition();

        if (textoJogador != null)
        {
            textoJogador.color = Color.white;
            textoJogador.text = "Carregando jogo...";
        }
        if (textoTurno != null)
        {
            textoTurno.color = Color.white;
            textoTurno.text = string.Empty;
        }
        SetButtonEnabled(false);
        SetContentAlpha(1f);
        if (loadingOpeningAudioRoutine != null)
            StopCoroutine(loadingOpeningAudioRoutine);
        loadingOpeningAudioRoutine = StartCoroutine(PlayLoadingOpeningAudio());
    }

    public void SetLoadingTeam(TeamId team, int turno)
    {
        if (!IsPresenting)
            BeginLoadingPresentation();

        if (textoJogador != null)
        {
            ApplyLoadingPlayerTextPosition();
            string htmlColor = ColorUtility.ToHtmlStringRGB(TeamUtils.GetColor(team));
            string teamName = TeamUtils.GetName(team).ToUpperInvariant();
            textoJogador.color = Color.white;
            textoJogador.text = $"Carregando turno do jogador\n<color=#{htmlColor}>{teamName}</color>";
        }
        if (textoTurno != null)
        {
            textoTurno.text = $"Turno {turno}";
            textoTurno.color = TeamUtils.GetColor(team);
        }
        SetButtonEnabled(false);
    }

    public IEnumerator ReleaseLoadingPresentation(TeamId team, int numeroJogador, int turno)
    {
        int version = presentationVersion;
        if (!IsPresenting)
            BeginLoadingPresentation();
        version = presentationVersion;

        // Mesmo quando o restore termina muito rapido, preserva a abertura sonora
        // completa ainda na fase "Carregando": menu_open -> aguardandoRodada.
        // So depois que o loop de espera entrou liberamos "Vez do Time" e o botao.
        if (loadingOpeningAudioRoutine != null)
            yield return new WaitUntil(() => loadingOpeningAudioRoutine == null || version != presentationVersion);
        if (version != presentationVersion)
            yield break;

        RestorePlayerTextPosition();

        if (textoJogador != null)
        {
            string htmlColor = ColorUtility.ToHtmlStringRGB(TeamUtils.GetColor(team));
            textoJogador.color = Color.white;
            textoJogador.text = $"Vez do Time <color=#{htmlColor}>{TeamUtils.GetName(team)}</color>";
        }
        if (textoTurno != null)
        {
            textoTurno.text = $"Turno {turno}";
            textoTurno.color = TeamUtils.GetColor(team);
        }

        aguardandoConfirmacao = true;
        SetButtonEnabled(true);
        if (botaoRodada != null)
            botaoRodada.Select();
        // Se menu_open ainda estiver tocando, a propria sequencia inicia o loop
        // logo depois. Se ja terminou, preserva/garante aguardandoRodada agora.
        if (loadingOpeningAudioRoutine == null)
            StartWaitingAudio();

        yield return new WaitUntil(() => confirmado || version != presentationVersion);
        if (version != presentationVersion)
            yield break;

        aguardandoConfirmacao = false;
        if (loadingOpeningAudioRoutine != null)
        {
            StopCoroutine(loadingOpeningAudioRoutine);
            loadingOpeningAudioRoutine = null;
        }
        audioSource.Stop();
        audioSource.loop = false;
        SetButtonEnabled(false);
        if (menuClose != null)
        {
            audioSource.clip = menuClose;
            audioSource.Play();
        }
        yield return AnimateClose();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        IsPresenting = false;
        ReleaseGameplayInputBlock();
        gameObject.SetActive(false);
    }

    public void CancelLoadingPresentation()
    {
        presentationVersion++;
        aguardandoConfirmacao = false;
        confirmado = false;
        if (loadingOpeningAudioRoutine != null)
        {
            StopCoroutine(loadingOpeningAudioRoutine);
            loadingOpeningAudioRoutine = null;
        }
        audioSource.Stop();
        audioSource.loop = false;
        SetButtonEnabled(false);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        IsPresenting = false;
        ReleaseGameplayInputBlock();
        gameObject.SetActive(false);
    }

    private void SetButtonEnabled(bool enabled)
    {
        if (botaoRodada != null)
            botaoRodada.interactable = enabled;
        if (buttonCanvasGroup != null)
        {
            buttonCanvasGroup.interactable = enabled;
            buttonCanvasGroup.blocksRaycasts = enabled;
            buttonCanvasGroup.alpha = 1f;
        }
    }

    private void StartWaitingAudio()
    {
        if (aguardandoRodada == null || audioSource == null)
            return;
        if (audioSource.isPlaying && audioSource.clip == aguardandoRodada && audioSource.loop)
            return;
        audioSource.clip = aguardandoRodada;
        audioSource.loop = true;
        audioSource.Play();
    }

    private void AcquireGameplayInputBlock()
    {
        if (gameplayInputBlockRegistered)
            return;
        gameplayInputBlockRegistered = true;
        gameplayInputBlockCount++;
    }

    private void ReleaseGameplayInputBlock()
    {
        if (!gameplayInputBlockRegistered)
            return;
        gameplayInputBlockRegistered = false;
        gameplayInputBlockCount = Mathf.Max(0, gameplayInputBlockCount - 1);
    }

    private IEnumerator PlayLoadingOpeningAudio()
    {
        if (menuOpen != null)
            yield return PlayClip(menuOpen, false);

        // Sequencia unica do painel: menu_open uma vez e, em seguida,
        // aguardandoRodada em loop. O loop atravessa a troca de
        // "Carregando" para "Vez do Time" e so para na confirmacao.
        StartWaitingAudio();
        loadingOpeningAudioRoutine = null;
    }

    private void CachePlayerTextPosition()
    {
        if (textoJogadorPositionCached || textoJogador == null)
            return;
        textoJogadorDefaultAnchoredPosition = textoJogador.rectTransform.anchoredPosition;
        textoJogadorPositionCached = true;
    }

    private void ApplyLoadingPlayerTextPosition()
    {
        CachePlayerTextPosition();
        if (textoJogador != null && textoJogadorPositionCached)
            textoJogador.rectTransform.anchoredPosition = textoJogadorDefaultAnchoredPosition + Vector2.up * loadingPlayerTextVerticalOffset;
    }

    private void RestorePlayerTextPosition()
    {
        CachePlayerTextPosition();
        if (textoJogador != null && textoJogadorPositionCached)
            textoJogador.rectTransform.anchoredPosition = textoJogadorDefaultAnchoredPosition;
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
