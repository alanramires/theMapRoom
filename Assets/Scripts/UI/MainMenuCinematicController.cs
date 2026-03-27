using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MainMenuCinematicController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup panelMenuCanvasGroup;
    [SerializeField] private GameObject panelMenuRoot;
    [SerializeField] private PanelMenu panelMenu;
    [SerializeField] private MainMenuStateController stateController;
    [SerializeField] private MatchMusicAudioManager musicAudioManager;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource cinematicAudioSource;
    [SerializeField] private Button cinematicButton;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RawImage cinematicOverlay;

    [Header("Cinematic")]
    [SerializeField] private VideoClip cinematicClip;
    [SerializeField] [Range(0f, 1f)] private float cinematicAudioVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float gameMusicOverrideVolumeWhileCinematic = 0.1f;
    [SerializeField] private bool restoreMusicVolumeAfterCinematic = true;

    private bool isPlaying;
    private float previousMusicVolume = 1f;
    private RenderTexture cinematicRenderTexture;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        ResolveReferences();
        EnsureVideoPlayerSetup();
    }

    private void OnEnable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached += HandleVideoLoopPointReached;

        SetCinematicOverlayVisible(false);
    }

    private void OnDisable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= HandleVideoLoopPointReached;
    }

    private void Update()
    {
        if (stateController != null)
            return;

        if (!isPlaying)
            return;

        if (WasCancelPressed())
            StopCinematicAndCleanup(playCancelSfx: true);
    }

    [ContextMenu("Stop Cinematic Now")]
    public void StopCinematicNow()
    {
        StopCinematicAndCleanup(playCancelSfx: true);
    }

    public void PlayCinematicFromMenu()
    {
        ResolveReferences();

        if (stateController != null)
        {
            stateController.RequestState(MainMenuState.Cinematic);
            return;
        }

        EnterCinematic();
    }

    public void EnterCinematic()
    {
        if (isPlaying)
            return;

        EnsureVideoPlayerSetup();

        if (videoPlayer == null)
        {
            Debug.LogWarning("[MainMenuCinematic] VideoPlayer nao encontrado.");
            return;
        }

        if (cinematicClip == null && videoPlayer.clip == null)
        {
            Debug.LogWarning("[MainMenuCinematic] Nenhum VideoClip configurado para cinematic.");
            return;
        }

        if (musicAudioManager != null)
        {
            previousMusicVolume = musicAudioManager.GetMasterMusicVolume();
            musicAudioManager.SetMasterMusicVolume(Mathf.Clamp01(gameMusicOverrideVolumeWhileCinematic));
        }

        if (cinematicClip != null)
            videoPlayer.clip = cinematicClip;

        SetMenuVisible(false);
        SetCinematicOverlayVisible(true);

        isPlaying = true;
        videoPlayer.Stop();
        videoPlayer.Play();
    }

    public void ExitToNeutral()
    {
        StopCinematicAndCleanup(playCancelSfx: false);
    }

    public void StopCinematicAndCleanup(bool playCancelSfx)
    {
        if (isPlaying)
        {
            isPlaying = false;

            if (videoPlayer != null)
                videoPlayer.Stop();

            if (musicAudioManager != null && restoreMusicVolumeAfterCinematic)
                musicAudioManager.SetMasterMusicVolume(previousMusicVolume);
        }

        SetCinematicOverlayVisible(false);
        SetMenuVisible(false);

        if (playCancelSfx)
            cursorController?.PlayCancelSfx();
    }

    private void HandleVideoLoopPointReached(VideoPlayer source)
    {
        isPlaying = false;
        SetCinematicOverlayVisible(false);
        SetMenuVisible(false);

        if (musicAudioManager != null && restoreMusicVolumeAfterCinematic)
            musicAudioManager.SetMasterMusicVolume(previousMusicVolume);
    }

    private void SetMenuVisible(bool visible)
    {
        if (panelMenuCanvasGroup != null)
        {
            panelMenuCanvasGroup.alpha = visible ? 1f : 0f;
            panelMenuCanvasGroup.interactable = visible;
            panelMenuCanvasGroup.blocksRaycasts = visible;
            return;
        }

        if (panelMenuRoot != null)
            panelMenuRoot.SetActive(visible);
    }

    private static bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.backspaceKey.wasPressedThisFrame)
                return true;
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame)
                return true;
        }
#endif
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            return true;

        // Fallback para UI modules que expõem "Cancel" via sistema de input clássico.
        if (Input.GetButtonDown("Cancel"))
            return true;

        // Ultimo fallback: botão direito do mouse também cancela durante cinematic.
        if (Input.GetMouseButtonDown(1))
            return true;

        return false;
    }

    private void ResolveReferences()
    {
        if (panelMenuCanvasGroup == null)
        {
            panelMenuCanvasGroup = GetComponent<CanvasGroup>();
            if (panelMenuCanvasGroup == null && transform.parent != null)
                panelMenuCanvasGroup = transform.parent.GetComponent<CanvasGroup>();
            if (panelMenuCanvasGroup == null)
            {
                CanvasGroup[] groups = FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < groups.Length; i++)
                {
                    CanvasGroup cg = groups[i];
                    if (cg == null || cg.gameObject == null || cg.gameObject.name == null)
                        continue;
                    if (cg.gameObject.name.Equals("Panel_Menu", System.StringComparison.OrdinalIgnoreCase))
                    {
                        panelMenuCanvasGroup = cg;
                        break;
                    }
                }
            }
        }

        if (panelMenuRoot == null)
        {
            if (panelMenuCanvasGroup != null)
                panelMenuRoot = panelMenuCanvasGroup.gameObject;
            else if (cinematicButton != null && cinematicButton.transform.parent != null)
                panelMenuRoot = cinematicButton.transform.parent.gameObject;
            else
            {
                Transform found = FindTransformByName("Panel_Menu");
                if (found != null)
                    panelMenuRoot = found.gameObject;
            }
        }

        if (panelMenu == null)
            panelMenu = GetComponent<PanelMenu>();
        if (panelMenu == null)
            panelMenu = FindAnyObjectByType<PanelMenu>();
        if (stateController == null)
            stateController = MainMenuStateController.EnsureSceneInstance();

        if (musicAudioManager == null)
            musicAudioManager = FindAnyObjectByType<MatchMusicAudioManager>();

        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();

        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer == null)
            videoPlayer = FindAnyObjectByType<VideoPlayer>();

        if (videoPlayer == null)
            videoPlayer = gameObject.AddComponent<VideoPlayer>();

        if (cinematicAudioSource == null)
            cinematicAudioSource = GetComponent<AudioSource>();

        if (cinematicAudioSource == null)
            cinematicAudioSource = gameObject.AddComponent<AudioSource>();

        if (targetCanvas == null)
        {
            if (panelMenuRoot != null)
                targetCanvas = panelMenuRoot.GetComponentInParent<Canvas>();
            if (targetCanvas == null)
                targetCanvas = FindAnyObjectByType<Canvas>();
        }

        EnsureOverlayObject();
    }

    private void EnsureVideoPlayerSetup()
    {
        if (videoPlayer == null)
            return;

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        EnsureRenderTexture();
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = cinematicRenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, cinematicAudioSource);
        if (cinematicAudioSource != null)
            cinematicAudioSource.volume = Mathf.Clamp01(cinematicAudioVolume);
    }

    private void OnValidate()
    {
        cinematicAudioVolume = Mathf.Clamp01(cinematicAudioVolume);
        gameMusicOverrideVolumeWhileCinematic = Mathf.Clamp01(gameMusicOverrideVolumeWhileCinematic);
        if (cinematicAudioSource != null)
            cinematicAudioSource.volume = cinematicAudioVolume;
    }

    private void EnsureOverlayObject()
    {
        if (targetCanvas == null)
            return;

        if (cinematicOverlay == null)
        {
            Transform existing = targetCanvas.transform.Find("CinematicOverlay");
            if (existing != null)
                cinematicOverlay = existing.GetComponent<RawImage>();
        }

        if (cinematicOverlay == null)
        {
            GameObject go = new GameObject("CinematicOverlay", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(targetCanvas.transform, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            cinematicOverlay = go.GetComponent<RawImage>();
            cinematicOverlay.color = Color.white;
            cinematicOverlay.raycastTarget = false;
        }

        EnsureRenderTexture();
        cinematicOverlay.texture = cinematicRenderTexture;
    }

    private void EnsureRenderTexture()
    {
        int width = Mathf.Max(2, Screen.width);
        int height = Mathf.Max(2, Screen.height);

        if (cinematicRenderTexture != null && cinematicRenderTexture.width == width && cinematicRenderTexture.height == height)
            return;

        if (cinematicRenderTexture != null)
            cinematicRenderTexture.Release();

        cinematicRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        cinematicRenderTexture.name = "MainMenuCinematicRT";
        cinematicRenderTexture.Create();
    }

    private void SetCinematicOverlayVisible(bool visible)
    {
        if (cinematicOverlay != null && cinematicOverlay.gameObject.activeSelf != visible)
            cinematicOverlay.gameObject.SetActive(visible);
    }

    private static Transform FindTransformByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform match = FindInChildrenRecursive(roots[i].transform, objectName);
            if (match != null)
                return match;
        }

        return null;
    }

    private static Transform FindInChildrenRecursive(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindInChildrenRecursive(root.GetChild(i), objectName);
            if (match != null)
                return match;
        }

        return null;
    }
}
