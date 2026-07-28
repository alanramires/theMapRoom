using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    /// <summary>
    /// Snapshot puro das LZs que o EWACS ainda consegue usar a partir do
    /// estado confirmado atual. Ele limita o reposicionamento normal, mas não
    /// pousa, embarca, move ou publica FOW.
    /// </summary>
    private sealed class EwacsRecoverySnapshot
    {
        public MelhorPousoResult Landing;
        public int NextUpkeep;
        public int MovementAllowance;
        public int CriticalThreshold;
        public bool FuelCritical;

        public bool HasRecovery =>
            Landing != null && Landing.options.Count > 0;
    }

    private static bool IsAirborneAirSurveillanceUnit(
        UnitManager unit)
    {
        if (!IsAirSurveillanceUnit(unit)
            || !unit.TryGetUnitData(out UnitData data)
            || data == null)
        {
            return false;
        }

        return data.domain == Domain.Air
            && unit.GetAircraftType() != AircraftType.None;
    }

    private EwacsRecoverySnapshot BuildEwacsRecoverySnapshot(
        UnitManager unit)
    {
        if (!IsAirborneAirSurveillanceUnit(unit)
            || boardTilemap == null
            || terrainDatabase == null)
        {
            return null;
        }

        var snapshot = new EwacsRecoverySnapshot();
        snapshot.FuelCritical =
            IsAircraftFuelCriticalForNextUpkeep(
                unit,
                out int movementAllowance,
                out int nextUpkeep,
                out int criticalThreshold);
        snapshot.MovementAllowance = movementAllowance;
        snapshot.NextUpkeep = nextUpkeep;
        snapshot.CriticalThreshold = criticalThreshold;
        snapshot.Landing = MelhorPousoService.Evaluate(
            new MelhorPousoRequest
            {
                aircraft = unit,
                map = boardTilemap,
                terrainDatabase = terrainDatabase,
                tacticalBudget = Mathf.Max(
                    0,
                    unit.RemainingMovementPoints),
                operationalTurns = 2
            });
        return snapshot;
    }

    private bool TryBuildEwacsEmergencyRecoveryAction(
        UnitManager ewacs,
        AIWorldSnapshot world,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        EwacsRecoverySnapshot recovery,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = string.Empty;
        if (ewacs == null
            || world == null
            || recovery == null
            || !ewacs.TryGetUnitData(out UnitData data)
            || data == null)
        {
            return false;
        }

        bool maintenanceEmergency =
            ewacs.IsUnderRepair
            || EvaluateRepairTriggers(ewacs, data);
        if (!maintenanceEmergency && !recovery.FuelCritical)
            return false;

        // Normalmente a autoridade global de reparo já interceptou a unidade
        // no Router. Esta proteção local mantém a prioridade correta mesmo se
        // a Vigilância Aérea for consultada isoladamente por debug/ferramenta.
        if (TryBuildAircraftRecoveryApproachAction(
                ewacs,
                world,
                fromCell,
                paths,
                out action))
        {
            reason =
                $"EWACS prioriza recuperacao " +
                $"fuel={ewacs.CurrentFuel} " +
                $"critical={recovery.CriticalThreshold} " +
                $"lz={recovery.Landing.options.Count}";
            return true;
        }

        HashSet<Vector3Int> airOccupied = BuildAirOccupied(ewacs);
        if (TryBuildAircraftEmergencyHoverAction(
                ewacs,
                world,
                fromCell,
                paths,
                airOccupied,
                critical: recovery.FuelCritical,
                out action))
        {
            reason =
                $"EWACS busca LZ de emergencia " +
                $"fuel={ewacs.CurrentFuel} " +
                $"critical={recovery.CriticalThreshold}";
            return true;
        }

        reason =
            $"EWACS em emergencia sem aproximacao materializavel " +
            $"fuel={ewacs.CurrentFuel} " +
            $"critical={recovery.CriticalThreshold}";
        return false;
    }

    private bool IsEwacsRecoveryCellSafe(
        UnitManager ewacs,
        Vector3Int candidate,
        List<Vector3Int> path,
        EwacsRecoverySnapshot recovery,
        out string reason)
    {
        reason = "n/a";
        if (recovery == null)
            return true;

        candidate.z = 0;
        int movementFuel = path != null
            ? UnitMovementPathRules.CalculateAutonomyCostForPath(
                boardTilemap,
                ewacs,
                path,
                terrainDatabase,
                applyOperationalAutonomyModifier: true)
            : 0;
        int availableAfterMoveAndUpkeep =
            ewacs.CurrentFuel
            - movementFuel
            - recovery.NextUpkeep;

        if (!recovery.HasRecovery)
        {
            reason =
                $"sem LZ; hold fuel={ewacs.CurrentFuel} " +
                $"move={movementFuel} upkeep={recovery.NextUpkeep}";
            return path == null || movementFuel == 0;
        }

        int nearestReturn = int.MaxValue;
        MelhorPousoOption nearest = null;
        for (int i = 0;
             i < recovery.Landing.options.Count;
             i++)
        {
            MelhorPousoOption option =
                recovery.Landing.options[i];
            if (option == null)
                continue;

            int distance =
                AIActionReachCoordinator.CubicDistance(
                    candidate,
                    option.cell);
            if (distance < nearestReturn)
            {
                nearestReturn = distance;
                nearest = option;
            }
        }

        if (nearest == null)
        {
            reason = "snapshot sem LZ utilizavel";
            return path == null || movementFuel == 0;
        }

        bool safe = nearestReturn <= availableAfterMoveAndUpkeep;
        reason =
            $"{(nearest.IsPlatform ? "platform" : "lz")}=" +
            $"{nearest.cell} return={nearestReturn} " +
            $"budget={availableAfterMoveAndUpkeep} " +
            $"moveFuel={movementFuel} upkeep={recovery.NextUpkeep}";
        return safe;
    }
}
