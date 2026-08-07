# Logística — doutrina

Doutrina definida pelo autor em 2026-08-06. Onde o código divergir dela, o código
está errado.

> **Está ferido? Acabou a bala? Aguenta aí que eu tô chegando!**

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |
| ❓ | não conferido |

**Subpapéis:** Logística de Campo, Estoque.

---

## 1. A prioridade

```text
Enxergar, Suprir, Transferir, Reposicionar, Embarcar,
Desembarcar, Mirar, Fundir, Capturar, Detectar
```

## 2. É o espelho exato da Vigilância

```text
Vigilância   Detectar 1º  ...  Enxergar 10º    vive de CONTATO
Logística    Enxergar 1º  ...  Detectar 10º    vive de TERRENO
```

> *"Não se preocupam em detectar o inimigo, pois se ele está tão perto **já é
> tarde demais**."*

Os dois papéis ocupam as pontas opostas da doutrina das duas verdades
(`CLAUDE.md`). Não é coincidência de projeto: é a prova de que `PodeEnxergar` e
`PodeDetectar` respondem perguntas diferentes — **um papel inteiro só precisa de
uma delas, e o outro papel só da outra**.

**Por que `Enxergar` em 1º:** estas unidades são **extremamente frágeis** e
precisam **ver o caminho até o cliente e medir o perigo**.

> *"Se uma unidade de logística avançar pelo terreno preto, ela está sendo
> imprudente — vai que tem um monte de tropas ali."*

---

## 3. Os dois subpapéis

### 3.1 Logística de Campo — o serviço

Converte **galões, caixas e peças** em serviço no campo: reabastecimento,
rearmamento e cura, **por um preço**.

> **Sem matéria-prima, sem serviço.**

### 3.2 Estoque — o movimento

Movimenta galões, caixas e peças **de e para** unidades e construções.

```text
Hub        movimenta entre si E para recebedores
Recebedor  SÓ recebe
```

✅ É a cadeia direcional do `PodeTransferir` (ver a nota de projeto sobre a cadeia
logística: tiers Hub/Receiver, infinito é fonte e nunca ralo, praia é baldeação
navio↔caminhão).

⚠️ **A IA não opera a cadeia hoje** — está registrado como frente própria em
`docs/ideias_futuras.md` item 3.

---

## 4. Posicionamento — bifurca pelo `playConservative`

| tem `playConservative` | não tem |
|---|---|
| procura a **retaguarda**, como um fogo de suporte faria, e **atende ou recusa atendimento** | vai atrás da **unidade crítica**, da **manutenção preventiva** ou do **EVAC** |

É a terceira vez que este flag bifurca um papel (ver `Vigilancia.md` §4.3 e
`Transporte.md`). ⚠️ Vale o mesmo aviso: hoje o flag e a família da unidade
tendem a andar juntos, então política construída sobre ele pode passar pelo
motivo errado.

---

## 5. A ordem do trabalho

```text
1. suprir o aliado ferido
2. ou transferir recursos para um supridor prestar serviço em campo
3. reposicionar na direção do ferido — podendo PEDIR CARONA, embarcando em navios
```

### 5.1 A triagem — não é o mais ferido, é o mais decisivo

> *"Não atendo só o mais ferido: **atendo o mais decisivo**."* — Marcha, estrofe 3

❌ **Regra nova**, veio com a Marcha e não estava escrita em lugar nenhum. A fila
**não** é ordenada por dano. É ordenada por **capacidade devolvida × o que aquele
papel perde parado** — e a Marcha enumera exatamente isso, papel por papel:

| verso | o que está sendo medido |
|---|---|
| *"o Capturador sem corpo perde tempo e produção"* | HP **é** a taxa de captura (§6) — atender devolve renda |
| *"a aeronave sem combustível perde o céu"* | autonomia zerada força pouso de emergência |
| *"o canhão sem suas caixas já não fecha o corredor"* | a formação do Fogo de Suporte deixa de cobrir |
| *"o blindado de elite pode segurar o agressor"* | a arma do Assalto é a ameaça que existe |

**As quatro linhas são as moedas dos outros papéis** (`ficha_do_papel.md` §7.8).
A triagem não precisa de tabela nova: ela **lê a moeda de quem pede**.

### Precedência quando embarcado

> *"Se transportando, seguem a agenda do transportador **apenas se não houver
> serviço de suprimento por fazer**."*

**A agenda do passageiro vence a do transportador** enquanto houver cliente. É a
única precedência invertida entre papéis registrada até aqui — e casa com o lema
do transporte (*"o transportador serve a carga"*): se a carga tem trabalho, o
trabalho manda.

---

## 6. A moeda: o ESTOQUE — e ela FUNDE

> *"Aceitam fundir pra produzir uma unidade nova pra durar mais tempo, já que os
> estoques são distribuídos para a nova unidade por **média ponderada**."*

| papel | onde o valor mora | fundir |
|---|---|---|
| **Capturador** | o corpo — HP é a taxa | **ganha** |
| Transportador | as vagas | perde |
| Assalto | a arma | perde |
| Fogo de Suporte | a formação | perde, e agrupar também |
| Vigilância | a origem do cone — *"cada casco é nova origem"* | perde |
| **Logística** | **o estoque** | **ganha** |

**Só dois papéis fundem, e por razões diferentes:**

```text
Capturador   HP É a taxa de captura      -> concentrar ACELERA
Logística    o estoque é CONSERVADO      -> a media ponderada nao perde nada,
             na fusao                       e o casco novo dura mais
```

Os outros quatro perdem algo **insubstituível** ao fundir: uma vaga, uma arma, um
nó da malha, um pedaço de área coberta. A logística não perde nada — **o estoque
atravessa**.

---

## 7. O resto quase não acontece

**Podem lutar, mas raramente lutam.** **Raramente capturam.** E **não detectam** —
§2.

---

## 8. Leituras

| documento | por quê |
|---|---|
| `ficha_do_papel.md` §7.8 | o quadro canônico dos papéis e as moedas |
| `Vigilancia.md` | o papel espelho: `Detectar` 1º, `Enxergar` 10º |
| `Transporte.md` | o modo Hospital, e por que a agenda da carga vence |
| `docs/ideias_futuras.md` item 3 | a cadeia de transferência que a IA ainda não opera |
| `CLAUDE.md`, "As duas verdades" | por que os dois papéis-espelho existem |

---

# Apêndice — Marcha da Logística

Escrita pelo autor em 2026-08-06. **Ela é a doutrina**, e vale a regra do
cabeçalho: **onde o código divergir de um verso, o código está errado.**

Sexta e última marcha. **É a única que descreve os outros cinco papéis.** As
outras cinco falam de si; esta é escrita da posição de quem serve todo mundo,
então seus versos são indexados por **o que cada papel perde quando para**:

```text
"o Capturador sem corpo perde tempo e producao"     -> a moeda do Capturador (HP e a taxa)
"a aeronave sem combustivel perde o ceu"            -> pouso de emergencia, autonomia
"o canhao sem suas caixas ja nao fecha o corredor"  -> a moeda do Fogo de Suporte (a formacao)
"o blindado de elite pode segurar o agressor"       -> a moeda do Assalto (a arma)
```

Isso não é ornamento. É **a função de triagem**, e ela estava faltando na §5.

| verso | o que ele fixa |
|---|---|
| *"Antes de seguir caminho, / eu preciso enxergar"* | `Enxergar` em 1º, literal |
| *"não atravesso a névoa / como um tanque de Assalto"* | a §2 dita como **contraste explícito** com outro papel |
| *"Não atendo só o mais ferido: / atendo o mais decisivo"* | **a triagem** — regra nova, promovida à §5.1 |
| *"quem distribui mantém o fluxo, / quem recebe encerra a mão"* | a cadeia direcional Hub/Recebedor — *"encerra a mão"* é o nó terminal que **nunca é fonte** |
| *"A viagem serve à cadeia. / A cadeia não serve ao mar."* | a precedência invertida da §5 — o transporte é **meio**, nunca motivo |
| *"não apaga uma arma-chave, / nem destrói uma formação"* | por que **esta** moeda funde: nomeia as duas que não fundem |

## O par que se completa em dois eixos

```text
Vigilancia   "dois cacadores separados valem mais que um so melhor"    NAO funde
Logistica    "melhor um posto que alcance do que dois prestes a cair"  FUNDE
```

**A mesma comparação, com o veredito oposto.** Os dois papéis já se espelhavam em
`Enxergar`/`Detectar` (§2); agora se espelham também na fusão — e nos dois casos
a resposta sai da moeda, não do HP.

---

**[Introdução]**

Abram a estrada! / Deixem-me passar! / Quem ficou sem força / não pode mais esperar!

Galões! Caixas! / Peças para entregar! / Aguenta firme, companheiro, / eu já estou a caminho!

**[Estrofe 1]**

Não tomo a cidade, / não procuro o agressor; / eu devolvo movimento, / munição e vigor.

Onde o motor se cala, / onde a arma quer parar, / eu transformo o meu estoque / em vontade de lutar.

Sem matéria-prima, / não existe solução; / cada peça tem um preço, / cada caixa, uma missão.

**[Refrão]**

Aguenta aí! / Eu estou chegando! / Se acabou a bala, / eu já venho rearmando!

Aguenta aí! / Não deixa a linha cair! / Tenho galão, caixa e peça / para a tropa prosseguir!

Eu não ganho a batalha, / mas não deixo ela parar: / sou a força que devolve / a capacidade de lutar!

**[Estrofe 2 — enxergar]**

Antes de seguir caminho, / eu preciso enxergar; / terreno preto esconde / quem me pode emboscar.

Minha carga vale vidas, / meu veículo é frágil; / não atravesso a névoa / como um tanque de Assalto.

Vejo a rota, vejo a volta, / vejo onde vou atender; / se o perigo fecha a estrada, / outro plano há de nascer.

**O valente entra no escuro. / Eu preciso sobreviver.**

**[Estrofe 3 — suprir]**

Quem precisa mais depressa? / Quem não pode esperar? / O ferido, o sem munição, / quem não tem como avançar.

O Capturador sem corpo / perde tempo e produção; / a aeronave sem combustível / perde o céu e a operação.

O canhão sem suas caixas / já não fecha o corredor; / o blindado de elite / pode segurar o agressor.

**Não atendo só o mais ferido: / atendo o mais decisivo.** / Prontidão devolvida / mantém o exército vivo!

**[Refrão]**

Aguenta aí! / Eu estou chegando! / Se acabou a bala, / eu já venho rearmando!

Aguenta aí! / Não deixa a linha cair! / Tenho galão, caixa e peça / para a tropa prosseguir!

**[Estrofe 4 — estoque]**

Do depósito para o hub, / de um hub para outro então; / quem distribui mantém o fluxo, / quem recebe encerra a mão.

Galões viram movimento, / caixas viram fogo e ação, / peças viram permanência / onde havia interrupção.

Estoque longe é promessa, / estoque perto é solução; / o recurso só tem valor / quando alcança a operação.

Transfere! / Abastece! / Não deixa a cadeia quebrar! / Sem matéria-prima em campo, / não há serviço a prestar!

**[Ponte — manutenção e EVAC]**

Nem sempre espero a pane / para então me aproximar; / manutenção preventiva / evita a tropa parar.

Mas se a frente ficou quente / e não dá para atender, / eu preparo a retirada / para o ferido não morrer.

Chama o táxi! / Puxa para trás! / Leva o homem para a base! / Salva o corpo, salva a arma, / salva o tempo da unidade!

**[Estrofe 5 — reposicionar]**

Se não há serviço agora, / vou para onde ele estará; / sigo o crítico, o estoque, / quem precisa evacuar.

Posso ir até a retaguarda, / posso ao cliente me lançar; / mas primeiro vejo o risco / e se é seguro aproximar.

Se o oceano corta a rota, / peço carona para cruzar; / mas não deixo um aliado perto / só para longe viajar.

**A viagem serve à cadeia. / A cadeia não serve ao mar.**

**[Estrofe 6 — fusão]**

Dois veículos avariados / podem juntos resistir; / média o estoque, soma a força, / faz um novo prosseguir.

Aqui fundir preserva / o serviço e a duração; / não apaga uma arma-chave, / nem destrói uma formação.

Melhor um posto que alcance / do que dois prestes a cair; / a Logística se reorganiza / para continuar a servir.

**[Chamada e resposta]**

— Quem traz combustível? / — A Logística!

— Quem devolve munição? / — A Logística!

— Quem busca o ferido? / — A Logística!

— E quando tudo vai parar? / — Aguenta aí, que eu vou chegar!

**[Refrão final]**

Aguenta aí! / Eu estou chegando! / Se acabou a bala, / eu já venho rearmando!

Aguenta aí! / Não deixa a linha cair! / Tenho galão, caixa e peça / para a tropa prosseguir!

Eu não tomo a vanguarda, / nem persigo o invasor; / mas sem mim o tanque para, / cala o canhão, cai o aviador!

Aguenta aí! / Ainda dá para lutar! / Eu enxerguei o caminho / e já estou para chegar!

**[Coda]**

Galões! / Caixas! / Peças na mão!

Ferido atendido! / Arma em condição!

A tropa já estava parando...

**mas a Logística chegou!**
