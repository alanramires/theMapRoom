# v9.1.0 — o save manda na partida

Esta é a versão do primeiro build de publicação. Não houve frente de arquitetura.
O dia foi um teste de pré-publicação conduzido pelo autor, com um checklist, e
cada item marcado "x" virou uma correção. O fio que amarra quase todas elas:

> *"o load deveria ler o contrato e sobrescrever o contrato pelo que tá no save"*

Antes desta versão, o save e a cena competiam pelo estado da partida, e a cena
ganhava várias vezes. Agora o que define a partida carregada é o save; a cena
só diz onde as coisas estão.

---

## Frente 1 — o save manda na partida *(o Y)*

### O load aplicava o contrato da cena

A cena Batalha tem um contrato de teste (PvP, configurado pelo autor para os
próprios testes). Um load feito a partir dela abria a partida com esse contrato,
e não com o do save: os jogadores, quem é IA, a dificuldade e as regras
(`gameSetup`) vinham da cena.

A correção foi armar o contrato **antes** de a cena carregar.
`SaveGameManager.ArmSaveContractForSceneLoad` lê o save (o mesmo
`TryReadSaveForAudit` do Save Inspector), ordena os jogadores por slot e preenche
o `PartidaConfig` com o que está no save. Isso inclui o preset de regras,
gravado agora em `SaveGameData.gameSetupSaved` / `gameSetupPreset`. Quando a
cena acorda, `MatchController.Awake` consome o contrato como faria vindo da Tela
de Entrada. Se a leitura falha, o contrato é limpo (`PartidaConfig.Clear`), para
não sobrar um contrato pela metade.

O autor resumiu o porquê melhor do que eu: *"o fato de eu mudar as configurações
da batalha é irrelevante, já que o contrato da tela de entrada vai ditar"*. O
load passou a seguir essa mesma regra.

### A cortina presa em "carregando o turno"

O sintoma era a tela de cortina parada para sempre depois do load, sem nenhum
comando funcionando. A causa estava na cena, não no load:
`pendingTurnStartUpkeep` e `pendingTurnStartEconomy` eram campos **serializados**
do `MatchController`. A cena Batalha tinha sido salva com os dois em `1`. O
`IsTurnBoardReady` espera que não haja efeito de início de turno pendente, e o
load nunca roda o início de turno. Resultado: os efeitos ficavam pendentes para
sempre.

A lição é a mesma da tabela dos três andares (CLAUDE.md): **estado de partida
salvo numa cena é contaminação**. Os campos viraram `[NonSerialized]`, junto com
outros que tinham o mesmo defeito:

| campo | o que guardava na cena |
|---|---|
| `pendingTurnStartUpkeep` / `pendingTurnStartEconomy` | efeitos de início de turno "pendentes" congelados |
| `cachedConstructionIncomeSignature` / `Count` | cache de renda de uma partida antiga |
| `capturedBuildingHistory` | histórico de capturas de uma partida de teste (Batalha tinha uma Fábrica Leve) |
| `PlayerEntry.isRebelRuntime` | flag de runtime marcada como `1` |

Por isso os diffs de `Batalha.unity` e `Tela de Entrada.unity` **perdem** essas
linhas. Não é churn: é o estado de partida saindo da cena.

`MarkTurnStartEffectsAppliedForLoad` marca os efeitos como já aplicados, porque
o save foi feito com o turno em andamento. A apresentação do turno passou a
depender de quem joga:

- **Turno de IA, sem hot seat:** a cortina sai assim que a partida está pronta, e
  a IA retoma da fase em que estava. O autor gostou: *"quando é a AI ela mal
  espera a tela cortina e assim que fica pronta já retoma"*.
- **Turno humano:** aparece o botão "Iniciar Turno", a pedido do autor.

### O histórico de capturas era guardado por cor

`ImportCapturedBuildingHistory` agora tem como chave o **slot**. A cor só entra
para migrar um save antigo que não tinha slot, e um slot inválido é ignorado.
Também saiu `RegisterCapturedBuilding(TeamId…)`, código morto de 31 linhas. O
autor corrigiu o vocabulário no caminho: *"time 2? estamos falando de slots o
tempo todo"*.

### O cursor saltava no load

Depois do load, o cursor percorria o tabuleiro casa a casa até o QG. Era a
supressão de um único disparo, que o load não encerrava.
`EndLoadHeadQuarterCursorSuppression` agora fecha essa supressão no `finally` do
`LoadSlotAsync`. Confirmado em Play: depois do load, passar a vez leva o cursor
direto ao QG da IA.

**Ferramentas:** o Save Inspector mostra a linha "Regras (gameSetup)", e o
Inspector do `MatchController` mostra "Is Rebel (runtime)" consultando
`IsSlotRebel`, não um campo serializado.

---

## Frente 2 — menu, save e load no turno da IA

O autor descreveu o problema assim: *"se você abre o menu no meio do turno da AI
o jogo trava total… a ideia é que durante o AI vs AI ou human vs AI você pode
salvar na vez da AI pra poder fazer outra coisa"*.

### A pausa era uma flag que alguém tinha de lembrar de desligar

O menu chamava `SetPlayerPaused(true)`. A IA só voltava se o fechamento passasse
por `TryExitPlayerMenuStateToNeutral` **com o cursor exatamente em
PlayerMenu**. Qualquer outro caminho deixava a IA parada para sempre:

- o submenu de Opções;
- Salvar ou Carregar;
- um pedido de abertura que nunca abriu (o `OpenMenu` limpava o pedido mesmo
  quando `TryEnterPlayerMenuState` recusava).

Por isso o "Voltar ao jogo" das Opções funcionava e os outros caminhos não.

Agora a pausa é **derivada**. `AIController.PlayerPauseHolds` mantém a IA parada
enquanto o jogador está com o menu aberto, pedido, ou dentro do escopo do menu.
Saindo por qualquer caminho, ela solta sozinha. É o mesmo raciocínio do "o mundo
só recalcula no Neutral": estado derivado não esquece de se desligar.

### Um deadlock de verdade no shopping da IA

Quando uma compra da IA falhava, ela pausava **antes** de fechar o próprio menu
de compras. O menu do jogador só abre no Neutral, e o cursor estava no menu de
compras. O jogador esperava a IA e a IA esperava o jogador. Agora a IA fecha o
shopping antes da pausa (`AIController.Phase3.cs`).

Como rede de segurança, se o pedido de menu não consegue abrir em 3 s com a IA
de fato parada, ele cai, a tela mostra "Menu indisponível neste momento" e a IA
segue. O relógio reinicia enquanto ainda há batch ou animação da IA rodando.

### A tela de salvar não aparecia

No turno da IA, o panel_helper e o panel_dialog se escondem por inteiro: tudo o
que aparece ali é tratado como apresentação de ação da IA. Só que Salvar,
Carregar e "Sair da partida" são telas do **jogador**.

Precisei de duas rodadas para acertar. Na primeira, liberei Saving/Loading; o
autor voltou com "o sair da partida não aparece opções também". Era o mesmo
defeito, porque a lista de estados existia em três lugares, cada um com estados
diferentes. A segunda correção criou uma fonte única,
`TurnStateManager.IsInPlayerMenuScope` (PlayerMenu, Saving, Loading). Quem usa:

- panel_helper e panel_dialog;
- a pausa da IA;
- o bloqueio de persistência do `SaveGameManager`.

---

## Frente 3 — poder de captura é o HP

O autor perguntou: *"se o bazooka tinha 10 de HP, por que não causou 5 de
captura? eu configurei a cidade pra ser 0.5"*.

`PodeCapturarSensor.GetCapturePower` cortava pela metade o poder de quem tinha
`CapturadorCombatente` como papel primário. Isso pegava o Bazooka e a
metralhadora, que capturam pela skill Capturador Alternativo. A regra de quanto
uma captura rende já mora no **alvo**: a eficiência que a construção dá a cada skill, que no
exemplo do autor era 0.5. Uma segunda redução no capturador aplicava o corte duas
vezes, e num lugar que o autor não via. Agora o poder é o HP, e o prédio decide.

É a lógica de "a skill é uma chave, não um poder", aplicada ao papel.

---

## Frente 4 — campanha: quadrante em desenvolvimento e nomes que dizem a verdade

- **`QuadranteData.emDesenvolvimento`.** É uma flag no Map Helper
  (`DrawQuadranteRow`, com Undo). O cabeçalho mostra "· EM DEV", e a seleção de
  campanha recusa lançar o quadrante (som de erro + "EM DESENVOLVIMENTO").
- **"Exige irmãos" era o nome errado.** O campo marca o mapa final da campanha
  (*"o correto é 'mapa fim de campanha'"*). O `[InspectorName]` passou a ser
  "Mapa final da campanha", "Campanha final do bloco" e "Bloco final do mundo",
  conforme o nível. O nome do campo no código não mudou, então nenhum asset
  precisou ser migrado. O Map Helper avisa que ele **ainda não tem efeito em
  jogo**.
- **Cor do jogador na tela da campanha.** O painel aparecia verde com o jogador
  no azul. Fora da partida não existe "jogador da vez", e o `ActiveTeam` da cena
  era o que tinha sobrado. `ResolveActiveTeamColor` agora pergunta quem é o
  humano local. Também aqui precisei de duas rodadas: a linha de detalhes da
  confirmação usava uma cor fixa (`FooterLabelIdleColor`) e continuou verde
  depois da primeira correção.

---

## Frente 5 — shopping sem dinheiro

- **O cursor sumia sobre um item indisponível.** O cinza apagado sobrescrevia o
  foco. Agora o item focado mostra o fundo do foco escurecido pela metade, com o
  texto em cinza claro: dá para ver onde o cursor está, mesmo sem poder comprar.
- **O `error.mp3` tocava duas vezes, sobreposto.** A compra tocava o próprio
  erro e o `HandleConfirmWhileShoppingAndServices` devolvia `ActionSfx.Error`,
  que tocava de novo. Agora cada caminho de falha toca uma vez só e o handler
  devolve `None`.

---

## Autoria e churn

- **`Mundo Fixture.asset` + `Fixture.unity`:** autoria de mapa feita pelo autor
  (QG movido, rally marcado). Vai num commit próprio; eu não acompanhei esse
  trabalho e não o descrevo além disso.
- **`Batalha.unity`:** além dos campos que saíram da cena (Frente 1), o autor
  desligou o debug para publicar: Start On Pause, logs e HUD da IA, atalhos de
  debug.
- **Atlas de fonte** (VT323, LiberationSans Fallback): churn do Editor.
- **`PerformanceTestRun*.json`:** gerados pelo pacote de teste de desempenho no
  build. Foram para o `.gitignore`.

---

## O que não terminou

- **Hot seat humano × humano não existe no MVP.** O contrato já se configura,
  mas o jogo publicado é humano × IA. Os itens de hot seat do checklist ficam
  para depois.
- **Os tutoriais estão desconectados do menu principal**, mesmo estando no build
  profile. É uma decisão temporária do autor.
- **O panel_dialog no menu do turno da IA** mostra o que está sob o cursor, e o
  cursor fica onde a IA estava agindo. Isso respeita a névoa, mas é uma decisão
  em aberto: talvez o autor prefira que ali não apareça nada.
- **A pausa derivada não foi provada inteira em Play.** O autor abriu o menu no
  turno da IA e entrou em Saving (o log mostra `PlayerMenu -> Saving`), e foi
  assim que as telas escondidas apareceram. Ainda não foi confirmado que a IA
  retoma depois de sair com ESC, nem que um load feito no turno da IA continua do
  ponto certo. O deadlock do shopping e a rede de 3 s **só compilaram**:
  provocar uma compra da IA que falha exige montar o caso.
- **"Mapa final da campanha"** continua sem consumidor em jogo.
