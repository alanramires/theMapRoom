using System;
using UnityEngine;

[Serializable]
public class CaptureReplayCommand : IReplayCommand
{
    public string UnitInstanceId;
    public string ConstructionId;
    public int CapturePointsBefore;
    public int CapturePointsAfter;
    public bool CaptureCompleted;
    public TeamId NewOwner;
    public string debugLabel;

    public string DebugLabel => string.IsNullOrWhiteSpace(debugLabel)
        ? $"Capture: unit {UnitInstanceId} on construction {ConstructionId} | {CapturePointsBefore}->{CapturePointsAfter}"
        : debugLabel;

    public ReplayStepType StepType => ReplayStepType.Capture;

    public void Execute(ReplayExecutionContext context)
    {
        ConstructionManager target = ReplayRuntimeLookup.FindConstructionByInstanceId(ConstructionId);
        if (target == null)
            target = ReplayRuntimeLookup.FindConstructionById(ConstructionId);
        if (target == null)
            return;

        target.SetCurrentCapturePoints(Mathf.Max(0, CapturePointsAfter));
        if (CaptureCompleted)
            target.SetTeamId(NewOwner);
    }
}
