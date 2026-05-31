# v2.0.8 - AI Debug

## AI Debug

- Adicionado fluxo de `AI Step` para preparar o proximo batch, mostrar preview no mapa e executar no segundo acionamento.
- Adicionados comandos `AI Shopping Pause` e `AI Shopping Resume` para travar/liberar a entrada da AI na fase de compras.
- Adicionado comando `AI Stage <1-3>` para reiniciar a AI a partir de uma fase especifica de debug.
- O painel de dialogo agora mostra estados de debug da AI como pause, resume, step, shopping pause/resume e stage.
- Removido o atalho F11 de fullscreen para liberar F11 para o debug step.

## Estado e Persistencia

- `AI spawn`, `wake unit` e `wake all units` agora mantem `UnitManager.AllActive` sincronizado para a AI enxergar unidades novas ou reativadas.
- O save/load agora persiste o plano real da AI (`ObjectiveManager`), incluindo objetivos, slots, rogues e handoff.
- O save/load agora guarda o stage runtime da AI para preservar o contexto de debug.
- `UnitManager` ganhou uma secao explicita de runtime do plano da AI para inspecao do badge/atribuição no Inspector.

## Decisao da AI

- Desempates de movimento do capturador agora favorecem rotas melhores em direcao ao HQ inimigo.
- O desempate usa custo real de movimento via `SectorManager`, com `UnitData` da unidade, em vez de distancia simples em linha reta.
- Logs de score do capturador agora mostram distancia/custo ate o HQ e o valor usado no desempate.
