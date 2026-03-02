# Debug Commands

Comandos disponiveis no `panel_debug`.

## Como enviar

- Digite o comando na caixa de texto.
- Pressione `Enter` ou clique no botao `Enviar`.

## Comandos

- `destroy unit`
  - Destroi a unidade sob o cursor (com apresentacao visual/sonora de destruicao).

- `wake unit`
  - Reativa a unidade sob o cursor (`HasActed = false`).

- `set hp <valor>`
  - Define o HP atual da unidade sob o cursor.
  - Ex.: `set hp 1`

- `repair unit`
  - Coloca o HP da unidade sob o cursor no maximo.

- `set autonomy <valor>`
- `set autonomi <valor>`
  - Define a autonomia atual da unidade sob o cursor.
  - Ex.: `set autonomy 50`

- `refuel unit`
  - Recarrega a autonomia para o maximo da unidade sob o cursor.

- `set ammo <valor>`
  - Atalho para arma `#1`.
  - Ex.: `set ammo 2`

- `set ammo#<indice> <valor>`
  - Define municao/ataques da arma informada.
  - Ex.: `set ammo#1 2`, `set ammo#2 1`

- `rearm unit`
  - Recarrega todas as armas embarcadas para o maximo.

- `spawn <unit>`
  - Spawna unidade no cursor para o time ativo atual.
  - Aceita nome ou apelido.
  - Ex.: `spawn SD`

- `spawn:<team> <unit>`
  - Spawna unidade no cursor para o time especificado.
  - `team` valido: `0..3`.
  - Ex.: `spawn:0 SD`, `spawn:1 SD`

## Observacoes

- Quase todos os comandos exigem uma unidade sob o cursor.
- Comandos sao case-insensitive (`SPAWN SD` funciona igual a `spawn sd`).
