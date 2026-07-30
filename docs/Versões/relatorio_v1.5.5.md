# AI Player data driven (v1.5.5)

## Resumo
Nesta versão, o comportamento do AI Player avançou para uma base mais data driven em decisões táticas por unidade e preparação de planejamento por dados.

## Entregas principais
- Base de `AI Plan` criada com:
  - `AIPlanData`
  - `AIPlanDatabase`
  - janela `Tools > AI > AI Planner`
- Novo editor para `UnitData > AI` com preferência de alvo por classe (`AI Target Preference (By Class)`).
- Preferência de alvo por classe ajustada para operar com fallback implícito:
  - classes não listadas são tratadas como prioridade interna terciária.
- `Target Preference` no perfil de IA operando como preferência hierárquica de alvo, sem bloquear ataque por ausência do alvo preferido.
- Correção no fluxo de captura para evitar caso de avanço sem tentativa de captura no ponto-alvo.
- Logs de snapshot ampliados com estado de FoW (`TotalWar`, `LoS`, `Stealth`) para depuração.

## Observações
- `AI Intel` permanece como ferramenta de diagnóstico/editor e ainda não é a fonte única de snapshot runtime do `AIPlayerController`.
- O pipeline de compras/suprimentos/fusão oportunista ainda contém trechos legados e será migrado para planner/perfis nas próximas versões.

## Arquivos de referência
- `Assets/Scripts/AI/Planning/AIPlanData.cs`
- `Assets/Scripts/AI/Planning/AIPlanDatabase.cs`
- `Assets/Editor/AIPlannerWindow.cs`
- `Assets/Scripts/Units/UnitData.cs`
- `Assets/Editor/UnitDataEditor.cs`
- `Assets/Scripts/AI/AIUnitProfile.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Automation.cs`
- `Assets/Scripts/AI/AIPlayerController.cs`