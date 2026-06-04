# v2.3.4 - AI Caminhos Validos

## Objetivo

Consolidar os ajustes recentes da AI para usar a ferramenta de caminhos validos como referencia de movimento, especialmente nos casos em que estrada, custo real de terreno e reposicionamento tatico mudam a melhor celula.

## Principais mudancas

- `TryScoreToolRouteProgression` passou a ser usado em decisoes que antes dependiam de heuristicas locais.
- Ajustes em assalto, fogo de suporte, reparo e logistica reduziram movimentos curtos ou laterais quando a ferramenta indicava progressao melhor.
- Fire support alocado em plano agora tenta reposicionar para colocar alvo conhecido na mira antes de cair no reposicionamento generico.
- O custo de movimento do range-step de fire support passou a consultar `CalculateMovementCostMap`, respeitando melhor estrada e celulas alcancadas por bonus.

## Rally Points e Setores

- `isRallyPoint` agora e persistido no save/load, assim como `isForwardObserverSpot`.
- Saves antigos inferem `isRallyPoint=true` quando a construcao ja possuia target slot de rally.
- A validacao de rally target passou a considerar slots de HQ inimigos pelo slot da partida, evitando falso negativo por estado runtime do dono da construcao.
- Assault rogue ganhou fallback para segurar setor recem-capturado quando o plano ainda nao foi reconstruido como `RallyAssembly`.

## Compras e Composicao

- Shopping ganhou leitura de paridade blindada para reservar/comprar assalto pesado quando o inimigo tem vantagem em blindados.
- Ajustes defensivos reduzem compras pequenas repetitivas quando a leitura estrategica pede unidade de impacto.

## Resultado esperado

- A AI deve escolher celulas mais coerentes com o que aparece em `Tools > Transporte > Caminhos Validos > Progressao`.
- Unidades com `play conservative` e preferencia por alcance maximo devem evitar vanguarda desnecessaria e buscar posicao de tiro segura.
- Rally points capturados devem funcionar como ponto de concentracao antes do avanco final, mesmo quando a captura ocorre no meio da Fase 2.

## Validacao

- Build local: `dotnet build Assembly-CSharp.csproj --no-restore`.
- Testes manuais em Battle Map focando fire support, assault rogue, rally points, save/load e progressao por estrada.
