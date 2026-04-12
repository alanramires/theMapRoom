/// <summary>
/// Ações que a IA sabe executar, em ordem de prioridade configurável por unidade.
/// Cresce junto com as capacidades da IA — apenas adicione aqui quando o orquestrador
/// souber executar a ação correspondente.
/// </summary>
public enum AISensorPriority
{
    Attack,
    Capture,
    Reposition,
}
