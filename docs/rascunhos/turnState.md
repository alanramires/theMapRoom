# TurnStateManager - mapa atual da FSM

Documento canonico do estado atual do `TurnStateManager`.
Para leitura analitica complementar, ver tambem:
- `docs/analises/07_relatorio_turn_state_manager.md`
- `docs/avaliacao do autor/sistema de estados.md`

Legenda:
- `[CursorState]`: estado explicito da FSM principal (`TurnStateManager.CursorState`).
- `[ScannerPromptStep]`: subestado interno do scanner / prompt de sensores.
- `[inline]`: fluxo sem estado novo na enum, mas ainda governado pelo `TurnStateManager` por flags, helper ou coroutine.

## CursorState atuais

1. `Neutral`
2. `UnitSelected`
3. `MoveuAndando`
4. `MoveuParado`
5. `Apenas Mover`
6. `Capturando`
7. `Mirando`
8. `Pousando`
9. `Embarcando`
10. `Desembarcando`
11. `Fundindo`
12. `ShoppingAndServices`
13. `Suprindo`
14. `InspectingUnit`
15. `InspectingBuilding`
16. `InspectingHotZone`
17. `CommandService`
18. `RemovingUnit`
19. `AircraftFuelDepletionQueue`

## Leitura funcional

### Base da interacao
- `Neutral`: ponto de entrada e saida mais comum.
- `UnitSelected`: unidade aliada ativa, pronta para mover ou abrir sensores.

### Movimento
- `MoveuParado` e `MoveuAndando` sao os dois estados apos a confirmacao de movimento.
- Ambos reconstroem o mesmo conjunto de fluxos de sensores, mudando apenas o contexto de movimentacao.
- `Capturando` segue como estado dedicado de execucao, e nao como substep de scanner.
- `Apenas mover` (`M`) e uma acao valida nesse ponto (scanner), mesmo sem criar `CursorState` dedicado.

### Sensores / prompts dedicados
- `Mirando` usa `MirandoCycleTarget` e `MirandoConfirmTarget`.
- `Pousando` usa `LandingCycleOption` e `LandingConfirmOption`.
- `Embarcando` usa `EmbarkCycleTarget` e `EmbarkConfirmTarget`.
- `Desembarcando` usa `DisembarkPassengerSelect`, `DisembarkLandingSelect` e `DisembarkConfirm`.
- `Fundindo` usa `MergeParticipantSelect`, `MergeTargetSelect` e `MergeConfirm`.
- `Suprindo` reaproveita parte da navegacao de merge, mas e um `CursorState` proprio.
- `ShoppingAndServices` e o estado de compra em construcao aliada.
- `InspectingUnit`, `InspectingBuilding` e `InspectingHotZone` sao estados dedicados de inspecao / overlay.
- `CommandService` e `RemovingUnit` tambem sao estados dedicados, nao fluxos inline.
- `AircraftFuelDepletionQueue` e estado dedicado de execucao da fila de aeronaves caindo (runtime).

### Fluxos inline que continuam sem CursorState proprio
- `transfer prompt`: pendencias de selecao / confirmacao controladas por flags e helper.
- `emergir` / forca de camada: segue por regras de camada e sensores auxiliares, sem `CursorState.Decolando`.
- `decolagem automatica`: pode ser preparada por regra no momento da selecao ou do movimento, mas nao cria um estado novo na enum.

## Arvore resumida

neutral [CursorState]
    inspect unit [CursorState]
    inspect building [CursorState]
    inspect hot zone [CursorState + ScannerPromptStep.ThreatLayerTeamSelect]
    command service [CursorState]
    removing unit [CursorState]
    aircraft fuel depletion queue [CursorState]
    shopping and services [CursorState]
    unit selected [CursorState]
        confirma no mesmo hex -> moveu parado [CursorState]
        confirma destino valido -> animacao de movimento -> moveu andando [CursorState]

unit selected [CursorState]
    (transicao de movimento)
        -> moveu parado [CursorState]
        -> moveu andando [CursorState]

moveu parado / moveu andando [CursorState]
    mirrored scanner tree:
        apenas mover (`M`) -> finaliza acao e retorna ao neutral
        mirar -> `MirandoCycleTarget` / `MirandoConfirmTarget`
        embarcar -> `EmbarkCycleTarget` / `EmbarkConfirmTarget`
        pousar -> `LandingCycleOption` / `LandingConfirmOption`
        desembarcar -> `DisembarkPassengerSelect` / `DisembarkLandingSelect` / `DisembarkConfirm`
        fundir -> `MergeParticipantSelect` / `MergeTargetSelect` / `MergeConfirm`
        suprir -> fluxo proprio, com reuso parcial de selecao de merge

## Contrato de neutral

- Regra alvo: `Neutral -> Acao -> Neutral`.
- Em gameplay: so gravar batch apos retorno para `Neutral`.
- Em replay: so avancar para proximo batch apos retorno para `Neutral`.
- Durante `AircraftFuelDepletionQueue`, Save/Load devem ficar bloqueados ate retorno para `Neutral`.

## Observacoes objetivas
- `HandleConfirm()` e `HandleCancel()` continuam centralizados, mas o roteamento agora cobre `Inspecting*`, `CommandService`, `RemovingUnit` e `AircraftFuelDepletionQueue` como estados proprios.
- `ScannerPromptStep.MergeTargetSelect` existe no enum, mas nao e o caminho principal da maioria dos fluxos atuais.
- O replay registra comandos de acao, nao o estado inteiro da FSM.
- Tipos atuais de acao persistidos no replay incluem `UnitAction`, `Shopping`, `CommandService` e `RemoveUnit`, alem dos fluxos de combate / logistica ligados ao runtime.
- Se um doc antigo tratar `command service`, `inspect hot zone` ou `removing unit` como inline, essa leitura esta deprecada.
