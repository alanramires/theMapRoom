using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeFundirSensor
{
    public static bool TryEvaluate(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        out int sameTypeAdjacentCount,
        out string reason)
    {
        sameTypeAdjacentCount = 0;
        reason = string.Empty;

        if (selectedUnit == null)
        {
            reason = "Selecione uma unidade.";
            return false;
        }

        if (selectedUnit.IsEmbarked)
        {
            reason = "Unidade embarcada nao pode fundir.";
            return false;
        }

        Tilemap map = boardTilemap != null ? boardTilemap : selectedUnit.BoardTilemap;
        if (map == null)
        {
            reason = "Tilemap indisponivel para avaliar fusao.";
            return false;
        }

        Vector3Int origin = selectedUnit.CurrentCellPosition;
        origin.z = 0;

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(map, origin, neighbors);

        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int cell = neighbors[i];
            cell.z = 0;

            UnitManager other = UnitOccupancyRules.GetUnitAtCell(map, cell, selectedUnit);
            if (other == null || other == selectedUnit || !other.gameObject.activeInHierarchy || other.IsEmbarked)
                continue;

            if (!IsSameTypeAndTeam(selectedUnit, other))
                continue;

            sameTypeAdjacentCount++;
        }

        if (sameTypeAdjacentCount <= 0)
        {
            reason = "Sem unidade adjacente (1 hex) do mesmo tipo para fundir.";
            return false;
        }

        return true;
    }

    private static bool IsSameTypeAndTeam(UnitManager a, UnitManager b)
    {
        if (a == null || b == null)
            return false;

        if ((int)a.TeamId != (int)b.TeamId)
            return false;

        string aId = a.UnitId;
        string bId = b.UnitId;
        if (!string.IsNullOrWhiteSpace(aId) && !string.IsNullOrWhiteSpace(bId))
            return string.Equals(aId.Trim(), bId.Trim(), System.StringComparison.OrdinalIgnoreCase);

        if (a.TryGetUnitData(out UnitData aData) && b.TryGetUnitData(out UnitData bData))
            return aData != null && bData != null && aData == bData;

        return false;
    }
}
