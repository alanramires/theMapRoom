using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AttackReplayCommand : IReplayCommand
{
    public string AttackerInstanceId;
    public string DefenderInstanceId;
    public int AttackerHpBefore;
    public int AttackerHpAfter;
    public int DefenderHpBefore;
    public int DefenderHpAfter;
    public bool AttackerDied;
    public bool DefenderDied;
    public List<EmbarkedCascadeEntry> EmbarkedCascade = new List<EmbarkedCascadeEntry>();
    public CinematicTrack CinematicTrack = new CinematicTrack();
    public string debugLabel;

    public string DebugLabel => string.IsNullOrWhiteSpace(debugLabel)
        ? $"Attack: Unit {AttackerInstanceId} -> Unit {DefenderInstanceId} | {DefenderHpBefore}hp -> {DefenderHpAfter}hp"
        : debugLabel;

    public ReplayStepType StepType => ReplayStepType.Attack;

    public void Execute(ReplayExecutionContext context)
    {
        UnitManager attacker = FindUnitByInstanceId(AttackerInstanceId);
        UnitManager defender = FindUnitByInstanceId(DefenderInstanceId);

        TryPlayCombatPresentation(context, attacker, defender);

        if (attacker != null)
        {
            attacker.SetCurrentHP(Mathf.Max(0, AttackerHpAfter));
            if (AttackerDied)
                attacker.gameObject.SetActive(false);
            else if (!attacker.gameObject.activeSelf)
                attacker.gameObject.SetActive(true);
        }

        if (defender != null)
        {
            defender.SetCurrentHP(Mathf.Max(0, DefenderHpAfter));
            if (DefenderDied)
                defender.gameObject.SetActive(false);
            else if (!defender.gameObject.activeSelf)
                defender.gameObject.SetActive(true);
        }

        if (EmbarkedCascade == null)
            return;

        for (int i = 0; i < EmbarkedCascade.Count; i++)
        {
            EmbarkedCascadeEntry entry = EmbarkedCascade[i];
            if (entry == null)
                continue;

            UnitManager unit = FindUnitByInstanceId(entry.UnitInstanceId);
            if (unit == null)
                continue;

            unit.SetCurrentHP(Mathf.Max(0, entry.HpAfter));
            if (entry.Died)
                unit.gameObject.SetActive(false);
            else if (!unit.gameObject.activeSelf)
                unit.gameObject.SetActive(true);
        }
    }


    private static void TryPlayCombatPresentation(ReplayExecutionContext context, UnitManager attacker, UnitManager defender)
    {
        if (context == null || !context.IsReplayMode || !context.AnimateCombatPresentation)
            return;
        if (attacker == null || defender == null)
            return;

        AnimationManager animation = context.AnimationManager != null
            ? context.AnimationManager
            : UnityEngine.Object.FindAnyObjectByType<AnimationManager>();
        CursorController cursor = context.CursorController != null
            ? context.CursorController
            : UnityEngine.Object.FindAnyObjectByType<CursorController>();

        if (animation != null)
        {
            animation.PlayCombatBumpTowards(attacker, defender);
            animation.PlayWeaponProjectile(attacker, defender, null, WeaponTrajectoryType.Straight);
        }

        cursor?.PlayCombatAttackSfx(WeaponTrajectoryType.Straight, 1f);
    }

    private static UnitManager FindUnitByInstanceId(string rawInstanceId)
    {
        if (string.IsNullOrWhiteSpace(rawInstanceId))
            return null;

        if (!int.TryParse(rawInstanceId, out int id))
            return null;

        UnitManager[] units = UnityEngine.Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit != null && unit.InstanceId == id)
                return unit;
        }

        return null;
    }
}

