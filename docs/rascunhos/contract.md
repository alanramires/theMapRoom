# Contract - Neutral First / Neutral Last

Este documento define o contrato operacional de estado para acoes de gameplay e replay.

## Objetivo

Toda acao deve:
1. iniciar em `CursorState.Neutral` (ou sair explicitamente de um estado valido com confirmacao);
2. executar seu fluxo completo (selecao, confirmacao, animacao, efeitos);
3. terminar em `CursorState.Neutral` antes de:
- gravar batch (gameplay), ou
- avancar para o proximo batch (replay).

Resumo: **Neutral -> Acao -> Neutral**.

## Fonte usada nesta revisao

- `docs/sensors.md`
- `docs/sensor (atalho).md`
- `docs/turnState.md`
- `Assets/Scripts/Match/TurnStateManager.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Automation.cs`
- `Assets/Scripts/Replay/ReplayManager.cs`
- `Assets/Scripts/UI/Replay/ReplayPanelUI.cs`

## Contrato por classe de fluxo

### 1) UnitAction (A/E/D/C/F/S/T/L/M)

Contrato:
- entra por `UnitSelected` / `Moveu*`;
- confirma acao;
- executa substeps e animacoes;
- volta a `Neutral`;
- grava/avanca batch somente apos `Neutral`.

Estado hoje: **Parcialmente aderente**.
- Listener de neutral existe e eh usado no replay.
- Ainda depende de consistencia de cada subfluxo especifico para nao liberar neutral cedo.

### 2) Shopping

Contrato:
- abrir menu de compra;
- navegar indice/confirmar;
- aplicar spawn/custos;
- voltar a `Neutral`;
- so entao concluir batch.

Estado hoje: **Aderente com fallback**.
- Replay navega por indice e confirma; possui fallback em caso de mismatch.

### 3) CommandService

Contrato:
- iniciar em `Neutral`;
- entrar em `CommandService` (estado de confirmacao);
- confirmar;
- executar fila;
- voltar a `Neutral`;
- concluir batch somente no fim.

Estado hoje: **Parcialmente aderente**.
- Fluxo foi endurecido no replay para forcar entrada no estado de confirmacao.
- Ainda requer validacao em cenario longo para confirmar ausencia de "passinho" residual.

### 4) RemoveUnit (manual)

Contrato esperado:
- `Neutral`;
- comando de destruir unidade (`U` no fluxo atual);
- entrar em `RemovingUnit`;
- perguntar confirmacao;
- confirmar;
- tocar animacao;
- voltar a `Neutral`;
- so entao gravar/concluir batch.

Estado hoje: **Aderente (apos ajuste recente)**.
- Confirmacao agora inicia coroutine de execucao;
- gravacao ocorre apos apresentacao de morte;
- neutralizacao ocorre no final do fluxo.

### 5) TurnStartFuelDepletionQueue (aeronaves caindo)

Contrato:
- fila executa sob estado dedicado (`AircraftFuelDepletionQueue`);
- finalizar fila;
- retornar para `Neutral`;
- somente depois permitir operacoes de persistencia.

Estado hoje: **Aderente com bloqueio de persistencia**.
- Save/Load bloqueados enquanto fila esta em execucao.

## Checklist rapido de conformidade

- [x] Replay nao deve avancar batch sem retorno a neutral.
- [x] Save/Load bloqueados durante replay ativo.
- [x] Save/Load bloqueados durante fila de aeronaves caindo em runtime.
- [x] RemoveUnit manual com confirmacao + animacao antes de neutral final.
- [ ] Telemetria global de contrato (START/END por actionId) ainda nao implementada.

## Gaps observados

1. `docs/turnState.md` esta desatualizado na lista de estados (ja existe `AircraftFuelDepletionQueue`).
2. Ainda falta auditoria unica de contrato para todos os fluxos (logger central de transicoes).
3. Alguns fluxos legacy podem disparar neutral cedo e continuar animacao; precisa validacao por telemetria.

## Criterio de aceite do contrato

Um fluxo so eh considerado valido quando todos os itens abaixo sao verdadeiros:
1. nao avanca batch em replay antes de `Neutral`;
2. nao grava batch em gameplay antes de `Neutral`;
3. qualquer erro aborta de forma graciosa sem quebrar FSM;
4. apos erro, controles de replay e cursor continuam operacionais.
