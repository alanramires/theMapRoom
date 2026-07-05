# v4.0.15 - Embarque e Desembarque Review

Esta versão revisa os fluxos de embarque e desembarque para mouse, teclado e interface mobile. As novas interações continuam delegando validação e execução aos sensores e à máquina de estados oficial do turno.

## Embarque

- Transportadores válidos passaram a aparecer como botões no `panel_helper`.
- A seleção inclui navegação circular e opção `CANCELAR`.
- Quando existe apenas um transportador válido, o fluxo preserva o avanço automático para confirmação.
- Clicar em um transportador válido durante as opções abre diretamente `CONFIRMAR EMBARQUE`.
- Clicar novamente no mesmo transportador confirma a operação.
- A confirmação exibe sprite, nome, HP e local do transportador.
- Confirmar e cancelar podem ser acionados por botão ou teclado.
- A seleção e a execução continuam utilizando os resultados oficiais do `PodeEmbarcar`.

## Desembarque em etapas

- O `panel_helper` agora representa separadamente:
  - escolha do passageiro;
  - escolha do local;
  - confirmação do desembarque;
  - revisão e execução da fila.
- Passageiros disponíveis são apresentados como botões clicáveis.
- As quatro setas navegam pela seleção com wrap.
- O loop inclui passageiros, `EXECUTAR FILA` quando disponível e `CANCELAR`.
- Enter executa o item atualmente destacado.
- O foco é reiniciado ao retornar para a seleção de passageiro.

## Atalhos de mouse no desembarque

- Clicar em um hex válido durante as opções funciona como gatilho do procedimento de desembarque.
- Com um único passageiro, o jogo seleciona a unidade, preserva o hex tocado e abre a confirmação.
- Com múltiplos passageiros, o jogo abre `ESCOLHER UNIDADE` sem inferir qual passageiro será usado.
- Durante a escolha do local, o primeiro clique em um hex válido abre a confirmação.
- Um segundo clique no mesmo hex adiciona a ordem à fila.
- Cliques em hex inválido ou em outro local durante a confirmação não executam a ordem.

## Redução de etapas seguras

- Quando há somente um passageiro, a seleção de passageiro é pulada pelo fluxo oficial.
- Quando há somente um local válido, `ESCOLHER LOCAL` é pulado e o fluxo abre diretamente a confirmação.
- A fila é executada automaticamente quando nenhum passageiro restante possui destino válido não reservado.
- Se ainda existir passageiro com local disponível, o fluxo retorna para `ESCOLHER UNIDADE`.

## Fila de desembarque

- Cada ordem mostra o sprite do passageiro, nome, destino e sprite do local.
- Construções têm prioridade sobre o terreno na representação visual do destino.
- O painel ajusta sua altura quando passageiros ou ordens são adicionados e removidos.
- Ordens informativas são visualmente separadas dos botões acionáveis.

## Inferência pós-movimento

- Clicar novamente na própria unidade confirma `Apenas Mover`, mesmo quando outros sensores estão disponíveis.
- `Conquistar` mantém prioridade quando a unidade está sobre uma construção capturável.
- Ações direcionadas continuam sendo inferidas pelo clique em seus respectivos alvos.

## Validação

- `Assembly-CSharp.csproj`: compilação concluída sem erros.
- `git diff --check`: sem erros de whitespace.

