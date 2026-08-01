# Capturador — doutrina

Doutrina definida pelo autor. Onde o código divergir dela, o código está errado.

Cada regra abaixo está marcada com o estado verificado no código:

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |

---

## 1. Capturador Agressivo é um ramo do Capturador

Não é outro papel: **segue as mesmas políticas**, muda só a prioridade e a forma
de se comportar.

✅ `UnitRole.CapturadorAgressivo = 12`, e
`UnitRoleCompatibility.CanSatisfy(CapturadorAgressivo, Capturador) == true` — ele
também satisfaz `Assalto`. O ramo agressivo é decidido **dentro** do fluxo do
capturador (`AIController.Capturer.cs:244`), depois da captura oportunista e
antes do avanço normal.

✅ O ramo vale **com e sem plano**. `assigned == null` é caso normal: sem
objetivo não há status de defesa, e o rótulo passa a ser a âncora. O caminho sem
plano (`Capturer.Rogue.cs`) chama o ramo agressivo antes da marcha final — mesma
posição relativa que ele ocupa no fluxo com objetivo.

✅ O teste do ramo é `UnitRoleCompatibility.CanSatisfy(data, CapturadorAgressivo)`,
não `roles[0]` estrito: papel em posição secundária continua sendo o papel.
Capturador puro não dispara o ramo — `CanSatisfy(Capturador, CapturadorAgressivo)`
cai no `default: return false`.

---

## 2. Alocação de capturas

**a) Com plano:** capturador tem preferência; capturador agressivo é **backup**.
Distância exatamente igual ao objetivo ⇒ **o agressivo cede a vez**.

**b) Sem plano (atribuição rebelde):** capturador tem preferência sobre
capturador agressivo. Distância exatamente igual ⇒ **o agressivo cede a vez**.

✅ `SortCandidates` ordena por **papel primeiro** — capturador puro antes de
agressivo — depois `InstanceId`, arestas e custo.

Vale para os dois casos pela mesma linha: o `CaptureOpportunityClaimService`
separa candidatos em lista **formal** (com plano) e **rogue** (sem plano), e as
duas passam pela mesma ordenação. Não há caminho separado para facção sem QG.

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

---

## 4. Combate

Segue as flags do `UnitData` da unidade.

✅ `prioritizeDpqAtBattle` é honrada em Assault, Capturer, Defender, Explorer,
Pursuer, HQBreaker, Rogue e — desde a v6.1.2 — no ataque preemptivo de papel
(`TryBuildRolePreemptiveAttack`), que era o último caminho que a ignorava.

**Regra adicional:** se o capturador agressivo for lutar mas o movimento o levar
para cima de um prédio capturável, ele **tenta lutar em outro lugar** — não
ocupa o capturável para brigar.

⚠️ Não verificado em `TryFindAssaultEscortAttack`. Existe guarda equivalente em
Assault e Capturer (`IsOtherAssignedCapturerTarget`, produção própria), mas ela
protege o capturável **de outro capturador designado** e devolve `false` quando
não há plano — não é a mesma regra.

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

---

## 7. Desembarque

Esperam ser largados na Hotzone **tática do objetivo em relação a eles** — como
se fossem teleportados até lá:

| papel | distância de largada aceitável |
|---|---|
| Capturador | 0 (em cima do alvo) a **3** hexes |
| Capturador Agressivo | 0 (em cima do alvo) a **2** hexes |

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

## Resumo do que falta

| # | regra | estado |
|---|---|---|
| 3 | agressivo larga captura para lutar | ⚠️ ordem invertida hoje |
| 11 | destino de unidade sem plano (gate por facção, praia, embarcado) | ❌ |
| 4 | agressivo não briga em cima de capturável | ⚠️ não verificado |
| 5 | ferido na vanguarda ganha iniciativa para recuar | ⚠️ age por último |
| 7 | distância de largada por papel (3 / 2) | ❌ |
