# Plano — Paradigma do Transportador por Eixo (R4)

> **Status:** 1º corte IMPLEMENTADO (demanda terrestre eixo-driven por profundidade de frente).
> Pré-requisitos já prontos: `InvasionAxisMap` (setor→eixo, `Axis.FrontSector`/`FrontIndex`/`Corridor`/`Complete`), presença por eixo (`BuildEixoPresence`/`GetEixoPresence`), `aiEixo` persistente (memória + save/load), R1 (bônus de frente) e estabilidade/rebalanceamento de eixo.

## O que já foi feito (1º corte)

`ComputeGroundTransportNeed` (`AITacticalAnalyzer.Builders.cs`) foi reescrita de **reativa/por-objetivo** para **eixo-driven por profundidade de frente**:

- A demanda de APC terrestre nasce **só no `FrontSector` do eixo** (um objetivo por eixo = a ponta) → **teto 1/eixo natural**, sem double-count entre objetivos atrás da linha.
- **Profundidade** = `GetDistanceToHQ(FrontSector)`. Frente rasa (`< GroundTransportEmbarkDistance` = 7h) → a pé resolve, pressão **zero**. Frente profunda → 1 APC.
- Desconta APCs terrestres já comprometidos com o eixo (`CountGroundTransportsOnEixo`: presença por `aiEixo` + recém-atribuídos ao objetivo da frente, dedupe por `InstanceId`).
- Acesso ao mapa: `AIController.CurrentAxisMap` (exposto), reusado via `GetShoppingAxisMap(team)` no analyzer — mesmo turno/time, fallback `InvasionAxisMap.Build` se não bater.
- Removido o gatilho antigo "tem capturador longe?" (`ObjectiveHasFilledCapturer`, varredura de `rideNeeding`/`emptyCapturerSlots`).

### Slot de Transportador do plano agora é front-depth aware (2º ajuste)

Descoberta via HUD (**Tools > Utils > Shopping Pressure**): no turno 1, um objetivo longe (ex.: Charlie a ≥7h) ganhava `Transportador 0/1` por **distância crua** ([`AIController.PlanEvaluator.cs:328`](../Assets/Scripts/Match/AI/2.%20Planner/AIController.PlanEvaluator.cs)), e esse slot alimentava um **segundo** sistema de demanda — `AIShoppingPlanner.ComputeTransportDemand` (`AIShoppingPlanner.Transport.cs`, `preventiveNeeded`) — gerando pressão de compra prematura, contra a filosofia do eixo (no início todo eixo tem frente rasa).

Correção: o slot agora nasce só quando `IsAxisFrontSector(obj.Sector) && distHQ >= threshold` (**frente profunda do eixo**) **OU** `isRallyAssemblySector` (**rally segurado** = reach máximo). Como `ComputeTransportDemand` faz `if (!hasTransportSlot) continue`, gatear o slot na fonte conserta os dois consumidores de uma vez (não foi preciso tocar no `AIShoppingPlanner.Transport.cs`).

Não alterados: slot de transporte da **base inimiga** (`PlanEvaluator.cs:472`, assalto final, profundo por natureza, fora da classificação de eixo) e o slot **oportunista** (`PlanEvaluator.cs:1368`, atribui APC já existente a capturador real longe — não é pré-compra). Há **dois** sistemas de demanda de transporte: `AITacticalAnalyzer.ComputeGroundTransportNeed` (op tática) e `AIShoppingPlanner.ComputeTransportDemand` (slot do plano) — ambos agora front-depth aware.

### Pipelining do corredor (3º ajuste)

`InvasionAxisMap.GetTransportTargetSector(eixo)` é a fonte única do **alvo de transporte** do eixo, usada pelos dois sistemas:
- normalmente o **`FrontSector`**;
- mas se a frente já está **sob captura** (`SectorInfo.HasPartialCapture`), o alvo avança **um nó no corredor** (`Corridor[FrontIndex+1]`, ou o rally se a frente é o último nó). Pipelining: enquanto a captura conclui neste turno, o APC já é demandado/posicionado pro próximo lance e a próxima unidade já embarca na base.

O **gate de profundidade é medido no ALVO**, não na frente: se a frente sob captura ainda está perto (dentro dos ~7h), o próximo nó também é raso → a pé resolve → não gera demanda (ressalva do usuário). Mantém teto 1/eixo (a frente sob captura perde o alvo; o próximo nó ganha). Pendente: limiar de "quanto de captura" para disparar o pipelining (hoje qualquer captura parcial; `HasPartialCapture`).

### Pendente / não feito neste corte
- **Prioridade por eixo** (frente mais profunda sobe na fila) — mantida `GetCaptureOperationPriority` atual. O band-aid `+9` em `TryMapNeedToShoppingDemand` **não** foi aplicado; reavaliar em playtest.
- **Rally segurado como reach máximo** explícito — hoje cai no limiar de profundidade (rally longe já passa dos 7h). Sem leitura de `AIRallyAssemblyState`.
- **"Prepara Foxtrot" quando a frente não é objetivo** — a demanda exige que o `FrontSector` seja um objetivo no plano (R1 quase sempre o seleciona). Frente sem objetivo não pré-compra.
- **Air transport** intocado (segue `TryBuildAirliftCaptureOps`).

## Problema

A demanda de transporte terrestre hoje é **reativa e por-objetivo**: `ComputeGroundTransportNeed` (`AITacticalAnalyzer.Builders.cs`) só pede APC quando um capturador **já atribuído** está a ≥ `GroundTransportEmbarkDistance` (7h) do objetivo, ou como aposta futura numa frente comprometida **sem capturador perto** (correção do Echo já aplicada: `hasNearCapturer` desliga a aposta).

Isso falha no pensamento de general: o transporte é o **único papel cujo valor é antecipatório/posicional** — ele prepara o terreno que você ainda vai tomar, não responde ao inimigo que já está aqui. O caso concreto: a AI deveria comprar APC agora não (só) pra ajudar Delta, mas pra **começar a preparar Foxtrot** (que sequer aparece como demanda de transporte hoje).

Sintoma secundário no scoring: com prioridade igual, o assalto vence o APC pelo bônus de combate (`+7000 × prioridade-alvo` vs inimigo dominante; APC = 0). Band-aid discutido mas **não aplicado**: offset `+9` em vez de `+10` pro `GroundTransport` em `TryMapNeedToShoppingDemand` (linha ~1471), pra destravar o empate. Decidir se entra junto ou se a reforma o torna desnecessário.

## A pressão como equação de controle

Cada papel é um sinal com **fonte (acúmulo)**, **teto (saturação)** e **alívio (decaimento)**. O segredo é nenhum saturar e dominar. O que já existe:

| Pressão | Sobe com | Teto | Cai quando |
|---|---|---|---|
| Assalto | força inimiga | pacote `2/2/1` (packages×2 − assaults) | compra / massa formada |
| Artilharia | volume inimigo / represália | `RallyMinimumArtillery`, PreventiveDefense | compra |
| Logística | feridos acumulando | `ceil(repair/2)` | reparo concluído |
| **Transporte** | **profundidade da frente do eixo / rally segurado** | **1 por eixo** | **compra** |

## Novo paradigma: pressão por EIXO escalada pela profundidade da frente

A intuição que guia o desenho (frase do usuário, manter como norte):

> **Eixo recém-saído do HQ (frente rasa) → transporte a pé resolve, pressão baixa. Eixo com a frente lá na ponta (vários setores segurados atrás) → pressão alta, porque o próximo capturador nasce no HQ e tem que cruzar tudo. E o rally segurado é o caso extremo: massa juntando no fim do corredor, reach máximo.**

Ou seja: parar de tratar transporte como demanda por-objetivo (carona de um capturador específico) e tratar como **sinal por-eixo**, contínuo, escalado pela **profundidade da frente**:

```
pressão_transporte(eixo) = f( profundidade_da_frente, rally_segurado )
    profundidade_da_frente ≈ GetDistanceToHQ(Axis.FrontSector)  (ou FrontIndex no corredor)
    teto: 1 transportador por eixo ativo
    decai: ao comprar (desconta APCs já alocados/comprados ao eixo)
```

- **Não é liga/desliga, é curva.** Frente rasa (perto do HQ) → pressão baixa (a pé resolve). Frente profunda (vários nós segurados atrás) → pressão alta. Rally segurado → caso extremo (massa juntando no fim do corredor, reach máximo).
- **Teto 1/eixo** resolve estruturalmente o "não pode dominar": no máximo 3 transportes pedidos (1 por eixo).
- **Prepara Foxtrot:** com o eixo do Foxtrot consolidando os setores de trás, nasce a pressão de 1 APC pra alimentar o próximo lance — sem depender de já ter um capturador longe lá.

## Esboço de implementação

1. **Substituir/augmentar `ComputeGroundTransportNeed`** por um cálculo eixo-driven:
   - Para cada eixo ativo do time (via `InvasionAxisMap.Build(team)` — ou reusar o `currentAxisMap` do planner se acessível na fase de shopping; senão reconstruir no analyzer).
   - `profundidade = GetDistanceToHQ(axis.FrontSector)` (ou `axis.FrontIndex`). Se a frente não existe (`Complete`) → 0.
   - Limiar: só gera pressão quando `profundidade ≥ GroundTransportEmbarkDistance` (mantém o "a pé resolve" pra frente rasa).
   - Bônus de rally segurado: se o rally do eixo está em estado de hold/assembly (ver `AIRallyAssemblyState`/`rallyContext`), trata como reach máximo.
   - Desconta transportes já em campo/alocados àquele eixo (presença de Transportador com `aiEixo == eixo`, ou APC já no corredor). Teto 1/eixo.
2. **Prioridade da demanda:** derivar do eixo (frente mais profunda / rally segurado → prioridade melhor), de forma que o transporte de um eixo maduro suba na fila naturalmente — possivelmente tornando o band-aid `+9` desnecessário. Se ainda precisar, aplicar o `+9` em `TryMapNeedToShoppingDemand`.
3. **Marcar o transporte comprado com o eixo** (quando spawnar/atribuir) pra o desconto de presença funcionar — `aiEixo` já persiste; garantir que o APC herde o eixo do corredor que vai servir.
4. **Manter air transport** (`AINeedKind.AirTransport`) no caminho atual por enquanto, ou estender o mesmo raciocínio (eixo aéreo) num passo seguinte.

## Decisões em aberto

- A frente do eixo usa `ControllingTeam` (atualiza no rebuild da virada de turno) — confirmar que a fase de shopping vê a frente já atualizada do turno corrente (roda depois do `BuildObjectivePlan`, deve estar ok).
- Forma exata de `f`: linear em `profundidade` (clamp), degrau em `EmbarkDistance`, ou ponderada pelo rally. Começar simples (limiar + 1/eixo) e calibrar.
- O `currentAxisMap` é instância do `AIController` (planner); o analyzer de shopping é outra classe — decidir se reconstrói o mapa no analyzer (custo: 1 build/turno) ou expõe o do planner.
- Como contar "APC já alocado ao eixo" de forma robusta (presença por `aiEixo` vs. posição no corredor).

## Referências de código

- `Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.Builders.cs` — `ComputeGroundTransportNeed`, `GroundTransportEmbarkDistance`, `TryBuildGroundCaptureOps`.
- `Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs` — `TryMapNeedToShoppingDemand` (~1471, offset de prioridade), `ScoreRoleShoppingCandidate` (~1527, bônus de combate), `DecideRoleBased` (loop de compra).
- `Assets/Scripts/Match/AI/2. Planner/InvasionAxisMap.cs` — `Axis.FrontSector`/`FrontIndex`/`Corridor`/`Complete`, `GetEixo`.
- `Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs` — `currentAxisMap`, `BuildEixoPresence`/`GetEixoPresence`.
- `Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.RallyPoints.cs` — estado de rally (`AIRallyAssemblyState`, `rallyContext`).
