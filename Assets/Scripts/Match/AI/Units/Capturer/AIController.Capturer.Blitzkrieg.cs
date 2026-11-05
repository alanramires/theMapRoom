using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    [System.NonSerialized] private bool difficultyModesValidated;
    [System.NonSerialized] private bool previouslyValidatedHardMode;
    [System.NonSerialized] private bool previouslyValidatedEasyMode;

    private void OnValidate()
    {
        if (hardMode && easyMode)
        {
            bool hardWasJustEnabled = difficultyModesValidated && !previouslyValidatedHardMode;
            bool easyWasJustEnabled = difficultyModesValidated && !previouslyValidatedEasyMode;

            if (easyWasJustEnabled && !hardWasJustEnabled)
                hardMode = false;
            else
                easyMode = false;
        }

        previouslyValidatedHardMode = hardMode;
        previouslyValidatedEasyMode = easyMode;
        difficultyModesValidated = true;
    }

    // Unidade -> prédio cuja primeira etapa ela abriu. Enquanto esse prédio continuar
    // parcial, a unidade permanece liberada para avançar no mesmo eixo.
    private readonly Dictionary<int, int> hardBlitzkriegHandoffs = new Dictionary<int, int>();

    private void RememberHardBlitzkriegCapture(UnitManager unit, TeamId team, Vector3Int cell)
    {
        if (!FazHandoffEmProfundidade || unit == null || team == TeamId.Neutral)
            return;

        cell.z = 0;
        ConstructionManager building = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (building == null || !building.IsCapturable || building.IsRallyPoint || building.IsPlayerHeadQuarter)
            return;

        hardBlitzkriegHandoffs[unit.InstanceId] = building.InstanceId;
    }

    private bool TryDecideHardBlitzkriegHandoff(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        out PlayerAction action)
    {
        action = null;
        if (!FazHandoffEmProfundidade || unit == null || snapshot == null)
            return false;

        bool plannedHandoff = TryGetPlannedHardHandoff(plan, unit, out SectorObjective handoffObjective);
        ConstructionManager origin;
        ConstructionSector advanceSector;

        if (plannedHandoff)
        {
            origin = FindCapturableInSector(handoffObjective.Sector, snapshot.AITeam);
            // A ponta pode ser de OUTRO objetivo (passou pelo predio a caminho do dela): ela
            // segue para o proprio objetivo. A ponta da casa perdeu a vaga ao ceder, nao tem
            // objetivo, e avanca pelo eixo — o mesmo de antes.
            SectorObjective ownObjective = plan != null ? ResolveAssignedObjective(unit, plan) : null;
            advanceSector = ResolveBlitzkriegAdvanceSector(handoffObjective.Sector, ownObjective, snapshot.AITeam);
        }
        else
        {
            if (!hardBlitzkriegHandoffs.TryGetValue(unit.InstanceId, out int buildingId))
                return false;

            origin = FindBlitzkriegConstruction(buildingId);
            if (origin == null || origin.CurrentCapturePoints <= 0
                || origin.CurrentCapturePoints >= origin.CapturePointsMax)
            {
                hardBlitzkriegHandoffs.Remove(unit.InstanceId);
                return false;
            }

            SectorObjective assigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;
            advanceSector = ResolveBlitzkriegAdvanceSector(origin.Sector, assigned, snapshot.AITeam);
        }

        ConstructionSector originSector = origin != null ? origin.Sector : handoffObjective.Sector;
        if (advanceSector == ConstructionSector.None || advanceSector == originSector)
            return false;

        // PREDIO DE OUTRO HERDEIRO NAO E MEU. A ponta que reveza ia para o proprio
        // objetivo mesmo quando ele acabava de ser passado a um herdeiro escolhido:
        // AI Step 2026-11-04, o #4 (poder 7) largou Echo e capturou Golf, que o #10
        // tinha passado ao #16 da base (poder 10, fechava). Segue para alem dele; sem
        // nada alem, a ponta so atira ou segura — devolver false a mandaria ao fluxo
        // normal, que capturaria o mesmo predio.
        bool holdIfNoAttack = false;
        SectorObjective advanceObjective = plan != null ? plan.GetObjectiveForSector(advanceSector) : null;
        if (advanceObjective != null
            && advanceObjective.HandoffHeirUnitId >= 0
            && advanceObjective.HandoffHeirUnitId != unit.InstanceId
            && FindActiveUnit(advanceObjective.HandoffHeirUnitId, snapshot.AITeam) != null)
        {
            ConstructionSector beyond = ComputeBlitzkriegForwardSector(advanceSector, snapshot.AITeam);
            Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} nao toma {advanceSector}: e do herdeiro " +
                      $"#{advanceObjective.HandoffHeirUnitId}" +
                      (beyond != ConstructionSector.None && beyond != advanceSector && beyond != originSector
                          ? $"; segue para {beyond}" : "; nada alem — so ataque ou segura"));
            if (beyond != ConstructionSector.None && beyond != advanceSector && beyond != originSector)
                advanceSector = beyond;
            else
                holdIfNoAttack = true;
        }

        Vector3Int from = unit.CurrentCellPosition;
        from.z = 0;
        ConstructionManager target = FindCapturableInSector(advanceSector, snapshot.AITeam, from);
        Vector3Int targetCell;
        if (target != null)
        {
            targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
        }
        else if (SectorManager.TryGetSectorInfo(advanceSector, out SectorManager.SectorInfo targetInfo)
            && targetInfo != null)
        {
            targetCell = targetInfo.RepresentativeCell;
            targetCell.z = 0;
        }
        else
            return false;
        Dictionary<Vector3Int, List<Vector3Int>> paths = UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        if (paths == null || paths.Count == 0)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        // Predio reservado ao herdeiro: nem captura nem estaciona em cima dele.
        bool targetReachable = !holdIfNoAttack
            && paths.ContainsKey(targetCell) && !occupied.Contains(targetCell);
        if (targetReachable && SimulateCaptureSensor(unit, targetCell, out _))
        {
            Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} hard handoff {originSector} -> captura {advanceSector}"
                + DescribeBlitzkriegHandoff(unit, origin, handoffObjective, plannedHandoff, target, targetCell));
            action = BuildCaptureBatch(unit, snapshot.AITeam, from, targetCell, paths);
            TraceAIReviewHandoffParticipants(action, unit, origin, handoffObjective);
            return true;
        }

        // SAIR NAO E SER EXPULSO. A ponta que reveza volta a ser um capturador com o turno
        // inteiro pela frente: se nao abre o proximo predio agora, antes de andar ela faz o que
        // um capturador faria — atira em quem esta capturando (atrasa a captura dele) ou em quem
        // ameaca um predio nosso (defesa). Andar e o fallback, quando acabam as opcoes.
        // AI Step 2026-11-04: o #6 largou Delta e so andou, com um soldado inimigo capturando
        // Charlie e outro subindo em Echo, ambos ao alcance.
        Vector3Int originCell = origin != null ? origin.CurrentCellPosition : new Vector3Int(int.MinValue, 0, 0);
        originCell.z = 0;
        if (TryDecideBlitzkriegTipAttack(unit, snapshot, from, originCell, paths, occupied, out action))
        {
            TraceAIReviewHandoffParticipants(action, unit, origin, handoffObjective);
            return true;
        }

        if (targetReachable)
        {
            Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} hard handoff {originSector} -> {advanceSector} (move)"
                + DescribeBlitzkriegHandoff(unit, origin, handoffObjective, plannedHandoff, target, targetCell)
                + " | sem ataque util: anda ate o alvo");
            action = BuildMoveBatch(unit, snapshot.AITeam, from, targetCell, paths);
            TraceAIReviewHandoffParticipants(action, unit, origin, handoffObjective);
            return true;
        }

        Vector3Int best = from;
        float bestDistance = SectorManager.HexDistance(from, targetCell);
        int bestSteps = -1;
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> entry in paths)
        {
            Vector3Int candidate = entry.Key;
            if (occupied.Contains(candidate)) continue;
            if (holdIfNoAttack && candidate == targetCell) continue;
            float distance = SectorManager.HexDistance(candidate, targetCell);
            int steps = entry.Value != null ? entry.Value.Count : 0;
            if (distance < bestDistance || (Mathf.Approximately(distance, bestDistance) && steps > bestSteps))
            {
                best = candidate;
                bestDistance = distance;
                bestSteps = steps;
            }
        }

        if (best == from)
            return false;

        Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} hard handoff {originSector} -> {advanceSector} via {best}"
            + DescribeBlitzkriegHandoff(unit, origin, handoffObjective, plannedHandoff, target, targetCell)
            + " | alvo fora de alcance e sem ataque util: avanca pelo hex mais perto dele");
        action = SimulateCaptureSensor(unit, best, out _)
            ? BuildCaptureBatch(unit, snapshot.AITeam, from, best, paths)
            : BuildMoveBatch(unit, snapshot.AITeam, from, best, paths);
        TraceAIReviewHandoffParticipants(action, unit, origin, handoffObjective);
        return true;
    }

    // O handoff é uma decisão de DUAS unidades, e a linha antiga só nomeava uma: "1 hard handoff
    // Alpha -> Echo via (7,11)". No AI Step isso parecia movimento aleatório — não dizia que prédio
    // a ponta largou, em que ponto da captura, quem fica para terminar, nem para onde ela vai.
    // Herdeiro = os outros capturadores do objetivo de origem no plano. Handoff sem plano
    // (só a memória do prédio aberto) não tem herdeiro nomeado, e a linha diz isso.
    private string DescribeBlitzkriegHandoff(
        UnitManager unit, ConstructionManager origin, SectorObjective handoffObjective,
        bool plannedHandoff, ConstructionManager target, Vector3Int targetCell)
    {
        string larga = "sem predio aberto";
        if (origin != null)
        {
            Vector3Int oc = origin.CurrentCellPosition;
            larga = $"{origin.name} ({oc.x},{oc.y}) {origin.CurrentCapturePoints}/{origin.CapturePointsMax}";
        }

        var herdeiros = new List<string>();
        if (handoffObjective != null && handoffObjective.HandoffHeirUnitId >= 0
            && handoffObjective.HandoffHeirUnitId != unit.InstanceId)
            herdeiros.Add("#" + handoffObjective.HandoffHeirUnitId);
        else if (handoffObjective != null && handoffObjective.Slots != null)
            foreach (SlotNeed slot in handoffObjective.Slots)
                if (slot != null && slot.Filled && slot.AssignedUnitId >= 0
                    && slot.AssignedUnitId != unit.InstanceId)
                    herdeiros.Add("#" + slot.AssignedUnitId);
        string herdeiro = herdeiros.Count > 0
            ? string.Join(",", herdeiros)
            : plannedHandoff ? "nenhum no plano" : "fora do plano (memoria do predio)";

        string alvo = target != null
            ? $"{target.name} ({targetCell.x},{targetCell.y})"
            : $"setor ({targetCell.x},{targetCell.y})";

        return $" | larga {larga} | herdeiro {herdeiro} | vai para {alvo}";
    }

    // Ataque da ponta que acabou de revezar. So ataques que servem a captura:
    //   inimigo EM CIMA de um capturavel que nao e dele -> esta capturando; atirar atrasa.
    //   inimigo perto (2h) de um predio NOSSO           -> defesa do que ja temos.
    // Briga sem relacao com predio nao entra: isso e papel do assalto, e o capturador que sai
    // para trocar tiro a toa deixa de capturar. A celula do predio largado nao serve de base de
    // tiro — ela e do herdeiro. O valor do ataque em si fica com o PassesAttackDecision.
    private bool TryDecideBlitzkriegTipAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int handedOffCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null || paths == null)
            return false;

        MatchController mc = GetMatchController();
        int mySlot = snapshot.AISlotIndex;
        var ownBuildingCells = new List<Vector3Int>();
        foreach (ConstructionManager building in ConstructionManager.AllActive)
        {
            if (building == null || !building.IsCapturable) continue;
            Vector3Int c = building.CurrentCellPosition; c.z = 0;
            // Nosso = ja e nosso OU tem capturador nosso em cima (captura em andamento). Sem o
            // segundo caso, o inimigo de HP 7 colado na cidade de Echo que o #4 esta capturando
            // nem entrava no loop (AI Step 2026-11-04): a cidade ainda era neutra.
            if (building.SlotIndex != mySlot && FindOwnCapturerAtCell(c, snapshot.AITeam) == null) continue;
            ownBuildingCells.Add(c);
        }

        var attackCells = new List<Vector3Int>(paths.Keys);
        if (!paths.ContainsKey(fromCell)) attackCells.Add(fromCell);

        UnitManager bestTarget = null;
        Vector3Int bestCell = fromCell;
        float bestScore = float.MinValue;
        string bestReason = "";

        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy == null || enemy.SlotIndex == mySlot || enemy.IsDead || enemy.IsEmbarked)
                continue;
            if (mc != null && !mc.IsUnitVisibleForSlot(enemy, PlayerSlotId.FromIndex(mySlot)))
                continue;
            if (mc != null && mc.GetTeamIdForSlot(enemy.SlotIndex) == snapshot.AITeam)
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition; enemyCell.z = 0;
            string motivo = null;
            float motive = 0f;
            ConstructionManager under = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, enemyCell);
            if (under != null && under.IsCapturable && under.SlotIndex != enemy.SlotIndex)
            {
                bool ours = under.SlotIndex == mySlot;
                motive = ours ? 9000f : 6000f;
                motivo = ours ? $"capturando predio nosso {under.name}" : $"capturando {under.name}";
            }
            else
            {
                foreach (Vector3Int own in ownBuildingCells)
                    if (SectorManager.HexDistance(own, enemyCell) <= 2)
                    {
                        motive = 4000f;
                        motivo = $"ameaca predio nosso ({own.x},{own.y})";
                        break;
                    }
            }
            if (motivo == null) continue;

            BazookaTargetPriority preference = ResolveCapturerTargetPreference(unit, enemy);
            foreach (Vector3Int raw in attackCells)
            {
                Vector3Int cell = raw; cell.z = 0;
                if (cell == handedOffCell) continue;
                if (cell != fromCell && occupied != null && occupied.Contains(cell)) continue;
                if (!CanAttackTargetFrom(fromCell, cell, unit, enemy)) continue;
                if (!PassesAttackDecision(unit, enemy, cell, true, out string decisionReason)) continue;

                float dpq = GetTerrainDpqPontos(cell);
                float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                // Degraus: motivo (dinheiro) > DPQ da celula de tiro > o resto. Quem reveza para
                // lutar luta do melhor terreno: no AI Step 2026-11-04 a montanha (dpq 3) perdeu para
                // o plano (dpq 2) porque a ameaca pesava 80 por ponto e anulava os 400 do DPQ,
                // com o combate previsto identico nos dois. Ameaca, alvo fraco e caminho so
                // desempatam dentro do mesmo DPQ.
                float score = motive * 1000f
                    + dpq * 100000f
                    + GetCapturerTargetPreferenceScore(preference) * 0.1f
                    + Mathf.Max(0, 20 - enemy.CurrentHP) * 100f
                    - Mathf.Clamp(threat, 0f, 500f) * 10f
                    - GetPathStepCount(paths, cell) * 20f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                    bestCell = cell;
                    bestReason = $"{motivo} | hp={enemy.CurrentHP} dpq={dpq:F1} threat={threat:F1} score={score:F0} {decisionReason}";
                }
            }
        }

        if (bestTarget == null)
            return false;

        Vector3Int targetCell = bestTarget.CurrentCellPosition; targetCell.z = 0;
        Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} revezou e ataca {bestTarget.UnitDisplayName}#{bestTarget.InstanceId} " +
                  $"via {bestCell} em vez de so andar ({bestReason})");
        action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, bestCell,
            bestTarget.InstanceId.ToString(), targetCell, paths);
        return true;
    }

    private static bool TryGetPlannedHardHandoff(
        TeamObjectivePlan plan,
        UnitManager unit,
        out SectorObjective objective)
    {
        objective = null;
        if (plan == null || unit == null || plan.HandoffVacaterIds == null
            || !plan.HandoffVacaterIds.Contains(unit.InstanceId))
            return false;

        foreach (SectorObjective candidate in plan.Objectives)
        {
            if (candidate != null
                && candidate.HandoffEligible
                && candidate.PreferredHandoffFromUnitId == unit.InstanceId)
            {
                objective = candidate;
                return true;
            }
        }

        // Saves retomados no meio do turno podem preservar HandoffVacaterIds sem manter
        // PreferredHandoffFromUnitId sincronizado. O marcador de vacate é autoritativo:
        // reconstruímos o objetivo pelo prédio parcial ocupado pela unidade.
        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        foreach (ConstructionManager building in ConstructionManager.AllActive)
        {
            if (building == null || !building.IsCapturable) continue;
            Vector3Int buildingCell = building.CurrentCellPosition;
            buildingCell.z = 0;
            if (buildingCell != cell) continue;
            objective = plan.GetObjectiveForSector(building.Sector);
            if (objective != null)
                return true;
        }
        return false;
    }

    private ConstructionSector ResolveBlitzkriegAdvanceSector(
        ConstructionSector originSector,
        SectorObjective assigned,
        TeamId team)
    {
        if (assigned != null && assigned.Sector != ConstructionSector.None && assigned.Sector != originSector)
            return assigned.Sector;

        // Mesmo caminho do plano: eixo, e sem eixo o vizinho a frente. Antes, sem eixo, este
        // ramo devolvia None e a ponta ficava parada — os dois caminhos do blitz discordavam.
        return ComputeBlitzkriegForwardSector(originSector, team);
    }

    private ConstructionSector ComputeBlitzkriegForwardSector(ConstructionSector sector, TeamId team)
    {
        InvasionAxisMap axisMap = InvasionAxisMap.Build(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(team)), boardTilemap);
        int axisIndex = axisMap.GetEixo(sector);
        if (axisIndex > 0)
        {
            ConstructionSector axisForward = axisMap.GetTransportTargetSector(axisIndex);
            if (axisForward != ConstructionSector.None && axisForward != sector)
                return axisForward;
        }

        return ComputeForwardNeighborSector(sector, team);
    }

    private static ConstructionManager FindBlitzkriegConstruction(int instanceId)
    {
        foreach (ConstructionManager building in ConstructionManager.AllActive)
            if (building != null && building.InstanceId == instanceId)
                return building;
        return null;
    }
}
