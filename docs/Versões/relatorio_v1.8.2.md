# The Map Room - Relatório de Atualização v1.8.2

**Versão:** v1.8.2
**Data:** 12 de Abril de 2026
**Tema:** AI Baseada em Batches

## Resumo das Alterações
A versão `v1.8.2` marca o nascimento da nova arquitetura de Inteligência Artificial do Map Room — redesenhada do zero sob o princípio correto: a IA como um **criador de batches**. Em vez de simular inputs ou manter estado interno próprio, o `AIPlayerOrchestrator` fabrica `PlayerAction` idênticos aos gerados por humanos e os entrega ao `ReplayManager` para execução. Toda a lógica de animação, sensores, fog of war e gravação de replay é herdada de graça.

## Alterações Tecnológicas Principais

### 1. AIPlayerOrchestrator — Reescrita Completa
O antigo orquestrador foi descartado e reescrito sob nova filosofia:

- **Loop de coroutine unificado:** Todo o turno da IA é gerenciado por uma única coroutine (`AIBatchExecutionLoop`) que itera até esgotar as unidades aptas, sem depender de eventos externos para encadear batches.
- **Sincronização via `WaitUntil(!IsStepExecutionBusy)`:** Após cada batch enviado ao `ReplayManager`, a coroutine aguarda o pipeline terminar antes de agir novamente. Elimina a race condition que existia na versão anterior (onde `OnCursorReturnedToNeutral` chegava enquanto o batch ainda estava executando internamente, travando a IA após a primeira unidade).
- **Filtro de unidades aptas:** A cada iteração, re-lê `UnitManager.AllActive` e filtra por `!HasActed`, `!IsDead`, `!IsEmbarked`, `!HasMerged`. Quando a lista esgota, chama `AdvanceTurnWithTransition()` automaticamente.
- **Snapshot de ocupação do tabuleiro:** Antes de sortear o destino de cada unidade, constrói um `HashSet<Vector3Int>` com as posições atuais de todos os aliados (excluindo a unidade em movimento). Garante que a IA nunca tente pousar em cima de uma peça própria — problema confirmado e corrigido nesta versão.

### 2. Timing Integrado ao ReplayManager
A IA passou a respeitar integralmente os timers configuráveis do `ReplayManager`:

- **`timeBetweenBatches`:** O delay entre batches consecutivos da IA agora usa `GetEffectiveTimeBetweenBatchesForAutoplay()` em vez do valor hardcoded anterior (`0.5f`). Respeita o Fast Mode automaticamente.
- **Delays internos de batch** (`cursorTravelStepDelay`, `sensorSubstepDelay`, `replayConfirmVisualDelay`): já eram respeitados por passarem pelo mesmo `ExecuteRecordedActionBatch` do replay.
- **Animações de unidade** (`AnimationManager`): respeitadas da mesma forma — o pipeline de movimento é idêntico ao do jogador humano.

### 3. Novo Slider: `unitSelectionHoldDelay`
Adicionado campo configurável ao `ReplayManager` (header Playback, Range 0–2s, padrão 0.3s):

- Introduz uma pausa **após a seleção da unidade** e **antes do cursor começar a se mover ao destino**, tanto no AI turn quanto no replay automatizado.
- Comportamento visual de "pensar antes de agir" — a unidade fica selecionada por um instante antes de se mover.
- Zera automaticamente no Fast Mode via `GetEffectiveUnitSelectionHoldDelay()`.

## Arquitetura do Fluxo de Turno da IA

```
OnActiveTeamChanged (time da IA)
    └── AIBatchExecutionLoop()
            ├── WaitForSeconds(timeBetweenBatches)
            ├── Filtra unidades aptas (AllActive snapshot)
            ├── Nenhuma? → AdvanceTurnWithTransition()
            ├── Escolhe unidade [0]
            ├── CalcularCaminhosValidos()
            ├── Snapshot de células ocupadas (AllActive)
            ├── Sorteia destino livre
            ├── Monta PlayerAction (IsAIGenerated = true)
            ├── ReplayManager.ExecuteLiveAIBatch()
            │       └── ExecuteRecordedActionBatch()
            │               ├── MoveCursorToCellWithTravel()
            │               ├── [unitSelectionHoldDelay]
            │               ├── MoveCursorToCellWithTravel() → destino
            │               ├── WaitForSensorsReady()
            │               └── HandleAutomatedMoveOnlyActionRequested()
            │                       └── WaitForCursorReturnedToNeutral()
            └── WaitUntil(!IsStepExecutionBusy) → próxima unidade
```

## Estado Atual da IA (v1.8.2)
| Capacidade | Status |
|---|---|
| Ciclo de turno completo | Funcional |
| Move + Wait para todas as unidades aptas | Funcional |
| Passagem de turno automática | Funcional |
| Timing configurável (sliders) | Funcional |
| Snapshot de ocupação (sem colisão com aliados) | Funcional |
| Decisão de ataque | Pendente |
| Decisão de captura | Pendente |
| Destino estratégico (não aleatório) | Pendente |
| Prioridade de unidades | Pendente |

## Impacto Pós-Atualização (Próximos Passos)
A fundação está estabelecida e testada com múltiplas unidades em campo. O próximo ciclo de desenvolvimento foca nas **decisões de ação**: detectar inimigos no alcance de tiro, avaliar construções capturáveis e construir o batch com o `SensorActionType` correto (`Attack`, `Capture`) em vez do `None` atual.
