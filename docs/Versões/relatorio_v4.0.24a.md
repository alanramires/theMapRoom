# v4.0.24a - Fog Total Atualizado

Ajuste da Névoa de Guerra total: a névoa visual agora também protege as consultas de jogo. Hexes ainda cobertos não podem ser inspecionados nem selecionados, e um tile de névoa próprio foi aplicado às cenas.

## Névoa protege as consultas

- A inspeção por hover só dispara em hexes visíveis na apresentação da névoa; sobre hex coberto, o painel não abre.
- A inspeção de terreno passa a usar `IsCellVisibleInFogPresentation` como critério de visibilidade (antes usava a visibilidade do time ativo).
- Seleção de loja, unidade ou construção é bloqueada em hex ainda coberto pela névoa (retorna sem ação), impedindo interação com o que está oculto.

## Arte da névoa

- Novo tile de névoa (`fow.png`) e entrada de paleta (`fow.asset`).
- Aplicação do tile de névoa às cenas de jogo, dev e tutoriais.

## Validação

- Alterações de script em `TurnStateManager.HelperPanel` e `TurnStateManager.StateMachine`; novos assets `Assets/img/tiles/fow.png` e `Assets/palette/fow.asset`; cenas atualizadas com a névoa.
- Pendente de verificação no Editor (Play mode): inspeção/hover e seleção bloqueadas sob hex coberto, e o visual do novo tile de névoa nos mapas.
