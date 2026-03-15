?? Funcional — coisas que podem quebrar silenciosamente

Indicador "acted" (unidade já agiu) — mover uma unidade, confirmar que o visual muda imediatamente
Troca de time — fim de turno, confirmar que todas as unidades atualizam cor/estado correto
Detecção — mover unidade inimiga para range de visão, confirmar que o indicador "detected" aparece
Embarque/desembarque — embarcar unidade num navio, desembarcar, confirmar que indicador detected recalcula
Load de save — carregar partida salva, confirmar que todas as referências resolvem corretamente (o fallback do Start() cobre isso, mas vale confirmar)


?? Performance — o número que importa

FPS em idle no turno do jogador — anota antes e depois pra comparar