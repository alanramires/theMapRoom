# Relatorio de Atualizacao - v2.0.25

## AI Refine II

Esta versao continua o refinamento da IA com foco em compras, cadeia de elite, transporte, suporte de fogo rebocado, HUD de diagnostico e separacao mais clara entre unidades ofensivas e defensivas.

## Em uma frase

A IA passa a comprar e transportar com mais criterio: respeita upgrades encadeados, reserva dinheiro para elite sem travar a composicao, usa fire support em proporcao ao assalto, evita entregas ruins de artilharia e mostra mais informacao de setor durante o debug.

## Compras e composicao

- `UnitData` ganhou `aiPurchaseMode`, permitindo marcar unidades como `Either`, `Offensive` ou `Defensive`.
- Compras defensivas deixam de puxar unidades marcadas como ofensivas.
- Compras ofensivas ignoram unidades defensivas quando nao ha ameaca ativa contra a base.
- Fire support defensivo pode entrar como resposta barata quando existe ameaca a base.
- A demanda de fire support agora escala pela razao assalto/suporte, em vez de parar apos a primeira unidade de suporte.
- A progressao de capturadores, transporte preventivo e pausa para suporte passou a escalar melhor com o total de slots do mapa.

## Elite e reservas

- `UnitData` ganhou `eliteFrom`, definindo cadeia direta de evolucao usada pela IA.
- Elite nivel 2 so fica disponivel quando a unidade predecessora ja esta em campo.
- A reserva economica para elite foi separada da compra imediata, evitando perder o alvo de reserva quando a composicao ainda nao esta pronta.
- A IA pode guardar dinheiro para elite no proximo turno quando a massa de capturadores ja esta proxima do limiar.
- Fire support elite evita ser a primeira compra de suporte, salvo quando o pivot de composicao realmente pede isso.
- O pivot de dream team continua podendo buscar fire support elite como complemento dos tanks elite.

## Transporte e suporte de fogo

- Transporte courier resolve alvo do passageiro com mais cuidado, distinguindo alvo real de fallback para HQ.
- Passageiros de fire support usam setor atribuido, construcao capturavel ou celula representativa sem confundir posicao atual com chegada.
- APC com artilharia embarcada agora prioriza avancar ate uma distancia util antes de desembarcar.
- O desembarque de fire support considera distancia da artilharia e do proprio transporte ao alvo.
- Shuttle ganhou segunda passada com limiar relaxado para nao ficar ocioso quando ha candidato quase elegivel.
- Transporte rogue evita perseguir capturador planejado que recusaria embarque por setor incompatavel.
- Transporte atribuido pode atacar alvo bloqueador no caminho do pickup quando isso nao prejudica a entrega.

## Roteamento e combate

- Transportadores deixam de cair no fluxo generico de captura quando passam pelo router comum.
- A escolha de alvo de ataque passou a ordenar candidatos e tentar o proximo alvo quando o primeiro e bloqueado por `AttackDecision`.
- Isso reduz casos em que uma unidade com alvo valido deixava de atacar por causa de uma unica preferencia rejeitada.

## Logistica rebocada

- O alvo de entrega para unidade rebocada agora distingue "sem alvo" de celula `(0,0,0)` valida.
- Suporte de fogo rogue procura setores seguros com capturadores antes de cair para HQ inimigo.
- A IA evita deixar artilharia em setor com inimigo visivel muito proximo do ponto de entrega.

## HUD, editor e atalhos

- HUD de construcao ganhou badge de setor para debug da IA quando o AI HUD esta ativo.
- O editor de `UnitData` expoe `aiPurchaseMode` e `eliteFrom`.
- F9 foi reservado para atalhos de AI Debug, evitando conflito com save, tutorial e replay.

## Bloco tecnico curto

- Ajustados `AIShoppingPlanner.cs`, `AIController.Router.cs` e arquivos de `Transportador`.
- Ajustados `AIController.Supridor.Shuttle.cs` e fluxos de entrega rebocada.
- Ajustados `UnitData.cs` e `UnitDataEditor.cs` para `aiPurchaseMode` e `eliteFrom`.
- Ajustados `ConstructionHudController.cs` e `ConstructionManager.cs` para badge de setor no HUD.
- Ajustados `SaveGameManager.cs`, `PanelVisibilityHotkeysController.cs` e `ReplayPanelUI.cs` para reservar F9.
- Assets de unidades, prefab de construcao e cenas receberam configuracoes relacionadas aos novos campos e ao fluxo de debug.

## Resultado

Versao preparada como pacote `AI Refine II`, focada em reduzir decisoes passivas ou incoerentes da IA e em deixar compras, upgrades, transporte e suporte de fogo mais previsiveis durante partidas longas.
