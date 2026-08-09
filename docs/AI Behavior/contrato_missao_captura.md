# Contrato — missão de captura (alocação pegajosa)

**Estado:** contrato histórico, parcialmente implementado e parcialmente
supersedido. Escopo original: o **básico** — só soldados, sem transporte, sem
elite, sem suporte de fogo. Use as atualizações datadas para distinguir o
runtime atual do desenho antigo.

> **ATUALIZAÇÃO VINCULANTE — 2026-08-09.** A população da aquisição foi
> decidida depois deste rascunho: capturador **com plano não entra** no matching
> do Melhor Captura. Com HQ, o planner publica as missões formais e somente os
> `RogueUnitIds` dividem o restante; sem HQ, todos são rogues e usam o mesmo
> matching residual. Toda passagem abaixo que põe formal + rogue no mesmo
> leilão está supersedida por esta regra e por `Capturador.md` §2.

> **HOJE** = código verificado. **CONTRATO** = decisão tomada, não escrita.
> **ABERTO** = ninguém decidiu.

---

## 0. Descoberto em partida: metade disto já existe

Levantado no tabuleiro de treino 12×12, turno 1, com o log
`[Rebelde] 1 confirma DesignatedCaptureTarget #2 em (0,0,0)`.

**HOJE, no caminho rebelde e SÓ nele**, a alocação pegajosa está pronta:

```text
UnitManager.cs:169-171           aiHasDesignatedCaptureTarget
                                 aiDesignatedCaptureTargetInstanceId
                                 aiDesignatedCaptureTargetCell     [SerializeField]
SaveDataDtos.cs:306-309          os quatro campos no DTO
SaveDataMapper.cs:241-246        gravados e restaurados            ← ATRAVESSA O SAVE
AIController.Rebel.cs:281-310    pendingRebelCaptureTargets
                                 CommitPendingRebelCaptureTarget   ← pending/commit
AIController.Rebel.cs:223-275    TryResolveUnitDesignatedCaptureTarget ← as baixas
```

O *"salva, fecha, abre e o cara que ia pro norte continua indo pro norte"*
**já funciona hoje, para a rebelde**, e é testável sem escrever uma linha.

### 0.1 O que então falta de verdade

| # | lacuna |
|---|---|
| 1 | **é rebelde-only.** `TryDecideCapturerAction` continua releiloando pelo `CaptureOpportunityClaimService` a cada mudança de snapshot |
| 2 | **são três verdades sobre a mesma coisa**: `AIPlanRuntimeIntent` (nunca `Capture`), `DesignatedCaptureTarget` (só rebelde) e a reivindicação volátil |
| 3 | **não há baixa por alcance.** As baixas de hoje (§3.7) não olham rota: a rebelde segura um alvo do outro lado do oceano para sempre |
| 4 | `pendingRebelCaptureTargets` duplica `pendingAIDesignatedMissions` — dois pending, dois commits |

O trabalho não é construir o mecanismo. É **promover o mecanismo do rebelde à
camada compartilhada** e parar de ter três representações.

---

## 1. O que muda

**HOJE** `CaptureOpportunityClaimService` faz matching 1:1 somente sobre os
capturadores **sem plano**. Endereços formais são publicados diretamente pelo
planner em `AIPlanRuntimeIntent.Capture` e retirados do conjunto antes de os
rogues dividirem o restante.

**CONTRATO** A reivindicação vira missão:

```text
matcher roda  →  só sobre capturador SEM missão e construção SEM dono
quem tem alvo →  carrega AIPlanRuntimeIntent.Capture e sai do leilão
baixa         →  só pelas condições da §3, avaliadas todo turno
```

### 1.1 Onde cada pedaço mora

**HOJE**, as três peças já são compartilhadas pelos dois caminhos de decisão:

```text
AIController.Rebel.cs:105-149     GetOrBuild → TryGetClaimForUnit
AIController.Capturer*.cs         o mesmo serviço
AIController.Phase2.cs:299        CommitPendingAIDesignatedMission(unit)
```

**CONTRATO**

| peça | onde | por quê |
|---|---|---|
| pegajosidade | dentro do `CaptureOpportunityClaimService` | um lugar, e rebelde e capturador normal ganham juntos |
| missão *pending* | quem decidiu escreve (`Rebel.cs` ou `Capturer*.cs`) | é a decisão dele |
| commit | `Phase2.cs:299`, já centralizado | já roda depois do batch comprometido |

**CONTRATO — atenção ao roteador.** A decisão do slot rebelde sai de
`TryDecideRebelAction` (`AIController.Router.cs:107`), **antes** do bloco
`plan != null`. Ela **não** passa por `TryDecideCapturerAction`. Mudança escrita
só no arquivo do capturador não é exercitada por tabuleiro rebelde.

### 1.2 O conjunto de prédios com dono é DERIVADO

**CONTRATO.** "Quem já tem dono" sai das **missões vivas**, não de um registro
paralelo. Registro separado seria a terceira cópia da mesma verdade — o par
ícone-do-hex × Jornal de novo.

Consequência boa: **esta mudança não acrescenta um único campo ao save.** A
missão já é persistida (`SaveDataMapper.cs:397` chama `SetAIDesignatedMission`),
e o conjunto de alvos ocupados se reconstrói dela na carga.

---

## 2. Aquisição

**CONTRATO ATUAL.** O matcher 1:1 determinístico continua com a prioridade
estável do capturador antes do custo, mas sua população é exclusivamente rogue.
O plano formal não “vence” dentro do matcher: ele acontece antes e remove seus
endereços. Entram depois os `RogueUnitIds`; sem plano, entram todos.

**HOJE** a banda de aquisição é o **Operacional** (`CacheKey.OperationalTurns`;
no log: *"alcança prédio capturável próximo no Operational: custo=5 no turno 2 de
2"*).

---

## 3. As condições de baixa

Esta seção **é** a especificação. Alocação pegajosa que segura atribuição ruim é
pior que otimizador que troca demais.

**CONTRATO — reavaliadas todo turno**, contra o estado do alvo. Não só verificadas
na chegada. É a mesma lição que o `SpottingDeCobertura` já registrou: missão que
depende de estado alheio se confere contra ele.

### 3.1 Cumprida

```text
o prédio é meu agora      →  capturei. Baixa limpa.
```

### 3.2 Perdida — o alvo deixou de existir como alvo

```text
o prédio já é do meu time     →  outro capturador ou o Serviço do Comando tomou
o prédio deixou de ser capturável POR MIM  →  perdi a chave / a regra mudou
```

**CONTRATO — o prédio capturado pelo INIMIGO não é baixa.** Continua capturável,
continua sendo o meu alvo. Trocar de objetivo porque o dono mudou seria abandonar
justamente o que virou mais valioso.

### 3.3 Perdida — não chego mais lá

Aqui mora a histerese, e ela é **de banda, não de bônus**:

```text
banda de AQUISIÇÃO   Operacional            (2 turnos encadeados) — apertada
banda de RETENÇÃO    componente conectado   custo finito qualquer  — larga
```

**CONTRATO.** Adquire perto, segura longe. Um soldado marchando dez hexes até a
cidade **nunca** sai da missão só por estar longe — ele está andando para lá, e a
cada turno fica mais perto.

A baixa dura é topológica, não métrica:

```text
o componente de movimento não toca mais o alvo  →  baixa
```

**HOJE** essa máquina existe e roda — a frase `#84 descarta pax=#173: componentes
de movimento nao se tocam` saiu do log do turno 1. Não é peça nova.

Isso substitui o `−15` de aderência ao objetivo anterior. A aderência era um
remendo por fora de um otimizador global sem memória; a assimetria
aquisição/retenção resolve na raiz. **Se o tabuleiro de 2 capturadores parar de
trocar, o `−15` pode ser aposentado.**

### 3.4 Perdida — a peça

```text
a unidade morreu  →  a missão morre com ela
```

### 3.5 O que NÃO é baixa

| não é baixa | por quê |
|---|---|
| outro capturador está mais perto | é exatamente o otimizador global. Ser subótimo não é motivo para largar — é o ponto inteiro da pegajosidade |
| tem inimigo em cima do prédio | é problema de combate, não de missão. A missão fica; o ocupante é resolvido |
| meu turno não andou (bloqueio, MP) | turno travado não é missão perdida |
| o plano do time mudou | é o que o `−15` estava remendando. Ver §3.6 |

### 3.6 Preempção pelo plano formal

**ABERTO.** Hoje plano formal vence oportunidade na **aquisição**. Com missão, a
pergunta nova é se um objetivo de plano formal pode **arrancar** uma missão
oportunista já em curso.

Recomendação: pode, mas **só no início do turno** e **com log explícito** do
motivo. Preempção silenciosa no meio da Fase 2 é indistinguível do bug que a
pegajosidade existe para matar.

No tabuleiro rebelde isso **nunca dispara** — `BuildObjectivePlan` apaga o plano
do slot rebelde e retorna (`PlanEvaluator.cs:85-93`), então não há plano formal.

### 3.7 As baixas que a rebelde JÁ tem

**HOJE**, em `TryResolveUnitDesignatedCaptureTarget`
(`AIController.Rebel.cs:223-275`):

```text
a unidade morreu                                    →  baixa   (= §3.4)
a unidade não satisfaz Capturador                   →  baixa   (usa CanSatisfy, correto)
o alvo sumiu do AllActive                           →  baixa   (= §3.2)
!IsRebelCapturable                                  →  baixa   (= §3.2)
ocupante ALIADO bloqueando a célula do alvo         →  baixa   ← não estava neste doc
```

**CONTRATO — a quinta é boa e fica.** Aliado em cima do alvo quer dizer que
alguém já chegou; insistir é empilhar. Note a assimetria com §3.5: **inimigo** em
cima não é baixa, **aliado** é.

**CONTRATO — falta a §3.3 inteira.** Nenhuma dessas cinco olha rota. A retenção
de hoje é infinita: nada solta um alvo que ficou inalcançável. Num tabuleiro
todo plains isso nunca aparece — é a lacuna que só um mapa com água exibe.

### 3.8 Fora de escopo deste documento

**FUTURO.** Unidade embarcada mantém a missão — o passageiro está sendo entregue
ao alvo dela, e é justamente daí que sai a próxima peça: o `QueroCarona` passa a
perguntar *"minha missão é aquele prédio; chego sozinho?"* em vez de *"existe
capturável livre no meu alcance?"*. Só soldados neste teste; a carona entra
depois.

---

## 4. O log que hoje não existe

**HOJE** o verbo nunca foi escrito, logo não há uma linha sequer para ele. Sem
log o teste vira "olhar o soldado e achar que continua indo pro norte".

**CONTRATO — nasce junto com o commit:**

```text
[Missao] #12 Capture -> (-31,-13) setor C   adquirida (matcher 1:1, custo=2)
[Missao] #12 Capture -> (-31,-13)           mantida   (custo=1)
[Missao] #12 Capture -> (-31,-13)           BAIXA: capturei
[Missao] #12 Capture -> (-31,-13)           BAIXA: componente nao toca mais o alvo
```

O motivo da baixa é a parte que importa. A pergunta *"por que esse capturador
trocou de prédio?"* não tem resposta no log de hoje.

---

## 5. Protocolo de teste

### Tabuleiro 1 — persistência (o que o autor pediu)

```text
1 QG, 1 cidade, IA rebelde, 1 soldado
```

Prova: persistência e disciplina transacional. **Não** prova anti-oscilação — com
N=M=1 o matcher é trivial e não há disputa.

```text
turno 1   [Missao] adquirida, alvo = a cidade
salvar, FECHAR o jogo, abrir, carregar
turno 2   MESMA missão, MESMO alvo, sem releilão
F11 passo a passo e cancelar   →  NENHUMA missão fantasma
```

O último é grátis de testar e é onde esse tipo de mudança costuma vazar: missão
só vira estado depois do compromisso.

### Tabuleiro 2 — anti-oscilação (o outro motivo da mudança)

```text
1 QG, 2 cidades mais ou menos equidistantes, IA rebelde, 2 soldados
```

```text
antes   os dois trocam de alvo entre turnos (é o que o −15 remenda)
depois  cada um mantém o seu até baixa por §3
```

Se passar, o `−15` entra na fila de aposentadoria — com o seu próprio teste,
noutro dia.

---

## 6. Leituras

| documento | por quê |
|---|---|
| `docs/AI Behavior/contrato_missoes.md` | o que é missão, e as duas regras que o código já impõe (valor novo no fim do enum; missão só vira estado depois do compromisso) |
| `docs/arquitetura/acoes_transacionais.md` | o invariante que o commit em `Phase2.cs:299` respeita |
| `docs/AI Behavior/contrato_envelope_alcance.md` | banda é parâmetro da unidade avaliada — vale para as duas bandas da §3.3 |
