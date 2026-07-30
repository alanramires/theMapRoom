# v5.1.1 — Refinamento: Melhor LZ Embarque

## Visão geral

Esta versão refina a ferramenta `Tools > Transporte > Melhor LZ de Embarque`
para transformar uma consulta isolada em um instrumento de acompanhamento da
decisão runtime da IA.

Agora é possível distinguir visualmente:

- a melhor opção do ranking bruto;
- a opção que a política de Pickup aceitaria;
- a unidade que a IA está processando;
- a unidade cujo batch foi preparado pelo modo de passo `F11`.

## Ranking bruto e política runtime

Quando a consulta é feita durante a execução da partida, a janela apresenta
dois resultados:

- **Ferramenta — ranking bruto:** preserva a ordenação completa do sensor;
- **Política runtime — Pickup:** simula a filtragem usada pela operação real.

A política runtime:

- prioriza `Tactical → Operational → Strategic`;
- descarta passageiros classificados como `OpportunisticFallback`;
- exige encontro materializável para passageiros terrestres;
- preserva a exceção de camada das aeronaves;
- procura o próximo pedido elegível quando a melhor nota bruta representa uma
  carona recusada.

A opção bruta permanece marcada em amarelo. Uma escolha runtime diferente é
marcada em azul, recebe o rótulo `[RUNTIME]` na lista e é desenhada na Scene
View.

Com o jogo parado ou pausado fora do fluxo runtime, a ferramenta mantém apenas o
retrato atual, evitando apresentar como runtime uma decisão inexistente.

## Auto Detect

O botão `Auto Detect` deixou de apenas procurar mapa e banco de terrenos.

Durante o Play Mode ele agora:

1. tenta obter `TurnStateManager.SelectedUnit`;
2. se não houver seleção de TurnState, tenta a unidade do batch preparado pelo
   `F11`;
3. preenche o campo `Transportador`;
4. seleciona o GameObject no Editor;
5. centraliza a Scene View na unidade;
6. limpa resultados antigos antes de uma nova consulta.

O status informa a fonte utilizada:

- `TurnStateManager.SelectedUnit`;
- `batch preparado pelo F11`.

Também informa quando a unidade encontrada não é transportadora ou quando a IA
está entre batches e não possui unidade detectável.

## Diagnóstico do F11

`AIController` passou a expor
`TryGetDebugStepPendingUnit(out UnitManager)` como leitura diagnóstica.

O método:

- consulta somente o `UnitInstanceId` da ação pendente;
- resolve a unidade no registro runtime;
- não seleciona unidade no jogo;
- não executa, substitui ou cancela o batch;
- não altera a requisição de step.

Isso cobre o intervalo em que o primeiro `F11` já preparou e exibiu o batch, mas
o TurnState continua em `Neutral` e ainda não possui `SelectedUnit`.

## Contrato transacional

As mudanças são observacionais.

- a seleção realizada é a seleção do Editor, não a seleção de gameplay;
- nenhuma ação pendente é comprometida;
- FOW, ocupação, recursos, sensores, revisões e caches confirmados não são
  modificados;
- a autoridade da decisão permanece no `AIController`, nos sensores e no fluxo
  explícito de confirmação.

## Logs do TurnState incluídos no checkpoint

O worktree também contém uma limpeza dos rótulos de diagnóstico do TurnState.
`LogStateStep`, `Advance`, `Retreat` e `ExecuteAndReset` passaram a usar
`CallerMemberName`, reduzindo strings duplicadas e divergências entre o nome do
handler e o texto registrado.

Detalhes adicionais continuam podendo ser informados explicitamente. A mudança
é de instrumentação e legibilidade dos logs, sem alterar a máquina de estados.

O checkpoint inclui ainda o estado atual de `.claude/settings.local.json`,
conforme o escopo integral solicitado por `git add .`.

## Validação

- `Assembly-CSharp-Editor.csproj`: compilação concluída com 0 erros;
- avisos exibidos permanecem os avisos preexistentes do projeto;
- a leitura do batch F11 é somente diagnóstica;
- o Auto Detect não chama comandos de gameplay;
- o teste visual confirmou uma divergência legítima:
  uma opção Tactical `OpportunisticFallback` foi rejeitada e um pedido
  Strategic `Requested` foi escolhido pela política runtime.

## Roteiro de teste

1. pausar a IA com `F10`;
2. pressionar `F11` para preparar o próximo batch;
3. clicar em `Auto Detect`;
4. confirmar no status a origem `batch preparado pelo F11`;
5. calcular o Melhor LZ;
6. comparar o amarelo do ranking bruto com o azul da política runtime;
7. pressionar `F11` novamente e verificar que a ferramenta não interferiu na
   execução normal do batch.
