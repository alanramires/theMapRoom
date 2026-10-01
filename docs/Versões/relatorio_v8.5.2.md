# v8.5.2 — a forma tem que casar com o dado

O dia em que o MVP virou tela. A `v8.5.1` fechou o laço por dentro; esta versão é
o que o jogador vê — e quase toda decisão dela foi sobre **qual forma cada
informação merece**.

---

## O fio do dia

Um contador de campanha desenhado como barra de proporção. O autor olhou e disse
que estava estranho, e teve que explicar o que o cinza significava:

> *"1 vitória pro verde, 1 pro vermelho e 2 mapas não jogados."*

Se o autor precisou explicar, um jogador novo não deduziria. E a causa não era
estética:

> **Barra é contínua e pede que se interprete área. Quadrante é objeto contável.**

A barra forçava o jogador a extrair uma *contagem* de uma *proporção*. Trocada por
quatro quadradinhos, o denominador parou de precisar de explicação — porque
passou a estar desenhado.

E a mesma régua, aplicada à batalha, respondeu o contrário. Ver a frente 6.

---

## Frente 1 — a paridade da linha sobrevive à translação

O achado mais caro da versão, e ele veio de o autor comparar duas telas.

O mapa de campanha e a batalha desenhavam o **mesmo quadrante** com a costa
diferente. O tabuleiro é **odd-r**: linha ímpar desloca meia célula, então a
posição de mundo de uma linha depende da **paridade** do seu `y`, não da diferença
entre dois `y`.

```text
mosaico da Campanha    pinta em coordenada GLOBAL, na origem de autoria
Batalha                pinta em coordenada LOCAL, com paintOrigin (0,0)
```

Traduzir um retângulo autorado em `y = -9` (ímpar) para `y = 0` (par) inverte a
paridade de **todas** as linhas. O tile continua na célula lógica certa — por isso
nada reclamava — mas a linha inteira anda meia célula.

A previsão que confirmou o diagnóstico **antes** de escrever uma linha:

```text
A_IA_Q1  originY  10  PAR    → sai perfeito
A_IA_Q2  originY  -9  ÍMPAR  → sai cisalhado
A_IA_Q3  originY  10  PAR    → sai perfeito
A_IA_Q4  originY  -9  ÍMPAR  → sai cisalhado
```

O autor confirmou: era o Q2.

Agora existe `origemDaPintura`, derivada uma vez por `Build`, e **todos os seis**
pontos de tradução passam por ela — terreno, camada, rota, construção e unidade.
Se um só ficasse no campo cru, o tabuleiro nasceria coerente por fora e
desalinhado por dentro, que é pior que o bug original.

O `paintOrigin` virou **enquadramento pedido** no tooltip, e o log do build mostra
o pedido e o efetivo. O save grava o pedido, nunca o derivado.

---

## Frente 2 — o fim de partida ganha destino

Quando a partida termina o tabuleiro congela: não há cursor, não há turno. A tela
de vitória era só texto, e a saída era um Enter escondido no
`QuadranteController`.

Agora há botões, em **duas superfícies de propósito** — o `Panel_vitoria` e o menu
do Esc. A redundância é o ponto: o Esc é a única coisa que o jogador alcança
quando o resultado congela tudo.

```text
QuadranteController.PodeVoltarParaCampanha       a pergunta
QuadranteController.TryVoltarParaCampanha()      a ação
BattleMapMenuRootController.BotaoVoltarAoMenuPrincipal   o par
```

Cada componente é dono do **seu** destino: um conhece o `campaignSceneName`, o
outro o `mainMenuSceneName`. Duplicar o nome de cena criaria a segunda fonte para
divergir.

A confirmação de saída deixou de ter três opções fixas — ela é **derivada**, e
"voltar à campanha" entra na frente quando existe. Isso obrigou a tirar do
`PanelHelperController` os rótulos e os índices digitados na mão: com a lista
variando de tamanho, rótulo num arquivo e ação em outro viraria bug na primeira
vez que alguém mexesse só num.

---

## Frente 3 — hot seat: a tela preta

```csharp
if (useHotSeatPanel && !hasVictoryWinner)   // ← se terminou, pula TUDO
```

A cortina sobe, o `AdvanceTurn` resolve o início do turno por baixo dela, e se a
partida **acabar ali dentro** o bloco que apresenta ou libera a cortina é pulado
inteiro. Ninguém mais a abaixa.

Só o Esc respondia porque o `CancelLoadingPresentation` é também quem chama o
`ReleaseGameplayInputBlock`. Tela preta e input morto tinham a mesma causa.

### A tentativa que travou, e por quê

O autor diagnosticou como *"o cálculo tem que esperar o Enter"*, e eu implementei
isso. **Deadlock imediato**, e a causa é um invariante que eu não vi:

```csharp
AreTurnStartEffectsPending => pendingTurnStartUpkeep || pendingTurnStartEconomy
IsTurnBoardReady           => !AreTurnStartEffectsPending && ...
Apresentar(..., IsTurnBoardReadyForHumanConfirmation)   ← só arma o Enter quando pronto
```

Adiar o `ReleaseUnitsForActiveTeam` deixa as flags penduradas → o tabuleiro nunca
fica "pronto" → o Enter nunca arma → o jogador não confirma → o adiado nunca roda.

> **O portão de confirmação É "os efeitos de início de turno já rodaram".** Ele
> existe para o jogador não levantar a cortina sobre um tabuleiro pela metade.

O requisito do autor e esse portão não podem valer os dois. Revertido.

O autor então decidiu o assunto por outro caminho: *"já apresenta vitória direto
sem esperar o outro jogador assumir... afinal ele perdeu mesmo"*. Fechado por
decisão, não por código.

---

## Frente 4 — o quadrante diz com que dinheiro se começa, e a bancada mostra onde

O campo `economiaInicial` existia desde a `v8.5.1` e **não tinha onde ser
editado** — o autor abriu o Map Helper e não achou. Campo sem lugar de edição é
campo que não existe.

Duas linhas na bancada, uma por slot, com a regra escrita na tela:

> *extra na 1ª rodada, somado à renda dos prédios*

Renda 6000 com caixa inicial 14000 abre com 20000, e as rodadas seguintes voltam a
6000. Conferido no `MatchController`: o `startMoney` é somado ao `incomePerTurn`
uma única vez.

**Fica fora do botão Assar** pela mesma razão que decidiu onde o campo mora:
dinheiro não é espacial. Tropa vem do bake porque *está no retângulo*; cem mil no
bolso não está em lugar nenhum da cena.

**Sem prévia da conta**, e isso é deliberado. Seria fácil somar aqui o
`capturedIncoming` dos prédios assados e mostrar "6000 + 14000 = 20000" — e seria
uma segunda continha, que o briefing da cena de campanha já proíbe. A regra vive no
`MatchController` e depende do que a bancada não sabe: no modo fácil a renda da IA
é dividida por três em prédio que não é cidade. O número mentiria exatamente nos
mapas em que mais importa.

---

## Frente 5 — os quadradinhos

Um quadrado por quadrante, preenchido com a cor de quem o tomou.

**Letra foi descartada.** `[V]` e `[R]` só funcionam enquanto os times forem verde
e vermelho — e as cores são escolhidas no menu. É a mesma armadilha que a `v8.5.0`
inteira existiu para consertar, e letra de cor é ainda mais frágil que a cor.

**Não são espaciais**, e isso foi decidido **contra a minha própria sugestão**. Eu
tinha proposto destacar o quadrante em foco e ordenar pela navegação; o autor
apontou que este mundo é uma pizza de quatro fatias mas os próximos podem ser
caóticos.

Conferido: a lista já é ordenada por `quadranteId` e a navegação do mapa é por
**direção**. As duas nunca concordariam — destaque de foco pularia de forma
aparentemente aleatória e ensinaria uma relação falsa.

Então cada um faz uma coisa: **o mapa mostra onde e de quem; os quadradinhos
mostram quantos de quantos.** A contagem sobrevive a qualquer layout.

---

## Frente 6 — a mesma régua, e ela aponta para o outro lado

O autor perguntou se a barra de território da **batalha** também deveria virar
quadradinhos. A resposta é não, e o motivo é o mesmo princípio:

```text
campanha   quantos quadrantes de quantos          4 objetos contáveis  → quadrados
batalha    controlledCapturePoints / total        proporção contínua   → barra
```

Na batalha a captura **parcial** entra na conta:

```csharp
unclaimedFromTeam1 = stats0.contestedOwnedCapturePoints - stats1.contestingCapturePoints;
percent0 = (stats0.controlledCapturePoints + unclaimedFromTeam1) / totalPoints;
```

Um prédio meio capturado **não é meio cubinho**. Num quadrado ele teria que acender
ou apagar, e qualquer das duas mente.

E as duas telas contam a **mesma história** — meu, dele, em aberto — cada uma na
forma que o dado dela merece. Na barra, o miolo cinza *é* o em disputa, e encolhe
pelos dois lados conforme a partida anda.

---

## Frente 7 — vocabulário: `turno` não é `rodada`

O registro do quadrante mostrava `TURNOS: 3` e o autor leu como três jogadas.
Verificado: o `currentTurn` só incrementa em
`CloseRoundAndAdvanceToFirstPlayer` — quando o índice de jogador **dá a volta**.
Ele conta **rodadas**.

```text
o autor     turno = uma jogada          rodada = o ciclo completo
o código    turno = o ciclo completo    (não tem nome para a jogada individual)
```

Mesma palavra, dois significados — e já custou uma checagem no código no meio de
uma conversa. É o mesmo problema que o `plano_campanha` resolveu separando
*quadrante* de *setor*.

Trocado **só no registro do quadrante**, porque aquele número é a **marca**: o
jogador compara ao rejogar, e ele aparece sem nenhuma tela por perto para dar
contexto. O HUD da batalha fica como está — mudar mexe em texto que o jogador já
viu no tutorial.

---

## Frente 8 — duas falhas silenciosas que passaram a falar

**Busca por nome que falha.** O objeto no prefab estava como `bar_tvencedor`, com
um `t` a mais. O painel simplesmente não desenhou — sem erro, sem log — e custou
uma ida e volta. O erro de digitação é trivial; o problema é o código não ter
reclamado. Busca por nome é conveniente porque dispensa arrastar referência, e o
preço dela é falhar calada. Esse preço não precisa ser pago.

**Modo Loop sem clipe** (da `v8.5.1`) continua caindo na faixa de abertura, mas
agora avisa uma vez. Silêncio teria apontado o problema em dois minutos; música
errada custou uma investigação inteira.

---

## Frente 9 — título e placar brigando pelo mesmo campo

Na cena Campanha o título voltou a dizer "Turno 1". Não era bug: o
`presentationTextOverride` estava vazio, e estava vazio porque preenchê-lo
**escondia o subpainel** — a regra dizia que texto de apresentação significa
"prefab reusado fora de uma partida, sem estatística para mostrar".

Essa regra nasceu **antes** de a campanha ter placar. Agora ela tem: os
quadradinhos são conquistas confirmadas, e o título é "Selecione o Mapa" ao mesmo
tempo. Duas coisas amarradas no mesmo campo que precisaram divergir — e a saída de
quem estava mexendo foi apagar o texto, que é o sintoma que apareceu na tela.

A campanha virou exceção **escrita**. Hoje o guard nem chega a rodar lá, mas
depender da ordem de duas coisas distantes é exatamente como o título se perdeu.

---

## Frente 10 — trabalho do autor e da frente paralela

**Autoria.** O `A_IA_Q2` (Terra Firme) ganhou caixa inicial de **10000 para cada
slot** e a **primeira unidade assada do projeto** — um `chinook` no slot 0, em
`(3,4)` local. Os dois mecanismos, criados na `v8.5.1` sem uso, foram exercitados
pela primeira vez.

**Frente paralela** (`d95ba35` e a árvore):

- `CampaignProgressStore.TryGetResult` — devolve dono **e** rodadas juntos, que é
  o que o painel de inspeção do quadrante mostra
- `Campanha Controller` na cena, com o `CampanhaManager`
- `PanelHelperController` esconde o helper enquanto o menu da campanha está aberto
  — os dois ocupam a mesma área
- Depois de carregar um save de campanha, o helper volta a mostrar o quadrante em
  foco em vez do texto "Campanha carregada do slot N"

---

## O que NÃO terminou

### O `0b` continua sem nunca ter rodado

Os hooks de `sceneLoaded` do `ObjectiveManager` e do `AITacticalAnalyzer` estão
escritos desde a `v8.5.1` e **ninguém exercitou**. O teste é um só:

> mapa A → turno 5 → menu → mapa B. No turno 1 do B, o plano da IA tem de nascer
> **vazio**.

É o tipo de bug que só aparece na segunda partida, e nenhuma sessão de teste
chegou lá ainda.

### Há três lugares respondendo "de quem é este quadrante"

```text
CampaignSelectionController.GetWonSectorCounts    conta por slot
CampaignSelectionController.GetQuadrantOwners     lista por quadrante
ProgressoDaCampanha                               a versão que sobe a hierarquia
```

Os três concordam hoje porque todos consultam o `CampaignProgressStore`. Vão
discordar no dia em que "concluído" virar recursivo — *campanha concluída = todos
os quadrantes dela* — que é exatamente o que o portão de destrave vai precisar.

O gatilho é conhecido e o conserto é conhecido: os dois primeiros delegam ao
terceiro. Não foi feito porque hoje não quebra nada.

### O progresso tem um registro de mentira dentro

O `RODADAS: 3` do Terra Firme é o resultado da **armadilha do turno 2** — a partida
que acabou por zero unidades, não por jogo. Agora que o Q2 tem caixa inicial, vale
**limpar o progresso antes de testar para valer**, senão as anotações misturam
resultado real com resultado do setup.

### Arestas de tela, todas conhecidas

- **O painel de inspeção tapa a borda direita do mapa.** Navegar até um quadrante
  de lá é inspecionar algo que não se vê. A ideia parqueada: o painel escolher o
  lado oposto ao quadrante em foco.
- **Sem o override, a campanha cai no "Turno {n}"** do ramo de batalha, e turno não
  significa nada numa tela de seleção. É visível, então não é falha silenciosa.
- **Os números laterais e os quadradinhos dizem a mesma coisa** com 4 quadrantes.
  Ganham utilidade quando uma campanha tiver 12.
- **A mixagem de SFX segue espalhada** por três cenas, uma delas com cursor inline.
- **O silêncio entre menu e campanha** continua: duas causas conhecidas, as duas de
  pé.

### E o de sempre

Nada desta versão passou por um compilador antes de o autor abrir a Unity. O que
foi exercitado, foi por ele jogando — e é assim que a paridade do Q2, o título e o
`bar_tvencedor` apareceram.
