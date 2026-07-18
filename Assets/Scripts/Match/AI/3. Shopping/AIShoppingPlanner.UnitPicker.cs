using System.Collections.Generic;
using UnityEngine;

// Seleção de unidade por edifício: PickUnit, PickAirUnit e helpers de classificação.
public partial class AIShoppingPlanner
{
    private static UnitData PickAirUnit(
        ConstructionManager building, int budget,
        bool wantsTransport, bool wantsCacaB, bool wantsCacaA, bool wantsApache, bool wantsBomba, bool wantsAirTanker, bool wantsIntel,
        bool urgentCacaB,
        TeamId aiTeam)
    {
        if (building == null || building.OfferedUnits == null) return null;

        UnitData best      = null;
        int      bestScore = int.MinValue;

        foreach (UnitData u in building.OfferedUnits)
        {
            if (u == null) continue;
            if (IsHardModeBannedForAI(u)) { Debug.Log($"[AI Shopping Air] SKIP {u.displayName} — banida no Hard Mode"); continue; }
            if (u.domain != Domain.Air) continue;
            if (u.cost > budget)
            {
                Debug.Log($"[AI Shopping Air] SKIP {u.displayName} ${u.cost} — custo>{budget}");
                continue;
            }
            if (u.roles == null || u.roles.Count == 0) continue;

            UnitRole primary = u.roles[0];
            bool elite = u.eliteLevel >= 1;
            int score;
            if      (primary == UnitRole.Transportador && wantsTransport)        score = 10000 + u.cost;
            else if (primary == UnitRole.Interceptador && !elite && wantsCacaB)  score = (urgentCacaB ? 25000 : 18000) + u.cost;
            else if (primary == UnitRole.Interceptador &&  elite && wantsCacaA)  score = 30000 + u.cost;
            else if (primary == UnitRole.AtaqueAereo   && !elite && wantsApache) score = 20000 + u.cost;
            else if (primary == UnitRole.AtaqueAereo   &&  elite && wantsBomba)  score = 36000 + u.cost;
            else if (primary == UnitRole.Logistica && wantsAirTanker && IsAirTankerPurchase(u)) score = 24000 + u.cost;
            else if (wantsIntel && IsDedicatedIntelPurchase(u))
            {
                if (CountExistingDedicatedIntel(aiTeam, Domain.Air) >= 1)
                {
                    Debug.Log($"[AI Shopping Air] SKIP {u.displayName} — EWACS/intel aérea já existente");
                    continue;
                }
                score = 26000 + GetIntelPurchaseVisionScore(u) + u.cost;
            }
            else
            {
                Debug.Log($"[AI Shopping Air] SKIP {u.displayName} — sem demanda role={primary} elite={elite} trans={wantsTransport} cacaB={wantsCacaB} cacaA={wantsCacaA} apache={wantsApache} bomba={wantsBomba} tanker={wantsAirTanker} intel={wantsIntel}");
                continue;
            }

            Debug.Log($"[AI Shopping Air] candidato {u.displayName} ${u.cost} role={primary} elite={elite} urgentCacaB={urgentCacaB} bomba={wantsBomba} intel={wantsIntel && IsDedicatedIntelPurchase(u)} score={score}");
            if (score > bestScore) { bestScore = score; best = u; }
        }
        return best;
    }

    private static float GetMinDistanceToOpenObjective(ConstructionManager building, TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null) return float.MaxValue;
        float minDist = float.MaxValue;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (!obj.HasOpenSlot(UnitRole.Capturador) && !obj.HasOpenSlot(UnitRole.Assalto)) continue;
            if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info)) continue;

            foreach (SectorManager.SectorTeamDistances td in info.SectorDistances)
            {
                if (td.Team != aiTeam) continue;
                foreach (SectorManager.SectorDistanceEntry e in td.Entries)
                {
                    bool match = building.IsPlayerHeadQuarter
                        ? e.IsHQ
                        : (!e.IsHQ && e.InstanceId == building.InstanceId);
                    if (match && e.Distance < minDist) minDist = e.Distance;
                }
            }
        }

        return minDist;
    }

    private static UnitData PickUnit(
        ConstructionManager building,
        AIWorldSnapshot snapshot,
        int budget,
        int openCapturerSlots,
        int openAssaultSlots,
        int openTransportSlots = 0,
        bool urgentTransportDemand = false,
        int openLogisticsSlots = 0,
        int openFireSupportSlots = 0,
        int openMobileAirIntelSlots = 0,
        bool preferDefensiveFireSupport = false,
        UnitData eliteAssaultTarget = null,
        UnitData eliteFireSupportTarget = null,
        bool defensiveBaseThreat = false,
        bool allowDefensiveEliteAssault = false,
        int defensiveBaseResponseReserveCost = 0,
        bool defensiveBaseManpowerShortage = false,
        int defensiveBaseBasicMassCost = 0,
        bool defensiveBaseTankBought = false,
        bool defensiveArmorThreat = false,
        bool strategicArmorParity = false,
        bool wantsEliteFireSupport = false,
        int activeFireSupportCount = 0,
        bool proactiveDefFireSupport = false,
        bool proactiveAntiAir = false,
        int activeSAMs = 0,
        int activeAAAs = 0,
        int aaaCap = 0,
        bool aaaThreat = false,
        bool defensiveInfantryThreat = false,
        bool offensiveAntiInfantryFireSupport = false,
        bool matureEconomyEliteAssaultPivot = false,
        float enemyInfantryPressure = 0f,
        float enemyArmorPressure = 0f)
    {
        if (building.OfferedUnits == null || building.OfferedUnits.Count == 0) return null;

        bool defensiveStance  = snapshot.Stance == AIStance.Defensive;
        bool hasOpenDefensiveSlot = HasOpenDefensiveSlot(snapshot.AITeam);
        bool decisiveDefensiveFireNeeded = ShouldPrioritizeDecisiveDefensiveFire(
            snapshot, budget, openFireSupportSlots, preferDefensiveFireSupport,
            defensiveBaseThreat, activeFireSupportCount);
        if (decisiveDefensiveFireNeeded
            && ShouldYieldDecisiveFireToAffordableAssault(
                building, snapshot, budget, openAssaultSlots, activeFireSupportCount, defensiveBaseThreat))
        {
            decisiveDefensiveFireNeeded = false;
            Debug.Log($"[AI PickUnit] decisive_fire_suppressed: assalto pesado compravel budget={budget} ass={openAssaultSlots} activeFire={activeFireSupportCount} stance={snapshot.Stance}");
        }

        UnitData best      = null;
        int      bestScore = int.MinValue;

        foreach (UnitData u in building.OfferedUnits)
        {
            if (u == null || u.cost > budget) { if (u != null) Debug.Log($"[AI PickUnit] SKIP {u.displayName} ${u.cost} — custo>{budget}"); continue; }
            if (IsHardModeBannedForAI(u)) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — banida no Hard Mode"); continue; }
            if (u.domain != Domain.Land) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — domain={u.domain} (não Land)"); continue; }
            bool isAntiAirOnly = IsAntiAirOnlyUnit(u);
            bool isSAMType = isAntiAirOnly && IsPrimaryRole(u, UnitRole.FogoIndireto);
            bool isAAAType = isAntiAirOnly && IsPrimaryRole(u, UnitRole.Assalto);
            if (matureEconomyEliteAssaultPivot
                && IsFireSupportPurchase(u)
                && u.eliteLevel < 1
                && IsFireSupportSaturated(snapshot)
                && !defensiveBaseThreat
                && !isAntiAirOnly)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} - quality pivot: fogo comum saturado, preservando compra elite");
                continue;
            }
            int samCap = Instance != null ? Instance.MaxProactiveAntiAirSAM : 3;
            if (isSAMType && activeSAMs >= 1 && !aaaThreat)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — SAM proativo ja coberto ({activeSAMs}/1)");
                continue;
            }
            if (isSAMType && activeSAMs >= samCap)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — SAM cap atingido ({activeSAMs}/{samCap})");
                continue;
            }
            if (isAAAType && aaaCap > 0 && activeAAAs >= aaaCap)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — AAA cap atingido ({activeAAAs}/{aaaCap} cobertura 1:2)");
                continue;
            }
            if (isAntiAirOnly && !HasAnyAirThreat() && !proactiveAntiAir)
            {
                Debug.Log($"[AI PickUnit] SKIP {u.displayName} — anti-aerea sem ameaca aerea em campo");
                continue;
            }

            bool isPrimaryCapturer   = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Capturador;
            bool isPrimaryAssault    = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Assalto;
            bool isAggressiveCapturer = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.CapturadorAgressivo;
            bool isPrimaryTransporter = UnitRoleCompatibility.ResolveCompositionRole(u) == UnitRole.Transportador;
            bool isPrimaryLogistics = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.Logistica;
            bool isPrimaryFireSupport = u.roles != null && u.roles.Count > 0 && u.roles[0] == UnitRole.FogoIndireto;
            bool isPrimaryIntel = IsDedicatedIntelPurchase(u);
            bool isFireSupportCapable = u.roles != null && u.roles.Contains(UnitRole.FogoIndireto);
            bool isHybridCapturer    = isPrimaryAssault && u.roles.Contains(UnitRole.Capturador);
            bool isSecondary       = !isPrimaryCapturer && u.roles != null && u.roles.Contains(UnitRole.Capturador);
            bool fireSupportAllowedNow = openFireSupportSlots > 0 || IsFireSupportAllowedByTiming(snapshot);
            bool isDefensiveOnlyUnit = u.aiPurchaseMode == AIPurchaseMode.Defensive;
            bool isOffensiveOnlyUnit = u.aiPurchaseMode == AIPurchaseMode.Offensive;
            bool strategicArmorParityBypass = strategicArmorParity && IsDefensiveBaseAssaultTankPurchase(u);

            bool proactiveAntiAirSAMBypass = proactiveAntiAir && isSAMType;
            bool proactiveAntiAirAAABypass = proactiveAntiAir && isAAAType && aaaCap > 0 && activeAAAs < aaaCap;
            bool proactiveDefBypass = (proactiveDefFireSupport || proactiveAntiAirSAMBypass) && isDefensiveOnlyUnit && isFireSupportCapable;
            if (!defensiveBaseThreat && isDefensiveOnlyUnit && !proactiveDefBypass && !proactiveAntiAirAAABypass && !strategicArmorParityBypass)
            { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — Defensive-only, sem ameaça"); continue; }
            if (defensiveBaseThreat && isOffensiveOnlyUnit)
            { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — Offensive-only, modo defensivo"); continue; }

            if (isAggressiveCapturer && openCapturerSlots <= 0 && openAssaultSlots <= 0)
            { Debug.Log($"[AI PickUnit] SKIP {u.displayName} - sem demanda capturador/assalto para capturador agressivo"); continue; }

            if (isPrimaryCapturer && openCapturerSlots <= 0)
            { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda capturador"); continue; }

            if (isPrimaryLogistics && openLogisticsSlots <= 0)
            { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda logistics"); continue; }

            if (isPrimaryIntel && openMobileAirIntelSlots <= 0)
            { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda intel aérea móvel"); continue; }

            if (isPrimaryAssault && !isHybridCapturer && openAssaultSlots <= 0 && !defensiveBaseThreat && !proactiveAntiAirAAABypass && !strategicArmorParityBypass)
            { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda assault"); continue; }

            if (isPrimaryTransporter && openTransportSlots <= 0 && !urgentTransportDemand && !defensiveBaseThreat)
            { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda transporte"); continue; }

            bool defensiveFireSupportBypass = defensiveBaseThreat && isDefensiveOnlyUnit && isFireSupportCapable;
            if (isFireSupportCapable && !isPrimaryAssault && openFireSupportSlots <= 0 && !defensiveFireSupportBypass)
            { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — sem demanda fire_support"); continue; }

            if (isFireSupportCapable && isPrimaryAssault && !fireSupportAllowedNow && !defensiveBaseThreat)
            { Debug.Log($"[AI PickUnit] SKIP {u.displayName} - fire_support cedo demais"); continue; }

            bool isAllowedDefensiveElite = allowDefensiveEliteAssault && u == eliteAssaultTarget;
            bool isAllowedDefensiveFireSupport = openFireSupportSlots > 0
                && ((preferDefensiveFireSupport && IsDefensiveFireSupportPurchase(u))
                    || (defensiveBaseThreat && isFireSupportCapable));
            bool canAffordDefensiveTank = CanAffordDefensiveBaseTankPurchase(u, budget, defensiveBaseResponseReserveCost);
            bool canBuyBasicMass = defensiveBaseManpowerShortage
                && defensiveBaseTankBought
                && IsDefensiveBaseBasicMassPurchase(u);
            bool canBuyLogistics = openLogisticsSlots > 0 && isPrimaryLogistics;
            bool isAAADefense = isAAAType && aaaThreat && activeAAAs < aaaCap;
            if (defensiveBaseThreat && canBuyLogistics) { /* logistics valid during defense */ }
            else if (defensiveBaseThreat
                && !isDefensiveOnlyUnit
                && !IsDefensiveBaseThreatPurchase(u)
                && !isAllowedDefensiveElite
                && !isAllowedDefensiveFireSupport
                && !canAffordDefensiveTank
                && !canBuyBasicMass
                && !isAAADefense) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — defThreat filter (notThreat={!IsDefensiveBaseThreatPurchase(u)} notElite={!isAllowedDefensiveElite} notTank={!canAffordDefensiveTank} notMass={!canBuyBasicMass} notAAA={!isAAADefense})"); continue; }
            if (!defensiveBaseThreat && isHybridCapturer && !hasOpenDefensiveSlot) { Debug.Log($"[AI PickUnit] SKIP {u.displayName} — hybrid sem slot defensivo"); continue; }

            int score = u.cost;
            if (defensiveBaseThreat && isDefensiveOnlyUnit && isFireSupportCapable)
                score += 55000;
            if (defensiveBaseThreat && defensiveBaseManpowerShortage)
            {
                int basicReserve = Mathf.Max(0, defensiveBaseBasicMassCost) * 2;
                if (IsDefensiveBaseAssaultTankPurchase(u) && budget >= u.cost + basicReserve)
                    score += 180000;
                else if (IsDefensiveBaseThreatPurchase(u))
                    score += 90000;
                else if (IsDefensiveBaseBasicMassPurchase(u))
                    score += 70000;
            }
            if (defensiveArmorThreat && IsDefensiveBaseThreatPurchase(u))
                score += 80000;
            if (strategicArmorParityBypass)
            {
                score += IsDefensiveBaseAssaultTankPurchase(u)
                    ? 85000 + Mathf.Max(0, u.eliteLevel) * 10000
                    : 145000;
                if (u.ResolveAiTargetPriorityForTargetClass(GameUnitClass.Armored) == BazookaTargetPriority.Primary)
                    score += 20000;
                Debug.Log($"[AI PickUnit] armor_parity_bonus {u.displayName} strategic_at={strategicArmorParityBypass}");
            }
            if (defensiveInfantryThreat && IsAntiInfantryFireSupportPurchase(u))
                score += 80000;
            if (offensiveAntiInfantryFireSupport && IsAntiInfantryFireSupportPurchase(u))
            {
                score += IsOffensiveFireSupportPurchase(u) ? 180000 : 115000;
                if (isPrimaryFireSupport) score += 25000;
                if (u.cost >= 6000) score += 15000;
                Debug.Log($"[AI PickUnit] offensive_anti_inf_fire_bonus {u.displayName} fire={openFireSupportSlots} preferDef={preferDefensiveFireSupport}");
            }
            if (defensiveInfantryThreat && !defensiveArmorThreat
                && IsDefensiveBaseAssaultTankPurchase(u)
                && u.ResolveAiTargetPriorityForTargetClass(GameUnitClass.Infantry) == BazookaTargetPriority.Primary)
                score += 75000;
            if (openTransportSlots > 0 && isPrimaryTransporter)
                score += urgentTransportDemand ? 144000 : 108000;
            if (openLogisticsSlots > 0 && isPrimaryLogistics)
            {
                score += openLogisticsSlots >= 2 ? 220000 : 185000;
                if (defensiveBaseThreat) score -= 25000;
            }
            if (openMobileAirIntelSlots > 0 && isPrimaryIntel)
                score += 132000 + GetIntelPurchaseVisionScore(u);
            if (openFireSupportSlots > 0 && isFireSupportCapable)
            {
                bool preferredProfile = preferDefensiveFireSupport
                    ? IsDefensiveFireSupportPurchase(u)
                    : IsOffensiveFireSupportPurchase(u);
                bool fallbackProfile = preferDefensiveFireSupport
                    ? IsOffensiveFireSupportPurchase(u)
                    : IsDefensiveFireSupportPurchase(u);

                score += preferredProfile ? 118000 : fallbackProfile ? 72000 : 35000;
                if (!isPrimaryFireSupport) score -= 18000;
                score += Mathf.Max(0, u.eliteLevel) * 1500;
                if (activeFireSupportCount == 0 && u.eliteLevel >= 1)
                    score -= 120000;
                if (wantsEliteFireSupport && u == eliteFireSupportTarget) score += 500000;
                if (defensiveBaseThreat)
                {
                    score += 150000;
                    if (isPrimaryAssault) score += 25000;
                    else if (isPrimaryFireSupport) score += 18000;
                }
                if (!preferredProfile && !fallbackProfile) score -= 25000;
            }
            if (decisiveDefensiveFireNeeded && IsDecisiveDefensiveFirePurchase(u))
            {
                score += 45000;
                Debug.Log($"[AI PickUnit] decisive_fire_bonus {u.displayName} +45000 budget={budget} fire={openFireSupportSlots} activeFire={activeFireSupportCount} stance={snapshot.Stance}");
            }
            if (openCapturerSlots > 0)
            {
                if (isPrimaryCapturer)              score += 100000;
                else if (isAggressiveCapturer)      score +=  85000;
                else if (isSecondary && defensiveStance) score +=  10000;
                else if (openAssaultSlots <= 0 && !(openTransportSlots > 0 && isPrimaryTransporter)) score -= 100000;
            }
            if (openAssaultSlots > 0)
            {
                if (u == eliteAssaultTarget) score += 500000;
                if (isPrimaryAssault && !isHybridCapturer) score += 90000;
                else if (isAggressiveCapturer && openCapturerSlots <= 0) score += 85000;
                else if (isPrimaryAssault && defensiveStance) score += 10000;
                else if (isPrimaryAssault) score -= 90000;
                else if (openCapturerSlots <= 0 && !isPrimaryTransporter) score -= 90000;
            }

            if (isAggressiveCapturer)
            {
                bool antiInfantry = u.ResolveAiTargetPriorityForTargetClass(GameUnitClass.Infantry)
                    == BazookaTargetPriority.Primary;
                bool antiArmor = u.ResolveAiTargetPriorityForTargetClass(GameUnitClass.Armored)
                    == BazookaTargetPriority.Primary;
                if (antiInfantry)
                    score += Mathf.RoundToInt(Mathf.Clamp(enemyInfantryPressure, 0f, 6f) * 25000f);
                if (antiArmor)
                    score += Mathf.RoundToInt(Mathf.Clamp(enemyArmorPressure, 0f, 6f) * 25000f);

                int sameSpecialists = CountActiveAggressiveCounterSpecialists(snapshot.AITeam,
                    antiInfantry ? GameUnitClass.Infantry : GameUnitClass.Armored);
                score -= sameSpecialists * 12000;
            }

            if (isAAADefense) score += 320000;
            if (proactiveAntiAir && isSAMType && openFireSupportSlots > 0) score += 420000;
            if (!defensiveStance && u.movement < 3) score -= (3 - u.movement) * 1500;

            string roleStr = isPrimaryIntel ? "INTEL" : isFireSupportCapable && !isPrimaryFireSupport ? "ASS/FIRE" : isPrimaryFireSupport ? "FIRE" : isPrimaryLogistics ? "LOG" : isPrimaryTransporter ? "TRANS" : isPrimaryCapturer ? "CAP" : isAggressiveCapturer ? "CAP-AGG" : isPrimaryAssault ? $"ASS(hybrid={isHybridCapturer})" : "other";
            Debug.Log($"[AI PickUnit] {u.displayName} ${u.cost} role={roleStr} score={score} mov={u.movement} | trans={openTransportSlots} transUrg={urgentTransportDemand} log={openLogisticsSlots} intelMobileAir={openMobileAirIntelSlots} cap={openCapturerSlots} ass={openAssaultSlots} fire={openFireSupportSlots} fireDef={preferDefensiveFireSupport} defThreat={defensiveBaseThreat}");
            if (score > bestScore) { bestScore = score; best = u; }
        }

        return best;
    }

    private static int CountActiveAggressiveCounterSpecialists(TeamId aiTeam, GameUnitClass targetClass)
    {
        int count = 0;
        foreach (UnitManager manager in UnitManager.AllActive)
        {
            if (manager == null || manager.TeamId != aiTeam || manager.IsDead)
                continue;
            if (!manager.TryGetUnitData(out UnitData data) || data == null
                || data.roles == null || data.roles.Count == 0
                || data.roles[0] != UnitRole.CapturadorAgressivo)
                continue;
            if (data.ResolveAiTargetPriorityForTargetClass(targetClass) == BazookaTargetPriority.Primary)
                count++;
        }
        return count;
    }

    private static bool IsPrimaryRole(UnitData unit, UnitRole role)
    {
        return unit != null && unit.roles != null && unit.roles.Count > 0 && unit.roles[0] == role;
    }

    // Hard Mode: unidades marcadas como banidas não entram na lista de compras da IA.
    private static bool IsHardModeBannedForAI(UnitData unit)
    {
        return unit != null
            && unit.bannedOnHardMode
            && AIController.Instance != null
            && AIController.Instance.HardMode;
    }

    private static bool IsAirTankerPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Air
            && unit.isSupplier
            && IsPrimaryRole(unit, UnitRole.Logistica);
    }

    private static bool IsDedicatedIntelPurchase(UnitData unit)
    {
        if (unit == null || unit.roles == null || unit.roles.Count == 0)
            return false;
        if (unit.roles[0] != UnitRole.Intel)
            return false;

        return !unit.roles.Contains(UnitRole.Assalto)
            && !unit.roles.Contains(UnitRole.Capturador)
            && !unit.roles.Contains(UnitRole.FogoIndireto)
            && !unit.roles.Contains(UnitRole.Transportador)
            && !unit.roles.Contains(UnitRole.Interceptador)
            && !unit.roles.Contains(UnitRole.AtaqueAereo)
            && !unit.roles.Contains(UnitRole.Logistica);
    }

    private static int GetIntelPurchaseVisionScore(UnitData unit)
    {
        if (unit == null)
            return 0;

        int airLow = unit.ResolveVisionFor(Domain.Air, HeightLevel.AirLow);
        int airHigh = unit.ResolveVisionFor(Domain.Air, HeightLevel.AirHigh);
        int general = Mathf.Max(0, unit.visao);
        return Mathf.Max(airLow, airHigh, general) * 1000;
    }

    private static bool IsFireSupportPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && unit.roles != null
            && unit.roles.Contains(UnitRole.FogoIndireto);
    }

    private static bool IsDefensiveFireSupportPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && IsFireSupportPurchase(unit)
            && unit.longRangeStationary;
    }

    private static bool IsOffensiveFireSupportPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && IsFireSupportPurchase(unit)
            && unit.preferRepositionAtWeaponMaxRange;
    }

    private static bool IsPurePrimaryAssault(UnitData unit)
    {
        return IsPrimaryRole(unit, UnitRole.Assalto)
            && (unit.roles == null || !unit.roles.Contains(UnitRole.Capturador));
    }

    private static bool IsDefensiveBaseThreatPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && unit.unitClass == GameUnitClass.Infantry
            && unit.roles != null
            && unit.roles.Count > 0
            && unit.roles[0] == UnitRole.Assalto
            && unit.roles.Contains(UnitRole.Capturador);
    }

    private static bool IsDefensiveBaseAssaultTankPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && unit.unitClass == GameUnitClass.Armored
            && IsPurePrimaryAssault(unit);
    }

    private static bool IsDefensiveBaseBasicMassPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && unit.unitClass == GameUnitClass.Infantry
            && IsPrimaryRole(unit, UnitRole.Capturador);
    }

    private static bool HasDefensiveBaseManpowerShortage(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyUnits == null || snapshot.EnemyUnits == null)
            return false;

        int myCount = snapshot.MyUnits.Count;
        int visibleEnemyCount = snapshot.EnemyUnits.Count;
        return myCount > 0
            && myCount <= DefensiveLowTroopCountThreshold
            && visibleEnemyCount > myCount;
    }

    private static bool IsDecisiveDefensiveFirePurchase(UnitData unit)
    {
        return unit != null
            && IsFireSupportPurchase(unit)
            && IsPrimaryRole(unit, UnitRole.FogoIndireto)
            && !IsAntiAirOnlyUnit(unit)
            && unit.cost >= 10000;
    }

    private static bool ShouldPrioritizeDecisiveDefensiveFire(
        AIWorldSnapshot snapshot,
        int budget,
        int openFireSupportSlots,
        bool preferDefensiveFireSupport,
        bool defensiveBaseThreat,
        int activeFireSupportCount)
    {
        if (snapshot == null)
            return false;
        if (openFireSupportSlots <= 0 || !preferDefensiveFireSupport)
            return false;
        if (budget < 10000)
            return false;
        if (defensiveBaseThreat)
            return true;

        bool fireBacklog = openFireSupportSlots >= 2 || activeFireSupportCount <= 1;
        if (snapshot.Stance == AIStance.Defensive && fireBacklog)
            return true;

        IReadOnlyDictionary<ConstructionSector, AISectorIntent> intents = AISectorIntentAnalyzer.GetIntents(snapshot.AITeam);
        if (intents == null)
            return false;

        foreach (AISectorIntent intent in intents.Values)
        {
            if (intent == null) continue;
            if (intent.Kind != AISectorIntentKind.Defend) continue;
            if (intent.Confidence >= 0.70f && intent.HotScore >= 6f)
                return true;
        }

        return false;
    }

    private static bool ShouldYieldDecisiveFireToAffordableAssault(
        ConstructionManager building,
        AIWorldSnapshot snapshot,
        int budget,
        int openAssaultSlots,
        int activeFireSupportCount,
        bool defensiveBaseThreat)
    {
        if (building == null || building.OfferedUnits == null || snapshot == null)
            return false;
        if (defensiveBaseThreat || snapshot.Stance != AIStance.Offensive)
            return false;
        if (openAssaultSlots <= 0 || activeFireSupportCount < 2)
            return false;

        foreach (UnitData unit in building.OfferedUnits)
        {
            if (unit == null || unit.cost > budget || unit.domain != Domain.Land)
                continue;
            if (IsPurePrimaryAssault(unit))
                return true;
        }

        return false;
    }

    private static bool IsFireSupportAllowedByTiming(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return false;

        int minTurn = Instance != null ? Instance.MinTurnForFireSupport : 3;
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn)
            return false;

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto, requirePrimary: true);
        int minCapturers = Instance != null ? Instance.MinActiveCapturersForFireSupport : 2;
        int minAssault = Instance != null ? Instance.MinActiveAssaultForFireSupport : 1;

        return activeCapturers >= minCapturers && activeAssault >= minAssault;
    }

    private static bool CanOfferUnit(ConstructionManager building, UnitData target)
    {
        if (building == null || target == null || building.OfferedUnits == null) return false;
        for (int i = 0; i < building.OfferedUnits.Count; i++)
            if (building.OfferedUnits[i] == target) return true;
        return false;
    }

    private static bool CanOfferAntiAirDefenseUnit(ConstructionManager building)
    {
        if (building == null || building.OfferedUnits == null)
            return false;

        for (int i = 0; i < building.OfferedUnits.Count; i++)
        {
            UnitData unit = building.OfferedUnits[i];
            if (unit != null && unit.domain == Domain.Land && IsAntiAirOnlyUnit(unit))
                return true;
        }

        return false;
    }

    private static bool CanOfferDefensiveFireSupportUnit(ConstructionManager building)
    {
        if (building == null || building.OfferedUnits == null)
            return false;

        for (int i = 0; i < building.OfferedUnits.Count; i++)
        {
            UnitData unit = building.OfferedUnits[i];
            if (unit != null && unit.domain == Domain.Land && IsDefensiveFireSupportPurchase(unit))
                return true;
        }

        return false;
    }

    private static bool CanOfferFireSupportUnit(ConstructionManager building)
    {
        if (building == null || building.OfferedUnits == null) return false;
        for (int i = 0; i < building.OfferedUnits.Count; i++)
            if (IsFireSupportPurchase(building.OfferedUnits[i])) return true;
        return false;
    }

    private static bool CanOfferAffordableDefensiveTank(ConstructionManager building, int budget, int reserve)
    {
        if (building == null || building.OfferedUnits == null) return false;

        foreach (UnitData unit in building.OfferedUnits)
        {
            if (CanAffordDefensiveBaseTankPurchase(unit, budget, reserve)) return true;
        }

        return false;
    }

    private static bool CanAffordDefensiveBaseTankPurchase(UnitData unit, int budget, int reserve)
    {
        if (!IsDefensiveBaseAssaultTankPurchase(unit))
            return false;

        int safeReserve = unit.eliteLevel >= 1 ? 0 : Mathf.Max(0, reserve);
        return budget >= unit.cost + safeReserve;
    }

    private static bool CanOfferPrimaryRoleUnit(ConstructionManager building, UnitRole role)
    {
        if (building == null || building.OfferedUnits == null) return false;
        foreach (UnitData u in building.OfferedUnits)
            if (u != null && u.domain == Domain.Land && IsPrimaryRole(u, role)) return true;
        return false;
    }

    private static UnitData FindCheapestAffordableLandUnit(ConstructionManager building, int budget)
    {
        if (building == null || building.OfferedUnits == null) return null;
        UnitData cheapest = null;
        foreach (UnitData u in building.OfferedUnits)
        {
            if (u == null || u.domain != Domain.Land || u.cost > budget || u.cost <= 0) continue;
            if (IsHardModeBannedForAI(u)) continue;
            if (cheapest == null || u.cost < cheapest.cost) cheapest = u;
        }
        return cheapest;
    }

    private static int GetBaseDefenseProductionPriority(
        ConstructionManager building,
        AIWorldSnapshot snapshot,
        bool criticalAirThreat)
    {
        if (building == null)
            return int.MaxValue;

        int distance = GetDistanceToOwnHQ(building, snapshot);
        bool offersAirDefense = CanOfferAntiAirDefenseUnit(building);
        bool offersDefensiveFire = CanOfferDefensiveFireSupportUnit(building);
        bool offersBaseDefense = criticalAirThreat
            ? offersAirDefense || offersDefensiveFire
            : offersDefensiveFire || CanOfferFireSupportUnit(building) || CanOfferPrimaryRoleUnit(building, UnitRole.Assalto);

        return (offersBaseDefense ? 0 : 10000) + distance;
    }

    private static int GetDistanceToOwnHQ(ConstructionManager building, AIWorldSnapshot snapshot)
    {
        if (building == null)
            return int.MaxValue / 4;

        Vector3Int cell = building.CurrentCellPosition;
        cell.z = 0;

        if (snapshot != null && snapshot.MyHQ != null)
        {
            Vector3Int hq = snapshot.MyHQ.CurrentCellPosition;
            hq.z = 0;
            return Mathf.RoundToInt(SectorManager.HexDistance(cell, hq));
        }

        if (building.IsPlayerHeadQuarter)
            return 0;
        if (ConstructionSectorHelper.IsBase(building.Sector))
            return 1;
        return 99;
    }

    // Returns true when all of the unit's weapons target only air (AntiAerea).
    private static bool IsAntiAirOnlyUnit(UnitData unit)
    {
        if (unit == null || unit.embarkedWeapons == null || unit.embarkedWeapons.Count == 0)
            return false;
        foreach (UnitEmbarkedWeapon ew in unit.embarkedWeapons)
        {
            if (ew?.weapon == null) continue;
            if (ew.weapon.WeaponCategory != WeaponCategory.AntiAerea)
                return false;
        }
        return true;
    }

    // Returns true when at least one active unit on the map has a native Air domain.
    private static bool HasAnyAirThreat()
    {
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.IsDead) continue;
            if (!u.TryGetUnitData(out UnitData data) || data == null) continue;
            if (data.domain == Domain.Air) return true;
        }
        return false;
    }
}
