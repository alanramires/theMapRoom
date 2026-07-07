# v2.3.5 - AI Construction fix

## Objetivo

Corrigir inconsistencias de construcoes que afetavam leitura da AI, save/load e ferramentas legadas de spawn, deixando o fluxo de construcao mais previsivel para partidas novas e carregadas.

## Principais mudancas

- Save/load agora persiste o `slotIndex` das construcoes, preservando HQs, rally targets, anchors e inferencias ligadas ao slot do time.
- New game reajusta o proximo ID do `UnitSpawner` com base nas unidades realmente ativas em cena, reduzindo divergencia entre jogo novo e load game.
- `ConstructionSpawner` teve os fluxos legados de manual spawn, map spawn e field spawn removidos, ja que o fluxo atual usa `Tools > Construction > Construction Painter`.
- `UnitSpawner` teve os fluxos legados de manual spawn e map spawn removidos, consolidando o uso do `Tools > Unit > Unit Painter`.
- O editor customizado de `ConstructionSpawner` e `UnitSpawner` foi simplificado para esconder controles antigos que nao fazem mais parte do fluxo atual.

## AI e Diagnostico

- `AIShoppingPlanner` agora loga construcoes proprias ignoradas como produtor quando nao possuem ofertas de unidade terrestre/aerea.
- `ConstructionManager` expoe a regra de venda runtime para diagnostico da AI.
- O novo log ajuda a diferenciar construcao capturada sem ofertas (`offers=0`) de construcao ausente no snapshot de compras.

## Resultado esperado

- Save/load deve preservar melhor o comportamento estrategico ligado a construcoes e slots.
- Partidas novas devem ficar mais proximas do comportamento observado apos load.
- Ferramentas antigas de spawn deixam de poluir Inspector e reduzem caminhos paralelos para criar unidades/construcoes.
- Se uma construcao capturada nao for usada como produtora, o log de shopping deve apontar se ela esta sem ofertas ou fora da leitura da AI.

## Validacao

- Build local: `dotnet build Assembly-CSharp.csproj --no-restore`.
- Testes manuais em Battle Map focando save/load, captura de construcoes, compra da AI e limpeza dos inspectors de spawner.
