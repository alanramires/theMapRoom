# v4.4.3 — Transporte e Logística: Reach Controller Implementado

## Objetivo

Implementar o coordenador compartilhado de alcance decisório da IA e migrar o
transporte de passageiros como primeiro consumidor completo.

O serviço separa alcance tático, operacional e estratégico sem assumir a
semântica de combate, transporte, logística, captura ou reparo.

## AIActionReachCoordinator

- Criado o serviço puro `AIActionReachCoordinator`.
- O consumidor fornece os avaliadores específicos de cada nível.
- O coordenador executa somente os níveis habilitados pela política.
- O resultado tipado informa:
  - nível que encontrou a solução;
  - célula de ação;
  - célula alvo;
  - score;
  - motivo;
  - payload específico do consumidor.
- Tentativas e resultados usam logs padronizados `[AI Reach][Contexto]`.
- O serviço não move unidades nem altera ocupação, FOW, recursos, revisões ou
  memória da IA.

## Níveis de alcance

### Reach tático

- Equivale à hotzone ou ao serviço de alcance da rodada atual.
- Consulta caminhos válidos e sensores reais do consumidor.
- No transporte, executa `MelhorDesembarque` nas LZs alcançáveis agora.

### Reach operacional

- Equivale à progressão ou reach de progressão.
- O horizonte padrão é configurável e atualmente usa duas rodadas.
- No transporte, procura uma LZ válida no envelope operacional e usa a
  ferramenta de progressão para executar apenas o primeiro movimento.

### Reach estratégico

- Escolhe uma âncora distante por distância cúbica de hex.
- A distância estratégica apenas seleciona a direção ou o objetivo.
- Caminhos válidos, sensores, ocupação, terreno e estruturas continuam sendo
  os validadores da ação.

## Distância cúbica

- Implementada `AIActionReachCoordinator.CubicDistance`.
- A rotina converte as coordenadas offset `even-r` do Tilemap para coordenadas
  cúbicas.
- A distância é calculada por `max(|dx|, |dy|, |dz|)`.
- Seletores estratégicos de desembarque rebelde e rogue passaram a usar
  explicitamente essa métrica.

## Transporte de passageiros

O transporte tornou-se o primeiro consumidor dos três níveis.

Para carga com plano:

1. o reach tático procura uma LZ para o objetivo do passageiro prioritário;
2. o reach operacional procura uma LZ para o mesmo objetivo em duas rodadas;
3. o reach estratégico usa o objetivo do primeiro passageiro pela fila FIFO.

Para carga rogue ou de facção rebelde:

1. o reach tático combina LZs próximas e construções capturáveis;
2. o reach operacional amplia a consulta para duas rodadas;
3. o reach estratégico escolhe o capturável mais próximo por distância cúbica.

O passageiro prioritário continua sendo determinado por `embarkedOnTurn`, com
a vaga física como desempate.

## Progressão naval

- O objetivo estratégico não é usado como validação direta de movimento.
- Navios traduzem a construção alvo para uma praia ou célula naval válida.
- Depois da escolha da âncora, `Caminhos Válidos > Progressão` determina o
  movimento real.
- Esse fluxo permite contornar penínsulas, costas e outros obstáculos em vez de
  avançar diretamente contra uma célula terrestre.
- A busca global de LZs distantes permanece substituída por seleção orientada ao
  objetivo.

## Políticas disponíveis

- `TacticalOnly`: somente hotzone.
- `FieldLogistics`: tático e estratégico, pulando o operacional.
- `Transport`: tático, operacional e estratégico.
- `PlannedTransport`: tático, operacional e estratégico, preservando o destino
  do plano.

A política de logística está preparada no serviço. A migração dos
controladores logísticos será realizada separadamente para preservar os
comportamentos já calibrados.

## Vocabulário aceito

O próprio serviço registra os nomes equivalentes usados no projeto:

- tático, hotzone, serviço de alcance ou reach tático;
- operacional, progressão, reach de progressão ou reach operacional;
- estratégico, router estratégico, seletor distante ou reach estratégico.

Essas variações de nomenclatura não devem gerar implementações paralelas.

## Arquitetura transacional

- Todos os avaliadores são consultas de planejamento.
- Nenhuma etapa compromete movimento, desembarque, captura ou recursos.
- A ação definitiva continua sendo executada somente pelo batch confirmado.
- O retorno a `CursorState.Neutral` permanece a fronteira para atualização do
  estado confirmado.

## Verificação

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- Resultado: build concluído com 0 erros.
