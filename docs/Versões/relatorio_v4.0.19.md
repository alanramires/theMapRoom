# v4.0.19 - Ajustes para a versão web

Esta versão foca em **acessibilidade de entrada multiplataforma** — deixar o jogo bem jogável só com **mouse** (web), com **controle remoto de TV** (Fire TV / Android TV) e com **gamepad**, além de corrigir um som que faltava no menu de carregar. Passos rumo à publicação da versão web.

## Botão direito como ESC (mouse)

- O **clique direito curto** (sem arrastar) já agia como cancelar no gameplay; agora também **abre o menu do mapa** em Neutral e **fecha/volta** com o menu aberto — igual ao ESC, para quem joga só de mouse.
- **Arrastar** o botão direito continua fazendo **pan** da câmera; a distinção tap/arrasto é por deslocamento em pixels.
- Na **Tela de Entrada**, o clique direito também passou a agir como ESC: fecha o Carregar Jogo, volta dos submenus e abre a confirmação de sair no menu raiz — entrando no mesmo fluxo do ESC, sem caminho novo.

## Suporte a controle remoto de TV e gamepad

- Novo utilitário `RemoteInput`, sempre aditivo (nunca substitui teclado/mouse), centralizando **Confirmar** (Select/OK, botão A do gamepad) e **Cancelar** (Voltar/Back, botão B).
- No **Fire TV / Android TV**, o botão **Voltar (Back)** chega como `KeyCode.Escape` no Input legacy. Vários helpers, no ramo do Input System novo, retornavam antes de checar o legacy — então o Back era ignorado e o Android podia encerrar o app. Agora todos consultam o `RemoteInput` antes, garantindo que o Back seja consumido como cancelar/voltar **em todos os estados** (gameplay, menu do mapa, menu inicial, carregar jogo).
- O **Select/OK** do remote e o **botão A** do gamepad passam a confirmar nesses mesmos pontos.

## Som de cancelar no Carregar Jogo

- Na Tela de Entrada, apertar **ESC** no painel Carregar Jogo não tocava o `cancel.mp3`: o `MainMenuStateController` fechava o painel via `ChangeState` direto (silencioso), enquanto o botão "Voltar" usava `CloseLoadPanel` (com som).
- O ESC agora passa pelo mesmo `CloseLoadPanel`, tocando o `cancel.mp3`. A transição de estado é idêntica.

## Validação

- `Assembly-CSharp`: compilação a ser confirmada no Editor.
- A validar: no **navegador/mouse**, clique direito abrindo/fechando menus (mapa e tela inicial) e o arrasto ainda fazendo pan; no **Fire TV**, o botão Voltar cancelando/voltando sem fechar o app e o Select confirmando; e o `cancel.mp3` no ESC do Carregar Jogo. O mapeamento exato do Select no Fire TV pode precisar de ajuste fino no device.
