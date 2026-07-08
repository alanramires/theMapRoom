# v4.0.27 - AI Transporter Optimization

Esta versão reduz o custo de decisão dos APCs e demais transportadores, removendo simulações repetidas de progressão de dois turnos e caches locais ineficientes durante o avanço de assalto.

## Progressão do transportador

- `FindTransportMove` deixou de mover o APC virtualmente para cada destino candidato.
- A progressão de dois turnos agora é derivada do mapa reverso de custo já calculado até o alvo.
- O cálculo passa de múltiplas buscas completas por candidato para:
  - um mapa reverso até o objetivo;
  - um mapa curto a partir da origem;
  - scoring constante por destino.
- Foram preservados no scoring:
  - progresso no primeiro e segundo turnos;
  - bônus de estrada;
  - ameaça local;
  - tráfego aliado;
  - construções não pertencentes ao time;
  - custo real de movimento como desempate.
- A segunda passagem duplicada de progressão foi removida.

## APC em Assault Pressure

- O rastreio demonstrou que os APCs mais lentos não estavam usando `TransportDelivery`, mas `AssaultPressure`.
- Esse ramo também executava uma busca completa de caminhos para cada parada candidata.
- A simulação genérica de dois turnos foi removida apenas de `AssaultPressure`.
- O seletor existente por progresso de rota, alinhamento, ameaça, DPQ e custo de caminho passou a decidir diretamente o avanço.

## Cache de ameaça

- `CalculateThreatLevel` não consulta mais a visibilidade de todos os inimigos para cada hex avaliado.
- As posições dos inimigos visíveis são coletadas uma vez por unidade/decisão.
- Cada candidato executa apenas distâncias hexagonais sobre essa lista compacta.
- O custo medido de ameaça no `AssaultPressure` caiu de aproximadamente `3,2 s` para `118-166 ms`.

## Cache de DPQ

- O DPQ de cada célula é resolvido uma vez por decisão.
- O scoring mantém os valores dos melhores candidatos em vez de consultar novamente suas células.
- Isso evita buscas repetidas de construções, estruturas, redes viárias e tilemaps.
- O custo medido de DPQ caiu para aproximadamente `11-15 ms` em 25-28 candidatos.

## Instrumentação

Novos estágios permitem distinguir o fluxo real de cada unidade:

- `toolProgression.<Intent>`;
- `transportMove`;
- `transportReverseCostMap`;
- `transportOriginCostMap`;
- `assaultPressureMove`;
- `assaultPressureThreat`;
- `assaultPressureDpq`.

## Resultado observado

- APCs que antes consumiam aproximadamente `8-12 s` de decisão caíram para cerca de `3-4,6 s` nos testes finais.
- `assaultPressureMove` caiu de aproximadamente `3,2-3,8 s` para `158-208 ms` após a remoção das buscas repetidas e o cache de ameaça.
- Consultas de rota no ramo otimizado ficaram em aproximadamente `36-45 ms` para 33-36 chamadas.
- A execução/animação continua medida separadamente e não é confundida com CPU de decisão.

## Validação

- `Assembly-CSharp.csproj`: build concluído com `0` erros.
- Testes reproduzidos no turno 8 com múltiplos APCs nos times Green e Red.
