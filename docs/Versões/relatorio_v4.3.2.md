# v4.3.2 - Refactor de FOW para AI 2/7

Esta versão conclui a segunda das sete etapas do refactor de Fog of War para
turnos executados por AI, jogador remoto, replay ou qualquer outra origem de
batches.

O objetivo desta etapa é substituir decisões implícitas baseadas no participante
temporariamente ativo por um contexto explícito de atualização do FoW.

## `FogUpdateContext`

Cada atualização relevante passa a declarar:

- `gameplaySlot`: participante cuja ação confirmada originou a atualização;
- `observerSlot`: participante para o qual o conhecimento é calculado;
- `presentationSlot`: perspectiva autorizada nos Tilemaps locais;
- `publishGameplayData`: permissão para publicar o snapshot consultável;
- `publishVisuals`: permissão para alterar overlay, memória visual e visibilidade
  runtime;
- `recordExplorationMemory`: permissão para registrar memória confirmada;
- `recordIntel`: permissão para alimentar o ledger de contatos.

Assim, a origem da ação, o dono do conhecimento e o observador visual deixam de
ser inferidos como se fossem necessariamente o mesmo participante.

## Fluxos cobertos

O contexto explícito foi aplicado em:

- full refresh do participante ativo;
- cálculo `DataOnly` durante o turno adversário;
- publicação da perspectiva humana durante o turno da AI;
- atualização incremental depois do compromisso de uma unidade;
- republicação visual depois de um commit adversário;
- refresh de perspectiva solicitado pelo replay.

## Política `DataOnly`

Um contexto `DataOnly` pode:

- recalcular as contribuições do próprio observador;
- publicar seu snapshot de gameplay;
- registrar sua memória confirmada;
- registrar sua própria inteligência.

Ele não pode escrever nos Tilemaps visuais. Essa proibição agora está expressa
em `publishVisuals=false` e também é consultada pela barreira defensiva de
escrita.

Mesmo quando `gameplaySlot`, `observerSlot` e `presentationSlot` coincidirem por
acaso, o modo `DataOnly` não recebe autoridade visual.

## Ponte para coletores legados

Alguns algoritmos existentes ainda consultam `ActiveSlotId`. A adaptação
temporária de `activePlayerListIndex` e `activeTeamId` foi concentrada em:

```text
EnterFogObserverScope
ExitFogObserverScope
```

O estado anterior, inclusive um contexto aninhado, é restaurado em `finally`.
Essa ponte não decide permissões; apenas apresenta ao código legado o
`observerSlot` já autorizado.

Isso remove as trocas temporárias espalhadas entre full refresh, atualização
incremental e replay, preparando a futura migração dos coletores para receber o
slot diretamente.

## Barreiras integradas ao contexto

A barreira de memória confirma que o contexto ativo:

- permite `recordExplorationMemory`;
- pertence ao mesmo `observerSlot` que receberá a memória;
- está em `CursorState.Neutral`;
- possui o cache correto ativado.

A barreira visual confirma que:

- o contexto permite `publishVisuals`;
- o cache pertence ao `presentationSlot`;
- o estado está em `CursorState.Neutral`.

## Diagnóstico

O log `[FoW][Context]` passa a informar:

- slots de gameplay, observação e apresentação;
- permissões de publicação, visual, memória e inteligência;
- time ativo e cena/tilemap utilizados.

Uma inconsistência entre o escopo legado e o contexto explícito produz
`observer_scope_mismatch` quando os logs de validação estão habilitados.

## Contrato transacional

- o contexto não autoriza publicação antes do compromisso;
- memória, inteligência e apresentação definitiva continuam restritas ao retorno
  a `Neutral`;
- cancelamento não cria contexto de publicação confirmada;
- AI, humano, jogador remoto e replay usam a mesma separação entre executor,
  observador e apresentação;
- `PlayerSlotId` continua sendo a identidade de conhecimento.

O contrato foi registrado em
`docs/arquitetura/fow_canais_visibilidade.md`.

## Alterações paralelas incluídas

Conforme solicitado por `git add .`, este marco também inclui o estado corrente
dos ajustes de tutorial existentes no workspace.

## Validação

- `Assembly-CSharp.csproj` compilado com zero erros.
- Permanecem 248 avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- As alterações paralelas do tutorial foram preservadas.

