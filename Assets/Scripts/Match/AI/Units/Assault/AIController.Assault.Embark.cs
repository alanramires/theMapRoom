using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Tow embark â€” towable assault units (e.g. field artillery) board a nearby
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

            // Logistics trucks have their own mission â€” only board if the truck is already
            // ahead of the artillery (closer to the delivery target), meaning it is
            // advancing toward the front, not retreating toward base.
            if (IsPrimaryLogisticsUnit(opt.transporterUnit))
            {
                Vector3Int truckCell = opt.transporterUnit.CurrentCellPosition; truckCell.z = 0;
                if (SectorManager.HexDistance(truckCell, deliveryTarget) > distToTarget) continue;
            }

            if (IsFireSupportUnit(unit)
                && !CanFireSupportTowEmbarkSafely(unit, opt.transporterUnit, snapshot, plan, fromCell, deliveryTarget, out string towSafetyReason))
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} ignora embarque TOW -> {opt.transporterUnit.InstanceId}: {towSafetyReason}");
                continue;
            }

            // transporterUnit.HasActed is intentionally NOT checked â€” the truck may have
            // already parked adjacent this turn; the artillery boards on the same tick.
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} embarca (reboque) â†’ {opt.transporterUnit.InstanceId} slot {opt.transporterSlotIndex} destino {deliveryTarget}");
            return BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, opt.transporterUnit, opt.transporterSlotIndex, paths);
        }

        return null;
    }

    private bool CanFireSupportTowEmbarkSafely(
        UnitManager fireSupport,
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int fireSupportCell,
        Vector3Int deliveryTarget,
        out string reason)
    {
        reason = "";
        if (fireSupport == null || transporter == null || snapshot == null)
        {
            reason = "dados insuficientes";
            return false;
        }

        fireSupportCell.z = 0;
        deliveryTarget.z = 0;
        bool hotDestination = CalculateThreatLevel(deliveryTarget, snapshot.AITeam) > 0.1f
            || HasNearbyVisibleEnemy(deliveryTarget, snapshot.AITeam, 2);
        if (!hotDestination)
            return true;

        Vector3Int truckCell = transporter.CurrentCellPosition;
        truckCell.z = 0;
        Vector3Int mainLineAnchor = snapshot.EnemyHQ != null ? snapshot.EnemyHQ.CurrentCellPosition : deliveryTarget;
        mainLineAnchor.z = 0;

        int currentSupport = CountTowCourierFrontlineSupport(
            transporter,
            fireSupport,
            snapshot,
            truckCell,
            deliveryTarget,
            out float currentNearestSupport);
        if (currentSupport > 0
            && !IsLogisticsForwardOfMainLine(transporter, snapshot, truckCell, mainLineAnchor)
            && IsFireSupportConservativeCellAllowed(fireSupport, snapshot, truckCell))
        {
            reason = $"destino hot, truck atual tem retaguarda allies3={currentSupport} near={currentNearestSupport:F1}";
            return true;
        }

        Dictionary<Vector3Int, List<Vector3Int>> truckPaths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap,
                transporter,
                Mathf.Max(0, transporter.RemainingMovementPoints),
                terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(transporter);

        if (TryFindConservativeTowCourierRendezvousCell(
                transporter,
                fireSupport,
                snapshot,
                plan,
                truckCell,
                mainLineAnchor,
                truckPaths,
                occupied,
                out Vector3Int rendezvousCell,
                out string rendezvousDetails))
        {
            reason = $"destino hot, rendezvous seguro {rendezvousCell} {rendezvousDetails}";
            return true;
        }

        reason = $"destino hot {deliveryTarget} sem drop/rendezvous de retaguarda";
        return false;
    }
}
