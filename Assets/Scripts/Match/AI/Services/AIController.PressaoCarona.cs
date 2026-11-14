using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    /// <summary>
    /// O pedido de carona que o PressaoCaronaService usa: o MESMO do capturador
    /// (BuildCapturerRideRequest), com o objetivo resolvido da alocacao do plano.
    /// Montar outro pedido faria a pressao responder outra pergunta e cair em outra
    /// entrada do cache do QueroCarona.
    /// </summary>
    internal QueroCaronaRequest BuildRidePressureRequest(
        UnitManager unit, TeamObjectivePlan plan, out bool comPlano)
    {
        if (TryResolveRallyRideTarget(unit, plan, null, out Vector3Int invasionTarget))
        {
            comPlano = true;
            return BuildRallyAdvanceRideRequest(unit, invasionTarget, 2);
        }
        SectorObjective assigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;
        comPlano = assigned != null;
        return BuildCapturerRideRequestWithTarget(unit, assigned);
    }

    // Inert UnitManager adapters are the same mechanism used by model reach envelopes.
    internal List<UnitManager> BuildRidePressureAirModels(int slot)
    {
        var data = new HashSet<UnitData>();
        foreach (UnitManager u in UnitManager.AllActive)
            if (u != null && !u.IsDead && u.SlotIndex == slot && u.TryGetUnitData(out UnitData d)
                && d.domain == Domain.Air && UnitRoleCompatibility.IsOperationalTransporter(d)) data.Add(d);
        foreach (ConstructionManager b in ConstructionManager.AllActive)
            if (b != null && b.SlotIndex == slot && b.CanProduceUnitsForSlot(slot) && b.OfferedUnits != null)
                foreach (UnitData d in b.OfferedUnits)
                    if (d != null && d.domain == Domain.Air && UnitRoleCompatibility.IsOperationalTransporter(d)
                        && !(RespeitaListaBanida && d.bannedOnHardMode)) data.Add(d);
        var models = new List<UnitManager>();
        foreach (UnitData d in data) models.Add(UnitManager.CreateDetachedPlanningModel(d,
            Match != null ? Match.GetTeamIdForSlot(slot) : TeamId.Blue,
            slot, boardTilemap, Vector3Int.zero));
        return models;
    }

    internal bool HasKnownAirDeliveryForRide(UnitManager passenger, QueroCaronaResult ride,
        List<UnitManager> models)
    {
        if (passenger == null || ride == null || models.Count == 0 || boardTilemap == null
            || terrainDatabase == null || !passenger.TryGetUnitData(out UnitData passengerData)) return false;
        var target = ride.evaluatedTarget; target.z = 0;
        var building = ride.evaluatedConstruction
            ?? ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, target);
        if (building == null || !building.IsCapturable) return false;
        bool Known(Vector3Int cell) => Match != null
            && Match.IsCellVisibleOrExploredForSlot(PlayerSlotId.FromIndex(passenger.SlotIndex), cell);
        if (!Known(target)) return false;
        var compatible = new List<UnitManager>();
        foreach (UnitManager model in models)
            if (model.TryGetUnitData(out UnitData d) && d.transportSlots != null
                && d.transportSlots.Exists(slot => PodeEmbarcarSensor.CanUseSlot(passenger, passengerData, slot, out _)))
                compatible.Add(model);
        if (compatible.Count == 0) return false;
        var reach = UnitReachEnvelopeService.Build(new UnitReachRequest {
            Unit = passenger, BoardMap = boardTilemap, TerrainDatabase = terrainDatabase,
            OriginOverride = target, Intent = ReachIntent.Mobility, Band = ReachBand.Operational,
            MovementMode = UnitThreatEnvelopeMovement.Potential, SubStep = ReachSubStep.Terrestre });
        if (reach == null) return false;
        var lzs = new List<Vector3Int>(7);
        foreach (var drop in reach.MovementCells)
        {
            if (!Known(drop)) continue;
            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, drop, lzs);
            lzs.Add(drop);
            foreach (var lz in lzs)
            {
                if (!Known(lz)) continue;
                foreach (UnitManager model in compatible)
                    if (CanAirTransportStopAtCell(model, lz)
                        && PodeDesembarcarSensor.CanDeliverPassengerFromCell(model, passenger, lz, drop,
                            boardTilemap, terrainDatabase, out _)) return true;
            }
        }
        return false;
    }
}
