# v5.0.1 — Plano de Otimização do Tabuleiro, Melhor X e Jogabilidade 1/8

## Visão geral

Esta versão conclui a primeira parte do plano de otimização do tabuleiro:
instrumentar o custo real das consultas atuais antes de introduzir índices ou
caches.

O objetivo é estabelecer uma linha de base confiável para responder quantas
vezes cada ferramenta é chamada, quanto tempo consome, quantas ondas de
movimento reconstrói e quantas células percorre. Assim, as próximas etapas
podem ser comparadas com o comportamento original sem depender apenas da
percepção de lentidão.

Esta etapa é estritamente observacional. Nenhuma regra de movimento, ranking,
legalidade, ocupação, Fog of War ou comprometimento de ação foi alterada.

## Medição por decisão

Cada decisão de unidade da IA passa a registrar o tempo acumulado dos estágios
que participou da escolha, incluindo:

- `MelhorEstoque`;
- `MelhorPouso`;
- `MelhorEmbarque`;
- `MelhorDesembarque`;
- `QueroCarona`;
- `QueroCaronaAerea`;
- planejamento de transporte;
- caminhos válidos;
- mapas de custo de movimento;
- demais estágios de decisão que já utilizavam o medidor da Phase 2.

O relatório individual mantém os tempos de decisão, execução, reconstrução de
snapshot e atraso visual. Os estágios internos agora usam precisão decimal
para que consultas curtas, mas repetidas, não desapareçam como `0ms`.

## Medição agregada da Phase 2

Foi acrescentado um acumulador que começa junto da Phase 2 e reúne todas as
decisões realizadas naquela fase. Ao final, o Console emite uma linha
`boardQueries` com dois grupos:

```text
boardQueries stages=... metrics=...
```

`stages` mostra tempo total e quantidade de chamadas por serviço. `metrics`
mostra os volumes de trabalho realizados durante a fase inteira.

Isso permite identificar tanto uma chamada isoladamente cara quanto uma
consulta barata que foi repetida dezenas de vezes.

## Contadores introduzidos

O pathfinding registra:

- `MovementWavesBuilt`;
- `MovementCacheMisses`;
- `ValidPathWaves`;
- `MovementCostWaves`;
- `MovementQueryCachesBuilt`;
- `PathStatesExpanded`;
- `MovementCostCellsExpanded`;
- `ReachableCellsProduced`;
- `CellsVisited`.

Nesta versão toda nova onda é registrada como miss, pois o cache compartilhado
ainda não existe. Esse número é a linha de base que permitirá provar os hits e
a eliminação de reconstruções na futura etapa de cache de movimento.

As ferramentas registram:

- `MelhorEstoqueCalls`;
- `MelhorPousoCalls`;
- `MelhorEmbarqueCalls`;
- `MelhorDesembarqueCalls`;
- `QueroCaronaCalls`;
- `QueroCaronaAereaCalls`;
- `TransportPlanningCalls`.

As varreduras integrais de `cellBounds` executadas por Melhor Pouso e Melhor
Embarque também registram:

- `TopologyFullScans`;
- `TopologyCellsVisited`;
- `CellsVisited`.

Com isso, a próxima etapa poderá demonstrar diretamente que o índice permanente
retirou a redescoberta de praias, superfícies de pouso e LZs do caminho quente
da IA.

## Contrato transacional

A instrumentação só observa chamadas executadas dentro da decisão ativa da IA.
Ela não publica índices, não invalida revisões e não grava informações
provisórias no estado confirmado.

Movimentos temporários, abertura de sensores, animações e cancelamentos
continuam sem alterar a verdade persistente do tabuleiro. A ação permanece
provisória até o compromisso explícito e o retorno a `CursorState.Neutral`.

Os sensores oficiais continuam sendo a autoridade de legalidade. Nenhuma
métrica participa de ranking ou decisão.

## Validação

- `Assembly-CSharp.csproj` compilado sem erros;
- `Assembly-CSharp-Editor.csproj` compilado sem erros;
- `git diff --check` aprovado;
- avisos de compilação existentes permanecem sem relação com esta etapa;
- revisão do diff confirmou apenas cronômetros, acumuladores, contadores e
  logs;
- nenhuma implementação de cache foi antecipada nesta versão.

## Próxima etapa

A Parte 2 implementará e validará o `BoardTopologyIndex`, contendo a geografia
imutável do mapa e seus candidatos estruturais. Os consumidores atuais serão
mantidos inicialmente para permitir comparação funcional antes da remoção das
varreduras completas.

