# Relatório v1.8.3 — AI Aprendendo a Atirar

## Objetivo

Evoluir a IA batch-creator para tomar decisões de posicionamento de combate usando o mesmo sistema de qualidade de posição (DPQ) que o jogo já expõe para o jogador humano.

---

## O que mudou

### `AIPlayerOrchestrator.cs` — Lógica de decisão refatorada

#### Seleção de posição de ataque com DPQ (`prioritizeDpqAtBattle`)

Quando a flag `UnitData.prioritizeDpqAtBattle` está ativada na unidade, o comportamento de ataque muda de "primeira posição que funciona" para "melhor posição defensiva que permite atacar":

**Antes (sem flag):**
1. Tenta atacar parado → retorna imediatamente se possível
2. Tenta mover + atacar em cada célula livre → retorna na primeira que encontrar
3. Se não houver ataque, avança em linha reta ao inimigo mais próximo

**Agora (com flag `prioritizeDpqAtBattle`):**
1. Coleta **todas** as posições de ataque possíveis (parado + todas as células livres alcançáveis)
2. Pontua cada posição via `turnStateManager.GetCellDpqPoints()` — mesma fonte de verdade do jogo
3. Escolhe a posição com o maior DPQ
4. Em caso de empate ao avançar sem ataque, também prefere a célula com maior DPQ

#### Avanço com DPQ

Quando a flag está ativa e não há ataque possível, o avanço em direção ao inimigo passa a desempatar por DPQ: entre todas as células à mesma distância mínima do alvo, escolhe a com melhor posição defensiva.

---

## Arquitetura de resolução DPQ

A IA usa `TurnStateManager.GetCellDpqPoints(cell, unit)` — método já existente que resolve a cadeia de prioridade:

```
Construção (ocupada) → Estrutura → Terreno → default 1
```

Isso garante que a IA e o jogador humano usam exatamente a mesma fonte de verdade, sem duplicar lógica de resolução de terreno/DPQ no orquestrador.

---

## `UnitData.cs` — Novo atributo de comportamento

```csharp
[Header("Combat Behavior")]
public bool prioritizeDpqAtBattle = false;
```

Configurável por unidade no Inspector. Desativado por padrão — sem impacto em unidades existentes.

**Intenção de design:** unidades táticas (infantaria, bazooka, artilharia) podem ter a flag ativada para buscar cobertura antes de atirar. Unidades de manobra (blindados rápidos) ficam no comportamento padrão de primeiro ataque disponível.

---

## `PodeMirarSensor.cs` — Consulta hipotética de posição

Extensão adicionada em v1.8.2, consolidada aqui:

```csharp
public static bool CollectTargets(
    UnitManager attacker,
    Tilemap boardTilemap,
    TerrainDatabase terrainDatabase,
    SensorMovementMode movementMode,
    List<PodeMirarTargetOption> output,
    Vector3Int? fromCell = null)
```

O parâmetro `fromCell` permite perguntar "quais alvos eu poderia atacar **se estivesse** nessa célula?" sem mover a unidade. Somente a origem espacial da consulta muda — armas, layer, munição e stats vêm do attacker real.

---

## Fluxo de decisão completo (v1.8.3)

```
DecideBatch(unit)
│
├── prioritizeDpqAtBattle = true?
│   ├── Coleta todos os ataques possíveis (parado + movimento)
│   ├── Pontua por DPQ
│   └── Executa o melhor → BuildAttackBatch
│       └── Se nenhum ataque: avança com desempate DPQ
│
└── prioritizeDpqAtBattle = false?
    ├── Atacar parado (MoveuParado) → BuildAttackBatch
    ├── Mover e atacar (MoveuAndando, primeiro disponível) → BuildAttackBatch
    └── Avançar ao inimigo visível mais próximo → BuildMoveBatch
        └── Se sem inimigos visíveis: movimento aleatório
```

---

## Sistemas herdados sem alteração

- Animação, fog of war, gravação de replay: via `ReplayManager.ExecuteLiveAIBatch()`
- Timing entre batches: sliders do ReplayManager (timeBetweenBatches, sensorListNavDelay, beforeConfirmDelay, etc.)
- Navegação de lista de alvos: cycling via `StepMirandoForReplay()` com delay configurável
- Sincronização de fim de batch: `WaitUntil(() => !replayManager.IsStepExecutionBusy)`
