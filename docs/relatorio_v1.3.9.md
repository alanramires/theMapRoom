# v1.3.9 - replay fixes parte 2

## Resumo
- Consolidacao dos ajustes de replay com foco em execucao por input (sem teleporte de estado) e estabilidade de batches/snapshots.
- Revisao do pipeline de sensores no replay manager, incluindo tratamento por sensor e substeps.
- Correcao da gravacao/consumo de substeps para fluxos com fila (especialmente fusao e suprimento).

## Replay / Sensores
- Execucao por sensor no replay: Attack, Embark, Disembark, Capture, Merge, Supply, Transfer, Land, CommandService e RemoveUnit.
- Ajustes de navegacao/confirmacao por lote para manter comportamento equivalente ao runtime.
- Correcao de pontos de fallback para evitar mistura indevida entre target unico e lista de substeps.

## Shopping emulado por input
- PlayerAction passou a registrar:
  - ShoppingSelectedIndex
  - ShoppingUnitTypeId
- Gravacao no fluxo de compra para preservar indice selecionado e tipo comprado.
- Emulacao no replay:
  - abre menu via confirm
  - navega ate o indice gravado com delay configuravel
  - valida ShoppingUnitTypeId e emite warning em caso de divergencia
  - confirma a compra pelo mesmo fluxo de gameplay

## Build
- Build limpo validado:
  - Assembly-CSharp
  - Assembly-CSharp-Editor
