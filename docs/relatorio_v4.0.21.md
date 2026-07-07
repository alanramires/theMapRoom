# v4.0.21 - AI Easy

Esta versão introduz o modo Easy da AI e completa a apresentação do turno inimigo no Fog of War Total.

## AI Easy

- Novo modo `Easy Mode` no AI Manager.
- A AI recebe apenas `1/3` da renda normal de construções que não sejam cidades.
- HQ de `$3000` passa a render `$1000`; construções de `$1500` passam a render `$500`.
- Cidades continuam rendendo o valor integral de `$1000`.
- A redução vale apenas para jogadores controlados pela AI; jogadores humanos mantêm a renda integral.
- `ConstructionData` passa a identificar cidades explicitamente.

## Fog of War Total

- A visão apresentada durante o turno da AI permanece baseada nos sensores do jogador humano.
- Cursor, câmera, painéis, áreas, rastros e linhas auxiliares deixam de revelar ações ocultas.
- Sprites, HUDs e projéteis são atualizados durante movimento, combate, embarque, desembarque, fusão, suprimento, transferência e Serviço do Comando.
- Linhas de tiro de atacantes ocultos aparecem somente ao entrar em área visível; atacantes detectados mantêm a linha completa.
- `fow partial` preserva a visualização de depuração anterior e `fow off` restaura a apresentação integral.
- Fora do preset `FogOfWarTotal`, o campo de batalha e o contraste entre unidades e construções permanecem no comportamento original.

## Proteção de informação da AI

- Shopping oculto não reproduz `cursor.mp3` nem `done.mp3`.
- Saldo, renda, variações econômicas, tesouro e quantidade de unidades da AI ficam ocultos.
- O indicador `TURNO DA IA` permanece fixo durante compras e só libera a visão durante batches de ação do Stage 2.

## Interface mobile

- Botões gerados pelo `panel_helper` passam a usar altura `80`.
- Fonte VT323 usa tamanho máximo `60`, com redução automática até `32` para textos longos.
- O botão `CANCELAR` permanece separado no rodapé ou como último item das listas.

## Validação

- `Assembly-CSharp.csproj`: build sem erros.
