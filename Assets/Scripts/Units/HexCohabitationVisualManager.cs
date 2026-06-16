using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class HexCohabitationVisualManager
{
    public static Vector3 AirOffset = new Vector3(-0.1f, 0.2f, 0f);
    public static Vector3 SurfaceOffset = new Vector3(0f, -0.2f, 0f);
    public static Vector3 SharedScale = new Vector3(0.6f, 0.6f, 1f);
    // Espalhamento horizontal entre unidades da MESMA banda dividindo o hex
    // (ex.: dois caças de times diferentes em hex contestado).
    public static float IntraLayerSpread = 0.18f;

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

        List<UnitManager> airUnits = new List<UnitManager>();
        List<UnitManager> surfaceUnits = new List<UnitManager>();

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager u = units[i];
            if (u == null || u.IsEmbarked || u.IsDead)
                continue;

            HeightBand band = OccupancyResolver.GetHeightBand(u);
            if (band == HeightBand.Air)
                airUnits.Add(u);
            else if (band == HeightBand.Blocking)
                surfaceUnits.Add(u);
        }

        // Reset baseline: so re-aplicamos visual quando houver de fato divisao de hex.
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] != null)
                units[i].ClearCohabitationVisual();
        }

        // Divisao de hex so quando ha ao menos um aereo envolvido (ar+ar ou ar+chao).
        // Solo+solo contestado nao recebe efeito visual.
        if (airUnits.Count == 0 || airUnits.Count + surfaceUnits.Count < 2)
            return;

        ApplyLayerFan(airUnits, AirOffset);
        ApplyLayerFan(surfaceUnits, SurfaceOffset);
    }

    // Distribui as unidades de uma mesma banda em leque horizontal ao redor do offset base.
    // Com 1 unidade, mantem o offset base exatamente (sem regressao no caso ar+chao classico).
    private static void ApplyLayerFan(List<UnitManager> layerUnits, Vector3 baseOffset)
    {
        int n = layerUnits.Count;
        if (n == 0)
            return;

        float center = (n - 1) * 0.5f;
        for (int i = 0; i < n; i++)
        {
            UnitManager u = layerUnits[i];
            if (u == null)
                continue;

            float dx = (i - center) * IntraLayerSpread;
            Vector3 offset = baseOffset + new Vector3(dx, 0f, 0f);
            u.ApplyCohabitationVisual(offset, SharedScale);
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
