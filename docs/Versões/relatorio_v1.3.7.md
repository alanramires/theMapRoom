# RELATORIO v1.3.7

Data: 2026-03-19
Tema: replay versao 1

## Objetivo desta versao

Estabilizar o novo replay baseado em pilha de `action batches` + `snapshots`, com fluxo fiel ao que o jogador executa em partida.

## Entregas

1. Estrutura base de replay por pilha
- Sequencia consolidada em blocos: `snapshot -> action batch -> snapshot`.
- Gravacao do batch apenas apos confirmacao da acao; buffer volatil segue descartavel ate confirmar.

2. Execucao de replay orientada por batches
- `FWD` executa um batch por vez e avanca entre snapshots.
- `PLAY` executa batches em sequencia e respeita pausa somente no limite de snapshot.
- `BACK` volta por snapshots sem reexecutar batch.

3. Cursor e apresentacao durante replay
- Movimento de cursor entre origem/destino do batch com travel em hex.
- Execucao de comandos gravados com foco em comportamento de jogador (selecao, movimento, sensor, confirmacao).

4. Snapshot inicial e carga de save
- `snapshot#0` de partida nova e gravado apenas quando o jogo termina validacoes iniciais e libera em estado neutro.
- Em `loadgame`, `snapshot#0` carregado e preservado quando corresponde ao turno/time atual.

5. UI de replay e controles
- Inicio de replay respeitando modo selecionado no painel (incluindo "From Beginning").
- Suporte ao campo de time especifico para visao durante replay, com `-1` representando "qualquer time".

## Impacto esperado

- Replay mais confiavel para auditoria de jogadas passadas.
- Menor divergencia entre acao gravada e acao exibida.
- Base pronta para iteracoes de qualidade em batches/sensores e refinamento de UX de painel.

## Observacoes

- Esta versao marca o fechamento da primeira entrega funcional do replay em pilha.
- Ajustes finos de cobertura de sensores e apresentacao cinematica podem seguir em ciclos incrementais.
