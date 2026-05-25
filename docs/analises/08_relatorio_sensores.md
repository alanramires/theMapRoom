# Relatorio de Sensores

Data base: 2026-05-25 (revisado; base original: 2026-03-06)

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
| `PodeEmergirSensor` | Valida emersao de submarino (Submarine/Submerged → Naval/Surface) no hex atual | Fluxo 'L' de layer, `forceSurfaceBeforeSupply` | `TurnStateManager.ScannerPrompt`, `TurnStateManager.SupplyQueue`, `TurnStateManager.CommandService` |
| `PodeDetectarSensor` | Motor principal de FoW/deteccao: calcula hexes visiveis, detecta inimigos, classifica em 4 buckets | Refresh de FoW, combate indireto, UI | `TurnStateManager`, `MatchController` — ver `05_relatorio_visao_spotting.md` |
| `SensorHandle` | Orquestrador: roda sensores principais e popula codigos A/E/D | Refresh de sensores no estado de scanner | `TurnStateManager.Sensors` |

## Quando entram no turno
- Entrada principal: `RefreshSensorsForCurrentState()` em `TurnStateManager.Sensors.cs`.
- Esse refresh roda para unidade selecionada em estados de scanner (pos-movimento) e repinta acoes disponiveis.

## Codigos de acao no scanner
Codigos populados em `availableSensorActionCodes`:

| Codigo | Acao | Sensor responsavel |
|---|---|---|
| `A` | Mirar (ataque) | `PodeMirarSensor` |
| `E` | Embarcar | `PodeEmbarcarSensor` |
| `D` | Desembarcar | `PodeDesembarcarSensor` |
| `C` | Capturar | `PodeCapturarSensor` |
| `F` | Fundir | `PodeFundirSensor` |
| `S` | Suprir | `PodeSuprirSensor` |
| `T` | Transferir | `PodeTransferirSensor` |

**Operacoes aereas (sem codigo de tecla):** o resultado de `AircraftOperationRules.Evaluate(...)` e armazenado em `cachedAircraftOperationDecision` mas **nao entra em `availableSensorActionCodes`** e nao tem atalho de teclado. O fluxo e acionado via UI, nao por tecla. Isso inclui pouso, decolagem e emersao de submarino.

## Observacoes importantes
- Nem todos os sensores passam pelo `SensorHandle`; alguns sao chamados direto no `TurnStateManager` (captura/fusao/suprir/transferir/command service/layer ops).
- O sensor de combate (`PodeMirarSensor`) e o mais complexo e concentra boa parte das validacoes taticas do jogo.
- `PodeEmergirSensor` valida emersao de submarino verificando: (a) unidade em Submarine/Submerged, (b) unidade suporta Naval/Surface, (c) construcao/estrutura/terreno no hex aceita Naval/Surface, (d) skills requeridas/bloqueadas.
- `PodeDetectarSensor` e o maior sensor do projeto (2594 linhas) e esta detalhado em `05_relatorio_visao_spotting.md`.
- `AISensorPriorityDefinition.cs` e um enum legado (`Attack/Capture/Reposition`) mantido por compatibilidade; nao e consumido pelo fluxo principal de sensores.
