# Relatorio Tecnico - v1.0.7

## Versoes cobertas

- `v1.0.7` - commit `e21737d` - `v1.0.7 - embarque, take off and landing (inicio)`
- Branch: `main`

Este relatorio resume as principais mudancas entregues na versao `v1.0.7`.

---

## 1) Sensor Pode Embarcar (validos + invalidos)

### Objetivo

Dar visibilidade completa do motivo de bloqueio de embarque e estabilizar regras de validacao de transportador/slot.

### O que entrou

1. Estrutura de invalidacao detalhada:
- novo tipo `PodeEmbarcarInvalidOption`;
- retorno de lista de validos e invalidos, com motivo por etapa.

2. Ferramenta de debug de sensor:
- `Tools > Sensors > Pode Embarcar` agora mostra:
  - transportadores validos;
  - resultados invalidos com motivo (contexto, slot, custo, movimento, lotacao, etc).

3. Regra de contexto e slot:
- embarque exige transportador adjacente valido;
- valida contexto por `allowedEmbarkTerrains` / `allowedEmbarkConstructions`;
- se listas estiverem vazias, usa fallback por compatibilidade de dominio/altura do transportador com o hex.

4. Regras solicitadas de dominio:
- transportador em `Domain.Air` bloqueia embarque;
- removida regra anterior de "dominios diferentes com mesma altura".

5. Custo de embarque:
- usa custo normal de entrada quando possivel;
- com fallback para custo do terreno base (incluindo overrides de skill) quando o passageiro nao pisaria no hex do transportador.

### Arquivos-chave

- `Assets/Scripts/Sensors/PodeEmbarcarSensor.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarOption.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarInvalidOption.cs`
- `Assets/Editor/PodeEmbarcarSensorDebugWindow.cs`

---

## 2) Refactor de slots de transporte

### Objetivo

Remover redundancias e deixar o modelo de slot mais flexivel.

### O que entrou

1. Simplificacao de filtros:
- removidos `filterAllowedClasses` e `requireAllSkills` do slot;
- lista vazia = sem restricao;
- lista preenchida = validacao ativa.

2. Camadas permitidas por lista:
- `allowedDomain/allowedHeight` migrado para lista de modos;
- criado tipo dedicado `TransportSlotLayerMode` (sem sprites).

3. Compatibilidade de dados:
- migracao automatica de dados legados no `OnValidate/EnsureDefaults`.

### Arquivos-chave

- `Assets/Scripts/Units/UnitTransportSlotRule.cs`
- `Assets/Scripts/Units/TransportSlotLayerMode.cs`
- `Assets/Scripts/Units/UnitData.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarSensor.cs`

---

## 3) Landing/Takeoff: regras movidas para contexto (Construction/Structure/Terrain)

### Objetivo

Tirar regras aereas do `UnitData` e centralizar permissao no hex/contexto.

### O que entrou

1. `UnitData` simplificado:
- removidos campos de pouso/decolagem por unidade (`canLandOn...`, `takeoffConsumes...`, `preferredAirHeight`, `aircraftType`, etc).
- classe da unidade (`Jet/Plane/Helicopter`) passa a definir se e aeronave.

2. Novos campos de contexto:
- `ConstructionData`:
  - `landingAllowedClasses`
  - `landingRequiredSkills`
  - `takeoffAllowedMovementModes`
- `StructureData`:
  - `landingAllowedClasses`
  - `landingRequiredSkills`
  - `roadLandingRequiresStoppedClasses`
  - `takeoffAllowedMovementModes`
- `TerrainTypeData`:
  - `landingAllowedClasses`
  - `landingRequiredSkills`
  - `takeoffAllowedMovementModes`

3. Reescrita de `AircraftOperationRules`:
- validacao de pouso/decolagem baseada no contexto do hex;
- takeoff controlado por lista de modo permitido (`MoveuParado` / `MoveuAndando`);
- altura aerea preferida inferida do dominio/camada da unidade.

### Arquivos-chave

- `Assets/Scripts/Units/Rules/AircraftOperationRules.cs`
- `Assets/Scripts/Units/UnitData.cs`
- `Assets/Scripts/Construction/ConstructionData.cs`
- `Assets/Scripts/Structures/StructureData.cs`
- `Assets/Scripts/Terrain/TerrainTypeData.cs`
- `Assets/Editor/UnitDataEditor.cs`

---

## 4) Construction Configuration: captura e mercado

### Objetivo

Adicionar parametros de economia/captura por construcao.

### O que entrou

1. Novo valor de captura:
- `capturedIncoming` (default `1000`).

2. Politica de producao/venda por lista:
- `canProduceAndSellUnits` com:
  - `FreeMarket`: vende para quem capturou (exceto neutro);
  - `OriginalOwner`: vende apenas para o dono original.

3. `ConstructionManager` atualizado:
- rastreia `originalOwnerTeamId`;
- `CanProduceUnits` agora respeita a politica configurada.

4. Editores atualizados:
- `ConstructionDataEditor`
- `ConstructionFieldDatabaseEditor`
- `ConstructionSpawnerEditor`

### Arquivos-chave

- `Assets/Scripts/Construction/ConstructionSiteRuntime.cs`
- `Assets/Scripts/Construction/ConstructionManager.cs`
- `Assets/Editor/ConstructionDataEditor.cs`
- `Assets/Editor/ConstructionFieldDatabaseEditor.cs`
- `Assets/Editor/ConstructionSpawnerEditor.cs`

---

## 5) Ajustes de inspector e animacao

1. `UnitManager` inspector:
- removeu `Current Ammo` / `Max Ammo` da UI;
- `Has Acted` movido para o topo logico;
- `Match Controller` reposicionado junto de controllers/databases.

2. Velocidade de movimento:
- override por `UnitData` movido para `AnimationManager`;
- sem match na lista, velocidade padrao = `1`.

### Arquivos-chave

- `Assets/Editor/UnitManagerEditor.cs`
- `Assets/Scripts/Match/Animation/AnimationManager.cs`
- `Assets/Scripts/Units/UnitManager.cs`

---

## Observacao importante

- `supplierResources.maxCapacity` ainda nao foi conectado ao runtime logistico.
- Portanto, nesta versao, `0` ainda nao esta operando como "infinito" em logica de consumo/restricao.

