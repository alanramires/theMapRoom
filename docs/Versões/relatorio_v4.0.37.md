# v4.0.37 — Performance Fixes

Data: 17/07/2026

## Visão geral

Esta revisão concentra a investigação e a correção dos maiores travamentos percebidos em partidas com muitas unidades. O trabalho cobriu a confirmação de movimentos, o Fog of War incremental, o carregamento de partidas, a restauração do estado e o controle dos logs de diagnóstico.

O objetivo foi reduzir o tempo em que o jogo parece congelado sem alterar alcance, especializações de visão, stealth, persistência de contatos ou as regras transacionais do tabuleiro.

## Fog of War incremental

- A telemetria incremental passou a separar `updateCache`, coleta, renderização, visibilidade das unidades, inteligência da IA, efeitos de detecção, persistência e callbacks.
- O overlay de névoa agora mantém um snapshot das células visíveis já desenhadas e altera somente as células cujo estado realmente mudou.
- O modo de visão `All` deixou de reconstruir mapas de distância redundantes para terra, superfície naval e camada submarina; essas especializações já fazem parte da coleta confirmada principal.
- As passagens virtuais adicionais foram preservadas somente para visão aérea, que é independente do terreno existente sob o hex.
- O `AIIntelLedger` passou a reutilizar o cache de visibilidade recém-calculado, mantendo fallback seguro quando a equipe observadora não possui cache válido.
- Nenhum alcance ou atributo de visão das unidades foi reduzido para obter o ganho.

## Resultados medidos

No submarino de teste, após confirmar movimento:

- tempo incremental total: de aproximadamente **2.856 ms** para **1.320 ms**;
- renderização do FoW: de aproximadamente **1.292 ms** para **17 ms**;
- atualização de inteligência: de aproximadamente **247 ms** para menos de **1 ms**.

No EWACS de teste:

- tempo incremental total: aproximadamente **560 ms**;
- renderização do FoW: aproximadamente **3 ms**;
- atualização de inteligência e persistência: praticamente instantâneas.

Os custos restantes estão concentrados na coleta real de visão, na atualização visual das unidades e, no caso de contatos submarinos, nas regras de detecção e persistência. Esses caminhos foram mantidos por segurança, pois representam regras distintas de “hex visível” e “unidade efetivamente detectada”.

## Carregamento de partidas

- O carregamento recebeu marcadores de desempenho por estágio, cobrindo preprocessamento, JSON, restauração de construções, unidades embarcadas, estado da partida, flags, FoW, replay, jogadas e apresentação do turno.
- A medição revelou duas reaplicações globais do time ativo e das flags das unidades, responsáveis por aproximadamente 16 segundos de espera.
- As reaplicações redundantes foram removidas do fluxo posterior ao load, mantendo a restauração já realizada pela rotina principal.
- Em uma partida de teste extensa, o botão **Iniciar turno** passou a ficar pronto em aproximadamente **5 segundos**, contra cerca de **18 a 21 segundos** antes da correção, desconsiderando o tempo de reação do jogador.

## Logs sob controle

- O AI Manager recebeu `Show AI Logs`, cobrindo mensagens de inicialização, troca de equipe, fases, rotas e transporte.
- O Unit Spawner recebeu `Show Unit Spawner Logs` para mensagens como o ajuste de `NextId` a partir das unidades em cena.
- O SaveGameManager recebeu `Show Save/Load Logs`, incluindo traces e a telemetria detalhada de load.
- O TurnStateManager recebeu `Show Movement Logs` para mensagens específicas de movimento, incluindo movimento confirmado na mesma célula.
- O JogadasManager recebeu uma flag principal para os logs `[Jogadas]`.
- O dispatch do Replay passou a respeitar a flag de logs já existente no ReplayManager.
- O antigo `LogManager`, que duplicava responsabilidades sem centralizar efetivamente os emissores, foi removido. Cada sistema agora controla diretamente seus próprios logs.
- Os inspetores rápidos foram atualizados para expor as novas opções de diagnóstico.

## Estabilidade e apresentação

- O fluxo de confirmação continua publicando o FoW definitivo somente após o compromisso da ação e o retorno a `Neutral`.
- A otimização reutiliza apenas snapshots confirmados; prévias e cancelamentos não alimentam cache definitivo, contatos ou inteligência.
- O Jornal do Comandante recebeu classificação visual por severidade para destacar perdas, ameaças e informações gerais.
- Painéis e fontes receberam ajustes de apresentação e espaçamento para melhorar a leitura de textos extensos.
- Cenas de desenvolvimento e mapas foram atualizados para remover referências ao gerenciador de logs descontinuado e persistir as configurações atuais.

## Validação

- Build de `Assembly-CSharp.csproj`: **0 erros**.
- `git diff --check` executado sem erros de whitespace.
- Testes manuais confirmaram a redução do carregamento e do custo incremental do FoW em submarino e EWACS.
- As regras de visão por camada e o contrato transacional foram preservados.
