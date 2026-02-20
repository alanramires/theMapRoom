# Combat

Documento de referencia do combate no estado atual do projeto.

## Escopo

Este fluxo cobre:

- selecao de alvo e arma via sensor (`PodeMirar`)
- resolucao de ataque e revide
- RPS base por classe/categoria de arma
- camada extra de Elite Skill
- defesa com bonus de DPQ
- matchup de DPQ para arredondamento

## Entrada do combate

O combate parte de um `PodeMirarTargetOption`, com:

- atacante e defensor
- arma atacante selecionada
- arma de revide (quando houver)
- indices de armas embarcadas
- distancia

## Pipeline (runtime)

Arquivo principal:
- `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`

Ordem:

1. valida entrada e unidades
2. consome municao do atacante
3. tenta consumir municao de revide do defensor
4. resolve RPS de ataque (base)
5. resolve Elite Skill (owner/opponent, atk/def)
6. calcula forca de ataque efetiva
7. resolve DPQ de posicao (atacante e defensor)
8. resolve RPS de defesa (base)
9. calcula forca de defesa efetiva
10. resolve matchup DPQ (outcome atacante/defensor)
11. calcula eliminacao, arredonda por outcome, aplica HP final

## Formulas atuais

## Ataque

Para cada lado:

- `RPSAtaqueFinal = RPSAtaqueBase + EliteSkillAtaqueTotal`
- `FAEfetiva = HP * max(0, Arma + RPSAtaqueFinal)`

Onde:

- `EliteSkillAtaqueTotal (lado X) = ownerAttack da skill do lado X + opponentAttack vindo da skill do outro lado`

## Defesa

Para cada lado:

- `RPSDefesaFinal = RPSDefesaBase + EliteSkillDefesaTotal`
- `FDEfetiva = DefesaUnidade + DefesaDPQ + RPSDefesaFinal`

Onde:

- `EliteSkillDefesaTotal (lado X) = ownerDefense da skill do lado X + opponentDefense vindo da skill do outro lado`

## Eliminacao

- no defensor: `FAEfetivaAtacante / max(1, FDEfetivaDefensor)`
- no atacante (se revide): `FAEfetivaDefensor / max(1, FDEfetivaAtacante)`

Arredondamento usa `DPQCombatMath.DivideAndRound(...)` com outcome de DPQ:

- `Vantagem`
- `Neutro`
- `Desvantagem`

## RPS base

Arquivos:

- `Assets/Scripts/RPS/RPSData.cs`
- `Assets/Scripts/RPS/RPSDatabase.cs`

Chave de ataque:

- `attackerClass + weaponCategory + defenderClass`

Chave de defesa:

- `defenderClass + attackerClass + weaponCategory`

Sem match:

- bonus `0`

## Elite Skill

Arquivos:

- `Assets/Scripts/Skills/SkillData.cs`
- `Assets/Scripts/Units/UnitData.cs` (`eliteLevel`)

Ativacao depende de filtros da skill:

- classe owner
- classe opponent
- categoria da arma
- mesma classe (opcional)
- comparacao de elite (`eliteComparison` + `minEliteDifference`)

A skill pode aplicar 4 modificadores:

- owner attack
- owner defense
- opponent attack
- opponent defense

## Revide

Revide so entra quando:

- defensor pode contra-atacar
- existe arma valida de revide
- consumo de municao do revide foi bem-sucedido

Se nao houver revide:

- ataque do defensor = 0
- termos de skill/RPS do ataque defensor ficam neutros

## DPQ de posicao

Resolucao (prioridade):

1. override de ar por camada ativa (`DPQAirHeightConfig`)
2. construcao na celula (suportando layer da unidade)
3. estrutura na celula (suportando layer)
4. terreno na celula (suportando layer)
5. fallback neutro

## Ferramentas de editor

Menu:

- `Tools/Combat/Calcular Combate`
- `Tools/Combat/Matriz de Combate`

Arquivos:

- `Assets/Editor/CombatCalculatorWindow.cs`
- `Assets/Editor/CombatMatrixWindow.cs`

As duas ferramentas estao alinhadas com o runtime:

- usam RPS base
- aplicam Elite Skill
- mostram elite no log
- incluem DPQ no calculo

## Logs esperados

No trace (runtime e tools), buscar:

- classe e elite de atacante/defensor
- chave de RPS
- termos de Elite Skill (ataque e defesa)
- forca de ataque/defesa final
- matchup DPQ e resultado aplicado

Se Elite Skill nao entrar:

1. skill nao atribuida em `UnitData.skills`
2. `enableCombatRpsModifier` desativado
3. filtro da skill nao bate
4. regra de elite nao atende
