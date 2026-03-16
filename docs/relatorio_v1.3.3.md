# RELATORIO v1.3.3

## Tema
Ajustes em estruturas e minor fixes (turno neutro, FoW, audio e editor).

## Entregas principais
- Turno do exercito neutro integrado ao fluxo de partida quando `includeNeutralTeam` esta ligado.
- FoW do turno neutro usando unidades neutras (sem visao de construcoes).
- Neutro sem logistica ativa: bloqueios de `suprir` e `transferir`.
- Contador de "unidades a agir" atualizado para suportar time neutro.
- `TAB` no turno neutro agora cicla para unidade neutra pronta.
- Fallback de teleporte do cursor no inicio do turno neutro sem HQ para unidade neutra mais proxima.
- `MatchMusicAudioManager` com suporte a `neutralTrack`.
- Deteccao com submarino apos `HasActed` com fallback de SFX sonar.
- Alivio de performance no editor: refresh continuo desligado por padrao em `ConstructionManager` e `MatchController`.

## Mudancas tecnicas relevantes
- `MatchController`
  - ajuste de ciclo/skip de turno neutro sem unidades em campo;
  - contagem de unidades por time aceita neutro quando a flag esta ativa;
  - FoW permite time neutro e evita contribuicao de construcoes no neutro;
  - fallback de cursor no start do turno neutro sem HQ;
  - fallback de SFX sonar para deteccao de submarino em alvo de superficie.
- `CursorController`
  - ciclo por `TAB` aceita time neutro quando `IncludeNeutralTeam` estiver ativo.
- `PanelRemainingController`
  - painel de unidades prontas considera neutro quando habilitado.
- `PodeSuprirSensor` e `PodeTransferirSensor`
  - bloqueio explicito para unidades do time neutro.
- `MatchMusicAudioManager`
  - novo campo `neutralTrack`;
  - selecao de faixa para `teamId = -1` com fallback seguro;
  - auto-assign reconhece `neutralTrack`/`neutral` em `Assets/audio/music`.
- `ConstructionManager` e `MatchController`
  - novos toggles para desativar polling continuo em modo de edicao.

## Arquivos chave alterados
- `Assets/Scripts/Match/MatchController.cs`
- `Assets/Scripts/Cursor/CursorController.cs`
- `Assets/Scripts/UI/PanelRemainingController.cs`
- `Assets/Scripts/Sensors/PodeSuprirSensor.cs`
- `Assets/Scripts/Sensors/PodeTransferirSensor.cs`
- `Assets/Scripts/Audio/MatchMusicAudioManager.cs`
- `Assets/Scripts/Construction/ConstructionManager.cs`
- `Assets/Scripts/Units/Rules/UnitMovementPathRules.cs`
- `Assets/Scripts/Structures/StructureData.cs`
- `docs/estruturas.md`

## Git
- Tag: `v1.3.3`
- Branch: `main`
