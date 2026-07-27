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
            // Ferido a bordo ja passou pelo modo hospital no roteador; chegar aqui com
            // paciente significa que nao havia servico nem recarga. Ver
            // Transportador.Hospital.
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
            if (TryBuildLogisticsStockRestockAction(
                    unit,
                    snapshot,
                    fromCell,
                    paths,
                    occupied,
                    out PlayerAction stockAction,
                    out string stockReason))
            {
                Debug.Log(
                    $"{TL("Logistics")} {unit.InstanceId} " +
                    $"recarrega pela rede de estoque " +
                    $"{restockReason} {stockReason}");
                return stockAction;
            }

            Debug.Log(
                $"{TL("Logistics")} {unit.InstanceId} " +
                $"restock bloqueado: {restockReason} " +
                $"MelhorEstoque={stockReason}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        if (TryBuildLogisticsSupplyAction(unit, snapshot, fromCell, paths, occupied, baseDefense, out PlayerAction supplyAction, out string supplyReason))
        {
            Debug.Log($"{TL("Logistics")} {unit.InstanceId} atende logistica {supplyReason}");
            return supplyAction;
        }

        // Logistica continua priorizando atendimento de campo. Quando nao ha
        // paciente/cliente valido, um Hub com Transfer pode circular carga pela
        // mesma rede usada pelo papel Estoque.
        if (TryBuildStockNetworkAction(
                unit,
                snapshot,
                fromCell,
                paths,
                occupied,
                out PlayerAction distributionAction,
                out string distributionReason))
        {
            Debug.Log(
                $"{TL("Logistics")} {unit.InstanceId} " +
                $"branch de estoque — {distributionReason}");
            return distributionAction;
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
            string serviceTargetDebug;
            if (serviceTarget != null)
            {
                Vector3Int targetCell = serviceTarget.CurrentCellPosition;
                targetCell.z = 0;
                serviceTargetDebug =
                    $"serviceTarget={serviceTarget.UnitDisplayName}#{serviceTarget.InstanceId}@{targetCell}";
            }
            else
            {
                serviceTargetDebug = "serviceTarget=none";
            }

            Debug.Log($"{TL("Logistics")} {unit.InstanceId} move retaguarda via {moveCell} " +
                      $"anchor={anchorReason} {serviceTargetDebug} {reason}");

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
