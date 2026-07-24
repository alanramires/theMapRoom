using UnityEngine;

public static class ThreatRevisionTracker
{
    private const int SlotMin = 0;
    private const int SlotMax = 9;
    private static readonly int[] slotObserverRevision = new int[SlotMax + 1];
    private static int globalBoardRevision;
    private static int matchFlagsHash = BuildFlagsHash(enableLdt: true, enableLos: true, enableSpotter: true);

    public static int GlobalBoardRevision => globalBoardRevision;
    public static int MatchFlagsHash => matchFlagsHash;

    public static int GetSlotObserverRevision(PlayerSlotId slot)
    {
        if (!slot.IsValid || slot.Value < SlotMin || slot.Value > SlotMax)
            return 0;
        return slotObserverRevision[slot.Value];
    }

    public static void SetMatchFlags(bool enableLdt, bool enableLos, bool enableSpotter)
    {
        int nextHash = BuildFlagsHash(enableLdt, enableLos, enableSpotter);
        if (matchFlagsHash == nextHash)
            return;

        matchFlagsHash = nextHash;
    }

    public static void NotifyUnitCellChanged(UnitManager unit, Vector3Int previousCell, Vector3Int nextCell)
    {
        if (!Application.isPlaying || unit == null)
            return;

        previousCell.z = 0;
        nextCell.z = 0;
        if (previousCell == nextCell)
            return;

        IncrementGlobalBoard();
        IncrementSlotObserver(unit.SlotIndex);
    }

    public static void NotifyUnitLayerChanged(UnitManager unit, Domain previousDomain, HeightLevel previousHeight, Domain nextDomain, HeightLevel nextHeight)
    {
        if (!Application.isPlaying || unit == null)
            return;
        if (previousDomain == nextDomain && previousHeight == nextHeight)
            return;

        IncrementGlobalBoard();
        IncrementSlotObserver(unit.SlotIndex);
    }

    public static void NotifyUnitEmbarkStateChanged(UnitManager unit, bool previousEmbarked, bool nextEmbarked)
    {
        if (!Application.isPlaying || unit == null)
            return;
        if (previousEmbarked == nextEmbarked)
            return;

        IncrementGlobalBoard();
        IncrementSlotObserver(unit.SlotIndex);
    }

    public static void NotifyUnitSlotChanged(int previousSlot, int nextSlot)
    {
        if (!Application.isPlaying)
            return;
        if (previousSlot == nextSlot)
            return;

        IncrementGlobalBoard();
        IncrementSlotObserver(previousSlot);
        IncrementSlotObserver(nextSlot);
    }

    public static void NotifyUnitDisabled(UnitManager unit, bool isEmbarked)
    {
        if (!Application.isPlaying || unit == null)
            return;

        IncrementGlobalBoard();
        IncrementSlotObserver(unit.SlotIndex);
    }

    public static void NotifyUnitDataApplied(UnitManager unit)
    {
        if (!Application.isPlaying || unit == null)
            return;

        IncrementSlotObserver(unit.SlotIndex);
    }

    public static void NotifyUnitSpawned(UnitManager unit)
    {
        if (!Application.isPlaying || unit == null)
            return;

        IncrementGlobalBoard();
        IncrementSlotObserver(unit.SlotIndex);
    }

    public static void ForceInvalidateAll()
    {
        if (!Application.isPlaying)
            return;

        IncrementGlobalBoard();
        for (int slot = SlotMin; slot <= SlotMax; slot++)
            IncrementSlotObserver(slot);
    }

    public static void NotifyConstructionCellChanged(ConstructionManager construction, Vector3Int previousCell, Vector3Int nextCell)
    {
        if (!Application.isPlaying || construction == null)
            return;

        previousCell.z = 0;
        nextCell.z = 0;
        if (previousCell == nextCell)
            return;

        IncrementGlobalBoard();
    }

    public static void NotifyConstructionTeamChanged(TeamId previousTeam, TeamId nextTeam)
    {
        if (!Application.isPlaying)
            return;
        if (previousTeam == nextTeam)
            return;

        IncrementGlobalBoard();
    }

    private static void IncrementGlobalBoard()
    {
        if (globalBoardRevision == int.MaxValue)
            globalBoardRevision = 1;
        else
            globalBoardRevision++;
    }

    private static void IncrementSlotObserver(int slot)
    {
        if (slot < SlotMin || slot > SlotMax)
            return;

        if (slotObserverRevision[slot] == int.MaxValue)
            slotObserverRevision[slot] = 1;
        else
            slotObserverRevision[slot]++;
    }

    private static int BuildFlagsHash(bool enableLdt, bool enableLos, bool enableSpotter)
    {
        int hash = 17;
        hash = hash * 31 + (enableLdt ? 1 : 0);
        hash = hash * 31 + (enableLos ? 1 : 0);
        hash = hash * 31 + (enableSpotter ? 1 : 0);
        return hash;
    }
}
