using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private PlayerAction TryDecideLogisticsAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!IsPrimaryLogisticsUnit(unit))
            return null;

        // Tow delivery takes priority — drop off passengers before doing logistics work.
        // Resolve the delivery target for tow cargo: the passenger may have no formal plan slot
        // (picked up by tow shuttle), so re-derive the target via FindTowDeliveryTarget.
        if (HasTransportCargo(unit))
        {
            // Pass assignedSectorTarget=zero — TryResolveCourierPassengerTarget re-derives the
            // target from the passenger's plan slot, handling cell (0,0,0) correctly.
            return DecideTransportadorCourierAction(unit, snapshot);
        }

        PlayerAction repairAction = TryDecideRepairAction(unit, snapshot, plan);
        if (repairAction != null)
            return repairAction;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildLogisticsPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        bool baseDefense = IsLogisticsBaseDefenseEmergency(snapshot);
        Vector3Int anchor = ResolveLogisticsAnchor(snapshot, fromCell);
        bool needsReload = LogisticsHasEmptyCargoProduct(unit);

        if (needsReload)
        {
            if (TryBuildLogisticsTransferReceiveAction(unit, snapshot, fromCell, paths, out PlayerAction transferAction, out string transferReason))
            {
                Debug.Log($"{TL("Logistics")} {unit.InstanceId} recarrega por transferencia {transferReason}");
                return transferAction;
            }

            if (TryFindLogisticsReloadCell(unit, snapshot, fromCell, paths, occupied, out Vector3Int reloadCell, out string reloadReason))
            {
                Debug.Log($"{TL("Logistics")} {unit.InstanceId} volta para recarga via {reloadCell} {reloadReason}");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, reloadCell, paths);
            }
        }

        if (TryBuildLogisticsSupplyAction(unit, snapshot, fromCell, paths, occupied, baseDefense, out PlayerAction supplyAction, out string supplyReason))
        {
            Debug.Log($"{TL("Logistics")} {unit.InstanceId} atende logistica {supplyReason}");
            return supplyAction;
        }

        UnitManager serviceTarget = FindLogisticsServiceTarget(unit, snapshot, fromCell, paths, occupied, baseDefense);

        if (paths == null || paths.Count == 0)
        {
            Debug.Log($"{TL("Logistics")} {unit.InstanceId} sem caminhos - segura {fromCell}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);
        }

        if (TryFindLogisticsRepositionCell(
                unit,
                snapshot,
                fromCell,
                anchor,
                serviceTarget,
                baseDefense,
                paths,
                occupied,
                out Vector3Int moveCell,
                out string reason))
        {
            Debug.Log($"{TL("Logistics")} {unit.InstanceId} move retaguarda via {moveCell} {reason}");

            // When forced to vacate a producer, opportunistically supply from the destination.
            if (reason.StartsWith("desocupa_produtora") && moveCell != fromCell)
            {
                bool allowPreventive = IsPreventiveLogisticsAllowed(unit, snapshot, fromCell, paths, occupied);
                int limit = GetLogisticsServiceLimit(unit);
                List<UnitManager> vacateSupply = CollectLogisticsTargetsInServiceRange(unit, snapshot, moveCell, limit, allowPreventive);
                if (vacateSupply.Count > 0 && IsLogisticsServiceCellAllowed(unit, snapshot, moveCell))
                {
                    Debug.Log($"{TL("Logistics")} {unit.InstanceId} desocupa_produtora + supre {vacateSupply.Count} unidade(s) via {moveCell}");
                    return BuildSupplyBatch(unit, snapshot.AITeam, fromCell, moveCell, vacateSupply, paths);
                }
            }

            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        Debug.Log($"{TL("Logistics")} {unit.InstanceId} conserva posicao {fromCell} " +
                  $"dpq={GetTerrainDpqPontos(fromCell):F1} threat={CalculateThreatLevel(fromCell, snapshot.AITeam):F1} baseDef={baseDefense}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }
}
