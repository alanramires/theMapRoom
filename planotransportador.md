# Plano — AIController.Transportador

## Visão geral

O transportador é um **vetor do capturador**: não captura, não explora FoW. Sua função é fazer ferries de unidades (infantaria, bazookas) de hexes próximos à base até objetivos distantes, cobrindo longas distâncias rapidamente (soldados movem 3, bazooka move 2, APC move 6). Após cada entrega, retorna para a próxima carga.

**Modo preferido**: rogue shuttle inteligente — sem slot formal em objetivos. Se alocado formalmente (plano distante), busca a unidade do plano.

---

## Como embarque e desembarque funcionam

**Embarque** — a ação é do **passageiro**:
- O passageiro usa `PodeEmbarcarSensor.CollectOptions(passenger, map, db, remainingMovement, options)` para encontrar transportadores adjacentes (range 1).
- O passageiro se move até o hex do transportador com `SensorActionType.Embark`.
- O transportador permanece no lugar; o passageiro "entra" nele.

**Desembarque** — a ação é do **transportador**:
- O transportador usa `PodeDesembarcarSensor.CollectOptions()` para listar hexes adjacentes livres onde cada passageiro pode desembarcar.
- Cada passageiro recebe um hex de destino (SubStep com `TargetInstanceId` + `TargetHex`).
- Após o desembarque, o passageiro fica `MarkAsActed()` (já agiu neste turno).

---

## Arquitetura em arquivos

### Novos arquivos
| Arquivo | Responsabilidade |
|---|---|
| `AIController.Transportador.cs` | Entry point `TryDecideTransportadorAction()`, despacho por estado |
| `AIController.Transportador.Shuttle.cs` | Rogue shuttle: scan de candidatos, pickup, retorno à base |
| `AIController.Transportador.Courier.cs` | Carregado: cálculo do drop-off, movimento evitando combate, emite desembarque |
| `AIController.Transportador.Assigned.cs` | Alocado em plano: busca a unidade do slot, depois opera como shuttle |

### Arquivos modificados
| Arquivo | Mudança |
|---|---|
| `AIController.Router.cs` | Adiciona `TryDecideTransportadorAction()` entre Assalto e fallback HexEvaluator |
| `AIController.Batches.cs` | Adiciona `BuildEmbarcarBatch()` e `BuildDesembarcarBatch()` |
| `AIController.PlanEvaluator.cs` | Slot `UnitRole.Transportador` em objetivos distantes; atribuição de transportes livres |
| `AIController.Capturer.cs` | Intercepção de embarque: capturador embarca quando transportador está adjacente |
| `AIController.Initiative.cs` | Transportadores rogue com candidato válido sobem para Group 1 |
| `AIShoppingPlanner.cs` | Compra por demanda de slots ou excesso de capturadores inimigos |
| `UnitData.cs` | Flag `preferMaxDisplacement` |

---

## Mecânica de embarque no turno

**Problema de coordenação**: o passageiro precisa ver o transportador adjacente para embarcar. Se o capturador agir antes do transporte, ele avança e o transporte perde a janela de pickup.

**Solução via iniciativa**:
- Transportador rogue com candidato válido (explicado abaixo) → **Group 1** (age antes dos capturadores do Group 2).
- Transportador age, se move adjacente ao candidato.
- Capturador age, detecta transporte adjacente → emite embarque em vez de avançar.
- Próximo turno: transporte carrega o passageiro ao objetivo.

**Fluxo de um ferry completo** (exemplo: Foxtrot a 10h):
- T1: Shopping compra APC (nasce na fábrica)
- T2: APC (Group 1) move-se adjacente ao capturador recém-saído da fábrica. Capturador (Group 2) detecta APC → embarca.
- T3: APC move 6h rumo ao setor Foxtrot.
- T4: APC chega e desembarca capturador a 1-2h do objetivo. Capturador captura.

Sem transporte: capturador andaria 3h/turno, chegaria em T5-T6 (10h ÷ 3h ≈ T3-T4 de deslocamento puro = T5 de chegada). Com transporte: chega em T4. Vantagem de 1-2 turnos em objetivos ≥ 7h.

---

## Lógica por estado

### Entry point — `TryDecideTransportadorAction()`

```
1. Verificar role primário = Transportador; se não → return null
2. TryDecideRepairAction → se em reparo, retorna
3. hasCargo = TransportedUnitSlots.Any(s => s.embarkedUnit != null && s.embarkedUnit.IsEmbarked)
4. assigned = ResolveAssignedTransportObjective(unit, plan)

5. if assigned != null → DecideAssignedTransportAction (→ busca unidade do plano ou, se tem carga, Courier)
6. elif hasCargo → DecideTransportadorCourierAction (entrega a carga)
7. else → DecideRogueShuttleAction (scan de candidatos)
```

---

### Courier (carregado, qualquer modo)

1. Coleta passageiros via `unit.TransportedUnitSlots` (embarked = true).
2. Para cada passageiro, resolve objetivo: `ObjectiveManager.GetPlanForTeam()` → encontra o slot do passageiro.
3. Se passageiro tem objetivo → alvo = capturable do objetivo.
4. Se passageiro não tem objetivo (rogue) → alvo = hex inimigo mais próximo do HQ inimigo.
5. **Drop-off point**: usa `PodeDesembarcarSensor.CollectOptions()` COMBINADO com distância ao alvo. Escolhe o hex adjacente que coloca o passageiro mais próximo do capturable, com DPQ tiebreak (passageiro sai em posição segura).
6. Se transporte já está no drop-off com desembarque válido → **BuildDesembarcarBatch**.
7. **Fallback de desembarque travado**: se `PodeDesembarcarSensor` retorna vazio na posição atual, procura hexes dentro do alcance de movimento que tenham opções válidas de desembarque adjacentes ao alvo e se move até o melhor. Se nenhum disponível (movimento zerado ou terreno fechado): `BuildMoveBatch(from, from)` — espera um turno sem travar.
8. Senão → move em direção ao drop-off com scoring de máximo deslocamento (`preferMaxDisplacement`).
9. **Combate carregado**: só ataca se inimigo HP ≤ 2 E o ataque não desvia mais de 2h da rota. Nunca escolhe hex de combate que atrase a entrega.
10. **Construções neutras/inimigas — penalidade, não exclusão**: ao avaliar hexes de parada, aplica `score -= 50000` em construções não controladas pelo time. O transporte evita naturalmente quando há alternativa; em terreno apertado sem outros hexes livres, aceita parar lá.

---

### Rogue Shuttle (vazio, sem plano)

**Objetivo**: encontrar o candidato de pickup de maior valor e se mover até ele.

**Critérios de candidato**:
1. Unidade amiga viva, não embarcada, não sob reparo.
2. Unidade cabe em algum slot do transporte (`UnitData.transportSlots` → `allowedClasses`).
3. `HexDistance(unit.CurrentCellPosition, objective.capturable) > MinDistanceForTransportSlot` — único critério de distância; se o objetivo está longe, o passageiro sempre se beneficia de transporte independente de onde ele está.
4. Unidade ainda não foi transportada neste turno (não `IsActed`).

**Score de candidato**:
```
score = distânciaObjetivo × 100          // quanto mais longe, mais urgente
      - distânciaTransporte × 50          // quanto mais perto do transporte, mais fácil de pegar
      - (slotPrioridade) × 10             // preferir capturadores sobre assaltos como passageiros
```

**Movimento**:
- Se candidato dentro do alcance de movimento (+ 1 hex para adjacência) → move-se para ficar adjacente ao candidato.
- Se fora do alcance → move em direção ao candidato, preferindo `preferMaxDisplacement`.
- Sem candidato → move-se em direção à fábrica mais próxima do time (espera próxima carga).

**Combate vazio**: pode atacar oportunisticamente (copia lógica do `TryFindAssaultBreakerAttack`), mas não desvia mais de 1h da rota de pickup.

**Não explora FoW**: não entra em sub-sistema de reveal (sem `TryFindAssaultScoutRevealMove`). Não captura.

---

### Assigned (plano formal)

Ocorre quando o PlanEvaluator criou um slot `UnitRole.Transportador` no objetivo:

1. Resolve a unidade-alvo: primeiro slot `Capturador` preenchido do mesmo objetivo.
2. Se unidade-alvo não existe ainda (slot não preenchido) → comporta como Rogue Shuttle mas priorizando o setor do plano como destino.
3. Se tem cargo de passageiro formal → Courier para esse objetivo.
4. Se recebe passageiro oportunista (outro embarque válido) → ainda entrega ao objetivo do passageiro.

---

## Intercepção no Capturador

Em `TryDecideCapturerAction`, após o repair check e antes da árvore de movimento principal:

```
TryDecideCapturerEmbarkAction(unit, snapshot, assigned):
  1. Usa PodeEmbarcarSensor.CollectOptions(unit, map, db, unit.RemainingMovementPoints, options)
  2. Se opções válidas existem:
     a. Filtra: apenas transportadores do mesmo time
     b. Prefere o transporte cujo objetivo-alvo é o mesmo objetivo do capturador (se houver)
     c. Fallback: qualquer transporte disponível
     d. → BuildEmbarcarBatch(passenger=unit, transporter=escolhido, slotIndex)
  3. Senão → return null (segue comportamento normal de captura)
```

O capturador **não espera ativamente** — se não houver transporte no alcance do sensor (range 1), avança normalmente. O transporte (Group 1) precisa ter se posicionado antes.

---

## Novos batch builders — `AIController.Batches.cs`

### BuildEmbarcarBatch (ação do PASSAGEIRO)
```csharp
private PlayerAction BuildEmbarcarBatch(
    UnitManager passenger, TeamId team, Vector3Int from,
    UnitManager transporter, int slotIndex,
    Dictionary<Vector3Int, List<Vector3Int>> paths = null)
{
    Vector3Int transporterCell = transporter.CurrentCellPosition; transporterCell.z = 0;
    List<Vector3Int> movementPath = null;
    paths?.TryGetValue(transporterCell, out movementPath);
    return new PlayerAction
    {
        IsAIGenerated = true,
        ActionType = PlayerActionType.UnitAction,
        ActingTeam = team,
        TurnNumber = matchController != null ? matchController.CurrentTurn : 0,
        CursorHex = from, HasCursorHex = true,
        UnitInstanceId = passenger.InstanceId.ToString(),
        MoveFrom = from, HasMoveFrom = true,
        MoveTo = transporterCell, HasMoveTo = true,
        SensorAction = SensorActionType.Embark,
        TargetInstanceId = transporter.InstanceId.ToString(),
        TargetHex = transporterCell, HasTargetHex = true,
        MovementPath = movementPath,
        DebugLabel = $"AI Embark {passenger.InstanceId} → {transporter.InstanceId}",
    };
}
```

### BuildDesembarcarBatch (ação do TRANSPORTADOR)
```csharp
private PlayerAction BuildDesembarcarBatch(
    UnitManager transporter, TeamId team, Vector3Int from,
    List<PodeDesembarcarOption> disembarkOrders,
    Dictionary<Vector3Int, List<Vector3Int>> paths = null)
{
    var action = new PlayerAction
    {
        IsAIGenerated = true,
        ActionType = PlayerActionType.UnitAction,
        ActingTeam = team,
        TurnNumber = matchController != null ? matchController.CurrentTurn : 0,
        CursorHex = from, HasCursorHex = true,
        UnitInstanceId = transporter.InstanceId.ToString(),
        MoveFrom = from, HasMoveFrom = true,
        MoveTo = from, HasMoveTo = true,
        SensorAction = SensorActionType.Disembark,
        DebugLabel = $"AI Disembark ← {transporter.InstanceId} ({disembarkOrders.Count} passageiro(s))",
    };
    foreach (PodeDesembarcarOption order in disembarkOrders)
    {
        Vector3Int targetCell = order.disembarkCell; targetCell.z = 0;
        action.SubSteps.Add(new PlayerActionSubStep
        {
            Label = "AIDisembark",
            TargetInstanceId = order.passengerUnit.InstanceId.ToString(),
            TargetHex = targetCell,
            HasTargetHex = true,
        });
    }
    return action;
}
```

---

## Plan Evaluator — slots de transporte

```csharp
[Header("Transportador")]
[Range(0, 20)] public int MinDistanceForTransportSlot = 7;
```

Em `BuildObjectivePlan`, ao criar slots de um novo objetivo:

```csharp
float distFromHQ = info.GetDistanceToHQ(aiTeam);
if (distFromHQ >= MinDistanceForTransportSlot)
    obj.Slots.Add(new SlotNeed { Role = UnitRole.Transportador });
```

Na atribuição (Passo 5 — após capturadores e assaltos):
- Iterar transportadores livres (`GetAvailableTransporters(aiTeam)`)
- Preencher slots `Transportador` abertos por proximidade ao setor objetivo

**Validação de slot existente**: na Passo 1 (validação de objetivos), se o slot Transportador está preenchido mas o transporte foi destruído ou está em reparo → `slot.Filled = false`.

---

## Shopping — `AIShoppingPlanner.cs`

**Transportador como assalto alternativo**: o transporte é classificado como unidade de suporte ofensivo. O plano rogue cuida de conectá-lo com capturadores.

**Triggers de compra**:
| Trigger | Condição |
|---|---|
| Demanda direta | `CountOpenSlots(Transportador) > 0` |
| Anti-infantaria | Comprado via lógica normal de assalto (é anti-inf natural) |

**Score em `PickUnit`**:
```csharp
bool isPrimaryTransporter = IsPrimaryRole(u, UnitRole.Transportador);
bool wantsTransport = openTransportSlots > 0;
if (isPrimaryTransporter && wantsTransport) score += 95000;
// (abaixo de elite assault 200k, acima de capturador puro 100k)
```

**Helper novo**: `CountOpenSlots(Transportador)` já existe via a função genérica de `CountOpenSlots`.

**Timing**: com demanda de slot desde o turno 1 (objetivo distante detectado), o shopping compra o transporte no turno 1-2 dependendo da renda. **Chegada esperada: T2-T3** para objetivos ≥ 7h.

---

## Iniciativa — `AIController.Initiative.cs`

**Regra nova**:

```
Se unit.role = Transportador E está vazio E tem candidato válido de pickup
    dentro de (movement range + 1) → Group 1
Senão se unit.role = Transportador E atribuído a objetivo → Group 2
    com sub-prioridade antes do capturador pareado
Senão → Group 3 (rogue normal)
```

**Lógica de "candidato válido"** (para Group 1):
- Qualquer unidade amiga não-embarcada dentro de `unit.RemainingMovementPoints + 1` hexes
- Que pode embarcar neste transporte (`UnitData.transportSlots` aceita a classe)
- Que tem objetivo com distância > `MinDistanceForTransportSlot`

A checagem é barata: apenas distância hex, sem path finding completo.

---

## Flag UnitData — `preferMaxDisplacement`

```csharp
[Tooltip("IA prioriza atingir o máximo de deslocamento possível em direção ao alvo " +
         "(favorece rotas de estrada por cobrirem mais hexes por turno).")]
public bool preferMaxDisplacement = false;
```

**Não requer mudança no pathfinder.** `CalcularCaminhosValidos` já retorna todos os hexes alcançáveis como chaves do dicionário. Implementar max-displacement é só scoring: iterar `paths.Keys` e escolher o hex que minimiza `HexDistance(cell, target)`. O bônus de estrada já está embutido no pathfinder (unidades com move ≥ 4 em hexes de estrada ganham 1 passo grátis via `useFreeRoadBonusStep`), então o APC com move=6 chega a 7h por estrada sem qualquer mudança de scoring.

**Implementação concreta**: reutilizar o padrão de `FindAssaultPressureMove` com `pressureTarget = dropOffCell` (Courier) ou `pressureTarget = candidateCell` (Shuttle).

**Uso na avaliação**: se aplica **somente em movimento** (Pickup, Courier em trânsito, retorno ao setor). Em combate oportunista (vazio), o scoring normal de DPQ/ameaça prevalece.

---

## Restrições do Transportador

| Comportamento | Regra |
|---|---|
| Não explora FoW | Sem lógica de reveal (`TryFindAssaultScoutRevealMove`) |
| Não captura | Nunca emite `SensorActionType.Capture` |
| Não bloqueia capturadores | Não para em hex de construção que não seja `IsFullyControlled && ControllingTeam == aiTeam` |
| Não usa DPQ de posição | `positionQuality` não é critério primário de parada |
| Combate carregado restrito | Só ataca se HP inimigo ≤ 2 E não desvia da rota de entrega |
| Combate vazio liberado | Pode pressionar inimigos como assault.rogue, com limite de 1h de desvio da rota |

---

## Sequência de implementação

Ordem de menor para maior risco de quebrar coisas existentes. Cada passo é compilável e testável antes do próximo.

### Passo 1 — `UnitData.cs`
Adiciona flag `preferMaxDisplacement`. Sem lógica de AI ainda; só dados.

### Passo 2 — `AIController.Batches.cs`
Adiciona `BuildEmbarcarBatch()` e `BuildDesembarcarBatch()`. Sem efeito ainda (ninguém os chama).

### Passo 3 — Controllers do Transportador (4 arquivos)
- `AIController.Transportador.cs` — entry point + despacho
- `AIController.Transportador.Shuttle.cs` — rogue vazio
- `AIController.Transportador.Courier.cs` — carregado
- `AIController.Transportador.Assigned.cs` — plano formal

Ainda sem dispatch no Router; testar via debug log manual.

### Passo 4 — `AIController.Router.cs`
Adiciona `TryDecideTransportadorAction()` entre Assalto e fallback. Transportador passa a agir.
**Teste**: transportador existente no mapa deve começar a tomar decisões de shuttle.

### Passo 5 — `AIController.Initiative.cs`
Group 1 para rogue com candidato válido. Sem isso o pickup não funciona no mesmo turno.
**Teste**: APC comprado junto com soldados deve agir antes dos soldados no T2.

### Passo 6 — `AIController.Capturer.cs`
Interceptação de embarque. Capturador detecta transporte adjacente e embarca.
**Teste**: soldado com transporte adjacente no início do turno deve emitir Embark em vez de avançar.

### Passo 7 — `AIController.PlanEvaluator.cs`
Slots `Transportador` em objetivos ≥ 7h. Atribuição de transportes livres a esses slots.
**Teste**: mapa com objetivo distante deve criar slot de transporte no plano.

### Passo 8 — `AIShoppingPlanner.cs`
Score de compra do transporte por demanda de slot.
**Teste**: AI com objetivo distante e budget deve comprar APC antes de terceiro capturador.

---

## Notas arquiteturais (não são bugs)

**APC em Courier não faz pickup novo (comportamento correto):**
Quando o APC tem carga (modo Courier), ele é Group 3. Capturadores que ele passa naquele turno avançam por conta própria. Isso é esperado — o APC está entregando, não captando. O próximo scan de shuttle (turno seguinte ou outro APC) cuida dos novos pickups. Documentar com comentário explícito no `TryDecideTransportadorAction` para evitar debug report futuro.

## Pendências a validar antes de implementar

- **Spawn no mesmo hex**: `PodeEmbarcarSensor` exige adjacência estrita (range 1 — escaneia `GetImmediateHexNeighbors`). Se APC e soldado spawnarem em fábricas adjacentes, o soldado já está a 1h do APC e pode embarcar no T2. Se spawnarem na *mesma* fábrica, não é possível (cada fábrica só vende 1 unidade por turno via `occupied.Add(cell)`). Sem problema.
- **Candidato já agiu**: a checagem de candidato válido no initiative (Passo 5) deve filtrar unidades com `unit.IsActed == true` para evitar tentar pickup de quem já encerrou o turno.
