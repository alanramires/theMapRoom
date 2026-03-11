using UnityEngine;
using UnityEngine.Tilemaps;

public enum AircraftOperationAction
{
    None = 0,
    Land = 1,
    Takeoff = 2
}

public readonly struct AircraftOperationDecision
{
    public readonly bool available;
    public readonly AircraftOperationAction action;
    public readonly bool consumesAction;
    public readonly string label;
    public readonly string reason;

    public AircraftOperationDecision(
        bool available,
        AircraftOperationAction action,
        bool consumesAction,
        string label,
        string reason)
    {
        this.available = available;
        this.action = action;
        this.consumesAction = consumesAction;
        this.label = label ?? string.Empty;
        this.reason = reason ?? string.Empty;
    }
}

public static class AircraftOperationRules
{
    public static AircraftOperationDecision Evaluate(
        UnitManager unit,
        Tilemap referenceTilemap,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode)
    {
        if (unit == null)
            return Unavailable("Unidade invalida.");

        if (!unit.TryGetUnitData(out UnitData data) || data == null)
            return Unavailable("UnitData nao encontrado.");

        if (!HasAirOperationProfile(unit, data))
            return Unavailable("Unidade sem perfil de operacao aerea.");

        if (unit.TryGetForcedLayerLock(out Domain lockDomain, out HeightLevel lockHeight, out int lockTurns))
        {
            string lockReason = PanelDialogController.ResolveDialogMessage(
                "layer.locked.by.weapon",
                "Camada travada em <domain>/<height> por <turns> turno(s).",
                new System.Collections.Generic.Dictionary<string, string>
                {
                    { "unit", !string.IsNullOrWhiteSpace(unit.UnitDisplayName) ? unit.UnitDisplayName : unit.name },
                    { "domain", lockDomain.ToString() },
                    { "height", lockHeight.ToString() },
                    { "turns", lockTurns.ToString() }
                });
            return Unavailable(lockReason);
        }

        if (unit.AircraftOperationLockTurns > 0)
            return Unavailable("Aeronave em recuperacao operacional.");

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        AirOperationTileContext tileContext = AirOperationResolver.ResolveContext(referenceTilemap, terrainDatabase, cell);

        if (unit.GetDomain() == Domain.Air && !unit.IsAircraftGrounded)
            return EvaluateLanding(unit, movementMode, tileContext);

        return EvaluateTakeoff(unit, movementMode, tileContext);
    }

    public static bool TryApplyOperation(
        UnitManager unit,
        Tilemap referenceTilemap,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        out AircraftOperationDecision decision)
    {
        decision = Evaluate(unit, referenceTilemap, terrainDatabase, movementMode);
        if (!decision.available)
            return false;

        if (!unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        if (decision.action == AircraftOperationAction.Land)
        {
            unit.TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface);
            unit.SetAircraftGrounded(true);
            unit.SetAircraftEmbarkedInCarrier(false);
            unit.SetAircraftOperationLockTurns(0);
            return true;
        }

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        AirOperationTileContext tileContext = AirOperationResolver.ResolveContext(referenceTilemap, terrainDatabase, cell);
        HeightLevel endHeight = ResolvePreferredAirHeight(data);
        if (AirOperationResolver.TryGetTakeoffPlan(unit, tileContext, movementMode, out AirTakeoffPlan takeoffPlan, out _))
            endHeight = takeoffPlan.endHeight;

        unit.TrySetCurrentLayerMode(Domain.Air, endHeight);
        unit.SetAircraftGrounded(false);
        unit.SetAircraftEmbarkedInCarrier(false);
        unit.SetAircraftOperationLockTurns(0);
        return true;
    }

    private static AircraftOperationDecision EvaluateLanding(
        UnitManager unit,
        SensorMovementMode movementMode,
        AirOperationTileContext tileContext)
    {
        if (!AirOperationResolver.CanLand(unit, tileContext, movementMode, out string reason))
            return Unavailable(reason);

        return Available(AircraftOperationAction.Land, consumesAction: true, BuildLandingLabel(tileContext.source));
    }

    private static AircraftOperationDecision EvaluateTakeoff(UnitManager unit, SensorMovementMode movementMode, AirOperationTileContext tileContext)
    {
        if (!AirOperationResolver.CanTakeoff(unit, tileContext, movementMode, out string reason))
            return Unavailable(reason);

        return Available(AircraftOperationAction.Takeoff, consumesAction: false, BuildTakeoffLabel(tileContext.source));
    }

    private static bool HasAirOperationProfile(UnitManager unit, UnitData data)
    {
        if (data == null)
            return false;

        return AirOperationResolver.UnitHasAnyAircraftSkill(unit);
    }

    private static string BuildLandingLabel(AirOperationRuleSource source)
    {
        switch (source)
        {
            case AirOperationRuleSource.Construction:
                return "Pousar (Construcao)";
            case AirOperationRuleSource.Structure:
                return "Pousar (Estrutura)";
            case AirOperationRuleSource.Terrain:
                return "Pousar (Terreno)";
            default:
                return "Pousar";
        }
    }

    private static string BuildTakeoffLabel(AirOperationRuleSource source)
    {
        switch (source)
        {
            case AirOperationRuleSource.Construction:
                return "Decolar (Construcao)";
            case AirOperationRuleSource.Structure:
                return "Decolar (Estrutura)";
            case AirOperationRuleSource.Terrain:
                return "Decolar (Terreno)";
            default:
                return "Decolar";
        }
    }

    private static HeightLevel ResolvePreferredAirHeight(UnitData data)
    {
        if (data == null)
            return HeightLevel.AirLow;

        if (data.domain == Domain.Air && (data.heightLevel == HeightLevel.AirLow || data.heightLevel == HeightLevel.AirHigh))
            return data.heightLevel;

        if (data.aditionalDomainsAllowed != null)
        {
            for (int i = 0; i < data.aditionalDomainsAllowed.Count; i++)
            {
                UnitLayerMode mode = data.aditionalDomainsAllowed[i];
                if (mode.domain == Domain.Air && (mode.heightLevel == HeightLevel.AirLow || mode.heightLevel == HeightLevel.AirHigh))
                    return mode.heightLevel;
            }
        }

        return HeightLevel.AirLow;
    }

    private static AircraftOperationDecision Available(AircraftOperationAction action, bool consumesAction, string label)
    {
        return new AircraftOperationDecision(true, action, consumesAction, label, string.Empty);
    }

    private static AircraftOperationDecision Unavailable(string reason)
    {
        return new AircraftOperationDecision(false, AircraftOperationAction.None, false, string.Empty, reason);
    }

}
