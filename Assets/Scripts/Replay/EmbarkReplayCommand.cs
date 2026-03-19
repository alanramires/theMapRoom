using System;

[Serializable]
public class EmbarkReplayCommand : IReplayCommand
{
    public string PassengerInstanceId;
    public string TransporterInstanceId;
    public int SeatIndex;
    public string debugLabel;

    public string DebugLabel => string.IsNullOrWhiteSpace(debugLabel)
        ? $"Embark: passenger {PassengerInstanceId} -> transporter {TransporterInstanceId} slot {SeatIndex}"
        : debugLabel;

    public ReplayStepType StepType => ReplayStepType.Embark;

    public void Execute(ReplayExecutionContext context)
    {
        UnitManager passenger = ReplayRuntimeLookup.FindUnitByInstanceId(PassengerInstanceId);
        UnitManager transporter = ReplayRuntimeLookup.FindUnitByInstanceId(TransporterInstanceId);
        if (passenger == null || transporter == null)
            return;

        transporter.TryEmbarkPassengerInSlot(passenger, SeatIndex, out _);
    }
}
