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
6. Integrar `AirSurveillance`.
7. Integrar `RepairOrEvacuation` e `LandingSupport`.
8. Atualizar o modal, logs, save/load e ferramentas de comparação.
9. Aposentar `QueroCaronaAereaService`.

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
