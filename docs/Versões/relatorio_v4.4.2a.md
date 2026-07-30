# v4.4.2a — Transporte e Logística: Reach Controller

## Objetivo

Introduzir no transporte o fluxo de alcance decisório que servirá de base para
um coordenador compartilhado entre os controladores da IA:

1. decisão imediata no envelope da rodada;
2. progressão validada no envelope de duas rodadas;
3. seleção de uma âncora estratégica distante.

Esta versão aplica o padrão ao desembarque de cargas livres e registra a
direção arquitetural para sua centralização posterior.

## Fluxo de alcance do transporte

- O primeiro nível continua usando os caminhos válidos da rodada atual e o
  `PodeDesembarcarSensor`.
- Cargas rogue ou de facção rebelde que não encontram uma LZ imediata consultam
  uma progressão limitada a duas rodadas.
- O segundo nível funciona tanto com um passageiro quanto com grupos que exigem
  desembarque conjunto.
- Se nenhuma LZ adequada existir no envelope de duas rodadas, o controlador
  deixa de executar a antiga busca global de `120 MP` e passa ao seletor de
  âncora do domínio.
- Passageiros com plano não usam o fluxo livre: preservam o objetivo estratégico
  e a busca orientada pelo planejador.

## Âncora costeira naval

- A seleção de praia passou a nascer no objetivo capturável.
- A busca expande em bolhas a partir da construção até encontrar a primeira
  faixa de células navais que permita embarque ou desembarque.
- Dentro da primeira faixa válida, vence a célula com menor custo de rota para
  o transportador.
- Ocupação, visibilidade confirmada, capacidade de encerrar movimento e regras
  reais dos sensores continuam sendo validadas.
- O algoritmo deixa de executar `PodeDesembarcar` indiscriminadamente em todas
  as praias alcançáveis de mapas continentais ou oceânicos.
- Um limite defensivo impede expansão ilimitada quando a geografia não oferece
  uma costa compatível.

## Política descoberta

O padrão consolidado é:

- `Hotzone`: decisões possíveis na rodada atual;
- `Progression`: decisões alcançáveis em duas rodadas;
- `Strategic`: escolha da âncora mais próxima para orientar a progressão.

A distância estratégica apenas ordena ou escolhe alvos. Ela não substitui
caminhos válidos, sensores, ocupação, terreno ou estruturas permitidas.

Controladores poderão habilitar somente os níveis apropriados:

- logística de campo: `Hotzone + Strategic`;
- transporte: `Hotzone + Progression + Strategic`;
- fusão imediata: `Hotzone`;
- captura e outros papéis móveis: política definida durante a migração.

## Próximo passo arquitetural

Extrair a coordenação dos níveis para um serviço puro, provisoriamente chamado
`AIActionReachCoordinator`, mantendo cada controlador responsável apenas pela
semântica de seus candidatos e sensores.

A migração deverá ocorrer consumidor por consumidor para preservar os
comportamentos já calibrados.

## Arquitetura transacional

- As três etapas são consultas de planejamento e não comprometem ações.
- O cálculo não altera posição, ocupação, FOW, detecção, recursos, revisões ou
  memória da IA.
- Movimento e desembarque permanecem definitivos somente no commit explícito do
  batch e no retorno a `CursorState.Neutral`.

## Verificação

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- Resultado: build concluído com 0 erros.
