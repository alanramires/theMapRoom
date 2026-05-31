# AI Ajustes nos Reparos

## Resumo
- ajuste no fluxo de reparo para tratar fusao durante manutencao como atalho de reparo
- bloqueio de comportamento ofensivo normal quando a unidade entra em fusao no contexto de manutencao
- nova flag no `AIUnitProfile`: `canShootFromDistanceWhileRepairing`
- tiro durante manutencao deixa de depender implicitamente de `holdPositionWhenInRange`

## Objetivo
Separar melhor tres conceitos que estavam misturados:
- retorno para reparo
- fusao como reparo rapido
- permissao para atirar enquanto a unidade ainda esta em manutencao

## Efeito esperado
- unidade em manutencao pode fundir sem virar combatente normal no mesmo turno
- perfis passam a controlar explicitamente se podem atirar parados durante manutencao
- comportamento de reparo fica mais previsivel e menos acoplado ao estilo de combate da unidade
