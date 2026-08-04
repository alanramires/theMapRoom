using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// SERVICO BURRO: a linha entre duas celulas do tabuleiro.
///
/// Sai do EV de onde o observador esta, chega ao EV da celula alvo, e responde
/// se a reta passa. Nao sabe de alcance, chave de deteccao, metodo,
/// especializacao, furtividade nem time — nada disso e pergunta de geometria.
///
/// Existe para que as duas verdades — PodeEnxergar, que revela hexagonos, e
/// PodeDetectar, que faz unidades aparecerem — usem a MESMA linha sem uma
/// depender da outra. Consome dois servicos abaixo dele: ObservationCellService
/// para o fato da celula e HexGridGeometry para a grade.
/// </summary>
/// <summary>
/// De onde a linha parte. E decisao de quem pergunta, nao da geometria.
/// </summary>
public enum OriginEvRule
{
    /// <summary>
    /// Observacao — revelar hexagono e detectar unidade. A unidade herda o EV
    /// do terreno onde esta: o soldado na montanha observa de 2.
    /// </summary>
    InheritTerrain = 0,

    /// <summary>
    /// Linha de tiro. Herda o EV do terreno apenas quando o terreno autoriza o
    /// atirador (shooterInheritsTerrainEv), com o override quando houver.
    ///
    /// E o que permite a bazuca de alcance 2 na montanha acertar quem esta
    /// atras da floresta: ela parte de 2 e passa por cima do EV 1 da arvore.
    /// Fora dos terrenos que autorizam, o atirador parte do EV da camada.
    /// </summary>
    ShooterInheritsWhenTerrainAllows = 1
}

public static class ObservationLineService
{
    /// <summary>
    /// Margem de raspao. Um bloqueador so detem a linha quando esta ACIMA dela
    /// por mais que isto; empate passa. E o que faz a linha nivelada de EV 0
    /// atravessar planicie, praia e mar sem ser detida por eles.
    /// </summary>
    public const float LosGrazeEpsilon = 0.05f;

    /// <summary>
    /// Instrumentacao. Publica porque quem reporta e o consumidor; o servico so
    /// conta o que fez.
    /// </summary>
    public static double LerpMs;
    public static int LerpCells;

    public static void ResetCounters()
    {
        LerpMs = 0d;
        LerpCells = 0;
    }

    /// <summary>
    /// Percorre a linha de <paramref name="originCell"/> ate
    /// <paramref name="targetCell"/> e diz se ela chega.
    ///
    /// A altura da linha em cada hex cruzado vem da distancia REAL projetada do
    /// centro do hex sobre a reta, nao do indice na lista: a duplicacao de hexes
    /// na fronteira entre dois hexagonos distorceria o "t" e dois hexes
    /// empatados na mesma distancia ganhariam limiares diferentes.
    ///
    /// Bloqueia quem tem blockLoS e EV MAIOR que a linha naquele ponto. EV 0
    /// nunca bloqueia, e empate passa.
    /// </summary>
    public static bool TryTrace(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int originCell,
        Vector3Int targetCell,
        UnitManager observer,
        UnitManager target,
        DPQAirHeightConfig dpqAirHeightConfig,
        out List<Vector3Int> intermediateCells,
        out List<float> evPath,
        out Vector3Int blockedCell,
        bool enableLosValidation,
        Domain? forcedTargetDomain = null,
        HeightLevel? forcedTargetHeightLevel = null,
        OriginEvRule rule = OriginEvRule.InheritTerrain)
    {
        intermediateCells = new List<Vector3Int>();
        evPath = new List<float>();
        blockedCell = Vector3Int.zero;
        if (tilemap == null)
            return false;

        if (!ObservationCellService.TryResolveCellVision(
                tilemap,
                terrainDatabase,
                originCell,
                observer,
                dpqAirHeightConfig,
                out float originEv,
                out _))
        {
            originEv = 0;
        }

        // De onde a linha parte e decisao de QUEM PERGUNTA, nao da geometria.
        // Observar e atirar usam a MESMA reta e regras de origem diferentes; por
        // isso a origem e uma regra nomeada, e nao uma segunda implementacao.
        originEv = ResolveOriginEv(
            tilemap,
            terrainDatabase,
            originCell,
            observer,
            dpqAirHeightConfig,
            originEv,
            rule);

        if (!forcedTargetDomain.HasValue &&
            !forcedTargetHeightLevel.HasValue &&
            target == null &&
            ObservationCellService.TryResolveObservationLayer(
                tilemap,
                terrainDatabase,
                targetCell,
                out Domain resolvedTargetDomain,
                out HeightLevel resolvedTargetHeightLevel,
                useOccupantLayerForTarget: false))
        {
            forcedTargetDomain = resolvedTargetDomain;
            forcedTargetHeightLevel = resolvedTargetHeightLevel;
        }

        if (!ObservationCellService.TryResolveCellVision(
                tilemap,
                terrainDatabase,
                targetCell,
                target,
                dpqAirHeightConfig,
                out float targetEv,
                out _,
                forcedDomain: forcedTargetDomain,
                forcedHeightLevel: forcedTargetHeightLevel))
        {
            targetEv = 0;
        }

        evPath.Add(originEv);
        double lerpStartMs = Time.realtimeSinceStartupAsDouble;
        List<Vector3Int> crossedCells = GetIntermediateCellsByCellLerp(
            tilemap,
            originCell,
            targetCell);
        LerpMs += (Time.realtimeSinceStartupAsDouble - lerpStartMs) * 1000d;
        LerpCells += crossedCells.Count;
        intermediateCells.AddRange(crossedCells);

        Vector2 losOriginWorld2 = HexGridGeometry.ToWorld2(
            tilemap.GetCellCenterWorld(originCell));
        Vector2 losTargetWorld2 = HexGridGeometry.ToWorld2(
            tilemap.GetCellCenterWorld(targetCell));
        Vector2 losDir = losTargetWorld2 - losOriginWorld2;
        float losLenSq = Vector2.Dot(losDir, losDir);

        for (int i = 0; i < crossedCells.Count; i++)
        {
            Vector3Int cell = crossedCells[i];
            float t = losLenSq > 0.0001f
                ? Mathf.Clamp01(Vector2.Dot(
                    HexGridGeometry.ToWorld2(tilemap.GetCellCenterWorld(cell)) - losOriginWorld2,
                    losDir) / losLenSq)
                : (i + 1f) / (crossedCells.Count + 1f);
            float losHeightAtCell = Mathf.Lerp(originEv, targetEv, t);
            evPath.Add(losHeightAtCell);

            if (!enableLosValidation)
                continue;

            if (!ObservationCellService.TryResolveCellVision(
                    tilemap,
                    terrainDatabase,
                    cell,
                    null,
                    dpqAirHeightConfig,
                    out float cellEv,
                    out bool cellBlocksLoS,
                    forcedDomain: forcedTargetDomain,
                    forcedHeightLevel: forcedTargetHeightLevel))
            {
                continue;
            }

            if (!cellBlocksLoS || cellEv <= 0)
                continue;

            if (cellEv > losHeightAtCell + LosGrazeEpsilon)
            {
                blockedCell = cell;
                return false;
            }
        }

        evPath.Add(targetEv);
        return true;
    }

    /// <summary>
    /// De que altura a linha parte.
    ///
    /// Camada que nao e terreno — ar e submerso — sempre consulta a politica do
    /// DPQ Air Height Config. Sem clamp em zero de proposito: se um dia o
    /// submerso for -1, a linha sobe em vez de descer, e isso e decisao do dado.
    ///
    /// Sobre terreno, <paramref name="rule"/> decide (ver OriginEvRule).
    /// </summary>
    public static float ResolveOriginEv(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int originCell,
        UnitManager observer,
        DPQAirHeightConfig dpqAirHeightConfig,
        float fallbackEv,
        OriginEvRule rule = OriginEvRule.InheritTerrain)
    {
        if (observer == null)
            return Mathf.Max(0f, fallbackEv);

        Domain domain = observer.GetDomain();
        HeightLevel height = observer.GetHeightLevel();

        if (domain == Domain.Air ||
            (domain == Domain.Submarine && height == HeightLevel.Submerged))
        {
            if (dpqAirHeightConfig != null &&
                dpqAirHeightConfig.TryGetVisionFor(domain, height, out int layerEv, out _))
            {
                return layerEv;
            }

            return fallbackEv;
        }

        originCell.z = 0;
        if (tilemap != null &&
            terrainDatabase != null &&
            ObservationCellService.TryResolveTerrain(
                tilemap,
                terrainDatabase,
                originCell,
                out TerrainTypeData originTerrain) &&
            originTerrain != null)
        {
            if (rule == OriginEvRule.InheritTerrain)
                return originTerrain.ev;

            return originTerrain.shooterInheritsTerrainEv
                ? originTerrain.ResolveShooterInheritedEv()
                : 0;
        }

        return 0;
    }

    /// <summary>
    /// Hexes cruzados pela reta, por supersampling com expansao apenas em
    /// fronteira ambigua.
    ///
    /// O traçado por cube-line escolhe UM caminho em diagonais e empates, e por
    /// isso deixa passar bloqueio por relevo entre dois hexes. Aqui, quando a
    /// amostra cai equidistante de dois centros, os dois entram.
    /// </summary>
    public static List<Vector3Int> GetIntermediateCellsByCellLerp(
        Tilemap tilemap,
        Vector3Int originCell,
        Vector3Int targetCell)
    {
        List<Vector3Int> cells = new List<Vector3Int>();

        originCell.z = 0;
        targetCell.z = 0;
        if (tilemap == null)
            return cells;

        Vector3 originWorld = tilemap.GetCellCenterWorld(originCell);
        Vector3 targetWorld = tilemap.GetCellCenterWorld(targetCell);
        Vector2 originWorld2 = new Vector2(originWorld.x, originWorld.y);
        Vector2 targetWorld2 = new Vector2(targetWorld.x, targetWorld.y);
        float neighborStep = 1f;
        List<Vector3Int> originNeighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, originCell, originNeighbors);
        if (originNeighbors.Count > 0)
        {
            Vector3 n = tilemap.GetCellCenterWorld(originNeighbors[0]);
            neighborStep = Vector2.Distance(originWorld2, new Vector2(n.x, n.y));
            if (neighborStep <= 0.0001f)
                neighborStep = 1f;
        }

        float worldDistance = Vector2.Distance(originWorld2, targetWorld2);
        if (worldDistance <= 0.0001f)
            return cells;

        int approxHexes = Mathf.Max(1, Mathf.CeilToInt(worldDistance / Mathf.Max(0.0001f, neighborStep)));
        if (approxHexes <= 1)
            return cells;

        float borderEpsilon = Mathf.Max(0.01f, neighborStep * 0.08f);
        int sampleCount = approxHexes * 10;
        if (sampleCount <= 1)
            sampleCount = approxHexes * 6;

        HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        List<Vector3Int> centerNeighbors = new List<Vector3Int>(6);
        for (int i = 1; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector2 sample2 = Vector2.Lerp(originWorld2, targetWorld2, t);

            Vector3Int centerCell = tilemap.WorldToCell(new Vector3(sample2.x, sample2.y, 0f));
            centerCell.z = 0;
            if (centerCell != originCell && centerCell != targetCell && seen.Add(centerCell))
                cells.Add(centerCell);

            Vector2 centerWorld2 = HexGridGeometry.ToWorld2(tilemap.GetCellCenterWorld(centerCell));
            float distToCenter = Vector2.Distance(sample2, centerWorld2);

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, centerCell, centerNeighbors);
            for (int n = 0; n < centerNeighbors.Count; n++)
            {
                Vector3Int neighborCell = centerNeighbors[n];
                neighborCell.z = 0;
                if (neighborCell == originCell || neighborCell == targetCell)
                    continue;

                Vector2 neighborWorld2 = HexGridGeometry.ToWorld2(
                    tilemap.GetCellCenterWorld(neighborCell));
                float distToNeighbor = Vector2.Distance(sample2, neighborWorld2);
                if (Mathf.Abs(distToCenter - distToNeighbor) > borderEpsilon)
                    continue;

                if (seen.Add(neighborCell))
                    cells.Add(neighborCell);
            }
        }

        return cells;
    }

    /// <summary>
    /// Traçado alternativo por linha cubica. Mais barato e mais estreito: em
    /// diagonais e empates ele escolhe um caminho so. Nao e o usado pela
    /// TryTrace justamente por isso.
    /// </summary>
    public static List<Vector3Int> GetIntermediateCellsByCubeLine(
        Tilemap tilemap,
        Vector3Int originCell,
        Vector3Int targetCell)
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        if (tilemap == null)
            return cells;

        originCell.z = 0;
        targetCell.z = 0;
        if (originCell == targetCell)
            return cells;

        if (!HexGridGeometry.TryResolveOddRowOffset(tilemap, out bool oddRowOffset))
            return null;

        HexGridGeometry.CubeCoord originCube =
            HexGridGeometry.OffsetToCube(originCell, oddRowOffset);
        HexGridGeometry.CubeCoord targetCube =
            HexGridGeometry.OffsetToCube(targetCell, oddRowOffset);
        int steps = HexGridGeometry.CubeDistance(originCube, targetCube);
        if (steps <= 1)
            return cells;

        HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        for (int i = 1; i < steps; i++)
        {
            float t = i / (float)steps;
            HexGridGeometry.CubeCoord lerped =
                HexGridGeometry.CubeLerp(originCube, targetCube, t);
            HexGridGeometry.CubeCoord rounded = HexGridGeometry.CubeRound(lerped);
            Vector3Int cell = HexGridGeometry.CubeToOffset(rounded, oddRowOffset);
            cell.z = 0;
            if (cell == originCell || cell == targetCell)
                continue;
            if (seen.Add(cell))
                cells.Add(cell);
        }

        return cells;
    }
}
