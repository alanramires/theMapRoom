using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class UnitOccupancyRules
{
    public static event Action<UnitManager, Vector3Int, Vector3Int> OnUnitOccupancyChanged;

    private static int cachedUnitsFrame = -1;
    private static UnitManager[] cachedUnits = System.Array.Empty<UnitManager>();

    private static UnitManager[] GetActiveUnitsSnapshot()
    {
        int frame = Time.frameCount;
        if (cachedUnitsFrame == frame && cachedUnits != null)
            return cachedUnits;

        var all = UnitManager.AllActive;
        if (all == null || all.Count == 0)
        {
            cachedUnits = System.Array.Empty<UnitManager>();
        }
        else
        {
            if (cachedUnits == null || cachedUnits.Length != all.Count)
                cachedUnits = new UnitManager[all.Count];
            all.CopyTo(cachedUnits);
        }
        cachedUnitsFrame = frame;
        return cachedUnits;
    }

    public static void NotifyUnitOccupancyChanged(UnitManager unit, Vector3Int previousCell, Vector3Int currentCell)
    {
        cachedUnitsFrame = -1;
        if (unit == null || !Application.isPlaying)
            return;

        previousCell.z = 0;
        currentCell.z = 0;
        OnUnitOccupancyChanged?.Invoke(unit, previousCell, currentCell);
    }

    public static bool IsUnitCellOccupied(Tilemap referenceTilemap, Vector3Int cell, UnitManager exceptUnit = null)
    {
        if (UnitRulesDefinition.IsTotalWarEnabled() && exceptUnit != null)
            return IsUnitCellOccupiedForTeam(referenceTilemap, cell, exceptUnit.TeamId, exceptUnit);

        cell.z = 0;
        int count = 0;

        UnitManager[] units = GetActiveUnitsSnapshot();
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked)
                continue;
            if (!IsUnitOnReferenceMap(unit, referenceTilemap))
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell != cell)
                continue;

            count++;
            if (count >= UnitRulesDefinition.MaxUnitsPerHex)
                return true;
        }

        return false;
    }

    public static bool IsUnitCellOccupiedForTeam(Tilemap referenceTilemap, Vector3Int cell, TeamId teamId, UnitManager exceptUnit = null)
    {
        cell.z = 0;

        UnitManager[] units = GetActiveUnitsSnapshot();
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked)
                continue;
            if (unit.TeamId != teamId)
                continue;
            if (!IsUnitOnReferenceMap(unit, referenceTilemap))
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell == cell)
                return true;
        }

        return false;
    }

    public static UnitManager GetUnitAtCell(Tilemap referenceTilemap, Vector3Int cell, UnitManager exceptUnit = null)
    {
        cell.z = 0;

        if (UnitRulesDefinition.IsTotalWarEnabled() && exceptUnit != null)
        {
            // Prioriza retornar bloqueador do mesmo time para evitar empilhamento
            // quando coexistencia entre times diferentes for permitida.
            UnitManager sameTeam = null;
            UnitManager otherTeam = null;

            UnitManager[] totalWarUnits = GetActiveUnitsSnapshot();
            for (int i = 0; i < totalWarUnits.Length; i++)
            {
                UnitManager unit = totalWarUnits[i];
                if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked)
                    continue;
                if (!IsUnitOnReferenceMap(unit, referenceTilemap))
                    continue;

                Vector3Int occupiedCell = unit.CurrentCellPosition;
                occupiedCell.z = 0;
                if (occupiedCell != cell)
                    continue;

                if (unit.TeamId == exceptUnit.TeamId)
                {
                    sameTeam = unit;
                    break;
                }

                if (otherTeam == null)
                    otherTeam = unit;
            }

            if (sameTeam != null)
                return sameTeam;
            if (otherTeam != null)
                return otherTeam;
        }

        UnitManager[] units = GetActiveUnitsSnapshot();
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked)
                continue;
            if (!IsUnitOnReferenceMap(unit, referenceTilemap))
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell == cell)
                return unit;
        }

        return null;
    }

    private static bool IsUnitOnReferenceMap(UnitManager unit, Tilemap referenceTilemap)
    {
        if (unit == null || referenceTilemap == null)
            return false;
        if (unit.BoardTilemap == null || unit.BoardTilemap != referenceTilemap)
            return false;

        return unit.gameObject.scene == referenceTilemap.gameObject.scene;
    }
}
