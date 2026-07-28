using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private AIBacklineSettings BuildBacklineSettings()
    {
        AIBacklineSettings settings = AIBacklineSettings.Default;
        if (boardTilemap != null)
            settings.CellToWorld = c => boardTilemap.GetCellCenterWorld(c);
        return settings;
    }

    private bool TryAnalyzeBackline(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int anchor,
        out AIBacklineResult result)
    {
        result = null;
        if (!TryBuildBacklineContext(unit, snapshot, out List<Vector3Int> combatants, out List<Vector3Int> enemies))
            return false;

        anchor = ResolveBacklineAnchor(enemies, anchor);
        result = AIBacklineAnalyzer.Analyze(combatants, enemies, anchor, BuildBacklineSettings());
        return result != null && result.Success;
    }

    private bool TryScoreBacklineCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int anchor,
        out AIBacklineScore score)
    {
        score = default;
        if (!TryBuildBacklineContext(unit, snapshot, out List<Vector3Int> combatants, out List<Vector3Int> enemies))
            return false;

        anchor = ResolveBacklineAnchor(enemies, anchor);
        score = AIBacklineAnalyzer.ScoreCell(combatants, enemies, cell, anchor, BuildBacklineSettings());
        return true;
    }

    /// <summary>
    /// Fallback de formacao para Play Conservative. A unidade nao inventa
    /// um objetivo proprio: acompanha a faixa de combatentes aliados dois
    /// hexes atras da frente e para quando ja esta bem posicionada.
    /// Operacoes reais (combate, reparo, transporte e servico) sao resolvidas
    /// antes pelos chamadores.
    /// </summary>
    private PlayerAction TryBuildConservativeRearFollowAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Dictionary<Vector3Int, List<Vector3Int>> paths = null,
        string context = "fallback")
    {
        if (unit == null
            || snapshot == null
            || !unit.TryGetUnitData(out UnitData data)
            || data == null
            || !data.playConservative)
        {
            return null;
        }

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        paths ??= UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap,
            unit,
            Mathf.Max(0, unit.RemainingMovementPoints),
            terrainDatabase);
        if (paths == null || paths.Count == 0)
            return null;

        if (!TryBuildBacklineContext(
                unit,
                snapshot,
                out List<Vector3Int> combatants,
                out List<Vector3Int> enemies))
        {
            return null;
        }

        Vector3Int anchor;
        if (snapshot.EnemyHQ != null)
        {
            anchor = snapshot.EnemyHQ.CurrentCellPosition;
            anchor.z = 0;
        }
        else if (enemies.Count > 0)
        {
            anchor = ResolveBacklineAnchor(enemies, fromCell);
        }
        else
        {
            // Sem qualquer direcao conhecida de frente, "retaguarda" seria
            // arbitraria. Preserve o fallback anterior do papel.
            return null;
        }

        anchor = ResolveBacklineAnchor(enemies, anchor);
        AIBacklineSettings settings = BuildBacklineSettings();
        AIBacklineResult geometry = AIBacklineAnalyzer.Analyze(
            combatants,
            enemies,
            anchor,
            settings);
        if (geometry == null || !geometry.Success)
            return null;

        AIBacklineScore fromRear = AIBacklineAnalyzer.ScoreCell(
            combatants,
            enemies,
            fromCell,
            anchor,
            settings,
            geometry);
        float fromScore = ScoreConservativeRearFollowCell(
            unit,
            snapshot,
            fromCell,
            fromCell,
            fromRear,
            pathCost: 0);
        Vector3Int bestCell = fromCell;
        AIBacklineScore bestRear = fromRear;
        float bestScore = fromScore;
        int bestPathCost = 0;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;

            List<UnitManager> occupants =
                UnitOccupancyRules.GetUnitsAtCell(
                    boardTilemap, cell, unit);
            if (!CanAIUnitEndMoveAtCell(
                    unit, cell, occupants))
            {
                continue;
            }

            if (data.aiConservativeSupplyAvoidEnemyRange > 0
                && HasNearbyVisibleEnemy(
                    cell,
                    snapshot.AITeam,
                    data.aiConservativeSupplyAvoidEnemyRange))
            {
                continue;
            }

            AIBacklineScore rear = AIBacklineAnalyzer.ScoreCell(
                combatants,
                enemies,
                cell,
                anchor,
                settings,
                geometry);
            if (rear.IsVanguard)
                continue;

            int pathCost = GetPathStepCount(paths, cell);
            float score = ScoreConservativeRearFollowCell(
                unit,
                snapshot,
                cell,
                fromCell,
                rear,
                pathCost);
            if (score <= bestScore)
                continue;

            bestCell = cell;
            bestRear = rear;
            bestScore = score;
            bestPathCost = pathCost;
        }

        // Uma margem pequena evita tremedeira entre hexes equivalentes. Se a
        // unidade esta na vanguarda, qualquer melhora real para tras e aceita.
        float moveMargin = fromRear.IsVanguard ? 1f : 20f;
        if (bestCell == fromCell
            || bestScore < fromScore + moveMargin)
        {
            Debug.Log(
                $"{TL("Conservative")} {unit.InstanceId} acompanha a " +
                $"retaguarda parado em {fromCell} context={context} " +
                $"score={fromScore:F0} depth={fromRear.Depth:F1} " +
                $"frontAlly={fromRear.NearestFrontAlly:F1}.");
            return BuildMoveBatch(
                unit,
                snapshot.AITeam,
                fromCell,
                fromCell,
                paths);
        }

        Debug.Log(
            $"{TL("Conservative")} {unit.InstanceId} segue a retaguarda " +
            $"{fromCell}->{bestCell} context={context} " +
            $"score={fromScore:F0}->{bestScore:F0} " +
            $"depth={bestRear.Depth:F1} " +
            $"frontAlly={bestRear.NearestFrontAlly:F1} " +
            $"path={bestPathCost}.");
        return BuildMoveBatch(
            unit,
            snapshot.AITeam,
            fromCell,
            bestCell,
            paths);
    }

    private float ScoreConservativeRearFollowCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int fromCell,
        AIBacklineScore rear,
        int pathCost)
    {
        float threat = CalculateThreatLevel(
            cell, snapshot.AITeam);
        float cohesion =
            CalculateFireSupportCohesionScore(
                unit, snapshot, cell);
        float score = rear.Score
            + cohesion * 0.15f
            - Mathf.Abs(rear.NearestFrontAlly - 2f) * 55f
            - threat * 140f
            - Mathf.Max(0, pathCost) * 5f;

        // A retaguarda desejada e uma faixa, nao uma corrida de um extremo ao
        // outro a cada rodada. Ficar parado ganha um pequeno amortecedor.
        if (cell == fromCell)
            score += 12f;
        return score;
    }

    private static Vector3Int ResolveBacklineAnchor(
        IReadOnlyList<Vector3Int> enemies,
        Vector3Int fallback)
    {
        fallback.z = 0;
        if (enemies == null || enemies.Count == 0)
            return fallback;

        Vector3 sum = Vector3.zero;
        foreach (Vector3Int raw in enemies)
        {
            Vector3Int cell = raw;
            cell.z = 0;
            sum += new Vector3(cell.x, cell.y, 0f);
        }

        Vector3 average = sum / enemies.Count;
        return new Vector3Int(
            Mathf.RoundToInt(average.x),
            Mathf.RoundToInt(average.y),
            0);
    }

    private bool TryBuildBacklineContext(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        out List<Vector3Int> combatants,
        out List<Vector3Int> enemies)
    {
        combatants = new List<Vector3Int>();
        enemies = new List<Vector3Int>();

        if (snapshot == null || snapshot.MyUnits == null)
            return false;

        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;
            if (!IsFrontlineBacklineAnchorUnit(ally))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            combatants.Add(allyCell);
        }

        if (combatants.Count == 0)
            return false;

        if (snapshot.EnemyUnits != null)
        {
            foreach (UnitManager enemy in snapshot.EnemyUnits)
            {
                if (enemy == null || enemy.IsDead || enemy.IsEmbarked)
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition;
                enemyCell.z = 0;
                enemies.Add(enemyCell);
            }
        }

        return true;
    }

    private static bool IsFrontlineBacklineAnchorUnit(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.roles == null)
            return false;

        UnitRole role = UnitRoleCompatibility.ResolveCompositionRole(data);
        return role == UnitRole.Capturador || role == UnitRole.Assalto;
    }

    // A célula está na RETAGUARDA SEGURA (atrás da linha de combate, não na vanguarda / raio do HQ
    // inimigo)? Usa a ferramenta de retaguarda com o HQ inimigo como referência de "frente". Sem
    // linha de combatentes ou sem HQ inimigo conhecido, não restringe (retorna true) — o chamador
    // mantém o comportamento anterior. Complementa decisões como a fusão de unidades em reparo.
    private bool IsCellInSafeRear(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int cell)
    {
        if (snapshot == null)
            return true;

        bool hasKnownEnemy = false;
        if (snapshot.EnemyUnits != null)
        {
            foreach (UnitManager enemy in snapshot.EnemyUnits)
            {
                if (enemy == null || enemy.IsDead || enemy.IsEmbarked)
                    continue;
                hasKnownEnemy = true;
                break;
            }
        }
        if (!hasKnownEnemy && snapshot.EnemyHQ == null)
            return true;

        Vector3Int anchor = snapshot.EnemyHQ != null
            ? snapshot.EnemyHQ.CurrentCellPosition
            : cell;
        anchor.z = 0;
        if (!TryScoreBacklineCell(unit, snapshot, cell, anchor, out AIBacklineScore score))
            return true;

        return score.InRearSlice && !score.IsVanguard;
    }
}
