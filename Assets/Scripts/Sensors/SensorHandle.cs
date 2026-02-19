using System.Collections.Generic;
using UnityEngine.Tilemaps;

public static class SensorHandle
{
    public static bool RunAll(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase,
        WeaponPriorityData weaponPriorityData,
        SensorMovementMode movementMode,
        List<char> availableActionCodes,
        List<PodeMirarTargetOption> podeMirarTargets)
    {
        if (availableActionCodes == null || podeMirarTargets == null)
            return false;

        availableActionCodes.Clear();
        podeMirarTargets.Clear();

        if (selectedUnit == null)
            return false;

        bool hasAnyAction = false;

        bool canAim = PodeMirarSensor.CollectTargets(selectedUnit, boardTilemap, terrainDatabase, movementMode, podeMirarTargets, null, weaponPriorityData);
        if (canAim)
        {
            availableActionCodes.Add('A');
            hasAnyAction = true;
        }

        return hasAnyAction;
    }
}
