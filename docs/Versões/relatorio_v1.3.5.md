# RELATORIO v1.3.5

## Tema
Refactor de inicio de rodada em andamento com foco em performance de FoW/visao, reducao de custo por refresh e instrumentacao de diagnostico.

## Entregas principais
- Instrumentacao temporaria de performance para FoW:
  - logs de cache: `[FoW][Cache] hits=X misses=Y`;
  - logs de pool de BFS: rents/releases gerais e recorte da Fragata.
- Reuso de estruturas no BFS de visao (`PodeDetectarSensor`):
  - `Dictionary + Queue + List` migrados para workspace com pool.
- Cache de `CollectVisibleCells` por chave de contexto:
  - chave inclui unidade, celula, alcance, flags de sensor e revisoes globais.
- Refactor do traco de LoS:
  - substituicao do oversampling por `cube_linedraw` (aritmetica de grid hex);
  - fallback mantido por flag `UseLegacyLoSLerp`.
- Otimizacao do caminho quente de terreno/LoS:
  - remocao de `GetComponentsInChildren<Tilemap>()` por chamada, com cache por `GridLayout`;
  - cache de terreno por celula no escopo do refresh FoW (limpo a cada refresh).
- Otimizacoes adicionais de LoS:
  - early-exit para `distance <= 1`;
  - cache de LoS por refresh para evitar recalculo repetido entre especializacoes/camadas.

## Mudancas tecnicas relevantes
- `Assets/Scripts/Sensors/PodeDetectarSensor.cs`
  - pooling de workspace de distancia;
  - caches de visao/terreno/LoS em escopo de refresh;
  - line draw em cube coords com rollback por flag legacy;
  - contadores de diagnostico para hits/misses e rents/releases.
- `Assets/Scripts/Match/MatchController.cs`
  - reset/leitura dos contadores de debug FoW;
  - limpeza do cache de terreno/LoS no inicio de `RefreshFogOfWarForActiveTeam`.
- `Assets/Editor/MatchControllerEditor.cs`
  - exposicao de campos de perf de FoW no editor custom.

## Estado atual
- Refactor de inicio de rodada/performance ainda em andamento.
- Build validado sem erros de compilacao (`Assembly-CSharp`).

## Git
- Tag: `v1.3.5`
- Branch: `main`
