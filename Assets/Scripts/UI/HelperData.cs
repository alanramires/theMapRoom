using UnityEngine;

[CreateAssetMenu(menuName = "Game/UI/Helper Data", fileName = "Helper Data_")]
public class HelperData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico da mensagem (ex.: helper.title.shopping).")]
    public string id;

    [TextArea]
    [Tooltip("Descricao da condicao em que a mensagem aparece.")]
    public string condition;

    [TextArea]
    [Tooltip(
        "Template da mensagem.\n" +
        "Tokens comuns: <action>, <label>, <index>, <unit>, <valor>, <stats>, <terrain>.\n\n" +
        "Uso por ID:\n" +
        "- helper.shopping.line.with_cost -> <index>, <unit>, <valor>\n" +
        "- helper.shopping.line.no_cost -> <index>, <unit>\n" +
        "- helper.sensors.line.format -> <action>, <label>\n" +
        "- helper.sensors.line.move_only -> <action>, <label>\n" +
        "- helper.disembark.order.line -> <index>, <unit>, <stats>, <terrain>\n" +
        "- helper.disembark.passenger.line -> <index>, <unit>, <stats>\n" +
        "- helper.merge.queue.line -> <index>, <unit>, <stats>\n" +
        "- helper.merge.candidate.line -> <index>, <unit>, <stats>\n" +
        "- helper.merge.confirm.line -> <index>, <unit>, <stats>\n" +
        "- helper.merge.separator -> sem token\n" +
        "- helper.merge.confirm.preview -> <preview>\n" +
        "- helper.merge.process_order.line -> <preview>\n" +
        "- demais IDs de titulo/label -> sem token")]
    public string message;
}

