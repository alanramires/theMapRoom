# v4.1.8 - Separar economia por SlotId

Esta versão consolida a quarta etapa da migração de identidade do participante. Economia, progressão de compra, derrota e vitória deixam de usar `TeamId` como chave principal e passam a operar pelo slot lógico do jogador.

O cenário de referência continua sendo uma partida com dois participantes vermelhos: `slot 2` e `slot 3` possuem a mesma apresentação visual, mas mantêm tesouros, rendas, unidades, conquistas, estrelas e condições de vitória independentes.

## Economia por slot

- Adicionadas APIs de consulta, débito e alteração de dinheiro por `PlayerSlotId`.
- A economia armazenada em cada `PlayerEntry` agora é acessada diretamente pelo índice do slot.
- APIs legadas baseadas em `TeamId` somente resolvem o participante quando a cor identifica um único slot.
- Renda por turno é calculada a partir das construções cujo `SlotIndex` corresponde ao jogador.
- Crédito inicial e renda recorrente são aplicados ao slot ativo.

## Compras, serviços e limites

- Compras humanas debitam o slot ativo.
- Compras diretas da IA debitam o slot proprietário da construção.
- Unidades produzidas preservam o slot comprador.
- O limite máximo de unidades passou a ser verificado por slot.
- Supply, reparos e Serviço do Comando cobram o slot proprietário da unidade atendida.
- Previews e painéis auxiliares consultam o saldo do slot correto.

## Progressão de construções

- O histórico de construções capturadas passou a registrar `slotIndex`.
- Requisitos para produzir unidades são avaliados pelo histórico do slot.
- Construções inicialmente possuídas também liberam progressão somente para seu proprietário real.
- A captura registra a conquista para o slot capturador, mesmo quando ele compartilha a cor de outro participante.

## Vitória e derrota

- Estrelas de vitória são armazenadas e consultadas por slot.
- Controle de construções de vitória é contado por `ConstructionManager.SlotIndex`.
- O vencedor possui `victoryWinnerSlotIndex`; `victoryWinnerTeam` permanece como informação visual e ponte de compatibilidade.
- Captura de QG distingue proprietário anterior e capturador pelos respectivos slots.
- Derrota por zero unidades conta apenas as unidades do slot afetado.
- Rendição, último sobrevivente e vitória por eliminação identificam o participante pelo slot.
- Somente as construções pertencentes ao slot derrotado são neutralizadas.

## HUD e IA

- O painel de dinheiro acompanha o saldo do slot ativo.
- O progresso e as estrelas exibidos pertencem ao slot ativo.
- O resumo de status consulta o tesouro do slot ativo.
- O snapshot da IA recebe orçamento, renda, unidades e construções próprias pelo slot resolvido.

## Save e compatibilidade

- O formato de save foi atualizado para a versão `11`.
- Saves novos persistem:
  - slot vencedor;
  - estrelas por slot;
  - histórico de construções capturadas por slot.
- Campos visuais de `TeamId` foram mantidos para apresentação e compatibilidade.
- Saves antigos tentam migrar dados baseados em cor somente quando a associação para um slot é inequívoca.

## Contrato transacional

Débitos, compras, captura, derrota e vitória continuam sendo aplicados apenas nos pontos confirmados de compromisso da ação. As alterações desta versão não transformam previews ou estados intermediários em estado definitivo.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.

## Próximas etapas

- Migrar FOW, detecção confirmada e memória por slot.
- Migrar integralmente snapshots, planners e Stages da IA.
- Revisar replay e demais estruturas legadas ainda indexadas por `TeamId`.
- Reexecutar o stress test com dois slots vermelhos independentes.
