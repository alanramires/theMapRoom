# v4.1.1 - FOW parcial

Esta versao introduz memoria geografica por time e transforma o Fog of War em uma representacao parcial da ultima informacao confirmada pelo jogador, preservando o mundo real sob duas camadas visuais independentes.

## Memoria de exploracao

- Cada time passa a manter seu proprio conjunto permanente de hexes explorados.
- A memoria registra somente coordenadas reveladas por visao confirmada.
- Movimento provisorio, animacao e cancelamento nao atualizam exploracao nem inteligencia.
- A memoria de exploracao e persistida e restaurada pelo save/load.
- Foram adicionadas consultas de runtime para saber se uma celula foi explorada e quantas celulas cada time conhece.

## FOW parcial em duas camadas

- Criada a sorting layer `FogOfWarTile`, posicionada acima do mundo real e abaixo de `FogOfWar`.
- `FogOfWar` continua sendo o tampao superior: vazio em celulas visiveis, mais leve nas exploradas e completo nas desconhecidas.
- `FogOfWarTile` apresenta uma copia opaca do terreno conhecido, impedindo que unidades, barras, efeitos ou movimentos reais vazem pela transparencia.
- O tilemap de memoria e criado e alinhado automaticamente em runtime.
- O multiplicador visual de nevoa explorada pode ser ajustado em `FogOfWarController`; o padrao e `0.8`.
- A unidade em movimento provisorio preserva a apresentacao existente acima do FOW, sem transformar o destino cancelavel em conhecimento confirmado.

## Memoria de construcoes

- Construcoes observadas passam a possuir uma fotografia confirmada por time.
- A fotografia guarda construcao, coordenada, ultimo dono conhecido e orientacao visual.
- Construcoes memorizadas sao copiadas para `FogOfWarTile`, sem copiar barras atuais de HP ou captura.
- Alteracoes de dono realizadas fora da visao nao recolorem a fotografia antiga.
- Uma nova observacao confirmada substitui a fotografia desatualizada pela realidade atual.
- A memoria de construcoes tambem e persistida no save/load.

## Sensores e informacao

- `PodeCapturarSensor` deixa de consultar ou revelar construcoes nunca conhecidas em celulas fora da visao.
- Construcoes ja memorizadas continuam servindo como inteligencia geografica, ainda que seu estado possa estar desatualizado.
- Ocupacao e pathfinding continuam calculados sobre o tabuleiro real; por isso um caminho pode denunciar um obstaculo sem revelar visualmente sua identidade.
- `PodeDesembarcarSensor` ignora aeronaves em voo como ocupantes do espaco fisico de desembarque, mantendo aeronaves pousadas como bloqueadoras.

## Progressao de construcoes e unidades

- `ConstructionData` e `UnitData` receberam requisito de construcao.
- O historico de edificios capturados e mantido por time e considera construcoes que ja comecam sob seu controle.
- Captura, construcao e compras respeitam os pre-requisitos configurados.
- Itens bloqueados permanecem listados no shopping com indicacao compacta do edificio requerido.

## Audio e Inspector

- `CursorController` recebeu master de SFX e volumes individuais de abertura e fechamento de menu.
- `MatchMusicAudioManager` recebeu volume individual da musica de rodada, subordinado ao master de musica.
- Os audios do painel de rodada foram encaminhados aos respectivos masters.
- Regras de skill por terreno exibem o nome do terreno no Inspector no lugar de `Element N`.

## Contrato transacional

- Exploracao, memoria visual, dono conhecido e contatos so sao publicados a partir do snapshot confirmado.
- O refresh definitivo permanece condicionado ao compromisso da acao e ao retorno para `CursorState.Neutral`.
- Cancelar um movimento nao deixa terreno, construcao ou contato residual na memoria do time.

## Validacao

- Assembly principal compilado com sucesso, sem erros.
- Assembly do Editor compilado com sucesso, sem erros.
- Arquivos alterados passaram por `git diff --check`.
