# v4.1.4 - Hex compartilhado, modo observador e alcance 0

Esta versao consolida a apresentacao de partidas observadas, melhora a leitura de unidades em hexes multicamada e introduz suporte a combate de alcance zero no mesmo hex.

## Modo observador e Fog of War

- Partidas AI vs AI passam a usar a apresentacao da visao humana para o observador local, preservando o FOW total sem expor os previews internos de planejamento da AI.
- Cursor, unidade em acao, linha de movimento e demais elementos da execucao observada mantem a ordem visual correta sobre os tampoes do Fog of War.
- A apresentacao temporaria de unidades inimigas em movimento permanece continua quando elas atravessam a fronteira da nevoa e entram em uma area detectada.
- Range maps e linhas de apoio da AI continuam ocultos, enquanto caminhos ja comprometidos podem ser apresentados ao observador.
- A pausa de desenvolvedor por F10 permite abrir o MenuRoot com ESC ou clique direito durante o turno da AI.
- F11 e F12 respeitam a configuracao do Match Controller e nao entregam um time humano ao controlador da AI.

## Hex compartilhado e apresentacao multicamada

- Submarinos visiveis em `Submarine/Submerged` dividem visualmente o hex com unidades de superficie, reutilizando o layout de coabitacao de aeronaves e veiculos.
- O submarino revelado ocupa a faixa superior e a unidade de superficie a faixa inferior, com escala e offsets puramente visuais.
- Quando o submarino deixa de ser visivel para o observador, o layout volta ao estado normal no proximo refresh confirmado.
- A ordenacao frontal de unidades submersas em hex multicamada continua preservada para os demais casos de empilhamento.
- HUD, indicadores de camada e configuracoes visuais de coabitacao receberam ajustes para melhorar a leitura das unidades compartilhando o mesmo hex.

## Alcance 0 e combate no mesmo hex

- O `PodeMirarSensor` aceita armas com alcance minimo e maximo zero como armas validas contra unidades na mesma celula.
- O mapa de distancia passa a reconhecer raio zero no fluxo de escolha de alvo, eliminando o falso motivo de fora de alcance nesse caso.
- As regras de revide continuam separadas do ataque deliberado; alcance zero nao e convertido antecipadamente em alcance um.
- A Fragata recebeu ajustes de dados para os experimentos com mina naval e combate multicamada.
- O painel `CONFIRMAR ATAQUE` agora mostra explicitamente a arma escolhida no passo anterior.

## Animacoes e efeitos

- O efeito de hit naval existente passa a ser selecionado pelos pares ativos `Naval/Surface` e `Submarine/Submerged`.
- Unidades fora desses dominios continuam usando os frames convencionais de taking hit.
- O fallback por classe foi removido para impedir splash naval em uma unidade operando fora da camada aquatica.

## Interface, cenas e documentacao

- Menu de batalha, painel de dinheiro, prefab de unidade e cenas de mapa receberam atualizacoes de configuracao.
- Fontes TMP e materiais fallback foram atualizados para os novos textos e glifos usados pela interface.
- O manual tecnico e a critica de The Map Room foram revisados e ampliados.

## Contrato transacional

- Offsets de coabitacao, visibilidade durante animacoes e elevacoes de sorting sao apenas apresentacao temporaria.
- Posicao logica, ocupacao, FOW, deteccao e memoria confirmada continuam sendo atualizados somente apos o compromisso da acao e o retorno a `CursorState.Neutral`.

## Validacao

- Assembly principal compilado com sucesso, sem erros.
- Diff revisado; avisos de whitespace correspondem a campos serializados do Unity e quebras Markdown preservadas nesta versao.
