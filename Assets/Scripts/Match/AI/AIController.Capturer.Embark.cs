using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Intercepção de embarque — capturador embarca em transporte no alcance
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideCapturerEmbarkAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!unit.TryGetUnitData(out UnitData data) || data?.roles == null || data.roles.Count == 0
            || !data.roles.Contains(UnitRole.Capturador)) return null;

        // Primary capturer: strict sector alignment (don't board a wrong-direction APC).
        // Secondary capturer (e.g. Assalto+Capturador): can board any APC that has no formal
        // passenger — it is acting as shuttle and will reorient to the passenger's objective.
        bool isPrimaryCapturador = data.roles[0] == UnitRole.Capturador;

        // Pass 1: sensor padrão — encontra transporters adjacentes (1h)
        var options = new List<PodeEmbarcarOption>();
        PodeEmbarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase,
            Mathf.Max(0, unit.RemainingMovementPoints), options);

        SectorObjective assigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        if (ShouldSkipCapturerEmbarkForShortWalk(unit, assigned, fromCell, "origem"))
            return null;

        PodeEmbarcarOption best = null;

        if (options.Count > 0)
        {
            if (assigned != null && plan != null)
            {
                foreach (PodeEmbarcarOption opt in options)
                {
                    SectorObjective tObj = ResolveAssignedTransportObjective(opt.transporterUnit, plan);
                    bool sectorMatch = tObj != null && tObj.Sector == assigned.Sector;
                    // Secondary capturer: also accepts APC with no formal passenger (shuttle mode)
                    bool freeTransport = !isPrimaryCapturador
                        && (tObj == null || ResolveAssignedPassengerUnit(tObj, snapshot.AITeam) == null);
                    if (sectorMatch || freeTransport) { best = opt; break; }
                }
            }
            else
            {
                // Capturador rogue: embark oportunista em transporter rogue (sem plano).
                foreach (PodeEmbarcarOption opt in options)
                {
                    SectorObjective tObj = plan != null
                        ? ResolveAssignedTransportObjective(opt.transporterUnit, plan) : null;
                    if (tObj == null) { best = opt; break; }
                }
            }
        }

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);

        if (best != null)
        {
            if (ShouldYieldEmbarkToNeedierCapturer(unit, best.transporterUnit, assigned, plan))
                return null;
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca → {best.transporterUnit.InstanceId} slot {best.transporterSlotIndex}");
            return BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, best.transporterUnit, best.transporterSlotIndex, paths);
        }

        // Pass 2: simula PodeEmbarcarSensor em cada hex candidato (ficar parado + hexes alcançáveis)
        if (paths == null || paths.Count == 0) return null;
        return TryBuildExtendedEmbarkBatch(unit, data, snapshot, plan, assigned, fromCell, paths);
    }

    // -------------------------------------------------------------------------
    // Pass 2: simula o sensor em cada hex candidato para achar embarque válido
    // -------------------------------------------------------------------------

    private PlayerAction TryBuildExtendedEmbarkBatch(
        UnitManager unit, UnitData unitData, AIWorldSnapshot snapshot, TeamObjectivePlan plan,
        SectorObjective assigned, Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        // Embark estendido só se o transporter tem plano no mesmo setor deste capturador.
        if (assigned == null || plan == null) return null;

        // movePaths: hexes alcançáveis reservando 1 MP para o custo de embarque.
        // fromCell (ficar parado) é verificado separadamente com MP completo.
        int mpForMove = Mathf.Max(0, unit.RemainingMovementPoints - 1);
        var movePaths = mpForMove > 0
            ? UnitMovementPathRules.CalcularCaminhosValidos(boardTilemap, unit, mpForMove, terrainDatabase)
            : null;

        var neighborBuf = new List<Vector3Int>(6);

        // 1) Ficar parado em fromCell — MP completo disponível para embarque
        UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, fromCell, neighborBuf);
        foreach (Vector3Int tCell in neighborBuf)
        {
            if (TryEmbarkFromHex(fromCell, null, unit.RemainingMovementPoints,
                    tCell, unit, unitData, plan, assigned, snapshot, out PlayerAction a))
                return a;
        }

        // 2) Hexes alcançáveis com MP reservado — simula sensor de cada um
        if (movePaths == null) return null;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        foreach (var kvp in movePaths)
        {
            Vector3Int hex = kvp.Key;
            if (hex == fromCell) continue;
            if (occupied.Contains(hex)) continue; // unidade não pode parar num hex ocupado

            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, hex, neighborBuf);
            foreach (Vector3Int tCell in neighborBuf)
            {
                // remainingMPAtHex = ao menos 1 (garantido pelo budget de movePaths)
                if (TryEmbarkFromHex(hex, kvp.Value, 1,
                        tCell, unit, unitData, plan, assigned, snapshot, out PlayerAction a))
                    return a;
            }
        }

        return null;
    }

    // Retorna true se outro capturer do mesmo setor está mais longe do objetivo e
    // ainda dentro do pickup range do APC — este capturer deve ceder a vaga.
    private bool ShouldYieldEmbarkToNeedierCapturer(
        UnitManager unit, UnitManager transporter, SectorObjective assigned, TeamObjectivePlan plan)
    {
        if (assigned == null || plan == null) return false;

        ConstructionManager objBuilding = FindCapturableInSector(assigned.Sector, unit.TeamId);
        if (objBuilding == null) return false;
        Vector3Int objCell = objBuilding.CurrentCellPosition; objCell.z = 0;

        Vector3Int myCell = unit.CurrentCellPosition; myCell.z = 0;
        float myDist = SectorManager.HexDistance(myCell, objCell);

        Vector3Int apcCell = transporter.CurrentCellPosition; apcCell.z = 0;

        foreach (SlotNeed slot in assigned.Slots)
        {
            if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
            if (slot.AssignedUnitId == unit.InstanceId) continue;

            UnitManager other = FindActiveUnit(slot.AssignedUnitId, unit.TeamId);
            if (other == null || other.HasActed || other.IsEmbarked || other.IsDead) continue;

            Vector3Int otherCell = other.CurrentCellPosition; otherCell.z = 0;
            float otherDist = SectorManager.HexDistance(otherCell, objCell);
            if (otherDist <= myDist) continue; // não está mais longe

            float otherDistToAPC = SectorManager.HexDistance(otherCell, apcCell);
            if (otherDistToAPC > ShuttlePickupRange + 0.5f) continue; // fora do alcance do APC

            Debug.Log($"{TL("Capturador")} {unit.InstanceId} cede embarque para {other.InstanceId} ({otherDist:F0}h > {myDist:F0}h ao objetivo)");
            return true;
        }

        return false;
    }

    // Verifica se há um transporter válido em tCell acessível a partir de fromHex,
    // com MP restante suficiente para embarcar. Retorna true e preenche action se válido.
    private bool TryEmbarkFromHex(
        Vector3Int fromHex, List<Vector3Int> pathToHex, int remainingMPAtHex,
        Vector3Int tCell, UnitManager unit, UnitData unitData,
        TeamObjectivePlan plan, SectorObjective assigned,
        AIWorldSnapshot snapshot, out PlayerAction action)
    {
        action = null;

        UnitManager transporter = UnitOccupancyRules.GetUnitAtCell(boardTilemap, tCell, unit);
        if (transporter == null || transporter.TeamId != unit.TeamId) return false;
        if (transporter.IsDead || transporter.IsEmbarked) return false;
        if (!transporter.TryGetUnitData(out UnitData tData) || !tData.isTransporter) return false;

        // Primary capturer: APC must be assigned to the same sector.
        // Secondary capturer: also accepts an APC with no formal passenger (shuttle mode).
        SectorObjective tObj = ResolveAssignedTransportObjective(transporter, plan);
        bool isPrimary = unitData.roles != null && unitData.roles.Count > 0
            && unitData.roles[0] == UnitRole.Capturador;
        bool sameSector = tObj != null && tObj.Sector == assigned.Sector;
        bool shuttleFree = !isPrimary
            && (tObj == null || ResolveAssignedPassengerUnit(tObj, unit.TeamId) == null);
        if (!sameSector && !shuttleFree) return false;
        if (ShouldSkipCapturerEmbarkForShortWalk(unit, assigned, fromHex, "hex embarque"))
            return false;

        // Transporter deve estar dentro do pickup range da posição original
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        if (SectorManager.HexDistance(fromCell, tCell) > ShuttlePickupRange + 0.5f) return false;

        // Verifica custo de embarque vs MP restante no hex intermediário
        if (!UnitMovementPathRules.TryGetEnterCellCost(
                boardTilemap, unit, tCell, terrainDatabase, false, out int embarkCost))
            embarkCost = 1;
        embarkCost = Mathf.Max(1, embarkCost);
        if (remainingMPAtHex < embarkCost) return false;

        int slotIdx = FindFittingSlotIndex(transporter, tData, unitData);
        if (slotIdx < 0) return false;

        if (ShouldYieldEmbarkToNeedierCapturer(unit, transporter, assigned, plan))
            return false;

        tCell.z = 0;
        var pathsForBatch = pathToHex != null
            ? new Dictionary<Vector3Int, List<Vector3Int>> { [tCell] = pathToHex }
            : null;

        Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca (ext {(int)SectorManager.HexDistance(fromCell, tCell)}h) → {transporter.InstanceId} slot {slotIdx} via {fromHex}");
        action = BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, transporter, slotIdx, pathsForBatch);
        return true;
    }

    private bool ShouldSkipCapturerEmbarkForShortWalk(
        UnitManager unit,
        SectorObjective assigned,
        Vector3Int candidateCell,
        string context)
    {
        if (unit == null || assigned == null)
            return false;

        ConstructionManager objBuilding = FindCapturableInSector(assigned.Sector, unit.TeamId);
        if (objBuilding == null)
            return false;

        Vector3Int objCell = objBuilding.CurrentCellPosition; objCell.z = 0;
        candidateCell.z = 0;
        float objectiveDist = SectorManager.HexDistance(candidateCell, objCell);
        if (objectiveDist >= MinDistanceForTransportSlot)
            return false;

        Debug.Log($"{TL("Capturador")} {unit.InstanceId} ignora embarque ({context} {objectiveDist:F0}h<{MinDistanceForTransportSlot}h de {assigned.Sector})");
        return true;
    }
}
