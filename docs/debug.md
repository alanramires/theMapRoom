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

- `set galao <valor>`
  - Define o estoque atual de gasolina (galao) da unidade logistica sob o cursor.
  - Ex.: `set galao 12`

- `set caixas <valor>`
  - Define o estoque atual de caixas de municao da unidade logistica sob o cursor.
  - Ex.: `set caixas 8`

- `set pecas <valor>`
  - Define o estoque atual de pecas da unidade logistica sob o cursor.
  - Ex.: `set pecas 5`

- `set move_remain <valor>`
- `set move remain <valor>`
  - Define o movimento restante atual da unidade sob o cursor.
  - Ex.: `set move_remain 3`

- `refuel unit`
  - Recarrega a autonomia para o maximo da unidade sob o cursor.

- `set ammo <valor>`
  - Atalho para arma `#1`.
  - Ex.: `set ammo 2`

- `set ammo:<indice> <valor>`
  - Define municao/ataques da arma informada.
  - Ex.: `set ammo:1 2`, `set ammo:2 1`

- `rearm unit`
  - Recarrega todas as armas embarcadas para o maximo.

- `set construction team <valor>`
  - Define o time da construcao sob o cursor.
  - Faixa valida: `0..4` (`0` = neutral).
  - Ex.: `set construction team 1`

- `set capture points <valor>`
  - Define os capture points atuais da construcao sob o cursor.
  - Ex.: `set capture points 10`

- `spawn <unit>`
  - Spawna unidade no cursor para o time ativo atual.
  - Aceita nome ou apelido.
  - Ex.: `spawn SD`

- `spawn:<team> <unit>`
  - Spawna unidade no cursor para o time especificado.
  - `team` valido: `0..3`.
  - Ex.: `spawn:0 SD`, `spawn:1 SD`

- `set money <valor>`
  - Define o dinheiro atual do time ativo.
  - Ex.: `set money 5000`

- `set money:<team> <valor>`
  - Define o dinheiro atual do time informado.
  - `team` valido: `0..3`.
  - Ex.: `set money:2 8000`

- `set economy on`
- `set economy off`
- `set economy true|false|1|0`
  - Liga/desliga a economia da partida.
  - Ex.: `set economy off`

- `change altitude <dominio>/<altura>`
  - Altera a camada/altitude da unidade sob o cursor (debug).
  - Ex.: `change altitude air/high`
  - Dominios aceitos: `land`, `naval`, `submarine`, `air`.
  - Alturas aceitas: `surface`, `submerged`, `low`, `high`.

- `landing`
  - Atalho para `change altitude land/surface`.

- `emerge`
  - Atalho para `change altitude naval/surface`.

- `submerge`
  - Atalho para `change altitude submarine/submerged`.

- `take off`
  - Atalho para `change altitude air/low`.

- `fast take off`
  - Atalho para `change altitude air/high`.

## Observacoes

- A maioria dos comandos exige unidade sob o cursor.
- `set construction team` e `set capture points` exigem construcao sob o cursor.
- `set money` e `set economy` dependem de `MatchController`.
- Comandos sao case-insensitive (`SPAWN SD` funciona igual a `spawn sd`).
