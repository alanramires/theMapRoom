using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Capturador Perseguidor - combate imediato ao redor do objetivo
    // -------------------------------------------------------------------------

    private bool TryDecideCapturerPursuerCurrent(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned, Vector3Int fromCell, Vector3Int targetCell, Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied, out PlayerAction action)
    {
        action = null;

        // Prioridade: mover+atacar sempre supera ficar+atacar.
        // Verifica se o scoring loop (hasAttackHex) terá algum inimigo visível atacável
        // a partir de uma célula que avance (ou, para dpqPref, que fique à mesma distância).
        float fromDist = SectorManager.HexDistance(fromCell, targetCell);
        bool fromRouteFound = TryCalculateRouteDistance(unit, fromCell, targetCell, out float fromRouteDist);
        bool dpqPref = unit.TryGetUnitData(out UnitData udPursuer) && udPursuer.prioritizeDpqAtBattle;
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, snapshot.AITeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, targetCell) > fromDist) continue;
            foreach (Vector3Int cell in paths.Keys)
            {
                if (occupied.Contains(cell)) continue;
                float cellDist = SectorManager.HexDistance(cell, targetCell);
                bool cellRouteFound = TryCalculateRouteDistance(unit, cell, targetCell, out float cellRouteDist);
                float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - cellRouteDist : 0f;
                bool advancesByRoute = (!fromRouteFound && cellRouteFound) || routeProgress > 0f;
                bool sameRoute = fromRouteFound && cellRouteFound && Mathf.Abs(routeProgress) <= 0.001f;
                // célula elegível se avança, ou se dpqPref e está à mesma distância (posição DPQ lateral)
                if (!advancesByRoute)
                {
                    if (fromRouteFound && cellRouteFound)
                    {
                        if (!sameRoute || !dpqPref) continue;
                    }
                    else
                    {
                        if (cellDist > fromDist) continue;
                        if (cellDist == fromDist && !dpqPref) continue;
                    }
                }
                if (CanAttackTargetFrom(fromCell, cell, unit, enemy))
                    return false; // scoring loop tem move+attack disponível — ele decide
            }
        }

        // Nenhum avanço com ataque disponível → avalia ataque da posição atual
        if (!HasAttackTargetAtCurrentPos(unit)) return false;

        var stayTargets = new List<PodeMirarTargetOption>();
        PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
            SensorMovementMode.MoveuParado, stayTargets);
        UnitManager bestStayTarget = null;
        float       bestPriority   = float.MinValue;
        foreach (PodeMirarTargetOption opt in stayTargets)
        {
            if (opt?.targetUnit == null) continue;
            if (!PassesAttackDecision(unit, opt.targetUnit, fromCell, assigned.Status == ObjectiveStatus.Defending, out _)) continue;
            Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
            if (SectorManager.HexDistance(tc, targetCell) > fromDist &&
                SectorManager.HexDistance(tc, fromCell) > DefenseEnemyRange) continue;
            float priority = AttackTargetPriorityPursuer(tc, targetCell);
            if (priority > bestPriority) { bestPriority = priority; bestStayTarget = opt.targetUnit; }
        }
        if (bestStayTarget == null) return false;

        TeamObjectivePlan activePlanDef = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        bool mustVacate = activePlanDef != null && IsBlockingCaptureTarget(unit, activePlanDef, snapshot.AITeam);

        if (mustVacate || dpqPref)
        {
            Vector3Int ec = bestStayTarget.CurrentCellPosition; ec.z = 0;
            if (TryFindBestLoSCell(unit, paths, occupied, ec, out Vector3Int combatCell)
                && combatCell != fromCell)
            {
                string reason = mustVacate && dpqPref ? "vacate+DPQ" : mustVacate ? "vacate" : "DPQ";
                Debug.Log($"{TL("Perseguidor")} {unit.InstanceId} [{reason}] reposiciona @ {combatCell} — ataca {bestStayTarget.UnitDisplayName}#{bestStayTarget.InstanceId} @ {ec}");
                action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, combatCell,
                    bestStayTarget.InstanceId.ToString(), ec);
                return true;
            }
        }

        Vector3Int stCell = bestStayTarget.CurrentCellPosition; stCell.z = 0;
        Debug.Log($"{TL("Perseguidor")} {unit.InstanceId} elimina bloqueador de {assigned.Sector} — ataca {bestStayTarget.UnitDisplayName}#{bestStayTarget.InstanceId} @ {stCell}");
        action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, fromCell,
            bestStayTarget.InstanceId.ToString(), stCell);
        return true;
    }
}
