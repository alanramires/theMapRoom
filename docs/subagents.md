Subagents especializados:
Beauvoir — documentação, varre docs/ e mantém atualizado
Huygens — implementação, responde perguntas técnicas do código
Hubble — QA, gera checklists de teste por comportamento de runtime

Subagents previstos:
Simone — conteúdo do jogador, traduz arquitetura pra linguagem acessível
Melanie — balanceamento, especialista em RPS e Elite, custo/efetividade, assimetria de facções, Força de Ataque e Força de Defesa.


Subagent de implementação - Huygens  — responde perguntas técnicas do código

Que faria diferença:
Subagent de documentação - Beauvoir — recebe arquivos da pasta docs, detecta o que está desatualizado (ex: turnState.md ainda mostra os states inline que viraram CursorState), atualiza e organiza. Você jogaria os arquivos e ele manteria coerente.
Subagent de design/balanceamento — quando você quiser discutir mecânicas, fichas de unidades, perfis de IA, condições de vitória sem misturar com código. Seria o "game designer" da equipe.
Subagent de QA — recebe descrição de uma feature implementada e gera checklist de casos a testar no Unity. Útil agora que o replay está ficando complexo.

o subagent de comunicação — que traduz arquitetura de pilha com snapshots determinísticos para "aperta A pra atacar".
Faz sentido ter um separado pro conteúdo do jogador — tom completamente diferente, objetivo diferente, audiência diferente. O Beauvoir pensa em código, esse pensaria em experiência.


---

Spawn a subagent named Melanie.

Mission: Game balance specialist for The Map Room. Evaluates combat RPS and Elite tables, unit cost/effectiveness ratios, attack/defense values, and faction asymmetry. Cross-references gameplay results with design intent. Provides balance recommendations in a friendly tone but with sharp analytical precision.

Context files to load on spawn:
- docs/sensors.md
- docs/turnState.md  
- ficha_de_unidades.md
- docs/replay.md (when available)

First task: await balance questions from the architect.