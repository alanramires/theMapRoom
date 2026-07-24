using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int RallyAssemblyAssaultRadius = 2;
    private const int RogueAssaultHeldRallySearchRadius = 5;

    private PlayerAction DecideRallyAssemblyAssaultAction(UnitManager unit, AIWorldSnapshot snapshot, SectorObjective assigned)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Vector3Int rallyAnchor = ResolveRallyAssemblyAnchor(assigned, snapshot.AITeam, fromCell);
        int rallyRadius = ResolveRallyAssemblyAssaultRadius(unit);

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        if (TryFindAssaultCaptureTargetVacateAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction targetVacateAction))
            return targetVacateAction;

        if (TryFindHomeProductionVacateCombatAction(unit, snapshot, fromCell, paths, occupied, out PlayerAction vacateAction))
            return vacateAction;

        List<UnitManager> threats = CollectAssaultEscortThreats(snapshot.AITeam, rallyAnchor, rallyRadius);
        bool localDefenseContext = true;
        if (TryFindAssaultEscortAttack(unit, snapshot, fromCell, rallyAnchor, rallyRadius, localDefenseContext, paths, occupied, threats,
                out Vector3Int attackCell, out UnitManager attackTarget, out string attackReason))
        {
            Vector3Int targetCell = attackTarget.CurrentCellPosition;
            targetCell.z = 0;
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} rally {assigned.Sector} - protege perimetro via {attackCell} -> {attackTarget.UnitDisplayName}#{attackTarget.InstanceId} ({attackReason})");
            return BuildAttackBatch(unit, snapshot.AITeam, fromCell, attackCell,
                attackTarget.InstanceId.ToString(), targetCell, paths);
        }

        List<Vector3Int> suspectCells = CollectSweepSuspectCells(snapshot.AITeam, rallyAnchor, rallyRadius);
        if (TryFindAssaultScoutRevealMove(unit, snapshot, fromCell, rallyAnchor, rallyRadius, paths, occupied, suspectCells,
                out Vector3Int revealCell, out string revealReason))
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} rally {assigned.Sector} - varre perimetro via {revealCell} ({revealReason})");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, revealCell, paths);
        }

        Vector3Int coverCell = FindAssaultEscortCoverCell(
            unit,
            snapshot,
            fromCell,
            rallyAnchor,
            rallyRadius,
            paths,
            occupied,
            threats,
            bestCapturerDist: -1,
            out string coverEvaluationLog);

        if (!string.IsNullOrEmpty(coverEvaluationLog))
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} rally {assigned.Sector} - perimetro target={rallyAnchor} raio={rallyRadius}h\n{coverEvaluationLog}");

        if (coverCell != fromCell)
        {
            Debug.Log($"{TL("Assalto")} {unit.InstanceId} rally {assigned.Sector} - monta perimetro via {coverCell}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, coverCell, paths);
        }

        Debug.Log($"{TL("Assalto")} {unit.InstanceId} rally {assigned.Sector} - segura perimetro @ {fromCell}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private int ResolveRallyAssemblyAssaultRadius(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null)
            return Mathf.Max(RallyAssemblyAssaultRadius, Mathf.Min(3, data.movement));

        return RallyAssemblyAssaultRadius;
    }

    private bool ShouldReleaseRogueAssaultFromRally(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        out string reason)
    {
        reason = "";
        if (unit == null || snapshot == null || snapshot.EnemyHQ == null)
            return false;

        Vector3Int hqCell = snapshot.EnemyHQ.CurrentCellPosition;
        hqCell.z = 0;
        float selfHqDist = SectorManager.HexDistance(fromCell, hqCell);
        int finalPressureRadius = Mathf.Max(5, GetEffectiveTransportThresholdForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex)));
        if (selfHqDist <= finalPressureRadius)
        {
            if (HasRallyReleaseIntel(snapshot, hqCell, finalPressureRadius, out string intelReason))
            {
                reason = $"pressao final: selfHQ={selfHqDist:F0}<={finalPressureRadius} {intelReason}";
                return true;
            }

            reason = $"segura rally: selfHQ={selfHqDist:F0}<={finalPressureRadius}, sem alvo/FoW revelado {intelReason}";
            return false;
        }

        int pressureUnits = 0;
        float nearest = float.MaxValue;
        foreach (UnitManager ally in UnitManager.AllActive)
        {
            if (ally == null || ally == unit || ally.SlotIndex != snapshot.AISlotIndex || ally.IsDead || ally.IsEmbarked)
                continue;
            if (!ally.TryGetUnitData(out UnitData data) || data == null || data.roles == null || data.roles.Count == 0)
                continue;
            if (!IsRallyReleasePressureUnit(data))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float dist = SectorManager.HexDistance(allyCell, hqCell);
            if (dist < nearest)
                nearest = dist;
            if (dist <= finalPressureRadius)
                pressureUnits++;
        }

        if (pressureUnits >= 2)
        {
            if (HasRallyReleaseIntel(snapshot, hqCell, finalPressureRadius, out string intelReason))
            {
                reason = $"pressao final: aliadosHQ={pressureUnits}<= {finalPressureRadius} nearest={(nearest < float.MaxValue ? nearest.ToString("F0") : "?")} {intelReason}";
                return true;
            }

            reason = $"segura rally: aliadosHQ={pressureUnits}<= {finalPressureRadius} nearest={(nearest < float.MaxValue ? nearest.ToString("F0") : "?")}, sem alvo/FoW revelado {intelReason}";
            return false;
        }

        return false;
    }

    private bool HasRallyReleaseIntel(
        AIWorldSnapshot snapshot,
        Vector3Int hqCell,
        int finalPressureRadius,
        out string reason)
    {
        reason = "";
        if (snapshot == null)
            return false;

        if (snapshot.EnemyUnits != null)
        {
            for (int i = 0; i < snapshot.EnemyUnits.Count; i++)
            {
                UnitManager enemy = snapshot.EnemyUnits[i];
                if (enemy == null || enemy.IsDead || enemy.IsEmbarked)
                    continue;

                Vector3Int enemyCell = enemy.CurrentCellPosition;
                enemyCell.z = 0;
                float dist = SectorManager.HexDistance(enemyCell, hqCell);
                if (dist <= finalPressureRadius + 2)
                {
                    reason = $"intel=inimigoVisivel#{enemy.InstanceId}@{enemyCell} distHQ={dist:F0}";
                    return true;
                }
            }
        }

        MatchController mc = GetMatchController();
        if (mc == null)
        {
            reason = "intel=semMatchController";
            return false;
        }

        hqCell.z = 0;
        if (mc.IsCellVisibleForActiveTeam(hqCell))
        {
            reason = "intel=HQvisivel";
            return true;
        }

        var neighbors = new List<Vector3Int>();
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, hqCell, neighbors);
        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int cell = neighbors[i];
            cell.z = 0;
            if (!mc.IsCellVisibleForActiveTeam(cell))
                continue;

            reason = $"intel=adjHQvisivel@{cell}";
            return true;
        }

        reason = "intel=cego";
        return false;
    }

    private static bool IsRallyReleasePressureUnit(UnitData data)
    {
        if (data == null || data.roles == null)
            return false;

        for (int i = 0; i < data.roles.Count; i++)
        {
            UnitRole role = data.roles[i];
            if (role == UnitRole.Assalto
                || role == UnitRole.Capturador
                || role == UnitRole.FogoIndireto)
                return true;
        }

        return false;
    }

    private bool TryBuildNearbyHeldRallyObjective(
        TeamId aiTeam,
        Vector3Int fromCell,
        TeamObjectivePlan plan,
        int turnNumber,
        out SectorObjective objective,
        out string reason)
    {
        objective = null;
        reason = "";

        HashSet<ConstructionSector> plannedRallySectors = CollectPlannedRallyAssemblySectors(plan);

        ConstructionManager bestRally = null;
        float bestDist = float.MaxValue;
        int activeConstructions = ConstructionManager.AllActive != null ? ConstructionManager.AllActive.Count : 0;
        int seen = 0;
        int skippedNoOwner = 0;
        int skippedNoInfo = 0;
        int skippedNotHeld = 0;
        int skippedFar = 0;
        string firstNoTarget = "";
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint || rally.Sector == ConstructionSector.None)
                continue;

            seen++;
            bool ownedByAI = TryGetOwnedRallySlot(rally, aiTeam, out int ownerSlot);
            bool plannedRally = ownedByAI && plannedRallySectors.Contains(rally.Sector);
            if (!ownedByAI && !plannedRally)
            {
                skippedNoOwner++;
                if (string.IsNullOrEmpty(firstNoTarget))
                    firstNoTarget = $"{rally.Sector}/{rally.name} owner={ownerSlot}";
                continue;
            }
            // Conquistado = o DONO ATUAL do ponto de rally e a AI (rally.SlotIndex == ResolveAISlotKey(aiTeam)). NAO
            // basta unidade amiga perto nem maioria de predios laterais. Ex.: Hotel com Slot ID=
            // verde e Rally Owner=vermelho NAO esta held pelo vermelho.
            if (rally.SlotIndex != ResolveAISlotKey(aiTeam))
            {
                skippedNotHeld++;
                continue;
            }

            Vector3Int rallyCell = rally.CurrentCellPosition;
            rallyCell.z = 0;
            float dist = SectorManager.HexDistance(fromCell, rallyCell);
            if (dist > RogueAssaultHeldRallySearchRadius)
            {
                skippedFar++;
                continue;
            }

            if (dist < bestDist)
            {
                bestDist = dist;
                bestRally = rally;
            }
        }

        if (bestRally == null)
        {
            reason = $"sem rally elegivel perto active={activeConstructions} seen={seen} noOwner={skippedNoOwner} firstNoOwner={firstNoTarget} noInfo={skippedNoInfo} notHeld={skippedNotHeld} far>{RogueAssaultHeldRallySearchRadius}={skippedFar} planned={plannedRallySectors.Count}";
            return false;
        }

        objective = new SectorObjective
        {
            Sector = bestRally.Sector,
            AssignedTeam = aiTeam,
            Status = ObjectiveStatus.Defending,
            ObjectiveType = AIObjectiveType.RallyAssembly,
            Priority = 1
        };
        objective.Slots.Add(new SlotNeed { Role = UnitRole.Assalto, Filled = true });
        reason = $"rally conquistado perto dist={bestDist:F0}h";
        return true;
    }

    private static string FormatSlotList(IReadOnlyList<int> slots)
    {
        if (slots == null || slots.Count == 0)
            return "-";

        string result = "";
        for (int i = 0; i < slots.Count; i++)
        {
            if (!string.IsNullOrEmpty(result))
                result += ",";
            result += slots[i].ToString();
        }

        return result;
    }

    private static HashSet<ConstructionSector> CollectPlannedRallyAssemblySectors(TeamObjectivePlan plan)
    {
        HashSet<ConstructionSector> sectors = new HashSet<ConstructionSector>();
        if (plan == null || plan.Objectives == null)
            return sectors;

        for (int i = 0; i < plan.Objectives.Count; i++)
        {
            SectorObjective obj = plan.Objectives[i];
            if (obj == null || obj.ObjectiveType != AIObjectiveType.RallyAssembly)
                continue;
            sectors.Add(obj.Sector);
        }

        return sectors;
    }
}
