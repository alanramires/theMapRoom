# v8.4.0 — O catálogo diz o que uma coisa É, e hoje parou de dizer onde ela está

Fechada em 2026-08-14. Antecessora: [`v8.3.0`](relatorio_v8.3.0.md).

---

## O fio do dia

A frase que organiza o `CLAUDE.md` desde sempre:

> **O catálogo diz o que uma coisa É. A cena diz onde ela ESTÁ.**

Ela era doutrina, não estado. Hoje virou estado — e o caminho até lá foi achar
**três camadas** de layout escondidas em catálogo, a última delas no lugar mais
global que existe.

```text
ConstructionDatabase.fieldEntries        1058 entradas em 7 catálogos    ZERO leitores
StructureDatabase.roadRoutesByStructure    93 rotas em 16 catálogos
StructureData.roadRoutes                   16 rotas em 4 TIPOS           ← a pior
```

A terceira merece o destaque: o asset **"Rodovias"** — que diz o que uma rodovia
*é*, custo e chaves e família topológica — carregava **onze traçados concretos**.
Toda cena que usasse o tipo herdava estrada de outro mapa, sem erro e sem aviso.

Isso é o que fazia o teste de aceitação do `CLAUDE.md` falhar:

> *Duplique uma cena, aponte pros catálogos, e o mapa novo tem que nascer VAZIO.*

Ele falhou de manhã, na `v8.3.0`, quando duplicar o Fixture trouxe **46 rotas
estrangeiras**. Hoje ele passa — e passa **por construção**, não por disciplina:
não existe mais de onde herdar.

---

## Frente A — A desagregação

### `ConstructionDatabase.fieldEntries`: zero leitores, e existia mesmo assim

Todos os consumidores eram Editor. O que alimentava o campo era o
`ConstructionPainterWindow`, com um flag `persistToFieldDatabase` que **espelhava
no catálogo** o que já plantava na cena. O nome do flag entregava a direção: a
cena era a fonte, o catálogo era a cópia.

Uma cópia que ninguém lia, e que obrigava a existir **um catálogo por mapa**.

O autor decidiu apagar sem exportar — as cenas têm as construções de verdade, e o
espelho era peso morto. Sumiram 1058 entradas.

### `roadRoutes`: a condição estava escrita, e não estava satisfeita

O `CLAUDE.md` prescrevia o pré-requisito:

> *"Não apague a seção do catálogo até que todo mapa carregue a flag: o catálogo
> é o fallback compartilhado, e limpá-lo estranha qualquer mapa que não migrou."*

Verificado mapa a mapa: **12 das 13 cenas** com `routesMigratedToScene: 1`. A
exceção era `História 3 - Resgate Off Road`, com flag `0` e zero rotas na cena —
dependendo integralmente do catálogo `Tutorial 4 - roads e bridges`, que tem 3
rotas (`Tutorial 4 Road`, `Route 2`, `Ponte de Rammelle`).

**O autor decidiu quebrar o tutorial 3.** Está registrado como escolha, não como
descuido, e as rotas se repintam com o `RoadRoutePainterWindow` — que agora grava
direto na cena.

### O que encolheu

| arquivo | antes | depois | o que era |
|---|---|---|---|
| `ConstructionDatabaseEditor.cs` | 311 | 40 | lista de `fieldEntries` + import do "legacy" |
| `StructureDatabaseEditor.cs` | 108 | 38 | migração `StructureData` → catálogo |
| `RoadNetworkManagerEditor.cs` | 240 | 67 | migração catálogo → cena; sobrou o relatório |
| `ConstructionPainterWindow.cs` | 656 | 466 | 190 linhas do espelho, num bloco contíguo |
| `RoadRoutePainterWindow.cs` | — | — | grava sempre na cena, sem cadeia de três |

A flag `routesMigratedToScene` sumiu porque **perdeu o sentido**: ela existia para
separar *"vazio porque não migrei"* de *"vazio porque não há estrada aqui"*. Com
uma fonte só, vazio quer dizer uma coisa só.

### ⚠️ E eu errei de novo, do mesmo jeito

Antes de chegar aqui, eu encontrei sete catálogos por mapa e **construí em cima
disso**: propus um `MundoData.constructionDatabase`, para que cada mundo trouxesse
o seu, e escrevi um setter no `ConstructionSpawner` para trocar em runtime.

O autor cortou: *"catálogos são apenas agrupamento de coisas... que merda de
vincular catálogo em cena é essa? tem q ser como o unit database"*.

**Estava certo.** Eu apliquei o padrão que encontrei sem perguntar **por que** ele
existia — e a resposta era um campo que ninguém lia. É a **quarta** vez em dois
dias que erro assim, e já é armadilha registrada na `v8.3.0`. Revertido.

O `TerrainDatabase` provava o ponto o tempo todo: **um asset só**, compartilhado
por todo mapa, sem vínculo com cena. Ninguém achou que precisava de um por mapa —
porque ele não carrega layout.

---

## Frente B — Construções entram no recorte

`ConstrucaoAssada` é o que `ConstructionFieldEntry` era, no lugar certo: dentro do
quadrante, em coordenada local, saído do bake.

O bake varre os `ConstructionManager` da cena de autoria e guarda os que caem
dentro do retângulo. **O dono vem junto** — a construção nasce da cor com que foi
pintada, mesma regra das unidades: *"se está pintado no retângulo, vem como
está"*. Não existe flag de "tem QG"; a escolha é o desenho.

A pintura reusa `ConstructionSpawner.SpawnAtCell` — o caminho que todo
carregamento de save já exercita.

### ⚠️ O setor quase ficou de fora

Comparando com `ConstructionFieldEntry`, o meu `ConstrucaoAssada` estava perdendo
três campos. O pior era `sector`:

> `ConstructionSector` tem `Alpha = 0` e `None = -1`. **O default do enum é
> `Alpha`, não `None`.** Esquecer o setor não daria erro — daria toda construção
> nascendo em Alpha, e o planner da IA lendo um tabuleiro que não existe.

É a `ARMADILHA PERMANENTE` escrita no próprio enum, e eu ia cair nela. O bake agora
leva `sector`, `isAnchorSector` e `initialCapturePoints`, lidos do
`ConstructionManager` da cena.

### Ordem e idempotência

Construções são plantadas **depois** do terreno: o spawner recusa célula ocupada e
precisa do tabuleiro no lugar para converter célula em posição de mundo.

E `clearBeforeBuild` passou a destruir as construções também. Sem isso o build não
era repetível: `ClearAllTiles` limpava o chão, as construções ficavam, e o segundo
build virava uma fila de avisos em vez de um tabuleiro.

---

## Frente C — A bancada

**Descrição** nos três níveis, via `INoDoMapa` — um campo, três telas.

**Aviso de `id` não-técnico.** O YAML já reclamava sozinho
(`campanhaId: "Feij\xE3o Torto"`), e esse mesmo texto é digitado à mão em dois
lugares que precisam bater. O aviso **não corrige sozinho**: mudar id é mudar
endereço, e correção automática quebraria em silêncio.

```text
id             feijao-torto      técnico, é o que o save grava
nome           Feijão Torto      livre, é o que o jogador lê
descrição      briefing
```

**Herança do pai.** "Usar a caixa desenhada" virou contextual: campanha herda o
retângulo do **bloco**, quadrante herda o da **campanha**, e só o bloco cai no
scan. O ganho não é conveniência — é que o filho **nasce contido**, e a validação
de continência deixa de disparar por causa do valor inicial.

---

## Frente D — Autoria

`Autoria/Fixture.unity`: **950 → 1800 tiles**, 41 → 210 GameObjects, **13
construções** pintadas e 2 rotas.

`Mundo Fixture.asset`, com ids técnicos:

```text
bloco A
 └─ campanha A_IA
     ├─ A_IA_Q1   (-18,10) 16×17 = 272   272 tiles · 13 construções assadas
     └─ A_IA_Q2                          sem bake
```

`All Buildings.asset` nasceu como o catálogo compartilhado que o autor descreveu —
15 tipos, servindo o Fixture e a Batalha. O `Hot Seat.asset` (por mapa) foi
apagado.

⚠️ O `All Buildings` foi duplicado do `Hot Seat` e ainda carrega **126
`fieldEntries` herdados no YAML**. O campo não existe mais na classe; a Unity
descarta na próxima reserialização.

---

## Frente E — Tela de Entrada

Dois objetos de UI desativados (`m_IsActive: 1 → 0`) e reposicionados (âncoras no
topo-esquerda, `550×64`, em `y = -106` e `y = -254`). Trabalho do autor —
*"estamos voltando às origens"*.

---

## O que não terminou

- **As construções plantam só em parte.** O autor considera esperado nesta etapa;
  a causa não foi investigada. O diagnóstico por construção existe — o pintor diz
  qual falhou e em que célula.
- ⚠️ **A `Batalha` está com o `ConstructionSpawner` sem catálogo**
  (`constructionDatabase: {fileID: 0}`), enquanto o `Fixture` aponta pro
  `All Buildings`. Enquanto não for ligado, nenhum id resolve e o quadrante nasce
  sem QG. **É a primeira coisa a arrumar.**
- **Estruturas não são recortadas nem pintadas.** O teste de aceitação do plano —
  *a estrada entra na linha R nas duas bordas* — continua por fazer.
- ⚠️ **Quando o recorte de rotas for escrito, ele lê o `RoadNetworkManager` da
  cena** — não existe mais catálogo de onde ler, o que simplifica, mas o hábito
  antigo de olhar o catálogo agora acha vazio em vez de errado.
- **`A_IA_Q2` não foi assado.**
- **Unidades iniciais não entram no bake.** É o mesmo molde do
  `bakedConstrucoes`, e não foi escrito.
- **`História 3 - Resgate Off Road` perdeu suas 3 rotas**, por decisão do autor.
- **`BoardReady` continua sem leitor.**
- **A avaliação de destrave não existe.** Só os campos.
- **A dívida do `guid` segue de pé**, com gatilho conhecido: antes do primeiro
  arquivo de progresso.
- **Nada foi medido em escala.** Bancada e pintor rodaram sobre 1800 células.

---

## Armadilhas que este dia acrescenta

| armadilha | regra |
|---|---|
| **catálogo por mapa tomado como padrão** | ele existia só porque o catálogo carregava layout. O `TerrainDatabase` — 1 asset, compartilhado — provava o contrário o tempo todo |
| **layout no TIPO compartilhado** | "Rodovias" carregava 11 traçados. Toda cena que usasse o tipo herdava estrada de outro mapa, sem erro |
| **campo sem leitor tomado como inofensivo** | `fieldEntries` tinha zero leitores em runtime **e** obrigava sete catálogos a existir |
| **`ConstructionSector` default** | é `Alpha = 0`, não `None = -1`. Esquecer o setor não dá erro: dá plano degenerado |
| **build não idempotente** | limpar tiles sem limpar construções faz o segundo build virar fila de avisos |
| **id com acento ou espaço** | o YAML escapa (`"Feij\xE3o Torto"`) e o mesmo texto é digitado em dois lugares que precisam bater |
| **`sed` em C# sem conferir chaves** | comeu um `}` de fechamento hoje. Contagem de profundidade antes de seguir |
| **apagar dado do autor sem perguntar** | 1058 entradas e 3 rotas de tutorial. Perguntei, ele decidiu — e é assim que tem de ser |

---

## Onde isso deixa a próxima sessão

```text
1. ligar All Buildings no ConstructionSpawner da Batalha        ← 1 minuto
2. investigar por que as construções plantam só em parte
3. assar o A_IA_Q2
4. recorte de ESTRUTURAS  → e aí o teste de aceitação fecha
5. bake de unidades iniciais (mesmo molde do bakedConstrucoes)
```

E os dois bloqueios da `v8.2.2` seguem intocados: **evento único de fim de
partida** e **`sceneLoaded` nos 4 managers**. Nenhum depende de decisão em aberto,
e a campanha vai encadear cenas.
