# Implementar o `MelhorCombate`

Documento de trabalho. A investigação preliminar é do auditor; esta versão
acrescenta **verificação no código**, cinco ajustes e a ordem de execução.

**Estado atual:** etapas 1, 2, 3, 4 e 5 implementadas. O `Attack Decision` já produz
resultado estruturado mantendo o wrapper booleano, e o DPQ de posição já possui
resolver geral fora do Capturer. O `CombatEvaluationService` já simula uma opção
canônica do `PodeMirar` e aplica as preferências de `Attack Decision` sem depender
do `AIController`. O `MelhorCombateService` já agrega origens, células e alvos
sem consumidor runtime. `Tools > Hotzone > Melhor Combate` expõe os rankings no
Scene View e no painel. O MVP terminou aqui para validação visual antes de qualquer
migração de consumidor runtime.

---

## 0. O que foi verificado antes da implementação

Quatro afirmações da investigação foram conferidas no código de partida. **As
quatro eram verdadeiras**, e uma era mais grave do que estava descrita. Os dois
últimos itens abaixo registram a linha de base que as etapas 1 e 2 já corrigiram.

```csharp
// Batches.cs:106 — nenhum parametro de arma
BuildAttackBatch(unit, team, from, to, targetId, targetCell, paths)

// AttackDecision.cs:167 — antes devolvia bool e descartava o resto
private bool PassesAttackDecision(...)

// Capturer.Attack.cs:691 — antes mantinha logica geral em arquivo de papel
private PositionDpqForAttackDecision ResolveDpqForAttackDecision(Vector3Int cell)
```

### A das escalas é pior que "incompatíveis"

Os mesmos números aparecem com **semânticas diferentes**:

| valor | onde | significa |
|---|---|---|
| `150000f` | `Assault.Targeting.cs:45` | primário de preferência de classe |
| `30000f` | `Capturer.Attack.cs:90` | primário de preferência de classe |
| `18000f` | `FireSupport.Helpers.cs:509` | primário de preferência de classe |
| `30000f` / `18000f` | **`AirCombat.cs:1338`** | **elite ≥1 vs não-elite** |
| `18000f` | `Repair.Combat.cs:69` | bônus de kill garantido |

Quem der `grep 18000f` recebe quatro ocorrências com **três significados
distintos**. Não é apenas escala divergente — é constante reaproveitada, que é a
espécie que **sobrevive ao refactor** porque parece intencional.

---

## 1. O desenho aceito

A divisão em camadas mapeia a doutrina do projeto (serviço burro → consumidor →
organizador), e o corte extra é real:

```text
PodeMirar
    ↓ legalidade e opção executável

CombatEvaluationService          função PURA de (atacante, célula, alvo, opção)
    ↓ avalia UM combate específico

MelhorCombateService             agrega células, agrupa combates, ranqueia
    ↓ consulta apenas — não cria PlayerAction

Papel / Router
    ↓ objetivo, capitão, captura, defesa, rally
```

É a mesma fronteira que separa `UnitReachEnvelopeService` de
`MelhorDesembarque`: um responde por sujeito, o outro cruza.

### Três acertos que devem ser preservados

- **nota da célula = melhor combate admissível, não soma.** É o mesmo modo de
  falha da banda de artilharia: soma premia quantidade, máximo premia decisão.
  Cinco trocas medíocres não podem vencer uma eliminação decisiva;
- **`PreparedNextTurn` nunca vira ataque imediato.** É a invariante transacional
  respeitada **por construção**, não por disciplina do chamador;
- **componentes em vez de nota opaca** (resultado militar, preferência da ficha,
  qualidade da posição, adequação ao alcance, custo de movimento). É o estilo do
  `MelhorDesembarque`/`MelhorCaptura` que já roda.

A lista de "o serviço **não** deve" da investigação está correta e completa —
mantenha-a como está.

---

## 2. Cinco ajustes

### 2.1 ⚠️ O índice de arma reordena a sequência — não é rodapé

Confirmado: `BuildAttackBatch` não carrega arma, e na execução vence a primeira
opção do sensor para aquele alvo.

Logo, um serviço que ranqueia a arma B enquanto o executor sempre dispara a A
produz **promessa ≠ execução** — e este projeto já pagou por essa classe de bug
exata (foi o motivo de o Serviço do Comando virar planner único + replay).

A investigação diz "a primeira versão precisa pontuar exatamente a opção
canônica". **Isso é frágil:** fica silenciosamente errado para toda unidade
multi-arma, e ninguém percebe até uma unidade escolher mal.

**Ajuste proposto:** o serviço devolve a opção canônica **marcada como tal** e
**recusa-se a ranquear alternativas** enquanto o batch não carregar
`weaponIndex`. Limitação alta e visível, não implícita.

### 2.2 ✅ O passo 3 virou o passo 1 e está concluído

Transformar `Attack Decision` em resultado estruturado é o item **mais barato e
mais valioso** da lista:

- **`SimulationUnavailable` se disfarçava de `Allowed`.** Agora o fallback ainda
  aprova para preservar comportamento, mas retorna status próprio e deixa de ser
  indistinguível de uma aprovação simulada;
- é a armadilha do **gate inaplicável**, que já custou tempo aqui: todo gate
  precisa separar *"não satisfeito"* de *"impossível/desconhecido"*;
- é refactor puro, com wrapper booleano, **zero mudança de comportamento**.

O wrapper `PassesAttackDecision` preserva todos os consumidores e as mesmas
strings de diagnóstico. Nenhum papel precisou ser migrado nesta etapa.

### 2.3 Falta o `HexEvaluator` na lista de migração

O passo 8 cita FS, Assault, Capturer, Repair "e os demais". Mas o `HexEvaluator`
é o **fallback** — o que roda quando nenhum papel devolve ação.

Se ele mantiver pontuação de combate própria, **sobrevive uma segunda fonte de
verdade exatamente no caminho de escape**, e o objetivo do serviço não é
atingido.

> **Correção de uma afirmação anterior deste documento.** Uma versão anterior
> dizia que o `HexEvaluator` é "o caminho mais comum do tabuleiro". O código prova
> que ele é **fallback**, não com que frequência é atingido — isso exige medição
> runtime e não foi medida. A necessidade de migrá-lo não depende da frequência.

### 2.4 `longRangeStationary` e `playConservative` — desmembrar, não reinterpretar

São campos cujo significado é decidido pelo **leitor**, não pelo dono. O serviço
não pode adivinhar qual contrato vale, e escolher um em silêncio muda combate sem
que o diff denuncie.

**Onde eles travam:** não nos passos 1–3, que são refactor puro e não interpretam
nenhum dos dois. Travam o **passo 4**, o ranking. Ver §4.

#### A evidência está nos próprios tooltips

Verificado em `UnitData.cs`. Os dois campos **já denunciam o desmembramento**:

```csharp
// UnitData.cs:179 — o campo nasceu sabendo que ia crescer
[Tooltip("Se ativo, a IA nao reposiciona esta unidade apos compra/spawn.
          Outros casos especiais podem mover a unidade em regras futuras.")]
public bool longRangeStationary = false;

// UnitData.cs:171 — já é por-coluna, e NUNCA menciona perda de HP
[Tooltip("Quando ativo, a IA joga com cautela e, sem tarefa prioritaria,
          acompanha pela retaguarda a linha de combatentes aliados. Tambem
          preserva as regras conservadoras especificas de captura, suporte,
          logistica e transporte.")]
public bool playConservative = false;
```

**Isto decide a questão do `playConservative`:** o tooltip enumera captura,
suporte, logística e transporte — e em nenhum momento fala de tolerância a perda.
Ler o campo como "menor perda própria" seria **invenção**, não interpretação.

#### E os consumidores confirmam: `playConservative` é política de POSIÇÃO

Decisão do autor, e o código sustenta. Os **dez** consumidores do campo:

```text
HexEvaluator · Backline · Transportador (×3) · TransportOperationsService
Stock · Logistics.Helpers · Capturer · FireSupport.Helpers
```

**Nenhum é arquivo de ataque.** Não aparece em `AttackDecision.cs`, nem em
`Assault.Targeting.cs`, nem em `Capturer.Attack.cs`, nem em `AirCombat.cs`.

O campo governa posicionamento, retaguarda, transporte e logística — e o próprio
tooltip diz o que ele escolhe: *"acompanha pela **retaguarda** a linha de
combatentes aliados"*. Isso é **estação na linha**, vocabulário que o projeto já
tem (vanguarda / retaguarda / flancos), não cautela genérica.

> **A pergunta nunca foi "deve sair do Attack Decision" — ele nunca esteve lá.**
> O risco era deixá-lo *entrar* agora, junto com o `MelhorCombate`. Não deixe.

#### As duas famílias, separadas

```text
ACEITAÇÃO — "que combate eu aceito"          dono: Attack Decision
    attackAcceptHpLossPercent
    attackMustSurvive
    defensiveAttackExtraHpLossPercent
    → vira AdmissionStatus DENTRO do serviço, por combate

POSICIONAMENTO — "onde eu fico"              dono: nenhum ainda
    combatRepositionMode         posso me mover para combater?
    playConservative / estação   vanguarda ou retaguarda?
    skipInitialSpawnReposition   caso de spawn
    → NÃO entra na nota de combate
```

São eixos diferentes: um é **permissão de mover**, o outro é **profundidade
preferida na linha**. E cada um entra no `MelhorCombate` por uma porta distinta:

| campo | como entra |
|---|---|
| `combatRepositionMode` | **filtro de origens** na requisição — `HoldCurrent` pede só a célula atual |
| aceitação | `AdmissionStatus` de cada combate, dentro do serviço |
| `playConservative` | **não entra.** É o papel que prefere retaguarda ao escolher entre células admissíveis |

#### O dono da tolerância já existe, e são três campos

Confirmado em `UnitData.cs:190-199`:

| campo | tooltip |
|---|---|
| `attackAcceptHpLossPercent` | "perda máxima de HP que a unidade aceita sofrer ao iniciar combate" |
| `attackMustSurvive` | "rejeita ataques em que a simulação indica que ela morre no contra-ataque" |
| `defensiveAttackExtraHpLossPercent` | "tolerância extra de HP quando estiver defendendo setor/base" |

Usar `playConservative` para o mesmo assunto **duplicaria política com dono**.

#### A divisão fechada pelo autor

```text
Attack Decision  →  tolerância à troca e à sobrevivência   (entra no serviço)
playConservative →  política de POSICIONAMENTO             (fica fora do serviço)
```

E, para o `longRangeStationary`, dois conceitos onde hoje há um bool:

```text
skipInitialSpawnReposition : bool
combatRepositionMode       : Allowed | HoldCurrent | TransportOnly
```

`TransportOnly` descreve uma peça que **não anda sozinha mas pode ser rebocada** —
e isso não é hipotético: é exatamente o caminhão-supridor rebocando artilharia
que já existe no projeto.

**Por que desmembrar é melhor que escolher um significado:** se os dois
comportamentos existem no jogo, escolher um **perde o outro**. E desmembrar é
*extração*, que é pré-requisito de qualquer parametrização futura — vale a regra
já registrada em `revisao_papeis.md`: *não dá pra parametrizar uma política que
ainda não foi extraída*.

#### O horizonte

Depois, `playConservative` deixa de ser bool universal e cada coluna ganha sua
expressão:

```text
combate     não piorar exposição
logística   atender pela retaguarda
transporte  evitar LZ ameaçada
estoque     evitar rota exposta
```

Isso é degrau 4 e **não bloqueia o `MelhorCombate`** — basta que o combate tenha
um significado próprio e explícito.

### 2.5 Unificar a medição **não** unifica a escala

Ponto que a investigação assume resolvido e não está.

Os `150k / 30k / 18k` existem porque cada papel pendurou preferência na
**própria** pontuação. Se os papéis continuarem multiplicando a saída do serviço
por pesos próprios, as escalas voltam com outro nome.

Uma regra escrita **não impede a recaída**. O retorno precisa tornar a reescala
difícil — em vez de entregar um `float Score` reescalável, entregar uma **chave
canônica**:

```text
CombatRankKey
  AdmissionStatus · CombatMode · Kill · Survives · Trade
  Damage · OwnLoss · TargetPreference · RangeFit
  PositionQuality · MovementCost
```

**Mas a struct sozinha não basta** — um papel ainda pode escrever
`key.Damage * 3000 + key.TargetPreference * 150000`. O que fecha a porta é o
serviço **também ser dono da comparação**:

```text
MelhorCombate   entrega a chave E o comparador canônico
o papel         filtra por missão, escolhe a FAIXA, desempata dentro dela
                — nunca faz aritmética sobre os componentes
```

Composição segura:

```text
faixa da missão  →  ranking canônico do MelhorCombate  →  desempate determinístico
```

Assim o Capturador põe "ocupante do meu objetivo" numa faixa superior **sem
reinventar quanto vale uma eliminação**.

> **Nota de camada, para não arquivarem isto errado:** o `CLAUDE.md` diz que
> ranquear e desempatar **nunca** são trabalho do serviço. Isso vale para o
> **serviço burro** (`PodeMirar`, `UnitReachEnvelopeService`). O `MelhorCombate` é
> **consumidor**, e a definição de consumidor no mesmo documento é literalmente
> "agrega: interseções, **rankings**, notas, pareamento 1:1". Portanto ele pode e
> deve possuir o comparador.

---

## 3. Duas extrações que servem ao capturador hoje

Independente do resto, duas peças são refactor puro e melhoram a pasta
`Capturer/` sem mudar comportamento:

| estado | extrair | de onde | ganho |
|---|---|---|---|
| ✅ | resolver geral de DPQ de posição | `Capturer.Attack.cs:691` | tirou lógica geral do maior arquivo da pasta e criou `PositionDpqResolver` |
| ✅ | avaliador puro de um combate | antes espalhado no `AIController` | criou `CombatEvaluationService`, compartilhável por runtime e Edit Mode |

O serviço separa explicitamente a opção canônica retornada pelo `PodeMirar` do
fallback automático antigo. O futuro `MelhorCombate` não autoriza fallback: se
não recebeu uma opção canônica, não promete uma arma que o executor talvez não
use. O fallback permanece apenas no adaptador legado do `AIController`, para esta
extração não alterar comportamento existente.

Sobre o `UnitCounterEvaluator`: a ressalva da investigação procede — ele avalia
fichas em HP máximo, escolhe arma sozinho e não trabalha com a opção exata do
sensor. **A matemática econômica dele é boa e deve descer** para um avaliador
canônico que aceite HP atual e a opção do `PodeMirar`; ferramenta, Shopping e
`MelhorCombate` passam a usar o mesmo núcleo.

---

## 3.5 O corte do MVP — eram dois projetos

Decisão fechada. Estávamos misturando duas coisas:

```text
1. "qual é o melhor combate desta unidade agora?"     ferramenta contida
2. "como todos os papéis passam a consumir isso?"     o major de 8 etapas
```

**O MVP é só o primeiro.** Sem papel, capitão, missão, captura, rally ou router.
Entrega:

```text
CombatEvaluationService
MelhorCombateService
Tools > Hotzone > Melhor Combate
   ── e para aqui, para teste visual ──
```

Precedente: `MelhorCaptura`, `MelhorCapitao` e `MelhorVisao` **todos** nasceram
como ferramenta sem consumidor.

### Os três modos que a ferramenta mostra

| modo | consulta | ficha exemplo |
|---|---|---|
| **Parado** | `MoveuParado` na célula atual | Artilharia — *"quem eu arraso mais?"* |
| **Mover e atacar** | `MoveuAndando` em cada célula alcançável | Soldado — bolinha na célula, nota do melhor combate dali |
| **Híbrido** | os dois rankings, separados | Tanque Z — com `preferArtilleryModeBeforeCombatant`, o modo "Auto da ficha" tenta o parado antes |

### Fica de fora do MVP

`PreparedNextTurn`, reposicionamento futuro de artilharia, `playConservative`,
`longRangeStationary`, faixas de missão, e a migração de Assault / Capturer /
Fire Support / HexEvaluator.

`PreparedNextTurn` responde **outra pergunta** — *"onde posiciono a artilharia
para talvez atirar na próxima rodada?"*. Útil, e não é esta ferramenta.

### ⚠️ Mas o MVP não pula os passos 1–3 — pula os 6–8

O `CombatResult` do MVP lista `AttackDecisionStatus`, `AttackerDpq` e
`DefenderDpq`. Isso **exige** os três primeiros refactors.

**Os três já estão concluídos.** `CombatEvaluationOutcome` reúne a simulação de
HP/arma/DPQ e o `AttackDecisionResult` de um combate específico. A etapa 4 pode
agregar — e agora agrega — esses resultados por célula e alvo, sem reimplementar
a fórmula.

O agregador mantém `StationaryRanking` e `MobileRanking` separados, devolve a
melhor opção de cada célula por máximo (nunca soma), registra rejeições do
`PodeMirar` e expõe `CombatRankKey` junto do comparador canônico. O modo Auto usa
`preferArtilleryModeBeforeCombatant` para escolher qual ranking tentar primeiro,
sem transformar a preferência de modo em peso de combate. Alternativas de arma
do mesmo alvo são ignoradas: só a primeira opção canônica que o executor usaria
é avaliada.

E o passo 2 é obrigatório por motivo técnico, não estético:

```csharp
// Capturer.Attack.cs:691 — privado, de instância, usa cache de instância
private PositionDpqForAttackDecision ResolveDpqForAttackDecision(Vector3Int cell)
{
    if (aiDpqByCell.TryGetValue(cell, out ...))
```

Um serviço **não conseguia chamar isto**. A etapa 2 extraiu a precedência
construção → estrutura → terreno, o fallback entre tilemaps e o DPQ aéreo por
altura para `PositionDpqResolver`. O cache continua no AIController, preservando
o custo e a invalidação que já existiam.

### A ferramenta é o pagamento do passo 1

Estruturar o `Attack Decision` não tem benefício visível até que **alguma coisa
exiba o status**. A ferramenta exibe. Passo 1 + ferramenta é o menor incremento
que se justifica sozinho — e é a primeira vez que `SimulationUnavailable` deixa
de se disfarçar de `Allowed`.

### A ferramenta deve funcionar no Scene:Edit Mode e no runtime

> **Correção de uma premissa anterior deste documento.** Uma versão anterior
> afirmava que HP atual, munição carregada e estado de movimento não existiam no
> Edit Mode. Isso é falso: esses dados pertencem à instância de `UnitManager` e
> são serializados na Scene.

Verificado em `UnitManager.cs` e `UnitManagerEditor.cs`:

| estado da instância | origem usada pela ferramenta |
|---|---|
| HP atual | `UnitManager.CurrentHP` / slider **Current HP** |
| autonomia atual | `UnitManager.CurrentFuel` / slider **Current Fuel** |
| movimento restante | `UnitManager.RemainingMovementPoints` / slider **Movement Remaining** |
| munição da arma | `embarkedWeaponsRuntime[].squadAmmunition` / campo **Ammo / Attacks Remaining** |
| arma e alcance atuais | `embarkedWeaponsRuntime` da própria instância |
| DPQ da posição simulada | construção → estrutura → terreno da célula avaliada |

Portanto, a Scene já é um **cenário tático simulável**. O autor pode ajustar HP,
autonomia e munição das peças no Inspector, selecionar uma unidade e auditar qual
combate ela prefere sem entrar em Play Mode. Isso é parte central do propósito da
ferramenta, não um fallback aproximado.

Os dois contextos compartilham o mesmo núcleo de avaliação e diferem somente na
fonte do tabuleiro observável:

| contexto | contrato |
|---|---|
| **Scene:Edit Mode** | lê o bake persistente da rodada 0, cozido manualmente para todos os slots pelo botão do `MatchController`; pintar, remover ou mover peças não dispara recálculo. Um modo experimental explícito pode montar uma fotografia temporária somente ao apertar **Calcular**, sem sobrescrever o bake |
| **Runtime** | lê o estado vivo das instâncias e recebe uma cópia dos contatos inimigos visíveis já publicados no snapshot confirmado do slot; a mesma lista é reutilizada em todas as origens e o `PodeMirar` não percorre o tabuleiro nem consulta visibilidade novamente |

O `PodeMirarSensor` já possui coleta própria para Edit Mode e aceita uma célula
hipotética de origem. O `MelhorCombate` deve reutilizar esse caminho; não deve
substituí-lo por uma comparação entre fichas em HP máximo.

No Scene:Edit, `FogKnowledgeSnapshotBuilder` é executado pelo comando manual
**Cozinhar FOW da Rodada 0** do `MatchController`. O comando processa todos os
slots como uma única transação e persiste na Scene as células geográficas,
cobertura de sensor, conhecimento por camada, contatos e contribuidores, além
das contribuições por fonte no formato validável pelo runtime. A receita usa
`CollectVisibleCellsForFogOfWar` por unidade; raio geográfico das construções
com sensor apenas no próprio hex; especializações de visão por camada; e
`PodeDetectar` sem observador avançado para formar os contatos do slot.

O bake **não** é invalidado nem refeito por `OnValidate`, spawners, pintura,
seleção ou pela janela Melhor Combate. Se a Scene mudou desde o último bake, a
ferramenta apenas informa que a fotografia está antiga e continua respeitando a
escolha autoral. Para experiências, a opção **Experimento: recalcular FOW** cria
uma fotografia efêmera no momento do cálculo e nunca escreve no `MatchController`.

Uma origem móvel hipotética pode melhorar a linha de tiro contra um contato já
cozido, mas não pode descobrir e atacar outro alvo na mesma ação. O `PodeMirar`
continua sendo dono de LoS, LdT e `forwardObserver` para autorizar o disparo.

Esse snapshot offline não representa exploração histórica: `KnownCells` quer
dizer **conhecido na rodada 0 no instante do bake**. O cozimento manual não pinta
tilemap, não publica eventos e não escreve memória ou `AIIntelLedger`. A
assinatura da Scene é diagnóstica; diferença de assinatura nunca aciona um novo
cozimento.

Ao iniciar uma partida, o `MatchController` tenta restaurar as contribuições de
todos os slots antes do primeiro `ApplyActiveTeamIfChanged`. Hashes, checksums e
identidade das fontes são validados. Um bake incompatível é rejeitado sem ser
sobrescrito, e o runtime segue pelo fallback normal de FOW para preservar a
correção da partida.

Autonomia não altera diretamente o dano simulado, mas participa da viabilidade e
do custo para alcançar uma origem de ataque. HP e munição entram diretamente no
combate. Movimento restante limita as origens disponíveis quando a consulta pede
o estado atual da instância.

Nos dois contextos a consulta é **pura e transacional**: não move peças, não
consome movimento, autonomia ou munição, não aplica dano e não atualiza FOW,
detecção, caches confirmados ou `HasActed`. Uma origem hipotética existe apenas
na requisição e no resultado visual da ferramenta.

No runtime não existe fallback dinâmico para percepção. O `MatchController`
fornece um `FogKnowledgeSnapshot` por
`TryCopyConfirmedFogKnowledgeSnapshotForSlot`, copiando o snapshot já publicado.
Se ele ainda não existe para o slot selecionado, a janela interrompe a consulta
e pede que se aguarde a publicação em `Neutral`; ela não chama `PodeDetectar`,
não refresca FOW e não usa a posição provisória.
`MelhorCombateRequest.TargetCandidates` recebe os contatos uma vez, e todas as
origens hipotéticas compartilham a mesma lista no runtime e no Scene:Edit.

### ⚠️ A ordem do score é PROPOSTA, e diverge do que a IA faz hoje

```text
1. admitido pelo Attack Decision      5. preferência de alvo da ficha
2. kill                               6. DPQ, quando prioritizeDpqAtBattle
3. sobrevivência                      7. menor custo de movimento
4. melhor troca (dano × perda)        8. desempate determinístico
```

Hoje a preferência de classe vale `150000f` no Assault — ou seja, **domina tudo**.
Nesta ordem ela é o critério **5**, abaixo da troca.

Isso é mudança de comportamento, não neutralidade. Como não há consumidor, não
quebra nada — mas **o autor vai clicar e ver divergência em relação ao que a IA
decide hoje.** Essa divergência é esperada e é o primeiro assunto a inspecionar,
não um bug da ferramenta.

### Critério de aceite

Não "parece certo". Três fichas × os modos que cada uma suporta, com o painel
explicando **todo combate bloqueado**:

- [ ] os mesmos valores serializados de HP, munição e autonomia produzem o mesmo
      resultado no Scene:Edit Mode e no runtime, quando FOW não remove candidatos
- [ ] alterar HP ou munição de uma instância pelo `UnitManagerEditor` muda a
      simulação sem alterar a ficha `UnitData`
- [ ] esgotar a munição da arma preferida (ex.: roofgun do tanque contra
      infantaria) faz o `PodeMirar` rejeitá-la por munição, promove a próxima
      arma executável (ex.: canhão) e a ferramenta exibe o resultado ruim dessa
      arma — sem fingir que a roofgun ainda dispararia
- [ ] executar a ferramenta no Edit Mode não altera nenhum estado serializado da
      Scene e não marca a Scene como modificada
- [ ] Artilharia (parado) — alvos ordenados, e a zona morta de alcance mínimo não
      aparece como alvo
- [ ] Soldado (mover e atacar) — bolinha por célula, linha da célula ao melhor
      alvo, célula onde o ataque é **bloqueado** visivelmente distinta
- [ ] Tanque Z (híbrido) — os dois rankings, e o "Auto da ficha" respeitando
      `preferArtilleryModeBeforeCombatant`
- [ ] pelo menos um caso de **`SimulationUnavailable`** visível no painel — é a
      coisa que hoje ninguém consegue ver
- [ ] a arma exibida é a **canônica do `PodeMirar`**, marcada como tal

---

## 4. Ordem de execução

| quando | o quê | risco |
|---|---|---|
| **1 ✅** | `Attack Decision` → resultado estruturado + wrapper booleano | concluído; comportamento preservado |
| **2 ✅** | extrair o resolver geral de DPQ | concluído; comportamento e cache preservados |
| **3 ✅** | extrair o avaliador canônico de um combate | concluído; `AIController` virou adaptador |
| — | **decisão do autor sobre os dois campos ambíguos** (§2.4) | não é código |
| **4 ✅** | `MelhorCombate` como **consulta apenas**, sem consumidor | concluído; nenhum consumidor runtime |
| **5 ✅** | ferramenta `Tools > Hotzone > Melhor Combate` | implementada; aguarda validação visual do autor |
| ══ | **FIM DO MVP — parar aqui e testar visualmente** | ══ |
| **6** | migrar **um** consumidor simples | primeiro risco real |
| **7** | comparar ranking antigo × novo nos logs | **é aqui que o tempo vai** |
| **8** | migrar FS, Assault, Capturer, Repair, **HexEvaluator** | uma classe por vez |

Os passos **1 a 5 são o MVP** e cabem num Y. Os passos **6 a 8 são o major** e
só começam depois que o autor clicar em Soldado, Artilharia e Tanque Z e disser
*"sim, é exatamente essa resposta"*. Só então se discute **qual cachorro consome
essa resposta, e de que maneira**.

### Enquadramento

O `MelhorCombate` completo é um **major disfarçado de serviço** — 8 passos, 7
consumidores, e o passo 7 é onde o custo real mora. Pelo esquema do autor é
**X ou Y**, nunca Z.

Mas os passos **1 a 3 são seguros e servem ao capturador hoje**, e não competem
com a migração `Capturer.Explorer → MelhorVisao` (462 linhas, serviço já
validado). Podem andar juntos.

---

## 5. O resumo em uma linha

```text
PodeMirar        diz "pode"
simulação        diz "o que acontece"
Attack Decision  diz "aceito"      ← resultado estruturado; wrapper legado ainda entrega bool
MelhorCombate    diz "entre todas as células e combates aceitos,
                  estes são os melhores segundo a ficha"
o papel          diz "por que este combate interessa à missão"
```

O órgão já está praticamente desenhado. O que falta não é inventá-lo — é
**parar de recompor uma nota diferente em cada consumidor**.
