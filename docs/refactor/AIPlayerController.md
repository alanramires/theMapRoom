Falta principalmente quebrar os blocos grandes que ainda concentram responsabilidade demais em [AIPlayerController.cs](/d:/Unity%20Projects/The%20Map%20Room/Assets/Scripts/AI/AIPlayerController.cs).

Já foi separado:
- [AIPlayerController.Transport.cs](/d:/Unity%20Projects/The%20Map%20Room/Assets/Scripts/AI/AIPlayerController.Transport.cs)
- [AIPlayerController.Support.cs](/d:/Unity%20Projects/The%20Map%20Room/Assets/Scripts/AI/AIPlayerController.Support.cs)
- [AIPlayerController.TurnSummary.cs](/d:/Unity%20Projects/The%20Map%20Room/Assets/Scripts/AI/AIPlayerController.TurnSummary.cs)
- [AIPlayerController.CombatTargeting.cs](/d:/Unity%20Projects/The%20Map%20Room/Assets/Scripts/AI/AIPlayerController.CombatTargeting.cs)
- [AIPlayerController.Capture.cs](/d:/Unity%20Projects/The%20Map%20Room/Assets/Scripts/AI/AIPlayerController.Capture.cs)

O que ainda vale separar:
- `Planner`
- `Save/Restore` do planner
- `Phase2_MoveUnit` e helpers de execução de turno
- `Shopping`
- possivelmente `Repair/Merge/Recovery`
- possivelmente `Debug/Inspector-facing planner debug`

Minha ordem recomendada:
1. `AIPlayerController.Planner.cs`
2. `AIPlayerController.PlannerPersistence.cs`
3. `AIPlayerController.Shopping.cs`
4. `AIPlayerController.Phase2.cs`
5. `AIPlayerController.RepairRecovery.cs`

O ganho esperado no final:
- menos risco de estragar uma área ao editar outra
- navegação muito mais rápida no arquivo certo
- diffs menores e mais legíveis
- compile errors mais localizados
- manutenção melhor para features novas como transporte, protect, shopping pressure
- queda real na chance de repetir aquele problema de blob/arquivo monstro
- mais clareza de arquitetura, mesmo sem reescrever a lógica

O que não muda por si só:
- performance em runtime praticamente não muda
- complexidade lógica não desaparece sozinha
- bugs de regra continuam possíveis se a responsabilidade ainda estiver misturada entre métodos

Resumo curto:
- falta separar `Planner`, `Shopping` e o miolo de execução de turno
- o ganho principal é manutenção, segurança de edição e legibilidade
- não é otimização de FPS; é otimização de engenharia