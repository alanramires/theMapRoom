using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SupplyReplayCommand : IReplayCommand
{
    public string SupplierInstanceId;
    public string ReceiverInstanceId;
    public int FuelTransferred;
    public int AmmoTransferred;
    public int PartsTransferred;
    public int EconomyCostBefore;
    public int EconomyCostAfter;
    public TeamId PayingTeam;
    public int ReceiverHpBefore;
    public int ReceiverHpAfter;
    public int ReceiverFuelBefore;
    public int ReceiverFuelAfter;
    public List<int> ReceiverAmmoBeforeByWeapon = new List<int>();
    public List<int> ReceiverAmmoAfterByWeapon = new List<int>();
    public string SourceConstructionInstanceId;
    public string debugLabel;

    public string DebugLabel => string.IsNullOrWhiteSpace(debugLabel)
        ? $"Supply: {SupplierInstanceId} -> {ReceiverInstanceId} | HP +{PartsTransferred} F +{FuelTransferred} A +{AmmoTransferred} | ${EconomyCostBefore}->{EconomyCostAfter}"
        : debugLabel;

    public ReplayStepType StepType => ReplayStepType.Supply;

    public void Execute(ReplayExecutionContext context)
    {
        UnitManager receiver = ReplayRuntimeLookup.FindUnitByInstanceId(ReceiverInstanceId);
        if (receiver == null)
            return;

        if (ReceiverHpAfter >= 0)
            receiver.SetCurrentHP(Mathf.Max(0, ReceiverHpAfter));
        else
            receiver.SetCurrentHP(receiver.CurrentHP + Mathf.Max(0, PartsTransferred));

        if (ReceiverFuelAfter >= 0)
            receiver.SetCurrentFuel(Mathf.Max(0, ReceiverFuelAfter));
        else
            receiver.SetCurrentFuel(receiver.CurrentFuel + Mathf.Max(0, FuelTransferred));

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = receiver.GetEmbarkedWeapons();
        if (runtimeWeapons != null && runtimeWeapons.Count > 0)
        {
            if (ReceiverAmmoAfterByWeapon != null && ReceiverAmmoAfterByWeapon.Count > 0)
            {
                ApplyAmmoBySnapshot(receiver, runtimeWeapons, ReceiverAmmoAfterByWeapon);
            }
            else if (AmmoTransferred > 0)
            {
                ApplyAmmoByDelta(receiver, runtimeWeapons, AmmoTransferred);
            }
        }

        MatchController match = context != null ? context.MatchController : UnityEngine.Object.FindAnyObjectByType<MatchController>();
        if (match != null)
            match.TrySetActualMoney(PayingTeam, Mathf.Max(0, EconomyCostAfter));
    }

    private static void ApplyAmmoBySnapshot(UnitManager receiver, IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons, List<int> ammoAfterByWeapon)
    {
        List<UnitEmbarkedWeapon> baselineWeapons = null;
        if (receiver.TryGetUnitData(out UnitData data) && data != null)
            baselineWeapons = data.embarkedWeapons;

        int count = Mathf.Min(runtimeWeapons.Count, ammoAfterByWeapon.Count);
        for (int i = 0; i < count; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            if (runtime == null)
                continue;

            int maxAmmo = int.MaxValue;
            if (baselineWeapons != null && i < baselineWeapons.Count && baselineWeapons[i] != null)
                maxAmmo = Mathf.Max(0, baselineWeapons[i].squadAmmunition);

            runtime.squadAmmunition = Mathf.Clamp(Mathf.Max(0, ammoAfterByWeapon[i]), 0, maxAmmo);
        }
    }

    private static void ApplyAmmoByDelta(UnitManager receiver, IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons, int ammoTransferred)
    {
        List<UnitEmbarkedWeapon> baselineWeapons = null;
        if (receiver.TryGetUnitData(out UnitData data) && data != null)
            baselineWeapons = data.embarkedWeapons;

        int remaining = Mathf.Max(0, ammoTransferred);
        for (int i = 0; i < runtimeWeapons.Count && remaining > 0; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            if (runtime == null)
                continue;

            int maxAmmo = runtime.squadAmmunition + remaining;
            if (baselineWeapons != null && i < baselineWeapons.Count && baselineWeapons[i] != null)
                maxAmmo = Mathf.Max(0, baselineWeapons[i].squadAmmunition);

            int missing = Mathf.Max(0, maxAmmo - runtime.squadAmmunition);
            if (missing <= 0)
                continue;

            int gained = Mathf.Min(remaining, missing);
            runtime.squadAmmunition += gained;
            remaining -= gained;
        }
    }
}

