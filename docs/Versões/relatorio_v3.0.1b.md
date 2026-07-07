# v3.0.1b - AI Tuning

Patch focado em performance da IA durante o turno: baseline estava em 15FPS com congelamentos de decisão; após os fixes voltou a 60FPS com picos pontuais e isolados por unidade.

## Phase2 — Loop de Decisão

- **Bloco síncrono movido para fora do while**: `SyncAIUnitCellsFromTransforms`, `AIWorldSnapshot.BuildLight`, `UpdateRepairState`, construção do `groupCache`, sort de iniciativa e log agora executam uma única vez por fase, antes do loop. O `while` passou a conter apenas decisão da unidade atual + `yield return ExecuteAIBatchWithDebugStep`.
- **Iteração por cursor**: substituído o padrão de reconstruir `GetAvailableUnits` a cada iteração por uma lista pré-ordenada com cursor (`cursor++`). Unidades agidas, mortas ou deferidas são puladas pelo cursor sem realocar nada.
- **Segunda passagem para deferidos**: unidades deferidas acumulam em `deferredUnitIds`; ao esgotar as não-deferidas, `cursor` é resetado com `secondPass = true` para processá-las. Elimina o `RemoveAll` com closure e o rebuild de lista que ocorria no loop original.
- **Código morto removido**: `needsSort = true` era incondicional e o bloco `if (!needsSort)` nunca executava; ambos removidos junto com `prevGroupCache`.
- **`yield return null` antes de `continue` nos deferidos**: unidades deferidas agora cedem um frame ao engine antes de recomeçar o loop, evitando N decisões consecutivas no mesmo frame.

## Alocações por Iteração Eliminadas

| Alocação anterior | Substituição |
|---|---|
| `new List<UnitManager>()` em `GetAvailableUnits` | `_availableUnitsBuffer` — campo `readonly`, limpo com `.Clear()` |
| `new Dictionary<int, int>` (groupCache) | `_groupCache` — campo `readonly`, limpo com `.Clear()` |
| `new StringBuilder` (log de iniciativa) | `_initLogBuilder` — campo `static readonly`, limpo após uso |
| Closure do `.Sort((a,b) => {...})` | `_initiativeComparison` — `Comparison<T>` criado uma vez no `Awake` via method group |
| Closure do `.Sort` em `GetAvailableUnits` | `_availableUnitsComparison` — idem |

`_sortAiTeam` e `_sortActivePlan` são campos de instância setados antes de cada sort para fornecer contexto aos comparadores sem captura em closure.

## MainMenuStateController

- **`enabled = false` quando não há menu na cena**: `Start()` desativa o componente se `panelMenu == null` após o `Awake`. O componente estava ativo na cena de gameplay chamando `FindObjectsByType` (com `FindObjectsInactive.Include`) três vezes por frame indefinidamente — custo de 30–41ms eliminado do baseline.
- **Flag `_referencesResolved`**: guard adicionado no topo de `ResolveReferences()` para parar buscas após encontrar todos os componentes na cena de menu, sem custo residual.

## Resultado

- Baseline: 66ms (15FPS) → 16ms (60FPS)
- GC.Collect eliminado como ruído de fundo
- `MainMenuStateController` removido do timeline do Profiler
- Picos restantes são custo legítimo de decisão por unidade (sensors, pathfinding, avaliação de objetivos)
