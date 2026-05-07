# Relatorio de Atualizacao - v2.0.18

## AI Transporter (part 2)

Esta versao fecha a segunda rodada do pacote de transporte, corrigindo interacoes entre capturadores, captura oportunista, DPQ de batalha e simulacao de HP usada pela IA.

## Em uma frase

A IA passa a respeitar melhor quem deve capturar cada predio, simula combate com DPQ real e ganha uma Matriz de HP mais precisa para auditar decisoes de batalha.

## O que isso trouxe na pratica

- Captura oportunista nao rouba predio de um capturador do plano que consegue capturar naquele turno.
- Se o capturador designado esta longe demais, o oportunista ainda pode capturar o predio.
- Quando um predio oportunista esta reservado, a unidade continua procurando outro predio alcancavel.
- O ataque pos-avanco do capturador passa pelo mesmo `Attack Decision` dos outros ataques.
- O simulador de HP da IA considera DPQ real do atacante e do defensor.
- A Matriz de HP agora permite forcar DPQ diferente para atacante e defensor.
- Logs de decisao de ataque mostram pontos/defesa de DPQ usados na simulacao.

## Principais melhorias

1. Captura oportunista mais fiel ao plano
- O oportunista verifica se o predio pertence a um objetivo com capturador atribuido.
- A reserva so bloqueia o oportunismo quando esse capturador consegue capturar o predio no turno.
- Predios reservados sao pulados individualmente, permitindo capturar outro alvo valido no alcance.

2. Correcao do fallback de ataque
- O capturador ja avaliava ataques com `PassesAttackDecision`, mas o ataque depois do `bestMove` podia escapar desse gate.
- Esse caminho agora tambem valida perda de HP, dano minimo, sobrevivencia e contexto defensivo.
- Ataques bloqueados por `hpLoss` nao reaparecem por uma segunda rota de decisao.

3. DPQ real no Attack Decision
- O simulador de combate da IA deixou de assumir DPQ padrao fixo `1x1`.
- `PassesAttackDecision` resolve DPQ do hex de ataque e do hex do alvo.
- Pontos de DPQ e bonus de defesa entram na previsao de HP antes de aceitar ou bloquear o combate.

4. Matriz de HP especializada
- `Tools > Combat > Matriz de HP` ganhou seletores separados de DPQ do atacante e DPQ do defensor.
- A matriz 10x10 usa esses DPQs na diferenca de matchup e na defesa efetiva.
- O log de cada celula mostra pontos, defesa, diferenca DPQ, outcome e resultado de HP.

5. Debug mais auditavel
- Logs de `Attack Decision` exibem `dpq=a/d` e `def=a/d`.
- Isso permite conferir rapidamente se uma montanha, floresta, estrutura ou predio entrou na conta.
- A ferramenta agora reproduz melhor a mesma pergunta que a IA faz antes de aceitar um combate.

## Bloco tecnico curto

- Ajustado `AIController.Capturer.cs` para validar `PassesAttackDecision` tambem no ataque pos-avanco.
- Ajustados `AIController.Capturer.cs` e `AIController.Capturer.Helpers.cs` para continuar varrendo predios oportunistas apos uma cedencia.
- Ajustado `AIController.AttackDecision.cs` para resolver DPQ real das celulas envolvidas.
- Ajustado `AICombatHpSimulator.cs` para aceitar pontos e bonus de defesa de DPQ por lado.
- Ajustado `CombatHpMatrixWindow.cs` para expor DPQ do atacante/defensor e aplicar esses valores na matriz.

## Resultado

Versao preparada como pacote `AI Transporter (part 2)`, focada em tornar a IA de captura e combate mais previsivel, auditavel e coerente com DPQ real do mapa.
