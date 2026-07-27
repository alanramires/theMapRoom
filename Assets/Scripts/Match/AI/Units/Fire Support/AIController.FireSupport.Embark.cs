using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int FireSupportEmbarkSectorThreshold = 6;

    private PlayerAction TryDecideFireSupportEmbarkAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        SectorObjective assigned = null)
    {
        TryDecideFireSupportTransportAction(
            unit, snapshot, plan, assigned,
            out PlayerAction action);
        return action;
    }

    private FireSupportTransportOutcome
        TryDecideFireSupportTransportAction(
            UnitManager unit,
            AIWorldSnapshot snapshot,
            TeamObjectivePlan plan,
            SectorObjective assigned,
            out PlayerAction action)
    {
        action = null;
        if (unit == null || snapshot == null)
            return FireSupportTransportOutcome.NoAction;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        if (assigned != null)
        {
            if (!TryGetAnySectorInfo(assigned.Sector, out SectorManager.SectorInfo info))
                return FireSupportTransportOutcome.NoAction;

            if (IsRallyAssemblyObjective(assigned)
                && !IsRallyAssemblyEstablishedForFireSupport(info, snapshot.AITeam))
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} ignora embarque rally: {assigned.Sector} ainda sem cabeca de ponte");
                return FireSupportTransportOutcome.TransportRejected;
            }

            float sectorDistance = info.GetDistanceToHQ(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
            if (sectorDistance < FireSupportEmbarkSectorThreshold)
            {
                Debug.Log($"{TL("FireSupport")} {unit.InstanceId} ignora embarque: {assigned.Sector} dist={sectorDistance:F0}h < {FireSupportEmbarkSectorThreshold}h");
                return FireSupportTransportOutcome.NoAction;
            }
        }
        else if (TryFindTowDeliveryTarget(unit, fromCell, snapshot, plan, out Vector3Int deliveryTarget)
            && SectorManager.HexDistance(fromCell, deliveryTarget) < FireSupportEmbarkSectorThreshold)
        {
            Debug.Log($"{TL("FireSupport")} {unit.InstanceId} ignora embarque rogue: destino < {FireSupportEmbarkSectorThreshold}h");
            return FireSupportTransportOutcome.NoAction;
        }

        action = TryDecideCombatPassengerTransportAction(
            unit, snapshot, plan,
            CombatPassengerTransportPolicy.FireSupport,
            assigned);
        return action != null
            ? FireSupportTransportOutcome.Handled
            : FireSupportTransportOutcome.TransportRejected;
    }

    private static bool IsRallyAssemblyEstablishedForFireSupport(SectorManager.SectorInfo info, TeamId aiTeam)
    {
        if (info == null)
            return false;

        if (info.ControllingTeam == aiTeam)
            return true;

        if (info.IsFullyControlled && info.ControllingTeam == aiTeam)
            return true;

        int owned = 0;
        int total = 0;
        IReadOnlyList<SectorManager.SectorConstructionInfo> constructions = info.Constructions;
        if (constructions != null)
        {
            for (int i = 0; i < constructions.Count; i++)
            {
                SectorManager.SectorConstructionInfo construction = constructions[i];
                if (construction == null)
                    continue;

                total++;
                if (construction.OwnerTeam == aiTeam)
                    owned++;
            }
        }

        return total > 0 && owned * 2 > total;
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
        bool hotDestination =
            CalculateThreatLevel(deliveryTarget, snapshot.AITeam) > 0.1f
            || HasNearbyVisibleEnemy(deliveryTarget, snapshot.AITeam, 2);
        if (!hotDestination)
            return true;

        Vector3Int transporterCell = transporter.CurrentCellPosition;
        transporterCell.z = 0;
        Vector3Int mainLineAnchor = snapshot.EnemyHQ != null
            ? snapshot.EnemyHQ.CurrentCellPosition
            : deliveryTarget;
        mainLineAnchor.z = 0;

        int currentSupport = CountTowCourierFrontlineSupport(
            transporter, fireSupport, snapshot, transporterCell,
            deliveryTarget, out float currentNearestSupport);
        if (currentSupport > 0
            && !IsLogisticsForwardOfMainLine(
                transporter, snapshot, transporterCell, mainLineAnchor)
            && IsFireSupportConservativeCellAllowed(
                fireSupport, snapshot, transporterCell))
        {
            reason =
                $"destino hot, transportador atual tem retaguarda " +
                $"allies3={currentSupport} " +
                $"near={currentNearestSupport:F1}";
            return true;
        }

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, transporter,
                Mathf.Max(0, transporter.RemainingMovementPoints),
                terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(transporter);

        if (TryFindConservativeTowCourierRendezvousCell(
                transporter, fireSupport, snapshot, plan,
                transporterCell, mainLineAnchor, paths, occupied,
                out Vector3Int rendezvousCell,
                out string rendezvousDetails))
        {
            reason =
                $"destino hot, rendezvous seguro {rendezvousCell} " +
                rendezvousDetails;
            return true;
        }

        if (HasSafeFireSupportDropZoneOnRoute(
                transporter, fireSupport, snapshot, transporterCell,
                deliveryTarget, mainLineAnchor))
        {
            reason =
                "destino hot, rota tem zona conservadora para desembarque";
            return true;
        }

        reason =
            $"destino hot {deliveryTarget} sem drop/rendezvous de retaguarda";
        return false;
    }

    private bool HasSafeFireSupportDropZoneOnRoute(
        UnitManager transporter,
        UnitManager fireSupport,
        AIWorldSnapshot snapshot,
        Vector3Int transporterCell,
        Vector3Int deliveryTarget,
        Vector3Int mainLineAnchor)
    {
        transporterCell.z = 0;
        deliveryTarget.z = 0;
        mainLineAnchor.z = 0;
        if (SectorManager.HexDistance(
                transporterCell, deliveryTarget) < 3f)
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> extendedPaths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, transporter,
                transporter.MaxMovementPoints * 3,
                terrainDatabase);
        if (extendedPaths == null || extendedPaths.Count == 0)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(transporter);
        return TryFindBestToolProgressionCell(
            transporter, snapshot, transporterCell, deliveryTarget,
            extendedPaths, occupied,
            ToolProgressionIntent.TransportDelivery,
            out _, out _, out _,
            allowCell: cell =>
            {
                if (SectorManager.HexDistance(
                        cell, deliveryTarget) < 2f)
                    return false;
                if (IsLogisticsForwardOfMainLine(
                        transporter, snapshot, cell, mainLineAnchor))
                    return false;
                if (!IsFireSupportConservativeCellAllowed(
                        fireSupport, snapshot, cell))
                    return false;
                return CountTowCourierFrontlineSupport(
                    transporter, fireSupport, snapshot, cell,
                    deliveryTarget, out _) > 0;
            });
    }
}
