# Relatorio de Atualizacao - v2.0.12

## AI Capturador Refinado

Esta versao registra o ajuste fino do capturador depois da divisao em partials, com foco em deixar mais coerente a transicao entre ponta de lanca, perseguidor e explorador durante setores em disputa.

## Em uma frase

O capturador passou a tratar melhor inimigos proximos ao caminho e ao ponto atual da unidade, evitando avancos passivos quando havia oportunidade clara de apoio em combate.

## O que isso trouxe na pratica

- Unidades em funcao de perseguidor nao ficam presas apenas ao inimigo perto do predio-alvo.
- O ponta de lanca passa a expor melhor, no log, quando esta avancando em contexto de disputa.
- A documentacao do papel de capturador foi atualizada para refletir a regra: setor limpo avanca, setor conquistado defende, setor em disputa persegue.

## Principais melhorias

1. Perseguidor mais atento ao entorno
- O filtro de alvo passou a considerar inimigos perto da celula atual da unidade.
- Isso permite que um capturador em transicao apoie combates proximos em vez de ignorar uma ameaca lateral relevante.

2. Avanco com leitura de disputa
- Ao escolher o melhor movimento, o capturador agora tambem considera inimigos perto da celula de destino do movimento.
- Quando o setor esta em disputa, o log usa o papel `Perseguidor`, deixando a decisao do roteador mais legivel durante o debug step.

3. Regra de papel registrada
- `docs/Capturadores.md` documenta que a ponta de lanca deve avancar apenas com setor limpo.
- Em setor conquistado, a tendencia e defender.
- Em setor disputado, a unidade deve apoiar como perseguidor ate a frente ficar segura.

## Bloco tecnico curto

- Ajustado `AIController.Capturer.Pursuer.cs` para aceitar alvos perto da posicao atual alem do alvo do setor.
- Ajustado `AIController.Capturer.cs` para aceitar alvos perto do melhor movimento e classificar o log como `Explorador`, `Perseguidor` ou `PontaLanca`.
- Adicionado `docs/Capturadores.md` com a direcao comportamental dos papeis do capturador.

## Resultado

O capturador ficou mais fiel a leitura tática esperada: quando a construcao ainda esta em disputa, ele tende a abrir caminho e apoiar o combate antes de retomar a funcao de ponta de lanca.
