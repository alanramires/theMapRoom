# Refactor de Fusao - parte 2 (AI)

## Resumo

Esta versao fecha a segunda parte do ajuste de fusao para a IA, corrigindo os casos em que a unidade escolhia aproximacoes incoerentes, tentava fundir com candidato que ja tinha ficado invalido ou parava em hex que so permitia passagem.

- a IA de reparo agora diferencia melhor candidato de fusao imediato, aproximacao valida e fallback para reparo
- o ranking de aproximacao passa a considerar se a unidade pode realmente terminar o movimento no hex escolhido
- o fluxo automatico de fusao evita confirmar candidato invalido depois do movimento
- logs e ids de auditoria ficaram mais rastreaveis no runtime

## Principais mudancas

- `AIPlayerController`:
  - objetivo de fusao em `repairMode` agora carrega a `approachCell` validada ate a execucao do movimento
  - a IA nao reinterpreta mais o destino mirando o hex do candidato quando a simulacao validou outro hex adjacente
  - hex ocupado por aliado que permite atravessar, mas nao permite encerrar movimento, deixa de entrar no ranking de fusao
  - telemetria final agora diferencia `avancou para fusao` de `retornou para reparo`
- `TurnStateManager`:
  - entrada automatica em `F` so ocorre quando ainda existe candidato valido no snapshot atual
  - replay/automacao de fusao nao tenta mais confirmar alvo que ja ficou invalido
  - `FusaoDBG` passou a incluir a unidade selecionada em todas as linhas
- auditoria/runtime:
  - `deadByUnit` agora grava identificador runtime estavel da unidade agressora/receptora, no formato usado no rastreio
  - nomes de unidades reais em cena voltaram a ser estabilizados por id runtime, sem reintroduzir sufixos transitorios de estado
- replay/runtime:
  - limpeza de artefatos de replay foi endurecida para tentar remover clones residuais no inicio da partida

## Efeito esperado

- a IA passa a preferir candidatos de fusao realmente executaveis, em vez de aproximacoes que so pareciam boas no path
- candidatos bloqueados por ocupacao final deixam de provocar indecisao e `stay/fallback`
- console e inspector ficam mais uteis para rastrear quem fundiu com quem e por que a IA tomou determinada rota
