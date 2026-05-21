# v2.1.1 - AI Helicoptero e Passageiro

## Resumo

Point save focado no fluxo de IA para helicopteros, passageiros e captura aerotransportada.

## Principais ajustes

- Helicopteros sobem na iniciativa para se posicionarem antes dos capturadores.
- Logs de iniciativa passaram a exibir nome e ID da unidade, reduzindo ambiguidade no debug.
- Capturadores preferem transportes do mesmo plano/setor e evitam embarcar em caronas de setores distantes.
- Setores vizinhos diretos pelo `SectorManager` sao aceitos como tolerancia de carona.
- Capturadores rogue continuam oportunistas e podem usar helicopteros como carona livre.
- Transporte aereo passou a usar custo/alcance de ar, evitando comportamento de unidade terrestre voadora.
- Bombardeiros e unidades de ataque aereo receberam acao propria para avancar como aviao, com foco em alvo inimigo/HQ.
- Helicoptero formal de setor usa o alvo do setor como ancora de pickup.
- Score de pickup aereo passou a considerar distancia ao objetivo, deslocamento do helicoptero, quantidade de capturadores atendidos, construcao/producao e ameaca.
- Rogues que bloqueiam corredor de capturador formal sobem na iniciativa para liberar caminho antes do avanco.

## Compras e defesa

- Compra de capturadores voltou a respeitar demanda mesmo com filtros `onlyCapturers`.
- Defesa preventiva pode furar o gate de turno quando ha caixa alto.
- Planner aereo reserva verba para combate aereo e compra bombardeiro/apache conforme demanda.

## Debug esperado

- `pickup-score cell=... objDist=... travel=... support=... score=...`
- `BLOQUEADO setor distante: assigned=... tObj=...`
- `iniciativa: rogue ... libera corredor de ...`

## Observacao

Este save consolida a heuristica atual. O proximo teste deve validar se o heli escolhe pontos laterais de pickup sem bloquear capturadores de outros setores.
