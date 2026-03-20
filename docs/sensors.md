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
- Decide alvos de ataque validos e invalidos (LoS/LdT, alcance, municao, layer, spotter/stealth etc).
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
- Decide captura/recuperacao em construcao sob a unidade.
- Tambem distingue operacao: `CaptureEnemy` vs `RecoverAlly`.

5. `PodeFundirSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeFundirSensor.cs`
- Funcao principal: `CollectOptions(...)`
- Decide candidatos de fusao (mesmo tipo/time, movimento, contexto).
- Payloads: `PodeFundirOption`, `PodeFundirInvalidOption`.

6. `PodeSuprirSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeSuprirSensor.cs`
- Funcao principal: `CollectOptions(...)`
- Decide candidatos de suprimento (servicos, estoque, alcance, dominio/camada, regras de supplier).
- Payloads: `PodeSuprirOption`, `PodeSuprirInvalidOption`.

7. `PodeTransferirSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeTransferirSensor.cs`
- Funcao principal: `CollectOptions(...)`
- Decide opcoes de transferencia logistica (hub/receiver, construcao/unidade, flow mode).
- Payloads: `PodeTransferirOption`, `PodeTransferirInvalidOption`.

## Sensores automaticos / auxiliares (nao necessariamente atalho direto)

8. `PodeDecolarSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeDecolarSensor.cs`
- Funcao principal: `Evaluate(...)`
- Avalia se aeronave em solo pode decolar no contexto atual e quais modos de decolagem sao permitidos (0/1/full).
- Payload: `PodeDecolarReport`.

9. `PodePousarSensor`
- Arquivo: `Assets/Scripts/Sensors/PodePousarSensor.cs`
- Funcao principal: `Evaluate(...)`
- Avalia se aeronave pode pousar no contexto atual.
- Payload: `PodePousarReport`.

10. `PodeDetectarSensor`
- Arquivo: `Assets/Scripts/Sensors/PodeDetectarSensor.cs`
- Funcoes usadas no jogo: `CollectDetection(...)`, `CollectVisibleCells(...)`, `IsTargetObservedByTeam(...)`.
- E a base de deteccao/visibilidade (stealth, observacao por time, LoS de sensores, cache de alcance visivel).

## Sensores de FOW (runtime)

11. `PodeEnxergar` (fluxo runtime de FOW)
- Implementacao principal: `Assets/Scripts/Match/MatchController.cs`
- Base de calculo: `PodeDetectarSensor.CollectVisibleCells(...)`
- Responsabilidade: liberar/atualizar tiles visiveis no Fog of War por unidade/time.
- Nao existe como classe `Sensor` dedicada hoje; e um fluxo runtime no `MatchController` apoiado por `PodeDetectarSensor`.

12. `AlguemMeVe` (fluxo runtime para stealth/olhinho)
- Implementacao principal: `Assets/Scripts/Match/MatchController.cs`
- Base de calculo: `PodeDetectarSensor.CollectDetection(...)` e `PodeDetectarSensor.IsTargetObservedByTeam(...)`.
- Responsabilidade: decidir se uma unidade stealth esta observada por inimigos e atualizar o indicador visual de detectado (olhinho).
- Nao existe como classe `Sensor` dedicada hoje; e uma rotina runtime no `MatchController`.

## Sensor que nao comeca com "Pode"

13. `ServicoDoComandoSensor`
- Arquivo: `Assets/Scripts/Sensors/ServicoDoComandoSensor.cs`
- Funcao principal: `CollectOptions(...)`
- Decide ordens elegiveis do comando para prestacao de servicos (com fonte em construcao/unidade), incluindo invalidos.
- Payloads: `ServicoDoComandoOption`, `ServicoDoComandoInvalidOption`.

## Orquestradores e consumidores

- `SensorHandle.RunAll(...)`
  - Dispara principalmente `PodeMirar`, `PodeEmbarcar`, `PodeDesembarcar` no ciclo base do scanner.
- `TurnStateManager.Sensors.RefreshSensorsForCurrentState()`
  - Complementa com `PodeCapturar`, `PodeFundir`, `PodeSuprir`, `PodeTransferir`, alem de restricoes contextuais.
- `MatchController`
  - Usa `PodeDetectarSensor` para `PodeDetectar`, `PodeEnxergar` (FOW) e `AlguemMeVe` (stealth/olhinho) no runtime de equipes.
- Fluxos especificos (captura/combate/supply/transfer/etc)
  - Consomem as listas/razoes geradas por cada sensor.

## Mapa rapido de atalhos (scanner)

- `A`: mirar/combate (`PodeMirarSensor`)
- `E`: embarcar (`PodeEmbarcarSensor`)
- `D`: desembarcar (`PodeDesembarcarSensor`)
- `C`: capturar (`PodeCapturarSensor`)
- `F`: fundir (`PodeFundirSensor`)
- `S`: suprir (`PodeSuprirSensor`)
- `T`: transferir (`PodeTransferirSensor`)

## Observacao importante

- `PodeDecolarSensor` e `PodePousarSensor` sao sensores de decisao de contexto importantes, mas nao entram no mesmo contrato de atalhos do `SensorHandle.RunAll(...)`.
- Mesmo assim, participam diretamente da tomada de decisao automatica do fluxo (camada/dominio/aviacao).

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
- Regra pratica: submarino/submerso pode ser forçado para camada de superficie (`Naval/Surface`) conforme contexto do hex/evento.

2. Preferencia de camada ao fim do movimento
- Quando nao ha forca externa no hex, o fluxo tenta aplicar camada naval preferida da unidade.
- Pontos principais:
  - `TurnStateManager.Movement.TryApplyPreferredNavalLayerAfterMovement(...)`
  - `UnitManager.TryGetPreferredNavalLayerMode(...)`
- Prioridade atual: `forcado por contexto/evento` > `preferencia de camada`.

3. Diferenca para `PodeDecolar`/`PodePousar`
- `PodeDecolarSensor` e `PodePousarSensor` continuam sendo fluxo aereo.
- O "emergir" de submarino nao passa por esses sensores; ele usa as regras de layer force acima.

4. Configuracao de forca por dados de mapa
- O forcar camada no fim do movimento e dirigido por dados:
  - `TerrainTypeData.forceEndMovementOnTerrainDomainForDomains`
  - `StructureData.forceEndMovementOnTerrainDomainForDomains`
  - `ConstructionData.forceEndMovementOnTerrainDomainForDomains`

## Nota sobre pouso e decolagem

o pode decolar é chamado ao selecionar a unidade e antes de iniciar qualquer movimento (parado ou andando) e já a coloca no ar dependendo da regra e do retorno (0/1/full) limitando drasticamente o movimento restante da unidade que decolou, ou liberando tudo. se ele é parte integrante da rotina de selecionar unidade e, selecionar unidade, faz parte do batch e por conseguinte ao ser emulado no automated play, não faz sentido guardar isso.

o pode pousar é chamado por outros sensores como parte de nivelar a mesma altitude para a prestação de serviço, atualmente é chamado pelo supridor em pode suprir, quando quer forçar um caça a pousar ao lado do supridor para receber suprimentos (ou seja, antes do jogador escolher o sensor o sistema já consulta o pode pousar de aeronaves voando proximas, pra saber se elas tem capacidade de pousar ao lado do supridor) e quando o supridor a seleciona, a aeronave pousa como parte da animação, mudando sua camada e recebendo os recursos; tambem é chamada pelo embarque para forçar o helicoptero a pousar pr receber passageiro e pelo desembarque para liberar passageiros; é chamado tambem pelo supridor para fazer o kc-130 descer para air/low para igualar ao helicoptero em nivel de serviço (local da prestação de serviço) e pra forçar subs a emergir (na teoria, um pouso invertido) para naval/surface para receber suprimentos do navio tanker. tambem é usado por outros participantes do suprir como caças que descem para air/low para igualar a um kc-130 que está abastecendo helicopteros. O serviço do comando automaticamente força aeronaves a pousar como parte de sua rotina de manutençao. entao não sei se precisa gravar step pra isso no batch.