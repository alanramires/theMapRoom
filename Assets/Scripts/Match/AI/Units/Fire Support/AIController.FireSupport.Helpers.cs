using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Helpers de apoio de fogo: propriedades de unidade, scoring de alvos
    // e métricas de coesão/linha de retaguarda.
    // -------------------------------------------------------------------------

    private static bool IsFireSupportUnit(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;
        return UnitRoleCompatibility.CanSatisfy(data, UnitRole.FogoIndireto);
    }

    // Skill "Precisa de Reboque" (id "precisaReboque"): a unidade só embarca em slots dedicados de
    // reboque (ex.: Artilharia de Campanha rebocada pelo Suprimentos). Cercamos o embarque no
    // supridor por ESTA skill — não por papel/nome — então só quem de fato é rebocado é afetado.
    private const string TowRequiredSkillId = "precisaReboque";

    private static bool UnitNeedsTow(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || data.skills == null)
            return false;
        for (int i = 0; i < data.skills.Count; i++)
        {
            SkillData skill = data.skills[i];
            if (skill != null && skill.id == TowRequiredSkillId)
                return true;
        }
        return false;
    }

    private static bool IsLongRangeStationary(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.longRangeStationary;
    }

    private static bool PreferFireSupportWeaponMaxRange(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && (data.preferRepositionAtWeaponMaxRange
                || data.preferArtilleryModeBeforeCombatant
                || IsCombatantFireSupport(unit)
                || IsRangedAntiAirFireSupport(unit));
    }

    private static bool IsArtilleryModeOnly(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.preferArtilleryModeBeforeCombatant;
    }

    private static int GetUnitIndirectWeaponMinRange(UnitManager unit)
    {
        if (unit == null) return -1;
        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null) return -1;
        int best = -1;
        foreach (UnitEmbarkedWeapon embarked in weapons)
        {
            if (embarked?.weapon == null || embarked.squadAmmunition <= 0) continue;
            int minR = embarked.GetRangeMin();
            if (minR < 2) continue;
            if (best < 0 || minR < best) best = minR;
        }
        return best;
    }

    private static bool IsFireSupportConservative(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.playConservative;
    }

    private static int GetFireSupportConservativeAvoidEnemyRange(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null || !data.playConservative)
            return 0;

        return Mathf.Max(0, data.aiConservativeSupplyAvoidEnemyRange);
    }

    private bool IsFireSupportConservativeCellAllowed(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int cell)
    {
        int avoidRange = GetFireSupportConservativeAvoidEnemyRange(unit);
        if (avoidRange <= 0)
            return true;

        TeamId aiTeam = snapshot != null ? snapshot.AITeam : unit != null ? unit.TeamId : TeamId.Neutral;
        return !HasNearbyVisibleEnemy(cell, aiTeam, avoidRange);
    }

    private static bool PreferFireSupportBestDpq(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.preferMoveOnBestDPQ;
    }

    private static bool IsFireSupportCloseEnoughToHold(UnitManager unit, Vector3Int cell, Vector3Int anchor)
    {
        int maxRange = GetFireSupportMaxWeaponRange(unit);
        if (maxRange <= 0)
            return true;

        return SectorManager.HexDistance(cell, anchor) <= maxRange + 1f;
    }

    private static int GetFireSupportMaxWeaponRange(UnitManager unit)
    {
        if (unit == null)
            return 0;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null)
            return 0;

        int best = 0;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null || embarked.squadAmmunition <= 0) continue;
            best = Mathf.Max(best, embarked.GetRangeMax());
        }

        return best;
    }

    private Dictionary<Vector3Int, List<Vector3Int>> BuildFireSupportPaths(UnitManager unit)
    {
        return UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap,
            unit,
            Mathf.Max(0, unit.RemainingMovementPoints),
            terrainDatabase);
    }

    private static SectorObjective ResolveAssignedFireSupportObjective(UnitManager unit, TeamObjectivePlan plan)
    {
        if (unit == null || plan == null) return null;
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Role == UnitRole.FogoIndireto && slot.Filled && slot.AssignedUnitId == unit.InstanceId)
                    return obj;
        return null;
    }

    private Vector3Int ResolveFireSupportObjectiveAnchor(SectorObjective assigned, TeamId aiTeam, Vector3Int fallback)
    {
        if (assigned == null) return fallback;

        if (IsRallyAssemblyObjective(assigned))
            return ResolveRallyAssemblyAnchor(assigned, aiTeam, fallback);

        ConstructionManager target = FindCapturableInSector(assigned.Sector, aiTeam, fallback);
        if (target != null)
        {
            Vector3Int targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            return targetCell;
        }

        if (TryGetAnySectorInfo(assigned.Sector, out SectorManager.SectorInfo info))
        {
            Vector3Int cell = info.RepresentativeCell;
            cell.z = 0;
            return cell;
        }

        return fallback;
    }

    private bool TryResolveFireSupportLiveSupportAnchor(
        UnitManager fireSupport,
        AIWorldSnapshot snapshot,
        SectorObjective assigned,
        Vector3Int objectiveAnchor,
        out Vector3Int anchor,
        out string reason)
    {
        anchor = objectiveAnchor;
        reason = null;
        if (fireSupport == null || snapshot == null || assigned == null || assigned.Slots == null)
            return false;

        UnitManager best = null;
        float bestScore = float.MinValue;
        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!slot.Filled || slot.AssignedUnitId == fireSupport.InstanceId)
                continue;

            UnitManager ally = FindActiveUnit(slot.AssignedUnitId, snapshot.AITeam);
            if (ally == null || ally.IsDead || ally.IsEmbarked || ally.IsUnderRepair)
                continue;
            if (IsBacklineSupportUnit(ally))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float distToObjective = SectorManager.HexDistance(allyCell, objectiveAnchor);
            float distToSupport = SectorManager.HexDistance(allyCell, fireSupport.CurrentCellPosition);
            float roleBonus = slot.Role == UnitRole.Capturador ? 900f
                : slot.Role == UnitRole.Assalto ? 450f
                : 0f;
            float score = roleBonus
                - distToObjective * 120f
                - distToSupport * 8f;

            if (score > bestScore)
            {
                bestScore = score;
                best = ally;
                anchor = allyCell;
            }
        }

        if (best == null)
            return false;

        reason = $"liveSupport=#{best.InstanceId}";
        return true;
    }

    private Vector3Int ResolveRogueFireSupportAnchor(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fallback)
    {
        if (TryResolveFireSupportMagnet(
                unit,
                snapshot,
                fallback,
                out UnitManager leader,
                out Vector3Int leaderCell,
                out string magnetKind))
        {
            Debug.Log(
                $"{TL("FireSupport")} {unit.InstanceId} " +
                $"{magnetKind}=#{leader.InstanceId} " +
                $"anchor={leaderCell} " +
                $"dist={SectorManager.HexDistance(fallback, leaderCell):F0}h");
            return leaderCell;
        }

        if (snapshot != null && snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition;
            hq.z = 0;
            return hq;
        }

        if (snapshot != null && snapshot.EnemyUnits != null && snapshot.EnemyUnits.Count > 0)
        {
            UnitManager best = null;
            float bestDist = float.MaxValue;
            foreach (UnitManager enemy in snapshot.EnemyUnits)
            {
                if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
                Vector3Int ec = enemy.CurrentCellPosition;
                ec.z = 0;
                float dist = SectorManager.HexDistance(fallback, ec);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = enemy;
                }
            }

            if (best != null)
            {
                Vector3Int cell = best.CurrentCellPosition;
                cell.z = 0;
                return cell;
            }
        }

        return fallback;
    }

    private bool IsCellACapturerTarget(Vector3Int cell, TeamObjectivePlan plan, TeamId aiTeam)
    {
        cell.z = 0;
        if (plan != null)
        {
            foreach (SectorObjective obj in plan.Objectives)
            {
                bool hasCapturerSlot = false;
                foreach (SlotNeed slot in obj.Slots)
                    if (slot.Role == UnitRole.Capturador && slot.Filled) { hasCapturerSlot = true; break; }
                if (!hasCapturerSlot) continue;
                ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
                if (tgt == null) continue;
                Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;
                if (tc == cell) return true;
            }
        }

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (construction == null
            || !construction.IsCapturable
            || construction.CapturePointsMax <= 0
            || (construction.SlotIndex == ResolveAISlotKey(aiTeam) && construction.CurrentCapturePoints >= construction.CapturePointsMax))
        {
            return false;
        }

        return HasAvailableCapturerReachableForCaptureCell(cell, construction, aiTeam);
    }

    private bool HasAvailableCapturerReachableForCaptureCell(
        Vector3Int captureCell,
        ConstructionManager construction,
        TeamId aiTeam)
    {
        if (construction == null)
            return false;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == null || candidate.SlotIndex != ResolveAISlotKey(aiTeam))
                continue;
            if (candidate.HasActed || candidate.IsDead || candidate.IsEmbarked || candidate.IsUnderRepair)
                continue;
            if (!SimulateCaptureSensor(candidate, captureCell, out ConstructionManager target)
                || target != construction)
            {
                continue;
            }

            Dictionary<Vector3Int, List<Vector3Int>> candidatePaths =
                UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap,
                    candidate,
                    Mathf.Max(0, candidate.RemainingMovementPoints),
                    terrainDatabase);

            if (candidatePaths != null && candidatePaths.ContainsKey(captureCell))
                return true;
        }

        return false;
    }

    private Vector3Int FindFireSupportCapturerVacateCell(
        UnitManager unit, AIWorldSnapshot snapshot, Vector3Int fromCell,
        TeamObjectivePlan plan, Dictionary<Vector3Int, List<Vector3Int>> paths, HashSet<Vector3Int> occupied)
    {
        Vector3Int best = fromCell;
        float bestScore = float.MinValue;
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell; cell.z = 0;
            if (cell == fromCell || occupied.Contains(cell)) continue;
            if (IsCellACapturerTarget(cell, plan, snapshot.AITeam)) continue;
            ConstructionManager destinationConstruction =
                ConstructionOccupancyRules.GetConstructionAtCell(
                    boardTilemap, cell);
            if (destinationConstruction != null
                && destinationConstruction.IsCapturable
                && destinationConstruction.CapturePointsMax > 0
                && destinationConstruction.SlotIndex
                    != ResolveAISlotKey(snapshot.AITeam))
            {
                // Desocupar uma oportunidade de captura parando sobre outra
                // apenas transfere o bloqueio. Radar movel e fogo de suporte
                // podem usar construcoes aliadas, mas nao neutras/inimigas.
                continue;
            }
            float score = GetTerrainDpqPontos(cell) * 25f - CalculateThreatLevel(cell, snapshot.AITeam) * 50f;
            if (score > bestScore) { bestScore = score; best = cell; }
        }
        return best;
    }

    private float ScoreFireSupportTarget(
        UnitManager attacker,
        PodeMirarTargetOption option,
        Vector3Int attackCell,
        Vector3Int targetCell,
        Vector3Int anchor,
        WeaponPriorityData weaponPriorityData,
        out string details)
    {
        details = "";
        UnitManager target = option != null ? option.targetUnit : null;
        if (target == null)
            return 0f;

        float score = 10000f;
        score -= SectorManager.HexDistance(targetCell, anchor) * 500f;
        score += Mathf.Max(0, 20 - target.CurrentHP) * 120f;
        BazookaTargetPriority targetPreference = ResolveFireSupportTargetPreference(attacker, target);
        score += GetFireSupportTargetPreferenceScore(targetPreference);
        if (option.isPreferredTargetForWeapon)
            score += 6500f;
        score += GetFireSupportRangeFitScore(attacker, target, option.distance, option.weapon, weaponPriorityData);

        float targetValueScore = 0f;
        if (target.TryGetUnitData(out UnitData targetUnitData) && targetUnitData != null)
        {
            targetValueScore = targetUnitData.cost * 1.5f + targetUnitData.eliteLevel * 5000f;
            score += targetValueScore;
        }

        string simDetails = "";
        if (TrySimulateAttackForAI(attacker, target, attackCell, out AIAttackSimulationSummary sim))
        {
            float damageScore = sim.targetDamage * 3000f;
            float damagePctScore = sim.targetDamagePct * 80f;
            float killScore = sim.result.killGuaranteed ? 12000f : 0f;
            float survivalPenalty = sim.result.attackerSurvives ? 0f : 4000f;
            score += damageScore + damagePctScore + killScore - survivalPenalty;
            simDetails = $" simDmg={sim.targetDamage} dmgPct={sim.targetDamagePct}% kill={sim.result.killGuaranteed} simScore={(damageScore + damagePctScore + killScore - survivalPenalty):F0}";
        }

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, targetCell);
        float constructionThreatScore = ScoreFireSupportConstructionThreat(target, construction, attacker != null ? attacker.TeamId : TeamId.Neutral);
        score += constructionThreatScore;

        details = $"pref={targetPreference} value={targetValueScore:F0}{simDetails} bldgThreat={constructionThreatScore:F0}";
        return score;
    }

    private float ScoreFireSupportConstructionThreat(UnitManager target, ConstructionManager construction, TeamId aiTeam)
    {
        if (target == null || construction == null || !construction.IsCapturable)
            return 0f;

        bool ownedOrContested = construction.SlotIndex == ResolveAISlotKey(aiTeam)
            || (construction.SlotIndex == ResolveAISlotKey(aiTeam) && construction.CurrentCapturePoints < construction.CapturePointsMax);
        bool enemyHeld = construction.SlotIndex != ResolveAISlotKey(aiTeam);
        float score = ownedOrContested ? 26000f : enemyHeld ? 12000f : 0f;

        if (target.TryGetUnitData(out UnitData targetData)
            && targetData != null
            && targetData.roles != null
            && targetData.roles.Contains(UnitRole.Capturador))
        {
            score += 9000f;
        }

        score += Mathf.Max(0, target.CurrentHP) * 350f;
        return score;
    }

    private float CalculateFireSupportTacticalPressureScore(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        WeaponPriorityData weaponPriorityData)
    {
        if (unit == null || snapshot == null || snapshot.EnemyUnits == null)
            return 0f;

        float best = 0f;
        foreach (UnitManager enemy in snapshot.EnemyUnits)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked) continue;
            if (!enemy.TryGetUnitData(out UnitData enemyData) || enemyData == null) continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            int distance = Mathf.Max(1, Mathf.RoundToInt(SectorManager.HexDistance(cell, enemyCell)));
            float targetScore = GetFireSupportTargetPreferenceScore(ResolveFireSupportTargetPreference(unit, enemy));
            float weaponFit = GetFireSupportRangeFitScore(unit, enemy, distance, null, weaponPriorityData);
            if (weaponFit <= 0f)
                continue;

            float hpScore = Mathf.Max(0, 20 - enemy.CurrentHP) * 45f;
            float score = targetScore + weaponFit + hpScore - enemy.InstanceId * 0.001f;
            if (score > best)
                best = score;
        }

        return best;
    }

    private static BazookaTargetPriority ResolveFireSupportTargetPreference(UnitManager attacker, UnitManager target)
    {
        if (attacker == null || target == null)
            return BazookaTargetPriority.Tertiary;
        if (!attacker.TryGetUnitData(out UnitData attackerData) || attackerData == null)
            return BazookaTargetPriority.Tertiary;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return BazookaTargetPriority.Tertiary;

        return attackerData.ResolveAiTargetPriorityForTargetClass(targetData.unitClass);
    }

    private static float GetFireSupportTargetPreferenceScore(BazookaTargetPriority priority)
    {
        switch (priority)
        {
            case BazookaTargetPriority.Primary:
                return 18000f;
            case BazookaTargetPriority.Secondary:
                return 8500f;
            default:
                return 0f;
        }
    }

    private static float GetFireSupportRangeFitScore(
        UnitManager attacker,
        UnitManager target,
        int distance,
        WeaponData actualWeapon,
        WeaponPriorityData weaponPriorityData)
    {
        if (attacker == null || target == null)
            return 0f;
        if (!target.TryGetUnitData(out UnitData targetData) || targetData == null)
            return 0f;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = attacker.GetEmbarkedWeapons();
        if (weapons == null || weapons.Count == 0)
            return 0f;

        BazookaTargetPriority targetPreference = ResolveFireSupportTargetPreference(attacker, target);
        bool preferredByUnitData = targetPreference == BazookaTargetPriority.Primary
            || targetPreference == BazookaTargetPriority.Secondary;
        float best = 0f;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null) continue;
            if (actualWeapon != null && embarked.weapon != actualWeapon) continue;
            if (embarked.squadAmmunition <= 0) continue;
            if (!embarked.weapon.SupportsOperationOn(target.GetDomain(), target.GetHeightLevel())) continue;

            int minRange = embarked.GetRangeMin();
            int maxRange = embarked.GetRangeMax();
            if (maxRange <= 0) continue;

            bool preferredWeapon = PodeMirarSensor.IsPreferredWeaponForTarget(weaponPriorityData, embarked.weapon, targetData.unitClass);
            int idealRange = Mathf.Clamp(2, minRange, maxRange);
            bool inRange = distance >= minRange && distance <= maxRange;
            if (actualWeapon == null && !inRange)
                continue;

            float rangeError = distance < minRange
                ? minRange - distance
                : distance > maxRange ? distance - maxRange
                : preferredByUnitData ? 0f : Mathf.Abs(distance - idealRange);
            float inRangeBonus = inRange ? 3500f : 0f;
            float preferredBonus = preferredWeapon ? 6500f : 0f;
            float unitPreferenceInRangeBonus = preferredByUnitData && inRange ? 2500f : 0f;
            float score = preferredBonus + inRangeBonus + unitPreferenceInRangeBonus + Mathf.Max(0f, 2600f - rangeError * 900f);
            if (score > best)
                best = score;
        }

        return best;
    }

    private float ScoreFireSupportRepositionCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int cell,
        Vector3Int fromCell,
        Vector3Int anchor,
        float fromDist,
        int pathCost,
        bool preferMaxRange,
        bool conservative,
        bool preferBestDpq,
        int maxRange,
        WeaponPriorityData weaponPriorityData,
        out string details)
    {
        float dist = SectorManager.HexDistance(cell, anchor);
        float progress = fromDist - dist;
        float dpq = GetTerrainDpqPontos(cell);
        float threat = CalculateThreatLevel(cell, snapshot.AITeam);
        float cohesion = conservative ? CalculateFireSupportCohesionScore(unit, snapshot, cell) : 0f;
        float rearLine = conservative ? CalculateFireSupportRearLineScore(unit, snapshot, cell, anchor) : 0f;
        float tacticalPressure = CalculateFireSupportTacticalPressureScore(unit, snapshot, cell, weaponPriorityData);
        if (conservative)
            tacticalPressure = Mathf.Min(tacticalPressure, 3200f);

        float dpqWeight = preferBestDpq ? 95f : 35f;
        float threatWeight = conservative ? 145f : 15f;
        float movementWeight = preferBestDpq ? 18f : 4f;
        float postureScore;

        if (preferMaxRange && maxRange > 0)
        {
            float idealDist = maxRange;
            float rangeError = Mathf.Abs(dist - idealDist);
            postureScore = Mathf.Max(0f, 360f - rangeError * (conservative ? 115f : 90f));

            float overSupportRange = dist - (maxRange + 1f);
            if (overSupportRange > 0f)
                postureScore -= overSupportRange * (conservative ? 260f : 180f);
        }
        else
        {
            postureScore = preferMaxRange
                ? dist * (conservative ? 35f : 50f)
                : progress * (conservative ? 70f : 120f);
        }

        float score = postureScore
            + tacticalPressure
            + dpq * dpqWeight
            + cohesion
            + rearLine
            - threat * threatWeight
            - pathCost * movementWeight;

        if (cell != fromCell && tacticalPressure <= 0f)
            score -= conservative ? 80f : 25f;
        if (conservative && threat > 0f && cell != fromCell)
            score -= threat * 90f;

        details = $"dist={dist:F1} range={maxRange} dpq={dpq:F1} prog={progress:F1} coh={cohesion:F0} rear={rearLine:F0} threat={threat:F1} pressure={tacticalPressure:F0}";
        return score;
    }

    private float CalculateFireSupportCohesionScore(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int cell)
    {
        if (snapshot == null || snapshot.MyUnits == null)
            return 0f;

        float bestDist = float.MaxValue;
        float sumDist = 0f;
        int count = 0;
        foreach (UnitManager ally in snapshot.MyUnits)
        {
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked) continue;
            if (IsBacklineSupportUnit(ally)) continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float dist = SectorManager.HexDistance(cell, allyCell);
            bestDist = Mathf.Min(bestDist, dist);
            sumDist += dist;
            count++;
        }

        if (count == 0)
            return 0f;

        float averageDist = sumDist / count;
        float nearestScore = -Mathf.Abs(bestDist - 2f) * 90f;
        float groupScore = -Mathf.Abs(averageDist - 3.5f) * 35f;
        return nearestScore + groupScore;
    }

    private float CalculateFireSupportRearLineScore(UnitManager unit, AIWorldSnapshot snapshot, Vector3Int cell, Vector3Int anchor)
    {
        if (!TryScoreBacklineCell(unit, snapshot, cell, anchor, out AIBacklineScore backline))
            return 0f;

        return backline.Score;
    }
}
