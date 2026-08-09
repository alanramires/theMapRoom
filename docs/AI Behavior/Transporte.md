# Transporte — contrato

Doutrina definida pelo autor. Onde o código divergir dela, o código está errado.

> **Função:** alavancar unidades para que cheguem mais rápido aos seus destinos.

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |
| ❓ | não conferido — a busca não fecha a questão |

**Rogue/Rebelde vira Rogue.** Este documento já fala em rogue; onde o código
ainda disser rebelde, ver `docs/refactor/ai_sem_plano.md`.

---

## 0. Notação

| termo | significado |
|---|---|
| **Courier** | transportador **com carga**, fazendo entregas |
| **Pickup** | transportador **vazio** |

Não existe "parcialmente vazio". ✅ O código já se comporta assim: o scanner de
pickup (`Shuttle`) só roda vazio, e encher a vaga livre no caminho não existe
(`BuildAttempts` retorna cedo com carga).

---

## 0.1 A ficha do papel

**Descrição do autor:**

> *"Eu sou o Táxi! Eu acelero a sua missão e quero chegar o mais rápido possível
> onde sou necessário!"*

**Lema:**

> ## O transportador serve a carga.
> ## Chegar cedo é entregar; o casco é a capacidade, e o destino nunca é meu.

| cláusula | o que ela produz |
|---|---|
| **serve a carga** | a promessa, o modo Hospital, não brigar, **não escolher o destino** — ele é do passageiro |
| **chegar cedo é entregar** | velocidade, aninhamento, desembarque no Tático do alvo |
| **o casco é a capacidade** | fusão negada, lotar o máximo, aninhar para estender alcance |

### Prioridade de sensor — MODAL

```text
Pickup (vazio)   Embarcar, Reposicionar, Enxergar, Detectar, Mirar,
                 Desembarcar, Transferir, Suprir, Capturar, Fundir

Courier (carga)  Embarcar, Reposicionar, Enxergar, Suprir, Transferir,
                 Desembarcar, Detectar, Mirar, Capturar, Fundir
```

**Por que `Embarcar` é o topo nos DOIS modos:** aninhamento. Um transportador
que sabe que não entrega sozinho **pede carona também** — e continua andando até
o ponto de encontro enquanto espera. O APC atravessa o canal dentro do navio; o
APC atravessa o território dentro do trem e **desembarca com o tanque cheio**.
Ver §7.

### ⚠️ Disciplina do modal — para os modos não se multiplicarem

Duas trocas de posição da lista têm naturezas diferentes:

```text
Mirar cai quando carregado           por VALOR — carregando, o tiro rende menos
Enxergar sobe antes de Desembarcar   por PRECONDIÇÃO — não se desembarca na névoa
```

A segunda é ordem de verdade: é **causal**. A primeira já é o **terceiro passo do
gate** (*"vale o meu turno?"*) — foi assim que o `CapturadorCombatente` se dissolveu
sem virar papel novo (`docs/AI Behavior/Capturador.md` §1).

**Regra:** modo só onde o motivo é **precondição**. Valor resolve no gate. Sem
isso nascem modos para Collapsing, para HP baixo, para combustível baixo.

E note que o `Suprir` **não precisa** de modo: `serviceRange = SameHexOrEmbarked`
faz a casa cair sozinha quando não há embarcado a servir (§14).

### Os três tipos, cada um com sua variante

| tipo | foco |
|---|---|
| **terrestre** | trilho ou rodas — reposicionamento de unidades em superfície |
| **aéreo** | helicóptero ou aeronave — travessia de terreno difícil ou do mar |
| **naval** | barco — travessia naval não só dos capturadores, mas do **restante do exército** |

---

## 1. Atribuição e planos

A IA **pode** atribuir transportadores a planos, mas **o ideal é que todos sejam
rogues**. O transportador atribuído a um plano usa isso de duas formas:

- gera uma **promessa de entrega**; ou
- dá **preferência**, quando vazio, a procurar unidades recém-compradas
  atribuídas àquele setor — para que cheguem ao destino mais rápido.

Ou seja: a atribuição influencia a **escolha de pickup**, não obriga rota.

---

## 2. Comportamento — a esteira

Um transportador pode prometer buscar unidades isoladas, mas **a promessa não
impede outros de embarcar no meio do caminho**.

> O passageiro mais antigo **assume o volante** e leva o transportador até onde
> quer ir. O carona (2º passageiro) pode tentar desembarcar junto se estiver no
> raio dele. O transportador procura o melhor envelope para ambos, mas a
> prioridade continua sendo o principal.

| regra | estado |
|---|---|
| quem embarca primeiro dita a rota (FIFO por turno de embarque) | ✅ `ResolvePrimaryPassenger` |
| larga esse passageiro no Tactical do objetivo **dele** | ✅ |
| o próximo da fila vira referência | ✅ — emergente: o principal é recalculado a cada decisão a partir de quem ainda está a bordo |
| promessa não bloqueia embarque de terceiros | ✅ |
| promessa persistente no `AIDesignatedMission` do transportador | ✅ `AIController.RidePromise.cs` |
| outros transportadores leem a promessa como **farol distributivo** | ✅ preferem passageiros ainda sem farol; se não houver alternativa, atendem o mesmo passageiro |
| com carga, o transportador herda o **destino da carga** no AI Plan Runtime | ✅ o courier publica `Transport` para o objetivo do passageiro; a promessa não recebe baixa quando a carga embarca no próprio casco |
| encher a vaga livre no caminho | ❌ |
| promessa reserva passageiro, vaga ou veículo | **não é doutrina** — promessa é farol, nunca lock |

A distribuição tem duas memórias que formam um único sinal: os faróis
provisórios da Fase 2 e a promessa persistida no `Mission Intent`. Um casco
prefere carga ainda não apontada por outro, mas o filtro sempre possui fallback
para os apontados. Assim três APCs podem convergir ao mesmo soldado quando ele é
a única demanda; havendo três soldados equivalentes, tendem a se distribuir.

⚠️ **Cascata registrada:** implementar "encher a vaga livre" sozinho **piora** a
fome do passageiro esquecido — o veículo fica ocupado mais tempo. Só entra junto
com a reserva de vaga ou com a pressão de compra.

---

## 3. Combate

**Transportador armado só entra em combate no modo Pickup (vazio).** Pode apoiar
a infantaria a tomar um setor, lutando no **Tático do capitão**, e depois volta
às suas tarefas.

Vale inclusive para o naval: **o transporte marítimo só atira vazio**, porque uma
rodada de combate significa que as aeronaves embarcadas não decolam — e caças e
bombardeiros são a defesa principal de uma armada.

Ele **consulta** a perna de combate de Assault ou Fire Support conforme foi
projetado, com **prioridade menor**.

| regra | estado |
|---|---|
| courier não ataca | ✅ **de facto** — `TryFindTransportCourierAttack` existe mas **não tem chamador**: é código morto a apagar |
| consulta a perna de Assault/Fire Support e volta à agenda | ❌ o roteador consulta o transporte **antes** dos papéis de combate e não volta |
| lutar no Tático do capitão | ❌ |

⚠️ **Conflito com o rascunho anterior deste mesmo documento.** A §"Quando o
Porta-Aviões atira" dizia que **com carga aérea** ele ainda atira, mas só contra
bombardeiro ("deles não dá para fugir"). Esta seção diz que **vazio é condição**.
A regra nova é mais restritiva e, sendo posterior, prevalece — mas o rascunho do
porta-aviões ficou abaixo, marcado, porque a justificativa dele (bombardeiro é
ameaça inescapável) não foi retirada, só sobreposta.

---

## 4. Embarque

Transportadores podem **embarcar em outros transportadores** — navios de
transporte, trens — para estender alcance poupando combustível.

> Um APC carregando um soldado que quer ir além-mar embarca no navio junto com
> ele; ou embarca num trem, e o trem assume a maior parte da viagem, liberando os
> dois no destino. Um APC com um soldado pode embarcar num Chinook, que leva a
> composição inteira.

Mas é **mais comum** ver o transportador usando o próprio combustível do que
aceitar uma carona aninhada.

| regra | estado |
|---|---|
| o sensor permite transportador como passageiro | ❓ `PodeEmbarcarSensor` valida o **transportador de destino**; não localizei bloqueio do lado do passageiro, mas também não confirmei que ele passa |
| a IA procura carona aninhada | ❌ nenhuma política busca isso |

---

## 5. Atração dos "quero carona"

Cada papel tem a sua atração — em geral quando o objetivo está **além do raio
Operacional** da unidade. Ela ergue a mão e **começa a contar o tempo**.

```text
espera ↑  →  pressão para atrair transportador rogue ↑
pressão alta  →  pressão para COMPRAR mais transportador ↑
```

Abertura comum: comprar Chinooks e infantaria na compra inicial, para os
Chinooks já começarem a levar soldados a destinos longos e a pressão nunca
acumular.

| regra | estado |
|---|---|
| tempo de espera vira urgência crescente, com teto | ✅ `AIController.RideWait.cs` |
| transportador que não alcança o passageiro não promete | ✅ |
| espera vira **pressão de compra** de mais transporte | ❌ |

---

## 6. Desembarque

### 6.1 A área de largada

Não é raio em hexes a partir do veículo. É **projeção invertida**, a partir do
passageiro:

> Teleporta a unidade para cima do objetivo, calcula o **Tactical dela** dali, e
> essa é a área.

A largada é boa quando o passageiro, no turno seguinte, fecha o objetivo com o
próprio movimento. Obus de 2 MP e infantaria de 3 MP não aceitam a mesma
largada. "0" é válido — em cima do alvo é o melhor caso, não exceção.

⚠️ Hoje é constante fixa e propriedade do **veículo**:
`TransportDropOffRange = 4`, `FireSupportDropOffRange = 3`, `AirDropOffRange = 2`.

### 6.2 As combinações

| | **sem atribuição** | **com atribuição** |
|---|---|---|
| **passageiro rogue** | alvo = capturável livre mais próximo | alvo = a coordenada da atribuição |
| **passageiro com plano** | alvo = o RepCell / capturável do setor dele | alvo = a coordenada da atribuição (pode ser outro prédio do plano, não só o RepCell) |

Nas quatro, o mecanismo depois é o mesmo: **Melhor LZ de Desembarque** para achar
onde largar. A atribuição muda a **âncora**, não o mecanismo. Espera-se que a IA
que atribui coordenadas saiba organizar isso — o desembarque não conserta
atribuição ruim, só a executa.

### 6.3 Tático ou Operacional

O transportador pode largar no **Tactical** ou no **Operational** do passageiro
em relação a onde ele quer ir. Verde = fecha no mesmo turno; azul = fecha no
turno seguinte.

**Dois passageiros:** tenta o principal primeiro (Tactical **ou** Operational) e
o carona **apenas no Tactical**, se houver envelope que atenda ambos.

| regra | estado |
|---|---|
| principal aceita Tactical ou Operational | ✅ dois passes de avaliação |
| carona só entra se a LZ atender ambos | ✅ `SearchMatching` maximiza entregues, depois minimiza rota |
| **carona limitado ao Tactical** | ❌ regra nova — hoje `BuildPassengerRouteLimits` aplica o mesmo teto a todos; o tier é do **passe**, não do papel do passageiro |

O que hoje separa principal de carona é outra coisa:
`ApplyOperationalDisembarkCapacity` — dois passageiros mirando o **mesmo** alvo,
e o alvo não estando sob pressão confirmada, o não-principal fica a bordo.

### 6.4 Casos especiais

| caso | regra | estado |
|---|---|---|
| **caças prontos** (transportador `isMaritime`, passageiro `isAircraft`) | **não** chama o Melhor LZ. Segue a rotina de reposicionamento e a aeronave decola em qualquer hex, de preferência na direção de onde foi designada | ❌ hoje `HasEmbarkedAircraft` apenas **alarga** o conjunto de LZs (devolve `null` e usa todos os caminhos), em vez de pular a consulta |
| **carga de superfície** (`isMaritime` + passageiro regular) | LZ normal | ✅ |
| **sem hex válido no Tactical** | tenta o Operational; sem isso também, **recusa a carona** | ❓ o `QueroCaronaService` tem o conceito (`isStranded`), mas a recusa por ausência de LZ não foi conferida |

> **Montanha.** Cidade na montanha herda as regras de montanha: sem infraestrutura
> de embarque/desembarque. Como desembarcar é sempre 1 MP e a cidade cobra 2 MP,
> ninguém embarca nem desembarca em montanha. *"Vai ficar assim por enquanto."*
> Larga a carga e ela vai a pé, ou usa estruturas na montanha.

### 6.5 Transporte não pousa em prédio capturável

O veículo **nunca** larga âncora em cima de um capturável: ocupa a célula que o
capturador precisa e trava a captura que ele veio viabilizar. Sem alternativa,
**sobe na iniciativa** para sair de lá primeiro na rodada seguinte.

❌ Nenhuma das duas existe. A iniciativa tem a regra espelhada só para o outro
lado (grupo 0 inclui *"blocker sobre o objetivo de outro capturador"*), e o teste
exige `CanSatisfy(targetData, UnitRole.Capturador)` — então um transportador
parado em cima do prédio **não é reconhecido como bloqueador**.

---

## 7. Transportador sendo transportado

O transportador aninhado que carrega transportador **lê os destinos das suas
cargas**, como faria transportando qualquer outra unidade. ❓ não conferido.

### Aninhar é como se ESTENDE alcance

Esta é a razão de `Embarcar` ser o topo dos dois modos, e ela faltava aqui.

> Você compra soldado e APC, mas a demanda é do outro lado do canal. Os navios
> não estão na praia e o APC está perto. O soldado pede carona e embarca. **O
> APC, sabendo que não vai conseguir cumprir a entrega, pede carona também** — e
> isso não o impede de andar até a praia. Quando o navio vier fazer o rendezvous,
> o APC embarca com o soldado dentro e cruza o canal.

Dois ganhos, e o segundo é fácil de esquecer:

```text
alcance      a cadeia entrega onde nenhum elo entregaria sozinho
combustível  quem viaja embarcado não gasta. O APC que atravessa o território
             dentro do trem DESEMBARCA COM O TANQUE CHEIO
```

**Nem todo elo existe ainda.** Um trem precisaria de um navio de tipo especial;
um navio talvez precisasse de um heli carrier. A cadeia é limitada pelas vagas
que as fichas declaram, não por regra de IA.

### ✅ O motor suporta — é capacidade não usada, não feature faltando

```csharp
// TurnStateManager.SupplyQueue.cs:2422
// Sync sprite so nested transporters show their transport sprite (not the default one).
```

E o portão do `PodeEmbarcarSensor` é `CanUseSlot(passenger, passengerData, slot)`
— compatibilidade de **vaga**, não *"o passageiro está carregando"*. Não há regra
barrando o APC cheio de entrar no navio.

O que falta é o **Courier pedir carona**, que é justamente o `Embarcar` no topo.

### ⚠️ O preço: a cadeia concentra risco, e o dano DESCE por ela

```csharp
// TurnStateManager.ScannerPrompt.cs:4071-4104
ApplyRatioDamageToEmbarkedRecursive(directlyHitUnit, ratio);
// Regra de sobrevida: dano proporcional em embarcados nao mata enquanto ...
    ApplyRatioDamageToEmbarkedRecursive(child, ratio);   // RECURSIVO
```

Tiro no casco aplica dano proporcional aos embarcados — e **desce a cadeia
inteira**. Soldado dentro do APC dentro do navio recebe proporcional de cada tiro
que o navio leva. (Há regra de sobrevida: o proporcional não mata.)

**Aninhar estende alcance E concentra risco**, e o preço é cobrado na moeda do
**capturador**: HP é o relógio dele (`Capturador.md` §0), então um tiro no navio
**atrasa o relógio de todos os capturadores dentro dele**.

Consequência para a política: a cadeia não é de graça. Quanto mais longo o
aninhamento, mais capturadores um único tiro desacelera.

---

### 7.1 Os quatro estados do transportador — e não há enum novo

Desenhado pelo autor em 2026-08-08. O `intent` continua `Transport` nos quatro; o
estado sai de **dois fatos que a unidade já publica**.

| `HasCargo` | `wantsRide` | estado | âncora — **para onde** | degrau | hoje |
|---|---|---|---|---|---|
| false | false | **pickup** | hex provável de LZ — está *procurando* passageiro | Evac / Pickup | ✅ existe |
| true | false | **courier** | a coordenada da carga | Courier / Delivery | ✅ existe |
| true | true | **need a lift** | a coordenada da carga, **longe ou atrás de travessia** | Courier / Delivery *far far away* | ⚠️ cai em courier |
| false | true | **ASAP** | **quem ele prometeu**, além-mar ou muito longe | Evac / Pickup *far far away* | ❌ inalcançável |

**O comportamento não é o mesmo nos quatro**, e a divisão cai da carga:

| estado | combate |
|---|---|
| **pickup** | **pode entrar em combate** — está vazio, não há o que proteger |
| **courier** | **cuida da carga.** Não é "ataca menos", é outro ofício |
| **need a lift** | carregado ⇒ mesma regra do courier |
| **ASAP** | ❓ vazio como o pickup, mas com urgência. Combate atrasa o resgate |

✅ **Isto já é o que o código faz** — conferido em 2026-08-08:

```text
vazio      Shuttle.cs:787   "Opportunistic attack (shuttle, empty) — max 1h deviation"   LIVE
carregado  Courier.Passengers.cs:325   cabeçalho de seção com CORPO VAZIO
           Courier.Attack.cs → TryFindTransportCourierAttack, 0 chamadores externos      MORTO
```

⚠️ **`CLAUDE.md` está desatualizado neste ponto.** Ele lista, como prioridade 3 do
courier, *"Opportunistic attack — near-dead enemies (HP ≤ 2), ≤2h route
deviation"*. Essa regra **não existe**: sobrou o comentário e o arquivo morto
(T7). Corrigir lá, ou alguém vai reimplementá-la achando que regrediu.

⚠️ **`need a lift` tem duas causas, e elas não são a mesma pergunta:**

```text
muito longe        orçamento — alcançaria, não neste horizonte    ReachableStrategic
requer travessia   topologia — não alcança nunca, a pé            NoCurrentRoute
```

Misturar as duas é a armadilha registrada no resumo (*"teste que se chama
estrutural e mede prazo"*). A segunda é `isStranded`, e é a que **não melhora
esperando** — nenhum turno a mais faz um APC atravessar água. Se as duas caírem no
mesmo score, o que precisa de navio perde para quem só precisa de tempo.

**O degrau não muda — a banda muda.** `wantsRide` não pede escada nova: ele diz
que a âncora está fora do envelope próprio, que é literalmente o que a palavra
significa. As duas linhas de baixo são as duas de cima com o alvo longe.

> **Para o transportador, `Embarcar` é o sensor número 1** — embarcar para
> alavancar, e para acelerar tanto a carona quanto a aproximação até o caroneiro.

E é por isso que **nenhum valor novo entra no enum**: `Delivery × Pickup` deriva
da carga, `need a lift × delivery` deriva do booleano. Dois bits, quatro estados.

⚠️ Isto só funciona com `wantsRide` **publicado e estável**. Enquanto ele era
resposta de pergunta alheia — `QueroCarona` é par a par, cada transportador com a
sua banda — o estado do transportador oscilava dentro do mesmo turno. A
publicação (§7.3) é pré-requisito do modelo, não faxina.

### 7.2 O pior caso — o resgate na ilha, ida e volta

Critério de aceitação do papel inteiro. **Percorre as quatro células, na ordem, e
volta.**

```text
ida    APC vazio sobe no navio                    ASAP
       navio cruza, desembarca o APC
       APC atravessa o território até o soldado   pickup
volta  APC carregado espera na praia              need a lift
       navio recebe, cruza, desembarca
       APC termina o resgate                      courier
```

**Ida e volta são a mesma viagem, e a única coisa que muda é um bit.** Se a volta
exigisse estado novo, a fatoração da §7.1 estaria errada — é este o teste dela.

O que o cenário cobra e a tabela não previa:

| # | o que falta | por que não está na tabela |
|---|---|---|
| A | o navio **desembarcando um transportador** | é `MelhorDesembarque` com transportador como carga: a LZ tem de largar o APC dentro do Tactical dele **em direção ao soldado**, não em qualquer praia |
| B | "vai até a praia" | `need a lift` precisa de âncora **própria** — o ponto de encontro, não o destino da carga. É a pergunta que o resumo chama de `TouchesComponent` (estação, helipad, convés e praia são a mesma) |
| C | cadeia de três elos | soldado → APC → navio. Pela doutrina resolve sozinho (publicação é barramento, nenhum papel chama outro), mas nunca foi exercitada |

⚠️ **Aposta de onde falha primeiro: o item A.** A regra oficial da LZ é *"terreno
visível ou já explorado"*, e no log de 2026-08-08 essa é a rejeição mais volumosa
do transporte inteiro — 394 ocorrências de
`REJECT reason=transporter_cell_not_visible_or_explored`. O cenário tem **duas**
atracagens. A praia perto do soldado provavelmente está explorada (o soldado é
nosso e revela em volta); a de chegada não necessariamente. **Conferir isso antes
de culpar a IA** — não é problema de modelo de carona, é a regra da LZ.

### 7.3 Os dois furos medidos — e eles são de naturezas diferentes

```text
need a lift   passa no gate, mas Embarcar só enxerga Tactical      →  a BANDA
ASAP          nunca passa no gate: wantsRide é sempre false        →  a PERGUNTA
```

**A banda.** `Embarcar` é o único degrau da escada **sem banda**:

```csharp
// AIController.TransportOperations.cs:206 — TryDecideNestedTransportEmbarkAction
int budget = Mathf.Max(0, unit.RemainingMovementPoints);
PodeEmbarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, budget, options);
if (options.Count == 0) return null;      // silencioso: nem hit nem miss no log
```

Todos os outros correm `Tactical → Operational → Strategic`. Este corre `Tactical`
e acabou. Ele só sabe dizer *"o navio está colado em mim neste turno"* — e nos
dois estados para os quais foi criado, o navio está a dois turnos. Cala, e o
`Strategic` do `Delivery`, que sempre acerta, leva a decisão. É a doutrina de novo:
*banda é parâmetro da unidade avaliada, nunca constante do papel.*

Traço do T4 em `docs/gamelog/log.md`, os dois lados na mesma corrida:

```text
Embarcar                          Tactical apenas, silêncio
[AI Reach][TransportDelivery:8]   Tactical:miss
                                  Operational:miss  budget=12
                                  Strategic:HIT     budget=120  cubic=24   ← o "far far away"
```

O estado `need a lift` **já existe no alcance**. Falta o sensor número 1 poder
responder na banda em que o estado vive.

**A pergunta.** `EvaluatePickupRideNeed` manda transportador vazio para o ramo
"emergência apenas" (sem carga → não resolve âncora de destino; não é capturador
terrestre → sem pergunta de captura). Ele devolve `wantsRide=false` **sempre**, e
como o aninhamento tranca em `!AIIsWaitingForRide`, `ASAP` não chega a ser
tentado. A linha não está errada — está inalcançável.

**❓ Em aberto, e decide o item 1 da ordem abaixo: a banda do `Embarcar` é de
quem?** Do transportador que sobe (*"eu alcanço o navio"*) ou do **encontro**
(*"nós dois nos encontramos em N turnos"*)? Os outros degraus perguntam do próprio
sujeito, mas carona é a única situação em que **os dois lados andam** — e o
`MelhorEmbarque` já responde banda de encontro em `rotaPax`
(`ReachableNow / Later / Strategic`). Talvez `Embarcar` deva ler dali em vez de
calcular a sua.

E a pergunta que vem colada: **se `Embarcar` ganhar banda, ele continua sendo o
número 1?** No Tactical sim — subir hoje bate qualquer coisa. No Strategic ele
passa a competir com um `Delivery` que também acerta lá. Ali "primeiro" precisa
querer dizer *ganha o empate*, não *responde antes* — senão um navio a cinco
turnos congela um APC que podia estar andando.

### 7.4 Ordem proposta

```text
1. banda do Embarcar     destrava need a lift   ← é o APC parado no mapa de hoje
2. a pergunta do vazio   destrava ASAP          ← é a perna de ida do cenário
3. âncora de praia       dá comportamento ao need a lift (item B da §7.2)
4. LZ em névoa           conferir antes de culpar a IA (item A da §7.2)
```

Só o 1 muda o que se vê na partida de hoje. Os outros três aparecem quando o
cenário da ilha existir no mapa.

### 7.5 ✅ Resolvido — um campo, dois significados

`intent=Transport` com alvo `#N` tinha **dois donos e a mesma forma**, e a
condição de baixa de um era a condição de início do outro:

```text
PROMESSA   "vou buscar #N"        acaba QUANDO #N embarca
HERANÇA    "levo #N até (x,y)"    COMEÇA quando #N embarca
```

`UpdateRidePromiseState` roda no setup da Fase 2, lia a missão herdada como
promessa, via a carga embarcada — **dentro do próprio transportador** — e dava
baixa. Resultado: o APC agia com `intent=None` todo turno e só reescrevia depois
de agir. E como o navio decide antes dele na iniciativa (`grp=2` contra `grp=4`),
o navio lia a ficha exatamente na janela em que ela estava zerada. *A missão
herdada existia; nunca no instante em que alguém olhava.*

O discriminador: embarcou em **outro** veículo é baixa (promessa cumprida por
terceiro, o caso para o qual a regra nasceu); embarcou em **mim** é o começo da
entrega.

### 7.6 ✅ Quando a missão é publicada — e por que não é depois de agir

> *"Não acha estranho registrar onde você quer ir **depois** de ter agido?"*

Sim, e a objeção estava certa. O invariante transacional protege o **tabuleiro**
— FoW, ocupação, recurso gasto, unidade marcada como agida. **Missão é intenção**,
e intenção só serve para uma coisa: coordenar. Publicada depois da ação, ela não
serve para nada, porque ninguém pôde ler enquanto decidia.

O corte cai na própria tabela da §7.1 — **o que é fato publica cedo, o que é
decisão publica no commit:**

| estado | âncora | é o quê | quando publica |
|---|---|---|---|
| **courier** / **need a lift** | destino da carga | **fato** — quem está a bordo e para onde vai não depende de decidir nada | setup da Fase 2, antes da 1ª seleção |
| **pickup** / **ASAP** | o encontro | **é a decisão** — não existe antes de ser tomada | commit pós-ação, como sempre |

```text
setup da Fase 2, para toda unidade viva, antes de ninguém agir:
    1. PublishInheritedMissionIntent   PARA ONDE eu vou
    2. PublishRideNeed                 CONSIGO chegar?
    3. UpdateRidePromiseState

na seleção de cada unidade, antes de DecideUnitAction:
    PublishInheritedMissionIntent      RE-CHECK
```

**Os dois bits que definem os quatro estados passam a ser publicados no mesmo
instante.** Antes, `wantsRide` era adiantado e uniforme e `intent` era atrasado e
desigual — os estados existiam em tempos diferentes.

⚠️ **O re-check na seleção não é redundante.** O setup fotografa o **início** do
turno, mas a carga muda **dentro** dele: o passageiro embarca na vez dele, e o
transportador pode decidir depois disso. Sem republicar, um APC que virou
`courier` no meio do turno decidiria — e seria lido — como o `pickup` que era.

**A linha da iniciativa passou a carregar o estado inteiro**, porque `target` e
`missao` são coisas diferentes e era fácil ler uma pela outra:

```text
[grp=4] APC#8 @ (49,-2,0) target=null missao=Transport(25,-2,0) carona=SIM(3t)
                          └ objetivo   └ ficha da unidade        └ o 2º bit
                            do PLANO
```

⚠️ **Nada disso conserta o embarque.** O comportamento já estava certo — a escada
gateia em `context.HasCargo`, estado vivo, então o transportador sempre soube que
tinha carga. Quem lia errado era **de fora**: o navio, a iniciativa, o Inspector.
É leitura que ficou certa, não decisão. `need a lift` continua caindo em
`courier` enquanto `Embarcar` não tiver banda (§7.3, passo 1 da §7.4).

---

## 8. EVAC

Levam a carga para a **retaguarda**, numa construção aliada controlada — ou o
**HQ**, caso a retaguarda esteja impossível.

Unidades `isMaritime` seguem a rotina delas: são capazes de prestar socorro aos
próprios embarcados (têm `isLogistic` e o Serviço do Comando as aciona).

| regra | estado |
|---|---|
| destino é construção aliada de reparo | ✅ `FindRepairConstruction` em `AIController.Transportador.Evac.cs` |
| HQ como último recurso | ❓ |
| marítimo com `isLogistic` não faz EVAC, trata a bordo | ✅ é a mesma regra do modo Hospital (§11) |

---

## 9. Capturar

Apenas se existir a skill **"captura construções"**. É raro e deve ter
**prioridade baixa**.

✅ o sensor já exige `skill.canCaptureConstructions` (`PodeCapturarSensor.cs:36`).
❓ a prioridade baixa dentro do transporte não foi conferida.

---

## 10. Mobilidade e iniciativa

**Não ligam para flanco, vanguarda e retaguarda**, mesmo que isso os coloque em
perigo. A função é ser a alavanca, não a cautela.

**Iniciativa: média-alta** — abaixo de Fire Support e Assault. Levar tropa e
desembarcar o quanto antes.

⚠️ A iniciativa hoje **não ordena por papel**. Os grupos são situacionais e o
transportador aparece em dois deles: grupo 0 (transportador atribuído com
passageiro formal ainda não agido) e grupo 2 (rogue vazio com candidato de
pickup no alcance). Não existe "abaixo de Fire Support e Assault".

---

## 11. Leitura de FoW

> A IA pode não saber onde estão unidades e hexes revelados, mas **sempre sabe
> onde estão todas as construções**. Caso contrário o jogo vira um eterno
> explora-voa-explora-voa. É o **único vazamento autorizado** do jogo, e é para
> os transportadores.

✅ de facto: a resolução de alvo lê `ConstructionManager.AllActive` direto, sem
filtro de visibilidade.

O transportador **não tem opção de desembarque** se voar para célula em FoW
fechado; se voar para *explored* ou *revealed*, segue a política de
`unitData > transport > allow disembark when...`.

✅ a LZ do transportador já exige visível-ou-explorado
(`allowTransporterCell = IsConfirmedVisibleOrExploredCellForAI`). ❓ o
chaveamento pela política da ficha não foi conferido.

---

## 12. Fusão

**Transportadores não fundem.** Mesmo muito avariado, um transportador ainda
transporta.

> Com 2 Chinooks eu levo 4 infantarias; um Chinook fundido leva 2, porque vira
> uma unidade nova.

❓ O autor registra que "já existe um bloqueio que impede transportadores courier
de fundirem". Procurei por `isTransporter` cruzado com merge/fusão em
`Assets/Scripts/Match/AI` e não localizei — o que **não prova ausência**: pode
estar no sensor de fusão ou no `TurnStateManager.Merge`. A conferir.

### O princípio geral: cada papel tem uma moeda, e a moeda decide fundir

A **mesma ação** é boa para o capturador e proibida para o transportador, e o
motivo é o que HP *significa* em cada um:

```text
capturador     HP é o RELÓGIO           GetCapturePower devolve HP
                                        → concentrar ACELERA → fundir GANHA
transportador  o CASCO é a capacidade    capacidade é por casco, não por HP
                                        → concentrar DESTRÓI → fundir PERDE
```

Não é exceção do transporte. É a regra que o próximo papel vai perguntar, e a
resposta dele sai da mesma conta.

---

## 13. Reparos

Qualquer construção **capturada que aceite pouso** serve. Unidades com carga
**não desembarcam a carga**: na construção ambos serão tratados (regra do Serviço
do Comando). ❓ não conferido.

### Limiar de reparo — quase zero, e o motivo cai do lema

**Transportador opera até o fim.** Um Chinook com 1 de HP leva a mesma coisa que
um com 10: HP não é velocidade para ele, é só a **distância até a morte**.
Reparar não compra operação nenhuma — compra sobrevivência, que é outro produto.

> *"Ficar 4 turnos parado esperando o HP recuperar é desperdício de missão."*

**A política é DADO, não código.** Os campos existem em `UnitData`:

```csharp
repairTriggerHpBelow     = 0    (0-9)     0 = nunca dispara por HP
repairTriggerAutonomyPct        (0-100)   o gatilho que importa: combustível
repairRecoverHpAbove     = 8    (1-10)    quando SAI do reparo
```

**⚠️ A ARMADILHA está no segundo número, não no primeiro.** `repairRecoverHpAbove`
é **8** por padrão: um transportador que parou por **combustível** fica preso até
o HP passar de 8. Zerar o gatilho não o solta — quem solta é o limiar de saída.
**Os dois precisam descer juntos**, senão os quatro turnos parados continuam
exatamente onde estavam.

| unidade | `repairTriggerHpBelow` | por quê |
|---|---|---|
| Chinook, APC, caminhão | 0 — só combustível | operam até o fim; capacidade não cai com HP |
| **Porta-Aviões** | ~5 | **exceção por externalidade, não por tamanho** |

O HP do porta-aviões não protege a capacidade **dele** — protege a
**disponibilidade da pista**. Se ele afunda, a asa aérea inteira perde a base.
É a única unidade do papel cuja morte tira operação de terceiros. Ver §16.

---

## 14. Logística

Unidades com `isLogistic` que sejam transportadoras **suprem os próprios
embarcados preferencialmente**. Se o transportador só atende o adjacente, segue a
rotina normal dele.

✅ é o **modo Hospital** (`AIController.Transportador.Hospital.cs`):
`serviceRange = SameHexOrEmbarked` nutre a bordo; `Adjacent1Hex` estruturalmente
não consegue e continua desembarcando.

---

## 15. Transferência

Unidades **hub** retornam a construções, navios de carga, estações de trem ou 18w
para buscar suprimento quando acabam.

❌ para a IA: a cadeia `PodeTransferir` existe (tiers Hub/Receiver, domínio de
operação, baldeação navio↔caminhão), mas a IA ainda não a opera.

---

## 15.1 Postura — defesa e Collapsing

**✅ RESOLVIDO pelo autor, na Marcha do Transportador (apêndice):**

> *Em colapso não se nega / a quem precisa embarcar:*
> ***nego apenas a viagem / que não pode mais ganhar.***
>
> *Avançar ou retirar, / é o mesmo movimento:*
> *a missão muda o vetor, / não muda o meu talento!*

**Nega-se a VIAGEM, não o embarque.** A posição inicial era *"em modo de defesa o
embarque seria negado"* — o que mataria o EVAC junto, e EVAC em colapso é
preservar relógio de capturador.

O registro do raciocínio, que continua valendo:

**Onde eu discordo, e por quê.** Pelo lema — *o transportador serve a carga* —,
em colapso a carga não some: ela **muda de direção**.

```text
EVAC                  §8 já existe. Levar ferido para a retaguarda é PRESERVAR
                      RELÓGIO de capturador (Capturador.md §0: HP é o relógio).
                      Em colapso é o trabalho mais valioso que existe, e negar
                      embarque o mata junto

redistribuir defesa   tirar defensor de setor quieto e pôr no ameaçado É
                      reposicionamento — a função central do papel, não exceção
```

*"Mover tropas não rola"* vale para **avançar**. Recuar e redistribuir são o mesmo
verbo com o vetor trocado, e são exatamente o que um exército em colapso precisa.

**Proposta:** negar embarque **para avanço**, não embarque. É o mesmo movimento
que a estrofe do cerco fez com o capturador — o lema não muda com a postura, muda
qual termo domina (`Capturador.md` §0).

**Continua ❓:** o que é exatamente a "função de tiro" em colapso — a arma do
próprio casco (`embarkedWeapons`), ou o transportador entrando na conta de
combate? A marcha diz *"eu atiro para abrir, não atiro por valor"*, o que sugere
a primeira, mas não fecha.

---

## 16. O Porta-Aviões é deste papel

Papel primário **Transportador**, com três pernas que ele **consulta**:

| perna | o quê |
|---|---|
| Fire Support | duas armas antiaéreas de longo alcance — tenta o tiro, volta à agenda |
| Logística | suporte |
| Transferência | é **Hub**: recebe do porto naval e repassa ao avião-tanque |

Segue a agenda courier/pickup deste documento. Não herda retaguarda, iniciativa
alta nem recusa de embarque do `FireSupport.md`: aquilo é de quem tem Fire
Support como papel primário.

### Rascunho anterior — sobreposto pela §3

> Mantido porque a justificativa não foi retirada, só sobreposta pela regra
> "só atira vazio".

| estado | comportamento |
|---|---|
| **vazio** | atira contra aeronaves no raio Tático |
| **com carga aérea** | atira **apenas** contra bombardeiro, porque dele não dá para fugir |
| **com carga aérea, sob caças** | ergue os caças — reconhecidamente tarde demais |

Com o convés cheio, gastar o tiro em caça é desperdício (caça se evita
manobrando); bombardeiro é ameaça inescapável. E a resposta contra caça não é a
arma do navio, é o próprio convés. "Erguer os caças tarde demais" admite que a
decisão de decolar deveria acontecer ao **detectar** o caça, não ao ser atacado.

---

## Pendências

| # | contrato | código hoje |
|---|---|---|
| T1 | a atribuição vale para todo passageiro | ⚠️ lida **só** no ramo rebelde — um call site, em `AIController.MelhorDesembarque.cs:1242`. Some no refactor de `ai_sem_plano.md` |
| T2 | rogue mira o capturável livre mais próximo | ⚠️ rogue de IA **com** QG ainda usa `TryResolveRogueCorridorCaptureTarget` — o funil do QG, doutrina derrubada na v6.1.2/6.1.3 e viva no transporte |
| T3 | passageiro com plano mira o capturável do setor | ⚠️ cai no `RepresentativeCell` quando não acha (`Courier.Passengers.cs:228`) **contra o comentário logo acima**: em setor já capturado o RepCell é a célula do próprio caminhão, e sai desembarque de distância zero |
| T4 | carona limitado ao Tactical | ❌ |
| T5 | o Melhor LZ consome a hotzone | ❌ hoje varre LZs por `CalcularCaminhosValidos` + `PodeDesembarcarSensor`. A **forma** já é a certa; a fonte de alcance é que não é o envelope |
| T6 | área de largada é banda do passageiro | ⚠️ constantes fixas do veículo (4 / 3 / 2) |
| T7 | courier não ataca | ✅ de facto — mas `AIController.Transportador.Courier.Attack.cs` é **código morto**, sem chamador. Apagar |
| T8 | consulta a perna de Assault/Fire Support com prioridade menor | ❌ o roteador consulta transporte antes do combate e não volta |
| T9 | naval só atira vazio | ❌ e **conflita** com o rascunho do porta-aviões, mantido na §16 |
| T10 | caças decolam sem Melhor LZ | ❌ `HasEmbarkedAircraft` alarga a LZ em vez de pular a consulta |
| T11 | com carga, herda o destino da carga no AI Plan Runtime | ✅ herdada, publicada no setup + re-check na seleção, e a baixa indevida corrigida — §7.5 e §7.6. ⚠️ ainda **write-only**: `TryResolveCargoDestinationAnchor` escava o passageiro primário em vez de ler a ficha do transportador |
| T12 | não pousa em capturável / sobe na iniciativa | ❌ nenhuma das duas |
| T13 | iniciativa média-alta, abaixo de FS e Assault | ⚠️ não existe ordenação por papel; os grupos são situacionais |
| T14 | espera vira pressão de compra | ❌ |
| T15 | carona aninhada (APC em navio/trem/Chinook) | ⚠️ `TryDecideNestedTransportEmbarkAction` existe e já subiu em campo. Mas **sem banda**: só enxerga Tactical, e sai calado quando não acha — §7.3 |
| T18 | os quatro estados (`HasCargo` × `wantsRide`) | ⚠️ dois rodam, `need a lift` cai em delivery, `ASAP` é inalcançável — §7.1 |
| T19 | `wantsRide` é fato publicado pela unidade | ✅ publicado uma vez por turno na Fase 2 contra a própria missão. ⚠️ os quatro pontos antigos ainda podem **levantar** (nunca baixar): a zona do Radar e o alvo reservado do capturador não estão na missão |
| T16 | transportadores não fundem | ❓ bloqueio não localizado — a conferir no sensor de fusão |
| T17 | hub busca suprimento | ❌ cadeia existe, IA não opera |

### Agrupadas por quem resolve

A tabela acima lista; esta agrupa. O critério não é severidade, é **de quem é o
trabalho** — porque metade destas pendências não é trabalho próprio.

| grupo | itens | quem resolve |
|---|---|---|
| **Já tem dono** | T1, T2 | o refactor de `docs/refactor/ai_sem_plano.md`. Somem junto com o funil do QG; não abrir frente própria |
| **Migração para o envelope** | T5, T6, T10 | um bloco só. Área de largada, fonte de alcance e dispensa de LZ para aeronave são a mesma mudança vista de três ângulos |
| **Independentes e pequenas** | T3, T7 | T7 é apagar arquivo morto. T3 é um `else` que contradiz o comentário acima dele — e **provavelmente já acontece em jogo sem ninguém notar** |
| **Precisam de decisão antes de código** | T8, T9, T13 | a ordem do roteador (transporte antes ou depois do combate), qual regra do porta-aviões sobrevive, e se iniciativa passa a ordenar por papel. Nada disso se decide lendo código |
| **Pergunta em aberto** | T16 | não é pendência até se confirmar que falta. Pode já existir onde não procurei |
| **Doutrina nova, sem base** | T4, T12, T14, T17 | trabalho de verdade, do zero |
| **A frente da travessia** | T11, T15, T18, T19 | um bloco só, e a ordem está na §7.4. Banda do `Embarcar` primeiro — é a única que muda o que se vê hoje |

---

# Apêndice — Marcha do Transportador

Escrita pelo autor em 2026-08-06. **Ela é a doutrina**, e vale a regra do
cabeçalho: **onde o código divergir de um verso, o código está errado.**

Ela **resolveu o §15.1** (postura em Collapsing) e confirma, em verso, coisas que
o código já faz e o doc não registrava:

| verso | o que ele confirma |
|---|---|
| *"Uma vaga prometida não será esquecida: / quem espera há mais tempo ganha força na corrida"* | `RidePromise` + a antiguidade **idempotente** da `FilaCarona` — o anti-fome |
| *"Se a névoa cobre a praia, eu peço observação: / primeiro abram meus olhos, depois abro o porão"* | `Enxergar` antes de `Desembarcar` é **precondição**, não valor — a disciplina do modal (§0.1) |
| *"Reparar não abre vagas... compra distância entre meu casco e morrer"* | o limiar quase-zero do §13, e o porquê |
| *"um tiro contra o casco atinge todos lá atrás"* | ✅ `ApplyRatioDamageToEmbarkedRecursive` — e desce a cadeia inteira (§7) |
| *"Para o capturador, dois relógios podem unir; / para quem transporta tropas, dois caminhos devem existir"* | o princípio da moeda (§12), comparando os dois papéis |

---

**[Introdução — metais em galope]**

Abre a estrada! / Limpa o corredor! / Quem ficou distante / chama o Transportador!

Um, dois! / Motor a girar! / Eu sou o táxi — / vim para buscar!

**[Primeira estrofe — a voz do papel]**

Não quero a cidade, / não quero o poder; / eu quero levar-te / aonde vais vencer.

Não sou teu destino, / não tomo teu lugar; / encurto a distância / que tens de enfrentar.

Se a tropa está longe, / eu chego primeiro; / o tempo da missão / é meu passageiro!

**[Refrão]**

Embarca! Embarca! / Não vamos esperar! / A carga é a missão, / meu dever é transportar!

Avança! Acelera! / O motor é o tambor! / Eu corto a distância — / sou o Transportador!

Por terra, céu ou mar, / não importa o setor: / onde a tropa é necessária, / chega o Transportador!

**[Segunda estrofe — Pickup]**

Se estou vazio, / eu busco a demanda; / escuto quem chama / do outro lado da banda.

Não fico esperando / a tropa me encontrar; / eu vou ao encontro / de quem precisa embarcar.

Escolho o caminho, / o ponto e o momento; / não é o mais perto — / é o melhor deslocamento.

Uma vaga prometida / não será esquecida: / quem espera há mais tempo / ganha força na corrida!

**[Chamada e resposta]**

— Quem está esperando? / — Eu vou buscar!

— Quem precisa de apoio? / — Eu vou chegar!

— Quem ficou distante? / — Pode me chamar!

— Qual é o meu trabalho? / — Reposicionar!

**[Terceira estrofe — Embarcar]**

Mas antes da estrada, / eu olho ao redor: / talvez outro transporte / me leve ainda melhor.

O Soldado no APC, / o APC no navio; / um carrega o outro / para atravessar o rio.

O trem corta a terra, / o barco cruza o mar; / o helicóptero sobe / onde ninguém pode passar.

Eu também sou passageiro / se isso encurta a missão: / não importa quem dirige, / importa a conexão!

**[Refrão]**

Embarca! Embarca! / Não vamos esperar! / A carga é a missão, / meu dever é transportar!

Avança! Acelera! / O motor é o tambor! / Eu corto a distância — / sou o Transportador!

Por terra, céu ou mar, / não importa o setor: / onde a tropa é necessária, / chega o Transportador!

**[Quarta estrofe — Courier]**

Quando estou carregado, / meu rumo está traçado; / a missão do passageiro / é o meu dever sagrado.

Não solto a tropa / em qualquer posição; / procuro o ponto exato / para entrar em operação.

Se a névoa cobre a praia, / eu peço observação: / primeiro abram meus olhos, / depois abro o porão.

Quem desembarca pronto / não perde a ocasião; / a melhor zona de entrega / é o começo da missão!

**[Quinta estrofe — serviço à carga]**

Se carrego aeronaves, / eu cuido da esquadrilha; / combustível, munição, / cada peça na mochila.

Reparo antes da entrega, / reabasteço para voar; / não percorri o mundo / para soltá-las sem lutar.

Eu sirvo a minha carga / enquanto a faço avançar; / não basta chegar viva — / ela precisa operar!

**[Ponte — casco e sobrevivência]**

Meu casco é capacidade, / não velocidade ou poder; / com um ponto ou com dez, / a mesma carga posso ter.

Reparar não abre vagas, / não faz a viagem correr; / reparar compra distância / entre meu casco e morrer.

Mas se eu for Porta-Aviões, / há mais para proteger: / se afundar a minha pista, / uma força deixa de existir!

**[Sexta estrofe — não fundir]**

Dois cascos machucados / ainda fazem duas viagens; / dois pontos de coleta, / dois caminhos, duas margens.

Se eu fundir dois veículos / para um só sobreviver, / ganho força num casco / e perco o dobro a fazer.

Para o capturador, / dois relógios podem unir; / para quem transporta tropas, / dois caminhos devem existir!

**[Refrão forte]**

Embarca! Embarca! / Não vamos esperar! / A carga é a missão, / meu dever é transportar!

Avança! Acelera! / O motor é o tambor! / Eu corto a distância — / sou o Transportador!

Por terra, céu ou mar, / não importa o setor: / onde a tropa é necessária, / chega o Transportador!

**[Sétima estrofe — combate]**

Eu não busco combate, / minha carga vale mais; / um tiro contra o casco / atinge todos lá atrás.

Mas se fecham a estrada, / se ameaçam o embarque, / eu cubro a retirada / e limpo o ponto de embarque.

Eu atiro para abrir, / não atiro por valor; / a batalha só me serve / se ela serve ao passageiro!

**[Oitava estrofe — Collapsing]**

Quando o exército avança, / eu empurro para a frente; / quando a linha recua, / mudo o rumo simplesmente.

Busco o ferido, / retiro o defensor; / salvo a artilharia / e protejo o supridor.

Em colapso não se nega / a quem precisa embarcar: / nego apenas a viagem / que não pode mais ganhar.

Avançar ou retirar, / é o mesmo movimento: / a missão muda o vetor, / não muda o meu talento!

**[Marcha crescente — três transportes]**

Sobre rodas! / Pela estrada!

Sobre trilhos! / Na jornada!

Pelo alto! / Sobre o chão!

Pelo mar! / No meu porão!

Cada domínio / tem seu condutor; / cada distância / tem um Transportador!

**[Refrão final — coro completo]**

Embarca! Embarca! / Não vamos esperar! / A carga é a missão, / meu dever é transportar!

Avança! Acelera! / O motor é o tambor! / Eu corto a distância — / sou o Transportador!

Se a tropa está distante, / eu encontro a solução; / se o terreno interrompe, / eu construo a conexão!

Embarca! Embarca! / Que o caminho já se abriu! / Soldado dentro do APC, / APC dentro do navio!

Por terra, céu ou mar, / não importa o setor: / onde a tropa é necessária, / chega o Transportador!

**[Coda — chamada e resposta]**

— De quem é a missão? / — Da carga!

— O que é a distância? / — Atraso!

— Para onde nós vamos? / — Onde somos necessários!

— E quem abre o caminho? / — O Transportador!

Um, dois! / Motor a rugir!

Um, dois! / Buscar e conduzir!

Eu não sou o destino, / escutem meu clamor:

**Eu sou o caminho mais curto — / eu sou o Transportador!**
