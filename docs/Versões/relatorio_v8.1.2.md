# v8.1.2 — O dado existia; faltava estar publicado no instante em que alguém olha

Fechada em 2026-08-08. Antecessora: [`v8.1.1`](relatorio_v8.1.1.md).

---

## O fio do dia

Seis defeitos, e nenhum deles era informação faltando. Em todos, o dado existia
e o problema era **de publicação** — quem escreve, quando, e em que camada:

```text
exclusiveSlot          tinha QUATRO leitores. Todos perguntando "posso AGORA?"
                       contra quem já está a bordo — nunca contra as outras opções

missão herdada         escrita todo turno, apagada todo turno, no setup da Fase 2.
                       Existia; nunca no instante em que alguém olhava

wantsRide              não era fato da unidade: era resposta da pergunta alheia,
                       e quem perguntasse por último gravava a ficha

range da IA            pintado. Na sorting layer SFX, debaixo de 261 tiles de névoa

lista de transport slots   o drawer de array da Unity existia; o laço manual
                           simplesmente não o invocava

parcial=False          sem indicador na tela. "Liguei a névoa" e "liguei o parcial"
                       eram indistinguíveis olhando o jogo
```

> **Nenhum destes é bug de cálculo. Todos são bugs de publicação.**
> O valor estava certo, e chegava tarde, ou na camada errada, ou escrito por
> quem não deveria escrevê-lo.

Isso não fecha a travessia do canal — o APC continua sem embarcar no navio. Mas
tira do caminho seis coisas que faziam a investigação mentir.

---

## Frente 1 — O peso da vaga

`MelhorEmbarqueService`. O item que a `v8.1.1` deixou nomeado como próximo.

**A correção do resumo anterior:** ele dizia *"`exclusiveSlot` tem zero leitores
na IA"*. Tem quatro — `MelhorEmbarqueService:878`, `PodeEmbarcarSensor:60,166`,
`UnitManager:2091,2163`. Todos perguntam *"posso usar esta vaga agora?"* contra a
carga já embarcada. Com o casco vazio a resposta é sim para todos, e daí o
`MelhorEmbarquePassengers:4` do Chinook no T4: quatro ofertas simultâneas, o
motor recusando depois.

**A pergunta que faltava não é sobre o estado do casco — é sobre o que uma opção
custa às outras:**

```text
peso = unidades que TRAZ  −  assentos que DESLOCA
       ↑ conta carga aninhada    ↑ só assento que outro candidato disputa
```

Política escolhida pelo autor: **só a vaga que alguém quer**. Casco sem fila,
aninhar sai de graça — sem essa cláusula um APC sozinho recusaria um Chinook
vazio, que é regressão no cenário do canal.

A exclusividade morde dos **dois lados** (`UnitManager:2508` recusa tanto entrar
numa vaga exclusiva havendo carga alheia quanto entrar numa vaga comum havendo
exclusiva ocupada), e o termo cobra os dois.

Mora fora do laço de pares — depende de `(passageiro, slot)`, nunca da LZ. Custo
zero no trecho mais caro do transporte (2368 pares, ~790ms no Chinook).

### ✅ Validado por aritmética, não por impressão

O log do T4 fecha exato na opção vencedora:

```text
score = 100000 − dist·100 − routePenalty − rescueApproach + ajusteCarona + peso·300
101499 = 100000 −    0    −      1       −        0       +    1800      +  (−1)·300
```

`101799` era o score antigo. Os 300 que faltam **são** o termo, e o peso de #8 deu
`−1` — `traz=2, desloca=3`, exatamente a conta prevista para o slot exclusivo.

### ⚠️ E não adiantou — pelo motivo certo

O Chinook pegou o APC mesmo assim, e **não foi escolha**: a escada de bandas é
léxica. `Pickup[Tactical]` acertou em #8 (colado, `dist=0`) e devolveu; os
soldados estavam no balde `Operational` e nunca disputaram.

> **O peso ordena DENTRO de uma banda. Não atravessa fronteira de banda.**
> −300, −3000 ou −30000 dá no mesmo, porque a disputa não acontece.

Registrado, e é o padrão que a `v8.1.0` já tinha nomeado: *o comportamento não
está esquecido, está depois de um `return`*.

### ⚠️ Metade do termo nasceu dormente

O autor removeu o slot `APC` do Chinook no mesmo dia (frente 6). Com isso o
catálogo ficou com **zero** slots exclusivos, e a metade *"desloca"* passa a ser
sempre 1. A metade *"traz"* continua viva e é a que serve o canal — no navio
(`Cargo`, cap 2, não exclusivo), APC carregado dá `+1` contra soldado `0`.

Não é trabalho perdido: o modelo está certo e acorda sozinho no dia em que outro
slot exclusivo nascer. Mas é honesto dizer que metade dorme.

---

## Frente 2 — Publicação: `wantsRide` e a missão herdada

A frente maior do dia, e a que o autor conduziu.

### 2.1 A dança do `quero carona`

O autor relatou *"uma dança terrível no Inspector que não faz sentido"*. A causa,
em `AIController.RideWait.cs:89`:

```csharp
if (unit.IsEmbarked || !rideNeed.wantsRide)
    unit.ClearAIRideWait();
```

`ApplyRideWaitStamp` só rodava **quando algum transportador avaliava a unidade**.
E `QueroCarona` é pergunta **par a par** — cada transportador com a sua banda, o
seu horizonte, o seu tier. Dois transportadores davam respostas opostas sobre a
mesma unidade no mesmo turno, e o último a perguntar gravava a ficha.

Não era cosmético: **o degrau de aninhamento tranca nessa flag**, então a
capacidade de um APC subir no navio dependia de qual transportador o tinha
avaliado antes na ordem de iniciativa.

**O defeito era a BAIXA, não a subida:**

> Quem responde "sim" sabe de um caminho real. Quem responde "não" só sabe que
> **ele** não serve.

Correção em duas partes:

1. `PublishRideNeed` — escritor único, uma vez por turno, no setup da Fase 2,
   contra a **própria** missão. Reusa `EvaluatePickupRideNeed` de propósito:
   esta fatia muda *quem escreve e quando*, não o conteúdo da resposta.
2. `ApplyRideWaitStamp` ficou **monótono dentro do turno** — pode levantar, nunca
   baixar.

### ⚠️ Por que não cortei os quatro pontos antigos

Meu plano original era deixar a publicação como escritora única. Fui conferir e
ela é **mais estreita** que os quatro pontos que substituiria:

```text
Vigilancia:989       explicitTarget = zona de vigilância    ← publicação não conhece
Capturer.Embark:261  explicitTarget = alvo reservado        ← mais rico que o do plano
Assault:442          objetivo atribuído
TransportOps:899     destino da carga
```

Um Radar cairia no ramo "emergência apenas" e sairia da fila calado. É o critério
do próprio projeto: *quando a regra nova cobre menos casos que a que substitui, a
incompleta é a nova*. Daí o corte monótono, que é menor e não derruba peça
nenhuma.

### 2.2 Um campo, dois donos

`intent=Transport` com alvo `#N` tinha **dois significados com a mesma forma**, e
a condição de baixa de um era a condição de início do outro:

```text
PROMESSA   "vou buscar #N"        acaba QUANDO #N embarca
HERANÇA    "levo #N até (x,y)"    COMEÇA quando #N embarca
```

`UpdateRidePromiseState` roda no setup da Fase 2, lia a missão herdada como
promessa, via a carga embarcada — **dentro do próprio transportador** — e dava
baixa. A linha que a gente vinha lendo como *"o #8 desistiu de buscar o #1"*:

```text
[T4][Promessa] #8 baixa a promessa a pax=#1: passageiro embarcou.
```

não era isso. Era o APC **perdendo a própria missão de entrega**, todo turno.

Discriminador: embarcou em **outro** veículo é baixa; embarcou em **mim** é o
começo da entrega.

### 2.3 Publicar antes de agir — a objeção do autor, e ela estava certa

> *"Não acha estranho registrar onde você quer ir **depois** de ter agido?"*

Sim. O invariante transacional protege o **tabuleiro** — FoW, ocupação, recurso
gasto, unidade marcada como agida. **Missão é intenção**, e intenção só serve
para coordenar. Publicada depois da ação, não serve para nada.

O corte cai na tabela dos quatro estados — **o que é fato publica cedo, o que é
decisão publica no commit:**

| estado | âncora | é o quê | quando publica |
|---|---|---|---|
| courier / need a lift | destino da carga | **fato** | setup da Fase 2 |
| pickup / ASAP | o encontro | **é a decisão** | commit pós-ação |

```text
setup da Fase 2, antes de ninguém agir:
    PublishInheritedMissionIntent   PARA ONDE eu vou
    PublishRideNeed                 CONSIGO chegar?
    UpdateRidePromiseState

na seleção de cada unidade:
    PublishInheritedMissionIntent   RE-CHECK
```

**O re-check não é redundante:** o setup fotografa o *início* do turno, mas a
carga muda **dentro** dele. Sem republicar, um APC que virou `courier` no meio do
turno decidiria — e seria lido — como o `pickup` que era.

E fecha o ponto do autor com precisão: o navio decide em `grp=2`, o APC em
`grp=4`. **O navio olhava a ficha do APC exatamente na janela em que ela estava
zerada.** *"Senão o navio de transporte não vai conseguir ler"* — ele
literalmente nunca poderia.

### Os dois campos na ficha

```text
Ai Wants Ride       bool   publicado pela unidade, contra a própria missão
Ai Ride Wait Turns  int    contagem materializada
Ai Ride Wait Since  int    o carimbo, continua sendo a verdade da antiguidade
```

O terceiro já existia e enganava: esperando desde o T1, no T4 ele mostra **1**,
que se lê como *"esperei 1 turno"* quando são 3. `HP` e `Autonomia` já vinham com
fração; movimento e espera não.

A linha da iniciativa passou a carregar o estado inteiro:

```text
[grp=4] APC#8 @ (49,-2,0) target=null missao=Transport(25,-2,0) carona=SIM(3t)
                          └ objetivo   └ ficha da unidade        └ o 2º bit
                            do PLANO
```

⚠️ **Nada disso conserta o embarque.** O comportamento já estava certo — a escada
gateia em `context.HasCargo`, estado vivo. Quem lia errado era **de fora**.

---

## Frente 3 — O observador do FoW em AI vs AI

Sintoma: *"a unidade está selecionada e piscando… cadê o range map?"*

A caçada eliminou, em ordem: MP zerado, gate de IA na pintura, preset errado,
observador na IA errada. **Não havia gate nenhum** — `PaintSelectedUnitMovementRange`
não pergunta de quem é a unidade, e o próprio log do autor prova que pintou:

```text
[RangeCache] MISS - reason: empty key | unit=APC_T1_U8 mp=6 fuel=53 rev=354
[FSM] Estado: Neutral -> UnitSelected
```

A resposta veio de uma linha do log dele:

```text
[FoW][Estado][ON] ... debugLigado=True parcial=False totalWar=True
                                       ^^^^^^^^^^^^^
```

```csharp
showHumanTurnTools = playerTurn || (debugFogOfWarPartial && showCursorAboveFog);
                     └ false (turno da IA)   └ FALSE
→ rangeRenderer.sortingLayerName = "SFX", sortingOrder = 0
```

**O range não estava desligado — estava debaixo da névoa.** Em Total FoW o
overlay preto cobre o mundo a partida inteira, e `nevoaTiles=261` de
`celulasTabuleiro=281`.

### A correção: derivar, não configurar

`debugFogOfWarPartial` tinha **uma única** atribuição `= true` em todo o projeto
(o comando de debug `FOW PARTIAL`), e `SetFogOfWarDebugEnabled` o zerava
incondicionalmente. Ou seja: `FOW ON` derrubava o parcial.

```csharp
private bool IsFogPartialObserverActive =>
    debugFogOfWarPartial                                    // override manual
    || (players != null && players.Count > 0 && !AnyHumanPlayerExists());
```

> **Sem nenhum humano na partida não existe a quem fixar a câmera.** Somos
> observadores, e o único ponto de vista possível é o de quem está jogando.

O conceito já estava no código, em comentário de `ShouldPresentActiveActionToLocalObserver`
— *"sem jogador humano local, a partida AI vs AI possui um observador neutro"*.
Faltava alguém ligá-lo. Mesma forma da facção sem QG: o fato vem do mundo, não de
um campo que alguém tem que lembrar de marcar.

O log ficou honesto sobre os dois: `parcial=True(manual=False)`.

### A auditoria dos quatro cenários

Feita contra `TryResolveFogPresentationSlot`. Os quatro batem com a expectativa do
autor:

| cenário | observador | rangeMap + linha |
|---|---|---|
| local vs [AI/remoto] | humano local fixo, inclusive no turno alheio | SFX, sob a névoa ✅ |
| local vs local vs [AI/remoto] | ninguém artificial; não-local cai na PrivacyCurtain | idem ✅ |
| [AI/remoto] vs [AI/remoto] | a IA da vez | ⚠️ era aqui — corrigido |
| local vs AI vs AI | um único humano local, mesmo caminho do 1º | ✅ |

---

## Frente 4 — A lista de transport slots do Inspector

O autor: *"eu perdi a capacidade de edição no inspector"*. **Eu disse que nunca
tinha existido. Estava errado.**

`639c02e` ("Add configurable disembark location data") trocou:

```diff
- DrawIfExists(serializedObject.FindProperty("transportSlots"), "Transport Slots");
+ SerializedProperty size = transportSlots.FindPropertyRelative("Array.size");
+ for (int i = 0; i < transportSlots.arraySize; i++) ...
```

A linha antiga era um `PropertyField` no **array inteiro** — o drawer padrão da
Unity, que traz `+`, `−`, arrastar e o *Delete Array Element*. Desenhar elemento
por elemento nunca invoca esse drawer, e os controles somem junto. Sobrava o
campo `Size`, que só corta do fim.

A intenção do commit era só embrulhar a seção num foldout. Nada nele é sobre
slots de transporte — foi efeito colateral.

Restaurado, com o porquê e o número do commit no código, para não voltar.

---

## Frente 5 — Doutrina: os quatro estados do transportador

`docs/AI Behavior/Transporte.md`, §7.1 a §7.6. Desenho do autor.

O `intent` continua `Transport` nos quatro; o estado sai de **dois fatos que a
unidade já publica**:

| `HasCargo` | `wantsRide` | estado | âncora | combate | hoje |
|---|---|---|---|---|---|
| false | false | **pickup** | hex provável de LZ | **pode combater** | ✅ |
| true | false | **courier** | a coordenada da carga | **cuida dela** | ✅ |
| true | true | **need a lift** | a carga, longe **ou atrás de travessia** | como courier | ⚠️ cai em courier |
| false | true | **ASAP** | **quem ele prometeu** | ❓ | ❌ inalcançável |

**O degrau não muda — a banda muda.** `wantsRide` não pede escada nova: ele diz
que a âncora está fora do envelope próprio. As duas linhas de baixo são as duas de
cima com o alvo longe.

> **Para o transportador, `Embarcar` é o sensor número 1** — embarcar para
> alavancar, e para acelerar tanto a carona quanto a aproximação até o caroneiro.

**Nenhum valor novo entra no enum.** Dois bits, quatro estados.

### O pior caso — o resgate na ilha

Critério de aceitação do papel inteiro, e ele **percorre as quatro células, na
ordem, e volta**:

```text
ida    APC vazio sobe no navio                    ASAP
       navio cruza, desembarca o APC
       APC atravessa o território até o soldado   pickup
volta  APC carregado espera na praia              need a lift
       navio recebe, cruza, desembarca
       APC termina o resgate                      courier
```

**Ida e volta são a mesma viagem, e a única coisa que muda é um bit.** Se a volta
exigisse estado novo, a fatoração estaria errada — é este o teste dela.

### Os dois furos, de naturezas diferentes

```text
need a lift   passa no gate, mas Embarcar só enxerga Tactical    →  a BANDA
ASAP          nunca passa no gate: wantsRide é sempre false      →  a PERGUNTA
```

`Embarcar` é o **único degrau da escada sem banda** — `budget =
RemainingMovementPoints`, e acabou. Todos os outros correm `Tactical →
Operational → Strategic`. Ele só sabe dizer *"o navio está colado em mim neste
turno"*, e nos dois estados para os quais foi criado o navio está a dois turnos.
Cala, e o `Strategic` do `Delivery` — que sempre acerta — leva a decisão.

O traço do T4 mostra o estado existindo no alcance e o sensor calado ao lado:

```text
Embarcar                          Tactical apenas, silêncio
[AI Reach][TransportDelivery:8]   Tactical:miss
                                  Operational:miss  budget=12
                                  Strategic:HIT     budget=120  cubic=24
```

### ⚠️ `CLAUDE.md` está desatualizado

Ele lista, como prioridade 3 do courier, *"Opportunistic attack — near-dead
enemies (HP ≤ 2), ≤2h route deviation"*. Essa regra **não existe**:

```text
vazio      Shuttle.cs:787   "Opportunistic attack (shuttle, empty) — 1h deviation"   LIVE
carregado  Courier.Passengers.cs:325   cabeçalho de seção com CORPO VAZIO
           Courier.Attack.cs → TryFindTransportCourierAttack, 0 chamadores           MORTO
```

A doutrina do autor (*pickup pode combater, courier cuida da carga*) **já é o que
o código faz**. Quem mente é o arquivo de instruções. Não corrigido — decisão do
autor.

---

## Frente 6 — Trabalho do autor

- **`AR Chinook.asset`** — slot `APC` exclusivo removido (13 linhas). O helicóptero
  virou infantaria-only, e com isso o catálogo ficou sem nenhum `exclusiveSlot`.
  **Um único asset pagava por uma dimensão inteira de raciocínio da IA.**
- **Presets** — `AIPreset_Gastadora` → `AIPreset_Gulosa` (exibido "Padrão"), e
  **`AIPreset_Dificil` novo**, derivado dela com `respeitarListaBanida: 1` e
  `projetarProducaoInimiga: 1`. O rename `Intel → AirSurveillance` dentro deles é
  reserialização do `f631fe9`, não trabalho de hoje.
- **`Hot Seat.asset`** (catálogo de construção) — `fieldEntries` de uma
  `Hidrobase` em `(26,6,0)`, montando o cenário naval.
- **Cenas** — `Hot Seat 0` com unidades novas (churn de teste); `Battle Map 1 -
  Ground` reserializado pela partida AI vs AI rodada nele.

---

## O que NÃO terminou

**O APC continua sem embarcar no navio.** É o item que abriu e fechou o dia sem
sair do lugar. Tudo que foi corrigido tirou ruído do caminho; nada mudou a
decisão.

```text
1. banda do Embarcar     destrava need a lift   ← o único que muda o que se vê hoje
2. a pergunta do vazio   destrava ASAP          ← a perna de ida do cenário da ilha
3. âncora de praia       dá comportamento ao need a lift
4. LZ em névoa           conferir antes de culpar a IA
```

**O item 1 precisa de uma decisão do autor antes do código:** a banda do
`Embarcar` é **do transportador** que sobe (*"eu alcanço o navio"*) ou **do
encontro** (*"nós dois nos encontramos em N turnos"*)? Os outros degraus perguntam
do próprio sujeito, mas carona é a única situação em que **os dois lados andam** —
e o `MelhorEmbarque` já responde banda de encontro em `rotaPax`
(`ReachableNow / Later / Strategic`).

E a pergunta colada: **se `Embarcar` ganhar banda, continua sendo o número 1?** No
Tactical sim. No Strategic ele passa a competir com um `Delivery` que também
acerta lá — ali "primeiro" precisa querer dizer *ganha o empate*, não *responde
antes*, senão um navio a cinco turnos congela um APC que podia estar andando.

### Outros pendentes

- **O `parcial=True` não foi visto em jogo.** A derivação compila e a lógica está
  auditada, mas ninguém rodou uma partida AI vs AI depois da mudança.
- **A janela do range é curta.** Mesmo com o observador certo, o range é pintado
  na seleção e apagado quando a unidade anda. As duas pausas do F11 caem **fora**
  dessa janela (`Preparando próximo batch` é antes da seleção; `Executando batch`
  é depois do movimento). Falta um ponto de parada entre `SetSelectedUnit` e a
  execução — proposto, não decidido.
- **O painel mostra `Move: 3`**, o atributo, não o restante. Foi essa lacuna que
  fez "0 de movimento" parecer "range quebrado". `HP` e `Autonomia` já vêm com
  fração.
- **Exclusividade × vaga livre.** Carga *e* assento livre com alguém na fila é o
  caso que a tabela de quatro estados não resolve — os estados são exclusivos e a
  unidade está em dois. Marcado com ❓ no código; a carga ganha por ora.
- **A missão herdada continua write-only.** `TryResolveCargoDestinationAnchor`
  escava o passageiro primário em vez de ler a ficha do transportador. É a fatia
  2, e agora ela é possível porque a ficha finalmente está certa no instante da
  leitura.

---

## Onde eu errei, e o que a medição desmentiu

Isto vale mais que as frentes, porque é o que a próxima sessão repete.

**1. Concluí ausência a partir de busca vazia.** O autor disse que a lista de
slots era editável antes. Rodei `git log -S`, achei só a *adição* do laço manual,
e afirmei que os botões nunca tinham existido. Não abri o diff para ver **o que
aquele laço substituiu**. Era um `PropertyField` no array inteiro. *Ausência não
se prova com busca vazia — se prova abrindo o diff.*

**2. Pedi confirmação de estado ao humano em vez de ler o estado.** Perguntei
*"o preset é FogOfWarTotal?"* e *"`ActiveSlotId == aiPresentationSlot`?"*. Ele
respondeu "sim" para os dois, de boa-fé, e eu segui por um caminho errado. A
resposta estava no log dele o tempo todo: `parcial=False`, impressa direto do
campo. **Quando existe uma linha de log que imprime o campo, pedir sim/não é
trocar medição por lembrança.**

**3. Defendi o invariante onde ele não se aplicava.** O autor achou estranho a
missão ser registrada depois de agir; eu respondi "é o invariante transacional" e
tratei isso como fim de discussão. O invariante protege o **tabuleiro**. Missão é
intenção, e intenção publicada depois da ação não serve para a única coisa que
ela faz. Ele estava certo e eu levei duas voltas para ver.

**4. Meu primeiro corte da publicação era mais estreito que o que substituía.**
Ia deixar `PublishRideNeed` como escritora única e derrubaria o Radar da fila. Só
não aconteceu porque fui conferir os quatro pontos antes de cortar.

**5. Errei a ordem antes de rodar, e o compilador não pegaria.** Contei a demanda
por slot **antes** da poda de `acceptedPassengers`, o que faria os `DESCARTA`
virarem concorrentes fantasma encarecendo a vaga exclusiva contra ninguém. É o
padrão que a `v8.1.0` já registrou: *o conteúdo estava certo e a ordem estava
errada.*

---

## Armadilhas novas

| armadilha | lição |
|---|---|
| **pedir estado ao humano em vez de ler o log** | ele responde de boa-fé o que acredita. `parcial=False` estava impresso, vindo direto do campo. Se existe linha que imprime, ela é a fonte |
| **campo booleano sem indicador é indistinguível de si mesmo** | "liguei a névoa" e "liguei o parcial" pareciam iguais no jogo. Um estado que só aparece no log só existe quando alguém roda o comando que o imprime |
| **overlay não some, desce de camada** | `sortingLayerName = playerTurn ? "FogOfWar" : "SFX"`. Procurei um `if` que desligasse a pintura; não havia. O range estava pintado embaixo de 261 tiles pretos |
| **um único asset pagando por uma dimensão inteira** | o Chinook era o **único** `exclusiveSlot` do catálogo, e obrigava serviço, sensores, motor e score a carregar o caso. Contar os portadores antes de generalizar |
| **a condição de baixa de um significado é a de início do outro** | `intent=Transport → #N` servia a promessa e à herança. Quando um campo tem dois donos, procure a transição onde um termina exatamente onde o outro começa |
| **"não" alheio não é informação sobre mim** | quem responde sim sabe de um caminho real; quem responde não só sabe que **ele** não serve. Publicação alheia pode levantar, nunca baixar |
| **o degrau que não loga é o que decide** | `Courier`, `Delivery` e `Pickup` logam hit/miss com motivo. `Embarcar` sai por `options.Count == 0` calado — e foi ele que segurou o APC em duas sessões |
| **banda congelada em Tactical** | mesma doença das constantes fixas, num degrau novo. *Banda é parâmetro da unidade avaliada, nunca constante do papel* — e `Embarcar` é o único sem banda |
