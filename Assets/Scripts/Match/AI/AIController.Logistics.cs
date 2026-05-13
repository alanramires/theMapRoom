using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private PlayerAction TryDecideLogisticsAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!IsPrimaryLogisticsUnit(unit))
            return null;

        // Tow delivery takes priority — drop off passengers before doing logistics work.
        if (HasTransportCargo(unit))
            return DecideTransportadorCourierAction(unit, snapshot);

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
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        Debug.Log($"{TL("Logistics")} {unit.InstanceId} conserva posicao {fromCell} " +
                  $"dpq={GetTerrainDpqPontos(fromCell):F1} threat={CalculateThreatLevel(fromCell, snapshot.AITeam):F1} baseDef={baseDefense}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }
}
