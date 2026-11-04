# v9.6.0 — A IA lance a lance: o autor marca, o agente lê

O fio do dia: na v9.5.0 o Claude jogava e perguntava à IA o que ela faria, e isso
custava caro. Cada dúvida exigia ler o código, rodar a decisão e reconstruir o porquê
pelos logs. Hoje o caminho inverteu. O **jogo** grava o raciocínio no instante da
decisão, o **autor** marca o passo que estranhou com uma pergunta, e o agente lê só
aquele registro.

Na primeira tarde de uso, três marcas renderam duas correções na IA e uma correção
de log. Uma delas foi vista funcionando na partida seguinte.

O mapa inicial não tem elite. Por isso o que se revisou é o **núcleo** das decisões:
captura, compra de massa, handoff e postura. Tudo o que vier depois apoia nisso.

---

## 1. AI Step: a tela que mostra o pensamento antes da execução

*Escrita por outro agente, com o autor. Documentação completa em `docs/ai_step.md`.*

**O que é.** Uma janela que aparece no turno da IA, entre preparar a ação e enviá-la
ao replay. Ela mostra:

- a decisão;
- a missão;
- os motivos (os logs reais da IA);
- os comandos previstos;
- as alternativas instrumentadas, com uma prévia tracejada no mapa.

**Controles.** Executar, Marcar como estranho, Continuar automaticamente, ou fechar
mantendo a pausa.

**Por que a janela abre antes da execução.** Esse é o acerto de desenho. A foto é tirada
com o tabuleiro **exato** daquele passo. Ninguém recalcula a decisão depois do fato,
quando o estado já mudou.

**O que fica gravado:**

- Cada passo vira `AIReviews/<sessionId>/step-NNNNNN.json` no `persistentDataPath`,
  com fotos antes da decisão e antes da execução, candidatos, `rawReasons`,
  `actionJson` e o save mais próximo para reproduzir.
- Cada marca acrescenta uma linha a `ai_step_marcas.jsonl` na raiz, que entrou no
  `.gitignore`. A linha traz o passo inteiro e a nota do autor.
- O registro também viaja no replay (`PlayerAction.AIReviewJson`), e marcar gera
  `ai_step.marked` no diagnóstico do save.

**Onde mexeu:**

- `AIController.Debug.cs` concentra a maior parte.
- Phase 2 e Phase 3 passam pelos pontos de revisão (`DecideUnitActionForReview`,
  `DecideShoppingForReview`).
- Os papéis ganharam `TraceAIReviewCandidate`: Router, ataque, captura, embarque e
  UnitPicker.
- Entraram no circuito: `ReplayManager`, `PlayerAction`, `SaveGameManager`,
  `TurnStateManager.ScannerPrompt` e o bloqueio de entrada enquanto a nota está
  sendo editada (cursor, câmera, `UiInputBlocker`, menu).
- Scripts de verificação com dados sintéticos: `tools/ai_test/verificar_*.cs`.

**Ideias que vieram da conversa.** Duas sugestões do Claude entraram na ferramenta:
o botão "Marcar como estranho" e o campo de nota de uma linha. A nota vale mais que
o resto do registro, porque sem ela o agente chuta o que incomodou.

**De passagem.** `MatchController` só suja a cena quando a renda muda de fato. Antes,
o Inspector a marcava como modificada a cada repaint.

## 2. O método de teste da IA virou documento e ferramenta

Os scripts com que o Claude jogou na v9.5.0 viviam na pasta temporária da sessão e
morreriam com ela. Agora estão em `tools/ai_test/`, fora de `Assets/`:

- `estado`, `ia_plano`, `ia_unidade`, `ia_compra`, `opcoes`, `agir`, `conferir`,
  `jogadas`;
- os parâmetros ficam no topo de cada script.

`docs/ai_test.md` reúne:

- o método;
- as **8 regras** que custaram um erro cada: batch e não chamada por dentro, uma
  unidade por vez, plano antes do ensaio de compra, nunca `unity recompile` em
  Play...;
- a tabela de achados;
- a referência do autor no Difícil ("formiga + counter");
- o plano de cenários de teste.

`docs/ai_step_investigacao_2026-11-04.md`, do outro agente, lê as mesmas marcas por
outro ângulo. Ele achou um defeito que o Claude também viu: nas compras, o campo
`candidates` vem vazio. A causa é que a instrumentação está no `UnitPicker`, mas o
shopping por papéis usa outro caminho (`BuildBestRoleShoppingCart`).

> **A divergência entre os dois agentes é instrutiva.** O autor pediu ao outro agente
> só a última marca. Ele leu as três, e para a Metranca concluiu "coerente com a
> demanda registrada", um veredito errado e fora do escopo pedido. Isso é verdade e não responde
> a pergunta. O Claude foi ao código do score e achou o mecanismo: o bônus de encaixe
> **atravessa** a prioridade. Explicar a intenção não é o mesmo que provar que a
> decisão é boa. O próprio documento dele diz isso, e foi exatamente ali que estava
> o defeito.

## 3. Grana antes do counter (Médio e Difícil)

Marca: **"porque metranca?"**, T2, slot 1, Difícil.

**O que aconteceu.** O QG e a primeira fábrica compraram Soldado. A terceira fábrica
comprou Metranca para a demanda de counter, que tinha prioridade 16 e score 202.210.
O 3º capturador, de prioridade 15 e mais importante, ficou com score 155.500.

**Por quê.** Em `ScoreRoleShoppingCandidate` o score começa em
`200000 − prioridade × 3000`. Um degrau de prioridade vale 3.000, mas os bônus de
encaixe do counter (classe-alvo e `counterFit`) valem até 18.000 cada. Na prática, a
prioridade só ordena a lista; quem decide é o encaixe.

O counter tinha disparado por **presença**: seis soldados do autor capturando, não
ameaçando. E a Metranca captura a 50%, então a terceira fábrica entregou meio
capturador por 2,5× o preço. A referência do autor no Difícil foi 3 Soldados por
turno e o counter só no T3.

**A correção.** Uma capacidade nova, `expansaoAntesDoCounter`, com grupo próprio em
Capacidades. Ela age quando três coisas valem juntas:

- o mapa está em `EarlyExpansion`;
- há vaga de captura aberta no plano;
- o counter não é urgente.

Nesse caso, `DeferCounterDuringExpansion` tira o counter da fila do turno.
Continuam passando o counter urgente (a pressão passou do limiar de escalada) e a
escalada para elite. Só sai da fila a demanda que é counter **de ponta a ponta**:
`MergeRoleDemand` funde origens com "+", e adiar uma fusão apagaria junto uma demanda
alheia.

**Por que não mexer nos pesos.** Mudar o degrau de prioridade reordenaria a fila
inteira para resolver um caso que só existe na abertura.

**Dificuldade.** Decisão do autor: o Fácil **continua** fazendo a besteira. Esperteza
é capacidade do preset, não ramo de código que erra de propósito. O campo nasce
desligado, e a overlay faz `= !easy`. O autor ligou a caixa nos assets do Médio e do
Difícil.

**Visto rodando.** No T2 da partida seguinte, em Difícil, o log trouxe "expansão
antes do counter: adiado Counter/Infantry x2 ... 5 vaga(s) de captura abertas", e as
três fábricas compraram Soldado.

## 4. A postura media os dois lados em pontos diferentes do ciclo

Visto nas duas compras do slot 1: `stance=Defensive` no T2, sem nenhuma ameaça ao QG.

**A causa.** A primeira metade de `CalculateStance`, a ameaça real ao QG, estava
certa e não disparou. Quem pegou foi a razão global de HP: abaixo de 0,35, a postura
vira Defensiva. O slot 1 tira a foto **antes** de comprar, e o slot 0 já comprou duas
vezes. Fica 30 contra 60, razão 0,33. Quem joga em segundo caía em Defensive no T2 de
toda partida por **ordem de turno**, não por desvantagem. E a postura cascateia por
cerca de 20 portões: shopping, plano, logística, ataque do capturador.

A regra era uma tentativa do autor de fazer a IA notar diferença numérica. A ideia
fica; a medida é que estava errada.

**A correção.** `ProjectOwnProductionHp` soma ao meu lado o que eu ainda vou produzir
neste turno: cada produtor livre leva o corpo mais barato, enquanto o caixa pagar.

> **Hipótese errada do Claude, pega antes de entregar.** A primeira versão projetava
> **sempre**. Isso dava ao slot 0 Ofensivo de graça: 30 contra 0 no T1, 60 contra 30
> no T2. A projeção só iguala as fotos para quem joga **depois** de algum inimigo na
> rodada (`HasEnemyActedBeforeMeThisRound`). A ordem de turno por índice de slot foi
> conferida em `MatchController`.

| momento | antes | agora |
|---|---|---|
| slot 1, T1 (0 contra 3) | 0,00 → Defensive | 0,50 → Tactical |
| slot 1, T2 (3 contra 6) | 0,33 → Defensive | 0,50 → Tactical |
| slot 0, T2 (3 contra 3) | 0,50 → Tactical | 0,50 → Tactical |

Vale nas três dificuldades, porque é conserto de **medida**, não de esperteza.

## 5. O handoff do blitz agora conta a decisão inteira

Marca: **"ele moveu aleatorio? blitzkrieg ou revezamento?"**, T3, slot 0.

A decisão estava certa. O #1 largou a fábrica com 20/30 para o #7, que estava no QG
colado nela, e avançou para Echo. A fábrica fecha no T4 do mesmo jeito, e a ponta
ganha dois turnos.

**O defeito era o log.** A linha era só `1 hard handoff Alpha -> Echo via (7,11)`, e o
blitz não está entre as políticas instrumentadas pelo AI Step. Agora as duas linhas
do handoff trazem `| larga <prédio> (x,y) p/max | herdeiro #N | vai para <alvo>`
(`DescribeBlitzkriegHandoff`).

**A lição generaliza.** Quando a resposta não está no registro, o defeito é o log não
contar a decisão, e consertar o log também conta como correção.

## 6. Afinação do autor nos assets

- **Difícil:** `respeitarListaBanida` desligado, e `bannedOnHardMode` tirado de APC,
  Astros II, Jipe, Obus Leve e Tanque B. O Difícil passa a poder comprar a peça
  básica, coerente com a doutrina "formiga + counter".
- **Médio:** `conscricaoQuandoPerdendo` ligado.
- **Médio e Difícil:** `expansaoAntesDoCounter` ligado.
- O bloco `transportador` apareceu serializado no Difícil. É reserialização: os
  valores são os defaults.

## O que não terminou

- **Blitz no Médio continua desligado.** Pendente desde a v9.5.0, e o asset do Médio
  não mudou nesse campo.
- **A correção da postura e o log novo do handoff não foram vistos rodando.** Validar
  pelo save do slot 2 da primeira marca (T2, slot 1). A fila deve dizer
  `stance=Tactical`.
- **As correções da v9.5.0** (sair da produtora capturando, papel antes da liberação)
  seguem sem observação direta. Só a compra por vaga do plano apareceu (`vagasPlano=5`).
- **O shopping não mostra alternativas no AI Step.** É preciso instrumentar
  `BuildBestRoleShoppingCart`, que é o caminho em uso, e não o `UnitPicker`.
- **Rota do handoff fora de alcance.** Ela escolhe o hex por `HexDistance`, que ignora
  o custo do terreno. Não muda nada em planície, mas erra em floresta ou montanha.
- **"Por que Tanque B?"** Terceira marca, investigada só pelo outro agente: é
  decisão discutível de doutrina, e a pergunta ao autor ficou aberta. Quanto o
  anti-infantaria pode furar a prioridade de Assalto neste tipo de mapa?
- A comparação lance a lance das 3 rodadas Difícil do autor contra a IA, planejada
  na v9.5.0, ficou para o AI Step em modo replay.
