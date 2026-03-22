using System.Collections.Generic;
using UnityEngine;

public enum HeightBand
{
    Air,      // air/low, air/high
    Sub,      // naval/submerged
    Blocking  // land/surface, naval/surface
}

public struct LayerOccupancyKey
{
    public Vector3Int Cell;
    public Domain Domain;
    public HeightBand HeightBand;
}

public static class OccupancyResolver
{
    // Flag de rollout local (default on). Regras layer-aware so ativam quando Total War estiver ativo.
    public static bool EnableLayerOccupancyResolver = true;
    public static bool IsLayerAwareRulesActive => EnableLayerOccupancyResolver && UnitRulesDefinition.IsTotalWarEnabled();

    public static HeightBand GetHeightBand(UnitManager unit)
    {
        if (unit == null)
            return HeightBand.Blocking;

        Domain domain = unit.GetDomain();
        HeightLevel height = unit.GetHeightLevel();
        return GetHeightBand(domain, height);
    }

    public static bool IsBlockingLayer(UnitManager unit)
    {
        return GetHeightBand(unit) == HeightBand.Blocking;
    }

    public static bool CanPassThrough(UnitManager mover, UnitManager blocker, Vector3Int cell)
    {
        if (blocker == null)
            return true;
        if (mover == null)
            return false;

        // Regra global: aliado nunca bloqueia travessia de path.
        // O bloqueio para terminar no mesmo hex fica em CanEndMove.
        if (AreFriendlyForPathTraversal(mover.TeamId, blocker.TeamId))
            return true;

        if (!IsLayerAwareRulesActive)
            return UnitRulesDefinition.CanPassThrough(mover, blocker);

        HeightBand moverBand = GetHeightBand(mover);
        HeightBand blockerBand = GetHeightBand(blocker);

        // Camadas diferentes sempre cruzam.
        if (moverBand != blockerBand)
            return true;

        // Camadas nao bloqueantes sempre cruzam.
        if (moverBand != HeightBand.Blocking)
            return true;

        // Mesma camada bloqueante + inimigo: sempre bloqueia passagem.
        // Total War impacta apenas regra de termino de movimento (CanEndMove).
        return false;
    }

    private static bool AreFriendlyForPathTraversal(TeamId moverTeam, TeamId blockerTeam)
    {
        return moverTeam == blockerTeam;
    }

    public static bool CanEndMove(UnitManager mover, Vector3Int cell, IEnumerable<UnitManager> occupants)
    {
        if (mover == null)
            return false;

        if (!IsLayerAwareRulesActive)
            return true;

        HeightBand moverBand = GetHeightBand(mover);
        if (moverBand != HeightBand.Blocking)
            return true;

        if (occupants == null)
            return true;

        foreach (UnitManager occupant in occupants)
        {
            if (occupant == null || occupant == mover)
                continue;
            if (GetHeightBand(occupant) != moverBand)
                continue;

            // Em camada bloqueante:
            // - aliado nunca pode compartilhar hex final;
            // - inimigo segue regra Total War (aqui ativo por IsLayerAwareRulesActive).
            if (occupant.TeamId == mover.TeamId)
                return false;
        }

        return true;
    }

    public static bool CanEnter(UnitManager unit, Vector3Int cell, IEnumerable<UnitManager> occupants)
    {
        if (unit == null)
            return false;

        if (!IsLayerAwareRulesActive)
            return true;

        // Semantica inicial: entrada usa a mesma regra de termino.
        return CanEndMove(unit, cell, occupants);
    }

    private static HeightBand GetHeightBand(Domain domain, HeightLevel height)
    {
        if (domain == Domain.Air || height == HeightLevel.AirLow || height == HeightLevel.AirHigh)
            return HeightBand.Air;

        if (domain == Domain.Submarine || height == HeightLevel.Submerged)
            return HeightBand.Sub;

        return HeightBand.Blocking;
    }
}
