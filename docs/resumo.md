# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-07, **depois** da tag `v8.1.1`. Leia isto
primeiro.

---

## Estado

`v8.1.1` tagueada e publicada. Relatório:
[`relatorio_v8.1.1.md`](relatorio_v8.1.1.md).

**Cinco portas em série, todas validadas em campo**, e a cadeia de resgate
multimodal anda até ao penúltimo passo:

```text
Soldado#1 embarca no APC#8  →  APC#8 (carregado) pede carona pelo destino da
carga  →  Navio#5 promete resgate ao APC e zarpa (rotaPax=ReachableStrategic)
```

O fio, e ele repete-se cinco vezes:

> **O que barrava era prazo, política ou formação vestidos de fato — sempre
> dentro de um teste que se chamava estrutural.**

```text
MeetingWalkTurns = 2        prazo no filtro topológico
âncora só de capturável     política no lugar da coordenada
horizonte por tier          o Strategic negava o próprio horizonte
flags invertidas            pedia LZ estratégica, recusava rota estratégica
InRearSlice no resgate      formação onde o ofício é sair da formação
```

### ⚠️ O que a v8.1.1 QUEBROU — e é a primeira coisa a consertar

**As cinco correções somadas fizeram o navio vaguear.** Elas abriram o oceano
inteiro como lista de candidatos e **nenhuma deu ao navio motivo para ficar**:

```text
1. navio promete #8 e navega até o encontro
2. #8 chega a sua vez e escolhe Delivery — dirige para longe
3. o encontro evapora; a opção de #8 sai da lista
4. o farol da promessa não a acha, e cai para o próximo passageiro
5. repete
```

**O navio não tem histerese** — mesmo defeito que o capturador já teve
(otimizador global sem aderência ao objetivo anterior). A promessa devia ser a
histerese, mas o farol só pesa quando a opção do prometido **está na lista**;
fora dela, ela não perde para nada, ela não existe.

### ✅ Resolvido depois da tag — o aninhamento

`abd5d91` (fora da v8.1.1): `TryDecideNestedTransportEmbarkAction`, irmão do
`TryEvacEmbarkAction`, no topo de `TryDecideTransportOperationsAction`. **T15
fechada.** Validado em campo no T4:

```text
[Transporte] 8 ANINHA — embarca em #9 slot 1 levando a carga junto.
```

O gate é a própria fila de carona: quem alcança o destino da carga devolve
`wantsRide = false` e nunca sobe. A guarda da ficha já estava publicada.

⚠️ **`Embarcar` do transportador ≠ `embarcar` da unidade.** O primeiro é
aninhar-se noutro veículo; o segundo é o passageiro subir. Confundi os dois numa
sessão inteira.

### ❌ E a promessa NÃO precisa de conserto — a proposta anterior estava errada

O que estava escrito aqui — *"promessa pendente deve segurar posição"* —
transformava o farol em lock, e o cabeçalho do `RidePromise.cs` proíbe isso:
*"promessa NÃO é preempção; ela pesa na escolha, não sequestra o veículo"*.

A baixa por terceiro **já funciona**: `#9 baixa a promessa a pax=#1: passageiro
embarcou`, e promete a outro no turno seguinte. O navio a vaguear era sintoma do
aninhamento em falta, não de lock em falta. **Não implementar.**

---

## ⏭️ O PRÓXIMO ITEM — o peso da vaga

Com o aninhamento a funcionar, apareceu o que estava por baixo. Ficha do Chinook:

```yaml
- slotId: passageiros   capacity: 2   exclusiveSlot: 0
- slotId: APC           capacity: 1   exclusiveSlot: 1   ← ocupa o casco inteiro
```

`exclusiveSlot` tem **zero leitores na IA** — só `UnitManager.cs:2506-2510`, o
motor. O `MelhorEmbarque` oferece o mesmo Chinook a quatro passageiros ao mesmo
tempo (`slot=0` a dois soldados, `slot=1` a dois APCs) e o motor recusa depois.

**A conta que a IA não sabe fazer:**

```text
Chinook com APC carregado (1 soldado)  →  2 unidades, casco inteiro
Chinook com 2 soldados                 →  2 unidades, casco inteiro
                                          ─────────── EMPATE
```

E o navio já vinha buscar aquele APC. O aninhamento no Chinook não ganhou nada,
custou a carga do navio e deixou dois soldados na ilha.

> **A IA não sabe o que uma vaga custa nem o que ela carrega.** Exclusividade e
> carga aninhada são a mesma dimensão ausente, vista de dois lados.

**Um termo, não dois itens:**

```text
peso da opção = unidades que traz − capacidade que desloca
                                     ↑ é aqui que exclusiveSlot entra
```

Casa: `MelhorEmbarqueService`, o `optionScore` do laço de pares.

⚠️ **É o laço mais caro do transporte** — 2356 pares e ~700ms no Chinook. Não
abrir com pouca margem de contexto. E não escrever "ler `exclusiveSlot`" como
gate isolado: isso não resolve nada; o que resolve é o termo.

### Ainda em aberto, e independente

`BuildAttempts` devolve cedo com carga (`Courier; Delivery; return`). Com vaga
livre e alguém na fila, o transportador **não parte para entregar**: ou aproxima
do tático do segundo caroneiro, ou segura. A guarda natural é o próprio Pickup —
se ninguém está materializável, a escada cai sozinha para Courier. Sem constante
nova. Decisão do autor, já tomada.

### O defer da iniciativa — ✅ validado no T2

`ShouldDeferPassengerForIncomingTransport` ([`AIController.Phase2.cs`](../Assets/Scripts/Match/AI/1.%20Phases/AIController.Phase2.cs)):
quem está na fila de carona cede a vez a um transportador que ainda não agiu,
tem vaga, está a ≤ MP+1 e encosta por topologia. Guarda `!secondPass` contra a
cessão mútua com o `cede destino`.

**Rodou e passou** — três cessões no T2:

```text
Fase2 — Soldado#4 espera carona e cede vez para APC#3 encostar primeiro.
Fase2 — Soldado#11 espera carona e cede vez para APC#3 encostar primeiro.
Fase2 — APC#8 espera carona e cede vez para APC#3 encostar primeiro.
```

### Doutrina nova, escrita e sem código

O autor desceu o modal de sensores à mão para quatro casos do capturador e três
do transportador. **O modal não é uma cadeia "o primeiro que responde ganha" — é
um funil que preenche quatro campos**, e essa forma já é o `PlayerAction`:

```text
âncora       PARA ONDE            Capturar, Enxergar, Detectar
movimento    ATÉ ONDE hoje        Reposicionar
etiqueta     O QUE fazer lá       Capturar, Mirar, Desembarcar, Suprir, Fundir
publicação   o que fica no mundo  Embarcar → "quero carona"
```

Consequências, todas no `relatorio_v8.1.1.md` §Doutrina:

- **subpapéis terrestre/aéreo/naval não devem existir** — o mesmo funil serve
  trem e hidroavião. Teste: *se um passo responderia diferente para os dois, não
  é passo, é resposta de sensor*
- **`TouchesComponent` é a forma geral de "praia"** — estação, helipad, convés e
  praia são a mesma pergunta
- **a escada de âncora é compartilhada:** EVAC → casa → retaguarda da massa →
  fica. O vazio pula do 2 para o 4 porque o 3 exige direção de frente conhecida
- **`publicação` é o barramento entre papéis** — quatro moradores; nenhum papel
  chama outro
- **`isStranded` só é medido contra capturáveis** — quem não captura não tem
  sujeito para o encalhamento
- **publicação só vale se o solicitante puder aceitar** — soldado com 0 de
  autonomia não embarca nem com o transporte ao lado, e o anti-fome promove esse
  pedido a inegociável em 3 turnos

---

## Estado anterior — v8.1.0

**34 commits, e o transporte inteiro remexido.** Mas o fio do dia não é nenhuma
das mudanças — é uma constatação que apareceu **três vezes**:

> **Quando um comportamento parece "esquecido", conferir se ele não está apenas
> depois de um `return`.**

```text
skip do Tactical      estava no organizador; procurei no serviço
MelhorCapturaCalls:3  o alvo é resolvido 3× por decisão
largar × avançar      decidido pela posição do if, não por comparação
```

Nas três o conteúdo estava certo e **a ordem estava errada**.

### O que rodou e o que não rodou

**A fatia 1 passou** (atravessou duas versões compilada). O ciclo `T1→T6` fechou
e a missão morre limpa quando o objetivo é tomado.

⚠️ **Depois disso, 34 commits e nenhuma corrida de aceitação fechada.** O que se
sabe é que **em gameplay o comportamento está certo** — o APC escolhe hex no
Tactical fora da névoa e desembarca. O que **não** se sabe é se cada mudança
individual está certa: elas nunca foram exercitadas uma de cada vez.

### O sinal de log de cada mudança sem teste

```text
[Missao] ... (adquirida) já no T1              missão antes dos atalhos
PassengerTarget #N MISSAO (x,y) verbo=         courier lê a coordenada
ancora entrega (x,y) (Tactical; ...)           mira a zona, não o prédio
ancora avancar (x,y)                           sem célula conhecida, entra no escuro
larga no TACTICAL / ADIA a largada / OPERACIONAL   os três tempos
range=6 (Operational; Tactical=3)              banda do passageiro
fila de carona pede Transportador xN           demanda derivada (só sem APC no mapa)
```

## A FATIA 1 PASSOU — 2026-08-06, depois da v8.0.1

O commit `3e0565d` atravessou duas versões compilado e sem rodar. **Rodou, e
passou.** `Hot Seat 0 - Treino`, com `Reload Domain and Scene` ligado:

```text
T1   origemAlvo=servico   envelope=BeyondOperational   QueroCarona=SIM   (7,0)→(4,0)
     [Missao] 1 Capture -> (0,0,0) predio=#2 (adquirida)
────────────── salvar · Stop · Play · carregar ──────────────
T2   origemAlvo=reserva   envelope=Operational  custo=4  QueroCarona=NAO  (4,0)→(1,0)
     [Missao] 1 Capture -> (0,0,0) predio=#2 (mantida)
     [FilaCarona] #1 sai da fila apos 1 turno(s)
```

Bate **linha por linha** com o traço pré-fatia do relatório da v8.0.0. A fatia é
subtração, então **igualdade É o resultado correto**: os três campos
`aiDesignatedCaptureTarget*` sumiram, saíram do DTO, e `AIPlanRuntimeIntent.Capture`
passou a ter escritor **e** leitor que atravessa o save.

**Onde a missão é escrita — e por que isso está certo:** depois do commit da
ação, nunca na decisão. É o invariante transacional. Não adianta conferir o
Inspector antes de a unidade agir no turno 1: não há missão ainda.

### O ciclo completo T1→T6 — corrido em 2026-08-06, log em `docs/gamelog/log.md`

| turno | dist | banda | QueroCarona | `MelhorCapturaCalls` | `decision` | estágios |
|---|---|---|---|---|---|---|
| T1 | 7h | BeyondOperational | **SIM** | **3** | 136ms | 8 |
| T2 | 4h | Operational | NAO | 1 | 25ms | 7 |
| T3 | 1h | *pulado* | — | 0 | 7ms | 1 |
| T4 | 0h | *pulado* | — | **0** | **1ms** | **0** |
| T5 | 7h | BeyondOperational | **SIM** | **3** | 84ms | 8 |
| T6 | 5h | Operational | NAO | 1 | 24ms | 7 |

**A missão morre limpa** — era a única incógnita do teste:

```text
T1 predio=#2 (adquirida)  ·  T2 (mantida)  ·  T4 capturado
T5 predio=#1 (ADQUIRIDA — nova, sozinha)  ·  T6 (mantida)
```

Sem resíduo e sem baixa forçada. O `[SemPlano]` reancorou no HQ inimigo quando o
serviço não achou mais capturável em banda.

**O T3 pula o `QueroCarona` — e o skip já existia.** Não no serviço: em
`TryDecideCapturerAction`, via `[Oportunista] captura local ... antes de embarcar`,
que retorna **antes** do gate de embarque. ⚠️ Eu procurei no `QueroCaronaService`,
não achei early-out e concluí "não existe" — **camada errada**.

**O T4 é o chão absoluto:** `stages=- metrics=-`. Em cima do próprio alvo, a IA
não consulta o tabuleiro uma única vez.

### Fatia 2 — o alvo agora está MEDIDO

```text
MelhorCapturaCalls   3 · 1 · 0 · 0 · 3 · 1
                     ↑           ↑
                     descobre    descobre
```

**3 quando descobre o alvo, 1 quando lembra dele.** A missão já corta 3→1 nos
turnos seguintes; a fatia 2 é levar o **turno de aquisição** de 3 para 1 também.
Turno mais caro da partida: T5, `routeDistance:50,9ms/39`,
`MovementQueryCachesBuilt:940`.

⚠️ **`ms` entre corridas não vale sem a contagem ao lado.** O mesmo T2 mediu
125ms logo após um load e 25ms em corrida seca — **contagens idênticas** (`/39`,
`/5`, `/1`). Era JIT, não lógica. As contagens não mentem; o relógio mente.

### Fatia 2 — medir antes de escrever

Ela ia inverter o táxi (missão no topo, carona medida contra ela). **Parte disso
já funciona no caminho rebelde** — o log do T2 mostra a reserva alimentando a
recusa de carona:

> *"alcança alvo reservado Cidade@(0,0,0) no Operational: custo=4 no turno 2 de
> 2. **Recusa carona**."*

Levantar o que sobra da fatia 2 antes de abrir editor.

---

## A voz dos papéis — o método que apareceu hoje

O capturador tinha ~20 exceções que pareciam arbitrárias. Elas ficaram legíveis no
instante em que o **lema** apareceu:

> **O capturador adianta a renda do exército.**
> **Nenhum prédio é dele, e o HP é o relógio.**
>
> *"É a mosca atraída pela luz roxa. Ele não consegue evitar."*

E o teste que ele produz, que hoje é critério de aceitação de regra nova:

> **Esta exceção adianta renda, ou existe porque a peça se achou dona?**
> As que adiantam renda viram **termo do score**. As que existem por posse
> **dissolvem**. O que sobrar é **gosto** — e só isso vira política.

**As seis vozes estão escritas.** Cada papel tem lema, ficha e marcha:

| papel | a moeda — *onde mora o valor da peça* | funde |
|---|---|---|
| Capturador | o **corpo** — HP **é** a taxa | **ganha** |
| Transportador | as **vagas** | perde |
| Assalto | a **arma** — cada casco é ameaça | perde |
| Fogo de Suporte | a **formação** — cones cruzados | perde, e agrupar também |
| Vigilância | a **origem do cone** | perde |
| Logística | o **estoque** — média ponderada conserva | **ganha** |

> **Cada papel tem uma moeda, e a moeda decide sozinha se fundir é ganho ou
> perda.** Seis papéis, seis acertos — inclusive nas duas vezes em que a resposta
> contraria a intuição de HP.

### Onde mora o quê — a divisão que apareceu ao errar

```text
marcha   o INVARIANTE — o que os ramos compartilham
ficha    o PARAMETRO  — 1 rodada / 2 rodadas + emerge
```

**As marchas envelhecem melhor que as seções** porque foram escritas no nível
certo. Se um verso parecer contradizer uma seção, **testar primeiro se o verso
está um nível acima dela** — errei isso duas vezes seguidas na mesma tarde.

---

## Onde eu parei — os documentos

| documento | o que é |
|---|---|
| [`AI Behavior/ficha_do_papel.md`](AI%20Behavior/ficha_do_papel.md) | a matriz `Pode*` → `Melhor*` **pareada pelo autor**, o questionário padrão, `RoleData` como dado |
| [`AI Behavior/Capturador.md`](AI%20Behavior/Capturador.md) | §0 o lema; §1 e §3 revistos; apêndice com a **Marcha do Capturador** |
| [`AI Behavior/Transporte.md`](AI%20Behavior/Transporte.md) | §0.1 a ficha; §7 aninhamento; §12 a moeda; §13 limiar de reparo; §15.1 postura ❓ |
| `Match/AI/3. Shopping/Shopping.md` | **novo** — o buraco elegibilidade × preferência, e os três papéis-fantasma |
| [`AI Behavior/contrato_missao_captura.md`](AI%20Behavior/contrato_missao_captura.md) | alocação pegajosa e as condições de baixa |
| [`AI Behavior/contrato_recencia_de_cobertura.md`](AI%20Behavior/contrato_recencia_de_cobertura.md) | ledger de idade da vigilância |
| [`AI Behavior/Assalto.md`](AI%20Behavior/Assalto.md) | a ficha; §5.1 **novo** — furtividade aérea, 1 rodada, e por que o custo é de outra natureza que o do sub |
| [`AI Behavior/FireSupport.md`](AI%20Behavior/FireSupport.md) | a ficha; a modalidade **híbrida**; o auto-repelir como consequência da moeda |
| [`AI Behavior/Vigilancia.md`](AI%20Behavior/Vigilancia.md) | **novo** — §0 o teste de pertencimento; §2.1 detecção total; §5.1 o preço do tiro submarino |
| [`AI Behavior/Logistica.md`](AI%20Behavior/Logistica.md) | **novo** — o espelho da Vigilância; §5.1 a triagem que lê a moeda de quem pede |
| `Units/Capturer/Capturer.md` | o código: a ordem real, os seis mecanismos de ceder, o inventário |
| `docs/AI Behavior/rascunho/` | **a fonte** — o que o autor escreveu antes das fichas. Quando uma ficha divergir, confere-se aqui |

**Dívida declarada:** o `Capturer.md` é quatro documentos grampeados. A fronteira
com a doutrina está escrita, mas a ordem interna não ajuda quem lê do começo.

---

## MUDA REGRA — a lista de divergências doc × código

**É o trabalho concreto que sobrou.** Cada uma tem doc dizendo uma coisa e código
fazendo outra, e a regra dos docs de doutrina é *"onde o código divergir, o código
está errado"*.

```text
ajuda entre eixos          IsOtherAssignedCapturerTarget (Capturer.cs:52) barra
                           alvo alheio INCONDICIONALMENTE. A doutrina condiciona
                           à banda: se o dono está no Operacional, outro ajuda

swap por cap power         FindSwapIncomingCapturer compara HP CRU. Funciona hoje
                           porque GetCapturePower devolve HP — QUEBRA no dia em
                           que a chave de eficiência entrar (ideias_futuras §10)

capturador em Collapsing   Demand.cs:3092 dá +16000 a Assalto/Fogo/AA e NEGA ao
                           capturador, "porque é expansão". A doutrina diz que ele
                           defende a LINHA DE RENDA — corpo em prédio conquistado
                           é a defesa mais barata que existe

MelhorVisao (ramo IsAll)   a matriz diz "revelação pura de hexágonos"; o ramo
                           IsAll responde por detecção
                           (contrato_recencia_de_cobertura §4.2)

imposto de conscrição      só ConscriptionDoctrine liga. A política do autor pede
                           macroLosing como gatilho também (Shopping.md §6)
```

---

## Buracos estruturais

**Quatro `Melhor*` faltam:** `Suprir` (criticidade, peso por elite, manutenção
preventiva), `Fundir` (fundir na retaguarda — hoje dentro do `AIRepair`),
`MelhorDeteccao` e `MelhorSpotting`.

**Duas casas do questionário do capturador estão vazias no runtime:** `Detectar` e
`Enxergar` correspondem a `RevelacaoDeContato` e `RevelacaoTerritorial`, que o
`contrato_missoes.md` marca como brainstorming.

**Três papéis-fantasma no enum**, que existem para o shopping conseguir comprar:
`CapturadorCombatente` (12), `ArtilheiroCombatente` (13), `AntiaereoCombatente`
(14). Roteiro seguro de remoção em `Shopping.md` §3.1 — e `UnitData.roles` **não é
persistido no save**, então o risco é asset e cena, não arquivo de partida.

**Rotas: falta limpar a origem.** Todos os mapas migraram (`routesMigratedToScene`
ligado); falta apagar a seção do `StructureDatabase`, o `StructureData.roadRoutes`
e o `RoadRouteDefinition.ownerDatabase` — nessa ordem, `ownerDatabase` por último.

**`fieldEntries` do `ConstructionDatabase`:** mesma doença, **zero leitores de
runtime**. Autoria no asset errado.

**`ObjectiveManager` é `DontDestroyOnLoad` sem gancho de `sceneLoaded`.** Falta
conferir quem chama `ClearPlanForSlot` — se ninguém, o plano do mapa A chega no
mapa B.

**`[FoW][RoundZeroBake] restored=1/2`.** Um slot rejeitado, motivo calado:
`enableFogValidationLogs` desligado, e a linha `rejected=<motivo>` existe
(`MatchController.cs:7093`).

---

## A dívida de validação em jogo

Da lista da `v7.2.1`, continuam sem partida:

```text
1. as duas janelas lado a lado         o teste do relatorio unificado
2. radar movido duas vezes             delta do som: toca na primeira, cala na segunda
3. aeronave em voo -> fow off          deve RECUSAR; depois nevoaTiles ~1700
5. turno com 2+ cidades vazias         as duas no Jornal
6. hot seat DEMORANDO na cortina       o Jornal abre inteiro
7. menu > resumo do turno              o botao movido abre o Jornal
9. linha [AI Perf][Unit] do APC #31    nunca chegou
12. Suprimentos #24 e #73 buscando artilharia
```

**Protocolo de névoa:** com a partida em Play, ninguém salva `.cs` — recompile
religa `debugFogOfWarEnabled = true` e parece conserto.

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta o de cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ faltam quatro (§ Buracos estruturais)
 3. papéis → só POLÍTICA          ⚠️ as SEIS fichas escritas; RoleData não existe
 4. variações de papel            perfil/trait depois da extração
```

O degrau 3 tem **vocabulário completo e zero código**. As seis fichas descrevem a
forma; `RoleData`/`RoleDatabase` (o `ScriptableObject` que o autor desenhou) não
existe, e nenhuma das ~20 exceções do capturador foi re-derivada ainda.

**O teste do degrau 3** — e ele ainda não pode ser feito:

> Cada exceção nomeada (*ponta de lança, handover, sai do meu prédio, ceder para
> o capturador x*) ou se re-deriva de `(papel, modalidade, banda, âncora)`, ou
> vira **política declarada** em `Match/AI/Service/Capture_Policy`. O que não for
> nem uma nem outra é resíduo.

---

## Regras de trabalho

- **Uma classe por vez.** Compilar e rodar no jogo antes da próxima fase.
- **Avaliar não é executar.** Plano pedido não autoriza implementação.
- **Verificar antes de documentar.** E **checar um leitor não prova onde o dado
  mora**; e **listar arquivo por nome pareia errado** — o que decide é a pergunta
  que o consumidor responde, e ela está na docstring.
- **Doutrina em `docs/AI Behavior/`; comportamento do código ao lado do código.**
- **Verso não é lugar de hipótese** — a Marcha vale como especificação.
- **Nada provisório publica verdade confirmada — e apagar é publicar.**
- **Tem relatório, tem tag. Não tem relatório, é só commit.**
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Não classificar arquivo do autor como churn sem perguntar.**
- **Não salvar `.cs` enquanto o autor testa em Play.**
- **Nada foi distribuído** — save e bake podem mudar de forma. Não propor shim.
- **Um commit por frente de trabalho.**
- `dotnet build Assembly-CSharp.csproj -v q --nologo` — o Editor é outro assembly.
  Arquivo `.cs` novo não entra no `.csproj` até a Unity regerar.
- Fechar o dia: skill `.claude/skills/fechamento-do-dia/SKILL.md`.

---

## Armadilhas que já custaram tempo

| armadilha | lição |
|---|---|
| **compilar não prova que o arquivo mudou** | um script que aborta antes de gravar deixa a árvore idêntica, e o `git commit` passa se outro arquivo mudou junto. Aconteceu: um commit meu descrevia trabalho que não existia. Conferir o alvo, não só o build |
| **ferramenta que discorda do jogo é pior que ferramenta faltando** | duas vezes num dia: a bancada não passava `allowTransporterCell` nem `maxRemainingRouteCost`, e aprovava LZ que o runtime recusa. A resposta errada parece legítima. Se a pergunta é a mesma, o código tem que ser o mesmo — `TryResolveDeliveryZoneAnchor` virou estática por isso |
| **conclusão sem abrir o que existe** | quatro vezes hoje: disse que o Tactical não era skip (estava no organizador), que nada valorizava subir a montanha (a ferramenta dá +15,6), que o APC não sobe (tem `OFF Road` no asset), e que o Strategic cúbico era defeito (é projeto). **Antes de afirmar ausência: abrir a ferramenta, o asset, e a camada de cima** |
| **conferir coerência não é conferir correção** | carimbei um verso da Marcha contra `Vigilancia.md` §5 — e a §5 era justamente a cláusula que estava no **documento errado**. Os dois erros se cancelaram num ✅. Quando a referência está torta, bater com ela é **sintoma**, não prova. O ✅ só vale se o doc de referência também já foi conferido |
| **descompasso de generalidade É a evidência** | o verso dizia *"**SE** eu sou furtivo"* (dois ramos); a cláusula dizia *"unidades furtivas **AÉREAS**"* (um). Estreitei o verso **duas vezes seguidas** para caber. Quando o texto novo cobre **mais** casos que a regra contra a qual se confere, **a regra é que está incompleta** |
| **causa escrita depois dos efeitos** | documentei repulsa e ledger como duas decisões lado a lado; as duas são consequência da **detecção ser total**, fato declarado depois. Quando dois fatos aparecem juntos e um parece explicar o outro, desconfie de que **falta o terceiro** |
| **listar arquivo por nome pareia errado** | montei a matriz `Pode*`→`Melhor*` por `find -name` e errei **quatro** linhas. Bastou abrir duas docstrings: *"uma combinação passageiro-**LZ**"* e *"usa a consulta prospectiva do **PodeTransferir**"*. O que decide o par é a **pergunta que o consumidor responde** |
| **checar um leitor não prova onde o dado mora** | afirmei que o `ConstructionDatabase` estava limpo porque o builder de topologia itera instâncias da cena. Havia uma seção `Field Entries (Map Scope)` que eu não procurei |
| **doutrina no doc de implementação** | escrevi o lema no `Capturer.md` (ao lado do código) sem checar que existia `docs/AI Behavior/Capturador.md`. Doutrina e comportamento têm casas diferentes, e a fronteira precisa estar escrita nos dois |
| **verso não é lugar de hipótese** | a Marcha vale como *"onde o código divergir de um verso, o código está errado"*. Um verso sobre unidade inexistente esvazia a regra para todos os outros. Regra sobre peça que existe entra; peça que não existe, não |
| **`default` de enum não é "nenhum"** | `default(ConstructionSector)` é **Alpha**. `!= default` como "tem vizinho" apagou Alpha do grafo |
| **`enumValueIndex` não é o índice de `Enum.GetValues`** | o popup mostrava o rótulo certo e a cena gravava o setor vizinho. O contrato de serialização é o **valor** |
| **layout de mapa em asset de catálogo** | o modo de falha é **silêncio**: só grita quando as coordenadas não existem no outro tabuleiro |
| **lookup que mistura fontes devolve lista temporária** | a migração copiou 23 rotas para um objeto que ninguém serializa. Escrita procura o **bucket serializado**, nunca o lookup |
| **migração que não é idempotente** | rodar de novo empilhou: 23 → 46. Migração **substitui**, e **confere o próprio resultado** |
| **auto-assign por "o primeiro que aparecer"** | `FindObjectsSortMode.None` é ordem arbitrária. Desempate explícito, e **avisar** quando não há critério |
| **`.gitignore` não desfaz o que já está no índice** | `Assets/_Recovery` tinha 28 arquivos rastreados; ignorar só barra o que é novo |
| **o limiar de SAÍDA é onde moram os turnos parados** | `repairTriggerHpBelow = 0` não solta ninguém: quem prende é `repairRecoverHpAbove = 8`. Os dois descem juntos |
| **campo global usado como decisão local** | ia passar `EnableLos = false` na vigilância; aquele campo é o **toggle da partida** |
| **memória onde o fato é observável** | o Fire Support lembra porque o passageiro embarcado não se vê da posição; contato na rede se vê toda rodada |
| **política única para famílias opostas** | aérea repele; naval não. Bifurcar por **família** antes de por postura |
| **onde eu ponho o teste erra mais que o que o teste faz** | a consulta cara antes do filtro barato, duas vezes na mesma sessão |
| **hash que se invalida pela própria contabilidade** | o `captureClaimStateHash` dobrava o `HasActed` das 66 unidades |
| **hit e miss de cache logam igual** | não dá para auditar cache pelo texto; só pelo contador |
| **verdade vazia em laço de prova** | `for (...) if (achou) return;` conclui o pior quando a lista está **vazia** |
| **doc que envelhece e vira fato** | e agora os docs estão **à frente** do código em vários pontos: as marcas `HOJE/CONTRATO/ABERTO` e `✅⚠️❌❓` só funcionam se alguém as mexer quando o código alcançar |
| **recompile em Play parece conserto** | salvar `.cs` religa `debugFogOfWarEnabled = true`. A configuração que causa isso é `Preferences > General > Script Changes While Playing`, **não** o `Enter Play Mode Settings` — são duas coisas diferentes e é fácil trocar |
| **estático que sobrevive ao Stop** | com `Enter Play Mode = Do not reload Domain or Scene`, um teste de save/load pode passar porque o estático **nunca morreu**. Para testar persistência: `Reload Domain and Scene`, ou fechar a Unity |
| **posição hipotética criando conhecimento** | movimento no cálculo não permite detectar antes do compromisso |
| **foco tratado como gate** | `FocusCells` só soma pontos; admissibilidade precisa ser explícita |
| **dividir commit por hunk sem rede** | guarde o arquivo final, aplique a frente A, **restaure**, e o resto é a frente B |
| **`git add .`** | só no passo de churn do fechamento |
| **tag antes do commit final** | tag é a última coisa da versão |
| **teste que se chama "estrutural" e mede prazo** | cinco vezes na v8.1.1. Estrutural é topologia: *"algum dia, a qualquer distância"*. Orçamento de turnos é ranking, uma camada acima. Se o nome do teste promete estrutura, ele não pode ter constante de tempo dentro |
| **duas flags que deviam andar juntas e não andam** | `includeStrategic = true` com `resolveLongRangePassengerMeeting = false` fazia o tier Strategic negar o próprio horizonte. Quando um pedido tem dois eixos (o que produzir × o que calcular), conferir se algum caller os separou |
| **cinco cláusulas, um só `false`** | o `isMaterializable` do Pickup recusava 48 opções com log idêntico ao de zero opções. Adivinhei duas vezes; à segunda errei. **Contador por motivo antes do terceiro palpite** — custa nada e transforma a corrida seguinte em resposta |
| **alargar o conjunto de candidatos sem dar razão para ficar** | as cinco correções somadas fizeram o navio trocar de passageiro todo turno. Quem passa a ver mais opções precisa de **histerese** na mesma passada, senão vira otimizador global — exatamente o que já mordeu o capturador |
| **`alvo=(0,0,0)` não é "sem alvo"** | é a célula (0,0,0). Li como ausência e construí meia teoria em cima. Coordenada nula e coordenada zero são indistinguíveis no log — conferir no Inspector antes de concluir |
| **"não era o bloqueio" ≠ "não era necessário"** | classifiquei o horizonte por tier como decoração porque não destravou sozinho. A opção vencedora do navio é `ReachableStrategic` — era carga. Numa série de portas, cada uma é necessária e nenhuma é suficiente |

---

## RETOMADA — 2026-08-07

### Três mudanças compiladas, NENHUMA rodou

Todas em `Hot Seat 0 - Treino`, que agora tem **duas faixas de montanha** entre o
APC e o objetivo, e uma **LZ de grama do outro lado**. Atravessar a serra é o
desafio: o APC tem `OFF Road` e **sobe** (custo 6, MP 6 = um hex por turno).

| commit | o que muda | como saber que funcionou |
|---|---|---|
| `e75f308` | demanda de transporte do rebelde vem da **fila de carona** | tirar o APC da cena; o shopping tem que pedir `Transportador` |
| `303ce58` | `dropOffRange` do courier vira **banda do passageiro** | o log diz `range=6 (Operational; Tactical=3)` em vez de `range=4` |
| `8dc7ef1` | **missão no topo** + o APC obedece a missão | `PassengerTarget #1 MISSAO (x,y)` em vez de `capturavel proximo` |

**Rodar uma de cada vez.** As três mexem no mesmo caminho e empilhá-las esconde
qual regrediu.

### O bug que ficou aberto, e ele tem endereço

```csharp
// ProgressionSelector.cs:108
return distanceToTargetMap.TryGetValue(cell, out int routeCost)
    ? routeCost                                     // custo de ROTA
    : SectorManager.HexDistance(cell, targetCell);  // ← fallback SILENCIOSO
```

Célula fora do mapa de rota não é marcada inalcançável: recebe linha reta. Aí
`firstTurnProgress = originDistance − cellDistance` **subtrai custo de rota de
distância em hex**. Com montanha os dois divergem 3× e o sinal inverte:

```text
origem  (19,2)  fora do mapa  → hex  4
serra   (19,3)  no mapa       → rota 12
progresso = 4 − 12 = −8      (a ferramenta dá +15,6 no mesmo hex)
```

Resultado: platô de zeros, `tool = −moveCost`, e o hex mais caro — o único que
resolve — fica em último. **É o ping-pong `(19,2)↔(18,2)` do `log.md`.**

O log já dizia, em toda linha: `route=?` é `RouteFound == false`, e o `next=4,0`
ao lado é o fallback. Sem montanha os dois números coincidem e isso nunca
aparece.

### A ferramenta é a fonte, não o meu raciocínio

`Tools > Transporte > Caminhos Válidos > Progressão` calcula **a fórmula runtime
do intent** e mostra `FinalScore/1000`. Onde ela e o log discordarem, é diferença
de **entrada** — e o painel expõe origem, PM do turno, PM dos seguintes,
horizonte e intent.

⚠️ **Errei quatro vezes hoje concluindo sem abrir o que existia:** disse que
Tactical não era skip (o skip estava no organizador, não no serviço), que nada
valorizava subir a montanha (a ferramenta dá +15,6), que o APC não subia (tem
`OFF Road`, e o custo está no `Montanha.asset`), e que o Strategic cúbico era
defeito (é projeto). **Antes de afirmar que algo não existe: abrir a ferramenta,
o asset, e a camada de cima.**

---

## Critério de retomada

**Rodar as mudanças uma de cada vez.** São sete no caminho do transporte, todas
compiladas e nenhuma validada isoladamente — só o gameplay solto mostrou que o
conjunto se comporta. Os sinais de log de cada uma estão na seção Estado.

A fila curta:

```text
1. validar as sete, uma por vez, com o sinal de log de cada
2. o fallback silencioso do ProgressionSelector.cs:108   ← bug com endereço
3. Mission Intent = None quando o transportador está à toa (RidePromise)
4. a triagem de entregáveis virar consulta de runtime (hoje só na bancada)
5. abrir o capturador: quantas das ~20 exceções sobrevivem como POLÍTICA
```

### O item 2 tem evidência gravada

```csharp
// ProgressionSelector.cs:108 — célula fora do mapa de rota recebe LINHA RETA
? routeCost                                     // custo de ROTA
: SectorManager.HexDistance(cell, targetCell);  // ← fallback silencioso
```

`firstTurnProgress` acaba subtraindo custo de rota de distância em hex. Com serra
os dois divergem 3× e o sinal inverte — é o ping-pong `(19,2)↔(18,2)` do
[`docs/gamelog/log.md`](gamelog/log.md). Sem montanha os números coincidem e isso
nunca aparece.

### O modelo da missão do transportador, escrito e com uma perna faltando

```text
Transport + carga a bordo      Delivery   alvo = objetivo da carga     ✅
Transport + vazio + promessa   Pickup     alvo = o passageiro          ✅
None                           nada a fazer                            ❌
```

Delivery × Pickup é **derivável** da carga — não precisa de valor novo no enum. O
buraco é o terceiro: vazio, sem promessa, com missão velha pendurada. O navio
leria um encontro que já não existe, que é pior que ler nada.
