# v8.1.0 — O comportamento estava certo; a ordem dos `if` é que não estava

O dia começou com a fatia 1 finalmente rodando e terminou com o transporte
inteiro remexido. Mas o fio que amarra tudo não é nenhuma das mudanças: é uma
constatação que apareceu **três vezes**, em subsistemas diferentes, sempre com a
mesma cara.

> **Quando um comportamento parece "esquecido", conferir se ele não está apenas
> depois de um `return`.**

---

## 1. A fatia 1 passou — e atravessou duas versões para isso

O commit `3e0565d` (v8.0.0) compilava desde ontem e nunca tinha rodado.

```text
T1   origemAlvo=servico   BeyondOperational   QueroCarona=SIM   (7,0)→(4,0)
     [Missao] 1 Capture -> (0,0,0) predio=#2 (adquirida)
────────── salvar · Stop · Play · carregar ──────────
T2   origemAlvo=reserva   Operational custo=4  QueroCarona=NAO  (4,0)→(1,0)
     [Missao] 1 Capture -> (0,0,0) predio=#2 (mantida)
```

Bate linha por linha com o traço pré-fatia. A fatia era subtração, então
**igualdade É o resultado correto**.

O ciclo completo `T1→T6` fechou depois, e a única incógnita — *a missão morre
limpa quando o objetivo é tomado?* — respondeu sozinha: o prédio `#2` saiu, o
`#1` entrou, sem resíduo e sem baixa forçada.

**E o ciclo entregou o alvo medido da frente seguinte:**

```text
MelhorCapturaCalls   3 · 1 · 0 · 0 · 3 · 1
                     ↑           ↑
                     descobre    descobre
```

Três chamadas quando descobre o alvo, uma quando lembra dele. Deixou de ser
suspeita e virou número.

---

## 2. As três vezes em que a ordem era o defeito

### 2.1 O skip do Tactical existia — no organizador, não no serviço

O autor perguntou se o capturador pulava o `QueroCarona` ao entrar no Tactical.
Fui ao `QueroCaronaService`, não achei early-out, e respondi **"não é skip, é
resposta"**.

Errado. O skip mora em `TryDecideCapturerAction`, via
`[Oportunista] captura local antes de embarcar`, e **retorna antes** do gate de
embarque. A medição:

```text
T2   decision=125ms   7 estágios
T3   decision=  7ms   1 estágio     ← pulou
T4   decision=  1ms   stages=- metrics=-    ← chão absoluto, zero consultas
```

### 2.2 O alvo é resolvido três vezes por decisão

`QueroCarona` resolve o alvo para saber se quer carona; `SemPlano` resolve a
âncora; `Rogue` marcha. Mesmo alvo, três vezes — e não é circunstancial, é a
estrutura.

### 2.3 Largar × avançar nunca foi uma escolha

```text
3. TryBuildBestCourierDisembarkAction   ← se devolver ação, acabou o turno
4. âncora + FindTransportMove           ← só chega aqui se a largada falhou
```

Qualquer LZ admissível encerrava o turno, e toda a lógica de âncora **nem
rodava**. A pergunta do autor — *"não era pra ele ter avançado pro operacional
com delay de desembarque?"* — não tinha resposta porque a decisão não existia.

---

## 3. O que virou código no transporte

| mudança | o que fixa |
|---|---|
| missão nasce **antes** dos atalhos | Oportunista, embarque, handoff e swap agiam sem declarar destino |
| courier lê a **coordenada**, qualquer verbo | trocar `Capture` por `Pressure` fazia a carga "não ir a lugar nenhum" |
| `dropOffRange` vira **banda do passageiro** | a constante `4` era frouxa para quem anda 3 e apertada para quem anda 1 |
| âncora na **zona de entrega** | mirar o prédio punha a mira atrás do obstáculo |
| **três tempos**: Tactical, adiar, Operacional | virou comparação em vez de posição de `if` |
| transportador declara **Transport → objetivo da carga** | transporte aninhado: o navio lê o APC como o APC lê o soldado |
| demanda de transporte da **fila de carona** | rebelde com garagem, R$4500 e capturador na fila comprava nada |

### A distinção que gerou metade disso

> **Borda do Tactical é para EMBARQUE. Entrega é para DENTRO.**

Duas geometrias opostas para a mesma banda. No encontro o passageiro caminha
para fora, então a borda economiza turno dos dois; na entrega quem anda é só ele,
e cada hex que sobra é turno perdido antes de capturar.

Eu tinha previsto o sintoma certo — *"o APC vai parar longe demais"* — e
diagnosticado a causa errada: achei que era a faixa, era a **ordem das chaves**
do desempate.

---

## 4. Três faxinas do mesmo tipo

```text
aiDesignatedCaptureTarget*   já eram derivados da missão      (v8.0.0)
aiHasDesignatedMission       o enum tem None; o bool sobrava
Mission Sector               escrito por 2, lido por NINGUÉM
```

O `aiHasDesignatedMission` confessava sozinho — a única escrita dele era
`aiHasDesignatedMission = intent != None`. **Já era derivação, só estava
armazenada.**

E ficou o critério que o autor fixou, que vale para o resto:

> **Pode mudar durante a partida?** Se pode, é circunstancial e tem que ser
> re-derivado a cada decisão — nunca guardado. Flag é para o que é decisão de
> autoria (`routesMigratedToScene`), não para estado de tabuleiro.

---

## 5. O papel é DERIVÁVEL — e o critério é do autor

Pergunta: *"o F-22 e o B-2 não são vigilância?"* Resposta: **não, porque não têm
visão especializada.** E isso já existe como predicado:

```csharp
// UnitData.cs:612
HasStealthDetectionFor(domain, height)
    => TryGetVisionException(...) && entry.detectUnitsWithFollowingSkills.Count > 0;
```

Não é *"tem exceção de visão"* — o F-22 tem, e enxerga bem. É *"a exceção carrega
**lista de detecção**"*.

```text
carregar AR Stealth  →  você é a FECHADURA. não muda seu papel.
listar   AR Stealth  →  você é a CHAVE. isso é Vigilância.
```

Melhor que o argumento por moeda que eu tinha dado, porque é **um campo da
ficha**, não uma inferência.

---

## 6. As ferramentas discordavam do jogo — duas vezes

| defeito | consequência |
|---|---|
| a bancada não passava `allowTransporterCell` | o runtime recusava ~190 LZs por névoa; a janela aprovava todas |
| a bancada não passava `maxRemainingRouteCost` | elegia LZ com o passageiro ainda a **R15** do alvo |

Nos dois, um portão que só existia do lado do jogo. **Duas ferramentas dando
respostas opostas sobre a mesma cena é o pior resultado possível para um
diagnóstico** — pior que uma ferramenta faltando, porque a errada parece
legítima.

O conserto estrutural veio depois: `TryResolveDeliveryZoneAnchor` virou
**estática com tudo por parâmetro**, e a bancada chama a mesma. Uma
implementação, dois chamadores.

### E a névoa saiu da bancada no fim do dia

Quatro commits tentando fazer a janela prever com névoa, até o autor cortar:

> *"no fundo a ferramenta devolve o mapa alheio à fow e a AI courier decide, né?"*

Sim — e não é recuo, é pôr o conhecimento no andar certo. Havia um motivo
estrutural para nunca funcionar: a previsão usa a informação de **antes** do
passo, e é o **commit** que revela. O gameplay acerta porque já chegou lá; a
bancada, parada, só podia errar.

---

## 7. Um commit meu não continha o que a mensagem dizia

O commit *"Bancada do MelhorDesembarque também lê só a coordenada da missão"*
entregou **apenas o rótulo da vaga**. O script python abortou numa asserção
antes de gravar o arquivo, e eu commitei sem conferir — a mensagem descrevia
trabalho que não existia.

Descoberto por acaso, ao reabrir o arquivo por outro motivo. Corrigido no commit
`c05ee4d`, que declara o erro.

> **Compilar não prova que o arquivo mudou.** Um script que falha antes de
> escrever deixa a árvore idêntica, e `git commit` de árvore idêntica no arquivo
> alvo passa sem reclamar se outro arquivo mudou junto.

---

## 8. O que NÃO terminou

### O fallback silencioso do `ProgressionSelector` — o bug com endereço

```csharp
// ProgressionSelector.cs:108
return distanceToTargetMap.TryGetValue(cell, out int routeCost)
    ? routeCost                                     // custo de ROTA
    : SectorManager.HexDistance(cell, targetCell);  // ← fallback SILENCIOSO
```

Célula fora do mapa de rota não é marcada inalcançável: recebe linha reta. Aí
`firstTurnProgress = origem − célula` **subtrai custo de rota de distância em
hex**. Com serra os dois divergem 3× e o sinal inverte:

```text
origem  (19,2)  fora do mapa  → hex  4
serra   (19,3)  no mapa       → rota 12
progresso = 4 − 12 = −8       (a ferramenta dá +15,6 no mesmo hex)
```

Resultado: platô de zeros, `tool = −moveCost`, e o hex mais caro — o único que
resolve — fica em último. **É o ping-pong `(19,2)↔(18,2)` do `docs/gamelog/log.md`.**

Sem montanha os dois números coincidem e isso nunca aparece. Foi preciso cercar
o alvo para ver.

### 34 commits, e a validação foi só gameplay solto

Nenhuma corrida de aceitação fechada. O que se sabe é que **em gameplay o
comportamento está certo** — o APC escolhe hex no Tactical fora da névoa e
desembarca. O que não se sabe é se cada mudança individual está certa, porque
elas nunca foram exercitadas uma de cada vez.

### `Mission Intent = None` quando o transportador está à toa

O modelo ficou escrito e duas das três pernas estão de pé:

```text
Transport + carga a bordo      Delivery   alvo = objetivo da carga     ✅
Transport + vazio + promessa   Pickup     alvo = o passageiro          ✅
None                           nada a fazer                            ❌
```

O buraco: vazio, sem promessa, com missão velha pendurada. O navio leria um
encontro que já não existe — pior que ler nada, porque parece informação. O lugar
está identificado (`AIController.RidePromise.cs`, na baixa da promessa) e o
cuidado também: são dois caminhos de saída com destinos opostos — passageiro
embarcou vira Delivery, passageiro sumiu vira `None`.

### Herdado e intocado

- ~190 rejeições de LZ por névoa rodam **duas vezes** por turno (~380 avaliações
  para zero resultado)
- `TransportDropOffRange` migrado só no courier; Naval, Evac e Assigned seguem na
  constante (18 pontos de chamada)
- a triagem de locais entregáveis existe **só na bancada** — nada no runtime a
  consome
- limpar a origem das rotas, `fieldEntries` do `ConstructionDatabase`,
  `ObjectiveManager` sem hook de `sceneLoaded`

---

## 9. O que eu errei, e como

Cinco vezes, e quatro delas com a mesma forma: **concluir "não existe" sem abrir
o que existia.**

| conclusão | onde estava |
|---|---|
| *"Tactical não é skip"* | estava no organizador; procurei no serviço |
| *"nada valoriza subir a montanha"* | a ferramenta dá **+15,6** no hex |
| *"o APC não sobe"* | tem `OFF Road`, e o custo está no `Montanha.asset` |
| *"o Strategic cúbico é defeito"* | é projeto, e a ferramenta já o aplica por intent |
| *"a marcha estava errada"* | conferi o verso contra um doc que estava errado |

A quinta é de outra família e é a mais perigosa: **conferir coerência não é
conferir correção**. Bater com uma referência torta é sintoma, não prova.

E há um sinal que eu tinha na mão nas duas vezes em que estreitei um verso: o
texto do autor cobria **mais casos** que a regra contra a qual eu o conferia.
Descompasso de generalidade **é a evidência** — a regra é que está incompleta.
