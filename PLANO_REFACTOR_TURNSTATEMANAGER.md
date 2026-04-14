# Plano de Refactor: TurnStateManager — Stack de Estados

## Objetivo

Substituir o campo `cursorState` (campo único, sem histórico) por uma
`Stack<CursorState>` no `TurnStateManager`, adotando o padrão `Advance` /
`Retreat` / `ExecuteAndReset` já implementado corretamente em:

```
D:\Unity Projects\A Sala de Mapas\Assets\Scripts\TurnStateManager.cs
```

Leia esse arquivo primeiro. É a referência canônica e tem ~80 linhas.

---

## Por que fazer isso

O problema atual: cada `HandleCancelWhileXxx()` sabe hardcoded para qual estado
anterior voltar. Se o fluxo muda, quebra. Com pilha, `Retreat()` sabe
automaticamente onde voltar — o estado anterior está na pilha.

Exemplo concreto:
- Atual: `ExitMirandoStateToMovement()` recebe `targetMovementState` como
  parâmetro porque precisa saber se era `MoveuAndando` ou `MoveuParado`.
- Com pilha: `Retreat()` já sabe, porque `MoveuAndando/Parado` foi empilhado
  antes de `Mirando`.

---

## Arquitetura da pilha (referência de A Sala de Mapas)

```csharp
private readonly Stack<CursorState> stateStack = new();

public CursorState CurrentCursorState => stateStack.Peek();

// Avança para novo estado (empilha)
public void Advance(CursorState nextState) { ... }

// Cancela / volta ao anterior (desempilha)
public void Retreat() { ... }

// Ação executada com sucesso — limpa tudo, volta ao Neutral
public void ExecuteAndReset() { ... }
```

---

## Fluxo da pilha em tempo de execução

```
Neutral                       ← base da pilha (sempre presente)
  Advance(UnitSelected)
  UnitSelected
    Advance(MoveuAndando)
    MoveuAndando
      Advance(Mirando)
      Mirando
        Retreat()             ← volta para MoveuAndando automaticamente
      Retreat()               ← volta para UnitSelected
    Retreat()                 ← volta para Neutral (ou ExecuteAndReset se confirmou)
```

---

## Passo 1 — Modificar `TurnStateManager.cs` (arquivo principal)

**Arquivo:** `Assets/Scripts/Match/TurnStateManager.cs`

### 1a. Substituir o campo `cursorState`

REMOVER:
```csharp
[SerializeField] private CursorState cursorState = CursorState.Neutral;
```

ADICIONAR (como campo privado, não serializado):
```csharp
private readonly Stack<CursorState> stateStack = new();
```

### 1b. Atualizar a propriedade pública

REMOVER:
```csharp
public CursorState CurrentCursorState => cursorState;
```

ADICIONAR:
```csharp
public CursorState CurrentCursorState => stateStack.Count > 0 ? stateStack.Peek() : CursorState.Neutral;
```

### 1c. Inicializar a pilha no Awake

Localizar o método `Awake()` (ou `Start()` se não houver Awake). Adicionar no
início:

```csharp
stateStack.Clear();
stateStack.Push(CursorState.Neutral);
```

### 1d. Refatorar `SetCursorState` para usar a pilha internamente

O método atual `SetCursorState(CursorState nextState, string reason, bool rollback = false)`
tem lógica de side effects importante (limpar threat overlay, notificar neutral)
que deve ser **preservada**. Apenas a atribuição `cursorState = nextState` muda.

O método passa a ser **privado e interno** — chamado pelos novos métodos públicos.
Reestruturar assim:

```csharp
// Método interno — mantém todos os side effects existentes
private void ApplyStateTransition(CursorState nextState, string reason, bool rollback = false)
{
    CursorState previous = CurrentCursorState;

    // Mantém side effects existentes: limpar threat overlay, notificar neutral
    bool shouldClearThreatOverlayOnTransition =
        nextState != CursorState.Neutral &&
        nextState != CursorState.InspectingHotZone;
    if (shouldClearThreatOverlayOnTransition)
    {
        if (scannerPromptStep == ScannerPromptStep.ThreatLayerTeamSelect)
            scannerPromptStep = ScannerPromptStep.AwaitingAction;
        ClearEnemyThreatLayersOverlay();
    }

    // Aplica o estado (a pilha já foi modificada pelo chamador)
    if (nextState == CursorState.Neutral && previous != CursorState.Neutral)
    {
        if (enableTurnStateRuntimeLogs)
            Debug.Log($"[Replay][Dispatch] OnCursorReturnedToNeutral fired previous={previous} current={nextState}");
        CursorController.NotifyCursorReturnedToNeutral();
    }
    RuntimeLog($"[FSM] Estado: {previous} -> {nextState}");

    if (!enableTurnStateRuntimeLogs) return;
    string rollbackTag = rollback ? " [roll back]" : string.Empty;
    string selectedName = selectedUnit != null ? selectedUnit.name : "(none)";
    Debug.Log($"[TurnState]{rollbackTag} transition={previous} -> {nextState} | reason={reason} | selected={selectedName}");
}
```

### 1e. Adicionar os três métodos públicos da pilha

```csharp
// Avança: empilha novo estado
private void Advance(CursorState nextState, string reason)
{
    stateStack.Push(nextState);
    ApplyStateTransition(nextState, reason, rollback: false);
}

// Recua: desempilha estado atual e volta ao anterior
private void Retreat(string reason)
{
    if (stateStack.Count > 1)
        stateStack.Pop();
    ApplyStateTransition(CurrentCursorState, reason, rollback: true);
}

// Ação confirmada: limpa pilha, volta ao Neutral
private void ExecuteAndReset(string reason)
{
    stateStack.Clear();
    stateStack.Push(CursorState.Neutral);
    ApplyStateTransition(CursorState.Neutral, reason, rollback: false);
}
```

> Nota: esses três métodos substituem `SetCursorState`. Após a migração
> completa, `SetCursorState` pode ser removido. Durante a migração, mantenha
> os dois coexistindo para não quebrar nada de uma vez.

### 1f. Atualizar `SetCursorState` para delegar à pilha (adaptador temporário)

Para não quebrar os ~60 call sites de uma vez, transformar `SetCursorState`
num adaptador que usa a pilha internamente:

```csharp
private void SetCursorState(CursorState nextState, string reason, bool rollback = false)
{
    if (rollback)
    {
        // Um único pop — preserva o comportamento original de "voltar um nível"
        if (stateStack.Count > 1) stateStack.Pop();
    }
    else
    {
        stateStack.Push(nextState);
    }
    ApplyStateTransition(CurrentCursorState, reason, rollback);
}
```

> **Por que um único pop:** o `SetCursorState(..., rollback: true)` original
> era um set direto — nunca navegava múltiplos níveis. Um pop único preserva
> esse comportamento durante a migração.
>
> Se durante a migração você encontrar um caso onde o cancel precisa voltar
> **dois níveis** (ex: ir de `Mirando` direto para `UnitSelected` pulando
> `MoveuAndando`), substitua por dois `Retreat()` explícitos — não tente
> resolver isso no adaptador.
>
> Este adaptador permite que os call sites existentes continuem funcionando
> enquanto são migrados um a um para `Advance`/`Retreat`/`ExecuteAndReset`.

---

## Passo 2 — Categorizar e migrar os call sites de `SetCursorState`

São ~60 ocorrências espalhadas pelos arquivos parciais. A tabela abaixo
categoriza cada uma. Migre de cima para baixo, arquivo por arquivo.

### Regras de classificação

| Tipo | Critério | Migração |
|------|----------|----------|
| **Advance** | Indo para estado mais profundo no fluxo; `rollback: false` (padrão) | `Advance(estado, reason)` |
| **Retreat** | Voltando ao estado anterior; `rollback: true` OU saindo de qualquer estado em direção a Neutral/movimento | `Retreat(reason)` |
| **ExecuteAndReset** | Ação concluída com sucesso; volta ao Neutral sem possibilidade de cancelar | `ExecuteAndReset(reason)` |

---

### `TurnStateManager.StateMachine.cs` (13 ocorrências)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 139 | `SetCursorState(Neutral, "normalize")` | `ExecuteAndReset("normalize")` — normaliza antes de re-processar |
| 155 | `SetCursorState(InspectingUnit, "enemy inspect")` | `Advance(InspectingUnit, ...)` |
| 164 | `SetCursorState(InspectingUnit, "acted ally inspect")` | `Advance(InspectingUnit, ...)` |
| 178 | `SetCursorState(UnitSelected, "ally selected")` | `Advance(UnitSelected, ...)` |
| 191 | `SetCursorState(InspectingBuilding, "enemy construction")` | `Advance(InspectingBuilding, ...)` |
| 202 | `SetCursorState(InspectingBuilding, "ally construction")` | `Advance(InspectingBuilding, ...)` |
| 573 | `SetCursorState(UnitSelected, ..., rollback: true)` | `Retreat(...)` |
| 584 | `SetCursorState(UnitSelected, ..., rollback: true)` | `Retreat(...)` |
| 600 | `SetCursorState(UnitSelected, ..., rollback: true)` | `Retreat(...)` |
| 810 | `SetCursorState(PlayerMenu, ...)` | `Advance(PlayerMenu, ...)` |
| 819 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` |
| 830 | `SetCursorState(Replay, ...)` | `Advance(Replay, ...)` |
| 839 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` |

---

### `TurnStateManager.cs` (arquivo principal, 3 ocorrências além do próprio método)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 908 | `SetCursorState(Neutral, "ClearSelectionAndReturnToNeutral", rollback: ...)` | `ExecuteAndReset(...)` — seleção descartada |
| 1302 | `SetCursorState(AircraftFuelDepletionQueue, "begin")` | `Advance(AircraftFuelDepletionQueue, ...)` |
| 1310 | `SetCursorState(Neutral, "completed")` | `ExecuteAndReset(...)` — fila processada |

---

### `TurnStateManager.Movement.cs` (2 ocorrências)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 15 | `SetCursorState(resolvedMovementState, ...)` | `Advance(resolvedMovementState, ...)` — entra em Moved após mover |
| 155 | `SetCursorState(onCompleteState, ..., rollback: onCompleteState == UnitSelected)` | `if rollback → Retreat(...)` else `Advance(...)` — manter lógica condicional |

---

### `TurnStateManager.Capture.cs` (1 ocorrência)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 38 | `SetCursorState(Capturando, ...)` | `Advance(Capturando, ...)` |

Além disso: onde a captura é concluída (buscar por onde `captureExecutionInProgress`
é setado para false e o estado retorna ao Neutral) → `ExecuteAndReset(...)`.

---

### `TurnStateManager.CommandService.cs` (7 ocorrências)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 91 | `SetCursorState(CommandService, ...)` | `Advance(CommandService, ...)` |
| 115 | `SetCursorState(CommandService, ...)` | `Advance(CommandService, ...)` |
| 140 | `SetCursorState(CommandService, ...)` | `Advance(CommandService, ...)` — verificar se não duplica avanço |
| 668 | `SetCursorState(Neutral, "no served targets")` | `ExecuteAndReset(...)` |
| 704 | `SetCursorState(Neutral, "completed")` | `ExecuteAndReset(...)` |
| 717 | `SetCursorState(Neutral, "cleanup")` | `ExecuteAndReset(...)` |
| 762 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` — cancelamento |

> Atenção: CommandService pode ser aberto de 3 lugares diferentes (hotkey,
> menu, ordem). Garantir que a pilha não acumule estados CommandService
> duplicados — considerar checar se já está nesse estado antes de `Advance`.

---

### `TurnStateManager.ConstructionShopping.cs` (2 ocorrências)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 40 | `SetCursorState(ShoppingAndServices, ...)` | `Advance(ShoppingAndServices, ...)` |
| 52 | `SetCursorState(Neutral, ..., rollback: rollback)` | `if rollback → Retreat(...)` else `ExecuteAndReset(...)` |

---

### `TurnStateManager.Disembark.cs` (2 ocorrências)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 118 | `SetCursorState(Desembarcando, ...)` | `Advance(Desembarcando, ...)` |
| 384 | `SetCursorState(targetMovementState, ..., rollback: true)` | `Retreat(...)` — pilha já tem MoveuAndando/Parado |

---

### `TurnStateManager.HelperPanel.cs` (2 ocorrências)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 795 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` |
| 894 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` |

---

### `TurnStateManager.Merge.cs` (2 ocorrências)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 83 | `SetCursorState(Fundindo, ...)` | `Advance(Fundindo, ...)` |
| 327 | `SetCursorState(targetMovementState, ..., rollback: true)` | `Retreat(...)` |

---

### `TurnStateManager.Planning.cs` (5 ocorrências)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 32 | `SetCursorState(Planning, ...)` | `Advance(Planning, ...)` |
| 41 | `SetCursorState(Neutral, ..., rollback: rollback)` | `if rollback → Retreat(...)` else `ExecuteAndReset(...)` |
| 92 | `SetCursorState(TurnStartRallyQueue, ...)` | `Advance(TurnStartRallyQueue, ...)` |
| 100 | `SetCursorState(Neutral, "completed")` | `ExecuteAndReset(...)` |
| 135 | `SetCursorState(UnitSelected, "planning move select")` | `Advance(UnitSelected, ...)` — caso especial, planejamento faz seleção de unidade |

---

### `TurnStateManager.ScannerPrompt.cs` (14 ocorrências)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 421 | `SetCursorState(RemovingUnit, ...)` | `Advance(RemovingUnit, ...)` |
| 463 | `SetCursorState(RemovingUnit, ...)` | `Advance(RemovingUnit, ...)` |
| 571 | `SetCursorState(Neutral, ..., rollback: logCanceled)` | `if logCanceled → Retreat(...)` else `ExecuteAndReset(...)` |
| 711 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` |
| 745 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` |
| 780 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` |
| 1003 | `SetCursorState(Pousando, ...)` | `Advance(Pousando, ...)` |
| 1043 | `SetCursorState(InspectingHotZone, ...)` | `Advance(InspectingHotZone, ...)` |
| 1065 | `SetCursorState(UnitSelected, "auto-select")` | `Advance(UnitSelected, ...)` |
| 1107 | `SetCursorState(Pousando, ...)` | `Advance(Pousando, ...)` — verificar se não duplica |
| 1138 | `SetCursorState(Embarcando, ...)` | `Advance(Embarcando, ...)` |
| 1486 | `SetCursorState(UnitSelected, "debug keep turn")` | `ExecuteAndReset(...)` + `Advance(UnitSelected, ...)` — reset completo antes |
| 3492 | `SetCursorState(Mirando, ...)` | `Advance(Mirando, ...)` |
| 3842 | `SetCursorState(targetMovementState, ..., rollback: rollback)` | `Retreat(...)` |
| 3864 | `SetCursorState(targetMovementState, ..., rollback: true)` | `Retreat(...)` |
| 3886 | `SetCursorState(targetMovementState, ..., rollback: true)` | `Retreat(...)` |

---

### `TurnStateManager.SupplyQueue.cs` (2 ocorrências)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 70 | `SetCursorState(Suprindo, ...)` | `Advance(Suprindo, ...)` |
| 290 | `SetCursorState(targetMovementState, ..., rollback: true)` | `Retreat(...)` |

---

### `TurnStateManager.Automation.cs` (2 ocorrências)

| Linha | Chamada atual | Migração |
|-------|---------------|----------|
| 1271 | `SetCursorState(CommandService, ...)` | `Advance(CommandService, ...)` |
| 1285 | `SetCursorState(RemovingUnit, ...)` | `Advance(RemovingUnit, ...)` |

---

## Passo 3 — Simplificar os `HandleCancelWhileXxx` após migração

Após todos os call sites estarem migrados, os cancel handlers que hoje fazem:
```csharp
SetCursorState(targetMovementState, "ExitMirandoStateToMovement", rollback: true);
```
passam a ser simplesmente:
```csharp
Retreat("ExitMirandoStateToMovement");
```
E o parâmetro `targetMovementState` pode ser removido dos métodos que o usavam,
porque a pilha já sabe onde voltar.

---

## Passo 4 — Atualizar `LogStateStep` e `RuntimeLog`

Ambos usam `cursorState` diretamente. Substituir por `CurrentCursorState`:

```csharp
// Antes
Debug.Log($"[TurnState] state={cursorState} ...");

// Depois
Debug.Log($"[TurnState] state={CurrentCursorState} ...");
```

Também atualizar `IsTurnStartFuelDepletionExecutionInProgress` e
`IsTurnStartRallyExecutionInProgress` que checam `cursorState ==` diretamente.

---

## Passo 5 — Validar e remover `SetCursorState`

Após todos os call sites migrados:
1. Remover o adaptador `SetCursorState` (ou deixar como wrapper privado para
   casos de debug)
2. Rodar o projeto no Unity e verificar que não há erros de compilação
3. Testar o fluxo principal: Neutral → UnitSelected → MoveuAndando → Mirando →
   Retreat → Retreat → Neutral
4. Testar cancels em todos os estados: pressionar ESC em cada estado e
   confirmar que a pilha volta corretamente

---

## Pontos de atenção

1. **CommandService aberto de múltiplos lugares**: linhas 91, 115, 140 e 1271
   todas chamam `Advance(CommandService)`. Se o usuário já está em
   `CommandService`, não empilhar de novo. Adicionar guarda:
   ```csharp
   if (CurrentCursorState != CursorState.CommandService)
       Advance(CursorState.CommandService, reason);
   ```

2. **`ExitInspectStateToNeutral`**: chamada de vários lugares. Com pilha, é
   apenas `Retreat()`. Mas alguns lugares chamam logo em seguida
   `HandleConfirmFromNeutralLikeState()` — a ordem deve ser mantida.

3. **Estados de fila** (`AircraftFuelDepletionQueue`, `TurnStartRallyQueue`):
   esses estados bloqueiam input. Ao completar, usam `ExecuteAndReset` — correto.
   Garantir que o Retreat não seja chamado neles (não são canceláveis pelo usuário).

4. **`HandleMovementAnimationCompleted`** (linha 155): a lógica condicional
   `rollback: onCompleteState == UnitSelected` significa que quando a animação
   de rollback termina e retorna a `UnitSelected`, é um Retreat; quando avança
   para MoveuAndando/Parado após mover, é um Advance. Manter essa distinção.

5. **`IsInspectingState()`** e outros helpers que checam `cursorState ==`
   diretamente: não precisam mudar — apenas usar `CurrentCursorState` no lugar
   de `cursorState`.

---

## Ordem recomendada de execução

1. Passo 1 (modificar arquivo principal com adaptador)
2. Compilar e verificar que o projeto ainda compila
3. Passo 2, arquivo por arquivo (StateMachine.cs primeiro por ser o mais central)
4. Compilar após cada arquivo
5. Passo 3 (simplificar cancel handlers)
6. Passo 4 (atualizar logs)
7. Passo 5 (remover adaptador, testar)
