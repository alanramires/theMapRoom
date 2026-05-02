using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Capturador Explorador - revela alvo oculto por FoW
    // -------------------------------------------------------------------------

    private bool TryDecideCapturerExplorer(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned, Vector3Int fromCell, Vector3Int targetCell, Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied, out PlayerAction action)
    {
        action = null;
        // Explorador: ocupante invisível no alvo → DPQ mais elevado + ataque lateral oportunista
        {
            UnitManager occupant = HexOccupancyQuery.FindUnitAtCell(targetCell);
            if (occupant != null && occupant.TeamId != snapshot.AITeam)
            {
                MatchController mc = GetMatchController();
                if (mc == null || !mc.IsUnitVisibleForTeam(occupant, snapshot.AITeam))
                {
                    if (TryFindBestLoSCell(unit, paths, occupied, targetCell, out Vector3Int dpqCell))
                    {
                        assigned.Status = ObjectiveStatus.Pursuing;

                        SensorMovementMode dpqMode = dpqCell != fromCell
                            ? SensorMovementMode.MoveuAndando
                            : SensorMovementMode.MoveuParado;
                        var dpqTargets = new List<PodeMirarTargetOption>();
                        if (PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                                dpqMode, dpqTargets, fromCell: dpqCell) && dpqTargets.Count > 0)
                        {
                            UnitManager lateralTarget = null; float lateralPri = float.MinValue;
                            foreach (PodeMirarTargetOption opt in dpqTargets)
                            {
                                if (opt?.targetUnit == null) continue;
                                Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                                if (SectorManager.HexDistance(tc, targetCell) > DefenseEnemyRange + 1) continue;
                                float p = AttackTargetPriorityPursuer(tc, targetCell);
                                if (p > lateralPri) { lateralPri = p; lateralTarget = opt.targetUnit; }
                            }
                            if (lateralTarget != null)
                            {
                                Vector3Int ltCell = lateralTarget.CurrentCellPosition; ltCell.z = 0;
                                Debug.Log($"{TL("Explorador")} {unit.InstanceId} DPQ {assigned.Sector} via {dpqCell} + ataque lateral → {lateralTarget.UnitDisplayName}#{lateralTarget.InstanceId}");
                                action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, dpqCell,
                                    lateralTarget.InstanceId.ToString(), ltCell, paths);
                                return true;
                            }
                        }

                        Debug.Log($"{TL("Explorador")} {unit.InstanceId} DPQ para revelar {assigned.Sector} via {dpqCell} (ev={GetTerrainEv(dpqCell):F0})");
                        action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, dpqCell, paths);
                        return true;
                    }
                }
            }
        }
        return false;
    }
}
