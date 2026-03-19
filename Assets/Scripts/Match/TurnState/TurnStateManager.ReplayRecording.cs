using System.Collections.Generic;
using UnityEngine;

public partial class TurnStateManager
{
    private void RecordCinematicConfirm(Vector3Int cursorHex)
    {
        // Deprecated with ActionStack replay.
    }

    private void RecordCinematicAimAction(Vector3Int cursorHex)
    {
        // Deprecated with ActionStack replay.
    }

    private void RecordCinematicCursorMove(Vector3Int cursorHex)
    {
        // Deprecated with ActionStack replay.
    }

    private void DiscardPendingCombatCinematicTrack()
    {
        replayManager?.DiscardCurrentBuffer("ESC");
    }

    private void RecordCaptureReplayCommand(
        UnitManager capturer,
        ConstructionManager targetConstruction,
        int capturePointsBefore,
        int capturePointsAfter,
        bool captureCompleted,
        TeamId newOwner)
    {
        if (replayManager == null || capturer == null || targetConstruction == null)
            return;

        replayManager.UpdateCurrentBufferSensorAction(SensorActionType.Capture, "CaptureConfirm");
        replayManager.UpdateCurrentBufferTarget(null, targetConstruction, targetConstruction.CurrentCellPosition, "CaptureTargetConfirm");
    }

    private void RecordEmbarkReplayCommand(UnitManager passenger, UnitManager transporter, int slotIndex)
    {
        if (replayManager == null || passenger == null || transporter == null)
            return;

        replayManager.UpdateCurrentBufferSensorAction(SensorActionType.Embark, "EmbarkConfirm");
        replayManager.UpdateCurrentBufferTarget(transporter, null, transporter.CurrentCellPosition, "EmbarkTargetConfirm");
    }

    private void RecordDisembarkReplayCommand(UnitManager passenger, UnitManager transporter, Vector3Int targetCell)
    {
        if (replayManager == null || passenger == null || transporter == null)
            return;

        replayManager.UpdateCurrentBufferSensorAction(SensorActionType.Disembark, "DisembarkConfirm");
        replayManager.UpdateCurrentBufferTarget(passenger, null, targetCell, "DisembarkTargetConfirm");
    }

    private void RecordMergeReplayCommand(UnitManager receiver, List<UnitManager> consumedUnits)
    {
        if (replayManager == null || receiver == null)
            return;

        replayManager.UpdateCurrentBufferSensorAction(SensorActionType.Merge, "MergeConfirm");
        replayManager.UpdateCurrentBufferTarget(receiver, null, receiver.CurrentCellPosition, "MergeTargetConfirm");
    }

    private void RecordSupplyReplayCommand(
        UnitManager supplier,
        ConstructionManager sourceConstruction,
        UnitManager receiver,
        TeamId payingTeam,
        int economyBefore,
        int economyAfter,
        int hpBefore,
        int hpAfter,
        int fuelBefore,
        int fuelAfter,
        List<int> ammoBefore,
        List<int> ammoAfter)
    {
        if (replayManager == null || receiver == null)
            return;

        replayManager.UpdateCurrentBufferSensorAction(SensorActionType.Supply, "SupplyConfirm");
        replayManager.UpdateCurrentBufferTarget(receiver, sourceConstruction, receiver.CurrentCellPosition, "SupplyTargetConfirm");
    }

    private static List<int> SnapshotUnitAmmoByWeapon(UnitManager unit)
    {
        List<int> snapshot = new List<int>();
        if (unit == null)
            return snapshot;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null)
            return snapshot;

        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon weapon = weapons[i];
            snapshot.Add(weapon != null ? Mathf.Max(0, weapon.squadAmmunition) : 0);
        }

        return snapshot;
    }

    private static int ComputeAmmoGain(List<int> before, List<int> after)
    {
        if (before == null || after == null)
            return 0;

        int count = Mathf.Min(before.Count, after.Count);
        int gain = 0;
        for (int i = 0; i < count; i++)
            gain += Mathf.Max(0, after[i] - before[i]);

        return gain;
    }
}
