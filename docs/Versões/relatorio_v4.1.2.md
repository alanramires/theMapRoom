# v4.1.2 - Fow Partial fixes

Esta versao consolida o FOW parcial, corrigindo a fotografia do terreno conhecido, a restauracao por save/load e a apresentacao das acoes sob nevoa.

## Fotografia do terreno conhecido

- A memoria visual passa a incluir quebramares, estradas, trilhos e demais estruturas de terreno.
- Quebramares usam os tiles e matrizes reais da camada original, sem recalcular conexoes a partir de vizinhos incompletos.
- Trechos de estrada que cruzam a fronteira entre terreno conhecido e desconhecido sao recortados no limite do hex, evitando vazamento para fora da nevoa.
- Construcoes continuam representadas pela ultima fotografia confirmada, sem expor alteracoes ocorridas fora da visao.
- A composicao e o empilhamento visual de construcao e unidade sao atualizados quando a fotografia entra ou sai de cena, evitando HUDs concorrentes.

## FOW, debug e save/load

- Os comandos `fow on`, `fow off` e `fow partial` agora limpam e reconstroem todas as camadas da fotografia, inclusive em chamadas repetidas.
- O carregamento de partida recalcula o FOW confirmado e remove sobrescritas visuais temporarias remanescentes.
- Unidades reais permanecem escondidas em terreno apenas conhecido; somente a fotografia confirmada do mapa e das construcoes continua visivel.
- Estradas e quebramares preservam corretamente sua geometria apos load e reconstrucoes manuais do FOW.

## Movimento e modo observador

- No FOW total, range map, linhas de apoio e outros indicadores provisórios ficam ocultos para nao atravessarem a fotografia conhecida.
- Movimento e desembarque do jogador humano permanecem temporariamente acima do FOW durante a apresentacao da acao.
- Em partidas AI vs AI, o observador tambem acompanha movimento e desembarque sobre a fotografia conhecida.
- O acompanhamento de camera do cursor foi restaurado depois de carregar partidas AI vs AI.

## Sensores e informacao conhecida

- `PodeCapturar`, `PodeDesembarcar` e `PodeTransferir` aceitam terreno conhecido como informacao geografica valida.
- `PodeMirar` continua exigindo alvo em hex atualmente visivel.
- `PodeFundir`, `PodeSuprir` e `PodeEmbarcar` preservam suas regras anteriores.

## Ajustes de IA

- Artilharia elite passa a ter prioridade efetiva sobre alvos nao elite dentro da mesma classe de preferencia.
- A avaliacao de compras diferencia demanda antitanque de demanda generica por fogo indireto, permitindo que lancadores de foguetes disputem compras quando houver alvos adequados.
- Foram adicionados ajustes auxiliares de planejamento, pressao de compra e politica lateral da IA.

## Contrato transacional

- Memoria, visao e fotografia definitiva continuam sendo atualizadas apenas depois do compromisso da acao e do retorno a `CursorState.Neutral`.
- Apresentacoes temporarias de movimento e desembarque nao alteram a verdade confirmada do tabuleiro.

## Validacao

- Assembly principal compilado com sucesso, sem erros.
- Alteracoes verificadas com `git diff --check`.
