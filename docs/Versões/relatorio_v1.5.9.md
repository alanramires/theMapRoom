# AI Planner Refactor por plano

## Resumo
- Refatoracao do Planner para manter atribuicoes entre turnos por plano (persistencia de missao), reduzindo realocacoes indevidas.
- Consolidacao do fluxo de papeis do Planner via `AIPlanRole` no lugar de strings legadas no runtime.
- Adicao de debug visual no `AI Manager` para mostrar o plano atual direto no HUD da unidade.

## Alteracoes principais
- `AIPlanEvaluator`
  - Pipeline com persistencia e realocacao controlada (preservacao, excecoes e preenchimento de faltas).
  - Chave estavel de plano (`BuildPlanKey`) para manter continuidade entre turnos.
  - Logs de ciclo do planner (`preservado`, `realocado-defesa`, `realocado-invasao`, `bloqueado-realocacao`, `liberado-estagnacao`, `preenchido-livre`).

- `AIPlayerController`
  - Memoria de atribuicoes por unidade/plano entre turnos (`previousAssignmentsByUnitId`).
  - Atualizacao de memoria com metrica de progresso por plano para detectar estagnacao.
  - Novo toggle de debug no manager: `showPlanDebugAtUnit`.
  - Aplicacao de badge de plano no HUD da unidade ao fim da avaliacao do planner.

- `UnitHudController`
  - Suporte a badge de plano no elemento `plan` do `unitHUD`.
  - API `SetPlanDebugBadge(bool visible, string text)`.
  - Auto-bind de referencias do badge para facilitar uso no prefab atual.

- `AIPlayerControllerEditor`
  - Exposicao do campo `Show Plan (Debug) At Unit` no inspector customizado.

## Convencao do badge de plano (debug)
- Plano fixo de defesa: `0`
- Plano fixo de invasao/ataque: `>`
- Plano por setor: inicial do setor (`Alpha=A`, `India=I`, etc.)

## Validacao
- Build `Assembly-CSharp`: sucesso (0 erros).

## Observacoes
- Migracao de legado de papeis por string permanece temporaria para compatibilidade de assets; remover apos reserializacao completa dos planos.
