# Relatorio de Atualizacao - v1.1.2

## Em uma frase
A v1.1.2 reforca o fluxo de debug e melhora a leitura tatica do Mirando com feedback visual/sonoro para alvos invalidos.

## O que isso trouxe na pratica
- O painel de debug ficou mais util no dia a dia, com comandos para HP, autonomia, municao e spawn.
- O jogador entende melhor por que um alvo nao pode ser confirmado em Mirando (ex.: sem spotter).
- O fluxo de mira evita confirmacoes indevidas em alvo invalido e reduz confusao durante teste/combate.

## Principais melhorias
1. Painel debug expandido
- Inclusao e ajuste de comandos como `destroy unit`, `wake unit`, `set hp`, `set autonomy`, `refuel unit`, `set ammo`, `set ammo#N`, `rearm unit`, `repair unit` e `spawn`.
- `set ammo <valor>` agora assume arma `#1` quando o indice nao e informado.

2. Mirando com visual aid para invalidos
- Navegacao de alvos em Mirando passou a considerar validos e invalidos no ciclo, com destaque claro de estado.
- Arco de tiro invalido usa cor cinza escuro, unidades invalidas ficam escurecidas e `Enter` toca erro em vez de confirmar.

3. Regras de entrada e confirmacao em mira
- Se `Pode Mirar = nao`, o jogo nao entra no estado de Mirando.
- No ciclo de mira, alvo invalido nao avanca para confirmacao: permanece no ciclo e mostra motivo no log.

4. Qualidade de uso no painel debug
- Comandos antigos/atalhos residuais foram removidos para evitar conflito com controles de jogo.
- Ao focar o campo de texto do debug, atalhos globais nao devem interceptar digitacao.

## Regras importantes
- `Pode Mirar = nao`: bloqueia entrada no Mirando.
- `Mirando (alvo invalido)`: `Enter` nao confirma ataque, apenas toca erro e exibe o motivo.
- `set ammo`: sem sufixo `#N`, aplica em `arma #1`.

## Bloco tecnico curto
- Fluxos alterados em `TurnStateManager.ScannerPrompt` para selecao mista de alvos e bloqueio de confirmacao invalida.
- Ajustes em `DebugManager` e `TurnStateManager` para parser/comandos de debug e servicos auxiliares.
- Documentacao de comandos adicionada em `docs/debug.md`.

## Resultado
A v1.1.2 deixa o ciclo de combate mais legivel, reduz erro de operacao em mira e acelera iteracao de testes com um debug panel mais forte.

