/// <summary>
/// Define a prioridade de ação da unidade dentro do turno da IA.
/// Unidades com Initiative mais alta agem antes das demais.
/// </summary>
public enum AIInitiative
{
    Priority = 0, // Age primeiro de todos — artilharia (define posição antes dos demais moverem)
    High     = 1, // Age cedo — escoltas, combatentes, suporte ofensivo
    Medium   = 2, // Ordem padrão — unidades sem prioridade especial
    Low      = 3, // Age por último — capturadores (após escoltas limparem o terreno)
}
