# RELATORIO_V1.2.9

Versao: `v1.2.9`  
Commit: `46a378f`  
Titulo: **Separacao de Visao de Posicao x visao de Unidade**

## Principais mudancas

- Separacao formal de dois pipelines de visibilidade:
  - **Visao de posicao (hex/FoW de terreno)**: decide quais hexes ficam revelados.
  - **Visao de unidade (detectar/alguem me ve)**: decide se a unidade inimiga aparece.

- FoW alinhado com LoS/EV de terreno (sem bypass):
  - ajuste para usar contexto de visao por hex com regras de elevacao/bloqueio;
  - correcoes para impedir vazamento de visao atras de floresta/montanha;
  - spotter removido do pipeline de revelacao de hex no FoW.

- Regra de "duas verdades de visao" na revelacao de hex:
  - quando ha alcances diferentes por dominio/altura, a visao validada nao e eclipsada pela menor;
  - ex.: unidade com visao maior em camada especifica continua revelando hexes nessa faixa quando a regra da camada permitir.

- `PodeDetectarSensor` reforcado para contexto virtual por hex:
  - resolucao de camada do hex na ordem:
    1. ocupante (quando aplicavel),
    2. construcao,
    3. estrutura,
    4. terreno;
  - aplicado no calculo de visibilidade por celula e LoS para hex virtual.

- `Pode Mirar`/debug:
  - ajuste para evitar falso negativo no tool por gate de visibilidade de TotalWar fora de runtime;
  - alinhamento de tilemap no contexto de simulacao.

- Novo tool:
  - `Tools > Sensors > Pode Enxergar` (alias: `Tools > Sensor > Pode Enxergar`);
  - lista hexes visiveis e invalidos com camada virtual;
  - botoes de desenho em lote:
    - `Desenhar todas as validas` (toggle),
    - `Desenhar todas as invalidas` (toggle).

- Padronizacao de referencias de board:
  - reforco de preferencia por `TileMap` em spawner/managers/tools para reduzir divergencia de sensor por mapa errado.

## Arquivos com destaque

- Codigo:
  - `Assets/Scripts/Match/MatchController.cs`
  - `Assets/Scripts/Sensors/PodeDetectarSensor.cs`
  - `Assets/Scripts/Sensors/PodeMirarSensor.cs`
  - `Assets/Scripts/Match/TurnStateManager.cs`
  - `Assets/Scripts/Units/UnitSpawner.cs`
  - `Assets/Scripts/Units/UnitManager.cs`
  - `Assets/Scripts/Cursor/CursorController.cs`
  - `Assets/Scripts/Construction/ConstructionManager.cs`

- Ferramentas:
  - `Assets/Editor/PodeEnxergarSensorDebugWindow.cs`
  - `Assets/Editor/PodeDetectarSensorDebugWindow.cs`
  - `Assets/Editor/PodeMirarSensorDebugWindow.cs`
  - `Assets/Editor/UnitPainterWindow.cs`

- Documentacao:
  - `docs/FOW.md`
  - `docs/testes/checklist_sensores_fow_maluco.md`

## Publicacao

- Branch `main` publicada em `origin`.
- Tag `v1.2.9` criada e publicada em `origin`.
