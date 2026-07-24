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

        if (hasCargo)
            return DecideNavalDeliveryAction(unit, snapshot, plan, fromCell, paths, occupied);

        return DecideNavalPickupAction(unit, snapshot, plan, fromCell, paths, occupied);
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
        Vector3Int objective = ResolveNavalDeliveryObjective(unit, snapshot, plan);

        // 1) Ja da para largar daqui? O sensor manda — se ha desembarque valido na celula
        //    atual, a entrega acontece, sem exigir proximidade do objetivo. Chegar a costa
        //    certa ja foi o trabalho; a tropa segue por conta a partir dali.
        List<PodeDesembarcarOption> here = SimulateDisembarkFromCell(unit, fromCell);
        if (here.Count > 0)
        {
            List<PodeDesembarcarOption> passengers = SelectNavalDisembarkOrders(here);
            if (passengers.Count > 0)
            {
                Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} desembarca em {fromCell} " +
                          $"({passengers.Count} passageiro(s)) — objetivo {objective}.");
                return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, passengers);
            }
        }

        // 2) Escolher o melhor ponto de desembarque ALCANCAVEL neste turno.
        if (TryFindNavalLandingSite(unit, objective, fromCell, paths, occupied,
                out Vector3Int landingCell, out List<PodeDesembarcarOption> landingPassengers))
        {
            Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} move {fromCell}->{landingCell} e desembarca " +
                      $"({landingPassengers.Count} passageiro(s)) — objetivo {objective}.");
            return BuildDesembarcarBatch(
                unit, snapshot.AITeam, fromCell, landingPassengers, landingCell, paths[landingCell]);
        }

        // 3) Nenhum ponto de desembarque ao alcance: aproximar. O alvo de rota nao pode ser
        //    o objetivo em terra (mapa reverso vazio), entao usamos a melhor praia CONHECIDA
        //    como destino intermediario — ela e agua, logo alcancavel, e o gradiente volta.
        Vector3Int approachTarget = ResolveNavalApproachTarget(unit, objective, fromCell);
        if (approachTarget != fromCell)
        {
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

    // Melhor celula de agua alcancavel de onde o desembarque e valido, pontuada pela
    // distancia entre o hex onde o PASSAGEIRO pisa e o objetivo.
    private bool TryFindNavalLandingSite(
        UnitManager unit,
        Vector3Int objective,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out List<PodeDesembarcarOption> bestPassengers)
    {
        bestCell = fromCell;
        bestPassengers = null;
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
                List<PodeDesembarcarOption> passengers = SelectNavalDisembarkOrders(options);
                if (passengers.Count == 0)
                    continue;

                bestScore = landingScore;
                bestCell = cell;
                bestPassengers = passengers;
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
        HashSet<Vector3Int> occupied)
    {
        UnitManager candidate = FindNavalPickupCandidate(unit, snapshot, plan);
        if (candidate == null)
        {
            Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} sem candidato de embarque — aguarda.");
            return null;
        }

        Vector3Int candidateCell = candidate.CurrentCellPosition;
        candidateCell.z = 0;

        // Ja estamos num ponto de encontro valido? Entao ESPERAR e a acao correta — o
        // embarque e acao do passageiro, e sair do lugar so atrasaria o encontro.
        if (IsNavalPickupCell(unit, fromCell))
        {
            Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} aguarda embarque em {fromCell} " +
                      $"(candidato #{candidate.InstanceId}@{candidateCell}).");
            return null;
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
                if (occupied != null && occupied.Contains(cell)) continue;
                if (!IsNavalPickupCell(unit, cell)) continue;

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
                      $"#{candidate.InstanceId}@{candidateCell}.");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, bestCell, paths);
        }

        // Nenhum ponto de encontro ao alcance: aproximar do candidato pelo mar.
        Vector3Int moveTo = FindTransportMove(unit, fromCell, candidateCell, paths, occupied, snapshot.AITeam);
        if (moveTo != fromCell)
        {
            Debug.Log($"{TL("NavalTransport")} {unit.InstanceId} aproxima {fromCell}->{moveTo} " +
                      $"de #{candidate.InstanceId}@{candidateCell}.");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTo, paths);
        }

        return null;
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
            ConstructionManager rebelTarget = FindNearestRebelCaptureTarget(unit, snapshot, shipCell);
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

    // Praia conhecida mais perto do objetivo, usada como destino intermediario quando
    // nenhum ponto de desembarque esta ao alcance neste turno. Varre o entorno do
    // objetivo porque e ali que a cabeca de praia interessa.
    private Vector3Int ResolveNavalApproachTarget(UnitManager unit, Vector3Int objective, Vector3Int fromCell)
    {
        Dictionary<Vector3Int, int> reachable = UnitMovementPathRules.CalculateMovementCostMap(
            boardTilemap, unit, fromCell, 120, terrainDatabase);
        if (reachable == null || reachable.Count == 0)
            return fromCell;

        Vector3Int best = fromCell;
        float bestDist = float.MaxValue;

        foreach (KeyValuePair<Vector3Int, int> pair in reachable)
        {
            Vector3Int cell = pair.Key;
            cell.z = 0;
            float d = SectorManager.HexDistance(cell, objective);
            if (d >= bestDist)
                continue;

            // So interessa agua que sirva de ponto de encontro com a terra.
            if (SimulateDisembarkFromCell(unit, cell).Count == 0 && !IsNavalPickupCell(unit, cell))
                continue;

            bestDist = d;
            best = cell;
        }

        return best;
    }

    private UnitManager FindNavalPickupCandidate(UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (snapshot?.MyUnits == null) return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        UnitManager best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < snapshot.MyUnits.Count; i++)
        {
            UnitManager ally = snapshot.MyUnits[i];
            if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked)
                continue;
            if (!CanNavalTransporterCarry(unit, ally))
                continue;

            Vector3Int allyCell = ally.CurrentCellPosition;
            allyCell.z = 0;
            float d = SectorManager.HexDistance(fromCell, allyCell);
            if (d < bestDist)
            {
                bestDist = d;
                best = ally;
            }
        }

        return best;
    }

    // Ha vaga compativel com este passageiro? Reaproveita FindFittingSlotIndex (shuttle),
    // que ja resolve classe, skill, camada e ocupacao pelo sensor — nada reimplementado.
    private static bool CanNavalTransporterCarry(UnitManager transporter, UnitManager passenger)
    {
        if (transporter == null || passenger == null) return false;
        if (!transporter.TryGetUnitData(out UnitData transporterData) || transporterData == null) return false;
        if (!passenger.TryGetUnitData(out UnitData passengerData) || passengerData == null) return false;

        int slot = FindFittingSlotIndex(transporter, transporterData, passenger, passengerData);
        return slot >= 0 && transporter.CanUseTransportSlotExclusivity(slot, out _);
    }

    // Uma ordem por passageiro: o sensor emite varias opcoes para o mesmo embarcado (uma
    // por hex de destino possivel) e o batch espera uma ordem por unidade.
    private static List<PodeDesembarcarOption> SelectNavalDisembarkOrders(List<PodeDesembarcarOption> options)
    {
        var orders = new List<PodeDesembarcarOption>();
        if (options == null) return orders;

        var seen = new HashSet<UnitManager>();
        for (int i = 0; i < options.Count; i++)
        {
            PodeDesembarcarOption option = options[i];
            if (option?.passengerUnit == null) continue;
            if (!seen.Add(option.passengerUnit)) continue;
            orders.Add(option);
        }

        return orders;
    }
}
