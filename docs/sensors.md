# Sensores do jogo (estado atual)

Documento consolidado dos sensores usados no runtime.

## Como o sistema esta organizado

- Orquestracao de scanner tatico (apos movimento):
  - `Assets/Scripts/Match/TurnState/TurnStateManager.Sensors.cs`
  - `Assets/Scripts/Sensors/SensorHandle.cs`
- Modo de movimento para sensores:
  - `Assets/Scripts/Sensors/SensorMovementMode.cs` (`MoveuParado`, `MoveuAndando`)

No scanner, os sensores retornam opcoes validas + invalidas (com motivo), e o `TurnStateManager` decide quais acoes (`A/E/D/C/F/S/T`) ficam disponiveis.

## Sensores "Pode X" (acao do jogador)

1. `PodeMirarSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeMirarSensor.cs`
- Funcao principal: `CollectTargets(...)`
- Decide alvos de ataque validos e invalidos (LoS / LdT, alcance, municao, layer, spotter / stealth etc).
- Payloads: `PodeMirarTargetOption`, `PodeMirarInvalidOption`.

2. `PodeEmbarcarSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeEmbarcarSensor.cs`
- Funcao principal: `CollectOptions(...)`
- Decide embarque em transportadores adjacentes (slot, capacidade, contexto, custo, movimento restante).
- Payloads: `PodeEmbarcarOption`, `PodeEmbarcarInvalidOption`.

3. `PodeDesembarcarSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeDesembarcarSensor.cs`
- Funcoes principais: `CollectOptions(...)`, `CollectReport(...)`
- Decide desembarque de passageiros e celulas de desembarque validas.
- Payloads: `PodeDesembarcarOption`, `PodeDesembarcarInvalidOption`, `PodeDesembarcarReport`.

4. `PodeCapturarSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeCapturarSensor.cs`
- Funcao principal: `TryGetCaptureTarget(...)`
- Decide captura / recuperacao em construcao sob a unidade.
- Tambem distingue operacao: `CaptureEnemy` vs `RecoverAlly`.

5. `PodeFundirSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeFundirSensor.cs`
- Funcao principal: `CollectOptions(...)`
- Decide candidatos de fusao (mesmo tipo / time, movimento, contexto).
- Payloads: `PodeFundirOption`, `PodeFundirInvalidOption`.

6. `PodeSuprirSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeSuprirSensor.cs`
- Funcao principal: `CollectOptions(...)`
- Decide candidatos de suprimento (servicos, estoque, alcance, dominio / camada, regras de supplier).
- Payloads: `PodeSuprirOption`, `PodeSuprirInvalidOption`.

7. `PodeTransferirSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeTransferirSensor.cs`
- Funcao principal: `CollectOptions(...)`
- Decide opcoes de transferencia logistica (hub / receiver, construcao / unidade, flow mode).
- Payloads: `PodeTransferirOption`, `PodeTransferirInvalidOption`.

## Sensores automaticos / auxiliares (nao necessariamente atalho direto)

8. `PodeDecolarSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeDecolarSensor.cs`
- Funcao principal: `Evaluate(...)`
- Avalia se aeronave em solo pode decolar no contexto atual e quais modos de decolagem sao permitidos (0 / 1 / full).
- Payload: `PodeDecolarReport`.

9. `PodePousarSensor`
- Arquivo: `Assets/Scripts/Sensors/PodePousarSensor.cs`
- Funcao principal: `Evaluate(...)`
- Avalia se aeronave pode pousar no contexto atual.
- Payload: `PodePousarReport`.

10. `PodeDetectarSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeDetectarSensor.cs`
- Funcoes usadas no jogo: `CollectDetection(...)`, `CollectVisibleCells(...)`, `IsTargetObservedByTeam(...)`.
- E a base de deteccao / visibilidade (stealth, observacao por time, LoS de sensores, cache de alcance visivel).

## Sensores de FOW (runtime)

11. `PodeEnxergar` (fluxo runtime de FOW)
- Implementacao principal: `Assets/Scripts/Match/MatchController.cs`
- Base de calculo: `PodeDetectarSensor.CollectVisibleCells(...)`
- Responsabilidade: liberar / atualizar tiles visiveis no Fog of War por unidade / time.
- Nao existe como classe `Sensor` dedicada hoje; e um fluxo runtime no `MatchController` apoiado por `PodeDetectarSensor`.

12. `AlguemMeVe` (fluxo runtime para stealth / olhinho)
- Implementacao principal: `Assets/Scripts/Match/MatchController.cs`
- Base de calculo: `PodeDetectarSensor.CollectDetection(...)` e `PodeDetectarSensor.IsTargetObservedByTeam(...)`.
- Responsabilidade: decidir se uma unidade stealth esta observada por inimigos e atualizar o indicador visual de detectado (olhinho).
- Nao existe como classe `Sensor` dedicada hoje; e uma rotina runtime no `MatchController`.

## Sensor que nao comeca com "Pode"

13. `ServicoDoComandoSensor`
- Arquivo: `Assets/Scripts/Sensors/ServicoDoComandoSensor.cs`
- Funcao principal: `CollectOptions(...)`
- Decide ordens elegiveis do comando para prestacao de servicos (com fonte em construcao / unidade), incluindo invalidos.
- Payloads: `ServicoDoComandoOption`, `ServicoDoComandoInvalidOption`.

## Orquestradores e consumidores

- `SensorHandle.RunAll(...)`
  - Dispara principalmente `PodeMirar`, `PodeEmbarcar`, `PodeDesembarcar` no ciclo base do scanner.
- `TurnStateManager.Sensors.RefreshSensorsForCurrentState()`
  - Complementa com `PodeCapturar`, `PodeFundir`, `PodeSuprir`, `PodeTransferir`, alem de restricoes contextuais.
- `MatchController`
  - Usa `PodeDetectarSensor` para `PodeDetectar`, `PodeEnxergar` (FOW) e `AlguemMeVe` (stealth / olhinho) no runtime de equipes.
- Fluxos especificos (captura / combate / supply / transfer etc)
  - Consomem as listas / razoes geradas por cada sensor.

## Mapa rapido de atalhos (scanner)

- `A`: mirar / combate (`PodeMirarSensor`)
- `E`: embarcar (`PodeEmbarcarSensor`)
- `D`: desembarcar (`PodeDesembarcarSensor`)
- `C`: capturar (`PodeCapturarSensor`)
- `F`: fundir (`PodeFundirSensor`)
- `S`: suprir (`PodeSuprirSensor`)
- `T`: transferir (`PodeTransferirSensor`)

## Contrato de listeners do replay (runtime)

Este contrato define o que o replay precisa ouvir para nao avancar batch cedo demais.

- Listener 1: `CursorController.OnCursorReturnedToNeutral`
  - Sinal de fim de batch.
  - `Play` usa esse evento para disparar o proximo batch.
- Listener 2: `TurnStateManager.OnSensorsReady`
  - Sinal interno de "movimento terminou e scanner carregou opcoes".
  - Relevante dentro do batch de `UnitAction` apos confirmar destino.

### Regra por tipo de batch

- `UnitAction` com movimento + sensor (`A/E/D/C/F/S/T/Land`)
  - fluxo: confirmar destino -> esperar `OnSensorsReady` -> executar sensor/substeps -> confirmar -> esperar `OnCursorReturnedToNeutral`.
- `UnitAction` `SensorAction == None` (move-only)
  - fluxo: confirmar destino -> esperar `OnSensorsReady` (ou fallback para `Neutral`) -> finalizar sem acao -> esperar `OnCursorReturnedToNeutral`.
- `Shopping`
  - nao usa `OnSensorsReady`.
  - aguarda apenas retorno para `Neutral` ao final do fluxo de compra.
- `CommandService` / `RemoveUnit`
  - nao usam `OnSensorsReady`.
  - aguardam apenas retorno para `Neutral` ao final da execucao.

### Observacoes de escopo

- `OnSensorsReady` so importa para replay/automacao dentro do batch; nao muda o fluxo normal do jogador.
- Se o fluxo cair direto em `Neutral` sem abrir scanner (casos especiais), o replay deve tratar `Neutral` como condicao valida para seguir.

## Observacao importante

- `PodeDecolarSensor` e `PodePousarSensor` sao sensores de decisao de contexto importantes, mas nao entram no mesmo contrato de atalhos do `SensorHandle.RunAll(...)`.
- Mesmo assim, participam diretamente da tomada de decisao automatica do fluxo (camada / dominio / aviacao).

## Fluxos de camada relacionados a sensores (nao-FSM dedicado)

Este bloco documenta comportamentos que parecem "estado", mas hoje sao regras de camada aplicadas por fluxo.

1. `Force to emerge` (submarino)
- Nao existe `CursorState` dedicado tipo `Emergindo`.
- O comportamento existe como regra de camada no fim do movimento e por evento.
- Pontos principais:
  - `TurnStateManager.Movement.TryApplyForcedEndMovementLayerBeforeSensors(...)`
  - `TurnStateManager.Movement.TryResolveForcedEndMovementTargetForCell(...)`
  - `TurnStateManager.Movement.TryResolveForcedEmergeLayerTarget(...)`
  - `TurnStateManager.ScannerPrompt.TryApplyForcedEmergeAfterHitFromWeapon(...)`
- Regra pratica: submarino / submerso pode ser forçado para camada de superficie (`Naval/Surface`) conforme contexto do hex / evento.

2. Preferencia de camada ao fim do movimento
- Quando nao ha forca externa no hex, o fluxo tenta aplicar camada naval preferida da unidade.
- Pontos principais:
  - `TurnStateManager.Movement.TryApplyPreferredNavalLayerAfterMovement(...)`
  - `UnitManager.TryGetPreferredNavalLayerMode(...)`
- Prioridade atual: `forcado por contexto / evento` > `preferencia de camada`.

3. Diferenca para `PodeDecolar` / `PodePousar`
- `PodeDecolarSensor` e `PodePousarSensor` continuam sendo fluxo aereo.
- O "emergir" de submarino nao passa por esses sensores; ele usa as regras de layer force acima.

4. Configuracao de forca por dados de mapa
- O forcar camada no fim do movimento e dirigido por dados:
  - `TerrainTypeData.forceEndMovementOnTerrainDomainForDomains`
  - `StructureData.forceEndMovementOnTerrainDomainForDomains`
  - `ConstructionData.forceEndMovementOnTerrainDomainForDomains`

## Nota sobre pouso e decolagem

- `PodeDecolarSensor` e consultado no preparo da aeronave para selecao ou movimento, para definir se a decolagem e possivel e qual modo de saida pode ser aplicado.
- `PodePousarSensor` entra nos fluxos de transicao de camada e nos servicos que precisam nivelar dominio / altura antes de executar a acao.
- `TurnStateManager.CommandService` e `TurnStateManager.SupplyQueue` podem forcar `land`, `takeoff` ou `surface` como parte da propria execucao da ordem quando o conteudo da ordem pede isso.
- Essas transicoes sao parte da acao executada; nao sao um replay step separado.
