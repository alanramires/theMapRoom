# v2.2.26b - Caminhos Validos Fix

## Problema

Regressao introduzida no commit 412705a ("AI Road Booster"): unidades aliadas do mesmo dominio deixaram de ser passiveis de passagem durante o movimento do jogador humano. A unidade movia o cursor em direcao a uma celula alem de um aliado e o cursor ficava travado antes do aliado, sem conseguir alcancar o destino.

## Causa raiz

`enableTotalWar = true` por padrao em `MatchController`, o que ativa `OccupancyResolver.IsLayerAwareRulesActive`. Em 412705a, um `continue;` foi adicionado ao bloco de filtragem de pintura do alcance de movimento (`ApplyMovementRangePaint`) em `TurnStateManager.Range.cs`. O efeito foi correto em termos de regra (aliado nao pode ser destino final), mas removeu o hex aliado do `paintedRangeLookup`.

`TryResolveCursorMove` usa `paintedRangeLookup` para decidir se o cursor pode se mover para uma celula. Com o hex aliado ausente do lookup, o fallback direcional (`HexPathResolver.TryResolveDirectionalFallback`) so verifica vizinhos imediatos da celula atual — nao alcanca a celula alem do aliado. O cursor ficava preso.

## Correcao

`TurnStateManager.Range.cs` — `ApplyMovementRangePaint`:

Quando `CanPaintMovementStopAtCell` retorna falso (hex aliado de mesma banda em modo TW), a celula agora ainda e adicionada a `paintCells`, `paintedRangeCells`, `paintedRangeLookup` e `movementPathsByCell`, mas o fluxo da funcao faz `continue` antes de adicionar ao set definitivo de pintura visual (isso nao muda nada agora pois a pintura visual tambem esta incluida). Na pratica: hex aliado aparece pintado como alcance normal, cursor navega por ele, mas confirmar ali resulta em "Hex ocupado" via `CanEndMove` (TW) ou `FindUnitAtCell` (non-TW) — que rodam antes da verificacao de `paintedRangeLookup`.

Inimigos nao sao afetados pois bloqueiam o BFS desde a travessia (`CanPassThrough = false`) e nunca chegam a `pathsByDestination`.

## Resultado

- Hex de aliado aparece pintado no alcance de movimento (igual ao comportamento anterior a 412705a)
- Cursor navega pelo hex aliado sem travar
- Celulas alem do aliado sao alcancaveis normalmente
- Tentar confirmar no hex aliado continua bloqueado com "Hex ocupado"
