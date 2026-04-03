# v1.5.11 - Antes do Refactor no Plano

## Objetivo
Congelar o estado atual do planner e da IA antes do proximo refactor, com logs de diagnostico, ajustes de replay e refinamento do comportamento tatico por perfil e papel.

## Entrega desta versão
- Instrumentacao do restore e da avaliacao do planner para comparar estado restaurado vs. estado consumido no turno.
- Persistencia e debug mais claros para runtime/plans por time no fluxo de save/load.
- `F9` alinhado ao comportamento do menu: pode aguardar o fim da acao atual e abrir replay pausando a IA.
- Classificacao automatica de unidades em `Combatente`, `Artilheiro`, `Hibrido` e `Civil`.
- Ajustes de engajamento para respeitar classificacao de combate e papel de escolta.

## Pontos principais
1. Planner e restore
- Logs adicionados logo apos `RestorePlannerSaveData` e na entrada de `EvaluatePlanner`.
- Validacao do plano restaurado ficou mais visivel no consumo, com foco em setor, ownership e capturabilidade.
- Base pronta para comparar timing de snapshot contra restauracao de construcoes.

2. Replay e interrupcao da IA
- `F9` agora pode ficar pendente durante uma acao automatizada e abrir quando o cursor voltar para `Neutral`.
- Ao abrir o painel de replay, o jogo entra em estado `Replay`, pausando a automacao da IA no mesmo modelo do menu.

3. Perfis de unidade e combate
- `UnitData` passou a derivar automaticamente a classificacao de combate a partir das armas embarcadas.
- `UnitManager` expoe essa classificacao apenas para leitura em runtime.
- A selecao de alvo e o reposicionamento passaram a distinguir `Combatente`, `Artilheiro` e `Hibrido`.

4. Escolta e coesao de plano
- Escoltas agora priorizam ameacas ao capturador e ao objetivo do plano antes do score bruto de combate.
- Logs de score passaram a expor `escortBand` para explicar por que um alvo foi escolhido dentro ou fora do contexto da missao.
- Isso reduz abandono indevido do destacamento por alvos "sweet" fora da bolha de protecao da captura.

## Arquivos centrais
- `Assets/Scripts/AI/AIPlayerController.cs`
- `Assets/Scripts/Save/SaveGameManager.cs`
- `Assets/Scripts/UI/Replay/ReplayPanelUI.cs`
- `Assets/Scripts/Units/UnitData.cs`
- `Assets/Scripts/Units/UnitManager.cs`

## Observacao
- Esta versao serve como marco anterior ao proximo refactor do plano. Os logs e as regras adicionadas aqui facilitam comparar comportamento antigo vs. comportamento refatorado.
