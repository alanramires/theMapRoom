using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeMirarSensor
{
    private const string InvalidReasonNoAmmo = "Falta de municao.";
    private const string InvalidReasonLayer = "Layer do alvo incompativel com a arma.";
    private const string InvalidReasonLosBlocked = "Linha de visada bloqueada.";
    private const string InvalidReasonStealth = "Alvo nao detectado (stealth placeholder).";

    private struct WeaponRangeCandidate
    {
        public int index;
        public UnitEmbarkedWeapon embarked;
        public int minRange;
        public int maxRange;
    }

    public static bool CollectTargets(
        UnitManager attacker,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        List<PodeMirarTargetOption> output,
        List<PodeMirarInvalidOption> invalidOutput = null,
        WeaponPriorityData weaponPriorityData = null,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool fogOfWarEnabled = true)
    {
        if (output == null)
            return false;

        output.Clear();
        invalidOutput?.Clear();
        if (attacker == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> embarkedWeapons = attacker.GetEmbarkedWeapons();
        if (embarkedWeapons == null || embarkedWeapons.Count == 0)
            return false;

        List<WeaponRangeCandidate> candidates = new List<WeaponRangeCandidate>(embarkedWeapons.Count);
        int globalMaxRange = 0;

        for (int i = 0; i < embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = embarkedWeapons[i];
            if (!TryBuildCandidate(embarked, i, movementMode, out WeaponRangeCandidate candidate, requireAmmo: false))
                continue;

            candidates.Add(candidate);
            if (candidate.maxRange > globalMaxRange)
                globalMaxRange = candidate.maxRange;
        }

        if (candidates.Count == 0 || globalMaxRange <= 0)
            return false;

        Tilemap map = boardTilemap != null ? boardTilemap : attacker.BoardTilemap;
        if (map == null)
            return false;

        Vector3Int origin = attacker.CurrentCellPosition;
        origin.z = 0;
        Dictionary<Vector3Int, int> distances = BuildDistanceMap(map, origin, globalMaxRange);
        string attackerPositionLabel = ResolveUnitPositionLabel(map, terrainDatabase, attacker, origin);

        int sensorOrderCounter = 0;
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager target = units[i];
            if (!IsEnemyTargetCandidate(attacker, target))
                continue;

            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            string defenderPositionLabel = ResolveUnitPositionLabel(map, terrainDatabase, target, targetCell);
            if (!distances.TryGetValue(targetCell, out int distance))
                continue;

            for (int j = 0; j < candidates.Count; j++)
            {
                WeaponRangeCandidate weaponCandidate = candidates[j];
                if (distance < weaponCandidate.minRange || distance > weaponCandidate.maxRange)
                    continue;

                WeaponData weapon = weaponCandidate.embarked.weapon;
                if (weapon == null)
                    continue;

                if (weaponCandidate.embarked.squadAmmunition <= 0)
                {
                    AppendInvalid(
                        invalidOutput,
                        attacker,
                        target,
                        weapon,
                        weaponCandidate.index,
                        distance,
                        attackerPositionLabel,
                        defenderPositionLabel,
                        InvalidReasonNoAmmo,
                        Vector3Int.zero,
                        null);
                    continue;
                }

                if (!weapon.SupportsOperationOn(target.GetDomain(), target.GetHeightLevel()))
                {
                    AppendInvalid(
                        invalidOutput,
                        attacker,
                        target,
                        weapon,
                        weaponCandidate.index,
                        distance,
                        attackerPositionLabel,
                        defenderPositionLabel,
                        InvalidReasonLayer,
                        Vector3Int.zero,
                        null);
                    continue;
                }

                List<Vector3Int> intermediateCells = null;
                Vector3Int blockedCell = Vector3Int.zero;
                if (weaponCandidate.embarked.selectedTrajectory == WeaponTrajectoryType.Straight &&
                    !HasValidStraightLineOfFire(
                        map,
                        terrainDatabase,
                        origin,
                        targetCell,
                        attacker,
                        target,
                        dpqAirHeightConfig,
                        fogOfWarEnabled,
                        weapon,
                        out intermediateCells,
                        out blockedCell))
                {
                    AppendInvalid(
                        invalidOutput,
                        attacker,
                        target,
                        weapon,
                        weaponCandidate.index,
                        distance,
                        attackerPositionLabel,
                        defenderPositionLabel,
                        $"{InvalidReasonLosBlocked} ({blockedCell.x},{blockedCell.y})",
                        blockedCell,
                        intermediateCells);
                    continue;
                }

                if (!IsTargetDetectableByAttacker(attacker, target))
                {
                    AppendInvalid(
                        invalidOutput,
                        attacker,
                        target,
                        weapon,
                        weaponCandidate.index,
                        distance,
                        attackerPositionLabel,
                        defenderPositionLabel,
                        InvalidReasonStealth,
                        Vector3Int.zero,
                        intermediateCells);
                    continue;
                }

                bool canCounter = TryResolveCounterAttack(
                    target,
                    attacker,
                    distance,
                    out WeaponData counterWeapon,
                    out int counterIndex,
                    out string counterReason);
                GameUnitClass targetClass = ResolveUnitClass(target);
                WeaponCategory weaponCategory = weapon.WeaponCategory;
                bool preferredTarget = EvaluateWeaponPriority(weaponPriorityData, weaponCategory, targetClass);
                output.Add(new PodeMirarTargetOption
                {
                    attackerUnit = attacker,
                    targetUnit = target,
                    weapon = weapon,
                    embarkedWeaponIndex = weaponCandidate.index,
                    distance = distance,
                    sensorOrder = sensorOrderCounter++,
                    isPreferredTargetForWeapon = preferredTarget,
                    attackerPositionLabel = attackerPositionLabel,
                    defenderPositionLabel = defenderPositionLabel,
                    defenderCanCounterAttack = canCounter,
                    defenderCounterWeapon = counterWeapon,
                    defenderCounterEmbarkedWeaponIndex = counterIndex,
                    defenderCounterDistance = canCounter ? distance : 0,
                    defenderCounterReason = canCounter ? string.Empty : counterReason,
                    lineOfFireIntermediateCells = intermediateCells ?? new List<Vector3Int>()
                });
            }
        }

        output.Sort(CompareTargetOptionsByPriority);

        for (int i = 0; i < output.Count; i++)
        {
            string weaponName = output[i].weapon != null && !string.IsNullOrWhiteSpace(output[i].weapon.displayName)
                ? output[i].weapon.displayName
                : "arma";
            output[i].displayLabel = $"alvo {i + 1}, {weaponName}";
        }

        return output.Count > 0;
    }

    private static int CompareTargetOptionsByPriority(PodeMirarTargetOption a, PodeMirarTargetOption b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        bool samePair = a.attackerUnit == b.attackerUnit && a.targetUnit == b.targetUnit;
        if (samePair)
        {
            int preferredCmp = b.isPreferredTargetForWeapon.CompareTo(a.isPreferredTargetForWeapon);
            if (preferredCmp != 0)
                return preferredCmp;

            int weaponIndexCmp = a.embarkedWeaponIndex.CompareTo(b.embarkedWeaponIndex);
            if (weaponIndexCmp != 0)
                return weaponIndexCmp;
        }

        return a.sensorOrder.CompareTo(b.sensorOrder);
    }

    private static bool EvaluateWeaponPriority(
        WeaponPriorityData data,
        WeaponCategory category,
        GameUnitClass targetClass)
    {
        return data != null && data.IsPreferredTarget(category, targetClass);
    }

    private static GameUnitClass ResolveUnitClass(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return data.unitClass;

        return GameUnitClass.Infantry;
    }

    public static bool HasAnyFireCandidateWeapon(UnitManager attacker, SensorMovementMode movementMode)
    {
        if (attacker == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> embarkedWeapons = attacker.GetEmbarkedWeapons();
        if (embarkedWeapons == null || embarkedWeapons.Count == 0)
            return false;

        for (int i = 0; i < embarkedWeapons.Count; i++)
        {
            if (TryBuildCandidate(embarkedWeapons[i], i, movementMode, out _, requireAmmo: true))
                return true;
        }

        return false;
    }

    public static void CollectValidFireCells(
        UnitManager attacker,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase,
        SensorMovementMode movementMode,
        ICollection<Vector3Int> outputCells,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool fogOfWarEnabled = true)
    {
        if (outputCells == null)
            return;

        outputCells.Clear();
        if (attacker == null)
            return;

        Tilemap map = boardTilemap != null ? boardTilemap : attacker.BoardTilemap;
        if (map == null)
            return;

        IReadOnlyList<UnitEmbarkedWeapon> embarkedWeapons = attacker.GetEmbarkedWeapons();
        if (embarkedWeapons == null || embarkedWeapons.Count == 0)
            return;

        List<WeaponRangeCandidate> candidates = new List<WeaponRangeCandidate>(embarkedWeapons.Count);
        int globalMaxRange = 0;
        for (int i = 0; i < embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = embarkedWeapons[i];
            if (!TryBuildCandidate(embarked, i, movementMode, out WeaponRangeCandidate candidate, requireAmmo: true))
                continue;

            candidates.Add(candidate);
            if (candidate.maxRange > globalMaxRange)
                globalMaxRange = candidate.maxRange;
        }

        if (candidates.Count == 0 || globalMaxRange <= 0)
            return;

        Vector3Int origin = attacker.CurrentCellPosition;
        origin.z = 0;
        Dictionary<Vector3Int, int> distances = BuildDistanceMap(map, origin, globalMaxRange);
        foreach (KeyValuePair<Vector3Int, int> pair in distances)
        {
            Vector3Int cell = pair.Key;
            int distance = pair.Value;
            if (distance <= 0)
                continue;

            for (int i = 0; i < candidates.Count; i++)
            {
                WeaponRangeCandidate candidate = candidates[i];
                if (distance < candidate.minRange || distance > candidate.maxRange)
                    continue;

                WeaponData weapon = candidate.embarked.weapon;
                if (weapon == null)
                    continue;

                if (!CanWeaponHitVirtualCell(
                    map,
                    terrainDatabase,
                    origin,
                    cell,
                    attacker,
                    dpqAirHeightConfig,
                    fogOfWarEnabled,
                    candidate.embarked.selectedTrajectory,
                    weapon))
                {
                    continue;
                }

                outputCells.Add(cell);
                break;
            }
        }
    }

    private static bool CanWeaponHitVirtualCell(
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int originCell,
        Vector3Int targetCell,
        UnitManager attacker,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool fogOfWarEnabled,
        WeaponTrajectoryType trajectory,
        WeaponData weapon)
    {
        if (map == null || weapon == null)
            return false;

        // "Alvo virtual": o dominio/camada do hex de destino precisa ser compativel com a arma.
        if (!TryResolveTerrainAtCell(map, terrainDatabase, targetCell, out TerrainTypeData destinationTerrain) || destinationTerrain == null)
            return false;
        if (!TerrainAllowsWeaponTrajectory(destinationTerrain, weapon))
            return false;

        if (trajectory != WeaponTrajectoryType.Straight)
            return true;

        return HasValidStraightLineOfFire(
            map,
            terrainDatabase,
            originCell,
            targetCell,
            attacker,
            null,
            dpqAirHeightConfig,
            fogOfWarEnabled,
            weapon,
            out _,
            out _);
    }

    private static bool TryBuildCandidate(UnitEmbarkedWeapon embarked, int index, SensorMovementMode movementMode, out WeaponRangeCandidate candidate, bool requireAmmo)
    {
        candidate = default;
        if (embarked == null || embarked.weapon == null)
            return false;

        if (requireAmmo && embarked.squadAmmunition <= 0)
            return false;

        int min = embarked.GetRangeMin();
        int max = embarked.GetRangeMax();

        if (movementMode == SensorMovementMode.MoveuAndando)
        {
            if (min != 1)
                return false;

            min = 1;
            max = 1;
        }

        if (max < min)
            max = min;

        if (max <= 0)
            return false;

        candidate = new WeaponRangeCandidate
        {
            index = index,
            embarked = embarked,
            minRange = min,
            maxRange = max
        };
        return true;
    }

    private static bool IsEnemyTargetCandidate(UnitManager attacker, UnitManager target)
    {
        if (attacker == null || target == null)
            return false;

        if (target == attacker)
            return false;

        if (!target.gameObject.activeInHierarchy || target.IsEmbarked)
            return false;

        return attacker.TeamId != target.TeamId;
    }

    private static bool TryResolveCounterAttack(
        UnitManager defender,
        UnitManager attacker,
        int distance,
        out WeaponData counterWeapon,
        out int counterEmbarkedIndex,
        out string reason)
    {
        counterWeapon = null;
        counterEmbarkedIndex = -1;
        reason = string.Empty;

        if (defender == null || attacker == null)
        {
            reason = "Defensor ou atacante invalido.";
            return false;
        }

        // Revide: apenas alcance minimo 1 (regra de sensor para unidade em revide).
        if (distance != 1)
        {
            reason = "Distancia para revide diferente de 1.";
            return false;
        }

        IReadOnlyList<UnitEmbarkedWeapon> defenderWeapons = defender.GetEmbarkedWeapons();
        if (defenderWeapons == null || defenderWeapons.Count == 0)
        {
            reason = "Defensor sem armas.";
            return false;
        }

        bool hasMinRangeOne = false;
        bool hasAmmo = false;
        bool hasLayerCompatible = false;

        for (int i = 0; i < defenderWeapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = defenderWeapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;

            int min = embarked.GetRangeMin();
            if (min != 1)
                continue;
            hasMinRangeOne = true;

            if (embarked.squadAmmunition <= 0)
                continue;
            hasAmmo = true;

            if (!embarked.weapon.SupportsOperationOn(attacker.GetDomain(), attacker.GetHeightLevel()))
                continue;
            hasLayerCompatible = true;

            counterWeapon = embarked.weapon;
            counterEmbarkedIndex = i;
            return true;
        }

        if (!hasMinRangeOne)
            reason = "Defensor sem arma de revide (range min 1).";
        else if (!hasAmmo)
            reason = "Defensor sem municao para revide.";
        else if (!hasLayerCompatible)
            reason = "Layer do atacante incompativel para revide.";
        else
            reason = "Revide indisponivel.";

        return false;
    }

    private static bool HasValidStraightLineOfFire(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int originCell,
        Vector3Int targetCell,
        UnitManager attacker,
        UnitManager defender,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool fogOfWarEnabled,
        WeaponData weapon,
        out List<Vector3Int> intermediateCells,
        out Vector3Int blockedCell)
    {
        intermediateCells = new List<Vector3Int>();
        blockedCell = Vector3Int.zero;
        if (tilemap == null || weapon == null)
            return false;

        if (!TryResolveCellVision(
                tilemap,
                terrainDatabase,
                originCell,
                attacker,
                dpqAirHeightConfig,
                out int originEv,
                out _))
        {
            originEv = 0;
        }

        if (!TryResolveCellVision(
                tilemap,
                terrainDatabase,
                targetCell,
                defender,
                dpqAirHeightConfig,
                out int targetEv,
                out _))
        {
            targetEv = 0;
        }

        List<Vector3Int> crossedCells = GetIntermediateCellsByCellLerp(originCell, targetCell);
        intermediateCells.AddRange(crossedCells);
        for (int i = 0; i < crossedCells.Count; i++)
        {
            Vector3Int cell = crossedCells[i];

            if (!TryResolveTerrainAtCell(tilemap, terrainDatabase, cell, out TerrainTypeData terrain) || terrain == null)
                continue;

            if (!TerrainAllowsWeaponTrajectory(terrain, weapon))
            {
                blockedCell = cell;
                return false;
            }

            if (!fogOfWarEnabled)
                continue;

            if (!TryResolveCellVision(
                    tilemap,
                    terrainDatabase,
                    cell,
                    null,
                    dpqAirHeightConfig,
                    out int cellEv,
                    out bool cellBlocksLoS))
            {
                continue;
            }

            if (!cellBlocksLoS)
                continue;

            if (cellEv <= 0)
                continue;

            // Excecao suprema: alvo com EV pelo menos 2 acima do obstaculo nao e bloqueado por ele.
            if ((targetEv - cellEv) >= 2)
                continue;

            float t = (i + 1f) / (crossedCells.Count + 1f);
            float losHeightAtCell = Mathf.Lerp(originEv, targetEv, t);
            if (cellEv > losHeightAtCell)
            {
                blockedCell = cell;
                return false;
            }
        }

        return true;
    }

    private static bool TerrainAllowsWeaponTrajectory(TerrainTypeData terrain, WeaponData weapon)
    {
        if (terrain == null || weapon == null)
            return false;

        if (weapon.SupportsOperationOn(terrain.domain, terrain.heightLevel))
            return true;

        if (terrain.alwaysAllowAirDomain && weapon.SupportsOperationOn(Domain.Air, HeightLevel.AirLow))
            return true;

        if (terrain.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < terrain.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = terrain.aditionalDomainsAllowed[i];
            if (weapon.SupportsOperationOn(mode.domain, mode.heightLevel))
                return true;
        }

        return false;
    }

    private static List<Vector3Int> GetIntermediateCellsByCellLerp(Vector3Int originCell, Vector3Int targetCell)
    {
        List<Vector3Int> cells = new List<Vector3Int>();

        originCell.z = 0;
        targetCell.z = 0;
        int dx = targetCell.x - originCell.x;
        int dy = targetCell.y - originCell.y;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        if (steps <= 1)
            return cells;

        HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        for (int i = 1; i < steps; i++)
        {
            int x = Mathf.RoundToInt(originCell.x + (dx * (float)i / steps));
            int y = Mathf.RoundToInt(originCell.y + (dy * (float)i / steps));
            Vector3Int cell = new Vector3Int(x, y, 0);
            if (seen.Add(cell))
                cells.Add(cell);
        }

        return cells;
    }

    private static bool TryResolveTerrainAtCell(Tilemap terrainTilemap, TerrainDatabase terrainDatabase, Vector3Int cell, out TerrainTypeData terrain)
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

    private static Dictionary<Vector3Int, int> BuildDistanceMap(Tilemap tilemap, Vector3Int origin, int maxRange)
    {
        Dictionary<Vector3Int, int> distances = new Dictionary<Vector3Int, int>();
        if (tilemap == null || maxRange < 0)
            return distances;

        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        origin.z = 0;
        distances[origin] = 0;
        frontier.Enqueue(origin);

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            int currentDistance = distances[current];
            if (currentDistance >= maxRange)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                if (distances.ContainsKey(next))
                    continue;

                int nextDistance = currentDistance + 1;
                if (nextDistance > maxRange)
                    continue;

                distances[next] = nextDistance;
                frontier.Enqueue(next);
            }
        }

        return distances;
    }

    private static void AppendInvalid(
        List<PodeMirarInvalidOption> output,
        UnitManager attacker,
        UnitManager target,
        WeaponData weapon,
        int embarkedWeaponIndex,
        int distance,
        string attackerPositionLabel,
        string defenderPositionLabel,
        string reason,
        Vector3Int blockedCell,
        List<Vector3Int> lineOfFireIntermediateCells)
    {
        if (output == null)
            return;

        output.Add(new PodeMirarInvalidOption
        {
            attackerUnit = attacker,
            targetUnit = target,
            weapon = weapon,
            embarkedWeaponIndex = embarkedWeaponIndex,
            distance = distance,
            attackerPositionLabel = attackerPositionLabel,
            defenderPositionLabel = defenderPositionLabel,
            reason = reason,
            blockedCell = blockedCell,
            lineOfFireIntermediateCells = lineOfFireIntermediateCells != null
                ? new List<Vector3Int>(lineOfFireIntermediateCells)
                : new List<Vector3Int>()
        });
    }

    private static string ResolveUnitPositionLabel(Tilemap referenceTilemap, TerrainDatabase terrainDatabase, UnitManager unit, Vector3Int cell)
    {
        if (unit == null)
            return "-";

        cell.z = 0;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(referenceTilemap, cell);
        if (construction != null)
        {
            string constructionName = !string.IsNullOrWhiteSpace(construction.ConstructionDisplayName)
                ? construction.ConstructionDisplayName
                : (!string.IsNullOrWhiteSpace(construction.ConstructionId) ? construction.ConstructionId : construction.name);
            return $"Construcao: {constructionName}";
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(referenceTilemap, cell);
        if (structure != null)
        {
            string structureName = !string.IsNullOrWhiteSpace(structure.displayName)
                ? structure.displayName
                : (!string.IsNullOrWhiteSpace(structure.id) ? structure.id : structure.name);
            return $"Estrutura: {structureName}";
        }

        if (TryResolveTerrainAtCell(referenceTilemap, terrainDatabase, cell, out TerrainTypeData terrain) && terrain != null)
        {
            string terrainName = !string.IsNullOrWhiteSpace(terrain.displayName)
                ? terrain.displayName
                : (!string.IsNullOrWhiteSpace(terrain.id) ? terrain.id : terrain.name);
            return $"Terreno: {terrainName}";
        }

        return "Terreno: (desconhecido)";
    }

    private static bool TryResolveCellVision(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        UnitManager occupantUnit,
        DPQAirHeightConfig dpqAirHeightConfig,
        out int ev,
        out bool blockLoS)
    {
        ev = 0;
        blockLoS = true;
        if (!TryResolveTerrainAtCell(tilemap, terrainDatabase, cell, out TerrainTypeData terrain) || terrain == null)
            return false;

        ConstructionData constructionData = null;
        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(tilemap, cell);
        if (construction != null)
        {
            ConstructionDatabase db = construction.ConstructionDatabase;
            string id = construction.ConstructionId;
            if (db != null && !string.IsNullOrWhiteSpace(id) && db.TryGetById(id, out ConstructionData data) && data != null)
                constructionData = data;
        }

        StructureData structureData = StructureOccupancyRules.GetStructureAtCell(tilemap, cell);

        Domain domain = Domain.Land;
        HeightLevel height = HeightLevel.Surface;
        if (occupantUnit != null)
        {
            domain = occupantUnit.GetDomain();
            height = occupantUnit.GetHeightLevel();
        }

        TerrainVisionResolver.Resolve(
            terrain,
            domain,
            height,
            dpqAirHeightConfig,
            constructionData,
            structureData,
            out ev,
            out blockLoS);

        return true;
    }

    private static bool IsTargetDetectableByAttacker(UnitManager attacker, UnitManager target)
    {
        if (attacker == null || target == null)
            return false;

        // Placeholder para futura regra de stealth/deteccao.
        // Ex.: F-117 em AirHigh pode estar visivel por LoS, mas invisivel sem detector adequado.
        return true;
    }
}
