using UnityEngine;

[System.Serializable]
public class ConstructionFieldEntry
{
    [Tooltip("ID da entrada no mapa (opcional, para organizacao).")]
    public string id;

    [Tooltip("Tipo de construcao desta entrada.")]
    public ConstructionData construction;

    [Tooltip("Slot do MatchController que controla esta construcao no inicio. -1 = Neutral.")]
    public int initialSlotIndex = -1;

    [Tooltip("Setor estrategico ao qual esta construcao pertence. Use Base1-Base4 para areas de base de cada jogador.")]
    public ConstructionSector sector = ConstructionSector.Alpha;

    [Tooltip("Posicao da construcao no mapa (hex).")]
    public Vector3Int cellPosition = Vector3Int.zero;

    [Header("Instance State")]
    [Tooltip("Pontos de captura iniciais desta instancia. -1 usa o maximo da configuracao.")]
    public int initialCapturePoints = -1;

    [Header("Construction Configuration Override")]
    [Tooltip("Se true, sobrescreve a configuracao padrao da construcao para esta entrada.")]
    public bool useConstructionConfigurationOverride = false;
    public ConstructionSiteRuntime constructionConfiguration = new ConstructionSiteRuntime();
}

