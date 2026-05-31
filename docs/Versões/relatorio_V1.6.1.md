# v1.6.1 - Antes do Suprir

## Escopo

Checkpoint defensivo antes da refatoracao do fluxo de suprimento da IA.

## Estado consolidado

- correcoes no fluxo de ataque automatizado para respeitar melhor as regras de engajamento do `AI Unit Profile`
- limite de compra `fallback-save` para evitar que a IA gaste demais enquanto junta recursos para tiers superiores
- manutencao da agressividade normal de fallback em modo de defesa
- penalidade de reserva do hex de captura para impedir que a escolta pise no destino do capturador
- heuristica minima para artilharia, civis e supridores preferirem proximidade com aliados e evitarem avancos mais expostos

## Proximo passo

A proxima etapa sera reestruturar o suprimento para usar hexes adjacentes validos de servico, em vez de tentar navegar para o hex ocupado da unidade alvo.
