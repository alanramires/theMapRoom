# AI Rebelde tunning

Versao: v4.1.18
Status: em validacao no Unity

## Resumo

- Sessao focada em **performance do turno da IA em mapa gigante** (com IA Rebelde) e no
  **afinamento da economia rebelde**. O turno saiu de minutos para segundos.
- Diagnostico guiado pelos logs `[AI Perf]` / `[FoW][Perf]` / `[AI Perf][Unit]`: o gargalo NAO
  era o cerebro da IA — era Fog of War recolhido inteiro por acao, e pathfinding naval condenado.
- Rebelde agora **compra** em predios capturados marcados, com **doutrina propria** (so capturador).

## Fog of War — refresh por captura (Fase 2)

- **Full refresh de FoW so em captura/predio** (`AIController.Phase2`): antes, cada acao de unidade
  disparava `RefreshFogOfWarForActiveTeam(DataOnly)` = recolhimento COMPLETO das ~35 unidades
  (~3,8s), porque a chave do cache de visao inclui o `globalBoardRevision`, que sobe a cada passo de
  qualquer unidade → todos perdem cache. Argumento decisivo: `CommitAIWorldLightAfterAction` e
  EXCLUSIVO da IA — o humano nunca passa por ele e enxerga certo com o caminho **incremental (delta
  confirmado)** que ja roda no commit da acao. O full refresh por movimento era redundante. Agora so
  captura (que muda visao de CONSTRUCAO, fora do delta do movedor) dispara o refresh completo.
  Movimento/ataque confiam no delta. Ganho: Fase 2 de ~120s para ~40s no mapa de teste.

## Performance naval — FireSupport / routeDistance

O `FireSupportReposition` de unidades navais chegava a **73s** numa unica decisao (`routeDistance`
chamado 1689–4486×, ~12ms cada em pathfinding sobre mar). Cinco tecnicas empilhadas, todas sem
mudanca de comportamento:

- **Memo de route distance** (`AIController.Capturer.Attack`): `TryCalculateRouteDistance`
  memoizado por `(unidade, from, to)` dentro de uma revisao de tabuleiro. Colapsa milhares de
  chamadas identicas por decisao.
- **Hoist do custo de origem** (`AIController.Progression`/`ProgressionSelector`): o
  `CalculateMovementCostMap(origin)` — invariante — era recomputado por candidato. Agora calculado
  uma vez e passado (`costFromOrigin`), como o irmao `TryScoreTwoTurnProgression` ja fazia.
- **Top-K no two-turn** (`ProgressionSelector`): a 2ª jogada (`CalcularCaminhosValidos(firstStop)`,
  cara em naval) so roda nos **12** candidatos mais promissores por progresso de 1º turno; os demais
  sao pontuados so pelo 1º turno. Unico tradeoff de comportamento (um candidato fora do top-12 nao
  ganha bonus de lookahead) — em conjuntos <=12 (unidades terrestres) o comportamento e identico.
- **Mapa reverso de distancia** (`SectorManager.TryBuildLandMovementDistanceToTargetMap`, aditivo):
  1 Dijkstra reverso a partir do target substitui N buscas ponto-a-ponto no loop interno do
  two-turn. Bit-a-bit identico: custo por-no em grafo nao-direcionado → o caminho minimo minimiza o
  mesmo somatorio nos dois sentidos, `D(cell→target) = D(target→cell) + enterCost(target) −
  enterCost(cell)`. Inspirado no proprio Waypoint da ferramenta `CaminhosValidosWindow`, que ja usa
  o padrao (`CalculateMovementCostMap` a partir do destino). `CalcularCaminhosValidos` e a ferramenta
  ficaram **intactos**.
- **Early-out no destino intransponivel** (`SectorManager.TryComputeLandMovementDistance`): o custo
  REAL do naval era um alvo em TERRA — o navio nunca roteia ate la, mas cada busca varria o mar
  inteiro ate estourar o teto para so entao cair em `HexDistance`. Agora `if (!TryGetLandEnterCost(to))
  return false;` falha em microssegundos. Resultado identico (false), instantaneo. Cobre o caso de
  mar aberto (mais celulas alcancaveis → mais buscas condenadas).

Resultado combinado: Destroyer FireSupport de **73s / 15s / 9s → ~0,5s**.

## AI Rebelde — economia da insurgencia

- **Flag `allowRebelAIPurchase`** (`ConstructionData`, default false): a faccao sem QG nunca produz —
  esta flag e a excecao renegada. Um predio marcado permite que o rebelde que o capturar compre ali,
  **ignorando as regras de dono** (OriginalOwner/FirstOwner — rebelde nunca e o dono original do que
  toma); so `sellingRule=Disabled` ainda barra. Ligada no ponto unico de producao
  (`ConstructionManager.CanProduceUnitsForVisualTeamAfterSlotValidation`). Nao afeta times com QG.
- **Custom editor** (`ConstructionDataEditor`): a flag aparece logo abaixo de *Selling Rules*, com
  HelpBox explicando a excecao.
- **Doutrina de compra so-capturador** (`AIShoppingPlanner.Demand.BuildRoleShoppingDemands`): o
  rebelde retorna cedo com demanda **so de Capturador**, sem o pacote de composicao 2/2/1, sem
  elite/ar/intel/counter. Motivo: o prédio renegado so vende capturador basico, e a formula de
  composicao (`packages*2 − capturers`) zerava a demanda de capturador quando ja havia capturador
  demais → carrinho vazio, caixa preservado. Agora a demanda casa com a oferta e o rebelde compra 1
  capturador/turno (limite do produtor) para alimentar a insurgencia.

## Cursor (apresentacao rapida)

- **Teleporte estilo LClick em fast mode** (`ReplayManager.MoveCursorToCellWithTravel`): no fast mode
  o cursor varria os hexes intermediarios a 1 frame cada (com frames rapidos virava borrao + rajada
  de SFX de cursor, um por celula). Agora salta direto para o alvo num unico passo — como um LClick
  humano na unidade. O glide celula-a-celula fica so no modo cinematico (nao-fast), onde o delay por
  passo o torna assistivel.

## Presets

- `AIPreset_Gastadora` (novo) substitui `AIPreset_Baseline` — perfil "gastadora" para testar economia
  agressiva. `AIPresetData` segue **Fase 1 (inerte)**: nenhuma decisao le do preset ainda.

## Pendencias

- **Rebelde como doutrina de preset (Fase 2)**: a demanda so-capturador e hoje um gate hardcoded por
  `IsHeadQuarterlessTeam`. O destino certo e um `AIPresetData` de insurgencia (`coreMinAssault=0`,
  etc.) quando a Fase 2 ligar as consultas ao preset.
- **Full refresh de FoW residual**: captura, turn-start (`CommitAIWorldHeavy`, ~4,4s no pre-shopping)
  e spawn ainda pagam o recolhimento O(unidades) pela mesma chave `globalBoardRevision`. Fix
  estrutural (chave por-unidade / revisao so-estrutural) fica para depois.
- **Alvo-agua inalcancavel**: o early-out cobre alvo em terra; um alvo em agua inalcancavel (raro)
  ainda faria a busca reversa completa. Se pesar, limitar o raio de expansao.
