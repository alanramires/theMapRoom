using UnityEngine;

[System.Serializable]
public class ConstructionFieldEntry
{
    [Tooltip("ID da entrada no mapa (opcional, para organizacao).")]
    public string id;

    [Tooltip("Tipo de construcao desta entrada.")]
    public ConstructionData construction;

    [Tooltip("Time inicial desta construcao em campo.")]
    public TeamId initialTeamId = TeamId.Neutral;

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
