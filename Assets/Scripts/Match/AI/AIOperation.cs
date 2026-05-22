using System.Collections.Generic;
using UnityEngine;

public enum AINeedKind
{
    Capturer,
    Assault,
    FireSupport,
    Artillery,
    AAA,
    SAM,
    GroundTransport,
    AirTransport,
    FighterB,
    FighterA,
    Apache,
}

public enum AIOperationType
{
    BaseDefense,
    SectorDefense,
    GroundCapture,
    AirliftCapture,
    AirInterception,
    PreventiveDefense,
    Reserve,
}

public enum AIOperationPhase
{
    Forming,
    Moving,
    Engaging,
    Capturing,
    Holding,
    Complete,
    Aborted,
}

public class AIOperationSlotNeed
{
    public AINeedKind Kind;
    public bool Filled;
    public int AssignedUnitId = -1;
}

public class AIOperation
{
    public int Id;
    public AIOperationType Type;
    public AIOperationPhase Phase;
    public ConstructionSector Sector;
    public TeamId Team;
    public int Priority;

    public Vector3Int AnchorCell;
    public Vector3Int TargetCell;

    public List<AIOperationSlotNeed> RequiredSlots = new List<AIOperationSlotNeed>();
    public List<int> AssignedUnitIds = new List<int>();

    public bool IsUrgent;
    public bool IsPreventive;
    public int CreatedTurn;
    public int LastUpdatedTurn;

    public SectorObjective LinkedObjective;

    public int CountOpenSlots(AINeedKind kind)
    {
        int count = 0;
        foreach (AIOperationSlotNeed slot in RequiredSlots)
            if (slot.Kind == kind && !slot.Filled)
                count++;
        return count;
    }

    public bool HasOpenSlot(AINeedKind kind)
    {
        foreach (AIOperationSlotNeed slot in RequiredSlots)
            if (slot.Kind == kind && !slot.Filled)
                return true;
        return false;
    }

    public void AddSlots(AINeedKind kind, int count)
    {
        for (int i = 0; i < count; i++)
            RequiredSlots.Add(new AIOperationSlotNeed { Kind = kind });
    }
}

public struct OperationDeficit
{
    public AIOperation Operation;
    public AINeedKind Kind;
    public int Count;
}
