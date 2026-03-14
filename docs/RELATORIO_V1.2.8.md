# RELATORIO_V1.2.8

Versao: `v1.2.8`  
Commit: `fe852ff`  
Titulo: **Carga de profundidade**

## Principais mudancas

- Sistema de deteccao stealth consolidado em runtime por unidade/time observado:
  - Atualizacao de `pode detectar` e `alguem me ve` no fluxo de `hasAct:true`.
  - Controle de times que observam cada unidade no `UnitManager`.
  - Atualizacao de HUD/olhinho de detectado conforme observacao real.

- Regra de stealth apos disparo:
  - Novo flag runtime `HasFiredThisTurn` no `UnitManager`.
  - Ao confirmar disparo, unidade marca `hasFired`.
  - Enquanto `hasFired` estiver ativo, a unidade pode ser tratada como nao-stealth nas validacoes de observacao/FOW.
  - Flag limpa no reset de turno da unidade.

- Ajuste de ocupacao em hex disputado:
  - Continua proibido encerrar movimento com 2 unidades do mesmo time no mesmo hex.
  - Passagem por hex com aliado no caminho agora permitida (quando valido), sem permitir terminar nele.

- Armas e camada forcada apos hit:
  - Novo atributo em `WeaponData`:
    - `unitsOnTheFollowDomainAreForcedToEmergeAfterBeingHit` (lista `Domain/HeightLevel`).
  - Integracao no combate para forcar emerge de alvos em camadas configuradas.
  - Lock de camada ajustado para 2 turnos nesse fluxo.

- FX naval de dano:
  - `AnimationManager` recebeu suporte a frames dedicados de hit naval (`takingHitNavalFrames`).
  - Reuso do mesmo controller/tempos do Taking Hit normal (sem duplicar pipeline).
  - Auto-assign de frames em `Assets/img/animations/naval hit/`.

- Dados de combate/RPS:
  - Ajustes no catalogo e tabelas RPS, incluindo entrada de `RPS Navio`.
  - Atualizacoes de assets de unidades/armas marinhas e calibracao naval.

## Arquivos com destaque

- Codigo:
  - `Assets/Scripts/Match/MatchController.cs`
  - `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`
  - `Assets/Scripts/Match/TurnState/TurnStateManager.Range.cs`
  - `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
  - `Assets/Scripts/Match/Animation/AnimationManager.cs`
  - `Assets/Scripts/Units/UnitManager.cs`
  - `Assets/Scripts/Units/UnitRulesDefinition.cs`
  - `Assets/Scripts/Weapons/WeaponData.cs`

- Dados/Assets:
  - `Assets/DB/Combat/RPS/*`
  - `Assets/DB/Character/Weapons/Anti Navio/Carga de Profundidade.asset`
  - `Assets/img/animations/naval hit/*`
  - `docs/calibracao/calibração_marinha.md`

## Publicacao

- Branch `main` publicada em `origin`.
- Tag `v1.2.8` criada e publicada em `origin`.
