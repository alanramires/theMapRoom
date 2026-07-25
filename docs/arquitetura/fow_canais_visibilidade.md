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

### Invariantes antes da publicação

Depois de reconstruir o cache persistido, mas antes de publicar snapshot,
detecção, contatos, overlay, HUD ou `OnFogOfWarUpdated`, o fast path confirma:

- correspondência exata de todas as fontes e seus dois conjuntos de células;
- contagem agregada de contribuidores geográficos por célula;
- contagem agregada de contribuidores sensoriais por célula.

As contagens esperadas são derivadas da fotografia já validada, sem executar
novamente o algoritmo de visão. Qualquer divergência produz
`rebuild_invariant_mismatch`, descarta integralmente o runtime reconstruído e
devolve o controle ao cold refresh.

O caminho de sucesso mantém apenas o log resumido
`[FoW][LoadCacheRestore] success=true`, agora com totais separados de unidades e
construções. O diagnóstico detalhado de Tilemaps permanece restrito ao caminho
de erro de célula inválida.

### Runtime quente por slot durante movimentos

Durante um turno de IA com apresentação sob os sensores de um jogador humano,
o controlador mantém uma fotografia transitória das contribuições confirmadas
de cada slot já calculado. A fotografia contém:

- contribuições geográficas e sensoriais por fonte;
- contagem agregada dos dois canais;
- chaves incrementais das unidades.

Quando uma unidade compromete um movimento e o estado retorna a `Neutral`, o
runtime do slot ativo é reativado e somente a fonte movida é atualizada. Em
seguida, o runtime do slot de apresentação é reativado: como suas fontes não
mudaram, somente a visibilidade dos alvos é republicada e o overlay é redesenhado.

Isso evita o antigo full refresh duplo do slot da IA e do observador humano. A
fotografia é exclusivamente runtime, nunca é alimentada por posição provisória e
é descartada em resets completos do Fog of War. Se algum contexto necessário não
estiver disponível, o fluxo mantém `RefreshFogOfWarForActiveTeam()` como fallback
conservador.

A camada visual de memória/overlay é compartilhada, portanto possui uma barreira
adicional: quando gameplay e apresentação pertencem a slots diferentes, o
contexto da IA é estritamente `DataOnly` e não pode chamar renderização. A rotina
de render também rejeita defensivamente qualquer cache cujo observador não seja o
`PlayerSlotId` de apresentação. Somente depois de reativar o contexto humano a
memória explorada e o overlay podem ser escritos nos Tilemaps visuais.

## Barreiras de escrita por slot

Existem duas autorizações distintas na fronteira de saída do FoW:

- memória confirmada só pode ser registrada em `Neutral` e quando o cache ativo
  pertence exatamente ao `PlayerSlotId` que receberá a informação;
- Tilemaps de overlay e memória só podem ser alterados em `Neutral`, pelo cache do
  `PlayerSlotId` escolhido como observador visual local.

Isso permite calcular e memorizar o FoW próprio de uma IA, jogador remoto ou
replay em modo `DataOnly`, sem conceder a esse participante autoridade sobre a
apresentação local. A origem do batch não participa da decisão: somente o slot
observador e o contexto de apresentação importam.

As rotinas centrais de renderização e de contribuição geográfica verificam essa
barreira defensivamente. Com `enableFogValidationLogs`, uma tentativa rejeitada
gera `[FoW][WriteBarrier]` deduplicado para diagnosticar o chamador sem contaminar
o estado confirmado.
