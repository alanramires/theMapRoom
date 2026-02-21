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
        List<PodeEmbarcarInvalidOption> podeEmbarcarInvalidTargets = null)
    {
        if (availableActionCodes == null || podeMirarTargets == null || podeEmbarcarTargets == null)
            return false;

        availableActionCodes.Clear();
        podeMirarTargets.Clear();
        podeEmbarcarTargets.Clear();
        podeEmbarcarInvalidTargets?.Clear();

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

        return hasAnyAction;
    }
}
