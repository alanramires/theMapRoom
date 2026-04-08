using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeFundirOption
{
    public UnitManager receiverUnit;
    public UnitManager candidateUnit;
    public Vector3Int candidateCell;
    public int remainingMovement;
    public int requiredMovementCost;
    public string displayLabel;
}

public class PodeFundirInvalidOption
{
    public const string ReasonIdInsufficientMovement = "fuse.invalid.insufficient_movement";
    public const string ReasonIdLayerMismatch = "fuse.invalid.layer_mismatch";
    public const string ReasonIdCandidateHasCargo = "fuse.invalid.candidate_has_cargo";
    public const string ReasonIdDifferentTeam = "fuse.invalid.different_team";
    public const string ReasonIdDifferentType = "fuse.invalid.different_type";

    public UnitManager receiverUnit;
    public UnitManager candidateUnit;
    public Vector3Int candidateCell;
    public int remainingMovement;
    public int requiredMovementCost;
    public string reasonId;
    public string reason;
}

public static class PodeFundirSensor
{
    public static bool TryEvaluate(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        out int sameTypeAdjacentCount,
        out string reason)
    {
        List<PodeFundirOption> options = new List<PodeFundirOption>();
        bool canMerge = CollectOptions(
            selectedUnit,
            boardTilemap,
            terrainDatabase: null,
            remainingMovementPoints: selectedUnit != null ? Mathf.Max(0, selectedUnit.RemainingMovementPoints) : 0,
            output: options,
            reason: out reason,
            invalidOutput: null);
        sameTypeAdjacentCount = options.Count;
        return canMerge;
    }

    public static bool CollectOptions(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase,
        int remainingMovementPoints,
        List<PodeFundirOption> output,
        out string reason,
        List<PodeFundirInvalidOption> invalidOutput = null)
    {
        reason = string.Empty;
        bool sensorLogs = SensorLogGate.IsPodeFundirEnabled();
        output?.Clear();
        invalidOutput?.Clear();

        if (sensorLogs)
            SensorLogGate.Log("PodeFundirSensor", $"collect receiver={(selectedUnit != null ? selectedUnit.name : "(null)")} remainingMove={remainingMovementPoints}");

        if (selectedUnit == null)
        {
            reason = "Selecione uma unidade.";
            return false;
        }

        if (selectedUnit.IsEmbarked)
        {
            reason = "Unidade embarcada nao pode fundir.";
            return false;
        }

        if (HasPassengers(selectedUnit))
        {
            reason = "Unidade transportando passageiros nao pode fundir.";
            return false;
        }

        if (selectedUnit.CurrentHP >= 10)
        {
            reason = "Unidade com HP maximo nao pode fundir.";
            return false;
        }

        Tilemap map = boardTilemap != null ? boardTilemap : selectedUnit.BoardTilemap;
        if (map == null)
        {
            reason = "Tilemap indisponivel para avaliar fusao.";
            return false;
        }

        Vector3Int origin = selectedUnit.CurrentCellPosition;
        origin.z = 0;
        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(map, origin, neighbors);

        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int cell = neighbors[i];
            cell.z = 0;

            UnitManager other = UnitOccupancyRules.GetUnitAtCell(map, cell, selectedUnit);
            if (other == null || other == selectedUnit || !other.gameObject.activeInHierarchy || other.IsEmbarked)
                continue;

            if ((int)selectedUnit.TeamId != (int)other.TeamId)
            {
                if (invalidOutput != null)
                {
                    invalidOutput.Add(new PodeFundirInvalidOption
                    {
                        receiverUnit = selectedUnit,
                        candidateUnit = other,
                        candidateCell = cell,
                        remainingMovement = Mathf.Max(0, remainingMovementPoints),
                        requiredMovementCost = 0,
                        reasonId = PodeFundirInvalidOption.ReasonIdDifferentTeam,
                        reason = "Unidade adjacente eh de outro time."
                    });
                }
                continue;
            }

            if (!IsSameType(selectedUnit, other))
            {
                if (invalidOutput != null)
                {
                    invalidOutput.Add(new PodeFundirInvalidOption
                    {
                        receiverUnit = selectedUnit,
                        candidateUnit = other,
                        candidateCell = cell,
                        remainingMovement = Mathf.Max(0, remainingMovementPoints),
                        requiredMovementCost = 0,
                        reasonId = PodeFundirInvalidOption.ReasonIdDifferentType,
                        reason = "Unidade adjacente nao eh do mesmo tipo."
                    });
                }
                continue;
            }

            if (HasPassengers(other))
            {
                if (invalidOutput != null)
                {
                    invalidOutput.Add(new PodeFundirInvalidOption
                    {
                        receiverUnit = selectedUnit,
                        candidateUnit = other,
                        candidateCell = cell,
                        remainingMovement = Mathf.Max(0, selectedUnit.RemainingMovementPoints),
                        requiredMovementCost = 0,
                        reasonId = PodeFundirInvalidOption.ReasonIdCandidateHasCargo,
                        reason = "Candidato transportando passageiros nao pode fundir."
                    });
                }
                continue;
            }

            Domain receiverDomain = selectedUnit.GetDomain();
            HeightLevel receiverHeight = selectedUnit.GetHeightLevel();
            Domain candidateDomain = other.GetDomain();
            HeightLevel candidateHeight = other.GetHeightLevel();
            bool sameOperationalLayer = receiverDomain == candidateDomain && receiverHeight == candidateHeight;
            if (!sameOperationalLayer)
            {
                if (invalidOutput != null)
                {
                    invalidOutput.Add(new PodeFundirInvalidOption
                    {
                        receiverUnit = selectedUnit,
                        candidateUnit = other,
                        candidateCell = cell,
                        remainingMovement = Mathf.Max(0, selectedUnit.RemainingMovementPoints),
                        requiredMovementCost = 0,
                        reasonId = PodeFundirInvalidOption.ReasonIdLayerMismatch,
                        reason = "Camada/altura diferente do receptor."
                    });
                }

                continue;
            }

            bool valid = EvaluateCandidateMovement(
                selectedUnit,
                other,
                map,
                terrainDatabase,
                remainingMovementPoints,
                out string invalidReasonId,
                out string invalidReason,
                out int requiredMovementCost,
                out int remainingMovement);

            if (valid)
            {
                output?.Add(new PodeFundirOption
                {
                    receiverUnit = selectedUnit,
                    candidateUnit = other,
                    candidateCell = cell,
                    remainingMovement = remainingMovement,
                    requiredMovementCost = requiredMovementCost,
                    displayLabel = $"{other.name} ({cell.x},{cell.y})"
                });
            }
            else if (invalidOutput != null)
            {
                invalidOutput.Add(new PodeFundirInvalidOption
                {
                    receiverUnit = selectedUnit,
                    candidateUnit = other,
                    candidateCell = cell,
                    remainingMovement = remainingMovement,
                    requiredMovementCost = requiredMovementCost,
                    reasonId = invalidReasonId,
                    reason = string.IsNullOrWhiteSpace(invalidReason) ? "Candidato invalido para fusao." : invalidReason
                });
            }
        }

        int validCount = output != null ? output.Count : 0;
        int invalidCount = invalidOutput != null ? invalidOutput.Count : 0;
        if (validCount > 0)
        {
            if (sensorLogs)
                SensorLogGate.Log("PodeFundirSensor", $"result valid={validCount} invalid={invalidCount} hasAny=true");
            return true;
        }

        if (invalidCount > 0)
        {
            string firstReason = !string.IsNullOrWhiteSpace(invalidOutput[0].reason)
                ? invalidOutput[0].reason
                : "Sem candidatos validos para fusao.";
            reason = $"Sem candidatos validos para fusao. {firstReason}";
            if (sensorLogs)
                SensorLogGate.Log("PodeFundirSensor", $"result valid={validCount} invalid={invalidCount} hasAny=true(reason-only)");
            return true;
        }

        reason = "Sem unidade adjacente (1 hex) do mesmo tipo para fundir.";
        if (sensorLogs)
            SensorLogGate.Log("PodeFundirSensor", $"result valid={validCount} invalid={invalidCount} hasAny=false");
        return false;
    }

    public static bool EvaluateCandidateMovement(
        UnitManager mover,
        UnitManager target,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        int remainingMovementPoints,
        out string invalidReasonId,
        out string invalidReason,
        out int requiredMovementCost,
        out int remainingMovement)
    {
        invalidReasonId = string.Empty;
        invalidReason = string.Empty;
        requiredMovementCost = 0;
        remainingMovement = 0;

        if (mover == null || target == null || map == null)
        {
            invalidReason = "Contexto de fusao invalido.";
            return false;
        }

        remainingMovement = Mathf.Max(0, remainingMovementPoints);
        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> validPaths = UnitMovementPathRules.CalcularCaminhosValidos(
            map,
            mover,
            remainingMovement,
            terrainDatabase);

        if (validPaths != null &&
            validPaths.TryGetValue(targetCell, out List<Vector3Int> mergePath) &&
            mergePath != null &&
            mergePath.Count >= 2)
        {
            requiredMovementCost = Mathf.Max(0, UnitMovementPathRules.CalculateAutonomyCostForPath(
                map,
                mover,
                mergePath,
                terrainDatabase,
                applyOperationalAutonomyModifier: false));

            if (requiredMovementCost > remainingMovement)
            {
                invalidReasonId = PodeFundirInvalidOption.ReasonIdInsufficientMovement;
                invalidReason = $"Movimento insuficiente ({remainingMovement}/{requiredMovementCost}).";
                return false;
            }

            return true;
        }

        if (!TryResolveMergeEnterCost(
                map,
                terrainDatabase,
                mover,
                targetCell,
                out requiredMovementCost,
                out string enterCostReason))
        {
            invalidReason = string.IsNullOrWhiteSpace(enterCostReason)
                ? "Nao consegue entrar no hex do alvo (terreno/camada bloqueia)."
                : enterCostReason;
            return false;
        }

        requiredMovementCost = Mathf.Max(0, requiredMovementCost);
        if (requiredMovementCost > remainingMovement)
        {
            invalidReasonId = PodeFundirInvalidOption.ReasonIdInsufficientMovement;
            invalidReason = $"Movimento insuficiente ({remainingMovement}/{requiredMovementCost}).";
            return false;
        }

        invalidReason = "Sem caminho valido ate o alvo (bloqueio no trajeto).";
        return false;
    }

    private static bool TryResolveMergeEnterCost(
        Tilemap map,
        TerrainDatabase terrainDatabase,
        UnitManager mover,
        Vector3Int targetCell,
        out int cost,
        out string reason)
    {
        cost = 0;
        reason = string.Empty;
        if (map == null || mover == null)
        {
            reason = "Contexto invalido para calcular custo de fusao.";
            return false;
        }

        targetCell.z = 0;
        if (UnitMovementPathRules.TryGetEnterCellCost(
                map,
                mover,
                targetCell,
                terrainDatabase,
                applyOperationalAutonomyModifier: false,
                out int normalCost))
        {
            cost = Mathf.Max(1, normalCost);
            return true;
        }

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(map, targetCell);
        if (construction != null)
        {
            if (!ConstructionAllowsUnitBySkillRules(construction, mover))
            {
                reason = "Unidade ativa nao cumpre as regras da construcao para fundir neste hex.";
                return false;
            }

            cost = GetAutonomyCostWithSkillOverrides(construction.GetBaseMovementCost(), construction.GetSkillCostOverrides(), mover);
            cost = Mathf.Max(1, cost);
            return true;
        }

        if (!TryResolveTerrainAtCell(map, terrainDatabase, targetCell, out TerrainTypeData terrain) || terrain == null)
        {
            reason = "Sem terreno valido para fallback de custo da fusao.";
            return false;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(map, targetCell);
        if (structure != null)
        {
            if (!StructureAllowsUnitBySkillRules(structure, terrain, mover))
            {
                reason = "Unidade ativa nao cumpre as regras da estrutura para fundir neste hex.";
                return false;
            }

            cost = GetAutonomyCostWithSkillOverrides(structure.baseMovementCost, structure.GetSkillCostOverrides(terrain), mover);
            cost = GetAutonomyCostWithSkillOverrides(cost, terrain.skillCostOverrides, mover);
            cost = Mathf.Max(1, cost);
            return true;
        }

        if (!UnitPassesTerrainSkillRequirement(mover, terrain))
        {
            reason = "Unidade ativa nao cumpre skill minima do terreno para fundir neste hex.";
            return false;
        }

        cost = GetAutonomyCostWithSkillOverrides(terrain.basicAutonomyCost, terrain.skillCostOverrides, mover);
        cost = Mathf.Max(1, cost);
        return true;
    }

    private static bool TryResolveTerrainAtCell(
        Tilemap terrainTilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out TerrainTypeData terrain)
    {
        terrain = null;
        if (terrainTilemap == null || terrainDatabase == null)
            return false;

        cell.z = 0;
        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
        {
            terrain = byMainTile;
            return true;
        }

        GridLayout grid = terrainTilemap.layoutGrid;
        if (grid == null)
            return false;

        Tilemap[] maps = grid.GetComponentsInChildren<Tilemap>(includeInactive: true);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;

            TileBase other = map.GetTile(cell);
            if (other == null)
                continue;

            if (terrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) && byGridTile != null)
            {
                terrain = byGridTile;
                return true;
            }
        }

        return false;
    }

    private static bool UnitPassesTerrainSkillRequirement(UnitManager unit, TerrainTypeData terrain)
    {
        if (unit == null || terrain == null)
            return false;

        if (terrain.requiredSkillsToEnter == null || terrain.requiredSkillsToEnter.Count == 0)
            return true;

        for (int i = 0; i < terrain.requiredSkillsToEnter.Count; i++)
        {
            SkillData skill = terrain.requiredSkillsToEnter[i];
            if (skill == null)
                continue;
            if (unit.HasSkill(skill))
                return true;
        }

        return false;
    }

    private static bool UnitBlockedByAnySkill(UnitManager unit, IReadOnlyList<SkillData> blockedSkills)
    {
        if (unit == null || blockedSkills == null || blockedSkills.Count == 0)
            return false;

        for (int i = 0; i < blockedSkills.Count; i++)
        {
            SkillData skill = blockedSkills[i];
            if (skill == null)
                continue;
            if (unit.HasSkill(skill))
                return true;
        }

        return false;
    }

    private static bool ConstructionAllowsUnitBySkillRules(ConstructionManager construction, UnitManager unit)
    {
        if (construction == null || unit == null)
            return false;

        IReadOnlyList<SkillData> required = construction.GetRequiredSkillsToEnter();
        IReadOnlyList<SkillData> blocked = construction.GetBlockedSkillsToEnter();
        if (UnitBlockedByAnySkill(unit, blocked))
            return false;

        if (required == null || required.Count == 0)
            return true;

        for (int i = 0; i < required.Count; i++)
        {
            SkillData skill = required[i];
            if (skill == null)
                continue;
            if (unit.HasSkill(skill))
                return true;
        }

        return false;
    }

    private static bool StructureAllowsUnitBySkillRules(StructureData structure, TerrainTypeData terrain, UnitManager unit)
    {
        if (structure == null || unit == null)
            return false;

        IReadOnlyList<SkillData> required = structure.GetRequiredSkillsToEnter(terrain);
        IReadOnlyList<SkillData> blocked = structure.GetBlockedSkillsToEnter(terrain);
        if (UnitBlockedByAnySkill(unit, blocked))
            return false;

        if (required == null || required.Count == 0)
            return true;

        for (int i = 0; i < required.Count; i++)
        {
            SkillData skill = required[i];
            if (skill == null)
                continue;
            if (unit.HasSkill(skill))
                return true;
        }

        return false;
    }

    private static int GetAutonomyCostWithSkillOverrides(
        int baseCost,
        IReadOnlyList<TerrainSkillCostOverride> overrides,
        UnitManager unit)
    {
        int safeBase = Mathf.Max(1, baseCost);
        if (unit == null || overrides == null)
            return safeBase;

        for (int i = 0; i < overrides.Count; i++)
        {
            TerrainSkillCostOverride entry = overrides[i];
            if (entry == null || entry.skill == null)
                continue;

            if (unit.HasSkill(entry.skill))
                return Mathf.Max(1, entry.autonomyCost);
        }

        return safeBase;
    }

    private static bool HasPassengers(UnitManager unit)
    {
        IReadOnlyList<UnitTransportSeatRuntime> seats = unit.TransportedUnitSlots;
        if (seats == null) return false;
        for (int i = 0; i < seats.Count; i++)
        {
            if (seats[i] != null && seats[i].embarkedUnit != null)
                return true;
        }
        return false;
    }

    private static bool IsSameType(UnitManager a, UnitManager b)
    {
        if (a == null || b == null)
            return false;

        string aId = a.UnitId;
        string bId = b.UnitId;
        if (!string.IsNullOrWhiteSpace(aId) && !string.IsNullOrWhiteSpace(bId))
            return string.Equals(aId.Trim(), bId.Trim(), System.StringComparison.OrdinalIgnoreCase);

        if (a.TryGetUnitData(out UnitData aData) && b.TryGetUnitData(out UnitData bData))
            return aData != null && bData != null && aData == bData;

        return false;
    }
}
