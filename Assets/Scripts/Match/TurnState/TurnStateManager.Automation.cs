using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    public bool HandleAutomatedSensorActionRequested(SensorActionType action)
    {
        switch (action)
        {
            case SensorActionType.None:
                return HandleAutomatedMoveOnlyActionRequested();
            case SensorActionType.Attack:
                HandleAimActionRequested();
                return true;
            case SensorActionType.Embark:
                HandleEmbarkActionRequested();
                return true;
            case SensorActionType.Disembark:
                HandleDisembarkActionRequested();
                return true;
            case SensorActionType.Capture:
                HandleCaptureActionRequested();
                return true;
            case SensorActionType.Merge:
                HandleMergeActionRequested();
                return true;
            case SensorActionType.Supply:
                HandleSupplyActionRequested();
                return true;
            case SensorActionType.Transfer:
                HandleTransferActionRequested();
                return true;
            case SensorActionType.Land:
                HandleLandingSensorRequested();
                return true;
            case SensorActionType.CommandService:
                return HandleAutomatedCommandServiceRequested();
            case SensorActionType.RemoveUnit:
                return HandleAutomatedRemoveUnitRequested();
            case SensorActionType.Shopping:
                return false;
            default:
                return false;
        }
    }

    public bool HandleAutomatedMoveOnlyActionRequested()
    {
        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
            return false;

        HandleMoveOnlyActionRequested();
        return cursorState == CursorState.Neutral;
    }

    public bool TryAutomatedSelectUnitAndEnterMoveuParado(UnitManager unit)
    {
        if (unit == null || cursorController == null)
            return false;
        if (cursorState != CursorState.Neutral)
            return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        cursorController.SetCell(unitCell, playMoveSfx: false);

        // Confirm #1: seleciona unidade aliada.
        HandleConfirm();
        if (selectedUnit != unit)
            return false;

        // Confirm #2 no mesmo hex: entra em MoveuParado (sensores habilitados).
        cursorController.SetCell(unitCell, playMoveSfx: false);
        HandleConfirm();
        return cursorState == CursorState.MoveuParado || cursorState == CursorState.MoveuAndando;
    }

    public bool TryExecuteAutomatedAttackFirstTarget()
    {
        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
            return false;
        if (!HandleAutomatedSensorActionRequested(SensorActionType.Attack))
            return false;

        if (cachedPodeMirarTargets == null || cachedPodeMirarTargets.Count <= 0)
        {
            HandleCancel();
            return false;
        }

        for (int i = 0; i < cachedPodeMirarTargets.Count; i++)
        {
            PodeMirarTargetOption option = cachedPodeMirarTargets[i];
            if (option == null || option.targetUnit == null)
                continue;

            UnitManager target = option.targetUnit;
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            if (TryExecuteAutomatedAttackReplayTarget(target.InstanceId.ToString(), targetCell))
                return true;
        }

        HandleCancel();
        return false;
    }

    public bool HasAutomatedAttackAvailable()
    {
        return availableSensorActionCodes != null && availableSensorActionCodes.Contains('A');
    }

    public bool HasAutomatedMoveAvailable()
    {
        return cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado;
    }

    public IEnumerator WaitUntilAutomatedNeutralReady(float timeoutSeconds)
    {
        float endTime = Time.time + Mathf.Max(0.2f, timeoutSeconds);
        while (Time.time < endTime)
        {
            if (cursorState == CursorState.Neutral && !IsScannerActionExecutionInProgress && !IsMovementAnimationRunning())
                yield break;

            yield return null;
        }
    }

    public IEnumerator MoveCursorToCellWithAutomatedTravel(Vector3Int targetCell, float stepDelay = -1f)
    {
        float resolvedStepDelay = stepDelay >= 0f
            ? stepDelay
            : (replayManager != null
                ? Mathf.Max(0f, replayManager.GetEffectiveCursorTravelStepDelayForRuntimeMotion())
                : 0.08f);

        yield return MoveCursorToCellLikeReplayAtTurnStart(targetCell, resolvedStepDelay);
    }

    // Retorna os pontos de DPQ da celula para a unidade (terreno/estrutura/construcao).
    // Default = 1. Usado pela IA para preferir terreno defensivo.
    public int GetCellDpqPoints(Vector3Int cell, UnitManager unit)
    {
        if (unit == null)
            return 1;

        Tilemap board = terrainTilemap != null ? terrainTilemap : unit.BoardTilemap;
        if (board == null)
            return 1;

        cell.z = 0;
        Domain domain = unit.GetDomain();
        HeightLevel height = unit.GetHeightLevel();

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(board, cell);
        if (construction != null && TryGetConstructionDpq(construction, out DPQData constructionDpq) && constructionDpq != null)
            return constructionDpq.Pontos;

        StructureData structure = StructureOccupancyRules.GetStructureAtCell(board, cell);
        if (structure != null && structure.dpqData != null)
            return structure.dpqData.Pontos;

        if (TryResolveTerrainAtCellForLayer(board, terrainDatabase, cell, domain, height, out TerrainTypeData terrain)
            && terrain != null && terrain.dpqData != null)
            return terrain.dpqData.Pontos;

        return 1;
    }

    // Equivalente ao ExecuteReplayConfirmInput do ReplayManager: confirma e toca o SFX correspondente.
    public void HandleConfirmWithFeedback()
    {
        ActionSfx sfx = HandleConfirm();
        if (cursorController == null)
            return;
        switch (sfx)
        {
            case ActionSfx.Confirm: cursorController.PlayConfirmSfx(); break;
            case ActionSfx.Cancel: cursorController.PlayCancelSfx(); break;
            case ActionSfx.Error:  cursorController.PlayErrorSfx();  break;
        }
    }

    // Retorna a melhor celula alcancavel em direcao ao targetCell.
    // Score = proximidade ao alvo + bonus de DPQ (quando prioritizeDpq=true e unidade fornecida).
    // Celulas em occupiedByAllies sao ignoradas.
    public bool TryGetBestReachableCellTowards(
        Vector3Int targetCell,
        HashSet<Vector3Int> occupiedByAllies,
        out Vector3Int bestCell,
        bool prioritizeDpq = false,
        UnitManager unit = null)
    {
        bestCell = default;
        if (movementPathsByCell == null || movementPathsByCell.Count == 0)
            return false;

        targetCell.z = 0;
        Tilemap reference = terrainTilemap;
        Vector3 targetWorld = reference != null
            ? reference.GetCellCenterWorld(targetCell)
            : new Vector3(targetCell.x, targetCell.y, 0f);

        // Descobre o alcance maximo de distancia para normalizar
        float maxDistSq = 0f;
        foreach (var kvp in movementPathsByCell)
        {
            Vector3Int c = kvp.Key; c.z = 0;
            if (occupiedByAllies != null && occupiedByAllies.Contains(c)) continue;
            Vector3 w = reference != null ? reference.GetCellCenterWorld(c) : new Vector3(c.x, c.y, 0f);
            float d = (w - targetWorld).sqrMagnitude;
            if (d > maxDistSq) maxDistSq = d;
        }
        if (maxDistSq <= 0f) maxDistSq = 1f;

        float bestScore = float.MinValue;
        bool found = false;
        foreach (var kvp in movementPathsByCell)
        {
            Vector3Int cell = kvp.Key;
            cell.z = 0;
            if (occupiedByAllies != null && occupiedByAllies.Contains(cell))
                continue;

            Vector3 cellWorld = reference != null
                ? reference.GetCellCenterWorld(cell)
                : new Vector3(cell.x, cell.y, 0f);

            // Score de proximidade: 1 = no alvo, 0 = mais longe possivel
            float proximityScore = 1f - (cellWorld - targetWorld).sqrMagnitude / maxDistSq;

            // Score de DPQ: normalizado 0-1 (pontos vao de 0 a 4)
            float dpqScore = 0f;
            if (prioritizeDpq && unit != null)
                dpqScore = Mathf.Clamp01(GetCellDpqPoints(cell, unit) / 4f);

            // DPQ tem peso de 40% quando priorizando, para nao dominar completamente a decisao
            float score = prioritizeDpq
                ? proximityScore * 0.6f + dpqScore * 0.4f
                : proximityScore;

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = cell;
                found = true;
            }
        }
        return found;
    }

    // Versao alternativa para IA: escolhe a celula alcancavel com menor distancia hex ao alvo.
    // Evita desvios laterais causados por distancia euclidiana em mapas hex.
    public bool TryGetBestReachableCellTowardsHexDistance(
        Tilemap boardTilemap,
        Vector3Int targetCell,
        HashSet<Vector3Int> occupiedByAllies,
        out Vector3Int bestCell,
        bool prioritizeDpq = false,
        UnitManager unit = null)
    {
        bestCell = default;
        if (movementPathsByCell == null || movementPathsByCell.Count == 0)
            return false;
        if (boardTilemap == null)
            return TryGetBestReachableCellTowards(targetCell, occupiedByAllies, out bestCell, prioritizeDpq, unit);

        targetCell.z = 0;
        int bestHexDistance = int.MaxValue;
        int bestDpqBand = int.MinValue;
        int bestDpqPoints = int.MinValue;
        bool found = false;

        foreach (var kvp in movementPathsByCell)
        {
            Vector3Int cell = kvp.Key;
            cell.z = 0;
            if (occupiedByAllies != null && occupiedByAllies.Contains(cell))
                continue;

            int dist = GetHexDistance(boardTilemap, cell, targetCell, 64);
            if (dist == int.MaxValue)
                continue;

            int dpqPoints = 1;
            int dpqBand = 0;
            if (prioritizeDpq && unit != null)
            {
                dpqPoints = GetCellDpqPoints(cell, unit);
                dpqBand = ResolveDpqBand(dpqPoints);
            }

            bool better = dist < bestHexDistance
                || (dist == bestHexDistance && dpqBand > bestDpqBand)
                || (dist == bestHexDistance && dpqBand == bestDpqBand && dpqPoints > bestDpqPoints);
            if (!better)
                continue;

            bestHexDistance = dist;
            bestDpqBand = dpqBand;
            bestDpqPoints = dpqPoints;
            bestCell = cell;
            found = true;
        }

        return found;
    }

    // Retorna a melhor celula alcancavel cuja distancia hex ao alvo esteja entre [minHexDistance, maxHexDistance].
    // Quando preferMaxDistance=true, prefere ficar o mais longe possivel dentro da banda (util para artilharia de alcance).
    public bool TryGetBestReachableCellAtHexDistanceBand(
        Tilemap boardTilemap,
        Vector3Int targetCell,
        int minHexDistance,
        int maxHexDistance,
        HashSet<Vector3Int> occupiedByAllies,
        out Vector3Int bestCell,
        bool prioritizeDpq = false,
        UnitManager unit = null,
        bool preferMaxDistance = true)
    {
        bestCell = default;
        if (movementPathsByCell == null || movementPathsByCell.Count == 0)
            return false;
        if (boardTilemap == null)
            return false;

        targetCell.z = 0;
        int minDist = Mathf.Max(0, minHexDistance);
        int maxDist = Mathf.Max(minDist, maxHexDistance);

        int bestHexDistance = preferMaxDistance ? int.MinValue : int.MaxValue;
        int bestDpqBand = int.MinValue;
        int bestDpqPoints = int.MinValue;
        bool found = false;

        foreach (var kvp in movementPathsByCell)
        {
            Vector3Int cell = kvp.Key;
            cell.z = 0;
            if (occupiedByAllies != null && occupiedByAllies.Contains(cell))
                continue;

            int dist = GetHexDistance(boardTilemap, cell, targetCell, 64);
            if (dist == int.MaxValue || dist < minDist || dist > maxDist)
                continue;

            int dpqPoints = 1;
            int dpqBand = 0;
            if (prioritizeDpq && unit != null)
            {
                dpqPoints = GetCellDpqPoints(cell, unit);
                dpqBand = ResolveDpqBand(dpqPoints);
            }

            bool better = false;
            if (!found)
            {
                better = true;
            }
            else if (preferMaxDistance)
            {
                better = dist > bestHexDistance
                    || (dist == bestHexDistance && dpqBand > bestDpqBand)
                    || (dist == bestHexDistance && dpqBand == bestDpqBand && dpqPoints > bestDpqPoints);
            }
            else
            {
                better = dist < bestHexDistance
                    || (dist == bestHexDistance && dpqBand > bestDpqBand)
                    || (dist == bestHexDistance && dpqBand == bestDpqBand && dpqPoints > bestDpqPoints);
            }

            if (!better)
                continue;

            bestHexDistance = dist;
            bestDpqBand = dpqBand;
            bestDpqPoints = dpqPoints;
            bestCell = cell;
            found = true;
        }

        return found;
    }

    // Faixas de prioridade pedidas para engajamento:
    // favoravel > melhorado > padrao > desfavoravel.
    private static int ResolveDpqBand(int dpqPoints)
    {
        if (dpqPoints >= 3) return 3; // favoravel (ex.: montanha)
        if (dpqPoints == 2) return 2; // melhorado
        if (dpqPoints == 1) return 1; // padrao
        return 0;                     // desfavoravel
    }

    private static int GetHexDistance(Tilemap tilemap, Vector3Int from, Vector3Int to, int maxSteps)
    {
        if (tilemap == null)
            return int.MaxValue;

        from.z = 0;
        to.z = 0;
        if (from == to)
            return 0;

        HashSet<Vector3Int> visited = new HashSet<Vector3Int> { from };
        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        Queue<int> depth = new Queue<int>();
        frontier.Enqueue(from);
        depth.Enqueue(0);

        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        int safeMax = Mathf.Clamp(maxSteps, 1, 256);

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            int d = depth.Dequeue();
            if (d >= safeMax)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int n = neighbors[i];
                n.z = 0;
                if (visited.Contains(n))
                    continue;

                if (n == to)
                    return d + 1;

                visited.Add(n);
                frontier.Enqueue(n);
                depth.Enqueue(d + 1);
            }
        }

        return int.MaxValue;
    }

    // Aguarda a animacao de movimento concluir e o estado ficar em MoveuAndando ou MoveuParado.
    public IEnumerator WaitUntilMovementAnimationDone(float timeoutSeconds)
    {
        float endTime = Time.time + Mathf.Max(0.5f, timeoutSeconds);
        while (Time.time < endTime)
        {
            if (!IsMovementAnimationRunning() &&
                (cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado))
                yield break;
            yield return null;
        }
    }

    public float GetAutomatedPreSelectDelay()
    {
        return animationManager != null ? animationManager.TurnStartFuelDeathCursorFocusDelay : 0.20f;
    }

    public float GetAutomatedBetweenUnitsDelay()
    {
        return animationManager != null ? animationManager.TurnStartFuelDeathBetweenKillsDelay : 0.15f;
    }

    public bool HandleAutomatedCommandServiceRequested()
    {
        if (cursorState != CursorState.Neutral)
            return false;

        TryCloseThreatLayerHotzone();
        if (!TryPreviewCommandServiceOrder(out _, emitLogs: false))
            return false;

        SetCursorState(CursorState.CommandService, "HandleAutomatedCommandServiceRequested");
        return true;
    }

    public bool HandleAutomatedRemoveUnitRequested()
    {
        if (cursorState != CursorState.Neutral)
            return false;

        if (!TryGetUnitUnderCursorForDebug(out UnitManager target, out Vector3Int cursorCell, out _))
            return false;

        string targetName = ResolveDebugUnitName(target);
        PanelDialogController.TrySetExternalText($"Destroy Unit :: {targetName} {FormatMapCellWithZ(cursorCell)} :: Confirm");
        SetCursorState(CursorState.RemovingUnit, "HandleAutomatedRemoveUnitRequested");
        return true;
    }
}
