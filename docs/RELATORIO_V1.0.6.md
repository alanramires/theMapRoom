# Relatorio Tecnico - v1.0.6

## Versoes cobertas

- `v1.0.5` - commit `3f5ff93` - `v1.0.5 - Elite combat`
- `v1.0.6` - commit `ba4f924` - `v1.0.6 - road bonus, domain change`
- Branch: `main`

Este relatorio resume as principais mudancas entregues entre `v1.0.5` e `v1.0.6`.

---

## 1) Combate elite (v1.0.5)

### Objetivo

Adicionar inflexao de combate para unidades de mesma classe com diferenca de elite, sem quebrar o RPS base.

### O que entrou

1. Skill de combate com modificador de RPS condicional:
- filtros por classe do dono/oponente;
- filtro por categoria de arma;
- comparacao de elite e diferenca minima.

2. Modelo de bonus expandido para 4 eixos:
- bonus de ataque do owner;
- bonus de defesa do owner;
- bonus de ataque do oponente;
- bonus de defesa do oponente.

3. `eliteLevel` explicito em `UnitData` (default `0`).

4. Integracao no runtime de combate:
- calculo final considera RPS base + bonus de skill elite;
- logs de combate passaram a exibir elite/skill nas contas.

5. Ferramentas de editor alinhadas ao runtime:
- `Tools/Combat/Calcular Combate`
- `Tools/Combat/Matriz de Combate`
- ambas passaram a refletir elite + RPS + DPQ no mesmo padrao.

### Arquivos-chave

- `Assets/Scripts/Skills/SkillData.cs`
- `Assets/Scripts/Units/UnitData.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`
- `Assets/Editor/CombatCalculatorWindow.cs`
- `Assets/Editor/CombatMatrixWindow.cs`

### Documentacao criada/atualizada

- `docs/Elite.md`
- `docs/Combat.md`
- `docs/Sensor PodeMirar.md`

---

## 2) Mobilidade por estrada e troca de domain (v1.0.6)

### Objetivo

Melhorar leitura tatico-operacional de movimento com bonus de estrada controlado por dados e controle manual de camada/domain por unidade.

### O que entrou

1. Bonus de estrada no pathfinding:
- regra: ao fazer full move em estrada, ganha `+1` passo;
- restrito a `Land/Surface` com `move >= 4`;
- passo bonus custa `0` de autonomia;
- o passo bonus tambem precisa cair em estrada (sem exploit em montanha).

2. Bonus orientado a dados de estrutura:
- `StructureData` ganhou flag `roadBoost`;
- apenas estruturas com `roadBoost = true` habilitam o bonus;
- ponte pode ficar sem bonus (`roadBoost = false`).

3. Inspector do `UnitManager` com controle de camada:
- botoes `Subir Domain` e `Descer Domain`;
- ordenacao por pilha de altitude (bottom->top):
  - `Submerged = 2`
  - `Surface = 3`
  - `AirLow = 4`
  - `AirHigh = 5`
- sem loop circular: no topo/na base, nao avanca.

4. Atualizacao de assets de mapa/arte:
- pacote de tiles de quebra-mar;
- palette dedicada para costa;
- ajustes de cena para nova leitura de litoral.

### Arquivos-chave

- `Assets/Scripts/Units/Rules/UnitMovementPathRules.cs`
- `Assets/Scripts/Structures/StructureData.cs`
- `Assets/Editor/UnitManagerEditor.cs`
- `Assets/DB/structures/Rodovias.asset`
- `Assets/Scenes/SampleScene.unity`

---

## 3) Resultado consolidado (v1.0.5 + v1.0.6)

O projeto ficou mais forte em tres frentes:

1. **Balanceamento de combate**
- RPS base continua simples;
- elite adiciona inflexao controlada por dados.

2. **Movimento tatico**
- estrada agora tem identidade mecanica clara;
- custo de autonomia e alcance valido seguem consistentes.

3. **Legibilidade de jogo**
- fluxo de altitude/domain mais controlavel no inspector;
- costa/agua comunicam melhor as regras de mapa.

---

## Referencias de apoio

- `docs/Combat.md`
- `docs/Elite.md`
- `docs/Sensor PodeMirar.md`
- `docs/regras de LoS e LdT.md`
- `docs/v1.md`
