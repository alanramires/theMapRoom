using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Tow embark — towable assault units (e.g. field artillery) board a nearby
    // compatible transporter when they are far from a useful target.
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideAssaultEmbarkAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        int minDeliveryDistance = TowDeliveryThreshold)
    {
        // Pass 1: find adjacent compatible transporters via sensor.
        // PodeEmbarcarSensor already checks slot rules (reboque skill, class, domain/height).
        var options = new List<PodeEmbarcarOption>();
        PodeEmbarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase,
            Mathf.Max(0, unit.RemainingMovementPoints), options);
        if (options.Count == 0) return null;

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;

        // Only embark when there is a useful destination far enough to justify the ride.
        if (!TryFindTowDeliveryTarget(unit, fromCell, snapshot, plan, out Vector3Int deliveryTarget)) return null;

        float distToTarget = SectorManager.HexDistance(fromCell, deliveryTarget);
        if (distToTarget < minDeliveryDistance) return null;

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);

        foreach (PodeEmbarcarOption opt in options)
        {
            if (opt?.transporterUnit == null) continue;

            // Logistics trucks have their own mission — only board if the truck is already
            // ahead of the artillery (closer to the delivery target), meaning it is
            // advancing toward the front, not retreating toward base.
            if (IsPrimaryLogisticsUnit(opt.transporterUnit))
            {
                Vector3Int truckCell = opt.transporterUnit.CurrentCellPosition; truckCell.z = 0;
                if (SectorManager.HexDistance(truckCell, deliveryTarget) >= distToTarget) continue;
            }

            // transporterUnit.HasActed is intentionally NOT checked — the truck may have
            // already parked adjacent this turn; the artillery boards on the same tick.
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} embarca (reboque) → {opt.transporterUnit.InstanceId} slot {opt.transporterSlotIndex} destino {deliveryTarget}");
            return BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, opt.transporterUnit, opt.transporterSlotIndex, paths);
        }

        return null;
    }
}
