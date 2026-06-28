using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Helpers de planejamento: utilitários compartilhados pelo PlanEvaluator.
    // -------------------------------------------------------------------------

    private const int RecentlyCapturedGarrisonTurns = 2;
    private static readonly Dictionary<string, int> recentlyCapturedSectorTurns = new Dictionary<string, int>();

    private void ClearObjectiveHUD(SectorObjective obj)
    {
        foreach (SlotNeed slot in obj.Slots)
        {
            if (!slot.Filled) continue;
            UnitManager u = FindActiveUnit(slot.AssignedUnitId, obj.AssignedTeam);
            if (u != null) u.ClearAIAssignedPlan();
        }
    }

    private void ApplyPlanHUD(UnitManager unit, SectorObjective obj, UnitRole role = UnitRole.Capturador)
    {
        string sectorName = obj.Sector.ToString();
        string badge;
        if (IsRallyAssemblyObjective(obj))
        {
            // Identifica qual massa de invasão a unidade está montando. O antigo "+"
            // escondia o destino quando havia mais de um rally ativo (C+ = Charlie,
            // H+ = Hotel etc.). O planKey continua guardando o nome completo do setor.
            string sectorInitial = sectorName.Length > 0
                ? sectorName[0].ToString().ToUpper()
                : "?";
            badge = sectorInitial + "+";
        }
        else if (ConstructionSectorHelper.IsBase(obj.Sector))
        {
            TeamId hqTeam = FindHQTeamInSector(obj.Sector);
            badge = hqTeam != obj.AssignedTeam
                ? ">>"
                : IsCriticalHomeDefenseObjective(obj, obj.AssignedTeam) ? "!" : "#";
        }
        else
        {
            badge = sectorName.Length > 0 ? sectorName[0].ToString().ToUpper() : "?";
        }
        unit.SetAIAssignedPlan(sectorName, sectorName, badge, (int)role, showAIUnitHUD);
        // A unidade herda o eixo do setor do seu objetivo (0 = fora de eixo).
        unit.SetAIEixo(currentAxisMap != null ? currentAxisMap.GetEixo(obj.Sector) : 0);
    }

    private static TeamId FindHQTeamInSector(ConstructionSector sector)
    {
        foreach (ConstructionManager c in ConstructionManager.AllActive)
            if (c.Sector == sector && c.IsPlayerHeadQuarter)
                return c.TeamId;
        return TeamId.Neutral;
    }

    private static UnitManager FindActiveUnit(int instanceId, TeamId team)
    {
        foreach (UnitManager u in UnitManager.AllActive)
            if (u.InstanceId == instanceId && u.TeamId == team && !u.IsDead) return u;
        return null;
    }

    private static bool HasFilledSlot(SectorObjective obj, UnitRole role)
    {
        if (obj == null || obj.Slots == null) return false;
        foreach (SlotNeed slot in obj.Slots)
            if (slot.Role == role && slot.Filled) return true;
        return false;
    }

    private static bool HasAnySlot(SectorObjective obj, UnitRole role)
    {
        if (obj == null || obj.Slots == null) return false;
        foreach (SlotNeed slot in obj.Slots)
            if (slot.Role == role) return true;
        return false;
    }

    private static int GetCompatibleSlotCapacity(UnitManager transporter, List<UnitManager> capturers)
    {
        if (transporter == null || capturers == null || capturers.Count == 0) return 0;
        if (!transporter.TryGetUnitData(out UnitData tData) || tData == null
            || tData.transportSlots == null || tData.transportSlots.Count == 0) return 0;
        int total = 0;
        foreach (UnitTransportSlotRule tSlot in tData.transportSlots)
        {
            foreach (UnitManager cap in capturers)
            {
                if (!cap.TryGetUnitData(out UnitData cData) || cData == null) continue;
                if (PodeEmbarcarSensor.CanUseSlot(cap, cData, tSlot, out _))
                {
                    total += Mathf.Max(1, tSlot.capacity);
                    break;
                }
            }
        }
        return total;
    }

    private bool IsObjectiveInCombatDisadvantage(
        SectorObjective obj,
        TeamId aiTeam,
        Vector3Int targetCell,
        int enemyRange,
        float disadvantageRatio,
        out int enemyHp,
        out int allyHp)
    {
        enemyHp = 0;
        allyHp = 0;
        if (obj == null)
            return false;

        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, targetCell) <= enemyRange)
                enemyHp += Mathf.Max(1, enemy.CurrentHP);
        }
        if (enemyHp == 0)
            return false;

        foreach (SlotNeed slot in obj.Slots)
        {
            if (!slot.Filled) continue;
            UnitManager ally = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (ally != null)
                allyHp += Mathf.Max(1, ally.CurrentHP);
        }

        return allyHp > 0 && enemyHp >= allyHp * disadvantageRatio;
    }

    private static List<UnitManager> GetAvailablePrimaryAssaults(TeamId aiTeam)
    {
        var list = new List<UnitManager>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId != aiTeam || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
            if (!u.TryGetUnitData(out UnitData data)) continue;
            if (UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Assalto)
                list.Add(u);
        }
        return list;
    }

    private static List<UnitManager> GetAvailablePrimaryFireSupports(TeamId aiTeam)
    {
        var list = new List<UnitManager>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.TeamId != aiTeam || u.IsDead || u.IsEmbarked || u.IsUnderRepair) continue;
            if (!u.TryGetUnitData(out UnitData data)) continue;
            if (UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.FogoIndireto)
                list.Add(u);
        }
        return list;
    }

    private bool HasNearbyVisibleEnemy(Vector3Int cell, TeamId aiTeam, int range)
    {
        MatchController mc = GetMatchController();
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked) continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam)) continue;
            Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(ec, cell) <= range) return true;
        }
        return false;
    }

    private static bool TryGetAnySectorInfo(ConstructionSector sector, out SectorManager.SectorInfo info)
    {
        if (SectorManager.TryGetSectorInfo(sector, out info))
            return true;
        return SectorManager.TryGetBaseInfo(sector, out info);
    }

    private bool TryResolveObjectiveTargetCell(SectorObjective obj, TeamId aiTeam, out Vector3Int targetCell)
    {
        targetCell = Vector3Int.zero;
        if (obj == null)
            return false;

        ConstructionManager target = FindCapturableInSector(obj.Sector, aiTeam);
        if (target != null)
        {
            targetCell = target.CurrentCellPosition;
            targetCell.z = 0;
            return true;
        }

        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
        {
            targetCell = info.RepresentativeCell;
            targetCell.z = 0;
            return true;
        }

        return false;
    }

    private static void EnsureOpenSlots(SectorObjective obj, UnitRole role, int desiredTotal)
    {
        if (obj == null || obj.Slots == null)
            return;

        int total = 0;
        foreach (SlotNeed slot in obj.Slots)
            if (slot.Role == role)
                total++;

        while (total < desiredTotal)
        {
            obj.Slots.Add(new SlotNeed { Role = role });
            total++;
        }
    }

    private static int CountOpenSlots(SectorObjective obj, UnitRole role)
    {
        if (obj == null || obj.Slots == null)
            return 0;

        int count = 0;
        foreach (SlotNeed slot in obj.Slots)
            if (slot.Role == role && !slot.Filled)
                count++;

        return count;
    }

    private static bool HasOwnCaptureProgressInSector(ConstructionSector sector, TeamId aiTeam)
    {
        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.Sector != sector)
                continue;
            if (!construction.IsCapturable || construction.CapturePointsMax <= 0)
                continue;
            if (construction.TeamId != aiTeam)
                continue;
            if (construction.CurrentCapturePoints > 0 && construction.CurrentCapturePoints < construction.CapturePointsMax)
                return true;
        }

        return false;
    }

    private static AIIntelReport BuildPlanIntelReport(AIWorldSnapshot snapshot)
    {
        if (snapshot == null)
            return null;

        JogadasManager jogadas = JogadasManager.EnsureInstance();
        if (jogadas == null || jogadas.log == null || jogadas.log.jogadas == null || jogadas.log.jogadas.Count == 0)
            return null;

        int lookback = AIShoppingPlanner.Instance != null ? Mathf.Max(1, AIShoppingPlanner.Instance.IntelShoppingLookbackTurns) : 4;
        return AIIntelAnalyzer.BuildReport(jogadas.log, snapshot.AITeam, lookback, 5, snapshot.TurnNumber);
    }

    private static AISectorIntel FindIntelForSector(AIIntelReport intel, ConstructionSector sector)
    {
        if (intel == null || intel.sectors == null)
            return null;

        string sectorName = sector.ToString();
        for (int i = 0; i < intel.sectors.Count; i++)
        {
            AISectorIntel entry = intel.sectors[i];
            if (entry != null && entry.sector == sectorName)
                return entry;
        }
        return null;
    }

    private static bool IsHotPlanIntelSector(AISectorIntel intel)
    {
        if (intel == null)
            return false;

        return intel.hotScore >= 2f
            || intel.capturePressure > 0f
            || intel.landingPressure > 0f
            || intel.damageTaken > 0f
            || intel.enemyPresence >= 2f;
    }

    private static int GetEscortFallbackRiskRank(SectorManager.SectorRiskLevel risk)
    {
        switch (risk)
        {
            case SectorManager.SectorRiskLevel.High: return 3;
            case SectorManager.SectorRiskLevel.Medium: return 2;
            case SectorManager.SectorRiskLevel.Low: return 1;
            default: return 0;
        }
    }

    private static int GetIntelSectorPriorityBonus(AIIntelReport intel, ConstructionSector sector)
    {
        AISectorIntel entry = FindIntelForSector(intel, sector);
        if (entry == null)
            return 0;

        float raw = entry.enemyActivity * 2f
            + entry.enemyPresence * 3f
            + entry.capturePressure * 5f
            + entry.landingPressure * 4f
            + entry.damageTaken * 4f;
        return Mathf.Clamp(Mathf.RoundToInt(raw), 0, 35);
    }

    private static int CountControlledProductionBuildings(AIWorldSnapshot snapshot, TeamId aiTeam)
    {
        if (snapshot == null || snapshot.MyBuildings == null)
            return 1;

        int count = 0;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null)
                continue;
            if (building.CanProduceUnitsForTeam(aiTeam))
                count++;
        }

        return Mathf.Max(1, count);
    }

    private static bool IsCaptureProgressStatus(ObjectiveStatus status)
    {
        return status == ObjectiveStatus.Pending
            || status == ObjectiveStatus.Pursuing
            || status == ObjectiveStatus.Capturing
            || status == ObjectiveStatus.PartialReadyForHandoff;
    }

    private static string RecentlyCapturedKey(TeamId team, ConstructionSector sector)
    {
        return ((int)team).ToString() + ":" + sector.ToString();
    }

    private static void RememberRecentlyCapturedSector(TeamId team, ConstructionSector sector, int turn)
    {
        int safeTurn = Mathf.Max(0, turn);
        string key = RecentlyCapturedKey(team, sector);
        if (recentlyCapturedSectorTurns.TryGetValue(key, out int rememberedTurn)
            && safeTurn >= rememberedTurn
            && safeTurn - rememberedTurn < RecentlyCapturedGarrisonTurns)
            return;

        recentlyCapturedSectorTurns[key] = safeTurn;
    }

    private static bool IsRecentlyCapturedSector(TeamId team, ConstructionSector sector, int turn)
    {
        if (!recentlyCapturedSectorTurns.TryGetValue(RecentlyCapturedKey(team, sector), out int capturedTurn))
            return false;

        int safeTurn = Mathf.Max(0, turn);
        return safeTurn >= capturedTurn
            && safeTurn - capturedTurn < RecentlyCapturedGarrisonTurns;
    }

    private static int GetRecentlyCapturedTurnsLeft(TeamId team, ConstructionSector sector, int turn)
    {
        if (!recentlyCapturedSectorTurns.TryGetValue(RecentlyCapturedKey(team, sector), out int capturedTurn))
            return 0;

        int safeTurn = Mathf.Max(0, turn);
        if (safeTurn < capturedTurn)
            return 0;
        return Mathf.Max(0, RecentlyCapturedGarrisonTurns - (safeTurn - capturedTurn));
    }

    private static MatchController cachedMatchController;
    private static MatchController GetMatchController()
    {
        if (cachedMatchController == null)
            cachedMatchController = Object.FindAnyObjectByType<MatchController>();
        return cachedMatchController;
    }
}
