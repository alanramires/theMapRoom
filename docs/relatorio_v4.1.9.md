# v4.1.9 - Separar FOW e Intel por SlotId + expor caças furtivos no revide

Esta versão consolida a quinta etapa da migração de identidade dos participantes. Fog of War, detecção, memória de exploração e inteligência deixam de compartilhar estado pela cor visual (`TeamId`) e passam a identificar o observador pelo slot lógico (`PlayerSlotId`).

O cenário de referência permanece uma partida em que `slot 2` e `slot 3` usam a cor vermelha. Apesar da aparência igual, cada participante agora possui visão, células exploradas, contatos, memória e decisões de inteligência independentes.

## FOW por slot

- O cache runtime do Fog of War identifica o slot observador.
- Snapshots de células visíveis, células conhecidas e unidades detectadas são publicados por slot.
- Unidades contribuem para a visão apenas do próprio `SlotIndex`.
- Construções concedem visão e revelação somente ao slot proprietário.
- A apresentação humana ou parcial do FOW resolve o slot exato, sem selecionar silenciosamente o primeiro participante da mesma cor.
- Foram adicionadas consultas explícitas de visibilidade e exploração por `PlayerSlotId`.
- APIs legadas por `TeamId` só são usadas quando o contexto ativo ou uma associação inequívoca permite resolver um único slot.

## Detecção e ocultação

- Consultas coletivas de sensores filtram observadores por `SlotIndex`.
- Chaves de cache de detecção registram o slot do observador.
- Revelação temporária de stealth é registrada para o slot detector.
- Dois participantes com a mesma cor não compartilham contatos nem revelação de unidades furtivas.
- Consultas de combate, comandos e snapshots da IA passaram a encaminhar o slot observador.

## Inteligência da IA

- O `AIIntelLedger` passou a ser indexado pelo slot da IA.
- Contatos visíveis, sinais de ameaça e compromissos de compra de elite são independentes por participante.
- O snapshot da IA consulta inimigos visíveis usando `AISlotIndex`.
- O processamento de eventos de combate compara o slot observador registrado no log.
- Compatibilidade por `TeamId` não mistura participantes quando a mesma cor aparece em mais de um slot.

## Memória, briefing e save

- Células exploradas são exportadas e restauradas com `slotIndex`.
- Memória conhecida de construções registra o slot observador.
- Eventos do Jornal do Comandante possuem slot destinatário e são drenados apenas no turno daquele participante.
- O ledger persistido da IA armazena `observerSlotIndex`.
- O formato de save foi atualizado para a versão `12`.
- Saves antigos são migrados por cor somente quando o `TeamId` identifica um único slot.

## Caças furtivos no revide

- Um defensor que efetivamente executa o revide agora recebe `MarkAsFired()`.
- O revide passa a expor aeronaves furtivas da mesma forma que um ataque iniciado por elas.
- A regra fica simétrica ao comportamento já existente dos submarinos ao revidar.
- A documentação de visão, ação e decisões de design foi atualizada para registrar que disparar custa ocultação, inclusive no revide automático.

## Contrato transacional

O refactor preserva a regra de que FOW, exploração, contatos e inteligência definitiva só são publicados após o compromisso da ação e em estado `Neutral`. Movimento provisório e previews não passam a gravar conhecimento confirmado.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.

## Próximas etapas

- Migrar integralmente os Stages, planners e demais decisões da IA ainda dependentes de `TeamId`.
- Revisar replay e estruturas legadas restantes.
- Reexecutar o stress test com dois slots vermelhos e validar FOW, contatos, briefing e compras independentes.
