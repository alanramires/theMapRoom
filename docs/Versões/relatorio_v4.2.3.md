# v4.2.3 - Refactor do Save/Load para SlotID parte 3/6

Esta versão conclui a terceira das seis etapas do refactor do Save/Load de Fog of War por `PlayerSlotId`.

O runtime deixa de manter um cache estrutural exclusivo para unidades e passa a representar explicitamente cada entidade que contribui para o FOW como uma fonte identificável.

## Identidade das fontes

Cada fonte de contribuição possui uma chave composta por:

- `FogContributionSourceType`: `Unit` ou `Construction`;
- `InstanceId`: identidade estável da entidade dentro da partida.

O tipo faz parte da chave, impedindo colisão semântica entre uma unidade e uma construção que possuam o mesmo número de instância.

Quando uma entidade ainda não possui `InstanceId` válido, o runtime mantém o fallback já utilizado pelo projeto para sua identidade de entidade.

## Cache genérico por fonte

O antigo cache exclusivamente por unidade foi substituído por `fogContributionsBySource`.

Cada `FogSourceContributionCacheEntry` mantém:

- `geographicCells`;
- `sensorCells`;
- a chave incremental de validade quando a fonte é uma unidade.

Os agregados geográfico e sensor por célula passam a ser derivados da soma das entradas individuais das fontes.

## Unidades

- A unidade usa `Unit + InstanceId` como identidade da fonte.
- Um cache válido continua evitando a coleta redundante de células.
- Movimento comprometido remove as contribuições geográficas e sensoriais anteriores antes de adicionar as novas.
- Spawn incremental registra uma nova fonte sem recalcular todas as demais unidades.
- Desativação, morte ou remoção retiram integralmente a contribuição associada à fonte.
- O cache especializado por domínio e altura continua separado e indexado pela unidade.

## Construções

- Cada construção passa a possuir uma entrada própria no cache genérico.
- Construções aliadas registram o raio em `geographicCells`.
- Somente o próprio hex entra em `sensorCells`.
- QGs inimigos tratados como marcos globais registram apenas o próprio hex geográfico.
- Uma fonte já existente é removida simetricamente antes de ser reconstruída, impedindo dupla contagem caso o coletor seja repetido.

## Operações simétricas

Foram centralizadas as operações para:

- adicionar uma célula geográfica a uma fonte;
- adicionar uma célula sensorial a uma fonte;
- remover integralmente os dois conjuntos de uma fonte.

Os `HashSet` da entrada impedem que a mesma fonte incremente duas vezes o mesmo canal na mesma célula.

## Diagnóstico

O log opcional `[FoW][Coverage]` agora informa:

- total de fontes;
- quantidade de fontes do tipo unidade;
- quantidade de fontes do tipo construção;
- células geográficas;
- células sensoriais;
- células exclusivamente geográficas.

## Save e load

Esta etapa permanece exclusivamente no runtime:

- o save continua na versão `15`;
- nenhum DTO de contribuição por fonte foi adicionado;
- `fogContributionsBySource` ainda não é serializado;
- o agregado geográfico legado continua sendo exportado;
- o load continua executando cold refresh;
- a restauração do cache permanece desligada.

## Contrato transacional

As entradas por fonte representam somente o estado confirmado. Movimento provisório e outras ações canceláveis não podem substituir, remover ou publicar contribuições. Os deltas continuam sendo processados após compromisso explícito e retorno a `CursorState.Neutral`.

## Documentação

`docs/arquitetura/fow_canais_visibilidade.md` foi atualizado para registrar a identidade das fontes, os conjuntos individuais e a relação entre as entradas e os agregados por célula.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- DTOs e fluxo de Save/Load permaneceram sem alterações nesta etapa.

## Próxima etapa

Etapa 4/6: persistir as contribuições por fonte no save, inicialmente sem consumi-las no load, permitindo validar o conteúdo salvo antes de ativar qualquer fast path.
