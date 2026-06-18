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
        public int AISlotIndex;
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

    private static AIRallyPlanContext BuildRallyPlanContext(TeamId aiTeam, int aiSlotIndex, int turnNumber)
    {
        AIRallyPlanContext context = new AIRallyPlanContext
        {
            TargetingEnemyHQ = new HashSet<ConstructionSector>(),
            AISlotIndex = aiSlotIndex
        };

        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint)
                continue;

            if (rally.Sector == ConstructionSector.None)
            {
                Debug.Log($"[AI Rally][T{turnNumber}][{aiTeam}] ignorado {rally.name}: rally sem setor");
                continue;
            }

            if (!IsRallyOwnedBySlot(rally, aiTeam, aiSlotIndex))
                continue;

            context.TargetingEnemyHQ.Add(rally.Sector);
            context.RallyPointCount++;
            LogRallyReadiness(rally, rally.RallyOwnerSlotIndex, aiTeam, turnNumber);
        }

        return context;
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

    // RallyOwnerSlotIndex marks the slot that uses this sector as its invasion rally.
    private static bool IsRallyOwnedBySlot(
        ConstructionManager rally,
        TeamId aiTeam,
        int aiSlotIndex)
    {
        if (rally == null || !rally.IsRallyPoint)
            return false;

        MatchController mc = GetMatchController();
        if (mc == null)
            return false;

        int aiSlot = aiSlotIndex >= 0 ? aiSlotIndex : ResolveAISlotIndex(aiTeam, mc);
        if (aiSlot < 0)
            return false;

        return rally.RallyOwnerSlotIndex == aiSlot;
    }

    private static bool IsValidRallyAssemblySectorForSlot(ConstructionSector sector, TeamId aiTeam, int aiSlotIndex)
    {
        if (sector == ConstructionSector.None)
            return false;

        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint || rally.Sector != sector)
                continue;

            if (IsRallyOwnedBySlot(rally, aiTeam, aiSlotIndex))
                return true;
        }

        return false;
    }

    private static bool TryGetOwnedRallySlot(ConstructionManager rally, TeamId aiTeam, out int ownerSlot)
    {
        ownerSlot = -1;
        if (!IsRallyOwnedBySlot(rally, aiTeam, ResolveAISlotIndex(aiTeam, GetMatchController())))
            return false;

        ownerSlot = rally.RallyOwnerSlotIndex;
        return true;
    }

    private static int ResolveAISlotIndex(TeamId aiTeam, MatchController mc)
    {
        if (mc == null || aiTeam == TeamId.Neutral)
            return -1;

        if (mc.ActiveTeam == aiTeam)
            return mc.ActivePlayerListIndex;

        return mc.GetSlotIndexForTeam(aiTeam);
    }

    private static void LogRallyReadiness(ConstructionManager rally, int ownerSlot, TeamId aiTeam, int turnNumber)
    {
        AIRallyReadiness readiness = EvaluateRallyReadiness(rally, aiTeam);
        string rallyName = rally != null ? rally.name : "(null)";
        ConstructionSector sector = rally != null ? rally.Sector : ConstructionSector.None;

        Debug.Log(
            $"[AI Rally][T{turnNumber}][{aiTeam}] {sector} via {rallyName} owner={ownerSlot} " +
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

        ConstructionManager bestRally = null;
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint || rally.Sector != obj.Sector)
                continue;

            if (!IsRallyOwnedBySlot(rally, aiTeam, ResolveAISlotIndex(aiTeam, GetMatchController())))
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
