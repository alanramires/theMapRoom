# v4.0.18 - Preparativos pro MVP

Esta versão reúne melhorias de **qualidade de vida**, uma nova **sinalização visual de estoque do supridor**, correção de um **travamento no modo de inspeção** e o conserto do **carregamento da cena inicial** — passos de preparação rumo ao MVP.

## Sinalização de estoque do supridor

- Unidades supridoras (`isSupplier`) agora exibem uma **pilha de alerta** de estoque baixo no HUD, em três slots: `supply_top`, `supply_middle`, `supply_bottom`.
- Cada suprimento entra na pilha quando seu estoque cai a **50% ou menos** do baseline (`supplierStockAlertThreshold`).
- A pilha **enche de baixo pra cima**: o suprimento mais crítico (vazio primeiro, depois menor percentual) ocupa o `supply_bottom`, junto do campo visual das barras de autonomia/munição.
- Os ícones de alerta (metade/vazio) vêm do próprio **`SupplyData`** (`spriteDefault` / `spriteHalf` / `spriteEmpty`), eliminando qualquer casamento por nome. Se o sprite específico não estiver preenchido, cai no `spriteDefault`.
- Unidades **não supridoras nunca exibem** os slots — o gate é por `isSupplier` e a visibilidade controla o `SetActive` do objeto (um `Image` sem sprite mas habilitado desenharia um quadrado branco). Os slots já nascem inativos no prefab.

## Inspeção — correção de travamento

- Ao inspecionar uma unidade/construção e deixar o **tempo limite** expirar, o estado ficava preso em `InspectingUnit`/`InspectingBuilding` e o jogo travava.
- Agora, quando o helper de inspeção expira, o cursor **volta para Neutral** (mesmo caminho do clique fora / tecla), em vez de apenas limpar o painel.
- O retorno é escopado (`hadHelper && IsInspectingState()`) para **não afetar** a camada de ameaça (`InspectingHotZone`), que não usa helper e tem dismiss próprio.

## Comodidade de controles

- **Botão direito curto = ESC/Cancelar.** Um clique direito rápido (sem arrastar) age como cancelar; **arrastar** o botão direito continua fazendo o **pan** da câmera. A distinção é por deslocamento em pixels, relativo à tela.
- **Scroll sobre o `panel_helper` não dá zoom no mapa.** Quando o ponteiro está sobre o painel de ajuda, a roda do mouse é ignorada pela câmera (deixando o painel rolar seu conteúdo) em vez de dar zoom.
- **Zoom por pinça (dois dedos) para tablet.** Aproveitando a lógica de zoom já existente: afastar os dedos dá zoom in, aproximar dá zoom out, com âncora no ponto médio entre eles. Durante a pinça, o toque simulado não move mais o cursor por engano.

## Cena inicial — carregamento

- O botão **Novo Jogo** apontava para uma cena com nome desatualizado (`"Battle Map"`), causando erro de cena não encontrada.
- Nome corrigido para **`"Battle Map 1 - Ground"`** em `PanelMenu` e `NewGamePanelController`, alinhado ao arquivo da cena e ao Build Settings (que referencia por guid).

## Validação

- `Assembly-CSharp`: compilação a ser confirmada no Editor (mudanças de UI/input exigem verificação em Play mode).
- A validar no Editor: alertas de estoque em supridores (metade/vazio, ordem da pilha, ocultação em não-supridores), inspeção com timeout, botão direito como cancelar vs. pan, scroll sobre o painel, pinça no tablet e o fluxo de Novo Jogo.
