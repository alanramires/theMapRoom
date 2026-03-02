# Relatorio de Atualizacao - v1.0.18

## Em uma frase
Servico do Comando (X) ficou pronto para execucao em lote com mais previsibilidade.

## O que isso trouxe na pratica
- Atendimento de varias unidades em fila ficou mais confiavel.
- Melhor tratamento para transportador + embarcados.
- Logs e animacoes ficaram mais claros para acompanhar resultados.

## Bloco tecnico curto
- Nova acao global no `TurnStateManager.CommandService`.
- Melhoria de fila por grupo de transportador.
- Correcao de calculo com estoque infinito (overflow).
