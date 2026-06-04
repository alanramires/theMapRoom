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

        // capturerAssigned: slot de capturador exclusivo — usado para o skip de embarque.
        // Rogues (sem slot de capturador) recebem null e nunca pulam o embarque por
        // "estar perto do objetivo", pois seu destino real é o HQ inimigo, não o setor.
        SectorObjective capturerAssigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;

        // assigned: ampliado para multi-role (e.g. Assalto+Capturador) que não têm slot de
        // capturador mas têm atribuição em outro role — usado para sector match do APC.
        SectorObjective assigned = capturerAssigned;
        if (assigned == null && plan != null)
            assigned = ResolveAnyAssignedObjective(unit, plan);

        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        if (ShouldSkipCapturerEmbarkForShortWalk(unit, capturerAssigned, fromCell, "origem"))
            return null;

        // Não embarcar em transporters ainda no aeroporto/fábrica — espera sair primeiro.
        options.RemoveAll(opt =>
        {
            if (opt?.transporterUnit == null) return false;
            Vector3Int tc = opt.transporterUnit.CurrentCellPosition; tc.z = 0;
            return IsTeamProductionBuilding(tc, unit.TeamId)
                && !IsAirTransporter(opt.transporterUnit);
        });

        PodeEmbarcarOption best = null;
        int bestPriority = int.MaxValue;
        float bestDistance = float.MaxValue;

        if (options.Count > 0)
        {
            foreach (PodeEmbarcarOption opt in options)
            {
                if (!TryGetCapturerEmbarkPreference(unit, assigned, opt, plan, snapshot.AITeam,
                        out int priority, out float distance))
                    continue;

                if (priority < bestPriority
                    || (priority == bestPriority && distance < bestDistance))
                {
                    best = opt;
                    bestPriority = priority;
                    bestDistance = distance;
                }
            }
        }

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);

        if (best != null && bestPriority == 0)
        {
            if (ShouldYieldEmbarkToNeedierCapturer(unit, best.transporterUnit, assigned, plan))
                return null;
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca → {best.transporterUnit.InstanceId} slot {best.transporterSlotIndex}");
            return BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, best.transporterUnit, best.transporterSlotIndex, paths);
        }

        // Pass 2: simula PodeEmbarcarSensor em cada hex candidato (ficar parado + hexes alcançáveis).
        // Pass 2a: exige transporter formalmente pareado com este passageiro.
        // Pass 2b: exige transporter do mesmo setor do plano.
        // Pass 2c: aceita transporter livre (sem passageiro formal).
        // Pass 3: overflow — embarca em qualquer transporter com slot físico livre (último recurso).
        if (paths == null || paths.Count == 0)
        {
            Debug.Log(BuildCapturerEmbarkScanDebug(unit, data, assigned, plan, snapshot,
                fromCell, options.Count, best, bestPriority, "sem paths"));
            return null;
        }
        PlayerAction formalExtendedEmbark =
            TryBuildExtendedEmbarkBatch(unit, data, snapshot, plan, assigned, fromCell, paths, requireFormalPassenger: true);
        if (formalExtendedEmbark != null) return formalExtendedEmbark;

        if (best != null)
        {
            if (ShouldYieldEmbarkToNeedierCapturer(unit, best.transporterUnit, assigned, plan))
                return null;
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca fallback p{bestPriority} â†’ {best.transporterUnit.InstanceId} slot {best.transporterSlotIndex}");
            return BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, best.transporterUnit, best.transporterSlotIndex, paths);
        }

        PlayerAction extendedEmbark =
            TryBuildExtendedEmbarkBatch(unit, data, snapshot, plan, assigned, fromCell, paths, requireSectorMatch: true)
            ?? TryBuildExtendedEmbarkBatch(unit, data, snapshot, plan, assigned, fromCell, paths, requireSectorMatch: false)
            ?? TryBuildExtendedEmbarkBatch(unit, data, snapshot, plan, assigned, fromCell, paths, requireSectorMatch: false, allowOverflow: true);
        if (extendedEmbark != null) return extendedEmbark;

        // Rogue capturer: extended embark failed — move toward nearest rogue transporter so
        // it enters embark range next turn. Only applies when there is no sector assignment
        // (rogues march to enemy HQ; boarding any rogue transport accelerates the push).
        if (assigned == null)
        {
            UnitManager rogueTransport = FindNearestRogueTransporter(unit, data, plan, snapshot);
            if (rogueTransport != null)
            {
                Vector3Int tCell = rogueTransport.CurrentCellPosition; tCell.z = 0;
                HashSet<Vector3Int> occ = BuildOccupied(unit);
                Vector3Int moveTarget = FindTransportMove(unit, fromCell, tCell, paths, occ, snapshot.AITeam);
                if (moveTarget != fromCell)
                {
                    Debug.Log($"{TL("Capturador")} {unit.InstanceId} rogue — avança para transporte rogue {rogueTransport.InstanceId}@{tCell} via {moveTarget}");
                    return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
                }
            }
        }
        Debug.Log(BuildCapturerEmbarkScanDebug(unit, data, assigned, plan, snapshot,
            fromCell, options.Count, best, bestPriority, "sem embarque valido"));
        return null;
    }

}
