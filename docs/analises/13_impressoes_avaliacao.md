# 13 - Impressoes e Avaliacao Geral

Data base: 2026-05-25
Baseado em: revisao completa dos docs 01-12 contra o codigo-fonte atual.

---

## Contexto da avaliacao

310 scripts C#. 145 commits. 45 arquivos de IA, 30 de sensores, 23 de TurnStateManager. Projeto que saiu de prototipo e chegou a produto jogavel com IA funcional cobrindo todas as categorias de unidade, incluindo operacoes aereas completas.

---

## Pontos fortes concretos

### 1. Arquitetura de sensores e a decisao mais acertada do projeto
A escolha de encapsular toda validacao de acao em sensores (`PodeMirarSensor`, `PodeEmbarcarSensor`, `PodeCapturarSensor`, etc.) e pagar esse custo cedo foi a decisao arquitetural mais importante. A IA nunca precisa reimplementar "posso atacar isso?" — ela pergunta ao sensor. O `TurnStateManager` nunca precisa calcular alcance — ele pergunta ao sensor. Isso eliminou uma classe inteira de bugs de divergencia que matam projetos desse tipo.

### 2. O padrao `*Executing` resolve um problema dificil de forma elegante
Separar "decisao do jogador" de "execucao do engine" com estados dedicados (`AttackingExecuting`, `EmbarcandoExecuting`, etc.) e bloquear input durante corotinas assincronas e uma solucao limpa para um problema que normalmente vira spaghetti em jogos por turno. Nenhuma entrada e aceita durante execucao — sem race conditions, sem estados inconsistentes.

### 3. Combate deterministico e rastreaavel
Sem RNG. Toda decisao de dano e reconstituivel a partir dos dados: arma + RPS + elite + HP + DPQ + terreno. Isso tem valor duplo: para o jogador (aprende as regras porque elas sao estaveis) e para o desenvolvedor (balancear e diagnosticar e possivel sem precisar de logs extensos).

### 4. Logistica como sistema real, nao como decoracao
A finitude de supply (`quantity` nos assets), a formula unificada de custo (`ServiceCostFormula`), o sistema de autonomia por tipo de motor — esses tres elementos juntos criam um trade-off genuino entre sustentar o que esta em campo e expandir. Nao e so um numero de recursos: e uma cadeia de dependencias que o jogador precisa gerenciar. Isso e raro em jogos do genero.

### 5. FoW completo e nao-trivial
`PodeDetectarSensor` com 2594 linhas nao e excesso de engenharia — e um sistema que modela visao de forma composicional: unidade + especializacao por dominio/altura + LoS por terreno + forward observer + deteccao stealth. O BFS aquatico para submarinos, a deteccao radar por alcance puro para AirHigh, os tres caches por revisao de tabuleiro — tudo isso esta em producao. Poucos jogos indie chegam a esse nivel de fidelidade no FoW.

### 6. A cadeia de comando da IA e sofisticada
A separacao `AIOperationManager` (o que e necessario taticamentente) / `ObjectiveManager` (o que e necessario territorialmente) / `AIShoppingPlanner` (o que comprar para suprir as lacunas) / `AIController` (execucao por unidade) e uma arquitetura que escala. Nao e um arvore de decisao plana — e uma estrutura de forca-tarefa onde o nivel de abstacao adequado toma cada decisao. Os 45 arquivos partial de `AIController` sao gerenciaveis justamente porque cada responsabilidade esta isolada.

### 7. Economia orientada a territorio
Renda derivada de construcoes capturadas, nao de bonus globais. `ConstructionUnitMarketRule` desacoplando "capturar para renda" de "capturar para produzir". `economyEnabled` como chave de cenario. Isso significa que cada captura tem impacto economico direto e mensuravel — o pacing do jogo e determinado pela disputa territorial, nao por timers ou thresholds artificiais.

### 8. Terreno como sistema de regras, nao como estetica
`requiredSkillsToEnter`, `blockedSkills`, `skillCostOverrides` por terreno transformam montanha/floresta/praia em restricoes taticas reais. `Alpino` transforma montanha em rota. `Guerrilha` transforma floresta em coridor barato. `roadBoost` da um extra de movimento em estrada para unidades rapidas. Esses sistemas existem e funcionam — nao sao flavor text.

---

## Gaps e riscos concretos

### 1. IA e completamente reativa — sem memoria entre turnos
`AIWorldSnapshot` e descartado e reconstruido todo turno. Nao ha tracking de tendencias: perdas acumuladas, velocidade de avanco inimigo, construcoes estrategicas perdidas. Uma IA que perde 40% das tropas em 3 turnos e que viu o flanco direito cair nao sabe disso — ela age como se fosse o turno 1. Isso limita muito a sensacao de inteligencia em partidas longas.

### 2. Onboarding inexistente
O sistema e profundo e bem construido, mas nao ha trilha de aprendizado. Um jogador novo nao consegue descobrir por tentativa e erro como logistica, FoW, DPQ e autonomia interagem — sao muitas variaveis. Esse e o gap entre "projeto com mecanicas boas" e "jogo que pessoas conseguem jogar e gostar".

### 3. Stealth marcado como experimental
`05_relatorio_visao_spotting.md` documenta explicitamente: "experimental / nao validado". O sistema existe no codigo (`stealthRevealScope`, `stealthVisibleIfDetectedForTurns`, `detectUnitsWithFollowingSkills`) mas nao foi testado em gameplay real. E uma mecanica de alto impacto tatico que pode estar produzindo comportamentos incorretos sem ninguem saber.

### 4. Save/load de estado de IA nao documentado
Nao foi possivel confirmar se `TeamObjectivePlan`, `AIOperation` e o estado de slots preenchidos persistem corretamente no save/load. Se nao persistirem, a IA recomecarao o turno do zero apos um load — o que pode gerar comportamentos erraticos ou decisoes inconsistentes com o estado do tabuleiro.

### 5. Postura estrategica existe mas nao muda comportamento de forma sistematica
`snapshot.Stance` esta presente mas o codigo de decisao da IA nao usa a postura como gate principal de comportamento. A IA nao muda de perfil agressivo para defensivo baseada no estado global da batalha — as operacoes sao reativas a ameacas locais, nao a uma leitura de "estamos ganhando ou perdendo".

### 6. Sem rotina de balanceamento
Mudancas em custo de unidade, percentual de servico logistico, tabela RPS ou valores de autonomia nao tem checklist de validacao. Cada mudanca e um experimento sem controle — funciona ate aparecer uma regressao nao prevista em gameplay.

---

## Diagnostico tecnico

**O projeto tem uma das arquiteturas mais limpas vistas em jogos Unity indie de estrategia**: separacao de dados, sensores, estado e execucao esta consistente em todo o codebase. O risco tecnico principal nao e divida tecnica — e a ausencia de testes automatizados e de ferramentas de validacao de comportamento de IA, o que significa que a qualidade depende do desenvolvedor lembrar de testar manualmente cada cenario apos cada mudanca.

## Diagnostico de design

**O nucleo mecanico e genuinamente bom**: terreno + logistica + FoW + autonomia criam profundidade estrategica real sem RNG. O risco de design e que toda essa profundidade esta escondida atras de uma curva de entrada muito inclinada. O jogo nao ensina as suas proprias regras — e o primeiro problema a resolver antes de qualquer novo conteudo.

---

## Ja funciona de verdade

- Loop tatico completo: mover, atacar, capturar, suprir, embarcar, desembarcar, fundir
- Combate deterministico com RPS, elite, DPQ, terreno
- FoW com LOS, especializacao por dominio, forward observer, stealth parcial
- Logistica com supply finitio, formula unificada, autonomia por tipo de motor
- Economia por territorio com regras de mercado por construcao
- IA cobrindo todos os papeis de unidade incluindo operacoes aereas completas
- Cadeia de comando IA: Operacoes → Plano → Shopping → Execucao por papel
- State machine com 34 estados e lockout de input durante execucao

## Existe mas ainda esta incompleto

- Stealth (validado em codigo, nao em gameplay)
- Postura estrategica da IA (campo existe, influencia e limitada)
- Onboarding / tutorial
- Metricas de qualidade de decisao da IA
- Documentacao de save/load de estado de IA

## Proximos passos mais sensatos

1. **Tutorial minimo viavel** — uma partida guiada que cubra movimento, ataque, logistica e captura. Nao precisa cobrir tudo — so o suficiente para o jogador descobrir o resto sozinho.
2. **Validacao de stealth** — uma sessao focada de playtesting dos casos limite: detectar, revelar, atirar na janela.
3. **Checklist de regressao** — 5 cenarios de partida rapida executados a cada mudanca de balanceamento. Nao precisa ser automatizado — precisa existir.
4. **Memoria de tendencias na IA** — rastrear perdas e avanco inimigo entre turnos para que a postura realmente mude o comportamento.
