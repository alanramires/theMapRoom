# v4.0.5 - AI Estatisticas

Esta versao introduz a primeira base de estatisticas de partida para depuracao e futura interface do jogador, usando o Jogadas Manager como fonte historica principal e complementando a leitura atual do mapa.

## Tools > Utils > Estatisticas

- Nova janela `Tools > Utils > Estatisticas`, voltada inicialmente para bastidores e validacao.
- Exibe o turno atual e o slot ativo no cabecalho, facilitando prints e comparacoes durante testes.
- Inclui resumo por slot com unidades atuais, compras, perdas, kills, dano, territorio, predios e setores.
- A tela passa a usar rolagem vertical, permitindo crescer sem cortar blocos inferiores em resolucoes menores.
- Botao `Refresh` atualiza a leitura atual; botao `Rebuild log` reprocessa o historico do Jogadas Manager.

## Reconstrucao por Jogadas Manager

- Novo `MatchStatsManager` consolida estatisticas a partir das jogadas registradas.
- Compras, ataques, capturas, embarques, desembarques e fusoes passam a alimentar contadores operacionais.
- Saves sem historico de jogadas exibem aviso explicito: compras, kills e dano nao podem ser reconstruidos quando o log esta vazio.
- O painel combina dados reconstruidos do log com leitura viva do mapa para territorio, predios, setores e unidades atuais.

## Territorio e captura

- O controle territorial segue a regra por pontos de captura introduzida na v4.0.4.
- Predios parcialmente capturados/contestados reduzem o controle de quem esta sob ataque e aumentam a pressao de disputa do atacante.
- A tela diferencia:
  - capture points;
  - predios;
  - setores;
  - full sectors;
  - disputados;
  - cap sob ataque;
  - cap atacando.
- Capturas e reconquistas foram separadas para leitura operacional mais clara.

## Economia, combate e unidades por tipo

- O painel detalha compras, gasto total, unidades atuais, kills, perdas, dano causado e dano recebido.
- Valores monetarios passam a separar:
  - dinheiro gasto em compras;
  - valor proprio perdido;
  - valor inimigo destruido.
- Breakdown por tipo de unidade exibe atual, comprado, perdido, destruido, gasto, valor perdido e valor destruido.
- Isso permite avaliar composicao e eficiencia de cada familia de unidade sem depender apenas de kills.

## Logistica e servicos

- Estatisticas de logistica foram adicionadas ao painel:
  - custo de servico do comando;
  - custo de logistica;
  - custo total de manutencao;
  - quantidade de servicos;
  - custo de reparo;
  - custo de reabastecimento;
  - custo de rearme;
  - HP, AUT e MUN restaurados.
- As rotinas de servico do comando e suprimento passam a registrar informacoes suficientes para alimentar esses contadores.
- Os antigos campos humanos de "entrou reparo" e "saiu reparo" foram removidos do painel, pois pertencem mais a diagnostico da AI do que a estatisticas de jogador.

## Operacao

- A secao operacional agora inclui:
  - acoes de captura;
  - capturas;
  - reconquistas;
  - embarques;
  - desembarques;
  - fusoes.
- O objetivo e separar volume de atividade de resultado real, evitando misturar uma acao de captura com uma conquista efetiva.

## Ajustes de Jogadas

- `JogadasLog` e `JogadasManager` foram ampliados para registrar novas acoes e campos auxiliares usados pela estatistica.
- Desembarque e fusao entram como eventos reconhecidos na classificacao de jogadas.
- Servicos de comando/suprimento passam a contribuir com metadados de custo e restauracao.

## Ajustes de AI relacionados

- O embarque de capturadores foi refinado para reduzir caronas ruins quando a unidade ja esta perto do objetivo por custo de terreno.
- Transportadores ociosos mantem comportamento de pressao/estacionamento fora de produtora, evitando bloquear compra quando nao ha passageiro.
- A janela de Shopping Pressure recebeu pequenos ajustes de apresentacao relacionados ao novo ecossistema de estatisticas e diagnostico.

## Validacao

- Alteracoes preparadas para validacao em partida limpa.
- Saves antigos sem historico completo continuam carregando, mas o painel informa quando nao ha log suficiente para reconstruir compras, kills e dano.
