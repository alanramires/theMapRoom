using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MergeReplayCommand : IReplayCommand
{
    public string ReceiverInstanceId;
    public List<string> ConsumedInstanceIds = new List<string>();
    public int ReceiverHpAfter;
    public int ReceiverFuelAfter;
    public string debugLabel;

    public string DebugLabel => string.IsNullOrWhiteSpace(debugLabel)
        ? $"Merge: receiver {ReceiverInstanceId} hp={ReceiverHpAfter} fuel={ReceiverFuelAfter} consumed={ConsumedInstanceIds?.Count ?? 0}"
        : debugLabel;

    public ReplayStepType StepType => ReplayStepType.Merge;

    public void Execute(ReplayExecutionContext context)
    {
        UnitManager receiver = ReplayRuntimeLookup.FindUnitByInstanceId(ReceiverInstanceId);
        if (receiver != null)
        {
            receiver.SetCurrentHP(Mathf.Max(0, ReceiverHpAfter));
            receiver.SetCurrentFuel(Mathf.Max(0, ReceiverFuelAfter));
            if (!receiver.gameObject.activeSelf)
                receiver.gameObject.SetActive(true);
        }

        if (ConsumedInstanceIds == null)
            return;

        for (int i = 0; i < ConsumedInstanceIds.Count; i++)
        {
            UnitManager consumed = ReplayRuntimeLookup.FindUnitByInstanceId(ConsumedInstanceIds[i]);
            if (consumed == null)
                continue;

            consumed.SetCurrentHP(0);
            consumed.gameObject.SetActive(false);
        }
    }
}
