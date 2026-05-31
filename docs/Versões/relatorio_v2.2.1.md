# v2.2.1 - AI Air Refuel Operations

## Resumo

Marco para separar logistica terrestre de suporte aereo.

O objetivo e preparar a IA para tratar reabastecimento aereo como uma operacao propria, com o KC-130 comprado por demanda real de combustivel, e nao como consequencia generica de reparo ou tamanho do exercito.

## Contexto

- A compra de `Suprimentos` ainda vinha de `ComputeLogisticsDemand()`.
- Um unico helicoptero em estado ruim podia ativar demanda logistica.
- Depois disso, o piso por tamanho do exercito podia elevar a compra para 3 supridores terrestres.
- Isso misturava problemas diferentes:
  - reparo/dano de unidade terrestre;
  - aeronave danificada voltando para aeroporto;
  - aeronave com combustivel baixo;
  - rearmamento, que o KC-130 nao deve fazer.

## Problema observado

Com frota grande em campo, a IA podia comprar varios supridores militares mesmo quando havia apenas uma unidade relevante com problema.

Esse comportamento e ruim porque:

- escala por `MyUnits.Count`, nao por necessidade real;
- usa supridor terrestre como resposta para problema aereo;
- antecipa suporte logistico demais;
- gasta caixa que deveria responder a defesa, interceptacao, captura ou transporte.

## Direcao definida

Separar as demandas:

- `Suprimentos` terrestre:
  - atende dano, municao e suporte de unidades terrestres;
  - nao deve escalar apenas pelo tamanho total do exercito;
  - deve considerar quantidade real de alvos terrestres criticos.

- Aeronave danificada:
  - deve voltar para aeroporto seguro, preferencialmente aerodromo/aeroporto;
  - HQ/base terrestre so fica como fallback.

- Aeronave com combustivel baixo:
  - deve gerar demanda de KC-130;
  - essa demanda deve ser operacional, nao logistica terrestre.

- KC-130:
  - reabastece aeronaves;
  - nao repara;
  - nao rearma;
  - nao substitui supridor terrestre.

## Proxima implementacao

Adicionar uma operacao de suporte aereo ao comando:

- `AirRefuelSupport` ou equivalente no `AIOperationManager`;
- `AINeedKind.AirTanker`;
- shopping converte `AirTanker` em compra de KC-130;
- limite inicial conservador:
  - 1 KC-130 para frota com aeronaves abaixo do threshold;
  - 2 apenas com frota aerea grande e multiplas aeronaves criticas;
  - 0 se ja existe KC-130 ativo e operacional.

## Ajuste imediato recomendado

Remover ou restringir o piso por tamanho do exercito em `ComputeLogisticsDemand()`.

O criterio deve passar a contar alvos terrestres reais, nao total bruto de unidades.

## Validacao esperada

- Uma aeronave danificada nao deve comprar 3 supridores terrestres.
- Helicoptero danificado deve procurar aeroporto seguro.
- Combustivel baixo deve aparecer como demanda de KC-130.
- KC-130 nao deve ser comprado por dano ou municao.
- Supridor terrestre deve ser comprado apenas quando houver necessidade terrestre concreta.

## Observacao

Este marco registra a transicao da logistica antiga para uma leitura operacional mais limpa: suporte terrestre, reparo aereo e reabastecimento aereo passam a ser problemas separados.
