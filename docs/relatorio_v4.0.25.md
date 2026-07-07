# v4.0.25 - AI Eixo

Rodada focada nos EIXOS de invasão da AI: um sistema de override manual por slot para corrigir a classificação automática, ferramentas de inspeção/desenho no editor, e correções de comportamento da AI (estabilidade de atribuição, blitzkrieg no Hard e handoff da ponta de lança).

## Override manual de eixo (por slot)

- `ConstructionManager` ganhou uma LISTA de overrides de eixo (`EixoOverrideEntry { slotIndex, eixo }`). O mesmo setor pode ser nó do leque de um slot e rally de outro, então cada entrada fixa o eixo para um slot específico (ou `-1` = todos).
- `InvasionAxisMap.Build` aplica os overrides (`ApplyEixoOverrides`) depois da numeração dos eixos: remove o setor do corredor automático e o reinsere no corredor do eixo escolhido (ordem por distância ao HQ), recomputando a frente de todos. Integração completa — vale para `GetEixo`, transporte, frente, balanceamento e desenho do editor.
- Setores de rally não são reatribuíveis (eles definem o eixo); override `0` (ou eixo inexistente) tira o setor de qualquer eixo.

## Ferramentas de editor

- **ConstructionManager**: bloco "Override Eixo (por slot)" com dropdown de slot + dropdown de eixo mostrando a sigla (`E2: B-F` = Bravo→Foxtrot), marcação `(atual)` do eixo geométrico e linha "auto (sem override)" de referência.
- **SectorManager**: painel read-only "Eixo Infos" listando, por slot, cada eixo com nome auto (`E1 Bravo→Foxtrot`), caminho de nós (frente marcada com `>`), frente, rally e nº de setores participantes.
- **SectorManager**: filtro de eixos agora é por SLOT (era por time; o time é resolvido do slot) e novo toggle "Ocultar eixo de invasão (visual)" para desenhar só os eixos principais.

## Setores de base 0-indexados

- `Base1..Base4` renomeados para `Base0..Base3` (para casar com os slots `0..3`), preservando os valores inteiros — cenas e saves serializados por int não quebram; o nome é só rótulo (a atribuição continua no campo `slotIndex` da construção).

## Estabilidade de atribuição de capturador

- Histerese de objetivo: um capturador ganha um bônus para ser reatribuído ao MESMO setor do turno anterior, evitando que o otimizador global de atribuição embaralhe unidades que já estavam a caminho de um objetivo (o "soldado pulando de eixo").

## Blitzkrieg (Hard) — compras

- O primeiro elite terrestre prioriza o MBT (peça de ruptura) sobre o Obus, no shopping por papéis.
- Poupança bootstrap para o 1º MBT: enquanto não há blindado elite em campo e há demanda de assalto, a AI segura o caixa (liberando só um corpo barato por rodada) até poder pagar o MBT mais a gordura de manutenção (Serviço do Comando), com horizonte de até 3 rodadas. Só poupa se o MBT for alcançável com a renda líquida.

## Handoff da ponta de lança

- Corrigido o handoff blitz quando a ponta está EM CIMA do prédio parcial: um seguidor do eixo que alcance um vizinho do prédio agora assume, liberando a ponta para avançar. Antes, o próprio bloqueio da célula impedia o handoff e a ponta ficava terminando a captura.

## Validação

- Alterações de script em Construção (`ConstructionManager`, `ConstructionSector`, `EixoOverrideEntry`, `ConstructionFieldEntry`), Planner (`InvasionAxisMap`, `AIController.PlanEvaluator`, `.Handoff`), Shopping (`AIShoppingPlanner`) e editores (`ConstructionManagerEditor`, `SectorManagerEditor`).
- Pendente de verificação no Editor/Play mode: override de eixo refletindo no "Desenhar eixos"/"Eixo Infos" por slot; dropdown de sigla; bases exibindo Base0-3; e, em partida Hard, a poupança do MBT, o handoff da ponta e capturadores estáveis (sem saltar de eixo).
