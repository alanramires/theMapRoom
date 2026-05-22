using System.Collections.Generic;
using UnityEngine;

public class AIOperationManager : MonoBehaviour
{
    private const int HomeThreatRange = 3;
    private const int SectorDefenseRange = 3;
    private const int DefensiveArmorThreatRange = 3;
    private const int AirRefuelLowFuelPct = 35;
    private const int AirRefuelCriticalFuelPct = 20;

    private static AIOperationManager instance;
    public static AIOperationManager Instance => EnsureInstance();

    private readonly Dictionary<TeamId, List<AIOperation>> operationsByTeam = new Dictionary<TeamId, List<AIOperation>>();
    private int nextId = 1;

    private static AIOperationManager EnsureInstance()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<AIOperationManager>();
        if (instance != null) return instance;
        GameObject go = new GameObject(nameof(AIOperationManager));
        instance = go.AddComponent<AIOperationManager>();
        DontDestroyOnLoad(go);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Rebuild(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        var ops = new List<AIOperation>();
        operationsByTeam[team] = ops;
        if (snapshot == null)
            return;

        TryBuildBaseDefenseOp(team, snapshot, plan, ops);
        TryBuildSectorDefenseOps(team, snapshot, plan, ops);
        TryBuildAirliftCaptureOps(team, snapshot, plan, ops);
        TryBuildAirRefuelSupportOp(team, snapshot, ops);
        TryBuildPreventiveDefenseOp(team, snapshot, ops);

        AssignExistingUnitsToOperations(team, snapshot, ops);
        InferPhasesForAllOps(snapshot, ops);
        LogOperations(team, snapshot.TurnNumber, ops);
    }

    public IReadOnlyList<AIOperation> GetOperationsForTeam(TeamId team)
    {
        if (operationsByTeam.TryGetValue(team, out List<AIOperation> ops))
            return ops;
        return System.Array.Empty<AIOperation>();
    }

    public List<OperationDeficit> GetDeficits(TeamId team)
    {
        var deficits = new List<OperationDeficit>();
        if (!operationsByTeam.TryGetValue(team, out List<AIOperation> ops))
            return deficits;

        ops.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        foreach (AIOperation op in ops)
        {
            var counts = new Dictionary<AINeedKind, int>();
            foreach (AIOperationSlotNeed slot in op.RequiredSlots)
            {
                if (slot.Filled) continue;
                counts.TryGetValue(slot.Kind, out int count);
                counts[slot.Kind] = count + 1;
            }

            foreach (KeyValuePair<AINeedKind, int> kv in counts)
            {
                if (kv.Value <= 0) continue;
                deficits.Add(new OperationDeficit
                {
                    Operation = op,
                    Kind = kv.Key,
                    Count = kv.Value,
                });
                Debug.Log($"[AI Ops][T{op.LastUpdatedTurn}][{team}] deficit {op.Type} {op.Sector}: {kv.Key}x{kv.Value}");
            }
        }

        return deficits;
    }

    private void TryBuildBaseDefenseOp(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan, List<AIOperation> ops)
    {
        int aircraftNearHQ = CountVisibleEnemyAircraftNearHQ(snapshot, InstanceSafeAntiAirCoverageRange());
        int armorNearBase = CountVisibleEnemyArmorNearOwnedBase(snapshot, DefensiveArmorThreatRange);
        bool captureActive = CountOwnedHomeConstructionsUnderCapture(snapshot, team) > 0;
        bool homeThreat = IsHomeDefenseThreatened(snapshot, team, HomeThreatRange) || captureActive;

        if (aircraftNearHQ <= 0 && armorNearBase <= 0 && !homeThreat)
            return;

        AIOperation op = CreateOperation(team, AIOperationType.BaseDefense, 1, snapshot, snapshot.MyHQ != null ? snapshot.MyHQ.Sector : ConstructionSector.Base1);
        op.IsUrgent = true;
        op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
        op.TargetCell = op.AnchorCell;
        op.LinkedObjective = FindHomeDefenseObjective(plan, team);

        if (aircraftNearHQ > 0)
        {
            op.AddSlots(AINeedKind.FighterB, CountActiveNeed(snapshot, AINeedKind.FighterB) <= 0 ? 1 : 0);
            op.AddSlots(AINeedKind.AAA, Mathf.Max(0, InstanceMinBaseAAA() - CountActiveNeed(snapshot, AINeedKind.AAA)));
            if (CountActiveNeed(snapshot, AINeedKind.SAM) < 1)
                op.AddSlots(AINeedKind.SAM, 1);
        }

        if (homeThreat || armorNearBase > 0)
        {
            if (captureActive)
                op.AddSlots(AINeedKind.Capturer, 2);
            else if (CountOwnedConstructionsUnderCapture(snapshot, team) > 0)
                op.AddSlots(AINeedKind.Capturer, 1);
            op.AddSlots(AINeedKind.Assault, armorNearBase > 0 ? 2 : 1);
            op.AddSlots(AINeedKind.Artillery, 1);
        }

        if (op.RequiredSlots.Count == 0)
            return;

        ops.Add(op);
        Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] BaseDefense URGENTE aircraft={aircraftNearHQ} armor={armorNearBase} capture={captureActive} slots={DescribeSlots(op)}");
    }

    private void TryBuildSectorDefenseOps(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan, List<AIOperation> ops)
    {
        if (plan == null)
            return;

        var built = new HashSet<ConstructionSector>();
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Status != ObjectiveStatus.Defending) continue;
            if (ConstructionSectorHelper.IsBase(obj.Sector)) continue;
            if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info)) continue;
            BuildSectorDefenseOp(team, snapshot, obj, info, ops, built);
        }

        foreach (SectorManager.SectorInfo info in SectorManager.GetAllSectorInfos())
        {
            if (info == null || ConstructionSectorHelper.IsBase(info.Sector)) continue;
            if (built.Contains(info.Sector)) continue;
            if (!info.IsFullyControlled || info.ControllingTeam != team) continue;
            if (!HasNearbyVisibleEnemy(snapshot, info.RepresentativeCell, SectorDefenseRange)) continue;
            BuildSectorDefenseOp(team, snapshot, plan.GetObjectiveForSector(info.Sector), info, ops, built);
        }
    }

    private void BuildSectorDefenseOp(
        TeamId team,
        AIWorldSnapshot snapshot,
        SectorObjective linked,
        SectorManager.SectorInfo info,
        List<AIOperation> ops,
        HashSet<ConstructionSector> built)
    {
        AIOperation op = CreateOperation(team, AIOperationType.SectorDefense, 2, snapshot, info.Sector);
        op.LinkedObjective = linked;
        op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
        op.TargetCell = Normalize(info.RepresentativeCell);

        if (info.HasPartialCapture)
            op.AddSlots(AINeedKind.Capturer, 1);
        op.AddSlots(AINeedKind.Assault, 1);
        if (info.GetDistanceToHQ(team) <= GetEffectiveTransportThreshold(team))
            op.AddSlots(AINeedKind.Artillery, 1);
        if (CountVisibleEnemyAircraftNearCell(snapshot, info.RepresentativeCell, 2) > 0)
            op.AddSlots(AINeedKind.AAA, 1);

        ops.Add(op);
        built.Add(info.Sector);
        Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] SectorDefense {info.Sector}: partial={info.HasPartialCapture} slots={DescribeSlots(op)}");
    }

    private void TryBuildAirliftCaptureOps(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan, List<AIOperation> ops)
    {
        if (plan == null)
            return;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null) continue;
            if (obj.Status != ObjectiveStatus.Pending
                && obj.Status != ObjectiveStatus.Pursuing
                && obj.Status != ObjectiveStatus.Capturing)
                continue;
            if (!HasAnySlot(obj, UnitRole.Transportador)) continue;
            if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info)) continue;

            int desiredPassengers = CountSlots(obj, UnitRole.Capturador);
            int qualifiedCapturers = CountFilledCompatibleSlots(obj, AINeedKind.Capturer);
            int assignedChinooks = CountFilledCompatibleSlots(obj, AINeedKind.AirTransport);
            int embarkedCapturers = CountEmbarkedCapturersForObjective(snapshot, obj);
            int capturerDeficit = Mathf.Max(0, desiredPassengers - qualifiedCapturers - embarkedCapturers);
            int neededChinooks = Mathf.CeilToInt(desiredPassengers / 2f);
            int airDeficit = Mathf.Max(0, neededChinooks - assignedChinooks);

            AIOperation op = CreateOperation(team, AIOperationType.AirliftCapture, 4, snapshot, obj.Sector);
            op.LinkedObjective = obj;
            op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
            op.TargetCell = Normalize(info.RepresentativeCell);
            op.AddSlots(AINeedKind.Capturer, capturerDeficit);
            op.AddSlots(AINeedKind.AirTransport, airDeficit);
            if (info.GetRiskLevelFor(team) >= SectorManager.SectorRiskLevel.High)
                op.AddSlots(AINeedKind.Assault, 1);
            if (CountTotalVisibleEnemyAircraft(snapshot) > 0)
                op.AddSlots(AINeedKind.FighterB, CountActiveNeed(snapshot, AINeedKind.FighterB) <= 0 ? 1 : 0);

            if (op.RequiredSlots.Count == 0)
                continue;

            ops.Add(op);
            Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] AirliftCapture {obj.Sector}: desejado={desiredPassengers} qualified={qualifiedCapturers} embarked={embarkedCapturers} cap_deficit={capturerDeficit} air_deficit={airDeficit}");
        }
    }

    private void TryBuildPreventiveDefenseOp(TeamId team, AIWorldSnapshot snapshot, List<AIOperation> ops)
    {
        if (snapshot == null)
            return;

        int minTurn = AIShoppingPlanner.Instance != null ? AIShoppingPlanner.Instance.MinTurnBaseDefense : 3;
        bool rich = HasPreventiveDefenseBudget(snapshot);
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn && !rich)
            return;

        int activeArt = CountActiveNeed(snapshot, AINeedKind.Artillery) + CountActiveNeed(snapshot, AINeedKind.FireSupport);
        int activeAAA = CountActiveNeed(snapshot, AINeedKind.AAA);
        int activeSAM = CountActiveNeed(snapshot, AINeedKind.SAM);

        int artDeficit = Mathf.Max(0, InstanceMinBaseArtillery() - activeArt);
        int aaaDeficit = Mathf.Max(0, InstanceMinBaseAAA() - activeAAA);
        int samDeficit = activeAAA >= 1 && activeSAM < 1 ? 1 : 0;
        if (artDeficit <= 0 && aaaDeficit <= 0 && samDeficit <= 0)
            return;

        AIOperation op = CreateOperation(team, AIOperationType.PreventiveDefense, 6, snapshot, snapshot.MyHQ != null ? snapshot.MyHQ.Sector : ConstructionSector.Base1);
        op.IsPreventive = true;
        op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
        op.TargetCell = op.AnchorCell;
        op.AddSlots(AINeedKind.Artillery, artDeficit);
        op.AddSlots(AINeedKind.AAA, aaaDeficit);
        op.AddSlots(AINeedKind.SAM, samDeficit);

        ops.Add(op);
        Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] PreventiveDefense: Artilleryx{artDeficit} AAAx{aaaDeficit} SAMx{samDeficit} activeArt={activeArt} activeAAA={activeAAA} activeSAM={activeSAM}");
    }

    private void TryBuildAirRefuelSupportOp(TeamId team, AIWorldSnapshot snapshot, List<AIOperation> ops)
    {
        if (snapshot?.MyUnits == null)
            return;

        int lowFuelAircraft = 0;
        int criticalFuelAircraft = 0;
        int airFleet = 0;

        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked) continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.domain != Domain.Air) continue;
            if (UnitDataSatisfiesNeed(data, AINeedKind.AirTanker)) continue;

            airFleet++;
            int maxFuel = Mathf.Max(1, unit.GetMaxFuel());
            float fuelPct = unit.CurrentFuel * 100f / maxFuel;
            if (fuelPct <= AirRefuelLowFuelPct)
                lowFuelAircraft++;
            if (fuelPct <= AirRefuelCriticalFuelPct)
                criticalFuelAircraft++;
        }

        if (lowFuelAircraft <= 0)
            return;

        int desiredTankers = 1;
        if ((airFleet >= 8 && lowFuelAircraft >= 4) || criticalFuelAircraft >= 2)
            desiredTankers = 2;

        int activeTankers = CountActiveNeed(snapshot, AINeedKind.AirTanker);
        int tankerDeficit = Mathf.Max(0, desiredTankers - activeTankers);
        if (tankerDeficit <= 0)
        {
            Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] AirRefuelSupport coberto: lowFuel={lowFuelAircraft} critical={criticalFuelAircraft} activeTankers={activeTankers}/{desiredTankers}");
            return;
        }

        AIOperation op = CreateOperation(team, AIOperationType.AirRefuelSupport, 3, snapshot, snapshot.MyHQ != null ? snapshot.MyHQ.Sector : ConstructionSector.Base1);
        op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
        op.TargetCell = op.AnchorCell;
        op.AddSlots(AINeedKind.AirTanker, tankerDeficit);

        ops.Add(op);
        Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] AirRefuelSupport: lowFuel={lowFuelAircraft} critical={criticalFuelAircraft} airFleet={airFleet} activeTankers={activeTankers} deficit={tankerDeficit}");
    }

    private AIOperation CreateOperation(TeamId team, AIOperationType type, int priority, AIWorldSnapshot snapshot, ConstructionSector sector)
    {
        int turn = snapshot != null ? snapshot.TurnNumber : 0;
        return new AIOperation
        {
            Id = nextId++,
            Type = type,
            Phase = AIOperationPhase.Forming,
            Sector = sector,
            Team = team,
            Priority = priority,
            CreatedTurn = turn,
            LastUpdatedTurn = turn,
        };
    }

    private void AssignExistingUnitsToOperations(TeamId team, AIWorldSnapshot snapshot, List<AIOperation> ops)
    {
        var used = new HashSet<int>();
        foreach (AIOperation op in ops)
        {
            foreach (AIOperationSlotNeed slot in op.RequiredSlots)
            {
                UnitManager unit = FindLinkedObjectiveUnit(op.LinkedObjective, slot.Kind, used);
                if (unit == null && CanUseGlobalUnitFill(op))
                    unit = FindAnyUnitForNeed(snapshot, slot.Kind, used);
                if (unit == null)
                    continue;

                slot.Filled = true;
                slot.AssignedUnitId = unit.InstanceId;
                if (!op.AssignedUnitIds.Contains(unit.InstanceId))
                    op.AssignedUnitIds.Add(unit.InstanceId);
                used.Add(unit.InstanceId);
            }
        }
    }

    private static bool CanUseGlobalUnitFill(AIOperation op)
    {
        return op != null
            && (op.Type == AIOperationType.BaseDefense
                || op.Type == AIOperationType.PreventiveDefense);
    }

    private UnitManager FindLinkedObjectiveUnit(SectorObjective obj, AINeedKind kind, HashSet<int> used)
    {
        if (obj == null || obj.Slots == null)
            return null;
        foreach (SlotNeed slot in obj.Slots)
        {
            if (!slot.Filled || used.Contains(slot.AssignedUnitId)) continue;
            UnitManager unit = FindActiveUnit(slot.AssignedUnitId);
            if (UnitSatisfiesNeed(unit, kind))
                return unit;
        }
        return null;
    }

    private UnitManager FindAnyUnitForNeed(AIWorldSnapshot snapshot, AINeedKind kind, HashSet<int> used)
    {
        if (snapshot?.MyUnits == null)
            return null;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsUnderRepair || used.Contains(unit.InstanceId)) continue;
            if (UnitSatisfiesNeed(unit, kind))
                return unit;
        }
        return null;
    }

    private static UnitManager FindActiveUnit(int id)
    {
        foreach (UnitManager unit in UnitManager.AllActive)
            if (unit != null && !unit.IsDead && unit.InstanceId == id)
                return unit;
        return null;
    }

    private void InferPhasesForAllOps(AIWorldSnapshot snapshot, List<AIOperation> ops)
    {
        foreach (AIOperation op in ops)
        {
            if (op.LinkedObjective != null && op.LinkedObjective.Status == ObjectiveStatus.Capturing)
            {
                op.Phase = AIOperationPhase.Capturing;
                continue;
            }
            if (op.LinkedObjective != null && op.LinkedObjective.Status == ObjectiveStatus.Defending)
            {
                op.Phase = AIOperationPhase.Holding;
                continue;
            }
            if (HasEnemyNearCell(snapshot, op.TargetCell, 2) && op.AssignedUnitIds.Count > 0)
            {
                op.Phase = AIOperationPhase.Engaging;
                continue;
            }
            if (op.AssignedUnitIds.Count > 0 && DistanceAssignedToTarget(op) > 3)
            {
                op.Phase = AIOperationPhase.Moving;
                continue;
            }
            op.Phase = op.AssignedUnitIds.Count == 0 && HasOpenAnySlot(op)
                ? AIOperationPhase.Forming
                : AIOperationPhase.Holding;
        }
    }

    private void LogOperations(TeamId team, int turn, List<AIOperation> ops)
    {
        foreach (AIOperation op in ops)
            Debug.Log($"[AI Ops][T{turn}][{team}] {op.Type} {op.Sector} pri={op.Priority} phase={op.Phase} urgent={op.IsUrgent} preventive={op.IsPreventive} slots={DescribeSlots(op)} assigned={op.AssignedUnitIds.Count}");
    }

    private static string DescribeSlots(AIOperation op)
    {
        var counts = new Dictionary<AINeedKind, int>();
        foreach (AIOperationSlotNeed slot in op.RequiredSlots)
        {
            counts.TryGetValue(slot.Kind, out int count);
            counts[slot.Kind] = count + 1;
        }

        var parts = new List<string>();
        foreach (KeyValuePair<AINeedKind, int> kv in counts)
            if (kv.Value > 0)
                parts.Add($"{kv.Key}x{kv.Value}");
        return parts.Count > 0 ? string.Join(" ", parts) : "-";
    }

    private static bool HasOpenAnySlot(AIOperation op)
    {
        foreach (AIOperationSlotNeed slot in op.RequiredSlots)
            if (!slot.Filled)
                return true;
        return false;
    }

    private static int DistanceAssignedToTarget(AIOperation op)
    {
        int best = int.MaxValue;
        foreach (int id in op.AssignedUnitIds)
        {
            UnitManager unit = FindActiveUnit(id);
            if (unit == null) continue;
            Vector3Int cell = Normalize(unit.CurrentCellPosition);
            best = Mathf.Min(best, Mathf.RoundToInt(SectorManager.HexDistance(cell, op.TargetCell)));
        }
        return best == int.MaxValue ? 0 : best;
    }

    public static bool UnitDataSatisfiesNeed(UnitData data, AINeedKind kind)
    {
        if (data == null)
            return false;

        switch (kind)
        {
            case AINeedKind.Capturer:
                return IsPrimaryRole(data, UnitRole.Capturador);
            case AINeedKind.Assault:
                return IsPrimaryRole(data, UnitRole.Assalto) && !IsAntiAirOnlyUnit(data);
            case AINeedKind.AAA:
                return IsAntiAirOnlyUnit(data) && IsPrimaryRole(data, UnitRole.Assalto);
            case AINeedKind.SAM:
                return IsAntiAirOnlyUnit(data) && IsPrimaryRole(data, UnitRole.FogoIndireto);
            case AINeedKind.Artillery:
            case AINeedKind.FireSupport:
                return IsPrimaryRole(data, UnitRole.FogoIndireto) && !IsAntiAirOnlyUnit(data);
            case AINeedKind.AirTransport:
                return IsPrimaryRole(data, UnitRole.Transportador) && data.domain == Domain.Air;
            case AINeedKind.GroundTransport:
                return IsPrimaryRole(data, UnitRole.Transportador) && data.domain == Domain.Land;
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
            if (HasEnemyNearCell(snapshot, building.CurrentCellPosition, range))
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

    private static int CountOwnedHomeConstructionsUnderCapture(AIWorldSnapshot snapshot, TeamId team)
    {
        if (snapshot?.MyBuildings == null)
            return 0;
        int count = 0;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (!IsCriticalHomeConstruction(building, team)) continue;
            if (IsOwnedConstructionUnderCapture(building, team))
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

    private static bool HasPreventiveDefenseBudget(AIWorldSnapshot snapshot)
    {
        if (snapshot == null) return false;
        int income = Mathf.Max(1, snapshot.IncomePerTurn);
        return snapshot.Budget >= 40000 || snapshot.Budget >= Mathf.Max(20000, income * 2);
    }

    private static Vector3Int Normalize(Vector3Int cell)
    {
        cell.z = 0;
        return cell;
    }
}
