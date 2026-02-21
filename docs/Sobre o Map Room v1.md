# The Map Room v1

## Visao geral

The Map Room e um jogo tatico por turnos em grade hexagonal, com combate orientado a dados e foco em legibilidade de simulacao.

O estado atual prioriza:

- previsibilidade de combate (formulas e trace)
- tuning rapido por ferramentas de editor
- separacao clara entre dados (assets) e regra (scripts)
- regras de embarque e operacao aerea dirigidas por contexto de mapa

## Loop principal (estado atual)

1. Selecionar unidade e movimentar.
2. Rodar sensores taticos (ex.: `PodeMirar`, `PodeEmbarcar`, `L` para operacao aerea).
3. Escolher acao (combater, embarcar, pousar/decolar, apenas mover).
4. Resolver combate com:
   - RPS base
   - Skill de elite (quando ativa)
   - DPQ de posicao
   - revide do defensor (quando valido)
5. Aplicar dano e encerrar acao.

## Sistemas implementados

## 1) Sensor de combate (`PodeMirar`)

Arquivos:

- `Assets/Scripts/Sensors/PodeMirarSensor.cs`
- `Assets/Scripts/Sensors/PodeMirarTargetOption.cs`
- `Assets/Scripts/Sensors/PodeMirarInvalidOption.cs`

Entregas:

- valida alcance, dominio/altura e operacao da arma
- valida LoS/LdT conforme trajetoria
- calcula opcoes validas/invalidas com motivo
- calcula possibilidade de revide e arma de revide

## 2) Sensor de embarque (`PodeEmbarcar`)

Arquivos:

- `Assets/Scripts/Sensors/PodeEmbarcarSensor.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarOption.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarInvalidOption.cs`
- `Assets/Editor/PodeEmbarcarSensorDebugWindow.cs`

Entregas:

- varredura de transportadores adjacentes (range 1)
- validacao por contexto do transportador:
  - `allowedEmbarkTerrains`
  - `allowedEmbarkConstructions`
  - fallback por dominio/altura do hex quando listas vazias
- bloqueio de transportador em dominio aereo
- validacao de slot por:
  - camadas permitidas (lista de domain/height)
  - classe permitida
  - skills obrigatorias/bloqueadas
  - capacidade
- retorno de validos e invalidos com motivo detalhado

## 2) LoS e LdT por regra de terreno/camada

Referencias:

- `docs/regras de LoS e LdT.md`
- `Assets/Scripts/Sensors/PodeMirarSensor.cs`
- `Assets/Scripts/Terrain/TerrainVisionResolver.cs`

Estado atual:

- `Straight`: usa bloqueio por percurso e EV/BlockLoS
- `Parabolic`: validacao de destino + checks basicos (sem percurso completo)
- override de ar por `DPQAirHeightConfig` (`AirLow`/`AirHigh`)

## 3) Combate (runtime)

Arquivo central:

- `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`

Pontos chave:

- consumo de municao de atacante e revide
- RPS de ataque e defesa por classe/categoria
- soma de modificadores de elite via skill
- defesa efetiva com DPQ
- matchup DPQ para arredondamento do dano
- trace completo no log

## Ordem oficial de altitude (HeightLevel)

Referencia de codigo: `Assets/Scripts/Units/DomainManager.cs`

- `Submerged = 2`
- `Surface = 3` (Land/Naval)
- `AirLow = 4`
- `AirHigh = 5`

Regra de projeto:

- manter essa ordem numerica como base para comparacoes de camada/altura
- evitar alterar esses valores sem migracao explicita dos sistemas dependentes

## 4) RPS base por dados

Arquivos:

- `Assets/Scripts/RPS/RPSData.cs`
- `Assets/Scripts/RPS/RPSDatabase.cs`
- `Assets/DB/RPS/*`

Caracteristica:

- match exato por chave de classe e categoria de arma
- sem match => bonus `+0`

## 5) Elite Skill (inflexao de combate)

Arquivos:

- `Assets/Scripts/Skills/SkillData.cs`
- `Assets/Scripts/Units/UnitData.cs`
- `docs/Elite.md`

Capacidades:

- `eliteLevel` por unidade (default `0`)
- skill condicional por classe/categoria/comparacao de elite
- 4 modificadores independentes:
  - owner attack
  - owner defense
  - opponent attack
- opponent defense

## 6) Operacoes aereas (`Landing/Takeoff`)

Arquivos:

- `Assets/Scripts/Units/Rules/AircraftOperationRules.cs`
- `Assets/Scripts/Construction/ConstructionData.cs`
- `Assets/Scripts/Structures/StructureData.cs`
- `Assets/Scripts/Terrain/TerrainTypeData.cs`

Caracteristicas:

- regras de pouso/decolagem movidas para contexto do hex (construcao/estrutura/terreno)
- unidade aerea definida por classe (`Jet`, `Plane`, `Helicopter`)
- permissao de pouso por:
  - classes permitidas
  - skills requeridas
- permissao de decolagem por:
  - modos permitidos (`MoveuParado`, `MoveuAndando`)
- suporte a pista improvisada e variacoes futuras sem hardcode em `UnitData`

## 7) Construction Configuration (captura e mercado)

Arquivos:

- `Assets/Scripts/Construction/ConstructionSiteRuntime.cs`
- `Assets/Scripts/Construction/ConstructionManager.cs`
- `Assets/Editor/ConstructionDataEditor.cs`
- `Assets/Editor/ConstructionFieldDatabaseEditor.cs`
- `Assets/Editor/ConstructionSpawnerEditor.cs`

Campos novos:

- `capturedIncoming` (default `1000`)
- `canProduceAndSellUnits` (lista de regras):
  - `FreeMarket`
  - `OriginalOwner`

Comportamento:

- `FreeMarket`: vende/produz para o dono atual (exceto neutro)
- `OriginalOwner`: vende/produz apenas para o dono original

## 8) Ferramentas de simulacao (editor)

Menu:

- `Tools/Combat/Calcular Combate`
- `Tools/Combat/Matriz de Combate`

Arquivos:

- `Assets/Editor/CombatCalculatorWindow.cs`
- `Assets/Editor/CombatMatrixWindow.cs`

Estado atual:

- alinhadas com runtime (RPS + Elite + DPQ)
- exibem elite de atacante/defensor no log
- matriz 5x5 de DPQ com baseline (`DPQ_Padrao x DPQ_Padrao`)

## 9) Ajustes recentes de dados e inspector

1. `UnitManager` inspector:
- `Has Acted` reposicionado para cima
- `Match Controller` reposicionado junto dos controllers/databases
- `Current Ammo` e `Max Ammo` removidos da UI da instancia

2. Animacao de movimento:
- velocidade custom por unidade movida para `AnimationManager`
- override por par `UnitData/speed`
- sem match, velocidade padrao `1`

3. Slots de transporte:
- removidos booleans redundantes de filtro
- modos permitidos em lista dedicada (`TransportSlotLayerMode`)

## Diferencial do v1

O projeto ja tem um nucleo de combate tatico funcional com boa observabilidade e regras de movimento/embarque mais robustas:

- log detalhado para debug de balanceamento
- simuladores para validar matchup sem depender de play completo
- sensores com lista de invalidacao e motivo
- estrutura de dados pronta para evoluir faccoes, classes, counters e operacoes de mobilidade

## Limites atuais (conhecidos)

- parte de LoS/LdT ainda difere por tipo de trajetoria (`Straight` vs `Parabolic`)
- balanceamento ainda em tuning iterativo de assets
- cena de teste e dados de combate seguem em evolucao frequente
- `supplierResources.maxCapacity` ainda nao esta conectado ao runtime logistico

## Documentos de apoio

- `docs/Combat.md`
- `docs/Elite.md`
- `docs/Sensor PodeMirar.md`
- `docs/regras de LoS e LdT.md`
- `docs/RELATORIO_V1.0.4.md`
- `docs/RELATORIO_V1.0.6.md`
- `docs/RELATORIO_V1.0.7.md`
