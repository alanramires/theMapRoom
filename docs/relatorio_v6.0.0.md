# Board baking e loading optimization

## Versão

`v6.0.0`

## Objetivo

Consolidar um ponto de restauração após a primeira rodada ampla de otimização
do tabuleiro, do carregamento de partidas com Fog of War e da preparação do
turno da IA em mapas grandes.

O princípio deste checkpoint é:

> Dados estáticos ou já confirmados devem ser cozidos, restaurados ou
> reutilizados. O jogo não deve reconstruir o mesmo tabuleiro várias vezes
> antes de permitir que a partida continue.

## Cache de Fog of War no save/load

O load passou a tratar o snapshot salvo do FoW como fonte de verdade:

- restaura separadamente o cache de cada observador;
- preserva contribuições de unidades e construções;
- impede `ApplyActiveTeam` de construir um FoW frio e provisório durante o
  carregamento;
- cancela warmups antigos quando um load começa;
- identifica cada warmup por geração, impedindo que uma coroutine anterior
  retome depois que a supressão for liberada;
- mantém fallback frio para saves antigos, incompletos ou incompatíveis.

Nos testes com 61 unidades da IA, a barreira pós-restauração passou a reutilizar
as 61 fontes, sem recolher células novamente.

## Warmup e barreiras de apresentação

O warmup de FoW não pode competir com o load nem atravessar a restauração do
snapshot. O painel de carregamento e a cortina da IA permanecem responsáveis
por ocultar a preparação real do tabuleiro.

No turno da IA, o beep de conclusão deixou de significar apenas “arquivo
lido”. Ele é emitido depois da preparação da iniciativa, quando a IA está
realmente pronta para aguardar `AI STEP`, continuar automaticamente ou
apresentar sua primeira ação.

## Board baking e geometria hexagonal

A vizinhança imediata dos hexes foi memoizada por Tilemap e célula:

- elimina chamadas repetidas de `GetCellCenterWorld`;
- evita recriar e ordenar listas para a mesma geometria;
- reutiliza os seis vizinhos em movimento, LoS, mapas de custo e progressão;
- invalida o cache quando a cena é descarregada;
- mantém uma saída explícita para alterações de layout em runtime.

O checkpoint inclui o estado serializado atual do mapa de teste e dos assets
Unity associados ao cozimento e à validação visual.

## Reconstrução de setores

O `SectorManager` agora associa sua fotografia à revisão confirmada do
tabuleiro:

- requisições concorrentes da mesma revisão são consolidadas;
- as construções restauradas são processadas ainda sob o painel de load;
- `after-load-success` não refaz uma fotografia já pronta;
- `CommitAIWorldHeavy` reutiliza a reconstrução do load;
- a telemetria informa motivo, revisão, setores, bases e tempo total.

Isso elimina duas passadas consecutivas e idênticas de aproximadamente 2,5
segundos cada no cenário de teste.

## Preparação da iniciativa da IA

A ordenação da Fase 2 deixou de executar sensores e consultas caras dentro do
comparador do `Sort`.

As chaves são calculadas uma vez por unidade:

- distância ao objetivo designado;
- grupo de iniciativa;
- bloqueio de captura;
- ataque de fogo de suporte disponível;
- oportunidade de combate;
- prioridade do objetivo;
- distância ao transportador;
- elite, reparo e iniciativa efetiva.

A política tática foi preservada. Apenas a forma de obter os valores mudou.
No teste com 59 unidades, a preparação caiu de aproximadamente 6,18 segundos
para 1,15 segundo, enquanto o `Sort` isolado passou a aproximadamente 1,2 ms.

A linha `[AI Perf][InitiativeSetup]` separa:

- unidades disponíveis;
- snapshot;
- reparos;
- grupos;
- fatos congelados;
- ordenação;
- emissão do log.

## Melhor LZ de Embarque

O planejamento runtime do transportador recebeu encerramento tático
antecipado:

- células candidatas são visitadas por proximidade;
- caminhos táticos reais são avaliados primeiro;
- ao encontrar passageiro `Requested + ReachableNow`, sem emergência
pendente, a busca não varre desnecessariamente Operational e Strategic;
- a ferramenta de estudo continua podendo produzir o ranking completo.

Assim, o runtime e a ferramenta preservam a mesma fonte de verdade, mas o
batch pode interromper uma consulta quando sua decisão já é conclusiva.

## Contrato transacional

As otimizações não transformam cálculos provisórios em estado definitivo.

- FoW salvo só é publicado depois de validado;
- warmups não alteram o tabuleiro confirmado;
- caches de geometria contêm apenas relações estáticas;
- snapshots de setores e iniciativa são somente leitura;
- decisões de transporte continuam sendo comprometidas pelo fluxo normal;
- cancelamento e rollback permanecem capazes de retornar a
  `CursorState.Neutral` sem resíduos.

## Validação e métricas observadas

- `Assembly-CSharp.csproj`: compilado com 0 erros;
- warmup concorrente observado: de aproximadamente 42 s para 0;
- reconstrução fria de FoW durante o load: suprimida;
- barreira de FoW restaurada: 61 unidades reutilizadas em aproximadamente
  250–304 ms;
- preparação de iniciativa: de aproximadamente 6,18 s para 1,15 s;
- `Sort` da iniciativa: aproximadamente 1,2 ms;
- reconstruções duplicadas de setor: consolidadas por revisão;
- beep da IA: movido para a barreira real de preparação pós-load.

## Conteúdo do checkpoint

Este ponto inclui todo o worktree solicitado por `git add .`, inclusive:

- scripts de cache, load, FoW, setores, iniciativa e transporte;
- cache geométrico dos caminhos válidos;
- prefab de unidade;
- cena `Hot Seat 1 - Pvp`;
- assets de fontes modificados pelo Unity;
- demais dados serializados presentes no workspace no momento do checkpoint.

