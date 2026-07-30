# v4.3.4 - Refactor de FOW para AI 4/7

Esta versão conclui a quarta das sete etapas do refactor de Fog of War para
turnos executados por AI, jogador remoto, replay ou qualquer outra origem de
batches.

O objetivo desta etapa é aproveitar, no início de cada turno, a fotografia
quente de contribuições do observador, evitando recalcular a visão de todas as
unidades quando o estado das fontes permaneceu inalterado.

## Início de turno incremental

Ao ativar um slot, o controlador tenta restaurar seu runtime de contribuições e
reconciliá-lo com o tabuleiro confirmado.

A sincronização:

- acontece somente em `CursorState.Neutral`;
- preserva as células de unidades cujo `sourceStateHash` não mudou;
- recalcula unidades novas ou com estado de visão alterado;
- remove fontes de unidades inexistentes, mortas ou embarcadas;
- reconstrói as contribuições de construções como um subconjunto barato;
- republica visibilidade, memória e inteligência conforme o
  `FogUpdateContext`;
- limita a escrita visual ao `presentationSlot`.

## Rebase de unidades inalteradas

Uma revisão global pode avançar devido às ações de outro jogador sem modificar
a geometria de visão das unidades do observador.

Quando posição, slot, domínio, altura, embarque, alcance e tipo permanecem
iguais, a contribuição anterior é preservada e apenas a chave de cache é
rebaseada sobre a revisão confirmada atual. Assim, essas unidades não executam
novamente `CollectVisibleCells`.

## Reconciliação de construções

As construções continuam contribuindo para FoW e podem mudar de proprietário
durante outro turno. Como seu conjunto de visão é pequeno, suas fontes são
removidas e reconstruídas durante a reconciliação incremental.

Isso mantém corretas:

- a visão da própria célula;
- a revelação geográfica adjacente;
- as regras especiais de construções e quartéis-generais;
- mudanças de captura e propriedade.

## Mortes e contrato transacional

Uma unidade apresentada como destruída durante uma fila de animação não remove
imediatamente sua contribuição confirmada.

Se a notificação ocorrer fora de `Neutral`, o controlador marca uma
reconciliação pendente. A remoção efetiva acontece somente após o retorno a
`Neutral`, quando a unidade já deixou o tabuleiro confirmado.

Isso impede que apresentação provisória altere:

- FoW definitivo;
- memória de exploração;
- detecção;
- inteligência da AI;
- caches confirmados.

## Gameplay e apresentação separados

Quando o executor do turno e o observador visual são slots diferentes:

1. o slot executor é sincronizado em `DataOnly`;
2. o slot de apresentação é sincronizado em `FullVisual`;
3. somente a perspectiva local escreve nos Tilemaps;
4. cada observador mantém sua própria memória e inteligência.

O mesmo fluxo serve para AI local, jogador remoto, segundo jogador e replay.

## Fallback conservador

Se a fotografia necessária estiver ausente, inválida ou inconsistente, o fast
path é abandonado e o controlador executa
`RefreshFogOfWarForActiveTeam()`.

Portanto, a otimização não troca correção por desempenho.

## Diagnóstico

O novo log `[FoW][TurnStartCache]` informa:

```text
slot
activated
units.changed
units.unchanged
units.removed
cells.collected
constructions
constructions.removed
total
fallback
```

Em um turno quente, espera-se que a maior parte das unidades apareça em
`units.unchanged` e que `cells.collected` permaneça baixo.

## Documentação

O comportamento foi registrado em
`docs/arquitetura/fow_canais_visibilidade.md`.

## Alterações paralelas incluídas

Conforme solicitado por `git add .`, este marco também inclui o estado corrente
dos ajustes paralelos presentes no workspace.

## Validação

- `Assembly-CSharp.csproj` compilado com zero erros e zero warnings.
- `git diff --check` concluído sem erros.
- O caminho incremental possui fallback integral conservador.
- A publicação definitiva permanece restrita ao retorno a `Neutral`.
