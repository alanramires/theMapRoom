# Plano — MVP jogável da campanha

Escrito em 2026-09-07, sobre a `v8.5.0`. Este documento é **plano**: nada aqui
está implementado, e plano pedido não autoriza implementação.

O tronco continua sendo [`plano_campanha.md`](plano_campanha.md). Este recorta
dele **uma linha só**, definida pelo autor:

```text
Tela de Entrada → BLOCO → CAMPANHA → mapa jogável → campanha 1/4
                → mapa jogável → campanha vencida → Tela de Entrada
```

**Fora do escopo deste plano:** o `MenuRoot` e o `Panel_campanha` (escapada, save,
load, sair). O autor está fazendo isso em outra frente, e este documento não
encosta.

---

## 1. O fluxo, e o que existe em cada passo

| passo | estado | onde |
|---|---|---|
| Tela de Entrada — contrato (slot 0, slot 1, dificuldade, preset) | ✅ | `PanelMenu` |
| **→ BLOCO / CAMPANHA** | ⛔ **não são telas — são o portão** | o jogador navega por quadrante; a hierarquia decide se ele entra. `destravadoPor` e `exigeIrmaos` existem nos três níveis com **zero leitores** |
| → mapa jogável | ✅ | `QuadranteController` monta o quadrante |
| → volta pra campanha | ✅ desde a `v8.5.0` | Enter na tela de vitória |
| **→ campanha 1/4** | ⛔ **não existe contador** | — |
| **→ campanha vencida** | ⛔ **não existe condição nem transição** | — |
| **→ Tela de Entrada** | ⛔ | — |

Quatro buracos. Nenhum deles é grande; o segundo é o que decide a forma dos
outros.

E há três coisas que quebram o fluxo **em qualquer passo**, independentes da
hierarquia — elas vêm primeiro, nas §2 a §4.

---

## 2. Etapa 0 — compilar e jogar o laço uma vez

**Bloqueia tudo.** Não faz sentido construir em cima de código que ninguém sabe
se compila.

Toda a `v8.5.0` foi escrita contra as APIs lidas. As mudanças de assinatura são
as que mais podem morder:

```text
MatchController.OnMatchConcluded     ganhou PlayerSlotId no primeiro parâmetro
CampaignProgressStore.TryGetOwner    devolve PlayerSlotId, não TeamId
CampaignProgressStore.RecordOwner    recebe PlayerSlotId, não TeamId
```

**Verificação:** menu → Amarelo vs Vermelho → quadrante → render → Enter → o mapa
volta com aquele quadrante em vermelho. Inclusive a derrota, que é o caminho que
ninguém testa.

---
## 3. Etapa 1 — o quadrante diz como cada lado começa

⚠️ *Reescrito em 2026-09-07. A versão anterior apresentava três saídas
excludentes — tropa inicial **ou** caixa inicial **ou** carência na derrota. O
autor corrigiu: não é escolha.*

> **O quadrante é configurável.** Ele pode ter unidades iniciais por slot já em
> campo **e** dizer que o slot 0 começa com 100k no bolso.

### As três coisas, e de onde cada uma vem

Fechado com o autor em 2026-09-07. São origens diferentes, e confundi-las é o
único jeito de errar isto:

| o quê | de onde vem | campo novo? |
|---|---|---|
| **unidades no turno 1** | do **bake** — o que está pintado no retângulo | ✅ já existe (`bakedUnidades`, `v8.5.0`) |
| **renda por rodada** | dos **prédios controlados** — soma do `capturedIncoming` de cada um | ❌ **nenhum.** Já funciona |
| **renda inicial** | do **quadrante**, autoral. Quase sempre 0; alguns mapas dão um extra | ⬜ é o único que falta |

> Quando o jogo começa é **0 + prédios controlados** — e alguns mapas somam um
> caixa inicial por cima.

⚠️ **Não criar campo de renda por rodada no quadrante.** Ele colidiria de frente:
`RecalculateIncomePerTurnForAllPlayers` faz *atribuição*
(`entry.incomePerTurn = soma dos prédios`), não soma, e roda no `Awake` **e** a
cada início de turno. Um valor declarado seria apagado no primeiro recálculo, sem
erro — e se não fosse, capturar cidade deixaria de valer alguma coisa.

A renda por rodada **já vem do quadrante**: pelos prédios que ele assa. Só que
derivada, não declarada — que é a forma certa, porque ela muda quando o
território muda.

Isso é o mesmo princípio que já governa o resto do projeto, um nível abaixo:
`ConstrucaoAssada` carrega o `siteRuntime` porque *"a cena de autoria é a lei"* —
uma fábrica leve autorada sem radar móvel tem de nascer sem radar móvel. A
economia inicial é a mesma ideia para o mapa inteiro: **Feijão Torto é um mapa
que começa assim.**

E isso dissolve o problema em vez de escondê-lo. O sintoma era:

```text
Batalha.unity   startMoney: 0   actualMoney: 0   allowDefeatForZeroUnits: 1
                derrota por zero unidades testada a partir do turno 2
```

Quem não comprasse no turno 1 perdia no turno 2. Com o quadrante dizendo com o
que cada lado começa, isso deixa de ser possível por autoria — **sem toggle, sem
carência, sem caso especial.** A carência sai da mesa: ela escondia o sintoma.

### Onde o dinheiro mora — e é a parte que se erra

`QuadranteData` já tem **duas seções**, e a diferença entre elas é a decisão:

```text
autoral            quadranteId · displayName · origin · width/height
                   destravadoPor · exigeIrmaos · idSerial
                   ← editado à mão, sobrevive ao bake

[Header("Assado — artefato, nao editar a mao")]
                   bakedTiles · bakedConstrucoes · bakedCamadas
                   bakedRotas · bakedUnidades
                   ← regerado pelo botão, nunca editado
```

**A economia inicial é autoral, não assada.** O motivo é direto: dinheiro não é
espacial. Tropa inicial vem do bake porque ela *está no retângulo* — a regra "se
está no retângulo, vem como está" se aplica. Cem mil no bolso não está em lugar
nenhum da cena; é uma afirmação sobre a partida.

⚠️ Pôr o campo sob o header do assado seria mentira, e a mentira cobraria: mais
cedo ou mais tarde alguém limpa a seção "artefato" inteira e o número do autor
some junto, sem erro.

Forma sugerida, ao lado do retângulo:

```csharp
[Tooltip("Caixa inicial por slot. Slot ausente = 0. É autoral, não sai do bake.")]
public List<EconomiaInicialSlot> economiaInicial = new List<EconomiaInicialSlot>();
```

Uma entrada `{ slotIndex, startMoney }` por slot, pelo mesmo motivo que
`ConstrucaoAssada` guarda `slotIndex` e não cor: **o dono é o slot**, e as cores
são escolhidas no menu.

### O leitor já existe, e está certo

Este é o ponto que faz a etapa ser pequena. Conferido:

```csharp
// MatchController.ApplyEconomyAtTurnStartForActiveTeam
int credit = Mathf.Max(0, entry.incomePerTurn);
if (!entry.startMoneyApplied)
{
    credit += Mathf.Max(0, entry.startMoney);
    entry.startMoneyApplied = true;
}
```

O `startMoney` é creditado **uma vez**, no primeiro início de turno daquele slot,
junto com a renda. E o `PartidaConfig.Apply` já zera `startMoneyApplied` para
todos os slots ao aplicar o contrato — então um valor novo é sempre concedido, e
nunca duas vezes.

Ou seja: o campo não precisa de mecanismo, precisa de **quem o escreva nos
`players` antes do turno 1**. E esse alguém já está no lugar certo, na hora certa:
o `QuadranteController` roda em `-9000` e já chama
`EnsurePartidaConfigApplied()` antes de pintar.

O que falta de API: existe `TrySetActualMoney(PlayerSlotId, int)`, mas **não**
existe equivalente para `startMoney` — hoje ele só entra por
`ImportPlayersState`. Um setter pequeno, ou a passagem pelo import que o
`PartidaConfig.Apply` já faz.

⚠️ **Cuidado com a regra que já está escrita.** O `PartidaConfig.Apply` preserva
de propósito a economia da cena-base: *"a economia da cena-base pertence ao slot
lógico, não à cor escolhida"*. A economia do quadrante tem de ser aplicada
**depois** disso, senão o `Apply` a sobrescreve com os zeros da `Batalha.unity`.

### Pronto quando

Um quadrante autorado com `100000` no slot 0 abre com cem mil no painel de
dinheiro no turno 1, e um quadrante autorado sem nada continua abrindo em zero.
E uma partida ignorada por dois turnos não termina sozinha — porque o autor deu
tropa, dinheiro, ou os dois.

---

## 4. Etapa 2 — o `0b`, que a repetição torna obrigatório

O fluxo é **jogar quadrantes em sequência**. Três managers globais carregam
estado de uma partida para a seguinte:

| manager | o que atravessa |
|---|---|
| `ObjectiveManager` | `plans`. Hoje quem limpa é **só o `RestoreSaveData`** — carregar save limpa, começar partida nova não |
| `AITacticalAnalyzer` | `operationsBySlot` — estado indexado por slot, e o slot 0 da próxima é outra pessoa |
| `HexCohabitationVisualManager` | `cachedTurnStateManager`, `cachedMatchController` — referências a objetos da cena anterior, já destruídos |

**Começar pelo `AIShoppingPlanner`, que é o que provavelmente NÃO muda.**
Conferido: é tudo `public` sob `[Header]` — Economia Exército, Defesa de Base,
Intel de Jogadas, Logística, Economia Aeronáutica. Configuração, e configuração
**deve** atravessar cenas. Um `Clear()` ali apagaria tunables e a IA jogaria
zerada, sem um erro. Confirmar que ele não guarda estado é mais barato que
descobrir depois que guardava.

**Verificação:** menu → mapa A → turno 5 → menu → mapa B. No turno 1 do B, o
plano tem de nascer **vazio**.

---
## 5. Etapa 3 — o portão hierárquico

⚠️ *Reescrito em 2026-09-07. A versão anterior propunha escopar a cena a UMA
campanha, com passos de seleção de bloco e de campanha. Errado: o autor
esclareceu que **o jogador sempre navega por quadrante**.*

> O jogador navega o mapa por quadrante. Se ele chegar num quadrante de uma
> campanha não liberada, não entra. Se chegar num de um bloco não liberado,
> também não. **É hierarquia.**

Então o mosaico achatado de hoje — todo quadrante de toda campanha de todo bloco
na mesma tela — **é o desenho certo**, não um defeito. Não há tela de escolher
bloco nem de escolher campanha: `entrada → bloco → campanha → mapa` descreve a
**hierarquia que o jogador atravessa**, não três telas.

O que falta não é escopo. É o **portão**.

### Duas recursões, em direções opostas

O `INoDoMapa` já documenta uma delas:

> *"Concluído" é recursivo, e é isso que faz um campo só resolver os três níveis:
> quadrante concluído = venci ele; campanha concluída = todos os quadrantes dela
> concluídos; bloco concluído = todas as campanhas dele concluídas.*

A outra é a que o autor acabou de nomear, e **não está escrita em lugar nenhum**:

```text
CONCLUÍDO  sobe    quadrante → campanha → bloco     agregação sobre os filhos
LIBERADO   desce   bloco → campanha → quadrante     conjunção com o pai
```

Escritas como código, são duas funções e acabou:

```csharp
Concluido(nó)  =  nó é quadrante ? venciEle(nó)
                                 : filhos.All(Concluido)

Liberado(nó)   =  (pai == null || Liberado(pai))
                  && nó.DestravadoPor.All(Concluido)
                  && (!nó.ExigeIrmaos || irmãos.All(Concluido))
```

O autor descreveu isso como *"quase orientação a objetos"*, e é exatamente o que
o `INoDoMapa` existe para permitir: **uma implementação para os três níveis**, não
três quase iguais.

⚠️ **Se essas funções acabarem escritas duas vezes** — uma para campanha, outra
para bloco — passou pelo lugar errado. É o mesmo aviso da §6, e vale mais aqui,
porque aqui há três níveis e a tentação de tratar quadrante como caso especial é
grande.

### Os campos existem e ninguém os lê

Conferido: `destravadoPor` e `exigeIrmaos` estão nos **três** níveis, expostos por
`INoDoMapa`, e têm **zero leitores** fora das classes de dado. Estão prontos há
versões, esperando o avaliador.

*Este plano os tinha listado como "fora de escopo — ligar destrave é feature, não
MVP". Estava errado: sem o portão, `bloco` e `campanha` não existem como conceito
para o jogador, e o fluxo que o autor pediu não acontece.*

### O que o jogador vê ao esbarrar num portão fechado

Um quadrante bloqueado precisa dizer **por quê**, senão vira um bug aos olhos de
quem joga. A informação já existe no dado: `destravadoPor` tem os ids, e a cena já
sabe resolver id → nome pelo `MundoData`.

A regra de cena que o briefing já escreveu vale aqui:

> *`destravadoPor` e `exigeIrmaos` estão nos dados e ninguém os avalia ainda. Pode
> mostrar como informação ("requer X"), mas não bloqueie o clique por conta
> própria.*

Agora o avaliador passa a existir, e a segunda metade da frase se inverte:
bloquear vira o certo. A primeira metade continua valendo — **mostre o requisito**.

⚠️ **O `destravadoPor` guarda texto, e devia guardar `idSerial`.** O próprio
`INoDoMapa` avisa: renomear um nó faz o `TryGet` não achar, e aí *"o destrave
simplesmente não destrava. Nenhum erro, nenhum log"*. Enquanto ninguém avaliava,
a dívida dormia. **Ligar o portão é o gatilho dela** — e conserta-se barato agora,
com um mundo de quatro quadrantes, ou caro depois.

### No MVP o portão não bloqueia nada

Um bloco, uma campanha, quatro quadrantes, todos com `destravadoPor` vazio. O
portão vai responder "liberado" para tudo, e é o correto.

Isso não o torna inútil de escrever agora — torna-o **testável sem risco**: dá
para pôr um `destravadoPor` de mentira num quadrante, ver a porta fechar, e tirar.

**Pronto quando:** um quadrante com `destravadoPor` apontando para um irmão não
concluído recusa a entrada e diz o que falta; e o mesmo acontece quando o
bloqueio vem da campanha ou do bloco acima, sem código novo para cada nível.

---

## 6. Etapa 4 — o registro, o contador e o fim

### O registro do quadrante é um retrato, não um recorde

Definido pelo autor em 2026-09-07:

```text
venci em 33      →  Feijão Torto · vencedor slot 0 · turno 33
rejoguei, 45     →  Feijão Torto · vencedor slot 0 · turno 45     (piorou, e tudo bem)
rejoguei, perdi  →  Feijão Torto · vencedor slot 1 · turno 18
```

Sem histórico, sem "melhor de". Uma linha por quadrante, sobrescrita ao fim de
cada partida. Histórico de tentativas fica para quando fizer falta.

**Isto fecha a divergência nº 2 da `v8.4.1` no sentido oposto ao que se supunha.**
Aquele relatório listava *"`lastTurn` é último, não melhor"* como defeito. **Não
é defeito, é o desenho** — e o `CampaignProgressStore` de hoje já está certo
nesse ponto. Uma dívida sai da lista sem uma linha de código.

Estatísticas ficam de fora por enquanto: `SlotMatchStats` já existe com 40+
campos, já é coletado e já é serializável, mas gravar campo que nenhuma tela lê é
a cicatriz do `fieldEntries`. Entra quando existir a tela.

### O progresso é derivado, nos três níveis

```text
1 BLOCO      tem n campanhas
1 CAMPANHA   tem n quadrantes
1 QUADRANTE  é jogado por vez

campanha vencida  =  todo quadrante dela tem vencedor == meu slot
bloco vencido     =  toda campanha dele está vencida
jogo zerado       =  todo bloco está vencido
```

**Nada disso é gravado além do quadrante.** Um campo `campanhaConcluida` seria um
segundo lugar da verdade, e divergiria na primeira vez que alguém rejogasse e
perdesse.

O contador `1/4` é a contagem de quadrantes cujo **último** vencedor é você. Como
tudo deriva, ele anda para os dois lados: dá para estar em 4/4 e voltar para 3/4,
e não existe ponto sem retorno.

⚠️ **A regra é a mesma nos três níveis e deve ser escrita uma vez**, sobre
`INoDoMapa` — a interface existe exatamente para código que serve aos três. Se
ela acabar escrita duas vezes, uma para campanha e outra para bloco, passou pelo
lugar errado.

*O tronco previa um carimbo de conclusão que nunca se apagava. Ele saiu — era a
única peça do modelo que não derivava. Registrado lá, com a data.*

### O fim, e a volta para a Tela de Entrada

O fluxo termina em `campanha vencida → Tela de Entrada`. **Decidido pelo autor em
2026-09-07: a campanha vencida OFERECE a saída, não expulsa.**

Como rejogar continua permitido e o 4/4 pode desandar, "vencida" não é uma porta
que fecha — é um estado que a tela reconhece. A saída é **celebração**, não
bloqueio: mostra que acabou, e o jogador vai embora se quiser.

Na prática: o reconhecimento aparece, e voltar à Tela de Entrada é uma escolha —
o mesmo Enter que já traz o jogador da batalha serve, sem inventar entrada nova.

⚠️ **Sem estado terminal, sem `if`.** "Campanha vencida" continua sendo a
contagem derivada, avaliada toda vez que a tela pergunta. Se um dia a saída
precisar acontecer *uma vez só* — "já mostrei a celebração, não mostra de novo" —
isso é um dado gravado, e é o carimbo que saiu de propósito na §6. Pense duas
vezes antes de trazê-lo de volta por causa de uma animação.

**Pronto quando:** o contador sobe ao tomar um quadrante, desce ao perder um que
era seu, o registro mostra a última partida mesmo quando pior, e 4/4 **oferece** a
volta à Tela de Entrada sem forçá-la.

---

## 7. O que fica de fora

| fora | por quê |
|---|---|
| **`MenuRoot` / `Panel_campanha`** | **frente do autor.** Escapada, save, load e sair não são deste plano |
| **o silêncio entre as cenas** | duas causas confirmadas e de pé (`MatchMusicAudioManager` sem `DontDestroyOnLoad`; `BuildWorldMosaic` no `Awake` em ordem `-10000`). Não quebra o fluxo, mas é a primeira coisa que se nota |
| **medir em escala** | a bancada rodou sobre 1800 células |
| **histórico de tentativas** | nomeado pelo autor como futuro |

### A dívida que o fluxo não usa, mas o save vai encostar

**Salvar dentro de uma batalha de campanha está quebrado, e em silêncio.**
`SaveGameManager` tem **zero** menções a quadrante ou campanha — grava o nome da
cena. Carregar um save de batalha de campanha faria:

```text
1. carrega "Batalha"
2. QuadranteController não acha endereço no PartidaConfig
3. cai nos campos do Inspector — hoje A_IA / A_IA_Q1
4. pinta o Q1, seja qual for o quadrante de onde o save veio
5. o save restaura as peças em cima
```

Se o save veio do Q1, funciona. Se veio do Q3, o jogador carrega e está noutro
mapa com as peças do dele.

Não é deste plano — mas o `Panel_campanha` que você está fazendo tem Save e Load,
e é lá que essa porta abre. **Vale saber antes de ligar o botão.**

---

## 8. Ordem

```text
Etapa 0   compilar e jogar o laço       ← bloqueia todas
   │
   ├── Etapa 1   o quadrante diz como   campo autoral + o leitor que já existe
   │              cada lado começa
   ├── Etapa 2   o 0b (3 managers)       começa pelo que não muda
   └── Etapa 3   o portão hierárquico    ← Concluido() é a base da 4
          └── Etapa 4   registro, contador e fim
```

A 3 vem antes da 4 porque as duas usam a **mesma** função. `Concluido(nó)` é o
que o portão consulta para decidir se abre, e é o que o contador `1/4` conta.
Escrever o contador primeiro produziria a segunda cópia dela.

As etapas 1 e 2 são independentes e podem correr em qualquer ordem depois da 0.

---

## 9. As decisões que são suas

1. ~~**Como o turno 1 deixa de ser armadilha**~~ **resolvido:** o quadrante é
   configurável e diz as duas coisas — tropa inicial por slot (do bake) e caixa
   inicial por slot (autoral). Sem carência, sem toggle. Ver §3.
2. **Como o jogador escolhe bloco e campanha** (§5) — painéis na Entrada ou níveis
   de zoom do mapa. *Não bloqueia: escopar primeiro deixa as duas abertas.*
3. ~~**Campanha vencida expulsa do mapa ou oferece a saída?**~~ **resolvido:
   oferece.** Ver §6.

**Nenhuma decisão em aberto.** A única coisa que ainda não tem forma decidida é
*como* o jogador vê um portão fechado (§5) — e isso é desenho de tela, resolvível
quando a tela existir.
