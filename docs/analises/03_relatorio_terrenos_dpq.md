# Relatorio de Terrenos e DPQ

## Base analisada
- Assets de terreno: `Assets/DB/World Building/Terrain/*.asset`
- Assets de construcoes: `Assets/DB/World Building/Construction/*.asset`
- Assets de estruturas: `Assets/DB/World Building/Structures/*.asset`
- Estrutura de dados: `TerrainTypeData`, `ConstructionData`, `StructureData`, `DPQData`, `TerrainVisionResolver`

## Tabela de terrenos
| Terreno | Dominio | Custo movimento (autonomia) | DPQ | Pontos DPQ | Defesa DPQ | EV | Block LoS | Shooter herda EV |
|---|---|---:|---|---:|---:|---:|---|---|
| Planicie | Land/Surface | 1 | Padrao | 1 | 0 | 0 | sim | nao |
| Floresta | Land/Surface | 2 | Melhorado | 2 | 2 | 1 | sim | nao |
| Montanha | Land/Surface | 99 | Favoravel | 3 | 4 | 2 | sim | sim |
| Mar | Naval/Surface | 1 | Padrao | 1 | 0 | 0 | sim | nao |
| Praia | Naval/Surface | 1 | Unfavorable | 0* | -1* | 0 | sim | nao |

`*` Em `DPQ_Desfavoravel.asset` os campos numericos nao estao serializados, mas `DPQData` aplica default por qualidade (`Unfavorable => pontos 0, defesa -1`).

## Modificadores defensivos
- Defensivo de terreno entra por `terrain.dpqData.DefesaBonus` (via `ResolveDpqAtUnitPosition` no combate).
- O terreno tambem influencia LoS por `ev` e `blockLoS` (via `TerrainVisionResolver.Resolve`).

## Modificadores de movimento
- Campo principal: `basicAutonomyCost` de `TerrainTypeData`.
- Ainda pode ser alterado por skill via `skillCostOverrides` e regras de entrada (`requiredSkillsToEnter`, `blockedSkills`).

## Skills de pre-requisito e barateamento em terrenos
Leitura dos assets atuais:

- Floresta:
- `blockedSkills`: `Linha de Trem`
- `skillCostOverrides`: `Guerrilha => autonomyCost 1` (barateia de 2 para 1)

- Montanha:
- `requiredSkillsToEnter`: `Alpino` ou `Off - Road`
- `blockedSkills`: `Linha de Trem`
- `skillCostOverrides`:
- `Alpino => autonomyCost 2` (de 99 para 2)
- `Off - Road => autonomyCost 6` (de 99 para 6)

## Modificadores de visao
- `ev`: elevacao de visada do hex.
- `blockLoS`: se o hex bloqueia linha de visada.
- `shooterInheritsTerrainEv`: se atirador herda EV da origem.
- Excecoes por construcao/estrutura no terreno (`constructionVisionOverrides`, `structureVisionOverrides`).
- Para camadas aereas, pode haver override por `DPQAirHeightConfig.TryGetVisionFor(...)`.

## LoS por elevacao relativa
No `PodeMirarSensor`, a linha de visada em cada celula intermediaria usa:
- `losHeightAtCell = Lerp(originEv, targetEv, t)`
- bloqueia quando `cellBlocksLoS == true` e `cellEv > losHeightAtCell`
- excecao: se `targetEv - cellEv >= 2`, o obstaculo nao bloqueia

Detalhamento completo da cadeia de visao/spotting esta no relatorio 05 (`05_relatorio_visao_spotting.md`).

## Construcoes (impacto tatico-economico)
No banco atual, construcoes usam principalmente DPQ/renda/supply, com regras de skill de entrada vazias:

| Construcao | baseMovementCost | requiredSkillsToEnter | blockedSkills | skillCostOverrides |
|---|---:|---|---|---|
| HQ | 1 | vazio | vazio | vazio |
| Cidade | 1 | vazio | vazio | vazio |
| Fabrica | 1 | vazio | vazio | vazio |
| Aeroporto | 1 | vazio | vazio | vazio |
| Porto Naval | 1 | vazio | vazio | vazio |
| Barracks | 1 | vazio | vazio | vazio |

## Estruturas (impacto de mobilidade e acesso)
| Estrutura | baseMovementCost | requiredSkillsToEnter | blockedSkills | roadBoost |
|---|---:|---|---|---|
| Rodovia | 1 | vazio | Linha de Trem | sim |
| Ponte Alta | 1 | vazio | Linha de Trem | nao |
| Ponte para Trem | 1 | Linha de Trem | vazio | nao |
| Trilho | 1 | vazio | vazio | nao |

## Road Boost da estrada
- A flag `roadBoost` da estrutura e consumida no pathfinding (`UnitMovementPathRules`).
- Regra atual:
- unidade terrestre em `Land/Surface`
- `baseMove >= 4`
- se fizer o deslocamento base inteiro em celulas com `roadBoost`, ganha 1 passo extra com custo 0 (movimento e autonomia) em uma celula de estrada logo apos o full move.

## Leitura estrategica
- Montanha concentra melhor pacote defensivo/visao (DPQ alto + EV alto + heranca de EV).
- Floresta e mid-ground tatico: ganho de defesa e visada, custo de deslocamento maior.
- Planicie e mar sao neutros para defesa/visao e favorecem mobilidade.
- O eixo skill+terreno esta forte: `Alpino`/`Off - Road` transformam montanha de gargalo em rota viavel.
- Rodovia com `roadBoost` acelera projecao terrestre e influencia bastante tempo de reforco entre frentes.

## Referencia cruzada: pouso e supply aereo
- As skills exigidas por terreno/estrutura/construcao para pouso/decolagem entram no resultado tatico do turno.
- O comportamento de "forcar pouso/decolagem antes de suprir" (decisao de balanceamento) esta detalhado no `07_relatorio_turn_state_manager.md`.
