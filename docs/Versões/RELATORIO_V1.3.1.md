# RELATORIO v1.3.1

## Tema
Refactor e Cache.

## Entregas principais
- Refactor de fluxo de visuais da unidade para modelo reativo por eventos (reduz polling em runtime).
- Lista centralizada de unidades (`UnitManager.AllActive`) aplicada aos sensores para remover buscas globais frequentes.
- Cache de pathfinding por seleção no range de movimento.
- Batch no tilemap de range para clear/paint com `SetTiles`.
- Supressão de refresh de FoW durante `LoadRoutine`, com refresh único no fim do load.

## Escopo consolidado (inclui base de 1.3.0 e 1.3.0a)
- `v1.3.0`: FoW com múltiplas visões, LoS/DPQ por camada, debug expandido de sensores, filtros por board/scene e revisão de consistência entre sensores.
- `v1.3.0a`: checkpoint de emergência para estabilização/perf antes do refactor principal.
- `v1.3.1`: foco em performance estrutural (polling -> eventos, cache, batch, supressão de cascata no load).

## Mudanças técnicas relevantes
- `UnitManager.Update()` removido do caminho crítico de consistência visual; atualização agora por eventos.
- `MatchController` com eventos de estado de turno/unidade/FoW para desacoplar refresh per-frame.
- Sensores críticos migrados para `UnitManager.AllActive` no lugar de `FindObjectsByType` em runtime quente.
- `PaintSelectedUnitMovementRange` com cache por chave composta e reaproveitamento de paths.
- `TurnStateManager.Range` usando batch `SetTiles` para limpar/pintar alcance.
- `SaveGameManager` controla janela de supressão de FoW durante load e dispara refresh único ao final.

## Arquivos chave alterados
- `Assets/Scripts/Units/UnitManager.cs`
- `Assets/Scripts/Match/MatchController.cs`
- `Assets/Scripts/Match/TurnStateManager.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Range.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Save/SaveGameManager.cs`
- `Assets/Scripts/Units/Rules/UnitMovementPathRules.cs`
- `Assets/Scripts/Match/Path/PathManager.cs`
- `Assets/Scripts/Sensors/*.cs` (detectar/mirar/suprir/transferir/servico)

## Resumo de performance
perf: refactor Update polling to events, centralize unit list, cache pathfinding, batch tilemap ops, suppress FoW on load

- UnitManager.Update() removed; acted/detected/team visuals now event-driven
- MatchController exposes OnActiveTeamChanged, OnUnitActedStateChanged, OnFogOfWarUpdated
- UnitManager.AllActive replaces FindObjectsByType in all sensors
- Movement range cache keyed by (unitId, mp, fuel, boardRevision)
- Tilemap clear batched via SetTiles; SetTileFlags/SetColor loop removed from clear
- FoW refresh suppressed during LoadRoutine; single refresh on load complete

Result: 17 FPS -> 84 FPS, load 3s -> <2s

## Git
- Commit: d5d3fb1
- Tag: v1.3.1
- Branch: main
