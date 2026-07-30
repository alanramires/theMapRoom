# v5.0.5 — Plano de Otimização do Tabuleiro, Melhor X e Jogabilidade 5/8

## Visão geral

Esta versão conclui a quinta parte do plano de otimização com a implementação do
`MovementReachCache`.

As duas ondas históricas de movimento continuam separadas:

- `UnitMovementPathRules.CalcularCaminhosValidos`;
- `UnitMovementPathRules.CalculateMovementCostMap`.

Não houve tentativa de unificar as árvores. Cada rotina preserva sua semântica
de custo, autonomia, estrada, ocupação e reconstrução de caminho, mas uma
consulta idêntica no mesmo snapshot confirmado deixa de executar novamente a
BFS.

## Chave de consulta

A chave distingue:

- tipo da onda;
- mapa e banco de terrenos;
- unidade runtime e `InstanceId`;
- origem;
- orçamento da busca;
- combustível atual;
- perfil de movimento;
- domínio, altura e modos adicionais;
- skills e perfil de autonomia;
- time, slot e estado embarcado;
- modo de ocupação por camada e Total War;
- revisão confirmada de ocupação;
- versão e fingerprint da topologia.

Alterar qualquer componente relevante produz uma chave diferente. Uma rota de
um Soldado não pode ser reutilizada por um Trem, uma aeronave em `AirHigh` não
recebe o resultado de `AirLow`, e uma busca Operational não se confunde com o
alcance Tactical.

## Resultados isolados

As entradas armazenadas não são entregues diretamente aos consumidores.

No primeiro cálculo:

1. a rotina constrói seu resultado normal;
2. o cache grava uma cópia;
3. o chamador conserva o dicionário original.

Em um cache hit, o chamador recebe outra cópia. Isso preserva consumidores
legados que acrescentam origem, removem candidatos ou modificam listas de
caminho. Essas alterações continuam locais e não contaminam consultas
posteriores.

## Limite de memória

O cache usa descarte por uso recente com dois limites simultâneos:

- no máximo 96 entradas;
- no máximo 120.000 referências de células.

Mapas de caminho contabilizam tanto destinos quanto as células contidas em cada
rota. Mapas de custo contabilizam suas células. Uma entrada individual maior
que o limite não é armazenada.

Assim, uma busca Strategic excepcionalmente grande não pode expulsar o runtime
para um crescimento de memória sem limite.

## Contrato transacional

O cache só aceita uma consulta quando:

- o jogo está em execução;
- o `BoardTopologyIndex` está pronto;
- o `ConfirmedOccupancyIndex` está pronto;
- não existem alterações de ocupação pendentes;
- a célula, camada, slot, time e embarque runtime da unidade ainda correspondem
  ao registro confirmado.

Durante movimento provisório, animação, embarque em preparação, load incompleto
ou rollback pendente, a consulta ignora o cache e executa o caminho histórico.
O resultado provisório também não é publicado.

Cancelar uma ação não invalida o snapshot anterior. Depois de uma ação
comprometida e do retorno a `CursorState.Neutral`, a ocupação é reconciliada,
sua revisão avança e as entradas daquele mapa são removidas.

Trocas de configuração de slots também avançam o contexto confirmado. O registro
de ocupação passou a incluir `TeamId`, além do slot, porque a relação
aliado/inimigo interfere na travessia.

Nenhum resultado de movimento:

- atualiza FOW;
- revela informação;
- altera posição;
- consome combustível ou movimento;
- reserva destino;
- marca `HasActed`;
- sobrevive como autoridade após uma revisão incompatível.

## Consultas físicas e ocupação

Nos misses confirmados, o cache local da onda passou a reutilizar:

- unidades do `ConfirmedOccupancyIndex`;
- terreno, estruturas e segmentos declarados do `BoardTopologyIndex`;
- construções registradas em `ConstructionManager.AllActive`.

Isso retira `FindObjectsByType` do caminho normal de uma onda confirmada.

O fallback histórico permanece ativo quando o índice confirmado não pode servir
consultas, como em cenas auxiliares, bootstrap incompleto ou estado provisório.
Nessa situação a prioridade é preservar a legalidade e o comportamento
existente, não publicar um cache incerto.

## Invalidação

As entradas são removidas:

- quando a revisão confirmada de ocupação muda;
- quando a configuração de slots muda e é reconciliada;
- quando o índice de ocupação do mapa é desativado;
- por invalidação global explícita dos sensores;
- por limite LRU.

Mudanças de combustível, orçamento, camada ou perfil da própria unidade não
exigem limpar todo o mapa porque já fazem parte da chave.

## Instrumentação

Foram acrescentados os contadores:

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

`MovementWavesBuilt` agora representa somente BFS realmente construída. Um hit
não aumenta esse contador.

## Arquivos principais

- `Assets/Scripts/Units/Rules/MovementReachCache.cs`;
- `Assets/Scripts/Units/Rules/UnitMovementPathRules.cs`;
- `Assets/Scripts/Hex/Core/ConfirmedOccupancyIndex.cs`;
- `Assets/Scripts/Match/ThreatRevisionTracker.cs`.

## Validação técnica

- `Assembly-CSharp.csproj`: compilação concluída com 0 erros;
- `Assembly-CSharp-Editor.csproj`: compilação concluída com 0 erros;
- 258 avisos no runtime e 417 no Editor permanecem preexistentes;
- nenhum aviso novo foi emitido pelo `MovementReachCache`;
- as duas rotinas continuam com implementações independentes;
- resultados de hit são cópias mutáveis locais;
- cache é recusado quando o snapshot confirmado não está disponível;
- invalidação ocorre pela revisão confirmada, não pelo fim de animação;
- nenhuma cena, prefab ou ficha de unidade foi alterada por esta parte.

## Validação em partida

O primeiro teste recomendado é repetir uma ferramenta que solicite a mesma onda
duas vezes sem alterar o tabuleiro. A segunda chamada deve registrar:

```text
MovementCacheHits > 0
MovementWavesBuilt sem incremento equivalente
```

Em seguida:

1. abrir e cancelar um movimento: o snapshot confirmado deve continuar
   reutilizável;
2. comprometer um movimento: as entradas do mapa devem ser invalidadas somente
   no retorno a `Neutral`;
3. repetir com Trem, unidade aérea, unidade naval e infantaria;
4. testar embarque, desembarque, morte, spawn e troca de camada;
5. comparar destinos e rankings com a versão anterior.

## Próxima etapa

A Parte 6 deve completar o `TransportPlanningSnapshot`.

O alcance compartilhado criado nesta versão poderá ser reutilizado para reunir,
uma única vez por transportador e snapshot confirmado:

- alcance do transportador;
- passageiros compatíveis;
- alcance atual e futuro dos passageiros;
- respostas de `QueroCarona`;
- candidatos de LZ;
- opções Tactical, Operational e Strategic.

EVAC, Pickup, Supply e Assault deverão filtrar esse resultado sem reconstruir
as mesmas avaliações.
