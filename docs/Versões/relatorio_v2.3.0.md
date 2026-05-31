# v2.3.0 - AI Partials

## Contexto

Versão de refatoração estrutural. Todos os arquivos de IA com mais de ~800 linhas foram divididos em `partial class` por responsabilidade. Nenhuma lógica de jogo foi alterada — o objetivo era tornar cada arquivo legível de forma autônoma e facilitar navegação, revisão e futuras extensões.

---

## Reorganização de Pastas

A pasta `Assets/Scripts/Match/AI/` ganhou subpastas numeradas que comunicam a ordem de execução no turno:

```
AI/
├── 1. Phases/          ← Orquestrador de turno + Phase0-4
├── 2. Planner/         ← PlanEvaluator + partials
│   └── Save and Persist/  ← ObjectiveManager + AISectorIntent
├── 3. Shopping/        ← AIShoppingPlanner, AITacticalAnalyzer, AITacticalNeed
└── Units/              ← Capturer, Assault, Transport, FireSupport, Logistics, Air, Generic
```

---

## Splits Realizados

### AIController.Phases.cs → 6 arquivos

| Arquivo | Conteúdo |
|---|---|
| `AIController.Phases.cs` | `RunAITurn` + `RunAIDebugStage` (orquestradores) |
| `AIController.Phase0.cs` | `Phase0_WaitForTurnReady` |
| `AIController.Phase1.cs` | `Phase1_CommandService` |
| `AIController.Phase2.cs` | `Phase2_UnitActions` + helpers locais |
| `AIController.Phase3.cs` | `Phase3_Shopping` |
| `AIController.Phase4.cs` | `Phase4_EndTurn` |

### AIController.PlanEvaluator.cs → 6 arquivos (3142 → ~700L principal)

| Arquivo | Conteúdo |
|---|---|
| `PlanEvaluator.cs` | `BuildObjectivePlan` + `RefreshSlotDistances` + `SolveAssignment` |
| `PlanEvaluator.MacroContext.cs` | Enum/struct macro, `BuildMacroTerritoryContext`, `ApplyMacroExistingOffensiveCap` |
| `PlanEvaluator.Defense.cs` | Defesa doméstica, SOS de base, invalidação mid-turn |
| `PlanEvaluator.Handoff.cs` | Handoff de captura parcial, cascade de seleção |
| `PlanEvaluator.SectorScoring.cs` | `CalculateSectorPriority`, gating de risco, preempção |
| `PlanEvaluator.Helpers.cs` | Utilitários compartilhados, constantes, `GetMatchController` |

### Helpers de Units → 6 novos arquivos

| Original | Novos arquivos |
|---|---|
| `Capturer.Helpers.cs` (1272L) | `Capturer.Helpers.cs` · `Capturer.Attack.cs` · `Capturer.Vacate.cs` |
| `FireSupport.Helpers.cs` (1405L) | `FireSupport.Helpers.cs` · `FireSupport.Attack.cs` · `FireSupport.Reposition.cs` |
| `Logistics.Helpers.cs` (1028L) | `Logistics.Helpers.cs` · `Logistics.Supply.cs` · `Logistics.Reposition.cs` |

### AIShoppingPlanner.cs → 7 arquivos (3583 → 994L principal)

| Arquivo | Conteúdo |
|---|---|
| `AIShoppingPlanner.cs` | `Decide()` + singleton + `BuildProductionOccupiedCells` + `IndexOf` |
| `AIShoppingPlanner.Intel.cs` | `BuildShoppingIntelReport`, `ApplyJogadasIntelBias` |
| `AIShoppingPlanner.UnitPicker.cs` | `PickUnit`, `PickAirUnit`, classificadores, `CanOffer*` |
| `AIShoppingPlanner.Demand.cs` | `Compute*Demand`, `Count*`, progressão de capturadores |
| `AIShoppingPlanner.Defense.cs` | Ameaças de base, anti-ar, emergência de produção |
| `AIShoppingPlanner.EliteReserve.cs` | `FindElite*ReserveTarget`, `IsElite*ReserveReady`, `FindAffordable*` |
| `AIShoppingPlanner.Transport.cs` | `ComputeTransportDemand`, `ComputeAirTransportDemand`, seed de passageiros |

### AITacticalAnalyzer.cs → 4 arquivos (1094 → 180L principal)

| Arquivo | Conteúdo |
|---|---|
| `AITacticalAnalyzer.cs` | Singleton + `Rebuild` + API pública + `CreateOperation` + `Normalize` |
| `AITacticalAnalyzer.Builders.cs` | 7 métodos `TryBuild*` por tipo de operação |
| `AITacticalAnalyzer.Units.cs` | Atribuição, inferência de fase/coesão, find units, logging |
| `AITacticalAnalyzer.Helpers.cs` | Classificadores, contadores, detecção de ameaça, intel, config |

---

## Convenção Adotada

- `AIController.X.cs` = partial de `AIController`
- `AIShoppingPlanner.X.cs` = partial de `AIShoppingPlanner`
- `AITacticalAnalyzer.X.cs` = partial de `AITacticalAnalyzer`
- Nomes como `ObjectiveManager` e `AISectorIntent` **não** seguem o padrão `AIController.*` pois são classes próprias — ficam em `2. Planner/Save and Persist/`

---

## Resultado

Nenhum comportamento de jogo alterado. A codebase de IA passou de ~15 arquivos grandes para ~55 arquivos focados, cada um com responsabilidade única e entre 100–700 linhas.
