using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private string BuildCapturerEmbarkScanDebug(
        UnitManager unit,
        UnitData unitData,
        SectorObjective assigned,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        int adjacentOptions,
        PodeEmbarcarOption best,
        int bestPriority,
        string reason)
    {
        var sb = new System.Text.StringBuilder();
        string sector = assigned != null ? assigned.Sector.ToString() : "rogue";
        sb.AppendLine($"{TL("Capturador")} {unit.InstanceId} embarque scan: assigned={sector} reason={reason} adjacentOptions={adjacentOptions} best={(best?.transporterUnit != null ? best.transporterUnit.InstanceId.ToString() : "-")} p={(bestPriority == int.MaxValue ? "-" : bestPriority.ToString())}");

        int listed = 0;
        foreach (UnitManager t in UnitManager.AllActive)
        {
            if (t == null || t == unit || t.TeamId != unit.TeamId || t.IsDead || t.IsEmbarked) continue;
            if (!t.TryGetUnitData(out UnitData tData) || tData == null || !tData.isTransporter) continue;

            Vector3Int tCell = t.CurrentCellPosition; tCell.z = 0;
            float dist = SectorManager.HexDistance(fromCell, tCell);
            if (dist > 8f) continue;

            SectorObjective tObj = plan != null ? ResolveAssignedTransportObjective(t, plan) : null;
            UnitManager formalPassenger = tObj != null ? ResolveAssignedPassengerUnit(tObj, unit.TeamId) : null;
            bool sameSector = assigned != null && tObj != null && tObj.Sector == assigned.Sector;
            bool compatibleSector = assigned != null && tObj != null
                && AreEmbarkSectorsCompatible(assigned.Sector, tObj.Sector);
            bool production = IsTeamProductionBuilding(tCell, unit.TeamId);
            int slot = FindFittingSlotIndex(t, tData, unit, unitData);
            string cargo = HasTransportCargo(t) ? "cargo" : "empty";

            sb.AppendLine($"  heli/trans {t.InstanceId}@{tCell} dist={dist:F0} sector={(tObj != null ? tObj.Sector.ToString() : "free")} formal={(formalPassenger != null ? formalPassenger.InstanceId.ToString() : "-")} slot={slot} same={sameSector} compat={compatibleSector} prod={production} acted={t.HasActed} repair={t.IsUnderRepair} {cargo}");
            listed++;
        }

        if (listed == 0)
            sb.AppendLine("  nenhum transporte aliado <=8h");

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Pass 2: simula o sensor em cada hex candidato para achar embarque válido
    // -------------------------------------------------------------------------


    private bool TryGetCapturerEmbarkPreference(
        UnitManager unit,
        SectorObjective assigned,
        PodeEmbarcarOption option,
        TeamObjectivePlan plan,
        TeamId aiTeam,
        out int priority,
        out float distance)
    {
        priority = int.MaxValue;
        distance = float.MaxValue;

        UnitManager transporter = option != null ? option.transporterUnit : null;
        if (unit == null || transporter == null || transporter.IsUnderRepair)
            return false;

        SectorObjective transporterObjective = plan != null
            ? ResolveAssignedTransportObjective(transporter, plan)
            : null;

        Vector3Int unitCell = unit.CurrentCellPosition; unitCell.z = 0;
        Vector3Int transporterCell = transporter.CurrentCellPosition; transporterCell.z = 0;
        distance = SectorManager.HexDistance(unitCell, transporterCell);

        if (assigned == null)
        {
            if (transporterObjective != null
                && !CanRogueUseAssignedTransporter(unit, transporter, transporterObjective, aiTeam))
                return false;

            priority = transporterObjective == null ? 0 : 1;
            return true;
        }

        bool sameSector = transporterObjective != null && transporterObjective.Sector == assigned.Sector;
        bool compatibleSector = transporterObjective != null
            && AreEmbarkSectorsCompatible(assigned.Sector, transporterObjective.Sector);
        UnitManager formalPassenger = transporterObjective != null
            ? ResolveAssignedPassengerUnit(transporterObjective, aiTeam)
            : null;
        bool formalMatch = sameSector && formalPassenger == unit;
        bool freeTransport = transporterObjective == null;
        bool compatibleFreeTransport = transporterObjective != null
            && compatibleSector
            && formalPassenger == null;

        if (formalMatch)
            priority = 0;
        else if (sameSector)
            priority = 1;
        else if (compatibleFreeTransport)
            priority = 2;
        else if (freeTransport)
            priority = 3;
        else
            return false;

        return true;
    }


    private static bool AreEmbarkSectorsCompatible(ConstructionSector assignedSector, ConstructionSector transportSector)
    {
        if (assignedSector == transportSector)
            return true;

        if (SectorManager.TryGetSectorInfo(assignedSector, out SectorManager.SectorInfo assignedInfo)
            && assignedInfo != null
            && (assignedInfo.ClosestNeighbor1 == transportSector || assignedInfo.ClosestNeighbor2 == transportSector))
            return true;

        if (SectorManager.TryGetSectorInfo(transportSector, out SectorManager.SectorInfo transportInfo)
            && transportInfo != null
            && (transportInfo.ClosestNeighbor1 == assignedSector || transportInfo.ClosestNeighbor2 == assignedSector))
            return true;

        return false;
    }


    private bool ShouldSkipCapturerEmbarkForShortWalk(
        UnitManager unit,
        SectorObjective assigned,
        Vector3Int candidateCell,
        string context)
    {
        if (unit == null)
            return false;

        // Rogues marcham ao HQ inimigo, não ao capturável mais próximo.
        // A referência de distância seria inválida — sempre permite embarque.
        if (assigned == null)
            return false;

        candidateCell.z = 0;

        ConstructionManager objBuilding = FindCapturableInSector(assigned.Sector, unit.TeamId);

        if (objBuilding == null)
            return false;

        Vector3Int objCell = objBuilding.CurrentCellPosition; objCell.z = 0;
        int effectiveThreshold = GetEffectiveTransportThreshold(unit.TeamId);
        // Slow units benefit from transport from farther away (+2h per MP below 3).
        int baseMP = unit.MaxMovementPoints;
        if (baseMP < 3) effectiveThreshold += (3 - baseMP) * 2;
        int terrainCost = TerrainCostToCell(unit, candidateCell, objCell, effectiveThreshold);
        if (terrainCost >= effectiveThreshold)
            return false;

        string sectorLabel = assigned != null ? assigned.Sector.ToString() : objBuilding.Sector.ToString();
        Debug.Log($"{TL("Capturador")} {unit.InstanceId} ignora embarque ({context} terreno={terrainCost}<{effectiveThreshold}h de {sectorLabel})");
        return true;
    }


    private ConstructionManager FindNearestCapturableForUnit(UnitManager unit, Vector3Int fromCell)
    {
        ConstructionManager best = null;
        float bestDist = float.MaxValue;
        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (!c.IsCapturable || c.CapturePointsMax <= 0) continue;
            if (c.TeamId == unit.TeamId && c.CurrentCapturePoints >= c.CapturePointsMax) continue;
            Vector3Int tc = c.CurrentCellPosition; tc.z = 0;
            float dist = SectorManager.HexDistance(fromCell, tc);
            if (dist < bestDist) { bestDist = dist; best = c; }
        }
        return best;
    }

    // Finds the nearest rogue transporter (no formal plan assignment) that has a fitting
    // slot for this capturer. Used as a fallback move target when extended embark fails.

    private UnitManager FindNearestRogueTransporter(UnitManager capturer, UnitData capturerData,
        TeamObjectivePlan plan, AIWorldSnapshot snapshot)
    {
        UnitManager best = null;
        float bestDist = float.MaxValue;
        Vector3Int fromCell = capturer.CurrentCellPosition; fromCell.z = 0;
        int pickupThreshold = Mathf.Max(4, GetEffectiveTransportThreshold(capturer.TeamId) / 2 + 1);

        foreach (UnitManager t in UnitManager.AllActive)
        {
            if (t == capturer) continue;
            if (t.TeamId != capturer.TeamId || t.IsDead || t.IsEmbarked || t.IsUnderRepair) continue;
            if (!t.TryGetUnitData(out UnitData tData) || !tData.isTransporter) continue;
            SectorObjective tObj = plan != null ? ResolveAssignedTransportObjective(t, plan) : null;
            if (tObj != null) continue;
            if (FindFittingSlotIndex(t, tData, capturer, capturerData) < 0) continue;
            Vector3Int tCell = t.CurrentCellPosition; tCell.z = 0;
            float dist = SectorManager.HexDistance(fromCell, tCell);
            if (dist > pickupThreshold) continue;
            if (dist < bestDist) { bestDist = dist; best = t; }
        }

        return best;
    }

}
