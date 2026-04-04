# Relatorio de Atualizacao - v1.6.2

## Em uma frase
Supply e Transfer ganham suporte completo na IA, com perfis configuráveis por unidade e decisões emergindo dos sensores do jogo.

## O que isso trouxe na pratica
- Caminhões de suprimento suprem aliados e voltam à base para reabastecer de forma autônoma e configurável.
- Cada unidade suprida tem limiares individuais de combustível, munição e HP para ser considerada alvo prioritário.
- Perfis de IA distintos (Lutador, Kamikaze, Supridor, Bazooka, Capturador) validados e corrigidos.

## Principais melhorias

1. **Supply como Sensor de primeira classe**
- Supply saiu de um bloco hardcoded pré-sensor e entrou na fila `sensorPriority` como `AIUnitSensorKind.Supply`.
- O supridor agora age como qualquer outra unidade: executa sensores em ordem, respeita seu perfil.
- Resultado: porta-aviões e unidades multi-papel podem ser supridores sem código especial.

2. **Restock Decision — volta à base para reabastecer a carroceria**
- Novos limiares configuráveis no perfil: `Galões %`, `Caixas %`, `Peças %`.
- Quando a carroceria cai abaixo do limiar, a unidade interrompe a missão e executa `Pode Transferir` na construção aliada mais próxima.
- Autonomia própria baixa (combustível da unidade) também dispara retorno — via transferência, não via reparo.

3. **Supply Decision — critérios para escolha do aliado a suprir**
- Limiares configuráveis por perfil: `Refuel Ally %`, `Rearm Ally %`, `Repair Ally %`.
- Score por criticidade: HP faltando (×120), munição faltando (×45), combustível faltando (×35), com bônus por limiar atingido.
- Alvo preferido (pré-calculado no movimento) mantém coerência com a execução final via bonus de 100.000 pts.

4. **Return to Base / Repair — seção unificada com Fuse**
- Seção renomeada para evidenciar que reparo, fusão e retorno são uma decisão única de sobrevivência.
- `Fuse with Nearby Units While Repairing`: toggle por perfil — Kamikaze desliga, Lutador mantém ativo.
- Triggers de reparo agora incluem: HP ≤ threshold, autonomia ≤ 25% (não-supridores), munição = 0.

5. **Híbrido — fallback artilheiro correto**
- Fluxo: tiro parado → move+atira → reposiciona para faixa de tiro (não avança às cegas).
- Âncora de reposicionamento artilheiro agora é passada corretamente no escopo do turno.
- Suporte a `preferredSupportCells` e `penalizedMovementCells` no reposicionamento híbrido.

6. **Dead Heart Sprite no HUD**
- Campo `deadHeartSprite` adicionado na seção HP do `UnitHudController`.
- Exibido quando HP = 0, antes da morte da unidade.

## Regras importantes

- `allowSupply`: deve estar `true` no perfil para a unidade entrar no sensor Supply. Default `false`.
- `preferDefendMode`: supridor com este flag patrulha zona defensiva ao invés de avançar.
- `restockFuelThresholdPercent = 0`: só volta para reabastecer quando a carroceria zerar (comportamento padrão).
- `fuseWhileOnRepairMode = false`: unidade nunca se expõe para fusão durante retorno ao base (ideal para Kamikaze/Sniper).
- `hpRepairThreshold = 0`: unidade jamais entra em modo de reparo (Kamikaze).

## Bloco tecnico curto

- `AIUnitProfile.cs`: novos campos `restockFuel/Ammo/PartsThresholdPercent`, `supplyAllyFuel/Ammo/HpThresholdPercent`, `fuseWhileOnRepairMode`, `preferDefendMode`, `allowSupply`.
- `AIPlayerController.cs`: `IsSupplyTruckOutOfReserves` usa limiares por tipo de supply (galão/caixa/peça) vs baseline de fábrica; `IsSupplyTruckTargetThresholdMet` usa thresholds do perfil supridor; anchor híbrido setado no escopo do turno.
- `AIUnitSensorKind.cs`: `Supply = 2`, `Reposition = 3`.
- `AIUnitProfileEditor.cs`: seções Restock Decision, Supply Decision, Return to Base / Repair com Fuse, sensor priority reordenável.
- `UnitHudController.cs`: `deadHeartSprite` para HP = 0.

## Resultado
A IA de suprimento deixou de ser um bloco monolítico hardcoded e passou a ser um conjunto de perfis declarativos. Um caminhão, um porta-aviões e um destruidor podem ter comportamentos logísticos completamente diferentes sem tocar em código — só configurando assets.
