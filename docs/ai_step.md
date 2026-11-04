# AI Step: revisar, marcar e guardar decisoes

Ative **AI Step** no AI Manager (`AIController`). Pode combinar com Show AI Logs,
AI Unit HUD, Start on Pause e Hard Mode. A ferramenta nao muda a dificuldade;
menu e save continuam sendo as fontes de dificuldade de seus respectivos fluxos.

A janela aparece no turno da IA, entre preparar a acao e envia-la ao replay.
Arraste pelo titulo. O fundo e escuro, quase opaco; cliques e rolagem dentro da
janela nao selecionam hexagonos, cancelam acoes ou iniciam pan/zoom do mapa.

## Controles

- **Executar decisao** / F11: executa exatamente a acao exibida, sem recalcular.
- **Salvar marca**: acrescenta uma linha completa em `ai_step_marcas.jsonl`.
  Nao executa, aprova nem recalcula nada. O caminho aparece na janela.
- **Continuar automaticamente**: aprova a atual, desliga AI Step e libera a pausa.
- **Fechar e manter pausada**: conserva a decisao pendente.
- `show ai step`: alterna visibilidade; `show ai step on` abre, `off` fecha. Abrir reativa AI Step se ele foi desligado por Continuar automaticamente, sem aprovar nenhuma acao.
- O Inspector tem **Mostrar revisao AI Step** e exportacao JSON.
- F12 sozinho e desligar o toggle nao aprovam uma revisao pendente.

Start on Pause permite ao calculo avancar ate a revisao, onde a execucao espera.
Menus do jogador continuam segurando a IA. Recalcular apos descarte nao existe:
decidir ainda pode registrar reservas internas. Para repetir, recarregue o save.

## Registro por passo (schemaVersion 2)

Cada passo e salvo automaticamente em:

`Application.persistentDataPath/AIReviews/<sessionId>/step-000001.json`

O campo **Nota opcional** e uma TextArea de ate 2048 caracteres, com quebra
automatica de linha e altura que cresce de tres linhas ate um limite para
preservar os botoes. Enter insere uma nova linha; Escape sai da edicao sem
executar a decisao. Enquanto tem foco, os atalhos externos ficam bloqueados.
As quebras sao preservadas no registro e escapadas no JSON: cada marca continua
ocupando uma unica linha fisica do arquivo JSONL.

Todas as marcas ficam em **um arquivo acumulado**, `ai_step_marcas.jsonl`, na raiz
do projeto no Editor (em build, no persistentDataPath). Cada clique acrescenta
uma linha JSON completa: `format`, `markId`, `markedAtUtc` e `step`, incluindo
nota, fotos, candidatos e contexto de save. O arquivo nunca e apagado ao dar Play;
`sessionId` separa partidas. Repetir a marca acrescenta outra linha e conserva a
nota anterior. A marca e a fotografia daquele clique, portanto pode ter status
pendente. O resultado posterior atualiza o arquivo individual do passo e o replay,
sem reescrever o JSONL. Nao sao mais criadas copias individuais em `marked/`.

Arquivos individuais de passos usam substituicao por temporario; o JSONL usa
append com flush ao fechar. Uma interrupcao durante escrita pode deixar a ultima
linha incompleta: o leitor deve reportar/ignorar essa linha sem perder as demais.
Falhas aparecem na janela sem aprovar a acao. Sessoes tem identificador unico.

`nearestSave` aponta para o checkpoint observado mais proximo, preferindo um
anterior ao passo: slot de save, caminho, data UTC, hash, turno, slot ativo e indice
de replay. `relation` distingue before_step, after_step, same_replay_boundary e
unknown_order; `replayActionsToStep` informa a distancia. Somente saves/loads
bem-sucedidos desta linhagem entram. Load redefine a linhagem. Arquivo removido ou
sobrescrito nao e usado como referencia. Sem checkpoint valido, `found=false`
explica a ausencia. Nao cria nem sobrescreve saves automaticamente e nao garante
reproducao exata: um checkpoint e o ponto de partida, nao a foto inteira do passo.

O registro inclui:

- turno, slot, fase, revisao do tabuleiro, caixa, decisao, missao e comandos;
- `stateBeforeDecision`: foto capturada antes da chamada de decisao;
- `stateBeforeExecution`: foto da revisao anterior a execucao;
- unidades proprias e inimigos visiveis (HP, municao, combustivel, movimento,
  posicao, acao e embarque), construcoes visiveis ou lembradas, terreno conhecido,
  plano, dificuldade e destinos reservados;
- `candidates`: categoria, acao, alvo, celula, status, gate e motivo; `hasScore`,
  `score`, `scoreScope`, `hasRally`, `rally` e preferencia de embarque quando existem;
- `rawReasons`: logs originais, alem de `reasons` com coordenadas padronizadas;
- `actionJson`, resultado do automata, marcacao e nota opcional.

Sao copias de valores feitas naquele instante, nao referencias a unidades vivas.
Marcar depois nao captura outra foto. A foto e um diagnostico da informacao
disponivel ao slot, **nao um save completo restauravel**. Dados inimigos ocultos
nao entram; construcoes apenas lembradas nao recebem HP de captura/posse atuais.
Falha na foto e indicada em `snapshotWarning` e nos flags `hasStateBeforeDecision`/`hasStateBeforeExecution`, sem alterar a decisao do jogo.

Compras sao planejadas em conjunto no inicio da fase: a foto da decisao e a do
planejamento; a foto anterior a execucao e de cada compra individual. Uma unidade
decide e executa antes de a seguinte decidir.

## Alternativas: cobertura real

Hoje estao instrumentados captura local, gates do sensor de captura, gates/opcoes
de embarque do capturador, politica de ataque, pontuacao do UnitPicker de compras
e precedencia da liberacao de produtora. Outros handlers ainda dependem de
`rawReasons`; `coverage` registra esse limite. Nenhum scan ou decisao extra roda
para fabricar uma alternativa.

Guarda ate tres candidatos pontuados e tres recusados por grupo, com teto de 96
entradas por decisao. `candidatesObserved` informa o total observado antes do corte.
Pontuacoes so se comparam dentro de `scoreScope`, nunca entre politicas distintas.
Um branch encerrado por prioridade aparece como `not_evaluated` e `hasScore=false`.
Exemplo: captura local escolhida antes de embarque nao implica que embarcar teve
uma pontuacao menor. O embarque nem chegou a ser avaliado.

## Agente, arquivo e replay

```bash
unity command script eval --file tools/ai_test/ai_step.cs
```

Retorna um indice compacto com ids, decisoes, marcacoes e caminhos. Para "olha as marcas", leia `ai_step_marcas.jsonl`; cada linha ja inclui a nota e o passo completo. O indice em memoria tem teto de 100 passos; os
arquivos anteriores permanecem no disco.

O mesmo registro acompanha as acoes reais do automata. Ao salvar, o replay guarda
`PlayerAction.AIReviewJson`; o load preserva o texto, sem consultar a IA novamente.
Nao ha payload de diagnostico em acoes sem revisao. Com uma acao selecionada no
replay, consulte:

```bash
unity command script eval --file tools/ai_test/replay_ai_step.cs
```

Marcar tambem gera `ai_step.marked` no diagnostico, se Diagnostico no Save estiver
ligado, com identificadores e caminho. O registro completo esta no arquivo e no
replay das acoes executadas. Um passo ainda pendente ja tem arquivo mesmo antes
de entrar no replay. Sucesso do automata nao prova qualidade estrategica: consulte
JogadasLog para os efeitos realizados.

## Previa no mapa

Linhas tracejadas com setas: ciano = movimento, vermelho = ataque, roxo = transporte,
amarelo = outras interacoes. Cada alvo dos SubSteps tem sua ligacao; interacao no
proprio hex mostra anel tracejado. Reaproveita material/largura/sorting do spotter,
sem mover cursor/unidade nem chamar sensores. Fecha ou aprova: a previa desaparece.

## Verificacao

Fora de Play, com Batalha aberta:

```bash
unity command script eval --file tools/ai_test/verificar_ai_step.cs
unity command script eval --file tools/ai_test/verificar_ai_pairs.cs
unity command script eval --file tools/ai_test/verificar_ai_review_v2.cs
unity command script eval --file tools/ai_test/verificar_ai_checkpoint.cs
```

Os dois ultimos testam ranking, gates, alvos, coordenadas, append de marcas, roundtrip do replay e escolha do checkpoint com dados sinteticos, sem executar jogadas ou sobrescrever saves reais. Seus arquivos temporarios
sao removidos. Nao use `unity recompile` em Play: preserve a partida.

## Clicar nas alternativas

As entradas em Alternativas e Gates sao botoes. Clique para destacar a entrada e
mostrar apenas os pares daquela alternativa no mapa. Voltar a decisao escolhida
restaura os pares do batch original. A selecao visual reseta no proximo passo.
Executar decisao sempre executa a escolha original da IA, nunca a alternativa
que esta sendo inspecionada.

A previa usa actionJson e a foto guardada no registro, sem consultar unidades
vivas, rodar sensores, mover cursor/camera ou recalcular a IA. Ataque usa a celula
de tiro registrada e a posicao congelada do inimigo; captura mostra deslocamento
e anel no destino; embarque liga a unidade ao transporte sem inventar o caminho
de aproximacao; compra marca a produtora. Alternativa recusada mostra o gate e
linhas atenuadas. Ramo nao avaliado ou alvo sem posicao registrada mostra uma
explicacao em vez de inventar linhas. A linha e uma ligacao visual, nao prova de
caminho ou permissao legal.

Teste de dados sinteticos, sem executar jogadas:

`unity command script eval --file tools/ai_test/verificar_ai_candidate_preview.cs`

## Postura no cabecalho

`current_stance` aparece acima da area rolavel. O valor e a postura estrategica
capturada no snapshot do turno (ou no snapshot de compras para a fase de shopping).
Fica guardado no registro junto com `stanceSource`; nao e recalculado ao abrir a
janela ou consultar uma marca antiga. Registros anteriores sem o campo mostram
"nao registrada neste passo". O fundo escuro foi suavizado para maior transparencia.

## Classificacao da nota

Antes da TextArea ha cinco opcoes: Duvida, Estranho, Possivel bug, Sugestao e
Observacao. A selecao inicial e Duvida e a ultima escolha e mantida entre passos.
Salvar marca grava `classification` na linha JSONL e `step.markClassification`
no registro/replay, com codigos estaveis: `duvida`, `estranho`, `possivel_bug`,
`sugestao`, `observacao`. A classificacao expressa a intencao do autor; Possivel
bug nao afirma que o jogo tem um defeito confirmado. O texto da nota e independente.

Marcas antigas sem o campo devem ser lidas como Estranho, sem reescrever o arquivo.
`markedStrange` continua como alias legado de marcado; novos leitores devem usar
`marked` e `markClassification`. A API antiga MarkAIReviewStrange continua
registrando Estranho. A API geral e MarkAIReview(note, classification).

## F10: pausa com prioridade

F10 sempre tem prioridade no handler de atalhos da IA, mesmo com foco na nota ou
no console. F11/F12 continuam bloqueados enquanto se digita. Se chegarem juntos,
F10 vence. Continuar automaticamente limpa o foco da nota ao desligar AI Step.
A pausa aguarda o ponto seguro do batch em andamento; nao e cancelamento/rollback.

Teste fora de Play: `unity command script eval --file tools/ai_test/verificar_ai_f10.cs`.
