# Hex disputado revisao 1

Versao: v1.4.3  
Status: em validacao no Unity

## Resumo
- Inicio da migracao para regras de ocupacao por camada, com foco em pathfinding e confirmacao de movimento.
- Resolver central introduzido sem alterar sensores/spawner/construcoes nesta etapa.
- Fluxo ajustado para reduzir mismatch entre hex pintado e confirm de movimento.

## Entregas desta revisao
- Criado `OccupancyResolver` com:
  - `HeightBand` (`Air`, `Sub`, `Blocking`)
  - `LayerOccupancyKey`
  - `CanPassThrough`, `CanEndMove`, `CanEnter`
- Regras do resolver subordinadas ao `Total War`:
  - `TotalWar=true` ativa regras layer-aware
  - `TotalWar=false` usa fallback legado
- `CanPassThrough` atualizado:
  - inimigo na mesma camada bloqueante sempre bloqueia passagem
- BFS de movimento (`UnitMovementPathRules`) migrado para `OccupancyResolver.CanPassThrough`
  - avaliando ocupantes por hex
- Pintura de alcance (`TurnStateManager.Range`) filtra destino final por `OccupancyResolver.CanEndMove`
- Confirm de movimento (`TurnStateManager.StateMachine`) alinhado ao resolver quando ativo
  - fallback antigo preservado quando regras layer-aware desativadas
- Logs temporarios de diagnostico no BFS para debug de banda e bloqueio por ocupante

## Ajustes de suporte
- Flag de debug de pathfinding exposta/ativada no `PathManager` para facilitar QA
- Documentacao tecnica atualizada em `docs/hexDisputado.md` com status real de implementacao

## Pendencias de QA
- Validar matriz minima:
  - caca (Air) sobre unidade bloqueante inimiga (Blocking)
  - unidade terrestre bloqueante desviando de inimigo bloqueante
  - comportamento com `TotalWar=true` e `TotalWar=false`
- Confirmar consistencia entre:
  - BFS (visitacao)
  - range pintado
  - confirm de movimento
- Remover logs temporarios de BFS apos fechar diagnostico
