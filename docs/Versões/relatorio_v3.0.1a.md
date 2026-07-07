# v3.0.1a - AI Lagging

Patch focado em unidades que travavam ou recuavam quando deveriam avançar: artilharia parada esperando reboque que nunca chegava, tow shuttle se movendo para tile errado, fire support escolhendo celula de regressao e tanque caçando alvo de menor valor por ignorar elite level.

## Fire Support

- **Rendezvous consulta ferramenta de progressao primeiro**: `TryFireSupportRendezvousAction` agora chama `TryFindBestToolProgressionCell` antes do fallback de pressao. Evita que fire support escolha rota sem consultar o custo real de terreno.
- **Fallback sem retrocesso**: se a ferramenta de progressao nao encontrar candidato com progresso positivo e o fallback (`FindAssaultPressureMove`) retornar uma celula igual ou mais distante do alvo de rendezvous, a unidade fica parada em vez de recuar.
- **Reposicionamento sem retrocesso**: loop de fallback em `TryFindFireSupportRepositionCell` passou a bloquear celulas de retrocesso quando nao ha ameaca imediata nem preferencia de alcance maximo.

## Tow / Logística

- **Embarque de artilharia em tow courier**: correcao de `>=` para `>` na guarda de proximidade do caminhao logistico. Um caminhao na mesma distancia do alvo de entrega que a artilharia nao e considerado em retrocesso — agora o embarque e permitido.
- **Tow shuttle adjacente nao se move**: `TryDecideTowShuttleAction` agora para em lugar quando ja esta a ≤1h da artilharia candidata, evitando mover para tile irrelevante no mesmo turno em que o embarque aconteceria.

## Capturer

- **Defesa de predio aliado respeita prioritizeDpqAtBattle**: `TryDecideCapturerOwnedBuildingDefenseBeforeEmbark` passou a usar `IsBetterAttackCandidate` (hard-sort por `targetPriority` → `attackDpq` → `score`) em vez de peso multiplicativo, alinhando com o comportamento de ataque padrao do capturador.

## Assault

- **Elite level no scoring de alvo**: `TryFindAssaultEscortAttack` e `TryFindAssaultAdvanceRouteAttack` adicionam `eliteLevel * 4000f` ao score do alvo. Um nivel de elite equivale a ~4 HP de vantagem no desempate, garantindo que artilharia de elite (Artilharia de Campanha) seja priorizada sobre artilharia comum (Obus Leve) dentro do mesmo tier de preferencia de alvo (`Primary`). O campo `elite=X` aparece no log de debug do ataque.

## Resultado esperado

Artilharia embarca no tow courier no turno em que o caminhao para ao lado. Fire support nao recua durante rendezvous. Tanques caçam artilharia de campanha antes de artilharia leve quando ambas sao alvo primario.
