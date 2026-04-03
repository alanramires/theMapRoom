# Relatorio v1.4.0

Data: 2026-03-20
Versao: v1.4.0
Resumo: Replay parte 3 com hardening de runtime, watchdog de timeout e melhorias de UX/seguranca em fluxo de replay.

## Principais entregas

- Replay com hardening de persistencia:
  - Save bloqueado durante replay ativo.
  - Load bloqueado durante replay ativo.
  - Feedback em `panel_dialog` para bloqueios de save/load durante replay.

- Replay panel (F9) com comportamento mais seguro:
  - Fechar painel pausa automaticamente o replay quando estava em execucao.
  - Mensagens de estado para autoplay ligado/desligado e replay pausado.

- Validacao de consistencia de batch:
  - Verificacao de `UnitInstanceId` antes do `HandleConfirm()` no batch de unidade.
  - Em divergencia, aborta batch de forma graciosa (sem crash), com warning e mensagem de erro no dialog.

- Watchdog de timeout no ReplayManager:
  - Novo campo de inspector `replayBatchTimeoutSeconds` (default `10f`).
  - Timeout aplicado em `WaitForReplaySystemsIdle()`.
  - Timeout aplicado nos waits de `ExecuteActionFromAutomatedPlayer()`.
  - Ao estourar timeout, aborta graciosamente com `replayBatchAbortRequested = true`, log `[Replay] Timeout: batch nao completou em Xs` e dialog de erro.

- Assets de dialog para replay:
  - Novos `DialogData` para: autoplay ligado, autoplay desligado, replay pausado, erro de replay, save desativado durante replay e load desativado durante replay.
  - Registro dos novos assets no `Dialog Database`.

## Arquivos-chave impactados

- `Assets/Scripts/Replay/ReplayManager.cs`
- `Assets/Scripts/Save/SaveGameManager.cs`
- `Assets/Scripts/UI/Replay/ReplayPanelUI.cs`
- `Assets/DB/Dialog/Dialog Database.asset`
- `Assets/DB/Dialog/Dialog Data/Replay/*`

## Validacao

- Build limpo executado com sucesso:
  - `Assembly-CSharp.csproj` (clean + build)
  - `Assembly-CSharp-Editor.csproj` (clean + build)

## Observacao

- Este release inclui tambem outras alteracoes presentes no working tree (docs/cena/assets) no momento do commit.
