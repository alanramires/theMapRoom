# Tutorial em andamento

Versao: v1.4.7  
Status: checkpoint de integracao de tutorial, hints e condicoes de fim de partida

## Base comparada
- Commit anterior: `af00e07` (v1.4.6 - apos o planner)
- Commit atual: `c68942a` (v1.4.7 - tutorial em andamento)
- Delta tecnico: 110 arquivos alterados, 415961 insercoes e 36401 remocoes

## Resumo
- Estrutura completa de tutorial adicionada (dados, regras, manager, painel e cenas dedicadas).
- Fluxo de objetivos passou a responder a eventos reais da partida (compra, ataque, movimento, destruicao, revelacao na fog e embarque/desembarque).
- Jogo ganhou fechamento formal de vitoria/derrota para tutorial e para o caso de ficar sem unidades.
- Sistema de hints contextuais foi integrado ao dialogo e ao hover com delay configuravel.

## Entregas principais

### 1) Framework de Tutorial (dados + runtime)
- Novos scripts centrais:
  - `TutorialData`, `TutorialDatabase`, `TutorialManager`, `TutorialRules`, `PanelTutorialController`, `HelpManager`, `HelpHintId`.
- Novo fluxo de objetivos com suporte a:
  - objetivos normais e condicoes de derrota (`isDefeatCondition`/`hasFailed`),
  - validacao por eventos de combate e movimento,
  - objetivos por coordenada e por tipo de unidade,
  - spawn dinamico por parametro (`spawn:TEAM TOKEN X,Y`).
- `TutorialRules` centraliza regras especiais por tutorial (ex.: restauracao de HP em marco de objetivo no Tutorial 1).

### 2) Barramento de eventos de gameplay para objetivos
- `TurnStateManager` passou a publicar eventos de alto nivel:
  - `OnUnitPurchased`, `OnUnitInspected`, `OnConstructionInspected`, `OnAttackResolved`,
  - `OnUnitRevealedFromFog`, `OnUnitDestroyed`, `OnUnitMovementExecuted`,
  - `OnUnitSelected`, `OnUnitEmbarked`, `OnUnitDisembarked`.
- Esses eventos agora sao disparados nos pontos corretos dos subfluxos (`ConstructionShopping`, `Movement`, `ScannerPrompt`, `Disembark`, `HelperPanel`).
- `UnitManager` notifica revelacao na fog quando unidade inimiga volta a ficar visivel.

### 3) Vitoria/Derrota e encerramento de partida
- `MatchController` recebeu:
  - `DeclareTutorialVictory(...)`,
  - `DeclareTutorialDefeat(...)`,
  - deteccao de derrota por ficar sem unidades (turno >= 2), inclusive por evento de destruicao.
- `Panel_endGame` passa a ser controlado explicitamente para abrir/fechar com texto apropriado.
- `MatchMusicAudioManager` agora pode parar musica permanentemente no fim da partida.
- `CursorController` ganhou SFX dedicados de vitoria/derrota e consolidacao de metodos de UI SFX.

### 4) Ajuda contextual e UX de tutorial
- `HelpManager` adiciona modo tutorial ativo por `TutorialData` e gerencia hints aprendidos por time.
- Hints contextuais por hover foram conectados ao estado neutro do cursor com delay (`HoverHelpDelay`).
- `PanelDialogController` ganhou checagem de texto fixo ativo para evitar sobrescrita indevida.
- Sistema de placeholders de dialogo foi melhorado para aceitar variacoes de case (`<key>`, `<KEY>`, `<Key>`), refletido em `DialogCatalog`, `DialogDatabase` e `PanelDialogController`.
- `PanelTutorialController` mostra checklist de objetivos com estado visual (pendente, concluido, falho) e toggle via F6.

### 5) Melhorias de painel de informacoes e visual
- `TurnStateManager.HelperPanel` foi expandido para mostrar secoes detalhadas:
  - armas,
  - transporte,
  - servicos,
  - suprimentos.
- `PanelHelperController` passou a colorir dados pelo time do assunto inspecionado (nao apenas time ativo).
- `PanelRemainingController` agora exibe tambem o cap maximo de unidades (`text_cap`).
- `PanelVisibilityHotkeysController` ganhou evento/hotkey de toggle do painel de tutorial (F6).

### 6) Ajustes no planning e conteudo de jogo
- `PlanningManager` recebeu:
  - ajuste do offset visual de rally flag,
  - flag de rally fixada na layer `SFX`,
  - aumento de limites de guarda em buscas de caminho (melhor robustez em mapas maiores).
- Conteudo novo para tutorial:
  - catalogos e assets de unidades/construcoes,
  - dialogos de hints e vitoria,
  - cenas `Assets/Scenes/Tutoriais/...` (3 historias),
  - audios de derrota/vitoria,
  - atualizacoes de tiles, fontes e dados de combate.

## Estado antes do proximo passo
- Base de tutorial jogavel e integrada ao loop principal.
- Objetivos ja reagem a eventos de partida e suportam condicoes de falha.
- Encerramento de partida (vitoria/derrota) ficou consistente para tutorial e partida normal.
- Proximo passo natural: validar cada cena de tutorial ponta a ponta (objetivos, hints, spawn e condicoes de derrota) e corrigir edge cases de balanceamento.
