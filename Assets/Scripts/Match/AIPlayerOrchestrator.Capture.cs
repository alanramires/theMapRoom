using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Lógica de decisão de captura de construções para a IA.
/// Partial class de AIPlayerOrchestrator.
/// </summary>
public partial class AIPlayerOrchestrator
{
    /// <summary>
    /// Tenta decidir uma ação de captura para a unidade.
    /// Retorna null quando a captura não é possível.
    /// - contestedBuildingCell: prédio com inimigo mais próximo (para aproximação cautelosa por FOW)
    /// - captureStepCell: próximo passo em direção ao objetivo de captura (para retomar se Attack falhar)
    /// </summary>
    private PlayerAction TryDecideCapture(UnitManager unit, TeamId myTeam,
        Vector3Int fromCell, List<Vector3Int> freeCells,
        out Vector3Int? contestedBuildingCell,
        out Vector3Int? captureStepCell)
    {
        contestedBuildingCell = null;
        captureStepCell = null;

        var alliedCells = new HashSet<Vector3Int>();
        var enemyCells  = new HashSet<Vector3Int>();
        foreach (var u in UnitManager.AllActive)
        {
            if (u == unit || u.IsEmbarked) continue;
            Vector3Int p = u.CurrentCellPosition;
            p.z = 0;
            if (u.TeamId == myTeam) alliedCells.Add(p);
            else                    enemyCells.Add(p);
        }

        ConstructionManager bestFree          = null;
        float               bestFreeDist      = float.MaxValue;
        ConstructionManager bestContested     = null;
        float               bestContestedDist = float.MaxValue;
        ConstructionManager bestEnemyHq       = null;
        float               bestHqDist        = float.MaxValue;

        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (!c.IsCapturable) continue;

            bool isEnemy   = c.TeamId != myTeam;
            bool isPartial = c.TeamId == myTeam && c.CurrentCapturePoints < c.CapturePointsMax;
            if (!isEnemy && !isPartial) continue;

            Vector3Int cCell = c.CurrentCellPosition;
            cCell.z = 0;
            float dist = HexWorldDistance(fromCell, cCell);

            if (alliedCells.Contains(cCell))
            {
                if (isEnemy && c.IsPlayerHeadQuarter && dist < bestHqDist)
                {
                    bestHqDist = dist;
                    bestEnemyHq = c;
                }
                continue;
            }

            if (enemyCells.Contains(cCell))
            {
                if (dist < bestContestedDist)
                {
                    bestContestedDist = dist;
                    bestContested = c;
                }
                if (c.IsPlayerHeadQuarter && dist < bestHqDist)
                {
                    bestHqDist = dist;
                    bestEnemyHq = c;
                }
                continue;
            }

            if (dist < bestFreeDist)
            {
                bestFreeDist = dist;
                bestFree = c;
            }
        }

        if (bestContested != null)
        {
            Vector3Int cc = bestContested.CurrentCellPosition;
            cc.z = 0;
            contestedBuildingCell = cc;
        }

        // --- Caso 1: prédio com inimigo é mais próximo que o livre → expulsar primeiro ---
        if (bestContested != null && bestContestedDist < bestFreeDist)
        {
            Debug.Log($"[AI] {unit.InstanceId} prédio contestado ({bestContestedDist:F1}) mais perto que livre ({bestFreeDist:F1}) → exit para Attack");
            return null;
        }

        // --- Caso 2: prédio livre encontrado → capturar ---
        if (bestFree != null)
        {
            Vector3Int targetCell = bestFree.CurrentCellPosition;
            targetCell.z = 0;

            if (targetCell == fromCell)
            {
                Debug.Log($"[AI] {unit.InstanceId} captura parado em {targetCell}");
                return BuildCaptureBatch(unit, myTeam, moveFrom: fromCell, moveTo: fromCell);
            }

            if (freeCells.Contains(targetCell))
            {
                Debug.Log($"[AI] {unit.InstanceId} {fromCell} -> {targetCell} (captura)");
                return BuildCaptureBatch(unit, myTeam, moveFrom: fromCell, moveTo: targetCell);
            }

            // Prédio fora de alcance — calcula step e expõe como fallback
            Vector3Int step = freeCells.Count > 0
                ? freeCells.OrderBy(c => HexWorldDistance(c, targetCell)).First()
                : fromCell;
            captureStepCell = step;

            // Cede para Attack se houver inimigo visível em qualquer célula alcançável
            if (TryFindAttack(unit, fromCell, SensorMovementMode.MoveuParado, out _) ||
                freeCells.Any(c => TryFindAttack(unit, c, SensorMovementMode.MoveuAndando, out _)))
            {
                Debug.Log($"[AI] {unit.InstanceId} tem ataque disponível no caminho → exit Capture para Attack");
                return null;
            }

            Debug.Log($"[AI] {unit.InstanceId} {fromCell} -> {step} (avança ao prédio {targetCell})");
            return BuildMoveBatch(unit, myTeam, moveFrom: fromCell, moveTo: step);
        }

        // --- Caso 3: todos os prédios têm inimigo → exit para Attack ---
        if (bestContested != null)
        {
            Debug.Log($"[AI] {unit.InstanceId} todos prédios contestados → exit Capture para Attack");
            return null;
        }

        // --- Caso 4: sem prédios capturáveis → rush ao HQ inimigo ---
        if (bestEnemyHq == null)
        {
            foreach (ConstructionManager c in ConstructionManager.AllActive)
            {
                if (c.TeamId == myTeam || !c.IsPlayerHeadQuarter) continue;
                Vector3Int hqCell = c.CurrentCellPosition;
                hqCell.z = 0;
                float dist = HexWorldDistance(fromCell, hqCell);
                if (dist < bestHqDist) { bestHqDist = dist; bestEnemyHq = c; }
            }
        }

        if (bestEnemyHq != null)
        {
            Vector3Int hqTarget = bestEnemyHq.CurrentCellPosition;
            hqTarget.z = 0;
            Vector3Int hqStep = freeCells.Count > 0
                ? freeCells.OrderBy(c => HexWorldDistance(c, hqTarget)).First()
                : fromCell;
            captureStepCell = hqStep;

            if (TryFindAttack(unit, fromCell, SensorMovementMode.MoveuParado, out _) ||
                freeCells.Any(c => TryFindAttack(unit, c, SensorMovementMode.MoveuAndando, out _)))
            {
                Debug.Log($"[AI] {unit.InstanceId} tem ataque disponível no caminho ao HQ → exit Capture para Attack");
                return null;
            }

            Debug.Log($"[AI] {unit.InstanceId} {fromCell} -> {hqStep} (rush HQ inimigo {hqTarget})");
            return BuildMoveBatch(unit, myTeam, moveFrom: fromCell, moveTo: hqStep);
        }

        return null;
    }

    private PlayerAction BuildCaptureBatch(UnitManager unit, TeamId myTeam,
        Vector3Int moveFrom, Vector3Int moveTo)
    {
        return new PlayerAction
        {
            IsAIGenerated = true,
            ActionType = PlayerActionType.UnitAction,
            ActingTeam = myTeam,
            TurnNumber = matchController.CurrentTurn,
            CursorHex = moveFrom,
            HasCursorHex = true,
            UnitInstanceId = unit.InstanceId.ToString(),
            MoveFrom = moveFrom,
            MoveTo = moveTo,
            HasMoveFrom = true,
            HasMoveTo = true,
            SensorAction = SensorActionType.Capture,
        };
    }
}
