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
        string anchorReason = "home";
        if (!baseDefense
            && TryResolveRallyInfluence(plan, snapshot.AITeam, fromCell, includeGoGreen: false, out AIRallyInfluence rally)
            && rally.Active
            && IsRallyAssemblingState(rally.State))
        {
            anchor = rally.Anchor;
            anchorReason = $"rally {rally.Sector} {rally.State}";
        }
        bool needsReload = ShouldRestockLogisticsUnit(unit, out string restockReason);
        Debug.Log($"{TL("Logistics")} {unit.InstanceId} restockCheck {(needsReload ? "SIM" : "nao")} {restockReason}");

        if (needsReload)
        {
            if (TryBuildLogisticsTransferReceiveAction(unit, snapshot, fromCell, paths, out PlayerAction transferAction, out string transferReason))
            {
                Debug.Log($"{TL("Logistics")} {unit.InstanceId} recarrega por transferencia {restockReason} {transferReason}");
                return transferAction;
            }

            Debug.Log($"{TL("Logistics")} {unit.InstanceId} restock sem transferencia imediata {restockReason} motivo={transferReason}");

            if (TryFindLogisticsReloadCell(unit, snapshot, fromCell, paths, occupied, out Vector3Int reloadCell, out string reloadReason))
            {
                if (TryBuildLogisticsTransferReceiveActionAtCell(unit, snapshot, fromCell, reloadCell, paths, out PlayerAction moveTransferAction, out string moveTransferReason))
                {
                    Debug.Log($"{TL("Logistics")} {unit.InstanceId} move + recarrega por transferencia {restockReason} {moveTransferReason} {reloadReason}");
                    return moveTransferAction;
                }

                Debug.Log($"{TL("Logistics")} {unit.InstanceId} volta para recarga via {reloadCell} {restockReason} {reloadReason}");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, reloadCell, paths);
            }

            Debug.Log($"{TL("Logistics")} {unit.InstanceId} restock bloqueado: sem transferencia/rota segura {restockReason} reload={reloadReason}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        if (TryBuildLogisticsSupplyAction(unit, snapshot, fromCell, paths, occupied, baseDefense, out PlayerAction supplyAction, out string supplyReason))
        {
            Debug.Log($"{TL("Logistics")} {unit.InstanceId} atende logistica {supplyReason}");
            return supplyAction;
        }

        // O envelope movimento + alcance de servico ja foi resolvido acima por
        // TryBuildLogisticsSupplyAction. Fora dele, somente uma unidade realmente
        // em manutencao pode orientar deslocamento futuro; preventivos distantes
        // nao sao trabalho desta rodada.
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
            Debug.Log($"{TL("Logistics")} {unit.InstanceId} move retaguarda via {moveCell} anchor={anchorReason} {reason}");

            // If repositioning already lands in a valid service cell, do the service in the same action.
            if (moveCell != fromCell)
            {
                bool allowPreventive = IsPreventiveLogisticsAllowed(unit, snapshot, fromCell, paths, occupied);
                int limit = GetLogisticsServiceLimit(unit);
                List<UnitManager> moveSupply = CollectLogisticsTargetsBySupplySensorAtCell(
                    unit,
                    snapshot,
                    moveCell,
                    limit,
                    allowPreventive,
                    out int validCount,
                    out int invalidCount,
                    out string sensorDebug);
                if (moveSupply.Count > 0 && IsLogisticsServiceCellAllowed(unit, snapshot, moveCell))
                {
                    Debug.Log($"{TL("Logistics")} {unit.InstanceId} move retaguarda + supre {moveSupply.Count} unidade(s) via {moveCell}");
                    return BuildSupplyBatch(unit, snapshot.AITeam, fromCell, moveCell, moveSupply, paths);
                }
                if (invalidCount > 0)
                    Debug.Log($"{TL("Logistics")} {unit.InstanceId} move retaguarda nao supre via {moveCell}: PodeSuprir valid={validCount} invalid={invalidCount} {sensorDebug}");
            }

            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        Debug.Log($"{TL("Logistics")} {unit.InstanceId} conserva posicao {fromCell} " +
                  $"dpq={GetTerrainDpqPontos(fromCell):F1} threat={CalculateThreatLevel(fromCell, snapshot.AITeam):F1} baseDef={baseDefense}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }
}
