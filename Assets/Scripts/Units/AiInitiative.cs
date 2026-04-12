/// <summary>
/// Ordem de iniciativa da unidade na fila de ação da IA.
/// A IA ordena as unidades disponíveis por esta prioridade (crescente),
/// desempatando por HP descrescente — unidades mais saudáveis agem antes.
/// </summary>
public enum AiInitiative
{
    Priority = 0,
    High     = 1,
    Medium   = 2,
    Low      = 3,
    Retreat  = 4,
}
