# v4.0.11 - AI Unit Analysis

## Contexto

Nova ferramenta de Editor para analisar o desempenho de combate das unidades **a partir dos dados** (`UnitData`), sem precisar de cena ou Play mode. O objetivo é dar suporte a três frentes ao mesmo tempo: **balanceamento** (achar unidades dominantes/sem-counter), **manual** (exportar tabelas de matchup) e **IA** (validar quem contera quem).

Nenhuma lógica de jogo foi alterada. A ferramenta é somente leitura.

---

## Fonte única de verdade

A janela consome o **mesmo `AICombatHpSimulator`** que a IA usa em jogo para estimar duelos. Não há uma nova cópia da fórmula de combate — a scorecard é, literalmente, a previsão do que a IA vai calcular.

- DPQ customizável via overload `Simulate(..., dpqPoints, dpqDefenseBonus)`.
- Override de arma via `SimulateWithWeapons(...)`.
- Seleção de arma/revide pela mesma lógica de sensor (`PodeMirarSensor`).
- Mapeamento de DPQ canônico via `DPQData.GetPontosPadrao` / `GetDefesaPadrao`.

Havia 4 janelas de combate no Editor que **reimplementam** a fórmula (`CombatCalculatorWindow`, `CombatMatrixWindow`, `CombatHpMatrixWindow`, `CombatLargeMatrixWindow`). Convergi-las para o `AICombatHpSimulator` fica como limpeza futura.

---

## Janela: `Tools/Units/Unit Analysis`

Escolhe **1 atacante** e simula o disparo contra **todas** as unidades do banco.

### Parâmetros

| Campo | Função |
|---|---|
| Atacante | unidade sob análise |
| Distância | hex (default 1); a dist ≥2 não há revide (regra do sensor) |
| Arma do atacante | `Auto` (Weapon Priority) · `Principal` · `Secundária` — força a arma, contornando a prioridade (ex: tanque sem munição no canhão primário) |
| DPQ da unidade / do oponente | qualidade de posição dos dois lados (`Unfavorable`…`Unique`) |
| HP do atacante | máximo ou override |

### Tabela ofensiva (por alvo)

| Coluna | Conteúdo |
|---|---|
| Dano | ★0-5 pelos buckets (0-2 fraco, 3-4 razoável, 5-7 forte, 8-10 counter) + valor |
| Recebe | ★ de segurança invertida (0 recebido = ★★★★★) |
| Troca $ | score −5..+5 = valor destruído vs arriscado (`dano/HPmax × custo`) + net em dinheiro |
| TTK | turnos para matar |
| Vive | atacante sobrevive à troca |
| Veredito | `COUNTER NATURAL / Counter (custo) / Forte / Troca boa / Desvantagem / Neutro / não alcança` |

---

## Filtros

- **Listar alvos inalcançáveis** — inclui matchups fora de alcance/domínio.
- **Incluir a própria unidade como alvo**.
- **Ordenar por troca de valor** (counters no topo).
- **Ocultar alvos que não revidam** / **Exibir apenas os que não revidam** (mutuamente exclusivas; a segunda isola alvos moles tipo artilharia/suprimento).
- **Mostrar quem atira nesta unidade (e o revide dela)** — segunda tabela, visão defensiva: cada unidade ataca a escolhida e vê-se quanto ela toma e devolve. Veredito `MORRE / Sem revide / Devolve+ / Troca / Apanha`. O override de arma aqui força a arma de **revide** da unidade.

---

## Matriz arma × classe (cobertura p/ Shopping)

Visão conjunta (não dois agrupamentos exclusivos): agrupa por **arma usada × classe do alvo**, com uma nota de **cobertura**.

**Cobertura** = Σ(notas dos alvos alcançáveis daquela arma+classe) ÷ **tamanho total da classe**.

### Nota estável por matchup

```
1,00 counter natural   · 0,75 counter econômico · 0,60 forte
0,40 parcial/troca boa · 0,20 neutro            · 0,00 desvantagem/inalcançável
```

### Correções de precisão (para uso no Shopping)

1. **Inalcançáveis contam como zero.** O denominador é o tamanho cheio da classe; inalcançáveis só entram no denominador. Uma unidade não fica "melhor" ignorando alvos que não atinge. Classes 100% inalcançáveis aparecem como linha explícita com cobertura 0,00.
2. **Veredito econômico, não diferença de dano.** A cobertura é a média das notas por matchup, e cada nota vem do veredito individual, calculado com **custos** (`tradeScore`/`netValue`) — não mais `dano_médio − dano_recebido`.
3. **Mortes explícitas.** Cada célula guarda `deaths` (matchups fatais para o atacante), visível mesmo com cobertura alta — não some na média.

O caso Tanque B sai como esperado: contribui contra infantaria (cobertura ~0,45) mas não fecha uma demanda sozinho.

**Pendência conhecida:** o denominador inclui não-combatentes (suprimento/transporte) na classe, o que pode subestimar cobertura. Filtro "só combatentes" (via `combatClassification`) fica como próximo passo.

---

## Exportação

Botão **Exportar CSV** (`docs/UNIT_ANALYSIS.csv` por padrão). Quando a matriz está ativa, exporta a matriz (arma, classe, cobertura, alcance, mortes, nota); senão, exporta a tabela ofensiva por alvo. Serve de insumo direto pro manual e pra autoria da `Tabela de Prioridades` da IA.

---

## Arquivos

- `Assets/Editor/Units/UnitAnalysisWindow.cs` (novo)

---

## Resultado

Ferramenta de análise de combate data-driven, alinhada à fórmula real da IA, com scorecard por alvo, visão defensiva (revide), override de arma e matriz de cobertura arma × classe pronta para alimentar decisões de Shopping e o manual.
