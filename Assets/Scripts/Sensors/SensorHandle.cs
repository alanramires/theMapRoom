using System.Collections.Generic;
using UnityEngine.Tilemaps;

public static class SensorHandle
{
    public static bool RunAll(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase,
        WeaponPriorityData weaponPriorityData,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool fogOfWarEnabled,
        SensorMovementMode movementMode,
        int remainingMovementPoints,
        List<char> availableActionCodes,
        List<PodeMirarTargetOption> podeMirarTargets,
        List<PodeEmbarcarOption> podeEmbarcarTargets,
        List<PodeEmbarcarInvalidOption> podeEmbarcarInvalidTargets,
        List<PodeDesembarcarOption> podeDesembarcarTargets,
        List<PodeDesembarcarInvalidOption> podeDesembarcarInvalidTargets = null)
    {
        if (availableActionCodes == null ||
            podeMirarTargets == null ||
            podeEmbarcarTargets == null ||
            podeDesembarcarTargets == null)
            return false;

        availableActionCodes.Clear();
        podeMirarTargets.Clear();
        podeEmbarcarTargets.Clear();
        podeEmbarcarInvalidTargets?.Clear();
        podeDesembarcarTargets.Clear();
        podeDesembarcarInvalidTargets?.Clear();

        if (selectedUnit == null)
            return false;

        bool hasAnyAction = false;

        bool canAim = PodeMirarSensor.CollectTargets(
            selectedUnit,
            boardTilemap,
            terrainDatabase,
            movementMode,
            podeMirarTargets,
            null,
            weaponPriorityData,
            dpqAirHeightConfig,
            fogOfWarEnabled);
        if (canAim)
        {
            availableActionCodes.Add('A');
            hasAnyAction = true;
        }

        bool canEmbark = PodeEmbarcarSensor.CollectOptions(
            selectedUnit,
            boardTilemap,
            terrainDatabase,
            remainingMovementPoints,
            podeEmbarcarTargets,
            podeEmbarcarInvalidTargets);
        if (canEmbark)
        {
            availableActionCodes.Add('E');
            hasAnyAction = true;
        }

        bool canDisembark = PodeDesembarcarSensor.CollectOptions(
            selectedUnit,
            boardTilemap,
            terrainDatabase,
            podeDesembarcarTargets,
            podeDesembarcarInvalidTargets);
        if (canDisembark)
            hasAnyAction = true;

        return hasAnyAction;
    }
}
