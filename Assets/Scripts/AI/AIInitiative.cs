/// <summary>
/// Define a prioridade de acao da unidade dentro do turno da IA.
/// Unidades com Initiative mais alta agem antes das demais.
/// </summary>
public enum AIInitiative
{
    Priority = 0, // Age primeiro de todos - artilharia (define posicao antes dos demais moverem)
    High     = 1, // Age cedo - escoltas, combatentes, suporte ofensivo
    Medium   = 2, // Ordem padrao - unidades sem prioridade especial
    Low      = 3, // Age por ultimo - capturadores (apos escoltas limparem o terreno)
    Retreat  = 4, // Iniciativa temporaria: unidades em Return to Base / Repair agem depois de Low.
}
