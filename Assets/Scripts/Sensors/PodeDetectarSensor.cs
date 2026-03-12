using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeDetectarSensor
{
    public static bool IsTargetObservedByTeam(
        UnitManager target,
        int viewerTeamId,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool enableLosValidation = true,
        bool enableSpotter = true,
        bool enableStealthValidation = true)
    {
        if (target == null || !target.gameObject.activeInHierarchy || target.IsEmbarked)
            return false;

        if ((int)target.TeamId == viewerTeamId)
            return true;

        Tilemap boardMap = map != null ? map : target.BoardTilemap;
        if (boardMap == null)
            return false;

        UnitData targetData = null;
        target.TryGetUnitData(out targetData);
        bool isStealthTarget = targetData != null && targetData.IsStealthUnit(target.GetDomain(), target.GetHeightLevel());
        if (isStealthTarget && enableStealthValidation && PodeMirarSensor.IsStealthTargetRevealedForTeam(target, viewerTeamId))
            return true;

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager observer = units[i];
            if (observer == null || !observer.gameObject.activeInHierarchy || observer.IsEmbarked)
                continue;
            if ((int)observer.TeamId != viewerTeamId)
                continue;

            if (CanObserverObserveTarget(
                    observer,
                    target,
                    boardMap,
                    terrainDatabase,
                    dpqAirHeightConfig,
                    enableLosValidation,
                    enableSpotter,
                    enableStealthValidation))
            {
                return true;
            }
        }

        return false;
    }

    public static void CollectVisibleCells(
        UnitManager observer,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        ICollection<Vector3Int> visibleCellsOutput,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool enableLosValidation = true,
        bool enableSpotter = true,
        bool useOccupantLayerForTarget = true)
    {
        if (visibleCellsOutput == null)
            return;

        if (observer == null || observer.IsEmbarked)
            return;

        Tilemap boardMap = map != null ? map : observer.BoardTilemap;
        if (boardMap == null)
            return;

        UnitData observerData = null;
        observer.TryGetUnitData(out observerData);

        int maxRange = ResolveObserverMaxVisionRange(observerData, observer);
        if (maxRange <= 0)
            return;

        Vector3Int observerCell = observer.CurrentCellPosition;
        observerCell.z = 0;
        visibleCellsOutput.Add(observerCell);

        Dictionary<Vector3Int, int> distanceMap = BuildDistanceMap(boardMap, observerCell, maxRange);
        foreach (KeyValuePair<Vector3Int, int> pair in distanceMap)
        {
            Vector3Int cell = pair.Key;
            int distance = pair.Value;
            if (distance <= 0)
                continue;
            if (!TryResolveObservationTargetLayer(
                    boardMap,
                    terrainDatabase,
                    cell,
                    out Domain targetDomain,
                    out HeightLevel targetHeight,
                    useOccupantLayerForTarget))
                continue;

            int detectionRange = ResolveDetectionRange(observer, observerData, null, targetDomain, targetHeight);
            if (distance > detectionRange)
                continue;

            bool hasDirectLos = !enableLosValidation || HasValidStraightObservationLine(
                boardMap,
                terrainDatabase,
                observerCell,
                cell,
                observer,
                null,
                dpqAirHeightConfig,
                out _,
                out _,
                out _,
                enableLosValidation: true);

            bool hasObservation = hasDirectLos;
            if (!hasObservation)
            {
                if (distance <= detectionRange)
                {
                    hasObservation = true;
                }
                else if (enableSpotter && ShouldUseForwardObserverRule(targetDomain, targetHeight))
                {
                    hasObservation = TryFindForwardObserverForVirtualCell(
                        observer,
                        cell,
                        boardMap,
                        terrainDatabase,
                        dpqAirHeightConfig,
                        enableLosValidation: true);
                }
            }

            if (hasObservation)
                visibleCellsOutput.Add(cell);
        }
    }

    public static bool CollectDetection(
        UnitManager observer,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        List<PodeDetectarOption> detectedStealthOutput,
        List<PodeDetectarOption> undetectedStealthOutput,
        List<PodeDetectarOption> spottedCandidatesOutput,
        List<PodeDetectarOption> inRangeButLosBlockedOutput,
        out string reason,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool enableLosValidation = true,
        bool enableSpotter = true,
        bool enableStealthValidation = true)
    {
        reason = string.Empty;
        if (detectedStealthOutput == null || undetectedStealthOutput == null || spottedCandidatesOutput == null || inRangeButLosBlockedOutput == null)
        {
            reason = "Listas de output nao podem ser nulas.";
            return false;
        }

        detectedStealthOutput.Clear();
        undetectedStealthOutput.Clear();
        spottedCandidatesOutput.Clear();
        inRangeButLosBlockedOutput.Clear();

        if (observer == null)
        {
            reason = "Selecione uma unidade observadora.";
            return false;
        }

        if (observer.IsEmbarked)
        {
            reason = "Unidade embarcada nao pode detectar.";
            return false;
        }

        Tilemap boardMap = map != null ? map : observer.BoardTilemap;
        if (boardMap == null)
        {
            reason = "Tilemap indisponivel para o scan.";
            return false;
        }

        UnitData observerData = null;
        observer.TryGetUnitData(out observerData);

        int maxRange = ResolveObserverMaxVisionRange(observerData, observer);
        if (maxRange <= 0)
        {
            reason = "Observador sem alcance de visao valido.";
            return false;
        }

        Vector3Int observerCell = observer.CurrentCellPosition;
        observerCell.z = 0;
        Dictionary<Vector3Int, int> distanceMap = BuildDistanceMap(boardMap, observerCell, maxRange);

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager target = units[i];
            if (!IsEnemyTargetCandidate(observer, target))
                continue;

            UnitData targetData = null;
            target.TryGetUnitData(out targetData);
            bool isStealthTarget = targetData != null && targetData.IsStealthUnit(target.GetDomain(), target.GetHeightLevel());

            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            if (!distanceMap.TryGetValue(targetCell, out int distance))
                continue;

            Domain targetDomain = target.GetDomain();
            HeightLevel targetHeight = target.GetHeightLevel();
            int detectionRange = ResolveDetectionRange(observer, observerData, target, targetDomain, targetHeight);
            if (distance > detectionRange)
                continue;

            List<Vector3Int> lineCells = new List<Vector3Int>();
            Vector3Int blockedCell = Vector3Int.zero;
            bool hasDirectLos = !enableLosValidation || HasValidStraightObservationLine(
                boardMap,
                terrainDatabase,
                observerCell,
                targetCell,
                observer,
                target,
                dpqAirHeightConfig,
                out lineCells,
                out _,
                out blockedCell,
                enableLosValidation: true);

            bool usedForwardObserver = false;
            UnitManager forwardObserver = null;
            bool canUseForwardObserver = enableSpotter && ShouldUseForwardObserverRule(targetDomain, targetHeight);
            if (!hasDirectLos)
            {
                // Auto-observador: quando o alvo esta no proprio alcance de visao,
                // a unidade pode atuar como seu proprio observer.
                if (distance <= detectionRange)
                {
                    usedForwardObserver = true;
                    forwardObserver = observer;
                }
                else if (canUseForwardObserver)
                {
                    List<UnitManager> forwardObservers = CollectForwardObserversForTarget(
                        observer,
                        target,
                        boardMap,
                        terrainDatabase,
                        dpqAirHeightConfig,
                        enableLosValidation: true);
                    if (forwardObservers.Count > 0)
                    {
                        usedForwardObserver = true;
                        forwardObserver = forwardObservers[0];
                    }
                }
            }

            bool hasObservation = hasDirectLos || usedForwardObserver;
            if (!hasObservation)
            {
                if (isStealthTarget)
                {
                    undetectedStealthOutput.Add(new PodeDetectarOption
                    {
                        observerUnit = observer,
                        targetUnit = target,
                        observerCell = observerCell,
                        targetCell = targetCell,
                        distance = distance,
                        targetDomain = targetDomain,
                        targetHeightLevel = targetHeight,
                        detectionRangeUsed = detectionRange,
                        hasDirectLos = false,
                        usedForwardObserver = false,
                        forwardObserverUnit = null,
                        lineOfSightIntermediateCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>(),
                        blockedCell = blockedCell,
                        reason = "Furtiva no alcance, mas nao detectada por falta de LOS."
                    });
                }

                inRangeButLosBlockedOutput.Add(new PodeDetectarOption
                {
                    observerUnit = observer,
                    targetUnit = target,
                    observerCell = observerCell,
                    targetCell = targetCell,
                    distance = distance,
                    targetDomain = targetDomain,
                    targetHeightLevel = targetHeight,
                    detectionRangeUsed = detectionRange,
                    hasDirectLos = false,
                    usedForwardObserver = false,
                    forwardObserverUnit = null,
                    lineOfSightIntermediateCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>(),
                    blockedCell = blockedCell,
                    reason = "No alcance, mas sem LOS."
                });
                continue;
            }

            if (isStealthTarget)
            {
                if (!enableStealthValidation)
                {
                    detectedStealthOutput.Add(new PodeDetectarOption
                    {
                        observerUnit = observer,
                        targetUnit = target,
                        observerCell = observerCell,
                        targetCell = targetCell,
                        distance = distance,
                        targetDomain = targetDomain,
                        targetHeightLevel = targetHeight,
                        detectionRangeUsed = detectionRange,
                        hasDirectLos = hasDirectLos,
                        usedForwardObserver = usedForwardObserver,
                        forwardObserverUnit = forwardObserver,
                        lineOfSightIntermediateCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>(),
                        blockedCell = blockedCell,
                        reason = "Detectado (Stealth validation desativada no Game Setup)."
                    });
                    continue;
                }

                bool canDetectStealth = observerData != null &&
                    observerData.CanDetectStealthFor(targetDomain, targetHeight, targetData);
                if (!canDetectStealth)
                {
                    undetectedStealthOutput.Add(new PodeDetectarOption
                    {
                        observerUnit = observer,
                        targetUnit = target,
                        observerCell = observerCell,
                        targetCell = targetCell,
                        distance = distance,
                        targetDomain = targetDomain,
                        targetHeightLevel = targetHeight,
                        detectionRangeUsed = detectionRange,
                        hasDirectLos = hasDirectLos,
                        usedForwardObserver = usedForwardObserver,
                        forwardObserverUnit = forwardObserver,
                        lineOfSightIntermediateCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>(),
                        blockedCell = blockedCell,
                        reason = "Furtiva no alcance/visao, mas sem especializacao de deteccao stealth."
                    });
                    continue;
                }

                detectedStealthOutput.Add(new PodeDetectarOption
                {
                    observerUnit = observer,
                    targetUnit = target,
                    observerCell = observerCell,
                    targetCell = targetCell,
                    distance = distance,
                    targetDomain = targetDomain,
                    targetHeightLevel = targetHeight,
                    detectionRangeUsed = detectionRange,
                    hasDirectLos = hasDirectLos,
                    usedForwardObserver = usedForwardObserver,
                    forwardObserverUnit = forwardObserver,
                    lineOfSightIntermediateCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>(),
                    blockedCell = blockedCell,
                    reason = usedForwardObserver ? "Detectado via observador avancado." : "Detectado com LOS direta."
                });
                continue;
            }

            spottedCandidatesOutput.Add(new PodeDetectarOption
            {
                observerUnit = observer,
                targetUnit = target,
                observerCell = observerCell,
                targetCell = targetCell,
                distance = distance,
                targetDomain = targetDomain,
                targetHeightLevel = targetHeight,
                detectionRangeUsed = detectionRange,
                hasDirectLos = hasDirectLos,
                usedForwardObserver = usedForwardObserver,
                forwardObserverUnit = forwardObserver,
                lineOfSightIntermediateCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>(),
                blockedCell = blockedCell,
                reason = usedForwardObserver ? "Avistado via observador avancado." : "Avistado com LOS direta."
            });
        }

        reason = $"FurtivasDetectadas={detectedStealthOutput.Count} | FurtivasNaoDetectadas={undetectedStealthOutput.Count} | Avistadas={spottedCandidatesOutput.Count} | SemLOS={inRangeButLosBlockedOutput.Count}";

        return detectedStealthOutput.Count > 0 || spottedCandidatesOutput.Count > 0;
    }

    private static int ResolveObserverMaxVisionRange(UnitData observerData, UnitManager observer)
    {
        int maxRange = observerData != null ? Mathf.Max(1, observerData.visao) : Mathf.Max(1, observer.Visao);
        if (observerData == null || observerData.visionSpecializations == null)
            return maxRange;

        for (int i = 0; i < observerData.visionSpecializations.Count; i++)
        {
            UnitVisionException entry = observerData.visionSpecializations[i];
            if (entry == null)
                continue;

            int value = Mathf.Max(0, entry.vision);
            if (value > maxRange)
                maxRange = value;
        }

        return maxRange;
    }

    private static int ResolveDetectionRange(
        UnitManager observer,
        UnitData observerData,
        UnitManager target,
        Domain targetDomain,
        HeightLevel targetHeight)
    {
        if (observerData != null)
            return Mathf.Max(1, observerData.ResolveVisionFor(targetDomain, targetHeight));
        if (observer != null)
            return Mathf.Max(1, observer.Visao);

        return 1;
    }

    private static bool ShouldUseForwardObserverRule(Domain domain, HeightLevel heightLevel)
    {
        return (domain == Domain.Land && heightLevel == HeightLevel.Surface) ||
            (domain == Domain.Naval && heightLevel == HeightLevel.Surface);
    }

    private static List<UnitManager> CollectForwardObserversForTarget(
        UnitManager observer,
        UnitManager target,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation)
    {
        List<UnitManager> observers = new List<UnitManager>();
        if (observer == null || target == null || map == null)
            return observers;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        int maxObserverRange = GetTeamMaxObservationRangeHexes(observer, target);
        Dictionary<Vector3Int, int> localAroundTarget = BuildDistanceMap(map, targetCell, maxObserverRange);
        if (localAroundTarget.Count == 0)
            return observers;

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager ally = units[i];
            if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                continue;
            if (ally.TeamId != observer.TeamId)
                continue;
            if (ally == observer)
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

    private static int GetObservationRangeHexes(UnitManager unit, UnitManager target)
    {
        if (target != null && unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return Mathf.Max(1, data.ResolveVisionFor(target.GetDomain(), target.GetHeightLevel()));
        if (unit != null)
            return Mathf.Max(1, unit.Visao);

        return 1;
    }

    private static int GetObservationRangeHexes(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return Mathf.Max(1, data.ResolveVisionFor(unit.GetDomain(), unit.GetHeightLevel()));
        if (unit != null)
            return Mathf.Max(1, unit.Visao);

        return 1;
    }

    private static int GetTeamMaxObservationRangeHexes(UnitManager referenceUnit, UnitManager target)
    {
        if (referenceUnit == null)
            return 1;

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

    private static int GetTeamMaxObservationRangeHexes(UnitManager referenceUnit)
    {
        if (referenceUnit == null)
            return 1;

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

    private static bool TryFindForwardObserverForVirtualCell(
        UnitManager observer,
        Vector3Int targetCell,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation)
    {
        if (observer == null || map == null)
            return false;

        targetCell.z = 0;
        int maxObserverRange = GetTeamMaxObservationRangeHexes(observer);
        Dictionary<Vector3Int, int> localAroundTarget = BuildDistanceMap(map, targetCell, maxObserverRange);
        if (localAroundTarget.Count == 0)
            return false;

        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager ally = units[i];
            if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                continue;
            if (ally.TeamId != observer.TeamId)
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

    private static bool IsEnemyTargetCandidate(UnitManager observer, UnitManager target)
    {
        if (observer == null || target == null)
            return false;
        if (target == observer)
            return false;
        if (!target.gameObject.activeInHierarchy || target.IsEmbarked)
            return false;

        return observer.TeamId != target.TeamId;
    }

    private static bool CanObserverObserveTarget(
        UnitManager observer,
        UnitManager target,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation,
        bool enableSpotter,
        bool enableStealthValidation)
    {
        if (observer == null || target == null || boardMap == null)
            return false;

        UnitData observerData = null;
        observer.TryGetUnitData(out observerData);
        UnitData targetData = null;
        target.TryGetUnitData(out targetData);

        Vector3Int observerCell = observer.CurrentCellPosition;
        observerCell.z = 0;
        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;

        Domain targetDomain = target.GetDomain();
        HeightLevel targetHeight = target.GetHeightLevel();
        int detectionRange = ResolveDetectionRange(observer, observerData, target, targetDomain, targetHeight);

        Dictionary<Vector3Int, int> distanceMap = BuildDistanceMap(boardMap, observerCell, detectionRange);
        if (!distanceMap.TryGetValue(targetCell, out int distance))
            return false;
        if (distance > detectionRange)
            return false;

        bool hasDirectLos = !enableLosValidation || HasValidStraightObservationLine(
            boardMap,
            terrainDatabase,
            observerCell,
            targetCell,
            observer,
            target,
            dpqAirHeightConfig,
            out _,
            out _,
            out _,
            enableLosValidation: true);

        bool usedForwardObserver = false;
        if (!hasDirectLos)
        {
            // Mantem o mesmo comportamento do sensor Pode Detectar atual.
            if (distance <= detectionRange)
            {
                usedForwardObserver = true;
            }
            else if (enableSpotter && ShouldUseForwardObserverRule(targetDomain, targetHeight))
            {
                List<UnitManager> forwardObservers = CollectForwardObserversForTarget(
                    observer,
                    target,
                    boardMap,
                    terrainDatabase,
                    dpqAirHeightConfig,
                    enableLosValidation: true);
                usedForwardObserver = forwardObservers.Count > 0;
            }
        }

        bool hasObservation = hasDirectLos || usedForwardObserver;
        if (!hasObservation)
            return false;

        bool isStealthTarget = targetData != null && targetData.IsStealthUnit(targetDomain, targetHeight);
        if (!isStealthTarget || !enableStealthValidation)
            return true;

        return observerData != null && observerData.CanDetectStealthFor(targetDomain, targetHeight, targetData);
    }

    private static bool TryResolveObservationTargetLayer(
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        out Domain domain,
        out HeightLevel height,
        bool useOccupantLayerForTarget = true)
    {
        domain = Domain.Land;
        height = HeightLevel.Surface;

        if (useOccupantLayerForTarget)
        {
            UnitManager occupant = HexOccupancyQuery.FindUnitAtCell(cell);
            if (occupant != null)
            {
                domain = occupant.GetDomain();
                height = occupant.GetHeightLevel();
                return true;
            }
        }

        if (!TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrain) || terrain == null)
            return true;

        domain = terrain.domain;
        height = terrain.heightLevel;
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
        UnitManager observer,
        DPQAirHeightConfig dpqAirHeightConfig,
        int fallbackEv)
    {
        if (observer == null)
            return Mathf.Max(0, fallbackEv);

        Domain domain = observer.GetDomain();
        HeightLevel height = observer.GetHeightLevel();
        if (domain == Domain.Air)
        {
            if (dpqAirHeightConfig != null &&
                dpqAirHeightConfig.TryGetVisionFor(domain, height, out int airEv, out _))
            {
                return Mathf.Max(0, airEv);
            }

            return Mathf.Max(0, fallbackEv);
        }

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
            Tilemap scan = maps[i];
            if (scan == null)
                continue;

            TileBase other = scan.GetTile(cell);
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

    private static List<Vector3Int> GetIntermediateCellsByCellLerp(Tilemap tilemap, Vector3Int originCell, Vector3Int targetCell)
    {
        List<Vector3Int> cells = new List<Vector3Int>();

        originCell.z = 0;
        targetCell.z = 0;
        if (tilemap == null)
            return cells;

        Vector3 originWorld = tilemap.GetCellCenterWorld(originCell);
        Vector3 targetWorld = tilemap.GetCellCenterWorld(targetCell);
        Vector2 originWorld2 = new Vector2(originWorld.x, originWorld.y);
        Vector2 targetWorld2 = new Vector2(targetWorld.x, targetWorld.y);
        float neighborStep = 1f;
        List<Vector3Int> originNeighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, originCell, originNeighbors);
        if (originNeighbors.Count > 0)
        {
            Vector3 n = tilemap.GetCellCenterWorld(originNeighbors[0]);
            neighborStep = Vector2.Distance(originWorld2, new Vector2(n.x, n.y));
            if (neighborStep <= 0.0001f)
                neighborStep = 1f;
        }

        float worldDistance = Vector2.Distance(originWorld2, targetWorld2);
        if (worldDistance <= 0.0001f)
            return cells;

        int approxHexes = Mathf.Max(1, Mathf.CeilToInt(worldDistance / Mathf.Max(0.0001f, neighborStep)));
        if (approxHexes <= 1)
            return cells;

        float borderEpsilon = Mathf.Max(0.01f, neighborStep * 0.08f);
        int sampleCount = approxHexes * 10;
        if (sampleCount <= 1)
            sampleCount = approxHexes * 6;

        HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        List<Vector3Int> centerNeighbors = new List<Vector3Int>(6);
        for (int i = 1; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector2 sample2 = Vector2.Lerp(originWorld2, targetWorld2, t);

            Vector3Int centerCell = tilemap.WorldToCell(new Vector3(sample2.x, sample2.y, 0f));
            centerCell.z = 0;
            if (centerCell != originCell && centerCell != targetCell && seen.Add(centerCell))
                cells.Add(centerCell);

            Vector2 centerWorld2 = ToWorld2(tilemap.GetCellCenterWorld(centerCell));
            float distToCenter = Vector2.Distance(sample2, centerWorld2);

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, centerCell, centerNeighbors);
            for (int n = 0; n < centerNeighbors.Count; n++)
            {
                Vector3Int neighborCell = centerNeighbors[n];
                neighborCell.z = 0;
                if (neighborCell == originCell || neighborCell == targetCell)
                    continue;

                Vector2 neighborWorld2 = ToWorld2(tilemap.GetCellCenterWorld(neighborCell));
                float distToNeighbor = Vector2.Distance(sample2, neighborWorld2);
                if (Mathf.Abs(distToCenter - distToNeighbor) > borderEpsilon)
                    continue;

                if (seen.Add(neighborCell))
                    cells.Add(neighborCell);
            }
        }

        return cells;
    }

    private static Vector2 ToWorld2(Vector3 world)
    {
        return new Vector2(world.x, world.y);
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
}
