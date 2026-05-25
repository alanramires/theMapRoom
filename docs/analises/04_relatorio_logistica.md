# Relatorio de Logistica (ServiceData + Suprimento)

Data base: 2026-05-25 (revisado; base original: 2026-03-06)

## Objetivo
Validar configuracao e unificacao de custo/consumo entre runtime e tools para `PodeSuprir` e `ServicoDoComando`.

## Configuracao atual de ServiceData
Fonte: `Assets/DB/Logistic/Services/*.asset`

| Servico | % custo (`percentCost`) | Recover points (L/M/H) | Cost Weight (L/M/H) | Recupera |
|---|---:|---|---|---|
| Reparar | 40 | 2 / 1 / 1 | - / - / - | HP |
| Reparos Leves | 40 | 2 / 1 / 1 | - / - / - | HP |
| Reabastecimento | 5 | 3 / 2 / 1 | - / - / - | Fuel |
| Rearmamento | 10 | 3 / 2 / 1 | 1 / 2 / 3 | Ammo |
| Transfer | 0 | - | - | (nao e servico de recuperacao) |

**Alteracoes desde a revisao anterior:**
- Reparar: 65% → 40%
- Reabastecimento: 10% → 5%
- Rearmamento: 25% → 10%
- Reparos Leves: servico novo (id: `reparosLeves`), mesmos stats que Reparar — diferenca esperada em gating/contexto de uso

**Campos novos em `ServiceData.cs`:**
- `apenasEntreSupridores`: quando ativo, o servico so pode ser aplicado entre unidades/construcoes supridoras (restringe Transfer a fluxo hub↔hub).
- `serviceLimitPerUnitPerTurn`: limite de usos do servico por unidade por turno (0 = sem limite).

## Servico de Transferencia (logistica de estoque)
- `Transfer` nao recupera HP/Fuel/Ammo diretamente no alvo de combate.
- Papel: mover estoque de `SupplyData` entre fornecedores/logisticos (hub <-> receiver, unidade <-> construcao) para alimentar a cadeia.
- Sensor dedicado: `PodeTransferirSensor` (valida tier, range de coleta/servico, dominio operacional e capacidade de receber/fornecer).
- Acao no turno: codigo `T` no scanner.

Resumo de gating no fluxo atual:
1. Unidade precisa ser `isSupplier`.
2. Precisa ter o servico `Transfer`.
3. Tier `SelfSupplier` nao participa.
4. Sem estoque/sem capacidade no destino -> sem opcao valida.

## Sistema de Autonomia (novo)
Fonte: `Assets/DB/Logistic/Autonomy/*.asset` + `Autonomy Database.asset`

O sistema de autonomia define como cada tipo de motor consome combustivel. Existem 4 configs atualmente:

| Config | id | isAircraft | movementMultiplier | turnStartUpkeep | Layers com upkeep |
|---|---|---:|---:|---:|---|
| Heavy Motor Autonomy | HeavymotorAutonomy | nao | 10 | 0 | - |
| Rotor Autonomy | rotorAutonomy | sim | 1 | 2 | AirLow, AirHigh, Surface |
| Turbo Helice Autonomy | turboHeliceAutonomy | sim | 1 | 3 | AirLow, AirHigh, Surface |
| Jet Autonomy | jetAutonomy | sim | 1 | 5 | AirLow, AirHigh, Surface |

**Regras de consumo:**
- **Terrestre (Heavy Motor)**: consome autonomia por hexagono movido, multiplicado por `movementAutonomyMultiplier` (10). Sem upkeep fixo por turno.
- **Aéreo (Rotor/TurboHelice/Jet)**: consome `turnStartUpkeep` por turno ao iniciar em camadas aereas ou no solo (Surface). Multiplier = 1 (movimento aereo nao adiciona custo extra de autonomia).
- O `upkeepStartLayerModes` define quais combinacoes de domain/heightLevel ativam o desconto de upkeep no inicio do turno.

## Recursos de Sensor (novo)
Fonte: `Assets/DB/Logistic/Recursos/*.asset`

Estes nao sao suprimentos de combate: sao **recursos de deteccao** consumidos por unidades sensor (EWACS, Submarino). Armazenados na pasta Logistic por compartilhar o sistema de estoque/supply.

| Recurso | id | Alcance (min-max) | Domain fonte | Detecta |
|---|---|---:|---|---|
| EWACS Radar | ewacsRadar | 3–10 | Air/AirHigh | AirLow, Naval/Surface, Land/Surface |
| Radar Station | radarStation | 1–10 | Air/AirHigh | AirLow, Naval/Surface, Land/Surface |
| Sonoboia | sonoBoia | 1–7 | Submarine/Submerged | Naval/Surface |
| Sonar Pulse | sonarPulse | 1–10 | Submarine/Submerged | Naval/Surface |

Detalhamento de uso e integracao com visao/FoW: ver `05_relatorio_visao_spotting.md`.

## Formula unificada de custo
Implementacao central: `Assets/Scripts/Services/ServiceData.cs` (`ServiceCostFormula.ComputeServiceMoneyCost`).

Blocos:
1. Alocacao do bloco monetario do servico:
`allocated = targetData.cost * (service.percentCost / 100f)`

2. HP
`unitHpCost = Round(allocated / maxHP)`
`hpCost = hpGain * unitHpCost`

3. Fuel
`unitFuelCost = Round(allocated / maxFuel)`
`fuelCost = fuelGain * unitFuelCost`

4. Ammo
- Capacidade ponderada por arma:
`weightedCapacity = sum(maxAmmoDaArma * costWeightDaClasse)`
- Custo por ponto ponderado:
`costPerWeightedPoint = allocated / weightedCapacity`
- Custo unitario por slot:
`unitWeaponCost = Round(costPerWeightedPoint * weightSlot)`
- Custo por arma:
`weaponCost = ammoRecoveredSlot * unitWeaponCost`

Fallback de peso (quando `costWeight` nao estiver preenchido):
- Light=1, Medium=2, Heavy=3.

## Formula unificada de consumo
Implementacao central: `ServiceLogisticsFormula.EstimatePotentialServiceGains(...)` em `ServiceData.cs`.

Regra por bloco (HP/Fuel/Ammo):
1. Resolve `pointsPerSupply` pela classe alvo.
2. Calcula recuperacao maxima por estoque.
3. Converte recuperacao em suprimentos com teto:
`requiredSupplies = Ceil(recovered / pointsPerSupply)`
4. Consome snapshot e retorna ganho efetivo.

Regra importante:
- Se nao houver supply disponivel para o servico, o ganho e zero.
- Em pratica: quando o estoque acaba, o servico acaba junto.

## Onde ocorre arredondamento operacional
- `RoundToInt` em:
- pontos por supply (`ResolvePointsPerSupply`)
- custo unitario HP/Fuel/Ammo
- capacidade ponderada de ammo
- `CeilToInt` em:
- conversao de recuperacao -> unidades de supply consumidas

## Chamadas por fluxo

### PodeSuprir (runtime)
- `TurnStateManager.Supply.cs` chama:
- `ServiceLogisticsFormula.EstimatePotentialServiceGains(...)`
- `ServiceCostFormula.ComputeServiceMoneyCost(...)`

### PodeSuprir (tools)
- `Editor/PodeSuprirSensorDebugWindow.cs` chama as mesmas funcoes centrais.

### ServicoDoComando (runtime)
- `TurnStateManager.CommandService.cs` chama:
- `ServiceLogisticsFormula.EstimatePotentialServiceGains(...)`
- `ServiceCostFormula.ComputeServiceMoneyCost(...)`

### ServicoDoComando (tools)
- `Editor/ServicoDoComandoDebugWindow.cs` chama as mesmas funcoes centrais.

## Papel das unidades logisticas (ponte para o front)
Exemplos no banco atual:

- `Trem de Carga`:
- supplier, tier Hub, com `Transfer` e estoque embarcado (fuel/ammo/pecas) alto.
- funcao: coletar e redistribuir para sustentar cidades finitas e recebedores no interior.

- `Navio Tanque`:
- supplier naval, com `Transfer` + `Reabastecimento`, estoque embarcado alto.
- funcao: linha de suprimento maritima para grupos navais e pontos costeiros.

Outros recebedores relevantes:
- `Suprimentos` (terrestre receiver), `Aviao Tanque` (aereo receiver), `Porta Avioes` (naval receiver).

## Finitude vs infinito de supply
- Construcao suporta oferta finita (`quantity`) ou infinita (`int.MaxValue`) no runtime.
- No config de mapa, os polos de base do jogador podem estar com oferta infinita dos 3 supplies (ex.: overrides com `2147483647`).
- Cidades e varias plataformas de campo operam com estoque finito.
- Como os servicos consomem supply, manter cadeia de transferencia ativa e obrigatorio para sustentar reparo/rearmamento/reabastecimento no front.
- **Estacao de Trem** tem supply infinito (3 tipos) — funciona como polo logistico alternativo ao HQ.

## Regra operacional: transportador com carga embarcada
Decisao de design adotada para evitar ambiguidade entre logistica movel e infraestrutura:

1. `Truck` pode suprir o `transportador` (a unidade veiculo), mesmo que ele esteja com passageiros embarcados.
2. `Truck` nao supre passageiros embarcados diretamente.
3. `Cidade/Base` pode suprir o `transportador` e tambem passageiros embarcados no mesmo contexto de servico local.
4. Passageiro embarcado recebe servico direto apenas de:
- `Cidade/Base`, ou
- do proprio `transportador` quando ele for `isSupplier`.

## Conclusao de unificacao
- Custo e consumo estao centralizados em `ServiceCostFormula` e `ServiceLogisticsFormula`.
- Runtime e simuladores compartilham o mesmo nucleo de calculo.
- Diferencas residuais hoje tendem a ser de contexto de entrada (estoque, alvo, gating), nao de formula.
- Consumidores finais tipicos: unidades de combate terrestre (ex: MBT, Tanque Pesado, Soldado) que recebem HP, Fuel e Ammo diretamente do Caminhao Suprimentos adjacente via ServicoDoComando ou PodeSuprir.
