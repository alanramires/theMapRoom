# Elite e RPS Skill

Este documento descreve como funciona a camada de elite no combate e como ela modifica o RPS base.

## Visao geral

O sistema de combate continua usando a matriz RPS normal (`RPSData`/`RPSDatabase`) como base.

Por cima disso, uma `SkillData` pode aplicar modificadores extras condicionais (ex.: `Dog Fight`) usando:

- classe da unidade dona da skill
- classe do oponente
- categoria da arma
- comparacao de `eliteLevel`

Esses modificadores extras sao chamados aqui de **Elite Skill**.

## Onde configurar

## UnitData

Arquivo: `Assets/Scripts/Units/UnitData.cs`

- `eliteLevel` (default `0`)
- `skills` (lista de skills da unidade)

Exemplo:
- Caca A: `eliteLevel = 1`, skill `Dog Fight`
- Caca B: `eliteLevel = 0`, sem skill (ou com outra)

## SkillData

Arquivo: `Assets/Scripts/Skills/SkillData.cs`

Campos principais da camada de combate:

- `enableCombatRpsModifier`
- filtros:
  - `filterOwnerClass` + `requiredOwnerClass`
  - `filterOpponentClass` + `requiredOpponentClass`
  - `filterWeaponCategory` + `requiredWeaponCategory`
  - `requireSameUnitClass`
- regra de elite:
  - `eliteComparison`
  - `minEliteDifference`
- 4 bonus da skill:
  - `ownerAttackRpsModifier`
  - `ownerDefenseRpsModifier`
  - `opponentAttackRpsModifier`
  - `opponentDefenseRpsModifier`

## Regra de ativacao por elite

Exemplo com `eliteComparison = AttackerGreater`:

- ativa se `eliteOwner - eliteOpponent >= minEliteDifference`
- com `minEliteDifference = 1`:
  - `2 vs 1`: ativa
  - `1 vs 0`: ativa
  - `2 vs 0`: ativa
- com `minEliteDifference = 2`:
  - `2 vs 0`: ativa
  - `2 vs 1`: nao ativa
  - `1 vs 0`: nao ativa

## Formula no combate

Cada lado recebe RPS base + Elite Skill.

## Ataque

- Atacante:
  - `RPSAtaqueFinal = RPSAtaqueBase + (ownerAttack do atacante) + (opponentAttack vindo da skill do defensor)`
- Defensor:
  - `RPSAtaqueFinal = RPSAtaqueBase + (ownerAttack do defensor) + (opponentAttack vindo da skill do atacante)`

## Defesa

- Atacante:
  - `RPSDefesaFinal = RPSDefesaBase + (ownerDefense do atacante) + (opponentDefense vindo da skill do defensor)`
- Defensor:
  - `RPSDefesaFinal = RPSDefesaBase + (ownerDefense do defensor) + (opponentDefense vindo da skill do atacante)`

## Onde isso ja esta aplicado

- Combate em jogo:
  - `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`
- Ferramentas de editor:
  - `Assets/Editor/CombatMatrixWindow.cs`
  - `Assets/Editor/CombatCalculatorWindow.cs`

Menu:
- `Tools/Combat/Matriz de Combate`
- `Tools/Combat/Calcular Combate`

## Logs para validacao

No trace de combate e nas ferramentas, verifique:

- `Elite atacante: X`
- `Elite defensor: Y`
- `ELITE SKILL ataque atacante/defensor`
- `ELITE SKILL defesa atacante/defensor`

Se aparecer tudo `+0`, normalmente e um destes pontos:

1. a unidade nao tem a skill atribuida em `UnitData.skills`
2. `enableCombatRpsModifier` esta desligado
3. algum filtro nao bate (classe/categoria)
4. regra de elite nao atende (`eliteComparison`/`minEliteDifference`)
