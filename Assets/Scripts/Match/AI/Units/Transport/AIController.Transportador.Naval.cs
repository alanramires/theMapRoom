using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Transporte NAVAL (navio de desembarque).
    //
    // Por que existe um caminho proprio: o fluxo terrestre rota o transportador ATE o
    // objetivo. Isso nao funciona no mar — o objetivo fica em terra, o mapa de custo
    // reverso e calculado com as regras do navio, e nasce vazio. Sem gradiente, o navio
    // oscila na costa sem nunca concluir a entrega.
    //
    // A correcao nao e um alvo diferente, e uma PERGUNTA diferente. O navio nunca quer
    // chegar ao objetivo; ele quer chegar ao PONTO DE ENCONTRO com a terra:
    //
    //   entrega  -> celula de agua de onde o desembarque e valido, escolhendo a que
    //               deixa o passageiro mais perto do objetivo;
    //   coleta   -> celula de agua onde o navio pode RECEBER passageiro (praia/porto,
    //               conforme a propria ficha dele), mais perto de quem espera embarque.
    //
    // Resolvido o ponto de encontro, ele vira o alvo de rota — e agora e alcancavel,
    // entao FindTransportMove e o courier voltam a funcionar sem alteracao.
    // -------------------------------------------------------------------------

    // Quantos hexes de folga aceitamos entre o ponto de pouso do passageiro e o objetivo
    // ao comparar praias. Nao e criterio de largar (isso quem decide e o sensor): e so o
    // desempate entre pontos de desembarque validos.
    private const int NavalLandingScoreHorizon = 60;

    private PlayerAction TryDecideNavalTransportAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        if (unit == null || snapshot == null) return null;

        bool hasCargo = HasTransportCargo(unit);
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths = UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap, unit, unit.RemainingMovementPoints, terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (!hasCargo)
            return null;
        return DecideNavalDeliveryAction(
            unit, snapshot, plan, fromCell, paths, occupied);
    }

    // -------------------------------------------------------------------------
    // Entrega: achar a praia e largar a tropa
    // -------------------------------------------------------------------------

    private PlayerAction DecideNavalDeliveryAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        // EVAC: com ferido a bordo o destino nao e o objetivo de combate do passageiro
        // (que, ao entrar em reparo, ate perdeu o slot do plano) e sim o ponto de reparo
        // mais proximo — o mesmo criterio do courier terrestre/aereo. Sem este ramo o
        // navio largava o ferido na praia mais perto de um alvo que nao existe mais.
        UnitManager navalEvacuee = FindEmbarkedPatient(unit);
        if (navalEvacuee != null)
            return DecideNavalEvacDeliveryAction(unit, navalEvacuee, snapshot, fromCell, paths, occupied);

        // Qualquer aeronave que ja saiu de UnderRepair fica autorizada a
        // decolar, mas nao dita uma rota de entrega ao porta-avioes. O navio
        // executa a propria politica e, se ela resultar em movimento/espera
        // com LZ valida, libera as aeronaves prontas no destino escolhido.
        if (TryBuildReadyAircraftReleaseAfterNavalPosture(
                unit,
                snapshot,
                plan,
                fromCell,
                out PlayerAction aircraftRelease))
        {
            return aircraftRelease;
        }

        Vector3Int objective = ResolveNavalDeliveryObjective(unit, snapshot, plan);
        List<UnitManager> carriedPassengers = CollectPassengers(unit);
        if (TryBuildBestCourierDisembarkAction(
                unit,
                carriedPassengers,
                plan,
                snapshot,
                Vector3Int.zero,
                paths,
                TransportDropOffRange,
                false,
                "Naval",
                out PlayerAction bestDropAction))
            return bestDropAction;

        // A praia de coleta tambem permite desembarque, mas nao conclui a entrega.
        // Como no courier terrestre/aereo, a carga so desce perto do objetivo dela.
        List<PodeDesembarcarOption> here = SimulateDisembarkFromCell(unit, fromCell);
        if (here.Count > 0)
        {
            List<PodeDesembarcarOption> passengers =
                SelectNavalDisembarkOrders(here, plan, snapshot);
            float hereDistance = FindBestNavalDropDistance(passengers, objective);
            if (passengers.Count > 0 && hereDistance <= TransportDropOffRange)
            {
                Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} desembarca em {fromCell} " +
                          $"({passengers.Count} passageiro(s)) — objetivo {objective}, dist={hereDistance:F0}h.");
                return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, passengers);
            }
        }

        // 2) Escolher o melhor ponto de desembarque ALCANCAVEL neste turno.
        if (TryFindNavalLandingSite(unit, objective, fromCell, paths, occupied,
                plan, snapshot,
                out Vector3Int landingCell, out List<PodeDesembarcarOption> landingPassengers,
                out float landingDistance))
        {
            if (landingDistance <= TransportDropOffRange)
            {
                Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} move {fromCell}->{landingCell} e desembarca " +
                          $"({landingPassengers.Count} passageiro(s)) — objetivo {objective}, dist={landingDistance:F0}h.");
                return BuildDesembarcarBatch(
                    unit, snapshot.AITeam, fromCell, landingPassengers, landingCell, paths[landingCell]);
            }

            Vector3Int progressionTarget =
                ResolveNavalApproachTarget(unit, objective, fromCell);
            if (progressionTarget != fromCell
                && TryFindBestToolProgressionCell(
                    unit,
                    snapshot,
                    fromCell,
                    progressionTarget,
                    paths,
                    occupied,
                    ToolProgressionIntent.TransportDelivery,
                    out Vector3Int progressionCell,
                    out _,
                    out string progressionReason))
            {
                Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} courier progressao " +
                          $"{fromCell}->{progressionCell} rumo a praia {progressionTarget}; " +
                          $"mantem carga ({progressionReason}).");
                return BuildMoveBatch(
                    unit, snapshot.AITeam, fromCell, progressionCell, paths);
            }

            Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} courier fallback {fromCell}->{landingCell}, " +
                      $"mantem carga — praia ainda esta {landingDistance:F0}h do objetivo {objective}.");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, landingCell, paths);
        }

        // 3) Nenhum ponto de desembarque ao alcance: aproximar. O alvo de rota nao pode ser
        //    o objetivo em terra (mapa reverso vazio), entao usamos a melhor praia CONHECIDA
        //    como destino intermediario — ela e agua, logo alcancavel, e o gradiente volta.
        Vector3Int approachTarget = ResolveNavalApproachTarget(unit, objective, fromCell);
        if (approachTarget != fromCell)
        {
            if (TryFindBestToolProgressionCell(
                    unit,
                    snapshot,
                    fromCell,
                    approachTarget,
                    paths,
                    occupied,
                    ToolProgressionIntent.TransportDelivery,
                    out Vector3Int progressionCell,
                    out _,
                    out string progressionReason))
            {
                Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} courier progressao " +
                          $"{fromCell}->{progressionCell} rumo a praia {approachTarget} " +
                          $"(objetivo {objective}; {progressionReason}).");
                return BuildMoveBatch(
                    unit, snapshot.AITeam, fromCell, progressionCell, paths);
            }

            Vector3Int moveTo = FindTransportMove(unit, fromCell, approachTarget, paths, occupied, snapshot.AITeam);
            if (moveTo != fromCell)
            {
                Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} aproxima {fromCell}->{moveTo} " +
                          $"rumo a praia {approachTarget} (objetivo {objective}).");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTo, paths);
            }
        }

        Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} sem ponto de desembarque alcancavel — aguarda.");
        return null;
    }

    private bool TryBuildReadyAircraftReleaseAfterNavalPosture(
        UnitManager transporter,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int fromCell,
        out PlayerAction action)
    {
        action = null;
        List<UnitManager> passengers = CollectPassengers(transporter);
        var readyAircraft = new List<UnitManager>();
        for (int i = 0; i < passengers.Count; i++)
        {
            UnitManager passenger = passengers[i];
            if (passenger == null
                || passenger.IsUnderRepair
                || !PodeSuprirSensor.IsAirFamilyUnit(passenger))
            {
                continue;
            }

            readyAircraft.Add(passenger);
        }
        if (readyAircraft.Count == 0)
            return false;

        PlayerAction posture = null;
        if (transporter.TryGetUnitData(out UnitData data)
            && data != null)
        {
            if (UnitRoleCompatibility.CanSatisfy(
                    data,
                    UnitRole.FogoIndireto))
            {
                posture = TryDecideFireSupportAction(
                    transporter,
                    snapshot,
                    plan);
            }
            else if (UnitRoleCompatibility.CanSatisfy(
                         data,
                         UnitRole.Assalto))
            {
                posture = TryDecideAssaultAction(
                    transporter,
                    snapshot,
                    plan);
            }

            // Porta-avioes normalmente declara PlayConservative em vez de um
            // papel terrestre de Assalto/Fogo Indireto. Essa e a agenda
            // propria dele: seguir protegido pela retaguarda aliada.
            if (posture == null && data.playConservative)
            {
                Dictionary<Vector3Int, List<Vector3Int>> posturePaths =
                    UnitMovementPathRules.CalcularCaminhosValidos(
                        boardTilemap,
                        transporter,
                        Mathf.Max(
                            0,
                            transporter.RemainingMovementPoints),
                        terrainDatabase);
                posture = TryBuildConservativeRearFollowAction(
                    transporter,
                    snapshot,
                    posturePaths,
                    context:
                        "porta-avioes com aeronaves prontas");
            }
        }

        if (posture == null)
            return false;

        // Ataque ou outra acao propria continua tendo precedencia; a aeronave
        // permanece a bordo ate uma rodada em que o navio possa desembarcar.
        if (posture.SensorAction != SensorActionType.None
            || !posture.HasMoveTo)
        {
            action = posture;
            return true;
        }

        Vector3Int destination = posture.MoveTo;
        destination.z = 0;
        List<PodeDesembarcarOption> options =
            SimulateDisembarkFromCell(
                transporter,
                destination);
        var selected =
            new List<PodeDesembarcarOption>();
        var selectedPassengerIds = new HashSet<int>();
        for (int i = 0; i < options.Count; i++)
        {
            PodeDesembarcarOption option = options[i];
            if (option == null
                || option.passengerUnit == null
                || !readyAircraft.Contains(option.passengerUnit)
                || !selectedPassengerIds.Add(
                    option.passengerUnit.InstanceId))
            {
                continue;
            }

            selected.Add(option);
        }
        if (selected.Count == 0)
        {
            action = posture;
            return true;
        }

        Debug.Log(
            $"{TL("NavalTransport")} {transporter.InstanceId} " +
            $"executa postura propria ate {destination} e autoriza " +
            $"decolagem de {selected.Count} aeronave(s) fora de UnderRepair.");
        action = BuildDesembarcarBatch(
            transporter,
            snapshot.AITeam,
            fromCell,
            selected,
            destination,
            posture.MovementPath);
        return true;
    }

    // -------------------------------------------------------------------------
    // EVAC naval: entregar o ferido no ponto de reparo, nao no objetivo de combate
    // -------------------------------------------------------------------------

    private PlayerAction DecideNavalEvacDeliveryAction(
        UnitManager unit,
        UnitManager evacuee,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        ConstructionManager repairDest = FindRepairConstruction(
            evacuee, fromCell, snapshot.AITeam, new HashSet<Vector3Int>(occupied));
        Vector3Int target = repairDest != null
            ? repairDest.CurrentCellPosition
            : FindTransportWaitTarget(snapshot.AITeam, fromCell);
        target.z = 0;

        // 1) Ja da para largar daqui, perto o bastante e em celula segura?
        List<PodeDesembarcarOption> here = SimulateDisembarkFromCell(unit, fromCell);
        if (here.Count > 0)
        {
            List<PodeDesembarcarOption> selected =
                SelectEvacDisembarkForPassenger(here, evacuee, target, snapshot.AITeam);
            if (selected.Count > 0)
            {
                Vector3Int dropCell = selected[0].disembarkCell;
                dropCell.z = 0;
                float dropDistance = SectorManager.HexDistance(dropCell, target);
                if (dropDistance <= TransportDropOffRange)
                {
                    Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} EVAC desembarca #{evacuee.InstanceId} " +
                              $"em {dropCell} — reparo {target}, dist={dropDistance:F0}h.");
                    return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected);
                }
            }
        }

        // 2) Melhor ponto de desembarque alcancavel neste turno, medido pelo hex onde o
        //    ferido pisa em relacao ao ponto de reparo.
        Vector3Int bestCell = fromCell;
        List<PodeDesembarcarOption> bestSelection = null;
        float bestDistance = float.MaxValue;
        if (paths != null)
        {
            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (cell == fromCell)
                    continue;
                if (occupied != null && occupied.Contains(cell))
                    continue;

                List<PodeDesembarcarOption> options = SimulateDisembarkFromCell(unit, cell);
                if (options.Count == 0)
                    continue;

                List<PodeDesembarcarOption> selected =
                    SelectEvacDisembarkForPassenger(options, evacuee, target, snapshot.AITeam);
                if (selected.Count == 0)
                    continue;

                Vector3Int dropCell = selected[0].disembarkCell;
                dropCell.z = 0;
                float distance = SectorManager.HexDistance(dropCell, target);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestCell = cell;
                bestSelection = selected;
            }
        }

        if (bestSelection != null && bestDistance <= TransportDropOffRange)
        {
            Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} EVAC move {fromCell}->{bestCell} e desembarca " +
                      $"#{evacuee.InstanceId} — reparo {target}, dist={bestDistance:F0}h.");
            return BuildDesembarcarBatch(
                unit, snapshot.AITeam, fromCell, bestSelection, bestCell, paths[bestCell]);
        }

        // 3) Nada ao alcance: aproximar da praia conhecida mais perto do ponto de reparo.
        Vector3Int approachTarget = ResolveNavalApproachTarget(unit, target, fromCell);
        if (approachTarget != fromCell)
        {
            Vector3Int moveTo = FindTransportMove(
                unit, fromCell, approachTarget, paths, occupied, snapshot.AITeam);
            if (moveTo != fromCell)
            {
                Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} EVAC aproxima {fromCell}->{moveTo} " +
                          $"rumo a praia {approachTarget} (reparo {target}) com #{evacuee.InstanceId} a bordo.");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTo, paths);
            }
        }

        Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} EVAC sem ponto de desembarque alcancavel " +
                  $"para #{evacuee.InstanceId} — mantem {fromCell}.");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    // Melhor celula de agua alcancavel de onde o desembarque e valido, pontuada pela
    // distancia entre o hex onde o PASSAGEIRO pisa e o objetivo.
    private bool TryFindNavalLandingSite(
        UnitManager unit,
        Vector3Int objective,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot,
        out Vector3Int bestCell,
        out List<PodeDesembarcarOption> bestPassengers,
        out float bestLandingDistance)
    {
        bestCell = fromCell;
        bestPassengers = null;
        bestLandingDistance = float.MaxValue;
        if (paths == null || paths.Count == 0)
            return false;

        float bestScore = float.MaxValue;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;
            if (occupied != null && occupied.Contains(cell))
                continue;

            List<PodeDesembarcarOption> options = SimulateDisembarkFromCell(unit, cell);
            if (options.Count == 0)
                continue;

            // Score = quao perto do objetivo a tropa consegue pisar a partir dali.
            float landingScore = float.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                PodeDesembarcarOption option = options[i];
                if (option == null) continue;
                Vector3Int target = option.disembarkCell;
                target.z = 0;
                float d = SectorManager.HexDistance(target, objective);
                if (d < landingScore) landingScore = d;
            }

            if (landingScore >= float.MaxValue || landingScore > NavalLandingScoreHorizon)
                continue;

            if (landingScore < bestScore)
            {
                List<PodeDesembarcarOption> passengers =
                    SelectNavalDisembarkOrders(options, plan, snapshot);
                if (passengers.Count == 0)
                    continue;

                bestScore = landingScore;
                bestCell = cell;
                bestPassengers = passengers;
                bestLandingDistance = landingScore;
            }
        }

        return bestPassengers != null && bestPassengers.Count > 0;
    }

    // -------------------------------------------------------------------------
    // Coleta: encontrar praia/porto valido e esperar quem vai embarcar
    // -------------------------------------------------------------------------

    private PlayerAction DecideNavalPickupAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        float evacMaxTransportDistance = -1f,
        UnitManager preferredCandidate = null)
    {
        // EVAC precede o ferry comum. A compatibilidade do paciente vem dos
        // transportSlots do UnitData (FindFittingSlotIndex), permitindo que
        // porta-avioes receba aeronaves em reparo sem hardcode de Domain.Air.
        UnitManager candidate = preferredCandidate;
        if (candidate == null)
            candidate = FindBestEvacCandidate(
                unit, snapshot, fromCell, paths, evacMaxTransportDistance);
        bool evacPickup = candidate != null;
        if (candidate == null)
            candidate = FindNavalPickupCandidate(unit, snapshot, plan);
        if (preferredCandidate != null)
            evacPickup = preferredCandidate.IsUnderRepair;
        if (candidate == null)
        {
            Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} sem candidato de embarque — aguarda.");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        Vector3Int candidateCell = candidate.CurrentCellPosition;
        candidateCell.z = 0;
        HashSet<Vector3Int> passengerReachable = BuildPassengerReachableSet(candidate);
        bool hasRendezvous = TryFindNavalPickupRendezvous(
            unit,
            candidateCell,
            passengerReachable,
            out Vector3Int rendezvousCell);

        // Ja estamos num ponto de encontro valido? Entao ESPERAR e a acao correta — o
        // embarque e acao do passageiro, e sair do lugar so atrasaria o encontro.
        if (IsNavalPickupCell(unit, fromCell)
            && CanPassengerReachEmbarkStopForTransporterCell(
                fromCell, passengerReachable))
        {
            Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} aguarda " +
                      $"{(evacPickup ? "EVAC" : "embarque")} em {fromCell} " +
                      $"(candidato #{candidate.InstanceId}@{candidateCell}).");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        // Melhor ponto de encontro alcancavel: aquele mais perto de quem vai embarcar.
        Vector3Int bestCell = fromCell;
        float bestDist = float.MaxValue;

        if (paths != null)
        {
            foreach (Vector3Int rawCell in paths.Keys)
            {
                Vector3Int cell = rawCell;
                cell.z = 0;
                if (cell == fromCell) continue;
                // Nao use o occupied generico aqui: unidade naval e passageiro
                // terrestre podem coexistir justamente na praia de embarque.
                // O sensor do transportador e a fonte canonica para decidir se
                // esta celula aceita coleta segundo o UnitData.
                if (!CanUseTransporterPickupCell(unit, snapshot.AITeam, cell)) continue;
                if (!CanPassengerReachEmbarkStopForTransporterCell(
                        cell, passengerReachable)) continue;

                float d = SectorManager.HexDistance(cell, candidateCell);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestCell = cell;
                }
            }
        }

        if (bestCell != fromCell)
        {
            Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} move {fromCell}->{bestCell} para receber " +
                      $"{(evacPickup ? "EVAC " : string.Empty)}" +
                      $"#{candidate.InstanceId}@{candidateCell}.");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestCell, paths);
        }

        // Nenhum ponto de encontro ao alcance neste turno: a progressao deve mirar
        // a PRAIA valida, nunca a celula crua do passageiro. Hexes de mar podem ser
        // apenas transito; nao se tornam local de espera/pickup.
        Vector3Int progressionTarget = hasRendezvous ? rendezvousCell : candidateCell;
        HashSet<Vector3Int> navalPickupOccupied = occupied;
        if (hasRendezvous && occupied != null && occupied.Contains(rendezvousCell))
        {
            // A ocupacao terrestre do passageiro nao bloqueia a unidade naval no
            // rendezvous que o proprio sensor aprovou.
            navalPickupOccupied = new HashSet<Vector3Int>(occupied);
            navalPickupOccupied.Remove(rendezvousCell);
        }
        Vector3Int moveTo = fromCell;
        string routeReason = string.Empty;
        bool hasValidatedProgression = hasRendezvous
            && TryFindBestToolProgressionCell(
                unit,
                snapshot,
                fromCell,
                progressionTarget,
                paths,
                navalPickupOccupied,
                ToolProgressionIntent.TransportRendezvous,
                out moveTo,
                out _,
                out routeReason);
        if (moveTo != fromCell)
        {
            string operationLabel = evacPickup ? "EVAC" : "pickup";
            string movementKind = moveTo == rendezvousCell
                ? $"chega ao ponto de encontro de {operationLabel}"
                : $"transita rumo ao ponto de encontro de {operationLabel}";
            string targetLabel = evacPickup ? "paciente" : "passageiro";
            Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} {movementKind} " +
                      $"{fromCell}->{moveTo} encontro={rendezvousCell} " +
                      $"{targetLabel}=#{candidate.InstanceId}@{candidateCell} " +
                      $"({routeReason}).");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTo, paths);
        }

        if (!hasRendezvous)
        {
            Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} sem praia permitida por " +
                      $"Allow Embark When Transporter At alcancavel pelo passageiro " +
                      $"#{candidate.InstanceId}@{candidateCell}; mantem {fromCell}.");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        if (!hasValidatedProgression)
        {
            Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} sem progressao validada por " +
                      $"CaminhosValidos rumo a praia {rendezvousCell}; mantem {fromCell}.");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} sem aproximacao naval valida para " +
                  $"#{candidate.InstanceId}@{candidateCell} — mantem pickup em {fromCell}.");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    private bool TryFindNavalPickupRendezvous(
        UnitManager transporter,
        Vector3Int passengerCell,
        HashSet<Vector3Int> passengerReachable,
        out Vector3Int bestCell)
    {
        bestCell = Vector3Int.zero;
        if (transporter == null || boardTilemap == null)
            return false;

        Vector3Int shipCell = transporter.CurrentCellPosition;
        shipCell.z = 0;
        passengerCell.z = 0;
        float bestScore = float.MaxValue;
        BoundsInt bounds = boardTilemap.cellBounds;

        foreach (Vector3Int rawCell in bounds.allPositionsWithin)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (!boardTilemap.HasTile(cell))
                continue;
            if (!IsNavalPickupCell(transporter, cell))
                continue;
            if (!CanPassengerReachEmbarkStopForTransporterCell(
                    cell, passengerReachable))
                continue;

            float passengerDistance = SectorManager.HexDistance(passengerCell, cell);
            float shipDistance = SectorManager.HexDistance(shipCell, cell);
            float score = passengerDistance * 1000f + shipDistance;
            if (score >= bestScore)
                continue;

            bestScore = score;
            bestCell = cell;
        }

        return bestScore < float.MaxValue;
    }

    // O hex serve de ponto de encontro? Quem responde e a ficha do proprio navio
    // (praia, porto, doca...), pelo mesmo helper que o shuttle aereo ja usa — ele aceita
    // a celula por parametro, sem precisar deslocar a unidade para simular.
    private bool IsNavalPickupCell(UnitManager unit, Vector3Int cell)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return false;

        cell.z = 0;
        return PodeEmbarcarSensor.IsTransporterCellValidForEmbark(
            boardTilemap, terrainDatabase, data, cell);
    }

    // -------------------------------------------------------------------------
    // Alvos e helpers
    // -------------------------------------------------------------------------

    private Vector3Int ResolveNavalDeliveryObjective(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        // Uma vez carregado, o navio e courier: quem determina o destino e o
        // passageiro, pela mesma resolucao usada por APC e transporte aereo.
        List<UnitManager> passengers = CollectPassengers(unit);
        if (passengers.Count > 0)
        {
            UnitManager primaryPassenger = ResolvePrimaryPassenger(unit, passengers, plan);
            Vector3Int fallbackCell = unit.CurrentCellPosition;
            fallbackCell.z = 0;
            if (TryResolveCourierPassengerTarget(
                    primaryPassenger, plan, snapshot, Vector3Int.zero,
                    fallbackCell, out Vector3Int passengerTarget))
                return passengerTarget;
        }

        SectorObjective assigned = plan != null ? ResolveAssignedTransportObjective(unit, plan) : null;
        if (assigned != null)
        {
            // O objetivo aponta para um SETOR; a celula util e o predio capturavel dele —
            // mesmo caminho que o transporte aereo usa para resolver destino.
            ConstructionManager target = FindCapturableInSector(assigned.Sector, snapshot.AITeam);
            if (target != null)
            {
                Vector3Int targetCell = target.CurrentCellPosition;
                targetCell.z = 0;
                return targetCell;
            }
        }

        // Facção sem QG: sem setor atribuido, o alvo e o capturavel mais proximo — mesmo
        // criterio do rebelde a pe e do courier terrestre/aereo. Sem isto o navio cairia no
        // QG inimigo abaixo, o mesmo funil que a rebelde nao deve seguir.
        if (ConstructionManager.IsHeadQuarterlessTeam(snapshot.AITeam))
        {
            Vector3Int shipCell = unit.CurrentCellPosition; shipCell.z = 0;
            ConstructionManager rebelTarget = FindNearestPlanlessCaptureTarget(unit, snapshot, shipCell);
            if (rebelTarget != null)
            {
                Vector3Int rc = rebelTarget.CurrentCellPosition; rc.z = 0;
                return rc;
            }
            return shipCell; // sem alvo a vista: nao marcha para o QG inimigo
        }

        if (snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition;
            hq.z = 0;
            return hq;
        }

        Vector3Int fallback = unit.CurrentCellPosition;
        fallback.z = 0;
        return fallback;
    }

    // Dois objetivos podem compartilhar o mesmo navio quando suas cabeças de praia
    // pertencem ao mesmo corredor naval. Mede a melhor ordem de entrega e aceita
    // no máximo um turno de movimento adicional sobre a viagem ao destino mais longe.
    private bool AreNavalEmbarkRoutesCompatible(
        UnitManager transporter,
        SectorObjective passengerObjective,
        SectorObjective transporterObjective,
        AIWorldSnapshot snapshot)
    {
        if (transporter == null || passengerObjective == null
            || transporterObjective == null || snapshot == null)
            return false;
        if (transporter.GetDomain() != Domain.Naval)
            return false;

        ConstructionManager passengerBuilding =
            FindCapturableInSector(passengerObjective.Sector, snapshot.AITeam);
        ConstructionManager primaryBuilding =
            FindCapturableInSector(transporterObjective.Sector, snapshot.AITeam);
        if (passengerBuilding == null || primaryBuilding == null)
            return false;

        Vector3Int shipCell = transporter.CurrentCellPosition;
        Vector3Int passengerTarget = passengerBuilding.CurrentCellPosition;
        Vector3Int primaryTarget = primaryBuilding.CurrentCellPosition;
        shipCell.z = passengerTarget.z = primaryTarget.z = 0;

        Vector3Int passengerBeach =
            ResolveNavalApproachTarget(transporter, passengerTarget, shipCell);
        Vector3Int primaryBeach =
            ResolveNavalApproachTarget(transporter, primaryTarget, shipCell);
        if (passengerBeach == shipCell || primaryBeach == shipCell)
            return false;

        float shipToPassenger =
            CalculateRouteDistanceOrHex(transporter, shipCell, passengerBeach);
        float shipToPrimary =
            CalculateRouteDistanceOrHex(transporter, shipCell, primaryBeach);
        float between =
            CalculateRouteDistanceOrHex(transporter, passengerBeach, primaryBeach);
        float combined = Mathf.Min(
            shipToPassenger + between,
            shipToPrimary + between);
        float directToFarther = Mathf.Max(shipToPassenger, shipToPrimary);
        float detour = combined - directToFarther;
        float allowedDetour = Mathf.Max(1, transporter.MaxMovementPoints);
        bool compatible = detour <= allowedDetour + 0.01f;

        Debug.Log($"{TL("NavalTransport")} rota compartilhada {transporter.InstanceId}: " +
                  $"{transporterObjective.Sector}@{primaryBeach} + " +
                  $"{passengerObjective.Sector}@{passengerBeach} " +
                  $"detour={detour:F1} limite={allowedDetour:F1} compatible={compatible}.");
        return compatible;
    }

    private static float FindBestNavalDropDistance(
        List<PodeDesembarcarOption> options,
        Vector3Int objective)
    {
        float best = float.MaxValue;
        if (options == null)
            return best;

        objective.z = 0;
        for (int i = 0; i < options.Count; i++)
        {
            PodeDesembarcarOption option = options[i];
            if (option == null)
                continue;
            Vector3Int dropCell = option.disembarkCell;
            dropCell.z = 0;
            best = Mathf.Min(best, SectorManager.HexDistance(dropCell, objective));
        }
        return best;
    }

    // Praia conhecida mais perto do objetivo, usada como destino intermediario quando
    // nenhum ponto de desembarque esta ao alcance neste turno. A busca nasce NO
    // objetivo e expande em bolhas ate encontrar a primeira faixa de praias/LZs
    // validas. Assim um mapa oceanico nao paga PodeDesembarcar em cada celula
    // alcancavel entre a origem e o extremo oposto do tabuleiro.
    private Vector3Int ResolveNavalApproachTarget(UnitManager unit, Vector3Int objective, Vector3Int fromCell)
    {
        Dictionary<Vector3Int, int> reachable = UnitMovementPathRules.CalculateMovementCostMap(
            boardTilemap, unit, fromCell, 120, terrainDatabase);
        if (reachable == null || reachable.Count == 0)
            return fromCell;

        objective.z = 0;
        fromCell.z = 0;
        Vector3Int best = fromCell;
        int bestRouteCost = int.MaxValue;
        int bestBubbleDepth = int.MaxValue;
        var queue = new Queue<Vector3Int>();
        var depthByCell = new Dictionary<Vector3Int, int>();
        var neighbors = new List<Vector3Int>(6);
        queue.Enqueue(objective);
        depthByCell[objective] = 0;

        // Limite defensivo; na pratica a busca termina na primeira costa ao
        // redor da construcao alvo.
        const int maxObjectiveBubbleCells = 4096;
        while (queue.Count > 0
               && depthByCell.Count <= maxObjectiveBubbleCells)
        {
            Vector3Int cell = queue.Dequeue();
            cell.z = 0;
            int depth = depthByCell[cell];
            if (bestBubbleDepth < int.MaxValue && depth > bestBubbleDepth)
                break;

            if (reachable.TryGetValue(cell, out int routeCost)
                && IsConfirmedVisibleCellForAI(cell))
            {
                List<UnitManager> occupants =
                    UnitOccupancyRules.GetUnitsAtCell(
                        boardTilemap, cell, unit);
                bool canStop = OccupancyResolver.CanEndMove(
                    unit, cell, occupants);
                bool isMeetingCell = canStop
                    && (SimulateDisembarkFromCell(unit, cell).Count > 0
                        || IsNavalPickupCell(unit, cell));
                if (isMeetingCell)
                {
                    bestBubbleDepth = depth;
                    if (routeCost < bestRouteCost)
                    {
                        bestRouteCost = routeCost;
                        best = cell;
                    }
                }
            }

            if (bestBubbleDepth < int.MaxValue)
                continue;

            neighbors.Clear();
            UnitMovementPathRules.GetImmediateHexNeighbors(
                boardTilemap, cell, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                if (depthByCell.ContainsKey(next))
                    continue;
                depthByCell[next] = depth + 1;
                queue.Enqueue(next);
            }
        }

        Debug.Log($"{TL("NavalTransport")} ancora costeira objetivo={objective} " +
                  $"LZ={best} bolha=" +
                  $"{(bestBubbleDepth < int.MaxValue ? bestBubbleDepth.ToString() : "none")} " +
                  $"rota={(bestRouteCost < int.MaxValue ? bestRouteCost.ToString() : "?")} " +
                  $"visitadas={depthByCell.Count}.");
        return best;
    }

    private UnitManager FindNavalPickupCandidate(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (snapshot?.MyUnits == null) return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        // O pickup naval honra primeiro o mesmo pareamento 1:1 do APC. A regra vale
        // tanto para faccoes com QG quanto para faccoes sem QG: o que manda e o plano,
        // nao o tipo de controlador que esta executando o batch.
        SectorObjective assigned = plan != null
            ? ResolveAssignedTransportObjective(unit, plan)
            : null;
        if (assigned != null)
        {
            UnitManager formalPassenger =
                ResolveAssignedPassengerForTransporter(unit, snapshot, plan, assigned);
            if (formalPassenger != null
                && !formalPassenger.IsDead
                && !formalPassenger.IsEmbarked
                && CanNavalTransporterCarry(unit, formalPassenger))
                return formalPassenger;
        }

        // Sem passageiro formal, reaproveita a escolha orientada a objetivo do shuttle.
        UnitManager shuttleCandidate =
            FindBestShuttleCandidate(unit, snapshot, plan, fromCell, out _, assigned);
        if (shuttleCandidate != null && CanNavalTransporterCarry(unit, shuttleCandidate))
            return shuttleCandidate;

        // Nao transforme qualquer unidade compativel em passageiro. Esse
        // fallback capturava, por exemplo, um aviao-tanque saudavel ao lado do
        // porta-avioes apenas porque havia vaga. Pickup normal exige demanda
        // orientada a objetivo; paciente IsUnderRepair ja foi tratado pelo EVAC.
        return null;
    }

    // Ha vaga compativel com este passageiro? Reaproveita FindFittingSlotIndex (shuttle),
    // que ja resolve classe, skill, camada e ocupacao pelo sensor — nada reimplementado.
    private static bool CanNavalTransporterCarry(UnitManager transporter, UnitManager passenger)
    {
        if (transporter == null || passenger == null) return false;
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null) return false;
        if (!passenger.TryGetUnitData(out UnitData passengerData) || passengerData == null) return false;
        if (!UnitRoleCompatibility.ParticipatesInBattle(passengerData)) return false;

        int slot = FindFittingSlotIndex(transporter, transporterData, passenger, passengerData);
        return slot >= 0 && transporter.CanUseTransportSlotExclusivity(slot, out _);
    }

    // Uma ordem por passageiro: o sensor emite varias opcoes para o mesmo embarcado (uma
    // por hex de destino possivel) e o batch espera uma ordem por unidade.
    private List<PodeDesembarcarOption> SelectNavalDisembarkOrders(
        List<PodeDesembarcarOption> options,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot)
    {
        var orders = new List<PodeDesembarcarOption>();
        if (options == null) return orders;
        if (IsRuntimeRebelSnapshot(snapshot))
        {
            List<PodeDesembarcarOption> rebelOrders =
                SelectRebelDisembarkOrdersByDistinctTargets(
                options, snapshot, TransportDropOffRange);
            UnitManager rebelTransporter = options.Find(
                option => option?.transporterUnit != null)?.transporterUnit;
            List<UnitManager> rebelPassengers =
                CollectPassengers(rebelTransporter);
            if (ShouldRejectPartialRebelOrRogueSelection(
                    rebelTransporter,
                    rebelOrders,
                    rebelPassengers,
                    plan,
                    snapshot,
                    Vector3Int.zero,
                    "Naval fallback rebelde"))
                return orders;
            return rebelOrders;
        }

        var passengers = new HashSet<UnitManager>();
        for (int i = 0; i < options.Count; i++)
        {
            PodeDesembarcarOption option = options[i];
            if (option?.passengerUnit == null) continue;
            passengers.Add(option.passengerUnit);
        }

        foreach (UnitManager passenger in passengers)
        {
            bool targetFound = TryResolveCourierPassengerTarget(
                passenger, plan, snapshot, Vector3Int.zero,
                passenger.CurrentCellPosition, out Vector3Int target);
            PodeDesembarcarOption best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < options.Count; i++)
            {
                PodeDesembarcarOption option = options[i];
                if (option?.passengerUnit != passenger)
                    continue;
                Vector3Int dropCell = option.disembarkCell;
                dropCell.z = 0;
                float distance = targetFound
                    ? SectorManager.HexDistance(dropCell, target)
                    : 0f;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = option;
                }
            }

            if (best == null)
                continue;
            if (targetFound && bestDistance > TransportDropOffRange)
            {
                Debug.Log($"{TL("NavalTransport")} passageiro #{passenger.InstanceId} permanece a bordo: " +
                          $"melhor desembarque {best.disembarkCell} ainda esta " +
                          $"{bestDistance:F0}h do objetivo {target}.");
                continue;
            }
            orders.Add(best);
        }

        UnitManager transporter = options.Find(
            option => option?.transporterUnit != null)?.transporterUnit;
        List<UnitManager> carriedPassengers = CollectPassengers(transporter);
        if (ShouldRejectPartialRebelOrRogueSelection(
                transporter,
                orders,
                carriedPassengers,
                plan,
                snapshot,
                Vector3Int.zero,
                "Naval fallback"))
            orders.Clear();

        return orders;
    }
}
