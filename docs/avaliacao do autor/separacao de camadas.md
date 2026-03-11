# Separacao de camadas

## 1) O sistema possui separacao clara entre modelo de dados, regras de dominio, fluxo do jogo e interface?
Sim. A separacao existe e e consistente na maior parte do projeto.

### Modelo de dados
Camada declarativa baseada em `ScriptableObject`, com identidade e parametros de jogo:
- `UnitData`, `WeaponData`, `ConstructionData`, `TerrainTypeData`, `ServiceData`, `DPQData`, `RPSData`
- catalogos: `UnitDatabase`, `WeaponDatabase`, `ConstructionDatabase`, `DPQDatabase`, `RPSDatabase`

Papel: descrever o "que existe" no jogo (atributos, capacidades, restricoes), sem controlar fluxo de input/turno.

### Regras de dominio
Camada de validacao e calculo de mecanicas:
- movimento/ocupacao: `UnitMovementPathRules`, `UnitOccupancyRules`, `UnitRulesDefinition`
- ar/camadas: `AircraftOperationRules`, `AirOperationResolver`
- combate: `PodeMirarSensor`, `CombatModifierResolver`, `DPQCombatMath`, `RPSDatabase`
- visao: `TerrainVisionResolver`
- logistica: `ServicoDoComandoSensor`, `ServiceCostFormula`, `ServiceLogisticsFormula`
- sensores de acao: familia `Pode*Sensor`

Papel: responder "pode ou nao pode" e "quanto custa/quanto recupera", com regras taticas.

### Fluxo do jogo
Camada de orquestracao temporal e de estado:
- macrofluxo de partida: `MatchController` (turno, time ativo, economia, upkeep, presets)
- microfluxo por unidade: `TurnStateManager` (state machine de cursor/acao, commits, cancelamentos, execucao)

Papel: coordenar ordem de eventos e transicoes de estado.

### Interface / visualizacao
Camada de exibicao e apoio a decisao:
- `PanelHelperController`, `PanelDialogController`, `PanelTurnController`, HUDs
- overlays de alcance/linha de tiro/ameaca
- apresentacao de mensagens, previews e motivos de invalidade

Papel: mostrar estado e feedback ao jogador, sem concentrar regra tatico-dominio.

## 2) Quais partes mostram pensamento arquitetural explicito?
As evidencias mais claras sao:

- Padrao "Data + Runtime + Rules":
  - dados imutaveis/configuraveis em `*Data`
  - estado vivo em `*Manager` (`UnitManager`, `ConstructionManager`)
  - regras/calculo em `*Rules`, `*Resolver`, `*Sensor`
  - isso reduz acoplamento e permite evoluir balanceamento sem reescrever fluxo.

- Separacao entre decisao e execucao:
  - sensores e resolvers coletam opcoes/invalidos;
  - `TurnStateManager` executa a acao escolhida (coroutines, commit/rollback, consumo de recursos).
  - isso e arquitetura orientada a pipeline de decisao.

- Decomposicao do orquestrador por subdominio:
  - `TurnStateManager` parcial em arquivos dedicados (`StateMachine`, `Movement`, `Sensors`, `Combat`, `Supply`, `Transfer`, `HelperPanel`, etc.).
  - mostra intencao arquitetural de coesao por feature e controle de complexidade.

- Modelo de invalidacao/caching transverso:
  - `ThreatRevisionTracker` + cache de overlays de ameaca.
  - evidencia preocupacao com custo computacional e consistencia entre regras e visualizacao.

- Regras explicaveis por design:
  - sensores retornam listas validas e invalidas com motivo, nao apenas booleano.
  - isso conecta arquitetura de dominio com UX de jogo tatico (explicabilidade).

## Conclusao
O sistema apresenta separacao de camadas clara e intencional. A arquitetura nao parece acidental: ha um desenho explicito em torno de modelo declarativo, motores de regra e orquestracao de fluxo, com interface consumindo resultados dessas camadas.
