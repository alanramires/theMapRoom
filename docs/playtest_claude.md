# Playtest: Claude como jogador e validador da IA

Iniciado em 2026-11-03. Partida hot seat na cena Batalha: o autor joga no slot 0
(Azul) e o Claude no slot 1 (Amarelo), os dois como humanos, com névoa total.

**Método.** O Claude lê o tabuleiro pelos sensores e serviços do jogo, via
`unity command script eval` (ver `docs/indie/unity_cli_mcp.md`), e não pela
tela. Só consulta inimigo que o próprio slot enxerga (`IsUnitVisibleForSlot`).
Em cada decisão, roda a lógica da IA como se ela estivesse no slot 1, sem executar,
e compara com a própria jogada. **Cada divergência é registrada aqui**, separada em:

- **bug**: a IA erra por um defeito de código;
- **decisão discutível**: a IA segue uma regra de design que, nesta situação, joga mal;
- **gosto**: preferência do Claude, sem argumento forte.

### Como o Claude age: o mesmo caminho do humano e da IA

As jogadas **não** são feitas chamando funções por dentro. Cada ação vira um
`PlayerAction` (os mesmos construtores da IA: `BuildMoveBatch`, `BuildShoppingBatch`,
`BuildEndTurnBatch`...) e vai para `ReplayManager.ExecuteLiveAIBatch(action, fastAI: false)`,
que **emula a entrada ao vivo**: o cursor anda, o menu abre, a escolha é confirmada,
e o autor vê acontecer na tela. Assim o playtest exercita o caminho de verdade —
menu de compra, passar a vez pelo menu —, que é onde a IA já tropeçou antes.

---

## Turno 1 (slot 1)

**Tabuleiro:** espelhado. Cada lado tem QG e duas fábricas leves, e há uma fábrica
neutra colada em cada base. Sete prédios neutros no total, nenhuma unidade em campo.
Caixa de 6000.

### Compra: a IA deixa um produtor parado

| | compra | sobra |
|---|---|---|
| IA (`AIShoppingPlanner.Decide`) | 2 Soldados: QG 12,4 e fábrica 11,3 | 4000 |
| Claude | 3 Soldados, um em cada produtor | 3000 |

**Divergência: decisão discutível.** No turno 1 de uma corrida por sete prédios
neutros, cada capturador a mais começa a render um turno antes, e a renda das
capturas paga a economia que a IA faz. A IA tinha dinheiro (sobram 4000) e um
produtor livre (a fábrica 13,4), e mesmo assim comprou só 2.

Hipótese, ainda não verificada: o pacote de composição (`pacote 2/2/1 cap=2`, nos
logs de shopping) limita os capturadores a 2 por compra, e a reserva para elite
(`SavingPercentualForElite`, `ComputeBlitzFirstArmorReserve`) segura o resto.
Conferir em `AIShoppingPlanner.Demand.cs` quantos capturadores o pacote pede
quando há muitos neutros ao alcance: o número deveria depender dos **prédios a
capturar**, e não ser fixo em 2.

### Divergência de processo (do Claude, apontada pelo autor)

No turno 1 o Claude comprou com `TryAIDirectPurchase` e passou a vez com
`TryOpenEndingTurnConfirmation`, chamados por dentro: o cursor não andou, o menu de
compra não abriu, o menu de passar a vez não foi navegado. A jogada valeu (dinheiro
debitado, jogada registrada), mas **não testou** o caminho que o humano e a IA usam,
e o autor não viu nada na tela. A partir do turno 2, toda ação vai por batch
(ver "Como o Claude age").

---

## Turno 2 (slot 1)

Método corrigido: cada unidade decide **uma de cada vez**, como a IA (que é
sequencial): a IA é consultada com o tabuleiro já mudado pela unidade anterior, e só
então o Claude age, por batch, pelo automata.

### Bug: liberar produtora atropela a captura

| unidade | IA | Claude |
|---|---|---|
| Unit4 (12,4, em cima do QG) | **mover** para 10,5 | **mover e capturar** 10,5 (fábrica 30 → 20) |

A missão da IA estava certa: `[Missao] 4 Capture -> (10, 5, 0) (plano Golf)`. Mas
ela nunca rodou. Antes do papel de capturador, o roteador roda a regra de liberar
produtora:

```text
[Base] 4 libera produtora travada (3/3 ocupadas, cheapest=$1000) via (10, 5, 0)
```

Os três soldados comprados no turno 1 nasceram em cima dos três produtores. A regra
tira o Unit4 de cima do QG com um batch de **mover**, e o destino que ela escolhe é
justamente o prédio que ele devia capturar. A unidade chega à fábrica e não captura:
**um turno de captura perdido**. O sensor confirma que mover e capturar no mesmo
turno é legal (`PodeCapturarSensor` aceita `MoveuAndando`).

Onde mexer: na ação de liberar produtora do roteador (`AIController.Router.cs`,
`regularProductionUnlockAction`). Quando o destino escolhido é capturável pela
unidade (`SimulateCaptureSensor`), usar `BuildCaptureBatch` em vez de
`BuildMoveBatch`. Ou: deixar o papel decidir primeiro, e liberar a produtora só se o
papel não tirar a unidade de cima do prédio.

### Concordância

Unit5 → 9,4 e Unit6 → 12,7: as duas são a melhor célula por custo real até o alvo da
missão (empate com 8,3 e 11,7). Nenhuma divergência.

### Compra: de novo, produtores parados

| | compra | sobra |
|---|---|---|
| IA | Tanque Leve em 11,3 | 3000, dois produtores parados |
| Claude | Tanque Leve + 2 Soldados | 1000 |

Mesmo padrão do turno 1 (decisão discutível). Ainda há neutros sem ninguém indo
(9,8 e 10,12), e a IA prefere guardar 3000 a pôr mais 2 capturadores em campo.
Reforça a hipótese do turno 1: o número de capturadores não acompanha o número de
prédios a capturar.

---

## Correções e verificações depois do turno 2

### Corrigido: sair da produtora captura quando dá

`AIController.Capturer.Vacate.cs`: as duas saídas de produtora (travada e ameaçada)
passam por `BuildVacateBatch`. Se a célula de saída é um prédio que a unidade pode
capturar (`SimulateCaptureSensor`), o batch é de **mover e capturar**; senão, de
mover, como antes.

### Compra: o que o código mostra

O shopping em uso é o por papéis (`UseRoleBasedShopping = true`). Nele, o número de
capturadores vem do **pacote de composição** (`AIShoppingPlanner.Demand.cs`,
"pacote 2/2/1"):

```text
packages   = max(1, ceil(core / 5))
capturador = packages * 2 - capturadores_em_campo
```

Com 0 a 4 unidades de núcleo, o pacote pede **2 capturadores**, não importa quantos
prédios neutros existam. Os slots abertos de captura do plano (`CountOpenSlots`) só
entram no shopping **legado**. No por papéis, o Echo sem capturador no turno 2 não
gerou pedido de capturador, e a IA comprou o tanque.

Isto mexe na doutrina de composição e no gate de elite (decisões do autor). Fica
como proposta, sem alteração: a demanda de capturador seria
`max(pacote, slots de captura abertos no plano)`, limitada pelos produtores livres.

### Falha do ensaio do Claude no turno 1

No turno 1 a compra da IA foi simulada **sem o plano montado** (`BuildObjectivePlan`
só passou a ser chamado no ensaio do turno 2). A IA de verdade monta o plano antes de
comprar. O resultado do turno 1 (2 Soldados) pode ser diferente do que ela faria. O
padrão se repetiu no turno 2, já com plano, então a conclusão se mantém; mas o
turno 1 sozinho não prova nada.

---

## Turno 3 (slot 1)

Partida retomada do save (o `unity recompile` em Play tinha derrubado a sessão — erro
do Claude, anotado na memória: em Play, só `tools/compilar.sh`).

| unidade | IA | Claude | |
|---|---|---|---|
| Unit4 (10,5) | captura no lugar | igual | fábrica 20 → 10 |
| Unit6 (12,7) | move e captura 11,9 | igual | |
| Unit5 (9,4) | **move** para 6,4 | igual | ver abaixo: regra, não bug |
| Unit11 (QG 12,4) | move para **12,3** (para trás) | move para 10,7, rumo à cidade 9,8 | **bug** |
| Unit12 (13,4) | move para 12,7 | igual | melhor célula rumo a 10,12 |
| Tanque (11,3) | move para 11,7 (escolta do Charlie) | igual | |
| compra (7000) | Tanque Leve, sobram 1000 | Tanque Leve + 1 Soldado | o plano tem Bravo com 2 vagas de capturador vazias |

### Regra, não bug: capturar no escuro

O Unit5 alcança a cidade 6,4 mas o sensor recusa a captura: **"Terreno ainda
desconhecido."** Sem névoa, a captura passaria. O hex está no escuro, e destino
provisório na névoa só libera o ataque (os outros sensores esperam o compromisso).
Vale igual para humano e IA: entrar e capturar no mesmo turno um prédio inexplorado
custa um turno. A IA está certa em só mover.

### Bug: liberar produtora ignora a missão da unidade

Mesma regra do turno 2, outro defeito:

```text
[Base] 11 libera produtora travada (3/3 ocupadas, cheapest=$1000) via (12, 3, 0) (score=11 dpq=1,0 threat=0,0 path=2)
```

A célula de saída é escolhida por pontuação local (terreno, ameaça, distância), **sem
olhar para onde a unidade quer ir**. O Unit11 tem missão no plano (Golf) e sai do QG
para trás. A correção do turno 2 (capturar se a saída cai num prédio) não cobre isto.
A correção que cobre os dois casos: **o papel decide primeiro**; a liberação só
entra quando a ação do papel deixaria a unidade parada em cima da produtora.

---

## Considerações do autor e correções (depois do turno 3)

### Blitzkrieg desligado no Médio

A partida está em **Médio** (`AppliedDifficulty`). O revezamento em profundidade
(`papeis.capturador.blitzkrieg`) está ligado só no `AIPreset_Dificil`; o
`AIPreset_Medio` está com 0. O próprio código registra a intenção contrária ("a média
faz blitzkrieg MAS não respeita a lista banida", em `AIController.Preset.cs` e
`AIPresetCatalog.cs`). Correção de autoria, no Inspector do `AIPreset_Medio`.

E o blitzkrieg dependia da outra correção: o plano já tinha o Golf em
`PartialReadyForHandoff` com o Unit11 escalado para assumir a fábrica 10,5, mas a
liberação de produtora mandou o Unit11 para trás.

### Corrigido: o papel decide antes da liberação de produtora

`AIController.Router.cs`: quando a liberação se aplica, o roteador pede a decisão do
papel (`DecideUnitAction` com a liberação suprimida). Se ela tira a unidade de cima
da produtora, vale; senão, entra a liberação como antes. Junto com o
`BuildVacateBatch` (turno 2), cobre os dois defeitos: sair sem capturar e sair para
o lado errado.

Cuidado conhecido: a decisão do papel roda e pode ser descartada (quando deixa a
unidade parada). Se algum papel registrar reserva ao decidir, essa reserva fica.

### Corrigido: vaga de captura do plano vira compra

`AIShoppingPlanner.Demand.cs`: a demanda de capturador do shopping por papéis passa
a ser `max(pacote 2/2/1, vagas de capturador abertas no plano)`, com prioridade 30
(antes do assalto). No turno 3 isso dá exatamente a compra do Claude: Soldado (1000)
para a vaga do Bravo e Tanque Leve (6000) com o resto.

**Nada disto foi visto rodando ainda.** Compila fora e dentro da Unity.
