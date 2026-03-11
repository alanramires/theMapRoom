# Avaliacao Arquitetural e Modelagem Conceitual do Repositorio

## Escopo da avaliacao
Analise do projeto como sistema completo, ignorando estilo de codigo e focando em:
- arquitetura
- modelos conceituais
- niveis de abstracao
- evidencias de design de sistemas e pensamento algoritmico

## Visao sistemica
O repositorio organiza o jogo como um sistema tatico por camadas:
1. catalogos de dados (`*Data`, `*Database`)
2. entidades runtime (`*Manager`)
3. regras e resolvers puros (`*Rules`, `*Resolver`, `*Sensor`)
4. orquestracao de fluxo de turno (`TurnStateManager` + `MatchController`)
5. interface de apoio/inspecao (`PanelHelperController`, HUDs)

O resultado e um desenho orientado a regras declarativas com execucao imperativa: os dados descrevem capacidades e restricoes; os resolvers calculam elegibilidade/custos; os managers aplicam efeitos no estado do jogo.

## Niveis de abstracao usados pelo autor

### 1) Nivel de dados (catlogos declarativos)
O autor modela entidades do jogo em `ScriptableObject`, com identidade, parametros e validacao:
- `UnitData`, `ConstructionData`, `WeaponData`, `ServiceData`, `TerrainTypeData`, `DPQData`, `RPSData`
- bancos indexados por ID: `UnitDatabase`, `ConstructionDatabase`, `WeaponDatabase`, `DPQDatabase`, `RPSDatabase`

Evidencia de pensamento de sistema:
- separacao entre "definicao de regra" e "estado runtime"
- normalizacao e sanidade em `OnValidate` para proteger invariantes de dominio
- lookup por ID para permitir composicao entre sistemas sem acoplamento direto de cena

### 2) Nivel de dominio (linguagem do problema)
O dominio e explicitamente modelado por conceitos militares/taticos:
- `Domain`, `HeightLevel`, `MovementCategory`, `GameUnitClass`, `SupplierTier`, `ServiceType`, `TakeoffProcedure`
- regras de camada (terra/ar/naval/submerso), pouso/decolagem, stealth/deteccao, captura, suprimento, transferencia, fusao, embarque/desembarque

Evidencia:
- `UnitData` e `ConstructionData` concentram semantica de capacidades e restricoes
- `AirOperationResolver` formaliza hierarquia de contexto (Construcao > Estrutura > Terreno)
- `TerrainVisionResolver` compoe EV/LoS entre terreno, estrutura, construcao e altura aerea

### 3) Nivel de mecanicas (regras operacionais)
As mecanicas sao encapsuladas em modulos especializados:
- movimento/path: `UnitMovementPathRules`, `HexPathResolver`
- ocupacao: `UnitOccupancyRules`, `ConstructionOccupancyRules`, `StructureOccupancyRules`
- combate: `PodeMirarSensor`, `CombatModifierResolver`, `DPQCombatMath`, `RPSDatabase`
- logistica/servicos: `ServicoDoComandoSensor`, `ServiceCostFormula`, `ServiceLogisticsFormula`
- operacao aerea: `AircraftOperationRules`, `AirOperationResolver`

Evidencia:
- regras calculam "pode / nao pode" com motivo explicito (diagnostico de invalidez)
- composicao de modificadores (RPS + elite + DPQ + servicos + camada)
- separacao de calculo de custo/ganho vs aplicacao de efeitos

### 4) Nivel de fluxo de turno (orquestracao temporal)
O fluxo macro e micro esta separado:
- macro-turno/economia: `MatchController`
- micro-fluxo de acao da unidade: `TurnStateManager` (classe parcial em varios arquivos)

Evidencia:
- `MatchController` administra ciclo de times, upkeep de autonomia, economia por turno e transicao audiovisual
- `TurnStateManager` implementa maquina de estados de cursor/acao (`Neutral`, `UnitSelected`, `MoveuAndando`, `Mirando`, etc.)
- a divisao por arquivos parciais (`StateMachine`, `Movement`, `Sensors`, `Combat`, `Supply`, `Transfer`, `HelperPanel`) mostra decomposicao por subdominio de interacao

### 5) Nivel de interface e observabilidade
A UI nao decide regra; ela apresenta estado e justificativas:
- `PanelHelperController` consome `HelperPanelData` produzido pelo `TurnStateManager`
- overlays de alcance/linha de tiro/ameaca e paineis de "motivos invalidos"

Evidencia:
- pipeline "estado -> dados de helper -> renderizacao"
- infraestrutura de revisao de ameaca (`ThreatRevisionTracker`) para invalidacao/caching de overlays
- mensagens orientadas a decisao do jogador (confirmacoes, pre-visualizacao de custo e impacto)

## Evidencias de raciocinio de design de sistemas

### A. Separacao entre decisao e execucao
Padrao recorrente no codigo:
- sensores e regras calculam opcoes validas (`CollectOptions`, `CollectTargets`, `Evaluate`)
- manager executa ordem e aplica mutacao no estado (coroutines, consumo de recursos, mudanca de camada)

Impacto arquitetural:
- reduz acoplamento entre regra tatico-dominio e input/UI
- facilita debug, explicabilidade e futura automacao (IA, replay, testes)

### B. Uso consistente de modelos declarativos + runtime
Padrao:
- `*Data` define semantica fixa
- `*Manager` carrega estado mutavel em campo
- `*Database` garante lookup e integridade minima

Impacto:
- facilita iteracao de balanceamento sem reescrever logica
- separa configuracao de conteudo da simulacao runtime

### C. Controle de complexidade por particionamento funcional
`TurnStateManager` foi fragmentado em partes por tema. Isso e sinal de preocupacao com:
- coesao por feature
- reducao de risco de regressao por modulo
- crescimento incremental sem colapsar em um unico arquivo monolitico

### D. Modelo de invalidacao/caching para custo computacional
`ThreatRevisionTracker` + cache de overlay no helper representam pensamento de performance sistêmica:
- revisoes globais e por time
- invalidacao por eventos relevantes (movimento, camada, time, flags de match)
- evita recomputar ameaças a cada frame sem necessidade

### E. Regras explicaveis (design orientado a diagnostico)
Sensores retornam nao apenas booleano, mas:
- listas validas
- listas invalidas
- motivos textuais mapeaveis por reason ID

Isso mostra desenho voltado a:
- UX de jogo tatico (jogador entende o porque do bloqueio)
- observabilidade de sistemas complexos

## Evidencias de modelagem de dominio

### 1) Dominio multi-camada (espaco tatico)
O autor nao trata "posicao" como 2D simples; usa:
- hex + dominio + nivel de altura
- compatibilidade de camada para travessia, combate, deteccao e pouso/decolagem

Isso aparece em:
- `UnitRulesDefinition.CanPassThrough`
- `UnitMovementPathRules.CanTraverseCell`
- `AirOperationResolver`
- `TerrainVisionResolver`

### 2) Dominio logistico-economico integrado
Suprimento nao e efeito isolado:
- depende de estoque, eficiencia por classe, limite por turno, custo monetario e servicos disponiveis
- `ServiceLogisticsFormula` e `ServiceCostFormula` centralizam esse calculo
- `ServicoDoComandoSensor` e `TurnStateManager.CommandService` conectam regra a execucao por fila

### 3) Dominio de combate composicional
Combate combina:
- alcance e tipo de trajeto
- LoS/LdT/spotter/stealth
- categoria de arma
- RPS
- modificadores de elite
- DPQ para arredondamento e resultado final

Ha clareza de "pipeline de resolucao", nao um unico bloco ad-hoc.

### 4) Dominio de infraestrutura de turno
Macro-regras de partida:
- sequencia de jogadores, neutral opcional, ciclo de turno
- upkeep no inicio do turno
- economia por renda de construcao

Micro-regras de acao:
- selecao -> movimento -> sensores -> confirmacao -> execucao -> commit/rollback

## Evidencias de pensamento algoritmico

### 1) Busca e custo em pathfinding
`UnitMovementPathRules.CalcularCaminhosValidos` usa exploracao em fronteira com estado enriquecido:
- posicao
- custo de movimento
- custo de autonomia
- flags de bonus de estrada

Tambem escolhe "melhor estado por destino" por menor custo (autonomia) e desempate por passos.

### 2) Heuristica direcional
`HexPathResolver.TryResolveDirectionalFallback` usa produto escalar + penalidade de distancia para selecionar fallback coerente com direcao desejada.

### 3) Composicao incremental de custos/logistica
`ServiceLogisticsFormula` simula consumo de estoque e ganhos por budget/limite, com suporte a simulacao parcial e tracking de consumo por supply.

### 4) Ordenacao e normalizacao de filas
`ServicoDoComandoSensor` classifica opcoes por contexto (embarcados, assento, coordenada, label) e reordena familias de transporte para preservar consistencia operacional.

### 5) Controle de estado transacional no fluxo da unidade
No `TurnStateManager.Movement` ha snapshot/rollback de caminho e camada forçada:
- commit de caminho
- preparacao de custos
- retorno animado com restauracao de estado em cancelamento

Isso caracteriza pensamento de "transacao de acao", nao apenas mudanca direta de variaveis.

## Conclusao
O autor trabalha com uma arquitetura de jogo tatico orientada a dominio, com boa separacao entre:
- dados declarativos
- regras de elegibilidade/calculo
- orquestracao de turno
- interface explicativa

As evidencias no codigo indicam maturidade em design de sistemas: modelagem explicita de conceitos de guerra por camadas, pipeline de resolucao de mecanicas, estrategias de invalidacao/caching e preocupacao com legibilidade operacional (motivos de invalidacao, previews, confirmacoes e rollback). O pensamento algoritmico aparece principalmente em pathfinding com restricoes multiplas, ordenacao de filas de servico, composicao de custos e resolucao de combate/logistica baseada em regras.
