using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;

public partial class AIShoppingPlanner
{
    private static readonly Dictionary<(UnitData attacker, UnitData defender), UnitCounterEvaluator.Evaluation>
        counterEvaluationCache =
            new Dictionary<(UnitData attacker, UnitData defender), UnitCounterEvaluator.Evaluation>();

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
        public float Coverage;
        public float Unmet;
        internal readonly Dictionary<UnitData, float> UnitTypeScores =
            new Dictionary<UnitData, float>();
    }

    public sealed class CounterPressureInspection
    {
        public readonly List<EnemyClassPressureInspection> Classes = new List<EnemyClassPressureInspection>();
        public readonly List<OwnCounterContributionInspection> OwnContributions =
            new List<OwnCounterContributionInspection>();
        public float AntiInfantry;
        public float AntiTank;
        public float AntiAir;
        public float AntiShip;
        public float RawAntiInfantry;
        public float RawAntiTank;
        public float RawAntiAir;
        public float RawAntiShip;
        public float AntiInfantryCoverage;
        public float AntiTankCoverage;
        public float AntiAirCoverage;
        public float AntiShipCoverage;
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

    public sealed class OwnCounterContributionInspection
    {
        public int UnitInstanceId;
        public string UnitName;
        public int EliteLevel;
        public GameUnitClass TargetClass;
        public WeaponCategory Category;
        public float Coverage;
    }

    public sealed class AIRosterProfile
    {
        public UnitData Unit;
        public readonly HashSet<WeaponCategory> WeaponCategories = new HashSet<WeaponCategory>();
        public readonly HashSet<ConstructionManager> Producers = new HashSet<ConstructionManager>();
        public UnitRole CompositionRole;
        public bool AvailableNow;
        public bool Unlockable;
        public bool IsTransporter;
        public bool IsSupplier;
        public readonly List<AIRosterCoverage> CoverageMatrix = new List<AIRosterCoverage>();

        public bool HasWeaponCategory(WeaponCategory category)
            => WeaponCategories.Contains(category);
    }

    public sealed class AIRosterCoverage
    {
        public bool HasWeapon;
        public WeaponCategory WeaponCategory;
        public GameUnitClass TargetClass;
        public float Coverage;
        public int Reach;
        public int ClassSize;
        public int Deaths;
    }

    public sealed class AIRosterKnowledge
    {
        public readonly List<AIRosterProfile> Profiles = new List<AIRosterProfile>();
        private readonly Dictionary<UnitData, AIRosterProfile> byUnit =
            new Dictionary<UnitData, AIRosterProfile>();

        internal AIRosterProfile GetOrCreate(UnitData unit)
        {
            if (!byUnit.TryGetValue(unit, out AIRosterProfile profile))
            {
                profile = new AIRosterProfile
                {
                    Unit = unit,
                    CompositionRole = UnitRoleCompatibility.ResolveCompositionRole(unit),
                    IsTransporter = UnitRoleCompatibility.IsOperationalTransporter(unit),
                    IsSupplier = unit != null && unit.isSupplier,
                };
                if (unit?.embarkedWeapons != null)
                    foreach (UnitEmbarkedWeapon slot in unit.embarkedWeapons)
                        if (slot?.weapon != null)
                            profile.WeaponCategories.Add(slot.weapon.WeaponCategory);
                byUnit.Add(unit, profile);
                Profiles.Add(profile);
            }
            return profile;
        }

        internal bool TryGetProfile(UnitData unit, out AIRosterProfile profile)
        {
            profile = null;
            return unit != null && byUnit.TryGetValue(unit, out profile);
        }
    }

    public static AIRosterKnowledge InspectRosterKnowledge(AIWorldSnapshot snapshot)
        => BuildRosterKnowledge(snapshot, log: false);

    private static AIRosterKnowledge BuildRosterKnowledge(AIWorldSnapshot snapshot, bool log)
    {
        var roster = new AIRosterKnowledge();
        if (snapshot?.MyBuildings == null)
            return roster;

        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || building.OfferedUnits == null)
                continue;
            bool producerAvailable = building.CanProduceUnitsForTeam(snapshot.AITeam);
            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null)
                    continue;
                AIRosterProfile profile = roster.GetOrCreate(unit);
                profile.Producers.Add(building);
                bool chainAvailable = IsEliteChainAvailable(unit, snapshot);
                profile.AvailableNow |= producerAvailable && chainAvailable
                    && IsRolePurchaseAllowed(unit, snapshot.Stance, emergency: true);
                profile.Unlockable |= !chainAvailable || !producerAvailable;
            }
        }

        roster.Profiles.Sort((a, b) =>
        {
            int available = b.AvailableNow.CompareTo(a.AvailableNow);
            if (available != 0) return available;
            int elite = a.Unit.eliteLevel.CompareTo(b.Unit.eliteLevel);
            if (elite != 0) return elite;
            return a.Unit.cost.CompareTo(b.Unit.cost);
        });

        BuildRosterCoverageMatrix(roster);

        if (log)
            LogRosterKnowledge(snapshot, roster);
        return roster;
    }

    private static void BuildRosterCoverageMatrix(AIRosterKnowledge roster)
    {
        if (roster == null)
            return;

        ResolveCounterCombatDatabases(out RPSDatabase rps, out DPQMatchupDatabase dpq,
            out WeaponPriorityData priorities);
        foreach (AIRosterProfile attackerProfile in roster.Profiles)
        {
            UnitData attacker = attackerProfile.Unit;
            if (attacker == null)
                continue;

            var classSizes = new Dictionary<GameUnitClass, int>();
            var byMatchup = new Dictionary<(WeaponCategory, GameUnitClass), AIRosterCoverage>();
            var classesWithReach = new HashSet<GameUnitClass>();
            foreach (AIRosterProfile defenderProfile in roster.Profiles)
            {
                UnitData defender = defenderProfile.Unit;
                if (defender == null || defender == attacker)
                    continue;
                classSizes.TryGetValue(defender.unitClass, out int classSize);
                classSizes[defender.unitClass] = classSize + 1;

                UnitCounterEvaluator.Evaluation evaluation =
                    UnitCounterEvaluator.EvaluateBestAuto(attacker, defender, rps, dpq, priorities);
                if (!evaluation.IsValid)
                    continue;

                classesWithReach.Add(defender.unitClass);
                var key = (evaluation.WeaponCategory, defender.unitClass);
                if (!byMatchup.TryGetValue(key, out AIRosterCoverage row))
                {
                    row = new AIRosterCoverage
                    {
                        HasWeapon = true,
                        WeaponCategory = evaluation.WeaponCategory,
                        TargetClass = defender.unitClass,
                    };
                    byMatchup.Add(key, row);
                }
                row.Coverage += evaluation.Coverage;
                row.Reach++;
                if (!evaluation.Survives)
                    row.Deaths++;
            }

            foreach (AIRosterCoverage row in byMatchup.Values)
            {
                row.ClassSize = classSizes.TryGetValue(row.TargetClass, out int size)
                    ? Mathf.Max(1, size)
                    : 1;
                row.Coverage /= row.ClassSize;
                attackerProfile.CoverageMatrix.Add(row);
            }
            foreach (KeyValuePair<GameUnitClass, int> entry in classSizes)
                if (!classesWithReach.Contains(entry.Key))
                    attackerProfile.CoverageMatrix.Add(new AIRosterCoverage
                    {
                        HasWeapon = false,
                        TargetClass = entry.Key,
                        Coverage = 0f,
                        Reach = 0,
                        ClassSize = entry.Value,
                    });

            attackerProfile.CoverageMatrix.Sort((a, b) => b.Coverage.CompareTo(a.Coverage));
        }
    }

    private static UnitData FindBestRosterCounter(
        AIRosterKnowledge roster,
        CounterPressureInspection pressure,
        WeaponCategory category,
        GameUnitClass? targetClass,
        bool availableNow,
        int minElite,
        int maxElite)
    {
        if (roster == null)
            return null;

        UnitData best = null;
        float bestScore = float.MinValue;
        foreach (AIRosterProfile profile in roster.Profiles)
        {
            UnitData unit = profile.Unit;
            if (unit == null || !profile.HasWeaponCategory(category)
                || unit.eliteLevel < minElite || unit.eliteLevel > maxElite
                || (availableNow && !profile.AvailableNow))
                continue;
            if (targetClass.HasValue
                && unit.ResolveAiTargetPriorityForTargetClass(targetClass.Value)
                    < BazookaTargetPriority.Primary)
                continue;

            float fit = ScoreCounterFitForDemand(unit, pressure, category, targetClass);
            float economicFit = unit.cost > 0 ? fit * 10000f / unit.cost : fit;
            float score = fit * 100f + economicFit * 8f;
            if (score > bestScore)
            {
                bestScore = score;
                best = unit;
            }
        }
        return best;
    }

    private static void LogRosterKnowledge(AIWorldSnapshot snapshot, AIRosterKnowledge roster)
    {
        var log = new StringBuilder();
        log.Append($"[AI Roster][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
            + $"catalogo={roster.Profiles.Count}");
        foreach (AIRosterProfile profile in roster.Profiles)
        {
            UnitData unit = profile.Unit;
            log.Append($"\n  {unit.displayName} id={unit.id} role={profile.CompositionRole} "
                + $"elite={unit.eliteLevel} cost={unit.cost} "
                + $"status={(profile.AvailableNow ? "available" : profile.Unlockable ? "unlockable" : "blocked")} "
                + $"weapons=[{string.Join(",", profile.WeaponCategories)}]");
        }
        Debug.Log(log.ToString());
    }

    private sealed class OwnCounterCandidate
    {
        public UnitManager Manager;
        public UnitData Data;
    }

    public static CounterPressureInspection InspectCounterPressure(AIWorldSnapshot snapshot)
        => BuildCounterPressure(snapshot, BuildRosterKnowledge(snapshot, log: false));

    public static float InspectCounterFit(UnitData unit, CounterPressureInspection pressure)
        => ScoreCounterFit(unit, pressure);

    private static CounterPressureInspection BuildCounterPressure(
        AIWorldSnapshot snapshot,
        AIRosterKnowledge roster = null)
    {
        var result = new CounterPressureInspection();
        if (snapshot == null)
            return result;
        if (roster == null)
            roster = BuildRosterKnowledge(snapshot, log: false);
        counterEvaluationCache.Clear();

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
            AIIntelLedger.GetThreatSignals(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
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
                // Sem a identidade do atacante nao ha linha exata na matriz. Trata o
                // evento como uma estimativa de 0..1 unidade de cobertura equivalente,
                // preservando a escala oficial em vez de reintroduzir pontos arbitrarios.
                float score = Mathf.Clamp(
                    0.2f + signal.damage * 0.04f + signal.kills * 0.4f
                    + signal.destroyedValue / 20000f,
                    0.1f, 1f) * recency;
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
            float score = ComputeEnemyCounterWeight(enemy, data, roster);
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
                float score = ComputeEnemyCounterWeight(null, data, roster) * recency * confidence;
                AddPressure(result, byClass, data, score, visible: false);
            }
        }

        result.Classes.Sort((a, b) => b.Score.CompareTo(a.Score));
        ApplyOwnedCounterCoverage(snapshot, result);
        return result;
    }

    private static void ApplyOwnedCounterCoverage(
        AIWorldSnapshot snapshot, CounterPressureInspection pressure)
    {
        pressure.RawAntiInfantry = pressure.AntiInfantry;
        pressure.RawAntiTank = pressure.AntiTank;
        pressure.RawAntiAir = pressure.AntiAir;
        pressure.RawAntiShip = pressure.AntiShip;

        var ownCounters = new List<OwnCounterCandidate>();
        if (snapshot?.MyUnits != null)
        foreach (UnitManager own in snapshot.MyUnits)
        {
            if (own == null || own.IsDead || !own.TryGetUnitData(out UnitData data) || data == null)
                continue;
            ownCounters.Add(new OwnCounterCandidate { Manager = own, Data = data });
        }

        // As pecas mais decisivas alocam cobertura primeiro. Cada unidade cobre uma classe
        // favorita por snapshot; Tanque A contra artilharia nao e contado de novo contra veiculo.
        ownCounters.Sort((a, b) => ComputeBestCounterCoverageFit(b.Data, pressure)
            .CompareTo(ComputeBestCounterCoverageFit(a.Data, pressure)));
        foreach (OwnCounterCandidate candidate in ownCounters)
        {
            UnitData data = candidate.Data;
            EnemyClassPressureInspection best = null;
            float bestNeed = 0f;
            foreach (EnemyClassPressureInspection enemyClass in pressure.Classes)
            {
                float fit = ComputeCounterCoverageFit(data, enemyClass);
                if (fit <= 0f)
                    continue;
                float unmet = Mathf.Max(0f, enemyClass.Score - enemyClass.Coverage);
                float need = unmet * fit;
                if (need > bestNeed)
                {
                    best = enemyClass;
                    bestNeed = need;
                }
            }
            if (best == null)
                continue;
            float available = Mathf.Max(0f, best.Score - best.Coverage);
            float contribution = Mathf.Min(available,
                ComputeCounterCoverageFit(data, best));
            best.Coverage += contribution;
            if (contribution > 0f)
                pressure.OwnContributions.Add(new OwnCounterContributionInspection
                {
                    UnitInstanceId = candidate.Manager != null
                        ? candidate.Manager.InstanceId
                        : 0,
                    UnitName = data.displayName,
                    EliteLevel = data.eliteLevel,
                    TargetClass = best.UnitClass,
                    Category = best.CounterCategory,
                    Coverage = contribution,
                });
        }

        foreach (EnemyClassPressureInspection enemyClass in pressure.Classes)
        {
            enemyClass.Unmet = Mathf.Max(0f, enemyClass.Score - enemyClass.Coverage);
            switch (enemyClass.CounterCategory)
            {
                case WeaponCategory.AntiInfantaria:
                    pressure.AntiInfantryCoverage += enemyClass.Coverage; break;
                case WeaponCategory.AntiTanque:
                    pressure.AntiTankCoverage += enemyClass.Coverage; break;
                case WeaponCategory.AntiAerea:
                    pressure.AntiAirCoverage += enemyClass.Coverage; break;
                case WeaponCategory.AntiNavio:
                    pressure.AntiShipCoverage += enemyClass.Coverage; break;
            }
        }

        pressure.AntiInfantry = Mathf.Max(0f,
            pressure.RawAntiInfantry - pressure.AntiInfantryCoverage);
        pressure.AntiTank = Mathf.Max(0f,
            pressure.RawAntiTank - pressure.AntiTankCoverage);
        pressure.AntiAir = Mathf.Max(0f,
            pressure.RawAntiAir - pressure.AntiAirCoverage);
        pressure.AntiShip = Mathf.Max(0f,
            pressure.RawAntiShip - pressure.AntiShipCoverage);
    }

    private static float ComputeBestCounterCoverageFit(
        UnitData unit,
        CounterPressureInspection pressure)
    {
        float best = 0f;
        if (unit == null || pressure == null)
            return best;
        foreach (EnemyClassPressureInspection enemyClass in pressure.Classes)
            best = Mathf.Max(best, ComputeCounterCoverageFit(unit, enemyClass));
        return best;
    }

    private static float ComputeCounterCoverageFit(UnitData unit, EnemyClassPressureInspection enemyClass)
    {
        if (unit == null || enemyClass == null || enemyClass.UnitTypeScores.Count == 0)
            return 0f;

        ResolveCounterCombatDatabases(out RPSDatabase rps, out DPQMatchupDatabase dpq,
            out WeaponPriorityData priorities);
        float weightedFit = 0f;
        float totalWeight = 0f;
        foreach (KeyValuePair<UnitData, float> pair in enemyClass.UnitTypeScores)
        {
            if (pair.Key == null || pair.Value <= 0f)
                continue;
            var cacheKey = (unit, pair.Key);
            if (!counterEvaluationCache.TryGetValue(cacheKey, out UnitCounterEvaluator.Evaluation evaluation))
            {
                evaluation = UnitCounterEvaluator.EvaluateBestAuto(
                    unit, pair.Key, rps, dpq, priorities);
                counterEvaluationCache[cacheKey] = evaluation;
            }
            float fit = evaluation.IsValid && evaluation.WeaponCategory == enemyClass.CounterCategory
                ? evaluation.Coverage
                : 0f;
            weightedFit += fit * pair.Value;
            totalWeight += pair.Value;
        }
        return totalWeight > 0f ? Mathf.Clamp01(weightedFit / totalWeight) : 0f;
    }

    private static TurnStateManager counterTurnStateManager;

    private static void ResolveCounterCombatDatabases(out RPSDatabase rps,
        out DPQMatchupDatabase dpq, out WeaponPriorityData priorities)
    {
        if (counterTurnStateManager == null)
            counterTurnStateManager = UnityEngine.Object.FindAnyObjectByType<TurnStateManager>();
        rps = counterTurnStateManager != null ? counterTurnStateManager.RpsDatabaseRef : null;
        dpq = counterTurnStateManager != null ? counterTurnStateManager.DpqMatchupDatabaseRef : null;
        priorities = counterTurnStateManager != null ? counterTurnStateManager.WeaponPriorityDataRef : null;
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
        entry.UnitTypeScores.TryGetValue(data, out float typeScore);
        entry.UnitTypeScores[data] = typeScore + score;
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

    private static float ComputeEnemyCounterWeight(
        UnitManager enemy,
        UnitData data,
        AIRosterKnowledge roster)
    {
        if (data == null)
            return 0f;
        WeaponCategory category = CounterCategoryFor(data.unitClass);
        float matrixCoverage = ResolveRosterMatrixCoverage(
            data, category, data.unitClass, roster);
        float hpRatio = enemy != null && data.maxHP > 0
            ? Mathf.Clamp01(enemy.CurrentHP / (float)data.maxHP)
            : 1f;
        return matrixCoverage * hpRatio;
    }

    private static float ResolveRosterMatrixCoverage(
        UnitData attacker,
        WeaponCategory category,
        GameUnitClass targetClass,
        AIRosterKnowledge roster)
    {
        if (attacker == null || roster == null)
            return 0f;
        if (roster.TryGetProfile(attacker, out AIRosterProfile profile))
            foreach (AIRosterCoverage row in profile.CoverageMatrix)
                if (row.HasWeapon && row.WeaponCategory == category
                    && row.TargetClass == targetClass)
                    return row.Coverage;

        ResolveCounterCombatDatabases(out RPSDatabase rps, out DPQMatchupDatabase dpq,
            out WeaponPriorityData priorities);
        float sum = 0f;
        int classSize = 0;
        foreach (AIRosterProfile defenderProfile in roster.Profiles)
        {
            UnitData defender = defenderProfile.Unit;
            if (defender == null || defender == attacker || defender.unitClass != targetClass)
                continue;
            classSize++;
            UnitCounterEvaluator.Evaluation evaluation = UnitCounterEvaluator.EvaluateBestAuto(
                attacker, defender, rps, dpq, priorities);
            if (evaluation.IsValid && evaluation.WeaponCategory == category)
                sum += evaluation.Coverage;
        }
        return classSize > 0 ? Mathf.Clamp01(sum / classSize) : 0f;
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

    private static void AddCounterPressureDemands(
        AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands,
        CounterPressureInspection pressure,
        AIRosterKnowledge roster)
    {
        if (snapshot == null || demands == null || pressure == null)
            return;

        AddCounterCategoryDemands(snapshot, demands, pressure,
            WeaponCategory.AntiTanque, pressure.RawAntiTank,
            pressure.AntiTankCoverage, pressure.AntiTank, 11, 13, roster);
        AddCounterCategoryDemands(snapshot, demands, pressure,
            WeaponCategory.AntiInfantaria, pressure.RawAntiInfantry,
            pressure.AntiInfantryCoverage, pressure.AntiInfantry, 12, 14, roster);
    }

    private static void AddCounterCategoryDemands(
        AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands,
        CounterPressureInspection pressure,
        WeaponCategory category,
        float rawPressure,
        float coverage,
        float aggregateUnmet,
        int classPriority,
        int anonymousPriority,
        AIRosterKnowledge roster)
    {
        float escalationThreshold = Instance != null
            ? Mathf.Max(1f, Instance.CounterEliteEscalationPressure)
            : 8f;

        // A decisao de parar de comprar pecas baratas pertence a categoria inteira.
        // Sem isso, uma pressao anti-tank alta fragmentada entre Armored, Artillery,
        // Vehicle e sinais anonimos nunca alcança o limiar elite em ramo algum.
        if (aggregateUnmet >= escalationThreshold)
        {
            GameUnitClass? targetClass = FindAggregateEliteCounterTarget(
                snapshot, pressure, category, roster);
            UnitData immediate = FindBestRosterCounter(
                roster, pressure, category, targetClass,
                availableNow: true, minElite: 0, maxElite: 0);
            if (immediate != null)
            {
                AddCounterPressureDemand(snapshot, demands, pressure,
                    category, targetClass, rawPressure, coverage, aggregateUnmet,
                    Mathf.Max(1, classPriority - 7), roster,
                    forceBasicResponse: true);
            }
            AddCounterPressureDemand(snapshot, demands, pressure,
                category, targetClass, rawPressure, coverage, aggregateUnmet,
                classPriority, roster, forceEliteEscalation: true);
            return;
        }

        float classifiedUnmet = 0f;
        foreach (EnemyClassPressureInspection enemyClass in pressure.Classes)
        {
            if (enemyClass.CounterCategory != category)
                continue;
            classifiedUnmet += enemyClass.Unmet;
            AddCounterPressureDemand(snapshot, demands, pressure,
                category, enemyClass.UnitClass, enemyClass.Score,
                enemyClass.Coverage, enemyClass.Unmet, classPriority, roster);
        }

        // Sinais anonimos pequenos ainda podem pedir resposta comum, mas nunca criam
        // uma compra barata paralela quando a categoria agregada já escalou para elite.
        AddCounterPressureDemand(snapshot, demands, pressure,
            category, null, rawPressure, coverage,
            Mathf.Max(0f, aggregateUnmet - classifiedUnmet), anonymousPriority, roster);
    }

    private static GameUnitClass? FindAggregateEliteCounterTarget(
        AIWorldSnapshot snapshot,
        CounterPressureInspection pressure,
        WeaponCategory category,
        AIRosterKnowledge roster)
    {
        EnemyClassPressureInspection bestWithElite = null;
        EnemyClassPressureInspection bestKnown = null;
        foreach (EnemyClassPressureInspection enemyClass in pressure.Classes)
        {
            if (enemyClass.CounterCategory != category || enemyClass.Unmet <= 0.05f)
                continue;
            if (bestKnown == null || enemyClass.Unmet > bestKnown.Unmet)
                bestKnown = enemyClass;
            if (FindBestEliteCounter(snapshot, pressure, roster, category, enemyClass.UnitClass,
                    requireAvailableChain: false) != null
                && (bestWithElite == null || enemyClass.Unmet > bestWithElite.Unmet))
                bestWithElite = enemyClass;
        }

        // Prefere a maior subameaça que possua counter elite favorito. Se a inteligência
        // só tem sinais anônimos (ou nenhuma classe tem elite dedicado), deixa o alvo
        // aberto e escolhe a melhor unidade elite da categoria.
        if (bestWithElite != null)
            return bestWithElite.UnitClass;
        return bestKnown != null
            && FindBestEliteCounter(snapshot, pressure, roster, category, bestKnown.UnitClass,
                requireAvailableChain: false) != null
            ? bestKnown.UnitClass
            : (GameUnitClass?)null;
    }

    private static void AddCounterPressureDemand(
        AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands,
        CounterPressureInspection pressure,
        WeaponCategory category,
        GameUnitClass? targetClass,
        float rawPressure,
        float coverage,
        float unmetPressure,
        int rememberedPriority,
        AIRosterKnowledge roster,
        bool forceEliteEscalation = false,
        bool forceBasicResponse = false)
    {
        // Evita transformar residuo de ponto flutuante (exibido como 0,0) em compra real.
        if (unmetPressure <= 0.05f)
            return;

        float escalationThreshold = Instance != null
            ? Mathf.Max(1f, Instance.CounterEliteEscalationPressure)
            : 8f;
        bool highPressure = !forceBasicResponse && (forceEliteEscalation
            || unmetPressure >= escalationThreshold);
        UnitData potentialElite = highPressure
            ? FindBestEliteCounter(snapshot, pressure, roster, category, targetClass,
                requireAvailableChain: false)
            : null;
        UnitData availableElite = highPressure
            ? FindBestEliteCounter(snapshot, pressure, roster, category, targetClass,
                requireAvailableChain: true)
            : null;
        bool eliteCounterExists = potentialElite != null;
        bool escalate = availableElite != null;
        float basicCoverage = ResolveBasicCounterCoverage(
            roster, pressure, category, targetClass);
        int basicCount = Mathf.Min(2,
            Mathf.Max(1, Mathf.CeilToInt(unmetPressure / basicCoverage)));
        int priority = pressure.RememberedUnits > 0
            ? rememberedPriority
            : rememberedPriority + 4;

        AIShoppingDemand counterDemand = NewRoleDemand(
            UnitRole.None,
            forceBasicResponse
                ? basicCount
                : escalate || eliteCounterExists
                ? 1
                : basicCount,
            escalate ? Mathf.Max(1, priority - 3) : priority,
            escalate ? "counter-pressure-elite"
                : eliteCounterExists ? "counter-pressure-prerequisite" : "counter-pressure",
            $"{category}/{(targetClass.HasValue ? targetClass.Value.ToString() : "desconhecido")}"
                + $" bruto={rawPressure:F1} cobertura={coverage:F1} saldo={unmetPressure:F1}"
                + $" vis={pressure.VisibleUnits} memoria={pressure.RememberedUnits}",
            forceBasicResponse);
        counterDemand.RequiredWeaponCategory = category;
        if (forceBasicResponse)
            counterDemand.MaxEliteLevel = 0;
        counterDemand.TargetClass = targetClass;
        if (targetClass.HasValue)
            counterDemand.MinTargetPriority = BazookaTargetPriority.Primary;
        if (escalate)
        {
            counterDemand.MinEliteLevel = availableElite.eliteLevel;
            counterDemand.MaxEliteLevel = availableElite.eliteLevel;
            counterDemand.RequiredUnitId = availableElite.id;
            counterDemand.StrategicEscalation = true;
        }
        else if (eliteCounterExists && potentialElite.eliteFrom != null)
        {
            // Compra exatamente o elo que libera o counter elite, nao duas pecas baratas
            // quaisquer que apenas compartilham a categoria da arma.
            counterDemand.RequiredUnitId = potentialElite.eliteFrom.id;
            counterDemand.MinTargetPriority = BazookaTargetPriority.Tertiary;
        }
        MergeRoleDemand(demands, counterDemand, false);
    }

    private static float ResolveBasicCounterCoverage(
        AIRosterKnowledge roster,
        CounterPressureInspection pressure,
        WeaponCategory category,
        GameUnitClass? targetClass)
    {
        UnitData basic = FindBestRosterCounter(
            roster, pressure, category, targetClass,
            availableNow: true, minElite: 0, maxElite: 0);
        if (basic == null)
            return 0.2f;

        // Mesma nota 0..1 usada pela matriz do Unit Analysis. Assim a quantidade
        // pedida e a cobertura exibida falam exatamente a mesma unidade de medida.
        float coverage = 0f;
        foreach (EnemyClassPressureInspection enemyClass in pressure.Classes)
        {
            if (enemyClass.CounterCategory != category
                || (targetClass.HasValue && enemyClass.UnitClass != targetClass.Value))
                continue;
            coverage = Mathf.Max(coverage,
                ComputeCounterCoverageFit(basic, enemyClass));
        }
        return Mathf.Max(0.05f, coverage);
    }

    private static UnitData FindBestEliteCounter(
        AIWorldSnapshot snapshot,
        CounterPressureInspection pressure,
        AIRosterKnowledge roster,
        WeaponCategory category,
        GameUnitClass? targetClass,
        bool requireAvailableChain)
    {
        return FindBestRosterCounter(
            roster, pressure, category, targetClass,
            availableNow: requireAvailableChain,
            minElite: 1, maxElite: int.MaxValue);
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

            // O carrinho reage apenas ao saldo ainda descoberto. Depois que um counter
            // poderoso cobre Armored, esse matchup deixa de inflar novas compras e outra
            // classe passa a disputar o carrinho.
            score += enemyClass.Unmet * ComputeCounterCoverageFit(unit, enemyClass);
        }
        return score;
    }

    private static float ScoreCounterFitForDemand(
        UnitData unit,
        CounterPressureInspection pressure,
        WeaponCategory? requiredCategory,
        GameUnitClass? targetClass)
    {
        // Demandas genericas de composicao nao podem absorver a pressao global de
        // counters. Esse bonus pertence somente ao matchup explicitamente pedido.
        if (unit == null || pressure == null || !requiredCategory.HasValue
            || unit.embarkedWeapons == null)
            return 0f;

        bool hasRequiredWeapon = false;
        foreach (UnitEmbarkedWeapon weapon in unit.embarkedWeapons)
        {
            if (weapon?.weapon != null
                && weapon.weapon.WeaponCategory == requiredCategory.Value)
            {
                hasRequiredWeapon = true;
                break;
            }
        }
        if (!hasRequiredWeapon)
            return 0f;

        float score = 0f;
        foreach (EnemyClassPressureInspection enemyClass in pressure.Classes)
        {
            if (enemyClass.CounterCategory != requiredCategory.Value)
                continue;
            if (targetClass.HasValue && enemyClass.UnitClass != targetClass.Value)
                continue;
            score += enemyClass.Unmet * ComputeCounterCoverageFit(unit, enemyClass);
        }
        return score;
    }
}
