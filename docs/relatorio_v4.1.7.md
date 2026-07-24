# v4.1.7 - Separar turno, ownership e relações por SlotId

Esta versão consolida as três primeiras etapas da migração arquitetural que separa a identidade lógica do participante (`SlotIndex`/`PlayerSlotId`) de sua cor ou facção visual (`TeamId`).

O objetivo é permitir que múltiplos participantes usem a mesma cor sem que o runtime os trate como um único jogador. No stress test de referência, `slot 2 = vermelho` e `slot 3 = vermelho` continuam sendo participantes independentes e inimigos entre si.

## 1. Identidade de participante

- Introduzido `PlayerSlotId` como identidade lógica explícita.
- Adicionadas APIs centrais no `MatchController` para resolver slot ativo, time visual de um slot, proprietário de unidades e construções e relações entre participantes.
- Resoluções de `TeamId` para slot exigem unicidade; cores duplicadas não podem mais selecionar silenciosamente um participante arbitrário.

## 2. Turno por Slot

- A troca de turno passou a comparar o índice do slot ativo, não apenas a cor.
- Adicionado o evento `OnActiveSlotChanged`; o evento legado por time visual foi preservado temporariamente para compatibilidade.
- Liberação de unidades, reset de `HasActed`, passageiros embarcados e foco inicial do cursor agora filtram pelo slot ativo.
- Dois slots consecutivos com o mesmo `TeamId` disparam turnos distintos.
- O ciclo de vida da IA consulta se o slot ativo é controlado por IA, evitando confundir dois participantes da mesma cor.

## 3. Ownership e relações por Slot

- Criado `PlayerSlotRelations` como regra central de aliado e inimigo.
- Slots diferentes são inimigos mesmo quando compartilham o mesmo `TeamId`.
- Seleção e autorização de unidades e construções passaram a usar `SlotIndex`.
- Ocupação, coexistência por camada, transições de altura e regras de movimento distinguem participantes por slot.
- Mira, detecção, combate, captura, embarque, fusão, supply, transferência e Serviço do Comando foram migrados nas relações diretas entre entidades.
- Remoção manual de unidade valida o proprietário pelo slot ativo.

## Construções e produção

- Adicionado `ConstructionManager.SetOwnerSlot` para transferir propriedade sem deduzir o dono pela cor.
- Capturas atribuem à construção o slot exato da unidade capturadora.
- Abertura de loja e autorização de produção usam o slot proprietário.
- Unidades compradas recebem explicitamente o slot comprador após o spawn.
- Compras diretas da IA preservam o slot proprietário da construção.

## Compatibilidade temporária

`TeamId` continua válido para:

- cor, sprite, nome e apresentação;
- registros e APIs legadas ainda não migradas;
- subsistemas reservados às próximas etapas, como economia completa, FOW, memória/inteligência da IA, vitória, save/load e replay.

Essas referências não devem voltar a ser usadas como prova de propriedade ou identidade do participante.

## Contrato transacional

As alterações desta versão afetam resolução de identidade e autorização. Elas não antecipam mutações definitivas durante ações provisórias: captura, compra e demais alterações confirmadas continuam ocorrendo somente nos fluxos explícitos de compromisso da ação.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem apenas avisos preexistentes do projeto, principalmente APIs obsoletas do Unity e análise de serialização.
- `git diff --check` concluído sem erros.

## Próximas etapas

- Migrar economia, limites e recursos por slot.
- Separar FOW, detecção confirmada e memória por slot.
- Migrar snapshots, planners e Stages da IA.
- Revisar derrota, vitória, replay e compatibilidade de saves.
- Executar novamente o stress test com dois slots vermelhos independentes.
