using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Um TRECHO de rota dentro do retangulo de um quadrante, em coordenada LOCAL.
///
/// Trecho, nao rota — e a diferenca importa. Construcao e enfeite se recortam por
/// pertencimento: a celula esta dentro, vem; esta fora, fica. Rota nao. Rota e uma
/// SEQUENCIA ORDENADA, e o que o autor desenhou nao foi um conjunto de celulas, foi
/// uma sucessao de arestas — "daqui pra ali". Recortar corta a sequencia, e o
/// pedaco que sobra tem de virar uma rota propria, terminando na borda.
///
/// POR QUE NAO DA PRA SO FILTRAR AS CELULAS E MANTER UMA ROTA SO:
///
/// Os tres consumidores leem PARES CONSECUTIVOS, nao o conjunto. Tirar uma celula
/// do meio COLA as duas vizinhas dela numa aresta que ninguem desenhou.
///
///   visual      RoadNetworkManager.CreateRouteSegments estica um sprite de centro
///               a centro do par, sem conferir adjacencia: o buraco vira uma
///               estrada reta e comprida atravessando o vazio
///
///   movimento   UnitMovementPathRules procura a aresta (from,to) nos pares da
///               rota. Se a rota sai do retangulo e volta por uma celula VIZINHA
///               da que saiu, o par colado e adjacente de verdade — e vira um
///               atalho de estrada pela quina, com bonus e tudo, que o autor
///               nunca tracou
///
///   topologia   BoardTopologyIndexBuilder e SectorManager leem as mesmas rotas
///
/// O segundo e o perigoso: nao aparece na tela, nao loga, so faz a IA e o jogador
/// andarem mais rapido por onde nao ha estrada.
///
/// TAMBEM QUEBRA EM BURACO, e por um motivo separado: IsRouteValid e TUDO-OU-NADA
/// — uma unica celula invalida na lista descarta o desenho da rota INTEIRA. Uma
/// rodovia que atravesse um hex vazio do retangulo sumiria por completo em vez de
/// aparecer partida em duas.
/// </summary>
[System.Serializable]
public class RotaAssada
{
    [Tooltip("Id no StructureDatabase. E por ele que o build resolve a estrutura dona.")]
    public string structureId;

    [Tooltip("Nome do trecho. So log e Inspector — ganha sufixo quando o corte parte a rota.")]
    public string routeName;

    [Tooltip(
        "Celulas em coordenada LOCAL, na ordem do autor. Contiguas POR CONSTRUCAO: "
        + "o bake quebra num trecho novo a cada celula perdida.")]
    public List<Vector3Int> celulas = new List<Vector3Int>();

    public int Count => celulas != null ? celulas.Count : 0;

    public override string ToString()
    {
        return $"{structureId}/{routeName} ({Count} celulas)";
    }
}
