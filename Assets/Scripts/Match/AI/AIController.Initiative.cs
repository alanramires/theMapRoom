using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------

    // Helpers

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

    // 1 = reparo sobre capturável não-completo (libera prédio),

    // 2 = objetivo normal, 3 = rogue/sem objetivo, 4 = reparo em campo (age por último).

    private int GetInitiativeGroup(UnitManager unit, TeamObjectivePlan plan, TeamId aiTeam)

    {

        if (plan != null && plan.HandoffVacaterIds.Contains(unit.InstanceId)) return 0;

        // Blocker: unidade está sobre o objetivo de captura de outro capturador designado.
        // Age primeiro (grupo 0) para liberar o hex — com ou sem inimigos adjacentes.
        if (plan != null && IsBlockingCaptureTarget(unit, plan, aiTeam)) return 0;

        if (unit.IsUnderRepair)

        {

            Vector3Int cell = unit.CurrentCellPosition; cell.z = 0;

            if (ShouldDelayRepairInitiative(unit, cell, aiTeam)) return 4;

            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);

            if (bldg != null && bldg.IsCapturable) return 1;

            // Unidade no corredor de avanço de algum objetivo ativo: age antes dos capturadores
            // (grupo 1) para liberar o hex. Caso contrário age por último (grupo 4).
            if (plan != null && IsRepairUnitInActiveCorridor(unit, cell, plan, aiTeam)) return 1;

            return 4;

        }

        // Capturador no corredor de outro setor (mais perto do objetivo alheio que o capturador
        // designado a ele) → age antes (grupo 1) para liberar o caminho.
        if (!unit.IsUnderRepair && plan != null)
        {
            Vector3Int unitCell = unit.CurrentCellPosition; unitCell.z = 0;
            if (IsCapturerInOtherCapturerCorridor(unit, unitCell, plan, aiTeam)) return 1;
        }

        // Assault escort mais perto do objetivo que o capturador designado ao mesmo setor
        // → age antes (grupo 1) para liberar o corredor de avanço.
        if (!unit.IsUnderRepair && plan != null)
        {
            Vector3Int escortCell = unit.CurrentCellPosition; escortCell.z = 0;
            if (IsAssaultEscortInCapturerCorridor(unit, escortCell, plan, aiTeam)) return 1;
        }

        // Transportador rogue vazio com candidato de pickup no alcance →
        // age antes dos capturadores (grupo 1) para se posicionar adjacente.
        if (!unit.IsUnderRepair && IsTransporterWithValidPickupCandidate(unit, plan, aiTeam)) return 1;

        bool hasObjective = plan != null && ResolveAnyAssignedObjective(unit, plan) != null;

        return hasObjective ? 2 : 3;

    }

    // Retorna true se o transportador está vazio e tem pelo menos um candidato de pickup
    // dentro do alcance de movimento (+1 para adjacência). Checagem barata: só hex distance.
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
            if (FindFittingSlotIndex(unit, data, candidateData) < 0) continue;
            return true;
        }

        return false;
    }

    // Retorna true se um capturador está mais perto do objetivo de OUTRO setor do que
    // o capturador designado a ele — passa pelo corredor alheio e deve agir primeiro.
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

    // Retorna true se a unidade de reparo está mais perto de algum objetivo ativo do que
    // o capturador designado a ele — ou seja, está no corredor de avanço e pode bloquear.
    private bool IsRepairUnitInActiveCorridor(UnitManager unit, Vector3Int repairCell, TeamObjectivePlan plan, TeamId aiTeam)
    {
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj.Status == ObjectiveStatus.Defending) continue;
            ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
            if (tgt == null) continue;
            Vector3Int objCell = tgt.CurrentCellPosition; objCell.z = 0;
            float repairDist = SectorManager.HexDistance(repairCell, objCell);
            foreach (SlotNeed slot in obj.Slots)
            {
                if (!slot.Filled) continue;
                UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (capturer == null) continue;
                Vector3Int capCell = capturer.CurrentCellPosition; capCell.z = 0;
                if (repairDist < SectorManager.HexDistance(capCell, objCell))
                    return true;
            }
        }
        return false;
    }

    private bool ShouldDelayRepairInitiative(UnitManager unit, Vector3Int repairCell, TeamId aiTeam)
    {
        if (!HasNearbyVisibleEnemy(repairCell, aiTeam, AlliesEnemyRange))
            return true;

        if (!TryFindTeamHQCell(aiTeam, out Vector3Int hqCell))
            return false;

        return SectorManager.HexDistance(repairCell, hqCell) <= DefenseEnemyRange;
    }

    private static bool TryFindTeamHQCell(TeamId team, out Vector3Int hqCell)
    {
        hqCell = Vector3Int.zero;
        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.TeamId != team || !construction.IsPlayerHeadQuarter)
                continue;

            hqCell = construction.CurrentCellPosition;
            hqCell.z = 0;
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
        return ResolveAssignedAssaultObjective(unit, plan);
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

    private HashSet<Vector3Int> BuildOccupied(UnitManager excludeUnit)

    {

        var set = new HashSet<Vector3Int>();

        foreach (UnitManager u in UnitManager.AllActive)

        {

            if (u == excludeUnit || u.IsEmbarked || u.IsDead) continue;

            Vector3Int p = u.CurrentCellPosition; p.z = 0;

            set.Add(p);

        }

        return set;

    }
}
