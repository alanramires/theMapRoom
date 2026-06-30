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

        score = AIBacklineAnalyzer.ScoreCell(combatants, enemies, cell, anchor, BuildBacklineSettings());
        return true;
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
        if (snapshot == null || snapshot.EnemyHQ == null)
            return true;

        Vector3Int anchor = snapshot.EnemyHQ.CurrentCellPosition;
        anchor.z = 0;
        if (!TryScoreBacklineCell(unit, snapshot, cell, anchor, out AIBacklineScore score))
            return true;

        return score.InRearSlice && !score.IsVanguard;
    }
}
