# v4.1.0 - Manual da Sala de Mapas

Esta versao inaugura a serie 4.1 com documentacao ampliada da Sala de Mapas, novas unidades e especializacoes de combate, refinamentos de DPQ e correcoes de apresentacao, save/load e Fog of War.

## Manual e documentacao

- Adicionado o manual tecnico da Sala de Mapas, reunindo regras, sistemas e referencias de operacao do jogo.
- Documentadas a carga entre mapas diferentes e as regras de deteccao e caca.
- A matriz geral de combate pode ser exportada em CSV para consulta e balanceamento.

## Combate aereo e unidades

- Catalogo PvP atualizado com novas configuracoes para Caca F e Bombardeiro F.
- Variantes visuais de aeronaves passaram a distinguir estados pousado, voo baixo e voo alto.
- Novos traits de combate aereo `Dog Fight Interceptor` e `Dog Fight Master`.
- EWACS e Radar Movel receberam ajustes de visao especializada e deteccao.
- O indicador de unidade detectada permanece coerente para unidades com capacidade stealth, inclusive fora da camada preferencial da skill.

## DPQ e Grande Matriz

- A Grande Matriz ganhou seletores independentes de DPQ para atacante e defensor.
- Pontos de DPQ controlam o matchup e o arredondamento; o bonus de defesa entra na defesa efetiva de cada lado.
- Revide, detalhes da celula e calculadora simples respeitam os DPQs selecionados.
- Valores padrao de DPQ passam a ser derivados da qualidade em runtime quando configurados para usar o preset.
- Arredondamento neutro de combate usa metade para cima, eliminando o arredondamento bancario em resultados terminados em `0,5`.

## Fog of War e deteccao

- Terreno revelado e unidade detectada passam a ser tratados como informacoes distintas.
- O EWACS pode abrir o tampao em seu grande alcance especializado sem revelar automaticamente ocupantes fora da visao aplicavel ao alvo.
- Em hex aberto, sprites e HUDs inimigos obedecem ao snapshot confirmado de deteccao; em hex ainda coberto, permanecem renderizados sob o overlay para preservar o movimento natural pelos recortes do FOW.
- `PodeMirar` continua aceitando somente alvos efetivamente observados pelo time.
- Stealth preserva sua validacao individual e o Fog of War nao usa posicoes provisorias como verdade confirmada.

## Save, load e Hot Seat

- O load iniciado pela Tela de Entrada deixa de confundir o gate de privacidade do Hot Seat com um turno ativo da IA.
- Depois da confirmacao do `Panel_Rodada`, o gate temporario e liberado, restaurando cursor, menus e musica em partidas jogador contra jogador.
- O bloqueio normal de save/load durante turnos reais da IA permanece ativo.

## Apresentacao e empilhamento

- Submarinos ficam em primeiro plano quando compartilham hex com navios ou unidades terrestres sobre pontes.
- Duas aeronaves sozinhas no mesmo hex usam duas linhas visuais, como no caso aereo mais terrestre.
- Quando uma unidade terrestre ou naval de superficie entra no hex, as aeronaves retornam ao leque horizontal superior e a superficie ocupa a linha inferior.
- Reorganizacoes visuais de coabitacao continuam ocorrendo somente no estado confirmado `Neutral`.

## Conteudo e interface

- Cena e catalogo Hot Seat PvP receberam os ajustes de conteudo preparados para a nova versao.
- Porto, fontes, prefabs e recursos graficos foram atualizados junto ao novo conjunto de unidades e documentacao.

## Validacao

- Assembly principal compilado com sucesso, sem erros.
- Assembly do Editor compilado com sucesso, sem erros.
- Arquivos de codigo passaram por `git diff --check`.
