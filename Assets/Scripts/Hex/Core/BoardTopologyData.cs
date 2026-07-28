using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BoardTopologyCellRecord
{
    public Vector3Int cell;
    public TerrainTypeData terrain;
    public StructureData structure;
    public ConstructionData construction;
    public List<Vector3Int> neighbors = new List<Vector3Int>(6);
    public bool hasAnyPaintedTile;
    public bool isBeach;
    public bool isCoastal;
    public bool isPotentialLandingSurface;
    public bool isPotentialEmbarkCell;
    public bool isPotentialDisembarkCell;

    [SerializeField, HideInInspector]
    private string sourceTileSignature = string.Empty;

    public string SourceTileSignature => sourceTileSignature ?? string.Empty;

    public void SetSourceTileSignature(string value)
    {
        sourceTileSignature = value ?? string.Empty;
    }
}

[Serializable]
public sealed class BoardTopologyRouteEdgeRecord
{
    public Vector3Int from;
    public Vector3Int to;
    public StructureData structure;
    public string routeName;

    public BoardTopologyEdgeKey EdgeKey =>
        new BoardTopologyEdgeKey(from, to);
}

public readonly struct BoardTopologyEdgeKey :
    IEquatable<BoardTopologyEdgeKey>
{
    public readonly Vector3Int a;
    public readonly Vector3Int b;

    public BoardTopologyEdgeKey(Vector3Int first, Vector3Int second)
    {
        first.z = 0;
        second.z = 0;
        if (CompareCells(first, second) <= 0)
        {
            a = first;
            b = second;
        }
        else
        {
            a = second;
            b = first;
        }
    }

    public bool Equals(BoardTopologyEdgeKey other)
    {
        return a == other.a && b == other.b;
    }

    public override bool Equals(object obj)
    {
        return obj is BoardTopologyEdgeKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (a.GetHashCode() * 397) ^ b.GetHashCode();
        }
    }

    private static int CompareCells(Vector3Int left, Vector3Int right)
    {
        int x = left.x.CompareTo(right.x);
        if (x != 0)
            return x;
        return left.y.CompareTo(right.y);
    }
}

public sealed class BoardTopologyValidationReport
{
    public readonly List<string> errors = new List<string>();
    public readonly List<string> warnings = new List<string>();

    public bool IsValid => errors.Count == 0;

    public void AddError(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            errors.Add(message);
    }

    public void AddWarning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            warnings.Add(message);
    }

    public void Merge(BoardTopologyValidationReport other)
    {
        if (other == null)
            return;
        errors.AddRange(other.errors);
        warnings.AddRange(other.warnings);
    }

    public string Format(string title)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("[BoardTopology] ")
            .Append(string.IsNullOrWhiteSpace(title)
                ? "Validation"
                : title)
            .Append(": ")
            .Append(errors.Count)
            .Append(" error(s), ")
            .Append(warnings.Count)
            .Append(" warning(s).");

        for (int i = 0; i < errors.Count; i++)
            builder.Append("\n  ERROR: ").Append(errors[i]);
        for (int i = 0; i < warnings.Count; i++)
            builder.Append("\n  WARN: ").Append(warnings[i]);
        return builder.ToString();
    }
}

internal sealed class BoardTopologyBuildResult
{
    public string mapId = string.Empty;
    public string fingerprint = string.Empty;
    public readonly List<BoardTopologyCellRecord> cells =
        new List<BoardTopologyCellRecord>();
    public readonly List<BoardTopologyRouteEdgeRecord> routeEdges =
        new List<BoardTopologyRouteEdgeRecord>();
    public readonly BoardTopologyValidationReport validation =
        new BoardTopologyValidationReport();
}
