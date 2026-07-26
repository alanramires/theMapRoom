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
    // atCell responde "e se a aeronave estivesse NAQUELE hex?" sem mover a unidade.
    // Tudo abaixo desta funcao (ResolveContext, ResolveGroundedLayerForCell,
    // CanEndLayerTransitionAtCell) ja recebia a celula por parametro; so a entrada
    // amarrava a consulta a posicao atual, obrigando quem perguntava sobre outro hex
    // a deslocar a unidade e restaurar depois. Omitido = hex atual, como sempre foi.
    public static AircraftOperationDecision Evaluate(
        UnitManager unit,
        Tilemap referenceTilemap,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        bool allowSameTeamAirBlockerForMovementTakeoff = false,
        Vector3Int? atCell = null)
    {
        if (unit == null)
            return Unavailable("Unidade invalida.");

        if (!unit.TryGetUnitData(out UnitData data) || data == null)
            return Unavailable("UnitData nao encontrado.");

        if (!data.IsAircraft())
            return Unavailable("Unidade selecionada nao e aeronave.");

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

        if (unit.LayerLockTurnsRemaining > 0)
            return Unavailable("Unidade sob trava de camada.");

        Vector3Int cell = atCell ?? unit.CurrentCellPosition;
        cell.z = 0;
        AirOperationTileContext tileContext = AirOperationResolver.ResolveContext(referenceTilemap, terrainDatabase, cell);

        if (unit.GetDomain() == Domain.Air && !unit.IsAircraftGrounded)
        {
            ResolveGroundedLayerForCell(unit, referenceTilemap, terrainDatabase, cell, out Domain landDomain, out HeightLevel landHeight);
            if (!UnitOccupancyRules.CanEndLayerTransitionAtCell(referenceTilemap, cell, unit, landDomain, landHeight, out UnitManager blocker))
            {
                string blockerName = blocker != null && !string.IsNullOrWhiteSpace(blocker.UnitDisplayName) ? blocker.UnitDisplayName : "aliado";
                return Unavailable($"Pouso indisponivel: camada Surface ocupada por {blockerName}.");
            }

            return EvaluateLanding(unit, movementMode, tileContext);
        }

        if (unit.GetDomain() == Domain.Air ||
            unit.GetHeightLevel() != HeightLevel.Surface ||
            !unit.IsAircraftGrounded)
        {
            return Unavailable("A aeronave precisa estar pousada em uma camada Surface para decolar.");
        }

        if (unit.CurrentFuel <= 0)
            return Unavailable("Autonomia insuficiente para decolagem.");

        AircraftOperationDecision takeoffDecision = EvaluateTakeoff(unit, movementMode, tileContext);
        if (!takeoffDecision.available)
            return takeoffDecision;

        // Decolagem: aeronave do mesmo time nao empilha na banda aerea
        // (inimigo coexiste -> hex contestado/dogfight). Durante a preparacao
        // de movimento, um aliado no ar nao bloqueia a decolagem em si; o hex
        // final ainda sera validado por CanEndMoveAsLayer.
        HeightLevel takeoffHeight = ResolvePreferredAirHeight(data);
        if (AirOperationResolver.TryGetTakeoffPlan(
                unit,
                tileContext,
                movementMode,
                out AirTakeoffPlan takeoffPlan,
                out _))
        {
            takeoffHeight = takeoffPlan.endHeight;
        }

        if (!UnitOccupancyRules.CanEndLayerTransitionAtCell(
                referenceTilemap,
                cell,
                unit,
                Domain.Air,
                takeoffHeight,
                out UnitManager airBlocker,
                ignoreSameTeamAirBlocker: allowSameTeamAirBlockerForMovementTakeoff))
        {
            string blockerName = airBlocker != null && !string.IsNullOrWhiteSpace(airBlocker.UnitDisplayName) ? airBlocker.UnitDisplayName : "aliado";
            return Unavailable($"Decolagem nao autorizada: espaco aereo ocupado por {blockerName}.");
        }

        return takeoffDecision;
    }

    public static bool TryApplyOperation(
        UnitManager unit,
        Tilemap referenceTilemap,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        out AircraftOperationDecision decision,
        bool allowSameTeamAirBlockerForMovementTakeoff = false)
    {
        decision = Evaluate(unit, referenceTilemap, terrainDatabase, movementMode, allowSameTeamAirBlockerForMovementTakeoff);
        if (!decision.available)
            return false;

        if (!unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;

        if (decision.action == AircraftOperationAction.Land)
        {
            ResolveGroundedLayerForCell(unit, referenceTilemap, terrainDatabase, cell, out Domain landDomain, out HeightLevel landHeight);
            if (!UnitOccupancyRules.CanEndLayerTransitionAtCell(referenceTilemap, cell, unit, landDomain, landHeight, out _))
                return false;

            unit.TrySetCurrentLayerMode(landDomain, landHeight);
            unit.SetAircraftGrounded(true);
            unit.SetAircraftEmbarkedInCarrier(false);
            unit.SetAircraftOperationLockTurns(0);
            return true;
        }

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

    // Camada de pouso derivada do hex, nao mais Land/Surface fixo: Naval/Surface
    // quando a unidade suporta o modo e o hex o aceita (hidroaviao pousa na agua
    // ou praia de trem recolhido — a troca de camada aplica o sprite do modo
    // Naval/Surface do UnitData); caso contrario, Land/Surface. A regra de hex e
    // a mesma da emersao (CanSurfaceAtCell: construcao > estrutura > terreno +
    // skills), mantendo uma unica fonte de verdade para "Naval/Surface cabe aqui".
    public static void ResolveGroundedLayerForCell(
        UnitManager unit,
        Tilemap referenceTilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out Domain domain,
        out HeightLevel height)
    {
        domain = Domain.Land;
        height = HeightLevel.Surface;

        if (unit == null || referenceTilemap == null)
            return;

        cell.z = 0;
        if (unit.SupportsLayerMode(Domain.Naval, HeightLevel.Surface) &&
            PodeEmergirSensor.CanSurfaceAtCell(unit, referenceTilemap, terrainDatabase, cell, out _))
        {
            domain = Domain.Naval;
        }
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
