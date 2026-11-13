# Checklist — desligar logs e diagnóstico antes de preparar a versão

Levantado em 2026-11-12, depois da rodada de desempenho (relatório do dia). Complementa a
janela `Tools ▸ Publicação ▸ Checklist` (`Assets/Editor/PublicacaoChecklist.cs`): ela só
sabe desligar **flag serializada** nas cenas do build. Tudo o que está hardcoded fica de
fora dela e vai para o jogador. Este arquivo lista os dois grupos.

Cenas do build hoje: Tela de Entrada, Campanha, Batalha (`EditorBuildSettings.asset`).

## A. O que a janela já desliga (conferir, não refazer)

Valores lidos da cena **Batalha** em 2026-11-12, antes do Preparar:

| Flag | Componente | Hoje | Publicar |
|---|---|---|---|
| `startOnPause` | AIController | 1 | 0 |
| `showAILogs` | AIController | 1 | 0 |
| `showAIUnitHUD` | AIController | — | 0 |
| `aiDebugShortcutsEnabled`, `debugShortcutsEnabled`, `panelRodadaDesativadoPvpDebug` | DebugManager | — | 0 |
| `gravarDiagnosticoNoSave` | DebugManager | — | 1 |
| `enableTurnPerfLogs` | MatchController | 1 | 0 |
| `enableFog*Logs`, `enableSensorsRuntimeLogs`, `enablePode*Logs` | MatchController | 0 | 0 |
| `enablePathfindingDebugLogs` | PathManager | 0 | 0 |
| `enableTurnStateRuntimeLogs`, `showMovementLogs` | TurnStateManager | 1 | 0 |
| `enableRangeCacheDebugLogs`, `showFrameSpikeLogs`, `showClickSpikeLogs` | TurnStateManager | — | 0 |
| `enableReplayRuntimeLogs` | ReplayManager | — | 0 |
| `verboseLogs` | SaveGameManager | — | 0 |
| `sectorLog` | SectorManager | 0 | 0 |
| `aiStep` | AIController | 0 | 0 (não está na janela; conferir à mão) |

Armadilha conhecida (resumo, "Regras de trabalho"): **"Voltar ao debug" só restaura o que o
"Preparar" mudou.** Flag desligada à mão antes do Preparar não volta sozinha.

## B. O que vai para o jogador mesmo com tudo acima desligado

Logs sem flag nenhuma. Cada linha custa formatação + Console/arquivo de log; em Web vira
`console.log`. Contados em 2026-11-12.

| Prefixo | Onde | Quando dispara | Gate hoje |
|---|---|---|---|
| `[AI AMARELO][T#][…]` (via `TL()`) | 541 chamadas em `Assets/Scripts/Match/AI/**` | toda decisão da IA | **nenhum**: `showAILogs` gateia só ~12 delas |
| `[AI Perf] Stage0…TURNO TOTAL`, `BuildObjectivePlan` com marcas por passo | `1. Phases/AIController.Phases.cs` (14 linhas) | 1 vez por fase, por turno | nenhum |
| `[AI Perf][InitiativeSetup]`, `[AI Perf][Unit]`, `[AI Perf][Phase2 Breakdown]` | `1. Phases/AIController.Phase2.cs:127,609,629` | por unidade e por turno | nenhum |
| `[AI Perf][ResumeSettle]`, `[AI Perf][PostLoadReady]` | `Phases.cs:254-304`, `AIController.Lifecycle.cs:291` | load / retomada | nenhum |
| `[AI Commit Heavy] …` (6 linhas) | `AIController.WorldCommit.cs:208-252` | 2 vezes por turno | nenhum |
| `[AI Turn Transition Timing]` (~15 linhas) | `1. Phases/AIController.Phase4.cs:24`, chamado em Phase0/1/4, Phases.cs, Debug.cs | por turno da IA | nenhum (entrou em 2026-11-12) |
| `[AI Shopping Timing]` | `1. Phases/AIController.Phase3.cs` | compra | `aiStep` — ok |
| `[AI Ops] …` do analisador tático | `3. Shopping/Demand/AITacticalAnalyzer.cs:189` e vizinhos | por turno | parâmetro `log` — conferir quem passa `true` |
| `[SectorManager][Perf]` e `[Perf][Steps]` | `Construction/SectorManager.cs:1559-1565` | por rebuild de setores | só `Application.isPlaying` |
| `[FoW][LoadCacheRestore]` | `Save/SaveGameManager.cs:2382-2438` | por load | nenhum (`verboseLogs` não cobre) |
| `[PathBFS][OccupancyDebug]` | `Units/Rules/UnitMovementPathRules.cs` | por célula | `PathManager.IsPathfindingDebugLogsEnabled` — ok |

Diagnóstico por arquivo (memória "Diagnóstico por arquivo"): procurar `File.AppendAllText`
em `Assets/Scripts` antes de fechar; instrumentação de caso fechado tem de sair.

## C. Como fechar o grupo B — duas opções, uma recomendada

**Recomendada: uma chave só, no build.** Em `#if !UNITY_EDITOR`, num bootstrap
(`RuntimeInitializeOnLoadMethod`), `Debug.unityLogger.filterLogType = LogType.Warning`.
Some todo `Debug.Log` do jogador, mantém `LogWarning`/`LogError` (que são os que importam
num relato de bug), e nenhuma das 541 linhas precisa ser tocada. Custo residual: a
interpolação das strings ainda roda; é pequeno perto do log em si. Uma flag de
`DebugManager` (`manterLogsNoBuild`, default false) deixa reabrir para um build de teste.

**Alternativa, se quiser ver log no build:** criar `showAIPerfLogs` no `AIController` e
gatear `[AI Perf]`, `[AI Commit Heavy]`, `[AI Turn Transition Timing]`; `enableSectorPerfLogs`
no `SectorManager`; estender `verboseLogs` ao `[FoW][LoadCacheRestore]`; e trocar
`Debug.Log($"{TL(` por um `LogAI(...)` que confere `showAILogs` (mass edit de 541 linhas).
Depois, adicionar cada flag nova à lista `Items` da janela. Mais trabalho e mais lugares
para esquecer.

As duas convivem: a chave do build é a rede; as flags continuam servindo no Editor.

## D. Ordem no dia da versão

1. `git status` limpo e tag do fechamento feita (regra "tag antes do churn").
2. `Tools ▸ Publicação ▸ Checklist` → **Preparar para publicar**. Conferir na janela que
   nenhum item ficou vermelho; conferir `aiStep` à mão na Batalha.
3. Conferir que a chave do grupo B existe no build (opção C recomendada) ou que as flags
   novas entraram na janela.
4. Itens manuais da janela: Web sem LTO; jogar 1 turno no build gerado.
5. Rodar um turno da IA no build e abrir o log do Player (`Player.log` / console do
   navegador): deve haver **zero** linhas `[AI` e `[…Perf]`.
6. Depois do build, **Voltar ao debug** e conferir `git diff` das três cenas: só o que o
   Preparar mudou volta.

Fora de escopo aqui, mas no mesmo dia: áudio sem licença (memória "Áudio sem licença"),
tutoriais fora do menu (decisão do autor), texturas Web 512/1024 (a janela já cobre).
