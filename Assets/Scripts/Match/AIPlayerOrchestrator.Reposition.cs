using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Lógica de reposicionamento e aproximação cautelosa para a IA.
/// Partial class de AIPlayerOrchestrator.
/// </summary>
public partial class AIPlayerOrchestrator
{
    /// <summary>
    /// Move em direção ao inimigo visível mais próximo.
    /// Com useDpq ativo, desempata posições equidistantes pelo maior DPQ.
    /// </summary>
    private PlayerAction DecideReposition(UnitManager unit, TeamId myTeam,
        Vector3Int fromCell, List<Vector3Int> freeCells, bool useDpq,
        Vector3Int? targetOverride = null)
    {
        Vector3Int destination = fromCell;

        if (freeCells.Count > 0)
        {
            Vector3Int? target = targetOverride;

            if (target == null)
            {
                UnitManager closestEnemy = FindClosestVisibleEnemy(fromCell, myTeam);
                if (closestEnemy != null)
                {
                    Vector3Int enemyCell = closestEnemy.CurrentCellPosition;
                    enemyCell.z = 0;
                    target = enemyCell;
                    Debug.Log($"[AI] {unit.InstanceId} avança em direção a {closestEnemy.InstanceId} em {enemyCell}");
                }
            }

            if (target.HasValue)
            {
                destination = useDpq
                    ? freeCells
                        .OrderBy(c => HexWorldDistance(c, target.Value))
                        .ThenByDescending(c => turnStateManager.GetCellDpqPoints(c, unit))
                        .First()
                    : freeCells
                        .OrderBy(c => HexWorldDistance(c, target.Value))
                        .First();

                Debug.Log($"[AI] {unit.InstanceId} reposicionando → {destination} (target {target.Value})");
            }
            else
            {
                destination = freeCells[Random.Range(0, freeCells.Count)];
                Debug.Log($"[AI] {unit.InstanceId} sem inimigos visíveis, move aleatório para {destination}");
            }
        }

        return BuildMoveBatch(unit, myTeam, fromCell, destination);
    }

    /// <summary>
    /// Aproximação cautelosa: avança em direção a um prédio contestado oculto por FOW,
    /// sempre priorizando a célula com melhor DPQ entre as mais próximas ao alvo.
    /// </summary>
    private PlayerAction DecideCautiousApproach(UnitManager unit, TeamId myTeam,
        Vector3Int fromCell, List<Vector3Int> freeCells, Vector3Int targetCell)
    {
        if (freeCells.Count == 0)
            return BuildMoveBatch(unit, myTeam, fromCell, fromCell);

        Vector3Int destination = freeCells
            .OrderBy(c => HexWorldDistance(c, targetCell))
            .ThenByDescending(c => turnStateManager.GetCellDpqPoints(c, unit))
            .First();

        return BuildMoveBatch(unit, myTeam, fromCell, destination);
    }
}
