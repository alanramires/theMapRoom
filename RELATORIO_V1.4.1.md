# Replay em testes

Versão: v1.4.1  
Status: em validação no Unity

## Resumo
- Replay baseado em ActionStack + AutomatedPlayer em fase de testes integrados.
- Fluxo orientado a eventos em validação (`OnCursorReturnedToNeutral` e `OnSensorsReady`).
- Ajustes recentes focados em robustez do autoplay entre batches de movimento e combate.

## Hardening aplicado nesta etapa
- Bloqueio de save/load durante replay ativo.
- Pausa automática do replay ao fechar painel F9.
- Validação de `UnitInstanceId` antes de confirmar ações gravadas.
- Mensagens/dialogs de feedback para estados de replay (loading, pause, erro, save/load desativado).

## Performance e operação
- Modo `fastReplayMode` com redução de delays artificiais.
- Cursor com travel/teleporte conforme configuração.
- Logs de listeners e dispatch adicionados para diagnóstico de travas entre batches.

## Pendências de QA
- Validar sequência completa de batches com combate em autoplay sem travar.
- Confirmar consistência de sensores após movimento (incluindo casos limítrofes).
- Reexecutar checklist de replay/hardening no Unity após cada ajuste da FSM.
