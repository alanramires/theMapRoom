# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Mandatory transactional-action invariant

**Nothing in the game is definitive until the player commits the action.** Every board action starts in `CursorState.Neutral` and ends in `CursorState.Neutral`. Intermediate movement, animation, menus and sensor states are provisional and cancelable. They must not permanently refresh FOW, reveal units/terrain, update detection or AI intelligence, consume resources, mutate confirmed occupancy/revisions, or mark a unit as acted. After explicit commitment, return to `Neutral` and only then recalculate the confirmed board.

Read `docs/arquitetura/acoes_transacionais.md` before changing TurnState, movement, FOW, sensors, combat, capture, transport, supply, merge, replay or AI action execution.

## Versioning and reports

`vX.Y.Z`, a scheme defined by the author:

| digit | meaning |
|---|---|
| **X** | large architecture change — throwing an AI away and writing another, altering an already-validated sensor rule, changing game speed |
| **Y** | important localized change — taking one part (the capturer, say) and working it and its children |
| **Z** | end-of-work save point |

**Every version has a report** (`docs/relatorio_vX.Y.Z.md`) that explains the
*why*, not just the *what*, and each report is tagged in git.

**Report location is a convention:** the **current** major lives in `docs/`;
when a major closes, its reports are archived into `docs/Versões/`. Do not move
current-major reports there.

`CHANGELOG.md` is the index. 329 tags and 325 reports exist — do not try to list
them all; the current major is detailed and closed majors are pointers.

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
- `BuildDesembarcarBatch(unit, team, from, passengers)` — disembark in place
- `BuildDesembarcarBatch(unit, team, from, passengers, moveTo, movementPath)` — move **then** disembark (supported by engine)
- `BuildShoppingBatch(team, order)` — buy unit

**Move + disembark in one batch is fully supported.** `ReplayManager.ExecuteRecordedUnitActionBatch` moves the APC to `action.MoveTo` first, waits for sensors to refresh from the new position, then executes the disembark sub-steps. Setting `MoveTo ≠ MoveFrom` with `SensorAction = Disembark` is the correct way to combine both.

### Transport system (`AIController.Transportador*.cs`)

Split into four files:

- `AIController.Transportador.cs` — entry point `TryDecideTransportadorAction`, shared helpers, legacy range constants (see "Ranges are bands, not hex numbers" below)
- `AIController.Transportador.Shuttle.cs` — empty APC scanning for pickup candidates
- `AIController.Transportador.Courier.cs` — APC with embarked passengers, delivers toward objectives
- `AIController.Transportador.Assigned.cs` — APC with an explicit plan-assigned slot

Capturers can intercept to embark via `TryDecideCapturerEmbarkAction` in `AIController.Capturer.Embark.cs` (called near the top of `TryDecideCapturerAction`).

**Hospital mode** (`AIController.Transportador.Hospital.cs`): a unit that is both `isSupplier` and `isTransporter` carrying a passenger with `IsUnderRepair` does maintenance before delivery. `TryDecideSupplierHospitalAction` is called at the **top of `DecideUnitAction`** (before every role) because a transporter at capacity skips the universal courier gate. Priority: supply aboard → restock keeping the patient aboard → hold (stationary shot / rear reposition) → return `null` so the normal EVAC disembarks. The `UnitData.aiDisembarkWhenCannotSupply` flag (default `true`, Logistics AI section) gates the whole mode; `serviceRange = SameHexOrEmbarked` means the supplier can only serve its **own** embarked passengers, so an `Adjacent1Hex` truck structurally can't nurse its cargo and keeps disembarking as before. The patient leaves the mode on its own: Phase 2 runs `UpdateRepairState` on embarked units too, so `repairRecoverHpAbove` releases it.

**Courier decision priority** (`DecideTransportadorCourierAction`):
1. Move + disembark — if moving gains >1h toward target AND the simulated drop-off lands inside the passenger's drop band (today: `TransportDropOffRange`)
2. Disembark in place — if moving gains ≤1h and current position already qualifies for drop-off
3. Opportunistic attack — near-dead enemies (HP ≤ 2), ≤2h route deviation
4. Move toward target

**Simulating sensors from a hypothetical position**: use `SimulateDisembarkFromCell(unit, cell)` — temporarily calls `unit.SetCurrentCellPosition(cell, false)`, runs the sensor, then restores. Safe because it is synchronous and finishes before any other unit's decision runs. Do not replicate sensor logic directly.

**Unit-aware routing**: `FindTransportMove` uses `UnitMovementPathRules.CalculateMovementCostMap(tilemap, unit, target, budget, terrainDb)` (reverse BFS from target) for real MP costs. This correctly prefers roads over forest for ground APCs. `SectorManager.HexDistance` is unit-agnostic and should not be used as a movement cost proxy for routing decisions.

**Extended embark (pass-2)**: `movePaths` computed with `remainingMP - 1` may include friendly-occupied hexes (passable for pathfinding) but the capturer cannot stop there. Always filter with `BuildOccupied(unit)` before treating a hex as a valid intermediate stop.

### The three layers — dumb service, consumer, organizer

Where a piece of logic belongs is decided by this, and almost every "where does
this go?" question answers itself with it.

| layer | examples | job |
|---|---|---|
| **serviço burro** | `UnitReachEnvelopeService` (Hotzone), `UnitMovementPathRules`, the `Pode*` sensors | receives a unit or a cell, returns the area. Knows no policy, no priority, no objective |
| **consumidor** | `MelhorDesembarque`, `MelhorEmbarque`, `MelhorEstoque`, `MelhorPouso`, `QueroCaronaService`, `CaptureOpportunityClaimService` | queries the service **once per subject** — each passenger, each candidate — and aggregates: intersections, rankings, scores, 1:1 matching |
| **organizador** | `AIController.*` | decides with the consumer's answer plus its own policies and priorities |

**The rule:** intersecting, ranking and tie-breaking are **never** the service's
job. If you catch yourself adding "…and also return the best one" to a service,
it belongs one layer up.

Concretely: the envelope answers "where can *this* passenger be dropped";
`MelhorDesembarque` asks once per embarked unit and crosses the results into a
joint drop zone; the transport AI decides whether the joint drop is worth it
against the promise it made and the conveyor it is running.

### Military dialect — read this before any AI doctrine doc

The project speaks a deliberate military vocabulary. Every contract in
`docs/AI Behavior/` is written in it, and the Hotzone tool exists to make it
visible. These are not loose words: each one names a computed answer.

| term | meaning |
|---|---|
| **Tático** (Tactical) | what the unit materializes **this round**. Band of `UnitReachEnvelopeService` |
| **Operacional** (Operational) | the **next turn** — `+MP`, chained, never `MP × 2` pooled |
| **vanguarda** | the forward line. Where assault belongs and fire support must never be |
| **retaguarda** | behind the line. Where fire support, logistics and repair belong |
| **flancos** | the sides of the advance. Assault may hold them; fire support may not |
| **âncora** | the cell an agenda advances toward (objective, captain, capturable) |
| **capitão / magnético** | the unit another unit orbits. Capturer for assault, maritime assault for navy, Radar/EWACS for AA |
| **camada** | operation layer (domain + height). Native layer = where the unit prefers to end its turn |

**The one rule that generates the rest:** *banda, âncora e camada são sempre
parâmetro da unidade avaliada — nunca constante do papel.*

Every regression this project has hit in these areas came from freezing one of
the three: a fixed hex range instead of a band, a fixed enemy-HQ anchor instead
of the nearest capturable, a fixed air layer instead of the specialized vision
layer.

**Known inversion:** for `Artilheiro` (stationary long-range), the band is the
**weapon's**, not the movement's — green from hex 0 to max range, blue at
`2 × max range`. A 1 MP howitzer with a movement band would have an Operational
of hex 2. See `docs/contrato_envelope_alcance.md`.

### Ranges are bands, not hex numbers

**Doctrine:** an AI "range" is a band of the reach envelope — `Tactical` or
`Operational` — **of the unit being evaluated**, resolved by
`UnitReachEnvelopeService`. It is never a fixed hex count, because a 2 MP
howitzer and a 3 MP rifleman do not share a reach.

The fixed constants below are **legacy**, from before the envelope existed. They
still run; do not read them as the rule. Each one names what it should become:

| constant | value in code | should be |
|---|---|---|
| `TransportDropOffRange` | 4 | passenger's `Tactical`, computed from the objective cell (reverse: "teleport the unit onto the target, that area is the drop zone") |
| `FireSupportDropOffRange` | 3 | same — it exists only because the number differs per role, which is what a per-unit band already solves |
| `ShuttlePickupRange` | 2 | already close: every call site adds it to `RemainingMovementPoints` as slack, so it behaves as "Tactical + margin". Pickup itself is decided by `MelhorEmbarqueService` on the unit's Tactical |
| `MinDistanceForTransportSlot` | serialized tunable on `AIController`, default 7 (also in `AIPresetData`) | the evaluated unit's `Operational`. Today it is a cap over a map-derived value (`Min(tunable, Max(3, farthestSector))`) and never looks at the unit |

Doctrine and per-role rules: `docs/AI Behavior/Transporte.md` and
`docs/AI Behavior/Capturador.md`. Envelope contract:
`docs/contrato_envelope_alcance.md`.

### Shopping (`AIShoppingPlanner.cs`)

`AIShoppingPlanner.Decide(snapshot)` returns a list of `ShoppingOrder`. Scoring uses `CountOpenSlots(team, role)` to detect plan gaps. Transport slots appear in objectives when the objective is beyond the unit's own reach — see "Ranges are bands, not hex numbers". The current gate still uses `MinDistanceForTransportSlot`.

### Hex utilities

- `SectorManager.HexDistance(a, b)` — hex grid distance (unit-agnostic; do not use as movement cost proxy)
- `UnitMovementPathRules.CalculateMovementCostMap(tilemap, unit, startCell, budget, terrainDb)` → `Dictionary<Vector3Int, int>` — real MP cost from `startCell` to every reachable cell within `budget` steps, unit and terrain aware
- `CalculateThreatLevel(cell, team)` — threat score for a cell
- `ConstructionOccupancyRules.GetConstructionAtCell(tilemap, cell)` — building at hex
- All cell positions: zero out `z` before comparisons (`cell.z = 0`)

## Conventions

- Adding a new role: create `AIController.YourRole.cs` partial class, add an entry point `TryDecideYourRoleAction`, call it from `AIController.Router.cs` in `DecideUnitAction`.
- `plannedDestinations` (HashSet on `AIController`) tracks move destinations within a phase-2 pass — include planned moves so later units avoid collisions.
- Logging: use `TL("Category")` helper which stamps `[AI TEAM][T#][Category]`.
- `AIWorldSnapshot` is built fresh per-iteration; never cache it across unit decisions.
- Disembark is always a transporter-side action; embark is always a passenger-side action.
