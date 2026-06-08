using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const float RogueCourierLocalSectorRange = 1.5f;

    private bool TryBuildRogueCourierLocalOpportunityDrop(
        UnitManager transporter,
        List<UnitManager> passengers,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        action = null;
        if (transporter == null || passengers == null || passengers.Count == 0 || snapshot == null)
            return false;

        PodeDesembarcarOption bestOption = null;
        UnitManager bestPassenger = null;
        ConstructionManager bestTarget = null;
        Vector3Int bestTransporterCell = fromCell;
        float bestScore = float.MinValue;

        var transporterCells = new List<Vector3Int> { fromCell };
        if (paths != null)
        {
            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (cell == fromCell)
                    continue;
                transporterCells.Add(cell);
            }
        }

        foreach (UnitManager passenger in passengers)
        {
            if (!IsRogueLocalOpportunityPassenger(passenger, plan))
                continue;

            foreach (ConstructionManager target in ConstructionManager.AllActive)
            {
                if (!IsRogueLocalOpportunityTarget(target, snapshot.AITeam, plan))
                    continue;

                Vector3Int targetCell = target.CurrentCellPosition;
                targetCell.z = 0;

                for (int i = 0; i < transporterCells.Count; i++)
                {
                    Vector3Int transporterCell = transporterCells[i];
                    transporterCell.z = 0;
                    if (transporterCell != fromCell && occupied != null && occupied.Contains(transporterCell))
                        continue;
                    if (transporterCell != fromCell && IsNonTeamConstruction(transporterCell, snapshot.AITeam))
                        continue;

                    List<PodeDesembarcarOption> options = transporterCell == fromCell
                        ? CollectCurrentRogueCourierLocalDisembarkOptions(transporter)
                        : SimulateDisembarkFromCell(transporter, transporterCell);
                    if (options == null || options.Count == 0)
                        continue;

                    foreach (PodeDesembarcarOption opt in options)
                    {
                        if (opt == null || opt.passengerUnit != passenger)
                            continue;

                        Vector3Int dropCell = opt.disembarkCell;
                        dropCell.z = 0;
                        float dropDist = SectorManager.HexDistance(dropCell, targetCell);
                        if (dropDist > RogueCourierLocalSectorRange)
                            continue;

                        float score = ScoreRogueCourierLocalOpportunity(
                            passenger, transporterCell, dropCell, targetCell, snapshot.AITeam, paths, fromCell);

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestOption = opt;
                            bestPassenger = passenger;
                            bestTarget = target;
                            bestTransporterCell = transporterCell;
                        }
                    }
                }
            }
        }

        if (bestOption == null || bestPassenger == null || bestTarget == null)
            return false;

        var selected = new List<PodeDesembarcarOption> { bestOption };
        Vector3Int targetPos = bestTarget.CurrentCellPosition;
        targetPos.z = 0;

        if (bestTransporterCell == fromCell)
        {
            Debug.Log($"{TL("Transporte")} {transporter.InstanceId} courier rogue libera #{bestPassenger.InstanceId} em {bestTarget.Sector} agora dc={bestOption.disembarkCell} alvo={targetPos} score={bestScore:F0}");
            action = BuildDesembarcarBatch(transporter, snapshot.AITeam, fromCell, selected);
            return true;
        }

        List<Vector3Int> movePath = null;
        paths?.TryGetValue(bestTransporterCell, out movePath);
        Debug.Log($"{TL("Transporte")} {transporter.InstanceId} courier rogue libera #{bestPassenger.InstanceId} em {bestTarget.Sector} via {bestTransporterCell} dc={bestOption.disembarkCell} alvo={targetPos} score={bestScore:F0}");
        action = BuildDesembarcarBatch(transporter, snapshot.AITeam, fromCell, selected, bestTransporterCell, movePath);
        return true;
    }

    private List<PodeDesembarcarOption> CollectCurrentRogueCourierLocalDisembarkOptions(UnitManager transporter)
    {
        var options = new List<PodeDesembarcarOption>();
        PodeDesembarcarSensor.CollectOptions(transporter, boardTilemap, terrainDatabase, options);
        return options;
    }

    private bool TryBuildRogueCourierContestedRendezvousDrop(
        UnitManager transporter,
        List<UnitManager> passengers,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int fromCell,
        Vector3Int transporterCell,
        List<PodeDesembarcarOption> options,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        out PlayerAction action)
    {
        action = null;
        if (transporter == null || passengers == null || passengers.Count == 0 || snapshot == null || options == null || options.Count == 0)
            return false;

        PodeDesembarcarOption bestOption = null;
        UnitManager bestPassenger = null;
        ConstructionManager bestTarget = null;
        float bestScore = float.MinValue;
        int passengerRejected = 0;
        int targetRejected = 0;
        int rangeRejected = 0;
        int optionRejected = 0;

        foreach (UnitManager passenger in passengers)
        {
            if (!IsRogueLocalOpportunityPassenger(passenger, plan))
            {
                passengerRejected++;
                continue;
            }

            foreach (PodeDesembarcarOption opt in options)
            {
                if (opt == null || opt.passengerUnit != passenger)
                {
                    optionRejected++;
                    continue;
                }

                Vector3Int dropCell = opt.disembarkCell;
                dropCell.z = 0;
                foreach (ConstructionManager target in ConstructionManager.AllActive)
                {
                    if (!IsRogueCourierContestedRendezvousTarget(target, snapshot.AITeam))
                    {
                        targetRejected++;
                        continue;
                    }

                    Vector3Int targetCell = target.CurrentCellPosition;
                    targetCell.z = 0;
                    float dropDist = SectorManager.HexDistance(dropCell, targetCell);
                    if (dropDist > TransportDropOffRange)
                    {
                        rangeRejected++;
                        continue;
                    }

                    float score = ScoreRogueCourierContestedRendezvousDrop(
                        passenger, transporterCell, dropCell, target, snapshot.AITeam, paths, fromCell);
                    if (score <= bestScore)
                        continue;

                    bestScore = score;
                    bestOption = opt;
                    bestPassenger = passenger;
                    bestTarget = target;
                }
            }
        }

        if (bestOption == null || bestPassenger == null || bestTarget == null)
        {
            Debug.Log($"{TL("Transporte")} {transporter.InstanceId} courier invasao-rendezvous sem drop util: passageirosSkip={passengerRejected} optsSkip={optionRejected} targetsSkip={targetRejected} rangeSkip={rangeRejected} range={TransportDropOffRange}");
            return false;
        }

        List<Vector3Int> movePath = null;
        paths?.TryGetValue(transporterCell, out movePath);
        var selected = new List<PodeDesembarcarOption> { bestOption };
        Vector3Int targetPos = bestTarget.CurrentCellPosition;
        targetPos.z = 0;
        Debug.Log($"{TL("Transporte")} {transporter.InstanceId} courier invasao-rendezvous libera #{bestPassenger.InstanceId} para disputar {bestTarget.Sector} via {transporterCell} dc={bestOption.disembarkCell} alvo={targetPos} cap={bestTarget.CurrentCapturePoints}/{bestTarget.CapturePointsMax} score={bestScore:F0}");
        action = BuildDesembarcarBatch(transporter, snapshot.AITeam, fromCell, selected, transporterCell, movePath);
        return true;
    }

    private bool IsRogueLocalOpportunityPassenger(UnitManager passenger, TeamObjectivePlan plan)
    {
        if (passenger == null || passenger.IsDead || passenger.IsUnderRepair)
            return false;
        if (plan != null && IsPassengerInPlanSlot(passenger, plan))
            return false;
        if (!passenger.TryGetUnitData(out UnitData data) || data?.roles == null)
            return false;

        return data.roles.Contains(UnitRole.Capturador)
            || data.roles.Contains(UnitRole.Assalto);
    }

    private bool IsRogueLocalOpportunityTarget(
        ConstructionManager target,
        TeamId aiTeam,
        TeamObjectivePlan plan)
    {
        if (target == null || !target.IsCapturable || target.CapturePointsMax <= 0)
            return false;
        if (target.TeamId != TeamId.Neutral)
            return false;
        if (target.Sector == ConstructionSector.None || ConstructionSectorHelper.IsBase(target.Sector))
            return false;
        if (HasBlockingSurfaceUnitAtCell(target.CurrentCellPosition))
            return false;
        if (HasPlanAllocationForSector(plan, target.Sector, aiTeam))
            return false;

        return true;
    }

    private bool IsRogueCourierContestedRendezvousTarget(ConstructionManager target, TeamId aiTeam)
    {
        if (target == null || !target.IsCapturable || target.CapturePointsMax <= 0)
            return false;
        if (target.Sector == ConstructionSector.None || ConstructionSectorHelper.IsBase(target.Sector) || target.IsPlayerHeadQuarter)
            return false;
        if (target.TeamId == aiTeam && target.CurrentCapturePoints >= target.CapturePointsMax)
            return false;

        return target.TeamId != aiTeam || target.CurrentCapturePoints < target.CapturePointsMax;
    }

    private bool HasPlanAllocationForSector(TeamObjectivePlan plan, ConstructionSector sector, TeamId aiTeam)
    {
        if (plan == null || plan.Objectives == null)
            return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Sector != sector || obj.Slots == null)
                continue;

            foreach (SlotNeed slot in obj.Slots)
            {
                if (slot == null || !slot.Filled)
                    continue;
                UnitManager assigned = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (assigned != null && !assigned.IsEmbarked && !assigned.HasActed)
                    return true;
            }
        }

        return false;
    }

    private bool HasBlockingSurfaceUnitAtCell(Vector3Int cell)
    {
        cell.z = 0;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (OccupancyResolver.GetHeightBand(unit) != HeightBand.Blocking)
                continue;

            Vector3Int unitCell = unit.CurrentCellPosition;
            unitCell.z = 0;
            if (unitCell == cell)
                return true;
        }

        return false;
    }

    private float ScoreRogueCourierLocalOpportunity(
        UnitManager passenger,
        Vector3Int transporterCell,
        Vector3Int dropCell,
        Vector3Int targetCell,
        TeamId aiTeam,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Vector3Int fromCell)
    {
        float dropDist = SectorManager.HexDistance(dropCell, targetCell);
        float threat = CalculateThreatLevel(dropCell, aiTeam);
        int pathCost = transporterCell == fromCell ? 0 : GetPathStepCount(paths, transporterCell);
        float score = 6000f
            - dropDist * 900f
            - threat * 80f
            - pathCost * 15f
            + GetTerrainDpqPontos(dropCell) * 45f;

        if (passenger != null && passenger.TryGetUnitData(out UnitData data) && data?.roles != null)
        {
            if (data.roles.Contains(UnitRole.Capturador) && dropCell == targetCell)
                score += 2200f;
            if (data.roles.Contains(UnitRole.Assalto))
                score += 450f;
        }

        return score;
    }

    private float ScoreRogueCourierContestedRendezvousDrop(
        UnitManager passenger,
        Vector3Int transporterCell,
        Vector3Int dropCell,
        ConstructionManager target,
        TeamId aiTeam,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Vector3Int fromCell)
    {
        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        float dropDist = SectorManager.HexDistance(dropCell, targetCell);
        float threat = CalculateThreatLevel(dropCell, aiTeam);
        int pathCost = transporterCell == fromCell ? 0 : GetPathStepCount(paths, transporterCell);
        float missingCapture = Mathf.Max(0, target.CapturePointsMax - target.CurrentCapturePoints);
        float score = 7200f
            - dropDist * 900f
            - threat * 70f
            - pathCost * 12f
            + missingCapture * 85f
            + GetTerrainDpqPontos(dropCell) * 45f;

        if (target.TeamId != aiTeam)
            score += 1400f;
        if (SimulateCaptureSensor(passenger, dropCell, out ConstructionManager immediate) && immediate == target)
            score += 3200f;

        if (passenger != null && passenger.TryGetUnitData(out UnitData data) && data?.roles != null)
        {
            if (data.roles.Contains(UnitRole.Capturador))
                score += dropDist <= 0.5f ? 1800f : 650f;
            if (data.roles.Contains(UnitRole.Assalto))
                score += 500f;
        }

        return score;
    }
}
