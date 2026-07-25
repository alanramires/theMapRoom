# v4.2.5 - Refactor do Save/Load para SlotID parte 5/6

Esta versão conclui a quinta das seis etapas do refactor do Save/Load de Fog of War por `PlayerSlotId`.

O load passa a verificar a fotografia de contribuições persistida na v16, mas continua usando exclusivamente o cold refresh como verdade. A finalidade desta etapa é provar que o cache salvo reproduz o resultado recalculado antes de autorizar qualquer fast path.

## Ordem do load

O fluxo permanece conservador:

1. unidades e construções são reidratadas;
2. estado confirmado e slot ativo são restaurados;
3. o cursor retorna a `Neutral`;
4. `RefreshFogOfWarForActiveTeam()` executa o cold refresh;
5. somente depois do refresh, o cache salvo é comparado com o runtime.

Nenhuma contribuição salva é aplicada durante essa sequência.

## Verificação por fonte

`VerifyFogSourceContributionsFromSave` compara cada entrada persistida com `fogContributionsBySource`.

São verificados:

- slot observador;
- tipo da fonte;
- `InstanceId`;
- `sourceStateHash`;
- conjunto de células geográficas;
- conjunto de células sensoriais.

As células são comparadas como conjuntos. Duplicatas no conteúdo salvo também provocam divergência.

## Divergências detectadas

O verificador contabiliza:

- entradas inválidas;
- identidades duplicadas;
- fontes salvas ausentes no runtime;
- fontes runtime ausentes no save;
- assinaturas incompatíveis;
- conjuntos geográficos diferentes;
- conjuntos sensoriais diferentes.

Uma fonte só conta como equivalente quando identidade, assinatura e os dois canais coincidem.

## Logs controlados

O resultado usa o log único `[FoW][LoadCacheVerify]`.

No sucesso:

- uma única linha informa `exact=true`;
- são apresentados slot, fontes salvas, fontes runtime e total correspondente.

Na divergência:

- é emitido um único warning;
- os contadores resumem cada categoria;
- no máximo oito detalhes de fontes são anexados;
- grandes saves não geram uma linha por célula ou por entidade.

Saves antigos ou sem `fogSourceContributions` não produzem esse log e seguem normalmente pelo cold refresh.

## Instrumentação do load

Foram adicionadas duas etapas ao perfil:

- `verify_fog_cache.begin`;
- `verify_fog_cache.end`.

Assim, o custo da validação pode ser medido separadamente do custo real de reconstrução do FOW.

## Segurança

- O verificador é read-only.
- Não altera agregados geográficos ou sensoriais.
- Não publica snapshots.
- Não atualiza detecção, memória ou inteligência.
- Não modifica o overlay.
- A API de restauração continua desconectada.

## Contrato transacional

A comparação ocorre depois do retorno a `Neutral` e depois do snapshot confirmado ser recalculado. Ela nunca observa uma posição provisória como verdade nem permite que dados persistidos substituam o estado confirmado.

## Documentação

`docs/arquitetura/fow_canais_visibilidade.md` foi atualizado com a ordem da verificação, seus campos comparados, limites de log e caráter estritamente read-only.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- Auditoria confirmou a ordem `cold refresh → verificação`.
- Auditoria confirmou que nenhuma API de restauração foi conectada.

## Próxima etapa

Etapa 6/6: ativar a restauração das contribuições quando todas as validações conservadoras forem satisfeitas, mantendo cold refresh integral como fallback seguro.
