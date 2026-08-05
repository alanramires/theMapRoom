# Contrato de missões

> **Status: BRAINSTORMING, não especificação.** Nasceu de uma conversa de design,
> não de código escrito. As três missões descritas aqui — `RevelacaoDeContato`,
> `RevelacaoTerritorial` e `SpottingDeCobertura` — **não existem no runtime**, e o
> modelo delas ainda não foi validado em partida.
>
> O que **é** verdade verificada: a seção "O modelo que já existe" e as duas regras
> que o código impõe. O resto é desenho, e o próprio autor marcou o limite:
>
> > *"até encontrar um modelo, tudo o que estamos fazendo é brainstorming. Vou
> > jogar algumas partidas e a gente vai arrumando uma unidade por vez."*
>
> Leia como acordo de vocabulário e de camadas, não como fila de implementação.

---

## O que é uma missão

Uma missão é **um objetivo com várias maneiras de ser cumprido**, preso a uma
unidade até que ela o cumpra ou o perca.

Ela não é um papel (`UnitRole`), que é o que a unidade *é*. Não é uma postura
(`AIStance`), que é como o time se comporta. É o que **esta peça está fazendo
agora**, e é a única das três que sobrevive entre turnos preso à unidade.

```text
papel      Capturador          o que a peca E          nao muda no turno
postura    Ofensiva            como o TIME se comporta muda por leitura de mapa
missao     Capture -> hex 12,7 o que ESTA PECA faz     muda quando cumpre
```

---

## O modelo que já existe

```text
AIPlanRuntimeIntent            o VERBO — enum em UnitManager.cs
PendingAIDesignatedMission     a missao calculada, ainda nao comprometida
UnitManager.SetAIDesignatedMission / ClearAIDesignatedMission
```

Verbos de hoje, na ordem do enum:

```text
0 None          9 Transport
1 Capture
2 Pressure
3 FireSupport
4 AntiAir
5 AirSurveillance
6 Repair
7 Supply
8 Restock
```

Uma missão carrega mais que o verbo: `TargetCell`, `TargetUnitInstanceId`,
`TargetConstructionInstanceId` e `Sector`. O alvo pode ser célula, unidade ou
construção — e às vezes os três, porque *"o prédio no hex 12,7 do setor C"* é
uma coisa só vista de três ângulos.

### As duas regras que o código já impõe

**Valor novo entra sempre no fim do enum.** O save grava o inteiro; renumerar não
migra missão antiga, **troca** missão antiga. Está escrito no próprio
`AIPlanRuntimeIntent`.

**Missão só vira estado depois do compromisso.** `pendingAIDesignatedMissions`
guarda a intenção calculada; `CommitPendingAIDesignatedMission` a aplica **depois
que o batch retorna comprometido**. F11 e rollback não deixam missão fantasma.

É o invariante transacional do projeto aplicado a missão, e vale igual para toda
missão nova: *nada provisório publica verdade confirmada*, e uma missão gravada é
verdade confirmada sobre o que a peça está fazendo.

---

## As três camadas, aplicadas a missão

Uma missão **consome** serviços; ela não é serviço nenhum.

| camada | quem | trabalho |
|---|---|---|
| **serviço burro** | `UnitReachEnvelopeService`, os `Pode*`, cobertura de detecção | recebe unidade e célula, devolve **área**. Não conhece missão |
| **consumidor** | `Melhor*` | pergunta ao serviço uma vez por candidato e **agrega**: interseção, anotação, casamento 1:1 |
| **organizador** | `AIController.*` | escolhe a missão, escolhe quem vai, decide se vale o turno |

**O serviço nunca ordena.** A prova está no caso concreto abaixo: a mesma lista de
postos de observação é ranqueada ao contrário por uma unidade de chão e por uma
aeronave. Se o serviço ordenasse, ele precisaria saber que a segunda voa e orbita
um capitão — e aí já não é serviço, é doutrina.

---

## Missão nova nº 1 — `RevelacaoDeContato`

> *De onde eu descubro **quem** está naquele hex.*

### O caso que a justifica

O jogador humano começa a capturar um prédio da IA. `MelhorCaptura` avisa. A IA
desloca um soldado, que está atrás de duas florestas; o prédio está cercado de
floresta.

Os caminhos válidos dizem que a unidade **não ocupa o prédio**: alguém está lá.
Mas não dizem **quem**. Pode ser um soldado; pode ser um tanque deixado de
armadilha. As duas respostas pedem ações opostas.

### O que a IA sabe e o que ela não sabe

```text
VAZA      coordenada do predio    repCell e adjuntos. Sem isso, eixos e setores
                                  nao existem e a IA bate cabeca ao acaso
NAO VAZA  quem esta dentro        e por isso esta missao existe
```

O vazamento é **só a coordenada**. A IA sabe onde fica; continua obrigada a
chegar lá, e continua sem saber quem ocupa.

### O que o serviço devolve

Entrada: unidade, âncora (célula), tilemap, banda (`Tactical` ou `Operational`),
objetivo opcional.

Saída: postos de observação, **sem ordem**, cada um com

```text
celula C           onde a unidade para
custo ate C        do envelope
cobertura extra    CONJUNTO de celulas alem da ancora — nunca um numero
bonus de DPQ       o que aquele terreno da de defesa PARA ESTA unidade
grau               confirmado | hipotetico
```

**Cobertura extra é conjunto, não pontuação.** No instante em que vira `+12`,
some a informação que decide: para onde o leque aponta. Um morro que cobre doze
hexes de mar vazio às costas perde para a floresta que cobre sete hexes da
estrada por onde o inimigo vem. O número não sabe disso; o conjunto sabe.

**O grau existe porque o EV não vaza.** O envelope de alcance é canal legítimo —
o humano vê o mesmo overlay ao selecionar a unidade, e por ele descobre quais
células alcança e a que custo. O **EV** daquelas células, não: só quem já viu o
hex sabe. Então:

```text
confirmado   terreno do posto e do caminho conhecido/explorado
             "suba ali e voce ve quem esta no predio"        e PROMESSA
hipotetico   algum hex da conta esta no preto
             "SE houver relevo ali, voce veria"              e APOSTA
```

O grau é o que liga esta missão à `RevelacaoTerritorial` sem fundir as duas: não
dá para calcular posto sobre o preto, então revelar vem antes de observar.

> **Aviso.** Nada no código impede a trapaça. O tilemap de terreno está sempre lá;
> `explorado` controla **apresentação**, não acesso, e
> `ObservationCellService.TryResolveCellVision` responde o EV de qualquer célula a
> qualquer momento. A honestidade aqui é disciplina, não trava — por isso o grau
> tem que ser campo explícito. Implícito, a primeira otimização o apaga sem
> perceber que apagou a regra junto.

### A política que fica de fora

Tudo isto é do organizador e nada disso entra no serviço:

```text
"revelar o hex e um bonus aqui"                  peso entre as duas moedas
"melhor mandar o soldado do que gastar o tank"   custo de oportunidade do turno
"as vezes pode ser eu mesmo"                     o capturador aceita desviar?
coesao com o capitao                             criterio da aeronave, nao do chao
```

### A inversão que decide quem vai

> **O melhor revelador não é quem detecta melhor. É aquele cujo turno vale menos.**

O blindado longe do capitão, que se aproxima mas não alcança combate, tem turno
barato — ele não ia atacar de qualquer jeito. O tanque colado na frente tem turno
caro. O serviço não pode saber disso: ele não conhece missão, capitão nem valor
de ação. O organizador escolhe quase só por isso.

### Os dois rankings opostos

Mesma saída, ordens contrárias — é por isso que ordenar não é do serviço:

```text
unidade de chao   morro > floresta > campo aberto
                  intel adicional + bonus de DPQ de defesa

unidade voadora   coesao com o capitao domina
                  voar 5 casas revela muito e pode ser errado: dispersa a vanguarda
```

---

## Missão nova nº 2 — `RevelacaoTerritorial`

> *Avançar na névoa na direção que o solicitante quer ir, para revelar hexágonos.*

### O caso que a justifica

Apache e Chinook voando lado a lado sobre o mar, com uma faixa de praia à frente.
O Chinook está carregado e publica a intenção *"quero saber se aquela faixa tem
lugar para pouso"*. O Apache assume, voa até a névoa e revela com sua visão 3.
Achou planície, o Chinook vem, pousa e libera as unidades.

Se o Chinook fosse na frente, perderia o turno: **não se desembarca na névoa**.

> **Pré-requisito não cumprido.** O portão existe para desembarque —
> `TurnStateManager.Disembark.cs:666`, `showDisembarkAboveFog`. Para **pouso** não
> existe. Enquanto `PodePousar` não tiver o portão, esta missão não tem cliente no
> caso da praia: o Chinook pousa no preto e nada o impede.

### Por que ela é missão e não serviço

Revelar no preto **não é pontuável sem trapaça**: para saber quanto você revelaria
dali, você precisa do EV do terreno que está justamente no preto. As outras
consultas pontuam sobre coisa conhecida; esta pontuaria sobre a coisa que ela
existe para descobrir.

O que sobra de honesto é a **fronteira do conhecido** — quais células pretas
fazem divisa com o que já se sabe — e isso é geometria de conjunto, não linha de
visão. A direção vem do eixo; o alcance vem do envelope; a decisão de gastar o
turno vem do organizador.

---

## Missão nova nº 3 — `SpottingDeCobertura`

> *De onde eu ilumino **o conjunto de hexes** que a artilharia alcança mas não vê.*

### O caso que a justifica

A artilharia está atrás da montanha. O tático dela — banda `{3, 4}` — atinge quem
está do outro lado do morro. Mas aquele território está preto **ou explorado**, e
em nenhum dos dois casos há detecção. Ela alcança e não vê.

Alguém precisa subir o morro e revelar aquele grupo de hexes de vanguarda. Não
necessariamente o morro: um helicóptero sustenta o spotting, e **melhor** que a
infantaria — lá de cima ele ilumina um monte de hexes de uma vez.

### É a `RevelacaoDeContato` com âncora plural

Mesma pergunta, mesmo serviço, `|S|` diferente:

```text
RevelacaoDeContato    de onde eu detecto o conjunto S     |S| = 1   um predio
SpottingDeCobertura   de onde eu detecto o conjunto S     |S| = N   a banda da arma
```

O que muda é a política de quem consome: uma é admissibilidade (pega aquele hex,
sim ou não), a outra é **fração de cobertura** — quanto da banda eu ilumino.

Duas armadilhas no `S`:

**A banda é a da ARMA, não a do movimento**, e tem buraco de alcance mínimo. Um
obus `3-4` tem `{3, 4}`; hexes 0, 1 e 2 voltam vazios. Se o `S` chegar como raio,
o serviço erra o obus inteiro. Está em `contrato_envelope_alcance.md`.

**O spotter empresta o olho, nunca a trajetória.** Verificado no `PodeMirarSensor`:

| tiro | o que o observador avançado destrava |
|---|---|
| **reto** ([:354](../../Assets/Scripts/Sensors/PodeMirarSensor.cs#L354)) | alvo **fora da visão** do atirador, com **LdT já limpa**. Trajetória bloqueada morre num `continue` antes de o observador ser cogitado |
| **parabólico** ([:399](../../Assets/Scripts/Sensors/PodeMirarSensor.cs#L399)) | ignora trajetória; o observador é o que autoriza o tiro |

Então para artilharia de tiro **reto**, o `S` é a interseção da banda com o que a
LdT já alcança — senão o serviço posiciona um observador feliz da vida para ver
hexes que o canhão nunca acerta. Para **parabólico**, o `S` é a banda inteira, e
aí o spotting vale ouro.

### Ela só existe por causa do parto

> *"o território está preto **ou explorado**, logo sem detecção"*

Esse *"ou explorado"* é o dividendo da `v7.1.0`. Antes da separação, terreno
conhecido implicava unidade visível e esta missão não faria sentido — não haveria
o que spottar num hex que você já enxerga. É o quadrante **hex conhecido + sem
contato** virando missão.

### A primeira missão que termina por causa de outra peça

As outras duas acabam quando **quem carrega** chega e olha. Esta acaba por estado
do **beneficiário**:

```text
1. a artilharia nao tem alvo no tatico REVELADO nem no operacional REVELADO
   -> nao ha o que sustentar; ela precisa avancar

2. a vanguarda avancou e a retaguarda nao e mais aqui
   -> hora de pedir carona ou mover
```

O spotter pode estar sentado no morro cumprindo perfeitamente e ainda assim ser
liberado. O modelo já suporta o vínculo:
`PendingAIDesignatedMission.TargetUnitInstanceId` amarra o spotter à peça que ele
serve.

Consequência: esta missão precisa ser **reavaliada todo turno contra o
beneficiário**, não só verificada na chegada como a de contato.

### Coesão é restrição, cobertura é objetivo

O helicóptero é preferido aqui por iluminar muitos hexes de uma vez — e na
história do prédio o critério da aeronave era **não dispersar da vanguarda**. Não
é contradição: cobertura é o que se maximiza, coesão é o que limita. As duas são
do organizador, e é por isso que nenhuma das duas entra no serviço.

---

## A regra que impede as duas de duplicar trabalho

No early game a IA tem poucas peças e cada uma já tem missão. O capturador avança
pela névoa em direção ao prédio que ele **sabe** que existe, e revela o caminho
como efeito do próprio movimento — senão o turno da IA não anda.

Se o prédio revelar **vazio**, ele captura na rodada seguinte. Se revelar
**ocupado**, a rodada foi só mover, e a revelação de contato se cumpriu por
acidente dentro do avanço.

Daí a regra:

> **Missão de revelação só é atribuída quando ela gasta o turno de alguém.**
>
> Se o avanço que já ia acontecer revela, nenhuma missão foi criada. Ela se
> cumpriu, e o organizador apenas **verifica na chegada**.

Sem isso, o early game atribui missão de exploração ao capturador que já estava
indo, e a IA contabiliza duas missões onde existe um movimento. As missões são
**aninhadas**, não sequenciais: a de contato mora dentro do avanço e completa
sozinha.

Consequência de código: revelação de contato é **verificada na chegada**, nunca
escalonada na partida.

---

## Uma premissa que precisa de predicado

> *"os caminhos válidos não levam até lá, logo alguém está ocupando"*

Verdadeiro para prédio — você sabe que ali cabe unidade de chão, porque é prédio e
o alcance está do lado. **Não é verdadeiro em geral:** uma célula pode faltar no
caminho válido por ocupação, por terreno que exige chave que a unidade não tem,
ou por MP insuficiente. Três causas, um sintoma.

Se o gatilho da missão for *"não alcancei, logo tem alguém"* sem o teste de
terreno, a IA manda um soldado observar um prédio numa montanha onde ele
simplesmente não sabe entrar. O teste é barato; a ausência dele só aparece como
bug quando a unidade for alpina.

---

## Ocupante assumido

Detectar *quem está lá* exige assumir **o que** está lá: um fuzileiro colado no
prédio não detecta um furtivo dentro dele; um sonar não detecta infantaria.

O padrão é **unidade comum, na camada nativa do prédio, detecção básica do
`PodeDetectar`** — sem furtividade e sem especialização. Camada e perfil de
ocultação ficam como parâmetro para quem precisar depois; é o caso do submarino,
não o do prédio.

Isto confirma, por um caminho concreto, que a âncora é o par **(célula, camada)** e
não a célula sozinha: sem a camada o serviço assume superfície em silêncio e nunca
responde pelo helicóptero parado sobre a mesma cidade.

---

## Onde estas missões entram

```text
AIPlanRuntimeIntent
  ...
  9  Transport             <- ultimo de hoje
  10 RevelacaoDeContato    <- novo, NO FIM
  11 RevelacaoTerritorial  <- novo, NO FIM
  12 SpottingDeCobertura   <- novo, NO FIM
```

E o serviço burro que falta, do qual as duas dependem:

```text
existe   UnitReachEnvelopeService   onde eu posso ESTAR
existe   VisionCoverageService      o que eu REVELARIA (moeda: hex)
falta    ---                        o que eu DETECTARIA (moeda: contato)
```

**Não fundir os dois envelopes num serviço só** porque um consumidor quer os dois
números. São duas verdades — `PodeEnxergar` revela hexágono, `PodeDetectar` faz
unidade aparecer — e o consumidor pergunta duas vezes, uma para cada. Juntá-las no
serviço porque dá jeito é exatamente o erro que a `v7.1.0` levou dias para
desfazer.

---

## Leituras obrigatórias antes de implementar

| documento | por quê |
|---|---|
| `docs/AI Behavior/contrato_envelope_alcance.md` | banda, âncora e camada são parâmetro da unidade avaliada |
| `docs/arquitetura/acoes_transacionais.md` | missão só vira estado depois do compromisso |
| `docs/manual/01_principios_e_vocabulario.md` | decide onde uma regra pode morar |
| `CLAUDE.md`, seção das duas verdades | por que os dois envelopes não se fundem |
