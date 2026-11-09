# Arquitetura atual da tomada de decisão da IA

## Escopo e estado da investigação

Leitura estática realizada em **08/11/2026**, com referência ao HEAD `6488cf6`. O foco é o Capturador e suas conexões com o turno, o roteador, o plano e os serviços compartilhados. Os demais papéis aparecem para situar essas conexões; este documento não é um inventário completo da Aeronáutica, do Shopping ou de todos os comportamentos do jogo.

Nenhum código foi alterado, nenhuma partida foi executada e nenhuma refatoração foi realizada nesta investigação. As marcas e correções anteriores dão contexto, mas a descrição abaixo se baseia nos corpos dos métodos atuais. Uma diferença entre dois caminhos não é, por si só, um bug.

**Resultado principal confirmado:** a IA combina uma sequência de prioridades por retorno antecipado com seleção por pontuação dentro de determinados caminhos. Há políticas compartilhadas, mas não existe, no percurso examinado, uma etapa universal que reaplique todas as oportunidades do Capturador antes de aceitar qualquer movimento. O autor de cada caminho escolhe quais consultas chamar e em que ordem.

As seções 1–6 descrevem **fatos confirmados no código**. A seção 7 reúne **hipóteses e limites da investigação**. A seção 8 apresenta apenas **possibilidades futuras**, sem aprovar uma terceira refatoração ou antecipar a revisão da Aeronáutica.

## 1. Do turno à ação de uma unidade — fatos confirmados

### 1.1 Organização e responsabilidades

Grande parte da implementação é uma única classe Unity, `AIController`, dividida em arquivos `partial`. Separar um comportamento em `AIController.Capturer.Swap.cs`, por exemplo, não cria um componente autônomo: ele continua compartilhando métodos privados e estado da mesma instância com o roteador e os outros papéis. Há também serviços e estruturas independentes, como `UnitReachEnvelopeService`, `CaptureOpportunityClaimService` e `TransportOperationsService`.

| Camada | Implementação e responsabilidade observada |
|---|---|
| Ciclo do turno | `RunAITurn`, em [AIController.Phases.cs](<../../Assets/Scripts/Match/AI/1. Phases/AIController.Phases.cs>), monta/retoma o contexto e conduz comando, ações das unidades e compras, conforme estágio salvo e opções de emulação. |
| Plano | `BuildObjectivePlan`, em [AIController.PlanEvaluator.cs](<../../Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs>), participa da preparação do plano; `ObjectiveManager` disponibiliza o `TeamObjectivePlan` por slot. Objetivos, vagas, intenções e claims orientam decisões posteriores. |
| Preparação tática | O fluxo publica intenções de captura, publica os claims de `CaptureOpportunityClaimService` e reconstrói `AITacticalAnalyzer`. Isso não substitui a decisão individual. |
| Ordem das unidades | `Phase2_UnitActions` e os métodos de [AIController.Initiative.cs](../../Assets/Scripts/Match/AI/AIController.Initiative.cs) organizam quem decide primeiro. A ordem da iniciativa é diferente da ordem das políticas dentro da decisão. |
| Decisão individual | `DecideUnitAction`, em [AIController.Router.cs](../../Assets/Scripts/Match/AI/AIController.Router.cs), percorre consultas em ordem e retorna uma `PlayerAction`. |
| Registro e revisão | `DecideUnitActionForReview`, em [AIController.Debug.cs](../../Assets/Scripts/Match/AI/AIController.Debug.cs), envolve a decisão para o AI Step. `ObservarQuestionario` ocorre depois da decisão e antes da execução. |
| Construção da ação | `BuildMoveBatch`, `BuildCaptureBatch`, `BuildAttackBatch` e outros, em [AIController.Batches.cs](../../Assets/Scripts/Match/AI/AIController.Batches.cs), montam `PlayerAction`. Montar um movimento não pesquisa automaticamente outras capturas ou ataques. |
| Execução | [AIController.Phase2.cs](<../../Assets/Scripts/Match/AI/1. Phases/AIController.Phase2.cs>) entrega a ação a `ExecuteAIBatchWithDebugStep`, trata sucesso/falha e atualiza o contexto para a próxima unidade. |

`Phase2_UnitActions` usa `AIWorldSnapshot.BuildLight` e o reconstrói ao longo da execução sequencial. Não é uma rodada de decisões simultâneas calculadas uma única vez sobre o tabuleiro inicial. Também existem dados consultados diretamente no estado vivo (`UnitManager.AllActive`, construções e sensores); o snapshot não é a única fonte de todas as consultas.

A Fase 2 pode adiar uma unidade para outra agir antes, por exemplo em preparação de combate ou liberação de destino. Ela também verifica ocupação final: pode adiar para o bloqueador aliado agir ou substituir a ação por mover parado. Se a decisão final for `null`, há um caminho que marca a unidade como agida. Isso é diferente de um `null` interno do roteador, que geralmente significa “tente a próxima política”.

O contrato de execução permanece o de [ações transacionais](acoes_transacionais.md). Este documento descreve inclusive alterações de estado de planejamento (`assigned.Status`, reservas e intenções); não as confunde com confirmação de movimento, dano, captura ou combustível.

### 1.2 O roteador não é um grande `switch/case`

O corpo examinado de `DecideUnitAction` é uma cascata de `if`, chamadas `TryDecide*` e retornos. Existem `switch` em outras partes da IA, como classificação de papéis no planner e no Questionário. Isso não torna o roteador, nem toda a IA, uma seleção exclusiva por `switch(role)`.

O roteador pode consultar mais de um papel para a mesma unidade. Compatibilidade de ficha, vagas, capacidades, estado e guardas internas determinam quais métodos realmente produzem uma ação. Há tratamento explícito para híbridos e exceções de prioridade.

Árvore textual abreviada, preservando as precedências relevantes:

```text
RunAITurn
  preparação/retomada do plano, intenções, claims e análise tática
  ├─ Fase 1: CommandService
  ├─ Fase 2: iniciativa → unidade → DecideUnitActionForReview
  │    └─ DecideUnitAction
  │         ├─ liberar produtora, se necessário e não Vigilância aérea
  │         │    └─ consulta recursiva ao papel com liberação suprimida:
  │         │         se a ação já desocupa, usa-a; senão usa a saída genérica
  │         ├─ transporte vazio libera alvo de captura após entrega
  │         ├─ reparo/emergência da própria unidade
  │         ├─ liberação de produtora da Vigilância aérea, na ordem especial
  │         ├─ estoque crítico para transportador-hub vazio, quando aplicável
  │         ├─ operações globais de transporte
  │         ├─ comportamento rebelde
  │         ├─ com plano: Capturador
  │         │    depois: Fogo de Suporte/Assalto, com exceções de híbridos
  │         ├─ Vigilância → Combate Aéreo
  │         ├─ ataque preemptivo de supridor naval combatente, quando aplicável
  │         ├─ Logística → Estoque
  │         ├─ fallback de transportador: courier residual ou vazio
  │         └─ fallback geral: retaguarda conservadora / HexEvaluator
  │              captura, ataque ou movimento, conforme contexto e validação
  │
  │    observador do Questionário / adiamentos / guarda de ocupação
  │    → revisão AI Step / execução do batch → novo contexto
  └─ Fase 3: Shopping

Em cada retorno de ação, os irmãos posteriores deixam de ser consultados.
O diagrama omite detalhes dos outros papéis; não é uma ordem universal por papel.
```

A liberação de produtora é um exemplo de composição já existente: ela não ignora necessariamente a missão. O roteador pergunta se a própria ação do papel já resolve a necessidade de liberar o prédio, usando `suppressProductionUnlockVacate` para evitar recursão repetida.

## 2. Árvore interna do Capturador — fatos confirmados

Fonte principal: [AIController.Capturer.cs](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.cs), métodos `TryDecideCapturerAction` e `DecideAssignedCapturerAction`.

```text
TryDecideCapturerAction
  ├─ TryDecideRepairAction → se houver ação, retorna
  ├─ TryDecideHardBlitzkriegHandoff → se resolver, retorna
  ├─ com plano: FindSwapIncomingCapturer → DecideSwapVacateAction
  │    ├─ outra captura oportunista, respeitando reservas
  │    ├─ combate útil que desocupe
  │    └─ movimento de agenda que desocupe
  ├─ captura no hex atual, se sensor permitir e não houver claim impeditivo
  ├─ captura local antes de embarcar: TryDecideNearbyRallyCaptureBeforeEmbark
  ├─ defesa de prédio aliado antes de embarcar
  ├─ TryDecideCapturerEmbarkAction
  │    ├─ guardas de rally e necessidade de carona
  │    ├─ embarque direto / estendido, por prioridades específicas
  │    ├─ encontro terrestre/LZ por MelhorEmbarque
  │    │    ├─ sem encontro e ilhado: trabalho a pé → cabeça de praia → espera
  │    │    └─ com encontro: mover/esperar; abate oportunista restrito
  │    └─ aproximação de transporte rogue, quando aplicável
  └─ resolver objetivo
       ├─ sem atribuição: rogue permitido / âncora sem plano / caça rebelde
       └─ com atribuição: DecideAssignedCapturerAction
            ├─ alvo designado; senão prédio capturável do setor
            ├─ sem alvo → DecideCapturerDefenderAction
            ├─ sem caminhos → mover parado
            ├─ chegada/captura direta: TryDecideCapturerSpearheadArrival
            ├─ combate atual: TryDecideCapturerPursuerCurrent
            ├─ captura oportunista, com cessão de reservas
            ├─ CapturadorCombatente: TryDecideAggressiveCapturerAction
            ├─ calcula avanço recomendado
            ├─ ataque de oportunidade defensiva
            ├─ exploração/revelação do objetivo
            └─ pontuação de avanço e combate
                 ├─ move+ataca, se escolhido
                 ├─ sem avanço: ceder hex alheio / captura de emergência / esperar / null
                 └─ com avanço: último teste de tiro na posição → move
```

Esse é um desenho de controle de fluxo, não uma pontuação global entre todos os galhos. Por exemplo, uma ação devolvida pelo swap não concorre com uma ação que só seria encontrada mais tarde pelo explorador.

O método da ponta pode devolver `true` com `action == null` ao concluir que já está no alvo, mas não há captura e o objetivo deve ser marcado como completo. O chamador interrompe a sequência interna; o roteador ainda pode tentar outros comportamentos. Portanto, `true`, `null`, mover parado e movimento para outro hex não são resultados intercambiáveis.

## 3. Galhos, entradas, prioridades e saídas

As consultas abaixo são **locais ao galho**. Antes de embarque, defesa e exploração, parte das capturas já pode ter sido testada pelo prefixo comum. Dizer que um método não repete a consulta não significa que a unidade inteira nunca a consultou.

| Galho e fontes | Entrada e prioridade confirmadas | Ação/fallback e limite da composição |
|---|---|---|
| **Swap** — [Capturer.Swap](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Swap.cs), `FindSwapIncomingCapturer`, `DecideSwapVacateAction` | Política `FazSubstituicaoPorEficiencia`; ocupante sobre prédio capturável; candidato elegível fecha em menos rodadas e alcança o prédio. A versão completa verifica caminhos; há versão rápida para iniciativa. | Outra captura → ataque que libere o hex → movimento de agenda. Exclui o prédio cedido e passa `committedCapturer: incoming` à reserva oportunista: o substituto está comprometido com o prédio original. Retorna antes das oportunidades posteriores do método principal. |
| **Handoff em profundidade** — [Capturer.Blitzkrieg](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Blitzkrieg.cs), `TryDecideHardBlitzkriegHandoff` | `FazHandoffEmProfundidade`, handoff planejado ou lembrado e origem/alvo ainda elegíveis. Precede swap e captura local. | Captura do próximo alvo alcançável → ataque especializado da ponta → chegada/avanço. No avanço final, testa captura no hex escolhido. Não faz a mesma varredura oportunista de todos os prédios que o swap faz. Protege o prédio do herdeiro. |
| **Captura local antes de embarcar** — `TryDecideNearbyRallyCaptureBeforeEmbark`, em Capturer.cs | Após captura atual e antes do embarque. Apesar do nome, não exige que todo prédio candidato seja rally: rally pesa no score. Filtra distância até 3, sensor, ocupação e cessão ao responsável. | Escolhe captura pelo score local de distância, captura faltante, rally e ameaça. Não é a mesma seleção “primeiro elegível dos caminhos” de `TryFindOpportunisticCapture`. |
| **Embarque/encontro** — [Capturer.Embark](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.cs), `TryDecideCapturerEmbarkAction`, `TryBuildCapturerLzRendezvousAction` | Ficha compatível, rally não exige permanência e `QueroCarona` confirma necessidade. Embarques materializáveis precedem o encontro residual. | Pode embarcar, aproximar do encontro ou esperar. O encontro usa o lado terrestre do passageiro, não simplesmente a célula aérea/naval do transporte. Não reaplica indiscriminadamente todo combate do Capturador. |
| **Trabalhar enquanto espera** — `TryBuildWorkWhileWaitingForRide`, no mesmo arquivo | Subcaso sem encontro materializável para passageiro ilhado; exige `FazCapturaOportunista`. | Procura prédio capturável no componente a pé com orçamento longo; respeita ocupação, destinos planejados e reservas. Captura agora ou aproxima nesta rodada. Registra `CaptureApproachLedger` e mantém a demanda de carona. É uma busca de trabalho com horizonte diferente da captura tática comum. |
| **Cabeça de praia** — `TryBuildAdvanceToBeachhead` | Depois de não encontrar trabalho a pé no subcaso acima. | Escolhe ponto alcançável no próprio componente que aproxime do alvo do plano; evita produtora própria, ocupação e destinos planejados. Move via `BuildVacateBatch` ou não oferece avanço. A função em si não varre combate/captura de novo. O nome não implica uma praia autorada: é a borda útil do componente terrestre. |
| **Chegada da ponta** — [Capturer.PontaLanca](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.PontaLanca.cs), `TryDecideCapturerSpearheadArrival` | Já no alvo ou alvo alcançável e não ocupado; precede perseguidor e captura oportunista do caminho atribuído. | Captura se o sensor aceitar; pode mover diretamente ao alvo se a captura não for aceita. Isso é precedência explícita da missão, não um fallback que percorreu todas as oportunidades. |
| **Perseguidor** — [Capturer.Pursuer](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Pursuer.cs), `TryDecideCapturerPursuerCurrent` | Próximo ao objetivo, com tratamento para manter prédio aliado sob pressão e para quem precisa desocupar alvo alheio. | Pode atacar parado/reposicionar por DPQ. Quando identifica avanço com tiro disponível, pode devolver `false` para deixar a pontuação posterior decidir. Nem toda resposta negativa significa ausência de combate. |
| **Oportunista atribuído** — `TryFindOpportunisticCapture` + `ShouldReserveOpportunisticCaptureForCloserUnit` → [Capturer.Opportunist](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Opportunist.cs) | Depois de chegada e perseguidor. Exclui o alvo principal e tenta outras construções. | Seleção e reserva ocorrem no chamador; `DecideCapturerOpportunistAction` apenas registra e constrói a captura. O arquivo dedicado não é, sozinho, a política de oportunidades. |
| **Combatente e exploração** — [Capturer.Agressive](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Agressive.cs), [Capturer.Explorer](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Explorer.cs) | Agressivo exige compatibilidade `CapturadorCombatente`. Explorador trata informação/ocupante oculto e posições para revelar o objetivo; vem depois da captura oportunista atribuída. | Ataques e reposicionamentos seguem condições próprias. Revelação de FoW não deve ser confundida com oportunidade econômica omitida: o objetivo do deslocamento é obter informação. |
| **Defensor** — [Capturer.Defender](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Defender.cs), `DecideCapturerDefenderAction` | Sem capturável no setor; pode redirecionar à defesa crítica da base, manter guarnição recente ou rally. Pode devolver `null` cedo se não houver motivo para continuar defendendo. | Se permanece no galho: captura oportunista → liberação/combate de produtora → defesa/combate/reposicionamento. A captura não é consultada nesse método depois de uma saída antecipada que libera o papel. |
| **Rogue/sem plano** — [Capturer.Rogue](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Rogue.cs), `DecideRogueCapturerAction` | Sem vaga atribuída e permitido no plano, ou caminho sem plano/rebelde com âncora resolvida. | Começa com combate da posição atual; no ramo de engajamento testa captura antes do combate móvel. Também testa captura no alvo e oportunista antes de outras aproximações. A âncora é parâmetro, não necessariamente o HQ inimigo. A ordem local não é igual à do swap. |

### Caso motivador: o swap

A marca anterior expôs uma lacuna real daquele fluxo: ceder o prédio chegava ao combate/movimento sem consultar outra captura. **No código atual isso já foi corrigido.** `DecideSwapVacateAction` chama `TryFindUnreservedOpportunisticCapture` antes do combate e do movimento.

O episódio demonstra o risco de composição por chamador, mas não prova que todo galho novo esteja errado. O ajuste atual também mostra que havia uma consulta reutilizável: não foi necessário criar outro serviço de captura.

## 4. Políticas transversais já compartilhadas e diferenças de acesso

### 4.1 Captura: legalidade, seleção e precedência são três perguntas

Em [Capturer.Helpers](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Helpers.cs):

- `SimulateCaptureSensor` consulta `PodeCapturarSensor.TryGetCaptureTargetAtCell` na célula avaliada. Responde se a captura é legal, não se deve preceder a missão.
- `TryFindOpportunisticCapture` aplica o gate `FazCapturaOportunista`, exclusões, ocupação e o sensor. Retorna o primeiro candidato elegível na enumeração dos caminhos; não ordena globalmente todos os prédios por uma única utilidade.
- `TryFindUnreservedOpportunisticCapture` acrescenta a cessão por `ShouldReserveOpportunisticCaptureForCloserUnit` e tenta outro candidato quando necessário.
- `ShouldReserveOpportunisticCaptureForCloserUnit` consulta atribuição, disponibilidade, caminhos, precedência do papel e rodadas para fechar; o nome não resume todo o critério a proximidade.
- `RodadasAteFechar` usa o poder de captura do sensor e o progresso do prédio, em vez de tomar HP bruto como eficiência universal.

Há composição repetida confirmada: o caminho atribuído escreve o laço de busca + reserva + descarte que o wrapper `TryFindUnreservedOpportunisticCapture` já encapsula. Há também seleções diferentes legítimas: captura local de até 3 hexes e trabalho enquanto espera usam scores/horizontes próprios. Unificá-las apenas porque todas chamam o sensor mudaria a escolha.

**Exceção explícita:** no fallback atribuído sem avanço, o código chama a busca simples e o comentário declara captura de emergência ignorando reservas. A omissão da cessão nesse trecho é verificável e deliberada no código; se essa doutrina ainda é desejada precisa de decisão do autor, não de correção automática.

### 4.2 Reservas não são um único conjunto

| Mecanismo existente | O que representa |
|---|---|
| `CaptureOpportunityClaimService`, `PublishForPlan` / `TryGetPublishedForSlot` | Pareamento publicado entre capturadores e construções; também informa unidades sem par. `IsOtherAssignedCapturerTarget`, em Capturer.cs, consulta essa tabela e tem exceção para unidade registrada como sem par. |
| `ShouldReserveOpportunisticCaptureForCloserUnit` | Cessão de uma oportunidade ao responsável ou a outro candidato elegível. Inclui avaliação de fechamento/precedência. |
| `plannedDestinations` | Destinos já planejados na Fase 2. Não equivale a propriedade de prédio nem substitui o sensor de ocupação. |
| `BuildOccupied` | Conjunto de destinos evitados pela IA. Em [Initiative](../../Assets/Scripts/Match/AI/AIController.Initiative.cs), incorpora ocupação e políticas como produtor fechado por conscrição e reserva de pouso de reparo. |
| Reservas de encontro/embarque e `CaptureApproachLedger` | Compromissos de coleta e aproximação a trabalho; não são o mesmo claim econômico de construção. O ledger está no arquivo do `CaptureOpportunityClaimService`. |

Não é seguro trocar um desses filtros por outro apenas porque todos se chamam “reserva”. O parâmetro `committedCapturer` do swap é justamente contexto adicional de missão, limitado à consulta oportunista daquela troca.

### 4.3 Combate: critérios básicos compartilhados, orquestração local

`PassesAttackDecision`, em [AIController.AttackDecision.cs](../../Assets/Scripts/Match/AI/AIController.AttackDecision.cs), delega a `EvaluateAttackDecision`. Os caminhos examinados também usam `PodeMirar`, `CanAttackTargetFrom` e simulação para avaliar um ataque real. Há consultas comuns para DPQ, inclusive `TryFindBetterDpqAttackCellForTarget` e [PositionDpqResolver](../../Assets/Scripts/DPQ/PositionDpqResolver.cs).

Entretanto, os laços que enumeram posições/inimigos e os critérios de preferência ficam em vários chamadores: `TryBuildSwapVacateCombatAction`, `TryDecideBlitzkriegTipAttack`, perseguidor, rogue e pontuação final atribuída. Isso é repetição de **orquestração e seleção**, não prova de que a fórmula de dano esteja duplicada em todos esses métodos.

`TryBuildRolePreemptiveAttack`, no roteador, já oferece uma seleção reutilizável de combate. Sua existência não prova equivalência com o combate da ponta, do swap ou da espera por carona: cada um tem restrições próprias.

Exemplo de exclusividade necessária: `TryBuildRideWaitOpportunityKill` só substitui a espera/reposicionamento quando preserva o encontro, encontra abate garantido, sobrevivência, perda máxima de 1 HP e ameaça aceitável. Usa DPQ da simulação. Transformar esse caminho em “qualquer ataque aprovado pelo Capturador” eliminaria as garantias que o distinguem.

### 4.4 Movimento não é um ponto universal de decisão

`BuildMoveBatch` apenas materializa o movimento recebido. `BuildVacateBatch`, em [Capturer.Vacate](../../Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Vacate.cs), tem uma composição adicional concreta: testa `SimulateCaptureSensor` **no destino já escolhido** e pode construir captura em vez de movimento. Ele não procura outro prédio nem executa toda a árvore do Capturador. Isso também se aplica à cabeça de praia, que o utiliza. O resultado final pode ser movimento porque:

- o chamador já esgotou suas oportunidades;
- uma missão tem precedência e escolheu deslocar diretamente;
- o objetivo é revelar informação ou chegar a um encontro;
- o destino está bloqueado e a Fase 2 substituiu a ação por espera;
- uma exceção de decisão provocou o fallback de mover parado.

Por isso, encontrar `BuildMoveBatch` não basta para concluir que “faltou testar combate/captura”. É necessário reconstruir o caminho até ele e as consultas feitas no prefixo.

## 5. Deslocamentos parecidos, políticas diferentes

| Comparação confirmada | Diferença observada | Leitura arquitetural |
|---|---|---|
| Swap × handoff | Ambos cedem prédio e seguem adiante. Swap varre outras capturas com reserva; handoff prioriza próximo alvo e combate especializado, e só verifica captura no hex final do avanço. | Existe assimetria de oportunidades; ainda não é prova de que o handoff deva abandonar o eixo para qualquer prédio. |
| Encontro para carona × marcha atribuída | Ambos movem infantaria. Marcha atribuída percorre captura/combate/exploração; encontro tenta preservar coleta e só admite combate oportunista restrito. | Parte da diferença é contrato da missão, não duplicação defeituosa. Falta de captura local não pode ser inferida sem considerar o prefixo comum anterior ao embarque. |
| Trabalho enquanto espera × cabeça de praia | Ambos aproximam o passageiro sem embarcar agora. Trabalho procura capturável com rota longa; cabeça de praia só entra depois de não encontrar esse trabalho e escolhe borda do componente. | Aqui existe encadeamento deliberado: a cabeça de praia não ignora simplesmente uma busca que o fluxo acabou de executar. |
| Swap × defensor/rogue | Usam a busca oportunista com reservas, mas em posições diferentes da sequência. Rogue pode atacar parado antes da busca do ramo móvel; defensor pode liberar a função antes de procurar captura local. | O predicado é compartilhado; prioridade e condição de chamada não são universais. |
| Avanço atribuído × fallback `HexEvaluator` | Ambos podem terminar em movimento ou ataque. Só se chega ao fallback global quando os caminhos anteriores não retornam ação; ele usa sua própria avaliação e guarda de captura reservada. | O fallback global não funciona como revisão obrigatória de cada movimento especializado. |

## 6. Reuso existente a consultar antes de propor serviços

| Pergunta | Autoridade/componente já existente e alcance confirmado |
|---|---|
| Pode capturar aqui? | `PodeCapturarSensor` e `SimulateCaptureSensor`. Legalidade não é ordem de prioridade. |
| Que capturas existem e como reivindicá-las? | [MelhorCapturaService](../../Assets/Scripts/Match/AI/Services/MelhorCapturaService.cs), `CaptureOpportunityClaimService`, helpers de reserva do Capturador. Há chamadas de `MelhorCapturaService.Evaluate` no serviço de claims e em `QueroCaronaService`; não se deve criar outra descoberta de prédios sem comparar esses contratos. |
| Precisa de carona? Onde encontrar/embarcar? | [QueroCaronaService](../../Assets/Scripts/Match/AI/Services/QueroCaronaService.cs), `BuildCapturerRideRequest`, [MelhorEmbarqueService](../../Assets/Scripts/Match/AI/Services/MelhorEmbarqueService.cs) e `PodeEmbarcar`. Pedido, escolha do encontro e legalidade física são responsabilidades distintas. |
| Onde desembarcar? | [MelhorDesembarqueService](../../Assets/Scripts/Match/AI/Services/MelhorDesembarqueService.cs), com opções do `PodeDesembarcar`. |
| Alcance real e horizonte? | [UnitReachEnvelopeService](../../Assets/Scripts/Match/AI/Services/UnitReachEnvelopeService.cs) e [AIActionReachCoordinator](../../Assets/Scripts/Match/AI/Services/AIActionReachCoordinator.cs). O envelope fornece origens/turnos; o coordenador organiza tentativas por banda. Eles não escolhem sozinhos a doutrina econômica do Capturador. |
| Como progredir a uma âncora? | `TryFindBestToolProgressionCell`, em [AIController.ProgressionSelector.cs](../../Assets/Scripts/Match/AI/AIController.ProgressionSelector.cs). Já é usado no encontro e em outros papéis; comparar critérios antes de escrever outra seleção de movimento. |
| Um ataque é aceitável? | `PodeMirar`, `PassesAttackDecision`, simulação e resolução de DPQ. Seleção do alvo e restrições de missão continuam nos consumidores examinados. |
| Já existe seleção de combate? | [MelhorCombateService](../../Assets/Scripts/Match/AI/Services/MelhorCombateService.cs) tem `Evaluate` e comparação de candidatos. Não foi encontrada chamada direta de produção a `MelhorCombateService.*` em `Assets/Scripts` nesta leitura. Isso não prova ausência de uso em ferramentas Editor/reflexão; tampouco autoriza substituir os seletores locais sem comparação. |
| Destino de reparo e segurança da fusão? | `RankRepairConstructions` / `FindRepairConstruction`, em [Repair.Movement](../../Assets/Scripts/Match/AI/Units/Repair/AIController.Repair.Movement.cs), e `IsRepairFusionSiteSafe`, em [Repair](../../Assets/Scripts/Match/AI/Units/Repair/AIController.Repair.cs). Exemplo de regras já compartilhadas entre escolha e execução. |
| Faixa e prazo de atendimento do supridor? | `ResolveLogisticsNeed`, `FindLogisticsServiceTargetWithTiming` e checagem de origens, em [Logistics.Supply](../../Assets/Scripts/Match/AI/Units/Logistics/AIController.Logistics.Supply.cs). É outro exemplo de composição compartilhada, sem implicar que todo papel tenha a mesma sequência. |

### Questionário: infraestrutura existente, função atual de observador

[AIController.Questionario.cs](../../Assets/Scripts/Match/AI/Questionario/AIController.Questionario.cs) define respondentes compartilhados e `ObservarQuestionario`; [Questionario.Capturador](../../Assets/Scripts/Match/AI/Questionario/AIController.Questionario.Capturador.cs) declara a ordem das perguntas e políticas do observador. No fluxo examinado, a Fase 2 chama o observador **depois** da ação escolhida, sem tomar sua resposta como substituta da decisão.

Portanto, essa infraestrutura merece ser examinada antes de inventar uma nova camada de consultas. Ela **não é evidência de que a produção já consulte todas as perguntas em todo galho**. O cabeçalho histórico de [contrato_questionario.md](../AI%20Behavior/contrato_questionario.md) diz “nenhuma linha de código existe”, mas o observador já existe no código atual. Não se deve usar esse cabeçalho antigo como retrato da implementação nem confundir o desenho completo do contrato com sua adoção efetiva.

## 7. Suspeitas arquiteturais e investigação ainda necessária

1. **Cobertura desigual pode produzir novos casos como o swap.** A assimetria dos caminhos está confirmada; a existência de uma oportunidade vantajosa ignorada em cada um deles não está. Para demonstrar uma lacuna nova, é necessário um estado reproduzível, a sequência exata de retornos e as alternativas legalmente disponíveis naquele passo.
2. **Há possível consolidação da seleção de combate.** Vários laços fazem trabalho semelhante, mas usam objetivos, limites de desvio e critérios de DPQ distintos. Ainda falta comparar os candidatos e a ordem de preferência em estados iguais. Reutilizar um laço comum sem isso pode alterar comportamentos já validados.
3. **Reservas merecem comparação por contrato.** Claims publicados, cessão dinâmica, destino planejado e compromisso de transporte são diferentes. A leitura não provou que as combinações sejam contraditórias em todos os caminhos. Um teste deve separar “prédio designado”, “outra unidade fecha antes” e “substituto já comprometido”.
4. **A exceção de captura de emergência precisa de validação doutrinária.** O bypass de reservas é explícito no fallback atribuído. Não foi decidido nesta investigação se deve permanecer, nem reproduzido um conflito decorrente dele.
5. **Retornos antecipados podem explicar observações sem indicar erro.** Chegada ao alvo, reparo, revelação, rally e encontro podem legitimamente impedir consultas posteriores. Logs de candidatos não avaliados não provam que esses candidatos foram recusados por score.
6. **Não há conclusão de desempenho para uma composição mais ampla.** Consultas de captura, caminhos e combate têm custos diferentes. Perguntar tudo em todo galho pode repetir sensores ou buscas que já rodaram. Esta leitura não fez benchmark nem propõe multiplicar avaliações.

## 8. Oportunidades futuras de refatoração — sem execução ou mudança de arquitetura

A base aproveitável já existe: predicados compartilhados, sensores, serviços de alcance/embarque/captura, construtores de ação e observação pelo AI Step/Questionário. O problema a explorar é a **composição desses recursos**, preservando prioridades, e não a substituição da IA por outra arquitetura.

Possíveis passos posteriores à revisão da Aeronáutica:

- **Começar por equivalências pequenas e demonstradas.** O laço atribuído de oportunidade + reserva é candidato a comparação com o wrapper existente. Verificar exclusões, ordem dos caminhos e atualização do estado antes de trocar chamadas.
- **Explicitar o contexto que limita uma oportunidade.** Exemplos atuais: não ocupar o prédio do herdeiro, retirar o substituto da disputa oportunista do swap, preservar o encontro da carona e manter presença no rally. Compartilhar a consulta não exige impor a mesma prioridade a todas essas missões.
- **Distinguir “movimento escolhido pela missão” de “fallback após esgotar opções”.** Essa distinção pode guiar documentação/log e testes antes de qualquer alteração. Não inserir uma reavaliação automática dentro de `BuildMoveBatch`: isso mudaria o significado de todos os seus consumidores.
- **Comparar serviços existentes por equivalência comportamental.** Antes de aproximar seletores de combate de `MelhorCombateService`, ou buscas econômicas de `MelhorCapturaService`, registrar os candidatos e o vencedor no mesmo estado, incluindo DPQ, reservas e restrições de missão.
- **Usar regressões de cenário como limite da futura extração.** Swap deve liberar o prédio e aproveitar outra captura; handoff deve preservar o herdeiro/eixo; espera por carona não pode atrasar embarque para perseguir combate; defesa crítica não deve abandonar sua obrigação sem autorização; cabeça de praia deve continuar dentro do componente alcançável. Os testes existentes em `tools/ai_test` são pontos de partida, não comprovação de toda a árvore.

Essas possibilidades não aprovam um serviço novo, não definem uma ordem universal de políticas e não alteram a doutrina de missão. A decisão sobre a terceira refatoração permanece posterior.

## Síntese do diagnóstico

**Confirmado:** a responsabilidade por perguntar por oportunidades é distribuída entre o prefixo comum e os caminhos especializados. Compartilhar o predicado não garante que ele seja chamado antes de todo retorno. O swap foi um caso concreto dessa composição e já recebeu correção.

**Não confirmado:** que toda diferença seja uma omissão defeituosa, que todos os movimentos devam passar pela mesma ordem, ou que falte uma camada inteiramente nova. Há exclusividades de missão reais e serviços já disponíveis. A revisão futura deve procurar equivalências localizadas e preservar os comportamentos validados.
