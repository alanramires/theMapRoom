using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DisembarkReplayCommand : IReplayCommand
{
    public string PassengerInstanceId;
    public string TransporterInstanceId;
    public Vector3Int DisembarkHex;
    public UnitLayerMode DisembarkLayer;
    public string debugLabel;

    public string DebugLabel => string.IsNullOrWhiteSpace(debugLabel)
        ? $"Disembark: passenger {PassengerInstanceId} from transporter {TransporterInstanceId} -> ({DisembarkHex.x},{DisembarkHex.y})"
        : debugLabel;

    public ReplayStepType StepType => ReplayStepType.Disembark;

    public void Execute(ReplayExecutionContext context)
    {
        UnitManager passenger = ReplayRuntimeLookup.FindUnitByInstanceId(PassengerInstanceId);
        UnitManager transporter = ReplayRuntimeLookup.FindUnitByInstanceId(TransporterInstanceId);
        if (passenger == null || transporter == null)
            return;

        if (passenger.IsEmbarked && passenger.EmbarkedTransporter == transporter)
        {
            IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
            if (seats != null)
            {
                for (int i = 0; i < seats.Count; i++)
                {
                    UnitTransportSeatRuntime seat = seats[i];
                    if (seat == null || seat.embarkedUnit != passenger)
                        continue;

                    transporter.TryDisembarkPassengerFromSeat(seat.slotIndex, seat.seatIndex, out _, out _);
                    break;
                }
            }
        }

        Vector3Int targetCell = DisembarkHex;
        targetCell.z = 0;
        passenger.SetCurrentCellPosition(targetCell, enforceFinalOccupancyRule: false);
        passenger.TrySetCurrentLayerMode(DisembarkLayer.domain, DisembarkLayer.heightLevel);
    }
}
