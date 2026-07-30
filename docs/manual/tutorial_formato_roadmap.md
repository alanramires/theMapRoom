# Formato de tutorial (JSON) — roadmap e dívida técnica

Contexto: o tutorial é definido em `TutorialData` (ScriptableObject) com duas listas —
`objectives` (as **tarefas**: `id` = tipo de evento, `key` = identidade `hist_Y_XX`) e `script`
(os **passos**/falas). Há export/import JSON (`TutorialManager` + `TutorialManagerEditor`).

Uma auditoria externa avaliou o formato JSON. Este documento registra **o que foi aceito e feito**,
**o que foi recusado** (e por quê), e **o que ficou como dívida** (Fase 3), para não se reabrir a
discussão do zero depois.

---

## Feito

### Fase 1 — DTO limpo (risco ~zero, só camada de export/import)
- **Enums como string**: `advance`, `turn`, `movement`, `interactionType` saem no JSON com o **nome**
  (`"AimOpened"`, `"HoldOnly"`), não o int cru do `JsonUtility`. O asset segue com enums; a conversão
  é `ToString()` ↔ `Enum.TryParse` (ignoreCase). Round-trip exato.
- **Estado de runtime fora do asset**: `TutorialObjectiveDto` leva só campos de **design**. `isVisible`
  / `isCompleted` / `hasFailed` são runtime (o `TutorialManager` reseta no início) e **não** entram no
  round-trip — no import são reinicializados (`isVisible = !startHidden`, `isCompleted=false`). Isso
  elimina o risco de assar o estado de um teste no asset de design.

### Fase 2 — Linter de tutorial (aditivo)
- Campo `interactionType` por passo (`Auto | Narrative | Passive | Active | Milestone`). Default
  `Auto` = **inferido** de `advance` + presença de comandos de cena. Override opcional.
- `TutorialManager.LintTutorial(objectives, script)` (lógica pura, sem dependência de editor):
  - **Ritmo**: nº de passos antes da 1ª interação `Active` (avisa ≥6), aviso se não há `Active`, e
    resumo por tipo + "≈1 interação a cada N passos".
  - **Keys órfãs**: `objectiveKey` / `waitObjectiveKey` / `revealObjectiveKey` que referenciam
    objetivo inexistente → `[ERRO]`.
  - **Rich-text**: tags desconhecidas ou desbalanceadas (set real: `ordem/enfase/azul/amarelo/vermelho`,
    pareadas — espelha `PanelDialogTutorialController.FormatSpeechText`).
- Roda **automático no import** e por um botão **"Validar"** no inspector.

---

## Recusado (com base no código, não na intuição)

O auditor só viu o JSON. Estes pontos dele são leitura incorreta:

- **"`description` do objective é redundante com o script"** — **falso**. `objective.description` é a
  **label da lista de tarefas** e a **mensagem de derrota** (`TutorialManager` → `DeclareTutorialDefeat`),
  superfície diferente do balão de diálogo. E `obj.id` é o **tipo de evento** (`UNIT_AT_HEX`...), não
  "código genérico". Manter.
- **"`voicePath` vazio é poluição"** — é future-proofing de dublagem. Um `voice_mapping.json` separado
  é over-engineering agora. Não fazer.
- **"Números mágicos de `advance/turn/movement`"** — o problema real era só a renderização do
  `JsonUtility` (int), resolvido na Fase 1 (string). Os enums têm significado claro no código.

---

## Dívida técnica (Fase 3) — só com aval, exige migração + teste por tutorial

Estes valem, mas são projeto à parte: **quebram compatibilidade e obrigam a migrar todo tutorial já
escrito**, testando cada um. Ficam documentados aqui como dívida consciente.

### 3.1 — Unificar os caminhos moderno e legado (advance/reveal/wait)

**Problema.** Coexistem dois sistemas para "esperar/revelar/completar objetivo":
- Moderno: `advance` (`ObjectiveCompleted`) + `objectiveKey` + `revealObjective` (bool).
- Legado: `waitObjectiveKey` / `waitObjectiveIndex` e `revealObjectiveKey` / `revealObjectiveIndex`
  e `unlockMovement`.

A `TutorialData.MigrateLegacyDialogFlow` consolida os legados nos modernos **uma vez** (OnValidate),
mas o runtime **ainda lê os legados** — confirmado em:
- `PanelDialogTutorialController.ResolveWaitIndex` lê `waitObjectiveKey` (fallback `waitObjectiveIndex`).
- `PanelDialogTutorialController.ResolveRevealIndex` lê `revealObjectiveKey` (fallback índice).
- `PanelDialogTutorialController` e `TutorialManager` leem `unlockMovement`.

Por isso os campos legados **não podem ser dropados** hoje (por isso seguem no DTO do round-trip).

**Alvo.** Colapsar para um caminho único: `advance` + `objectiveKey` + `revealObjective` + `movement`.
- Passo 1: fazer o `PanelDialogTutorialController` ler **só** os modernos.
- Passo 2: migração one-shot de todos os `TutorialData` (garantir que a `MigrateLegacyDialogFlow` rodou
  e persistir; ou script de editor que varre os assets).
- Passo 3: remover os campos legados de `TutorialDialogEntry`, do DTO e da resolução.

**Risco.** Médio-alto: se a migração perder um caso, um tutorial quebra silenciosamente (fala não
avança / tarefa não revela). Exige rodar cada tutorial ponta-a-ponta depois.

### 3.2 — Ações estruturadas no lugar de `spawnCommand` / `statCommand` (strings)

**Problema.** Hoje os comandos de cena são mini-linguagens em string:
`spawnCommand = "slot0 SD 1,3 name=Ryan cursor"`, `statCommand = "Mathias hp=4; pan Mathias"`.
Frágil (um `;` errado quebra), sem validação no editor, e renomear uma unidade obriga caçar em todas
as strings. Parseado por `TutorialManager.ProcessSpawnCommand` / `TryExecuteSingleStatCommand`
(sintaxe completa no comando de debug `tutorial help`).

**Alvo.** Array de ações tipadas, ex.:
```json
"actions": [
  { "type": "spawn", "slot": 0, "faction": "SD", "pos": [1,3], "name": "Ryan", "cursor": true },
  { "type": "setStat", "unit": "Mathias", "hp": 4 },
  { "type": "cameraPan", "target": "Mathias" }
]
```
Permite validação/autocomplete no editor e erro em tempo de design.

**Custo.** Alto: reescrever o parser (manter o parser de string como **fallback/migrador**), migrar
todos os assets, e um período de dupla-fonte. Vale, mas é o item mais caro.

### 3.3 — (Menor) `interactionType` como dado curado

Hoje é `Auto` (inferido). Se a Fase 3.2 acontecer, ações estruturadas dão sinais mais fortes para a
inferência; e vale curar manualmente os `Milestone` (que a inferência não pega).

---

## Princípio

Fase 1 (legibilidade) + Fase 2 (feedback/linter) entregam ~80% do benefício com ~20% do risco, **sem
migrar nada**. A Fase 3 é onde se paga o custo de compatibilidade — só quando houver apetite para
migrar e testar cada tutorial. O próximo ganho de iteração não é mais um campo; é o linter já
entregue, mais a unificação (3.1) quando a dívida incomodar.
