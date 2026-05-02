using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Capturador Oportunista - captura transversal no caminho
    // -------------------------------------------------------------------------

    private PlayerAction DecideCapturerOpportunistAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int fromCell,
        Vector3Int captureCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        Debug.Log($"{TL("Oportunista")} {unit.InstanceId} captura oportunista @ {captureCell} -> {assigned.Sector}");
        return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, captureCell, paths);
    }
}
