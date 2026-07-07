using UnityEngine;

// Uma entrada de override manual de eixo, por SLOT. O mesmo setor pode participar de forma
// diferente no leque de cada slot (nó de um, rally/eixo distinto de outro), então o override é
// uma LISTA dessas entradas — uma por slot que o designer quer forçar.
[System.Serializable]
public struct EixoOverrideEntry
{
    [Tooltip("Slot cujo leque de eixos esta entrada afeta. -1 = todos os slots.")]
    public int slotIndex;
    [Tooltip("Eixo (1..N, mesma numeração do 'Desenhar eixos') a que o setor pertence nesse slot. 0 = fora de eixo.")]
    public int eixo;
}
