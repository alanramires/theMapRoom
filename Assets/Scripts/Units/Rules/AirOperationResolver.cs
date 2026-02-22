using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum AirOperationRuleSource
{
    None = 0,
    Construction = 1,
    Structure = 2,
    Terrain = 3
}

public readonly struct AirOperationTileContext
{
    public readonly ConstructionData construction;
    public readonly StructureData structure;
    public readonly TerrainTypeData terrain;
    public readonly AirOperationRuleSource source;
    public readonly LandingSurface landingSurface;
    public readonly DockingSurface dockingSurface;
    public readonly bool coastObstruction;
    public readonly bool blocksAirLanding;

    public AirOperationTileContext(
        ConstructionData construction,
        StructureData structure,
        TerrainTypeData terrain,
        AirOperationRuleSource source,
        LandingSurface landingSurface,
        DockingSurface dockingSurface,
        bool coastObstruction,
        bool blocksAirLanding)
    {
        this.construction = construction;
        this.structure = structure;
        this.terrain = terrain;
        this.source = source;
        this.landingSurface = landingSurface;
        this.dockingSurface = dockingSurface;
        this.coastObstruction = coastObstruction;
        this.blocksAirLanding = blocksAirLanding;
    }
}

public readonly struct AirTakeoffPlan
{
    public readonly TakeoffProcedure procedure;
    public readonly int rollMinHex;
    public readonly int rollMaxHex;
    public readonly HeightLevel endHeight;

    public AirTakeoffPlan(TakeoffProcedure procedure, int rollMinHex, int rollMaxHex, HeightLevel endHeight)
    {
        this.procedure = procedure;
        this.rollMinHex = Mathf.Max(0, rollMinHex);
        this.rollMaxHex = Mathf.Max(this.rollMinHex, rollMaxHex);
        this.endHeight = endHeight;
    }
}

public readonly struct AirLandingEvaluation
{
    public readonly bool allowed;
    public readonly LandingProcedure procedure;
    public readonly string reason;
    public readonly AirOperationRuleSource source;

    public AirLandingEvaluation(bool allowed, LandingProcedure procedure, string reason, AirOperationRuleSource source)
    {
        this.allowed = allowed;
        this.procedure = procedure;
        this.reason = reason ?? string.Empty;
        this.source = source;
    }
}

public readonly struct AirTakeoffEvaluation
{
    public readonly bool allowed;
    public readonly AirTakeoffPlan plan;
    public readonly string reason;
    public readonly AirOperationRuleSource source;

    public AirTakeoffEvaluation(bool allowed, AirTakeoffPlan plan, string reason, AirOperationRuleSource source)
    {
        this.allowed = allowed;
        this.plan = plan;
        this.reason = reason ?? string.Empty;
        this.source = source;
    }
}

public readonly struct AirDockEvaluation
{
    public readonly bool allowed;
    public readonly string reason;
    public readonly DockingSurface dockingSurface;
    public readonly AirOperationRuleSource source;

    public AirDockEvaluation(bool allowed, string reason, DockingSurface dockingSurface, AirOperationRuleSource source)
    {
        this.allowed = allowed;
        this.reason = reason ?? string.Empty;
        this.dockingSurface = dockingSurface;
        this.source = source;
    }
}

public static class AirOperationResolver
{
    private const string SkillVtol = "vtol";
    private const string SkillStovl = "stovl";
    private const string SkillAircraftLanding = "aircraft landing";
    private const string SkillCarrierLanding = "aircraft carrier landing";

    // Hierarquia canonica: Construcao > [Par Estrutura+Terreno] > Terreno.
    public static AirOperationTileContext ResolveContext(Tilemap referenceTilemap, TerrainDatabase terrainDatabase, Vector3Int cell)
    {
        cell.z = 0;
        ConstructionManager constructionManager = ConstructionOccupancyRules.GetConstructionAtCell(referenceTilemap, cell);
        ConstructionData construction = null;
        if (constructionManager != null)
            constructionManager.TryResolveConstructionData(out construction);

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(referenceTilemap, cell);
        TerrainTypeData terrain = ResolveTerrain(referenceTilemap, terrainDatabase, cell);

        if (construction != null)
        {
            return new AirOperationTileContext(
                construction,
                structure,
                terrain,
                AirOperationRuleSource.Construction,
                ResolveConstructionLandingSurface(construction),
                DockingSurface.None,
                false,
                false);
        }

        if (structure != null)
        {
            LandingSurface landingSurface = ResolveStructureLandingSurface(structure, terrain);
            return new AirOperationTileContext(
                null,
                structure,
                terrain,
                AirOperationRuleSource.Structure,
                landingSurface,
                DockingSurface.None,
                false,
                false);
        }

        if (terrain != null)
        {
            return new AirOperationTileContext(
                null,
                null,
                terrain,
                AirOperationRuleSource.Terrain,
                ResolveTerrainLandingSurface(terrain),
                DockingSurface.None,
                false,
                false);
        }

        return new AirOperationTileContext(null, null, null, AirOperationRuleSource.None, LandingSurface.None, DockingSurface.None, false, false);
    }

    public static AirLandingEvaluation EvaluateLanding(UnitManager unit, AirOperationTileContext tile, SensorMovementMode movementMode)
    {
        if (!TryGetUnitData(unit, out _, out string reason))
            return new AirLandingEvaluation(false, LandingProcedure.Instant, reason, tile.source);

        if (!UnitHasAnyAircraftSkill(unit))
            return new AirLandingEvaluation(false, LandingProcedure.Instant, "Unidade sem skill de operacao aerea.", tile.source);

        if (tile.blocksAirLanding)
            return new AirLandingEvaluation(false, LandingProcedure.Instant, "Hex bloqueia pouso aereo.", tile.source);

        if (!TryResolveLandingRules(tile, out bool allowLanding, out IReadOnlyList<SkillData> requiredSkills, out bool requireAtLeastOneSkill, out reason))
            return new AirLandingEvaluation(false, LandingProcedure.Instant, reason, tile.source);

        if (!allowLanding)
            return new AirLandingEvaluation(false, LandingProcedure.Instant, "Contexto atual nao permite pouso.", tile.source);

        if (!UnitSatisfiesRequiredSkills(unit, requiredSkills, requireAtLeastOneSkill))
            return new AirLandingEvaluation(false, LandingProcedure.Instant, "Unidade nao possui as skills exigidas para pouso neste contexto.", tile.source);

        if (requiredSkills == null || requiredSkills.Count == 0)
        {
            if (tile.dockingSurface != DockingSurface.None)
            {
                if (!HasSkill(unit, SkillCarrierLanding))
                    return new AirLandingEvaluation(false, LandingProcedure.Instant, "Pouso naval exige skill Aircraft Carrier Landing.", tile.source);
            }
            else if (!HasAnySkill(unit, SkillVtol, SkillStovl, SkillAircraftLanding))
            {
                return new AirLandingEvaluation(false, LandingProcedure.Instant, "Pouso terrestre exige VTOL, STOVL ou Aircraft Landing.", tile.source);
            }
        }

        return new AirLandingEvaluation(true, LandingProcedure.Instant, string.Empty, tile.source);
    }

    public static AirTakeoffEvaluation EvaluateTakeoff(UnitManager unit, AirOperationTileContext tile, SensorMovementMode movementMode)
    {
        if (!TryGetUnitData(unit, out UnitData data, out string reason))
            return new AirTakeoffEvaluation(false, default, reason, tile.source);

        if (!UnitHasAnyAircraftSkill(unit))
            return new AirTakeoffEvaluation(false, default, "Unidade sem skill de operacao aerea.", tile.source);

        if (!TryResolveTakeoffRules(tile, out bool allowTakeoff, out IReadOnlyList<SkillData> requiredSkills, out bool requireAtLeastOneSkill, out reason))
            return new AirTakeoffEvaluation(false, default, reason, tile.source);

        if (!allowTakeoff)
            return new AirTakeoffEvaluation(false, default, "Contexto atual nao permite decolagem.", tile.source);

        if (!UnitSatisfiesRequiredSkills(unit, requiredSkills, requireAtLeastOneSkill))
            return new AirTakeoffEvaluation(false, default, "Unidade nao possui as skills exigidas para decolagem neste contexto.", tile.source);

        if (requiredSkills == null || requiredSkills.Count == 0)
        {
            if (tile.dockingSurface != DockingSurface.None)
            {
                if (!HasSkill(unit, SkillCarrierLanding))
                    return new AirTakeoffEvaluation(false, default, "Decolagem naval exige skill Aircraft Carrier Landing.", tile.source);
            }
            else if (!HasAnySkill(unit, SkillVtol, SkillStovl, SkillAircraftLanding))
            {
                return new AirTakeoffEvaluation(false, default, "Decolagem terrestre exige VTOL, STOVL ou Aircraft Landing.", tile.source);
            }
        }

        AirTakeoffPlan plan = ResolveTakeoffPlanFromSkills(unit, data, tile);
        return new AirTakeoffEvaluation(true, plan, string.Empty, tile.source);
    }

    public static AirDockEvaluation EvaluateDock(UnitManager unit, AirOperationTileContext tile)
    {
        if (!TryGetUnitData(unit, out _, out string reason))
            return new AirDockEvaluation(false, reason, tile.dockingSurface, tile.source);

        if (tile.dockingSurface == DockingSurface.PortDock)
            return new AirDockEvaluation(true, string.Empty, tile.dockingSurface, tile.source);

        if (tile.dockingSurface == DockingSurface.BeachDock)
        {
            if (tile.coastObstruction)
                return new AirDockEvaluation(false, "BeachDock bloqueado por CoastObstruction.", tile.dockingSurface, tile.source);
            return new AirDockEvaluation(true, string.Empty, tile.dockingSurface, tile.source);
        }

        if (tile.dockingSurface == DockingSurface.OpenSeaDock)
        {
            if (!HasSkill(unit, SkillCarrierLanding))
                return new AirDockEvaluation(false, "OpenSeaDock exige skill Aircraft Carrier Landing.", tile.dockingSurface, tile.source);
            return new AirDockEvaluation(true, string.Empty, tile.dockingSurface, tile.source);
        }

        return new AirDockEvaluation(false, "Hex sem DockingSurface valido.", tile.dockingSurface, tile.source);
    }

    public static bool UnitHasAnyAircraftSkill(UnitManager unit)
    {
        return HasAnySkill(unit, SkillVtol, SkillStovl, SkillAircraftLanding, SkillCarrierLanding);
    }

    public static bool CanLand(UnitManager unit, Tilemap referenceTilemap, TerrainDatabase terrainDatabase, Vector3Int cell)
    {
        AirOperationTileContext tile = ResolveContext(referenceTilemap, terrainDatabase, cell);
        return EvaluateLanding(unit, tile, SensorMovementMode.MoveuParado).allowed;
    }

    public static bool CanTakeoff(UnitManager unit, Tilemap referenceTilemap, TerrainDatabase terrainDatabase, Vector3Int cell)
    {
        AirOperationTileContext tile = ResolveContext(referenceTilemap, terrainDatabase, cell);
        return EvaluateTakeoff(unit, tile, SensorMovementMode.MoveuParado).allowed;
    }

    public static bool CanLand(UnitManager unit, AirOperationTileContext tile)
    {
        return EvaluateLanding(unit, tile, SensorMovementMode.MoveuParado).allowed;
    }

    public static bool CanTakeoff(UnitManager unit, AirOperationTileContext tile)
    {
        return EvaluateTakeoff(unit, tile, SensorMovementMode.MoveuParado).allowed;
    }

    public static bool CanLand(UnitManager unit, AirOperationTileContext tile, SensorMovementMode movementMode, out string reason)
    {
        AirLandingEvaluation eval = EvaluateLanding(unit, tile, movementMode);
        reason = eval.reason;
        return eval.allowed;
    }

    public static bool CanTakeoff(UnitManager unit, AirOperationTileContext tile, SensorMovementMode movementMode, out string reason)
    {
        AirTakeoffEvaluation eval = EvaluateTakeoff(unit, tile, movementMode);
        reason = eval.reason;
        return eval.allowed;
    }

    public static bool TryGetTakeoffPlan(UnitManager unit, AirOperationTileContext tile, SensorMovementMode movementMode, out AirTakeoffPlan plan, out string reason)
    {
        AirTakeoffEvaluation eval = EvaluateTakeoff(unit, tile, movementMode);
        plan = eval.plan;
        reason = eval.reason;
        return eval.allowed;
    }

    private static bool TryGetUnitData(UnitManager unit, out UnitData data, out string reason)
    {
        reason = string.Empty;
        data = null;
        if (unit == null)
        {
            reason = "Unidade invalida.";
            return false;
        }

        if (!unit.TryGetUnitData(out data) || data == null)
        {
            reason = "UnitData nao encontrado.";
            return false;
        }

        return true;
    }

    private static bool TryResolveLandingRules(
        AirOperationTileContext tile,
        out bool allowLanding,
        out IReadOnlyList<SkillData> requiredSkills,
        out bool requireAtLeastOneSkill,
        out string reason)
    {
        reason = string.Empty;
        allowLanding = false;
        requiredSkills = null;
        requireAtLeastOneSkill = false;

        switch (tile.source)
        {
            case AirOperationRuleSource.Construction:
                allowLanding = tile.construction != null && tile.construction.allowAircraftTakeoffAndLanding;
                requiredSkills = ExtractConstructionRequiredSkills(tile.construction);
                requireAtLeastOneSkill = tile.construction != null && tile.construction.requireAtLeastOneLandingSkill;
                return true;
            case AirOperationRuleSource.Structure:
                if (TryGetStructureTerrainRule(tile, out StructureAirOpsTerrainRule landingRule))
                {
                    allowLanding = landingRule != null && landingRule.allowTakeoffAndLanding;
                    requiredSkills = ExtractStructureRequiredSkills(landingRule);
                    requireAtLeastOneSkill = landingRule != null && landingRule.requireAtLeastOneLandingSkill;
                }
                // Se nao houver regra para o par Estrutura+Terreno, nao pode pousar.
                return true;
            case AirOperationRuleSource.Terrain:
                allowLanding = tile.terrain != null && tile.terrain.allowAircraftTakeoffAndLanding;
                requiredSkills = tile.terrain != null ? tile.terrain.requiredLandingSkills : null;
                requireAtLeastOneSkill = tile.terrain != null && tile.terrain.requireAtLeastOneLandingSkill;
                return true;
            default:
                reason = "Hex sem contexto de operacao aerea.";
                return false;
        }
    }

    private static bool TryResolveTakeoffRules(
        AirOperationTileContext tile,
        out bool allowTakeoff,
        out IReadOnlyList<SkillData> requiredSkills,
        out bool requireAtLeastOneSkill,
        out string reason)
    {
        reason = string.Empty;
        allowTakeoff = false;
        requiredSkills = null;
        requireAtLeastOneSkill = false;

        switch (tile.source)
        {
            case AirOperationRuleSource.Construction:
                allowTakeoff = tile.construction != null && tile.construction.allowAircraftTakeoffAndLanding;
                requiredSkills = ExtractConstructionRequiredSkills(tile.construction);
                requireAtLeastOneSkill = tile.construction != null && tile.construction.requireAtLeastOneLandingSkill;
                return true;
            case AirOperationRuleSource.Structure:
                if (TryGetStructureTerrainRule(tile, out StructureAirOpsTerrainRule takeoffRule))
                {
                    allowTakeoff = takeoffRule != null && takeoffRule.allowTakeoffAndLanding;
                    requiredSkills = ExtractStructureRequiredSkills(takeoffRule);
                    requireAtLeastOneSkill = takeoffRule != null && takeoffRule.requireAtLeastOneLandingSkill;
                }
                // Se nao houver regra para o par Estrutura+Terreno, nao pode decolar.
                return true;
            case AirOperationRuleSource.Terrain:
                allowTakeoff = tile.terrain != null && tile.terrain.allowAircraftTakeoffAndLanding;
                requiredSkills = tile.terrain != null ? tile.terrain.requiredLandingSkills : null;
                requireAtLeastOneSkill = tile.terrain != null && tile.terrain.requireAtLeastOneLandingSkill;
                return true;
            default:
                reason = "Hex sem contexto de operacao aerea.";
                return false;
        }
    }

    private static AirTakeoffPlan ResolveTakeoffPlanFromSkills(UnitManager unit, UnitData data, AirOperationTileContext tile)
    {
        HeightLevel preferredHeight = ResolvePreferredAirHeight(data);

        if (TryResolveConstructionConfiguredTakeoffPlan(unit, tile.construction, preferredHeight, out AirTakeoffPlan configuredPlan))
            return configuredPlan;

        if (TryResolveStructureConfiguredTakeoffPlan(unit, tile, preferredHeight, out AirTakeoffPlan structureConfiguredPlan))
            return structureConfiguredPlan;

        if (HasSkill(unit, SkillVtol))
            return new AirTakeoffPlan(TakeoffProcedure.InstantToPreferredHeight, 0, 0, preferredHeight);

        if (HasSkill(unit, SkillStovl))
        {
            bool airportRunway = tile.landingSurface == LandingSurface.AirportRunway;
            return airportRunway
                ? new AirTakeoffPlan(TakeoffProcedure.InstantToPreferredHeight, 0, 0, preferredHeight)
                : new AirTakeoffPlan(TakeoffProcedure.ShortRoll0to1HexEndAirLow, 0, 1, HeightLevel.AirLow);
        }

        if (HasSkill(unit, SkillAircraftLanding))
        {
            bool roadRunway = tile.landingSurface == LandingSurface.RoadRunway;
            return roadRunway
                ? new AirTakeoffPlan(TakeoffProcedure.RunwayRoll1HexEndAirLow, 1, 1, HeightLevel.AirLow)
                : new AirTakeoffPlan(TakeoffProcedure.InstantToPreferredHeight, 0, 0, preferredHeight);
        }

        return new AirTakeoffPlan(TakeoffProcedure.InstantToPreferredHeight, 0, 0, preferredHeight);
    }

    private static IReadOnlyList<SkillData> ExtractConstructionRequiredSkills(ConstructionData construction)
    {
        if (construction == null || construction.requiredLandingSkillRules == null || construction.requiredLandingSkillRules.Count == 0)
            return null;

        List<SkillData> extracted = new List<SkillData>(construction.requiredLandingSkillRules.Count);
        for (int i = 0; i < construction.requiredLandingSkillRules.Count; i++)
        {
            ConstructionLandingSkillRule rule = construction.requiredLandingSkillRules[i];
            if (rule == null || rule.skill == null)
                continue;
            extracted.Add(rule.skill);
        }

        return extracted.Count > 0 ? extracted : null;
    }

    private static bool TryResolveConstructionConfiguredTakeoffPlan(
        UnitManager unit,
        ConstructionData construction,
        HeightLevel preferredHeight,
        out AirTakeoffPlan plan)
    {
        plan = default;
        if (unit == null || construction == null || construction.requiredLandingSkillRules == null || construction.requiredLandingSkillRules.Count == 0)
            return false;

        ConstructionLandingSkillRule selectedRule = null;
        if (construction.requireAtLeastOneLandingSkill)
        {
            for (int i = 0; i < construction.requiredLandingSkillRules.Count; i++)
            {
                ConstructionLandingSkillRule candidate = construction.requiredLandingSkillRules[i];
                if (candidate == null || candidate.skill == null)
                    continue;
                if (unit.HasSkill(candidate.skill))
                {
                    selectedRule = candidate;
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < construction.requiredLandingSkillRules.Count; i++)
            {
                ConstructionLandingSkillRule candidate = construction.requiredLandingSkillRules[i];
                if (candidate == null || candidate.skill == null)
                    continue;
                if (!unit.HasSkill(candidate.skill))
                    return false;
                if (selectedRule == null)
                    selectedRule = candidate;
            }
        }

        if (selectedRule == null)
            return false;

        plan = BuildTakeoffPlan(selectedRule.takeoffMode, preferredHeight);
        return true;
    }

    private static IReadOnlyList<SkillData> ExtractStructureRequiredSkills(StructureAirOpsTerrainRule rule)
    {
        if (rule == null || rule.requiredLandingSkillRules == null || rule.requiredLandingSkillRules.Count == 0)
            return null;

        List<SkillData> extracted = new List<SkillData>(rule.requiredLandingSkillRules.Count);
        for (int i = 0; i < rule.requiredLandingSkillRules.Count; i++)
        {
            StructureLandingSkillRule entry = rule.requiredLandingSkillRules[i];
            if (entry == null || entry.skill == null)
                continue;
            extracted.Add(entry.skill);
        }

        return extracted.Count > 0 ? extracted : null;
    }

    private static bool TryResolveStructureConfiguredTakeoffPlan(
        UnitManager unit,
        AirOperationTileContext tile,
        HeightLevel preferredHeight,
        out AirTakeoffPlan plan)
    {
        plan = default;
        if (unit == null || tile.source != AirOperationRuleSource.Structure)
            return false;

        if (!TryGetStructureTerrainRule(tile, out StructureAirOpsTerrainRule rule) || rule == null)
            return false;

        if (rule.requiredLandingSkillRules == null || rule.requiredLandingSkillRules.Count == 0)
            return false;

        StructureLandingSkillRule selectedRule = null;
        if (rule.requireAtLeastOneLandingSkill)
        {
            for (int i = 0; i < rule.requiredLandingSkillRules.Count; i++)
            {
                StructureLandingSkillRule candidate = rule.requiredLandingSkillRules[i];
                if (candidate == null || candidate.skill == null)
                    continue;
                if (unit.HasSkill(candidate.skill))
                {
                    selectedRule = candidate;
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < rule.requiredLandingSkillRules.Count; i++)
            {
                StructureLandingSkillRule candidate = rule.requiredLandingSkillRules[i];
                if (candidate == null || candidate.skill == null)
                    continue;
                if (!unit.HasSkill(candidate.skill))
                    return false;
                if (selectedRule == null)
                    selectedRule = candidate;
            }
        }

        if (selectedRule == null)
            return false;

        plan = BuildTakeoffPlan(selectedRule.takeoffMode, preferredHeight);
        return true;
    }

    private static AirTakeoffPlan BuildTakeoffPlan(TakeoffProcedure procedure, HeightLevel preferredHeight)
    {
        switch (procedure)
        {
            case TakeoffProcedure.RunwayRoll1HexEndAirLow:
                return new AirTakeoffPlan(procedure, 1, 1, HeightLevel.AirLow);
            case TakeoffProcedure.ShortRoll0to1HexEndAirLow:
                return new AirTakeoffPlan(procedure, 0, 1, HeightLevel.AirLow);
            case TakeoffProcedure.InstantToPreferredHeight:
            default:
                return new AirTakeoffPlan(TakeoffProcedure.InstantToPreferredHeight, 0, 0, preferredHeight);
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

    private static bool UnitSatisfiesRequiredSkills(UnitManager unit, IReadOnlyList<SkillData> requiredSkills, bool requireAtLeastOneSkill)
    {
        if (requiredSkills == null || requiredSkills.Count == 0)
            return true;
        if (unit == null)
            return false;

        if (requireAtLeastOneSkill)
        {
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

        for (int i = 0; i < requiredSkills.Count; i++)
        {
            SkillData required = requiredSkills[i];
            if (required == null)
                continue;
            if (!unit.HasSkill(required))
                return false;
        }

        return true;
    }

    private static bool HasAnySkill(UnitManager unit, params string[] tokens)
    {
        if (tokens == null)
            return false;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (HasSkill(unit, tokens[i]))
                return true;
        }

        return false;
    }

    private static bool HasSkill(UnitManager unit, string token)
    {
        if (unit == null || string.IsNullOrWhiteSpace(token))
            return false;

        if (!unit.TryGetUnitData(out UnitData data) || data == null || data.skills == null)
            return false;

        string key = NormalizeToken(token);
        for (int i = 0; i < data.skills.Count; i++)
        {
            SkillData skill = data.skills[i];
            if (skill == null)
                continue;

            string id = NormalizeToken(skill.id);
            string name = NormalizeToken(skill.displayName);
            if (id == key || name == key || id.Contains(key) || name.Contains(key) || key.Contains(id) || key.Contains(name))
                return true;
        }

        return false;
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");
    }

    private static LandingSurface ResolveConstructionLandingSurface(ConstructionData construction)
    {
        if (construction == null)
            return LandingSurface.None;
        return construction.allowAircraftTakeoffAndLanding ? LandingSurface.AirportRunway : LandingSurface.None;
    }

    private static LandingSurface ResolveStructureLandingSurface(StructureData structure, TerrainTypeData terrain)
    {
        if (!TryGetStructureTerrainRule(structure, terrain, out StructureAirOpsTerrainRule pairRule) || pairRule == null)
            return LandingSurface.None;

        if (!pairRule.allowTakeoffAndLanding)
            return LandingSurface.None;
        return LandingSurface.RoadRunway;
    }

    private static bool TryGetStructureTerrainRule(AirOperationTileContext tile, out StructureAirOpsTerrainRule rule)
    {
        return TryGetStructureTerrainRule(tile.structure, tile.terrain, out rule);
    }

    private static bool TryGetStructureTerrainRule(StructureData structure, TerrainTypeData terrain, out StructureAirOpsTerrainRule rule)
    {
        rule = null;
        if (structure == null || terrain == null || structure.aircraftOpsByTerrain == null)
            return false;

        string terrainId = terrain.id;
        for (int i = 0; i < structure.aircraftOpsByTerrain.Count; i++)
        {
            StructureAirOpsTerrainRule candidate = structure.aircraftOpsByTerrain[i];
            if (candidate == null || candidate.terrainData == null)
                continue;

            if (candidate.terrainData == terrain)
            {
                rule = candidate;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(terrainId) && candidate.terrainData.id == terrainId)
            {
                rule = candidate;
                return true;
            }
        }

        return false;
    }

    private static LandingSurface ResolveTerrainLandingSurface(TerrainTypeData terrain)
    {
        if (terrain == null)
            return LandingSurface.None;
        return terrain.allowAircraftTakeoffAndLanding ? LandingSurface.FlatGround : LandingSurface.None;
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
}
