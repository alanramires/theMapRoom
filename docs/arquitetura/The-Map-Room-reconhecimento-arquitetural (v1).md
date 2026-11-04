# The Map Room — reconhecimento arquitetural revisado, v9.4.0

**Data:** 08/10/2026. **Branch:** `main`. **Commit:** `7afcd951e119bd34270794d93dec744d7aa7d5ed` (08/10/2026, 12:14:47, UTC−3).
**Comparação:** análise inicial de `3da75fd42d547d5803197f664ea04c4d8f1a8377`.

## Parecer atualizado

A v9.4.0 melhora a coerência do motor sem desmontar a máquina de ações consolidada. As mudanças mais relevantes são funcionais: fórmula de combate compartilhada, resolução de posição/DPQ comum à execução e previsão, avaliação de ataques reais baseada na opção do sensor e um sinal explícito após o delta confirmado de FOW.

O diagnóstico deixa de ser “há fórmulas duplicadas a unificar” e passa a ser “há um núcleo compartilhado a validar, com atenção aos dados de entrada e aos caminhos de aborto”. A prioridade agora é confirmar essas fronteiras com cenários representativos. O tamanho do TurnStateManager permanece um custo de manutenção, não uma indicação de refactor obrigatório. A decisão do autor de preservar essa estrutura foi incorporada a esta revisão.

## Método e limites

Conferi o commit publicado, seu diff contra a revisão inicial, o relatório `docs/relatorio_v9.4.0.md` e os trechos de implementação dos sistemas alterados. Usei uma extração separada do commit para leitura; não editei código, assets ou documentação do projeto, não fiz commit ou push.

Esta é uma revisão estática. Não executei Unity, player, teste de gameplay, compilação ou profiling. Traces e validações manuais descritos pelo autor/Codex no relatório da versão são evidência relatada, não testes executados nesta sessão. Centralizar código reduz oportunidades de divergência; não comprova sozinho equivalência de todos os resultados.

## Arquitetura atual e dependências

A organização geral continua a mesma, com melhorias em duas fronteiras centrais:

| Sistema | Responsabilidade e dependências |
|---|---|
| Campanha e cenários | Dados de mundo/bloco/campanha/quadrante alimentam a batalha. Progresso por slot recebe os resultados da partida. |
| MatchController | Turno, dinheiro, slots, vitória e grande parte de FOW/detecção. Recebe deltas confirmados das ações. |
| TurnStateManager | Coordena seleção, planejamento provisório, opção desejada, execução, cancelamento e retorno a Neutral. Mantém os fluxos específicos em arquivos partial. |
| Dados/ScriptableObjects | Unidades, armas, serviços, terrenos, construções, RPS e DPQ configuram o comportamento. |
| Topologia, ocupação e movimento | Índice confirmado, regras por camada, caminhos e cache de alcance sustentam sensores e planejamento. |
| Sensores | Determinam ações e opções oferecidas. PodeMirar fornece arma e revide ao caminho canônico de previsão. |
| Combate | **CombatFormula** calcula; **PositionDpqResolver** resolve posição/camada; execução aplica efeitos e simulador prevê resultados. |
| IA | Snapshot, objetivos, iniciativa, serviços táticos e compras. Executa por batches/automação e usa opções do sensor para ataques de gameplay nos caminhos alterados. |
| Save/replay | DTO versionado, hash canônico, snapshots e execução automatizada. Continuam dependentes da FSM e do estado confirmado. |
| UI/tutorial | Apresentação e aulas. Resultado de aula perdida agora tem oponente vencedor e proteção do progresso já conquistado. |

Unity permanece 6000.5.3f1. Pacotes e fronteiras de assemblies não mudaram no diff. Há 435 arquivos C# fora de `AI_Legacy~`, aproximadamente 227.316 linhas físicas incluindo editor/comentários; TurnStateManager soma 32.942 linhas, arquivos AIController 52.694 e MatchController 12.001. Esses números dimensionam o projeto, não medem sua qualidade ou desempenho.

## O que mudou em relação à análise inicial

| Achado anterior | Veredito na v9.4.0 |
|---|---|
| Fórmula duplicada entre previsão e execução | **Tratado no código:** ambos chamam CombatFormula.Resolve. Restam validações das entradas e efeitos. |
| DPQ com diferenças entre consumidores | **Tratado nos caminhos alterados:** PositionDpqResolver concentra a regra por camada; execução e previsão chamam esse resolvedor. |
| Ataques avaliados pela ficha em gameplay | **Tratado nos caminhos alterados:** defensor do capturador e nota da automação usam opção do PodeMirar, HP/DPQ reais e estado de aeronave pousada. |
| Neutral anunciado antes do FOW | **Contrato explicitado:** OnCursorReturnedToNeutral preserva reconciliação de ocupação; OnBoardSettledAtNeutral vem após aplicação do delta. |
| Guardas incompletas para Executing | **Tratamento parcial deliberado:** captura, fusão e ataque entram em reset-only; suprimento, desembarque e embarque mantêm abortos por Retreat. |
| Esperas de fases IA sem diagnóstico | **Melhorado:** watchdog a cada 20 segundos, respeitando pausa e término de partida. Não recupera automaticamente uma ação travada. |
| Duração de emersão com constante sem dono | **Tratado:** WeaponData.forcedEmergeTurns substitui o valor fixo por dano, padrão 2. |
| Cobertura de gameplay/CI | **Permanece:** não há novos testes automatizados de gameplay ou workflows no diff. |
| Save/load e determinismo entre máquinas | **Permanece a validar:** esses sistemas não foram alterados nesta versão. |

### Correções necessárias à leitura anterior

**Distância zero.** Minha conclusão anterior sobre a fragata foi ampla demais. CombatEvaluationService chama SimulateWithWeapons quando recebe opção canônica; essa chamada não recebe distância e não rejeita zero. O Max(1, distância) e a rejeição de zero pertencem ao caminho por ficha/legado. A v9.4.0 também remove seu uso dos caminhos de automação inspecionados. Não mantenho “a IA não consegue usar a fragata” como diagnóstico geral.

**Desembarque e informação oculta.** A mensagem com o nome do bloqueador permanece no sensor, mas a lista de opções inválidas inspecionada é consumida pelo LogScannerPanel de diagnóstico. Não há comprovação de vazamento nominal na UI normal. A versão registra como design aceito que a opção desapareça quando o desembarque está bloqueado; isso deve orientar a leitura das regras anteriores. A proibição de nomear inimigo oculto ao jogador continua sendo uma fronteira a preservar.

**Emersão.** Exposição de quem disparou e emersão imposta ao alvo são efeitos distintos. Não precisam compartilhar uma duração. A melhoria correta foi dar uma fonte configurável à duração do efeito da arma, mantendo separado UnitData.emergeAfterAttackTurns.

**Buscas globais.** Encontrar FindObjectsByType não demonstra gargalo. O relatório da versão explica que o caminho de FOW relevante não usa essa consulta de ocupante. O risco de desempenho fica dependente de profiling, sem tratá-lo como problema confirmado.

## A máquina consolidada

O modelo conceitual continua coerente: **Neutral → seleção/planejamento → posição provisória → decisão → execução → fechamento em Neutral**. Movimento parado e andando são alternativas de planejamento; não publicam, por si, uma descoberta definitiva do mundo.

No fechamento da implementação atual:

1. A pilha retorna a Neutral.
2. OnCursorReturnedToNeutral permite reconciliação de ocupação confirmada.
3. MatchController aplica o delta confirmado pendente.
4. OnBoardSettledAtNeutral anuncia a conclusão dessa aplicação.

Essa ordem preserva a dependência “ocupação antes da névoa” e nomeia o subpasso interno de Neutral. Não é necessário criar outro estado principal para representar o recálculo.

**Limite da mudança:** não encontrei assinaturas de OnBoardSettledAtNeutral em consumidores do código C# ativo. O novo sinal existe, mas ainda não substitui os eventos antigos de replay/automação/visuais. Os listeners visuais continuam recebendo OnFogOfWarUpdated. Portanto, a mudança estabelece uma fronteira mais clara; não demonstra migração completa de todos os consumidores para “mundo pronto”. Também não introduz, por si, um bloqueio geral de input durante qualquer recálculo futuro assíncrono.

## Riscos restantes, em ordem de interesse

### 1. Abortos dentro de Executing

Há retornos por Retreat em embarque, desembarque e suprimento. Isso é deliberado; a guarda foi ajustada para não classificar todos como reset-only. Mas “Executing” não significa uniformemente “toda mutação já é irreversível” nesses caminhos.

O teste importante é falhar ou abortar depois de uma transição/animação temporária e verificar posição, camada, grounded, passageiros, combustível, estoques, HasActed, FOW e inteligência. Um aviso de delta confirmado aplicado na volta por Retreat deve ser investigado, não usado como prova de rollback correto. Esta é a principal área de validação da máquina sem reorganizar o TurnStateManager.

### 2. Entradas do cálculo compartilhado

A fórmula agora é única, mas revide, seleção de arma, DPQ, HP e grounded continuam fornecidos por chamadores. Os novos caminhos diminuem diferenças de entrada. Comparar previsão e execução para ataque com/sem revide, distância zero, aeronave pousada, submerso e modificadores continua necessário.

O código distingue calculadora de balanceamento por ficha de avaliação contextual. UnitCounterEvaluator ainda usa ficha para cobertura/matchup e compras, o que não equivale a simular um ataque legal de uma unidade concreta.

### 3. Avaliação inválida com opção existente

BlockedNoSensorOption bloqueia corretamente a ausência de opção nos pedidos sem fallback legado. Porém BuildSimulationInvalidResult continua retornando IsAllowed=true quando existe opção mas a simulação é inválida. É compatibilidade deliberada, não falta de implementação do novo bloqueio. Convém observar esses casos e decidir a política com dados de gameplay; não mudar automaticamente o comportamento.

O fallback legado permanece no serviço, mas não encontrei atribuição que o ligue nos consumidores ativos pesquisados. Não o trato como desvio de gameplay atual.

### 4. Sincronização dos consumidores de Neutral

Separar chegada a Neutral de aplicação do delta é uma melhoria. Replay e automação ainda escutam o evento anterior; acompanhar se dependem apenas da FSM ou também do novo estado de FOW. Na execução síncrona atual, disparar o evento cedo não prova que outra ordem ocorrerá antes da conclusão. Uma eventual mudança para recálculo distribuído entre frames exigirá um critério explícito de prontidão.

### 5. Validação automatizada e persistência

A fórmula compartilhada agora torna viável um teste pequeno de alto valor. O relatório da versão registra dois traces manuais, sem uma suíte nova. Save/load por hash e canonicalização completa do planner/intel continuam pendentes; ausência de RNG não comprova determinismo entre máquinas.

### 6. Desempenho e concentração de responsabilidades

Watchdog melhora diagnóstico, não reduz custo de planejamento nem garante recuperação. A dimensão dos controladores permanece; a recomendação é medir e proteger fluxos existentes, respeitando a decisão de manter a estrutura. Não há evidência nova que justifique DOTS/ECS ou reescrita ampla.

## Melhorias de campanha e tutorial

A perda de aula passa a registrar o oponente como vencedor. QuadranteController evita que TutorialFailed substitua a conquista de um humano e retranque progressão já obtida. A proteção foi confirmada no diff; combinações de slots/progresso ainda precisam de gameplay.

Os assets e roteiros também mudaram: captura precoce na aula 1 desperta a guarnição, aula 2 foca reabastecimento e extração passa ao rascunho da aula 3. São mudanças de conteúdo e ensino além da engenharia. Não validei todas as condições dos JSONs e assets.

## Próxima verificação recomendada

| Verificação | Resultado esperado |
|---|---|
| Aborto de embarque, desembarque e suprimento | Estado confirmado anterior preservado, sem custo ou informação residual |
| Previsão versus execução em casos contrastantes | Mesmos HP previstos e realizados, com arma/revide/DPQ idênticos |
| IA sem opção do sensor | BlockedNoSensorOption, sem ataque inventado pelo fallback |
| Fechamento da ação | Ocupação reconciliada antes do delta; sinal settled após sua aplicação |
| Save → load → hash | Estado autoritativo equivalente; divergências localizadas pelo dump |

**Conclusão:** a v9.4.0 resolve boa parte das duplicações e explicita melhor o fechamento da máquina. A próxima etapa é verificar abortos e equivalência de resultados, mantendo a arquitetura consolidada.

## Evidências fixadas no commit

- [Relatório da versão](https://github.com/alanramires/theMapRoom/blob/7afcd951e119bd34270794d93dec744d7aa7d5ed/docs/relatorio_v9.4.0.md)
- [CombatFormula](https://github.com/alanramires/theMapRoom/blob/7afcd951e119bd34270794d93dec744d7aa7d5ed/Assets/Scripts/Combat/CombatFormula.cs)
- [PositionDpqResolver](https://github.com/alanramires/theMapRoom/blob/7afcd951e119bd34270794d93dec744d7aa7d5ed/Assets/Scripts/DPQ/PositionDpqResolver.cs)
- [CombatEvaluationService](https://github.com/alanramires/theMapRoom/blob/7afcd951e119bd34270794d93dec744d7aa7d5ed/Assets/Scripts/Combat/CombatEvaluationService.cs)
- [TurnStateManager](https://github.com/alanramires/theMapRoom/blob/7afcd951e119bd34270794d93dec744d7aa7d5ed/Assets/Scripts/Match/TurnStateManager.cs)
- [Eventos do cursor](https://github.com/alanramires/theMapRoom/blob/7afcd951e119bd34270794d93dec744d7aa7d5ed/Assets/Scripts/Cursor/CursorController.cs)
