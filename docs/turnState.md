# TurnStateManager - mapa de estados

Legenda:
- `[CursorState]`: estado explicito da FSM principal (`TurnStateManager.CursorState`).
- `[ScannerPromptStep]`: subestado interno do scanner/sensors.
- `[inline]`: fluxo sem `CursorState` dedicado (flags, helper, hotkey, coroutine).

Arvore atual (alto nivel):

neutral [CursorState]
    inspect unit [inline]
        BeginInspectedHelper (helper de inspecao)
    inspect building [inline]
        BeginInspectedConstructionHelper (helper de inspecao)
    inspect hot zone [inline + ScannerPromptStep]
        ThreatLayerTeamSelect [ScannerPromptStep]
    command service [inline]
        preview/confirmacao pendente (`commandServiceConfirmationPending`)
        execucao (`commandServiceExecutionInProgress`)
    removing unit [inline/debug]
        pending destroy confirmation (`pendingDestroyUnitConfirmation`)
    shopping and services [CursorState]
        compra em construcao aliada
    unit selected [CursorState]
        confirma no mesmo hex -> moveu parado [CursorState]
        confirma destino valido -> animacao de movimento -> moveu andando [CursorState]

unit selected [CursorState]
    (transicao de movimento)
        -> moveu parado [CursorState]
        -> moveu andando [CursorState]

moveu parado [CursorState]
    sensors awaiting action [ScannerPromptStep.AwaitingAction]
        mirando [CursorState]
            MirandoCycleTarget [ScannerPromptStep]
            MirandoConfirmTarget [ScannerPromptStep]
        embarcando [CursorState]
            EmbarkCycleTarget [ScannerPromptStep]
            EmbarkConfirmTarget [ScannerPromptStep]
        pousando [CursorState]
            LandingCycleOption [ScannerPromptStep]
            LandingConfirmOption [ScannerPromptStep]
        desembarcando [CursorState]
            DisembarkPassengerSelect [ScannerPromptStep]
            DisembarkLandingSelect [ScannerPromptStep]
            DisembarkConfirm [ScannerPromptStep]
        capturando [CursorState]
            fluxo por coroutine (sem substep dedicado)
        fundindo [CursorState]
            MergeParticipantSelect [ScannerPromptStep]
            MergeConfirm [ScannerPromptStep]
            MergeTargetSelect [ScannerPromptStep] (reservado no enum; nao e o caminho principal atual)
        suprindo [CursorState]
            MergeParticipantSelect [ScannerPromptStep] (reuso)
            MergeConfirm [ScannerPromptStep] (reuso)
        transfer prompt [inline]
            selection pending (flag)
            confirmation pending (flag)
        move only [inline]
            finaliza acao e volta para neutral

moveu andando [CursorState]
    mesmo conjunto de subfluxos de `moveu parado`
    (mirando, embarcando, pousando, desembarcando, capturando, fundindo, suprindo, transfer prompt)

mirando [CursorState]
    MirandoCycleTarget [ScannerPromptStep]
    MirandoConfirmTarget [ScannerPromptStep]
    confirma ataque -> sequencia de combate -> neutral
    esc -> volta para moveu parado/moveu andando

embarcando [CursorState]
    EmbarkCycleTarget [ScannerPromptStep]
    EmbarkConfirmTarget [ScannerPromptStep]
    confirma -> sequencia de embarque
    esc -> volta para moveu parado/moveu andando

pousando [CursorState]
    LandingCycleOption [ScannerPromptStep]
    LandingConfirmOption [ScannerPromptStep]
    confirma -> aplica transicao de camada
    esc -> volta para moveu parado/moveu andando

capturando [CursorState]
    estado de execucao de captura (coroutine)
    ao finalizar -> neutral

desembarcando [CursorState]
    DisembarkPassengerSelect [ScannerPromptStep]
    DisembarkLandingSelect [ScannerPromptStep]
    DisembarkConfirm [ScannerPromptStep]
    confirma -> fila/execucao de desembarque
    esc -> retrocede subpasso ou volta para moveu parado/moveu andando

fundindo [CursorState]
    MergeParticipantSelect [ScannerPromptStep]
    MergeConfirm [ScannerPromptStep]
    confirma -> fila/execucao de fusao
    esc -> retrocede subpasso ou volta para moveu parado/moveu andando

suprindo [CursorState]
    MergeParticipantSelect [ScannerPromptStep] (reuso)
    MergeConfirm [ScannerPromptStep] (reuso)
    confirma -> fila/execucao de suprimento
    esc -> retrocede subpasso ou volta para moveu parado/moveu andando

shopping and services [CursorState]
    selecao de opcao de compra
    confirma -> compra
    esc -> neutral

Observacoes:
- `HandleCancel()` e centralizado, mas delega por estado (`HandleCancelWhile...`) e por subpassos (`HandleScannerPromptCancel`).
- Fluxos de `inspect unit/building`, `inspect hot zone`, `command service`, `transfer prompt` e `removing unit` nao tem `CursorState` proprio.
- Fluxos de "emergir"/forca de camada (submarino) tambem nao tem `CursorState` proprio; ver `docs/sensors.md` em "Fluxos de camada relacionados a sensores (nao-FSM dedicado)".

## Classificacao validada no codigo

1. Estados inferiores (overlay/helper em `Neutral`, sem `CursorState` proprio)
- `inspect unit`: inline via `BeginInspectedHelper(...)`.
- `inspect building`: inline via `BeginInspectedConstructionHelper(...)`.
- `inspect hot zone`: inline via `ScannerPromptStep.ThreatLayerTeamSelect`.
- Comportamento de saida:
  - `inspect unit/building`: fecha com qualquer input (teclado/mouse) ou ao mover cursor para outro hex.
  - `inspect hot zone`: fecha por `ESC`, `Z` (toggle) ou ao sair de `Neutral` (ex.: selecionar unidade).
- Implicacao: precisa mesmo de `HandleNeutral` robusto, porque o jogador pode sair da inspecao e entrar em selecao/acao no mesmo contexto de input.

2. Estados menores com caminho unico (confirmar ate o fim ou cancelar)
- `command service` [inline]:
  - Preview deixa `commandServiceConfirmationPending = true`.
  - `Enter` confirma e inicia coroutine de execucao.
  - `ESC` cancela pendencia (`TryCancelPendingCommandServiceConfirmation()`).
  - Exige `cursorState == Neutral`.
- `removing unit` [inline/debug]:
  - Hotkey `U` abre confirmacao (`pendingDestroyUnitConfirmation = true`).
  - `Enter` executa destruir.
  - `ESC` cancela.
- `shopping and services` [`CursorState.ShoppingAndServices`]:
  - `Enter` confirma compra.
  - `ESC` volta para `Neutral`.

3. Estados automaticos hardcoded
- `pousando`: existe como `CursorState.Pousando` + substeps (`LandingCycleOption`/`LandingConfirmOption`).
- `decolando`: nao existe `CursorState.Decolando`; e fluxo automatico por regra/flags (`TryPrepareTemporaryTakeoffStateForSelection`, `TryPrepareAutomaticTakeoffForMovement`).

4. Estados/fluxos que entram no replay
- O replay registra comandos de acao, nao "todo estado" da FSM.
- Tipos atuais: `MoveUnit`, `Attack`, `BuyUnit`, `Capture`, `Embark`, `Disembark`, `Merge`, `Supply`.
- Na pratica, eles sao filhos do fluxo iniciado em `UnitSelected` (direto ou via `MoveuParado/MoveuAndando`), mas a gravação ocorre no momento da execucao da acao.
- Cinematico (cursor/camera/input sintetico) hoje so existe para `Attack`.
