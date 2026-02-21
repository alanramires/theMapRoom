# Relatorio Tecnico - v1.0.0 a v1.0.3

## Escopo

- Base: `v1.0.0` (`6a41b98`) - `the map room - validando movimentos`
- Incrementos:
  - `v1.0.1` (`8afc15d`) - `antes da refatoracao`
  - `v1.0.2` (`ac555d8`) - `logistica`
  - `v1.0.3` (`67bd7bc`) - `sensores de mira, ajustes no cursor, linha de tiro, utilitarios de unidade`

Resumo de impacto no intervalo `v1.0.0..v1.0.3`:
- `336 files changed`
- `59238 insertions`
- `14232 deletions`

---

## 1) Base funcional em v1.0.0

### Fundacao do jogo em hex

Arquitetura inicial consolidada com:
- grade hexagonal e utilitarios de coordenada/caminho:
  - `Assets/Scripts/Hex/Core/HexCoordinates.cs`
  - `Assets/Scripts/Hex/Path/HexPathResolver.cs`
- controle de turno e estados:
  - `Assets/Scripts/Match/TurnStateManager.cs`
  - `Assets/Scripts/Match/TurnState/*`
- fluxo de movimento com range/ocupacao:
  - `Assets/Scripts/Units/Rules/UnitMovementPathRules.cs`
  - `Assets/Scripts/Units/Rules/UnitOccupancyRules.cs`

### Catalogos e dados iniciais

Primeira versao de bancos de dados de gameplay:
- unidades (`Assets/DB/Unit/*`)
- terrenos (`Assets/DB/terrain/*`)
- construcoes (`Assets/DB/construction/*`)
- estruturas/rotas (`Assets/DB/structures/*`)

### Base visual e ferramentas

Entraram os elementos editoriais/visuais que sustentam o loop:
- spawners e editores de unidade/construcao
- prefab de unidade e cena de teste
- tile palette inicial, cursor, HUD e audio de interface/movimento

---

## 2) v1.0.1 - consolidacao pre-refatoracao

### Foco da versao

Expansao de mapa tatico e preparacao de regras orientadas a dados.

### Mudancas principais

1. Skills e custos por contexto:
- introducao de `SkillData`/`SkillDatabase`
- overrides de custo de terreno por skill (`TerrainSkillCostOverride`)

2. Estruturas de rota (estrada/ponte):
- evolucao de `RoadNetworkManager`
- ampliacao de `StructureData`/`StructureDatabase`
- fortalecimento do painter de rota e ocupacao de estrutura

3. Ajustes de unidade/movimento:
- evolucao de `UnitData` e `UnitManager`
- atualizacoes em `UnitMovementPathRules` e `TurnStateManager`

4. Pacote de assets:
- novos terrenos/estruturas/tiles de rodovia e ponte
- atualizacoes relevantes na `SampleScene`

---

## 3) v1.0.2 - camada de logistica

### Foco da versao

Introduzir economia operacional (servicos, suprimentos, recursos/sensores) no modelo de dados e runtime.

### Mudancas principais

1. Novo dominio de dados de logistica:
- `Assets/Scripts/Services/*`
- `Assets/Scripts/Supplies/*`
- `Assets/Scripts/Resources/*`
- `Assets/Scripts/Construction/ConstructionSupplierSettings.cs`
- `Assets/Scripts/Construction/ConstructionSupplyOffer.cs`

2. Modelagem de unidade e arma mais rica:
- embarques de armas/suprimentos/recursos em `UnitData`
- expansao dos bancos:
  - `Assets/DB/Logistic/*`
  - `Assets/DB/Weapons/*`
  - `Assets/DB/Recursos/*`

3. Ferramentas de authoring:
- editores custom para Service/Supply/Weapon/Construction/Unit
- melhor suporte para manter consistencia de dados em massa

4. Atualizacao de catalogo de faccoes:
- ampliacao de unidades e parametros para exercito/aeronautica/marinha

---

## 4) v1.0.3 - sensor de mira, linha de tiro e DPQ

### Foco da versao

Dar capacidade real de decisao de combate antes da resolucao final.

### Mudancas principais

1. Sistema de sensor de ataque (`PodeMirar`):
- `Assets/Scripts/Sensors/PodeMirarSensor.cs`
- `Assets/Scripts/Sensors/PodeMirarTargetOption.cs`
- `Assets/Scripts/Sensors/PodeMirarInvalidOption.cs`
- `Assets/Scripts/Sensors/SensorHandle.cs`

2. Linha de tiro e visualizacao:
- pintura de area/linha valida em estado de turno
- debug dedicado:
  - `Assets/Editor/PodeMirarSensorDebugWindow.cs`

3. Camada DPQ inicial:
- introducao do pacote `Assets/Scripts/DPQ/*`
- banco `Assets/DB/DPQ/*`
- suporte a leitura de posicao/altura no combate e sensores

4. Ajustes de UX/timing de turno:
- refinamentos no cursor, scanner prompt e animacao de movimento
- utilitarios de unidade e tuning de cena para validacao rapida

---

## 5) Estado consolidado ao final de v1.0.3

Ao fechar `v1.0.3`, o projeto ja tinha:

1. **Nucleo tatico operacional**
- movimento em hex, ocupacao e estados de turno estaveis.

2. **Modelo de dados amplo**
- unidades, armas, skills, suprimentos, servicos, recursos, terrenos e estruturas.

3. **Pipeline de combate preparatorio**
- sensor de mira + linha de tiro + DPQ, com ferramentas de debug.

4. **Ferramentas de editor maduras**
- authoring e tuning com janelas dedicadas para os principais subsistemas.

Esse conjunto foi a base direta para a etapa seguinte (`v1.0.4`), onde entrou a matriz de combate RPS/DPQ e a resolucao de combate aprofundada.

