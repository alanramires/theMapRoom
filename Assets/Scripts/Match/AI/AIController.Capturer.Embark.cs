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
            || data.roles[0] != UnitRole.Capturador) return null;

        // Pass 1: sensor padrão — encontra transporters adjacentes (1h)
        var options = new List<PodeEmbarcarOption>();
        PodeEmbarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase,
            Mathf.Max(0, unit.RemainingMovementPoints), options);

        SectorObjective assigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;
        PodeEmbarcarOption best = null;

        if (options.Count > 0)
        {
            if (assigned != null && plan != null)
            {
                // Capturador designado: só embarca no transporter formalmente do mesmo setor.
                // Não cai em qualquer APC oportunista — o APC rogue não tem destino correto.
                foreach (PodeEmbarcarOption opt in options)
                {
                    SectorObjective tObj = ResolveAssignedTransportObjective(opt.transporterUnit, plan);
                    if (tObj != null && tObj.Sector == assigned.Sector) { best = opt; break; }
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

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);

        if (best != null)
        {
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

        foreach (var kvp in movePaths)
        {
            Vector3Int hex = kvp.Key;
            if (hex == fromCell) continue;

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

        // Só embarca se o transporter está designado ao mesmo setor
        SectorObjective tObj = ResolveAssignedTransportObjective(transporter, plan);
        if (tObj == null || tObj.Sector != assigned.Sector) return false;

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

        tCell.z = 0;
        var pathsForBatch = pathToHex != null
            ? new Dictionary<Vector3Int, List<Vector3Int>> { [tCell] = pathToHex }
            : null;

        Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca (ext {(int)SectorManager.HexDistance(fromCell, tCell)}h) → {transporter.InstanceId} slot {slotIdx} via {fromHex}");
        action = BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, transporter, slotIdx, pathsForBatch);
        return true;
    }
}
