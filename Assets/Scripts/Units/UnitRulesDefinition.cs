using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class UnitRulesDefinition
{
    public const int MaxUnitsPerHex = 1;

    public static bool IsUnitCellOccupied(Tilemap referenceTilemap, Vector3Int cell, UnitManager exceptUnit = null)
    {
        return UnitOccupancyRules.IsUnitCellOccupied(referenceTilemap, cell, exceptUnit);
    }

    public static bool IsUnitCellOccupiedForTeam(Tilemap referenceTilemap, Vector3Int cell, TeamId teamId, UnitManager exceptUnit = null)
    {
        return UnitOccupancyRules.IsUnitCellOccupiedForTeam(referenceTilemap, cell, teamId, exceptUnit);
    }

    public static UnitManager GetUnitAtCell(Tilemap referenceTilemap, Vector3Int cell, UnitManager exceptUnit = null)
    {
        return UnitOccupancyRules.GetUnitAtCell(referenceTilemap, cell, exceptUnit);
    }

    public static bool CanPassThrough(UnitManager mover, UnitManager blocker)
    {
        if (blocker == null)
            return true;
        if (mover == null)
            return false;

        Domain moverDomain = mover.GetDomain();
        Domain blockerDomain = blocker.GetDomain();
        HeightLevel moverHeight = mover.GetHeightLevel();
        HeightLevel blockerHeight = blocker.GetHeightLevel();

        // Camadas diferentes podem se cruzar (ex.: infantaria sob helicoptero).
        if (moverDomain != blockerDomain || moverHeight != blockerHeight)
            return true;

        if (IsTotalWarEnabled())
        {
            // Total War: na mesma camada, o movimento pode atravessar unidades de
            // qualquer time. O bloqueio de "parar no hex com aliado" eh aplicado
            // na validacao de destino final.
            return true;
        }

        // Mesma camada: aliado atravessa; inimigo fica bloqueado por enquanto.
        return mover.TeamId == blocker.TeamId;
    }

    // Preparacao para combate/interacoes: verifica se existe alguma camada comum entre as unidades.
    public static bool HasAnySharedLayerMode(UnitManager a, UnitManager b)
    {
        if (a == null || b == null)
            return false;

        IReadOnlyList<UnitLayerMode> aModes = a.GetAllLayerModes();
        for (int i = 0; i < aModes.Count; i++)
        {
            UnitLayerMode mode = aModes[i];
            if (b.SupportsLayerMode(mode.domain, mode.heightLevel))
                return true;
        }

        return false;
    }

    public static bool IsTotalWarEnabled()
    {
        MatchController matchController = Object.FindAnyObjectByType<MatchController>();
        return matchController != null && matchController.EnableTotalWar;
    }
}
