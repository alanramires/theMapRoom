# Relatorio v1.7.8 - Ferramentas de AI

## Em uma frase

Versao de ferramentas e polimento da IA: transporte de unidades rogue, hot zone no pathfinding de APC, correcao do composition-save, logs de orcamento, e correcao do botao F de fusao.

## O que isso trouxe na pratica

- APCs agora buscam unidades sem plano atribuido e as levam ate a construcao aliada mais avancada.
- O APC desvia de zonas de perigo ao escolher celulas de staging e ao se mover pelo mapa.
- O composition-save nao permite mais que outras construcoes gastem o dinheiro reservado para fechar uma composicao.
- A opcao "F - Fundir" nao aparece mais erroneamente quando a unica unidade adjacente e de tipo diferente.

## Principais melhorias

1. **Transporte de unidades rogue**
- APC identifica unidades terrestres sem atribuicao de plano (rogues) e calcula um ponto de interceptacao no caminho delas para o inimigo.
- Apos embarcar o rogue, o APC o leva ate a construcao aliada mais proxima do QG inimigo.
- Passageiro rogue tambem solicita carona autonomamente, encontrando o APC mais proximo com assento disponivel.

2. **Hot zone no transporte**
- O pathfinding de APC agora inclui penalidade de perigo (mesmo mecanismo do Supridor) ao se mover e ao escolher celulas de staging.
- Celulas a 1 hex de inimigos visiveis recebem penalidade de score, desviando o APC sem bloquea-lo caso nao haja alternativa.

3. **Composition-save com reserva correta**
- O pre-passe `EvaluateCompositionSave` identifica o produtor designado e reserva o custo do item na variavel `strategicReservedMoney`.
- Outras construcoes enxergam saldo zero apos a reserva e nao gastam o dinheiro antes que o produtor execute a compra.
- `TryAssignCompositionSaveReserved` permite que apenas o produtor designado ignore a reserva e conclua a compra.

4. **Logs de orcamento no shopping**
- `Phase3_BuyUnits` agora emite logs `[BUDGET]` por construcao (decisao, custo, motivo) e um resumo final por turno (inicial / gasto / final / compras).

5. **Correcao do botao F - Fundir**
- `PodeFundirSensor.CollectOptions` retornava `true` mesmo quando so havia candidatos invalidos (tipo diferente, camada diferente, etc.), fazendo o botao "F" aparecer sem candidatos validos.
- A checagem no runtime passou a usar `cachedPodeFundirTargets.Count > 0` (apenas candidatos validos), alinhando o botao com o que o sensor debug ja reportava corretamente.

6. **Menu AI Combat HP Simulator**
- Movido de `Tools/AI Combat/` para `Tools/AI/`, centralizando todas as ferramentas de IA no mesmo submenu.

## Bloco tecnico curto

- `AIPlayerController.Transport.cs`: novas funcoes `TryGetEmbarkedRoguePassenger`, `TryGetTransportRoguePickupObjective`, `TryGetTransportPickupObjectiveForRogue`, `FindBestRogueTransporter`, `TryGetNearestEnemyHqCell`, `TryGetMostForwardFriendlyConstruction`; staging cell scoring convertido para score-based com `dangerPenalty`.
- `AIPlayerController.Phase2.cs`: sensores de rogue adicionados ao loop de prioridade; `dangerPenaltyCells` estendido para transportadores.
- `AIShoppingManager.cs`: `EvaluateCompositionSave` + `TryAssignCompositionSaveReserved` + campos `compositionSaveCandidate/Producer/Committed` no `Ctx`.
- `AIPlayerController.Shopping.cs`: logs `[BUDGET]` em todo o fluxo de `Phase3_BuyUnits`.
- `TurnStateManager.Sensors.cs`: gate do codigo 'F' alterado de `canMerge` para `cachedPodeFundirTargets.Count > 0`.
- `AICombatHpSimulatorWindow.cs`: MenuItem reposicionado para `Tools/AI/`.
