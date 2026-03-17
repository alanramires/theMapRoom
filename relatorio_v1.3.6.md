# Relatorio v1.3.6

## Tema
Point save pre-agente com foco em performance, estabilidade de fluxo e legibilidade de UI durante turno/compras/fog.

## Entregas principais
- Fluxo de compra em construcoes revisado com selecao por foco (`shoppingSelectedIndex`), confirmacao por Enter e navegacao por setas.
- Preview detalhado no painel de dialogo durante compras (stats, armas, carga e sprite da unidade), com layout dinamico para leitura.
- Atualizacao do helper panel para destacar item focado na loja, melhorar linhas de transporte e limpar ruido visual.
- Ajustes no `ConstructionManager` para priorizar corretamente oferta runtime de suprimentos (inclusive quando nao infinito).
- Inclusao de `FogOfWarController` (debug/editor) para snapshot de contribuidores por unidade alvo.
- `MatchController` recebeu utilitarios de debug/consistencia de FoW e aplicacao conservadora de visibilidade no load.
- `SaveGameManager` aplica visibilidade conservadora de FoW apos carregar estado.
- Ajustes de fluxo em `TurnStateManager` (shopping, helper, transfer, state machine) e pequenos refinos de SFX/UI.

## Dados e balanceamento
- Atualizacoes amplas em assets de unidades (Aeronáutica, Exército e Marinha), incluindo custos/atributos e catalogo.
- Remocao de `MA Sea Hawk` dos assets.
- Atualizacao de matriz de combate em `docs/COMBAT_MATRIX.csv`.
- Ajustes de cena em `Assets/Scenes/Team Island.unity` e atualizacoes de assets de fonte/TMP.

## Impacto
- Menor friccao operacional no turno (comprar/confirmar/cancelar) e melhor feedback visual para tomada de decisao.
- Estado de FoW mais seguro durante carregamento, reduzindo risco de exibicao indevida de unidades.
- Base preparada para trabalho com agente com snapshot consistente do estado atual.

## Observacao
- Esta versao consolida todas as alteracoes presentes no working tree no momento do point save.
