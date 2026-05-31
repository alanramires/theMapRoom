# v2.0.9 - AI Capturador - Refinamentos

## AI Capturador

- Refinado o desempate de movimento do capturador: primeiro aproxima do setor atribuido, depois usa a direcao do HQ inimigo como criterio secundario.
- O capturador agora evita captura oportunista quando outro capturador ja esta designado e consegue chegar ao mesmo alvo.
- Unidades que bloqueiam fisicamente o alvo de captura de outro capturador ganham prioridade de iniciativa quando ha inimigos por perto.
- Defensores em construcoes passaram a ter prioridade de ataque maior, com prioridade maxima para o defensor no predio objetivo.
- Unidades com preferencia de DPQ podem reposicionar antes de atacar, e blockers podem sair do alvo para liberar o hex do capturador designado.

## Plano e Handoff

- Setores de alto risco agora exigem co-chegada: a AI evita enviar capturador solo quando a segunda unidade esta distante demais.
- O custo de atribuicao passou a considerar impacto de risco configuravel, reduzindo escolhas agressivas demais em setores perigosos.
- A cascata de objetivos ficou restrita a distribuicao inicial e agora usa apenas vizinhos em direcao de avanco.
- O handoff ganhou swap: se nao houver substituto livre, uma unidade sticky ja posicionada pode assumir o objetivo quando a troca for vantajosa.
- Logs do plano agora mostram distancias reais de terreno, facilitando leitura do comportamento da AI.

## Debug e Ferramentas

- Adicionada a janela `Tools > AI > Plan Evaluator` para inspecionar objetivos, unidades, matriz de distancia, multiplicador de risco e linhas no SceneView.
- `AI Stage` pode resetar o plano antes de reconstruir objetivos quando a opcao de debug estiver ativa.
- Preview de ataque agora mostra movimento quando a acao inclui reposicionamento antes do disparo.
- F9 ficou reservado para atalhos de debug da AI; o painel de replay nao intercepta mais essa tecla.

## Persistencia

- Save/load agora preserva a configuracao `commandServiceAutomatic` por jogador.

