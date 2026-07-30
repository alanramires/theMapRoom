# v5.1.1-3 — Refinamento: Vigilância Aérea 3/8

## Objetivo

Especializar o Radar Móvel como unidade estacionária de Vigilância Aérea.
Ele passa a avaliar sua posição atual contra somente as células Tactical que
consegue alcançar nesta rodada e só se move quando existe ganho operacional
significativo.

## Identificação

A política específica é aplicada quando a unidade reúne:

- papel `VigilanciaAerea`;
- domínio terrestre;
- `UnitData > AI Behavior > Long Range Stationary`.

Assim, o comportamento nasce da ficha e não depende do nome ou ID do asset.
O EWACS continua usando a política móvel compartilhada.

## Consulta virtual de cobertura

`PodeDetectarSensor` recebeu uma origem virtual opcional para consultas de
células visíveis. A unidade não é movida durante a avaliação.

Foi adicionada a consulta:

```csharp
PodeDetectarSensor.CollectVisibleAirCellsAt(...)
```

Ela usa as mesmas regras da apresentação de FOW por camada:

- alcance definido no `UnitData`;
- AirLow e AirHigh separadamente;
- política de LoS da partida e da especialização;
- bloqueios geográficos, incluindo montanhas;
- configuração de altura aérea;
- cache com a célula candidata incluída na chave.

A consulta não publica FOW, contatos ou detecção e não altera a posição do
observador.

## Pontuação da posição

Cada célula Tactical válida é comparada pela cobertura efetiva:

- células AirLow visíveis;
- células AirHigh visíveis;
- capacidade de detectar stealth em cada camada;
- cobertura marginal ainda não oferecida por Radar ou EWACS aliado;
- sobreposição com Vigilância Aérea aliada;
- coesão e faixa de retaguarda;
- ameaça e segurança;
- custo do caminho;
- relação com a âncora operacional.

Uma célula na vanguarda é descartada quando a geometria de retaguarda está
disponível.

## Regra estacionária

O hex atual é sempre pontuado antes dos candidatos.

O Radar permanece parado quando:

- nenhum candidato é melhor;
- o ganho não supera a margem estacionária;
- o destino é ocupado segundo `UnitOccupancyRules`;
- o destino está reservado a um capturador;
- o destino viola segurança ou retaguarda.

A margem exige ganho absoluto e proporcional. Uma posição muito obstruída usa
uma margem menor para permitir a saída; uma posição funcional exige melhora
substancial para evitar oscilação entre hexes equivalentes.

Os logs distinguem:

```text
radar stationary hold
radar stationary move
```

e apresentam cobertura atual, cobertura candidata, ganho exigido e custo do
caminho.

## Autoridades preservadas

- `PodeDetectarSensor`: alcance, LoS e especializações de visão.
- `UnitMovementPathRules`: células Tactical alcançáveis.
- `UnitOccupancyRules`: ocupação e coexistência por camada.
- `CanAIUnitEndMoveAtCell`: legalidade final do destino.
- `BuildMoveBatch`: materialização do movimento.

## Contrato transacional

- A posição do Radar não é alterada para simular candidatos.
- Nenhuma célula é revelada durante a decisão.
- A avaliação não publica contatos nem memória da IA.
- O movimento continua provisório até a confirmação normal do batch.
- O FOW definitivo somente observa a posição comprometida.

## Validação

- Compilação de runtime e editor concluída sem erros.
- Apenas células presentes nos caminhos Tactical são consideradas.
- Radar e EWACS aliados contribuem para o cálculo de sobreposição.
- Aeronaves em outra camada não bloqueiam indevidamente o destino terrestre.
- A consulta virtual usa a célula candidata em sua chave de cache.

## Próxima etapa

A Parte 4 integrará o Radar Móvel à política de transporte terrestre:

- consultar `QueroCarona`;
- exigir ganho operacional de cobertura;
- selecionar LZ materializável;
- respeitar `PodeEmbarcar`, slots e ficha dos transportadores;
- coordenar passageiro e transportador sem abandonar uma posição excelente por
  ganho pequeno.
