using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// SERVICO BURRO, e o mais burro de todos: geometria de grade hexagonal.
/// Converte entre coordenada de offset e cubo, mede distancia, interpola e
/// arredonda. Nao sabe o que e unidade, visao, deteccao, terreno ou time — nao
/// abre o TerrainDatabase, nao pergunta quem esta no hex.
///
/// Estava dentro do PodeDetectar porque foi ali que a primeira linha de visada
/// precisou dela. Nao era dele: e do tabuleiro, e tanto o PodeEnxergar quanto o
/// PodeDetectar precisam.
/// </summary>
public static class HexGridGeometry
{
    /// <summary>
    /// Coordenada cubica. Em ponto flutuante de proposito: a interpolacao entre
    /// dois hexes passa por posicoes fracionarias antes do arredondamento.
    /// </summary>
    public readonly struct CubeCoord
    {
        public readonly float x;
        public readonly float y;
        public readonly float z;

        public CubeCoord(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    private static readonly Dictionary<int, bool> oddRowOffsetCache =
        new Dictionary<int, bool>();

    public static void ClearCaches()
    {
        oddRowOffsetCache.Clear();
    }

    public static Vector2 ToWorld2(Vector3 world)
    {
        return new Vector2(world.x, world.y);
    }

    /// <summary>
    /// Descobre, pela forma real da vizinhanca do tilemap, se a grade desloca
    /// as linhas impares ou as pares. Perguntar ao mapa em vez de assumir e o
    /// que permite trocar o layout sem reescrever a geometria. Cacheado por
    /// tilemap porque a resposta nao muda em tempo de execucao.
    /// </summary>
    public static bool TryResolveOddRowOffset(Tilemap tilemap, out bool oddRowOffset)
    {
        oddRowOffset = true;
        if (tilemap == null)
            return false;

        int tilemapId = tilemap.GetEntityId().GetHashCode();
        if (oddRowOffsetCache.TryGetValue(tilemapId, out bool cached))
        {
            oddRowOffset = cached;
            return true;
        }

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, Vector3Int.zero, neighbors);
        if (neighbors.Count <= 0)
            return false;

        int oddScore = 0;
        int evenScore = 0;
        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int n = neighbors[i];
            if (IsExpectedNeighborForOddRowOffsetEvenRow(n))
                oddScore++;
            if (IsExpectedNeighborForEvenRowOffsetEvenRow(n))
                evenScore++;
        }

        oddRowOffset = oddScore >= evenScore;
        oddRowOffsetCache[tilemapId] = oddRowOffset;
        return true;
    }

    private static bool IsExpectedNeighborForOddRowOffsetEvenRow(Vector3Int cell)
    {
        return (cell.x == 1 && cell.y == 0) ||
               (cell.x == -1 && cell.y == 0) ||
               (cell.x == 0 && cell.y == -1) ||
               (cell.x == -1 && cell.y == -1) ||
               (cell.x == 0 && cell.y == 1) ||
               (cell.x == -1 && cell.y == 1);
    }

    private static bool IsExpectedNeighborForEvenRowOffsetEvenRow(Vector3Int cell)
    {
        return (cell.x == 1 && cell.y == 0) ||
               (cell.x == -1 && cell.y == 0) ||
               (cell.x == 1 && cell.y == -1) ||
               (cell.x == 0 && cell.y == -1) ||
               (cell.x == 1 && cell.y == 1) ||
               (cell.x == 0 && cell.y == 1);
    }

    public static CubeCoord OffsetToCube(Vector3Int cell, bool oddRowOffset)
    {
        int row = cell.y;
        int rowParity = Mathf.Abs(row) & 1;
        float q = oddRowOffset
            ? cell.x - ((row - rowParity) / 2f)
            : cell.x - ((row + rowParity) / 2f);
        float r = row;
        float x = q;
        float z = r;
        float y = -x - z;
        return new CubeCoord(x, y, z);
    }

    public static Vector3Int CubeToOffset(CubeCoord cube, bool oddRowOffset)
    {
        int q = Mathf.RoundToInt(cube.x);
        int r = Mathf.RoundToInt(cube.z);
        int rowParity = Mathf.Abs(r) & 1;
        int col = oddRowOffset
            ? q + ((r - rowParity) / 2)
            : q + ((r + rowParity) / 2);
        return new Vector3Int(col, r, 0);
    }

    public static int CubeDistance(CubeCoord a, CubeCoord b)
    {
        float dx = Mathf.Abs(a.x - b.x);
        float dy = Mathf.Abs(a.y - b.y);
        float dz = Mathf.Abs(a.z - b.z);
        return Mathf.RoundToInt(Mathf.Max(dx, Mathf.Max(dy, dz)));
    }

    public static CubeCoord CubeLerp(CubeCoord a, CubeCoord b, float t)
    {
        return new CubeCoord(
            Mathf.Lerp(a.x, b.x, t),
            Mathf.Lerp(a.y, b.y, t),
            Mathf.Lerp(a.z, b.z, t));
    }

    /// <summary>
    /// Arredonda a coordenada fracionaria para o hex real, corrigindo o eixo de
    /// maior erro para a soma continuar zero — sem isso a interpolacao escorrega
    /// para hexes que a reta nao cruza.
    /// </summary>
    public static CubeCoord CubeRound(CubeCoord fractional)
    {
        float rx = Mathf.Round(fractional.x);
        float ry = Mathf.Round(fractional.y);
        float rz = Mathf.Round(fractional.z);

        float xDiff = Mathf.Abs(rx - fractional.x);
        float yDiff = Mathf.Abs(ry - fractional.y);
        float zDiff = Mathf.Abs(rz - fractional.z);

        if (xDiff > yDiff && xDiff > zDiff)
            rx = -ry - rz;
        else if (yDiff > zDiff)
            ry = -rx - rz;
        else
            rz = -rx - ry;

        return new CubeCoord(rx, ry, rz);
    }
}
