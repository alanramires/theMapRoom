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
| Vigilância | a área coberta e a idade dela | perde |
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
