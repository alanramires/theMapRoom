# 12 - Sistema de IA do Projeto

Data base: 2026-05-25

## Visao geral: cadeia de comando em tres camadas

A IA opera como uma forca-tarefa com cadeia de comando clara. Tres camadas coordenadas constroem o turno de dentro para fora:

```
AIOperationManager      ← Comando: detecta ameacas, cria Operacoes com slots de necessidade
ObjectiveManager        ← Plano Territorial: gerencia SectorObjective por setor do mapa
AIShoppingPlanner       ← Aquisicao: compra o que o Comando pede via GetDeficits()
        |
        v
AIController            ← Unidades: executam dentro dos planos ou como rogues
```

Toda a logica esta em `Assets/Scripts/Match/AI/`. `AIController` e uma `partial class` com ~45 arquivos. `AIOperationManager` e `AIShoppingPlanner` sao `MonoBehaviour` singletons separados.

---

## Camada 1 — AI de Comando: `AIOperationManager`

**Arquivo:** `AIOperationManager.cs`

Reconstroi operacoes taticas a cada turno via `Rebuild(team, snapshot, plan)`. Identifica ameacas no campo e responde com operacoes que definem *quem e necessario e onde*.

### Tipos de operacao (`AIOperationType`)
| Tipo | Gatilho |
|---|---|
| `BaseDefense` | Aeronaves ou blindados proximos ao HQ, ou captura ativa de construcao propria |
| `SectorDefense` | Setor aliado com inimigo proximo ou em captura parcial |
| `AirliftCapture` | Objetivo de captura com slot de Transportador no plano, deficit de capturadores/helicopteros |
| `AirRefuelSupport` | Aeronaves proprias com combustivel baixo (< 35%) sem tanque ativo |
| `PreventiveDefense` | Falta de artilharia/AAA/SAM de base apos turno minimo e com orcamento adequado |
| `GroundCapture` | (enum definido, logica no plano de objetivos) |
| `AirInterception` | (enum definido, uso futuro) |
| `Reserve` | (enum definido, uso futuro) |

### Fases de operacao (`AIOperationPhase`)
`Forming` → `Moving` → `Engaging` → `Capturing` → `Holding` → `Complete` / `Aborted`

A fase e inferida automaticamente em `InferPhasesForAllOps()` com base na posicao das unidades atribuidas e no status do objetivo vinculado.

### Necessidades de unidade (`AINeedKind`)
`Capturer`, `Assault`, `Artillery`, `FireSupport`, `AAA`, `SAM`, `GroundTransport`, `AirTransport`, `FighterA` (elite), `FighterB`, `Apache`, `AirTanker`

### Preenchimento de slots
`AssignExistingUnitsToOperations()` tenta preencher cada slot primeiro a partir das unidades ja atribuidas ao `SectorObjective` vinculado, depois busca qualquer unidade ativa elegivel no snapshot.

`GetDeficits()` expoe os slots nao preenchidos por operacao — e a entrada principal do `AIShoppingPlanner`.

---

## Camada 2a — Plano Territorial: `ObjectiveManager`

**Arquivo:** `ObjectiveManager.cs`

Gerencia `TeamObjectivePlan`, que contem uma lista de `SectorObjective` por setor do mapa.

### Status de objetivo (`ObjectiveStatus`)
`Pending` → `Pursuing` → `Capturing` → `Defending`

### Slots de objetivo (`SlotNeed`)
Cada `SectorObjective` tem slots com `UnitRole` (Capturador, Assalto, Transportador, FogoIndireto, Logistica) e `AssignedUnitId`. Quando uma unidade e atribuida, `Filled = true`.

O plano e reconstruido no inicio de cada turno em `BuildObjectivePlan(snapshot)` e novamente ao final da Fase 2 antes das compras — garantindo que o shopping reflita o estado pos-acoes.

---

## Camada 2b — AI de Compras: `AIShoppingPlanner`

**Arquivo:** `AIShoppingPlanner.cs`

Singleton configuravel via Inspector. `Decide(snapshot)` retorna lista de `ShoppingOrder` com unidade e construcao alvo.

### Logica de demanda (em ordem de avaliacao)
1. **Capturadores** — preenche slots abertos no plano via `CountOpenSlots`
2. **Assault** — minimo configuravel (`MinFilledAssaultSlots`)
3. **Transportadores** — terrestres quando distancia ao HQ >= `MinDistanceForTransportSlot`; aereos (`AirTransport`) quando ha objetivos com slot de Transportador no plano
4. **Fire Support / Artilharia** — apos turno minimo (`MinTurnForFireSupport`), ratio de capturadores e assalto ativos
5. **AAA / SAM** — proativo apos ameaca aerea detectada pelo `AIOperationManager`
6. **Interceptadores (CacaB/CacaA)** — gates por turno e ratio helicopteros/cacas
7. **AtaqueAereo (Apache)** — apos turno minimo, ratio chinooks/apaches
8. **Logistica** — caminhoes, tanques de combustivel aereo, etc.
9. **Elite** — gate: `EliteCapturerFillRatio` (padrao 60%) dos slots de capturador preenchidos

O `AIShoppingPlanner` consulta `AIOperationManager.GetDeficits()` para orientar compras de AAA/SAM/FighterA conforme os deficits ativos de operacoes.

### Parametros Inspector relevantes
| Campo | Descricao |
|---|---|
| `SavingPercentualForElite` | % do orcamento reservado para elite |
| `EliteCapturerFillRatio` | gate de fill de capturadores para liberar elite |
| `MinTurnForFireSupport` | turno minimo para comprar artilharia |
| `MinBaseArtilharia` / `MinBaseAAA` | quantidades minimas de defesa de base |
| `MaxAirTransporters` | teto de helicopteros de transporte |
| `MinTurnForInterceptador` | turno minimo para comprar caca |

---

## Camada 3 — AI de Unidade: `AIController`

**Arquivo:** `AIController.cs` (raiz) + ~45 arquivos partial

Loop de turno em `AIController.Phases.cs` — corrotina `RunAITurn(TeamId)`:

| Fase | Metodo | O que faz |
|---|---|---|
| 0 | `Phase0_WaitForTurnReady` | Aguarda servicos automaticos e delay |
| 1 | `Phase1_CommandService` | Dispara servico do comando em lote se automatico |
| 2 | `Phase2_UnitActions` | Loop principal — decide e executa por unidade |
| 3 | `Phase3_Shopping` | Reconstroi plano e compras via `AIShoppingPlanner.Decide` |
| 4 | `Phase4_EndTurn` | Passa o turno |

### Roteamento de decisao (`AIController.Router.cs`)

`DecideUnitAction(unit, snapshot)` chama handlers em ordem ate o primeiro retorno nao-nulo:

1. `TryDecideCapturerAction` — papel Capturador (com plano)
2. `TryDecideAssaultAction` — papel Assalto (com plano)
3. `TryDecideFireSupportAction` — papel FogoIndireto (com plano)
4. `TryDecideTransportadorAction` — papel Transportador (sempre, tem guard interno)
5. `TryDecideLogisticsAction` — papel Logistica (com plano)
6. `HexEvaluator` — fallback generico para unidades sem papel ou sem plano

### Papeis implementados e seus arquivos

| Papel | Arquivos |
|---|---|
| Capturador | `Capturer.cs`, `Capturer.Helpers.cs`, `Capturer.Embark.cs`, `Capturer.PontaLanca.cs`, `Capturer.Opportunist.cs`, `Capturer.Explorer.cs`, `Capturer.Rogue.cs`, `Capturer.Pursuer.cs`, `Capturer.Defender.cs` |
| Assalto | `Assault.cs`, `Assault.Embark.cs`, `Assault.Explorer.cs`, `Assault.Defender.cs` |
| Fogo Indireto | `FireSupport.cs`, `FireSupport.Helpers.cs`, `FireSupport.Rogue.cs`, `FireSupport.Defender.cs` |
| Transportador | `Transportador.cs`, `Transportador.Shuttle.cs`, `Transportador.Courier.cs`, `Transportador.Assigned.cs`, `Transportador.Evac.cs`, `Transportador.Air.cs` |
| Logistica | `Logistics.cs`, `Logistics.Helpers.cs` |
| Combate Aereo | `AirCombat.cs` |

### Unidades sem plano (rogues)
Unidades sem `SectorObjective` atribuido caem no `HexEvaluator` (logica de avaliacao de hex generica: move/attack/capture pelo score). Capturadores rogues tem prioridade extra de embarque: agem antes de transportadores para garantir embarque disponivel.

### Ordenacao de iniciativa (`AIController.Initiative.cs`)

| Grupo | Condicao |
|---|---|
| 0 | Vacater handoff ou bloqueando hex de captura de aliado |
| 1 | Helicoptero |
| 2 | Em corredor ativo ou transportador com candidato de embarque proximo |
| 3 | Com objetivo atribuido (por prioridade do objetivo) |
| 4 | Rogue sem objetivo |
| 5 | Em reparo |

---

## Fluxo de um turno completo

```
RunAITurn()
  ├── Phase0: aguarda turn start
  ├── BuildObjectivePlan()          ← ObjectiveManager reconstroi SectorObjectives
  ├── AIOperationManager.Rebuild()  ← detecta ameacas, cria Operacoes com slots
  ├── Phase1: ServicoDoComando
  ├── Phase2: por unidade
  │     └── DecideUnitAction()
  │           ├── handler de papel (Capturer/Assault/FireSupport/Transport/Logistics)
  │           └── HexEvaluator (fallback rogue)
  ├── BuildObjectivePlan() (reavaliacao pos-acoes)
  ├── AIOperationManager.Rebuild() (reavaliacao pos-acoes)
  └── Phase3: AIShoppingPlanner.Decide()
        └── consulta GetDeficits() → compra o que falta
```

---

## O que ainda nao existe

- **Memoria entre turnos**: nenhum tracking de tendencias (perdas acumuladas, velocidade de avanco inimigo). A IA e reativa a partir do snapshot atual.
- **Postura estrategica dinamica**: a `Stance` existe no snapshot mas a IA nao muda de comportamento global com base nela de forma sistematica.
- **Metricas de qualidade**: sem avaliacao quantitativa de decisoes por turno.
