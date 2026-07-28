# v5.0.0 — Plano de Otimização do Tabuleiro, Melhor X e Jogabilidade

## Visão geral

Esta versão registra o plano técnico para retirar reconstruções repetidas de
mapa das ferramentas da família **Melhor X**, reduzir o custo das decisões da
IA e preservar integralmente as regras de jogabilidade, informação e
comprometimento do The Map Room.

O diagnóstico encontrou uma separação arquitetural que o runtime ainda não
aproveita: o mundo físico da partida é permanente, enquanto as unidades e os
estados operacionais mudam. Terrenos, praias, mares, montanhas, construções,
estruturas, pontes, estradas e trilhos não são destruídos nem construídos
durante uma partida. Construções permanecem na mesma célula e apenas alteram
dono, captura, estoque, atendimento e mercado. Unidades são os únicos
elementos que aparecem, se deslocam, embarcam, mudam de camada ou são
destruídos.

Hoje parte das consultas trata essas duas categorias como se fossem igualmente
voláteis. Em consequência, uma ferramenta pode voltar a percorrer o mapa para
responder onde existe uma praia, pista ou local de encontro mesmo que essa
geografia já tenha sido descoberta por uma consulta anterior.

O objetivo do ciclo v5 é fazer o jogo consultar uma representação permanente
do tabuleiro e recalcular somente aquilo que realmente mudou.

## Diagnóstico das reconstruções

As consultas atuais são puras e preservam o estado do jogo, mas frequentemente
criam seus dados de trabalho do zero:

- `MelhorPouso` calcula caminhos da aeronave e percorre
  `cellBounds.allPositionsWithin` procurando superfícies e plataformas;
- `MelhorEmbarque` calcula o alcance do transportador, percorre o mapa atrás de
  LZs e cria dois mapas de alcance para cada passageiro elegível;
- `QueroCarona` cria um mapa operacional de custos para responder se a unidade
  alcança o próprio objetivo;
- `QueroCaronaAerea` chama tanto a avaliação terrestre de emergência quanto
  `MelhorPouso`;
- `MelhorEstoque` cria caminhos táticos e outro mapa de custos operacional;
- `MelhorDesembarque` compartilha rotas reversas apenas dentro de uma chamada;
  o cache local desaparece quando a avaliação termina;
- `TransportOperationsService` pode repetir a avaliação de embarque para EVAC
  e Pickup nos níveis Tactical, Operational e Strategic.

O `MovementQueryCache` usado pelo pathfinding também é local a uma única onda.
Ele evita consultas duplicadas enquanto aquela BFS está viva, mas uma nova
chamada volta a coletar tilemaps, unidades, construções e redes viárias.

Portanto, o problema não é somente redescobrir a praia na rodada seguinte.
Dependendo da sequência de tentativas, a mesma geografia e os mesmos alcances
podem ser reconstruídos várias vezes durante a decisão de uma única unidade.

## Princípios que limitam a otimização

### O mapa físico é permanente

Podem ser indexados uma vez por mapa:

- terreno e domínio físico de cada célula;
- estrutura e combinação estrutura+terreno;
- construção, tipo e posição;
- vizinhança hexagonal;
- segmentos ferroviários realmente declarados por rota;
- praias, costas, pistas potenciais e superfícies de pouso;
- células estruturalmente possíveis para embarque e desembarque;
- permissões físicas que não dependem de uma unidade presente.

Essa informação não precisa de revisão por turno ou por movimento.

### A partida continua dinâmica

Permanecem dinâmicos:

- unidades por célula e por andar;
- domínio e altura atuais;
- combustível, movimento, HP e munição;
- passageiros, vagas e exclusividade de slots;
- dono, captura, estoque e capacidade de atendimento das construções;
- FOW, detecção, contatos e conhecimento confirmado;
- planos, objetivos, reservas e prioridades da IA.

Esses dados não devem contaminar o índice permanente.

### Cache não é autoridade de jogabilidade

Sensores continuam sendo a fonte de verdade para legalidade. Um índice pode
responder rapidamente quais praias são candidatas, mas `PodePousar`,
`PodeEmbarcar`, `PodeDesembarcar` e os demais sensores continuam responsáveis
pela validação final de skills, camadas, ocupação, combustível, vagas e regras
da ficha.

O mesmo vale para informação. O índice conhece a verdade física do mapa, mas
menus, previews e IA só podem usar o conhecimento confirmado permitido pelo
FOW. Cachear uma praia não autoriza revelá-la ao jogador nem usar uma unidade
oculta como filtro de menu.

### Toda invalidação é transacional

Nada entre a saída e o retorno a `CursorState.Neutral` altera índices ou
revisões confirmadas. Movimento provisório, pouso temporário, abertura de
sensor, animação e cancelamento não invalidam o cache confirmado.

Somente uma ação explicitamente comprometida pode atualizar ocupação, unidade,
estoque ou decisão. Depois do compromisso, o fluxo retorna a `Neutral` e então
os índices dinâmicos refletem o novo snapshot confirmado.

## Camada 1 — índice permanente do mapa

Será criado um `BoardTopologyIndex`, preparado no Editor e serializado com a
cena ou com um asset próprio do mapa.

O formato serializado usa listas e registros estáveis. No runtime, essas listas
são convertidas uma única vez em dicionários e conjuntos para consulta rápida.
O índice oferece, no mínimo:

```text
BoardTopologyIndex
├── célula -> terreno
├── célula -> estrutura
├── célula -> construção
├── célula -> contexto efetivo por prioridade
├── célula -> seis vizinhos
├── aresta -> segmento ferroviário declarado
├── praias e células costeiras
├── pistas e superfícies potenciais
├── células potenciais de embarque
└── células potenciais de desembarque
```

Mapas antigos ou cenas de desenvolvimento recebem um fallback que constrói o
índice uma vez ao carregar e emite diagnóstico. O fallback não pode virar o
caminho normal das ferramentas.

Uma ferramenta de validação do Editor deve detectar:

- índice ausente ou desatualizado;
- tile sem entrada no banco de terrenos;
- rota ferroviária com salto entre células não adjacentes;
- célula indexada fora do tilemap correspondente;
- fingerprint diferente do conteúdo atual da cena.

## Camada 2 — ocupação confirmada

Será criado um `ConfirmedOccupancyIndex`, separado da topologia, contendo:

- unidades ativas por célula;
- ocupante por andar operacional;
- transportadores e passageiros;
- plataformas com vagas disponíveis;
- unidades logísticas por perfil;
- fornecedores, Hubs e Receivers móveis;
- revisão confirmada de ocupação.

O índice deve ser atualizado incrementalmente após:

- movimento comprometido;
- spawn ou compra;
- morte e remoção;
- embarque e desembarque;
- mudança confirmada de camada;
- troca de slot ou time da unidade.

Captura ou mudança de estoque de construção não reconstrói ocupação nem
topologia. Cada subsistema invalida apenas o tipo de resposta afetado.

## Camada 3 — cache de movimento

Um `MovementReachCache` compartilhará os resultados de:

- `UnitMovementPathRules.CalcularCaminhosValidos`;
- `UnitMovementPathRules.CalculateMovementCostMap`;
- mapas reversos usados pelo desembarque e pela progressão.

Uma chave de consulta deve incluir:

```text
mapa
unidade e perfil de movimento
origem
domínio e altura
combustível
orçamento da busca
revisão confirmada de ocupação
versão do índice topológico
```

Os resultados publicados pelo cache serão tratados como imutáveis. Consumidores
que precisarem acrescentar ou remover entradas deverão criar uma visão local,
sem modificar o valor compartilhado.

O primeiro passo é cachear separadamente as duas rotinas existentes. A
possibilidade de produzir caminhos táticos e custos operacionais a partir de
uma única árvore será avaliada depois, pois as duas implementações atuais não
têm semântica idêntica para autonomia, estados de caminho e bônus de estrada.
Unificar sem provar equivalência poderia alterar a jogabilidade.

O cache terá limite de memória e descarte previsível, preferencialmente por
snapshot e por uso recente. Uma mudança confirmada de ocupação pode invalidar
alcances dependentes de bloqueadores sem tocar no índice físico do mapa.

## Camada 4 — snapshot de planejamento de transporte

As tentativas EVAC, Pickup, Tactical, Operational e Strategic não devem
reexecutar `MelhorEmbarque`. Será produzido um único
`TransportPlanningSnapshot` por transportador e snapshot confirmado:

```text
TransportPlanningSnapshot
├── alcance do transportador
├── LZs estruturalmente possíveis
├── passageiros estruturalmente compatíveis
├── alcance atual e futuro de cada passageiro
├── QueroCarona de cada passageiro
├── opções validadas pelos sensores
└── ranking classificado por tier e disposição
```

EVAC passa a filtrar emergências desse resultado. Pickup filtra pedidos
normais. Tactical, Operational e Strategic selecionam o tier correspondente.
Uma rejeição por segurança ou materialização continua procurando a próxima
opção sem reconstruir a malha.

## Plano por ferramenta

### Melhor Estoque

- consultar diretamente construções e unidades logísticas indexadas;
- reutilizar o alcance tático e operacional do ator;
- manter estoque, urgência, compatibilidade e quantidade útil como filtros
  dinâmicos;
- continuar validando a transferência prospectiva pelo `PodeTransferir`;
- invalidar ranking por mudanças relevantes de estoque, dono ou unidade, sem
  reconstruir a geografia.

### Melhor Local de Pouso

- substituir a varredura de `cellBounds` pela lista de superfícies potenciais;
- acrescentar plataformas móveis a partir do índice confirmado de unidades;
- reutilizar o alcance da aeronave;
- verificar ocupação e vaga na hora;
- manter `PodePousar` e `AirOperationResolver` como autoridades finais.

### Quero Carona Aérea

- separar a verificação barata de emergência/reparo da análise terrestre de
  objetivo;
- reutilizar o resultado de pouso da mesma aeronave e snapshot;
- avaliar ganho de posicionamento sem executar nova varredura;
- nunca reservar vaga ou alterar camada durante a consulta.

### Melhor LZ de Embarque

- consultar LZs estruturais compatíveis pelo índice;
- calcular o alcance do transportador uma vez;
- reutilizar os mapas atual e futuro de cada passageiro;
- calcular `QueroCarona` uma vez por passageiro e snapshot;
- produzir todos os tiers numa única avaliação;
- permitir que Transport Operations apenas filtre o resultado.

### Quero Carona

- reutilizar o mapa operacional já calculado para a unidade;
- cachear a resposta pela revisão confirmada, objetivo e versão do plano;
- invalidar quando unidade, objetivo, reparo ou ocupação relevante mudar;
- preservar a diferença entre uma estimativa e uma ordem materializável.

### Melhor LZ de Desembarque

- receber o caminho compartilhado do transportador;
- persistir rotas reversas por passageiro, alvo e horizonte durante o snapshot;
- consultar apenas LZs alcançáveis e spots vizinhos estruturalmente possíveis;
- revalidar ocupação e conhecimento confirmado antes de criar a ação;
- preservar a regra de que o passageiro materializa sobre o transportador e
  pisa num vizinho válido.

## Save e Load

O save não armazenará uma cópia de todas as praias, terrenos, estradas ou
construções. Esses dados pertencem à cena e seriam duplicação volumosa de
informação imutável.

Cada mapa terá:

- identificador estável;
- `topologyVersion`;
- `topologyFingerprint`;
- índice topológico serializado junto do conteúdo do mapa.

O save registra apenas o identificador e o fingerprint necessários para
validar a associação. No load:

1. a cena fornece seu índice permanente;
2. o fingerprint é comparado ao registrado;
3. unidades e construções recuperam o estado dinâmico;
4. o `ConfirmedOccupancyIndex` é reconstruído uma vez;
5. caches de movimento e decisão começam vazios;
6. opcionalmente, um warm-up controlado prepara as primeiras unidades da IA;
7. `SaveGameManager.OnAfterLoadSuccess` só é disparado depois de o snapshot
   confirmado estar coerente.

Se uma atualização do jogo modificar o mapa, a divergência de fingerprint será
registrada e o índice atual da cena será usado. Não existe delta topológico de
partida a restaurar, pois o jogo não cria nem destrói esses elementos.

## Instrumentação e metas

Antes de substituir os caminhos atuais, serão registrados:

```text
TopologyFullScans
MovementWavesBuilt
MovementCacheHits
MovementCacheMisses
CellsVisited
MelhorEstoqueMs
MelhorPousoMs
MelhorEmbarqueMs
MelhorDesembarqueMs
QueroCaronaMs
TransportPlanningMs
```

As metas do ciclo são:

- zero varreduras completas de mapa durante decisões depois do carregamento;
- uma avaliação completa de transporte por unidade e snapshot;
- nenhuma BFS repetida com chave idêntica;
- nenhum novo `FindObjectsByType` por onda de movimento;
- rankings e ações iguais aos produzidos antes da otimização;
- Save/Load sem crescimento material por dados derivados;
- melhoria mensurável no tempo total da Phase 2 em mapas grandes.

## Validação funcional

O conjunto mínimo de testes deve cobrir:

- repetição da mesma consulta produzindo cache hit;
- cancelamento de movimento sem invalidação confirmada;
- compromisso de movimento atualizando ocupação depois de `Neutral`;
- spawn, morte, embarque, desembarque e mudança de camada;
- plataforma que perde ou recupera vaga;
- praia indexada que fica temporariamente ocupada;
- ponte sobre mar e ponte sobre praia;
- trem usando somente segmentos consecutivos declarados;
- mudança de dono, captura e estoque sem reconstrução topológica;
- Save/Load preservando o mesmo ranking e a mesma legalidade;
- FOW sem revelar praia, construção ou unidade desconhecida por ausência de
  opção;
- IA e jogador recebendo a mesma resposta dos sensores para a mesma situação;
- replay produzindo o mesmo estado confirmado.

## Ordem de implementação

1. Instrumentar custo e contagem das consultas atuais.
2. Implementar e validar `BoardTopologyIndex`.
3. Remover as varreduras completas de `MelhorPouso` e `MelhorEmbarque`.
4. Implementar `ConfirmedOccupancyIndex`.
5. Implementar `MovementReachCache` sem alterar semântica.
6. Criar `TransportPlanningSnapshot`.
7. Adaptar Estoque, Desembarque e as duas variantes de Quero Carona.
8. Integrar fingerprint, reconstrução dinâmica e warm-up ao Save/Load.
9. Comparar rankings, ações, métricas e comportamento transacional.
10. Somente depois avaliar a unificação das árvores de movimento.

## Resultado esperado

O tabuleiro físico passa a ser preparado uma vez. As unidades atualizam apenas
o índice dinâmico depois de ações comprometidas. As ferramentas Melhor X
consultam candidatos prontos, reutilizam alcances equivalentes e continuam
pedindo aos sensores oficiais a palavra final.

Em termos práticos, o jogo deixa de perguntar repetidamente “onde está a
praia?” e passa a perguntar somente “qual das praias conhecidas e
estruturalmente possíveis serve para esta unidade, neste snapshot confirmado?”.

Essa mudança reduz espera sem simplificar as regras. A jogabilidade continua
determinística, orientada por domínio, logística e informação, e nada se torna
definitivo antes do compromisso oficial da ação.
