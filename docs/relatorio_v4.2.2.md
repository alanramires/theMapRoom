# v4.2.2 - Refactor do Save/Load para SlotID parte 2/6

Esta versão conclui a segunda das seis etapas do refactor do Save/Load de Fog of War por `PlayerSlotId`.

O foco desta etapa é separar no runtime dois conceitos que antes compartilhavam o mesmo agregado: revelar a geografia de uma célula e possuir cobertura capaz de participar da detecção de um ocupante.

## Dois canais de visibilidade

O runtime agora mantém dois contadores independentes por célula:

- `fogGeographicContributorsByCell`: controla abertura do overlay, terreno revelado e memória visual;
- `fogSensorContributorsByCell`: registra cobertura de fontes capazes de participar da detecção.

Uma célula geograficamente aberta e sem contribuição sensorial passa a ser representada explicitamente como `geographicOnly`.

## Contribuições das unidades

- A visão confirmada de uma unidade contribui para os canais geográfico e sensor.
- Movimento comprometido remove as duas contribuições da posição anterior e adiciona as novas.
- Spawn incremental e desativação também mantêm os dois contadores simétricos.
- Movimento provisório continua impedido de publicar qualquer contribuição definitiva.

O canal sensor não substitui o algoritmo de detecção. `PodeDetectarSensor` continua sendo a autoridade para domínio, altura, alcance, LOS, stealth e demais regras aplicáveis ao alvo.

## Contribuições das construções

- Uma construção aliada revela geograficamente todas as células dentro de `ConstructionData.visao`.
- Somente o próprio hex da construção recebe contribuição no canal sensor.
- Células adjacentes podem ter terreno revelado sem expor as unidades que as ocupam.
- Um QG inimigo tratado como marco global revela geograficamente apenas o próprio hex e não fornece cobertura sensorial.

Essa separação formaliza o comportamento já desejado pelo jogo sem transformar terreno conhecido em detecção automática.

## Snapshot por slot

`FogSlotGameplaySnapshot` agora publica separadamente:

- `geographicallyVisibleCells`;
- `sensorCoveredCells`;
- `geographicOnlyCells`;
- `knownCells`;
- visibilidade final por unidade.

O heurístico anterior que inferia um marco global pela existência de exatamente um contribuidor foi removido. `geographicOnlyCells` agora é calculado diretamente pela diferença entre os canais geográfico e sensor.

## Consultas e diagnóstico

- `IsCellGeographicallyVisibleForActiveSlot` explicita a consulta de terreno aberto.
- `IsCellCoveredBySensorForActiveSlot` expõe a cobertura sensorial confirmada.
- `IsCellVisibleForActiveTeam` permanece como ponte compatível para a semântica geográfica anterior.
- O log opcional `[FoW][Coverage]` informa quantidades geográficas, sensoriais e exclusivamente geográficas.
- O cache de conhecimento por frame foi renomeado para identificar explicitamente o slot observador.

## Save e load

Esta etapa não altera o formato de persistência:

- o save permanece na versão `15`;
- os novos canais existem somente no runtime;
- o agregado persistido existente continua representando a abertura geográfica;
- o load continua executando cold refresh;
- a restauração do cache runtime continua desligada.

## Contrato transacional

Os dois canais representam somente estado confirmado. Eles são reconstruídos ou atualizados após compromisso explícito e retorno a `CursorState.Neutral`. Previews, animações, movimento provisório e submenus não podem revelar geografia nem publicar cobertura sensorial.

## Documentação

Foi adicionada a referência arquitetural `docs/arquitetura/fow_canais_visibilidade.md`, registrando as responsabilidades dos canais, das fontes e do snapshot por slot.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem 248 avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- A auditoria confirmou que o load ainda chama `RefreshFogOfWarForActiveTeam()` e não conecta a restauração do cache.

## Próxima etapa

Etapa 3/6: generalizar o cache para contribuições identificadas por fonte, cobrindo unidades e construções, ainda sem persistir ou consumir essas contribuições no load.
