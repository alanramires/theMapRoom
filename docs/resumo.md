# Resumo — onde estamos e o que vem

Ponto de retomada. Atualizado em 2026-09-10, **depois** da tag `v8.5.2`.
Leia isto primeiro.

---

## Estado

`v8.5.2` tagueada e publicada. Relatório:
[`relatorio_v8.5.2.md`](relatorio_v8.5.2.md).

```text
v8.3.0   o primeiro quadrante pintou      361 tiles, 2 ms, cena vazia
v8.4.0   o catálogo parou de dizer ONDE   três camadas de layout removidas
v8.4.1   a peça tem lado                  orientação, rotas partidas, identidade
v8.5.0   o laço fecha                     volta, dono por slot, tropa inicial
v8.5.1   o que atravessa a cena            save por endereço, isPlayable, 0b
v8.5.2   a forma casa com o dado           quadradinhos, paridade, fim de partida
```

**O MVP virou tela.** Menu → Campanha → Batalha → volta → o mapa pintado, o
placar, e o registro de quem tomou o quê em quantas rodadas.

### A descoberta que organiza o resto

> **A forma tem que casar com o dado.** Contável vira quadrado; contínuo vira
> barra.

Um contador de campanha desenhado como barra forçava o jogador a extrair uma
**contagem** de uma **proporção** — e o autor teve que explicar o que o cinza
significava. Trocado por quatro quadradinhos, o denominador parou de precisar de
explicação porque passou a estar desenhado.

A mesma régua, aplicada à batalha, responde o contrário: lá o dado é
`controlledCapturePoints / total`, com captura **parcial** entrando na conta. Um
prédio meio capturado não é meio cubinho.

```text
campanha   ■ meu  ■ dele  □ em aberto     contável   → quadrados
batalha    ▓ meu  ▓ dele  ░ em aberto     contínuo   → barra
```

As duas telas contam a mesma história, cada uma na forma que o dado dela merece.

### As descobertas anteriores, que seguem valendo

> **v8.5.1 — a cena `Campanha` parece uma partida e não é.**

Ela tem `MatchController`, `TurnStateManager` e lista de jogadores porque nasceu
da cena-base de batalha. Perguntar *"estou numa partida?"* olhando o que **existe**
na cena responde sim e age errado. Pergunte ao `MatchController.IsPlayable` — e ao
**da própria cena**, não a qualquer um.

> **v8.5.0 — cor não é identidade. É uma fantasia que o slot veste por uma
> partida.**

As duas cores são escolhidas no menu. Tudo que atravessa a fronteira entre autoria
e partida — ou entre uma partida e a seguinte — endereça por **slot**, e a cor se
resolve só na hora de pintar, por `GetTeamIdForSlot`. Vale para o tint do mapa, os
quadradinhos, o registro do vencedor e o dono de cada construção.

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
                            CampanhaManager              ← ponte da cena p/ o placar
                            CampaignProgressStore        ← por SLOT, e dentro do save
Assets/Editor/              MapHelperWindow (a bancada) · MapaTerrenoJson
                            RoadRoutePainterWindow · SceneSanitizerWindow
Assets/DB/Campanha/         Mundo Fixture.asset
Assets/Scripts/UI/          BarraVencedorController      ← um quadrado por quadrante
                            PanelVitoriaController       ← os botões do fim de partida
Assets/Prefab/Managers/     AudioManager.prefab          ← nas 3 cenas do fluxo
Assets/Prefab/              Panel_turn · Panel_vitoria · MenuRoot · Cursor
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

26 construções assadas. **O `A_IA_Q2` é o único autorado por inteiro:** caixa
inicial de 10000 para cada slot e um `chinook` no slot 0. Os outros três seguem
sem tropa e sem caixa — o padrão, e o que o campo significa quando ausente.

---

## Onde eu parei

### O que o autor exercitou jogando

Não há build por linha de comando: o que foi provado, foi ele jogando. E foi
assim que a paridade do Q2, o título perdido e o `bar_tvencedor` apareceram.

```text
✅ o laço inteiro        menu → campanha → batalha → volta → mapa pintado
✅ paridade odd-r        o Q2 (originY ímpar) monta idêntico ao mosaico
✅ cores pelo slot       tint, quadradinhos e registro concordam
✅ tropa e caixa inicial  A_IA_Q2 tem chinook no slot 0 e 10000 para cada lado
✅ save/load de campanha  volta ao quadrante em foco
```

### O `0b` NUNCA rodou

Os hooks de `sceneLoaded` do `ObjectiveManager` e do `AITacticalAnalyzer` estão
escritos desde a `v8.5.1` e ninguém exercitou. É o tipo de bug que só aparece na
segunda partida, e nenhuma sessão chegou lá.

> mapa A → turno 5 → menu → mapa B. No turno 1 do B, o plano da IA tem de nascer
> **vazio**.

Sintoma se falhar: a IA do segundo mapa persegue setor que não existe ali.

### O progresso tem um registro de mentira dentro

O `RODADAS: 3` do Terra Firme veio da **armadilha do turno 2** — a partida que
acabou por zero unidades, não por jogo. Agora que o Q2 tem caixa inicial e tropa,
**limpar o progresso antes de testar para valer**, senão as anotações misturam
resultado real com resultado do setup.

### Três lugares respondem "de quem é este quadrante"

```text
CampaignSelectionController.GetWonSectorCounts    conta por slot
CampaignSelectionController.GetQuadrantOwners     lista por quadrante
ProgressoDaCampanha                               a versão que sobe a hierarquia
```

Concordam hoje porque todos consultam o `CampaignProgressStore`. Vão discordar no
dia em que "concluído" virar recursivo — *campanha concluída = todos os quadrantes
dela* — que é o que o portão de destrave vai precisar. Gatilho conhecido, conserto
conhecido: os dois primeiros delegam ao terceiro.

### Arestas de tela, todas conhecidas

- **O painel de inspeção tapa a borda direita do mapa.** Navegar até um quadrante
  de lá é inspecionar algo que não se vê. Ideia parqueada: o painel escolher o
  lado oposto ao quadrante em foco.
- **Sem o `presentationTextOverride`, a campanha cai no "Turno {n}"** do ramo de
  batalha. Visível, então não é falha silenciosa — mas turno não significa nada
  numa tela de seleção.
- **Os números laterais e os quadradinhos dizem a mesma coisa** com 4 quadrantes.
  Ganham utilidade quando uma campanha tiver 12.
- **A mixagem de SFX segue espalhada** por três cenas, uma delas com cursor
  inline, e o menu toca a 3× o volume das outras.
- **O silêncio entre menu e campanha** continua: `MatchMusicAudioManager` sem
  `DontDestroyOnLoad`, e `BuildWorldMosaic` no `Awake` em ordem `-10000`.

### Depois

```text
1. testar o 0b — é o único bloqueio do MVP que nunca foi exercitado
2. o fim da campanha: 4/4 oferece a volta à Tela de Entrada
3. unificar as três contagens (gatilho: o portão de destrave)
4. o portão hierárquico — Liberado() precisa de pai e irmãos, que o MundoData
   não expõe
```

⚠️ **`MatchController.cs` carrega duas frentes.** `isPlayable` (paralela) e
`TrySetStartMoney` (minha) entraram no mesmo arquivo no `aac0c48`. Reverter um sem
o outro é edição manual.

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ faltam Suprir, Fundir, Detecção e Spotting
 3. papéis → somente POLÍTICA     ⚠️ as seis fichas existem; RoleData ainda não
 4. variações de papel            perfil/trait depois da extração
 5. CAMPANHA                      🟡 o laço roda de ponta a ponta e tem tela;
                                     falta EXERCITAR o 0b e o portão de destrave
```

---

## Armadilhas que importam nesta retomada

| armadilha | regra |
|---|---|
| **forma que não casa com o dado** | contável desenhado como barra obriga o jogador a extrair contagem de proporção; contínuo desenhado como quadrado finge discreto o que é fracionário. Antes de escolher barra ou quadrado, pergunte se o dado é contável |
| **busca por nome que falha calada** | `FindNamedChild("bar_vencedor")` com o objeto chamado `bar_tvencedor` não desenhou nada, sem erro nem log. Busca por nome dispensa arrastar referência, e o preço é falhar em silêncio — **toda busca por nome tem de avisar quando não acha** |
| **duas coisas amarradas no mesmo campo** | o `presentationTextOverride` decidia o título E se o placar aparecia. Quando a campanha precisou dos dois, alguém apagou o texto para o placar voltar, e o título se perdeu. Campo que decide duas coisas separa no dia em que elas divergirem |
| **escape `
` em script que gera código** | a ferramenta processa a barra antes do Python e o literal C# não casa. Usar `chr(92)`. **Está nesta tabela e eu tropecei duas vezes antes de olhar** |
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
| [`relatorio_v8.5.2.md`](relatorio_v8.5.2.md) | a forma tem que casar com o dado — quadradinhos, paridade, fim de partida |
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
