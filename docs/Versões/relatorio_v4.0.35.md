# Relatório v4.0.35 — Save e Load, ajustes no inspect

## Visão geral

Atualização concentrada na segurança visual e sonora do carregamento de partidas, na persistência de construções e em melhorias das informações e marcações apresentadas durante o inspect.

## Save e Load

- O carregamento de partidas agora utiliza o painel de rodada como barreira de privacidade enquanto o estado do jogo é restaurado.
- Durante o load, o painel informa qual jogador está sendo carregado e mantém o botão **Iniciar Turno** desativado até que toda a restauração esteja concluída.
- O painel recebeu espaçamento próprio entre o nome do jogador e o número da rodada durante a apresentação de carregamento.
- A música anterior é interrompida assim que o load começa e permanece bloqueada durante a restauração.
- O som de espera do painel de rodada toca durante o carregamento, e o tema do time só começa depois que o jogador pressiona **Iniciar Turno**.
- Em caso de falha no carregamento pelo menu, a reprodução musical anterior é restaurada.
- Dados runtime de construções, incluindo estoques e estado operacional, passam a ser preservados corretamente pelo save e load.
- A inicialização normal da partida não sobrescreve o estado que está sendo restaurado por um load iniciado no menu principal.

## Inspect e interface

- A janela de estatísticas em **Tools > Utils > Estatísticas** agora apresenta construções controladas, neutras e inimigas.
- Construções inimigas permanecem ocultas nas estatísticas quando o Fog of War está ativo, evitando vazamento de informações como slots e percentual de controle.
- Quando uma unidade está sobre uma construção, o painel auxiliar exibe em uma linha própria os estoques atuais de galões, caixas e peças.
- Ajustados o tamanho da fonte e o espaçamento do estoque para preservar a leitura em painéis estreitos.
- Corrigida a sobreposição entre as marcações de movimento e tiro: a marcação de movimento permanece visualmente acima, enquanto a de tiro fica abaixo sem engrossar indevidamente o contorno.
- Ajustadas transparências e recursos visuais usados pelos alvos e células inspecionadas.

## Validação

- Projeto runtime compilado sem erros ou avisos.
- Fluxos de carregamento, painel de rodada, bloqueio musical, estoques de construções e overlays de inspect revisados.
