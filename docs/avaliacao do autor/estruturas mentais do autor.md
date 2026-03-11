# Estruturas mentais do autor

## 1) Quais estruturas de dados ou padroes aparecem repetidamente?
No codigo, os padroes recorrentes sao:

- Listas (`List<T>`): colecoes ordenadas de opcoes, alvos, linhas de helper, recursos, armas e filas de execucao.
- Mapas/dicionarios (`Dictionary<TKey, TValue>`): lookup por ID, cache de estado, custo por estado de caminho, agrupamento por celula e revisao por time.
- Conjuntos (`HashSet<T>`): deduplicacao de celulas pintadas, unidades ja processadas e controle de membership rapido.
- Filas (`Queue<T>`): exploracao de caminho e processamento progressivo de estados no movimento.
- Maquina de estados: `CursorState` e subpassos de scanner no `TurnStateManager`.
- Pipeline de sensores: familia `Pode*Sensor` retornando opcoes validas/invalidas.
- Cache com invalidacao por revisao: `ThreatRevisionTracker` + caches de overlay.
- Modelos de grafo em grid hex: vizinhanca imediata, mapa de distancias, caminhos por destino.

## 2) O uso dessas estruturas sugere raciocinio algoritmico consciente?
Sim, claramente.

Os exemplos mais fortes:

- Pathfinding com estado enriquecido em `UnitMovementPathRules`: usa fila + mapas de custo + reconstrução de caminho, com restricoes de autonomia e bonus de estrada.
- Escolha de fallback direcional em `HexPathResolver`: usa heuristica vetorial (produto escalar + penalidade de distancia).
- Sensores com validacao multicriterio (`PodeMirarSensor`, `ServicoDoComandoSensor`): filtragem, ordenacao, deduplicacao e explicacao de invalidez.
- Caching orientado a evento (`ThreatRevisionTracker`): evita recomputo desnecessario com revisoes globais e por time.
- Orquestracao por estado no `TurnStateManager`: transicoes explicitas, commit/rollback e controle de execucao concorrente de acoes.

Conclusao: as estruturas nao estao sendo usadas apenas por conveniencia. Elas refletem desenho intencional para resolver problemas de busca, elegibilidade, custo e consistencia de estado em um sistema tatico complexo.
