# v3.0.3 - Segundo Mapa Aereo

Esta versao marca a abertura do segundo mapa aereo e consolida os ajustes de suporte para operar melhor com cenario dividido entre mapa terrestre e mapa aereo. O foco principal foi preparar dados, cenas, catalogos e sistemas de apoio para que a camada aerea tenha comportamento mais consistente em compra, movimento, coabitacao, suprimento, IA e persistencia.

## Mapas e catalogos

- Adicionado o mapa `Battle Map 2 - Air` como segundo mapa aereo jogavel.
- Separado o antigo catalogo generico de estruturas em catalogos especificos para `Battle Map 1 - Ground` e `Battle Map 2 - Air`.
- Atualizados dados de mapa, build settings e cenas para reconhecer a nova divisao entre mapa terrestre e mapa aereo.
- Mantida a compatibilidade com o mapa `AI Ground` durante a transicao de catalogos.

## Operacoes aereas

- Refinada a logica de aeronaves em solo e em voo usando o estado de camada (`Domain`/`HeightLevel`) como fonte de verdade.
- Ajustado o comportamento de helicopteros comprados para preservar a intencao de nascerem em solo quando a unidade suporta `Land/Surface`.
- Melhorada a coabitacao visual entre unidades em bandas diferentes do mesmo hex.
- Revisitadas regras de ocupacao para reduzir conflitos entre unidades terrestres, aeronaves pousadas e aeronaves em voo.

## Save e load

- O save de unidades agora registra o estado operacional aereo: `isAircraftGrounded` e `aircraftOperationLockTurns`.
- O load restaura o estado de aeronaves depois de aplicar a camada salva, evitando que helicopteros voltem em voo quando deveriam estar pousados.
- Saves antigos continuam compatíveis: aeronave salva fora de `Domain.Air` e tratada como grounded no carregamento.
- Mantida a persistencia de flags de turno, suprimentos embarcados, municao, combustivel, transporte e badges de plano da IA.

## IA e logistica

- Ajustes na IA para operar melhor com unidades aereas, transporte aereo, reposicionamento e suprimento.
- Refinos no planejamento de compras para considerar demanda por helicopteros, transporte aereo, interceptadores e apoio logistico.
- Melhorias em decisoes de courier, desembarque e oportunidade local para transportadores aereos.
- Ajustes em reparo e suprimento para respeitar melhor o estado grounded/airborne das aeronaves.

## Sensores e comandos

- Revisados sensores de decolagem, suprimento e servico do comando para lidar melhor com camadas e operacoes aereas.
- Ajustado o fluxo de scanner e state machine para manter coerencia entre selecao, preview de decolagem, execucao e rollback.
- Melhorados caminhos de movimentacao e regras de transicao para reduzir estados temporarios presos apos acoes ou cancelamentos.

## Editor e debug

- Atualizadas janelas de debug relacionadas a suprimento, setores e unidades.
- Reforcados indicadores editoriais para estado de aeronave, lock de operacao e coabitacao.
- Logs de `SectorManager`, AI e save/load continuam servindo como base para diagnosticar rebuilds, commits de mundo e persistencia.

## Resultado

O projeto passa a ter uma base mais clara para evoluir mapas aereos separados, com catalogos dedicados e persistencia mais segura de aeronaves. O principal ganho pratico e que helicopteros e outras aeronaves agora preservam melhor sua condicao entre compra, turno, save e load, reduzindo discrepancias entre o que o jogador viu antes de salvar e o que aparece ao carregar.
