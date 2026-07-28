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
