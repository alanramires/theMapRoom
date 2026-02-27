using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private void EnterSensorsState(CursorState movementState)
    {
        LogStateStep($"EnterSensorsState(anchor={movementState})");
        CursorState resolvedMovementState = movementState == CursorState.MoveuAndando
            ? CursorState.MoveuAndando
            : CursorState.MoveuParado;

        TryApplyForcedEndMovementLayerBeforeSensors(resolvedMovementState);
        SetCursorState(resolvedMovementState, $"EnterSensorsState(anchor={resolvedMovementState})");
        RefreshSensorsForCurrentState();
    }

    private void BeginMovementToSelectedCell(List<Vector3Int> path)
    {
        LogStateStep("BeginMovementToSelectedCell");
        if (selectedUnit == null || path == null || path.Count < 2)
            return;

        committedMovementPath.Clear();
        committedMovementPath.AddRange(path);
        committedOriginCell = path[0];
        committedDestinationCell = path[path.Count - 1];
        hasCommittedMovement = true;

        if (animationManager == null)
            return;

        Tilemap movementTilemap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        List<Vector3Int> walkedTrail = new List<Vector3Int> { path[0] };
        DrawCommittedPathVisual(walkedTrail);
        animationManager.PlayMovement(
            selectedUnit,
            movementTilemap,
            path,
            playStartSfx: true,
            onAnimationStart: () => PlayMovementStartSfx(selectedUnit),
            onAnimationFinished: () => HandleMovementAnimationCompleted(CursorState.MoveuAndando),
            onCellReached: cell =>
            {
                walkedTrail.Add(cell);
                DrawCommittedPathVisual(walkedTrail);
            });
    }

    private bool BeginRollbackToSelection()
    {
        LogStateStep("BeginRollbackToSelection", rollback: true);
        if (selectedUnit == null || !TryGetCommittedMovementPath(out List<Vector3Int> committedPath, out Vector3Int originCell, out Vector3Int destinationCell))
        {
            Debug.Log("[Rollback] BeginRollbackToSelection abortado: unidade nula ou caminho comprometido indisponivel.");
            return false;
        }

        Vector3Int currentCell = selectedUnit.CurrentCellPosition;
        currentCell.z = 0;
        if (currentCell != destinationCell)
        {
            Debug.Log(
                $"[Rollback] BeginRollbackToSelection abortado: unidade fora do destino comprometido | " +
                $"current={currentCell.x},{currentCell.y} | destination={destinationCell.x},{destinationCell.y}");
            return false;
        }

        List<Vector3Int> reversePath = new List<Vector3Int>(committedPath);
        reversePath.Reverse();
        reversePath[reversePath.Count - 1] = originCell;

        if (animationManager == null)
        {
            Debug.Log("[Rollback] BeginRollbackToSelection abortado: AnimationManager ausente.");
            return false;
        }

        Tilemap movementTilemap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        Debug.Log(
            $"[Rollback] Iniciando animacao de retorno | from={destinationCell.x},{destinationCell.y} -> " +
            $"to={originCell.x},{originCell.y} | steps={reversePath.Count}");
        ClearCommittedPathVisual();
        animationManager.PlayMovement(
            selectedUnit,
            movementTilemap,
            reversePath,
            playStartSfx: false,
            onAnimationStart: null,
            onAnimationFinished: () =>
            {
                Debug.Log("[Rollback] Animacao de retorno concluida. Aplicando UnitSelected.");
                HandleMovementAnimationCompleted(CursorState.UnitSelected);
            },
            onCellReached: null);
        return true;
    }

    private void HandleMovementAnimationCompleted(CursorState onCompleteState)
    {
        LogStateStep($"HandleMovementAnimationCompleted(target={onCompleteState})", rollback: onCompleteState == CursorState.UnitSelected);
        if (selectedUnit != null)
            animationManager?.ApplySelectionVisual(selectedUnit);

        if (onCompleteState == CursorState.UnitSelected)
        {
            RestoreForcedLayerAfterRollbackIfNeeded();
            ClearCommittedMovement();
            if (cursorController != null && selectedUnit != null)
            {
                Vector3Int selectedUnitCell = selectedUnit.CurrentCellPosition;
                selectedUnitCell.z = 0;
                cursorController.SetCell(selectedUnitCell, playMoveSfx: false);
            }
        }
        else if (onCompleteState == CursorState.MoveuAndando)
        {
            PrepareFuelCostForCommittedPath();
            DrawCommittedPathVisual(committedMovementPath);
            Debug.Log($"moveu para {committedDestinationCell.x},{committedDestinationCell.y}");
            EnterSensorsState(CursorState.MoveuAndando);
            return;
        }
        else if (onCompleteState == CursorState.MoveuParado)
        {
            EnterSensorsState(CursorState.MoveuParado);
            return;
        }

        SetCursorState(onCompleteState, $"HandleMovementAnimationCompleted(target={onCompleteState})", rollback: onCompleteState == CursorState.UnitSelected);
        if (cursorState == CursorState.UnitSelected)
        {
            ClearSensorResults();
            PaintSelectedUnitMovementRange();
        }
        else
            ClearSensorResults();
    }

    private void TryApplyForcedEndMovementLayerBeforeSensors(CursorState movementState)
    {
        if (selectedUnit == null)
            return;
        if (movementState != CursorState.MoveuAndando && movementState != CursorState.MoveuParado)
            return;

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        if (boardMap == null)
            return;

        Vector3Int cell = selectedUnit.CurrentCellPosition;
        cell.z = 0;
        Domain currentDomain = selectedUnit.GetDomain();
        HeightLevel currentHeight = selectedUnit.GetHeightLevel();
        if (!TryResolveForcedEndMovementTargetForCell(
                boardMap,
                terrainDatabase,
                cell,
                currentDomain,
                currentHeight,
                out Domain forcedDomain,
                out HeightLevel forcedHeight,
                out string forcedReason))
        {
            TryApplyPreferredNavalLayerAfterMovement(boardMap, cell, currentDomain, currentHeight);
            return;
        }

        if (forcedDomain == currentDomain && forcedHeight == currentHeight)
            return;

        if (!selectedUnit.SupportsLayerMode(forcedDomain, forcedHeight))
        {
            Debug.Log($"[LayerForce] Ignorado: unidade nao suporta camada forcada {forcedDomain}/{forcedHeight}.");
            return;
        }

        if (!hasForcedLayerRollbackSnapshot)
        {
            hasForcedLayerRollbackSnapshot = true;
            forcedLayerRollbackDomain = currentDomain;
            forcedLayerRollbackHeight = currentHeight;
        }

        if (selectedUnit.TrySetCurrentLayerMode(forcedDomain, forcedHeight))
            Debug.Log($"[LayerForce] {forcedReason} | {currentDomain}/{currentHeight} -> {forcedDomain}/{forcedHeight}");
    }

    private void TryApplyPreferredNavalLayerAfterMovement(
        Tilemap boardMap,
        Vector3Int cell,
        Domain currentDomain,
        HeightLevel currentHeight)
    {
        if (selectedUnit == null)
            return;
        if (!selectedUnit.TryGetPreferredNavalLayerMode(out Domain preferredDomain, out HeightLevel preferredHeight))
            return;
        if (preferredDomain == currentDomain && preferredHeight == currentHeight)
            return;
        if (!selectedUnit.SupportsLayerMode(preferredDomain, preferredHeight))
            return;
        if (!CanUseLayerModeAtCellForLayerForce(selectedUnit, boardMap, terrainDatabase, cell, preferredDomain, preferredHeight))
            return;

        if (!hasForcedLayerRollbackSnapshot)
        {
            hasForcedLayerRollbackSnapshot = true;
            forcedLayerRollbackDomain = currentDomain;
            forcedLayerRollbackHeight = currentHeight;
        }

        if (selectedUnit.TrySetCurrentLayerMode(preferredDomain, preferredHeight))
        {
            Debug.Log($"[LayerForce] AutoNavalPreference | {currentDomain}/{currentHeight} -> {preferredDomain}/{preferredHeight}");
        }
    }

    private void RestoreForcedLayerAfterRollbackIfNeeded()
    {
        if (!hasForcedLayerRollbackSnapshot || selectedUnit == null)
            return;

        if (selectedUnit.TrySetCurrentLayerMode(forcedLayerRollbackDomain, forcedLayerRollbackHeight))
            Debug.Log($"[LayerForce] [roll back] restaurado para {forcedLayerRollbackDomain}/{forcedLayerRollbackHeight}");
        hasForcedLayerRollbackSnapshot = false;
    }

    private static bool TryResolveForcedEndMovementTargetForCell(
        Tilemap boardMap,
        TerrainDatabase terrainDb,
        Vector3Int cell,
        Domain currentDomain,
        HeightLevel currentHeight,
        out Domain targetDomain,
        out HeightLevel targetHeight,
        out string reason)
    {
        targetDomain = currentDomain;
        targetHeight = currentHeight;
        reason = string.Empty;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
        if (construction != null)
        {
            if (construction.TryResolveConstructionData(out ConstructionData constructionData) &&
                MatchesAnyLayerMode(constructionData != null ? constructionData.forceEndMovementOnTerrainDomainForDomains : null, currentDomain, currentHeight))
            {
                if (!TryResolveForcedEmergeLayerTarget(currentDomain, currentHeight, out targetDomain, out targetHeight))
                {
                    // Fallback de compatibilidade para contextos legados que ainda dependem do dominio nativo.
                    targetDomain = construction.GetDomain();
                    targetHeight = construction.GetHeightLevel();
                }
                reason = $"ForceEmerge (Construction={construction.name})";
                return true;
            }
            // Se a construcao existir mas nao forcar neste contexto, faz fallback
            // para Structure+Terrain e depois Terrain.
        }

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardMap, cell);
        bool hasTerrain = TryResolveTerrainAtCellForLayerForce(boardMap, terrainDb, cell, out TerrainTypeData terrain) && terrain != null;
        if (structure != null)
        {
            bool forceByStructure = MatchesAnyLayerMode(structure.forceEndMovementOnTerrainDomainForDomains, currentDomain, currentHeight);
            bool forceByTerrain = hasTerrain && MatchesAnyLayerMode(terrain.forceEndMovementOnTerrainDomainForDomains, currentDomain, currentHeight);
            if (!forceByStructure && !forceByTerrain)
                return false;

            if (!hasTerrain)
                return false;

            if (!TryResolveForcedEmergeLayerTarget(currentDomain, currentHeight, out targetDomain, out targetHeight))
            {
                // Fallback de compatibilidade para contextos legados que ainda dependem do dominio nativo.
                targetDomain = terrain.domain;
                targetHeight = terrain.heightLevel;
            }
            reason = $"ForceEmerge (Structure+Terrain={structure.displayName}+{terrain.displayName})";
            return true;
        }

        if (!hasTerrain)
            return false;

        if (!MatchesAnyLayerMode(terrain.forceEndMovementOnTerrainDomainForDomains, currentDomain, currentHeight))
            return false;

        if (!TryResolveForcedEmergeLayerTarget(currentDomain, currentHeight, out targetDomain, out targetHeight))
        {
            // Fallback de compatibilidade para contextos legados que ainda dependem do dominio nativo.
            targetDomain = terrain.domain;
            targetHeight = terrain.heightLevel;
        }
        reason = $"ForceEmerge (Terrain={terrain.displayName})";
        return true;
    }

    private static bool TryResolveForcedEmergeLayerTarget(
        Domain currentDomain,
        HeightLevel currentHeight,
        out Domain targetDomain,
        out HeightLevel targetHeight)
    {
        // "Forced to emerge": unidade submersa sobe para Naval/Surface.
        if (currentDomain == Domain.Submarine || currentHeight == HeightLevel.Submerged)
        {
            targetDomain = Domain.Naval;
            targetHeight = HeightLevel.Surface;
            return true;
        }

        targetDomain = currentDomain;
        targetHeight = currentHeight;
        return false;
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

    private static bool CanUseLayerModeAtCellForLayerForce(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDb,
        Vector3Int cell,
        Domain targetDomain,
        HeightLevel targetHeight)
    {
        if (unit == null || boardMap == null)
            return false;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardMap, cell);
        if (construction != null)
            return construction.SupportsLayerMode(targetDomain, targetHeight);

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(boardMap, cell);
        bool hasTerrain = TryResolveTerrainAtCellForLayerForce(boardMap, terrainDb, cell, out TerrainTypeData terrain) && terrain != null;
        if (structure != null)
        {
            if (!StructureSupportsLayerModeForLayerForce(structure, targetDomain, targetHeight))
                return false;
            if (!hasTerrain)
                return false;
            return TerrainSupportsLayerModeForLayerForce(terrain, targetDomain, targetHeight);
        }

        if (!hasTerrain)
            return false;

        return TerrainSupportsLayerModeForLayerForce(terrain, targetDomain, targetHeight);
    }

    private static bool TerrainSupportsLayerModeForLayerForce(TerrainTypeData terrain, Domain domain, HeightLevel heightLevel)
    {
        if (terrain == null)
            return false;
        if (terrain.domain == domain && terrain.heightLevel == heightLevel)
            return true;
        if (domain == Domain.Air && terrain.alwaysAllowAirDomain)
            return true;
        if (terrain.aditionalDomainsAllowed == null)
            return false;
        for (int i = 0; i < terrain.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = terrain.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool StructureSupportsLayerModeForLayerForce(StructureData structure, Domain domain, HeightLevel heightLevel)
    {
        if (structure == null)
            return false;
        if (structure.domain == domain && structure.heightLevel == heightLevel)
            return true;
        if (domain == Domain.Air && structure.alwaysAllowAirDomain)
            return true;
        if (structure.aditionalDomainsAllowed == null)
            return false;
        for (int i = 0; i < structure.aditionalDomainsAllowed.Count; i++)
        {
            TerrainLayerMode mode = structure.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private static bool TryResolveTerrainAtCellForLayerForce(
        Tilemap terrainTilemap,
        TerrainDatabase terrainDb,
        Vector3Int cell,
        out TerrainTypeData terrain)
    {
        terrain = null;
        if (terrainTilemap == null || terrainDb == null)
            return false;

        cell.z = 0;
        TileBase tile = terrainTilemap.GetTile(cell);
        if (tile != null && terrainDb.TryGetByPaletteTile(tile, out TerrainTypeData byMainTile) && byMainTile != null)
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

            if (terrainDb.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) && byGridTile != null)
            {
                terrain = byGridTile;
                return true;
            }
        }

        return false;
    }

    private void PlayMovementStartSfx(UnitManager unit)
    {
        if (unit == null || cursorController == null)
            return;

        cursorController.PlayUnitMovementSfx(unit.GetMovementCategory());
    }
}
