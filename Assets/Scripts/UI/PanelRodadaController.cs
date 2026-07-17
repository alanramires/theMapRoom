using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

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
    [Header("Team Loading Video")]
    [SerializeField] private VideoClip videoVerde;
    [SerializeField] private VideoClip videoVermelho;
    [SerializeField] private VideoClip videoAzul;
    [SerializeField] private VideoClip videoAmarelo;
    [SerializeField] private Vector2 videoSize = new Vector2(720f, 360f);
    [SerializeField] private Vector2 videoAnchoredPosition = new Vector2(0f, -320f);
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
    private RawImage teamVideoImage;
    private VideoPlayer teamVideoPlayer;
    private RenderTexture teamVideoTexture;
    private TeamId playingVideoTeam = TeamId.Neutral;
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
        EnsureTeamVideoPlayer();
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
        StopTeamVideo();
        ReleaseGameplayInputBlock();
    }

    private void OnDestroy()
    {
        if (teamVideoPlayer != null)
            teamVideoPlayer.frameReady -= HandleTeamVideoFrameReady;
        if (teamVideoTexture == null)
            return;
        teamVideoTexture.Release();
        Destroy(teamVideoTexture);
        teamVideoTexture = null;
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
        StartTeamVideo(team);
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
        StopTeamVideo();

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

    public IEnumerator ReleaseLoadingPresentation(
        TeamId team,
        int numeroJogador,
        int turno,
        System.Action onButtonReady = null)
    {
        int version = presentationVersion;
        if (!IsPresenting)
            BeginLoadingPresentation();
        version = presentationVersion;

        // O audio de abertura e apenas apresentacao: ele pode continuar tocando,
        // mas nunca deve segurar a liberacao do turno depois que o save foi restaurado.
        RestorePlayerTextPosition();
        StartTeamVideo(team);

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
        onButtonReady?.Invoke();
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
        StopTeamVideo();
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
        StopTeamVideo();
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

    private void EnsureTeamVideoPlayer()
    {
        if (teamVideoPlayer != null && teamVideoImage != null && teamVideoTexture != null)
            return;

        GameObject videoObject = new GameObject(
            "team_loading_video",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage),
            typeof(VideoPlayer));
        videoObject.layer = gameObject.layer;
        RectTransform rect = videoObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = videoSize;
        rect.anchoredPosition = videoAnchoredPosition;
        rect.SetAsFirstSibling();

        teamVideoImage = videoObject.GetComponent<RawImage>();
        teamVideoImage.raycastTarget = false;
        teamVideoImage.color = Color.white;

        teamVideoTexture = new RenderTexture(1024, 512, 0, RenderTextureFormat.ARGB32)
        {
            name = "PanelRodadaTeamVideoRT",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        teamVideoTexture.Create();
        teamVideoImage.texture = teamVideoTexture;

        teamVideoPlayer = videoObject.GetComponent<VideoPlayer>();
        teamVideoPlayer.playOnAwake = false;
        teamVideoPlayer.isLooping = true;
        teamVideoPlayer.waitForFirstFrame = true;
        teamVideoPlayer.skipOnDrop = true;
        teamVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
        teamVideoPlayer.targetTexture = teamVideoTexture;
        teamVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        teamVideoPlayer.sendFrameReadyEvents = true;
        teamVideoPlayer.frameReady += HandleTeamVideoFrameReady;
        videoObject.SetActive(false);
    }

    private void StartTeamVideo(TeamId team)
    {
        VideoClip clip = ResolveTeamVideo(team);
        if (clip == null)
        {
            StopTeamVideo();
            return;
        }

        EnsureTeamVideoPlayer();
        if (teamVideoPlayer == null || teamVideoImage == null)
            return;
        if (playingVideoTeam == team && teamVideoPlayer.clip == clip && teamVideoPlayer.isPlaying)
            return;

        teamVideoPlayer.Stop();
        teamVideoPlayer.clip = clip;
        teamVideoPlayer.isLooping = true;
        teamVideoImage.gameObject.SetActive(true);
        teamVideoImage.enabled = false;
        ClearTeamVideoTexture();
        teamVideoPlayer.Play();
        playingVideoTeam = team;
    }

    private void StopTeamVideo()
    {
        if (teamVideoPlayer != null)
        {
            teamVideoPlayer.Stop();
            teamVideoPlayer.clip = null;
        }
        if (teamVideoImage != null)
        {
            teamVideoImage.enabled = false;
            teamVideoImage.gameObject.SetActive(false);
        }
        ClearTeamVideoTexture();
        playingVideoTeam = TeamId.Neutral;
    }

    private void HandleTeamVideoFrameReady(VideoPlayer source, long frameIndex)
    {
        if (source != teamVideoPlayer || teamVideoImage == null || source.clip == null)
            return;
        teamVideoImage.enabled = true;
    }

    private void ClearTeamVideoTexture()
    {
        if (teamVideoTexture == null || !teamVideoTexture.IsCreated())
            return;
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = teamVideoTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = previous;
    }

    private VideoClip ResolveTeamVideo(TeamId team)
    {
        switch (team)
        {
            case TeamId.Green: return videoVerde;
            case TeamId.Red: return videoVermelho;
            case TeamId.Blue: return videoAzul;
            case TeamId.Yellow: return videoAmarelo;
            default: return null;
        }
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
