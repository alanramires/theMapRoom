using System.Collections.Generic;
using UnityEngine;
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
        ByTeam = 1
    }

    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private AudioSource audioSource;

    [Header("Playback")]
    [SerializeField] private MusicPlaybackMode playbackMode = MusicPlaybackMode.Free;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.7f;
    [SerializeField] private bool shuffleFreeMode = false;

    [Header("Free Mode Playlist")]
    [SerializeField] private List<AudioClip> freeModePlaylist = new List<AudioClip>();

    [Header("Team Tracks")]
    [SerializeField] private AudioClip team0Track;
    [SerializeField] private AudioClip team1Track;
    [SerializeField] private AudioClip team2Track;
    [SerializeField] private AudioClip team3Track;

    private int currentFreeIndex = -1;
    private int observedTeamId = int.MinValue;
    private bool isPausedByUser;
    private bool pausedByTurnTransition;
    private bool suppressPlaybackForTurnTransition;
    public bool IsPausedByUser => isPausedByUser;
    public bool IsPlaying => audioSource != null && audioSource.isPlaying;
    public bool IsFreeMode => playbackMode == MusicPlaybackMode.Free;

    private void Awake()
    {
        EnsureReferences();
        EnsureFreePlaylistFallback();
        ApplyAudioSourceDefaults();
    }

    private void Start()
    {
        if (playOnStart)
            StartPlaybackForCurrentMode(forceRestart: true);
    }

    private void Update()
    {
        HandleToggleShortcut();
        if (isPausedByUser || suppressPlaybackForTurnTransition)
            return;

        EnsurePlayback();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureReferences();
        musicVolume = Mathf.Clamp01(musicVolume);
        TryAutoAssignMusicClipsInEditor();
        EnsureFreePlaylistFallback();
        ApplyAudioSourceDefaults();
    }
#endif

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

    private void EnsurePlayback()
    {
        if (audioSource == null)
            return;

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

    private void PlayTeamTrack(int teamId, bool forceRestart)
    {
        if (audioSource == null)
            return;

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
        audioSource.volume = musicVolume;
        audioSource.Play();
    }

    private AudioClip GetTeamClip(int teamId)
    {
        switch (teamId)
        {
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
        if (!WasToggleKeyPressedThisFrame())
            return;

        TogglePlayPause();
    }

    private bool WasToggleKeyPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.P);
#endif
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

    private void ApplyAudioSourceDefaults()
    {
        if (audioSource == null)
            return;

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = musicVolume;
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
            if (name == "team0")
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
