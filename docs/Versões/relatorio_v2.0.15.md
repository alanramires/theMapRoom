# Relatorio de Atualizacao - v2.0.15

## AI Refine - parte 3 (defenders)

Esta versao consolida a terceira rodada de refinamento da IA, com foco no comportamento de defensores, reparo sem destino, ocupacao apos fusao e escolha de terreno defensivo durante combate.

## Em uma frase

A IA passa a distinguir melhor entre mover para cobertura, lutar a partir do melhor DPQ disponivel e preservar unidades em reparo quando nao existe destino de conserto imediato.

## O que isso trouxe na pratica

- Defensores fora do predio defendido agora podem avancar para interceptar ameacas proximas sem abandonar a zona defensiva.
- A escolha de ataque passou a priorizar DPQ de batalha quando a unidade consegue lutar a partir de um terreno superior.
- Hexes ocupados por unidades mortas ou removidas por fusao deixam de bloquear decisoes de movimento.
- Unidades em reparo sem destino valido retornam para o HQ ou aguardam no fim da iniciativa quando a situacao esta segura ou cercada.

## Principais melhorias

1. Defensores mais ativos
- O defensor do setor avalia ameacas proximas que ainda nao consegue atacar no turno atual.
- Quando vale a pena, ele avanca para uma celula de interceptacao que reduz distancia ate o inimigo, mas continua dentro da area defensiva do objetivo.
- A escolha considera terreno, DPQ/EV, ameaca local, custo de caminho e distancia ao ponto defendido.

2. DPQ de batalha corrigido
- A flag de DPQ de movimento continua restrita ao uso logistico.
- A flag de DPQ de batalha agora influencia a escolha do hex de ataque quando a unidade consegue alcancar um terreno melhor.
- O desempate de ataque separa prioridade do alvo, DPQ do hex de combate e score tatico geral.

3. Ocupacao e fusao mais consistentes
- Regras de ocupacao passaram a ignorar unidades mortas.
- O estado de fusao nao e mais usado como criterio simples para remover uma unidade viva da ocupacao.
- Reservas antigas de destino planejado deixam de manter hexes vazios bloqueados em decisoes futuras da IA.

4. Reparo sem destino
- Quando nao existe construcao aliada valida para reparo, a unidade tenta voltar para o HQ.
- Se ja estiver em zona segura, ou em cerco perto do HQ sem rota melhor, a unidade passa a ir para o fim da fila de iniciativa.
- O comportamento evita gastar prioridade com uma unidade avariada que nao tem acao util imediata.

## Bloco tecnico curto

- Ajustado `AIController.Capturer.Defender.cs` para incluir movimento de interceptacao defensiva.
- Ajustados `AIController.Capturer.cs` e `AIController.Capturer.Helpers.cs` para comparacao de ataque com DPQ de batalha.
- Ajustado `AIController.Repair.cs` para fallback de retorno ao HQ quando nao ha destino de reparo.
- Ajustado `AIController.Initiative.cs` para atrasar iniciativa de unidades em reparo quando a area esta segura ou quando a unidade ja esta cercada perto do HQ.
- Ajustadas regras de ocupacao em `HexOccupancyQuery`, `UnitOccupancyRules`, `UnitMovementPathRules` e confirmacao de turno para ignorar unidades mortas.

## Resultado

- Versao preparada como continuidade do pacote `AI Refine`, fechando a rodada de correcoes dos defensores e dos efeitos colaterais de fusao/reparo.
