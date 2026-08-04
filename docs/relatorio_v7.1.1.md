# v7.1.1 — Uma pergunta, uma implementação

Fecha a segunda metade do dia 2026-08-04, a partir da `v7.1.0`.

A `v7.1.0` separou **enxergar** de **detectar**. Esta versão descobriu que a
detecção tinha o problema espelhado: uma pergunta respondida por **duas
implementações** que discordavam entre si.

O sintoma foi o autor colocando as duas telas lado a lado:

> *O PodeDetectar achou ambas as aeronaves, mas elas não aparecem no jogo.*

---

## 1. A descoberta: a ferramenta auditava um caminho que o jogo não usava

Existiam duas respostas para *"eu detecto este alvo?"*:

```text
janela  → PodeDetectarSensor.CollectDetection    varredura por observador
jogo    → CanObserverObserveTarget               par a par, via IsTargetObservedByTeam
```

Elas podiam divergir, e divergiam. A janela mostrava um caça detectado por chave
de `Stealth` com LOS direta a 7; o tabuleiro não o desenhava. Como
`logicallyVisible` vem desse caminho, a apresentação de contato cinza sobre o
preto — que a `v7.1.0` tinha acabado de tornar possível — **nunca disparava**
para ele.

Isso é o pior tipo de bug de ferramenta: ela não estava errada, estava auditando
outra coisa. Enquanto durou, ela deu confiança falsa sobre uma ficha correta.

`IsTargetObservedByTeam` passou a consumir `CollectDetection`. Todo consumidor de
visibilidade de unidade herdou de uma vez: FOW de runtime, apresentação de
contato, `PodeMirar` com `respectTotalWarVisibility`, e a IA.

**Efeito colateral que o autor notou sozinho:** o combate se resolveu junto, e o
recálculo ao destruir unidade também. Os dois liam a mesma visibilidade que
estava mentindo.

---

## 2. O delta do FOW assumia que revelar e detectar eram a mesma coisa

Mesmo com a fonte unificada, o caça continuava sem aparecer. A causa foi a
`v7.1.0` quebrando uma premissa que ninguém tinha escrito.

O refresh por movimento é incremental e filtra assim:

```csharp
if (!affectedTargetCells.Contains(cell))
    continue;      // a visibilidade desta unidade nao e recalculada
```

O conjunto de células afetadas vem da mudança de **revelação geográfica**. Isso
cobria a detecção **por acidente**: enquanto a especialização do observador
inflava o conjunto geográfico, a célula do alvo entrava no delta de graça.

Depois da separação, um radar que detecta a 7 sem revelar terreno não põe a
célula do alvo no conjunto. A visibilidade daquele inimigo ficava com o valor
velho para sempre.

O mesmo filtro existia em **dois** lugares, e o segundo só apareceu depois de o
primeiro ser consertado: `RefreshRuntimeUnitFogVisibilityForCells` e
`PublishFogGameplaySnapshot` — e é do snapshot que a apresentação lê.

No ponto de compromisso da ação, os dois passam a ser completos. É o que o
invariante transacional já mandava: movimento provisório não publica verdade, e
ao voltar para `Neutral` o mundo confirmado é reconstruído.

---

## 3. A base: dois serviços saíram do `PodeDetectar`

Para as duas verdades deixarem de compartilhar internals, o que nunca foi regra
de detecção mudou de casa:

```text
ObservationCellService   terreno, construcao, estrutura, camada, EV, blockLoS
                         + os tres caches de escopo de refresh e o de grid

HexGridGeometry          CubeCoord, offset↔cubo, distancia, lerp, round,
                         resolucao de odd-row e o cache dela
```

Nenhum dos dois sabe de alcance, chave, método, especialização, furtividade ou
time. São fatos do tabuleiro e geometria de grade — e as duas verdades precisam
dos mesmos.

Movimentação literal nos dois casos: o corpo é o mesmo, o `PodeDetectar` ficou
com wrappers privados de assinatura idêntica, e nenhum dos ~20 chamadores
internos mudou.

---

## 4. Propagação virou dado, e deixou de ser privilégio do submarino

O `DetectionMethod` criado na `v7.1.0` só decidia metade: a linha. O **mapa de
distância** continuava escolhido por um `if` de camada — `Submarine/Submerged`
sempre propagava, tivesse a ficha declarado ou não.

Duas correções, em dois passos:

1. o gatilho passou a ser o método declarado na ficha;
2. o `if` de camada saiu inteiro. O meio vem da **camada do alvo**: a propagação
   anda pelas células que aquela camada aceita.

A consequência foi o autor quem nomeou:

> *Se um dia eu quiser inventar um sensor de motores e dizer que é uma detecção
> 5 em land/surface propagate, eu criei um megafone.*

E criou — sem uma linha de código nova. O som contorna o morro pelas células de
superfície do mesmo jeito que o sonar contorna a península pela água.

---

## 5. Código morto: 193 linhas

`CanObserverObserveTarget` ficou sem chamador e saiu.
`GetIntermediateCellsByCellLerpLegacy` era **cópia linha a linha** da principal —
a "legada" era a mesma função sem o `if` que escolhia entre as duas. Saiu junto
com o toggle `UseLegacyLoSLerp`, que ninguém setava e que, mesmo setado, não
mudava nada.

Deleção pura, sem mudança de comportamento por construção.

---

## 6. Doutrina escrita no `CLAUDE.md`

Duas coisas que só existiam na cabeça do autor:

**As duas verdades.** `PodeEnxergar` revela hexágonos, `PodeDetectar` faz
unidades aparecerem. Melhor Visão, Melhor Spotting, revelar FOW, fotografar
`explored` e o bake da rodada zero são **derivações** — nenhum é fonte. Com o
modo de falha junto: *se alguém precisar desligar uma flag para uma das duas se
comportar, a entidade está errada, não a flag.*

**Nada foi distribuído.** O jogo não está na Steam, existe só na máquina do
autor, previsão de meados de 2027 a 2028. Logo: arquitetura de save e de bake
muda quantas vezes o design precisar, e proposta de shim de versão ou
retrocompatibilidade é custo que não compra nada.

---

## 7. O que não terminou

**A linha ainda mora no `PodeDetectar`.** `HasValidStraightObservationLine`,
`ResolveOriginEvForLos`, o lerp e o cube-line. Com a geometria e o fato de célula
já fora, ela deixou de arrastar a teia — mas continua lá.

**O `PodeEnxergar` ainda é o `PodeDetectar` com flags desligadas.** Não tem laço
próprio. Só quando tiver é que `skipSpecializedTargetLayers`,
`preserveObserverLayerRangeForHexVisibility` e o `ignoreDetectSpecializations`
— este último andaime criado nesta série — podem morrer.

**A linha de quem detectou não aparece no resultado.** O autor pediu que o
`PodeDetectar` mostre a subida ou descida da linha, como o `PodeEnxergar` já
mostra. Não feito.

**`skipLosForCurrentTarget` continua vindo do `DPQAirHeightConfig`.** Ele fala do
**meio** — se a camada bloqueia linha — e não do sensor. Decisão pendente: fica
propriedade do meio ou vira método declarado?

**O alerta sonoro precisa de um gancho que não existe.** O certo não é
"detectou", é **"passou a detectar"**: o delta entre o conjunto anterior e o novo
no publish. Sem isso o sonar toca a cada refresh.

**`KnownCells` continua um balde só**, e o `FogKnowledgeSnapshot` segue sem eixo
de camada — o Melhor Spotting depende disso.

**Resíduo nos saves** anteriores à `v7.1.0`. Agora é trivial: é só apagar.

---

## 8. Dívidas que esta versão criou

**Perf, não medida.** Três trocas de filtro por varredura na mesma sessão:

```text
IsTargetObservedByTeam   par-a-par  →  CollectDetection por observador
refresh de visibilidade  delta      →  cheio, por commit
publish do snapshot      delta      →  cheio, por commit
```

Cada uma é defensável sozinha; as três juntas multiplicam trabalho no caminho
quente. **Um turno de IA com muitas unidades é o teste**, e o `FrameSpike` é o
instrumento. O suspeito provável é o `CollectDetection` por observador.

**Um bug aberto e fechado na mesma versão.** Ao generalizar `Propagated`, o mapa
de distância por propagação continuou sendo montado uma vez por coleta sem
guardar de que camada era. Enquanto só o submerso propagava não aparecia; com o
megafone, uma ficha com duas camadas propagadas faria a segunda receber a
distância pela água. Os dois coletores passaram a guardar a camada carregada e
remontar quando a pedida for outra.

---

## 9. Armadilha de ambiente que quase entrou no repositório

Editei o `PodeDetectarSensor.cs` por script para apagar as 193 linhas. O
`Get-Content` do PowerShell 5.1, num arquivo **sem BOM**, lê como ANSI e
**corrompe os acentos** — os comentários em português viraram `Ã¢â‚¬â€`.

Só apareceu porque o `git diff --stat` mostrou 11 inserções numa deleção que
deveria ser pura. Revertí e refiz com `ReadAllLines`/`WriteAllLines` UTF-8
explícito.

A lição não é sobre encoding: é que **o diffstat de uma deleção pura tem que
mostrar zero inserções**. Quando não mostra, alguma coisa foi reescrita sem
intenção.
