# Relatorio de Atualizacao - v1.1.3

## Em uma frase
A v1.1.3 consolida o sistema de economia da partida, com saldo por time, cobrancas reais de servicos/compra e feedback visual de dinheiro na interface.

## O que isso trouxe na pratica
- O fluxo financeiro do turno ficou claro: saldo inicial, renda por turno e saldo atual funcionando no ciclo completo.
- Compras e servicos agora respeitam dinheiro disponivel e interrompem quando o saldo nao permite continuar.
- O jogador passa a enxergar variacao de saldo em tempo real no painel de money.

## Principais melhorias
1. Economia de partida por player
- Estrutura de players no `MatchController` consolidada com `start money`, `actual money` e `income per turn`.
- No inicio do turno, aplica saldo inicial (uma unica vez) + renda por construcao no saldo atual.

2. Custo real em gameplay
- Compra de unidade deduz de `actual money`; com saldo insuficiente, a compra e bloqueada com erro.
- Em suprimento e servico do comando, cada etapa cobra o custo e para quando nao houver saldo suficiente.

3. Debug de economia e controle rapido
- Novos comandos de debug para economia: ajuste de saldo por time e toggle de economia.
- `set money <value>` passou a usar o time ativo como default (alinhado ao comportamento de spawn).

4. HUD de dinheiro
- `text_money` mostra `actual money` do time ativo com formatacao simples (`$ 4.500`).
- `text_update` mostra variacao (`+$` ou `-$`), com fade automatico e interrupcao correta quando chega um novo update.

## Regras importantes (opcional)
- `Economy OFF`: custos de economia viram zero para compras/servicos que usam o resolvedor de custo.
- `set money <value>`: aplica no time ativo.
- `set money:<team> <value>`: aplica no time explicitamente informado.

## Bloco tecnico curto
- Sistemas-chave: `MatchController`, `TurnStateManager` (shopping/supply/command service), `DebugManager`, `SaveGameManager`.
- HUD de money: novo `PanelMoneyController` com variacao por delta de saldo e fade configuravel.
- Novo slider de fade em `AnimationManager`: `Money UI Timing > moneyUpdateFadeDuration`.

## Resultado
- A versao deixa a economia jogavel de ponta a ponta: previsivel no turno, consistente nas deducoes e visivel no HUD durante a partida.

