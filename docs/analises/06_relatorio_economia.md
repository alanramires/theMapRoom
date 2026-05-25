# Relatorio de Economia do Jogo

Data base: 2026-05-25 (revisado; base original: 2026-03-06)

## Fontes
- Renda e caixa por time: `Assets/Scripts/Match/MatchController.cs`
- Dados de construcao: `Assets/DB/World Building/Construction/*.asset`
- Custos de unidades: `Assets/DB/Character/Unit/**/*.asset`

## Renda por tipo de construcao (base)
| Construcao | Renda (`capturedIncoming`) | capturePointsMax | sellingRule |
|---|---:|---:|---|
| HQ | 3000 | 40 | OriginalOwner |
| Fabrica | 1500 | 30 | OriginalOwner |
| Aeroporto | 1500 | 30 | OriginalOwner |
| Porto Naval | 1500 | 30 | OriginalOwner |
| Estacao de Trem | 1500 | 30 | OriginalOwner |
| Cidade | 1000 | 20 | Disabled |
| Barracks | 500 | 20 | FreeMarket |
| flag | 500 | 20 | Disabled |

Observacao: mapas podem sobrescrever `constructionConfiguration.capturedIncoming` por `fieldEntry`.

## Como renda entra no caixa
No `MatchController`:
1. `RecalculateIncomePerTurnForAllPlayers()` soma `construction.CapturedIncoming` de construcoes capturadas por time.
2. No inicio do turno, renda do time ativo e creditada em `actualMoney`.
3. Gastos usam `TrySpendActualMoney(...)`.

**Campos novos em `PlayerEntry` (desde revisao anterior):**
- `startMoney`: orcamento inicial concedido ao time antes da partida comecar.
- `startMoneyApplied`: flag que garante que `startMoney` e creditado apenas uma vez (junto com a renda do primeiro turno).
- Combinado: no primeiro turno, `credit = incomePerTurn + startMoney` se `startMoneyApplied == false`.

**`economyEnabled` (MatchController):**
- Flag global booleana (padrao: `true`).
- Quando `false`, `ResolveEconomyCost(baseCost)` retorna `0` — todos os custos sao zerados.
- Permite cenarios/tutoriais com economia desativada sem alterar a logica de cada sistema.

## Regras de venda de unidades (Market Rule)
Enum `ConstructionUnitMarketRule` (4 valores, um a mais que a revisao anterior):

| Valor | Nome | Comportamento |
|---|---|---|
| 0 | `FreeMarket` | Qualquer time que controle a construcao pode comprar |
| 1 | `OriginalOwner` | So o time dono original pode comprar, mesmo apos captura |
| 2 | `FirstOwner` | So o primeiro time que capturou pode comprar |
| 3 | `Disabled` | Nenhum time pode comprar (construcao sem producao) |

**`Disabled` e novo** (nao documentado antes). Cidade e flag usam esse valor: geram renda e supply mas nao funcionam como fabrica de unidades.

Aplicacao no fluxo de compra:
1. Tela/fluxo de shopping consulta `construction.CanProduceUnitsForTeam(buyerTeam)`.
2. Switch em `ConstructionManager.CanProduceUnitsForTeam`:
   - `Disabled` → false imediato
   - `FreeMarket` → true se ownership atual bater
   - `OriginalOwner` → true se `buyerTeam == originalOwnerTeamId`
   - `FirstOwner` → true se `firstOwnerInitialized && buyerTeam == firstOwnerTeamId`
3. Se a regra bloquear, a compra nao e autorizada.

Impacto economico/estrategico:
- Em `FreeMarket`, capturar construcao costuma transferir imediatamente poder de producao (ex.: Barracks).
- Em `OriginalOwner`/`FirstOwner`, captura pode gerar renda sem necessariamente liberar producao ao capturador.
- Em `Disabled`, a construcao so vale como fonte de renda, supply e posicao tatica — nao como plataforma de compra.

## Captura de construcoes e impacto economico
Fluxo de captura:
- A acao nasce no sensor `PodeCapturarSensor` e executa em `TurnStateManager.Capture`.
- Dano de captura por acao = `HP atual` da unidade capturadora.
- Se for construcao inimiga: reduz `CurrentCapturePoints` ate `0`.
- Quando conclui captura inimiga:
- `SetTeamId(capturer.TeamId)` troca ownership.
- capture e resetado para `CapturePointsMax`.
- Se for construcao aliada parcialmente perdida, a mesma acao pode recuperar pontos de captura.

**Resistencia de captura por tipo** (via `capturePointsMax`):
- HQ e o mais resistente (40 pontos) — capturar o HQ adversario requer unidades de alto HP ou multiplas acoes.
- Fabricas/Aeroportos/Portos/Estacoes (30) — resistencia media.
- Cidades/Barracks/flags (20) — rapidas de capturar.

Impacto economico:
- Ownership alterado entra na conta de renda em `RecalculateIncomePerTurnForAllPlayers()`.
- Resultado pratico: capturar propriedade transfere fluxo de `capturedIncoming` entre times no ciclo de turnos.
- Portanto, captura e alavanca economica direta (alem do valor tatico/posicional).

## Custos das unidades compraveis
Referencie consolidado em: `docs/analises/01_relatorio_unidades.md`.

Faixa observada no banco atual (atualizado):
- minimo: 1000 (Soldado)
- maximo: 30000 (Destroyer)
- media aproximada: 11690.32 (31 unidades)

## Fluxo medio esperado no turno inicial
Depende do mapa e ownership inicial.

Baseline por propriedade:
- 1 HQ + 1 Fabrica + 1 Cidade => 5500 por turno

Exemplo real (Battle Map catalog):
- Team 0 inicia com renda total configurada em 4500 (4 propriedades)
- Team 1 inicia com renda total configurada em 4000 (3 propriedades)
- Propriedades neutras configuradas no mapa: 5500 de renda potencial adicional apos captura

## Leitura estrategica
- O pacing economico e dominado por ownership de construcoes, nao por bonus globais ocultos.
- Delta de 1 cidade (1000) por turno altera rapidamente janelas de compra de unidades medias.
- Logistica (custos de servico) consome o mesmo caixa de compra de unidades, gerando trade-off real entre sustain e expansao.
- As `Market Rules` de cada construcao podem desacoplar "capturar para renda" de "capturar para produzir", mudando prioridades de ataque/defesa.
- Construcoes `Disabled` (Cidade, flag) devem ser priorizadas por renda e posicionamento logistico, nao por producao.
- `economyEnabled = false` e uma alavanca de design util para tutoriais ou cenarios de sandbox.
