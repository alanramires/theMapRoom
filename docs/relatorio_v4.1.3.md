# v4.1.3 - Ajustes na AI e FOW parcial

Esta versao melhora a preparacao e a execucao da invasao final pela AI e corrige detalhes de apresentacao das acoes e da memoria visual sob Fog of War.

## Planejamento e invasao da AI

- O estado Go Green passa a absorver unidades sem plano no objetivo de invasao, evitando unidades rogue durante o ataque final.
- As atribuicoes de contingencia da invasao sao identificadas, persistidas no save e liberadas quando o plano volta a ser reorganizado.
- O eixo final de invasao passa a ser representado separadamente dos tres eixos regulares e aparece corretamente no HUD das unidades.
- O recálculo de plano ocorre antes da pausa de depuracao, mantendo HUD e Shopping Pressure sincronizados mesmo durante inspecao por etapas.
- A prontidao do rally passa a ponderar melhor unidades de assalto e identifica capturadores pela habilidade efetiva de captura.
- Forcas de ruptura esmagadoras podem iniciar a invasao sem aguardar indefinidamente uma cota de artilharia indisponivel.

## Logistica, reparo e compras

- A AI recebeu ajustes de pressao operacional para compras e de distribuicao de suprimentos durante a preparacao da ofensiva.
- Unidades de logistica e reparo refinam a escolha de apoio, movimento e atendimento das forcas em operacao.
- Transportadores envolvidos na invasao reconhecem melhor suas tarefas de courier e integracao com o eixo ofensivo.
- A janela de Shopping Pressure foi atualizada para expor os novos sinais usados pelo planejamento.

## Fog of War e apresentacao de acoes

- Segmentos da fotografia de terreno conhecido sao recortados pela fronteira realmente oculta, sem cobrir um hex vizinho que esteja visivel no momento.
- Em partidas AI vs AI com FOW total, projeteis de servico, suprimento e transferencia podem ser apresentados acima da nevoa durante a acao ativa.
- A animacao de embarque aplica e restaura a elevacao visual temporaria de passageiro e transportador conforme as regras de apresentacao do FOW.
- Os efeitos temporarios sao removidos ao final da sequencia e nao alteram a informacao confirmada do tabuleiro.

## Interface e dados

- A barra de controle territorial evita uma faixa neutra falsa durante a captura parcial de construcoes inimigas em mapas sem objetivos neutros.
- O save da AI preserva a marcacao das atribuicoes fallback criadas durante o Go Green.
- Cena de batalha, fontes e materiais associados receberam atualizacoes de configuracao.

## Contrato transacional

- As mudancas visuais durante embarque, servico e transferencia permanecem temporarias e restauraveis.
- FOW, memoria, ocupacao e demais estados confirmados continuam sendo recalculados somente depois do compromisso da acao e do retorno a `CursorState.Neutral`.

## Validacao

- Assembly principal compilado com sucesso, sem erros.
- Alteracoes verificadas com `git diff --check`.
