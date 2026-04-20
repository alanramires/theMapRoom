# Plano de Refactor: TurnStateManager â€” Stack de Estados

## Objetivo

Substituir o campo `cursorState` (campo Ãºnico, sem histÃ³rico) por uma
`Stack<CursorState>` no `TurnStateManager`, adotando o padrÃ£o `Advance` /
`Retreat` / `ExecuteAndReset` jÃ¡ implementado corretamente em:

```
D:\Unity Projects\A Sala de Mapas\Assets\Scripts\TurnStateManager.cs
```

Leia esse arquivo primeiro. Ã‰ a referÃªncia canÃ´nica e tem ~80 linhas.

---

## Por que fazer isso

O problema atual: cada `HandleCancelWhileXxx()` sabe hardcoded para qual estado
anterior voltar. Se o fluxo muda, quebra. Com pilha, `Retreat()` sabe
automaticamente onde voltar â€” o estado anterior estÃ¡ na pilha.

Exemplo concreto:
- Atual: `ExitMirandoStateToMovement()` recebe `targetMovementState` como
  parÃ¢metro porque precisa saber se era `MoveuAndando` ou `MoveuParado`.
- Com pilha: `Retreat()` jÃ¡ sabe, porque `MoveuAndando/Parado` foi empilhado
  antes de `Mirando`.

---

## Arquitetura da pilha (referÃªncia de A Sala de Mapas)

```csharp
private readonly Stack<CursorState> stateStack = new();

public CursorState CurrentCursorState => stateStack.Peek();

// AvanÃ§a para novo estado (empilha)
public void Advance(CursorState nextState) { ... }

// Cancela / volta ao anterior (desempilha)
public void Retreat() { ... }

// AÃ§Ã£o executada com sucesso â€” limpa tudo, volta ao Neutral
public void ExecuteAndReset() { ... }
```

---

## Fluxo da pilha em tempo de execuÃ§Ã£o

```
Neutral                       â† base da pilha (sempre presente)
  Advance(UnitSelected)
  UnitSelected
    Advance(MoveuAndando)
    MoveuAndando
      Advance(Mirando)
      Mirando
        Retreat()             â† volta para MoveuAndando automaticamente
      Retreat()               â† volta para UnitSelected
    Retreat()                 â† volta para Neutral (ou ExecuteAndReset se confirmou)
```

---

## Passo 1 â€” Modificar `TurnStateManager.cs` (arquivo principal)

**Arquivo:** `Assets/Scripts/Match/TurnStateManager.cs`

### 1a. Substituir o campo `cursorState`

REMOVER:
```csharp
[SerializeField] private CursorState cursorState = CursorState.Neutral;
```

ADICIONAR (como campo privado, nÃ£o serializado):
```csharp
private readonly Stack<CursorState> stateStack = new();
```

### 1b. Atualizar a propriedade pÃºblica

REMOVER:
```csharp
public CursorState CurrentCursorState => cursorState;
```

ADICIONAR:
```csharp
public CursorState CurrentCursorState => stateStack.Count > 0 ? stateStack.Peek() : CursorState.Neutral;
```

### 1c. Inicializar a pilha no Awake

Localizar o mÃ©todo `Awake()` (ou `Start()` se nÃ£o houver Awake). Adicionar no
inÃ­cio:

```csharp
stateStack.Clear();
stateStack.Push(CursorState.Neutral);
```

### 1d. Refatorar `SetCursorState` para usar a pilha internamente

O mÃ©todo atual `SetCursorState(CursorState nextState, string reason, bool rollback = false)`
tem lÃ³gica de side effects importante (limpar threat overlay, notificar neutral)
que deve ser **preservada**. Apenas a atribuiÃ§Ã£o `cursorState = nextState` muda.

O mÃ©todo passa a ser **privado e interno** â€” chamado pelos novos mÃ©todos pÃºblicos.
Reestruturar assim:

```csharp
// MÃ©todo interno â€” mantÃ©m todos os side effects existentes
private void ApplyStateTransition(CursorState nextState, string reason, bool rollback = false)
{
    CursorState previous = CurrentCursorState;

    // MantÃ©m side effects existentes: limpar threat overlay, notificar neutral
    bool shouldClearThreatOverlayOnTransition =
        nextState != CursorState.Neutral &&
        nextState != CursorState.InspectingHotZone;
    if (shouldClearThreatOverlayOnTransition)
    {
        if (scannerPromptStep == ScannerPromptStep.ThreatLayerTeamSelect)
            scannerPromptStep = ScannerPromptStep.AwaitingAction;
        ClearEnemyThreatLayersOverlay();
    }

    // Aplica o estado (a pilha jÃ¡ foi modificada pelo chamador)
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

### 1e. Adicionar os trÃªs mÃ©todos pÃºblicos da pilha

```csharp
// AvanÃ§a: empilha novo estado
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

// AÃ§Ã£o confirmada: limpa pilha, volta ao Neutral
private void ExecuteAndReset(string reason)
{
    stateStack.Clear();
    stateStack.Push(CursorState.Neutral);
    ApplyStateTransition(CursorState.Neutral, reason, rollback: false);
}
```

> Nota: esses trÃªs mÃ©todos substituem `SetCursorState`. ApÃ³s a migraÃ§Ã£o
> completa, `SetCursorState` pode ser removido. Durante a migraÃ§Ã£o, mantenha
> os dois coexistindo para nÃ£o quebrar nada de uma vez.

### 1f. Atualizar `SetCursorState` para delegar Ã  pilha (adaptador temporÃ¡rio)

Para nÃ£o quebrar os ~60 call sites de uma vez, transformar `SetCursorState`
num adaptador que usa a pilha internamente:

```csharp
private void SetCursorState(CursorState nextState, string reason, bool rollback = false)
{
    if (rollback)
    {
        // Um Ãºnico pop â€” preserva o comportamento original de "voltar um nÃ­vel"
        if (stateStack.Count > 1) stateStack.Pop();
    }
    else
    {
        stateStack.Push(nextState);
    }
    ApplyStateTransition(CurrentCursorState, reason, rollback);
}
```

> **Por que um Ãºnico pop:** o `SetCursorState(..., rollback: true)` original
> era um set direto â€” nunca navegava mÃºltiplos nÃ­veis. Um pop Ãºnico preserva
> esse comportamento durante a migraÃ§Ã£o.
>
> Se durante a migraÃ§Ã£o vocÃª encontrar um caso onde o cancel precisa voltar
> **dois nÃ­veis** (ex: ir de `Mirando` direto para `UnitSelected` pulando
> `MoveuAndando`), substitua por dois `Retreat()` explÃ­citos â€” nÃ£o tente
> resolver isso no adaptador.
>
> Este adaptador permite que os call sites existentes continuem funcionando
> enquanto sÃ£o migrados um a um para `Advance`/`Retreat`/`ExecuteAndReset`.

---

## Passo 2 â€” Categorizar e migrar os call sites de `SetCursorState`

SÃ£o ~60 ocorrÃªncias espalhadas pelos arquivos parciais. A tabela abaixo
categoriza cada uma. Migre de cima para baixo, arquivo por arquivo.

### Regras de classificaÃ§Ã£o

| Tipo | CritÃ©rio | MigraÃ§Ã£o |
|------|----------|----------|
| **Advance** | Indo para estado mais profundo no fluxo; `rollback: false` (padrÃ£o) | `Advance(estado, reason)` |
| **Retreat** | Voltando ao estado anterior; `rollback: true` OU saindo de qualquer estado em direÃ§Ã£o a Neutral/movimento | `Retreat(reason)` |
| **ExecuteAndReset** | AÃ§Ã£o concluÃ­da com sucesso; volta ao Neutral sem possibilidade de cancelar | `ExecuteAndReset(reason)` |

---

### `TurnStateManager.StateMachine.cs` (13 ocorrÃªncias)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 139 | `SetCursorState(Neutral, "normalize")` | `ExecuteAndReset("normalize")` â€” normaliza antes de re-processar |
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

### `TurnStateManager.cs` (arquivo principal, 3 ocorrÃªncias alÃ©m do prÃ³prio mÃ©todo)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 908 | `SetCursorState(Neutral, "ClearSelectionAndReturnToNeutral", rollback: ...)` | `ExecuteAndReset(...)` â€” seleÃ§Ã£o descartada |
| 1302 | `SetCursorState(AircraftFuelDepletionQueue, "begin")` | `Advance(AircraftFuelDepletionQueue, ...)` |
| 1310 | `SetCursorState(Neutral, "completed")` | `ExecuteAndReset(...)` â€” fila processada |

---

### `TurnStateManager.Movement.cs` (2 ocorrÃªncias)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 15 | `SetCursorState(resolvedMovementState, ...)` | `Advance(resolvedMovementState, ...)` â€” entra em Moved apÃ³s mover |
| 155 | `SetCursorState(onCompleteState, ..., rollback: onCompleteState == UnitSelected)` | `if rollback â†’ Retreat(...)` else `Advance(...)` â€” manter lÃ³gica condicional |

---

### `TurnStateManager.Capture.cs` (1 ocorrÃªncia)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 38 | `SetCursorState(Capturando, ...)` | `Advance(Capturando, ...)` |

AlÃ©m disso: onde a captura Ã© concluÃ­da (buscar por onde `captureExecutionInProgress`
Ã© setado para false e o estado retorna ao Neutral) â†’ `ExecuteAndReset(...)`.

---

### `TurnStateManager.CommandService.cs` (7 ocorrÃªncias)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 91 | `SetCursorState(CommandService, ...)` | `Advance(CommandService, ...)` |
| 115 | `SetCursorState(CommandService, ...)` | `Advance(CommandService, ...)` |
| 140 | `SetCursorState(CommandService, ...)` | `Advance(CommandService, ...)` â€” verificar se nÃ£o duplica avanÃ§o |
| 668 | `SetCursorState(Neutral, "no served targets")` | `ExecuteAndReset(...)` |
| 704 | `SetCursorState(Neutral, "completed")` | `ExecuteAndReset(...)` |
| 717 | `SetCursorState(Neutral, "cleanup")` | `ExecuteAndReset(...)` |
| 762 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` â€” cancelamento |

> AtenÃ§Ã£o: CommandService pode ser aberto de 3 lugares diferentes (hotkey,
> menu, ordem). Garantir que a pilha nÃ£o acumule estados CommandService
> duplicados â€” considerar checar se jÃ¡ estÃ¡ nesse estado antes de `Advance`.

---

### `TurnStateManager.ConstructionShopping.cs` (2 ocorrÃªncias)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 40 | `SetCursorState(ShoppingAndServices, ...)` | `Advance(ShoppingAndServices, ...)` |
| 52 | `SetCursorState(Neutral, ..., rollback: rollback)` | `if rollback â†’ Retreat(...)` else `ExecuteAndReset(...)` |

---

### `TurnStateManager.Disembark.cs` (2 ocorrÃªncias)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 118 | `SetCursorState(Desembarcando, ...)` | `Advance(Desembarcando, ...)` |
| 384 | `SetCursorState(targetMovementState, ..., rollback: true)` | `Retreat(...)` â€” pilha jÃ¡ tem MoveuAndando/Parado |

---

### `TurnStateManager.HelperPanel.cs` (2 ocorrÃªncias)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 795 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` |
| 894 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` |

---

### `TurnStateManager.Merge.cs` (2 ocorrÃªncias)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 83 | `SetCursorState(Fundindo, ...)` | `Advance(Fundindo, ...)` |
| 327 | `SetCursorState(targetMovementState, ..., rollback: true)` | `Retreat(...)` |

---

### `TurnStateManager.Planning.cs` (5 ocorrÃªncias)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 32 | `SetCursorState(Planning, ...)` | `Advance(Planning, ...)` |
| 41 | `SetCursorState(Neutral, ..., rollback: rollback)` | `if rollback â†’ Retreat(...)` else `ExecuteAndReset(...)` |
| 92 | `SetCursorState(TurnStartRallyQueue, ...)` | `Advance(TurnStartRallyQueue, ...)` |
| 100 | `SetCursorState(Neutral, "completed")` | `ExecuteAndReset(...)` |
| 135 | `SetCursorState(UnitSelected, "planning move select")` | `Advance(UnitSelected, ...)` â€” caso especial, planejamento faz seleÃ§Ã£o de unidade |

---

### `TurnStateManager.ScannerPrompt.cs` (14 ocorrÃªncias)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 421 | `SetCursorState(RemovingUnit, ...)` | `Advance(RemovingUnit, ...)` |
| 463 | `SetCursorState(RemovingUnit, ...)` | `Advance(RemovingUnit, ...)` |
| 571 | `SetCursorState(Neutral, ..., rollback: logCanceled)` | `if logCanceled â†’ Retreat(...)` else `ExecuteAndReset(...)` |
| 711 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` |
| 745 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` |
| 780 | `SetCursorState(Neutral, ..., rollback: true)` | `Retreat(...)` |
| 1003 | `SetCursorState(Pousando, ...)` | `Advance(Pousando, ...)` |
| 1043 | `SetCursorState(InspectingHotZone, ...)` | `Advance(InspectingHotZone, ...)` |
| 1065 | `SetCursorState(UnitSelected, "auto-select")` | `Advance(UnitSelected, ...)` |
| 1107 | `SetCursorState(Pousando, ...)` | `Advance(Pousando, ...)` â€” verificar se nÃ£o duplica |
| 1138 | `SetCursorState(Embarcando, ...)` | `Advance(Embarcando, ...)` |
| 1486 | `SetCursorState(UnitSelected, "debug keep turn")` | `ExecuteAndReset(...)` + `Advance(UnitSelected, ...)` â€” reset completo antes |
| 3492 | `SetCursorState(Mirando, ...)` | `Advance(Mirando, ...)` |
| 3842 | `SetCursorState(targetMovementState, ..., rollback: rollback)` | `Retreat(...)` |
| 3864 | `SetCursorState(targetMovementState, ..., rollback: true)` | `Retreat(...)` |
| 3886 | `SetCursorState(targetMovementState, ..., rollback: true)` | `Retreat(...)` |

---

### `TurnStateManager.SupplyQueue.cs` (2 ocorrÃªncias)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 70 | `SetCursorState(Suprindo, ...)` | `Advance(Suprindo, ...)` |
| 290 | `SetCursorState(targetMovementState, ..., rollback: true)` | `Retreat(...)` |

---

### `TurnStateManager.Automation.cs` (2 ocorrÃªncias)

| Linha | Chamada atual | MigraÃ§Ã£o |
|-------|---------------|----------|
| 1271 | `SetCursorState(CommandService, ...)` | `Advance(CommandService, ...)` |
| 1285 | `SetCursorState(RemovingUnit, ...)` | `Advance(RemovingUnit, ...)` |

---

## Passo 3 â€” Simplificar os `HandleCancelWhileXxx` apÃ³s migraÃ§Ã£o

ApÃ³s todos os call sites estarem migrados, os cancel handlers que hoje fazem:
```csharp
SetCursorState(targetMovementState, "ExitMirandoStateToMovement", rollback: true);
```
passam a ser simplesmente:
```csharp
Retreat("ExitMirandoStateToMovement");
```
E o parÃ¢metro `targetMovementState` pode ser removido dos mÃ©todos que o usavam,
porque a pilha jÃ¡ sabe onde voltar.

---

## Passo 4 â€” Atualizar `LogStateStep` e `RuntimeLog`

Ambos usam `cursorState` diretamente. Substituir por `CurrentCursorState`:

```csharp
// Antes
Debug.Log($"[TurnState] state={cursorState} ...");

// Depois
Debug.Log($"[TurnState] state={CurrentCursorState} ...");
```

TambÃ©m atualizar `IsTurnStartFuelDepletionExecutionInProgress` e
`IsTurnStartRallyExecutionInProgress` que checam `cursorState ==` diretamente.

---

## Passo 5 â€” Validar e remover `SetCursorState`

ApÃ³s todos os call sites migrados:
1. Remover o adaptador `SetCursorState` (ou deixar como wrapper privado para
   casos de debug)
2. Rodar o projeto no Unity e verificar que nÃ£o hÃ¡ erros de compilaÃ§Ã£o
3. Testar o fluxo principal: Neutral â†’ UnitSelected â†’ MoveuAndando â†’ Mirando â†’
   Retreat â†’ Retreat â†’ Neutral
4. Testar cancels em todos os estados: pressionar ESC em cada estado e
   confirmar que a pilha volta corretamente

---

## Pontos de atenÃ§Ã£o

1. **CommandService aberto de mÃºltiplos lugares**: linhas 91, 115, 140 e 1271
   todas chamam `Advance(CommandService)`. Se o usuÃ¡rio jÃ¡ estÃ¡ em
   `CommandService`, nÃ£o empilhar de novo. Adicionar guarda:
   ```csharp
   if (CurrentCursorState != CursorState.CommandService)
       Advance(CursorState.CommandService, reason);
   ```

2. **`ExitInspectStateToNeutral`**: chamada de vÃ¡rios lugares. Com pilha, Ã©
   apenas `Retreat()`. Mas alguns lugares chamam logo em seguida
   `HandleConfirmFromNeutralLikeState()` â€” a ordem deve ser mantida.

3. **Estados de fila** (`AircraftFuelDepletionQueue`, `TurnStartRallyQueue`):
   esses estados bloqueiam input. Ao completar, usam `ExecuteAndReset` â€” correto.
   Garantir que o Retreat nÃ£o seja chamado neles (nÃ£o sÃ£o cancelÃ¡veis pelo usuÃ¡rio).

4. **`HandleMovementAnimationCompleted`** (linha 155): a lÃ³gica condicional
   `rollback: onCompleteState == UnitSelected` significa que quando a animaÃ§Ã£o
   de rollback termina e retorna a `UnitSelected`, Ã© um Retreat; quando avanÃ§a
   para MoveuAndando/Parado apÃ³s mover, Ã© um Advance. Manter essa distinÃ§Ã£o.

5. **`IsInspectingState()`** e outros helpers que checam `cursorState ==`
   diretamente: nÃ£o precisam mudar â€” apenas usar `CurrentCursorState` no lugar
   de `cursorState`.

---

## Ordem recomendada de execuÃ§Ã£o

1. Passo 1 (modificar arquivo principal com adaptador)
2. Compilar e verificar que o projeto ainda compila
3. Passo 2, arquivo por arquivo (StateMachine.cs primeiro por ser o mais central)
4. Compilar apÃ³s cada arquivo
5. Passo 3 (simplificar cancel handlers)
6. Passo 4 (atualizar logs)
7. Passo 5 (remover adaptador, testar)

------------------
ANDAMENTO DA MIGRAÇÃO
----------------
Estamos indo na direção certa, principalmente por uma coisa: os fluxos estão deixando de ser “efeitos colaterais de UI” e virando rotas explícitas de estado.

Hoje a FSM já está mais preparada para AI/replay do que antes por causa destes pontos:

1. **Rotas com origem preservada**

Antes vários fluxos faziam algo como:

```text
menu fecha
estado muda
ESC tenta adivinhar para onde voltar
```

Agora a stack resolve isso:

```text
Neutral > CommandService > Neutral
Neutral > PlayerMenu > CommandService > PlayerMenu > Neutral
```

E o mesmo padrão foi estendido para:

```text
RemovingUnit
Saving
Loading
EndingTurn
```

Isso é bom para replay e AI porque o estado anterior não precisa ser inferido por botão, painel, variável temporária ou “quem chamou”. Ele está na pilha.

2. **Separação entre preview e execução**

`CommandService` e `RemovingUnit` já apontam para um modelo saudável:

```text
CommandService          // preview / confirmação
CommandServiceExecuting // execução irreversível
```

```text
RemovingUnit
RemovingUnitExecuting
```

Isso é exatamente o que replay e AI precisam. O replay consegue gravar a decisão e executar sem depender de input humano. A AI consegue pedir uma ação, validar, confirmar e aguardar o estado executivo terminar.

3. **Estados bloqueiam input indevido**

Adicionar estados como `Saving`, `Loading`, `EndingTurn`, `EndingTurnExecuting`, `RemovingUnitExecuting` na FSM reduz vazamento de input. Isso é importante porque AI/replay falham quando um input humano ou UI ainda consegue interferir durante execução automática.

O caminho ideal é:

```text
estado de decisão
estado de confirmação/preview
estado de execução
retorno por Retreat ou ExecuteAndReset
```

4. **UI está virando cliente da FSM**

O menu não deveria “ser dono” da lógica de jogo. Ele deve pedir:

```csharp
TryOpenCommandServiceFromMenu()
TryOpenDestroyUnitPromptFromMenu()
TryEnterSavingState()
TryEnterLoadingState()
TryExecuteEndingTurnFromMenu()
```

Esse é o modelo certo. A UI dispara intenção; a FSM decide se pode, empilha estado e controla retorno.

Para AI/replay, isso é essencial porque eles podem chamar os mesmos métodos sem depender de botão.

5. **Replay precisa de comandos determinísticos**

Para replay manager, o ponto mais importante é: cada ação gravada precisa ter uma entrada clara e uma finalização clara.

Exemplo bom:

```text
Replay action: CommandService
FSM: Neutral > CommandService > CommandServiceExecuting > Neutral
```

O replay pode aguardar:

```text
CurrentCursorState == Neutral
```

ou evento:

```text
OnCursorReturnedToNeutral
```

Isso é muito melhor do que esperar “alguma coroutine terminar” sem saber qual estado estava ativo.

**O que ainda falta melhorar**

Ainda tem partes que parecem meio híbridas:

- `SaveGameManager` ainda tem lógica própria de estado e cena, fora da FSM.
- `BattleMapMenuRootController` ainda carrega bastante responsabilidade de navegação, seleção e dispatch.
- Alguns fluxos ainda parecem depender de `CursorController` para confirmar/cancelar coisas que conceitualmente pertencem à FSM.
- A FSM ainda mistura estados de UI/player menu com estados de ação de jogo.

Mas isso não é necessariamente errado agora. Só significa que estamos no meio da migração.

**A direção que eu seguiria**

Eu manteria o padrão atual e consolidaria:

```text
1. Toda ação tática tem TryOpenX(...)
2. Toda ação com preview tem X + XExecuting
3. Cancelamento de preview usa Retreat()
4. Execução final usa ExecuteAndReset()
5. Menu nunca decide retorno; retorno vem da stack
6. Replay/AI chamam a mesma API que o jogador chama
```

Para uma futura AI, o ideal seria algo assim:

```csharp
if (turnStateManager.TryOpenCommandServiceFromAutomation(out reason))
{
    turnStateManager.TryConfirmPendingCommandServiceOrder();
}
```

Para replay:

```csharp
yield return ExecuteRecordedCommandServiceBatch(action);
yield return WaitUntilNeutral();
```

Ou seja: AI e replay não deveriam simular tecla, botão ou painel. Eles devem conversar direto com a FSM.

**Resumo**

Estamos preparando a base correta: a FSM está começando a virar a fonte de verdade dos fluxos, e o menu está virando só uma interface de entrada. Isso é exatamente o que AI e replay precisam.

O próximo salto de qualidade seria padronizar todos os sensores/ações nesse contrato:

```text
CanPreview
OpenPreview
ConfirmExecution
CancelPreview
ExecuteAndReset
```

Aí AI, replay e jogador passam a usar a mesma estrada, com entradas diferentes.

Eu não começaria um jogo novo do zero agora.

O que você tem hoje já tem valor demais para jogar fora: sensores, FoW, replay parcial, economia, logística, unidades, terreno, UI, saves, cenas, AI em andamento. O problema não é “o projeto é irrecuperável”. O problema é que a arquitetura cresceu com muita regra acoplada em `TurnStateManager`, `CursorController`, menu e managers laterais. Isso dá dor porque cada correção pequena vira investigação grande.

Minha recomendação pragmática é: **continuar este jogo, mas parar de adicionar feature por um tempo e fazer uma estabilização arquitetural curta e objetiva.**

Não um refactor infinito. Um ciclo fechado.

Algo como:

```text
Objetivo: preparar FSM para replay/AI sem reescrever o jogo.
Escopo: 1 a 2 semanas de trabalho focado.
Regra: nenhuma feature nova, só padronização de fluxo.
```

Eu faria assim:

1. **Congelar os fluxos que já funcionam**

Declare que estes são referência:

```text
CommandService
RemovingUnit
Saving
Loading
EndingTurn
```

E documente o contrato:

```text
Neutral > X > Neutral
Neutral > PlayerMenu > X > PlayerMenu > Neutral
X > XExecuting > Neutral
```

2. **Transformar isso em padrão de código**

Criar helpers pequenos na FSM, não um framework enorme:

```csharp
TryEnterPreviewState(...)
RetreatPreviewState(...)
EnterExecutingState(...)
ExecuteAndReset(...)
```

Ou mesmo só nomes e organização consistentes primeiro.

3. **Parar de deixar UI decidir retorno**

Menu pode chamar ação, mas não deve guardar breadcrumb. Breadcrumb tem que ser stack.

4. **Separar ação tática de input**

Para cada ação importante:

```text
Input humano chama TryOpenXFromHotkey()
Menu chama TryOpenXFromMenu()
AI chama TryOpenXFromAutomation()
Replay chama TryReplayX()
```

Mas todos entram no mesmo núcleo:

```text
TryOpenXCore(origin)
ConfirmX()
CancelX()
```

5. **Criar testes manuais/checklist antes de mexer**

Porque sem teste, cada ajuste consome crédito em regressão. Um checklist simples já ajuda:

```text
X hotkey > ESC > Neutral
X menu > ESC > Menu
X menu > Confirm > Executing > Neutral
Save hotkey > ESC > Neutral
Load cross-scene > troca cena > carrega
Minimap > scroll > toggle > volta zoom anterior
```

Sobre começar outro jogo do zero: parece tentador porque você levaria as lições aprendidas. Mas geralmente o segundo projeto começa limpo por 20% do tempo e depois volta a ficar complexo. Você gastaria meses reconstruindo sistemas que já existem e ainda teria que resolver os mesmos problemas de FSM, replay, AI, save, UI e cenas.

Eu só começaria do zero se uma destas fosse verdade:

```text
O core gameplay mudou completamente.
As cenas/prefabs/dados estão inutilizáveis.
O código não compila por dias.
Você não quer mais manter compatibilidade com saves/mapas atuais.
Você quer transformar isso em outro produto, não evoluir este.
```

Pelo que vejo, não é esse o caso.

Minha sugestão honesta: **não recomeça. Faz uma “v2.1 arquitetura” dentro do projeto atual.** Pequena, agressiva e com critério de fim.

O foco não deve ser “deixar bonito”. Deve ser reduzir o custo de cada bug futuro:

```text
menos inferência
menos estado escondido
menos UI mandando em gameplay
mais rotas explícitas
mais APIs reutilizáveis por jogador/AI/replay
```

Isso é menos glamouroso que começar do zero, mas tem muito mais chance de entregar o jogo.