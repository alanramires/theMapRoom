# Táxi e Carona. Antes do Refactor

## Versão

`v6.1.0`

## Objetivo

Este ponto de verificação fecha a **Fase 1** da migração da IA para o envelope
unificado — captura e pedido de carona — e registra, com evidência, o estado do
sistema de transporte **antes** do refactor que vem a seguir.

O nome é literal: a IA já sabe dizer quem precisa de táxi. O que ela ainda não
sabe é despachar o táxi.

## Parte 1 — Captura e Quero Carona no envelope

### O orçamento fantasma

Cada papel da IA vinha inventando o próprio Tactical e o próprio Operational.
Nos dois serviços desta fase o modelo era o mesmo:

```csharp
operationalBudget = tacticalBudget * operationalTurns;   // MP x N num bolso só
```

Um soldado de 3 MP recebia 6 no bolso e atravessava três montanhas de custo 2.
No jogo ele entra em **uma por turno**: gasta 2, sobra 1, e a montanha seguinte
pede 2. Em duas rodadas ele anda duas montanhas, não três.

A consequência era o pior erro que este serviço pode cometer: **recusar carona
por alcance que não existe**, deixando a unidade marchar sozinha rumo a um
objetivo inalcançável.

Agora as duas classes pedem `ReachIntent.Capture` ao `UnitReachEnvelopeService`
e leem alcance, custo e origem de lá.

- `CaptureOpportunityClaimService` — um envelope por capturador elegível.
  Intocados: elegibilidade, `TryResolveFormalCaptureSector`, o matching 1:1 e a
  precedência plano-formal-vence-oportunidade.
- `QueroCaronaService` — um `UnitReachProfile` por avaliação, nas duas bandas.
  Intocados: curto-circuito de emergência, `EvaluateRepairTriggers`, reserva 1:1.

### A banda deixou de ser conta de inteiro

O teste antigo era `routeCost <= tacticalBudget`, com Operational **inferido de
estar no dicionário**. Não havia predicado de banda: havia pertinência a um
conjunto construído com o orçamento errado.

Hoje quem responde é `UnitReachProfile.TryClassify`, e o diagnóstico publica o
**turno**, não um teto:

```text
no Tactical: custo=1 no turno 1 (nesta rodada). Recusa carona.
no Operational: custo=6 no turno 2 de 2. Recusa carona.
```

O que é fixo aqui é a **banda** — duas rodadas, por definição do contrato. O
alcance dentro dela é variável e depende do terreno. Imprimir um teto de `MP × N`
ao lado do custo ressuscitava justamente o predicado removido.

### Reuso por identidade de envelope

O compartilhamento de malha entre os dois serviços era validado por igualdade
inteira de orçamento. Com o envelope isso virou **identidade**: mesma unidade,
mesma intenção, mesma banda, mesmos turnos. `QueroCaronaRequest` deixou de
carregar `IReadOnlyDictionary` + `int`.

O caminho que alimentava esse reuso vindo do `MelhorEmbarqueService` foi cortado
de propósito: a malha de lá é `MaxMovementPoints × turnos` pooled — emprestá-la
de volta era reinjetar o alcance fantasma no passageiro.

### Fome estrutural: quem não tem rota própria

O envelope tem duas bandas e para no segundo turno, por contrato. Ele não
distingue "turno 3" de "ilha". Sem essa distinção **todo pedido de carona vale o
mesmo**, e quem mais precisa perde para sempre de quem está a três hexes.

A pergunta certa é a consulta dirigida que o contrato já prevê —
`ClassifyMobility` sobre o componente de movimento sem teto. `OtherComponent` é,
literalmente, "pedido de carona".

`QueroCaronaResult.isStranded` distingue os dois casos:

| situação | score |
|---|---|
| emergência de reparo | 2000 |
| **sem rota própria até o objetivo** | **1500** |
| fora das bandas, mas chega andando | 1000 |
| alcança sozinho | 0 |

O rogue/rebelde tem pergunta própria, porque não tem um objetivo: *existe algum
capturável dentro do meu componente?* Nenhum = ilhado.

O flood fill só roda para quem **já falhou as duas bandas** — ou seja, para quem
vai pedir carona de qualquer jeito. O cache é chaveado por topologia e
identidade de movimento, deliberadamente **não** por `GlobalBoardRevision`, que
muda a cada passo de unidade e já custou caro neste projeto.

## Parte 2 — Por que o táxi não vem

Quatro defeitos, todos verificados no código e visíveis em log de partida.

### 1. A fórmula inverte a necessidade

`MelhorEmbarqueService`:

```text
score = 100000 − distânciaDoTransportador × 100
              − penalidadeDaRotaDoPassageiro
              − penalidadeDeAproximação
              + ajusteDeCarona
```

| estado da rota do passageiro | penalidade |
|---|---|
| `ReachableNow` | custo (≈1) |
| `ReachableLater` | 1000 |
| `ReachableStrategic` | 5000 |
| `NoCurrentRoute` | **10000** |

```text
continental:      100000 − 300 − 1 − 0 + 1000 = 100699
ilhado, melhor caso: 100000 − 1000 − 10000 − 0 + 1000 = 90000
```

"Não tem rota nenhuma" é a **definição** de quem precisa de carona, e custa
10000 porque é tratado como defeito da opção — coleta difícil — e não como prova
de necessidade. Querer carona paga +1000. O desespero vale um décimo do
incômodo. O ilhado precisaria que o concorrente estivesse a ~107 hexes.

### 2. A busca para antes de chegar nele

```text
Ranking encerrado no Tactical: existe passageiro Requested + ReachableNow
LZs sem passageiro ReachableNow=9; preservadas apenas no ranking plano.
```

`stopAfterDecisiveTactical` encerra a varredura no primeiro passageiro perto que
chega agora. O ilhado não perde a disputa: ele não entra nela. A única condição
que suspende a parada hoje é `hasEmergencyRideNeed`, ligada só a `IsUnderRepair`.

O mecanismo de "esse aqui é inegociável, não pare a busca" **já existe** — está
amarrado a reparo, e ilhado não é reparo.

### 3. Carga a bordo desliga a coleta

`TransportOperationsService.BuildAttempts`:

```csharp
if (caps.CanTransport && context.HasCargo)
{
    attempts.Add(Courier, Tactical);
    attempts.Add(Delivery, Operational);
    return attempts;      // <- Pickup nunca é tentado
}
```

Com a primeira vaga ocupada, o transportador para de procurar a segunda —
mesmo com `IsTransporterAtCapacity` dizendo que ainda cabe gente. É a origem do
Chinook de duas vagas voando com um soldado.

### 4. Reserva sem compromisso

`IsAlreadyFormalPassenger` bloqueia outros transportadores quando o passageiro
pertence a um objetivo com outro transportador atribuído. Mas o casamento
nomeado (`ResolveAssignedPassengerForTransporter`) só é alcançável **depois** do
portão `if (!HasTransportCargo(unit)) return null;` — ou seja, quem está vazio,
exatamente quem faria o resgate, nunca passa por ele. Vazio decide por
`MelhorEmbarque`, cujo filtro de candidatos não olha setor nem plano.

Evidência da partida:

```text
[Plan] Transportador 124 -> Golf (passenger=123)
[TransportOps] Pickup Tactical hit passageiro=#122
```

O plano prometeu o Chinook ao #123 e ele foi buscar o #122. Um terceiro APC
pularia o #123 por já ser "serviço do 124".

| | rogue ilhado | ilhado com plano |
|---|---|---|
| concorre com todo mundo | sim | sim (mesmo ranking) |
| outros podem buscar | sim | **não** — reservado |
| o reservado é obrigado a ir | — | **não** |

O plano põe uma plaquinha de "reservado" e não põe ninguém no volante.

## Parte 3 — A doutrina decidida (o refactor que vem)

A reserva é **promessa, não exclusividade do veículo**. O transporte funciona
como esteira:

1. quem embarca primeiro senta no banco da frente e **dita a rota** — já é o que
   `ResolvePrimaryPassenger` faz, FIFO por `embarkedOnTurn`;
2. o transportador larga esse passageiro no Tactical do objetivo **dele**;
3. o próximo da fila vira referência, e a esteira continua;
4. vagas livres são preenchidas no caminho — mobilidade de tropas não pode
   parar;
5. **a promessa não é esquecida**: o ilhado continua reservado;
6. e se o transporte viver ocupado, a espera **vira pressão de compra** de um
   segundo transporte, em vez de furar a fila.

O item 6 é a decisão de projeto mais importante desta versão: o ilhado não fura
fila, ele **gera demanda**. É o modelo de linha de ônibus — a parada lotada não
sequestra o veículo, ela justifica comprar outro.

Estado da esteira: os itens 1, 2 e 3 já existem no código; 4, 5 e 6 são o
refactor.

**Cascata a respeitar:** consertar o item 4 sozinho **piora** a fome. Hoje o
transportador para de coletar na primeira carga e fica livre mais cedo; enchendo
as vagas ele fica ocupado mais tempo, e a espera do ilhado aumenta. O item 4 tem
que entrar junto com 5 ou 6.

## Verificação

Fase 1 rodou em partida, com a banda e o custo publicados pelo envelope e a
cadeia fechando ponta a ponta:

```text
pax=#90 → "sem prédio capturável alcançável: aceita carona"
       → carona=Requested rotaPax=ReachableNow
       → Navio #179 aguarda na LZ (-16,-12) passageiro=#90
```

Números de turno estáveis antes e depois: `BuildObjectivePlan` 4124ms contra
4154ms, `CommitAIWorldHeavy` 74ms contra 67ms, `InitiativeSetup` 41,4ms contra
40,8ms.

Nota de leitura: `PRE-Stage2 acumulado` é relógio de parede e inclui as pausas
de debug do passo a passo. Não serve como medida de custo.

**Não observado ainda:** o diagnóstico por turno e a classificação de fome
estrutural entraram depois da última partida registrada. Nenhuma linha de
"Recusa carona" ou `SEM ROTA PRÓPRIA` foi vista em execução.

**Não exercitado ainda:** o caso da cordilheira. A janela onde o modelo antigo e
o novo divergem é estreita — vale exatamente o MP desperdiçado por turno, um hex
para 3 MP em terreno de custo 2 — e nenhuma das partidas registradas passou por
ela. O caminho barato de conferir é `Tools/Utils/Hotzone` na unidade: para
terrestre, Mobilidade e Captura devolvem as mesmas células, então o azul da
janela é literalmente a resposta que o Quero Carona recebe.

## Pendências conhecidas

- Itens 4, 5 e 6 da esteira: coleta com carga parcial, promessa persistente e
  espera virando demanda de compra.
- A demanda de transporte no shopping tem **dois** sistemas paralelos, gateados
  pelo mesmo slot do plano. Mexer em um só produz demanda fantasma.
- Três portões fora do Quero Carona ainda decidem "vai a pé" com `MP × 2`
  pooled sobre busca de terreno real: `AIController.Rebel.cs` (que decide
  **antes** de consultar o serviço), `Transportador.Shuttle.cs` e
  `Transportador.Assigned.cs`.
- O mapa de custo encadeado não tem cache, ao contrário do mapa de um turno.
  Contadores para acompanhar: `CaptureClaimReachBuilds`,
  `CaptureClaimReachReuses`, `QueroCaronaCaptureReachBuilds`,
  `QueroCaronaMobilityComponentBuilds/Hits`, `TurnChainedCellsExpanded`.
- `UnitThreatEnvelopeService` permanece como adaptador dos consumidores ainda
  não migrados (Fases 2, 4 e 5).
- O contrato prevê banda ausente para Fusão e Artilheiro, alcance logístico em
  vermelho, e as intenções `Estoque` e `Desembarque`, que ainda não existem.
