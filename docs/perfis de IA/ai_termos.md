# Glossário de termos da IA

Este documento registra o vocabulário interno criado para descrever decisões, estados e comportamentos da IA. Muitos termos aparecem em logs, nomes de classes e janelas de inspeção; não são necessariamente conceitos visíveis ao jogador.

Os nomes entre crases são os identificadores mais comuns no código. O glossário deve ser atualizado quando um termo novo passar a carregar significado próprio.

## Visão estratégica

| Termo | Significado no projeto |
|---|---|
| **Snapshot** | Foto confirmada do mundo no início do turno da IA (`AIWorldSnapshot`). Contém unidades, construções, orçamento, postura e informação disponível. |
| **Stance / postura** | Orientação geral do turno: `Tactical`, `Offensive` ou `Defensive`. Controla gates e pesos, mas não substitui objetivos individuais. |
| **Tactical** | Postura padrão, sem pressão suficiente para assumir um comportamento ofensivo ou defensivo global. |
| **Offensive** | Postura que libera mais avanço, composição ofensiva e pressão sobre objetivos. |
| **Defensive** | Postura que favorece proteção da base, resposta a ameaças e composição defensiva. |
| **Macro context** | Leitura conjunta de território e força usada para limitar ou liberar objetivos ofensivos. |
| **Early Expansion** | Fase inicial (`EarlyExpansion`) com bastante território neutro; a IA prioriza expansão e ainda não aplica o cap macro normal. |
| **Balanced** | Estado macro equilibrado: a IA não está claramente ganhando nem colapsando. |
| **Collapsing** | Estado macro de desvantagem. Território ou força caíram para a faixa de risco, reduzindo aventuras ofensivas. Nos painéis aparece como **Perdendo**. |
| **Dominating** | Estado macro em que território e força estão ambos em vantagem. Nos painéis aparece como **Ganhando**. Pode liberar o fechamento da invasão sem exigir um rally local formal. |
| **Force ratio** | Proporção entre força própria e força total considerada (`minhas / (minhas + inimigas)`). |
| **Offensive cap** | Limite macro de objetivos ofensivos simultâneos. Evita espalhar o exército em frentes demais. |
| **IsInvading** | Flag paralela à postura. Indica que uma operação de invasão `GoGreen` está em andamento; a IA pode estar `Offensive` e invadindo ao mesmo tempo. |
| **Hard projection** | Projeção de força inimiga usada no modo difícil, incluindo capacidade futura de produtores conhecidos. |
| **Stalemate** | Impasse prolongado detectado pela inteligência, usado para pedir ruptura, assalto pesado ou fogo indireto. |
| **Hot score / setor hot** | Pontuação de atividade ou ameaça atribuída a um setor pela inteligência. Quanto maior, mais atenção defensiva ou operacional ele recebe. |

## Mapa operacional

| Termo | Significado no projeto |
|---|---|
| **Eixo** | Corredor operacional que sai do QG, atravessa setores ordenados e aponta para um rally ou para a base inimiga (`InvasionAxisMap.Axis`). |
| **Leque de eixos** | Conjunto de eixos de um slot, ordenados angularmente da esquerda para a direita a partir do QG. |
| **Corredor** | Lista ordenada de setores pertencentes a um eixo. Representa a progressão territorial esperada. |
| **Frente do eixo** | Primeiro setor ainda não controlado no corredor (`FrontSector`). |
| **Eixo completo** | Eixo cujo corredor e rally já estão controlados. |
| **Eixo de invasão** | Eixo sintético final entre o QG próprio e o QG inimigo. Existe para organizar transporte e ataque de encerramento. |
| **Off-axis** | Objetivo ou setor fora do eixo operacional atual. Pode ser removido quando o cap macro exige concentração. |
| **Rogue / fora de eixo** | Unidade sem slot formal no plano ou setor forçado para eixo zero. Ela usa comportamentos oportunistas e fallbacks, em vez de ficar parada. |
| **Override de eixo** | Associação manual feita pelo designer para colocar um setor em determinado eixo ou deixá-lo fora de todos. |
| **Anchor / âncora** | Célula, construção ou setor estável usado como referência de formação, defesa, logística ou cálculo de retaguarda. |
| **Anchor sector** | Setor estrutural que deve ser assegurado antes de abrir novas projeções, normalmente ligado à base econômica ou ao caminho inicial. |
| **Frontline / linha de frente** | Faixa ocupada pelas unidades de combate mais avançadas na direção do inimigo. |
| **Backline / retaguarda** | Região atrás da linha de frente onde artilharia, inteligência e logística procuram operar protegidas. |
| **Front band** | Banda geométrica usada pelo analisador de retaguarda para representar a frente atual. |
| **Screen / tela de proteção** | Unidades aliadas entre um suporte vulnerável e a ameaça. Um `screen` suficiente permite aproximar artilharia, transporte ou logística com menor risco. |
| **Gap** | Distância ou espaço operacional entre uma unidade e a formação, frente, passageiro ou cobertura que deveria acompanhá-la. |
| **Rendezvous** | Célula de encontro entre transportador e passageiro, ou ponto seguro usado para entrega intermediária. |
| **Staging** | Área de preparação anterior ao destino final; serve para concentrar ou descarregar força sem expô-la diretamente. |
| **Bridgehead / cabeça de ponte** | Presença mínima de assalto e apoio já estabelecida perto do destino de invasão. Permite que transportes avancem e desembarquem com cobertura. |

## Rally e montagem

| Termo | Significado no projeto |
|---|---|
| **Rally Point** | Construção marcada como ponto de concentração de um eixo. Depois de capturada, organiza a montagem da força antes da invasão. |
| **Rally Assembly** | Objetivo (`RallyAssembly`) que reúne capturador, assalto, fogo, inteligência e logística ao redor do rally. |
| **Rally influence** | Influência de um rally ativo sobre unidades e objetivos próximos, mesmo quando eles não estão exatamente no setor do rally. |
| **WaitHold** | Rally conquistado, mas ainda sem segurança ou presença mínima para iniciar a montagem. |
| **Assembling** | Rally reunindo os papéis e a massa exigidos. |
| **Ready** | Requisitos de montagem satisfeitos, aguardando a liberação operacional. |
| **GoGreen** | Sinal de partida: libera a força montada para iniciar a invasão. Também abre exceções de agressividade e coordenação. |
| **Expired** | Montagem encerrada ou janela operacional expirada; o rally deixa de comandar aquele agrupamento. |
| **Readiness** | Avaliação dos requisitos do rally: hold, capturadores, assalto/blindados, artilharia, inteligência, logística e ameaça. |
| **Hold local** | Força mínima mantida no rally ou objetivo para não abandonar cedo uma posição importante. |
| **Massa** | Quantidade mínima de unidades de uma função ou composição antes de liberar suporte, transporte ou ofensiva. Não significa apenas número total do exército. |
| **Core operacional** | Composição básica que precisa estar pronta ou comprometida antes de o shopping gastar em elites e luxos. |
| **Supressão do GoGreen** | Janela que preserva o estado operacional da invasão e evita remontagens ou mudanças contraditórias logo após a partida. |

## Objetivos e alocação

| Termo | Significado no projeto |
|---|---|
| **Objective / objetivo** | Tarefa persistente de equipe associada a um setor e preenchida por slots de função. |
| **CaptureSector** | Objetivo regular de conquistar ou assegurar um setor. |
| **InvasionAttack** | Objetivo dedicado ao ataque contra a base ou QG inimigo. |
| **Slot** | Vaga funcional de um objetivo, como capturador, assalto, transporte ou fogo indireto. |
| **Filled slot** | Slot que já possui uma unidade válida atribuída. |
| **Open slot / gap de composição** | Papel necessário ainda não preenchido; pode virar demanda de compra. |
| **Sticky assignment** | Atribuição preservada entre turnos para evitar que unidades troquem de objetivo continuamente. |
| **Handoff** | Transferência controlada de uma captura parcial ou objetivo para outra unidade mais apta. |
| **PartialReadyForHandoff** | Captura parcial pronta para receber um substituto sem perder a continuidade da operação. |
| **Swap** | Troca de atribuições ou posições entre unidades quando uma substituição melhora o plano. |
| **Vacate** | Ação de liberar uma célula ou construção importante para outra unidade que precisa ocupá-la. |
| **Reserved capture** | Construção ou célula de captura reservada para um capturador específico, evitando disputa entre aliados. |
| **Intent / intenção de setor** | Rótulo persistente de alto nível: `Hold`, `Attack`, `Defend`, `Avoid`, `Intercept`, `Secure` ou `Covered`. |
| **Cohesion** | Grau de coerência de uma operação: distância, slots preenchidos, screen e capacidade de agir como grupo. |

## Arquétipos de comportamento

| Termo | Significado no projeto |
|---|---|
| **Capturer / capturador** | Unidade cujo papel principal é conquistar construções e setores. |
| **Assault / assalto** | Unidade de combate direto que abre caminho, protege capturadores e ocupa perímetros. |
| **Fire Support** | Unidade de apoio de fogo, geralmente artilharia ou ataque à distância, posicionada atrás da screen. |
| **Intel** | Unidade dedicada a visão, detecção e iluminação das aproximações. |
| **Logistics** | Unidade de reparo, reabastecimento, remuniciamento ou transferência de reservas. |
| **Defender** | Comportamento de guarda, patrulha ou interceptação ligado a objetivo/setor controlado. |
| **Explorer** | Comportamento de avanço e reconhecimento quando não existe alvo tático imediato. |
| **Opportunist** | Capturador que aproveita uma captura local vantajosa sem abandonar uma obrigação superior. |
| **Pursuer** | Capturador que continua perseguindo a cadeia de objetivos ou um alvo coerente com sua rota. |
| **Ponta de lança** | Capturador ou elemento avançado que conduz a progressão territorial na frente do eixo. |
| **Blitzkrieg / blitz** | Avanço agressivo de capturadores e blindados, com prioridade em profundidade e ritmo sobre cautela local. |
| **HQ Breaker** | Comportamento de assalto especializado em romper a defesa da base/QG inimigo. |
| **Combatant** | Variante que participa diretamente do combate, em oposição ao papel puramente posicional ou de suporte. |

## Transporte e logística

| Termo | Significado no projeto |
|---|---|
| **Assigned transport** | Transportador formalmente vinculado a passageiro ou slot de uma operação. |
| **Courier** | Transportador carregado ou com entrega definida. Prioriza levar a carga ao alvo e encontrar um drop seguro. |
| **Shuttle** | Transportador livre, procurando passageiro, reboque ou oportunidade de transferência. |
| **Tow courier** | Transporte/reboque dedicado a levar fogo indireto ou outra unidade rebocável até a frente. |
| **Pickup** | Célula ou ação de coleta de passageiro. |
| **Drop / drop-off** | Célula ou ação de desembarque escolhida conforme distância, ameaça e utilidade operacional. |
| **Local opportunity drop** | Entrega oportunista próxima que aproveita um alvo útil sem desorganizar uma missão maior. |
| **Evac** | Evacuação de unidade ameaçada, encalhada ou inadequadamente exposta. |
| **Shuttle logístico** | Movimento de logística entre fontes e alvos para manter a rede de suprimento funcionando. |
| **Rear area** | Área considerada de retaguarda e, portanto, adequada a logística e recuperação. |
| **Operational pressure** | Demanda calculada a partir do progresso dos eixos, profundidade, cobertura existente e lacunas de transporte/logística. |

## Shopping e recrutamento

| Termo | Significado no projeto |
|---|---|
| **Shopping** | Fase de compra da IA. Converte slots, ameaças, pressão operacional e reservas em escolha de unidades. |
| **Demand / demanda** | Necessidade quantificada por um papel ou capacidade. Pode vir do plano, inteligência, composição ou pressão operacional. |
| **Recrutamento** | Nome conceitual para transformar demandas em novas unidades compradas. |
| **Conscription / conscrição** | Doutrina que compra o corpo mais barato em todo produtor antes de gastar o excedente em demandas e elites. |
| **Formigueiro / doutrina do enxame** | Nome de dificuldade/comportamento que mantém conscrição permanente e privilegia massa. |
| **Conscription when losing** | Recrutamento forçado emergencial ativado apenas quando a IA está perdendo. |
| **Reserve / reserva de compra** | Dinheiro protegido para uma compra futura importante, como elite, transporte aéreo, inteligência ou antiaéreo. |
| **Safety buffer / colchão** | Margem preservada além do custo-alvo para não quebrar a economia ao comprar uma unidade cara. |
| **Free spending / gasto livre** | Orçamento restante depois de descontar reservas estratégicas. |
| **Elite reserve** | Reserva dirigida a uma unidade elite específica quando o núcleo operacional já permite esse investimento. |
| **Counter pressure** | Demanda por contramedidas gerada pela composição ou atividade inimiga conhecida. |
| **Preventive demand** | Compra antecipada baseada em risco provável, antes de existir uma emergência imediata. |
| **Defensive burst** | Abertura rápida de slots defensivos quando várias operações estão expostas ou a defesa está saturada. |
| **Breakthrough / ruptura** | Resposta de composição para quebrar parede de artilharia, blindagem ou impasse. |
| **Hard blitz** | Regra do modo difícil que prioriza massa e blindados de avanço antes de certos investimentos elite. |
| **Decisive fire** | Compra de fogo pesado tratada como capaz de decidir um impasse, recebendo bônus especial no seletor. |

## Termos de execução e diagnóstico

| Termo | Significado no projeto |
|---|---|
| **Planner** | Camada que cria, mantém e pontua objetivos de equipe. |
| **Router** | Camada que encaminha uma unidade para o comportamento adequado conforme papel e contexto. |
| **Evaluator** | Rotina que pontua células, objetivos, ataques ou candidatos. |
| **Batch** | Ação completa planejada pela IA e executada pela máquina de estados oficial. |
| **Fast AI / IA rápida** | Modo de apresentação que executa batches sem sustentar delays e navegação visual intermediária. |
| **Initiative / iniciativa** | Ordem calculada de atuação das unidades no turno, usada para que ações preparatórias ocorram antes das dependentes. |
| **Fallback** | Caminho alternativo seguro quando a decisão especializada não encontra ação válida. |
| **Gate** | Condição obrigatória que bloqueia ou libera um comportamento, demanda ou compra. |
| **Score** | Valor comparável usado para ordenar candidatos; não representa uma probabilidade. |
| **Pressure** | Pontuação acumulada que expressa necessidade, ameaça ou incentivo. O significado exato depende do subsistema. |
| **Coverage / cobertura** | Quantidade existente ou atribuída comparada à quantidade desejada de uma função. |
| **Projection** | Estimativa de necessidade futura baseada em distância, produtores, eixos ou força ainda não observada diretamente. |
| **Commitment / comprometimento** | Unidade, dinheiro ou slot já reservado para uma necessidade, mesmo antes da conclusão física da ação. Não deve ser confundido com o compromisso transacional do tabuleiro. |

## Convenções de leitura dos logs

- `cap`, `ass`, `fire`, `trans`, `log` e `intel` normalmente significam capturador, assalto, fogo indireto, transporte, logística e inteligência.
- `active`, `assigned`, `desired`, `open` e `gap` significam, respectivamente, existente, atribuído, desejado, ainda aberto e déficit.
- Distâncias terminadas em `h` são distâncias em hexes.
- `reason` explica por que uma opção foi aceita ou rejeitada; `score` permite comparar opções do mesmo seletor.
- Termos iguais em subsistemas diferentes podem ter pesos diferentes. Por exemplo, `pressure` de infantaria, pressão territorial e pressão operacional não usam necessariamente a mesma escala.

