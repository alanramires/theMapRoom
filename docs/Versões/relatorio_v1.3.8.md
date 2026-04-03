# RELATORIO v1.3.8

Data: 2026-03-20
Tema: replay fixes parte 1

## Objetivo desta versao

Corrigir inconsistencias do replay por pilha (buffer/acoes/snapshots), estabilizar a restauracao visual de unidades e reforcar controles de execucao durante modo replay.

## Entregas

1. Estrutura e execucao de replay
- Bloqueio de gravacao enquanto `isReplaying == true` no `ReplayManager`.
- Ajuste de resolucao de origem de acao para priorizar `MoveFrom` antes de `CursorHex`, reduzindo deslocamento intermediario indevido do cursor.
- Logs de diagnostico adicionados para trilha de deslocamento do cursor (`[Replay][CursorTravel]`) nas fases pre-batch, origem e destino.

2. Snapshot/restauracao e estado de unidade
- Ajustes no `UnitManager` para auditoria de flags:
  - `isDead`, `deadWhenTurn`, `deadByReason`, `diedByUnit`
  - `hasMerged`, `mergedWhenTurn`, `mergedWithUnit`
- `diedByUnit` padronizado para priorizar `unitId` (rastreabilidade).
- Correcao de restauracao visual: unidade revivida por snapshot volta com sprite/HUD/UI habilitados.
- Motivos de morte conectados em fluxos principais:
  - combate
  - falta de combustivel/queda aerea
  - morte por transportador
  - comando destroy unit
  - fusao

3. UI e fluxo de replay
- `ReplayPanelUI` mantido com botoes de controle ativos (`Start`, `Back`, `Play`, `Pause`, `Forward`, `Stop`).
- Selecao de visao por time especifico mantida e aplicada via `ReplayVisionMode`.

4. Logs por sensor no MatchController
- Novas flags por sensor no `MatchController` e `MatchControllerEditor`:
  - `PodeMirar`, `PodeEmbarcar`, `PodeDesembarcar`, `PodeCapturar`, `PodeFundir`, `PodeSuprir`, `PodeTransferir`, `ServicoDoComando`, `PodePousar`.
- Introduzido `SensorLogGate` para centralizar leitura de flags e emissao de logs por sensor.
- Sensores principais conectados ao gate com logs resumidos de entrada/resultado.

## Impacto esperado

- Menos gravacao indevida durante replay.
- Melhor previsibilidade de cursor na reproducao de batches.
- Melhor auditabilidade de morte/fusao por unidade.
- Diagnostico mais rapido de sensores e deslocamento do replay via logs dedicados.

## Observacoes

- Esta versao foca correcao estrutural e instrumentacao (logs) para fechar regressao de replay.
- Proxima parte recomendada: consolidar/remover trilha legada de `ReplayTurnRecord.Steps` onde aplicavel.
