using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private bool IsTransportInvasionDelivery(
        UnitManager passenger,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        Vector3Int target)
    {
        if (passenger == null || snapshot == null)
            return false;

        SectorObjective objective = ResolveAnyAssignedObjective(passenger, plan);
        if (objective != null)
        {
            if (objective.ObjectiveType == AIObjectiveType.InvasionAttack)
                return true;
            if (ConstructionSectorHelper.IsBase(objective.Sector)
                && !IsCriticalHomeDefenseObjective(objective, snapshot.AITeam))
                return true;
        }

        target.z = 0;
        ConstructionManager building = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, target);
        if (building != null
            && building.SlotIndex != snapshot.AISlotIndex
            && (building.IsPlayerHeadQuarter || ConstructionSectorHelper.IsBase(building.Sector)))
            return true;

        if (snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition;
            hq.z = 0;
            if (SectorManager.HexDistance(target, hq) <= 4f)
                return true;
        }

        return false;
    }


    private bool IsTransportInvasionCourierCellAllowed(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int target)
    {
        if (cell == transporter.CurrentCellPosition)
            return true;

        // GO GREEN é uma ordem operacional global: todos avançam para o plano final ">>".
        // Não exigir screen/bridgehead local aqui, pois isso cria um deadlock em que o APC não
        // avança sem screen e o passageiro que formaria a screen permanece preso no APC.
        // Quando a invasão falha, IsInvading volta a false e os gates conservadores retornam.
        if (snapshot != null && snapshot.IsInvading)
            return true;

        // Facção sem QG (rebeldes) não monta massa: sem produção, ela nunca junta assalto+artilharia
        // perto do APC, então a cadeia GoGreen/ponte/screen seria um bloqueio permanente — o mesmo
        // deadlock descrito acima, só que sem saída. Rebelde infiltra, não faz invasão de conjunto.
        if (snapshot != null && ConstructionManager.IsHeadQuarterlessTeam(snapshot.AITeam))
            return true;

        if (!HasTransportInvasionGoGreen(transporter, snapshot, target, out _))
            return false;

        if (HasTransportInvasionBridgeheadGoGreen(transporter, snapshot, out _))
            return true;

        return TryGetTransportScreenMetrics(transporter, snapshot, cell, target,
            out float gap, out _, out _)
            && gap >= 0.75f;
    }


    private bool IsTransportInvasionDropAllowed(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        Vector3Int transporterCell,
        Vector3Int dropCell,
        Vector3Int target)
    {
        // Durante GO GREEN o desembarque também integra a invasão total; não condicionar a
        // liberação da carga a uma screen que pode estar justamente dentro do transporte.
        if (snapshot != null && snapshot.IsInvading)
            return true;

        // Ver IsTransportInvasionCourierCellAllowed: de nada adianta o APC rebelde avançar se a
        // carga fica presa dentro dele por falta de screen.
        if (snapshot != null && ConstructionManager.IsHeadQuarterlessTeam(snapshot.AITeam))
            return true;

        if (!IsTransportInvasionCourierCellAllowed(transporter, snapshot, transporterCell, target))
            return false;

        if (!HasTransportInvasionGoGreen(transporter, snapshot, target, out _))
            return false;

        if (HasTransportInvasionBridgeheadGoGreen(transporter, snapshot, out _))
            return true;

        return TryGetTransportScreenMetrics(transporter, snapshot, dropCell, target,
            out float gap, out _, out _)
            && gap >= 0.25f;
    }


    private bool HasTransportInvasionGoGreen(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        Vector3Int target,
        out string reason)
    {
        const float assaultReadyRange = 2f;
        const float bridgeheadAssaultRange = 2f;
        const float bridgeheadArtilleryRange = 5f;
        int assaultReady = 0;
        int bridgeheadAssault = 0;
        int bridgeheadArtillery = 0;
        float nearestAssault = float.MaxValue;
        float nearestBridgeheadAssault = float.MaxValue;
        float nearestBridgeheadArtillery = float.MaxValue;
        target.z = 0;

        if (snapshot?.MyUnits == null)
        {
            reason = "sem snapshot";
            return false;
        }

        foreach (UnitManager ally in snapshot.MyUnits)
        {
            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            if (IsTransportInvasionAssaultReadyUnit(ally, transporter))
            {
                float dist = SectorManager.HexDistance(allyCell, target);
                if (dist < nearestAssault)
                    nearestAssault = dist;
                if (dist <= assaultReadyRange)
                    assaultReady++;

                Vector3Int transportCell = transporter != null ? transporter.CurrentCellPosition : Vector3Int.zero;
                transportCell.z = 0;
                float bridgeDist = SectorManager.HexDistance(allyCell, transportCell);
                if (bridgeDist < nearestBridgeheadAssault)
                    nearestBridgeheadAssault = bridgeDist;
                if (bridgeDist <= bridgeheadAssaultRange)
                    bridgeheadAssault++;
            }

            if (IsTransportInvasionArtilleryReadyUnit(ally, transporter))
            {
                Vector3Int transportCell = transporter != null ? transporter.CurrentCellPosition : Vector3Int.zero;
                transportCell.z = 0;
                float artDist = SectorManager.HexDistance(allyCell, transportCell);
                if (artDist < nearestBridgeheadArtillery)
                    nearestBridgeheadArtillery = artDist;
                if (artDist <= bridgeheadArtilleryRange)
                    bridgeheadArtillery++;
            }
        }

        bool targetReady = assaultReady >= 1;
        bool bridgeheadReady = bridgeheadAssault >= 1 && bridgeheadArtillery >= 1;
        bool ready = targetReady || bridgeheadReady;
        reason = ready
            ? targetReady
                ? $"goGreen assalto={assaultReady}<=2h alvo"
                : $"goGreen ponte assalto={bridgeheadAssault}<=2h apc art={bridgeheadArtillery}<=5h"
            : $"aguarda assalto<=2h alvo nearest={(nearestAssault < float.MaxValue ? nearestAssault.ToString("F1") : "?")} ponteAss={(nearestBridgeheadAssault < float.MaxValue ? nearestBridgeheadAssault.ToString("F1") : "?")} ponteArt={(nearestBridgeheadArtillery < float.MaxValue ? nearestBridgeheadArtillery.ToString("F1") : "?")}";
        return ready;
    }


    private bool HasTransportInvasionBridgeheadGoGreen(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        out string reason)
    {
        const float bridgeheadAssaultRange = 2f;
        const float bridgeheadArtilleryRange = 5f;
        int bridgeheadAssault = 0;
        int bridgeheadArtillery = 0;
        float nearestAssault = float.MaxValue;
        float nearestArtillery = float.MaxValue;
        reason = "sem snapshot";

        if (transporter == null || snapshot?.MyUnits == null)
            return false;

        Vector3Int transportCell = transporter.CurrentCellPosition;
        transportCell.z = 0;

        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == transporter || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float dist = SectorManager.HexDistance(allyCell, transportCell);

            if (IsTransportInvasionAssaultReadyUnit(ally, transporter))
            {
                if (dist < nearestAssault)
                    nearestAssault = dist;
                if (dist <= bridgeheadAssaultRange)
                    bridgeheadAssault++;
            }

            if (IsTransportInvasionArtilleryReadyUnit(ally, transporter))
            {
                if (dist < nearestArtillery)
                    nearestArtillery = dist;
                if (dist <= bridgeheadArtilleryRange)
                    bridgeheadArtillery++;
            }
        }

        bool ready = bridgeheadAssault >= 1 && bridgeheadArtillery >= 1;
        reason = ready
            ? $"ponte assalto={bridgeheadAssault}<=2h art={bridgeheadArtillery}<=5h"
            : $"ponte aguarda ass={(nearestAssault < float.MaxValue ? nearestAssault.ToString("F1") : "?")} art={(nearestArtillery < float.MaxValue ? nearestArtillery.ToString("F1") : "?")}";
        return ready;
    }


    private Vector3Int FindTransportInvasionRendezvousCell(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int target,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out string reason)
    {
        Vector3Int best = fromCell;
        float bestScore = float.MinValue;
        reason = "sem screen";

        if (TryFindBestToolProgressionCell(
                transporter,
                snapshot,
                fromCell,
                target,
                paths,
                occupied,
                ToolProgressionIntent.TransportRendezvous,
                out Vector3Int toolCell,
                out ToolProgressionCandidate toolCandidate,
                out string toolReason,
                allowCell: cell =>
                {
                    if (IsNonTeamConstruction(cell, snapshot.AITeam))
                        return false;
                    if (!TryGetTransportScreenMetrics(transporter, snapshot, cell, target,
                            out float gap, out _, out _))
                        return false;
                    return gap >= 0.75f;
                },
                tacticalScore: (cell, candidate) =>
                {
                    if (!TryGetTransportScreenMetrics(transporter, snapshot, cell, target,
                            out float gap, out float nearestScreenDist, out float cellDist))
                        return -100000f;

                    float threat = CalculateThreatLevel(cell, snapshot.AITeam);
                    float dpq = GetTerrainDpqPontos(cell);
                    float idealGap = 2f;
                    return 3000f
                        - Mathf.Abs(gap - idealGap) * 420f
                        - threat * 90f
                        - candidate.MoveCost * 12f
                        - cellDist * 4f
                        + dpq * 25f
                        - nearestScreenDist * 2f;
                }))
        {
            best = toolCell;
            reason = $"toolProgress {toolReason}";
            return best;
        }

        var candidates = new List<Vector3Int> { fromCell };
        if (paths != null)
            candidates.AddRange(paths.Keys);

        foreach (Vector3Int rawCell in candidates)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell != fromCell && occupied != null && occupied.Contains(cell))
                continue;
            if (cell != fromCell && IsNonTeamConstruction(cell, snapshot.AITeam))
                continue;
            if (!TryGetTransportScreenMetrics(transporter, snapshot, cell, target,
                    out float gap, out float nearestScreenDist, out float cellDist))
                continue;
            if (gap < 0.75f)
                continue;

            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            int pathCost = cell == fromCell ? 0 : GetPathStepCount(paths, cell);
            float dpq = GetTerrainDpqPontos(cell);
            float idealGap = 2f;
            float score = 3000f
                - Mathf.Abs(gap - idealGap) * 420f
                - threat * 90f
                - pathCost * 12f
                - cellDist * 4f
                + dpq * 25f
                + (cell == fromCell ? 120f : 0f);

            if (score > bestScore)
            {
                bestScore = score;
                best = cell;
                reason = $"gap={gap:F1} screenDist={nearestScreenDist:F1} cellDist={cellDist:F1} threat={threat:F1} path={pathCost} score={score:F0}";
            }
        }

        return best;
    }


    private bool TryGetTransportScreenMetrics(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int target,
        out float gap,
        out float nearestScreenDist,
        out float cellDist)
    {
        gap = 0f;
        nearestScreenDist = float.MaxValue;
        cell.z = 0;
        target.z = 0;
        cellDist = SectorManager.HexDistance(cell, target);

        if (snapshot == null || snapshot.MyUnits == null)
            return false;

        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (!IsTransportInvasionScreenUnit(ally, transporter))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float dist = SectorManager.HexDistance(allyCell, target);
            if (dist < nearestScreenDist)
                nearestScreenDist = dist;
        }

        if (nearestScreenDist >= float.MaxValue)
            return false;

        gap = cellDist - nearestScreenDist;
        return true;
    }


    private static bool IsTransportInvasionScreenUnit(UnitManager ally, UnitManager transporter)
    {
        if (ally == null || ally == transporter || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
            return false;
        if (!ally.TryGetUnitData(out UnitData data) || data == null || data.roles == null)
            return false;

        return data.roles.Contains(UnitRole.Assalto)
            || data.roles.Contains(UnitRole.Capturador);
    }


    private static bool IsTransportInvasionAssaultReadyUnit(UnitManager ally, UnitManager transporter)
    {
        if (ally == null || ally == transporter || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
            return false;
        if (!ally.TryGetUnitData(out UnitData data) || data == null || data.roles == null)
            return false;

        return data.roles.Contains(UnitRole.Assalto);
    }


    private static bool IsTransportInvasionArtilleryReadyUnit(UnitManager ally, UnitManager transporter)
    {
        if (ally == null || ally == transporter || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
            return false;
        if (!ally.TryGetUnitData(out UnitData data) || data == null || data.roles == null)
            return false;

        return data.roles.Contains(UnitRole.FogoIndireto);
    }

    // -------------------------------------------------------------------------
    // Conservative tow: drop FireSupport at best safe rear-area position
    // -------------------------------------------------------------------------


}
