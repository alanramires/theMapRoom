# v1.2.5 - Hexagonos contestados

## Resumo
- Introduzida a regra de **hexagono contestado** no fluxo de `Total War`.
- Ajustado o comportamento de empilhamento e selecao em hex com unidades de times diferentes.
- Ajustado o scanner para bloquear acoes nao permitidas em hex contestado.

## Regras aplicadas
- `Total War = true`: permite compartilhar hex apenas com unidade de outro time.
- Em hex contestado, ficam bloqueadas as acoes:
  - `Pode fundir`
  - `Pode capturar`
  - `Pode embarcar`
  - `Pode desembarcar`
  - `Pode suprir`
  - `Pode transferir`
- `Pode mirar` nao aparece quando a unidade chegou ao hex contestado via `MoveuAndando`.

## UX / Interface
- Ao confirmar selecao em hex contestado, a unidade do time ativo passa a ter prioridade.
- A unidade selecionada sobe visualmente para o topo do empilhamento.
- No modo `Mirando`, ao navegar entre alvos, a unidade atualmente focada sobe no empilhamento para facilitar identificacao.
