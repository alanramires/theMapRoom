using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    private const int CapturerRideOperationalTurns = 2;

    // -------------------------------------------------------------------------
    // Intercepção de embarque — capturador embarca em transporte no alcance
    // -------------------------------------------------------------------------

    // True se a unidade esta atribuida a um rally assembly ainda ativo (montando massa, nao GoGreen)
    // E ja esta dentro do raio de montagem do ponto de rally — nesse caso deve SEGURAR, nao embarcar.
    private bool ShouldHoldRallyAssemblyInsteadOfEmbark(UnitManager unit, SectorObjective assigned)
    {
        if (!IsActiveRallyAssemblyObjective(assigned))
            return false;
        if (!TryGetRallyAnchorCell(assigned.Sector, out Vector3Int anchor))
            return false;
        Vector3Int cell = unit.CurrentCellPosition; cell.z = 0;
        return SectorManager.HexDistance(anchor, cell) <= RallyAssemblyForceRadius;
    }

    // Celula do ponto de rally do setor (anchor usado na contagem de presenca do GoGreen).
    private bool TryGetRallyAnchorCell(ConstructionSector sector, out Vector3Int anchor)
    {
        anchor = default;
        if (sector == ConstructionSector.None || ConstructionManager.AllActive == null)
            return false;
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally != null && rally.IsRallyPoint && rally.Sector == sector)
            {
                anchor = rally.CurrentCellPosition; anchor.z = 0;
                return true;
            }
        }
        return false;
    }

    private PlayerAction TryDecideCapturerEmbarkAction(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (!unit.TryGetUnitData(out UnitData data)
            || !UnitRoleCompatibility.CanSatisfy(data, UnitRole.Capturador)) return null;

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

        // Capturador montando massa num rally assembly AINDA ativo (nao GoGreen/Expired) e ja
        // DENTRO do raio de montagem NAO deve embarcar — sair leva a massa embora e adia o GoGreen
        // (presenca conta, ver EvaluateRallyReadiness). Se estiver LONGE do rally, carona pra chegar
        // continua valendo. (Caso: 63 segurando Foxtrot em Assembling pegou carona no APC 19.)
        if (ShouldHoldRallyAssemblyInsteadOfEmbark(unit, assigned))
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} NAO embarca: montando massa no rally "
                + $"{assigned.Sector} (state={assigned.RallyState}) dentro do raio {RallyAssemblyForceRadius}h");
            return null;
        }

        // Gate único de necessidade. Não escolhe transporte, vaga ou LZ e não
        // materializa ação; apenas decide se vale iniciar os scans de embarque.
        QueroCaronaResult rideNeed =
            EvaluateCapturerRideNeed(unit, assigned);
        if (!rideNeed.wantsRide)
            return null;

        // Pass 1: sensor padrão — encontra transporters adjacentes (1h).
        // Só varre depois de o passageiro confirmar que precisa de carona.
        var options = new List<PodeEmbarcarOption>();
        PodeEmbarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase,
            Mathf.Max(0, unit.RemainingMovementPoints), options);

        // Não embarcar em transporters ainda no aeroporto/fábrica — espera sair primeiro.
        options.RemoveAll(opt =>
        {
            if (opt?.transporterUnit == null) return false;
            Vector3Int tc = opt.transporterUnit.CurrentCellPosition; tc.z = 0;
            return IsTeamProductionBuilding(tc, unit.TeamId);
        });

        PodeEmbarcarOption best = null;
        int bestPriority = int.MaxValue;
        float bestDistance = float.MaxValue;

        if (options.Count > 0)
        {
            foreach (PodeEmbarcarOption opt in options)
            {
                if (!TryGetCapturerEmbarkPreference(unit, assigned, opt, plan, snapshot, snapshot.AITeam,
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

        // Combate/captura imediatos continuam sendo política do controller,
        // mesmo quando a estimativa confirmou necessidade de transporte.
        if (assigned == null && ShouldRogueCapturerFightBeforeTransport(unit, snapshot, fromCell, paths))
            return null;

        if (best != null && bestPriority == 0)
        {
            if (ShouldYieldEmbarkToNeedierCapturer(
                    unit, best.transporterUnit, assigned, plan))
                return null;
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca → {best.transporterUnit.InstanceId} slot {best.transporterSlotIndex}");
            return BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, best.transporterUnit, best.transporterSlotIndex, paths);
        }

        PlayerAction formalExtendedEmbark =
            TryBuildExtendedEmbarkBatch(
                unit, data, snapshot, plan, assigned, rideNeed,
                fromCell, paths, requireFormalPassenger: true);
        if (formalExtendedEmbark != null) return formalExtendedEmbark;

        if (best != null)
        {
            if (ShouldYieldEmbarkToNeedierCapturer(
                    unit, best.transporterUnit, assigned, plan))
                return null;
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} embarca fallback p{bestPriority} â†’ {best.transporterUnit.InstanceId} slot {best.transporterSlotIndex}");
            return BuildEmbarcarBatch(unit, snapshot.AITeam, fromCell, best.transporterUnit, best.transporterSlotIndex, paths);
        }

        PlayerAction extendedEmbark =
            TryBuildExtendedEmbarkBatch(
                unit, data, snapshot, plan, assigned, rideNeed,
                fromCell, paths, requireSectorMatch: true)
            ?? TryBuildExtendedEmbarkBatch(
                unit, data, snapshot, plan, assigned, rideNeed,
                fromCell, paths, requireSectorMatch: false)
            ?? TryBuildExtendedEmbarkBatch(
                unit, data, snapshot, plan, assigned, rideNeed,
                fromCell, paths, requireSectorMatch: false,
                allowOverflow: true);
        if (extendedEmbark != null) return extendedEmbark;

        // Rogue capturer: extended embark failed — move toward nearest rogue transporter so
        // it enters embark range next turn. Only applies when there is no sector assignment
        // (rogues march to enemy HQ; boarding any rogue transport accelerates the push).
        if (assigned == null)
        {
            UnitManager rogueTransport = FindNearestRogueTransporter(
                unit, data, plan, snapshot, rideNeed);
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

    private QueroCaronaResult EvaluateCapturerRideNeed(
        UnitManager unit,
        SectorObjective assigned)
    {
        QueroCaronaContext context = assigned != null
            ? QueroCaronaContext.ComPlano
            : QueroCaronaContext.RogueOuRebelde;
        ConstructionSector sector = assigned != null
            ? assigned.Sector
            : ConstructionSector.None;
        QueroCaronaResult result = QueroCaronaService.Evaluate(
            new QueroCaronaRequest
            {
                unit = unit,
                map = boardTilemap,
                terrainDatabase = terrainDatabase,
                context = context,
                plannedSector = sector,
                operationalTurns = CapturerRideOperationalTurns,
                emulateUnderRepairFromUnitData = false
            });

        string target = result.evaluatedConstruction != null
            ? $"{result.evaluatedConstruction.name}@{result.evaluatedTarget}"
            : result.evaluatedTarget.ToString();
        string routeCost = result.routeCost == int.MaxValue
            ? "-"
            : result.routeCost.ToString();
        Debug.Log(
            $"{TL("Capturador")} {unit.InstanceId} QueroCarona=" +
            $"{(result.wantsRide ? "SIM" : "NAO")} " +
            $"contexto={context} setor={sector} " +
            $"emergencia={result.isEmergency} " +
            $"envelope={result.reach} custo={routeCost} " +
            $"alvo={target} motivo={result.reason}");
        return result;
    }

    private bool ShouldRogueCapturerFightBeforeTransport(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        Vector3Int pressureTarget = snapshot.EnemyHQ != null
            ? snapshot.EnemyHQ.CurrentCellPosition
            : fromCell;
        pressureTarget.z = 0;

        if (TryFindUnreservedOpportunisticCapture(unit, snapshot.AITeam, paths, occupied, pressureTarget, out Vector3Int captureCell, "rogue transporte"))
        {
            Debug.Log($"{TL("Capturador")} {unit.InstanceId} rogue ignora transporte: captura/pressao disponivel @ {captureCell}");
            return true;
        }

        if (HasAttackTargetAtCurrentPos(unit))
        {
            var stayTargets = new List<PodeMirarTargetOption>();
            PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                SensorMovementMode.MoveuParado, stayTargets);
            UnitManager stayBest = PickBestRogueTarget(stayTargets, snapshot.AITeam, unit, fromCell, false, out _);
            if (stayBest != null)
            {
                Debug.Log($"{TL("Capturador")} {unit.InstanceId} rogue ignora transporte: alvo atual {stayBest.UnitDisplayName}#{stayBest.InstanceId}");
                return true;
            }
        }

        var targets = new List<PodeMirarTargetOption>();
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (occupied.Contains(cell))
                continue;

            targets.Clear();
            PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                SensorMovementMode.MoveuAndando, targets, fromCell: cell);
            UnitManager bestTarget = PickBestRogueTarget(targets, snapshot.AITeam, unit, cell, false, out _);
            if (bestTarget == null)
                continue;

            Debug.Log($"{TL("Capturador")} {unit.InstanceId} rogue ignora transporte: ataque disponivel {bestTarget.UnitDisplayName}#{bestTarget.InstanceId} via {cell}");
            return true;
        }

        return false;
    }

}
