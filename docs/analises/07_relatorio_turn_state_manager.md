# Relatorio do TurnStateManager

## Visao geral
`TurnStateManager` e a espinha de execucao tatico-operacional por estado de cursor.

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

## Ordem operacional tipica de uma acao de unidade
1. `Neutral` -> selecionar unidade aliada (`UnitSelected`)
2. confirmar movimento -> `MoveuAndando` (ou `MoveuParado` se nao deslocou)
3. `RefreshSensorsForCurrentState()` calcula acoes possiveis (A/E/D/C/F/S/T/L)
4. jogador escolhe sensor/acao e entra no estado correspondente
5. ao finalizar acao, unidade marca `HasActed` e fluxo volta para `Neutral`

## Onde ocorre combate
- Sensor de alvo: `PodeMirarSensor`
- Confirmacao/execucao: estado `Mirando`
- Formula final: `TurnStateManager.Combat.cs` (`ResolveCombatFromSelectedOption`)

## Onde ocorre suprimento e reparos
- Fluxo de pode suprir: estado `Suprindo`
- Runtime de aplicacao: `TurnStateManager.Supply.cs`
- Execucao em lote/encadeada: `TurnStateManager.SupplyQueue.cs`
- Servico do comando (batch): `TurnStateManager.CommandService.cs`

## Adendo de fusao (Merge): contribuicao proporcional por HP
A fusao nao soma recursos "secos"; ela pondera contribuicao por HP de cada participante.

No runtime (`TurnStateManager.Merge.cs`):
- `baseSteps = HP_base * fuel_base`
- `participantsSteps += HP_i * fuel_i`
- `resultHp = min(10, soma HPs)`
- `resultFuel = totalSteps / resultHp` (divisao inteira)

Efeito pratico:
- a unidade resultante pode sair com autonomia/estado "degradado" quando um membro com HP baixo entra na fusao.
- isso e intencional para evitar exploit de "recarga gratis" so por juntar cascas.

Exemplo simples:
- Unidade A: `HP8`, `Fuel70` -> `560 steps`
- Unidade B: `HP2`, `Fuel10` -> `20 steps`
- Soma HP = `10`, total steps = `580`
- Resultado: `HP10`, `Fuel58` (e nao 70)

Observacao: ammo/suprimentos embarcados tambem sao agregados por logica proporcional/slots, com descarte quando faltam slots de destino.

## Servicos automaticos
- Nao ha "tick automatico" de reparo/abastecimento por turno em massa sem acao.
- O que existe:
- execucao por comando do jogador em `Suprindo`
- execucao em lote via `ServicoDoComando` com confirmacao
- upkeep automatico de turno e economia ocorre no `MatchController` (renda, reset/rotina de inicio de turno), nao em auto-supply global.

Adendo de integracao futura (QoL):
- A ideia de `ServicoDoComando` automatico via flag on/off no `MatchController` e coerente com o desenho atual.
- Estado atual do codigo: ainda nao existe essa flag de automacao de command service no `MatchController`; hoje a execucao segue via acao/sensor com confirmacao.
- Recomendacao: manter modo manual como baseline e expor automacao opcional para reduzir microgerenciamento sem remover controle de quem prefere operar no detalhe.

## Adendo de pouso/suprimento: decisao intencional de balanceamento
Para unidades aereas, o fluxo de suprimento valida dominio/camada e pode forcar transicao antes de atender:
- `forceLandBeforeSupply`
- `forceTakeoffBeforeSupply`
- `forceSurfaceBeforeSupply` (caso naval/sub)

Isso aparece em `PodeSuprirSensor` e `ServicoDoComandoSensor` (validacao de dominio com `PodePousar`/`PodeDecolar` e checagem de camada permitida no hex atual).

Decisao de design:
- forcar pouso/decolagem/surface antes do servico quando necessario e **intencional**.
- objetivo: evitar abastecimento aereo adjacente "gratuito" fora do contexto de operacao e manter custo/risco logistico relevante.
- documentar isso evita que manutencao futura trate o comportamento como bug e "corrija" sem querer o balance.

## Integracao com MatchController
- `MatchController` governa turno/economia macro (`AdvanceTurn`, renda por turno, time ativo).
- `TurnStateManager` governa microfluxo tatico da unidade dentro do turno ativo.

## Resumo
`TurnStateManager` implementa uma state machine de acao por unidade, com sensores como gateway de validacao e com executores especializados por dominio (combate, merge, supply, command service, etc.).
