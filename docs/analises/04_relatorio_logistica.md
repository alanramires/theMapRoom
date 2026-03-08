# Relatorio de Logistica (ServiceData + Suprimento)

## Objetivo
Validar configuracao e unificacao de custo/consumo entre runtime e tools para `PodeSuprir` e `ServicoDoComando`.

## Configuracao atual de ServiceData
Fonte: `Assets/DB/Logistic/Services/*.asset`

| Servico | % custo (`percentCost`) | Recover points (L/M/H) | Cost Weight (L/M/H) | Recupera |
|---|---:|---|---|---|
| Reparar | 65 | 2 / 1 / 1 | - / - / - | HP |
| Reabastecimento | 10 | 3 / 2 / 1 | - / - / - | Fuel |
| Rearmamento | 25 | 3 / 2 / 1 | 1 / 2 / 3 | Ammo |
| Transfer | 0 | - | - | (nao e servico de recuperacao) |

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

## PodeSuprir (runtime)
- `TurnStateManager.Supply.cs` chama:
- `ServiceLogisticsFormula.EstimatePotentialServiceGains(...)`
- `ServiceCostFormula.ComputeServiceMoneyCost(...)`

## PodeSuprir (tools)
- `Editor/PodeSuprirSensorDebugWindow.cs` chama as mesmas funcoes centrais.

## ServicoDoComando (runtime)
- `TurnStateManager.CommandService.cs` chama:
- `ServiceLogisticsFormula.EstimatePotentialServiceGains(...)`
- `ServiceCostFormula.ComputeServiceMoneyCost(...)`

## ServicoDoComando (tools)
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
- Consumidores finais típicos: unidades de combate terrestre (ex: MBT, Tanque Pesado, Soldado) que recebem HP, Fuel e Ammo diretamente do Caminhão Suprimentos adjacente via ServicoDoComando ou Suprir de um caminhão adjacente por exemplo.
