using System;
using UnityEngine;

[Serializable]
public class BuyUnitReplayCommand : IReplayCommand
{
    public string UnitInstanceId;
    public string UnitTypeId;
    public Vector3Int SpawnHex;
    public UnitLayerMode SpawnLayer;
    public TeamId BuyingTeam;
    public int EconomyBefore;
    public int EconomyAfter;
    public string debugLabel;

    public string DebugLabel => string.IsNullOrWhiteSpace(debugLabel)
        ? $"Buy: {BuyingTeam} spawns {UnitTypeId} (id:{UnitInstanceId}) at ({SpawnHex.x},{SpawnHex.y}) | ${EconomyBefore} -> ${EconomyAfter}"
        : debugLabel;

    public ReplayStepType StepType => ReplayStepType.BuyUnit;

    public void Execute(ReplayExecutionContext context)
    {
        UnitSpawner spawner = UnityEngine.Object.FindAnyObjectByType<UnitSpawner>();
        if (spawner == null || string.IsNullOrWhiteSpace(UnitTypeId))
            return;

        if (!spawner.TryGetUnitData(UnitTypeId, out UnitData unitData) || unitData == null)
            return;

        Vector3Int spawnCell = SpawnHex;
        spawnCell.z = 0;

        UnitManager manager = null;
        int targetId = 0;
        if (int.TryParse(UnitInstanceId, out targetId) && targetId > 0)
            manager = ReplayRuntimeLookup.FindUnitByInstanceId(UnitInstanceId);

        if (manager == null)
        {
            GameObject spawned = spawner.SpawnAtCell(unitData, BuyingTeam, spawnCell);
            if (spawned == null)
                return;

            manager = spawned.GetComponent<UnitManager>();
            if (manager == null)
                return;

            if (targetId > 0)
            {
                manager.AssignSpawnInstanceId(targetId);
                spawner.EnsureNextIdAbove(targetId);
            }
        }
        else
        {
            if (!manager.gameObject.activeSelf)
                manager.gameObject.SetActive(true);

            manager.SetTeamId(BuyingTeam);
            manager.SetCurrentCellPosition(spawnCell, enforceFinalOccupancyRule: false);
        }

        manager.TrySetCurrentLayerMode(SpawnLayer.domain, SpawnLayer.heightLevel);

        MatchController match = context != null ? context.MatchController : UnityEngine.Object.FindAnyObjectByType<MatchController>();
        if (match != null)
            match.TrySetActualMoney(BuyingTeam, Mathf.Max(0, EconomyAfter));

        CursorController cursor = UnityEngine.Object.FindAnyObjectByType<CursorController>();
        if (cursor != null)
            cursor.PlayDoneSfx();
    }
}


