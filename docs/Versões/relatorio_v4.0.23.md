# v4.0.23 - Victory Conditions

Esta versão define e unifica as condições de fim de jogo: captura de QG, destruição total de um exército e rendição passam a compartilhar o mesmo ciclo de eliminação e a mesma apresentação, com resultado colorido pelo time e distinção clara entre vitória e derrota do humano local.

## Condições de fim de jogo

- Capturar o QG (`isPlayerHeadQuarter`) de um jogador elimina aquele time na hora e pode encerrar a partida.
- O primeiro a eliminar um jogador — capturando um QG ou destruindo o exército por completo — vence imediatamente, mesmo que ainda restem outros jogadores ou IA.
- O vencedor é o time que executou a eliminação; em derrota por 0 unidades, a eliminação é atribuída a quem estava agindo (time ativo), com fallback para o primeiro oponente vivo.
- A rendição pelo menu (Render-se) entra no mesmo ciclo: o jogador que se rende é marcado como derrotado e o oponente vence.
- Ao eliminar um time, suas construções são neutralizadas e o evento de derrota é disparado — comportamento comum a QG, 0 unidades e rendição.
- Nova flag `allowDefeatForHeadQuarterCapture` permite ligar/desligar a derrota por captura de QG.

## Apresentação de vitória e derrota

- O resultado é mostrado do ponto de vista do humano local: se o vencedor for humano, aparece VITÓRIA!; se quem venceu foi a IA e há humano na partida, aparece DERROTA!.
- O título usa a cor do time vencedor na vitória e a cor do próprio time na derrota.
- A descrição cita o time derrotado pintado na cor dele, com o motivo específico: QG capturado, exército derrotado ou rendição do time.
- O `Panel_vitoria` é reutilizado para os dois desfechos, trocando apenas título, cor e SFX (vitória/derrota).

## Menu durante o turno da IA

- O clique direito não abre nem agenda o menu durante o turno da IA, mesmo sendo o equivalente ao ESC.
- A guarda passou a valer também nas janelas Neutral entre batches da IA, onde antes o clique direito escorregava direto para a abertura do menu.
- O ESC/Backspace continua funcionando: abre na hora se possível, ou pausa a simulação e agenda a abertura no próximo Neutral.

## Painel de opções do capturador

- Para unidades capturadoras, a ação Conquistar sobe para o topo do painel OPÇÕES quando está disponível.
- A ordem de navegação (setas) e o foco pré-selecionado passam a começar por Conquistar, batendo com a ordem visual do painel.
- Para os demais papéis, a ordem padrão é mantida.

## Validação

- Alterações concentradas em `MatchController`, `BattleMapMenuRootController`, `TurnStateManager.Capture`, `TurnStateManager.HelperPanel` e `TurnStateManager.ScannerPrompt`.
- Pendente de verificação no Editor (Play mode): compilação e teste em jogo dos quatro desfechos (QG capturado, exército eliminado, rendição e derrota do humano).
