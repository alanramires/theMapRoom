# RELATORIO v1.3.4

## Tema
Expansao de comandos de debug, melhorias de UX em paineis/atalhos e consolidacao das regras de logistica (transferencia e documentacao).

## Entregas principais
- `Panel_Debug` com novos comandos:
  - `wake all units`
  - `help`
  - `set fuel` (alias de `set autonomy`)
  - `set owner x`
  - `set active team x` (forca time ativo sem passar turno completo)
- Fluxo de `set active team` com comportamento ajustado:
  - troca musica/FoW/navegacao de time;
  - preserva selecao apenas se a unidade pertencer ao novo time;
  - mensagem no dialog: `DEBUG: Active Team forced to x`.
- Bloqueio de atalhos enquanto digita no `Panel_Debug`:
  - `H` e `X` nao disparam durante foco de texto;
  - atalho do proprio debug fecha o painel e nao fica digitado no input;
  - foco automatico no input ao abrir e foco persistente ate fechar.
- Save/Load por slot via helper/dialog:
  - prompt de salvar e carregar com slots `1..3`;
  - preview de mapa + data/hora em slots ocupados;
  - confirmacao de sobrescrita;
  - bloqueio de load em slot vazio.
- SaveGameManager revisado:
  - remocao de `activeSlot` para fluxo por atalhos;
  - `slotName` renomeado para `fileNameDefault` com tags `<Map><date><hour>`;
  - caminho de save editavel e visivel (`customSaveDirectory`).
- Strings de helper/dialog localizaveis via `HelperDatabase`/`DialogDatabase`.
- Revisao de logistica:
  - `Receiver` so recebe (nao doa para `Hub` nem para `Receiver`);
  - `Hub infinito` atua como fonte (nao recebe de outro hub);
  - barreira por time consistente (sem transferencia com inimigo);
  - transferencia para unidade respeita capacidade maxima de reserva.

## Mudancas tecnicas relevantes
- `DebugManager` / `UiInputBlocker`: foco explicito e supressao de input de gameplay durante digitacao.
- `PanelVisibilityHotkeysController`: hotkeys bloqueadas por foco em input, com excecao controlada para toggle do debug.
- `TurnStateManager.CommandService`: novos comandos e melhorias de execucao/feedback.
- `TurnStateManager.Transfer` e `PodeTransferirSensor`:
  - filtros de elegibilidade por tier/time/capacidade;
  - runtime de transferencia com limite hard por capacidade restante.
- `SaveGameManager`:
  - prompts de slot para salvar/carregar;
  - nome de arquivo default por tags;
  - suporte a diretorio customizado.
- `PanelHelperController` / `PanelDialogController`:
  - resolucao de mensagens por database e textos externos para prompts.
- Atualizacao da documentacao de logistica em `docs/Logistica/`.

## Arquivos chave alterados
- `Assets/Scripts/UI/DebugManager.cs`
- `Assets/Scripts/UI/PanelVisibilityHotkeysController.cs`
- `Assets/Scripts/Match/MatchController.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.CommandService.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Transfer.cs`
- `Assets/Scripts/Sensors/PodeTransferirSensor.cs`
- `Assets/Scripts/Save/SaveGameManager.cs`
- `Assets/Scripts/UI/PanelHelperController.cs`
- `Assets/DB/Dialog/Dialog Database.asset`
- `Assets/DB/Dialog/Helper Database.asset`
- `docs/Logistica/logistica.md`

## Git
- Tag: `v1.3.4`
- Branch: `main`
