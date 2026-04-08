# Shopping AI Refactor

## Resumo

Esta versão consolida a refatoração do sistema de compras da IA para um modelo orientado por capability, alinhado ao planner e aos `AIUnitProfile`.

## Principais mudanças

- `Shopping v2` agora prioriza demanda por capability:
  - `Capture`
  - `Escort`
  - `FireSupport`
  - `Logistics`
- Remoção do fluxo principal hardcoded que forçava compra de capturador quando um plano ficava sem `Capture`.
- Introdução de `AIShoppingDemandSummary` para resumir déficit de papéis a partir dos planos já gerados.
- Compra por capability agora usa `planCapabilities` do `AIUnitProfile` como filtro formal.
- `AIData` permanece como preferência de compra dentro da capability, não mais como override cego da demanda do planner.
- Regra de `massa minima` no early game para impedir que a IA economize cedo demais e colapse em número de unidades.
- Regra simples de `save` por até 2 turnos para comprar uma unidade melhor da mesma capability.
- Preservação dos caminhos de fallback de compra quando apropriado.

## Debug e inspeção

- Logs de compra agora distinguem melhor os branches do shopping:
  - `capability-buy`
  - `capability-save`
  - `composition-target`
  - `fallback-save`
- O inspector do `AI Manager` e a janela `AI Planner` agora exibem contadores de alocação por plano:
  - `CAP`
  - `ESC`
  - `ART`
  - `SUP`

## Ajustes auxiliares

- `AI Burra` foi ajustada para testar melhor o fluxo novo de `Capture` em ataque e defesa.
- A documentação de `AI Unit Profile` foi atualizada para refletir o `Shopping v2`.
