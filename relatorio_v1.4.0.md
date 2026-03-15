# Relatorio v1.4.0

## Tema
Gerador de mapa basico no Editor com foco em fluxo rapido de montagem de tabuleiro.

## Entregas principais
- Janela `Tools/Utils/Map Generator (Basic)` com geracao global e por zona retangular.
- Modo `WaterOnly` e modo `ConvincingFromDescription`.
- Coordenadas no padrao do projeto: origem no topo-esquerdo e preenchimento para direita/baixo.
- Validacao/clamp de zona com feedback na UI.
- Presets de arquipelago 60x60.
- Botao de geracao completa do arquipelago.
- Modo simetrico para arquipelago completo (espelhamento esquerda -> direita).
- Conectividade de terreno via flood-fill com `minIslandSize`.
- Ajustes de suavizacao de borda para evitar cantos sempre em agua.
- Override de `targetLandRatio` no modo zona.
- Canal com `targetLandRatio <= 0.1` forca agua pura.
- Ruido com coordenadas globais para continuidade entre zonas adjacentes.

## Impacto
- Montagem de mapas ficou mais previsivel, repetivel por seed e mais rapida para cenario competitivo.
- Reducao de artefatos visuais entre zonas e melhora de simetria para mapas PvP.

## Observacao
- Esta versao inclui tambem alteracoes adicionais presentes no working tree no momento do release.
