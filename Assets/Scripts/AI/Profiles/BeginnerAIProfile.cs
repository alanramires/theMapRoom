using UnityEngine;

// Perfil de IA iniciante (IA Burra).
// Logica reativa simples: defende o HQ se houver inimigo visivel a <= 5 hexagonos,
// caso contrario ataca. Sem memoria entre partidas.
public class BeginnerAIProfile : AIProfile
{
    public const int DefendRadius = 5;

    public override AIStance EvaluateStance(AISnapshot snapshot)
    {
        if (!snapshot.HasHq || snapshot.BoardTilemap == null)
            return AIStance.Attack;

        Vector3Int hqCell = snapshot.HqCell;
        hqCell.z = 0;

        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null)
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;

            if (HexCoordinates.IsWithinRange(snapshot.BoardTilemap, hqCell, enemyCell, DefendRadius))
                return AIStance.Defend;
        }

        return AIStance.Attack;
    }
}
