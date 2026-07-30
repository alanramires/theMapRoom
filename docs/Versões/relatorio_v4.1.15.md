# v4.1.15 - Fow Partial por slot e desembarque em fow

Esta versão corrige arestas remanescentes da migração do Fog of War para `SlotId`, separa a verdade privada usada pela IA da perspectiva visual apresentada ao jogador e garante a atualização do FOW após compras e desembarques.

## FOW e memória por slot

- A coleta de visão comum e especializada passa a selecionar observadores por `SlotIndex`.
- Snapshots de gameplay, memória de terreno explorado e memória de construções são consultados pelo slot observador.
- Chaves e revisões do cache de visão deixam de usar `TeamId` como índice lógico.
- Restauração do cache de FOW valida o slot ativo.
- Visibilidade de unidades amigas, apresentação em hexes empilhados e notificações de revelação passam a distinguir participantes que compartilham a mesma cor.
- `TeamId` permanece responsável pela identidade visual; `SlotId` governa visão, memória e ownership.

## FOW ON e FOW PARTIAL

- `fow on` mantém a perspectiva visual do jogador humano sem entregar compras e unidades ocultas do oponente.
- Durante o turno da IA, sua percepção privada continua sendo calculada e publicada em modo de dados, independentemente do overlay humano.
- `fow partial` acompanha a IA do slot ativo, em vez de selecionar sempre a primeira IA configurada.
- A apresentação das ações automáticas compara o slot observador correto.

## Compras da IA sob FOW

- A abertura automatizada da loja usa `ActiveSlotId`, não o valor cosmético de `ActiveTeamId`.
- A IA pode utilizar seus próprios produtores mesmo quando o hex está oculto para o observador humano.
- O fallback visual do cursor deixa de determinar se a IA conhece ou pode acessar sua própria construção.
- Compras confirmadas solicitam uma reconstrução completa do FOW ao retornar a `Neutral`, evitando divergência entre o cálculo direto de LOS e o overlay publicado.

## Desembarque e visão

- O desembarque é tratado como uma alteração confirmada multiunidade.
- Passageiros desembarcados e transportador deixam de disputar uma única referência de atualização incremental.
- Ao concluir o desembarque e retornar a `Neutral`, uma única reconstrução completa incorpora posição, camada e visão de todos os envolvidos.
- Movimentos comuns continuam usando o caminho incremental otimizado.

## Contrato transacional

- Compras e desembarques não publicam visão definitiva durante estados provisórios.
- A atualização completa fica pendente até a máquina de estados retornar a `CursorState.Neutral`.
- Cancelamentos não alimentam memória explorada, intel ou caches confirmados.
- A mesma separação entre verdade confirmada e apresentação visual vale para humano, IA e replay.

## Conteúdo adicional do point save

- Ajustes em planejamento, composição e demanda de compras da IA.
- Presets de IA e conteúdo de desenvolvimento associados ao mapa `Quadrado`.
- Atualizações de menu, cena de desenvolvimento e ativos de fontes presentes no workspace.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem 248 avisos não bloqueantes no runtime.

## Verificações recomendadas

- Comprar uma unidade com `fow on` e confirmar que seu alcance aparece imediatamente após o retorno a `Neutral`.
- Comparar `fow on` e `fow partial`: a decisão da IA deve ser idêntica, mudando apenas a perspectiva visual.
- Desembarcar um ou vários passageiros e confirmar que todos contribuem visão.
- Testar dois slots com o mesmo `TeamId` para confirmar memórias de FOW independentes.
