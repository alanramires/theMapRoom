# Relatorio do Sistema de Combate

## Objetivo
Mapear a anatomia da formula de combate: origem de FA/FD, aplicacao de RPS/DPQ, elite, ferido e calculo final de eliminados.

## Pipeline da resolucao
Arquivo-base: `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs` (`ResolveCombatFromSelectedOption`).

1. Snapshot inicial
- Coleta atacante, defensor, arma usada e HP atual.
- Consome 1 municao do atacante e, se houver revide valido, 1 municao do defensor.

2. FA (forca de ataque efetiva)
- Termo bruto de ataque por lado:
`arma.basicAttack + RPSAtaqueBase + EliteSkillAtaqueProprio + EliteSkillAtaqueRecebido`
- Piso operacional: `max(1, termoBruto)` para disparo valido.
- FA efetiva:
`HPAtual * termoAplicado`

3. FD (forca de defesa efetiva)
- Defesa base da unidade vem de `UnitData.defense`.
- Defesa total por lado:
`defesaUnidade + defesaDPQ + RPSDefesaBase + EliteSkillDefesaProprio + EliteSkillDefesaRecebido + UnidadeFerida`

4. Matchup DPQ
- Resolve outcome do atacante e do defensor pelo delta de pontos DPQ.
- Banco: `DPQMatchupDatabase`.

5. Eliminados
- Bruto:
`elimNoDefensor = FAatacante / FDdefensor`
`elimNoAtacante = FAdefensor / FDatacante` (se revide)
- Arredondamento orientado por outcome DPQ via `DPQCombatMath.DivideAndRound`.
- Clamp por trava de HP: eliminacao aplicada em cada lado nao pode ultrapassar o HP do oponente no inicio da troca.

## De onde vem cada termo

## FA base e FD base
- FA base vem de `WeaponData.basicAttack` da arma selecionada.
- FD base vem de `UnitData.defense`.

## RPS
- Ataque: `ResolveAttackRps(...)` -> `RPSDatabase.TryResolveAttackBonus(...)`.
- Defesa: `ResolveDefenseRps(...)` -> `RPSDatabase.TryResolveDefenseBonus(...)`.
- Valores de tabela: assets em `Assets/DB/Combat/RPS/`.

## DPQ
- DPQ da posicao e resolvido em `ResolveDpqAtUnitPosition(...)`.
- Prioridade de fonte:
1. Ar em altura (via `DPQAirHeightConfig`)
2. Construcao ocupante
3. Estrutura ocupante
4. Terreno
- Outcome atacante/defensor resolvido em `dpqMatchupDatabase.Resolve(...)`.

## Elite
- Nivel base: `UnitData.eliteLevel`.
- Aplicacao real vem de `CombatModifierResolver.Resolve(...)`, usando `CombatModifierData` da unidade.
- Modifiers podem alterar quatro eixos simultaneamente:
`ownerAttack`, `ownerDefense`, `opponentAttack`, `opponentDefense`.

## Unidade ferida (penalidade)
- Em `ResolveWoundedDefensePenalty(...)`:
- HP cheio: `0`
- HP entre 6 e max-1: `-1`
- HP <= 5: `-2`

## Numero final de eliminados
- Calculado no fim de `ResolveCombatFromSelectedOption(...)`.
- Etapas finais:
1. Divide FA por FD segura (`max(1, FD)`).
2. Arredonda por outcome DPQ.
3. Aplica `Mathf.Max(0, ...)`.
4. Aplica trava por HP do oponente.
5. Atualiza HP final (sem negativo).

## Observacoes de design
- O motor usa composicao de efeitos (arma + RPS + elite + DPQ + estado de ferimento), entao balancear apenas por custo e insuficiente.
- Revide so entra se sensor confirmar condicoes (arma, range, municao, camada).
- O trace de combate no proprio metodo ja imprime todos os termos usados, bom para auditoria de balanceamento.
