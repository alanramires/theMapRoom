# Relatorio de Atualizacao - v1.5.0

## Em uma frase
A versao v1.5.0 estabelece a fundacao da IA jogavel: perfis de IA, snapshot de estado, avaliacao de postura reativa (Attack/Defend), integracao com o ciclo de turnos, alem de melhorias na tela de entrada, novo painel de nova partida, servico do comando automatico e atalhos de teclado desativados.

## O que isso trouxe na pratica
- A IA avalia sua postura no inicio de cada turno: defende o HQ se houver inimigo visivel a 5 hexagonos, ataca caso contrario.
- O `AIPlayerController` responde automaticamente ao evento `OnActiveTeamChanged` quando o time ativo tem `isAI = true`.
- Intel de construcoes e publica: a IA conhece todos os HQs e construcoes do mapa desde o inicio.
- O painel de nova partida permite configurar jogadores (Humano/IA/Off), regras e cena alvo antes de iniciar a partida.
- O Servico do Comando pode rodar automaticamente no inicio do turno da IA, simulando X + Enter com animacao completa.
- O MenuRoot agora funciona em qualquer cena (removida restricao ao nome "Tela de Entrada").
- Atalhos P, Z e F9 desativados temporariamente (mantidos no codigo para proxima versao).

## Principais entregas

### 1. Sistema de IA — fundacao
- `AIStance` (enum): `Attack` / `Defend`
- `AISnapshot`: foto do estado do jogo no inicio do turno — HQ proprio, HQ inimigo, construcoes conhecidas, unidades amigas, inimigos visiveis, tilemap de referencia.
- `AIProfile` (base abstrata): interface para perfis de IA intercambiaveis.
- `BeginnerAIProfile`: avalia postura com regra dos 5 hexes do HQ; sem memoria entre partidas.
- `AIPlayerController` (MonoBehaviour): assina `OnActiveTeamChanged`, constroi snapshot, avalia postura. Flag `aiLog` para ligar/desligar logs no inspector.
- `HexCoordinates.IsWithinRange`: BFS usando `GetImmediateHexNeighbors` para medir distancia hexagonal real.

### 2. Flag isAI no MatchController
- `PlayerEntry.isAI` adicionado ao struct de jogador.
- `SetPlayerIsAI(TeamId, bool)` e `IsPlayerAI(TeamId)` como metodos de acesso.
- Editor custom (`MatchControllerEditor`) exibe o campo no inspector.

### 3. PartidaConfig — persistencia entre cenas
- Classe estatica que carrega configuracao de nova partida (times, isAI, flipX, preset, cena alvo) e aplica no `MatchController` ao acordar na cena de batalha.
- `SaveGameManager.SetupForNewGame(saveDir)` carrega o diretorio de save sem auto-save.

### 4. NewGamePanelController
- Painel fixo com J1-J4: J1 inicia Humano, J2 inicia IA, J3/J4 iniciam Off.
- Modos por slot: Humano / IA / Off (exclusivos).
- Selecao de cena automatica por contagem de jogadores (2→Battle Map, 3→Triple Trouble, 4→Team Island).
- Navegacao por teclado com som de cursor e confirmacao; highlight de selecao (#4A5A43).
- Draft do `MatchController` atualizado em tempo real enquanto o jogador configura.

### 5. Servico do Comando automatico
- Flag `commandServiceAutomatic` no `MatchController` (+ editor custom).
- `HandleAutoCommandServiceTeamChanged`: pula turnos 0 e 1, dispara coroutine nos demais.
- `AutoCommandServiceRoutine`: simula X (abre preview) + 1 frame + Enter (inicia animacao), com animacao completa visivel.

### 6. MenuRoot universal
- `MainMenuStateController`: removidos `MainMenuSceneName` e `IsMainMenuScene` — bootstrap em qualquer cena.
- `BattleMapMenuRootController`: removido guard `IsBattleMapScene` — ESC funciona em qualquer cena de batalha.

### 7. Atalhos desativados
- `P` (scanner prompt), `Z` (scanner prompt), `F9` (replay panel, helper panel, visibility hotkeys) desativados com comentario `// disabled - proxima versao`.

## Bloco tecnico
- Scripts novos:
  - `Assets/Scripts/AI/AIStance.cs`
  - `Assets/Scripts/AI/AISnapshot.cs`
  - `Assets/Scripts/AI/AIProfile.cs`
  - `Assets/Scripts/AI/AIPlayerController.cs`
  - `Assets/Scripts/AI/Profiles/BeginnerAIProfile.cs`
  - `Assets/Scripts/Match/PartidaConfig.cs`
  - `Assets/Scripts/UI/NewGamePanelController.cs`
- Scripts modificados:
  - `Assets/Scripts/Hex/Core/HexCoordinates.cs` (+ IsWithinRange)
  - `Assets/Scripts/Match/MatchController.cs` (isAI, commandServiceAutomatic, PartidaConfig.Apply)
  - `Assets/Scripts/Match/TurnState/TurnStateManager.CommandService.cs` (auto command service)
  - `Assets/Scripts/Match/TurnState/TurnStateManager.HelperPanel.cs` (subscribe auto command)
  - `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs` (P, Z desativados)
  - `Assets/Scripts/Save/SaveGameManager.cs` (SetupForNewGame)
  - `Assets/Scripts/UI/BattleMapMenuRootController.cs` (universal)
  - `Assets/Scripts/UI/MainMenuStateController.cs` (universal)
  - `Assets/Scripts/UI/DebugManager.cs`
  - `Assets/Scripts/UI/PanelHelperController.cs` (F9 desativado)
  - `Assets/Scripts/UI/PanelVisibilityHotkeysController.cs` (F9 desativado)
  - `Assets/Scripts/UI/Replay/ReplayPanelUI.cs` (F9 desativado)
  - `Assets/Editor/MatchControllerEditor.cs` (isAI, commandServiceAutomatic)
- Documentacao:
  - `docs/relatorio_v1.5.0.md`

## Pendencias conhecidas (proxima versao)
- TeamId engessado: construcoes e unidades usam TeamId fixo em vez de slot do MatchController. Ao mudar o time de um slot, os objetos em cena nao atualizam automaticamente.
- Execucao de acoes da IA ainda nao implementada (proximo passo: integracao com automated player do ReplayManager).
- Atalhos P, Z, F9 aguardando redesign para proxima versao.

## Resultado
A v1.5.0 marca a transicao do Map Room para um jogo com IA jogavel em esboço. A fundacao esta no lugar: perfil, snapshot, postura, integracao com turnos. A proxima versao endereçara o sistema de slots de time e a execucao real de acoes pela IA.
