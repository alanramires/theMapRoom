# v4.0.40 - AI fixes

Esta versao corrige a separacao entre a percepcao logica da IA e a apresentacao do Fog of War para o jogador humano, alem de consolidar ajustes visuais e de interface.

## IA e Fog of War

- O snapshot logico do Fog of War passa a ser mantido separadamente por time.
- A apresentacao do FOW humano nao sobrescreve mais as celulas visiveis, celulas conhecidas e unidades detectadas pela IA.
- Capturadores voltam a consultar a perspectiva correta da IA durante partidas com FOW Total.
- Atualizacoes incrementais republicam o snapshot somente depois do compromisso da acao e do retorno ao estado `Neutral`.
- Os comandos `fow on`, `fow partial`, `fow off` e seus aliases `set fow ...` foram preservados para os novos snapshots.
- `fow off` tambem limpa snapshots logicos antigos antes de uma futura reativacao.

## Apresentacao sob o tampao do FOW

- Unidades comuns permanecem renderizadas abaixo do overlay opaco no FOW Total.
- Movimento e rastro passam a aparecer naturalmente apenas pelos recortes visiveis da neblina.
- Unidades com stealth ativo continuam obedecendo a deteccao individual.
- A Neblina Leve continua usando ocultacao individual de sprites e HUDs, pois nao possui o overlay opaco do FOW Total.
- Unidades compradas em um HQ inimigo globalmente conhecido nao vazam mais informacao pelo hex aberto do marco.
- Ao abandonar o HQ, a unidade volta ao comportamento normal sob o tampao; selecao automatica e rollback respeitam a mesma regra.

## Save, load e sensores

- O load republica imediatamente o snapshot confirmado do time restaurado.
- Alvos visiveis por spotting deixam de ser descartados por snapshots anteriores ao load.
- A restauracao nao depende mais da janela de um frame do refresh visual agendado para liberar a mira.

## Interface

- O painel de confirmacao de partida calcula a altura do bloco de detalhes conforme o texto de regras.
- O botao `INICIAR JOGO` e reposicionado pelo layout sem sobrepor titulo ou regras.
- Painel de turno e assets visuais receberam os ajustes preparados para esta versao.

## Validacao

- Projeto compilado com sucesso, sem erros.
- Arquivos de codigo passaram por `git diff --check`.
- Os arquivos serializados pelo Unity mantêm apenas os avisos de whitespace e normalizacao de final de linha gerados pelo editor.
