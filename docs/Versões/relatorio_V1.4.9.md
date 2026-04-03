# Relatorio de Atualizacao - v1.4.9

## Em uma frase
A versao v1.4.9 consolidou a base da nova tela de entrada e preparou o ecossistema de mensagens/dados que sustenta load, cinematic, quit e fluxo de tutorial.

## O que isso trouxe na pratica
- Estrutura inicial da tela de entrada ficou funcional para navegar entre menu principal, load e cinematic.
- Sistema de mensagens (dialog/helper/tutorial) foi ampliado para reduzir texto hardcoded e padronizar feedback em UI.
- Save/Load e componentes de menu receberam ajustes de integracao para sustentar o refactor que seria fechado no v1.4.10.

## Principais melhorias
1. Tela de entrada em transicao de arquitetura
- Cena `Tela de Entrada` foi atualizada com novo arranjo de paineis e fluxo de interacao.
- Scripts de menu (`PanelMenu`, `MainMenuLoadPanelController`, `MainMenuCinematicController`, `MainMenuKeyboardController`) foram ajustados em conjunto.
- Resultado percebido: base de navegacao mais organizada para o refatoramento final por state machine.

2. Expansao do catalogo de mensagens
- Grande pacote novo em `Assets/DB/Messages` para `Dialog Data`, `Helper Data`, `Tutorial Data` e `Automata Data`.
- Entradas adicionadas para load/save, quit do menu principal, replay, hotkeys, sensores, transferencias, fusao, supply e servicos de comando.
- Resultado percebido: textos de feedback mais padronizados e facil manutencao por asset.

3. Load/Save e feedback de UI
- `SaveGameManager` e `DialogManager` receberam ajustes para trabalhar com novos textos e respostas de fluxo.
- Mensagens de load/delete/confirmacao passaram a existir como dados dedicados no banco de dialogos.
- Resultado percebido: fluxo de slot com melhor previsibilidade de prompts e mensagens de status.

4. Tutorial e suporte de conteudo
- Base de dados de tutoriais e assets de historia foram atualizados/adicionados.
- `MatchController` e scripts correlatos receberam ajustes para alinhamento com novas mensagens e estados auxiliares.
- Resultado percebido: continuidade entre menu de entrada e conteudo de tutorial com menos lacunas de UX.

## Bloco tecnico curto
- Principais scripts alterados na versao:
  - `Assets/Scripts/UI/KeyboardManager.cs`
  - `Assets/Scripts/UI/MainMenuKeyboardController.cs`
  - `Assets/Scripts/UI/PanelMenu.cs`
  - `Assets/Scripts/UI/MainMenuLoadPanelController.cs`
  - `Assets/Scripts/UI/MainMenuCinematicController.cs`
  - `Assets/Scripts/Save/SaveGameManager.cs`
  - `Assets/Scripts/UI/DialogManager.cs`
  - `Assets/Scripts/UI/PanelDialogController.cs`
  - `Assets/Scripts/Match/MatchController.cs`
- Cenas e configuracoes atualizadas:
  - `Assets/Scenes/Tela de Entrada.unity`
  - `Assets/Scenes/Battle Map.unity`
  - `ProjectSettings/EditorBuildSettings.asset`
- Documentacao original da versao: `docs/V1.4.9.md` (na linha atual de desenvolvimento, reorganizada para `docs/relatorio_V1.4.9.md`).

## Resultado
A v1.4.9 foi uma versao de consolidacao e preparacao: deixou a tela de entrada operavel e montou a infraestrutura de mensagens/dados que permitiu fechar a estabilidade no v1.4.10.
