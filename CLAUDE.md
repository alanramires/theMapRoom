# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

This is a Unity project — there is no CLI build. Open in Unity Editor (Windows) and use Play mode to test. Scripts auto-compile when files are saved. Check the Unity Console for compilation errors and runtime logs.

## Project Overview

Turn-based hex strategy game. Two teams alternate turns; the AI controls one (or both) teams. All AI code lives in `Assets/Scripts/Match/AI/`.

## AI Architecture

`AIController` is a `MonoBehaviour` split across ~20 `partial class` files. Each file owns one concern. Never add new logic directly to `AIController.cs` (the root file only holds serialized fields and properties).

### Turn phases (`AIController.Phases.cs`)

Each AI turn runs as a coroutine: `RunAITurn(TeamId)` → four phases:

| Phase | Method | What it does |
|-------|--------|--------------|
| 0 | `Phase0_WaitForTurnReady` | Waits for auto-services, tick delay |
| 1 | `Phase1_CommandService` | Fires command-service batch if automatic |
| 2 | `Phase2_UnitActions` | Main loop — decides and executes one unit at a time |
| 3 | `Phase3_Shopping` | Buys units via `AIShoppingPlanner` |
| 4 | `Phase4_EndTurn` | Passes turn |

### Snapshot & Plan

- `AIWorldSnapshot` — built fresh at the start of each phase-2 iteration via `AIWorldSnapshot.Build(team, matchController)`. Contains `MyUnits`, `EnemyUnits`, `EnemyHQ`, `EnemyBuildings`, `Budget`, `Stance`, `TurnNumber`.
- `TeamObjectivePlan` / `SectorObjective` / `SlotNeed` — persistent objective plan managed by `ObjectiveManager`. Built in `BuildObjectivePlan` (`AIController.PlanEvaluator.cs`) at the top of every turn.

### Unit roles (`UnitRole` enum)

| Role | Value | Handler file(s) |
|------|-------|-----------------|
| `Capturador` | 1 | `AIController.Capturer*.cs` |
| `Assalto` | 2 | `AIController.Assault*.cs` |
| `Transportador` | 3 | `AIController.Transportador*.cs` |

`data.roles[0]` is the primary role. Always guard role checks with `data.roles != null && data.roles.Count > 0`.

### Decision routing (`AIController.Router.cs`)

`DecideUnitAction` calls role handlers in order and returns the first non-null `PlayerAction`:

1. `TryDecideCapturerAction` (only when `plan != null`)
2. `TryDecideAssaultAction` (only when `plan != null`)
3. `TryDecideTransportadorAction` (always — has its own plan-null guard)
4. `HexEvaluator` fallback (generic move/attack/capture logic)

### Initiative ordering (`AIController.Initiative.cs`)

Units are sorted by `GetInitiativeGroup` before acting in Phase 2. Lower group acts first:

| Group | Condition |
|-------|-----------|
| 0 | Vacater handoff OR blocking another capturer's target hex |
| 1 | Under repair on capturable, in active corridor, or transporter with nearby pickup candidate |
| 2 | Has assigned objective |
| 3 | Rogue (no objective) |
| 4 | Under repair, not in any corridor |

### Sensor system

Sensors are the source of truth for legal actions — never replicate their logic in AI code. Key sensors:

- `PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase, mode, targets, fromCell)` — valid attack targets
- `PodeEmbarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, remainingMP, options)` — valid embark actions (passenger side)
- `PodeDesembarcarSensor` — valid disembark sub-steps (transporter side)
- `UnitMovementPathRules.CalcularCaminhosValidos(boardTilemap, unit, remainingMP, terrainDatabase)` → `Dictionary<Vector3Int, List<Vector3Int>>` — all reachable cells with paths

### Batch system (`AIController.Batches.cs`)

A `PlayerAction` is a batch of sub-steps executed atomically. Key builders:

- `BuildMoveBatch(unit, team, from, to, paths?)` — movement
- `BuildAttackBatch(unit, team, from, to, targetId, targetCell, paths)` — move + attack
- `BuildCaptureBatch(unit, team, from, to, paths)` — move + capture
- `BuildEmbarcarBatch(unit, team, from, transporter, slotIndex, paths)` — embark (passenger action)
- `BuildDesembarcarBatch(unit, team, from, passengers)` — disembark (transporter stays; no move+disembark)
- `BuildShoppingBatch(team, order)` — buy unit

### Transport system (`AIController.Transportador*.cs`)

Split into four files:

- `AIController.Transportador.cs` — entry point `TryDecideTransportadorAction`, shared helpers, constants (`MinDistanceForTransportSlot = 7`, `TransportDropOffRange = 3`)
- `AIController.Transportador.Shuttle.cs` — empty APC scanning for pickup candidates
- `AIController.Transportador.Courier.cs` — APC with embarked passengers, delivers toward objectives
- `AIController.Transportador.Assigned.cs` — APC with an explicit plan-assigned slot

Capturers can intercept to embark via `TryDecideCapturerEmbarkAction` in `AIController.Capturer.Embark.cs` (called near the top of `TryDecideCapturerAction`).

### Shopping (`AIShoppingPlanner.cs`)

`AIShoppingPlanner.Decide(snapshot)` returns a list of `ShoppingOrder`. Scoring uses `CountOpenSlots(team, role)` to detect plan gaps. Transport slots appear in objectives when distance to HQ ≥ `MinDistanceForTransportSlot`.

### Hex utilities

- `SectorManager.HexDistance(a, b)` — hex grid distance
- `CalculateThreatLevel(cell, team)` — threat score for a cell
- `ConstructionOccupancyRules.GetConstructionAtCell(tilemap, cell)` — building at hex
- All cell positions: zero out `z` before comparisons (`cell.z = 0`)

## Conventions

- Adding a new role: create `AIController.YourRole.cs` partial class, add an entry point `TryDecideYourRoleAction`, call it from `AIController.Router.cs` in `DecideUnitAction`.
- `plannedDestinations` (HashSet on `AIController`) tracks move destinations within a phase-2 pass — include planned moves so later units avoid collisions.
- Logging: use `TL("Category")` helper which stamps `[AI TEAM][T#][Category]`.
- `AIWorldSnapshot` is built fresh per-iteration; never cache it across unit decisions.
- Disembark is always a transporter-side action; embark is always a passenger-side action.
