# AI Roadmap — The Map Room v2.x

Melhorias planejadas para a IA após a base v2.0 (captura por setor, cascata, handoff, hex real).

---

## Estado atual (v2.0.7 + Fase 1 completa)

**Funciona bem:**
- Planejamento por setor com backtracking ótimo (custo real de pathfinding)
- Cascata dinâmica com consciência de direção de time
- Handoff: substituto herda captura parcial, vacater avança para setor forward
- FoW filtra inimigos do snapshot
- Distância hex real (pointy-top even-r) + BFS de terreno como fallback
- Ordenação de iniciativa ciente de ocupação (bloqueador age antes do bloqueado)
- Compra básica de unidades
- ✅ Stance defensiva reduz prioridade de incursões e bloqueia abertura de novos objetivos de risco
- ✅ Fábricas e HQs inimigos pesam mais que cidades genéricas no scoring de setor e no HexEvaluator

**Pontos fracos:**
- Unidades de assalto caem no HexEvaluator sem objetivo de combate
- Rogues marcham ao HQ inimigo em linha reta sem adaptação
- Sem escolta ou coordenação entre capturadores e combatentes

---

## Sequência de implementação

```
Fase 1 — Impacto imediato, mudanças cirúrgicas        ✅ COMPLETA
  [1] Stance defensiva muda prioridade de setores      ✅
  [2] Fábrica vs cidade no scoring de setor            ✅

Fase 2 — Comportamento ofensivo mais inteligente
  [3] Rogues com alvos dinâmicos
  [4] Shopping ciente de papeis de combate

Fase 3 — Funcionalidade nova
  [5] Unidades de assalto com objetivo de combate
```

---

## [1] Stance defensiva muda o plano ✅

**Arquivo:** `AIController.PlanEvaluator.cs` → `CalculateSectorPriority`, `BuildObjectivePlan`

**Implementado:**

- `CalculateSectorPriority` recebe `AIStance stance` e aplica:
  - **Defensive:** `+round(1/(dist+1)*30)` para setores próximos ao HQ; `-30` em setores `High`/`DeepRaid`
  - **Offensive:** `+20` em `High`/`DeepRaid`; `-10` em `Safe`/`Low`
- `BuildObjectivePlan` (passo 2): em Defensive, não abre novos objetivos em setores `Medium` ou piores — apenas mantém os já existentes
- Edge case: se nenhum objetivo sobrar em Defensive, adiciona o setor capturável mais próximo do HQ como fallback
- Todos os call sites de `CalculateSectorPriority` passam `snapshot.Stance`

---

## [2] Fábrica vs cidade no scoring de setor ✅

**Arquivo:** `AIController.PlanEvaluator.cs` → `CalculateSectorPriority`
**Secundário:** `HexEvaluator.cs` → `GetSectorStrategicWeight`

**Implementado:**

`CalculateSectorPriority` agora itera `info.Constructions` e soma `buildingValueBonus`:

| Tipo de construção | Bônus (Tactical/Offensive) | Bônus (Defensive) |
|--------------------|---------------------------|-------------------|
| HQ inimigo | +100 (cap 80) | +20 (cap 80) |
| Fábrica (`CanProduceUnits`) | +30 | +30 |
| Prédio com renda (`CapturedIncoming > 0`) | +15 | +15 |
| Prédio genérico | 0 | 0 |

Teto: `Mathf.Clamp(buildingValueBonus, 0, 80)`.

`HexEvaluator.GetSectorStrategicWeight` substituiu a heurística de setor (disputado/próximo-inimigo) por tipo de construção:

| Tipo | Peso |
|------|------|
| HQ inimigo | 3.0 |
| Fábrica | 2.0 |
| Prédio com renda | 1.5 |
| Prédio aliado completo | 0.5 |
| Default | 1.0 |

---

## [3] Rogues com alvos dinâmicos

**Arquivo:** `AIController.Capturer.cs` → `DecideRogueCapturerAction`

**Hoje:** `target = snapshot.EnemyHQ` fixo. O rogue avança em linha reta sem considerar valor de construções no caminho.

**Mudança:** substituir o alvo fixo por scoring sobre `EnemyBuildings + NeutralBuildings`:

```
rogueScore = buildingValue / hexDistance
```

Usando a mesma tabela de pesos de [2]. Uma fábrica inimiga próxima bate um HQ distante.

**Por stance:**
- **Defensive:** rogues não recebem alvo — reabsorvidos como combate auxiliar (HexEvaluator)
- **Offensive:** bônus de `+20` no score de alvos em setores `High`/`DeepRaid`

**Edge case:** se nenhuma construção for visível por FoW, fallback para `snapshot.EnemyHQ`.

---

## [4] Shopping ciente de papeis de combate

**Arquivo:** `AIShoppingPlanner.cs` → `PickUnit` e `CountNearbyOpenCapturerSlots`

**Hoje:** `PickUnit` só considera `UnitRole.Capturador`. Unidades de combate ganham score genérico.

**Mudança:** generalizar `CountNearbyOpenCapturerSlots(role)` para qualquer papel. Calcular dois contadores por fábrica: `openCapturerSlots` e `openAssaultSlots`.

**Ordem de prioridade por stance:**

| Stance | Prioridade de compra |
|--------|---------------------|
| Tactical | Balanceada por slot mais carente |
| Defensive | Combatentes primeiro, depois capturadores |
| Offensive | Capturadores primeiro, depois combatentes |

**Antes de [5]:** implementação parcial usando `CombatClassification` como proxy para unidades de combate, sem depender de `UnitRole.Assalto` nos slots.

---

## [5] Unidades de assalto com objetivo de combate

**Arquivos:** novo `AIController.Combat.cs` + mudanças em `AIController.PlanEvaluator.cs`, `AIController.cs` e `ObjectiveManager.cs`

Esta é a única melhoria que cria arquivo novo e altera o fluxo de `DecideUnitAction`.

### A. Slots de assalto no planejador

Em `BuildObjectivePlan`, após criar slots de `Capturador`, adicionar `SlotNeed { Role = Assalto }` condicionado ao risco do setor:

| Risco | Slots de Assalto |
|-------|-----------------|
| Safe / Low | 0 (stance Offensive: 0) |
| Medium | 1 |
| High / DeepRaid | 2 |
| Safe próximo ao HQ (Defensive) | 1 |

Rodar segundo backtracking (`SolveAssignment`) para atribuir unidades de combate aos slots abertos.

### B. Atribuição de unidades

Criar `GetAvailableCombatUnits` análogo a `GetAvailableCapturers`, filtrando por `UnitRole.Assalto` em `UnitData.roles` ou por `CombatClassification == Combatente / Hibrido`.

### C. Comportamento — `DecideAssaultUnitAction`

Prioridade de decisão por turno:

1. Inimigo visível em alcance de tiro na posição atual → atacar
2. Inimigo visível no raio de `movimento + 1` → mover para melhor hex de ataque (priorizando DPQ se `prioritizeDpqAtBattle`)
3. Capturador aliado atribuído ao mesmo setor → escolta a 1–2 hexes atrás ou ao lado (usar `BuildOccupied` para não bloquar a rota)
4. Fallback → avançar em direção ao centro de massa dos inimigos visíveis, mantendo cobertura de terreno positivo

### Integração em `DecideUnitAction`

Antes de chamar `TryDecideCapturerAction`, verificar se a unidade tem slot de `Assalto` atribuído e redirecionar para `DecideAssaultUnitAction`. Unidades com múltiplos papeis (ex. Capturador + Assalto): o fluxo de captura tem precedência.

### Edge cases

- Se o capturador escoltado for eliminado, o assaltante cai no Fallback (passo 4) automaticamente — não há estado persistente na escolta.
- Fábricas que só oferecem capturadores não são afetadas — `GetAvailableCombatUnits` retorna lista vazia e nenhum slot de Assalto é preenchido.

---

## Arquivos envolvidos por fase

| Fase | Arquivo principal | Método(s) |
|------|-------------------|-----------|
| [1] | `AIController.PlanEvaluator.cs` | `CalculateSectorPriority`, `BuildObjectivePlan` |
| [2] | `AIController.PlanEvaluator.cs` | `CalculateSectorPriority` |
| [2] | `HexEvaluator.cs` | `GetSectorStrategicWeight` |
| [3] | `AIController.Capturer.cs` | `DecideRogueCapturerAction` |
| [4] | `AIShoppingPlanner.cs` | `PickUnit`, `CountNearbyOpenCapturerSlots` |
| [5] | `AIController.PlanEvaluator.cs` | `BuildObjectivePlan`, `GetAvailableCombatUnits` |
| [5] | `AIController.cs` | `DecideUnitAction` |
| [5] | `AIController.Combat.cs` *(novo)* | `DecideAssaultUnitAction` |
| [5] | `ObjectiveManager.cs` | instanciação de `SlotNeed { Role = Assalto }` |
