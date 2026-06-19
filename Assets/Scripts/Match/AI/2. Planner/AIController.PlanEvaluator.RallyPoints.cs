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
    private const int RallyMinimumArtillery = 3;
    private const int RallyIntelRadius = 6;
    private const int RallyLogisticsRadius = 5;
    private const int RallyAssemblyTimeoutTurns = 4;
    private const int RallyGoGreenSuppressTurns = 8;
    private static readonly Dictionary<string, int> rallyGoGreenTurns = new Dictionary<string, int>();
    private static readonly Dictionary<string, AIRallyHudSnapshot> rallyHudStates = new Dictionary<string, AIRallyHudSnapshot>();

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
        public int Assault;
        public int AirAttack;
        public int Artillery;
        public int Intel;
        public int Logistics;
        public int VisibleThreats;
        public int KnownEnemyForce;
        public int RequiredPackages;
        public int RequiredForce;
        public int ForceScore;
        public bool Timeout;
        public bool GoGreen;
        public AIRallyAssemblyState State;
        public string Status;
        public string Missing;
    }

    private struct AIRallyInfluence
    {
        public bool Active;
        public ConstructionSector Sector;
        public Vector3Int Anchor;
        public AIRallyAssemblyState State;
        public AIRallyReadiness Readiness;
        public int AssemblyRadius;
        public int SupportRadius;
        public string Reason;
    }

    private struct AIRallyHudSnapshot
    {
        public AIRallyAssemblyState State;
        public string Reason;
        public int TurnNumber;
    }

    private static AIRallyPlanContext BuildRallyPlanContext(
        TeamId aiTeam,
        int aiSlotIndex,
        int turnNumber,
        AIIntelReport intel)
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
            LogRallyReadiness(rally, rally.RallyOwnerSlotIndex, aiTeam, turnNumber, intel);
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

    private static void LogRallyReadiness(
        ConstructionManager rally,
        int ownerSlot,
        TeamId aiTeam,
        int turnNumber,
        AIIntelReport intel)
    {
        AIRallyReadiness readiness = EvaluateRallyReadiness(rally, aiTeam, turnNumber, -1, intel);
        string rallyName = rally != null ? rally.name : "(null)";
        ConstructionSector sector = rally != null ? rally.Sector : ConstructionSector.None;
        PublishRallyHudState(rally, readiness.State, readiness.Status, turnNumber);

        Debug.Log(
            $"[AI Rally][T{turnNumber}][{aiTeam}] {sector} via {rallyName} owner={ownerSlot} " +
            $"held={readiness.Held} art={readiness.Artillery} cap={readiness.Capturers} " +
            $"ass={readiness.Assault} airAtk={readiness.AirAttack} intel={readiness.Intel} log={readiness.Logistics} " +
            $"threat={readiness.VisibleThreats} knownEnemy={readiness.KnownEnemyForce} " +
            $"packages={readiness.RequiredPackages} force={readiness.ForceScore}/{readiness.RequiredForce} " +
            $"rallyState={readiness.State} goGreen={readiness.GoGreen} timeout={readiness.Timeout} " +
            $"missing={readiness.Missing} {readiness.Status}");
    }

    private static AIRallyReadiness EvaluateRallyReadiness(ConstructionManager rally, TeamId aiTeam)
    {
        return EvaluateRallyReadiness(rally, aiTeam, -1, -1, null);
    }

    private static AIRallyReadiness EvaluateRallyReadiness(
        ConstructionManager rally,
        TeamId aiTeam,
        int turnNumber,
        int startedTurn,
        AIIntelReport intel)
    {
        AIRallyReadiness readiness = new AIRallyReadiness
        {
            Status = "WAIT_HOLD",
            State = AIRallyAssemblyState.WaitHold,
            Missing = "hold"
        };
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
            bool assault = IsRallyPrimaryAssaultUnit(data);
            bool airAttack = IsOperationalRallyAirAttackUnit(unit, data);
            bool artillery = IsRealRallyArtilleryUnit(data);
            bool intelUnit = HasRole(data, UnitRole.Intel);
            bool logistics = HasRole(data, UnitRole.Logistica);

            if (dist <= RallyHoldRadius && (capturer || assault))
                localHold = true;

            if (dist <= RallyAssemblyForceRadius)
            {
                if (capturer)
                    readiness.Capturers++;
                if (assault)
                    readiness.Assault++;
            }

            if (airAttack)
                readiness.AirAttack++;

            if (dist <= RallyArtilleryRadius && artillery)
                readiness.Artillery++;
            if (dist <= RallyIntelRadius && intelUnit)
                readiness.Intel++;
            if (dist <= RallyLogisticsRadius && logistics)
                readiness.Logistics++;
        }

        readiness.Held = sectorHeld || localHold;
        readiness.VisibleThreats = CountVisibleEnemyThreatsNearRally(anchor, aiTeam, RallyAssemblyForceRadius + 1);
        readiness.KnownEnemyForce = CountKnownEnemyRallyForce(intel);
        readiness.RequiredPackages = Mathf.Clamp(
            Mathf.Max(
                1,
                Mathf.CeilToInt(readiness.KnownEnemyForce / 8f),
                Mathf.CeilToInt(readiness.VisibleThreats / 3f)),
            1,
            5);
        readiness.RequiredForce = 1
            + (readiness.RequiredPackages * 3)
            + (RallyMinimumArtillery * 2);

        int breakthrough = readiness.Assault + readiness.AirAttack;
        int usefulArtillery = Mathf.Min(readiness.Artillery, RallyMinimumArtillery);
        readiness.ForceScore = readiness.Capturers + (breakthrough * 3) + (usefulArtillery * 2);
        readiness.Timeout = startedTurn >= 0 && turnNumber >= 0
            && turnNumber - startedTurn >= RallyAssemblyTimeoutTurns;

        bool hasHoldPackage = readiness.Held
            && readiness.Capturers >= 1
            && breakthrough >= readiness.RequiredPackages;
        bool hasArtillery = readiness.Artillery >= RallyMinimumArtillery;
        bool hasRequiredForce = readiness.ForceScore >= readiness.RequiredForce;
        readiness.GoGreen = readiness.Held
            && hasHoldPackage
            && hasRequiredForce
            && hasArtillery;

        if (!readiness.Held)
        {
            readiness.State = AIRallyAssemblyState.WaitHold;
            readiness.Status = "WAIT_HOLD";
            readiness.Missing = "hold";
        }
        else if (readiness.GoGreen)
        {
            readiness.State = AIRallyAssemblyState.GoGreen;
            readiness.Status = "GO_GREEN";
            readiness.Missing = "-";
        }
        else if (hasHoldPackage && hasArtillery)
        {
            readiness.State = AIRallyAssemblyState.Ready;
            readiness.Status = "READY";
            readiness.Missing = "-";
        }
        else
        {
            readiness.State = AIRallyAssemblyState.Assembling;
            readiness.Status = readiness.Artillery <= 0 ? "ASSEMBLING_WAIT_ART" : "ASSEMBLING";
            readiness.Missing = BuildRallyMissingText(readiness, hasHoldPackage, hasArtillery, hasRequiredForce);
        }

        return readiness;
    }

    private static int CountVisibleEnemyThreatsNearRally(Vector3Int anchor, TeamId aiTeam, float radius)
    {
        MatchController mc = GetMatchController();
        int count = 0;
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy == null || enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked)
                continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam))
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            if (SectorManager.HexDistance(anchor, enemyCell) <= radius)
                count++;
        }

        return count;
    }

    private static int CountKnownEnemyRallyForce(AIIntelReport intel)
    {
        if (intel == null || intel.enemyLastKnownUnits == null)
            return 0;

        float known = 0f;
        for (int i = 0; i < intel.enemyLastKnownUnits.Count; i++)
        {
            AIUnitIntel enemy = intel.enemyLastKnownUnits[i];
            if (enemy == null || enemy.destroyed)
                continue;

            known += Mathf.Clamp(enemy.confidence, 0.25f, 1f);
        }

        return Mathf.CeilToInt(known);
    }

    private static string BuildRallyMissingText(
        AIRallyReadiness readiness,
        bool hasHoldPackage,
        bool hasArtillery,
        bool hasRequiredForce)
    {
        string missing = "";
        if (!hasHoldPackage)
        {
            if (readiness.Capturers < 1)
                missing += $"cap({readiness.Capturers}/1)";
            int breakthrough = readiness.Assault + readiness.AirAttack;
            if (breakthrough < readiness.RequiredPackages)
                missing += string.IsNullOrEmpty(missing)
                    ? $"ruptura({breakthrough}/{readiness.RequiredPackages})"
                    : $"+ruptura({breakthrough}/{readiness.RequiredPackages})";
        }
        if (!hasArtillery)
            missing += string.IsNullOrEmpty(missing)
                ? $"art({readiness.Artillery}/{RallyMinimumArtillery})"
                : $"+art({readiness.Artillery}/{RallyMinimumArtillery})";
        if (!hasRequiredForce)
            missing += string.IsNullOrEmpty(missing)
                ? $"forca({readiness.ForceScore}/{readiness.RequiredForce})"
                : $"+forca({readiness.ForceScore}/{readiness.RequiredForce})";
        return string.IsNullOrEmpty(missing) ? "-" : missing;
    }

    private static void UpdateRallyObjectiveState(
        SectorObjective obj,
        ConstructionManager rally,
        TeamId aiTeam,
        int turnNumber,
        AIIntelReport intel)
    {
        if (obj == null || rally == null)
            return;

        if (obj.RallyAssemblyStartedTurn < 0)
            obj.RallyAssemblyStartedTurn = turnNumber;

        AIRallyReadiness readiness = EvaluateRallyReadiness(
            rally,
            aiTeam,
            turnNumber,
            obj.RallyAssemblyStartedTurn,
            intel);
        Vector3Int rallyAnchor = rally.CurrentCellPosition;
        rallyAnchor.z = 0;
        EnsureRallyAssemblySlots(
            obj,
            readiness.RequiredPackages,
            readiness.AirAttack,
            aiTeam,
            rallyAnchor);
        obj.RallyState = readiness.State;
        obj.RallyReadinessReason =
            $"{readiness.Status} ready={readiness.ForceScore} cap={readiness.Capturers} " +
            $"ass={readiness.Assault} airAtk={readiness.AirAttack} " +
            $"art={readiness.Artillery} intel={readiness.Intel} log={readiness.Logistics} " +
            $"threat={readiness.VisibleThreats} knownEnemy={readiness.KnownEnemyForce} " +
            $"packages={readiness.RequiredPackages} force={readiness.ForceScore}/{readiness.RequiredForce} " +
            $"missing={readiness.Missing}";
        PublishRallyHudState(rally, readiness.State, obj.RallyReadinessReason, turnNumber);

        if (readiness.GoGreen && obj.RallyGoGreenTurn < 0)
        {
            obj.RallyGoGreenTurn = turnNumber;
            RememberRallyGoGreen(aiTeam, obj.Sector, turnNumber);
        }
    }

    private static void EnsureRallyAssemblySlots(
        SectorObjective obj,
        int requiredPackages,
        int operationalAirAttack,
        TeamId aiTeam,
        Vector3Int rallyAnchor)
    {
        if (obj == null)
            return;

        EnsureRallyRoleSlots(obj, UnitRole.Capturador, 1, aiTeam, rallyAnchor);
        EnsureRallyRoleSlots(
            obj,
            UnitRole.Assalto,
            Mathf.Max(0, requiredPackages - operationalAirAttack),
            aiTeam,
            rallyAnchor);
        EnsureRallyRoleSlots(obj, UnitRole.FogoIndireto, RallyMinimumArtillery, aiTeam, rallyAnchor);
    }

    private static void EnsureRallyRoleSlots(
        SectorObjective obj,
        UnitRole role,
        int required,
        TeamId aiTeam,
        Vector3Int rallyAnchor)
    {
        int current = 0;
        for (int i = 0; i < obj.Slots.Count; i++)
        {
            if (obj.Slots[i].Role == role)
                current++;
        }

        while (current > required)
        {
            int removeIndex = FindRallyExcessSlotIndex(obj, role, aiTeam, rallyAnchor);
            if (removeIndex < 0)
                break;

            SlotNeed removed = obj.Slots[removeIndex];
            if (removed.Filled)
            {
                UnitManager released = FindActiveUnit(removed.AssignedUnitId, aiTeam);
                if (released != null)
                    released.ClearAIAssignedPlan();
                Debug.Log($"[AI Rally] {obj.AssignedTeam} {obj.Sector} libera excedente {role} " +
                          $"Unit{removed.AssignedUnitId}: slots={current}->{current - 1} alvo={required}");
            }

            obj.Slots.RemoveAt(removeIndex);
            current--;
        }

        for (int i = current; i < required; i++)
            obj.Slots.Add(new SlotNeed { Role = role });
    }

    private static int FindRallyExcessSlotIndex(
        SectorObjective obj,
        UnitRole role,
        TeamId aiTeam,
        Vector3Int rallyAnchor)
    {
        int farthestFilledIndex = -1;
        float farthestDistance = float.MinValue;
        for (int i = obj.Slots.Count - 1; i >= 0; i--)
        {
            SlotNeed slot = obj.Slots[i];
            if (slot.Role != role)
                continue;
            if (!slot.Filled)
                return i;

            UnitManager assigned = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (assigned == null)
                return i;

            Vector3Int cell = assigned.CurrentCellPosition;
            cell.z = 0;
            float distance = SectorManager.HexDistance(cell, rallyAnchor);
            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                farthestFilledIndex = i;
            }
        }

        return farthestFilledIndex;
    }

    private static void NormalizeStandardObjectiveFireSupportSlots(SectorObjective obj, TeamId aiTeam)
    {
        if (obj == null || obj.Slots == null)
            return;

        Vector3Int anchor = Vector3Int.zero;
        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
        {
            anchor = info.RepresentativeCell;
            anchor.z = 0;
        }

        int current = 0;
        for (int i = 0; i < obj.Slots.Count; i++)
            if (obj.Slots[i].Role == UnitRole.FogoIndireto)
                current++;

        while (current > 1)
        {
            int removeIndex = FindRallyExcessSlotIndex(
                obj,
                UnitRole.FogoIndireto,
                aiTeam,
                anchor);
            if (removeIndex < 0)
                break;

            SlotNeed removed = obj.Slots[removeIndex];
            if (removed.Filled)
            {
                UnitManager released = FindActiveUnit(removed.AssignedUnitId, aiTeam);
                released?.ClearAIAssignedPlan();
                Debug.Log($"[AI Rally] {obj.AssignedTeam} {obj.Sector} perdeu montagem: " +
                          $"libera FireSupport Unit{removed.AssignedUnitId} slots={current}->{current - 1}");
            }

            obj.Slots.RemoveAt(removeIndex);
            current--;
        }
    }

    public static bool TryGetRallyHudState(
        int rallyOwnerSlotIndex,
        ConstructionSector sector,
        out AIRallyAssemblyState state,
        out string reason)
    {
        state = AIRallyAssemblyState.None;
        reason = string.Empty;

        if (rallyOwnerSlotIndex < 0 || sector == ConstructionSector.None)
            return false;

        string key = BuildRallyHudKey(rallyOwnerSlotIndex, sector);
        if (!rallyHudStates.TryGetValue(key, out AIRallyHudSnapshot snapshot))
            return false;

        state = snapshot.State;
        reason = snapshot.Reason;
        return true;
    }

    private static void PublishRallyHudState(
        ConstructionManager rally,
        AIRallyAssemblyState state,
        string reason,
        int turnNumber)
    {
        if (rally == null || !rally.IsRallyPoint || rally.RallyOwnerSlotIndex < 0 || rally.Sector == ConstructionSector.None)
            return;

        string key = BuildRallyHudKey(rally.RallyOwnerSlotIndex, rally.Sector);
        bool changed = !rallyHudStates.TryGetValue(key, out AIRallyHudSnapshot previous)
            || previous.State != state
            || previous.Reason != reason;

        rallyHudStates[key] = new AIRallyHudSnapshot
        {
            State = state,
            Reason = reason,
            TurnNumber = turnNumber
        };

        if (changed)
            ConstructionManager.RefreshRallyHudVisuals(rally.Sector, rally.RallyOwnerSlotIndex);
    }

    private static string BuildRallyHudKey(int rallyOwnerSlotIndex, ConstructionSector sector)
    {
        return $"{rallyOwnerSlotIndex}:{sector}";
    }

    private static void RememberRallyGoGreen(TeamId aiTeam, ConstructionSector sector, int turnNumber)
    {
        if (sector == ConstructionSector.None || turnNumber < 0)
            return;
        rallyGoGreenTurns[$"{(int)aiTeam}:{sector}"] = turnNumber;
    }

    private static bool IsRallyGoGreenSuppressed(TeamId aiTeam, ConstructionSector sector, int turnNumber)
    {
        if (sector == ConstructionSector.None || turnNumber < 0)
            return false;
        string key = $"{(int)aiTeam}:{sector}";
        return rallyGoGreenTurns.TryGetValue(key, out int goTurn)
            && turnNumber - goTurn <= RallyGoGreenSuppressTurns;
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

    private static bool IsRallyPrimaryAssaultUnit(UnitData data)
    {
        return UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Assalto;
    }

    private static bool IsOperationalRallyAirAttackUnit(UnitManager unit, UnitData data)
    {
        if (unit == null || data == null || unit.IsUnderRepair)
            return false;
        if (data.roles == null || data.roles.Count == 0 || data.roles[0] != UnitRole.AtaqueAereo)
            return false;
        if (unit.MaxAmmo > 0 && unit.CurrentAmmo <= 0)
            return false;
        if (unit.MaxFuel > 0 && unit.CurrentFuel <= 0)
            return false;

        return true;
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

    private static bool IsActiveRallyAssemblyObjective(SectorObjective obj)
    {
        return IsRallyAssemblyObjective(obj)
            && obj.RallyState != AIRallyAssemblyState.GoGreen
            && obj.RallyState != AIRallyAssemblyState.Expired;
    }

    private static bool IsRallyGoGreenObjective(SectorObjective obj)
    {
        return IsRallyAssemblyObjective(obj)
            && obj.RallyState == AIRallyAssemblyState.GoGreen;
    }

    private static bool TryFindOwnedRallyForSector(
        ConstructionSector sector,
        TeamId aiTeam,
        out ConstructionManager bestRally)
    {
        bestRally = null;
        if (sector == ConstructionSector.None)
            return false;

        int slot = ResolveAISlotIndex(aiTeam, GetMatchController());
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint || rally.Sector != sector)
                continue;
            if (!IsRallyOwnedBySlot(rally, aiTeam, slot))
                continue;

            bestRally = rally;
            return true;
        }

        return false;
    }

    private static bool TryResolveRallyInfluence(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        Vector3Int fromCell,
        bool includeGoGreen,
        out AIRallyInfluence influence)
    {
        influence = new AIRallyInfluence
        {
            Active = false,
            Anchor = fromCell,
            AssemblyRadius = RallyAssemblyForceRadius,
            SupportRadius = RallyArtilleryRadius,
            State = AIRallyAssemblyState.None,
            Reason = "sem rally"
        };

        if (plan == null || plan.Objectives == null)
            return false;

        SectorObjective bestObj = null;
        ConstructionManager bestRally = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < plan.Objectives.Count; i++)
        {
            SectorObjective obj = plan.Objectives[i];
            if (!IsRallyAssemblyObjective(obj))
                continue;
            if (!includeGoGreen && !IsActiveRallyAssemblyObjective(obj))
                continue;
            if (obj.RallyState == AIRallyAssemblyState.Expired)
                continue;
            if (!TryFindOwnedRallyForSector(obj.Sector, aiTeam, out ConstructionManager rally))
                continue;

            Vector3Int rallyCell = rally.CurrentCellPosition;
            rallyCell.z = 0;
            float score = Mathf.Max(0f, 20f - obj.Priority) * 100f
                - SectorManager.HexDistance(fromCell, rallyCell) * 25f
                + (obj.RallyState == AIRallyAssemblyState.GoGreen ? 150f : 350f);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestObj = obj;
            bestRally = rally;
        }

        if (bestObj == null || bestRally == null)
            return false;

        Vector3Int anchor = bestRally.CurrentCellPosition;
        anchor.z = 0;
        AIRallyReadiness readiness = EvaluateRallyReadiness(
            bestRally,
            aiTeam,
            -1,
            bestObj.RallyAssemblyStartedTurn,
            null);

        influence = new AIRallyInfluence
        {
            Active = true,
            Sector = bestObj.Sector,
            Anchor = anchor,
            State = bestObj.RallyState,
            Readiness = readiness,
            AssemblyRadius = RallyAssemblyForceRadius,
            SupportRadius = RallyArtilleryRadius,
            Reason = bestObj.RallyReadinessReason
        };
        return true;
    }

    private static bool IsRallyAssemblingState(AIRallyAssemblyState state)
    {
        return state == AIRallyAssemblyState.WaitHold
            || state == AIRallyAssemblyState.Assembling
            || state == AIRallyAssemblyState.Ready;
    }

    private static Vector3Int ResolveRallyAssemblyAnchor(SectorObjective obj, TeamId aiTeam, Vector3Int fallback)
    {
        if (obj == null)
            return fallback;

        if (TryFindOwnedRallyForSector(obj.Sector, aiTeam, out ConstructionManager bestRally))
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
