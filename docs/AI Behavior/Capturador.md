# Capturador — doutrina

Doutrina definida pelo autor. Onde o código divergir dela, o código está errado.

Cada regra abaixo está marcada com o estado verificado no código:

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |

---

## 0. O lema — tudo abaixo deriva daqui

> ## O capturador adianta a renda do exército.
> ## Nenhum prédio é dele, e o HP é o relógio.

> *"O capturador é a mosca atraída pela luz roxa. Ele não consegue evitar."*

Definido pelo autor em 2026-08-06. As ~20 exceções espalhadas em
`AIController.Capturer*.cs` não são vinte regras: são vinte expressões de **um**
objetivo, descobertas separadamente e escritas como `if`.

Cada cláusula gera uma família inteira:

| cláusula | o que ela produz |
|---|---|
| **adianta a renda** | pressa; carona quando o alvo está no Operacional; desembarque no Tático do alvo. Renda antecipada **compra** a próxima captura — compõe, não só soma |
| **nenhum prédio é dele** | handoff, swap, ceder ao oportunista, ceder hex alheio, não estorvar. **Cinco das seis formas de ceder** caem daqui |
| **o HP é o relógio** | `GetCapturePower` devolve HP: HP não é vida, é **velocidade**. Daí evitar luta, capturar com HP cheio, e fundir |

**As seis formas de ceder são o contrapeso da compulsão.** Se a atração não fosse
irresistível, não seriam precisas seis regras mandando a peça sair de cima da
luz. As exceções não contradizem o lema — existem porque ele é obedecido demais.

### A postura não muda o objetivo, muda qual termo domina

| postura | o capturador | o termo |
|---|---|---|
| Ofensiva | captura | renda **adicionada** |
| Defensiva | fica em cima do prédio conquistado | renda **protegida** |
| Collapsing | arrisca sair se o time segura | renda futura > risco |
| sempre | libera produtora | quem protege a renda melhor que ele |

Ele não defende **território** — defende a **linha de renda**. Se o score tiver
os dois termos, `AIStance` vira **peso**, não ramo.

❌ Nenhum termo do score de hoje fala em **turnos** nem em **renda**:
`CaptureProximityBase 500`, `DpqWeight 200`, `ThreatWeight 50`,
`AttackHexBonus 800` — proximidade, DPQ e ameaça são vocabulário de **combate**.
A IA de conquista foi escrita com a régua da IA de briga, e cada vez que a régua
não media o que importava nasceu uma exceção.

### O teste de cada exceção

> **Esta exceção adianta renda, ou existe porque a peça se achou dona?**

As que adiantam renda viram **termo do score**. As que existem por posse
**dissolvem** quando a peça para de se achar dona. O que sobrar das duas peneiras
é **gosto** — e só isso vira política em `Services/CapturePolicy/`.

```text
GOSTO (vira política)     capturador burro (Iniciante), agressivo,
                          limiar do blitz, apetite do playConservative
CONTA (vira score)        swap, handoff, fusão de eficiência,
                          ceder ao oportunista, não estorvar
```

---

## 1. Capturador Combatente é um ramo do Capturador

Não é outro papel: **segue as mesmas políticas**, muda só a prioridade e a forma
de se comportar.

✅ `UnitRole.CapturadorCombatente = 12`, e
`UnitRoleCompatibility.CanSatisfy(CapturadorCombatente, Capturador) == true` — ele
também satisfaz `Assalto`. O ramo agressivo é decidido **dentro** do fluxo do
capturador (`AIController.Capturer.cs:244`), depois da captura oportunista e
antes do avanço normal.

✅ O ramo vale **com e sem plano**. `assigned == null` é caso normal: sem
objetivo não há status de defesa, e o rótulo passa a ser a âncora. O caminho sem
plano (`Capturer.Rogue.cs`) chama o ramo agressivo antes da marcha final — mesma
posição relativa que ele ocupa no fluxo com objetivo.

✅ O teste do ramo é `UnitRoleCompatibility.CanSatisfy(data, CapturadorCombatente)`,
não `roles[0]` estrito: papel em posição secundária continua sendo o papel.
Capturador puro não dispara o ramo — `CanSatisfy(Capturador, CapturadorCombatente)`
cai no `default: return false`.

### ⚠️ REVISTO em 2026-08-06 — ele não precisa ser ramo, nem papel

Decomposto, `CapturadorCombatente` é a soma de três coisas que **já existem em
outro lugar**:

```text
chave 0.5        PodeCapturarSensor.cs:152 — o relógio dele anda pela metade
a mesma ordem    mesma lista de categorias do capturador
gancho de compra 5 dos 8 usos no código são de SHOPPING, não de comportamento
```

O comportamento sai **de graça** do terceiro passo do gate: sem tiro que renda
mais que meio relógio, `Capturar` aceita; havendo tiro, declina e a ordem segue
até `Mirar`. Dito pelo lado positivo, como o autor formulou: **sem combate no seu
Tático e sem capturador de maior cap power alcançando o Tático dele, ele captura
para quebrar o galho.** Meio relógio bate relógio nenhum.

Uma skill que "promove Mirar" seria **poder disfarçado de chave** e falharia o
teste do renome. A chave 0.5 é legítima porque **quem a lista é a construção**.

Ver `Assets/Scripts/Match/AI/3. Shopping/Shopping.md` (o gancho de compra) e
`docs/ideias_futuras.md` item 10 (o roteiro seguro de remoção).

---

## 2. Alocação de capturas

**a) Com plano:** o planner já distribuiu unidade + setor. O endereço da
construção é materializado diretamente em `MissionIntent.Capture`; essa unidade
não entra no matching do Melhor Captura.

**b) Sem plano:** o capturador entra no matching residual do Melhor Captura.
Capturador puro tem preferência sobre capturador combatente; no empate, o
agressivo cede a vez.

✅ `SortCandidates` ordena por **papel primeiro** — capturador puro antes de
agressivo — depois `InstanceId`, arestas e custo.

O corte é por **estado de planejamento**, não por facção:

```text
IA com HQ     planner publica formais → RogueUnitIds dividem o restante
IA sem HQ     não há plano            → todos dividem o restante como rogues
```

Não há caminho separado para facção sem QG. O
`CaptureOpportunityClaimService` recebe somente capturadores sem plano; os
endereços formais já publicados são retirados antes do matching e permanecem,
no máximo, como referência magnética para uma sobra.

Por que ordem de escolha basta, e distância não precisou entrar: cada candidato
já tem as arestas ordenadas por custo de rota (`CompareEdges`), então cada um
escolhe o prédio mais perto **dele**. A precedência de papel só decide quando os
dois querem o **mesmo** prédio — aí o puro leva e o agressivo cai para o próximo.
É exatamente "agressivo é backup" e "no empate, cede a vez", sem reordenar a
alocação por distância (o que mudaria também a IA com plano).

---

## 3. Política de captura

**Capturador agressivo** para de capturar e vai para a luta, e **retorna à
captura quando estiver livre**.

**Capturador** captura mesmo que as condições de luta não sejam boas.

⚠️ Hoje a ordem é a inversa para o agressivo: a captura oportunista é avaliada
**antes** do ramo agressivo (`Capturer.cs:241` retorna captura; o ramo agressivo
só é consultado depois). Ou seja, havendo captura disponível, o agressivo
captura em vez de lutar.

✅ Para o capturador puro, "captura mesmo em condição ruim" é o comportamento
atual — ele não consulta gate de combate para decidir capturar.

**RESOLUÇÃO (2026-08-06).** O ⚠️ acima não se conserta invertendo a ordem do ramo
— conserta-se no **terceiro passo do gate** do `Capturar`:

```text
1. o sensor devolveu opção?              senão → NÃO SE APLICA
2. alguém com CAP POWER maior fecha antes?  → cedo (swap)
3. vale o meu turno?                     ← a chave 0.5 morde aqui
```

O passo 3 pergunta *"há alvo no meu Tático?"* — que é **fato**, respondido pelo
`PodeMirar`. Não pergunta *"o que o Mirar decidiria"*, que acoplaria as duas
casas. O gate fica auto-contido e o questionário continua **primeira não-nula
ganha**.

---

## 4. Combate

Segue as flags do `UnitData` da unidade.

✅ `prioritizeDpqAtBattle` é honrada em Assault, Capturer, Defender, Explorer,
Pursuer, HQBreaker, Rogue e — desde a v6.1.2 — no ataque preemptivo de papel
(`TryBuildRolePreemptiveAttack`), que era o último caminho que a ignorava.

**Regra adicional:** se o capturador combatente for lutar mas o movimento o levar
para cima de um prédio capturável, ele **tenta lutar em outro lugar** — não
ocupa o capturável para brigar.

✅ `IsReservedAssaultEscortCaptureCell` é a **primeira guarda** do laço de células
em `TryFindAssaultEscortAttack` — a função que o ramo agressivo usa nas duas
chamadas. A célula é descartada quando há capturável que ainda importa:

```csharp
return construction.SlotIndex != ResolveAISlotKey(aiTeam)               // não é meu
    || construction.CurrentCapturePoints < construction.CapturePointsMax; // meu, incompleto
```

Só libera quando o prédio já é meu e está com captura cheia — aí não há o que
atrapalhar. O `continue` é literalmente "tenta lutar em outro lugar": segue
procurando outra célula.

Não depende de plano, então vale igual para unidade sem plano.

---

## 5. Fusão e reparo

Seguem as decisões de reparo. **Fundir na retaguarda, sempre.**

Ganham **iniciativa** quando estão na vanguarda, para poder recuar e tentar
reparar na retaguarda — liberando espaço para combatentes novos na frente.

⚠️ Divergente. Os grupos de iniciativa hoje (`AIController.Initiative.cs`):

```text
0 = vacater de handoff, ou blocker sobre objetivo alheio
1 = helicóptero; feridos entre si durante a invasão
2 = unidade ativa liberando corredor
3 = objetivo normal
4 = rogue / sem objetivo
5 = reparo/manutenção  ← age por ÚLTIMO
```

Ferido em reparo age por último (grupo 5), não primeiro. A exceção existente é
só durante a invasão, e ordena elites entre si — não é "vanguarda sai da frente
primeiro".

---

## 6. Embarque

Usa a Hotzone: deslocamento **Tactical** e **Operational**, e pede carona quando
está longe do que quer alcançar — **com plano e sem plano**.

✅ Implementado nas v6.1.0/v6.1.1. `QueroCaronaService` consome
`UnitReachEnvelopeService` na intenção `Capture`, nas duas bandas; a banda vem
de `TryClassify`, não de comparação de inteiro. Vale igual para unidade sem
plano — o caminho rogue/rebelde tem pergunta própria ("existe algum capturável
dentro do meu componente de movimento?").

Complementos da mesma entrega: fila de espera por antiguidade (`isStranded`,
carimbo de turno, urgência crescente) e teste de encontro transportador ×
passageiro.

### 6.1 Quando a carona ainda não pode ser materializada

Depois que `QueroCarona` responde **SIM**, falhar no scan de embarque deste turno
não devolve o capturador ao movimento magnético. Isso seria uma contradição:
`isStranded` acabou de provar que o prédio pertence a outro componente de
movimento, portanto avançar pela distância cúbica só faz a infantaria acompanhar
a margem ou apontar para dentro do canal.

O controller consulta `MelhorEmbarqueService.EvaluateForPassenger` com:

- o capturador como passageiro;
- transportador vazio, para comparar todos os aliados compatíveis;
- Strategic habilitado;
- o resultado já calculado de `QueroCarona`.

O capturador anda para `passengerMeetingCell`, que é o lado terrestre do
encontro. Ele nunca usa `lzCell` como destino próprio, pois a LZ do navio pode
ser água. Se um transportador prometeu buscá-lo, essa solução ganha preferência
dentro da mesma banda; continua sem lock, portanto uma solução Tactical de outro
casco vence uma promessa Operational ou Strategic. Se já estiver no encontro,
aguarda. Se não existir encontro materializável e ele estiver sem rota própria,
também aguarda em vez de retomar a marcha impossível.

---

## 7. Desembarque

Esperam ser largados na Hotzone **tática do objetivo em relação a eles** — como
se fossem teleportados até lá:

| papel | distância de largada aceitável |
|---|---|
| Capturador | 0 (em cima do alvo) a **3** hexes |
| Capturador Combatente | 0 (em cima do alvo) a **2** hexes |

❌ Não existe distinção por papel. O código tem `TransportDropOffRange = 4`
(entrega terrestre) e `AirDropOffRange = 2` (helicóptero — mais preciso porque
voa direto ao alvo). As duas são propriedades do **transportador**, não do
passageiro.

Nota: o `CLAUDE.md` documenta `TransportDropOffRange = 3`; o código diz **4**.
Um dos dois está errado.

---

## 8. Estoque e suprimento

Não se aplica ao papel.

✅ Nada no fluxo do capturador consulta estoque ou suprimento.

---

## 9. Sub-decisões do capturar

Governam a forma de fazer a blitzkrieg. Cada uma é um arquivo em
`Assets/Scripts/Match/AI/Units/Capturer/`:

| sub-decisão | papel |
|---|---|
| `Blitzkrieg` | ponta de lança que já iniciou captura não fica terminando o prédio; segue pelo eixo e outra infantaria fecha atrás |
| `PontaLanca` | a própria ponta do eixo |
| `Swap` | capturador mais forte do mesmo objetivo chega neste turno ⇒ o atual cede o prédio e sai do caminho |
| `Vacate` | sair do hex que outro precisa |
| `Opportunist` | capturável livre no caminho tem prioridade sobre o objetivo formal |
| `Attack` | ataque do capturador (com DPQ da ficha) |
| `Defender` | defesa de setor/prédio próprio |
| `Explorer` | descoberta |
| `Pursuer` | perseguição |
| `Rogue` | unidade **sem plano**: âncora = próximo prédio alcançável e capturável (ver §10) |
| `Agressive` | o ramo agressivo |
| `Embark*` | embarque (scan, pathing, transportador, estendido) |
| `CaptureDecisionReport` | diagnóstico da decisão |

---

## 10. Âncora do capturador rogue

**O capturador rogue não marcha mais para o QG.** Ele marcha para o **próximo
prédio alcançável e capturável**.

Isso vale para unidade sem plano de qualquer facção — com QG ou sem. O funil
único até a base adversária, atravessando o mapa e ignorando cada prédio do
caminho, acabou.

O QG inimigo continua sendo destino possível: ele é um capturável como outro
qualquer, e é escolhido quando for o mais próximo. O que morreu foi a marcha
cega até ele.

**"Alcançável" é literal:** dentro do componente de movimento próprio da
unidade. Prédio do outro lado do mar não é destino de marcha — é pedido de
carona, e quem responde isso é o Quero Carona (§6).

✅ `TryResolvePlanlessCapturerAnchor` (`Capturer.Rogue.cs`) resolve sempre pelo
capturável mais próximo, com `requireOwnMovementReach: true`. Não há mais ramo
de QG. `DecideRogueCapturerAction` recebe a âncora como parâmetro.

A escolha registra a intenção — alvo designado e reserva da passada —, porque
para quem não tem plano é essa designação que faz o papel de objetivo: é o que o
Quero Carona e o transporte leem para saber para onde a unidade quer ir.

Sem capturável alcançável a pé, a unidade cai no fluxo normal: carona, ou
`HexEvaluator`.

**Nota de escopo:** o filtro de alcance é opcional em
`FindNearestPlanlessCaptureTarget` e só é ligado por quem vai **marchar**. O
transporte não liga: ele pergunta por um alvo para largar o passageiro, e
passageiro embarcado tem o componente do veículo — num navio, água — que
reprovaria todo prédio em terra.

---

## 11. Destino de unidade sem plano — coerência (C8)

Três problemas do mesmo tronco: **o código pergunta "a facção tem QG?" onde a
pergunta certa é "esta unidade tem plano?"**.

### 11.1 O gate errado

`Courier.Passengers`, `Courier.Invasion` (dois pontos) e `Naval` decidem o
destino do passageiro com `ConstructionManager.IsHeadQuarterlessTeam(team)`.
Resultado: passageiro **sem plano de uma IA com QG** continua sendo entregue no
setor do QG inimigo —

```csharp
// Rogue capturer — no plan slot. Head to the HQ sector and drop at the nearest capturable.
if (target == Vector3Int.zero && snapshot.EnemyHQ != null)
    tgt = FindCapturableInSector(snapshot.EnemyHQ.Sector, ...);
```

— que é exatamente o funil abolido no §10, sobrevivendo dentro do transporte.

### 11.2 Unidade que começa embarcada

Cenário raro, quase sempre de mapa montado à mão: a unidade nunca passa pelo
próprio caminho de decisão, então quem escolhe o destino dela é **o
transportador**. Para facção sem QG isso já funciona; para rogue de IA com QG,
cai no funil de 11.1.

### 11.3 O efeito praia

Dois capturadores desembarcam, há **um** capturável por perto. A alocação 1:1 dá
o prédio a um deles. O outro consulta o Quero Carona, não encontra capturável
livre nas duas bandas — porque o único perto está reservado — e **aceita
carona**: volta para o barco enquanto o colega anda três hexes.

Isso não é hipótese; está nos logs de teste:

```text
pax=#15 ... sem prédio capturável livre alcançável em Tactical ou Operational;
2 oportunidade(s) reservada(s) 1:1 para outro capturador (ex.: #72): aceita carona.
```

**A distinção que falta:**

| situação | leitura correta |
|---|---|
| não há capturável livre alcançável | pedido de carona legítimo |
| há capturável, mas **reservado 1:1 para outro** | *não* é motivo de carona — marche para o próximo |

O dado já existe e já é contado: `QueroCaronaResult.captureClaimsBlocked` e
`captureClaimOwnerUnitId`. Hoje ele só decora a mensagem; deveria mudar a
decisão.

---

## Pendências

Lista viva. `§` aponta a seção deste documento; itens sem `§` são de outras
frentes que tocam o capturador.

### Doutrina do capturador

| # | o que falta | onde | tamanho |
|---|---|---|---|
| **C4** | agressivo **larga a captura para lutar** — hoje a captura oportunista é avaliada antes do ramo agressivo (§3) | `Capturer.cs:241-244` | M |
| **C6** | ferido na vanguarda **ganha iniciativa** para recuar e liberar espaço; hoje reparo age por último, grupo 5 (§5) | `Initiative.cs` | M |
| **C7** | zona de largada = **banda da unidade**, não hex fixo (§7) | `MelhorDesembarque`, `Courier`, `Assigned` | M |
| **C8** | destino de unidade **sem plano**: gate por facção, efeito praia, embarcado (§11) | `Courier.Passengers`, `Courier.Invasion` ×2, `Naval`, `QueroCarona` | M |
| **C9** | transporte **não pousa em capturável**; se for inevitável, sobe na iniciativa para sair | LZ + `Initiative.cs` | M |

### Herança do refactor do sem-plano

| # | o que falta | onde | tamanho |
|---|---|---|---|
| **R1** | tirar nomes de rebelde dos auxiliares que são gerais (`IsRebelCapturable` e cia.) | `Rebel.cs` → capturador | P |
| **R2** | `IsOtherAssignedCapturerTarget` valer **sem plano** — hoje devolve `false` e a regra some | `Capturer.cs:514` | P |
| **R3** | guardas de célula no `TryBuildRolePreemptiveAttack` (produção própria, capturável alheio) | `Router.cs` | P |

### Transporte, que o capturador consome

| # | o que falta | onde | tamanho |
|---|---|---|---|
| **T1** | transporte com vaga livre volta a coletar — ⚠️ **sozinho piora a fome** | `BuildAttempts` (o `return`) | PP |
| **T2** | promessa reserva **uma vaga**, não o veículo | `MelhorEmbarque` / slots | M |
| **T3** | espera vira **pressão de compra** de transporte | `AIShoppingPlanner` + demanda | M |
| **T4** | `IsAlreadyFormalPassenger` sai; a promessa assume a exclusividade | `TransportOperations` | P |

### Fora do papel, mas afetam a decisão

| # | o que falta | onde | tamanho |
|---|---|---|---|
| **A1** | infantaria trocando 5 HP por 2 contra helicóptero: RPS −2 devia apertar o limite de perda | `PassesAttackDecision` | M |
| **D2** | varredura de conferência do `CLAUDE.md` contra o código | `CLAUDE.md` | M |
| **L1** | fome da artilharia | `docs/implementar_logistica.md` | — |
| **E1** | Fases 2, 4 e 5 da migração do envelope | plano original | G |

### Cascatas registradas

- **T1 nunca sozinho.** Encher a vaga deixa o veículo ocupado mais tempo e
  aumenta a espera de quem ficou. Entra junto com T2 ou T3.
- **C6 por último dos médios.** Mexer na ordem de iniciativa altera todas as
  unidades da Fase 2 de uma vez e contamina qualquer teste em paralelo.
- **C7 começa pela `ShuttlePickupRange`.** Ela já é `MP + folga` em todos os
  chamadores — é o caso mais barato para provar o padrão antes das duas
  constantes de largada.

### Fechados

C1 (precedência capturador > agressivo, com e sem plano) · C2 (ramo agressivo
sem plano) · C3 (`CanSatisfy` no lugar de `roles[0]`) · C5 (não brigar em cima de
capturável — já existia) · D1 (constantes erradas no `CLAUDE.md`) · âncora do
rogue (§10) · `prioritizeDpqAtBattle` no ataque preemptivo.

---

# Apêndice — Marcha do Capturador

Escrita pelo autor em 2026-08-06, no dia em que o lema apareceu. **Ela é a
doutrina**, e cobre **oito** das dez casas do questionário.

`Suprir` e `Transferir` ficam de fora **de propósito**: as peças que os usariam —
field medic, engenheiro — não existem. **Verso não é lugar de hipótese.** Se a
marcha vale como *"onde o código divergir de um verso, o código está errado"*, um
verso sobre unidade inexistente esvazia a regra para todos os outros.

A estrofe já escrita para elas está guardada em `docs/ideias_futuras.md`
(item 11), e entra aqui no dia em que as peças existirem.

A linha divisória: **regra decidida sobre peça que existe** entra, com a
divergência marcada na tabela acima; **peça que não existe** não entra.

Vale a mesma regra do cabeçalho deste documento: **onde o código divergir de um
verso, o código está errado.** Três trechos já divergem hoje, e os três estão
marcados como `MUDA REGRA` no corpo do doc e no `Shopping.md`:

| verso | o que o código faz hoje |
|---|---|
| *"Se o prédio é de outro eixo / não espero o responsável"* | `IsOtherAssignedCapturerTarget` barra alvo alheio **incondicionalmente** |
| *"Não disputa a cidade com quem fecha primeiro"* | `FindSwapIncomingCapturer` compara **HP cru**, não cap power |
| *"não guardo uma bandeira, eu guardo a produção"* | `Demand.cs:3092` nega o bônus de Collapsing ao capturador, *"porque é expansão"* |

---

## Marcha do Capturador

**[Introdução — caixa clara, metais e coro]**

Um, dois! / Passo ligeiro! / Antes do tiro, / vem o dinheiro!

Um, dois! / Sem se atrasar! / Prédio parado / tem que começar!

**[Primeira estrofe]**

Não quero medalha, / não quero brasão, / eu quero a cidade / rendendo ao batalhão.

Não guardo conquista, / não planto bandeira; / se outro termina, / eu sigo a carreira.

O mapa me chama, / o eixo conduz; / sou mosca marchando / na direção da luz.

**[Refrão]**

Avança! Captura! / Não deixa esperar! / Dinheiro mais cedo / faz outro avançar!

A renda não dorme, / o turno não para; / um prédio tomado / amanhã compra a tropa!

Avança! Captura! / O caixa é o tambor! / Se a renda começa, / o exército é maior!

**[Segunda estrofe — transporte]**

Se o alvo está longe, / eu chamo o transporte; / não gasto marchando / o tempo da sorte.

No Operacional, / já mando buscar; / no Tático eu desço, / prontinho para entrar.

Se comecei a obra / e a frente está vazia, / deixo alguém fechando / e avanço a economia.

Nenhum prédio é meu, / não existe vaidade: / o dono é o exército, / meu dever é velocidade.

**[Terceira estrofe — névoa]**

Mas se a cidade / se esconde no breu, / não entro às cegas / como se já fosse meu.

Chamo olhos à frente / para o setor revelar: / quem chega sem ver / perde o turno de capturar.

Se a estrada está livre, / mas não posso avançar, / há presença escondida / que alguém deve encontrar.

Detecta o bloqueio, / faz a névoa ceder; / um olho abre o caminho, / e o caixa faz render.

**[Ponte — HP e fusão]**

Meu HP é relógio, / cada baixa é demora; / quem perde seus homens / perde renda lá fora.

Dois relógios cansados, / sem trabalho a cumprir, / viram um só bem forte, / pronto para seguir.

Mas se há dois destinos, / cada qual toma um chão: / não se funde trabalho / que trabalha em divisão!

Dois prédios, duas tropas! / Um prédio, união! / Sem prédio, força inteira / para a próxima missão!

**[Quarta estrofe — cessão]**

Se outro fecha antes, / eu cedo o lugar; / não importa o meu nome, / importa arrecadar.

Se alguém vem mais forte, / assume a construção; / eu libero o caminho / e procuro outra missão.

Se o prédio é de outro eixo / e eu posso completar, / não espero o responsável: / faço a renda começar.

Não existe posse / no serviço financeiro; / quem conclui primeiro / serve melhor o dinheiro.

**[Quinta estrofe — defesa e cerco]**

Quando a frente vacila, / eu permaneço no chão; / não guardo uma bandeira, / eu guardo a produção.

O prédio não é meu, / mas a renda é do batalhão; / se o inimigo o retoma, / fecha-se a arrecadação.

Se o exército sustenta, / posso ainda avançar: / a renda que vem depois / vale o risco de deixar.

Mas se ninguém me cobre, / não abandono o lugar: / dinheiro protegido / também é adiantar.

**[Sexta estrofe — Capturador Combatente]**

E vem o agressivo / com a chave no bornal; / seu relógio anda lento, / mas seu tiro é fatal.

Quando existe combate, / ele limpa o caminho; / quando ninguém captura, / ele toma o predinho.

Não disputa a cidade / com quem fecha primeiro; / é soldado de assalto / com instinto financeiro.

Se não há tiro útil / nem relógio melhor, / meia renda ainda vence / uma renda igual a zero.

**[Marcha intermediária — chamada e resposta]**

— Quem toma a cidade? / — Quem termina primeiro!

— Quem guarda o edifício? / — Quem protege o dinheiro!

— De quem é a conquista? / — Do exército inteiro!

— E o HP, o que marca? / — O tempo do ponteiro!

**[Refrão final — coro completo]**

Avança! Captura! / Não deixa esperar! / Dinheiro mais cedo / faz outro avançar!

A renda não dorme, / o turno não para; / um prédio tomado / amanhã compra a tropa!

Avança! Captura! / O caixa é o tambor! / Se a renda começa, / o exército é maior!

Protege! Captura! / Recua ou vai além! / O prédio não é nosso, / mas o rendimento vem!

Avança! Captura! / Sem nunca se deter! / O mapa vira dinheiro, / e o dinheiro vira poder!

**[Coda]**

— De quem é o prédio? / — Do exército!

— O que vale o HP? / — O nosso tempo!

— E qual é a missão? / — Fazer render!

Um, dois! / Sem se deter!

Um, dois! / Capturar e crescer!

Money, money! / Passo certeiro!

A luz está acesa — / avança, Capturador!
