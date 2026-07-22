# v4.0.19c - Ajustes para a versão web parte IV

Quarta leva de preparativos para a **versão web (WebGL)** e **mobile**: navegação da câmera por **toque**, controles de **loja** amigáveis ao dedo e o botão de **tela cheia** integrado ao menu inicial.

## Pan da câmera por toque

- **Um dedo arrastando** agora faz **pan** do mapa (equivalente ao arrasto do botão direito/meio no desktop).
- **Dois dedos** não fazem pan — ficam reservados para a **pinça de zoom** (parte II), sem conflito.
- O arrasto que **começa sobre a UI** (painel de ajuda, botões, qualquer `Selectable`/elemento clicável) **não** move a câmera: `IsTouchOverInteractiveUI` bloqueia o pan naquele toque, para não brigar com a interface.
- Implementado nos dois backends (Input System novo via `Touchscreen`, legacy via `Input.GetTouch`), mantendo o mesmo `panSpeed` do pan de mouse.

## Controles de toque na loja

- O `Panel_dialog` ganhou controles de **loja** navegáveis por toque: **anterior / próximo / comprar / sair** e o **contador**, resolvidos e vinculados em runtime (`shopping_touch_controls`).
- Assim a compra de unidades funciona no navegador/celular sem depender de teclado.

## Tela cheia no menu inicial

- O botão de **tela cheia** foi integrado ao `PanelMenu`: entra na navegação por teclado/controle, toca o som de confirmar e chama `FullscreenShortcutButton.ToggleFullscreen()`.
- Auto-resolução do botão por nome (`button_TelaCheia` / `tela_cheia` / `fullscreen`) e binding em runtime.

## Cenas e prefabs

- `Battle Map 1 - Ground` e `Tela de Entrada` atualizadas; `Panel_dialog` com os novos controles de loja por toque.

## Validação

- `Assembly-CSharp`: compilação a ser confirmada no Editor.
- A validar: no **navegador/celular**, arrastar um dedo movendo a câmera (e não movendo quando começa sobre UI), a pinça ainda dando zoom, os botões de loja por toque comprando/navegando, e o botão de tela cheia no menu inicial.
