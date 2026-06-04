using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public partial class AIController
{
    private const int RallyPointSectorPriorityBonus = 55;
    private const int RallyHoldRadius = 2;
    private const int RallyAssemblyForceRadius = 3;
    private const int RallyArtilleryRadius = 4;

    private struct AIRallyPlanContext
    {
        public HashSet<ConstructionSector> TargetingEnemyHQ;
        public int RallyPointCount;
    }

    private struct AIRallyReadiness
    {
        public bool Held;
        public int Capturers;
        public int Armor;
        public int Artillery;
        public int ForceScore;
        public string Status;
    }

    private static AIRallyPlanContext BuildRallyPlanContext(TeamId aiTeam, int turnNumber)
    {
        AIRallyPlanContext context = new AIRallyPlanContext
        {
            TargetingEnemyHQ = new HashSet<ConstructionSector>()
        };

        HashSet<int> enemyHQSlots = CollectEnemyHQSlots(aiTeam);
        if (enemyHQSlots.Count == 0)
            return context;

        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint)
                continue;

            if (rally.Sector == ConstructionSector.None)
            {
                Debug.Log($"[AI Rally][T{turnNumber}][{aiTeam}] ignorado {rally.name}: rally sem setor");
                continue;
            }

            IReadOnlyList<int> targetSlots = rally.RallyTargetSlotIndexes;
            if (targetSlots == null || targetSlots.Count == 0)
                continue;

            if (!TryGetFirstEnemyRallyTargetSlot(targetSlots, enemyHQSlots, out int targetSlot))
                continue;

            context.TargetingEnemyHQ.Add(rally.Sector);
            context.RallyPointCount++;
            LogRallyReadiness(rally, targetSlot, aiTeam, turnNumber);
        }

        return context;
    }

    private static HashSet<int> CollectEnemyHQSlots(TeamId aiTeam)
    {
        HashSet<int> slots = new HashSet<int>();
        int ownSlot = -1;

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || !construction.IsPlayerHeadQuarter)
                continue;
            if (construction.TeamId == aiTeam && construction.SlotIndex >= 0)
            {
                ownSlot = construction.SlotIndex;
                break;
            }
        }

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || !construction.IsPlayerHeadQuarter)
                continue;
            if (construction.SlotIndex < 0)
                continue;

            if (ownSlot >= 0)
            {
                if (construction.SlotIndex != ownSlot)
                    slots.Add(construction.SlotIndex);
                continue;
            }

            if (construction.TeamId == aiTeam || construction.TeamId == TeamId.Neutral)
                continue;

            slots.Add(construction.SlotIndex);
        }

        return slots;
    }

    private static bool IsEnemyHQRallySector(AIRallyPlanContext context, ConstructionSector sector)
    {
        return context.TargetingEnemyHQ != null && context.TargetingEnemyHQ.Contains(sector);
    }

    private static bool IsEnemyHQRallySectorHeld(AIRallyPlanContext context, ConstructionSector sector, TeamId aiTeam)
    {
        return IsEnemyHQRallySector(context, sector)
            && TryGetAnySectorInfo(sector, out SectorManager.SectorInfo info)
            && IsRallySectorHeldByTeam(info, aiTeam);
    }

    private static int GetRallySectorPriorityBonus(AIRallyPlanContext context, ConstructionSector sector, TeamId aiTeam)
    {
        return IsEnemyHQRallySector(context, sector) ? RallyPointSectorPriorityBonus : 0;
    }

    private static bool TryGetFirstEnemyRallyTargetSlot(IReadOnlyList<int> targetSlots, HashSet<int> enemyHQSlots, out int targetSlot)
    {
        targetSlot = -1;
        if (targetSlots == null || enemyHQSlots == null || enemyHQSlots.Count == 0)
            return false;

        for (int i = 0; i < targetSlots.Count; i++)
        {
            if (!enemyHQSlots.Contains(targetSlots[i]))
                continue;

            targetSlot = targetSlots[i];
            return true;
        }

        return false;
    }

    private static void LogRallyReadiness(ConstructionManager rally, int targetSlot, TeamId aiTeam, int turnNumber)
    {
        AIRallyReadiness readiness = EvaluateRallyReadiness(rally, aiTeam);
        string rallyName = rally != null ? rally.name : "(null)";
        ConstructionSector sector = rally != null ? rally.Sector : ConstructionSector.None;

        Debug.Log(
            $"[AI Rally][T{turnNumber}][{aiTeam}] {sector} via {rallyName} target={targetSlot} " +
            $"held={readiness.Held} art={readiness.Artillery} cap={readiness.Capturers} " +
            $"armor={readiness.Armor} force={readiness.ForceScore} {readiness.Status}");
    }

    private static AIRallyReadiness EvaluateRallyReadiness(ConstructionManager rally, TeamId aiTeam)
    {
        AIRallyReadiness readiness = new AIRallyReadiness { Status = "WAIT_HOLD" };
        if (rally == null)
            return readiness;

        Vector3Int anchor = rally.CurrentCellPosition;
        anchor.z = 0;

        bool sectorHeld = TryGetAnySectorInfo(rally.Sector, out SectorManager.SectorInfo info)
            && IsRallySectorHeldByTeam(info, aiTeam);
        bool localHold = false;

        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.TeamId != aiTeam || unit.IsDead || unit.IsEmbarked || !unit.gameObject.activeInHierarchy)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;

            Vector3Int unitCell = unit.CurrentCellPosition;
            unitCell.z = 0;
            float dist = SectorManager.HexDistance(anchor, unitCell);

            bool capturer = HasRole(data, UnitRole.Capturador);
            bool armor = IsRallyArmorOrAssaultUnit(data);
            bool artillery = IsRealRallyArtilleryUnit(data);

            if (dist <= RallyHoldRadius && (capturer || armor))
                localHold = true;

            if (dist <= RallyAssemblyForceRadius)
            {
                if (capturer)
                    readiness.Capturers++;
                if (armor)
                    readiness.Armor++;
            }

            if (dist <= RallyArtilleryRadius && artillery)
                readiness.Artillery++;
        }

        readiness.Held = sectorHeld || localHold;
        readiness.ForceScore = readiness.Capturers + (readiness.Armor * 2) + (readiness.Artillery * 2);

        if (!readiness.Held)
            readiness.Status = "WAIT_HOLD";
        else if (readiness.Artillery <= 0)
            readiness.Status = "WAIT_ART";
        else
            readiness.Status = "READY";

        return readiness;
    }

    private static bool IsRallySectorHeldByTeam(SectorManager.SectorInfo info, TeamId aiTeam)
    {
        if (info == null)
            return false;
        if (info.ControllingTeam == aiTeam)
            return true;
        if (info.IsFullyControlled && info.ControllingTeam == aiTeam)
            return true;

        int owned = 0;
        int total = 0;
        IReadOnlyList<SectorManager.SectorConstructionInfo> constructions = info.Constructions;
        if (constructions == null)
            return false;

        for (int i = 0; i < constructions.Count; i++)
        {
            SectorManager.SectorConstructionInfo construction = constructions[i];
            if (construction == null || construction.CapturePointsMax <= 0)
                continue;

            total++;
            if (construction.OwnerTeam == aiTeam)
                owned++;
        }

        return total > 0 && owned >= (total / 2) + 1;
    }

    private static bool IsRallyArmorOrAssaultUnit(UnitData data)
    {
        if (data == null)
            return false;

        return data.unitClass == GameUnitClass.Armored
            || data.unitClass == GameUnitClass.Vehicle
            || HasRole(data, UnitRole.Assalto);
    }

    private static bool IsRealRallyArtilleryUnit(UnitData data)
    {
        if (data == null)
            return false;
        if (!HasRole(data, UnitRole.FogoIndireto) && data.unitClass != GameUnitClass.Artillery)
            return false;

        string key = NormalizeRallyUnitKey($"{data.id} {data.displayName} {data.apelido}");
        if (key.Contains("obus") && key.Contains("leve"))
            return false;

        return key.Contains("astros")
            || (key.Contains("art") && key.Contains("campanha"))
            || (key.Contains("obus") && key.Contains("medio"));
    }

    private static bool HasRole(UnitData data, UnitRole role)
    {
        return data != null && data.roles != null && data.roles.Contains(role);
    }

    private static string NormalizeRallyUnitKey(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string lower = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(lower.Length);
        for (int i = 0; i < lower.Length; i++)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(lower[i]);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(lower[i]);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool HasFilledRealRallyArtillerySlot(SectorObjective obj, TeamId aiTeam)
    {
        if (obj == null || obj.Slots == null)
            return false;

        for (int i = 0; i < obj.Slots.Count; i++)
        {
            SlotNeed slot = obj.Slots[i];
            if (slot == null || slot.Role != UnitRole.FogoIndireto || !slot.Filled)
                continue;

            UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (unit == null || !unit.TryGetUnitData(out UnitData data))
                continue;

            if (IsRealRallyArtilleryUnit(data))
                return true;
        }

        return false;
    }

    private static bool IsRallyAssemblyObjective(SectorObjective obj)
    {
        return obj != null && obj.ObjectiveType == AIObjectiveType.RallyAssembly;
    }

    private static Vector3Int ResolveRallyAssemblyAnchor(SectorObjective obj, TeamId aiTeam, Vector3Int fallback)
    {
        if (obj == null)
            return fallback;

        HashSet<int> enemyHQSlots = CollectEnemyHQSlots(aiTeam);
        ConstructionManager bestRally = null;
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint || rally.Sector != obj.Sector)
                continue;

            IReadOnlyList<int> targetSlots = rally.RallyTargetSlotIndexes;
            if (targetSlots == null || targetSlots.Count == 0)
                continue;

            if (!TryGetFirstEnemyRallyTargetSlot(targetSlots, enemyHQSlots, out _))
                continue;

            bestRally = rally;
            break;
        }

        if (bestRally != null)
        {
            Vector3Int rallyCell = bestRally.CurrentCellPosition;
            rallyCell.z = 0;
            return rallyCell;
        }

        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
        {
            Vector3Int repCell = info.RepresentativeCell;
            repCell.z = 0;
            return repCell;
        }

        return fallback;
    }
}
