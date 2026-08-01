# Unificação de AI Capturador: Planos e Atribuição

## Versão

`v6.1.3`

## Objetivo

A `v6.1.2` devolveu o capturador sem plano ao controlador que já existia. Esta
versão fecha as consequências disso: a **âncora** de quem não tem plano, a
**precedência** entre capturador e capturador agressivo, e o ramo agressivo
passando a existir fora do plano.

Junto vieram os dois primeiros manuais de doutrina — `docs/AI Behavior/` — com
cada regra marcada pelo estado verificado no código, e não pelo que a gente
achava que estava lá.

---

## 1. O capturador rogue não marcha mais para o QG

**Regra do autor:** capturador rogue não marcha para o QG para morrer igual a
inseto em desktop tower defense. Marcha para o **próximo prédio alcançável e
capturável**.

O QG inimigo continua sendo destino possível — ele é um capturável como outro
qualquer, e é escolhido quando for o mais próximo. O que morreu foi a marcha
cega até ele, atravessando o mapa e ignorando cada prédio do caminho.

`TryResolvePlanlessCapturerAnchor` perdeu o ramo de QG e resolve sempre pelo
capturável mais próximo. **"Alcançável" é literal:** dentro do componente de
movimento próprio da unidade — prédio do outro lado do mar não é destino de
marcha, é pedido de carona, e quem responde isso é o Quero Carona.

O filtro de alcance é **opcional** e só ligado por quem vai marchar. O transporte
não liga: ele pergunta por um alvo para largar o passageiro, e passageiro
embarcado tem o componente do veículo — num navio, água — que reprovaria todo
prédio em terra e quebraria o desembarque naval.

Sem capturável alcançável a pé, a unidade cai no fluxo normal: carona, ou
`HexEvaluator`.

---

## 2. Precedência na alocação (C1)

**Regra do autor:** capturador tem preferência; capturador agressivo é backup.
Distância exatamente igual ⇒ o agressivo cede a vez. Vale com plano e sem plano.

`SortCandidates` passou a ordenar por **papel primeiro** — capturador puro antes
de agressivo — depois `InstanceId`, arestas e custo.

### Por que ordem de escolha bastou, e distância não precisou entrar

A alternativa óbvia era reordenar por custo de rota. Ela foi descartada: trocar a
chave primária mudaria o resultado do matching em **todos** os casos, inclusive
na IA com plano, que hoje funciona.

Não foi preciso, porque cada candidato já tem as arestas ordenadas por custo
(`CompareEdges`) — ou seja, **cada um escolhe o prédio mais perto dele**. A
precedência de papel só decide quando os dois querem o **mesmo** prédio: aí o
puro leva e o agressivo cai para o próximo. É exatamente "agressivo é backup" e
"no empate, cede a vez".

### E vale para os dois casos pela mesma linha

O `CaptureOpportunityClaimService` separa candidatos em lista **formal** (com
plano) e **rogue** (sem plano) — `TryResolveFormalCaptureSector` devolve false
quando não há plano — e as duas passam pela **mesma** ordenação. A facção sem QG
entra inteira na lista rogue e herda a regra sem nenhum caminho próprio.

É a primeira colheita concreta do refactor da v6.1.2: uma regra escrita uma vez,
valendo nos dois mundos.

---

## 3. O ramo agressivo fora do plano (C2, C3)

**C2 — `assigned == null` virou caso normal.** A guarda antiga exigia objetivo
atribuído, então unidade agressiva sem plano não tinha comportamento agressivo
nenhum: caía no rogue comum. Sem objetivo não há status de defesa, e o rótulo do
log passa a ser a âncora.

Remover a guarda não bastava: o ramo só era alcançado por
`DecideAssignedCapturerAction`, que exige objetivo. Ele ganhou chamador em
`DecideRogueCapturerAction`, antes da marcha final — mesma posição relativa que
ocupa no fluxo com objetivo.

**C3 — `CanSatisfy` no lugar de `roles[0]`.** O teste era papel primário estrito;
unidade com o papel em posição secundária não entrava. Agora é
`UnitRoleCompatibility.CanSatisfy(data, CapturadorAgressivo)`.

Seguro por construção: `CanSatisfy(Capturador, CapturadorAgressivo)` cai no
`default: return false`, então capturador puro não vira agressivo por acidente.

---

## 4. Manuais de doutrina

`docs/AI Behavior/Capturador.md` e `docs/AI Behavior/Transporte.md`. Cada regra
carrega o estado **verificado**:

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge |
| ❌ | não existe |

A disciplina aqui é a que já custou caro antes: nota de design só vira manual
depois de conferida no código. Metade do valor destes dois arquivos está nos
⚠️ e ❌ — foi ao escrevê-los que apareceram os itens abaixo.

### O que a verificação encontrou

**A área de largada é do passageiro, não do veículo.** A definição do autor para
`TransportDropOffRange`: teleporta a unidade para cima do objetivo, calcula o
Tactical dela dali, e essa é a zona de largada. Consequência: obus de 2 MP e
infantaria de 3 MP não aceitam a mesma largada. O código trata as duas igual,
com constante fixa do transportador (`TransportDropOffRange = 4`,
`AirDropOffRange = 2`).

Duas descobertas no caminho: já existe `BuildPassengerRouteLimits(passengers,
turns)`, que limita rota **por passageiro** — embrião da regra certa; e há um
comentário no `MelhorDesembarque` registrando que o `TransportDropOffRange` *"era
uma regra de entrega pingada"* que eliminava o segundo passageiro de uma entrega
conjunta. O sintoma já tinha aparecido e sido contornado localmente.

Nota: o `CLAUDE.md` documenta `TransportDropOffRange = 3`; o código diz **4**.

**Transporte não deve pousar em capturável.** A iniciativa tem a regra
espelhada — grupo 0 inclui "blocker sobre o objetivo de captura de outro
capturador" — mas o teste exige que o bloqueador satisfaça o papel Capturador,
então transportador parado em cima do prédio não é reconhecido como bloqueador.

**Ferido age por último (grupo 5)**, quando a doutrina pede que ganhe iniciativa
na vanguarda para recuar e liberar espaço.

---

## 5. O efeito praia (C8, não implementado)

Dois capturadores desembarcam, há **um** capturável por perto. A alocação 1:1 dá
o prédio a um. O outro consulta o Quero Carona, não encontra capturável livre nas
duas bandas — porque o único perto está reservado — e **aceita carona**: volta
para o barco enquanto o colega anda três hexes.

Não é hipótese. Está nos logs de teste desta sessão:

```text
pax=#15 ... sem prédio capturável livre alcançável em Tactical ou Operational;
2 oportunidade(s) reservada(s) 1:1 para outro capturador (ex.: #72): aceita carona.
```

A distinção que falta:

| situação | leitura correta |
|---|---|
| não há capturável livre alcançável | pedido de carona legítimo |
| há, mas **reservado 1:1 para outro** | não é motivo de carona — marche para o próximo |

O dado já existe e já é contado (`captureClaimsBlocked`,
`captureClaimOwnerUnitId`); hoje ele só decora a mensagem em vez de mudar a
decisão.

Ao mesmo item pertence o gate trocado: `Courier.Passengers`,
`Courier.Invasion` (dois pontos) e `Naval` decidem destino de passageiro com
`IsHeadQuarterlessTeam` — perguntam *"a facção tem QG?"* quando a pergunta certa
é *"esta unidade tem plano?"*. Resultado: passageiro sem plano de uma IA **com**
QG continua sendo entregue no setor do QG inimigo, que é o funil abolido no item
1 sobrevivendo dentro do transporte.

E também: **unidades que começam a partida embarcadas** (cenário montado à mão).
Elas nunca passam pelo próprio caminho de decisão — quem escolhe o destino é o
transportador —, então dependem inteiramente deste gate estar certo.

---

## Verificação

**Não executado em partida.** Esta versão muda a ordem de alocação de capturas e
o destino de toda unidade sem plano.

O que observar primeiro:

```text
[SemPlano] <id> âncora = capturável (x, y, 0) (mais próximo alcançável a pé).
[Rogue]    <id> marcha para âncora (x, y, 0) via (a, b, 0)
[CapturadorAgressivo] <id> abre caminho para âncora (x, y, 0) ...
```

A linha `marcha para HQ inimigo` não deve mais existir. E `CapturadorAgressivo`
deve aparecer em unidade sem plano, o que antes era impossível.

Para a precedência: um capturador puro e um agressivo disputando o mesmo prédio —
o puro leva, o agressivo vai para o próximo. Se o agressivo estiver bem mais
perto de **outro** prédio, ele deve pegar esse, não ficar esperando.

---

## Pendências

| # | item | estado |
|---|---|---|
| C4 | agressivo larga a captura para lutar (ordem hoje invertida) | ⚠️ |
| C5 | agressivo não briga em cima de capturável | não verificado |
| C6 | ferido na vanguarda ganha iniciativa para recuar | ⚠️ |
| C7 | zona de largada derivada do Tactical do passageiro | ❌ |
| C8 | destino de unidade sem plano: gate por facção, praia, embarcado | ❌ |
| R1 | mudar de casa `IsRebelCapturable` e cia. | mecânico |
| R2 | `IsOtherAssignedCapturerTarget` valer sem plano | ❌ |
| R3 | guardas de célula no `TryBuildRolePreemptiveAttack` | ❌ |
| T1-T4 | esteira: coleta com vaga livre, reserva de vaga, pressão de compra | ❌ |
| A1 | infantaria trocando 5 HP por 2 contra helicóptero | ⚠️ |
| L1 | fome da artilharia | `docs/implementar_logistica.md` |
