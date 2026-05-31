# Capturer Refiner

## Objetivo

Refinar a IA de capturadores para lidar melhor com captura parcial quando a unidade em cima da construção está ferida e existe outro capturador aliado mais saudável nas proximidades.

Caso-alvo:

- unidade capturadora entra no prédio com 10 HP e marca 10 pontos de captura
- inimigo ataca e a unidade cai para 4 HP
- no turno seguinte, ela poderia capturar de novo e levar o prédio para 14 pontos
- mas existe outro capturador aliado com 10 HP perto o bastante para entrar e finalizar

Nesse caso, pode ser melhor a unidade ferida sair da construção e deixar a unidade saudável terminar o serviço.

## Ideia central

Adicionar uma decisão de `handoff` de captura.

Antes de uma unidade ferida capturar automaticamente no prédio onde está, ela avalia se existe um aliado melhor para assumir aquela captura neste turno. Se existir, ela sai para um hex seguro e libera a construção. O loop da IA reconstrói a foto do mundo depois do batch, então a próxima unidade pode decidir entrar e capturar com mais HP.

## Onde encaixar

Arquivo principal:

- `Assets/Scripts/Match/AI/AIController.Capturer.cs`

Ponto de decisão atual:

```csharp
if (fromCell == targetCell)
{
    if (SimulateCaptureSensor(unit, targetCell, out _))
    {
        assigned.Status = ObjectiveStatus.Capturing;
        return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, targetCell);
    }
    assigned.Status = ObjectiveStatus.Complete;
    return null;
}
```

O refinamento entra antes do `BuildCaptureBatch`.

## Comportamento desejado

Quando a unidade está no prédio alvo:

1. Verificar se ela pode capturar.
2. Verificar se ela está ferida o suficiente para considerar handoff.
3. Procurar capturador aliado disponível e mais saudável.
4. Confirmar que esse aliado consegue alcançar a construção neste turno.
5. Confirmar que o aliado capturaria melhor que a unidade atual.
6. Encontrar um hex de saída para a unidade ferida.
7. Reatribuir o objetivo ou registrar a intenção de handoff.
8. Gerar um `MoveBatch` para a unidade ferida sair.

## Critérios de handoff

### Condições mínimas

Executar handoff somente se:

- a unidade atual está no `targetCell`
- a construção ainda não está totalmente capturada pelo time da IA
- `SimulateCaptureSensor(unit, targetCell, out target)` retorna verdadeiro
- a unidade atual está abaixo de um limiar de HP, por exemplo `CurrentHP <= 6`
- existe capturador aliado que ainda não agiu
- esse aliado não está morto, embarcado ou fundido
- esse aliado tem HP maior por uma margem relevante, por exemplo `ally.CurrentHP >= unit.CurrentHP + 3`
- esse aliado consegue alcançar `targetCell` neste turno
- `SimulateCaptureSensor(ally, targetCell, out sameTarget)` também retorna verdadeiro
- existe um hex válido para a unidade ferida sair

### Não fazer handoff se

- a unidade ferida já completa a captura agora
- não existe substituto que consiga entrar no prédio neste turno
- o substituto precisaria passar por uma célula bloqueada
- a unidade ferida não tem para onde sair
- o hex de saída bloqueia o caminho do substituto
- há ameaça imediata alta e sair do prédio é pior que concluir progresso parcial
- o substituto tem uma prioridade mais importante já atribuída

## Modelo de avaliação

Comparar valor de capturar agora contra valor de ceder.

### Capturar agora

```text
afterCurrent = currentCapturePoints + currentUnitHP
```

Para operação contra prédio inimigo/neutro, adaptar ao modelo real da captura:

- se `CaptureEnemy`, a captura reduz pontos até zero e depois muda dono
- se `RecoverAlly`, a captura aumenta pontos até o máximo

O importante é medir progresso efetivo da unidade atual.

### Ceder para aliado

```text
afterReplacement = currentCapturePoints + replacementHP
```

O handoff só vale se o substituto entrega ganho real:

```text
afterReplacement - afterCurrent >= minCaptureGain
```

Valor inicial sugerido:

```text
minCaptureGain = 3
```

Além disso, se o substituto completa a captura e a unidade atual não completa, handoff ganha prioridade máxima.

## API interna sugerida

Adicionar helpers privados ao partial `AIController.Capturer.cs`:

```csharp
private bool TryBuildCaptureHandoff(
    UnitManager unit,
    AIWorldSnapshot snapshot,
    SectorObjective assigned,
    ConstructionManager target,
    Dictionary<Vector3Int, List<Vector3Int>> currentUnitPaths,
    HashSet<Vector3Int> occupied,
    out PlayerAction action)
```

Responsabilidade:

- validar se handoff faz sentido
- escolher substituto
- escolher célula de saída
- atualizar plano se necessário
- devolver `BuildMoveBatch(...)` para a unidade ferida

Helpers menores:

```csharp
private bool TryFindHealthyCaptureReplacement(
    UnitManager wounded,
    TeamId aiTeam,
    ConstructionManager target,
    HashSet<Vector3Int> occupied,
    out UnitManager replacement)
```

```csharp
private bool TryFindCaptureRetreatCell(
    UnitManager wounded,
    UnitManager replacement,
    ConstructionManager target,
    Dictionary<Vector3Int, List<Vector3Int>> woundedPaths,
    HashSet<Vector3Int> occupied,
    TeamId aiTeam,
    out Vector3Int retreatCell)
```

```csharp
private void ReassignCaptureObjective(
    SectorObjective assigned,
    UnitManager from,
    UnitManager to)
```

## Escolha do substituto

Pontuar candidatos:

```text
score =
    hp * 100
  - distanceToTarget * 20
  - threatAtTarget * 30
  + completesCaptureBonus
  + alreadyAssignedToSameObjectiveBonus
```

Filtros:

- mesmo time
- capturador
- ainda não agiu
- não morto
- não embarcado
- não fundido
- consegue alcançar `targetCell`
- `targetCell` ficará livre depois da saída da unidade ferida

Preferências:

- maior HP
- mais perto
- já atribuído ao mesmo setor
- menor risco ao entrar

## Escolha do hex de saída

O hex de saída da unidade ferida deve:

- estar nos caminhos válidos dela
- não ser o `targetCell`
- não estar ocupado
- não bloquear o caminho do substituto, se o caminho for conhecido
- reduzir exposição a ameaças
- manter a unidade perto do setor, se possível

Scoring sugerido:

```text
score =
    terrainDpqOrEv
  - threat * ThreatWeight
  - distanceFromTarget * smallPenalty
  - blocksReplacementPathPenalty
```

Se não houver hex seguro, não fazer handoff.

## Integração com o plano

Existem duas opções.

### Opção A: reatribuir slot imediatamente

Quando o ferido sai:

- o slot do `SectorObjective` passa de `wounded.InstanceId` para `replacement.InstanceId`
- a unidade ferida vira rogue ou fica sem slot
- HUD do plano é atualizado nas duas unidades

Vantagem:

- simples
- o próximo `GetAvailableUnits` tende a tratar o substituto como dono do objetivo

Risco:

- se o substituto não agir logo depois por ordenação, outra lógica pode interferir

### Opção B: registrar intenção temporária de handoff

Criar estado temporário:

```csharp
CaptureHandoffIntent
{
    int woundedUnitId;
    int replacementUnitId;
    Vector3Int targetCell;
    ConstructionSector sector;
}
```

Na próxima decisão do substituto, priorizar cumprir o handoff.

Vantagem:

- comportamento mais controlado

Risco:

- mais estado para invalidar se alguém morrer, bloquear caminho ou capturar antes

Primeira implementação recomendada: Opção A. Se a ordenação gerar casos ruins, evoluir para Opção B.

## Relação com ordenação de unidades

Hoje `GetAvailableUnits` ordena por distância ao objetivo atribuído, iniciativa e HP.

Depois de reatribuir o slot, o substituto deve ficar mais bem posicionado na ordenação porque passa a ter o objetivo. Mesmo assim, pode ser útil ajustar desempate:

- capturador que consegue completar construção parcial deve agir antes
- capturador em handoff explícito deve agir antes

Isso pode ser um refinamento posterior.

## Casos que precisam cuidado

### Captura inimiga versus recuperação aliada

O cálculo de benefício precisa respeitar `PodeCapturarSensor.CaptureOperationType`.

- `RecoverAlly`: HP soma pontos até o máximo.
- `CaptureEnemy`: HP reduz pontos até zero; ao zerar, muda dono e reseta para máximo.

Não assumir sempre `current + hp`.

### Unidade ferida bloqueando o substituto

O substituto só consegue entrar depois que a unidade ferida sai. Então a simulação do caminho do substituto deve considerar o `targetCell` como livre.

Se a API de path atual não permite ignorar ocupação de destino, pode bastar:

- calcular caminhos normais do substituto
- aceitar `targetCell` se o pathing geométrico alcança
- validar ocupação manualmente considerando que o ferido saiu

### Ameaça inimiga

Sair do prédio pode expor a unidade ferida. A IA não deve trocar uma captura parcial por uma morte quase certa sem benefício forte.

Regra simples inicial:

- se handoff completa a captura, aceitar risco moderado
- se handoff só melhora poucos pontos, exigir hex de saída seguro

## Ordem de implementação sugerida

1. Adicionar `TryBuildCaptureHandoff(...)`.
2. Chamar esse helper antes da captura automática em `fromCell == targetCell`.
3. Implementar busca de substituto com filtros conservadores.
4. Implementar busca de hex de saída.
5. Reatribuir o slot do objetivo para o substituto.
6. Atualizar HUD/plano do ferido e do substituto.
7. Adicionar logs com prefixo `[AI][Handoff]`.
8. Testar primeiro apenas com capturadores atribuídos ao mesmo setor.
9. Depois liberar candidatos capturadores de outros setores se o ganho for alto.

## Logs úteis

Exemplos:

```text
[AI][Handoff] Unit12 ferida hp=4 em BaseNorte 10/20; Unit18 hp=10 pode completar, saindo para (4,7,0)
```

```text
[AI][Handoff][Skip] Unit12 completa captura sozinha; sem handoff
```

```text
[AI][Handoff][Skip] sem retreatCell seguro
```

```text
[AI][Handoff][Skip] melhor substituto hp=6 ganho insuficiente
```

## Testes manuais

### Caso 1: substituto completa

- construção em `10/20`
- capturador atual com `4 HP`
- aliado capturador com `10 HP` alcança o prédio

Esperado:

- ferido sai
- slot é transferido para o aliado
- aliado entra e captura

### Caso 2: ferido completa sozinho

- construção em `16/20`
- capturador atual com `4 HP`
- aliado com `10 HP` perto

Esperado:

- ferido captura e completa
- não há handoff

### Caso 3: substituto não alcança

- construção em `10/20`
- ferido com `4 HP`
- aliado com `10 HP` longe demais

Esperado:

- ferido captura
- não há handoff

### Caso 4: sem hex de saída

- ferido está cercado
- substituto saudável perto

Esperado:

- ferido captura ou cai no comportamento atual
- não tenta handoff impossível

### Caso 5: ameaça alta

- ferido sairia para hex muito ameaçado
- substituto só melhora poucos pontos

Esperado:

- não faz handoff

## Resultado esperado

A IA passa a preservar e acelerar capturas parciais de forma mais inteligente. Em vez de gastar ações fracas com unidades muito feridas, ela usa capturadores saudáveis para concluir construções quando o posicionamento permite, sem quebrar o fluxo atual de batch por unidade.
