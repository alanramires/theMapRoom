public interface IReplayCommand
{
    string DebugLabel { get; }
    ReplayStepType StepType { get; }
    void Execute(ReplayExecutionContext context);
}
