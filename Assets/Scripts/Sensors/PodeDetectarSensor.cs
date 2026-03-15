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
        if (!IsUnitOnBoard(target, boardMap))
            return false;

        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager observer = units[i];
            if (observer == null || !observer.gameObject.activeInHierarchy || observer.IsEmbarked)
                continue;
            if ((int)observer.TeamId != viewerTeamId)
                continue;
            if (!IsUnitOnBoard(observer, boardMap))
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
        bool useOccupantLayerForTarget = true,
        bool preserveObserverLayerRangeForHexVisibility = false,
        bool forceVirtualTargetLayer = false,
        Domain forcedVirtualTargetDomain = Domain.Land,
        HeightLevel forcedVirtualTargetHeight = HeightLevel.Surface,
        int forcedDetectionRangeOverride = -1,
        bool skipSpecializedTargetLayers = false,
        bool useRangeOnlyForAirHighWhenConfigured = false)
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
        Domain observerDomain = observer.GetDomain();
        HeightLevel observerHeight = observer.GetHeightLevel();
        int observerLayerRangeFloor = preserveObserverLayerRangeForHexVisibility
            ? ResolveDetectionRange(observer, observerData, null, observerDomain, observerHeight)
            : 0;

        int maxRange = forcedDetectionRangeOverride >= 0
            ? Mathf.Max(0, forcedDetectionRangeOverride)
            : ResolveObserverMaxVisionRange(observerData, observer);
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
            Domain targetDomain;
            HeightLevel targetHeight;
            if (forceVirtualTargetLayer)
            {
                targetDomain = forcedVirtualTargetDomain;
                targetHeight = forcedVirtualTargetHeight;
            }
            else
            {
                if (!TryResolveObservationTargetLayer(
                        boardMap,
                        terrainDatabase,
                        cell,
                        out targetDomain,
                        out targetHeight,
                        useOccupantLayerForTarget))
                {
                    continue;
                }
            }

            if (skipSpecializedTargetLayers &&
                HasVisionSpecializationForLayer(observerData, targetDomain, targetHeight))
            {
                continue;
            }

            if (!forceVirtualTargetLayer && preserveObserverLayerRangeForHexVisibility)
            {
                if (CanObserveCellByAnyObserverVisionLayer(
                        observer,
                        observerData,
                        boardMap,
                        terrainDatabase,
                        dpqAirHeightConfig,
                        observerCell,
                        cell,
                        distance,
                        targetDomain,
                        targetHeight,
                        enableLosValidation,
                        enableSpotter,
                        useRangeOnlyForAirHighWhenConfigured))
                {
                    visibleCellsOutput.Add(cell);
                }

                continue;
            }

            int detectionRange = forcedDetectionRangeOverride >= 0
                ? Mathf.Max(0, forcedDetectionRangeOverride)
                : ResolveDetectionRange(observer, observerData, null, targetDomain, targetHeight);
            if (preserveObserverLayerRangeForHexVisibility && observerLayerRangeFloor > detectionRange)
                detectionRange = observerLayerRangeFloor;
            if (distance > detectionRange)
                continue;

            bool skipLosForCurrentTarget = useRangeOnlyForAirHighWhenConfigured &&
                IsAirHighRangeOnlyVision(dpqAirHeightConfig, targetDomain, targetHeight);
            bool hasDirectLos = skipLosForCurrentTarget || !enableLosValidation || HasValidStraightObservationLine(
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
                enableLosValidation: true,
                forcedTargetDomain: forceVirtualTargetLayer ? forcedVirtualTargetDomain : null,
                forcedTargetHeightLevel: forceVirtualTargetLayer ? forcedVirtualTargetHeight : null);

            bool hasObservation = hasDirectLos;
            if (!hasObservation)
            {
                if (enableSpotter && ShouldUseForwardObserverRule(targetDomain, targetHeight))
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

    private static bool CanObserveCellByAnyObserverVisionLayer(
        UnitManager observer,
        UnitData observerData,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        Vector3Int observerCell,
        Vector3Int targetCell,
        int distance,
        Domain resolvedTargetDomain,
        HeightLevel resolvedTargetHeight,
        bool enableLosValidation,
        bool enableSpotter,
        bool useRangeOnlyForAirHighWhenConfigured)
    {
        if (CanObserveCellWithLayer(
                observer,
                observerData,
                boardMap,
                terrainDatabase,
                dpqAirHeightConfig,
                observerCell,
                targetCell,
                distance,
                resolvedTargetDomain,
                resolvedTargetHeight,
                enableLosValidation,
                enableSpotter,
                useRangeOnlyForAirHighWhenConfigured))
        {
            return true;
        }

        if (observerData == null || observerData.visionSpecializations == null || observerData.visionSpecializations.Count <= 0)
            return false;

        HashSet<int> seen = new HashSet<int>();
        for (int i = 0; i < observerData.visionSpecializations.Count; i++)
        {
            UnitVisionException specialization = observerData.visionSpecializations[i];
            if (specialization == null)
                continue;

            Domain domain = specialization.domain;
            HeightLevel height = specialization.heightLevel;
            int key = ((int)domain * 100) + (int)height;
            if (!seen.Add(key))
                continue;
            if (domain == resolvedTargetDomain && height == resolvedTargetHeight)
                continue;

            if (CanObserveCellWithLayer(
                    observer,
                    observerData,
                    boardMap,
                    terrainDatabase,
                    dpqAirHeightConfig,
                    observerCell,
                    targetCell,
                    distance,
                    domain,
                    height,
                    enableLosValidation,
                    enableSpotter,
                    useRangeOnlyForAirHighWhenConfigured))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanObserveCellWithLayer(
        UnitManager observer,
        UnitData observerData,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        DPQAirHeightConfig dpqAirHeightConfig,
        Vector3Int observerCell,
        Vector3Int targetCell,
        int distance,
        Domain targetDomain,
        HeightLevel targetHeight,
        bool enableLosValidation,
        bool enableSpotter,
        bool useRangeOnlyForAirHighWhenConfigured)
    {
        int detectionRange = ResolveDetectionRange(observer, observerData, null, targetDomain, targetHeight);
        if (distance > detectionRange)
            return false;

        bool skipLosForCurrentTarget = useRangeOnlyForAirHighWhenConfigured &&
            IsAirHighRangeOnlyVision(dpqAirHeightConfig, targetDomain, targetHeight);
        bool hasDirectLos = skipLosForCurrentTarget || !enableLosValidation || HasValidStraightObservationLine(
            boardMap,
            terrainDatabase,
            observerCell,
            targetCell,
            observer,
            null,
            dpqAirHeightConfig,
            out _,
            out _,
            out _,
            enableLosValidation: true,
            forcedTargetDomain: targetDomain,
            forcedTargetHeightLevel: targetHeight);

        if (hasDirectLos)
            return true;

        if (enableSpotter && ShouldUseForwardObserverRule(targetDomain, targetHeight))
        {
            return TryFindForwardObserverForVirtualCell(
                observer,
                targetCell,
                boardMap,
                terrainDatabase,
                dpqAirHeightConfig,
                enableLosValidation: true);
        }

        return false;
    }

    private static bool HasVisionSpecializationForLayer(UnitData observerData, Domain targetDomain, HeightLevel targetHeight)
    {
        if (observerData == null || observerData.visionSpecializations == null)
            return false;

        for (int i = 0; i < observerData.visionSpecializations.Count; i++)
        {
            UnitVisionException entry = observerData.visionSpecializations[i];
            if (entry == null)
                continue;
            if (entry.domain == targetDomain && entry.heightLevel == targetHeight)
                return true;
        }

        return false;
    }

    private static bool IsAirHighRangeOnlyVision(DPQAirHeightConfig dpqAirHeightConfig, Domain targetDomain, HeightLevel targetHeight)
    {
        if (targetDomain != Domain.Air || targetHeight != HeightLevel.AirHigh)
            return false;
        if (dpqAirHeightConfig == null)
            return false;
        if (!dpqAirHeightConfig.TryGetVisionFor(targetDomain, targetHeight, out _, out bool blockLoS))
            return false;

        return !blockLoS;
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
        if (!IsUnitOnBoard(observer, boardMap))
        {
            reason = "Observador fora do tilemap selecionado.";
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

        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager target = units[i];
            if (!IsEnemyTargetCandidate(observer, target, boardMap))
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
            bool skipLosForCurrentTarget = IsAirHighRangeOnlyVision(dpqAirHeightConfig, targetDomain, targetHeight);
            bool hasDirectLos = skipLosForCurrentTarget || !enableLosValidation || HasValidStraightObservationLine(
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
                if (canUseForwardObserver)
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

                string stealthDetectionReason = ResolveStealthDetectionReason(
                    observerData,
                    targetData,
                    targetDomain,
                    targetHeight);
                string observationModeReason = usedForwardObserver
                    ? "via observador avancado"
                    : "com LOS direta";
                string detectedReason = string.IsNullOrWhiteSpace(stealthDetectionReason)
                    ? $"Detectado stealth {observationModeReason}."
                    : $"{stealthDetectionReason} ({observationModeReason}).";

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
                    reason = detectedReason
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

    public static bool TryGetObservationLineDebug(
        UnitManager observer,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int targetCell,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation,
        Domain? forcedTargetDomain,
        HeightLevel? forcedTargetHeightLevel,
        out float finalReachedEv,
        out float losHeightAtBlockedCell,
        out int blockedCellEv,
        out Vector3Int blockedCell,
        out float losHeightAtStrongestPassedCell,
        out int strongestPassedCellEv,
        out Vector3Int strongestPassedCell,
        out List<float> lineRiseHeights)
    {
        finalReachedEv = 0f;
        losHeightAtBlockedCell = 0f;
        blockedCellEv = 0;
        blockedCell = Vector3Int.zero;
        losHeightAtStrongestPassedCell = 0f;
        strongestPassedCellEv = 0;
        strongestPassedCell = Vector3Int.zero;
        lineRiseHeights = new List<float>();

        if (observer == null)
            return false;

        Tilemap boardMap = map != null ? map : observer.BoardTilemap;
        if (boardMap == null)
            return false;

        Vector3Int originCell = observer.CurrentCellPosition;
        originCell.z = 0;
        targetCell.z = 0;

        bool hasDirectLos = HasValidStraightObservationLine(
            boardMap,
            terrainDatabase,
            originCell,
            targetCell,
            observer,
            null,
            dpqAirHeightConfig,
            out List<Vector3Int> intermediateCells,
            out List<float> evPath,
            out blockedCell,
            enableLosValidation: enableLosValidation,
            forcedTargetDomain: forcedTargetDomain,
            forcedTargetHeightLevel: forcedTargetHeightLevel);

        if (evPath != null && evPath.Count > 0)
        {
            finalReachedEv = evPath[evPath.Count - 1];
            lineRiseHeights.AddRange(evPath);
        }
        losHeightAtBlockedCell = finalReachedEv;

        if (blockedCell != Vector3Int.zero && intermediateCells != null && evPath != null)
        {
            int blockedIndex = intermediateCells.IndexOf(blockedCell);
            if (blockedIndex >= 0)
            {
                int evPathIndex = blockedIndex + 1; // +1 because index 0 is origin EV.
                if (evPathIndex >= 0 && evPathIndex < evPath.Count)
                    losHeightAtBlockedCell = evPath[evPathIndex];
            }

            if (TryResolveCellVision(
                    boardMap,
                    terrainDatabase,
                    blockedCell,
                    null,
                    dpqAirHeightConfig,
                    out int resolvedBlockedEv,
                    out _,
                    forcedDomain: forcedTargetDomain,
                    forcedHeightLevel: forcedTargetHeightLevel))
            {
                blockedCellEv = resolvedBlockedEv;
            }
        }

        if (intermediateCells != null && evPath != null)
        {
            for (int i = 0; i < intermediateCells.Count; i++)
            {
                Vector3Int cell = intermediateCells[i];
                int evPathIndex = i + 1; // index 0 is origin EV
                if (evPathIndex < 0 || evPathIndex >= evPath.Count)
                    continue;

                if (!TryResolveCellVision(
                        boardMap,
                        terrainDatabase,
                        cell,
                        null,
                        dpqAirHeightConfig,
                        out int cellEv,
                        out bool cellBlocksLoS,
                        forcedDomain: forcedTargetDomain,
                        forcedHeightLevel: forcedTargetHeightLevel))
                {
                    continue;
                }

                if (cellEv <= 0)
                    continue;

                float losHeightAtCell = evPath[evPathIndex];
                if (cellEv > losHeightAtCell)
                    continue;

                if (cellEv > strongestPassedCellEv)
                {
                    strongestPassedCellEv = cellEv;
                    strongestPassedCell = cell;
                    losHeightAtStrongestPassedCell = losHeightAtCell;
                }
            }
        }

        return hasDirectLos;
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
        if (!IsUnitOnBoard(observer, map) || !IsUnitOnBoard(target, map))
            return observers;

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;
        int maxObserverRange = GetTeamMaxObservationRangeHexes(observer, target, map);
        Dictionary<Vector3Int, int> localAroundTarget = BuildDistanceMap(map, targetCell, maxObserverRange);
        if (localAroundTarget.Count == 0)
            return observers;

        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager ally = units[i];
            if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                continue;
            if (ally.TeamId != observer.TeamId)
                continue;
            if (ally == observer)
                continue;
            if (!IsUnitOnBoard(ally, map))
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

    private static int GetTeamMaxObservationRangeHexes(UnitManager referenceUnit, UnitManager target, Tilemap boardMap)
    {
        if (referenceUnit == null || boardMap == null)
            return 1;

        int maxRange = GetObservationRangeHexes(referenceUnit, target);
        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager ally = units[i];
            if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                continue;
            if (ally.TeamId != referenceUnit.TeamId)
                continue;
            if (!IsUnitOnBoard(ally, boardMap))
                continue;

            int allyRange = GetObservationRangeHexes(ally, target);
            if (allyRange > maxRange)
                maxRange = allyRange;
        }

        return Mathf.Max(1, maxRange);
    }

    private static int GetTeamMaxObservationRangeHexes(UnitManager referenceUnit, Tilemap boardMap)
    {
        if (referenceUnit == null || boardMap == null)
            return 1;

        int maxRange = GetObservationRangeHexes(referenceUnit);
        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager ally = units[i];
            if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                continue;
            if (ally.TeamId != referenceUnit.TeamId)
                continue;
            if (!IsUnitOnBoard(ally, boardMap))
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
        if (!IsUnitOnBoard(observer, map))
            return false;

        targetCell.z = 0;
        int maxObserverRange = GetTeamMaxObservationRangeHexes(observer, map);
        Dictionary<Vector3Int, int> localAroundTarget = BuildDistanceMap(map, targetCell, maxObserverRange);
        if (localAroundTarget.Count == 0)
            return false;

        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager ally = units[i];
            if (ally == null || !ally.gameObject.activeInHierarchy || ally.IsEmbarked)
                continue;
            if (ally.TeamId != observer.TeamId)
                continue;
            if (!IsUnitOnBoard(ally, map))
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

    private static bool IsEnemyTargetCandidate(UnitManager observer, UnitManager target, Tilemap boardMap)
    {
        if (observer == null || target == null)
            return false;
        if (target == observer)
            return false;
        if (!target.gameObject.activeInHierarchy || target.IsEmbarked)
            return false;
        if (!IsUnitOnBoard(observer, boardMap) || !IsUnitOnBoard(target, boardMap))
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
        if (!IsUnitOnBoard(observer, boardMap) || !IsUnitOnBoard(target, boardMap))
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

        bool skipLosForCurrentTarget = IsAirHighRangeOnlyVision(dpqAirHeightConfig, targetDomain, targetHeight);
        bool hasDirectLos = skipLosForCurrentTarget || !enableLosValidation || HasValidStraightObservationLine(
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
            if (enableSpotter && ShouldUseForwardObserverRule(targetDomain, targetHeight))
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

    private static bool IsUnitOnBoard(UnitManager unit, Tilemap boardMap)
    {
        if (unit == null || boardMap == null)
            return false;

        if (unit.BoardTilemap == null || unit.BoardTilemap != boardMap)
            return false;

        return unit.gameObject.scene == boardMap.gameObject.scene;
    }

    private static string ResolveStealthDetectionReason(
        UnitData observerData,
        UnitData targetData,
        Domain targetDomain,
        HeightLevel targetHeight)
    {
        if (observerData == null)
            return string.Empty;

        UnitVisionException specialization = FindVisionSpecialization(observerData, targetDomain, targetHeight);
        if (specialization == null)
            return string.Empty;

        if (targetData == null || specialization.detectUnitsWithFollowingSkills == null || specialization.detectUnitsWithFollowingSkills.Count == 0)
            return string.Empty;

        List<SkillData> targetStealthSkills = targetData.ResolveStealthSkillsForDetection(targetDomain, targetHeight);
        if (targetStealthSkills == null || targetStealthSkills.Count == 0)
            return string.Empty;

        if (!TryGetFirstMatchingSkill(specialization.detectUnitsWithFollowingSkills, targetStealthSkills, out SkillData matchedSkill))
            return string.Empty;

        string skillName = ResolveSkillName(matchedSkill);
        return string.IsNullOrWhiteSpace(skillName)
            ? "Detectado via skill da visao especializada"
            : $"Detectado via skill '{skillName}' da visao especializada";
    }

    private static UnitVisionException FindVisionSpecialization(UnitData observerData, Domain targetDomain, HeightLevel targetHeight)
    {
        if (observerData == null || observerData.visionSpecializations == null)
            return null;

        for (int i = 0; i < observerData.visionSpecializations.Count; i++)
        {
            UnitVisionException entry = observerData.visionSpecializations[i];
            if (entry == null)
                continue;
            if (entry.domain != targetDomain || entry.heightLevel != targetHeight)
                continue;

            return entry;
        }

        return null;
    }

    private static bool TryGetFirstMatchingSkill(
        List<SkillData> detectorSkills,
        List<SkillData> targetSkills,
        out SkillData matchedSkill)
    {
        matchedSkill = null;
        if (detectorSkills == null || targetSkills == null)
            return false;

        for (int i = 0; i < detectorSkills.Count; i++)
        {
            SkillData detectorSkill = detectorSkills[i];
            if (detectorSkill == null)
                continue;

            string detectorId = string.IsNullOrWhiteSpace(detectorSkill.id) ? string.Empty : detectorSkill.id.Trim();
            for (int j = 0; j < targetSkills.Count; j++)
            {
                SkillData targetSkill = targetSkills[j];
                if (targetSkill == null)
                    continue;

                if (ReferenceEquals(detectorSkill, targetSkill))
                {
                    matchedSkill = detectorSkill;
                    return true;
                }

                string targetId = string.IsNullOrWhiteSpace(targetSkill.id) ? string.Empty : targetSkill.id.Trim();
                if (detectorId.Length > 0 && targetId.Length > 0 &&
                    string.Equals(detectorId, targetId, System.StringComparison.OrdinalIgnoreCase))
                {
                    matchedSkill = detectorSkill;
                    return true;
                }
            }
        }

        return false;
    }

    private static string ResolveSkillName(SkillData skill)
    {
        if (skill == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(skill.displayName))
            return skill.displayName.Trim();
        if (!string.IsNullOrWhiteSpace(skill.id))
            return skill.id.Trim();
        return skill.name;
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

        if (TryResolveConstructionAtCell(map, cell, out ConstructionData constructionData) && constructionData != null)
        {
            domain = constructionData.domain;
            height = constructionData.heightLevel;
            return true;
        }

        StructureData structureData = StructureOccupancyRules.GetStructureAtCell(map, cell);
        if (structureData != null)
        {
            domain = structureData.domain;
            height = structureData.heightLevel;
            return true;
        }

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
        bool enableLosValidation,
        Domain? forcedTargetDomain = null,
        HeightLevel? forcedTargetHeightLevel = null)
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

        if (!forcedTargetDomain.HasValue &&
            !forcedTargetHeightLevel.HasValue &&
            target == null &&
            TryResolveObservationTargetLayer(
                tilemap,
                terrainDatabase,
                targetCell,
                out Domain resolvedTargetDomain,
                out HeightLevel resolvedTargetHeightLevel,
                useOccupantLayerForTarget: false))
        {
            forcedTargetDomain = resolvedTargetDomain;
            forcedTargetHeightLevel = resolvedTargetHeightLevel;
        }

        if (!TryResolveCellVision(
                tilemap,
                terrainDatabase,
                targetCell,
                target,
                dpqAirHeightConfig,
                out int targetEv,
                out _,
                forcedDomain: forcedTargetDomain,
                forcedHeightLevel: forcedTargetHeightLevel))
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
                    out bool cellBlocksLoS,
                    forcedDomain: forcedTargetDomain,
                    forcedHeightLevel: forcedTargetHeightLevel))
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
        out bool blockLoS,
        Domain? forcedDomain = null,
        HeightLevel? forcedHeightLevel = null)
    {
        ev = 0;
        blockLoS = true;
        if (!TryResolveTerrainAtCell(tilemap, terrainDatabase, cell, out TerrainTypeData terrain) || terrain == null)
            return false;

        TryResolveConstructionAtCell(tilemap, cell, out ConstructionData constructionData);
        StructureData structureData = StructureOccupancyRules.GetStructureAtCell(tilemap, cell);

        Domain domain = Domain.Land;
        HeightLevel height = HeightLevel.Surface;
        if (forcedDomain.HasValue && forcedHeightLevel.HasValue)
        {
            domain = forcedDomain.Value;
            height = forcedHeightLevel.Value;
        }
        else if (occupantUnit != null)
        {
            domain = occupantUnit.GetDomain();
            height = occupantUnit.GetHeightLevel();
        }
        else
        {
            TryResolveObservationTargetLayer(
                tilemap,
                terrainDatabase,
                cell,
                out domain,
                out height,
                useOccupantLayerForTarget: false);
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

    private static bool TryResolveConstructionAtCell(Tilemap tilemap, Vector3Int cell, out ConstructionData constructionData)
    {
        constructionData = null;
        if (tilemap == null)
            return false;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(tilemap, cell);
        if (construction == null)
            return false;

        ConstructionDatabase db = construction.ConstructionDatabase;
        string id = construction.ConstructionId;
        if (db == null || string.IsNullOrWhiteSpace(id))
            return false;

        if (!db.TryGetById(id, out ConstructionData data) || data == null)
            return false;

        constructionData = data;
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

