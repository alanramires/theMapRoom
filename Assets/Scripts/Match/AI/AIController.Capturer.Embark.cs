using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Intercepção de embarque — capturador embarca em transporte adjacente
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideCapturerEmbarkAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!unit.TryGetUnitData(out UnitData data) || data?.roles == null || data.roles.Count == 0
            || data.roles[0] != UnitRole.Capturador) return null;

        var options = new List<PodeEmbarcarOption>();
        if (!PodeEmbarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase,
                Mathf.Max(0, unit.RemainingMovementPoints), options) || options.Count == 0)
            return null;

        // Prefer a transporter formally assigned to the same sector as this capturer
        SectorObjective assigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;
        PodeEmbarcarOption best = null;

        if (assigned != null && plan != null)
        {
            foreach (PodeEmbarcarOption opt in options)
            {
                SectorObjective transporterObj = ResolveAssignedTransportObjective(opt.transporterUnit, plan);
                if (transporterObj != null && transporterObj.Sector == assigned.Sector)
                {
                    best = opt;
                    break;
                }
            }
        }

        if (best == null) best = options[0];

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);

        Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca → {best.transporterUnit.InstanceId} slot {best.transporterSlotIndex}");
        return BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, best.transporterUnit, best.transporterSlotIndex, paths);
    }
}
