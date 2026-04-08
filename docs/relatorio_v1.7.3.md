# Shopping v2

## Resumo

Esta versao consolida a segunda grande etapa do sistema de compras da IA. O shopping deixou de ser apenas uma soma de fallbacks por construcao e passou a operar como uma camada central de decisao, conectada ao planner e ao estado real da partida.

- compras passam a respeitar demanda por capability e por plano
- o sistema ganhou `strategic save` para unidades premium
- a fila de compras agora pode promover compras caras assim que a meta de caixa e atingida
- a `mass floor` deixou de depender de turnos fixos e passou a usar o estado do mapa
- sobras grandes de caixa agora podem virar compra de pressao ofensiva mesmo sem demanda explicita do planner
- o debug de shopping ficou visivel no inspector, incluindo budget, strategic save, mass floor e decisoes por construcao

## Principais mudancas

- `AIShoppingManager`
  - consolidacao da demanda do planner por `Capture`, `Escort`, `FireSupport` e `Logistics`
  - fila central de pedidos por plano/capability
  - budget separado em `reservedMoney`, `freeMoney`, `saveTargetMoney` e `strategicReserveMoney`
  - `capability ladder` por modo (`Attack` / `Defense`) com resolucao `ideal`, `buy-now` e `fallback`
  - `strategic save` com score de pressao por capability
  - `strategic-buy` quando a meta de caixa ja foi atingida
  - `overflow-pressure-buy` para gastar caixa excedente acima do teto caro do catalogo do modo
  - `mass floor` baseada em estado da partida, nao mais em cortes fixos por turno
- `AIPlayerController`
  - debug serializavel do shopping runtime por time
  - exposicao de orders, decisions, strategic save e mass floor no inspector
  - supridores ociosos agora tentam sair de cima de construcoes e procuram staging mais seguro quando o alvo de supply esta em zona perigosa
- documentacao/debug
  - `massFloorCurrentUnits`, `massFloorRequiredUnits` e `massFloorReason`
  - logs de compra com `branch=strategic-buy`, `overflow-pressure-buy`, `capability-buy`, `save-fallback`

## Efeito esperado

- a IA passa a guardar para tanques e artilharias caras quando isso faz sentido para a composicao atual
- ao atingir a meta de caixa, a compra premium deixa de se perder no fluxo normal e vira compra prioritaria
- a interrupcao do save premium agora acontece por falta real de massa/composicao, nao por um numero arbitrario de turno
- quando a IA estiver com caixa excedente e sem bloqueio critico, ela passa a converter essa gordura em pressao de mapa
