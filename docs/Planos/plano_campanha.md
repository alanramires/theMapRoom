# Plano — Campanha

> **Status:** desenho fechado em conversa, **zero código escrito**. Plano pedido
> não autoriza implementação.
> **Alvo:** MVP com **uma** campanha de **dois** quadrantes de 18×18 lado a lado,
> num mapa novo e burro de propósito — muralha de montanha na fronteira e uma
> única estrada atravessando por um passo.

---

## Estado

O jogo hoje é uma partida avulsa: menu → um mapa → sair. Não existe campanha,
não existe progresso entre partidas, e **nenhuma preferência do jogador
sobrevive a fechar o jogo** (zero `PlayerPrefs` na árvore inteira).

O que já existe e vai ser reusado está catalogado em
[§ O que já está pronto](#o-que-já-está-pronto). É mais do que parece.

---

## Princípio

O erro natural aqui é deixar cada mapa de batalha ser autoral e depois tentar
costurar um mapa-múndi em cima. Isso são duas fontes de verdade, e elas
desincronizam **em silêncio** — o modo de falha que o `CLAUDE.md` já nomeia.

A direção é a inversa:

> **O mapa grande governa os pequenos. Existe um tabuleiro autoral, e as
> batalhas são janelas sobre ele.**

E a frase que evita o mal-entendido mais caro:

> **Não é zoom. É recorte. O hexágono tem o mesmo tamanho nos dois lugares —
> o que muda é a janela, não a escala.**

Disso sai de graça a feature que justifica a campanha inteira: a estrada que
cruza a fronteira entre dois quadrantes **não precisa ser sincronizada**, porque
são literalmente os mesmos tiles, no mesmo sistema de coordenadas.

Corolário que protege a fonte única para sempre:

> **O recorte é imutável.** Toda luta começa como o mapa grande manda. A única
> coisa que muda entre jogadas é *de quem é o quadrante* — e isso mora no arquivo de
> progresso, **nunca no mapa**.

Ou seja: a cidade que você capturou na luta passada **não está lá** quando você
voltar. É o Street Fighter 2 — round novo, vida cheia.

---

## Vocabulário — `quadrante` ≠ `setor`

O retângulo recortável da campanha chama-se **quadrante**. Nunca "setor".

A palavra "setor" **já está ocupada** no jogo, por
[`ConstructionSector`](../../Assets/Scripts/Construction/ConstructionSector.cs) —
e o comentário desse enum, ironicamente, já diz *"setores de campanha (alfabeto
OTAN 0..25, alfabeto grego 30..51)"*. Mas ele é outra coisa:

| | `ConstructionSector` (existe) | `Quadrante` (novo) |
|---|---|---|
| o que é | **rótulo estratégico numa construção** — Alpha, Bravo, … + Base0..Base4 | **região retangular do mapa de campanha** |
| quem consome | o planner da IA, dentro de uma partida | a campanha, entre partidas |
| forma | `enum` com valores fixos, contrato de serialização | `{ originX, originY, width, height }` |
| escopo | uma partida | uma campanha |

⚠️ O enum carrega `ARMADILHA PERMANENTE` na própria documentação
(`default(ConstructionSector)` é `Alpha`, não `None`; renumerar quebra toda cena e
save gravados). Não é um vizinho com quem valha a pena confundir nomes: **um
quadrante contém setores**, e escrever "setor" para os dois tornaria todo log e
toda conversa ambíguos.

---

## Os quatro donos de dado

O `CLAUDE.md` tem três tiers (global / catálogo / cena). A campanha acrescenta um
quarto, e quase toda pergunta de "onde isso mora?" se responde com esta tabela:

| dono | onde | o que guarda | vive quanto |
|---|---|---|---|
| **catálogo** | `ScriptableObject` | o que EXISTE: unidades, terrenos, custos | do jogo, versionado |
| **cena de autoria** | **uma** cena de mundo, que **não entra no build** | o tabuleiro gigante com todas as campanhas | do jogo, versionado |
| **do jogador** | `persistentDataPath` | preferências **e** campanhas em curso | do jogador, fora do git |
| **cena de execução** | Entrada / Campanha / Batalha | nada autoral — tudo pintado | efêmero |

A distinção **cena de autoria ≠ cena de execução** é a que faz a conta de cenas
fechar: a cena de mundo nunca entra no build.

---

## O mundo, as campanhas e os quadrantes

### Uma cena de mundo, não uma por campanha

As campanhas são **contíguas**: Europa faz fronteira com África, que faz com
Ásia. Elas moram todas no **mesmo tilemap**, no mesmo espaço de coordenada.

O argumento é curto e decide sozinho:

> **Não dá para desenhar uma fronteira contínua entre Europa e África se elas
> estão em cenas diferentes.**

Separar impediria exatamente o que a continuidade existe para dar. Cena única
não é concessão, é requisito.

⚠️ **O que isso custa e precisa ser medido cedo:** 4 continentes ≈ 92.000 tiles
numa cena só. Não é bloqueio — a cena de mundo **não é jogável**, então nada de
FoW, pathfinding ou sensores roda nela, e o Tilemap da Unity é fragmentado. Mas
operações de pintura em área grande podem arrastar. **Medir, não assumir.**

⚠️ E uma consequência de responsabilidade: a cena de mundo passa a ser **o
arquivo mais valioso do projeto** — todo mapa jogável de toda campanha deriva
dela. É o ponto único de falha para backup, e não é para abrir junto com o
Inspector de um `.asset`.

### A recursão — campanha e quadrante são o mesmo gesto

```text
cena de mundo
 └── Campanha = retângulo GRANDE      Europa, África, Ásia, Oceania
      └── Quadrante = retângulo pequeno   onde se luta
```

**Campanha é um retângulo. Quadrante é um retângulo.** A mesma ferramenta de
arrasto serve aos dois níveis, com as mesmas regras — centro-dentro, normalizar
para célula, mostrar os números, perguntar antes de gravar.

Disso sai uma economia: **a "foto" da campanha não precisa ser arte.** É o
retângulo dela, enquadrado pela câmera. A tela de campanhas mostra o mundo com o
zoom na Europa.

### A estrutura de assets

```text
CampaignDatabase (SO)                a lista — irmão de UnitDatabase/ConstructionDatabase
  └── CampaignData "Europa" (SO)
        ├── id · nome · descrição
        ├── retângulo { originX, originY, width, height }   ← o enquadramento
        ├── destravadaPor[]  ← campanhas que precisam estar concluídas; vazio = livre
        └── QuadranteData[]
              { id, nome, originX, originY, width, height }
```

A cena de mundo **não é campo de campanha nenhuma** — é uma só, e o retângulo diz
onde cada campanha está dentro dela.

### Ordem: parcial fora, livre dentro

```text
entre campanhas     ordem PARCIAL     terminar a Europa libera a África
dentro da campanha  ordem LIVRE       faz o quadrante 4, depois o 2, tanto faz
```

Curva de dificuldade autorada onde importa (qual continente), liberdade onde é
divertido (qual quadrante). Nenhum dos dois níveis precisa do mecanismo do outro.

---

## Certidão × preferência

Regra do autor: dificuldade da IA, estilo de jogo (preset) e slot POV **não podem
mudar depois de escolhidos**. "IA rápida", "atalho contextual", fullscreen e
volume podem mudar sempre.

Isso não é uma diferença de UI. São **donos diferentes**:

```text
CERTIDÃO DA CAMPANHA   escrita uma vez, na criação, nunca reescrita
   qual campanha · dificuldade · estilo · slot POV · local/remoto/IA por slot

PREFERÊNCIA DO JOGADOR  muda quando quiser, vale para todas as campanhas
   IA rápida · atalho contextual · fullscreen · volume
```

A régua que separa:

> **Se mudar no meio da campanha invalidaria a marca, é certidão. Se não, é
> preferência.**

Consequência: **a dificuldade não é do jogador nem da partida — é da campanha.**
Duas campanhas Oceania simultâneas, uma no Fácil e uma na Agressiva, são dois
arquivos, sem conflito.

---

## Cenas — três de execução

Cinco dos seis passos do fluxo são **menus**, e o roteador de menus já existe:
[`MainMenuState`](../../Assets/Scripts/UI/MainMenuState.cs) já trata `NewGame`,
`LoadMenu`, `Tutorial` e `Config` como **painéis**, com navegação por teclado,
SFX de confirmar/cancelar e integração com o `PanelHelper`.

Promover menu a cena custaria: re-resolver tudo isso N vezes, flash de
carregamento entre passos, e um wizard **pela metade** tendo que sobreviver a
`LoadScene`.

```text
cena: Tela de Entrada    TODOS os menus, via MainMenuState
                         Setup · Config · Nova Partida · Minhas Campanhas
                         + Campanhas (a FOTO, estilo Ticket to Ride)
                         + SlotSetup

cena: Campanha           o tabuleiro hexagonal gigante (~115×200)
                         + camada de alças de mira retangulares
                         + tint de domínio
                         + detalhes do quadrante
                         (SectorSelect não é tela separada: é o clique aqui)

cena: Batalha            vazia. Pintada pelo recorte. O resto do jogo
                         funciona exatamente como funciona hoje.
```

**Três cenas de execução, para qualquer número de campanhas e quadrantes.** Mais os
cinco tutoriais que já existem.

### Nome da cena de batalha

Não chamar de `Build` (colide com o vocabulário da Unity — `EditorBuildSettings`,
"fazer um build"; `LoadScene("Build")` lê como bug) nem de `Construção` (colide
com o vocabulário **do próprio jogo**: `ConstructionData`, `ConstructionManager`,
`ConstructionDatabase`, `ConstructionSector`, `ConstructionSpawner`,
`ConstructionSiteRuntime`, `ConstructionPainterWindow`, `ConstructionOccupancyRules`
— "as construções da cena Construção" é ambíguo de verdade).

**`Batalha`.**

### Onde os arquivos ficam

```text
Assets/Scenes/Tela de Entrada.unity     execução  · build
Assets/Scenes/Campanha.unity            execução  · build
Assets/Scenes/Batalha.unity             execução  · build · nasce VAZIA de tabuleiro
Assets/Scenes/Autoria/Mundo.unity       autoria   · NUNCA no build
Assets/Scenes/Autoria/Fixture.unity     autoria   · NUNCA no build · fixture de regressão
```

⚠️ **A pasta não é o que protege.** Em Unity, uma cena entra no build por estar em
**Build Settings**, não pela pasta. `Autoria/` organiza e sinaliza intenção — quem
protege é não adicionar à lista.

⚠️ **"Batalha vazia" quer dizer sem tabuleiro, não sem managers.** Ela precisa de
`MatchController`, `CursorController`, `UnitSpawner`, `ConstructionSpawner`,
`FogOfWarController`, `SectorManager`, `RoadNetworkManager`, `TurnStateManager`,
`Canvas`, `Grid` + `Tilemap`. O jeito barato de nascer com tudo isso e com o Grid
**idêntico** ao das outras cenas é **duplicar uma cena que já funciona e limpar o
tabuleiro** — nunca criar do zero.

⚠️ Se o Grid da cena de autoria e o da `Batalha` divergirem em *cell size*, *cell
layout* ou *cell swizzle*, **toda tradução de coordenada sai errada** — e sai
errada parecendo bug de recorte, não de configuração.

---

## Fluxo

```text
Tela de Entrada ─── Nova Partida ─── Campanhas ─── SlotSetup ──┐
       │                              (a foto)                  │
       └────────── Minhas Campanhas ────────────────────────────┤
                   (certidão já assinada)                       │
                                                                ▼
                                                       cena: Campanha
                                                    bigview + tint + alças
                                                                │
                                                    clica na alça 14×14
                                                                ▼
                                                     detalhes do quadrante
                                            construções iniciais · renda projetada
                                                  posição de HQs e cidades
                                                       │            │
                                                   aceita        recusa
                                                       ▼            └──► volta
                                                cena: Batalha
                                                       │
                                        ganhou/perdeu ─┘  volta pro bigview
```

### Por que campanha vem antes do SlotSetup

Porque **existe um segundo caminho de entrada**. Na retomada, o SlotSetup **não
acontece** — a certidão já foi assinada. E não dá pra assinar certidão de uma
campanha que ainda não foi escolhida. Slot antes de campanha quebra os dois
lados.

---

## SlotSetup — a mágica, e ela não precisa de modelo novo

Cada slot escolhe entre **IA (dificuldade) · humano local · humano remoto**. Da
combinação nascem os modos:

| slot 0 | slot 1 | modo |
|---|---|---|
| humano local | IA | campanha solo (o padrão) |
| humano local | humano local | **campanha hot seat** — o defensor contra o invasor |
| humano local | humano remoto | campanha à distância |
| IA | IA | assistir a máquina, apostar |

Os três estados **já existem no modelo**, por slot:

```text
MatchController.cs:343    public bool isLocal;
              :1868       SetPlayerIsLocal(slotId, isLocal)
              :1874       entry.isLocal = !entry.isAI && isLocal;
              :1879       IsPlayerLocal(slotId)
```

O SlotSetup é **UI sobre modelo pronto** — a feature mais barata da lista.

⚠️ Nunca usar `!IsPlayerAI` como proxy de "é local". Humano remoto já existe no
modelo e cai no buraco desse proxy.

---

## O recorte

A alça de mira é **retangular** de propósito: a geometria de um quadrante cabe em
quatro inteiros, e isso é exatamente o formato que a ferramenta existente já usa.

```text
Quadrante = { originX, originY, width, height }   ← e nada mais
```

### A ferramenta de autoria — arrastar, ver, aceitar

`EditorWindow` com seleção retangular por *click-n-drag* sobre o tilemap do mapa
de campanha. O gesto pinta uma prévia dos hexágonos atingidos e **pergunta antes
de gravar**.

É a **lei transacional do jogo aplicada ao Editor** — o mesmo
`Neutral → provisório → compromisso → Neutral` de
[`acoes_transacionais.md`](../arquitetura/acoes_transacionais.md). Arrasto e
prévia são cancelávelis; só o aceite grava.

**Regra de pertencimento: centro dentro do retângulo.** Não "qualquer toque".

| regra | na fronteira |
|---|---|
| **centro dentro** | a largura gravada é a largura arrastada |
| qualquer toque | você pede 18 e recebe 19 ou 20, dependendo de onde começou o gesto |

⚠️ A razão **não** é impedir sobreposição — sobreposição é permitida, ver abaixo.
É garantir que a sobreposição seja **sua**, e não um arredondamento da ferramenta.

**O gesto é descartado; o retângulo de células é o que fica.** A ferramenta
normaliza o arrasto para coordenada de célula *antes* de perguntar, e toda linha
passa a ter exatamente `width` células por construção.

> **O zigue-zague é da renderização, nunca do dado.**

Isso não é preciosismo: se o conjunto pudesse sair irregular (18 numa linha, 17 na
outra), **os quatro inteiros não conseguiriam representá-lo** e o quadrante viraria
uma lista de células — mais pesada, e o recorte de rotas e o bake mudariam de forma
junto. O formato barato depende de o retângulo ser verdadeiro.

**A confirmação mostra o número, não só a cor:**

```text
Quadrante 1
origem (0, 0) · 18 × 18 · 324 células
[ aceitar ]   [ refazer ]
```

O critério de aceitação do MVP depende de **alinhamento** — a estrada entrando na
mesma linha R dos dois lados só fecha se os quadrantes tiverem a mesma altura e
origens alinhadas. Um erro de uma célula é invisível na cor e óbvio no número.

### Sobreposição é permitida — exceção, não regra

Quadrantes **podem** se sobrepor. Você arrasta o quadrante 2 entrando um pouco no
1, e aquela faixa aparece **nos dois** mapas de batalha, como terreno normal e
jogável.

O precedente é o Game Boy Wars: as faixas de montanha aparentemente inúteis e
nunca visitadas na borda **eram** pedaços do quadrante vizinho. A fronteira não é
vazia, é compartilhada.

Isso **não quebra nada**, e vale entender por quê:

| preocupação | por que não se aplica |
|---|---|
| progresso ambíguo | o dono é **por quadrante**, não por célula. Contar quadrantes não muda |
| recorte instável | cada quadrante recorta o mesmo retângulo sempre; a célula compartilhada é lida duas vezes, igual |
| coordenada colidindo | cada recorte translada pra origem local própria |

**A única coisa que precisa de regra é o tint** (ver [§ Tint](#tint-de-domínio--mesma-arte-entidade-diferente)):
onde dois quadrantes se cruzam, o desenho é por **ordem determinística de id**, o
maior por cima. Arbitrário e inofensivo, porque sobreposição é exceção.

> **Sobreposição é decisão de autoria, nunca resultado da regra de seleção.**

#### Dica de design: a sobreposição é para o CHÃO

O construtor lê **tudo** que está no retângulo. Terreno compartilhado é o recurso;
**peça** compartilhada nasce duas vezes — uma em cada batalha.

> **Não deixe construções na interseção de quadrantes.**

```text
terreno na faixa      serra, estrada, rio          ← é o ponto. Os dois lados veem
construção na faixa   QG, cidade, fábrica          ← nasce nos DOIS quadrantes
```

Para QG é erro puro: um quadrante nasceria com três ou quatro QGs, e a contagem por
slot quebra. Para cidade é confusão de renda e de captura — a mesma cidade
capturável em duas lutas independentes, com a mesma identidade visual e nenhuma
relação entre elas.

A faixa de fronteira quer ser **geografia**, não patrimônio: serra, mata, rio,
estrada. Se algo capturável precisa ficar perto da fronteira, coloque-o **dentro**
de um dos quadrantes, fora da interseção.

### Não existem saídas — o quadrante é fechado

Dentro da partida você está **preso ao quadrante**. A única saída é ganhar ou
perder. Não há travessia, não há passagem para o quadrante vizinho no meio da
luta.

⚠️ **Não desenhar setas nem indicadores de "leva para o próximo quadrante".**
Seta implica travessia; prometer um mecanismo que não existe é pior que não
sinalizar nada.

A continuidade se comunica sozinha, do jeito que o Game Boy Wars fazia: a estrada
some pela borda, a serra continua. O jogador entende que há mundo lá fora sem
que nada afirme que dá pra ir.

### O construtor de quadrante — tudo vem do mapa de campanha

O *quadrante builder* lê do mapa de autoria **o que estiver dentro do retângulo**
— terreno, estruturas, construções e unidades — e plota na área vazia jogável.

> **Fonte única, sem exceção.** Não existe um segundo lugar guardando peças.

Duas consequências que valem escrito:

- **Não existe flag `temUnidadesIniciais`.** Se você pintou unidades ali, o
  quadrante nasce com elas; se não pintou, nasce vazio e o jogador compra tudo. A
  escolha é o **desenho**, não um campo — mesma economia da habilidade-como-chave.
- **As posições de QG são sempre conhecidas**, porque vêm da foto maior. É isso
  que permite a tela de detalhes mostrar onde ficam os QGs e as cidades **antes**
  de aceitar a luta, sem construir nada.

#### O pintor nunca escreve na cena de autoria

O construtor tem **origem** e **destino**, e eles nunca podem ser o mesmo arquivo:

```text
lê   de   Autoria/Mundo.unity      a fonte
escreve  em   Batalha.unity        descartável, sempre repintada
```

⚠️ Vale igual para o botão **"pintar este quadrante agora"** do Editor (fase 2):
ele também precisa de uma cena de destino aberta. Pintar o recorte de volta na
cena de autoria **corrompe a própria fonte** — e de um jeito difícil de perceber,
porque o resultado *parece* um mapa válido.

É a classe do "arquivo gerado que alguém editou", ao contrário: o gerador
escrevendo em cima da origem.

### O que o recorte produz

| camada | de onde sai | estado |
|---|---|---|
| terreno | `GetTile` no retângulo → lista achatada de ids | ferramenta existe |
| estradas / estruturas | filtrar `RoadRouteDefinition.cells` no retângulo, transladar | formato já é célula |
| construções | posições dentro do retângulo | spawner pronto |
| unidades iniciais | posições dentro do retângulo | spawner pronto |
| **setores** (o `ConstructionSector` do jogo) e praias | **derivados** do tilemap e das construções por `BeachManager`/`SectorManager` | grátis — ver ⚠️ abaixo |
| slots, cores, economia, preset | `PartidaConfig` | pronto |

### ⚠️ O portão de ordem — quem pergunta antes da tinta secar

O `SectorManager` **já se reconstrói sozinho, e já é preguiçoso**: a primeira
consulta dispara a reconstrução a partir das construções ativas.

```text
SectorManager.cs:461, 502, 593, 634    RebuildFromActiveConstructions("first-query")
```

Isso é ótimo — não precisa de código novo. **E é exatamente por isso que vira
armadilha.** Se qualquer coisa consultar o tabuleiro *antes* de a pintura
terminar, ele assa um mapa **vazio** e **cacheia o vazio**. Não há erro, não há
log vermelho: há um plano degenerado, que é o sintoma já catalogado neste projeto
("mapa sem topologia = plano degenerado e ~60s por chamada").

Hoje isso nunca acontece porque a cena de batalha **nasce pronta** — o tabuleiro
existe antes de qualquer consulta. Com a cena vazia pintada em runtime, essa
garantia desaparece.

> **A pintura precisa de um portão explícito: nada consulta o tabuleiro até ela
> declarar que terminou.** Não confiar em ordem de `Awake`/`Start`, nem em
> `DefaultExecutionOrder` — o que quebra aqui é silencioso e cacheado.

### Coordenadas

**Local, com offset guardado.** O recorte translada para origem `(0,0)` e o
`CampaignData` guarda de onde veio.

Motivo: save, replay e IA continuam com os números pequenos de hoje, e nada
precisa ser tocado. Coordenada global da campanha seria mais elegante, mas o
`CLAUDE.md` avisa que faixas de coordenada sobrepostas entre mapas falham **em
silêncio** — e aqui todos os quadrantes viriam do mesmo espaço.

---

## O que já está pronto

Esta seção é o motivo de o plano ser viável. Tudo abaixo foi verificado no
código, não deduzido.

### O pintor já roda em produção

O caminho de restauração de save é *"reusa se existir, cria se não existir"*:

```text
SaveGameManager.cs:1619   if (manager == null)
              :1622          constructionSpawner.Spawn(constructionData, teamId, world, ...)
              :1680          unitSpawner.Spawn(...)
```

Numa cena **vazia** nada existe, então tudo cai no ramo de criar. A cena de
Batalha não precisa de código novo de spawn — precisa do caminho que já roda em
todo load, com o "se existir" nunca batendo.

### A máquina de recorte existe em embrião

[`BasicMapGeneratorWindow.cs`](../../Assets/Editor/BasicMapGeneratorWindow.cs), 1984 linhas:

```text
MapSlotData { originX, originY, width, height, List<string> terrainIds }
GetTile de uma região       linhas 1579, 1855, 1876
SetTile numa outra          linhas 855, 1650, 1885
copiar região src → dst     linha 1498
snapshot / restore          linhas 1855-1901
```

Falta apontar a leitura pra uma cena de campanha em vez de um preset de zona.

### As estradas já estão na forma certa

[`RoadRouteDefinition.cs:14`](../../Assets/Scripts/Structures/RoadRouteDefinition.cs)
guarda `List<Vector3Int> cells`. Recortar uma estrada que cruza a fronteira é
**filtrar as células dentro do retângulo e transladar**. Sem formato novo.

### O que o save NÃO carrega

[`SaveDataDtos.cs`](../../Assets/Scripts/Shared/SaveData/SaveDataDtos.cs) tem
`UnitSaveData` e `ConstructionSaveData`, cada um com `cellX`/`cellY`. **Nenhum
tile.** O save é construtor de *estado*, não de *chão*. O chão é responsabilidade
do recorte.

### Bônus: a campanha termina uma migração em voo

Existem **10 catálogos de estrutura, um por mapa**
(`Assets/DB/World Building/Structures/Catalogues/*.asset`) — layout morando em
catálogo, que o `CLAUDE.md` chama de violação. A migração pra cena já está no
meio do caminho, com ponto único
([`RoadNetworkManager.cs:830`](../../Assets/Scripts/Structures/RoadNetworkManager.cs))
e a flag `routesMigratedToScene` (linha 29).

Com **uma** cena de Batalha, esses 10 catálogos perdem a razão de existir: as
rotas moram no mapa de campanha e o recorte filtra. A campanha é o motivo que
faltava pra terminar a migração.

---

## Tint de domínio — mesma arte, entidade diferente

O território colorido é **da campanha**. Não entra no build de carregamento, a
batalha nunca o vê.

⚠️ **Não construir isso em cima da névoa de guerra.** São perguntas diferentes:

```text
névoa   "o que eu enxergo AGORA"     volátil, volta a ser preto
tint    "o que eu CONQUISTEI"        permanente, é placar
```

Se compartilharem sistema, um dia alguém desliga uma flag pra fazer um dos dois
se comportar — e o `CLAUDE.md` já diz o que isso significa: *"se você se pega
desligando uma flag pra fazer uma das duas funcionar, a entidade está errada, não
a flag"*. Compartilhar a **arte** (convenção de overlay: miolo branco tintável +
outline preto), nunca a entidade.

⚠️ A cor não sai do slot direto: `slot → GetTeamIdForSlot → TeamUtils.GetColor`.
Senão o quadrante conquistado pinta errado quando o POV não for o slot 0.

⚠️ **Onde quadrantes se sobrepõem**, o desenho é por **ordem determinística de
id** (maior por cima). Nunca por ordem de iteração de coleção nem por ordem de
carregamento — senão a mesma campanha pinta diferente entre duas aberturas, e
ninguém vai suspeitar do tint.

---

## Detalhes do quadrante — é preview, e preview tem histórico nesta casa

A tela de detalhes mostra construções iniciais, **renda projetada** e posição de
HQs. A renda não é campo de construção, é derivada:

```text
MatchController.cs:3946   RecalculateIncomePerTurnForAllPlayers(List<ConstructionManager>)
```

O parâmetro é **objeto de cena**. A tela de detalhes não tem objeto de cena
nenhum — o recorte ainda não foi construído.

O caminho preguiçoso é escrever uma segunda continha ("soma as cidades do
retângulo"), e isso recria a divergência **promessa ≠ execução** que o Serviço do
Comando já custou uma vez. A receita que funcionou lá vale aqui:

> **Uma regra só, dois consumidores.** A regra de renda sai do
> `ConstructionManager` e vira serviço burro sobre dados; batalha e preview leem
> o mesmo.

Se o preview disser "renda 12" e a partida começar com 9, o jogador não confia em
mais nenhum número da tela.

---

## Progresso e marca

Um arquivo por campanha em curso, em `persistentDataPath`, ao lado das
preferências.

```text
certidão   { campanha, dificuldade, estilo, POV, slots }   imutável
por quadrante  { dono, turnos, quando }                        atualizado ao fim de cada luta
```

**Os dois modos guardam a mesma coisa.** O quadrante tem um dono, sempre — e é só
isso. A diferença entre solo e duelo **não está no dado, está na condição de
fim**:

| modo | dono do quadrante | fim da campanha |
|---|---|---|
| **solo (vs IA)** | neutro · meu · da IA | **quando TODOS forem meus** |
| **duelo (vs jogador)** | neutro · slot 0 · slot 1 | **não tem** — *for fun mode* |

Ganhar os 10 mapas num duelo não vence campanha nenhuma: o mapa colorido é o
placar, e o fim é quando os dois quiserem parar. Só o solo tem estado terminal.

### Como o território troca de mão

**A IA nunca ataca no mapa de campanha.** Ela não tem iniciativa estratégica — só
segura o que você não conseguiu tomar. Território só se move quando **você**
escolhe um quadrante e entra:

```text
você vence  →  o quadrante fica seu, com a marca (turnos)
você perde  →  a IA marca o ponto: o quadrante fica DELA
               e você decide — rejogar pra reivindicar, ou atacar outro
```

Por isso a campanha solo não trava: perder nunca fecha caminho, só adia.

E por isso o tint precisa de **três estados**, não dois — "ainda não tentei" e
"tentei e falhei" são informações diferentes, e a segunda é metade da narrativa
da campanha.

### Regras

- **A marca é por quadrante.** Quadrantes de tamanhos diferentes geram partidas de
  durações diferentes; turnos não são comparáveis entre quadrantes, e tudo bem.
- **Só marca melhor sobrescreve.** Vencer de novo em mais turnos não piora o
  registro.
- **O duelo não tem fim** e não precisa de condição de vitória. Zero trabalho.
- **Caçar marca é aposta.** Rejogar um quadrante que já é meu só pra melhorar o
  tempo e perder **entrega o quadrante ao adversário**, igual a qualquer outra
  derrota. Não existe "meu e imperdível".
- **Campanha vencida fica vencida.** O carimbo de conclusão é registro do que
  aconteceu, não estado do mundo. Depois dele, rejogar é treino.

### O que essas duas regras produzem juntas

Elas parecem opostas — uma diz "o território sempre se move", a outra diz "a
conclusão nunca se move". Mas se apoiam na mesma divisão, que já vale para a
marca:

```text
ESTADO      dono do quadrante            muda sempre, sem exceção
REGISTRO    marca · carimbo          nunca piora, nunca se apaga
```

O resultado é um modelo **sem nenhum caso especial**: nenhuma regra muda depois
da conclusão, nenhum "modo pós-jogo" existe no código. O que muda é só o
significado — o território continua trocando de mão, mas não gateia mais nada, e
por isso *parece* treino. Zero `if (campanhaConcluida)` em qualquer lugar.

Consequências aceitas de propósito:

- **A campanha solo anda pra trás.** Dá pra estar em 9/10 e voltar pra 8/10.
  Não existe ponto sem retorno.
- **O mapa perfeito pode ser sujado depois de vencer.** Rejogar um quadrante por
  esporte e perder pinta ele com a cor do adversário — mesmo com a campanha já
  carimbada. Basta reconquistar. Se isso incomodar no playtest, a correção é
  visual (mostrar o carimbo com destaque), **não** uma exceção na regra.

### Destrave entre campanhas

Acima do quadrante existe um segundo nível de progresso: `CampaignData.destravadaPor[]`.
Uma campanha só aparece disponível quando todas as campanhas listadas estiverem
concluídas. Vazio = disponível desde o começo.

```text
entre campanhas     ordem PARCIAL     terminar a Europa libera a África
dentro da campanha  ordem LIVRE       faz o quadrante 4, depois o 2, tanto faz
```

O estado "concluída" é o mesmo carimbo da seção acima — registro, não estado do
mundo. Perder território numa campanha já concluída **não retranca** as campanhas
que ela destravou.

Se um dia o duelo precisar de fim, existe precedente pronto de forma:
`victoryStarsToWin` ([`MatchController.cs:430`](../../Assets/Scripts/Match/MatchController.cs),
`1..12`, padrão 5) — a mesma forma um andar acima.

---

## Modelo de dados

### O sabor de orientação a objetos que este projeto usa

Não é o dos anos 80-90 (dado + comportamento + herança). O projeto já escolheu
**dado burro de um lado, comportamento do outro**, e a prova está em
[`SaveDataDtos.cs`](../../Assets/Scripts/Shared/SaveData/SaveDataDtos.cs):

```text
33 classes · 0 métodos · 0 heranças
comportamento inteiro em SaveDataMapper.cs, 751 linhas, separado
```

E a doutrina mais central do jogo **é** a regra anti-herança, um andar acima:

```text
OOP 80-90       UnidadeAlpina : Unidade { podeEscalar() }
este projeto    SkillData "alpino" carrega só identidade
                TerrainTypeData.requiredSkillsToEnter — a montanha decide
```

*Uma habilidade não é um poder, é uma chave.* O modelo da campanha segue o mesmo
padrão: registros sem comportamento, e quem interpreta mora fora.

### A árvore

```text
ARQUIVO DE CAMPANHA (um por campanha em curso, em persistentDataPath)
│
├── contrato                       imutável, escrito uma vez na criação
│     campanha · dificuldade · estilo · POV
│     slots[] { IA(dificuldade) | humano local | humano remoto }
│
└── quadrantes[]
      { id, dono, marca }
              │      └── a MELHOR corrida, INTEIRA:
              │          { turnos, quando, estatísticas }
              │          nula enquanto nunca foi vencido
              └── neutro · meu · do adversário

DERIVADO — calculado na hora, nunca guardado
      progresso    quantos quadrantes são meus / total   → "campanha 2: 50%"
      concluída    todos meus                          (só no solo)
      modo         dois humanos no contrato            → duelo
      contador     quantos de cada dono                → placar do duelo
```

### As três regras da estrutura

**1. Status é derivado, nunca guardado.** "Campanha 2 = 50%" se conta da lista de
quadrantes no momento de mostrar. Guardar o número num campo é criar a segunda
fonte: um dia a lista diz 3/6 e o cabeçalho diz 40%. É o mesmo modo de falha do
recompute parcial e da divergência promessa ≠ execução, que já custaram caro
neste projeto duas vezes.

**2. A marca e as estatísticas vêm do MESMO jogo.** A marca é um **pacote
inteiro**, não campos soltos competindo cada um por conta. Se "melhor tempo"
vier da corrida A e "mais abates" da corrida B, o registro descreve um jogador
que nunca existiu.

**3. Duelo não é irmão de campanha — é leitura do contrato.**
`CampanhaDuelo : Campanha` duplicaria a árvore inteira pra mudar uma condição de
fim: é exatamente a armadilha dos anos 80. Dois humanos no contrato → é duelo.
Mesmo arquivo, mesma forma, leitura diferente.

### Estado e registro são eixos independentes

```text
dono    ESTADO      de quem é AGORA          muda sempre
marca   REGISTRO    o que eu já fiz aqui     nunca piora
```

Perder um quadrante que era seu numa rejogada tira o `dono` e **não toca na marca**.
Continua registrado que você tomou aquilo em 11 turnos. É a mesma natureza do
carimbo de conclusão da campanha.

⚠️ Não colapsar os dois num campo só ("vencido: sim/não"). É o que obrigaria a
escolher entre apagar história e congelar território — e as duas saídas já foram
recusadas.

---

## Bloqueios — pré-requisitos que não são features

Os dois primeiros valem por si mesmos, **mesmo se a campanha for adiada**. O
terceiro é diferente: ele não existe hoje como defeito — ele **nasce** no dia em
que a cena de batalha vira única, e por isso tem que viajar junto com ela.

### 1. Fim de partida não tem ponto único

`hasVictoryWinner = true` é escrito em nove lugares do `MatchController` e volta a
`false` na linha 2998. **Não existe nenhum evento** — e sem ele sobra `Update`
perguntando `HasVictoryWinner` todo frame, o padrão que já fritou o FPS neste
projeto uma vez (a cicatriz é o `MaxResolveAttemptsBeforeSelfDisable` em
[`MainMenuStateController.cs:36-42`](../../Assets/Scripts/UI/MainMenuStateController.cs)).

**Mas o trabalho é bem menor do que "consolidar nove escritas": o funil já
existe.**

```csharp
// MatchController.cs:3025 — a taxonomia já está pronta
private enum VictoryReason
{
    HeadQuarterCaptured,   ← MVP
    ArmyEliminated,        ← MVP
    Surrender,
    VictoryStars           ← adiado, ver abaixo
}

// MatchController.cs:3033 — e a assinatura é exatamente a que a campanha precisa
HandleVictoryAestheticPresentation(TeamId winnerTeam, TeamId defeatedTeam, VictoryReason reason)
```

Chamado de 2355 (estrelas), 2751 e 2770 (eliminação), 7253 (rendição) e 11444 — e
a captura de QG chega lá via `DeclareEliminationVictory`. **Os dois motivos do MVP
já passam pelo funil.**

O que falta:

| # | trabalho |
|---|---|
| 1 | o funil tem nome de **estética** e nenhum evento — publicar `OnMatchConcluded(vencedor, derrotado, motivo, turno)` dali, no molde do `SaveGameManager.OnAfterLoadSuccess` |
| 2 | **três caminhos o contornam**: `DeclareTutorialVictory` (2358), `DeclareTutorialDefeat` (11491), `DeclareDefeat` (2391) |
| 3 | ⚠️ a linha **1250 é `ImportVictoryState`** — restauração, não conclusão. **Nunca pode disparar** |

⚠️ O item 3 é a armadilha real: carregar um save de partida já concluída
reescreve a flag como `true`. Um evento ingênuo dispara no *load*, e a campanha
marca a vitória de novo toda vez que o save for aberto.

**Escopo do MVP:** vitória só por `HeadQuarterCaptured` e `ArmyEliminated`, como
no Game Boy Wars. As **estrelas ficam para depois do MVP** — e o funil é
justamente o que torna isso barato: acrescentar um motivo depois é uma chamada, não
um sistema novo.

### 2. Quatro managers vazam entre cenas

Campanha é a **primeira coisa neste projeto que encadeia cena → cena → cena**.
Hoje o jogo sempre faz menu → um mapa → sair, então isso nunca foi exercitado.

```text
MatchStatsManager                 tem sceneLoaded   ✅
PanelVisibilityHotkeysController  tem sceneLoaded   ✅
ObjectiveManager                  SEM GANCHO        ⚠️
AIShoppingPlanner                 SEM GANCHO        ⚠️
AITacticalAnalyzer                SEM GANCHO        ⚠️
HexCohabitationVisualManager      SEM GANCHO        ⚠️
```

Os três primeiros com ⚠️ são **o cérebro persistente inteiro da IA**: plano de
objetivos, compras e análise tática. Numa campanha, o plano do quadrante 1 entra no
quadrante 2. O `CLAUDE.md` já suspeitava do `ObjectiveManager` ("verify who clears
it") — a resposta é que **ninguém limpa**, e mais dois estão iguais.

Vai aparecer do jeito que o doc avisa: **em silêncio**, como uma IA que joga
estranho no segundo mapa, sem nenhum erro no console.

### 3. A guarda de cena para de proteger no dia da cena única

Hoje existe uma proteção real contra carregar um save no mapa errado:

```csharp
// SaveGameManager.cs:1317
if (!string.IsNullOrWhiteSpace(data.sceneName) && !string.Equals(data.sceneName, currentScene, ...))
{
    if (blockCrossSceneLoad)
    {
        Debug.LogWarning($"[SaveGame] Load bloqueado: save da cena '{data.sceneName}', cena atual '{currentScene}'.");
        yield break;   // ← barra o load
    }
}
```

É isso que impede carregar um save do `Battle Map 1` dentro do `Hot Seat`. O
caminho do menu principal contorna carregando a cena certa antes
(`PendingMainMenuLoadRequest { slotIndex, sceneName }`).

**Com uma única cena de batalha, essa guarda deixa de proteger qualquer coisa.**
Todo save passa a ter `sceneName = "Batalha"`, então os nomes sempre batem:

```text
save do quadrante 1 de Oceania   →  carregado numa Batalha pintada
                                     como quadrante 3 de Metallion
guarda diz:                          "tudo certo, mesma cena"
```

E a falha é **muda**: as coordenadas são locais nos dois casos, então as unidades
caem em células que existem. Exército do mapa errado, terreno errado, zero erro —
o parágrafo do `CLAUDE.md` sobre faixas de coordenada sobrepostas, ao pé da letra.

#### A correção não é uma guarda melhor — é não precisar de guarda

A primeira reação é trocar a chave: comparar `(campanha, quadrante)` em vez de
`sceneName`. Funciona, mas continua sendo **validação** — alguém compara duas
coisas que poderiam divergir, e torce para o `if` estar certo.

A cena única permite algo melhor: **o save deixa de ser conferido e passa a
mandar.**

```text
GUARDA (fraco)      a cena já existe → compara com o save → aceita ou recusa
DRIVER (forte)      a cena nasce vazia → o save DIZ o que pintar → pinta → restaura
```

O `.tmrsave` carrega `(campanha, quadrante)`. A `Batalha` nasce sem tabuleiro. O
carregamento **lê a identidade do save e pinta aquele quadrante**, e só então
restaura peças. Não existe "save no mapa errado" porque **não existe mapa antes do
save dizer qual é** — a divergência deixa de ser representável.

Isso também apaga o `PendingMainMenuLoadRequest`, que hoje existe só pra carregar
a cena certa antes de aplicar o save: passa a haver uma cena só.

**A ordem do carregamento vira contrato:**

```text
1. carrega a cena Batalha (vazia)
2. lê (campanha, quadrante) do save
3. PINTA o quadrante
4. portão: "pintura terminou"        ← o mesmo da fase 2
5. restaura unidades e construções
```

⚠️ O passo 4 não é enfeite. Restaurar peças antes de a pintura acabar coloca
unidade em tabuleiro que ainda não existe, e o `SectorManager` assa vazio e cacheia
— sem erro nenhum no console.

⚠️ Isso tem que entrar **junto** com a cena única, na mesma frente. Enquanto a
`Batalha` for única e o save não carregar a identidade, a proteção de hoje já foi
embora e nada ocupou o lugar dela.

✅ **Não é preciso back-compat.** O jogo não está publicado
(`CLAUDE.md` § *Distribution state*): saves antigos não têm dívida, o formato pode
mudar de forma.

---

## Custo escondido: a bancada de Editor cega

Hoje o autor abre a cena de batalha no Editor e **vê o tabuleiro**. Mais de vinte
janelas em [`Assets/Editor/`](../../Assets/Editor/) dependem disso: Hotzone,
MelhorCaptura, MelhorEmbarque, MelhorDesembarque, CaminhosValidos, AlguemMeVe,
HexEnxergado, NavalOps.

Com a cena de Batalha vazia até o Play, essa bancada passa a responder sobre um
tabuleiro que não existe — a versão grande do modo de falha já documentado:
*"registro só-de-runtime faz a consulta responder otimista — 'hex livre'"*.

**A mitigação é barata se nascer junto, e cara se vier depois:** um botão
**"pintar este quadrante agora"** no Editor, rodando o mesmo pintor em edit mode, sem
salvar a cena. Não é item opcional da fase 1.

---

## Fases

Ordem escolhida pelo que **destrava** e pelo que **cega** se ficar pra depois.

| # | frente | por que nesta posição |
|---|---|---|
| **0a** | evento no funil `HandleVictoryAestheticPresentation` + 3 caminhos que o contornam | bloqueio; vale sozinho. **Menor do que parece** — o funil e o `VictoryReason` já existem |
| **0b** | `sceneLoaded` nos 4 managers | bloqueio; vale sozinho |
| **1** | pintor de terreno em runtime | único código realmente novo |
| **2** | botão "pintar agora" no Editor **+ portão de "pintura terminou"** | **mesma frente que 1.** A bancada não pode cegar, e nada pode consultar o tabuleiro antes da tinta secar |
| **3** | recorte de rotas (filtrar `cells` + transladar) | aqui os 10 catálogos por mapa começam a morrer |
| **4** | spawn de peças reusando os spawners | já pronto, é ligar |
| **4b** | save carrega `(campanha, quadrante)` e **dirige** a pintura no load | **mesma frente que a cena única.** Não é guarda melhor: é a divergência deixar de ser representável. Mata também o `PendingMainMenuLoadRequest` |
| **5** | `GameSettings` estático + `settings.json` | preferências; primeiro trabalho é **remover** as duas cópias de fullscreen que já existem |
| **6** | `CampaignData` + arquivo de progresso | depende de 0a para saber quando marcar |
| **7** | painéis Campanhas e SlotSetup na Tela de Entrada | UI sobre modelo pronto |
| **8** | cena Campanha: bigview, alças, tint, detalhes | a tela bonita é a última |

**A tela bonita por último.** Durante todo o MVP, a seleção de quadrante pode ser
dois botões feios. O que precisa estar certo é o recorte.

---

## Teste de aceitação do MVP

O que distingue "mapa grande governando os pequenos" de "dois mapas feitos à mão"
é **uma única coisa**: a continuidade na fronteira. Sem ela, o mapa grande não
comprou nada.

### O fixture

**Mapa novo, pequeno e burro de propósito** — não reaproveitar o Hot Seat. Um
mapa reaproveitado traz QG, equilíbrio de terreno e posicionamento que não têm
nada a ver com o recorte, e vira ruído no diagnóstico.

```text
36 × 18 células, dois quadrantes de 18×18 lado a lado

┌─────────────────┬─────────────────┐
│                 ███               │
│   quadrante 1   ███   quadrante 2 │
│                 ═╪═  ← passo      │   ███  muralha de montanha
│                 ███               │   ═══  a única estrada
└─────────────────┴─────────────────┘
```

A muralha existe pra **tornar a borda visível a olho nu**; o passo único existe
pra dar um ponto de verificação em vez de uma faixa.

Ele não é descartável: vira o **fixture permanente de regressão** da máquina de
recorte. Minúsculo, e regerável a qualquer momento.

⚠️ **Desenhe a muralha pelas coordenadas de célula, não a olho.** Num tilemap
hexagonal com linhas deslocadas, um intervalo retangular de células é limpo no
dado e **serrilhado na tela**. Pintar a montanha "onde parece a metade" faz o
primeiro teste *parecer* quebrado com a máquina certa — falso negativo caro.

### O critério

> **Pinte a estrada cruzando o passo no mapa de autoria. Regere os dois
> quadrantes. Sem tocar em nenhuma cena de batalha:**
>
> - a estrada entra no **quadrante 1 pela borda leste, na linha R**;
> - a estrada entra no **quadrante 2 pela borda oeste, na MESMA linha R**.

Confere-se em cinco segundos, sem log e sem ferramenta. E se a linha não bater
entre os dois, o erro é de **translação de origem** — não precisa investigar mais
nada.

Se passa, o resto é UI e paciência. Se não passa, descobriu-se com dois quadrantes
em vez de dez.

**Por isso o MVP é 2 quadrantes, não 4.**

---

## Armadilhas

| armadilha | regra |
|---|---|
| derivar o mapa grande dos pequenos | a direção é a inversa: um autoral no topo, batalhas são janelas |
| tratar recorte como zoom | mesmo tamanho de hexágono; o que muda é a janela |
| editar a cena de batalha à mão | é artefato. O próximo recorte apaga. Se editou, perdeu |
| estado de partida subindo pro mapa | o recorte é imutável; só o **dono** do quadrante persiste |
| tint construído sobre a névoa | perguntas diferentes; mesma arte, entidades separadas |
| cor lida do slot direto | `slot → GetTeamIdForSlot → TeamUtils.GetColor` |
| preview com continha própria | uma regra, dois consumidores. Renda divergente mata a confiança na tela inteira |
| promover menu a cena | `MainMenuState` já resolve painéis; cena custa flash, wizard partido e boilerplate ×N |
| SlotSetup antes da campanha | na retomada ele nem acontece; a certidão é da campanha |
| alça de formato livre | retângulo é 4 inteiros e casa com o `MapSlotData` existente |
| alça grande "porque cabe" | 14×14 = 196 hexes é o perfil que funciona hoje; 40×40 acorda o problema conhecido de mapa gigante |
| coordenada global da campanha | faixas sobrepostas entre quadrantes falham em silêncio; local + offset |
| cena chamada `Build` ou `Construção` | ambas colidem com vocabulário existente. `Batalha` |
| deixar o botão de pintar no Editor pra depois | a bancada inteira cega e ninguém percebe na hora |
| chamar o retângulo de "setor" | `ConstructionSector` já ocupou a palavra, e é outra coisa. **Quadrante** contém setores |
| desenhar a fronteira a olho | retângulo de células é limpo no dado e **serrilhado na tela** (linhas hex deslocadas). Pintar pelo olho gera falso negativo no primeiro teste |
| gravar o gesto em vez do retângulo de células | o arrasto é pixel e aproximado; guardar linhas de larguras diferentes **quebra os 4 inteiros** e força lista de células |
| pertencimento por "qualquer toque" | a largura gravada ≠ a arrastada. **Centro dentro**, sempre — sobreposição se faz arrastando, não arredondando |
| tratar sobreposição como bug | é recurso: a faixa de fronteira do Game Boy Wars. O dono é por **quadrante**, não por célula |
| **construção na interseção de quadrantes** | a faixa é para o **chão**. Peça ali nasce nos dois: QG duplicado quebra a contagem por slot, cidade duplicada confunde renda e captura |
| pintor escrevendo na cena de autoria | origem e destino nunca são o mesmo arquivo — corrompe a fonte e o resultado *parece* válido |
| criar a `Batalha` do zero | Grid divergente em cell size/layout/swizzle torce **toda** tradução de coordenada, parecendo bug de recorte. Duplicar cena que funciona e limpar |
| achar que a pasta `Autoria/` mantém fora do build | quem decide é **Build Settings**; a pasta só sinaliza |
| sinalizar saída para o quadrante vizinho | não há travessia — preso até ganhar ou perder. Seta prometeria mecanismo inexistente |
| criar flag de "tem unidades iniciais" | se está pintado no retângulo, vem. A escolha é o desenho |
| uma cena de autoria por campanha | mata a fronteira contínua entre continentes, que é o motivo de tudo. **Uma** cena de mundo |
| disparar o evento de conclusão no load | `ImportVictoryState` (linha 1250) reescreve a flag; save de partida concluída marcaria vitória de novo a cada abertura |
| tratar campanha e quadrante como conceitos diferentes na ferramenta | são o **mesmo retângulo** em dois níveis; uma ferramenta, duas escalas |
| tint sobreposto por ordem de iteração | a mesma campanha pintaria diferente entre aberturas. **Ordem determinística de id** |
| confirmar quadrante só pela cor | alinhamento de origem e altura é o que o teste de aceitação exige; erro de 1 célula é invisível na cor |
| reaproveitar mapa existente como fixture | traz QG, equilíbrio e posicionamento que viram ruído no diagnóstico. Fixture é desenhado pro teste, não herdado |
| consultar o tabuleiro antes da pintura terminar | o `SectorManager` assa vazio e **cacheia o vazio**. Portão explícito, nunca ordem de `Awake` |
| confiar em `blockCrossSceneLoad` depois da cena única | todo save vira `sceneName = "Batalha"`; a guarda passa a aprovar tudo. Identidade vira `(campanha, quadrante)` |

### Sobre o tamanho do bigview

~115×200 ≈ **23.000 hexes** é um número que deveria assustar, dado o histórico de
performance de mapa gigante — mas **não acorda o problema**, porque o mapa de
campanha **não é jogável**: sem FoW, sem pathfinding, sem sensores, sem IA. É
renderização, e o Tilemap da Unity engole isso.

A batalha continua em ~196 hexes. **O perfil de performance fica idêntico ao de
hoje.** O que mudou é que o tamanho da alça virou alavanca de performance — com
preço conhecido, não surpresa.

---

## Decisões em aberto

| # | pergunta | por que trava |
|---|---|---|
| 1 | ~~a borda é corte seco ou tem anel de contexto não-jogável?~~ **resolvido: corte seco.** O contexto vem de **sobreposição autoral** — terreno normal e jogável nos dois quadrantes, sem terceiro estado de célula | — |
| 2 | ~~o mapa de autoria guarda as unidades iniciais, ou vêm do `CampaignData`?~~ **resolvido: do mapa de autoria.** O builder lê tudo que está no retângulo. Sem flag de "tem unidades" — a escolha é o desenho | — |
| 3 | ~~o tint mostra só "conquistado"?~~ **resolvido:** três estados — neutro · meu · do adversário | — |
| 4 | ~~rejogar um quadrante já meu: se eu perder, o adversário toma?~~ **resolvido: toma.** Caçar marca é aposta, e a campanha pode andar pra trás | — |
| 5 | ~~depois da campanha concluída, rejogar arrisca o território?~~ **resolvido:** o carimbo de conclusão não se desfaz; o território continua se movendo. Nenhuma regra muda, rejogar vira treino | — |

---

## Referências

| documento | uso |
|---|---|
| `CLAUDE.md` § Subir pra cima | os tiers e o teste de aceitação da cena vazia |
| `CLAUDE.md` § As duas verdades | por que tint e névoa não compartilham entidade |
| [`docs/resumo.md`](../resumo.md) | estado geral e armadilhas da retomada |
| [`arquitetura/acoes_transacionais.md`](../arquitetura/acoes_transacionais.md) | lei de compromisso — vale dentro da batalha, inalterada |
