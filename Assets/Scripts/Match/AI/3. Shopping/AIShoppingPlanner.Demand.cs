using System.Collections.Generic;
using UnityEngine;

// Cálculo de demanda por role e progressão de compra de capturadores.
public partial class AIShoppingPlanner
{
    private static int ComputeCacaBDemand(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return 0;

        int enemyHelicos  = 0;
        int enemyBombers  = 0;
        if (snapshot.EnemyUnits != null)
            foreach (UnitManager u in snapshot.EnemyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain != Domain.Air) continue;
                UnitRole r = d.roles[0];
                if (r == UnitRole.Transportador || (r == UnitRole.AtaqueAereo && d.eliteLevel == 0))
                    enemyHelicos++;
                else if (r == UnitRole.AtaqueAereo && d.eliteLevel >= 1)
                    enemyBombers++;
            }

        int activeCacaA = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain == Domain.Air && d.roles[0] == UnitRole.Interceptador && d.eliteLevel >= 1) activeCacaA++;
            }
        int uncoveredBombers = Mathf.Max(0, enemyBombers - activeCacaA);

        int minTurn = Instance != null ? Instance.MinTurnForInterceptador : 4;
        bool tooEarly = snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn;
        bool hasVisibleThreat = enemyHelicos > 0 || uncoveredBombers > 0;
        if (tooEarly && !hasVisibleThreat) return 0;

        int ratio       = Instance != null ? Instance.HelicopterosPorCacaB : 3;
        int maxCacaB    = Instance != null ? Instance.MaxCacaB : 4;
        int minPresence = tooEarly ? 0 : (Instance != null ? Instance.MinCacaBPresence : 1);
        int heliDesired = Mathf.CeilToInt(enemyHelicos / (float)ratio);
        int desired = Mathf.Max(minPresence, Mathf.Min(maxCacaB, heliDesired + uncoveredBombers));

        int active = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain == Domain.Air && d.roles[0] == UnitRole.Interceptador && d.eliteLevel == 0) active++;
            }

        int demand = Mathf.Max(0, desired - active);
        Debug.Log($"[AI Shopping] cacaB_demand: demand={demand} desired={desired} active={active} enemyHelicos={enemyHelicos} enemyBombers={enemyBombers} uncoveredBombers={uncoveredBombers} activeCacaA={activeCacaA} ratio=1:{ratio} max={maxCacaB} tooEarly={tooEarly} bypassed={tooEarly && hasVisibleThreat}");
        return demand;
    }

    private static int ComputeCacaADemand(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return 0;

        int enemyFighters = 0;
        if (snapshot.EnemyUnits != null)
            foreach (UnitManager u in snapshot.EnemyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain != Domain.Air) continue;
                UnitRole r = d.roles[0];
                if (r == UnitRole.Interceptador || (r == UnitRole.AtaqueAereo && d.eliteLevel >= 1))
                    enemyFighters++;
            }

        int minTurn = Instance != null ? Instance.MinTurnForInterceptador : 4;
        bool tooEarly = snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn;
        if (tooEarly && enemyFighters == 0) return 0;

        int maxCacaA = Instance != null ? Instance.MaxCacaA : 2;
        int desired  = Mathf.Min(maxCacaA, enemyFighters);
        Debug.Log($"[AI Shopping] cacaA_demand: tooEarly={tooEarly} bypassed={tooEarly && enemyFighters>0} enemyFighters={enemyFighters}");

        int active = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.roles[0] == UnitRole.Interceptador && d.eliteLevel >= 1) active++;
            }

        int demand = Mathf.Max(0, desired - active);
        Debug.Log($"[AI Shopping] cacaA_demand: demand={demand} desired={desired} active={active} enemyFighters={enemyFighters} max={maxCacaA}");
        return demand;
    }

    private static int ComputeApacheDemand(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return 0;

        // The turn no longer hard-blocks the demand (mirrors ComputeCacaBDemand): it only drops the
        // baseline presence floor. Real, self-regulating demand — escorting our own Chinooks and
        // countering visible enemy helicopters — flows through even in the early game.
        int minTurn = Instance != null ? Instance.MinTurnForAtaqueAereo : 5;
        bool tooEarly = snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn;

        int activeChinooks = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain == Domain.Air && d.roles[0] == UnitRole.Transportador) activeChinooks++;
            }

        // Apache as an alternative anti-helicopter: count visible enemy helicopters (same definition
        // as ComputeCacaBDemand) and let them raise the desired count.
        int enemyHelicos = 0;
        if (snapshot.EnemyUnits != null)
            foreach (UnitManager u in snapshot.EnemyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain != Domain.Air) continue;
                UnitRole r = d.roles[0];
                if (r == UnitRole.Transportador || (r == UnitRole.AtaqueAereo && d.eliteLevel == 0))
                    enemyHelicos++;
            }

        int ratio       = Instance != null ? Instance.ChinooksPorApache : 2;
        int heliRatio   = Instance != null ? Instance.HelicopterosInimigosPorApache : 3;
        int minPresence = tooEarly ? 0 : (Instance != null ? Instance.MinApachePresence : 1);

        int escortDesired = Mathf.CeilToInt(activeChinooks / (float)ratio);
        int threatDesired = Mathf.CeilToInt(enemyHelicos / (float)heliRatio);
        int desired = Mathf.Max(minPresence, Mathf.Max(escortDesired, threatDesired));

        bool defenseBonus = snapshot.Stance == AIStance.Defensive
            && Instance != null && Instance.ComprarApacheEmModoDefesa;
        if (defenseBonus && !tooEarly) desired = Mathf.Max(desired, 1);

        int active = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.roles[0] == UnitRole.AtaqueAereo && d.eliteLevel == 0) active++;
            }

        int demand = Mathf.Max(0, desired - active);
        Debug.Log($"[AI Shopping] apache_demand: demand={demand} desired={desired} active={active} chinooks={activeChinooks} escort={escortDesired} enemyHelicos={enemyHelicos} threat={threatDesired} ratio=1:{ratio} heliRatio=1:{heliRatio} tooEarly={tooEarly} defBonus={defenseBonus}");
        return demand;
    }

    private static int ComputeBombaDemand(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return 0;

        // Bombardeiro e uma peca de ruptura ofensiva, nao apenas um upgrade depois de X Apaches.
        // A regra por Apaches continua existindo, mas plano ofensivo com economia madura tambem
        // abre demanda para botar pressao e preparar invasao.
        int minTurn = Instance != null ? Instance.MinTurnForAtaqueAereo : 5;
        bool tooEarly = snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn;

        int activeApaches = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.roles[0] == UnitRole.AtaqueAereo && d.eliteLevel == 0) activeApaches++;
            }

        bool offensivePlan = snapshot.Stance == AIStance.Offensive
            || snapshot.Stance == AIStance.Tactical
            || HasAnyOffensiveObjective(snapshot.AITeam);
        bool economyReady = snapshot.Budget >= Mathf.Max(20000, Mathf.Max(1, snapshot.IncomePerTurn) * 2);
        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto, requirePrimary: true);
        int minCap = Instance != null ? Instance.MinActiveCapturersForFireSupport : 2;
        int minAss = Instance != null ? Instance.MinActiveAssaultForFireSupport : 1;
        bool armyReady = activeCapturers >= minCap && activeAssault >= minAss;
        bool turnAllowsOffensiveBomba = !tooEarly || HasPreventiveDefenseBudget(snapshot);
        int offensiveDesired = offensivePlan && economyReady && armyReady && turnAllowsOffensiveBomba ? 1 : 0;
        if (offensiveDesired > 0 && snapshot.Budget >= 45000 && activeApaches >= 2)
            offensiveDesired = 2;

        int ratio       = Instance != null ? Instance.ApachesParaBombardeiro : 2;
        int minPresence = tooEarly ? 0 : (Instance != null ? Instance.MinBombaPresence : 0);
        int desired     = Mathf.Max(minPresence, Mathf.FloorToInt(activeApaches / (float)ratio), offensiveDesired);

        int active = CountOwnedBombers(snapshot, includeUnderRepair: true);

        int demand = Mathf.Max(0, desired - active);
        Debug.Log($"[AI Shopping] bomba_demand: demand={demand} desired={desired} active={active} apaches={activeApaches} offensive={offensiveDesired} plan={offensivePlan} economy={economyReady} army={armyReady} cap={activeCapturers}/{minCap} ass={activeAssault}/{minAss} ratio=1:{ratio} tooEarly={tooEarly}");
        return demand;
    }

    private static void ComputeIntelDemand(
        AIWorldSnapshot snapshot,
        AIIntelReport intel,
        out int openAirIntelSlots,
        out int openMobileAirIntelSlots)
    {
        openAirIntelSlots = 0;
        openMobileAirIntelSlots = 0;
        if (snapshot == null)
            return;

        int minTurn = Instance != null ? Instance.MinTurnForIntel : 4;
        bool tooEarly = snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn;
        int visibleAir = CountTotalVisibleEnemyAircraft(snapshot);
        float inferredAir = intel != null ? intel.enemyAirThreatScore : 0f;
        float airThreshold = Instance != null ? Instance.IntelAirThreatAntiAirThreshold : 2f;

        int activeAirIntel = CountActiveDedicatedIntel(snapshot, Domain.Air);
        int activeMobileAirIntel = CountActiveDedicatedIntel(snapshot, Domain.Land);
        int activeAirCombat = CountActiveAirCombatUnits(snapshot);
        bool enemyAirProduction = HasEnemyAirportProductionCapacity(snapshot);
        bool ownAirIntelProduction = HasDedicatedIntelProduction(snapshot, Domain.Air);
        int cheapestAirIntel = ownAirIntelProduction ? FindCheapestDedicatedIntelCost(snapshot, Domain.Air) : 0;
        bool offensivePlan = snapshot.Stance == AIStance.Offensive
            || snapshot.Stance == AIStance.Tactical
            || HasAnyOffensiveObjective(snapshot.AITeam);
        bool needsAirPicture = visibleAir > 0
            || inferredAir >= airThreshold
            || enemyAirProduction
            || (offensivePlan && activeAirCombat > 0)
            || (offensivePlan && snapshot.Budget >= Mathf.Max(16000, Mathf.Max(1, snapshot.IncomePerTurn) * 2));

        if (tooEarly && visibleAir == 0 && inferredAir < airThreshold)
        {
            Debug.Log($"[AI Shopping] intel_demand: 0 turn={snapshot.TurnNumber}<{minTurn} visibleAir={visibleAir} inferredAir={inferredAir:F1}");
            return;
        }

        // EWACS cobre um mapa pequeno/medio quase inteiro. Plataforma aerea de
        // Intel fica como peca estrategica unica; radar movel complementa o ceu.
        int maxAir = Mathf.Min(1, Instance != null ? Instance.MaxAirIntel : 1);
        int maxMobileAir = Instance != null ? Instance.MaxMobileAirIntel : 1;
        int desiredAirIntel = needsAirPicture ? 1 : 0;
        openAirIntelSlots = ownAirIntelProduction
            ? Mathf.Max(0, Mathf.Min(maxAir, desiredAirIntel) - activeAirIntel)
            : 0;

        bool canBuyAirIntel = ownAirIntelProduction
            && cheapestAirIntel > 0
            && snapshot.Budget >= cheapestAirIntel;
        bool hasOrWillHaveAirIntel = activeAirIntel > 0 || openAirIntelSlots > 0;
        bool needsMobileAirIntel = desiredAirIntel > 0
            && activeMobileAirIntel <= 0
            && (activeAirIntel > 0
                || !hasOrWillHaveAirIntel
                || !ownAirIntelProduction
                || !canBuyAirIntel
                || snapshot.Budget < Mathf.Max(12000, Mathf.Max(1, snapshot.IncomePerTurn)));
        int desiredMobileAirIntel = needsMobileAirIntel ? 1 : 0;
        openMobileAirIntelSlots = Mathf.Max(0, Mathf.Min(maxMobileAir, desiredMobileAirIntel) - activeMobileAirIntel);

        Debug.Log($"[AI Shopping] intel_demand: air={openAirIntelSlots} mobileAir={openMobileAirIntelSlots} desiredAir={desiredAirIntel} airCap={maxAir} mobileAirCap={maxMobileAir} needsMobileAir={needsMobileAirIntel} activeAir={activeAirIntel} activeMobileAir={activeMobileAirIntel} enemyAirProd={enemyAirProduction} ownAirIntelProd={ownAirIntelProduction} visibleAir={visibleAir} inferredAir={inferredAir:F1} airCombat={activeAirCombat} offensive={offensivePlan} budget={snapshot.Budget}");
    }

    private static int ComputeFireSupportDemand(
        AIWorldSnapshot snapshot,
        int openCapturerSlots,
        int openAssaultSlots,
        out bool preferDefensiveFireSupport)
    {
        preferDefensiveFireSupport = false;
        if (snapshot == null) return 0;

        int minTurn = Instance != null ? Instance.MinTurnForFireSupport : 3;
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn)
        {
            Debug.Log($"[AI Shopping] fire_support_demand: 0 turn={snapshot.TurnNumber}<{minTurn}");
            return 0;
        }

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto, requirePrimary: true);
        int activeFireSupport = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager fsUnit in snapshot.MyUnits)
            {
                if (fsUnit == null || fsUnit.IsDead || fsUnit.IsEmbarked || fsUnit.IsUnderRepair) continue;
                if (!fsUnit.TryGetUnitData(out UnitData fsData) || fsData?.roles == null) continue;
                if (fsData.roles.Contains(UnitRole.FogoIndireto)) activeFireSupport++;
            }

        int minCapturers = Instance != null ? Instance.MinActiveCapturersForFireSupport : 2;
        int minAssault = Instance != null ? Instance.MinActiveAssaultForFireSupport : 1;
        bool compositionReady = activeCapturers >= minCapturers && activeAssault >= minAssault;

        if (!compositionReady)
        {
            Debug.Log($"[AI Shopping] fire_support_demand: 0 composition cap={activeCapturers}/{minCapturers} ass={activeAssault}/{minAssault} openCap={openCapturerSlots} openAss={openAssaultSlots}");
            return 0;
        }

        bool defensiveNeed = snapshot.Stance == AIStance.Defensive || HasAnyVisibleEnemyNearOwnedBase(snapshot, DefensiveBaseThreatRange);
        bool offensiveNeed = snapshot.Stance == AIStance.Offensive || HasAnyOffensiveObjective(snapshot.AITeam);
        preferDefensiveFireSupport = defensiveNeed && !offensiveNeed || snapshot.Stance == AIStance.Defensive;

        bool hasNeed = defensiveNeed || offensiveNeed || snapshot.Stance == AIStance.Tactical;
        int ratio = Instance != null ? Instance.AssaultPerFireSupportRatio : 2;
        int desiredFireSupport = hasNeed
            ? Mathf.Max(1, Mathf.CeilToInt(activeAssault / (float)ratio))
            : 0;
        int demand = Mathf.Max(0, desiredFireSupport - activeFireSupport);
        Debug.Log($"[AI Shopping] fire_support_demand: demand={demand} desired={desiredFireSupport} activeFire={activeFireSupport} activeAss={activeAssault} ratio=1:{ratio} stance={snapshot.Stance} defensive={defensiveNeed} offensive={offensiveNeed} preferDef={preferDefensiveFireSupport}");
        return demand;
    }

    private static int ComputeLogisticsDemand(AIWorldSnapshot snapshot, out int repairDemandCount, out int activeLogisticsCount)
    {
        repairDemandCount = CountGroundUnitsUnderRepair(snapshot);
        int criticalPreventiveDemandCount = CountCriticalPreventiveGroundLogisticsDemand(snapshot);
        activeLogisticsCount = CountActiveGroundLogistics(snapshot);
        int activeLogisticsCapacity = CountActiveGroundLogisticsCapacity(snapshot);

        if (snapshot != null && snapshot.TurnNumber <= 1)
        {
            Debug.Log($"[AI Shopping] logistics_demand: 0 turn={snapshot.TurnNumber}<=1");
            return 0;
        }

        int repairsPerSupplier = Instance != null ? Mathf.Max(1, Instance.RepairsPerGroundSupplier) : 2;
        int logisticsWorkload = repairDemandCount + criticalPreventiveDemandCount;
        int desiredLogistics = logisticsWorkload > 0
            ? Mathf.CeilToInt(logisticsWorkload / (float)repairsPerSupplier)
            : 0;
        int logisticsCap = logisticsWorkload >= 6 ? 3 : 2;
        desiredLogistics = Mathf.Min(desiredLogistics, logisticsCap);

        int demand = Mathf.Max(0, desiredLogistics - activeLogisticsCount);
        Debug.Log($"[AI Shopping] logistics_demand: demand={demand} groundRepairs={repairDemandCount} criticalPreventive={criticalPreventiveDemandCount} workload={logisticsWorkload} activeLog={activeLogisticsCount} activeCap={activeLogisticsCapacity} desired={desiredLogistics} repairsPerSupplier={repairsPerSupplier} cap={logisticsCap} units={snapshot?.MyUnits?.Count ?? 0}");
        return demand;
    }

    private static bool ComputeOffensiveAntiInfantryFireSupportDemand(
        AIWorldSnapshot snapshot,
        AIIntelReport intel,
        out bool offensiveAntiInfantryFireSupport)
    {
        offensiveAntiInfantryFireSupport = false;
        if (snapshot == null || Instance == null)
            return false;
        if (snapshot.Stance == AIStance.Defensive)
            return false;
        if (!HasAnyOffensiveObjective(snapshot.AITeam))
            return false;

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto, requirePrimary: true);
        int activeFireSupport = CountActiveCombatFireSupport(snapshot);
        int minCapturers = Mathf.Max(5, (Instance.MinActiveCapturersForFireSupport > 0 ? Instance.MinActiveCapturersForFireSupport : 2) + 3);
        int minAssault = Mathf.Max(1, Instance.MinActiveAssaultForFireSupport);
        if (activeCapturers < minCapturers || activeAssault < minAssault)
            return false;

        float infantryPressure = intel != null ? intel.enemyInfantryPressureScore : 0f;
        float enemyInfantryForce = intel != null ? intel.enemyInfantryForce : 0f;
        float topHot = 0f;
        float topEnemyActivity = 0f;
        string topSector = "-";
        if (intel != null && intel.sectors != null && intel.sectors.Count > 0 && intel.sectors[0] != null)
        {
            topHot = intel.sectors[0].hotScore;
            topEnemyActivity = intel.sectors[0].enemyActivity;
            topSector = intel.sectors[0].sector;
        }

        float threshold = Instance.IntelOffensiveAntiInfantryFireThreshold;
        bool infantryMass = infantryPressure >= threshold
            || enemyInfantryForce >= threshold;
        bool hotOffensiveSector = topHot >= Instance.IntelFireSupportGapHotThreshold && topEnemyActivity > 0f;
        if (!infantryMass && !hotOffensiveSector)
            return false;

        bool strongInfantryMass = infantryPressure >= threshold * 2f || enemyInfantryForce >= threshold * 2f;
        bool strongHotSector = topHot >= Instance.IntelFireSupportGapHotThreshold + 3f;
        bool hasScreenForSecondFireSupport = activeAssault >= minAssault + 1 || activeCapturers >= minCapturers + 2;
        int desiredFireSupport = (strongInfantryMass || strongHotSector) && hasScreenForSecondFireSupport
            ? 2
            : 1;
        bool needed = activeFireSupport < desiredFireSupport;
        offensiveAntiInfantryFireSupport = needed;
        if (needed)
            Debug.Log($"[AI Shopping] offensive_anti_inf_fire_demand: needed=True activeFire={activeFireSupport}/{desiredFireSupport} cap={activeCapturers}/{minCapturers} ass={activeAssault}/{minAssault} screen2={hasScreenForSecondFireSupport} infantry={infantryPressure:F1}/{enemyInfantryForce:F1} top={topSector} hot={topHot:F1} enemy={topEnemyActivity:F1}");
        return needed;
    }

    private static int ComputeNumericalBulkCapturerDemand(AIWorldSnapshot snapshot, AIIntelReport intel)
    {
        if (intel == null || Instance == null || snapshot == null) return 0;
        float pressure = intel.numericalPressure;
        float threshold = Instance.IntelNumericalPressureThreshold;
        if (pressure < threshold) return 0;
        return Mathf.Clamp(Mathf.CeilToInt(pressure / 2f), 1, 3);
    }

    private static int CountUnfilledDefenseOps(TeamId aiTeam)
    {
        AITacticalAnalyzer mgr = AITacticalAnalyzer.Instance;
        if (mgr == null) return 0;
        int count = 0;
        foreach (AITacticalNeed op in mgr.GetOperationsForTeam(aiTeam))
        {
            if (op.Type != AITacticalNeedType.SectorDefense) continue;
            if (op.CountOpenSlots(AINeedKind.Assault) > 0 || op.CountOpenSlots(AINeedKind.Artillery) > 0)
                count++;
        }
        return count;
    }

    private static int CountOpenSlots(TeamId aiTeam, UnitRole role)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null) return 0;

        int open = 0;
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == role && !slot.Filled) open++;
        return open;
    }

    private static void CountSlots(TeamId aiTeam, UnitRole role, out int total, out int filled)
    {
        total = 0; filled = 0;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null) return;
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == role) { total++; if (slot.Filled) filled++; }
    }

    private static bool HasOpenDefensiveSlot(TeamId aiTeam)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null) return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj.Status != ObjectiveStatus.Defending) continue;
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.Capturador && !slot.Filled) return true;
        }

        return false;
    }

    private static int CountActiveUnitsWithRole(AIWorldSnapshot snapshot, UnitRole role, bool requirePrimary)
    {
        if (snapshot == null || snapshot.MyUnits == null) return 0;

        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked) continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.roles == null || data.roles.Count == 0) continue;
            if (requirePrimary)
            {
                if (data.roles[0] == role) count++;
            }
            else if (data.roles.Contains(role))
            {
                count++;
            }
        }
        return count;
    }

    private static int CountActiveDedicatedIntel(AIWorldSnapshot snapshot, Domain domain)
    {
        if (snapshot == null)
            return 0;

        int count = CountExistingDedicatedIntel(snapshot.AITeam, domain);
        if (count > 0 || snapshot.MyUnits == null)
            return count;

        // Fallback for tests/mocked snapshots where UnitManager.AllActive is not populated.
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.domain != domain)
                continue;
            if (IsDedicatedIntelPurchase(data))
                count++;
        }
        return count;
    }

    private static int CountExistingDedicatedIntel(TeamId aiTeam, Domain domain)
    {
        int count = 0;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.TeamId != aiTeam || unit.IsDead || unit.IsEmbarked)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.domain != domain)
                continue;
            if (IsDedicatedIntelPurchase(data))
                count++;
        }
        return count;
    }

    private static int CountActiveAirCombatUnits(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0;

        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked || unit.IsUnderRepair)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.domain != Domain.Air)
                continue;
            if (IsPrimaryRole(data, UnitRole.Interceptador) || IsPrimaryRole(data, UnitRole.AtaqueAereo))
                count++;
        }
        return count;
    }

    private static bool HasDedicatedIntelProduction(AIWorldSnapshot snapshot, Domain domain)
    {
        return FindCheapestDedicatedIntelCost(snapshot, domain) > 0;
    }

    private static bool HasEnemyAirportProductionCapacity(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.EnemyBuildings == null)
            return false;

        foreach (ConstructionManager building in snapshot.EnemyBuildings)
        {
            if (building == null)
                continue;
            if (!building.TryResolveConstructionData(out ConstructionData data) || data == null || !data.isAirport)
                continue;
            if (!building.CanProduceUnitsForTeam(building.TeamId))
                continue;
            if (building.OfferedUnits == null)
                continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit != null && unit.domain == Domain.Air)
                    return true;
            }
        }

        return false;
    }

    private static int FindCheapestDedicatedIntelCost(AIWorldSnapshot snapshot, Domain domain)
    {
        if (snapshot == null || snapshot.MyBuildings == null)
            return 0;

        int cheapest = int.MaxValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam))
                continue;
            if (building.OfferedUnits == null)
                continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.domain != domain)
                    continue;
                if (!IsDedicatedIntelPurchase(unit))
                    continue;
                if (unit.cost > 0 && unit.cost < cheapest)
                    cheapest = unit.cost;
            }
        }

        return cheapest == int.MaxValue ? 0 : cheapest;
    }

    private static int CountActiveCombatFireSupport(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0;

        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked || unit.IsUnderRepair)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;
            if (data.roles == null || !data.roles.Contains(UnitRole.FogoIndireto))
                continue;
            if (IsAntiAirOnlyUnit(data))
                continue;
            count++;
        }

        return count;
    }

    private static int CountActiveEliteFireSupportUnits(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null) return 0;
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked) continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null) continue;
            if (data.roles == null || !data.roles.Contains(UnitRole.FogoIndireto)) continue;
            if (IsAntiAirOnlyUnit(data)) continue;
            if (data.eliteLevel >= 1) count++;
        }
        return count;
    }

    private static int CountActiveEliteAssaultUnits(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null) return 0;
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked) continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null) continue;
            if (data.domain != Domain.Land
                || data.unitClass != GameUnitClass.Armored
                || !IsPurePrimaryAssault(data)
                || data.eliteLevel < 1)
                continue;
            count++;
        }
        return count;
    }

    private static int CountActiveArmoredAssaultUnits(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null) return 0;
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked || unit.IsUnderRepair) continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null) continue;
            if (IsDefensiveBaseAssaultTankPurchase(data)) count++;
        }
        return count;
    }

    private static int CountVisibleEnemyArmor(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.EnemyUnits == null) return 0;
        int count = 0;
        foreach (UnitManager unit in snapshot.EnemyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked) continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null) continue;
            if (data.domain == Domain.Land && data.unitClass == GameUnitClass.Armored) count++;
        }
        return count;
    }

    private static int CountGroundUnitsUnderRepair(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0;

        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (unit.TryGetUnitData(out UnitData data) && data != null && data.domain == Domain.Air)
                continue;
            if (unit.IsUnderRepair)
                count++;
        }

        return count;
    }

    private static int CountCriticalPreventiveGroundLogisticsDemand(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0;

        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked || unit.IsUnderRepair)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;
            if (data.domain == Domain.Air)
                continue;
            if (data.roles != null && data.roles.Contains(UnitRole.Logistica))
                continue;

            bool fireSupport = data.roles != null && data.roles.Contains(UnitRole.FogoIndireto)
                || data.unitClass == GameUnitClass.Artillery
                || data.preferArtilleryModeBeforeCombatant
                || data.longRangeStationary;
            if (!fireSupport)
                continue;

            if (HasAnyShoppingWeaponAmmoAtOrBelow(unit, 0) || HasAnyShoppingWeaponAmmoAtOrBelow(unit, 1))
                count++;
        }

        return count;
    }

    private static bool HasAnyShoppingWeaponAmmoAtOrBelow(UnitManager unit, int ammoThreshold)
    {
        if (ammoThreshold < 0 || unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.embarkedWeapons == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = unit.GetEmbarkedWeapons();
        if (runtimeWeapons == null)
            return false;

        int count = Mathf.Min(runtimeWeapons.Count, data.embarkedWeapons.Count);
        for (int i = 0; i < count; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons[i];
            UnitEmbarkedWeapon baseline = data.embarkedWeapons[i];
            if (runtime == null || baseline == null)
                continue;
            if (baseline.squadAmmunition > 0 && runtime.squadAmmunition <= ammoThreshold)
                return true;
        }

        return false;
    }

    private static int CountActiveGroundLogistics(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0;

        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked || unit.IsUnderRepair)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;
            if (data.domain == Domain.Air)
                continue;
            if (data.roles != null && data.roles.Contains(UnitRole.Logistica))
                count++;
        }
        return count;
    }

    private static int CountActiveGroundLogisticsCapacity(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0;

        int capacity = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked || unit.IsUnderRepair)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;
            if (data.domain == Domain.Air)
                continue;
            if (data.roles == null || !data.roles.Contains(UnitRole.Logistica))
                continue;
            if (!data.isSupplier || data.maxUnitsServedPerTurn <= 0)
                continue;

            capacity += data.maxUnitsServedPerTurn;
        }
        return capacity;
    }

    private static bool HasActivePrimaryRole(AIWorldSnapshot snapshot, UnitRole role)
    {
        if (snapshot == null) return false;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked) continue;
            if (unit.TryGetUnitData(out UnitData data) && IsPrimaryRole(data, role)) return true;
        }
        return false;
    }

    private static bool CanAffordPurePrimaryRole(AIWorldSnapshot snapshot, UnitRole role, int budget)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return false;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;
            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.cost > budget || unit.domain != Domain.Land) continue;
                if (!IsPrimaryRole(unit, role)) continue;
                if (unit.roles != null && unit.roles.Contains(UnitRole.Capturador)) continue;
                return true;
            }
        }
        return false;
    }

    private static int FindCheapestPrimaryRoleLandCost(AIWorldSnapshot snapshot, UnitRole role)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return 0;

        int cheapest = int.MaxValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.domain != Domain.Land) continue;
                if (!IsPrimaryRole(unit, role)) continue;
                if (unit.cost < cheapest)
                    cheapest = unit.cost;
            }
        }

        return cheapest == int.MaxValue ? 0 : cheapest;
    }

    private static int CountAvailablePrimaryRoleLandProductionSlots(
        AIWorldSnapshot snapshot,
        UnitRole role,
        HashSet<Vector3Int> occupied)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return 0;

        int slots = 0;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            if (occupied != null && occupied.Contains(cell)) continue;

            bool canProduceRole = false;
            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.domain != Domain.Land) continue;
                if (!IsPrimaryRole(unit, role)) continue;
                canProduceRole = true;
                break;
            }

            if (canProduceRole)
                slots++;
        }

        return slots;
    }

    private static int LimitCapturerDemandForProgression(
        AIWorldSnapshot snapshot,
        int openCapturerSlots,
        int openAssaultSlots,
        int openTransportSlots,
        int openLogisticsSlots,
        int openFireSupportSlots)
    {
        if (snapshot == null)
            return 0;

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activePrimaryCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: true);

        if (activeCapturers == 0)
            return openCapturerSlots;

        int batchSize = Instance != null ? Instance.ProgressiveCapturerBatchSize : 2;
        int capped = Mathf.Min(openCapturerSlots, Mathf.Max(1, batchSize));
        int supportDemand = openAssaultSlots + openTransportSlots + openLogisticsSlots + openFireSupportSlots;

        int activeAssault = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto, requirePrimary: true);

        CountSlots(snapshot.AITeam, UnitRole.Capturador, out int totalCapSlots, out int _);
        int supportPauseThreshold;
        if (totalCapSlots > 0)
        {
            float pauseRatio = (Instance != null ? Instance.EliteCapturerFillRatio : 0.6f) * 0.85f;
            supportPauseThreshold = Mathf.Max(1, Mathf.CeilToInt(totalCapSlots * pauseRatio));
        }
        else
        {
            supportPauseThreshold = Instance != null ? Instance.CapturersPerPreventiveTransport : 4;
        }

        int numSectors = SectorManager.GetAllSectorInfos().Count;
        if (numSectors > 0)
            supportPauseThreshold = Mathf.Max(supportPauseThreshold, numSectors);

        if (activeCapturers >= supportPauseThreshold && activePrimaryCapturers >= supportPauseThreshold && activeAssault >= 1)
            capped = 0;

        if (capped == 0 && activeCapturers < supportPauseThreshold)
            capped = Mathf.Min(Mathf.Max(1, batchSize), supportPauseThreshold - activeCapturers);

        if (capped != openCapturerSlots)
        {
            Debug.Log($"[AI Shopping] capturer_progression: raw={openCapturerSlots} capped={capped} activeCap={activeCapturers} primaryCap={activePrimaryCapturers} activeAss={activeAssault} supportDemand={supportDemand} batch={batchSize} pauseAt={supportPauseThreshold} totalCapSlots={totalCapSlots}");
        }

        return capped;
    }

    private static int RestoreCapturerDemandForIdleAirlift(AIWorldSnapshot snapshot, int rawOpenCapturerSlots, int cappedOpenCapturerSlots)
    {
        if (cappedOpenCapturerSlots > 0 || rawOpenCapturerSlots <= 0 || snapshot == null)
            return cappedOpenCapturerSlots;
        if (!MapNeedsAirTransport(snapshot, out int minDist))
            return cappedOpenCapturerSlots;

        int emptyAirTransporters = CountAirTransporters(snapshot, requireEmpty: true);
        if (emptyAirTransporters <= 0)
            return cappedOpenCapturerSlots;

        const int HeliCapacity = 2;
        int pickupCapturers = CountAirTransportPickupCapturers(snapshot);
        int spareSeats = Mathf.Max(0, emptyAirTransporters * HeliCapacity - pickupCapturers);
        if (spareSeats <= 0)
            return cappedOpenCapturerSlots;

        int batchSize = Instance != null ? Instance.ProgressiveCapturerBatchSize : 2;
        int airliftSeats = Mathf.Max(1, spareSeats);
        int restored = Mathf.Max(rawOpenCapturerSlots, airliftSeats);
        restored = Mathf.Min(restored, spareSeats);
        Debug.Log($"[AI Shopping] capturer_airlift_feed: raw={rawOpenCapturerSlots} capped={cappedOpenCapturerSlots}->{restored} emptyAir={emptyAirTransporters} pickupCap={pickupCapturers} spareSeats={spareSeats} batch={batchSize} airliftSeats={airliftSeats} minDist={minDist}");
        return restored;
    }

    private static int RestoreStrategicPrimaryCapturerDemand(
        AIWorldSnapshot snapshot,
        int rawOpenCapturerSlots,
        int cappedOpenCapturerSlots,
        ref int openFireSupportSlots,
        ref bool preferDefensiveFireSupport)
    {
        if (snapshot == null || rawOpenCapturerSlots <= 0)
            return cappedOpenCapturerSlots;
        if (!HasUnownedCapturableBuilding(snapshot))
            return cappedOpenCapturerSlots;
        if (!CanAffordPurePrimaryRole(snapshot, UnitRole.Capturador, snapshot.Budget))
            return cappedOpenCapturerSlots;

        int activePrimaryCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: true);
        int activeHybridCapturers = Mathf.Max(0, CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false) - activePrimaryCapturers);
        int activeCombatFireSupport = CountActiveCombatFireSupport(snapshot);
        int strategicFloor = GetStrategicPrimaryCapturerFloor(snapshot);
        bool belowStrategicFloor = activePrimaryCapturers < strategicFloor;
        bool fireSupportSkew = activeCombatFireSupport >= Mathf.Max(4, activePrimaryCapturers * 2 + 2);

        if (!belowStrategicFloor && !fireSupportSkew)
            return cappedOpenCapturerSlots;

        int restored = Mathf.Max(cappedOpenCapturerSlots, 1);
        if (fireSupportSkew && openFireSupportSlots > 0)
        {
            Debug.Log($"[AI Shopping] capturer_composition: fire skew activeFire={activeCombatFireSupport} primaryCap={activePrimaryCapturers} hybridCap={activeHybridCapturers} -> suspendendo fire_slots {openFireSupportSlots}->0");
            openFireSupportSlots = 0;
            preferDefensiveFireSupport = false;
        }

        if (restored != cappedOpenCapturerSlots)
        {
            Debug.Log($"[AI Shopping] capturer_composition: restaurando cap {cappedOpenCapturerSlots}->{restored} raw={rawOpenCapturerSlots} primaryCap={activePrimaryCapturers}/{strategicFloor} hybridCap={activeHybridCapturers} activeFire={activeCombatFireSupport} skew={fireSupportSkew}");
        }

        return restored;
    }

    private static int GetStrategicPrimaryCapturerFloor(AIWorldSnapshot snapshot)
    {
        if (snapshot == null)
            return 3;

        int capturableFronts = CountUnownedCapturableSectors(snapshot);
        int floor = capturableFronts >= 4 ? 4 : 3;
        if (snapshot.Stance == AIStance.Offensive && capturableFronts >= 2)
            floor = Mathf.Max(floor, 4);
        if (snapshot.MyUnits != null && snapshot.MyUnits.Count <= 5)
            floor = Mathf.Min(floor, 2);
        return floor;
    }

    private static bool HasUnownedCapturableBuilding(AIWorldSnapshot snapshot)
    {
        return CountUnownedCapturableSectors(snapshot) > 0;
    }

    private static int CountUnownedCapturableSectors(AIWorldSnapshot snapshot)
    {
        if (snapshot == null)
            return 0;

        var sectors = new HashSet<ConstructionSector>();
        AddCapturableSectors(snapshot.NeutralBuildings, sectors);
        AddCapturableSectors(snapshot.EnemyBuildings, sectors);
        return sectors.Count;
    }

    private static void AddCapturableSectors(List<ConstructionManager> buildings, HashSet<ConstructionSector> sectors)
    {
        if (buildings == null || sectors == null)
            return;

        foreach (ConstructionManager building in buildings)
        {
            if (building == null || !building.IsCapturable)
                continue;
            sectors.Add(building.Sector);
        }
    }

    private static int GetFireSupportSaturationLimit(AIWorldSnapshot snapshot)
    {
        int activeAssault = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto, requirePrimary: true);
        int ratio = Instance != null ? Mathf.Max(1, Instance.AssaultPerFireSupportRatio) : 2;
        int compositionLimit = Mathf.Max(1, Mathf.CeilToInt(activeAssault / (float)ratio));
        return Mathf.Max(2, compositionLimit + 1);
    }

    private static bool IsFireSupportSaturated(AIWorldSnapshot snapshot)
    {
        if (snapshot == null)
            return false;

        int activeFireSupport = CountActiveCombatFireSupport(snapshot);
        int saturationLimit = GetFireSupportSaturationLimit(snapshot);
        return activeFireSupport >= saturationLimit;
    }

    private static int CountVisibleEnemyCombatFireSupport(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.EnemyUnits == null)
            return 0;

        int count = 0;
        foreach (UnitManager unit in snapshot.EnemyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;
            if (IsAntiAirOnlyUnit(data))
                continue;

            bool fireSupport = data.roles != null && data.roles.Contains(UnitRole.FogoIndireto)
                || data.unitClass == GameUnitClass.Artillery
                || data.preferArtilleryModeBeforeCombatant
                || data.longRangeStationary;
            if (fireSupport)
                count++;
        }

        return count;
    }

    private static int CountOwnedBombers(AIWorldSnapshot snapshot, bool includeUnderRepair)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0;

        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (!includeUnderRepair && unit.IsUnderRepair)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;
            if (data.domain == Domain.Air && IsPrimaryRole(data, UnitRole.AtaqueAereo) && data.eliteLevel >= 1)
                count++;
        }

        return count;
    }

    private static int FindBreakthroughArmorCost(AIWorldSnapshot snapshot, int budget)
    {
        if (snapshot == null || snapshot.MyBuildings == null)
            return 0;

        UnitData best = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam))
                continue;
            if (building.OfferedUnits == null)
                continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (!IsDefensiveBaseAssaultTankPurchase(unit))
                    continue;
                if (unit.cost <= 0 || unit.cost > budget)
                    continue;
                if (best == null
                    || unit.eliteLevel > best.eliteLevel
                    || (unit.eliteLevel == best.eliteLevel && unit.cost > best.cost))
                    best = unit;
            }
        }

        return best != null ? best.cost : 0;
    }

    private static bool ShouldApplyArtilleryWallBreakthrough(
        AIWorldSnapshot snapshot,
        AIIntelReport intel,
        int visibleEnemyFireSupport)
    {
        if (snapshot == null)
            return false;

        bool offensivePlan = snapshot.Stance == AIStance.Offensive
            || snapshot.Stance == AIStance.Tactical
            || HasAnyOffensiveObjective(snapshot.AITeam);
        if (!offensivePlan)
            return false;

        int myUnits = snapshot.MyUnits != null ? snapshot.MyUnits.Count : 0;
        bool armyMassed = myUnits >= 18;
        bool economyReady = snapshot.Budget >= Mathf.Max(18000, Mathf.Max(1, snapshot.IncomePerTurn));
        bool visibleWall = visibleEnemyFireSupport >= 3
            || (visibleEnemyFireSupport >= 2 && armyMassed);

        float stalematePressure = intel != null ? intel.stalemateElitePressure : 0f;
        float artilleryIntel = intel != null ? intel.enemyArtilleryThreatScore : 0f;
        float heavyThreshold = Instance != null ? Instance.IntelStalemateFireSupportThreshold : 6f;
        bool intelWall = artilleryIntel >= 2f
            || (stalematePressure >= heavyThreshold && artilleryIntel > 0f);

        return economyReady && (visibleWall || intelWall);
    }
}
