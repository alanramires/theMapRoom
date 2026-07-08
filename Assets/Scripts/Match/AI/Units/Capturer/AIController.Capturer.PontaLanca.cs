using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Capturador Ponta de Lanca - chegada e captura direta
    // -------------------------------------------------------------------------

    private bool TryDecideCapturerSpearheadArrival(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned, Vector3Int fromCell, Vector3Int targetCell, Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied, out PlayerAction action)
    {
        using var perf = new AIDecisionPerfScope(unit, "spearhead");
        action = null;
        // Já está no hex alvo
        if (fromCell == targetCell)
        {
            if (SimulateCaptureSensor(unit, targetCell, out _))
            {
                assigned.Status = ObjectiveStatus.Capturing;
                Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} captura {assigned.Sector} @ {targetCell}");
                action = BuildCaptureBatch(unit, snapshot.AITeam, fromCell, targetCell);
                return true;
            }
            assigned.Status = ObjectiveStatus.Complete;
            action = null;
            return true;
        }

        // Alvo alcançável neste turno
        if (paths.ContainsKey(targetCell) && !occupied.Contains(targetCell))
        {
            if (SimulateCaptureSensor(unit, targetCell, out _))
            {
                assigned.Status = ObjectiveStatus.Capturing;
                Debug.Log($"{TL("PontaLanca")} {unit.InstanceId} alcança e captura {assigned.Sector} @ {targetCell}");
                action = BuildCaptureBatch(unit, snapshot.AITeam, fromCell, targetCell, paths);
                return true;
            }
            action = BuildMoveBatch(unit, snapshot.AITeam, fromCell, targetCell, paths);
            return true;
        }
        return false;
    }
}
