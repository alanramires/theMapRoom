# Relatório de Atualização - v2.0.0

## Em uma frase
Fundações para o próximo salto: IA recebe motor de decisão por score multi-critério, fase de compras autônoma, ferramenta de inspeção no Editor — e o TurnStateManager está documentado e pronto para virar pilha.

---

## O que isso trouxe na prática
- **IA mais coerente:** cada unidade recebe um score por hex candidato e escolhe o melhor; sem mais heurística hardcoded de "vai ao prédio mais perto".
- **IA compra soldados sozinha** ao fim do turno, usando as mesmas regras de shopping do replay humano.
- **Desenvolvedor enxerga a IA em tempo real** via painel `Tools > AI > AI Unit Planner` sem precisar de logs no console.

---

## Principais melhorias

### 1. Motor de decisão HexEvaluator (novo)
- Scoring multi-critério: `captureProximity + combatValue + cohesion − deviation + safety`.
- Contexto resolvido uma vez por unidade (`ResolveContext`): determina papel (`CaptureNow`, `CaptureAdvance`, `UsefulCombat`, `Cohesion`, `PrudentFow`, `UnderRepair`) e célula-alvo antes de qualquer scoring.
- Superlotação detectada por raio: ≥ 2 aliados a ≤ 2,5 unidades do alvo → pula para próxima opção.
- Ataque a ocupante de prédio contestado vale 2× combatValue e suprime penalidade de safety.
- Arquivos: `Assets/Scripts/Match/AI/HexEvaluator.cs`, `HexEvaluation.cs`, `CaptureDecisionReport.cs`.

### 2. Fase 2 — Shopping autônomo (novo)
- `AIPlayerOrchestrator.Shopping.cs` envia um `PlayerAction` do tipo `Shopping` para cada construção aliada desocupada com mercado habilitado.
- Seleciona "soldado" por id/nome; fallback para índice 0.
- Detecta menu de shopping que ficou aberto (compra sem dinheiro) e chama `HandleCancel()` para não travar o CommandService no turno seguinte.
- Fase inserida no loop principal entre `Phase1_UnitActions` e `Phase3_EndTurn`.

### 3. Ferramenta AIUnitPlannerWindow (nova)
- `Assets/Editor/AI/AIUnitPlannerWindow.cs`, acessível em `Tools > AI > AI Unit Planner`.
- Painel esquerdo: fila de unidades com snapshot tirado no AI Pause.
- Painel direito: tabela de HexEvaluation com scores por critério, linha vencedora destacada e `combatSummary` com alvos reais do PodeMirarSensor.
- Só avalia a primeira unidade da fila (as demais veriam estado desatualizado do mapa).

### 4. UnitManager — MarkAsDonorMergedInto (novo método)
- Permite que a unidade doadora registre a fusão em si mesma (`hasMerged`, `mergedWhenTurn`, `mergedWithUnit`) ao ser absorvida por outra.
- Complementa `MarkMergedWith` que já existia do lado da receptora.

### 5. AIPlayerOrchestrator — refinos de captura e combate
- `TryAttackContestedOccupants`: itera todos os prédios contestados, coleta candidatos (destino, opção de ataque, DPQ) e escolhe o de maior DPQ se `prioritizeDpqAtBattle`, ou o primeiro válido.
- `TryFindAttackTargeting`: variante de `TryFindAttack` que filtra por alvo específico, usada pela lógica de prédios contestados.
- `DecideCautiousApproach` chamado quando Attack falha por FOW mas há prédio contestado conhecido.

---

## Regras importantes

- `AISensorPriority`: lista por unidade em `UnitData.aiSensorPriority`. Sem lista → padrão `[Attack, Reposition]`. Com `Capture` → usa `TryDecideCapture` no loop de prioridades.
- `prioritizeDpqAtBattle`: flag por unidade. Ligada → varredura exaustiva de posições, escolhe DPQ máximo. Desligada → primeira opção válida encontrada.
- `repairTriggerHpBelow / repairTriggerAutonomyPct / repairTriggerAmmoEnabled`: gatilhos de entrada em modo de manutenção. `repairRecoverHpAbove` define saída. Unidade em reparo não entra no loop de decisão normal — marcha para o prédio aliado livre mais próximo.
- `fuseWhileInRepair`: flag que permite tentativa de fusão mesmo durante manutenção antes de reposicionar.

---

## Bloco técnico

- **Novos arquivos:** `Assets/Scripts/Match/AI/` (HexEvaluator, HexEvaluation, CaptureDecisionReport), `Assets/Editor/AI/` (AIUnitPlannerWindow), `AIPlayerOrchestrator.Shopping.cs`.
- **Modificados:** `AIPlayerOrchestrator.cs` (loop de fases), `AIPlayerOrchestrator.Capture.cs` (TryAttackContestedOccupants), `AIPlayerOrchestrator.Combat.cs` (TryFindAttackTargeting), `UnitManager.cs` (MarkAsDonorMergedInto), `TurnStateManager.Merge.cs` (ajuste menor), `Soldado.asset`.
- **Plano documentado:** `PLANO_REFACTOR_TURNSTATEMANAGER.md` — ~60 call sites de `SetCursorState` mapeados e classificados em `Advance / Retreat / ExecuteAndReset`. Refator ainda **não** aplicado; este commit é o checkpoint "antes".
- **Compatibilidade:** sem quebras de replay. Os `PlayerAction` gerados pela IA continuam idênticos ao formato humano.

---

## Resultado

A IA passou de heurística direta para motor de scoring. O próximo passo está planejado: o `TurnStateManager` vira pilha, eliminando os ~60 `SetCursorState` hardcoded e tornando os cancel handlers automáticos.
