using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Deteccao de CONFIRMAR / CANCELAR vinda de controle remoto de TV (Fire TV / Android TV)
/// e de gamepads, para complementar teclado/mouse. Totalmente aditivo: sempre usado em OR
/// com as fontes existentes, nunca as substitui.
///
/// Motivo: no Fire TV o botao "Voltar" (Back) gera KEYCODE_BACK, que o Input LEGACY entrega
/// como KeyCode.Escape. Varios helpers do jogo, no ramo do Input System novo, retornavam a
/// checagem de teclado antes de cair no legacy — entao o Back nao era consumido e o Android
/// podia encerrar o app. O Select/OK do remote chega como DPAD center / botao A do gamepad.
/// </summary>
public static class RemoteInput
{
    // Select / OK do remote, botao A (South) do gamepad. O Enter/Return ja e tratado pelos
    // helpers existentes (o DPAD center costuma chegar como Return em muitos remotes).
    public static bool ConfirmDownThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.JoystickButton0))
            return true;
#endif
        return false;
    }

    // Clique DIREITO como ESC, para contextos SEM pan de camera (menus da Tela de Entrada).
    // NAO usar no gameplay: la o botao direito arrasta a camera e o CursorController distingue
    // tap de arrasto (WasRightClickCancelTapThisFrame). No menu nao ha arrasto, entao o down basta.
    public static bool RightClickCancelDownThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(1))
            return true;
#endif
        return false;
    }

    // Voltar (Back) do Fire TV = KEYCODE_BACK -> KeyCode.Escape (Input legacy). Backspace e o
    // botao B (East) do gamepad tambem cancelam.
    public static bool CancelDownThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            return true;
#endif
        return false;
    }
}
