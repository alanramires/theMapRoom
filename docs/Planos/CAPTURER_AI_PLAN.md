# Plano de Implementação — IA do Capturador V2

> **Contexto:** Unity + C#. Partial class `AIController` dividida em
> `AIController.cs`, `AIController.Capturer.cs`, `AIController.PlanEvaluator.cs`.
> Entrada principal: `DecideUnitAction` → `TryDecideCapturerAction` → HexEvaluator (fallback).

---

## Status Geral

| Fase | Descrição | Status |
|------|-----------|--------|
| 0 | Fundamentos e correções pontuais | ✅ Concluída |
| 1 | Substituir `canAdvance` por `ScoreHex` | ✅ Concluída |
| 2 | `CalculateThreatLevel` com FoW | ✅ Concluída |
| 3 | `CanAttackTargetFrom` + `attackHex` seguro | ✅ Concluída |
| 4 | Testes in-game e calibração de pesos | ⬜ Pendente |
| 5 | Extrair pesos para ScriptableObject (se necessário) | ⬜ Pendente |
| 6 | Estender scoring ao avanço do Rogue | ⬜ Pendente |

---

## Fase 0 — Fundamentos e correções pontuais ✅

Correções já aplicadas nesta sessão. Servem de baseline para as fases seguintes.

- [x] Refatorar decisão do capturador em partial class `AIController.Capturer.cs`
- [x] Separar fluxo Assigned (`DecideAssignedCapturerAction`) e Rogue (`DecideRogueCapturerAction`)
- [x] Remover `HasEnemyNearCell(targetCell)` early-return (causava freeze por delegação ao HexEvaluator)
- [x] `FindCapturableInSector` agora retorna o prédio **mais próximo da unidade** no setor (não o primeiro da lista)
- [x] `TryFindBestLoSCell` respeita `UnitData.prioritizeDpqAtBattle`: usa `DPQData.Pontos` ou `ev` conforme a flag
- [x] Adicionar helper `GetTerrainDpqPontos` paralelo ao `GetTerrainEv`
- [x] `FindBestAttackTarget` dá +10 000 de score a inimigos sobre construções (preferência do capturador)
- [x] `SolveAssignment` (backtracking): minimiza distância total capturador→setor na atribuição de planos

---

## Fase 1 — Substituir `canAdvance` por `ScoreHex` ⬜

**Arquivo:** `Assets/Scripts/Match/AI/AIController.Capturer.cs`  
**Método alvo:** `DecideAssignedCapturerAction` (bloco final do `canAdvance`)

### O que muda

O loop atual escolhe o hex geometricamente mais próximo do alvo (distância pura).
Substituir por `ScoreHex` que pontua cada hex alcançável com três fatores.

### Implementação

```csharp
// Constantes nomeadas (topo do arquivo, dentro da partial class)
private const float CaptureProximityWeight = 500f;
private const float DpqWeight              = 200f;
private const float ThreatWeight           = 50f;   // sinal negativo no uso
private const float AttackHexBonus         = 800f;
private const float SafetyThresholdFactor  = 0f;    // score mínimo pré-bônus para aceitar attackHex

private float ScoreHex(Vector3Int cell, Vector3Int targetCell, float threatLevel)
{
    float dist            = Vector3Int.Distance(cell, targetCell);
    float captureProximity = 1f / (dist + 1f) * CaptureProximityWeight;
    float dpq             = GetTerrainDpqPontos(cell) * DpqWeight;
    float threat          = threatLevel * ThreatWeight;
    return captureProximity + dpq - threat;
}
```

### Substituição no fluxo

```
// ANTES
Vector3Int bestMove     = fromCell;
float      bestMoveDist = Vector3Int.Distance(fromCell, targetCell);
bool       canAdvance   = false;

foreach (Vector3Int cell in paths.Keys)
{
    if (occupied.Contains(cell)) continue;
    float dist = Vector3Int.Distance(cell, targetCell);
    if (dist < bestMoveDist) { bestMoveDist = dist; bestMove = cell; canAdvance = true; }
}

if (!canAdvance) return null;
return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestMove);

// DEPOIS
Vector3Int bestMove  = fromCell;
float      bestScore = float.MinValue;
bool       canAdvance = false;

foreach (Vector3Int cell in paths.Keys)
{
    if (occupied.Contains(cell)) continue;
    if (Vector3Int.Distance(cell, targetCell) >= Vector3Int.Distance(fromCell, targetCell)) continue; // só hexes que aproximam
    float threat = CalculateThreatLevel(cell, snapshot.AITeam);
    float score  = ScoreHex(cell, targetCell, threat);
    if (score > bestScore) { bestScore = score; bestMove = cell; canAdvance = true; }
}

if (!canAdvance) return null;
assigned.Status = ObjectiveStatus.Pursuing;
return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestMove);
```

### Critério de aceite
- Capturador escolhe hex de DPQ alto sobre hex geometricamente mais próximo quando o ganho de distância é pequeno
- Capturador não avança para hex com ameaça extrema quando existe alternativa de DPQ razoável

---

## Fase 2 — `CalculateThreatLevel` com FoW ⬜

**Arquivo:** `Assets/Scripts/Match/AI/AIController.Capturer.cs`

### Implementação

```csharp
private const int ThreatRadius = 3;

private static float CalculateThreatLevel(Vector3Int cell, TeamId aiTeam)
{
    float threat = 0f;
    MatchController mc = GetMatchController();
    foreach (UnitManager enemy in UnitManager.AllActive)
    {
        if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
        if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue; // FoW obrigatório
        Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
        int dist = Mathf.RoundToInt(Vector3Int.Distance(cell, ec));
        if (dist <= ThreatRadius)
            threat += (ThreatRadius - dist + 1) * 10f; // peso decrescente com distância
    }
    return threat;
}
```

### Limitação conhecida
Usa distância geométrica, não alcance real da arma inimiga. Artilharia com `minRange > 1`
próxima é superestimada como ameaça. Aceitável como aproximação na fase atual.

### Critério de aceite
- Capturador não atravessa hex coberto por 3+ inimigos visíveis quando há rota alternativa
- Inimigos invisíveis (FoW) não influenciam a pontuação

---

## Fase 3 — `CanAttackTargetFrom` + `attackHex` seguro ⬜

**Arquivo:** `Assets/Scripts/Match/AI/AIController.Capturer.cs`  
**Dependência:** Fases 1 e 2 concluídas

### Objetivo

Permite ao capturador **planejar movimento + ataque** contra o defensor do prédio,
em vez de só reagir quando o inimigo entra no alcance da posição atual.

### Implementação de `CanAttackTargetFrom`

```csharp
private bool CanAttackTargetFrom(Vector3Int fromCell, Vector3Int toCell,
    UnitManager unit, UnitManager target)
{
    bool hasMoved = toCell != fromCell;
    SensorMovementMode mode = hasMoved
        ? SensorMovementMode.MoveuAndando
        : SensorMovementMode.MoveuParado;

    var targets = new List<PodeMirarTargetOption>();
    bool hasAny = PodeMirarSensor.CollectTargets(
        unit, boardTilemap, terrainDatabase, mode, targets, fromCell: toCell);

    if (!hasAny) return false;
    foreach (PodeMirarTargetOption opt in targets)
        if (opt?.targetUnit == target) return true;  // atenção: campo é targetUnit (camelCase)
    return false;
}
```

### Integração no loop de scoring (Fase 1)

```csharp
Vector3Int bestMove   = fromCell;
float      bestScore  = float.MinValue;
bool       canAdvance = false;

// Variáveis para attackHex
Vector3Int attackMove   = fromCell;
float      attackScore  = float.MinValue;
bool       hasAttackHex = false;
UnitManager defender    = HexOccupancyQuery.FindUnitAtCell(targetCell);
bool defenderVisible    = defender != null
    && defender.TeamId != snapshot.AITeam
    && GetMatchController()?.IsUnitVisibleForTeam(defender, snapshot.AITeam) == true;

foreach (Vector3Int cell in paths.Keys)
{
    if (occupied.Contains(cell)) continue;
    if (Vector3Int.Distance(cell, targetCell) >= Vector3Int.Distance(fromCell, targetCell)) continue;

    float threat = CalculateThreatLevel(cell, snapshot.AITeam);
    float score  = ScoreHex(cell, targetCell, threat);

    if (score > bestScore) { bestScore = score; bestMove = cell; canAdvance = true; }

    // Checa se a partir deste hex é possível atacar o defensor
    if (defenderVisible && score >= SafetyThresholdFactor) // segurança mínima
    {
        if (CanAttackTargetFrom(fromCell, cell, unit, defender))
        {
            float aScore = score + AttackHexBonus;
            if (aScore > attackScore) { attackScore = aScore; attackMove = cell; hasAttackHex = true; }
        }
    }
}

// Prioridade: atacar defensor > avançar
if (hasAttackHex)
{
    assigned.Status = ObjectiveStatus.Pursuing;
    Vector3Int targetCellForAttack = defender.CurrentCellPosition; targetCellForAttack.z = 0;
    return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackMove,
        defender.InstanceId.ToString(), targetCellForAttack);
}

if (!canAdvance) return null;
assigned.Status = ObjectiveStatus.Pursuing;
return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestMove);
```

### Critério de aceite
- Quando defensor visível ocupa o prédio alvo e existe hex alcançável com linha de tiro,
  o capturador move + ataca em vez de só avançar
- Capturador não escolhe `attackHex` com score negativo (posição suicida)

---

## Fase 4 — Testes in-game e calibração ⬜

### Checklist de validação

- [ ] Capturador assigned avança para prédio mais próximo do setor (não primeiro da lista)
- [ ] Capturador prefere hex de DPQ alto quando gain de distância é marginal
- [ ] Capturador não congela quando inimigo está perto do alvo (bug original)
- [ ] Capturador move + ataca defensor no prédio quando tem linha de tiro
- [ ] FoW passinho continua funcionando (ocupante invisível no alvo → move adjacente)
- [ ] Captura oportunista no caminho continua funcionando
- [ ] Rogue ainda delega ao HexEvaluator quando inimigo em raio de engajamento
- [ ] Post-capture: defende se inimigos próximos, aguarda se setor limpo

### Pesos atuais (ajustar conforme observação in-game)

| Constante | Valor inicial | Notas |
|-----------|---------------|-------|
| `CaptureProximityWeight` | 500 | Base de atração ao prédio |
| `DpqWeight` | 200 | DPQ Unique (+4) vale 800 pts |
| `ThreatWeight` | 50 | 3 inimigos a 1 hex = 120 pts de penalidade |
| `AttackHexBonus` | 800 | Supera DPQ Unique, fica abaixo de captura direta |
| `ThreatRadius` | 3 | Raio de checagem de ameaça |
| `SafetyThresholdFactor` | 0 | Score mínimo pré-bônus para aceitar attackHex |

---

## Fase 5 — ScriptableObject de pesos (opcional) ⬜

**Pré-condição:** pesos validados na Fase 4.

Criar `CapturerAIWeights : ScriptableObject` com os campos das constantes.
Referenciar via `[SerializeField]` em `AIController`. Somente se a calibração
mostrar que os valores precisam variar por dificuldade ou cenário.

---

## Fase 6 — Scoring no avanço do Rogue ⬜

**Pré-condição:** Fase 4 validada para assigned.

- Aplicar `ScoreHex` no loop de avanço de `DecideRogueCapturerAction`
  (atualmente escolhe hex mais próximo do HQ por distância pura)
- Manter delegação ao HexEvaluator quando `HasEnemyInEngageRadius` = true
- Reutilizar `TryFindOpportunisticCapture` já existente — não reimplementar

---

## Referência rápida — APIs relevantes

```
UnitMovementPathRules.CalcularCaminhosValidos(tilemap, unit, movPts, terrainDb)
  → Dictionary<Vector3Int, List<Vector3Int>>

PodeMirarSensor.CollectTargets(unit, tilemap, terrainDb, mode, targets, fromCell)
  → bool; preenche List<PodeMirarTargetOption>
  opt.targetUnit  ← campo correto (camelCase)

HexOccupancyQuery.FindUnitAtCell(cell)         → UnitManager ou null
ConstructionOccupancyRules.GetConstructionAtCell(tilemap, cell) → ConstructionManager ou null
MatchController.IsUnitVisibleForTeam(unit, team) → bool
TerrainTypeData.dpqData.Pontos                 → int 0–4
TerrainTypeData.ev                             → int (visão, não DPQ)
UnitData.prioritizeDpqAtBattle                 → bool
```

---

## Notas de arquitetura

- **`TryDecideCapturerAction` retorna `null`** apenas quando a unidade está sem movimento
  ou sem ação válida. Para assigned, cobrir todos os casos internamente.
  Para rogue em combate, continuar delegando ao HexEvaluator.
- **FoW passinho** (`TryFindBestLoSCell`) permanece como nó condicional explícito
  ativado por `HexOccupancyQuery.FindUnitAtCell(targetCell) + !IsUnitVisibleForTeam`,
  **não** como pré-fase de varredura de EV. Integrar antes do scoring.
- **`GetRogueTarget`** descrito pelo consultor já existe como `TryFindOpportunisticCapture`.
  Não reimplementar.
- **`CalculateThreatLevel`** usa distância geométrica — artilharia de longo alcance
  próxima é superestimada. Limitação aceitável até Fase 6.
