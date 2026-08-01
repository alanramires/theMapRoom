# Transporte — doutrina

Doutrina definida pelo autor. Onde o código divergir dela, o código está errado.

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |

---

## 1. Onde largar o passageiro (`TransportDropOffRange`)

A área de largada **não é um raio em hexes a partir do transportador**. Ela é
derivada por **análise reversa**, a partir do passageiro:

> Teleporta a unidade para cima do objetivo, calcula o **Tactical dela** a partir
> dali, e é essa a área. O passageiro pode ser largado em qualquer célula dentro
> dela — em cima do objetivo ou nas adjacências.

O que isso quer dizer na prática: a largada é boa quando o passageiro, no turno
seguinte, **fecha o objetivo com o próprio movimento**. Um obus de 2 MP e uma
infantaria de 3 MP não aceitam a mesma largada, porque o Tactical delas não é o
mesmo — e é a unidade que define a área, não o veículo.

Isso também explica por que "0" é válido: em cima do alvo é o melhor caso, não
uma exceção.

⚠️ Hoje é constante fixa e propriedade do **veículo**, não do passageiro:

```csharp
TransportDropOffRange   = 4   // entrega terrestre
FireSupportDropOffRange = 3   // artilharia
AirDropOffRange         = 2   // helicóptero voa direto ao alvo
```

Existe uma peça no caminho certo: `BuildPassengerRouteLimits(passengers, turns)`
no `MelhorDesembarque` limita a rota **por passageiro**, em turnos. E há um
comentário no mesmo arquivo registrando que o `TransportDropOffRange` *"era uma
regra de entrega pingada"* que eliminava o segundo passageiro de uma entrega
conjunta mesmo quando a melhor LZ era alcançável no mesmo turno.

O conserto natural é o envelope: `UnitReachEnvelopeService` na banda `Tactical`
do **passageiro**, calculado a partir da célula do objetivo. É a mesma consulta
que o Quero Carona já faz, invertida.

Ver `docs/AI Behavior/Capturador.md` §7 para os limites por papel que o autor
definiu (capturador 0-3, agressivo 0-2) — eles são política **sobre** essa área,
não substitutos dela.

---

## 2. Transporte não pousa em prédio capturável

O veículo **nunca** larga âncora em cima de um capturável. Um transporte parado
sobre o prédio ocupa a célula que o capturador precisa e, pior, trava a captura
que ele mesmo veio viabilizar.

Se não houver jeito — sem célula alternativa —, ele **sobe na iniciativa** para
sair de lá o mais rápido possível na rodada seguinte.

❌ Nenhuma das duas existe. A iniciativa hoje tem a regra espelhada, mas só para
o outro lado: grupo 0 inclui *"blocker sobre o objetivo de captura de outro
capturador"* — e o teste exige que o bloqueador **satisfaça o papel Capturador**
(`CanSatisfy(targetData, UnitRole.Capturador)`), então um transportador parado
em cima do prédio não é reconhecido como bloqueador.

Duas frentes, então:

1. **evitar** — a escolha de LZ precisa rejeitar célula com capturável;
2. **sair** — quando for inevitável, o transportador entra no grupo de
   iniciativa que age primeiro, para liberar o hex antes que o capturador
   precise dele.

> Marcado pelo autor como "a gente vai chegar lá ainda".

---

## 2-PA. O Porta-Aviões é deste papel

Papel primário **Transportador**, com três pernas que ele **consulta**:

| perna | o quê |
|---|---|
| Fire Support | duas armas antiaéreas de longo alcance — tenta o tiro, volta à agenda |
| Logística | suporte |
| Transferência | é **Hub**: recebe do porto naval e repassa ao avião-tanque (perna no controlador de Estoque) |

Ele segue a agenda **courier/pickup** deste documento. Não herda a doutrina de
retaguarda, iniciativa alta nem recusa de embarque do `FireSupport.md`: aquilo é
de quem tem Fire Support como papel primário.

Mesmo mecanismo do Artilheiro Combatente, com os lados trocados — *"tento o
tiro; se não der, volto para a minha agenda"*. A diferença é qual agenda espera
a bola de volta.

❌ A perna de Fire Support do transportador não existe no roteador: hoje o
transporte é consultado antes dos papéis de combate e não volta.

### Quando o Porta-Aviões atira — rascunho do autor

> Anotado como veio, ainda sem lapidar. Fica aqui porque é política **do
> transportador**, e este é o documento dele.

| estado | comportamento |
|---|---|
| **vazio** | atira contra aeronaves no seu raio **Tático** |
| **com carga aérea** | atira **apenas contra ataque aéreo** (bombardeiros), porque deles não dá para fugir — são mais rápidos |
| **com carga aérea, sob caças** | **ergue os caças** — reconhecidamente tarde demais |

A lógica por trás: com o convés cheio, gastar o tiro em caça é desperdício
(caça se evita manobrando), enquanto bombardeiro é ameaça inescapável. E a
resposta contra caça não é a arma do navio, é o próprio convés.

"Erguer os caças tarde demais" é a admissão de que a decisão de decolar deveria
ter acontecido antes — provavelmente ao **detectar** o caça, não ao ser atacado
por ele.

---

## 3. Esteira (implementado nas v6.1.0/v6.1.1)

A doutrina completa está no `docs/relatorio_v6.1.1.md`. Resumo:

| regra | estado |
|---|---|
| quem embarca primeiro dita a rota (FIFO por turno de embarque) | ✅ |
| larga esse passageiro no Tactical do objetivo **dele** | ✅ |
| o próximo da fila vira referência | ✅ |
| fila de espera por antiguidade (urgência cresce, teto na emergência) | ✅ |
| transportador que não alcança o passageiro não promete | ✅ |
| promessa persistente no `AIDesignatedMission` do transportador | ✅ |
| encher a vaga livre no caminho | ❌ (`BuildAttempts` retorna cedo com carga) |
| promessa reserva **uma vaga**, não o veículo | ❌ |
| espera vira pressão de compra de mais transporte | ❌ |

⚠️ **Cascata registrada:** implementar "encher a vaga livre" sozinho **piora** a
fome do passageiro esquecido — o veículo passa a ficar ocupado mais tempo. Só
entra junto com a reserva de vaga ou com a pressão de compra.

---

## 4. Para onde levar — as quatro combinações

Levantado com o autor em 2026-08-01. Duas perguntas independentes decidem o
alvo de cada passageiro, e elas se cruzam em quatro casos:

| | **sem atribuição** | **com atribuição** |
|---|---|---|
| **passageiro rogue** | alvo = capturável livre mais próximo | alvo = a coordenada da atribuição |
| **passageiro com plano** | alvo = o capturável do setor do slot dele | alvo = a coordenada da atribuição (pode ser outro prédio do plano, não só o do slot) |

Em todos os quatro, o transportador faz a **mesma** coisa depois de saber o
alvo: chama o Melhor LZ de Desembarque para achar onde largar. A atribuição
não muda o mecanismo, muda só a âncora.

**Atribuição** é o `AIDesignatedMission*` do `AI Plan Runtime` no `UnitManager`.
Ela resolve **unidade antes de célula**: com
`AIDesignatedMissionTargetUnitInstanceId` válido o alvo persegue a unidade, e só
sem ela é que cai em `AIDesignatedMissionTargetCell`. Uma atribuição pode,
portanto, mirar um alvo móvel.

Quem atribui é a IA de alocação. Espera-se dela que não mande dois passageiros
para o mesmo prédio — o desembarque não conserta atribuição ruim, só a executa.

### Tático ou operacional

O transportador pode largar o passageiro no **Tactical** ou no **Operational**
dele em relação ao objetivo. Verde é largada boa (o passageiro fecha no mesmo
turno); azul é largada degradada (fecha no turno seguinte), e existe para o caso
raro de o objetivo estar cercado — montanha, ilha, LZ ocupada. Ver a projeção
invertida em `docs/contrato_envelope_alcance.md`.

### Dois passageiros

| regra | estado |
|---|---|
| tenta o principal primeiro, Tactical **ou** Operational | ✅ |
| carona só entra se houver LZ que atenda **ambos** | ✅ (`SearchMatching` maximiza entregues, depois minimiza rota) |
| desembarcado o principal, o carona vira principal | ✅ — emergente: o principal é recalculado a cada decisão a partir de quem ainda está a bordo |
| **carona aceita só o Tactical**; o Operational é privilégio do principal | ❌ regra nova |

O último item é doutrina que ainda não existe. Hoje `BuildPassengerRouteLimits`
aplica o mesmo teto a todos (1× alcance no passe tático, 2× no operacional): o
tier é do **passe de avaliação**, não do papel do passageiro. O que hoje separa
principal de carona é outra coisa — `ApplyOperationalDisembarkCapacity`: dois
passageiros mirando o **mesmo** alvo, e o alvo não estando sob pressão
confirmada, o não-principal fica a bordo.

### Pendências

| # | contrato | código hoje |
|---|---|---|
| P1 | a atribuição vale para todo passageiro | ⚠️ lida **só** no ramo rebelde — um único call site, dentro de `if (IsRuntimeRebelSnapshot)`, em `AIController.MelhorDesembarque.cs:1242`. Numa IA com QG a atribuição é ignorada no desembarque |
| P2 | rogue mira o capturável livre mais próximo | ⚠️ rogue de IA **com** QG ainda usa `TryResolveRogueCorridorCaptureTarget` — o corredor rumo ao QG, doutrina derrubada na v6.1.2/v6.1.3 e viva no transporte |
| P3 | passageiro com plano mira o capturável do setor | ⚠️ mira, mas cai no `RepresentativeCell` quando não acha (`Courier.Passengers.cs:228`) **contra o comentário logo acima** (211-213), que manda não cair: em setor já capturado o RepCell é a célula do próprio caminhão, e sai desembarque de distância zero |
| P4 | carona limitado ao Tactical | ❌ não existe |
| P5 | o Melhor LZ consome a hotzone | ❌ hoje varre as LZs por `CalcularCaminhosValidos` + `PodeDesembarcarSensor` e cruza com um mapa de custo reverso por passageiro. A **forma** já é a certa (uma pergunta por passageiro, interseção no consumidor); a fonte de alcance é que ainda não é o envelope |

P1 e P2 morrem no refactor de `docs/refactor/ai_sem_plano.md` — são a cópia que
o transporte fez do funil do QG. P3 é independente e sobrevive a ele.
