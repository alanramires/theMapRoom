# Relatorio de Atualizacao - v1.5.3

## Em uma frase
A versao v1.5.3 consolida a AI Automatation com captura e pausar, permitindo interromper a simulacao da IA com seguranca via Player Menu e retomar sem quebrar batches em andamento.

## O que isso trouxe na pratica
- `ESC` durante turno da IA agora solicita pausa e abre o menu no proximo estado `Neutral`.
- A IA respeita o estado `PlayerMenu`: enquanto o menu estiver aberto, a execucao automatica fica congelada.
- Ao sair do menu, o fluxo retorna para `Neutral` e a IA continua o turno do ponto correto.
- Save/Load acionados pelo menu passam a compartilhar o state de `PlayerMenu`, mantendo o dispatcher oficial.
- A espera automatica da IA foi ajustada para nao estourar timeout enquanto estiver em `PlayerMenu`.

## Principais entregas

### 1. Pausa pendente por ESC no turno da IA
- Implementado pedido de pausa pendente quando o jogador aperta `ESC` fora de `Neutral`.
- O menu do jogador so abre quando o fluxo atual chega em `Neutral` e sem execucao critica em andamento.
- Feedback visual no `PanelDialog` informando que a pausa foi solicitada.

### 2. Congelamento seguro da automacao em PlayerMenu
- `AIPlayerController` ganhou espera explicita para `PlayerMenu` entre fases/sub-passos.
- Evita avancar fase, selecionar unidade ou confirmar acao enquanto o jogador esta no menu.

### 3. WaitUntilAutomatedNeutralReady compatível com pausa
- `TurnStateManager.Automation` passou a ignorar consumo de timeout enquanto estado for `PlayerMenu`.
- Mantem sincronizacao limpa ao retomar, sem queda prematura para fallback.

### 4. Save/Load integrados ao state PlayerMenu
- `SaveGameManager` permite operacao quando o turno da IA esta pausado no `PlayerMenu`.
- Entrada e saida do prompt de persistencia agora preservam retorno para `Neutral` ao finalizar/cancelar.

## Bloco tecnico
- Scripts modificados:
  - `Assets/Scripts/UI/BattleMapMenuRootController.cs`
  - `Assets/Scripts/AI/AIPlayerController.cs`
  - `Assets/Scripts/Match/TurnState/TurnStateManager.Automation.cs`
  - `Assets/Scripts/Save/SaveGameManager.cs`

## Pendencias conhecidas (proxima versao)
- Unificar logs de pausa/retomada com padrao unico de telemetria por fase.
- Revisar calibracao fina de captura em cenarios com multiplos objetivos equidistantes.
- Expandir controles de pausa para comandos dedicados (alem de `ESC`) mantendo semantica do dispatcher.

## Resultado
A v1.5.3 torna a simulacao da IA interrompivel de forma confiavel, sem quebrar animacoes, batches ou estados do fluxo oficial, mantendo captura e automacao alinhadas ao pipeline de jogo.
