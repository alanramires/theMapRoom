# RELATORIO V1.2.4

Tag: `v1.2.4`  
Commit: `5d0583e`  
Data: 2026-03-10

## Resumo
A versao 1.2.4 consolida ajustes de pouso/decolagem, combate aereo e regras de camada, com impacto em sensores, resolver de combate, logistica e dados de jogo (unidades, armas e RPS).

## Principais entregas
- Regras de combustivel no inicio da rodada com fila de destruicao por falta de autonomia em voo.
- Fluxo de explosao por falta de combustivel com foco de cursor/camera e temporizacoes no `AnimationManager`.
- Restricao de uso de armas por camada (`cantUseWeaponsOnTheFollowDomain`) integrada em mira, combate e revide.
- Nova mecanica de arma para forcar camada apos acerto:
  - `forceOpponentToGoToDomainAfterHit` em `WeaponData`.
  - lock de camada por turnos em `UnitManager`.
  - bloqueio de atalhos (decolar/submergir/transicao/suprimento forcado) enquanto lock estiver ativo.
- Regra de RPS para aeronave grounded no `CombatResolver`:
  - atacante usa `max(RPSAtaqueBase, 0)` quando o alvo da troca estiver grounded.
- Dialogs novos para mensagens editaveis de camada forçada/travada.

## Dados de jogo e conteudo
- Ajustes em varios `UnitData` aeronauticos e navais.
- Inclusao de arma antinavio/antisubmarino (`Carga de Profundidade`) e ajustes de `Torpedo`.
- Atualizacoes de matriz RPS (incluindo `RPS Submarine`).
- Inclusao de novo casco naval (`Marinha_Navio Desembarque`).
- Atualizacoes em prefabs, cenas de teste e sprites navais.

## Arquivos de codigo com maior impacto
- `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Match/MatchController.cs`
- `Assets/Scripts/Units/UnitManager.cs`
- `Assets/Scripts/Units/UnitData.cs`
- `Assets/Scripts/Weapons/WeaponData.cs`
- `Assets/Scripts/Sensors/PodeMirarSensor.cs`
- `Assets/Scripts/Sensors/PodeSuprirSensor.cs`
- `Assets/Scripts/Sensors/ServicoDoComandoSensor.cs`
- `Assets/Scripts/Units/Rules/AircraftOperationRules.cs`

## Dialog Data adicionados
- `layer.locked.by.weapon`
- `layer.forced.after_hit`
- `aim.invalid.attacker_blocked_at`
- `aim.invalid.attacker_layer_blocked`

## Observacoes
- A release agregou tambem reorganizacao/catalogacao de assets e atualizacoes extensas de cena.
- Escopo efetivo do commit: 91 arquivos alterados.
