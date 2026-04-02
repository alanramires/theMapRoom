using System.Collections.Generic;
using UnityEngine;

// Avaliador stateless de planos da IA.
// Planos fixos (defesa/ataque) sao ScriptableObjects configurados no editor.
// Planos variaveis sao gerados dinamicamente a partir do snapshot:
// um plano por setor com construcoes capturaveis nao controladas pela IA.
public static class AIPlanEvaluator
{
    public static List<AIPlanIntent> Evaluate(AIPlanDatabase database, AISnapshot snapshot)
    {
        var result = new List<AIPlanIntent>();
        if (database == null || snapshot == null)
            return result;

        database.EnsureDefaults();

        // HashSet de instanceIds ja designados para evitar duplicidade entre planos.
        var assignedUnits = new HashSet<int>();

        // Planos fixos: defesa primeiro, depois ataque.
        TryActivateFixedPlan(database.defensePlan, snapshot, assignedUnits, result);
        TryActivateFixedPlan(database.attackPlan, snapshot, assignedUnits, result);

        // Planos variaveis: sempre gerados em runtime por setor.
        GenerateDynamicVariablePlans(
            snapshot,
            database.maxVariablePlans,
            database.maxUnitsPerVariablePlan,
            assignedUnits,
            result);

        return result;
    }

    // -- fixed plan activation -------------------------------------------------

    private static bool TryActivateFixedPlan(
        AIPlanData plan,
        AISnapshot snapshot,
        HashSet<int> assignedUnits,
        List<AIPlanIntent> result)
    {
        if (plan == null)
            return false;

        if (!EvaluateConditions(plan, snapshot))
            return false;

        AIPlanIntent intent = BuildIntentFromPlan(plan, snapshot);
        AssignUnitsFromParticipants(intent, plan, snapshot, assignedUnits);
        result.Add(intent);
        return true;
    }

    // ── dynamic variable plan generation ──────────────────────────────────────

    private static void GenerateDynamicVariablePlans(
        AISnapshot snapshot,
        int maxPlans,
        int maxUnitsPerPlan,
        HashSet<int> assignedUnits,
        List<AIPlanIntent> result)
    {
        // Coleta setores distintos com construções capturáveis não totalmente controladas pela IA
        var candidateSectors = new List<SectorCandidate>();
        var seenSectors = new HashSet<ConstructionSector>();

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || !info.IsCapturable) continue;
            if (info.Sector == ConstructionSector.BaseTeam) continue;
            if (seenSectors.Contains(info.Sector)) continue;

            seenSectors.Add(info.Sector);

            // Conta construções capturáveis não controladas pela IA neste setor
            int uncaptured = 0;
            bool hasEnemyHq = false;
            for (int j = 0; j < snapshot.KnownConstructions.Count; j++)
            {
                AIConstructionInfo c = snapshot.KnownConstructions[j];
                if (c == null || c.Sector != info.Sector || !c.IsCapturable) continue;
                bool aiOwnsIt = c.TeamId == snapshot.AiTeam && c.CapturePoints >= c.CapturePointsMax;
                if (!aiOwnsIt)
                {
                    uncaptured++;
                    if (c.IsHq && c.TeamId != snapshot.AiTeam) hasEnemyHq = true;
                }
            }

            if (uncaptured == 0) continue; // setor já totalmente controlado — pula

            // Prioridade: setor com HQ inimigo > mais construções livres > mais próximo do HQ próprio
            int distToHq = int.MaxValue;
            if (snapshot.HasHq)
            {
                Vector2 centroid = ComputeSectorCentroid(info.Sector, snapshot);
                distToHq = Mathf.RoundToInt(
                    (new Vector2(snapshot.HqCell.x, snapshot.HqCell.y) - centroid).magnitude);
            }

            candidateSectors.Add(new SectorCandidate
            {
                Sector      = info.Sector,
                Uncaptured  = uncaptured,
                HasEnemyHq  = hasEnemyHq,
                DistToHq    = distToHq,
            });
        }

        // Ordena: HQ inimigo primeiro, depois mais construções livres, depois mais próximo
        candidateSectors.Sort((a, b) =>
        {
            if (a.HasEnemyHq != b.HasEnemyHq) return b.HasEnemyHq.CompareTo(a.HasEnemyHq);
            if (a.Uncaptured != b.Uncaptured)  return b.Uncaptured.CompareTo(a.Uncaptured);
            return a.DistToHq.CompareTo(b.DistToHq);
        });

        int generated = 0;
        for (int i = 0; i < candidateSectors.Count && generated < maxPlans; i++)
        {
            ConstructionSector sector = candidateSectors[i].Sector;
            AIPlanIntent intent = BuildDynamicIntent(sector, snapshot);

            // Designa até maxUnitsPerPlan infantarias mais próximas do alvo
            AssignInfantryUnits(intent, snapshot, assignedUnits, maxUnitsPerPlan);

            result.Add(intent);
            generated++;
        }
    }

    private struct SectorCandidate
    {
        public ConstructionSector Sector;
        public int Uncaptured;
        public bool HasEnemyHq;
        public int DistToHq;
    }

    // ── conditions (planos fixos) ──────────────────────────────────────────────

    private static bool EvaluateConditions(AIPlanData plan, AISnapshot snapshot)
    {
        if (plan.activationConditions == null || plan.activationConditions.Count == 0)
            return true;

        for (int i = 0; i < plan.activationConditions.Count; i++)
        {
            if (!EvaluateCondition(plan.activationConditions[i], plan.targetSector, snapshot))
                return false;
        }
        return true;
    }

    private static bool EvaluateCondition(PlanCondition cond, ConstructionSector sector, AISnapshot snapshot)
    {
        switch (cond.type)
        {
            case PlanConditionType.AlwaysActive:
                return true;

            case PlanConditionType.SectorNotControlledByAI:
            {
                for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
                {
                    AIConstructionInfo info = snapshot.KnownConstructions[i];
                    if (info == null || info.Sector != sector || !info.IsCapturable) continue;
                    if (info.TeamId != snapshot.AiTeam)
                        return true;
                }
                return false;
            }

            case PlanConditionType.SectorPartiallyControlledByAI:
            {
                bool hasAI = false, hasOther = false;
                for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
                {
                    AIConstructionInfo info = snapshot.KnownConstructions[i];
                    if (info == null || info.Sector != sector || !info.IsCapturable) continue;
                    if (info.TeamId == snapshot.AiTeam) hasAI = true;
                    else                               hasOther = true;
                }
                return hasAI && hasOther;
            }

            case PlanConditionType.EnemyUnitsVisibleInSector:
            {
                for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
                {
                    UnitManager enemy = snapshot.VisibleEnemies[i];
                    if (enemy == null) continue;
                    for (int j = 0; j < snapshot.KnownConstructions.Count; j++)
                    {
                        if (snapshot.KnownConstructions[j]?.Sector == sector)
                            return true;
                    }
                }
                return false;
            }

            case PlanConditionType.FriendlyStrengthBelowPercent:
                return false; // MVP: não implementado

            default:
                return true;
        }
    }

    // ── intent building ────────────────────────────────────────────────────────

    private static AIPlanIntent BuildIntentFromPlan(AIPlanData plan, AISnapshot snapshot)
    {
        var intent = new AIPlanIntent
        {
            Plan        = plan,
            Sector      = plan.targetSector,
            DisplayName = plan.displayName,
        };
        FillCaptureTarget(intent, plan.targetSector, snapshot);
        FillSectorEnemy(intent, plan.targetSector, snapshot);
        return intent;
    }

    private static AIPlanIntent BuildDynamicIntent(ConstructionSector sector, AISnapshot snapshot)
    {
        var intent = new AIPlanIntent
        {
            Plan        = null,
            Sector      = sector,
            DisplayName = $"Captura {sector}",
        };
        FillCaptureTarget(intent, sector, snapshot);
        FillSectorEnemy(intent, sector, snapshot);
        return intent;
    }

    private static void FillCaptureTarget(AIPlanIntent intent, ConstructionSector sector, AISnapshot snapshot)
    {
        AIConstructionInfo bestCapture = null;
        int bestCategory = int.MaxValue;

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Sector != sector || !info.IsCapturable) continue;

            int category;
            if (info.TeamId == snapshot.AiTeam)
            {
                if (info.CapturePoints >= info.CapturePointsMax) continue;
                category = 2;
            }
            else if (info.IsHq) category = 0;
            else               category = 1;

            if (category < bestCategory)
            {
                bestCategory = category;
                bestCapture = info;
            }
        }

        if (bestCapture == null) return;
        intent.HasCaptureTarget    = true;
        intent.CaptureTargetCell   = bestCapture.Cell;
        intent.CaptureTargetCell.z = 0;
        intent.CaptureTargetLabel  = !string.IsNullOrWhiteSpace(bestCapture.DisplayName)
            ? bestCapture.DisplayName
            : sector.ToString();
    }

    private static void FillSectorEnemy(AIPlanIntent intent, ConstructionSector sector, AISnapshot snapshot)
    {
        if (snapshot.VisibleEnemies.Count == 0 || sector == ConstructionSector.BaseTeam)
            return;

        Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
        float bestDist = float.MaxValue;

        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null) continue;
            Vector3Int ec = enemy.CurrentCellPosition;
            float dist = (new Vector2(ec.x, ec.y) - centroid).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                intent.SectorEnemy = enemy;
            }
        }
    }

    private static Vector2 ComputeSectorCentroid(ConstructionSector sector, AISnapshot snapshot)
    {
        Vector2 sum = Vector2.zero;
        int count = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Sector != sector) continue;
            sum += new Vector2(info.Cell.x, info.Cell.y);
            count++;
        }
        return count > 0 ? sum / count : Vector2.zero;
    }

    // ── unit assignment ────────────────────────────────────────────────────────

    // Para planos fixos: segue a lista de participantes do ScriptableObject
    private static void AssignUnitsFromParticipants(
        AIPlanIntent intent,
        AIPlanData plan,
        AISnapshot snapshot,
        HashSet<int> assignedUnits)
    {
        if (plan.participants == null || plan.participants.Count == 0)
            return;

        Vector3Int targetCell = intent.HasCaptureTarget ? intent.CaptureTargetCell : snapshot.HqCell;

        for (int p = 0; p < plan.participants.Count; p++)
        {
            AIPlanParticipantDefinition def = plan.participants[p];
            if (def == null) continue;

            UnitManager bestUnit = null;
            int bestDist = int.MaxValue;

            for (int u = 0; u < snapshot.FriendlyUnits.Count; u++)
            {
                UnitManager unit = snapshot.FriendlyUnits[u];
                if (unit == null || unit.IsDead) continue;
                if (assignedUnits.Contains(unit.InstanceId)) continue;

                unit.TryGetUnitData(out UnitData unitData);

                if (def.unitData != null)
                {
                    if (unitData != def.unitData) continue;
                }
                else
                {
                    if (unitData == null || unitData.unitClass != def.preferredClass) continue;
                }

                Vector3Int uc = unit.CurrentCellPosition;
                int dist = Mathf.Abs(uc.x - targetCell.x) + Mathf.Abs(uc.y - targetCell.y);
                if (dist < bestDist) { bestDist = dist; bestUnit = unit; }
            }

            if (bestUnit == null) continue;
            assignedUnits.Add(bestUnit.InstanceId);
            intent.Assignments.Add(new AIPlanAssignment
            {
                UnitInstanceId = bestUnit.InstanceId,
                Role           = def.role,
                Intent         = intent,
            });
        }
    }

    // Para planos dinâmicos: designa as N infantarias mais próximas do alvo de captura
    private static void AssignInfantryUnits(
        AIPlanIntent intent,
        AISnapshot snapshot,
        HashSet<int> assignedUnits,
        int maxUnits)
    {
        Vector3Int targetCell = intent.HasCaptureTarget ? intent.CaptureTargetCell : snapshot.HqCell;

        // Coleta candidatos ordenados por distância
        var candidates = new List<(UnitManager unit, int dist)>();
        for (int u = 0; u < snapshot.FriendlyUnits.Count; u++)
        {
            UnitManager unit = snapshot.FriendlyUnits[u];
            if (unit == null || unit.IsDead) continue;
            if (assignedUnits.Contains(unit.InstanceId)) continue;

            unit.TryGetUnitData(out UnitData unitData);
            if (unitData == null || unitData.unitClass != GameUnitClass.Infantry) continue;
            if (unitData.aiUnitProfile != null && !unitData.aiUnitProfile.allowCapture) continue;

            Vector3Int uc = unit.CurrentCellPosition;
            int dist = Mathf.Abs(uc.x - targetCell.x) + Mathf.Abs(uc.y - targetCell.y);
            candidates.Add((unit, dist));
        }

        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

        int assigned = 0;
        for (int i = 0; i < candidates.Count && assigned < maxUnits; i++)
        {
            UnitManager unit = candidates[i].unit;
            assignedUnits.Add(unit.InstanceId);
            intent.Assignments.Add(new AIPlanAssignment
            {
                UnitInstanceId = unit.InstanceId,
                Role           = "capturador",
                Intent         = intent,
            });
            assigned++;
        }
    }
}



