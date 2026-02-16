using UnityEngine;
using UnityEngine.Tilemaps;

public static class HexCoordinates
{
    public static Vector3 GetCellCenterWorld(Tilemap tilemap, Vector3Int cell)
    {
        if (tilemap == null)
            return Vector3.zero;

        return tilemap.GetCellCenterWorld(cell);
    }

    public static Vector3Int WorldToCell(Tilemap tilemap, Vector3 world)
    {
        if (tilemap == null)
            return Vector3Int.zero;

        return tilemap.WorldToCell(world);
    }
}
