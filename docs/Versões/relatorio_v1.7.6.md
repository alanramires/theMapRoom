# v1.7.6 - AI Player Refactor

## Tema
Refatoração estrutural do `AIPlayerController` para reduzir acoplamento, facilitar manutenção e preparar cortes futuros sem continuar inflando o arquivo principal.

## O que entrou
- Início da parcialização de `AIPlayerController` com extração dos primeiros blocos coesos.
- Separação de `Transport` para arquivo próprio.
- Separação de `Support` para arquivo próprio.
- Separação de `CombatTargeting` para arquivo próprio.
- Separação de `Capture` para arquivo próprio.
- Separação de `TurnSummary` para arquivo próprio.
- Limpeza dos pontos quebrados deixados pela extração inicial.
- Ajustes de compilação em tipos auxiliares usados pelo planner e pelo save.

## Novos arquivos
- `Assets/Scripts/AI/AIPlayerController.Transport.cs`
- `Assets/Scripts/AI/AIPlayerController.Support.cs`
- `Assets/Scripts/AI/AIPlayerController.CombatTargeting.cs`
- `Assets/Scripts/AI/AIPlayerController.Capture.cs`
- `Assets/Scripts/AI/AIPlayerController.TurnSummary.cs`

## Intenção da refatoração
- Reduzir o custo de navegação no código.
- Tornar cada domínio da IA mais localizável.
- Diminuir o risco de quebrar comportamento distante ao editar o controller.
- Preparar a próxima rodada de cortes: `Planner`, `PlannerPersistence`, `Shopping`, `Phase2` e `RepairRecovery`.

## Observações
- Esta versão é focada em organização e manutenção, não em ganho de performance.
- O comportamento da IA continua o mesmo em essência; a principal mudança é estrutural.
- O `AIPlayerController.cs` ainda concentra partes sensíveis, especialmente planner e execução de turno, que serão os próximos candidatos de extração.
