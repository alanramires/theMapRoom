# v2.2.6 - AI vs Time Verde

## Contexto

Rodada de calibracao em partida Time Verde vs AI, com foco no comportamento da AI vermelha no inicio do T2 e T3. O objetivo foi validar se o pipeline de Intel, Planner, Operations, Shopping e Command Service esta interpretando corretamente a pressao do time verde, as capturas iniciais e a divisao territorial do mapa.

## Leitura da AI Intel

- `Acoes recentes` representa eventos registrados no log dentro da janela configurada, nao quantidade de unidades nem poder de fogo. No T3, `inimigo=18` indica volume de jogadas recentes do time verde.
- `Forca` representa a contagem viva conhecida no snapshot: no caso analisado, AI com 6 unidades contra 9 unidades inimigas.
- A Intel detectou corretamente pressao de captura em Alpha e Bravo, com Alpha como setor mais quente e Bravo como eixo secundario relevante.
- `Base1` foi interpretada corretamente como HQ inimigo para a AI vermelha, enquanto `Base2` foi usada como base propria nas operacoes preventivas.
- A ausencia de dano e desembarque manteve a leitura da ameaca centrada em infantaria, captura e expansao terrestre.

## Fluxo observado no T3

- A AI iniciou o turno em `Offensive`, com 6 unidades, nenhum inimigo visivel e R$ 7000.
- O Planner escalou o cap de objetivos para 6 em funcao da quantidade de setores do mapa.
- Objetivos existentes em Bravo, Charlie, Delta, Echo, Foxtrot e Golf ocuparam o cap ofensivo.
- Alpha foi reconhecido como setor quente, mas descartado por cap atingido mesmo com prioridade alta.
- Base1 tambem foi reconhecida como base inimiga, mas ficou fora do plano pelo mesmo limite de objetivos.
- O Intent Analyzer marcou:
  - Alpha como `Intercept`, por owner Green, hot alto e pressao de captura.
  - Bravo como `Attack`, por objetivo pendente/disputado.
  - Charlie, Delta, Echo, Foxtrot e Golf como `Attack`, por objetivos de captura em setores ainda neutros.
  - Base2 como foco de `PreventiveDefense`.

## Decisoes de operacao

- GroundCapture foi criado para os objetivos ativos, com reforco de Assalto em setores considerados arriscados.
- Bravo recebeu demanda de dois capturadores e um assalto, coerente com pressao no territorio do time verde.
- Charlie e Echo tambem foram tratados como arriscados por presenca/hot intel.
- PreventiveDefense abriu slots para artilharia e AAA na base propria, refletindo lacunas de composicao.
- O Command Service foi executado antes das acoes de unidade, como esperado pelo fluxo atual de turno.

## Pontos de atencao

- O stance ofensivo ainda depende demais de inimigos visiveis. Com FoW ocultando o time verde, a AI pode manter postura agressiva mesmo quando a Intel historica aponta pressao alta.
- Alpha ser descartado por cap mesmo com `hot=14` indica que o Planner precisa permitir preempcao de objetivos menos urgentes quando ha ameaca quente em territorio inimigo ou setor critico.
- Setores do lado vermelho como Charlie, Delta e Echo aparecem como `owner=Neutral`. Se o design espera que esses setores sejam tratados como territorio natural da AI, o Planner precisa consultar uma nocao de zona territorial alem de `ControllingTeam`.
- O texto `Acoes recentes` pode ser renomeado ou expandido para evitar confusao com forca militar.

## Resultado

O pipeline esta coerente de ponta a ponta: Intel gera sinais, Planner cria objetivos, Operations transforma objetivos em necessidades e Shopping reage a lacunas. A calibracao principal agora e de prioridade macro: setores quentes como Alpha precisam furar o cap quando representam captura ativa do time verde, e o stance deve ponderar Intel historica quando a visibilidade estiver baixa.
