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

        if (!IsAircraftClass(data.unitClass))
            return Unavailable("Unidade nao e aeronave.");

        if (unit.AircraftOperationLockTurns > 0)
            return Unavailable("Aeronave em recuperacao operacional.");

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        AircraftContext context = ResolveContext(referenceTilemap, terrainDatabase, cell);

        if (unit.GetDomain() == Domain.Air && !unit.IsAircraftGrounded)
            return EvaluateLanding(unit, data, movementMode, context);

        return EvaluateTakeoff(unit, data, movementMode, context);
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

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;
        AircraftContext context = ResolveContext(referenceTilemap, terrainDatabase, cell);

        if (decision.action == AircraftOperationAction.Land)
        {
            bool landedOnCarrierDeck = context.structure != null && context.structure.isCarrierDeck;
            unit.TrySetCurrentLayerMode(Domain.Land, HeightLevel.Surface);
            unit.SetAircraftGrounded(true);
            unit.SetAircraftEmbarkedInCarrier(landedOnCarrierDeck);
            unit.SetAircraftOperationLockTurns(0);
            return true;
        }

        HeightLevel preferred = ResolvePreferredAirHeight(data);
        unit.TrySetCurrentLayerMode(Domain.Air, preferred);
        unit.SetAircraftGrounded(false);
        unit.SetAircraftEmbarkedInCarrier(false);
        unit.SetAircraftOperationLockTurns(0);
        return true;
    }

    private static AircraftOperationDecision EvaluateLanding(
        UnitManager unit,
        UnitData data,
        SensorMovementMode movementMode,
        AircraftContext context)
    {
        bool isStopped = movementMode == SensorMovementMode.MoveuParado;

        bool constructionAllowsLanding = context.construction != null && context.construction.allowAircraftLanding;
        if (constructionAllowsLanding)
        {
            if (!IsClassAllowed(context.construction.landingAllowedClasses, data.unitClass))
                return Unavailable("Classe da unidade nao permitida para pouso nesta construcao.");
            if (!UnitHasAnyRequiredSkill(unit, context.construction.landingRequiredSkills))
                return Unavailable("Falta skill obrigatoria para pouso nesta construcao.");
            return Available(AircraftOperationAction.Land, consumesAction: true, "Pousar (Construcao)");
        }

        bool structureAllowsLanding = context.structure != null && context.structure.allowAircraftLanding;
        if (structureAllowsLanding)
        {
            if (!IsClassAllowed(context.structure.landingAllowedClasses, data.unitClass))
                return Unavailable("Classe da unidade nao permitida para pouso nesta estrutura.");
            if (!UnitHasAnyRequiredSkill(unit, context.structure.landingRequiredSkills))
                return Unavailable("Falta skill obrigatoria para pouso nesta estrutura.");

            bool requiresStopped = context.structure.roadLandingRequiresStoppedClasses != null &&
                                   context.structure.roadLandingRequiresStoppedClasses.Contains(data.unitClass);
            if (requiresStopped && !isStopped)
                return Unavailable("Pouso em estrada exige MoveuParado.");

            return Available(AircraftOperationAction.Land, consumesAction: true, "Pousar (Estrutura)");
        }

        bool terrainAllowsLanding = context.terrain != null && context.terrain.allowAircraftLanding;
        if (terrainAllowsLanding)
        {
            if (!IsClassAllowed(context.terrain.landingAllowedClasses, data.unitClass))
                return Unavailable("Classe da unidade nao permitida para pouso neste terreno.");
            if (!UnitHasAnyRequiredSkill(unit, context.terrain.landingRequiredSkills))
                return Unavailable("Falta skill obrigatoria para pouso neste terreno.");
            return Available(AircraftOperationAction.Land, consumesAction: true, "Pousar (Terreno)");
        }

        return Unavailable("Nao ha ponto de pouso valido no hex.");
    }

    private static AircraftOperationDecision EvaluateTakeoff(UnitManager unit, UnitData data, SensorMovementMode movementMode, AircraftContext context)
    {
        bool constructionAllowsTakeoff = context.construction != null && context.construction.allowAircraftTakeoff;
        if (constructionAllowsTakeoff)
        {
            if (!IsClassAllowed(context.construction.landingAllowedClasses, data.unitClass))
                return Unavailable("Classe da unidade nao permitida para decolagem nesta construcao.");
            if (!IsTakeoffMovementAllowed(context.construction.takeoffAllowedMovementModes, movementMode))
                return Unavailable("Modo de movimento atual nao permitido para decolagem nesta construcao.");
            return Available(AircraftOperationAction.Takeoff, consumesAction: false, "Decolar (Construcao)");
        }

        bool structureAllowsTakeoff = context.structure != null && context.structure.allowAircraftTakeoff;
        if (structureAllowsTakeoff)
        {
            if (!IsClassAllowed(context.structure.landingAllowedClasses, data.unitClass))
                return Unavailable("Classe da unidade nao permitida para decolagem nesta estrutura.");
            if (!IsTakeoffMovementAllowed(context.structure.takeoffAllowedMovementModes, movementMode))
                return Unavailable("Modo de movimento atual nao permitido para decolagem nesta estrutura.");
            return Available(AircraftOperationAction.Takeoff, consumesAction: false, "Decolar (Estrutura)");
        }

        bool terrainAllowsTakeoff = context.terrain != null && context.terrain.allowAircraftTakeoff;
        if (terrainAllowsTakeoff)
        {
            if (!IsClassAllowed(context.terrain.landingAllowedClasses, data.unitClass))
                return Unavailable("Classe da unidade nao permitida para decolagem neste terreno.");
            if (!IsTakeoffMovementAllowed(context.terrain.takeoffAllowedMovementModes, movementMode))
                return Unavailable("Modo de movimento atual nao permitido para decolagem neste terreno.");
            return Available(AircraftOperationAction.Takeoff, consumesAction: false, "Decolar (Terreno)");
        }

        return Unavailable("Nao ha ponto de decolagem valido no hex.");
    }

    private static AircraftContext ResolveContext(Tilemap referenceTilemap, TerrainDatabase terrainDatabase, Vector3Int cell)
    {
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(referenceTilemap, cell);
        StructureData structure = StructureOccupancyRules.GetStructureAtCell(referenceTilemap, cell);
        TerrainTypeData terrain = ResolveTerrain(referenceTilemap, terrainDatabase, cell);
        ConstructionData constructionData = null;
        if (construction != null)
            construction.TryResolveConstructionData(out constructionData);

        return new AircraftContext(constructionData, structure, terrain);
    }

    private static TerrainTypeData ResolveTerrain(Tilemap referenceTilemap, TerrainDatabase terrainDatabase, Vector3Int cell)
    {
        if (referenceTilemap == null || terrainDatabase == null)
            return null;

        cell.z = 0;
        TileBase tile = referenceTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData byMain) && byMain != null)
            return byMain;

        GridLayout grid = referenceTilemap.layoutGrid;
        if (grid == null)
            return null;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            TileBase other = map.GetTile(cell);
            if (other == null)
                continue;

            if (terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData byGrid) && byGrid != null)
                return byGrid;
        }

        return null;
    }

    private static bool IsTerrainId(TerrainTypeData terrain, string expectedId)
    {
        if (terrain == null || string.IsNullOrWhiteSpace(expectedId))
            return false;

        if (!string.IsNullOrWhiteSpace(terrain.id) && terrain.id.Trim().ToLowerInvariant() == expectedId)
            return true;
        if (!string.IsNullOrWhiteSpace(terrain.displayName) && terrain.displayName.Trim().ToLowerInvariant() == expectedId)
            return true;

        return false;
    }

    private static bool IsAircraftClass(GameUnitClass unitClass)
    {
        return unitClass == GameUnitClass.Jet ||
               unitClass == GameUnitClass.Plane ||
               unitClass == GameUnitClass.Helicopter;
    }

    private static bool IsClassAllowed(System.Collections.Generic.IReadOnlyList<GameUnitClass> allowedClasses, GameUnitClass unitClass)
    {
        if (allowedClasses == null || allowedClasses.Count == 0)
            return IsAircraftClass(unitClass);

        for (int i = 0; i < allowedClasses.Count; i++)
        {
            if (allowedClasses[i] == unitClass)
                return true;
        }

        return false;
    }

    private static bool UnitHasAnyRequiredSkill(UnitManager unit, System.Collections.Generic.IReadOnlyList<SkillData> requiredSkills)
    {
        if (requiredSkills == null || requiredSkills.Count == 0)
            return true;
        if (unit == null)
            return false;

        for (int i = 0; i < requiredSkills.Count; i++)
        {
            SkillData required = requiredSkills[i];
            if (required == null)
                continue;
            if (unit.HasSkill(required))
                return true;
        }

        return false;
    }

    private static bool IsTakeoffMovementAllowed(System.Collections.Generic.IReadOnlyList<SensorMovementMode> allowedModes, SensorMovementMode currentMode)
    {
        if (allowedModes == null || allowedModes.Count == 0)
            return currentMode == SensorMovementMode.MoveuParado || currentMode == SensorMovementMode.MoveuAndando;

        for (int i = 0; i < allowedModes.Count; i++)
        {
            if (allowedModes[i] == currentMode)
                return true;
        }

        return false;
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

    private static bool IsConstructionId(ConstructionData construction, string expectedId)
    {
        if (construction == null || string.IsNullOrWhiteSpace(expectedId))
            return false;

        string expected = expectedId.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(construction.id) && construction.id.Trim().ToLowerInvariant().Contains(expected))
            return true;
        if (!string.IsNullOrWhiteSpace(construction.displayName) && construction.displayName.Trim().ToLowerInvariant().Contains(expected))
            return true;

        return false;
    }

    private static bool IsStructureId(StructureData structure, string expectedId)
    {
        if (structure == null || string.IsNullOrWhiteSpace(expectedId))
            return false;

        string expected = expectedId.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(structure.id) && structure.id.Trim().ToLowerInvariant().Contains(expected))
            return true;
        if (!string.IsNullOrWhiteSpace(structure.displayName) && structure.displayName.Trim().ToLowerInvariant().Contains(expected))
            return true;

        return false;
    }

    private static AircraftOperationDecision Available(AircraftOperationAction action, bool consumesAction, string label)
    {
        return new AircraftOperationDecision(true, action, consumesAction, label, string.Empty);
    }

    private static AircraftOperationDecision Unavailable(string reason)
    {
        return new AircraftOperationDecision(false, AircraftOperationAction.None, false, string.Empty, reason);
    }

    private readonly struct AircraftContext
    {
        public readonly ConstructionData construction;
        public readonly StructureData structure;
        public readonly TerrainTypeData terrain;

        public AircraftContext(ConstructionData construction, StructureData structure, TerrainTypeData terrain)
        {
            this.construction = construction;
            this.structure = structure;
            this.terrain = terrain;
        }
    }
}
