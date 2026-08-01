using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int AggressiveCapturerEngagementRadius = 3;

    private bool TryDecideAggressiveCapturerAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int fromCell,
        Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action)
    {
        using var perf = new AIDecisionPerfScope(unit, "aggressive");
        action = null;
        // assigned NULO e caso normal: unidade sem plano tambem tem ramo
        // agressivo. Antes esta guarda exigia objetivo atribuido, e o resultado
        // era que agressivo de faccao sem QG — ou rogue de IA com QG — nao
        // tinha comportamento agressivo nenhum.
        if (unit == null || snapshot == null || paths == null)
            return false;
        // CanSatisfy, nao roles[0]: papel em posicao secundaria continua sendo
        // o papel. Gate estrito ja mordeu este projeto antes.
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || !UnitRoleCompatibility.CanSatisfy(
                data, UnitRole.CapturadorAgressivo))
            return false;

        // Sem objetivo nao ha status de defesa, e o rotulo do log passa a ser a
        // ancora — que e o que faz papel de objetivo para quem nao tem plano.
        bool defensiveContext =
            assigned != null
            && assigned.Status == ObjectiveStatus.Defending;
        string objectiveLabel = assigned != null
            ? assigned.Sector.ToString()
            : $"âncora {targetCell}";

        List<UnitManager> threats = CollectAssaultEscortThreats(
            snapshot.AITeam, targetCell, AggressiveCapturerEngagementRadius);
        AddAssaultEscortTravelThreats(snapshot.AITeam, fromCell, paths, threats);

        // Ranged aggressive capturers (Bazooka) fire from their current cell whenever
        // possible. The legacy melee search used to evaluate movement first and could
        // walk a ranged unit into counterattack range despite already having a shot.
        var stationaryPath = new Dictionary<Vector3Int, List<Vector3Int>>
        {
            [fromCell] = paths.TryGetValue(fromCell, out List<Vector3Int> stayPath)
                ? stayPath
                : new List<Vector3Int> { fromCell }
        };
        if (TryFindAssaultEscortAttack(
                unit,
                snapshot,
                fromCell,
                targetCell,
                AggressiveCapturerEngagementRadius,
                defensiveContext,
                stationaryPath,
                occupied,
                threats,
                out _,
                out UnitManager rangedTarget,
                out string rangedReason))
        {
            Vector3Int rangedTargetCell = rangedTarget.CurrentCellPosition;
            rangedTargetCell.z = 0;
            Debug.Log($"{TL("CapturadorAgressivo")} {unit.InstanceId} atira parado para {objectiveLabel} "
                + $"de {fromCell} -> {rangedTarget.UnitDisplayName}#{rangedTarget.InstanceId} ({rangedReason})");
            action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, fromCell,
                rangedTarget.InstanceId.ToString(), rangedTargetCell, paths);
            return true;
        }

        if (!TryFindAssaultEscortAttack(
                unit,
                snapshot,
                fromCell,
                targetCell,
                AggressiveCapturerEngagementRadius,
                defensiveContext,
                paths,
                occupied,
                threats,
                out Vector3Int attackCell,
                out UnitManager attackTarget,
                out string attackReason))
            return false;

        Vector3Int enemyCell = attackTarget.CurrentCellPosition;
        enemyCell.z = 0;
        Debug.Log($"{TL("CapturadorAgressivo")} {unit.InstanceId} abre caminho para {objectiveLabel} "
            + $"via {attackCell} -> {attackTarget.UnitDisplayName}#{attackTarget.InstanceId} ({attackReason})");
        action = BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
            attackTarget.InstanceId.ToString(), enemyCell, paths);
        return true;
    }
}
