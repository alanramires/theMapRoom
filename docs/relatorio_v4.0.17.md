# v4.0.17 - AI Supridor Fixes

Esta versão concentra correções e melhorias no fluxo de **Suprimento** (Supridor) e no `panel_helper`, deixando a seleção de alvos mais clara para o jogador e mais fiel na apresentação ao vivo da IA. Também inclui ajustes no fluxo de **Fusão**.

## Suprir — seleção de candidatos

- Candidatos **inválidos** do mesmo time agora aparecem na lista em **cinza**, com o motivo, em vez de sumirem sem explicação.
- Candidatos de **outro time** não entram mais na lista de suprimento.
- Navegação por teclado (setas + Enter) percorre candidatos válidos, inválidos, `EXECUTAR FILA` e `CANCELAR`, com foco circular, cursor sincronizado e feedback sonoro.
- Selecionar um candidato inválido mostra o motivo e toca o som de erro, sem travar o fluxo.
- Clique do ponteiro seleciona diretamente o alvo suprido no mapa e no painel.

## Suprir — passo de confirmação

- O passo **CONFIRMAR SUPRIMENTO** deixou de empilhar informações como botões: consumo e carroceria agora vêm em **texto** no corpo do painel.
- O `ADICIONAR À FILA` virou **botão de rodapé**, acima do `CANCELAR`, no mesmo padrão do `CONFIRMAR ATAQUE` — assim o jogador não confunde linhas informativas com ações e enxerga a ação real.
- O **Consumo do Supridor** lista apenas os recursos realmente gastos. Reparo só de HP mostra somente Peças; Galões/Munição não aparecem quando não são consumidos.
- Removido o cabeçalho enganoso que somava tipos diferentes de recurso e os rotulava como "GALÕES".

## Fusão

- A lista de fusão traz **apenas unidades idênticas do mesmo time**, válidas e inválidas.
- Vizinhos de outro time ou de tipo diferente não entram mais na lista (nem como opção cinza).
- Candidatos inválidos permanecem em **cinza** de forma estável, sem repintar com a cor do time a cada frame.

## Apresentação ao vivo da IA

- No modo de apresentação normal, a IA percorre a lista de suprimento pelo mesmo foco navegável do jogador antes de confirmar o alvo.
- A execução da fila de suprimento respeita os delays de apresentação e evita fallback de duplo-confirme quando a execução já está em andamento.
- Mensagens de sensor mais curtas (ex.: "Unidade decolou recentemente.") para caber melhor no painel.

## Validação

- `Assembly-CSharp.csproj`: compilação a ser confirmada no Editor (mudanças de UI exigem verificação em Play mode).
- Fluxos de Suprir (seleção, inválidos, confirmação, fila) e Fusão a validar no Editor.
