using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum HeightBand
{
    Air,      // qualquer Domain.Air; altitude nao cria slot separado
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
    // Flag de rollout local (default on).
    public static bool EnableLayerOccupancyResolver = true;

    // Os tres andares do hexagono sao regra de TABULEIRO e valem em toda partida: aviao
    // sobrevoa tanque, submarino navega sob navio, tanque para no conves com navio embaixo.
    // Nada disso depende de nevoa — FOW e cobertura de INFORMACAO sobre o tabuleiro, nao um
    // modo de regras. Modos sem FOW (neblina leve, montanha, gameboy, fisica basica) apenas
    // revelam o tabuleiro; a ocupacao por camadas continua identica.
    public static bool IsLayerAwareRulesActive => EnableLayerOccupancyResolver;

    // O que E exclusivo do Total War: dois INIMIGOS dividirem a MESMA banda (hex disputado).
    // Fora dele, a mesma banda comporta uma presenca so, seja aliada ou inimiga.
    public static bool AllowsEnemyShareInSameBand => UnitRulesDefinition.IsTotalWarEnabled();

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

        // Conves da ponte separa terra e agua: nao ha o que bloquear entre quem anda em
        // cima e quem navega embaixo, nem sendo inimigos.
        if (DeckSeparatesFromWater(mover, blocker, cell))
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

        if (occupants == null)
            return true;

        HeightBand moverBand = GetHeightBand(mover);
        return CanEndMoveInBand(mover, moverBand, occupants, cell);
    }

    public static bool CanEndMoveAsLayer(
        UnitManager mover,
        Domain targetDomain,
        HeightLevel targetHeight,
        IEnumerable<UnitManager> occupants)
    {
        if (mover == null)
            return false;

        if (!IsLayerAwareRulesActive)
            return true;

        HeightBand targetBand = GetHeightBand(targetDomain, targetHeight);
        return CanEndMoveInBand(mover, targetBand, occupants);
    }

    // Conves da ponte: no hex marcado, Land/Surface e Naval/Surface deixam de ser o mesmo
    // andar. E a unica excecao ao "superficie e superficie" — em todo o resto do mapa terra
    // e mar ao nivel do mar disputam a mesma vaga, que e o que impede tanque e navio de
    // dividirem uma praia. Sobre a ponte existe conves em cima e agua embaixo, entao os dois
    // coexistem de fato.
    private static bool DeckSeparatesFromWater(UnitManager a, UnitManager b, Vector3Int? cell)
    {
        if (!cell.HasValue || a == null || b == null)
            return false;

        Domain domainA = a.GetDomain();
        Domain domainB = b.GetDomain();
        bool oneIsLandOtherIsNaval =
            (domainA == Domain.Land && domainB == Domain.Naval) ||
            (domainA == Domain.Naval && domainB == Domain.Land);
        if (!oneIsLandOtherIsNaval)
            return false;

        Tilemap map = a.BoardTilemap != null ? a.BoardTilemap : b.BoardTilemap;
        if (map == null)
            return false;

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(map, cell.Value);
        if (structure == null)
            return false;

        // Regra do PAR estrutura+terreno: a mesma ponte tem vao sobre o mar e encosta no
        // chao sobre a praia. So o par declara se ha conves separando terra de agua.
        TerrainTypeData terrain = ResolveTerrainAtCell(map, cell.Value);
        return structure.TryGetNavalOpsRuleForTerrain(terrain, out StructureNavalOpsTerrainRule rule)
            && rule != null
            && rule.separaConvesEAgua;
    }

    private static TerrainDatabase cachedTerrainDatabase;

    private static TerrainTypeData ResolveTerrainAtCell(Tilemap map, Vector3Int cell)
    {
        if (map == null)
            return null;

        if (cachedTerrainDatabase == null)
        {
            TurnStateManager turnState = Object.FindAnyObjectByType<TurnStateManager>();
            if (turnState != null)
                cachedTerrainDatabase = turnState.TerrainDatabaseRef;

            if (cachedTerrainDatabase == null)
            {
                MatchController match = Object.FindAnyObjectByType<MatchController>();
                if (match != null)
                    cachedTerrainDatabase = match.TerrainDatabaseRef;
            }
        }

        if (cachedTerrainDatabase == null)
            return null;

        cell.z = 0;
        TileBase tile = map.GetTile(cell);
        if (tile != null && cachedTerrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData terrain))
            return terrain;

        return null;
    }

    private static bool CanEndMoveInBand(
        UnitManager mover,
        HeightBand moverBand,
        IEnumerable<UnitManager> occupants,
        Vector3Int? cell = null)
    {
        if (occupants == null)
            return true;

        foreach (UnitManager occupant in occupants)
        {
            if (occupant == null || occupant == mover)
                continue;
            if (GetHeightBand(occupant) != moverBand)
                continue;
            if (moverBand == HeightBand.Blocking && DeckSeparatesFromWater(mover, occupant, cell))
                continue;

            // Vale para todas as bandas (Blocking, Air, Sub). Em Air,
            // AirLow e AirHigh compartilham o mesmo slot por dominio/time.
            // - aliado nunca compartilha o hex final na mesma banda;
            // - inimigo so compartilha sob Total War (hex disputado/dogfight).
            if (PlayerSlotRelations.AreAllies(occupant, mover))
                return false;

            if (!AllowsEnemyShareInSameBand)
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

    public static bool CanEndLayerTransition(UnitManager unit, Domain targetDomain, HeightLevel targetHeight, IEnumerable<UnitManager> occupants)
    {
        if (unit == null)
            return false;

        if (!IsLayerAwareRulesActive)
            return true;

        if (occupants == null)
            return true;

        HeightBand targetBand = GetHeightBand(targetDomain, targetHeight);

        foreach (UnitManager occupant in occupants)
        {
            if (occupant == null || occupant == unit)
                continue;
            if (GetHeightBand(occupant) != targetBand)
                continue;

            // Transicao para camada bloqueante e mais restrita que movimento:
            // qualquer ocupante (aliado ou inimigo) impede pousar/trocar para ela.
            if (targetBand == HeightBand.Blocking)
                return false;

            // Air/Sub: aliado bloqueia (sem empilhar mesmo time);
            // inimigo coexiste -> decola/transiciona para hex contestado.
            if (PlayerSlotRelations.AreAllies(occupant, unit))
                return false;
        }

        return true;
    }

    public static HeightBand GetHeightBand(Domain domain, HeightLevel height)
    {
        if (domain == Domain.Air || height == HeightLevel.AirLow || height == HeightLevel.AirHigh)
            return HeightBand.Air;

        if (domain == Domain.Submarine || height == HeightLevel.Submerged)
            return HeightBand.Sub;

        return HeightBand.Blocking;
    }
}
