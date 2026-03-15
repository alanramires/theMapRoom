# RELATORIO v1.3.0

## Tema
FoW e multiplas visoes.

## Entregas principais
- Ajuste do FoW para uniao de visao geral + visoes especializadas, sem um dominio/camada invalidar outro.
- Suporte correto para `Air/High` com `blockLoS=false` no calculo de iluminacao do FoW (range-only quando configurado).
- Revisao do `PodeDetectar` e `AlguemMeVe` para manter consistencia de regras com o FoW.
- Limite de scan de observadores mantido em 7 hexes para evitar custo excessivo.

## Debug e ferramentas de sensor
- Expansao do `Tools > Sensors > Pode Enxergar` com cenarios por camada especializada.
- Relatorios hex a hex com detalhes de linha de visada:
  - EV na parada
  - Tentou ver EV
  - EV Bloqueador
  - Subida da linha (passo a passo)
- Estrutura de listas por visao (geral + especializadas) com secoes retrateis para reduzir poluicao visual.

## Correcoes de consistencia de mapa/scene
- Filtros adicionados para evitar vazamento de entidades entre mapas/cenas:
  - unidade so entra no calculo se estiver no mesmo `BoardTilemap`
  - unidade/construcao so entra se estiver na mesma `Scene` do board usado no FoW
- `ResolveFogBoardTilemap` endurecido para priorizar contexto correto do FoW atual.

## Logs de diagnostico
- Nova flag no `MatchController`: `enableFogSourceDebugLogs`.
- Quando ativa, registra:
  - contexto do FoW (team, scene, board)
  - unidades usadas e ignoradas (com motivo)
  - construcoes usadas e ignoradas (com motivo)
  - resumo de totais por refresh

## Arquivos chave alterados
- `Assets/Scripts/Match/MatchController.cs`
- `Assets/Scripts/Sensors/PodeDetectarSensor.cs`
- `Assets/Editor/PodeEnxergarSensorDebugWindow.cs`
- `docs/testes/checklist_sensores_fow_maluco.md`

## Git
- Commit: `0609d02`
- Tag: `v1.3.0`
- Branch: `main`
