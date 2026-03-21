# Avanços no replay, antes do refactor do hex disputado

Versão: v1.4.2  
Status: em validação no Unity

## Resumo
- Fluxo de replay consolidado para avançar batches com gate por estado real da FSM.
- Autoplay ajustado para respeitar neutralidade do cursor e execução assíncrona sem polling legado.
- Hardening aplicado em comando/suprimento/dialogs para reduzir divergências entre snapshot, sensor e confirmação.

## Avanços principais desta etapa
- Avanço entre batches condicionado por:
  - `cursor == Neutral`
  - `!IsReplayStepExecutionBusy()`
- Diagnóstico de busy no autoplay com motivo explícito:
  - `actionStepRoutine`
  - `IsAnimatingMovement`
  - `IsScannerActionExecutionInProgress`
- Limpeza de estado transitório do Serviço do Comando durante `RestoreSnapshot`, evitando falso "sem candidatos" no replay.
- Supressão de mensagens transitórias de painel durante execução de suprimento pós-confirmação.
- Ajustes no fluxo para troncos sem substeps (mover/shopping/comando/destruir), mantendo avanço orientado por cursor neutro.

## Coerência de snapshot e execução
- Snapshot/restauração validando flags de turno e estado de unidade antes de seguir no batch.
- Reforços no caminho de confirmação para reduzir divergência de `UnitInstanceId` em execuções sequenciais.
- Sequenciamento de autoplay preservando animações e aguardando retorno efetivo ao estado estável.

## Ajustes de UX ligados ao replay nesta janela
- Compras no shopping passam a respeitar camada forçada no hex de spawn (ex.: submarino emergido quando o hex força surface).
- Aeronaves compradas iniciam pousadas.
- Preview de shopping para submarino prioriza sprite `Naval/Surface` com aplicação de cor de time no preview.

## Pendências de QA
- Revalidar autoplay completo em sequência longa com mistura de:
  - mover sem substep
  - shopping
  - serviço do comando
  - destruir unidade
- Confirmar ausência de dialogs indevidos durante execuções de suprimento em replay.
- Rodar replay fim-a-fim com fast replay ligado/desligado e comparar consistência de estado final.
