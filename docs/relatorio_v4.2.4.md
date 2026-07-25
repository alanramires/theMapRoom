# v4.2.4 - Refactor do Save/Load para SlotID parte 4/6

Esta versão conclui a quarta das seis etapas do refactor do Save/Load de Fog of War por `PlayerSlotId`.

O save passa a transportar a fotografia das contribuições individuais de unidades e construções. O load ainda não usa esses dados: esta etapa existe para estabilizar e inspecionar o formato antes de autorizar qualquer restauração.

## Save v16

O formato de save foi atualizado para a versão `16`.

Foi adicionada a coleção `fogSourceContributions`, composta por entradas `FogSourceContributionSaveData`.

Cada entrada registra:

- `observerSlotIndex`;
- tipo estável da fonte;
- `sourceInstanceId`;
- `sourceStateHash`;
- células geográficas;
- células sensoriais.

Os valores persistidos para o tipo da fonte são estáveis no formato:

- `1`: unidade;
- `2`: construção.

## Assinatura da fonte

`sourceStateHash` registra uma assinatura determinística do estado relevante para a contribuição.

Para unidades, a assinatura considera:

- posição;
- slot proprietário;
- domínio e altura;
- condição de embarque;
- alcance de visão;
- identidade estável do `UnitData`.

Para construções, considera:

- posição;
- slot proprietário;
- condição de QG;
- alcance de visão;
- identidade estável do `ConstructionData`.

Strings usam uma função de hash própria e determinística, evitando depender de `string.GetHashCode()` entre processos.

## Exportação

- `ExportFogSourceContributionsForSave` percorre o cache confirmado por fonte.
- Entradas vazias não são gravadas.
- Todas as entradas recebem o slot observador proprietário do snapshot.
- Canais geográfico e sensor são exportados separadamente.
- O agregado geográfico legado continua sendo salvo durante a transição.

## Canonicalização

Antes da escrita:

- células geográficas são ordenadas por coordenadas;
- células sensoriais são ordenadas por coordenadas;
- fontes são ordenadas por slot observador, tipo e `InstanceId`.

Isso mantém o JSON persistido determinístico mesmo quando os dados runtime vierem de `Dictionary` e `HashSet`.

## Hash autoritativo

As contribuições por fonte são cache derivado e não fazem parte da verdade autoritativa da partida.

`MatchStateHasher` substitui temporariamente `fogSourceContributions` por uma lista vazia durante o cálculo do hash e restaura a referência após a serialização canônica. Assim, diferenças de cache não provocam falso desync.

## Compatibilidade

- Saves anteriores à v16 recebem uma lista vazia.
- O campo novo é inicializado defensivamente após a desserialização.
- Nenhuma migração tenta fabricar contribuições ausentes.
- Saves antigos continuam seguindo o cold refresh atual.

## Load deliberadamente inalterado

- `fogSourceContributions` não é importado.
- Nenhum cache por fonte é restaurado.
- O load continua chamando `RefreshFogOfWarForActiveTeam()`.
- O custo frio permanece nesta versão.
- A função existente de restauração do agregado continua fora do fluxo.

## Contrato transacional

Somente contribuições do snapshot confirmado são exportadas. Ações provisórias não podem substituir o cache confirmado e, portanto, não podem inserir no save geografia, cobertura sensorial ou detecção oriundas de uma posição cancelável.

## Documentação

`docs/arquitetura/fow_canais_visibilidade.md` foi atualizado com o formato persistido, sua exclusão do hash e a regra de que o load ainda não consome as entradas.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- Auditoria confirmou ausência de importação ou restauração das novas entradas no load.

## Próxima etapa

Etapa 5/6: depois do cold refresh, comparar as contribuições salvas com as recalculadas e registrar divergências por fonte e canal, ainda sem usar o cache salvo como verdade do jogo.
