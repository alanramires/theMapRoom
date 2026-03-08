# Game Systems Overview

## Escopo
Documento tecnico consolidado dos sistemas principais do projeto:
- unidades
- combate
- logistica
- economia
- terrenos/DPQ
- sensores
- fluxo de turno

## 1. Unidades
- Fonte principal: `UnitData` (`Assets/DB/Character/Unit/**`).
- Campos nucleares: custo, HP max, autonomia, movimento, visao, defesa, elite, dominio/altura, armas embarcadas.
- Relatorio detalhado: `01_relatorio_unidades.md`.

## 2. Combate
- Core em `TurnStateManager.Combat.cs`.
- Formula e composicional:
`ataque (arma+RPS+elite) x HP` contra `defesa (base+DPQ+RPS+elite+ferido)`.
- DPQ define outcome de arredondamento.
- Resultado final aplica clamp por trava de HP para evitar overkill matematico acima do limite do confronto.
- Relatorio detalhado: `02_relatorio_sistema_combate.md`.

## 3. Terrenos e DPQ
- `TerrainTypeData` define custo de movimento, EV, block LoS e DPQ vinculado.
- `DPQData` define pontos e bonus de defesa.
- Resolver de visao por celula: `TerrainVisionResolver`.
- Relatorio detalhado: `03_relatorio_terrenos_dpq.md`.

## 4. Logistica
- Nucleo unificado em `ServiceData.cs`:
- `ServiceCostFormula.ComputeServiceMoneyCost`
- `ServiceLogisticsFormula.EstimatePotentialServiceGains`
- Runtime e tools (PodeSuprir + ServicoDoComando) convergem para esse nucleo.
- Relatorio detalhado: `04_relatorio_logistica.md`.

## 5. Visao e spotting
- Visao usa `UnitData.visao` + LoS por terreno/camada + regras globais.
- Spotter habilita fogo indireto/fora de observacao direta em contexto valido.
- Relatorio detalhado: `05_relatorio_visao_spotting.md`.

## 6. Economia
- Caixa por time e renda por turno em `MatchController`.
- Renda deriva de construcoes capturadas (`CapturedIncoming`).
- Compras (shopping) e servicos logisticos competem pelo mesmo caixa.
- Relatorio detalhado: `06_relatorio_economia.md`.

## 7. Fluxo de turno
- `TurnStateManager` conduz state machine por unidade/acao.
- `MatchController` conduz camada macro de turno/time/economia.
- Relatorio detalhado: `07_relatorio_turn_state_manager.md`.

## 8. Sensores
- Sensores encapsulam validacao tatico-contextual de cada acao.
- `SensorHandle` orquestra parte do conjunto, e `TurnStateManager` consome os resultados para abrir estados de execucao.
- Relatorio detalhado: `08_relatorio_sensores.md`.

## Conclusao executiva
O projeto ja esta estruturado em camadas coerentes (dados -> sensores -> state machine -> execucao), e a unificacao recente da logistica reduziu significativamente risco de divergencia entre simuladores e runtime. O maior vetor de balanceamento permanece na intersecao entre custo de unidade, tabela RPS/elite e custos operacionais de sustain (repair/refuel/rearm).
