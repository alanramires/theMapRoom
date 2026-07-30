# v4.3.5 - Refactor de FOW para AI 5/7

Esta versão conclui a quinta das sete etapas do refactor de Fog of War para
turnos executados por AI, jogador remoto, replay ou qualquer outra origem de
batches.

O objetivo desta etapa é representar alterações confirmadas do tabuleiro por
meio de um contrato explícito e acumulável, eliminando o conjunto anterior de
flags pendentes e a limitação de armazenar somente uma unidade alterada.

## `CommittedBoardDelta`

Foi introduzido o envelope `CommittedBoardDelta`, que descreve:

- os tipos de mudança comprometida;
- as unidades alteradas, sem duplicatas;
- as células confirmadas afetadas;
- a exigência de reconciliação de fontes removidas;
- a necessidade excepcional de um full refresh.

Os tipos de mudança atualmente representados são:

```text
UnitActed
UnitSpawned
UnitRemoved
MultiUnitChanged
```

## Acumulação de ações compostas

O modelo anterior mantinha:

- uma única unidade pendente;
- uma flag de exigência de `HasActed`;
- uma flag de full refresh;
- uma flag separada para remoções.

Notificações sucessivas podiam substituir a unidade anterior. Agora os deltas
são mesclados, preservando todas as unidades e células envolvidas na ação.

Cada unidade também mantém individualmente a exigência de `HasActed`, evitando
que a regra de uma unidade contamine spawns ou outras mudanças presentes no
mesmo delta.

## Células confirmadas

Quando existe um caminho de movimento comprometido, o delta registra:

- célula de origem;
- célula de destino;
- célula confirmada atual da unidade.

Essas informações não alteram FoW por conta própria. Elas formam a entrada
confirmada para a seleção de observadores e alvos afetados na próxima etapa.

## Barreira transacional

`SubmitCommittedBoardDelta` verifica o estado da FSM:

- em `Neutral`, o delta pode ser consumido imediatamente;
- fora de `Neutral`, o delta é apenas acumulado;
- `NotifyTurnStateReturnedToNeutral` retira e consome o envelope pendente.

Nenhuma memória, detecção, inteligência, overlay ou cache confirmado é
publicado a partir de um delta enquanto a ação ainda está provisória.

## Compatibilidade dos produtores

Os pontos públicos existentes permanecem como adaptadores:

- `NotifyUnitReachedHasAct`;
- `NotifyCommittedUnitSpawnedForFog`;
- `NotifyCommittedMultiUnitBoardChangeForFog`;
- `NotifyUnitWillBeDisabledForFog`.

Assim, movimento humano, batches da AI, jogador remoto e replay convergem para
o mesmo contrato sem depender da origem do comando.

## Consumo do delta

O consumidor mantém os caminhos seguros existentes:

- unidade isolada usa atualização incremental;
- mudança multiunidade declarada usa full refresh;
- remoção solicita reconciliação das fontes;
- cache ausente ou inconsistente mantém fallback conservador.

Esta etapa normaliza o contrato. A redução dos observadores e alvos
republicados será feita na etapa seguinte.

## Diagnóstico

O log `[FoW][CommittedDelta]` informa:

```text
kinds
units
cells
full
reconcile
```

Isso permite verificar se cada ação gerou o delta esperado antes de medir o
custo dos consumidores.

## Documentação

O contrato foi registrado em
`docs/arquitetura/fow_canais_visibilidade.md`.

## Alterações paralelas incluídas

Conforme solicitado por `git add .`, este marco também inclui o estado corrente
dos ajustes de tutorial presentes no workspace.

## Validação

- `Assembly-CSharp.csproj` compilado com zero erros e zero warnings.
- `git diff --check` concluído sem erros.
- Ações compostas preservam múltiplas unidades no mesmo delta.
- A publicação definitiva permanece restrita ao retorno a `Neutral`.
