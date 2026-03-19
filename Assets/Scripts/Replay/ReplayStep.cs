using System;

[Serializable]
public class ReplayStep
{
    public int StepIndex;
    public ReplayStepType StepType;
    [NonSerialized] public IReplayCommand Command;
    [NonSerialized] public TurnStartSnapshot PostStepSnapshot;
    public string DebugLabel;
}
