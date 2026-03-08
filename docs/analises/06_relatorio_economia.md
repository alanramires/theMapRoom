# Relatorio de Economia do Jogo

## Fontes
- Renda e caixa por time: `Assets/Scripts/Match/MatchController.cs`
- Dados de construcao: `Assets/DB/World Building/Construction/*.asset`
- Custos de unidades: `Assets/DB/Character/Unit/**/*.asset`

## Renda por tipo de construcao (base)
| Construcao | Renda (`capturedIncoming`) |
|---|---:|
| HQ | 3000 |
| Fabrica | 1500 |
| Aeroporto | 1500 |
| Porto Naval | 1500 |
| Cidade | 1000 |
| Barracks | 500 |

Observacao: mapas podem sobrescrever `constructionConfiguration.capturedIncoming` por `fieldEntry`.

## Como renda entra no caixa
No `MatchController`:
1. `RecalculateIncomePerTurnForAllPlayers()` soma `construction.CapturedIncoming` de construcoes capturadas por time.
2. No inicio do turno, renda do time ativo e creditada em `actualMoney`.
3. Gastos usam `TrySpendActualMoney(...)`.

## Regras de venda de unidades (Market Rule)
Venda de unidades por construcao nao depende so de ownership atual; depende tambem da regra de mercado da construcao.

Fontes:
- `Assets/Scripts/Construction/ConstructionSiteRuntime.cs`
- `Assets/Scripts/Construction/ConstructionManager.cs` (`CanProduceUnitsForTeam(...)`)
- `Assets/Scripts/Match/TurnState/TurnStateManager.ConstructionShopping.cs`

Tipos de regra:
- `FreeMarket`: qualquer time que controle a construcao no momento pode comprar.
- `OriginalOwner`: so o time dono original da construcao pode comprar, mesmo apos captura.
- `FirstOwner`: so o primeiro time que capturou/assumiu a construcao pode comprar.

Aplicacao no fluxo de compra:
1. Tela/fluxo de shopping consulta `construction.CanProduceUnitsForTeam(buyerTeam)`.
2. Essa validacao compara:
- time comprador,
- ownership atual da construcao,
- regra `sellingRule`,
- e metadados de ownership (`OriginalOwnerTeamId`, `FirstOwnerTeamId`).
3. Se a regra bloquear, a compra nao e autorizada.

Impacto economico/estrategico:
- Em `FreeMarket`, capturar fabrica costuma transferir imediatamente poder de producao.
- Em `OriginalOwner`/`FirstOwner`, captura pode gerar renda sem necessariamente liberar producao ao capturador.
- Isso altera forte o valor real de cada ponto no mapa: uma construcao pode valer muito em renda, mas pouco em projecao de compra para certos times.

## Captura de construcoes e impacto economico
Fluxo de captura:
- A acao nasce no sensor `PodeCapturarSensor` e executa em `TurnStateManager.Capture`.
- Dano de captura por acao = `HP atual` da unidade capturadora.
- Se for construcao inimiga: reduz `CurrentCapturePoints` ate `0`.
- Quando conclui captura inimiga:
- `SetTeamId(capturer.TeamId)` troca ownership.
- capture e resetado para `CapturePointsMax`.
- Se for construcao aliada parcialmente perdida, a mesma acao pode recuperar pontos de captura.

Impacto economico:
- Ownership alterado entra na conta de renda em `RecalculateIncomePerTurnForAllPlayers()`.
- Resultado pratico: capturar propriedade transfere fluxo de `capturedIncoming` entre times no ciclo de turnos.
- Portanto, captura e alavanca economica direta (alem do valor tatico/posicional).

## Custos das unidades compraveis
Referencie consolidado em: `docs/analises/01_relatorio_unidades.md`.

Faixa observada no banco atual:
- minimo: 1000 (Soldado)
- maximo: 30000 (Destroyer)
- media aproximada: 11710.34

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
