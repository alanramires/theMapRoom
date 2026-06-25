using System.Collections.Generic;
using UnityEngine;

// Classificadores de unidade, contadores de ameaça, intel e config de instância.
public partial class AITacticalAnalyzer
{
    public static bool UnitDataSatisfiesNeed(UnitData data, AINeedKind kind)
    {
        if (data == null)
            return false;

        switch (kind)
        {
            case AINeedKind.Capturer:
                return UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Capturador;
            case AINeedKind.Assault:
                return UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Assalto && !IsAntiAirOnlyUnit(data);
            case AINeedKind.AAA:
                return data.roles != null && data.roles.Count > 0
                    && data.roles[0] == UnitRole.AntiaereoCombatente;
            case AINeedKind.SAM:
                return data.roles != null && data.roles.Count > 0
                    && data.roles[0] == UnitRole.Antiaereo;
            case AINeedKind.Artillery:
            case AINeedKind.FireSupport:
                return UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.FogoIndireto && !IsAntiAirOnlyUnit(data);
            case AINeedKind.AirTransport:
                return UnitRoleCompatibility.CanSatisfy(data, UnitRole.Transportador) && data.domain == Domain.Air;
            case AINeedKind.GroundTransport:
                return UnitRoleCompatibility.CanSatisfy(data, UnitRole.Transportador) && data.domain == Domain.Land;
            case AINeedKind.FighterB:
                return IsPrimaryRole(data, UnitRole.Interceptador) && data.eliteLevel == 0;
            case AINeedKind.FighterA:
                return IsPrimaryRole(data, UnitRole.Interceptador) && data.eliteLevel >= 1;
            case AINeedKind.Apache:
                return IsPrimaryRole(data, UnitRole.AtaqueAereo) && data.eliteLevel == 0;
            case AINeedKind.AirTanker:
                return data.domain == Domain.Air && IsPrimaryRole(data, UnitRole.Logistica) && data.isSupplier;
            default:
                return false;
        }
    }

    private static bool UnitSatisfiesNeed(UnitManager unit, AINeedKind kind)
    {
        if (unit == null || unit.IsDead || unit.IsUnderRepair)
            return false;
        return unit.TryGetUnitData(out UnitData data) && UnitDataSatisfiesNeed(data, kind);
    }

    private static bool IsPrimaryRole(UnitData unit, UnitRole role)
    {
        return unit != null && unit.roles != null && unit.roles.Count > 0 && unit.roles[0] == role;
    }

    private static bool IsAntiAirOnlyUnit(UnitData unit)
    {
        if (unit == null || unit.embarkedWeapons == null || unit.embarkedWeapons.Count == 0)
            return false;
        foreach (UnitEmbarkedWeapon weapon in unit.embarkedWeapons)
        {
            if (weapon?.weapon == null) continue;
            if (weapon.weapon.WeaponCategory != WeaponCategory.AntiAerea)
                return false;
        }
        return true;
    }

    private static int CountActiveNeed(AIWorldSnapshot snapshot, AINeedKind kind)
    {
        if (snapshot?.MyUnits == null)
            return 0;
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (UnitSatisfiesNeed(unit, kind))
                count++;
        return count;
    }

    private static int CountSlots(SectorObjective obj, UnitRole role)
    {
        int count = 0;
        foreach (SlotNeed slot in obj.Slots)
            if (slot.Role == role)
                count++;
        return count;
    }

    private static int CountFilledCompatibleSlots(SectorObjective obj, AINeedKind kind)
    {
        if (obj == null || obj.Slots == null)
            return 0;
        int count = 0;
        foreach (SlotNeed slot in obj.Slots)
        {
            if (!slot.Filled) continue;
            UnitManager unit = FindActiveUnit(slot.AssignedUnitId);
            if (UnitSatisfiesNeed(unit, kind))
                count++;
        }
        return count;
    }

    private static bool HasAnySlot(SectorObjective obj, UnitRole role)
    {
        if (obj == null || obj.Slots == null)
            return false;
        foreach (SlotNeed slot in obj.Slots)
            if (slot.Role == role)
                return true;
        return false;
    }

    private static int CountEmbarkedCapturersForObjective(AIWorldSnapshot snapshot, SectorObjective obj)
    {
        if (snapshot?.MyUnits == null || obj == null)
            return 0;

        var objectiveCapturers = new HashSet<int>();
        foreach (SlotNeed slot in obj.Slots)
            if (slot.Filled)
            {
                UnitManager unit = FindActiveUnit(slot.AssignedUnitId);
                if (UnitSatisfiesNeed(unit, AINeedKind.Capturer))
                    objectiveCapturers.Add(unit.InstanceId);
            }

        int count = 0;
        foreach (UnitManager transport in snapshot.MyUnits)
        {
            if (!UnitSatisfiesNeed(transport, AINeedKind.AirTransport) || transport.TransportedUnitSlots == null)
                continue;
            foreach (UnitTransportSeatRuntime seat in transport.TransportedUnitSlots)
            {
                UnitManager passenger = seat != null ? seat.embarkedUnit : null;
                if (passenger != null && passenger.IsEmbarked && objectiveCapturers.Contains(passenger.InstanceId))
                    count++;
            }
        }

        return count;
    }

    private static int CountVisibleEnemyAircraftNearHQ(AIWorldSnapshot snapshot, int range)
    {
        if (snapshot?.MyHQ == null)
            return 0;
        return CountVisibleEnemyAircraftNearCell(snapshot, snapshot.MyHQ.CurrentCellPosition, range);
    }

    private static int CountVisibleEnemyFighterANearHQ(AIWorldSnapshot snapshot, int range)
    {
        if (snapshot?.MyHQ == null)
            return 0;
        return CountVisibleEnemyFighterANearCell(snapshot, snapshot.MyHQ.CurrentCellPosition, range);
    }

    private static int CountVisibleEnemyAircraftNearCell(AIWorldSnapshot snapshot, Vector3Int cell, int range)
    {
        if (snapshot?.EnemyUnits == null)
            return 0;
        Vector3Int center = Normalize(cell);
        int count = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (!enemy.TryGetUnitData(out UnitData data) || data == null || data.domain != Domain.Air) continue;
            if (SectorManager.HexDistance(center, Normalize(enemy.CurrentCellPosition)) <= range)
                count++;
        }
        return count;
    }

    private static int CountVisibleEnemyFighterANearCell(AIWorldSnapshot snapshot, Vector3Int cell, int range)
    {
        if (snapshot?.EnemyUnits == null)
            return 0;
        Vector3Int center = Normalize(cell);
        int count = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (!enemy.TryGetUnitData(out UnitData data) || !IsFighterA(data)) continue;
            if (SectorManager.HexDistance(center, Normalize(enemy.CurrentCellPosition)) <= range)
                count++;
        }
        return count;
    }

    private static int CountTotalVisibleEnemyAircraft(AIWorldSnapshot snapshot)
    {
        if (snapshot?.EnemyUnits == null)
            return 0;
        int count = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (enemy.TryGetUnitData(out UnitData data) && data != null && data.domain == Domain.Air)
                count++;
        }
        return count;
    }

    private static int CountTotalVisibleEnemyFighterA(AIWorldSnapshot snapshot)
    {
        if (snapshot?.EnemyUnits == null)
            return 0;
        int count = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (enemy.TryGetUnitData(out UnitData data) && IsFighterA(data))
                count++;
        }
        return count;
    }

    private static bool IsFighterA(UnitData data)
    {
        return data != null
            && data.domain == Domain.Air
            && IsPrimaryRole(data, UnitRole.Interceptador)
            && data.eliteLevel >= 1;
    }

    private static bool IsOwnedDefensibleSector(SectorManager.SectorInfo info, TeamId team)
    {
        return info != null
            && info.ControllingTeam == team
            && (info.IsFullyControlled || info.IsDisputed || info.HasPartialCapture);
    }

    private static int CountVisibleEnemyArmorNearOwnedBase(AIWorldSnapshot snapshot, int range)
    {
        if (snapshot?.EnemyUnits == null || snapshot.MyBuildings == null)
            return 0;
        int count = 0;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (!enemy.TryGetUnitData(out UnitData data) || data == null) continue;
            if (data.unitClass != GameUnitClass.Armored) continue;
            if (IsPrimaryRole(data, UnitRole.Transportador)) continue;
            if (data.eliteLevel < 1) continue;

            Vector3Int enemyCell = Normalize(enemy.CurrentCellPosition);
            foreach (ConstructionManager building in snapshot.MyBuildings)
            {
                if (!IsCriticalHomeConstruction(building, snapshot.AITeam)) continue;
                if (SectorManager.HexDistance(enemyCell, Normalize(building.CurrentCellPosition)) <= range)
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    private static bool IsHomeDefenseThreatened(AIWorldSnapshot snapshot, TeamId team, int range)
    {
        if (snapshot?.MyBuildings == null || snapshot.EnemyUnits == null)
            return false;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (!IsCriticalHomeConstruction(building, team)) continue;
            if (HasGroundEnemyNearCell(snapshot, building.CurrentCellPosition, range))
                return true;
        }
        return false;
    }

    private static bool HasNearbyVisibleEnemy(AIWorldSnapshot snapshot, Vector3Int cell, int range)
    {
        return HasEnemyNearCell(snapshot, cell, range);
    }

    private static bool HasEnemyNearCell(AIWorldSnapshot snapshot, Vector3Int cell, int range)
    {
        if (snapshot?.EnemyUnits == null)
            return false;
        Vector3Int center = Normalize(cell);
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (SectorManager.HexDistance(center, Normalize(enemy.CurrentCellPosition)) <= range)
                return true;
        }
        return false;
    }

    private static bool HasGroundEnemyNearCell(AIWorldSnapshot snapshot, Vector3Int cell, int range)
    {
        if (snapshot?.EnemyUnits == null)
            return false;
        Vector3Int center = Normalize(cell);
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (enemy.GetHeightLevel() != HeightLevel.Surface) continue;
            if (SectorManager.HexDistance(center, Normalize(enemy.CurrentCellPosition)) <= range)
                return true;
        }
        return false;
    }

    private static int CountOwnedHomeConstructionsUnderCapture(AIWorldSnapshot snapshot, TeamId team)
    {
        if (snapshot?.MyBuildings == null)
            return 0;
        int count = 0;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (!IsCriticalHomeConstruction(building, team)) continue;
            if (IsOwnedConstructionUnderActiveCapture(building, team, snapshot, HomeThreatRange))
                count++;
        }
        return count;
    }

    private static int CountOwnedConstructionsUnderCapture(AIWorldSnapshot snapshot, TeamId team)
    {
        if (snapshot?.MyBuildings == null)
            return 0;
        int count = 0;
        foreach (ConstructionManager building in snapshot.MyBuildings)
            if (IsOwnedConstructionUnderCapture(building, team))
                count++;
        return count;
    }

    private static bool IsOwnedConstructionUnderCapture(ConstructionManager building, TeamId team)
    {
        return building != null
            && building.TeamId == team
            && building.CapturePointsMax > 0
            && building.CurrentCapturePoints > 0
            && building.CurrentCapturePoints < building.CapturePointsMax;
    }

    private static bool IsOwnedConstructionUnderActiveCapture(ConstructionManager building, TeamId team, AIWorldSnapshot snapshot, int range)
    {
        if (!IsOwnedConstructionUnderCapture(building, team))
            return false;
        return snapshot != null && HasGroundEnemyNearCell(snapshot, building.CurrentCellPosition, range);
    }

    private static bool IsCriticalHomeConstruction(ConstructionManager building, TeamId team)
    {
        return building != null
            && building.TeamId == team
            && (building.IsPlayerHeadQuarter || ConstructionSectorHelper.IsBase(building.Sector));
    }

    private static SectorObjective FindHomeDefenseObjective(TeamObjectivePlan plan, TeamId team)
    {
        if (plan == null)
            return null;
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Status != ObjectiveStatus.Defending) continue;
            if (ConstructionSectorHelper.IsBase(obj.Sector))
                return obj;
            if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info)
                && info != null
                && info.ControllingTeam == team
                && ConstructionSectorHelper.IsBase(info.Sector))
                return obj;
        }
        return null;
    }

    private static int InstanceSafeAntiAirCoverageRange()
    {
        return AIShoppingPlanner.Instance != null ? AIShoppingPlanner.Instance.AntiAirCoverageRange : 5;
    }

    private static int InstanceMinBaseAAA()
    {
        return AIShoppingPlanner.Instance != null ? AIShoppingPlanner.Instance.MinBaseAAA : 1;
    }

    private static int InstanceMinBaseArtillery()
    {
        return AIShoppingPlanner.Instance != null ? AIShoppingPlanner.Instance.MinBaseArtilharia : 1;
    }

    private static int GetEffectiveTransportThreshold(TeamId team)
    {
        return AIController.Instance != null ? AIController.Instance.GetEffectiveTransportThreshold(team) : 7;
    }

    // Mapa de eixos para a fase de shopping. Reusa o currentAxisMap do planner (construido
    // no mesmo turno, mesmo time, antes do shopping) e so reconstroi se nao houver mapa do
    // time pedido — evita 1 build/turno no caso comum.
    private static InvasionAxisMap GetShoppingAxisMap(TeamId team)
    {
        InvasionAxisMap map = AIController.Instance != null ? AIController.Instance.CurrentAxisMap : null;
        if (map != null && map.Team == team)
            return map;
        return InvasionAxisMap.Build(team);
    }

    private static bool HasPreventiveDefenseBudget(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return false;
        int income = Mathf.Max(1, snapshot.IncomePerTurn);
        return snapshot.Budget >= 40000 || snapshot.Budget >= Mathf.Max(20000, income * 2);
    }

    private static AIIntelReport BuildOperationIntelReport(TeamId team, AIWorldSnapshot snapshot)
    {
        if (snapshot == null)
            return null;

        JogadasManager jogadas = JogadasManager.EnsureInstance();
        if (jogadas == null || jogadas.log == null || jogadas.log.jogadas == null || jogadas.log.jogadas.Count == 0)
            return null;

        int lookback = AIShoppingPlanner.Instance != null ? Mathf.Max(1, AIShoppingPlanner.Instance.IntelShoppingLookbackTurns) : 4;
        return AIIntelAnalyzer.BuildReport(jogadas.log, team, lookback, 5, snapshot.TurnNumber);
    }

    private static AISectorIntel FindIntelForSector(AIIntelReport intel, ConstructionSector sector)
    {
        if (intel == null || intel.sectors == null)
            return null;

        string sectorName = sector.ToString();
        for (int i = 0; i < intel.sectors.Count; i++)
        {
            AISectorIntel entry = intel.sectors[i];
            if (entry != null && entry.sector == sectorName)
                return entry;
        }
        return null;
    }

    private static bool IsHotIntelSector(AISectorIntel intel)
    {
        if (intel == null)
            return false;

        return intel.hotScore >= 2f
            || intel.capturePressure > 0f
            || intel.landingPressure > 0f
            || intel.damageTaken > 0f
            || intel.enemyPresence >= 2f;
    }
}
