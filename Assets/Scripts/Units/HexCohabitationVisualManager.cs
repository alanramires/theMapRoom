using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class HexCohabitationVisualManager
{
    private static readonly Vector3 AirOffset = new Vector3(-0.1f, 0.2f, 0f);
    private static readonly Vector3 SurfaceOffset = new Vector3(0f, -0.2f, 0f);
    private static readonly Vector3 SharedScale = new Vector3(0.6f, 0.6f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        UnitOccupancyRules.OnUnitOccupancyChanged -= OnOccupancyChanged;
        UnitOccupancyRules.OnUnitOccupancyChanged += OnOccupancyChanged;

        // Scan no próximo frame para pegar unidades já posicionadas (load/start de cena)
        var bootstrap = new GameObject("[HexCohabitationBootstrap]");
        Object.DontDestroyOnLoad(bootstrap);
        bootstrap.AddComponent<CohabitationBootstrap>();
    }

    public static void ScanAllCells()
    {
        var visited = new HashSet<(Tilemap, Vector3Int)>();
        var all = UnitManager.AllActive;

        for (int i = 0; i < all.Count; i++)
        {
            UnitManager u = all[i];
            if (u == null || u.IsEmbarked || u.IsDead || u.BoardTilemap == null)
                continue;

            Vector3Int cell = u.CurrentCellPosition;
            cell.z = 0;

            var key = (u.BoardTilemap, cell);
            if (visited.Contains(key))
                continue;

            visited.Add(key);
            EvaluateCell(u.BoardTilemap, cell);
        }
    }

    private static void OnOccupancyChanged(UnitManager unit, Vector3Int previousCell, Vector3Int currentCell)
    {
        if (unit == null)
            return;

        Tilemap tilemap = unit.BoardTilemap;
        if (tilemap == null)
            return;

        previousCell.z = 0;
        currentCell.z = 0;

        if (previousCell != currentCell)
            EvaluateCell(tilemap, previousCell);

        EvaluateCell(tilemap, currentCell);
    }

    private static void EvaluateCell(Tilemap tilemap, Vector3Int cell)
    {
        List<UnitManager> units = UnitOccupancyRules.GetUnitsAtCell(tilemap, cell);

        UnitManager airUnit = null;
        UnitManager surfaceUnit = null;

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager u = units[i];
            if (u == null || u.IsEmbarked || u.IsDead)
                continue;

            HeightBand band = OccupancyResolver.GetHeightBand(u);
            if (band == HeightBand.Air && airUnit == null)
                airUnit = u;
            else if (band == HeightBand.Blocking && surfaceUnit == null)
                surfaceUnit = u;
        }

        if (airUnit != null && surfaceUnit != null)
        {
            airUnit.ApplyCohabitationVisual(AirOffset, SharedScale);
            surfaceUnit.ApplyCohabitationVisual(SurfaceOffset, SharedScale);
        }
        else
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null)
                    units[i].ClearCohabitationVisual();
            }
        }
    }

    private class CohabitationBootstrap : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null; // espera um frame para todos os OnEnable rodarem
            ScanAllCells();
            Destroy(gameObject);
        }
    }
}
