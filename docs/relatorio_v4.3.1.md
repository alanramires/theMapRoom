# v4.3.1 - Refactor de FOW para AI 1/7

Esta versão conclui a primeira das sete etapas do refactor de Fog of War para
turnos executados por AI, jogador remoto, replay ou qualquer outra origem de
batches.

O objetivo desta etapa é estabelecer uma barreira estrutural entre o estado de
gameplay calculado em modo `DataOnly` e a apresentação visual local.

## Barreira de memória confirmada

A memória explorada e a memória de construções somente podem ser registradas
quando:

- a partida está em `CursorState.Neutral`;
- o slot destinatário é válido;
- o cache ativo pertence exatamente ao mesmo `PlayerSlotId`.

Isso permite que a AI mantenha sua própria memória confirmada sem risco de
gravar células visitadas na memória do jogador humano.

## Barreira de apresentação

As rotinas centrais que escrevem o overlay e os Tilemaps de memória agora
confirmam que:

- o estado está em `Neutral`;
- o cache ativo pertence ao slot de apresentação local;
- o observador visual é válido.

A validação cobre:

- inicialização do overlay;
- renderização do overlay e da memória explorada;
- aplicação visual de contribuições geográficas;
- alteração visual da transparência do Fog of War.

Um cálculo da AI, jogador remoto ou replay pode continuar atualizando seus dados
em `DataOnly`, mas não recebe autoridade para pintar a perspectiva local.

## Diagnóstico defensivo

Quando `enableFogValidationLogs` está habilitado, uma tentativa recusada gera:

```text
[FoW][WriteBarrier] rejected=...
```

O aviso informa operação, slot solicitado, slot do cache, slot de apresentação
e estado transacional. Mensagens idênticas são deduplicadas para evitar poluição
dos logs.

## Contrato transacional

Esta etapa preserva a lei fundamental do tabuleiro:

- nenhuma memória definitiva é registrada antes do compromisso;
- nenhuma posição provisória alimenta o Fog of War;
- a publicação ocorre somente depois do retorno a `Neutral`;
- origem do batch e autoridade de apresentação permanecem conceitos separados;
- conhecimento e apresentação continuam identificados por `PlayerSlotId`.

O contrato foi atualizado em
`docs/arquitetura/fow_canais_visibilidade.md`.

## Escopo das próximas etapas

Esta versão ainda não altera:

- o contexto explícito de gameplay/apresentação;
- a persistência dos caches de todos os slots;
- o início de turno incremental;
- a classificação por `CommittedBoardDelta`;
- a atualização somente dos alvos afetados;
- o planejamento e a separação entre CPU ativa e wall time.

## Alterações paralelas incluídas

Conforme solicitado por `git add .`, o marco também inclui o estado corrente dos
arquivos de tutorial e dos assets adicionados ou modificados no workspace.

## Validação

- `Assembly-CSharp.csproj` compilado com zero erros.
- Permanecem 248 avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- Arquivos de trabalho já existentes foram preservados.

