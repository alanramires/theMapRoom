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
        // Perseguidor: inimigos próximos ao objetivo — elimina antes de avançar
        if (HasAttackTargetAtCurrentPos(unit))
        {
            var stayTargets = new List<PodeMirarTargetOption>();
            PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                SensorMovementMode.MoveuParado, stayTargets);
            UnitManager bestStayTarget = null;
            float       bestPriority   = float.MinValue;
            foreach (PodeMirarTargetOption opt in stayTargets)
            {
                if (opt?.targetUnit == null) continue;
                Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                if (SectorManager.HexDistance(tc, targetCell) > DefenseEnemyRange &&
                    SectorManager.HexDistance(tc, fromCell) > DefenseEnemyRange) continue;
                float priority = AttackTargetPriorityPursuer(tc, targetCell);
                if (priority > bestPriority) { bestPriority = priority; bestStayTarget = opt.targetUnit; }
            }
            if (bestStayTarget != null)
            {
                TeamObjectivePlan activePlanDef = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
                bool mustVacate = activePlanDef != null && IsBlockingCaptureTarget(unit, activePlanDef, snapshot.AITeam);
                bool dpqPref    = unit.TryGetUnitData(out UnitData udDef) && udDef.prioritizeDpqAtBattle;

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
        return false;
    }
}
