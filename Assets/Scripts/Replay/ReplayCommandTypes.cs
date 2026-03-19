using System;
using System.Collections.Generic;
using UnityEngine;

public interface IReplayCommand
{
    string DebugLabel { get; }
    ReplayStepType StepType { get; }
    void Execute(ReplayExecutionContext context);
}

public enum ReplayStepType
{
    MoveUnit,
    Attack,
    Capture,
    BuyUnit,
    Embark,
    Disembark,
    EndTurn,
    Merge,
    Supply,
    Custom
}

[Serializable]
public class ReplayStep
{
    public int StepIndex;
    public ReplayStepType StepType;
    [NonSerialized] public IReplayCommand Command;
    public string DebugLabel;
}

public enum CinematicAction
{
    None,
    Confirm,
    AimAction
}

[Serializable]
public class CinematicEvent
{
    public Vector3Int CursorHex;
    public CinematicAction Action;
    public float DelayAfter;
}

[Serializable]
public class CinematicTrack
{
    public List<CinematicEvent> Events = new List<CinematicEvent>();
}

[Serializable]
public class MoveUnitReplayCommand : IReplayCommand
{
    public string DebugLabel => debugLabel;
    public ReplayStepType StepType => ReplayStepType.MoveUnit;

    public string debugLabel;

    public void Execute(ReplayExecutionContext context)
    {
        // Legacy placeholder command: actual replay path currently uses action snapshots.
    }
}

[Serializable]
public class AttackReplayCommand : IReplayCommand
{
    public string DebugLabel => debugLabel;
    public ReplayStepType StepType => ReplayStepType.Attack;

    public string debugLabel;
    public CinematicTrack CinematicTrack;

    public void Execute(ReplayExecutionContext context)
    {
        // Legacy placeholder command: actual replay path currently uses action snapshots.
    }
}

[Serializable]
public class BuyUnitReplayCommand : IReplayCommand
{
    public string DebugLabel => debugLabel;
    public ReplayStepType StepType => ReplayStepType.BuyUnit;

    public string debugLabel;

    public void Execute(ReplayExecutionContext context) { }
}

[Serializable]
public class CaptureReplayCommand : IReplayCommand
{
    public string DebugLabel => debugLabel;
    public ReplayStepType StepType => ReplayStepType.Capture;

    public string debugLabel;

    public void Execute(ReplayExecutionContext context) { }
}

[Serializable]
public class EmbarkReplayCommand : IReplayCommand
{
    public string DebugLabel => debugLabel;
    public ReplayStepType StepType => ReplayStepType.Embark;

    public string debugLabel;

    public void Execute(ReplayExecutionContext context) { }
}

[Serializable]
public class DisembarkReplayCommand : IReplayCommand
{
    public string DebugLabel => debugLabel;
    public ReplayStepType StepType => ReplayStepType.Disembark;

    public string debugLabel;

    public void Execute(ReplayExecutionContext context) { }
}

[Serializable]
public class MergeReplayCommand : IReplayCommand
{
    public string DebugLabel => debugLabel;
    public ReplayStepType StepType => ReplayStepType.Merge;

    public string debugLabel;

    public void Execute(ReplayExecutionContext context) { }
}

[Serializable]
public class SupplyReplayCommand : IReplayCommand
{
    public string DebugLabel => debugLabel;
    public ReplayStepType StepType => ReplayStepType.Supply;

    public string debugLabel;

    public void Execute(ReplayExecutionContext context) { }
}
