using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Bloqueio global de input de gameplay enquanto um campo de texto tem foco.
// Nao pertence ao DebugManager nem ao PanelDebugController: e consumido por
// cursor, camera, menus, save/load e replay.
public static class UiInputBlocker
{
    private static int suppressUntilFrame = -1;
    private static bool explicitTextInputFocused;

    public static void SuppressGameplayInputForFrames(int frames)
    {
        int safeFrames = Mathf.Max(1, frames);
        suppressUntilFrame = Mathf.Max(suppressUntilFrame, Time.frameCount + safeFrames);
    }

    public static void SetExplicitTextInputFocused(bool focused)
    {
        explicitTextInputFocused = focused;
    }

    public static bool IsTextInputFocused()
    {
        if (explicitTextInputFocused)
            return true;

        if (Time.frameCount <= suppressUntilFrame)
            return true;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected == null)
            return false;

        InputField legacyInput = selected.GetComponentInParent<InputField>();
        if (legacyInput != null && legacyInput.isFocused)
            return true;

        TMP_InputField tmpInput = selected.GetComponentInParent<TMP_InputField>();
        if (tmpInput != null && tmpInput.isFocused)
            return true;

        // Fallback robusto: alguns fluxos nao mantem o selected GameObject no input.
        TMP_InputField[] tmpInputs = Object.FindObjectsByType<TMP_InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < tmpInputs.Length; i++)
        {
            TMP_InputField field = tmpInputs[i];
            if (field != null && field.isActiveAndEnabled && field.isFocused)
                return true;
        }

        InputField[] legacyInputs = Object.FindObjectsByType<InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < legacyInputs.Length; i++)
        {
            InputField field = legacyInputs[i];
            if (field != null && field.isActiveAndEnabled && field.isFocused)
                return true;
        }

        return false;
    }
}
