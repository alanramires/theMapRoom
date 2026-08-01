# Start and loading optimization

## Versão

`v6.0.1`

## Objetivo

Consolidar as otimizações finais de início e carregamento realizadas após o
checkpoint `v6.0.0`, reduzindo o tempo até o tabuleiro e a IA estarem realmente
prontos para interação.

O resultado observado no mapa de teste com 61 unidades da IA foi um load
percebido de aproximadamente 5 segundos, com o beep emitido somente depois da
preparação efetiva da iniciativa.

## Distâncias persistentes do tabuleiro

O `SectorManager` deixou de recalcular repetidamente as mesmas distâncias entre
construções.

- distâncias terrestres são memoizadas por origem, destino e contexto;
- a chave considera Tilemap, Terrain Database e UnitData de referência;
- uma impressão digital do layout das construções invalida o cache quando a
  geometria relevante muda;
- dono, pontos de captura e unidades em campo não invalidam distâncias
  estruturais que permanecem idênticas;
- consultas que exigem reconstrução do caminho continuam usando a busca real;
- resultados inalcançáveis também são armazenados.

A telemetria separa construção de contextos, loop de setores, vizinhança,
número de buscas, hits, falhas, expansões e custo total.

No teste validado, 44 consultas foram atendidas pelo cache e a reconstrução de
setores caiu para poucos milissegundos.

## Warmup do Fog of War por orçamento

O warmup deixou de processar rigidamente uma fonte por frame. Agora utiliza um
orçamento de CPU configurável:

- valor padrão de 40 ms por frame;
- várias fontes baratas podem ser processadas no mesmo frame;
- o warmup ainda cede controle ao atingir o orçamento;
- troca de turno, load, mudança de geração e estado não neutro continuam
  cancelando ou suspendendo o trabalho;
- o painel permanece responsável por ocultar a preparação.

Isso reduz o tempo perdido apenas aguardando dezenas de frames quando o custo
real por fonte já foi otimizado.

## Cache de construção e estrutura por célula

As consultas de visão passaram a reutilizar construção e estrutura resolvidas
por célula durante o mesmo refresh:

- evita `FindObjectsByType` repetido para cada célula;
- armazena também resultados ausentes;
- usa o mesmo escopo do cache de terreno;
- reduz o custo tanto da coleta de visão quanto da publicação das células
  conhecidas.

O FoW confirmado e suas regras de camada não foram alterados; somente a
resolução repetitiva dos mesmos dados foi eliminada.

## Vigilância Aérea em Air/High

EWACS e Radar Móvel agora ranqueiam ganho de cobertura somente em `Air/High`.

`Air/Low` permanece disponível no FoW oficial e é recalculado normalmente
depois da ação comprometida, mas deixa de exigir uma segunda passada estrutural
para cada hex candidato da decisão.

No teste do EWACS:

- decisão anterior: aproximadamente 69,4 s;
- decisão após a política `Air/High`: aproximadamente 4,76 s;
- atualização incremental oficial do FoW: aproximadamente 245 ms.

A métrica
`AirSurveillanceCoverageAirLowSkippedByPolicy` registra as passadas evitadas.

## Telemetria de FoW

Foram adicionadas medições para distinguir:

- busca de unidades;
- coleta de células;
- visão de construções;
- publicação;
- visibilidade das unidades;
- inteligência;
- renderização;
- callbacks;
- armazenamento do runtime;
- células visitadas, checagens de camada e especificação;
- chamadas e hits de LoS;
- custos de ativação, trabalho, armazenamento e restauração do warmup;
- unidades mais caras com nome de objeto, slot e célula.

Essas métricas permitiram substituir hipóteses por custos observados e
continuam disponíveis para mapas maiores.

## Resultado observado

No teste final:

- reconstrução de setores: aproximadamente 3,8 ms;
- `PlanningBarrier`: aproximadamente 21 ms;
- `CommitAIWorldHeavy`: aproximadamente 70 ms;
- iniciativa de 59 unidades: aproximadamente 1,17 s;
- beep emitido em `[AI Perf][PostLoadReady]`;
- cursor com `aiInputLock=False` e `gameplayInputBlocked=False`;
- nenhum warmup concorrente;
- nenhum fallback frio de FoW;
- load percebido: aproximadamente 5 segundos.

## Contrato transacional

Todos os caches são auxiliares de consulta:

- não movem unidades;
- não alteram captura ou ocupação definitiva;
- não publicam FoW provisório;
- não registram contatos ou inteligência antes do compromisso;
- não modificam combustível, munição, HP ou `HasActed`;
- o FoW oficial continua sendo atualizado após a ação confirmada e o retorno a
  `CursorState.Neutral`.

## Validação

- `Assembly-CSharp.csproj`: 0 erros e 0 warnings;
- cache de setores validado por revisão e impressão digital do layout;
- cache de célula limitado ao escopo do refresh;
- política `Air/High` limitada ao ranking puro de Vigilância Aérea;
- comportamento final observado em gameplay com aproximadamente 100 unidades
  no tabuleiro.

