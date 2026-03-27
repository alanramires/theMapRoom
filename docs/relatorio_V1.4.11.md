# Relatorio de Atualizacao - v1.4.11

## Em uma frase
A versao v1.4.11 ajustou o menu do jogador na Battle Map, integrando abertura por ESC em neutral, navegacao por paineis, reuso dos atalhos de jogo e comportamento de dock semelhante ao panel_helper.

## O que isso trouxe na pratica
- ESC em neutral abre o menu do jogador sem quebrar o fluxo do tabuleiro.
- Navegacao entre `panel_menu`, `panel_options` e `panel_gerenciar` ficou consistente com sons de cursor/confirm/cancel.
- Acoes de menu reaproveitam os fluxos existentes de gameplay (X, R, N, U), evitando duplicacao de regra.
- O menu agora reposiciona com dock dinamico conforme proximidade do cursor, no mesmo estilo do panel_helper.

## Principais melhorias
1. Fluxo de abertura/fechamento por ESC
- `CursorController` passou a priorizar o tratamento do menu do jogador antes do bloqueio geral de input.
- Abertura ocorre apenas em estado `Neutral`, com restauracao da celula original do cursor ao fechar.
- Resultado percebido: ESC funcional como entrada do menu de jogador sem vazar para estados indevidos.

2. Navegacao e transicao entre paineis
- Implementado controlador dedicado para `MenuRoot` com foco inicial por painel e retorno de foco em botoes de contexto.
- ESC dentro dos paineis respeita hierarquia de retorno (`gerenciar -> options -> menu -> jogo`).
- Resultado percebido: navegacao previsivel e coerente com a UX definida para o menu.

3. Reuso de fluxos existentes (atalhos)
- `btn_comando` chama o fluxo de comando existente (atalho X).
- `btn_rodada` chama confirmacao de fim de turno (atalho R).
- `btn_minimapa` chama toggle de minimapa/zoom rapido (atalho N).
- `btn_destruir` chama fluxo de remover unidade (atalho U).
- Resultado percebido: um unico ponto de regra por acao, com menor risco de divergencia.

4. Dock dinamico do menu
- `menuRoot` ganhou comportamento de dock por proximidade do cursor, com histerese de entrada/saida.
- A referencia de proximidade foi ajustada para o painel ativo, evitando comportamento estranho por container amplo.
- Resultado percebido: menu mais legivel e menos intrusivo durante a navegacao no tabuleiro.

5. Isolamento do menu principal da Tela de Entrada
- `MainMenuStateController` foi restringido para inicializar apenas na cena `Tela de Entrada`.
- Resultado percebido: remove interferencia indevida do state machine do menu principal dentro da Battle Map.

## Bloco tecnico curto
- Scripts principais alterados:
  - `Assets/Scripts/UI/BattleMapMenuRootController.cs`
  - `Assets/Scripts/Cursor/CursorController.cs`
  - `Assets/Scripts/Match/TurnState/TurnStateManager.CommandService.cs`
  - `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
  - `Assets/Scripts/Camera/CameraController.cs`
  - `Assets/Scripts/Save/SaveGameManager.cs`
  - `Assets/Scripts/UI/MainMenuStateController.cs`

## Resultado
A v1.4.11 fecha o ajuste do menu do jogador na Battle Map com abertura por ESC em neutral, navegacao robusta por paineis, acoes integradas ao gameplay existente e dock dinamico alinhado ao panel_helper.
