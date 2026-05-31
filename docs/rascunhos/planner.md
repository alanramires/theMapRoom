# Documentação: Sistema de Planning (Rally Points)

## 1. Visão Geral
O sistema de Planning permite que o jogador no turno ativo defina rotas e crie "Rally Points" (Pontos de Encontro) no mapa tático, além de designar comandos automáticos de movimento para as unidades selecionadas. Quando o turno de um jogador inicia, o sistema executa automaticamente os movimentos das unidades registradas em direção a estes pontos.

### 1.1 Entidades Principais
* **RallyPoint:** Guarda um ID único, um nome de exibição, a coordenada de destino (`Vector2Int`), o time proprietário (`TeamId`) e um status booleano `ativo`.
* **RallyAssignment:** Representa o vínculo entre uma `Unidade` (através de `unitId`) e um `RallyPoint`.
* **PlanningConfig:** Mantém as configurações gerais como quantidade máxima de Rally Points por time (atualmente hardcoded/serializado como 5).

### 1.2 O Core: `PlanningManager`
Esse script atua como controlador central de todas as regras de planning.

**Fluxo de UI e Inputs:**
- A ativação do modo é controlada via `TryEnterPlanningMode()`, desde que o cursor esteja neutro, não haja scanner rodando e não seja um replay.
- Quando ativo, o clique no mapa (sem colidir na UI) ou marca um *Pending Destination* para o próximo Rally Point ou intercala (Toggle) o *Assignment* de uma unidade existente ao Rally Point selecionado.
- Exibe highlights visuais: Unidades designadas pulsam de cor via manipulação do `SpriteRenderer` (`UpdateAssignedUnitsPulse()`) e os destions geram `GameObjects` com bandeiras dinâmicas (na camada de Sorting SFX).

**Execução Automática (TurnStart):**
- O método de corrotina `ExecuteTurnStartRallyPhase(TeamId)` roda antes do jogador receber o controle do turno.
- Para cada *Assignment* do time atual:
  - Validações rígidas retiram a unidade da fila se ela sair do mapa, sofrer dano (entrar em estado de combate) ou chegar a uma zona <= 2 hexágonos do alvo.
  - Confere se existe rota viável ignorando unidades pelo método `HasTraversableRouteIgnoringUnits` (uso de BFS).
  - Tenta progredir pelo menos 1 hexágono em rota com `TryResolveBestTurnProgressPath`. O percurso calculado leva em base a melhor aproximação possível para aquele turno usando `UnitMovementPathRules`.
  - Passa a responsabilidade de locomoção pontual para `TurnStateManager.ExecutePlanningMoveOnlyAlongPath()`.

**Persistência:**
- Os métodos `ExportPlanningData` e `ImportPlanningData` são consumidos pelo subsistema de serialização (`SaveGameManager` e DTOs) garantindo o espelhamento persistente entre as sessões.

---

## 2. Sugestões de Melhoria e Refatoração

Após a análise do código e fluxo de dados, alguns pontos podem ser otimizados visando performance, escalabilidade do projeto e UX:

### 2.1 Refatoração e Performance (Algoritmos)
* **Pathfinding Otimizado (A* em vez de BFS):** 
  Os métodos `HasTraversableRouteIgnoringUnits` e `ComputeBoardDistance` hoje utilizam fila BFS simples para encontrar distâncias usando vizinhos geográficos hex a hex. Há "guards" hardcoded (ex: `guard++ < 12000`) para evitar loops que travam a engine no caso do mapa ser vasto ou inteiramente bloqueado por cadeias de montanhas. Seria recomendado implementar/usar as regras de *A-Star (A*)* com cálculo heurístico que prioriza o caminho para as posições do alvo, rodando muito mais rápido e sem o risco de explorar milhares de hex de áreas erradas.
* **Cálculos Duplos de Caminho Evitáveis:**
  Durante a execução, ocorre uma checagem ampla (`HasAnyOccupancyRouteProgress`) e então o recálculo via (`TryResolveBestTurnProgressPath`). Esses passos processam requisições pesadas de Pathfinding pelo terreno (em `UnitMovementPathRules.CalcularCaminhosValidos`) múltiplas vezes por unidade afetada. Um sistema de cache da malha de caminhos daquele turno poderia aliviar a carga caso existam muitas unidades seguindo para lugares próximos.

### 2.2 Visual e UI
* **Uso de Prefabs no lugar de instâncias literais:**
  As bandeiras ("rallyFlags") são instanciadas como GameObjects vazios em que componentes como Box e SpriteRenderer são adicionados pelo código no método `RefreshFlagsForActiveTeam()`. Abstrair isso para carregar um prefab `RallyFlag.prefab` facilitaria criar pequenas animações de sway na bandeira, instanciar partículas leves ou ajustar a posição âncora visualmente pela Unity de forma mais organizada que linhas de código acopladas.
* **Cor do Sprite Pulsante afeta Batching:**
  A coloração das unidades designadas sofre manipulação de cor de sprite literal em todo Update (`SpriteRenderer.color = pulse`). Isso consome leve recurso e quebra *static/dynamic batching* se existirem muitas unidades. Uma técnica de performance mais fluída envolve injetar a mudança de cor através de um Material Property Block (`SetPropertyBlock`), aliviando a carga drástica do lado da GPU e otimizando renderizações.

### 2.3 Gameplay e UX
* **Seleção por Área / Drag Select:** 
  A adição de nós se resume a clicar unidade por unidade se tiverem 10 delas a andar juntas. Implementar um retângulo de seleção que faça um check de intersecção adicionaria todos num pacote (um "Lasso Select") e enriqueceria intensamente o onboarding da feature de Planning.
* **Visualização da Rota:**
  No momento do clique em modo restrito, mostrar um pontilhado fraco (Line Renderer) da unidade selecionada até o seu destino daria uma clareza magistral para o jogador, prevendo por onde o A* pretende rotear os movimentos.
