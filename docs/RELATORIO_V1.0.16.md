# Relatorio Tecnico - v1.0.16

## Versoes cobertas

- `v1.0.15` - commit `04d813f` - `v1.0.15 - Logistica: Suprir Unidades`
- `v1.0.16` - commit `fd9990e` - `v1.0.16 - logistica de porta avioes`
- Branch: `main`

Este relatorio consolida os ajustes de logistica embarcada (porta-avioes), junto com a nova regra de trilhos por skill e melhorias de tooling/dados de estruturas e terrenos.

---

## 1) Logistica embarcada (porta-avioes)

### Objetivo

Habilitar `Pode Suprir` para suppliers com `serviceRange=EmbarkedOnly`, com fluxo completo de selecao e execucao sobre passageiros embarcados.

### O que entrou

1. Sensor `PodeSuprir` com range embarcado:
- coleta candidatos a partir de `TransportedUnitSlots` do supplier
- permite alvo embarcado no proprio supplier (inclusive quando inativo no hierarchy)
2. Bypass de validacao de dominio para passageiros embarcados do proprio supplier:
- para `EmbarkedOnly`, nao bloqueia por `supplierOperationDomains` no check inicial
- para candidato embarcado, usa dominio/altura do supplier como plano efetivo
3. Mensagem de indisponibilidade dedicada:
- `Sem unidades embarcadas validas para suprir.`

### Arquivo-chave

- `Assets/Scripts/Sensors/PodeSuprirSensor.cs`

---

## 2) Runtime de fila de suprimento para embarcados

### Objetivo

Fazer a fila `Suprindo` aceitar e executar atendimento de unidades embarcadas no proprio supplier, sem quebrar regras de camada para alvos externos.

### O que entrou

1. Lista de candidatos aceita embarcados do supplier selecionado.
2. Em embarcados:
- cursor/alvo usa cell do supplier
- ignora transicoes `forceLand/forceTakeoff/forceSurface`
- ignora validacao de camada de atendimento da fila
3. Visual de selecao/execucao embarcada:
- mostra 1 embarcado por vez no preview de selecao (conforme cursor)
- durante execucao sequencial, garante 1 por vez (sem empilhamento visual)
- supplier oculta HUD durante o fluxo embarcado; HUD volta ao final
4. Ao atender cada embarcado:
- unidade aparece em `Land/Surface` no cell do supplier
- recebe sorting temporario acima do supplier
- HUD da unidade e atualizado/apresentado
- apos atendimento, unidade volta a estado oculto embarcado

### Arquivo-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.SupplyQueue.cs`

---

## 3) Regras de movimento por trilho (skill)

### Objetivo

Introduzir regra explicita para unidades com skill de trem, independente da hierarquia padrao de terreno/estrutura.

### O que entrou

1. Nova regra em pathing:
- se unidade tem skill `Linha de Trem`, so pode andar em hex com estrutura `isRail=true` e `structureBlocksRail=false`
2. Regra aplicada no validador de passagem de hex em movimento.

### Arquivo-chave

- `Assets/Scripts/Units/Rules/UnitMovementPathRules.cs`

---

## 4) Skill Rules em Terrain/Structure/Construction

### Objetivo

Padronizar bloqueios por skill no modelo de dados de mapa.

### O que entrou

1. `blockedSkills` em:
- `TerrainTypeData`
- `StructureData`
- `ConstructionData`
2. Organizacao da secao `Skill Rules` em structure/construction (junto de `requiredSkillsToEnter` e `skillCostOverrides`).
3. Inicializacao/garantia de listas em `OnValidate`.
4. Flags de trilho em `StructureData`:
- `isRail`
- `structureBlocksRail`

### Arquivos-chave

- `Assets/Scripts/Terrain/TerrainTypeData.cs`
- `Assets/Scripts/Structures/StructureData.cs`
- `Assets/Scripts/Construction/ConstructionData.cs`

---

## 5) Tooling: Road Route Painter

### Objetivo

Melhorar UX do painter para selecionar estrutura diretamente do catalogo do banco.

### O que entrou

1. Substituicao do `ObjectField` manual por seletor (`Popup`) com itens da `StructureDatabase` do `RoadNetworkManager`.
2. Sincronizacao de indice selecionado com estrutura atual.
3. Rotulo de opcoes no formato `id (displayName)`.
4. Mensagens de validacao quando nao ha database/itens.

### Arquivo-chave

- `Assets/Editor/RoadRoutePainterWindow.cs`

---

## 6) Dados e assets

### O que entrou

1. Nova skill:
- `Linha de Trem`
2. Novas estruturas:
- `Trilhos`
- `Ponte com Trilhos`
3. Ajustes de catalogos e estruturas/terrenos para regras de skill e trilho.
4. Novos tiles:
- `traintracks.png`
- `traintracks bridge.png`

### Arquivos de dados relevantes

- `Assets/DB/Character/Skills/Linha de Trem.asset`
- `Assets/DB/World Building/Structures/Trilhos.asset`
- `Assets/DB/World Building/Structures/Ponte com Trilhos.asset`
- `Assets/DB/World Building/Structures/Catalogo de Estruturas.asset`
- `Assets/DB/Character/Skills/Catalogo de Skills.asset`

---

## 7) Resumo de volume (base `04d813f` -> `fd9990e`)

- `29 files changed`
- `12865 insertions`
- `11748 deletions`

