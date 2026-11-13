# v10.10.0 — O jogo congelava 14 segundos por turno da IA, e ninguém tinha medido

2026-11-12. Um dia em duas metades. De manhã, três marcas do AI Step viraram três regras
(recuperação aérea em célula ocupada, carga com tarefa atendida, frota de Chinooks pela fila
persistente) e uma quarta marca, "tem um erro no Serviço do Comando haha", virou a regra de
acesso da família. À tarde, a pergunta do autor — *"por que tá um baita lag no AI Step antes de
abrir o shopping?"* — abriu a frente que dá nome à versão: **o turno da IA congelava o frame por
14 s somados** (plano 6,5 s, pré-compra 1,5 s, compra 6,5 s), num mapa de 86 unidades, e o
jogador via tela morta. Fechou o dia em **3 s**, com o autor contando no relógio.

O relatório diário do Codex, com cada medição e cada fixture, está em
[`relatorio_2026-11-12.md`](relatorio_2026-11-12.md). Este explica o porquê.

## O fio: três custos escondidos atrás de nomes de consulta

Nenhum dos três era "algoritmo lento". Eram três utilitários com nome de consulta barata e
corpo de varredura, chamados dentro de laços por célula. A regra 2 das Regras de trabalho já
dizia isso do `GetConstructionAtCell` (13 s, v10.4.0). Hoje apareceram os irmãos:

| quem | custo por chamada | quem chamava em laço | efeito |
|---|---:|---|---:|
| `AIController.ResolveAISlotKey` — `FindAnyObjectByType<MatchController>` | 0,8 ms | 264 sítios; laços construção × inimigo no planner | plano 8,9 s |
| `StructureOccupancyRules.GetStructureAtCell` — `FindObjectsByType<RoadNetworkManager>` | 0,8 ms | linha de tiro do `PodeMirarSensor`, por célula da reta; envelope de combate, por célula de movimento; compra, por aeronave inimiga | compra 6,5 s |
| `UnitMovementPathRules.CalcularCaminhosValidos` com orçamento 30 | 1,7 s por unidade | matriz de atribuição do plano, por capturador livre | plano 5,4 s |

O terceiro é de outra natureza e merece a frase: **a busca enumera passeios, não caminhos.**
O estado dela é `(célula, passos exatos, bônus de estrada)`, porque o passo grátis de estrada
depende de `steps == MP`. Com orçamento 30, uma célula a 5 passos vira também um estado a 6, 7,
…, 30 por idas e vindas. A malha saturava em 57 células a partir do orçamento 10 e o tempo
seguia linear: 338 ms com 10, 1.668 ms com 30. `CalculateMovementCostMap(30)`, Dijkstra por
custo, dava as mesmas 57 células em 3 ms. O planner só queria distância; passou a pedir custo.

### O método, porque foi ele que achou

A hipótese inicial do autor era "o AI Step tá rastreando tudo". Estava errada, e a medição
disse isso antes de qualquer código: a captura do AI Step custa `captureMs`, já instrumentado, e
`InspectRoleDemands` frio custava 7–9 s com o AI Step desligado. Cada camada foi medida com um
script read-only em `tools/perf/` (oito novos hoje, todos rodam com
`unity command script eval --file` em Play, e os que "esfriam" caches só limpam o que se
reconstrói sozinho). A decomposição foi sempre a mesma: medir o todo, achar o bloco que domina,
medir dentro dele, até chegar numa função de corpo legível. Sem isso, o `ResolveAISlotKey` teria
sido declarado "o fix" (o planner isolado caiu para <1 s) e o turno real continuaria em 6,4 s,
como o Codex mediu e disse.

Duas hipóteses minhas também caíram pelo número: o solver de atribuição (backtracking, "P(M,N)
explode") custou <1 ms com `nu=4`; e o recrutamento de 15 unidades no rally custou 0,96 ms.

## Desempenho (`4ba7d85` + três commits deste fechamento)

**`ResolveAISlotKey` com cache por cena.** O `GetMatchController` que já existia em
`PlanEvaluator.Helpers` subiu para o lado do resolvedor: `AIController.Instance.Match` primeiro,
senão um estático invalidado em `sceneLoaded` e no `OnDestroy`, com ausência também cacheada.
Chamadores e retorno intactos. Planner isolado: 9,6 s → 0,5 s.

**Registro de `RoadNetworkManager`.** `AllActive` por `OnEnable`/`OnDisable`, como o de
`ConstructionManager`; `GetStructureAtCell` lê o registro em Play e segue buscando a cena no
Editor. Quinze chamadores ganham (pathfinding, `PodeEmbarcar`, `PodeDesembarcar`, LoS).
Linha de tiro: 3,96 ms → 0,07 ms por chamada; nove envelopes de aeronave: 7,5 s → 0,25 s;
`decide` da compra real: 6.567 ms → 256 ms.

**Matriz de atribuição por custo em MP.** `PlanEvaluator.cs` (Passo 5b e
`PlanAssignmentDistance`) troca `CalcularCaminhosValidos` por `CalculateMovementCostMap`,
origem da unidade, orçamento 30. O fallback `TryGetLandMovementDistance`/hex já usava custo, então
a mistura de réguas acabou, não começou. `UnitMovementPathRules` não foi tocado. Matriz 5b:
5.381 ms → 62 ms; `BuildObjectivePlan` real: 6.406 ms → 1.124 ms; Fase 4 → Serviço do Comando:
8.139 ms → 2.974 ms. Os 28 pares capturador/setor do T15 azul ficaram iguais, comparados pelo
`planJson` do AI Review.

**Instrumentação que ficou.** `[AI Perf] BuildObjectivePlan` agora traz marcas por passo e
`livres/objetivos/nu`; `[AI Turn Transition Timing]` marca a transição de turno;
`[AI Shopping Timing]` só com `aiStep`. As marcas são o que permitiu ler o turno real sem
reproduzir nada; e são também o item 1 do que não terminou.

## Frota de Chinooks pela fila persistente (`040ecba`)

A marca "ah mto bom... parou de spamar soldadinhos" (T14) foi a validação em Play. Entrevista
P1–P15 fechada pelo autor: *persistente* = espera desde o turno anterior; *congestionado* =
persistentes úteis acima de uma viagem da frota aérea operacional. `PressaoCaronaService.Read`
publica o fato (útil, persistente, sem entrega conhecida, capacidade por viagem), com
`HasKnownAirDeliveryForRide` perguntando ao `PodeDesembarcarSensor.CanDeliverPassengerFromCell`
pela LZ hipotética, honesto com a névoa do slot. A compra pede **um** transportador aéreo a mais
por turno enquanto há congestionamento, e **freia** as demandas de capturador de expansão (não a
defesa concreta). Único relógio de espera: `PublishRideNeed` no setup da Fase 2.

Ponto de atenção registrado na revisão: sem produtora aérea, o freio solta devagar, não trava
(a fila esvazia quando os passageiros desistem ou morrem); e há duas réguas de "entregável"
(`HasKnownAirDeliveryForRide` e a do courier) que não foram unificadas.

## Serviço do Comando: acesso da família e decolagem (`e793da8`)

Marca T15: helicóptero com ferido sobre cidade cujo chão estava ocupado pelo inimigo recebia
cura. Regra do jogo, não da IA: o acesso é **do transportador**
(`IsDomainCompatibleForConstructionCommandService`, agora pública e compartilhada por preview e
execução); aliado no chão também bloqueia; a família pousa **uma vez** antes do primeiro
atendimento que cabe e volta ao voo pelo `ExecuteReturnToFlightAfterSupply`/`PodeArremeter` ao
trocar de família. Quem começou pousado fica pousado; aeronave solta no ar não ganha serviço só
porque a construção lista Air. Pouso de emergência continua separado: `PodeDecolarSensor` veta
decolagem até combustível real, e o `cantUseWeaponsOnTheFollowDomain` da ficha cuida do tiro.
Só fixture; não visto em Play.

## Recuperação aérea e carga com tarefa atendida (`0e98feb`, `3066a3e`, `9667f23`)

Marcas da manhã (T13, Chinooks #66, #56, #18, #72). `TryEvaluateAircraftRecoveryAnchor` recebe
o conjunto de ocupação aérea do reparo e recusa célula ocupada no Tático e no Operacional, sem
suprimir o hover de emergência. `DecideAirCourierAction` consulta
`FindNearestPlanlessCaptureTarget` antes do fallback de rally quando Hotel e India já estão
atendidos. Comandos `ai step on/off`. Só fixture.

## Checklist de logs para a versão (`docs/checklist_logs_publicacao.md`)

Pedido do autor ao ver os 3 s. Achado que importa: **`showAILogs` desligado não cala a IA** —
gateia 12 das 541 linhas `[AI …]` via `TL()`. Os logs de perf (`[AI Perf]`, `[AI Commit Heavy]`,
`[AI Turn Transition Timing]`, `[SectorManager][Perf]`, `[FoW][LoadCacheRestore]`) não têm flag
nenhuma. A janela `Tools ▸ Publicação ▸ Checklist` cobre só flag serializada. Recomendação na
seção C: uma chave no build (`Debug.unityLogger.filterLogType = LogType.Warning` fora do Editor),
que some com tudo sem tocar 541 linhas, mais uma flag de `DebugManager` para reabrir num build
de teste.

## O que não terminou

1. **Logs sem gate** (acima): decisão do autor entre a chave do build e flags novas; o
   `[AI Turn Transition Timing]` é o mais barulhento e entrou hoje.
2. **Commit pré-compra de 1,5 s.** `[AI Commit Heavy]` soma 1.504 ms com todos os parciais em
   0 ms; o custo está fora dos trechos cronometrados (`CommitAIWorldHeavy`,
   `AIController.WorldCommit.cs:193-256`). Próxima tela morta visível.
3. **`PressaoCaronaService.Read`: 0,4 s por slot** na compra. Zona de "não notei"; anotado.
4. **Correção 2, `pergunte-me`:** a cópia privada `PodeMirarSensor.TryResolveCellVision`
   (`PodeMirarSensor.cs:1868`) duplica `ObservationCellService.TryResolveCellVision` e perde o
   cache por refresh; **não são idênticas** (sem ocupante, a cópia assume Land/Surface; o serviço
   resolve a camada por `TryResolveObservationLayer`). Doutrina do CLAUDE.md diz que o serviço é a
   base; unificar é decisão do autor.
5. **`CalcularCaminhosValidos` com orçamento acima do MP** continua enumerando passeios para os
   outros chamadores (Handoff, Initiative, Progression, Router). Podar estados dominados mexe no
   bônus de estrada, regra do jogo: só com `pergunte-me`.
6. **`TryGetLandMovementDistance`: 1,8 ms por chamada**, em vários laços do plano. Hoje não é o
   gargalo.
7. **Setup do plano: 870 ms** (antes do Passo 1) e distâncias finais 133 ms. Sem causa medida.
8. Carregados da v10.9.1 e ainda abertos: `pergunte-me` da invasão por ilha; teto de unidades 60
   vs 40; passageiros segurando vaga de setor atendido; eixo forte/fraco; a marcha de Golf ainda
   não vista; D4 e Marinha.

## Docs de doutrina

Desatualizados (apontados, não reescritos):

- `docs/AI Behavior/Transporte.md` §5: pressão de compra pela espera **existe** agora (fila
  persistente, congestionamento = uma viagem da frota, freio de expansão). Visto em Play (T14:
  "parou de spamar soldadinhos").
- `docs/manual/07_logistica_e_servicos.md`: acesso ao Serviço do Comando é do transportador;
  uma família pousa uma vez e volta ao voo. Só fixture.
- `docs/AI Behavior/Transporte.md`: recuperação aérea recusa célula de atendimento ocupada; carga
  com tarefa atendida busca capturável desatendido. Só fixture.
- Carregados da v10.9.1: `Assalto.md`/`Papeis.md` (ar com vaga no rally, interceptador sem
  terciário, visto em Play); `Transporte.md` (carga sem tarefa no montinho); rascunhos de Shopping
  (conscrição com teto, projeção simétrica); lista da v10.9.0 (`Forcas.md` inexistente,
  `rally_proposta.md`, `contrato_questionario.md` §7.1, `ficha_do_papel.md`, `governanca.md`
  G17) e da v10.8.0/v10.7.0, sem mudança.

Prontos para consolidar (estável há 2 fechamentos, visto em Play, sem marca aberta):

- **Interceptador não inicia tiro terciário** e **ar com vaga em rally montando fica na área**
  (`Assalto.md`/`Papeis.md`): entraram na v10.9.1, vistos em Play nas marcas T12/T13, nenhum
  commit de hoje tocou `AIController.AirCombat.cs`, e as seis marcas de hoje são de Chinook e
  Serviço do Comando. É o primeiro item que cumpre os três sinais desde a v10.8.0.

## Marcas do AI Step do dia

Seis marcas em 2026-11-12, todas com commit: #66 e #56 (recuperação, `0e98feb`), #18 e #72
(carga, `3066a3e`), "parou de spamar" (frota, `040ecba`), Serviço do Comando (`e793da8`).
Nenhuma aberta.
