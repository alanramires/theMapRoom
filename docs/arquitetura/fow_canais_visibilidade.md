# Canais de visibilidade do Fog of War

## Regra central

Revelar a geografia de uma célula não implica detectar uma unidade que a ocupa.

O runtime mantém dois canais confirmados e independentes por slot observador:

- `geographic`: controla abertura do overlay, terreno conhecido e memória visual;
- `sensor`: registra células cobertas por uma fonte capaz de participar da detecção.

Uma célula presente em `geographic`, mas ausente em `sensor`, é `geographicOnly`.

## Fontes

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

Na etapa 2 do refactor, os dois canais existem apenas no runtime. O save continua no formato da etapa anterior e o load continua executando cold refresh.

A persistência por fonte será introduzida somente depois que unidades e construções compartilharem um modelo explícito de contribuição.
