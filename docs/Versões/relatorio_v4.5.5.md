# v4.5.5 — Movement Reach Cache

## Visão geral

Este checkpoint implementa o cache compartilhado das duas ondas de movimento
do tabuleiro:

- `UnitMovementPathRules.CalcularCaminhosValidos`;
- `UnitMovementPathRules.CalculateMovementCostMap`.

As rotinas continuam independentes e preservam suas regras históricas. Uma
consulta idêntica no mesmo snapshot confirmado, porém, deixa de reconstruir a
BFS e recebe uma cópia do resultado armazenado.

## Movement Reach Cache

O novo `MovementReachCache` diferencia consultas por:

- mapa e banco de terrenos;
- tipo da onda;
- unidade, origem e orçamento;
- combustível e perfil de movimento;
- domínio, altura, modos adicionais e skills;
- time, slot e embarque;
- configuração de ocupação e Total War;
- revisão confirmada da ocupação;
- versão e fingerprint da topologia.

O cache possui limite de 96 entradas e peso máximo de 120.000 referências de
células. O descarte usa ordem de uso recente. Uma entrada individual maior que
o teto não é armazenada.

## Isolamento dos resultados

O resultado armazenado nunca é entregue diretamente.

- no miss, o cache grava uma cópia e o consumidor conserva o resultado original;
- no hit, o consumidor recebe uma nova cópia;
- alterações locais em dicionários ou listas de caminho não modificam o cache.

Isso preserva os consumidores existentes que ajustam a coleção recebida durante
suas avaliações.

## Topologia e ocupação

Uma onda confirmada passa a consultar:

- terreno, estruturas e segmentos do `BoardTopologyIndex`;
- unidades do `ConfirmedOccupancyIndex`;
- construções registradas em `ConstructionManager.AllActive`.

O caminho normal deixa de executar `FindObjectsByType` a cada nova onda.

Quando a topologia ou a ocupação ainda não pode servir consultas, o runtime usa
o comportamento histórico. Esse fallback cobre bootstrap, cenas auxiliares,
load incompleto e ações provisórias.

## Invalidação transacional

O cache só pode ser lido ou publicado quando a unidade runtime coincide com seu
registro confirmado e o índice não possui alterações pendentes.

Movimento provisório, animação, rollback e cancelamento não substituem o
snapshot confirmado. Depois de uma ação comprometida e do retorno a
`CursorState.Neutral`, a ocupação é reconciliada e as entradas daquele mapa são
invalidadas.

O registro confirmado também passou a conter `TeamId`. Assim, troca de time ou
configuração de slot não pode reutilizar um caminho calculado sob uma relação
aliado/inimigo anterior.

## Telemetria

Os novos contadores incluem:

```text
MovementCacheHits
ValidPathCacheHits
MovementCostCacheHits
MovementCacheMisses
MovementCacheBypasses
MovementCacheStores
MovementCacheEvictions
MovementCacheOversizedSkips
MovementCacheInvalidatedEntries
MovementQueryConfirmedOccupancyUses
MovementQueryLiveOccupancyFallbacks
```

`MovementWavesBuilt` passa a contar somente BFS realmente executadas.

## FOW e filas de início de turno

O checkpoint também registra as correções de início de turno que já estavam no
worktree:

- mortes por falta de combustível são enfileiradas antes do FOW, mas só começam
  depois que o snapshot visual do observador correto foi publicado;
- filas de rally seguem a mesma barreira;
- pouso de emergência marca a visão como suja;
- o refresh definitivo acontece uma única vez depois que a fila termina e o
  cursor retorna a `Neutral`.

Isso evita que a apresentação permaneça presa no observador anterior e mantém o
refresh de FOW fora dos estados provisórios da fila.

## Arquivos principais

- `Assets/Scripts/Units/Rules/MovementReachCache.cs`;
- `Assets/Scripts/Units/Rules/UnitMovementPathRules.cs`;
- `Assets/Scripts/Hex/Core/ConfirmedOccupancyIndex.cs`;
- `Assets/Scripts/Match/ThreatRevisionTracker.cs`;
- `Assets/Scripts/Match/MatchController.cs`;
- `Assets/Scripts/Match/TurnStateManager.cs`;
- `docs/relatorio_v5.0.5.md`.

## Validação

- `Assembly-CSharp.csproj`: 0 erros;
- `Assembly-CSharp-Editor.csproj`: 0 erros;
- `git diff --check`: aprovado;
- resultados armazenados permanecem isolados dos consumidores;
- cache é recusado fora de um snapshot confirmado coerente;
- invalidação de movimento continua ocorrendo depois do compromisso e retorno a
  `Neutral`;
- FOW de pouso emergencial é atualizado somente depois da fila.

Os avisos de APIs obsoletas e serialização já existentes permanecem sem relação
com este checkpoint.

## Teste recomendado

Em uma rodada grande, repetir consultas equivalentes deve produzir
`MovementCacheHits` sem incremento correspondente em `MovementWavesBuilt`.

Também devem ser exercitados:

- cancelamento de movimento;
- movimento comprometido;
- trem e segmentos ferroviários;
- aeronaves em camadas diferentes;
- embarque e desembarque;
- spawn e morte;
- pouso emergencial no início do turno;
- mudança de observador humano/IA sob FOW.
