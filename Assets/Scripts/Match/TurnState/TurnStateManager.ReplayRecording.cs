using System.Collections.Generic;
using UnityEngine;

public partial class TurnStateManager
{
    private readonly List<CinematicEvent> pendingCombatCinematicEvents = new List<CinematicEvent>();

    private void RecordCinematicConfirm(Vector3Int cursorHex)
    {
        RecordCinematicEvent(cursorHex, CinematicAction.Confirm, 0.10f, "Confirm");
    }

    private void RecordCinematicAimAction(Vector3Int cursorHex)
    {
        RecordCinematicEvent(cursorHex, CinematicAction.AimAction, 0.10f, "AimAction");
    }

    private void RecordCinematicCursorMove(Vector3Int cursorHex)
    {
        RecordCinematicEvent(cursorHex, CinematicAction.None, 0.06f, "CursorMove");
    }

    private void DiscardPendingCombatCinematicTrack()
    {
        pendingCombatCinematicEvents.Clear();
    }

    private CinematicTrack ConsumePendingCombatCinematicTrack()
    {
        CinematicTrack track = new CinematicTrack();
        for (int i = 0; i < pendingCombatCinematicEvents.Count; i++)
        {
            CinematicEvent item = pendingCombatCinematicEvents[i];
            if (item == null)
                continue;

            CinematicEvent clone = new CinematicEvent
            {
                CursorHex = item.CursorHex,
                Action = item.Action,
                DelayAfter = item.DelayAfter,
                DebugLabel = item.DebugLabel
            };
            track.Events.Add(clone);
        }

        pendingCombatCinematicEvents.Clear();
        return track;
    }

    private void RecordCinematicEvent(Vector3Int cursorHex, CinematicAction action, float delayAfter, string debugLabel)
    {
        if (replayManager == null || !replayManager.IsRecording)
            return;

        cursorHex.z = 0;
        pendingCombatCinematicEvents.Add(new CinematicEvent
        {
            CursorHex = cursorHex,
            Action = action,
            DelayAfter = Mathf.Max(0f, delayAfter),
            DebugLabel = debugLabel
        });
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

        CaptureReplayCommand command = new CaptureReplayCommand
        {
            UnitInstanceId = capturer.InstanceId.ToString(),
            ConstructionId = targetConstruction.InstanceId.ToString(),
            CapturePointsBefore = Mathf.Max(0, capturePointsBefore),
            CapturePointsAfter = Mathf.Max(0, capturePointsAfter),
            CaptureCompleted = captureCompleted,
            NewOwner = newOwner,
            debugLabel = $"Capture: unit {capturer.InstanceId} on construction {targetConstruction.InstanceId} | {capturePointsBefore}->{capturePointsAfter}"
        };

        replayManager.RecordCommand(command);
    }

    private void RecordEmbarkReplayCommand(UnitManager passenger, UnitManager transporter, int slotIndex)
    {
        if (replayManager == null || passenger == null || transporter == null)
            return;

        EmbarkReplayCommand command = new EmbarkReplayCommand
        {
            PassengerInstanceId = passenger.InstanceId.ToString(),
            TransporterInstanceId = transporter.InstanceId.ToString(),
            SeatIndex = Mathf.Max(0, slotIndex),
            debugLabel = $"Embark: passenger {passenger.InstanceId} -> transporter {transporter.InstanceId} slot {Mathf.Max(0, slotIndex)}"
        };

        replayManager.RecordCommand(command);
    }

    private void RecordDisembarkReplayCommand(UnitManager passenger, UnitManager transporter, Vector3Int targetCell)
    {
        if (replayManager == null || passenger == null || transporter == null)
            return;

        Vector3Int normalizedCell = targetCell;
        normalizedCell.z = 0;

        DisembarkReplayCommand command = new DisembarkReplayCommand
        {
            PassengerInstanceId = passenger.InstanceId.ToString(),
            TransporterInstanceId = transporter.InstanceId.ToString(),
            DisembarkHex = normalizedCell,
            DisembarkLayer = passenger.GetCurrentLayerMode(),
            debugLabel = $"Disembark: passenger {passenger.InstanceId} from transporter {transporter.InstanceId} -> ({normalizedCell.x},{normalizedCell.y})"
        };

        replayManager.RecordCommand(command);
    }

    private void RecordMergeReplayCommand(UnitManager receiver, List<UnitManager> consumedUnits)
    {
        if (replayManager == null || receiver == null)
            return;

        List<string> consumed = new List<string>();
        if (consumedUnits != null)
        {
            for (int i = 0; i < consumedUnits.Count; i++)
            {
                UnitManager unit = consumedUnits[i];
                if (unit == null || unit.InstanceId <= 0)
                    continue;
                consumed.Add(unit.InstanceId.ToString());
            }
        }

        MergeReplayCommand command = new MergeReplayCommand
        {
            ReceiverInstanceId = receiver.InstanceId.ToString(),
            ConsumedInstanceIds = consumed,
            ReceiverHpAfter = Mathf.Max(0, receiver.CurrentHP),
            ReceiverFuelAfter = Mathf.Max(0, receiver.CurrentFuel),
            debugLabel = $"Merge: receiver {receiver.InstanceId} hp={receiver.CurrentHP} fuel={receiver.CurrentFuel} consumed={consumed.Count}"
        };

        replayManager.RecordCommand(command);
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

        int hpGain = Mathf.Max(0, hpAfter - hpBefore);
        int fuelGain = Mathf.Max(0, fuelAfter - fuelBefore);
        int ammoGain = ComputeAmmoGain(ammoBefore, ammoAfter);
        string supplierId = supplier != null ? supplier.InstanceId.ToString() : string.Empty;
        string sourceConstructionId = sourceConstruction != null ? sourceConstruction.InstanceId.ToString() : string.Empty;

        SupplyReplayCommand command = new SupplyReplayCommand
        {
            SupplierInstanceId = supplierId,
            ReceiverInstanceId = receiver.InstanceId.ToString(),
            FuelTransferred = fuelGain,
            AmmoTransferred = ammoGain,
            PartsTransferred = hpGain,
            EconomyCostBefore = Mathf.Max(0, economyBefore),
            EconomyCostAfter = Mathf.Max(0, economyAfter),
            PayingTeam = payingTeam,
            ReceiverHpBefore = Mathf.Max(0, hpBefore),
            ReceiverHpAfter = Mathf.Max(0, hpAfter),
            ReceiverFuelBefore = Mathf.Max(0, fuelBefore),
            ReceiverFuelAfter = Mathf.Max(0, fuelAfter),
            ReceiverAmmoBeforeByWeapon = ammoBefore != null ? new List<int>(ammoBefore) : new List<int>(),
            ReceiverAmmoAfterByWeapon = ammoAfter != null ? new List<int>(ammoAfter) : new List<int>(),
            SourceConstructionInstanceId = sourceConstructionId,
            debugLabel = $"Supply: {(string.IsNullOrWhiteSpace(supplierId) ? $"construction {sourceConstructionId}" : $"unit {supplierId}")} -> unit {receiver.InstanceId} | HP +{hpGain} F +{fuelGain} A +{ammoGain} | ${Mathf.Max(0, economyBefore)}->{Mathf.Max(0, economyAfter)}"
        };

        replayManager.RecordCommand(command);
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
