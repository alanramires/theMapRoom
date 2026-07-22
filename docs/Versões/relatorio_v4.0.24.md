# v4.0.24 - Fog Total

Esta versão reformula a Névoa de Guerra total: durante o turno da IA, a névoa passa a subir acima do mundo e cobrir a apresentação pela perspectiva do humano, no lugar de ocultar sprite por sprite. Também ajusta o que é visível por padrão (QG como marco global, visão por raio das construções) e adiciona controle de opacidade.

## Névoa por cobertura (overlay)

- Enquanto a IA joga e a partida é apresentada pela ótica do humano, a névoa cobre o mundo pela sua própria camada, em vez de esconder cada unidade/efeito individualmente.
- O tilemap da névoa muda dinamicamente de sorting layer: `FogOfWar` (acima do mundo) quando precisa cobrir a apresentação; `SFX` no restante.
- Nova sorting layer `FogOfWar` adicionada ao projeto.
- Unidades e efeitos permanecem renderizados sob a névoa (a cobertura é da camada), simplificando o pipeline de visibilidade.
- A validação da sorting layer é reaplicada ao trocar preset, alternar a névoa, entrar no modo parcial e ao restaurar o turno real após o refresh visual.

## Visibilidade padrão

- O QG passa a ser um marco global do tabuleiro: todos conhecem o hex do QG inimigo; apenas o dono recebe o restante do raio de visão configurado.
- Construções contribuem visão por raio (`ConstructionData.visao`), não mais apenas o próprio hex.
- A ocultação de HUD de construções respeita a visibilidade da célula na apresentação da névoa.

## Opacidade da névoa

- Nova opção para ajustar a opacidade da névoa de 0 a 100%.
- Comando de debug `set fog <0-100>` aplica a opacidade; aliases `set fog partial` / `set fow partial` para o modo parcial.

## Limpeza

- Removida a lógica antiga de visibilidade por sprite no turno da IA (recorte de preview de mira, visibilidade por ponto de interpolação da unidade em movimento e ocultação individual de efeitos).
- API de consulta unificada em `IsCellVisibleInFogPresentation(cell)`, consumida pela ocultação de HUD.

## Validação

- Alterações concentradas em `MatchController`, `ConstructionManager`, `AnimationManager`, `CursorController`, `TurnStateManager`, `TurnStateManager.ScannerPrompt`, `TurnStateManager.PathVisual`, `UnitManager` e `DebugManager`, além da sorting layer `FogOfWar` em `TagManager.asset`.
- Pendente de verificação no Editor (Play mode): apresentação da névoa no turno da IA, QG global, visão por raio das construções e o comando `set fog`.
