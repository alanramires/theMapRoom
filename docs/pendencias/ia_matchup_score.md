# Pendência — nota canônica de matchup da IA

## Problema

`Tools > Utils > Unit > Unit Analyser > Matriz arma x classe`, o Shopping e a decisão de ataque runtime compartilham o motor real de combate, mas não compartilham uma única nota final de matchup.

A ferramenta e o Shopping usam `UnitCounterEvaluator`, que converte a simulação em uma cobertura estável de `0..1`. A decisão runtime usa o mesmo `AICombatHpSimulator`, RPS, DPQ e preferências de arma, porém recompõe outra nota com pesos táticos e com a preferência doutrinária de classe declarada no `UnitData`.

Assim, uma unidade pode aparecer como excelente contra uma classe na matriz e escolher outro alvo no jogo por razões válidas, mas que a ferramenta não apresenta.

## Fontes atuais

- `Assets/Editor/Units/UnitAnalysisWindow.cs`: matriz arma × classe;
- `Assets/Scripts/Combat/UnitCounterEvaluator.cs`: cobertura usada pela análise e pelo Shopping;
- `Assets/Scripts/Match/AI/AIController.AttackDecision.cs`: simulação runtime;
- `Assets/Scripts/Match/AI/Units/Assault/AIController.Assault.Targeting.cs`: preferência doutrinária de alvos;
- controladores de Assault, FireSupport, AirCombat e Capturer: composição local do ranking.

## Direção do refactor

Criar uma avaliação canônica de matchup reutilizável pelos três consumidores:

1. A nota-base vem da simulação real e do `UnitCounterEvaluator`.
2. Atacante, defensor, HP, distância, arma e bancos RPS/DPQ iguais devem produzir a mesma nota-base em ferramenta, Shopping e runtime.
3. Preferência doutrinária, posição, objetivo, risco, DPQ contextual e urgência continuam como modificadores próprios do runtime.
4. Esses modificadores devem aparecer separadamente no log e no `DecisionPreview`.
5. A cobertura agregada da matriz não deve substituir sozinha a decisão tática, pois mede composição do roster, não a situação concreta do tabuleiro.

## Critério de validação

Para o mesmo matchup, os três consumidores exibem a mesma nota-base e explicam numericamente cada modificador adicional que altera o ranking final.

## Status

Aberta.
