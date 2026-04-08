# Shopping

## Visao geral

O `Shopping v2` e a camada central de compras da IA. Ele fica entre o planner e a execucao do shopping nas construcoes.

Responsabilidade do sistema:
- receber a demanda do turno produzida pelo planner
- consolidar pedidos por plano e por capability
- calcular budget comprometido vs budget livre
- decidir entre `buy-now`, `fallback`, `strategic save` e compra de pressao por overflow
- distribuir decisoes por construcao antes da execucao da Fase 3

Responsabilidade que continua fora dele:
- mover cursor
- abrir tela de shopping
- navegar catalogo
- confirmar compra no runtime

## Origem da demanda

O planner exporta, por plano:
- `DesiredCaptureCount`
- `DesiredEscortCount`
- `DesiredArtilleryCount`
- `DesiredSupportCount`

O shopping traduz isso para:
- `Capture`
- `Escort`
- `FireSupport`
- `Logistics`

Cada plano vira demanda faltante (`missingCount`) e depois uma lista central de `orders`.

## Ordem de decisao

Fluxo por turno:
1. ler snapshot fresco do time
2. construir a demanda por plano e por capability
3. ordenar `orders` por prioridade
4. avaliar `strategic save`
5. avaliar compra premium imediata (`strategic-buy`)
6. avaliar compra por overflow de caixa (`overflow-pressure-buy`)
7. resolver orders normais por capability
8. usar fallback apenas quando necessario

## Capability ladder

O shopping usa a ordem do catalogo do `AIDataMode` atual como ladder real de compra.

Em cada capability, a decisao e:
- `ideal`: melhor unidade do ladder do modo atual
- `buy-now`: melhor unidade do ladder que ja cabe no caixa
- `fallback`: alternativa de fallback do modo

Importante:
- o ladder respeita o modo atual
- unidades presentes so em `Defense` nao entram no `Attack`
- o planner pede capability; o `AIData` define qual unidade da capability e preferida

## Strategic save

O `strategic save` existe para unidades premium, principalmente:
- `Escort`
- `FireSupport`

Ele usa score por capability considerando:
- quantidade de planos pressionando a capability
- quantidade faltante dessa capability
- risco tatico acumulado
- valor do upgrade sobre a opcao barata compravel agora
- penalidade por espera

Quando a unidade premium ja esta compravel:
- o estado vira `strategic-ready`
- e a primeira construcao compativel tenta `strategic-buy`

O `HQ` e priorizado na lista de construcoes para esse tipo de compra.

## Overflow pressure buy

Se nao houver bloqueio critico e o caixa ja cobrir o topo caro do catalogo ofensivo do modo atual, a IA pode gastar mesmo sem demanda explicita do planner.

Objetivo:
- transformar gordura de caixa em pressao de mapa

Prioridade atual:
- `Escort`
- `FireSupport`
- `Assault`

## Mass floor

A `mass floor` protege a IA contra save ganancioso quando o exercito esta fino demais.

Ela agora e baseada em estado da partida:
- massa estrutural minima a partir de construcoes produtoras e frentes ativas
- pressao combinada de `Capture` + `Escort`
- pequena cauda de suporte para `FireSupport` e `Logistics`
- limite de faltas taticas totais

Campos de debug:
- `massFloorBlocked`
- `massFloorCurrentUnits`
- `massFloorRequiredUnits`
- `massFloorReason`

Exemplos de motivo:
- `state-based: critical capture gap ativo`
- `state-based: unidades abaixo do minimo estrutural`
- `state-based: unidades abaixo do minimo dinamico`
- `state-based: faltas taticas acima do limite`

## Debug no inspector

No `AIPlayerController`, em `Shopping Runtime (Debug)`, ficam visiveis:
- `totalMoney`
- `reservedMoney`
- `freeMoney`
- `saveTargetMoney`
- `strategicReserveMoney`
- `massFloorBlocked`
- `massFloorCurrentUnits`
- `massFloorRequiredUnits`
- `massFloorReason`
- `hasCriticalCaptureGap`
- `strategicSaveActive`
- `strategicSaveUnitId`
- `strategicSaveSourceLabel`
- `strategicSaveCost`
- `strategicSaveTurnsToAfford`
- `strategicSaveDeferredOrders`
- `strategicSaveReason`
- `Orders`
- `Decisions`

## Leitura rapida dos branches de log

- `capability-buy`
  - compra normal resolvida pelo ladder da capability
- `strategic-save`
  - a IA decidiu guardar para unidade premium
- `strategic-ready`
  - a meta de caixa ja foi atingida
- `strategic-buy`
  - a unidade premium foi comprada
- `overflow-pressure-buy`
  - caixa excedente virou compra de pressao ofensiva
- `save-fallback`
  - a IA estava em save, mas usou fallback permitido

## Relacao com o AI Unit Profile

O `AI Unit Profile` continua definindo:
- `planCapabilities`
- comportamento por stance
- thresholds de reparo e supply

Para a referencia detalhada do sistema de compras, veja `docs/shopping.md`.
