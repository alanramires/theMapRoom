using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private List<PodeDesembarcarOption> SimulateDisembarkFromCell(UnitManager unit, Vector3Int simCell)
    {
        Vector3Int originalCell = unit.CurrentCellPosition;
        simCell.z = 0; originalCell.z = 0;
        var options = new List<PodeDesembarcarOption>();

        // Nao experimente desembarque a partir de um destino ainda preto. O
        // batch pode mover ate la, mas so podera desembarcar numa decisao futura,
        // depois de o movimento ser comprometido e o FOW confirmado em Neutral.
        if (simCell != originalCell
            && (matchController == null || !matchController.IsCellVisibleForActiveTeam(simCell)))
            return options;

        try
        {
            unit.SetCurrentCellPosition(simCell, enforceFinalOccupancyRule: false);
            PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, options);
        }
        finally
        {
            unit.SetCurrentCellPosition(originalCell, enforceFinalOccupancyRule: false);
        }

        // Tambem nao revele, pela lista de opcoes, o terreno/ocupacao de uma
        // celula de desembarque que o snapshot confirmado ainda nao enxerga.
        if (matchController == null)
            options.Clear();
        else
            options.RemoveAll(option =>
            {
                Vector3Int targetCell = option.disembarkCell;
                targetCell.z = 0;
                return !matchController.IsCellVisibleForActiveTeam(targetCell);
            });

        return options;
    }

    // -------------------------------------------------------------------------
    // Passenger helpers
    // -------------------------------------------------------------------------


    private List<PodeDesembarcarOption> SelectBestDisembarkPerPassenger(
        List<PodeDesembarcarOption> options,
        List<UnitManager> passengers,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot)
    {
        var selected = new List<PodeDesembarcarOption>();
        foreach (UnitManager passenger in passengers)
        {
            bool targetFound = TryResolveCourierPassengerTarget(passenger, plan, snapshot, Vector3Int.zero, passenger.CurrentCellPosition, out Vector3Int target);
            PodeDesembarcarOption best = null;
            float bestDist = float.MaxValue;
            float bestThreat = float.MaxValue;

            foreach (PodeDesembarcarOption opt in options)
            {
                if (opt.passengerUnit != passenger) continue;
                Vector3Int dc = opt.disembarkCell; dc.z = 0;
                float dist = targetFound
                    ? SectorManager.HexDistance(dc, target)
                    : 0f;
                float threat = CalculateThreatLevel(dc, snapshot.AITeam);
                float score = ScoreCourierDisembarkOption(passenger, dc, target, snapshot.AITeam, dist, threat);
                float bestScore = best != null
                    ? ScoreCourierDisembarkOption(passenger, best.disembarkCell, target, snapshot.AITeam, bestDist, bestThreat)
                    : float.MinValue;
                bool isBetter = score > bestScore + 0.1f
                    || (score > bestScore - 0.1f && dist < bestDist - 0.1f)
                    || (score > bestScore - 0.1f && dist < bestDist + 0.1f && threat < bestThreat - 0.001f);
                if (isBetter) { bestDist = dist; bestThreat = threat; best = opt; }
            }

            if (best != null) selected.Add(best);
        }
        return selected;
    }

    // Partial disembark: remove passengers whose best drop-off cell is outside dropOffRange
    // of their own objective. They stay aboard for delivery on the next leg.
    // When allowStuck is true (helicopter can't move), all passengers are released.

    private List<PodeDesembarcarOption> FilterDisembarkByTargetRange(
        List<PodeDesembarcarOption> options,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        int dropOffRange,
        bool allowStuck = false)
    {
        if (allowStuck || options.Count <= 1) return options;
        var filtered = new List<PodeDesembarcarOption>();
        foreach (PodeDesembarcarOption opt in options)
        {
            bool targetFound = TryResolveCourierPassengerTarget(
                opt.passengerUnit, plan, snapshot, Vector3Int.zero,
                opt.passengerUnit.CurrentCellPosition, out Vector3Int target);
            if (!targetFound) { filtered.Add(opt); continue; }
            Vector3Int dc = opt.disembarkCell; dc.z = 0;
            float dist = SectorManager.HexDistance(dc, target);
            if (dist <= dropOffRange)
                filtered.Add(opt);
            else if (IsRogueCapturerPassenger(opt.passengerUnit, plan)
                     && IsUsefulRogueDropCell(opt.passengerUnit, dc, snapshot, dropOffRange))
            {
                filtered.Add(opt);
                Debug.Log($"{TL("Transporte")} partial_disembark: #{opt.passengerUnit.InstanceId} rogue DESCE oportunista dc={dc} alvoOriginal={target} dist={dist:F0}h");
            }
            else
                Debug.Log($"{TL("Transporte")} partial_disembark: #{opt.passengerUnit.InstanceId} FICA — dc={dc} alvo={target} dist={dist:F0}h > {dropOffRange}h");
        }
        // Safety: never return empty — if every passenger is out of range keep all.
        // This prevents the helicopter from carrying cargo forever when it gets stuck.
        return filtered.Count > 0 ? filtered : options;
    }


    private bool IsRogueCapturerPassenger(UnitManager passenger, TeamObjectivePlan plan)
    {
        if (passenger == null || plan == null) return false;
        if (IsPassengerInPlanSlot(passenger, plan)) return false;
        if (!passenger.TryGetUnitData(out UnitData data) || data?.roles == null) return false;
        return data.roles.Contains(UnitRole.Capturador);
    }


    private bool IsUsefulRogueDropCell(UnitManager passenger, Vector3Int dropCell, AIWorldSnapshot snapshot, int range)
    {
        if (passenger == null || snapshot == null) return false;

        dropCell.z = 0;
        if (SimulateCaptureSensor(passenger, dropCell, out ConstructionManager immediate)
            && immediate != null
            && immediate.SlotIndex != snapshot.AISlotIndex)
            return true;

        foreach (ConstructionManager b in ConstructionManager.AllActive)
        {
            if (b == null || !b.IsCapturable || b.CapturePointsMax <= 0)
                continue;
            if (b.SlotIndex == snapshot.AISlotIndex && b.CurrentCapturePoints >= b.CapturePointsMax)
                continue;
            if (b.Sector == ConstructionSector.None || ConstructionSectorHelper.IsBase(b.Sector) || b.IsPlayerHeadQuarter)
                continue;
            if (HasBlockingSurfaceUnitAtCell(b.CurrentCellPosition))
                continue;

            Vector3Int bc = b.CurrentCellPosition;
            bc.z = 0;
            if (SectorManager.HexDistance(dropCell, bc) <= range)
                return true;
        }

        return false;
    }


    private float ScoreCourierDisembarkOption(
        UnitManager passenger,
        Vector3Int disembarkCell,
        Vector3Int assignedTarget,
        TeamId aiTeam,
        float distToAssignedTarget,
        float threat)
    {
        disembarkCell.z = 0;
        float score = -distToAssignedTarget * 20f;

        // FireSupport units prioritize defensive position quality — they fight from where they land.
        if (IsFireSupportUnit(passenger))
            score += GetTerrainDpqPontos(disembarkCell) * 60f;

        if (SimulateCaptureSensor(passenger, disembarkCell, out ConstructionManager captureTarget))
        {
            Vector3Int captureCell = captureTarget.CurrentCellPosition; captureCell.z = 0;
            bool isAssignedTarget = assignedTarget != Vector3Int.zero && captureCell == assignedTarget;
            bool isNeutralOrEnemy = captureTarget.SlotIndex != ResolveAISlotKey(aiTeam);
            score += isAssignedTarget ? 3000f : isNeutralOrEnemy ? 1800f : 900f;
        }

        return score;
    }

    // Returns true if a real target was resolved (including cell (0,0,0) which is a valid hex).
    // Returns false only when no target could be determined at all.

}
