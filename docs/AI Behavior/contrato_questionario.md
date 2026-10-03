# Contrato do questionário — a IA pergunta antes de decidir

**Estado:** CONTRATO, escrito em 2026-10-02 a partir do desenho do autor (seis
colunas, 33 unidades em 6 papéis) e de dois exemplos do Capturador ditados por
ele. **Nenhuma linha de código existe.**

> **HOJE** = verificado no código. **CONTRATO** = decidido, não escrito.
> **ABERTO** = ninguém decidiu.

Leia antes: [`papeis.md`](papeis.md), que traz as 8 ações e as 2 capacidades de
informação, e [`ficha_do_papel.md`](ficha_do_papel.md), que traz a matriz
`Pode*` × `Melhor*`.

---

## 1. Por que existe

**HOJE** a IA decide pela primeira resposta não-nula e executa
(`AIController.Router.cs`, `DecideUnitAction`). Uma cascata de `TryDecide*` em
ordem fixa e global: a primeira que devolve ação ganha, e a unidade se manda.

```text
desbloquear produção → vacate → reparo → estoque crítico → transporte
→ rebelde → capturador → (FS antes?) → assalto → FS → vigilância
→ combate aéreo → logística → estoque → HexEvaluator
```

Três defeitos saem disso:

| defeito | consequência |
|---|---|
| ninguém compara | se o reparo devolve ação, o tiro ao lado nem é perguntado. **A ordem faz o papel do peso** |
| a ordem é uma só | cada papel se defende com guardas internas (`isAirSurveillance`, `preferFireSupportFirst`, híbrido), e cada guarda é um `if` cruzando dois papéis |
| decide e executa | nenhuma decisão é revista à luz do entorno. O handoff, o blitzkrieg e o vacate existem como remendos espalhados |

O segundo é o **n × n**: cada papel novo cruza com os outros em guardas. O
questionário existe para que isso cresça em **10 + 6**, não em **10 × 6**.

> **Todo papel responde as mesmas dez perguntas. O que muda é a ordem em que
> pergunta e o que faz com as respostas.**

---

## 2. As dez casas

O desenho tem dez linhas por coluna. São as **8 ações diretas** e as **2
capacidades de informação** do [`papeis.md`](papeis.md) (quadro geral):

| casa | espécie | sensor | consumidor |
|---|---|---|---|
| Capturar | ação | `PodeCapturar` | `MelhorCaptura` ✅ |
| Embarcar | ação | `PodeEmbarcar` | `MelhorEmbarque`, `QueroCarona` ✅ |
| Desembarcar | ação | `PodeDesembarcar` | `MelhorDesembarque` ✅ |
| Mirar | ação | `PodeMirar` | `MelhorCombateService` ⚠️ existe, mas nenhuma IA o chama; o combate decide à mão (`C.Attack` e cia.) |
| ~~Fundir~~ | — | `PodeFundir` | **fora do questionário**: a decisão é só do reparo, `AIController.Repair.cs` (§6.4) |
| Suprir | ação | `PodeSuprir` | ❌ |
| Transferir | ação | `PodeTransferir` | `MelhorEstoque` ✅ |
| Reposicionar | ação | envelope (Hotzone) | `MelhorCapitao` ⚠️ sem tradutor |
| **Enxergar** | **informação** | `PodeEnxergar` | `MelhorVisao` ✅ |
| **Detectar** | **informação** | `PodeDetectar` | `MelhorDeteccao` ❌ |

### 2.1 Casa de ação

> *"Existe célula no meu tático de onde eu posso X, e isso serve ao meu papel?"*

O fluxo do jogo é sempre **posicionar, depois agir**, e permanecer no hex também
é posicionamento ([`papeis.md`](papeis.md), linha 5). Uma casa de ação devolve
o par **(célula, ação)**. Um SIM é candidato a decisão.

### 2.2 Casa de informação

> *"O que eu sei?"*

Ninguém "executa Detectar". Enxergar e Detectar **não viram ação**: viram
**contexto** para as casas abaixo delas e para as políticas. A posição delas na
coluna quer dizer *"o que eu preciso saber antes de considerar o resto"*.

O papel decide se a informação **barra** ou só **pesa**:

| modo | efeito de uma resposta ruim | onde aparece |
|---|---|---|
| **contexto** | as casas abaixo rodam; a resposta só chega às políticas | Capturador (§6) |
| **portão** | as casas abaixo não rodam; a ação que sobra é Reposicionar para obter a informação | ABERTO — a Vigilância (Detectar em 1º, Reposicionar em 2º) parece ser este |

Quando a informação falta e a ação é Reposicionar, a informação nova **só chega
no Neutral seguinte**, e serve às **outras** peças. O movimento provisório não
revela nada ([`papeis.md`](papeis.md), linhas 518–533).

---

## 3. Os estados de uma resposta

Toda casa responde com um destes quatro:

| estado | quer dizer |
|---|---|
| **SIM** | o sensor devolveu opção e o papel quer usá-la |
| **NÃO** | o sensor devolveu vazio, ou devolveu opção e o papel não quer |
| **NÃO SE APLICA** | a peça não tem a capacidade (submarino em Capturar) |
| **NÃO SEI RESPONDER** | a casa não tem consumidor ainda |

O quarto estado é obrigatório. Sem ele, a falta de `MelhorCombate` vira um
"NÃO" inventado, e a grade de respostas mente sobre o que a IA sabe. **A grade
inteira é também o mapa do que falta na escada.**

O terceiro herda o ABERTO do §7.6 da ficha: *"não se aplica" é autorado ou
derivado?*

### 3.1 Toda resposta carrega o porquê

Um NÃO sozinho não conta história. A resposta guarda o fato que a produziu:

```text
Capturar   NÃO   porque   fora do tático (terreno)
                          dentro do tático, sem caminho   ← indício de ocupante
                          prédio já é meu e está cheio
                          sem chave aceita pela construção
```

### 3.2 Enxergar tem três respostas, não duas

O [`papeis.md`](papeis.md) separa três estados do hex, e cada um conta uma
história diferente:

```text
desconhecido               nunca revelado
conhecido, fora da visão   revelado antes; o conteúdo é foto velha
visível agora              há cobertura atual
```

---

## 4. Respostas em conjunto são um diagnóstico

**As respostas não valem uma a uma.** Uma combinação que não fecha é
informação. Exemplo ditado pelo autor:

```text
consigo capturar?    NÃO   (o prédio está no tático, mas o caminho não completa)
enxergo o hex?       SIM
detecto alguém lá?   NÃO
```

> *Só essas três perguntas já me contam a história do mapa.*

| Enxergar | + não detecto + caminho não completa = |
|---|---|
| desconhecido | névoa; não sei nada |
| conhecido, fora da visão | foto velha; alguém entrou depois |
| visível agora | **ocupante furtivo**: vejo o hex e não tenho a chave |

**HOJE, e é o que torna isso legítimo:** o pathfinding barra ocupante pela
posição real, com ou sem detecção (`UnitMovementPathRules.cs`, linhas 170–200).
O humano vê o mesmo buraco no alcance: o [`papeis.md`](papeis.md) (linha 524)
chama isso de *indício*. Ler o buraco não é wallhack. **Indício não é
detecção:** Mirar continua vazio.

O buraco pode estar **no caminho**, não no prédio. Quem localiza é comparar o
envelope sem ocupantes com o envelope com ocupantes.

---

## 5. As duas etapas

```text
1. QUESTIONÁRIO   as casas, na ordem do papel           → decisão PRELIMINAR
                  preliminar = primeira casa de AÇÃO com SIM
2. POLÍTICAS      o papel revisa olhando o entorno      → decisão FINAL
```

As políticas fazem duas coisas, e só duas:

| a política… | exemplo |
|---|---|
| **troca a ação** | Capturar → Reposicionar, para liberar o prédio (§6.1) |
| **escolhe a célula** | Reposicionar → a montanha, entre as células equivalentes (§6.2) |

Trocar a ação **está previsto no jogo**: *"poder capturar, atacar ou embarcar
não impede a unidade de apenas confirmar sua posição"*, e **liberar passagem** é
razão legítima para Reposicionar ([`papeis.md`](papeis.md), linhas 11 e 63).

### 5.1 A saída é um trio

```text
(célula, ação, motivo)
```

O **motivo é a agenda do papel**, não o efeito colateral. O Capturador que sobe
a montanha registra *"avançar para capturar"*. Revelar o hex é ganho extra. Se o
log disser *"reposicionar para revelar"*, o Capturador passa a ser lido como
explorador, que é o desvio de identidade que a revisão de papéis apontou no
`C.Explorer`.

---

## 6. O Capturador, nos exemplos do autor

**Lema:** *"antes do tiro, vem o dinheiro"*. **Magnético:** construções
capturáveis. Ele não abre missão para outros revelarem o que é dele se consegue
chegar lá. **A IA sabe onde ficam os prédios.**

Para o Capturador, Enxergar e Detectar são **contexto**, nunca portão.

### 6.1 Handoff — liberar o prédio para quem vem atrás

```text
capturar agora?                       SIM
enxergo o hex?                        SIM
detecto inimigo no objetivo/visão?    SIM
embarcar para chegar?                 NÃO
reposicionar para chegar?             NÃO
mirar?                                NÃO
fundir (HP baixo)?                    NÃO

PRELIMINAR   Capturar

políticas
  algum capturador atrás chega ao prédio no tático, a partir do PRÉDIO?   SIM
  ele está com HP cheio?                                                  SIM
  há outros capturáveis livres no meu tático ou operacional?              SIM

FINAL        Reposicionar rumo ao outro prédio, deixando este vago
MOTIVO       ceder a captura e seguir o eixo
```

A pergunta da política é **centrada no alvo**: *"quais capturadores chegam a
este prédio no tático?"*. O prédio é o centro, e cada candidato usa o próprio
movimento ([`papeis.md`](papeis.md), linha 32).

Isso fecha dois dos três casos que reprovam no
[`teste_blitzkrieg.md`](teste_blitzkrieg.md) (`AIController.PlanEvaluator.Handoff.cs:97`):

| caso | hoje | com a política |
|---|---|---|
| C — substituto que não alcança é aceito | reprova | ✅ medido no tático do substituto |
| E — vizinho do prédio conta como chegar | reprova | ✅ medido até o prédio |
| F — ninguém confere se o substituto já agiu | reprova | ❌ **falta uma pergunta** |

**ABERTO:** a política precisa de *"ele ainda não agiu?"*. Mesmo que não tenha
agido, quem sai deixa uma **reserva** do prédio para ele. Senão o seguidor chega
no turno dele, o questionário dele escolhe outra coisa e o prédio fica vago.

### 6.2 Avanço na névoa — a montanha

Cenário: a cidade está atrás da floresta, e os terrenos estão em névoa.

```text
capturar agora?             NÃO
enxergo o hex?              SIM
detecto alguém lá?          NÃO
embarcar para chegar?       NÃO
reposicionar para chegar?   SIM
mirar?                      NÃO
fundir?                     NÃO

PRELIMINAR   Reposicionar

política     entre as células que mantêm a captura no próximo turno,
             a de melhor posição

FINAL        Reposicionar para a montanha
MOTIVO       avançar para capturar
```

> *Se eu fosse o soldado, iria para a montanha: fico em posição defensável,
> revelo quem está ali e ainda faço de spotter para os meus aliados.*

**A regra:**

```text
"capturar agora" respondeu SIM   → captura. Nenhuma posição compete.
respondeu NÃO                    → candidatas = células de onde o prédio fica no
                                   TÁTICO do próximo turno; entre elas vence
                                   DPQ + visão (defesa, revela, spotter)
a célula empurra a captura para
duas rodadas ou mais             → perde, porque custa o lema
```

**Progresso se mede em rodadas até capturar, não em hexes.** É a doutrina do
CLAUDE.md (*alcance é banda, não número de hexes*) aplicada ao desempate: se a
captura não acontece nesta rodada de qualquer jeito, ficar colado no prédio na
planície e ficar na montanha empatam, e a montanha ganha pela posição.

A montanha empresta EV a quem a ocupa (a regra de visão da v9.0.0), então a
visão melhora de verdade. **Mas a montanha não dá chave de detecção.** No caso
furtivo do §4, o Capturador avança mesmo assim: o lema não muda, só a história
que o log conta.

### 6.3 Prédio preto no tático — o pedido de spotting

**CONTRATO, decidido pelo autor em 2026-10-02.**

**HOJE**, e é o que cria o problema: o `PodeCapturar`, na hora de agir, recusa
a célula que **não está visível e nunca foi explorada**
(`PodeCapturarSensor.cs`, linhas 358–369, motivo *"Terreno ainda
desconhecido"*). Hex **explorado**, mesmo fora da visão, se captura normalmente.
Como o movimento provisório não revela nada, o capturador que para em cima do
preto não vê a opção de captura: chega, revela no Neutral e só captura na rodada
seguinte. **A rodada se perde no preto de verdade, e só nele.**

Se outra peça revelar o hex antes, o capturador chega e captura no mesmo turno.

**Quando o pedido vale:** só quando ganha uma rodada.

```text
prédio no TÁTICO e PRETO     → pede: revelado antes, ele captura NESTE turno
prédio no tático, explorado  → não pede: captura direto
prédio só no operacional     → em geral não pede: a própria marcha revela no
                               caminho (exceto atrás de floresta ou serra)
```

O gatilho é a casa Capturar respondendo **NÃO, porque o prédio está no tático
mas é preto** (§3.1). O planejamento consulta o sensor com a névoa desligada
(`applyFogOfWar: false`) para saber que o prédio existe; a hora de agir o recusa.
A distância entre as duas respostas é o pedido.

**O prazo é a fase de ações, não a rodada.** O spotter tem de agir antes do
capturador e no mesmo turno. Na rodada seguinte o pedido já não ganha nada.

```text
1ª passada   capturador: Capturar NÃO (preto, no tático) → pendura o pedido e ADIA
             outras peças jogam; uma delas pode atender e revelar
             Neutral → névoa recalculada
2ª passada   capturador: Capturar SIM → captura neste turno
             ninguém atendeu → ele mesmo vai (a montanha, §6.2); o pedido morre
```

**Adiamento único.** Na segunda passada ele não pede de novo: decide. É a guarda
`!secondPass` que todo adiamento da Fase 2 exige, porque adiamento sem ela já
virou cessão mútua e deadlock.

**Quem atende paga com a própria ação** (*"obter informação nova exige
comprometer a ação de reconhecimento"*, [`papeis.md`](papeis.md)). A decisão de
atender é **do spotter**: o pedido entra como uma resposta a mais no
questionário dele, e as políticas dele aceitam ou não. Esse lado é o
`MelhorSpotting`, que **não existe**. O lado de quem pede é o rascunho
`QueroSpottingWindow` (Tools ▸ Hotzone ▸ Quero Spotting), que já nomeia o par:
*QueroSpotting ↔ MelhorSpotting*, como QueroCarona ↔ MelhorEmbarque.

**É a mesma missão da artilharia:**

```text
pedido de spotting = (células, quem pediu, ganho, prazo)
artilharia   muitas células — a vanguarda cega da faixa da arma   ganho: tiro
capturador   uma célula — o prédio, que a IA sabe onde fica       ganho: 1 rodada de renda
```

**Entra no questionário sem esperar a missão.** A política fica escrita desde
já: *prédio preto no tático, peço olho e espero uma passada; se ninguém atendeu,
eu mesmo vou para a melhor posição.* Sem `MelhorSpotting`, o pedido nunca é
atendido e ele sempre cai no "eu mesmo vou", que é o §6.2. No observador, o
pedido aparece só como **fato** no registro (*"pediria spotting em X"*), e isso
já mede quantas rodadas a IA perde por preto.

**A Marcha não muda.** A terceira estrofe fica coerente com isto:

| verso | leitura |
|---|---|
| *"não entro às cegas / como se já fosse meu"* | não pisa no prédio preto: pede olho, ou toma a posição que revela |
| *"chamo olhos à frente / para o setor revelar"* | o pedido de spotting |
| *"quem chega sem ver / perde o turno de capturar"* | a rodada perdida do `PodeCapturar` no preto |
| *"há presença escondida / que alguém deve encontrar"* | o caso furtivo do §4, em que a montanha não resolve |

### 6.4 Fusão de trabalho — fora do reparo

**CONTRATO, decidido pelo autor em 2026-10-02.** Existem **duas** fusões, e
cada uma tem dono:

| fusão | quem decide | onde | para quê |
|---|---|---|---|
| **de reparo** | a decisão de reparo, que é invariante e fica **acima** do questionário | **retaguarda, sempre**; nunca perto de inimigo | sobreviver |
| **de trabalho** | o questionário, casa Fundir do Capturador, **só quando a peça não está em reparo** | onde está o prédio, que pode ser a vanguarda | capturar mais rápido |

> *É experiência de jogador. O fundir na retaguarda continua, que é o reparo
> falando; quando não estamos no reparo, o questionário entra.*

**HOJE:** a fusão de reparo existe, no passo 2 do reparo
(`AIController.Repair.cs`, linhas 508–641). Roda só em modo reparo, para quem
tem `fuseWhileInRepair` na ficha, com a soma de HP até 10, longe de inimigo, e
na invasão só na retaguarda segura. A fusão de trabalho **não existe**. A
[`revisao_papeis.md`](../revisao_papeis.md) diz que *"a IA nunca funde"*: está
errado, ela contou só os arquivos do Capturador.

**É rara, e é fusão "sem necessidade"** — só para aumentar o cap power. O caso
do autor:

> *Estou capturando um prédio. Passei a vez, levei dano, meu cap power caiu. No
> turno seguinte há um aliado próximo que também pode capturar o prédio, mas os
> dois estão com HP baixo e há inimigos à frente. Deixar o outro fazer
> blitzkrieg até pode, mas é mais vantajoso capturar com HP baixo e o de trás
> vir fundir em cima: aumenta o HP, o cap power e a sobrevida do soldado para
> resistir a novos ataques.*

**A mecânica (HOJE, `TurnStateManager.Merge.cs`, linhas 592–669):** a unidade
**selecionada anda até o hex do parceiro**; o parceiro é consumido (*"morto
porque fundiu"*) e a selecionada fica no hex dele, com o HP somado (teto 10),
marcada como *já agiu*. Não há filtro de "o parceiro já agiu?", então dá para
fundir em quem já agiu neste turno.

```text
A, ferido, no prédio   → captura (aplica o cap power desta rodada)
B, ferido, atrás       → selecionado, anda até A e funde: A some, B fica NO PRÉDIO
                         com o HP somado → mais cap power na próxima rodada e mais sobrevida
```

**Não custa rodada de captura.** O custo é **presença**: um capturador a menos
para ocupar outro prédio. Por isso só entra quem **não tem outro trabalho**. A
Marcha já dizia:

> *Dois relógios cansados, / sem trabalho a cumprir, / viram um só bem forte. /
> Mas se há dois destinos, / cada qual toma um chão: / não se funde trabalho /
> que trabalha em divisão!*

**DECIDIDO pelo autor em 2026-10-02: a fusão sai do questionário.** Nenhum papel
pergunta "fundir?"; a decisão de fundir é **só do reparo**. A fusão de trabalho
não será escrita. A ação Fundir continua no jogo e no reparo; só deixa de ser
casa de papel.

O motivo: **as outras políticas já cobrem o caso.** Capturador de 5 HP no prédio
e o de trás com 6 HP é candidato ao **Swap** (§6.7): o mais forte assume o
prédio e o de 5 sai do caminho. O resultado é parecido (o prédio fica com quem
tem mais cap power) e mantém as duas peças em campo, sem perder presença.

O que se perde, de olhos abertos: o caso raro acima, de dois feridos **fora** do
modo reparo, não funde nunca.

O questionário passa a ter **nove casas**: sete ações e duas informações. O
`AICasa.Fundir` continua no enum só para descrever o que o código fez quando o
reparo funde.

**Só o Capturador vale a pena fundir** (autor, 2026-10-02). Nos outros papéis a
fusão aumenta o dano mas diminui a presença. E mesmo na retaguarda ela pode ser
ruim: sob invasão, a massa segura o atraso do inimigo, e fundir abre lacuna na
defesa.

**A fusão de reparo virou capacidade do perfil** (feito em 2026-10-02):

```text
AICapabilityPreset.fusaoEmReparo     Todos (regra antiga) · Só capturador · Desligado
Fácil    Todos          funde sempre e erra, que é a regra antiga
Médio    Só capturador
Difícil  Desligado
```

O valor `0` é `Todos`, então os perfis autorados antes do campo não mudam de
comportamento até alguém escolher. A flag `fuseWhileInRepair` da ficha continua
sendo a **permissão da peça**; o perfil decide quem, dentre as permitidas, usa.
A pergunta única é `AIController.PermiteFusaoEmReparo`, lida pelo reparo e pela
logística (que adiava suprir infantaria prestes a fundir; com duas respostas, o
caminhão esperaria uma fusão que o reparo nunca faria).

### 6.5 A casa Capturar — o que ela pergunta

**CONTRATO, decidido pelo autor em 2026-10-02.**

> *"Existe capturável na minha célula ou no meu TÁTICO?"*

Tático é o MP desta rodada; operacional é o MP da rodada seguinte, encadeado
([`papeis.md`](papeis.md), convenção das faixas). **Nenhum raio fixo em hexes.**

**HOJE** há três capturas na entrada do Capturador, e duas já obedecem:

| captura | hoje | contrato |
|---|---|---|
| célula atual (`Capturer.cs:47`) | ✅ | entra na casa |
| perto de rally, antes de embarcar (`Capturer.cs:133`) | ⚠️ tático **e** `distance > 3` — um teto fixo de 3 hexes por cima da banda | o teto sai; vale o tático |
| oportunista no caminho (`Capturer.Helpers.cs:64`) | ✅ varre o tático (`paths`) | entra na casa |

⚠️ **O oportunista não escolhe: pega o primeiro.** `TryFindOpportunisticCapture`
devolve o **primeiro** capturável na ordem do dicionário de caminhos, sem
ranquear. Na casa, a escolha entre vários é do `MelhorCaptura` (consumidor),
nunca da ordem de iteração.

### 6.6 Com plano × sem plano — quem cede

**CONTRATO, decidido pelo autor:**

```text
capturador SEM plano acha um prédio no tático dele
  o capturador COM plano para esse prédio chega nele no TÁTICO (a partir do prédio)
      → o sem plano cede e procura outra coisa
  o com plano só chega no OPERACIONAL
      → o sem plano vai e captura primeiro
```

**HOJE isso já existe**, e é a melhor pista de como uma política se escreve:
`ShouldReserveOpportunisticCaptureForCloserUnit` →
`TryFindAssignedCapturerForCaptureTarget` (`Capturer.Helpers.cs`, linhas
132–218). O capturador alocado ao setor do prédio que alcança a célula **com o MP
desta rodada** fica com ela. E já confere o que o handoff não confere: alocado
que **já agiu**, morto, embarcado ou em reparo não segura nada (linha 203), que é
o caso F do teste da blitzkrieg resolvido aqui.

⚠️ **O código faz uma coisa a mais**, e ela não está na regra do autor: depois
do alocado, **qualquer** aliado que chegue em menos passos também segura o
prédio (linhas 154–173). A medida é **passos**, não *quem fecha primeiro*. Ver
§10.

### 6.7 Swap, Blitz, Vacate e ceder são políticas

**CONTRATO, decidido pelo autor.** Hoje eles rodam **antes** da captura, como
casas (`Capturer.cs`, linhas 28–40). No questionário, viram políticas que
**trocam a ação**:

| política | sobre qual preliminar | troca para |
|---|---|---|
| **Blitz** (handoff) | Capturar | Reposicionar rumo ao próximo prédio (§6.1) |
| **Swap** | Capturar | Reposicionar, saindo do caminho de quem fecha antes |
| **Ceder ao com plano** | Capturar | a próxima opção da casa, ou Reposicionar (§6.6) |
| **Vacate** | **qualquer** | Reposicionar para fora da célula que outro precisa |

O Vacate é o único que não depende de "Capturar": ele dispara quando a peça
**não** está capturando, mas está parada em cima do alvo de outro.

A doutrina já tinha a divisão: *CONTA vira score (swap, handoff, ceder, não
estorvar); GOSTO vira política* ([`Capturador.md`](Capturador.md), §0). No
questionário, as duas moram na etapa de políticas; o que muda é se o número vem
do perfil (gosto) ou da conta de rodadas (conta).

### 6.8 Capturador Combatente é rótulo de consulta

**CONTRATO, decidido pelo autor em 2026-10-02.** `CapturadorCombatente` não tem
política própria. É um **rótulo que classifica para baixo** entre os
capturadores:

> *Um bazooka e um soldado, sem plano, ambos chegam ao prédio: o soldado tem
> preferência, e o bazooka vai fazer outra coisa.*

Cedido o prédio, a casa Capturar do bazooka responde NÃO e o questionário segue
na ordem do papel (Embarcar, Reposicionar para outro prédio, Mirar…). **Não há
"combatente com alvo → Mirar"**: a sexta estrofe da Marcha já dizia *"quando
ninguém captura, ele toma o predinho"*. A chave 0.5 continua onde está, na
construção.

**Quem fica com o prédio**, entre os que chegam nele no tático — uma regra só:

```text
1. o capturador COM plano para esse prédio        (§6.6)
2. Capturador antes de Capturador Combatente      (o rótulo)
3. empate no rótulo → quem FECHA primeiro          (rodadas até cair, pelo cap power)
```

**HOJE são dois lugares, e eles discordam:**

| onde | regra de hoje | contrato |
|---|---|---|
| `CaptureOpportunityClaimService.SortCandidates` (casamento de sem-planos) | rótulo ✅, empate por `InstanceId` | empate por rodadas até cair |
| `ShouldReserveOpportunisticCaptureForCloserUnit` (cessão do oportunista) | ✅ só compilou (2026-10-03): com plano → rótulo → rodadas até fechar; empate total, o oportunista fica | — |
| `FindSwapIncomingCapturer` (Swap) | ✅ só compilou (2026-10-03): troca só se o candidato fecha em **menos** rodadas; empate não troca | — |

O passo 3 é o verso *"não disputa a cidade com quem fecha primeiro"*.

**A régua é `RodadasAteFechar`** (`Capturer.Helpers.cs`): pontos que faltam ÷
cap power **neste prédio**, pela conta do sensor (`PodeCapturarSensor.GetCapturePower`:
HP × eficiência da chave, com a penalidade de pré-requisito). Sem chave para a
construção, nunca fecha. Vale igual em todos os perfis: é correção, não
esperteza. Falta o casamento de sem-planos, cujo desempate segue `InstanceId`:
lá a ordenação é por unidade, não por prédio, e a régua por prédio não cabe sem
mexer no algoritmo.

### 6.9 Defensor e Rally são políticas que leem a postura

**CONTRATO, decidido pelo autor em 2026-10-02.** A
[`revisao_papeis.md`](../revisao_papeis.md) já os chamava de **modificadores de
contexto, não ações**. No questionário, não ganham casa: são políticas que leem a
postura e mudam a preliminar.

| contexto | preliminar | a política faz |
|---|---|---|
| **Defensiva** (setor já conquistado) | Reposicionar | escolhe ficar em cima do prédio conquistado |
| **Rally montando massa**, peça dentro do raio | Embarcar | vira NÃO: sair do raio atrasa o GoGreen, e a presença conta |

> *Não guardo uma bandeira, / eu guardo a produção.*

**HOJE** os dois existem como ramos próprios: o modo Defensor quando o setor
não tem mais capturável (`Capturer.cs:211`), e a captura de rally e a defesa do
prédio próprio antes de embarcar (`Capturer.cs`, linhas 60–64).

### 6.10 Regras que saíram desses exemplos

- **Ninguém pede olho para o que alcança sozinho.** É a mesma forma de *"ninguém
  pede táxi sem saber para onde vai"*. Pedir ajuda nasce da **falha** de uma
  casa de ação, não da vontade de saber. O pedido de spotting do §6.3 é
  exatamente isso: a casa Capturar falhou **só** por falta de informação.
- **O spotter só vale para quem ainda não agiu.** A informação chega no Neutral
  seguinte. Por isso o pedido adia o capturador em vez de reordenar a
  iniciativa.

---

## 7. A ordem de cada papel

**CONTRATO:** o desenho do autor de 2026-10-02 é a ordem canônica. Onde ele
diverge do §7.8 da ficha, **este vale**, e a ficha precisa ser corrigida.

### 7.1 Os seis papéis

| papel | subpapéis | lema | magnético | especial |
|---|---|---|---|---|
| **Capturador** | skill Captura e Capturador Alternativo | antes do tiro, vem o dinheiro | construções capturáveis | blitzkrieg, captura oportunista |
| **Transportador** | Pickup, Courier | eu sou o táxi e acelero sua missão | Pickup: quem pede carona ou o capitão · Courier: o local da entrega | só entra em reparo por falta de autonomia e munição |
| **Assalto** | Assalto, Interceptador Aéreo, Ataque Aéreo | tá difícil seguir em frente? me chama | vanguarda do capitão | repara na vanguarda se for elite |
| **Fogo de Suporte** | Fogo de Suporte, Antiaéreo, Híbridas | amacio a vanguarda de longe | retaguarda e/ou flancos do capitão | auto-repelir (FS); tático = alcance da arma |
| **Vigilância** | Vigilância Aérea, Raid anti-submarina (ASW) | onde está minha presa? | capitão, melhores spots | auto-repelir (ar); caça sub em grupo |
| **Logística** | Logística de Campo, Estoque | acabou a gasolina? tô indo | unidade ferida, retaguarda | manutenção crítica e preventiva; HUB |

### 7.2 As ordens

**"Papéis em condições normais"** (desenho do autor, 2026-10-02): é a coluna
**padrão**, o modo de ataque. As missões (§11) trocam a coluna enquanto duram.
Nove casas: Fundir saiu de todas (§6.4).

```text
#   Capturador    Pickup        Courier       Assalto       Fogo Suporte  Vigilância    Logística
1   Capturar      Embarcar      Embarcar      Detectar      Detectar      Detectar      Enxergar
2   Enxergar      Reposicionar  Reposicionar  Mirar         Enxergar      Reposicionar  Suprir
3   Detectar      Detectar      Suprir        Embarcar      Mirar         Mirar         Transferir
4   Embarcar      Mirar         Transferir    Reposicionar  Reposicionar  Suprir        Reposicionar
5   Reposicionar  Transferir    Enxergar      Capturar      Embarcar      Transferir    Embarcar
6   Mirar         Suprir        Desembarcar   Transferir    Transferir    Desembarcar   Desembarcar
7   Suprir        Capturar      Detectar      Suprir        Suprir        Embarcar      Mirar
8   Transferir    Enxergar      Mirar         Desembarcar   Desembarcar   Enxergar      Capturar
9   Desembarcar   Desembarcar   Capturar      Enxergar      Capturar      Capturar      Detectar
```

### 7.2b O Transportador: dois MODOS, não dois papéis

**CONTRATO, desenho do autor em 2026-10-03.** Pickup (vazio) e Courier (com
carga) são o **mesmo papel** em dois estados. É o mesmo mecanismo das missões: o
estado da peça troca a coluna, com as mesmas casas. O modo sai de um **fato** (tem
carga ou não), não de um campo.

| | Pickup (vazio) | Courier (com carga) |
|---|---|---|
| magnético | quem pede carona, ou o capitão | o local da entrega |
| especial | só entra em reparo por falta de autonomia ou munição | idem |

**A casa Embarcar do transportador é ele mesmo embarcando num transporte maior**
— o embarque aninhado. Pela regra do jogo, embarcar é sempre ação de quem
embarca: o APC ao lado de um soldado não o recolhe; quem escolhe embarcar é o
soldado. O exemplo do autor:

> *O APC está na ilha, pega um ferido (EVAC), vai até a praia e EMBARCA no navio
> de transporte. O navio entra em modo hospital, porque nos passageiros
> aninhados há um ferido dentro de outro transportador, e cruza o canal de
> volta para a base.*

(A fragata leva os Apaches; quem leva o APC é o navio de transporte, `MA Desembarque`.)

**O modo é um fato da carga INTEIRA, em todos os níveis.** Ferido em qualquer
ponto da carga = modo hospital; carga em qualquer nível = Courier.

**HOJE, pela metade:** o EVAC procura o ferido só entre os passageiros
**diretos** (`passengers.Find(p => p.IsUnderRepair)`, `Transportador.Courier.cs:91`
e `Transportador.Air.cs:65`) — o navio vê um APC são e não entra em modo
hospital. O **destino** sobe por herança: *"o navio lê do APC, exatamente como o
APC lê o soldado"* (`Courier.cs`, comentário do autor de 2026-08-07). Se o APC em
EVAC declarar o hospital como destino, o navio leva até lá — mas viajando como
táxi comum, sem a prioridade e a cautela de hospital. Não conferido se o APC em
EVAC declara esse destino.

**O Courier com Suprir em 3º é o modo hospital** do supridor que também
transporta (CLAUDE.md, "Hospital mode"): a exceção que hoje mora no topo do
Router é, no desenho, só a ordem natural da coluna.

**Com carga, não entra em combate; com ameaça, foge** (autor, sobre o
porta-aviões): dois caças de 1 HP lá dentro valem mais que o tiro no
bombardeiro — perder o casco perde todos. A moeda do papel são as vagas.

### 7.3 Os híbridos são cadeia, não papel

```text
Fogo de Suporte híbrido   Detectar → Enxergar → Mirar (como FS)
                          sem solução → entra no Detectar do Assalto
```

É a cadeia dentro da coluna da [`revisao_papeis.md`](../revisao_papeis.md)
(o "Labradoodle"). O turno seguinte roda o questionário do começo, então **não é
preciso caminho de volta**.

**CONTRATO, decidido com o autor em 2026-10-03.** Dois eixos, e a seta só mexe
num deles:

```text
COMO LUTA   vem da ARMA       combatente · artilheiro · híbrida (a seta)
ONDE FICA   vem da ESSÊNCIA   o magnético da própria coluna; a seta nunca muda isso
```

- **Assalto híbrido** (Obus Leve, Tanque Z): as três casas do FS (Detectar,
  Enxergar, Mirar **parado**) e, falhando, a coluna do Assalto. Mora na vanguarda.
- **FS híbrido**: a coluna do FS e, no Mirar, a perna do contato. Mora na
  retaguarda e flancos.
- **O AAA** mora com o que protege e sai para o bombardeiro que entra no tático
  **dele** (movimento + alcance 1). Com arma 1~1 ele **não é híbrido**: é
  combatente puro com endereço de antiaéreo. O rótulo decide o endereço; a arma
  decide como luta. Com 1~2, vira híbrido pela ficha da arma, sem código.

**Cada perna usa a sua banda:** a perna FS, a da **arma** (min–max, a inversão
do artilheiro); a perna Assalto, a do **movimento** (MP + alcance).

**A modalidade vem da arma, não de campo da ficha** (`UnitCombatModalityRules`):

```text
alcança o contato (mín ≤ 1) E a distância (máx ≥ 2)   → Híbrida   (numa arma ou somando armas)
só a distância                                        → Artilheiro
só o contato                                          → Combatente
```

Lê o **mesmo alcance do `PodeMirar`** (`UnitEmbarkedWeapon.GetRangeMin/Max`, o da
unidade, não o default da `WeaponData`). A munição não entra: ela muda a resposta
do Mirar no turno, não a identidade.

**A perna FS pergunta "tiro parado que VALE", não "tiro parado possível".** O
Tanque Z (canhão 1~2, metralhadora 1) é péssimo de canhão contra infantaria: com
um infante a 2, ele não atira de longe — vai para cima com a metralhadora.
Quem compara é o `MelhorCombateService`, que já mantém os dois rankings
separados (`StationaryRanking` × andar-e-atirar, modo `Hybrid`). Dar alcance 2 à
metralhadora mudaria a **regra do jogo** para consertar a **IA**: descartado.
A cadeia do híbrido **depende** do MelhorCombate; sem ele, o FS-primeiro faria o
Tanque Z gastar o canhão no infante.

**A prévia (lida das fichas em disco em 2026-10-03; a fonte é a janela Tools ▸
Auditoria ▸ Papéis e Capacidades):**

| achado | fichas | leitura |
|---|---|---|
| híbridas **sem** o campo "FS antes" | Bombardeiro F (1~2), Caça F (1~2), Submarino (1~3) | **mudariam de comportamento** com a troca — testar uma a uma |
| campo "FS antes" **sem** arma de distância | Bazooka, Metranca | campo inerte: sem arma ≥2 a perna FS não tem o que dar |
| híbridas **com** o campo | Obus Leve, Tanque Z | não mudam |
| `longRangeStationary` ≠ artilheiro | Astros, Obus Médio, SAM, Destroyer, Porta-Aviões (artilheiros sem o campo); Radar Móvel (com o campo, sem arma) | **o campo não é a modalidade**: ele diz "não reposiciona depois de comprado" — a Artilharia de Campanha (rebocada) e o Radar Móvel. É mobilidade, não arma. **Não substituir** |

Conclusão: só `preferArtilleryModeBeforeCombatant` é candidato a virar leitura da
arma. `longRangeStationary` fica como está.

**FEITO em 2026-10-03 (só compilou):** o campo saiu do `UnitData`, e os leitores
perguntam `UnitCombatModalityRules.IsHybrid`. O autor confirmou a causa do
Bazooka e da Metranca: *foram nerfados de 1~2 para 1 e o campo ficou esquecido* —
nerfou a arma, o comportamento não mudou, sem erro nenhum. Agora **nerfou a arma,
mudou o comportamento.**

⚠️ **A previsão de que os três furtivos mudariam estava errada.** A cadeia do
híbrido no Router só roda para quem **satisfaz Fogo de Suporte**
(`PreferFireSupportBeforeAssault`): o Obus Leve e o Tanque Z são
`ArtilheiroCombatente` e satisfazem; o Caça F (Interceptador), o Bombardeiro F
(Ataque Aéreo) e o Submarino (Vigilância), não. Na tática, **nada muda**.

Onde muda: o "é fogo de suporte?" do shopping (`AIShoppingPlanner.Demand.cs`, duas
contagens) e da logística (`Logistics.Supply.cs`, dois bônus de prioridade). Ali a
pergunta virou "híbrido **e** satisfaz Fogo de Suporte" — o mesmo conjunto que o
campo marcava, **menos o Bazooka e a Metranca**, que deixam de contar como fogo
de suporte. Sem o "e satisfaz", os três furtivos entrariam nessas contas sem
ninguém pedir.

Consequência para o desenho do autor: a seta *"o assalto puxa as pernas do FS"*
**ainda não existe para o Assalto puro híbrido** — só para o rótulo
`ArtilheiroCombatente`. Um Caça F que tente o tiro a 2 antes do contato precisa
da cadeia valendo pela **modalidade**, não pelo rótulo, e isso depende do
MelhorCombate (a perna FS que pergunta "vale?").

### 7.4 O que mudou em relação ao §7.8 da ficha

| papel | §7.8 | desenho | leitura |
|---|---|---|---|
| Capturador | Enxergar, Detectar, **Capturar**… | **Capturar**, Enxergar, Detectar… | "capturar **agora**" é fato do prédio ao alcance; não depende de visão. Enxergar passa a filtrar Embarcar e Reposicionar |
| Pickup | Enxergar em 3º | Enxergar em 8º | — |
| Courier | Detectar em 4º, antes de Suprir | Suprir e Transferir sobem; Detectar em 7º, **depois** de Desembarcar | ⚠️ ABERTO: o §7.8 chamava Detectar de *precondição* com carga |
| Vigilância | Detectar, **Mirar, Reposicionar**… Enxergar em 10º | Detectar, **Reposicionar, Mirar**… Enxergar em 8º | — |
| Logística | …Mirar, Fundir… | …Fundir, Mirar… | — |

### 7.5 ABERTO — as cores do desenho

O desenho pinta as casas de verde, amarelo e vermelho. A leitura provável é
agenda do papel / condicional / raro, mas **o autor não confirmou**. Fica
pendente também se o vermelho é perguntado (por último) ou pulado. A revisão de
papéis diz que *"não se aplica" é propriedade da ficha, não do papel*: um
Capturador com `isSupplier` (o field medic) precisa ter a casa Suprir
perguntada.

---

## 8. Onde mora

```text
Router.cs, linhas 28–105    INVARIANTES — produção, vacate, reparo, transporte crítico
                            ── ficam ACIMA do questionário, nunca reordenáveis ──
AIQuestionario              motor genérico: roda a ordem, grava respostas, acha a preliminar
respondente por casa        chama o Pode* e o Melhor* da casa — igual para todo papel
AIController.<papel>        a ORDEM e as POLÍTICAS. Só isso
```

**O que o motor NÃO pode:**

```text
conhecer papel            se aparecer "if capturador" no motor, o n × n voltou
ranquear ou desempatar    é do consumidor (Melhor*), uma camada abaixo
decidir                   a preliminar é mecânica; quem decide é a política
```

Segue as três camadas do CLAUDE.md: o respondente é **consumidor**, o motor e
as políticas são **organizador**, e o sensor continua sendo a fonte de verdade.

A ordem nasce **em código**, versionada com o papel. Ela vira dado (o `RoleData`
do §7 da ficha) depois que as seis colunas rodarem. **Não dá para parametrizar
uma política que ainda não foi extraída.**

---

## 9. Os degraus

```text
1. OBSERVADOR    o questionário roda ao lado do código atual e só anota.
                 Cada peça grava: as dez respostas → preliminar → políticas → final,
                 e ao lado "o código de hoje fez X". Nenhum comportamento muda.
2. CAPTURADOR    decide PELO questionário, atrás de um toggle,
                 com o código antigo como reserva
3. OS OUTROS 5   papel por papel, na mesma forma
```

**HOJE (2026-10-02): o degrau 1 está escrito e compila; ainda não foi visto em
Play.** Só o Capturador.

```text
Questionario/AIQuestionario.cs                      o motor — casas, respostas, preliminar. Sem papel
Questionario/AIQuestionarioLog.cs                   questionario_observador.log na raiz, só no Editor
Questionario/AIController.Questionario.cs           respondentes (iguais p/ todo papel) e o gancho
Questionario/AIController.Questionario.Capturador.cs  ordem e políticas do Capturador
1. Phases/AIController.Phase2.cs                    chama o observador depois da decisão, antes da execução
```

Liga e desliga no Inspector do `AIController` (*Questionário (observador)*). As
políticas anotadas neste degrau são o pedido de spotting (§6.3) e a cessão do
prédio pela regra de hoje (§6.6); as outras aparecem como *não ligadas*. Casas
sem respondente respondem **NÃO SEI**.

O observador vem primeiro por três razões:

- **É o Auditável puro.** *"O questionário diria capturar, o código fez
  mirar."* Cada divergência é uma caixinha de surpresas do `AIController`
  virando visível, que é o critério de sucesso do §7.1 da ficha.
- **Mede o custo antes de alguém depender dele.** Dez casas por peça custam
  mais que a primeira não-nula, e o planejador já leva 7–9 s por turno no
  celular. Medir, não supor.
- **Mostra as casas que não sabem responder.** A grade de "NÃO SEI RESPONDER" é
  a lista ordenada dos consumidores que faltam.

---

## 10b. As políticas no perfil — a escada das três IAs

**CONTRATO, decidido pelo autor em 2026-10-03.** O critério: **esperteza** vai
para o perfil (`AIPresetData` ▸ Papeis / Missoes); **higiene** (Vacate, liberar
produtora) e **correção** (a régua de quem fecha primeiro, o tiro que vale)
ficam sempre ligadas. *"A IA Fácil joga simples: toma decisões elementares."*

| | Fácil | Médio | Difícil |
|---|---|---|---|
| Capturador: Blitzkrieg | off | off | **on** |
| Capturador: Substituição por eficiência | off | **on** | on |
| Capturador: Captura oportunista | on | on | on |
| Assalto: Caçar o alvo preferido | off | **on** | on |
| Fogo de Suporte: Fogo de preparação | off | off | **on** |
| Reparo: Fusão em reparo | Todos | **Só capturador** | **Desligado** |

A escada: do Fácil para o Médio entram as políticas de **uma peça** (trocar pelo
mais eficiente, caçar a presa certa); do Médio para o Difícil, as de **duas
peças** (revezamento no eixo, artilharia preparando antes do assalto).

**O Médio perdeu o Blitzkrieg de propósito** (tinha desde a v8.6.1): *"o médio
fazer blitzkrieg é inteligente demais pra ele."*

O Oportunista fica ligado no Fácil: desligado, o soldado passaria ao lado de um
prédio vazio no caminho, e isso parece bug, não simplicidade.

---

## 11. Missões — a coluna que substitui a normal

**CONTRATO, desenho do autor em 2026-10-02 (quadro "Missões"), em construção.**

Uma missão é *um objetivo com várias maneiras de ser cumprido, preso a uma
unidade até que ela o cumpra ou o perca* ([`contrato_missoes.md`](contrato_missoes.md)).
No questionário, ela é **outra coluna do mesmo papel**: as mesmas casas e os
mesmos respondentes, com outra âncora, outra ordem e outras políticas. O
precedente está no próprio quadro dos papéis: o Transportador já tem duas colunas
(Pickup e Courier) conforme o estado da peça.

Toda missão responde cinco coisas:

```text
ENTRADA    quando a peça entra
QUEM DÁ    a própria peça · o plano · um pedido de outra peça
ÂNCORA     para onde ela vai
POLÍTICAS  o que muda nas casas enquanto durar
SAÍDA      quando ela sai
```

| missão | quem dá | estado |
|---|---|---|
| **Reparo** | a própria peça | desenhada (§11.1) |
| **SOS** (HQ sob ameaça) | o plano | só o nome |
| **Guarnição** (prédio recém-tomado) | o plano | só o nome |
| **Rally** (formação de massa) | o plano | só o nome |
| **EVAC** (carona urgente) | pedido: o ferido pede, o transportador atende | só o nome |
| **Spotting** (revelar hexágonos) | pedido: quem precisa de olho pede (§6.3) | só o nome |
| **Quero Carona** (tradicional) | pedido: o passageiro pede, o transportador atende com a promessa (`Transport`) | só o nome |

Nos **pedidos**, a missão fica com **quem atende**; quem pede só pendura o
pedido. A diferença entre EVAC e Quero Carona é o destino: o EVAC leva o ferido
para o reparo, o Quero Carona leva a peça para o objetivo.

### 11.1 Reparo

**HOJE** já é verbo de missão: `AIPlanRuntimeIntent.Repair = 6`.

| parte | desenho do autor | código |
|---|---|---|
| **entrada** | HP abaixo do limite da unidade, **ou** combustível baixo, **ou** munição zerada | ✅ por ficha: `repairTriggerHpBelow`, `repairTriggerAutonomyPct`, `repairTriggerAmmoPct` (`UnitData.cs`, 222–229). Na autonomia, **0 = desligado** |
| **reparos e posição** | só capturadores fundem | ✅ capacidade do perfil (§6.4) |
| **reparos e posição** | fogo de suporte e híbridos atiram em reparo, no prédio | ✅ só compilou (tabela abaixo) |
| **iniciativa e movimento** | ganha iniciativa na vanguarda ou sobre construção capturável | ✅ só compilou: grupo 1 pela régua da Retaguarda (`IsWoundedInVanguard`); sobre alvo de captura já era grupo 0 |
| **iniciativa e movimento** | prédios preferenciais para elites na vanguarda | ✅ `EliteHoldsDangerousRepair` relaxa a segurança e dá +600 ao prédio avançado (a preferência é por distância, não pelo tático) |
| **iniciativa e movimento** | move para a retaguarda; prédios no caminho | ✅ só compilou: atrás da linha serve mesmo com inimigo a N hexes; à frente da linha é recusado (`FindRepairConstruction` + `ClassificarNaLinha`). "No caminho" é, na prática, **o mais perto** |
| **saída** | HP acima do threshold **e** combustível com autonomia **e** munição | ✅ sai com HP ≥ `repairRecoverHpAbove` **e** nenhum gatilho ativo, munição incluída se o gatilho dela estiver ligado na ficha (`AIController.Repair.cs:64`) |

**Fogo de suporte e híbridos não reparam passivos** (autor, 2026-10-02):

| situação | o que faz | código |
|---|---|---|
| ferido a caminho, fora de prédio de reparo | não atira, **recua** | ✅ `fireSupportInOpenField` pula os tiros de rota |
| no prédio de reparo | **atira de volta, parado** | ✅ só compilou: o tiro parado vem antes do teste do substituto, mesmo sob ameaça (`AIController.Repair.cs`, ramo do prédio atual) |
| elite | fica, **ignorando a vacância do SOS** | ✅ `rejectBaseCluster` só vale para não-elite |

Na forma de coluna, é uma política da coluna Reparo **do Fogo de Suporte**: a
coluna Reparo varia por papel.

**O SOS muda as condições do reparo.** Além de puxar as unidades próximas de
volta, sob pressão ele manda **vagar as produtoras** (base, âncora, HQ) para a
base produzir defensores: o não-elite não repara ali; o elite fica. Pela regra
do autor, **sair do prédio atrás de outro lugar de reparo é coisa do SOS.**

⚠️ **O código tem um segundo motivo de saída**, que não é o SOS: qualquer
unidade reparando num prédio com inimigo a ≤3 hexes **sai** se houver um aliado
são a ≤3 hexes, "para deixar o substituto" (`AIController.Repair.cs`,
`hasReplacement`). Vale para qualquer prédio e qualquer papel. Ver §10.

**A regra, nas palavras do exemplo do autor:** *uma unidade com munição 0 e HP 9
entra em reparo.* A entrada é **OU** (qualquer gatilho dispara); a saída é **E**
(todos resolvidos). Com "OU" na saída, a unidade de HP 9 sem munição sairia no
mesmo turno em que entrou e voltaria no seguinte, num pingue-pongue sem nunca
rearmar.

---

## 10. Em aberto

| # | pergunta |
|---|---|
| 1 | portão ou contexto: quais papéis usam a informação como portão? (§2.2) |
| 2 | o que as cores querem dizer, e se o vermelho é perguntado (§7.5) |
| 3 | Courier com Detectar depois de Desembarcar: intencional? (§7.4) |
| 4 | a política de handoff ganha *"o seguidor ainda não agiu?"* e a reserva do prédio (§6.1) |
| 5 | o questionário responde as dez casas sempre, ou para na primeira ação SIM e grava o resto como "não perguntado"? Decide-se pelo custo medido no degrau 1 |
| 6 | "não se aplica": autorado ou derivado? (herdado da ficha, §7.6) |
| 7 | quem atende o pedido de spotting: o `MelhorSpotting` e a política de aceitar, no questionário do spotter (§6.3) |
| ~~8~~ | ~~`fuseWhileInRepair` em 27 de 35 fichas~~ — **resolvido**: a fusão de reparo virou capacidade do perfil (§6.4); a flag fica como permissão da peça |
| ~~10~~ | ~~a cessão a "qualquer aliado em menos passos" fica?~~ — **resolvido** no §6.8: com plano → rótulo → quem fecha primeiro |
| ~~11~~ | ~~a saída do Reparo inclui munição?~~ — **sim**: entrada OU, saída E (§11.1) |
| 13 | a saída "com substituto" (`hasReplacement`) morre? Pela regra do autor, sair do prédio de reparo é só do SOS; hoje tanque e soldado também saem de prédio ameaçado quando há aliado são por perto (§11.1) |
| 12 | quando duas missões disputam a mesma peça (ferido numa base sob SOS), qual ganha? Hoje o Reparo vem antes no Router (§11) |
| ~~9~~ | ~~a Logística funde?~~ — **não**: só o Capturador vale a pena fundir. O §7.8 da ficha (Logística "ganha") precisa ser corrigido |
