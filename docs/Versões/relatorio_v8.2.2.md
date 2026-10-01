# v8.2.2 — O tronco existe no papel, e a bancada aprendeu a ver o mapa

Fechada em 2026-08-14. Antecessora: [`v8.2.1`](relatorio_v8.2.1.md).

---

## O fio do dia

Um dia de **desenho e de bancada**, não de arquitetura. Nada de campanha foi
implementado; o que existe é um plano de 1120 linhas e duas ferramentas de Editor
que nasceram porque a autoria travou sem elas.

A frase que organiza o dia:

> **O mapa grande governa os pequenos. Não é zoom, é recorte.**

E o achado que mais mudou o rumo veio do autor, não do plano:

> **Com uma cena de batalha única, o save deixa de ser conferido e passa a
> mandar.** A `Batalha` nasce vazia, o save diz que quadrante pintar, e só então
> as peças voltam. "Save no mapa errado" deixa de ser detectável porque deixa de
> ser *representável*.

---

## Frente A — O plano de campanha

`docs/Planos/plano_campanha.md`, 1120 linhas, escrito contra o código e não
deduzido. Três commits (`013198e`, `c1c4d59`, `098415c`).

O autor o chamou de **tronco mestre**, e a razão é estrutural: até aqui todos os
planos eram internos à IA (iniciativa, transportador, capturador). Este é o
primeiro que descreve **o que o jogador faz com o jogo** — e por isso é o
primeiro que *puxa* arquitetura em vez de empurrar.

Isso importa porque várias pendências estavam paradas **por serem higiene sem
consumidor**:

```text
fim de partida sem evento          conhecido, parado
4 managers DontDestroyOnLoad       CLAUDE.md já suspeitava, parado
10 catálogos de estrutura por mapa doc já chamava de violação, parado
routesMigratedToScene sem prazo    a flag existe PORQUE não havia prazo
zero persistência de preferência   fullscreen duplicado em 2 lugares
```

A campanha não descobriu nenhuma delas. Ela deu **motivo** a todas.

### O que ficou decidido

Cinco decisões de design fecharam, e três delas com o mesmo formato — *estado é
uma coisa, registro é outra*:

| decisão | resolução |
|---|---|
| tint do quadrante | três estados: neutro · meu · do adversário |
| rejogar quadrante já meu | **é aposta.** Perder entrega o território, sem exceção |
| campanha concluída | o **carimbo não se desfaz**; rejogar vira treino |
| borda do recorte | **corte seco**, e o contexto vem de sobreposição autoral |
| unidades iniciais | vêm do mapa de autoria; **sem flag** `temUnidadesIniciais` |

As duas do meio parecem opostas — "o território sempre se move" contra "a
conclusão nunca se move" — mas se apoiam na mesma divisão, e juntas produzem um
modelo **sem nenhum caso especial**: não existe `if (campanhaConcluida)` em lugar
nenhum.

### Descobertas que barataram o plano

**O funil de vitória já existe.** O plano dizia "8 escritas de `hasVictoryWinner`,
zero eventos", o que subestimava a situação. Existe `VictoryReason`
(`MatchController.cs:3025`) já com os dois motivos do MVP —
`HeadQuarterCaptured` e `ArmyEliminated` — e existe
`HandleVictoryAestheticPresentation` (`:3033`) com **exatamente a assinatura que
a campanha precisa**: `(vencedor, derrotado, motivo)`.

Sobram três coisas, não nove: publicar o evento no funil, rotear os três caminhos
que o contornam (`DeclareTutorialVictory`, `DeclareTutorialDefeat`,
`DeclareDefeat`), e **blindar `ImportVictoryState` (`:1250`) de disparar no
load** — restauração não é conclusão.

**A recursão do retângulo.** Campanha e quadrante são o mesmo gesto em duas
escalas, o que faz uma ferramenta só servir aos dois níveis — e faz a "foto" da
campanha não precisar ser arte: é o retângulo dela, enquadrado pela câmera.

**Cena de mundo única.** As campanhas são contíguas (Europa faz fronteira com
África), então moram no mesmo tilemap. O argumento decide sozinho: *não dá para
desenhar uma fronteira contínua entre duas cenas*.

---

## Frente B — Map Helper

`Assets/Editor/MapHelperWindow.cs`, 880 linhas (`7e8fc82`).
`Tools > Utils > Map Helper`.

Nasceu de um pedido simples — *"não consigo ver largura e altura"* — e virou o
primeiro pedaço do C1 do plano.

Faz quatro coisas: contorno e leitura da caixa desenhada, buracos marcados em
vermelho, régua nas bordas mais rótulo por hexágono, e prévia de quadrante com a
sobreposição calculada. Seleção por **dois cliques** na Scene view.

**Duas decisões que valem preservar:**

O **contorno é serrilhado de propósito**. Ele liga os *centros* das células da
borda, então mostra onde o retângulo de célula realmente cai — não onde "parece
reto". Um retângulo limpo mentiria sobre a armadilha que a ferramenta existe pra
expor.

A **seleção é por célula, não por pixel**. Os cantos já *são* células, então o
retângulo nasce exato e toda linha tem a mesma largura — os quatro inteiros
descrevem o quadrante sem perda, que é a invariante de que o bake e o recorte de
rotas dependem. E entre o primeiro clique e o segundo o retângulo é provisório e
cinza: `Neutral → provisório → compromisso → Neutral`, a mesma lei de
[`acoes_transacionais.md`](arquitetura/acoes_transacionais.md).

Nada de prefab de texto: tudo é `Handles`, que só existe no Editor. O rótulo por
hexágono tem três travas (só o visível, só com zoom suficiente, teto de
quantidade) porque sem elas a cena de Mundo congelaria o Editor.

---

## Frente C — Faxina de Cena

`Assets/Editor/SceneSanitizerWindow.cs`, 521 linhas.
`Tools > Utils > Faxina de Cena`.

Nasceu de um problema real: duplicar uma cena de mapa para criar a de autoria
trouxe **46 rotas de estrada de outro mapa** — `Auridia`, `Valentia`,
`Baia Santos`, `Ye-Country`, `Cabo Leste`.

### O diagnóstico que o autor pediu, e o que ele não era

A reação inicial foi *"eu já falei que essas coisas são desacopladas"*. Mas as
rotas estarem na cena **é o desacoplamento funcionando**: `routesMigratedToScene`
está `1`, o layout saiu do catálogo e foi para a cena, que é o tier certo. O que
faltava não era desacoplar mais — era existir uma operação de **esvaziar o
tabuleiro**, que nunca existiu.

O sinal de contaminação é preciso:

```text
cena structureDatabase   82ff47f9…  (o Fixture.asset novo)
 1 rota  ownerDatabase = 82ff47f9…  ← Cruzamento, do autor
46 rotas ownerDatabase = db7a20bd…  ← catálogo estrangeiro
```

A ferramenta classifica por `ownerDatabase` em *própria / estrangeira / legado*,
e remove por filtro — com `Undo` e confirmação em tudo. Depois de remover ela
invalida o lookup e reconstrói os visuais, senão a cena continuaria desenhando
estrada que já não existe no dado.

### ⚠️ Correção de uma afirmação errada minha

Eu afirmei ao autor que as rotas estrangeiras *"viram estrada de verdade no seu
mapa de teste"*. **Está errado.** `RoadNetworkManager.IsRouteAllowedForCurrentDatabase`
(`:361`) filtra por `ownerDatabase`, e o padrão `filterRoutesByOwnerDatabase` é
`true` — elas são **inertes**.

O que elas realmente são: peso morto no arquivo, mais **duas minas**.

1. Rota com `ownerDatabase` **nulo** é tratada como legado/global e **passa** no
   filtro. É a única classe estrangeira que age de verdade.
2. Se o bake do recorte ler a lista crua (`RoadRoutesByStructure`) em vez do
   caminho filtrado (`GetRoadRoutes`), ele pega tudo.

O item 2 é dívida futura registrada aqui de propósito: quando o C2 for escrito,
ele **tem** de ler pelo caminho filtrado.

---

## Frente D — Sondagem curta na iniciativa

`085a74b`. `MelhorEmbarqueService.cs` + `AIController.TransportOperations.cs` +
`AI Behavior/Transporte.md`.

Trabalho que estava no disco há dias sem commit, encontrado durante uma
investigação de build lento.

A fila da Fase 2 precisa de **um** fato para promover um transportador vazio ao
grupo 2: existe passageiro com carona publicada, encontro `Tactical` e rota
`ReachableNow`. Para responder esse booleano ela mandava construir o snapshot de
pickup **completo** — Operational, Strategic, manifestos e diagnóstico por par — e
jogava o resto fora.

> Serviço descreve possibilidade; iniciativa só ordena. Colher um fato não devia
> custar um ranking.

`MelhorEmbarqueRequest.tacticalOnly` corta três coisas: o `operational` deixa de
multiplicar por `operationalTurns`; a varredura **quebra** assim que o tier sai de
`Tactical`, aproveitando que `orderedCandidateCells` já vem por distância; e a
segunda onda do passageiro não é calculada, matando um `CalculateMovementCostMap`
e um `BuildMeetingCostMap` por avaliação.

No setup da iniciativa, um pré-filtro barato descarta o transportador sem nenhum
candidato publicado **antes** de montar o planning snapshot. E
`EvaluateTacticalPickupInitiativeFact` **não** marca `PickupEvaluated` nem
preenche `planning.Pickup` — a decisão real do transportador continua construindo
o snapshot completo preguiçosamente, como antes.

---

## Frente E — Autoria: o fixture nasce

Três cenas em `Assets/Scenes/Autoria/` (nenhuma entra no build) e um
`StructureDatabase` novo (`Fixture.asset`) que carrega **só a identidade** da
estrada — `roadRoutesByStructure` vazio, o traçado mora na cena.

```text
Autoria/Fixture.unity   37 × 19 = 703 tiles, sólido · serra ao centro · 1 rota
Autoria/Mundo.unity     vazio, esperando os continentes
```

E as duas **cenas de execução**, criadas durante o próprio fechamento, ao lado da
Tela de Entrada — vazias, com os 41 managers e o Grid idêntico ao das demais
(`CellLayout` hexagonal, `Swizzle 0`), porque nasceram de duplicação de cena que
já funciona:

```text
Campanha.unity    o bigview — NÃO é o mesmo que Autoria/Mundo: um é fonte, o outro vitrine
Batalha.unity     a cena única onde todo quadrante é pintado
```

O `Cruzamento` atravessa de `x −17` a `x 17` na linha `y 0`, pelo passo aberto na
muralha. É o que o teste de aceitação vai conferir.

### O serrilhado apareceu duas vezes, e da mesma forma

O primeiro desenho tinha **11 buracos**, dez deles em `x=16` e **só nas linhas
ímpares**. Depois de corrigido, a muralha repetiu o padrão: `x=1` tinha montanha
só nas linhas **pares**.

É a assinatura de pintar "onde parece reto" numa grade hexagonal com linhas
deslocadas. Um retângulo limpo em coordenada de célula **não** é um retângulo na
tela, e é por isso que o Map Helper desenha o contorno serrilhado.

---

## Frente F — Hot Seat repintado

`Hot Seat 1 - Pvp.unity`: **+208 tiles** (1893 → 2101), zero GameObjects
alterados. Autoria de cenário confirmada pelo autor, commitada como frente
própria.

---

## O que não terminou

- **Nada da campanha tem uma linha de código.** Existem 1120 linhas de plano e
  zero de implementação. O plano é **carga**, não verdade validada.
- **As cenas de execução estão vazias e desligadas.** `Campanha.unity` e
  `Batalha.unity` nasceram durante o fechamento, com os 41 managers e o Grid
  certo, mas **nenhuma foi adicionada ao Build Settings** — hoje isso é proteção,
  amanhã é pendência. Nada as pinta ainda.
- **A frente D não teve corrida de aceitação.** O diff foi lido por inteiro; o
  comportamento não foi exercitado em partida. É mudança na fila de iniciativa, e
  o `resumo.md` já avisa que defers ali podem gerar cessão mútua.
- **O `Map Helper` e a `Faxina de Cena` não foram exercitados no `Mundo`** — os
  três gates de performance do rótulo por hexágono e a varredura sob demanda
  foram desenhados para dezenas de milhares de células, e testados em 703.
- **Os ~92.000 tiles da cena de mundo continuam sendo estimativa.** Marcado no
  plano como *medir, não assumir*.
- **A ordem de tamanho do quadrante está acima do provado**: 19×19 = 361 células
  contra as ~196 que já rodaram.
- **`CLAUDE.md` continua desatualizado** sobre o ataque oportunista do courier —
  pendência herdada da `v8.2.1`, não tocada.

---

## Armadilhas que este dia acrescenta

| armadilha | regra |
|---|---|
| **`git status` limpo confundido com cena limpa** | rodei o teste "nasceu vazia?" olhando tiles, construções e unidades — e **não olhei rotas**. Elas estavam lá o tempo todo. O teste do `CLAUDE.md` inclui estradas; apliquei pela metade |
| **contaminação inerte descrita como ativa** | afirmei que as rotas estrangeiras viravam estrada no mapa; o filtro por `ownerDatabase` as barra. Conferir o filtro **antes** de descrever o efeito |
| **rota com `ownerDatabase` nulo** | é tratada como legado/global e **passa** no filtro. É a única classe estrangeira que age |
| **retângulo de célula desenhado a olho** | num grid hexagonal a borda é serrilhada; pintar pelo que "parece reto" fura linha sim, linha não. Aconteceu **duas vezes** no mesmo dia |
| **script de Editor culpado por build lento** | `Assets/Editor/` não entra em player build. Quem mexe no build é runtime — no dia, a frente D, que estava sem commit |
| **`Assets/Resources/` como pasta neutra** | ela embarca **inteira** em todo build; um build cancelado deixou dois JSON de instrumentação ali |
| **duplicar cena esperando cópia limpa** | duplicação copia o layout que a cena legitimamente possui. O que faltava era a operação de esvaziar, não mais desacoplamento |
| **derivado não invalidado** | remover rota sem `InvalidateRoutesLookup` + `RebuildRoadVisuals` deixa a cena desenhando estrada que já não existe no dado |

---

## Onde isso deixa a próxima sessão

O plano define oito fases. As **duas primeiras não dependem de nenhuma decisão em
aberto e valem por si mesmas**:

```text
0a  evento no funil de vitória     conserta um beco sem saída que já existe hoje
0b  sceneLoaded nos 4 managers     testável agora: menu → mapa A → menu → mapa B
```

A `0a` merece destaque: `freezeTurnAdvanceAfterVictory` (`MatchController.cs:431`)
é um campo **deliberado**, ligado por padrão, que congela o turno depois da
vitória porque não havia para onde ir. Não é obra inacabada — é um marcador
esperando a camada que agora existe no papel.
