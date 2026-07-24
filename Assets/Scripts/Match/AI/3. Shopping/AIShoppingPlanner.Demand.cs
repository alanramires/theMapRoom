using System.Collections.Generic;
using UnityEngine;

// CÃ¡lculo de demanda por role e progressÃ£o de compra de capturadores.
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
        // baseline presence floor. Real, self-regulating demand â€” escorting our own Chinooks and
        // countering visible enemy helicopters â€” flows through even in the early game.
        int minTurn = Instance != null ? Instance.MinTurnForAtaqueAereo : 5;
        bool tooEarly = snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn;

        int activeChinooks = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain == Domain.Air
                    && UnitRoleCompatibility.ResolveCompositionRole(d) == UnitRole.Transportador) activeChinooks++;
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

    // CHOKE POINT do teto de logística do Hard Mode.
    //
    // Há MAIS DE UM emissor de demanda de Logistica (ComputeLogisticsDemand/"service",
    // BuildLogisticsOperationalPressure/"operational-pressure", ...) e MergeRoleDemand funde por
    // Max(Count). Aplicar o teto dentro de um emissor não segura nada: o emissor que respeitava o
    // limite (x1) perdia na fusão pro que não o conhecia (x3, teto próprio hardcoded), e o Hard
    // comprava 3 supridores com o limite configurado em 1.
    //
    // Aqui, com a lista já fechada, o teto vale pra qualquer emissor — presente ou futuro.
    // Tanque aéreo (Domain.Air) tem trilho próprio e não entra nesta conta.
    private static void ApplyHardModeLogisticsCap(AIWorldSnapshot snapshot, List<AIShoppingDemand> demands)
    {
        if (snapshot == null || demands == null
            || AIController.Instance == null || !AIController.Instance.HardMode)
            return;

        int cap = AIController.Instance.MaxLogisticUnitsOnHardMode;
        int active = CountActiveGroundLogistics(snapshot);
        int allowed = Mathf.Max(0, cap - active);

        foreach (AIShoppingDemand demand in demands)
        {
            if (demand == null || demand.Role != UnitRole.Logistica) continue;
            if (demand.Domain == Domain.Air) continue;
            if (demand.Count <= allowed) continue;
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + $"teto Hard de logística: Logistica x{demand.Count} -> x{allowed} "
                + $"(max={cap} ativos={active} origem={demand.Origin})");
            demand.Count = allowed;
        }

        demands.RemoveAll(d => d != null && d.Role == UnitRole.Logistica
            && d.Domain != Domain.Air && d.Count <= 0);
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

        // Hard Mode: limita o total de unidades de logística mantidas em campo.
        if (AIController.Instance != null && AIController.Instance.HardMode)
            desiredLogistics = Mathf.Min(desiredLogistics, AIController.Instance.MaxLogisticUnitsOnHardMode);

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
        foreach (AITacticalNeed op in mgr.GetOperationsForSlot(
                     PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))))
        {
            if (op.Type != AITacticalNeedType.SectorDefense) continue;
            if (op.CountOpenSlots(AINeedKind.Assault) > 0 || op.CountOpenSlots(AINeedKind.Artillery) > 0)
                count++;
        }
        return count;
    }

    private static int CountOpenSlots(TeamId aiTeam, UnitRole role)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
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
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
        if (plan == null) return;
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == role) { total++; if (slot.Filled) filled++; }
    }

    private static bool HasOpenDefensiveSlot(TeamId aiTeam)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
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
            if (unit == null || unit.SlotIndex != AIController.ResolveAISlotKey(aiTeam) || unit.IsDead || unit.IsEmbarked)
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
            if (!building.CanProduceUnitsForSlot(building.SlotIndex))
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
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex))
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
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)) continue;
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
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)) continue;
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
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)) continue;
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
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex))
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

    private sealed class RoleShoppingCandidate
    {
        public ConstructionManager Building;
        public UnitData Unit;
        public AIShoppingDemand Demand;
        public int DemandIndex;
        public int Score;
    }

    private sealed class RoleShoppingCart
    {
        public readonly List<RoleShoppingCandidate> Items = new List<RoleShoppingCandidate>();
        public int[] RemainingDemand;
        public bool[] CoveredDemand;
        public int RemainingBudget;
        public int UrgentFulfilled;
        public int CommittedEliteFulfilled;
        public string CommittedEliteUnitId;
        public int ExpansionCapturerFulfilled;
        public int ExpansionCapturerTarget;
        public int CoveragePriorityScore;
        public int DistinctCovered;
        public int FulfillmentPriorityScore;
        public int FulfilledCount;
        public int QualityScore;
        public int Spent;

        public RoleShoppingCart Clone()
        {
            var clone = new RoleShoppingCart
            {
                RemainingDemand = (int[])RemainingDemand.Clone(),
                CoveredDemand = (bool[])CoveredDemand.Clone(),
                RemainingBudget = RemainingBudget,
                UrgentFulfilled = UrgentFulfilled,
                CommittedEliteFulfilled = CommittedEliteFulfilled,
                CommittedEliteUnitId = CommittedEliteUnitId,
                ExpansionCapturerFulfilled = ExpansionCapturerFulfilled,
                ExpansionCapturerTarget = ExpansionCapturerTarget,
                CoveragePriorityScore = CoveragePriorityScore,
                DistinctCovered = DistinctCovered,
                FulfillmentPriorityScore = FulfillmentPriorityScore,
                FulfilledCount = FulfilledCount,
                QualityScore = QualityScore,
                Spent = Spent,
            };
            clone.Items.AddRange(Items);
            return clone;
        }
    }

    private const int RoleShoppingCartBeamWidth = 1024;

    // Penalidade aplicada ao capturador AGRESSIVO quando disputa uma demanda de CAPTURA pura contra o
    // capturador dedicado. Grande o bastante pra dominar o bônus de custo (cost/2, teto 25000) e o
    // nudge de modo (12000), garantindo que o dedicado vença — mas afeta só QualityScore, então o
    // agressivo segue como fallback quando é a única oferta pro slot.
    private const int DedicatedCapturerPreferencePenalty = 80000;

    private static List<ShoppingOrder> DecideRoleBased(AIWorldSnapshot snapshot)
    {
        var orders = new List<ShoppingOrder>();
        if (snapshot == null)
            return orders;

        List<AIShoppingDemand> demands = BuildRoleShoppingDemands(snapshot);
        AIElitePurchaseCommitment eliteCommitment =
            ResolveElitePurchaseCommitment(snapshot, demands);
        AIRosterKnowledge shoppingRoster = BuildRosterKnowledge(snapshot, log: false);
        CounterPressureInspection counterPressure = BuildCounterPressure(snapshot, shoppingRoster);
        var occupied = BuildProductionOccupiedCells();
        var usedBuildings = new HashSet<ConstructionManager>();
        int remaining = snapshot.Budget;

        // DOUTRINA DO ENXAME (Agressivo/conscriptionDoctrine): imposto de conscrição. Reserva o
        // custo do corpo Army mais barato de CADA produtor do exército livre — o carrinho
        // (incluindo o elite) só gasta o que couber POR CIMA da massa garantida. Ex.: 20k, 4
        // produtores, MBT 18k → imposto 4k, gastoLivre 16k < 18k: MBT não fecha, 4 soldados saem.
        // Com 26k → gastoLivre 22k: MBT fecha E os outros 3 compram soldado. Levemente conservador:
        // o produtor que o próprio carrinho usar também foi taxado (devolve só no turno seguinte).
        // Computado ANTES da reserva blitz/elite porque o gate "já dá pra comprar o MBT" precisa
        // somar o imposto — senão o blitz solta a reserva, o imposto come a diferença e o Obus mais
        // barato fura a fila de armor-first. Válvula: em Fase de Massacre a doutrina pausa (o caixa
        // inteiro volta pras demandas e a máquina de elite existente assume).
        int conscriptionTax = 0;
        bool conscriptionActive = false;
        if (AIController.Instance != null && AIController.Instance.ConscriptionDoctrine)
            conscriptionActive = !ResolveMassacrePhase(snapshot, AIController.Instance);
        if (conscriptionActive)
        {
            conscriptionTax = ComputeConscriptionTax(snapshot, occupied);
            if (conscriptionTax > 0)
                Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] doutrina do "
                    + $"enxame: imposto de conscrição={conscriptionTax} — demandas só gastam acima da "
                    + $"massa garantida (todo produtor do exército compra SEMPRE)");
        }

        // Reserva estratégica existe apenas para alvo ELITE com o core já formado.
        // Compras regulares permanecem gulosas para montar o exército rapidamente.
        int reserve = ComputeStrategicSavingReserve(
            snapshot, demands, remaining, eliteCommitment,
            out AIShoppingDemand reserveTarget);
        // Bootstrap Blitz: no Hard o primeiro Assalto É o MBT elite caro (básico banido). A reserva
        // elite normal exige um core que já inclui Assalto — impossível antes de comprar o primeiro.
        // Este bootstrap fura esse catch-22 e segura o grosso do caixa até o MBT ficar pagável.
        // Passa o imposto: o MBT só "fecha a conta" quando cabe DEPOIS da massa garantida.
        int blitzArmorReserve = ComputeBlitzFirstArmorReserve(
            snapshot, demands, remaining, conscriptionTax,
            out AIShoppingDemand blitzArmorTarget, out bool buyFirstMbtNow);
        if (blitzArmorReserve > reserve)
        {
            reserve = blitzArmorReserve;
            reserveTarget = blitzArmorTarget;
        }
        bool hasStrategicReserve = reserve > 0 && reserveTarget != null;

        // Collapsing aumenta o peso defensivo, mas nao invalida sozinho um compromisso
        // persistente. Apenas uma demanda urgente pode romper a reserva estrategica.
        bool macroLosing = AIController.GetMacroTerritoryForInspection(snapshot.AITeam).Losing;

        // RECRUTAMENTO FORÇADO (só HARD): sob pressão/Perdendo (no Hard, dirigido pela projeção de
        // força), corpo vem antes do elite. Enche cada produtor livre com o corpo mais BARATO (massa),
        // e o elite comprometido só entra no ÚLTIMO produtor se ainda couber no caixa depois da massa —
        // o resto vira reserva. Normal/Easy seguem o fluxo validado (retrato de hoje), intocados.
        if (AIController.Instance != null
            && AIController.Instance.ConscriptionWhenLosing
            && macroLosing)
        {
            RecruitmentSurgeFill(snapshot, orders, usedBuildings, occupied, ref remaining, demands, eliteCommitment);
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] RECRUTAMENTO FORÇADO: "
                + $"massa nos produtores primeiro, elite só no leftover — restante(reserva)={remaining}");
            return orders;
        }

        int expansionCapturerTarget = ComputeExpansionCapturerCartTarget(snapshot, demands);
        AIShoppingDemand reserveBreakingEmergency = FindReserveBreakingEmergency(demands);
        bool breakStrategicReserve = macroLosing && reserve > 0
            && reserveBreakingEmergency != null;
        if (breakStrategicReserve)
        {
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] perdendo o mapa: "
                + $"emergencia {FormatDemandCapability(reserveBreakingEmergency)} "
                + $"origem={reserveBreakingEmergency.Origin} rompe reserva estrategica ({reserve})");
            reserve = 0;
            hasStrategicReserve = false;
        }

        // CONCENTRAÇÃO: conta quantos prédios podem PRODUZIR de fato neste turno (podem produzir e a
        // célula de spawn não está ocupada — unidade amiga/inimiga em cima bloqueia). Se sobrou pouco
        // (1-2 slots) com caixa, NÃO dá pra comprar massa — então concentra o gasto: cada slot deve
        // levar a unidade mais FORTE que couber (libera o gate de elite). Sem isso a AI compra 1 peça
        // barata e deixa o resto do caixa parado (não há outro prédio pra gastar).
        int availableProductionSlots = 0;
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null || !b.CanProduceUnitsForSlot(snapshot.AISlotIndex) || b.OfferedUnits == null)
                continue;
            Vector3Int bc = b.CurrentCellPosition; bc.z = 0;
            if (!occupied.Contains(bc))
                availableProductionSlots++;
        }
        // Só concentra quando há caixa SOBRANDO que não vira massa: poucos slots E orçamento daria
        // pra 3+ corpos baratos (que não há onde produzir). Senão (early game, caixa baixo) segue
        // normal — expande, não despeja num elite cedo.
        bool concentrateSpend = availableProductionSlots > 0 && availableProductionSlots <= 2
            && remaining >= EstimateCheapBodyCost(snapshot) * 3;
        // Emergência = concentrando E perdendo o mapa. Aí ignora ATÉ a cadeia de elite (compra a peça
        // forte "a frio", sem possuir o tier anterior) — desespero defensivo, qualidade já que não há
        // como fazer quantidade. Concentração SEM perder mantém a cadeia (não fura a progressão à toa).
        bool concentrateEmergency = concentrateSpend && macroLosing;
        if (concentrateSpend)
        {
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] concentra gasto: "
                + $"só {availableProductionSlots} prédio(s) disponível(is) — cada slot leva a peça mais forte"
                + (hasStrategicReserve ? $" (reserva elite preservada={reserve})" : "")
                + (concentrateEmergency ? " (EMERGÊNCIA: cadeia de elite ignorada)" : ""));
        }

        // PRÉ-COMPRA do 1º MBT: quando o blitz decide que já dá pra comprar o MBT, a compra é
        // GARANTIDA fora do carrinho — senão a soma-de-cobertura do beam prefere largura (2-3 peças
        // baratas cobrindo mais demandas) à profundidade (1 MBT caro), e o "primeiro elite = MBT"
        // nunca acontece enquanto houver artilharia de rally / contra-pressão competindo. Ocupa o
        // produtor antes do carrinho; o troco (respeitando gordura + imposto) segue pro carrinho/fill.
        if (buyFirstMbtNow && blitzArmorTarget != null)
            TryPreCommitBlitzFirstMbt(
                snapshot, blitzArmorTarget, eliteCommitment,
                orders, usedBuildings, occupied, ref remaining);

        // Nota — fill do fim do turno: consome o imposto comprando a massa (só a reserva elite é
        // intocável lá). Com ConscriptionWhenLosing (Médio/Competitiva), Perdendo nunca chega aqui
        // (RecruitmentSurgeFill retorna antes); na doutrina (Formigueiro/Agressiva), Perdendo passa
        // por aqui mesmo — conscrição é sempre, e massacre nunca ativa perdendo.
        // Teto de logística do Hard Mode aplicado DEPOIS da fusão de demandas — ver
        // ApplyHardModeLogisticsCap. Precisa vir antes do log da fila pra ela anunciar o número
        // que vai ser realmente comprado.
        ApplyHardModeLogisticsCap(snapshot, demands);
        LogRoleShoppingQueue(snapshot, demands, remaining);
        if (expansionCapturerTarget > 0)
        {
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + $"expansão econômica: prioriza até {expansionCapturerTarget} capturador(es) "
                + $"no carrinho antes de diversificar");
        }
        RoleShoppingCart cart = BuildBestRoleShoppingCart(
            snapshot, demands, counterPressure, occupied, remaining - reserve - conscriptionTax,
            macroLosing, concentrateSpend, concentrateEmergency, expansionCapturerTarget,
            breakStrategicReserve ? null : eliteCommitment?.unitId);
        if (cart != null)
        {
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] carrinho "
                + $"itens={cart.Items.Count} demandas={cart.DistinctCovered} "
                + $"atendimentos={cart.FulfilledCount} gasto={cart.Spent} "
                + $"saldo livre={cart.RemainingBudget}"
                + (cart.ExpansionCapturerTarget > 0
                    ? $" expansãoCap={cart.ExpansionCapturerFulfilled}/{cart.ExpansionCapturerTarget}"
                    : ""));
            foreach (RoleShoppingCandidate item in cart.Items)
            {
                int index = IndexOf(item.Building.OfferedUnits, item.Unit);
                orders.Add(new ShoppingOrder
                {
                    Building = item.Building,
                    UnitToBuy = item.Unit,
                    SelectedIndex = index,
                });
                item.Demand.Count--;
                remaining -= item.Unit.cost;
                usedBuildings.Add(item.Building);
                Vector3Int productionCell = item.Building.CurrentCellPosition;
                productionCell.z = 0;
                occupied.Add(productionCell);
                Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                    + $"{item.Building.ConstructionDisplayName} compra {item.Unit.displayName} ${item.Unit.cost} "
                    + $"para {FormatDemandCapability(item.Demand)} origem={item.Demand.Origin} "
                    + $"pri={item.Demand.Priority} score={item.Score} restante={remaining}");
                if (eliteCommitment != null
                    && string.Equals(item.Unit.id, eliteCommitment.unitId,
                        System.StringComparison.Ordinal))
                {
                    AIIntelLedger.ClearElitePurchaseCommitment(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
                    Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                        + $"compromisso elite concluído: {item.Unit.displayName}");
                }
            }
        }
        else
        {
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + $"carrinho vazio: nenhuma oferta elegível atende demanda com gastoLivre="
                + $"{Mathf.Max(0, remaining - reserve - conscriptionTax)} — caixa preservado");
        }

        // DEFESA: não deixar prédio de produção VAZIO — cada casa aberta é captura fácil pro oponente
        // oportunista. Quando perdendo (Collapsing) ou em stance Defensiva, depois das compras por
        // demanda preenche cada prédio livre/não-ocupado com o defensor mais barato que couber
        // (prefere Assalto/Capturador). Gasta o caixa pra negar a captura em vez de deixar parado.
        // Perder o mapa nao torna toda fabrica uma torre defensiva. O preenchimento barato
        // so existe quando ha ameaca local visivel a uma base; longe dela a stance e as
        // demandas operacionais continuam decidindo a composicao.
        //
        // DOUTRINA DO ENXAME (Agressivo, SEMPRE): produtor nunca dorme. Depois das compras por
        // demanda, todo produtor livre compra o corpo Army mais barato gastando APENAS o
        // excedente acima da reserva estratégica — a poupança pro elite (Caça B, MBT...)
        // continua intocável, e Aeronáutica/Marinha só produzem quando a demanda real fecha
        // a conta. Tática do enxame: a AI poupa POR CIMA da produção de massa, nunca em vez
        // dela — o humano nunca ganha um turno de folga. Sob ameaça visível à base, o
        // preenchimento defensivo clássico prevalece (sem restrição de força).
        //
        // A reserva estratégica CEDE o imposto de conscrição pro fill: a poupança
        // (blitz/elite) pode reivindicar quase o caixa todo (ex.: 12k com reserva 11k),
        // mas a massa vem primeiro — sem essa cessão, o fill respeitava a reserva cheia
        // e só 1 produtor comprava (o horizonte da poupança estica; a doutrina aceita).
        bool defendFillBuildings = HasAnyVisibleEnemyNearOwnedBase(
            snapshot, DefensiveBaseThreatRange);
        bool hardSwarmDoctrine = conscriptionActive;
        if (defendFillBuildings || hardSwarmDoctrine)
            FillIdleProductionBuildings(
                snapshot, orders, usedBuildings, occupied, ref remaining,
                hasStrategicReserve ? Mathf.Max(0, reserve - conscriptionTax) : 0,
                armyMassOnly: hardSwarmDoctrine && !defendFillBuildings,
                counterPressure: counterPressure);

        foreach (AIShoppingDemand demand in demands)
            if (demand.Count > 0)
                Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] pendente "
                    + $"{FormatDemandCapability(demand)} x{demand.Count} pri={demand.Priority} "
                    + $"origem={demand.Origin} motivo={demand.Reason}");
        return orders;
    }

    private static AIShoppingDemand FindReserveBreakingEmergency(
        List<AIShoppingDemand> demands)
    {
        if (demands == null)
            return null;
        AIShoppingDemand best = null;
        foreach (AIShoppingDemand demand in demands)
            if (demand != null && demand.Count > 0 && demand.Urgent
                && (best == null || demand.Priority < best.Priority))
                best = demand;
        return best;
    }

    public static AIShoppingDemand FindReserveBreakingEmergencyForInspection(
        List<AIShoppingDemand> demands)
        => FindReserveBreakingEmergency(demands);

    private static RoleShoppingCart BuildBestRoleShoppingCart(
        AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands,
        CounterPressureInspection counterPressure,
        HashSet<Vector3Int> occupied,
        int spendableBudget,
        bool macroLosing,
        bool concentrateSpend,
        bool concentrateEmergency,
        int expansionCapturerTarget,
        string committedEliteUnitId)
    {
        if (snapshot?.MyBuildings == null || demands == null || demands.Count == 0
            || spendableBudget <= 0)
            return null;

        var buildings = new List<ConstructionManager>();
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                || building.OfferedUnits == null || building.OfferedUnits.Count == 0)
                continue;
            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            if (!occupied.Contains(cell))
                buildings.Add(building);
        }
        if (buildings.Count == 0)
            return null;

        // Primeiro processa os vendedores mais restritos. Isso preserva no beam as combinações
        // difíceis de substituir antes dos vendedores com catálogos amplos.
        buildings.Sort((a, b) =>
        {
            int offered = a.OfferedUnits.Count.CompareTo(b.OfferedUnits.Count);
            if (offered != 0) return offered;
            Vector3Int ac = a.CurrentCellPosition;
            Vector3Int bc = b.CurrentCellPosition;
            int x = ac.x.CompareTo(bc.x);
            return x != 0 ? x : ac.y.CompareTo(bc.y);
        });

        var initial = new RoleShoppingCart
        {
            RemainingDemand = new int[demands.Count],
            CoveredDemand = new bool[demands.Count],
            RemainingBudget = spendableBudget,
            ExpansionCapturerTarget = expansionCapturerTarget,
            CommittedEliteUnitId = committedEliteUnitId,
        };
        for (int i = 0; i < demands.Count; i++)
            initial.RemainingDemand[i] = Mathf.Max(0, demands[i].Count);

        var beam = new List<RoleShoppingCart> { initial };
        foreach (ConstructionManager building in buildings)
        {
            var next = new List<RoleShoppingCart>(beam.Count * 2);
            foreach (RoleShoppingCart state in beam)
            {
                // Não comprar também é uma escolha válida: caixa sem demanda elegível é preservado.
                next.Add(state);
                foreach (UnitData unit in building.OfferedUnits)
                {
                    if (unit == null || unit.cost <= 0 || unit.cost > state.RemainingBudget
                        || unit.militaryForce == MilitaryForce.Navy
                        || (!concentrateEmergency && !IsEliteChainAvailable(unit, snapshot)))
                        continue;

                    for (int demandIndex = 0; demandIndex < demands.Count; demandIndex++)
                    {
                        AIShoppingDemand demand = demands[demandIndex];
                        if (state.RemainingDemand[demandIndex] <= 0
                            || !IsRolePurchaseAllowed(unit, snapshot.Stance,
                                IsEmergencyShoppingDemand(demand))
                            || !DoesUnitMeetShoppingDemand(unit, demand)
                            || ShouldYieldGenericFireSupportToAntiInfantry(
                                unit, demand, counterPressure))
                            continue;

                        var candidate = new RoleShoppingCandidate
                        {
                            Building = building,
                            Unit = unit,
                            Demand = demand,
                            DemandIndex = demandIndex,
                            Score = ScoreRoleShoppingCandidate(
                                snapshot, unit, demand, spendableBudget,
                                counterPressure, macroLosing, concentrateSpend),
                        };
                        RoleShoppingCart expanded = state.Clone();
                        AddCandidateToRoleShoppingCart(expanded, candidate);
                        next.Add(expanded);
                    }
                }
            }

            next.Sort(CompareRoleShoppingCarts);
            if (next.Count > RoleShoppingCartBeamWidth)
                next.RemoveRange(RoleShoppingCartBeamWidth, next.Count - RoleShoppingCartBeamWidth);
            beam = next;
        }

        beam.Sort(CompareRoleShoppingCarts);
        RoleShoppingCart best = beam.Count > 0 ? beam[0] : null;
        return best != null && best.Items.Count > 0 ? best : null;
    }

    private static void AddCandidateToRoleShoppingCart(
        RoleShoppingCart cart, RoleShoppingCandidate candidate)
    {
        int demandIndex = candidate.DemandIndex;
        bool firstCoverage = !cart.CoveredDemand[demandIndex];
        cart.Items.Add(candidate);
        cart.RemainingDemand[demandIndex]--;
        cart.RemainingBudget -= candidate.Unit.cost;
        cart.Spent += candidate.Unit.cost;
        cart.FulfilledCount++;
        cart.QualityScore += candidate.Score;
        int priorityValue = Mathf.Max(1, 100 - candidate.Demand.Priority);
        cart.FulfillmentPriorityScore += priorityValue;
        if (candidate.Demand.Urgent)
            cart.UrgentFulfilled++;
        if (!string.IsNullOrEmpty(cart.CommittedEliteUnitId)
            && string.Equals(candidate.Unit.id, cart.CommittedEliteUnitId,
                System.StringComparison.Ordinal))
            cart.CommittedEliteFulfilled = 1;
        if (candidate.Demand.Role == UnitRole.Capturador
            && cart.ExpansionCapturerFulfilled < cart.ExpansionCapturerTarget)
            cart.ExpansionCapturerFulfilled++;
        if (firstCoverage)
        {
            cart.CoveredDemand[demandIndex] = true;
            cart.DistinctCovered++;
            cart.CoveragePriorityScore += priorityValue;
        }
    }

    // Sort ascendente: o melhor carrinho precisa ficar no índice zero.
    private static int CompareRoleShoppingCarts(RoleShoppingCart a, RoleShoppingCart b)
    {
        int compare = b.UrgentFulfilled.CompareTo(a.UrgentFulfilled);
        if (compare != 0) return compare;
        compare = b.CommittedEliteFulfilled.CompareTo(a.CommittedEliteFulfilled);
        if (compare != 0) return compare;
        // Opening econômico: enquanto existem muitos neutros e falta massa de captura,
        // repetir capturador gera renda mais cedo e vale mais que diversificar combate.
        compare = b.ExpansionCapturerFulfilled.CompareTo(a.ExpansionCapturerFulfilled);
        if (compare != 0) return compare;
        compare = b.CoveragePriorityScore.CompareTo(a.CoveragePriorityScore);
        if (compare != 0) return compare;
        compare = b.DistinctCovered.CompareTo(a.DistinctCovered);
        if (compare != 0) return compare;
        compare = b.FulfillmentPriorityScore.CompareTo(a.FulfillmentPriorityScore);
        if (compare != 0) return compare;
        compare = b.FulfilledCount.CompareTo(a.FulfilledCount);
        if (compare != 0) return compare;
        compare = b.QualityScore.CompareTo(a.QualityScore);
        if (compare != 0) return compare;
        compare = b.Spent.CompareTo(a.Spent);
        if (compare != 0) return compare;
        return CompareRoleShoppingCartSignature(a, b);
    }

    private static int CompareRoleShoppingCartSignature(RoleShoppingCart a, RoleShoppingCart b)
    {
        int count = Mathf.Min(a.Items.Count, b.Items.Count);
        for (int i = 0; i < count; i++)
        {
            string aId = a.Items[i].Unit != null ? a.Items[i].Unit.id : "";
            string bId = b.Items[i].Unit != null ? b.Items[i].Unit.id : "";
            int compare = string.CompareOrdinal(aId, bId);
            if (compare != 0) return compare;
        }
        return a.Items.Count.CompareTo(b.Items.Count);
    }

    private static int ComputeExpansionCapturerCartTarget(
        AIWorldSnapshot snapshot, List<AIShoppingDemand> demands)
    {
        if (snapshot == null || demands == null)
            return 0;

        AIController.MacroTerritoryInspection macro =
            AIController.GetMacroTerritoryForInspection(snapshot.AITeam);
        if (!string.Equals(macro.PhaseRaw, "EarlyExpansion", System.StringComparison.Ordinal)
            || macro.NeutralSectors <= 0)
            return 0;

        int activeCapturers = CountCompositionRole(snapshot, UnitRole.Capturador);
        int massTarget = Instance != null ? Instance.MinCapturerMassForSupport : 4;
        int missingMass = Mathf.Max(0, massTarget - activeCapturers);
        if (missingMass <= 0)
            return 0;

        int demandedCapturers = 0;
        foreach (AIShoppingDemand demand in demands)
            if (demand != null && demand.Count > 0 && demand.Role == UnitRole.Capturador)
                demandedCapturers += demand.Count;

        return Mathf.Min(missingMass, demandedCapturers);
    }

    private static AIElitePurchaseCommitment ResolveElitePurchaseCommitment(
        AIWorldSnapshot snapshot, List<AIShoppingDemand> demands)
    {
        AIElitePurchaseCommitment existing =
            AIIntelLedger.GetElitePurchaseCommitment(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        if (existing != null)
        {
            UnitData unit = FindOfferedUnitById(snapshot, existing.unitId, out _,
                existing.counterEscalation);
            bool counterPressureStillPresent = !existing.counterEscalation
                || HasCounterPressureDemand(demands, existing.counterCategory);
            string invalidReason = null;
            if (unit == null)
                invalidReason = "unidade não está mais ofertada";
            else if (unit.eliteLevel != existing.eliteLevel
                || UnitRoleCompatibility.ResolveCompositionRole(unit) != existing.role)
                invalidReason = "dados da unidade mudaram";
            bool valid = invalidReason == null;
            if (!valid)
            {
                Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                    + $"cancela compromisso elite {existing.unitId}: {invalidReason}");
                AIIntelLedger.ClearElitePurchaseCommitment(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
                existing = null;
            }
            else
            {
                EnsureEliteCommitmentDemand(demands, existing);
                Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                    + $"mantém compromisso elite: {unit.displayName} ${unit.cost} "
                    + $"desde T{existing.committedTurn}"
                    + (!counterPressureStillPresent
                        ? " (pressão original coberta; compromisso persiste até a compra)"
                        : "")
                    + (!IsEliteChainAvailable(unit, snapshot)
                        ? " (aguardando pré-requisito da cadeia)"
                        : ""));
                return existing;
            }
        }

        bool strategicCounterEscalation = HasStrategicCounterEscalationDemand(demands);
        if (!IsOperationalCoreReadyForElite(snapshot, demands)
            && !strategicCounterEscalation)
            return null;

        AIShoppingDemand bestDemand = null;
        UnitData bestUnit = null;
        foreach (AIShoppingDemand demand in demands)
        {
            if (demand == null || demand.Count <= 0 || demand.MinEliteLevel <= 0)
                continue;
            UnitData candidate = FindBestOfferedUnitForDemand(snapshot, demand);
            if (candidate == null)
                continue;
            if (bestDemand == null || candidate.eliteLevel < bestUnit.eliteLevel
                || (candidate.eliteLevel == bestUnit.eliteLevel
                    && demand.Priority < bestDemand.Priority)
                || (candidate.eliteLevel == bestUnit.eliteLevel
                    && demand.Priority == bestDemand.Priority && candidate.cost > bestUnit.cost))
            {
                bestDemand = demand;
                bestUnit = candidate;
            }
        }
        if (bestDemand == null || bestUnit == null)
            return null;

        var commitment = new AIElitePurchaseCommitment
        {
            unitId = bestUnit.id,
            role = UnitRoleCompatibility.ResolveCompositionRole(bestUnit),
            eliteLevel = bestUnit.eliteLevel,
            targetCost = bestUnit.cost,
            committedTurn = snapshot.TurnNumber,
            counterEscalation = bestDemand.StrategicEscalation,
            counterCategory = bestDemand.RequiredWeaponCategory
                ?? WeaponCategory.AntiTanque,
            counterHasTargetClass = bestDemand.TargetClass.HasValue,
            counterTargetClass = bestDemand.TargetClass ?? GameUnitClass.Armored,
        };
        AIIntelLedger.SetElitePurchaseCommitment(PlayerSlotId.FromIndex(snapshot.AISlotIndex), commitment);
        Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
            + $"novo compromisso elite: {bestUnit.displayName} ${bestUnit.cost} "
            + $"papel={commitment.role} elite={commitment.eliteLevel}");
        return commitment;
    }

    private static void EnsureEliteCommitmentDemand(
        List<AIShoppingDemand> demands, AIElitePurchaseCommitment commitment)
    {
        if (FindDemandForEliteCommitment(demands, commitment) != null)
            return;
        AIShoppingDemand demand = NewRoleDemand(
            commitment.role, 1, 23, "elite-commitment",
            $"compromisso desde T{commitment.committedTurn}", false);
        demand.MinEliteLevel = commitment.eliteLevel;
        demand.MaxEliteLevel = commitment.eliteLevel;
        demand.StrategicEscalation = commitment.counterEscalation;
        if (commitment.counterEscalation)
        {
            demand.RequiredWeaponCategory = commitment.counterCategory;
            if (commitment.counterHasTargetClass)
            {
                demand.TargetClass = commitment.counterTargetClass;
                demand.MinTargetPriority = BazookaTargetPriority.Primary;
            }
            demand.RequiredUnitId = commitment.unitId;
        }
        demands.Add(demand);
        demands.Sort((a, b) =>
        {
            int urgent = b.Urgent.CompareTo(a.Urgent);
            if (urgent != 0) return urgent;
            int priority = a.Priority.CompareTo(b.Priority);
            if (priority != 0) return priority;
            return ((int)a.Role).CompareTo((int)b.Role);
        });
    }

    private static AIShoppingDemand FindDemandForEliteCommitment(
        List<AIShoppingDemand> demands, AIElitePurchaseCommitment commitment)
    {
        if (demands == null || commitment == null)
            return null;
        foreach (AIShoppingDemand demand in demands)
            if (demand != null && demand.Count > 0
                && (demand.Role == commitment.role || demand.Role == UnitRole.None)
                && demand.MinEliteLevel <= commitment.eliteLevel
                && demand.MaxEliteLevel >= commitment.eliteLevel
                && (!commitment.counterEscalation
                    || (demand.RequiredWeaponCategory == commitment.counterCategory
                        && (!commitment.counterHasTargetClass
                            || demand.TargetClass == commitment.counterTargetClass))))
                return demand;
        return null;
    }

    private static bool HasStrategicCounterEscalationDemand(
        List<AIShoppingDemand> demands,
        WeaponCategory? category = null,
        GameUnitClass? targetClass = null)
    {
        if (demands == null)
            return false;
        foreach (AIShoppingDemand demand in demands)
            if (demand != null && demand.Count > 0 && demand.StrategicEscalation
                && demand.RequiredWeaponCategory.HasValue
                && (!category.HasValue
                    || demand.RequiredWeaponCategory.Value == category.Value)
                && (!targetClass.HasValue || demand.TargetClass == targetClass.Value))
                return true;
        return false;
    }

    private static bool HasCounterPressureDemand(
        List<AIShoppingDemand> demands,
        WeaponCategory category)
    {
        if (demands == null)
            return false;
        foreach (AIShoppingDemand demand in demands)
            if (demand != null && demand.Count > 0
                && demand.RequiredWeaponCategory == category
                && demand.Origin != null
                && demand.Origin.Contains("counter-pressure"))
                return true;
        return false;
    }

    private static UnitData FindOfferedUnitById(
        AIWorldSnapshot snapshot, string unitId, out bool availableNow,
        bool allowStanceBypass = false)
    {
        availableNow = false;
        if (snapshot?.MyBuildings == null || string.IsNullOrEmpty(unitId))
            return null;
        HashSet<Vector3Int> occupied = BuildProductionOccupiedCells();
        UnitData found = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                || building.OfferedUnits == null)
                continue;
            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || !string.Equals(unit.id, unitId, System.StringComparison.Ordinal))
                    continue;
                found = unit;
                if (!occupied.Contains(cell)
                    && IsRolePurchaseAllowed(unit, snapshot.Stance, allowStanceBypass))
                    availableNow = true;
            }
        }
        return found;
    }

    private static UnitData FindBestOfferedUnitForDemand(
        AIWorldSnapshot snapshot, AIShoppingDemand demand)
    {
        UnitData best = null;
        if (snapshot?.MyBuildings == null || demand == null)
            return null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                || building.OfferedUnits == null)
                continue;
            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.eliteLevel <= 0
                    || !IsEliteChainAvailable(unit, snapshot)
                    || !IsRolePurchaseAllowed(unit, snapshot.Stance,
                        IsEmergencyShoppingDemand(demand))
                    || !DoesUnitMeetShoppingDemand(unit, demand))
                    continue;
                if (best == null || unit.cost < best.cost)
                    best = unit;
            }
        }
        return best;
    }

    private static bool IsOperationalCoreReadyForElite(
        AIWorldSnapshot snapshot, List<AIShoppingDemand> demands)
    {
        if (snapshot == null || snapshot.IncomePerTurn <= 0)
            return false;
        bool rallyAssemblyActive = HasActiveRallyAssembly(
            PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        foreach (AIShoppingDemand demand in demands)
            if (demand != null && demand.Count > 0
                && (demand.Urgent
                    || (!rallyAssemblyActive
                        && demand.Role == UnitRole.Capturador && demand.Priority <= 16)))
                return false;
        return HasOperationalCore(snapshot);
    }

    public static bool HasActiveRallyAssembly(PlayerSlotId slotId)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(slotId);
        if (plan?.Objectives == null)
            return false;
        foreach (SectorObjective objective in plan.Objectives)
            if (objective != null
                && objective.ObjectiveType == AIObjectiveType.RallyAssembly
                && (objective.RallyState == AIRallyAssemblyState.Assembling
                    || objective.RallyState == AIRallyAssemblyState.Ready))
                return true;
        return false;
    }

    // Alvos do núcleo, JÁ descontando componentes que este mapa não vende. Ponto único: o gate
    // (HasOperationalCore) e o gradiente (ComputeOperationalCoreMaturity) precisam enxergar
    // exatamente os mesmos alvos, senão divergem e a maturidade nunca chega a 1 com o gate aberto.
    // Cache dos alvos do núcleo por PASSADA de shopping. HasOperationalCore/Maturity são chamados
    // dentro do scoring, que roda numa busca em feixe (largura 1024) — recomputar a varredura de
    // ofertas por candidato custava dezenas de segundos. Os alvos são INVARIANTES durante o turno
    // (mínimos do AI Manager + ofertas fixas), então memoizamos por REFERÊNCIA de snapshot: novo
    // turno = novo snapshot = recomputa; mesma passada = leitura O(1).
    [System.NonSerialized] private static AIWorldSnapshot s_coreTargetsSnapshot;
    private static int s_coreCapTarget, s_coreAssTarget, s_coreArtTarget;

    private static void ResolveOperationalCoreTargets(
        AIWorldSnapshot snapshot,
        out int capturerTarget,
        out int assaultTarget,
        out int artilleryTarget)
    {
        if (ReferenceEquals(snapshot, s_coreTargetsSnapshot))
        {
            capturerTarget = s_coreCapTarget;
            assaultTarget = s_coreAssTarget;
            artilleryTarget = s_coreArtTarget;
            return;
        }

        // Composição mínima do núcleo (gate de elite) vem do AI Manager, com par por modo (normal/hard).
        capturerTarget  = AIController.Instance != null ? AIController.Instance.CoreMinInfantry  : 2;
        assaultTarget   = AIController.Instance != null ? AIController.Instance.CoreMinAssault   : 2;
        artilleryTarget = AIController.Instance != null ? AIController.Instance.CoreMinArtillery : 1;

        // Componente INSATISFAZÍVEL sai da conta: exigir artilharia num mapa cujos produtores não
        // ofertam artilharia trava o elite para sempre (mesmo padrão do "gate inaplicável" do rally).
        // Foi essa rigidez que obrigou o bootstrap do MBT no Hard a furar o próprio gate — o código
        // chama de catch-22 em ComputeBlitzFirstArmorReserve.
        if (capturerTarget > 0 && !CanAnyOfferedUnitCloseCore(snapshot, UnitRole.Capturador))
            capturerTarget = 0;
        if (assaultTarget > 0 && !CanAnyOfferedUnitCloseCore(snapshot, UnitRole.Assalto))
            assaultTarget = 0;
        if (artilleryTarget > 0 && !CanAnyOfferedUnitCloseCore(snapshot, UnitRole.FogoIndireto))
            artilleryTarget = 0;

        s_coreTargetsSnapshot = snapshot;
        s_coreCapTarget = capturerTarget;
        s_coreAssTarget = assaultTarget;
        s_coreArtTarget = artilleryTarget;
    }

    // Algum produtor da IA oferta unidade que CONTA para este componente do núcleo?
    // Usa ResolveCompositionRole de propósito — é o mesmo predicado de CountCompositionRole. Usar
    // CanSatisfy aqui faria um CapturadorAgressivo "prometer" fechar o slot de Assalto que ele nunca
    // conta, e o alvo continuaria inalcançável.
    private static bool CanAnyOfferedUnitCloseCore(AIWorldSnapshot snapshot, UnitRole role)
    {
        if (snapshot?.MyBuildings == null)
            return true; // sem informação de oferta: não afrouxa o gate
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || building.OfferedUnits == null) continue;
            foreach (UnitData offered in building.OfferedUnits)
                if (offered != null && UnitRoleCompatibility.ResolveCompositionRole(offered) == role)
                    return true;
        }
        return false;
    }

    private static bool HasOperationalCore(AIWorldSnapshot snapshot)
    {
        if (snapshot == null)
            return false;
        ResolveOperationalCoreTargets(snapshot, out int capturerTarget, out int assaultTarget, out int artilleryTarget);
        return CountCompositionRole(snapshot, UnitRole.Capturador) >= capturerTarget
            && CountCompositionRole(snapshot, UnitRole.Assalto) >= assaultTarget
            && CountCompositionRole(snapshot, UnitRole.FogoIndireto) >= artilleryTarget;
    }

    private static float ComputeOperationalCoreMaturity(AIWorldSnapshot snapshot)
    {
        if (snapshot == null)
            return 0f;
        // Mesma composição-alvo do gate de elite, pelo MESMO resolvedor (inclui o desconto de
        // componente insatisfazível). Alvo 0 = componente já satisfeito.
        ResolveOperationalCoreTargets(snapshot, out int capturerTarget, out int assaultTarget, out int artilleryTarget);
        float capturer = capturerTarget <= 0 ? 1f : Mathf.Clamp01(
            CountCompositionRole(snapshot, UnitRole.Capturador) / (float)capturerTarget);
        float assault = assaultTarget <= 0 ? 1f : Mathf.Clamp01(
            CountCompositionRole(snapshot, UnitRole.Assalto) / (float)assaultTarget);
        float fire = artilleryTarget <= 0 ? 1f : Mathf.Clamp01(
            CountCompositionRole(snapshot, UnitRole.FogoIndireto) / (float)artilleryTarget);
        return (capturer + assault + fire) / 3f;
    }

    // Decide quanto de caixa segurar para um elite ou uma capacidade crítica prioritária
    // alcançável em poucos turnos. O excedente continua disponível para compras menores.
    // Retorna 0 quando não há nada para poupar (guloso normal).
    private static int ComputeStrategicSavingReserve(
        AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands,
        int remaining,
        AIElitePurchaseCommitment commitment,
        out AIShoppingDemand target)
    {
        target = null;
        if (Instance == null || demands == null || remaining <= 0)
            return 0;
        int maxTurns = AIController.Instance != null ? AIController.Instance.EliteSaveTurns : 1;
        if (maxTurns <= 0)
            return 0;

        int income = Mathf.Max(0, snapshot.IncomePerTurn);
        int reach = remaining + income * maxTurns;
        int armySize = snapshot.MyUnits != null ? snapshot.MyUnits.Count : 0;
        bool coreReady = IsOperationalCoreReadyForElite(snapshot, demands);
        if (!coreReady && commitment == null)
            return 0;

        int targetCost = 0;
        if (commitment != null)
        {
            target = FindDemandForEliteCommitment(demands, commitment);
            UnitData committedUnit = FindOfferedUnitById(snapshot, commitment.unitId,
                out bool availableNow, commitment.counterEscalation);
            if (target != null && committedUnit != null)
            {
                targetCost = committedUnit.cost;
                if (targetCost <= remaining && availableNow)
                    return 0;
            }
        }

        foreach (AIShoppingDemand d in demands)
        {
            if (target != null)
                break;
            if (d.Count <= 0)
                continue;
            bool availableNow = true;
            int cost;
            if (d.MinEliteLevel > 0)
            {
                cost = FindCheapestBuildableCost(snapshot, d, out availableNow);
            }
            else if (IsCriticalCapabilityDemand(d) && d.Priority <= 15)
            {
                cost = FindBestCriticalEliteCapabilityCost(snapshot, d, out availableNow);
            }
            else
            {
                continue;
            }
            if (cost <= 0 || cost > reach)
                continue;
            if (cost <= remaining && availableNow)
                continue; // já cabe e existe produção livre neste turno
            if (target == null || d.Priority < target.Priority
                || (d.Priority == target.Priority && cost > targetCost))
            {
                target = d;
                targetCost = cost;
            }
        }
        if (target == null)
            return 0;

        // Elite protege apenas o caixa necessário para garantir a compra no próximo turno.
        // A margem operacional cresce junto com o tamanho do exército: exército pequeno usa
        // tudo para formar core; exército maduro protege o percentual máximo configurado.
        // Ex.: caixa 13113 + renda 14000 - alvo 14500 = 12613; com margem 20%, gasto livre
        // = 10090 e caixa mínimo preservado = 3023.
        int reserve;
        float maturity = ComputeOperationalCoreMaturity(snapshot);
        float maintenancePct = AIController.Instance != null
            ? Mathf.Clamp01(AIController.Instance.EliteMaintenanceReservePercent / 100f) * maturity
            : 0.2f * maturity;
        int projectedAfterTarget = Mathf.Max(0, remaining + income - targetCost);
        int maintenanceReserve = Mathf.CeilToInt(projectedAfterTarget * maintenancePct);
        int spendableNow = Mathf.Max(0, projectedAfterTarget - maintenanceReserve);
        reserve = Mathf.Clamp(remaining - spendableNow, 0, remaining);
        Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] poupando p/ "
            + $"{FormatDemandCapability(target)} custo={targetCost} renda={income} maxTurnos={maxTurns} "
            + $"coreOperacional={(coreReady ? "pronto" : "comprometido")} unidades={armySize} "
            + $"maturidade={maturity:P0} margem={maintenancePct:P0} "
            + $"saldoPosAlvo={projectedAfterTarget} manutencao={maintenanceReserve} "
            + $"reserva={reserve} budget={remaining} gastoLivre={remaining - reserve}");
        return reserve;
    }

    // Bootstrap da poupança Blitzkrieg para o PRIMEIRO blindado elite (MBT). No Hard o assalto
    // básico é banido, então a única peça de Assalto comprável é o MBT elite (caro). A reserva
    // elite padrão (ComputeStrategicSavingReserve) só forma compromisso com o core já pronto — que
    // exige Assalto em campo, impossível antes de comprar o primeiro. Este método fura esse
    // deadlock: enquanto não há Assalto elite em campo e há demanda de Assalto pendente, segura o
    // grosso do caixa até o MBT ficar pagável, liberando apenas um corpo barato (renda/screen).
    private static int ComputeBlitzFirstArmorReserve(
        AIWorldSnapshot snapshot, List<AIShoppingDemand> demands, int remaining,
        int conscriptionTax, out AIShoppingDemand target, out bool buyNow)
    {
        target = null;
        buyNow = false;
        if (snapshot == null || demands == null || remaining <= 0)
            return 0;
        if (AIController.Instance == null || !AIController.Instance.HardMode)
            return 0;
        if (CountActiveEliteAssaultUnits(snapshot) > 0)
            return 0;

        // NÃO entesoura pro MBT antes de ter a massa de captura inicial. No começo (poucos/zero
        // capturadores) a prioridade é EXPANDIR — spam de capturador barato. Só passa a poupar pro
        // blindado quando o núcleo de captura já existe (mesmo limiar que libera suporte). Sem isso
        // a reserva estrangulava o early game comprando 1 corpo por turno em vez de expandir.
        int capturerMass = CountCompositionRole(snapshot, UnitRole.Capturador);
        int massGate = Instance != null ? Instance.MinCapturerMassForSupport : 4;
        if (capturerMass < massGate)
            return 0;

        AIShoppingDemand assaultDemand = null;
        foreach (AIShoppingDemand d in demands)
        {
            if (d == null || d.Count <= 0 || d.Role != UnitRole.Assalto)
                continue;
            if (d.Domain == Domain.Air) // AAA/ar não é a peça de ruptura terrestre
                continue;
            if (assaultDemand == null || d.Priority < assaultDemand.Priority)
                assaultDemand = d;
        }
        if (assaultDemand == null)
            return 0;

        int mbtCost = FindCheapestBuildableCost(snapshot, assaultDemand, out bool availableNow);
        if (mbtCost <= 0) // MBT não ofertado / cadeia indisponível: nada a poupar
            return 0;

        int income = Mathf.Max(0, snapshot.IncomePerTurn);
        float upkeepBufferPct = BlitzArmorUpkeepReservePercent;
        // Gordura de manutenção SEMPRE reservada p/ o Serviço do Comando (~10% do caixa). A compra
        // do MBT não pode zerar o caixa, senão o Refuel/reparo cobrado na Fase 1 do turno seguinte
        // não fecha. Por isso só compra quando dá pra pagar MBT + gordura, e mesmo aí segura os 20%.
        int upkeepFat = Mathf.CeilToInt(remaining * upkeepBufferPct);

        // Já dá pra comprar o MBT mantendo a gordura E a massa de conscrição? O imposto entra na
        // conta: o carrinho gasta remaining - reserva - imposto, então o MBT só cabe quando o caixa
        // cobre MBT + gordura + imposto. Sem isso o blitz soltava a reserva na janela em que o
        // imposto ainda comia a diferença, e o Obus (mais barato) furava a fila de armor-first.
        if (mbtCost + upkeepFat + conscriptionTax <= remaining && availableNow)
        {
            target = assaultDemand;
            buyNow = true;
            UnitData mbtNow = FindBestOfferedUnitForDemand(snapshot, assaultDemand);
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] blitz_armor: "
                + $"compra 1º MBT {(mbtNow != null ? mbtNow.displayName : "Assalto")} custo={mbtCost} "
                + $"caixa={remaining} segura gordura={upkeepFat} imposto={conscriptionTax} ({upkeepBufferPct:P0} p/ Serviço do Comando)");
            return upkeepFat;
        }

        // Ainda juntando. A renda NÃO acumula 100% rumo ao MBT — o Serviço do Comando drena caixa
        // todo turno (custo dinâmico, Fase 1) e ainda liberamos um corpo barato por turno. Desconta a
        // gordura da renda projetada; senão a conta nunca fecha e a IA entesoura eternamente. Ok
        // esperar até 3 rodadas comprando soldado enquanto junta (peça de maior ticket).
        int netIncome = Mathf.FloorToInt(income * (1f - upkeepBufferPct));
        int maxTurns = Mathf.Max(3, AIController.Instance.EliteSaveTurns);
        // Alcance conta com o imposto: se nem em maxTurns o caixa cobre MBT + gordura + massa,
        // não entesoura o grosso (segue expandindo/spammando). Mesma conta do gate de compra.
        if (remaining + netIncome * maxTurns < mbtCost + upkeepFat + conscriptionTax)
            return 0;

        // Segura o grosso; libera um corpo barato (soldado) por turno — vai comprando soldados
        // enquanto junta, sem deixar a fábrica ociosa e crescendo renda/screen.
        int cheapBody = EstimateCheapBodyCost(snapshot);
        int spendable = Mathf.Clamp(cheapBody, 0, remaining);
        int reserve = Mathf.Clamp(remaining - spendable, 0, remaining);
        if (reserve <= 0)
            return 0;

        target = assaultDemand;
        UnitData mbt = FindBestOfferedUnitForDemand(snapshot, assaultDemand);
        Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] blitz_armor: "
            + $"poupando p/ 1º MBT {(mbt != null ? mbt.displayName : "Assalto")} custo={mbtCost} "
            + $"caixa={remaining} renda={income} rendaLiq={netIncome} gordura={upkeepFat} "
            + $"({upkeepBufferPct:P0} p/ Serviço do Comando) horizonte={maxTurns}T reserva={reserve} "
            + $"gastoLivre={remaining - reserve} (vai comprando soldado até o MBT)");
        return reserve;
    }

    // Pré-compra GARANTIDA do 1º MBT quando o blitz sinaliza buyNow. Emite a ordem fora do carrinho
    // (o beam prefere largura à profundidade e o MBT caro nunca ganharia a soma-de-cobertura sozinho).
    // Escolhe o produtor LIVRE mais barato que satisfaz a demanda-alvo; ocupa-o pro carrinho/fill
    // pularem. Idempotente por chamada: decrementa a demanda e retorna false se não achar produtor.
    private static bool TryPreCommitBlitzFirstMbt(
        AIWorldSnapshot snapshot, AIShoppingDemand target,
        AIElitePurchaseCommitment eliteCommitment,
        List<ShoppingOrder> orders, HashSet<ConstructionManager> usedBuildings,
        HashSet<Vector3Int> occupied, ref int remaining)
    {
        if (snapshot?.MyBuildings == null || target == null || target.Count <= 0)
            return false;

        ConstructionManager bestBuilding = null;
        UnitData bestUnit = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || usedBuildings.Contains(building)
                || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex) || building.OfferedUnits == null)
                continue;
            Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
            if (occupied.Contains(cell))
                continue;
            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.cost <= 0 || unit.cost > remaining
                    || unit.militaryForce == MilitaryForce.Navy
                    || !IsEliteChainAvailable(unit, snapshot)
                    || !IsRolePurchaseAllowed(unit, snapshot.Stance, IsEmergencyShoppingDemand(target))
                    || !DoesUnitMeetShoppingDemand(unit, target))
                    continue;
                if (bestUnit == null || unit.cost < bestUnit.cost)
                {
                    bestUnit = unit;
                    bestBuilding = building;
                }
            }
        }
        if (bestUnit == null || bestBuilding == null)
            return false;

        int idx = IndexOf(bestBuilding.OfferedUnits, bestUnit);
        orders.Add(new ShoppingOrder { Building = bestBuilding, UnitToBuy = bestUnit, SelectedIndex = idx });
        remaining -= bestUnit.cost;
        usedBuildings.Add(bestBuilding);
        Vector3Int pc = bestBuilding.CurrentCellPosition; pc.z = 0;
        occupied.Add(pc);
        target.Count = Mathf.Max(0, target.Count - 1);
        Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] blitz_armor: "
            + $"PRÉ-COMPRA garantida do 1º MBT {bestUnit.displayName} ${bestUnit.cost} em "
            + $"{bestBuilding.ConstructionDisplayName} (fura a soma-de-cobertura do carrinho) restante={remaining}");
        if (eliteCommitment != null
            && string.Equals(bestUnit.id, eliteCommitment.unitId, System.StringComparison.Ordinal))
        {
            AIIntelLedger.ClearElitePurchaseCommitment(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + $"compromisso elite concluído (pré-compra): {bestUnit.displayName}");
        }
        return true;
    }

    private static bool IsCriticalCapabilityDemand(AIShoppingDemand demand)
        => demand != null && (demand.Urgent || demand.RequiredWeaponCategory.HasValue);

    private static int FindBestCriticalEliteCapabilityCost(
        AIWorldSnapshot snapshot,
        AIShoppingDemand demand,
        out bool availableNow)
    {
        availableNow = false;
        if (snapshot == null || snapshot.MyBuildings == null || demand == null)
            return 0;

        CounterPressureInspection pressure = BuildCounterPressure(snapshot);
        HashSet<Vector3Int> occupied = BuildProductionOccupiedCells();
        UnitData best = null;
        float bestFit = float.MinValue;
        bool bestAvailableNow = false;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                || building.OfferedUnits == null)
                continue;

            Vector3Int productionCell = building.CurrentCellPosition;
            productionCell.z = 0;
            bool buildingAvailableNow = !occupied.Contains(productionCell);
            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.cost <= 0 || unit.eliteLevel <= 0
                    || unit.militaryForce == MilitaryForce.Navy
                    || !IsRolePurchaseAllowed(unit, snapshot.Stance,
                        IsEmergencyShoppingDemand(demand))
                    || !IsEliteChainAvailable(unit, snapshot)
                    || !DoesUnitMeetShoppingDemand(unit, demand))
                    continue;

                float fit = ScoreCounterFit(unit, pressure);
                if (best == null
                    || (buildingAvailableNow && !bestAvailableNow)
                    || (buildingAvailableNow == bestAvailableNow && fit > bestFit)
                    || (buildingAvailableNow == bestAvailableNow
                        && Mathf.Approximately(fit, bestFit) && unit.eliteLevel > best.eliteLevel)
                    || (buildingAvailableNow == bestAvailableNow
                        && Mathf.Approximately(fit, bestFit) && unit.eliteLevel == best.eliteLevel
                        && unit.cost > best.cost))
                {
                    best = unit;
                    bestFit = fit;
                    bestAvailableNow = buildingAvailableNow;
                }
            }
        }

        if (best == null)
        {
            Debug.LogWarning($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + $"demanda crítica sem oferta habilitada: {FormatDemandCapability(demand)}");
            return 0;
        }

        availableNow = bestAvailableNow;
        Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] alvo elite crítico "
            + $"{FormatDemandCapability(demand)} -> {best.displayName} custo={best.cost} "
            + $"fit={bestFit:F1} elite={best.eliteLevel} disponívelAgora={availableNow}");
        return best.cost;
    }

    private static int FindCheapestBuildableCost(AIWorldSnapshot snapshot, AIShoppingDemand demand)
        => FindCheapestBuildableCost(snapshot, demand, out _);

    private static int FindCheapestBuildableCost(
        AIWorldSnapshot snapshot,
        AIShoppingDemand demand,
        out bool availableNow)
    {
        availableNow = false;
        if (snapshot == null || snapshot.MyBuildings == null)
            return 0;
        HashSet<Vector3Int> occupied = BuildProductionOccupiedCells();
        int cheapest = int.MaxValue;
        int cheapestAvailable = int.MaxValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                || building.OfferedUnits == null)
                continue;
            Vector3Int productionCell = building.CurrentCellPosition;
            productionCell.z = 0;
            bool buildingAvailable = !occupied.Contains(productionCell);
            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.cost <= 0 || unit.militaryForce == MilitaryForce.Navy)
                    continue;
                if (!IsRolePurchaseAllowed(unit, snapshot.Stance,
                        IsEmergencyShoppingDemand(demand))
                    || !IsEliteChainAvailable(unit, snapshot)
                    || !DoesUnitMeetShoppingDemand(unit, demand))
                    continue;
                if (unit.cost < cheapest)
                    cheapest = unit.cost;
                if (buildingAvailable && unit.cost < cheapestAvailable)
                    cheapestAvailable = unit.cost;
            }
        }
        if (cheapestAvailable != int.MaxValue)
        {
            availableNow = true;
            return cheapestAvailable;
        }
        return cheapest == int.MaxValue ? 0 : cheapest;
    }

#if UNITY_EDITOR
    // Acesso somente-editor para a janela Tools > Utils > Shopping Pressure.
    // Reconstrói a fila de demandas exatamente como o shopping a veria agora.
    public static List<AIShoppingDemand> InspectRoleDemands(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || AITacticalAnalyzer.Instance == null)
            return new List<AIShoppingDemand>();
        return BuildRoleShoppingDemands(snapshot, log: false);
    }

    // Match real (mesma regra do shopping) para a janela montar o catálogo "à venda x demanda".
    public static bool UnitMeetsDemandForInspection(UnitData unit, AIShoppingDemand demand)
        => DoesUnitMeetShoppingDemand(unit, demand);

    public static bool InspectPurchaseEligibility(
        AIWorldSnapshot snapshot,
        UnitData unit,
        List<AIShoppingDemand> demands,
        out string reason)
    {
        reason = "elegível";
        if (snapshot == null || unit == null)
        {
            reason = "dados indisponíveis";
            return false;
        }
        if (unit.militaryForce == MilitaryForce.Navy)
        {
            reason = "Marinha excluída deste shopping";
            return false;
        }
        if (unit.cost > snapshot.Budget)
        {
            reason = $"orçamento ${snapshot.Budget} < ${unit.cost}";
            return false;
        }

        if (!IsEliteChainAvailable(unit, snapshot))
        {
            string prerequisite = unit.eliteFrom != null ? unit.eliteFrom.displayName : "elite anterior";
            reason = $"cadeia elite: falta {prerequisite}";
            return false;
        }

        if (demands != null)
            foreach (AIShoppingDemand demand in demands)
                if (demand != null && demand.Count > 0
                    && DoesUnitMeetShoppingDemand(unit, demand))
                {
                    if (IsRolePurchaseAllowed(unit, snapshot.Stance,
                            IsEmergencyShoppingDemand(demand)))
                        return true;
                    reason = $"postura {snapshot.Stance} bloqueia {unit.aiPurchaseMode} para {demand.Origin}";
                }

        reason = $"sem demanda {UnitRoleCompatibility.ResolveCompositionRole(unit)}";
        return false;
    }
#endif

    private static List<AIShoppingDemand> BuildRoleShoppingDemands(AIWorldSnapshot snapshot, bool log = true)
    {
        // Faccao sem QG (rebelde): doutrina de insurgencia. Sem pacote de composicao 2/2/1, sem
        // elite, sem ar/intel/transporte/counter — o rebelde so sabe CAPTURAR, entao gasta o caixa
        // em MAIS capturadores. Nao pode reusar a formula de composicao: ela e razao (packages*2 -
        // capturers), que zera assim que ha capturador demais — exatamente o que trava o rebelde.
        // A demanda aqui e generosa e fixa; a cadencia real vem do produtor renegado (~1 compra/turno).
        // Ver AIController.Rebel / project_faccao_sem_qg.
        if (ConstructionManager.IsHeadQuarterlessTeam(snapshot.AITeam))
        {
            var rebelDemands = new List<AIShoppingDemand>();
            EnsureRoleDemand(rebelDemands, UnitRole.Capturador, 6, 30,
                "rebel-insurgency", "doutrina rebelde: so capturador (sem composicao/elite/ar)");
            if (log)
                Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                    + "doutrina rebelde: demanda so Capturador — sem pacote 2/2/1 / elite / ar");
            return rebelDemands;
        }

        var demands = new List<AIShoppingDemand>();
        AIRosterKnowledge roster = BuildRosterKnowledge(snapshot, log);
        CounterPressureInspection counterPressure = BuildCounterPressure(snapshot, roster);
        List<TacticalDeficit> deficits = AITacticalAnalyzer.Instance.GetDeficits(
            PlayerSlotId.FromIndex(snapshot.AISlotIndex),
            log);
        SectorObjective rallyFireSupportFocus = FindRallyShoppingFocus(deficits);
        foreach (TacticalDeficit deficit in deficits)
        {
            bool rallyFireSupport = IsRallyFireSupportDeficit(deficit);
            bool rallyBreakthrough = IsRallyBreakthroughDeficit(deficit);
            if (rallyFireSupport
                && deficit.Operation.LinkedObjective != rallyFireSupportFocus)
                continue;
            if (deficit.Count <= 0 || !TryMapNeedToShoppingDemand(deficit, out AIShoppingDemand demand))
                continue;
            if (rallyFireSupport)
            {
                demand.Priority = 10;
                demand.Origin = "rally-assembly";
                demand.MinRallyArtilleryWeight = 1f;
                demand.Reason = $"GoGreen {rallyFireSupportFocus.Sector}: "
                    + $"fogo indireto pesado faltando x{deficit.Count}";
                demand.Urgent = false;
            }
            else if (rallyBreakthrough)
            {
                demand.Priority = 9;
                demand.Origin = "rally-assembly";
                demand.RequireRallyBreakthrough = true;
                demand.Domain = Domain.Land;
                demand.Reason = $"GoGreen {deficit.Operation.LinkedObjective.Sector}: "
                    + $"ruptura blindada faltando x{deficit.Count}";
                demand.Urgent = false;
            }
            MergeRoleDemand(demands, demand, addCounts: true);
        }

        int capturers = CountCompositionRole(snapshot, UnitRole.Capturador);
        int assaults = CountCompositionRole(snapshot, UnitRole.Assalto);
        int fireSupport = CountCompositionRole(snapshot, UnitRole.FogoIndireto);
        int core = capturers + assaults + fireSupport;
        int packages = Mathf.Max(1, Mathf.CeilToInt(core / 5f));
        EnsureRoleDemand(demands, UnitRole.Capturador, Mathf.Max(0, packages * 2 - capturers), 30,
            "composition", $"pacote 2/2/1 cap={capturers} ass={assaults} art={fireSupport}");
        EnsureRoleDemand(demands, UnitRole.Assalto, Mathf.Max(0, packages * 2 - assaults), 31,
            "composition", $"pacote 2/2/1 cap={capturers} ass={assaults} art={fireSupport}");
        // Fire support de composição espera a massa inicial de capturadores se formar (igual ao
        // transporte). Assalto continua liberado. A artilharia defensiva (PreventiveDefense) não
        // passa por aqui e não é gateada.
        int massGate = Instance != null ? Instance.MinCapturerMassForSupport : 4;
        // Composicao define o espaco doutrinario, mas nao pode gastar o produtor/caixa com
        // qualquer artilharia enquanto existe um counter terrestre material em aberto.
        // A demanda de counter abaixo escolhe a peca pelo matchup: foguetes se couberem;
        // metralhadora ou outro counter validado pela matriz como resposta acessivel. Quando a pressao baixar, o pacote
        // 2/2/1 volta a completar seu fire support normalmente.
        const float materialCounterGap = 0.4f;
        bool counterResponseOwnsFireSlot = Mathf.Max(
            counterPressure.AntiInfantry, counterPressure.AntiTank) >= materialCounterGap;
        int compositionFire = capturers >= massGate && !counterResponseOwnsFireSlot
            ? Mathf.Max(0, packages - fireSupport)
            : 0;
        EnsureRoleDemand(demands, UnitRole.FogoIndireto, compositionFire, 32,
            "composition", $"pacote 2/2/1 cap={capturers} ass={assaults} art={fireSupport}");
        if (log && counterResponseOwnsFireSlot && capturers >= massGate
            && packages > fireSupport)
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + $"composition fire_support cedido ao counter: "
                + $"antiInf={counterPressure.AntiInfantry:F2} "
                + $"antiTank={counterPressure.AntiTank:F2}");

        int visibleAir = CountEnemyDomain(snapshot, Domain.Air);
        if (visibleAir > 0)
        {
            int activeAA = CountAtomicRole(snapshot, UnitRole.AntiaereoCombatente)
                + CountAtomicRole(snapshot, UnitRole.Antiaereo);
            int desiredAA = Mathf.Max(1, Mathf.CeilToInt(visibleAir / 2f));
            if (activeAA < desiredAA)
            {
                UnitRole exact = CountAtomicRole(snapshot, UnitRole.AntiaereoCombatente)
                    <= CountAtomicRole(snapshot, UnitRole.Antiaereo)
                    ? UnitRole.AntiaereoCombatente : UnitRole.Antiaereo;
                MergeRoleDemand(demands, NewRoleDemand(UnitRole.Antiaereo, desiredAA - activeAA, 8,
                    "threat", $"aeronaves visiveis={visibleAir}", true, exact), false);
            }
        }

        int enemyArtillery = CountEnemyCombatRole(snapshot, UnitRole.FogoIndireto);
        if (enemyArtillery >= 3 && CountOwnedAirAttack(snapshot, eliteOnly: true) == 0)
        {
            AIShoppingDemand bomber = NewRoleDemand(UnitRole.AtaqueAereo, 1, 12,
                "breakthrough", $"parede de artilharia={enemyArtillery}", false);
            bomber.Domain = Domain.Air;
            bomber.MinEliteLevel = 1;
            MergeRoleDemand(demands, bomber, false);
        }

        if (HasEnemyAirProduction(snapshot) && CountCompositionRole(snapshot, UnitRole.Intel) == 0)
        {
            AIShoppingDemand intel = NewRoleDemand(UnitRole.Intel, 1, 20,
                "intel", "oponente possui capacidade aeroportuaria", false);
            MergeRoleDemand(demands, intel, false);
        }

        if (HasEnemySubmarineCapability(snapshot) && CountCompositionRole(snapshot, UnitRole.RaidAntiSub) == 0)
        {
            MergeRoleDemand(demands, NewRoleDemand(UnitRole.RaidAntiSub, 1, 18,
                "anti-sub", "submarino visivel ou porto inimigo", false), false);
        }

        int repairWork = CountUnitsUnderRepair(snapshot);
        int logistics = CountCompositionRole(snapshot, UnitRole.Logistica);
        int wantedLogistics = repairWork > 0 ? Mathf.Max(1, Mathf.CeilToInt(repairWork / 2f)) : 0;
        if (wantedLogistics > logistics)
        {
            // Prioridade ESCALA com os feridos acumulados, pra logistica nao morrer de fome atras
            // do combate/elite eternamente. Poucos feridos -> fica embaixo (elite ganha, normal);
            // muitos -> sobe na fila (a hemorragia de forca virou urgente). Pacote assalto/fogo=12,
            // capturador=16, logistica base=22.
            int logisticsPriority = repairWork >= 6 ? 10 : repairWork >= 4 ? 14 : 22;
            EnsureRoleDemand(demands, UnitRole.Logistica, wantedLogistics - logistics, logisticsPriority,
                "service", $"unidades em reparo={repairWork}");
        }

        AddEliteProgressionDemand(snapshot, demands, UnitRole.Assalto, assaults, 24);
        AddEliteProgressionDemand(snapshot, demands, UnitRole.FogoIndireto, fireSupport, 25);
        AddCounterPressureDemands(snapshot, demands, counterPressure, roster);
        bool groundCounterPressureCovered = counterPressure.AntiTank <= 0.05f
            && counterPressure.AntiInfantry <= 0.05f;
        bool rallyAssemblyActive = HasActiveRallyAssembly(
            PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        AddEliteQualityDemand(snapshot, demands, UnitRole.Assalto, assaults, 26,
            groundCounterPressureCovered, rallyAssemblyActive);
        AddEliteQualityDemand(snapshot, demands, UnitRole.FogoIndireto, fireSupport, 27,
            groundCounterPressureCovered, rallyAssemblyActive);
        AddOperationalPressureDemands(snapshot, demands, BuildOperationalPressure(snapshot));

        ApplyHardModeArmorFirst(snapshot, demands);

        demands.Sort((a, b) =>
        {
            int urgent = b.Urgent.CompareTo(a.Urgent);
            if (urgent != 0) return urgent;
            int priority = a.Priority.CompareTo(b.Priority);
            if (priority != 0) return priority;
            return ((int)a.Role).CompareTo((int)b.Role);
        });
        return demands;
    }

    // Hard/Blitz: enquanto não há NENHUM blindado de assalto elite em campo, o primeiro elite
    // terrestre deve ser a peça de ruptura (MBT), não o Obus Médio. Rebaixa a artilharia de
    // composição/operação para logo abaixo do assalto pendente, de modo que o carrinho compre o
    // blindado primeiro quando o caixa só dá pra um — mas ainda compre a artilharia se o MBT for
    // inviável no turno (o beam só compara carrinhos realmente pagáveis). Preserva fogo pesado de
    // rally (GoGreen), antiaéreo (SAM) e demandas urgentes — que continuam furando a regra.
    // Espelha a intenção da regra legada em AIShoppingPlanner.cs (path não-role-based, desativado).
    // Gordura de manutenção do bootstrap Blitz (Serviço do Comando): reservada ao poupar pro 1º MBT
    // e descontada da renda projetada. Dedicada (não usa EliteMaintenanceReservePercent da reserva
    // madura) — 10% aqui é menos conservador, compra o MBT mais cedo. Dial de ajuste.
    private const float BlitzArmorUpkeepReservePercent = 0.10f;

    private static void ApplyHardModeArmorFirst(AIWorldSnapshot snapshot, List<AIShoppingDemand> demands)
    {
        if (snapshot == null || demands == null || demands.Count == 0)
            return;
        if (AIController.Instance == null || !AIController.Instance.HardMode)
            return;
        if (CountActiveEliteAssaultUnits(snapshot) > 0)
            return;

        int bestAssaultPriority = int.MaxValue;
        foreach (AIShoppingDemand d in demands)
        {
            if (d == null || d.Count <= 0 || d.Role != UnitRole.Assalto)
                continue;
            if (d.Domain == Domain.Air) // AAA/ar não é a peça de ruptura terrestre
                continue;
            if (d.Priority < bestAssaultPriority)
                bestAssaultPriority = d.Priority;
        }
        if (bestAssaultPriority == int.MaxValue) // sem assalto pendente: nada a proteger
            return;

        int deferPriority = bestAssaultPriority + 1;
        foreach (AIShoppingDemand d in demands)
        {
            if (d == null || d.Count <= 0 || d.Role != UnitRole.FogoIndireto)
                continue;
            if (d.Urgent) // urgências (ex.: ameaça imediata) furam a regra
                continue;
            if (d.Domain == Domain.Air) // SAM / fogo antiaéreo preservado
                continue;
            if (d.Origin == "rally-assembly" || d.RequireRallyBreakthrough
                || d.MinRallyArtilleryWeight > 0f) // fogo pesado de GoGreen preservado
                continue;
            if (d.Priority >= deferPriority) // já está atrás do assalto
                continue;

            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] hard_blitz: "
                + $"adiando artilharia {FormatDemandCapability(d)} pri {d.Priority}->{deferPriority} "
                + $"(primeiro elite terrestre = MBT, origem={d.Origin})");
            d.Priority = deferPriority;
        }
    }

    private static SectorObjective FindRallyShoppingFocus(List<TacticalDeficit> deficits)
    {
        SectorObjective best = null;
        float bestScore = float.MinValue;
        if (deficits == null)
            return null;

        foreach (TacticalDeficit deficit in deficits)
        {
            if (!IsRallyFireSupportDeficit(deficit) || deficit.Count <= 0)
                continue;
            SectorObjective obj = deficit.Operation.LinkedObjective;
            int stateScore = obj.RallyState == AIRallyAssemblyState.Ready ? 3
                : obj.RallyState == AIRallyAssemblyState.Assembling ? 2 : 1;
            float score = stateScore * 1000f - obj.Priority * 10f;
            if (score > bestScore)
            {
                bestScore = score;
                best = obj;
            }
        }
        return best;
    }

    private static bool IsRallyFireSupportDeficit(TacticalDeficit deficit)
    {
        return deficit.Operation != null
            && deficit.Operation.LinkedObjective != null
            && deficit.Operation.LinkedObjective.ObjectiveType == AIObjectiveType.RallyAssembly
            && (deficit.Kind == AINeedKind.FireSupport
                || deficit.Kind == AINeedKind.Artillery);
    }

    private static bool IsRallyBreakthroughDeficit(TacticalDeficit deficit)
    {
        return deficit.Operation != null
            && deficit.Operation.LinkedObjective != null
            && deficit.Operation.LinkedObjective.ObjectiveType == AIObjectiveType.RallyAssembly
            && deficit.Kind == AINeedKind.Assault;
    }

    private static AIShoppingDemand NewRoleDemand(UnitRole role, int count, int priority,
        string origin, string reason, bool urgent, UnitRole exactRole = UnitRole.None)
    {
        return new AIShoppingDemand
        {
            Role = role,
            ExactRole = exactRole,
            Count = count,
            Priority = priority,
            Origin = origin,
            Reason = reason,
            Urgent = urgent,
        };
    }

    private static bool TryMapNeedToShoppingDemand(TacticalDeficit deficit, out AIShoppingDemand demand)
    {
        demand = null;
        int priority = deficit.Operation != null ? Mathf.Max(1, deficit.Operation.Priority) : 40;
        bool urgent = deficit.Operation != null && deficit.Operation.IsUrgent;
        string reason = deficit.Operation != null
            ? $"{deficit.Operation.Type} {deficit.Operation.Sector}" : deficit.Kind.ToString();
        UnitRole role;
        UnitRole exact = UnitRole.None;
        Domain? domain = null;
        int minElite = 0;
        int maxElite = int.MaxValue;
        switch (deficit.Kind)
        {
            case AINeedKind.Capturer: role = UnitRole.Capturador; break;
            case AINeedKind.Assault: role = UnitRole.Assalto; break;
            case AINeedKind.FireSupport:
            case AINeedKind.Artillery: role = UnitRole.FogoIndireto; break;
            case AINeedKind.AAA: role = UnitRole.Antiaereo; exact = UnitRole.AntiaereoCombatente; break;
            case AINeedKind.SAM: role = UnitRole.Antiaereo; exact = UnitRole.Antiaereo; break;
            case AINeedKind.GroundTransport: role = UnitRole.Transportador; domain = Domain.Land; break;
            case AINeedKind.AirTransport: role = UnitRole.Transportador; domain = Domain.Air; break;
            case AINeedKind.FighterB: role = UnitRole.Interceptador; domain = Domain.Air; maxElite = 0; break;
            case AINeedKind.FighterA: role = UnitRole.Interceptador; domain = Domain.Air; minElite = 1; break;
            case AINeedKind.Apache: role = UnitRole.AtaqueAereo; domain = Domain.Air; maxElite = 0; break;
            case AINeedKind.AirTanker: role = UnitRole.Logistica; domain = Domain.Air; break;
            default: return false;
        }

        demand = NewRoleDemand(role, deficit.Count, urgent ? priority : priority + 10,
            "operation", reason, urgent, exact);
        demand.Domain = domain;
        demand.MinEliteLevel = minElite;
        demand.MaxEliteLevel = maxElite;
        return true;
    }

    private static void EnsureRoleDemand(List<AIShoppingDemand> demands, UnitRole role,
        int count, int priority, string origin, string reason)
    {
        if (count <= 0) return;
        MergeRoleDemand(demands, NewRoleDemand(role, count, priority, origin, reason, false), false);
    }

    private static void MergeRoleDemand(List<AIShoppingDemand> demands, AIShoppingDemand incoming, bool addCounts)
    {
        if (incoming == null || incoming.Count <= 0) return;
        foreach (AIShoppingDemand current in demands)
        {
            bool currentRally = current.Origin != null
                && current.Origin.Contains("rally-assembly");
            bool incomingRally = incoming.Origin != null
                && incoming.Origin.Contains("rally-assembly");
            if (currentRally != incomingRally)
                continue;
            if (current.Role != incoming.Role || current.ExactRole != incoming.ExactRole
                || current.Domain != incoming.Domain || current.MinEliteLevel != incoming.MinEliteLevel
                || current.MaxEliteLevel != incoming.MaxEliteLevel
                || !Mathf.Approximately(current.MinRallyArtilleryWeight,
                    incoming.MinRallyArtilleryWeight)
                || current.RequireRallyBreakthrough != incoming.RequireRallyBreakthrough
                || current.RequiredWeaponCategory != incoming.RequiredWeaponCategory
                || current.TargetClass != incoming.TargetClass
                || current.MinTargetPriority != incoming.MinTargetPriority
                || !string.Equals(current.RequiredUnitId, incoming.RequiredUnitId,
                    System.StringComparison.Ordinal))
                continue;
            current.Count = addCounts ? current.Count + incoming.Count : Mathf.Max(current.Count, incoming.Count);
            current.Priority = Mathf.Min(current.Priority, incoming.Priority);
            current.Urgent |= incoming.Urgent;
            current.StrategicEscalation |= incoming.StrategicEscalation;
            if (!current.Origin.Contains(incoming.Origin)) current.Origin += "+" + incoming.Origin;
            if (!current.Reason.Contains(incoming.Reason)) current.Reason += "; " + incoming.Reason;
            return;
        }
        demands.Add(incoming);
    }

    private static bool DoesUnitMeetShoppingDemand(UnitData unit, AIShoppingDemand demand)
    {
        if (unit == null || demand == null || unit.roles == null || unit.roles.Count == 0)
            return false;
        if (!string.IsNullOrEmpty(demand.RequiredUnitId)
            && !string.Equals(unit.id, demand.RequiredUnitId,
                System.StringComparison.Ordinal))
            return false;
        if (demand.Domain.HasValue && unit.domain != demand.Domain.Value)
            return false;
        if (unit.eliteLevel < demand.MinEliteLevel || unit.eliteLevel > demand.MaxEliteLevel)
            return false;
        if (demand.ExactRole != UnitRole.None && unit.roles[0] != demand.ExactRole)
            return false;
        if (demand.RequiredWeaponCategory.HasValue
            && !HasWeaponCategory(unit, demand.RequiredWeaponCategory.Value))
            return false;
        if (demand.MinRallyArtilleryWeight > 0f
            && GetShoppingRallyArtilleryWeight(unit) < demand.MinRallyArtilleryWeight)
            return false;
        if (demand.RequireRallyBreakthrough
            && !IsShoppingRallyBreakthroughUnit(unit))
            return false;
        if (demand.TargetClass.HasValue
            && (int)unit.ResolveAiTargetPriorityForTargetClass(demand.TargetClass.Value)
                < (int)demand.MinTargetPriority)
            return false;
        if (demand.Role == UnitRole.None)
            return true;

        switch (demand.Role)
        {
            case UnitRole.Capturador:
            case UnitRole.Assalto:
            case UnitRole.FogoIndireto:
                return UnitRoleCompatibility.ResolveCompositionRole(unit) == demand.Role;
            case UnitRole.Transportador:
                return UnitRoleCompatibility.IsOperationalTransporter(unit);
            default:
                return UnitRoleCompatibility.CanSatisfy(unit, demand.Role);
        }
    }

    private static string FormatDemandCapability(AIShoppingDemand demand)
    {
        if (demand == null)
            return "demanda";
        string label = demand.Role == UnitRole.None
            && demand.RequiredWeaponCategory.HasValue
                ? demand.TargetClass.HasValue
                    ? $"Counter/{demand.TargetClass.Value}"
                    : "Counter/desconhecido"
                : $"{demand.Role}/{demand.ExactRole}";
        if (demand.RequiredWeaponCategory.HasValue)
            label += $"+{demand.RequiredWeaponCategory.Value}";
        if (demand.MinRallyArtilleryWeight > 0f)
            label += $"+rallyArt>={demand.MinRallyArtilleryWeight:0.#}";
        if (demand.TargetClass.HasValue)
            label += $"→{demand.TargetClass.Value}";
        if (demand.RequireRallyBreakthrough)
            label += "+rallyBreak";
        if (!string.IsNullOrEmpty(demand.RequiredUnitId))
            label += $"[{demand.RequiredUnitId}]";
        return label;
    }

    private static int ScoreRoleShoppingCandidate(AIWorldSnapshot snapshot, UnitData unit,
        AIShoppingDemand demand, int remaining, CounterPressureInspection counterPressure,
        bool macroLosing = false, bool concentrate = false)
    {
        int score = 200000 - demand.Priority * 3000;
        if (demand.Urgent) score += 250000;
        if (demand.TargetClass.HasValue)
            score += (int)unit.ResolveAiTargetPriorityForTargetClass(demand.TargetClass.Value) * 18000;
        float counterFit = ScoreCounterFitForDemand(
            unit, counterPressure, demand.RequiredWeaponCategory, demand.TargetClass);
        score += Mathf.RoundToInt(counterFit * 18000f);
        if (demand.MinRallyArtilleryWeight > 0f)
            score += Mathf.RoundToInt(GetShoppingRallyArtilleryWeight(unit) * 35000f);
        if (demand.RequireRallyBreakthrough)
            score += IsShoppingRallyBreakthroughUnit(unit) ? 90000 : -300000;

        bool eliteReady = IsEliteEconomyReady(snapshot, unit, remaining, concentrate);
        bool eliteBudgetRole = demand.Role == UnitRole.Assalto
            || demand.Role == UnitRole.FogoIndireto || demand.Role == UnitRole.AtaqueAereo;

        // GATE DE NÚCLEO SUAVE (opcional). O gate duro faz DUAS coisas de uma vez quando a
        // composição não fechou: bane o elite (-120000) E desliga o nudge anti-barato (-25000).
        // Resultado perverso: numa demanda de fogo indireto o obus fraco vence por WO — não por
        // mérito, mas porque os concorrentes foram removidos. E comprá-lo é justamente o que fecha
        // o núcleo, ou seja, a IA paga o pedágio com lixo para abrir a própria cancela.
        // No modo suave a maturidade (0..1) vira PESO contínuo: com o núcleo quase pronto o elite
        // tem desvantagem modesta em vez de banimento, e a IA escolhe se compensa.
        // O piso de CAIXA continua duro — isso é poder de compra, não doutrina.
        bool softGate = AIController.Instance != null
            && AIController.Instance.SoftCoreGate
            && !concentrate
            && HasEliteCashFloor(snapshot, unit, remaining);

        if (softGate)
        {
            float maturity = ComputeOperationalCoreMaturity(snapshot);
            if (unit.eliteLevel > 0)
                score += Mathf.RoundToInt(
                    Mathf.Lerp(-120000f, 65000f + unit.eliteLevel * 18000f, maturity));
            else if (eliteBudgetRole)
                score -= Mathf.RoundToInt(25000f * maturity);
        }
        else if (unit.eliteLevel > 0)
            score += eliteReady ? 65000 + unit.eliteLevel * 18000 : -120000;
        else if (eliteReady && eliteBudgetRole)
            score -= 25000;

        // Demanda de CAPTURA pura: o capturador dedicado (roles[0]==Capturador, ex.: Soldado) tem que
        // ganhar do capturador AGRESSIVO (CapturadorAgressivo, ex.: Machine Gunner), que só captura a
        // 50% e é quebra-galho. Sem isso, CanSatisfy/ResolveCompositionRole achatam os dois em
        // "Capturador" e o viés de custo (cost/2) escolhe o agressivo por ser mais caro — torrando o
        // caixa da expansão em unidades de combate. A penalidade domina qualquer diferença de custo,
        // mas como coverage vem antes de QualityScore no desempate do carrinho, o agressivo ainda entra
        // como FALLBACK quando nenhum capturador dedicado está ofertado pro slot. Demandas de combate
        // (Assalto/anti-infantaria) seguem inalteradas: lá o agressivo é um corpo de combate legítimo.
        if (demand.Role == UnitRole.Capturador && IsPrimaryRole(unit, UnitRole.CapturadorAgressivo))
            score -= DedicatedCapturerPreferencePenalty;

        // Perdendo (Collapsing) conta como emergência defensiva: unidade de modo Defensivo ganha o
        // bônus pesado mesmo que a stance "oficial" ainda esteja Tactical — é o que faz a artilharia
        // anti-blindado (obus/campanha) ganhar do anti-infantaria quando há tanque no QG.
        if ((snapshot.Stance == AIStance.Defensive || macroLosing) && unit.aiPurchaseMode == AIPurchaseMode.Defensive)
            score += 24000;
        if (snapshot.Stance != AIStance.Defensive && unit.aiPurchaseMode == AIPurchaseMode.Offensive)
            score += 12000;
        // Concentrando (poucos slots): NÃO limita o bônus de custo — a peça mais cara/forte que cabe
        // é preferida, pra usar o caixa que de outro modo ficaria parado.
        score += concentrate ? unit.cost / 2 : Mathf.Min(25000, unit.cost / 2);

        // Time PERDENDO o mapa (Collapsing): empurra combate pra cima — segurar o que resta vem antes
        // de expandir. Defensores (assalto/fogo/AA) ganham peso; capturador (expansao) NAO. Sem RNG.
        if (macroLosing
            && (demand.Role == UnitRole.Assalto || demand.Role == UnitRole.FogoIndireto
                || demand.Role == UnitRole.Antiaereo))
            score += 16000;

        return score;
    }

    private static float GetShoppingRallyArtilleryWeight(UnitData data)
    {
        if (data == null)
            return 0f;
        if ((data.roles == null || !data.roles.Contains(UnitRole.FogoIndireto))
            && data.unitClass != GameUnitClass.Artillery)
            return 0f;

        string key = $"{data.id} {data.displayName} {data.apelido}".ToLowerInvariant();
        if (key.Contains("obus") && key.Contains("leve"))
            return 0.5f;
        if (key.Contains("art") && key.Contains("campanha"))
            return 1.5f;
        if (key.Contains("astros")
            || (key.Contains("obus") && (key.Contains("medio")
                || key.Contains("médio") || key.Contains("mÃ©dio"))))
            return 1f;

        return 0.5f;
    }

    // Preenche prédios de produção ociosos (não usados nesta passada, podem produzir, célula livre)
    // com o defensor mais barato disponível — nega captura oportunista de "casa vazia" quando perdendo
    // ou defendendo. Respeita orçamento e o filtro de stance.
    private static bool IsShoppingRallyBreakthroughUnit(UnitData data)
    {
        return data != null
            && data.domain == Domain.Land
            && data.unitClass == GameUnitClass.Armored
            && UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Assalto
            && (data.roles == null || !data.roles.Contains(UnitRole.FogoIndireto));
    }

    // RECRUTAMENTO FORÇADO (Hard, Perdendo): enche cada produtor livre com o corpo mais BARATO (massa)
    // — no Hard o básico de assalto/artilharia é banido, então cai no capturador/soldado barato. O
    // elite COMPROMETIDO é comprado só no ÚLTIMO produtor, e apenas se ainda couber no caixa depois de
    // encher os outros com massa (o "no último, se sobrar"). O que não for gasto fica de reserva. O
    // compromisso persiste se o elite não couber (compra num turno que dê pra pagar massa + elite).
    private static void RecruitmentSurgeFill(
        AIWorldSnapshot snapshot, List<ShoppingOrder> orders,
        HashSet<ConstructionManager> usedBuildings, HashSet<Vector3Int> occupied,
        ref int remaining, List<AIShoppingDemand> demands, AIElitePurchaseCommitment eliteCommitment)
    {
        if (snapshot?.MyBuildings == null)
            return;

        var freeProducers = new List<ConstructionManager>();
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null || usedBuildings.Contains(b)
                || !b.CanProduceUnitsForSlot(snapshot.AISlotIndex) || b.OfferedUnits == null)
                continue;
            Vector3Int cell = b.CurrentCellPosition; cell.z = 0;
            if (occupied.Contains(cell))
                continue;
            freeProducers.Add(b);
        }
        if (freeProducers.Count == 0)
            return;

        // Elite QUERIDO e o produtor que o constrói: o compromisso (obus reativo) OU o mais valioso
        // elite que atende uma demanda pendente (ex.: o MBT/dream, mesmo vindo da reserva blitz). Esse
        // produtor fica RESERVADO pro fim; os outros enchem de massa; e o elite só entra se sobrar.
        UnitData eliteUnit = null;
        ConstructionManager eliteProducer = null;
        bool eliteCommitted = false;
        foreach (ConstructionManager p in freeProducers)
        {
            foreach (UnitData u in p.OfferedUnits)
            {
                if (u == null || u.eliteLevel < 1 || u.cost <= 0 || u.militaryForce == MilitaryForce.Navy)
                    continue;
                if (!IsRolePurchaseAllowed(u, snapshot.Stance, emergency: true) || !IsEliteChainAvailable(u, snapshot))
                    continue;
                bool committed = eliteCommitment != null && !string.IsNullOrEmpty(eliteCommitment.unitId)
                    && string.Equals(u.id, eliteCommitment.unitId, System.StringComparison.Ordinal);
                if (!committed && !DemandWantsUnit(demands, u))
                    continue;
                // Prefere o comprometido; senão o mais caro (o "dream", ex.: MBT sobre obus).
                if (eliteUnit == null
                    || (committed && !eliteCommitted)
                    || (committed == eliteCommitted && u.cost > eliteUnit.cost))
                {
                    eliteUnit = u; eliteProducer = p; eliteCommitted = committed;
                }
            }
        }

        // Massa nos produtores (menos o reservado pro elite).
        foreach (ConstructionManager building in freeProducers)
        {
            if (eliteUnit != null && building == eliteProducer)
                continue;
            BuySurgeMassBody(snapshot, orders, usedBuildings, occupied, ref remaining, building);
        }

        // Por último, o produtor reservado: se sobrou pro elite, compra; senão, mais massa.
        if (eliteProducer != null)
        {
            if (eliteUnit != null && eliteUnit.cost <= remaining)
            {
                Vector3Int ec = eliteProducer.CurrentCellPosition; ec.z = 0;
                int idxE = IndexOf(eliteProducer.OfferedUnits, eliteUnit);
                orders.Add(new ShoppingOrder { Building = eliteProducer, UnitToBuy = eliteUnit, SelectedIndex = idxE });
                remaining -= eliteUnit.cost;
                usedBuildings.Add(eliteProducer);
                occupied.Add(ec);
                if (eliteCommitted)
                    AIIntelLedger.ClearElitePurchaseCommitment(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
                Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] recrutamento forçado: "
                    + $"último produtor {eliteProducer.ConstructionDisplayName} compra elite {eliteUnit.displayName} "
                    + $"${eliteUnit.cost} (sobrou) restante={remaining}");
            }
            else
            {
                BuySurgeMassBody(snapshot, orders, usedBuildings, occupied, ref remaining, eliteProducer);
            }
        }
    }

    // Compra o corpo mais BARATO (massa) num produtor livre — igual ao filler de prédio vazio.
    private static void BuySurgeMassBody(
        AIWorldSnapshot snapshot, List<ShoppingOrder> orders,
        HashSet<ConstructionManager> usedBuildings, HashSet<Vector3Int> occupied,
        ref int remaining, ConstructionManager building)
    {
        UnitData pick = null;
        foreach (UnitData u in building.OfferedUnits)
        {
            // Massa do recrutamento forçado e SEMPRE do Exercito ("onde tiver
            // soldado"): aeroporto/porto nao viram fabrica de massa cara — o
            // elite comprometido (que pode ser aereo) continua no fluxo proprio.
            if (u == null || u.cost <= 0 || u.cost > remaining
                || u.militaryForce != MilitaryForce.Army
                || !IsRolePurchaseAllowed(u, snapshot.Stance, emergency: true))
                continue;
            if (pick == null || IsBetterEmptyBuildingFiller(u, pick))
                pick = u;
        }
        if (pick == null)
            return;

        Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
        int idx = IndexOf(building.OfferedUnits, pick);
        orders.Add(new ShoppingOrder { Building = building, UnitToBuy = pick, SelectedIndex = idx });
        remaining -= pick.cost;
        usedBuildings.Add(building);
        occupied.Add(cell);
        Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] recrutamento forçado: "
            + $"{building.ConstructionDisplayName} massa {pick.displayName} ${pick.cost} restante={remaining}");
    }

    // Verdadeiro se alguma demanda pendente (Count>0) é atendida por esta unidade.
    private static bool DemandWantsUnit(List<AIShoppingDemand> demands, UnitData unit)
    {
        if (demands == null || unit == null)
            return false;
        foreach (AIShoppingDemand d in demands)
            if (d != null && d.Count > 0 && DoesUnitMeetShoppingDemand(unit, d))
                return true;
        return false;
    }

    // FASE DE MASSACRE (válvula da doutrina): jogadores cessam o recrutamento forçado quando
    // têm clara vantagem numérica (~2:1) ou estão perto do teto de unidades, e convertem o
    // caixa em elite pra fechar o jogo de uma vez. Histerese entre entrar (>= enter) e sair
    // (< exit) evita alternar soldado/elite com o ratio oscilando na fronteira. Fog-honesto
    // por construção: o ForceRatio macro usa inimigos CONHECIDOS (+ projeção de produtores no
    // Hard) — a AI pode "achar" que domina enquanto o inimigo esconde exército, o mesmo erro
    // que um humano cometeria com a mesma informação. O gate exige EnemyForce > 0: sem nenhum
    // contato/projeção o ratio degenera pra 100% e desligaria a doutrina no turno 1.
    private static bool ResolveMassacrePhase(AIWorldSnapshot snapshot, AIController ai)
    {
        AIController.MacroTerritoryInspection macro =
            AIController.GetMacroTerritoryForInspection(snapshot.AITeam);
        bool wasActive = ai.MassacrePhaseActive;
        bool byRatio = macro.EnemyForce > 0 && macro.ForceRatio
            >= (wasActive ? ai.MassacreExitForceRatio : ai.MassacreEnterForceRatio);
        int unitCap = ai.Match != null ? ai.Match.MaxUnitsPerTeam : 0;
        bool byCap = unitCap > 0 && snapshot.MyUnits.Count
            >= Mathf.CeilToInt(unitCap * ai.MassacreUnitCapFillRatio);
        bool active = byRatio || byCap;
        ai.MassacrePhaseActive = active;
        if (active != wasActive)
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + (active
                    ? $"FASE DE MASSACRE: conscrição cessa (fr={macro.ForceRatio:P0} "
                        + $"unidades={snapshot.MyUnits.Count}/{unitCap}) — caixa vira elite"
                    : $"fim do massacre (fr={macro.ForceRatio:P0}) — conscrição reativada"));
        return active;
    }

    // Imposto de conscrição (doutrina do enxame, Hard): soma do corpo Army mais barato de
    // cada produtor do exército livre com célula de spawn desocupada. Descontado do
    // orçamento do carrinho de demandas ANTES das compras, garante que o elite só fecha a
    // conta quando cabe por cima da massa — todo produtor do exército compra todo turno.
    // Aeroporto/porto (sem oferta Army) não pagam imposto: ficam quietos poupando.
    private static int ComputeConscriptionTax(
        AIWorldSnapshot snapshot, HashSet<Vector3Int> occupied)
    {
        if (snapshot.MyBuildings == null)
            return 0;
        int tax = 0;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                || building.OfferedUnits == null)
                continue;
            Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
            if (occupied.Contains(cell))
                continue;
            int cheapest = 0;
            foreach (UnitData u in building.OfferedUnits)
            {
                if (u == null || u.cost <= 0 || u.militaryForce != MilitaryForce.Army
                    || !IsRolePurchaseAllowed(u, snapshot.Stance, emergency: true))
                    continue;
                if (cheapest == 0 || u.cost < cheapest)
                    cheapest = u.cost;
            }
            tax += cheapest;
        }
        return tax;
    }

    // armyMassOnly (doutrina do enxame): a massa e SEMPRE do Exercito — produtor
    // que so oferece Aeronautica/Marinha (aeroporto, porto) fica quieto guardando
    // caixa pro elite ("fecha a conta? manda"); base mista compra o soldado. No
    // preenchimento DEFENSIVO (ameaca visivel), a restricao nao vale: ocupar a
    // casa contra captura importa mais que a composicao.
    private static void FillIdleProductionBuildings(
        AIWorldSnapshot snapshot, List<ShoppingOrder> orders,
        HashSet<ConstructionManager> usedBuildings, HashSet<Vector3Int> occupied, ref int remaining,
        int protectedReserve = 0,
        bool armyMassOnly = false,
        CounterPressureInspection counterPressure = null)
    {
        if (snapshot.MyBuildings == null) return;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || usedBuildings.Contains(building)
                || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex) || building.OfferedUnits == null)
                continue;
            Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
            if (occupied.Contains(cell))
                continue;

            UnitData pick = null;
            int spendable = Mathf.Max(0, remaining - protectedReserve);
            foreach (UnitData u in building.OfferedUnits)
            {
                if (u == null || u.cost <= 0 || u.cost > spendable
                    || (armyMassOnly ? u.militaryForce != MilitaryForce.Army : u.militaryForce == MilitaryForce.Navy)
                    || !IsRolePurchaseAllowed(u, snapshot.Stance, emergency: true)
                    || ShouldYieldGenericFireSupportToAntiInfantry(
                        u, demand: null, counterPressure: counterPressure))
                    continue;
                if (pick == null || IsBetterEmptyBuildingFiller(u, pick))
                    pick = u;
            }
            if (pick == null)
                continue;

            int idx = IndexOf(building.OfferedUnits, pick);
            orders.Add(new ShoppingOrder { Building = building, UnitToBuy = pick, SelectedIndex = idx });
            remaining -= pick.cost;
            usedBuildings.Add(building);
            occupied.Add(cell);
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + $"{(armyMassOnly ? "conscrição (enxame)" : "defesa")}: preenche "
                + $"prédio vazio {building.ConstructionDisplayName} com {pick.displayName} ${pick.cost} "
                + $"restante={remaining}");
        }
    }

    private static bool ShouldYieldGenericFireSupportToAntiInfantry(
        UnitData unit,
        AIShoppingDemand demand,
        CounterPressureInspection counterPressure)
    {
        if (unit == null || counterPressure == null
            || counterPressure.AntiInfantry < 0.4f
            || counterPressure.AntiInfantry <= counterPressure.AntiTank)
            return false;

        // Demandas de matchup explicitas ja possuem seu proprio filtro. A regra
        // existe para impedir composicao/progressao/fill de comprarem artilharia
        // antitanque enquanto a lacuna dominante e infantaria.
        if (demand != null && (demand.RequiredWeaponCategory.HasValue
            || demand.Urgent || demand.RequireRallyBreakthrough
            || demand.MinRallyArtilleryWeight > 0f))
            return false;
        if (!UnitRoleCompatibility.CanSatisfy(unit, UnitRole.FogoIndireto))
            return false;

        float antiInfantryFit = ScoreCounterFitForDemand(
            unit, counterPressure, WeaponCategory.AntiInfantaria,
            GameUnitClass.Infantry);
        return antiInfantryFit <= 0.0001f;
    }

    // Melhor defensor pra encher prédio vazio: prefere Assalto/Capturador (corpo de defesa), depois
    // Fogo/AA, depois qualquer; dentro do mesmo rank, o mais barato (massa).
    private static bool IsBetterEmptyBuildingFiller(UnitData candidate, UnitData current)
    {
        int c = EmptyFillerRoleRank(candidate);
        int cur = EmptyFillerRoleRank(current);
        if (c != cur) return c < cur;
        return candidate.cost < current.cost;
    }

    private static int EmptyFillerRoleRank(UnitData u)
    {
        UnitRole r = UnitRoleCompatibility.ResolveCompositionRole(u);
        if (r == UnitRole.Assalto || r == UnitRole.Capturador) return 0;
        if (r == UnitRole.FogoIndireto || r == UnitRole.Antiaereo) return 1;
        return 2;
    }

    // Filtro de compra por stance, com escape de EMERGÊNCIA: quando perdendo/defendendo, libera
    // unidade de QUALQUER modo (ofensivo E defensivo) — sobrevivência primeiro. Sem o escape, a
    // stance Tactical trancava as peças de modo Defensivo (obus/artilharia de campanha anti-blindado)
    // justamente quando mais precisava delas (tanque no QG).
    private static bool IsRolePurchaseAllowed(UnitData unit, AIStance stance, bool emergency)
    {
        // Hard Mode: unidade banida nunca entra na compra da IA — antes até do bypass de emergência.
        if (IsHardModeBannedForAI(unit))
            return false;
        if (emergency)
            return true;
        return IsRolePurchaseAllowedByStance(unit, stance);
    }

    private static bool IsEmergencyShoppingDemand(AIShoppingDemand demand)
    {
        if (demand == null)
            return false;
        if (demand.Urgent || demand.StrategicEscalation
            || demand.RequiredWeaponCategory.HasValue)
            return true;
        return demand.Reason != null
            && demand.Reason.Contains("SectorDefense");
    }

    private static bool IsRolePurchaseAllowedByStance(UnitData unit, AIStance stance)
    {
        if (unit.aiPurchaseMode == AIPurchaseMode.Either) return true;
        if (stance == AIStance.Defensive) return unit.aiPurchaseMode == AIPurchaseMode.Defensive;
        return unit.aiPurchaseMode == AIPurchaseMode.Offensive;
    }

    // Poder de compra puro, separado da composição: o gate suave precisa manter ESTE piso duro
    // (não adianta "escolher" um elite que o caixa não banca) enquanto suaviza só a doutrina.
    private static bool HasEliteCashFloor(AIWorldSnapshot snapshot, UnitData unit, int remaining)
    {
        if (snapshot == null || unit == null)
            return false;
        return remaining >= Mathf.Max(unit.cost, Mathf.Max(1, snapshot.IncomePerTurn));
    }

    private static bool IsEliteEconomyReady(AIWorldSnapshot snapshot, UnitData unit, int remaining, bool concentrate = false)
    {
        if (!HasEliteCashFloor(snapshot, unit, remaining))
            return false;
        // Unidade ELITE: pode liberar abaixo do piso de massa se o caixa banca o elite E os corpos
        // que faltam pra fechar a massa (ver IsEliteArmyFloorOrBudgetReady). Unidade COMUM (usada no
        // nudge anti-cheap, -25000): mantém o piso de massa ESTRITO — não solta o nudge cedo demais.
        return concentrate || HasOperationalCore(snapshot);
    }

    // Elite liberado quando há MASSA (>= piso) OU CAIXA pra bancar o elite + os soldados baratos que
    // ainda faltam pra fechar a massa. Com caixa gordo não é "elite OU tropa": cabe os dois.
    // concentrate=true (poucos slots de produção): libera direto — não dá pra comprar massa mesmo,
    // então o slot único deve levar a peça mais forte (qualidade já que não dá quantidade).
    // Custo do corpo barato (capturador/assalto não-elite mais barato à venda) — base pra estimar
    // quanto o caixa precisa reservar pra fechar a massa junto com o elite. Fallback 4000.
    private static int EstimateCheapBodyCost(AIWorldSnapshot snapshot)
    {
        int cheapest = int.MaxValue;
        if (snapshot.MyBuildings != null)
            foreach (ConstructionManager b in snapshot.MyBuildings)
            {
                if (b == null || b.OfferedUnits == null) continue;
                foreach (UnitData u in b.OfferedUnits)
                {
                    if (u == null || u.cost <= 0 || u.eliteLevel > 0) continue;
                    UnitRole role = UnitRoleCompatibility.ResolveCompositionRole(u);
                    if (role != UnitRole.Capturador && role != UnitRole.Assalto) continue;
                    if (u.cost < cheapest) cheapest = u.cost;
                }
            }
        return cheapest == int.MaxValue ? 4000 : cheapest;
    }

    private static bool IsEliteChainAvailable(UnitData unit, AIWorldSnapshot snapshot)
    {
        if (unit == null || unit.eliteLevel <= 0 || unit.eliteFrom == null) return true;
        // Hard Mode: se o tier-base (eliteFrom) está banido pra compra da IA, o pré-requisito é
        // inobtenível de propósito — libera a cadeia pra a AI comprar o elite direto (senão o ban
        // do básico travaria também o elite, e a AI cairia em counters baratos).
        if (unit.eliteFrom.bannedOnHardMode
            && AIController.Instance != null && AIController.Instance.HardMode)
            return true;
        foreach (UnitManager owned in snapshot.MyUnits)
            if (owned != null && owned.TryGetUnitData(out UnitData data) && data == unit.eliteFrom)
                return true;
        return false;
    }

    private static void AddEliteProgressionDemand(AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands, UnitRole role, int activeRoleCount, int priority)
    {
        if (activeRoleCount <= 0)
            return;
        int currentElite = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && unit.TryGetUnitData(out UnitData data)
                && UnitRoleCompatibility.ResolveCompositionRole(data) == role)
                currentElite = Mathf.Max(currentElite, data.eliteLevel);

        int nextElite = int.MaxValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || building.OfferedUnits == null) continue;
            foreach (UnitData offered in building.OfferedUnits)
            {
                if (offered == null || offered.eliteLevel <= currentElite
                    || UnitRoleCompatibility.ResolveCompositionRole(offered) != role
                    || !IsEliteChainAvailable(offered, snapshot))
                    continue;
                nextElite = Mathf.Min(nextElite, offered.eliteLevel);
            }
        }
        if (nextElite == int.MaxValue)
            return;

        AIShoppingDemand elite = NewRoleDemand(role, 1, priority, "elite",
            $"massa={snapshot.MyUnits.Count} caixa={snapshot.Budget} nivel={currentElite}->{nextElite}", false);
        elite.MinEliteLevel = nextElite;
        elite.MaxEliteLevel = nextElite;

        // Só persegue o elite se ele cabe agora ou é alcançável dentro do horizonte de poupança.
        // (Substitui o antigo piso fixo de caixa, que com renda baixa nunca deixava o alvo aparecer.)
        int saveTurns = AIController.Instance != null ? AIController.Instance.EliteSaveTurns : 0;
        int reach = saveTurns > 0
            ? snapshot.Budget + Mathf.Max(0, snapshot.IncomePerTurn) * saveTurns
            : snapshot.Budget;
        int eliteCost = FindCheapestBuildableCost(snapshot, elite);
        if (eliteCost <= 0 || eliteCost > reach)
            return;

        // Gate massa-ou-orçamento: abaixo do piso de massa, só cria a demanda elite se o caixa ATUAL
        // banca o elite + os corpos que faltam pra fechar a massa — aí compra elite E tropa no mesmo
        // turno (caixa gordo). Com massa >= piso, segue como antes (a reach/poupança já gateia).
        if (!IsOperationalCoreReadyForElite(snapshot, demands))
            return;

        MergeRoleDemand(demands, elite, false);
    }

    private static void AddEliteQualityDemand(
        AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands,
        UnitRole role,
        int activeRoleCount,
        int priority,
        bool groundCounterPressureCovered,
        bool rallyAssemblyActive)
    {
        if (snapshot == null || activeRoleCount <= 0
            || !IsOperationalCoreReadyForElite(snapshot, demands))
            return;

        // Razões de elite agora vêm do AI Manager (AIController), com par próprio por modo (normal/hard).
        float pressureRatio = AIController.Instance != null
            ? Mathf.Clamp01(AIController.Instance.EliteRatioPressure)
            : 0.33f;
        float safeRatio = AIController.Instance != null
            ? Mathf.Clamp01(AIController.Instance.EliteRatioSafe)
            : 0.5f;
        float targetRatio = groundCounterPressureCovered || rallyAssemblyActive
            ? Mathf.Max(pressureRatio, safeRatio)
            : pressureRatio;
        int desiredElite = Mathf.CeilToInt(activeRoleCount * targetRatio);

        int currentElite = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead
                && unit.TryGetUnitData(out UnitData data) && data != null
                && data.eliteLevel > 0
                && UnitRoleCompatibility.ResolveCompositionRole(data) == role)
                currentElite++;

        int alreadyDemanded = 0;
        foreach (AIShoppingDemand demand in demands)
            if (demand != null && demand.Count > 0 && demand.Role == role
                && demand.MinEliteLevel > 0)
                alreadyDemanded += demand.Count;

        int missing = Mathf.Max(0, desiredElite - currentElite - alreadyDemanded);
        if (missing <= 0)
            return;

        AIShoppingDemand quality = NewRoleDemand(
            role,
            missing,
            priority,
            "elite-quality",
            $"qualidade elite={currentElite}/{desiredElite} role={activeRoleCount}"
                + $" ratio={targetRatio:P0} countersCobertos={groundCounterPressureCovered}"
                + $" rallyAtivo={rallyAssemblyActive}",
            false);
        quality.MinEliteLevel = 1;

        int saveTurns = AIController.Instance != null ? AIController.Instance.EliteSaveTurns : 0;
        int reach = snapshot.Budget
            + Mathf.Max(0, snapshot.IncomePerTurn) * Mathf.Max(0, saveTurns);
        int eliteCost = FindCheapestBuildableCost(snapshot, quality);
        if (eliteCost <= 0 || eliteCost > reach)
            return;

        MergeRoleDemand(demands, quality, false);
    }

    private static int CountCompositionRole(AIWorldSnapshot snapshot, UnitRole role)
    {
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && unit.TryGetUnitData(out UnitData data)
                && (role == UnitRole.Logistica || role == UnitRole.Intel || role == UnitRole.RaidAntiSub
                    ? UnitRoleCompatibility.CanSatisfy(data, role)
                    : UnitRoleCompatibility.ResolveCompositionRole(data) == role))
                count++;
        return count;
    }

    private static int CountAtomicRole(AIWorldSnapshot snapshot, UnitRole role)
    {
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && unit.TryGetUnitData(out UnitData data)
                && data.roles != null && data.roles.Count > 0 && data.roles[0] == role)
                count++;
        return count;
    }

    private static int CountEnemyDomain(AIWorldSnapshot snapshot, Domain domain)
    {
        int count = 0;
        foreach (UnitManager unit in snapshot.EnemyUnits)
            if (unit != null && !unit.IsDead && unit.TryGetUnitData(out UnitData data) && data.domain == domain)
                count++;
        return count;
    }

    private static int CountEnemyCombatRole(AIWorldSnapshot snapshot, UnitRole role)
    {
        int count = 0;
        foreach (UnitManager unit in snapshot.EnemyUnits)
            if (unit != null && !unit.IsDead && unit.TryGetUnitData(out UnitData data)
                && UnitRoleCompatibility.CanSatisfy(data, role))
                count++;
        return count;
    }

    private static int CountOwnedAirAttack(AIWorldSnapshot snapshot, bool eliteOnly)
    {
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && unit.TryGetUnitData(out UnitData data)
                && data.domain == Domain.Air && UnitRoleCompatibility.CanSatisfy(data, UnitRole.AtaqueAereo)
                && (!eliteOnly || data.eliteLevel > 0))
                count++;
        return count;
    }

    private static int CountUnitsUnderRepair(AIWorldSnapshot snapshot)
    {
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && unit.IsUnderRepair) count++;
        return count;
    }

    private static bool HasEnemyAirProduction(AIWorldSnapshot snapshot)
    {
        foreach (ConstructionManager building in snapshot.EnemyBuildings)
            if (building != null && building.TryResolveConstructionData(out ConstructionData data)
                && data != null && data.isAirport) return true;
        return false;
    }

    private static bool HasEnemySubmarineCapability(AIWorldSnapshot snapshot)
    {
        if (CountEnemyDomain(snapshot, Domain.Submarine) > 0) return true;
        foreach (ConstructionManager building in snapshot.EnemyBuildings)
            if (building != null && building.TryResolveConstructionData(out ConstructionData data)
                && data != null && data.isHarbor) return true;
        return false;
    }

    private static GameUnitClass ResolveDominantVisibleEnemyClass(AIWorldSnapshot snapshot)
    {
        var counts = new Dictionary<GameUnitClass, int>();
        GameUnitClass best = GameUnitClass.Infantry;
        int bestCount = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || !enemy.TryGetUnitData(out UnitData data)) continue;
            counts.TryGetValue(data.unitClass, out int count);
            count++;
            counts[data.unitClass] = count;
            if (count > bestCount) { bestCount = count; best = data.unitClass; }
        }
        return best;
    }

    private static void LogRoleShoppingQueue(AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands, int budget)
    {
        var log = new System.Text.StringBuilder();
        log.Append($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
            + $"fila unica budget={budget} stance={snapshot.Stance}");
        foreach (AIShoppingDemand demand in demands)
            log.Append($"\n  pri={demand.Priority} urgent={demand.Urgent} {FormatDemandCapability(demand)}"
                + $" x{demand.Count} elite={demand.MinEliteLevel}-{demand.MaxEliteLevel}"
                + $" origem={demand.Origin} motivo={demand.Reason}");
        Debug.Log(log.ToString());
    }
}
