using System;
using UnityEngine;

[Serializable]
public class RallyPoint
{
    public int id;
    public string nome = string.Empty;
    public Vector2Int hexDestino;
    public int teamOwner;
    public bool ativo;
}

[Serializable]
public class RallyAssignment
{
    public int rallyPointId;
    public int unitId;
}

[Serializable]
public class PlanningConfig
{
    public int maxRallyPointsPerTeam = 5;
}

public enum RallyAssignmentDecision
{
    None = 0,
    Moved = 1,
    Skipped = 2,
    Removed = 3
}
