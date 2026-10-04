using UnityEngine;

[CreateAssetMenu(menuName = "Game/UI/UI Text Data", fileName = "UI Text Data")]
public sealed class UITextData : ScriptableObject
{
    public string id;
    [TextArea] public string condition;
    [TextArea(3, 10)] public string message;
    [TextArea(3, 10)] public string messageEnglish;

    public string LocalizedMessage => MessageTemplate.SelectLanguage(
        message, messageEnglish, PanelMessageLanguageSettings.CurrentLanguage == PanelMessageLanguage.English);
}
