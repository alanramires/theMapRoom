# Antes do refactor de FoW e Detection

Versao: v1.4.4  
Status: checkpoint pre-refactor

## Resumo
- Fechado checkpoint tecnico antes de iniciar refactor de Fog of War e Detection.
- Pacote de baixo risco aplicado em troca de turno para reduzir custo do `OnActiveTeamChanged`.
- Ajustes de load assincrono consolidados para reduzir travamentos no thread principal.

## Entregas principais

### 1) Turno e desempenho (`OnActiveTeamChanged`)
- `UnitManager.HandleActiveTeamChanged` otimizado:
  - refresh completo apenas para unidades do time ativo anterior e novo ativo.
  - unidades fora desse escopo fazem update minimo.
- `ConstructionManager.HandleActiveTeamChanged` otimizado:
  - troca de `force:true` para `force:false` no refresh de runtime para aproveitar short-circuit.
- Instrumentacao de performance adicionada:
  - `[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=N ms=Y`
  - `[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=N ms=Y`

### 2) Occupancy / pathing
- Regras de passagem por aliado mantidas para path valido, sem permitir finalizar no mesmo hex.
- Ajustes de debug/logs de path e range para diagnostico de mismatch preview x confirm.
- Consolidação de editor/debug do `TurnStateManager` para remover duplicidade de flags e manter logs/perf agrupados.

### 3) Save/Load e replay
- Load assincrono ajustado:
  - `Task.Run` para leitura/decompress.
  - `JsonUtility.FromJson` retornado ao main thread.
- Warmup de threat cache no load agora controlado por toggle:
  - `enableThreatCacheWarmupOnLoad` (default `false`).
- Mensagem de carregamento aplicada durante load:
  - `Carregando jogo, aguarde`.
- Replay/save:
  - persistencia de replay no save condicionada a `IsRecording`.
  - F9 bloqueado quando replay nao esta em contexto valido.

## Estado antes do proximo passo
- Branch em estado de checkpoint com foco em estabilidade e observabilidade.
- Proximo bloco planejado: refactor de FoW/Detection (reduzir custo por celula/LoS e clarificar caches).
