# Academia · Bloco Caserna — primeiras aulas

Três aulas para os três quadrantes que já existem:
- **Campanha Soldado:** Aula 1 e Aula 2;
- **Campanha Cabo:** Aula 1.

Sintaxe completa em [`sintaxe.md`](sintaxe.md).

**Como usar este arquivo.** Cada aula traz um esboço do mapa com **marcas** (`{A}`,
`{B}`...). Pinte o quadrante, escolha os hexes e troque cada marca pela coordenada
que aparece na **cena de autoria** (ex.: `{A}` → `60,32`). O roteiro fala nesse
referencial; a Batalha converte.

**Regra de ouro de cada aula:** uma chave por aula. Mapa minúsculo, exército fixo,
um objetivo que doa se feito errado. O aluno é sempre o **slot 0**; o inimigo, o
slot 1.

---

## Soldado · Aula 1 — "Operação Cabeça de Ponte"

> *A Fábrica de Porto Ferro produz os blindados que a Metalion joga contra nós no
> continente. Do mar ela é intocável: a costa é mata fechada, e só um trecho de
> areia, ao pé da fábrica, aceita pouso. A Capitã Moura leva vocês dois até lá no
> Chinook. Desembarcam, entram na fábrica e não saem até a bandeira ser nossa.
> A guarnição da Metalion vai acordar no primeiro tiro de captura. Segurem.*
>
> — Sargento Ávila, briefing da Operação Cabeça de Ponte

**Ensina:** transporte aéreo (voar, pousar, desembarcar); capturar leva turnos;
capturar e defender ao mesmo tempo; HP é poder de captura (soldado ferido captura
menos).

**Unidades iniciais (na cena de autoria, assadas):** Chinook (slot 0) em {C}, com
**2 soldados embarcados**. Embarque-os no Editor e refaça o bake.

**Regras:** `forcarRegras` = sim, `regras` = **A Montanha Avacalha** (tudo ligado
exceto névoa). **Inimigo:** `IA de verdade`. A Metalion não tem QG no quadrante, então é
facção **rebelde**: nunca produz e é puxada pelo magnético do capitão até a
fábrica.

**Combustível:** o Chinook voa 6, começa com **meio tanque (30)** e a **exatos 5
hexes** de {L}. Pousa no primeiro turno se for direto; se ficar passeando, a conta
aperta.

**Mapa** (quadrado; mar à esquerda e no centro, praia no canto superior direito):

```text
F F F F F F . {P}{P}     F = floresta densa na diagonal superior (fecha o pouso)
~ ~ F F F . . {P}{X}     {X} = Fábrica "Porto Ferro" (neutra, 30 de captura)
~ ~ ~ F F . {L}{P} .     {L} = a areia/praia: a ÚNICA área de pouso
~ ~ ~ ~ F . . . .        {P} = hexes vizinhos da fábrica (zona de desembarque)
~ ~ ~ ~ ~ ~ . {M2} .     {M1}/{M2} = spawns da guarnição, acima e abaixo do L
{C}~ ~ ~ ~ ~ ~ ~ ~       {C} = Chinook, no canto inferior esquerdo, sobre o mar
```

**Tarefas**

| key | id | parameters | description | flags |
|---|---|---|---|---|
| `sold_1_01` | `UNIT_AT_HEX` | `SD && ({P} \|\| {P} \|\| {P})` | Desembarque as tropas junto à fábrica | startHidden |
| `sold_1_02` | `CAPTURE_PROGRESS` | `SD && Porto Ferro` | Comece a capturar a fábrica | startHidden |
| `sold_1_03` | `CAPTURE_CONSTRUCTION` | `Porto Ferro` | Tome a Fábrica de Porto Ferro | startHidden |
| `sold_1_04` | `PLAYER_ELIMINATED` | | Não perca todas as unidades | **isDefeatCondition** |
| `sold_1_05` | `ENEMY_CAPTURE` | `Porto Ferro` | Não deixe a Metalion tomar a fábrica | **isDefeatCondition** |

**Roteiro**

| # | text | advance | objectiveKey / reveal | spawnCommand | statCommand | turn | movement |
|---|---|---|---|---|---|---|---|
| 1 | Porto Ferro, à vista. A costa é mata pura: só a areia ao pé da fábrica aceita pouso. E o tanque está pela metade. [ordem]Leve o Chinook até a praia.[/ordem] | All Units Acted | | | `CH fuel=30; pan Porto Ferro` | Locked | Unlocked |
| 2 | Chinook no ar não larga ninguém. [enfase]Pouse antes de desembarcar[/enfase]. [ordem]Passe a vez e, no próximo turno, pouse e desembarque junto à fábrica.[/ordem] | Objective Completed | `sold_1_01` ✓ | | | Unlocked | |
| 3 | Em terra. A fábrica tem [enfase]30 de captura[/enfase]; um soldado inteiro tira 10 por turno. [ordem]Entre na fábrica e comece a captura.[/ordem] | Objective Completed | `sold_1_02` ✓ | | `pan Porto Ferro` | | |
| 4 | Contato! A guarnição acordou. [enfase]Quem captura não atira[/enfase]: o outro recruta cobre. [ordem]Segure a fábrica e termine a captura.[/ordem] | Objective Completed | `sold_1_03` ✓ + revela `sold_1_05` | `slot1 SD @flag; slot1 SD @flag` | `pan Porto Ferro` | | |
| 5 | Porto Ferro é nossa. Sem ela, os blindados da Metalion param no estaleiro. [enfase]Transporte, desembarque, captura e cobertura[/enfase]: vocês acabaram de fazer uma operação inteira. | Immediate | | | | | |

**Spawn em L por bandeiras:** `flag#1` e `flag#2`, construções ocultas
(`isVisible` desligado) nas pontas dos braços do L. `@flag` sorteia uma livre
para cada soldado. **Bandeira ocupada no chão não recebe spawn; helicóptero no ar
não bloqueia.** Se um recruta estiver "guardando o caixão" numa ponta, só nasce
um inimigo. Como a vitória é capturar, um dos dois recrutas precisa estar na
fábrica, e no máximo um pode guardar uma ponta. Com mais bandeiras no mapa, a
guarnição muda de lugar a cada partida.

**Derrota `sold_1_04` (sem unidades)** fica visível desde o início (sem
`startHidden`): perder o Chinook com os dois dentro, por combustível ou abatido,
também é derrota.

**Por que funciona:** a captura leva 3 turnos com o soldado inteiro, e mais se ele
for ferido. A guarnição chega no meio. O aluno descobre sozinho que precisa de um
soldado capturando e outro cobrindo, e que deixar o capturador apanhar alonga a
captura.

---

## Soldado · Aula 2 — "Quem tem o morro"

**Ensina:** terreno defende; inspecionar o inimigo antes de agir; atacar de
posição; não perder a unidade.

**Mapa** (7×6):

```text
. . . . . . .
. {A} ^ . . . .     ^ = MONTANHA em {M}, adjacente a {A}
. . . . . {E} .     {E} = soldado inimigo, em planície, a 2 hexes de {M}
. . . . . . .
```

O inimigo precisa de um **AutomataData** novo: `unitToken` = `SD`, time do
slot 1, `preferAttack` = sim, `stationary` = não, e esta aula em `tutorials`.

**Tarefas**

| key | id | parameters | description | flags |
|---|---|---|---|---|
| `sold_2_01` | `INSPECT_ENEMY_UNIT` | | Inspecione o soldado inimigo | startHidden |
| `sold_2_02` | `UNIT_AT_HEX` | `SD && ({M})` | Suba o morro | startHidden |
| `sold_2_03` | `ATTACK_UNIT` | | Ataque do alto do morro | startHidden |
| `sold_2_04` | `DESTROY_ENEMY_UNIT` | | Elimine o inimigo | startHidden |
| `sold_2_05` | `UNIT_DEAD` | `Recruta` | Não perca o Recruta | **isDefeatCondition** |

**Roteiro**

| # | text | advance | objectiveKey / reveal | spawnCommand | statCommand | turn | movement |
|---|---|---|---|---|---|---|---|
| 1 | Contato! Um soldado inimigo na estrada. Antes de atirar, [enfase]conheça o alvo[/enfase]. [ordem]Inspecione o inimigo.[/ordem] | Objective Completed | `sold_2_01` ✓ | `slot0 SD {A} name=Recruta; slot1 SD {E} acted` | `pan {E}` | Locked | Locked |
| 2 | Mesmo soldado que o seu. Na planície, empate. Mas ali tem um morro, e [enfase]quem está no alto apanha menos[/enfase]. [ordem]Suba o morro.[/ordem] | Objective Completed | `sold_2_02` ✓ | | `pan {M}` | | Unlocked |
| 3 | Ninguém desce do morro sem ordem. [ordem]Passe a vez e veja o que ele faz.[/ordem] | Player Turn Started | | | | Unlocked | Hold Only |
| 4 | Ele veio, e pagou caro pela subida. Agora é a sua vez. [ordem]Mire e ataque.[/ordem] | Objective Completed | `sold_2_03` ✓ | | `pan Recruta` | Locked | Attack Only |
| 5 | Repita até ele cair. Do alto. [ordem]Elimine o inimigo.[/ordem] | Objective Completed | `sold_2_04` ✓ | | | Unlocked | Hold Only |
| 6 | Terreno é arma. O mesmo soldado vale mais [enfase]onde ele está[/enfase] do que [enfase]o que ele é[/enfase]. | Immediate | | | | | |

A aula vence ao eliminar o inimigo (as tarefas não opcionais completam). Se o
Recruta morrer, é derrota.

> **Não use o id `tutorial 1 - soldado`** nesta aula: o `TutorialRules` legado
> restaura o HP de todos depois de um ataque nesse id.

---

## Cabo · Aula 1 — "A carona"

**Ensina:** embarcar no APC, que a estrada encurta o caminho, desembarcar perto
do alvo, e que capturar leva mais de um turno.

**Precisa de construção:** uma vila neutra com nome autorado **`Vila`** (nome de
exibição na autoria + rebake).

**Mapa** (9×6):

```text
. . . . . . . . .
. {S}{T} = = = = = .     = = estrada de {T} até perto da Vila
. . . . . . . {V} .      {V} = a Vila (neutra)
. . . . . . {D} . .      {D} = hex vizinho da Vila, onde o soldado desembarca
```

**Tarefas**

| key | id | parameters | description | flags |
|---|---|---|---|---|
| `cabo_1_01` | `HAS_EMBARKED_UNIT` | `APC` | Embarque o soldado no APC | startHidden |
| `cabo_1_02` | `USED_ROAD_BOOST` | `APC` | Leve o APC pela estrada | startHidden |
| `cabo_1_03` | `UNIT_AT_HEX` | `SD && (Vila \|\| {D})` | Desembarque junto à Vila | startHidden |
| `cabo_1_04` | `CAPTURE_PROGRESS` | `SD && Vila` | Comece a capturar a Vila | startHidden |
| `cabo_1_05` | `CAPTURE_CONSTRUCTION` | `Vila` | Tome a Vila | startHidden |

**Roteiro**

| # | text | advance | objectiveKey / reveal | spawnCommand | statCommand | turn | movement |
|---|---|---|---|---|---|---|---|
| 1 | A pé, a Vila fica a três dias. De carona, a um. [ordem]Embarque o soldado no APC.[/ordem] | Objective Completed | `cabo_1_01` ✓ | `slot0 SD {S} name=Recruta; slot0 APC {T}` | `pan Vila` | Locked | Unlocked |
| 2 | Agora o APC. Na [enfase]estrada[/enfase] ele anda mais. [ordem]Leve o APC pela estrada até perto da Vila.[/ordem] | Objective Completed | `cabo_1_02` ✓ | | | | |
| 3 | [ordem]Desembarque o Recruta junto à Vila.[/ordem] Se não alcançar neste turno, passe a vez e continue. | Objective Completed | `cabo_1_03` ✓ | | `pan Vila` | Unlocked | |
| 4 | Um soldado não toma uma vila num estalo. [ordem]Entre na Vila e comece a captura.[/ordem] | Objective Completed | `cabo_1_04` ✓ | | | | |
| 5 | Viu a barra cair? [enfase]Capturar leva turnos[/enfase], e um soldado ferido captura menos. [ordem]Passe a vez e termine a captura.[/ordem] | Objective Completed | `cabo_1_05` ✓ | | | | |
| 6 | A Vila é nossa: ela [enfase]rende dinheiro todo turno[/enfase]. Quem tem mais prédios compra mais exército. | Immediate | | | | | |

A aula vence ao tomar a Vila.

---

## Ideias para as próximas (por patente)

| patente | aula | a chave |
|---|---|---|
| Soldado | Névoa | o que você não vê também te vê: `FOW_REVEAL_UNIT` |
| Soldado | Comprar | fábrica, dinheiro, uma compra: `PURCHASE_UNIT` |
| Cabo | Combustível | o jipe que para no meio do caminho: `UNIT_DEAD` com `AUT=0` como derrota |
| Cabo | Suprimento | o caminhão que reabastece: `SUPPLY_UNIT` |
| Sargento | A arma certa | bazooka contra tanque, soldado contra soldado |
| Sargento | Artilharia | atirar de longe sem ser visto |
| Subtenente | Armas combinadas | artilharia atrás, infantaria na frente |
| Tenente | Primeira partida | a IA de verdade, perfil Iniciante |
