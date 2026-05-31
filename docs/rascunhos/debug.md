# Debug Commands

Comandos disponiveis no `panel_debug`.

## Como enviar

- Digite o comando na caixa de texto.
- Pressione `Enter` ou clique em `Enviar`.
- Comandos sao case-insensitive.

## Comandos principais

- `help`
  - Mostra resumo dos comandos no helper (e no console).

- `destroy unit`
  - Destroi a unidade sob o cursor.

- `wake unit`
  - Reativa a unidade sob o cursor (`HasActed = false`).

- `wake all units`
  - Reativa todas as unidades do time ativo.

- `land unit`
  - Pousa a unidade selecionada (ou sob o cursor) em `Land/Surface` usando o sensor `PodePousar`. Nao consome acao.

## Unidade (HP, autonomia, municao, movimento)

- `set hp <valor>`
  - Define HP atual da unidade sob o cursor.

- `repair unit`
  - Restaura HP para o maximo.

- `set autonomy <valor>`
- `set autonomi <valor>` (alias legado)
- `set fuel <valor>` (alias)
  - Define autonomia atual da unidade sob o cursor.

- `refuel unit`
  - Restaura autonomia para o maximo.

- `set ammo <valor>`
  - Atalho para arma `#1`.

- `set ammo:<indice> <valor>`
  - Define municao da arma informada.

- `rearm unit`
  - Recarrega todas as armas para o maximo.

- `set move_remain <valor>`
- `set move remain <valor>` (alias)
  - Define movimento restante atual.

## Estoque logistico (reserva)

- `set galao <valor>`
- `set galoes <valor>` (alias)
  - Define estoque de gasolina.

- `set caixas <valor>`
  - Define estoque de caixas de municao.

- `set pecas <valor>`
  - Define estoque de pecas.

## Construcao e time

- `set construction team <valor>`
  - Define o time da construcao sob o cursor.

- `set owner <valor>` (alias de `set construction team`)
  - Aceita `-1` neutro, `0` verde, `1` azul, `2` vermelho, `3` amarelo.

- `set capture points <valor>`
  - Define capture points da construcao sob o cursor.

- `set active team <valor>`
  - Forca o time ativo no match sem avancar turno.

## Spawn e economia

- `spawn <unit>`
  - Spawna no cursor para o time ativo.

- `spawn:<team> <unit>`
  - Spawna no cursor para time especifico (`0..3`).

- `set money <valor>`
  - Define dinheiro do time ativo.

- `set money:<team> <valor>`
  - Define dinheiro do time informado (`0..3`).

- `set economy on`
- `set economy off`
- `set economy true|false|1|0`
  - Liga/desliga economia.

## Altitude/camada

- `change altitude <dominio>/<altura>`
  - Dominios: `land`, `naval`, `submarine`, `air`.
  - Alturas: `surface`, `submerged`, `low`, `high`.

- `land unit`
  - Pousa em `Land/Surface` via sensor (ver secao "Comandos principais").

- `landing`
- `emerge`
- `submerge`
- `take off`
- `fast take off`
  - Atalhos de altitude.

## Fog of War

- `fow on|off|true|false|1|0`
- `fog of war on|off|true|false|1|0` (alias)
  - Liga/desliga FoW em debug.

## Observacoes

- A maioria dos comandos exige unidade sob o cursor.
- Comandos de construcao exigem construcao sob o cursor.
- `set money`, `set economy` e `fow` dependem de `MatchController`.
