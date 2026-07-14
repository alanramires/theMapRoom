# Relatório v4.0.31b — Tutorial Fixes

## Visão geral

Atualização incremental dedicada ao primeiro tutorial, com ajustes de ritmo, enquadramento, instruções e apresentação do `panel_helper`.

## Tutorial — Aprendendo a Atirar

- Revisados e redistribuídos textos introdutórios para reduzir blocos excessivamente longos.
- A instrução de arrastar o mapa passa a ter sua própria etapa.
- Refinada a explicação de HP e da representação do esquadrão de dez soldados.
- Comandos de alteração de HP e autonomia passam a centralizar a câmera na unidade correspondente.
- Ajustadas instruções sobre floresta, montanha, passagem de turno e manutenção de posição.
- A explicação de **Manter Posição** foi dividida para melhorar o ritmo antes da observação do inimigo.
- Atualizados o `TutorialData` e a cena **História 1 - Aprendendo a Atirar**.

## Interface e ações

- Ao confirmar uma unidade que permaneceu parada, o `panel_helper` exibe **M - Confirmar Posição**.
- Depois de um deslocamento, o rótulo permanece **M - Apenas Mover**.
- A mudança é somente visual e preserva o mesmo fluxo transacional de compromisso da ação.
- A alça móvel do `panel_helper` passa a ser criada exclusivamente em Play Mode.
- `OnValidate` não cria nem reparenta mais objetos ao validar Prefab Assets, evitando alças órfãs e erros ao fechar cenas.

## Hot seat e conteúdo

- A partida PvP hot seat passa a disponibilizar 100 mil para cada jogador no início.
- Atualizados o mapa hot seat e os assets de fontes utilizados pela interface.

## Testes e validação

- Incluídas configurações geradas para execução dos testes de desempenho.
- Projeto runtime compilado sem erros.

