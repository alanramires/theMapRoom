# Relatorio Tecnico - v1.0.9

## Versoes cobertas

- `v1.0.9` - commit `6a4cd92` - `refatoração do sistema de pouso e decolagem`
- Branch: `main`

Este relatorio resume as principais mudancas entregues na versao `v1.0.9`.

---

## 1) Refatoracao de regras de decolagem por skill-slot (Construction)

### Objetivo

Remover regras hardcoded por tipo de skill e permitir configuracao de modo de decolagem por item da lista de skills aceitas na construcao.

### O que entrou

1. Novo modelo de regra por skill:
- `ConstructionLandingSkillRule` com:
  - `skill`
  - `takeoffMode` (`TakeoffProcedure`)

2. Novo campo principal em `ConstructionData`:
- `requiredLandingSkillRules` (lista de `skill + takeoffMode`).

3. Compatibilidade de dados:
- campo legado `legacyRequiredLandingSkills` mantido para migracao;
- migracao automatica no `OnValidate` quando a lista nova estiver vazia;
- saneamento de valores de enum legado para `InstantToPreferredHeight`.

4. Resolver de Air Ops atualizado:
- validacao de skill em construcao passou a extrair skills da lista nova;
- plano de decolagem passa a usar o `takeoffMode` configurado no slot da skill.

### Arquivos-chave

- `Assets/Scripts/Construction/ConstructionData.cs`
- `Assets/Scripts/Units/Rules/AirOperationResolver.cs`
- `Assets/Editor/ConstructionDataEditor.cs`

---

## 2) Refatoracao de regras de decolagem por skill-slot (Structure + Terrain Pair)

### Objetivo

Aplicar o mesmo padrao configuravel por slot na secao `Aircraft Ops By Terrain`.

### O que entrou

1. Novo modelo de regra por skill em estrutura:
- `StructureLandingSkillRule` com:
  - `skill`
  - `takeoffMode`

2. Novo campo por par Estrutura+Terreno:
- `requiredLandingSkillRules` (lista de `skill + takeoffMode`).

3. Compatibilidade de dados:
- campo legado `legacyRequiredLandingSkills` mantido e oculto no inspector;
- migracao automatica no `OnValidate`.

4. Padronizacao de nomenclatura:
- `isRoadRunway` renomeado para `allowTakeoffAndLanding`;
- migracao preservada com `FormerlySerializedAs("isRoadRunway")`.

5. Resolver atualizado:
- validacao de pouso/decolagem em contexto `Structure` usa lista nova;
- plano de decolagem em `Structure` usa `takeoffMode` do slot configurado.

### Arquivos-chave

- `Assets/Scripts/Structures/StructureData.cs`
- `Assets/Scripts/Units/Rules/AirOperationResolver.cs`
- `Assets/Editor/StructureDataEditor.cs`

---

## 3) Simplificacao de procedimentos de decolagem

### Objetivo

Eliminar acoplamento com nomenclatura antiga de skill e reduzir duplicidade de procedimento equivalente.

### O que entrou

- removido `VTOLPopUpToPreferredHeight` do enum `TakeoffProcedure`;
- VTOL passa a usar `InstantToPreferredHeight`;
- sensor de decolagem considera `full move` apenas por `InstantToPreferredHeight`.

### Arquivos-chave

- `Assets/Scripts/Units/AirOperationTypes.cs`
- `Assets/Scripts/Units/Rules/AirOperationResolver.cs`
- `Assets/Scripts/Sensors/PodeDecolarSensor.cs`

---

## 4) PodeDecolar com retorno operacional por codigos

### Objetivo

Padronizar retorno do sensor para dirigir fluxo de selecao/movimento no `TurnStateManager`.

### O que entrou

- `PodeDecolarReport.takeoffMoveOptions` (`List<int>`) com codigos:
  - `-1`: ignorar sensor (nao-aeronave ou aeronave ja em voo);
  - `0`: decolagem mantendo 0 hex;
  - `1`: decolagem com 1 hex;
  - `0,1`: decolagem com 0 ou 1 hex;
  - `9`: decolagem para altitude nativa com liberdade de movimento.

### Arquivo-chave

- `Assets/Scripts/Sensors/PodeDecolarSensor.cs`

---

## 5) TurnStateManager: snapshot de decolagem temporaria + ESC em duas etapas

### Objetivo

Permitir "decolagem temporaria" ao selecionar unidade e restaurar estado corretamente com `ESC`.

### O que entrou

1. Snapshot temporario ao sair de `Neutral`:
- guarda dominio/altura/flags originais da aeronave;
- guarda tambem as opcoes retornadas pelo `PodeDecolar`.

2. Comportamento de selecao:
- se retorno for `0`, `1` ou `0,1`, unidade sobe para `AirLow` ao entrar em `UnitSelected`;
- movimento permitido respeita exatamente esse conjunto de opcoes.

3. ESC em duas etapas:
- `ESC` de `MoveuParado/MoveuAndando` volta para `UnitSelected` mantendo estado aereo temporario;
- segundo `ESC` (saindo de `UnitSelected` para `Neutral`) restaura estado pousado original do snapshot.

4. Guarda para auto takeoff forcado:
- rotina de auto takeoff para movimento e ignorada quando o fluxo temporario (`0/1/0,1`) ja esta ativo.

### Arquivos-chave

- `Assets/Scripts/Match/TurnStateManager.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.StateMachine.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Movement.cs`

---

## 6) Range map filtrado por opcoes do PodeDecolar

### Objetivo

Restringir area pintada de movimento conforme retorno operacional do sensor.

### O que entrou

Filtro no paint do range:
- `-1` ou `9`: ignora filtro (range normal);
- `0`: pinta apenas hex atual;
- `1`: pinta apenas vizinhos imediatos;
- `0,1`: pinta hex atual + vizinhos.

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.Range.cs`
- `Assets/Scripts/Match/TurnStateManager.cs`

---

## 7) Promocao automatica para altitude nativa em unidade ja no ar

### Objetivo

Ao selecionar aeronave ja em voo (retorno `-1` no `PodeDecolar`), promover para altitude nativa de operacao e manter rollback em `ESC`.

### O que entrou

1. Promocao automatica na selecao:
- exemplo: unidade em `AirLow` sobe para `AirHigh` se essa for a altitude preferida.

2. Snapshot de entrada para callbacks/rollback:
- dominio/altura de entrada da promocao sao guardados.

3. ESC de saida da selecao:
- ao voltar para `Neutral`, restaura o estado de entrada (ex.: volta para `AirLow`).

### Arquivos-chave

- `Assets/Scripts/Match/TurnStateManager.cs`

---

## Observacoes

- A versao `v1.0.9` consolida o sistema de pouso/decolagem como configuracao por contexto e por slot de skill.
- Construction e Structure agora seguem o mesmo padrao de configuracao (`skill + takeoffMode`), com migracao de dados legados preservada.
