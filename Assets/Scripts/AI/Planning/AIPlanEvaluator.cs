using System.Collections.Generic;
using UnityEngine;

// Avaliador stateless de planos da IA.
// Planos fixos (defesa/ataque) sao ScriptableObjects configurados no editor.
// Planos variaveis sao gerados dinamicamente a partir do snapshot.
public static class AIPlanEvaluator
{
    public static List<AIPlanIntent> Evaluate(AIPlanDatabase database, AISnapshot snapshot)
    {
        var result = new List<AIPlanIntent>();
        if (database == null || snapshot == null)
            return result;

        database.EnsureDefaults();

        // Impede que a mesma unidade entre em dois planos.
        var assignedUnits = new HashSet<int>();

        // Planos fixos: defesa primeiro, depois ataque.
        TryActivateFixedPlan(database.defensePlan, snapshot, assignedUnits, result);
        TryActivateFixedPlan(database.attackPlan, snapshot, assignedUnits, result);

        // Planos variaveis: gerados em runtime por setor.
        GenerateDynamicVariablePlans(
            snapshot,
            database.maxVariablePlans,
            assignedUnits,
            result);

        return result;
    }

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

    private static void GenerateDynamicVariablePlans(
        AISnapshot snapshot,
        int maxPlans,
        HashSet<int> assignedUnits,
        List<AIPlanIntent> result)
    {
        var candidateSectors = new List<SectorCandidate>();
        var seenSectors = new HashSet<ConstructionSector>();

        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || !info.IsCapturable) continue;
            if (info.Sector == ConstructionSector.BaseTeam) continue;
            if (!seenSectors.Add(info.Sector)) continue;

            int uncaptured = CountUncapturedInSector(info.Sector, snapshot);
            if (uncaptured == 0) continue;

            bool hasEnemyHq = SectorHasEnemyHq(info.Sector, snapshot);
            int distToOwnHq = ComputeSectorDistanceToOwnHq(info.Sector, snapshot);
            int enemyPressure = EstimateEnemyPressure(info.Sector, snapshot);
            int distToEnemyHq = ComputeSectorDistanceToNearestEnemyHq(info.Sector, snapshot);

            candidateSectors.Add(new SectorCandidate
            {
                Sector = info.Sector,
                Uncaptured = uncaptured,
                HasEnemyHq = hasEnemyHq,
                DistToOwnHq = distToOwnHq,
                EnemyPressure = enemyPressure,
                DistToEnemyHq = distToEnemyHq,
            });
        }

        // Priorizacao: mais proximo do HQ proprio > mais construcoes > pressao inimiga > HQ inimigo.
        // Regra de negocio: setores proximos da base devem entrar antes no pipeline de captura.
        candidateSectors.Sort((a, b) =>
        {
            if (a.DistToOwnHq != b.DistToOwnHq) return a.DistToOwnHq.CompareTo(b.DistToOwnHq);
            if (a.Uncaptured != b.Uncaptured) return b.Uncaptured.CompareTo(a.Uncaptured);
            if (a.EnemyPressure != b.EnemyPressure) return b.EnemyPressure.CompareTo(a.EnemyPressure);
            return b.HasEnemyHq.CompareTo(a.HasEnemyHq);
        });

        var drafts = new List<DynamicPlanDraft>();
        int generated = 0;
        for (int i = 0; i < candidateSectors.Count && generated < maxPlans; i++)
        {
            SectorCandidate sector = candidateSectors[i];
            PlannedForce force = ComputePlannedForce(sector);
            AIPlanIntent intent = BuildDynamicIntent(sector.Sector, snapshot);

            var draft = new DynamicPlanDraft
            {
                Candidate = sector,
                Force = force,
                Intent = intent,
                PlanOrder = generated,
            };

            draft.CaptureTargets = CollectUncapturedTargetsInSector(sector.Sector, snapshot);
            draft.InfantryDemand = Mathf.Clamp(Mathf.Min(force.Infantry, draft.CaptureTargets.Count), 0, 6);

            // Ajusta nome para facilitar leitura no log/debug.
            intent.DisplayName = $"Captura {sector.Sector} [INF {force.Infantry}, ARM {force.ArmoredEscort}, ART {force.Artillery}, APC {force.ApcEscort}]";
            intent.TacticalRiskScore = ComputeSupportRiskScore(sector);

            drafts.Add(draft);
            generated++;
        }

        AssignDynamicInfantryAcrossPlans(drafts, snapshot, assignedUnits);

        List<DynamicPlanDraft> supportPriority = new List<DynamicPlanDraft>(drafts);
        supportPriority.Sort((a, b) => ComputeSupportRiskScore(b.Candidate).CompareTo(ComputeSupportRiskScore(a.Candidate)));

        for (int i = 0; i < supportPriority.Count; i++)
        {
            DynamicPlanDraft draft = supportPriority[i];
            if (!HasAssignedRole(draft.Intent, "capturador"))
                continue;

            AssignDynamicSupportForces(draft, snapshot, assignedUnits);
            result.Add(draft.Intent);
        }
    }

    private struct SectorCandidate
    {
        public ConstructionSector Sector;
        public int Uncaptured;
        public bool HasEnemyHq;
        public int DistToOwnHq;
        public int EnemyPressure;
        public int DistToEnemyHq;
    }

    private struct PlannedForce
    {
        public int Infantry;
        public int ArmoredEscort;
        public int Artillery;
        public int ApcEscort;
    }

    private sealed class DynamicPlanDraft
    {
        public SectorCandidate Candidate;
        public PlannedForce Force;
        public AIPlanIntent Intent;
        public List<AIConstructionInfo> CaptureTargets = new List<AIConstructionInfo>();
        public int InfantryDemand;
        public int PlanOrder;
    }

    private sealed class CaptureSlot
    {
        public DynamicPlanDraft Draft;
        public Vector3Int Cell;
        public string Label;
        public int PriorityPenalty;
    }

    private static PlannedForce ComputePlannedForce(SectorCandidate sector)
    {
        bool distant = sector.DistToOwnHq >= 8;
        bool enemyHqProximity = sector.HasEnemyHq || sector.DistToEnemyHq <= 6;

        int infantry = Mathf.Clamp(sector.Uncaptured, 1, 4);
        if (distant) infantry += 1;
        if (sector.EnemyPressure >= 2) infantry += 1;
        if (enemyHqProximity) infantry += 1;
        infantry = Mathf.Clamp(infantry, 1, 6);

        int armored = 0;
        if (sector.Uncaptured >= 2 || distant || sector.EnemyPressure > 0) armored = 1;
        if (sector.Uncaptured >= 4 || sector.EnemyPressure >= 3 || enemyHqProximity) armored = Mathf.Max(armored, 2);

        int artillery = 0;
        if (sector.Uncaptured >= 4 || sector.EnemyPressure >= 2 || enemyHqProximity) artillery = 1;
        if (sector.Uncaptured >= 5 && sector.EnemyPressure >= 3) artillery = 2;

        int apc = 0;
        if (infantry >= 3 && (distant || sector.Uncaptured >= 4)) apc = 1;

        return new PlannedForce
        {
            Infantry = infantry,
            ArmoredEscort = armored,
            Artillery = artillery,
            ApcEscort = apc,
        };
    }

    private static int CountUncapturedInSector(ConstructionSector sector, AISnapshot snapshot)
    {
        int uncaptured = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo c = snapshot.KnownConstructions[i];
            if (c == null || c.Sector != sector || !c.IsCapturable) continue;

            bool aiOwnsFully = c.TeamId == snapshot.AiTeam && c.CapturePoints >= c.CapturePointsMax;
            if (!aiOwnsFully)
                uncaptured++;
        }
        return uncaptured;
    }

    private static bool SectorHasEnemyHq(ConstructionSector sector, AISnapshot snapshot)
    {
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo c = snapshot.KnownConstructions[i];
            if (c == null || c.Sector != sector || !c.IsCapturable) continue;
            if (c.IsHq && c.TeamId != snapshot.AiTeam)
                return true;
        }
        return false;
    }

    private static int ComputeSectorDistanceToOwnHq(ConstructionSector sector, AISnapshot snapshot)
    {
        if (!snapshot.HasHq)
            return int.MaxValue;

        Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
        return Mathf.RoundToInt((new Vector2(snapshot.HqCell.x, snapshot.HqCell.y) - centroid).magnitude);
    }

    private static int ComputeSectorDistanceToNearestEnemyHq(ConstructionSector sector, AISnapshot snapshot)
    {
        Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
        int best = int.MaxValue;

        for (int i = 0; i < snapshot.EnemyHqs.Count; i++)
        {
            AIConstructionInfo hq = snapshot.EnemyHqs[i];
            int dist = Mathf.RoundToInt((new Vector2(hq.Cell.x, hq.Cell.y) - centroid).magnitude);
            if (dist < best)
                best = dist;
        }

        return best;
    }

    private static int EstimateEnemyPressure(ConstructionSector sector, AISnapshot snapshot)
    {
        if (snapshot.VisibleEnemies.Count == 0)
            return 0;

        Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
        int pressure = 0;

        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead) continue;

            Vector3Int ec = enemy.CurrentCellPosition;
            float sqrDist = (new Vector2(ec.x, ec.y) - centroid).sqrMagnitude;

            // Dentro de ~4 tiles do centroide conta como pressao local.
            if (sqrDist <= 16f)
                pressure++;
        }

        return pressure;
    }

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
                bool hasAI = false;
                bool hasOther = false;
                for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
                {
                    AIConstructionInfo info = snapshot.KnownConstructions[i];
                    if (info == null || info.Sector != sector || !info.IsCapturable) continue;
                    if (info.TeamId == snapshot.AiTeam) hasAI = true;
                    else hasOther = true;
                }
                return hasAI && hasOther;
            }

            case PlanConditionType.EnemyUnitsVisibleInSector:
            {
                Vector2 centroid = ComputeSectorCentroid(sector, snapshot);
                for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
                {
                    UnitManager enemy = snapshot.VisibleEnemies[i];
                    if (enemy == null || enemy.IsDead) continue;
                    Vector3Int ec = enemy.CurrentCellPosition;
                    float sqrDist = (new Vector2(ec.x, ec.y) - centroid).sqrMagnitude;
                    if (sqrDist <= 16f)
                        return true;
                }
                return false;
            }

            case PlanConditionType.FriendlyStrengthBelowPercent:
                return false; // MVP

            default:
                return true;
        }
    }

    private static AIPlanIntent BuildIntentFromPlan(AIPlanData plan, AISnapshot snapshot)
    {
        var intent = new AIPlanIntent
        {
            Plan = plan,
            Sector = plan.targetSector,
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
            Plan = null,
            Sector = sector,
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
            else if (info.IsHq)
            {
                category = 0;
            }
            else
            {
                category = 1;
            }

            if (category < bestCategory)
            {
                bestCategory = category;
                bestCapture = info;
            }
        }

        if (bestCapture == null) return;

        intent.HasCaptureTarget = true;
        intent.CaptureTargetCell = bestCapture.Cell;
        intent.CaptureTargetCell.z = 0;
        intent.CaptureTargetLabel = !string.IsNullOrWhiteSpace(bestCapture.DisplayName)
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
            if (enemy == null || enemy.IsDead) continue;

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
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestUnit = unit;
                }
            }

            if (bestUnit == null) continue;

            assignedUnits.Add(bestUnit.InstanceId);
            intent.Assignments.Add(new AIPlanAssignment
            {
                UnitInstanceId = bestUnit.InstanceId,
                Role = def.role,
                Intent = intent,
            });
        }
    }

    private static List<AIConstructionInfo> CollectUncapturedTargetsInSector(ConstructionSector sector, AISnapshot snapshot)
    {
        var targets = new List<AIConstructionInfo>();
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info == null || info.Sector != sector || !info.IsCapturable)
                continue;

            bool aiOwnsFully = info.TeamId == snapshot.AiTeam && info.CapturePoints >= info.CapturePointsMax;
            if (aiOwnsFully)
                continue;

            targets.Add(info);
        }

        Vector3Int hq = snapshot.HqCell;
        targets.Sort((a, b) =>
        {
            int da = Mathf.Abs(a.Cell.x - hq.x) + Mathf.Abs(a.Cell.y - hq.y);
            int db = Mathf.Abs(b.Cell.x - hq.x) + Mathf.Abs(b.Cell.y - hq.y);
            return da.CompareTo(db);
        });

        return targets;
    }

    private static void AssignDynamicInfantryAcrossPlans(
        List<DynamicPlanDraft> drafts,
        AISnapshot snapshot,
        HashSet<int> assignedUnits)
    {
        if (drafts == null || drafts.Count == 0)
            return;

        var infantryUnits = new List<UnitManager>();
        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager unit = snapshot.FriendlyUnits[i];
            if (unit == null || unit.IsDead)
                continue;
            if (assignedUnits.Contains(unit.InstanceId))
                continue;

            unit.TryGetUnitData(out UnitData data);
            if (data == null || data.unitClass != GameUnitClass.Infantry)
                continue;
            if (data.aiUnitProfile != null && !data.aiUnitProfile.allowCapture)
                continue;

            infantryUnits.Add(unit);
        }

        var slots = new List<CaptureSlot>();
        for (int i = 0; i < drafts.Count; i++)
        {
            DynamicPlanDraft draft = drafts[i];
            if (draft.InfantryDemand <= 0 || draft.CaptureTargets == null || draft.CaptureTargets.Count == 0)
                continue;

            int demand = Mathf.Min(draft.InfantryDemand, draft.CaptureTargets.Count);
            for (int t = 0; t < demand; t++)
            {
                AIConstructionInfo target = draft.CaptureTargets[t];
                Vector3Int cell = target.Cell;
                cell.z = 0;
                slots.Add(new CaptureSlot
                {
                    Draft = draft,
                    Cell = cell,
                    Label = !string.IsNullOrWhiteSpace(target.DisplayName) ? target.DisplayName : draft.Candidate.Sector.ToString(),
                    PriorityPenalty = draft.PlanOrder == 0 ? -1000 : 0,
                });
            }
        }

        if (infantryUnits.Count == 0 || slots.Count == 0)
            return;

        int unitCount = infantryUnits.Count;
        int slotCount = slots.Count;
        int source = 0;
        int unitOffset = 1;
        int slotOffset = unitOffset + unitCount;
        int sink = slotOffset + slotCount;
        int nodeCount = sink + 1;

        var graph = new List<List<FlowEdge>>(nodeCount);
        for (int i = 0; i < nodeCount; i++)
            graph.Add(new List<FlowEdge>());

        var unitToSlotEdges = new FlowEdge[unitCount, slotCount];

        for (int u = 0; u < unitCount; u++)
            AddFlowEdge(graph, source, unitOffset + u, 1, 0);

        for (int s = 0; s < slotCount; s++)
            AddFlowEdge(graph, slotOffset + s, sink, 1, 0);

        for (int u = 0; u < unitCount; u++)
        {
            Vector3Int uc = infantryUnits[u].CurrentCellPosition;
            uc.z = 0;
            for (int s = 0; s < slotCount; s++)
            {
                Vector3Int sc = slots[s].Cell;
                int dist = Mathf.Abs(uc.x - sc.x) + Mathf.Abs(uc.y - sc.y);
                int cost = dist * 10 + slots[s].PriorityPenalty;
                unitToSlotEdges[u, s] = AddFlowEdge(graph, unitOffset + u, slotOffset + s, 1, cost);
            }
        }

        int targetFlow = Mathf.Min(unitCount, slotCount);
        MinCostMaxFlow(graph, source, sink, targetFlow);

        for (int u = 0; u < unitCount; u++)
        {
            for (int s = 0; s < slotCount; s++)
            {
                FlowEdge edge = unitToSlotEdges[u, s];
                if (edge == null || edge.Capacity != 0)
                    continue;

                UnitManager unit = infantryUnits[u];
                CaptureSlot slot = slots[s];
                assignedUnits.Add(unit.InstanceId);
                slot.Draft.Intent.Assignments.Add(new AIPlanAssignment
                {
                    UnitInstanceId = unit.InstanceId,
                    Role = "capturador",
                    Intent = slot.Draft.Intent,
                    HasPlannedCaptureTarget = true,
                    PlannedCaptureCell = slot.Cell,
                    PlannedCaptureLabel = slot.Label,
                });
                break;
            }
        }
    }

    private static void AssignDynamicSupportForces(
        DynamicPlanDraft draft,
        AISnapshot snapshot,
        HashSet<int> assignedUnits)
    {
        AIPlanIntent intent = draft.Intent;
        PlannedForce force = draft.Force;
        Vector3Int targetCell = intent.HasCaptureTarget ? intent.CaptureTargetCell : snapshot.HqCell;

        int armoredAssigned = AssignClosestByClass(intent, snapshot, assignedUnits, targetCell, GameUnitClass.Armored, force.ArmoredEscort, "escolta blindada");
        int armoredMissing = force.ArmoredEscort - armoredAssigned;
        if (armoredMissing > 0)
            AssignClosestByClass(intent, snapshot, assignedUnits, targetCell, GameUnitClass.Vehicle, armoredMissing, "escolta leve");

        AssignClosestByClass(intent, snapshot, assignedUnits, targetCell, GameUnitClass.Artillery, force.Artillery, "apoio artilharia");

        if (force.ApcEscort > 0)
            AssignClosestTransporters(intent, snapshot, assignedUnits, targetCell, force.ApcEscort, "transporte escolta");
    }

    private sealed class FlowEdge
    {
        public int To;
        public int ReverseIndex;
        public int Capacity;
        public int Cost;
    }

    private static FlowEdge AddFlowEdge(List<List<FlowEdge>> graph, int from, int to, int capacity, int cost)
    {
        var fwd = new FlowEdge { To = to, ReverseIndex = graph[to].Count, Capacity = capacity, Cost = cost };
        var rev = new FlowEdge { To = from, ReverseIndex = graph[from].Count, Capacity = 0, Cost = -cost };
        graph[from].Add(fwd);
        graph[to].Add(rev);
        return fwd;
    }

    private static void MinCostMaxFlow(List<List<FlowEdge>> graph, int source, int sink, int targetFlow)
    {
        int n = graph.Count;
        int flow = 0;

        var dist = new int[n];
        var inQueue = new bool[n];
        var prevNode = new int[n];
        var prevEdge = new int[n];

        while (flow < targetFlow)
        {
            for (int i = 0; i < n; i++)
            {
                dist[i] = int.MaxValue;
                inQueue[i] = false;
                prevNode[i] = -1;
                prevEdge[i] = -1;
            }

            dist[source] = 0;
            var queue = new Queue<int>();
            queue.Enqueue(source);
            inQueue[source] = true;

            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                inQueue[v] = false;

                List<FlowEdge> edges = graph[v];
                for (int ei = 0; ei < edges.Count; ei++)
                {
                    FlowEdge e = edges[ei];
                    if (e.Capacity <= 0 || dist[v] == int.MaxValue)
                        continue;

                    int nd = dist[v] + e.Cost;
                    if (nd >= dist[e.To])
                        continue;

                    dist[e.To] = nd;
                    prevNode[e.To] = v;
                    prevEdge[e.To] = ei;

                    if (!inQueue[e.To])
                    {
                        inQueue[e.To] = true;
                        queue.Enqueue(e.To);
                    }
                }
            }

            if (dist[sink] == int.MaxValue)
                break;

            int add = targetFlow - flow;
            int cur = sink;
            while (cur != source)
            {
                int pv = prevNode[cur];
                int pe = prevEdge[cur];
                if (pv < 0 || pe < 0)
                {
                    add = 0;
                    break;
                }

                FlowEdge edge = graph[pv][pe];
                if (edge.Capacity < add)
                    add = edge.Capacity;

                cur = pv;
            }

            if (add <= 0)
                break;

            cur = sink;
            while (cur != source)
            {
                int pv = prevNode[cur];
                int pe = prevEdge[cur];
                FlowEdge edge = graph[pv][pe];
                edge.Capacity -= add;
                graph[cur][edge.ReverseIndex].Capacity += add;
                cur = pv;
            }

            flow += add;
        }
    }
    private static int ComputeSupportRiskScore(SectorCandidate sector)
    {
        // Risco tatico para escoltas:
        // - mais longe do HQ proprio = mais risco logistico;
        // - mais perto de HQ inimigo = maior risco de contato;
        // - pressao inimiga/HQ inimigo no setor aumentam prioridade.
        int score = 0;
        score += Mathf.Max(0, sector.DistToOwnHq) * 3;
        score += Mathf.Max(0, 12 - sector.DistToEnemyHq) * 4;
        score += Mathf.Max(0, sector.EnemyPressure) * 8;
        if (sector.HasEnemyHq) score += 12;
        return score;
    }

    private static bool HasAssignedRole(AIPlanIntent intent, string role)
    {
        if (intent == null || intent.Assignments == null || intent.Assignments.Count == 0)
            return false;

        for (int i = 0; i < intent.Assignments.Count; i++)
        {
            AIPlanAssignment assignment = intent.Assignments[i];
            if (assignment != null && assignment.Role == role)
                return true;
        }

        return false;
    }
    private static int AssignClosestByClass(
        AIPlanIntent intent,
        AISnapshot snapshot,
        HashSet<int> assignedUnits,
        Vector3Int targetCell,
        GameUnitClass unitClass,
        int wanted,
        string role,
        bool requireCaptureCapable = false)
    {
        if (wanted <= 0)
            return 0;

        var candidates = new List<(UnitManager unit, int dist)>();
        for (int u = 0; u < snapshot.FriendlyUnits.Count; u++)
        {
            UnitManager unit = snapshot.FriendlyUnits[u];
            if (unit == null || unit.IsDead) continue;
            if (assignedUnits.Contains(unit.InstanceId)) continue;

            unit.TryGetUnitData(out UnitData unitData);
            if (unitData == null || unitData.unitClass != unitClass) continue;

            if (requireCaptureCapable && unitData.aiUnitProfile != null && !unitData.aiUnitProfile.allowCapture)
                continue;

            Vector3Int uc = unit.CurrentCellPosition;
            int dist = Mathf.Abs(uc.x - targetCell.x) + Mathf.Abs(uc.y - targetCell.y);
            candidates.Add((unit, dist));
        }

        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

        int assigned = 0;
        for (int i = 0; i < candidates.Count && assigned < wanted; i++)
        {
            UnitManager unit = candidates[i].unit;
            assignedUnits.Add(unit.InstanceId);
            intent.Assignments.Add(new AIPlanAssignment
            {
                UnitInstanceId = unit.InstanceId,
                Role = role,
                Intent = intent,
            });
            assigned++;
        }

        return assigned;
    }

    private static int AssignClosestTransporters(
        AIPlanIntent intent,
        AISnapshot snapshot,
        HashSet<int> assignedUnits,
        Vector3Int targetCell,
        int wanted,
        string role)
    {
        if (wanted <= 0)
            return 0;

        var candidates = new List<(UnitManager unit, int dist)>();
        for (int u = 0; u < snapshot.FriendlyUnits.Count; u++)
        {
            UnitManager unit = snapshot.FriendlyUnits[u];
            if (unit == null || unit.IsDead) continue;
            if (assignedUnits.Contains(unit.InstanceId)) continue;

            unit.TryGetUnitData(out UnitData unitData);
            if (unitData == null || !unitData.isTransporter) continue;

            Vector3Int uc = unit.CurrentCellPosition;
            int dist = Mathf.Abs(uc.x - targetCell.x) + Mathf.Abs(uc.y - targetCell.y);
            candidates.Add((unit, dist));
        }

        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

        int assigned = 0;
        for (int i = 0; i < candidates.Count && assigned < wanted; i++)
        {
            UnitManager unit = candidates[i].unit;
            assignedUnits.Add(unit.InstanceId);
            intent.Assignments.Add(new AIPlanAssignment
            {
                UnitInstanceId = unit.InstanceId,
                Role = role,
                Intent = intent,
            });
            assigned++;
        }

        return assigned;
    }
}




