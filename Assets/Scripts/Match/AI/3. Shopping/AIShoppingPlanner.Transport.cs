using System.Collections.Generic;
using UnityEngine;

// Demanda de transporte terrestre e aéreo: APC, helicóptero e seed de passageiros.
public partial class AIShoppingPlanner
{
    private static int ComputeTransportDemand(AIWorldSnapshot snapshot, out bool urgentTransportDemand)
    {
        urgentTransportDemand = false;
        TeamId aiTeam = snapshot.AITeam;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(aiTeam);
        if (plan == null) return 0;

        int minDist = AIController.Instance != null
            ? AIController.Instance.GetEffectiveTransportThreshold(aiTeam) : 7;

        int activeTransporters = 0;
        int freeAPCs = 0;
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.TeamId != aiTeam || u.IsDead || u.IsEmbarked) continue;
            if (!u.TryGetUnitData(out UnitData d)) continue;
            if (d.roles == null || d.roles.Count == 0 || d.roles[0] != UnitRole.Transportador) continue;
            if (d.domain == Domain.Air) continue;
            activeTransporters++;
            bool hasCargo = false;
            if (u.TransportedUnitSlots != null)
                foreach (UnitTransportSeatRuntime seat in u.TransportedUnitSlots)
                    if (seat.embarkedUnit != null && seat.embarkedUnit.IsEmbarked) { hasCargo = true; break; }
            if (!hasCargo) freeAPCs++;
        }

        int activeCapturers = CountActiveUnitsWithRole(snapshot, UnitRole.Capturador, requirePrimary: false);
        int activeAssault = CountActiveUnitsWithRole(snapshot, UnitRole.Assalto, requirePrimary: true);
        int openCapturerSlots = CountOpenSlots(aiTeam, UnitRole.Capturador);
        int assignedNeeded = 0;
        int preventiveNeeded = 0;
        int projectionNeeded = 0;
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned)
                continue;

            bool hasTransportSlot = false;
            bool hasOpenTransportSlot = false;
            foreach (SlotNeed slot in obj.Slots)
            {
                if (slot.Role != UnitRole.Transportador) continue;
                hasTransportSlot = true;
                if (!slot.Filled) hasOpenTransportSlot = true;
            }
            if (!hasTransportSlot) continue;

            ConstructionManager tgt = AIController.FindCapturableInSector(obj.Sector, aiTeam);
            if (tgt == null) continue;

            bool sectorInfoFound = SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info);
            if (sectorInfoFound && info.GetTransportPreference(aiTeam) == SectorManager.SectorInfo.TransportPreference.Air)
                continue;

            if (hasOpenTransportSlot
                && activeCapturers >= 2
                && activeAssault >= 1
                && ObjectiveHasOpenOrFilledCapturer(obj)
                && sectorInfoFound
                && info.GetDistanceToHQ(aiTeam) >= minDist)
            {
                preventiveNeeded++;
            }

            UnitManager capturer = null;
            foreach (SlotNeed slot in obj.Slots)
            {
                if (slot.Role != UnitRole.Capturador || !slot.Filled) continue;
                foreach (UnitManager u in UnitManager.AllActive)
                    if (u.InstanceId == slot.AssignedUnitId && !u.IsDead) { capturer = u; break; }
                if (capturer != null) break;
            }
            if (capturer == null || capturer.IsEmbarked) continue;

            Vector3Int capCell = capturer.CurrentCellPosition; capCell.z = 0;
            Vector3Int tgtCell = tgt.CurrentCellPosition; tgtCell.z = 0;
            float dist = SectorManager.HexDistance(capCell, tgtCell);

            if (dist >= minDist) assignedNeeded++;
        }

        bool anchorsReady = AreOwnAnchorSectorsHeld(aiTeam, out int heldAnchors, out int totalAnchors);
        if (anchorsReady
            && activeTransporters <= 0
            && HasLongRangeGroundProjectionOpportunity(snapshot, minDist,
                out ConstructionSector projectionSector,
                out float projectionFootDistance,
                out float projectionVehicleDistance,
                out float projectionAirDistance))
        {
            int waitingHomeCapturers = CountGroundCapturersWaitingNearHome(snapshot);
            int batchSize = Instance != null ? Instance.ProgressiveCapturerBatchSize : 2;
            int incomingCapturers = openCapturerSlots > 0 ? batchSize : 0;
            int projectionPassengers = waitingHomeCapturers + incomingCapturers;
            if (projectionPassengers > 0)
            {
                projectionNeeded = 1;
                Debug.Log($"[AI Shopping] transport_projection: anchors={heldAnchors}/{totalAnchors} sector={projectionSector} foot={projectionFootDistance:F1}>={minDist} veh={projectionVehicleDistance:F1} air={projectionAirDistance:F1} openCap={openCapturerSlots} incoming={incomingCapturers} waitingHome={waitingHomeCapturers} activeCap={activeCapturers} -> APC");
            }
        }

        CountSlots(snapshot.AITeam, UnitRole.Capturador, out int totalCapSlotsForMass, out int _);
        int capturersPerTransport;
        if (totalCapSlotsForMass > 0)
        {
            float massRatio = (Instance != null ? Instance.EliteCapturerFillRatio : 0.6f) * 0.85f;
            capturersPerTransport = Mathf.Max(1, Mathf.CeilToInt(totalCapSlotsForMass * massRatio));
        }
        else
        {
            capturersPerTransport = Instance != null ? Instance.CapturersPerPreventiveTransport : 4;
        }
        int massNeeded = 0;
        if (activeCapturers >= capturersPerTransport && activeAssault >= 1)
            massNeeded = activeCapturers / Mathf.Max(1, capturersPerTransport);

        int endgameNeeded = ComputeEndgameTransportDemand(
            snapshot, plan, activeCapturers, activeAssault, openCapturerSlots,
            out string endgameReason);

        int preventiveTarget = Mathf.Max(preventiveNeeded, Mathf.Max(massNeeded, Mathf.Max(projectionNeeded, endgameNeeded)));
        int assignedDeficit = Mathf.Max(0, assignedNeeded - freeAPCs);
        int preventiveDeficit = Mathf.Max(0, preventiveTarget - activeTransporters);
        urgentTransportDemand = assignedDeficit > 0;
        int needed = Mathf.Max(assignedNeeded, preventiveTarget);
        int deficit = urgentTransportDemand ? assignedDeficit : preventiveDeficit;
        int demand  = Mathf.Min(deficit, 1);
        Debug.Log($"[AI Shopping] transport_demand: needed={needed} assigned={assignedNeeded} preventive={preventiveNeeded} projection={projectionNeeded} mass={massNeeded} endgame={endgameNeeded}({endgameReason}) capPerTrans={capturersPerTransport} activeCap={activeCapturers} activeAss={activeAssault} activeAPCs={activeTransporters} freeAPCs={freeAPCs} anchors={heldAnchors}/{totalAnchors} assignedDef={assignedDeficit} preventiveDef={preventiveDeficit} urgent={urgentTransportDemand} demand={demand} minDist={minDist}");
        return demand;
    }

    private static int ComputeEndgameTransportDemand(
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        int activeCapturers,
        int activeAssault,
        int openCapturerSlots,
        out string reason)
    {
        reason = "off";
        if (snapshot == null || plan == null)
            return 0;

        if (activeAssault <= 0)
        {
            reason = "semAssalto";
            return 0;
        }

        if (!HasGroundEndgameObjective(snapshot.AITeam, plan, out ConstructionSector sector))
            return 0;

        int batchSize = Instance != null ? Instance.ProgressiveCapturerBatchSize : 2;
        int incomingCapturers = openCapturerSlots > 0 ? Mathf.Min(openCapturerSlots, batchSize) : 0;
        int projectedCapturers = activeCapturers + incomingCapturers;
        if (projectedCapturers < 3)
        {
            reason = $"{sector} cap={projectedCapturers}<3";
            return 0;
        }

        int desiredFleet = Mathf.Clamp(Mathf.CeilToInt(projectedCapturers / 4f), 1, 2);
        reason = $"{sector} cap={projectedCapturers} incoming={incomingCapturers} fleet={desiredFleet}";
        return desiredFleet;
    }

    private static bool HasGroundEndgameObjective(TeamId aiTeam, TeamObjectivePlan plan, out ConstructionSector sector)
    {
        sector = ConstructionSector.None;
        if (plan == null || plan.Objectives == null)
            return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned)
                continue;

            if (obj.ObjectiveType != AIObjectiveType.InvasionAttack && !ConstructionSectorHelper.IsBase(obj.Sector))
                continue;

            ConstructionManager target = AIController.FindCapturableInSector(obj.Sector, aiTeam);
            if (target == null)
                continue;

            if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info)
                && info.GetTransportPreference(aiTeam) == SectorManager.SectorInfo.TransportPreference.Air)
                continue;

            sector = obj.Sector;
            return true;
        }

        return false;
    }

    private static bool AreOwnAnchorSectorsHeld(TeamId aiTeam, out int heldAnchors, out int totalAnchors)
    {
        heldAnchors = 0;
        totalAnchors = 0;

        HashSet<int> ownSlots = CollectOwnHQSlotsForShopping(aiTeam);
        if (ownSlots.Count == 0)
            return false;

        HashSet<ConstructionSector> counted = new HashSet<ConstructionSector>();
        foreach (ConstructionManager anchor in ConstructionManager.AllActive)
        {
            if (anchor == null || !anchor.IsAnchorSector)
                continue;
            if (anchor.Sector == ConstructionSector.None)
                continue;
            if (!ownSlots.Contains(anchor.AnchorSectorSlotIndex))
                continue;
            if (!counted.Add(anchor.Sector))
                continue;

            totalAnchors++;
            if (SectorManager.TryGetSectorInfo(anchor.Sector, out SectorManager.SectorInfo info)
                && info.IsFullyControlled
                && info.ControllingTeam == aiTeam)
            {
                heldAnchors++;
            }
        }

        return totalAnchors > 0 && heldAnchors >= totalAnchors;
    }

    private static HashSet<int> CollectOwnHQSlotsForShopping(TeamId aiTeam)
    {
        HashSet<int> slots = new HashSet<int>();
        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || !construction.IsPlayerHeadQuarter)
                continue;
            if (construction.TeamId != aiTeam)
                continue;
            if (construction.SlotIndex < 0)
                continue;

            slots.Add(construction.SlotIndex);
        }

        return slots;
    }

    private static bool HasLongRangeGroundProjectionOpportunity(
        AIWorldSnapshot snapshot,
        int minDist,
        out ConstructionSector sector,
        out float footDistance,
        out float vehicleDistance,
        out float airDistance)
    {
        sector = ConstructionSector.None;
        footDistance = 0f;
        vehicleDistance = 0f;
        airDistance = 0f;
        if (snapshot == null)
            return false;

        TeamId aiTeam = snapshot.AITeam;
        foreach (SectorManager.SectorInfo info in SectorManager.GetAllSectorInfos())
        {
            if (info == null)
                continue;
            if (info.IsFullyControlled && info.ControllingTeam == aiTeam)
                continue;
            if (ConstructionSectorHelper.IsBase(info.Sector))
                continue;
            if (info.GetTransportPreference(aiTeam) == SectorManager.SectorInfo.TransportPreference.Air)
                continue;

            float foot = info.GetDistanceToHQ(aiTeam);
            if (foot < minDist)
                continue;

            ConstructionManager target = AIController.FindCapturableInSector(info.Sector, aiTeam);
            if (target == null)
                continue;

            sector = info.Sector;
            footDistance = foot;
            vehicleDistance = info.GetVehicleDistanceToHQ(aiTeam);
            airDistance = info.GetAirDistanceToHQ(aiTeam);
            return true;
        }

        return false;
    }

    private static int ComputeAirTransportDemand(AIWorldSnapshot snapshot, int openCapturerSlots = 0)
    {
        TeamId aiTeam = snapshot.AITeam;

        if (!MapNeedsAirTransport(snapshot, out int minDist))
        {
            Debug.Log($"[AI Shopping] air_transport_demand: mapa pequeno (threshold={minDist}) → demand=0");
            return 0;
        }

        int activeAirTransporters = CountAirTransporters(snapshot, requireEmpty: false);
        int activeGroundCapturers = 0;
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.TeamId != aiTeam || u.IsDead || u.IsEmbarked) continue;
            if (!u.TryGetUnitData(out UnitData d)) continue;
            if (d.roles == null || d.roles.Count == 0) continue;
            if (d.roles[0] == UnitRole.Capturador && d.domain == Domain.Land)
                activeGroundCapturers++;
        }

        const int HeliCapacity = 2;
        int pickupCapturers = CountAirTransportPickupCapturers(snapshot);
        int troopsNeedingTransport;
        if (activeGroundCapturers == 0)
        {
            troopsNeedingTransport = openCapturerSlots;
        }
        else
        {
            int batchSize = Instance != null ? Instance.ProgressiveCapturerBatchSize : 2;
            int incomingCapturers = openCapturerSlots > 0 ? batchSize : 0;
            troopsNeedingTransport = pickupCapturers + incomingCapturers;
        }
        if (troopsNeedingTransport <= 0)
        {
            Debug.Log($"[AI Shopping] air_transport_demand: 0 sem passageiro pickup/base groundCap={activeGroundCapturers} pickupCap={pickupCapturers} openCapSlots={openCapturerSlots} activeAirTrans={activeAirTransporters} minDist={minDist}");
            return 0;
        }
        int helicoptersNeeded = Mathf.CeilToInt((float)troopsNeedingTransport / HeliCapacity);

        int maxFleet = Instance != null ? Instance.MaxAirTransporters : 3;
        int demand = Mathf.Max(0, Mathf.Min(helicoptersNeeded, maxFleet) - activeAirTransporters);
        Debug.Log($"[AI Shopping] air_transport_demand: groundCap={activeGroundCapturers} pickupCap={pickupCapturers} openCapSlots={openCapturerSlots} troops={troopsNeedingTransport} heliCap={HeliCapacity} heliNeeded={helicoptersNeeded} activeAirTrans={activeAirTransporters} maxFleet={maxFleet} minDist={minDist} demand={demand}");
        return demand;
    }

    private static bool MapNeedsAirTransport(AIWorldSnapshot snapshot, out int minDist)
    {
        TeamId aiTeam = snapshot != null ? snapshot.AITeam : TeamId.Neutral;
        minDist = AIController.Instance != null
            ? AIController.Instance.GetEffectiveTransportThreshold(aiTeam) : 7;

        foreach (SectorManager.SectorInfo info in SectorManager.GetAllSectorInfos())
        {
            if (info.IsFullyControlled && info.ControllingTeam == aiTeam) continue;
            if (info.GetDistanceToHQ(aiTeam) >= minDist) return true;
        }

        foreach (SectorManager.SectorInfo baseInfo in SectorManager.GetAllBaseInfos())
        {
            if (baseInfo.GetDistanceToHQ(aiTeam) >= minDist) return true;
        }

        return false;
    }

    private static int CountAirTransporters(AIWorldSnapshot snapshot, bool requireEmpty)
    {
        if (snapshot == null)
            return 0;

        int count = 0;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.TeamId != snapshot.AITeam || unit.IsDead || unit.IsEmbarked)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.domain != Domain.Air
                || UnitRoleCompatibility.ResolveCompositionRole(data) != UnitRole.Transportador)
                continue;
            if (requireEmpty && HasTransportCargo(unit))
                continue;
            count++;
        }

        return count;
    }

    private static bool HasTransportCargo(UnitManager unit)
    {
        if (unit == null || unit.TransportedUnitSlots == null)
            return false;
        foreach (UnitTransportSeatRuntime seat in unit.TransportedUnitSlots)
        {
            if (seat != null && seat.embarkedUnit != null && seat.embarkedUnit.IsEmbarked)
                return true;
        }
        return false;
    }

    private static int CountAirTransportPickupCapturers(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MyBuildings == null)
            return 0;

        var pickupCells = new List<Vector3Int>();
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForTeam(snapshot.AITeam))
                continue;
            if (!CanOfferAirTransporter(building))
                continue;

            Vector3Int cell = building.CurrentCellPosition;
            cell.z = 0;
            pickupCells.Add(cell);
        }

        if (pickupCells.Count == 0)
            return 0;

        const float PickupRadius = 3f;
        int count = 0;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.TeamId != snapshot.AITeam || unit.IsDead || unit.IsEmbarked || unit.IsUnderRepair)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || !IsPrimaryRole(data, UnitRole.Capturador) || data.domain != Domain.Land)
                continue;

            Vector3Int unitCell = unit.CurrentCellPosition;
            unitCell.z = 0;
            for (int i = 0; i < pickupCells.Count; i++)
            {
                if (SectorManager.HexDistance(unitCell, pickupCells[i]) <= PickupRadius)
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private static bool CanOfferAirTransporter(ConstructionManager building)
    {
        if (building == null || building.OfferedUnits == null)
            return false;
        foreach (UnitData unit in building.OfferedUnits)
        {
            if (unit == null || unit.domain != Domain.Air)
                continue;
            if (UnitRoleCompatibility.ResolveCompositionRole(unit) == UnitRole.Transportador)
                return true;
        }
        return false;
    }

    private static bool ObjectiveHasOpenOrFilledCapturer(SectorObjective obj)
    {
        if (obj == null || obj.Slots == null) return false;
        foreach (SlotNeed slot in obj.Slots)
            if (slot.Role == UnitRole.Capturador) return true;
        return false;
    }

    private static bool ObjectiveHasSlot(SectorObjective obj, UnitRole role)
    {
        if (obj == null || obj.Slots == null)
            return false;

        foreach (SlotNeed slot in obj.Slots)
            if (slot.Role == role)
                return true;

        return false;
    }

    private static bool ShouldSeedCapturerForNewAPC(
        AIWorldSnapshot snapshot,
        int openCapturerSlots,
        int pendingGroundCapturerBuys,
        int apcPassengerFollowupDemand)
    {
        if (snapshot == null)
            return false;
        if (openCapturerSlots > 0)
            return false;
        if (pendingGroundCapturerBuys > 0 || apcPassengerFollowupDemand > 0)
            return false;
        if (!HasGroundTransportObjectiveNeedingCapturer(snapshot)
            && !HasAnyOffensiveObjective(snapshot.AITeam))
            return false;
        if (CountGroundCapturersWaitingNearHome(snapshot) > 0)
            return false;

        return true;
    }

    private static bool HasGroundTransportObjectiveNeedingCapturer(AIWorldSnapshot snapshot)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForTeam(snapshot.AITeam);
        if (plan == null)
            return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned)
                continue;
            if (!ObjectiveHasSlot(obj, UnitRole.Transportador))
                continue;
            if (!ObjectiveHasOpenOrFilledCapturer(obj))
                continue;

            ConstructionManager target = AIController.FindCapturableInSector(obj.Sector, snapshot.AITeam);
            if (target == null)
                continue;

            if (SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info)
                && info.GetTransportPreference(snapshot.AITeam) == SectorManager.SectorInfo.TransportPreference.Air)
                continue;

            return true;
        }

        return false;
    }

    private static int CountGroundCapturersWaitingNearHome(AIWorldSnapshot snapshot)
    {
        if (snapshot?.MyUnits == null || snapshot.MyBuildings == null)
            return 0;

        const int PickupRange = 4;
        int count = 0;
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked || unit.IsUnderRepair)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.domain != Domain.Land)
                continue;
            if (data.roles == null || !data.roles.Contains(UnitRole.Capturador))
                continue;
            if (UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Transportador)
                continue;

            Vector3Int unitCell = unit.CurrentCellPosition;
            unitCell.z = 0;
            foreach (ConstructionManager building in snapshot.MyBuildings)
            {
                if (!IsCriticalHomeConstruction(building, snapshot.AITeam))
                    continue;

                Vector3Int baseCell = building.CurrentCellPosition;
                baseCell.z = 0;
                if (SectorManager.HexDistance(unitCell, baseCell) <= PickupRange)
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private static int FindCheapestAvailableTransportCost(AIWorldSnapshot snapshot)
    {
        int cheapest = 0;
        if (snapshot == null || snapshot.MyBuildings == null) return cheapest;
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null || !b.CanProduceUnitsForTeam(snapshot.AITeam) || b.OfferedUnits == null) continue;
            foreach (UnitData u in b.OfferedUnits)
            {
                if (u == null || u.domain != Domain.Land
                    || UnitRoleCompatibility.ResolveCompositionRole(u) != UnitRole.Transportador) continue;
                if (cheapest == 0 || u.cost < cheapest) cheapest = u.cost;
            }
        }
        return cheapest;
    }

    private static int FindCheapestAirTransportCost(AIWorldSnapshot snapshot)
    {
        int cheapest = 0;
        if (snapshot == null || snapshot.MyBuildings == null) return cheapest;
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null || !b.CanProduceUnitsForTeam(snapshot.AITeam) || b.OfferedUnits == null) continue;
            foreach (UnitData u in b.OfferedUnits)
            {
                if (u == null || u.domain != Domain.Air
                    || UnitRoleCompatibility.ResolveCompositionRole(u) != UnitRole.Transportador) continue;
                if (cheapest == 0 || u.cost < cheapest) cheapest = u.cost;
            }
        }
        return cheapest;
    }

    private static int FindCheapestAirCombatCost(AIWorldSnapshot snapshot)
    {
        int cheapest = 0;
        if (snapshot == null || snapshot.MyBuildings == null) return cheapest;
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null || !b.CanProduceUnitsForTeam(snapshot.AITeam) || b.OfferedUnits == null) continue;
            foreach (UnitData u in b.OfferedUnits)
            {
                if (u == null || u.domain != Domain.Air) continue;
                UnitRole r = u.roles != null && u.roles.Count > 0 ? u.roles[0] : UnitRole.None;
                if (r != UnitRole.Interceptador && r != UnitRole.AtaqueAereo) continue;
                if (cheapest == 0 || u.cost < cheapest) cheapest = u.cost;
            }
        }
        return cheapest;
    }

    private static int FindCheapestAirCombatCost(AIWorldSnapshot snapshot, UnitRole role, bool elite)
    {
        int cheapest = 0;
        if (snapshot == null || snapshot.MyBuildings == null) return cheapest;
        foreach (ConstructionManager b in snapshot.MyBuildings)
        {
            if (b == null || !b.CanProduceUnitsForTeam(snapshot.AITeam) || b.OfferedUnits == null) continue;
            foreach (UnitData u in b.OfferedUnits)
            {
                if (u == null || u.domain != Domain.Air) continue;
                if (!IsPrimaryRole(u, role)) continue;
                if ((u.eliteLevel >= 1) != elite) continue;
                if (cheapest == 0 || u.cost < cheapest) cheapest = u.cost;
            }
        }
        return cheapest;
    }
}
