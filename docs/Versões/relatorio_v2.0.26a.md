# Relatorio de Atualizacao - v2.0.26a

## AI Refine III a

Esta versao e um ajuste incremental sobre a v2.0.26, focado em permitir que suporte de fogo defensivo use transporte adjacente quando isso ajuda a chegar a uma posicao melhor.

## Em uma frase

O fire support defensivo deixa de ficar preso cedo demais: antes de segurar posicao como estacionario, ele agora pode embarcar em transporte disponivel para ser rebocado ate uma posicao mais util.

## Ajuste principal

- O fluxo de `FireSupport.Defender` agora tenta embarque por `TryDecideAssaultEmbarkAction` antes da decisao de manter posicao estacionaria.
- A decisao usa o plano atual do time para manter coerencia com o objetivo defensivo.
- Isso permite casos como artilharia ou suporte rebocado aproveitando caminhao/supridor adjacente para reposicionamento.
- A mudanca preserva a prioridade de ataque: se houver ataque defensivo valido, ele continua sendo executado antes do embarque.

## Bloco tecnico curto

- Ajustado `AIController.FireSupport.Defender.cs`.
- A ordem passa a ser: atacar se possivel, tentar embarcar em transporte adjacente, depois avaliar hold estacionario.

## Resultado

Versao preparada como pacote `AI Refine III a`, corrigindo um ponto de passividade do suporte de fogo defensivo sem alterar o restante do fluxo da v2.0.26.
