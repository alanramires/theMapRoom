# v4.1.12 - Aertas aparadas, agora SlotID governa os sistemas principais, TeamID os cosméticos

Esta versão conclui a oitava etapa do refactor de identidade dos participantes. As APIs ambíguas que usavam `TeamId` para identificar jogadores foram removidas ou migradas para `PlayerSlotId`. A partir deste ponto, o slot governa os sistemas principais da partida, enquanto `TeamId` permanece como identidade visual, cor e compatibilidade controlada com dados antigos.

O cenário de referência continua sendo:

- `slot 0`: azul;
- `slot 1`: amarelo;
- `slot 2`: vermelho;
- `slot 3`: vermelho.

Os slots 2 e 3 devem funcionar como participantes completamente independentes, mesmo compartilhando a mesma apresentação vermelha.

## APIs e identidade

- APIs de economia, turno, vitória, ownership, planejamento, FOW, inteligência, replay e IA passaram a receber `PlayerSlotId`.
- Sobrecargas públicas ambíguas baseadas em `TeamId` foram removidas dos fluxos de identidade.
- Conversões de cor para slot somente permanecem em caminhos explícitos de compatibilidade e exigem associação única.
- Quando uma cor pertence a mais de um slot, o sistema não escolhe silenciosamente o primeiro participante.
- `TeamId` permanece válido para sprites, cores, nomes, orientação visual e importação de dados legados.

## IA, planejamento e setores

- Snapshots da IA são construídos para o slot exato.
- Planos, intenções de setor, operações táticas e registros de inteligência são indexados por slot.
- Distâncias até HQ, fábricas, transportes e setores usam o slot observador.
- Eixos de invasão e estados de Go Green são independentes por slot.
- Rally, handoff, defesa, captura, logística, reparo, transporte e shopping deixam de compartilhar estado entre participantes da mesma cor.
- O limiar adaptativo de transporte agora possui API explícita por slot.

## Fog of War, sensores e ameaça

- Consultas de visibilidade recebem o slot observador.
- Memória de inteligência e contatos conhecidos são separados por slot.
- Revisões e invalidações do cache de ameaça são indexadas por slot.
- Sensores e HUD consultam a revisão correspondente ao observador exato.
- Dois slots visualmente vermelhos não compartilham visão, detecção, cache ou inteligência.

## Estatísticas e painel de turno

- `MatchStatsManager` passou de dicionário por `TeamId` para dicionário por slot.
- Compras, perdas, eliminações, dano, captura, logística e território são contabilizados para o slot correto.
- A reconstrução de estatísticas pelo histórico interpreta os identificadores de jogador como slots.
- O painel de turno consulta as estatísticas de cada posição separadamente.
- Dois participantes com a mesma cor não fundem mais contadores nem placar.

## Ocupação e ownership

- A regra de ocupação por participante no modo Total War compara `SlotIndex`.
- Unidades da mesma cor, mas de slots diferentes, não são tratadas automaticamente como pertencentes ao mesmo jogador.
- Setores e construções expõem e propagam o slot controlador.
- Ownership visual continua derivado da cor configurada para o slot.

## Save, load e replay

- O load restaurado na etapa anterior foi mantido e conferido junto às novas APIs.
- Estado ativo, ownership, unidades, construções, economia, IA, FOW e observador continuam sendo restaurados por slot.
- Replay seleciona participante e visão por slot.
- Campos legados de `TeamId` continuam disponíveis somente para compatibilidade visual e migração inequívoca.

## Contrato transacional

O refactor preserva o contrato de ações transacionais. Nenhuma mudança definitiva de FOW, sensores, inteligência, ocupação, recursos, combustível, munição, HP, captura ou `HasActed` foi deslocada para estados provisórios. O estado confirmado continua sendo recalculado somente após o compromisso explícito da ação e o retorno a `CursorState.Neutral`.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem 246 avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- Scan residual confirmou a remoção das APIs ambíguas principais por `TeamId`.
- Usos restantes de `TeamId` correspondem a apresentação, dados visuais ou compatibilidade explicitamente validada.

## Verificações recomendadas

- Executar uma partida com dois slots vermelhos e confirmar turnos independentes.
- Validar estatísticas, unidades restantes e território separadamente para cada slot vermelho.
- Confirmar FOW parcial e detecção diferentes para os dois observadores vermelhos.
- Salvar e carregar durante o turno de cada slot repetido.
- Validar replay, shopping e fases da IA para cada participante de mesma cor.
