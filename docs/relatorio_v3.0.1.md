# v3.0.1 - Ajustes da AI

Esta versao consolida os ajustes finos feitos apos a primeira bateria de testes da AI Versao 1 Exercito. O foco foi reduzir decisoes "8 ou 80", manter a pressao coordenada no mapa e corrigir casos em que unidades de suporte, transporte e assalto tomavam decisoes tecnicamente validas, mas ruins para o contexto tatico.

## AI tática

- Unificacao progressiva das decisoes de deslocamento com a ferramenta de progressao, reduzindo movimentos locais baseados em heuristicas isoladas.
- Ajustes para capturadores, assault/rogue e fire support cederem construcoes reservadas para captura quando outra unidade do plano ja pode ocupar o objetivo.
- Melhor tratamento de fim de jogo: unidades proximas ao HQ inimigo passam a evitar voltar para rally point quando o resto da forca ja esta invadindo.
- Ajustes de rendezvous e go green para liberar agressividade quando ha massa suficiente proxima do objetivo.
- Transporte rogue passou a avaliar oportunidades locais para desembarque, especialmente em setores neutros ou disputados proximos.
- Tow/courier recebeu refinamentos para apoiar melhor artilharia rebocada, evitando ficar parado quando pode liberar carga para contribuir com a linha de frente.

## Logística e manutenção

- Restock Decision para unidades supridoras: retorno para base/construcao aliada segura quando recursos internos ficam baixos.
- Supridores agora consideram movimento para atender melhor o conjunto de alvos no mesmo turno, respeitando o limite de atendimento.
- Priorizacao de manutencao critica foi refinada por HP faltante, valor militar, elite, papel tatico e falta de municao.
- Artilharia e fogo indireto sem municao ganharam peso maior na fila de suprimento, evitando ficarem parados por varias rodadas.
- Infantaria fundivel em estado critico perde prioridade relativa quando ha candidato de fusao proximo, evitando gastar suprimento em unidade que provavelmente sera absorvida.
- A demanda de compra de supridores foi recalibrada para evitar excesso no tabuleiro, mas ainda buscar o segundo caminhao quando a pressao logistica passa do aceitavel.

## Compras e composição

- Ajuste de prioridade para compra de logistica quando existe deficit real de atendimento.
- Bazooka e unidades defensivas foram melhor contextualizadas para cenarios de base sob ameaca.
- A composicao agora responde melhor a defesa, stalemate, falta de suporte e pressao de inimigos blindados/infantaria.

## Persistência e WebGL

- O plano da AI passou a ser salvo/restaurado, evitando divergencia de decisao ao salvar e carregar a mesma partida.
- Save WebGL agora aguarda confirmacao do IndexedDB antes de mostrar sucesso.
- Load/listagem de saves em WebGL ficam bloqueados ate o IndexedDB terminar a sincronizacao inicial.
- Em WebGL, o diretorio de save foi fixado em `Application.persistentDataPath`, pois o navegador nao expoe um caminho fisico do usuario.

## Resultado esperado

A AI deve continuar dentro do universo de decisoes ja existentes do jogo, mas escolher melhor entre elas: tanks na frente, soldados capturando e pressionando, artilharia atras, suprimentos na retaguarda e transporte acelerando a presenca onde faz sentido. A versao tambem reduz casos em que o save/load altera o plano ou em que a versao WebGL afirma ter salvo antes da persistencia real no navegador.
