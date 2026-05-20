# v2.1.2 - AI Gestao Aerea

## Resumo

Point save focado na gestao aerea da IA, no uso de helicopteros, resposta antiaerea e compra de reforcos quando ha caixa sobrando.

## Principais ajustes

- Transporte aereo passou a avaliar melhor passageiros perto da base e demanda real por helicopteros.
- Helicopteros vazios agora alimentam a compra de capturadores conforme assentos disponiveis, evitando passar turno com Chinooks ociosos e dinheiro alto.
- Planejamento de objetivos deixou de ficar preso a um cap fixo pequeno: o limite agora escala com a quantidade de setores do mapa.
- Setores proprios ameacados por inimigos proximos abrem demanda de assalto, nao apenas de capturador defensivo.
- Apoio de fogo fora de alcance util avancara para rotas melhores em vez de ficar parado longe demais do objetivo.
- Compra antiaerea ficou mais responsiva a aeronaves inimigas visiveis, incluindo cobertura de base e limite de AAA/SAM.

## Compras e defesa

- O planner reduz o caso em que a IA compra apenas uma unidade barata e encerra com muito caixa.
- A presenca de helicopteros inimigos continua gerando demanda de Caca B.
- A presenca de helicopteros aliados vazios aumenta a demanda por soldados/capturadores para ocupar a capacidade de transporte.
- Alpha e outros setores controlados sob ataque agora podem puxar compra de combate terrestre para defesa.

## Debug esperado

- `cap objetivos escalado: config=... setores=... -> ...`
- `capturer_airlift_feed: raw=... capped=... emptyAir=... spareSeats=... airliftSeats=...`
- `advanceRoute forced ...`
- `cacaB_demand: ... enemyHelicos=...`

## Observacao

Este save deve ser validado em cenarios com muito caixa, multiplos aerodromos e helicopteros vazios. O resultado esperado e a IA gastar mais agressivamente em capturadores/defesa quando houver capacidade aerea parada e setor proprio sob ataque.
