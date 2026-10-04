# Pendências do MVP

Levantamento de 03/10/2026, baseado em leitura de código, sem alterações de implementação. Os cenários abaixo ainda precisam de reprodução em Play. Referência obrigatória: [contrato de ações transacionais](arquitetura/acoes_transacionais.md).

O jogador deve decidir com a inteligência confirmada disponível antes da ação. Movimento provisório, menus e prévias não podem revelar informações novas. Cancelar deve restaurar o estado anterior; o tabuleiro é atualizado após o compromisso e o retorno a Neutral.

Regras novas de jogo que mudam o projeto inteiro (como a Zona de Controle) ficam **fora** do MVP, em [implementação pós mvp.md](implementação%20pós%20mvp.md).

## Ordem recomendada

1. ~~Ocupantes ocultos no desembarque.~~ Aceito por design (ver abaixo).
2. Memória das construções na captura. Ainda em aberto: o vazamento aqui é de natureza diferente (o dono **atual** de um prédio distante é informação estratégica, não "tem algo embaixo").
3. Objetivos de tutorial durante movimento cancelável.
4. Recuperação quando o rollback não inicia.
5. Integração dos recursos provisórios e testes transversais.

## ~~Alta~~ Aceito por design — Desembarque pode denunciar ocupantes na névoa parcial

- [x] **Decisão do autor (03/10/2026): é um vazamento aceitável. Não corrigir.**

**Por quê.** Dois terrestres não ocupam o mesmo hex: aliados passam, mas não param. Se o jogador leva um helicóptero a um hex explorado (mas não revelado) e a opção de pousar ou desembarcar não aparece, ele estranha ("uai, por que não dá pra pousar aqui?") e conclui que há algo embaixo. O mesmo vale para a lista de hexes onde largar o passageiro: o hex ocupado não entra nela. Depois do compromisso, o tabuleiro recalcula no `Neutral` e o inimigo aparece se estiver ao alcance da visão.

O vazamento é **de agora**, não de memória: o hex foi visitado no passado, e o ocupante pode ter chegado depois. Mesmo assim fica, porque:

- só acontece em hex **explorado**; o preto já é filtrado (`IsCellKnownForDisembark`);
- diz apenas "tem algo", nunca o quê nem de quem;
- a alternativa seria uma mecânica nova inteira para um caso raro.

**Alternativa recusada: a "emboscada" do Advance Wars.** Oferecer a opção mesmo assim e, na execução, parar a unidade antes do hex escondido e consumir a ação ("Trap!"). O The Map Room prefere que a regra física decida **antes**, não punir **depois**. Não reintroduzir sem nova decisão do autor.

O texto abaixo é o levantamento original, mantido como registro.

**Evidência:** o preto já é filtrado, mas, em célula explorada, `CanDisembarkAtCell` consulta ocupantes reais, bloqueia o destino e gera o motivo “Hex ocupado por…”. Mesmo que a frase não apareça, a ausência da opção pode denunciar um inimigo oculto.

**Código:** `Assets/Scripts/Sensors/PodeDesembarcarSensor.cs`, métodos `CanDisembarkAtCell` e `FindDisembarkBlockerAtCell`; consumo das opções em `TurnStateManager.Disembark.cs`, método `RebuildDisembarkLandingOptions`.

**Teste em Play:** comparar as opções para o mesmo terreno explorado, primeiro vazio e depois ocupado por inimigo não detectado, antes de confirmar a ação.

**Critério de conclusão:** as escolhas e os motivos anteriores ao compromisso usam somente informação conhecida. A execução continua validando a ocupação real, com comportamento definido para um bloqueio descoberto após o compromisso. A correção anterior do preto não cobre este caso.

## Alta — Captura consulta o estado atual de construção fora da visão

- [ ] Reproduzir e corrigir diferenças de menu que revelem proprietário ou captura desconhecidos.

**Evidência:** o sensor permite consultar terreno explorado e depois lê proprietário e pontos de captura atuais. Esses dados influenciam oferecer captura, recuperação de aliado ou nenhuma ação.

**Código:** `Assets/Scripts/Sensors/PodeCapturarSensor.cs`, consulta de `construction.SlotIndex` e `construction.CurrentCapturePoints` após a verificação de exploração; `TurnStateManager.Sensors.cs`, método `RunExploredTerrainContextSensors`.

**Teste em Play:** explorar uma construção, perder visão, mudar seu dono ou pontos de captura e aproximar uma unidade provisoriamente. Comparar o menu com a informação anteriormente conhecida.

**Critério de conclusão:** o menu e a prévia respeitam a memória confirmada; a validação definitiva da execução não permite usar estado antigo para executar uma operação ilegal.

### Decisão do autor (03/10/2026): menu, mapa e Jornal leem a mesma memória

Nenhum dos três pode saber mais que os outros.

```text
mapa mostra: prédio SEU (intel defasada)    realidade: já é do inimigo

hoje:      a unidade chega → menu oferece "Capturar" → o menu sabe mais que o mapa: vazou
decidido:  a unidade chega → menu responde pela MEMÓRIA ("meu") → não oferece captura
           → confirma → Neutral recalcula → hex visível → aparece o dono real
           → no turno seguinte, "Capturar"
```

Chegar com intel velha custa um turno. É o preço da névoa.

**O Jornal conta como informação legítima.** Se o Jornal do Comandante relatou que o
prédio foi capturado, a memória do time passa a dizer "inimigo", o mapa pinta a cor
do inimigo e o menu oferece "Capturar". Sem vazamento: a guarnição avisou.

**O que existe no código (levantado em 03/10/2026):**

- A memória já existe: `MatchController.fogConstructionMemoryBySlot` guarda, por
  time, o **dono conhecido** de cada prédio visto (`TryGetKnownConstructionAtCell`).
  Vai no save e é o que o mapa pinta na névoa.
- Ela **não guarda pontos de captura.** Fora da visão, um prédio lembrado como
  aliado não oferece "Recuperar": não se sabe se está ferido. Quem chega ao próprio
  prédio fora da visão recupera no turno seguinte.
- O Jornal relata a perda (`TurnStateManager.Capture.cs`,
  `ReportTurnBriefingEvent(... ConstructionLost ...)`), mas **não atualiza a
  memória** — por isso hoje o mapa pode contradizer o Jornal.
- A escrita na memória tem barreira (`IsFogConfirmedMemoryWriteAuthorized`): só no
  `Neutral` e só para o time cujo fog está sendo calculado. A perda acontece no
  turno do **capturador**; a memória a corrigir é a do **dono anterior**. Logo a
  atualização precisa ficar **pendente** e ser aplicada quando o fog daquele time for
  gravado de novo — e a pendência também vai no save.

**Plano:**

1. `PodeCapturarSensor`: em hex explorado e fora da visão, decidir pelo dono
   **lembrado** (`TryGetKnownConstructionAtCell`), não pelo real. Lembrado aliado →
   nada; lembrado inimigo ou neutro → "Capturar".
2. Execução da captura: revalidar contra o estado **real** no compromisso. Se a
   memória ofereceu "Capturar" e o prédio na verdade é aliado, a operação não
   acontece (comportamento a definir na implementação, sem conceder ação ilegal).
3. Jornal → memória: ao relatar `ConstructionLost`, registrar uma pendência
   "dono conhecido = capturador" para o time que perdeu; aplicar na próxima
   gravação do fog daquele time; salvar a pendência.
4. Teste em Play: o cenário do critério acima, com e sem a linha do Jornal.

**Implementado em 03/10/2026, falta o teste em Play (passo 4):**

- `PodeCapturarSensor`: parâmetro `respectFogMemory` (OFF por padrão). Ligado só
  no menu do jogador (`TurnStateManager.Sensors.cs`, nos dois pontos), no pedido de
  captura (`HandleCaptureActionRequested`) e no rótulo "Reforçar controle /
  Conquistar" do helper — que senão entregaria que o prédio é seu.
- A execução (`ExecuteCaptureSequence`) e a IA continuam pelo estado real. Se a
  memória ofereceu "Capturar" e o prédio é aliado com captura cheia, a execução
  recusa com o motivo do sensor e a ação é consumida: comprometeu com intel velha.
- `MatchController.ReportConstructionOwnerToSlot`: chamado junto com o
  `ConstructionLost` do Jornal; aplicado em `RecordConfirmedConstructionMemory`
  (antes do que se vê, que vence). Vai no save como entrada `isOwnerReport` de
  `FogConstructionMemorySaveData`.
- O rótulo do `PanelDialogController` no estado `Capturando` continua lendo o
  estado real de propósito: é a execução, depois do compromisso.

## Média — Tutorial pode avançar com movimento cancelável

- [x] Adiar a conclusão dos objetivos de movimento até o compromisso da ação. **Implementado em 04/10/2026, falta Play:** `USED_ROAD_BOOST` saiu do `HandleUnitMoved` e passou ao poll `CheckUnitAtHexObjectives` (unidade com `HasActed` e `UsedRoadBoostOnLastMove`); o rollback devolve o indicador ao valor de antes do movimento (`usedRoadBoostBeforeMove`).

**Evidência:** `OnUnitMovementExecuted` dispara antes da confirmação. O tutorial evita concluir `UNIT_AT_HEX` nesse evento, mas ainda conclui `USED_ROAD_BOOST`. O indicador `UsedRoadBoostOnLastMove` também é atualizado na conclusão da animação provisória; não foi encontrada restauração desse indicador no caminho normal de rollback revisado.

**Código:** `Assets/Scripts/Match/TurnState/TurnStateManager.Movement.cs`, método `HandleMovementAnimationCompleted`; `Assets/Scripts/UI/TutorialManager.cs`, método `HandleUnitMoved`.

**Teste em Play:** percorrer a estrada de forma suficiente para cumprir o objetivo e cancelar o movimento. Verificar objetivo, indicador e eventual progressão do tutorial.

**Critério de conclusão:** cancelar não deixa objetivos concluídos nem efeitos persistentes do deslocamento descartado.

## Média — Fallback de rollback não restaura explicitamente a posição

- [x] Reproduzir a falha de início do rollback e garantir restauração completa. **Implementado em 04/10/2026, falta Play:** no ramo de falha, a unidade é posta na origem (`committedOriginCell`) e o retorno segue o mesmo `HandleMovementAnimationCompleted(UnitSelected)` do rollback animado — camada forçada, indicador de estrada, rastro, cursor e área pintada.

**Evidência:** quando `BeginRollbackToSelection` falha, o fallback retorna à seleção e limpa o caminho, sem reposicionamento explícito na origem nesse trecho. Os custos já foram devolvidos. Existe risco de permanecer no destino com movimento restituído; não é uma falha comprovada do cancelamento normal.

**Código:** `Assets/Scripts/Match/TurnState/TurnStateManager.StateMachine.cs`, tratamento do retorno falso de `BeginRollbackToSelection`; `TurnStateManager.Movement.cs`, implementação desse método.

**Teste em Play:** simular indisponibilidade da animação no cancelamento e verificar posição, camada, combustível, movimento, seleção e visão.

**Critério de conclusão:** falhar a apresentação do retorno não impede restaurar o estado anterior e não concede deslocamento gratuito.

## Atenção arquitetural — Recursos provisórios na própria unidade

- [ ] Verificar consumidores dos valores temporários e decidir se é necessário separar a prévia do estado confirmado.

**Evidência:** a preparação desconta combustível e movimento do `UnitManager`, com devolução no cancelamento. A restauração existe; o risco é outro sistema tratar esses valores temporários como definitivos.

**Código:** `Assets/Scripts/Match/TurnStateManager.cs`, métodos `ApplyPreparedFuelCost`, `ApplyPreparedMovementCost`, `RestorePreparedFuelCostIfAny` e `RestorePreparedMovementCostIfAny`.

**Teste em Play:** mover, abrir e fechar submenus, cancelar e repetir; observar recursos, HUD, sensores, objetivos e persistência. Confirmar uma ação e verificar desconto único.

**Critério de conclusão:** nenhuma informação provisória sobrevive ao cancelamento ou alimenta efeitos definitivos. Esta observação não comprova perda de recursos no cancelamento normal.

## Proteções já encontradas no código

Estes mecanismos existem, mas não substituem verificação integrada em Play:

- Alvos do ataque humano passam pela visibilidade confirmada.
- Desembarque filtra células pretas antes de consultar terreno e ocupação.
- O rollback normal restaura custos e camada temporária.
- Mudanças pendentes do tabuleiro são aplicadas no retorno a `Neutral`.

## Bateria transversal ainda pendente

- [ ] Embarque, supply e fusão: verificar prévias, motivos de bloqueio e cancelamento. Filtrar relações entre unidades não certifica todos os dados apresentados.
- [ ] **Ação Direta a partir da seleção** (03/10/2026, `TurnStateManager.ContextualMove.cs`): tocar num inimigo, transporte, parceiro de fusão ou alvo de suprimento faz a unidade andar sozinha até uma célula legal e abrir o prompt. A escolha usa sensores de célula projetada e só considera inimigos visíveis ao lado ativo. Verificar em Play que nem a célula escolhida nem a ausência do atalho denunciam inimigo oculto, e que cancelar desfaz o movimento provisório e o alvo pendente.
- [ ] Ataque através de terreno desconhecido: verificar opções e motivos em origens visíveis e provisórias, sem expor terreno oculto.
- [ ] Transferência: verificar consultas de construções e pouso em áreas exploradas, mas fora da visão atual.
- [ ] Repetir os fluxos pertinentes para humano, IA e replay, preservando o mesmo contrato transacional.
- [ ] Em todas as ações: confirmar que cancelar não altera exploração, detecção, memória, recursos ou estado de ação; confirmar que o compromisso aplica os efeitos uma única vez.

Este levantamento prioriza confiabilidade das ações existentes. Não representa certificação completa dos fluxos nem inclui expansão de sistemas, modding ou tradução integral.

## Continuação da investigação — replay e falhas de execução

Segunda passagem em 03/10/2026. Somente leitura de código e atualização deste documento; sem alterações de implementação ou testes em Play. Os itens de replay abaixo devem entrar na prioridade alta da ordem recomendada.

### Alta — Voltar de submenu descarta o contexto inteiro do replay

- [ ] Preservar o contexto da ação ao voltar uma etapa; descartar a ação inteira somente no cancelamento correspondente.

**Evidência confirmada no código:** `HandleCancel` chama `DiscardPendingCombatCinematicTrack` antes de tratar os submenus. `HandleScannerPromptCancel` também chama esse descarte antes de voltar da confirmação de ataque para a seleção de alvo, da confirmação de embarque para a lista, ou de etapas de desembarque/fusão para etapas anteriores. O descarte executa `ReplayManager.DiscardCurrentBuffer`, que substitui todo o buffer por um `PlayerAction` novo.

O preenchimento de unidade/origem ocorre na seleção; o de deslocamento ocorre após a animação. Voltar apenas uma etapa do submenu não repete necessariamente esses passos. A confirmação posterior pode atualizar alvo/ação sem recuperar unidade e movimento. A finalização promove o buffer sem reconstruir esses campos.

**Código:** `TurnStateManager.StateMachine.cs`, `HandleCancel`; `TurnStateManager.ScannerPrompt.cs`, `HandleScannerPromptCancel`; `TurnStateManager.ReplayRecording.cs`, `DiscardPendingCombatCinematicTrack`; `ReplayManager.cs`, `DiscardCurrentBuffer`, `EnsureCurrentUnitActionBuffer` e `PromoteCurrentBuffer`.

**Teste em Play:** mover, abrir ataque, chegar à confirmação, voltar à seleção de alvo e confirmar outro alvo, sem cancelar o movimento. Repetir no embarque e nas filas. Inspecionar unidade, origem, destino e ação no registro, salvar/carregar e reproduzir.

**Critério de conclusão:** navegar para trás dentro da ação não perde o contexto necessário ao replay. Cancelar a ação inteira não publica um registro definitivo.

### Alta — Caminho do replay compartilha uma lista que é limpa

- [x] Dar ao registro de movimento uma cópia independente do trajeto. **Implementado em 04/10/2026, falta Play:** `UpdateCurrentBufferMovement` guarda `new List<Vector3Int>(movementPath)`. `RecordStandaloneAction` (que guarda a ação diretamente) não foi alterado.

**Evidência confirmada no código:** `RecordConfirmedMovementReplayStep` passa `committedMovementPath` para `UpdateCurrentBufferMovement`, que atribui diretamente `currentBuffer.MovementPath = movementPath`. `ClearCommittedMovement` limpa essa mesma lista. Na finalização comum, `ClearSelectionAndReturnToNeutral` ocorre antes de `PromoteCurrentBuffer`. `RecordStandaloneAction` também armazena a ação diretamente; a cópia encontrada em `CloneActionForCompactSave` é posterior, na compactação para save.

**Consequência delimitada:** o trajeto registrado pode ser esvaziado, mesmo quando origem e destino permanecem. Isso não demonstra, sozinho, que toda reprodução falha: é necessário verificar o comportamento quando o caminho está vazio e se a rota é reconstruída de forma diferente.

**Código:** `TurnStateManager.Movement.cs`, `RecordConfirmedMovementReplayStep`; `ReplayManager.cs`, `UpdateCurrentBufferMovement` e `RecordStandaloneAction`; `TurnStateManager.cs`, `ClearCommittedMovement` e `TryFinalizeSelectedUnitActionFromDebug`.

**Teste em Play:** executar uma rota com desvio deliberado, confirmar apenas movimento e conferir `MovementPath` após a finalização. Comparar o trajeto ao reproduzir imediatamente e depois de salvar/carregar.

**Critério de conclusão:** limpar a seleção ou iniciar outra ação não altera dados de ações registradas; o replay preserva o trajeto efetivamente escolhido.

### Média — Transferência pode falhar após alterar a camada de aeronaves

- [ ] Definir recuperação atômica ou conclusão coerente quando o pouso preparatório falha.

**Evidência:** a sequência de transferência valida as condições iniciais, depois pousa fornecedor e/ou alvo, e só então executa a transferência. `ApplyRequiredTransferLanding` pode mudar AirHigh para AirLow antes de uma operação de pouso que falhe. Também é possível o fornecedor já ter pousado quando uma etapa posterior falha. Os caminhos de falha saem da sequência; o `finally` limpa o prompt e a flag de execução, sem restauração explícita das camadas alteradas. A finalização normal só ocorre quando a transferência retorna sucesso.

**Classificação:** risco em caminhos de falha durante execução já confirmada; não é evidência de que abrir o menu ou cancelar antes da confirmação pousa aeronaves.

**Código:** `TurnStateManager.Transfer.cs`, `ExecuteTransferPromptSequence`, `ApplyRequiredTransferLanding` e `TryExecuteTransferOptionRuntime`.

**Teste em Play:** provocar falha entre a validação inicial e o pouso, ou entre o pouso do fornecedor e o do alvo. Verificar camadas de ambas as unidades, recursos, estado do cursor e publicação do tabuleiro.

**Critério de conclusão:** uma falha não devolve controle cancelável com alterações definitivas sem tratamento. O fluxo deve restaurar o que for provisório ou concluir/publicar explicitamente o resultado comprometido.

### Média — Supply pode retornar à seleção após transição de camada sem atender alvos

- [ ] Validar a recuperação de camada quando nenhum alvo recebe serviço.

**Evidência:** `ExecuteQueuedSupplyOrdersSequence` aplica transição de camada do fornecedor antes de percorrer a fila. Se `servedTargets <= 0`, retorna à seleção de candidatos. Nesse caminho não há restauração explícita da camada do fornecedor; o `finally` apenas limpa `supplyExecutionInProgress`.

**Classificação:** caminho de falha a reproduzir. Não foi demonstrado que uma fila válida chegue normalmente a esse caso sem mudanças de contexto durante a execução.

**Código:** `TurnStateManager.SupplyQueue.cs`, `ExecuteQueuedSupplyOrdersSequence`, chamada de `ApplySupplyLayerTransitionIfNeeded` e ramo `servedTargets <= 0`.

**Teste em Play:** fornecedor que precisa trocar de camada, com todos os alvos deixando de ser atendíveis antes do serviço. Após retornar à seleção, cancelar e comparar camada, recursos e visão com o estado anterior.

**Critério de conclusão:** retorno à seleção após execução sem serviço não conserva alterações de camada sem compromisso ou restauração definidos.

### Baixa — Atualização de caches de debug invalida revisões fora de Neutral

- [x] Restringir ou documentar a invalidação de caches de debug durante ações provisórias. **Implementado em 04/10/2026:** `ThreatRevisionTracker.ForceInvalidateAll` só roda no `Neutral`; fora dele a ferramenta só refaz os sensores da etapa atual.

**Evidência:** `TryRefreshRuntimeCachesFromDebug` executa `ThreatRevisionTracker.ForceInvalidateAll` e limpa caches antes de verificar `Neutral`. A guarda existente impede republicar FOW fora de Neutral, mas não impede essas invalidações anteriores.

**Código:** `TurnStateManager.Sensors.cs`, `TryRefreshRuntimeCachesFromDebug`.

**Teste:** chamar a ferramenta durante movimento provisório e comparar revisões e consumidores dos caches antes e após cancelar.

**Critério de conclusão:** ferramentas de debug não confundem estado provisório com revisão confirmada. Prioridade menor por ser um caminho de diagnóstico, não o fluxo normal do jogador.

## Refinamentos e limites desta segunda passagem

- **Embarque:** o sensor guarda referências e motivos para candidatos inválidos, inclusive ocupantes não aliados. Os consumidores rastreados desses detalhes são logs de Console (`LogScannerPanel` e `LogEmbarkSelectionPanel`); a lista selecionável usa candidatos válidos. Não classificar esses logs como vazamento confirmado na interface normal. Ainda conferir builds de diagnóstico e ferramentas que exponham essas listas.
- **Ataque:** existe neutralização de motivo/célula/caminho de opções inválidas no ramo de destino provisório sem conhecimento confirmado. O fluxo normal usa `SensorHandle.RunAll` sem passar por essa mesma neutralização. Isso é uma diferença de cobertura a testar, não prova de que todo detalhe chegue ao HUD. Comparar terreno oculto no corredor de tiro com atacante em origem visível e em destino provisório; verificar texto, linha e disponibilidade de tiro.
- **Fusão:** foi encontrada separação entre seleção/fila e `StartMergeExecution`; as mutações de HP, combustível, camada e doadores estão na execução. Não foi demonstrada nesta passagem uma mutação desses recursos ao simplesmente abrir a prévia. Permanecem os testes de cancelamento e falha/interrupção da execução.
- **Supply e transferência:** as mudanças citadas acima ocorrem após confirmação; o problema investigado é a coerência do caminho de falha, não antecipação automática de todos os seus efeitos.
- **Replay:** há buffer provisório e promoção explícita, o que é uma proteção real. Os problemas identificados são o descarte em navegação intermediária e a referência compartilhada do trajeto.
