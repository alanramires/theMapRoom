# Resumo — onde estamos e o que vem

Ponto de retomada. Atualizado em 2026-10-01, **depois** da tag `v8.6.1`.
Leia isto primeiro.

---

## Estado

`v8.6.1` tagueada e publicada. Relatório:
[`relatorio_v8.6.1.md`](relatorio_v8.6.1.md).

```text
v8.4.0   o catálogo parou de dizer ONDE   três camadas de layout removidas
v8.4.1   a peça tem lado                  orientação, rotas partidas, identidade
v8.5.0   o laço fecha                     volta, dono por slot, tropa inicial
v8.5.1   o que atravessa a cena            save por endereço, isPlayable, 0b
v8.5.2   a forma casa com o dado           quadradinhos, paridade, fim de partida
v8.6.0   a etiqueta muda de dono          rally em lista, eixo escrito, 3 perfis
v8.6.1   configurado ≠ valendo            perfis autorados, portões fechados
```

**O MVP virou tela.** Menu → Campanha → Batalha → volta → o mapa pintado, o
placar, e o registro de quem tomou o quê em quantas rodadas. O autor jogou o Q1
até a rodada 3 com o perfil Médio.

### A descoberta da v8.6.1, e a direção que ela dá

> **O que está configurado não é o que está valendo.**

Cinco vezes num dia, uma tela, um asset ou uma lembrança mostrava uma coisa e o
jogo fazia outra, e nenhuma deu erro:

```text
Inspector "Base Preset: Difícil"     o jogo usava o catálogo
perfis Fácil/Médio "como a média"    eram cópia do perfil de teste Gulosa
números do perfil Difícil            ninguém lê número nenhum, e eram os do normal
Retaguarda "massa inimiga"           posição REAL das tropas, sob névoa
"limpar o progresso"                 o progresso nunca foi para o disco
```

A direção, tirada do [guia de modding](modding/guia%20de%20modding.md) do autor:
deixar o jogo **auditável**. Cada valor sabe dizer de onde veio, e cada save se
lê sem arqueologia.

### A descoberta da v8.6.0, que segue valendo

> **Uma resposta só não serve a mais de um respondente.**

Três coisas tropeçaram nisso na mesma versão, e nenhuma delas dava erro:

```text
rally         um int   respondia "de quem e"      e o predio era dos DOIS lados
eixo          o angulo respondia "por onde vou"   e o autor queria escolher
dificuldade   hardMode respondia "o que eu faco"  seis lampadas, uma chave so
```

O remédio foi o mesmo nas três: **a etiqueta muda de dono**. O rally passou a ter
lista de slots, o eixo virou grafo escrito no quadrante, e cada portão de
comportamento voltou a perguntar o que ele queria saber — `FazHandoffEmProfundidade`,
`RespeitaListaBanida` — em vez de "eu sou o difícil?".

E o teste que denuncia o caso: se você precisa **desligar** uma parte da resposta
para a outra metade se comportar, a entidade está errada, não o flag.

### A descoberta da v8.5.2, que segue valendo

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

29 construções assadas, **todas no Q1 e no Q2**:

```text
A_IA_Q1  HQ + fábricas cada lado    sem caixa: jogável, a renda do HQ (6000) entra antes da 1ª compra
A_IA_Q2  HQ + fábricas cada lado    10000 por slot, chinook no slot 0
A_IA_Q3  NENHUMA construção         2000 no slot 1, que não compra nada
A_IA_Q4  NENHUMA construção         2000 no slot 0, que não compra nada
```

Sem HQ, os dois lados do Q3 e do Q4 entram no modo rebelde (v8.6.1, Frente 4).

---

## Onde eu parei

### O que o autor exercitou jogando

`bash tools/compilar.sh` prova que compila. Comportamento só se prova jogando, e
foi assim que a paridade do Q2, o título perdido e o `bar_tvencedor` apareceram.

```text
✅ o laço inteiro        menu → campanha → batalha → volta → mapa pintado
✅ paridade odd-r        o Q2 (originY ímpar) monta idêntico ao mosaico
✅ cores pelo slot       tint, quadradinhos e registro concordam
✅ tropa e caixa inicial  A_IA_Q2 tem chinook no slot 0 e 10000 para cada lado
✅ save/load de campanha  volta ao quadrante em foco
✅ perfil Médio           Q1 até a rodada 3, pelo menu (save do slot 2)
```

### A IA obedece o perfil, mas só nos toggles

O catálogo está ligado na Batalha, e **nenhum portão pergunta mais "sou o
difícil?"**. Os três perfis foram montados a partir das frases do autor (v8.6.1,
Frente 2): Fácil com ⅓ da renda e sem blitzkrieg, Médio com blitzkrieg, Difícil
com spam de soldado e os números do lado hard.

O que falta, em ordem:

1. **Decidir `poupaPraElite` × `eliteSaveTurns`.** O Difícil tem "poupa"
   desligado e poupança de 2 turnos. A proposta é o toggle desligado zerar a
   poupança.
2. **Fase 2, os números.** Nenhum dos ~70 valores do perfil é lido:
   - ~20 getters do `AIController`, cada um num ponto único (fácil);
   - ~50 campos públicos do `AIShoppingPlanner`, que viram campo privado com
     `FormerlySerializedAs`, mais getter.

   O mapa campo a campo está no gerador
   ([AIPresetGeneratorWindow.cs:275](../Assets/Editor/AI/AIPresetGeneratorWindow.cs)).
   Use ele, e não busca por nome.
3. **"Perfil em uso" no Inspector**, e **resolver o perfil no Play direto**. Hoje,
   abrir a Batalha no editor e dar Play roda **sem perfil** (os flags antigos da
   cena). Só o menu e o load de save resolvem o perfil
   ([AIController.Lifecycle.cs:167](../Assets/Scripts/Match/AI/AIController.Lifecycle.cs)).

### Auditável: a direção que o autor escolheu

Do guia de modding, o que se aplica ao jogo antes de ter mod nenhum, em ordem de
custo:

1. **Inspetor de save** (`Tools ▸ Save Inspector`). O `.tmrsave` é um zip com
   `manifest.json`, `game.json`, `replay.json` e `jogadas.json`; hoje lê-lo
   exige extrair à mão.
2. **Valor efetivo com a origem** (perfil, cena ou reserva). Vira obrigatório
   quando a fase 2 religar os números.
3. **Resultado com motivo** no progresso (HQ, eliminação, zero unidades).

### O teste da blitzkrieg reprova no código atual

O [`teste_blitzkrieg.md`](AI%20Behavior/teste_blitzkrieg.md) do autor pede troca
sem plano, mas a troca mora no planner. Os casos C, E e F falham em
[Handoff.cs:97](../Assets/Scripts/Match/AI/2.%20Planner/AIController.PlanEvaluator.Handoff.cs):
- substituto que não alcança o prédio é aceito;
- o vizinho do prédio conta como chegar nele;
- ninguém confere se o substituto já agiu.

### O eixo autorado precisa de uma ferramenta melhor

Escrever o grafo funciona: ficha do prédio → dropdown → grava na lista do
quadrante, e o desenho no Scene View mostra o que o jogo vai seguir. O veredito
do autor:

> *"não é prático pra dar manutenção, mas resolve, e é o que temos no momento"*

O gargalo é montar o grafo prédio por prédio sem ver a linha se formando.
Melhorar o dropdown é polir o gesto errado — o caminho é desenhar no Scene View:
clicar o QG, clicar os setores na ordem. E lembrar: **o desenho só se refaz quando
se aperta "Desenhar eixos" de novo.**

### O `0b` NUNCA rodou

Os hooks de `sceneLoaded` do `ObjectiveManager` e do `AITacticalAnalyzer` estão
escritos desde a `v8.5.1` e ninguém exercitou. É o tipo de bug que só aparece na
segunda partida, e nenhuma sessão chegou lá.

> mapa A → turno 5 → menu → mapa B. No turno 1 do B, o plano da IA tem de nascer
> **vazio**.

Sintoma se falhar: a IA do segundo mapa persegue setor que não existe ali.

### O progresso só existe dentro de um save

O `CampaignProgressStore` vive em memória, é zerado a cada Play e a cada Novo
Jogo, e só vai para o disco dentro do `.tmrsave`. Não há o que "limpar". Para
jogar os quatro quadrantes e manter o placar, é uma sessão só, ou salvar e
carregar.

Arquivo órfão de versão antiga, que o código não lê:
`AppData/LocalLow/Sistemas Info/The Map Room/CampaignProgress/mundo fixture__A_IA.json`.
Apagar só com o autor confirmando.

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
1. construções no Q3 e no Q4 — sem elas, metade da campanha não se joga
2. testar o 0b — jogar dois quadrantes em sequência já é o teste
3. o fim da campanha: 4/4 oferece a volta à Tela de Entrada
4. unificar as três contagens (gatilho: o portão de destrave)
5. o portão hierárquico — Liberado() precisa de pai e irmãos, que o MundoData
   não expõe
```

Pequenos, conhecidos:
- **"Oububro/2026"** no rótulo da Campanha e da Batalha. A Tela de Entrada diz
  "Outubro".
- **`Papeis.md` está em Windows-1252**; os acentos aparecem quebrados no git.

Pós-MVP, com rascunho feito: o **Quero Spotting**
([QueroSpottingWindow.cs](../Assets/Editor/QueroSpottingWindow.cs)) e o "quadro de
missões". **Antes de retomar missões, ler o
[`contrato_missoes.md`](AI%20Behavior/contrato_missoes.md)** (v7.2.1): ele já
desenha `SpottingDeCobertura`, e a conversa da v8.6.1 redescobriu partes dele sem
saber. O `IsForwardObserverSpot` vai cair, porque o autor não quer lugar
autorado de spotting.

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

O degrau 3 ganhou vizinho na v8.6.0: a **doutrina** saiu do `hardMode` e virou
capacidade nomeada, lida de um perfil autorado. É o mesmo movimento do degrau —
política deixando de ser `if` espalhado — só que na camada do general, não na do
papel. Na v8.6.1 a viagem das **capacidades** terminou; os *valores* ainda não
saíram do lugar.

---

## Armadilhas que importam nesta retomada

| armadilha | regra |
|---|---|
| **configurado tomado como valendo** | o Inspector mostra o campo, não o que o jogo usa. O `Base Preset` dizia Difícil e era só a reserva de um catálogo que cobre tudo. Antes de concluir pelo que a tela mostra, pergunte **quem responde em runtime** |
| **perfil copiado de um perfil de teste** | o Médio era o `AIPreset_Gulosa` renomeado, e o Fácil uma cópia dele: herdaram toggles de teste que contradiziam a doutrina escrita no próprio asset. Asset renomeado carrega o passado; confira contra a doutrina |
| **gerador que copia só um lado do par** | o gerador de preset copia o lado **normal** dos pares normal/hard. Os três perfis tinham os mesmos ~70 números. Religar leitura de valor sem comparar antes enfraquece o Difícil calado |
| **busca por nome tomada como ausência** | `intel.lookbackTurns` "sem leitor" se chama `IntelShoppingLookbackTurns` no shopping. Para mapear preset → cena, o gerador é o mapa; grep por nome não é |
| **Play direto na Batalha** | sem menu e sem save, a dificuldade não chega e a IA roda **sem perfil**, com os flags antigos da cena. Teste de perfil se faz pelo menu |
| **ferramenta que lê o fato em vez do observador** | a Retaguarda tira a "massa inimiga" da posição real das tropas. Para geografia o autor aceita; para decidir onde pedir olho é wallhack por procuração |
| **forma que não casa com o dado** | contável desenhado como barra obriga o jogador a extrair contagem de proporção; contínuo desenhado como quadrado finge discreto o que é fracionário. Antes de escolher barra ou quadrado, pergunte se o dado é contável |
| **busca por nome que falha calada** | `FindNamedChild("bar_vencedor")` com o objeto chamado `bar_tvencedor` não desenhou nada, sem erro nem log. Busca por nome dispensa arrastar referência, e o preço é falhar em silêncio — **toda busca por nome tem de avisar quando não acha** |
| **duas coisas amarradas no mesmo campo** | o `presentationTextOverride` decidia o título E se o placar aparecia. Quando a campanha precisou dos dois, alguém apagou o texto para o placar voltar, e o título se perdeu. Campo que decide duas coisas separa no dia em que elas divergirem |
| **escape `
` em script que gera código** | a ferramenta processa a barra antes do Python e o literal C# não casa. Usar `chr(92)`. **Está nesta tabela e eu tropecei duas vezes antes de olhar** |
| **`currentTurn` lido como jogada individual** | ele conta **rodadas**: só sobe quando o índice de jogador dá a volta. Passar a vez de um jogador ao outro não mexe nele. Todo número de "turno" que atravessa telas ou vira registro precisa dizer qual dos dois é |
| **translação de recorte em grade hexagonal** | odd-r: a posição de mundo de uma linha depende da PARIDADE do y, não da diferença entre dois y. Traduzir um retângulo autorado em y ímpar para y par inverte a paridade de todas as linhas e cisalha o recorte meia célula — e **nada reclama**, porque o tile continua na célula lógica certa. Se um recorte precisa mudar de y, a paridade tem de sobreviver |
| **nome interno que não casa com o botão** | a Tela de Entrada mostrava FÁCIL/MÉDIO/DIFÍCIL e o enum era Iniciante/Facil/Competitiva — o perfil chamado "facil" mudava o botão MÉDIO. Resolvido na v8.6.0 reduzindo a três com os nomes do jogador. Quando um rótulo de tela e um identificador interno convivem, **mostre os dois lado a lado na ferramenta** |
| **entrada de enum extinta num asset** | catálogo gravado com `dificuldade: 4` (o antigo `Competitiva`) não casa com nada depois da redução, e o perfil cai no caminho de reserva **calado**. Toda tabela dificuldade→asset precisa podar entrada que não existe mais, e dizer que podou |
| **reassar não conserta autoria** | a lista de eixos mora fora da seção assada de propósito; o bake não a toca. "Mudei e reassei e não mudou" é sintoma de estar olhando a fonte errada, não de bake quebrado |
| **o recorte de autoria vaza para o arquivo da cena** | não o recorte — o efeito dele. O `SectorManager` serializa `sectorInfos`, então salvar a Fixture com um quadrante em foco grava a lista **truncada**. Na autoria é cache e se refaz; mas é por isso que a `Fixture.unity` encolheu ~600 linhas na v8.6.0, e **não é regressão** |
| **editor que desenha o que o jogo não vai rodar** | o desenho de eixos só lia o quadrante que a Batalha pintou, e na autoria nada está pintado — mostrava sempre o leque automático. Editor divergindo do jogo é pior que editor sem desenho: a ferramenta passa a ensinar errado |
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
| [`relatorio_v8.6.1.md`](relatorio_v8.6.1.md) | configurado ≠ valendo — perfis, portões, auditável |
| [`modding/guia de modding.md`](modding/guia%20de%20modding.md) | o guia do autor; a direção "auditável" sai dele |
| [`AI Behavior/capturador_politicas.md`](AI%20Behavior/capturador_politicas.md) | as quatro políticas do capturador, e o [teste da blitzkrieg](AI%20Behavior/teste_blitzkrieg.md) |
| [`AI Behavior/contrato_missoes.md`](AI%20Behavior/contrato_missoes.md) | missões (brainstorming da v7.2.1) — ler antes de retomar o quadro de missões |
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
