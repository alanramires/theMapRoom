# Relatório de 2026-11-11 — continuação após v10.9.0

O fechamento anterior permanece no relatório versionado. Este documento registra o trabalho posterior.


### Combate aéreo com vaga de montagem no rally

- Decisão do autor (2026-11-11), caso T11 Golf: aeronaves com vaga em RallyAssembly montando podem combater nos arredores da posição do montinho; a vaga deve atraí-las ao rally, não a uma presa distante.
- `AIController.AirCombat.cs`: `TryResolveAirRallyAssembly` consulta a vaga publicada por `ResolveAnyAssignedObjective` e o estado de montagem. A âncora passa a abrir pela área do rally (`Rally:Montagem@Golf`): já presente, célula atual; ausente, célula mais próxima da Vanguarda (sem faixa, célula do rally). Depois do tiro local, aproxima/segura; não entra em caça operacional, interdição, negação de escolta, patrulha do capitão ou troca por plataforma nesse ramo. Emergências que já precedem o papel continuam no roteador.
- A coleta Tática descarta a célula de tiro fora de `IsRallyParticipantPresent(... Tactical, moveTo)` antes de rodar PodeMirar. Registra candidato `Ataque.Policy`, status blocked, gate `rally_area`, motivo `fora da área do montinho`. Área e sensores existentes; sem raio novo.
- O resolvedor chamado “Any” só cobria Capturador/Assalto/FogoIndireto. Completado em `AIController.Initiative.cs` para reconhecer também as demais vagas preenchidas (incluindo Interceptador/AtaqueAereo), preservando a prioridade anterior dos três resolvedores. A vaga do plano é autoridade, não depende do badge `AIHasAssignedPlan`.
- Sem vaga de rally montando, inclusive GoGreen, o ramo antigo conserva caça, interdição e escolta. A prioridade operacional do interceptador deixa de substituir o tiro local apenas durante montagem.
- `verificar_ar_no_rally.cs`: PASS fora do Play. Bombardeiro a 10h, soldado inimigo a 1h: recusa com gate rally_area e aproxima da Vanguarda mais próxima; dentro da área ataca com célula de tiro válida; sem alvo segura; sem vaga e GoGreen voltam ao ataque antigo. Vagas de Interceptador e Disruptor reconhecidas. Nenhum batch executado ou estado confirmado comprometido.
- Regressões solicitadas PASS: `verificar_prontidao_rally`, `verificar_caca_a_controle_ceu`, `verificar_desembarque_rally_aereo`. `bash tools/compilar.sh`: ambos assemblies verdes. Nada observado em Play; confirmação do caso Golf T11 continua para o autor. Um commit de correção e parada para revisão.


### Conscrição — teto absoluto, massa elite 0 e projeção simétrica

- Decisões do autor sobre a marca T11: defesa só cancela a parada por vantagem 2:1, não o teto. `ResolveMassacrePhase` usa `byCap || (byRatio && !defense)` e explica `TETO DE CONSCRIÇÃO mantido em defesa`. Histerese e valores configurados preservados; tooltip deixou de afirmar a exceção de defesa ao teto.
- `BuySurgeMassBody` e `ComputeConscriptionTax` exigem Exército e elite exatamente 0. Cardápios só de elite não compram nem pagam imposto de massa; continuam disponíveis para a demanda normal, com os gates existentes. O imposto já excluía elites positivas; agora expressa o mesmo contrato de nível zero da compra.
- A normalização de força ficou em `BuildMacroTerritoryContext`, com fonte explícita do controller nas chamadas do plano e do `macroDominating` do rally. Conta próprias pelo mesmo `Match.GetSlotUnitCounts` que inclui embarcados; quando a capacidade de projeção está ativa, soma produtores próprios e inimigos, ambos com a mesma régua de `CanProduceUnitsForSlot` e sem piso fictício de um produtor. A inspeção continua lendo o contexto publicado. Limiares 40%/60% preservados.
- Log macro mostra conhecidas + produtores dos dois lados; `OwnProducersProjected` acompanha a parcela inimiga no fato estruturado. Normal/Fácil sem projeção ficam com o retrato atual, incluindo embarcados.
- `verificar_conscricao.cs`: PASS fora do Play. 34/60 Losing/Defensiva para; 30/60 também para; 28/60 mantém a histerese de saída 45%; 20/60 Losing conscreve; 20/60 com 2:1 e Defensiva conscreve. Fábrica Pesada só com Pesado/Campanha e aeroporto só de elite não emitem ordem de massa nem imposto; HQ emite Soldado elite 0. Hard: snapshot 30, total real 32 (dois embarcados), cinco produtores próprios contra 36 + 14: 37/87 = 42,53%, `Balanced`. Sem projeção: 32/36, `Balanced`. Produtores e MatchController do fixture são isolados da configuração visual da cena; slots e caches anteriores são restaurados.
- Regressões PASS: `verificar_shopping_sem_qg`, `verificar_shopping_rally_acesso`, `verificar_demanda_parede_artilharia`, `verificar_caca_a_controle_ceu`, `verificar_prontidao_rally`.
- `bash tools/compilar.sh`: Assembly-CSharp e Assembly-CSharp-Editor verdes. Nenhuma compra, batch ou partida executada em Play. Um commit de correção; parada para revisão do autor.


### Carga sem tarefa — montinho do transporte (decisão 2026-11-12)

- Marca T12, Chinook #72: passageiros comprometidos com Hotel, mas captura já atendida e sem alternativa. O courier agora consulta a vaga do próprio transporte antes de usar a posição atual como alvo.
- Só com todos os passageiros sem alvo válido, transporte em RallyAssembly montando e rally próprio totalmente capturado: `TryBuildAirRallyAssemblyDelivery` recebe `fallbackRally`. Reutiliza Core, sensores, busca de LZ e matching; a vaga de rally própria do passageiro continua tendo precedência. Log identifica carga sem tarefa e o montinho de destino. Sem essa âncora, mantém o comportamento anterior.
- Não altera planner, vagas dos passageiros, prontidão ou estado confirmado. Eles descem no Core; eventual recrutamento é responsabilidade do planner no turno seguinte.
- `verificar_carga_sem_tarefa_rally`: PASS fora do Play; captura já atendida e alcançável por outro capturador entrega dois no Core de Golf; sem vaga do transporte mantém hold; alvo válido preserva entrega em Hotel. Vagas e estado confirmado intactos.
- Regressões PASS: `verificar_desembarque_rally_aereo`, `verificar_prontidao_rally`, `verificar_ar_no_rally`. O primeiro fixture só adapta a chamada por reflexão ao parâmetro opcional novo.
- `bash tools/compilar.sh`: Assembly-CSharp e Assembly-CSharp-Editor verdes. Nada observado em Play nem batch executado. Um commit e parada para revisão.


### Interceptador não inicia tiro terciário (decisão 2026-11-12)

- Marca T12, Caça A #97 em Golf: ataque à AAA #38 por preferência terciária. Removido o `!assembling` que desativava `TryResolveInterceptorOperationalPursuit` durante a montagem.
- Na coleta Tática de `AIController.AirCombat.cs`, essência é `MatchesLabel(roles[0], Interceptador)`, pelo catálogo. As preferências são classificadas uma vez por alvo; terciários ficam fora de `targetCandidates` do PodeMirar e da poda de alcance. A recusa próxima à célula candidata aparece como `Ataque.Policy`, blocked, gate `interceptor_tertiary`, razão `interceptador não inicia tiro terciário`. Não altera o sensor nem o fluxo de revide. Outros papéis conservam a coleta anterior.
- `verificar_interceptador_terciario`: PASS fora do Play. Caça A e B com AAA a 1h não atacam e registram gate; com presa no Operacional perseguem; com vaga de montagem e AAA dentro da área não atacam e ficam na área. Apache mantém ataque. Nenhum batch executado.
- Diferença do cenário literal solicitado: a ficha real do Caça B considera Jet terciário (Helicopter primário, Plane secundário). Sem modificar fichas, a perseguição do A usa Caça A inimigo e a do B usa Apache inimigo. Preservada a doutrina de presas existente.
- Regressões PASS: `verificar_ar_no_rally`, `verificar_caca_a_controle_ceu`, `verificar_prontidao_rally`. `bash tools/compilar.sh`: ambos assemblies verdes. Nada observado em Play. Um commit; parada para revisão.
