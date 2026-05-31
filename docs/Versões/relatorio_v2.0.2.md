# Relatorio de Atualizacao - v2.0.2

## Removing Unit revisado

Esta versao revisa o fluxo de `RemovingUnit` dentro da nova FSM baseada em pilha, usando o mesmo padrao validado no Servico do Comando: estado de preview, estado de execucao, bloqueio de cursor e retorno correto para o menu quando a rota nasceu do `PlayerMenu`.

---

## Em uma frase

`RemovingUnit` agora se comporta como uma rota completa da pilha: `Neutral > Player Menu > Removing Unit > Removing Unit Executing`, com cancelamento restaurando o menu e confirmacao entrando em estado de execucao irreversivel.

---

## Principais pontos revisados

### 1. Rota por menu preservada

- A entrada por menu nao desmonta mais `PlayerMenu` antes de abrir o prompt.
- A rota esperada passa a ser `Neutral > PlayerMenu > RemovingUnit`.
- Ao cancelar em `RemovingUnit`, o `Retreat` revela `PlayerMenu`.
- O menu e restaurado com painel e highlight anteriores, como ja acontecia em `CommandService`.

### 2. Estado de execucao explicito

- Confirmar a remocao empilha `RemovingUnitExecuting`.
- Durante `RemovingUnitExecuting`, confirm/cancel nao disparam nova acao.
- O fim da coroutine usa `ExecuteAndReset`, voltando ao `Neutral`.
- Falhas durante a execucao tambem resetam a pilha, evitando retorno a um prompt inconsistente.

### 3. Cursor bloqueado

- O cursor do tabuleiro fica travado em `RemovingUnit`.
- O cursor tambem fica travado em `RemovingUnitExecuting`.
- Isso impede que o alvo da remocao mude depois de abrir o prompt.

### 4. Save/Load bloqueado

- Save/Load fica bloqueado em `RemovingUnit`.
- Save/Load tambem fica bloqueado em `RemovingUnitExecuting`.
- O bloqueio cobre tanto a rota pelo menu quanto os atalhos rapidos de persistencia.

### 5. Guard da pilha atualizado

- `PlayerMenu` agora aceita `RemovingUnit` como filho valido, alem de `CommandService`.
- `RemovingUnitExecuting` exige `RemovingUnit` como pai.
- `RemovingUnitExecuting` e tratado como estado reset-only.

---

## Resultado

`RemovingUnit` deixou de ser um caso especial solto e passou a seguir a mesma gramatica da FSM em pilha usada por `CommandService`. Isso facilita a revisao das proximas rotas: cada fluxo deve declarar claramente sua fase de preview, sua fase de execucao e o ponto em que volta por `Retreat` ou por `ExecuteAndReset`.

---

## Validacao

Build C# executado:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
```

Resultado: 0 erros. Permanecem warnings antigos de APIs obsoletas do Unity.
