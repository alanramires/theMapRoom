# Game start tunning

## Versão

`v6.0.7`

## Objetivo

Este ponto de verificação ataca o tempo de abertura da partida. O gatilho foi
prático: classificar os setores do mapa de teste — de um único setor genérico
para 29 setores e 2 bases — fez o jogo demorar cerca de 20 segundos a mais para
abrir.

## O que estava caro

`SectorManager` calcula distância de movimento terrestre entre pares de células
para montar a vizinhança dos setores. Com um setor só, isso custava 377 ms. Com
29 setores, 12,5 segundos.

A comparação entre as duas configurações do mesmo mapa mostrou que **nenhuma
busca ficou mais lenta**: passaram a existir cerca de 40 vezes mais buscas, e
cada uma ficou 3 vezes mais longa. O custo por expansão era o mesmo nos dois
casos.

| | 1 setor | 29 setores | fator |
|---|---|---|---|
| `search.calls` | 88 | 3584 | 40× |
| `search.expanded` | 11259 | 337974 | 30× |
| tempo | 377 ms | 11254 ms | 30× |

## O culpado

Instrumentar o interior da busca — contadores baratos, cronômetro apenas por
expansão — apontou o ponto exato:

```text
vizinhos=10746ms            de total=10881ms
rota.calls=1916552
rota.topologia=16879        índice respondia 0,9% das consultas
rota.varreduraRede=1899673  99,1% caía no fallback
```

`TryGetConnectedRouteEnterCostForUnitData` responde "existe estrada entre estes
dois hexes vizinhos?". Quando o índice de topologia não responde, ela cai numa
varredura tripla aninhada — para cada rede rodoviária, para cada estrutura do
banco, para cada rota da estrutura, para cada célula da rota.

Isso rodava **1,9 milhão de vezes** por reconstrução, relendo todas as rotas
rodoviárias do projeto a cada passo de hex.

## As correções

**Memoização do custo de rota por par de células.** O tabuleiro tem poucos
milhares de pares vizinhos possíveis; as 816 buscas reais atravessavam os
mesmos hexes repetidamente. A chave reusa a `LandDistanceCacheKey` existente,
que já inclui o identificador de contexto — necessário porque o custo depende
da `UnitData` de referência.

Resultado: 1,9 milhão de execuções viraram **4.434** cálculos reais.

**Fronteira em heap binário.** A busca usava `List` e pagava dois laços O(n)
dentro do laço principal: varredura linear para achar o mínimo a cada remoção,
e `Contains` por vizinho. A ordem de inserção entra como chave secundária do
heap, o que reproduz exatamente o desempate anterior — custo e rota saem
idênticos, não apenas equivalentes.

**Memoização de terreno e de presença de tile por célula.** Ambos dependem
apenas dos tilemaps e não mudam durante uma reconstrução. Sem isso, cada
transição reabria `Tilemap.GetTile` em todas as camadas do grid.

## Resultado

Medido em abertura fria genuína — Unity fechado e reaberto —, reproduzido em
duas execuções consecutivas com contadores idênticos.

| | antes | depois |
|---|---|---|
| reconstrução do SectorManager | 12567 ms | **1703 ms** |
| `vizinhos` | 10746 ms | 1575 ms |
| `rota.varreduraRede` | 1899673 | 4393 |
| primeiro frame | 45,5 s | **18,3 s** |
| segundo frame | 13,0 s | 3,7 s |
| aquecimento de FoW | 13,9 s | 4,6 s |

Dois invariantes foram conferidos a cada passo e permaneceram intactos:
`search.expanded=337974` e `cache.size=3126`. O caminho percorrido pela busca é
o mesmo; apenas ficou barato percorrê-lo.

## Método

Registro honesto: duas tentativas anteriores falharam. O heap rendeu cerca de
10% e a memoização de terreno rendeu zero no caminho frio. Ambas nasceram de
leitura de código, não de medição.

Duas leituras erradas atrapalharam no meio do caminho:

- reconstruções de 32 ms foram tomadas como prova de sucesso quando eram, na
  verdade, segunda sessão de Play — campos `static` sobrevivem sem recarga de
  domínio, e só fechar o Unity dá medida fria;
- `FrameSpike` de dezenas de segundos com alocação próxima de zero foi lido
  primeiro como Editor ocioso e depois como laço apertado. As duas leituras são
  possíveis e o sinal sozinho não distingue.

O culpado só apareceu quando os contadores foram cravados dentro da busca. É a
mesma lição que já havia funcionado em investigações anteriores: medir o funil
em vez de deduzir a partir do código.

## Não resolvido

**O índice de topologia está em modo fallback.** `BoardTopologyIndex` respondeu
41 consultas de 1,9 milhão. Com ele assado, a varredura de `RoadNetworks` sairia
do caminho quente e a memoização de rota se tornaria supérflua. É o conserto de
raiz.

**`BuildLandDistanceContextId` é recalculado por chamada.** São 1,9 milhão de
execuções apenas para montar a chave do cache. Içá-lo para fora do laço deve
reduzir boa parte dos 1575 ms restantes.

**A invalidação explícita não tem chamador.** `InvalidateLandDistanceCache()`
existe e nada a aciona; a única invalidação real é o fingerprint de layout, que
cobre construções. Repintar terreno em tempo de execução não avisa esses
caches. Com os caches agora carregando peso de verdade, essa lacuna passou a
importar mais.

**O aquecimento de FoW não é custo de CPU.** Ele reporta cerca de 680 ms de
processamento e o restante como espera entre quadros, por orçamento de 40 ms
por quadro. Encurtar aquilo é ajustar o orçamento, não otimizar.
