using System;
using System.Collections.Generic;
using UnityEngine;

public struct AIBacklineSettings
{
    public int FrontBandWidth;
    public float DesiredRearGap;
    public int PaintRadius;
    public float ConeSpread;
    public int EnemyThreatRadius;
    public float EnemyThreatWeight;
    public int EnemyAvoidRange;
    public float AllyScreenStrength;
    public Func<Vector3Int, Vector2> CellToWorld;

    public static AIBacklineSettings Default => new AIBacklineSettings
    {
        FrontBandWidth = 3,
        DesiredRearGap = 2f,
        PaintRadius = 6,
        ConeSpread = 0.8f,
        EnemyThreatRadius = 3,
        EnemyThreatWeight = 120f,
        EnemyAvoidRange = 1,
        AllyScreenStrength = 0.6f
    };
}

public struct AIBacklineScore
{
    public float Score;
    public float Gap;
    public float Depth;
    public float LateralOffset;
    public float Threat;
    public float NearestFrontAlly;
    public bool IsVanguard;
    public bool InRearSlice;
}

public sealed class AIBacklineResult
{
    public bool Success;
    public string Error;
    public readonly List<Vector3Int> FrontBandCells = new List<Vector3Int>();
    public readonly Dictionary<Vector3Int, float> RearScoreMap = new Dictionary<Vector3Int, float>();
    public readonly HashSet<Vector3Int> VanguardCells = new HashSet<Vector3Int>();
    public float FrontBandDist;
    public Vector3Int Centroid;
    public float MaxRearScore = 1f;
    public Vector3Int BestRearCell;
    public bool HasBestRear;
    public Vector2 FrontCentroidWorld;
    public Vector2 Forward;
    public Vector2 Lateral;
    public float HexStep = 1f;
    public float LateralCenter;
    public float HalfWidth;
}

public static class AIBacklineAnalyzer
{
    public static AIBacklineResult Analyze(
        IReadOnlyList<Vector3Int> combatantCells,
        IReadOnlyList<Vector3Int> enemyCells,
        Vector3Int anchor,
        AIBacklineSettings settings,
        HashSet<Vector3Int> occupied = null)
    {
        var result = new AIBacklineResult();
        anchor.z = 0;

        if (combatantCells == null || combatantCells.Count == 0)
        {
            result.Error = "Nenhum combatente aliado em campo.";
            return result;
        }

        if (!TryBuildFrontGeometry(combatantCells, anchor, settings, result))
        {
            result.Error = "Faixa de vanguarda vazia.";
            return result;
        }

        occupied ??= new HashSet<Vector3Int>(combatantCells);

        for (int dx = -settings.PaintRadius; dx <= settings.PaintRadius; dx++)
        for (int dy = -settings.PaintRadius; dy <= settings.PaintRadius; dy++)
        {
            Vector3Int cell = new Vector3Int(result.Centroid.x + dx, result.Centroid.y + dy, 0);
            if (SectorManager.HexDistance(cell, result.Centroid) > settings.PaintRadius)
                continue;
            if (cell == anchor || occupied.Contains(cell))
                continue;

            AIBacklineScore score = ScoreCell(combatantCells, enemyCells, cell, anchor, settings, result);
            if (score.IsVanguard)
            {
                result.VanguardCells.Add(cell);
                continue;
            }

            if (!score.InRearSlice || IsWithinEnemyAvoidRange(cell, enemyCells, settings.EnemyAvoidRange))
                continue;

            if (score.Score <= 0f)
                continue;

            result.RearScoreMap[cell] = score.Score;
            if (!result.HasBestRear || score.Score > result.MaxRearScore)
            {
                result.MaxRearScore = score.Score;
                result.BestRearCell = cell;
                result.HasBestRear = true;
            }
        }

        result.Success = true;
        return result;
    }

    public static AIBacklineScore ScoreCell(
        IReadOnlyList<Vector3Int> combatantCells,
        IReadOnlyList<Vector3Int> enemyCells,
        Vector3Int cell,
        Vector3Int anchor,
        AIBacklineSettings settings)
    {
        var geometry = new AIBacklineResult();
        if (!TryBuildFrontGeometry(combatantCells, anchor, settings, geometry))
            return default;

        return ScoreCell(combatantCells, enemyCells, cell, anchor, settings, geometry);
    }

    public static AIBacklineScore ScoreCell(
        IReadOnlyList<Vector3Int> combatantCells,
        IReadOnlyList<Vector3Int> enemyCells,
        Vector3Int cell,
        Vector3Int anchor,
        AIBacklineSettings settings,
        AIBacklineResult geometry)
    {
        cell.z = 0;
        anchor.z = 0;

        Vector2 rel = ToWorld(cell, settings) - geometry.FrontCentroidWorld;
        float forward = Vector2.Dot(rel, geometry.Forward) / geometry.HexStep;
        float lateral = Vector2.Dot(rel, geometry.Lateral) / geometry.HexStep;

        var score = new AIBacklineScore
        {
            Gap = SectorManager.HexDistance(cell, anchor) - geometry.FrontBandDist,
            NearestFrontAlly = DistanceToNearest(cell, geometry.FrontBandCells)
        };

        if (forward > 0.5f)
        {
            score.IsVanguard = true;
            score.Score = -800f - forward * 220f;
            return score;
        }

        float depth = -forward;
        score.Depth = depth;
        if (depth < 0.5f)
        {
            score.Score = -160f - (0.5f - depth) * 120f;
            return score;
        }

        float lateralOffset = Mathf.Abs(lateral - geometry.LateralCenter);
        float allowedLateral = geometry.HalfWidth + settings.ConeSpread * depth;
        score.LateralOffset = lateralOffset;
        if (lateralOffset > allowedLateral)
        {
            score.Score = -Mathf.Min(420f, (lateralOffset - allowedLateral) * 180f);
            return score;
        }

        float threat = CalculateThreat(cell, enemyCells, combatantCells, settings);
        float depthError = Mathf.Abs(depth - settings.DesiredRearGap);
        float deepGapPenalty = Mathf.Max(0f, score.Gap - (settings.DesiredRearGap + 0.75f)) * 260f;
        float frontGapPenalty = Mathf.Max(0f, -score.Gap) * 420f;
        score.Threat = threat;
        score.InRearSlice = true;
        score.Score = 360f
            - depthError * 120f
            - lateralOffset * 45f
            - deepGapPenalty
            - frontGapPenalty
            - threat * settings.EnemyThreatWeight;
        return score;
    }

    private static bool TryBuildFrontGeometry(
        IReadOnlyList<Vector3Int> combatantCells,
        Vector3Int anchor,
        AIBacklineSettings settings,
        AIBacklineResult result)
    {
        if (combatantCells == null || combatantCells.Count == 0)
            return false;

        anchor.z = 0;
        int frontBandWidth = Mathf.Max(1, settings.FrontBandWidth);
        float frontDist = float.MaxValue;
        foreach (Vector3Int raw in combatantCells)
        {
            Vector3Int cell = raw;
            cell.z = 0;
            frontDist = Mathf.Min(frontDist, SectorManager.HexDistance(cell, anchor));
        }

        float bandSum = 0f;
        Vector3 centroidAcc = Vector3.zero;
        foreach (Vector3Int raw in combatantCells)
        {
            Vector3Int cell = raw;
            cell.z = 0;
            float dist = SectorManager.HexDistance(cell, anchor);
            if (dist > frontDist + frontBandWidth)
                continue;

            result.FrontBandCells.Add(cell);
            bandSum += dist;
            centroidAcc += new Vector3(cell.x, cell.y, 0);
        }

        if (result.FrontBandCells.Count == 0)
            return false;

        result.FrontBandDist = bandSum / result.FrontBandCells.Count;
        Vector3 mid = centroidAcc / result.FrontBandCells.Count;
        result.Centroid = new Vector3Int(Mathf.RoundToInt(mid.x), Mathf.RoundToInt(mid.y), 0);

        Vector2 frontCentroidWorld = Vector2.zero;
        foreach (Vector3Int cell in result.FrontBandCells)
            frontCentroidWorld += ToWorld(cell, settings);
        frontCentroidWorld /= result.FrontBandCells.Count;
        result.FrontCentroidWorld = frontCentroidWorld;

        Vector2 forward = ToWorld(anchor, settings) - frontCentroidWorld;
        result.Forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector2.up;
        result.Lateral = new Vector2(-result.Forward.y, result.Forward.x);

        result.HexStep = Vector2.Distance(ToWorld(result.Centroid, settings), ToWorld(result.Centroid + Vector3Int.right, settings));
        if (result.HexStep < 1e-4f)
            result.HexStep = 1f;

        float minLat = float.MaxValue;
        float maxLat = float.MinValue;
        foreach (Vector3Int cell in result.FrontBandCells)
        {
            float l = Vector2.Dot(ToWorld(cell, settings) - frontCentroidWorld, result.Lateral) / result.HexStep;
            minLat = Mathf.Min(minLat, l);
            maxLat = Mathf.Max(maxLat, l);
        }

        result.LateralCenter = (minLat + maxLat) * 0.5f;
        result.HalfWidth = (maxLat - minLat) * 0.5f + 0.75f;
        return true;
    }

    private static float CalculateThreat(
        Vector3Int cell,
        IReadOnlyList<Vector3Int> enemyCells,
        IReadOnlyList<Vector3Int> combatantCells,
        AIBacklineSettings settings)
    {
        if (settings.EnemyThreatRadius <= 0 || enemyCells == null)
            return 0f;

        float threat = 0f;
        foreach (Vector3Int rawEnemy in enemyCells)
        {
            Vector3Int enemy = rawEnemy;
            enemy.z = 0;
            float dist = SectorManager.HexDistance(cell, enemy);
            if (dist > settings.EnemyThreatRadius)
                continue;

            float baseThreat = settings.EnemyThreatRadius - dist + 1f;
            float shield = AllyScreenFactor(cell, enemy, dist, combatantCells);
            threat += baseThreat * Mathf.Clamp01(1f - shield * settings.AllyScreenStrength);
        }

        return threat;
    }

    private static float AllyScreenFactor(
        Vector3Int cell,
        Vector3Int enemy,
        float cellToEnemy,
        IReadOnlyList<Vector3Int> combatantCells)
    {
        if (combatantCells == null)
            return 0f;

        int screeners = 0;
        foreach (Vector3Int rawAlly in combatantCells)
        {
            Vector3Int ally = rawAlly;
            ally.z = 0;
            if (SectorManager.HexDistance(ally, enemy) >= cellToEnemy)
                continue;
            if (PerpendicularDistance(ally, cell, enemy) <= 1.2f)
                screeners++;
        }

        return screeners;
    }

    private static bool IsWithinEnemyAvoidRange(Vector3Int cell, IReadOnlyList<Vector3Int> enemyCells, int avoidRange)
    {
        if (avoidRange <= 0 || enemyCells == null)
            return false;

        foreach (Vector3Int rawEnemy in enemyCells)
        {
            Vector3Int enemy = rawEnemy;
            enemy.z = 0;
            if (SectorManager.HexDistance(cell, enemy) <= avoidRange)
                return true;
        }

        return false;
    }

    private static float DistanceToNearest(Vector3Int cell, IReadOnlyList<Vector3Int> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return float.MaxValue;

        float best = float.MaxValue;
        foreach (Vector3Int raw in candidates)
        {
            Vector3Int other = raw;
            other.z = 0;
            best = Mathf.Min(best, SectorManager.HexDistance(cell, other));
        }

        return best;
    }

    private static float PerpendicularDistance(Vector3Int point, Vector3Int lineStart, Vector3Int lineEnd)
    {
        Vector2 p = new Vector2(point.x, point.y);
        Vector2 a = new Vector2(lineStart.x, lineStart.y);
        Vector2 b = new Vector2(lineEnd.x, lineEnd.y);
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq <= 0.0001f)
            return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        return Vector2.Distance(p, a + ab * t);
    }

    private static Vector2 ToWorld(Vector3Int cell, AIBacklineSettings settings)
    {
        return settings.CellToWorld != null
            ? settings.CellToWorld(cell)
            : new Vector2(cell.x, cell.y);
    }
}
