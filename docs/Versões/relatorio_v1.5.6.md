# AI Player Planner

## Resumo
- Evolução do AI Player com foco em base de planejamento.
- Reativação do atalho `F9` para painel de replay.
- Introdução do estado dedicado `Replay` no TurnState para pausar automação e IA enquanto o replay está ativo.

## Alterações principais
- `TurnStateManager`: novo estado `CursorState.Replay`.
- `TurnStateManager.StateMachine`: entrada/saída de estado replay (`TryEnterReplayState`, `TryExitReplayStateToNeutral`).
- `TurnStateManager.Automation`: pausa de automação no estado replay, no mesmo modelo do player menu.
- `AIPlayerController`: espera quando cursor está em `PlayerMenu` ou `Replay`.
- `ReplayPanelUI`: F9 reativado e sincronização com estado replay.
- `PanelVisibilityHotkeysController`: F9 reativado no mapeamento de function keys.

## Validação
- Build `Assembly-CSharp` executado com sucesso (0 erros, 0 warnings).
