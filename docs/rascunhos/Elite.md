# Elite e RPS Skill

Este documento descreve como funciona a camada de elite no combate e como ela modifica o RPS base.

## Visao geral

O sistema de combate continua usando a matriz RPS normal (`RPSData`/`RPSDatabase`) como base.

Por cima disso, um `CombatModifierData` pode aplicar modificadores extras condicionais (ex.: `Dog Fight`) usando:

- classe da unidade dona do modifier
- classe do oponente
- categoria da arma
- comparacao de `eliteLevel`

Esses modificadores extras sao chamados aqui de **Elite Skill**.

## Onde configurar

## UnitData

Arquivo: `Assets/Scripts/Units/UnitData.cs`

- `eliteLevel` (default `0`)
- `combatModifiers` (lista de modifiers da unidade)

## CombatModifierData

Arquivo: `Assets/Scripts/Combat/CombatModifierData.cs`

Campos principais:

- `modifierType` (`Attack` ou `Defense`)
- filtros:
  - `requiredOpponentClass`
  - `requiredWeaponCategory`
- regra de elite:
  - `eliteComparison`
  - `minEliteDifference`
- 4 bonus da skill:
  - `ownerAttackRpsModifier`
  - `ownerDefenseRpsModifier`
  - `opponentAttackRpsModifier`
  - `opponentDefenseRpsModifier`

## Regra de ativacao por elite

Exemplo com `eliteComparison = Owner > Opponent`:

- ativa se `eliteOwner - eliteOpponent >= minEliteDifference`
- com `minEliteDifference = 1`:
  - `2 vs 1`: ativa
  - `1 vs 0`: ativa
  - `2 vs 0`: ativa

## Formula no combate

Use esta notacao:

- `FA_calc`: ataque calculado sem elite (arma + RPS + fatores base)
- `FD_calc`: defesa calculada sem elite (defesa base + DPQ + RPS)
- `FA_elite`: soma dos modificadores de ataque de elite
- `FD_elite`: soma dos modificadores de defesa de elite

## Duas formulas de eliminacao (explicitas)

### 1) Defensores Eliminados

`Defensores Eliminados = [HP Atacante x (FA base da Arma do Atacante + FA RPS do Atacante + FA Elite Atacante)] / (FD base Defensor + FD DPQ do Defensor + FD RPS Defensor + FD Elite Defensor)`

### 2) Atacantes Eliminados

`Atacantes Eliminados = [HP Defensor x (FA base da Arma do Defensor + FA RPS do Defensor + FA Elite Defensor)] / (FD base Atacante + FD DPQ do Atacante + FD RPS Atacante + FD Elite Atacante)`

### Composicao dos termos de Elite

- `FA Elite Atacante = ownerAttack(Atacante) + opponentAttack(Defensor)`
- `FD Elite Defensor = ownerDefense(Defensor) + opponentDefense(Atacante)`
- `FA Elite Defensor = ownerAttack(Defensor) + opponentAttack(Atacante)`
- `FD Elite Atacante = ownerDefense(Atacante) + opponentDefense(Defensor)`

## Os 4 pontos onde elite entra

1. No dividendo de `ELIM_D`: `FA_elite_A`
2. No divisor de `ELIM_D`: `FD_elite_D`
3. No dividendo de `ELIM_A`: `FA_elite_D`
4. No divisor de `ELIM_A`: `FD_elite_A`

Resumo: elite entra nos 2 dividendos e nos 2 divisores.

## Onde isso ja esta aplicado

- Combate em jogo:
  - `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`
- Ferramentas de editor:
  - `Assets/Editor/CombatMatrixWindow.cs`
  - `Assets/Editor/CombatCalculatorWindow.cs`
  - `Assets/Editor/Combat/CombatLargeMatrixWindow.cs`

Menu:
- `Tools/Combat/Matriz de Combate`
- `Tools/Combat/Calcular Combate`
- `Tools/Combat/Grande Matriz de Lutas`

## Logs para validacao

No trace de combate e nas ferramentas, verifique:

- `Elite atacante: X`
- `Elite defensor: Y`
- `ELITE SKILL ataque atacante/defensor`
- `ELITE SKILL defesa atacante/defensor`

Se aparecer tudo `+0`, normalmente e um destes pontos:

1. a unidade nao tem modifier atribuido em `UnitData.combatModifiers`
2. algum filtro nao bate (classe/categoria)
3. regra de elite nao atende (`eliteComparison`/`minEliteDifference`)
