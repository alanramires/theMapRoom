using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private PlayerAction TryBuildExtendedEmbarkBatch(
        UnitManager unit, UnitData unitData, AIWorldSnapshot snapshot, TeamObjectivePlan plan,
        SectorObjective assigned, Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        bool requireSectorMatch = false, bool allowOverflow = false, bool requireFormalPassenger = false)
    {
        // Usa os paths completos e calcula a sobra real por caminho. Reservar 1 PM
        // antecipadamente quebra casos em que o passageiro anda 2 casas e embarca na 3a.
        var movePaths = paths;

        var neighborBuf = new List<Vector3Int>(6);
        var pickupBuf = new List<Vector3Int>(12);
        var seenPickupCells = new HashSet<Vector3Int>();

        // 1) Ficar parado em fromCell — MP completo disponível para embarque
        CollectEmbarkTargetCells(fromCell, unit, neighborBuf, pickupBuf, seenPickupCells);
        foreach (Vector3Int tCell in pickupBuf)
        {
            if (TryEmbarkFromHex(fromCell, null, unit.RemainingMovementPoints,
                    tCell, unit, unitData, plan, assigned, snapshot, out PlayerAction a, requireSectorMatch, allowOverflow, requireFormalPassenger))
                return a;
        }

        // 2) Hexes alcançáveis — simula sensor de cada um com a sobra real de PM
        if (movePaths == null) return null;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        PlayerAction directTransporterEmbark = TryBuildDirectTransporterExtendedEmbarkBatch(
            unit, unitData, snapshot, plan, assigned, fromCell, movePaths, occupied,
            neighborBuf, requireSectorMatch, allowOverflow, requireFormalPassenger);
        if (directTransporterEmbark != null)
            return directTransporterEmbark;

        foreach (var kvp in movePaths)
        {
            Vector3Int hex = kvp.Key;
            if (hex == fromCell) continue;
            if (occupied.Contains(hex)) continue; // unidade não pode parar num hex ocupado

            CollectEmbarkTargetCells(hex, unit, neighborBuf, pickupBuf, seenPickupCells);
            foreach (Vector3Int tCell in pickupBuf)
            {
                int remainingMPAtHex = CalculateRemainingMovementAfterPath(unit, kvp.Value);
                if (remainingMPAtHex <= 0) continue;

                if (TryEmbarkFromHex(hex, kvp.Value, remainingMPAtHex,
                        tCell, unit, unitData, plan, assigned, snapshot, out PlayerAction a, requireSectorMatch, allowOverflow, requireFormalPassenger))
                    return a;
            }
        }

        return null;
    }


    private PlayerAction TryBuildDirectTransporterExtendedEmbarkBatch(
        UnitManager unit,
        UnitData unitData,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        SectorObjective assigned,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> movePaths,
        HashSet<Vector3Int> occupied,
        List<Vector3Int> neighborBuf,
        bool requireSectorMatch,
        bool allowOverflow,
        bool requireFormalPassenger)
    {
        foreach (UnitManager transporter in UnitManager.AllActive)
        {
            if (transporter == null || transporter == unit || transporter.SlotIndex != unit.SlotIndex)
                continue;
            if (transporter.IsDead || transporter.IsEmbarked || transporter.IsUnderRepair)
                continue;
            if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null || !transporterData.isTransporter)
                continue;
            if (FindFittingSlotIndex(transporter, transporterData, unit, unitData) < 0)
                continue;

            Vector3Int tCell = transporter.CurrentCellPosition;
            tCell.z = 0;
            float distFromOrigin = SectorManager.HexDistance(fromCell, tCell);
            if (distFromOrigin > unit.RemainingMovementPoints + 0.5f)
                continue;

            neighborBuf.Clear();
            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, tCell, neighborBuf);
            neighborBuf.Sort((a, b) =>
            {
                Vector3Int ca = a; ca.z = 0;
                Vector3Int cb = b; cb.z = 0;
                int cmp = SectorManager.HexDistance(fromCell, ca).CompareTo(SectorManager.HexDistance(fromCell, cb));
                if (cmp != 0) return cmp;
                return ca.GetHashCode().CompareTo(cb.GetHashCode());
            });
            foreach (Vector3Int rawStop in neighborBuf)
            {
                Vector3Int stopCell = rawStop;
                stopCell.z = 0;

                if (stopCell == fromCell)
                {
                    if (TryEmbarkFromHex(fromCell, null, unit.RemainingMovementPoints,
                            tCell, unit, unitData, plan, assigned, snapshot, out PlayerAction action,
                            requireSectorMatch, allowOverflow, requireFormalPassenger, transporter))
                        return action;
                    continue;
                }

                if (!TryGetEmbarkStopPath(unit, fromCell, stopCell, movePaths, out List<Vector3Int> pathToStop))
                    continue;

                int remainingMPAtStop = CalculateRemainingMovementAfterPath(unit, pathToStop);
                if (remainingMPAtStop <= 0)
                    continue;

                if (TryEmbarkFromHex(stopCell, pathToStop, remainingMPAtStop,
                        tCell, unit, unitData, plan, assigned, snapshot, out PlayerAction movedAction,
                        requireSectorMatch, allowOverflow, requireFormalPassenger, transporter))
                    return movedAction;
            }
        }

        return null;
    }


}
