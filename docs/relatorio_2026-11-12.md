# Relatório de 2026-11-12

Continuação do trabalho registrado em `relatorio_2026-11-11.md`.


### Recuperação aérea — célula de atendimento livre

- Decisão do autor, marca T13 slot 0, Chinook #56: recuperação escolhia Metrópole Lima ocupada pelo Chinook #2 pairando; a execução rebaixava o movimento e deixava o ferido no mar.
- `TryEvaluateAircraftRecoveryAnchor` recebe o conjunto de ocupação aérea já montado pelo reparo. Recusa cada célula diferente da origem que esteja ocupada, tanto no Tático como no Operacional (ServiceCells de supridor ou célula da âncora). Sem alterar score, serviços disponíveis ou prioridade.
- `HasAircraftRecoveryWithinReach`, `EvaluateAircraftRecoveryReach` e `TryBuildAircraftRecoveryApproachAction` compartilham esse conjunto; uma âncora bloqueada não suprime mais o hover de emergência. A materialização do trecho Operacional também exclui destinos ocupados. O chamador local de emergência do EWACS monta o conjunto uma única vez e o passa para recuperação e hover.
- `verificar_recuperacao_aerea_ocupada`: PASS fora do Play. Chinook carregado com 21/60 de autonomia, cidade a 2h ocupada por outro Chinook pairando e cidade livre a 5h: escolhe a livre. Com orçamento Tático menor, devolve Operacional e materializa trecho livre. Só a cidade ocupada: HasRecovery falso e hover em solo pousável, nunca hold sobre mar. Carga, combustível, camada e HasActed preservados.
- Regressões PASS: `verificar_desembarque_rally_aereo`, `verificar_carga_sem_tarefa_rally`, `verificar_recuperacao_aerea_celula_servico`, `verificar_ancora_fusao_retaguarda`. Os dois últimos adaptam a chamada por reflexão ao novo parâmetro explícito. `verificar_pouso_emergencia` não existe em tools/ai_test nesta revisão.
- `bash tools/compilar.sh`: Assembly-CSharp e Assembly-CSharp-Editor verdes. Nada observado em Play, nenhum batch executado. Alterações do autor em fichas, cena e fontes não entram neste commit. Parada para revisão.


### Carga com tarefa atendida — capturável desatendido (nota fora do commit, por pedido do autor)

- Caso T13, Chinook #72: Hotel já atendido; India, objetivo do transporte, também ocupada por aliado. Antes do fallback de rally, `DecideAirCourierAction` consulta `FindNearestPlanlessCaptureTarget` para cada capturador aéreo sem alvo. O primeiro com alternativa vira principal; a entrega segue pelo fluxo existente, inclusive Operacional, vetos de atracação e estacionamento. Sem alternativa: rally próprio montando; sem rally: hold.
- Os endereços alternativos vivem apenas durante a decisão, com restauração em finally. O resolvedor de passageiro e o montador conjunto leem esse contexto antes da missão antiga; não mudam vagas, Mission Intent nem o planner. Sem um serviço novo de busca ou de LZ.
- Duas réguas existentes permanecem diferentes: o buscador planless descarta ocupação aliada na camada de captura, reivindicação coletiva e reserva; `TryResolveUnservedAirCapture` também considera missão/vaga de outro capturador com alcance Tático e pressão confirmada, podendo buscar alternativa no setor. Nenhuma delas foi reescrita ou unificada neste bloco. Esta observação fica no relatório fora do commit solicitado.
- `verificar_carga_sem_tarefa_rally`: PASS com missão Capture→Hotel persistida, Hotel e India ocupadas, cidade desatendida a 8h de India; aproxima e desembarca nessa cidade. Sem alternativa mantém entrega no Core; sem rally mantém hold; alvo original válido continua em Hotel. Missões/vagas intactas, contexto temporário não vaza.
- Regressões PASS: `verificar_desembarque_rally_aereo`, `verificar_prontidao_rally`, `verificar_ar_no_rally`. `bash tools/compilar.sh`: ambos assemblies verdes. Nada observado em Play e nenhum batch executado. Nota deixada pendente no working tree de propósito; alterações do autor fora do commit.


### Frota de Chinooks pela fila persistente — entrevista P1–P15

- Commit preventivo `9667f23`: comandos `ai step on/off` e notas anteriores do courier. Fichas, cena e fontes do autor preservadas fora dos commits desta frente.
- Doutrina confirmada pelo autor: capturadores do plano e rogues com trabalho útil entram; passageiro sem entrega legal conhecida não justifica compra. Persistente = já espera desde o turno anterior. Congestionamento = passageiros úteis persistentes acima da capacidade de uma viagem da frota aérea operacional inteira. Carregados contam pela capacidade total, transportadores em reparo/embarcados não contam como frota disponível.
- `PressaoCaronaService.Read` publica a fila útil, persistente, sem entrega conhecida e capacidade operacional/viagem. Usa `ResolveAIRideWaitTurns` sem escrever o relógio. O pedido passa pelo `BuildCapturerRideRequestWithTarget`, o mesmo dono usado pelo capturador atual; o adaptador anterior ainda chamava a versão sem alvo. Diagnósticos brutos de pedidos e bancos permanecem.
- `AIController.PressaoCarona` reutiliza modelos inertes `CreateDetachedPlanningModel` (já usados pelo envelope), fichas existentes/ofertadas, `PodeEmbarcar.CanUseSlot`, envelope Operacional invertido do passageiro e conhecimento confirmado do slot. Poda por conhecimento antes de consultar o sensor. `PodeDesembarcarSensor.CanDeliverPassengerFromCell` só expõe a pergunta hipotética por passageiro, delegando aos predicados existentes de contexto, vizinhança e descida; não embarca, não move e não altera FOW/estado confirmado. Um pedido sem construção-alvo concreta é excluído da compra.
- `AIShoppingPlanner.OperationalPressure`: lê uma única fila por snapshot de compra; pressão persistente cria demanda não urgente de um transportador aéreo, prioridade 12, sem escalada estratégica. Referência inicial `MaxAirTransporters` continua nos caminhos iniciais; a fila pode crescer além dela. Não há compra imediata de bancos para a fila inteira.
- O carrinho compartilha a cobertura: qualquer compra de transportador aéreo atende o reforço da fila. Na ordem inversa, a compra pelo reforço atende uma demanda normal compatível, inclusive outra porta como counter; não soma outro helicóptero artificial. A fila sozinha só pede um adicional por compra do turno/slot e respeita o orçamento livre e a poupança existentes.
- Freio pela mesma leitura: demandas de capturadores para expansão zeram enquanto há congestionamento; candidatos genéricos também não podem comprar capturador por outra porta. Massa de conscrição, imposto e excedente passam pelo mesmo predicado. Exceção por campo `IsConcreteDefense`, publicado de `SectorDefense`, e preenchimento por ameaça visível concreta à base; postura Defensiva sozinha não libera expansão. Quando persistentes cabem numa viagem, o freio sai, sem exigir fila vazia. A fila reativa sem QG lê o mesmo fato aéreo; o trilho de demanda terrestre permanece separado.
- Tools/Transporte/Pressão Carona e Shopping Pressure mostram o fato publicado: útil/persistente, capacidade por viagem, indisponíveis, sem entrega conhecida e crescimento gradual; não recalculam o ranking do carrinho.

**Validação:** `verificar_frota_chinook` PASS fora do Play: seis passageiros úteis (com plano e rogues), quatro bancos, espera de um turno; carga não reduz capacidade/viagem; reparo reduz; fila nova não compra; alvos em ilhas sem LZ adjacente não justificam frota. Expansão/conscrição/imposto bloqueados; defesa concreta mantida; postura Defensiva não burla; produção volta com fila não vazia, mas sem congestionamento. Carrinho real com três produtores: fila compra um, operação de dois compra dois (não três), outra demanda compatível também não duplica; saldo livre inferior ao preço não compra nem classifica a fila como emergência que rompe reserva. Sem batch executado.

**Regressões PASS:** `verificar_conscricao`, `verificar_shopping_sem_qg`, `verificar_carga_sem_tarefa_rally`, `verificar_desembarque_rally_aereo`, `verificar_shopping_rally_acesso`, `verificar_caca_a_controle_ceu`. `bash tools/compilar.sh`: Assembly-CSharp e Assembly-CSharp-Editor verdes. Nada observado em Play; desempenho e escoamento da ilha com a fila grande continuam para validação do autor.

**Docs de doutrina:** `docs/AI Behavior/Transporte.md`, seção 5, ainda marca pressão de compra pela espera como ausente. Esta entrevista e implementação assentam essa regra; apontamento para `consolidar-doutrina` quando o autor chamar, sem reescrever o documento nesta frente.


### Serviço do Comando — acesso da família e decolagem após a carga

- Entrevista confirmada pelo autor, caso T15: helicóptero sobre cidade com chão ocupado por inimigo não pode receber nem repassar serviços da construção à carga. Vale para aliado bloqueando também; o aliado no chão continua elegível. Regra do jogo, não política só da IA; HP, combustível e munição usam o mesmo acesso.
- Checkpoint prévio `bb15ba6`: fichas aéreas, cena Batalha e fontes pendentes do autor, sem reescrevê-las.
- `ServicoDoComandoSensor`: o coletor dos passageiros verifica primeiro o acesso do transportador pela função existente `IsDomainCompatibleForConstructionCommandService`, agora compartilhada com a execução. Essa pergunta não exige necessidade de serviço do transportador: helicóptero inteiro pode pousar para atender passageiro ferido. Aeronave solta no ar não ganha atendimento automático só porque a ficha da construção lista Air; precisa de pouso legal. Aeronaves embarcadas em supridor continuam pelo caminho existente; KC-130 abastecendo voadores soltos é ação de suprimento ativo, não Serviço do Comando. Aeronave já pousada não decola para buscar uma camada de atendimento.
- `TurnStateManager.CommandService`: preview/plano e execução revalidam acesso pela mesma pergunta. Antes do primeiro atendimento que realmente cabe, pousa o transportador quando necessário, confirma que a transição se concretizou e guarda a intenção de retorno ao voo com o combustível anterior. A fila já normaliza famílias; o retorno é processado na troca de família ou no fim, depois dos passageiros tentados, inclusive quando um foi pulado por saldo/estoque/compatibilidade. Não aguarda cura completa nem muda a ordem econômica das famílias, o planejamento dos ganhos ou o alcance de atendimento.
- Aeronave que começou pousada permanece pousada. Retorno de quem pousou para a operação continua passando por `ExecuteReturnToFlightAfterSupply`/`PodeArremeter`, respeitando combustível, ocupação e demais vetos existentes; não força decolagem ilegal. Sem pouso confirmado, não cobra nem aplica serviço à família.
- `verificar_servico_comando_familia`: PASS. Sensor e corrotina real executados em cena isolada fora do Play: aliado/inimigo embaixo bloqueiam helicóptero e passageiros; aliado no solo recebe; construção que também lista Air não burla o pouso; helicóptero inteiro pousa para a carga; não decola entre os serviços; orçamento só para o transportador termina a tentativa e permite retorno; quem começou pousado permanece; fila coletada antes de aparecer bloqueador é recusada sem cobrar; artilharia no supridor e caça no porta-aviões continuam elegíveis; KC não atende caça solto automaticamente.
- Regressões PASS: `verificar_reserva_pouso_reparo`, `verificar_recuperacao_aerea_ocupada`, `verificar_desembarque_rally_aereo`, `verificar_carga_sem_tarefa_rally`. `bash tools/compilar.sh`: ambos assemblies verdes. Não observado na partida em Play; o teste executou somente objetos isolados no Editor e os destruiu/restaurou ao terminar.

**Docs de doutrina:** `docs/manual/07_logistica_e_servicos.md`, atendimento do primeiro nível de carga, e doutrina de transporte: registrar acesso pelo transportador e retorno ao voo ao fim da família. Apontamento para `consolidar-doutrina` quando o autor chamar; sem ampliar recursão de atendimento nesta correção.


### Desempenho — ResolveAISlotKey sem busca de cena por chamada

- Pedido do revisor, medido no T14 slot 1: `ResolveAISlotKey` fazia `FindAnyObjectByType<MatchController>` em cada acesso, inclusive dentro de laços construção × inimigo. A medição inicial informada pelo revisor foi 799 ms/1.000 resoluções, planner perto de 8,9 s e pré-shopping de 7.967 ms.
- `AIController.cs`: `ResolveAISlotKey` passa pelo `GetMatchController` que já existia em `PlanEvaluator.Helpers`, movido para junto do resolvedor. Prioridade para `AIController.Instance.Match`; sem referência da instância, fallback estático preenchido por uma busca e com ausência também cacheada. Retorno continua: slot ativo da cor pedida; senão único slot da cor; senão -1. Os chamadores de ResolveAISlotKey não foram alterados.
- Cache invalidado em `SceneManager.sceneLoaded` e no `OnDestroy` da IA. Inicialização de subsistema remove/reinstala o handler sem duplicar, inclusive sem domain reload. O fallback instala o mesmo gancho quando consultado em contexto de Editor. Nenhum cache global sem invalidação de cena foi acrescentado.
- Outras buscas substituídas neste commit: `AIController.Start` (Lifecycle), `ResumeActiveAITurnAfterLoad` e `TryStartDebugStage` (Debug) usam o mesmo `GetMatchController`. Permanecem as buscas independentes de `InvasionAxisMap.Build`, `ObjectiveManager.TryResolveSlot` e `MelhorCapturaService` — não houve mudança nesses consumidores ou nas regras da IA.

**Medições reproduzidas em Play com os scripts do revisor, 44 construções e 84 unidades:**

| Medida | Antes | Depois |
|---|---:|---:|
| `medir_slotkey`: ResolveAISlotKey ×1.000 | 593 ms | 0 ms (resolução inteira em ms do script) |
| Controle: FindAnyObjectByType ×1.000 | 664 ms | 752 ms |
| `medir_planner`: BuildObjectivePlan, primeira chamada | 9.643 ms | 505 ms |
| BuildObjectivePlan, segunda chamada | 8.521 ms | 167 ms |
| Analyzer.Rebuild, primeira / segunda | 29 / 27 ms | 10 / 9 ms |
| Pré-shopping real após a última unidade, `[AI Commit Heavy]` | 7.951 ms | 1.581 ms |
| FrameSpike correspondente ao pré-shopping | 7.979,15 ms | 1.602,97 ms |
| `medir_shopping`: demandas slot 1 | 6.625 ms | 365 ms |
| `medir_shopping`: fila de carona slot 1 | 224 ms | 291 ms |

- O antes real foi coletado após o autor executar a última decisão da unidade e parar antes da primeira compra. Console: T14 slot 1, 37 unidades próprias e 39 inimigas no snapshot, boardRev 450; Sync1/2, FogBarrier, SectorRebuild e snapshot em 0 ms. Só então o autor saiu do Play para salvar C#. Depois recarregou o mesmo save, avançou até a primeira compra (Caça B) e os logs foram coletados antes dos benchmarks, evitando confundir FrameSpikes das medições com o fluxo real.
- A meta do planner (<1 s) foi atingida. **A meta do pré-shopping real (<1 s) não foi atingida na primeira transição após carregar: ficou em 1,581 s, redução de aproximadamente 80%.** O quadro seguinte consumiu 6.425,02 ms ao montar a compra; isso explica o autor ainda observar perto de 8 s até a loja. Não declarar a espera total resolvida nem atribuir o custo residual a uma causa não medida neste bloco.
- O script de shopping mede ambos os lados: slot 0 teve demandas 10.059 → 9.449 ms; roster 1 → 2 ms, operacional 2 → 2 ms, counter 3 → 4 ms, fila 213 → 387 ms. Slot 1: snapshot 0 → 0 ms, roster 1 → 1 ms, operacional 0 → 0 ms e counter 3 → 3 ms. A grande variação entre primeira execução real e consultas seguintes exige perfil específico do carrinho/demandas; não foi mascarada com um cache de decisões.
- Scripts do revisor preservados em `tools/perf/medir_slotkey.cs`, `medir_planner.cs`, `medir_shopping.cs`. O planner equivale a dois replans; os outros não compram nem movem unidades.

**Validação:** `verificar_cache_match_ia` PASS fora do Play: Match da instância vence fallback global, cor repetida usa ativo, único não ativo é preservado, cor ambígua retorna -1, mil leituras reutilizam fallback, ausência cacheada não busca; handlers de sceneLoaded e OnDestroy invalidam. Regressões solicitadas PASS: `verificar_frota_chinook`, `verificar_prontidao_rally`, `verificar_shopping_sem_qg`. `bash tools/compilar.sh`: ambos assemblies verdes. Comparação de desempenho acima foi observada em Play; fixtures de semântica foram executados fora do Play. Um commit de correção; parada para revisão com o limite residual explicitado.
