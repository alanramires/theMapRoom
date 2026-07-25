# Canais de visibilidade do Fog of War

## Regra central

Revelar a geografia de uma célula não implica detectar uma unidade que a ocupa.

O runtime mantém dois canais confirmados e independentes por slot observador:

- `geographic`: controla abertura do overlay, terreno conhecido e memória visual;
- `sensor`: registra células cobertas por uma fonte capaz de participar da detecção.

Uma célula presente em `geographic`, mas ausente em `sensor`, é `geographicOnly`.

## Fontes

Cada fonte confirmada possui uma identidade composta por:

- `FogContributionSourceType`: `Unit` ou `Construction`;
- `InstanceId`: identidade estável da entidade dentro da partida.

O cache `fogContributionsBySource` mantém, para cada fonte, seus próprios conjuntos `geographicCells` e `sensorCells`. Os contadores agregados por célula são derivados da soma dessas entradas.

### Unidades

As células produzidas pelo cache normal de visão da unidade contribuem para os dois canais. A decisão final sobre a visibilidade de um alvo continua pertencendo ao algoritmo de sensores, que considera domínio, altura, alcance, LOS e stealth.

O canal `sensor` não substitui `PodeDetectarSensor` e não deve ser usado isoladamente para declarar um alvo visível.

### Construções

Uma construção aliada:

- revela geograficamente todas as células dentro de `ConstructionData.visao`;
- contribui ao canal de sensor somente no próprio hex;
- não revela ocupantes nas células adjacentes apenas por ter aberto o terreno.

Um QG inimigo tratado como marco global revela geograficamente somente o próprio hex e não concede cobertura sensorial.

## Snapshot

Cada `FogSlotGameplaySnapshot` publica:

- `geographicallyVisibleCells`;
- `sensorCoveredCells`;
- `geographicOnlyCells`;
- `knownCells`;
- visibilidade final por unidade.

Esses conjuntos pertencem ao `PlayerSlotId` observador. `TeamId` continua sendo apenas identidade visual.

## Contrato transacional

Os canais são estado confirmado. Eles só podem ser reconstruídos ou publicados depois do compromisso da ação e do retorno a `CursorState.Neutral`.

Movimento provisório, previews, animações e submenus não podem adicionar contribuições, revelar geografia, publicar cobertura sensorial ou registrar memória.

## Persistência

Na etapa 4 do refactor, o save v16 passa a gravar `fogSourceContributions`. Cada entrada contém:

- `observerSlotIndex`;
- tipo e `InstanceId` da fonte;
- assinatura estável do estado relevante da fonte;
- células geográficas;
- células sensoriais.

As listas são canonicalizadas antes da escrita, mas excluídas do hash autoritativo da partida por serem estado derivado.

Na etapa 5, o load continua executando cold refresh e, somente depois dele, compara a fotografia salva com as contribuições recalculadas.

A verificação é estritamente read-only e compara:

- slot observador;
- tipo e identidade da fonte;
- assinatura do estado;
- conjunto geográfico;
- conjunto sensor.

O resultado é emitido em `[FoW][LoadCacheVerify]`. Sucesso produz uma única linha; divergências produzem um único warning com no máximo oito detalhes. Saves sem a coleção nova seguem silenciosamente pelo cold refresh.

Nenhuma entrada salva é aplicada ao runtime nesta etapa. A restauração existente continua desconectada.
