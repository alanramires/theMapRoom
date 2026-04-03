# Relatorio de Atualizacao - v1.4.10

## Em uma frase
A tela de entrada foi reorganizada em uma maquina de estados unica para estabilizar os fluxos de Load, Cinematic e Sair com navegacao consistente por teclado.

## O que isso trouxe na pratica
- Transicoes previsiveis entre menu raiz e subpaineis, sem depender de listeners dispersos.
- Fluxo de Load mais confiavel para abrir, navegar, voltar e confirmar/cancelar sem vazamento de input.
- Cinematic e Sair passaram a seguir o mesmo contrato de estado do menu principal.

## Principais melhorias
1. Orquestracao por estado no menu principal
- Novo `MainMenuState` com estados explicitos: `Neutral`, `RootMenu`, `NewGame`, `LoadMenu`, `Tutorial`, `Config`, `Cinematic`, `Exit`.
- Novo `MainMenuStateController` centraliza `Enter/Exit` de cada estado e o roteamento de input.
- Resultado percebido: menos ambiguidade de fluxo e menos conflitos entre scripts de UI.

2. Fluxo de Load integrado ao state machine
- `MainMenuLoadPanelController` ganhou entrada/saida state-driven (`EnterLoadMenu`/`ExitLoadMenu`) e sincronizacao com `MainMenuStateController`.
- Fechamento do load retorna ao estado correto (`RootMenu`) sem "pular" etapas.
- Navegacao no load recebeu ajuste para feedback sonoro de cursor durante movimento vertical/horizontal.

3. Cinematic e controle de visibilidade
- `MainMenuCinematicController` foi alinhado ao controlador de estado para entrada/saida de cinematic.
- Visibilidade de `Panel_Menu` e overlay de video ficou consistente durante playback e cancelamento.
- Resultado percebido: transicao de cinematic sem deixar UI em estado invalido.

4. Fluxo de Sair (confirmacao)
- `PanelMenu` passou a trabalhar com estado `Exit` e confirmacao controlada pelo fluxo central.
- Tratamento de confirm/cancel ficou consistente com bloqueio de vazamento de input entre quadros.
- Resultado percebido: opcao Sair responde de forma previsivel no teclado.

## Bloco tecnico curto
- Scripts principais alterados:
  - `Assets/Scripts/UI/MainMenuState.cs`
  - `Assets/Scripts/UI/MainMenuStateController.cs`
  - `Assets/Scripts/UI/PanelMenu.cs`
  - `Assets/Scripts/UI/MainMenuLoadPanelController.cs`
  - `Assets/Scripts/UI/MainMenuCinematicController.cs`
- Cena e dados de suporte atualizados:
  - `Assets/Scenes/Tela de Entrada.unity`
  - mensagens de dialogo de quit/delete
  - ajustes de assets de fonte usados na tela
- Organizacao de documentacao:
  - consolidacao do `docs/relatorio_V1.4.9.md`
  - novo `docs/relatorio_v1.4.10.md`

## Resultado
A versao v1.4.10 fecha uma base estavel para a tela de entrada, com fluxo unificado de estados e comportamento consistente nos caminhos de Load, Cinematic e Sair.
