# v4.0.19b - Ajustes para a versão web parte III

Terceira leva de preparativos para a **versão web (WebGL)**: entrada por **toque** (tap = clique) para jogar no navegador/celular, botão de **tela cheia**, um **indicador visual do turno da IA** e um **resumo de confirmação** ao iniciar Novo Jogo.

## Toque como clique (web / mobile)

- Um **tap com um dedo** (toque sem arrastar) agora age como **clique esquerdo / confirmar**, permitindo jogar no navegador e no celular sem mouse.
- A distinção tap vs arrasto usa o mesmo limiar de deslocamento do clique direito; **multi-toque** (pinça) não dispara o tap.
- A posição do ponteiro passa a considerar o `primaryTouch` do `Touchscreen`, além do mouse.
- Taps **sobre a UI** não teleportam o cursor nem cancelam ações indevidamente: novos testes de raycast (`IsScreenPointOverUI` / `IsScreenPointOverClickableUI`) e respeito ao `panel_helper` no estado de inspeção.

## Botão de tela cheia

- Novo `FullscreenShortcutButton` no ícone **`tela_cheia`** (no Panel_money): alterna **tela cheia** ao toque/clique, com feedback no diálogo.
- Guardado para **WebGL** (o modo de janela fullscreen só é forçado fora do WebGL; no navegador o pedido de fullscreen respeita a política do browser).
- Novos ícones: "tela cheia" / "full screen".

## Indicador do turno da IA

- Overlay central pulsante **"TURNO DA IA"** (com a cor do time ativo e o estágio atual) enquanto a IA joga, para o jogador entender que a vez não é dele.
- Fica **oculto enquanto um batch da IA está executando** (`aiTurnBatchExecuting`), aparecendo nas pausas entre ações — sem poluir os momentos de animação/execução.

## Resumo de confirmação do Novo Jogo

- No passo final do assistente de Novo Jogo, o `panel_helper` mostra um **resumo**: mapa, setup (preset), Jogador 1 e Jogador 2 (com as cores dos times) e as **regras** do preset escolhido, antes de iniciar.

## Validação

- `Assembly-CSharp`: compilação a ser confirmada no Editor.
- A validar: no **navegador/celular**, tap confirmando e não vazando sobre a UI; **tela cheia** entrando/saindo (e o comportamento no WebGL); o indicador "TURNO DA IA" aparecendo nas pausas da IA; e o resumo de Novo Jogo no passo de confirmar.
