using System.Collections.Generic;
using System;
using UnityEngine;

public partial class AIShoppingPlanner
{
    public sealed class EnemyClassPressureInspection
    {
        public GameUnitClass UnitClass;
        public WeaponCategory CounterCategory;
        public int Count;
        public int VisibleCount;
        public int RememberedCount;
        public float Score;
        public float VisibleScore;
        public float RememberedScore;
    }

    public sealed class CounterPressureInspection
    {
        public readonly List<EnemyClassPressureInspection> Classes = new List<EnemyClassPressureInspection>();
        public float AntiInfantry;
        public float AntiTank;
        public float AntiAir;
        public float AntiShip;
        public int VisibleUnits;
        public int RememberedUnits;
        public int SensorContacts;
        public int CombatContacts;
        public int AnonymousThreatSignals;

        public float Get(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.AntiInfantaria: return AntiInfantry;
                case WeaponCategory.AntiTanque: return AntiTank;
                case WeaponCategory.AntiAerea: return AntiAir;
                case WeaponCategory.AntiNavio: return AntiShip;
                default: return 0f;
            }
        }

        public WeaponCategory DominantCategory
        {
            get
            {
                WeaponCategory best = WeaponCategory.AntiInfantaria;
                float bestScore = AntiInfantry;
                if (AntiTank > bestScore) { best = WeaponCategory.AntiTanque; bestScore = AntiTank; }
                if (AntiAir > bestScore) { best = WeaponCategory.AntiAerea; bestScore = AntiAir; }
                if (AntiShip > bestScore) best = WeaponCategory.AntiNavio;
                return best;
            }
        }
    }

    public static CounterPressureInspection InspectCounterPressure(AIWorldSnapshot snapshot)
        => BuildCounterPressure(snapshot);

    public static float InspectCounterFit(UnitData unit, CounterPressureInspection pressure)
        => ScoreCounterFit(unit, pressure);

    private static CounterPressureInspection BuildCounterPressure(AIWorldSnapshot snapshot)
    {
        var result = new CounterPressureInspection();
        if (snapshot == null)
            return result;

        var byClass = new Dictionary<GameUnitClass, EnemyClassPressureInspection>();
        var visibleIds = new HashSet<int>();
        IReadOnlyCollection<AIIntelContact> contacts =
            AIIntelLedger.UpdateAndGetContacts(snapshot);
        var contactsByUid = new Dictionary<int, AIIntelContact>();
        if (contacts != null)
            foreach (AIIntelContact contact in contacts)
                if (contact != null && contact.uid > 0)
                {
                    contactsByUid[contact.uid] = contact;
                    if (!contact.destroyed)
                    {
                        if (string.Equals(contact.source, "combate", StringComparison.OrdinalIgnoreCase))
                            result.CombatContacts++;
                        else
                            result.SensorContacts++;
                    }
                }

        IReadOnlyList<AIIntelThreatSignal> threatSignals =
            AIIntelLedger.GetThreatSignals(snapshot.AITeam);
        if (threatSignals != null)
        {
            int lookback = Instance != null
                ? Mathf.Max(1, Instance.IntelShoppingLookbackTurns)
                : 4;
            foreach (AIIntelThreatSignal signal in threatSignals)
            {
                if (signal == null)
                    continue;
                int age = Mathf.Max(0, snapshot.TurnNumber - signal.turn);
                if (age >= lookback)
                    continue;

                float recency = Mathf.Lerp(0.35f, 1f, 1f - age / (float)lookback);
                float score = (0.7f
                    + signal.damage * 0.16f
                    + signal.kills * 1.4f
                    + Mathf.Clamp(signal.destroyedValue / 12000f, 0f, 2.5f))
                    * recency;
                AddAnonymousWeaponPressure(result, signal.weaponCategory, score);
                result.AnonymousThreatSignals++;
            }
        }

        if (snapshot.EnemyUnits != null)
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || !enemy.TryGetUnitData(out UnitData data) || data == null)
                continue;

            visibleIds.Add(enemy.InstanceId);
            float score = ComputeEnemyCounterWeight(enemy, data);
            if (contactsByUid.TryGetValue(enemy.InstanceId, out AIIntelContact visibleMemory))
                score += ComputeCombatImpactScore(visibleMemory);
            AddPressure(result, byClass, data, score, visible: true);
        }

        if (contacts != null)
        {
            int lookback = Instance != null
                ? Mathf.Max(1, Instance.IntelShoppingLookbackTurns)
                : 4;
            foreach (AIIntelContact memory in contacts)
            {
                if (memory == null || memory.destroyed || visibleIds.Contains(memory.uid))
                    continue;

                int age = Mathf.Max(0, snapshot.TurnNumber - memory.lastSeenTurn);
                if (age >= lookback)
                    continue;
                UnitData data = ResolveUnitDataBySigla(memory.sigla);
                if (data == null)
                    continue;

                float recency = Mathf.Lerp(0.35f, 0.85f, 1f - age / (float)lookback);
                float confidence = Mathf.Clamp01(memory.confidence);
                float score = ComputeEnemyCounterWeight(null, data) * recency * confidence
                    + ComputeCombatImpactScore(memory) * recency;
                AddPressure(result, byClass, data, score, visible: false);
            }
        }

        result.Classes.Sort((a, b) => b.Score.CompareTo(a.Score));
        return result;
    }

    private static void AddAnonymousWeaponPressure(
        CounterPressureInspection result,
        WeaponCategory category,
        float score)
    {
        switch (category)
        {
            case WeaponCategory.AntiInfantaria: result.AntiInfantry += score; break;
            case WeaponCategory.AntiTanque: result.AntiTank += score; break;
            case WeaponCategory.AntiAerea: result.AntiAir += score; break;
            case WeaponCategory.AntiNavio: result.AntiShip += score; break;
        }
    }

    private static void AddPressure(
        CounterPressureInspection result,
        Dictionary<GameUnitClass, EnemyClassPressureInspection> byClass,
        UnitData data,
        float score,
        bool visible)
    {
        WeaponCategory counter = CounterCategoryFor(data.unitClass);
        if (!byClass.TryGetValue(data.unitClass, out EnemyClassPressureInspection entry))
        {
            entry = new EnemyClassPressureInspection
            {
                UnitClass = data.unitClass,
                CounterCategory = counter,
            };
            byClass.Add(data.unitClass, entry);
            result.Classes.Add(entry);
        }

        entry.Count++;
        entry.Score += score;
        if (visible)
        {
            entry.VisibleCount++;
            entry.VisibleScore += score;
            result.VisibleUnits++;
        }
        else
        {
            entry.RememberedCount++;
            entry.RememberedScore += score;
            result.RememberedUnits++;
        }

        switch (counter)
        {
            case WeaponCategory.AntiInfantaria: result.AntiInfantry += score; break;
            case WeaponCategory.AntiTanque: result.AntiTank += score; break;
            case WeaponCategory.AntiAerea: result.AntiAir += score; break;
            case WeaponCategory.AntiNavio: result.AntiShip += score; break;
        }
    }

    private static WeaponCategory CounterCategoryFor(GameUnitClass unitClass)
    {
        switch (unitClass)
        {
            case GameUnitClass.Infantry:
                return WeaponCategory.AntiInfantaria;
            case GameUnitClass.Vehicle:
            case GameUnitClass.Armored:
            case GameUnitClass.Artillery:
                return WeaponCategory.AntiTanque;
            case GameUnitClass.Jet:
            case GameUnitClass.Helicopter:
            case GameUnitClass.Plane:
                return WeaponCategory.AntiAerea;
            case GameUnitClass.Ship:
            case GameUnitClass.Submarine:
                return WeaponCategory.AntiNavio;
            default:
                return WeaponCategory.AntiInfantaria;
        }
    }

    private static float ComputeEnemyCounterWeight(UnitManager enemy, UnitData data)
    {
        float eliteWeight = 1f + Mathf.Max(0, data.eliteLevel) * 0.75f;
        float valueWeight = 1f + Mathf.Clamp(data.cost / 20000f, 0f, 1.5f);
        float hpRatio = enemy != null && data.maxHP > 0
            ? Mathf.Clamp01(enemy.CurrentHP / (float)data.maxHP)
            : 1f;
        float combatReadiness = Mathf.Lerp(0.55f, 1f, hpRatio);
        return eliteWeight * valueWeight * combatReadiness;
    }

    private static float ComputeCombatImpactScore(AIUnitIntel memory)
    {
        if (memory == null)
            return 0f;
        return Mathf.Max(0f, memory.recentDamageDealt) * 0.35f
            + Mathf.Max(0f, memory.recentKills) * 3f
            + Mathf.Max(0f, memory.recentDestroyedValue) / 5000f;
    }

    private static float ComputeCombatImpactScore(AIIntelContact memory)
    {
        if (memory == null)
            return 0f;
        return Mathf.Max(0f, memory.recentDamageDealt) * 0.35f
            + Mathf.Max(0f, memory.recentKills) * 3f
            + Mathf.Max(0f, memory.recentDestroyedValue) / 5000f;
    }

    private static Dictionary<string, UnitData> counterUnitDataBySigla;

    private static UnitData ResolveUnitDataBySigla(string sigla)
    {
        if (string.IsNullOrWhiteSpace(sigla))
            return null;
        if (counterUnitDataBySigla == null)
        {
            counterUnitDataBySigla =
                new Dictionary<string, UnitData>(StringComparer.OrdinalIgnoreCase);
            foreach (UnitData data in Resources.FindObjectsOfTypeAll<UnitData>())
                if (data != null && !string.IsNullOrWhiteSpace(data.apelido)
                    && !counterUnitDataBySigla.ContainsKey(data.apelido.Trim()))
                    counterUnitDataBySigla.Add(data.apelido.Trim(), data);
        }
        counterUnitDataBySigla.TryGetValue(sigla.Trim(), out UnitData resolved);
        return resolved;
    }

    private static bool HasWeaponCategory(UnitData unit, WeaponCategory category)
    {
        if (unit?.embarkedWeapons == null)
            return false;
        foreach (UnitEmbarkedWeapon weapon in unit.embarkedWeapons)
            if (weapon?.weapon != null && weapon.weapon.WeaponCategory == category)
                return true;
        return false;
    }

    private static int CountOwnCounters(
        AIWorldSnapshot snapshot,
        WeaponCategory category,
        UnitRole compositionRole)
    {
        int count = 0;
        if (snapshot?.MyUnits == null)
            return count;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && unit.TryGetUnitData(out UnitData data)
                && UnitRoleCompatibility.ResolveCompositionRole(data) == compositionRole
                && HasWeaponCategory(data, category))
                count++;
        return count;
    }

    private static void AddCounterPressureDemands(
        AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands,
        CounterPressureInspection pressure)
    {
        if (snapshot == null || demands == null || pressure == null)
            return;

        int desiredAntiTank = Mathf.Clamp(Mathf.CeilToInt(pressure.AntiTank / 4f), 0, 4);
        int ownAntiTankFire = CountOwnCounters(
            snapshot, WeaponCategory.AntiTanque, UnitRole.FogoIndireto);
        int counterGap = Mathf.Max(0, desiredAntiTank - ownAntiTankFire);
        if (counterGap <= 0)
            return;

        int fireGap = Mathf.Min(2, counterGap);
        AIShoppingDemand antiTankFireSupport = NewRoleDemand(
            UnitRole.FogoIndireto,
            fireGap,
            pressure.RememberedUnits > 0 ? 11 : 15,
            "counter-pressure",
            $"anti-tank={pressure.AntiTank:F1} vis={pressure.VisibleUnits} memoria={pressure.RememberedUnits} cobertura fogo AT={ownAntiTankFire}/{desiredAntiTank}",
            false);
        antiTankFireSupport.RequiredWeaponCategory = WeaponCategory.AntiTanque;
        MergeRoleDemand(demands, antiTankFireSupport, false);
    }

    private static float ScoreCounterFit(UnitData unit, CounterPressureInspection pressure)
    {
        if (unit == null || pressure == null || unit.embarkedWeapons == null)
            return 0f;

        var categories = new HashSet<WeaponCategory>();
        foreach (UnitEmbarkedWeapon weapon in unit.embarkedWeapons)
            if (weapon?.weapon != null)
                categories.Add(weapon.weapon.WeaponCategory);

        float score = 0f;
        foreach (EnemyClassPressureInspection enemyClass in pressure.Classes)
        {
            if (!categories.Contains(enemyClass.CounterCategory))
                continue;

            BazookaTargetPriority priority =
                unit.ResolveAiTargetPriorityForTargetClass(enemyClass.UnitClass);
            float preference = priority == BazookaTargetPriority.Primary
                ? 1f
                : priority == BazookaTargetPriority.Secondary ? 0.75f : 0.45f;
            score += enemyClass.Score * preference;
        }
        return score;
    }
}
