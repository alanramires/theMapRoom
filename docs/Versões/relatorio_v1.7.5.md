# Relatorio v1.7.5 - Transportadores - Parte 1

## Resumo

Esta versao inaugura a primeira etapa do comportamento de transportadores na IA. O foco foi colocar o ciclo basico de embarque e desembarque para capturadores terrestres, mantendo o passageiro como dono da missao e usando os sensores oficiais do jogo como fonte de verdade.

## Entregas

- novo sensor `Transport` no `AI Unit Profile`;
- primeiro fluxo de IA para parear `capturador + transportador`;
- embarque e desembarque automatizados usando `PodeEmbarcar` e `PodeDesembarcar`;
- heuristica de "vale a pena transportar?" para evitar consumir APC em objetivos curtos;
- APC vazio sem plano passa a estacionar na zona de pickup perto da base, em vez de avancar sozinho;
- ajuste visual no embarque oficial para o passageiro aparecer acima do transportador durante a entrada;
- persistencia de memoria de missao para unidades embarcadas;
- endurecimento do planner para evitar danca de unidades entre planos ativos normais;
- captura oportunista: unidade de captura pode aproveitar uma construcao inimiga no hex atual mesmo fora do alvo original do plano.

## Comportamento atual

- o `Capturador` continua sendo o dono da missao;
- o `Transportador` funciona como acelerador de mobilidade para levar esse capturador ao setor;
- o passageiro tenta embarcar primeiro pelas opcoes reais do sensor;
- o APC leva o passageiro usando o alvo do plano embarcado;
- o desembarque agora aceita celulas uteis nas proximidades do objetivo, sem exigir encaixe artificial na celula exata do plano.

## Limites desta etapa

- foco apenas em transporte terrestre de capturadores;
- sem pressao de compra de `Transport` no `Shopping Manager`;
- sem doutrina completa de pos-desembarque;
- extensoes para `Escort`, `FireSupport`, navio de transporte e helicoptero ficam para etapas seguintes.
