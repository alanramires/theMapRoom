# Relatorio de Atualizacao - v1.2.3

## Em uma frase
A v1.2.3 consolida a transferencia logistica (Tools + comando `T`), adiciona hotzone global por time (`Z`) e amplia a inspecao tatico-operacional de unidades e construcoes.

## O que isso trouxe na pratica
- Comando `T` passou a seguir o mesmo comportamento do fluxo em Tools, com selecao de candidato, relatorio final e confirmacao por `Enter`/retorno por `Esc`.
- Transferencia agora diferencia claramente `Doar` e `Receber`, respeitando tipo de fornecedor (`Hub`/`Receiver`), infinito/finito e regras de alcance/embarcado.
- Painel helper exibe detalhes operacionais de transferencia e inspecao (estoques, papel logistico, origem/destino e previsao de movimentacao).
- Hotzone (`Z`) passou a funcionar como camada de ameaca por time, com troca de time inspecionado, abertura/fechamento por atalho e encerramento por estados de turno.
- Inspecao de ameaca por unidade evoluiu para cobrir tres perfis: combate por movimento, longo alcance e hibrido, com prioridade visual adequada entre ameaca e movimento.
- Novo sensor de medicao em `Tools > Sensor > Medir` para tracar linha entre dois hexes e mostrar distancia.

## Principais melhorias
1. Transferencia logistica unificada
- Inclusao de `Transferir: Receber` e `Transferir: Doar` no fluxo operacional.
- Regras de disponibilidade por tipo de alvo:
- `Hub infinito`: fluxo focado em receber da fonte urbana ate completar embarcado.
- `Hub finito`: permite receber e doar.
- `Receiver`: apenas doar para ele.
- Casos invalidos (receiver->receiver) bloqueados.
- Transferencia embarcada respeita excecoes de dominio para unidades embarcadas quando aplicavel.

2. Comando `T` alinhado ao Tools
- Selecao de candidato valida com lista numerada/helper quando houver mais de uma opcao.
- Ao escolher a opcao, fluxo vai direto para tela final de confirmacao (sem subetapa redundante).
- Relatorio final mostra previsao de estoques no destino e, quando aplicavel, saida do fornecedor finito.
- Execucao com animacao de transferencia usando sprites de supplies.

3. Dados e editor de supplier tier
- Remocao de `Self Supplier` nas configuracoes de `Supplier Tier` em construction data e unit data.
- Evolucao da regra de infinito em construction data para flag explicita (`has infinite supply`) no lugar da convencao por `maxCapacity = -1`.

4. Ferramentas de inspecao e helper
- Clique em construcao passou a exibir no helper: dono atual, estoques e tipo de servico logistico ofertado.
- Duracao de exibicao de inspecao parametrizada no Animation Manager (construction/unit inspect duration).
- Mensagens de hotzone/sensores separadas em helper data/dialog data para facilitar edicao de texto sem hardcode.

5. Ameaca tatico-operacional (inspect/hotzone)
- Unidades de movimento (`min=max=1`) exibem area de movimento valida + ameaca nas bordas.
- Unidades de alcance (`max>1`) exibem ameaca por linha de tiro.
- Unidades hibridas (`min=1`, `max>1`) combinam as duas logicas em camadas (ameaca/movimento/ameaca), com prioridade visual da ameaca no overlap.
- Validacoes de dominio, LoS e spotting foram integradas ao calculo de ameaca conforme regras de combate.

6. Hotzone global por time (`Z`)
- Atalho alterado para `Z` (antes `L`) com comportamento toggle (abre/fecha pelo proprio atalho).
- Disponivel somente com cursor em estado neutro.
- Default prioriza time rival do turno atual; fallback para proprio time quando nao houver inimigos validos.
- Troca de time inspecionado por teclas numericas (1-9).
- Encerra ao sair do neutral, ao trocar rodada (`R`), ao abrir servicos de comando (`X`) e por `Esc`.

7. Performance: cache de ameaca
- Implementado `ThreatRevisionTracker` central com revisores globais:
- `GlobalBoardRevision`
- `TeamObserverRevision[teamId]`
- `MatchFlagsHash`
- Cache de hotzone por `unitInstanceId` com chave composta:
- `UnitSnapshotHash + GlobalBoardRevision + TeamObserverRevision[teamId] + MatchFlagsHash`
- Hit/miss de cache com metricas em debug log (por unidade e acumulado da sessao).
- Warm-up de cache no load de save via corrotina distribuida por frames para evitar travamento no loading.

## Regras importantes
- Hotzone nao deve usar textos de sensor comum (`PHM Sensor Line`); possui title/linhas proprias de hotzone.
- Inspecao aliada de unidade que ja agiu continua sem overlays de ameaca/movimento, mantendo helper basico.
- Unidades com `collection range = embarked only` nao exibem linha externa de transferencia.
- Mudancas de estado que afetam ameaca devem atualizar revisores no `ThreatRevisionTracker`.

## Bloco tecnico curto
- Scripts-chave: `TurnStateManager.HelperPanel`, `TurnStateManager.Transfer`, `TurnStateManager.Sensors`, `TurnStateManager.StateMachine`, `TurnStateManager.CommandService`, `ConstructionManager`, `UnitManager`, `MatchController`, `SaveGameManager`, `PanelHelperController`.
- Dados-chave: `Helper Database`, `Dialog Database`, novos assets de hotzone em `Assets/DB/Dialog/Dialog Data/Hotzone` e `Assets/DB/Dialog/Helper Data/Hotzone`.
- Editor/tools: `SensorMeasureWindow`, ajustes no debug de `PodeTransferir`.

## Resultado
A v1.2.3 entrega um salto grande de jogabilidade e leitura tatico-logistica: transferencia mais previsivel, inspecoes mais ricas, hotzone global de ameaca por time e base pronta para escalar performance com cache e warm-up.
