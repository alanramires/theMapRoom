# Relatório v1.8.4 — Fusão e Reparos

## Objetivo

Implementar dois comportamentos novos na IA: retirada para reparos e fusão de unidades danificadas, ambos usando os sensores existentes do jogo como fonte de verdade.

---

## O que mudou

### `AIPlayerOrchestrator.cs` — Avaliação de estado de reparo

#### `EvaluateRepairState(UnitManager unit)`

Chamada a cada vez que uma unidade é ativada, antes de decidir o batch. Avalia se a unidade deve entrar ou sair do modo de reparo com base nos thresholds configurados em `UnitData`:

**Entrada em reparo** (qualquer condição satisfeita):
- HP ≤ `repairTriggerHpBelow`
- Autonomia ≤ `repairTriggerAutonomyPct`%
- Alguma arma com munição ≤ `repairTriggerAmmoPct`% (via `HasAnyWeaponBelowAmmoPct`)

**Saída de reparo** (todas as condições satisfeitas simultaneamente):
- HP ≥ `repairRecoverHpAbove`
- Autonomia > threshold (se habilitado)
- Munição acima do threshold (se habilitado)

A verificação de munição usa `GetEmbarkedWeapons()` e inspeciona `squadAmmunition` por arma embarcada — não o campo de munição da unidade hospedeira.

#### `ShowAISprites` — controle de ícone de manutenção

Flag serializada no inspector do `AIPlayerOrchestrator`. Quando ativa, o ícone de chave de fenda (manutenção) aparece sobre a unidade enquanto ela estiver em `isUnderRepair`. `SetAIMaintenanceActive` agora depende apenas de `aiMaintenanceActive`, sem gating por `aiStanceVisible`.

#### Comportamento em modo de reparo

Enquanto `isUnderRepair`:
1. Se `fuseWhileInRepair` estiver ativo: tenta fundir antes de recuar (ver abaixo)
2. Calcula caminhos livres excluindo células ocupadas
3. Identifica o prédio aliado mais próximo não ocupado (`FindNearestAlliedBuilding`)
   - Fallback para HQ se todos os prédios aliados estiverem ocupados
4. Move em direção ao prédio via `DecideReposition` com `targetOverride`

---

### `AIPlayerOrchestrator.Fuse.cs` — Partial class de fusão

#### `TryDecideFuse(UnitManager unit, TeamId myTeam, Vector3Int fromCell)`

Simula fusão em todas as posições alcançáveis antes de montar o batch:

1. Calcula caminhos válidos via `UnitMovementPathRules.CalcularCaminhosValidos`
2. Filtra posições ocupadas por outras unidades
3. Para cada posição candidata:
   - Calcula o custo de movimento para chegar até ela via `CalculateAutonomyCostForPath`
   - Passa `remainingMove - costToDest` ao sensor — movimento restante **após** chegar, não o total
   - Consulta `PodeFundirSensor.CollectOptions` com `fromCell` simulado
   - Valida condição: `myHP + allyHP ≤ 10`
4. Escolhe o aliado com menos HP (menor perda por absorção)
5. Monta batch com `MoveTo = stopCell` (posição adjacente) e `TargetHex = candidateCell`

#### Correção do movimento restante simulado

O sensor recebia o movimento total da unidade, causando falsos positivos: a unidade chegava com 0 de movimento mas o sensor aprovava a fusão como se ela pudesse ainda se mover 1 hex. A correção subtrai o custo do caminho do `remainingMove` antes de passar ao sensor.

---

### `PodeFundirSensor.cs` — Parâmetro `fromCell`

```csharp
public static void CollectOptions(
    UnitManager selectedUnit,
    Tilemap boardTilemap,
    TerrainDatabase terrainDatabase,
    int remainingMovementPoints,
    List<PodeFundirOption> output,
    out bool hasAnyValid,
    Vector3Int? fromCell = null)
```

Mesmo padrão do `PodeMirarSensor`: `fromCell` substitui a posição real da unidade na consulta, permitindo simulação hipotética sem mover a unidade.

---

### `UnitData.cs` — Novos campos em Repair Decision

```csharp
public bool fuseWhileInRepair = false;
```

Quando ativo, a unidade tenta fundir com um aliado danificado antes de recuar para reparos. Desabilitado por padrão.

Campos removidos: `repairRecoverRequiresResupply` — a saída de reparo depende apenas de HP e recursos, não de ressuprimento recebido no turno.

---

### `ReplayManager.cs` — Navegação de listas com cursor.mp3

#### Fusão replay — cycling visual

Antes: `TryQueueAutomatedMergeReplayOrder` confirmava o candidato diretamente sem animação.

Agora: cicla pelos candidatos da lista um por um usando `StepMergeForReplay()` até chegar ao escolhido, com `cursor.mp3` a cada passo e `sensorListNavDelay` de delay entre passos — idêntico ao comportamento do combate.

#### Combate replay — cursor.mp3 restaurado

`StepMirandoForReplay()` agora chama `cursorController?.PlayCursorMoveSfx()` a cada passo de navegação, replicando o som que o jogador humano ouve ao ciclar com as setas.

**Timer unificado:** ambos os fluxos (fusão e combate) usam o mesmo slider `sensorListNavDelay` do `ReplayManager`.

---

### `TurnStateManager.Merge.cs` — APIs de replay

```csharp
public int GetMergeCurrentIndexForReplay()
public int FindMergeTargetIndexForReplay(string targetInstanceId)
public bool StepMergeForReplay()
```

Espelho das APIs equivalentes do Mirando (`GetMirandoCurrentIndexForReplay`, `FindMirandoTargetIndexForReplay`, `StepMirandoForReplay`). `StepMergeForReplay` usa `FindNextValidMergeCandidateIndex` internamente e move o cursor com `playMoveSfx: true`.

---

### `UnitManager.cs` — Correções de ícone e estado

- `SetIsUnderRepair(bool value)`: setter público para o flag controlado pela IA
- `RefreshAIAssignedPlanBadge`: `SetMaintenanceIconVisible` agora depende apenas de `aiMaintenanceActive`, sem gating por `aiStanceVisible`

---

## Fluxo de decisão completo (v1.8.4)

```
ActivateUnit(unit)
│
├── EvaluateRepairState(unit)
│   └── isUnderRepair → SetAIMaintenanceActive(showAISprites)
│
├── isUnderRepair = true?
│   ├── fuseWhileInRepair? → TryDecideFuse → BuildFuseBatch
│   └── sem fusão → FindNearestAlliedBuilding → DecideReposition(targetOverride)
│
└── isUnderRepair = false → DecideBatch(unit)
    ├── TryFindAttack → BuildAttackBatch
    ├── TryDecideCapture → BuildCaptureBatch
    └── DecideReposition / DecideCautiousApproach → BuildMoveBatch
```

---

## Sistemas herdados sem alteração

- Animação, fog of war, gravação de replay: via `ReplayManager.ExecuteLiveAIBatch()`
- Timing geral entre batches: `timeBetweenBatches`
- Delay de preview antes do confirm: `beforeConfirmDelay`
- Delay de navegação de lista: `sensorListNavDelay` (agora compartilhado por fusão e combate)
