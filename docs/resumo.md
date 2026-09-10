# Resumo — onde estamos e o que vem

Ponto de retomada. Atualizado em 2026-09-09, **depois** da tag `v8.5.1`.
Leia isto primeiro.

---

## Estado

`v8.5.1` tagueada e publicada. Relatório:
[`relatorio_v8.5.1.md`](relatorio_v8.5.1.md).

```text
v8.3.0   o primeiro quadrante pintou      361 tiles, 2 ms, cena vazia
v8.4.0   o catálogo parou de dizer ONDE   três camadas de layout removidas
v8.4.1   a peça tem lado                  orientação, rotas partidas, identidade
v8.5.0   o laço fecha                     volta, dono por slot, tropa inicial
v8.5.1   o que atravessa a cena            save por endereço, isPlayable, 0b
```

**O fluxo existe ponta a ponta, volta, e agora sobrevive a um save.**
Menu → Campanha → Batalha → Campanha.

### A descoberta que organiza o resto

> **A cena `Campanha` parece uma partida e não é.**

Ela tem `MatchController`, `TurnStateManager`, lista de jogadores e cursor —
porque foi construída sobre a cena-base de batalha. Tudo que pergunta *"estou
numa partida?"* olhando para o que **existe** na cena responde **sim**, e age
errado. Num dia só isso apareceu em quatro lugares independentes:

```text
cortina de hot seat ao carregar save      MatchController.isPlayable
apresentação de rodada na seleção         PanelRodadaController
menu de batalha no mapa de campanha       BattleMapMenuRootController
música do time na seleção                 MatchMusicAudioManager
```

Nenhum era bug do save, da música ou do menu: eram todos a mesma pergunta mal
formulada.

### A descoberta da versão anterior, que segue valendo

> **Cor não é identidade. É uma fantasia que o slot veste por uma partida.**

As duas cores são escolhidas no menu. Tudo que atravessa a fronteira entre
autoria e partida — ou entre uma partida e a seguinte — endereça por **slot**, e
a cor se resolve só na hora de pintar, por `GetTeamIdForSlot`.

A regra já estava escrita no briefing da cena de campanha (*"cor de time nunca
sai do slot direto"*). O dia mostrou que ela vale muito além dali: foi violada em
**três** pontos independentes, e os três sintomas eram silenciosos.

```text
1. construção assada nascia com a cor da AUTORIA      → azul num jogo amarelo
2. progresso gravava a COR do vencedor                → dono que some se você troca de cor
3. a volta não republicava a config                   → quadrante pintado na cor de outro
```

E o nº 1 tinha um segundo defeito embaixo: ler do slot na hora errada. O
`QuadranteController` roda em `-9000` e o `Awake` do `MatchController` em `0`, e é
lá que o `PartidaConfig` era aplicado. **Pintar antes da configuração chegar** — a
armadilha do projeto espelhada. Daí o `EnsurePartidaConfigApplied`, ponto único e
idempotente, que quem pinta antes chama primeiro.

---

## Vocabulário

```text
MUNDO       uma cena de autoria + UM asset. O globo inteiro, desenhado de uma vez
 └─ BLOCO       Europeu · América do Norte · Rússia    ← o jogador escolhe
     └─ CAMPANHA    Europa · África
         └─ QUADRANTE   Inglaterra · Congo...          ← aqui se luta, e é o que é assado
```

| termo | é |
|---|---|
| **quadrante** | o retângulo recortável onde se joga |
| **setor** | `ConstructionSector` — rótulo estratégico numa construção |
| **slot** | quem é o dono. Slot 0, slot 1. **A cor é roupa dele nesta partida** |
| **turno** ⚠️ | **duas palavras para a mesma coisa, e elas discordam.** O `currentTurn` do `MatchController` só incrementa em `CloseRoundAndAdvanceToFirstPlayer` — ou seja, conta **rodadas** (o ciclo completo de jogadores). Mas o código o chama de "turno" em todo lugar: HUD da batalha, cortina de privacidade. O **autor** chama isso de *rodada* e reserva *turno* para a jogada individual. O registro do quadrante já usa o vocabulário do autor (`RODADAS: 3`); o HUD ainda não |

Um quadrante **contém** setores.

**ids são técnicos** (`feijao-torto`), nomes são livres (`Feijão Torto`). O id é o
que o save grava e o que o endereço casa; a bancada avisa quando ele tem acento ou
espaço.

---

## O fluxo, e onde cada peça mora

```text
Tela de Entrada   PanelMenu:980   cor, cor, dificuldade, preset → PartidaConfig
      ↓           espera o SFX de confirmação terminar (WaitForSecondsRealtime)
Campanha          CampaignSelectionController   mosaico, setas, tint por dono
      ↓           Set(...) + SetDifficulty + SetQuadrante → LoadScene("Batalha")
Batalha           QuadranteController (-9000)   pinta terreno, construções,
      ↓                                          camadas, rotas e TROPA INICIAL
                  MatchController (0)            aplica a config (ou já foi aplicada)
      ↓           vitória/derrota → grava o SLOT vencedor → Enter
Campanha          republica a config na volta, e o quadrante aparece na cor do dono
```

**Toda a travessia é `PartidaConfig`.** Ele é de **consumo único**: quem produz
chama `Set`, quem consome chama `Apply` + `Clear`. Por isso a volta tem de
republicar — foi o que quase quebrou o laço em silêncio.

---

## O que existe

```text
Assets/Scripts/Campanha/    MundoData · BlocoData · CampanhaData · QuadranteData
                            INoDoMapa · QuadranteController
                            ConstrucaoAssada · UnidadeAssada · CamadaAssada · RotaAssada
                            EconomiaInicialSlot          ← caixa inicial, autoral
                            ProgressoDaCampanha          ← Concluido() nos 3 níveis
                            CampaignProgressStore        ← por SLOT, e dentro do save
Assets/Editor/              MapHelperWindow (a bancada) · MapaTerrenoJson
                            RoadRoutePainterWindow · SceneSanitizerWindow
Assets/DB/Campanha/         Mundo Fixture.asset
Assets/Prefab/Managers/     AudioManager.prefab          ← nas 3 cenas do fluxo
Assets/Scenes/Autoria/      Fixture (26 construções, 6 trechos) · Mundo
Assets/Scenes/              Campanha · Batalha           ← as duas no Build Settings
```

O fixture, quatro quadrantes de tamanhos diferentes:

```text
bloco A · Auridia
 └─ campanha A_IA · "A invasão a Auridia"
     ├─ A_IA_Q1  Feijão Torto     (-18,10)  16×17   272 tiles
     ├─ A_IA_Q2  Terra Firme      (-18,-9)  21×20   420 tiles
     ├─ A_IA_Q3  Peixe Pequeno     (-3,10)  35×17   595 tiles
     └─ A_IA_Q4  Tubarão Branco     (2,-9)  30×20   600 tiles
```

26 construções assadas nos quatro. **Terreno, construções, camadas, rotas e
unidades entram.** `bakedUnidades` e `economiaInicial` seguem **vazios** — os dois
mecanismos existem, falta o autor querer usá-los.

---

## Onde eu parei

### Compila e roda — falta exercitar

A `v8.5.0` e a `v8.5.1` foram escritas contra as APIs lidas, sem build por linha
de comando. Em 2026-09-09 o autor abriu o Q2 na Batalha e ele montou **idêntico
ao mosaico**, o que derruba a dívida para os caminhos exercitados:

```text
✅ QuadranteController.Build      terreno, camadas, rotas, construções
✅ paridade odd-r                 o Q2 (originY ímpar) sai igual ao mosaico
✅ EnsurePartidaConfigApplied     as cores saem do slot, e os prédios batem
✅ áudio                          chega na Batalha sem reclamar
```

**O que ainda ninguém exercitou**, em ordem de risco:

```text
0b        menu → mapa A → turno 5 → menu → mapa B
          → no turno 1 do B o plano nasce VAZIO
save      salvar numa batalha do Q3 → Tela de Entrada → Load
          → abre o Q3, não o Q1
volta     ganhar ou perder → Enter → volta ao mapa com o quadrante pintado
          na cor do slot vencedor
economia  autorar 100000 no slot 0 de um quadrante e ver no painel no turno 1
```

⚠️ **A armadilha do turno 2 continua de pé** enquanto `bakedUnidades` e
`economiaInicial` estiverem vazios: os dois lados abrem sem tropa, e quem não
comprar no turno 1 perde no turno 2. O mecanismo existe; falta autorar.

### O contador 1/4 não tem tela

`ProgressoDaCampanha` responde e ninguém pergunta. Falta o consumo na cena
Campanha (frente paralela), e são três chamadas:

```csharp
ProgressoDaCampanha.FormatarProgresso(mundo, campanha, meuSlot)   // "1/4"
ProgressoDaCampanha.Concluida(mundo, campanha, meuSlot)           // reconhecimento
matchController.TryGetSingleActiveLocalHumanSlot(out meuSlot)     // quem sou eu
```

Campanha vencida **oferece** a saída para a Tela de Entrada — não expulsa.

### O portão de destrave não existe, e saiu do caminho crítico

`Liberado()` é a recursão que **desce** (bloco → campanha → quadrante) e precisa
de navegação de **pai e de irmãos**, que o `MundoData` não expõe: só há
`TryGetBloco/Campanha/Quadrante` por id e `TryGetPorSerial`.

`Concluido()` só **sobe**, e subir é de graça porque as listas de filhos já
existem — por isso ele existe e o portão não. No MVP os quatro quadrantes têm
`destravadoPor` vazio: o portão responderia "liberado" para tudo e **nenhuma tela
mudaria**.

### O progresso só persiste se houver save

O `CampaignProgressStore` deixou de gravar arquivo próprio: virou snapshot dentro
do save. `RecordOwner` escreve no cache estático, e o cache morre com a sessão.
O teste de aceitação do MVP — *sair, voltar, e o mapa continuar contando a mesma
história* — passa a depender de ter havido save.

### A mixagem de SFX está espalhada por três cenas

| cena | cursor vem de | master | ui | move |
|---|---|---|---|---|
| Tela de Entrada | **inline, não é o prefab** | 0.4 | 1 | 1 |
| Campanha | `Cursor.prefab` + override | 0.4 | 0.3 | 0.3 |
| Batalha | `Cursor.prefab` + override | 0.4 | 0.3 | 0.3 |

Os SFX de cursor do menu tocam a **três vezes** o volume das outras cenas, e como
o cursor de lá é inline, mexer no prefab não o alcança. Tudo que seria preciso
para mixar já existe (master + 10 categorias de SFX; master + volume por faixa de
música) — falta **um lugar só** para mexer.

### Volume não é preferência de verdade

**Zero `PlayerPrefs` na árvore inteira**, verificado. Nada sobrevive à troca de
cena, quanto mais a fechar o jogo. Começado e parado para não sair do MVP.

### Depois

```text
1. o contador 1/4 e o fim da campanha (consumo na cena)
2. um lugar só para a mixagem de SFX
3. o portão de destrave — depois de existir uma segunda campanha
4. destravadoPor passa a guardar idSerial em vez de texto
```

### Dívidas com gatilho conhecido

- **O silêncio entre menu e campanha continua.** O `MatchMusicAudioManager` não é
  `DontDestroyOnLoad` (a música da cena que sai morre com ela) e o
  `BuildWorldMosaic()` roda no `Awake` em ordem `-10000`, travando o frame antes
  de qualquer `Start`. Virar prefab resolveu compartilhamento, não persistência.
- **`MatchController.cs` carrega duas frentes.** `isPlayable` (frente paralela) e
  `TrySetStartMoney` (minha) entraram no mesmo arquivo no commit `aac0c48`.
  Reverter um sem o outro é edição manual.
- **`BoardReady` tem um leitor** (`RefreshAllOccupancyVisuals`); os demais
  consumidores ainda não consultam.
- **Nada foi medido em escala.** A bancada e o pintor rodaram sobre 1800 células.
- **`.vscode/settings.json`** aponta pro `.slnx`, e o `.sln` é gerado pela Unity e
  não existe no disco. Sujo de propósito.

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ faltam Suprir, Fundir, Detecção e Spotting
 3. papéis → somente POLÍTICA     ⚠️ as seis fichas existem; RoleData ainda não
 4. variações de papel            perfil/trait depois da extração
 5. CAMPANHA                      🟡 laço, save por endereço, progresso por slot,
                                     0b e economia inicial prontos (não compilados);
                                     falta a TELA do 1/4 e o portão de destrave
```

---

## Armadilhas que importam nesta retomada

| armadilha | regra |
|---|---|
| **`currentTurn` lido como jogada individual** | ele conta **rodadas**: só sobe quando o índice de jogador dá a volta. Passar a vez de um jogador ao outro não mexe nele. Todo número de "turno" que atravessa telas ou vira registro precisa dizer qual dos dois é |
| **translação de recorte em grade hexagonal** | odd-r: a posição de mundo de uma linha depende da PARIDADE do y, não da diferença entre dois y. Traduzir um retângulo autorado em y ímpar para y par inverte a paridade de todas as linhas e cisalha o recorte meia célula — e **nada reclama**, porque o tile continua na célula lógica certa. Se um recorte precisa mudar de y, a paridade tem de sobreviver |
| **cena que parece partida** | a `Campanha` tem `MatchController`, `TurnStateManager` e lista de jogadores porque nasceu da cena-base de batalha. Perguntar "estou numa partida?" olhando o que EXISTE na cena responde sim e age errado. Pergunte ao `MatchController.IsPlayable` — e ao **da própria cena**, não a qualquer um |
| **`== null` da Unity em cache estático** | referência a objeto destruído responde `true` para `== null`. O padrão `if (cached == null) cached = Find(...)` **se auto-cura** entre cenas. "É estático e sobrevive à cena" NÃO é o teste de contaminação; o teste é **"guarda dado ou guarda referência?"** — dado contamina, referência morta se denuncia sozinha |
| **prefab compartilhado levando config de uma cena** | o `AudioManager` virou prefab das três cenas carregando o `playbackMode` da Tela de Entrada, e a Batalha passou a tocar a música do menu. O que é IGUAL nas cenas vai no prefab; o que é DIFERENTE deriva da cena ou vira override |
| **guarda que só sabe MANTER, não iniciar** | `TryEnsureSceneTrackPlayback` exigia que a faixa já estivesse tocando para protegê-la — confiava no `Start`, que tem saídas antecipadas. Guarda de invariante tem de perguntar "esta cena tem faixa própria?", não "a faixa própria já está tocando?" |
| **nome de cena como identidade de save** | o save gravava só o nome da cena, e toda partida de campanha é "Batalha". Carregar um save do Q3 pintava o Q1 com as peças do Q3 — e **funcionava** se o save viesse do Q1 |
| **cor tomada como identidade** | a cor é escolhida no menu, por partida. Dono, progresso e tropa endereçam por **slot**; a cor se resolve na hora de pintar. Violado em 3 pontos independentes num dia só, e nenhum deu erro |
| **`SetSlotIndex` tomado como "mudar de dono"** | ele só escreve o campo e **deixa a cor como estava**. Quem muda dono é `SetOwnerSlot` (construção) ou `SpawnAtCellForSlot` (unidade) — os mesmos caminhos da captura |
| **pintar antes da configuração chegar** | `QuadranteController` é `-9000`; o `Awake` do `MatchController` é `0`. Quem pinta antes tem de chamar `EnsurePartidaConfigApplied` primeiro |
| **`PartidaConfig` tomado como estado** | é de **consumo único**. Quem volta pra uma cena tem de republicar, senão a cena nasce com as cores serializadas nela |
| **`SetTile` tomado como cópia** | leva o tile e **descarta rotação, espelho e cor**. Só aparece em arte que tem lado; terreno é o caso em que o defeito é invisível |
| **`SetTransformMatrix` ignorado calado** | um tile com `LockTransform` faz o tilemap descartar a matriz sem avisar. Destravar (`SetTileFlags(None)`) antes de orientar |
| **recortar uma sequência como se fosse conjunto** | rota é ordenada e os consumidores leem PARES. Tirar uma célula do meio COLA as vizinhas numa aresta que ninguém desenhou |
| **`IsRouteValid` é tudo-ou-nada** | uma célula inválida descarta o desenho da rota INTEIRA |
| **`Build` fora do Play** | tudo que ele escreve é serializado. Numa cena compartilhada por todos os quadrantes, salvar grava o layout de um dentro dos outros |
| **camada decorativa é tilemap IRMÃO** | `ClearAllTiles` no tabuleiro não encosta nela |
| **contador de id recalculado do máximo** | certo no `UnitSpawner` (ids morrem com a partida), **errado** no mundo (o registro sobrevive ao nó) |
| **hexágono tratado como grade quadrada** | é odd-r: linha ímpar desloca meia célula |
| **campo sem leitor tomado como inofensivo** | `fieldEntries` tinha zero leitores **e** obrigava sete catálogos a existir. Por isso `UnidadeAssada` nasceu **sem** HP, combustível e elite |
| **generalizar do que se encontra sem checar a razão** | **perguntar por que aquilo existe antes de construir em cima** |
| **`ConstructionSector` default** | é `Alpha = 0`, não `None = -1`. Esquecer o setor não dá erro: dá plano degenerado |
| **id com acento ou espaço** | o YAML escapa (`"Feij\xE3o Torto"`) e o texto é digitado em dois lugares que precisam bater |
| **parse frágil de `.asset` virando afirmação** | contagem por faixa de linha é confiável; `$3` de `awk` não |
| **`.asset` em disco tomado como estado atual** | Inspector marca `dirty` e **não grava** |
| **`sed` em C# sem conferir chaves** | comeu um `}` de fechamento. Contar profundidade antes de seguir |
| **apagar dado do autor sem perguntar** | perguntar, sempre |
| **consulta antes da pintura terminar** | `SectorManager` assa o vazio e **cacheia**. Daí o `-9000` e o portão |
| **construção na interseção de quadrantes** | a faixa é para o **chão**. Peça ali nasce nos dois |
| **`Grid` divergente entre autoria e Batalha** | cell size/layout/swizzle diferentes torcem toda tradução de coordenada |
| farol tratado como lock | promessa e claim distribuem preferência; nunca proíbem |
| singleton de mapa atravessando cenas | `BeachManager` e `SectorManager` são da cena corrente |
| posição hipotética criando verdade | nenhum cálculo provisório atualiza FOW, ocupação ou caches |

---

## Documentos de referência

| documento | uso |
|---|---|
| [`Planos/plano_campanha.md`](Planos/plano_campanha.md) | **o tronco** — autoria, recorte, progresso, cenas, bloqueios, teste |
| [`Planos/briefing_cena_campanha.md`](Planos/briefing_cena_campanha.md) | o contrato entre as duas frentes |
| [`relatorio_v8.5.1.md`](relatorio_v8.5.1.md) | o que atravessa a cena — save por endereço, isPlayable, o 0b |
| [`relatorio_v8.5.0.md`](relatorio_v8.5.0.md) | o laço fecha, e o dono deixa de ser uma cor |
| [`relatorio_v8.4.1.md`](relatorio_v8.4.1.md) | orientação, rotas partidas e identidade estável |
| [`relatorio_v8.4.0.md`](relatorio_v8.4.0.md) | o dia em que o catálogo parou de dizer onde |
| [`AI Behavior/Transporte.md`](AI%20Behavior/Transporte.md) | estados, promessas, coleta e entrega |
| [`arquitetura/acoes_transacionais.md`](arquitetura/acoes_transacionais.md) | lei de compromisso e rollback |

---

## Regras de trabalho

- **Nada no jogo é definitivo antes do compromisso da ação.**
- **Plano pedido não autoriza implementação.**
- **Verificar antes de documentar.** Busca vazia não prova ausência, e `.asset`
  em disco não prova estado.
- **Perguntar por que uma coisa existe antes de construir em cima dela.**
- **Uma frente por commit.** `git add .` só no churn.
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Não salvar `.cs` enquanto o autor testa em Play.**
- Fechar o dia pela skill `.claude/skills/fechamento-do-dia/SKILL.md`.
