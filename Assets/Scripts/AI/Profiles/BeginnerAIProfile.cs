using UnityEngine;

// Perfil de IA iniciante (IA Burra).
// Avalia a postura lendo do BattleStanceDatabase: Invasion -> Defend -> Attack (fallback).
// Sem memoria entre partidas.
public class BeginnerAIProfile : AIProfile
{
    public override AIStance EvaluateStance(AISnapshot snapshot, BattleStanceDatabase stanceDatabase)
    {
        // Invasion: IA controla mais de X% das construcoes capturaveis.
        if (stanceDatabase != null && stanceDatabase.invasionStance != null)
        {
            BattleStanceData inv = stanceDatabase.invasionStance;
            if (inv.activationType == PlanConditionType.TotalConstructionsControlledAbovePercent)
            {
                int total = 0, controlled = 0;
                for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
                {
                    AIConstructionInfo info = snapshot.KnownConstructions[i];
                    if (info == null || !info.IsCapturable) continue;
                    total++;
                    if (info.TeamId == snapshot.AiTeam && info.CapturePoints >= info.CapturePointsMax)
                        controlled++;
                }
                if (total > 0 && (controlled / (float)total) * 100f > inv.threshold)
                    return AIStance.Invasion;
            }
        }

        // Defend: inimigo visivel dentro do raio do HQ (activationType == EnemyUnitsNearHQ).
        BattleStanceData defend = stanceDatabase?.defendStance;
        if (snapshot.HasHq && snapshot.BoardTilemap != null
            && (defend == null || defend.activationType == PlanConditionType.EnemyUnitsNearHQ))
        {
            int defendRadius = defend != null ? defend.hqEngagementRadius : snapshot.HqDefendRadius;

            if (defendRadius > 0)
            {
                Vector3Int hqCell = snapshot.HqCell;
                hqCell.z = 0;
                for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
                {
                    UnitManager enemy = snapshot.VisibleEnemies[i];
                    if (enemy == null) continue;
                    Vector3Int enemyCell = enemy.CurrentCellPosition;
                    enemyCell.z = 0;
                    if (HexCoordinates.IsWithinRange(snapshot.BoardTilemap, hqCell, enemyCell, defendRadius))
                        return AIStance.Defend;
                }
            }
        }

        return AIStance.Attack;
    }
}
