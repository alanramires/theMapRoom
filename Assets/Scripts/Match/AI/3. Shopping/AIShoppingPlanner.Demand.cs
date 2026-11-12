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
                if (r == UnitRole.Transportador || (UnitRoleCompatibility.MatchesLabel(r, UnitRole.AtaqueAereo) && d.eliteLevel == 0))
                    enemyHelicos++;
                else if (UnitRoleCompatibility.MatchesLabel(r, UnitRole.AtaqueAereo) && d.eliteLevel >= 1)
                    enemyBombers++;
            }

        int activeCacaA = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d?.roles == null || d.roles.Count == 0) continue;
                if (d.domain == Domain.Air && UnitRoleCompatibility.MatchesLabel(d.roles[0], UnitRole.Interceptador) && d.eliteLevel >= 1) activeCacaA++;
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
                if (d.domain == Domain.Air && UnitRoleCompatibility.MatchesLabel(d.roles[0], UnitRole.Interceptador) && d.eliteLevel == 0) active++;
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
                if (UnitRoleCompatibility.MatchesLabel(r, UnitRole.Interceptador) || (UnitRoleCompatibility.MatchesLabel(r, UnitRole.AtaqueAereo) && d.eliteLevel >= 1))
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
                if (UnitRoleCompatibility.MatchesLabel(d.roles[0], UnitRole.Interceptador) && d.eliteLevel >= 1) active++;
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
                if (r == UnitRole.Transportador || (UnitRoleCompatibility.MatchesLabel(r, UnitRole.AtaqueAereo) && d.eliteLevel == 0))
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
                if (UnitRoleCompatibility.MatchesLabel(d.roles[0], UnitRole.AtaqueAereo) && d.eliteLevel == 0) active++;
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
                if (UnitRoleCompatibility.MatchesLabel(d.roles[0], UnitRole.AtaqueAereo) && d.eliteLevel == 0) activeApaches++;
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

    private static void ComputeAirSurveillanceDemand(
        AIWorldSnapshot snapshot,
        AIIntelReport intel,
        out int openAirSurveillanceSlots,
        out int openMobileAirSurveillanceSlots)
    {
        openAirSurveillanceSlots = 0;
        openMobileAirSurveillanceSlots = 0;
        if (snapshot == null)
            return;

        int minTurn = Instance != null
            ? Instance.MinTurnForAirSurveillance
            : 4;
        bool tooEarly = snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn;
        int visibleAir = CountTotalVisibleEnemyAircraft(snapshot);
        float inferredAir = intel != null ? intel.enemyAirThreatScore : 0f;
        float airThreshold = Instance != null ? Instance.IntelAirThreatAntiAirThreshold : 2f;

        int activeAirSurveillance =
            CountActiveDedicatedAirSurveillance(snapshot, Domain.Air);
        int activeMobileAirSurveillance =
            CountActiveDedicatedAirSurveillance(snapshot, Domain.Land);
        int activeAirCombat = CountActiveAirCombatUnits(snapshot);
        bool enemyAirProduction = HasEnemyAirportProductionCapacity(snapshot);
        bool ownAirSurveillanceProduction =
            HasDedicatedAirSurveillanceProduction(
                snapshot, Domain.Air);
        int cheapestAirSurveillance =
            ownAirSurveillanceProduction
                ? FindCheapestDedicatedAirSurveillanceCost(
                    snapshot, Domain.Air)
                : 0;
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
            Debug.Log(
                $"[AI Shopping] vigilancia_aerea_demand: 0 " +
                $"turn={snapshot.TurnNumber}<{minTurn} " +
                $"visibleAir={visibleAir} inferredAir={inferredAir:F1}");
            return;
        }

        // EWACS cobre um mapa pequeno/medio quase inteiro. Plataforma aerea de
        // EWACS fica como peça estratégica única; Radar Móvel complementa o céu.
        int maxAir = Mathf.Min(
            1,
            Instance != null
                ? Instance.MaxAirSurveillance
                : 1);
        int maxMobileAir = Instance != null
            ? Instance.MaxMobileAirSurveillance
            : 1;
        int desiredAirSurveillance =
            needsAirPicture ? 1 : 0;
        openAirSurveillanceSlots = ownAirSurveillanceProduction
            ? Mathf.Max(
                0,
                Mathf.Min(maxAir, desiredAirSurveillance)
                - activeAirSurveillance)
            : 0;

        bool canBuyAirSurveillance =
            ownAirSurveillanceProduction
            && cheapestAirSurveillance > 0
            && snapshot.Budget >= cheapestAirSurveillance;
        bool hasOrWillHaveAirSurveillance =
            activeAirSurveillance > 0
            || openAirSurveillanceSlots > 0;
        bool needsMobileAirSurveillance =
            desiredAirSurveillance > 0
            && activeMobileAirSurveillance <= 0
            && (activeAirSurveillance > 0
                || !hasOrWillHaveAirSurveillance
                || !ownAirSurveillanceProduction
                || !canBuyAirSurveillance
                || snapshot.Budget < Mathf.Max(12000, Mathf.Max(1, snapshot.IncomePerTurn)));
        int desiredMobileAirSurveillance =
            needsMobileAirSurveillance ? 1 : 0;
        openMobileAirSurveillanceSlots = Mathf.Max(
            0,
            Mathf.Min(maxMobileAir, desiredMobileAirSurveillance)
            - activeMobileAirSurveillance);

        Debug.Log(
            $"[AI Shopping] vigilancia_aerea_demand: " +
            $"air={openAirSurveillanceSlots} " +
            $"mobileAir={openMobileAirSurveillanceSlots} " +
            $"desiredAir={desiredAirSurveillance} " +
            $"airCap={maxAir} mobileAirCap={maxMobileAir} " +
            $"needsMobileAir={needsMobileAirSurveillance} " +
            $"activeAir={activeAirSurveillance} " +
            $"activeMobileAir={activeMobileAirSurveillance} " +
            $"enemyAirProd={enemyAirProduction} " +
            $"ownAirSurveillanceProd={ownAirSurveillanceProduction} " +
            $"visibleAir={visibleAir} inferredAir={inferredAir:F1} " +
            $"airCombat={activeAirCombat} offensive={offensivePlan} " +
            $"budget={snapshot.Budget}");
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
            || AIController.Instance == null || !AIController.Instance.LimitaLogistica)
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
        if (AIController.Instance != null && AIController.Instance.LimitaLogistica)
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

    private static int CountActiveDedicatedAirSurveillance(
        AIWorldSnapshot snapshot,
        Domain domain)
    {
        if (snapshot == null)
            return 0;

        int count = CountExistingDedicatedAirSurveillance(
            snapshot.AITeam, domain);
        if (count > 0 || snapshot.MyUnits == null)
            return count;

        // Fallback for tests/mocked snapshots where UnitManager.AllActive is not populated.
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.domain != domain)
                continue;
            if (IsDedicatedAirSurveillancePurchase(data))
                count++;
        }
        return count;
    }

    private static int CountActiveDedicatedAirSurveillance(
        AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0;

        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;
            if (IsDedicatedAirSurveillancePurchase(data))
                count++;
        }
        return count;
    }

    private static int CountExistingDedicatedAirSurveillance(
        TeamId aiTeam,
        Domain domain)
    {
        int count = 0;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.SlotIndex != AIController.ResolveAISlotKey(aiTeam) || unit.IsDead || unit.IsEmbarked)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.domain != domain)
                continue;
            if (IsDedicatedAirSurveillancePurchase(data))
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

    private static bool HasDedicatedAirSurveillanceProduction(
        AIWorldSnapshot snapshot,
        Domain domain)
    {
        return FindCheapestDedicatedAirSurveillanceCost(
            snapshot, domain) > 0;
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

    private static int FindCheapestDedicatedAirSurveillanceCost(
        AIWorldSnapshot snapshot,
        Domain domain)
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
                if (!IsDedicatedAirSurveillancePurchase(unit))
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
                || (UnitCombatModalityRules.IsHybrid(unit)
                    && UnitRoleCompatibility.CanSatisfy(data, UnitRole.FogoIndireto))
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
                || (UnitCombatModalityRules.IsHybrid(unit)
                    && UnitRoleCompatibility.CanSatisfy(data, UnitRole.FogoIndireto))
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
        // Parcela do imposto de conscricao do produtor, devolvida ao carrinho quando ele o usa.
        public int TaxRefund;
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

        shoppingRideSnapshot = null;
        shoppingRideQueue = null;
        s_lastBuiltRoster = null;
        s_lastBuiltCounter = null;
        s_lastBuiltOperational = null;
        List<AIShoppingDemand> demands = BuildRoleShoppingDemands(snapshot);
        AIElitePurchaseCommitment eliteCommitment =
            ResolveElitePurchaseCommitment(snapshot, demands);
        AIShoppingDemand preventiveAirControl = eliteCommitment != null
            && eliteCommitment.role == UnitRole.Interceptador && eliteCommitment.eliteLevel == 1
                ? demands.Find(d => d != null && d.Count > 0 && d.Role == UnitRole.Interceptador
                    && d.IsPreventiveAirControl)
                : null;
        // Reusa o que a demanda acabou de montar (mesmo snapshot): antes refazia roster e counter
        // uma segunda vez aqui. Todas as faccoes publicam estes fatos na montagem.
        AIRosterKnowledge shoppingRoster = s_lastBuiltRoster ?? BuildRosterKnowledge(snapshot, log: false);
        CounterPressureInspection counterPressure = s_lastBuiltCounter ?? BuildCounterPressure(snapshot, shoppingRoster);
#if UNITY_EDITOR
        // FATO NA ORIGEM: a Shopping Pressure le ESTA compra, nao refaz outra ao lado. Mesmas
        // referencias que o carrinho usa; o que o resto da funcao muda na lista aparece la.
        s_lastDecisionBySlot[snapshot.AISlotIndex] = new ShoppingDecisionInspection
        {
            Turn = snapshot.TurnNumber,
            Slot = snapshot.AISlotIndex,
            SceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle.GetRawData(),
            Snapshot = snapshot,
            Demands = demands,
            Roster = shoppingRoster,
            Counter = counterPressure,
            Operational = s_lastBuiltOperational ?? BuildOperationalPressure(snapshot),
        };
#endif
        var occupied = BuildProductionOccupiedCells();
        var usedBuildings = new HashSet<ConstructionManager>();
        int remaining = snapshot.Budget;
        LastSavingSummary = null;
        lastSavingTargetLabel = null;

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
        var conscriptionTaxShares = new Dictionary<ConstructionManager, int>();
        bool conscriptionActive = false;
        if (HasShoppingBase(snapshot) && AIController.Instance != null && AIController.Instance.ConscriptionDoctrine)
            conscriptionActive = !ResolveMassacrePhase(snapshot, AIController.Instance);
        if (conscriptionActive)
        {
            conscriptionTax = ComputeConscriptionTax(snapshot, occupied, conscriptionTaxShares);
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
        // The selected preventive interceptor shares the existing reserve and
        // guaranteed-purchase path; an armor bootstrap must not override it.
        if (preventiveAirControl != null)
        {
            blitzArmorReserve = 0;
            buyFirstMbtNow = false;
        }
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
        if (HasShoppingBase(snapshot) && AIController.Instance != null
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
        // PODER DE CAPTURA INIMIGO EM TRANSITO ROMPE A POUPANCA DO ELITE, perdendo ou nao: tres
        // Chinooks carregados nao esperam dois turnos de economia (AI Step T3 amarelo: $10151
        // guardados para o Bombardeiro, nenhum Apache). O compromisso continua gravado; quando
        // os pares de Disruptor fecharem, a demanda some e a poupanca volta sozinha.
        // Rompe NO TAMANHO DA DEMANDA, nunca o caixa todo: zerar a reserva abria o dinheiro do
        // Bombardeiro para qualquer um, e a AAA urgente (+250000 no score) levou tudo — AI Step
        // T5 amarelo: Apache + 3 AAA, $2500 de sobra, compromisso de T3 jogado fora.
        // A ESCOLTA (o nosso poder de captura sendo mordido) rompe do mesmo jeito. Contains, e
        // nao igualdade: o MergeRoleDemand concatena a origem ("operation+escolta").
        // Peca aerea que JA CABE: compra garantida antes do carrinho (ver TryPreCommitDemand).
        // Sem isso, Caca B de $16000 com $18500 livres perdia para Apache + Chinook + Suprimentos
        // + Soldados — 6 atendimentos valem mais na soma do beam que 1 escolta (AI Step T7).
        AIShoppingDemand airPreCommit = null;
        // A VANGUARDA (caca pela presa sobre a tropa descoberta) e a escolta generalizada: mesma regra.
        foreach (string airOrigin in new[] { "disruptor", "vanguarda", "escolta" })
        {
            AIShoppingDemand airDemand = demands.Find(d => d != null && d.Count > 0
                && d.Origin != null && d.Origin.Contains(airOrigin));
            if (airDemand == null)
                continue;
            int demandCost = EstimateDemandCost(snapshot, airDemand);
            int unitCost = demandCost > 0 ? demandCost / airDemand.Count : 0;
            if (unitCost <= 0)
                continue;
            int spendableBefore = remaining - reserve - conscriptionTax;
            if (spendableBefore >= unitCost)
            {
                if (airPreCommit == null)
                    airPreCommit = airDemand; // ja cabe sem tocar na poupanca: garante antes do carrinho
                continue;
            }
            // NAO CABE NEM ROMPENDO: segura e passa a vez. O imposto de conscricao continua
            // (a massa sai), o resto do caixa fica guardado pra peca — no mesmo horizonte da
            // poupanca do elite (EliteSaveTurns). Liberar a reserva sem fechar a peca so
            // soltava o dinheiro: Caca B de $16000 com $15000 livres nao saiu, e os $6750 do
            // Bombardeiro (prometido em T3) viraram Apache + Chinook (AI Step T6 amarelo).
            if (spendableBefore + reserve < unitCost)
            {
                int saveTurns = AIController.Instance != null ? AIController.Instance.EliteSaveTurns : 1;
                int reach = remaining - conscriptionTax + Mathf.Max(0, snapshot.IncomePerTurn) * saveTurns;
                if (saveTurns > 0 && unitCost <= reach)
                {
                    int hold = Mathf.Max(reserve, remaining - conscriptionTax);
                    Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                        + $"{airOrigin} segura o caixa ({hold}) e passa a vez: peca de {unitCost} nao cabe "
                        + $"em livre={spendableBefore}+reserva={reserve}; alcance em {saveTurns} turno(s)={reach} "
                        + $"— imposto de conscricao ({conscriptionTax}) segue");
                    reserve = hold;
                    reserveTarget = airDemand;
                    lastSavingTargetLabel = $"{airOrigin} ${unitCost}";
                    hasStrategicReserve = reserve > 0;
                    break;
                }
                Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                    + $"{airOrigin} fora de alcance: peca de {unitCost} > {reach} em {saveTurns} turno(s) "
                    + $"— poupanca ({reserve}) mantida");
                continue;
            }
            int release = Mathf.Min(reserve, demandCost);
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + $"{airOrigin} libera {release} da reserva elite ({reserve}): {airDemand.Reason}");
            reserve -= release;
            hasStrategicReserve = reserve > 0 && reserveTarget != null;
            // Liberou porque AGORA cabe: garante a peca antes do carrinho, como no caminho de cima.
            // Sem isto a reserva solta ia para o beam, que prefere largura: AI Step T8 (Peixe
            // Pequeno) — vanguarda pediu Caca B, liberou 8580, e saiu Apache + Apache + Chinook.
            if (airPreCommit == null)
                airPreCommit = airDemand;
        }
        {
            var saving = new System.Text.StringBuilder();
            if (hasStrategicReserve)
                saving.Append($"Poupança: ${reserve} para {lastSavingTargetLabel ?? FormatDemandCapability(reserveTarget)} (fora do carrinho)");
            if (conscriptionTax > 0)
            {
                if (saving.Length > 0) saving.Append('\n');
                saving.Append($"Imposto de conscrição: ${conscriptionTax} (massa garantida em cada produtor do exército)");
            }
            LastSavingSummary = saving.Length > 0 ? saving.ToString() : null;
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
        if (preventiveAirControl != null)
            TryPreCommitDemand(snapshot, preventiveAirControl, eliteCommitment,
                orders, usedBuildings, occupied, ref remaining,
                remaining - reserve - conscriptionTax, "controle_do_ar", "interceptador preventivo");
        if (buyFirstMbtNow && blitzArmorTarget != null)
            TryPreCommitBlitzFirstMbt(
                snapshot, blitzArmorTarget, eliteCommitment,
                orders, usedBuildings, occupied, ref remaining);
        if (airPreCommit != null && airPreCommit.Count > 0)
            TryPreCommitDemand(
                snapshot, airPreCommit, eliteCommitment,
                orders, usedBuildings, occupied, ref remaining,
                remaining - reserve - conscriptionTax,
                "escolta/disruptor", $"({airPreCommit.Origin})");

        // Nota — fill do fim do turno: consome o imposto comprando a massa (só a reserva elite é
        // intocável lá). Com ConscriptionWhenLosing (Médio/Competitiva), Perdendo nunca chega aqui
        // (RecruitmentSurgeFill retorna antes); na doutrina (Formigueiro/Agressiva), Perdendo passa
        // por aqui mesmo — conscrição é sempre, e massacre nunca ativa perdendo.
        // Teto de logística do Hard Mode aplicado DEPOIS da fusão de demandas — ver
        // ApplyHardModeLogisticsCap. Precisa vir antes do log da fila pra ela anunciar o número
        // que vai ser realmente comprado.
        ApplyHardModeLogisticsCap(snapshot, demands);
        DeferCounterDuringExpansion(snapshot, demands);
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
            breakStrategicReserve ? null : eliteCommitment?.unitId,
            conscriptionTaxShares);
        if (cart != null)
        {
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] carrinho "
                + $"itens={cart.Items.Count} demandas={cart.DistinctCovered} "
                + $"atendimentos={cart.FulfilledCount} gasto={cart.Spent} "
                + $"saldo livre={cart.RemainingBudget}"
                + (cart.ExpansionCapturerTarget > 0
                    ? $" expansãoCap={cart.ExpansionCapturerFulfilled}/{cart.ExpansionCapturerTarget}"
                    : ""));
            for (int i = 0; i < demands.Count; i++)
                demands[i].Count = cart.RemainingDemand[i];
            foreach (RoleShoppingCandidate item in cart.Items)
            {
                int index = IndexOf(item.Building.OfferedUnits, item.Unit);
                orders.Add(new ShoppingOrder
                {
                    Building = item.Building,
                    UnitToBuy = item.Unit,
                    SelectedIndex = index,
                });
                // Final demand counts also include shared wall coverage from
                // purchases made for composition or elite progression.
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
        // Este e o caminho de compra em uso (carrinho por papeis). A troca de fabrica
        // pela estrada vive aqui tambem: primeiro decide O QUE, depois ONDE nasce.
        SwapRoadVehiclesToRoadFactories(orders);
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
        string committedEliteUnitId,
        Dictionary<ConstructionManager, int> conscriptionTaxShares = null)
    {
        if (snapshot?.MyBuildings == null || demands == null || demands.Count == 0)
            return null;
        // Com imposto, o carrinho pode ter saldo livre zero e ainda comprar no produtor
        // taxado (a parcela dele volta). Sem imposto, segue o teste antigo.
        if (spendableBudget <= 0 && (conscriptionTaxShares == null || conscriptionTaxShares.Count == 0))
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
            int taxRefund = conscriptionTaxShares != null
                && conscriptionTaxShares.TryGetValue(building, out int share) ? share : 0;
            foreach (RoleShoppingCart state in beam)
            {
                // Não comprar também é uma escolha válida: caixa sem demanda elegível é preservado.
                next.Add(state);
                foreach (UnitData unit in building.OfferedUnits)
                {
                    if (unit == null || unit.cost <= 0 || unit.cost > state.RemainingBudget + taxRefund
                        || unit.militaryForce == MilitaryForce.Navy
                        || (!concentrateEmergency && !IsEliteChainAvailable(unit, snapshot)))
                        continue;

                    for (int demandIndex = 0; demandIndex < demands.Count; demandIndex++)
                    {
                        AIShoppingDemand demand = demands[demandIndex];
                        if ((demand.IsRideQueueReinforcement && state.Items.Exists(item => IsAirBus(item.Unit)))
                            || (IsExpansionCapturerBlocked(snapshot, unit) && !demand.IsConcreteDefense)
                            || state.RemainingDemand[demandIndex] <= 0
                            || !IsRolePurchaseAllowed(unit, snapshot.Stance,
                                IsEmergencyShoppingDemand(demand))
                            || !DoesUnitMeetShoppingDemand(unit, demand)
                            || !MatchesWallOffer(demand, unit, building)
                            || ShouldYieldGenericFireSupportToAntiInfantry(
                                unit, demand, counterPressure))
                            continue;

                        var candidate = new RoleShoppingCandidate
                        {
                            Building = building,
                            Unit = unit,
                            Demand = demand,
                            DemandIndex = demandIndex,
                            TaxRefund = taxRefund,
                            Score = ScoreRoleShoppingCandidate(
                                snapshot, unit, demand, spendableBudget,
                                counterPressure, macroLosing, concentrateSpend),
                        };
                        RoleShoppingCart expanded = state.Clone();
                        AddCandidateToRoleShoppingCart(expanded, candidate, demands);
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
        RoleShoppingCart cart, RoleShoppingCandidate candidate, List<AIShoppingDemand> demands)
    {
        int demandIndex = candidate.DemandIndex;
        bool firstCoverage = !cart.CoveredDemand[demandIndex];
        cart.Items.Add(candidate);
        cart.RemainingDemand[demandIndex]--;
        if (IsAirBus(candidate.Unit))
        {
            // A hull bought by another door covers the queue. A queue hull also covers
            // one normal air-transport slot, so the reinforcement is never additive.
            for (int i = 0; i < demands.Count; i++)
                if (i != demandIndex && cart.RemainingDemand[i] > 0
                    && (demands[i].IsRideQueueReinforcement
                        || (candidate.Demand.IsRideQueueReinforcement
                            && DoesUnitMeetShoppingDemand(candidate.Unit, demands[i])
                            && MatchesWallOffer(demands[i], candidate.Unit, candidate.Building))))
                { cart.RemainingDemand[i]--; if (candidate.Demand.IsRideQueueReinforcement) break; }
        }
        // O produtor taxado que o carrinho usa devolve a parcela do imposto: o corpo de massa
        // que ela garantia nao vai mais ser comprado ali.
        cart.RemainingBudget -= candidate.Unit.cost - candidate.TaxRefund;
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
        // A composition/elite purchase may also fill ONE wall-response slot.
        // Never count the same hull against both buckets of a mixed wall.
        if (candidate.Demand.ArtilleryWallGroup == 0)
        {
            int chosen = -1;
            for (int i = 0; i < demands.Count; i++)
                if (demands[i].ArtilleryWallGroup != 0 && cart.RemainingDemand[i] > 0
                    && MatchesWallOffer(demands[i], candidate.Unit, candidate.Building, coverage: true)
                    && (chosen < 0 || cart.RemainingDemand[i] > cart.RemainingDemand[chosen])) chosen = i;
            if (chosen >= 0)
            {
                cart.RemainingDemand[chosen]--;
                cart.FulfilledCount++;
                int value = Mathf.Max(1, 100 - demands[chosen].Priority);
                cart.FulfillmentPriorityScore += value;
                if (!cart.CoveredDemand[chosen])
                {
                    cart.CoveredDemand[chosen] = true; cart.DistinctCovered++;
                    cart.CoveragePriorityScore += value;
                }
            }
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

    // A GRANA ANTES DO COUNTER (capacidade expansaoAntesDoCounter — Médio e Difícil).
    //
    // Por que existe: o score do carrinho começa em 200000 − prioridade×3000, e o counter ganha
    // até ~36000 de bônus de encaixe (classe-alvo + counterFit). Um degrau de prioridade vale 3000,
    // então o bônus atravessa a fila: no T2, com 3 vagas de captura abertas, a Metranca (counter
    // pri 16, score 202210) passou por cima do 3º Soldado (capturador pri 15, score 155500) só
    // porque havia 6 soldados inimigos à vista — soldados que estavam capturando, não ameaçando.
    // Marca do AI Step "porque metranca?", 2026-11-04.
    //
    // A regra: na EarlyExpansion, com vaga de captura aberta no plano, counter NÃO urgente sai
    // da fila deste turno. Prédio é renda; o counter cedo é pago com a renda que deixou de entrar.
    // Passa o que é urgente (forceBasicResponse: a categoria passou do limiar de escalada) e a
    // escalada estratégica — esses são ameaça, não presença. Não mexe nos pesos: mudar o degrau
    // de prioridade reordenaria a fila inteira para resolver um caso só da abertura.
    private static void DeferCounterDuringExpansion(
        AIWorldSnapshot snapshot, List<AIShoppingDemand> demands)
    {
        if (snapshot == null || demands == null
            || AIController.Instance == null || !AIController.Instance.ExpandeAntesDoCounter)
            return;

        AIController.MacroTerritoryInspection macro =
            AIController.GetMacroTerritoryForInspection(snapshot.AITeam);
        if (!string.Equals(macro.PhaseRaw, "EarlyExpansion", System.StringComparison.Ordinal))
            return;

        int openCaptureSlots = CountOpenSlots(snapshot.AITeam, UnitRole.Capturador);
        if (openCaptureSlots <= 0)
            return;

        for (int i = demands.Count - 1; i >= 0; i--)
        {
            AIShoppingDemand demand = demands[i];
            if (demand == null || demand.Count <= 0 || demand.Urgent || demand.StrategicEscalation
                || !IsPureCounterPressureOrigin(demand.Origin))
                continue;

            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] expansão antes do "
                + $"counter: adiado {FormatDemandCapability(demand)} x{demand.Count} origem={demand.Origin} "
                + $"({demand.Reason}) — {openCaptureSlots} vaga(s) de captura abertas, não é urgente");
            demands.RemoveAt(i);
        }
    }

    // MergeRoleDemand junta origens com "+": uma demanda de outra fonte com a mesma arma pode ter
    // absorvido o counter (ou vice-versa). Só sai da fila o que é counter de ponta a ponta — senão
    // adiar o counter apagaria junto uma demanda que não tem nada a ver com ele.
    private static bool IsPureCounterPressureOrigin(string origin)
    {
        if (string.IsNullOrEmpty(origin))
            return false;
        foreach (string part in origin.Split('+'))
            if (!part.StartsWith("counter-pressure", System.StringComparison.Ordinal))
                return false;
        return true;
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
        if (!HasShoppingBase(snapshot))
        {
            if (existing != null)
            {
                Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] cancela compromisso elite: time sem QG");
                AIIntelLedger.ClearElitePurchaseCommitment(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
            }
            return null;
        }
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
            else if (existing.counterEscalation
                && !IsCounterResponseRoleAllowed(unit, existing.counterCategory))
                invalidReason = "papel da unidade nao atende a categoria de counter (interceptador responde ao ar)";
            else if (existing.counterEscalation && unit.domain == Domain.Land
                && demands.Exists(d => d != null && d.Count > 0 && d.ExcludeLandResponse
                    && d.RequiredWeaponCategory == existing.counterCategory
                    && (!d.TargetClass.HasValue || !existing.counterHasTargetClass
                        || d.TargetClass.Value == existing.counterTargetClass)))
                invalidReason = "counter terrestre sem acesso a pressao em outra ilha";
            else if (IsCommitmentOutsideCutOffEliteLine(snapshot, demands, unit, existing))
                invalidReason = "fora da linha do elite (ilha sem ligacao: a ficha nao declara DisruptorDeCaptura)";
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
            bool compareControlLines = bestDemand != null && demand.Origin == "elite"
                && bestDemand.Origin == "elite"
                && (demand.Role == UnitRole.Interceptador || bestDemand.Role == UnitRole.Interceptador);
            int lineOrder = compareControlLines
                ? CompareEliteReserveLines(snapshot, candidate, bestUnit) : 0;
            if (lineOrder < 0 || (lineOrder == 0 && (bestDemand == null || candidate.eliteLevel < bestUnit.eliteLevel
                || (candidate.eliteLevel == bestUnit.eliteLevel
                    && demand.Priority < bestDemand.Priority)
                || (candidate.eliteLevel == bestUnit.eliteLevel
                    && demand.Priority == bestDemand.Priority && candidate.cost > bestUnit.cost))))
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

    // Compromisso de elite de ATAQUE AEREO na ilha cortada cuja ficha NAO declara
    // DisruptorDeCaptura: a linha agora e o Disruptor e ninguem mais o pede — so a ruptura
    // (parede de artilharia), e essa demanda, viva, o mantem. Com o rotulo na ficha (o
    // Bombardeiro como Disruptor secundario), o compromisso fica.
    // Compromisso de counter (counterEscalation) nunca cai aqui: tem sua propria persistencia.
    private static bool IsCommitmentOutsideCutOffEliteLine(
        AIWorldSnapshot snapshot, List<AIShoppingDemand> demands,
        UnitData unit, AIElitePurchaseCommitment commitment)
    {
        if (unit == null || commitment == null || commitment.counterEscalation)
            return false;
        if (!IsGroundCutOffFromEnemy(snapshot, log: false))
            return false;
        if (UnitRoleCompatibility.ResolveCompositionRole(unit) != UnitRole.AtaqueAereo
            || UnitRoleCompatibility.CanSatisfy(unit, UnitRole.DisruptorDeCaptura))
            return false;
        if (demands != null)
            foreach (AIShoppingDemand demand in demands)
                if (demand != null && demand.Count > 0
                    && string.Equals(demand.Origin, "breakthrough", System.StringComparison.Ordinal)
                    && UnitRoleCompatibility.CanSatisfy(unit, demand.Role)
                    && unit.eliteLevel >= demand.MinEliteLevel)
                    return false;
        return true;
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
        demands.Sort(CompareShoppingDemandPriority);
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
                    || !DoesUnitMeetShoppingDemand(unit, demand)
                    || !MatchesWallOffer(demand, unit, building))
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

    // ───────────────────────────────────────────── rota terrestre ao inimigo ──
    // INTEL: a ilha do QG desta IA tem ligacao POR TERRA com algum QG inimigo? Pergunta ao
    // SectorManager (mapa de distancia reverso a partir de cada QG inimigo, pela ficha
    // terrestre mais barata da familia Assalto que a IA vende) e cruza com o QG e os
    // produtores da IA.
    //
    // Tres respostas, separadas de proposito ("gate inaplicavel": nao confundir "ainda
    // nao" com "impossivel neste mapa"):
    //   - sem dado (sem assalto terrestre ofertado, sem QG inimigo, sem contexto de
    //     distancia) -> NAO corta nada. Falha conservadora.
    //   - alguma origem alcanca algum QG inimigo -> ligado.
    //   - todos os mapas montados e nenhuma origem dentro -> SEM LIGACAO.
    //
    // E a ilha INVADIDA (inimigo visivel numa celula de onde se chega por terra ao QG da IA)
    // religa tudo: pacote terrestre e poupanca do blindado voltam, porque ai o tanque tem
    // onde lutar.
    [System.NonSerialized] private static AIWorldSnapshot s_groundRouteSnapshot;
    private static bool s_groundCutOff;

    // log:false e para quem le de fora do carrinho (Shopping Pressure no editor, counter):
    // a janela monta snapshot novo a cada refresh e encheria o Console.
    private static bool IsGroundCutOffFromEnemy(AIWorldSnapshot snapshot, bool log = true)
    {
        if (snapshot == null)
            return false;
        if (ReferenceEquals(snapshot, s_groundRouteSnapshot))
            return s_groundCutOff;

        s_groundRouteSnapshot = snapshot;
        s_groundCutOff = ComputeGroundCutOff(snapshot, out string why);
        if (log)
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] rota terrestre: {why}");
        return s_groundCutOff;
    }

    // "Esta celula e a NOSSA terra?" — a ilha geografica do QG (BeachManager) ou, sem ela, o
    // alcance terrestre da ficha de referencia a partir do QG. Uma fonte so para a invasao
    // (TryFindEnemyOnOwnIsland) e para o counter (not_in_reach). Sem dado responde SIM:
    // falha conservadora, nao tira valor de resposta nenhuma.
    private sealed class OwnLandProbe
    {
        public BeachManager Beaches;
        public BeachManager.IslandInfo Island;
        public Dictionary<Vector3Int, int> Reach;
        public string Source = "";

        public bool Contains(Vector3Int cell)
        {
            cell.z = 0;
            if (Island != null)
                return Beaches.TryGetIslandAtCell(cell, out BeachManager.IslandInfo ilha) && ilha == Island;
            if (Reach != null)
                return Reach.ContainsKey(cell);
            return true;
        }
    }

    private static OwnLandProbe BuildOwnLandProbe(AIWorldSnapshot snapshot)
    {
        if (snapshot?.MyBuildings == null)
            return null;
        Vector3Int? ownHq = null;
        UnitData reference = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null) continue;
            if (building.IsPlayerHeadQuarter)
            {
                Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
                ownHq = cell;
            }
            if (building.OfferedUnits != null)
                foreach (UnitData offered in building.OfferedUnits)
                    if (offered != null && offered.domain == Domain.Land
                        && UnitRoleCompatibility.ResolveFamily(offered) == UnitRoleFamily.Assalto
                        && (reference == null || offered.cost < reference.cost))
                        reference = offered;
        }
        return ownHq.HasValue ? BuildOwnLandProbe(ownHq.Value, reference) : null;
    }

    private static OwnLandProbe BuildOwnLandProbe(Vector3Int ownHq, UnitData reference)
    {
        var probe = new OwnLandProbe();
        BeachManager beaches = BeachManager.GetOrCreate();
        if (beaches != null && beaches.TryGetIslandAtCell(ownHq, out BeachManager.IslandInfo ilhaDoQg) && ilhaDoQg != null)
        {
            probe.Beaches = beaches;
            probe.Island = ilhaDoQg;
            probe.Source = ilhaDoQg.DisplayName;
            return probe;
        }
        probe.Source = "alcance terrestre do QG";
        if (reference != null
            && SectorManager.TryBuildLandMovementDistanceToTargetMap(ownHq, reference, out Dictionary<Vector3Int, int> alcance)
            && alcance != null)
            probe.Reach = alcance;
        return probe;
    }

    private static bool ComputeGroundCutOff(AIWorldSnapshot snapshot, out string why)
    {
        why = "sem dado (nao corta)";
        if (snapshot.MyBuildings == null)
            return false;

        UnitData reference = null;
        var origins = new List<Vector3Int>();
        Vector3Int? ownHq = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null) continue;
            Vector3Int cell = building.CurrentCellPosition; cell.z = 0;
            bool sells = false;
            if (building.OfferedUnits != null)
                foreach (UnitData offered in building.OfferedUnits)
                {
                    if (offered == null || offered.domain != Domain.Land
                        || UnitRoleCompatibility.ResolveFamily(offered) != UnitRoleFamily.Assalto)
                        continue;
                    sells = true;
                    if (reference == null || offered.cost < reference.cost)
                        reference = offered;
                }
            if (building.IsPlayerHeadQuarter) ownHq = cell;
            if (sells || building.IsPlayerHeadQuarter)
                origins.Add(cell);
        }
        if (reference == null || origins.Count == 0)
            return false;

        var enemyHqs = new List<Vector3Int>();
        foreach (ConstructionManager c in ConstructionManager.AllActive)
            if (c != null && c.IsPlayerHeadQuarter && c.TeamId != TeamId.Neutral && c.TeamId != snapshot.AITeam)
            {
                Vector3Int hq = c.CurrentCellPosition; hq.z = 0;
                enemyHqs.Add(hq);
            }
        if (enemyHqs.Count == 0)
            return false;

        foreach (Vector3Int hq in enemyHqs)
        {
            if (!SectorManager.TryBuildLandMovementDistanceToTargetMap(hq, reference, out Dictionary<Vector3Int, int> map)
                || map == null)
                return false; // sem contexto de distancia: nao afirma corte
            foreach (Vector3Int origin in origins)
                if (map.ContainsKey(origin))
                {
                    why = $"ligada ({reference.displayName} chega a um QG inimigo)";
                    return false;
                }
        }

        // Sem ligacao. A ilha do QG esta sendo invadida?
        if (ownHq.HasValue && snapshot.EnemyUnits != null
            && TryFindEnemyOnOwnIsland(snapshot, ownHq.Value, reference, out UnitManager invasor, out string fonteIlha))
        {
            Vector3Int ec = invasor.CurrentCellPosition; ec.z = 0;
            why = $"SEM LIGACAO, mas a ilha do QG esta invadida ({invasor.UnitDisplayName}#{invasor.InstanceId} "
                + $"em ({ec.x},{ec.y}), {fonteIlha}) — pacote terrestre e poupanca do blindado religados";
            return false;
        }

        why = $"SEM LIGACAO: {reference.displayName} nao chega por terra a nenhum QG inimigo "
            + $"({origins.Count} origens, {enemyHqs.Count} QG) — pacote pede ataque aereo e antiaereo; "
            + "sem poupanca de blindado";
        return true;
    }

    // Inimigo visivel na ilha do QG. A ilha vem da GEOGRAFIA do BeachManager (planicie,
    // floresta, montanha e praia continuas; agua e ponte separam): pega tambem o desembarque
    // numa serra que o tanque nao alcanca — "atacar os setores cobertos pela ilha". Sem
    // BeachManager ou sem ilha no QG, cai no alcance terrestre do tanque a partir do QG.
    private static bool TryFindEnemyOnOwnIsland(
        AIWorldSnapshot snapshot, Vector3Int ownHq, UnitData reference,
        out UnitManager invader, out string source)
    {
        invader = null;
        OwnLandProbe probe = BuildOwnLandProbe(ownHq, reference);
        source = probe.Source;
        // Sem ilha e sem alcance nao ha como afirmar invasao (Contains responderia SIM a tudo).
        if (probe.Island == null && probe.Reach == null)
            return false;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            // So quem esta EM TERRA invade: aeronave sobrevoando nao e desembarque, e
            // tanque nao responde a ela (a vaga antiaerea do pacote responde).
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked || enemy.GetDomain() != Domain.Land) continue;
            if (probe.Contains(enemy.CurrentCellPosition))
            {
                invader = enemy;
                return true;
            }
        }
        return false;
    }

    // Algum produtor da IA oferta unidade que CONTA para este componente do nucleo? Assalto
    // e fogo contam por FAMILIA (o mesmo que CountCoreFamily); os demais, por composicao.
    // Usar CanSatisfy aqui faria um CapturadorCombatente "prometer" fechar o slot de Assalto
    // que ele nunca conta, e o alvo continuaria inalcancavel.
    private static bool CanAnyOfferedUnitCloseCore(AIWorldSnapshot snapshot, UnitRole role)
    {
        if (snapshot?.MyBuildings == null)
            return true; // sem informação de oferta: não afrouxa o gate
        UnitRoleFamily family = role == UnitRole.Assalto ? UnitRoleFamily.Assalto
            : role == UnitRole.FogoIndireto ? UnitRoleFamily.FogoDeSuporte : UnitRoleFamily.Nenhuma;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || building.OfferedUnits == null) continue;
            foreach (UnitData offered in building.OfferedUnits)
                if (offered != null && (family != UnitRoleFamily.Nenhuma
                        ? UnitRoleCompatibility.ResolveFamily(offered) == family
                        : UnitRoleCompatibility.ResolveCompositionRole(offered) == role))
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
            && CountCoreFamily(snapshot, UnitRoleFamily.Assalto) >= assaultTarget
            && CountCoreFamily(snapshot, UnitRoleFamily.FogoDeSuporte) >= artilleryTarget;
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
            CountCoreFamily(snapshot, UnitRoleFamily.Assalto) / (float)assaultTarget);
        float fire = artilleryTarget <= 0 ? 1f : Mathf.Clamp01(
            CountCoreFamily(snapshot, UnitRoleFamily.FogoDeSuporte) / (float)artilleryTarget);
        return (capturer + assault + fire) / 3f;
    }

    // Decide quanto de caixa segurar para um elite ou uma capacidade crítica prioritária
    // alcançável em poucos turnos. O excedente continua disponível para compras menores.
    // Retorna 0 quando não há nada para poupar (guloso normal).
    // Para o AI Step: o que o turno guardou FORA do carrinho, em texto (poupanca do elite com
    // o nome da unidade, imposto de conscricao). Reescrito a cada compra por papeis.
    public static string LastSavingSummary { get; private set; }
    private static string lastSavingTargetLabel;

    private static int ComputeStrategicSavingReserve(
        AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands,
        int remaining,
        AIElitePurchaseCommitment commitment,
        out AIShoppingDemand target)
    {
        target = null;
        if (!HasShoppingBase(snapshot) || Instance == null || demands == null || remaining <= 0)
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
        string targetUnitName = null;
        if (commitment != null)
        {
            target = FindDemandForEliteCommitment(demands, commitment);
            UnitData committedUnit = FindOfferedUnitById(snapshot, commitment.unitId,
                out bool availableNow, commitment.counterEscalation);
            if (target != null && committedUnit != null)
            {
                targetCost = committedUnit.cost;
                // O log dizia a DEMANDA que casou com o compromisso ("Counter/Infantry...")
                // em vez da UNIDADE poupada (Bombardeiro $22000).
                targetUnitName = committedUnit.displayName;
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
        if (targetUnitName == null)
            targetUnitName = FindBestOfferedUnitForDemand(snapshot, target)?.displayName;
        lastSavingTargetLabel = targetUnitName != null
            ? $"{targetUnitName} ${targetCost}"
            : $"{FormatDemandCapability(target)} ${targetCost}";
        Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] poupando p/ "
            + $"{lastSavingTargetLabel} ({FormatDemandCapability(target)}) renda={income} maxTurnos={maxTurns} "
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
        if (!HasShoppingBase(snapshot) || demands == null || remaining <= 0)
            return 0;
        if (AIController.Instance == null || !AIController.Instance.AbreComBlindado)
            return 0;
        if (CountActiveEliteAssaultUnits(snapshot) > 0)
            return 0;
        // Ilha sem rota terrestre ao inimigo: nao entesoura caixa para o primeiro blindado.
        if (IsGroundCutOffFromEnemy(snapshot))
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
        => TryPreCommitDemand(snapshot, target, eliteCommitment, orders, usedBuildings,
            occupied, ref remaining, remaining, "blitz_armor", "do 1º MBT");

    // Compra GARANTIDA antes do carrinho: o beam soma cobertura e prefere largura (varias
    // pecas baratas) a profundidade (uma cara). Para o 1o MBT e para a escolta isso inverte
    // a intencao. spendLimit = quanto desta compra pode sair sem tocar poupanca/imposto.
    private static bool TryPreCommitDemand(
        AIWorldSnapshot snapshot, AIShoppingDemand target,
        AIElitePurchaseCommitment eliteCommitment,
        List<ShoppingOrder> orders, HashSet<ConstructionManager> usedBuildings,
        HashSet<Vector3Int> occupied, ref int remaining, int spendLimit,
        string logTag, string logWhat)
    {
        if (snapshot?.MyBuildings == null || target == null || target.Count <= 0)
            return false;
        int budget = Mathf.Min(remaining, spendLimit);

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
                if (unit == null || unit.cost <= 0 || unit.cost > budget
                    || unit.militaryForce == MilitaryForce.Navy
                    || !IsEliteChainAvailable(unit, snapshot)
                    || !IsRolePurchaseAllowed(unit, snapshot.Stance, IsEmergencyShoppingDemand(target))
                    || !DoesUnitMeetShoppingDemand(unit, target)
                    || !MatchesWallOffer(target, unit, building))
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
        Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] {logTag}: "
            + $"PRÉ-COMPRA garantida {logWhat} {bestUnit.displayName} ${bestUnit.cost} em "
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
                    || !DoesUnitMeetShoppingDemand(unit, demand)
                    || !MatchesWallOffer(demand, unit, building))
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
                    || !DoesUnitMeetShoppingDemand(unit, demand)
                    || !MatchesWallOffer(demand, unit, building))
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

    // O que a ultima montagem de demanda REAL (log=true, a do Decide) calculou no caminho.
    // Zerados no inicio do DecideRoleBased; nunca lidos fora dele e da inspecao.
    private static AIRosterKnowledge s_lastBuiltRoster;
    private static CounterPressureInspection s_lastBuiltCounter;
    private static OperationalPressureInspection s_lastBuiltOperational;

#if UNITY_EDITOR
    // A ultima compra de cada slot, exatamente como o carrinho a viu.
    public sealed class ShoppingDecisionInspection
    {
        public int Turn;
        public int Slot;
        public ulong SceneHandle;
        public AIWorldSnapshot Snapshot;
        public List<AIShoppingDemand> Demands;
        public AIRosterKnowledge Roster;
        public CounterPressureInspection Counter;
        public OperationalPressureInspection Operational;
    }

    // O planner e DontDestroyOnLoad: o registro carrega a cena em que nasceu e e recusado em
    // outra, para a compra do mapa anterior nunca aparecer como se fosse deste.
    private static readonly Dictionary<int, ShoppingDecisionInspection> s_lastDecisionBySlot =
        new Dictionary<int, ShoppingDecisionInspection>();

    public static bool TryGetLastShoppingDecision(int slot, out ShoppingDecisionInspection decision)
    {
        if (s_lastDecisionBySlot.TryGetValue(slot, out decision) && decision != null
            && decision.SceneHandle == UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle.GetRawData())
            return true;
        decision = null;
        return false;
    }

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
        var demands = new List<AIShoppingDemand>();
        AIRosterKnowledge roster = BuildRosterKnowledge(snapshot, log);
        CounterPressureInspection counterPressure = BuildCounterPressure(snapshot, roster);
        if (log)
        {
            s_lastBuiltRoster = roster;
            s_lastBuiltCounter = counterPressure;
        }
        // Sem QG nao ha composicao de base nem operacao para equipar. As
        // perguntas reativas continuam usando os mesmos donos: claims, fila e counter.
        if (!HasShoppingBase(snapshot))
        {
            int uncovered = CountUncoveredCaptureOpportunities(snapshot);
            EnsureRoleDemand(demands, UnitRole.Capturador, uncovered, 30,
                "capture-opportunity", $"capturaveis alcancaveis sem destinatario={uncovered}");
            AppendRideQueueTransportDemand(demands, snapshot, log);
            AddCounterPressureDemands(snapshot, demands, counterPressure, roster, log);
            ApplyRideQueueCapturerBrake(snapshot, demands, log);
            demands.Sort(CompareShoppingDemandPriority);
            if (log)
                Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                    + $"sem QG: oportunidades livres={uncovered}; fila e ameaca visivel; "
                    + "sem pacote/progressao/reserva/vigilancia/rally/escolta de operacao");
            return demands;
        }

        List<TacticalDeficit> deficits = AITacticalAnalyzer.Instance.GetDeficits(
            PlayerSlotId.FromIndex(snapshot.AISlotIndex),
            log);
        SectorObjective rallyFireSupportFocus = FindRallyShoppingFocus(deficits);
        var rallyAccess = new ArtilleryWallContext();
        foreach (ConstructionManager building in snapshot.MyBuildings)
            if (building != null && building.BoardTilemap != null)
            { rallyAccess.Board = building.BoardTilemap; break; }
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
            else if (demand.Role == UnitRole.Assalto || demand.Role == UnitRole.FogoIndireto)
            {
                // A escolta da operacao (GroundCapture "risky" pede Assaultx1) segue a mesma
                // linha do pacote e do elite: ilha sem ligacao por terra, assalto e a
                // familia Assalto NO AR (Apache), fogo e a familia FdS antiaerea. Sem isto
                // o pacote pedia ar e a operacao comprava Tanque Leve pela porta do lado.
                EliteLine line = ResolveEliteLine(snapshot, demand.Role);
                if (line.CutOff)
                {
                    demand.Role = line.Role;
                    ApplyEliteLine(demand, line);
                }
            }
            if (rallyFireSupport || rallyBreakthrough)
            {
                Vector3Int rallyCell = deficit.Operation.TargetCell;
                if (AIController.Instance != null && AIController.Instance.TryGetRallyAnchorCell(
                        deficit.Operation.LinkedObjective.Sector, out Vector3Int authoredRally))
                    rallyCell = authoredRally;
                if (!ConfigureRallyShoppingAccess(snapshot, demand, rallyCell, rallyAccess, log))
                    continue;
            }
            MergeRoleDemand(demands, demand, addCounts: true);
        }

        int capturers = CountCompositionRole(snapshot, UnitRole.Capturador);
        // Assalto e fogo contam por FAMILIA (Tools > Role Families): Apache conta como assalto,
        // SAM/AAA como fogo de suporte — o pacote fecha no ar quando o solo nao serve.
        int assaults = CountCoreFamily(snapshot, UnitRoleFamily.Assalto);
        int fireSupport = CountCoreFamily(snapshot, UnitRoleFamily.FogoDeSuporte);
        int core = capturers + assaults + fireSupport;
        int packages = Mathf.Max(1, Mathf.CeilToInt(core / 5f));
        // Vaga de captura ABERTA no plano tambem e demanda de capturador. O pacote 2/2/1
        // sozinho pede 2 capturadores ate o nucleo passar de 5, nao importa quantos
        // predios ha para tomar: com setor sem capturador (Bravo vazio no playtest do
        // Claude, turno 3), a IA comprava tanque e guardava o resto com produtor parado.
        // Prioridade 30 (antes do assalto, 31): com caixa para os dois, compra os dois.
        int openCapturerSlots = CountOpenSlots(snapshot.AITeam, UnitRole.Capturador);
        int capturerDemand = Mathf.Max(Mathf.Max(0, packages * 2 - capturers), openCapturerSlots);
        EnsureRoleDemand(demands, UnitRole.Capturador, capturerDemand, 30,
            "composition", $"pacote 2/2/1 cap={capturers} ass={assaults} art={fireSupport} vagasPlano={openCapturerSlots}");
        // ILHA SEM LIGACAO POR TERRA ao inimigo (e nao invadida): o pacote continua 2 assalto +
        // 1 fogo, mas pede a versao que serve ali — assalto = ATAQUE AEREO (Apache), fogo =
        // ANTIAEREO (SAM, ou AAA, que satisfaz Antiaereo). Tanque e artilharia so seriam
        // comprados por doutrina para avancar aonde nao chegam; a defesa segue pelas demandas
        // proprias, e a ilha invadida religa o pacote terrestre (IsGroundCutOffFromEnemy).
        bool groundCutOff = IsGroundCutOffFromEnemy(snapshot);
        int assaultNeed = Mathf.Max(0, packages * 2 - assaults);
        string compositionReason = $"pacote 2/2/1 cap={capturers} ass={assaults} art={fireSupport}"
            + (groundCutOff ? " (ilha sem ligacao por terra: ar + antiaereo)" : "");
        if (assaultNeed > 0)
        {
            AIShoppingDemand assaultDemand = NewRoleDemand(
                groundCutOff ? UnitRole.AtaqueAereo : UnitRole.Assalto, assaultNeed, 31,
                "composition", compositionReason, false);
            assaultDemand.RequiredFamily = UnitRoleFamily.Assalto;
            if (groundCutOff) assaultDemand.Domain = Domain.Air;
            MergeRoleDemand(demands, assaultDemand, false);
        }
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
        // Cede a vaga so quando o counter VAI pedir compra (anel ou cobertura abaixo da
        // tolerancia). Saldo material que o counter tolera nao segura mais a artilharia.
        bool counterResponseOwnsFireSlot =
            (counterPressure.AntiInfantry >= materialCounterGap
                && CounterCategoryWantsResponse(counterPressure, WeaponCategory.AntiInfantaria))
            || (counterPressure.AntiTank >= materialCounterGap
                && CounterCategoryWantsResponse(counterPressure, WeaponCategory.AntiTanque));
        // Na ilha sem ligacao o fogo e antiaereo, e a regra do counter terrestre nao se aplica.
        int compositionFire = capturers >= massGate && (groundCutOff || !counterResponseOwnsFireSlot)
            ? Mathf.Max(0, packages - fireSupport)
            : 0;
        if (compositionFire > 0)
        {
            AIShoppingDemand fireDemand = NewRoleDemand(
                groundCutOff ? UnitRole.Antiaereo : UnitRole.FogoIndireto, compositionFire, 32,
                "composition", compositionReason, false);
            fireDemand.RequiredFamily = UnitRoleFamily.FogoDeSuporte;
            MergeRoleDemand(demands, fireDemand, false);
        }
        if (log && counterResponseOwnsFireSlot && capturers >= massGate
            && packages > fireSupport)
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + $"composition fire_support cedido ao counter: "
                + $"antiInf={counterPressure.AntiInfantry:F2} "
                + $"antiTank={counterPressure.AntiTank:F2}");

        // So a aeronave DENTRO DO ANEL gera AA urgente: o Tatico dela (onde para e onde atira
        // neste turno) toca a nossa ilha (quando ilhados) ou prédio/unidade nossa. A largura
        // do anel e da propria aeronave, nunca um numero fixo. "O oponente tem forca aerea"
        // continua com a vaga antiaerea do pacote e a Vigilancia, sem urgencia. (Peixe
        // Pequeno T1: o Chinook recem-comprado no aeroporto do outro canto custava uma AAA
        // de prioridade 8 antes de qualquer capturador.)
        var airInRingByClass = new Dictionary<GameUnitClass, int>();
        var airRearByClass = new Dictionary<GameUnitClass, int>();
        var airFrontByClass = new Dictionary<GameUnitClass, int>();
        int ringAir = CountEnemyAirInRing(snapshot, out int airSeen, airInRingByClass,
            airRearByClass, airFrontByClass);
        int visibleAir = 0;
        foreach (int n in airRearByClass.Values) visibleAir += n;
        int frontAir = 0;
        foreach (int n in airFrontByClass.Values) frontAir += n;
        if (log && airSeen > 0)
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] ameaca aerea: "
                + $"{ringAir}/{airSeen} aeronave(s) visiveis dentro do anel; retaguarda descoberta={visibleAir} "
                + $"vanguarda descoberta={frontAir} — fora do anel, sob o guarda-chuva antiaereo, e transporte "
                + "quando ha Disruptor na oferta, nao pedem nada");
        // RETAGUARDA: so a aeronave que alcanca PREDIO nosso fora do guarda-chuva pede antiaereo.
        // A bateria defende o lugar; a tropa la na frente pede caca (VANGUARDA, abaixo).
        airInRingByClass = airRearByClass;
        if (visibleAir > 0)
        {
            int activeAA = CountAtomicRole(snapshot, UnitRole.AntiaereoCombatente)
                + CountAtomicRole(snapshot, UnitRole.Antiaereo);
            int desiredAA = Mathf.Max(1, Mathf.CeilToInt(visibleAir / 2f));

            // O QUE esta no anel decide QUAL antiaereo (autor, 2026-11-07): bombardeiro pede quem
            // tem Plane em primario (SAM), Apache/Chinook pede quem tem Heli em primario (AAA).
            // A presa sai da prioridade de alvo da ficha — o papel so diz a vaga. A classe com
            // mais aeronaves descobertas no anel leva a urgente; sem nenhum antiaereo ofertado
            // que a tenha em primario, volta a regra antiga (alterna AAA/SAM pela contagem).
            // ESCADA DA PRESA: quem a tem em PRIMARIO; se ninguem vende, quem a tem em SECUNDARIO.
            // Bombardeiro no anel sem SAM a venda (so Fabrica Leve): a AAA cumpre o papel. E troca
            // hit-kill, mas a AAA ($5500) ganha a troca financeira contra o bombardeiro ($22000).
            GameUnitClass? ringClass = null;
            BazookaTargetPriority ringTier = BazookaTargetPriority.Primary;
            int ringClassMissing = 0;
            foreach (KeyValuePair<GameUnitClass, int> pair in airInRingByClass)
            {
                BazookaTargetPriority tier;
                if (OffersAntiAirFor(snapshot, pair.Key, BazookaTargetPriority.Primary))
                    tier = BazookaTargetPriority.Primary;
                else if (OffersAntiAirFor(snapshot, pair.Key, BazookaTargetPriority.Secondary))
                    tier = BazookaTargetPriority.Secondary;
                else
                    continue;
                int classMissing = Mathf.Max(1, Mathf.CeilToInt(pair.Value / 2f))
                    - CountOwnAntiAirFor(snapshot, pair.Key, tier);
                if (classMissing > ringClassMissing)
                {
                    ringClassMissing = classMissing;
                    ringClass = pair.Key;
                    ringTier = tier;
                }
            }

            if (ringClass.HasValue)
            {
                AIShoppingDemand classAA = NewRoleDemand(UnitRole.Antiaereo, 1, 8, "threat",
                    $"aeronaves visiveis={visibleAir}, {ringClass.Value} no anel={airInRingByClass[ringClass.Value]} "
                    + $"(1 urgente/turno, presa em {(ringTier == BazookaTargetPriority.Primary ? "primario" : "secundario: sem primario a venda")})", true);
                classAA.TargetClass = ringClass.Value;
                classAA.MinTargetPriority = ringTier;
                MergeRoleDemand(demands, classAA, false);
                int restMissing = desiredAA - activeAA - 1;
                if (restMissing > 0)
                    MergeRoleDemand(demands, NewRoleDemand(UnitRole.Antiaereo, restMissing, 15,
                        "threat-followup", $"aeronaves visiveis={visibleAir}, restante sem urgencia", false), false);
            }
            else if (activeAA < desiredAA)
            {
                UnitRole exact = CountAtomicRole(snapshot, UnitRole.AntiaereoCombatente)
                    <= CountAtomicRole(snapshot, UnitRole.Antiaereo)
                    ? UnitRole.AntiaereoCombatente : UnitRole.Antiaereo;
                // So alterna entre o que o mapa VENDE: com 1 AAA e nenhum SAM a venda, pedir SAM
                // urgente nunca fechava.
                UnitRole other = exact == UnitRole.Antiaereo ? UnitRole.AntiaereoCombatente : UnitRole.Antiaereo;
                if (!OffersPrimaryRole(snapshot, exact) && OffersPrimaryRole(snapshot, other))
                    exact = other;
                // ESCALADA: uma AA urgente por turno. A urgencia (+250000) passa na frente de
                // tudo; de 0 a 3 num turno so (AI Step T5 amarelo) ela comia o caixa inteiro.
                // O resto disputa como demanda comum, e volta urgente no turno seguinte se a
                // ameaca seguir no anel.
                int missing = desiredAA - activeAA;
                MergeRoleDemand(demands, NewRoleDemand(UnitRole.Antiaereo, 1, 8,
                    "threat", $"aeronaves visiveis={visibleAir} (1 urgente/turno)", true, exact), false);
                // Sem ExactRole de proposito: com o mesmo exato, o MergeRoleDemand fundiria esta
                // na urgente (Urgent |=) e as tres voltariam urgentes. Assim ela se funde com a
                // vaga antiaerea comum do pacote, se houver.
                if (missing > 1)
                    MergeRoleDemand(demands, NewRoleDemand(UnitRole.Antiaereo, missing - 1, 15,
                        "threat-followup", $"aeronaves visiveis={visibleAir}, restante sem urgencia", false), false);
            }
        }

        // VANGUARDA: aeronave inimiga que alcanca tropa nossa FORA de predio e FORA do
        // guarda-chuva antiaereo pede CACA. "Voce na frente capturando, o cara ergue um
        // bombardeiro e fica moendo suas tropas, voce ergue um SAM, e ele fica la na base...
        // onde esta o caca?" (autor, 2026-11-07). A escada e a mesma da bateria, pela presa da
        // ficha: bombardeiro (Plane) -> quem tem Plane em primario (Caca A); cadeia fechada ->
        // secundario (Caca B, que abre a cadeia do A). Apache (Heli) -> Caca B. O caca orbita o
        // capitao (o capturador), entao nasce para ir com a frente, nao para ficar na base.
        bool frontInterceptorDemanded = false;
        if (frontAir > 0 && CanAnyOwnProducerOffer(snapshot, UnitRole.Interceptador))
        {
            GameUnitClass? frontClass = null;
            BazookaTargetPriority frontTier = BazookaTargetPriority.Primary;
            int frontMissing = 0;
            foreach (KeyValuePair<GameUnitClass, int> pair in airFrontByClass)
            {
                BazookaTargetPriority tier;
                if (OffersRoleFor(snapshot, UnitRole.Interceptador, pair.Key, BazookaTargetPriority.Primary))
                    tier = BazookaTargetPriority.Primary;
                else if (OffersRoleFor(snapshot, UnitRole.Interceptador, pair.Key, BazookaTargetPriority.Secondary))
                    tier = BazookaTargetPriority.Secondary;
                else
                    continue;
                // Em pares, como o Disruptor ("concentrar, nao dividir"), com o teto da escolta.
                int wanted = Mathf.Min(EscortDemandCap, Mathf.Max(1, Mathf.CeilToInt(pair.Value / 2f)));
                int missing = wanted - CountOwnRoleFor(snapshot, UnitRole.Interceptador, pair.Key, tier);
                if (missing > frontMissing)
                {
                    frontMissing = missing;
                    frontClass = pair.Key;
                    frontTier = tier;
                }
            }
            if (frontClass.HasValue)
            {
                AIShoppingDemand fighter = NewRoleDemand(UnitRole.Interceptador, frontMissing, 12,
                    "vanguarda",
                    $"{frontClass.Value} sobre tropa fora do guarda-chuva={airFrontByClass[frontClass.Value]} "
                    + $"(presa em {(frontTier == BazookaTargetPriority.Primary ? "primario" : "secundario: primario fora de alcance")})",
                    false);
                fighter.Domain = Domain.Air;
                fighter.TargetClass = frontClass.Value;
                fighter.MinTargetPriority = frontTier;
                MergeRoleDemand(demands, fighter, false);
                frontInterceptorDemanded = true;
            }
        }

        // DISRUPTOR: o espelho do capturador no shopping. O counter enxerga CLASSE (helicoptero
        // pede antiaereo) e nao enxerga DINHEIRO; tres Chinooks inimigos carregados passavam sem
        // pedir quem os caca. Transporte inimigo visivel levando poder de captura pede Disruptor
        // em PARES ("concentrar, nao dividir": um Apache danifica, dois abatem). Sem fabrica que
        // ofereca Disruptor, nao nasce (gate inaplicavel).
        int loadedCarriers = CountEnemyCapturePowerInTransit(snapshot, out int capturePowerInTransit);
        if (loadedCarriers > 0 && CanAnyOwnProducerOffer(snapshot, UnitRole.DisruptorDeCaptura))
        {
            int desiredDisruptors = Mathf.Min(DisruptorDemandCap, 2 * Mathf.CeilToInt(loadedCarriers / 2f));
            int activeDisruptors = CountActiveDisruptors(snapshot);
            if (activeDisruptors < desiredDisruptors)
            {
                // So o Disruptor de linha (nao elite): a demanda caca TRANSPORTE em transito.
                // O elite (Bombardeiro, que tambem e Disruptor) vem pela progressao.
                AIShoppingDemand disruptor = NewRoleDemand(UnitRole.DisruptorDeCaptura,
                    desiredDisruptors - activeDisruptors, 12, "disruptor",
                    $"poder de captura inimigo em transito={capturePowerInTransit} em {loadedCarriers} "
                    + $"transporte(s); Disruptores={activeDisruptors}/{desiredDisruptors} (em pares)", false);
                disruptor.MaxEliteLevel = 0;
                // Disruptor DE LINHA: papel principal. Quem e disruptor so de secundario (Caca B,
                // Caca A) faz o servico quando sobra, mas nao ocupa a vaga — e a contagem
                // (CountActiveDisruptors) so ve o principal: sem isto a demanda aceitava o caca,
                // nao o contava, e nunca fechava.
                disruptor.ExactRole = UnitRole.DisruptorDeCaptura;
                MergeRoleDemand(demands, disruptor, false);
            }
        }

        // ESCOLTA: o espelho do espelho. O Disruptor nasce do poder de captura INIMIGO em
        // transito; a escolta nasce do NOSSO sendo mordido: Disruptor inimigo no ar e o nosso
        // comboio aereo de pe. "Caca B surge como resposta ao numero de disruptores em voo e a
        // quantidade de chinooks aliados avariados — a operacao de captura esta sendo atrasada
        // pelos insetos" (autor, 2026-11-06). Comboio e coisa que ANDA: a AA cobre o que esta
        // parado, o caca vai junto. Sem transporte aereo nosso nao ha quem escoltar; sem fabrica
        // de Interceptador, gate inaplicavel.
        int enemyDisruptorsInAir = CountEnemyAirDisruptors(snapshot);
        int ownAirTransports = CountOwnAirTransports(snapshot, out int damagedAirTransports);
        // A escolta pede o INTERCEPTADOR DE DISRUPTORES (Caca B), nao qualquer interceptador:
        // um Caca A de $26000 cacando Apache e o trabalho errado pelo preco errado. Mapa que
        // nao vende o de disruptores cai no interceptador generico (gate inaplicavel).
        bool offersAntiDisruptor = OffersPrimaryRole(snapshot, UnitRole.InterceptadorDeDisruptor);
        UnitRole escortRole = offersAntiDisruptor ? UnitRole.InterceptadorDeDisruptor : UnitRole.Interceptador;
        // Com a VANGUARDA ja pedindo caca, a escolta nao pede de novo: o Chinook e tropa nossa
        // fora de predio, entao o Apache sobre ele ja caiu la. Duas demandas (uma com classe de
        // presa, outra sem) nao se fundiriam e o caixa pagaria dois cacas pela mesma ameaca.
        if (!frontInterceptorDemanded
            && enemyDisruptorsInAir > 0 && ownAirTransports > 0
            && CanAnyOwnProducerOffer(snapshot, UnitRole.Interceptador))
        {
            int desiredEscorts = Mathf.Min(EscortDemandCap,
                Mathf.CeilToInt((enemyDisruptorsInAir + damagedAirTransports) / 2f));
            int activeEscorts = offersAntiDisruptor
                ? CountAtomicRole(snapshot, UnitRole.InterceptadorDeDisruptor)
                : CountPrimaryLabel(snapshot, UnitRole.Interceptador);
            if (activeEscorts < desiredEscorts)
            {
                AIShoppingDemand escort = NewRoleDemand(UnitRole.Interceptador,
                    desiredEscorts - activeEscorts, 12, "escolta",
                    $"disruptores inimigos no ar={enemyDisruptorsInAir}; transportes aereos nossos="
                    + $"{ownAirTransports} (avariados={damagedAirTransports}); escoltas="
                    + $"{activeEscorts}/{desiredEscorts} ({escortRole})", false);
                if (offersAntiDisruptor)
                    escort.ExactRole = UnitRole.InterceptadorDeDisruptor;
                MergeRoleDemand(demands, escort, false);
            }
        }

        foreach (AIShoppingDemand wallDemand in BuildArtilleryWallDemands(snapshot, log))
            MergeRoleDemand(demands, wallDemand, false);

        // Vigilancia aerea so quando ha O QUE ACHAR: com o terreno revelado, ver o ceu de longe
        // nao acrescenta quase nada — o que o radar ganha e DETECTAR furtivo. Entao pede:
        // nevoa total (a visao vale por si) OU o inimigo pode pôr furtivo aereo em campo
        // (um produtor dele oferece, ou ja ha um visivel). Caca/bombardeiro furtivo exigem
        // aeroporto especial que nao esta no jogo: "tem aeroporto" sozinho comprava Radar
        // Movel no T2 do Peixe Pequeno com o troco que o Apache nao cabia.
        bool airSurveillanceWorth = IsTotalFogMatch()
            || EnemyCanFieldAirStealth(snapshot);
        if (!airSurveillanceWorth
            && log
            && HasEnemyAirProduction(snapshot)
            && CountActiveDedicatedAirSurveillance(snapshot) == 0)
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + "vigilancia aerea: sem demanda — tabuleiro revelado e o inimigo nao produz furtivo aereo");
        if (airSurveillanceWorth
            && HasEnemyAirProduction(snapshot)
            && CountActiveDedicatedAirSurveillance(snapshot) == 0)
        {
            AIShoppingDemand surveillance = NewRoleDemand(
                UnitRole.Vigilancia, 1, 20,
                "vigilancia-aerea",
                "oponente possui capacidade aeroportuaria",
                false);
            surveillance.RequiredVisionDomain = Domain.Air;
            surveillance.RequiredVisionHeight = HeightLevel.AirHigh;
            MergeRoleDemand(demands, surveillance, false);
        }

        // ANTI-SUB E DEMANDA DE VIGILANCIA POR CAMADA, nao um papel proprio.
        //
        // Pedia UnitRole.RaidAntiSub, e NENHUMA ficha do projeto tem esse papel:
        // as cinco unidades de vigilancia sao `roles: Vigilancia`. A contagem
        // dava zero para sempre, entao com submarino inimigo a vista a demanda
        // disparava todo turno e nao podia ser preenchida por ninguem — gate
        // inaplicavel, que nao separa "ainda nao satisfeito" de "impossivel".
        //
        // "Encontrar submarino" e visao. "Destruir submarino" e arma. Nenhum dos
        // dois e papel.
        if (HasEnemySubmarineCapability(snapshot)
            && CountSurveillanceForLayer(
                snapshot, Domain.Submarine, HeightLevel.Submerged) == 0)
        {
            AIShoppingDemand antiSub = NewRoleDemand(
                UnitRole.Vigilancia, 1, 18,
                "anti-sub", "submarino visivel ou porto inimigo", false);
            antiSub.RequiredVisionDomain = Domain.Submarine;
            antiSub.RequiredVisionHeight = HeightLevel.Submerged;
            MergeRoleDemand(demands, antiSub, false);
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
        AddEliteProgressionDemand(snapshot, demands, UnitRole.Interceptador,
            CountCompositionRole(snapshot, UnitRole.Interceptador),
            CountTotalVisibleEnemyAircraft(snapshot) > 0 ? 23 : 25, maxEliteLevel: 1);
        AddCounterPressureDemands(snapshot, demands, counterPressure, roster, log);
        bool groundCounterPressureCovered = counterPressure.AntiTank <= 0.05f
            && counterPressure.AntiInfantry <= 0.05f;
        bool rallyAssemblyActive = HasActiveRallyAssembly(
            PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        AddEliteQualityDemand(snapshot, demands, UnitRole.Assalto, assaults, 26,
            groundCounterPressureCovered, rallyAssemblyActive);
        AddEliteQualityDemand(snapshot, demands, UnitRole.FogoIndireto, fireSupport, 27,
            groundCounterPressureCovered, rallyAssemblyActive);
        OperationalPressureInspection operational = BuildOperationalPressure(snapshot);
        if (log)
            s_lastBuiltOperational = operational;
        AddOperationalPressureDemands(snapshot, demands, operational);

        ApplyRideQueueCapturerBrake(snapshot, demands, log);
        ApplyHardModeArmorFirst(snapshot, demands);

        demands.Sort(CompareShoppingDemandPriority);
        return demands;
    }

    private static bool HasShoppingBase(AIWorldSnapshot snapshot)
        => snapshot != null && !ConstructionManager.IsHeadQuarterlessTeam(snapshot.AITeam);

    private static int CompareShoppingDemandPriority(AIShoppingDemand a, AIShoppingDemand b)
    {
        int urgent = b.Urgent.CompareTo(a.Urgent);
        if (urgent != 0) return urgent;
        int priority = a.Priority.CompareTo(b.Priority);
        return priority != 0 ? priority : ((int)a.Role).CompareTo((int)b.Role);
    }

    private static int CountUncoveredCaptureOpportunities(AIWorldSnapshot snapshot)
    {
        var candidates = new List<ConstructionManager>();
        if (snapshot.NeutralBuildings != null) candidates.AddRange(snapshot.NeutralBuildings);
        if (snapshot.EnemyBuildings != null) candidates.AddRange(snapshot.EnemyBuildings);
        // A mesma consulta de acesso por ficha/produtora ja usada pelo rally.
        // Aqui so importa o componente conectado, nao a direcao/custo da marcha.
        // Ancora na produtora: uma varredura por ficha/origem, nao por predio do mapa.
        var access = new ArtilleryWallContext();
        bool CanReachAndCapture(ConstructionManager target)
        {
            if (!target.TryResolveConstructionData(out ConstructionData targetData)) return false;
            foreach (ConstructionManager producer in snapshot.MyBuildings)
            {
                if (producer == null || !producer.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                    || producer.OfferedUnits == null || producer.BoardTilemap == null) continue;
                access.Board = producer.BoardTilemap;
                access.Targets[0].Clear();
                access.Targets[0].Add(producer.CurrentCellPosition);
                foreach (UnitData data in producer.OfferedUnits)
                    if (data != null && UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Capturador
                        && IsEliteChainAvailable(data, snapshot)
                        && IsRolePurchaseAllowed(data, snapshot.Stance, false)
                        && PodeCapturarSensor.HasCaptureKeyFor(data, targetData)
                        && access.Reaches(data, target.CurrentCellPosition, 0)) return true;
            }
            return false;
        }
        return CaptureOpportunityClaimService.CountUnclaimedOpportunities(
            PlayerSlotId.FromIndex(snapshot.AISlotIndex), candidates, CanReachAndCapture);
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
        // Pergunta a capacidade, nao a dificuldade: e a mesma chave que a poupanca do
        // 1o MBT (ComputeBlitzFirstArmorReserve) ja le. Com HardMode aqui, desligar
        // "abertura blindado primeiro" no perfil apagava so metade da regra.
        if (AIController.Instance == null || !AIController.Instance.AbreComBlindado)
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

    private static bool ConfigureRallyShoppingAccess(AIWorldSnapshot snapshot,
        AIShoppingDemand demand, Vector3Int rallyCell, ArtilleryWallContext access, bool log)
    {
        access.Targets[0].Clear();
        rallyCell.z = 0;
        access.Targets[0].Add(rallyCell);
        Dictionary<string, HashSet<int>> Collect()
        {
            var offers = new Dictionary<string, HashSet<int>>();
            foreach (ConstructionManager producer in snapshot.MyBuildings)
            {
                if (producer == null || !producer.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                    || producer.OfferedUnits == null) continue;
                foreach (UnitData data in producer.OfferedUnits)
                {
                    if (data == null || data.cost <= 0 || !IsEliteChainAvailable(data, snapshot)
                        || !IsRolePurchaseAllowed(data, snapshot.Stance, false)
                        || !DoesUnitMeetShoppingDemand(data, demand)
                        || !access.Reaches(data, producer.CurrentCellPosition, 0)) continue;
                    if (!offers.TryGetValue(data.id, out var producers))
                        offers[data.id] = producers = new HashSet<int>();
                    producers.Add(producer.InstanceId);
                }
            }
            return offers;
        }
        var eligible = Collect();
        if (eligible.Count == 0 && demand.RequireRallyBreakthrough)
        {
            // The assault family can form in the air; a disconnected tank
            // cannot fulfill this rally just because its producer is idle.
            demand.RequireRallyBreakthrough = false;
            demand.Role = UnitRole.AtaqueAereo;
            demand.RequiredFamily = UnitRoleFamily.Assalto;
            demand.Domain = Domain.Air;
            demand.Reason += " (sem ruptura terrestre ofertada com acesso; assalto aereo)";
            eligible = Collect();
        }
        demand.RallyResponseOffers = eligible;
        if (log)
            Debug.Log($"[AI Shopping Rally] alvo={rallyCell} {FormatDemandCapability(demand)} "
                + (eligible.Count > 0 ? $"ofertas com acesso={eligible.Count}" : "sem oferta com acesso; nao compra peca encalhada"));
        return eligible.Count > 0;
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
        demand.IsConcreteDefense = deficit.Operation != null && deficit.Operation.Type == AITacticalNeedType.SectorDefense;
        demand.Domain = domain;
        demand.MinEliteLevel = minElite;
        demand.MaxEliteLevel = maxElite;
        return true;
    }

    /// <summary>
    /// Demanda de transporte DERIVADA da fila de carona.
    ///
    /// Nao e uma constante e nao depende de plano: le quantos passageiros estao
    /// esperando carona agora e desconta os transportadores que o time ja tem.
    /// Zero esperando (ou frota suficiente) = nenhuma demanda, e a linha some do
    /// log sozinha.
    ///
    /// A antiguidade da fila vira PRIORIDADE: quem espera ha mais tempo empurra a
    /// compra para cima. E o mesmo sinal que o QueroCarona/RideWait ja usa para
    /// ordenar coleta — aqui ele so atravessa para o shopping.
    ///
    /// Sem QG esta porta le a fila sem exigir slot formal. A IA com plano tem os seus dois
    /// sistemas de demanda de transporte (ambos gateados por slot de plano); ligar
    /// este terceiro sinal la exige reconcilia-los primeiro, e isso e frente
    /// propria — ver project_dual_transport_demand.
    /// </summary>
    private static void AppendRideQueueTransportDemand(
        List<AIShoppingDemand> demands, AIWorldSnapshot snapshot, bool log)
    {
        if (demands == null || snapshot?.MyUnits == null) return;

        // A fila e a mesma leitura do shopping operacional e do painel de
        // transporte, inclusive para passageiro sem plano. Nao recontar flags
        // de espera: elas podem carregar o retrato de uma decisao anterior.
        PressaoCaronaLeitura queue = GetShoppingRideQueue(snapshot);
        if (queue.AirCongested)
        {
            var pressure = new OperationalPressureInspection { RideQueue = queue,
                RideSeatsPerAirBus = ResolveCheapestAirTransportSeats(snapshot) };
            AddOperationalPressureDemands(snapshot, demands, pressure);
        }
        int waiting = queue.Pedidos.FindAll(p => !p.Result.isStranded && p.Categoria != PressaoCaronaCategoria.Emergencia).Count;
        int transporters = queue.Buses.FindAll(b => !b.Air && b.Operational).Count;
        int oldestWait = 0;
        foreach (PressaoCaronaPedido pedido in queue.Pedidos)
            if (pedido.Unit != null)
                oldestWait = Mathf.Max(oldestWait,
                    pedido.Unit.ResolveAIRideWaitTurns(snapshot.TurnNumber));

        int gap = waiting - transporters;
        if (gap <= 0)
        {
            if (log && waiting > 0)
                Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                    + $"fila de carona: {waiting} esperando e {transporters} transportador(es) "
                    + "em campo — sem demanda de transporte.");
            return;
        }

        // Prioridade sobe com a espera. Base abaixo do capturador (30): o rogue
        // ainda prefere mais capturador com a primeira nota. Passou de tres turnos
        // na fila, a carona vira o gargalo e passa na frente.
        int priority = oldestWait >= 3 ? 34 : 26;

        var groundRide = NewRoleDemand(UnitRole.Transportador, gap, priority, "ride-queue-land",
            $"fila terrestre: {waiting} esperando, {transporters} em campo", false);
        groundRide.Domain = Domain.Land;
        MergeRoleDemand(demands, groundRide, false);

        if (log)
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + $"fila de carona pede Transportador x{gap} pri={priority} "
                + $"(esperando={waiting} emCampo={transporters} esperaMaisAntiga={oldestWait}).");
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
            if (current.ArtilleryWallGroup != incoming.ArtilleryWallGroup) continue;
            if (current.IsPreventiveAirControl != incoming.IsPreventiveAirControl) continue;
            if (current.IsConcreteDefense != incoming.IsConcreteDefense
                || current.IsRideQueueReinforcement != incoming.IsRideQueueReinforcement) continue;
            if (current.RallyResponseOffers != null || incoming.RallyResponseOffers != null) continue;
            if (current.Role != incoming.Role || current.ExactRole != incoming.ExactRole
                || current.RequiredFamily != incoming.RequiredFamily
                || current.Domain != incoming.Domain
                || current.RequiredVisionDomain != incoming.RequiredVisionDomain
                || current.RequiredVisionHeight != incoming.RequiredVisionHeight
                || current.MinEliteLevel != incoming.MinEliteLevel
                || current.MaxEliteLevel != incoming.MaxEliteLevel
                || !Mathf.Approximately(current.MinRallyArtilleryWeight,
                    incoming.MinRallyArtilleryWeight)
                || current.RequireRallyBreakthrough != incoming.RequireRallyBreakthrough
                || current.ExcludeLandResponse != incoming.ExcludeLandResponse
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
        if (demand.ArtilleryWallGroup != 0
            && (demand.WallResponseOffers == null || !demand.WallResponseOffers.ContainsKey(unit.id)))
            return false;
        if (!string.IsNullOrEmpty(demand.RequiredUnitId)
            && !string.Equals(unit.id, demand.RequiredUnitId,
                System.StringComparison.Ordinal))
            return false;
        if (demand.Domain.HasValue && unit.domain != demand.Domain.Value)
            return false;
        if (demand.ExcludeLandResponse && unit.domain == Domain.Land)
            return false;
        if (demand.RequiredVisionDomain.HasValue
            || demand.RequiredVisionHeight.HasValue)
        {
            VisionCoverageLayer principal =
                VisionCoverageLayerResolver.ResolvePrincipal(unit);
            if (principal.IsAll)
                return false;
            if (demand.RequiredVisionDomain.HasValue
                && principal.Domain != demand.RequiredVisionDomain.Value)
                return false;
            if (demand.RequiredVisionHeight.HasValue
                && principal.Height != demand.RequiredVisionHeight.Value)
                return false;
        }
        if (unit.eliteLevel < demand.MinEliteLevel || unit.eliteLevel > demand.MaxEliteLevel)
            return false;
        if (demand.ExactRole != UnitRole.None && unit.roles[0] != demand.ExactRole)
            return false;
        if (demand.RequiredFamily.HasValue
            && UnitRoleCompatibility.ResolveFamily(unit) != demand.RequiredFamily.Value)
            return false;
        if (demand.RequiredWeaponCategory.HasValue
            && (!IsCounterResponseRoleAllowed(unit, demand.RequiredWeaponCategory.Value)
                || !HasWeaponCategory(unit, demand.RequiredWeaponCategory.Value)))
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
        if (demand.RequiredVisionDomain.HasValue)
        {
            label += $"+visao:{demand.RequiredVisionDomain.Value}";
            if (demand.RequiredVisionHeight.HasValue)
                label += $"/{demand.RequiredVisionHeight.Value}";
        }
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
        // ganhar do capturador AGRESSIVO (CapturadorCombatente, ex.: Machine Gunner), que só captura a
        // 50% e é quebra-galho. Sem isso, CanSatisfy/ResolveCompositionRole achatam os dois em
        // "Capturador" e o viés de custo (cost/2) escolhe o agressivo por ser mais caro — torrando o
        // caixa da expansão em unidades de combate. A penalidade domina qualquer diferença de custo,
        // mas como coverage vem antes de QualityScore no desempate do carrinho, o agressivo ainda entra
        // como FALLBACK quando nenhum capturador dedicado está ofertado pro slot. Demandas de combate
        // (Assalto/anti-infantaria) seguem inalteradas: lá o agressivo é um corpo de combate legítimo.
        if (demand.Role == UnitRole.Capturador && IsPrimaryRole(unit, UnitRole.CapturadorCombatente))
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
            // Massa do recrutamento forçado e SEMPRE elite 0 do Exercito ("onde tiver
            // soldado"): aeroporto/porto nao viram fabrica de massa cara — o
            // elite comprometido (que pode ser aereo) continua no fluxo proprio.
            if (u == null || u.cost <= 0 || u.cost > remaining
                || u.militaryForce != MilitaryForce.Army || u.eliteLevel != 0
                || IsExpansionCapturerBlocked(snapshot, u)
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
        // TETO COM FOLGA: a conscricao cessa cedo (ex.: 50% do limite) para sobrar manobra para
        // o elite entrar — com o teto em 60 e a valvula em 85% ela so parava em 51 unidades, e
        // imposto + reserva seguravam o elite a partida toda (autor, 2026-11-06). Histerese:
        // so volta abaixo do piso de saida.
        int unitCap = ai.Match != null ? ai.Match.MaxUnitsPerTeam : 0;
        float capRatio = wasActive ? ai.MassacreUnitCapExitFillRatio : ai.MassacreUnitCapFillRatio;
        // Conta COMO O TETO CONTA: o limite do jogo inclui embarcados, o snapshot.MyUnits nao.
        // Com a festa da carona (4 transportes lotados) a valvula via 2x-3x menos gente que o
        // HUD e a conscricao seguia acima do teto do autor (Peixe Pequeno T7: HUD 32, teto 30).
        int unitsInField = snapshot.MyUnits.Count;
        if (ai.Match != null)
            ai.Match.GetSlotUnitCounts(snapshot.AISlotIndex, out unitsInField, out _);
        bool byCap = unitCap > 0 && unitsInField
            >= Mathf.CeilToInt(unitCap * capRatio);
        // DEFESA so anula a vantagem por ratio, nunca o teto: base ameacada pede corpo, mas o limite
        // de massa continua absoluto. Mesmos sinais do shopping (postura Defensiva, Perdendo).
        bool defense = snapshot.Stance == AIStance.Defensive || macro.Losing;
        bool active = byCap || (byRatio && !defense);
        if (byCap && defense)
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] TETO DE CONSCRIÇÃO mantido em defesa ({unitsInField}/{unitCap})");
        ai.MassacrePhaseActive = active;
        if (active != wasActive)
            Debug.Log($"[AI Shopping Roles][T{snapshot.TurnNumber}][{snapshot.AITeam}] "
                + (active
                    // Dois gatilhos, uma consequencia (conscricao desliga), nomes diferentes:
                    // massacre e a vantagem de 2:1; o teto e so o exercito cheio.
                    ? (byRatio && !defense
                        ? $"FASE DE MASSACRE (vantagem {macro.ForceRatio:P0} >= {ai.MassacreEnterForceRatio:P0}): conscrição cessa "
                        : $"TETO DE CONSCRIÇÃO ({unitsInField}/{unitCap} >= {ai.MassacreUnitCapFillRatio:P0}): conscrição cessa ")
                        + $"(fr={macro.ForceRatio:P0} unidades={unitsInField}/{unitCap}) — caixa vira elite"
                    : $"conscrição reativada (fr={macro.ForceRatio:P0} unidades={unitsInField}/{unitCap}"
                        + (defense ? ", defesa" : "")
                        + ")"));
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
        return ComputeConscriptionTax(snapshot, occupied, null);
    }

    // shares (opcional): a parcela de CADA produtor taxado. O carrinho devolve a parcela do
    // produtor que ele mesmo usar — sem isso o corpo comprado pelo carrinho num produtor
    // taxado era pago duas vezes (o imposto ja o garantia). AI Step T3 amarelo: 3 Soldados
    // do carrinho nos produtores taxados + $3000 de imposto parados.
    private static int ComputeConscriptionTax(
        AIWorldSnapshot snapshot, HashSet<Vector3Int> occupied,
        Dictionary<ConstructionManager, int> shares)
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
                // Massa e corpo NAO-elite. A fabrica pesada que so vende MBT/Obuseiro/SAM nao
                // tem massa para garantir: taxa-la pelo SAM (12k) reservava um elite que
                // ninguem comprava e zerava o carrinho (Peixe Pequeno T1: imposto 16k contra
                // caixa 12,5k, Chinook e Apache barrados, 8,5k parados).
                if (u == null || u.cost <= 0 || u.militaryForce != MilitaryForce.Army
                    || u.eliteLevel != 0
                    || IsExpansionCapturerBlocked(snapshot, u)
                    || !IsRolePurchaseAllowed(u, snapshot.Stance, emergency: true))
                    continue;
                if (cheapest == 0 || u.cost < cheapest)
                    cheapest = u.cost;
            }
            tax += cheapest;
            if (shares != null && cheapest > 0)
                shares[building] = cheapest;
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
                    || (IsExpansionCapturerBlocked(snapshot, u) && !HasAnyVisibleEnemyNearOwnedBase(snapshot, DefensiveBaseThreatRange))
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
            && AIController.Instance != null && AIController.Instance.RespeitaListaBanida)
            return true;
        foreach (UnitManager owned in snapshot.MyUnits)
            if (owned != null && owned.TryGetUnitData(out UnitData data) && data == unit.eliteFrom)
                return true;
        return false;
    }

    // ───────────────────────────────────────── linha do elite ──
    // Qual unidade e "o elite de Assalto/Fogo" nesta ilha. Ligada por terra: a composicao
    // antiga, inalterada (Tanque de Batalha, Obuseiro Movel...). Ilha SEM LIGACAO (e nao
    // invadida): a mesma troca do pacote 2/2/1 — assalto vira FAMILIA Assalto NO AR
    // (Bombardeiro, vindo do Apache), fogo vira FAMILIA Fogo de Suporte ANTIAEREA (SAM).
    // Baixar o score do MBT nao bastaria: pela composicao, so tanque e "Assalto", e ele
    // seria o unico candidato — comprado ilhado do mesmo jeito.
    private struct EliteLine
    {
        public UnitRole Role;
        public UnitRoleFamily? Family;
        public Domain? Domain;
        public bool CutOff;
    }

    // forElite: a pergunta e "qual e o ELITE desta linha?". Na ilha cortada o assalto no ar
    // e o Disruptor (Apache). O elite dele e quem a FICHA declara Disruptor — o Bombardeiro,
    // com DisruptorDeCaptura como papel secundario: "bombardeiros sao excelentes disruptores,
    // mas brilham contra MBT e artilharia" (autor, 2026-11-06), e a progressao dele e o que
    // obriga o inimigo a comprar caca. Ficha elite sem o rotulo nao entra. A escolta das
    // operacoes (forElite=false) segue pedindo AtaqueAereo. A poupanca deste elite cede a
    // demanda de Disruptor (poder de captura inimigo em transito).
    private static EliteLine ResolveEliteLine(AIWorldSnapshot snapshot, UnitRole role, bool forElite = false)
    {
        if (!IsGroundCutOffFromEnemy(snapshot))
            return new EliteLine { Role = role };
        if (role == UnitRole.Assalto)
            return new EliteLine
            {
                Role = forElite ? UnitRole.DisruptorDeCaptura : UnitRole.AtaqueAereo,
                Family = UnitRoleFamily.Assalto,
                Domain = global::Domain.Air,
                CutOff = true
            };
        if (role == UnitRole.FogoIndireto)
            return new EliteLine { Role = UnitRole.Antiaereo, Family = UnitRoleFamily.FogoDeSuporte, CutOff = true };
        return new EliteLine { Role = role };
    }

    private static bool CountsForEliteLine(UnitData data, EliteLine line)
    {
        if (data == null)
            return false;
        if (!line.Family.HasValue)
            return UnitRoleCompatibility.ResolveCompositionRole(data) == line.Role;
        return UnitRoleCompatibility.ResolveFamily(data) == line.Family.Value
            && (!line.Domain.HasValue || data.domain == line.Domain.Value)
            && UnitRoleCompatibility.CanSatisfy(data, line.Role);
    }

    private static void ApplyEliteLine(AIShoppingDemand demand, EliteLine line)
    {
        demand.RequiredFamily = line.Family;
        demand.Domain = line.Domain;
        if (line.CutOff)
            demand.Reason += " (ilha sem ligacao por terra)";
    }

    private static void AddEliteProgressionDemand(AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands, UnitRole role, int activeRoleCount, int priority,
        int maxEliteLevel = int.MaxValue)
    {
        if (activeRoleCount <= 0
            || (role == UnitRole.Interceptador && !HasEnemyAviationForAirControl(snapshot)))
            return;
        EliteLine line = ResolveEliteLine(snapshot, role, forElite: true);
        int currentElite = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && unit.TryGetUnitData(out UnitData data)
                && CountsForEliteLine(data, line))
                currentElite = Mathf.Max(currentElite, data.eliteLevel);

        int nextElite = int.MaxValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || building.OfferedUnits == null) continue;
            foreach (UnitData offered in building.OfferedUnits)
            {
                if (offered == null || offered.eliteLevel <= currentElite
                    || offered.eliteLevel > maxEliteLevel
                    || !CountsForEliteLine(offered, line)
                    || !IsEliteChainAvailable(offered, snapshot))
                    continue;
                nextElite = Mathf.Min(nextElite, offered.eliteLevel);
            }
        }
        if (nextElite == int.MaxValue)
            return;

        AIShoppingDemand elite = NewRoleDemand(line.Role, 1, priority, "elite",
            $"massa={snapshot.MyUnits.Count} caixa={snapshot.Budget} nivel={currentElite}->{nextElite}"
                + (role == UnitRole.Interceptador ? " controle do ar" : ""), false);
        elite.MinEliteLevel = nextElite;
        elite.MaxEliteLevel = nextElite;
        elite.IsPreventiveAirControl = role == UnitRole.Interceptador;
        ApplyEliteLine(elite, line);

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
        EliteLine line = ResolveEliteLine(snapshot, role, forElite: true);

        int currentElite = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead
                && unit.TryGetUnitData(out UnitData data) && data != null
                && data.eliteLevel > 0
                && CountsForEliteLine(data, line))
                currentElite++;

        int alreadyDemanded = 0;
        foreach (AIShoppingDemand demand in demands)
            if (demand != null && demand.Count > 0 && demand.Role == line.Role
                && demand.MinEliteLevel > 0)
                alreadyDemanded += demand.Count;

        int missing = Mathf.Max(0, desiredElite - currentElite - alreadyDemanded);
        if (missing <= 0)
            return;

        AIShoppingDemand quality = NewRoleDemand(
            line.Role,
            missing,
            priority,
            "elite-quality",
            $"qualidade elite={currentElite}/{desiredElite} role={activeRoleCount}"
                + $" ratio={targetRatio:P0} countersCobertos={groundCounterPressureCovered}"
                + $" rallyAtivo={rallyAssemblyActive}",
            false);
        quality.MinEliteLevel = 1;
        ApplyEliteLine(quality, line);

        int saveTurns = AIController.Instance != null ? AIController.Instance.EliteSaveTurns : 0;
        int reach = snapshot.Budget
            + Mathf.Max(0, snapshot.IncomePerTurn) * Mathf.Max(0, saveTurns);
        int eliteCost = FindCheapestBuildableCost(snapshot, quality);
        if (eliteCost <= 0 || eliteCost > reach)
            return;

        MergeRoleDemand(demands, quality, false);
    }

    // ───────────────────────────────────────── familias do pacote / nucleo ──
    // O pacote 2/2/1 e o nucleo minimo pedem FAMILIA, nao dominio: "2 da familia Assalto + 1
    // da familia Fogo de Suporte" (UnitRoleCompatibility.ResolveFamily, Tools > Role Families).
    // No solo isso e tanque e artilharia; na ilha sem ligacao por terra, Apache e SAM/AAA.
    // O Feijao Torto (so solo) fecha o pacote como sempre; o Peixe Pequeno fecha pelo ar e
    // pelo antiaereo, sem forcar tanque onde ele nao chega (decisao do autor, 2026-11-05).
    // So o pacote e o nucleo contam por familia; o resto do shopping segue por rotulo.
    private static int CountCoreFamily(AIWorldSnapshot snapshot, UnitRoleFamily family)
    {
        int count = 0;
        if (snapshot?.MyUnits == null) return 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && unit.TryGetUnitData(out UnitData data)
                && UnitRoleCompatibility.ResolveFamily(data) == family)
                count++;
        return count;
    }
    private static int CountCompositionRole(AIWorldSnapshot snapshot, UnitRole role)
    {
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && unit.TryGetUnitData(out UnitData data)
                && (role == UnitRole.Logistica
                    || role == UnitRole.Vigilancia
                    ? UnitRoleCompatibility.CanSatisfy(data, role)
                    : UnitRoleCompatibility.ResolveCompositionRole(data) == role))
                count++;
        return count;
    }

    /// <summary>
    /// Vigilancia que cobre ESTA camada.
    ///
    /// Contar so o papel nao serve para demanda de visao: um EWACS e um
    /// submarino sao os dois `Vigilancia`, e um nao substitui o outro. A camada
    /// principal sai da ficha, pelo mesmo resolvedor que o MelhorVisao usa —
    /// nenhuma unidade e reconhecida por nome aqui.
    /// </summary>
    private static int CountSurveillanceForLayer(
        AIWorldSnapshot snapshot,
        Domain domain,
        HeightLevel height)
    {
        if (snapshot?.MyUnits == null)
            return 0;

        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;
            if (!UnitRoleCompatibility.CanSatisfy(data, UnitRole.Vigilancia))
                continue;

            VisionCoverageLayer principal =
                VisionCoverageLayerResolver.ResolvePrincipal(data);
            if (!principal.IsAll
                && principal.Domain == domain
                && principal.Height == height)
            {
                count++;
            }
        }
        return count;
    }

    // Como CountAtomicRole, mas aceita sub-rotulo (MatchesLabel): Interceptador conta tambem
    // o InterceptadorDeDisruptor.
    private static int CountPrimaryLabel(AIWorldSnapshot snapshot, UnitRole role)
    {
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && unit.TryGetUnitData(out UnitData data)
                && data.roles != null && data.roles.Count > 0
                && UnitRoleCompatibility.MatchesLabel(data.roles[0], role))
                count++;
        return count;
    }

    // "Vende" para a DEMANDA tem de ser o mesmo "vende" do CARRINHO: a ficha esta na prateleira
    // E a cadeia de elite esta aberta. Sem isto a demanda nascia pedindo SAM com nenhuma AAA em
    // campo (ou Caca A sem Caca B), o carrinho descartava a ficha (IsEliteChainAvailable) e a
    // demanda nunca fechava — urgente, segurando o caixa. Com o filtro, a escada da presa desce
    // ao secundario, compra o elo de baixo, e o elo de baixo abre a cadeia do de cima.
    private static bool IsOfferBuyableNow(UnitData unit, AIWorldSnapshot snapshot)
        => unit != null && IsEliteChainAvailable(unit, snapshot);

    // Algum produtor nosso vende uma ficha cujo papel PRINCIPAL e este (nao so secundario)?
    private static bool OffersPrimaryRole(AIWorldSnapshot snapshot, UnitRole role)
    {
        if (snapshot?.MyBuildings == null)
            return false;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                || building.OfferedUnits == null)
                continue;
            foreach (UnitData unit in building.OfferedUnits)
                if (unit != null && unit.roles != null && unit.roles.Count > 0 && unit.roles[0] == role
                    && IsOfferBuyableNow(unit, snapshot))
                    return true;
        }
        return false;
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

    // Teto da demanda de Disruptor: dois pares.
    private const int DisruptorDemandCap = 4;

    // Teto da escolta: caca custa o dobro do Apache; um par e meio ja cobre o enxame.
    private const int EscortDemandCap = 3;

    // Disruptores inimigos aereos visiveis (o rotulo primario, nao o secundario do Bombardeiro:
    // ele nega a escolta no chao, nao morde o comboio no ar).
    private static int CountEnemyAirDisruptors(AIWorldSnapshot snapshot)
    {
        int count = 0;
        if (snapshot?.EnemyUnits == null)
            return 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
            if (enemy != null && !enemy.IsDead && enemy.TryGetUnitData(out UnitData data) && data != null
                && data.IsAircraft() && data.roles != null && data.roles.Count > 0
                && data.roles[0] == UnitRole.DisruptorDeCaptura)
                count++;
        return count;
    }

    // Nossos transportes aereos (o comboio a escoltar) e quantos estao avariados.
    private static int CountOwnAirTransports(AIWorldSnapshot snapshot, out int damaged)
    {
        damaged = 0;
        int count = 0;
        if (snapshot?.MyUnits == null)
            return 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || !unit.TryGetUnitData(out UnitData data) || data == null
                || !data.isTransporter || !data.IsAircraft())
                continue;
            count++;
            if (data.maxHP > 0 && unit.CurrentHP < data.maxHP)
                damaged++;
        }
        return count;
    }

    // Transportes inimigos visiveis (no snapshot) levando poder de captura a bordo. O proprio
    // poder do transportador nao conta: a pergunta e "quanto dinheiro esta em transito".
    // Carga de unidade detectada e inspecao livre do jogador: nao e trapaca de nevoa.
    private static int CountEnemyCapturePowerInTransit(AIWorldSnapshot snapshot, out int capturePower)
    {
        capturePower = 0;
        int carriers = 0;
        if (snapshot?.EnemyUnits == null)
            return 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked)
                continue;
            int aboard = PodeCapturarSensor.GetCombinedCapturePower(enemy)
                - (PodeCapturarSensor.HasAnyCaptureKey(enemy) ? PodeCapturarSensor.GetCapturePower(enemy) : 0);
            if (aboard <= 0)
                continue;
            carriers++;
            capturePower += aboard;
        }
        return carriers;
    }

    private static int CountActiveDisruptors(AIWorldSnapshot snapshot)
    {
        int count = 0;
        if (snapshot?.MyUnits == null)
            return 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && unit.TryGetUnitData(out UnitData data) && data != null
                && data.roles != null && data.roles.Count > 0
                && data.roles[0] == UnitRole.DisruptorDeCaptura)
                count++;
        return count;
    }

    // Custo de atender a demanda: a oferta mais barata que a satisfaz, vezes a contagem.
    // Sem oferta, zero (nada a liberar).
    private static int EstimateDemandCost(AIWorldSnapshot snapshot, AIShoppingDemand demand)
    {
        if (snapshot?.MyBuildings == null || demand == null || demand.Count <= 0)
            return 0;
        int cheapest = int.MaxValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                || building.OfferedUnits == null)
                continue;
            // O mesmo filtro do carrinho (DoesUnitMeetShoppingDemand + cadeia): so CanSatisfy pelo
            // papel estimava o Caca A (Plane em primario, $26000) pelo preco do Caca B ($16000), e a
            // poupanca rompia/segurava o valor errado.
            foreach (UnitData unit in building.OfferedUnits)
                if (unit != null && unit.cost > 0 && unit.cost < cheapest
                    && DoesUnitMeetShoppingDemand(unit, demand)
                    && IsOfferBuyableNow(unit, snapshot))
                    cheapest = unit.cost;
        }
        return cheapest == int.MaxValue ? 0 : cheapest * demand.Count;
    }

    private static bool CanAnyOwnProducerOffer(AIWorldSnapshot snapshot, UnitRole role)
    {
        if (snapshot?.MyBuildings == null)
            return false;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                || building.OfferedUnits == null)
                continue;
            foreach (UnitData unit in building.OfferedUnits)
                if (unit != null && UnitRoleCompatibility.CanSatisfy(unit, role)
                    && IsOfferBuyableNow(unit, snapshot))
                    return true;
        }
        return false;
    }

    // Algum produtor nosso vende antiaereo (CanSatisfy Antiaereo) com esta classe em pelo
    // menos minTier de prioridade de alvo?
    private static bool OffersAntiAirFor(AIWorldSnapshot snapshot, GameUnitClass targetClass,
        BazookaTargetPriority minTier)
        => OffersRoleFor(snapshot, UnitRole.Antiaereo, targetClass, minTier);

    private static int CountOwnAntiAirFor(AIWorldSnapshot snapshot, GameUnitClass targetClass,
        BazookaTargetPriority minTier)
        => CountOwnRoleFor(snapshot, UnitRole.Antiaereo, targetClass, minTier);

    // A ESCADA DA PRESA, para qualquer papel: algum produtor nosso vende (e a cadeia de elite
    // deixa comprar) uma ficha deste papel com esta classe em pelo menos minTier?
    private static bool OffersRoleFor(AIWorldSnapshot snapshot, UnitRole role,
        GameUnitClass targetClass, BazookaTargetPriority minTier)
    {
        if (snapshot?.MyBuildings == null)
            return false;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                || building.OfferedUnits == null)
                continue;
            foreach (UnitData unit in building.OfferedUnits)
                if (unit != null && UnitRoleCompatibility.CanSatisfy(unit, role)
                    && (int)unit.ResolveAiTargetPriorityForTargetClass(targetClass) >= (int)minTier
                    && IsOfferBuyableNow(unit, snapshot))
                    return true;
        }
        return false;
    }

    // Pecas nossas em campo deste papel com esta classe em pelo menos minTier.
    private static int CountOwnRoleFor(AIWorldSnapshot snapshot, UnitRole role,
        GameUnitClass targetClass, BazookaTargetPriority minTier)
    {
        int count = 0;
        if (snapshot?.MyUnits == null)
            return 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && unit.TryGetUnitData(out UnitData data) && data != null
                && UnitRoleCompatibility.CanSatisfy(data, role)
                && (int)data.ResolveAiTargetPriorityForTargetClass(targetClass) >= (int)minTier)
                count++;
        return count;
    }

    private static int CountEnemyAirInRing(AIWorldSnapshot snapshot, out int seen)
        => CountEnemyAirInRing(snapshot, out seen, null);

    private static int CountEnemyAirInRing(AIWorldSnapshot snapshot, out int seen,
        Dictionary<GameUnitClass, int> byClass)
        => CountEnemyAirInRing(snapshot, out seen, byClass, null, null);

    // byClass (opcional): quantas aeronaves NO ANEL por classe (Heli, Plane, Jet) — para o
    // antiaereo do anel pedir a peca que tem aquela presa em primario.
    //
    // RETAGUARDA x VANGUARDA (autor, 2026-11-07): "voce ergue um SAM e ele fica la na base...
    // onde esta o caca?". O anel dizia SE a aeronave toca algo nosso; agora diz O QUE ela toca,
    // e se aquilo ja esta debaixo do GUARDA-CHUVA (celulas que um antiaereo nosso acerta no
    // proximo turno, por classe de presa):
    //   rearByClass   toca PREDIO nosso (ou unidade em cima dele) fora do guarda-chuva
    //                 -> antiaereo, que defende o LUGAR
    //   frontByClass  toca UNIDADE nossa fora de predio e fora do guarda-chuva
    //                 -> caca, que vai JUNTO com a tropa
    // Coberto nao pede nada: o guarda-chuva e o teto natural da bateria — o predio coberto
    // deixa de pedir, e a base nao lota de SAM (cada bateria a mais tambem e convite para o
    // bombardeiro, cuja presa primaria e Artillery, a classe da AAA e do SAM).
    private static int CountEnemyAirInRing(AIWorldSnapshot snapshot, out int seen,
        Dictionary<GameUnitClass, int> byClass,
        Dictionary<GameUnitClass, int> rearByClass,
        Dictionary<GameUnitClass, int> frontByClass)
    {
        seen = 0;
        int inRing = 0;
        if (snapshot?.EnemyUnits == null)
            return 0;

        AIController ai = AIController.Instance;
        UnityEngine.Tilemaps.Tilemap board = ai != null ? ai.BoardTilemapRef : null;
        OwnLandProbe island = IsGroundCutOffFromEnemy(snapshot, log: false) ? BuildOwnLandProbe(snapshot) : null;
        if (island != null && island.Island == null && island.Reach == null)
            island = null;

        // UMA AMEACA, UMA RESPOSTA. A ameaca do transporte e o poder de captura que ele leva, e
        // a resposta a isso e o Disruptor (demanda propria, "disruptor"). Contar o Chinook aqui
        // tambem pedia AAA para o mesmo alvo (AI Step T5 amarelo: Apache + 3 AAA pelos mesmos tres
        // Chinooks). Sem Disruptor na oferta, a AA volta a ser a unica resposta e ele conta.
        bool disruptorAnswersTransports = CanAnyOwnProducerOffer(snapshot, UnitRole.DisruptorDeCaptura);

        var ours = new HashSet<Vector3Int>();
        var ourBuildings = new HashSet<Vector3Int>();
        var ourUnitsOffBuilding = new HashSet<Vector3Int>();
        if (snapshot.MyBuildings != null)
            foreach (ConstructionManager b in snapshot.MyBuildings)
                if (b != null) { Vector3Int c = b.CurrentCellPosition; c.z = 0; ours.Add(c); ourBuildings.Add(c); }
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
                if (u != null && !u.IsDead) { Vector3Int c = u.CurrentCellPosition; c.z = 0; ours.Add(c); }
        foreach (Vector3Int c in ours)
            if (!ourBuildings.Contains(c))
                ourUnitsOffBuilding.Add(c);

        bool classify = rearByClass != null || frontByClass != null;
        var umbrellaByClass = classify ? new Dictionary<GameUnitClass, HashSet<Vector3Int>>() : null;

        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || !enemy.TryGetUnitData(out UnitData data)
                || data == null || data.domain != Domain.Air)
                continue;
            seen++;
            if (disruptorAnswersTransports && data.roles != null && data.roles.Count > 0
                && UnitRoleCompatibility.MatchesLabel(data.roles[0], UnitRole.Transportador))
                continue;

            // Sem tabuleiro nao ha como medir: conta, como antes (falha conservadora).
            if (board == null)
            {
                inRing++;
                AddRingClass(byClass, data.unitClass);
                // Sem tabuleiro tambem nao ha guarda-chuva: a resposta antiga (antiaereo).
                AddRingClass(rearByClass, data.unitClass);
                continue;
            }
            // Orcamento = movimento CHEIO: e o proximo turno dele. O MP restante do turno
            // que ele ja jogou deixaria o anel com largura zero.
            int budget = Mathf.Max(1, enemy.MaxMovementPoints);
            UnitReachEnvelope envelope = AIAirExposureService.BuildEnvelope(enemy, board, ai.TerrainDatabaseRef, ReachIntent.Combat, budget)
                // Sem arma (transporte puro): a ameaca e onde ele POUSA a carga.
                ?? AIAirExposureService.BuildEnvelope(enemy, board, ai.TerrainDatabaseRef, ReachIntent.Capture, budget);
            if (envelope != null && TouchesRing(envelope, ours, island))
            {
                inRing++;
                AddRingClass(byClass, data.unitClass);
                if (classify)
                {
                    HashSet<Vector3Int> umbrella = AIAirExposureService.GetAntiAirUmbrella(
                        snapshot, board, ai.TerrainDatabaseRef, data.unitClass, umbrellaByClass);
                    if (AIAirExposureService.TouchesUncovered(envelope, ourBuildings, umbrella))
                        AddRingClass(rearByClass, data.unitClass);
                    if (AIAirExposureService.TouchesUncovered(envelope, ourUnitsOffBuilding, umbrella))
                        AddRingClass(frontByClass, data.unitClass);
                }
            }
        }
        return inRing;
    }

    // GUARDA-CHUVA: celulas que algum antiaereo nosso acerta no proximo turno contra ESTA
    // classe de presa (prioridade de alvo da ficha >= Secundaria; Terciaria e "nao listada").
    // Construido uma vez por classe e por chamada. O servico de envelope responde a area;
    // a uniao e daqui (consumidor).
    private static void AddRingClass(Dictionary<GameUnitClass, int> byClass, GameUnitClass unitClass)
    {
        if (byClass == null)
            return;
        byClass.TryGetValue(unitClass, out int n);
        byClass[unitClass] = n + 1;
    }

    private static bool TouchesRing(UnitReachEnvelope envelope, HashSet<Vector3Int> ours, OwnLandProbe island)
    {
        bool Hit(Vector3Int cell)
        {
            cell.z = 0;
            return ours.Contains(cell) || (island != null && island.Contains(cell));
        }
        foreach (Vector3Int cell in envelope.MovementCells)
            if (Hit(cell)) return true;
        foreach (Vector3Int cell in envelope.AttackableCells)
            if (Hit(cell)) return true;
        return false;
    }

    // Response to support walls is separate from capture disruption. Everything
    // here is decision-local: no board mutations or process-global reach cache.
    private sealed class ArtilleryWallContext
    {
        public UnityEngine.Tilemaps.Tilemap Board;
        public readonly List<Vector3Int>[] Targets = { new List<Vector3Int>(), new List<Vector3Int>() };
        public readonly Dictionary<(UnitData, Vector3Int), Dictionary<Vector3Int, int>> LandRoutes = new();
        public readonly Dictionary<string, HashSet<int>>[] CoverageOffers = {
            new Dictionary<string, HashSet<int>>(), new Dictionary<string, HashSet<int>>() };
        public readonly List<(UnitData Unit, ConstructionManager Producer)>[] Offers = {
            new List<(UnitData, ConstructionManager)>(), new List<(UnitData, ConstructionManager)>() };

        public bool Reaches(UnitData unit, Vector3Int origin, int group)
        {
            if (unit.domain == Domain.Air) return Targets[group].Count > 0;
            if (unit.domain != Domain.Land) return false;
            foreach (Vector3Int target in Targets[group])
            {
                var key = (unit, target);
                if (!LandRoutes.TryGetValue(key, out var route))
                {
                    SectorManager.TryBuildLandMovementDistanceToTargetMap(Board, target, unit, out route);
                    LandRoutes[key] = route;
                }
                if (route != null && route.ContainsKey(origin)) return true;
            }
            return false;
        }
    }

    private static int ResolveArtilleryWallGroup(UnitData data)
    {
        if (data?.roles == null || data.roles.Count == 0) return -1;
        UnitRole main = data.roles[0];
        // AntiaereoCombatente also satisfies fire support: classify upward first.
        if (UnitRoleCompatibility.MatchesLabel(main, UnitRole.Antiaereo)
            || main == UnitRole.AntiaereoCombatente) return 1;
        if (UnitRoleCompatibility.MatchesLabel(main, UnitRole.FogoIndireto)
            || main == UnitRole.ArtilheiroCombatente) return 0;
        if (UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Assalto) return 0;
        return -1;
    }

    // Quarter-points avoid floating rounding at the three-point threshold.
    // Invasion preserves one response per artillery piece; assault pressure
    // remains supplementary at its normal weight.
    private static int ResolveWallPressure(UnitData data, bool invading)
    {
        bool artillery = data.roles[0] == UnitRole.ArtilheiroCombatente
            || data.roles[0] == UnitRole.AntiaereoCombatente
            || UnitRoleCompatibility.MatchesLabel(data.roles[0], UnitRole.FogoIndireto)
            || UnitRoleCompatibility.MatchesLabel(data.roles[0], UnitRole.Antiaereo);
        int level = Mathf.Clamp(data.eliteLevel, 0, 2);
        if (artillery) return invading ? 12 : level == 2 ? 12 : level == 1 ? 6 : 4;
        return level == 2 ? 6 : level == 1 ? 3 : 2;
    }

    private static bool IsArtilleryWallResponse(UnitData data)
    {
        if (data?.roles == null || data.roles.Count == 0
            || !HasWeaponCategory(data, WeaponCategory.AntiTanque)
            || data.ResolveAiTargetPriorityForTargetClass(GameUnitClass.Artillery) < BazookaTargetPriority.Secondary)
            return false;
        if (data.domain == Domain.Land)
            return UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Assalto;
        return data.domain == Domain.Air
            && UnitRoleCompatibility.MatchesLabel(data.roles[0], UnitRole.AtaqueAereo)
            && !UnitRoleCompatibility.MatchesLabel(data.roles[0], UnitRole.DisruptorDeCaptura);
    }

    private static bool MatchesWallOffer(AIShoppingDemand demand, UnitData unit,
        ConstructionManager producer, bool coverage = false)
    {
        if (demand.RallyResponseOffers != null
            && (unit == null || producer == null
                || !demand.RallyResponseOffers.TryGetValue(unit.id, out var rallyProducers)
                || !rallyProducers.Contains(producer.InstanceId))) return false;
        if (demand.ArtilleryWallGroup == 0) return true;
        var offers = coverage ? demand.WallCoverageOffers : demand.WallResponseOffers;
        return unit != null && producer != null && offers != null
            && offers.TryGetValue(unit.id, out var producers) && producers.Contains(producer.InstanceId);
    }

    private static List<AIShoppingDemand> BuildArtilleryWallDemands(AIWorldSnapshot snapshot, bool log)
    {
        var result = new List<AIShoppingDemand>();
        if (snapshot?.EnemyUnits == null || snapshot.MyBuildings == null) return result;
        var wall = new ArtilleryWallContext();
        foreach (ConstructionManager building in snapshot.MyBuildings)
            if (building != null && building.BoardTilemap != null) { wall.Board = building.BoardTilemap; break; }
        var pressure = new int[2];
        var enemyIds = new HashSet<int>();
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked || enemy.GetDomain() != Domain.Land
                || !enemyIds.Add(enemy.InstanceId)
                || !enemy.TryGetUnitData(out UnitData data)) continue;
            int group = ResolveArtilleryWallGroup(data);
            if (group < 0) continue;
            Vector3Int cell = enemy.CurrentCellPosition; cell.z = 0;
            wall.Targets[group].Add(cell);
            pressure[group] += ResolveWallPressure(data, snapshot.IsInvading);
        }
        int total = pressure[0] + pressure[1];
        int desired = snapshot.IsInvading ? total / 12 : Mathf.Min(3, total / 12);
        if (desired == 0) return result;

        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                || building.OfferedUnits == null) continue;
            foreach (UnitData unit in building.OfferedUnits)
            {
                if (!IsArtilleryWallResponse(unit) || unit.cost <= 0
                    || !IsRolePurchaseAllowed(unit, snapshot.Stance, false)
                    || !IsEliteChainAvailable(unit, snapshot)) continue;
                for (int group = 0; group < 2; group++)
                {
                    if (!wall.Reaches(unit, building.CurrentCellPosition, group)) continue;
                    wall.Offers[group].Add((unit, building));
                    if (!wall.CoverageOffers[group].TryGetValue(unit.id, out var producers))
                        wall.CoverageOffers[group][unit.id] = producers = new HashSet<int>();
                    producers.Add(building.InstanceId);
                }
            }
        }

        var preferredOffers = new List<(UnitData Unit, ConstructionManager Producer)>[2];
        var preferredCosts = new int[2];
        for (int group = 0; group < 2; group++)
        {
            Domain preferred = group == 0 ? Domain.Air : Domain.Land;
            var chosen = wall.Offers[group].FindAll(o => o.Unit.domain == preferred);
            bool converted = chosen.Count == 0;
            if (converted) chosen = wall.Offers[group];
            bool hasPrimary = chosen.Exists(o => o.Unit.ResolveAiTargetPriorityForTargetClass(GameUnitClass.Artillery)
                == BazookaTargetPriority.Primary);
            if (hasPrimary) chosen = chosen.FindAll(o => o.Unit.ResolveAiTargetPriorityForTargetClass(GameUnitClass.Artillery)
                == BazookaTargetPriority.Primary);
            preferredOffers[group] = chosen;
            preferredCosts[group] = int.MaxValue;
            foreach (var offer in chosen) preferredCosts[group] = Mathf.Min(preferredCosts[group], offer.Unit.cost);
            if (log && wall.Targets[group].Count > 0)
                Debug.Log($"[AI Shopping Wall] {(group == 0 ? "suporte/assalto terrestre" : "antiaereos")}={wall.Targets[group].Count} "
                    + $"pressao={pressure[group] / 4f:0.##} preferida={preferred} {(chosen.Count == 0 ? "sem resposta ofertada com acesso" : converted ? "convertida por oferta/acesso" : "preferida disponivel; caixa nao muda a escolha")}");
        }

        // Each existing piece contributes to one bucket only. First use its
        // preferred matchup, then spend surplus coverage on the other bucket.
        var owned = new List<(UnitManager Manager, UnitData Data)>();
        var ownIds = new HashSet<int>();
        if (snapshot.MyUnits != null)
            foreach (UnitManager own in snapshot.MyUnits)
                if (own != null && !own.IsDead && !own.IsEmbarked && ownIds.Add(own.InstanceId)
                    && own.TryGetUnitData(out UnitData data) && IsArtilleryWallResponse(data))
                    owned.Add((own, data));
        var initialCoverage = new int[2];
        foreach (var own in owned)
        {
            int preferred = own.Data.domain == Domain.Air ? 0 : 1;
            int group = wall.Reaches(own.Data, own.Manager.CurrentCellPosition, preferred) ? preferred : 1 - preferred;
            if (wall.Reaches(own.Data, own.Manager.CurrentCellPosition, group)) initialCoverage[group]++;
        }
        int[] quota = { desired * pressure[0] / total, desired * pressure[1] / total };
        if (quota[0] + quota[1] < desired)
        {
            int remainder0 = desired * pressure[0] % total;
            int remainder1 = desired * pressure[1] % total;
            int group = remainder0 > remainder1 ? 0 : 1;
            if (remainder0 == remainder1)
            {
                float cover0 = initialCoverage[0] / (float)Mathf.Max(1, pressure[0]);
                float cover1 = initialCoverage[1] / (float)Mathf.Max(1, pressure[1]);
                group = cover0 < cover1 ? 0 : cover1 < cover0 ? 1 : preferredCosts[0] < preferredCosts[1] ? 0 : 1;
            }
            quota[group]++;
        }
        var remaining = (int[])quota.Clone();
        var assigned = new HashSet<int>();
        for (int pass = 0; pass < 2; pass++)
            foreach (var own in owned)
            {
                if (assigned.Contains(own.Manager.InstanceId)) continue;
                int preferred = own.Data.domain == Domain.Air ? 0 : 1;
                int group = pass == 0 ? preferred : 1 - preferred;
                if (remaining[group] <= 0 || !wall.Reaches(own.Data, own.Manager.CurrentCellPosition, group)) continue;
                remaining[group]--; assigned.Add(own.Manager.InstanceId);
            }
        for (int group = 0; group < 2; group++)
        {
            var offers = preferredOffers[group];
            if (remaining[group] <= 0 || offers.Count == 0) continue;
            UnitData first = offers[0].Unit;
            var demand = NewRoleDemand(first.domain == Domain.Air ? UnitRole.AtaqueAereo : UnitRole.Assalto,
                remaining[group], 12, "breakthrough",
                $"parede {(group == 0 ? "suporte/assalto terrestre" : "antiaerea")}={wall.Targets[group].Count} "
                + $"pressao={pressure[group] / 4f:0.##} cobertura={quota[group] - remaining[group]}/{quota[group]} escala={(snapshot.IsInvading ? "1:1 artilharia + pressao de assalto" : "3 pontos/resposta, teto 3")}", false);
            demand.Domain = first.domain;
            demand.TargetClass = GameUnitClass.Artillery;
            demand.MinTargetPriority = first.ResolveAiTargetPriorityForTargetClass(GameUnitClass.Artillery);
            demand.ArtilleryWallGroup = group + 1;
            demand.WallResponseOffers = new Dictionary<string, HashSet<int>>();
            demand.WallCoverageOffers = wall.CoverageOffers[group];
            foreach (var offer in offers)
            {
                if (!demand.WallResponseOffers.TryGetValue(offer.Unit.id, out var producers))
                    demand.WallResponseOffers[offer.Unit.id] = producers = new HashSet<int>();
                producers.Add(offer.Producer.InstanceId);
            }
            result.Add(demand);
        }
        return result;
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

    // Nevoa total = o terreno fora da visao fica preto. Sem MatchController, conservador:
    // trata como nevoa (mantem o comportamento antigo da demanda).
    private static bool IsTotalFogMatch()
    {
        MatchController match = AIController.Instance != null ? AIController.Instance.Match : null;
        if (match == null)
            return true;
        return match.IsFogOfWarDebugEnabled && match.EnableTotalWar;
    }

    // O inimigo consegue pôr furtivo AEREO em campo? Pergunta a ficha (stealth skills), nao
    // o nome: um produtor inimigo que o oferece, ou um ja visivel/lembrado no snapshot.
    private static bool EnemyCanFieldAirStealth(AIWorldSnapshot snapshot)
    {
        if (snapshot == null)
            return false;
        if (snapshot.EnemyUnits != null)
            foreach (UnitManager enemy in snapshot.EnemyUnits)
                if (enemy != null && !enemy.IsDead
                    && enemy.TryGetUnitData(out UnitData data)
                    && IsAirStealth(data))
                    return true;
        if (snapshot.EnemyBuildings != null)
            foreach (ConstructionManager building in snapshot.EnemyBuildings)
            {
                if (building == null || building.OfferedUnits == null)
                    continue;
                for (int i = 0; i < building.OfferedUnits.Count; i++)
                    if (IsAirStealth(building.OfferedUnits[i]))
                        return true;
            }
        return false;
    }

    private static bool IsAirStealth(UnitData data)
        => data != null && data.domain == Domain.Air && data.IsStealthUnit();

    // O inimigo pode pôr submarino em campo? Mesmo formato do furtivo aereo: um ja visivel,
    // ou um produtor dele que OFERECE ficha de dominio Submarine. "Tem porto" sozinho nao
    // basta — porto que so faz superficie nao pede quem enxergue o submerso.
    private static bool HasEnemySubmarineCapability(AIWorldSnapshot snapshot)
    {
        if (CountEnemyDomain(snapshot, Domain.Submarine) > 0) return true;
        if (snapshot?.EnemyBuildings == null) return false;
        foreach (ConstructionManager building in snapshot.EnemyBuildings)
        {
            if (building == null || building.OfferedUnits == null) continue;
            for (int i = 0; i < building.OfferedUnits.Count; i++)
            {
                UnitData offered = building.OfferedUnits[i];
                if (offered != null && offered.domain == Domain.Submarine) return true;
            }
        }
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
