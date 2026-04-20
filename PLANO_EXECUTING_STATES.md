# Plano: Estados ___Executing para Sensores Principais

## Objetivo

Padronizar todos os sensores táticos com o padrão `X → XExecuting → Neutral`,
tornando a FSM a única fonte de verdade para AI e ReplayManager saberem
quando uma ação terminou.

## Motivação

AI e ReplayManager precisam aguardar o retorno ao Neutral antes de emitir
o próximo comando. Sem estados `Executing` explícitos, precisam checar
uma coleção de flags internas espalhadas pelos partials:

```csharp
// hoje — frágil, acoplado a implementação interna
embarkExecutionInProgress ||
mergeExecutionInProgress  ||
IsMovementAnimationRunning() || ...
```

Com estados `Executing`, a condição vira uma só:

```csharp
// depois — robusto, usa só a FSM
CurrentCursorState == CursorState.Neutral
// ou: aguardar evento OnCursorReturnedToNeutral
```

---

## Referências já implementadas (padrão canônico)

Estes já funcionam corretamente e servem de modelo:

| Preview | Executing | Arquivo |
|---------|-----------|---------|
| `CommandService` | `CommandServiceExecuting` | `TurnStateManager.CommandService.cs` |
| `RemovingUnit` | `RemovingUnitExecuting` | `TurnStateManager.ScannerPrompt.cs` |
| `EndingTurn` | `EndingTurnExecuting` | `TurnStateManager.StateMachine.cs` |

Fluxo canônico:

```
Neutral
  Advance(X)
  X                      ← preview / confirmação / input do jogador
    Advance(XExecuting)
    XExecuting           ← animação em curso, input bloqueado
    ExecuteAndReset()
Neutral
```

Cancelamento antes da execução:

```
X
  Retreat()
Neutral  (ou estado anterior na pilha)
```

---

## Sensores a padronizar

### Passo 6a — Capturando → CapturandoExecuting

**Arquivo:** `TurnStateManager.Capture.cs`

**Flag atual:** `captureExecutionInProgress`

**Fluxo atual:**
```
MoveuAndando/Parado → Capturando → (captureExecutionInProgress=true) → Neutral
```

**Fluxo alvo:**
```
MoveuAndando/Parado → Capturando → CapturandoExecuting → Neutral
```

**Mudanças:**
1. Adicionar `CapturandoExecuting = 28` ao enum `CursorState`
2. No início da coroutine de execução: `Advance(CapturandoExecuting, "ExecuteCapture: begin")`
3. Ao finalizar: `ExecuteAndReset("ExecuteCapture: done")`
4. `IsBoardCursorMovementLockedByCurrentState()`: adicionar `CapturandoExecuting`

---

### Passo 6b — Embarcando → EmbarcandoExecuting

**Arquivo:** `TurnStateManager.ScannerPrompt.cs`

**Flag atual:** `embarkExecutionInProgress`

**Fluxo atual:**
```
MoveuAndando/Parado → Embarcando → (embarkExecutionInProgress=true) → Neutral
```

**Fluxo alvo:**
```
MoveuAndando/Parado → Embarcando → EmbarcandoExecuting → Neutral
```

**Mudanças:**
1. Adicionar `EmbarcandoExecuting = 29`
2. Início da coroutine de embark: `Advance(EmbarcandoExecuting, ...)`
3. Ao finalizar: `ExecuteAndReset(...)`
4. `IsBoardCursorMovementLockedByCurrentState()`: adicionar `EmbarcandoExecuting`

---

### Passo 6c — Desembarcando → DesembarcandoExecuting

**Arquivo:** `TurnStateManager.Disembark.cs`

**Flag atual:** `disembarkExecutionInProgress`

**Fluxo alvo:**
```
MoveuAndando/Parado → Desembarcando → DesembarcandoExecuting → Neutral
```

**Mudanças:**
1. Adicionar `DesembarcandoExecuting = 30`
2. Mesma estrutura dos anteriores

---

### Passo 6d — Fundindo → FundindoExecuting

**Arquivo:** `TurnStateManager.Merge.cs`

**Flag atual:** `mergeExecutionInProgress`

**Fluxo alvo:**
```
MoveuAndando/Parado → Fundindo → FundindoExecuting → Neutral
```

**Mudanças:**
1. Adicionar `FundindoExecuting = 31`
2. Mesma estrutura dos anteriores

---

### Passo 6e — Suprindo → SuprindoExecuting

**Arquivo:** `TurnStateManager.SupplyQueue.cs`

**Flag atual:** `supplyExecutionInProgress`

**Fluxo alvo:**
```
MoveuAndando/Parado → Suprindo → SuprindoExecuting → Neutral
```

**Mudanças:**
1. Adicionar `SuprindoExecuting = 32`
2. Mesma estrutura dos anteriores

---

### Passo 6f — Mirando → MirandoExecuting (combate)

**Arquivo:** `TurnStateManager.ScannerPrompt.cs`

**Flag atual:** `combatExecutionInProgress`

**Fluxo alvo:**
```
MoveuAndando/Parado → Mirando → MirandoExecuting → Neutral
```

**Mudanças:**
1. Adicionar `MirandoExecuting = 33`
2. No momento em que o jogador confirma o alvo e a coroutine de combate inicia:
   `Advance(MirandoExecuting, "ExecuteCombat: begin")`
3. Ao finalizar combate: `ExecuteAndReset("ExecuteCombat: done")`
4. `IsBoardCursorMovementLockedByCurrentState()`: adicionar `MirandoExecuting`

---

### Passo 6g — Movimento → MovimentoExecuting (animação)

**Arquivo:** `TurnStateManager.Movement.cs`

**Caso especial** — o mais complexo. Não há um state de "preview" antes da
animação; o jogador seleciona a célula e a animação começa imediatamente.

**Fluxo atual:**
```
UnitSelected → (animação roda) → MoveuAndando / MoveuParado
```

**Fluxo alvo:**
```
UnitSelected → MovimentoExecuting → MoveuAndando / MoveuParado
```

**Onde entrar:** em `BeginMovementToSelectedCell`, logo antes de iniciar a
animação: `Advance(MovimentoExecuting, "Movement: animation begin")`

**Onde sair:** em `HandleMovementAnimationCompleted`, substituir a transição
direta para `MoveuAndando/Parado` por `Retreat()` + `Advance(resolvedState)`.

**Mudanças:**
1. Adicionar `MovimentoExecuting = 34`
2. `IsBoardCursorMovementLockedByCurrentState()`: adicionar `MovimentoExecuting`

> Nota: `MovimentoExecuting` bloqueia inputs mas **não** retorna ao Neutral —
> transita para `MoveuAndando/Parado`. É o único `Executing` com saída lateral.
> AI/Replay devem aguardar sair de `MovimentoExecuting`, não necessariamente
> chegar ao Neutral.

---

## Resumo dos novos valores no enum

```csharp
CapturandoExecuting    = 28,
EmbarcandoExecuting    = 29,
DesembarcandoExecuting = 30,
FundindoExecuting      = 31,
SuprindoExecuting      = 32,
MirandoExecuting       = 33,
MovimentoExecuting     = 34,
```

---

## Impacto em AI e ReplayManager

Após implementação, o contrato de espera unifica:

```csharp
// ReplayManager — aguardar qualquer ação terminar
yield return WaitUntil(() => turnStateManager.CurrentCursorState == CursorState.Neutral);

// AI — verificar se pode emitir próximo comando
bool canAct = turnStateManager.CurrentCursorState == CursorState.Neutral;
```

Exceção: `MovimentoExecuting` transita para `MoveuAndando/Parado`, não Neutral.
AI/Replay que aguardam pós-movimento devem esperar sair de `MovimentoExecuting`:

```csharp
yield return WaitUntil(() =>
    turnStateManager.CurrentCursorState != CursorState.MovimentoExecuting);
```

---

## Ordem recomendada de execução

Implementar na ordem de complexidade crescente:

1. `CapturandoExecuting` — fluxo mais simples, sem sub-estados
2. `SuprindoExecuting` — similar
3. `FundindoExecuting` — similar
4. `DesembarcandoExecuting` — tem fila de ordens
5. `EmbarcandoExecuting` — tem animação de preview
6. `MirandoExecuting` — combate tem casos de morte, contadores
7. `MovimentoExecuting` — mais complexo, saída lateral para MoveuAndando/Parado
