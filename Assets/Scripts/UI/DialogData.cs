using UnityEngine;

[CreateAssetMenu(menuName = "Game/UI/Dialog Data", fileName = "Dialog Data_")]
public class DialogData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico da mensagem (ex.: panel_dialog.state.moving).")]
    public string id;

    [TextArea]
    [Tooltip("Descricao da condicao em que a mensagem aparece.")]
    public string condition;

    [TextArea]
    [Tooltip(
        "Template da mensagem.\n" +
        "Tokens disponiveis: <unit>, <state>, <sensor>.\n\n" +
        "Uso por ID:\n" +
        "- panel_dialog.state.moving -> <unit>, <state>\n" +
        "- panel_dialog.label.moving -> sem token\n" +
        "- panel_dialog.state.sensor -> <unit>, <sensor>\n" +
        "- panel_dialog.state.sensor_confirm -> <unit>, <sensor>\n" +
        "- panel_dialog.sensor.* -> sem token")]
    public string message;
}

