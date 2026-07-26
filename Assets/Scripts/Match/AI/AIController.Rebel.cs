using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Facção sem QG (rebeldes/dissidentes).
    //
    // O planner normal assume o triangulo ancora -> eixo -> setor: ele nasce do proprio
    // QG e aponta para o inimigo. A rebelde nao tem nenhuma das pontas, entao cai por
    // todos os buracos e vira "todo passageiro rogue rumo ao QG inimigo" — um funil unico
    // que ignora o mapa inteiro.
    //
    // O comportamento dela e outro, e mais simples: nao ha plano, ha proximidade. Cada
    // unidade captura o que estiver mais perto; se ja tem alguem indo naquele predio,
    // varre a bolha seguinte e vai para o proximo. Sem eixo, sem massa, sem produção —
    // coerente com o conceito (captura livre, nunca produz, imune a derrota por 0).
    //
    // Este caminho e um curto-circuito ANTES do planner. Reaproveita os mesmos sensores
    // e batches de captura das outras unidades; o que muda e so a ESCOLHA do alvo, que
    // passa a ser geografica em vez de vir do plano.
    // -------------------------------------------------------------------------

    private PlayerAction TryDecideRebelAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        if (unit == null || snapshot == null)
            return null;
        PlayerSlotId rebelSlot = PlayerSlotId.FromIndex(snapshot.AISlotIndex);
        if (matchController == null || !matchController.IsSlotRebel(rebelSlot))
            return null;

        // Transporte e logistica rebeldes seguem seus proprios caminhos (o aereo/naval ja
        // resolvem entrega por conta). Aqui tratamos apenas quem CAPTURA — que e a razao
        // de existir da facção. Unidades sem papel de captura caem no fluxo normal, que
        // para elas ja funciona (o funil-para-o-QG so afeta capturadores rogue).
        if (!unit.TryGetUnitData(out UnitData unitData) || unitData == null
            || !UnitRoleCompatibility.CanSatisfy(unitData, UnitRole.Capturador))
            return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths = UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap, unit, unit.RemainingMovementPoints, terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        // 1) Combate oportunista vem antes da marcha de captura. A faccao sem QG
        // continua orientada por proximidade, mas nao ignora um inimigo que o
        // sensor confirma que pode atacar nesta rodada.
        if (TryBuildRolePreemptiveAttack(
                unit,
                snapshot,
                paths,
                occupied,
                defensiveContext: false,
                out PlayerAction attackAction,
                out string attackReason))
        {
            Debug.Log($"{TL("Rebelde")} {unit.InstanceId} ataque oportunista antes da captura - {attackReason}");
            return attackAction;
        }

        // 2) Capturar agora, se ja estamos em cima de um capturavel valido. O sensor decide
        //    — nao inventamos elegibilidade aqui.
        if (SimulateCaptureSensor(unit, fromCell, out _))
        {
            // Reserva a propria celula: outro rebelde (a pe ou transportado) nesta mesma
            // passada nao deve escolher este predio como alvo.
            plannedDestinations.Add(fromCell);
            rebelCaptureTargetReservations.Add(fromCell);
            Debug.Log($"{TL("Rebelde")} {unit.InstanceId} captura em {fromCell}.");
            return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        // 3) Alvo por PROXIMIDADE, pulando o que ja tem rebelde a caminho.
        //    rebelCaptureTargetReservations e a bolha que evita o empilhamento:
        //    dois rebeldes nao marcham para o mesmo predio distante.
        ConstructionManager target = FindNearestRebelCaptureTarget(unit, snapshot, fromCell);
        if (target == null)
        {
            Debug.Log($"{TL("Rebelde")} {unit.InstanceId} sem capturavel alcancavel na visao — cai no fluxo normal.");
            return null;
        }

        Vector3Int targetCell = target.CurrentCellPosition;
        targetCell.z = 0;

        // Mesma fronteira usada pelo APC: se o capturador alcanca um predio
        // livre em ate duas rodadas do proprio movimento, marcha em vez de
        // reembarcar logo depois de desembarcar.
        int walkBudget =
            ResolvePassengerWalkWithoutTransportBudget(unit);
        int walkCost = TerrainCostToCell(
            unit,
            fromCell,
            targetCell,
            walkBudget);
        bool reachesOnFoot =
            walkCost <= walkBudget;

        if (!reachesOnFoot)
        {
            PlayerAction embarkAction =
                TryDecideCapturerEmbarkAction(unit, snapshot, plan);
            if (embarkAction != null)
            {
                Debug.Log($"{TL("Rebelde")} {unit.InstanceId} aceita embarque: " +
                          $"objetivo livre {targetCell} nao alcancavel a pe " +
                          $"em {TransportPassengerWalkTurns} turnos " +
                          $"(move={unit.MaxMovementPoints}, budget={walkBudget}, custo={walkCost}).");
                return embarkAction;
            }
        }
        else
        {
            Debug.Log($"{TL("Rebelde")} {unit.InstanceId} ignora embarque: " +
                      $"objetivo livre {targetCell} alcancavel a pe " +
                      $"custo={walkCost}<={walkBudget} " +
                      $"({TransportPassengerWalkTurns} turnos x move={unit.MaxMovementPoints}).");
        }

        rebelCaptureTargetReservations.Add(targetCell);
        Debug.Log($"{TL("Rebelde")} {unit.InstanceId} reserva objetivo {targetCell} por proximidade.");

        // 5) Se um passo deste turno ja encosta no capturavel (destino = o proprio predio
        //    ou adjacente valido), tenta capturar/avancar; senao, marcha na direcao dele.
        Vector3Int approach = FindRebelApproachCell(unit, targetCell, fromCell, paths, occupied);
        if (approach == fromCell)
        {
            Debug.Log($"{TL("Rebelde")} {unit.InstanceId} sem avanço util rumo a {targetCell} — aguarda.");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
        }

        if (SimulateCaptureSensor(unit, approach, out _))
        {
            plannedDestinations.Add(approach);
            Debug.Log($"{TL("Rebelde")} {unit.InstanceId} move {fromCell}->{approach} e captura {targetCell}.");
            return BuildCaptureBatch(unit, snapshot.AITeam, fromCell, approach, paths);
        }

        plannedDestinations.Add(approach);
        Debug.Log($"{TL("Rebelde")} {unit.InstanceId} marcha {fromCell}->{approach} rumo a {targetCell}.");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, approach, paths);
    }

    // Capturavel mais proximo (distancia de hex) que ja nao esteja sendo tratado. Ondas
    // concentricas na pratica: o mais perto primeiro, e se ele ja tem dono, o de-fora
    // seguinte assume. Duas razoes para pular um predio:
    //   1) um aliado ja esta EM CIMA dele — capturando, e persiste entre turnos (o caso
    //      que o helicoptero ignorava: a tropa a pe estava la desde o turno anterior);
    //   2) outro rebelde reservou o hex NESTA passada da Fase 2 (coordena a pe + transporte
    //      no mesmo turno, antes de qualquer um chegar).
    private ConstructionManager FindNearestRebelCaptureTarget(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell)
    {
        // Celulas onde ja ha aliado parado — proxy de "predio ja sendo capturado por nos".
        HashSet<Vector3Int> allyCells = new HashSet<Vector3Int>();
        if (snapshot.MyUnits != null)
        {
            for (int i = 0; i < snapshot.MyUnits.Count; i++)
            {
                UnitManager ally = snapshot.MyUnits[i];
                if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked)
                    continue;
                Vector3Int ac = ally.CurrentCellPosition; ac.z = 0;
                allyCells.Add(ac);
            }
        }

        ConstructionManager best = null;
        float bestDist = float.MaxValue;

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null)
                continue;
            if (construction.TeamId == snapshot.AITeam)
                continue;
            if (!IsRebelCapturable(unit, construction))
                continue;

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;

            // Ja ha aliado capturando aqui (persiste entre turnos), ou outro rebelde
            // reservou este objetivo nesta passada? Pula para a bolha seguinte.
            if (allyCells.Contains(cell) || rebelCaptureTargetReservations.Contains(cell))
                continue;

            float dist = SectorManager.HexDistance(fromCell, cell);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = construction;
            }
        }

        return best;
    }

    // Capturavel para a rebelde: predio nao-aliado que o motor deixa este time tomar.
    // A regra de elegibilidade (rebelde ignora pre-requisito de progressao) vive no
    // MatchController; aqui so a consultamos.
    private bool IsRebelCapturable(UnitManager unit, ConstructionManager construction)
    {
        if (construction == null || matchController == null)
            return false;
        if (!construction.TryResolveConstructionData(out ConstructionData data) || data == null)
            return false;

        return matchController.CanCaptureConstruction(
            PlayerSlotId.FromIndex(unit.SlotIndex), data, out _);
    }

    // Celula alcancavel neste turno que mais aproxima do alvo. Prioriza pisar no proprio
    // predio (captura no mesmo passo) e, na falta, o hex de menor distancia de hex ate ele.
    private Vector3Int FindRebelApproachCell(
        UnitManager unit,
        Vector3Int targetCell,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied)
    {
        if (paths == null || paths.Count == 0)
            return fromCell;

        Vector3Int best = fromCell;
        float bestDist = SectorManager.HexDistance(fromCell, targetCell);

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;
            if (occupied != null && occupied.Contains(cell))
                continue;
            if (plannedDestinations.Contains(cell))
                continue;

            float dist = SectorManager.HexDistance(cell, targetCell);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = cell;
            }
        }

        return best;
    }
}
