# RELATORIO v1.3.6

Data: 2026-03-19
Tema: preparativos para o refactor de state + replay

## Objetivo desta versao

Consolidar a base de analise e documentacao para o refactor da FSM do `TurnStateManager` e da camada de replay, reduzindo ambiguidade entre:
- estados explicitos (`CursorState`)
- subpassos (`ScannerPromptStep`)
- fluxos inline/flags sem estado dedicado
- comandos logicos de replay vs trilha cinematica

## Entregas

1. Mapa de estados atualizado
- Documento `docs/turnState.md` com arvore de estados atual.
- Diferenciacao entre estado explicito, subestado e comportamento inline.
- Inclusao de classificacao pratica:
  - estados inferiores de inspecao
  - fluxos menores com caminho unico (confirm/cancel)
  - estados automaticos hardcoded
  - recorte de fluxos que entram no replay

2. Inventario de sensores atualizado
- Documento `docs/sensors.md` consolidado com sensores ativos.
- Inclusao dos fluxos de FOW runtime:
  - `PodeDetectar`
  - `PodeEnxergar`
  - `AlguemMeVe`
- Inclusao de secao de fluxos de camada nao-FSM (forca de camada / force to emerge / preferencia de camada).

3. Preparacao para refactor state + replay
- Base documental pronta para separar com clareza:
  - o que deve virar estado dedicado
  - o que deve permanecer como fluxo inline
  - o que deve ser apenas evento de replay
- Registro de que a cinematica de replay esta hoje focada em combate (`Attack`) e que demais comandos continuam logicos.

## Impacto esperado no proximo ciclo

- Refactor com menor risco de regressao por transicoes implicitas.
- Melhor rastreabilidade entre input do jogador, transicao de estado e comando gravado no replay.
- Reducao de comportamento "fantasma" em overlays/inspecoes por definicao explicita de entrada/saida.

## Observacoes

- Esta versao e de preparacao/organizacao para o refactor.
- O foco foi estruturar entendimento compartilhado e pontos de acoplamento antes de mexer na arquitetura principal.
