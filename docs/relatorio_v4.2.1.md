# v4.2.1 - Refactor do Save/Load para SlotID parte 1/6

Esta versão conclui a primeira das seis etapas do refactor do Save/Load de Fog of War para identidade explícita por `PlayerSlotId`.

O objetivo desta etapa é corrigir a semântica e preparar uma migração segura, sem ativar ainda a restauração do cache runtime e sem alterar as regras de visão ou detecção.

## Save v15

- O formato de save foi atualizado para a versão `15`.
- `fogObserverSlotIndex` passa a identificar explicitamente o slot observador proprietário do snapshot runtime de FOW.
- `fogExploredCellsBySlot` passa a armazenar explicitamente a memória de exploração por slot.
- Saves novos deixam vazios os campos legados equivalentes.

## Migração de saves antigos

- `fogCacheTeamId`, usado até a v14, é migrado diretamente para `fogObserverSlotIndex`.
- Apesar do nome histórico, esse campo sempre recebeu `ActiveSlotId.Value`; portanto, ele não é convertido por `TeamId`.
- A migração direta preserva dois participantes diferentes que compartilham a mesma cor.
- `fogExploredCellsByTeam` é transferido para `fogExploredCellsBySlot`.
- Depois da migração, os campos legados são limpos em memória para evitar duas representações concorrentes.

## Runtime orientado a slot

Foram corrigidos nomes internos cuja implementação já era indexada por slot:

- cache ativo do observador;
- snapshots de gameplay do FOW;
- células exploradas;
- memória conhecida de construções;
- parâmetros de publicação, consulta e renderização.

A API explícita de restauração agora recebe o índice do slot observador. A assinatura antiga foi preservada como ponte marcada com `Obsolete`, evitando quebra imediata de consumidores externos.

## Escopo deliberadamente preservado

- O load continua descartando a fotografia runtime persistida.
- `RefreshFogOfWarForActiveTeam()` continua executando o cold refresh confirmado depois da reidratação.
- Nenhum fast path foi conectado.
- O formato ainda não persiste contribuições individuais por unidade ou construção.
- A distinção entre revelação geográfica e detecção permanece para a etapa seguinte.

## Contrato transacional

Esta etapa altera identidade, serialização e nomes, mas não muda o momento de publicação do FOW. Visão, detecção, exploração e memória definitivas continuam sendo atualizadas somente a partir do tabuleiro confirmado e em `CursorState.Neutral`.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem 248 avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- Auditoria de referências confirmou que o método explícito de restauração não está conectado ao fluxo de load.

## Próxima etapa

Etapa 2/6: separar formalmente revelação geográfica de capacidade de detecção no runtime, sem persistir ou restaurar ainda as contribuições por fonte.
