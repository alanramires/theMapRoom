using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MatchMusicAudioManager : MonoBehaviour
{
    public enum MusicPlaybackMode
    {
        Free = 0,
        ByTeam = 1,
        Loop = 2
    }

    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private AudioSource audioSource;

    [Header("Playback")]
    [SerializeField] private MusicPlaybackMode playbackMode = MusicPlaybackMode.Free;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.7f;
    [SerializeField] [Range(0f, 1f)] private float roundMusicVolume = 1f;
    [SerializeField] private bool shuffleFreeMode = false;
    [Header("Per-Team Volume")]
    [SerializeField] [Range(0f, 2f)] private float neutralMusicVolume = 1f;
    [SerializeField] [Range(0f, 2f)] private float team0MusicVolume = 1f;
    [SerializeField] [Range(0f, 2f)] private float team1MusicVolume = 1f;
    [SerializeField] [Range(0f, 2f)] private float team2MusicVolume = 1f;
    [SerializeField] [Range(0f, 2f)] private float team3MusicVolume = 1f;

    [Header("Free Mode Playlist")]
    [SerializeField] private List<AudioClip> freeModePlaylist = new List<AudioClip>();

    [Header("Team Tracks")]
    [SerializeField] private AudioClip neutralTrack;
    [SerializeField] private AudioClip team0Track;
    [SerializeField] private AudioClip team1Track;
    [SerializeField] private AudioClip team2Track;
    [SerializeField] private AudioClip team3Track;
    [Header("Game Tracks")]
    [SerializeField] private AudioClip gameOpenTrack;
    [SerializeField] [Range(0f, 2f)] private float gameOpenMusicVolume = 1f;
    [SerializeField] private bool playGameOpenOnStart = true;
    [SerializeField] private bool playGameOpenOnlyInSpecificScene = true;
    [SerializeField] private string gameOpenSceneName = "Tela de Entrada";
    [Header("Transitions")]
    [SerializeField] [Range(0f, 1f)] private float menuLoadTransitionMusicVolume = 0.1f;
    [Header("Preview")]
    [SerializeField] [Range(-1, 3)] private int previewTeamId = 0;
    [SerializeField] private bool previewLoop = true;

    private int currentFreeIndex = -1;
    private int observedTeamId = int.MinValue;
    private bool isPausedByUser;
    private bool pausedByTurnTransition;
    private bool suppressPlaybackForTurnTransition;
    private bool hasRuntimeVolumeOverride;
    private float runtimeVolumeOverride = 1f;
    private Coroutine fadeOutRoutine;
    public bool IsPausedByUser => isPausedByUser;
    public bool IsPlaying => audioSource != null && audioSource.isPlaying;
    public bool IsFreeMode => playbackMode == MusicPlaybackMode.Free;
    public AudioClip GameOpenTrack => gameOpenTrack;
    public float MenuLoadTransitionMusicVolume => Mathf.Clamp01(menuLoadTransitionMusicVolume);

    private void Awake()
    {
        EnsureReferences();
        EnsurePerTeamVolumeLegacyFallback();
        ClampPerTeamVolumes();
        EnsureFreePlaylistFallback();
        ApplyAudioSourceDefaults();
    }

    private void Start()
    {
        if (ShouldSuppressPlaybackForPrivacyGate())
        {
            if (audioSource != null) audioSource.Stop();
            return;
        }

        if (TryPlayGameOpenTrackForMenuScene())
            return;

        if (playOnStart)
        {
            StartPlaybackForCurrentMode(forceRestart: true);
            return;
        }

        // Respeita "Play On Start" desativado: nao iniciar playback automatico no primeiro frame.
        isPausedByUser = true;
        if (audioSource != null)
            audioSource.Stop();
    }

    private void Update()
    {
        HandleToggleShortcut();
        if (ShouldSuppressPlaybackForPrivacyGate())
        {
            if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
            return;
        }
        if (isPausedByUser || suppressPlaybackForTurnTransition)
            return;

        EnsurePlayback();
    }

    private bool ShouldSuppressPlaybackForPrivacyGate()
    {
        return (matchController != null && matchController.IsHotSeatGateActive) ||
               SaveGameManager.HasPendingMainMenuLoadRequest ||
               SaveGameManager.IsAnyLoadInProgress;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureReferences();
        EnsurePerTeamVolumeLegacyFallback();
        musicVolume = Mathf.Clamp01(musicVolume);
        roundMusicVolume = Mathf.Clamp01(roundMusicVolume);
        ClampPerTeamVolumes();
        TryAutoAssignMusicClipsInEditor();
        EnsureFreePlaylistFallback();
        ApplyAudioSourceDefaults();
    }
#endif

    public void SetMasterMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        RefreshOutputVolume();
    }

    public float GetMasterMusicVolume()
    {
        return musicVolume;
    }

    public float GetRoundMusicVolume()
    {
        return roundMusicVolume;
    }

    public void SetTeamMusicVolume(int teamId, float volume)
    {
        float clamped = Mathf.Clamp(volume, 0f, 2f);
        switch (teamId)
        {
            case -1: neutralMusicVolume = clamped; break;
            case 0: team0MusicVolume = clamped; break;
            case 1: team1MusicVolume = clamped; break;
            case 2: team2MusicVolume = clamped; break;
            case 3: team3MusicVolume = clamped; break;
            default: return;
        }

        RefreshOutputVolume();
    }

    public float GetTeamMusicVolume(int teamId)
    {
        return ResolveTeamVolumeMultiplier(teamId);
    }

    public void SetPlaybackMode(MusicPlaybackMode mode)
    {
        if (playbackMode == mode)
            return;

        playbackMode = mode;
        StartPlaybackForCurrentMode(forceRestart: true);
    }

    public void TogglePlayPause()
    {
        if (audioSource == null)
            return;

        if (audioSource.isPlaying)
        {
            audioSource.Pause();
            isPausedByUser = true;
            return;
        }

        if (isPausedByUser && audioSource.clip != null)
        {
            audioSource.UnPause();
            isPausedByUser = false;
            return;
        }

        isPausedByUser = false;
        StartPlaybackForCurrentMode(forceRestart: audioSource.clip == null);
    }

    public void StopForTurnTransition()
    {
        if (audioSource == null || !audioSource.isPlaying)
            return;

        audioSource.Stop();
    }

    public void StopPlaybackPermanently()
    {
        if (fadeOutRoutine != null)
        {
            StopCoroutine(fadeOutRoutine);
            fadeOutRoutine = null;
        }

        isPausedByUser = true;
        if (audioSource != null)
            audioSource.Stop();
    }

    public void SetRuntimeVolumeOverride(float volumeScale)
    {
        hasRuntimeVolumeOverride = true;
        runtimeVolumeOverride = Mathf.Clamp(volumeScale, 0f, 1f);
        RefreshOutputVolume();
    }

    public void ClearRuntimeVolumeOverride()
    {
        hasRuntimeVolumeOverride = false;
        runtimeVolumeOverride = 1f;
        RefreshOutputVolume();
    }

    public void PrepareForMatchStart(bool forceRestartPlayback = true)
    {
        EnsureReferences();

        if (fadeOutRoutine != null)
        {
            StopCoroutine(fadeOutRoutine);
            fadeOutRoutine = null;
        }

        ClearRuntimeVolumeOverride();
        pausedByTurnTransition = false;
        suppressPlaybackForTurnTransition = false;
        isPausedByUser = false;

        if (audioSource == null)
            return;

        if (audioSource.isPlaying)
            audioSource.Stop();

        if (forceRestartPlayback || playOnStart)
            StartPlaybackForCurrentMode(forceRestart: true);
    }

    public IEnumerator FadeOutAndStop(float durationSeconds)
    {
        EnsureReferences();
        if (audioSource == null)
            yield break;

        if (fadeOutRoutine != null)
        {
            StopCoroutine(fadeOutRoutine);
            fadeOutRoutine = null;
        }

        if (!audioSource.isPlaying || durationSeconds <= 0f)
        {
            StopPlaybackPermanently();
            RefreshOutputVolume();
            yield break;
        }

        fadeOutRoutine = StartCoroutine(FadeOutAndStopRoutine(Mathf.Max(0.01f, durationSeconds)));
        yield return fadeOutRoutine;
    }

    public void BeginTurnTransition()
    {
        suppressPlaybackForTurnTransition = true;
    }

    public void EndTurnTransition()
    {
        suppressPlaybackForTurnTransition = false;
    }

    public void PauseForTurnTransition()
    {
        if (audioSource == null || !audioSource.isPlaying)
            return;

        audioSource.Pause();
        pausedByTurnTransition = true;
    }

    public void ResumeAfterTurnTransition()
    {
        if (audioSource == null || !pausedByTurnTransition)
            return;

        audioSource.UnPause();
        pausedByTurnTransition = false;
        suppressPlaybackForTurnTransition = false;
    }

    public void RestartCurrentModePlayback()
    {
        isPausedByUser = false;
        pausedByTurnTransition = false;
        suppressPlaybackForTurnTransition = false;
        StartPlaybackForCurrentMode(forceRestart: true);
    }

    [ContextMenu("Music Preview/Play Configured Team")]
    public void PlayPreviewConfiguredTeam()
    {
        PlayPreviewForTeam(previewTeamId, previewLoop);
    }

    [ContextMenu("Music Preview/Play Neutral")]
    public void PlayPreviewNeutral() => PlayPreviewForTeam(-1, previewLoop);

    [ContextMenu("Music Preview/Play Team 0")]
    public void PlayPreviewTeam0() => PlayPreviewForTeam(0, previewLoop);

    [ContextMenu("Music Preview/Play Team 1")]
    public void PlayPreviewTeam1() => PlayPreviewForTeam(1, previewLoop);

    [ContextMenu("Music Preview/Play Team 2")]
    public void PlayPreviewTeam2() => PlayPreviewForTeam(2, previewLoop);

    [ContextMenu("Music Preview/Play Team 3")]
    public void PlayPreviewTeam3() => PlayPreviewForTeam(3, previewLoop);

    [ContextMenu("Music Preview/Play Game Open")]
    public void PlayPreviewGameOpen()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Music] Preview funciona em Play Mode.");
            return;
        }

        if (audioSource == null || gameOpenTrack == null)
            return;

        isPausedByUser = false;
        pausedByTurnTransition = false;
        suppressPlaybackForTurnTransition = false;
        PlayClip(gameOpenTrack, previewLoop, forceRestart: true);
    }

    [ContextMenu("Music Preview/Stop")]
    public void StopPreview()
    {
        if (audioSource == null)
            return;

        audioSource.Stop();
    }

    public void PlayPreviewForTeam(int teamId, bool loop = true)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Music] Preview funciona em Play Mode.");
            return;
        }

        EnsureReferences();
        if (audioSource == null)
            return;

        AudioClip clip = GetTeamClip(teamId);
        if (clip == null)
        {
            Debug.LogWarning($"[Music] Sem faixa para o team {teamId}.");
            return;
        }

        observedTeamId = teamId;
        isPausedByUser = false;
        pausedByTurnTransition = false;
        suppressPlaybackForTurnTransition = false;
        audioSource.clip = clip;
        audioSource.loop = loop;
        RefreshOutputVolume();
        audioSource.Play();
    }

    public bool PlayGameOpenTrack(bool loop = true, bool forceRestart = true)
    {
        EnsureReferences();
        if (audioSource == null || gameOpenTrack == null)
            return false;

        isPausedByUser = false;
        pausedByTurnTransition = false;
        suppressPlaybackForTurnTransition = false;
        PlayClip(gameOpenTrack, loop, forceRestart);
        return true;
    }

    private void EnsurePlayback()
    {
        if (audioSource == null)
            return;

        if (playbackMode == MusicPlaybackMode.Loop)
        {
            if (!audioSource.isPlaying)
                PlayLoopTrack(forceRestart: false);
            return;
        }

        if (playbackMode == MusicPlaybackMode.ByTeam)
        {
            int activeTeam = matchController != null ? matchController.ActiveTeamId : -1;
            if (observedTeamId != activeTeam)
            {
                observedTeamId = activeTeam;
                PlayTeamTrack(activeTeam, forceRestart: true);
                return;
            }

            if (!audioSource.isPlaying)
                PlayTeamTrack(activeTeam, forceRestart: false);
            return;
        }

        if (!audioSource.isPlaying)
            PlayNextFreeTrack();
    }

    private void StartPlaybackForCurrentMode(bool forceRestart)
    {
        if (audioSource == null)
            return;

        if (playbackMode == MusicPlaybackMode.Loop)
        {
            PlayLoopTrack(forceRestart);
            return;
        }

        if (playbackMode == MusicPlaybackMode.ByTeam)
        {
            observedTeamId = matchController != null ? matchController.ActiveTeamId : -1;
            PlayTeamTrack(observedTeamId, forceRestart);
            return;
        }

        if (forceRestart)
            currentFreeIndex = -1;

        PlayNextFreeTrack();
    }

    private void PlayNextFreeTrack()
    {
        if (audioSource == null)
            return;

        List<AudioClip> valid = GetValidFreePlaylist();
        if (valid.Count == 0)
        {
            audioSource.Stop();
            audioSource.clip = null;
            return;
        }

        if (shuffleFreeMode)
        {
            int index = Random.Range(0, valid.Count);
            currentFreeIndex = index;
            PlayClip(valid[index], loop: false, forceRestart: true);
            return;
        }

        currentFreeIndex = (currentFreeIndex + 1 + valid.Count) % valid.Count;
        PlayClip(valid[currentFreeIndex], loop: false, forceRestart: true);
    }

    private void PlayLoopTrack(bool forceRestart)
    {
        if (audioSource == null)
            return;

        AudioClip clip = ResolveLoopClipCandidate();
        if (clip == null)
        {
            audioSource.Stop();
            audioSource.clip = null;
            return;
        }

        PlayClip(clip, loop: true, forceRestart: forceRestart);
    }

    private AudioClip ResolveLoopClipCandidate()
    {
        if (audioSource != null && audioSource.clip != null)
            return audioSource.clip;
        if (gameOpenTrack != null)
            return gameOpenTrack;

        List<AudioClip> valid = GetValidFreePlaylist();
        if (valid.Count > 0)
            return valid[0];

        return team0Track;
    }

    private void PlayTeamTrack(int teamId, bool forceRestart)
    {
        if (audioSource == null)
            return;

        observedTeamId = teamId;
        AudioClip clip = GetTeamClip(teamId);
        if (clip == null)
        {
            audioSource.Stop();
            audioSource.clip = null;
            return;
        }

        PlayClip(clip, loop: true, forceRestart: forceRestart);
    }

    private void PlayClip(AudioClip clip, bool loop, bool forceRestart)
    {
        if (audioSource == null)
            return;
        if (clip == null)
            return;

        bool sameClip = audioSource.clip == clip;
        if (audioSource.isPlaying && sameClip && !forceRestart && audioSource.loop == loop)
            return;

        audioSource.clip = clip;
        audioSource.loop = loop;
        RefreshOutputVolume();
        audioSource.Play();
    }

    private IEnumerator FadeOutAndStopRoutine(float durationSeconds)
    {
        float startVolume = audioSource != null ? audioSource.volume : 0f;
        float elapsed = 0f;

        while (audioSource != null && audioSource.isPlaying && elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            audioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        if (audioSource != null)
            audioSource.Stop();

        isPausedByUser = true;
        RefreshOutputVolume();
        fadeOutRoutine = null;
    }

    private AudioClip GetTeamClip(int teamId)
    {
        switch (teamId)
        {
            case -1:
                if (neutralTrack != null)
                    return neutralTrack;
                if (team0Track != null)
                    return team0Track;

                List<AudioClip> valid = GetValidFreePlaylist();
                return valid.Count > 0 ? valid[0] : null;
            case 0: return team0Track;
            case 1: return team1Track;
            case 2: return team2Track;
            case 3: return team3Track;
            default: return null;
        }
    }

    private List<AudioClip> GetValidFreePlaylist()
    {
        List<AudioClip> valid = new List<AudioClip>();
        for (int i = 0; i < freeModePlaylist.Count; i++)
        {
            AudioClip clip = freeModePlaylist[i];
            if (clip != null)
                valid.Add(clip);
        }

        return valid;
    }

    private void HandleToggleShortcut()
    {
        if (UiInputBlocker.IsTextInputFocused())
            return;

        if (!WasToggleKeyPressedThisFrame())
            return;

        TogglePlayPause();
    }

    private bool WasToggleKeyPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    private bool TryPlayGameOpenTrackForMenuScene()
    {
        if (!playGameOpenOnStart)
            return false;

        if (!playGameOpenOnlyInSpecificScene)
            return PlayGameOpenTrack(loop: true, forceRestart: true);

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return false;

        string configuredName = string.IsNullOrWhiteSpace(gameOpenSceneName) ? string.Empty : gameOpenSceneName.Trim();
        if (configuredName.Length <= 0)
            return false;

        if (!string.Equals(activeScene.name, configuredName, System.StringComparison.OrdinalIgnoreCase))
            return false;

        return PlayGameOpenTrack(loop: true, forceRestart: true);
    }

    private void EnsureReferences()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void EnsurePerTeamVolumeLegacyFallback()
    {
        // Compatibilidade: cenas antigas podem desserializar todos os novos campos em 0.
        // So aplica fallback quando TODOS estao zerados.
        bool allZero =
            Mathf.Approximately(neutralMusicVolume, 0f) &&
            Mathf.Approximately(team0MusicVolume, 0f) &&
            Mathf.Approximately(team1MusicVolume, 0f) &&
            Mathf.Approximately(team2MusicVolume, 0f) &&
            Mathf.Approximately(team3MusicVolume, 0f);

        if (!allZero)
            return;

        neutralMusicVolume = 1f;
        team0MusicVolume = 1f;
        team1MusicVolume = 1f;
        team2MusicVolume = 1f;
        team3MusicVolume = 1f;
    }

    private void ClampPerTeamVolumes()
    {
        neutralMusicVolume = Mathf.Clamp(neutralMusicVolume, 0f, 2f);
        team0MusicVolume = Mathf.Clamp(team0MusicVolume, 0f, 2f);
        team1MusicVolume = Mathf.Clamp(team1MusicVolume, 0f, 2f);
        team2MusicVolume = Mathf.Clamp(team2MusicVolume, 0f, 2f);
        team3MusicVolume = Mathf.Clamp(team3MusicVolume, 0f, 2f);
        gameOpenMusicVolume = Mathf.Clamp(gameOpenMusicVolume, 0f, 2f);
    }

    private void ApplyAudioSourceDefaults()
    {
        if (audioSource == null)
            return;

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        RefreshOutputVolume();
    }

    private void RefreshOutputVolume()
    {
        if (audioSource == null)
            return;

        float teamMultiplier = ResolveCurrentClipVolumeMultiplier();
        float runtimeScale = hasRuntimeVolumeOverride ? runtimeVolumeOverride : 1f;
        audioSource.volume = Mathf.Clamp01(musicVolume * teamMultiplier * runtimeScale);
    }

    private float ResolveCurrentClipVolumeMultiplier()
    {
        if (playbackMode == MusicPlaybackMode.ByTeam)
            return ResolveTeamVolumeMultiplier(observedTeamId);

        AudioClip currentClip = audioSource != null ? audioSource.clip : null;
        if (currentClip == null)
            return 1f;

        if (neutralTrack != null && currentClip == neutralTrack)
            return neutralMusicVolume;
        if (team0Track != null && currentClip == team0Track)
            return team0MusicVolume;
        if (team1Track != null && currentClip == team1Track)
            return team1MusicVolume;
        if (team2Track != null && currentClip == team2Track)
            return team2MusicVolume;
        if (team3Track != null && currentClip == team3Track)
            return team3MusicVolume;
        if (gameOpenTrack != null && currentClip == gameOpenTrack)
            return gameOpenMusicVolume;

        return 1f;
    }

    private float ResolveTeamVolumeMultiplier(int teamId)
    {
        switch (teamId)
        {
            case -1: return neutralMusicVolume;
            case 0: return team0MusicVolume;
            case 1: return team1MusicVolume;
            case 2: return team2MusicVolume;
            case 3: return team3MusicVolume;
            default: return 1f;
        }
    }

    private void EnsureFreePlaylistFallback()
    {
        bool hasAny = false;
        for (int i = 0; i < freeModePlaylist.Count; i++)
        {
            if (freeModePlaylist[i] != null)
            {
                hasAny = true;
                break;
            }
        }

        if (hasAny)
            return;

        freeModePlaylist.Clear();
        AddIfNotNull(team0Track);
        AddIfNotNull(team1Track);
        AddIfNotNull(team2Track);
        AddIfNotNull(team3Track);
    }

    private void AddIfNotNull(AudioClip clip)
    {
        if (clip == null)
            return;
        if (freeModePlaylist.Contains(clip))
            return;
        freeModePlaylist.Add(clip);
    }

#if UNITY_EDITOR
    private void TryAutoAssignMusicClipsInEditor()
    {
        const string musicFolder = "Assets/audio/music";
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { musicFolder });
        if (guids == null || guids.Length == 0)
            return;

        List<AudioClip> discovered = new List<AudioClip>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                continue;

            discovered.Add(clip);
            string name = clip.name.ToLowerInvariant();
            if (name == "neutraltrack" || name == "neutral")
                neutralTrack = clip;
            else if (name == "gameopentrack" || name == "gameopen")
                gameOpenTrack = clip;
            else if (name == "team0")
                team0Track = clip;
            else if (name == "team1")
                team1Track = clip;
            else if (name == "team2")
                team2Track = clip;
            else if (name == "team3")
                team3Track = clip;
        }

        if (freeModePlaylist == null)
            freeModePlaylist = new List<AudioClip>();
        freeModePlaylist.Clear();
        for (int i = 0; i < discovered.Count; i++)
            AddIfNotNull(discovered[i]);
    }
#endif
}
