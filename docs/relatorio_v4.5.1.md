# v4.5.1 — Refactor de Mudança de camada 1/5

## Objetivo

Fechar a escada naval de submersão, consolidando em um único sensor as regras
para a transição de `Naval/Surface` para `Submarine/Submerged`.

## Pode Submergir autoritativo

- Criado o `PodeSubmergirSensor`.
- O sensor valida se a unidade está em `Naval/Surface` e suporta
  `Submarine/Submerged`.
- Unidades embarcadas ou que dispararam na rodada não podem submergir.
- Locks de camada causados por disparo, dano ou emersão forçada continuam
  prevalecendo.
- A detecção confirmada por um oponente mantém a unidade exposta na superfície.
- A consulta pode avaliar o hex atual ou um hex fornecido pelo consumidor, sem
  mover a unidade.

## Hex e ocupação

- A banda `Submarine/Submerged` precisa estar livre para encerrar a transição.
- Construções precisam permitir a camada e respeitar skills obrigatórias e
  bloqueadas.
- Estruturas são avaliadas em conjunto com o terreno do hex.
- Terrenos sem suporte à camada submersa rejeitam a operação.
- Modos adicionais de terreno, estrutura e construção continuam válidos.
- Regras que forçam emersão em construção, estrutura combinada com terreno ou
  terreno impedem a submersão, mesmo quando o hex também declara suporte à
  camada submersa.

## Integração com TurnState

- O menu de mudança de camada do jogador passou a consultar o
  `PodeSubmergirSensor`.
- A decisão é revalidada imediatamente antes da aplicação da transição.
- O comando `SUBMERGE` de debug usa o mesmo relatório.
- A preferência naval automática após movimento também passa pelo sensor.
- O roteador genérico de camada do `TurnStateManager` encaminha transições
  `Naval/Surface → Submarine/Submerged` ao sensor, cobrindo consumidores como
  suprimento e fusão.

## Ferramenta Pode Submergir

- A janela `Tools > Operações Navais > Pode Submergir` deixou de reimplementar
  regras.
- A ferramenta apenas monta o contexto, chama o sensor e apresenta a
  explicação retornada.
- A consulta permanece pura e não confirma nenhuma ação.

## Arquitetura transacional

- O sensor não move unidades nem altera camada, ocupação, locks, detecção, FOW
  ou caches confirmados.
- A preferência naval aplicada durante o fluxo de movimento continua
  restaurável no rollback.
- A mudança definitiva permanece vinculada ao compromisso explícito da ação e
  ao retorno a `CursorState.Neutral`.
- Detecção e demais janelas de exposição são apenas consultadas; nenhuma
  informação confirmada é produzida pela prévia.

## Verificação

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- Resultado: builds concluídos com 0 erros.
- Implementação atual do refactor: `1/5`.
