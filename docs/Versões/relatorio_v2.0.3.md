# Relatorio de Atualizacao - v2.0.3

## Sensores menores revisados

Esta versao revisa fluxos menores de sensores e a integracao deles com a FSM em pilha, com foco em consistencia de entrada por atalho, entrada por menu, cancelamento por `ESC` e retorno ao estado correto.

---

## Em uma frase

Os atalhos e botoes de acoes auxiliares passam a respeitar melhor a origem do jogador: se a acao nasceu no `Neutral`, volta ao `Neutral`; se nasceu no `PlayerMenu`, retorna ao menu e ao botao correto.

---

## Principais pontos revisados

### 1. Save e Load em rotas separadas

- `Save` e `Load` deixam de ser tratados como uma tela unica generica.
- O menu passa a reconhecer `button_save` e `button_load` como acoes distintas.
- `Saving` restaura `panel_options + button_save` quando a rota nasceu do menu.
- `Loading` restaura `panel_options + button_load` quando a rota nasceu do menu.

### 2. Breadcrumb pela FSM

- `Saving` e `Loading` entram na mesma gramatica de pilha usada por `CommandService` e `RemovingUnit`.
- Atalho direto respeita `Neutral > Saving/Loading > Neutral`.
- Rota por menu respeita `Neutral > PlayerMenu > Saving/Loading > PlayerMenu > Neutral`.
- O restore do menu acontece apenas quando o `Retreat` revela `PlayerMenu`.

### 3. Cancelamento por ESC corrigido

- O `ESC` que cancela Save/Load agora e consumido no frame correto.
- Isso evita que o mesmo `ESC` tambem abra o menu imediatamente apos o retorno ao `Neutral`.
- O comportamento fica alinhado aos atalhos `X` e `U`.

### 4. Navegacao do menu por painel

- A lista navegavel de cada painel agora e reconstruida a partir do layout real do painel ativo.
- Botoes movidos para outro painel deixam de participar da navegacao do painel anterior.
- A selecao por teclado fica restrita ao painel atual.

### 5. Estados auxiliares bloqueados corretamente

- Save/Load ficam bloqueados durante estados de acao em preview/execucao quando nao fazem parte da rota atual.
- `Saving` e `Loading` passam a ser estados explicitamente aceitos pela FSM quando o prompt de persistencia esta aberto.
- Estados de confirmacao/execucao auxiliares ficam protegidos contra confirm/cancel indevidos.

---

## Resultado

Os sensores menores e prompts auxiliares deixam de depender de comportamento especial solto no menu. As rotas passam a seguir a mesma regra: o estado empilhado decide para onde o jogador volta, e o menu apenas restaura painel e selecao quando a pilha revela `PlayerMenu`.

---

## Validacao

Build C# executado:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
```

Resultado: 0 erros. Permanecem warnings antigos de APIs obsoletas do Unity.
