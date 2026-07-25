# v4.2.0 - Refactor do Save/Load para SlotID parte 1/6

Esta versão registra o estado estável imediatamente anterior ao refactor do Save/Load e dos caches persistidos de Fog of War para identidade explícita por `PlayerSlotId`.

O runtime de FOW, detecção, memória e inteligência já foi migrado em grande parte para o slot observador. O próximo ciclo deve remover ambiguidades remanescentes de nomes e formatos legados que ainda falam em `TeamId`, especialmente em partidas nas quais dois slots usam a mesma cor.

## Marco pré-refactor

- O cache runtime de FOW pertence ao slot observador, mesmo onde campos legados ainda usam `teamId` no nome.
- Unidades contribuem para a visão do próprio `SlotIndex`.
- Construções também são fontes de visão: podem revelar geografia sem necessariamente conceder detecção de ocupantes nas células adjacentes.
- Memória de exploração e memória conhecida de construções já carregam identidade de slot.
- O cache de visão atual é exportado pelo save, mas sua restauração permanece desligada.
- O load continua executando um refresh frio e autoritativo do FOW após reidratar unidades e construções.

## Limitação conhecida do cache salvo

O formato atual persiste a contagem agregada de contribuidores por célula e a visibilidade resultante de unidades, mas não persiste a contribuição individual de cada fonte.

Somente o agregado não permite retirar com segurança a contribuição anterior quando uma unidade ou construção se move, é removida, embarca, muda de proprietário ou deixa de fornecer visão. Por isso, `TryRestoreFogRuntimeCacheFromSave` não deve ser conectado diretamente ao load no formato atual.

O próximo modelo deverá representar:

- o `PlayerSlotId` observador;
- a identidade e o tipo da fonte (`Unit` ou `Construction`);
- as células contribuídas por cada fonte;
- a diferença entre revelação geográfica e capacidade de detectar ocupantes;
- uma chave ou assinatura estável para validar a contribuição no load;
- fallback seguro para o refresh frio quando o cache estiver ausente ou incompatível.

O agregado por célula poderá então ser reconstruído a partir das contribuições validadas, evitando persistir duas verdades potencialmente divergentes.

## Otimização incremental do FOW

- O spawn comprometido de uma única unidade, como uma compra, deixou de forçar refresh completo de todas as unidades do slot.
- A nova unidade soma sua própria visão pelo caminho incremental e republica o snapshot de detecção.
- Mudanças multiunidade continuam usando refresh completo.
- Se o cache confirmado não estiver pronto, o fluxo incremental mantém o fallback para reconstrução integral.
- O processamento continua ocorrendo somente após compromisso e retorno ao estado `Neutral`.

## Interface

- A tecla `J` alterna o Jornal do Comandante.
- Com o relatório aberto, `J` fecha o painel.
- Em `Neutral`, havendo relatório disponível, `J` abre o painel.
- O atalho respeita o bloqueio de input durante o turno da IA.

## Conteúdo e cenário

- Ajustes nos presets e catálogos de IA, construções e estruturas.
- Atualização do cenário `Hot Seat 1 - Pvp`.
- Ajustes de dados da construção `barracks`.

## Contrato transacional

Este marco preserva a regra fundamental do tabuleiro: nenhuma informação definitiva de FOW, detecção, memória ou inteligência é publicada durante uma ação provisória. O save representa somente o snapshot confirmado, e qualquer futura restauração de cache deverá ocorrer depois da reidratação do estado definitivo e em `CursorState.Neutral`.

## Próxima etapa

Refatorar o Save/Load de FOW em etapas independentes:

1. tornar explícita a propriedade por `PlayerSlotId` e migrar campos legados;
2. separar revelação geográfica de detecção;
3. generalizar contribuições para unidades e construções;
4. persistir contribuições por fonte sem alterar inicialmente o comportamento do load;
5. comparar cache restaurado com cold refresh;
6. ativar o fast path com fallback integral e, posteriormente, restauração parcial.

## Refactor Save/Load por SlotID — etapa 1/6

A primeira etapa foi concluída sem ativar a restauração do cache runtime:

- o formato de save passou para a versão `15`;
- `fogObserverSlotIndex` substitui semanticamente `fogCacheTeamId`;
- `fogExploredCellsBySlot` substitui a coleção legada `fogExploredCellsByTeam`;
- saves até v14 migram o valor de `fogCacheTeamId` diretamente como índice de slot, pois esse campo já armazenava `ActiveSlotId.Value`;
- a migração nunca tenta reinterpretar esse valor como `TeamId`, preservando participantes distintos que compartilham a mesma cor;
- campos legados continuam presentes apenas para desserialização compatível e ficam vazios em saves novos;
- snapshots, exploração, memória de construções e cache ativo foram renomeados internamente para explicitar indexação por slot;
- a API legada de restauração permanece como ponte obsoleta para compatibilidade;
- o load continua descartando a fotografia runtime e executando o cold refresh confirmado.

Não houve mudança nas regras de visão, detecção ou compromisso transacional nesta etapa.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem avisos não bloqueantes já existentes no projeto.
- `git diff --check` identificou espaços finais em arquivos YAML gerados pelo Unity; eles foram preservados neste snapshot para não reescrever dados de cena e assets fora do escopo.
