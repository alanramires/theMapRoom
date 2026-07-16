# Relatório v4.0.35a — Ajustes no inspect e no FOW

## Visão geral

Atualização de correção concentrada na apresentação do inspect, no bloqueio de informações protegidas pelo Fog of War e na segurança do fluxo de início de rodada após load ou troca de jogador.

## Inspect e Fog of War

- Alvos válidos e inválidos em células desconhecidas deixam de aparecer nas opções de mira, evitando vazamento de unidades inimigas pelo inspect.
- A validação de corredores de tiro passa a utilizar o conhecimento confirmado do time, incluindo visões especializadas e construções aliadas.
- A unidade selecionada em posição provisória é excluída da composição de visão, impedindo que uma ação cancelável revele ou valide informações do destino.
- As marcações de alcance de tiro continuam apresentando o envelope da arma sem transformar células ocultas em confirmação de alvos.
- Ajustada a ordem de renderização de unidades em hexes multicamada para manter a unidade do jogador visível sobre contatos inimigos empilhados.
- Refinados recursos visuais, fontes e configurações usados pelo inspect e pelos elementos do mapa.

## Painel de rodada e load

- Todo o upkeep da troca entre jogadores humanos aguarda a confirmação em **Iniciar Turno**, incluindo economia, reset das unidades, pousos emergenciais e quedas de aeronaves.
- A opção de debug que desativa o painel preserva o comportamento imediato anterior.
- Cursor, atalhos, menus e ações do `TurnStateManager` ficam integralmente bloqueados durante o load e enquanto o painel aguarda a confirmação do jogador.
- O áudio do load segue a sequência `menu_open` e `aguardando`, mantendo o som de espera em loop até o jogador iniciar a rodada.
- Mesmo em loads rápidos, o painel permanece em **Carregando** até a sequência de abertura alcançar o som de espera; somente então apresenta **Vez do Time** e libera o botão.

## Validação

- Projeto runtime compilado sem erros.
- Fluxos de inspect, FOW, load, bloqueio de input e início de upkeep revisados.
