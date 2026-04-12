using UnityEngine;

public partial class AIPlayerController
{
    private void Phase4_EndTurn(TeamId aiTeam)
    {
        if (aiLog) Debug.Log($"{T(aiTeam, 4)} Passando a vez");
        matchController.AdvanceTurnWithTransition();
    }

    private void LogSnapshot(TeamId aiTeam, AISnapshot snapshot)
    {
        if (!aiLog) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        string stanceLabel = currentStance == AIStance.Defend
            ? $"{GetStanceLabel(currentStance)} (inimigo a <= {snapshot.HqDefendRadius} hexes do HQ)"
            : GetStanceLabel(currentStance);
        int turn = matchController != null ? matchController.CurrentTurn : 0;
        sb.AppendLine($"[AI][T{turn}][{aiTeam}][Fase 0] postura: {stanceLabel} | amigos: {snapshot.FriendlyUnits.Count} | inimigos visiveis: {snapshot.VisibleEnemies.Count}");
        if (matchController != null)
            sb.AppendLine($"  FoW: TotalWar={matchController.EnableTotalWar} | LoS={matchController.EnableLosValidation} | Stealth={matchController.EnableStealthValidation}");
        if (snapshot.HasHq)
            sb.AppendLine($"  HQ proprio: ({snapshot.HqCell.x},{snapshot.HqCell.y})");
        else
            sb.AppendLine("  HQ proprio: nao encontrado");

        if (snapshot.EnemyHqs.Count == 0)
            sb.AppendLine("  HQ inimigo: nenhum encontrado");
        for (int i = 0; i < snapshot.EnemyHqs.Count; i++)
        {
            Vector3Int hqCell = snapshot.EnemyHqs[i].Cell;
            sb.AppendLine($"  HQ inimigo ({snapshot.EnemyHqs[i].TeamId}): ({hqCell.x},{hqCell.y})");
        }

        if (snapshot.VisibleEnemies.Count == 0)
        {
            sb.AppendLine("  inimigos visiveis: nenhum");
        }
        else
        {
            sb.AppendLine($"  inimigos visiveis ({snapshot.VisibleEnemies.Count}):");
            for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
            {
                UnitManager e = snapshot.VisibleEnemies[i];
                if (e == null) continue;
                Vector3Int eCell = e.CurrentCellPosition;
                sb.AppendLine($"    [{i}] {e.name} @ ({eCell.x},{eCell.y})");
            }
        }

        int ownedByAI = 0, neutral = 0, enemy = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            TeamId t = snapshot.KnownConstructions[i].TeamId;
            if (t == aiTeam) ownedByAI++;
            else if (t == TeamId.Neutral) neutral++;
            else enemy++;
        }
        sb.AppendLine($"  construcoes proprias: {ownedByAI} | neutras: {neutral} | inimigas: {enemy}");

        if (snapshot.HasHq)
        {
            sb.Append($"  proximas do HQ (r={snapshot.HqDefendRadius}): ");
            if (snapshot.ConstructionsNearHq.Count == 0)
            {
                sb.AppendLine("nenhuma");
            }
            else
            {
                for (int i = 0; i < snapshot.ConstructionsNearHq.Count; i++)
                {
                    AIConstructionInfo info = snapshot.ConstructionsNearHq[i];
                    if (i > 0) sb.Append(", ");
                    sb.Append($"{info.DisplayName}({info.TeamId})");
                }
                sb.AppendLine();
            }
        }

        if (snapshot.ActivePlans.Count == 0)
        {
            sb.AppendLine("  planos: nenhum ativo");
        }
        else
        {
            sb.AppendLine($"  planos ({snapshot.ActivePlans.Count}):");
            for (int i = 0; i < snapshot.ActivePlans.Count; i++)
            {
                AIPlanIntent intent = snapshot.ActivePlans[i];
                if (intent == null) continue;
                string planName = !string.IsNullOrWhiteSpace(intent.DisplayName)
                    ? intent.DisplayName
                    : "(plano dinamico)";
                string captureStr = intent.HasCaptureTarget
                    ? $" ? {intent.CaptureTargetLabel} ({intent.CaptureTargetCell.x},{intent.CaptureTargetCell.y})"
                    : string.Empty;
                string riskStr = intent.TacticalRiskScore > 0
                    ? $" | risco: {intent.TacticalRiskScore}"
                    : string.Empty;
                sb.Append($"    [{i}] {planName} [{intent.Sector}]{captureStr}{riskStr}");
                if (intent.Assignments.Count > 0)
                {
                    sb.Append("  unidades:");
                    for (int a = 0; a < intent.Assignments.Count; a++)
                    {
                        AIPlanAssignment asgn = intent.Assignments[a];
                        UnitManager u = FindUnitById(asgn.UnitInstanceId);
                        string uName = u != null ? u.name : $"#{asgn.UnitInstanceId}";
                        string role = asgn.Role.ToDebugLabel();
                        sb.Append($" {uName}({role})");
                    }
                }
                sb.AppendLine();
            }
        }

        Debug.Log(sb.ToString());
    }
}
