# Refactor do Quero Carona

## Problema atual

`QueroCaronaService` ainda responde principalmente a uma pergunta booleana:

> Esta unidade quer carona?

Isso é insuficiente para o planejamento coletivo. O transportador também
precisa saber:

- para quê a unidade quer transporte;
- qual alvo, setor ou unidade motivou o pedido;
- quão urgente é a necessidade;
- qual capacidade de transporte é exigida;
- quando a unidade consegue cumprir a intenção sozinha;
- qual ganho operacional será produzido pelo deslocamento.

Sem essa intenção, o transportador tenta deduzir sozinho o papel do passageiro
e pode escolher uma LZ correta para a geometria, mas errada para a missão.

## Contrato pretendido

O passageiro declara a necessidade e a intenção. O transportador decide apenas
como materializá-las.

## Mapa de magnets

`Magnet` é uma fonte de intenção operacional. Ele pode ser uma construção, uma
unidade aliada ou uma necessidade confirmada. O papel seguidor decide como
responder: marchar, escoltar, apoiar, permanecer numa faixa ou declarar
carona.

O grafo inicial observado no jogo é:

```text
Construção não controlada
   ├─ Capturador                         prioridade alta
   └─ Capturador Combatente               prioridade baixa

Capturador / cabeça de ponte
   ├─ Assault
   ├─ FireSupport
   │    ├─ FireSupport combatente
   │    └─ Antiaéreo combatente
   ├─ Antiaéreo                          fallback
   ├─ AirStrike
   ├─ AirCombat
   └─ AirSurveillance                    fallback de direção

AirSurveillance
   └─ Antiaéreo                          prioridade acima do Capturador

Unidade ou construção com estoque baixo
   └─ Stock

Unidade UnderRepair
   └─ Logistics

Intenções produzidas pelos papéis
   └─ Transporter                        alavanca/materialização
```

O grafo mistura relações já implementadas com prioridades pretendidas para o
refactor. Em especial, a precedência dedicada de `Capturador` sobre
`CapturadorCombatente` e a produção tipada de intenções por todos os papéis ainda
precisam ser formalizadas e testadas.

### Prioridades e composição de papéis

- `Capturador` possui prioridade maior que `CapturadorCombatente` na
  distribuição normal de construções.
- `CapturadorCombatente` continua podendo capturar, mas sua participação não
  deve retirar a missão principal de um Capturador dedicado.
- Assault e FireSupport usam a cabeça de ponte como direção quando não existe
  ação Tactical ou plano mais específico.
- seguir um Capturador não autoriza Assault a ocupar a construção que o
  capitão declarou como próxima conquista;
- o hex declarado é evitado no movimento normal, continua permitido durante
  combate e só é tolerado como deslocamento quando não existe alternativa
  materializável;
- essa proteção deve cobrir tanto slots formais do `TeamObjectivePlan` quanto
  alvos rogue/rebeldes escolhidos por proximidade;
- `FireSupport combatente` preserva sua capacidade de lutar antes da atração.
- `AntiaereoCombatente` pode entrar no roteador como FireSupport, mas sua
  capacidade `Antiaereo` ativa a preferência especial por Vigilância Aérea.
- Antiaéreo usa Radar Móvel, depois EWACS, antes de cair para Capturador.
- AirStrike e AirCombat não devem iniciar guerras particulares longe da cabeça
  de ponte quando não possuem alvo Tactical válido.
- AirSurveillance possui seu ranking próprio de cobertura, retaguarda,
  repulsão e plataforma; Capturador é apenas uma direção de fallback, nunca
  substitui ganho de cobertura.
- Stock é atraído por demanda real de estoque em unidades ou construções.
- Logistics é atraída por `UnderRepair`, evacuação, reparo e demais
  necessidades compatíveis declaradas.

### Transportador como alavanca

Transportador não é magnet estratégico e não inventa uma finalidade para o
passageiro.

Ele recebe intenções produzidas pelos demais papéis e pergunta:

```text
esta intenção já é materializável pelo passageiro?
   ├─ sim → não interfere
   └─ não → existe transporte compatível com ganho operacional?
              ├─ sim → materializa pickup, travessia ou entrega
              └─ não → passageiro mantém fallback próprio
```

Portanto:

- construção não controlada motiva Capturador, não o Chinook;
- cabeça de ponte motiva FireSupport, não o navio;
- estoque baixo motiva Stock, não o trem por simples proximidade;
- `UnderRepair` motiva Logistics, não o transportador por adivinhação;
- somente a intenção resultante pode ser amplificada por um transportador.

Esse modelo evita a inversão de autoridade observada durante os testes: o táxi
não manda no passageiro; o passageiro declara por que precisa do táxi.

Uma solicitação deve carregar:

- unidade solicitante;
- finalidade;
- alvo, setor ou unidade desejada;
- urgência;
- Tactical, Operational ou BeyondOperational;
- custo da rota própria;
- tipo de slot, camada, carga ou plataforma exigido;
- ganho esperado;
- motivo legível para logs e ferramentas.

Exemplo:

```text
Quero carona: SIM
Finalidade: Capturar
Destino: construção (-31, -13)
Setor: Operational P2
Urgência: normal
Alcance próprio: BeyondOperational
Motivo: demais construções locais já reservadas
```

## Finalidades

O modal deve perguntar:

> Você quer carona para quê?

- [ ] `Capture` — capturar uma construção ou cumprir uma agenda formal de
  captura.
- [ ] `SectorPressure` — pressionar, reforçar ou atacar um setor.
- [ ] `RevealFog` — revelar terreno ou contato necessário para a missão.
- [ ] `AirSurveillance` — levar Radar Móvel ou EWACS a uma zona com ganho de
  cobertura aérea.
- [ ] `CombinedArmsEscort` — acompanhar uma unidade aliada cuja função
  complementa a do passageiro, como SAM escoltando Radar Móvel ou EWACS.
- [ ] `LogisticsSupport` — alcançar setores, construções ou unidades aliadas
  com estoque crítico.
- [ ] `RepairOrEvacuation` — alcançar reparo ou retirar uma unidade avariada.
- [ ] `LandingSupport` — alcançar unidade ou construção com suporte de pouso
  compatível, incluindo porta-aviões e fragatas para helicópteros.

As opções do modal são filtros de estudo das intenções. A prioridade runtime
continua sendo determinada por emergência, papel da unidade, plano e ganho
operacional — não pela ordem visual das caixas.

## Autoridades preservadas

- `PodeEmbarcarSensor`: compatibilidade de slot, camada, classe, skills,
  exclusividade e vaga.
- `MelhorEmbarqueService`: ponto de encontro entre passageiro e transportador.
- `MelhorDesembarqueService`: LZ coerente com a intenção transportada.
- `PodePousarSensor` e `MelhorPousoService`: pouso e plataforma compatível.
- `PodeCapturarSensor`: legalidade da captura.
- `UnitMovementPathRules`: custo e alcance reais.
- `TeamObjectivePlan` e `SectorObjective`: agenda formal e reservas.
- snapshot confirmado: ocupação, posição, FOW e revisões.

## Hotzone materializável de embarque

Um encontro `ReachableNow` não significa apenas que o passageiro consegue
chegar perto do transportador.

Ele precisa conseguir pagar, no mesmo turno:

```text
custo do caminho até a posição de embarque
+ custo oficial para entrar na célula do transportador
<= movimento restante
```

O segundo custo vem do `PodeEmbarcarSensor` e considera:

- terreno sob o transportador;
- custo básico de autonomia;
- overrides de skill;
- fallback válido para transições entre camadas.

Quando o passageiro pode entrar normalmente na célula, vale o custo real do
terreno. Quando ele não pisaria nessa célula e depende do fallback de transição
— por exemplo, avião ou helicóptero embarcando em navio — o custo é sempre 1.
Assim a aeronave precisa conservar pelo menos 1 ponto para concluir o embarque.

Não deve existir hard-code `Tactical - 1`. Em terreno de custo 2, por exemplo,
o passageiro precisa conservar 2 pontos depois da aproximação. Obstáculos e
desvios permanecem incorporados pelo pathfinding da primeira parcela.

Quando a soma não cabe no turno atual:

- a opção não é `ReachableNow`;
- pode permanecer `ReachableLater` como direção Operational;
- o transportador deve aproximar sua LZ da hotzone real do passageiro;
- ele não pode gastar a ação esperando numa posição em que o embarque ainda é
  impossível.

## Reserva coletiva de captura

Uma construção não pode justificar simultaneamente a recusa de carona de
vários capturadores.

Antes de responder aos pedidos de captura, o planejamento deve produzir uma
projeção pura de reivindicações:

1. reunir capturadores ativos do slot;
2. reunir construções capturáveis;
3. calcular alcance Operational por caminhos válidos;
4. dar prioridade ao capturador formalmente atribuído;
5. distribuir as oportunidades restantes pelo melhor custo de rota;
6. atribuir no máximo uma construção a cada capturador;
7. atribuir no máximo um capturador a cada construção;
8. fazer unidades não atendidas procurarem outro alvo;
9. solicitar carona quando o próximo objetivo útil estiver além do
   Operational.

Essa reivindicação é somente uma projeção do planejamento. Ela não ocupa a
construção, não altera o plano persistido, não incrementa revisão e desaparece
quando o snapshot confirmado muda.

### Armadilha de validação

Cenário:

- uma construção vazia;
- cinco soldados ao redor;
- nenhuma outra construção dentro do Operational;
- construções distantes disponíveis por transporte.

Resultado correto:

- apenas um soldado reserva a construção local e recusa carona;
- os outros quatro não reutilizam a mesma construção como justificativa;
- eles procuram os alvos distantes;
- quando esses alvos estão BeyondOperational, declaram intenção `Capture`;
- transportadores adjacentes reconhecem os pedidos e não abandonam o grupo.

## Rupturas de mobilidade e mapas com ilhas

O próximo laboratório do refactor são mapas com componentes de movimento
desconectados.

O problema não deve ser modelado como uma exceção chamada “ilha”. A pergunta
geral é:

> A unidade possui uma rota própria completa até a missão escolhida?

Fluxo pretendido:

```text
objetivo operacional ou estratégico
        ↓
existe rota própria completa?
   ├─ sim → marcha normalmente
   └─ não → existe transportador capaz de atravessar a ruptura?
              ├─ sim → declara intenção de carona
              └─ não → procura outro objetivo ou aguarda em rally
```

Uma ruptura pode ser:

- mar entre duas massas terrestres;
- rio ou canal sem travessia compatível;
- ferrovia desconectada para uma unidade ferroviária;
- terreno ou camada que a unidade não consegue atravessar;
- qualquer separação entre componentes do grafo de movimento.

### Comportamento atual

Em mapas continentais, Rebel, Assault e FireSupport escolhem direções próximas
e se espalham de forma funcional.

Em mapas com ilhas:

- escolhem um objetivo pela direção ou proximidade;
- marcham até a costa;
- deixam de possuir progresso materializável;
- acumulam-se na praia;
- não publicam claramente que precisam atravessar uma ruptura;
- transportadores não recebem missão suficiente para buscá-los.

### Por que Capturadores já atravessam

Capturadores já produzem parcialmente o comportamento desejado porque procuram
qualquer construção ainda capturável, inclusive construções offshore.

Esse alvo concreto permite que:

- `QueroCarona` reconheça que a construção está BeyondOperational;
- o capturador declare necessidade de transporte;
- navios encontrem o passageiro;
- a direção da travessia seja inferida pelo objetivo de captura.

Assault e FireSupport normalmente possuem apenas direção, pressão ou apoio,
sem um alvo de transporte igualmente explícito. Eles chegam à praia, mas a
ruptura não se transforma em demanda.

Essa diferença é evidência de que o contrato orientado por intenção deve ser
generalizado, não substituído por uma regra especial para navios.

### Pedido futuro

Uma unidade bloqueada por ruptura deverá declarar:

```text
Finalidade: SectorPressure ou FireSupport
Objetivo: setor operacional
Rota própria: componente desconectado
Origem possível: praia/LZ de embarque
Destino desejado: praia/LZ de desembarque
Capacidade exigida: classe/camada do passageiro
```

O transportador passa a atender demanda por eixo de travessia, em vez de
escolher somente a unidade geometricamente mais próxima.

### Evitar montinhos artificiais

- somente passageiros com vaga/projeção compatível avançam até a LZ;
- excedentes aguardam em rally de retaguarda;
- cada assento atende uma intenção por vez;
- transportadores não reservam corredores definitivos;
- bloqueios posteriores provocam reavaliação, não movimento ilegal;
- depois do desembarque, a unidade recalcula sua agenda.

### Cache de topologia

Como o tabuleiro não é destruído durante a partida, os componentes estáticos de
movimento podem ser cacheados por:

- mapa;
- categoria de movimento;
- domínio e camada;
- versão/fingerprint da topologia.

Unidades, ocupação e ameaças continuam dinâmicas. A conectividade estrutural
não precisa ser reconstruída para cada decisão.

### Estado do estudo

Por enquanto, o comportamento caótico será mantido em observação. A partida
completa deve mostrar:

- onde Assault e FireSupport formam filas;
- quais navios passam perto sem reconhecer demanda;
- se o acúmulo ocorre por ausência de intenção, vaga, LZ ou compatibilidade;
- como os Capturadores bem-sucedidos atravessam e quais dados já fornecem ao
  transportador.

### Capitão embarcado, comboio e persistência do magnet

O magnet de Capturador não pode desaparecer simplesmente porque a unidade
embarcou. Hoje `TryResolveCapturerMagnet` ignora unidades `IsEmbarked`, e cada
seguidor reelege o Capturador desembarcado mais próximo a partir do snapshot
atual. Não existe um capitão global nem um vínculo persistente de formação.

Isso produz uma ruptura previsível:

```text
3 Capturadores + grupo de tanques na praia
        ↓
2 Capturadores embarcam
        ↓
o terceiro Capturador vira o magnet local dos tanques
        ↓
o terceiro também embarca
        ↓
os tanques perdem o magnet e voltam ao fallback de Assault
```

O embarque do capitão também não autoriza automaticamente o embarque dos
seguidores. Cada tanque ainda precisa:

- declarar a própria necessidade de travessia;
- encontrar um transportador com slot, camada e classe compatíveis;
- possuir uma LZ materializável;
- preservar sua agenda quando não houver vaga no mesmo navio.

Portanto, a solução não pode ser apenas usar a posição do navio como novo
magnet. Isso faria unidades terrestres perseguirem uma coordenada no mar ou
amontoarem-se na praia sem um plano de embarque.

#### Contrato pretendido

Durante uma travessia ativa:

1. o Capturador embarcado continua elegível como capitão;
2. o transportador que o carrega torna-se a âncora móvel/proxy da formação;
3. o vínculo seguidor-capitão possui estabilidade durante a operação;
4. seguidores não trocam de capitão a cada `commit light` apenas porque outro
   Capturador desembarcado ficou momentaneamente mais perto;
5. cada seguidor avalia sua própria ruptura e compatibilidade;
6. seguidores compatíveis solicitam embarque no mesmo comboio;
7. seguidores incompatíveis procuram outro transportador do mesmo eixo de
   travessia;
8. sem vaga materializável, aguardam numa LZ/rally de embarque em vez de
   perseguir o navio;
9. depois do desembarque do capitão, a formação volta a usar sua posição real;
10. abandono da travessia, morte, mudança de missão ou impossibilidade
    persistente liberam a eleição de outro capitão.

O pedido do seguidor deve carregar, além da finalidade normal:

```text
Finalidade: CombinedArmsEscort ou SectorPressure
Capitão: Capturador #id
Capitão embarcado: sim
Transportador proxy: unidade #id
Eixo/compromisso de travessia: id estável
LZ de embarque pretendida: célula
LZ/setor de desembarque pretendido: célula/setor
Compatibilidade exigida: slot, classe, domínio e camada
Estado: seguir, reunir, aguardar vaga, embarcado ou desembarcado
```

O transportador continua sendo alavanca, não comandante. Um tanque não embarca
“porque o capitão mandou”; ele declara que precisa acompanhar a cabeça de ponte
e o `Quero Carona` materializa essa intenção quando houver ganho e
compatibilidade.

#### Persistência e save/load

O capitão individual ainda pode ser uma projeção derivada enquanto não existe
travessia. Quando um embarque inicia uma operação que atravessa turnos, o
compromisso mínimo precisa sobreviver a save/load:

- capitão;
- transportador proxy;
- eixo ou missão de travessia;
- LZ de origem e destino;
- seguidores já aceitos pelo comboio.

Rankings, caminhos, vagas possíveis e candidatos excedentes continuam
derivados e são reconstruídos do snapshot confirmado. Cancelamento ou rollback
não pode publicar o compromisso provisório.

#### Cenário de validação

- três Capturadores e vários tanques chegam a uma praia;
- dois Capturadores embarcam;
- o terceiro permanece temporariamente em terra;
- depois, o terceiro também embarca;
- tanques compatíveis encontram transporte do mesmo comboio;
- tanques sem transporte aguardam numa LZ coerente;
- nenhum tanque tenta caminhar até a posição marítima do navio;
- o grupo não troca de capitão apenas por oscilação de distância;
- ao desembarcar, o capitão volta a ser o magnet físico da formação.

Essa política depende do contrato tipado de intenção, de compatibilidade,
capacidade, LZ e persistência. Ela não deve ser implementada como uma emenda
isolada no `TryResolveCapturerMagnet`.

### Emenda experimental: Capturador como ímã

Antes do contrato completo de travessia, será testada uma regra de coesão
simples:

- Capturadores continuam escolhendo e atravessando rumo às construções;
- combatentes não capturadores usam um Capturador aliado ativo como cabeça de
  ponte;
- o Capturador mais próximo vence, com desempate determinístico por
  `InstanceId`;
- combatentes preferem formar uma faixa a 1 hex do Capturador, sem tornar o
  hex do capitão ilegal quando não houver alternativa útil;
- ataque e demais necessidades Tactical continuam tendo precedência;
- somente o fallback de direção usa o ímã;
- a escolha é refeita a partir do snapshot confirmado depois de cada ação.

O experimento cobre inicialmente:

- Assault rogue;
- FireSupport rogue;
- Interceptador, Ataque Aéreo e Raid AntiSub;
- destino de missão de uma aeronave combatente ainda embarcada.

Essa regra não cria formação rígida, corredor reservado ou compromisso
persistente. Ela apenas impede que combatentes sem tarefa local inventem uma
guerra particular longe da infantaria que materializa a frente.

O teste deve observar:

- se caças deixam de cruzar o mapa sozinhos;
- se Assault e FireSupport se distribuem entre diferentes cabeças de ponte;
- se o Capturador mais próximo é uma âncora boa o bastante ou se será preciso
  preferir Capturador com objetivo/reserva formal;
- se o movimento até a praia passa a produzir pedidos de transporte úteis;
- se a troca de ímã entre commits causa oscilação.

Mesmo funcionando, a emenda não substitui o futuro pedido tipado de transporte:
um combatente terrestre pode seguir a direção de um Capturador offshore e ainda
precisar declarar explicitamente a ruptura de mobilidade ao navio.

### Embrião de força combinada

O ímã não deve ser universal. A âncora preferida depende do papel e das
limitações funcionais do seguidor.

Primeiro caso implementado: defesa antiaérea.

Um SAM possui visão local limitada e pode não detectar aeronaves furtivas.
Radar Móvel e EWACS fornecem a consciência aérea que permite ao SAM exercer sua
função com mais coerência. Por isso, unidades que satisfazem `Antiaereo` usam:

```text
ação Tactical disponível?
   ├─ sim → combate/necessidade local tem precedência
   └─ não → Radar Móvel aliado com rota estrutural válida?
              ├─ sim → usa Radar Móvel como âncora
              └─ não → EWACS em posição estruturalmente alcançável?
                         ├─ sim → usa EWACS como âncora
                         └─ não → usa Capturador como cabeça de ponte
```

Regras atuais:

- Radar Móvel vence EWACS porque compartilha o domínio terrestre com o SAM;
- distância cúbica não basta: a âncora de Vigilância Aérea precisa possuir rota
  estrutural válida para o seguidor;
- EWACS sobre mar ou componente terrestre desconectado não arrasta o SAM para
  uma costa sem saída;
- Capturador permanece como fallback;
- artilharia comum, Assault e AirCombat continuam usando Capturador;
- a mesma âncora deve sobreviver às passagens internas de reposicionamento e
  progressão da decisão;
- logs identificam tipo, `InstanceId`, célula e distância da unidade capitã.

Exemplos de diagnóstico:

```text
AirSurveillance:RadarMovel=#119 anchor=(...) dist=...h
AirSurveillance:EWACS=#113 anchor=(...) dist=...h
CapturerMagnet=#112 anchor=(...) dist=...h
```

Esse comportamento ainda é apenas um embrião: não cria uma formação militar
persistente nem compartilha automaticamente sensores entre unidades. Ele
produz coesão espacial a partir de uma dependência real já existente nas
fichas e regras do jogo.

No contrato futuro do `Quero Carona`, essa relação poderá gerar
`CombinedArmsEscort` quando o seguidor não possuir rota própria até a unidade
capitã. O transportador continuará sem autoridade para inventar a missão: ele
apenas atenderá uma intenção declarada pelo passageiro.

### Evidência runtime: Fire Support #39

Durante o teste com o Lança-Foguetes #39, o fluxo atual produziu:

```text
policy=FireSupport
QueroCarona=SIM
reach=BeyondOperational
motivo=sem prédio capturável livre
transportador=#88
LZ=(-27,-12)
route=ReachableNow
paxCost=2
transportCost=0
```

Dois defeitos independentes foram observados.

#### Finalidade incorreta

O Lança-Foguetes pediu carona porque o serviço genérico não encontrou prédio
capturável em Tactical ou Operational.

Fire Support não deve usar disponibilidade de construção capturável como
fonte de intenção. A pergunta correta é uma destas:

- existe rota própria até a força/cabeça de ponte que deve apoiar?
- existe uma ruptura de mobilidade entre a unidade e `SectorPressure`?
- a unidade precisa acompanhar uma relação `CombinedArmsEscort`?
- há posição de tiro, screen ou rendezvous materializável sem transporte?

Somente depois dessas perguntas Fire Support pode declarar carona.

#### `ReachableNow` não materializado

O passageiro possuía pontos de movimento suficientes e o próprio planejamento
classificou a opção como `ReachableNow`. Mesmo assim, o batch moveu #39 apenas
de `(-29,-14)` para `(-28,-13)`, deixando-o a um passo da LZ e sem embarcar.

Contrato esperado:

```text
passageiro ReachableNow
+ transportador já na LZ
+ custo de rota + custo de embarque <= movimento restante
    → batch transacional mover + embarcar
```

Somente quando a soma não couber na rodada o resultado deve ser
`TransportRendezvous` com aproximação parcial.

O batch combinado continua sujeito à lei transacional:

- movimento e embarque são provisórios até a confirmação;
- falha em qualquer etapa cancela ou degrada de maneira explícita;
- FOW, ocupação, combustível e `HasActed` só mudam no compromisso;
- o resultado confirmado retorna a `Neutral`.

#### Decisão de sequenciamento

O refactor não será iniciado no meio da rodada de observação. Como o defeito não
travou a partida, a rodada deve ser concluída para coletar:

- outros papéis pedindo carona por finalidade incorreta;
- opções `ReachableNow` que viram apenas progressão;
- transportadores aguardando passageiros que mudam de agenda;
- unidades que deveriam seguir capitão, setor ou suporte logístico;
- divergências entre decisão, apresentação e batch executado.

Essas evidências alimentarão os testes pequenos e previsíveis do contrato
tipado.

## Quero Carona Aérea

`QueroCaronaAereaService` deve ser absorvido por este contrato.

O antigo “quero embarque aéreo” passa a ser:

- `LandingSupport`, quando a aeronave procura pista ou plataforma;
- `AirSurveillance`, quando Radar/EWACS precisam de reposicionamento;
- `RepairOrEvacuation`, quando combustível ou dano tornam a recuperação
  urgente.

Emergência não exige um serviço paralelo; é a mesma intenção com prioridade
máxima.

## Sequenciamento sugerido

1. Desempate coletivo 1:1 para construções capturáveis.
2. Introduzir `RidePurpose` no resultado.
3. Separar `Capture` de `SectorPressure`.
4. Adicionar `RevealFog`.
5. Adicionar `LogisticsSupport`.
6. Adicionar `CombinedArmsEscort`.
7. Integrar `AirSurveillance`.
8. Integrar `RepairOrEvacuation` e `LandingSupport`.
9. Atualizar o modal, logs, save/load e ferramentas de comparação.
10. Aposentar `QueroCaronaAereaService`.

## Save e load

Intenções derivadas podem ser reconstruídas a partir do snapshot confirmado.
Somente compromissos operacionais que precisem sobreviver à troca de turno
devem ser persistidos.

Ao carregar:

- restaurar planos formais;
- restaurar unidades, transportes e ocupação confirmada;
- invalidar projeções transitórias;
- reconstruir intenções e reivindicações;
- liberar a interface somente quando o planejamento necessário estiver
  coerente.

## Contrato transacional

Consultas de carona e reivindicações são puras.

Antes do compromisso é proibido:

- ocupar construção;
- reservar slot definitivamente;
- mover passageiro ou transportador;
- pintar FOW;
- publicar contato;
- consumir combustível ou movimento;
- alterar `HasActed`;
- incrementar revisão confirmada.

O resultado apenas explica uma possibilidade. A ação vencedora continua sendo
materializada e comprometida pelo fluxo normal do turno.
