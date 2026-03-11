# Inferencia de jogo (por estrutura e interacao)

## Premissa
Analise feita inferindo mecanicas pelo formato do sistema (estados, fluxos, regras, entidades e dependencia entre modulos), sem depender de semantica textual de nomes/comentarios.

## O que o sistema parece ser
O codigo indica um **jogo tatico em grade hexagonal, por turnos, com unidades de equipes diferentes**, no qual cada unidade executa um ciclo de acao com validacoes contextuais antes de confirmar efeitos.

## Mecanicas inferidas pela arquitetura

### 1) Loop de turno por equipes
Evidencias estruturais:
- controlador global com time ativo, rodada atual, transicao de turno e aplicacao de efeitos no inicio do turno;
- economia por equipe e renda recorrente;
- reset/reativacao de unidades por lado no momento de troca.

Inferencia:
- jogo alterna entre equipes em ordem fixa; cada equipe joga seu "bloco de turno".

### 2) Unidade como agente tatico principal
Evidencias estruturais:
- entidade runtime com HP, municao, combustivel/autonomia, movimento restante, equipe, posicao em celula;
- marcador de “ja agiu” e estados de selecionada/embarcada;
- mudanca de camada operacional (dominio/altura) durante jogada.

Inferencia:
- cada peça no tabuleiro possui recursos operacionais limitados e geralmente realiza uma acao por turno.

### 3) Grade hex + pathfinding com custo
Evidencias estruturais:
- consultas de vizinhanca hexagonal e mapa de distancias;
- busca por fronteira com custo acumulado e reconstrução de caminho;
- custo de deslocamento condicionado por contexto da celula.

Inferencia:
- movimentacao usa alcance/custo em hexes; terreno/contexto altera custo e acessibilidade.

### 4) Multi-camadas operacionais no mesmo mapa
Evidencias estruturais:
- par de estado “dominio + altura” em regras de passagem, combate, visao e operacao;
- checagem de compatibilidade de camada para interacoes;
- regras de transicao contextual de camada (inclusive rollback).

Inferencia:
- o jogo simula diferentes planos de operacao (superficie, submerso, alturas de voo etc.) convivendo na mesma malha.

### 5) Combate validado por sensores
Evidencias estruturais:
- pipeline de coleta de alvos validos/invalidos;
- verificacoes de alcance, disponibilidade de recurso, linha de tiro/visada e bloqueios no trajeto;
- escolha de arma/contexto e composicao de modificadores.

Inferencia:
- atacar nao e clique direto: o sistema calcula elegibilidade tatico-espacial antes de permitir o disparo.

### 6) Visibilidade e informacao imperfeita
Evidencias estruturais:
- estado de deteccao com janela temporal;
- validacao dependente de observadores e de contexto de linha de visada;
- overlays de ameaca e revisao de cache por alteracao de tabuleiro.

Inferencia:
- existe nevoa parcial de informacao: nem todo alvo elegivel e automaticamente “visivel” o tempo todo.

### 7) Logistica como mecanica central
Evidencias estruturais:
- recursos transferiveis, prestacao de servico, limite de atendimentos, alcance de fornecedor e fila de ordens;
- formulas separadas para estimar ganho e custo monetario;
- opcoes de abastecimento para unidades no campo e tambem em relacao de transporte.

Inferencia:
- sustain (reparo/rearme/reabastecimento/transferencia) e parte do meta-jogo tatico, nao apenas detalhe.

### 8) Transporte e composicao de unidades
Evidencias estruturais:
- slots de assento/embarque com runtime dedicado;
- regras de elegibilidade de embarque/desembarque por contexto;
- unidade podendo ficar oculta/embarcada em outra.

Inferencia:
- ha mecanica de transporte de tropas/carga, com restricoes de onde entrar/sair e ordem operacional.

### 9) Controle de territorio/objetivos
Evidencias estruturais:
- entidades fixas no mapa com dono, pontos de captura e efeitos economicos;
- alteracao de propriedade impactando renda e estado de jogo.

Inferencia:
- ocupacao/captura de pontos estrategicos influencia vantagem economica e fluxo da partida.

### 10) Interface como painel de decisao
Evidencias estruturais:
- estrutura de dados de helper com linhas de preview de custo/ganho/opcoes;
- confirmacao/cancelamento e mensagens de motivo de invalidez por acao;
- overlays de alcance/ameaca em paralelo ao cursor.

Inferencia:
- UX foi desenhada para suportar jogo de decisao analitica, com feedback explicavel antes do commit.

## Conclusao sintetica
Pela forma do codigo, o jogo inferido e um **wargame tatico por turnos em hex**, com:
- movimentacao com custo;
- combate contextual por linha/visibilidade;
- camadas operacionais (superficie/subsuperficie/aereo);
- logistica e economia ativas;
- captura de pontos;
- transporte/embarque;
- maquina de estados para selecionar, validar, confirmar e executar acoes.

Ou seja, a estrutura aponta para simulacao tatica com forte enfase em **restricao operacional e planejamento de turno**, e nao para arcade de acao direta.
