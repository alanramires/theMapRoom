# v4.0.22 - Life Quality update

Esta versão reúne melhorias de leitura, inspeção e confirmação de ações, com foco em reduzir erros de clique e tornar as regras do mapa mais fáceis de descobrir.

## Inspeção de unidades e mapa

- O Inspect Unit agora mostra imagem, nome do local e defesa numérica da posição.
- Informações de local respeitam construção, estrutura e terreno predominantes.
- Unidades no ar usam nome, tile e DPQ de `DPQAirHeightConfig`, conforme a altura atual.
- Submarinos submersos passam a usar `subDisplayName`, `subTile`, `subDpq` e `subEv` configurados no mesmo asset.
- Em partidas com Fog of War, o Inspect Unit mostra alcance de visão, especializações por camada e skills de detecção correspondentes.
- Hexes vazios podem ser inspecionados ao manter o cursor parado; o painel fecha por movimento, entrada do jogador ou tempo limite.
- O Inspect Building também exibe imagem do local e defesa depois dos dados de estoque e serviços.
- Novos tiles visuais representam baixas altitudes, altas altitudes e camada submarina.

## Shopping

- Unidades sem saldo suficiente continuam visíveis, mas aparecem em cinza.
- Opções sem dinheiro ficam desabilitadas para clique, preservando a navegação e a leitura do catálogo.

## Fim de turno

- O atalho `R` passa a vez imediatamente.
- A AI rápida usa o mesmo caminho direto.
- O botão `Rodada` do menu e o botão flutuante abrem uma etapa de confirmação no `panel_helper`.
- A AI visível apresenta a confirmação antes de encerrar o turno.
- Foi removido o caminho legado que podia marcar uma confirmação sem entrar no estado `EndingTurn`.

## Estatísticas e interface

- Perdas não causadas por combate, incluindo doadores consumidos em fusão, passam a ser contabilizadas sem conceder kill ao adversário.
- A fonte VT323 e seu fallback foram atualizados para os novos textos e símbolos da interface.
- O planejamento inicial do tutorial foi registrado em `docs/tutorial/planejamento.md`.

## Validação

- `Assembly-CSharp.csproj`: build sem erros.
