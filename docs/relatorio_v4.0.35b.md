# Relatório v4.0.35b — Telas, ajustes visuais, qualidade de vida para o jogador

## Visão geral

Atualização concentrada na apresentação das informações de turno, logística e inspeção, com navegação mais consistente por teclado e mouse e maior clareza sobre o que acontecerá antes da confirmação das ações.

## Painel de rodada e carregamento

- O painel de rodada passa a exibir vídeos temáticos por time enquanto aguarda o jogador iniciar o turno.
- A reprodução visual ocorre apenas depois da conclusão do carregamento, evitando travamentos perceptíveis durante o pre-load.
- Vídeo e música de espera permanecem ativos até a confirmação em **Iniciar Turno**.
- O primeiro frame e os fallbacks dos vídeos foram ajustados para não exibir uma prévia incorreta de outro time.
- O fluxo de load mantém cursor, menus, atalhos e ações de tabuleiro bloqueados até o início efetivo da rodada.

## Inspect e Panel Helper

- Inspeções em altas e baixas altitudes podem apresentar simultaneamente o domínio aéreo e a construção sobrevoada.
- Sprites de terreno, construções, unidades embarcadas e alvos são exibidos claros, preservando a cor do time sem reproduzir o escurecimento momentâneo do mapa.
- Estoques de construções mostram valores atuais e máximos de galões, caixas de munição e peças.
- Ajustados espaçamentos, margens, hierarquias visuais e áreas clicáveis dos painéis de unidade, construção e transporte.
- Corrigido o comportamento de arrastar o painel de inspeção para construções, evitando fechamento acidental ao clicar no título.

## Consumo em voo

- Novo relatório visual de consumo em voo no início da rodada, com sprites, combustível antes/depois, localização e barras de autonomia.
- Unidades com menor autonomia aparecem primeiro para facilitar a identificação de riscos.
- As linhas são clicáveis e navegáveis, realizando apenas pan da câmera sem deslocar o cursor do tabuleiro.
- O relatório pode ser reaberto pelo botão **Consumo** no menu do jogador; nesse modo permanece aberto até cancelar.
- Cancelar, `Esc` e botão direito retornam corretamente ao menu que originou o relatório.

## Serviço do Comando

- O preview foi convertido de uma lista textual para cartões por unidade, preservando integralmente a fila, os custos e os atendimentos definidos pelo planner logístico.
- Cada cartão apresenta sprite na cor do time e os ganhos prometidos; atendimentos parciais e unidades não atendidas recebem diferenciação visual.
- Setas percorrem unidades, **Executar** e **Cancelar** em um único fluxo vertical.
- Focar ou clicar em uma unidade faz somente pan da câmera, sem mover o cursor nem comprometer a ordem.
- Cabeçalho e rodapé permanecem fixos; listas extensas usam viewport central limitado e rolagem automática até a unidade em foco.
- Previstos, custo previsto e saldo restante foram separados em linhas próprias.

## Transferência e hidroavião

- Doações agora possuem uma etapa intermediária para escolher `25%`, `50%`, `75%` ou `100%` do estoque disponível.
- Recebimentos continuam buscando o máximo possível até a capacidade do recebedor ou o limite da fonte.
- Adicionado suporte a unidades híbridas que precisam estar pousadas para transferir suprimentos.
- O hidroavião pode pousar para realizar a transferência e permanece pousado até uma decolagem normal em turno posterior.
- Ferramentas de debug e validações de logística foram atualizadas para o novo contrato do sensor de transferência.

## Qualidade de vida e validação

- Navegação, foco, cores de time, botões de cancelar e retorno aos menus foram uniformizados entre os principais painéis auxiliares.
- Projeto runtime compilado sem erros após as alterações.
- As prévias continuam sem consumir recursos ou alterar definitivamente o tabuleiro antes da confirmação explícita do jogador.
