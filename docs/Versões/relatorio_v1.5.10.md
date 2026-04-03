# v1.5.10 - AI Plan view plan

## Objetivo
Disponibilizar uma visão unificada no Inspector para inspecionar planos da IA por time durante execução/pause.

## Entrega desta versão
- Refatoração do runtime do planner para estado por time.
- Estrutura de debug serializável para inspeção no AI Manager.
- Botão de refresh no inspector para reconstruir a visão sob demanda.
- Ordenação estável da visualização (time -> plano -> unidades).

## Estruturas principais
- `TeamPlannerRuntimeState`
  - `teamId`
  - `currentTurnPlans`
  - `unitRoles`
  - `unitAssignments`
  - `previousAssignmentsByUnitId`
  - metadados de restauração futura (`hasRestoredStatePendingUse`, `restoredTurn`)
- `plannerStateByTeam : Dictionary<TeamId, TeamPlannerRuntimeState>`
- DTOs de inspector:
  - `TeamPlannerDebugView`
  - `PlanDebugView`
  - `AssignmentDebugView`

## Como usar (Play Mode)
1. Selecione o objeto `AI Manager`.
2. No componente `AI Player Controller`, vá em **Planner Runtime (Debug)**.
3. Clique em **Refresh Planner Debug View**.
4. Expanda `Planner Debug View` -> `Element` -> `plans` -> `assignments`.

## Observação
- Esta versão não integra save/load do planner ainda.
- A visualização é atualizada sob demanda para evitar custo por frame.

## Próximo passo recomendado
- Persistir estado multi-time no SaveGameManager usando DTOs de save por time.
