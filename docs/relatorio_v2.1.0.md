# Relatorio de Atualizacao - v2.1.0

## AI Helicoptero Transporte

Esta versao consolida a primeira entrega do fluxo de transporte aereo da IA, com foco em helicopteros capazes de buscar, carregar e reposicionar unidades dentro do plano tatico.

## Em uma frase

A IA passa a usar helicopteros de transporte como parte do plano operacional: identifica demandas de embarque, avalia rotas aereas, escolhe pontos de pickup/dropoff e integra esse comportamento ao planejamento de captura, apoio e logistica.

## Transporte aereo

- Adicionado fluxo dedicado para transporte aereo da IA.
- Helicopteros passam a participar da selecao de transportadores quando a rota terrestre nao e a melhor opcao.
- A avaliacao de embarque/desembarque considera posicao do passageiro, destino planejado e disponibilidade de slots.
- O comportamento preserva o controle de ocupacao e evita desembarque em celulas invalidas.
- O transporte aereo foi separado em arquivo proprio para manter o fluxo terrestre mais legivel.

## Planejamento da IA

- O plano da IA passa a considerar melhor unidades que dependem de transporte para atingir objetivos.
- Capturadores e unidades de apoio podem ser encaminhados por transporte quando isso reduz bloqueio ou atraso.
- A avaliacao de objetivos foi ajustada para lidar com cenarios de ilhas, frentes separadas e deslocamento por ar.
- A compra e priorizacao logisticas foram ajustadas para reconhecer melhor demandas criadas por transporte.

## Mapas e dados

- Incluido suporte de catalogo para o mapa `Battle Map Air Force`.
- Ajustados dados de unidades aereas e unidades relacionadas ao novo fluxo de transporte.
- Atualizados assets de construcao/cenario usados pelo ambiente de teste de transporte aereo.
- A cena de desenvolvimento `Battle Map Air Force` foi adicionada como base de validacao.

## Ajustes complementares

- Sensores de embarque e desembarque foram ajustados para trabalhar com o novo fluxo.
- O `UnitSpawner` agora preenche o slot do `UnitManager` ao criar unidades, permitindo nomes no formato `<nome>_T<teamId>_U<id>`.
- O `UnitManager` atualiza o nome dinamico ao receber slot, evitando unidades spawnadas sem o sufixo de time.
- Refinamentos de setor e avaliacao de reparo/logistica acompanham o novo comportamento de transporte.

## Bloco tecnico curto

- Adicionado `AIController.Transportador.Air.cs`.
- Ajustados `AIController.Transportador.cs`, `AIController.Transportador.Courier.cs` e `AIController.Transportador.Shuttle.cs`.
- Ajustados `AIController.PlanEvaluator.cs`, `AIController.Logistics.cs`, `AIController.Logistics.Helpers.cs`, `AIController.Repair.cs` e `AIShoppingPlanner.cs`.
- Ajustados `PodeEmbarcarSensor.cs` e `PodeDesembarcarSensor.cs`.
- Ajustados `UnitSpawner.cs` e `UnitManager.cs` para sincronizacao de slot/nome.
- Atualizados assets de unidades, armas, construcoes e cena de desenvolvimento ligados ao pacote.

## Resultado

Versao preparada como pacote `AI Helicoptero Transporte`, abrindo o uso de helicopteros pela IA e deixando o spawn de unidades alinhado ao identificador com time e instancia.
