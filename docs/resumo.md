# Resumo — onde estamos e o que vem

Ponto de retomada. Atualizado em 2026-09-05, **depois** da tag `v8.5.0`.
Leia isto primeiro.

---

## Estado

`v8.5.0` tagueada e publicada. Relatório:
[`relatorio_v8.5.0.md`](relatorio_v8.5.0.md).

```text
v8.3.0   o primeiro quadrante pintou      361 tiles, 2 ms, cena vazia
v8.4.0   o catálogo parou de dizer ONDE   três camadas de layout removidas
v8.4.1   a peça tem lado                  orientação, rotas partidas, identidade
v8.5.0   o laço fecha                     volta, dono por slot, tropa inicial
```

**O fluxo existe ponta a ponta e volta.** Menu → Campanha → Batalha → Campanha.

### A descoberta que organiza o resto

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
                            CampaignProgressStore        ← agora por SLOT
Assets/Editor/              MapHelperWindow (a bancada) · MapaTerrenoJson
                            RoadRoutePainterWindow · SceneSanitizerWindow
Assets/DB/Campanha/         Mundo Fixture.asset
Assets/Prefab/Managers/     AudioManager.prefab          ← nas 3 cenas do fluxo
Assets/Scenes/Autoria/      Fixture (13 construções, 6 trechos) · Mundo
Assets/Scenes/              Campanha · Batalha           ← as duas no Build Settings
```

O fixture, quatro quadrantes de tamanhos diferentes:

```text
bloco A · Auridia
 └─ campanha A_IA · "A invasão a Auridia"
     ├─ A_IA_Q1  Feijão Torto     (-18,10)  16×17   272 tiles · 13 construções
     ├─ A_IA_Q2  Terra Firme      (-18,-9)  21×20   420 tiles
     ├─ A_IA_Q3  Peixe Pequeno     (-3,10)  35×17   595 tiles
     └─ A_IA_Q4  Tubarão Branco     (2,-9)  30×20   600 tiles
```

**Terreno, construções, camadas, rotas e unidades entram.** As listas de unidade
estão vazias de propósito — o mecanismo existe, falta você pintar.

---

## Onde eu parei

### ⚠️ Nada da `v8.5.0` foi compilado

Não há build por linha de comando. Todo o código foi escrito contra as APIs
lidas. **Confira o Console antes de concluir qualquer coisa.**

O teste que fecha o laço: menu → Amarelo vs Vermelho → quadrante → perder de
propósito (rendição serve) → Enter → o mapa volta com aquele quadrante pintado na
cor do slot 1.

### O `0b` mudou de conta — revisado, não consertado

O resumo antigo dizia "`sceneLoaded` nos 4 managers". **Eles não são o mesmo
problema:**

| manager | o que carrega | veredito |
|---|---|---|
| `AITacticalAnalyzer` | `operationsBySlot` | limpar. Estado indexado por slot, e o slot 0 da próxima é outra pessoa |
| `ObjectiveManager` | `plans` | limpar. Quem limpa hoje é **só o `RestoreSaveData`** — carregar save limpa, começar partida nova não |
| `HexCohabitationVisualManager` | `cachedTurnStateManager`, `cachedMatchController` | limpar, mas é outro bug: referências a objetos da cena anterior, já destruídos |
| `AIShoppingPlanner` | quase tudo é *tunable* serializado | **provavelmente NÃO deve limpar** — configuração deve atravessar cenas |

**Próximo passo concreto:** ir campo a campo no `AIShoppingPlanner` separando
tunável de estado, **antes** de escrever qualquer hook. Um `Clear()` ali apagaria
configuração, não contaminação.

### A decisão de economia que ninguém tomou

`Batalha.unity` serializa `startMoney: 0` e `actualMoney: 0` nos dois slots, com
`allowDefeatForZeroUnits: 1`. A derrota por zero unidades roda a partir do **turno
2**. A renda chega no turno 1 e dá pra comprar — mas **quem não comprar perde no
turno 2 sem entender por quê.**

Três saídas, e é escolha de design:

```text
1. assar unidades iniciais            já é possível — só pintar e assar
2. dar caixa inicial ao bake          campo novo, precisa de leitor
3. dar carência à derrota por 0       já existe o toggle
```

### Depois

```text
1. o 0b, começando pelo AIShoppingPlanner
2. destravadoPor passa a guardar idSerial em vez de texto
3. avaliação de destrave — hoje só existem os campos
4. lastTurn: separar dono (muda sempre) de marca (nunca piora)
```

### Dívidas com gatilho conhecido

- **O silêncio entre menu e campanha continua.** Duas causas, as duas de pé: o
  `MatchMusicAudioManager` **não** é `DontDestroyOnLoad` (a música da cena que sai
  morre com ela), e o `BuildWorldMosaic()` roda no `Awake` da
  `CampaignSelectionController` em ordem `-10000` — todo `Awake` roda antes de
  qualquer `Start`, então o mosaico trava o frame e a música é a última da fila.
  Virar prefab não resolveu isso; resolveu compartilhamento de configuração.
- **`lastTurn` é *último*, não *melhor*, e mora junto do dono.** Perder o
  quadrante apaga que você o tomou em 11 turnos.
- **`BoardReady` tem um leitor** (`RefreshAllOccupancyVisuals`); os demais
  consumidores ainda não consultam.
- **Nada foi medido em escala.** A bancada e o pintor rodaram sobre 1800 células.
- **`.vscode/settings.json` voltou para `.slnx`**, desfazendo o `769a3dc`. O
  `.sln` é gerado pela Unity e não existe no disco. Deixado sujo de propósito.

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ faltam Suprir, Fundir, Detecção e Spotting
 3. papéis → somente POLÍTICA     ⚠️ as seis fichas existem; RoleData ainda não
 4. variações de papel            perfil/trait depois da extração
 5. CAMPANHA                      🟡 o laço fecha e o progresso é por slot;
                                     falta o 0b e a decisão de economia
```

---

## Armadilhas que importam nesta retomada

| armadilha | regra |
|---|---|
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
