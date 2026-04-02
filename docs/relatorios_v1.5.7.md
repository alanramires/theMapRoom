# AI Player Automatic Planner

## Resumo
- Planejador do AI Player consolidado com fluxo de planos fixos + variaveis automaticos.
- Defesa e ataque continuam configuraveis via ScriptableObject.
- Planos variaveis passam a ser gerados em runtime por setor com base no snapshot do turno.

## Alteracoes principais
- `AIPlanEvaluator`: geracao dinamica de planos variaveis por setor nao controlado.
- `AIPlanDatabase`: removida dependencia de lista de planos variaveis estaticos; mantidos limites dinamicos por turno.
- `AIPlannerWindow`: interface ajustada para configurar apenas orcamento dinamico (`maxVariablePlans` e `maxUnitsPerVariablePlan`).
- `AIIntelDebugWindow`: exibicao de planos ativos adaptada para intents dinamicos (`Plan == null`) com nome legivel.
- `AIPlayerController`: logs e leitura de planos do turno ajustados para intents fixos e dinamicos.

## Comportamento esperado
- A cada turno, a IA avalia planos fixos (defesa/ataque).
- Em seguida, gera planos variaveis automaticamente para setores capturaveis nao controlados.
- Unidades de infantaria livres sao designadas por proximidade ao alvo de captura, respeitando os limites configurados no banco.

## Validacao
- Build `Assembly-CSharp` executado com sucesso (0 erros, 0 warnings).
