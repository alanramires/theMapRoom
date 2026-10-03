using UnityEngine;

public enum PanelMessageLanguage
{
    PortugueseBrazil = 0,
    English = 1
}

/// <summary>
/// Shared language for Dialog Data and Helper Data. Select before entering Play;
/// texts already cached by menus or saves are not retroactively reformatted.
/// </summary>
[CreateAssetMenu(menuName = "Game/UI/Panel Message Language", fileName = "Panel Message Language")]
public sealed class PanelMessageLanguageSettings : ScriptableObject
{
    public const string ResourceName = "Panel Message Language";

    [Tooltip("Idioma dos dois paineis. Se uma mensagem nao tiver ingles, usa o portugues. Para testar, selecione antes de entrar em Play.")]
    public PanelMessageLanguage language = PanelMessageLanguage.PortugueseBrazil;

    private static PanelMessageLanguageSettings instance;

    public static PanelMessageLanguage CurrentLanguage
    {
        get
        {
            if (instance == null)
                instance = Resources.Load<PanelMessageLanguageSettings>(ResourceName);
            return instance != null ? instance.language : PanelMessageLanguage.PortugueseBrazil;
        }
    }
}
