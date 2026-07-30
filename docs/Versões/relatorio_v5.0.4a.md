# v5.0.4a — Plano de Otimização do Tabuleiro, Melhor X e Jogabilidade 4a/8

## Visão geral

Esta versão complementa a Parte 4 com as correções orientadas pelas primeiras
partidas de carga realizadas após a criação da topologia e da ocupação
confirmada.

O objetivo deste marco não é iniciar a Parte 5. Ele fecha os custos e regressões
descobertos ao colocar uma IA rebelde com 58 unidades em um mapa grande:

- decisões aéreas varrendo setores que deveriam servir apenas como direção;
- `MelhorEmbarque` calculando rotas antes de eliminar combinações incompatíveis;
- progressão estratégica reconstruindo distâncias exatas onde distância cúbica
  já era suficiente;
- fogo indireto solicitando mapas amplos fora do setor Tactical;
- desembarque provocando atualização FOW maior que o delta comprometido;
- espera fixa entre unidades ampliando artificialmente turnos grandes;
- primeiro turno da IA construindo todo o cache FOW somente após `Passar turno`;
- pré-aquecimento FOW liberando a jogabilidade antes de terminar;
- vídeo do `PanelRodada` reproduzido aos solavancos enquanto o main thread
  pré-calculava as fontes.

O resultado medido no mesmo cenário de estresse foi a redução do turno de
**3 minutos e 43 segundos para 2 minutos e 05 segundos**: economia de
**1 minuto e 38 segundos**, aproximadamente **44%**.

## Política espacial da IA

A política consolidada neste marco separa precisão local de orientação global:

- **Tactical** é a zona de decisão detalhada;
- **Operational** indica a direção do objetivo intermediário;
- **Strategic** indica a direção geral do teatro;
- setores fora de Tactical usam, sempre que a regra permitir, distância cúbica
  até o objetivo mais próximo;
- mapas de rota exata ficam reservados a células que podem materialmente afetar
  a ação presente.

Essa separação evita tratar todo o tabuleiro como uma única zona tática sem
remover a capacidade da IA de avançar rumo a objetivos distantes.

## Combate aéreo

Apache, Super Tucano e demais aeronaves de combate deixam de usar uma varredura
detalhada de ataque sobre o tabuleiro inteiro.

O fluxo passa a:

1. obter as células do setor Tactical da aeronave;
2. eliminar por distância cúbica as células que não podem produzir candidato
   local;
3. consultar sensores de ataque somente para candidatos táticos;
4. quando não existe alvo tático, usar Operational ou Strategic apenas como
   direção;
5. avançar pela construção ou referência conhecida mais próxima, sem uma
   segunda passagem ofensiva global.

Na rodada de validação, sete varreduras aéreas visitaram 696 células táticas em
apenas **0,9 ms** no total. Unidades sem candidato local registraram zero
chamadas de sensor ofensivo.

## Cache compartilhado de planejamento de transporte

As decisões de transporte passaram a compartilhar o snapshot preparado para a
mesma unidade e revisão confirmada.

Evac, Pickup, Supply, Assault e progressões relacionadas deixam de reconstruir
independentemente as mesmas listas básicas durante uma única decisão. O cache
é reutilizado enquanto unidade, tabuleiro, ocupação confirmada e contexto
permanecem equivalentes.

O cache continua sendo apenas material de consulta:

- não reserva passageiro;
- não reserva vaga;
- não compromete LZ;
- não altera posição;
- não sobrevive a uma revisão incompatível;
- sensores oficiais continuam validando a ação no momento de materializá-la.

## Compatibilidade antes da rota em MelhorEmbarque

`MelhorEmbarqueService` agora resolve passageiros e vagas compatíveis antes de
solicitar topologia, alcance ou mapas de movimento do transportador.

Se não existe passageiro compatível:

- a avaliação termina imediatamente;
- nenhuma rota do transportador é construída;
- nenhum candidato de LZ é classificado;
- a telemetria registra `MelhorEmbarqueCompatibilityEarlyOuts`.

No fluxo iniciado pelo passageiro, transportadores incompatíveis também são
rejeitados antes de chamar a avaliação completa. Esses descartes são medidos
por `MelhorEmbarqueCompatibilityRejects`.

Isso corrige o caso observado com o Lança-Foguetes: transportadores adjacentes
destinados apenas a infantaria não justificam uma varredura ou um mapa de rota
para uma carga incompatível.

## Progressão Tactical e direção cúbica

O seletor de progressão das ferramentas passa a distinguir alvo alcançável no
turno de alvo meramente direcional.

Quando o objetivo está dentro do alcance material:

- a IA pode construir uma rota exata limitada;
- a pontuação considera progresso real ao longo do caminho;
- a telemetria registra `ToolProgressionTacticalRouteUses`.

Quando o objetivo está fora desse alcance:

- a progressão usa distância cúbica;
- Operational e Strategic continuam orientando a unidade;
- não é criado um mapa de distância reverso para o tabuleiro inteiro;
- a telemetria registra `ToolProgressionCubicDirectionUses`.

Os mapas reversos que ainda forem necessários recebem um custo máximo
defensivo, derivado do movimento atual e da mobilidade da unidade.

## Fogo indireto

O reposicionamento de fogo indireto não constrói mais um mapa oculto com custo
160 até uma âncora fora do setor Tactical.

Dentro de Tactical, a unidade conserva a avaliação exata necessária para
escolher posição de tiro. Fora dele, Operational e Strategic fornecem direção
cúbica até a aproximação entrar na zona tática.

Assim, Lança-Foguetes e outras unidades de apoio deixam de percorrer o mapa
inteiro apenas para decidir a direção desta rodada.

## Desembarque incremental

Um desembarque comprometido agora informa explicitamente as unidades alteradas:

- transportador;
- passageiros efetivamente desembarcados.

O FOW recebe um `CommittedBoardDelta` multiunidade enumerado e atualiza somente
essas fontes depois do retorno a `CursorState.Neutral`.

O fato de a ação envolver várias unidades não exige, por si só, um refresh
completo. Full refresh permanece reservado a mutações que não possam descrever
suas fontes afetadas.

## Ritmo adaptativo da Fase 2

A pausa entre decisões da IA deixa de adicionar 0,5 segundo para cada unidade
independentemente do tamanho do exército.

Exércitos pequenos preservam o ritmo visual anterior. Acima de 12 unidades, a
espera é escalada para manter aproximadamente seis segundos de respiro total,
com piso defensivo de 0,08 segundo por unidade.

Em um exército de 58 unidades, a espera nominal cai para aproximadamente
0,103 segundo por decisão. A apresentação continua legível sem acrescentar
quase meio minuto de atraso artificial ao turno.

## Cache FOW pré-cozido por slot

O log revelou que os oito segundos entre `Passar turno` e o início da IA não
pertenciam ao planejamento: eram a primeira construção das contribuições FOW
das 58 unidades do slot AI.

O cache por observador já existia e já fazia parte de save/load, mas o primeiro
snapshot do slot inativo só era produzido quando esse slot se tornava ativo.

Agora, durante um turno humano confirmado:

- slots AI inativos são pré-aquecidos;
- uma fonte é processada por frame;
- o processo pausa fora de `CursorState.Neutral`;
- o contexto usa `publishGameplayData=false`;
- nenhum visual é publicado;
- nenhuma memória de exploração é gravada;
- nenhuma inteligência da IA é registrada;
- o runtime visual e geográfico do humano é restaurado antes de devolver o
  frame;
- uma troca de turno antecipada pode interromper o aquecimento e conservar o
  subconjunto já válido;
- o reconciliador do início do turno completa somente fontes ausentes ou
  realmente alteradas.

Na validação, 58 fontes foram pré-cozidas em 8,14 segundos. Ao entrar no turno
AI, todas apareceram como `units.unchanged=58`, eliminando o fallback frio de
aproximadamente oito segundos.

## Loading como barreira de prontidão

Distribuir o pré-cálculo por frames eliminou a parede única na troca de turno,
mas tornou o vídeo do `PanelRodada` visivelmente irregular. A solução adotada
não transforma o painel em autoridade lógica.

Em partidas com FOW:

1. a partida mostra `Carregando turno do jogador`;
2. o vídeo de time ainda não é iniciado;
3. input e botão permanecem bloqueados;
4. o cache FOW dos slots AI é pré-cozido atrás da tela estática;
5. `IsTurnBoardReady` permanece falso enquanto o aquecimento existir;
6. somente depois da prontidão o vídeo começa;
7. somente depois da prontidão o botão `Iniciar turno` é habilitado.

Em presets sem FOW, não existe pré-cozimento e essa espera adicional é
ignorada.

O mesmo contrato foi aplicado ao load. Saves atuais restauram os caches por
observador; saves antigos ou incompletos podem complementar fontes ausentes
ainda atrás do loading.

Se o `PanelRodada` for desativado por debug, a partida e a IA continuam
funcionando. A barreira visual não é requisito para `AdvanceTurn`, FOW ou
`RunAITurn`.

## Save e load

As contribuições FOW continuam persistidas por slot observador. O load valida
configuração, fontes, checksums e estado das unidades antes de ativá-las.

Este complemento acrescenta a preparação de caches ausentes antes da
apresentação do botão humano. O cache derivado nunca substitui unidades,
construções ou tabuleiro como verdade do save.

## Telemetria da rodada de validação

O cenário principal continha 58 unidades AI, incluindo representantes aéreos,
navais, terrestres, transportadores e fogo indireto.

Resultados:

- linha de base observada: **3m43s**;
- rodada após as correções: **2m05s**;
- redução absoluta: **1m38s**;
- redução relativa: **aproximadamente 44%**;
- cache FOW inicial: 58 fontes em 8,14s atrás do loading;
- barreira de planejamento FOW reutilizada: aproximadamente 149ms;
- combate aéreo tático: 0,9ms em sete chamadas;
- Fase 2 concluída: 58 decisões.

Os próximos custos dominantes são:

- `transportPlanning`: 22,43s em 16 chamadas;
- `melhorEmbarque`: 21,70s em 16 chamadas;
- `routeDistance`: 13,65s em 1.414 chamadas;
- `validPaths`: 7,67s em 413 chamadas;
- Lança-Foguetes mais lento: 8,98s na decisão completa.

Os estágios são aninhados e não devem ser somados como tempos independentes.
Eles indicam onde a Parte 5 deve concentrar cache e redução de mapas.

## Custos conhecidos fora deste complemento

O primeiro frame da cena ainda registrou aproximadamente 9,3 segundos durante
bootstrap de cena, alocação e fallback runtime de `BoardTopology`. Esse custo
acontece antes do aquecimento FOW distribuído e permanece como item separado de
inicialização.

O pré-cozimento FOW também continua custando CPU no primeiro mapa grande. A
mudança deste marco garante que esse trabalho aconteça atrás de loading estático
e não durante o vídeo ou a jogabilidade.

## Contrato transacional

Todas as otimizações preservam a lei fundamental das ações:

- caches confirmados só são publicados em estado confirmado;
- aquecimento FOW só usa unidades inativas do observador e estado `Neutral`;
- movimento provisório não alimenta visão, inteligência ou ocupação definitiva;
- desembarque só publica seu delta depois do compromisso;
- setores e distâncias classificam opções, mas não comprometem ações;
- sensores oficiais continuam responsáveis pela legalidade final;
- cancelamento retorna a `Neutral` sem deixar estado provisório nos caches.

## Conteúdo incluído no marco

Como este marco é fechado com `git add .`, ele registra também o estado atual
da cena `Hot Seat 1 - Pvp`, usada para o teste de carga com dezenas de unidades.
As alterações de conteúdo da cena integram o snapshot validado desta versão.

## Validação técnica

- `Assembly-CSharp.csproj`: compilação concluída com 0 erros;
- warnings preexistentes de APIs obsoletas e serialização permanecem;
- `git diff --check` aprovado para os arquivos-fonte e relatório desta versão;
- cache FOW não publica gameplay, memória, intel ou visual durante warmup;
- runtime do observador humano é restaurado antes de cada `yield`;
- botão humano observa a prontidão e não controla a lógica do turno;
- presets sem FOW não executam o pré-cozimento;
- teste de carga concluiu as 58 decisões da Fase 2.

## Próxima etapa

A Parte 5 deve atacar o bloco agora dominante de transporte e rotas:

- compartilhar resultados de `MelhorEmbarque` entre Evac, Pickup, Supply e
  Assault;
- reduzir as 1.414 consultas de `routeDistance`;
- evitar 413 reconstruções de `validPaths`;
- materializar mapas somente para candidatos Tactical;
- manter Operational e Strategic como direção cúbica;
- investigar o Lança-Foguetes de 8,98s sem reintroduzir varredura global.

O objetivo permanece o mesmo: decisões locais precisas, direção global barata e
nenhuma segunda verdade concorrente com o tabuleiro confirmado.
