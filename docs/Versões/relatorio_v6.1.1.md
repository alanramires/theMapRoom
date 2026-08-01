# Táxi e Carona

## Versão

`v6.1.1`

## Objetivo

A `v6.1.0` documentou, com evidência, por que o táxi nunca chegava no ilhado.
Esta versão conserta — e o conserto não é uma regra especial para ilhado, é
**ordem por antiguidade**: quem espera mais, sobe.

Três perguntas que antes se confundiam numa só passaram a ser respondidas
separadamente, e cada uma por quem sabe:

| pergunta | quem responde |
|---|---|
| "eu chego lá a pé?" | envelope + componente de movimento do passageiro |
| "há quanto tempo eu espero?" | carimbo de fila na unidade |
| "eu, veículo, chego até você?" | componente de movimento do transportador |

## Fome estrutural: não é longe, é ilha

O envelope tem duas bandas e para no segundo turno, por contrato. Ele não
distingue "turno 3" de "outra ilha", e sem essa distinção **todo pedido de
carona valia o mesmo**.

`QueroCaronaResult.isStranded` separa os dois casos, via a consulta dirigida que
o contrato já previa — `ClassifyMobility` sobre o componente de movimento sem
teto de turnos. `MobilityRelation.OtherComponent` é, literalmente, "pedido de
carona".

O rogue/rebelde tem pergunta própria, porque não tem um objetivo designado:
*existe algum capturável dentro do meu componente?* Nenhum = ilhado. Isso
importa porque **nada nesta versão depende de plano** — era esse o buraco
original, a reserva sem volante.

O flood fill só roda para quem já falhou as duas bandas, ou seja, para quem vai
pedir carona de qualquer jeito.

## A barrinha do panda

> "Quanto mais tempo você deixa o panda esperando, mais a barrinha de frustração
> dele aumenta." — a doutrina, em uma frase

`UnitManager.aiRideWaitSinceTurn` registra **quando** a unidade entrou na fila.
Três decisões de implementação:

- **Carimbo, não contador.** A espera é derivada (`turno atual − carimbo`). O
  pedido de carona é reavaliado dezenas de vezes por turno — uma vez por
  transportador que planeja coleta — e um contador incremental viraria contagem
  dupla na primeira reentrada.
- **`MarkAIRideWaitStart` é idempotente.** Chamar de novo não reinicia a espera:
  quem já estava na fila mantém a antiguidade, que é o ponto todo da fila.
- **Zero é o sentinela**, não `-1`. Save antigo não tem o campo, desserializa
  como 0, e como turno começa em 1 o zero já significa "não está esperando".
  Sem migração.

A antiguidade vira urgência:

```text
score = base + turnos_esperando × 100,  limitado a 2000
```

| situação | turno 0 | +3 turnos | +5 turnos |
|---|---|---|---|
| longe, mas anda até lá | 1000 | 1300 | 1500 |
| sem rota própria | 1500 | 1800 | **2000 (teto)** |
| emergência de reparo | 2000 | 2000 | 2000 |

O teto é deliberado: o caroneiro no talo **empata** com o ferido, nunca passa.
Ferido morre; ilhado espera. O excedente da espera não vai para o ranking — ele
existe para virar pressão de compra de mais transporte, que é a doutrina da
esteira: a parada lotada não sequestra o ônibus, ela justifica comprar outro.

O envelhecimento é aplicado no resultado **já devolvido** pelo serviço, nunca
dentro dele: o `QueroCaronaService` tem cache próprio cuja chave não conhece
turno, então somar a espera lá dentro serviria número velho no turno seguinte.
O objeto devolvido é sempre uma cópia — o serviço clona na leitura e na escrita
do cache —, então mexer nele do lado da IA é seguro. O serviço permanece puro:
janelas de Editor consultam à vontade sem mexer na fila de ninguém.

## As duas regras que produziam fome

Ambas no `MelhorEmbarqueService`, ambas desenhadas olhando **um** passageiro por
vez, e juntas produzindo o mesmo efeito: o mais necessitado é o mais caro de
servir, então perde sempre.

**A busca parava antes de chegar nele.** `stopAfterDecisiveTactical` encerrava a
varredura no primeiro passageiro perto que embarca agora, e as LZs distantes
nunca viravam opção. O ilhado não perdia a disputa — não entrava nela. A única
condição que suspendia a parada era emergência de reparo.

Agora `IsNonNegotiableRideNeed` inclui quem não tem rota própria **e já esperou
três turnos**. O limiar não é enfeite: um ilhado recém-chegado não pode custar a
varredura completa todo turno. É a espera que promove.

**A penalidade invertia a necessidade.** `NoCurrentRoute` custava −10000
enquanto "quero carona" pagava +1000 — quem precisava de resgate era penalizado
por precisar de resgate.

A distinção que resolve: "não tem rota" é defeito da **opção** para quem tem
pernas (entre duas LZs, a inalcançável vale menos) e é condição do
**passageiro** para quem nunca terá rota. Para o segundo, a penalidade é zerada.
O anel de aproximação continua valendo e é ele que ordena as LZs desse
passageiro — o transportador tem de encostar, porque quem não anda não vai ao
encontro.

Resultado, para um ilhado com transporte capaz a 10 hexes contra um concorrente
a 3 hexes que embarca agora:

| turno na fila | score do ilhado | concorrente | quem ganha |
|---|---|---|---|
| 0 | 91.500 | 100.699 | vizinho |
| 2 | 91.700 | 100.699 | vizinho |
| **3** | **100.800** | 100.699 | **ilhado** |

O sistema serve quem está perto enquanto isso é barato, e por volta do terceiro
turno de espera para de conseguir ignorar o cara na ilha.

## APC não cruza o mar

Promessa que o veículo não pode cumprir é pior que promessa nenhuma: reserva o
passageiro, gasta o turno do transportador e — depois da mudança acima — ainda
suspende o encerramento da varredura por causa de alguém que aquele APC jamais
alcançaria.

`CanTransporterMeetPassenger` pergunta se os componentes de movimento dos dois
**se tocam**. Tocar, não conter, porque embarque acontece com os dois em hexes
vizinhos:

| par | resultado |
|---|---|
| infantaria no continente + navio | encontro existe — ela caminha até a praia |
| infantaria na ilha + APC terrestre | não existe — descartado |
| qualquer um + aeronave | componente da aeronave é o tabuleiro |

O desenho é assimétrico de propósito. **Conectividade é pergunta do veículo** —
é ele que viaja longe. O passageiro não precisa de componente: a pergunta dele é
"consigo caminhar até um ponto de encontro em tempo útil", e isso é uma malha de
custo limitada a dois turnos, que já passa pelo `MovementReachCache`.

## O custo, e o que ele ensinou

A primeira versão desta entrega custou **43 segundos** num turno que rodava em
1,3s. A trilha até o conserto:

| etapa | tempo | o que mudou |
|---|---|---|
| primeira versão | 43.384ms | 47 floods do tabuleiro inteiro |
| encontro assimétrico | 22.681ms | 16 floods |
| componente por perfil | 17.521ms | ~8 floods |
| delegação com cache de consulta | **1.437ms** | mesmos ~8, 12× mais baratos cada |

O salto final mostra que o gargalo nunca foi *quantos* floods — foi o custo de
um. **Toda** varredura de tabuleiro do `UnitMovementPathRules` monta um
`MovementQueryCache`, resolvendo terreno, construção e estrutura uma vez por
célula. `BuildOwnMovementComponent` era a única exceção: BFS escrito à mão
chamando `TryGetEnterCellCost` cru, seis vezes por célula.

Ele agora delega para `CalculateTurnChainedCostMap` com horizonte alto, porque
essa busca **já implementa a regra do componente** — teto de MP por turno, hex
mais caro que o teto é intransponível para sempre e bloqueia o corredor atrás
dele. Três ganhos de uma vez: mais barato, mais correto (passa a usar as regras
reais de travessia, com célula anterior) e uma regra só, em vez de dois códigos
que podiam divergir.

Componentes também passaram a ser compartilhados por **perfil de movimento**
(ficha + domínio + altura + embarcado + teto de MP), nunca por instância: duas
unidades iguais na mesma massa de terra têm literalmente o mesmo conjunto.

`BuildOwnMovementComponent` ganhou `AIDecisionPerfScope` e o contador
`OwnMovementComponentBuilds` — ele aparece nominalmente no
`[AI Perf][Phase2 Breakdown]` em vez de virar suspeita na próxima investigação.

## Verificação

Mesmo turno, antes e depois de tudo: **1.289ms → 1.437ms**. Os ~150ms de
diferença são o preço de toda a funcionalidade nova. Heap durante a Fase 2 caiu
de 1.196MB para 951MB.

A fila aparece no log em transição de estado, não a cada avaliação:

```text
[FilaCarona] #90 entra na fila no turno 5 — fora das bandas (score=1000).
[FilaCarona] #90 sai da fila após 3 turno(s) — embarcou.
```

E no `MelhorEmbarque`:

```text
ACCEPT pax=#90 carona=Requested ajuste=1800 fila=3t INEGOCIÁVEL motivo=...
```

O inspector mostra `Ride Wait Since Turn` com a caixa "esperando há N turno(s)".
O `UnitManager` usa custom editor, então o campo precisou ser desenhado
explicitamente — campo serializado sozinho não aparece.

**O que NÃO foi observado ainda:** nenhuma unidade apareceu como
`SEM ROTA PRÓPRIA` nas partidas de teste. O mapa usado não tem o caso —
todos os pedidos foram "fora das bandas", score 1000, todos entrando na fila no
mesmo turno. A aritmética do desempate por antiguidade está escrita e o caminho
foi exercitado, mas o cenário do ilhado ainda não existiu em jogo.

## Estado da esteira

| doutrina | estado |
|---|---|
| quem embarca primeiro dita a rota | já existia (`ResolvePrimaryPassenger`, FIFO) |
| larga no Tactical do objetivo dele | já existia |
| o próximo da fila vira referência | já existia |
| encher a vaga livre no caminho | **falta** |
| não esquecer a promessa | **falta** |
| espera vira pressão por outro transporte | **falta** |

## Pendências conhecidas

- **Carga a bordo desliga a coleta.** `TransportOperationsService.BuildAttempts`
  retorna cedo com Courier/Delivery quando há qualquer carga, e `Pickup` nunca é
  tentado — origem do transporte de duas vagas viajando com uma. **Cuidado:**
  consertar isso sozinho **piora** a fome, porque o veículo passa a ficar
  ocupado mais tempo. Tem que entrar junto com a promessa ou com a pressão de
  compra.
- **A promessa não existe como objeto.** O plano do transportador para o
  passageiro tem que virar `SetAIDesignatedMission` no transportador, com
  `targetUnitInstanceId` — o campo já existe e já persiste no save. Aí
  `IsAlreadyFormalPassenger` passa a ler a promessa em vez do pareamento do
  plano, e "reservado" e "prometido" viram o mesmo objeto. Falta um valor novo
  no fim do `AIPlanRuntimeIntent` (o enum é persistido como `int`; nunca
  renumerar) e uma regra de baixa, senão promessa velha reconstrói a fome um
  andar acima.
- **Pressão de compra pela espera** — o excedente acima do teto de 2000 não é
  usado por ninguém ainda. Atenção: existem **dois** sistemas paralelos de
  demanda de transporte no shopping, gateados pelo mesmo slot do plano.
- Três portões fora do Quero Carona ainda decidem "vai a pé" com `MP × 2`
  pooled: `AIController.Rebel.cs` (que decide **antes** de consultar o serviço),
  `Transportador.Shuttle.cs` e `Transportador.Assigned.cs`.
- Logística (fome da artilharia) segue parqueada em
  `docs/implementar_logistica.md`.
