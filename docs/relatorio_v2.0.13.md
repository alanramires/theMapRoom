# Relatorio de Atualizacao - v2.0.13

## AI Tatical Router

Esta versao registra o refinamento da IA tática ao redor do roteamento de capturadores, perseguidores, rogues e estados de menu apos persistencia.

## Em uma frase

A IA passou a priorizar melhor o alvo tatico correto antes de deixar DPQ, movimentacao ou desempates locais decidirem a jogada.

## O que isso trouxe na pratica

- Capturadores perseguidores desentocam primeiro quem esta sobre o objetivo, escolhendo depois o melhor hex de ataque disponivel.
- Unidades rogue podem aproveitar avancos rumo ao HQ para atacar inimigos visiveis quando houver oportunidade real.
- Objetivos defensivos preservam defensores enquanto houver ameaca proxima, evitando redistribuicao precipitada.
- Save/Load aberto pelo menu volta para `Neutral` e fecha o menu quando a operacao e confirmada.

## Principais melhorias

1. Prioridade tática de alvo
- O ataque do capturador agora avalia o par `alvo + hex`.
- Inimigos em cima da construcao objetivo tem prioridade sobre alvos perifericos.
- O DPQ passa a escolher o melhor ponto para executar a prioridade, nao a substituir a prioridade.

2. Perseguidor mais coerente
- O perseguidor considera move+attack antes de ficar parado atacando.
- Quando nao consegue alcancar o alvo principal, avalia inimigos nos arredores do objetivo e da posicao atual.
- O comportamento fica mais proximo da regra: desentocar objetivo, lutar nos arredores, depois mover.

3. Rogue mais oportunista
- Unidades sem objetivo fixo podem atacar inimigos visiveis durante o avanco ao HQ.
- Isso reduz movimentos passivos quando a unidade ja possui uma linha de ataque util no caminho.

4. Plano defensivo mais estavel
- Setores ja conquistados e sob ameaca entram em `Defending`.
- Defensores nao sao removidos por handoff ofensivo enquanto o objetivo estiver em defesa.
- Reforcos defensivos preferem unidades proximas ao HQ, preservando unidades ja posicionadas na frente.

5. Menu apos Save/Load
- Confirmar salvar ou carregar pelo menu limpa o prompt e reseta a FSM para `Neutral`.
- Cancelar com `ESC` continua retornando ao menu.
- Confirmar uma operacao nao reabre mais `PlayerMenu` depois do fim do estado `Saving` ou `Loading`.

## Bloco tecnico curto

- Ajustado `AIController.Capturer.cs` para pontuar `alvo + hex` no move+attack.
- Ajustado `AIController.Capturer.Pursuer.cs` para preservar prioridade de combate antes do avanco simples.
- Ajustado `AIController.Capturer.Rogue.cs` para permitir move+attack em avancos ao HQ.
- Ajustado `AIController.PlanEvaluator.cs` para preservar objetivos defensivos e ordenar reforcos.
- Ajustado `SaveGameManager.cs` para finalizar persistencia confirmada em `Neutral`.

## Validacao

Build C# executado:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
```

Resultado: 0 erros. Permanecem warnings antigos de APIs obsoletas do Unity.
