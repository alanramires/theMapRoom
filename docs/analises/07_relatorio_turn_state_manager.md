# Relatorio do TurnStateManager

Data base: 2026-05-25 (revisado; base original: 2026-03-06)

## Visao geral
`TurnStateManager` e a espinha de execucao tatico-operacional por estado de cursor.
O mapa canonico dos estados esta em `docs/turnState.md`; este relatorio resume o que importa para leitura tecnica.

## Estados atuais (enum CursorState)
O enum cresceu de 17 para 34 valores. A expansao principal foi a introducao do padrao "Executing" e de estados globais/sistemicos.

### Estados de acao do jogador (existiam antes)
| # | Estado | O que representa |
|---|--------|-----------------|
| 0 | `Neutral` | Nenhuma unidade selecionada |
| 1 | `UnitSelected` | Unidade selecionada, aguardando destino |
| 2 | `MoveuAndando` | Confirmando movimento (unidade se moveu) |
| 3 | `MoveuParado` | Confirmando acao sem movimento |
| 4 | `Capturando` | Executando captura de construcao |
| 5 | `Mirando` | Selecionando alvo de ataque |
| 6 | `Pousando` | Selecionando local de pouso |
| 7 | `Embarcando` | Embarcando passageiro |
| 8 | `Desembarcando` | Desembarcando passageiro |
| 9 | `Fundindo` | Fundindo unidades |
| 10 | `ShoppingAndServices` | Loja de construcao |
| 11 | `Suprindo` | Suprindo unidade adjacente |
| 12 | `InspectingUnit` | Inspecionando ficha de unidade |
| 13 | `InspectingBuilding` | Inspecionando construcao |
| 14 | `InspectingHotZone` | Inspecionando zona de interesse |
| 15 | `CommandService` | Servico do comando (batch logistico) |
| 16 | `RemovingUnit` | Confirmando remocao de unidade |

### Estados novos — globais / sistemicos
| # | Estado | O que representa |
|---|--------|-----------------|
| 17 | `Planning` | Modo planejamento (`planningManager`) — jogador traca destinos antes de confirmar |
| 18 | `PlayerMenu` | Menu do jogador (pausa/opcoes) |
| 19 | `AircraftFuelDepletionQueue` | Fila automatica de pouso forcado por combustivel zerado |
| 20 | `TurnStartRallyQueue` | Fila de reagrupamento/eventos no inicio do turno |
| 21 | `Replay` | Modo replay ativo |
| 24 | `EndingTurn` | Confirmando fim de turno |
| 26 | `Saving` | Salvando partida (input bloqueado) |
| 27 | `Loading` | Carregando partida (input bloqueado) |

### Estados novos — "Executing" (lock-out de input)
Padrao arquitetural novo: cada acao de execucao assincrona ganhou um estado `*Executing` que bloqueia todo input enquanto a animacao / corrotina termina.

| # | Estado | Acao correspondente |
|---|--------|---------------------|
| 22 | `CommandServiceExecuting` | `CommandService` |
| 23 | `RemovingUnitExecuting` | `RemovingUnit` |
| 25 | `EndingTurnExecuting` | `EndingTurn` |
| 28 | `CapturandoExecuting` | `Capturando` |
| 29 | `SuprindoExecuting` | `Suprindo` |
| 30 | `FundindoExecuting` | `Fundindo` |
| 31 | `DesembarcandoExecuting` | `Desembarcando` |
| 32 | `EmbarcandoExecuting` | `Embarcando` |
| 33 | `AttackingExecuting` | `Mirando` (ataque em execucao) |

`HandleConfirm()` retorna `ActionSfx.None` para todos os estados `*Executing` — input e ignorado ate a corrotina concluir.

## Partials novos em TurnStateManager
Desde a revisao anterior, novos arquivos foram adicionados:

| Arquivo | Responsabilidade |
|---------|-----------------|
| `StateStackGuards.cs` | Guardrails e validacoes de transicao de estado |
| `StateStackHooks.cs` | Hooks executados em entradas/saidas de estados |
| `Automation.cs` | Execucao automatizada de acoes (IA/replay) — `HandleAutomatedMoveOnlyActionRequested` |
| `Planning.cs` | Modo planejamento (`TryTogglePlanningModeByHotkey`, `TurnStartRallyQueue`) |
| `HelperPanel.cs` | Painel de ajuda contextual por estado |
| `Transfer.cs` | Execucao da acao de transferencia de estoque (sem CursorState proprio — usa fluxo de `Suprindo`/`CommandService`) |
| `Range.cs` | Exibicao de alcance de armas no mapa |
| `LineOfFire.cs` | Exibicao de linha de fogo/visao |
| `PathVisual.cs` | Visualizacao de caminho de movimento |
| `ReplayRecording.cs` | Gravacao de replay durante a partida |
| `Hex.cs` | Utilitarios de celula hex para o estado de cursor |

## Eventos estaticos expostos por TurnStateManager
| Evento | Quando dispara |
|--------|---------------|
| `OnSensorsReady` | Sensores foram recalculados |
| `OnUnitPurchased` | Unidade comprada em construcao |
| `OnUnitInspected` | Inspecao de unidade iniciada |
| `OnConstructionInspected` | Inspecao de construcao iniciada |
| `OnAttackResolved` | Combate resolvido (atacante, defensor) |
| `OnUnitRevealedFromFog` | Unidade inimiga revelada do FoW |
| `OnUnitDestroyed` | Unidade eliminada |
| `OnUnitMovementExecuted` | Movimento de unidade concluido |
| `OnUnitSelected` | Unidade selecionada pelo cursor |
| `OnUnitEmbarked` | Embarque concluido |
| `OnUnitDisembarked` | Desembarque concluido |
| `OnUnitSupplied` | Supply concluido (fornecedor, alvo) |

## Ordem operacional tipica de uma acao de unidade
1. `Neutral` → selecionar unidade aliada (`UnitSelected`)
2. confirmar movimento → `MoveuAndando` ou `MoveuParado`
3. `RefreshSensorsForCurrentState()` calcula acoes possiveis
4. jogador escolhe sensor / acao e entra no estado correspondente
5. confirmacao entra no estado `*Executing` correspondente
6. ao terminar a corrotina, estado volta para `Neutral`

## Fluxos importantes
- Combate: `PodeMirarSensor` → `Mirando` → `AttackingExecuting` → `TurnStateManager.Combat.cs`
- Suprimento / reparo: `Suprindo` → `SuprindoExecuting` → `TurnStateManager.Supply.cs`
- Servico do comando: `CommandService` → `CommandServiceExecuting` → `TurnStateManager.CommandService.cs`
- Compra em construcao: `ShoppingAndServices` → `TurnStateManager.ConstructionShopping.cs`
- Captura: `Capturando` → `CapturandoExecuting` → `TurnStateManager.Capture.cs`
- Desembarque: `Desembarcando` → `DesembarcandoExecuting` → `TurnStateManager.Disembark.cs`
- Planejamento: `Neutral` → `Planning` → `Neutral` (via `TryTogglePlanningModeByHotkey`)
- Fim de turno: `EndingTurn` → `EndingTurnExecuting` → `Neutral` (proximo time)
- Combustivel zerado: `AircraftFuelDepletionQueue` — pouso forcado automatico ao inicio do turno
- Rally de turno: `TurnStartRallyQueue` — fila de eventos/animacoes no inicio do turno

## Adendo de fusao (Merge): contribuicao proporcional por HP
A fusao nao soma recursos "secos"; ela pondera contribuicao por HP de cada participante.

No runtime (`TurnStateManager.Merge.cs`):
- `baseSteps = HP_base * fuel_base`
- `participantsSteps += HP_i * fuel_i`
- `resultHp = min(10, soma HPs)`
- `resultFuel = totalSteps / resultHp` (divisao inteira)

Efeito pratico:
- a unidade resultante pode sair com autonomia / estado degradado quando um membro com HP baixo entra na fusao.
- isso e intencional para evitar exploit de recarga gratis apenas por juntar cascas.

Observacao: ammo / suprimentos embarcados tambem sao agregados por logica proporcional / slots, com descarte quando faltam slots de destino.

## Servicos automaticos
- Nao ha tick automatico de reparo / abastecimento por turno em massa sem acao.
- O que existe:
- execucao por comando do jogador em `Suprindo`
- execucao em lote via `ServicoDoComando` com confirmacao
- upkeep automatico de turno e economia ocorre no `MatchController`
- `AircraftFuelDepletionQueue`: pouso forcado automatico quando aeronave fica sem combustivel no inicio do turno

## Pouso, decolagem e camada
Para unidades aereas, o fluxo de servico pode forcar transicao de camada quando necessario:
- `forceLandBeforeSupply`
- `forceTakeoffBeforeSupply`
- `forceSurfaceBeforeSupply`

Isso aparece em `TurnStateManager.CommandService` e `TurnStateManager.SupplyQueue` como parte da execucao da ordem, nao como um `CursorState` novo.

Ponto de leitura importante:
- `Decolando` nao existe como `CursorState`.
- a preparacao de decolagem e a resolucao de pouso / camada sao tratadas por regras auxiliares e por `ScannerPromptStep`.

## Integracao com MatchController
- `MatchController` governa turno / economia macro (`AdvanceTurn`, renda por turno, time ativo).
- `TurnStateManager` governa o microfluxo tactico da unidade dentro do turno ativo.

## Resumo
`TurnStateManager` implementa uma state machine de acao por unidade, com sensores como gateway de validacao e com executores especializados por dominio. O padrao `*Executing` introduzido separa claramente "decisao do jogador" de "execucao da engine" — nenhuma entrada e aceita durante a execucao de uma acao assincrona.
