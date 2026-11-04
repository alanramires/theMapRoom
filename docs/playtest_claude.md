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
