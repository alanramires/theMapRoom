using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private sealed class RestockSource
    {
        public UnitManager Unit;
        public ConstructionManager Construction;
        public Vector3Int SourceCell;
        public Vector3Int RendezvousCell;

        public string DebugLabel =>
            Unit != null
                ? $"{Unit.UnitDisplayName}#{Unit.InstanceId}@{SourceCell}"
                : Construction != null
                    ? $"{Construction.ConstructionDisplayName}#{Construction.InstanceId}@{SourceCell}"
                    : $"none@{SourceCell}";

        public bool Matches(PodeTransferirOption option)
        {
            return option != null
                && option.flowMode == TransferFlowMode.Recebedor
                && ((Unit != null && option.targetUnit == Unit)
                    || (Construction != null && option.targetConstruction == Construction));
        }
    }

    private bool TryFindLogisticsReloadCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = "";
        if (paths == null || paths.Count == 0 || snapshot == null)
            return false;

        RestockSource target = FindBestRestockSource(unit, snapshot, fromCell);
        if (target == null)
        {
            reason = "sem RestockSource validado pelo PodeTransferir";
            return false;
        }

        Vector3Int targetCell = target.RendezvousCell;
        targetCell.z = 0;
        float fromDist = SectorManager.HexDistance(fromCell, targetCell);

        if (TryFindBestToolProgressionCell(
                unit,
                snapshot,
                fromCell,
                targetCell,
                paths,
                occupied,
                ToolProgressionIntent.LogisticsReload,
                out Vector3Int toolReloadCell,
                out ToolProgressionCandidate toolReloadCandidate,
                out string toolReloadReason,
                tacticalScore: (cell, candidate) =>
                {
                    float dist = SectorManager.HexDistance(cell, targetCell);
                    float progress = fromDist - dist;
                    float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                    float dpq = GetTerrainDpqPontos(cell);
                    float score = progress * 650f
                        - dist * 80f
                        + dpq * 40f
                        - threat * 120f
                        - candidate.MoveCost * 8f;

                    if (CanLogisticsReceiveTransferAtCell(unit, snapshot, fromCell, cell))
                        score += 6500f;
                    if (cell == targetCell)
                        score += 4000f;
                    return score;
                }))
        {
            bool hasReloadProgress = toolReloadCandidate.ToolScore > 0
                || toolReloadCandidate.FirstTurnProgress > 0f
                || toolReloadCandidate.TwoTurnProgress > 0f;
            if (hasReloadProgress || toolReloadCell == targetCell)
            {
                bestCell = toolReloadCell;
                reason = $"source={target.DebugLabel} rendezvous={targetCell} {toolReloadReason}";
                return true;
            }
        }

        float bestScore = float.MinValue;
        Vector3Int bestFallbackMove = fromCell;
        float bestFallbackScore = float.MinValue;
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;

            float dist = SectorManager.HexDistance(cell, targetCell);
            float progress = fromDist - dist;
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float dpq = GetTerrainDpqPontos(cell);
            float score = progress * 650f
                - dist * 80f
                + dpq * 40f
                - threat * 120f
                - GetPathStepCount(paths, cell) * 8f;

            if (CanLogisticsReceiveTransferAtCell(unit, snapshot, fromCell, cell))
                score += 6500f;
            if (cell == targetCell)
                score += 4000f;

            if (cell != fromCell && score > bestFallbackScore)
            {
                bestFallbackScore = score;
                bestFallbackMove = cell;
            }
            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        if (bestCell == fromCell && fromDist > 0f)
        {
            if (bestFallbackMove != fromCell)
            {
                bestCell = bestFallbackMove;
                reason = $"source={target.DebugLabel} rendezvous={targetCell} fallbackMove dist={SectorManager.HexDistance(bestCell, targetCell):F1} score={bestFallbackScore:F0}";
                return true;
            }
            return false;
        }

        reason = $"source={target.DebugLabel} rendezvous={targetCell} dist={SectorManager.HexDistance(bestCell, targetCell):F1} score={bestScore:F0}";
        return true;
    }

    private RestockSource FindBestRestockSource(
        UnitManager receiver,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell)
    {
        RestockSource best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < ConstructionManager.AllActive.Count; i++)
        {
            ConstructionManager building = ConstructionManager.AllActive[i];
            if (building == null || (int)building.TeamId != (int)receiver.TeamId)
                continue;
            if (building.CurrentCapturePoints < building.CapturePointsMax)
                continue;
            if (!building.TryResolveConstructionData(out ConstructionData buildingData)
                || buildingData == null
                || !buildingData.isSupplier
                || buildingData.supplierTier != SupplierTier.Hub)
                continue;

            var candidate = new RestockSource
            {
                Construction = building,
                SourceCell = NormalizeRestockCell(building.CurrentCellPosition)
            };
            ScoreRestockSourceCandidate(
                receiver, snapshot, fromCell, candidate, ref best, ref bestScore);
        }

        foreach (UnitManager hub in UnitManager.AllActive)
        {
            if (hub == null || hub == receiver || hub.IsDead || hub.IsEmbarked)
                continue;
            if ((int)hub.TeamId != (int)receiver.TeamId)
                continue;
            if (!hub.TryGetUnitData(out UnitData hubData)
                || hubData == null
                || !hubData.isSupplier
                || hubData.supplierTier != SupplierTier.Hub
                || !HasRuntimeTransferService(hub))
                continue;

            var candidate = new RestockSource
            {
                Unit = hub,
                SourceCell = NormalizeRestockCell(hub.CurrentCellPosition)
            };
            ScoreRestockSourceCandidate(
                receiver, snapshot, fromCell, candidate, ref best, ref bestScore);
        }

        return best;
    }

    private static bool HasRuntimeTransferService(UnitManager supplier)
    {
        if (supplier == null)
            return false;
        IReadOnlyList<ServiceData> services = supplier.GetEmbarkedServices();
        if (services == null)
            return false;
        for (int i = 0; i < services.Count; i++)
        {
            ServiceData service = services[i];
            if (service != null && service.serviceType == ServiceType.Transfer)
                return true;
        }
        return false;
    }

    private void ScoreRestockSourceCandidate(
        UnitManager receiver,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        RestockSource candidate,
        ref RestockSource best,
        ref float bestScore)
    {
        if (candidate == null)
            return;

        var meetingCells = new List<Vector3Int>(7) { candidate.SourceCell };
        var neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(
            boardTilemap, candidate.SourceCell, neighbors);
        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int neighbor = NormalizeRestockCell(neighbors[i]);
            if (!meetingCells.Contains(neighbor))
                meetingCells.Add(neighbor);
        }

        for (int i = 0; i < meetingCells.Count; i++)
        {
            Vector3Int rendezvous = meetingCells[i];
            if (!TryValidateRestockSourceWithSensor(
                    receiver, rendezvous, candidate, out _))
                continue;

            float threat = CalculateThreatLevel(rendezvous, snapshot.AITeam);
            if (threat > 0f)
                continue;

            float distance = CalculateRouteDistanceOrHex(
                receiver, fromCell, rendezvous);
            bool home = candidate.Construction != null
                && IsLogisticsHomeConstruction(
                    candidate.Construction, snapshot.AITeam);
            float score =
                -distance * 100f
                - threat * 1000f
                + (candidate.Unit != null ? 175f : 0f)
                + (home ? 150f : 0f);

            if (score <= bestScore)
                continue;

            candidate.RendezvousCell = rendezvous;
            bestScore = score;
            best = candidate;
        }
    }

    private bool TryValidateRestockSourceWithSensor(
        UnitManager receiver,
        Vector3Int prospectiveCell,
        RestockSource expectedSource,
        out PodeTransferirOption matchingOption)
    {
        matchingOption = null;
        if (receiver == null || expectedSource == null)
            return false;

        Vector3Int originalCell = NormalizeRestockCell(
            receiver.CurrentCellPosition);
        prospectiveCell = NormalizeRestockCell(prospectiveCell);
        var options = new List<PodeTransferirOption>();
        if (!PodeTransferirSensor.CollectOptionsFromCell(
                receiver,
                boardTilemap,
                terrainDatabase,
                prospectiveCell == originalCell
                    ? SensorMovementMode.MoveuParado
                    : SensorMovementMode.MoveuAndando,
                prospectiveCell,
                options,
                out _))
            return false;

        for (int i = 0; i < options.Count; i++)
        {
            PodeTransferirOption option = options[i];
            if (!expectedSource.Matches(option))
                continue;
            matchingOption = option;
            return true;
        }

        return false;
    }

    private static Vector3Int NormalizeRestockCell(Vector3Int cell)
    {
        cell.z = 0;
        return cell;
    }

    private static bool IsLogisticsHomeConstruction(
        ConstructionManager construction,
        TeamId aiTeam)
    {
        return construction != null
            && construction.SlotIndex == ResolveAISlotKey(aiTeam)
            && (construction.IsPlayerHeadQuarter
                || ConstructionSectorHelper.IsBase(construction.Sector));
    }
}
