using UnityEngine;
using UnityEngine.EventSystems;

// Fullscreen no WebGL precisa ser solicitado diretamente por um gesto do usuario.
// PointerDown e mais confiavel que Button.onClick/PointerClick, que chegam no release.
// Arquivo proprio (antes morava dentro de PanelMoneyController.cs) para aparecer no
// Add Component: a Unity so lista MonoBehaviour cujo arquivo tem o nome da classe.
public sealed class FullscreenShortcutButton : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        ToggleFullscreen();
        CursorController cursor = FindAnyObjectByType<CursorController>();
        cursor?.PlayConfirmSfx();
    }

    public void ToggleFullscreen() => SetFullscreen(!Screen.fullScreen);

    public static void SetFullscreen(bool enable)
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        Screen.fullScreenMode = enable
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;
#endif
        Screen.fullScreen = enable;
        PanelDialogController.TrySetTransientText(
            enable ? "Solicitando tela cheia..." : "Tela cheia: OFF",
            1.6f);
    }

    private void Awake()
    {
        UnityEngine.UI.Image image = GetComponent<UnityEngine.UI.Image>();
        if (image != null)
            image.raycastTarget = true;
    }
}
