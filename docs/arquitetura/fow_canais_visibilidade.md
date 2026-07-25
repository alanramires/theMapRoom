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

## Fast path de load

Na etapa 6, saves v17 podem restaurar as contribuições sem executar a coleta cara de visão. A restauração é integral: não existe aceitação parcial nesta versão.

Antes da primeira mutação, o load valida:

- versão do formato interno do cache;
- slot observador e ausência de perspectiva visual dividida;
- `CursorState.Neutral`;
- assinatura da cena, tilemap, terreno e configurações de sensor;
- conjunto completo de unidades e construções elegíveis;
- identidade e assinatura de cada fonte;
- checksum de cada contribuição;
- células existentes no board, sem duplicatas;
- equivalência entre os canais de unidade;
- regra geográfica/sensor das construções.

Somente depois de todas as verificações o runtime é reinicializado e os agregados são reconstruídos a partir das fontes salvas. A chave incremental das unidades é recriada usando o estado runtime confirmado.

Visibilidade final de unidades, stealth, contatos, memória, overlay e HUD continuam sendo publicados a partir do snapshot restaurado pelas rotinas normais. Os resultados derivados de detecção não são confiados cegamente ao save.

Qualquer falha produz `[FoW][LoadCacheRestore] success=false fallback=cold` e executa `RefreshFogOfWarForActiveTeam()`. Saves v16 ou anteriores sempre usam esse fallback. No fallback, a verificação da etapa 5 continua disponível para diagnóstico.

### Diagnóstico de células rejeitadas

Quando o fast path rejeita uma contribuição por célula inválida, o motivo de
`[FoW][LoadCacheRestore]` identifica a primeira divergência com:

- índice e identidade da fonte persistida;
- canal geográfico ou sensorial;
- índice e coordenada da célula;
- causa (`nonzero_z`, `board_tile_missing`, `duplicate`, lista ou board nulos);
- presença dentro dos bounds do board;
- até oito Tilemaps do mesmo Grid que possuem tile na coordenada.

Essa instrumentação é somente leitura. Ela não amplia o domínio aceito pelo
restore, não aplica contribuições parciais e mantém o cold refresh como fallback.

### Domínio canônico do tabuleiro

O Tilemap principal resolvido por `ResolveFogBoardTilemap()` é a fonte canônica
das células que podem integrar uma contribuição persistente. Uma célula só entra
nos conjuntos geográfico e sensorial quando:

```text
cell.z == 0 && boardMap.GetTile(cell) != null
```

O recorte acontece na fronteira comum de entrada das contribuições e vale para
unidades, construções, full refresh, atualização incremental e restauração.
Algoritmos de alcance podem calcular coordenadas além da borda, mas essas
coordenadas não passam a integrar o estado confirmado do FoW nem o save.
