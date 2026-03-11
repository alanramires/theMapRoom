# Sistema de estados

## 1) Analise do sistema de estados do jogo (turnos, acoes, mecanicas)
O projeto usa dois niveis de estado, bem definidos:

- Estado macro da partida (`MatchController`):
  - controla turno atual, time ativo, ordem de jogadores, neutral opcional
  - aplica eventos de inicio de turno (upkeep/autonomia, economia, renda por construcao)
  - faz a transicao de turno com delays e sincronizacao de audio

- Estado micro da unidade/entrada (`TurnStateManager`):
  - controla ciclo de interacao via `CursorState` (`Neutral`, `UnitSelected`, `MoveuAndando`, `MoveuParado`, `Mirando`, `Capturando`, `Suprindo`, `Embarcando`, `Desembarcando`, `Fundindo`, `ShoppingAndServices`, etc.)
  - processa `Confirm/Cancel` por estado (dispatch explicito)
  - integra sensores (`Pode*`) para abrir/fechar acoes validas
  - executa mecanicas com commit/rollback (movimento, consumo de custo, restauracao em cancelamento)

As mecanicas (combate, logistica, embarque, pouso/decolagem, captura, transferencia) entram como subfluxos dentro desse estado micro, geralmente com flags de execucao em progresso para bloquear concorrencia de acoes.

## 2) O design indica uma maquina de estados clara ou fluxos espalhados pelo codigo?
Predomina uma maquina de estados clara.

Evidencias:

- `CursorState` centraliza os modos de interacao.
- `HandleConfirm` e `HandleCancel` fazem roteamento por estado atual, com metodos dedicados por caso.
- `TurnStateManager` foi particionado em arquivos parciais por subdominio (`StateMachine`, `Movement`, `Sensors`, `Combat`, `Supply`, `Transfer`, `HelperPanel`), mantendo o mesmo nucleo de estado.
- `MatchController` separa o fluxo de turno global do fluxo tatico da unidade.

Existe complexidade alta (muitos subfluxos), mas ela nao esta "solta". O fluxo esta concentrado em orquestradores claros (`MatchController` e `TurnStateManager`), enquanto regras de elegibilidade/calculo ficam em sensores e resolvers.

## Conclusao
O sistema nao se comporta como fluxos espalhados aleatoriamente; ele implementa uma maquina de estados hierarquica:
- camada de turno (partida)
- camada de acao (cursor/unidade)
- camada de regras (sensores/resolvers)

Isso indica desenho intencional de fluxo, com controle de transicao e de efeitos colaterais.
