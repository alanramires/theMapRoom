# Relatorio de Sensores

## Lista de sensores identificados
Fonte: `Assets/Scripts/Sensors/*.cs`

| Sensor | O que detecta/valida | Momento de uso | Sistema consumidor |
|---|---|---|---|
| `PodeMirarSensor` | Alvos validos/invalidos de ataque (range, ammo, layer, LDT, LoS, spotter, stealth) | Apos movimento (`MoveuAndando`/`MoveuParado`) e em `Mirando` | `SensorHandle`, `TurnStateManager` (`TryConfirmScannerAttack`) |
| `PodeEmbarcarSensor` | Opcoes de embarque em transportador + invalidos | Apos movimento / estado de embarque | `SensorHandle`, `TurnStateManager` |
| `PodeDesembarcarSensor` | Opcoes de desembarque + invalidos | Estado `Desembarcando` | `SensorHandle`, `TurnStateManager` |
| `PodeCapturarSensor` | Se unidade pode capturar construcao no hex | Apos movimento parado/andando | `TurnStateManager.Sensors` |
| `PodeFundirSensor` | Candidatos de fusao validos/invalidos | Apos movimento | `TurnStateManager.Merge` |
| `PodeSuprirSensor` | Alvos para servico logistico direto | Apos movimento | `TurnStateManager.Supply` |
| `PodeTransferirSensor` | Opcoes de transferencia de estoque/recursos | Apos movimento | `TurnStateManager.Transfer` |
| `ServicoDoComandoSensor` | Candidatos e ordens de servico em lote (origem construcao/supridor) | Fluxo de comando/logistica | `TurnStateManager.CommandService` |
| `PodePousarSensor` | Valida pouso/estado de aeronave | Fluxos de layer/air ops | `TurnStateManager` / regras de aeronave |
| `PodeDecolarSensor` | Valida decolagem/planejamento de saida de solo | Selecao/movimento de aeronave | `TurnStateManager` / regras de aeronave |
| `SensorHandle` | Orquestrador: roda sensores principais e popula codigos A/E/D | Refresh de sensores no estado de scanner | `TurnStateManager.Sensors` |

## Quando entram no turno
- Entrada principal: `RefreshSensorsForCurrentState()` em `TurnStateManager.Sensors.cs`.
- Esse refresh roda para unidade selecionada em estados de scanner (pos-movimento) e repinta acoes disponiveis.
- Codigos de acao populados: `A` mirar, `E` embarcar, `D` desembarcar, `C` capturar, `F` fundir, `S` suprir, `T` transferir, `L` layer/altitude.

## Observacoes importantes
- Nem todos os sensores passam pelo `SensorHandle`; alguns sao chamados direto no `TurnStateManager` (captura/fusao/suprir/transferir/command service).
- O sensor de combate (`PodeMirarSensor`) e o mais complexo e concentra boa parte das validacoes taticas do jogo.
