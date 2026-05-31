using System.Collections.Generic;
using UnityEngine;

// Ameaças defensivas: cálculo de demanda anti-ar, artilharia preventiva, tanques de base
// e emergência de produção bloqueada.
public partial class AIShoppingPlanner
{
    private static bool ComputeProactiveAntiAirNeeded(AIWorldSnapshot snapshot, out int activeSAMs, out int activeAAAs)
    {
        activeSAMs = 0;
        activeAAAs = 0;
        if (snapshot?.MyUnits == null) return false;

        foreach (UnitManager u in snapshot.MyUnits)
        {
            if (u == null || u.IsDead || u.IsEmbarked) continue;
            if (!u.TryGetUnitData(out UnitData d) || d == null) continue;
            if (!IsAntiAirOnlyUnit(d)) continue;
            if (IsPrimaryRole(d, UnitRole.FogoIndireto)) activeSAMs++;
            else if (IsPrimaryRole(d, UnitRole.Assalto)) activeAAAs++;
        }

        if (HasAnyAirThreat()) return false;

        int minTurn = Instance != null ? Instance.MinTurnForFireSupport : 3;
        bool richEarly = HasPreventiveDefenseBudget(snapshot);
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn && !richEarly) return false;

        bool attackStance = snapshot.Stance == AIStance.Offensive || snapshot.Stance == AIStance.Tactical;
        if (!attackStance) return false;

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault   = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto,    requirePrimary: true);
        int minCap = Instance != null ? Instance.MinActiveCapturersForFireSupport : 2;
        int minAss = Instance != null ? Instance.MinActiveAssaultForFireSupport   : 1;
        bool armyReady = activeCapturers >= minCap && activeAssault >= minAss;

        Debug.Log($"[AI Shopping] proactive_anti_air: armyReady={armyReady} activeSAMs={activeSAMs} activeAAAs={activeAAAs} stance={snapshot.Stance} turn={snapshot.TurnNumber}/{minTurn} richEarly={richEarly} cap={activeCapturers}/{minCap} ass={activeAssault}/{minAss}");
        return armyReady;
    }

    private static bool HasPreventiveDefenseBudget(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return false;
        int income = Mathf.Max(1, snapshot.IncomePerTurn);
        return snapshot.Budget >= 40000 || snapshot.Budget >= Mathf.Max(20000, income * 2);
    }

    private static void ComputeGuaranteedBaseDefense(AIWorldSnapshot snapshot,
        out int openArtSlots, out bool forceBaseAAA)
    {
        openArtSlots = 0; forceBaseAAA = false;
        if (snapshot == null) return;
        int minTurn = Instance != null ? Instance.MinTurnBaseDefense : 3;
        bool richEarly = HasPreventiveDefenseBudget(snapshot);
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn && !richEarly)
        {
            Debug.Log($"[AI Shopping] base_defense: bloqueado por turno {snapshot.TurnNumber}<{minTurn} budget={snapshot.Budget}");
            return;
        }

        int minArt = Instance != null ? Instance.MinBaseArtilharia : 1;
        int minAAA = Instance != null ? Instance.MinBaseAAA : 1;

        int activeArt = 0, activeAntiAir = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d == null) continue;
                if (d.roles != null && d.roles.Contains(UnitRole.FogoIndireto) && !IsAntiAirOnlyUnit(d)) activeArt++;
                if (IsAntiAirOnlyUnit(d)) activeAntiAir++;
            }

        openArtSlots = Mathf.Max(0, minArt - activeArt);
        forceBaseAAA = activeAntiAir < minAAA;
        Debug.Log($"[AI Shopping] base_defense: activeArt={activeArt}/{minArt} activeAAA={activeAntiAir}/{minAAA} artSlots={openArtSlots} forceAAA={forceBaseAAA} richEarly={richEarly}");
    }

    private static bool ComputeProactiveDefensiveFireSupportNeeded(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return false;

        int cap = Instance != null ? Instance.MaxProactiveDefensiveFireSupport : 1;
        if (cap <= 0) return false;

        int minTurn = Instance != null ? Instance.MinTurnForFireSupport : 3;
        bool richEarly = HasPreventiveDefenseBudget(snapshot);
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn && !richEarly) return false;

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault   = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto,    requirePrimary: true);
        int minCap = Instance != null ? Instance.MinActiveCapturersForFireSupport : 2;
        int minAss = Instance != null ? Instance.MinActiveAssaultForFireSupport   : 1;
        if (activeCapturers < minCap || activeAssault < minAss) return false;

        int activeDefFS = 0;
        if (snapshot.MyUnits != null)
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (u == null || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
                if (!u.TryGetUnitData(out UnitData d) || d == null) continue;
                if (IsDefensiveFireSupportPurchase(d)) activeDefFS++;
            }

        int activeCombatFireSupport = CountActiveCombatFireSupport(snapshot);
        int saturationLimit = GetFireSupportSaturationLimit(snapshot);
        if (activeCombatFireSupport >= saturationLimit)
        {
            Debug.Log($"[AI Shopping] proactive_def_fire_support: bloqueado por saturacao fire={activeCombatFireSupport}/{saturationLimit} ass={activeAssault}");
            return false;
        }

        bool needed = activeDefFS < cap;
        Debug.Log($"[AI Shopping] proactive_def_fire_support: needed={needed} activeDefFS={activeDefFS} activeFire={activeCombatFireSupport}/{saturationLimit} cap={cap} cap={activeCapturers}/{minCap} ass={activeAssault}/{minAss} richEarly={richEarly}");
        return needed;
    }

    private static bool HasVisibleEnemyNearCell(Vector3Int cell, AIWorldSnapshot snapshot, int range)
    {
        if (snapshot == null || snapshot.EnemyUnits == null) return false;
        cell.z = 0;
        int safeRange = Mathf.Max(0, range);
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(cell, ec) <= safeRange) return true;
        }
        return false;
    }

    private static bool HasVisibleEnemyNearBase(ConstructionManager building, AIWorldSnapshot snapshot, int range)
    {
        if (building == null || snapshot == null || snapshot.EnemyUnits == null)
            return false;
        if (!IsCriticalHomeConstruction(building, snapshot.AITeam))
            return false;

        Vector3Int baseCell = building.CurrentCellPosition;
        baseCell.z = 0;
        int safeRange = Mathf.Max(0, range);

        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            if (SectorManager.HexDistance(baseCell, enemyCell) <= safeRange)
                return true;
        }

        return false;
    }

    private static int CountVisibleEnemyAircraftNearHQ(AIWorldSnapshot snapshot, int range)
    {
        if (snapshot == null || snapshot.EnemyUnits == null || snapshot.MyBuildings == null) return 0;

        Vector3Int hqCell = Vector3Int.zero;
        bool hqFound = false;
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null || !b.IsPlayerHeadQuarter) continue;
            hqCell = b.CurrentCellPosition; hqCell.z = 0;
            hqFound = true;
            break;
        }
        if (!hqFound) return 0;

        int count = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (!enemy.TryGetUnitData(out UnitData d) || d == null) continue;
            if (d.domain != Domain.Air) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(hqCell, ec) <= range) count++;
        }
        return count;
    }

    private static int CountTotalVisibleEnemyAircraft(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.EnemyUnits == null) return 0;
        int count = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (!enemy.TryGetUnitData(out UnitData d) || d == null) continue;
            if (d.domain == Domain.Air) count++;
        }
        return count;
    }

    private static int CountVisibleEnemyInfantryNearOwnedBase(AIWorldSnapshot snapshot, int range)
    {
        if (snapshot == null || snapshot.EnemyUnits == null || snapshot.MyBuildings == null)
            return 0;

        int safeRange = Mathf.Max(0, range);
        int count = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (!enemy.TryGetUnitData(out UnitData d) || d == null) continue;
            if (d.unitClass != GameUnitClass.Infantry) continue;
            if (d.roles == null || d.roles.Count == 0 || d.roles[0] != UnitRole.Capturador) continue;

            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            foreach (ConstructionManager building in snapshot.MyBuildings)
            {
                if (building == null) continue;
                if (!IsCriticalHomeConstruction(building, snapshot.AITeam)) continue;
                Vector3Int bc = building.CurrentCellPosition; bc.z = 0;
                if (SectorManager.HexDistance(bc, ec) <= safeRange) { count++; break; }
            }
        }
        return count;
    }

    private static int CountVisibleEnemyArmorNearOwnedBase(AIWorldSnapshot snapshot, int range)
    {
        if (snapshot == null || snapshot.EnemyUnits == null || snapshot.MyBuildings == null)
            return 0;

        int safeRange = Mathf.Max(0, range);
        int count = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (!enemy.TryGetUnitData(out UnitData enemyData) || enemyData == null) continue;
            if (enemyData.unitClass != GameUnitClass.Armored) continue;
            if (enemyData.roles != null && enemyData.roles.Count > 0 && enemyData.roles[0] == UnitRole.Transportador) continue;
            if (enemyData.eliteLevel < 1) continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            foreach (ConstructionManager building in snapshot.MyBuildings)
            {
                if (building == null) continue;
                if (!IsCriticalHomeConstruction(building, snapshot.AITeam)) continue;

                Vector3Int baseCell = building.CurrentCellPosition;
                baseCell.z = 0;
                if (SectorManager.HexDistance(baseCell, enemyCell) > safeRange) continue;

                count++;
                break;
            }
        }

        return count;
    }

    private static bool IsAntiInfantryFireSupportPurchase(UnitData unit)
    {
        return unit != null
            && unit.domain == Domain.Land
            && IsFireSupportPurchase(unit)
            && unit.ResolveAiTargetPriorityForTargetClass(GameUnitClass.Infantry) == BazookaTargetPriority.Primary;
    }

    private static UnitData FindAntiInfantryDefensiveTarget(AIWorldSnapshot snapshot, int budget)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return null;

        UnitData best = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (unit == null || unit.domain != Domain.Land) continue;
                if (!IsAntiInfantryFireSupportPurchase(unit)) continue;
                if (unit.cost > budget) continue;

                if (best == null
                    || unit.eliteLevel > best.eliteLevel
                    || (unit.eliteLevel == best.eliteLevel && unit.cost > best.cost))
                    best = unit;
            }
        }
        return best;
    }

    private static bool HasIntelArmorThreatNearOwnBase(AIWorldSnapshot snapshot, AIIntelReport intel, bool intelArmorThreat)
    {
        if (!intelArmorThreat || snapshot == null || intel == null || intel.sectors == null || intel.sectors.Count == 0)
            return false;
        if (snapshot.MyBuildings == null)
            return false;

        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (!IsCriticalHomeConstruction(building, snapshot.AITeam))
                continue;

            AISectorIntel sectorIntel = FindIntelSector(intel, building.Sector);
            if (IsBaseDefenseHotIntelSector(sectorIntel))
                return true;
        }

        return false;
    }

    private static bool HasIntelInfantryThreatNearOwnBase(AIWorldSnapshot snapshot, AIIntelReport intel)
    {
        if (snapshot == null || intel == null || intel.sectors == null || intel.sectors.Count == 0)
            return false;
        if (snapshot.MyBuildings == null)
            return false;
        if (intel.enemyInfantryForce < 2f && intel.enemyInfantryPressureScore < 2f)
            return false;

        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (!IsCriticalHomeConstruction(building, snapshot.AITeam)) continue;
            AISectorIntel sectorIntel = FindIntelSector(intel, building.Sector);
            if (sectorIntel != null && sectorIntel.capturePressure > 0f)
                return true;
        }
        return false;
    }

    private static bool IsCriticalHomeConstruction(ConstructionManager building, TeamId aiTeam)
    {
        if (building == null || building.TeamId != aiTeam)
            return false;
        return building.IsPlayerHeadQuarter || ConstructionSectorHelper.IsBase(building.Sector);
    }

    private static bool HasAnyVisibleEnemyNearOwnedBase(AIWorldSnapshot snapshot, int range)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return false;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null) continue;
            if (!IsCriticalHomeConstruction(building, snapshot.AITeam)) continue;
            if (HasVisibleEnemyNearBase(building, snapshot, range)) return true;
        }
        return false;
    }

    private static bool HasAnyOffensiveObjective(TeamId aiTeam)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null) return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Status == ObjectiveStatus.Defending || obj.Status == ObjectiveStatus.Complete) continue;
            if (obj.HasOpenSlot(UnitRole.Capturador) || obj.HasOpenSlot(UnitRole.Assalto))
                return true;

            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && (slot.Role == UnitRole.Capturador || slot.Role == UnitRole.Assalto))
                    return true;
        }
        return false;
    }

    private static bool TryFindEmergencyProductionDefensePurchase(
        AIWorldSnapshot snapshot,
        int budget,
        out UnitData bestUnit,
        out int contestedOwned)
    {
        bestUnit = null;
        contestedOwned = CountOwnedConstructionsUnderCapture(snapshot);
        if (snapshot == null || snapshot.MyUnits == null || snapshot.MyBuildings == null)
            return false;
        if (snapshot.MyUnits.Count != 1)
            return false;
        if (contestedOwned <= 0)
            return false;

        int bestScore = int.MinValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            UnitData unit = FindBestAffordableEmergencyDefensePurchase(building, budget);
            if (unit == null) continue;

            int score = ScoreEmergencyDefensePurchase(unit);
            if (score > bestScore)
            {
                bestScore = score;
                bestUnit = unit;
            }
        }

        return bestUnit != null;
    }

    private static int CountOwnedConstructionsUnderCapture(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyBuildings == null)
            return 0;

        int count = 0;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.IsCapturable || building.CapturePointsMax <= 0)
                continue;
            if (!IsCriticalHomeConstruction(building, snapshot.AITeam))
                continue;
            if (building.CurrentCapturePoints < building.CapturePointsMax)
                count++;
        }

        return count;
    }

    private static UnitData FindBestAffordableEmergencyDefensePurchase(ConstructionManager building, int budget)
    {
        if (building == null || building.OfferedUnits == null)
            return null;

        UnitData best = null;
        int bestScore = int.MinValue;
        foreach (UnitData unit in building.OfferedUnits)
        {
            if (unit == null || unit.cost > budget || unit.domain != Domain.Land)
                continue;
            if (!IsEmergencyDefensePurchase(unit))
                continue;

            int score = ScoreEmergencyDefensePurchase(unit);
            if (score > bestScore)
            {
                bestScore = score;
                best = unit;
            }
        }

        return best;
    }

    private static bool IsEmergencyDefensePurchase(UnitData unit)
    {
        if (unit == null || unit.roles == null)
            return false;

        bool fireSupport = unit.roles.Contains(UnitRole.FogoIndireto);
        bool assaultArmor = unit.unitClass == GameUnitClass.Armored
            && unit.roles.Count > 0
            && unit.roles[0] == UnitRole.Assalto;
        return fireSupport || assaultArmor;
    }

    private static int ScoreEmergencyDefensePurchase(UnitData unit)
    {
        if (unit == null)
            return int.MinValue;

        bool fireSupport = unit.roles != null && unit.roles.Contains(UnitRole.FogoIndireto);
        bool assaultArmor = unit.unitClass == GameUnitClass.Armored
            && unit.roles != null && unit.roles.Count > 0 && unit.roles[0] == UnitRole.Assalto;

        int score = unit.cost + Mathf.Max(0, unit.eliteLevel) * 10000;
        if (fireSupport) score += 100000;
        if (unit.longRangeStationary) score += 25000;
        if (unit.preferRepositionAtWeaponMaxRange) score += 15000;
        if (assaultArmor) score += 50000;
        return score;
    }

    private static int FindCheapestDefensiveBaseThreatPurchaseCost(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return 0;

        int cheapest = int.MaxValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (!IsDefensiveBaseThreatPurchase(unit)) continue;
                if (unit.cost < cheapest) cheapest = unit.cost;
            }
        }

        return cheapest < int.MaxValue ? cheapest : 0;
    }

    private static int FindCheapestDefensiveBaseBasicMassPurchaseCost(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return 0;

        int cheapest = int.MaxValue;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (!IsDefensiveBaseBasicMassPurchase(unit)) continue;
                if (unit.cost < cheapest) cheapest = unit.cost;
            }
        }

        return cheapest < int.MaxValue ? cheapest : 0;
    }

    private static bool CanAffordEliteDefensiveTank(AIWorldSnapshot snapshot, int budget)
    {
        if (snapshot == null || snapshot.MyBuildings == null) return false;

        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam)) continue;
            if (building.OfferedUnits == null) continue;

            foreach (UnitData unit in building.OfferedUnits)
            {
                if (!IsDefensiveBaseAssaultTankPurchase(unit)) continue;
                if (unit.eliteLevel < 1) continue;
                if (unit.cost <= budget) return true;
            }
        }

        return false;
    }
}
