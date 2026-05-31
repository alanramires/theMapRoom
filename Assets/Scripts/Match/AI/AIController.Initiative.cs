using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------

    // Helpers
    // Retorna a dist�ncia em hexes do centro da unidade at� o centro do seu objetivo designado, 
    // ou float.MaxValue se n�o tiver objetivo.

    // -------------------------------------------------------------------------

    private List<UnitManager> GetAvailableUnits(TeamId aiTeam)

    {

        var list = new List<UnitManager>();

        foreach (UnitManager u in UnitManager.AllActive)

        {

            if (u.TeamId != aiTeam || u.HasActed || u.IsDead || u.IsEmbarked)

                continue;

            list.Add(u);

        }

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);

        list.Sort((a, b) =>

        {

            // Unidades mais próximas do objetivo agem primeiro — evita bloqueio de rotas

            float da = GetDistanceToAssignedTarget(a, aiTeam, plan);

            float db = GetDistanceToAssignedTarget(b, aiTeam, plan);

            int cmp = da.CompareTo(db);

            if (cmp != 0) return cmp;

            // Desempate: aiInitiative menor age primeiro, depois HP maior

            int ia = a.TryGetUnitData(out UnitData ua) ? (int)ua.aiInitiative : (int)AiInitiative.Medium;

            int ib = b.TryGetUnitData(out UnitData ub) ? (int)ub.aiInitiative : (int)AiInitiative.Medium;

            cmp = ia.CompareTo(ib);

            return cmp != 0 ? cmp : b.CurrentHP.CompareTo(a.CurrentHP);

        });

        return list;

    }

    private static Vector3Int? GetAssignedTargetCell(UnitManager unit, TeamObjectivePlan plan)

    {

        SectorObjective obj = ResolveAnyAssignedObjective(unit, plan);

        if (obj == null) return null;

        ConstructionManager tgt = FindCapturableInSector(obj.Sector, unit.TeamId);

        if (tgt == null) return null;

        Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;

        return tc;

    }

    // Grupo de iniciativa (menor = age primeiro):

    // 0 = vacater handoff ou blocker com inimigos adjacentes (libera o hex para o capturador),

    // 1 = helicoptero (posiciona antes da coluna terrestre),

    // 2 = unidade ativa liberando corredor/posicionamento,

    // 3 = objetivo normal, 4 = rogue/sem objetivo, 5 = reparo/manutencao (age por ultimo).

    private int GetInitiativeGroup(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)

    {

        if (plan != null && plan.HandoffVacaterIds.Contains(unit.InstanceId)) return 0;

        // Blocker: unidade está sobre o objetivo de captura de outro capturador designado.
        // Age primeiro (grupo 0) para liberar o hex — com ou sem inimigos adjacentes.
        if (plan != null && IsBlockingCaptureTarget(unit, plan, aiTeam)) return 0;

        // Transportador com passageiro formal ainda nao agido precisa se posicionar
        // antes do capturador, para que o embarque seja avaliado no turno do passageiro.
        if (plan != null && IsAssignedTransporterWithUnactedPassenger(unit, plan, aiTeam)) return 0;

        // Rogue parado na ponta/corredor de um capturador formal deve agir antes dele:
        // sair do caminho, embarcar, ou continuar como oportunista sem bloquear o slot bom.
        if (!unit.IsUnderRepair && plan != null && IsRogueBlockingAssignedCapturerCorridor(unit, plan, aiTeam)) return 0;

        // Qualquer unidade (rogue ou assigned, capturador ou assalto) que ocupa hex adjacente
        // ao transporter cujo passageiro formal ainda nao agiu → sai na frente para liberar o staging.
        if (!unit.IsUnderRepair && plan != null && TryFindAssignedEmbarkStagingBlockedBy(unit, plan, aiTeam, out _, out _)) return 0;

        // Se um capturador inimigo esta entocado em construcao nossa, fogo de suporte
        // com tiro possivel deve agir antes da infantaria defensora se reposicionar.
        if (!unit.IsUnderRepair && HasFireSupportShotAtOwnedConstructionCapturer(unit, aiTeam)) return 0;

        // Swap: capturador fraco sobre o edificio do seu objetivo cede o hex para o
        // colega mais forte do mesmo objetivo que consegue chegar este turno.
        if (!unit.IsUnderRepair && plan != null && HasSwapIncomingCapturerFast(unit, plan, aiTeam)) return 0;

        // Manutencao nao preempta a fila. Se estiver em cima de alvo de captura,
        // IsBlockingCaptureTarget ja colocou no grupo 0 acima.
        if (unit.IsUnderRepair) return 5;

        if (IsHelicopterInitiativeUnit(unit)) return 1;

        if (HasFireSupportAttackInCurrentPosition(unit, aiTeam)) return 2;

        // Capturador no corredor de outro setor (mais perto do objetivo alheio que o capturador
        // designado a ele) → age antes (grupo 1) para liberar o caminho.
        if (!unit.IsUnderRepair && plan != null)
        {
            Vector3Int unitCell = unit.CurrentCellPosition; unitCell.z = 0;
            if (IsCapturerInOtherCapturerCorridor(unit, unitCell, plan, aiTeam)) return 2;
        }

        // Assault escort mais perto do objetivo que o capturador designado ao mesmo setor
        // → age antes (grupo 1) para liberar o corredor de avanço.
        if (!unit.IsUnderRepair && plan != null)
        {
            Vector3Int escortCell = unit.CurrentCellPosition; escortCell.z = 0;
            if (IsAssaultEscortInCapturerCorridor(unit, escortCell, plan, aiTeam)) return 2;
        }

        // Transportador rogue vazio com candidato de pickup no alcance →
        // age antes dos capturadores (grupo 1) para se posicionar adjacente.
        if (!unit.IsUnderRepair && IsTransporterWithValidPickupCandidate(unit, plan, aiTeam)) return 2;

        // Capturador cujo objetivo tem um transportador designado vivo e vazio:
        // o transportador age no grupo 0; o capturador age logo em seguida (grupo 1)
        // para garantir embarque antes que outras unidades ocupem os hexes adjacentes.
        if (!unit.IsUnderRepair && plan != null
            && IsCapturerWithAvailableAssignedTransporter(unit, plan, aiTeam)) return 2;

        bool hasObjective = plan != null && ResolveAnyAssignedObjective(unit, plan) != null;

        return hasObjective ? 3 : 4;

    }

    private static bool IsHelicopterInitiativeUnit(UnitManager unit)
    {
        if (unit == null)
            return false;

        return unit.GetAircraftType() == AircraftType.Helicopter;
    }

    private static string FormatInitiativeUnitName(UnitManager unit)
    {
        if (unit == null)
            return "Unit?";

        string displayName = !string.IsNullOrWhiteSpace(unit.UnitDisplayName)
            ? unit.UnitDisplayName.Trim()
            : "Unit";

        return $"{displayName}#{unit.InstanceId}";
    }

    // Retorna true se o transportador está vazio e tem pelo menos um candidato de pickup
    // dentro do alcance de movimento (+1 para adjacência). Checagem barata: só hex distance.
    private bool HasFireSupportAttackInCurrentPosition(UnitManager unit, TeamId aiTeam)
    {
        if (!IsFireSupportUnit(unit))
            return false;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        var targets = new List<PodeMirarTargetOption>();
        WeaponPriorityData weaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;
        if (!PodeMirarSensor.CollectTargets(
                unit,
                boardTilemap,
                terrainDatabase,
                SensorMovementMode.MoveuParado,
                targets,
                weaponPriorityData: weaponPriorityData,
                dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
                fromCell: fromCell))
            return false;

        foreach (PodeMirarTargetOption opt in targets)
        {
            if (opt == null || opt.targetUnit == null) continue;
            if (opt.targetUnit.TeamId == aiTeam || opt.targetUnit.IsDead) continue;
            if (PassesAttackDecision(unit, opt.targetUnit, fromCell, defensiveContext: false, out _))
                return true;
        }

        return false;
    }

    private bool HasFireSupportShotAtOwnedConstructionCapturer(UnitManager unit, TeamId aiTeam)
    {
        if (!IsFireSupportUnit(unit))
            return false;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildFireSupportPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        bool stationary = IsLongRangeStationary(unit);
        WeaponPriorityData weaponPriorityData = turnStateManager != null ? turnStateManager.WeaponPriorityDataRef : null;

        foreach (Vector3Int rawCell in EnumerateFireSupportCandidateCells(fromCell, paths, stationary))
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;

            SensorMovementMode mode = cell != fromCell
                ? SensorMovementMode.MoveuAndando
                : SensorMovementMode.MoveuParado;

            var targets = new List<PodeMirarTargetOption>();
            if (!PodeMirarSensor.CollectTargets(
                    unit,
                    boardTilemap,
                    terrainDatabase,
                    mode,
                    targets,
                    weaponPriorityData: weaponPriorityData,
                    dpqAirHeightConfig: turnStateManager != null ? turnStateManager.DpqAirHeightConfigRef : null,
                    fromCell: cell))
                continue;

            for (int i = 0; i < targets.Count; i++)
            {
                UnitManager target = targets[i] != null ? targets[i].targetUnit : null;
                if (!IsEnemyCapturerOnOwnedConstruction(target, aiTeam))
                    continue;
                if (PassesAttackDecision(unit, target, cell, defensiveContext: true, out _))
                    return true;
            }
        }

        return false;
    }

    private bool IsEnemyCapturerOnOwnedConstruction(UnitManager target, TeamId aiTeam)
    {
        if (target == null || target.TeamId == aiTeam || target.IsDead || target.IsEmbarked)
            return false;
        if (!target.TryGetUnitData(out UnitData targetData)
            || targetData == null
            || targetData.roles == null
            || !targetData.roles.Contains(UnitRole.Capturador))
            return false;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, targetCell);
        return construction != null
            && construction.IsCapturable
            && construction.TeamId == aiTeam
            && construction.CurrentCapturePoints < construction.CapturePointsMax;
    }

    private bool IsTransporterWithValidPickupCandidate(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Transportador) return false;

        if (HasTransportCargo(unit)) return false;

        Vector3Int transporterCell = unit.CurrentCellPosition; transporterCell.z = 0;
        float reach = Mathf.Max(0, unit.RemainingMovementPoints) + 1f;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == unit) continue;
            if (candidate.TeamId != aiTeam || candidate.IsDead || candidate.IsEmbarked || candidate.HasActed) continue;
            if (!candidate.TryGetUnitData(out UnitData candidateData)) continue;
            Vector3Int cc = candidate.CurrentCellPosition; cc.z = 0;
            if (SectorManager.HexDistance(transporterCell, cc) > reach) continue;
            if (FindFittingSlotIndex(unit, data, candidate, candidateData) < 0) continue;
            return true;
        }

        return false;
    }

    // Retorna true se este capturador tem um slot de transportador preenchido no seu objetivo
    // e o transportador atribuído está vivo e vazio (pronto para embarque).
    private bool IsCapturerWithAvailableAssignedTransporter(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Capturador) return false;

        SectorObjective obj = ResolveAssignedObjective(unit, plan);
        if (obj == null) return false;

        foreach (SlotNeed slot in obj.Slots)
        {
            if (slot.Role != UnitRole.Transportador || !slot.Filled) continue;
            UnitManager transporter = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (transporter == null || transporter.IsDead || transporter.IsEmbarked) continue;
            if (HasTransportCargo(transporter)) continue;
            return true;
        }
        return false;
    }

    // Retorna true se um capturador está mais perto do objetivo de OUTRO setor do que
    // o capturador designado a ele — passa pelo corredor alheio e deve agir primeiro.
    private bool IsAssignedTransporterWithUnactedPassenger(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Transportador) return false;

        if (HasTransportCargo(unit)) return false;

        SectorObjective assigned = ResolveAssignedTransportObjective(unit, plan);
        if (assigned == null) return false;

        UnitManager passenger = ResolveAssignedPassengerUnit(assigned, aiTeam);
        return passenger != null && !passenger.HasActed;
    }

    private bool IsRogueBlockingAssignedCapturerCorridor(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (unit == null || plan == null || plan.RogueUnitIds == null || !plan.RogueUnitIds.Contains(unit.InstanceId))
            return false;

        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || !data.roles.Contains(UnitRole.Capturador))
            return false;

        Vector3Int rogueCell = unit.CurrentCellPosition; rogueCell.z = 0;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Status == ObjectiveStatus.Defending) continue;
            ConstructionManager target = FindCapturableInSector(obj.Sector, aiTeam);
            if (target == null) continue;

            Vector3Int targetCell = target.CurrentCellPosition; targetCell.z = 0;
            float rogueDist = SectorManager.HexDistance(rogueCell, targetCell);
            if (rogueDist > 6f) continue;

            foreach (SlotNeed slot in obj.Slots)
            {
                if (slot == null || !slot.Filled || slot.Role != UnitRole.Capturador) continue;
                UnitManager assigned = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (assigned == null || assigned == unit || assigned.HasActed || assigned.IsEmbarked || assigned.IsDead) continue;

                // Se o capturador formal tem transporte atribuido pronto, nao preempta
                // com bloqueio generico de corredor. Bloqueio fisico de staging continua
                // coberto por TryFindAssignedEmbarkStagingBlockedBy.
                if (IsCapturerWithAvailableAssignedTransporter(assigned, plan, aiTeam))
                    continue;

                Vector3Int assignedCell = assigned.CurrentCellPosition; assignedCell.z = 0;
                float assignedDist = SectorManager.HexDistance(assignedCell, targetCell);
                if (rogueDist > assignedDist - 0.5f) continue;

                float lateralGap = SectorManager.HexDistance(rogueCell, assignedCell);
                if (lateralGap > Mathf.Max(3f, assigned.RemainingMovementPoints + 1f)) continue;

                Debug.Log($"{TL()} iniciativa: rogue {unit.InstanceId} libera corredor de {assigned.InstanceId}->{obj.Sector} (rogueDist={rogueDist:F0} capDist={assignedDist:F0})");
                return true;
            }
        }

        return false;
    }

    private bool IsRogueBlockingAssignedEmbarkStaging(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (unit == null || plan == null || plan.RogueUnitIds == null || !plan.RogueUnitIds.Contains(unit.InstanceId))
            return false;

        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || !data.roles.Contains(UnitRole.Capturador))
            return false;

        return TryFindAssignedEmbarkStagingBlockedBy(unit, plan, aiTeam, out _, out _);
    }

    private bool ShouldDeferCapturerForRogueEmbarkBlocker(
        UnitManager unit,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        out UnitManager blocker,
        out UnitManager transporter)
    {
        blocker = null;
        transporter = null;

        if (unit == null || plan == null || unit.HasActed || unit.IsEmbarked || unit.IsDead)
            return false;

        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Capturador)
            return false;

        UnitManager bestBlocker = null;
        UnitManager bestTransporter = null;

        SectorObjective assigned = ResolveAssignedObjective(unit, plan);
        if (assigned == null)
        {
            if (TryFindRogueEmbarkStagingBlockedBy(unit, plan, aiTeam, out bestBlocker, out bestTransporter))
            {
                blocker = bestBlocker;
                transporter = bestTransporter;
                return true;
            }

            return false;
        }

        UnitManager formalPassenger = ResolveAssignedPassengerUnit(assigned, aiTeam);
        if (formalPassenger != unit) return false;

        float bestDist = float.MaxValue;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == null || candidate.TeamId != aiTeam) continue;
            if (candidate == unit || candidate.HasActed || candidate.IsDead || candidate.IsEmbarked) continue;
            if (plan.RogueUnitIds == null || !plan.RogueUnitIds.Contains(candidate.InstanceId)) continue;

            if (!TryFindAssignedEmbarkStagingBlockedBy(candidate, plan, aiTeam, out UnitManager t, out SectorObjective tObj))
                continue;
            if (tObj != assigned) continue;

            Vector3Int uc = unit.CurrentCellPosition; uc.z = 0;
            Vector3Int bc = candidate.CurrentCellPosition; bc.z = 0;
            float dist = SectorManager.HexDistance(uc, bc);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestBlocker = candidate;
                bestTransporter = t;
            }
        }

        blocker = bestBlocker;
        transporter = bestTransporter;
        return blocker != null && transporter != null;
    }

    private bool TryFindRogueEmbarkStagingBlockedBy(
        UnitManager unit,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        out UnitManager blocker,
        out UnitManager transporter)
    {
        blocker = null;
        transporter = null;

        if (unit == null || plan == null || plan.RogueUnitIds == null || !plan.RogueUnitIds.Contains(unit.InstanceId))
            return false;

        if (!unit.TryGetUnitData(out UnitData unitData) || unitData == null
            || unitData.roles == null || !unitData.roles.Contains(UnitRole.Capturador))
            return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;

        UnitManager bestBlocker = null;
        UnitManager bestTransporter = null;
        float bestScore = float.MaxValue;
        var neighbors = new List<Vector3Int>(6);

        foreach (UnitManager t in UnitManager.AllActive)
        {
            if (t == null || t == unit || t.TeamId != aiTeam || t.IsDead || t.IsEmbarked || t.IsUnderRepair)
                continue;
            if (!t.TryGetUnitData(out UnitData tData) || tData == null || !tData.isTransporter)
                continue;
            if (FindFittingSlotIndex(t, tData, unit, unitData) < 0)
                continue;

            SectorObjective tObj = ResolveAssignedTransportObjective(t, plan);
            if (tObj != null && !CanRogueUseAssignedTransporter(unit, t, tObj, aiTeam))
                continue;

            Vector3Int tCell = t.CurrentCellPosition;
            tCell.z = 0;
            if (SectorManager.HexDistance(unitCell, tCell) > Mathf.Max(8f, unit.RemainingMovementPoints + ShuttlePickupRange + 2f))
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, tCell, neighbors);
            foreach (Vector3Int rawStop in neighbors)
            {
                Vector3Int stopCell = rawStop;
                stopCell.z = 0;

                UnitManager candidateBlocker = FindUnactedRogueEmbarkBlockerAt(stopCell, unit, plan, aiTeam);
                if (candidateBlocker == null)
                    continue;

                float score = SectorManager.HexDistance(unitCell, stopCell)
                    + SectorManager.HexDistance(stopCell, tCell) * 0.1f;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestBlocker = candidateBlocker;
                bestTransporter = t;
            }
        }

        blocker = bestBlocker;
        transporter = bestTransporter;
        return blocker != null && transporter != null;
    }

    private UnitManager FindUnactedRogueEmbarkBlockerAt(
        Vector3Int cell,
        UnitManager passenger,
        TeamObjectivePlan plan,
        TeamId aiTeam)
    {
        cell.z = 0;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == null || candidate == passenger)
                continue;
            if (candidate.TeamId != aiTeam || candidate.HasActed || candidate.IsDead || candidate.IsEmbarked || candidate.IsUnderRepair)
                continue;
            if (plan.RogueUnitIds == null || !plan.RogueUnitIds.Contains(candidate.InstanceId))
                continue;
            if (!candidate.TryGetUnitData(out UnitData candidateData) || candidateData == null
                || candidateData.roles == null || !candidateData.roles.Contains(UnitRole.Capturador))
                continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition;
            candidateCell.z = 0;
            if (candidateCell != cell)
                continue;

            if (OccupancyResolver.GetHeightBand(candidate) != OccupancyResolver.GetHeightBand(passenger))
                continue;

            return candidate;
        }

        return null;
    }

    private bool TryFindAssignedEmbarkStagingBlockedBy(
        UnitManager blocker,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        out UnitManager transporter,
        out SectorObjective transportObjective)
    {
        transporter = null;
        transportObjective = null;

        if (blocker == null || plan == null) return false;
        Vector3Int blockerCell = blocker.CurrentCellPosition; blockerCell.z = 0;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Status == ObjectiveStatus.Defending) continue;
            UnitManager passenger = ResolveAssignedPassengerUnit(obj, aiTeam);
            if (passenger == null || passenger.HasActed || passenger.IsEmbarked || passenger.IsDead) continue;

            foreach (SlotNeed slot in obj.Slots)
            {
                if (slot == null || slot.Role != UnitRole.Transportador || !slot.Filled) continue;
                UnitManager t = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (t == null || t.IsDead || t.IsEmbarked || HasTransportCargo(t)) continue;
                if (!t.TryGetUnitData(out UnitData tData) || tData == null || !tData.isTransporter) continue;

                if (!passenger.TryGetUnitData(out UnitData passengerData) || passengerData == null) continue;
                if (FindFittingSlotIndex(t, tData, passenger, passengerData) < 0) continue;

                Vector3Int tCell = t.CurrentCellPosition; tCell.z = 0;
                if (SectorManager.HexDistance(passenger.CurrentCellPosition, tCell) > 8f) continue;

                var neighbors = new List<Vector3Int>(6);
                UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, tCell, neighbors);
                foreach (Vector3Int nRaw in neighbors)
                {
                    Vector3Int n = nRaw; n.z = 0;
                    if (n != blockerCell) continue;
                    transporter = t;
                    transportObjective = obj;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsCapturerInOtherCapturerCorridor(UnitManager unit, Vector3Int unitCell, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Capturador) return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj.Status == ObjectiveStatus.Defending) continue;

            bool isOwnSector = false;
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && slot.AssignedUnitId == unit.InstanceId) { isOwnSector = true; break; }
            if (isOwnSector) continue;

            ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
            if (tgt == null) continue;

            Vector3Int objCell = tgt.CurrentCellPosition; objCell.z = 0;
            float myDist = SectorManager.HexDistance(unitCell, objCell);

            foreach (SlotNeed slot in obj.Slots)
            {
                if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
                UnitManager assigned = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (assigned == null) continue;
                Vector3Int assignedCell = assigned.CurrentCellPosition; assignedCell.z = 0;
                if (myDist < SectorManager.HexDistance(assignedCell, objCell))
                    return true;
            }
        }
        return false;
    }

    // Retorna true se o assault escort está mais perto do objetivo do seu setor do que
    // o capturador designado a ele — ou seja, está no corredor de avanço e pode bloquear.
    private bool IsAssaultEscortInCapturerCorridor(UnitManager escort, Vector3Int escortCell, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (!escort.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Assalto) return false;

        SectorObjective obj = ResolveAssignedAssaultObjective(escort, plan);
        if (obj == null || obj.Status == ObjectiveStatus.Defending) return false;

        ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
        if (tgt == null) return false;

        Vector3Int objCell = tgt.CurrentCellPosition; objCell.z = 0;
        float escortDist = SectorManager.HexDistance(escortCell, objCell);

        foreach (SlotNeed slot in obj.Slots)
        {
            if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
            UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (capturer == null) continue;
            Vector3Int capCell = capturer.CurrentCellPosition; capCell.z = 0;
            if (escortDist < SectorManager.HexDistance(capCell, objCell))
                return true;
        }
        return false;
    }

    private bool IsBlockingCaptureTargetWithEnemies(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)

        => IsBlockingCaptureTarget(unit, plan, aiTeam) && HasEnemyNearCell(unit.CurrentCellPosition, aiTeam);

    // Retorna true se a unidade está fisicamente sobre o alvo de captura de outro capturador designado.

    private bool IsBlockingCaptureTarget(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)

    {

        Vector3Int cell = unit.CurrentCellPosition; cell.z = 0;

        foreach (SectorObjective obj in plan.Objectives)

            foreach (SlotNeed slot in obj.Slots)

            {

                if (!slot.Filled || slot.AssignedUnitId == unit.InstanceId) continue;

                ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);

                if (tgt == null) continue;

                Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;

                if (tc == cell) return true;

            }

        return false;

    }

    private static SectorObjective ResolveAnyAssignedObjective(UnitManager unit, TeamObjectivePlan plan)
    {
        SectorObjective capturerObjective = ResolveAssignedObjective(unit, plan);
        if (capturerObjective != null) return capturerObjective;
        SectorObjective assaultObjective = ResolveAssignedAssaultObjective(unit, plan);
        if (assaultObjective != null) return assaultObjective;
        return ResolveAssignedFireSupportObjective(unit, plan);
    }

    private static int CompareUnitInitiative(UnitManager a, UnitManager b)
    {
        int ia = a != null && a.TryGetUnitData(out UnitData ua)
            ? (int)ua.aiInitiative
            : (int)AiInitiative.Medium;
        int ib = b != null && b.TryGetUnitData(out UnitData ub)
            ? (int)ub.aiInitiative
            : (int)AiInitiative.Medium;

        return ia.CompareTo(ib);
    }

    // Distância ao transporter mais próximo que tem slot compatível livre para esta unidade.
    // Retorna float.MaxValue se não houver nenhum disponível.
    private float GetDistanceToNearestAvailableTransporter(UnitManager unit, TeamId aiTeam)
    {
        if (unit == null) return float.MaxValue;
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.roles == null || !data.roles.Contains(UnitRole.Capturador))
            return float.MaxValue;

        Vector3Int unitCell = unit.CurrentCellPosition; unitCell.z = 0;
        float best = float.MaxValue;

        foreach (UnitManager t in UnitManager.AllActive)
        {
            if (t == null || t.TeamId != aiTeam || t.IsDead || t.IsEmbarked) continue;
            if (!t.TryGetUnitData(out UnitData tData) || tData == null || !tData.isTransporter) continue;
            if (FindFittingSlotIndex(t, tData, unit, data) < 0) continue;
            Vector3Int tCell = t.CurrentCellPosition; tCell.z = 0;
            float dist = SectorManager.HexDistance(unitCell, tCell);
            if (dist < best) best = dist;
        }

        return best;
    }

    private HashSet<Vector3Int> BuildOccupied(UnitManager excludeUnit)

    {

        var set = new HashSet<Vector3Int>();

        foreach (UnitManager u in UnitManager.AllActive)

        {

            if (u == excludeUnit || u.IsEmbarked || u.IsDead) continue;

            Vector3Int p = GetLiveUnitCell(u, syncState: true);

            set.Add(p);

        }

        return set;

    }
}
