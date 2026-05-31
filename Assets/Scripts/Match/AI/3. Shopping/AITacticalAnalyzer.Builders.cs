using System.Collections.Generic;
using UnityEngine;

// Construtores de operações táticas: um método TryBuild* por tipo de necessidade.
public partial class AITacticalAnalyzer
{
    private void TryBuildBaseDefenseOp(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan, List<AITacticalNeed> ops, AIIntelReport intel)
    {
        int aircraftNearHQ = CountVisibleEnemyAircraftNearHQ(snapshot, InstanceSafeAntiAirCoverageRange());
        int fighterANearHQ = CountVisibleEnemyFighterANearHQ(snapshot, InstanceSafeAntiAirCoverageRange());
        int armorNearBase = CountVisibleEnemyArmorNearOwnedBase(snapshot, DefensiveArmorThreatRange);
        bool captureActive = CountOwnedHomeConstructionsUnderCapture(snapshot, team) > 0;
        bool homeThreat = IsHomeDefenseThreatened(snapshot, team, HomeThreatRange) || captureActive;
        AISectorIntel homeIntel = FindIntelForSector(intel, snapshot.MyHQ != null ? snapshot.MyHQ.Sector : ConstructionSector.Base1);
        bool intelHomeThreat = homeIntel != null && IsHotIntelSector(homeIntel);

        if (aircraftNearHQ <= 0 && armorNearBase <= 0 && !homeThreat && !intelHomeThreat)
            return;

        AITacticalNeed op = CreateOperation(team, AITacticalNeedType.BaseDefense, 1, snapshot, snapshot.MyHQ != null ? snapshot.MyHQ.Sector : ConstructionSector.Base1);
        op.IsUrgent = true;
        op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
        op.TargetCell = op.AnchorCell;
        op.LinkedObjective = FindHomeDefenseObjective(plan, team);

        if (aircraftNearHQ > 0 || (homeIntel != null && intel.enemyAirThreatScore >= 2f))
        {
            if (fighterANearHQ > 0)
                op.AddSlots(AINeedKind.FighterA, CountActiveNeed(snapshot, AINeedKind.FighterA) <= 0 ? 1 : 0);
            else if (CountActiveNeed(snapshot, AINeedKind.FighterA) + CountActiveNeed(snapshot, AINeedKind.FighterB) <= 0)
                op.AddSlots(AINeedKind.FighterB, 1);
            op.AddSlots(AINeedKind.AAA, Mathf.Max(0, InstanceMinBaseAAA() - CountActiveNeed(snapshot, AINeedKind.AAA)));
            if (CountActiveNeed(snapshot, AINeedKind.SAM) < 1)
                op.AddSlots(AINeedKind.SAM, 1);
        }

        if (homeThreat || armorNearBase > 0 || intelHomeThreat)
        {
            if (captureActive || (homeIntel != null && homeIntel.capturePressure > 0f))
                op.AddSlots(AINeedKind.Capturer, 2);
            else if (CountOwnedConstructionsUnderCapture(snapshot, team) > 0)
                op.AddSlots(AINeedKind.Capturer, 1);
            op.AddSlots(AINeedKind.Assault, armorNearBase > 0 || (intel != null && intel.enemyArmorThreatScore >= 2f) ? 2 : 1);
            op.AddSlots(AINeedKind.Artillery, 1);
        }

        if (op.RequiredSlots.Count == 0)
            return;

        ops.Add(op);
        Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] BaseDefense URGENTE aircraft={aircraftNearHQ} fighterA={fighterANearHQ} armor={armorNearBase} capture={captureActive} intelHot={(homeIntel != null ? homeIntel.hotScore.ToString("F1") : "-")} slots={DescribeSlots(op)}");
    }

    private void TryBuildSectorDefenseOps(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan, List<AITacticalNeed> ops, AIIntelReport intel)
    {
        if (plan == null)
            return;

        var built = new HashSet<ConstructionSector>();
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Status != ObjectiveStatus.Defending) continue;
            if (ConstructionSectorHelper.IsBase(obj.Sector)) continue;
            if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info)) continue;
            if (!IsOwnedDefensibleSector(info, team))
            {
                Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] SectorDefense skip {obj.Sector}: stale defense owner={info.ControllingTeam}");
                continue;
            }
            BuildSectorDefenseOp(team, snapshot, obj, info, ops, built, FindIntelForSector(intel, info.Sector));
        }

        foreach (SectorManager.SectorInfo info in SectorManager.GetAllSectorInfos())
        {
            if (info == null || ConstructionSectorHelper.IsBase(info.Sector)) continue;
            if (built.Contains(info.Sector)) continue;
            if (!IsOwnedDefensibleSector(info, team)) continue;
            AISectorIntel sectorIntel = FindIntelForSector(intel, info.Sector);
            if (!HasNearbyVisibleEnemy(snapshot, info.RepresentativeCell, SectorDefenseRange) && !IsHotIntelSector(sectorIntel)) continue;
            BuildSectorDefenseOp(team, snapshot, plan.GetObjectiveForSector(info.Sector), info, ops, built, sectorIntel);
        }
    }

    private void TryBuildGroundCaptureOps(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan, List<AITacticalNeed> ops, AIIntelReport intel)
    {
        if (plan == null || snapshot == null)
            return;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Slots == null)
                continue;
            if (obj.Status != ObjectiveStatus.Pending
                && obj.Status != ObjectiveStatus.Pursuing
                && obj.Status != ObjectiveStatus.Capturing
                && obj.Status != ObjectiveStatus.PartialReadyForHandoff)
                continue;
            if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info))
                continue;
            if (HasAnySlot(obj, UnitRole.Transportador)
                && info.GetTransportPreference(team) == SectorManager.SectorInfo.TransportPreference.Air)
                continue;

            int capturers = CountSlots(obj, UnitRole.Capturador);
            int assaults = CountSlots(obj, UnitRole.Assalto);
            int fireSupport = CountSlots(obj, UnitRole.FogoIndireto);
            AISectorIntel sectorIntel = FindIntelForSector(intel, obj.Sector);
            bool risky = info.GetRiskLevelFor(team) >= SectorManager.SectorRiskLevel.Medium || IsHotIntelSector(sectorIntel);
            bool hasBasicTaskForce = capturers > 0 && (assaults > 0 || fireSupport > 0);
            bool needsAssaultScreen = risky && !hasBasicTaskForce;

            AITacticalNeed op = CreateOperation(team, AITacticalNeedType.GroundCapture, 4, snapshot, obj.Sector);
            op.LinkedObjective = obj;
            op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
            op.TargetCell = Normalize(info.RepresentativeCell);
            op.AddSlots(AINeedKind.Capturer, Mathf.Max(0, capturers));
            op.AddSlots(AINeedKind.Assault, Mathf.Max(assaults, needsAssaultScreen ? 1 : 0));
            op.AddSlots(AINeedKind.FireSupport, Mathf.Max(0, fireSupport));

            if (op.RequiredSlots.Count == 0)
                continue;

            ops.Add(op);
            Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] GroundCapture {obj.Sector}: cap={capturers} ass={assaults} fire={fireSupport} risky={risky} slots={DescribeSlots(op)}");
        }
    }

    private void BuildSectorDefenseOp(
        TeamId team,
        AIWorldSnapshot snapshot,
        SectorObjective linked,
        SectorManager.SectorInfo info,
        List<AITacticalNeed> ops,
        HashSet<ConstructionSector> built,
        AISectorIntel sectorIntel = null)
    {
        AITacticalNeed op = CreateOperation(team, AITacticalNeedType.SectorDefense, 2, snapshot, info.Sector);
        op.LinkedObjective = linked;
        op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
        op.TargetCell = Normalize(info.RepresentativeCell);

        if (info.HasPartialCapture)
            op.AddSlots(AINeedKind.Capturer, 1);
        if (sectorIntel != null && sectorIntel.capturePressure > 0f)
            op.AddSlots(AINeedKind.Capturer, 1);
        op.AddSlots(AINeedKind.Assault, 1);
        if (sectorIntel != null && (sectorIntel.enemyPresence >= 2f || sectorIntel.landingPressure > 0f))
            op.AddSlots(AINeedKind.Assault, 1);
        if (info.GetDistanceToHQ(team) <= GetEffectiveTransportThreshold(team))
            op.AddSlots(AINeedKind.Artillery, 1);
        else if (sectorIntel != null && sectorIntel.damageTaken > 0f)
            op.AddSlots(AINeedKind.Artillery, 1);
        if (CountVisibleEnemyAircraftNearCell(snapshot, info.RepresentativeCell, 2) > 0)
            op.AddSlots(AINeedKind.AAA, 1);

        ops.Add(op);
        built.Add(info.Sector);
        Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] SectorDefense {info.Sector}: partial={info.HasPartialCapture} intelHot={(sectorIntel != null ? sectorIntel.hotScore.ToString("F1") : "-")} slots={DescribeSlots(op)}");
    }

    private void TryBuildAirliftCaptureOps(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan, List<AITacticalNeed> ops, AIIntelReport intel)
    {
        if (plan == null)
            return;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null) continue;
            if (obj.Status != ObjectiveStatus.Pending
                && obj.Status != ObjectiveStatus.Pursuing
                && obj.Status != ObjectiveStatus.Capturing)
                continue;
            if (!HasAnySlot(obj, UnitRole.Transportador)) continue;
            if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info)) continue;

            int desiredPassengers = CountSlots(obj, UnitRole.Capturador);
            int qualifiedCapturers = CountFilledCompatibleSlots(obj, AINeedKind.Capturer);
            int assignedChinooks = CountFilledCompatibleSlots(obj, AINeedKind.AirTransport);
            int embarkedCapturers = CountEmbarkedCapturersForObjective(snapshot, obj);
            int capturerDeficit = Mathf.Max(0, desiredPassengers - qualifiedCapturers - embarkedCapturers);
            int neededChinooks = Mathf.CeilToInt(desiredPassengers / 2f);
            int airDeficit = Mathf.Max(0, neededChinooks - assignedChinooks);

            AITacticalNeed op = CreateOperation(team, AITacticalNeedType.AirliftCapture, 4, snapshot, obj.Sector);
            op.LinkedObjective = obj;
            op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
            op.TargetCell = Normalize(info.RepresentativeCell);
            AISectorIntel sectorIntel = FindIntelForSector(intel, obj.Sector);
            op.AddSlots(AINeedKind.Capturer, capturerDeficit);
            op.AddSlots(AINeedKind.AirTransport, airDeficit);
            if (info.GetRiskLevelFor(team) >= SectorManager.SectorRiskLevel.High || IsHotIntelSector(sectorIntel))
                op.AddSlots(AINeedKind.Assault, 1);
            if (sectorIntel != null && sectorIntel.damageTaken > 0f)
                op.AddSlots(AINeedKind.Artillery, 1);
            if (CountTotalVisibleEnemyFighterA(snapshot) > 0)
                op.AddSlots(AINeedKind.FighterA, CountActiveNeed(snapshot, AINeedKind.FighterA) <= 0 ? 1 : 0);
            else if (CountTotalVisibleEnemyAircraft(snapshot) > 0
                && CountActiveNeed(snapshot, AINeedKind.FighterA) + CountActiveNeed(snapshot, AINeedKind.FighterB) <= 0)
                op.AddSlots(AINeedKind.FighterB, 1);

            if (op.RequiredSlots.Count == 0)
                continue;

            ops.Add(op);
            Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] AirliftCapture {obj.Sector}: desejado={desiredPassengers} qualified={qualifiedCapturers} embarked={embarkedCapturers} cap_deficit={capturerDeficit} air_deficit={airDeficit} intelHot={(sectorIntel != null ? sectorIntel.hotScore.ToString("F1") : "-")}");
        }
    }

    private void TryBuildPreventiveDefenseOp(TeamId team, AIWorldSnapshot snapshot, List<AITacticalNeed> ops)
    {
        if (snapshot == null)
            return;

        int minTurn = AIShoppingPlanner.Instance != null ? AIShoppingPlanner.Instance.MinTurnBaseDefense : 3;
        bool rich = HasPreventiveDefenseBudget(snapshot);
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn && !rich)
            return;

        int activeArt = CountActiveNeed(snapshot, AINeedKind.Artillery) + CountActiveNeed(snapshot, AINeedKind.FireSupport);
        int activeAAA = CountActiveNeed(snapshot, AINeedKind.AAA);
        int activeSAM = CountActiveNeed(snapshot, AINeedKind.SAM);

        int artDeficit = Mathf.Max(0, InstanceMinBaseArtillery() - activeArt);
        int aaaDeficit = Mathf.Max(0, InstanceMinBaseAAA() - activeAAA);
        int samDeficit = activeAAA >= 1 && activeSAM < 1 ? 1 : 0;
        if (artDeficit <= 0 && aaaDeficit <= 0 && samDeficit <= 0)
            return;

        AITacticalNeed op = CreateOperation(team, AITacticalNeedType.PreventiveDefense, 6, snapshot, snapshot.MyHQ != null ? snapshot.MyHQ.Sector : ConstructionSector.Base1);
        op.IsPreventive = true;
        op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
        op.TargetCell = op.AnchorCell;
        op.AddSlots(AINeedKind.Artillery, artDeficit);
        op.AddSlots(AINeedKind.AAA, aaaDeficit);
        op.AddSlots(AINeedKind.SAM, samDeficit);

        ops.Add(op);
        Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] PreventiveDefense: Artilleryx{artDeficit} AAAx{aaaDeficit} SAMx{samDeficit} activeArt={activeArt} activeAAA={activeAAA} activeSAM={activeSAM}");
    }

    private void TryBuildAirRefuelSupportOp(TeamId team, AIWorldSnapshot snapshot, List<AITacticalNeed> ops)
    {
        if (snapshot?.MyUnits == null)
            return;

        int lowFuelAircraft = 0;
        int criticalFuelAircraft = 0;
        int airFleet = 0;

        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked) continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.domain != Domain.Air) continue;
            if (UnitDataSatisfiesNeed(data, AINeedKind.AirTanker)) continue;

            airFleet++;
            int maxFuel = Mathf.Max(1, unit.GetMaxFuel());
            float fuelPct = unit.CurrentFuel * 100f / maxFuel;
            if (fuelPct <= AirRefuelLowFuelPct) lowFuelAircraft++;
            if (fuelPct <= AirRefuelCriticalFuelPct) criticalFuelAircraft++;
        }

        if (lowFuelAircraft <= 0)
            return;

        int desiredTankers = 1;
        if ((airFleet >= 8 && lowFuelAircraft >= 4) || criticalFuelAircraft >= 2)
            desiredTankers = 2;

        int activeTankers = CountActiveNeed(snapshot, AINeedKind.AirTanker);
        int tankerDeficit = Mathf.Max(0, desiredTankers - activeTankers);
        if (tankerDeficit <= 0)
        {
            Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] AirRefuelSupport coberto: lowFuel={lowFuelAircraft} critical={criticalFuelAircraft} activeTankers={activeTankers}/{desiredTankers}");
            return;
        }

        AITacticalNeed op = CreateOperation(team, AITacticalNeedType.AirRefuelSupport, 3, snapshot, snapshot.MyHQ != null ? snapshot.MyHQ.Sector : ConstructionSector.Base1);
        op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
        op.TargetCell = op.AnchorCell;
        op.AddSlots(AINeedKind.AirTanker, tankerDeficit);

        ops.Add(op);
        Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] AirRefuelSupport: lowFuel={lowFuelAircraft} critical={criticalFuelAircraft} airFleet={airFleet} activeTankers={activeTankers} deficit={tankerDeficit}");
    }
}
