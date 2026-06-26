using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-240)]
public sealed class MatchStatsManager : MonoBehaviour
{
    public static MatchStatsManager Instance { get; private set; }

    [SerializeField] private bool logLifecycle;
    [SerializeField] private List<SlotMatchStats> slotStats = new List<SlotMatchStats>();
    [SerializeField] private int lastJogadasCount;
    [SerializeField] private int lastPurchaseJogadasCount;
    [SerializeField] private int lastAttackJogadasCount;
    [SerializeField] private int lastCaptureJogadasCount;

    private readonly Dictionary<TeamId, SlotMatchStats> statsByTeam = new Dictionary<TeamId, SlotMatchStats>();
    private readonly HashSet<int> purchasedUnitIds = new HashSet<int>();
    private readonly HashSet<int> destroyedUnitIds = new HashSet<int>();
    private bool subscribed;

    public IReadOnlyList<SlotMatchStats> SlotStats => slotStats;
    public int LastJogadasCount => lastJogadasCount;
    public int LastPurchaseJogadasCount => lastPurchaseJogadasCount;
    public int LastAttackJogadasCount => lastAttackJogadasCount;
    public int LastCaptureJogadasCount => lastCaptureJogadasCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        EnsureInstance();
    }

    public static MatchStatsManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        MatchStatsManager existing = FindFirstObjectByType<MatchStatsManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject go = new GameObject(nameof(MatchStatsManager));
        Instance = go.AddComponent<MatchStatsManager>();
        if (Application.isPlaying)
            DontDestroyOnLoad(go);
        return Instance;
    }

    public bool TryGetStats(TeamId team, out SlotMatchStats stats)
    {
        RefreshTerritoryStats();
        return statsByTeam.TryGetValue(team, out stats);
    }

    public SlotMatchStats GetOrCreateStats(TeamId team)
    {
        if (statsByTeam.TryGetValue(team, out SlotMatchStats stats) && stats != null)
            return stats;

        stats = new SlotMatchStats { team = team, teamName = TeamUtils.GetName(team) };
        statsByTeam[team] = stats;
        slotStats.Add(stats);
        SortSlotStats();
        return stats;
    }

    public void ResetStats()
    {
        slotStats.Clear();
        statsByTeam.Clear();
        purchasedUnitIds.Clear();
        destroyedUnitIds.Clear();
        lastJogadasCount = 0;
        lastPurchaseJogadasCount = 0;
        lastAttackJogadasCount = 0;
        lastCaptureJogadasCount = 0;
    }

    public void RebuildFromJogadas()
    {
        ResetStats();

        JogadasManager manager = JogadasManager.Instance;
        IReadOnlyList<Jogada> jogadas = manager != null && manager.log != null
            ? manager.log.jogadas
            : null;

        if (jogadas != null)
        {
            lastJogadasCount = jogadas.Count;
            for (int i = 0; i < jogadas.Count; i++)
            {
                CountJogadaKind(jogadas[i]);
                ApplyJogada(jogadas[i], fromReplay: true);
            }
        }

        RecountCurrentUnits();
        RefreshTerritoryStats();
        if (logLifecycle)
            Debug.Log($"[MatchStats] rebuild jogadas={jogadas?.Count ?? 0} slots={slotStats.Count}");
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (Application.isPlaying && transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        Subscribe();
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SaveGameManager.OnAfterLoadSuccess += HandleAfterLoadSuccess;
        RebuildFromJogadas();
    }

    private void OnDisable()
    {
        Unsubscribe();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SaveGameManager.OnAfterLoadSuccess -= HandleAfterLoadSuccess;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        JogadasLog.OnJogadaRegistrada += HandleJogadaRegistrada;
        TurnStateManager.OnUnitDestroyed += HandleUnitDestroyed;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        JogadasLog.OnJogadaRegistrada -= HandleJogadaRegistrada;
        TurnStateManager.OnUnitDestroyed -= HandleUnitDestroyed;
        subscribed = false;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebuildFromJogadas();
    }

    private void HandleAfterLoadSuccess()
    {
        RebuildFromJogadas();
    }

    private void HandleJogadaRegistrada(Jogada jogada)
    {
        ApplyJogada(jogada, fromReplay: false);
        RefreshTerritoryStats();
    }

    private void HandleUnitDestroyed(UnitManager unit)
    {
        if (unit == null || unit.InstanceId <= 0 || destroyedUnitIds.Contains(unit.InstanceId))
            return;

        destroyedUnitIds.Add(unit.InstanceId);
        SlotMatchStats ownerStats = GetOrCreateStats(unit.TeamId);
        UnitTypeMatchStats typeStats = ownerStats.GetOrCreateUnitStats(ResolveUnitKey(unit, out UnitData data), data, unit);
        typeStats.lost++;
        typeStats.valueLost += ResolveCost(data);
        RecountCurrentUnits();
    }

    private void ApplyJogada(Jogada jogada, bool fromReplay)
    {
        if (jogada == null)
            return;

        string action = jogada.acao ?? string.Empty;
        if (string.Equals(action, "Compra", StringComparison.OrdinalIgnoreCase))
        {
            ApplyPurchase(jogada);
            return;
        }

        if (string.Equals(action, "Ataque", StringComparison.OrdinalIgnoreCase))
        {
            ApplyAttack(jogada);
            return;
        }

        if (string.Equals(action, "Capturar", StringComparison.OrdinalIgnoreCase))
        {
            ApplyCapture(jogada);
            return;
        }

        if (string.Equals(action, "Embarque", StringComparison.OrdinalIgnoreCase))
        {
            GetOrCreateStats(ToTeam(jogada.team)).embarkActions++;
            return;
        }

        if (string.Equals(action, "Desembarque", StringComparison.OrdinalIgnoreCase))
        {
            GetOrCreateStats(ToTeam(jogada.team)).disembarkActions++;
            return;
        }

        if (string.Equals(action, "Fusao", StringComparison.OrdinalIgnoreCase))
        {
            GetOrCreateStats(ToTeam(jogada.team)).mergeActions++;
            return;
        }

        if (string.Equals(action, "Reparo", StringComparison.OrdinalIgnoreCase))
        {
            ApplyRepairState(jogada);
            return;
        }

        if (string.Equals(action, "ServicoLogistico", StringComparison.OrdinalIgnoreCase))
        {
            ApplyServiceResult(jogada);
        }
    }

    private void CountJogadaKind(Jogada jogada)
    {
        if (jogada == null)
            return;

        string action = jogada.acao ?? string.Empty;
        if (string.Equals(action, "Compra", StringComparison.OrdinalIgnoreCase))
            lastPurchaseJogadasCount++;
        else if (string.Equals(action, "Ataque", StringComparison.OrdinalIgnoreCase))
            lastAttackJogadasCount++;
        else if (string.Equals(action, "Capturar", StringComparison.OrdinalIgnoreCase))
            lastCaptureJogadasCount++;
    }

    private void ApplyPurchase(Jogada jogada)
    {
        if (jogada.uid > 0 && purchasedUnitIds.Contains(jogada.uid))
            return;

        if (jogada.uid > 0)
            purchasedUnitIds.Add(jogada.uid);

        TeamId team = ToTeam(jogada.team);
        UnitManager unit = FindUnit(jogada.uid);
        UnitData data = null;
        unit?.TryGetUnitData(out data);

        SlotMatchStats stats = GetOrCreateStats(team);
        UnitTypeMatchStats typeStats = stats.GetOrCreateUnitStats(ResolveUnitKey(jogada.unidadeSigla, unit, data), data, unit);
        typeStats.purchased++;
        typeStats.valuePurchased += ResolveCost(data);
        stats.totalPurchases++;
        stats.totalSpent += ResolveCost(data);
        RecountCurrentUnits();
    }

    private void ApplyAttack(Jogada jogada)
    {
        TeamId attackerTeam = ToTeam(jogada.team);
        TeamId defenderTeam = ToTeam(jogada.team2);
        SlotMatchStats attackerStats = GetOrCreateStats(attackerTeam);
        SlotMatchStats defenderStats = GetOrCreateStats(defenderTeam);

        if (jogada.hasHpState)
        {
            int damageToDefender = Mathf.Max(0, jogada.hp2Antes - jogada.hp2Depois);
            int damageToAttacker = Mathf.Max(0, jogada.hpAntes - jogada.hpDepois);
            attackerStats.damageCaused += damageToDefender;
            attackerStats.damageReceived += damageToAttacker;
            defenderStats.damageCaused += damageToAttacker;
            defenderStats.damageReceived += damageToDefender;
        }

        if (jogada.hp2Antes > 0 && jogada.hp2Depois <= 0)
            RecordKill(attackerTeam, defenderTeam, jogada.uid2, jogada.unidadeSigla2, jogada.defenderCost, jogada.defenderEliteLevel);

        if (jogada.hpAntes > 0 && jogada.hpDepois <= 0)
            RecordKill(defenderTeam, attackerTeam, jogada.uid, jogada.unidadeSigla, 0, 0);

        if (jogada.combatCargo != null)
        {
            for (int i = 0; i < jogada.combatCargo.Count; i++)
            {
                CombatCargoResult cargo = jogada.combatCargo[i];
                if (cargo == null || cargo.hpAntes <= 0 || cargo.hpDepois > 0)
                    continue;

                RecordKill(attackerTeam, ToTeam(cargo.team), cargo.uid, cargo.sigla, cargo.cost, cargo.eliteLevel, cargo.unitClass);
            }
        }
    }

    private void ApplyCapture(Jogada jogada)
    {
        SlotMatchStats stats = GetOrCreateStats(ToTeam(jogada.team));
        stats.captureActions++;
        if (!string.IsNullOrWhiteSpace(jogada.obs)
            && jogada.obs.IndexOf("capturado", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            stats.capturesCompleted++;
        }
        else if (!string.IsNullOrWhiteSpace(jogada.obs)
            && jogada.obs.IndexOf("reparado", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            stats.recaptureActions++;
        }
    }

    private void ApplyRepairState(Jogada jogada)
    {
        SlotMatchStats stats = GetOrCreateStats(ToTeam(jogada.team));
        if (!jogada.repairBefore && jogada.repairAfter)
            stats.unitsEnteredRepair++;
        else if (jogada.repairBefore && !jogada.repairAfter)
            stats.unitsLeftRepair++;
    }

    private void ApplyServiceResult(Jogada jogada)
    {
        if (jogada == null || !jogada.hasServiceResult)
            return;

        SlotMatchStats stats = GetOrCreateStats(ToTeam(jogada.team));
        int cost = Mathf.Max(0, jogada.serviceCost);
        int hp = Mathf.Max(0, jogada.serviceHpGain);
        int fuel = Mathf.Max(0, jogada.serviceFuelGain);
        int ammo = Mathf.Max(0, jogada.serviceAmmoGain);
        bool command = string.Equals(jogada.serviceSource, "ServicoComando", StringComparison.OrdinalIgnoreCase);
        if (command)
            stats.commandServiceCost += cost;
        else
            stats.fieldLogisticsCost += cost;

        stats.totalMaintenanceCost += cost;
        stats.serviceHpRecovered += hp;
        stats.serviceFuelRecovered += fuel;
        stats.serviceAmmoRecovered += ammo;
        AllocateServiceCostByGain(stats, cost, hp, fuel, ammo);
        if (hp > 0 || fuel > 0 || ammo > 0)
            stats.serviceApplications++;
    }

    private static void AllocateServiceCostByGain(SlotMatchStats stats, int cost, int hp, int fuel, int ammo)
    {
        if (stats == null || cost <= 0)
            return;

        int totalGain = Mathf.Max(0, hp) + Mathf.Max(0, fuel) + Mathf.Max(0, ammo);
        if (totalGain <= 0)
            return;

        int repairCost = Mathf.RoundToInt(cost * (Mathf.Max(0, hp) / (float)totalGain));
        int refuelCost = Mathf.RoundToInt(cost * (Mathf.Max(0, fuel) / (float)totalGain));
        int rearmCost = Mathf.Max(0, cost - repairCost - refuelCost);

        stats.repairServiceCost += repairCost;
        stats.refuelServiceCost += refuelCost;
        stats.rearmServiceCost += rearmCost;
    }

    private void RecordKill(
        TeamId killerTeam,
        TeamId victimTeam,
        int victimUid,
        string victimSigla,
        int victimCost,
        int victimEliteLevel,
        GameUnitClass fallbackClass = default)
    {
        if (victimUid > 0 && destroyedUnitIds.Contains(victimUid))
            return;

        if (victimUid > 0)
            destroyedUnitIds.Add(victimUid);

        UnitManager victim = FindUnit(victimUid);
        UnitData data = null;
        victim?.TryGetUnitData(out data);
        int resolvedCost = victimCost > 0 ? victimCost : ResolveCost(data);

        SlotMatchStats killerStats = GetOrCreateStats(killerTeam);
        SlotMatchStats victimStats = GetOrCreateStats(victimTeam);

        UnitTypeMatchStats killedType = killerStats.GetOrCreateUnitStats(
            ResolveUnitKey(victimSigla, victim, data),
            data,
            victim,
            victimSigla,
            fallbackClass);
        killedType.destroyedEnemy++;
        killedType.valueDestroyed += resolvedCost;

        UnitTypeMatchStats lostType = victimStats.GetOrCreateUnitStats(
            ResolveUnitKey(victimSigla, victim, data),
            data,
            victim,
            victimSigla,
            fallbackClass);
        lostType.lost++;
        lostType.valueLost += resolvedCost;

        killerStats.totalKills++;
        killerStats.totalDestroyedValue += resolvedCost;
        victimStats.totalLosses++;
        victimStats.totalLostValue += resolvedCost;
        if (victimEliteLevel > 0)
        {
            killerStats.eliteKills++;
            victimStats.eliteLosses++;
        }

        RecountCurrentUnits();
    }

    private void RecountCurrentUnits()
    {
        for (int i = 0; i < slotStats.Count; i++)
        {
            SlotMatchStats stats = slotStats[i];
            if (stats == null) continue;
            stats.currentUnits = 0;
            for (int u = 0; u < stats.units.Count; u++)
                stats.units[u].current = 0;
        }

        IReadOnlyList<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.IsDead)
                continue;

            UnitData data = null;
            unit.TryGetUnitData(out data);
            SlotMatchStats stats = GetOrCreateStats(unit.TeamId);
            UnitTypeMatchStats typeStats = stats.GetOrCreateUnitStats(ResolveUnitKey(unit, out data), data, unit);
            typeStats.current++;
            stats.currentUnits++;
        }
    }

    public void RefreshTerritoryStats()
    {
        for (int i = 0; i < slotStats.Count; i++)
            slotStats[i]?.ResetTerritory();

        int totalMapCapturePoints = 0;
        IReadOnlyList<SectorManager.SectorInfo> sectors = SectorManager.GetAllSectorInfos();
        IReadOnlyList<SectorManager.SectorInfo> bases = SectorManager.GetAllBaseInfos();
        AccumulateTerritory(sectors, countAsBase: false, ref totalMapCapturePoints);
        AccumulateTerritory(bases, countAsBase: true, ref totalMapCapturePoints);

        for (int i = 0; i < slotStats.Count; i++)
        {
            SlotMatchStats stats = slotStats[i];
            if (stats == null) continue;
            stats.totalCapturePointsMax = totalMapCapturePoints;
            stats.territoryControlPercent = stats.totalCapturePointsMax > 0
                ? stats.controlledCapturePoints * 100f / stats.totalCapturePointsMax
                : 0f;
        }
    }

    private void AccumulateTerritory(IReadOnlyList<SectorManager.SectorInfo> infos, bool countAsBase, ref int totalMapCapturePoints)
    {
        if (infos == null)
            return;

        for (int i = 0; i < infos.Count; i++)
        {
            SectorManager.SectorInfo info = infos[i];
            if (info == null)
                continue;

            IReadOnlyList<SectorManager.SectorConstructionInfo> constructions = info.Constructions;
            if (constructions != null)
            {
                for (int c = 0; c < constructions.Count; c++)
                {
                    SectorManager.SectorConstructionInfo construction = constructions[c];
                    if (construction == null)
                        continue;

                    int max = Mathf.Max(0, construction.CapturePointsMax);
                    int current = Mathf.Clamp(construction.CurrentCapturePoints, 0, max);
                    totalMapCapturePoints += max;
                    if (construction.OwnerTeam == TeamId.Neutral)
                        continue;

                    SlotMatchStats owner = GetOrCreateStats(construction.OwnerTeam);
                    owner.controlledConstructions++;
                    owner.controlledCapturePoints += current;
                    owner.contestedOwnedCapturePoints += Mathf.Max(0, max - current);

                    int contested = Mathf.Max(0, max - current);
                    if (contested > 0
                        && construction.Source != null
                        && construction.Source.CurrentOccupantOnTop != null
                        && construction.Source.CurrentOccupantOnTop.TeamId != TeamId.Neutral
                        && construction.Source.CurrentOccupantOnTop.TeamId != construction.OwnerTeam)
                    {
                        SlotMatchStats attacker = GetOrCreateStats(construction.Source.CurrentOccupantOnTop.TeamId);
                        attacker.contestingCapturePoints += contested;
                        attacker.controlledCapturePoints += contested;
                    }
                }
            }

            if (info.ControllingTeam == TeamId.Neutral)
                continue;

            SlotMatchStats sectorOwner = GetOrCreateStats(info.ControllingTeam);
            if (countAsBase)
                sectorOwner.controlledBases++;
            else
                sectorOwner.controlledSectors++;

            if (info.IsFullyControlled)
                sectorOwner.fullyControlledSectors++;
            if (info.IsDisputed || info.HasPartialCapture)
                sectorOwner.disputedSectors++;
        }
    }

    private static UnitManager FindUnit(int uid)
    {
        if (uid <= 0)
            return null;

        IReadOnlyList<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit != null && unit.InstanceId == uid)
                return unit;
        }

        return null;
    }

    private static string ResolveUnitKey(UnitManager unit, out UnitData data)
    {
        data = null;
        unit?.TryGetUnitData(out data);
        return ResolveUnitKey(null, unit, data);
    }

    private static string ResolveUnitKey(string sigla, UnitManager unit, UnitData data)
    {
        if (!string.IsNullOrWhiteSpace(sigla))
            return sigla.Trim();
        if (data != null && !string.IsNullOrWhiteSpace(data.apelido))
            return data.apelido.Trim();
        if (data != null && !string.IsNullOrWhiteSpace(data.id))
            return data.id.Trim();
        if (unit != null && !string.IsNullOrWhiteSpace(unit.UnitId))
            return unit.UnitId.Trim();
        if (unit != null && !string.IsNullOrWhiteSpace(unit.UnitDisplayName))
            return unit.UnitDisplayName.Trim();
        return "?";
    }

    private static int ResolveCost(UnitData data)
    {
        return data != null ? Mathf.Max(0, data.cost) : 0;
    }

    private static TeamId ToTeam(int team)
    {
        return Enum.IsDefined(typeof(TeamId), team) ? (TeamId)team : TeamId.Neutral;
    }

    private void SortSlotStats()
    {
        slotStats.Sort((a, b) => ((int)a.team).CompareTo((int)b.team));
    }
}

[Serializable]
public sealed class SlotMatchStats
{
    public TeamId team;
    public string teamName;
    public int totalPurchases;
    public int totalSpent;
    public int currentUnits;
    public int totalKills;
    public int totalLosses;
    public int eliteKills;
    public int eliteLosses;
    public int totalDestroyedValue;
    public int totalLostValue;
    public int damageCaused;
    public int damageReceived;
    public int captureActions;
    public int capturesCompleted;
    public int recaptureActions;
    public int embarkActions;
    public int disembarkActions;
    public int mergeActions;
    public int unitsEnteredRepair;
    public int unitsLeftRepair;
    public int commandServiceCost;
    public int fieldLogisticsCost;
    public int totalMaintenanceCost;
    public int repairServiceCost;
    public int refuelServiceCost;
    public int rearmServiceCost;
    public int serviceApplications;
    public int serviceHpRecovered;
    public int serviceFuelRecovered;
    public int serviceAmmoRecovered;
    public int controlledConstructions;
    public int controlledSectors;
    public int controlledBases;
    public int fullyControlledSectors;
    public int disputedSectors;
    public int controlledCapturePoints;
    public int contestedOwnedCapturePoints;
    public int contestingCapturePoints;
    public int totalCapturePointsMax;
    public float territoryControlPercent;
    public List<UnitTypeMatchStats> units = new List<UnitTypeMatchStats>();

    [NonSerialized] private Dictionary<string, UnitTypeMatchStats> unitStatsByKey;

    public UnitTypeMatchStats GetOrCreateUnitStats(
        string key,
        UnitData data,
        UnitManager unit,
        string siglaFallback = null,
        GameUnitClass fallbackClass = default)
    {
        EnsureIndex();
        key = string.IsNullOrWhiteSpace(key) ? "?" : key.Trim();
        if (unitStatsByKey.TryGetValue(key, out UnitTypeMatchStats stats) && stats != null)
            return stats;

        stats = new UnitTypeMatchStats { key = key };
        stats.sigla = !string.IsNullOrWhiteSpace(siglaFallback)
            ? siglaFallback.Trim()
            : (!string.IsNullOrWhiteSpace(data?.apelido) ? data.apelido.Trim() : key);
        stats.unitId = !string.IsNullOrWhiteSpace(data?.id)
            ? data.id.Trim()
            : (unit != null ? unit.UnitId : string.Empty);
        stats.displayName = !string.IsNullOrWhiteSpace(data?.displayName)
            ? data.displayName.Trim()
            : (unit != null ? unit.UnitDisplayName : stats.sigla);
        stats.unitClass = data != null ? data.unitClass : fallbackClass;
        stats.cost = data != null ? Mathf.Max(0, data.cost) : 0;
        stats.eliteLevel = data != null ? Mathf.Max(0, data.eliteLevel) : 0;
        units.Add(stats);
        unitStatsByKey[key] = stats;
        units.Sort((a, b) => string.Compare(a.sigla, b.sigla, StringComparison.OrdinalIgnoreCase));
        return stats;
    }

    public void ResetTerritory()
    {
        controlledConstructions = 0;
        controlledSectors = 0;
        controlledBases = 0;
        fullyControlledSectors = 0;
        disputedSectors = 0;
        controlledCapturePoints = 0;
        contestedOwnedCapturePoints = 0;
        contestingCapturePoints = 0;
        totalCapturePointsMax = 0;
        territoryControlPercent = 0f;
    }

    private void EnsureIndex()
    {
        if (unitStatsByKey != null)
            return;

        unitStatsByKey = new Dictionary<string, UnitTypeMatchStats>(StringComparer.OrdinalIgnoreCase);
        if (units == null)
            units = new List<UnitTypeMatchStats>();

        for (int i = 0; i < units.Count; i++)
        {
            UnitTypeMatchStats stats = units[i];
            if (stats == null)
                continue;

            string key = !string.IsNullOrWhiteSpace(stats.key) ? stats.key.Trim() : stats.sigla;
            if (string.IsNullOrWhiteSpace(key))
                key = "?";
            stats.key = key;
            unitStatsByKey[key] = stats;
        }
    }
}

[Serializable]
public sealed class UnitTypeMatchStats
{
    public string key;
    public string sigla;
    public string unitId;
    public string displayName;
    public GameUnitClass unitClass;
    public int cost;
    public int eliteLevel;
    public int purchased;
    public int current;
    public int lost;
    public int destroyedEnemy;
    public int valuePurchased;
    public int valueLost;
    public int valueDestroyed;
}
