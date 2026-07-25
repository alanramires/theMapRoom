# v4.3.6 - Refactor de FOW para AI 6/7

Esta versão conclui a sexta das sete etapas do refactor de Fog of War para
turnos executados por AI, jogador remoto, replay ou qualquer outra origem de
batches.

O objetivo desta etapa é limitar a republicação de visibilidade às unidades
realmente afetadas por um `CommittedBoardDelta`, evitando reavaliar todas as
unidades depois da alteração incremental de uma única fonte.

## Conjunto de impacto

Ao atualizar uma fonte de visão, o consumidor constrói um conjunto com:

- células geográficas contribuídas antes da mudança;
- células sensoriais contribuídas antes da mudança;
- células geográficas contribuídas depois da mudança;
- células sensoriais contribuídas depois da mudança;
- origem, destino e posição confirmada registrados no delta.

Essa união cobre tanto unidades que deixaram de ser observadas quanto unidades
que passaram a estar dentro da nova cobertura.

## Snapshot de gameplay

`PublishFogGameplaySnapshot` passou a aceitar um conjunto opcional de células
afetadas.

Quando existe um snapshot anterior válido:

- a cobertura geográfica e sensorial confirmada é republicada;
- a memória de exploração continua sendo registrada normalmente;
- entradas de visibilidade fora do conjunto são preservadas;
- somente unidades posicionadas nas células afetadas são reavaliadas.

Se o snapshot não existe ou o conjunto não é utilizável, a varredura integral
continua sendo executada.

## Apresentação runtime

Foi adicionado um caminho de atualização visual por células. Ele:

- mantém o cache de visibilidade das unidades não afetadas;
- recalcula apenas unidades dentro do conjunto;
- aplica sprite e HUD conforme a perspectiva de apresentação;
- atualiza a prioridade visual de pilhas sem recalcular sensores.

Mudanças de persistência de uma unidade stealth também usam sua célula
confirmada como alvo isolado, em vez de atualizar visualmente o exército
inteiro.

## Inteligência incremental

`AIIntelLedger.RecordVisibleContactsForSlot` aceita o mesmo filtro opcional.

No caminho incremental, somente inimigos nas células afetadas são consultados
e eventualmente registrados. Contatos fora da área continuam preservados com
seu estado anterior.

Chamadas de inicialização, load, full refresh e reconstrução pesada permanecem
sem filtro.

## Executor e observador separados

Quando gameplay e apresentação pertencem a slots diferentes:

- o executor usa a união da cobertura antiga e nova, pois sua fonte de visão
  mudou;
- o observador visual usa apenas origem, destino e posição confirmada da
  unidade movida;
- a cobertura da AI não é usada como autoridade para recalcular a memória do
  jogador humano;
- o mesmo isolamento vale para um jogador remoto.

## Fallbacks

A varredura integral permanece ativa quando:

- não existe snapshot anterior;
- o cache runtime ainda não foi inicializado;
- o conjunto de impacto está vazio;
- uma fonte precisa ser reconciliada;
- a ação declara uma mudança multiunidade;
- qualquer fast path anterior falha.

A otimização não altera o resultado de visibilidade e não substitui os
fallbacks conservadores.

## Diagnóstico

Os novos logs são:

```text
[FoW][AffectedTargets]
[FoW][AffectedTargets][Visual]
```

Eles informam:

- slot observador;
- quantidade de células afetadas;
- quantidade de unidades efetivamente avaliadas;
- total de unidades disponível no snapshot, quando aplicável.

## Contrato transacional

O conjunto de impacto nasce somente do `CommittedBoardDelta` e das
contribuições confirmadas da fonte.

Nenhuma atualização de snapshot, apresentação, memória ou inteligência ocorre
antes do retorno a `CursorState.Neutral`.

## Documentação

O comportamento foi registrado em
`docs/arquitetura/fow_canais_visibilidade.md`.

## Alterações paralelas incluídas

Conforme solicitado por `git add .`, este marco também inclui o estado corrente
dos ajustes de tutorial presentes no workspace.

## Validação

- `Assembly-CSharp.csproj` compilado com zero erros e zero warnings.
- `git diff --check` concluído sem erros.
- Snapshot, apresentação e inteligência compartilham o mesmo filtro confirmado.
- Fallbacks integrais continuam disponíveis.
