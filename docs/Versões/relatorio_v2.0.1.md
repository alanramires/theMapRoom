# Relatorio de Atualizacao - v2.0.1

## Servico do comando validado

Esta versao consolida a primeira rodada pratica de validacao da nova pilha de estados do `TurnStateManager`, com foco no Servico do Comando e nas rotas de entrada por atalho e pelo menu do jogador.

---

## Em uma frase

O Servico do Comando passou a respeitar a pilha `Neutral > Command` e `Neutral > Player Menu > Command`, com retorno correto por `Retreat`, bloqueio de cursor, estado de execucao explicito e debug visual da FSM.

---

## Principais pontos validados

### 1. Pilha de estados em uso real

- `Neutral > Command` ao acionar o Servico do Comando pelo atalho.
- `Neutral > Player Menu > Command` ao acionar pelo menu.
- `Retreat` agora revela o estado anterior em vez de forcar sempre `Neutral`.
- `CommandServiceExecuting` representa a fase irreversivel da execucao.

### 2. Debug visual da FSM

- UI Toolkit com elemento `FSM`/`fsm` para exibir a pilha em tempo real.
- Formato horizontal: `Neutral > Player Menu > Command > Executing`.
- Binding tolerante a maiusculas/minusculas no nome do elemento.

### 3. Servico do Comando

- Preview entra em `Command`.
- Confirmacao entra em `Executing`.
- Execucao finaliza com reset controlado para `Neutral`.
- Rota pelo menu preserva `PlayerMenu` como estado anterior.
- ESC em `Command` volta corretamente para `PlayerMenu` quando a origem foi o menu.

### 4. Cursor e menu

- Cursor do tabuleiro fica travado em `PlayerMenu`, `CommandService` e `CommandServiceExecuting`.
- Setas continuam navegando o menu quando o topo da pilha volta para `PlayerMenu`.
- O menu e restaurado ao revelar `PlayerMenu` no `Retreat`.
- Save/Load fica bloqueado enquanto a FSM esta em `PlayerMenu`, `CommandService` ou `CommandServiceExecuting`.

### 5. Ferramentas de teste

- Painel de debug recebeu comando para sincronizar a posicao da unidade selecionada pela posicao no Scene.
- Snap para celula evita erro de posicionamento manual impreciso.

---

## Limpezas e ajustes auxiliares

- Warnings de `DontDestroyOnLoad` em `LogManager` e `SectorManager` foram eliminados.
- Logs repetidos de bloqueio de Save/Load no `Update` foram silenciados quando nao ha tentativa ativa do jogador.
- Mensagens do Servico do Comando ficaram mais claras para casos sem alvos elegiveis.

---

## Resultado

O Servico do Comando esta validado como primeiro caso real da migracao para pilha. A rota por atalho e a rota por menu agora exercitam `Advance`, `Retreat` e `ExecuteAndReset` sem depender de if/else especifico para restaurar fluxo basico de estado.

Este checkpoint tambem expôs as proximas frentes: padronizar restauracao visual de UI no retorno por pilha e continuar a revisao dos sensores para que replay e futura IA possam produzir batches sem conhecer detalhes internos de cada sensor.
