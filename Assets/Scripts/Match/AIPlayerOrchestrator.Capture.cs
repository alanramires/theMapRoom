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
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == unit || u.IsEmbarked) continue;
            Vector3Int p = u.CurrentCellPosition; p.z = 0;
            if (u.TeamId == myTeam) alliedCells.Add(p);
            else                    enemyCells.Add(p);
        }

        ConstructionManager bestFree          = null;
        float               bestFreeDist      = float.MaxValue;
        ConstructionManager bestContested     = null;
        float               bestContestedDist = float.MaxValue;
        ConstructionManager bestEnemyHq       = null;
        float               bestHqDist        = float.MaxValue;

        // Todos os prédios contestados por inimigos (para simulação de ataque)
        var allContested = new List<ConstructionManager>();

        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (!c.IsCapturable) continue;

            bool isEnemy   = c.TeamId != myTeam;
            bool isPartial = c.TeamId == myTeam && c.CurrentCapturePoints < c.CapturePointsMax;
            if (!isEnemy && !isPartial) continue;

            Vector3Int cCell = c.CurrentCellPosition; cCell.z = 0;
            float dist = HexWorldDistance(fromCell, cCell);

            if (alliedCells.Contains(cCell))
            {
                if (isEnemy && c.IsPlayerHeadQuarter && dist < bestHqDist)
                { bestHqDist = dist; bestEnemyHq = c; }
                continue;
            }

            if (enemyCells.Contains(cCell))
            {
                allContested.Add(c);
                if (dist < bestContestedDist) { bestContestedDist = dist; bestContested = c; }
                if (c.IsPlayerHeadQuarter && dist < bestHqDist) { bestHqDist = dist; bestEnemyHq = c; }
                continue;
            }

            if (dist < bestFreeDist) { bestFreeDist = dist; bestFree = c; }
        }

        if (bestContested != null)
        {
            Vector3Int cc = bestContested.CurrentCellPosition; cc.z = 0;
            contestedBuildingCell = cc;
        }

        // --- Caso 1: prédio contestado mais próximo que o livre ---
        // Excepção: HQ inimigo livre tem urgência máxima e nunca é sacrificado
        bool freeIsEnemyHq = bestFree != null && bestFree.IsPlayerHeadQuarter;
        if (!freeIsEnemyHq && bestContested != null && bestContestedDist < bestFreeDist)
        {
            Vector3Int cc = bestContested.CurrentCellPosition; cc.z = 0;

            // HQ contestado: avança em direção a ele
            if (bestContested.IsPlayerHeadQuarter)
            {
                Vector3Int hqStep = freeCells.Count > 0
                    ? freeCells.OrderBy(c => HexWorldDistance(c, cc)).First()
                    : fromCell;
                captureStepCell = hqStep;
                Debug.Log($"[AI] {unit.InstanceId} HQ inimigo contestado em {cc} → avança para {hqStep}");
                return BuildMoveBatch(unit, myTeam, moveFrom: fromCell, moveTo: hqStep);
            }

            // Prédio comum contestado mais próximo: simula ataque ao ocupante
            PlayerAction contestAttack = TryAttackContestedOccupants(unit, myTeam, fromCell, freeCells, allContested);
            if (contestAttack != null) return contestAttack;

            // Fora de alcance → avança em direção ao prédio contestado
            captureStepCell = freeCells.Count > 0
                ? freeCells.OrderBy(c => HexWorldDistance(c, cc)).First()
                : fromCell;
            Debug.Log($"[AI] {unit.InstanceId} prédio contestado em {cc} fora de alcance → avança para {captureStepCell}");
            return BuildMoveBatch(unit, myTeam, moveFrom: fromCell, moveTo: captureStepCell.Value);
        }

        // --- Caso 2: prédio livre encontrado → capturar ---
        if (bestFree != null)
        {
            Vector3Int targetCell = bestFree.CurrentCellPosition; targetCell.z = 0;

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

            // Prédio fora de alcance: antes de avançar, tenta expulsar ocupantes de prédios contestados
            if (allContested.Count > 0)
            {
                PlayerAction contestAttack = TryAttackContestedOccupants(unit, myTeam, fromCell, freeCells, allContested);
                if (contestAttack != null) return contestAttack;
            }

            Vector3Int step = freeCells.Count > 0
                ? freeCells.OrderBy(c => HexWorldDistance(c, targetCell)).First()
                : fromCell;
            captureStepCell = step;
            Debug.Log($"[AI] {unit.InstanceId} {fromCell} -> {step} (avança ao prédio {targetCell})");
            return BuildMoveBatch(unit, myTeam, moveFrom: fromCell, moveTo: step);
        }

        // --- Caso 3: todos os prédios têm inimigo → "cai fora que esse prédio é meu!" ---
        if (bestContested != null)
        {
            Vector3Int contestedCell = bestContested.CurrentCellPosition; contestedCell.z = 0;

            PlayerAction contestAttack = TryAttackContestedOccupants(unit, myTeam, fromCell, freeCells, allContested);
            if (contestAttack != null) return contestAttack;

            // Fora de alcance → avança em direção ao prédio mais próximo
            Vector3Int step = freeCells.Count > 0
                ? freeCells.OrderBy(c => HexWorldDistance(c, contestedCell)).First()
                : fromCell;
            Debug.Log($"[AI] {unit.InstanceId} ocupante de {contestedCell} fora de alcance → avança para {step}");
            return BuildMoveBatch(unit, myTeam, fromCell, step);
        }

        // --- Caso 4: sem prédios capturáveis → rush ao HQ inimigo ---
        if (bestEnemyHq == null)
        {
            foreach (ConstructionManager c in ConstructionManager.AllActive)
            {
                if (c.TeamId == myTeam || !c.IsPlayerHeadQuarter) continue;
                Vector3Int hqCell = c.CurrentCellPosition; hqCell.z = 0;
                float dist = HexWorldDistance(fromCell, hqCell);
                if (dist < bestHqDist) { bestHqDist = dist; bestEnemyHq = c; }
            }
        }

        if (bestEnemyHq != null)
        {
            Vector3Int hqTarget = bestEnemyHq.CurrentCellPosition; hqTarget.z = 0;
            Vector3Int hqStep = freeCells.Count > 0
                ? freeCells.OrderBy(c => HexWorldDistance(c, hqTarget)).First()
                : fromCell;
            captureStepCell = hqStep;
            Debug.Log($"[AI] {unit.InstanceId} {fromCell} -> {hqStep} (rush HQ inimigo {hqTarget})");
            return BuildMoveBatch(unit, myTeam, moveFrom: fromCell, moveTo: hqStep);
        }

        return null;
    }

    /// <summary>
    /// Itera todos os prédios contestados, encontra seus ocupantes e simula ataque
    /// de cada posição alcançável. Com prioritizeDpqAtBattle, escolhe a posição
    /// de maior DPQ entre todas as opções válidas; caso contrário, retorna a primeira.
    /// </summary>
    private PlayerAction TryAttackContestedOccupants(UnitManager unit, TeamId myTeam,
        Vector3Int fromCell, List<Vector3Int> freeCells, List<ConstructionManager> contested)
    {
        bool useDpq = unit.TryGetUnitData(out UnitData ud) && ud != null && ud.prioritizeDpqAtBattle;

        // Coleta todos os pares (destino, opção de ataque, DPQ) válidos
        var candidates = new List<(Vector3Int dest, PodeMirarTargetOption opt, int dpq)>();

        foreach (ConstructionManager c in contested)
        {
            Vector3Int cCell = c.CurrentCellPosition; cCell.z = 0;

            UnitManager occupant = null;
            foreach (UnitManager u in UnitManager.AllActive)
            {
                if (u.IsEmbarked || u.TeamId == myTeam) continue;
                Vector3Int p = u.CurrentCellPosition; p.z = 0;
                if (p == cCell) { occupant = u; break; }
            }
            if (occupant == null) continue;

            if (TryFindAttackTargeting(unit, fromCell, SensorMovementMode.MoveuParado, occupant, out PodeMirarTargetOption stayOpt))
            {
                int dpq = useDpq ? turnStateManager.GetCellDpqPoints(fromCell, unit) : 0;
                candidates.Add((fromCell, stayOpt, dpq));
            }

            foreach (Vector3Int dest in freeCells)
            {
                if (TryFindAttackTargeting(unit, dest, SensorMovementMode.MoveuAndando, occupant, out PodeMirarTargetOption moveOpt))
                {
                    int dpq = useDpq ? turnStateManager.GetCellDpqPoints(dest, unit) : 0;
                    candidates.Add((dest, moveOpt, dpq));
                }
            }
        }

        if (candidates.Count == 0) return null;

        // Seleciona: DPQ máximo se useDpq, caso contrário o primeiro encontrado
        var best = useDpq
            ? candidates.OrderByDescending(x => x.dpq).First()
            : candidates[0];

        Debug.Log($"[AI] {unit.InstanceId} ataca {best.opt.targetUnit.InstanceId} de {best.dest} (DPQ={best.dpq})");
        return BuildAttackBatch(unit, myTeam, fromCell, best.dest, best.opt);
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
