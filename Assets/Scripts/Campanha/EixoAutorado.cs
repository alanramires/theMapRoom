using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Um eixo de avanco escrito pelo autor: o "caminho de pao" que a IA de UM slot
/// segue dentro de UM quadrante.
///
///   slot 0   A -> B -> F      F e o rally (o ultimo da lista)
///   slot 1   G -> H -> E
///
/// Existe porque o leque automatico (InvasionAxisMap, por angulo a partir do QG)
/// as vezes monta eixo estupido, e o override por construcao so corrige setor a
/// setor. Aqui o autor escreve o grafo inteiro. Quando o quadrante tem eixo
/// autorado para o slot, a IA usa EXATAMENTE isto — sem angulo, sem override.
/// Sem eixo autorado, tudo segue como antes.
///
/// Setor que nao aparece em nenhum eixo do slot fica FORA de eixo (o G do slot 0):
/// e intencional, nao esquecimento.
///
/// Guarda ROTULOS, nao coordenadas — por isso mora no QuadranteData sem violar o
/// "catalogo diz o que e, a cena diz onde esta": "Alpha" so significa algo dentro
/// deste quadrante, e cada quadrante tem a propria lista. E AUTORIA: o bake nao
/// toca nisto.
///
/// O rally continua sendo a marca no PREDIO (isRallyPoint): e onde a tropa para,
/// e o planner le a marca pra montar massa. O ultimo setor daqui deve ter um
/// rally deste slot; se nao tiver, o eixo funciona mas a massa nao se forma la.
/// </summary>
[System.Serializable]
public class EixoAutorado
{
    [Tooltip("Slot dono do eixo. Cada lado tem os seus.")]
    public int slotIndex;

    [Tooltip(
        "Setores na ordem de avanco, do mais perto do QG ao rally. O ULTIMO e o setor "
        + "do rally. QG e base nao entram.")]
    public List<ConstructionSector> caminho = new List<ConstructionSector>();

    public bool IsValid => caminho != null && caminho.Count > 0;

    public ConstructionSector Rally => IsValid ? caminho[caminho.Count - 1] : ConstructionSector.None;

    public override string ToString()
    {
        return $"slot {slotIndex}: {(IsValid ? string.Join(" -> ", caminho) : "(vazio)")}";
    }
}
