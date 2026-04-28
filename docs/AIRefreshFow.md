# Plano de tuning: AIRefreshFoW

## Objetivo

Reduzir as travadinhas causadas por `RefreshFogOfWarForActiveTeam()` durante o turno da IA sem perder a foto atual do jogo que a IA precisa para decidir o próximo batch.

A IA precisa recalcular a névoa depois de cada ação que muda visibilidade, porque o próximo `AIWorldSnapshot.Build()` e o próximo `DecideUnitAction()` acontecem antes de escrever o próximo `PlayerAction` no `ReplayManager`. O tuning não pode atrasar o cálculo lógico para o fim da rodada. Ele deve atrasar apenas o desenho visual.

## Diagnóstico atual

Hoje `AIController.Phase2_UnitActions()` executa uma unidade, espera o batch terminar e depois chama:

```csharp
matchController?.RefreshFogOfWarForActiveTeam();
```

Essa chamada está no lugar certo do ponto de vista lógico, porque a próxima unidade precisa enxergar o mundo atualizado. O problema é que `RefreshFogOfWarForActiveTeam()` mistura quatro trabalhos:

1. Descobrir células válidas do board.
2. Recalcular LOS e cache de células visíveis.
3. Redesenhar o tilemap da névoa.
4. Disparar eventos de UI/visuais via `OnFogOfWarUpdated`.

Durante o processamento da IA, o jogador não precisa ver a névoa sendo redesenhada entre uma unidade e outra. Mas a IA precisa dos caches lógicos atualizados.

## Restrições importantes

- `CursorState.Neutral` não significa fim do turno da IA.
- Cada unidade volta para `Neutral` depois do batch, e a IA então decide se move outra unidade, compra ou encerra.
- Portanto, não usar `Neutral` como gatilho para fazer refresh visual.
- `SuppressFogOfWarRefresh` não resolve, porque hoje ele pula tudo, inclusive o cache que a IA precisa.
- A IA deve continuar chamando refresh lógico depois de movimento ou ataque.
- A renderização visual completa deve acontecer quando o controle sair da IA ou quando for explicitamente necessário para o jogador.

## Separação proposta

Dividir o refresh de FoW em duas camadas:

### 1. Refresh lógico

Atualiza apenas dados usados por sensores, IA e regras:

- `fogBoardCellsBuffer`
- `fogVisibleCellsByUnit`
- `fogVisibleContributorsByCell`
- `fogUnitVisibilityByCacheIndex`
- `fogCachedTeamId`
- `fogOverlayInitialized`
- visibilidade lógica retornada por `IsUnitVisibleForTeam(...)`
- visibilidade lógica retornada por `IsCellVisibleForActiveTeam(...)`

Não deve:

- chamar `fogOfWarTilemap.ClearAllTiles()`
- chamar `SetTile`, `SetTileFlags` ou `SetColor`
- disparar `OnFogOfWarUpdated`
- redesenhar HUD ou overlays

### 2. Refresh visual

Sincroniza o tilemap e listeners visuais a partir do cache lógico já calculado:

- desenha a cobertura de fog nas células do board
- remove fog das células visíveis
- aplica cor/alpha do overlay
- chama `OnFogOfWarUpdated` uma vez

## API sugerida

Adicionar uma forma explícita de escolher o modo do refresh:

```csharp
public enum FogOfWarRefreshMode
{
    FullVisual,
    DataOnly
}

public void RefreshFogOfWarForActiveTeam(
    FogOfWarRefreshMode mode = FogOfWarRefreshMode.FullVisual)
```

Alternativa mais simples:

```csharp
public void RefreshFogOfWarForActiveTeam(bool visualRefresh = true)
```

Preferência: usar enum. O enum deixa a intenção clara e evita booleano ambíguo.

## Mudanças em MatchController

### Separar inicialização

Extrair de `InitializeFogOverlay(boardMap)` a parte que só coleta células:

```csharp
private void InitializeFogRuntimeData(Tilemap boardMap)
```

Responsabilidade:

- limpar `fogBoardCellsBuffer`
- chamar `CollectBoardCells(boardMap, fogBoardCellsBuffer)`
- marcar `fogCachedTeamId`
- marcar `fogOverlayInitialized`

Não escreve no tilemap.

### Manter draw visual separado

Criar método para desenhar o overlay completo:

```csharp
private void RenderFogOverlayFromRuntimeCache(Tilemap boardMap)
```

Responsabilidade:

- limpar o tilemap da fog
- preencher todas as células com tile de fog
- remover fog das células presentes em `fogVisibleContributorsByCell`
- aplicar alpha/cor

Esse método deve ser chamado apenas no modo `FullVisual`.

### Evitar writes em ApplyFogContribution durante DataOnly

Hoje `ApplyFogContribution(cell, delta, boardMap)` atualiza o dicionário e também escreve no tilemap quando a célula muda de estado.

Opções:

1. Adicionar parâmetro:

```csharp
private void ApplyFogContribution(
    Vector3Int cell,
    int delta,
    Tilemap boardMap,
    bool updateVisual)
```

2. Ou separar em dois métodos:

```csharp
private void ApplyFogContributionToCache(Vector3Int cell, int delta)
private void ApplyFogContributionVisual(Vector3Int cell, int current, int next, Tilemap boardMap)
```

Preferência: separar cache e visual se a mudança ficar simples. Isso reduz risco de voltar a acoplar render com lógica.

## Mudanças em AIController

Na fase de ações, trocar o refresh completo por refresh lógico:

```csharp
if (unitMoved || unitAttacked)
    matchController?.RefreshFogOfWarForActiveTeam(FogOfWarRefreshMode.DataOnly);
```

Isso mantém a foto atual para:

- `AIWorldSnapshot.Build(...)`
- `TryDecideCapturerAction(...)`
- `CalculateThreatLevel(...)`
- `HasEnemyBlockingPath(...)`
- `HasEnemyNearCell(...)`
- `IsUnitVisibleForTeam(...)`

## Quando fazer refresh visual

Não usar `CursorState.Neutral` como gatilho.

Pontos seguros:

1. Quando o active team deixa de ser IA.
2. Antes ou durante `Phase4_EndTurn()`, se ainda for necessário mostrar um estado consistente antes da transição.
3. Em `MatchController.ApplyActiveTeam` quando o novo time ativo for humano.

Critério prático recomendado:

- Durante turno da IA: `DataOnly`.
- Ao aplicar time humano ativo: `FullVisual`.
- Em carregamento/replay/debug/manual: manter `FullVisual`, salvo caso específico.

## Risco principal

O maior risco é `unit.SetFogOfWarVisibility(...)` dentro de `RefreshRuntimeUnitFogVisibility()`.

Mesmo em `DataOnly`, esse método pode alterar visibilidade de sprites e disparar efeitos de revelação. Isso é mais barato que redesenhar o tilemap, mas ainda pode causar mudança visual intermediária.

Decisão necessária na implementação:

- Se a IA só precisa de `fogUnitVisibilityByCacheIndex`, criar modo lógico que atualiza o dicionário sem chamar `SetFogOfWarVisibility`.
- Se a visibilidade real dos GameObjects precisa acompanhar a IA por regra de execução, manter `SetFogOfWarVisibility`, mas suprimir apenas tilemap e evento.

Primeira implementação recomendada: suprimir tilemap e `OnFogOfWarUpdated`, mas manter `RefreshRuntimeUnitFogVisibility()`. Se ainda houver travadinhas ou flicker visual, separar também `RefreshRuntimeUnitFogVisibility()` em cache-only e visual-apply.

## Ordem de implementação

1. Criar `FogOfWarRefreshMode`.
2. Alterar `RefreshFogOfWarForActiveTeam()` para aceitar modo.
3. Separar coleta de células do desenho visual.
4. Fazer `ApplyFogContribution` aceitar ou respeitar modo sem escrever no tilemap em `DataOnly`.
5. Garantir que `DataOnly` atualize `fogVisibleContributorsByCell`.
6. Garantir que `DataOnly` atualize `fogUnitVisibilityByCacheIndex`.
7. Trocar a chamada da IA para `DataOnly`.
8. Garantir um `FullVisual` quando a IA efetivamente deixa de controlar o turno.
9. Testar movimento, ataque, compra, fim de turno, save/load e replay.

## Testes manuais

### Caso 1: IA move várias unidades

Esperado:

- sem travadinhas fortes entre unidades
- decisões continuam usando inimigos recém-revelados
- tilemap de fog não fica redesenhando a cada unidade

### Caso 2: IA move e revela alvo

Esperado:

- próxima unidade da IA pode considerar o alvo revelado
- `IsUnitVisibleForTeam(enemy, aiTeam)` retorna valor atualizado
- visual só precisa estar correto quando jogador voltar a observar

### Caso 3: IA ataca e mata bloqueador de LOS

Esperado:

- cache lógico muda depois do ataque
- próxima unidade decide com LOS atualizado
- não há redesenho visual repetido durante processamento automático

### Caso 4: fim do controle da IA

Esperado:

- ao voltar para time humano, o tilemap da fog está coerente
- UI/HUD recebem um único `OnFogOfWarUpdated`

### Caso 5: replay e load

Esperado:

- replay continua renderizando FoW normalmente
- save/load continua restaurando cache e visual
- `SuppressFogOfWarRefresh` mantém comportamento atual para carregamento, salvo refatoração explícita posterior

## Métricas de sucesso

Adicionar logs temporários ou usar os existentes:

- tempo total de refresh FoW durante IA
- tempo de coleta LOS
- quantidade de writes no tilemap por batch
- quantidade de `OnFogOfWarUpdated` por turno da IA

Meta:

- durante ações de unidade da IA, writes no tilemap devem cair para zero
- `OnFogOfWarUpdated` deve cair para zero entre batches da IA
- decisões da IA não devem perder informação de visibilidade

## Resultado esperado

O turno da IA continua logicamente correto, porque a foto do mundo é atualizada depois de cada batch que altera visibilidade. A diferença é que o jogo para de pagar o custo visual completo da névoa em estados intermediários que o jogador não precisa ver.
