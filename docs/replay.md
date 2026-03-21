# Replay

Documento canonico do sistema de replay atual (versao por pilha de acoes + snapshots).

## Principio

O replay emula a jogada como runtime, nao faz "teleporte de resultado".
Ele reproduz inputs gravados por batch e usa snapshots como pontos de ancora.

Timeline conceitual:
- `snapshot#0` (inicio do turno/jogo carregado e liberado)
- `action#1` (batch confirmado)
- `snapshot#1`
- `action#2`
- `snapshot#2`
- ...

## Estruturas principais

- `ReplayManager.currentBuffer` (`PlayerAction`): buffer volatil da acao atual.
- `ActionStack.Actions`: pilha persistente de batches confirmados.
- `PlayerAction.Snapshot`: snapshot pos-acao (estado depois do batch).
- `ReplayTurnRecord.StartSnapshot`: snapshot inicial do record (equivalente ao `snapshot#0` da janela gravada).

Observacao: `ReplayTurnRecord.Steps` legado ainda existe para compatibilidade, mas o caminho ativo prioriza `ActionStack` + snapshots por acao.

## Regra de gravacao

1. Enquanto jogador esta escolhendo (unidade, destino, sensor, alvo), tudo fica no buffer volatil.
2. Se cancelar antes de confirmar, buffer eh descartado.
3. Ao confirmar a acao, grava o batch (`PlayerAction`) na pilha.
4. Ao concluir execucao/animação e voltar para neutro, grava snapshot pos-batch.
5. Durante replay (`isReplaying = true`), gravacao de batches/snapshots fica bloqueada.

## Start snapshot (`snapshot#0`)

- Partida nova: grava quando o jogo termina validacoes iniciais e libera o cursor em neutro.
- Load game: reaproveita `StartSnapshot` carregado quando turno/time batem com runtime.

## Execucao de playback

Controles principais do painel:
- `Start`: inicia sessao conforme `ReplayStartMode`.
- `<<` (Back): volta por snapshot sem executar batch.
- `>` (Forward): executa 1 batch e avanca para snapshot seguinte.
- `Play`: executa batches em sequencia.
- `Pause`: nao interrompe batch no meio; pausa no proximo limite de snapshot.
- `Stop`: encerra replay e retorna ao estado da partida atual (snapshot pre-replay).

## Modos de inicio (`ReplayStartMode`)

- `FromBeginning`: comeca no snapshot inicial.
- `FromCurrentTop`: comeca do topo atual gravado.
- `FromSpecificTurnTeam`: tenta iniciar por turno/time especificos.

## Visao do observador

No `ReplayPanelUI`:
- `viewUnderSpecificTeam = -1`: visao livre/omnisciente (qualquer time).
- `0..3`: visao filtrada por time.

Internamente:
- `ReplayVisionMode.Omniscient` para `-1`.
- `ReplayVisionMode.TeamFiltered` para time especifico.

## Execucao por input (batch)

Para acoes suportadas, o replay emula entrada:
- seleciona unidade
- move cursor/confirmacoes
- executa sensor e substeps gravados
- aguarda eventos de sincronizacao do runtime:
  - `TurnStateManager.OnSensorsReady` dentro do batch de movimento (quando aplicavel)
  - `CursorController.OnCursorReturnedToNeutral` para fechamento do batch e avancar no `Play`

Acoes suportadas no pipeline ativo:
- `UnitAction`
- `Shopping`
- `CommandService`
- `RemoveUnit`

Sensores/fluxos cobertos no batch de unidade:
- Attack, Embark, Disembark, Capture, Merge, Supply, Transfer, Land

Shopping tem navegacao de menu emulada por indice gravado:
- `ShoppingSelectedIndex`
- `ShoppingUnitTypeId`
- delay configuravel: `shoppingNavDelay`

Observacao de fluxo:
- `Shopping`, `CommandService` e `RemoveUnit` nao dependem de `OnSensorsReady`.
- Nesses fluxos, o replay espera apenas retorno para `Neutral` ao final da execucao.

## Save/load

Replay persiste no save (`ReplaySaveData`):
- `matchHistory`
- `currentRecord`
- `actionStack`
- configuracoes de visao/observador

Cada acao pode carregar seu snapshot associado, permitindo voltar por snapshot sem reexecutar lote.

## Arquivos-chave

- `Assets/Scripts/Replay/ReplayManager.cs`
- `Assets/Scripts/Replay/PlayerAction.cs`
- `Assets/Scripts/Replay/ActionStack.cs`
- `Assets/Scripts/UI/Replay/ReplayPanelUI.cs`
