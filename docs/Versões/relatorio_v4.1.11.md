# v4.1.11 - Separar Save and load por SlotID

Esta versão conclui a sétima etapa da migração de identidade dos participantes. Save, load, replay, relatórios e componentes de UI passam a preservar o `SlotId` como identidade real, mantendo `TeamId` somente como atributo visual e compatibilidade com dados antigos.

O cenário de referência continua sendo uma partida em que `slot 2` e `slot 3` usam a mesma cor vermelha. Salvar, carregar ou assistir ao replay não pode fundir esses participantes nem transferir estado entre eles.

## Save e load

- O formato de save foi atualizado para a versão `14`.
- O participante ativo é persistido e restaurado por `activeSlotIndex`.
- Cada jogador salvo possui seu próprio `slotIndex`.
- Unidades e construções preservam o slot proprietário.
- Configuração automática do Serviço de Comando é restaurada por slot.
- Relatórios de início de turno persistem o slot destinatário.
- A restauração seleciona o participante ativo sem iniciar um novo turno nem provocar efeitos colaterais.

## Compatibilidade com saves antigos

- `TeamId` continua presente como informação visual e campo legado.
- Dados antigos sem slot tentam migrar pela cor somente quando existe uma associação única.
- Se dois slots usam a mesma cor, o load não escolhe arbitrariamente o primeiro slot.
- O slot ativo fornece contexto quando a compatibilidade legada pode ser resolvida com segurança.

## Replay por slot

- Turnos gravados possuem `ActingSlotIndex`.
- Ações individuais possuem `ActingSlotIndex`.
- Snapshots preservam o slot ativo.
- A correspondência entre o turno em execução e o registro do replay compara o slot.
- Seleção de turno específico diferencia participantes que usam a mesma cor.
- O observador do replay possui `observerSlotIndex`.
- A visão e o Fog of War do replay são recalculados para o slot observador exato.
- Dados antigos de replay mantêm fallback por `TeamId` apenas quando a resolução é inequívoca.

## UI e relatórios

- O painel de replay seleciona e exibe `Slot N (Cor)`.
- A seleção do observador percorre os slots configurados na partida.
- O relatório de briefing é armazenado e restaurado por slot.
- Dicas aprendidas e indicadores de tutorial usam o slot do participante ativo.
- Contagens e verificações de unidades relacionadas ao turno deixam de agrupar jogadores pela cor.

## Jogadas e telemetria

- Compras, destruição, logística e Serviço de Comando registram o slot executor ou proprietário.
- As fases 1 e 2 da IA registram o `AISlotIndex`.
- Ações humanas e da IA propagam o slot para o histórico.
- Resultados de combate e cargas transportadas registram os slots envolvidos.
- Os nomes legados dos campos de CSV são mantidos por compatibilidade, mas sua semântica passa a ser de slot.

## Contrato transacional

A alteração preserva o contrato de ações transacionais. Save, replay e UI leem o estado confirmado; nenhuma atualização definitiva de posição, recursos, FOW, inteligência, ocupação ou `HasActed` foi movida para previews, animações ou estados provisórios.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem 249 avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- O scan residual confirmou que os principais registros de jogadas desta etapa não usam mais a cor como identidade.

## Próximas verificações

- Realizar save e load no turno de cada um dos dois slots vermelhos.
- Confirmar que economia, unidades, briefing e Serviço de Comando retornam ao slot correto.
- Reproduzir turnos alternados dos dois slots vermelhos com FOW parcial.
- Validar seleção de observador separadamente para cada slot de mesma cor.
