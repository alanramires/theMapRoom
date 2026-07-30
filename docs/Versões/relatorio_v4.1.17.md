# Ajustes gerais de estrutura e AI Rebelde

Versao: v4.1.17
Status: em validacao no Unity

## Resumo

- Nasceu o comportamento de jogo da **facção sem QG (rebeldes)**: captura por proximidade, sem
  plano, cabeando o mesmo criterio geografico por todas as vias de entrega (a pe, APC, aereo, naval).
- Novo caminho de decisao para o **transporte naval** da IA — o fluxo terrestre nunca alcança um
  objetivo em terra, entao o navio passa a mirar o PONTO DE ENCONTRO com a costa, nao o objetivo.
- Regras de tabuleiro por **camada do hexagono** deixaram de depender do modo FOW: os tres andares
  (aereo / superficie / submerso) valem em toda partida; só o compartilhamento entre inimigos segue
  exclusivo do Total War.
- Novo vocabulario de **estrutura+terreno**: conves da ponte separando terra de agua, veto de camada
  por par, e exigencia de **rota declarada** (trilho segue a linha; estrada de montanha vira
  desfiladeiro).
- Correção de performance de logistica em **mapas grandes** (peneira de zona de atendimento antes do
  sensor de suprimento) e limpeza do traçado de rodovia (aresta ida-e-volta nao dobra sprite).

## AI Rebelde (facção sem QG)

- **Curto-circuito antes do planner** (`AIController.Router` → `AIController.Rebel`): o plano normal
  nasce do proprio QG e aponta para o inimigo. Sem QG, todo capturador cai por todos os buracos e
  vira "rogue rumo ao QG inimigo" — um funil unico que ignora o mapa. `TryDecideRebelAction` roda
  antes do plano e troca a ESCOLHA do alvo: nao ha eixo, ha proximidade. Cada unidade captura o
  capturavel mais perto; se ja ha rebelde a caminho, varre a bolha seguinte. Reusa os mesmos
  sensores e batches de captura — só o alvo muda.
- **Coordenação sem empilhamento** (`FindNearestRebelCaptureTarget`): pula prédio com aliado ja em
  cima (persiste entre turnos) ou reservado por outro rebelde nesta passada da Fase 2
  (`plannedDestinations`). Dois rebeldes nunca marcham para o mesmo prédio.
- **Mesmo criterio quando a tropa e CARGA** (`AIController.Transportador.Courier.Passengers`): o
  passageiro rebelde nao tem slot de plano; sem tratamento, APC/Chinook/navio o entregariam no lugar
  errado (funil do QG). O alvo do passageiro passa a ser o capturavel mais proximo pelo mesmo
  buscador. Sem capturavel a vista, nao inventa alvo — deixa o transporte esperar.
- Cascata coerente com o arquetipo derivado (`IsHeadQuarterlessTeam`): captura livre, nunca produz,
  imune a derrota por 0 unidades. Ver `docs/arquitetura` e o relatorio de nascimento do arquetipo.

## Transporte naval da IA (novo)

- **`AIController.Transportador.Naval`** — caminho proprio, pelo mesmo motivo do aereo: o fluxo
  terrestre rota ATE o objetivo, e um navio nunca alcança um objetivo em terra (o mapa de custo
  reverso nasce vazio e o navio oscila na costa). A correção nao e um alvo diferente, e uma PERGUNTA
  diferente — o navio mira o **ponto de encontro com a terra**:
  - **entrega** → celula de agua de onde o desembarque e valido, escolhendo a que deixa o passageiro
    mais perto do objetivo (o sensor `PodeDesembarcar` manda; chegar a costa certa ja e o trabalho);
  - **coleta** → celula de agua onde o navio pode RECEBER passageiro (praia/porto, conforme a propria
    ficha), mais perto de quem espera embarque.
- Resolvido o ponto de encontro, ele vira alvo de rota — agora alcancavel — e `FindTransportMove` +
  courier voltam a funcionar sem alteração. Compatibilidade de vaga via `FindFittingSlotIndex`
  (mesmo do shuttle aereo); ponto de encontro via `PodeEmbarcarSensor.IsTransporterCellValidForEmbark`.
- Integra com a rebelde: sem setor atribuido, o objetivo de entrega tambem e o capturavel mais
  proximo, nunca o QG inimigo.

## Regras de tabuleiro — camadas independentes de FOW

- **Camadas valem em toda partida** (`OccupancyResolver`): os tres andares do hexagono sao regra de
  TABULEIRO — aviao sobrevoa tanque, submarino navega sob navio, tanque para no conves com navio
  embaixo. FOW e cobertura de INFORMAÇAO, nao um modo de regras; modos sem FOW apenas revelam o
  tabuleiro. `IsLayerAwareRulesActive` deixa de exigir Total War.
- **O que E exclusivo do Total War**: dois INIMIGOS dividirem a MESMA banda (hex disputado/dogfight).
  Fora dele, a banda comporta uma presença só, aliada ou inimiga (`AllowsEnemyShareInSameBand`).

## Estrutura + terreno — conves, veto de camada e rota declarada

- **Conves da ponte separa terra e agua** (`StructureData.separaConvesEAgua`,
  `OccupancyResolver.DeckSeparatesFromWater`): no par marcado, Land/Surface e Naval/Surface deixam de
  ser o mesmo andar — tanque em cima, navio embaixo, coexistindo. Única exceção ao "superficie e
  superficie". Marcado na ponte sobre MAR (ha vao); desmarcado sobre PRAIA (a ponte encosta no chao,
  sem vao — tanque e navio disputam a mesma vaga).
- **Veto de camada por par** (`StructureData.bloqueiaNaval` / `IsLayerBlockedAt`): roda antes de
  qualquer concessao — nem dominio nativo, nem adicional, nem terreno base valem se o par proibiu.
  Marcado em Ponte+Praia (cabeceira da ponte: aterro/estacas, nao agua navegavel).
- **Vao sob a ponte corrigido** (`UnitMovementPathRules.CanTraverseUsingStructure`): a camada
  ADICIONAL (passar por baixo) agora (1) exige que o TERRENO base suporte a camada — ponte sobre
  planicie nao cria agua, entao submarino nao navega sob um campo; (2) governa por SKILL só quem anda
  POR CIMA, nao quem passa por baixo — era por isso que o navio passava sob a ponte rodoviaria e nao
  sob a ferroviaria.
- **Rota declarada** (`StructureData.exigeRotaDeclarada` / `rotaDeclarada` por par /
  `exigeEstruturaNaConstrucao`; gate em `UnitMovementPathRules`): quando ativa, a unidade só entra na
  celula vindo de um hex que seja o par consecutivo dela numa `RoadRouteDefinition` — nao basta a
  estrutura estar pintada. Modela o **trilho** (o trem segue a linha) e a **estrada de montanha**
  como desfiladeiro (só se sobe a serra pela boca da estrada). Detalhes:
  - E um AND sobre a travessia normal, nao atalho: continua pagando custo e obedecendo terreno.
  - Falhar no gate NEGA A ESTRUTURA, nao o hex: quem nao veio pela rota perde o beneficio da via mas
    ainda entra se o TERRENO por baixo aceitar (o soldado cruza um trilho na planicie; o alpino sobe
    a montanha mesmo com estrada passando).
  - Construcao no hex normalmente encerra a exigencia (cidade na serra liberta), salvo
    `exigeEstruturaNaConstrucao` — o trilho, onde a cidade só e alcancavel se a linha chegar ate ela.
  - Global por estrutura (marca a Linha de Trem de uma vez) com override por par Estrutura+Terreno
    (Rodovia livre na floresta, canalizada na montanha).

## Performance e polimento

- **Zona de atendimento da logistica** (`AIController.Logistics.Supply.BuildLogisticsServiceZone`):
  o laço rodava um `PodeSuprir` completo por celula alcancavel — num mapa grande, centenas de
  varreduras por caminhao, quase todas sobre hexes vazios. Agora uma peneira barata (hexes das
  unidades aliadas + vizinhos) filtra antes do sensor. Deliberadamente FROUXA (inclui quem ja
  recebeu / nao precisa); quem responde "da para atender?" continua sendo o `PodeSuprir`. Devolve
  null sem candidato algum, preservando o comportamento antigo.
- **Traçado de rodovia sem sprite dobrado** (`RoadNetworkManager.CreateRouteSegments`): arestas ja
  desenhadas sao normalizadas por sentido (A→B = B→A). Traçar a volta de uma via (A→B→C→B→D, para
  sair do entroncamento noutra direcao) e tecnica legitima e deixa de custar sprite dobrado ou
  escurecer o traçado por soma de alfa.
- **Beep da confirmação de fim de turno tambem no modo visivel da IA** (`TurnStateManager.StateMachine`,
  `CursorController`): o som pertence a ABERTURA do painel, nao a quem pediu. Movido para
  `TryOpenEndingTurnConfirmation`, soa quando o humano abre pelo menu e quando a IA abre o mesmo
  painel no replay — o silencio da IA deixa de parecer bug.
- **SFX de manobra parada que muda de camada** (`TurnStateManager.Movement`): mover parado nao
  percorre caminho e nao disparava o SFX de movimento. Quando a parada MUDA a camada (submarino que
  mergulha, aeronave que sobe) houve manobra de verdade — comparando a camada no fim, o som toca só
  quando algo mudou.

## Pendencias

- Otimização da Rebelde em mapa gigante: `FindNearestRebelCaptureTarget` varre `AllActive` por
  unidade (O(unidades×prédios)); e `BuildObjectivePlan`/`TacticalAnalyzer.Rebuild` rodam todo turno
  mesmo o rebelde nao usando nenhum dos dois. Colher `[AI Perf] PRE-Stage2` vs `Stage2` de um turno
  antes de decidir onde cortar.
- Rebelde com 0 unidades continua "vivo", toma turnos vazios e trava o `TryDeclareLastStandingWinner`
  (herdado do arquetipo).
- Caminhao rebelde só recarrega em supridor Hub (perde o ramo `CanProduceUnits` de
  `IsLogisticsReloadConstruction`).
