using UnityEngine;

public static class ThreatRevisionTracker
{
    private const int TeamIdMin = 0;
    private const int TeamIdMax = 9;
    private static readonly int[] teamObserverRevision = new int[TeamIdMax + 1];
    private static int globalBoardRevision;
    private static int matchFlagsHash = BuildFlagsHash(enableLdt: true, enableLos: true, enableSpotter: true);

    public static int GlobalBoardRevision => globalBoardRevision;
    public static int MatchFlagsHash => matchFlagsHash;

    public static int GetTeamObserverRevision(int teamId)
    {
        if (teamId < TeamIdMin || teamId > TeamIdMax)
            return 0;
        return teamObserverRevision[teamId];
    }

    public static int GetTeamObserverRevision(TeamId teamId)
    {
        return GetTeamObserverRevision((int)teamId);
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
        IncrementTeamObserver(unit.TeamId);
    }

    public static void NotifyUnitLayerChanged(UnitManager unit, Domain previousDomain, HeightLevel previousHeight, Domain nextDomain, HeightLevel nextHeight)
    {
        if (!Application.isPlaying || unit == null)
            return;
        if (previousDomain == nextDomain && previousHeight == nextHeight)
            return;

        IncrementGlobalBoard();
        IncrementTeamObserver(unit.TeamId);
    }

    public static void NotifyUnitEmbarkStateChanged(UnitManager unit, bool previousEmbarked, bool nextEmbarked)
    {
        if (!Application.isPlaying || unit == null)
            return;
        if (previousEmbarked == nextEmbarked)
            return;

        IncrementGlobalBoard();
        IncrementTeamObserver(unit.TeamId);
    }

    public static void NotifyUnitTeamChanged(TeamId previousTeam, TeamId nextTeam)
    {
        if (!Application.isPlaying)
            return;
        if (previousTeam == nextTeam)
            return;

        IncrementGlobalBoard();
        IncrementTeamObserver(previousTeam);
        IncrementTeamObserver(nextTeam);
    }

    public static void NotifyUnitDisabled(UnitManager unit, TeamId teamId, bool isEmbarked)
    {
        if (!Application.isPlaying || unit == null)
            return;

        IncrementGlobalBoard();
        IncrementTeamObserver(teamId);
    }

    public static void NotifyUnitDataApplied(UnitManager unit)
    {
        if (!Application.isPlaying || unit == null)
            return;

        IncrementTeamObserver(unit.TeamId);
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

    private static void IncrementTeamObserver(TeamId teamId)
    {
        IncrementTeamObserver((int)teamId);
    }

    private static void IncrementTeamObserver(int teamId)
    {
        if (teamId < TeamIdMin || teamId > TeamIdMax)
            return;

        if (teamObserverRevision[teamId] == int.MaxValue)
            teamObserverRevision[teamId] = 1;
        else
            teamObserverRevision[teamId]++;
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
