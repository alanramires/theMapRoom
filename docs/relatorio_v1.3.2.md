# RELATORIO v1.3.2

## Tema
Refactor da construção (runtime e FoW).

## Entregas principais
- `ConstructionManager` migrou de refresh throttled por `Update()` para fluxo reativo por eventos.
- Introdução de lista ativa centralizada de construções: `ConstructionManager.AllActive`.
- `ApplyFriendlyConstructionVision` passou a usar `AllActive` no lugar de `FindObjectsByType<ConstructionManager>`.
- Novo evento de ocupação de unidade (`UnitOccupancyRules.OnUnitOccupancyChanged`) para atualizar HUD/tint de construções apenas quando necessário.
- Ajustes de ordem de inicialização para FoW (primeiro refresh movido para `Start`) e sincronização no load (`yield` antes do refresh final).

## Mudanças técnicas relevantes
- `ConstructionManager`
  - adicionada lista estática `AllActive` com registro em `OnEnable`/`OnDisable`;
  - removido polling de runtime visual por intervalo;
  - refresh de visual/HUD por eventos:
    - `MatchController.OnActiveTeamChanged`
    - `UnitOccupancyRules.OnUnitOccupancyChanged`;
  - fallback de refresh em editor mantido.
- `UnitOccupancyRules`
  - novo evento `OnUnitOccupancyChanged`;
  - novo `NotifyUnitOccupancyChanged(...)`;
  - snapshot de unidades usando `UnitManager.AllActive`.
- `UnitManager`
  - notifica ocupação em mudanças de célula, embarque/desembarque, troca de time e ciclo enable/disable.
- `MatchController`
  - FoW de construções usa `ConstructionManager.AllActive`;
  - auto-bind defensivo de `BoardTilemap` da construção quando nulo e compatível com cena;
  - primeiro `ApplyActiveTeamIfChanged(force: true)` em play movido de `Awake` para `Start`.
- `SaveGameManager`
  - no fim do `LoadRoutine`, adicionada espera de 1 frame antes do refresh final de FoW.

## Diagnóstico temporário
- Log temporário em `ApplyFriendlyConstructionVision`:
  - `allActive`
  - `activeTeamCandidates`
  - `included`
  - `activeTeam`

## Arquivos chave alterados
- `Assets/Scripts/Construction/ConstructionManager.cs`
- `Assets/Scripts/Match/MatchController.cs`
- `Assets/Scripts/Units/Rules/UnitOccupancyRules.cs`
- `Assets/Scripts/Units/UnitManager.cs`
- `Assets/Scripts/Save/SaveGameManager.cs`

## Git
- Tag: `v1.3.2`
- Branch: `main`
