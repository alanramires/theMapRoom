# Relatorio de Atualizacao - v2.0.23

## AI tank elite 2

Esta versao ajusta a progressao economica e ofensiva da IA no fim de jogo, com foco em escalar de tanks elite nivel 1 para nivel 2 e em transformar a base inimiga em objetivo real quando os setores comuns ja estao cobertos.

## Em uma frase

A IA agora monta um exercito de elite com mais intencao: depois de consolidar tanks elite, passa a mirar tank elite 2 ou pivotar para fire support elite, enquanto transporte e captura continuam coordenados contra a base inimiga.

## Compras e elite 2

- O planejador conta tanks de assalto elite ativos em campo, em vez de depender apenas de slots do plano.
- Com dois ou mais tanks elite ativos, a IA passa a procurar alvo de assalto elite nivel 2.
- Se nao houver elite 2 disponivel, o planner volta para elite nivel 1 como fallback.
- A reserva de tank elite e bloqueada quando o pivot de composicao pede fire support elite.
- Logs novos indicam quando a IA entra no modo `dream_team_pivot`.

## Dream team pivot

- Apos atingir massa de tanks elite, a IA deixa de comprar apenas mais blindados.
- O alvo passa a incluir fire support defensivo elite para equilibrar a composicao.
- A demanda usa contagem real de unidades em campo, funcionando tambem quando o plano esta vazio em modo rogue.
- Fire support elite pode ser comprado mesmo fora da regra anterior de limite baixo de suporte, desde que o pivot esteja ativo.

## Base inimiga como objetivo

- Bases inimigas entram no plano ofensivo quando os setores regulares ja estao cobertos.
- O dono canonico da base passa a ser o HQ do setor, evitando erro causado por `ControllingTeam` incorreto em construcoes capturaveis.
- A IA exige co-chegada de capturadores antes de abrir objetivo de base inimiga.
- O numero de capturadores escala com a quantidade de construcoes da base, com minimo de dois.
- Em postura defensiva, a IA so abre esse objetivo se ja houver captura parcial propria em andamento.

## Transporte e captura

- Capturador rogue, sem setor atribuido, pode aceitar APC livre sem passageiro formal.
- Embarque estendido usa a posicao apos movimento como referencia de pickup.
- Transporte atribuido evita pegar passageiro de outro setor.
- APC vazio perto de base inimiga deixa de ser liberado como se estivesse em destino seguro.
- O calculo de distancia efetiva para transporte passa a considerar bases inimigas no fim de jogo.
- Ataque oportunista foi removido do fluxo de pickup atribuido para nao atrasar a funcao principal do transporte.

## Editor e debug

- O editor de setores agora mostra o time controlador junto das distancias de HQ no foldout.
- As distancias aparecem ordenadas, deixando a leitura de setores e bases mais direta.
- Logs de fire support diferenciam apoio defensivo de apoio a captura de setor inimigo.

## Bloco tecnico curto

- Ajustado `AIShoppingPlanner.cs` para contagem de elite ativo, alvo elite 2 e pivot para fire support elite.
- Ajustado `AIController.PlanEvaluator.cs` para incluir base inimiga como objetivo ofensivo coordenado.
- Ajustados `AIController.Capturer.Embark.cs` e arquivos de `Transportador` para embarque rogue, pickup por setor e fim de jogo.
- Ajustado `SectorManagerEditor.cs` para mostrar controlador e distancias no inspector.
- Adicionado `docs/AI_refine_12052025.md` como registro de analise da sessao de refinamento da IA.

## Resultado

Versao preparada como pacote `AI tank elite 2`, focada em melhorar a progressao de compras da IA e em tornar o ataque final contra a base inimiga mais coordenado e menos passivo.
