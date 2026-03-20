# Relatorio do TurnStateManager

## Visao geral
`TurnStateManager` e a espinha de execucao tatico-operacional por estado de cursor.
O mapa canonico dos estados esta em `docs/turnState.md`; este relatorio resume o que importa para leitura tecnica.

## Estados atuais
Enum de fases (`CursorState`):
1. Neutral
2. UnitSelected
3. MoveuAndando
4. MoveuParado
5. Capturando
6. Mirando
7. Pousando
8. Embarcando
9. Desembarcando
10. Fundindo
11. ShoppingAndServices
12. Suprindo
13. InspectingUnit
14. InspectingBuilding
15. InspectingHotZone
16. CommandService
17. RemovingUnit

## Ordem operacional tipica de uma acao de unidade
1. `Neutral` -> selecionar unidade aliada (`UnitSelected`)
2. confirmar movimento -> `MoveuAndando` ou `MoveuParado`
3. `RefreshSensorsForCurrentState()` calcula acoes possiveis
4. jogador escolhe sensor / acao e entra no estado correspondente
5. ao finalizar a acao, a unidade volta para `Neutral` ou para o estado de origem apropriado

## Fluxos importantes
- Combate: `PodeMirarSensor` -> `Mirando` -> `TurnStateManager.Combat.cs`
- Suprimento / reparo: `Suprindo` -> `TurnStateManager.Supply.cs` / `TurnStateManager.SupplyQueue.cs`
- Servico do comando: `CommandService` -> `TurnStateManager.CommandService.cs`
- Compra em construcao: `ShoppingAndServices` -> `TurnStateManager.ConstructionShopping.cs`
- Inspecao: `InspectingUnit`, `InspectingBuilding`, `InspectingHotZone`
- Remocao de unidade: `RemovingUnit`

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
`TurnStateManager` implementa uma state machine de acao por unidade, com sensores como gateway de validacao e com executores especializados por dominio. Os estados que antes eram descritos como inline em docs antigos hoje estao formalizados como `CursorState` proprios.
