# v4.3.3 - Refactor de FOW para AI 3/7

Esta versão conclui a terceira das sete etapas do refactor de Fog of War para
turnos executados por AI, jogador remoto, replay ou qualquer outra origem de
batches.

O objetivo desta etapa é persistir fotografias de contribuições de FoW por
`PlayerSlotId`, evitando que apenas o observador ativo sobreviva ao save.

## Save v18

O formato de save foi elevado para a versão 18 e passa a incluir:

```text
fogSourceCachesByObserverSlot
```

Cada `FogObserverSourceCacheSaveData` contém:

- `observerSlotIndex`;
- versão interna do formato do cache;
- hash da configuração do tabuleiro e sensores;
- contribuições geográficas e sensoriais por fonte.

Cada contribuição continua contendo:

- identidade e tipo estável da fonte;
- assinatura do estado confirmado;
- checksum;
- células geográficas canonicalizadas;
- células sensoriais canonicalizadas.

## Exportação por slot

O save exporta:

- o runtime atualmente ativo;
- todas as fotografias quentes armazenadas em
  `fogContributionRuntimeBySlot`.

Os blocos são ordenados por `PlayerSlotId`. O runtime ativo prevalece sobre uma
fotografia armazenada do mesmo slot, garantindo que o save use o estado
confirmado mais recente.

Blocos sem contribuições não são gravados.

## Restauração independente

Durante o load:

1. os blocos dos slots não ativos são processados em contexto `DataOnly`;
2. cada bloco é validado integralmente;
3. blocos válidos são armazenados no runtime quente por slot;
4. o bloco do slot ativo é restaurado pelo fast path visual;
5. se o slot ativo falhar, apenas ele executa o cold fallback.

Um erro em um observador não descarta fotografias válidas já restauradas para
outros slots.

## Validações preservadas

Cada bloco passa pelas validações existentes:

- slot observador;
- versão interna do formato;
- hash da configuração;
- checksum de cada fonte;
- identidade e assinatura de unidades e construções;
- conjunto completo de fontes elegíveis;
- células pertencentes ao Tilemap canônico;
- ausência de duplicatas;
- equivalência dos canais das unidades;
- regras geográficas e sensoriais das construções;
- contadores agregados reconstruídos.

Nenhum bloco parcialmente válido é aceito.

## Publicação `DataOnly`

A restauração de um slot não ativo:

- não pinta overlay;
- não pinta memória geográfica;
- não altera visibilidade runtime;
- não publica eventos;
- não grava contatos;
- não altera a perspectiva local.

Ela apenas reconstrói e armazena a fotografia de contribuições confirmadas.

## Compatibilidade com v17

Saves v17 continuam aceitos. Quando não existe
`fogSourceCachesByObserverSlot`, a fotografia única legada é encapsulada em um
bloco usando o `fogObserverSlotIndex` original.

A migração:

- ocorre somente em memória;
- não converte a identidade por `TeamId`;
- preserva corretamente dois slots que compartilham a mesma cor;
- volta a ser gravada no formato v18 no próximo save.

## Logs

`[FoW][LoadCacheRestore]` passa a informar:

```text
slot=<PlayerSlotId>
success=<true|false>
retained=<true|false>
```

Para o slot ativo, uma falha também informa `fallback=cold`.

## Contrato transacional

- somente fotografias confirmadas entram no save;
- restauração acontece em `CursorState.Neutral`;
- slots não ativos são reconstruídos em `DataOnly`;
- nenhum cache restaurado concede autoridade visual;
- a identidade do conhecimento permanece `PlayerSlotId`;
- qualquer divergência mantém fallback conservador.

O contrato foi atualizado em
`docs/arquitetura/fow_canais_visibilidade.md`.

## Alterações paralelas incluídas

Conforme solicitado por `git add .`, este marco também inclui o estado corrente
dos ajustes de tutorial e assets presentes no workspace.

## Validação

- `Assembly-CSharp.csproj` compilado com zero erros.
- `git diff --check` concluído sem erros.
- A restauração mantém validação e fallback independentes por slot.
- Alterações paralelas existentes foram preservadas.

