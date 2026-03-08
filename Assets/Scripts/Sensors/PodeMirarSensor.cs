using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeMirarSensor
{
    private const string InvalidReasonNoAmmo = "Falta de municao.";
    private const string InvalidReasonLayer = "Layer do alvo incompativel com a arma.";
    private const string InvalidReasonLdtBlocked = "Linha de tiro invalida para os dominios no trajeto (LdT).";
    private const string InvalidReasonLosBlocked = "Linha de visada bloqueada.";
    private const string InvalidReasonNoForwardObserver = "Sem observador avancado (alcance visual 3 hex).";
    private const string InvalidReasonStealth = "Alvo nao detectado (stealth placeholder).";
    private const int DefaultObservationRangeHexes = 3;
    private static MatchController cachedMatchController;
    private static readonly Dictionary<int, StealthRevealState> stealthRevealStateByTarget = new Dictionary<int, StealthRevealState>();

    private sealed class StealthRevealState
    {
        public int revealForAllUntilTurn = int.MinValue;
        public readonly Dictionary<int, int> revealByTeamUntilTurn = new Dictionary<int, int>();
    }

    private static string ResolveInvalidReasonId(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return PodeMirarInvalidOption.ReasonIdGeneric;

        if (reason.StartsWith("Fora de alcance da arma", System.StringComparison.OrdinalIgnoreCase))
            return PodeMirarInvalidOption.ReasonIdOutOfRange;
        if (reason.IndexOf(InvalidReasonNoAmmo, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return PodeMirarInvalidOption.ReasonIdNoAmmo;
        if (reason.IndexOf(InvalidReasonLayer, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return PodeMirarInvalidOption.ReasonIdLayer;
        if (reason.IndexOf(InvalidReasonLdtBlocked, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return PodeMirarInvalidOption.ReasonIdLdtBlocked;
        if (reason.IndexOf(InvalidReasonLosBlocked, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return PodeMirarInvalidOption.ReasonIdLosBlocked;
        if (reason.IndexOf(InvalidReasonNoForwardObserver, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return PodeMirarInvalidOption.ReasonIdNoForwardObserver;
        if (reason.IndexOf(InvalidReasonStealth, System.StringComparison.OrdinalIgnoreCase) >= 0)
            return PodeMirarInvalidOption.ReasonIdStealth;

        return PodeMirarInvalidOption.ReasonIdGeneric;
    }

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
        bool enableLdtValidation = true,
        bool enableLosValidation = true,
        bool enableSpotter = true,
        bool enableStealthValidation = true)
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
                {
                    string rangeReason =
                        $"Fora de alcance da arma (dist={distance}, range={weaponCandidate.minRange}-{weaponCandidate.maxRange}).";
                    AppendInvalid(
                        invalidOutput,
                        attacker,
                        target,
                        weaponCandidate.embarked != null ? weaponCandidate.embarked.weapon : null,
                        weaponCandidate.index,
                        distance,
                        attackerPositionLabel,
                        defenderPositionLabel,
                        rangeReason,
                        Vector3Int.zero,
                        null,
                        null);
                    continue;
                }

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
                        null,
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
                        null,
                        null);
                    continue;
                }

                List<Vector3Int> intermediateCells = null;
                List<float> evPath = null;
                Vector3Int blockedCell = Vector3Int.zero;
                bool usedForwardObserver = false;
                UnitManager forwardObserver = null;
                string forwardObserverReason = string.Empty;
                List<UnitManager> forwardObserverCandidates = null;
                if (weaponCandidate.embarked.selectedTrajectory == WeaponTrajectoryType.Straight)
                {
                    bool hasDirectLos = HasValidStraightLineOfFire(
                            map,
                            terrainDatabase,
                            origin,
                            targetCell,
                            attacker,
                            target,
                            dpqAirHeightConfig,
                            weapon,
                            out intermediateCells,
                            out evPath,
                            out blockedCell,
                            enableLdtValidation,
                            enableLosValidation);
                    bool hasValidLdtPath = true;
                    if (!hasDirectLos && enableLdtValidation)
                    {
                        hasValidLdtPath = HasValidStraightLineOfFire(
                            map,
                            terrainDatabase,
                            origin,
                            targetCell,
                            attacker,
                            target,
                            dpqAirHeightConfig,
                            weapon,
                            out _,
                            out _,
                            out _,
                            enableLdtValidation: true,
                            enableLosValidation: false);
                    }
                    if (!hasDirectLos)
                    {
                        if (hasValidLdtPath && enableLosValidation && enableSpotter && TryFindForwardObserverForIndirectFire(
                                attacker,
                                target,
                                map,
                                terrainDatabase,
                                dpqAirHeightConfig,
                                out UnitManager blockerBypassObserver,
                                enableLosValidation))
                        {
                            hasDirectLos = true;
                            usedForwardObserver = blockerBypassObserver != null;
                            forwardObserver = blockerBypassObserver;
                            if (usedForwardObserver)
                                forwardObserverReason = "Forward observer confirmou LoS apesar de bloqueio direto.";
                        }

                        if (!hasDirectLos)
                        {
                            string invalidReason;
                            if (!hasValidLdtPath)
                            {
                                invalidReason = InvalidReasonLdtBlocked;
                            }
                            else if (enableLosValidation)
                            {
                                invalidReason = $"{InvalidReasonLosBlocked} ({blockedCell.x},{blockedCell.y}) | {InvalidReasonNoForwardObserver}";
                            }
                            else
                            {
                                invalidReason = InvalidReasonLdtBlocked;
                            }
                            AppendInvalid(
                                invalidOutput,
                                attacker,
                                target,
                                weapon,
                                weaponCandidate.index,
                                distance,
                                attackerPositionLabel,
                                defenderPositionLabel,
                                invalidReason,
                                blockedCell,
                                intermediateCells,
                                evPath);
                            continue;
                        }
                    }

                    int attackerObservationRange = GetObservationRangeHexes(attacker, target);
                    bool requiresForwardObserver = distance > 1 && enableLosValidation && enableSpotter && distance > attackerObservationRange;
                    if (requiresForwardObserver)
                    {
                        if (!TryFindForwardObserverForIndirectFire(
                                attacker,
                                target,
                                map,
                                terrainDatabase,
                                dpqAirHeightConfig,
                                out UnitManager longRangeObserver,
                                enableLosValidation))
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
                                InvalidReasonNoForwardObserver,
                                Vector3Int.zero,
                                null,
                                null);
                            continue;
                        }
                        usedForwardObserver = longRangeObserver != null;
                        forwardObserver = longRangeObserver;
                        if (usedForwardObserver)
                            forwardObserverReason = $"Forward observer confirmou alvo fora da visao do atacante ({attackerObservationRange} hex).";
                    }
                }
                else
                {
                    // Fogo indireto simplificado:
                    // 1) Usa LdT da arma parabólica (alcance/domínio já validados acima).
                    // 2) Exige observador avançado aliado em até 3 hexes do alvo com LoS válida até ele.
                    bool allowParabolic = true;
                    if (enableLosValidation)
                    {
                        if (enableSpotter)
                        {
                            bool shooterSeesTarget = false;
                            int attackerObservationRange = GetObservationRangeHexes(attacker, target);
                            if (distance <= 1)
                            {
                                shooterSeesTarget = true;
                            }
                            else if (distance <= attackerObservationRange)
                            {
                                shooterSeesTarget = HasValidStraightObservationLine(
                                    map,
                                    terrainDatabase,
                                    origin,
                                    targetCell,
                                    attacker,
                                    target,
                                    dpqAirHeightConfig,
                                    out _,
                                    out _,
                                    out _,
                                    enableLosValidation);
                            }

                            if (!shooterSeesTarget)
                            {
                                allowParabolic = TryFindForwardObserverForIndirectFire(
                                    attacker,
                                    target,
                                    map,
                                    terrainDatabase,
                                    dpqAirHeightConfig,
                                    out UnitManager indirectObserver,
                                    enableLosValidation);
                                if (allowParabolic && indirectObserver != null)
                                {
                                    usedForwardObserver = true;
                                    forwardObserver = indirectObserver;
                                    forwardObserverReason = "Forward observer confirmou alvo para tiro parabolico.";
                                }
                            }
                        }
                        // LoS=true + Spotter=false: mantém comportamento "parabólico ignora LoS".
                    }

                    if (!allowParabolic)
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
                            InvalidReasonNoForwardObserver,
                            Vector3Int.zero,
                            null,
                            null);
                        continue;
                    }
                }

                if (enableStealthValidation && !IsTargetDetectableByAttacker(attacker, target, map, terrainDatabase))
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
                        intermediateCells,
                        evPath);
                    continue;
                }

                bool canCounter = TryResolveCounterAttack(
                    target,
                    attacker,
                    distance,
                    weaponPriorityData,
                    out WeaponData counterWeapon,
                    out int counterIndex,
                    out string counterReason);
                GameUnitClass targetClass = ResolveUnitClass(target);
                WeaponCategory weaponCategory = weapon.WeaponCategory;
                bool preferredTarget = EvaluateWeaponPriority(weaponPriorityData, weaponCategory, targetClass);
                if (usedForwardObserver)
                    forwardObserverCandidates = CollectForwardObserversForTarget(attacker, target, map, terrainDatabase, dpqAirHeightConfig, enableLosValidation);

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
                    lineOfFireIntermediateCells = intermediateCells ?? new List<Vector3Int>(),
                    lineOfFireEvPath = evPath ?? new List<float>(),
                    usedForwardObserver = usedForwardObserver,
                    forwardObserverUnit = forwardObserver,
                    forwardObserverReason = forwardObserverReason,
                    forwardObserverCandidates = forwardObserverCandidates ?? new List<UnitManager>()
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
        bool enableLdtValidation = true,
        bool enableLosValidation = true,
        bool enableSpotter = true)
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
                    candidate.embarked.selectedTrajectory,
                    weapon,
                    enableLdtValidation,
                    enableLosValidation,
                    enableSpotter,
                    pair.Value))
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
        WeaponTrajectoryType trajectory,
        WeaponData weapon,
        bool enableLdtValidation,
        bool enableLosValidation,
        bool enableSpotter,
        int distanceFromAttacker)
    {
        if (map == null || weapon == null)
            return false;

        // "Alvo virtual": o dominio/camada do hex de destino precisa ser compativel com a arma.
        if (enableLdtValidation)
        {
            if (!TryResolveTerrainAtCell(map, terrainDatabase, targetCell, out TerrainTypeData destinationTerrain) || destinationTerrain == null)
                return false;
            if (!TerrainAllowsWeaponTrajectory(destinationTerrain, weapon))
                return false;
        }

        if (!enableLosValidation)
            return true;

        bool hasDirectLine = trajectory != WeaponTrajectoryType.Straight || HasValidStraightLineOfFire(
            map,
            terrainDatabase,
            originCell,
            targetCell,
            attacker,
            null,
            dpqAirHeightConfig,
            weapon,
            out _,
            out _,
            out _,
            enableLdtValidation,
            enableLosValidation);
        if (!hasDirectLine)
            return false;

        int attackerObservationRange = GetObservationRangeHexes(attacker);
        if (distanceFromAttacker <= 1)
            return true;

        if (!enableSpotter || distanceFromAttacker <= attackerObservationRange)
            return true;

        return TryFindForwardObserverForVirtualCell(
            attacker,
            targetCell,
            map,
            terrainDatabase,
            dpqAirHeightConfig,
            enableLosValidation);
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

    private static bool TryFindForwardObserverForIndirectFire(
        UnitManager attacker,
        UnitManager target,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        out UnitManager observer,
        bool enableLosValidation)
    {
        observer = null;
        List<UnitManager> observers = CollectForwardObserversForTarget(
            attacker,
            target,
            map,
            terrainDatabase,
            dpqAirHeightConfig,
            enableLosValidation);
        if (observers.Count <= 0)
            return false;

        observer = observers[0];
        return true;
    }

    private static bool TryFindForwardObserverForVirtualCell(
        UnitManager attacker,
        Vector3Int targetCell,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation)
    {
        if (attacker == null || map == null)
            return false;

        targetCell.z = 0;
        int maxObserverRange = GetTeamMaxObservationRangeHexes(attacker);
        Dictionary<Vector3Int, int> localAroundTarget = BuildDistanceMap(map, targetCell, maxObserverRange);
        if (localAroundTarget.Count == 0)
            return false;

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager ally = units[i];
            if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                continue;
            if (ally.TeamId != attacker.TeamId)
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            if (!localAroundTarget.TryGetValue(allyCell, out int allyDistanceToTarget))
                continue;

            int allyObservationRange = GetObservationRangeHexes(ally);
            if (allyDistanceToTarget > allyObservationRange)
                continue;

            if (HasValidStraightObservationLine(
                    map,
                    terrainDatabase,
                    allyCell,
                    targetCell,
                    ally,
                    null,
                    dpqAirHeightConfig,
                    out _,
                    out _,
                    out _,
                    enableLosValidation))
            {
                return true;
            }
        }

        return false;
    }

    private static List<UnitManager> CollectForwardObserversForTarget(
        UnitManager attacker,
        UnitManager target,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation)
    {
        List<UnitManager> observers = new List<UnitManager>();
        if (attacker == null || target == null || map == null)
            return observers;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        int maxObserverRange = GetTeamMaxObservationRangeHexes(attacker, target);
        Dictionary<Vector3Int, int> localAroundTarget = BuildDistanceMap(map, targetCell, maxObserverRange);
        if (localAroundTarget.Count == 0)
            return observers;

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager ally = units[i];
            if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                continue;
            if (ally.TeamId != attacker.TeamId)
                continue;
            if (ally == target)
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            if (!localAroundTarget.TryGetValue(allyCell, out int allyDistanceToTarget))
                continue;

            int allyObservationRange = GetObservationRangeHexes(ally, target);
            if (allyDistanceToTarget > allyObservationRange)
                continue;

            if (!HasValidStraightObservationLine(
                    map,
                    terrainDatabase,
                    allyCell,
                    targetCell,
                    ally,
                    target,
                    dpqAirHeightConfig,
                    out _,
                    out _,
                    out _,
                    enableLosValidation))
            {
                continue;
            }

            if (!observers.Contains(ally))
                observers.Add(ally);
        }

        return observers;
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

    private static int GetObservationRangeHexes(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return Mathf.Max(1, data.ResolveVisionFor(unit.GetDomain(), unit.GetHeightLevel()));
        if (unit != null)
            return Mathf.Max(1, unit.Visao);

        return DefaultObservationRangeHexes;
    }

    private static int GetObservationRangeHexes(UnitManager unit, UnitManager target)
    {
        if (target == null)
            return GetObservationRangeHexes(unit);

        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return Mathf.Max(1, data.ResolveVisionFor(target.GetDomain(), target.GetHeightLevel()));
        if (unit != null)
            return Mathf.Max(1, unit.Visao);

        return DefaultObservationRangeHexes;
    }

    private static int GetTeamMaxObservationRangeHexes(UnitManager referenceUnit)
    {
        if (referenceUnit == null)
            return DefaultObservationRangeHexes;

        int maxRange = GetObservationRangeHexes(referenceUnit);
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager ally = units[i];
            if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                continue;
            if (ally.TeamId != referenceUnit.TeamId)
                continue;

            int allyRange = GetObservationRangeHexes(ally);
            if (allyRange > maxRange)
                maxRange = allyRange;
        }

        return Mathf.Max(1, maxRange);
    }

    private static int GetTeamMaxObservationRangeHexes(UnitManager referenceUnit, UnitManager target)
    {
        if (referenceUnit == null)
            return DefaultObservationRangeHexes;

        int maxRange = GetObservationRangeHexes(referenceUnit, target);
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager ally = units[i];
            if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                continue;
            if (ally.TeamId != referenceUnit.TeamId)
                continue;

            int allyRange = GetObservationRangeHexes(ally, target);
            if (allyRange > maxRange)
                maxRange = allyRange;
        }

        return Mathf.Max(1, maxRange);
    }

    private static bool TryResolveCounterAttack(
        UnitManager defender,
        UnitManager attacker,
        int distance,
        WeaponPriorityData weaponPriorityData,
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
        int fallbackIndex = -1;
        WeaponData fallbackWeapon = null;
        GameUnitClass attackerClass = ResolveUnitClass(attacker);

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

            // Guarda primeira arma valida como fallback.
            if (fallbackWeapon == null)
            {
                fallbackWeapon = embarked.weapon;
                fallbackIndex = i;
            }

            // Prioriza arma com alvo preferencial para a classe do atacante.
            if (EvaluateWeaponPriority(weaponPriorityData, embarked.weapon.WeaponCategory, attackerClass))
            {
                counterWeapon = embarked.weapon;
                counterEmbarkedIndex = i;
                return true;
            }
        }

        if (fallbackWeapon != null)
        {
            counterWeapon = fallbackWeapon;
            counterEmbarkedIndex = fallbackIndex;
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
        WeaponData weapon,
        out List<Vector3Int> intermediateCells,
        out List<float> evPath,
        out Vector3Int blockedCell,
        bool enableLdtValidation,
        bool enableLosValidation)
    {
        intermediateCells = new List<Vector3Int>();
        evPath = new List<float>();
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
        originEv = ResolveOriginEvForLos(tilemap, terrainDatabase, originCell, attacker, dpqAirHeightConfig, originEv);

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

        evPath.Add(originEv);
        List<Vector3Int> crossedCells = GetIntermediateCellsByCellLerp(tilemap, originCell, targetCell);
        intermediateCells.AddRange(crossedCells);
        for (int i = 0; i < crossedCells.Count; i++)
        {
            Vector3Int cell = crossedCells[i];
            float t = (i + 1f) / (crossedCells.Count + 1f);
            float losHeightAtCell = Mathf.Lerp(originEv, targetEv, t);
            evPath.Add(losHeightAtCell);

            if (!TryResolveTerrainAtCell(tilemap, terrainDatabase, cell, out TerrainTypeData terrain) || terrain == null)
                continue;

            if (enableLdtValidation && !TerrainAllowsWeaponTrajectory(terrain, weapon))
            {
                blockedCell = cell;
                return false;
            }

            if (!enableLosValidation)
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
            if (cellEv > losHeightAtCell)
            {
                blockedCell = cell;
                return false;
            }
        }

        evPath.Add(targetEv);
        return true;
    }

    private static bool HasValidStraightObservationLine(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int originCell,
        Vector3Int targetCell,
        UnitManager observer,
        UnitManager target,
        DPQAirHeightConfig dpqAirHeightConfig,
        out List<Vector3Int> intermediateCells,
        out List<float> evPath,
        out Vector3Int blockedCell,
        bool enableLosValidation)
    {
        intermediateCells = new List<Vector3Int>();
        evPath = new List<float>();
        blockedCell = Vector3Int.zero;
        if (tilemap == null)
            return false;

        if (!TryResolveCellVision(
                tilemap,
                terrainDatabase,
                originCell,
                observer,
                dpqAirHeightConfig,
                out int originEv,
                out _))
        {
            originEv = 0;
        }
        originEv = ResolveOriginEvForLos(tilemap, terrainDatabase, originCell, observer, dpqAirHeightConfig, originEv);

        if (!TryResolveCellVision(
                tilemap,
                terrainDatabase,
                targetCell,
                target,
                dpqAirHeightConfig,
                out int targetEv,
                out _))
        {
            targetEv = 0;
        }

        evPath.Add(originEv);
        List<Vector3Int> crossedCells = GetIntermediateCellsByCellLerp(tilemap, originCell, targetCell);
        intermediateCells.AddRange(crossedCells);
        for (int i = 0; i < crossedCells.Count; i++)
        {
            Vector3Int cell = crossedCells[i];
            float t = (i + 1f) / (crossedCells.Count + 1f);
            float losHeightAtCell = Mathf.Lerp(originEv, targetEv, t);
            evPath.Add(losHeightAtCell);

            if (!enableLosValidation)
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

            if (!cellBlocksLoS || cellEv <= 0)
                continue;

            if ((targetEv - cellEv) >= 2)
                continue;

            if (cellEv > losHeightAtCell)
            {
                blockedCell = cell;
                return false;
            }
        }

        evPath.Add(targetEv);
        return true;
    }

    private static int ResolveOriginEvForLos(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int originCell,
        UnitManager attacker,
        DPQAirHeightConfig dpqAirHeightConfig,
        int fallbackEv)
    {
        if (attacker == null)
            return Mathf.Max(0, fallbackEv);

        Domain domain = attacker.GetDomain();
        HeightLevel height = attacker.GetHeightLevel();
        if (domain == Domain.Air)
        {
            if (dpqAirHeightConfig != null &&
                dpqAirHeightConfig.TryGetVisionFor(domain, height, out int airEv, out _))
            {
                return Mathf.Max(0, airEv);
            }

            return Mathf.Max(0, fallbackEv);
        }

        // Regra de origem da LoS: unidades no chao partem do EV de sua camada (Surface = 0),
        // a menos que o terreno marque explicitamente que o atirador herda EV do terreno.
        originCell.z = 0;
        if (tilemap != null &&
            terrainDatabase != null &&
            TryResolveTerrainAtCell(tilemap, terrainDatabase, originCell, out TerrainTypeData originTerrain) &&
            originTerrain != null &&
            originTerrain.shooterInheritsTerrainEv)
        {
            return Mathf.Max(0, originTerrain.ev);
        }

        return 0;
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

    private static List<Vector3Int> GetIntermediateCellsByCellLerp(Tilemap tilemap, Vector3Int originCell, Vector3Int targetCell)
    {
        List<Vector3Int> cells = new List<Vector3Int>();

        originCell.z = 0;
        targetCell.z = 0;
        int dx = targetCell.x - originCell.x;
        int dy = targetCell.y - originCell.y;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        if (steps <= 1)
            return cells;

        if (tilemap == null)
            return cells;

        Vector3 originWorld = tilemap.GetCellCenterWorld(originCell);
        Vector3 targetWorld = tilemap.GetCellCenterWorld(targetCell);
        int sampleCount = steps * 4; // supersampling leve para reduzir aliasing em bordas.
        if (sampleCount <= 1)
            sampleCount = steps;

        HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        for (int i = 1; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector3 sampleWorld = Vector3.Lerp(originWorld, targetWorld, t);
            Vector3Int cell = tilemap.WorldToCell(sampleWorld);
            cell.z = 0;
            if (cell == originCell || cell == targetCell)
                continue;
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
        List<Vector3Int> lineOfFireIntermediateCells,
        List<float> lineOfFireEvPath)
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
            reasonId = ResolveInvalidReasonId(reason),
            blockedCell = blockedCell,
            lineOfFireIntermediateCells = lineOfFireIntermediateCells != null
                ? new List<Vector3Int>(lineOfFireIntermediateCells)
                : new List<Vector3Int>(),
            lineOfFireEvPath = lineOfFireEvPath != null
                ? new List<float>(lineOfFireEvPath)
                : new List<float>()
        });
    }

    private static string ResolveUnitPositionLabel(Tilemap referenceTilemap, TerrainDatabase terrainDatabase, UnitManager unit, Vector3Int cell)
    {
        if (unit == null)
            return "-";

        cell.z = 0;
        Domain domain = unit.GetDomain();
        HeightLevel height = unit.GetHeightLevel();
        bool isGroundLayer = domain == Domain.Land && height == HeightLevel.Surface;
        string layerLabel = $"{domain}/{height}";

        if (!isGroundLayer)
        {
            if (TryResolveTerrainAtCell(referenceTilemap, terrainDatabase, cell, out TerrainTypeData airTerrain) && airTerrain != null)
            {
                string terrainName = !string.IsNullOrWhiteSpace(airTerrain.displayName)
                    ? airTerrain.displayName
                    : (!string.IsNullOrWhiteSpace(airTerrain.id) ? airTerrain.id : airTerrain.name);
                return $"Layer: {layerLabel} | Sobre: {terrainName}";
            }

            return $"Layer: {layerLabel}";
        }

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

    private static bool IsTargetDetectableByAttacker(
        UnitManager attacker,
        UnitManager target,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase)
    {
        if (attacker == null || target == null)
            return false;

        if (!IsStealthTarget(target))
            return true;

        if (IsTargetFreelyDetectedByForcedEndMovementRules(target, boardMap, terrainDatabase))
            return true;

        int attackerTeamId = (int)attacker.TeamId;
        int currentTurn = GetCurrentTurnSafe();
        StealthRevealScope revealScope = StealthRevealScope.AllTeams;
        int revealTurns = 1;
        ResolveStealthRevealRules(target, out revealScope, out revealTurns);
        if (IsTargetAlreadyRevealedForTeam(target, attackerTeamId, currentTurn))
            return true;

        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return false;

        target.TryGetUnitData(out UnitData targetData);
        bool canDetectNow = attackerData.CanDetectStealthFor(target.GetDomain(), target.GetHeightLevel(), targetData);
        if (!canDetectNow)
            return false;

        RegisterStealthReveal(target, attackerTeamId, currentTurn, revealTurns, revealScope);
        return true;
    }

    private static bool IsStealthTarget(UnitManager target)
    {
        if (target == null)
            return false;

        if (target.TryGetUnitData(out UnitData targetData) && targetData != null)
            return targetData.IsStealthUnit();

        return false;
    }

    private static void ResolveStealthRevealRules(UnitManager target, out StealthRevealScope revealScope, out int revealTurns)
    {
        revealScope = StealthRevealScope.AllTeams;
        revealTurns = 1;

        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return;

        revealScope = targetData.stealthRevealScope;
        revealTurns = targetData.ResolveStealthVisibleTurns();
    }

    private static bool IsTargetAlreadyRevealedForTeam(UnitManager target, int teamId, int currentTurn)
    {
        int targetKey = target.GetInstanceID();
        if (!stealthRevealStateByTarget.TryGetValue(targetKey, out StealthRevealState state) || state == null)
            return false;

        if (state.revealForAllUntilTurn >= currentTurn)
            return true;

        if (state.revealByTeamUntilTurn.TryGetValue(teamId, out int teamUntilTurn) && teamUntilTurn >= currentTurn)
            return true;

        return false;
    }

    private static void RegisterStealthReveal(
        UnitManager target,
        int detectorTeamId,
        int currentTurn,
        int revealTurns,
        StealthRevealScope revealScope)
    {
        if (target == null)
            return;

        int duration = Mathf.Max(1, revealTurns);
        int untilTurn = currentTurn + duration - 1;
        int targetKey = target.GetInstanceID();
        if (!stealthRevealStateByTarget.TryGetValue(targetKey, out StealthRevealState state) || state == null)
        {
            state = new StealthRevealState();
            stealthRevealStateByTarget[targetKey] = state;
        }

        if (revealScope == StealthRevealScope.AllTeams)
        {
            state.revealForAllUntilTurn = Mathf.Max(state.revealForAllUntilTurn, untilTurn);
            return;
        }

        if (!state.revealByTeamUntilTurn.TryGetValue(detectorTeamId, out int currentTeamUntil) || untilTurn > currentTeamUntil)
            state.revealByTeamUntilTurn[detectorTeamId] = untilTurn;
    }

    private static int GetCurrentTurnSafe()
    {
        if (cachedMatchController == null)
            cachedMatchController = Object.FindAnyObjectByType<MatchController>();

        return cachedMatchController != null ? cachedMatchController.CurrentTurn : 0;
    }

    private static bool IsTargetFreelyDetectedByForcedEndMovementRules(
        UnitManager target,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase)
    {
        if (target == null || boardMap == null)
            return false;

        Vector3Int cell = target.CurrentCellPosition;
        cell.z = 0;
        Domain targetDomain = target.GetDomain();
        HeightLevel targetHeight = target.GetHeightLevel();

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
        if (construction != null &&
            construction.TryResolveConstructionData(out ConstructionData constructionData) &&
            constructionData != null &&
            MatchesAnyLayerMode(constructionData.forceEndMovementOnTerrainDomainForDomains, targetDomain, targetHeight))
        {
            if (ShouldForceDetectByStealthSkills(
                    constructionData.forceDetectUnitsWithFollowingStealthSkills,
                    target))
                return true;

            if (constructionData.forceDetectOnForcedEndMovementDomains)
                return true;
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardMap, cell);
        if (structure != null &&
            MatchesAnyLayerMode(structure.forceEndMovementOnTerrainDomainForDomains, targetDomain, targetHeight))
        {
            if (ShouldForceDetectByStealthSkills(
                    structure.forceDetectUnitsWithFollowingStealthSkills,
                    target))
                return true;

            if (structure.forceDetectOnForcedEndMovementDomains)
                return true;
        }

        if (!TryResolveTerrainAtCell(boardMap, terrainDatabase, cell, out TerrainTypeData terrain) || terrain == null)
            return false;

        if (!MatchesAnyLayerMode(terrain.forceEndMovementOnTerrainDomainForDomains, targetDomain, targetHeight))
            return false;

        if (ShouldForceDetectByStealthSkills(terrain.forceDetectUnitsWithFollowingStealthSkills, target))
            return true;

        return terrain.forceDetectOnForcedEndMovementDomains;
    }

    private static bool MatchesAnyLayerMode(IReadOnlyList<TerrainLayerMode> modes, Domain domain, HeightLevel heightLevel)
    {
        if (modes == null || modes.Count == 0)
            return false;

        for (int i = 0; i < modes.Count; i++)
        {
            TerrainLayerMode mode = modes[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool ShouldForceDetectByStealthSkills(IReadOnlyList<SkillData> detectorSkills, UnitManager target)
    {
        if (detectorSkills == null || detectorSkills.Count == 0 || target == null)
            return false;

        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return false;

        List<SkillData> targetStealthSkills = targetData.ResolveStealthSkillsForDetection();
        if (targetStealthSkills == null || targetStealthSkills.Count == 0)
            return false;

        for (int i = 0; i < detectorSkills.Count; i++)
        {
            SkillData detectorSkill = detectorSkills[i];
            if (detectorSkill == null)
                continue;

            string detectorId = string.IsNullOrWhiteSpace(detectorSkill.id) ? string.Empty : detectorSkill.id.Trim();
            for (int j = 0; j < targetStealthSkills.Count; j++)
            {
                SkillData targetSkill = targetStealthSkills[j];
                if (targetSkill == null)
                    continue;

                if (ReferenceEquals(detectorSkill, targetSkill))
                    return true;

                string targetId = string.IsNullOrWhiteSpace(targetSkill.id) ? string.Empty : targetSkill.id.Trim();
                if (detectorId.Length > 0 && targetId.Length > 0 &&
                    string.Equals(detectorId, targetId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
