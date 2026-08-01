# Fix: Rebel — Capturador de volta pro AI Controller

## Versão

`v6.1.2`

## O que estava errado

Existem duas coisas no jogo: unidade **com** plano e unidade **sem** plano. A IA
com QG usa as duas — tem setores, faz planos, tem eixos. A IA sem QG só tem
unidades sem plano: não usa setor, não faz plano, escolhe alvo por proximidade.

Ou seja: **rebelde é um conjunto de parâmetros sobre o controlador que já
existe**, não um controlador paralelo. A estrutura de IA recebe os parâmetros
de entrada da facção sem QG, que sobrescrevem *algumas* decisões — como marchar
para algum lugar ou embarcar — e não todas.

`AIController.Rebel.cs` foi construído como espelho do capturador. 454 linhas,
seis funções próprias, com busca de alvo, célula de aproximação e portão de
deslocamento paralelos.

A justificativa estava escrita no próprio arquivo: *"o plano normal assume um
eixo a partir do próprio QG — a rebelde não tem, e sem este curto-circuito todo
capturador dela vira rogue e marcha para o QG inimigo"*.

O problema real era **uma linha**:

```csharp
// AIController.Capturer.Rogue.cs
Vector3Int target = snapshot.EnemyHQ.CurrentCellPosition;
```

Em vez de parametrizar a âncora, escreveu-se um caminho paralelo.

## O preço, medido

Cada regra do jogo precisava ser escrita duas vezes, e a segunda sempre
atrasava. O que a facção sem QG deixava de obedecer:

| regra | fluxo normal | espelho |
|---|---|---|
| `prioritizeDpqAtBattle` (flag da ficha) | Assault, Capturer, Defender, Explorer, Pursuer, HQBreaker | **ignorada** |
| alcance a pé | envelope, turnos encadeados | `MP × 2` num bolso só |
| decisão de carona | `QueroCaronaService` | decidida **antes** de consultar o serviço |
| alocação 1:1 de capturável | `CaptureOpportunityClaimService` | busca própria |
| não parar no capturável de outro | `IsOtherAssignedCapturerTarget` | — |
| não parar em produção própria | `CanProduceUnitsForSlot` | — |
| custo real de movimento | malha de custo | `SectorManager.HexDistance` |

A última é proibida em texto no `CLAUDE.md`: *"HexDistance is unit-agnostic and
should not be used as a movement cost proxy"*.

E o espelho tinha vazado: `IsRebelCapturable` — que não tem uma linha de lógica
rebelde, é só "esta unidade pode capturar esta construção" — já era chamada por
seis sítios de transporte e desembarque. O mirror deixara de ser opcional.

## O conserto

### 1. A âncora virou parâmetro

`DecideRogueCapturerAction` recebe `anchorCell`. Quem resolve é
`TryResolvePlanlessCapturerAnchor`:

```text
tem QG próprio → QG inimigo          (o eixo de sempre)
sem QG         → capturável livre mais próximo
```

A escolha do capturável também **registra a intenção** (alvo designado + reserva
da passada), porque para quem não tem plano é essa designação que faz o papel do
objetivo — é o que o Quero Carona e o transporte leem para saber para onde a
unidade quer ir.

### 2. "Sem plano" virou um modo do capturador

`TryDecideCapturerAction` aceita `plan == null`:

- `ResolveAssignedObjective` devolve `null` sem plano, em vez de estourar;
- o portão do rogue deixa de consultar `RogueUnitIds` quando não há plano —
  facção sem plano é 100% rogue por definição, não há lista a consultar. A IA
  com QG continua consultando, porque lá "sem objetivo" é exceção declarada pelo
  planner.

As demais etapas do capturador já toleravam plano nulo: reparo, blitz handoff,
swap e captura oportunista já vinham guardados, e o embarque já era chamado pelo
caminho rebelde antes deste refactor.

### 3. `Rebel.cs` virou roteador

```csharp
return TryDecideCapturerAction(unit, snapshot, plan: null);
```

134 linhas de espelho e 49 de `FindRebelApproachCell` foram embora — o arquivo
caiu de 454 para 299 linhas, e o que sobrou são auxiliares que nunca foram de
rebelde nenhum.

### 4. `FindNearestRebelCaptureTarget` → `FindNearestPlanlessCaptureTarget`

A pergunta que ela responde — *"qual capturável livre mais próximo ainda não
está sendo tratado?"* — vale para toda unidade sem plano, rebelde ou rogue. Ela
já era chamada por três sítios que não são rebeldes (HQBreaker, Courier,
Naval); agora o nome diz a verdade.

## O que a facção sem QG herda de graça

Sem uma linha escrita para ela: DPQ da ficha, alcance pelo envelope unificado,
fila da carona com antiguidade, reserva 1:1 de capturável, handoff de blitz,
swap de capturador, e as guardas de célula (não parar em produção própria nem no
capturável de outro).

## Também nesta versão

**`prioritizeDpqAtBattle` no ataque preemptivo de papel.**
`TryBuildRolePreemptiveAttack` escolhia célula de ataque sem olhar DPQ, enquanto
todos os outros papéis já honravam a flag. Corrigido com o mesmo mecanismo e o
mesmo peso dos demais (`GetTerrainDpqPontos × 2000` com a flag, `× 40` sem) — a
diferença entre os dois números importa: com 2000 a preferência **troca a
célula**; com 40 apenas desempata.

Três chamadores estavam sem: o ataque oportunista do rebelde, o preemptivo do
roteador e o tiro do supridor em modo hospital.

Flag na ficha é contrato, não sugestão.

## Verificação

O teste que separa "funcionou" de "quebrou":

> **IA com QG e planos × IA sem QG e sem planos**, no mesmo mapa.

Sinais no log da facção sem QG, que antes não existiam:

```text
[SemPlano] <id> âncora = capturável (x, y, 0) por proximidade.
[Rogue]    <id> marcha para âncora (x, y, 0) via (a, b, 0)
[Rogue]    <id> reposiciona DPQ e ataca ...
[FilaCarona] <id> entra na fila no turno N ...
```

A linha `marcha para HQ inimigo` deixou de existir: o texto agora nomeia a
âncora, seja ela o QG ou um capturável.

**Não observado ainda:** o refactor não foi executado em partida. A verificação
é o teste acima.

## Pendências

- **Mudança de casa dos auxiliares.** `IsRebelCapturable` (6 chamadores
  externos), `TryResolveUnitDesignatedCaptureTarget` (1) e
  `CommitPendingRebelCaptureTarget` (1) continuam em `Rebel.cs` com nome de
  rebelde. São predicados gerais e pertencem ao capturador. Renomeação
  mecânica, sem risco, adiada só para manter este commit legível.
- **`IsOtherAssignedCapturerTarget` devolve `false` sem plano.** A regra — não
  pare no hex do capturável que é alvo de outro — deveria valer exista plano ou
  não. A fonte para o caso sem plano já existe: o `CaptureOpportunityClaimService`,
  que aloca 1:1 e é o que o `AIDesignatedCaptureTarget` persiste.
- **`TryBuildRolePreemptiveAttack` não tem as guardas de célula** que Assault e
  Capturer têm (produção própria, capturável alheio). Vale para os três
  chamadores, não só o rebelde.
- Infantaria trocando 5 HP por 2 contra helicóptero passou no
  `PassesAttackDecision` com `loss=50%/50%`. Arma com RPS −2 contra a classe do
  alvo deveria ter limite de perda mais apertado. É gate de decisão de ataque,
  não DPQ.
