using System.Collections.Generic;
using System;
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

    private sealed class OwnCounterCandidate
    {
        public UnitManager Manager;
        public UnitData Data;
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
        ownCounters.Sort((a, b) => ComputeCounterPowerBase(b.Data)
            .CompareTo(ComputeCounterPowerBase(a.Data)));
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
                ComputeCounterPowerBase(data) * ComputeCounterCoverageFit(data, best));
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

    private static float ComputeCounterPowerBase(UnitData unit)
    {
        float basicCoverage = Instance != null
            ? Mathf.Max(1f, Instance.BasicCounterPressureCoverage)
            : 4f;
        float eliteMultiplier = 1f + Mathf.Max(0, unit.eliteLevel) * 0.9f;
        float valueMultiplier = Mathf.Lerp(0.85f, 1.35f,
            Mathf.Clamp01(unit.cost / 15000f));
        return basicCoverage * eliteMultiplier * valueMultiplier;
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

    private static void AddCounterPressureDemands(
        AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands,
        CounterPressureInspection pressure)
    {
        if (snapshot == null || demands == null || pressure == null)
            return;

        AddCounterCategoryDemands(snapshot, demands, pressure,
            WeaponCategory.AntiTanque, pressure.RawAntiTank,
            pressure.AntiTankCoverage, pressure.AntiTank, 11, 13);
        AddCounterCategoryDemands(snapshot, demands, pressure,
            WeaponCategory.AntiInfantaria, pressure.RawAntiInfantry,
            pressure.AntiInfantryCoverage, pressure.AntiInfantry, 12, 14);
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
        int anonymousPriority)
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
                snapshot, pressure, category);
            AddCounterPressureDemand(snapshot, demands, pressure,
                category, targetClass, rawPressure, coverage, aggregateUnmet,
                classPriority, forceEliteEscalation: true);
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
                enemyClass.Coverage, enemyClass.Unmet, classPriority);
        }

        // Sinais anonimos pequenos ainda podem pedir resposta comum, mas nunca criam
        // uma compra barata paralela quando a categoria agregada já escalou para elite.
        AddCounterPressureDemand(snapshot, demands, pressure,
            category, null, rawPressure, coverage,
            Mathf.Max(0f, aggregateUnmet - classifiedUnmet), anonymousPriority);
    }

    private static GameUnitClass? FindAggregateEliteCounterTarget(
        AIWorldSnapshot snapshot,
        CounterPressureInspection pressure,
        WeaponCategory category)
    {
        EnemyClassPressureInspection bestWithElite = null;
        EnemyClassPressureInspection bestKnown = null;
        foreach (EnemyClassPressureInspection enemyClass in pressure.Classes)
        {
            if (enemyClass.CounterCategory != category || enemyClass.Unmet <= 0.05f)
                continue;
            if (bestKnown == null || enemyClass.Unmet > bestKnown.Unmet)
                bestKnown = enemyClass;
            if (FindBestEliteCounter(snapshot, category, enemyClass.UnitClass,
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
            && FindBestEliteCounter(snapshot, category, bestKnown.UnitClass,
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
        bool forceEliteEscalation = false)
    {
        // Evita transformar residuo de ponto flutuante (exibido como 0,0) em compra real.
        if (unmetPressure <= 0.05f)
            return;

        float escalationThreshold = Instance != null
            ? Mathf.Max(1f, Instance.CounterEliteEscalationPressure)
            : 8f;
        bool highPressure = forceEliteEscalation
            || unmetPressure >= escalationThreshold;
        UnitData potentialElite = highPressure
            ? FindBestEliteCounter(snapshot, category, targetClass,
                requireAvailableChain: false)
            : null;
        UnitData availableElite = highPressure
            ? FindBestEliteCounter(snapshot, category, targetClass,
                requireAvailableChain: true)
            : null;
        bool eliteCounterExists = potentialElite != null;
        bool escalate = availableElite != null;
        int priority = pressure.RememberedUnits > 0
            ? rememberedPriority
            : rememberedPriority + 4;

        AIShoppingDemand counterDemand = NewRoleDemand(
            UnitRole.None,
            escalate || eliteCounterExists
                ? 1
                : Mathf.Min(2, Mathf.CeilToInt(unmetPressure /
                    (Instance != null ? Mathf.Max(1f, Instance.BasicCounterPressureCoverage) : 4f))),
            escalate ? Mathf.Max(1, priority - 3) : priority,
            escalate ? "counter-pressure-elite"
                : eliteCounterExists ? "counter-pressure-prerequisite" : "counter-pressure",
            $"{category}/{(targetClass.HasValue ? targetClass.Value.ToString() : "desconhecido")}"
                + $" bruto={rawPressure:F1} cobertura={coverage:F1} saldo={unmetPressure:F1}"
                + $" vis={pressure.VisibleUnits} memoria={pressure.RememberedUnits}",
            false);
        counterDemand.RequiredWeaponCategory = category;
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

    private static UnitData FindBestEliteCounter(
        AIWorldSnapshot snapshot,
        WeaponCategory category,
        GameUnitClass? targetClass,
        bool requireAvailableChain)
    {
        if (snapshot?.MyBuildings == null)
            return null;
        UnitData best = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)
                || building.OfferedUnits == null)
                continue;
            foreach (UnitData unit in building.OfferedUnits)
                if (unit != null && unit.eliteLevel > 0
                    && HasWeaponCategory(unit, category)
                    && (!targetClass.HasValue
                        || unit.ResolveAiTargetPriorityForTargetClass(targetClass.Value)
                            == BazookaTargetPriority.Primary)
                    && (!requireAvailableChain || IsEliteChainAvailable(unit, snapshot))
                    && IsRolePurchaseAllowed(unit, snapshot.Stance, emergency: true))
                {
                    bool better = best == null;
                    if (!better && requireAvailableChain)
                    {
                        // Com a cadeia aberta, sobe para a resposta mais poderosa: Medio ->
                        // Campanha, em vez de repetir o primeiro tier elite para sempre.
                        better = ComputeCounterPowerBase(unit) > ComputeCounterPowerBase(best);
                    }
                    else if (!better)
                    {
                        // Sem cadeia aberta, persegue primeiro o degrau mais proximo.
                        better = unit.eliteLevel < best.eliteLevel
                            || (unit.eliteLevel == best.eliteLevel
                                && ComputeCounterPowerBase(unit) > ComputeCounterPowerBase(best));
                    }
                    if (better)
                        best = unit;
                }
        }
        return best;
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
}
