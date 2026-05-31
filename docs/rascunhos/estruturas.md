# Avaliacao das 4 Estruturas (regras atuais)

## Regras-base consideradas
- Em hex com estrutura, `required/blocked` de entrada vem da estrutura (global ou `skillRulesByTerrain` quando houver match de terreno).
- Regras de `required/blocked` do terreno base nao bloqueiam entrada quando existe estrutura no hex.
- Custo usa cascata: custo base da estrutura -> override da estrutura (global ou por terreno) -> override do terreno (somente se houver entrada valida).
- Em `skillRulesByTerrain`, se existir regra para o terreno e a lista especifica nao estiver vazia, ela sobrepoe o global daquele bloco.

## 1) Rodovia (`road`)
- Base:
  - `baseMovementCost = 1`
  - `blockedSkills` global contem `Linha de Trem`
  - `roadBoost = true`
- Excecao por terreno:
  - Em Montanha: `Motor -> autonomyCost 2`
- Inferencia:
  - Unidade com `Linha de Trem` nao entra em Rodovia (global blocked).
  - Veiculos comuns entram com custo 1 em geral.
  - Em Rodovia sobre Montanha, unidade com `Motor` paga 2 (penalidade local explicita).

## 2) Trilho (`trilho`)
- Base:
  - `baseMovementCost = 2`
  - `roadBoost = false`
  - `required/blocked/override` globais estao com entradas `None` (nao efetivas, mas devem ser limpas no asset).
- Excecoes por terreno:
  - Regra A: terreno (guid `f8bd...`) com overrides:
    - `Motor -> 2`
    - `Linha de Trem -> 1`
  - Regra B: Montanha (guid `75d3...`):
    - `requiredSkillsToEnter = Linha de Trem`
    - `Linha de Trem -> 1`
- Inferencia:
  - Fora da excecao, trilho custa 2 para qualquer unidade.
  - Em Montanha, so entra quem tem `Linha de Trem`, e paga 1.
  - Em planicie, para trem pagar 1, precisa existir regra de planicie com override `Linha de Trem -> 1` (ou reduzir base).

## 3) Ponte Alta (`bridges`)
- Base:
  - `baseMovementCost = 1`
  - `blockedSkills` global contem `Linha de Trem`
  - Permite dominios adicionais (`Sea/Surface` e `Submerged` via lista atual do asset).
- Excecao por terreno:
  - Nao ha `skillRulesByTerrain` configurado.
- Inferencia:
  - Ponte Alta funciona como travessia generica com custo 1.
  - Trem nao entra (blocked global), salvo mudanca explicita.

## 4) Ponte com Trilhos (`trainBridge`)
- Base:
  - `baseMovementCost = 2`
  - `requiredSkillsToEnter = Linha de Trem`
  - Sem `skillRulesByTerrain`.
- Inferencia:
  - Estrutura dedicada a trem: sem `Linha de Trem`, nao entra.
  - Com `Linha de Trem`, entra e paga 2 (na configuracao atual).
  - Se quiser consistencia com trilho "rapido", adicionar override global `Linha de Trem -> 1` ou excecoes por terreno.

## Pontos de atencao
- `Trilhos.asset` ainda contem elementos `None` em listas globais. Mesmo com hardening no codigo, vale limpar no Inspector para evitar leitura ambigua.
- Juncoes podem trocar de estrutura efetiva por prioridade/ordem da `StructureDatabase`; isso pode mudar custo em um unico hex.
