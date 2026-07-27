using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------

    // Defini��o parcial da classe AIController, organizada em m�ltiplos arquivos para melhor legibilidade 
    // e manuten��o. Cada arquivo foca em um aspecto espec�fico do comportamento da IA, como ciclo de vida,
    // tomada de decis�es, avalia��o de hex�gonos e intera��o com o sistema de objetivos.
    // A classe � respons�vel por controlar a IA inimiga, incluindo a execu��o de suas a��es, 
    // planejamento de objetivos e tomada de decis�es, utilizando uma abordagem baseada
    //  em est�gios para organizar seu comportamento.    

    // -------------------------------------------------------------------------

    private PlayerAction DecideUnitAction(UnitManager unit, AIWorldSnapshot snapshot)

    {

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex));

        if (TryFindProductionUnlockVacateAction(unit, snapshot, out PlayerAction productionUnlockAction))
            return productionUnlockAction;

        // Reparos sao uma necessidade da propria unidade, inclusive quando ela
        // tambem possui slots de transporte. Sem este gate um Hidroaviao quase
        // sem autonomia entra primeiro na varredura de Pickup e pode ficar
        // aguardando uma infantaria distante em vez de buscar combustivel ou
        // uma LZ. Os papeis ainda podem cuidar de carga/passageiros quando a
        // unidade nao esta em reparo.
        PlayerAction selfRecoveryAction =
            TryDecideRepairAction(unit, snapshot, plan);
        if (selfRecoveryAction != null)
            return selfRecoveryAction;

        // Um transportador que tambem e Hub (como o Trem de Carga) precisa
        // conferir a rede antes de sair procurando passageiro. Essa preempcao
        // e reservada para falta critica: uma demanda preventiva continua
        // disponivel no branch normal de Estoque, mas nao faz o Trem abandonar
        // um pickup Tactical/Operational que ja esta pronto.
        if (!HasTransportCargo(unit)
            && IsPrimaryTransportRole(unit)
            && HasStockTransferCapability(unit)
            && TryDecideStockAction(
                unit, snapshot, plan, criticalOnly: true)
                is PlayerAction stockNetworkAction)
        {
            return stockNetworkAction;
        }

        PlayerAction transportOperationsAction =
            TryDecideTransportOperationsAction(unit, snapshot, plan);
        if (transportOperationsAction != null)
            return transportOperationsAction;

        // Supridor que TAMBEM transporta, carregando um ferido: a manutencao a bordo (ou
        // a recarga que a viabiliza) e a acao da rodada, antes de qualquer papel. Vem
        // aqui em cima porque um navio LOTADO pula o gate de courier abaixo e cairia no
        // ataque preemptivo naval — cacar sub com paciente a bordo. Devolve null quando
        // nao consegue suprir nem recarregar: dai o EVAC normal desembarca o ferido.
        // Ver Transportador.Hospital.
        // Hospital, EVAC, Supply, Pickup e Courier ja foram consultados pelo
        // TransportOperationsService no inicio do roteador.

        // Facção sem QG: captura por proximidade, antes do planner. O plano normal assume
        // um eixo a partir do proprio QG — a rebelde nao tem, e sem este curto-circuito
        // todo capturador dela vira rogue e marcha para o QG inimigo. Ver AIController.Rebel.
        PlayerAction rebelAction = TryDecideRebelAction(unit, snapshot, plan);
        if (rebelAction != null) return rebelAction;

        if (plan != null)

        {

            PlayerAction objectiveAction = TryDecideCapturerAction(unit, snapshot, plan);

            if (objectiveAction != null) return objectiveAction;

            bool preferFireSupportFirst = PreferFireSupportBeforeAssault(unit);
            bool suppressAssaultTransportFallback = false;

            if (preferFireSupportFirst)
            {
                bool primaryHybrid =
                    IsPrimaryAssaultFireSupportHybrid(unit);
                PlayerAction earlyFireSupportAction = primaryHybrid
                    ? TryDecideFireSupportAttackOnlyAction(
                        unit, snapshot, plan)
                    : TryDecideFireSupportAction(unit, snapshot, plan);
                if (earlyFireSupportAction != null) return earlyFireSupportAction;

                // Híbrido combatente: após não encontrar tiro útil,
                // tenta explicitamente o transporte especializado antes do
                // fallback Assault. Uma rejeição de segurança libera o papel
                // Assault, mas não permite repetir a carona pela política menos
                // restritiva.
                if (primaryHybrid)
                {
                    SectorObjective fireSupportObjective =
                        ResolveAssignedFireSupportObjective(unit, plan);
                    FireSupportTransportOutcome transportOutcome =
                        TryDecideFireSupportTransportAction(
                            unit, snapshot, plan, fireSupportObjective,
                            out PlayerAction fireSupportTransportAction);
                    if (transportOutcome
                            == FireSupportTransportOutcome.Handled
                        && fireSupportTransportAction != null)
                        return fireSupportTransportAction;

                    if (transportOutcome
                        == FireSupportTransportOutcome.TransportRejected)
                    {
                        suppressAssaultTransportFallback = true;
                        Debug.Log(
                            $"{TL("FireSupport")} {unit.InstanceId} " +
                            "hibrido combatente: transporte rejeitado; " +
                            "fallback Assault liberado sem repetir transporte.");
                    }
                }
            }

            PlayerAction assaultAction = TryDecideAssaultAction(
                unit, snapshot, plan,
                allowTransport: !suppressAssaultTransportFallback);

            if (assaultAction != null) return assaultAction;

            if (!preferFireSupportFirst)
            {
                PlayerAction fireSupportAction = TryDecideFireSupportAction(unit, snapshot, plan);
                if (fireSupportAction != null) return fireSupportAction;
            }

        }

        PlayerAction intelAction = TryDecideIntelAction(unit, snapshot, plan);

        if (intelAction != null) return intelAction;

        PlayerAction airCombatAction = TryDecideAirCombatAction(unit, snapshot);

        if (airCombatAction != null) return airCombatAction;

        // Navios de combate que tambem transportam suprimentos nao podem perder
        // um tiro legal adjacente porque satisfazem o papel de Logistica. Caminhoes
        // terrestres continuam priorizando exclusivamente o servico.
        if (IsNavalCombatSupplier(unit)
            && TryBuildRolePreemptiveAttack(
                unit, snapshot, null, null, defensiveContext: false,
                out PlayerAction navalSupplierAttack, out string navalAttackReason))
        {
            Debug.Log($"{TL("NavalCombatSupplier")} {unit.InstanceId} combate antes da logistica - {navalAttackReason}");
            return navalSupplierAttack;
        }

        // O servico de transporte ja tentou Hospital, EVAC, Supply, Pickup e
        // Courier. A logistica abaixo preserva reload, retirada e esperas
        // especializadas quando nenhuma operacao de transporte venceu.
        PlayerAction logisticsAction = TryDecideLogisticsAction(unit, snapshot, plan);

        if (logisticsAction != null) return logisticsAction;

        // Estoque puro e transportadores-hub entram aqui somente depois de
        // Transporte e Logistica terem tentado suas operacoes prioritarias.
        // O branch usa exclusivamente a capacidade Transfer declarada na ficha.
        PlayerAction stockAction =
            TryDecideStockAction(unit, snapshot, plan);
        if (stockAction != null)
            return stockAction;

        // Passageiro que respondeu NAO ao Quero Carona continua como
        // oportunidade de baixa prioridade. So agora, depois de objetivos,
        // combate, intel e logistica, o transportador pode usar esse ranking
        // para se posicionar sem transformar a estimativa em ordem obrigatoria.
        PlayerAction opportunisticPickup =
            TryDecideOpportunisticTransportPickupAction(
                unit, snapshot, plan);
        if (opportunisticPickup != null)
            return opportunisticPickup;

        // Carga ja embarcada ainda pode materializar o courier residual. Um
        // transportador vazio, porem, nao pode reabrir o seletor global antigo:
        // Tactical -> Operational -> Strategic ja foram varridos pelo servico,
        // sempre a partir da posicao do proprio transportador.
        if (IsPrimaryTransportRole(unit))
        {
            if (HasTransportCargo(unit))
            {
                PlayerAction transportFallback =
                    TryDecideTransportadorAction(unit, snapshot, plan);
                if (transportFallback != null)
                    return transportFallback;
            }
            else
            {
                Vector3Int transportCell = unit.CurrentCellPosition;
                transportCell.z = 0;
                Dictionary<Vector3Int, List<Vector3Int>> transportPaths =
                    UnitMovementPathRules.CalcularCaminhosValidos(
                        boardTilemap, unit,
                        Mathf.Max(0, unit.RemainingMovementPoints),
                        terrainDatabase);
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} sem operacao " +
                          "nas ondas Tactical/Operational/Strategic; aguarda.");
                return BuildMoveBatch(
                    unit, snapshot.AITeam, transportCell, transportCell,
                    transportPaths);
            }
        }

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        Dictionary<Vector3Int, List<Vector3Int>> paths =

            UnitMovementPathRules.CalcularCaminhosValidos(

                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);

        var freeCells = new List<Vector3Int>();

        if (paths != null)

            foreach (Vector3Int cell in paths.Keys)

                if (!occupied.Contains(cell))

                    freeCells.Add(cell);

        List<HexEvaluation> evaluations = HexEvaluator.Evaluate(

            unit, snapshot.AITeam, fromCell, freeCells,

            boardTilemap, terrainDatabase,

            out CandidateType resolvedRole,

            out Vector3Int resolvedTarget,

            out bool hasTarget,

            turnStateManager);

        HexEvaluation chosen = default;

        bool foundChosen = false;

        foreach (HexEvaluation e in evaluations)

        {

            if (e.isChosen) { chosen = e; foundChosen = true; break; }

        }

        if (showAIUnitHUD)

        {

            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"{TL("Think")} Unidade {unit.InstanceId} ({unit.UnitDisplayName}) | role={resolvedRole} target={resolvedTarget}");

            foreach (HexEvaluation e in evaluations)

                sb.AppendLine($"  {(e.isChosen ? "★" : " ")} {e.cell} | total={e.total:F2}" +

                              $"  cap={e.captureProximity:F2} cbt={e.combatValue:F2} dpq={e.positionQuality:F2}" +

                              $"  coh={e.cohesion:F2} dev={e.deviation:F2} saf={e.safety:F2}" +

                              $"  → {e.actionSummary}");

            Debug.Log(sb.ToString());

        }

        if (!foundChosen)

        {

            if (showAILogs)
                Debug.LogWarning($"[AI] {unit.InstanceId}: HexEvaluator sem vencedor — aguardando no lugar.");

            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        }

        Vector3Int destCell = chosen.cell;

        if (IsReservedCaptureCellForAnotherUnit(unit, snapshot.AITeam, destCell, paths, out UnitManager reservedFor))
        {
            if (showAILogs)
                Debug.Log($"[AI] {unit.InstanceId} evita mover para captura reservada @ {destCell} por {reservedFor.InstanceId}");
            if (TrySelectFallbackHexEvaluation(unit, snapshot.AITeam, evaluations, paths, out HexEvaluation fallbackChosen))
            {
                chosen = fallbackChosen;
                destCell = chosen.cell;
            }
            else
            {
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
            }
        }

        // 1. Captura: contexto aponta que devemos capturar neste hex

        bool isCaptureContext = chosen.type == CandidateType.CaptureNow

            || (chosen.type == CandidateType.CaptureAdvance && hasTarget && destCell == resolvedTarget);

        if (!isCaptureContext
            && chosen.type == CandidateType.CaptureAdvance
            && SimulateCaptureSensor(unit, destCell, out _)
            && !IsReservedCaptureCellForAnotherUnit(unit, snapshot.AITeam, destCell, paths, out _))
        {
            isCaptureContext = true;
        }

        if (isCaptureContext)

        {

            bool canCapture = unit.TryGetUnitData(out UnitData hexUnitData)
                && hexUnitData.roles != null && hexUnitData.roles.Count > 0
                && hexUnitData.roles.Contains(UnitRole.Capturador);

            // O tipo CaptureNow vem do PAPEL, nao do hex escolhido. Um fallback (apos ceder a captura
            // reservada a outra unidade) pode cair num hex de ATAQUE/movimento SEM capturavel —
            // montar BuildCaptureBatch la gera batch invalido e a unidade "trava". So captura se o
            // sensor confirmar capturavel no destino; senao cai pro ataque/movimento abaixo.
            if (canCapture && SimulateCaptureSensor(unit, destCell, out _))
            {

            if (showAILogs)
                Debug.Log($"[AI] {unit.InstanceId} → captura @ {destCell}");

            return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, destCell, paths);

            }

        }

        // 2. Ataque: posição escolhida tem valor de combate

        if (chosen.combatValue > 0f)

        {

            bool hasMoved = destCell != fromCell;

            var attackCandidates = FindAttackTargetsSorted(unit, destCell, hasMoved);

            if (attackCandidates != null)

            {

                foreach (var (target, _) in attackCandidates)

                {

                    if (target?.targetUnit == null) continue;

                    if (!PassesAttackDecision(unit, target.targetUnit, destCell, false, out string atkReason))
                    {
                        if (showAILogs)
                            Debug.Log($"[AI] {unit.InstanceId} → ataque bloqueado por AttackDecision ({target.targetUnit.InstanceId}): {atkReason}");
                        continue;
                    }

                    Vector3Int targetCell = target.targetUnit.CurrentCellPosition; targetCell.z = 0;

                    if (showAILogs)
                        Debug.Log($"[AI] {unit.InstanceId} → ataca {target.targetUnit.InstanceId} de {destCell}");

                    return BuildAttackBatch(

                        unit, snapshot.AITeam, fromCell, destCell,

                        target.targetUnit.InstanceId.ToString(), targetCell, paths);

                }

            }

            if (TryBuildFallbackAttackFromEvaluations(
                    unit, snapshot, fromCell, paths, evaluations, destCell, out PlayerAction fallbackAttack))
                return fallbackAttack;

        }

        // 3. Movimento simples

        if (showAILogs)
            Debug.Log($"[AI] {unit.InstanceId} → move para {destCell}");

        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, destCell, paths);

    }

    private static bool IsNavalCombatSupplier(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;
        if (data.domain != Domain.Naval || !UnitRoleCompatibility.CanSatisfy(data, UnitRole.Logistica))
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        for (int i = 0; weapons != null && i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon weapon = weapons[i];
            if (weapon != null && weapon.weapon != null && weapon.squadAmmunition > 0)
                return true;
        }
        return false;
    }

    private bool TryBuildRolePreemptiveAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        bool defensiveContext,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";
        if (unit == null || snapshot == null)
            return false;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        paths ??= UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap,
            unit,
            Mathf.Max(0, unit.RemainingMovementPoints),
            terrainDatabase);
        occupied ??= BuildOccupied(unit);
        if (paths == null || paths.Count == 0)
            return false;

        List<UnitManager> enemies = CollectVisibleAssaultEnemies(snapshot.AITeam);
        UnitManager bestTarget = null;
        Vector3Int bestCell = fromCell;
        float bestScore = float.MinValue;
        string bestDecision = "";

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied.Contains(cell))
                continue;

            for (int i = 0; i < enemies.Count; i++)
            {
                UnitManager enemy = enemies[i];
                if (enemy == null || enemy.IsDead || enemy.IsEmbarked)
                    continue;
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy))
                    continue;
                if (!PassesAttackDecision(
                        unit, enemy, cell, defensiveContext,
                        out string decisionReason))
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition;
                enemyCell.z = 0;
                BazookaTargetPriority preference =
                    ResolveAssaultTargetPreference(unit, enemy);
                float score =
                    GetAssaultTargetPreferenceScore(preference)
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 900f
                    - SectorManager.HexDistance(cell, enemyCell) * 100f
                    - GetPathStepCount(paths, cell) * 25f
                    - enemy.InstanceId * 0.001f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestCell = cell;
                bestTarget = enemy;
                bestDecision = decisionReason;
            }
        }

        if (bestTarget == null)
            return false;

        Vector3Int targetCell = bestTarget.CurrentCellPosition;
        targetCell.z = 0;
        action = BuildAttackBatch(
            unit,
            snapshot.AITeam,
            fromCell,
            bestCell,
            bestTarget.InstanceId.ToString(),
            targetCell,
            paths);
        reason = $"via={bestCell} -> {bestTarget.UnitDisplayName}#{bestTarget.InstanceId} score={bestScore:F0} {bestDecision}";
        return true;
    }

    private bool IsReservedCaptureCellForAnotherUnit(
        UnitManager unit,
        TeamId aiTeam,
        Vector3Int cell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out UnitManager reservedFor)
    {
        reservedFor = null;
        if (unit == null || paths == null)
            return false;
        cell.z = 0;
        if (!paths.ContainsKey(cell))
            return false;
        if (!SimulateCaptureSensor(unit, cell, out _))
            return false;
        return ShouldReserveOpportunisticCaptureForCloserUnit(unit, aiTeam, cell, paths, out reservedFor);
    }

    private bool TrySelectFallbackHexEvaluation(
        UnitManager unit,
        TeamId aiTeam,
        List<HexEvaluation> evaluations,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out HexEvaluation fallback)
    {
        fallback = default;
        if (evaluations == null)
            return false;

        bool found = false;
        float bestTotal = float.MinValue;
        foreach (HexEvaluation candidate in evaluations)
        {
            if (candidate.isChosen)
                continue;
            if (IsReservedCaptureCellForAnotherUnit(unit, aiTeam, candidate.cell, paths, out _))
                continue;
            if (!found || candidate.total > bestTotal)
            {
                fallback = candidate;
                bestTotal = candidate.total;
                found = true;
            }
        }

        return found;
    }
    private static bool PreferFireSupportBeforeAssault(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;
        return data.preferArtilleryModeBeforeCombatant
            && UnitRoleCompatibility.CanSatisfy(data, UnitRole.FogoIndireto);
    }

    private static bool IsPrimaryAssaultFireSupportHybrid(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;
        return IsCombatantFireSupport(unit)
            && UnitRoleCompatibility.CanSatisfy(data, UnitRole.Assalto);
    }

    private bool TryBuildFallbackAttackFromEvaluations(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        List<HexEvaluation> evaluations,
        Vector3Int excludedCell,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null || evaluations == null || evaluations.Count == 0)
            return false;

        var ordered = new List<HexEvaluation>();
        foreach (HexEvaluation eval in evaluations)
        {
            if (eval.cell == excludedCell) continue;
            if (eval.combatValue <= 0f) continue;
            ordered.Add(eval);
        }

        bool prioritizeDpqAtBattle = unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.prioritizeDpqAtBattle;

        ordered.Sort((a, b) =>
        {
            if (prioritizeDpqAtBattle)
            {
                int dpqCompare = b.positionQuality.CompareTo(a.positionQuality);
                if (dpqCompare != 0) return dpqCompare;
            }

            return b.total.CompareTo(a.total);
        });

        foreach (HexEvaluation eval in ordered)
        {
            Vector3Int attackCell = eval.cell;
            attackCell.z = 0;
            bool hasMoved = attackCell != fromCell;
            var attackCandidates = FindAttackTargetsSorted(unit, attackCell, hasMoved);
            if (attackCandidates == null) continue;

            foreach (var (target, _) in attackCandidates)
            {
                if (target?.targetUnit == null) continue;
                if (!PassesAttackDecision(unit, target.targetUnit, attackCell, false, out string atkReason))
                {
                    if (showAILogs)
                        Debug.Log($"[AI] {unit.InstanceId} -> fallback ataque bloqueado por AttackDecision ({target.targetUnit.InstanceId}): {atkReason}");
                    continue;
                }

                Vector3Int targetCell = target.targetUnit.CurrentCellPosition;
                targetCell.z = 0;
                if (showAILogs)
                    Debug.Log($"[AI] {unit.InstanceId} -> fallback ataca {target.targetUnit.InstanceId} de {attackCell}");
                action = BuildAttackBatch(
                    unit, snapshot.AITeam, fromCell, attackCell,
                    target.targetUnit.InstanceId.ToString(), targetCell, paths);
                return true;
            }
        }

        return false;
    }

    private List<(PodeMirarTargetOption opt, int score)> FindAttackTargetsSorted(UnitManager unit, Vector3Int fromCell, bool hasMoved)

    {

        var targets = new List<PodeMirarTargetOption>();

        SensorMovementMode mode = hasMoved

            ? SensorMovementMode.MoveuAndando

            : SensorMovementMode.MoveuParado;

        bool hasAny = PodeMirarSensor.CollectTargets(

            unit, boardTilemap, terrainDatabase, mode, targets, fromCell: fromCell);

        if (!hasAny || targets.Count == 0) return null;

        unit.TryGetUnitData(out UnitData attackerData);

        bool isCapturador = attackerData != null && attackerData.roles != null

            && attackerData.roles.Contains(UnitRole.Capturador);

        var scored = new List<(PodeMirarTargetOption opt, int score)>();

        foreach (PodeMirarTargetOption opt in targets)

        {

            if (opt?.targetUnit == null || opt.targetUnit.IsDead) continue;

            int score = 0;

            BazookaTargetPriority targetPreference = ResolveAssaultTargetPreference(unit, opt.targetUnit);
            score += Mathf.RoundToInt(GetAssaultTargetPreferenceScore(targetPreference));

            // Capturadores priorizam inimigos sobre construções

            if (isCapturador)

            {

                Vector3Int ec = opt.targetUnit.CurrentCellPosition; ec.z = 0;

                if (ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, ec) != null)

                    score += 10000;

            }

            // Preferir inimigos com HP baixo (mais fáceis de eliminar)

            score += (10 - opt.targetUnit.CurrentHP) * 200;

            // Heurística simples: alvo com ≤ 2 HP provavelmente morre

            if (opt.targetUnit.CurrentHP <= 2)

                score += 5000;

            // Penalidade por distância

            score -= opt.distance * 50;

            scored.Add((opt, score));

        }

        scored.Sort((a, b) => b.score.CompareTo(a.score));

        return scored;

    }
}

