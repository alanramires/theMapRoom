# v8.5.0 — o laço fecha, e o dono deixa de ser uma cor

A `v8.4.1` deixou quatro telas boas que não formavam um ciclo. Você entrava pelo
menu, escolhia o quadrante, lutava — e terminava olhando a tela de vitória, sem
volta. Este é o dia em que virou um jogo.

E o caminho até lá encontrou duas vezes o **mesmo defeito de classe**, em níveis
diferentes do jogo. Ele é o fio deste relatório.

---

## A descoberta que organiza o resto

> **Cor não é identidade. A cor é uma fantasia que o slot veste por uma partida.**

As duas cores são escolhidas no menu, uma vez por partida. Tudo que atravessa a
fronteira entre autoria e partida — ou entre uma partida e a seguinte — tem de
ser endereçado por **slot**, e a cor tem de ser resolvida só na hora de pintar,
por `MatchController.GetTeamIdForSlot`.

A regra já estava escrita, no briefing da cena de campanha:

> *Cor de time nunca sai do slot direto.*

O que este dia mostrou é que ela vale muito além do lugar onde foi escrita. Ela
foi violada em dois pontos independentes, e os dois sintomas foram silenciosos.

---

## Frente 1 — o tabuleiro nascia com a cor da autoria

**O sintoma, relatado pelo autor:** escolheu Amarelo contra Vermelho, e a
partida abriu *"azul com cursor amarelo e amarelo lá embaixo"*.

Nada disso era aleatório. O fixture foi autorado com o slot 0 em Azul
(`teamId: 2`) e o slot 1 em Amarelo (`teamId: 3`). E o `QuadranteController`
plantava assim:

```csharp
GameObject go = spawner.SpawnAtCell(c.constructionId, c.teamId, cell);
```

`c.teamId` é a cor **da cena de autoria**. Logo depois vinha
`manager.SetSlotIndex(c.slotIndex)` — que só escreve o campo e **não mexe na
cor**. Slot certo, cor errada, e nenhum erro no Console.

Então o cursor e o HUD mostravam Amarelo (vindos do `MatchController`, que
recebeu a escolha do menu), enquanto os prédios mostravam Azul e Amarelo (as
cores da autoria). O amarelo "lá embaixo" não era o prédio do jogador: era o do
inimigo, com a cor velha.

### O segundo defeito, empilhado sob o primeiro

Trocar `c.teamId` por `GetTeamIdForSlot` **não teria consertado nada** — teria
trocado uma cor errada por outra.

```text
QuadranteController   [DefaultExecutionOrder(-9000)]   pinta o tabuleiro
MatchController       (sem ordem declarada = 0)        aplica o PartidaConfig
```

Na hora em que o quadrante pinta, a lista de jogadores ainda é a **serializada
na cena Batalha**. A configuração do menu só chega no `Awake` do
`MatchController`, ordem 0 — depois.

É a armadilha do projeto espelhada. A original é *consultar antes da pintura
terminar*; esta é **pintar antes da configuração chegar**.

O conserto foi um ponto único e público:

```csharp
public void EnsurePartidaConfigApplied()
{
    if (!PartidaConfig.HasPending) return;
    PartidaConfig.Apply(this);
    PartidaConfig.Clear();
}
```

Idempotente de graça — `Clear()` derruba o `HasPending`, então a segunda chamada
não entra. O `Awake` chama, e quem pinta antes dele chama primeiro. Nenhuma
ordem de execução foi movida: `ApplyTeamFlipSettingsToSceneObjects()` continua no
fim do `Awake` do `MatchController`, depois do tabuleiro existir, então o flip
das construções não regrediu.

No spawn, o dono passou a sair do slot, e o `SetSlotIndex` deu lugar ao
`SetOwnerSlot` — o mesmo caminho que a **captura** usa, que deriva o time do slot
e refresca o visual com `force: true`:

```csharp
TeamId dono = c.slotIndex >= 0 && match != null
    ? match.GetTeamIdForSlot(c.slotIndex)
    : c.teamId;
```

`slotIndex >= 0` segue o slot. `-1` é conteúdo de **time fixo** e mantém a cor
assada — exatamente a regra que já estava escrita no recolorir do tutorial
(*"só tem efeito visível em cenas cujas unidades/construções usam slotIndex
(>= 0); conteúdo com time fixo (slotIndex -1) não acompanha"*). Ela dá de graça o
caso da facção sem QG, e toda cidade neutra do fixture continua nascendo neutra.

O `teamId` do bake foi rebaixado a **registro** na documentação do
`ConstrucaoAssada`. O comentário antigo — *"guarda o dono junto porque a
construção já nasce da cor com que foi pintada"* — era a justificativa do bug.

**A frente paralela pegou o conserto no mesmo dia** e aplicou a mesma regra ao
mosaico da cena Campanha, que tinha o defeito idêntico ao desenhar as construções
assadas. As duas frentes estão coerentes.

---

## Frente 2 — o dono do quadrante era uma cor

Achado durante a avaliação de MVP, e é o **mesmo defeito um nível acima**.

`CampaignProgressStore.RecordOwner` gravava `TeamId`:

```csharp
public int ownerTeamId = (int)TeamId.Neutral;
```

Jogue de Amarelo hoje e de Vermelho amanhã: o quadrante que você conquistou fica
pintado de amarelo, a cor de ninguém. E a pergunta que o arquivo de progresso
existe para responder — *"fui EU que tomei este?"* — deixa de ter resposta,
porque o arquivo não distingue jogador de IA.

Agora grava `ownerSlotIndex`, e a API fala `PlayerSlotId`. O evento de conclusão
passou a carregar o slot vencedor:

```csharp
public static event Action<PlayerSlotId, TeamId, TeamId, VictoryReason, int> OnMatchConcluded;
```

Conferi um por um os cinco caminhos que chegam em
`HandleVictoryAestheticPresentation`: **todos** escrevem `victoryWinnerSlotIndex`
antes da chamada. O único caso de slot inválido é rendição sem oponente vivo para
coroar — e aí não há dono novo mesmo, mas a partida acabou e a volta é armada
assim mesmo.

Na Campanha, o tint resolve slot → cor contra as cores **desta** sessão. Quem
venceu de Amarelo ontem e joga de Vermelho hoje vê o próprio quadrante em
vermelho. É o certo: a cor responde *"quem sou eu nesta partida"*, não *"que cor
eu era naquela"*.

---

## Frente 3 — a volta

Conferi os **seis** `SceneManager.LoadScene` do projeto inteiro. Nenhum carregava
`"Campanha"` além do wizard do menu. A única saída da Batalha era
`BattleMapMenuRootController` → `"Tela de Entrada"`, e manual: ESC → confirmar
sair. Depois do `Panel_vitoria` aparecer não acontecia mais nada.

A volta mora no `QuadranteController`, e a razão importa: a pergunta que decide se
há volta é *"esta partida veio da campanha?"* — e quem sabe isso é quem recebeu o
endereço (`recordsCampaignResult`). Partida aberta direto na Batalha para testar
um quadrante continua terminando na tela de vitória, como sempre terminou.

Vitória ou derrota, o Enter devolve ao mapa. Exige tecla **nova**
(`frameDaConclusao` + `IsSubmitHeldNow`), senão o mesmo Enter que confirmou a
última ação sairia da tela no frame em que ela aparece — o padrão que a
`CampaignSelectionController` já usava na ida.

### O que quase estragou a volta em silêncio

`PartidaConfig` é de **consumo único**. O `Awake` da Batalha já aplicou e limpou.

Voltar sem republicar faria a cena Campanha nascer com as cores **serializadas
nela** — e o quadrante recém-conquistado apareceria pintado na cor de outra
pessoa, porque o tint resolveria o slot contra a lista errada. O mesmo defeito de
classe, uma terceira vez, agora na direção contrária.

Então a volta reexporta o estado da partida e publica de novo, junto com a
dificuldade. É a travessia da ida ao contrário.

---

## Frente 4 — espaço para tropa inicial

Não existia `UnidadeAssada`. Os quadrantes nasciam sem ninguém em campo, e é
assim que os quatro do fixture continuam jogando — os dois lados começam
comprando, com a renda das construções (`capturedIncoming` de 1000 a 3000 por
prédio, que o bake já carregava).

O que faltava era **onde pintar**. Agora existe `QuadranteData.bakedUnidades`, a
bancada assa (`BakeUnidades`) e o `QuadranteController` planta (`BuildUnidades`),
depois das construções — tropa inicial em cima do próprio QG é desenho legítimo,
e o spawner recusa célula ocupada.

A regra é a das construções: **se está no retângulo, vem como está**. Pinta na
cena de autoria, assa, e está lá. Sem configuração à parte.

O dono sai do slot por `SpawnAtCellForSlot`, que resolve o time visual do slot e
aplica o `slotIndex` no mesmo caminho — o único em que cor e slot não podem
divergir.

**Nenhum campo de estado foi adicionado** (HP, combustível, elite). O projeto tem
a cicatriz do `fieldEntries`: campo sem leitor não é inofensivo. Quando houver
tropa inicial machucada, o campo entra junto com quem o lê.

---

## Frente 5 — autoria de rota (autor)

`RoadRoutePainterWindow` ganhou **"Remove Start Point"**. Até agora só dava para
aparar rota pelo fim; uma rota com o começo errado tinha de ser refeita inteira.
Os rótulos de Undo foram separados (`Remove Road Route Start Point` /
`Last Point`), e o layout dos botões foi corrigido — `Delete Route` estava dentro
do mesmo `BeginHorizontal` dos outros.

A Euro Road foi aparada com a ferramenta nova e o mundo foi reassado. O fixture
passou de **7 para 6 trechos** de rota assados.

---

## Frente 6 — o menu espera o som (frente paralela)

`PanelMenu.StartConfiguredNewGame` virou corrotina: toca o SFX de confirmação e
**espera ele terminar** antes de carregar a cena, com
`WaitForSecondsRealtime` — em tempo real, porque o menu pode estar com o tempo de
jogo pausado. `CursorController.GetDoneSfxDuration()` nasceu para isso, e devolve
a duração já corrigida pelo pitch do `AudioSource`.

Um `startingNewGame` guarda contra duplo disparo durante a espera, e trava
navegação e cancelamento do wizard enquanto ele acontece. A última opção de cada
passo do wizard passou a ser CANCELAR/VOLTAR tanto no mouse quanto no teclado.

---

## Frente 7 — o AudioManager virou prefab (frente paralela)

O `AudioManager` foi extraído para `Assets/Prefab/Managers/AudioManager.prefab` e
instanciado em **Tela de Entrada, Campanha e Batalha**. É o que explica as três
cenas encolherem centenas de linhas de uma vez.

`PanelTurnController` ganhou uma guarda relacionada: quando
`presentationTextOverride` está preenchido, o prefab está sendo reusado **fora de
uma partida** (o *"Selecione o mapa"* da cena Campanha), e o subpainel de
estatísticas fica oculto — ele não representa nada confirmado ali, mesmo com o
contrato de slots vivo entre as cenas.

---

## O que NÃO terminou

### Nada disto foi compilado

Não há build por linha de comando neste projeto. **Todo o código desta versão foi
escrito contra as APIs lidas.** Confira o Console antes de acreditar em qualquer
coisa acima.

### O `0b` foi revisado e a conta mudou

O resumo dizia *"`sceneLoaded` nos 4 managers"*. Revisei os quatro, e **eles não
são o mesmo problema**:

| manager | o que carrega | veredito |
|---|---|---|
| `AITacticalAnalyzer` | `operationsBySlot` — necessidades **por slot** | precisa limpar. Estado de partida indexado por slot, e o slot 0 da próxima é outra pessoa |
| `ObjectiveManager` | `plans` | precisa limpar. E achei quem limpa: **só o `RestoreSaveData`**. Carregar save limpa; começar partida nova, não |
| `HexCohabitationVisualManager` | `cachedTurnStateManager`, `cachedMatchController` estáticos | precisa limpar, mas é outro bug: são referências a objetos **da cena anterior**, já destruídos |
| `AIShoppingPlanner` | quase tudo ali é *tunable* serializado | **provavelmente não deve limpar nada** — configuração *deve* atravessar cenas, é o motivo de ele ser global |

Enfiar um `Clear()` no quarto apagaria configuração, não contaminação. Falta ir
campo a campo no `AIShoppingPlanner` separando tunável de estado **antes** de
escrever qualquer hook.

### A partida ainda pune quem não compra no turno 1

A `Batalha.unity` serializa `startMoney: 0` e `actualMoney: 0` nos dois slots, e
tem `allowDefeatForZeroUnits: 1`. O teste de derrota por zero unidades roda a
partir do **turno 2**. A renda chega no início do turno 1 e dá para comprar — mas
quem não comprar perde no turno 2 sem entender por quê.

Não é bug: é ausência de decisão. Três saídas honestas, e nenhuma foi tomada —
assar unidades iniciais (agora possível), dar caixa inicial ao bake, ou dar
carência à derrota por zero unidades.

### O silêncio entre menu e campanha

O `MatchMusicAudioManager` **continua sem `DontDestroyOnLoad`**: a música da cena
que sai morre com ela, e a da Campanha só nasce no `Start()` do manager novo. E o
`BuildWorldMosaic()` segue no `Awake` da `CampaignSelectionController`, em ordem
`-10000` — todo `Awake` roda antes de qualquer `Start`, então o mosaico dos quatro
quadrantes é construído com o jogo travado e a música é a última da fila.

Virar prefab não resolveu isso; resolveu o compartilhamento de configuração. As
duas causas seguem inteiras.

### O `lastTurn` continua sendo *último*, não *melhor*

A divergência nº 2 que o resumo da v8.4.1 apontou não foi tocada. `lastTurn` é
sobrescrito sempre e mora junto do dono — perder o quadrante apaga que você o
tomou em 11 turnos.

---

## Correções ao resumo anterior

Duas coisas que a `v8.4.1` afirmava e não valem mais:

- **"As cenas de execução seguem fora do Build Settings"** — falso. `Tela de
  Entrada`, `Campanha` e `Batalha` estão as três lá, e habilitadas.
- **"`PartidaConfig.SetQuadrante` ainda não existe"** (o briefing) — existe, com
  consumo próprio, separado do `Clear()` justamente porque o `QuadranteController`
  pinta antes de o `MatchController` consumir.
