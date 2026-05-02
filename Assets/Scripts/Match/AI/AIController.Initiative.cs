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

            if (u.TeamId != aiTeam || u.HasActed || u.IsDead || u.IsEmbarked || u.HasMerged)

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

        SectorObjective obj = ResolveAssignedObjective(unit, plan);

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

        // Blocker com ameaça: unidade está sobre o alvo de outro capturador E tem inimigo adjacente.

        // Age primeiro para engajar o inimigo e liberar o hex antes do capturador designado agir.

        if (plan != null && IsBlockingCaptureTargetWithEnemies(unit, plan, aiTeam)) return 0;

        if (unit.IsUnderRepair)

        {

            Vector3Int cell = unit.CurrentCellPosition; cell.z = 0;

            ConstructionManager bldg = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);

            // Qualquer construção capturável (incompleta ou posição defensiva conquistada):

            // age cedo (grupo 1) para vacatar e tentar fundir com aliados antes que se dispersem.

            bool onAnyBuilding = bldg != null && bldg.IsCapturable;

            return onAnyBuilding ? 1 : 4;

        }

        bool hasObjective = plan != null && ResolveAssignedObjective(unit, plan) != null;

        return hasObjective ? 2 : 3;

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

    private HashSet<Vector3Int> BuildOccupied(UnitManager excludeUnit)

    {

        var set = new HashSet<Vector3Int>();

        foreach (UnitManager u in UnitManager.AllActive)

        {

            if (u == excludeUnit || u.IsEmbarked || u.IsDead) continue;

            Vector3Int p = u.CurrentCellPosition; p.z = 0;

            set.Add(p);

        }

        // inclui destinos já reservados por unidades que agiram antes neste turno

        foreach (Vector3Int planned in plannedDestinations) set.Add(planned);

        return set;

    }
}
