# v7.1.0 — Enxergar e detectar deixaram de ser a mesma pergunta

Fecha o dia 2026-08-04, a partir da `v7.0.4`.

O dia começou no degrau planejado — levar o FOW cozido ao Melhor Visão — e
descarrilou para algo maior quando o autor olhou o EWACS em jogo:

> *O EWACS revelando tudo é um erro. Estamos fazendo FoW da maneira incompleta.*

A partir daí a versão inteira virou uma separação: **quem revela hexágono** e
**quem faz unidade aparecer** eram a mesma função, com as duas respostas caindo
no mesmo balde. O resto do dia foi desfazer essa fusão, e quase todo o custo veio
de eu não ter percebido o tamanho dela.

---

## 1. A descoberta que organiza tudo

Um submarino inimigo detectado a 7 hexes fazia o mar aparecer a 7 hexes. Um
EWACS com radar aéreo de alcance 7 e visão de superfície 3 revelava terreno a 7.
Isso não é névoa parcial: é **inteligência falsa**. O jogador passava a conhecer
a costa, o porto e a praia porque um sonar ouviu um motor.

A prova de que as duas respostas são independentes tem quatro quadrantes, e
todos existem:

| | hex conhecido | contato detectado |
|---|---|---|
| soldado comum | sim | sim |
| EWACS a 7 | **não** | sim |
| sniper ao lado | sim | **não** |
| nada | não | não |

Nenhuma das duas se deriva da outra. Logo não podem morar no mesmo conjunto —
e `KnownCells` era exatamente esse conjunto.

O autor fixou a regra em uma frase que virou o critério de aceite:

> **Detecção não revela FOW.**

---

## 2. `PodeEnxergar` nasce como entidade

Até esta versão o `PodeEnxergar` **não existia como código**. Era o
`PodeDetectarSensor` com outro conjunto de flags, mais uma janela de Editor com
o nome. Uma função respondendo duas perguntas independentes.

`Assets/Scripts/Sensors/PodeEnxergarSensor.cs` passa a responder **só por
hexágonos**, com a regra escrita pelo autor:

```text
alcance    UnitData.visao, imposto — nada da lista de Detect alarga ou estreita
camada     a superfície do terreno da célula; revelação não tem meio, só alcance
origem     o EV do lugar onde a unidade está
linha      descendente até o EV do destino; para só se um bloqueador tiver EV MAIOR
borda      célula sem tile não é hex recusado, é ausência de tabuleiro
```

Ar e submerso não são terreno, então o EV deles vem da política do
`DPQAirHeightConfig` — por consulta, não por fallback. O submarino em `Submerged`
sai de EV 0 e por isso **é um soldado em cima da água**: mesma regra, mesmo raio,
mesma linha nivelada por planície, praia e mar.

O `CollectVisibleCellsForFogOfWar` passou a delegar a ele, e os três consumidores
herdaram de uma vez: FOW de runtime, bake da rodada zero e a `RetaguardaWindow`.

---

## 3. As três portas por onde a detecção revelava terreno

Fechar isso exigiu achar três caminhos independentes, não um:

**`preserveObserverLayerRangeForHexVisibility`** — elevava o alcance de revelação
de qualquer hex ao alcance da **camada do próprio observador**. EWACS em AirHigh:
piso 7. Submarino em Submerged: piso 7, o alcance de caçar submarino. Era a
origem dos dois sintomas relatados.

**`BuildFogDisplayVisibleCellsForAllModes`** — varria as especializações de Air e
somava no conjunto que pinta terreno. Daí seguia para `knownCells` e para
`RecordConfirmedExploredCells`, a **memória permanente**. O vazamento não era
transitório: virava exploração gravada no save.

**`AddSpecializedAirKnowledge`** — o gêmeo da anterior no bake da rodada zero.

As três saíram. Quem revela hexágono é o campo `visao`, pelo `PodeEnxergar`.

---

## 4. `LosPolicy` virou `DetectionMethod`

O autor observou que radar é instrumento e ainda assim a conta dele é uma reta —
o pulso viaja em linha e o terreno bloqueia. Sonar contorna a península. O par
não separa óptico de instrumental; separa **geometria do cálculo**.

```text
InheritGlobal (0) → LineOfSight (0)   reta, obedecendo o toggle global
ForceOn       (1) → removido          nenhuma ficha usava
ForceOff      (2) → Propagated (2)    propaga pelo meio da camada do alvo
```

Os números ficaram de pé e o campo ganhou `FormerlySerializedAs`, então nenhuma
das três fichas que usam o valor 2 — `MA Submarino`, `MA Fragata`,
`AR Super Tucano` — precisou ser editada à mão. Todas elas já significavam
`Propagated`; o rename batizou o que a ficha sempre quis dizer.

A lista `visionSpecializations` passou a se chamar **Detect Specializations** no
inspector, sem tocar em serialização. O nome antigo era o que autorizava
mentalmente uma entrada dali revelar terreno.

`Propagated` já existia no código, escondido em dois lugares: o mapa de distância
aquático soldado à camada `Submarine/Submerged`, e o `range only` para alvos em
`AirHigh`. Dois casos especiais implícitos que agora têm nome — mas continuam
soldados no `if`; declará-los na ficha é trabalho da próxima versão.

---

## 5. Contato detectado autoriza o tiro

Consequência direta da separação, levantada pelo autor:

> *A unidade está detectada, independente de onde esteja, eu posso atirar nela.*

`RunMirarSensorAtUnconfirmedDestination` perdeu dois filtros que assumiam o
contrário — a regra da célula ("alvo em hex não conhecido sai silenciosamente")
e o rebaixamento de alvo válido por corredor não batido. Os dois existiam porque
contato confirmado implicava hex revelado, e o split tornou isso falso de
propósito.

O log `[MiraNoEscuro][LEAK]` foi junto: a premissa dele — *"se um alvo passou o
confirmado mas caiu aqui, o cache foi populado por caminho errado"* — deixou de
ser bug e virou a regra.

O que ficou: a neutralização do **motivo** das entradas inválidas. O alvo pode
aparecer nomeado; `"LOS bloqueada em (12,5), EV 3"` descreve terreno que ninguém
reconheceu.

---

## 6. Melhor Visão consome a fotografia

O degrau que era o plano do dia, entregue antes do descarrilamento.

`MelhorVisaoService` aceita um `FogKnowledgeSnapshot` e lê a cobertura aliada das
contribuições por hex, retirando a da própria unidade — em vez de reexecutar os
sensores de cada aliado. Sem fotografia, o caminho estrutural bruto segue
valendo.

`ResolveUsableSnapshot` separa os três casos: **não há** fotografia, ela **não se
aplica** (outro tabuleiro, outro slot, sem contribuições por hex — o caso do FOW
desligado) e ela **serve**. Falha conservadora cai no bruto.

Nenhum `AIController` consome isso. A Vigilância passa `IsKnown`/`IsExplored`
próprios e segue no caminho de sempre.

---

## 7. As ferramentas deixaram de mentir

A janela do `PodeEnxergar` listava **um cenário por Detect Specialization**, cada
um com camada forçada e alcance próprio. Numa janela com esse nome, era a própria
confusão que a versão existe para desfazer.

Sobrou um cenário: `Visão (campo visão da ficha)`. Saíram "Forçar camada virtual
do alvo" (impor a camada do alvo é pergunta de detecção) e "Restringir ao time
ativo" (time ativo é filtro de consumidor, não propriedade do sensor). O texto de
ajuda passou a descrever o que a janela faz.

A janela do `PodeMirar` ganhou duas linhas que não existiam: **Alvo detectado** e
**Hex do alvo revelado**. É o par que a versão separou, visível lado a lado.

`airLowBlockLoS` passou de `1` para `0` como consequência da mesma separação: um
helicóptero em `air/low` deixou de bloquear a linha de quem olha de cima.

---

## 8. O que não terminou

**O `PodeEnxergar` ainda é o `PodeDetectar` com flags desligadas.** Ele não tem
laço próprio: monta a resposta chamando `CollectVisibleCells` e neutralizando
regras uma a uma. O autor cravou o princípio:

> *PodeEnxergar não pode usar regras que pertençam ao PodeDetectar para liberar
> hexágonos.*

Enquanto for flag, qualquer regra nova do `PodeDetectar` volta a vazar para cá
sem aviso — e foi exatamente assim que o mar do submarino sumiu (ver seção 9). O
laço próprio precisa de duas primitivas expostas como geometria pura, sem
política: a caminhada dos hexes intermediários e a resolução de EV/`blockLoS` de
uma célula.

**A perna de detecção não começou.** Esta versão fecha só a revelação.

**`Propagated` ainda não é declarativo.** O mapa aquático e o `range only` de
`AirHigh` continuam decididos por `if` na camada, não pela ficha.

**`KnownCells` continua sendo um balde só.** Ele não recebe mais conhecimento
aéreo especializado, mas ainda mistura terreno e memória de exploração, e o
`FogKnowledgeSnapshot` segue sem eixo de camada. O Melhor Spotting vai precisar
disso.

**Saves antigos têm resíduo.** Hexes revelados pelo alcance de detecção antes
desta versão já estão gravados como explorados e não são limpos por ela.

---

## 9. O erro que custou o dia

Registrado porque o modo de falha é reproduzível, não porque foi vergonhoso.

Cinco hipóteses minhas sobre a causa do submarino revelar raio 1 caíram, uma
depois da outra: o piso da linha 703, o escopo da regra de corredor, a detecção
quebrada pelo commit do FOW, o mar bloqueando LoS, e a origem do EV. Cada uma
nasceu de ler **um trecho** do `PodeDetectarSensor` — um arquivo com quatro
caminhos parecidos — e afirmar mecanismo a partir dele. Uma delas piorou o
sintoma de raio 1 para raio 0 e precisou de revert.

A causa real era uma flag que eu mesmo tinha ligado:

```csharp
// PodeDetectarSensor.cs:660
if (skipSpecializedTargetLayers &&
    HasVisionSpecializationForLayer(observerData, targetDomain, targetHeight))
    continue;
```

`skipSpecializedTargetLayers` **não** ignora o alcance das especializações. Ele
descarta a **célula** quando a camada resolvida dela tem uma Detect
Specialization. O submarino tem uma para `Naval/Surface`; todo hex de mar sumia
antes de qualquer conta de linha. O soldado passava ileso por não ter
especialização nenhuma — e esse contraste era a pista, que eu li como sendo sobre
água.

A lição não é "leia mais". É: **numa função com múltiplos caminhos, ler um trecho
não permite afirmar qual roda.** O diagnóstico correto teria vindo do relatório
hex a hex da própria janela, que já mostrava `LoS direta: sim` e nenhuma parada
— dizendo, sem ambiguidade, que a célula caiu antes da linha importar.
